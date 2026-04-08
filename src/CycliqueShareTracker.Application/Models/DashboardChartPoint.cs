namespace CycliqueShareTracker.Application.Models;

public sealed record DashboardChartPoint(
    DateOnly Date,
    decimal Open,
    decimal High,
    decimal Low,
    decimal Close,
    decimal? Sma50,
    decimal? Sma200,
    decimal? Rsi14,
    decimal? MacdLine,
    decimal? MacdSignalLine,
    decimal? MacdHistogram,
    decimal? BollingerMiddle,
    decimal? BollingerUpper,
    decimal? BollingerLower,
    decimal? ParabolicSar);
