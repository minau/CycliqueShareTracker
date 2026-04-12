using System.Collections.Concurrent;
using CycliqueShareTracker.Application.Interfaces;
using CycliqueShareTracker.Application.Models;
using CycliqueShareTracker.Application.Trading;
using Microsoft.Extensions.Options;

namespace CycliqueShareTracker.Application.Services;

public sealed class SystemTradingClock : ITradingClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}

public sealed class InMemoryPositionStore : IPositionStore
{
    private readonly ConcurrentDictionary<string, TrackedPosition> _positions = new(StringComparer.OrdinalIgnoreCase);

    public TrackedPosition Get(string symbol)
        => _positions.TryGetValue(symbol, out var position) ? position : TrackedPosition.Empty(symbol);

    public void Save(TrackedPosition position) => _positions[position.Symbol] = position;
}

public sealed class InMemoryTradeExecutionLedger : ITradeExecutionLedger
{
    private readonly ConcurrentDictionary<string, byte> _keys = new(StringComparer.OrdinalIgnoreCase);

    public bool IsProcessed(string key) => _keys.ContainsKey(key);

    public void MarkProcessed(string key) => _keys.TryAdd(key, 0);
}

public sealed class TradingWindowService : ITradingWindowService
{
    private readonly TimeZoneInfo _timeZone;
    private readonly TimeOnly _start;
    private readonly TimeOnly _end;

    public TradingWindowService(IOptions<TradingWindowOptions> options)
    {
        var value = options.Value;
        _timeZone = TimeZoneInfo.FindSystemTimeZoneById(value.TimeZoneId);
        _start = TimeOnly.Parse(value.Start);
        _end = TimeOnly.Parse(value.End);
    }

    public bool IsInWindow(DateTimeOffset now)
    {
        var localTime = TimeOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, _timeZone).DateTime);
        return localTime >= _start && localTime <= _end;
    }
}

public sealed class SignalEngine : ISignalEngine
{
    private readonly IPositionStore _positionStore;
    private readonly ITradeExecutionLedger _ledger;
    private readonly ITradingWindowService _windowService;
    private readonly ITradingClock _clock;

    public SignalEngine(IPositionStore positionStore, ITradeExecutionLedger ledger, ITradingWindowService windowService, ITradingClock clock)
    {
        _positionStore = positionStore;
        _ledger = ledger;
        _windowService = windowService;
        _clock = clock;
    }

    public IReadOnlyList<DailySignalResult> Evaluate(string symbol, IReadOnlyList<PriceBar> prices, IReadOnlyList<ComputedIndicator> indicators, decimal defaultQuantity = 1000m)
    {
        if (prices.Count == 0 || indicators.Count == 0)
        {
            return Array.Empty<DailySignalResult>();
        }

        var indicatorsByDate = indicators.ToDictionary(x => x.Date);
        var orderedPrices = prices.OrderBy(x => x.Date).ToList();
        var results = new List<DailySignalResult>(orderedPrices.Count);

        var position = _positionStore.Get(symbol);
        var inWindow = _windowService.IsInWindow(_clock.UtcNow);
        var daysSinceBuyChange = 0;
        var daysSinceSellChange = 0;

        for (var i = 1; i < orderedPrices.Count; i++)
        {
            var currentPrice = orderedPrices[i];
            var previousPrice = orderedPrices[i - 1];

            if (!indicatorsByDate.TryGetValue(currentPrice.Date, out var current) ||
                !indicatorsByDate.TryGetValue(previousPrice.Date, out var previous))
            {
                continue;
            }

            UpdateSarTrendCounters(current, previous, ref daysSinceBuyChange, ref daysSinceSellChange);
            var entry = ComputeEntrySignal(currentPrice, current, previous);
            var exit = ComputeExitSignal(currentPrice, current, previous, daysSinceBuyChange, daysSinceSellChange);

            var actions = new List<TradeAction>();
            position = TransitionPosition(symbol, currentPrice, entry, exit, position, defaultQuantity, inWindow, actions);

            results.Add(new DailySignalResult(
                currentPrice.Date,
                entry,
                exit,
                actions,
                position,
                inWindow,
                inWindow && actions.Any(x => x.Status == TradeExecutionStatus.PendingWindow)));
        }

        _positionStore.Save(position);
        return results;
    }

