using CycliqueShareTracker.Application.Interfaces;
using CycliqueShareTracker.Application.Models;

namespace CycliqueShareTracker.Application.Services;

public sealed class IndicatorCalculator : IIndicatorCalculator
{
    public IReadOnlyList<ComputedIndicator> Compute(IReadOnlyList<PriceBar> prices)
    {
        if (prices.Count == 0)
        {
            return Array.Empty<ComputedIndicator>();
        }

        var ordered = prices.OrderBy(p => p.Date).ToList();
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

            var previousClose = i > 0 ? ordered[i - 1].Close : null;
            results.Add(new ComputedIndicator(current.Date, sma50, sma200, rsi14, drawdown, current.Close, previousClose));
        }

        return results;
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
