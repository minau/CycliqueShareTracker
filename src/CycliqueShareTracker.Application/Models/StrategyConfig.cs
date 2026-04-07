namespace CycliqueShareTracker.Application.Models;

public sealed record StrategyConfig
{
    public static StrategyConfig Default { get; } = new();

    public int BuyScoreThreshold { get; init; } = 70;
    public int SellScoreThreshold { get; init; } = 65;
    public decimal MinRsiForBuy { get; init; } = 35m;
    public decimal MaxRsiForBuy { get; init; } = 55m;
    public decimal MinRsiWeaknessForSell { get; init; } = 70m;
    public bool EnableMacdConfirmation { get; init; } = true;
    public int MinimumBarsBetweenSameSignal { get; init; } = 3;
    public decimal MaxDistanceAboveSma50ForBuyPct { get; init; } = 8m;
    public decimal MinSma50SlopeForBuy { get; init; } = 0m;
    public decimal MaxFlatSlopeThreshold { get; init; } = 0.05m;
    public decimal MinGapBetweenSma50AndSma200Pct { get; init; } = 1m;
    public bool EarlySellEnabled { get; init; } = true;
    public int EarlySellWeaknessScoreThreshold { get; init; } = 45;
    public decimal StrongExtensionAboveSma50ForSellPct { get; init; } = 10m;
    public MetaAlgoParameters MetaAlgoParameters { get; init; } = MetaAlgoParameters.Default;
    public decimal FeePercentPerSide { get; init; } = 0.1m;
}
