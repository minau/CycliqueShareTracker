namespace CycliqueShareTracker.Application.Models;

public sealed record TradingSignal(
    DateOnly Date,
    TradingSignalType Type,
    int Score,
    string Reason,
    IReadOnlyList<string> SignalReasons);
