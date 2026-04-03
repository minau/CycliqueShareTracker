using CycliqueShareTracker.Domain.Enums;

namespace CycliqueShareTracker.Application.Models;

public sealed record SignalHistoryRow(
    DateOnly Date,
    decimal Close,
    decimal? Sma50,
    decimal? Sma200,
    decimal? Rsi14,
    decimal? Drawdown52WeeksPercent,
    int? Score,
    SignalLabel? SignalLabel,
    int? ExitScore,
    ExitSignalLabel? ExitSignalLabel,
    string? ExitPrimaryReason);
