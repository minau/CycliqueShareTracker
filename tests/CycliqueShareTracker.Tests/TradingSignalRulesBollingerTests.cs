using System.Reflection;
using CycliqueShareTracker.Application.Models;
using CycliqueShareTracker.Application.Trading;
using Xunit;

namespace CycliqueShareTracker.Tests;

public sealed class TradingSignalRulesBollingerTests
{
    private static readonly MethodInfo BbBottomUpMethod = typeof(TradingSignalRules)
        .GetMethod("ComputeBbBottomUpSignal", BindingFlags.NonPublic | BindingFlags.Static)!;

    private static readonly MethodInfo BbMidHitUpMethod = typeof(TradingSignalRules)
        .GetMethod("ComputeBbMidHitUp", BindingFlags.NonPublic | BindingFlags.Static)!;

    private static readonly MethodInfo BbMidHitDownMethod = typeof(TradingSignalRules)
        .GetMethod("ComputeBbMidHitDown", BindingFlags.NonPublic | BindingFlags.Static)!;

    [Fact]
    public void BbIsBottomUp_ShouldReturnMinusOne_WhenHighIsInLowerPartWithoutTouchingMiddle()
    {
        var bar = CreateBar(95m, 90m, 93m);
        var indicator = CreateIndicator(bar.Date, close: 93m, mid: 100m, upper: 120m, lower: 80m);

        var result = InvokeBbBottomUp(bar, indicator);

        Assert.Equal(-1, result);
    }

    [Fact]
    public void BbIsBottomUp_ShouldReturnOne_WhenLowIsInUpperPartWithoutTouchingMiddle()
    {
        var bar = CreateBar(110m, 102m, 105m);
        var indicator = CreateIndicator(bar.Date, close: 105m, mid: 100m, upper: 120m, lower: 80m);

        var result = InvokeBbBottomUp(bar, indicator);

        Assert.Equal(1, result);
    }

    [Fact]
    public void BbIsBottomUp_ShouldReturnZero_WhenPriceTouchesOrCrossesWrongZone()
    {
        var bar = CreateBar(100m, 99m, 99m);
        var indicator = CreateIndicator(bar.Date, close: 99m, mid: 100m, upper: 120m, lower: 80m);

        var result = InvokeBbBottomUp(bar, indicator);

        Assert.Equal(0, result);
    }

    [Fact]
    public void BbIsBottomUp_ShouldReturnZero_WhenIndicatorsAreMissing()
    {
        var bar = CreateBar(95m, 90m, 93m);
        var indicator = CreateIndicator(bar.Date, close: 93m, mid: null, upper: 120m, lower: 80m);

        var result = InvokeBbBottomUp(bar, indicator);

        Assert.Equal(0, result);
    }

    [Fact]
    public void BbMidHitUp_ShouldReturnMinusOne_WhenAllConditionsMatchWithTrueJMinusFour()
    {
        var (prices, indicatorsByDate, currentIndex) = BuildSeriesForMidHitUp(includeAllPreviousMidValues: true);

        var currentBar = prices[currentIndex];
        var currentIndicator = indicatorsByDate[currentBar.Date];
        var result = InvokeBbMidHitUp(prices, indicatorsByDate, currentIndex, currentBar, currentIndicator);

        Assert.Equal(-1, result);
    }

    [Fact]
    public void BbMidHitUp_ShouldReturnZero_WhenOneConditionFails()
    {
        var (prices, indicatorsByDate, currentIndex) = BuildSeriesForMidHitUp(includeAllPreviousMidValues: true);
        var currentBar = prices[currentIndex] with { Close = 101m };
        var currentIndicator = indicatorsByDate[currentBar.Date];

        var result = InvokeBbMidHitUp(prices, indicatorsByDate, currentIndex, currentBar, currentIndicator);

        Assert.Equal(0, result);
    }

