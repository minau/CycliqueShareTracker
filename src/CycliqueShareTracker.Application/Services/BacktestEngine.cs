using CycliqueShareTracker.Application.Interfaces;
using CycliqueShareTracker.Application.Models;
using Microsoft.Extensions.Logging;

namespace CycliqueShareTracker.Application.Services;

public sealed class BacktestEngine : IBacktestEngine
{
    private readonly IIndicatorCalculator _indicatorCalculator;
    private readonly ISignalAlgorithmRegistry _algorithmRegistry;
    private readonly ILogger<BacktestEngine> _logger;

    public BacktestEngine(
        IIndicatorCalculator indicatorCalculator,
        ISignalAlgorithmRegistry algorithmRegistry,
        ILogger<BacktestEngine> logger)
    {
        _indicatorCalculator = indicatorCalculator;
        _algorithmRegistry = algorithmRegistry;
        _logger = logger;
    }

    public BacktestAssetResult RunForAsset(
        string symbol,
        string assetName,
        IReadOnlyList<PriceBar> priceBars,
        DateOnly simulationStartDate,
        DateOnly simulationEndDate,
        AlgorithmType algorithmType,
        StrategyConfig config)
    {
        if (priceBars.Count == 0)
        {
            _logger.LogWarning("No bars found for symbol {Symbol} in requested range.", symbol);
            var emptyMetrics = BuildMetrics(Array.Empty<Trade>());
            return new BacktestAssetResult(symbol, assetName, emptyMetrics, Array.Empty<Trade>(), "Aucune donnée OHLC disponible sur la période demandée.");
        }

        var orderedBars = priceBars.OrderBy(x => x.Date).ToList();
        var computed = _indicatorCalculator.Compute(orderedBars);
        var algorithm = _algorithmRegistry.Get(algorithmType);
        var algorithmResult = algorithm.ComputeSignals(orderedBars, new AlgorithmContext(computed, config));
        var pointsByDate = algorithmResult.Points.ToDictionary(p => p.Date);
        var trades = new List<Trade>();

        var hasBarsInWindow = orderedBars.Any(b => b.Date >= simulationStartDate && b.Date <= simulationEndDate);
        if (!hasBarsInWindow)
        {
            var emptyMetrics = BuildMetrics(Array.Empty<Trade>());
            return new BacktestAssetResult(symbol, assetName, emptyMetrics, Array.Empty<Trade>(), "Aucune donnée OHLC disponible dans la fenêtre demandée.");
        }

        OpenPosition? openPosition = null;
        var barsSinceLastBuy = config.MinimumBarsBetweenSameSignal;
        var barsSinceLastSell = config.MinimumBarsBetweenSameSignal;

        foreach (var bar in orderedBars)
        {
            if (!pointsByDate.TryGetValue(bar.Date, out var point))
            {
                continue;
            }

            var inSimulationWindow = bar.Date >= simulationStartDate && bar.Date <= simulationEndDate;
            if (!inSimulationWindow)
            {
                continue;
            }

            if (openPosition is null)
            {
                barsSinceLastBuy++;
                if (point.BuySignal && barsSinceLastBuy >= config.MinimumBarsBetweenSameSignal)
                {
                    openPosition = new OpenPosition(bar.Date, bar.Close, point.BuyReason);
                    barsSinceLastBuy = 0;
                }
            }
            else
            {
                barsSinceLastSell++;
                if (point.SellSignal && barsSinceLastSell >= config.MinimumBarsBetweenSameSignal)
                {
                    trades.Add(BuildTrade(symbol, openPosition, bar, point.SellReason, config.FeePercentPerSide));
                    openPosition = null;
                    barsSinceLastSell = 0;
                }
            }
        }

        if (openPosition is not null)
        {
            var lastBar = orderedBars.Last(x => x.Date >= simulationStartDate && x.Date <= simulationEndDate);
            trades.Add(BuildTrade(symbol, openPosition, lastBar, "Sortie forcée en fin de période de backtest.", config.FeePercentPerSide));
        }

        return new BacktestAssetResult(symbol, assetName, BuildMetrics(trades), trades);
    }

    private static Trade BuildTrade(string symbol, OpenPosition openPosition, PriceBar exitBar, string exitReason, decimal feePercentPerSide)
    {
        var entryCost = openPosition.EntryPrice * (1m + (feePercentPerSide / 100m));
        var exitProceed = exitBar.Close * (1m - (feePercentPerSide / 100m));
        var performance = entryCost == 0m ? 0m : ((exitProceed / entryCost) - 1m) * 100m;
        var durationDays = exitBar.Date.DayNumber - openPosition.EntryDate.DayNumber;

        return new Trade(
            symbol,
            openPosition.EntryDate,
            decimal.Round(openPosition.EntryPrice, 4),
            exitBar.Date,
            decimal.Round(exitBar.Close, 4),
            decimal.Round(performance, 2),
            Math.Max(durationDays, 0),
            openPosition.EntryReason,
            exitReason);
    }

    private static BacktestMetrics BuildMetrics(IReadOnlyList<Trade> trades)
    {
        if (trades.Count == 0)
        {
            return new BacktestMetrics(0, 0, 0, 0m, 0m, 0m, 0m, 0m, 0m, 0m);
        }

        var winningTrades = trades.Count(t => t.PerformancePercent > 0m);
        var losingTrades = trades.Count(t => t.PerformancePercent < 0m);

        var gains = trades.Where(t => t.PerformancePercent > 0m).Select(t => t.PerformancePercent).ToList();
        var losses = trades.Where(t => t.PerformancePercent < 0m).Select(t => Math.Abs(t.PerformancePercent)).ToList();

        var grossGain = gains.Sum();
        var grossLoss = losses.Sum();

        var winRate = (decimal)winningTrades / trades.Count * 100m;
        var averageGain = gains.Count == 0 ? 0m : gains.Average();
        var averageLoss = losses.Count == 0 ? 0m : losses.Average();
        var profitFactor = grossLoss == 0m ? grossGain : grossGain / grossLoss;

        decimal equity = 1m;
        decimal peak = 1m;
        decimal maxDrawdown = 0m;

        foreach (var trade in trades)
        {
            equity *= 1m + (trade.PerformancePercent / 100m);
            if (equity > peak)
            {
                peak = equity;
            }

            if (peak > 0)
            {
                var drawdown = ((equity / peak) - 1m) * 100m;
                if (drawdown < maxDrawdown)
                {
                    maxDrawdown = drawdown;
                }
            }
        }

        var totalPerformance = (equity - 1m) * 100m;
        var averageDuration = trades.Average(t => t.DurationDays);

        return new BacktestMetrics(
            trades.Count,
            winningTrades,
            losingTrades,
            decimal.Round(winRate, 2),
            decimal.Round(averageGain, 2),
            decimal.Round(averageLoss, 2),
            decimal.Round(profitFactor, 2),
            decimal.Round(totalPerformance, 2),
            decimal.Round(Math.Abs(maxDrawdown), 2),
            decimal.Round((decimal)averageDuration, 2));
    }

    private sealed record OpenPosition(DateOnly EntryDate, decimal EntryPrice, string EntryReason);
}
