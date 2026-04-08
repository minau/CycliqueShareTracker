namespace CycliqueShareTracker.Application.Models;

public sealed record ParabolicSarPoint(
    DateOnly Date,
    decimal? Sar,
    bool? IsUpTrend,
    decimal? ExtremePoint,
    decimal? AccelerationFactor,
    bool IsReversal);
