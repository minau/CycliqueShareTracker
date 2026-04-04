namespace CycliqueShareTracker.Application.Models;

public sealed record ComputedIndicator(
    DateOnly Date,
    decimal? Sma50,
    decimal? Sma200,
    decimal? Rsi14,
    decimal? Drawdown52WeeksPercent,
    decimal Close,
    decimal? PreviousClose,
    decimal? MacdLine = null,
    decimal? MacdSignalLine = null,
    decimal? MacdHistogram = null,
    decimal? PreviousMacdHistogram = null,
    decimal? Ema12 = null,
    decimal? Ema26 = null,
    int BullishStreakCount = 0,
    int BearishStreakCount = 0);
