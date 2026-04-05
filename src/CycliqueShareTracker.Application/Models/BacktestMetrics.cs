namespace CycliqueShareTracker.Application.Models;

public sealed record BacktestMetrics(
    int TotalTrades,
    int WinningTrades,
    int LosingTrades,
    decimal WinRatePercent,
    decimal AverageGainPercent,
    decimal AverageLossPercent,
    decimal ProfitFactor,
    decimal TotalPerformancePercent,
    decimal MaxDrawdownPercent,
    decimal AverageTradeDurationDays);
