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
    private readonly IIndicatorCalculator _indicatorCalculator;
    private readonly AssetOptions _assetOptions;
    private readonly DashboardOptions _dashboardOptions;

    public DashboardService(
        IAssetRepository assetRepository,
        IPriceRepository priceRepository,
        IIndicatorRepository indicatorRepository,
        ISignalRepository signalRepository,
        ISignalService signalService,
        IExitSignalService exitSignalService,
        IIndicatorCalculator indicatorCalculator,
        IOptions<AssetOptions> assetOptions,
        IOptions<DashboardOptions> dashboardOptions)
    {
        _assetRepository = assetRepository;
        _priceRepository = priceRepository;
        _indicatorRepository = indicatorRepository;
        _signalRepository = signalRepository;
        _signalService = signalService;
        _exitSignalService = exitSignalService;
        _indicatorCalculator = indicatorCalculator;
        _assetOptions = assetOptions.Value;
        _dashboardOptions = dashboardOptions.Value;
    }

    public async Task<DashboardSnapshot> GetSnapshotAsync(bool includeMacdInScoring = true, CancellationToken cancellationToken = default)
    {
        var asset = await _assetRepository.GetOrCreateAsync(_assetOptions.Symbol, _assetOptions.Name, _assetOptions.Market, cancellationToken);
        var latestPrice = await _priceRepository.GetLatestAsync(asset.Id, cancellationToken);
        var latestIndicator = await _indicatorRepository.GetLatestAsync(asset.Id, cancellationToken);
        var latestSignal = await _signalRepository.GetLatestAsync(asset.Id, cancellationToken);
        var historyDays = _dashboardOptions.HistoryDays > 0 ? _dashboardOptions.HistoryDays : DefaultHistoryDays;
        var recentPrices = await _priceRepository.GetPricesAsync(asset.Id, historyDays, cancellationToken);
        var recentIndicators = await _indicatorRepository.GetIndicatorsAsync(asset.Id, historyDays, cancellationToken);
        var recentSignals = await _signalRepository.GetSignalsAsync(asset.Id, historyDays, cancellationToken);
        var orderedIndicatorsDesc = recentIndicators.OrderByDescending(x => x.Date).ToList();
        var computedFromPrices = _indicatorCalculator.Compute(
            recentPrices
                .Select(p => new PriceBar(p.Date, p.Open, p.High, p.Low, p.Close, p.Volume))
                .OrderBy(p => p.Date)
                .ToList());

        var latestMacdLine = orderedIndicatorsDesc
            .Select(x => x.MacdLine)
            .FirstOrDefault(x => x.HasValue);
        var latestMacdSignalLine = orderedIndicatorsDesc
            .Select(x => x.MacdSignalLine)
            .FirstOrDefault(x => x.HasValue);
        var latestMacdHistogram = orderedIndicatorsDesc
            .Select(x => x.MacdHistogram)
            .FirstOrDefault(x => x.HasValue);
        var latestComputedWithMacd = computedFromPrices
            .OrderByDescending(x => x.Date)
            .FirstOrDefault(x => x.MacdLine.HasValue || x.MacdSignalLine.HasValue || x.MacdHistogram.HasValue);

        latestMacdLine ??= latestComputedWithMacd?.MacdLine;
        latestMacdSignalLine ??= latestComputedWithMacd?.MacdSignalLine;
        latestMacdHistogram ??= latestComputedWithMacd?.MacdHistogram;

        var ordered = recentPrices.OrderByDescending(x => x.Date).ToList();
        var indicatorByDate = recentIndicators.ToDictionary(x => x.Date);
        var signalByDate = recentSignals.ToDictionary(x => x.Date);
        var computedByDate = BuildComputedIndicatorsByDate(recentPrices, recentIndicators);
        var breakdownByDate = BuildSignalBreakdownByDate(computedByDate, includeMacdInScoring);
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
                    includeMacdInScoring ? signal?.Score ?? breakdown.Entry.Score : breakdown.Entry.Score,
                    breakdown.Entry.PrimaryReason,
                    breakdown.Entry.ScoreFactors,
                    includeMacdInScoring ? signal?.SignalLabel ?? breakdown.Entry.Label : breakdown.Entry.Label,
                    includeMacdInScoring ? signal?.ExitScore ?? breakdown.Exit.ExitScore : breakdown.Exit.ExitScore,
                    breakdown.Exit.PrimaryExitReason,
                    breakdown.Exit.ScoreFactors,
                    includeMacdInScoring ? signal?.ExitSignalLabel ?? breakdown.Exit.ExitSignal : breakdown.Exit.ExitSignal);
            })
            .ToList();

        IReadOnlyList<ScoreFactorDetail> entryFactors = Array.Empty<ScoreFactorDetail>();
        IReadOnlyList<ScoreFactorDetail> exitFactors = Array.Empty<ScoreFactorDetail>();
        string? entryPrimaryReason = null;
        string? exitPrimaryReason = latestSignal?.ExitPrimaryReason;
        int? latestEntryScore = includeMacdInScoring ? latestSignal?.Score : null;
        Domain.Enums.SignalLabel? latestEntryLabel = includeMacdInScoring ? latestSignal?.SignalLabel : null;
        int? latestExitScore = includeMacdInScoring ? latestSignal?.ExitScore : null;
        Domain.Enums.ExitSignalLabel? latestExitLabel = includeMacdInScoring ? latestSignal?.ExitSignalLabel : null;
        if (latestPrice is not null && breakdownByDate.TryGetValue(latestPrice.Date, out var latestBreakdown))
        {
            entryFactors = latestBreakdown.Entry.ScoreFactors;
            exitFactors = latestBreakdown.Exit.ScoreFactors;
            entryPrimaryReason = latestBreakdown.Entry.PrimaryReason;
            exitPrimaryReason = latestBreakdown.Exit.PrimaryExitReason;
            latestEntryScore ??= latestBreakdown.Entry.Score;
            latestEntryLabel ??= latestBreakdown.Entry.Label;
            latestExitScore ??= latestBreakdown.Exit.ExitScore;
            latestExitLabel ??= latestBreakdown.Exit.ExitSignal;
        }

        return new DashboardSnapshot(
            asset.Symbol,
            asset.Name,
            latestPrice?.Date,
            latestPrice?.Close,
            dayChange,
            latestIndicator?.Sma50,
            latestIndicator?.Sma200,
            latestIndicator?.Ema12,
            latestIndicator?.Ema26,
            latestIndicator?.Rsi14,
            latestIndicator?.Drawdown52WeeksPercent,
            latestIndicator?.MacdLine ?? latestMacdLine,
            latestIndicator?.MacdSignalLine ?? latestMacdSignalLine,
            latestIndicator?.MacdHistogram ?? latestMacdHistogram,
            latestEntryScore,
            latestEntryLabel,
            entryPrimaryReason,
            entryFactors,
            latestExitScore,
            latestExitLabel,
            exitPrimaryReason,
            exitFactors,
            chartPoints,
            ordered.Select(p => new PriceBar(p.Date, p.Open, p.High, p.Low, p.Close, p.Volume)).ToList());
    }

    public async Task<IReadOnlyList<SignalHistoryRow>> GetSignalHistoryAsync(bool includeMacdInScoring = true, CancellationToken cancellationToken = default)
    {
        var asset = await _assetRepository.GetOrCreateAsync(_assetOptions.Symbol, _assetOptions.Name, _assetOptions.Market, cancellationToken);
        var historyDays = _dashboardOptions.HistoryDays > 0 ? _dashboardOptions.HistoryDays : DefaultHistoryDays;

        var recentPrices = await _priceRepository.GetPricesAsync(asset.Id, historyDays, cancellationToken);
        var recentIndicators = await _indicatorRepository.GetIndicatorsAsync(asset.Id, historyDays, cancellationToken);
        var recentSignals = await _signalRepository.GetSignalsAsync(asset.Id, historyDays, cancellationToken);

        var indicatorByDate = recentIndicators.ToDictionary(x => x.Date);
        var signalByDate = recentSignals.ToDictionary(x => x.Date);
        var computedByDate = BuildComputedIndicatorsByDate(recentPrices, recentIndicators);
        var breakdownByDate = BuildSignalBreakdownByDate(computedByDate, includeMacdInScoring);

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
                    includeMacdInScoring ? signal?.Score ?? breakdown.Entry.Score : breakdown.Entry.Score,
                    includeMacdInScoring ? signal?.SignalLabel ?? breakdown.Entry.Label : breakdown.Entry.Label,
                    breakdown.Entry.PrimaryReason,
                    breakdown.Entry.ScoreFactors,
                    includeMacdInScoring ? signal?.ExitScore ?? breakdown.Exit.ExitScore : breakdown.Exit.ExitScore,
                    includeMacdInScoring ? signal?.ExitSignalLabel ?? breakdown.Exit.ExitSignal : breakdown.Exit.ExitSignal,
                    breakdown.Exit.PrimaryExitReason,
                    breakdown.Exit.ScoreFactors);
            })
            .ToList();
    }

    private Dictionary<DateOnly, (SignalResult Entry, ExitSignalResult Exit)> BuildSignalBreakdownByDate(
        IReadOnlyDictionary<DateOnly, ComputedIndicator> computedByDate,
        bool includeMacdInScoring)
    {
        var breakdown = new Dictionary<DateOnly, (SignalResult Entry, ExitSignalResult Exit)>(computedByDate.Count);
        ComputedIndicator? previous = null;

        foreach (var current in computedByDate.Values.OrderBy(x => x.Date))
        {
            var entrySignal = _signalService.BuildSignal(current, includeMacdInScoring);
            var exitSignal = _exitSignalService.BuildExitSignal(current, previous, includeMacdInScoring);
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
                previousMacdHistogram,
                indicator?.Ema12,
                indicator?.Ema26);
        }

        return result;
    }
}
