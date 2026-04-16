namespace CycliqueShareTracker.Web.Models;

public sealed class IndicatorSettingsFormModel
{
    public string Symbol { get; init; } = string.Empty;
    public string AlgorithmType { get; init; } = "RsiMeanReversion";
    public decimal ParabolicSarStep { get; init; }
    public decimal ParabolicSarMax { get; init; }
    public int BollingerPeriod { get; init; }
    public decimal BollingerStdDev { get; init; }
    public int MacdFastPeriod { get; init; }
    public int MacdSlowPeriod { get; init; }
    public int MacdSignalPeriod { get; init; }
}
