using CycliqueShareTracker.Application.Models;

namespace CycliqueShareTracker.Application.Trading;

public sealed class BacktestEngine
{
    private readonly PositionEngine _positionEngine = new();

    public BacktestResult Run(BacktestParameters parameters, IReadOnlyList<PriceBar> prices, IReadOnlyList<ComputedIndicator> indicators)
    {
        if (prices.Count < 2 || indicators.Count < 2)
        {
            return BuildEmpty(parameters);
        }

        var indicatorsByDate = indicators.ToDictionary(x => x.Date);
        var orderedPrices = prices.OrderBy(x => x.Date).ToList();
        var trades = new List<BacktestTrade>();
        var markers = new List<BacktestSignalPoint>();
        var equityCurve = new List<BacktestEquityPoint>();
        var openPosition = SimulatedPosition.Empty;
        var cash = parameters.InitialCapital;
        var daysSinceBuyChange = 0;
        var daysSinceSellChange = 0;

        equityCurve.Add(new BacktestEquityPoint(orderedPrices[0].Date, cash));

        for (var i = 1; i < orderedPrices.Count; i++)
        {
            var currentBar = orderedPrices[i];
            var previousBar = orderedPrices[i - 1];
            if (!indicatorsByDate.TryGetValue(currentBar.Date, out var currentIndicator) ||
                !indicatorsByDate.TryGetValue(previousBar.Date, out var previousIndicator))
            {
                continue;
            }

            TradingSignalRules.UpdateSarTrendCounters(currentIndicator, previousIndicator, ref daysSinceBuyChange, ref daysSinceSellChange);
            var entrySignal = TradingSignalRules.ComputeEntrySignal(currentBar, currentIndicator, previousIndicator);
            var exitSignal = TradingSignalRules.ComputeExitSignal(orderedPrices, indicatorsByDate, i, daysSinceBuyChange, daysSinceSellChange);

            if (exitSignal is not null)
            {
                markers.Add(ToMarker(exitSignal, currentBar.Close));
                var exited = _positionEngine.TryExit(exitSignal, currentBar, parameters, openPosition);
                if (exited.Trade is not null)
                {
                    trades.Add(exited.Trade);
                    cash += exited.Trade.NetPnl;
                    openPosition = SimulatedPosition.Empty;
                }
            }

            if (entrySignal is not null)
            {
                markers.Add(ToMarker(entrySignal, currentBar.Close));
                var transition = _positionEngine.ApplyEntry(entrySignal, currentBar, parameters, openPosition);
                if (transition.ClosedTrade is not null)
                {
                    trades.Add(transition.ClosedTrade);
                    cash += transition.ClosedTrade.NetPnl;
                }

                openPosition = transition.ResultingPosition;
            }

            var markToMarket = cash + CalculateOpenPositionPnl(openPosition, currentBar.Close);
            equityCurve.Add(new BacktestEquityPoint(currentBar.Date, markToMarket));
        }

        if (parameters.ForceCloseOnPeriodEnd && openPosition.IsOpen)
        {
            var lastBar = orderedPrices[^1];
            var forced = _positionEngine.ForceClose(lastBar, parameters, openPosition, "Clôture de fin de période");
            if (forced is not null)
            {
                trades.Add(forced);
                cash += forced.NetPnl;
                openPosition = SimulatedPosition.Empty;
                equityCurve[^1] = new BacktestEquityPoint(lastBar.Date, cash);
            }
        }

        return BuildResult(parameters, orderedPrices, trades, markers, equityCurve, cash);
    }

    private static BacktestResult BuildEmpty(BacktestParameters parameters)
    {
        return new BacktestResult(
            parameters,
            parameters.InitialCapital,
            parameters.InitialCapital,
            0m,
            0m,
            0m,
            0,
            0,
            0,
            0m,
            0m,
            Array.Empty<PriceBar>(),
            Array.Empty<BacktestTrade>(),
            Array.Empty<BacktestSignalPoint>(),
            Array.Empty<BacktestEquityPoint>());
    }

