using CycliqueShareTracker.Application.Models;

namespace CycliqueShareTracker.Application.Trading;

public static class TradingSignalRules
{
    public static TradeSignal? ComputeEntrySignal(PriceBar bar, ComputedIndicator current, ComputedIndicator previous)
    {
        var currentTrend = GetTrendFromSar(current);
        var previousTrend = GetTrendFromSar(previous);
        var trendChanged = currentTrend != previousTrend;
        var sarChange = trendChanged ? "chg" : "none";
        var rsiStrengthAbs = current.Rsi14.HasValue ? (current.Rsi14.Value - 50m) / 25m : (decimal?)null;
        var macdInverse = ComputeMacdInverse(current);
        var macdRatioClose = IsMacdRatioClose(current);

        if (trendChanged && currentTrend == "VENTE" && rsiStrengthAbs >= -1m && (sarChange is "acc" or "chg") && (macdInverse == -1 || macdRatioClose))
        {
            var reasons = new List<string>
            {
                TradeSignalMetadata.SarReversalDetected,
                TradeSignalMetadata.RsiValidated,
                TradeSignalMetadata.MacdValidated
            };

            return BuildSignal(bar.Date, TradeSignalType.Short, reasons);
        }

        if (trendChanged && currentTrend == "ACHAT" && rsiStrengthAbs <= 1m && (sarChange is "acc" or "chg") && (macdInverse == 1 || macdRatioClose))
        {
            var reasons = new List<string>
            {
                TradeSignalMetadata.SarReversalDetected,
                TradeSignalMetadata.RsiValidated,
                TradeSignalMetadata.MacdValidated
            };

            return BuildSignal(bar.Date, TradeSignalType.Long, reasons);
        }

        return null;
    }

    public static TradeSignal? ComputeExitSignal(
        IReadOnlyList<PriceBar> orderedPrices,
        IReadOnlyDictionary<DateOnly, ComputedIndicator> indicatorsByDate,
        int currentIndex,
        int daysSinceBuyChange,
        int daysSinceSellChange)
    {
        if (currentIndex <= 0 || currentIndex >= orderedPrices.Count)
        {
            return null;
        }

        var bar = orderedPrices[currentIndex];
        var previousBar = orderedPrices[currentIndex - 1];
        if (!indicatorsByDate.TryGetValue(bar.Date, out var current) ||
            !indicatorsByDate.TryGetValue(previousBar.Date, out var previous))
        {
            return null;
        }

        var trend = GetTrendFromSar(current);
        var bbIsBottomUp = ComputeBbBottomUpSignal(bar, current);
        var bbMidHitUp = ComputeBbMidHitUp(orderedPrices, indicatorsByDate, currentIndex, bar, current);
        var bbMidHitDown = ComputeBbMidHitDown(orderedPrices, indicatorsByDate, currentIndex, bar, current);
        var macdTrendChange = ComputeMacdTrendChange(current, previous);

        // Priority is deterministic when multiple exit conditions are true:
        // ExitTargetReached > TrendWeakening > MomentumBreakdown.
        if (trend == "ACHAT" && bbIsBottomUp == -1 && (bbMidHitUp == -1 || macdTrendChange == -1) && daysSinceBuyChange > 4)
        {
            var reasons = BuildExitReasons(bbMidHitUp == -1, macdTrendChange == -1);
            return BuildSignal(bar.Date, TradeSignalType.LeaveLong, reasons);
        }

        if (trend == "VENTE" && bbIsBottomUp == 1 && (bbMidHitDown == 1 || macdTrendChange == 1) && daysSinceSellChange > 4)
        {
            var reasons = BuildExitReasons(bbMidHitDown == 1, macdTrendChange == 1);
            return BuildSignal(bar.Date, TradeSignalType.LeaveShort, reasons);
        }

        return null;
    }

    public static void UpdateSarTrendCounters(ComputedIndicator current, ComputedIndicator previous, ref int daysSinceBuyChange, ref int daysSinceSellChange)
    {
        var currentTrend = GetTrendFromSar(current);
        var previousTrend = GetTrendFromSar(previous);
        if (!string.Equals(currentTrend, previousTrend, StringComparison.Ordinal))
        {
            daysSinceBuyChange = currentTrend == "ACHAT" ? 0 : daysSinceBuyChange + 1;
            daysSinceSellChange = currentTrend == "VENTE" ? 0 : daysSinceSellChange + 1;
            return;
        }

        if (currentTrend == "ACHAT")
        {
            daysSinceBuyChange++;
            daysSinceSellChange = 0;
            return;
        }

        if (currentTrend == "VENTE")
        {
            daysSinceSellChange++;
            daysSinceBuyChange = 0;
            return;
        }

        daysSinceBuyChange = 0;
        daysSinceSellChange = 0;
    }

    private static List<string> BuildExitReasons(bool bbMidHit, bool macdTrendChanged)
    {
        var reasons = new List<string> { TradeSignalMetadata.ExitTargetReached };
        if (bbMidHit)
        {
            reasons.Add(TradeSignalMetadata.TrendWeakening);
        }

        if (macdTrendChanged)
        {
            reasons.Add(TradeSignalMetadata.MomentumBreakdown);
        }

        return reasons;
    }

