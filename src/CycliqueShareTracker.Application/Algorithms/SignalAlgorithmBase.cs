using CycliqueShareTracker.Application.Interfaces;
using CycliqueShareTracker.Application.Models;

namespace CycliqueShareTracker.Application.Algorithms;

public abstract class SignalAlgorithmBase : ISignalAlgorithm
{
    public abstract AlgorithmType AlgorithmType { get; }
    public abstract string DisplayName { get; }

    public abstract AlgorithmResult ComputeSignals(IReadOnlyList<PriceBar> bars, AlgorithmContext context);
}