    [Fact]
    public void BbMidHitUp_ShouldReturnZero_WhenJMinusFourIsUnavailable()
    {
        var prices = new List<PriceBar>
        {
            CreateBar(100m, 90m, 95m, new DateOnly(2026, 1, 1)),
            CreateBar(100m, 90m, 95m, new DateOnly(2026, 1, 2)),
            CreateBar(100m, 90m, 95m, new DateOnly(2026, 1, 3)),
            CreateBar(100m, 90m, 95m, new DateOnly(2026, 1, 4))
        };
        var indicatorsByDate = prices.ToDictionary(
            x => x.Date,
            x => CreateIndicator(x.Date, x.Close, mid: 100m, upper: 120m, lower: 80m));

        var currentIndex = 3;
        var currentBar = prices[currentIndex];
        var currentIndicator = indicatorsByDate[currentBar.Date];
        var result = InvokeBbMidHitUp(prices, indicatorsByDate, currentIndex, currentBar, currentIndicator);

        Assert.Equal(0, result);
    }

    [Fact]
    public void BbMidHitUp_ShouldUseFourthPreviousValidPoint_NotCalendarOffset()
    {
        var (prices, indicatorsByDate, currentIndex) = BuildSeriesForMidHitUp(includeAllPreviousMidValues: false);

        var currentBar = prices[currentIndex];
        var currentIndicator = indicatorsByDate[currentBar.Date];
        var result = InvokeBbMidHitUp(prices, indicatorsByDate, currentIndex, currentBar, currentIndicator);

        Assert.Equal(-1, result);
    }

    [Fact]
    public void BbMidHitDown_ShouldReturnOne_WhenAllConditionsMatchWithTrueJMinusFour()
    {
        var (prices, indicatorsByDate, currentIndex) = BuildSeriesForMidHitDown(includeAllPreviousMidValues: true);

        var currentBar = prices[currentIndex];
        var currentIndicator = indicatorsByDate[currentBar.Date];
        var result = InvokeBbMidHitDown(prices, indicatorsByDate, currentIndex, currentBar, currentIndicator);

        Assert.Equal(1, result);
    }

    [Fact]
    public void BbMidHitDown_ShouldReturnZero_WhenOneConditionFails()
    {
        var (prices, indicatorsByDate, currentIndex) = BuildSeriesForMidHitDown(includeAllPreviousMidValues: true);
        var currentBar = prices[currentIndex] with { Close = 99m };
        var currentIndicator = indicatorsByDate[currentBar.Date];

        var result = InvokeBbMidHitDown(prices, indicatorsByDate, currentIndex, currentBar, currentIndicator);

        Assert.Equal(0, result);
    }

    [Fact]
    public void BbMidHitDown_ShouldReturnZero_WhenJMinusFourIsUnavailable()
    {
        var prices = new List<PriceBar>
        {
            CreateBar(110m, 105m, 108m, new DateOnly(2026, 2, 1)),
            CreateBar(110m, 105m, 108m, new DateOnly(2026, 2, 2)),
            CreateBar(110m, 105m, 108m, new DateOnly(2026, 2, 3)),
            CreateBar(110m, 105m, 108m, new DateOnly(2026, 2, 4))
        };
        var indicatorsByDate = prices.ToDictionary(
            x => x.Date,
            x => CreateIndicator(x.Date, x.Close, mid: 100m, upper: 120m, lower: 80m));

        var currentIndex = 3;
        var currentBar = prices[currentIndex];
        var currentIndicator = indicatorsByDate[currentBar.Date];
        var result = InvokeBbMidHitDown(prices, indicatorsByDate, currentIndex, currentBar, currentIndicator);

        Assert.Equal(0, result);
    }

    [Fact]
    public void BbMidHitDown_ShouldUseFourthPreviousValidPoint_NotCalendarOffset()
    {
        var (prices, indicatorsByDate, currentIndex) = BuildSeriesForMidHitDown(includeAllPreviousMidValues: false);

        var currentBar = prices[currentIndex];
        var currentIndicator = indicatorsByDate[currentBar.Date];
        var result = InvokeBbMidHitDown(prices, indicatorsByDate, currentIndex, currentBar, currentIndicator);

        Assert.Equal(1, result);
    }

    private static int InvokeBbBottomUp(PriceBar bar, ComputedIndicator indicator)
        => (int)BbBottomUpMethod.Invoke(null, new object[] { bar, indicator })!;

