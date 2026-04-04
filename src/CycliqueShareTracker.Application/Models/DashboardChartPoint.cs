using CycliqueShareTracker.Domain.Enums;

namespace CycliqueShareTracker.Application.Models;

public sealed record DashboardChartPoint(
    DateOnly Date,
    decimal Open,
    decimal High,
    decimal Low,
    decimal Close,
    decimal? Sma50,
    decimal? Sma200,
    SignalLabel? SignalLabel,
    ExitSignalLabel? ExitSignalLabel);
