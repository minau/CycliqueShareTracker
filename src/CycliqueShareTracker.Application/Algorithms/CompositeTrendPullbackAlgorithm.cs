using CycliqueShareTracker.Application.Models;

namespace CycliqueShareTracker.Application.Algorithms;

public sealed class CompositeTrendPullbackAlgorithm : SignalAlgorithmBase
{
    public override AlgorithmType AlgorithmType => AlgorithmType.CompositeTrendPullback;
    public override string DisplayName => "Composite Trend Pullback";

    private const int BuyTrendWeight = 35;
    private const int BuyPullbackWeight = 30;
    private const int BuyMomentumWeight = 25;
    private const int BuyEntryFilterWeight = 10;

    private const int SellMomentumWeaknessWeight = 35;
    private const int SellTrendDeteriorationWeight = 30;
    private const int SellExtensionWeight = 25;
    private const int SellEarlyWeaknessWeight = 10;

    public override AlgorithmResult ComputeSignals(IReadOnlyList<PriceBar> bars, AlgorithmContext context)
    {
        var points = new List<AlgorithmSignalPoint>(context.Indicators.Count);
        var parameters = context.StrategyConfig?.MetaAlgoParameters ?? MetaAlgoParameters.Default;

        var previousIndicators = new Queue<ComputedIndicator>();
        var lastBuySignalDate = DateOnly.MinValue;
        var lastSellSignalDate = DateOnly.MinValue;

        foreach (var current in context.Indicators)
        {
            var previous = previousIndicators.LastOrDefault();
            var beforePrevious = previousIndicators.Count > 1 ? previousIndicators.ElementAt(previousIndicators.Count - 2) : null;

            var slope = ComputeSlopePct(previous?.Sma50, current.Sma50);
            var distanceToSma50Pct = ComputeDistanceAboveSma50Pct(current.Close, current.Sma50);
            var smaGapPct = ComputeSmaGapPct(current.Sma50, current.Sma200);
            var histogramDelta = ComputeHistogramDelta(current.MacdHistogram, previous?.MacdHistogram);

            var buyDetails = BuildBuyScoreDetails(current, previous, beforePrevious, parameters, slope, distanceToSma50Pct, smaGapPct, histogramDelta);
            var sellDetails = BuildSellScoreDetails(current, previous, parameters, slope, distanceToSma50Pct, histogramDelta);

            var buyScore = CountTriggeredScore(buyDetails);
            var sellScore = CountTriggeredScore(sellDetails);

            var buyZone = buyScore >= parameters.BuyScoreThreshold;
            var sellZone = sellScore >= parameters.SellScoreThreshold;

            var buyCooldownOk = IsCooldownCompleted(lastBuySignalDate, current.Date, parameters.MinimumBarsBetweenSameSignal);
            var sellCooldownOk = IsCooldownCompleted(lastSellSignalDate, current.Date, parameters.MinimumBarsBetweenSameSignal);

            var buySignal = buyZone && buyCooldownOk;

            var weaknessScore = sellDetails.Where(x => x.Triggered && x.Label.StartsWith("Faiblesse", StringComparison.OrdinalIgnoreCase)).Sum(x => x.Points);
            var earlySell = parameters.EarlySellEnabled &&
                            !sellZone &&
                            weaknessScore >= parameters.EarlySellWeaknessScoreThreshold;

            var sellSignal = (sellZone || earlySell) && sellCooldownOk;

            if (buySignal)
            {
                lastBuySignalDate = current.Date;
            }

            if (sellSignal)
            {
                lastSellSignalDate = current.Date;
            }

            var buyReason = buySignal
                ? "Contexte haussier valide, pullback exploitable et reprise du momentum sans sur-extension."
                : "Achat non validé: tendance/pullback/momentum incomplets ou entrée trop étendue.";

            var sellReason = sellSignal
                ? (earlySell
                    ? "Sortie anticipée: signes de faiblesse progressifs avant retournement complet."
                    : "Sortie validée: momentum en retournement et/ou sur-extension avec essoufflement.")
                : "Pas de signal de sortie: faiblesse insuffisante ou contexte encore neutre.";

            var signalType = buySignal && sellSignal
                ? "Conflict"
                : buySignal ? "Buy"
                : sellSignal ? "Sell"
                : "Neutral";

            var point = new AlgorithmSignalPoint(
                current.Date,
                buyZone,
                sellZone || earlySell,
                buySignal,
                sellSignal,
                buyScore,
                sellScore,
                decimal.Round(Math.Abs(buyScore - sellScore) / 100m, 2),
                buyReason,
                sellReason,
                buyDetails,
                sellDetails)
            {
                DebugValues = new Dictionary<string, object?>
                {
                    ["signalType"] = signalType,
                    ["sma50SlopePct"] = slope,
                    ["distanceAboveSma50Pct"] = distanceToSma50Pct,
                    ["smaGapPct"] = smaGapPct,
                    ["histogramDelta"] = histogramDelta,
                    ["buyThreshold"] = parameters.BuyScoreThreshold,
                    ["sellThreshold"] = parameters.SellScoreThreshold,
                    ["buyCooldownOk"] = buyCooldownOk,
                    ["sellCooldownOk"] = sellCooldownOk,
                    ["earlySellActive"] = earlySell,
                    ["earlySellWeaknessScore"] = weaknessScore
                }
            };

            points.Add(point);
            previousIndicators.Enqueue(current);
            if (previousIndicators.Count > 3)
            {
                previousIndicators.Dequeue();
            }
        }

        return new AlgorithmResult(AlgorithmType, DisplayName, points);
    }

