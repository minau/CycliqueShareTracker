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
    private readonly ISignalService _signalService;
    private readonly IExitSignalService _exitSignalService;
    private readonly IReadOnlyList<TrackedAssetOptions> _watchlist;
    private readonly ILogger<DataSyncService> _logger;

    public DataSyncService(
        IDataProvider dataProvider,
        IPriceRepository priceRepository,
        IIndicatorRepository indicatorRepository,
        ISignalRepository signalRepository,
        IAssetRepository assetRepository,
        IIndicatorCalculator indicatorCalculator,
        ISignalService signalService,
        IExitSignalService exitSignalService,
        IOptions<WatchlistOptions> watchlistOptions,
        ILogger<DataSyncService> logger)
    {
        _dataProvider = dataProvider;
        _priceRepository = priceRepository;
        _indicatorRepository = indicatorRepository;
        _signalRepository = signalRepository;
        _assetRepository = assetRepository;
        _indicatorCalculator = indicatorCalculator;
        _signalService = signalService;
        _exitSignalService = exitSignalService;
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

        var signals = new List<DailySignal>(computed.Count);

        for (var i = 0; i < computed.Count; i++)
        {
            var item = computed[i];
            var previous = i > 0 ? computed[i - 1] : null;
            var signal = _signalService.BuildSignal(item);
            var exitSignal = _exitSignalService.BuildExitSignal(item, previous);

            signals.Add(new DailySignal
            {
                AssetId = asset.Id,
                Date = item.Date,
                Score = signal.Score,
                SignalLabel = signal.Label,
                Explanation = signal.Explanation,
                ExitScore = exitSignal.ExitScore,
                ExitSignalLabel = exitSignal.ExitSignal,
                ExitPrimaryReason = exitSignal.PrimaryExitReason
            });
        }

        await _signalRepository.UpsertSignalsAsync(asset.Id, signals, cancellationToken);

        _logger.LogInformation("Daily update complete for {Symbol} with {Count} rows", asset.Symbol, prices.Count);
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
