using CycliqueShareTracker.Application.Interfaces;
using CycliqueShareTracker.Application.Models;
using CycliqueShareTracker.Application.Trading;
using Microsoft.Extensions.Options;

namespace CycliqueShareTracker.Application.Services;

public sealed class BacktestService : IBacktestService
{
    private readonly IAssetRepository _assetRepository;
    private readonly IPriceRepository _priceRepository;
    private readonly IIndicatorCalculator _indicatorCalculator;
    private readonly IReadOnlyList<TrackedAssetOptions> _watchlist;
    private readonly BacktestEngine _engine = new();

    public BacktestService(
        IAssetRepository assetRepository,
        IPriceRepository priceRepository,
        IIndicatorCalculator indicatorCalculator,
        IOptions<WatchlistOptions> watchlistOptions)
    {
        _assetRepository = assetRepository;
        _priceRepository = priceRepository;
        _indicatorCalculator = indicatorCalculator;
        _watchlist = WatchlistOptions.BuildTrackedAssets(watchlistOptions.Value.Assets);
    }

    public IReadOnlyList<string> GetTrackedSymbols()
        => _watchlist.Select(x => x.Symbol).ToList();

    public async Task<BacktestResult> RunAsync(BacktestParameters parameters, CancellationToken cancellationToken = default)
    {
        if (parameters.EndDate < parameters.StartDate)
        {
            var safeParameters = parameters with { StartDate = parameters.EndDate, EndDate = parameters.StartDate };
            return await RunAsync(safeParameters, cancellationToken);
        }

        var trackedAsset = ResolveTrackedAsset(parameters.Symbol);
        var asset = await _assetRepository.GetOrCreateAsync(trackedAsset.Symbol, trackedAsset.Name, trackedAsset.Market, cancellationToken);
        var prices = await _priceRepository.GetPricesInRangeAsync(asset.Id, parameters.StartDate, parameters.EndDate, cancellationToken);
        var bars = prices
            .OrderBy(x => x.Date)
            .Select(x => new PriceBar(x.Date, x.Open, x.High, x.Low, x.Close, x.Volume))
            .ToList();
        var indicators = _indicatorCalculator.Compute(bars);

        return _engine.Run(parameters, bars, indicators);
    }

    private TrackedAssetOptions ResolveTrackedAsset(string symbol)
    {
        if (_watchlist.Count == 0)
        {
            throw new InvalidOperationException("Watchlist configuration is empty.");
        }

        if (string.IsNullOrWhiteSpace(symbol))
        {
            return _watchlist[0];
        }

        return _watchlist.FirstOrDefault(asset => string.Equals(asset.Symbol, symbol, StringComparison.OrdinalIgnoreCase))
               ?? throw new InvalidOperationException($"Symbol '{symbol}' is not configured in the watchlist.");
    }
}
