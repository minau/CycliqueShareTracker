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
        var breakdownByDate = BuildSignalBreakdownByDate(computedByDate);
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
                var breakdown = breakdownByDate[price.Date];
                return new DashboardChartPoint(
                    price.Date,
                    price.Open,
                    price.High,
                    price.Low,
                    price.Close,
                    indicator?.Sma50,
                    indicator?.Sma200,
                    indicator?.Rsi14,
                    indicator?.MacdLine,
                    indicator?.MacdSignalLine,
                    indicator?.MacdHistogram,
                    signal?.Score,
                    breakdown.Entry.PrimaryReason,
                    breakdown.Entry.ScoreFactors,
                    signal?.SignalLabel,
                    signal?.ExitScore,
                    breakdown.Exit.PrimaryExitReason,
                    breakdown.Exit.ScoreFactors,
                    signal?.ExitSignalLabel);
            })
            .ToList();

        IReadOnlyList<ScoreFactorDetail> entryFactors = Array.Empty<ScoreFactorDetail>();
        IReadOnlyList<ScoreFactorDetail> exitFactors = Array.Empty<ScoreFactorDetail>();
        string? entryPrimaryReason = null;
        string? exitPrimaryReason = latestSignal?.ExitPrimaryReason;
        if (latestPrice is not null && breakdownByDate.TryGetValue(latestPrice.Date, out var latestBreakdown))
        {
            entryFactors = latestBreakdown.Entry.ScoreFactors;
            exitFactors = latestBreakdown.Exit.ScoreFactors;
            entryPrimaryReason = latestBreakdown.Entry.PrimaryReason;
            exitPrimaryReason = latestBreakdown.Exit.PrimaryExitReason;
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
            latestIndicator?.MacdLine,
            latestIndicator?.MacdSignalLine,
            latestIndicator?.MacdHistogram,
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
        var breakdownByDate = BuildSignalBreakdownByDate(computedByDate);

        return recentPrices
            .OrderByDescending(x => x.Date)
            .Select(price =>
            {
                indicatorByDate.TryGetValue(price.Date, out var indicator);
                signalByDate.TryGetValue(price.Date, out var signal);
                var breakdown = breakdownByDate[price.Date];
                return new SignalHistoryRow(
                    price.Date,
                    price.Close,
                    indicator?.Sma50,
                    indicator?.Sma200,
                    indicator?.Rsi14,
                    indicator?.Drawdown52WeeksPercent,
                    signal?.Score,
                    signal?.SignalLabel,
                    breakdown.Entry.PrimaryReason,
                    breakdown.Entry.ScoreFactors,
                    signal?.ExitScore,
                    signal?.ExitSignalLabel,
                    breakdown.Exit.PrimaryExitReason,
                    breakdown.Exit.ScoreFactors);
            })
            .ToList();
    }

    private Dictionary<DateOnly, (SignalResult Entry, ExitSignalResult Exit)> BuildSignalBreakdownByDate(
        IReadOnlyDictionary<DateOnly, ComputedIndicator> computedByDate)
    {
        var breakdown = new Dictionary<DateOnly, (SignalResult Entry, ExitSignalResult Exit)>(computedByDate.Count);
        ComputedIndicator? previous = null;

        foreach (var current in computedByDate.Values.OrderBy(x => x.Date))
        {
            var entrySignal = _signalService.BuildSignal(current);
            var exitSignal = _exitSignalService.BuildExitSignal(current, previous);
            breakdown[current.Date] = (entrySignal, exitSignal);
            previous = current;
        }

        return breakdown;
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
            decimal? previousClose = i > 0 ? ordered[i - 1].Close : null;
            decimal? previousMacdHistogram = null;
            if (i > 0)
            {
                indicatorByDate.TryGetValue(ordered[i - 1].Date, out var previousIndicator);
                previousMacdHistogram = previousIndicator?.MacdHistogram;
            }

            result[current.Date] = new ComputedIndicator(
                current.Date,
                indicator?.Sma50,
                indicator?.Sma200,
                indicator?.Rsi14,
                indicator?.Drawdown52WeeksPercent,
                current.Close,
                previousClose,
                indicator?.MacdLine,
                indicator?.MacdSignalLine,
                indicator?.MacdHistogram,
                previousMacdHistogram);
        }

        return result;
    }
}
