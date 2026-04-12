using CycliqueShareTracker.Application.Trading;

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
    IReadOnlyList<DashboardChartPoint> ChartPoints,
    IReadOnlyList<TradeMarker> TradeMarkers,
    TrackedPosition CurrentPosition,
    IReadOnlyList<PriceBar> RecentPrices,
    AlgorithmType AlgorithmType,
    string AlgorithmName);
