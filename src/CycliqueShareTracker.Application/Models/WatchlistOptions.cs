namespace CycliqueShareTracker.Application.Models;

public sealed class WatchlistOptions
{
    public const string SectionName = "Watchlist";
    public static IReadOnlyList<TrackedAssetOptions> DefaultAssets { get; } = new List<TrackedAssetOptions>
    {
        new() { Symbol = "TTE.PA", Name = "TotalEnergies", Sector = "Energy", Market = "Euronext Paris" },
        new() { Symbol = "SU.PA", Name = "Schneider Electric", Sector = "Industrials", Market = "Euronext Paris" },
        new() { Symbol = "GLE.PA", Name = "Société Générale", Sector = "Financial Services", Market = "Euronext Paris" },
        new() { Symbol = "BNP.PA", Name = "BNP Paribas", Sector = "Financial Services", Market = "Euronext Paris" },
        new() { Symbol = "GOOGL", Name = "Alphabet", Sector = "Communication Services", Market = "NASDAQ" },
        new() { Symbol = "TSLA", Name = "Tesla", Sector = "Automotive", Market = "NASDAQ" },
        new() { Symbol = "CAC40", Name = "CAC 40", Sector = "Index", Market = "Euronext Paris" }
    };

    public List<TrackedAssetOptions> Assets { get; set; } = new();

    public static IReadOnlyList<TrackedAssetOptions> BuildTrackedAssets(IReadOnlyList<TrackedAssetOptions>? configuredAssets)
    {
        var source = configuredAssets is { Count: > 0 }
            ? configuredAssets
            : DefaultAssets;

        return source
            .Where(asset => !string.IsNullOrWhiteSpace(asset.Symbol))
            .GroupBy(asset => asset.Symbol.Trim(), StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToList();
    }
}

public sealed class TrackedAssetOptions
{
    public string Symbol { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Sector { get; set; } = "Unknown";
    public string Market { get; set; } = string.Empty;
}
