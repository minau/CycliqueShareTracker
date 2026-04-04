namespace CycliqueShareTracker.Application.Models;

public sealed class WatchlistOptions
{
    public const string SectionName = "Watchlist";
    public List<TrackedAssetOptions> Assets { get; set; } = new()
    {
        new() { Symbol = "TTE.PA", Name = "TotalEnergies", Sector = "Energy", Market = "Euronext Paris" },
        new() { Symbol = "CAT", Name = "Caterpillar", Sector = "Industrials", Market = "NYSE" },
        new() { Symbol = "NXPI", Name = "NXP Semiconductors", Sector = "Semiconductors", Market = "NASDAQ" },
        new() { Symbol = "DAL", Name = "Delta Air Lines", Sector = "Airlines", Market = "NYSE" },
        new() { Symbol = "PHM", Name = "PulteGroup", Sector = "Homebuilding", Market = "NYSE" }
    };
}

public sealed class TrackedAssetOptions
{
    public string Symbol { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Sector { get; set; } = "Unknown";
    public string Market { get; set; } = string.Empty;
}
