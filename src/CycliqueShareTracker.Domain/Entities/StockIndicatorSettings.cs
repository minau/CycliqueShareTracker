namespace CycliqueShareTracker.Domain.Entities;

public class StockIndicatorSettings
{
    public const decimal DefaultParabolicSarStep = 0.02m;
    public const decimal DefaultParabolicSarMax = 0.20m;
    public const int DefaultBollingerPeriod = 20;
    public const decimal DefaultBollingerStdDev = 2.0m;
    public const int DefaultMacdFastPeriod = 12;
    public const int DefaultMacdSlowPeriod = 26;
    public const int DefaultMacdSignalPeriod = 9;

    public int Id { get; set; }
    public string Symbol { get; set; } = string.Empty;
    public decimal ParabolicSarStep { get; set; } = DefaultParabolicSarStep;
    public decimal ParabolicSarMax { get; set; } = DefaultParabolicSarMax;
    public int BollingerPeriod { get; set; } = DefaultBollingerPeriod;
    public decimal BollingerStdDev { get; set; } = DefaultBollingerStdDev;
    public int MacdFastPeriod { get; set; } = DefaultMacdFastPeriod;
    public int MacdSlowPeriod { get; set; } = DefaultMacdSlowPeriod;
    public int MacdSignalPeriod { get; set; } = DefaultMacdSignalPeriod;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

    public static StockIndicatorSettings CreateDefault(string symbol)
    {
        return new StockIndicatorSettings
        {
            Symbol = symbol,
            UpdatedAtUtc = DateTime.UtcNow
        };
    }
}
