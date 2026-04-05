using CycliqueShareTracker.Application.Models;

namespace CycliqueShareTracker.Application.Interfaces;

public interface IExitSignalService
{
    ExitSignalResult BuildExitSignal(
        ComputedIndicator current,
        ComputedIndicator? previous,
        bool includeMacdInScoring = true,
        StrategyConfig? strategyConfig = null);
}
