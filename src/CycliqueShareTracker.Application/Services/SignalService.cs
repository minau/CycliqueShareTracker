using CycliqueShareTracker.Application.Interfaces;
using CycliqueShareTracker.Application.Models;
using CycliqueShareTracker.Domain.Enums;
using Microsoft.Extensions.Options;

namespace CycliqueShareTracker.Application.Services;

public sealed class SignalService : ISignalService
{
    private const int WatchThreshold = 40;
    private readonly SignalStrategyOptions _strategyOptions;

    public SignalService(IOptions<SignalStrategyOptions> strategyOptions)
    {
        _strategyOptions = strategyOptions.Value;
    }

    public SignalResult BuildSignal(ComputedIndicator indicator, bool includeMacdInScoring = true)
    {
        var isPriceAboveSma200 = indicator.Sma200.HasValue && indicator.Close > indicator.Sma200.Value;
        var isSma50AboveSma200 = indicator.Sma50.HasValue && indicator.Sma200.HasValue && indicator.Sma50.Value > indicator.Sma200.Value;
        var isRsiInBuyRange = indicator.Rsi14.HasValue && indicator.Rsi14.Value >= 35m && indicator.Rsi14.Value <= 55m;
        var isDrawdownInReloadZone = indicator.Drawdown52WeeksPercent.HasValue
            && indicator.Drawdown52WeeksPercent.Value >= -18m
            && indicator.Drawdown52WeeksPercent.Value <= -3m;
        var isPriceImproving = indicator.PreviousClose.HasValue && indicator.Close > indicator.PreviousClose.Value;

        var factors = new List<ScoreFactorDetail>
        {
            new("Prix au-dessus de la SMA200", 25, isPriceAboveSma200, "Filtre de tendance longue validé."),
            new("SMA50 au-dessus de la SMA200", 25, isSma50AboveSma200, "Structure haussière intermédiaire confirmée."),
            new("RSI14 dans la zone d'entrée (35-55)", 20, isRsiInBuyRange, "Entrée sur momentum raisonnable."),
            new("Drawdown 52 semaines entre -18% et -3%", 15, isDrawdownInReloadZone, "Repli exploitable sans détérioration extrême."),
            new("Clôture supérieure à la veille", 15, isPriceImproving, "Momentum court terme en reprise.")
        };

        var score = Math.Clamp(factors.Where(x => x.Triggered).Sum(x => x.Points), 0, 100);

        var trendFilterPassed = isPriceAboveSma200 && isSma50AboveSma200;
        var rsiFilterPassed = indicator.Rsi14.HasValue && indicator.Rsi14.Value <= _strategyOptions.MaxRsiForBuy;
        var applyMacdConfirmation = includeMacdInScoring && _strategyOptions.EnableMacdConfirmation;
        var macdFilterPassed = !applyMacdConfirmation
            || (indicator.MacdLine.HasValue && indicator.MacdSignalLine.HasValue && indicator.MacdLine.Value > indicator.MacdSignalLine.Value);

        var isBuyValidated = score >= _strategyOptions.BuyScoreThreshold
            && trendFilterPassed
            && rsiFilterPassed
            && macdFilterPassed;

        var label = isBuyValidated
            ? SignalLabel.BuyZone
            : score >= WatchThreshold
                ? SignalLabel.Watch
                : SignalLabel.NoBuy;

        var validationReasons = new List<string>();
        if (trendFilterPassed)
        {
            validationReasons.Add("tendance haussière validée");
        }

        if (rsiFilterPassed)
        {
            validationReasons.Add($"RSI <= {_strategyOptions.MaxRsiForBuy}");
        }

        if (applyMacdConfirmation && macdFilterPassed)
        {
            validationReasons.Add("confirmation MACD haussière");
        }

        if (score >= _strategyOptions.BuyScoreThreshold)
        {
            validationReasons.Add($"score >= {_strategyOptions.BuyScoreThreshold}");
        }

        var blockedReasons = new List<string>();
        if (!trendFilterPassed)
        {
            blockedReasons.Add("filtre de tendance invalide (prix/SMA)");
        }

        if (!rsiFilterPassed)
        {
            blockedReasons.Add($"RSI trop élevé pour un BUY (> {_strategyOptions.MaxRsiForBuy})");
        }

        if (score < _strategyOptions.BuyScoreThreshold)
        {
            blockedReasons.Add($"score insuffisant (< {_strategyOptions.BuyScoreThreshold})");
        }

        if (applyMacdConfirmation && !macdFilterPassed)
        {
            blockedReasons.Add("confirmation MACD absente");
        }

        var explanationParts = factors.Where(f => f.Triggered).Select(f => f.Label).ToList();
        explanationParts.AddRange(blockedReasons);
        var explanation = explanationParts.Count == 0
            ? "Aucun critère d'entrée validé."
            : string.Join("; ", explanationParts);

        var primaryReason = isBuyValidated
            ? $"BUY validé : {string.Join(" + ", validationReasons)}."
            : blockedReasons.Count > 0
                ? $"BUY non validé : {string.Join(" + ", blockedReasons)}."
                : "Conditions d'entrée insuffisantes pour le moment.";

        return new SignalResult(score, label, explanation, primaryReason, factors);
    }
}
