using CycliqueShareTracker.Domain.Enums;

namespace CycliqueShareTracker.Application.Models;

public sealed record DashboardChartPoint(
    DateOnly Date,
    decimal Close,
    decimal? Sma50,
    decimal? Sma200,
    SignalLabel? SignalLabel);
