using CycliqueShareTracker.Application.Models;

namespace CycliqueShareTracker.Application.Interfaces;

public interface ISignalService
{
    SignalResult BuildSignal(
        ComputedIndicator indicator,
        bool includeMacdInScoring = true,
        ComputedIndicator? previous = null,
        StrategyConfig? strategyConfig = null);
}
