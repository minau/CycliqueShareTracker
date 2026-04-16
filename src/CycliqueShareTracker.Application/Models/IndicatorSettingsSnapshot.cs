namespace CycliqueShareTracker.Application.Models;

public sealed record IndicatorSettingsSnapshot(
    decimal ParabolicSarStep,
    decimal ParabolicSarMax,
    int BollingerPeriod,
    decimal BollingerStdDev,
    int MacdFastPeriod,
    int MacdSlowPeriod,
    int MacdSignalPeriod,
    DateTime UpdatedAtUtc);
