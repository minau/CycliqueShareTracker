namespace CycliqueShareTracker.Application.Models;

public sealed class SignalStrategyOptions
{
    public const string SectionName = "SignalStrategy";

    public int BuyScoreThreshold { get; set; } = 70;
    public int SellScoreThreshold { get; set; } = 70;
    public decimal MaxRsiForBuy { get; set; } = 60m;
    public decimal MinRsiForSell { get; set; } = 40m;
    public bool EnableMacdConfirmation { get; set; } = true;
    public int MinimumBarsBetweenSameSignal { get; set; } = 8;
}
