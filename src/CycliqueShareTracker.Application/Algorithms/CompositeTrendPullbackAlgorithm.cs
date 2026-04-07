using CycliqueShareTracker.Application.Models;

namespace CycliqueShareTracker.Application.Algorithms;

public sealed class CompositeTrendPullbackAlgorithm : SignalAlgorithmBase
{
    public override AlgorithmType AlgorithmType => AlgorithmType.CompositeTrendPullback;
    public override string DisplayName => "Composite Trend Pullback";

    private const int BuyTrendWeight = 30;
    private const int BuyPullbackWeight = 25;
    private const int BuyMomentumWeight = 35;
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
        var warningDuration = 0;

        foreach (var current in context.Indicators)
        {
            var previous = previousIndicators.LastOrDefault();
            var beforePrevious = previousIndicators.Count > 1 ? previousIndicators.ElementAt(previousIndicators.Count - 2) : null;

            var slope = ComputeSlopePct(previous?.Sma50, current.Sma50);
            var distanceToSma50Pct = ComputeDistanceAboveSma50Pct(current.Close, current.Sma50);
            var smaGapPct = ComputeSmaGapPct(current.Sma50, current.Sma200);
            var histogramDelta = ComputeHistogramDelta(current.MacdHistogram, previous?.MacdHistogram);
            var histogramDecliningTwoBars = IsHistogramDecliningTwoBars(current, previous, beforePrevious);

            var rsiZone = ResolveRsiZone(current.Rsi14);
            var pullbackType = ResolvePullbackType(current.Rsi14);
            var rsiMomentumState = ResolveRsiMomentumState(previous?.Rsi14, current.Rsi14);

            var buyDetails = BuildBuyScoreDetails(current, previous, beforePrevious, parameters, slope, distanceToSma50Pct, smaGapPct, histogramDelta, rsiZone, pullbackType);

            var warningDetails = BuildEarlyWarningDetails(current, parameters, slope, histogramDelta, rsiMomentumState);
            var confirmedDetails = BuildConfirmedSellDetails(current, previous, parameters, distanceToSma50Pct, histogramDelta, histogramDecliningTwoBars);

            var earlyWarningScore = CountTriggeredScore(warningDetails);
            var confirmedSellScore = CountTriggeredScore(confirmedDetails);

            var buyScore = CountTriggeredScore(buyDetails);
            var buyZone = buyScore >= parameters.BuyScoreThreshold;

            var gateReasons = BuildSellGateReasons(current, previous, parameters, distanceToSma50Pct, histogramDecliningTwoBars);
            var sellConfirmedByGate = gateReasons.Count > 0;

            var warningActive = parameters.EarlySellEnabled && earlyWarningScore >= parameters.EarlySellWeaknessScoreThreshold;
            warningDuration = warningActive ? warningDuration + 1 : 0;

            var sellByThresholdGate = confirmedSellScore >= parameters.SellScoreThreshold && sellConfirmedByGate;
            var sellByEarlyGate = sellConfirmedByGate && confirmedSellScore >= 12;
            var sellByProgressiveWarning = warningDuration >= 2 && confirmedSellScore >= 10;
            var sellZone = sellByThresholdGate || sellByEarlyGate || sellByProgressiveWarning;

            var buyCooldownOk = IsCooldownCompleted(lastBuySignalDate, current.Date, parameters.MinimumBarsBetweenSameSignal);
            var sellCooldownOk = IsCooldownCompleted(lastSellSignalDate, current.Date, parameters.MinimumBarsBetweenSameSignal);

            var buySignal = buyZone && buyCooldownOk;
            var sellSignal = sellZone && sellCooldownOk;

            if (buySignal)
            {
                lastBuySignalDate = current.Date;
            }

            if (sellSignal)
            {
                lastSellSignalDate = current.Date;
            }

            var buyReason = buySignal
                ? "Entrée validée: tendance haussière, pullback acceptable et reprise momentum avec extension maîtrisée."
                : "Achat non validé: configuration incomplète ou extension trop élevée sans reprise suffisante.";

            var sellReason = sellSignal
                ? sellByProgressiveWarning
                    ? "Sortie progressive: warning persistant puis confirmation minimale du sell score."
                    : sellByEarlyGate
                        ? "Sortie anticipée confirmée: gate SELL validé + score confirmé >= 12."
                        : "Sortie confirmée: dégradation validée par gate SELL (momentum/tendance/extension)."
                : warningActive
                    ? "Warning SELL: fatigue détectée, sans confirmation suffisante pour sortie ferme."
                    : "Pas de sortie: ni warning fort, ni confirmation baissière.";

            var signalType = buySignal && sellSignal
                ? "Conflict"
                : buySignal ? "Buy"
                : sellSignal ? "Sell"
                : warningActive ? "Warning"
                : "Neutral";

            var sellDetails = warningDetails.Concat(confirmedDetails).ToList();

            var point = new AlgorithmSignalPoint(
                current.Date,
                buyZone,
                sellZone,
                buySignal,
                sellSignal,
                buyScore,
                confirmedSellScore,
                decimal.Round(Math.Abs(buyScore - confirmedSellScore) / 100m, 2),
                buyReason,
                sellReason,
                buyDetails,
                sellDetails)
            {
                DebugValues = new Dictionary<string, object?>
                {
                    ["signalType"] = signalType,
                    ["rsiZone"] = rsiZone,
                    ["pullbackType"] = pullbackType,
                    ["rsiMomentumState"] = rsiMomentumState,
                    ["histogramDelta"] = histogramDelta,
                    ["histogramDecliningTwoBars"] = histogramDecliningTwoBars,
                    ["sma50SlopePct"] = slope,
                    ["distanceAboveSma50Pct"] = distanceToSma50Pct,
                    ["smaGapPct"] = smaGapPct,
                    ["earlyWarningScore"] = earlyWarningScore,
                    ["confirmedSellScore"] = confirmedSellScore,
                    ["sellConfirmedByGate"] = sellConfirmedByGate,
                    ["sellGateReasons"] = gateReasons,
                    ["warningActive"] = warningActive,
                    ["warningDuration"] = warningDuration,
                    ["sellByEarlyGate"] = sellByEarlyGate,
                    ["sellByProgressiveWarning"] = sellByProgressiveWarning,
                    ["buyThreshold"] = parameters.BuyScoreThreshold,
                    ["sellThreshold"] = parameters.SellScoreThreshold,
                    ["buyWeights"] = new Dictionary<string, int>
                    {
                        ["trend"] = BuyTrendWeight,
                        ["pullback"] = BuyPullbackWeight,
                        ["momentum"] = BuyMomentumWeight,
                        ["filter"] = BuyEntryFilterWeight
                    },
                    ["sellWeights"] = new Dictionary<string, int>
                    {
                        ["momentumWeakness"] = SellMomentumWeaknessWeight,
                        ["trendDeterioration"] = SellTrendDeteriorationWeight,
                        ["extension"] = SellExtensionWeight,
                        ["earlySell"] = SellEarlyWeaknessWeight
                    },
                    ["buyCooldownOk"] = buyCooldownOk,
                    ["sellCooldownOk"] = sellCooldownOk
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

    private static IReadOnlyList<string> BuildSellGateReasons(
        ComputedIndicator current,
        ComputedIndicator? previous,
        MetaAlgoParameters parameters,
        decimal? distanceToSma50Pct,
        bool histogramDecliningTwoBars)
    {
        var reasons = new List<string>();

        if (current.Ema12.HasValue && current.Ema26.HasValue && current.Ema12.Value < current.Ema26.Value)
        {
            reasons.Add("EMA12 < EMA26");
        }

        if (current.MacdLine.HasValue && current.MacdSignalLine.HasValue && current.MacdLine.Value < current.MacdSignalLine.Value)
        {
            reasons.Add("MACD line < signal line");
        }

        if (histogramDecliningTwoBars)
        {
            reasons.Add("Histogramme MACD en baisse sur 2 barres");
        }

        if (current.Rsi14.HasValue && current.Rsi14.Value < 50m)
        {
            reasons.Add("RSI < 50");
        }

        if (distanceToSma50Pct.HasValue && distanceToSma50Pct.Value > parameters.StrongExtensionAboveSma50ForSellPct)
        {
            reasons.Add("Distance au-dessus SMA50 > seuil fort");
        }

        if (WasRecentBearishMacdCross(previous, current))
        {
            reasons.Add("Croisement MACD baissier récent");
        }

        return reasons;
    }

    private static List<ScoreFactorDetail> BuildEarlyWarningDetails(
        ComputedIndicator current,
        MetaAlgoParameters parameters,
        decimal? slope,
        decimal? histogramDelta,
        string rsiMomentumState)
    {
        return new List<ScoreFactorDetail>
        {
            new("Warning: histogramme MACD en baisse (1 barre)", 8, histogramDelta.HasValue && histogramDelta.Value < 0m, "Fatigue momentum naissante, sans confirmation complète."),
            new("Warning: RSI qui plafonne", 6, rsiMomentumState == "flat", "Le momentum RSI cesse de progresser."),
            new("Warning: RSI > 65", 4, current.Rsi14.HasValue && current.Rsi14.Value > 65m, "Zone de vigilance de fin de jambe haussière."),
            new("Warning: pente SMA50 qui ralentit", 5, slope.HasValue && slope.Value <= parameters.MaxFlatSlopeThreshold, "Ralentissement de la tendance intermédiaire."),
            new("Warning top detection (léger)", 8, current.Rsi14.HasValue && current.Rsi14.Value > 65m && histogramDelta.HasValue && histogramDelta.Value < 0m, "Alerte de sommet potentiel, mais pas sortie ferme.")
        };
    }

    private static List<ScoreFactorDetail> BuildConfirmedSellDetails(
        ComputedIndicator current,
        ComputedIndicator? previous,
        MetaAlgoParameters parameters,
        decimal? distanceToSma50Pct,
        decimal? histogramDelta,
        bool histogramDecliningTwoBars)
    {
        var bearishCross = WasRecentBearishMacdCross(previous, current);
        var macdBelowSignal = current.MacdLine.HasValue && current.MacdSignalLine.HasValue && current.MacdLine.Value < current.MacdSignalLine.Value;
        var emaBroken = current.Ema12.HasValue && current.Ema26.HasValue && current.Ema12.Value < current.Ema26.Value;
        var rsiBroken = current.Rsi14.HasValue && current.Rsi14.Value < 50m;
        var strongExtension = distanceToSma50Pct.HasValue && distanceToSma50Pct.Value > parameters.StrongExtensionAboveSma50ForSellPct;

        return new List<ScoreFactorDetail>
        {
            new("Confirm: histogramme MACD en baisse persistante (2 barres)", 14, histogramDecliningTwoBars, "Dégradation confirmée du momentum."),
            new("Confirm: MACD line < signal", 14, macdBelowSignal, "Momentum cassé."),
            new("Confirm: croisement MACD baissier récent", 14, bearishCross, "Retournement confirmé."),
            new("Confirm: EMA12 < EMA26", 18, emaBroken, "Cassure de tendance court terme."),
            new("Confirm: RSI < 50", 12, rsiBroken, "Momentum baissier installé."),
            new("Confirm: extension forte au-dessus SMA50", 10, strongExtension, "Risque de reprise baissière après sur-extension."),
            new("Confirm: extension + confirmation baissière", 12, strongExtension && (macdBelowSignal || bearishCross), "Sortie validée après excès."),
            new("Confirm: histogramme < 0 avec MACD < signal", 10, histogramDelta.HasValue && histogramDelta.Value < 0m && macdBelowSignal, "Momentum négatif cohérent.")
        };
    }

    private static List<ScoreFactorDetail> BuildBuyScoreDetails(
        ComputedIndicator current,
        ComputedIndicator? previous,
        ComputedIndicator? beforePrevious,
        MetaAlgoParameters parameters,
        decimal? slope,
        decimal? distanceToSma50Pct,
        decimal? smaGapPct,
        decimal? histogramDelta,
        string rsiZone,
        string pullbackType)
    {
        var trendBySma = current.Sma50.HasValue && current.Sma200.HasValue && current.Sma50.Value > current.Sma200.Value;
        var fallbackTrend = !current.Sma200.HasValue && current.Sma50.HasValue && current.Close > current.Sma50.Value;

        var details = new List<ScoreFactorDetail>
        {
            new("Trend: SMA50 > SMA200", 18, trendBySma, "Tendance de fond clairement haussière."),
            new("Trend fallback: SMA200 absente", 12, fallbackTrend, "Fallback prudent sur début de série."),
            new("Trend: pente SMA50 positive", 6, slope.HasValue && slope.Value >= parameters.MinSma50SlopeForBuy, "Tendance intermédiaire orientée positivement."),
            new("Trend: EMA12 > EMA26", 6, current.Ema12.HasValue && current.Ema26.HasValue && current.Ema12.Value > current.Ema26.Value, "Momentum de tendance court terme."),

            new("Pullback fort: RSI <= 55", 15, pullbackType == "strong", "Pullback de bonne qualité pour une entrée cyclique."),
            new("Pullback acceptable: RSI 55-65", 9, pullbackType == "weak", "Pullback moins profond mais exploitable."),
            new($"Prix proche SMA50 (<= {parameters.MaxDistanceAboveSma50ForBuyPct}%)", 7, distanceToSma50Pct.HasValue && distanceToSma50Pct.Value <= parameters.MaxDistanceAboveSma50ForBuyPct, "Entrée proche de la tendance intermédiaire."),
            new("Drawdown 52w modéré (-20% à -4%)", 3, current.Drawdown52WeeksPercent.HasValue && current.Drawdown52WeeksPercent.Value is <= -4m and >= -20m, "Contexte de respiration sans rupture majeure."),

            new("Momentum: histogramme MACD en amélioration", 14, histogramDelta.HasValue && histogramDelta.Value > 0m, "Accélération haussière en reprise."),
            new("Momentum: MACD line > signal", 12, current.MacdLine.HasValue && current.MacdSignalLine.HasValue && current.MacdLine.Value > current.MacdSignalLine.Value, "Momentum repasse positif."),
            new("Momentum: croisement MACD haussier récent", 9, WasRecentBullishMacdCross(previous, current), "Bonus de redémarrage du momentum."),

            new($"Filtre extension <= {parameters.MaxDistanceAboveSma50ForBuyPct}%", 5, distanceToSma50Pct.HasValue && distanceToSma50Pct.Value <= parameters.MaxDistanceAboveSma50ForBuyPct, "Évite les entrées trop étirées."),
            new("Filtre contexte trend non-neutre", 5, smaGapPct.HasValue && smaGapPct.Value >= parameters.MinGapBetweenSma50AndSma200Pct, "Réduit les signaux en range plat."),
            new("RSI zone neutre 65-72", 0, rsiZone == "neutral", "Toléré sans bonus ni malus pour cycliques lentes."),
            new("RSI zone élevée >72", 0, rsiZone == "high", "Risque d'entrée tardive, pénalité dédiée appliquée."),
            new("RSI repart à la hausse", 0, previous?.Rsi14.HasValue == true && current.Rsi14.HasValue && current.Rsi14.Value > previous.Rsi14.Value, "Contexte de reprise additionnelle.")
        };

        if (!parameters.EnableMacdConfirmation)
        {
            DisableMacdBuyRequirements(details);
        }

        if (rsiZone == "high")
        {
            details.Add(new("Pénalité buy: RSI > 72", -8, true, "Surachat avancé, entrée moins favorable."));
        }

        if (distanceToSma50Pct.HasValue && distanceToSma50Pct.Value > parameters.MaxDistanceAboveSma50ForBuyPct + 3m)
        {
            details.Add(new("Pénalité buy: extension excessive", -10, true, "Titre trop étendu par rapport à SMA50."));
        }

        if (beforePrevious is not null && previous is not null && current.Rsi14.HasValue && previous.Rsi14.HasValue && beforePrevious.Rsi14.HasValue)
        {
            var risingRsi = current.Rsi14.Value > previous.Rsi14.Value && previous.Rsi14.Value >= beforePrevious.Rsi14.Value;
            details.Add(new("RSI en reprise sur 2 barres", 4, risingRsi, "Reprise progressive validée."));
        }

        return details;
    }

    private static string ResolveRsiZone(decimal? rsi)
    {
        if (!rsi.HasValue)
        {
            return "unknown";
        }

        if (rsi.Value <= 55m)
        {
            return "low";
        }

        if (rsi.Value <= 65m)
        {
            return "acceptable";
        }

        if (rsi.Value <= 72m)
        {
            return "neutral";
        }

        return "high";
    }

    private static string ResolvePullbackType(decimal? rsi)
    {
        if (!rsi.HasValue)
        {
            return "none";
        }

        if (rsi.Value <= 55m)
        {
            return "strong";
        }

        if (rsi.Value <= 65m)
        {
            return "weak";
        }

        return "none";
    }

    private static string ResolveRsiMomentumState(decimal? previousRsi, decimal? currentRsi)
    {
        if (!previousRsi.HasValue || !currentRsi.HasValue)
        {
            return "unknown";
        }

        var delta = currentRsi.Value - previousRsi.Value;
        if (Math.Abs(delta) <= 0.5m)
        {
            return "flat";
        }

        return delta > 0m ? "rising" : "falling";
    }

    private static bool IsHistogramDecliningTwoBars(ComputedIndicator current, ComputedIndicator? previous, ComputedIndicator? beforePrevious)
    {
        if (!current.MacdHistogram.HasValue || previous?.MacdHistogram.HasValue != true || beforePrevious?.MacdHistogram.HasValue != true)
        {
            return false;
        }

        return current.MacdHistogram.Value < previous.MacdHistogram.Value && previous.MacdHistogram.Value < beforePrevious.MacdHistogram.Value;
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
