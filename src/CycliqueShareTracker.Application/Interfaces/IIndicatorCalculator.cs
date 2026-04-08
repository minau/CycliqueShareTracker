using CycliqueShareTracker.Application.Models;

namespace CycliqueShareTracker.Application.Interfaces;

public interface IIndicatorCalculator
{
    IReadOnlyList<ComputedIndicator> Compute(IReadOnlyList<PriceBar> prices);
    IReadOnlyList<BollingerBandsPoint> ComputeBollingerBands(IReadOnlyList<PriceBar> prices, int period = 20, decimal standardDeviationMultiplier = 2.0m);
    IReadOnlyList<ParabolicSarPoint> ComputeParabolicSar(IReadOnlyList<PriceBar> prices, decimal step = 0.02m, decimal maxStep = 0.20m);
    IReadOnlyList<EnrichedPriceBar> EnrichWithTechnicalIndicators(IReadOnlyList<PriceBar> prices, int bollingerPeriod = 20, decimal bollingerMultiplier = 2.0m, decimal parabolicSarStep = 0.02m, decimal parabolicSarMaxStep = 0.20m);
}
