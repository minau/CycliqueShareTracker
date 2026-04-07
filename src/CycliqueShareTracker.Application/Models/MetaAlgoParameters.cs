namespace CycliqueShareTracker.Application.Models;

public sealed record MetaAlgoParameters
{
    public static MetaAlgoParameters Default { get; } = new();

    public int BuyScoreThreshold { get; init; } = 68;
    public int SellScoreThreshold { get; init; } = 62;
    public decimal MinRsiForBuy { get; init; } = 35m;
    public decimal MaxRsiForBuy { get; init; } = 62m;
    public decimal MinRsiWeaknessForSell { get; init; } = 50m;
    public decimal MaxDistanceAboveSma50ForBuyPct { get; init; } = 5.5m;
    public decimal StrongExtensionAboveSma50ForSellPct { get; init; } = 10m;
    public decimal MinSma50SlopeForBuy { get; init; } = 0.02m;
    public decimal MaxFlatSlopeThreshold { get; init; } = 0.04m;
    public decimal MinGapBetweenSma50AndSma200Pct { get; init; } = 0.5m;
    public int MinimumBarsBetweenSameSignal { get; init; } = 3;
    public bool EarlySellEnabled { get; init; } = true;
    public int EarlySellWeaknessScoreThreshold { get; init; } = 35;
    public bool EnableMacdConfirmation { get; init; } = true;
}
