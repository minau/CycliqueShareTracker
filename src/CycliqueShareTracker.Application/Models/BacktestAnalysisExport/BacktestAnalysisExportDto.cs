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
    decimal BuyScoreThreshold,
    decimal SellScoreThreshold,
    int MinimumBarsBetweenSameSignal,
    int MaxHoldBars,
    decimal StopLossPercent,
    decimal TakeProfitPercent);

public sealed record BacktestAnalysisAssetDto(
    string Symbol,
    string AssetName,
    IReadOnlyList<BacktestAnalysisCandleDto> Candles,
    IReadOnlyList<BacktestSignal> Signals,
    IReadOnlyList<Trade> Trades,
    BacktestMetrics Metrics,
    string? Error);

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
