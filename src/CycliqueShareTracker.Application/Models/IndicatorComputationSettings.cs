using CycliqueShareTracker.Domain.Entities;

namespace CycliqueShareTracker.Application.Models;

public sealed record IndicatorComputationSettings(
    decimal ParabolicSarStep,
    decimal ParabolicSarMax,
    int BollingerPeriod,
    decimal BollingerStdDev,
    int MacdFastPeriod,
    int MacdSlowPeriod,
    int MacdSignalPeriod)
{
    public static IndicatorComputationSettings Default { get; } = new(
        StockIndicatorSettings.DefaultParabolicSarStep,
        StockIndicatorSettings.DefaultParabolicSarMax,
        StockIndicatorSettings.DefaultBollingerPeriod,
        StockIndicatorSettings.DefaultBollingerStdDev,
        StockIndicatorSettings.DefaultMacdFastPeriod,
        StockIndicatorSettings.DefaultMacdSlowPeriod,
        StockIndicatorSettings.DefaultMacdSignalPeriod);

    public static IndicatorComputationSettings FromEntity(StockIndicatorSettings settings)
    {
        return new IndicatorComputationSettings(
            settings.ParabolicSarStep,
            settings.ParabolicSarMax,
            settings.BollingerPeriod,
            settings.BollingerStdDev,
            settings.MacdFastPeriod,
            settings.MacdSlowPeriod,
            settings.MacdSignalPeriod);
    }
}
