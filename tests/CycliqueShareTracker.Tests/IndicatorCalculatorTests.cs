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
}
