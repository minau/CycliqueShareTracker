using CycliqueShareTracker.Application.Models;
using CycliqueShareTracker.Application.Services;
using CycliqueShareTracker.Domain.Enums;
using Microsoft.Extensions.Options;
using Xunit;

namespace CycliqueShareTracker.Tests;

public class ExitSignalServiceTests
{
    private static ExitSignalService CreateService(SignalStrategyOptions? options = null)
        => new(Options.Create(options ?? new SignalStrategyOptions()));

    [Fact]
    public void BuildExitSignal_ShouldTriggerEarlySell_WhenWeaknessAppearsBeforeDeepDrop()
    {
        var service = CreateService();
        var previous = new ComputedIndicator(new DateOnly(2026, 04, 01), 100m, 96m, 50m, -4m, 101m, 103m, 0.2m, 0.1m, BearishStreakCount: 1);
        var current = new ComputedIndicator(new DateOnly(2026, 04, 02), 100m, 96m, 46m, -5m, 99.5m, 101m, -0.1m, 0.0m, BearishStreakCount: 2);

        var result = service.BuildExitSignal(current, previous, includeMacdInScoring: true);

        Assert.Equal(ExitSignalLabel.SellZone, result.ExitSignal);
        Assert.Contains("précoce", result.PrimaryExitReason);
    }

    [Fact]
    public void BuildExitSignal_ShouldStayHold_WhenWeaknessContextIsNotCredible()
    {
        var service = CreateService();
        var previous = new ComputedIndicator(new DateOnly(2026, 04, 01), 100m, 97m, 54m, -4m, 101m, 100m, 0.3m, 0.2m, BearishStreakCount: 0);
        var current = new ComputedIndicator(new DateOnly(2026, 04, 02), 100.2m, 97m, 53m, -4m, 101.1m, 101m, 0.25m, 0.20m, BearishStreakCount: 0);

        var result = service.BuildExitSignal(current, previous, includeMacdInScoring: false);

        Assert.Equal(ExitSignalLabel.Hold, result.ExitSignal);
    }
}
