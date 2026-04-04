using CycliqueShareTracker.Application.Interfaces;
using CycliqueShareTracker.Application.Models;
using CycliqueShareTracker.Domain.Enums;
using Microsoft.Extensions.Options;

namespace CycliqueShareTracker.Application.Services;

public sealed class SignalService : ISignalService
{
    private const int WatchThreshold = 45;
    private readonly SignalStrategyOptions _strategyOptions;

    public SignalService(IOptions<SignalStrategyOptions> strategyOptions)
    {
        _strategyOptions = strategyOptions.Value;
    }

    public SignalResult BuildSignal(ComputedIndicator current, ComputedIndicator? previous, bool includeMacdInScoring = true)
    {
        var sma50SlopePct = CalculateSlopePercent(current.Sma50, previous?.Sma50);
        var distanceToSma50Pct = CalculateDistancePercent(current.Close, current.Sma50);
        var smaGapPct = CalculateGapPercent(current.Sma50, current.Sma200);

        var isPriceAboveSma200 = current.Sma200.HasValue && current.Close > current.Sma200.Value;
        var isSma50AboveSma200 = current.Sma50.HasValue && current.Sma200.HasValue && current.Sma50.Value > current.Sma200.Value;
        var hasPositiveSma50Slope = sma50SlopePct.HasValue && sma50SlopePct.Value >= _strategyOptions.MinSma50SlopeForBuy;
        var isRsiInBuyRange = current.Rsi14.HasValue
            && current.Rsi14.Value >= _strategyOptions.MinRsiForBuy
            && current.Rsi14.Value <= _strategyOptions.MaxRsiForBuy;
        var isDistanceToSma50Acceptable = distanceToSma50Pct.HasValue
            && distanceToSma50Pct.Value <= _strategyOptions.MaxDistanceFromSma50ForBuyPct;
        var hasTrendGap = smaGapPct.HasValue && smaGapPct.Value >= _strategyOptions.MinGapBetweenSma50AndSma200Pct;
        var isNotFlat = sma50SlopePct.HasValue && Math.Abs(sma50SlopePct.Value) >= _strategyOptions.MaxFlatSlopeThreshold;

        var applyMacdConfirmation = includeMacdInScoring && _strategyOptions.EnableMacdConfirmation;
        var isMacdBullish = current.MacdLine.HasValue
            && current.MacdSignalLine.HasValue
            && current.MacdLine.Value > current.MacdSignalLine.Value;

        var factors = new List<ScoreFactorDetail>
        {
            new("Prix au-dessus de SMA200", 25, isPriceAboveSma200, "Filtre de tendance long terme validé."),
            new("SMA50 au-dessus de SMA200", 20, isSma50AboveSma200, "Structure haussière intermédiaire."),
            new("Pente SMA50 positive", 20, hasPositiveSma50Slope, "Momentum de tendance haussier."),
            new($"RSI14 dans [{_strategyOptions.MinRsiForBuy}; {_strategyOptions.MaxRsiForBuy}]", 15, isRsiInBuyRange, "Entrée sans surchauffe."),
            new($"Distance prix/SMA50 <= {_strategyOptions.MaxDistanceFromSma50ForBuyPct}%", 10, isDistanceToSma50Acceptable, "Évite les achats trop étendus."),
            new($"Écart SMA50/SMA200 >= {_strategyOptions.MinGapBetweenSma50AndSma200Pct}%", 10, hasTrendGap, "Évite les contextes de range."),
            new("Clôture au-dessus de la veille", 5, current.PreviousClose.HasValue && current.Close > current.PreviousClose.Value, "Confirmation court terme.")
        };

        var blockingFilters = new List<ScoreFactorDetail>
        {
            new("Filtre bloquant: tendance haussière (prix>SMA200 et SMA50>SMA200)", 0, isPriceAboveSma200 && isSma50AboveSma200, "Obligatoire pour BUY."),
            new($"Filtre bloquant: pente SMA50 >= {_strategyOptions.MinSma50SlopeForBuy}%", 0, hasPositiveSma50Slope, "Obligatoire pour BUY."),
            new("Filtre bloquant: marché non plat", 0, isNotFlat, "Bloque les signaux en zone trop indécise."),
            new($"Filtre bloquant: RSI14 entre {_strategyOptions.MinRsiForBuy} et {_strategyOptions.MaxRsiForBuy}", 0, isRsiInBuyRange, "Évite les achats trop tardifs."),
            new($"Filtre bloquant: distance prix/SMA50 <= {_strategyOptions.MaxDistanceFromSma50ForBuyPct}%", 0, isDistanceToSma50Acceptable, "Évite les achats trop loin de la SMA50."),
            new($"Filtre bloquant: écart SMA50/SMA200 >= {_strategyOptions.MinGapBetweenSma50AndSma200Pct}%", 0, hasTrendGap, "Anti-range.")
        };

        if (applyMacdConfirmation)
        {
            blockingFilters.Add(new(
                "Filtre bloquant: confirmation MACD haussière",
                0,
                isMacdBullish,
                "Quand activé, le MACD ne génère pas le signal, il confirme seulement."));
        }

        var score = Math.Clamp(factors.Where(x => x.Triggered).Sum(x => x.Points), 0, 100);
        var allBlockingFiltersPassed = blockingFilters.All(x => x.Triggered);
        var isBuyValidated = score >= _strategyOptions.BuyScoreThreshold && allBlockingFiltersPassed;

        var label = isBuyValidated
            ? SignalLabel.BuyZone
            : score >= WatchThreshold
                ? SignalLabel.Watch
                : SignalLabel.NoBuy;

        var reasons = factors.Where(f => f.Triggered).Select(f => f.Label).ToList();
        var blocked = blockingFilters.Where(f => !f.Triggered).Select(f => f.Label).ToList();
        var explanationParts = reasons.Concat(blocked.Select(x => $"Non validé: {x}")).ToList();
        var explanation = explanationParts.Count > 0
            ? string.Join("; ", explanationParts)
            : "Aucun critère d'entrée validé.";

        var primaryReason = isBuyValidated
            ? "BUY validé : tendance haussière + pente SMA50 positive + RSI correct + prix proche SMA50 + score suffisant."
            : blocked.Count > 0
                ? $"BUY non validé : {string.Join(" + ", blocked)}."
                : $"BUY non validé : score insuffisant (< {_strategyOptions.BuyScoreThreshold}).";

        var allFactors = factors.Concat(blockingFilters).ToList();
        return new SignalResult(score, label, explanation, primaryReason, allFactors);
    }

    private static decimal? CalculateSlopePercent(decimal? currentValue, decimal? previousValue)
    {
        if (!currentValue.HasValue || !previousValue.HasValue || previousValue.Value == 0)
        {
            return null;
        }

        return ((currentValue.Value / previousValue.Value) - 1m) * 100m;
    }

    private static decimal? CalculateDistancePercent(decimal close, decimal? movingAverage)
    {
        if (!movingAverage.HasValue || movingAverage.Value == 0)
        {
            return null;
        }

        return ((close / movingAverage.Value) - 1m) * 100m;
    }

    private static decimal? CalculateGapPercent(decimal? sma50, decimal? sma200)
    {
        if (!sma50.HasValue || !sma200.HasValue || sma200.Value == 0)
        {
            return null;
        }

        return ((sma50.Value / sma200.Value) - 1m) * 100m;
    }
}
