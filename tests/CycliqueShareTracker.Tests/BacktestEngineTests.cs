using CycliqueShareTracker.Application.Interfaces;
using CycliqueShareTracker.Application.Models;
using CycliqueShareTracker.Application.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace CycliqueShareTracker.Tests;

public class BacktestEngineTests
{
    [Fact]
    public void RunForAsset_ShouldOpenAndCloseTrades_FromDeterministicSignals()
    {
        var engine = new BacktestEngine(
            new IndicatorCalculator(),
            new FixedAlgorithmRegistry(),
            NullLogger<BacktestEngine>.Instance);

        var prices = BuildBars(new decimal[] { 100m, 102m, 104m, 106m, 105m, 107m });
        var result = engine.RunForAsset("TEST", "Test Asset", prices, new DateOnly(2026, 01, 01), new DateOnly(2026, 01, 06), AlgorithmType.RsiMeanReversion, StrategyConfig.Default with
        {
            FeePercentPerSide = 0m,
            MinimumBarsBetweenSameSignal = 0
        });

        Assert.Equal(2, result.Trades.Count);
        Assert.Equal(new DateOnly(2026, 01, 02), result.Trades[0].EntryDate);
        Assert.Equal(new DateOnly(2026, 01, 04), result.Trades[0].ExitDate);
        Assert.Equal("Buy déterministe", result.Trades[0].EntryReason);
        Assert.Equal("Sell déterministe", result.Trades[0].ExitReason);
    }

    private static IReadOnlyList<PriceBar> BuildBars(decimal[] closes)
    {
        var start = new DateOnly(2026, 01, 01);
        return closes.Select((close, index) => new PriceBar(start.AddDays(index), close, close, close, close, 1000)).ToList();
    }

    private sealed class FixedAlgorithmRegistry : ISignalAlgorithmRegistry
    {
        private readonly ISignalAlgorithm _algorithm = new FixedSignalAlgorithm();

        public ISignalAlgorithm Get(AlgorithmType algorithmType) => _algorithm;

        public IReadOnlyList<ISignalAlgorithm> GetAll() => new[] { _algorithm };
    }

    private sealed class FixedSignalAlgorithm : ISignalAlgorithm
    {
        public AlgorithmType AlgorithmType => AlgorithmType.RsiMeanReversion;
        public string DisplayName => "Fixed";

        public AlgorithmResult ComputeSignals(IReadOnlyList<PriceBar> bars, AlgorithmContext context)
        {
            var buyDates = new HashSet<DateOnly> { new(2026, 01, 02), new(2026, 01, 05) };
            var sellDates = new HashSet<DateOnly> { new(2026, 01, 04), new(2026, 01, 06) };
            var points = bars.Select(b => new AlgorithmSignalPoint(
                b.Date,
                buyDates.Contains(b.Date),
                sellDates.Contains(b.Date),
                buyDates.Contains(b.Date),
                sellDates.Contains(b.Date),
                buyDates.Contains(b.Date) ? 80 : 0,
                sellDates.Contains(b.Date) ? 80 : 0,
                null,
                buyDates.Contains(b.Date) ? "Buy déterministe" : "No buy",
                sellDates.Contains(b.Date) ? "Sell déterministe" : "Hold",
                Array.Empty<ScoreFactorDetail>(),
                Array.Empty<ScoreFactorDetail>())).ToList();

            return new AlgorithmResult(AlgorithmType, DisplayName, points);
        }
    }
}
