namespace CycliqueShareTracker.Application.Models;

public sealed record BacktestResult(
    BacktestRequest Request,
    BacktestMetrics AggregateMetrics,
    IReadOnlyList<BacktestAssetResult> Assets);
