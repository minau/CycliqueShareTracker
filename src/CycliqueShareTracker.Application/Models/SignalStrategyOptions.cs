namespace CycliqueShareTracker.Application.Models;

public sealed class SignalStrategyOptions
{
    public const string SectionName = "SignalStrategy";

    public int BuyScoreThreshold { get; set; } = 70;
    public int SellScoreThreshold { get; set; } = 70;
    public decimal MinRsiForBuy { get; set; } = 40m;
    public decimal MaxRsiForBuy { get; set; } = 60m;
    public decimal MaxDistanceFromSma50ForBuyPct { get; set; } = 5m;
    public decimal MinRsiBreakdownForSell { get; set; } = 45m;
    public bool EnableMacdConfirmation { get; set; } = true;
    public int MinimumBarsBetweenSameSignal { get; set; } = 8;
    public decimal MinSma50SlopeForBuy { get; set; } = 0.05m;
    public decimal MaxFlatSlopeThreshold { get; set; } = 0.02m;
    public decimal MinGapBetweenSma50AndSma200Pct { get; set; } = 1.5m;
}
