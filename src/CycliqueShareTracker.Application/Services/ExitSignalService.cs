using CycliqueShareTracker.Application.Interfaces;
using CycliqueShareTracker.Application.Models;
using CycliqueShareTracker.Domain.Enums;
using Microsoft.Extensions.Options;

namespace CycliqueShareTracker.Application.Services;

public sealed class ExitSignalService : IExitSignalService
{
    private const int TrimThreshold = 45;
    private readonly SignalStrategyOptions _strategyOptions;

    public ExitSignalService(IOptions<SignalStrategyOptions> strategyOptions)
    {
        _strategyOptions = strategyOptions.Value;
    }

    public ExitSignalResult BuildExitSignal(ComputedIndicator current, ComputedIndicator? previous, bool includeMacdInScoring = true)
    {
        decimal? distanceToSma50Percent = null;
        if (current.Sma50.HasValue && current.Sma50.Value != 0)
        {
            distanceToSma50Percent = ((current.Close / current.Sma50.Value) - 1m) * 100m;
        }

        var isBelowSma50 = current.Sma50.HasValue && current.Close < current.Sma50.Value;
        var isBelowSma200 = current.Sma200.HasValue && current.Close < current.Sma200.Value;
        var isCloseWeakening = current.PreviousClose.HasValue && current.Close < current.PreviousClose.Value;
        var isRsiDeclining = previous?.Rsi14.HasValue == true && current.Rsi14.HasValue && current.Rsi14.Value < previous.Rsi14.Value;

        var factors = new List<ScoreFactorDetail>
        {
            new("Cassure sous SMA50", 35, isBelowSma50, "Rupture de la tendance intermédiaire."),
            new("Cassure sous SMA200", 25, isBelowSma200, "Risque de retournement de fond."),
            new("Affaiblissement journalier (clôture sous la veille)", 15, isCloseWeakening, "Momentum court terme en dégradation."),
            new("RSI14 au-dessus de 65", 15, current.Rsi14 is >= 65m, "Contexte de prise de profit."),
            new("RSI14 en baisse vs veille", 10, isRsiDeclining, "Perte progressive de momentum.")
        };

        if (distanceToSma50Percent.HasValue)
        {
            factors.Add(new(
                "Extension extrême au-dessus de la SMA50 (> 10%)",
                10,
                distanceToSma50Percent.Value > 10m,
                "Excès haussier potentiellement en fin de cycle."));
        }

        var score = Math.Clamp(factors.Where(x => x.Triggered).Sum(x => x.Points), 0, 100);

        var trendWeakeningFilterPassed = isBelowSma50 || (isCloseWeakening && (isRsiDeclining || isBelowSma200));
        var rsiFilterPassed = current.Rsi14.HasValue && current.Rsi14.Value >= _strategyOptions.MinRsiForSell;
        var applyMacdConfirmation = includeMacdInScoring && _strategyOptions.EnableMacdConfirmation;
        var macdFilterPassed = !applyMacdConfirmation
            || (current.MacdLine.HasValue && current.MacdSignalLine.HasValue && current.MacdLine.Value < current.MacdSignalLine.Value);

        var isSellValidated = score >= _strategyOptions.SellScoreThreshold
            && trendWeakeningFilterPassed
            && rsiFilterPassed
            && macdFilterPassed;

        var label = isSellValidated
            ? ExitSignalLabel.SellZone
            : score >= TrimThreshold
                ? ExitSignalLabel.TrimTakeProfit
                : ExitSignalLabel.Hold;

        var validationReasons = new List<string>();
        if (trendWeakeningFilterPassed)
        {
            validationReasons.Add("affaiblissement de tendance validé");
        }

        if (rsiFilterPassed)
        {
            validationReasons.Add($"RSI >= {_strategyOptions.MinRsiForSell}");
        }

        if (applyMacdConfirmation && macdFilterPassed)
        {
            validationReasons.Add("confirmation MACD baissière");
        }

        if (score >= _strategyOptions.SellScoreThreshold)
        {
            validationReasons.Add($"score >= {_strategyOptions.SellScoreThreshold}");
        }

        var blockedReasons = new List<string>();
        if (!trendWeakeningFilterPassed)
        {
            blockedReasons.Add("pas de cassure SMA50 ni affaiblissement clair");
        }

        if (!rsiFilterPassed)
        {
            blockedReasons.Add($"RSI trop bas pour un SELL (< {_strategyOptions.MinRsiForSell})");
        }

        if (score < _strategyOptions.SellScoreThreshold)
        {
            blockedReasons.Add($"score insuffisant (< {_strategyOptions.SellScoreThreshold})");
        }

        if (applyMacdConfirmation && !macdFilterPassed)
        {
            blockedReasons.Add("confirmation MACD baissière absente");
        }

        var primaryReason = isSellValidated
            ? $"SELL validé : {string.Join(" + ", validationReasons)}."
            : blockedReasons.Count > 0
                ? $"SELL non validé : {string.Join(" + ", blockedReasons)}."
                : "Aucun signal de sortie fort détecté.";

        return new ExitSignalResult(score, label, primaryReason, factors);
    }
}
