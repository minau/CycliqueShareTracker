namespace CycliqueShareTracker.Application.Models;

public sealed record BollingerBandsPoint(
    DateOnly Date,
    decimal? Middle,
    decimal? Upper,
    decimal? Lower,
    decimal? StdDev);