    private static BacktestResult BuildResult(
        BacktestParameters parameters,
        IReadOnlyList<PriceBar> orderedPrices,
        IReadOnlyList<BacktestTrade> trades,
        IReadOnlyList<BacktestSignalPoint> markers,
        IReadOnlyList<BacktestEquityPoint> equityCurve,
        decimal finalCapital)
    {
        var winning = trades.Count(x => x.NetPnl > 0m);
        var losing = trades.Count(x => x.NetPnl < 0m);
        var totalPnl = finalCapital - parameters.InitialCapital;
        var cumulative = parameters.InitialCapital == 0m ? 0m : (totalPnl / parameters.InitialCapital) * 100m;
        var averagePnl = trades.Count == 0 ? 0m : trades.Average(x => x.NetPnl);
        var winRate = trades.Count == 0 ? 0m : (winning / (decimal)trades.Count) * 100m;

        return new BacktestResult(
            parameters,
            parameters.InitialCapital,
            finalCapital,
            totalPnl,
            cumulative,
            averagePnl,
            trades.Count,
            winning,
            losing,
            winRate,
            ComputeMaxDrawdownPercent(equityCurve),
            orderedPrices,
            trades,
            markers,
            equityCurve);
    }

    private static BacktestSignalPoint ToMarker(TradeSignal signal, decimal price)
    {
        return signal.Type switch
        {
            TradeSignalType.Long => new BacktestSignalPoint(signal.Date, signal.Type, price, signal.Reason, "#16a34a", "triangle"),
            TradeSignalType.Short => new BacktestSignalPoint(signal.Date, signal.Type, price, signal.Reason, "#dc2626", "triangle"),
            TradeSignalType.LeaveLong => new BacktestSignalPoint(signal.Date, signal.Type, price, signal.Reason, "#f59e0b", "rectRot"),
            TradeSignalType.LeaveShort => new BacktestSignalPoint(signal.Date, signal.Type, price, signal.Reason, "#f59e0b", "rectRot"),
            _ => new BacktestSignalPoint(signal.Date, signal.Type, price, signal.Reason, "#64748b", "circle")
        };
    }

    private static decimal CalculateOpenPositionPnl(SimulatedPosition position, decimal currentClose)
    {
        if (!position.IsOpen)
        {
            return 0m;
        }

        return position.Side == PositionSide.Long
            ? (currentClose - position.EntryPrice) * position.Quantity
            : (position.EntryPrice - currentClose) * position.Quantity;
    }

    private static decimal ComputeMaxDrawdownPercent(IReadOnlyList<BacktestEquityPoint> equityCurve)
    {
        if (equityCurve.Count == 0)
        {
            return 0m;
        }

        var peak = equityCurve[0].Equity;
        var maxDrawdown = 0m;
        foreach (var point in equityCurve)
        {
            if (point.Equity > peak)
            {
                peak = point.Equity;
            }

            if (peak <= 0m)
            {
                continue;
            }

            var drawdown = ((peak - point.Equity) / peak) * 100m;
            if (drawdown > maxDrawdown)
            {
                maxDrawdown = drawdown;
            }
        }

        return maxDrawdown;
    }
}

public sealed class PositionEngine
{
    public EntryTransition ApplyEntry(TradeSignal signal, PriceBar bar, BacktestParameters parameters, SimulatedPosition currentPosition)
    {
        if (signal.Type == TradeSignalType.Long)
        {
            if (currentPosition.Side == PositionSide.Long)
            {
                return new EntryTransition(currentPosition, null);
            }

            if (currentPosition.Side == PositionSide.Short)
            {
                var closed = BuildClosedTrade(currentPosition, bar, parameters, signal.Reason);
                return new EntryTransition(Open(PositionSide.Long, bar, parameters, signal.Reason), closed);
            }

            return new EntryTransition(Open(PositionSide.Long, bar, parameters, signal.Reason), null);
        }

        if (signal.Type == TradeSignalType.Short)
        {
            if (currentPosition.Side == PositionSide.Short)
            {
                return new EntryTransition(currentPosition, null);
            }

            if (currentPosition.Side == PositionSide.Long)
            {
                var closed = BuildClosedTrade(currentPosition, bar, parameters, signal.Reason);
                return new EntryTransition(Open(PositionSide.Short, bar, parameters, signal.Reason), closed);
            }

            return new EntryTransition(Open(PositionSide.Short, bar, parameters, signal.Reason), null);
        }

        return new EntryTransition(currentPosition, null);
    }

