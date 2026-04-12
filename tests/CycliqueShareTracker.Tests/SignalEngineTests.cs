using CycliqueShareTracker.Application.Interfaces;
using CycliqueShareTracker.Application.Models;
using CycliqueShareTracker.Application.Services;
using CycliqueShareTracker.Application.Trading;

namespace CycliqueShareTracker.Tests;

public sealed class SignalEngineTests
{
    [Fact]
    public void Evaluate_ShouldOpenLong_WhenLongConditionsAreMet()
    {
        var engine = CreateEngine(inWindow: false);
        var prices = new[]
        {
            new PriceBar(new DateOnly(2026, 4, 1), 100, 102, 99, 100, 1_000),
            new PriceBar(new DateOnly(2026, 4, 2), 103, 105, 101, 104, 1_000)
        };

        var indicators = new[]
        {
            new ComputedIndicator(prices[0].Date, null, null, 45, null, prices[0].Close, null, 0.2m, 0.3m, -0.1m, null, null, null, 100, 103, 97, 2, 105),
            new ComputedIndicator(prices[1].Date, null, null, 55, null, prices[1].Close, prices[0].Close, 1.2m, 0.8m, 0.4m, -0.1m, null, null, 102, 106, 98, 2, 100)
        };

        var result = engine.Evaluate("TTE.PA", prices, indicators);

        var last = Assert.Single(result);
        Assert.Contains(last.Actions, x => x.ActionType == TradeActionType.BuyCall && x.Status == TradeExecutionStatus.Executed);
        Assert.Equal(PositionSide.Long, last.PositionAfter.Side);
    }

    [Fact]
    public void Evaluate_ShouldAvoidDoubleExecution_WhenReprocessed()
    {
        var positionStore = new InMemoryPositionStore();
        var ledger = new InMemoryTradeExecutionLedger();
        var engine = new SignalEngine(positionStore, ledger, new StaticTradingWindowService(false), new FixedClock());
        var prices = new[]
        {
            new PriceBar(new DateOnly(2026, 4, 1), 100, 102, 99, 100, 1_000),
            new PriceBar(new DateOnly(2026, 4, 2), 103, 105, 101, 104, 1_000)
        };

        var indicators = new[]
        {
            new ComputedIndicator(prices[0].Date, null, null, 45, null, prices[0].Close, null, 0.2m, 0.3m, -0.1m, null, null, null, 100, 103, 97, 2, 105),
            new ComputedIndicator(prices[1].Date, null, null, 55, null, prices[1].Close, prices[0].Close, 1.2m, 0.8m, 0.4m, -0.1m, null, null, 102, 106, 98, 2, 100)
        };

        var first = engine.Evaluate("TTE.PA", prices, indicators);
        var second = engine.Evaluate("TTE.PA", prices, indicators);

        Assert.Contains(first.SelectMany(x => x.Actions), x => x.Status == TradeExecutionStatus.Executed);
        Assert.Contains(second.SelectMany(x => x.Actions), x => x.Status == TradeExecutionStatus.AlreadyProcessed);
    }

    private static SignalEngine CreateEngine(bool inWindow)
        => new(new InMemoryPositionStore(), new InMemoryTradeExecutionLedger(), new StaticTradingWindowService(inWindow), new FixedClock());

    private sealed class StaticTradingWindowService(bool inWindow) : ITradingWindowService
    {
        public bool IsInWindow(DateTimeOffset now) => inWindow;
    }

    private sealed class FixedClock : ITradingClock
    {
        public DateTimeOffset UtcNow => new(2026, 4, 2, 17, 0, 0, TimeSpan.Zero);
    }
}
