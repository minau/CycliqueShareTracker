using CycliqueShareTracker.Application.Algorithms;
using CycliqueShareTracker.Application.Models;

namespace CycliqueShareTracker.Tests;

public class CompositeTrendPullbackAlgorithmTests
{
    private readonly CompositeTrendPullbackAlgorithm _algorithm = new();

    [Fact]
    public void ComputeSignals_ShouldKeepBuyLogicWorking()
    {
        var indicators = new List<ComputedIndicator>
        {
            BuildIndicator(new DateOnly(2026, 1, 1), 101m, 100m, 98m, 42m, 0.10m, 0.12m, -0.02m, 101m, 100m, -9m),
            BuildIndicator(new DateOnly(2026, 1, 2), 102m, 100.2m, 98.1m, 46m, 0.18m, 0.14m, 0.04m, 101.3m, 100.2m, -8m)
        };

        var result = _algorithm.ComputeSignals(BuildBars(indicators), BuildContext(indicators));
        Assert.True(result.Points[^1].BuySignal);
    }

    [Fact]
    public void ComputeSignals_ShouldOnlyWarn_WhenOnlyFatigueSignalsAppear()
    {
        var indicators = new List<ComputedIndicator>
        {
            BuildIndicator(new DateOnly(2026, 2, 1), 108m, 100m, 95m, 67m, 0.22m, 0.20m, 0.04m, 104m, 101m, -4m),
            BuildIndicator(new DateOnly(2026, 2, 2), 108.2m, 100.2m, 95.1m, 67m, 0.21m, 0.20m, 0.01m, 104.2m, 101.2m, -3.8m)
        };

        var point = _algorithm.ComputeSignals(BuildBars(indicators), BuildContext(indicators)).Points[^1];

        Assert.False(point.SellSignal);
        Assert.Equal("none", point.DebugValues["sellDecisionMode"]);
        Assert.True((bool)point.DebugValues["warningActive"]!);
    }

    [Fact]
    public void ComputeSignals_ShouldSell_OnConfirmedBreak_WhenTwoConditionsAreTrue()
    {
        var indicators = new List<ComputedIndicator>
        {
            BuildIndicator(new DateOnly(2026, 3, 1), 104m, 100m, 97m, 55m, 0.26m, 0.20m, 0.06m, 103.5m, 101m, -4m),
            BuildIndicator(new DateOnly(2026, 3, 2), 99m, 100.1m, 97.1m, 48m, 0.24m, 0.25m, -0.01m, 101.5m, 101.8m, -5m)
        };

        var point = _algorithm.ComputeSignals(BuildBars(indicators), BuildContext(indicators)).Points[^1];

        Assert.True(point.SellSignal);
        Assert.Equal("confirmed_break", point.DebugValues["sellDecisionMode"]);
        Assert.True((int)point.DebugValues["confirmedSellConditionsCount"]! >= 2);
    }

    [Fact]
    public void ComputeSignals_ShouldSell_OnExtendedReversalSpecialCase()
    {
        var indicators = new List<ComputedIndicator>
        {
            BuildIndicator(new DateOnly(2026, 4, 1), 112m, 100m, 96m, 69m, 0.30m, 0.24m, 0.06m, 106m, 103m, -3m),
            BuildIndicator(new DateOnly(2026, 4, 2), 111m, 100.2m, 96.2m, 68m, 0.18m, 0.22m, -0.04m, 105m, 103.2m, -2.5m)
        };

        var point = _algorithm.ComputeSignals(BuildBars(indicators), BuildContext(indicators)).Points[^1];

        Assert.True(point.SellSignal);
        Assert.Equal("extended_reversal", point.DebugValues["sellDecisionMode"]);
    }

    [Fact]
    public void ComputeSignals_ShouldExposeConfirmedConditionsInDebugValues()
    {
        var indicators = new List<ComputedIndicator>
        {
            BuildIndicator(new DateOnly(2026, 5, 1), 103m, 100m, 97m, 52m, 0.15m, 0.10m, 0.02m, 102m, 101m, -3m),
            BuildIndicator(new DateOnly(2026, 5, 2), 99m, 100m, 97.1m, 47m, 0.08m, 0.14m, -0.03m, 100.5m, 101.2m, -4m)
        };

        var point = _algorithm.ComputeSignals(BuildBars(indicators), BuildContext(indicators)).Points[^1];

        var conditions = (IReadOnlyList<string>)point.DebugValues["confirmedSellConditions"]!;
        Assert.NotEmpty(conditions);
        Assert.True((bool)point.DebugValues["sellConfirmedByGate"]!);
    }

    [Fact]
    public void ComputeSignals_ShouldRespectMinimumBarsBetweenSameSignal()
    {
        var indicators = Enumerable.Range(0, 8)
            .Select(index => BuildIndicator(new DateOnly(2026, 6, 1).AddDays(index), 102m + index, 100m + (index * 0.2m), 98m, 45m, 0.3m, 0.1m, 0.2m, 101m, 99m, -8m))
            .ToList();

        var context = BuildContext(indicators, MetaAlgoParameters.Default with { MinimumBarsBetweenSameSignal = 3 });
        var result = _algorithm.ComputeSignals(BuildBars(indicators), context);
        var buyDates = result.Points.Where(p => p.BuySignal).Select(p => p.Date).ToList();

        Assert.True(buyDates.Count >= 2);
        for (var i = 1; i < buyDates.Count; i++)
        {
            Assert.True((buyDates[i].DayNumber - buyDates[i - 1].DayNumber) >= 3);
        }
    }

    private static AlgorithmContext BuildContext(IReadOnlyList<ComputedIndicator> indicators, MetaAlgoParameters? parameters = null)
    {
        var config = StrategyConfig.Default with { MetaAlgoParameters = parameters ?? MetaAlgoParameters.Default };
        return new AlgorithmContext(indicators, config);
    }

    private static IReadOnlyList<PriceBar> BuildBars(IReadOnlyList<ComputedIndicator> indicators)
    {
        return indicators.Select(i => new PriceBar(i.Date, i.Close, i.Close, i.Close, i.Close, 1000)).ToList();
    }

    private static ComputedIndicator BuildIndicator(
        DateOnly date,
        decimal close,
        decimal? sma50,
        decimal? sma200,
        decimal? rsi,
        decimal? macd,
        decimal? signal,
        decimal? hist,
        decimal? ema12,
        decimal? ema26,
        decimal? drawdown)
    {
        return new ComputedIndicator(date, sma50, sma200, rsi, drawdown, close, close - 0.2m, macd, signal, hist, null, ema12, ema26);
    }
}
