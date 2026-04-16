using CycliqueShareTracker.Application.Interfaces;
using CycliqueShareTracker.Application.Models;

namespace CycliqueShareTracker.Application.Services;

public sealed class IndicatorCalculator : IIndicatorCalculator
{
    public IReadOnlyList<ComputedIndicator> Compute(IReadOnlyList<PriceBar> prices, IndicatorComputationSettings? settings = null)
    {
        settings ??= IndicatorComputationSettings.Default;
        if (prices.Count == 0)
        {
            return Array.Empty<ComputedIndicator>();
        }

        var ordered = prices.OrderBy(p => p.Date).ToList();
        var closeSeries = new decimal?[ordered.Count];
        for (var i = 0; i < ordered.Count; i++)
        {
            closeSeries[i] = ordered[i].Close;
        }

        var ema12Series = ComputeEma(closeSeries, settings.MacdFastPeriod);
        var ema26Series = ComputeEma(closeSeries, settings.MacdSlowPeriod);
        var macdLineSeries = ComputeEmaDifference(ema12Series, ema26Series);
        var macdSignalSeries = ComputeEma(macdLineSeries, settings.MacdSignalPeriod);
        var bollingerSeries = ComputeBollingerBands(ordered, settings.BollingerPeriod, settings.BollingerStdDev);
        var parabolicSarSeries = ComputeParabolicSar(ordered, settings.ParabolicSarStep, settings.ParabolicSarMax);
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
                decimal sum50 = 0;
                for (var j = i - 49; j <= i; j++)
                {
                    sum50 += ordered[j].Close;
                }

                sma50 = decimal.Round(sum50 / 50m, 4);
            }

            if (i >= 199)
            {
                decimal sum200 = 0;
                for (var j = i - 199; j <= i; j++)
                {
                    sum200 += ordered[j].Close;
                }

                sma200 = decimal.Round(sum200 / 200m, 4);
            }

            if (i >= 14)
            {
                rsi14 = ComputeRsi(ordered, i, 14);
            }

