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
    private readonly ISignalService _signalService;
    private readonly IExitSignalService _exitSignalService;
    private readonly AssetOptions _assetOptions;
    private readonly DashboardOptions _dashboardOptions;

    public DashboardService(
        IAssetRepository assetRepository,
        IPriceRepository priceRepository,
        IIndicatorRepository indicatorRepository,
        ISignalRepository signalRepository,
        ISignalService signalService,
        IExitSignalService exitSignalService,
        IOptions<AssetOptions> assetOptions,
        IOptions<DashboardOptions> dashboardOptions)
    {
        _assetRepository = assetRepository;
        _priceRepository = priceRepository;
        _indicatorRepository = indicatorRepository;
        _signalRepository = signalRepository;
        _signalService = signalService;
        _exitSignalService = exitSignalService;
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
        var computedByDate = BuildComputedIndicatorsByDate(recentPrices, recentIndicators);
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
                    price.Open,
                    price.High,
                    price.Low,
                    price.Close,
                    indicator?.Sma50,
                    indicator?.Sma200,
                    signal?.SignalLabel,
                    signal?.ExitSignalLabel);
            })
            .ToList();

        IReadOnlyList<ScoreFactorDetail> entryFactors = Array.Empty<ScoreFactorDetail>();
        IReadOnlyList<ScoreFactorDetail> exitFactors = Array.Empty<ScoreFactorDetail>();
        string? entryPrimaryReason = null;
        string? exitPrimaryReason = latestSignal?.ExitPrimaryReason;
        if (latestPrice is not null && computedByDate.TryGetValue(latestPrice.Date, out var latestComputed))
        {
            var latestEntrySignal = _signalService.BuildSignal(latestComputed);
            var previousComputed = recentPrices
                .Where(x => x.Date < latestPrice.Date)
                .OrderByDescending(x => x.Date)
                .Select(x => computedByDate[x.Date])
                .FirstOrDefault();
            var latestExitSignal = _exitSignalService.BuildExitSignal(latestComputed, previousComputed);

            entryFactors = latestEntrySignal.ScoreFactors;
            exitFactors = latestExitSignal.ScoreFactors;
            entryPrimaryReason = latestEntrySignal.PrimaryReason;
            exitPrimaryReason = latestExitSignal.PrimaryExitReason;
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
            entryPrimaryReason,
            entryFactors,
            latestSignal?.ExitScore,
            latestSignal?.ExitSignalLabel,
            exitPrimaryReason,
            exitFactors,
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
        var computedByDate = BuildComputedIndicatorsByDate(recentPrices, recentIndicators);

        return recentPrices
            .OrderByDescending(x => x.Date)
            .Select(price =>
            {
                indicatorByDate.TryGetValue(price.Date, out var indicator);
                signalByDate.TryGetValue(price.Date, out var signal);
                var currentComputed = computedByDate[price.Date];
                var previousComputed = computedByDate.Values
                    .Where(x => x.Date < price.Date)
                    .OrderByDescending(x => x.Date)
                    .FirstOrDefault();
                var entrySignal = _signalService.BuildSignal(currentComputed);
                var exitSignal = _exitSignalService.BuildExitSignal(currentComputed, previousComputed);
                return new SignalHistoryRow(
                    price.Date,
                    price.Close,
                    indicator?.Sma50,
                    indicator?.Sma200,
                    indicator?.Rsi14,
                    indicator?.Drawdown52WeeksPercent,
                    signal?.Score,
                    signal?.SignalLabel,
                    entrySignal.PrimaryReason,
                    entrySignal.ScoreFactors,
                    signal?.ExitScore,
                    signal?.ExitSignalLabel,
                    exitSignal.PrimaryExitReason,
                    exitSignal.ScoreFactors);
            })
            .ToList();
    }

    private static Dictionary<DateOnly, ComputedIndicator> BuildComputedIndicatorsByDate(
        IReadOnlyList<Domain.Entities.DailyPrice> prices,
        IReadOnlyList<Domain.Entities.DailyIndicator> indicators)
    {
        var indicatorByDate = indicators.ToDictionary(x => x.Date);
        var ordered = prices.OrderBy(x => x.Date).ToList();
        var result = new Dictionary<DateOnly, ComputedIndicator>(ordered.Count);

        for (var i = 0; i < ordered.Count; i++)
        {
            var current = ordered[i];
            indicatorByDate.TryGetValue(current.Date, out var indicator);
            var previousClose = i > 0 ? ordered[i - 1].Close : null;
            result[current.Date] = new ComputedIndicator(
                current.Date,
                indicator?.Sma50,
                indicator?.Sma200,
                indicator?.Rsi14,
                indicator?.Drawdown52WeeksPercent,
                current.Close,
                previousClose);
        }

        return result;
    }
}
