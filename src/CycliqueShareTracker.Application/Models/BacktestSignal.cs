namespace CycliqueShareTracker.Application.Models;

public sealed record BacktestSignal(
    DateOnly Date,
    string SignalType,
    int? Score,
    string Reason);
