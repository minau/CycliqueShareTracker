namespace CycliqueShareTracker.Application.Models;

public sealed record AlgorithmResult(
    AlgorithmType AlgorithmType,
    string AlgorithmName,
    IReadOnlyList<AlgorithmSignalPoint> Points);
