using CycliqueShareTracker.Domain.Enums;

namespace CycliqueShareTracker.Application.Models;

public sealed record DashboardSnapshot(
    string AssetSymbol,
    string AssetName,
    DateOnly? LastUpdateDate,
    decimal? LastClose,
    decimal? DayChangePercent,
    decimal? Sma50,
    decimal? Sma200,
    decimal? Ema12,
    decimal? Ema26,
    decimal? Rsi14,
    decimal? Drawdown52WeeksPercent,
    decimal? MacdLine,
    decimal? MacdSignalLine,
    decimal? MacdHistogram,
    int? Score,
    SignalLabel? SignalLabel,
    string? EntryPrimaryReason,
    IReadOnlyList<ScoreFactorDetail> EntryScoreFactors,
    int? ExitScore,
    ExitSignalLabel? ExitSignalLabel,
    string? ExitPrimaryReason,
    IReadOnlyList<ScoreFactorDetail> ExitScoreFactors,
    IReadOnlyList<DashboardChartPoint> ChartPoints,
    IReadOnlyList<PriceBar> RecentPrices);
