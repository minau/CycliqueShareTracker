using CycliqueShareTracker.Application.Models;

namespace CycliqueShareTracker.Application.Algorithms;

public sealed class PullbackInTrendAlgorithm : SignalAlgorithmBase
{
    public override AlgorithmType AlgorithmType => AlgorithmType.PullbackInTrend;
    public override string DisplayName => "Pullback in Trend";

    public override AlgorithmResult ComputeSignals(IReadOnlyList<PriceBar> bars, AlgorithmContext context)
    {
        var points = new List<AlgorithmSignalPoint>(context.Indicators.Count);
        ComputedIndicator? previous = null;

        foreach (var current in context.Indicators)
        {
            var rsiInRange = current.Rsi14.HasValue && current.Rsi14.Value >= 40m && current.Rsi14.Value <= 55m;
            var rsiRecovering = previous?.Rsi14.HasValue == true && current.Rsi14.HasValue && current.Rsi14.Value > previous.Rsi14.Value && current.Rsi14.Value >= 40m;
            var moderateDrawdown = current.Drawdown52WeeksPercent.HasValue && current.Drawdown52WeeksPercent.Value <= -4m && current.Drawdown52WeeksPercent.Value >= -18m;
            var bullishTrend = current.Sma50.HasValue && current.Sma200.HasValue && current.Ema12.HasValue && current.Ema26.HasValue &&
                               current.Sma50 > current.Sma200 && current.Ema12 > current.Ema26;

            var buyZone = bullishTrend && moderateDrawdown && (rsiInRange || rsiRecovering);
            var histogramDeteriorating = current.MacdHistogram.HasValue && previous?.MacdHistogram.HasValue == true && current.MacdHistogram.Value < previous.MacdHistogram.Value;
            var sellZone = (current.Ema12.HasValue && current.Ema26.HasValue && current.Ema12 < current.Ema26) || histogramDeteriorating || (current.Rsi14.HasValue && current.Rsi14 < 50m);

            var buyDetails = new List<ScoreFactorDetail>
            {
                new("SMA50 > SMA200", 25, current.Sma50.HasValue && current.Sma200.HasValue && current.Sma50 > current.Sma200, "Tendance de fond validée."),
                new("EMA12 > EMA26", 25, current.Ema12.HasValue && current.Ema26.HasValue && current.Ema12 > current.Ema26, "Momentum de tendance toujours positif."),
                new("RSI14 entre 40 et 55 ou en reprise", 30, rsiInRange || rsiRecovering, "Pullback sain ou reprise de momentum."),
                new("Drawdown 52w modéré", 20, moderateDrawdown, "Repli exploitable sans rupture majeure.")
            };
            var sellDetails = new List<ScoreFactorDetail>
            {
                new("EMA12 < EMA26", 45, current.Ema12.HasValue && current.Ema26.HasValue && current.Ema12 < current.Ema26, "Inversion du momentum."),
                new("Histogram MACD en dégradation", 30, histogramDeteriorating, "Essoufflement du rebond."),
                new("RSI14 < 50", 25, current.Rsi14.HasValue && current.Rsi14 < 50m, "Momentum sous neutralité.")
            };

            points.Add(new AlgorithmSignalPoint(
                current.Date,
                buyZone,
                sellZone,
                buyZone,
                sellZone,
                CountTriggeredScore(buyDetails),
                CountTriggeredScore(sellDetails),
                null,
                buyZone ? "Pullback dans une tendance haussière confirmé." : "Pas de configuration complète de pullback.",
                sellZone ? "Critères de fragilité détectés sur la tendance." : "Pas de signal de sortie.",
                buyDetails,
                sellDetails));

            previous = current;
        }

        return new AlgorithmResult(AlgorithmType, DisplayName, points);
    }
}
