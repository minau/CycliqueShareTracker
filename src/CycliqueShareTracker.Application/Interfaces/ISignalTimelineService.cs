using CycliqueShareTracker.Application.Models;

namespace CycliqueShareTracker.Application.Interfaces;

public interface ISignalTimelineService
{
    IReadOnlyDictionary<DateOnly, (SignalResult Entry, ExitSignalResult Exit)> BuildSignalTimeline(
        IReadOnlyDictionary<DateOnly, ComputedIndicator> computedByDate,
        bool includeMacdConfirmation = true);
}
