namespace CycliqueShareTracker.Application.Models;

public sealed record BacktestAssetResult(
    string Symbol,
    string AssetName,
    BacktestMetrics Metrics,
    IReadOnlyList<Trade> Trades,
    string? Error = null);
