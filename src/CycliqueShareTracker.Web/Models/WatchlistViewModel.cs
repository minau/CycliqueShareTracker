namespace CycliqueShareTracker.Web.Models;

public sealed class WatchlistViewModel
{
    public string ActiveAlgorithmType { get; init; } = "RsiMeanReversion";
    public string ActiveAlgorithmName { get; init; } = "RSI Mean Reversion";
    public string SortBy { get; init; } = "ticker";
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
    public string? Error { get; init; }
}
