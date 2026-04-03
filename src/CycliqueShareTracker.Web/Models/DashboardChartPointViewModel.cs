namespace CycliqueShareTracker.Web.Models;

public sealed class DashboardChartPointViewModel
{
    public string Date { get; init; } = string.Empty;
    public decimal Close { get; init; }
    public decimal? Sma50 { get; init; }
    public decimal? Sma200 { get; init; }
    public bool IsBuyZone { get; init; }
}
