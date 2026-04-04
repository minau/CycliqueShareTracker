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
        var isSma50Weakening = sma50SlopePct.HasValue && sma50SlopePct.Value <= -_strategyOptions.MaxFlatSlopeThreshold;
        var isRsiBreakdown = current.Rsi14.HasValue && current.Rsi14.Value <= _strategyOptions.MinRsiBreakdownForSell;
        var isMomentumWeak = current.PreviousClose.HasValue && current.Close < current.PreviousClose.Value;
        var isTrendGapNarrow = smaGapPct.HasValue && smaGapPct.Value < _strategyOptions.MinGapBetweenSma50AndSma200Pct;

        var applyMacdConfirmation = includeMacdInScoring && _strategyOptions.EnableMacdConfirmation;
        var isMacdBearish = current.MacdLine.HasValue
            && current.MacdSignalLine.HasValue
            && current.MacdLine.Value < current.MacdSignalLine.Value;

        var factors = new List<ScoreFactorDetail>
        {
            new("Prix sous SMA50", 25, isBelowSma50, "Premier signe de dégradation."),
            new("Pente SMA50 négative", 20, isSma50Weakening, "Affaiblissement de tendance."),
            new($"RSI14 <= {_strategyOptions.MinRsiBreakdownForSell}", 20, isRsiBreakdown, "Momentum en rupture."),
            new("Clôture sous la veille", 15, isMomentumWeak, "Perte de force immédiate."),
            new("Prix sous SMA200", 15, isBelowSma200, "Contexte baissier de fond."),
            new("Écart SMA50/SMA200 se resserre", 10, isTrendGapNarrow, "Contexte de range/dégradation.")
        };

        var weaknessCount = new[] { isBelowSma50, isSma50Weakening, isRsiBreakdown, isMomentumWeak }.Count(x => x);
        var hasSellContext = weaknessCount >= 2 || isBelowSma200;

        var blockingFilters = new List<ScoreFactorDetail>
        {
            new("Filtre bloquant: contexte de faiblesse multi-signaux", 0, hasSellContext, "Le SELL ne dépend jamais d'un seul indicateur."),
            new("Filtre bloquant: pente SMA50 non plate", 0, sma50SlopePct.HasValue && Math.Abs(sma50SlopePct.Value) >= _strategyOptions.MaxFlatSlopeThreshold, "Réduit les faux signaux en range."),
            new("Filtre bloquant: dégradation de tendance (sous SMA50/SMA200 ou pente négative)", 0, isBelowSma50 || isBelowSma200 || isSma50Weakening, "Assure une faiblesse technique réelle.")
        };

        if (applyMacdConfirmation)
        {
            blockingFilters.Add(new(
                "Filtre bloquant: confirmation MACD baissière",
                0,
                isMacdBearish,
                "Quand activé, MACD agit comme filtre de confirmation."));
        }

        var score = Math.Clamp(factors.Where(x => x.Triggered).Sum(x => x.Points), 0, 100);
        var allBlockingFiltersPassed = blockingFilters.All(x => x.Triggered);
        var isSellValidated = score >= _strategyOptions.SellScoreThreshold && allBlockingFiltersPassed;

        var label = isSellValidated
            ? ExitSignalLabel.SellZone
            : score >= TrimThreshold
                ? ExitSignalLabel.TrimTakeProfit
                : ExitSignalLabel.Hold;

        var reasons = factors.Where(f => f.Triggered).Select(f => f.Label).ToList();
        var blocked = blockingFilters.Where(f => !f.Triggered).Select(f => f.Label).ToList();

        var primaryReason = isSellValidated
            ? "SELL validé : cassure/faiblesse confirmée + score suffisant."
            : blocked.Count > 0
                ? $"SELL non validé : {string.Join(" + ", blocked)}."
                : $"SELL non validé : score insuffisant (< {_strategyOptions.SellScoreThreshold}).";

        var allFactors = factors.Concat(blockingFilters).ToList();
        return new ExitSignalResult(score, label, primaryReason, allFactors);
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