    private static List<ScoreFactorDetail> BuildBuyScoreDetails(
        ComputedIndicator current,
        ComputedIndicator? previous,
        ComputedIndicator? beforePrevious,
        MetaAlgoParameters parameters,
        decimal? slope,
        decimal? distanceToSma50Pct,
        decimal? smaGapPct,
        decimal? histogramDelta)
    {
        var sma200Unavailable = !current.Sma200.HasValue;
        var trendBySma = current.Sma50.HasValue && current.Sma200.HasValue && current.Sma50.Value > current.Sma200.Value;
        var fallbackTrend = sma200Unavailable && current.Sma50.HasValue && current.Close > current.Sma50.Value;

        var details = new List<ScoreFactorDetail>
        {
            new("Régime de tendance: SMA50 > SMA200", 20, trendBySma, "Validation principale de tendance de fond."),
            new("Régime de tendance fallback (SMA200 absente)", 12, fallbackTrend, "Fallback prudent: prix au-dessus de SMA50 sans forcer un buy extrême."),
            new("Pente SMA50 positive", 8, slope.HasValue && slope.Value >= parameters.MinSma50SlopeForBuy, "Confirme une tendance intermédiaire ascendante."),
            new("EMA12 > EMA26", 7, current.Ema12.HasValue && current.Ema26.HasValue && current.Ema12.Value > current.Ema26.Value, "Momentum haussier court terme."),

            new("Pullback RSI 40-55", 14, current.Rsi14.HasValue && current.Rsi14.Value >= 40m && current.Rsi14.Value <= 55m, "Zone de repli saine dans une tendance haussière."),
            new("Pullback opportuniste RSI 35-40", 8, current.Rsi14.HasValue && current.Rsi14.Value >= 35m && current.Rsi14.Value < 40m, "Repli plus marqué mais exploitable."),
            new($"Prix proche SMA50 (<= {parameters.MaxDistanceAboveSma50ForBuyPct}%)", 10, distanceToSma50Pct.HasValue && distanceToSma50Pct.Value <= parameters.MaxDistanceAboveSma50ForBuyPct, "Entrée avec extension maîtrisée."),
            new("Drawdown 52w modéré (-18% à -4%)", 6, current.Drawdown52WeeksPercent.HasValue && current.Drawdown52WeeksPercent.Value is <= -4m and >= -18m, "Repli cohérent pour une cyclique lente."),

            new("Histogramme MACD en amélioration", 10, histogramDelta.HasValue && histogramDelta.Value > 0m, "Acceleration positive du momentum."),
            new("MACD line au-dessus du signal", 9, current.MacdLine.HasValue && current.MacdSignalLine.HasValue && current.MacdLine.Value > current.MacdSignalLine.Value, "Momentum redevenu favorable."),
            new("Croisement MACD haussier récent", 6, WasRecentBullishMacdCross(previous, current), "Retournement haussier récent confirmé."),
            new("RSI repart à la hausse", 0, previous?.Rsi14.HasValue == true && current.Rsi14.HasValue && current.Rsi14.Value > previous.Rsi14.Value, "Indicateur contextuel pour confirmer la reprise après pullback."),

            new("Filtre entrée: RSI <= plafond de buy", 5, current.Rsi14.HasValue && current.Rsi14.Value <= parameters.MaxRsiForBuy, "Évite une entrée trop tardive sur momentum déjà chaud."),
            new($"Filtre entrée: extension <= {parameters.MaxDistanceAboveSma50ForBuyPct}%", 5, distanceToSma50Pct.HasValue && distanceToSma50Pct.Value <= parameters.MaxDistanceAboveSma50ForBuyPct, "Blocage des achats en sur-extension."),
            new("Filtre contexte: gap SMA50/SMA200 suffisant", 0, smaGapPct.HasValue && smaGapPct.Value >= parameters.MinGapBetweenSma50AndSma200Pct, "Réduit les signaux en marché trop neutre.")
        };

        if (!parameters.EnableMacdConfirmation)
        {
            DisableMacdBuyRequirements(details);
        }

        if (current.Rsi14.HasValue && current.Rsi14.Value > parameters.MaxRsiForBuy)
        {
            details.Add(new("Pénalité buy: RSI trop élevé", -10, true, "RSI trop haut: entrée tardive probable."));
        }

        if (distanceToSma50Pct.HasValue && distanceToSma50Pct.Value > parameters.MaxDistanceAboveSma50ForBuyPct)
        {
            details.Add(new("Pénalité buy: prix trop au-dessus de SMA50", -15, true, "Sur-extension: risque de faux signal d'achat."));
        }

        if (beforePrevious is not null && previous is not null && current.Rsi14.HasValue && previous.Rsi14.HasValue && beforePrevious.Rsi14.HasValue)
        {
            var risingRsi = current.Rsi14.Value > previous.Rsi14.Value && previous.Rsi14.Value >= beforePrevious.Rsi14.Value;
            details.Add(new("RSI en reprise sur 2 barres", 5, risingRsi, "Reprise plus robuste du momentum après respiration."));
        }

        return details;
    }

