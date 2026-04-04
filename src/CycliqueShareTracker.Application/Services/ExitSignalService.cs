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
        var sma50SlopePct = CalculateSlopePercent(current.Sma50, previous?.Sma50);
        var smaGapPct = CalculateGapPercent(current.Sma50, current.Sma200);

        var isBelowSma50 = current.Sma50.HasValue && current.Close < current.Sma50.Value;
        var isBelowSma200 = current.Sma200.HasValue && current.Close < current.Sma200.Value;
        var isSma50Weakening = sma50SlopePct.HasValue && sma50SlopePct.Value <= _strategyOptions.MaxFlatSlopeThreshold;
        var isSma50Negative = sma50SlopePct.HasValue && sma50SlopePct.Value < 0m;
        var isRsiWeak = current.Rsi14.HasValue && current.Rsi14.Value <= _strategyOptions.MinRsiWeaknessForSell;
        var isMomentumWeak = current.PreviousClose.HasValue && current.Close < current.PreviousClose.Value;
        var isTwoBearishBars = current.BearishStreakCount >= 2;
        var isGapNarrowing = smaGapPct.HasValue && smaGapPct.Value < _strategyOptions.MinGapBetweenSma50AndSma200Pct;

        var applyMacdConfirmation = includeMacdInScoring && _strategyOptions.EnableMacdConfirmation;
        var isMacdBearish = current.MacdLine.HasValue
            && current.MacdSignalLine.HasValue
            && current.MacdLine.Value < current.MacdSignalLine.Value;

        var scoreFactors = new List<ScoreFactorDetail>
        {
            new("Cassure sous SMA50", 25, isBelowSma50, "Signal de faiblesse technique précoce."),
            new("Pente SMA50 en ralentissement", 15, isSma50Weakening, "Tendance intermédiaire qui fatigue."),
            new("Pente SMA50 négative", 15, isSma50Negative, "Dégradation de tendance."),
            new($"RSI14 <= {_strategyOptions.MinRsiWeaknessForSell}", 18, isRsiWeak, "Fragilisation du momentum."),
            new("Perte de momentum (clôture sous la veille)", 12, isMomentumWeak, "Premier signal de distribution."),
            new("2 bougies défavorables consécutives", 10, isTwoBearishBars, "Faiblesse qui s'installe."),
            new("Prix sous SMA200", 20, isBelowSma200, "Confirmation de dégradation avancée."),
            new("Écart SMA50/SMA200 se resserre", 8, isGapNarrowing, "Sortie de tendance / entrée en zone risquée.")
        };

        var weaknessCount = new[] { isBelowSma50, isSma50Weakening, isRsiWeak, isMomentumWeak, isTwoBearishBars }.Count(x => x);
        var earlyWeaknessScore = scoreFactors.Where(f => f.Triggered).Sum(f => f.Points);

        var blockingFilters = new List<ScoreFactorDetail>
        {
            new("Filtre bloquant: contexte de faiblesse multi-signaux", 0, weaknessCount >= 2 || isBelowSma200, "Le SELL ne dépend pas d'un seul indicateur."),
            new("Filtre bloquant: faiblesse structurelle (sous SMA50 ou pente négative)", 0, isBelowSma50 || isSma50Negative || isBelowSma200, "Évite les faux SELL sur bruit."),
            new("Filtre bloquant: momentum en perte de force", 0, isMomentumWeak || isRsiWeak || isTwoBearishBars, "Détection précoce d'essoufflement.")
        };

        if (applyMacdConfirmation)
        {
            blockingFilters.Add(new(
                "Filtre bloquant: confirmation MACD baissière",
                0,
                isMacdBearish,
                "MACD confirmé uniquement quand activé."));
        }

        var confirmedSellScore = Math.Clamp(earlyWeaknessScore, 0, 100);
        var allBlockingFiltersPassed = blockingFilters.All(x => x.Triggered);
        var isConfirmedSell = confirmedSellScore >= _strategyOptions.SellScoreThreshold && allBlockingFiltersPassed;
        var isEarlySell = _strategyOptions.EarlySellEnabled
            && earlyWeaknessScore >= _strategyOptions.EarlySellWeaknessScoreThreshold
            && allBlockingFiltersPassed;

        var label = (isConfirmedSell || isEarlySell)
            ? ExitSignalLabel.SellZone
            : confirmedSellScore >= TrimThreshold
                ? ExitSignalLabel.TrimTakeProfit
                : ExitSignalLabel.Hold;

        var triggeredReasons = scoreFactors.Where(f => f.Triggered).Select(f => f.Label);
        var blockedReasons = blockingFilters.Where(f => !f.Triggered).Select(f => f.Label).ToList();
        var primaryReason = label == ExitSignalLabel.SellZone && isEarlySell && !isConfirmedSell
            ? "SELL précoce validé : essoufflement détecté avant cassure avancée."
            : label == ExitSignalLabel.SellZone
                ? "SELL confirmé : faiblesse technique et momentum baissier alignés."
                : blockedReasons.Count > 0
                    ? $"SELL non validé : {string.Join(" + ", blockedReasons)}."
                    : $"SELL non validé : score insuffisant (< {_strategyOptions.SellScoreThreshold}).";

        var factors = scoreFactors.Concat(blockingFilters).ToList();
        var explanation = string.Join("; ", triggeredReasons.Concat(blockedReasons.Select(x => $"Non validé: {x}")));
        if (!string.IsNullOrWhiteSpace(explanation))
        {
            factors.Add(new ScoreFactorDetail("Résumé", 0, true, explanation));
        }

        return new ExitSignalResult(confirmedSellScore, label, primaryReason, factors);
    }

    private static decimal? CalculateSlopePercent(decimal? currentValue, decimal? previousValue)
    {
        if (!currentValue.HasValue || !previousValue.HasValue || previousValue.Value == 0)
        {
            return null;
        }

        return ((currentValue.Value / previousValue.Value) - 1m) * 100m;
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
