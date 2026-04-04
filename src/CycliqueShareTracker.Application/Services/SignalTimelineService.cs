using CycliqueShareTracker.Application.Interfaces;
using CycliqueShareTracker.Application.Models;
using CycliqueShareTracker.Domain.Enums;
using Microsoft.Extensions.Options;

namespace CycliqueShareTracker.Application.Services;

public sealed class SignalTimelineService : ISignalTimelineService
{
    private readonly ISignalService _signalService;
    private readonly IExitSignalService _exitSignalService;
    private readonly SignalStrategyOptions _strategyOptions;

    public SignalTimelineService(
        ISignalService signalService,
        IExitSignalService exitSignalService,
        IOptions<SignalStrategyOptions> strategyOptions)
    {
        _signalService = signalService;
        _exitSignalService = exitSignalService;
        _strategyOptions = strategyOptions.Value;
    }

    public IReadOnlyDictionary<DateOnly, (SignalResult Entry, ExitSignalResult Exit)> BuildSignalTimeline(
        IReadOnlyDictionary<DateOnly, ComputedIndicator> computedByDate,
        bool includeMacdConfirmation = true)
    {
        var timeline = new Dictionary<DateOnly, (SignalResult Entry, ExitSignalResult Exit)>(computedByDate.Count);
        ComputedIndicator? previous = null;
        DateOnly? lastBuyZoneDate = null;
        DateOnly? lastSellZoneDate = null;

        foreach (var current in computedByDate.Values.OrderBy(x => x.Date))
        {
            var entry = _signalService.BuildSignal(current, includeMacdConfirmation);
            var exit = _exitSignalService.BuildExitSignal(current, previous, includeMacdConfirmation);

            entry = ApplyBuyCooldown(entry, current.Date, ref lastBuyZoneDate);
            exit = ApplySellCooldown(exit, current.Date, ref lastSellZoneDate);

            timeline[current.Date] = (entry, exit);
            previous = current;
        }

        return timeline;
    }

    private SignalResult ApplyBuyCooldown(SignalResult result, DateOnly currentDate, ref DateOnly? lastBuyZoneDate)
    {
        if (result.Label != SignalLabel.BuyZone)
        {
            return result;
        }

        if (!IsCooldownSatisfied(lastBuyZoneDate, currentDate))
        {
            return result with
            {
                Label = SignalLabel.Watch,
                PrimaryReason = "BUY non validé: signal précédent trop récent (cooldown actif).",
                Explanation = $"{result.Explanation}; BUY bloqué par cooldown de {_strategyOptions.MinimumBarsBetweenSameSignal} séances."
            };
        }

        lastBuyZoneDate = currentDate;
        return result;
    }

    private ExitSignalResult ApplySellCooldown(ExitSignalResult result, DateOnly currentDate, ref DateOnly? lastSellZoneDate)
    {
        if (result.ExitSignal != ExitSignalLabel.SellZone)
        {
            return result;
        }

        if (!IsCooldownSatisfied(lastSellZoneDate, currentDate))
        {
            var downgradedLabel = result.ExitScore >= 45 ? ExitSignalLabel.TrimTakeProfit : ExitSignalLabel.Hold;
            return result with
            {
                ExitSignal = downgradedLabel,
                PrimaryExitReason = $"SELL non validé: signal précédent trop récent (cooldown actif {_strategyOptions.MinimumBarsBetweenSameSignal} séances)."
            };
        }

        lastSellZoneDate = currentDate;
        return result;
    }

    private bool IsCooldownSatisfied(DateOnly? lastSignalDate, DateOnly currentDate)
    {
        if (!lastSignalDate.HasValue || _strategyOptions.MinimumBarsBetweenSameSignal <= 0)
        {
            return true;
        }

        var daysSinceLastSignal = currentDate.DayNumber - lastSignalDate.Value.DayNumber;
        return daysSinceLastSignal >= _strategyOptions.MinimumBarsBetweenSameSignal;
    }
}
