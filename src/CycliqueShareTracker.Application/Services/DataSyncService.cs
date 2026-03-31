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
    private readonly AssetOptions _assetOptions;
    private readonly ILogger<DataSyncService> _logger;

    public DataSyncService(
        IDataProvider dataProvider,
        IPriceRepository priceRepository,
        IIndicatorRepository indicatorRepository,
        ISignalRepository signalRepository,
        IAssetRepository assetRepository,
        IIndicatorCalculator indicatorCalculator,
        ISignalService signalService,
        IOptions<AssetOptions> assetOptions,
        ILogger<DataSyncService> logger)
    {
        _dataProvider = dataProvider;
        _priceRepository = priceRepository;
        _indicatorRepository = indicatorRepository;
        _signalRepository = signalRepository;
        _assetRepository = assetRepository;
        _indicatorCalculator = indicatorCalculator;
        _signalService = signalService;
        _assetOptions = assetOptions.Value;
        _logger = logger;
    }

    public async Task RunDailyUpdateAsync(CancellationToken cancellationToken = default)
    {
        var asset = await _assetRepository.GetOrCreateAsync(_assetOptions.Symbol, _assetOptions.Name, _assetOptions.Market, cancellationToken);

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

        var indicators = computed.Select(item => new DailyIndicator
        {
            AssetId = asset.Id,
            Date = item.Date,
            Sma50 = item.Sma50,
            Sma200 = item.Sma200,
            Rsi14 = item.Rsi14,
            Drawdown52WeeksPercent = item.Drawdown52WeeksPercent
        }).ToList();

        await _indicatorRepository.UpsertIndicatorsAsync(asset.Id, indicators, cancellationToken);

        var signals = computed.Select(item =>
        {
            var signal = _signalService.BuildSignal(item);
            return new DailySignal
            {
                AssetId = asset.Id,
                Date = item.Date,
                Score = signal.Score,
                SignalLabel = signal.Label,
                Explanation = signal.Explanation
            };
        }).ToList();

        await _signalRepository.UpsertSignalsAsync(asset.Id, signals, cancellationToken);

        _logger.LogInformation("Daily update complete for {Symbol} with {Count} rows", asset.Symbol, prices.Count);
    }
}