    private static List<ScoreFactorDetail> BuildSellScoreDetails(
        ComputedIndicator current,
        ComputedIndicator? previous,
        MetaAlgoParameters parameters,
        decimal? slope,
        decimal? distanceToSma50Pct,
        decimal? histogramDelta)
    {
        var bearishMacdCross = WasRecentBearishMacdCross(previous, current);
        var overExtended = distanceToSma50Pct.HasValue && distanceToSma50Pct.Value >= parameters.StrongExtensionAboveSma50ForSellPct;

        var details = new List<ScoreFactorDetail>
        {
            new("Faiblesse momentum: histogramme MACD en dégradation", 12, histogramDelta.HasValue && histogramDelta.Value < 0m, "Perte d'accélération du momentum."),
            new("Faiblesse momentum: MACD line < signal", 12, current.MacdLine.HasValue && current.MacdSignalLine.HasValue && current.MacdLine.Value < current.MacdSignalLine.Value, "Momentum orienté à la baisse."),
            new("Faiblesse momentum: croisement MACD baissier récent", 11, bearishMacdCross, "Retournement baissier validé."),

            new("Dégradation tendance: EMA12 < EMA26", 14, current.Ema12.HasValue && current.Ema26.HasValue && current.Ema12.Value < current.Ema26.Value, "Perte de tendance court terme."),
            new($"Dégradation tendance: RSI < {parameters.MinRsiWeaknessForSell}", 8, current.Rsi14.HasValue && current.Rsi14.Value < parameters.MinRsiWeaknessForSell, "Momentum repasse sous la zone de neutralité."),
            new("Dégradation tendance: pente SMA50 trop faible", 8, slope.HasValue && slope.Value <= parameters.MaxFlatSlopeThreshold, "Tendance intermédiaire qui s'aplatit."),

            new("Extension: RSI > 70", 10, current.Rsi14.HasValue && current.Rsi14.Value > 70m, "Surachat avancé."),
            new($"Extension: prix > SMA50 de {parameters.StrongExtensionAboveSma50ForSellPct}%", 10, overExtended, "Extension excessive au-dessus de la tendance intermédiaire."),
            new("Extension + retournement momentum", 5, overExtended && bearishMacdCross, "Combinaison de sur-extension et cassure de momentum."),

            new("Faiblesse contextuelle précoce", 10, current.Rsi14.HasValue && current.Rsi14.Value < 55m && histogramDelta.HasValue && histogramDelta.Value < 0m, "Signes progressifs de fatigue pour early sell.")
        };

        if (!parameters.EnableMacdConfirmation)
        {
            DisableMacdSellRequirements(details);
        }

        return details;
    }

