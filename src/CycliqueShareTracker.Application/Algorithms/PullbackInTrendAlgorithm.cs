using CycliqueShareTracker.Application.Models;

namespace CycliqueShareTracker.Application.Algorithms;

public sealed class PullbackInTrendAlgorithm : SignalAlgorithmBase
{
    public override AlgorithmType AlgorithmType => AlgorithmType.PullbackInTrend;
    public override string DisplayName => "Pullback in Trend";

    public override AlgorithmResult ComputeSignals(IReadOnlyList<PriceBar> bars, AlgorithmContext context)
        => new(AlgorithmType, DisplayName, Array.Empty<AlgorithmSignalPoint>());
}
