using CycliqueShareTracker.Application.Models;
using CycliqueShareTracker.Application.Trading;

namespace CycliqueShareTracker.Application.Interfaces;

public interface ISignalEngine
{
    IReadOnlyList<DailySignalResult> Evaluate(string symbol, IReadOnlyList<PriceBar> prices, IReadOnlyList<ComputedIndicator> indicators, decimal defaultQuantity = 1000m);
}

public interface IPositionStore
{
    TrackedPosition Get(string symbol);
    void Save(TrackedPosition position);
}

public interface ITradeExecutionLedger
{
    bool IsProcessed(string key);
    void MarkProcessed(string key);
}

public interface ITradingWindowService
{
    bool IsInWindow(DateTimeOffset now);
}

public interface ITradingClock
{
    DateTimeOffset UtcNow { get; }
}
