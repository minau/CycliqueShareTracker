using CycliqueShareTracker.Application.Models;

namespace CycliqueShareTracker.Application.Algorithms;

public sealed class RsiMeanReversionAlgorithm : SignalAlgorithmBase
{
    public override AlgorithmType AlgorithmType => AlgorithmType.RsiMeanReversion;
    public override string DisplayName => "RSI Mean Reversion";

    public override AlgorithmResult ComputeSignals(IReadOnlyList<PriceBar> bars, AlgorithmContext context)
    {
        var points = new List<AlgorithmSignalPoint>(context.Indicators.Count);

        foreach (var indicator in context.Indicators)
        {
            var buyZone = indicator.Rsi14.HasValue && indicator.Rsi14.Value < 35m;
            var sellZone = indicator.Rsi14.HasValue && indicator.Rsi14.Value > 65m;
            var buyScore = ScoreFromRsi(indicator.Rsi14, 35m, 65m, buyDirection: true);
            var sellScore = ScoreFromRsi(indicator.Rsi14, 35m, 65m, buyDirection: false);

            var buyDetails = new List<ScoreFactorDetail>
            {
                new("RSI14 < 35", 70, buyZone, "Signal de survente pour un retour à la moyenne."),
                new("Intensité RSI", buyScore, buyScore > 0, "Score progressif selon la profondeur de la survente.")
            };
            var sellDetails = new List<ScoreFactorDetail>
            {
                new("RSI14 > 65", 70, sellZone, "Signal de surachat pour alléger."),
                new("Intensité RSI", sellScore, sellScore > 0, "Score progressif selon la force du surachat.")
            };

            points.Add(new AlgorithmSignalPoint(
                indicator.Date,
                buyZone,
                sellZone,
                buyZone,
                sellZone,
                buyScore,
                sellScore,
                decimal.Round((buyScore + sellScore) / 200m, 2),
                buyZone ? "RSI en survente (<35)." : "Pas de signal RSI d'achat.",
                sellZone ? "RSI en surachat (>65)." : "Pas de signal RSI de vente.",
                buyDetails,
                sellDetails));
        }

        return new AlgorithmResult(AlgorithmType, DisplayName, points);
    }
}