    private static void UpdateSarTrendCounters(ComputedIndicator current, ComputedIndicator previous, ref int daysSinceBuyChange, ref int daysSinceSellChange)
    {
        var currentTrend = GetTrendFromSar(current);
        var previousTrend = GetTrendFromSar(previous);
        if (!string.Equals(currentTrend, previousTrend, StringComparison.Ordinal))
        {
            daysSinceBuyChange = currentTrend == "ACHAT" ? 0 : daysSinceBuyChange + 1;
            daysSinceSellChange = currentTrend == "VENTE" ? 0 : daysSinceSellChange + 1;
            return;
        }

        if (currentTrend == "ACHAT")
        {
            daysSinceBuyChange++;
            daysSinceSellChange = 0;
            return;
        }

        if (currentTrend == "VENTE")
        {
            daysSinceSellChange++;
            daysSinceBuyChange = 0;
            return;
        }

        daysSinceBuyChange = 0;
        daysSinceSellChange = 0;
    }

    private TrackedPosition TransitionPosition(
        string symbol,
        PriceBar bar,
        TradeSignal? entry,
        TradeSignal? exit,
        TrackedPosition current,
        decimal defaultQuantity,
        bool inWindow,
        List<TradeAction> actions)
    {
        var next = current;

        if (exit is not null)
        {
            if (exit.Type == TradeSignalType.LeaveLong && next.Side == PositionSide.Long)
            {
                next = ExecuteAction(symbol, bar, exit, TradeActionType.SellCall, PositionSide.None, ProductType.None, defaultQuantity, inWindow, actions, next);
            }
            else if (exit.Type == TradeSignalType.LeaveShort && next.Side == PositionSide.Short)
            {
                next = ExecuteAction(symbol, bar, exit, TradeActionType.SellPut, PositionSide.None, ProductType.None, defaultQuantity, inWindow, actions, next);
            }
        }

        if (entry is null)
        {
            return next;
        }

        if (entry.Type == TradeSignalType.Long)
        {
            if (next.Side == PositionSide.Short)
            {
                next = ExecuteAction(symbol, bar, entry, TradeActionType.SellPut, PositionSide.None, ProductType.None, defaultQuantity, inWindow, actions, next);
            }

            if (next.Side == PositionSide.None)
            {
                next = ExecuteAction(symbol, bar, entry, TradeActionType.BuyCall, PositionSide.Long, ProductType.Call, defaultQuantity, inWindow, actions, next);
            }
        }
        else if (entry.Type == TradeSignalType.Short)
        {
            if (next.Side == PositionSide.Long)
            {
                next = ExecuteAction(symbol, bar, entry, TradeActionType.SellCall, PositionSide.None, ProductType.None, defaultQuantity, inWindow, actions, next);
            }

            if (next.Side == PositionSide.None)
            {
                next = ExecuteAction(symbol, bar, entry, TradeActionType.BuyPut, PositionSide.Short, ProductType.Put, defaultQuantity, inWindow, actions, next);
            }
        }

        return next;
    }

    private TrackedPosition ExecuteAction(
        string symbol,
        PriceBar bar,
        TradeSignal signal,
        TradeActionType action,
        PositionSide resultingSide,
        ProductType resultingProduct,
        decimal quantity,
        bool inWindow,
        ICollection<TradeAction> actions,
        TrackedPosition current)
    {
        var key = BuildLedgerKey(symbol, signal.Date, signal.Type, action);
        if (_ledger.IsProcessed(key))
        {
            actions.Add(new TradeAction(signal.Date, signal.Type, action, signal.Reason, TradeExecutionStatus.AlreadyProcessed));
            return current;
        }

        if (inWindow)
        {
            actions.Add(new TradeAction(signal.Date, signal.Type, action, signal.Reason, TradeExecutionStatus.PendingWindow));
            return current;
        }

        _ledger.MarkProcessed(key);
        actions.Add(new TradeAction(signal.Date, signal.Type, action, signal.Reason, TradeExecutionStatus.Executed));

        return resultingSide == PositionSide.None
            ? TrackedPosition.Empty(symbol)
            : new TrackedPosition(symbol, resultingSide, resultingProduct, quantity, signal.Date, bar.Close, BuildProductId(symbol, resultingProduct));
    }

    private static string BuildLedgerKey(string symbol, DateOnly date, TradeSignalType signal, TradeActionType action)
        => $"{symbol}|{date:yyyyMMdd}|{signal}|{action}";

    private static string BuildProductId(string symbol, ProductType product)
        => product switch
        {
            ProductType.Call => $"{symbol}-CALL-SG",
            ProductType.Put => $"{symbol}-PUT-SG",
            _ => string.Empty
        };

