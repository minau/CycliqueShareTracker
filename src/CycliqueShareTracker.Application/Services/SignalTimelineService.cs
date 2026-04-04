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
        int? lastBuyZoneIndex = null;
        int? lastSellZoneIndex = null;

        var ordered = computedByDate.Values.OrderBy(x => x.Date).ToList();
        for (var index = 0; index < ordered.Count; index++)
        {
            var current = ordered[index];
            var entry = _signalService.BuildSignal(current, previous, includeMacdConfirmation);
            var exit = _exitSignalService.BuildExitSignal(current, previous, includeMacdConfirmation);

            entry = ApplyBuyCooldown(entry, index, ref lastBuyZoneIndex);
            exit = ApplySellCooldown(exit, index, ref lastSellZoneIndex);

            timeline[current.Date] = (entry, exit);
            previous = current;
        }

        return timeline;
    }

    private SignalResult ApplyBuyCooldown(SignalResult result, int currentIndex, ref int? lastBuyZoneIndex)
    {
        if (result.Label != SignalLabel.BuyZone)
        {
            return result;
        }

        if (!IsCooldownSatisfied(lastBuyZoneIndex, currentIndex))
        {
            return result with
            {
                Label = SignalLabel.Watch,
                PrimaryReason = "BUY non validé: signal précédent trop récent (cooldown actif).",
                Explanation = $"{result.Explanation}; BUY bloqué par cooldown de {_strategyOptions.MinimumBarsBetweenSameSignal} barres."
            };
        }

        lastBuyZoneIndex = currentIndex;
        return result;
    }

    private ExitSignalResult ApplySellCooldown(ExitSignalResult result, int currentIndex, ref int? lastSellZoneIndex)
    {
        if (result.ExitSignal != ExitSignalLabel.SellZone)
        {
            return result;
        }

        if (!IsCooldownSatisfied(lastSellZoneIndex, currentIndex))
        {
            var downgradedLabel = result.ExitScore >= 45 ? ExitSignalLabel.TrimTakeProfit : ExitSignalLabel.Hold;
            return result with
            {
                ExitSignal = downgradedLabel,
                PrimaryExitReason = $"SELL non validé: signal précédent trop récent (cooldown actif {_strategyOptions.MinimumBarsBetweenSameSignal} barres)."
            };
        }

        lastSellZoneIndex = currentIndex;
        return result;
    }

    private bool IsCooldownSatisfied(int? lastSignalIndex, int currentIndex)
    {
        if (!lastSignalIndex.HasValue || _strategyOptions.MinimumBarsBetweenSameSignal <= 0)
        {
            return true;
        }

        var barsSinceLastSignal = currentIndex - lastSignalIndex.Value;
        return barsSinceLastSignal >= _strategyOptions.MinimumBarsBetweenSameSignal;
    }
}
