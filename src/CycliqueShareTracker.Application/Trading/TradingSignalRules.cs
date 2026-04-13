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
            return new TradeSignal(bar.Date, TradeSignalType.Short, "SAR bascule VENTE + validation RSI/MACD.", true, false);
        }

        if (trendChanged && currentTrend == "ACHAT" && rsiStrengthAbs <= 1m && (sarChange is "acc" or "chg") && (macdInverse == 1 || macdRatioClose))
        {
            return new TradeSignal(bar.Date, TradeSignalType.Long, "SAR bascule ACHAT + validation RSI/MACD.", true, false);
        }

        return null;
    }

    public static TradeSignal? ComputeExitSignal(PriceBar bar, ComputedIndicator current, ComputedIndicator previous, int daysSinceBuyChange, int daysSinceSellChange)
    {
        var trend = GetTrendFromSar(current);
        var bbIsBottomUp = ComputeBbBottomUpSignal(bar, current);
        var bbMidHitUp = ComputeBbMidHitUp(bar, current, previous);
        var bbMidHitDown = ComputeBbMidHitDown(bar, current, previous);
        var macdTrendChange = ComputeMacdTrendChange(current, previous);
        if (trend == "ACHAT" && bbIsBottomUp == -1 && (bbMidHitUp == -1 || macdTrendChange == -1) && daysSinceBuyChange > 4)
        {
            return new TradeSignal(bar.Date, TradeSignalType.LeaveLong, "Essoufflement du LONG (Bollinger/MACD).", false, true);
        }

        if (trend == "VENTE" && bbIsBottomUp == 1 && (bbMidHitDown == 1 || macdTrendChange == 1) && daysSinceSellChange > 4)
        {
            return new TradeSignal(bar.Date, TradeSignalType.LeaveShort, "Essoufflement du SHORT (Bollinger/MACD).", false, true);
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
        if (!indicator.BollingerMiddle.HasValue)
        {
            return 0;
        }

        return bar.Close >= indicator.BollingerMiddle.Value ? 1 : -1;
    }

    private static int ComputeBbMidHitUp(PriceBar bar, ComputedIndicator current, ComputedIndicator previous)
    {
        if (!current.BollingerMiddle.HasValue || !previous.BollingerMiddle.HasValue || !previous.PreviousClose.HasValue)
        {
            return 0;
        }

        return previous.PreviousClose.Value >= previous.BollingerMiddle.Value && bar.Close < current.BollingerMiddle.Value ? -1 : 0;
    }

    private static int ComputeBbMidHitDown(PriceBar bar, ComputedIndicator current, ComputedIndicator previous)
    {
        if (!current.BollingerMiddle.HasValue || !previous.BollingerMiddle.HasValue || !previous.PreviousClose.HasValue)
        {
            return 0;
        }

        return previous.PreviousClose.Value <= previous.BollingerMiddle.Value && bar.Close > current.BollingerMiddle.Value ? 1 : 0;
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
