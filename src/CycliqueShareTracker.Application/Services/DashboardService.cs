using CycliqueShareTracker.Application.Interfaces;
using CycliqueShareTracker.Application.Models;
using Microsoft.Extensions.Options;

namespace CycliqueShareTracker.Application.Services;

public sealed class DashboardService : IDashboardService
{
    private const int DefaultHistoryDays = 252;
    private readonly IAssetRepository _assetRepository;
    private readonly IPriceRepository _priceRepository;
    private readonly IIndicatorRepository _indicatorRepository;
    private readonly ISignalRepository _signalRepository;
    private readonly AssetOptions _assetOptions;
    private readonly DashboardOptions _dashboardOptions;

    public DashboardService(
        IAssetRepository assetRepository,
        IPriceRepository priceRepository,
        IIndicatorRepository indicatorRepository,
        ISignalRepository signalRepository,
        IOptions<AssetOptions> assetOptions,
        IOptions<DashboardOptions> dashboardOptions)
    {
        _assetRepository = assetRepository;
        _priceRepository = priceRepository;
        _indicatorRepository = indicatorRepository;
        _signalRepository = signalRepository;
        _assetOptions = assetOptions.Value;
        _dashboardOptions = dashboardOptions.Value;
    }

    public async Task<DashboardSnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default)
    {
        var asset = await _assetRepository.GetOrCreateAsync(_assetOptions.Symbol, _assetOptions.Name, _assetOptions.Market, cancellationToken);
        var latestPrice = await _priceRepository.GetLatestAsync(asset.Id, cancellationToken);
        var latestIndicator = await _indicatorRepository.GetLatestAsync(asset.Id, cancellationToken);
        var latestSignal = await _signalRepository.GetLatestAsync(asset.Id, cancellationToken);
        var historyDays = _dashboardOptions.HistoryDays > 0 ? _dashboardOptions.HistoryDays : DefaultHistoryDays;
        var recentPrices = await _priceRepository.GetPricesAsync(asset.Id, historyDays, cancellationToken);
        var recentIndicators = await _indicatorRepository.GetIndicatorsAsync(asset.Id, historyDays, cancellationToken);
        var recentSignals = await _signalRepository.GetSignalsAsync(asset.Id, historyDays, cancellationToken);

        var ordered = recentPrices.OrderByDescending(x => x.Date).ToList();
        var indicatorByDate = recentIndicators.ToDictionary(x => x.Date);
        var signalByDate = recentSignals.ToDictionary(x => x.Date);
        decimal? dayChange = null;
        if (ordered.Count >= 2 && ordered[1].Close != 0)
        {
            dayChange = ((ordered[0].Close / ordered[1].Close) - 1m) * 100m;
        }

        var chartPoints = ordered
            .OrderBy(x => x.Date)
            .Select(price =>
            {
                indicatorByDate.TryGetValue(price.Date, out var indicator);
                signalByDate.TryGetValue(price.Date, out var signal);
                return new DashboardChartPoint(
                    price.Date,
                    price.Close,
                    indicator?.Sma50,
                    indicator?.Sma200,
                    signal?.SignalLabel);
            })
            .ToList();

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
            chartPoints,
            ordered.Select(p => new PriceBar(p.Date, p.Open, p.High, p.Low, p.Close, p.Volume)).ToList());
    }

    public async Task<IReadOnlyList<SignalHistoryRow>> GetSignalHistoryAsync(CancellationToken cancellationToken = default)
    {
        var asset = await _assetRepository.GetOrCreateAsync(_assetOptions.Symbol, _assetOptions.Name, _assetOptions.Market, cancellationToken);
        var historyDays = _dashboardOptions.HistoryDays > 0 ? _dashboardOptions.HistoryDays : DefaultHistoryDays;

        var recentPrices = await _priceRepository.GetPricesAsync(asset.Id, historyDays, cancellationToken);
        var recentIndicators = await _indicatorRepository.GetIndicatorsAsync(asset.Id, historyDays, cancellationToken);
        var recentSignals = await _signalRepository.GetSignalsAsync(asset.Id, historyDays, cancellationToken);

        var indicatorByDate = recentIndicators.ToDictionary(x => x.Date);
        var signalByDate = recentSignals.ToDictionary(x => x.Date);

        return recentPrices
            .OrderByDescending(x => x.Date)
            .Select(price =>
            {
                indicatorByDate.TryGetValue(price.Date, out var indicator);
                signalByDate.TryGetValue(price.Date, out var signal);
                return new SignalHistoryRow(
                    price.Date,
                    price.Close,
                    indicator?.Sma50,
                    indicator?.Sma200,
                    indicator?.Rsi14,
                    indicator?.Drawdown52WeeksPercent,
                    signal?.Score,
                    signal?.SignalLabel);
            })
            .ToList();
    }
}