    private static void DisableMacdBuyRequirements(List<ScoreFactorDetail> details)
    {
        for (var index = 0; index < details.Count; index++)
        {
            if (details[index].Label.Contains("MACD", StringComparison.OrdinalIgnoreCase))
            {
                details[index] = details[index] with { Triggered = false, Points = 0, Description = "MACD désactivé dans la configuration." };
            }
        }
    }

    private static void DisableMacdSellRequirements(List<ScoreFactorDetail> details)
    {
        for (var index = 0; index < details.Count; index++)
        {
            if (details[index].Label.Contains("MACD", StringComparison.OrdinalIgnoreCase))
            {
                details[index] = details[index] with { Triggered = false, Points = 0, Description = "MACD désactivé dans la configuration." };
            }
        }
    }

    private static bool IsCooldownCompleted(DateOnly lastSignalDate, DateOnly currentDate, int minimumBarsBetweenSignals)
    {
        if (minimumBarsBetweenSignals <= 0 || lastSignalDate == DateOnly.MinValue)
        {
            return true;
        }

        return (currentDate.DayNumber - lastSignalDate.DayNumber) >= minimumBarsBetweenSignals;
    }

    private static bool WasRecentBullishMacdCross(ComputedIndicator? previous, ComputedIndicator current)
    {
        return previous?.MacdLine.HasValue == true
               && previous.MacdSignalLine.HasValue
               && current.MacdLine.HasValue
               && current.MacdSignalLine.HasValue
               && previous.MacdLine.Value <= previous.MacdSignalLine.Value
               && current.MacdLine.Value > current.MacdSignalLine.Value;
    }

    private static bool WasRecentBearishMacdCross(ComputedIndicator? previous, ComputedIndicator current)
    {
        return previous?.MacdLine.HasValue == true
               && previous.MacdSignalLine.HasValue
               && current.MacdLine.HasValue
               && current.MacdSignalLine.HasValue
               && previous.MacdLine.Value >= previous.MacdSignalLine.Value
               && current.MacdLine.Value < current.MacdSignalLine.Value;
    }

    private static decimal? ComputeSlopePct(decimal? previousValue, decimal? currentValue)
    {
        if (!previousValue.HasValue || !currentValue.HasValue || previousValue.Value == 0m)
        {
            return null;
        }

        return ((currentValue.Value / previousValue.Value) - 1m) * 100m;
    }

    private static decimal? ComputeDistanceAboveSma50Pct(decimal close, decimal? sma50)
    {
        if (!sma50.HasValue || sma50.Value == 0m)
        {
            return null;
        }

        return ((close / sma50.Value) - 1m) * 100m;
    }

    private static decimal? ComputeSmaGapPct(decimal? sma50, decimal? sma200)
    {
        if (!sma50.HasValue || !sma200.HasValue || sma200.Value == 0m)
        {
            return null;
        }

        return ((sma50.Value / sma200.Value) - 1m) * 100m;
    }

    private static decimal? ComputeHistogramDelta(decimal? currentHistogram, decimal? previousHistogram)
    {
        if (!currentHistogram.HasValue || !previousHistogram.HasValue)
        {
            return null;
        }

        return currentHistogram.Value - previousHistogram.Value;
    }
}
