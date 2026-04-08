using CycliqueShareTracker.Application.Models;

namespace CycliqueShareTracker.Application.Algorithms;

public sealed class TrendFollowingAlgorithm : SignalAlgorithmBase
{
    public override AlgorithmType AlgorithmType => AlgorithmType.TrendFollowing;
    public override string DisplayName => "Trend Following";

    public override AlgorithmResult ComputeSignals(IReadOnlyList<PriceBar> bars, AlgorithmContext context)
        => new(AlgorithmType, DisplayName, Array.Empty<AlgorithmSignalPoint>());
}
