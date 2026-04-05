using CycliqueShareTracker.Application.Models;

namespace CycliqueShareTracker.Application.Algorithms;

public sealed class TrendFollowingAlgorithm : SignalAlgorithmBase
{
    public override AlgorithmType AlgorithmType => AlgorithmType.TrendFollowing;
    public override string DisplayName => "Trend Following SMA/EMA";

    public override AlgorithmResult ComputeSignals(IReadOnlyList<PriceBar> bars, AlgorithmContext context)
    {
        var points = new List<AlgorithmSignalPoint>(context.Indicators.Count);

        foreach (var current in context.Indicators)
        {
            var buyZone = current.Sma50.HasValue && current.Sma200.HasValue && current.Ema12.HasValue && current.Ema26.HasValue && current.Rsi14.HasValue &&
                          current.Sma50 > current.Sma200 && current.Ema12 > current.Ema26 && current.Rsi14 > 50m;
            var sellZone = (current.Ema12.HasValue && current.Ema26.HasValue && current.Ema12 < current.Ema26) ||
                           (current.Rsi14.HasValue && current.Rsi14 < 50m);

            var buyDetails = new List<ScoreFactorDetail>
            {
                new("SMA50 > SMA200", 35, current.Sma50.HasValue && current.Sma200.HasValue && current.Sma50 > current.Sma200, "Tendance de fond positive."),
                new("EMA12 > EMA26", 35, current.Ema12.HasValue && current.Ema26.HasValue && current.Ema12 > current.Ema26, "Momentum court terme haussier."),
                new("RSI14 > 50", 30, current.Rsi14.HasValue && current.Rsi14 > 50m, "Momentum supérieur à sa ligne médiane.")
            };
            var sellDetails = new List<ScoreFactorDetail>
            {
                new("EMA12 < EMA26", 60, current.Ema12.HasValue && current.Ema26.HasValue && current.Ema12 < current.Ema26, "Perte de momentum."),
                new("RSI14 < 50", 40, current.Rsi14.HasValue && current.Rsi14 < 50m, "Momentum redevenu fragile.")
            };
            var buyScore = CountTriggeredScore(buyDetails);

            points.Add(new AlgorithmSignalPoint(
                current.Date,
                buyZone,
                sellZone,
                buyZone,
                sellZone,
                buyScore,
                CountTriggeredScore(sellDetails),
                decimal.Round(buyScore / 100m, 2),
                buyZone ? "Tendance alignée SMA/EMA avec RSI > 50." : "Tendance incomplète pour entrer.",
                sellZone ? "Momentum cassé (EMA ou RSI)." : "Pas de signal de sortie.",
                buyDetails,
                sellDetails));
        }

        return new AlgorithmResult(AlgorithmType, DisplayName, points);
    }
}
