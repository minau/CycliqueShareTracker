namespace CycliqueShareTracker.Application.Models;

public sealed record PriceBar(DateOnly Date, decimal Open, decimal High, decimal Low, decimal Close, long Volume);
