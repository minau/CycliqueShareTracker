namespace CycliqueShareTracker.Application.Models;

public sealed record ComputedIndicator(
    DateOnly Date,
    decimal? Sma50,
    decimal? Sma200,
    decimal? Rsi14,
    decimal? Drawdown52WeeksPercent,
    decimal Close,
    decimal? PreviousClose);
