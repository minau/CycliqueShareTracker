using CycliqueShareTracker.Application.Algorithms;
using CycliqueShareTracker.Application.Models;

namespace CycliqueShareTracker.Tests;

public class CompositeTrendPullbackAlgorithmTests
{
    private readonly CompositeTrendPullbackAlgorithm _algorithm = new();

    [Fact]
    public void ComputeSignals_ShouldTriggerBuy_InBullTrendWithHealthyPullbackAndMacdRestart()
    {
        var indicators = new List<ComputedIndicator>
        {
            BuildIndicator(new DateOnly(2026, 1, 1), close: 101m, sma50: 100m, sma200: 98m, rsi: 42m, macd: 0.10m, signal: 0.12m, hist: -0.02m, ema12: 101m, ema26: 100m, drawdown: -9m),
            BuildIndicator(new DateOnly(2026, 1, 2), close: 102m, sma50: 100.2m, sma200: 98.1m, rsi: 46m, macd: 0.18m, signal: 0.14m, hist: 0.04m, ema12: 101.3m, ema26: 100.2m, drawdown: -8m)
        };

        var result = _algorithm.ComputeSignals(BuildBars(indicators), BuildContext(indicators));
        var point = result.Points[^1];

        Assert.True(point.BuySignal);
        Assert.True(point.BuyScore >= 55);
    }

    [Fact]
    public void ComputeSignals_ShouldOnlyWarn_WhenMomentumSlowsButTrendStillBullish()
    {
        var indicators = new List<ComputedIndicator>
        {
            BuildIndicator(new DateOnly(2026, 2, 1), close: 100m, sma50: 98m, sma200: 95m, rsi: 67m, macd: 0.25m, signal: 0.20m, hist: 0.05m, ema12: 101m, ema26: 99m, drawdown: -6m),
            BuildIndicator(new DateOnly(2026, 2, 2), close: 101m, sma50: 98.2m, sma200: 95.1m, rsi: 67m, macd: 0.24m, signal: 0.21m, hist: 0.03m, ema12: 101.1m, ema26: 99.2m, drawdown: -5.5m)
        };

        var result = _algorithm.ComputeSignals(BuildBars(indicators), BuildContext(indicators));
        var point = result.Points[^1];

        Assert.False(point.SellSignal);
        Assert.True((int)point.DebugValues["earlyWarningScore"]! >= 20);
        Assert.Equal(false, point.DebugValues["sellConfirmedByGate"]);
    }

    [Fact]
    public void ComputeSignals_ShouldConfirmSell_WhenGateConditionsAndScoreAreMet()
    {
        var indicators = new List<ComputedIndicator>
        {
            BuildIndicator(new DateOnly(2026, 3, 1), close: 104m, sma50: 100m, sma200: 97m, rsi: 55m, macd: 0.26m, signal: 0.20m, hist: 0.06m, ema12: 103.5m, ema26: 101m, drawdown: -4m),
            BuildIndicator(new DateOnly(2026, 3, 2), close: 102m, sma50: 100.1m, sma200: 97.1m, rsi: 48m, macd: 0.16m, signal: 0.21m, hist: -0.02m, ema12: 101.5m, ema26: 101.8m, drawdown: -5m),
            BuildIndicator(new DateOnly(2026, 3, 3), close: 101m, sma50: 100.1m, sma200: 97.2m, rsi: 46m, macd: 0.10m, signal: 0.20m, hist: -0.04m, ema12: 100.8m, ema26: 101.7m, drawdown: -6m)
        };

        var result = _algorithm.ComputeSignals(BuildBars(indicators), BuildContext(indicators));
        var point = result.Points[^1];

        Assert.True(point.SellSignal);
        Assert.True((bool)point.DebugValues["sellConfirmedByGate"]!);
        var gateReasons = (IReadOnlyList<string>)point.DebugValues["sellGateReasons"]!;
        Assert.NotEmpty(gateReasons);
    }

    [Fact]
    public void ComputeSignals_ShouldExposeRsiMomentumStateDebug()
    {
        var indicators = new List<ComputedIndicator>
        {
            BuildIndicator(new DateOnly(2026, 4, 1), close: 108m, sma50: 100m, sma200: 96m, rsi: 69m, macd: 0.30m, signal: 0.24m, hist: 0.06m, ema12: 106m, ema26: 103m, drawdown: -3m),
            BuildIndicator(new DateOnly(2026, 4, 2), close: 109m, sma50: 100.2m, sma200: 96.2m, rsi: 68m, macd: 0.24m, signal: 0.23m, hist: -0.02m, ema12: 105.3m, ema26: 103.5m, drawdown: -2.5m)
        };

        var result = _algorithm.ComputeSignals(BuildBars(indicators), BuildContext(indicators));
        var point = result.Points[^1];

        Assert.Equal("falling", point.DebugValues["rsiMomentumState"]);
        Assert.NotNull(point.DebugValues["confirmedSellScore"]);
    }

    [Fact]
    public void ComputeSignals_ShouldRespectMinimumBarsBetweenSameSignal()
    {
        var indicators = Enumerable.Range(0, 8)
            .Select(index => BuildIndicator(
                new DateOnly(2026, 6, 1).AddDays(index),
                close: 102m + index,
                sma50: 100m + (index * 0.2m),
                sma200: 98m,
                rsi: 45m,
                macd: 0.3m,
                signal: 0.1m,
                hist: 0.2m,
                ema12: 101m,
                ema26: 99m,
                drawdown: -8m))
            .ToList();

        var parameters = MetaAlgoParameters.Default with { MinimumBarsBetweenSameSignal = 3 };
        var context = BuildContext(indicators, parameters);

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
        return new ComputedIndicator(
            date,
            sma50,
            sma200,
            rsi,
            drawdown,
            close,
            close - 0.2m,
            macd,
            signal,
            hist,
            null,
            ema12,
            ema26);
    }
}
