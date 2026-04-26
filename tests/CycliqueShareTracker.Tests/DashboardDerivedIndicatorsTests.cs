using CycliqueShareTracker.Application.Services;
using Xunit;

namespace CycliqueShareTracker.Tests;

public sealed class DashboardDerivedIndicatorsTests
{
    private static readonly Type DashboardServiceType = typeof(DashboardService);

    [Theory]
    [InlineData(80, 3)]
    [InlineData(70, 2)]
    [InlineData(50, 1)]
    [InlineData(49, -1)]
    [InlineData(30, -2)]
    [InlineData(20, -3)]
    [InlineData(0.80, 3)]
    [InlineData(0.50, 1)]
    [InlineData(0.20, -3)]
    public void ComputeRsiStrengthAbs_ShouldMatchThresholds(decimal rsi, int expected)
    {
        var result = (int?)Invoke("ComputeRsiStrengthAbs", rsi);

        Assert.Equal(expected, result);
    }

    [Fact]
    public void ComputeRsiStrengthAbs_ShouldReturnNull_WhenMissing()
    {
        var result = (int?)Invoke("ComputeRsiStrengthAbs", null);

        Assert.Null(result);
    }

    [Fact]
    public void ComputeMacdTrendCount_ShouldUseFourDayWindowIncludingCurrentDay()
    {
        var history = new string?[] { "acc2", "acc2", "dec2", "acc2" };

        var result = (int)Invoke("ComputeMacdTrendCount", history, 3)!;

        Assert.Equal(2, result);
    }

    [Fact]
    public void ComputeMacdTrendCount_ShouldReturnNegativeWhenDecDominates()
    {
        var history = new string?[] { "dec2", "dec2", "acc2", "dec2" };

        var result = (int)Invoke("ComputeMacdTrendCount", history, 3)!;

        Assert.Equal(-2, result);
    }

    [Theory]
    [InlineData(1, -1, -1)]
    [InlineData(-1, 1, 1)]
    [InlineData(1, 0, 1)]
    [InlineData(-1, 0, -1)]
    [InlineData(3, 2, 0)]
    [InlineData(-2, -3, 0)]
    public void ComputeMacdTrendChange_ShouldDetectOnlyZeroCrossings(int current, int previous, int expected)
    {
        var result = (int)Invoke("ComputeMacdTrendChange", current, previous)!;

        Assert.Equal(expected, result);
    }

    [Fact]
    public void ComputeCountDaysSinceChg_ShouldUseSevenDaySlidingWindow()
    {
        var history = new string?[] { "ACHAT", "VENTE", "VENTE", "ACHAT", "VENTE", "ACHAT", "VENTE", "VENTE", "ACHAT" };

        var venteCount = (int)Invoke("ComputeCountDaysSinceChgVente", history, 8)!;
        var achatCount = (int)Invoke("ComputeCountDaysSinceChgAchat", history, 8)!;

        Assert.Equal(4, venteCount);
        Assert.Equal(3, achatCount);
    }

    [Fact]
    public void ComputeCountDaysSinceChg_ShouldBeCappedAtSeven()
    {
        var history = Enumerable.Repeat<string?>("VENTE", 10).ToArray();

        var venteCount = (int)Invoke("ComputeCountDaysSinceChgVente", history, 9)!;

        Assert.Equal(7, venteCount);
    }

    [Fact]
    public void ComputeTrendPositionOnChange_ShouldReturnAchatOnValidSwitch()
    {
        var wayChanges = new decimal?[] { -0.1m, -0.2m, -0.3m, -0.1m, 0.2m };
        var trends = new List<string?> { "VENTE", "VENTE", "VENTE", "VENTE" };

        var result = (string?)Invoke("ComputeTrendPositionOnChange", wayChanges, trends, 4, 100m, 101m);

        Assert.Equal("ACHAT", result);
    }

    [Fact]
    public void ComputeTrendPositionOnChange_ShouldReturnVenteOnValidSwitch()
    {
        var wayChanges = new decimal?[] { 0.1m, 0.2m, 0.3m, 0.1m, -0.2m };
        var trends = new List<string?> { "ACHAT", "ACHAT", "ACHAT", "ACHAT" };

        var result = (string?)Invoke("ComputeTrendPositionOnChange", wayChanges, trends, 4, 100m, 99m);

        Assert.Equal("VENTE", result);
    }

    [Fact]
    public void ComputeTrendPositionOnChange_ShouldFallbackToPreviousTrendWhenNoSwitch()
    {
        var wayChanges = new decimal?[] { 0.1m, 0.2m, 0.1m, 0.2m, 0.1m };
        var trends = new List<string?> { "ACHAT", "ACHAT", "ACHAT", "ACHAT" };

        var result = (string?)Invoke("ComputeTrendPositionOnChange", wayChanges, trends, 4, 100m, 99m);

        Assert.Equal("ACHAT", result);
    }

    private static object? Invoke(string methodName, params object?[] args)
    {
        var methods = DashboardServiceType
            .GetMethods(System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)
            .Where(m => m.Name == methodName)
            .ToList();

        var method = methods.Single(m => m.GetParameters().Length == args.Length);
        return method.Invoke(null, args);
    }
}
