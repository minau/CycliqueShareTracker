using CycliqueShareTracker.Application.Models;

namespace CycliqueShareTracker.Application.Algorithms;

public sealed class DrawdownReboundAlgorithm : SignalAlgorithmBase
{
    public override AlgorithmType AlgorithmType => AlgorithmType.DrawdownRebound;
    public override string DisplayName => "Drawdown Rebound";

    public override AlgorithmResult ComputeSignals(IReadOnlyList<PriceBar> bars, AlgorithmContext context)
    {
        var points = new List<AlgorithmSignalPoint>(context.Indicators.Count);
        ComputedIndicator? previous = null;

        foreach (var current in context.Indicators)
        {
            var deepDrawdown = current.Drawdown52WeeksPercent.HasValue && current.Drawdown52WeeksPercent <= -20m;
            var rsiImproving = current.Rsi14.HasValue && previous?.Rsi14.HasValue == true && current.Rsi14 > previous.Rsi14;
            var macdImproving = current.MacdHistogram.HasValue && previous?.MacdHistogram.HasValue == true && current.MacdHistogram > previous.MacdHistogram;

            var buyZone = deepDrawdown && (rsiImproving || macdImproving);

            var rsiElevated = current.Rsi14.HasValue && current.Rsi14 >= 65m;
            var macdTurningDown = current.MacdLine.HasValue && current.MacdSignalLine.HasValue && previous?.MacdLine.HasValue == true && previous.MacdSignalLine.HasValue &&
                                  previous.MacdLine >= previous.MacdSignalLine && current.MacdLine < current.MacdSignalLine;
            var sellZone = rsiElevated && macdTurningDown;

            var buyDetails = new List<ScoreFactorDetail>
            {
                new("Drawdown 52w suffisamment négatif", 50, deepDrawdown, "Recherche d'un point de rebond après forte baisse."),
                new("RSI en amélioration", 25, rsiImproving, "Momentum RSI en reprise."),
                new("MACD en amélioration", 25, macdImproving, "Histogramme MACD remonte.")
            };
            var sellDetails = new List<ScoreFactorDetail>
            {
                new("RSI élevé", 40, rsiElevated, "Le rebond devient étiré."),
                new("Retournement MACD", 60, macdTurningDown, "Signal de fin de rebond.")
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
                buyZone ? "Rebond après drawdown avec momentum en amélioration." : "Pas de rebond confirmé.",
                sellZone ? "RSI élevé + MACD baissier : prise de profit." : "Pas de signal de sortie.",
                buyDetails,
                sellDetails));

            previous = current;
        }

        return new AlgorithmResult(AlgorithmType, DisplayName, points);
    }
}
