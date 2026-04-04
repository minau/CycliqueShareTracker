using CycliqueShareTracker.Application.Models;
using CycliqueShareTracker.Application.Services;
using CycliqueShareTracker.Domain.Enums;
using Microsoft.Extensions.Options;
using System;
using Xunit;

namespace CycliqueShareTracker.Tests;

public class SignalServiceTests
{
    private static SignalService CreateService(SignalStrategyOptions? options = null)
        => new(Options.Create(options ?? new SignalStrategyOptions()));

    [Fact]
    public void BuildSignal_ShouldReturnBuyZone_OnPullbackInBullTrend()
    {
        var service = CreateService();
        var previous = new ComputedIndicator(new DateOnly(2026, 04, 01), 104m, 100m, 49m, -7m, 106m, 105m, 0.6m, 0.5m, BullishStreakCount: 1);
        var current = new ComputedIndicator(new DateOnly(2026, 04, 02), 105m, 100m, 50m, -6m, 106.5m, 106m, 0.8m, 0.6m, BullishStreakCount: 1);

        var result = service.BuildSignal(current, previous, includeMacdInScoring: true);

        Assert.Equal(SignalLabel.BuyZone, result.Label);
    }

    [Fact]
    public void BuildSignal_ShouldBlockBuy_WhenPriceIsTooExtendedAboveSma50()
    {
        var service = CreateService();
        var previous = new ComputedIndicator(new DateOnly(2026, 04, 01), 100m, 95m, 52m, -7m, 101m, 100m, 0.7m, 0.6m, BullishStreakCount: 2);
        var current = new ComputedIndicator(new DateOnly(2026, 04, 02), 101m, 95m, 56m, -6m, 109m, 101m, 0.8m, 0.6m, BullishStreakCount: 3);

        var result = service.BuildSignal(current, previous, includeMacdInScoring: true);

        Assert.NotEqual(SignalLabel.BuyZone, result.Label);
        Assert.Contains("distance au-dessus SMA50", result.PrimaryReason, StringComparison.OrdinalIgnoreCase);
    }
}
