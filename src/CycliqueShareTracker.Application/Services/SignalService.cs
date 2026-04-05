using CycliqueShareTracker.Application.Interfaces;
using CycliqueShareTracker.Application.Models;
using CycliqueShareTracker.Domain.Enums;

namespace CycliqueShareTracker.Application.Services;

public sealed class SignalService : ISignalService
{
    public SignalResult BuildSignal(
        ComputedIndicator indicator,
        bool includeMacdInScoring = true,
        ComputedIndicator? previous = null,
        StrategyConfig? strategyConfig = null)
    {
        var config = strategyConfig ?? StrategyConfig.Default;
        var includeMacd = includeMacdInScoring && config.EnableMacdConfirmation;

        decimal? distanceAboveSma50Pct = null;
        if (indicator.Sma50.HasValue && indicator.Sma50.Value != 0)
        {
            distanceAboveSma50Pct = ((indicator.Close / indicator.Sma50.Value) - 1m) * 100m;
        }

        decimal? sma50SlopePct = null;
        if (previous?.Sma50.HasValue == true && indicator.Sma50.HasValue && previous.Sma50.Value != 0)
        {
            sma50SlopePct = ((indicator.Sma50.Value / previous.Sma50.Value) - 1m) * 100m;
        }

        decimal? smaGapPct = null;
        if (indicator.Sma50.HasValue && indicator.Sma200.HasValue && indicator.Sma200.Value != 0)
        {
            smaGapPct = ((indicator.Sma50.Value / indicator.Sma200.Value) - 1m) * 100m;
        }

        var factors = new List<ScoreFactorDetail>
        {
            new(
                "Prix au-dessus de la SMA200",
                30,
                indicator.Sma200.HasValue && indicator.Close > indicator.Sma200.Value,
                "Confirme une tendance de fond positive."),
            new(
                "SMA50 au-dessus de la SMA200",
                20,
                indicator.Sma50.HasValue && indicator.Sma200.HasValue && indicator.Sma50.Value > indicator.Sma200.Value,
                "Structure haussière court terme > long terme."),
            new(
                $"RSI14 entre {config.MinRsiForBuy} et {config.MaxRsiForBuy}",
                20,
                indicator.Rsi14.HasValue && indicator.Rsi14.Value >= config.MinRsiForBuy && indicator.Rsi14.Value <= config.MaxRsiForBuy,
                "Momentum équilibré, sans excès."),
            new(
                "Drawdown 52 semaines entre -15% et -5%",
                20,
                indicator.Drawdown52WeeksPercent.HasValue &&
                indicator.Drawdown52WeeksPercent.Value >= -15m &&
                indicator.Drawdown52WeeksPercent.Value <= -5m,
                "Zone de rechargement recherchée."),
            new(
                "Prix en hausse par rapport à la veille",
                10,
                indicator.PreviousClose.HasValue && indicator.Close > indicator.PreviousClose.Value,
                "Validation de momentum court terme."),
            new(
                $"Prix proche de la SMA50 (<= {config.MaxDistanceAboveSma50ForBuyPct}%)",
                0,
                distanceAboveSma50Pct.HasValue && distanceAboveSma50Pct.Value <= config.MaxDistanceAboveSma50ForBuyPct,
                "Évite les achats trop éloignés de la moyenne intermédiaire."),
            new(
                $"Pente SMA50 >= {config.MinSma50SlopeForBuy}%",
                0,
                sma50SlopePct.HasValue && sma50SlopePct.Value >= config.MinSma50SlopeForBuy,
                "Confirme une orientation haussière de la tendance intermédiaire."),
            new(
                $"Écart SMA50/SMA200 >= {config.MinGapBetweenSma50AndSma200Pct}%",
                0,
                smaGapPct.HasValue && smaGapPct.Value >= config.MinGapBetweenSma50AndSma200Pct,
                "Réduit les signaux en zone de range plate.")
        };

        if (includeMacd)
        {
            factors.Add(new(
                "MACD haussier : la ligne MACD est au-dessus de la ligne signal",
                8,
                indicator.MacdLine.HasValue &&
                indicator.MacdSignalLine.HasValue &&
                indicator.MacdLine.Value > indicator.MacdSignalLine.Value,
                "Confirmation de momentum haussier."));
            factors.Add(new(
                "Signal MACD baissier : la ligne MACD est passée sous la ligne signal",
                -8,
                indicator.MacdLine.HasValue &&
                indicator.MacdSignalLine.HasValue &&
                indicator.MacdLine.Value < indicator.MacdSignalLine.Value,
                "Momentum orienté à la baisse."));
            factors.Add(new(
                "Momentum en amélioration : l'histogramme MACD augmente",
                6,
                indicator.MacdHistogram.HasValue &&
                indicator.PreviousMacdHistogram.HasValue &&
                indicator.MacdHistogram.Value > indicator.PreviousMacdHistogram.Value,
                "Accélération positive du momentum."));
            factors.Add(new(
                "Momentum s'essouffle : l'histogramme MACD ralentit",
                -6,
                indicator.MacdHistogram.HasValue &&
                indicator.PreviousMacdHistogram.HasValue &&
                indicator.MacdHistogram.Value < indicator.PreviousMacdHistogram.Value,
                "Perte de vitesse du momentum."));
        }

        var score = factors.Where(x => x.Triggered).Sum(x => x.Points);
        var reasons = factors.Where(x => x.Triggered).Select(x => x.Label).ToList();

        score = Math.Clamp(score, 0, 100);
        var label = score switch
        {
            <= 39 => SignalLabel.NoBuy,
            <= 69 => SignalLabel.Watch,
            _ => SignalLabel.BuyZone
        };

        var explanation = reasons.Count == 0
            ? "Aucun critère haussier validé."
            : string.Join("; ", reasons);

        var primaryReason = label switch
        {
            SignalLabel.BuyZone when factors[0].Triggered && factors[1].Triggered =>
                "Tendance haussière de fond avec repli modéré.",
            SignalLabel.BuyZone =>
                "Signal d'entrée valide dans une zone de rechargement.",
            SignalLabel.Watch =>
                "Configuration à surveiller avec validation partielle des critères d'entrée.",
            _ => "Conditions d'entrée insuffisantes pour le moment."
        };

        return new SignalResult(score, label, explanation, primaryReason, factors);
    }
}
