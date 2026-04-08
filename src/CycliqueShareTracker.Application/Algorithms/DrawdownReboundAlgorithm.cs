using CycliqueShareTracker.Application.Models;

namespace CycliqueShareTracker.Application.Algorithms;

public sealed class DrawdownReboundAlgorithm : SignalAlgorithmBase
{
    public override AlgorithmType AlgorithmType => AlgorithmType.DrawdownRebound;
    public override string DisplayName => "Drawdown Rebound";

    public override AlgorithmResult ComputeSignals(IReadOnlyList<PriceBar> bars, AlgorithmContext context)
        => new(AlgorithmType, DisplayName, Array.Empty<AlgorithmSignalPoint>());
}
