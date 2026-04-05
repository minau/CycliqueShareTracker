namespace CycliqueShareTracker.Application.Models;

public sealed record Trade(
    string Symbol,
    DateOnly EntryDate,
    decimal EntryPrice,
    DateOnly ExitDate,
    decimal ExitPrice,
    decimal PerformancePercent,
    int DurationDays,
    string EntryReason,
    string ExitReason);