    public ExitTransition TryExit(TradeSignal signal, PriceBar bar, BacktestParameters parameters, SimulatedPosition currentPosition)
    {
        if (!currentPosition.IsOpen)
        {
            return new ExitTransition(null);
        }

        if (signal.Type == TradeSignalType.LeaveLong && currentPosition.Side != PositionSide.Long)
        {
            return new ExitTransition(null);
        }

        if (signal.Type == TradeSignalType.LeaveShort && currentPosition.Side != PositionSide.Short)
        {
            return new ExitTransition(null);
        }

        return new ExitTransition(BuildClosedTrade(currentPosition, bar, parameters, signal.Reason));
    }

    public BacktestTrade? ForceClose(PriceBar bar, BacktestParameters parameters, SimulatedPosition currentPosition, string reason)
    {
        return currentPosition.IsOpen
            ? BuildClosedTrade(currentPosition, bar, parameters, reason)
            : null;
    }

    private static SimulatedPosition Open(PositionSide side, PriceBar bar, BacktestParameters parameters, string entryReason)
    {
        var entryPrice = ApplyEntrySlippage(bar.Close, side, parameters.SlippagePercent);
        var quantity = entryPrice == 0m ? 0m : parameters.FixedAmountPerTrade / entryPrice;
        return new SimulatedPosition(
            side,
            side == PositionSide.Long ? ProductType.Call : ProductType.Put,
            bar.Date,
            entryPrice,
            quantity,
            entryReason);
    }

    private static BacktestTrade BuildClosedTrade(SimulatedPosition position, PriceBar bar, BacktestParameters parameters, string exitReason)
    {
        var exitPrice = ApplyExitSlippage(bar.Close, position.Side, parameters.SlippagePercent);
        var grossPnl = position.Side == PositionSide.Long
            ? (exitPrice - position.EntryPrice) * position.Quantity
            : (position.EntryPrice - exitPrice) * position.Quantity;
        var netPnl = grossPnl - (parameters.FeePerTrade * 2m);
        var investedAmount = position.EntryPrice * position.Quantity;
        var returnPercent = investedAmount == 0m ? 0m : (netPnl / investedAmount) * 100m;

        return new BacktestTrade(
            position.EntryDate,
            bar.Date,
            position.Side,
            position.Product,
            position.EntryPrice,
            exitPrice,
            position.Quantity,
            grossPnl,
            netPnl,
            returnPercent,
            position.EntryReason,
            exitReason);
    }

    private static decimal ApplyEntrySlippage(decimal close, PositionSide side, decimal slippagePercent)
        => side == PositionSide.Long
            ? close * (1m + (slippagePercent / 100m))
            : close * (1m - (slippagePercent / 100m));

    private static decimal ApplyExitSlippage(decimal close, PositionSide side, decimal slippagePercent)
        => side == PositionSide.Long
            ? close * (1m - (slippagePercent / 100m))
            : close * (1m + (slippagePercent / 100m));
}

public sealed record SimulatedPosition(
    PositionSide Side,
    ProductType Product,
    DateOnly EntryDate,
    decimal EntryPrice,
    decimal Quantity,
    string EntryReason)
{
    public bool IsOpen => Side != PositionSide.None;
    public static SimulatedPosition Empty { get; } = new(PositionSide.None, ProductType.None, default, 0m, 0m, string.Empty);
}

public sealed record EntryTransition(
    SimulatedPosition ResultingPosition,
    BacktestTrade? ClosedTrade);

public sealed record ExitTransition(BacktestTrade? Trade);