    private static TradeSignal? ComputeEntrySignal(PriceBar bar, ComputedIndicator current, ComputedIndicator previous)
    {
        var currentTrend = GetTrendFromSar(current);
        var previousTrend = GetTrendFromSar(previous);
        var trendChanged = currentTrend != previousTrend;
        var sarChange = trendChanged ? "chg" : "none";
        var rsiStrengthAbs = current.Rsi14.HasValue ? (current.Rsi14.Value - 50m) / 25m : (decimal?)null;
        var macdInverse = ComputeMacdInverse(current);
        var macdRatioClose = IsMacdRatioClose(current);

        if (trendChanged && currentTrend == "VENTE" && rsiStrengthAbs >= -1m && (sarChange is "acc" or "chg") && (macdInverse == -1 || macdRatioClose))
        {
            return new TradeSignal(bar.Date, TradeSignalType.Short, "SAR bascule VENTE + validation RSI/MACD.", true, false);
        }

        if (trendChanged && currentTrend == "ACHAT" && rsiStrengthAbs <= 1m && (sarChange is "acc" or "chg") && (macdInverse == 1 || macdRatioClose))
        {
            return new TradeSignal(bar.Date, TradeSignalType.Long, "SAR bascule ACHAT + validation RSI/MACD.", true, false);
        }

        return null;
    }

    private static TradeSignal? ComputeExitSignal(PriceBar bar, ComputedIndicator current, ComputedIndicator previous, int daysSinceBuyChange, int daysSinceSellChange)
    {
        var trend = GetTrendFromSar(current);
        var bbIsBottomUp = ComputeBbBottomUpSignal(bar, current);
        var bbMidHitUp = ComputeBbMidHitUp(bar, current, previous);
        var bbMidHitDown = ComputeBbMidHitDown(bar, current, previous);
        var macdTrendChange = ComputeMacdTrendChange(current, previous);
        if (trend == "ACHAT" && bbIsBottomUp == -1 && (bbMidHitUp == -1 || macdTrendChange == -1) && daysSinceBuyChange > 4)
        {
            return new TradeSignal(bar.Date, TradeSignalType.LeaveLong, "Essoufflement du LONG (Bollinger/MACD).", false, true);
        }

        if (trend == "VENTE" && bbIsBottomUp == 1 && (bbMidHitDown == 1 || macdTrendChange == 1) && daysSinceSellChange > 4)
        {
            return new TradeSignal(bar.Date, TradeSignalType.LeaveShort, "Essoufflement du SHORT (Bollinger/MACD).", false, true);
        }

        return null;
    }

    private static string GetTrendFromSar(ComputedIndicator indicator)
    {
        if (!indicator.ParabolicSar.HasValue)
        {
            return "NONE";
        }

        return indicator.Close >= indicator.ParabolicSar.Value ? "ACHAT" : "VENTE";
    }

    private static int ComputeMacdInverse(ComputedIndicator indicator)
    {
        if (!indicator.MacdLine.HasValue || !indicator.MacdSignalLine.HasValue)
        {
            return 0;
        }

        return indicator.MacdLine.Value >= indicator.MacdSignalLine.Value ? 1 : -1;
    }

    private static bool IsMacdRatioClose(ComputedIndicator indicator)
    {
        if (!indicator.MacdSignalLine.HasValue || !indicator.MacdLine.HasValue || indicator.MacdLine.Value == 0m)
        {
            return false;
        }

        return Math.Abs((indicator.MacdSignalLine.Value / indicator.MacdLine.Value) - 1m) < 0.15m;
    }

    private static int ComputeBbBottomUpSignal(PriceBar bar, ComputedIndicator indicator)
    {
        if (!indicator.BollingerMiddle.HasValue)
        {
            return 0;
        }

        return bar.Close >= indicator.BollingerMiddle.Value ? 1 : -1;
    }

    private static int ComputeBbMidHitUp(PriceBar bar, ComputedIndicator current, ComputedIndicator previous)
    {
        if (!current.BollingerMiddle.HasValue || !previous.BollingerMiddle.HasValue || !previous.PreviousClose.HasValue)
        {
            return 0;
        }

        return previous.PreviousClose.Value >= previous.BollingerMiddle.Value && bar.Close < current.BollingerMiddle.Value ? -1 : 0;
    }

    private static int ComputeBbMidHitDown(PriceBar bar, ComputedIndicator current, ComputedIndicator previous)
    {
        if (!current.BollingerMiddle.HasValue || !previous.BollingerMiddle.HasValue || !previous.PreviousClose.HasValue)
        {
            return 0;
        }

        return previous.PreviousClose.Value <= previous.BollingerMiddle.Value && bar.Close > current.BollingerMiddle.Value ? 1 : 0;
    }

    private static int ComputeMacdTrendChange(ComputedIndicator current, ComputedIndicator previous)
    {
        if (!current.MacdHistogram.HasValue || !previous.MacdHistogram.HasValue)
        {
            return 0;
        }

        if (previous.MacdHistogram.Value >= 0m && current.MacdHistogram.Value < 0m)
        {
            return -1;
        }

        if (previous.MacdHistogram.Value <= 0m && current.MacdHistogram.Value > 0m)
        {
            return 1;
        }

        return 0;
    }

}
