namespace CycliqueShareTracker.Application.Models;

public static class AlgorithmTypeExtensions
{
    public static string ToDisplayName(this AlgorithmType algorithmType)
    {
        return algorithmType switch
        {
            AlgorithmType.RsiMeanReversion => "RSI Mean Reversion",
            AlgorithmType.EmaCrossover => "EMA Crossover",
            AlgorithmType.MacdReversal => "MACD Reversal",
            AlgorithmType.TrendFollowing => "Trend Following SMA/EMA",
            AlgorithmType.PullbackInTrend => "Pullback in Trend",
            AlgorithmType.DrawdownRebound => "Drawdown Rebound",
            AlgorithmType.CompositeTrendPullback => "Composite Trend Pullback",
            _ => algorithmType.ToString()
        };
    }
}
