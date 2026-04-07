using CycliqueShareTracker.Application.Models;

namespace CycliqueShareTracker.Application.Algorithms;

public sealed class CompositeTrendPullbackAlgorithm : SignalAlgorithmBase
{
    public override AlgorithmType AlgorithmType => AlgorithmType.CompositeTrendPullback;
    public override string DisplayName => "Composite Trend Pullback";

    // BUY blocks: Trend 30, Pullback 25, Momentum 35, Filter 10.
    private const int BuyTrendWeight = 30;
    private const int BuyPullbackWeight = 25;
    private const int BuyMomentumWeight = 35;
    private const int BuyEntryFilterWeight = 10;

    // SELL blocks: Momentum 35, Trend 30, Extension 25, Early 10.
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
            var histogramDecliningTwoBars = IsHistogramDecliningTwoBars(current, previous, beforePrevious);

            var rsiZone = ResolveRsiZone(current.Rsi14);
            var pullbackType = ResolvePullbackType(current.Rsi14);
            var rsiMomentumState = ResolveRsiMomentumState(previous?.Rsi14, current.Rsi14);
            var momentumWeakening = histogramDelta.HasValue && histogramDelta.Value < 0m;
            var rsiPlateau = rsiMomentumState == "flat" && current.Rsi14.HasValue && current.Rsi14.Value >= 60m;

            var buyDetails = BuildBuyScoreDetails(current, previous, beforePrevious, parameters, slope, distanceToSma50Pct, smaGapPct, histogramDelta, rsiZone, pullbackType);
            var sellDetails = BuildSellScoreDetails(current, previous, parameters, slope, distanceToSma50Pct, histogramDelta, histogramDecliningTwoBars, rsiMomentumState);

            var buyScore = CountTriggeredScore(buyDetails);
            var sellScore = CountTriggeredScore(sellDetails);

            var buyZone = buyScore >= parameters.BuyScoreThreshold;
            var sellZone = sellScore >= parameters.SellScoreThreshold;

            var buyCooldownOk = IsCooldownCompleted(lastBuySignalDate, current.Date, parameters.MinimumBarsBetweenSameSignal);
            var sellCooldownOk = IsCooldownCompleted(lastSellSignalDate, current.Date, parameters.MinimumBarsBetweenSameSignal);

            var buySignal = buyZone && buyCooldownOk;
            var weaknessScore = sellDetails.Where(x => x.Triggered && x.Label.StartsWith("Faiblesse", StringComparison.OrdinalIgnoreCase)).Sum(x => x.Points);
            var earlySellCondition = momentumWeakening || rsiPlateau;
            var earlySell = parameters.EarlySellEnabled
                            && !sellZone
                            && earlySellCondition
                            && (weaknessScore >= parameters.EarlySellWeaknessScoreThreshold || (momentumWeakening && rsiPlateau));
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
                ? "Entrée validée: tendance haussière, pullback acceptable et reprise momentum avec extension maîtrisée."
                : "Achat non validé: configuration incomplète ou extension trop élevée sans reprise suffisante.";

