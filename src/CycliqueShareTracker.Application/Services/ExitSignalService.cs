using CycliqueShareTracker.Application.Interfaces;
using CycliqueShareTracker.Application.Models;
using CycliqueShareTracker.Domain.Enums;

namespace CycliqueShareTracker.Application.Services;

public sealed class ExitSignalService : IExitSignalService
{
    public ExitSignalResult BuildExitSignal(ComputedIndicator current, ComputedIndicator? previous, bool includeMacdInScoring = true)
    {
        decimal? distanceToSma50Percent = null;
        if (current.Sma50.HasValue && current.Sma50.Value != 0)
        {
            distanceToSma50Percent = ((current.Close / current.Sma50.Value) - 1m) * 100m;
        }

        var factors = new List<ScoreFactorDetail>
        {
            new(
                "RSI14 élevé (>= 75)",
                25,
                current.Rsi14 is >= 75m,
                "Risque de surchauffe court terme."),
            new(
                "Prix fortement au-dessus de la SMA50 (> 12%)",
                20,
                distanceToSma50Percent.HasValue && distanceToSma50Percent.Value >= 12m,
                "Extension de prix potentiellement excessive."),
            new(
                "Excès haussier en perte de vitesse",
                20,
                previous is not null &&
                current.PreviousClose.HasValue &&
                current.Close < current.PreviousClose.Value &&
                previous.Rsi14 is >= 70m &&
                current.Rsi14.HasValue &&
                current.Rsi14.Value < previous.Rsi14.Value,
                "RSI en baisse après phase de surachat."),
            new(
                "Dégradation du momentum (clôture sous la veille)",
                15,
                previous is not null && current.PreviousClose.HasValue && current.Close < current.PreviousClose.Value,
                "Premier signal d'essoufflement."),
            new(
                "Cassure sous SMA50",
                25,
                current.Sma50.HasValue && current.Close < current.Sma50.Value,
                "Alerte sur la tendance intermédiaire."),
            new(
                "Cassure sous SMA200",
                30,
                current.Sma200.HasValue && current.Close < current.Sma200.Value,
                "Rupture de tendance de fond.")
        };

        if (includeMacdInScoring)
        {
            decimal? currentMacdSpread = ComputeMacdSpread(current);
            decimal? previousMacdSpread = previous is null ? null : ComputeMacdSpread(previous);

            factors.Add(new(
                "Momentum en ralentissement : l'histogramme MACD diminue",
                8,
                current.MacdHistogram.HasValue &&
                previous?.MacdHistogram.HasValue == true &&
                current.MacdHistogram.Value < previous.MacdHistogram.Value,
                "Le momentum haussier perd en intensité."));
            factors.Add(new(
                "Le MACD se rapproche de sa ligne signal, signe d'essoufflement",
                5,
                currentMacdSpread.HasValue &&
                previousMacdSpread.HasValue &&
                Math.Abs(currentMacdSpread.Value) < Math.Abs(previousMacdSpread.Value) &&
                previousMacdSpread.Value > 0,
                "Convergence MACD/Signal en fin d'impulsion haussière."));
            factors.Add(new(
                "Signal MACD baissier : la ligne MACD est passée sous la ligne signal",
                12,
                currentMacdSpread.HasValue &&
                previousMacdSpread.HasValue &&
                previousMacdSpread.Value >= 0 &&
                currentMacdSpread.Value < 0,
                "Risque de retournement à la baisse."));
            factors.Add(new(
                "Confirmation baissière : l'histogramme MACD est désormais négatif",
                6,
                current.MacdHistogram.HasValue && current.MacdHistogram.Value < 0,
                "Momentum baissier désormais dominant."));
        }

        var score = factors.Where(x => x.Triggered).Sum(x => x.Points);

        score = Math.Clamp(score, 0, 100);

        var label = score switch
        {
            <= 34 => ExitSignalLabel.Hold,
            <= 64 => ExitSignalLabel.TrimTakeProfit,
            _ => ExitSignalLabel.SellZone
        };

        var mainReason = factors[5].Triggered
            ? "Cassure technique suggérant une sortie."
            : factors[4].Triggered
                ? "Dégradation de tendance après phase haussière."
                : (factors[0].Triggered && factors[1].Triggered)
                    ? "Surchauffe de court terme."
                    : label == ExitSignalLabel.TrimTakeProfit
                        ? "Zone de prise de profit."
                        : "Aucun signal de sortie fort détecté.";

        return new ExitSignalResult(score, label, mainReason, factors);
    }

    private static decimal? ComputeMacdSpread(ComputedIndicator indicator)
    {
        if (!indicator.MacdLine.HasValue || !indicator.MacdSignalLine.HasValue)
        {
            return null;
        }

        return indicator.MacdLine.Value - indicator.MacdSignalLine.Value;
    }
}
