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
        var distanceAboveSma50Pct = CalculateDistancePercent(current.Close, current.Sma50);
        var smaGapPct = CalculateGapPercent(current.Sma50, current.Sma200);

        var isPriceAboveSma200 = current.Sma200.HasValue && current.Close > current.Sma200.Value;
        var isSma50AboveSma200 = current.Sma50.HasValue && current.Sma200.HasValue && current.Sma50.Value > current.Sma200.Value;
        var hasPositiveSma50Slope = sma50SlopePct.HasValue && sma50SlopePct.Value >= _strategyOptions.MinSma50SlopeForBuy;
        var isRsiInBuyRange = current.Rsi14.HasValue
            && current.Rsi14.Value >= _strategyOptions.MinRsiForBuy
            && current.Rsi14.Value <= _strategyOptions.MaxRsiForBuy;
        var isDistanceToSma50Acceptable = distanceAboveSma50Pct.HasValue
            && distanceAboveSma50Pct.Value <= _strategyOptions.MaxDistanceAboveSma50ForBuyPct;
        var hasTrendGap = smaGapPct.HasValue && smaGapPct.Value >= _strategyOptions.MinGapBetweenSma50AndSma200Pct;
        var isNotFlat = sma50SlopePct.HasValue && Math.Abs(sma50SlopePct.Value) >= _strategyOptions.MaxFlatSlopeThreshold;
        var isBullishStreakAcceptable = current.BullishStreakCount <= _strategyOptions.MaxBullishStreakForBuy;
        var hasPullbackProfile = current.PreviousClose.HasValue && current.Close <= current.PreviousClose.Value * 1.01m;

        var applyMacdConfirmation = includeMacdInScoring && _strategyOptions.EnableMacdConfirmation;
        var isMacdBullish = current.MacdLine.HasValue
            && current.MacdSignalLine.HasValue
            && current.MacdLine.Value > current.MacdSignalLine.Value;

        var scoreFactors = new List<ScoreFactorDetail>
        {
            new("Prix au-dessus de SMA200", 22, isPriceAboveSma200, "Tendance primaire haussière."),
            new("SMA50 au-dessus de SMA200", 18, isSma50AboveSma200, "Structure haussière propre."),
            new("Pente SMA50 positive", 15, hasPositiveSma50Slope, "Momentum de tendance valide."),
            new($"RSI14 dans [{_strategyOptions.MinRsiForBuy}; {_strategyOptions.MaxRsiForBuy}]", 15, isRsiInBuyRange, "RSI modéré pour un BUY."),
            new($"Distance au-dessus SMA50 <= {_strategyOptions.MaxDistanceAboveSma50ForBuyPct}%", 12, isDistanceToSma50Acceptable, "Évite les BUY trop hauts."),
            new("Profil pullback/rebond (pas d'accélération verticale)", 10, hasPullbackProfile, "Favorise les entrées sur repli exploitable."),
            new($"Bougies haussières consécutives <= {_strategyOptions.MaxBullishStreakForBuy}", 8, isBullishStreakAcceptable, "Évite d'acheter après extension."),
            new($"Écart SMA50/SMA200 >= {_strategyOptions.MinGapBetweenSma50AndSma200Pct}%", 10, hasTrendGap, "Filtre anti-range.")
        };

        var blockingFilters = new List<ScoreFactorDetail>
        {
            new("Filtre bloquant: Close > SMA200", 0, isPriceAboveSma200, "Contexte haussier minimal obligatoire."),
            new("Filtre bloquant: SMA50 > SMA200", 0, isSma50AboveSma200, "Contexte haussier minimal obligatoire."),
            new($"Filtre bloquant: pente SMA50 >= {_strategyOptions.MinSma50SlopeForBuy}%", 0, hasPositiveSma50Slope, "Évite les BUY en tendance molle."),
            new($"Filtre bloquant: RSI <= {_strategyOptions.MaxRsiForBuy}", 0, current.Rsi14.HasValue && current.Rsi14.Value <= _strategyOptions.MaxRsiForBuy, "Évite les BUY trop tardifs."),
            new($"Filtre bloquant: distance au-dessus SMA50 <= {_strategyOptions.MaxDistanceAboveSma50ForBuyPct}%", 0, isDistanceToSma50Acceptable, "Évite les BUY étendus."),
            new("Filtre bloquant: SMA50 non plate", 0, isNotFlat, "Réduit le bruit en range."),
            new($"Filtre bloquant: écart SMA50/SMA200 >= {_strategyOptions.MinGapBetweenSma50AndSma200Pct}%", 0, hasTrendGap, "Évite les contextes indécis.")
        };

        if (applyMacdConfirmation)
        {
            blockingFilters.Add(new(
                "Filtre bloquant: confirmation MACD haussière",
                0,
                isMacdBullish,
                "MACD agit uniquement comme confirmation optionnelle."));
        }

        var score = Math.Clamp(scoreFactors.Where(x => x.Triggered).Sum(x => x.Points), 0, 100);
        var allBlockingFiltersPassed = blockingFilters.All(x => x.Triggered);
        var isBuyValidated = score >= _strategyOptions.BuyScoreThreshold && allBlockingFiltersPassed;

        var label = isBuyValidated
            ? SignalLabel.BuyZone
            : score >= WatchThreshold
                ? SignalLabel.Watch
                : SignalLabel.NoBuy;

        var triggeredScoreReasons = scoreFactors.Where(f => f.Triggered).Select(f => f.Label);
        var blockedReasons = blockingFilters.Where(f => !f.Triggered).Select(f => f.Label).ToList();
        var explanation = string.Join("; ", triggeredScoreReasons.Concat(blockedReasons.Select(x => $"Non validé: {x}")));
        if (string.IsNullOrWhiteSpace(explanation))
        {
            explanation = "Aucun critère BUY validé.";
        }

        var primaryReason = isBuyValidated
            ? "BUY validé : tendance haussière + RSI modéré + proximité SMA50 + score suffisant."
            : blockedReasons.Count > 0
                ? $"BUY non validé : {string.Join(" + ", blockedReasons)}."
                : $"BUY non validé : score insuffisant (< {_strategyOptions.BuyScoreThreshold}).";

        return new SignalResult(score, label, explanation, primaryReason, scoreFactors.Concat(blockingFilters).ToList());
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
