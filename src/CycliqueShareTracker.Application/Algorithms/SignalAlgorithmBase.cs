using CycliqueShareTracker.Application.Interfaces;
using CycliqueShareTracker.Application.Models;

namespace CycliqueShareTracker.Application.Algorithms;

public abstract class SignalAlgorithmBase : ISignalAlgorithm
{
    public abstract AlgorithmType AlgorithmType { get; }
    public abstract string DisplayName { get; }

    public abstract AlgorithmResult ComputeSignals(IReadOnlyList<PriceBar> bars, AlgorithmContext context);

    protected static int ScoreFromRsi(decimal? rsi, decimal lower, decimal upper, bool buyDirection)
    {
        if (!rsi.HasValue)
        {
            return 0;
        }

        if (buyDirection)
        {
            if (rsi.Value >= lower)
            {
                return 0;
            }

            var span = Math.Max(lower, 1m);
            var intensity = (lower - rsi.Value) / span;
            return Math.Clamp((int)Math.Round(50m + intensity * 50m), 0, 100);
        }

        if (rsi.Value <= upper)
        {
            return 0;
        }

        var denominator = Math.Max(100m - upper, 1m);
        var sellIntensity = (rsi.Value - upper) / denominator;
        return Math.Clamp((int)Math.Round(50m + sellIntensity * 50m), 0, 100);
    }

    protected static int CountTriggeredScore(IEnumerable<ScoreFactorDetail> factors)
    {
        var score = factors.Where(f => f.Triggered).Sum(f => f.Points);
        return Math.Clamp(score, 0, 100);
    }
}
