using CycliqueShareTracker.Application.Models;

namespace CycliqueShareTracker.Application.Algorithms;

public sealed class MacdReversalAlgorithm : SignalAlgorithmBase
{
    public override AlgorithmType AlgorithmType => AlgorithmType.MacdReversal;
    public override string DisplayName => "MACD Reversal";

    public override AlgorithmResult ComputeSignals(IReadOnlyList<PriceBar> bars, AlgorithmContext context)
        => new(AlgorithmType, DisplayName, Array.Empty<AlgorithmSignalPoint>());
}
