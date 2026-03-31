using CycliqueShareTracker.Application.Interfaces;
using CycliqueShareTracker.Application.Models;
using Microsoft.Extensions.Options;

namespace CycliqueShareTracker.Application.Services;

public sealed class DashboardService : IDashboardService
{
    private readonly IAssetRepository _assetRepository;
    private readonly IPriceRepository _priceRepository;
    private readonly IIndicatorRepository _indicatorRepository;
    private readonly ISignalRepository _signalRepository;
    private readonly AssetOptions _assetOptions;

    public DashboardService(
        IAssetRepository assetRepository,
        IPriceRepository priceRepository,
        IIndicatorRepository indicatorRepository,
        ISignalRepository signalRepository,
        IOptions<AssetOptions> assetOptions)
    {
        _assetRepository = assetRepository;
        _priceRepository = priceRepository;
        _indicatorRepository = indicatorRepository;
        _signalRepository = signalRepository;
        _assetOptions = assetOptions.Value;
    }

    public async Task<DashboardSnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default)
    {
        var asset = await _assetRepository.GetOrCreateAsync(_assetOptions.Symbol, _assetOptions.Name, _assetOptions.Market, cancellationToken);
        var latestPrice = await _priceRepository.GetLatestAsync(asset.Id, cancellationToken);
        var latestIndicator = await _indicatorRepository.GetLatestAsync(asset.Id, cancellationToken);
        var latestSignal = await _signalRepository.GetLatestAsync(asset.Id, cancellationToken);
        var recentPrices = await _priceRepository.GetPricesAsync(asset.Id, 20, cancellationToken);

        var ordered = recentPrices.OrderByDescending(x => x.Date).ToList();
        decimal? dayChange = null;
        if (ordered.Count >= 2 && ordered[1].Close != 0)
        {
            dayChange = ((ordered[0].Close / ordered[1].Close) - 1m) * 100m;
        }

        return new DashboardSnapshot(
            asset.Symbol,
            asset.Name,
            latestPrice?.Date,
            latestPrice?.Close,
            dayChange,
            latestIndicator?.Sma50,
            latestIndicator?.Sma200,
            latestIndicator?.Rsi14,
            latestIndicator?.Drawdown52WeeksPercent,
            latestSignal?.Score,
            latestSignal?.SignalLabel,
            ordered.Select(p => new PriceBar(p.Date, p.Open, p.High, p.Low, p.Close, p.Volume)).ToList());
    }
}
