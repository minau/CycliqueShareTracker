namespace CycliqueShareTracker.Web.Models;

public sealed class SignalHistoryRowViewModel
{
    public string Date { get; init; } = string.Empty;
    public decimal Close { get; init; }
    public decimal? Sma50 { get; init; }
    public decimal? Sma200 { get; init; }
    public decimal? Rsi14 { get; init; }
    public decimal? Drawdown52WeeksPercent { get; init; }
    public int? Score { get; init; }
    public string Signal { get; init; } = "N/A";
}
