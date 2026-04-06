using CycliqueShareTracker.Application.Models;

namespace CycliqueShareTracker.Application.Algorithms;

public sealed class MacdReversalAlgorithm : SignalAlgorithmBase
{
    public override AlgorithmType AlgorithmType => AlgorithmType.MacdReversal;
    public override string DisplayName => "MACD Reversal";

    public override AlgorithmResult ComputeSignals(IReadOnlyList<PriceBar> bars, AlgorithmContext context)
    {
        var points = new List<AlgorithmSignalPoint>(context.Indicators.Count);
        ComputedIndicator? previous = null;

        foreach (var current in context.Indicators)
        {
            var bullishState = current.MacdLine.HasValue && current.MacdSignalLine.HasValue && current.MacdHistogram.HasValue &&
                               current.MacdLine.Value > current.MacdSignalLine.Value && current.MacdHistogram.Value > 0;
            var bearishState = current.MacdLine.HasValue && current.MacdSignalLine.HasValue && current.MacdHistogram.HasValue &&
                               current.MacdLine.Value < current.MacdSignalLine.Value && current.MacdHistogram.Value < 0;
            var bullishCross = previous?.MacdLine.HasValue == true && previous.MacdSignalLine.HasValue &&
                               current.MacdLine.HasValue && current.MacdSignalLine.HasValue &&
                               previous.MacdLine.Value <= previous.MacdSignalLine.Value && current.MacdLine.Value > current.MacdSignalLine.Value;
            var bearishCross = previous?.MacdLine.HasValue == true && previous.MacdSignalLine.HasValue &&
                               current.MacdLine.HasValue && current.MacdSignalLine.HasValue &&
                               previous.MacdLine.Value >= previous.MacdSignalLine.Value && current.MacdLine.Value < current.MacdSignalLine.Value;

            var buyDetails = new List<ScoreFactorDetail>
            {
                new("MACD > signal", 40, current.MacdLine.HasValue && current.MacdSignalLine.HasValue && current.MacdLine > current.MacdSignalLine, "Momentum haussier."),
                new("Histogram > 0", 30, current.MacdHistogram.HasValue && current.MacdHistogram > 0, "Confirmation positive."),
                new("Croisement haussier", 30, bullishCross, "Détection du retournement haussier.")
            };
            var sellDetails = new List<ScoreFactorDetail>
            {
                new("MACD < signal", 40, current.MacdLine.HasValue && current.MacdSignalLine.HasValue && current.MacdLine < current.MacdSignalLine, "Momentum baissier."),
                new("Histogram < 0", 30, current.MacdHistogram.HasValue && current.MacdHistogram < 0, "Confirmation négative."),
                new("Croisement baissier", 30, bearishCross, "Détection du retournement baissier.")
            };

            points.Add(new AlgorithmSignalPoint(
                current.Date,
                bullishState,
                bearishState,
                bullishCross,
                bearishCross,
                CountTriggeredScore(buyDetails),
                CountTriggeredScore(sellDetails),
                null,
                bullishCross ? "MACD croise au-dessus de la ligne signal." : "Pas de croisement MACD haussier.",
                bearishCross ? "MACD croise sous la ligne signal." : "Pas de croisement MACD baissier.",
                buyDetails,
                sellDetails));

            previous = current;
        }

        return new AlgorithmResult(AlgorithmType, DisplayName, points);
    }
}
