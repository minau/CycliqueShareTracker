using CycliqueShareTracker.Application.Models;
using CycliqueShareTracker.Application.Services;
using CycliqueShareTracker.Domain.Enums;
using Microsoft.Extensions.Options;
using System;
using Xunit;

namespace CycliqueShareTracker.Tests;

public class ExitSignalServiceTests
{
    private static ExitSignalService CreateService(SignalStrategyOptions? options = null)
        => new(Options.Create(options ?? new SignalStrategyOptions()));

    [Fact]
    public void BuildExitSignal_ShouldReturnSellZone_WhenTrendWeakensAndFiltersPass()
    {
        var service = CreateService();
        var previous = new ComputedIndicator(new DateOnly(2026, 04, 01), 101m, 96m, 65m, -5m, 102m, 101m, 1.1m, 1.0m);
        var current = new ComputedIndicator(new DateOnly(2026, 04, 02), 100m, 95m, 58m, -7m, 92m, 102m, 0.8m, 1.0m);

        var result = service.BuildExitSignal(current, previous, includeMacdInScoring: true);

        Assert.Equal(ExitSignalLabel.SellZone, result.ExitSignal);
        Assert.True(result.ExitScore >= 70);
    }

    [Fact]
    public void BuildExitSignal_ShouldNotReturnSellZone_WhenRsiIsBelowFloor()
    {
        var service = CreateService();
        var previous = new ComputedIndicator(new DateOnly(2026, 04, 01), 101m, 96m, 42m, -5m, 102m, 101m, 1.1m, 1.0m);
        var current = new ComputedIndicator(new DateOnly(2026, 04, 02), 100m, 95m, 35m, -7m, 92m, 102m, 0.8m, 1.0m);

        var result = service.BuildExitSignal(current, previous, includeMacdInScoring: true);

        Assert.NotEqual(ExitSignalLabel.SellZone, result.ExitSignal);
        Assert.Contains("RSI trop bas", result.PrimaryExitReason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BuildExitSignal_ShouldIgnoreMacdConfirmation_WhenToggleIsOff()
    {
        var service = CreateService();
        var previous = new ComputedIndicator(new DateOnly(2026, 04, 01), 101m, 96m, 65m, -5m, 102m, 101m, 1.1m, 1.0m);
        var current = new ComputedIndicator(new DateOnly(2026, 04, 02), 100m, 95m, 58m, -7m, 92m, 102m, 1.2m, 0.9m);

        var result = service.BuildExitSignal(current, previous, includeMacdInScoring: false);

        Assert.Equal(ExitSignalLabel.SellZone, result.ExitSignal);
    }
}
