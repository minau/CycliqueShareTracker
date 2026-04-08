namespace CycliqueShareTracker.Application.Models;

public sealed record EnrichedPriceBar(
    PriceBar Price,
    ComputedIndicator Indicator,
    BollingerBandsPoint BollingerBands,
    ParabolicSarPoint ParabolicSar);
