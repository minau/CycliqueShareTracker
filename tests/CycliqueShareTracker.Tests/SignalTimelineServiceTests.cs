using CycliqueShareTracker.Application.Models;
using CycliqueShareTracker.Application.Services;
using CycliqueShareTracker.Domain.Enums;
using Microsoft.Extensions.Options;
using Xunit;

namespace CycliqueShareTracker.Tests;

public class SignalTimelineServiceTests
{
    [Fact]
    public void BuildSignalTimeline_ShouldApplyCooldown_ForRepeatedBuyAndSellSignals()
    {
        var strategyOptions = Options.Create(new SignalStrategyOptions
        {
            MinimumBarsBetweenSameSignal = 3,
            EnableMacdConfirmation = false
        });

        var signalService = new SignalService(strategyOptions);
        var exitSignalService = new ExitSignalService(strategyOptions);
        var timelineService = new SignalTimelineService(signalService, exitSignalService, strategyOptions);

        var data = new Dictionary<DateOnly, ComputedIndicator>
        {
            [new DateOnly(2026, 04, 01)] = new(new DateOnly(2026, 04, 01), 120m, 100m, 50m, -8m, 121m, 119m, 1.2m, 1.0m),
            [new DateOnly(2026, 04, 02)] = new(new DateOnly(2026, 04, 02), 121m, 100m, 49m, -7m, 122m, 121m, 1.3m, 1.0m),
            [new DateOnly(2026, 04, 03)] = new(new DateOnly(2026, 04, 03), 100m, 95m, 58m, -7m, 92m, 122m, 0.8m, 1.0m),
            [new DateOnly(2026, 04, 04)] = new(new DateOnly(2026, 04, 04), 99m, 94m, 57m, -8m, 91m, 92m, 0.7m, 1.0m)
        };

        var result = timelineService.BuildSignalTimeline(data, includeMacdConfirmation: false);

        Assert.Equal(SignalLabel.BuyZone, result[new DateOnly(2026, 04, 01)].Entry.Label);
        Assert.Equal(SignalLabel.Watch, result[new DateOnly(2026, 04, 02)].Entry.Label);

        Assert.Equal(ExitSignalLabel.SellZone, result[new DateOnly(2026, 04, 03)].Exit.ExitSignal);
        Assert.Equal(ExitSignalLabel.TrimTakeProfit, result[new DateOnly(2026, 04, 04)].Exit.ExitSignal);
    }
}
