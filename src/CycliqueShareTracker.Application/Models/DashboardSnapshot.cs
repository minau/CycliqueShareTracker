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
    decimal? Rsi14,
    decimal? Drawdown52WeeksPercent,
    int? Score,
    SignalLabel? SignalLabel,
    IReadOnlyList<DashboardChartPoint> ChartPoints,
    IReadOnlyList<PriceBar> RecentPrices);
