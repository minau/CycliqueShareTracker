using CycliqueShareTracker.Application.Interfaces;
using CycliqueShareTracker.Application.Models;

namespace CycliqueShareTracker.Application.Services;

public sealed class IndicatorCalculator : IIndicatorCalculator
{
    private const int MacdFastPeriod = 12;
    private const int MacdSlowPeriod = 26;
    private const int MacdSignalPeriod = 9;

    public IReadOnlyList<ComputedIndicator> Compute(IReadOnlyList<PriceBar> prices)
    {
        if (prices.Count == 0)
        {
            return Array.Empty<ComputedIndicator>();
        }

        var ordered = prices.OrderBy(p => p.Date).ToList();
        var closeSeries = ordered.Select(x => (decimal?)x.Close).ToList();
        var ema12Series = ComputeEma(closeSeries, MacdFastPeriod);
        var ema26Series = ComputeEma(closeSeries, MacdSlowPeriod);
        var macdLineSeries = ComputeEmaDifference(ema12Series, ema26Series);
        var macdSignalSeries = ComputeEma(macdLineSeries, MacdSignalPeriod);
        var results = new List<ComputedIndicator>(ordered.Count);

        for (var i = 0; i < ordered.Count; i++)
        {
            var current = ordered[i];
            decimal? sma50 = null;
            decimal? sma200 = null;
            decimal? rsi14 = null;
            decimal? drawdown = null;

            if (i >= 49)
            {
                sma50 = ordered.Skip(i - 49).Take(50).Average(p => p.Close);
            }

            if (i >= 199)
            {
                sma200 = ordered.Skip(i - 199).Take(200).Average(p => p.Close);
            }

            if (i >= 14)
            {
                rsi14 = ComputeRsi(ordered, i, 14);
            }

            if (i >= 1)
            {
                var lookback = Math.Min(252, i + 1);
                var highest = ordered.Skip(i - lookback + 1).Take(lookback).Max(p => p.High);
                drawdown = highest == 0 ? 0 : ((current.Close / highest) - 1m) * 100m;
            }

            var macdLine = macdLineSeries[i];
            var macdSignal = macdSignalSeries[i];
            decimal? macdHistogram = null;
            if (macdLine.HasValue && macdSignal.HasValue)
            {
                macdHistogram = decimal.Round(macdLine.Value - macdSignal.Value, 4);
            }

            decimal? previousMacdHistogram = null;
            if (i > 0 && macdLineSeries[i - 1].HasValue && macdSignalSeries[i - 1].HasValue)
            {
                previousMacdHistogram = decimal.Round(macdLineSeries[i - 1]!.Value - macdSignalSeries[i - 1]!.Value, 4);
            }

            var previousClose = i > 0 ? ordered[i - 1].Close : (decimal?)null;
            results.Add(new ComputedIndicator(
                current.Date,
                sma50,
                sma200,
                rsi14,
                drawdown,
                current.Close,
                previousClose,
                macdLine,
                macdSignal,
                macdHistogram,
                previousMacdHistogram,
                ema12Series[i],
                ema26Series[i]));
        }

        return results;
    }

    private static decimal?[] ComputeEmaDifference(IReadOnlyList<decimal?> fastEma, IReadOnlyList<decimal?> slowEma)
    {
        var result = new decimal?[fastEma.Count];

        for (var i = 0; i < fastEma.Count; i++)
        {
            if (fastEma[i].HasValue && slowEma[i].HasValue)
            {
                result[i] = decimal.Round(fastEma[i]!.Value - slowEma[i]!.Value, 4);
            }
        }

        return result;
    }

    private static decimal?[] ComputeEma(IReadOnlyList<decimal?> series, int period)
    {
        var ema = new decimal?[series.Count];
        if (series.Count < period)
        {
            return ema;
        }

        var seedIndex = -1;
        for (var i = period - 1; i < series.Count; i++)
        {
            var windowStart = i - period + 1;
            var hasFullWindow = true;
            for (var j = windowStart; j <= i; j++)
            {
                if (!series[j].HasValue)
                {
                    hasFullWindow = false;
                    break;
                }
            }

            if (!hasFullWindow)
            {
                continue;
            }

            seedIndex = i;
            break;
        }

        if (seedIndex < 0)
        {
            return ema;
        }

        var seedValues = series.Skip(seedIndex - period + 1).Take(period).Select(x => x!.Value).ToList();
        var multiplier = 2m / (period + 1m);
        var previous = seedValues.Average();
        ema[seedIndex] = decimal.Round(previous, 4);

        for (var i = seedIndex + 1; i < series.Count; i++)
        {
            if (!series[i].HasValue)
            {
                continue;
            }

            previous = ((series[i]!.Value - previous) * multiplier) + previous;
            ema[i] = decimal.Round(previous, 4);
        }

        return ema;
    }

    private static decimal ComputeRsi(IReadOnlyList<PriceBar> bars, int endIndex, int period)
    {
        decimal gain = 0;
        decimal loss = 0;

        for (var i = endIndex - period + 1; i <= endIndex; i++)
        {
            var change = bars[i].Close - bars[i - 1].Close;
            if (change > 0)
            {
                gain += change;
            }
            else
            {
                loss -= change;
            }
        }

        if (loss == 0)
        {
            return 100;
        }

        var rs = gain / loss;
        var rsi = 100m - (100m / (1m + rs));
        return decimal.Round(rsi, 2);
    }
}