    private static int InvokeBbMidHitUp(
        IReadOnlyList<PriceBar> prices,
        IReadOnlyDictionary<DateOnly, ComputedIndicator> indicatorsByDate,
        int currentIndex,
        PriceBar currentBar,
        ComputedIndicator currentIndicator)
        => (int)BbMidHitUpMethod.Invoke(null, new object[] { prices, indicatorsByDate, currentIndex, currentBar, currentIndicator })!;

    private static int InvokeBbMidHitDown(
        IReadOnlyList<PriceBar> prices,
        IReadOnlyDictionary<DateOnly, ComputedIndicator> indicatorsByDate,
        int currentIndex,
        PriceBar currentBar,
        ComputedIndicator currentIndicator)
        => (int)BbMidHitDownMethod.Invoke(null, new object[] { prices, indicatorsByDate, currentIndex, currentBar, currentIndicator })!;

    private static (IReadOnlyList<PriceBar> Prices, IReadOnlyDictionary<DateOnly, ComputedIndicator> IndicatorsByDate, int CurrentIndex)
        BuildSeriesForMidHitUp(bool includeAllPreviousMidValues)
    {
        var prices = new List<PriceBar>
        {
            CreateBar(90m, 85m, 89m, new DateOnly(2026, 3, 1)),
            CreateBar(120m, 110m, 115m, new DateOnly(2026, 3, 2)), // J-4 attendu si tous valides
            CreateBar(102m, 95m, 100m, new DateOnly(2026, 3, 3)),
            CreateBar(99m, 94m, 97m, new DateOnly(2026, 3, 4)),
            CreateBar(98m, 93m, 96m, new DateOnly(2026, 3, 5)),
            CreateBar(95m, 90m, 94m, new DateOnly(2026, 3, 6))
        };

        var indicatorsByDate = prices.ToDictionary(
            x => x.Date,
            x => CreateIndicator(x.Date, x.Close, mid: 100m, upper: 120m, lower: 80m));

        if (!includeAllPreviousMidValues)
        {
            // Ce point précédent n'est pas exploitable pour J-4 car la BBMid est absente.
            indicatorsByDate[prices[4].Date] = indicatorsByDate[prices[4].Date] with { BollingerMiddle = null };
        }

        return (prices, indicatorsByDate, 5);
    }

    private static (IReadOnlyList<PriceBar> Prices, IReadOnlyDictionary<DateOnly, ComputedIndicator> IndicatorsByDate, int CurrentIndex)
        BuildSeriesForMidHitDown(bool includeAllPreviousMidValues)
    {
        var prices = new List<PriceBar>
        {
            CreateBar(95m, 90m, 93m, new DateOnly(2026, 4, 1)),
            CreateBar(90m, 85m, 88m, new DateOnly(2026, 4, 2)), // J-4 attendu si tous valides
            CreateBar(98m, 92m, 96m, new DateOnly(2026, 4, 3)),
            CreateBar(99m, 93m, 97m, new DateOnly(2026, 4, 4)),
            CreateBar(101m, 95m, 99m, new DateOnly(2026, 4, 5)),
            CreateBar(110m, 105m, 108m, new DateOnly(2026, 4, 6))
        };

        var indicatorsByDate = prices.ToDictionary(
            x => x.Date,
            x => CreateIndicator(x.Date, x.Close, mid: 100m, upper: 120m, lower: 80m));

        if (!includeAllPreviousMidValues)
        {
            // Ce point précédent n'est pas exploitable pour J-4 car la BBMid est absente.
            indicatorsByDate[prices[4].Date] = indicatorsByDate[prices[4].Date] with { BollingerMiddle = null };
        }

        return (prices, indicatorsByDate, 5);
    }

    private static PriceBar CreateBar(decimal high, decimal low, decimal close, DateOnly? date = null)
        => new(date ?? new DateOnly(2026, 1, 1), close, high, low, close, 1_000);

    private static ComputedIndicator CreateIndicator(DateOnly date, decimal close, decimal? mid, decimal? upper, decimal? lower)
        => new(
            date,
            null,
            null,
            50m,
            null,
            close,
            null,
            1m,
            1m,
            1m,
            null,
            null,
            null,
            mid,
            upper,
            lower,
            2m,
            close - 1m);
}
