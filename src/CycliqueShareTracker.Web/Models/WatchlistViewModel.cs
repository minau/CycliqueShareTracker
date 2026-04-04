namespace CycliqueShareTracker.Web.Models;

public sealed class WatchlistViewModel
{
    public bool IncludeMacdInScoring { get; init; } = true;
    public string SortBy { get; init; } = "buy";
    public string Filter { get; init; } = "all";
    public string? TopBuySymbol { get; init; }
    public string? TopSellSymbol { get; init; }
    public IReadOnlyList<WatchlistItemViewModel> Items { get; init; } = Array.Empty<WatchlistItemViewModel>();
}

public sealed class WatchlistItemViewModel
{
    public string Symbol { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Sector { get; init; } = "Unknown";
    public decimal? LastClose { get; init; }
    public decimal? Sma50 { get; init; }
    public decimal? Sma200 { get; init; }
    public decimal? Rsi14 { get; init; }
    public decimal? Drawdown52WeeksPercent { get; init; }
    public int? BuyScore { get; init; }
    public int? SellScore { get; init; }
    public string Status { get; init; } = "Neutral";
    public string PrimaryReason { get; init; } = "N/A";
    public string? Error { get; init; }
}