    private static TradeSignal BuildSignal(DateOnly date, TradeSignalType type, IReadOnlyList<string> reasons)
    {
        var category = TradeSignalMetadata.GetCategory(type);
        return new TradeSignal(
            date,
            type,
            TradeSignalMetadata.BuildSignalReason(reasons),
            reasons,
            category == SignalCategory.Entry,
            category == SignalCategory.Exit,
            TradeSignalMetadata.GetDirection(type),
            category);
    }

    private static string GetTrendFromSar(ComputedIndicator indicator)
    {
        if (!indicator.ParabolicSar.HasValue)
        {
            return "NONE";
        }

        return indicator.Close >= indicator.ParabolicSar.Value ? "ACHAT" : "VENTE";
    }

    private static int ComputeMacdInverse(ComputedIndicator indicator)
    {
        if (!indicator.MacdLine.HasValue || !indicator.MacdSignalLine.HasValue)
        {
            return 0;
        }

        return indicator.MacdLine.Value >= indicator.MacdSignalLine.Value ? 1 : -1;
    }

    private static bool IsMacdRatioClose(ComputedIndicator indicator)
    {
        if (!indicator.MacdSignalLine.HasValue || !indicator.MacdLine.HasValue || indicator.MacdLine.Value == 0m)
        {
            return false;
        }

        return Math.Abs((indicator.MacdSignalLine.Value / indicator.MacdLine.Value) - 1m) < 0.15m;
    }

    private static int ComputeBbBottomUpSignal(PriceBar bar, ComputedIndicator indicator)
    {
        if (!indicator.BollingerMiddle.HasValue || !indicator.BollingerLower.HasValue || !indicator.BollingerUpper.HasValue)
        {
            return 0;
        }

        if (indicator.BollingerLower.Value <= bar.High && bar.High < indicator.BollingerMiddle.Value)
        {
            return -1;
        }

        if (indicator.BollingerUpper.Value > bar.Low && bar.Low >= indicator.BollingerMiddle.Value)
        {
            return 1;
        }

        return 0;
    }

    private static int ComputeBbMidHitUp(
        IReadOnlyList<PriceBar> orderedPrices,
        IReadOnlyDictionary<DateOnly, ComputedIndicator> indicatorsByDate,
        int currentIndex,
        PriceBar bar,
        ComputedIndicator current)
    {
        if (!current.BollingerMiddle.HasValue || bar.Close > current.BollingerMiddle.Value)
        {
            return 0;
        }

        if (!TryGetFourthPreviousValidPoint(orderedPrices, indicatorsByDate, currentIndex, out var jMinusFourBar, out var jMinusFourIndicator))
        {
            return 0;
        }

        return jMinusFourBar.Close > jMinusFourIndicator.BollingerMiddle!.Value && jMinusFourBar.Close > bar.Close ? -1 : 0;
    }

    private static int ComputeBbMidHitDown(
        IReadOnlyList<PriceBar> orderedPrices,
        IReadOnlyDictionary<DateOnly, ComputedIndicator> indicatorsByDate,
        int currentIndex,
        PriceBar bar,
        ComputedIndicator current)
    {
        if (!current.BollingerMiddle.HasValue || bar.Close <= current.BollingerMiddle.Value)
        {
            return 0;
        }

        if (!TryGetFourthPreviousValidPoint(orderedPrices, indicatorsByDate, currentIndex, out var jMinusFourBar, out var jMinusFourIndicator))
        {
            return 0;
        }

        return jMinusFourBar.Close < jMinusFourIndicator.BollingerMiddle!.Value && jMinusFourBar.Close < bar.Close ? 1 : 0;
    }

    private static bool TryGetFourthPreviousValidPoint(
        IReadOnlyList<PriceBar> orderedPrices,
        IReadOnlyDictionary<DateOnly, ComputedIndicator> indicatorsByDate,
        int currentIndex,
        out PriceBar bar,
        out ComputedIndicator indicator)
    {
        // J-4 must be the 4th previous trading point with exploitable values (Bollinger middle available).
        var validPointsFound = 0;
        for (var i = currentIndex - 1; i >= 0; i--)
        {
            var candidateBar = orderedPrices[i];
            if (!indicatorsByDate.TryGetValue(candidateBar.Date, out var candidateIndicator) || !candidateIndicator.BollingerMiddle.HasValue)
            {
                continue;
            }

            validPointsFound++;
            if (validPointsFound != 4)
            {
                continue;
            }

            bar = candidateBar;
            indicator = candidateIndicator;
            return true;
        }

        bar = default!;
        indicator = default!;
        return false;
    }

    private static int ComputeMacdTrendChange(ComputedIndicator current, ComputedIndicator previous)
    {
        if (!current.MacdHistogram.HasValue || !previous.MacdHistogram.HasValue)
        {
            return 0;
        }

        if (previous.MacdHistogram.Value >= 0m && current.MacdHistogram.Value < 0m)
        {
            return -1;
        }

        if (previous.MacdHistogram.Value <= 0m && current.MacdHistogram.Value > 0m)
        {
            return 1;
        }

        return 0;
    }
}
