using CycliqueShareTracker.Application.Models;

namespace CycliqueShareTracker.Application.Interfaces;

public interface ISignalAlgorithm
{
    AlgorithmType AlgorithmType { get; }
    string DisplayName { get; }

    AlgorithmResult ComputeSignals(IReadOnlyList<PriceBar> bars, AlgorithmContext context);
}
