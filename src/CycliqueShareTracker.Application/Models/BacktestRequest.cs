namespace CycliqueShareTracker.Application.Models;

public sealed record BacktestRequest(
    DateOnly StartDate,
    DateOnly EndDate,
    IReadOnlyList<string> Symbols,
    bool IncludeMacdInScoring,
    AlgorithmType AlgorithmType = AlgorithmType.RsiMeanReversion,
    StrategyConfig? StrategyConfig = null);
