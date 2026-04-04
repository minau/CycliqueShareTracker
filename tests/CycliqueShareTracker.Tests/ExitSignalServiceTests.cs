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
    public void BuildExitSignal_ShouldReturnSellZone_WhenWeaknessSignalsStack()
    {
        var service = CreateService();
        var previous = new ComputedIndicator(new DateOnly(2026, 04, 01), 100m, 96m, 50m, -4m, 101m, 103m, 0.2m, 0.1m);
        var current = new ComputedIndicator(new DateOnly(2026, 04, 02), 99m, 96m, 43m, -5m, 95m, 101m, -0.1m, 0.0m);

        var result = service.BuildExitSignal(current, previous, includeMacdInScoring: true);

        Assert.Equal(ExitSignalLabel.SellZone, result.ExitSignal);
        Assert.True(result.ExitScore >= 70);
    }

    [Fact]
    public void BuildExitSignal_ShouldNotReturnSellZone_WhenWeaknessContextIsMissing()
    {
        var service = CreateService();
        var previous = new ComputedIndicator(new DateOnly(2026, 04, 01), 100m, 97m, 52m, -4m, 101m, 100m, 0.3m, 0.2m);
        var current = new ComputedIndicator(new DateOnly(2026, 04, 02), 100.01m, 97m, 51m, -4m, 101.2m, 101m, 0.25m, 0.20m);

        var result = service.BuildExitSignal(current, previous, includeMacdInScoring: false);

        Assert.NotEqual(ExitSignalLabel.SellZone, result.ExitSignal);
        Assert.Contains("contexte de faiblesse", result.PrimaryExitReason);
    }

    [Fact]
    public void BuildExitSignal_ShouldIgnoreMacdConfirmation_WhenDisabledInConfig()
    {
        var service = CreateService(new SignalStrategyOptions { EnableMacdConfirmation = false });
        var previous = new ComputedIndicator(new DateOnly(2026, 04, 01), 100m, 96m, 50m, -4m, 101m, 103m, 0.2m, 0.1m);
        var current = new ComputedIndicator(new DateOnly(2026, 04, 02), 99m, 96m, 43m, -5m, 95m, 101m, 0.5m, 0.2m);

        var result = service.BuildExitSignal(current, previous, includeMacdInScoring: true);

        Assert.Equal(ExitSignalLabel.SellZone, result.ExitSignal);
    }
}
