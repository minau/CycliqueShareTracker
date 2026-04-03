using CycliqueShareTracker.Application.Models;

namespace CycliqueShareTracker.Web.Models;

public sealed class DashboardViewModel
{
    public string AssetSymbol { get; init; } = string.Empty;
    public string AssetName { get; init; } = string.Empty;
    public DateOnly? LastUpdateDate { get; init; }
    public decimal? LastClose { get; init; }
    public decimal? DayChangePercent { get; init; }
    public decimal? Sma50 { get; init; }
    public decimal? Sma200 { get; init; }
    public decimal? Rsi14 { get; init; }
    public decimal? Drawdown52WeeksPercent { get; init; }
    public int? Score { get; init; }
    public string Signal { get; init; } = "N/A";
    public string? Notice { get; init; }
    public IReadOnlyList<PriceBar> RecentPrices { get; init; } = Array.Empty<PriceBar>();

    public static DashboardViewModel FromSnapshot(DashboardSnapshot snapshot, string? notice = null)
    {
        return new DashboardViewModel
        {
            AssetSymbol = snapshot.AssetSymbol,
            AssetName = snapshot.AssetName,
            LastUpdateDate = snapshot.LastUpdateDate,
            LastClose = snapshot.LastClose,
            DayChangePercent = snapshot.DayChangePercent,
            Sma50 = snapshot.Sma50,
            Sma200 = snapshot.Sma200,
            Rsi14 = snapshot.Rsi14,
            Drawdown52WeeksPercent = snapshot.Drawdown52WeeksPercent,
            Score = snapshot.Score,
            Signal = snapshot.SignalLabel?.ToString().ToUpperInvariant().Replace("NOBUY", "NO BUY").Replace("BUYZONE", "BUY ZONE") ?? "N/A",
            Notice = notice,
            RecentPrices = snapshot.RecentPrices
        };
    }
}
