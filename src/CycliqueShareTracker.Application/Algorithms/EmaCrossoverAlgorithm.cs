using CycliqueShareTracker.Application.Models;

namespace CycliqueShareTracker.Application.Algorithms;

public sealed class EmaCrossoverAlgorithm : SignalAlgorithmBase
{
    public override AlgorithmType AlgorithmType => AlgorithmType.EmaCrossover;
    public override string DisplayName => "EMA Crossover";

    public override AlgorithmResult ComputeSignals(IReadOnlyList<PriceBar> bars, AlgorithmContext context)
    {
        var points = new List<AlgorithmSignalPoint>(context.Indicators.Count);
        ComputedIndicator? previous = null;

        foreach (var current in context.Indicators)
        {
            var isBullState = current.Ema12.HasValue && current.Ema26.HasValue && current.Ema12.Value > current.Ema26.Value;
            var isBearState = current.Ema12.HasValue && current.Ema26.HasValue && current.Ema12.Value < current.Ema26.Value;
            var bullishCross = previous?.Ema12.HasValue == true && previous.Ema26.HasValue &&
                               current.Ema12.HasValue && current.Ema26.HasValue &&
                               previous.Ema12.Value <= previous.Ema26.Value && current.Ema12.Value > current.Ema26.Value;
            var bearishCross = previous?.Ema12.HasValue == true && previous.Ema26.HasValue &&
                               current.Ema12.HasValue && current.Ema26.HasValue &&
                               previous.Ema12.Value >= previous.Ema26.Value && current.Ema12.Value < current.Ema26.Value;

            var buyDetails = new List<ScoreFactorDetail>
            {
                new("EMA12 > EMA26", 60, isBullState, "Régime haussier court terme."),
                new("Croisement haussier", 40, bullishCross, "Détection du passage EMA12 au-dessus de EMA26.")
            };
            var sellDetails = new List<ScoreFactorDetail>
            {
                new("EMA12 < EMA26", 60, isBearState, "Régime baissier court terme."),
                new("Croisement baissier", 40, bearishCross, "Détection du passage EMA12 sous EMA26.")
            };

            points.Add(new AlgorithmSignalPoint(
                current.Date,
                isBullState,
                isBearState,
                bullishCross,
                bearishCross,
                CountTriggeredScore(buyDetails),
                CountTriggeredScore(sellDetails),
                null,
                bullishCross ? "Croisement haussier EMA12/EMA26." : "Pas de croisement haussier.",
                bearishCross ? "Croisement baissier EMA12/EMA26." : "Pas de croisement baissier.",
                buyDetails,
                sellDetails));

            previous = current;
        }

        return new AlgorithmResult(AlgorithmType, DisplayName, points);
    }
}
