using CycliqueShareTracker.Domain.Enums;

namespace CycliqueShareTracker.Application.Models;

public sealed record DashboardChartPoint(
    DateOnly Date,
    decimal Open,
    decimal High,
    decimal Low,
    decimal Close,
    decimal? Sma50,
    decimal? Sma200,
    decimal? Rsi14,
    decimal? MacdLine,
    decimal? MacdSignalLine,
    decimal? MacdHistogram,
    int? EntryScore,
    string? EntryPrimaryReason,
    IReadOnlyList<ScoreFactorDetail> EntryScoreFactors,
    SignalLabel? SignalLabel,
    int? ExitScore,
    string? ExitPrimaryReason,
    IReadOnlyList<ScoreFactorDetail> ExitScoreFactors,
    ExitSignalLabel? ExitSignalLabel,
    bool IsBuyZone,
    bool IsSellZone,
    bool BuySignal,
    bool SellSignal);
