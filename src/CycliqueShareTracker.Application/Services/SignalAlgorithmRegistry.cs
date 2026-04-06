using CycliqueShareTracker.Application.Interfaces;
using CycliqueShareTracker.Application.Models;

namespace CycliqueShareTracker.Application.Services;

public sealed class SignalAlgorithmRegistry : ISignalAlgorithmRegistry
{
    private readonly IReadOnlyDictionary<AlgorithmType, ISignalAlgorithm> _algorithms;

    public SignalAlgorithmRegistry(IEnumerable<ISignalAlgorithm> algorithms)
    {
        _algorithms = algorithms.ToDictionary(a => a.AlgorithmType);
    }

    public ISignalAlgorithm Get(AlgorithmType algorithmType)
    {
        if (_algorithms.TryGetValue(algorithmType, out var algorithm))
        {
            return algorithm;
        }

        return _algorithms[AlgorithmType.RsiMeanReversion];
    }

    public IReadOnlyList<ISignalAlgorithm> GetAll()
    {
        return _algorithms.Values.OrderBy(x => x.AlgorithmType).ToList();
    }
}
