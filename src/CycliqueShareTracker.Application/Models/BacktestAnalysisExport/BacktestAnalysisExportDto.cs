using CycliqueShareTracker.Application.Models;

namespace CycliqueShareTracker.Application.Models.BacktestAnalysisExport;

public sealed record BacktestAnalysisExportDto(
    BacktestAnalysisMetadataDto Metadata,
    BacktestAnalysisAlgorithmDto Algorithm,
    IReadOnlyList<BacktestAnalysisAssetDto> Assets,
    BacktestMetrics GlobalMetrics);

public sealed record BacktestAnalysisMetadataDto(
    DateTime GeneratedAtUtc,
    DateOnly StartDate,
    DateOnly EndDate,
    string SymbolSelection,
    IReadOnlyList<string> Symbols,
    bool IncludeMacdInScoring,
    DateTime? BacktestExecutedAtUtc);

public sealed record BacktestAnalysisAlgorithmDto(
    string Name,
    string Type,
    BacktestAnalysisAlgorithmParametersDto Parameters);

public sealed record BacktestAnalysisAlgorithmParametersDto(
    decimal FeePercentPerSide,
    int BuyScoreThreshold,
    int SellScoreThreshold,
    decimal MinRsiForBuy,
    decimal MaxRsiForBuy,
    decimal MinRsiWeaknessForSell,
    bool EnableMacdConfirmation,
    int MinimumBarsBetweenSameSignal,
    decimal MaxDistanceAboveSma50ForBuyPct,
    decimal MinSma50SlopeForBuy,
    decimal MaxFlatSlopeThreshold,
    decimal MinGapBetweenSma50AndSma200Pct,
    bool EarlySellEnabled,
    int EarlySellWeaknessScoreThreshold,
    decimal StrongExtensionAboveSma50ForSellPct,
    MetaAlgoParameters MetaAlgoParameters);


public sealed record BacktestAnalysisAssetDto(
    string Symbol,
    string AssetName,
    IReadOnlyList<BacktestAnalysisCandleDto> Candles,
    IReadOnlyList<BacktestSignal> Signals,
    IReadOnlyList<BacktestAnalysisTradeEnvelopeDto> Trades,
    BacktestMetrics Metrics,
    string? Error);

public sealed record BacktestAnalysisTradeEnvelopeDto(
    BacktestAnalysisTradeDto Trade);

public sealed record BacktestAnalysisTradeDto(
    DateOnly EntryDate,
    DateOnly ExitDate,
    decimal EntryPrice,
    decimal ExitPrice,
    decimal PnL,
    decimal MaxDrawdown,
    decimal MaxProfit,
    int HoldingDays,
    IReadOnlyList<string> EntryReasons,
    IReadOnlyList<string> ExitReasons);

public sealed record BacktestAnalysisCandleDto(
    DateOnly Date,
    decimal? Open,
    decimal? High,
    decimal? Low,
    decimal? Close,
    long? Volume,
    BacktestAnalysisIndicatorsDto Indicators,
    BacktestAnalysisAlgorithmPointDto Analysis);

public sealed record BacktestAnalysisIndicatorsDto(
    decimal? Sma50,
    decimal? Sma200,
    decimal? Ema12,
    decimal? Ema26,
    decimal? Rsi14,
    decimal? Drawdown52w,
    decimal? MacdLine,
    decimal? SignalLine,
    decimal? Histogram);

public sealed record BacktestAnalysisAlgorithmPointDto(
    bool? BuyZone,
    bool? SellZone,
    int? BuyScore,
    int? SellScore,
    string? SignalType,
    IReadOnlyList<string>? Reasons,
    IReadOnlyDictionary<string, object?>? DebugValues);
