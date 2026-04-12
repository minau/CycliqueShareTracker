using CycliqueShareTracker.Application.Models;
using CycliqueShareTracker.Application.Trading;
using Xunit;

namespace CycliqueShareTracker.Tests;

public sealed class BacktestEngineTests
{
    [Fact]
    public void TradingSignalRules_ShouldReturnLongSignal_WhenLongConditionsAreMet()
    {
        var previousBar = new PriceBar(new DateOnly(2026, 1, 1), 100m, 101m, 99m, 100m, 1000);
        var currentBar = new PriceBar(new DateOnly(2026, 1, 2), 104m, 105m, 103m, 104m, 1000);
        var previous = new ComputedIndicator(previousBar.Date, null, null, 45m, null, previousBar.Close, null, 0.2m, 0.3m, -0.1m, null, null, null, 100m, 103m, 97m, 2m, 105m);
        var current = new ComputedIndicator(currentBar.Date, null, null, 55m, null, currentBar.Close, previousBar.Close, 1.2m, 0.8m, 0.4m, -0.1m, null, null, 102m, 106m, 98m, 2m, 100m);

        var signal = TradingSignalRules.ComputeEntrySignal(currentBar, current, previous);

        Assert.NotNull(signal);
        Assert.Equal(TradeSignalType.Long, signal!.Type);
    }

    [Fact]
    public void PositionEngine_ShouldComputeLongTradePnl()
    {
        var engine = new PositionEngine();
        var parameters = BacktestParameters.Default("TTE.PA", new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 10));
        var openSignal = new TradeSignal(new DateOnly(2026, 1, 2), TradeSignalType.Long, "long", true, false);
        var entryBar = new PriceBar(openSignal.Date, 100m, 100m, 100m, 100m, 1000);
        var exitBar = new PriceBar(new DateOnly(2026, 1, 3), 110m, 110m, 110m, 110m, 1000);

        var opened = engine.ApplyEntry(openSignal, entryBar, parameters, SimulatedPosition.Empty);
        var closedTrade = engine.ForceClose(exitBar, parameters, opened.ResultingPosition, "close");

        Assert.NotNull(closedTrade);
        Assert.True(closedTrade!.NetPnl > 0m);
        Assert.Equal(PositionSide.Long, closedTrade.Side);
    }

    [Fact]
    public void PositionEngine_ShouldComputeShortTradePnl()
    {
        var engine = new PositionEngine();
        var parameters = BacktestParameters.Default("TTE.PA", new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 10));
        var openSignal = new TradeSignal(new DateOnly(2026, 1, 2), TradeSignalType.Short, "short", true, false);
        var entryBar = new PriceBar(openSignal.Date, 100m, 100m, 100m, 100m, 1000);
        var exitBar = new PriceBar(new DateOnly(2026, 1, 3), 90m, 90m, 90m, 90m, 1000);

        var opened = engine.ApplyEntry(openSignal, entryBar, parameters, SimulatedPosition.Empty);
        var closedTrade = engine.ForceClose(exitBar, parameters, opened.ResultingPosition, "close");

        Assert.NotNull(closedTrade);
        Assert.True(closedTrade!.NetPnl > 0m);
        Assert.Equal(PositionSide.Short, closedTrade.Side);
    }

    [Fact]
    public void PositionEngine_ShouldReverseFromLongToShort_OnShortSignal()
    {
        var engine = new PositionEngine();
        var parameters = BacktestParameters.Default("TTE.PA", new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 10));
        var longOpen = engine.ApplyEntry(
            new TradeSignal(new DateOnly(2026, 1, 2), TradeSignalType.Long, "long", true, false),
            new PriceBar(new DateOnly(2026, 1, 2), 100m, 100m, 100m, 100m, 1000),
            parameters,
            SimulatedPosition.Empty);

        var reversed = engine.ApplyEntry(
            new TradeSignal(new DateOnly(2026, 1, 3), TradeSignalType.Short, "reverse", true, false),
            new PriceBar(new DateOnly(2026, 1, 3), 95m, 95m, 95m, 95m, 1000),
            parameters,
            longOpen.ResultingPosition);

        Assert.NotNull(reversed.ClosedTrade);
        Assert.Equal(PositionSide.Short, reversed.ResultingPosition.Side);
    }

    [Fact]
    public void PositionEngine_ShouldCloseOnLeaveSignals_OnlyForMatchingSide()
    {
        var engine = new PositionEngine();
        var parameters = BacktestParameters.Default("TTE.PA", new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 10));
        var opened = engine.ApplyEntry(
            new TradeSignal(new DateOnly(2026, 1, 2), TradeSignalType.Long, "long", true, false),
            new PriceBar(new DateOnly(2026, 1, 2), 100m, 100m, 100m, 100m, 1000),
            parameters,
            SimulatedPosition.Empty);

        var leaveShort = engine.TryExit(
            new TradeSignal(new DateOnly(2026, 1, 3), TradeSignalType.LeaveShort, "leave short", false, true),
            new PriceBar(new DateOnly(2026, 1, 3), 100m, 100m, 100m, 100m, 1000),
            parameters,
            opened.ResultingPosition);
        var leaveLong = engine.TryExit(
            new TradeSignal(new DateOnly(2026, 1, 3), TradeSignalType.LeaveLong, "leave long", false, true),
            new PriceBar(new DateOnly(2026, 1, 3), 100m, 100m, 100m, 100m, 1000),
            parameters,
            opened.ResultingPosition);

        Assert.Null(leaveShort.Trade);
        Assert.NotNull(leaveLong.Trade);
    }
}
