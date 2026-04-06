using CycliqueShareTracker.Application.Models;

namespace CycliqueShareTracker.Application.Interfaces;

public interface ISignalAlgorithmRegistry
{
    ISignalAlgorithm Get(AlgorithmType algorithmType);
    IReadOnlyList<ISignalAlgorithm> GetAll();
}
