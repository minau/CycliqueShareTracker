namespace CycliqueShareTracker.Web.Models;

public sealed class DashboardChartPointViewModel
{
    public string Date { get; init; } = string.Empty;
    public decimal Open { get; init; }
    public decimal High { get; init; }
    public decimal Low { get; init; }
    public decimal Close { get; init; }
    public decimal? Sma50 { get; init; }
    public decimal? Sma200 { get; init; }
    public decimal? Rsi14 { get; init; }
    public decimal? MacdLine { get; init; }
    public decimal? MacdSignalLine { get; init; }
    public decimal? MacdHistogram { get; init; }
    public decimal? BollingerMiddle { get; init; }
    public decimal? BollingerUpper { get; init; }
    public decimal? BollingerLower { get; init; }
    public decimal? ParabolicSar { get; init; }
    public int? EntryScore { get; init; }
    public string EntryPrimaryReason { get; init; } = "N/A";
    public IReadOnlyList<SignalScoreFactorViewModel> EntryScoreFactors { get; init; } = Array.Empty<SignalScoreFactorViewModel>();
    public bool IsBuyZone { get; init; }
    public bool BuySignal { get; init; }
    public int? ExitScore { get; init; }
    public string ExitPrimaryReason { get; init; } = "N/A";
    public IReadOnlyList<SignalScoreFactorViewModel> ExitScoreFactors { get; init; } = Array.Empty<SignalScoreFactorViewModel>();
    public bool IsSellZone { get; init; }
    public bool SellSignal { get; init; }
}