            if (i >= 1)
            {
                var lookback = Math.Min(252, i + 1);
                var startIndex = i - lookback + 1;
                var highest = ordered[startIndex].High;
                for (var j = startIndex + 1; j <= i; j++)
                {
                    if (ordered[j].High > highest)
                    {
                        highest = ordered[j].High;
                    }
                }

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
                ema26Series[i],
                bollingerSeries[i].Middle,
                bollingerSeries[i].Upper,
                bollingerSeries[i].Lower,
                bollingerSeries[i].StdDev,
                parabolicSarSeries[i].Sar));
        }

        return results;
    }

    public IReadOnlyList<BollingerBandsPoint> ComputeBollingerBands(
        IReadOnlyList<PriceBar> prices,
        int period = 20,
        decimal standardDeviationMultiplier = 2.0m)
    {
        if (period <= 1)
        {
            throw new ArgumentOutOfRangeException(nameof(period), "Period must be greater than 1.");
        }

        if (standardDeviationMultiplier <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(standardDeviationMultiplier), "Standard deviation multiplier must be positive.");
        }

        if (prices.Count == 0)
        {
            return Array.Empty<BollingerBandsPoint>();
        }

        var ordered = prices.OrderBy(p => p.Date).ToList();
        var result = new List<BollingerBandsPoint>(ordered.Count);

        for (var i = 0; i < ordered.Count; i++)
        {
            decimal? middle = null;
            decimal? upper = null;
            decimal? lower = null;
            decimal? stdDev = null;

            if (i >= period - 1)
            {
                var windowStart = i - period + 1;
                decimal sum = 0;
                for (var j = windowStart; j <= i; j++)
                {
                    sum += ordered[j].Close;
                }

                var mean = sum / period;
                decimal varianceSum = 0;
                for (var j = windowStart; j <= i; j++)
                {
                    var delta = ordered[j].Close - mean;
                    varianceSum += delta * delta;
                }

                var variance = varianceSum / period;
                stdDev = decimal.Round((decimal)Math.Sqrt((double)variance), 4);
                middle = decimal.Round(mean, 4);
                upper = decimal.Round(middle.Value + (standardDeviationMultiplier * stdDev.Value), 4);
                lower = decimal.Round(middle.Value - (standardDeviationMultiplier * stdDev.Value), 4);
            }

            result.Add(new BollingerBandsPoint(ordered[i].Date, middle, upper, lower, stdDev));
        }

        return result;
    }

    public IReadOnlyList<ParabolicSarPoint> ComputeParabolicSar(
        IReadOnlyList<PriceBar> prices,
        decimal step = 0.02m,
        decimal maxStep = 0.20m)
    {
        if (step <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(step), "Step must be positive.");
        }

        if (maxStep < step)
        {
            throw new ArgumentOutOfRangeException(nameof(maxStep), "Maximum step must be greater than or equal to step.");
        }

        if (prices.Count == 0)
        {
            return Array.Empty<ParabolicSarPoint>();
        }

        var ordered = prices.OrderBy(p => p.Date).ToList();
        var result = new List<ParabolicSarPoint>(ordered.Count);

        result.Add(new ParabolicSarPoint(ordered[0].Date, null, null, null, null, false));
        if (ordered.Count == 1)
        {
            return result;
        }

        var isUpTrend = ordered[1].Close >= ordered[0].Close;
        var accelerationFactor = step;
        var extremePoint = isUpTrend ? Math.Max(ordered[0].High, ordered[1].High) : Math.Min(ordered[0].Low, ordered[1].Low);
        var currentSar = isUpTrend ? Math.Min(ordered[0].Low, ordered[1].Low) : Math.Max(ordered[0].High, ordered[1].High);

        result.Add(new ParabolicSarPoint(
            ordered[1].Date,
            decimal.Round(currentSar, 4),
            isUpTrend,
            decimal.Round(extremePoint, 4),
            accelerationFactor,
            false));

        for (var i = 2; i < ordered.Count; i++)
        {
            var bar = ordered[i];
            var previous = ordered[i - 1];
            var previous2 = ordered[i - 2];

            var candidateSar = currentSar + (accelerationFactor * (extremePoint - currentSar));

            if (isUpTrend)
            {
                candidateSar = Math.Min(candidateSar, previous.Low);
                candidateSar = Math.Min(candidateSar, previous2.Low);
            }
            else
            {
                candidateSar = Math.Max(candidateSar, previous.High);
                candidateSar = Math.Max(candidateSar, previous2.High);
            }

            var isReversal = false;
            if (isUpTrend && bar.Low < candidateSar)
            {
                isReversal = true;
                isUpTrend = false;
                candidateSar = extremePoint;
                extremePoint = bar.Low;
                accelerationFactor = step;
            }
            else if (!isUpTrend && bar.High > candidateSar)
            {
                isReversal = true;
                isUpTrend = true;
                candidateSar = extremePoint;
                extremePoint = bar.High;
                accelerationFactor = step;
            }
            else
            {
                if (isUpTrend && bar.High > extremePoint)
                {
                    extremePoint = bar.High;
                    accelerationFactor = Math.Min(accelerationFactor + step, maxStep);
                }
                else if (!isUpTrend && bar.Low < extremePoint)
                {
                    extremePoint = bar.Low;
                    accelerationFactor = Math.Min(accelerationFactor + step, maxStep);
                }
            }

            currentSar = candidateSar;
            result.Add(new ParabolicSarPoint(
                bar.Date,
                decimal.Round(currentSar, 4),
                isUpTrend,
                decimal.Round(extremePoint, 4),
                decimal.Round(accelerationFactor, 4),
                isReversal));
        }

        return result;
    }

    public IReadOnlyList<EnrichedPriceBar> EnrichWithTechnicalIndicators(
        IReadOnlyList<PriceBar> prices,
        IndicatorComputationSettings? settings = null)
    {
        settings ??= IndicatorComputationSettings.Default;
        if (prices.Count == 0)
        {
            return Array.Empty<EnrichedPriceBar>();
        }

        var ordered = prices.OrderBy(p => p.Date).ToList();
        var computedIndicators = Compute(ordered, settings);
        var bollinger = ComputeBollingerBands(ordered, settings.BollingerPeriod, settings.BollingerStdDev);
        var parabolicSar = ComputeParabolicSar(ordered, settings.ParabolicSarStep, settings.ParabolicSarMax);
        var result = new List<EnrichedPriceBar>(ordered.Count);

        for (var i = 0; i < ordered.Count; i++)
        {
            result.Add(new EnrichedPriceBar(ordered[i], computedIndicators[i], bollinger[i], parabolicSar[i]));
        }

        return result;
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

        decimal seedTotal = 0;
        for (var i = seedIndex - period + 1; i <= seedIndex; i++)
        {
            seedTotal += series[i]!.Value;
        }

        var multiplier = 2m / (period + 1m);
        var previous = seedTotal / period;
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
