using CycliqueShareTracker.Application.Models;

namespace CycliqueShareTracker.Application.Interfaces;

public interface ISignalService
{
    SignalResult BuildSignal(ComputedIndicator indicator);
}
