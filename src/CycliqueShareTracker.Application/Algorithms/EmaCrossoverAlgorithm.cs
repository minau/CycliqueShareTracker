using CycliqueShareTracker.Application.Models;

namespace CycliqueShareTracker.Application.Algorithms;

public sealed class EmaCrossoverAlgorithm : SignalAlgorithmBase
{
    public override AlgorithmType AlgorithmType => AlgorithmType.EmaCrossover;
    public override string DisplayName => "EMA Crossover";

    public override AlgorithmResult ComputeSignals(IReadOnlyList<PriceBar> bars, AlgorithmContext context)
        => new(AlgorithmType, DisplayName, Array.Empty<AlgorithmSignalPoint>());
}
