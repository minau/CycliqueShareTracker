using CycliqueShareTracker.Application.Interfaces;
using CycliqueShareTracker.Application.Models;
using CycliqueShareTracker.Domain.Enums;

namespace CycliqueShareTracker.Application.Services;

public sealed class BacktestEngine : IBacktestEngine
{
    private readonly IIndicatorCalculator _indicatorCalculator;
    private readonly ISignalService _signalService;
    private readonly IExitSignalService _exitSignalService;

    public BacktestEngine(
        IIndicatorCalculator indicatorCalculator,
        ISignalService signalService,
        IExitSignalService exitSignalService)
    {
        _indicatorCalculator = indicatorCalculator;
        _signalService = signalService;
        _exitSignalService = exitSignalService;
    }

    public BacktestAssetResult RunForAsset(string symbol, string assetName, IReadOnlyList<PriceBar> priceBars, bool includeMacdInScoring, StrategyConfig config)
    {
        if (priceBars.Count == 0)
        {
            var emptyMetrics = BuildMetrics(Array.Empty<Trade>());
            return new BacktestAssetResult(symbol, assetName, emptyMetrics, Array.Empty<Trade>(), "Aucune donnée OHLC disponible sur la période demandée.");
        }

        var orderedBars = priceBars.OrderBy(x => x.Date).ToList();
        var computed = _indicatorCalculator.Compute(orderedBars);
        var trades = new List<Trade>();

        ComputedIndicator? previousIndicator = null;
        OpenPosition? openPosition = null;
        var barsSinceLastBuy = int.MaxValue;
        var barsSinceLastSell = int.MaxValue;

        for (var i = 0; i < computed.Count; i++)
        {
            var currentIndicator = computed[i];
            var bar = orderedBars[i];

            var entrySignal = _signalService.BuildSignal(currentIndicator, includeMacdInScoring, previousIndicator, config);
            var exitSignal = _exitSignalService.BuildExitSignal(currentIndicator, previousIndicator, includeMacdInScoring, config);

            if (openPosition is null)
            {
                barsSinceLastBuy++;

                if (entrySignal.Label == SignalLabel.BuyZone &&
                    entrySignal.Score >= config.BuyScoreThreshold &&
                    barsSinceLastBuy >= config.MinimumBarsBetweenSameSignal)
                {
                    openPosition = new OpenPosition(bar.Date, bar.Close, entrySignal.PrimaryReason);
                    barsSinceLastBuy = 0;
                }
            }
            else
            {
                barsSinceLastSell++;
                var shouldSell = exitSignal.ExitSignal == ExitSignalLabel.SellZone && exitSignal.ExitScore >= config.SellScoreThreshold;

                if (!shouldSell && config.EarlySellEnabled)
                {
                    shouldSell = exitSignal.ExitScore >= config.EarlySellWeaknessScoreThreshold;
                }

                if (shouldSell && barsSinceLastSell >= config.MinimumBarsBetweenSameSignal)
                {
                    trades.Add(BuildTrade(symbol, openPosition, bar, exitSignal.PrimaryExitReason, config.FeePercentPerSide));
                    openPosition = null;
                    barsSinceLastSell = 0;
                }
            }

            previousIndicator = currentIndicator;
        }

        if (openPosition is not null)
        {
            var lastBar = orderedBars[^1];
            trades.Add(BuildTrade(symbol, openPosition, lastBar, "Sortie forcée en fin de période de backtest.", config.FeePercentPerSide));
        }

        var metrics = BuildMetrics(trades);
        return new BacktestAssetResult(symbol, assetName, metrics, trades);
    }

    private static Trade BuildTrade(string symbol, OpenPosition openPosition, PriceBar exitBar, string exitReason, decimal feePercentPerSide)
    {
        var entryCost = openPosition.EntryPrice * (1m + (feePercentPerSide / 100m));
        var exitProceed = exitBar.Close * (1m - (feePercentPerSide / 100m));
        var performance = entryCost == 0m ? 0m : ((exitProceed / entryCost) - 1m) * 100m;
        var durationDays = exitBar.Date.DayNumber - openPosition.EntryDate.DayNumber;

        return new Trade(
            symbol,
            openPosition.EntryDate,
            decimal.Round(openPosition.EntryPrice, 4),
            exitBar.Date,
            decimal.Round(exitBar.Close, 4),
            decimal.Round(performance, 2),
            Math.Max(durationDays, 0),
            openPosition.EntryReason,
            exitReason);
    }

    private static BacktestMetrics BuildMetrics(IReadOnlyList<Trade> trades)
    {
        if (trades.Count == 0)
        {
            return new BacktestMetrics(0, 0, 0, 0m, 0m, 0m, 0m, 0m, 0m, 0m);
        }

        var winningTrades = trades.Count(t => t.PerformancePercent > 0m);
        var losingTrades = trades.Count(t => t.PerformancePercent < 0m);

        var gains = trades.Where(t => t.PerformancePercent > 0m).Select(t => t.PerformancePercent).ToList();
        var losses = trades.Where(t => t.PerformancePercent < 0m).Select(t => Math.Abs(t.PerformancePercent)).ToList();

        var grossGain = gains.Sum();
        var grossLoss = losses.Sum();

        var winRate = (decimal)winningTrades / trades.Count * 100m;
        var averageGain = gains.Count == 0 ? 0m : gains.Average();
        var averageLoss = losses.Count == 0 ? 0m : losses.Average();
        var profitFactor = grossLoss == 0m ? grossGain : grossGain / grossLoss;

        decimal equity = 1m;
        decimal peak = 1m;
        decimal maxDrawdown = 0m;

        foreach (var trade in trades)
        {
            equity *= 1m + (trade.PerformancePercent / 100m);
            if (equity > peak)
            {
                peak = equity;
            }

            if (peak > 0)
            {
                var drawdown = ((equity / peak) - 1m) * 100m;
                if (drawdown < maxDrawdown)
                {
                    maxDrawdown = drawdown;
                }
            }
        }

        var totalPerformance = (equity - 1m) * 100m;
        var averageDuration = trades.Average(t => t.DurationDays);

        return new BacktestMetrics(
            trades.Count,
            winningTrades,
            losingTrades,
            decimal.Round(winRate, 2),
            decimal.Round(averageGain, 2),
            decimal.Round(averageLoss, 2),
            decimal.Round(profitFactor, 2),
            decimal.Round(totalPerformance, 2),
            decimal.Round(Math.Abs(maxDrawdown), 2),
            decimal.Round((decimal)averageDuration, 2));
    }

    private sealed record OpenPosition(DateOnly EntryDate, decimal EntryPrice, string EntryReason);
}
