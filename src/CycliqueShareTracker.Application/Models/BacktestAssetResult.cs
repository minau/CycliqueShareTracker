namespace CycliqueShareTracker.Application.Models;

public sealed record BacktestAssetResult(
    string Symbol,
    string AssetName,
    BacktestMetrics Metrics,
    IReadOnlyList<Trade> Trades,
    string? Error = null,
    IReadOnlyList<PriceBar>? OhlcvBars = null,
    IReadOnlyList<ComputedIndicator>? Indicators = null,
    AlgorithmResult? AlgorithmResult = null,
    IReadOnlyList<BacktestSignal>? Signals = null);
