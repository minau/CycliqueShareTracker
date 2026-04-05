namespace CycliqueShareTracker.Application.Models;

public sealed record AlgorithmContext(
    IReadOnlyList<ComputedIndicator> Indicators,
    StrategyConfig? StrategyConfig = null);