            var sellReason = sellSignal
                ? (earlySell
                    ? "Sortie anticipée: momentum qui faiblit (histogramme/RSI) avant dégradation complète." 
                    : "Sortie validée: affaiblissement trend/momentum ou sur-extension retournée.")
                : "Pas de sortie: pression vendeuse insuffisante dans le contexte actuel.";

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
                    ["rsiZone"] = rsiZone,
                    ["pullbackType"] = pullbackType,
                    ["momentumWeakening"] = momentumWeakening,
                    ["rsiMomentumState"] = rsiMomentumState,
                    ["sma50SlopePct"] = slope,
                    ["distanceAboveSma50Pct"] = distanceToSma50Pct,
                    ["smaGapPct"] = smaGapPct,
                    ["histogramDelta"] = histogramDelta,
                    ["histogramDecliningTwoBars"] = histogramDecliningTwoBars,
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
        decimal? histogramDelta,
        string rsiZone,
        string pullbackType)
    {
        var trendBySma = current.Sma50.HasValue && current.Sma200.HasValue && current.Sma50.Value > current.Sma200.Value;
        var fallbackTrend = !current.Sma200.HasValue && current.Sma50.HasValue && current.Close > current.Sma50.Value;

        var details = new List<ScoreFactorDetail>
        {
            // Trend block: 30
            new("Trend: SMA50 > SMA200", 18, trendBySma, "Tendance de fond clairement haussière."),
            new("Trend fallback: SMA200 absente", 12, fallbackTrend, "Fallback prudent sur début de série."),
            new("Trend: pente SMA50 positive", 6, slope.HasValue && slope.Value >= parameters.MinSma50SlopeForBuy, "Tendance intermédiaire orientée positivement."),
            new("Trend: EMA12 > EMA26", 6, current.Ema12.HasValue && current.Ema26.HasValue && current.Ema12.Value > current.Ema26.Value, "Momentum de tendance court terme."),

            // Pullback block: 25
            new("Pullback fort: RSI <= 55", 15, pullbackType == "strong", "Pullback de bonne qualité pour une entrée cyclique."),
            new("Pullback acceptable: RSI 55-65", 9, pullbackType == "weak", "Pullback moins profond mais exploitable."),
            new($"Prix proche SMA50 (<= {parameters.MaxDistanceAboveSma50ForBuyPct}%)", 7, distanceToSma50Pct.HasValue && distanceToSma50Pct.Value <= parameters.MaxDistanceAboveSma50ForBuyPct, "Entrée proche de la tendance intermédiaire."),
            new("Drawdown 52w modéré (-20% à -4%)", 3, current.Drawdown52WeeksPercent.HasValue && current.Drawdown52WeeksPercent.Value is <= -4m and >= -20m, "Contexte de respiration sans rupture majeure."),

            // Momentum block: 35
            new("Momentum: histogramme MACD en amélioration", 14, histogramDelta.HasValue && histogramDelta.Value > 0m, "Accélération haussière en reprise."),
            new("Momentum: MACD line > signal", 12, current.MacdLine.HasValue && current.MacdSignalLine.HasValue && current.MacdLine.Value > current.MacdSignalLine.Value, "Momentum repasse positif."),
            new("Momentum: croisement MACD haussier récent", 9, WasRecentBullishMacdCross(previous, current), "Bonus de redémarrage du momentum."),

            // Filter block: 10
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

    private static List<ScoreFactorDetail> BuildSellScoreDetails(
        ComputedIndicator current,
        ComputedIndicator? previous,
        MetaAlgoParameters parameters,
        decimal? slope,
        decimal? distanceToSma50Pct,
        decimal? histogramDelta,
        bool histogramDecliningTwoBars,
        string rsiMomentumState)
    {
        var bearishMacdCross = WasRecentBearishMacdCross(previous, current);
        var overExtended = distanceToSma50Pct.HasValue && distanceToSma50Pct.Value >= parameters.StrongExtensionAboveSma50ForSellPct;
        var topDetection = current.Rsi14.HasValue && current.Rsi14.Value > 65m && histogramDelta.HasValue && histogramDelta.Value < 0m;
        var rsiVigilance = current.Rsi14.HasValue && current.Rsi14.Value >= 60m && current.Rsi14.Value <= 70m;
        var rsiOverbought = current.Rsi14.HasValue && current.Rsi14.Value > 70m;

        var details = new List<ScoreFactorDetail>
        {
            // Momentum weakness 35+
            new("Faiblesse momentum: histogramme MACD en baisse", 20, histogramDelta.HasValue && histogramDelta.Value < 0m, "Perte d'accélération du momentum."),
            new("Faiblesse momentum: histogramme négatif sur 2 barres", 30, histogramDecliningTwoBars, "Faiblesse persistante sur plusieurs barres."),
            new("Faiblesse momentum: MACD line < signal", 14, current.MacdLine.HasValue && current.MacdSignalLine.HasValue && current.MacdLine.Value < current.MacdSignalLine.Value, "Momentum orienté à la baisse."),
            new("Faiblesse momentum: croisement MACD baissier récent", 11, bearishMacdCross, "Retournement baissier validé."),

            // Trend deterioration 30
            new("Dégradation tendance: EMA12 < EMA26", 14, current.Ema12.HasValue && current.Ema26.HasValue && current.Ema12.Value < current.Ema26.Value, "Perte de tendance court terme."),
            new($"Dégradation tendance: RSI < {parameters.MinRsiWeaknessForSell}", 8, current.Rsi14.HasValue && current.Rsi14.Value < parameters.MinRsiWeaknessForSell, "Momentum passe sous la neutralité."),
            new("Dégradation tendance: pente SMA50 faible", 8, slope.HasValue && slope.Value <= parameters.MaxFlatSlopeThreshold, "Tendance intermédiaire qui s'aplatit."),

            // Extension / top detection 25+
            new("RSI vigilance 60-70", 8, rsiVigilance, "Zone de vigilance: momentum haut avec risque de plafonnement."),
            new("RSI surachat > 70", 14, rsiOverbought, "Zone de surachat avancé."),
            new($"Extension: prix > SMA50 de {parameters.StrongExtensionAboveSma50ForSellPct}%", 10, overExtended, "Extension excessive au-dessus de la tendance intermédiaire."),
            new("Extension + retournement momentum", 5, overExtended && bearishMacdCross, "Combinaison de sur-extension et cassure de momentum."),
            new("Top detection: RSI > 65 + histogramme en baisse", 25, topDetection, "Signal fort de sommet de cycle potentiel."),

            // Early contextual weakness 10
            new("Faiblesse contextuelle précoce", 10, (histogramDelta.HasValue && histogramDelta.Value < 0m) || rsiMomentumState == "flat", "Déclencheur précoce sur fatigue momentum ou RSI qui plafonne.")
        };

        if (!parameters.EnableMacdConfirmation)
        {
            DisableMacdSellRequirements(details);
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
