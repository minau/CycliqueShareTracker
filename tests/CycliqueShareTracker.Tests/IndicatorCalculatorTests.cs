using CycliqueShareTracker.Application.Models;
using CycliqueShareTracker.Application.Services;
using Xunit;

namespace CycliqueShareTracker.Tests;

public class IndicatorCalculatorTests
{
    [Fact]
    public void Compute_ShouldReturnRsi100_WhenNoLosses()
    {
        var calculator = new IndicatorCalculator();
        var data = new List<PriceBar>();
        var start = new DateOnly(2025, 1, 1);

        for (var i = 0; i < 20; i++)
        {
            data.Add(new PriceBar(start.AddDays(i), 10 + i, 11 + i, 9 + i, 10 + i, 100));
        }

        var computed = calculator.Compute(data);
        var latest = computed.Last();

        Assert.Equal(100m, latest.Rsi14);
    }

    [Fact]
    public void Compute_ShouldReturnNegativeDrawdown_WhenBelowRecentHigh()
    {
        var calculator = new IndicatorCalculator();
        var data = new List<PriceBar>();
        var start = new DateOnly(2025, 1, 1);

        data.Add(new PriceBar(start, 100, 110, 90, 105, 100));
        data.Add(new PriceBar(start.AddDays(1), 106, 112, 101, 110, 100));
        data.Add(new PriceBar(start.AddDays(2), 104, 108, 95, 96, 100));

        var computed = calculator.Compute(data);
        var latest = computed.Last();

        Assert.True(latest.Drawdown52WeeksPercent < 0);
    }

    [Fact]
    public void Compute_ShouldReturnNullMacdComponents_WhenHistoryIsInsufficient()
    {
        var calculator = new IndicatorCalculator();
        var data = new List<PriceBar>();
        var start = new DateOnly(2025, 1, 1);

        for (var i = 0; i < 20; i++)
        {
            data.Add(new PriceBar(start.AddDays(i), 100, 101, 99, 100 + i, 100));
        }

        var latest = calculator.Compute(data).Last();

        Assert.Null(latest.MacdLine);
        Assert.Null(latest.MacdSignalLine);
        Assert.Null(latest.MacdHistogram);
    }

    [Fact]
    public void Compute_ShouldReturnMacdValues_WhenHistoryIsSufficient()
    {
        var calculator = new IndicatorCalculator();
        var data = new List<PriceBar>();
        var start = new DateOnly(2025, 1, 1);

        for (var i = 0; i < 80; i++)
        {
            var close = 100m + (i * 0.6m);
            data.Add(new PriceBar(start.AddDays(i), close - 1m, close + 1m, close - 2m, close, 100));
        }

        var latest = calculator.Compute(data).Last();

        Assert.True(latest.MacdLine.HasValue);
        Assert.True(latest.MacdSignalLine.HasValue);
        Assert.True(latest.MacdHistogram.HasValue);
        Assert.True(latest.MacdLine > latest.MacdSignalLine);
        Assert.True(latest.PreviousMacdHistogram.HasValue);
    }

    [Fact]
    public void Compute_ShouldInitializeMacdSignalLine_AfterMacdWarmupWindow()
    {
        var calculator = new IndicatorCalculator();
        var data = new List<PriceBar>();
        var start = new DateOnly(2025, 1, 1);

        for (var i = 0; i < 60; i++)
        {
            var close = 100m + (i * 0.4m);
            data.Add(new PriceBar(start.AddDays(i), close, close, close, close, 100));
        }

        var computed = calculator.Compute(data);

        Assert.Null(computed[32].MacdSignalLine);
        Assert.True(computed[33].MacdSignalLine.HasValue);
        Assert.True(computed[33].MacdHistogram.HasValue);
    }

    [Fact]
    public void Compute_ShouldInitializeAndUpdateEma12_UsingSmaSeedAndStandardFormula()
    {
        var calculator = new IndicatorCalculator();
        var data = new List<PriceBar>();
        var start = new DateOnly(2025, 1, 1);

        for (var i = 1; i <= 20; i++)
        {
            data.Add(new PriceBar(start.AddDays(i - 1), i, i, i, i, 100));
        }

        var computed = calculator.Compute(data);
        var emaSeedIndex = 11;
        var emaNextIndex = 12;

        Assert.Equal(6.5m, computed[emaSeedIndex].Ema12);
        Assert.Equal(7.5m, computed[emaNextIndex].Ema12);
    }

    [Fact]
    public void Compute_ShouldReturnFirstEma26_AsSma26WhenEnoughData()
    {
        var calculator = new IndicatorCalculator();
        var data = new List<PriceBar>();
        var start = new DateOnly(2025, 1, 1);

        for (var i = 1; i <= 30; i++)
        {
            data.Add(new PriceBar(start.AddDays(i - 1), i, i, i, i, 100));
        }

        var computed = calculator.Compute(data);
        Assert.Equal(13.5m, computed[25].Ema26);
        Assert.Null(computed[24].Ema26);
    }
}
