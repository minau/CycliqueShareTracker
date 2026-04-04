using CycliqueShareTracker.Application.Interfaces;
using CycliqueShareTracker.Application.Models;
using CycliqueShareTracker.Domain.Entities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CycliqueShareTracker.Application.Services;

public sealed class DataSyncService : IDataSyncService
{
    private readonly IDataProvider _dataProvider;
    private readonly IPriceRepository _priceRepository;
    private readonly IIndicatorRepository _indicatorRepository;
    private readonly ISignalRepository _signalRepository;
    private readonly IAssetRepository _assetRepository;
    private readonly IIndicatorCalculator _indicatorCalculator;
    private readonly ISignalTimelineService _signalTimelineService;
    private readonly IReadOnlyList<TrackedAssetOptions> _watchlist;
    private readonly ILogger<DataSyncService> _logger;

    public DataSyncService(
        IDataProvider dataProvider,
        IPriceRepository priceRepository,
        IIndicatorRepository indicatorRepository,
        ISignalRepository signalRepository,
        IAssetRepository assetRepository,
        IIndicatorCalculator indicatorCalculator,
        ISignalTimelineService signalTimelineService,
        IOptions<WatchlistOptions> watchlistOptions,
        ILogger<DataSyncService> logger)
    {
        _dataProvider = dataProvider;
        _priceRepository = priceRepository;
        _indicatorRepository = indicatorRepository;
        _signalRepository = signalRepository;
        _assetRepository = assetRepository;
        _indicatorCalculator = indicatorCalculator;
        _signalTimelineService = signalTimelineService;
        _watchlist = BuildWatchlist(watchlistOptions.Value.Assets);
        _logger = logger;
    }

    public async Task RunDailyUpdateAsync(CancellationToken cancellationToken = default)
    {
        foreach (var trackedAsset in _watchlist)
        {
            try
            {
                await SyncAssetAsync(trackedAsset, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled error while syncing {Symbol}", trackedAsset.Symbol);
            }
        }
    }

    private async Task SyncAssetAsync(TrackedAssetOptions trackedAsset, CancellationToken cancellationToken)
    {
        var asset = await _assetRepository.GetOrCreateAsync(trackedAsset.Symbol, trackedAsset.Name, trackedAsset.Market, cancellationToken);
        IReadOnlyList<PriceBar> prices;
        try
        {
            prices = await _dataProvider.FetchDailyPricesAsync(asset.Symbol, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Provider failure while fetching daily prices for {Symbol}", asset.Symbol);
            return;
        }

        if (prices.Count == 0)
        {
            _logger.LogWarning("No prices returned by provider for {Symbol}", asset.Symbol);
            return;
        }

        await _priceRepository.UpsertDailyPricesAsync(asset.Id, prices, cancellationToken);

        var persisted = await _priceRepository.GetPricesAsync(asset.Id, 400, cancellationToken);
        var bars = persisted
            .Select(p => new PriceBar(p.Date, p.Open, p.High, p.Low, p.Close, p.Volume))
            .OrderBy(p => p.Date)
            .ToList();

        var computed = _indicatorCalculator.Compute(bars);
        if (computed.Count > 0)
        {
            var latest = computed[^1];
            if (!latest.Ema12.HasValue || !latest.Ema26.HasValue)
            {
                _logger.LogWarning(
                    "Insufficient history to compute EMA values for {Symbol}. Required at least 26 closes, got {Count}.",
                    asset.Symbol,
                    bars.Count);
            }
        }

        var indicators = computed.Select(item => new DailyIndicator
        {
            AssetId = asset.Id,
            Date = item.Date,
            Sma50 = item.Sma50,
            Sma200 = item.Sma200,
            Rsi14 = item.Rsi14,
            Drawdown52WeeksPercent = item.Drawdown52WeeksPercent,
            Ema12 = item.Ema12,
            Ema26 = item.Ema26,
            MacdLine = item.MacdLine,
            MacdSignalLine = item.MacdSignalLine,
            MacdHistogram = item.MacdHistogram
        }).ToList();

        await _indicatorRepository.UpsertIndicatorsAsync(asset.Id, indicators, cancellationToken);

        var computedWithContext = BuildComputedWithContext(computed);
        var computedByDate = computedWithContext.ToDictionary(x => x.Date);
        var timeline = _signalTimelineService.BuildSignalTimeline(computedByDate, includeMacdConfirmation: true);

        var signals = timeline.Select(item => new DailySignal
        {
            AssetId = asset.Id,
            Date = item.Key,
            Score = item.Value.Entry.Score,
            SignalLabel = item.Value.Entry.Label,
            Explanation = item.Value.Entry.Explanation,
            ExitScore = item.Value.Exit.ExitScore,
            ExitSignalLabel = item.Value.Exit.ExitSignal,
            ExitPrimaryReason = item.Value.Exit.PrimaryExitReason
        }).ToList();

        await _signalRepository.UpsertSignalsAsync(asset.Id, signals, cancellationToken);

        _logger.LogInformation("Daily update complete for {Symbol} with {Count} rows", asset.Symbol, prices.Count);
    }


    private static IReadOnlyList<ComputedIndicator> BuildComputedWithContext(IReadOnlyList<ComputedIndicator> computed)
    {
        var result = new List<ComputedIndicator>(computed.Count);
        var ordered = computed.OrderBy(x => x.Date).ToList();
        var bullishStreak = 0;
        var bearishStreak = 0;

        for (var i = 0; i < ordered.Count; i++)
        {
            var current = ordered[i];
            var previousClose = i > 0 ? ordered[i - 1].Close : (decimal?)null;
            if (previousClose.HasValue)
            {
                bullishStreak = current.Close > previousClose.Value ? bullishStreak + 1 : 0;
                bearishStreak = current.Close < previousClose.Value ? bearishStreak + 1 : 0;
            }
            else
            {
                bullishStreak = 0;
                bearishStreak = 0;
            }

            result.Add(current with
            {
                BullishStreakCount = bullishStreak,
                BearishStreakCount = bearishStreak
            });
        }

        return result;
    }
    private static IReadOnlyList<TrackedAssetOptions> BuildWatchlist(IReadOnlyList<TrackedAssetOptions>? configuredAssets)
    {
        var source = configuredAssets is { Count: > 0 }
            ? configuredAssets
            : WatchlistOptions.DefaultAssets;

        return source
            .Where(asset => !string.IsNullOrWhiteSpace(asset.Symbol))
            .GroupBy(asset => asset.Symbol.Trim(), StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToList();
    }
}
