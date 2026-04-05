using CycliqueShareTracker.Application.Interfaces;
using CycliqueShareTracker.Application.Models;
using CycliqueShareTracker.Application.Services;
using CycliqueShareTracker.Domain.Enums;
using Microsoft.Extensions.Logging.Abstractions;

namespace CycliqueShareTracker.Tests;

public class BacktestEngineTests
{
    [Fact]
    public void RunForAsset_ShouldOpenAndCloseTrades_FromDeterministicSignals()
    {
        var engine = new BacktestEngine(
            new IndicatorCalculator(),
            new FixedSignalService(),
            new FixedExitSignalService(),
            NullLogger<BacktestEngine>.Instance);

        var prices = BuildBars(new decimal[] { 100m, 102m, 104m, 106m, 105m, 107m });
        var result = engine.RunForAsset("TEST", "Test Asset", prices, new DateOnly(2026, 01, 01), new DateOnly(2026, 01, 06), includeMacdInScoring: false, StrategyConfig.Default with
        {
            BuyScoreThreshold = 70,
            SellScoreThreshold = 60,
            FeePercentPerSide = 0m,
            MinimumBarsBetweenSameSignal = 0
        });

        Assert.Equal(2, result.Trades.Count);
        Assert.Equal(new DateOnly(2026, 01, 02), result.Trades[0].EntryDate);
        Assert.Equal(new DateOnly(2026, 01, 04), result.Trades[0].ExitDate);
        Assert.Equal("Buy déterministe", result.Trades[0].EntryReason);
        Assert.Equal("Sell déterministe", result.Trades[0].ExitReason);
    }


    [Fact]
    public void RunForAsset_ShouldAllowFirstTrade_WhenMinimumBarsBetweenSignalsIsConfigured()
    {
        var engine = new BacktestEngine(
            new IndicatorCalculator(),
            new FixedSignalService(),
            new FixedExitSignalService(),
            NullLogger<BacktestEngine>.Instance);

        var prices = BuildBars(new decimal[] { 100m, 102m, 104m, 106m, 105m, 107m });
        var result = engine.RunForAsset("TEST", "Test Asset", prices, new DateOnly(2026, 01, 01), new DateOnly(2026, 01, 06), includeMacdInScoring: false, StrategyConfig.Default with
        {
            MinimumBarsBetweenSameSignal = 3,
            FeePercentPerSide = 0m
        });

        Assert.NotEmpty(result.Trades);
        Assert.Equal(new DateOnly(2026, 01, 02), result.Trades[0].EntryDate);
    }

    [Fact]
    public void RunForAsset_ShouldComputeExpectedMetrics()
    {
        var engine = new BacktestEngine(
            new IndicatorCalculator(),
            new FixedSignalService(),
            new FixedExitSignalService(),
            NullLogger<BacktestEngine>.Instance);

        var prices = BuildBars(new decimal[] { 100m, 102m, 104m, 106m, 95m, 97m });
        var result = engine.RunForAsset("TEST", "Test Asset", prices, new DateOnly(2026, 01, 01), new DateOnly(2026, 01, 06), includeMacdInScoring: false, StrategyConfig.Default with
        {
            FeePercentPerSide = 0m,
            MinimumBarsBetweenSameSignal = 0
        });

        Assert.Equal(2, result.Metrics.TotalTrades);
        Assert.Equal(1, result.Metrics.WinningTrades);
        Assert.Equal(1, result.Metrics.LosingTrades);
        Assert.True(result.Metrics.MaxDrawdownPercent >= 0m);
    }

    private static IReadOnlyList<PriceBar> BuildBars(decimal[] closes)
    {
        var start = new DateOnly(2026, 01, 01);
        return closes.Select((close, index) => new PriceBar(start.AddDays(index), close, close, close, close, 1000)).ToList();
    }

    private sealed class FixedSignalService : ISignalService
    {
        public SignalResult BuildSignal(ComputedIndicator indicator, bool includeMacdInScoring = true, ComputedIndicator? previous = null, StrategyConfig? strategyConfig = null)
        {
            var buyDates = new HashSet<DateOnly> { new(2026, 01, 02), new(2026, 01, 05) };
            return buyDates.Contains(indicator.Date)
                ? new SignalResult(80, SignalLabel.BuyZone, "", "Buy déterministe", Array.Empty<ScoreFactorDetail>())
                : new SignalResult(10, SignalLabel.NoBuy, "", "No buy", Array.Empty<ScoreFactorDetail>());
        }
    }

    private sealed class FixedExitSignalService : IExitSignalService
    {
        public ExitSignalResult BuildExitSignal(ComputedIndicator current, ComputedIndicator? previous, bool includeMacdInScoring = true, StrategyConfig? strategyConfig = null)
        {
            var sellDates = new HashSet<DateOnly> { new(2026, 01, 04), new(2026, 01, 06) };
            return sellDates.Contains(current.Date)
                ? new ExitSignalResult(80, ExitSignalLabel.SellZone, "Sell déterministe", Array.Empty<ScoreFactorDetail>())
                : new ExitSignalResult(5, ExitSignalLabel.Hold, "Hold", Array.Empty<ScoreFactorDetail>());
        }
    }
}
