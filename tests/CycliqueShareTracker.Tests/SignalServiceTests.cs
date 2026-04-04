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
    public void BuildSignal_ShouldReturnBuyZone_WhenAllBlockingFiltersPass()
    {
        var service = CreateService();
        var previous = new ComputedIndicator(new DateOnly(2026, 04, 01), 104m, 100m, 48m, -7m, 106m, 105m, 0.6m, 0.5m);
        var current = new ComputedIndicator(new DateOnly(2026, 04, 02), 105m, 100m, 50m, -6m, 107m, 106m, 0.8m, 0.6m);

        var result = service.BuildSignal(current, previous, includeMacdInScoring: true);

        Assert.Equal(SignalLabel.BuyZone, result.Label);
        Assert.True(result.Score >= 70);
    }

    [Fact]
    public void BuildSignal_ShouldBlockBuy_WhenPriceTooFarAboveSma50()
    {
        var service = CreateService();
        var previous = new ComputedIndicator(new DateOnly(2026, 04, 01), 100m, 95m, 50m, -7m, 101m, 100m, 0.5m, 0.4m);
        var current = new ComputedIndicator(new DateOnly(2026, 04, 02), 101m, 95m, 55m, -6m, 108m, 101m, 0.8m, 0.6m);

        var result = service.BuildSignal(current, previous, includeMacdInScoring: true);

        Assert.NotEqual(SignalLabel.BuyZone, result.Label);
        Assert.Contains("distance prix/SMA50", result.PrimaryReason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BuildSignal_ShouldIgnoreMacdFilter_WhenDisabledAtRuntime()
    {
        var service = CreateService();
        var previous = new ComputedIndicator(new DateOnly(2026, 04, 01), 104m, 100m, 48m, -7m, 106m, 105m, 0.7m, 0.6m);
        var current = new ComputedIndicator(new DateOnly(2026, 04, 02), 105m, 100m, 50m, -6m, 107m, 106m, 0.6m, 0.8m);

        var result = service.BuildSignal(current, previous, includeMacdInScoring: false);

        Assert.Equal(SignalLabel.BuyZone, result.Label);
    }
}
