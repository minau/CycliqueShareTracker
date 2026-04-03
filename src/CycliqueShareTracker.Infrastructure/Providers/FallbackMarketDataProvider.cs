using CycliqueShareTracker.Application.Interfaces;
using CycliqueShareTracker.Application.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CycliqueShareTracker.Infrastructure.Providers;

public sealed class FallbackMarketDataProvider : IDataProvider
{
    private readonly Dictionary<string, IMarketDataSource> _providers;
    private readonly MarketDataOptions _options;
    private readonly ILogger<FallbackMarketDataProvider> _logger;

    public FallbackMarketDataProvider(
        IEnumerable<IMarketDataSource> providers,
        IOptions<MarketDataOptions> options,
        ILogger<FallbackMarketDataProvider> logger)
    {
        _providers = providers.ToDictionary(p => p.Name, StringComparer.OrdinalIgnoreCase);
        _options = options.Value;
        _logger = logger;
    }

    public async Task<IReadOnlyList<PriceBar>> FetchDailyPricesAsync(string symbol, CancellationToken cancellationToken = default)
    {
        var primary = ResolveProvider(_options.PrimaryProvider);
        var fallback = ResolveProvider(_options.FallbackProvider);

        if (primary is null && fallback is null)
        {
            _logger.LogError("No market data providers are configured.");
            return Array.Empty<PriceBar>();
        }

        if (primary is not null)
        {
            var primaryData = await TryFetchAsync(primary, symbol, cancellationToken);
            if (primaryData.Count > 0)
            {
                _logger.LogInformation("Market data fetched from primary provider {Provider} with {Count} rows", primary.Name, primaryData.Count);
                return primaryData;
            }
        }

        if (fallback is not null && !ReferenceEquals(fallback, primary))
        {
            _logger.LogWarning("Fallback provider {Provider} activated for symbol {Symbol}", fallback.Name, symbol);
            var fallbackData = await TryFetchAsync(fallback, symbol, cancellationToken);
            if (fallbackData.Count > 0)
            {
                _logger.LogInformation("Market data fetched from fallback provider {Provider} with {Count} rows", fallback.Name, fallbackData.Count);
                return fallbackData;
            }
        }

        _logger.LogError(
            "Market data fetch failed for symbol {Symbol}. Primary={PrimaryProvider}, Fallback={FallbackProvider}",
            symbol,
            primary?.Name ?? _options.PrimaryProvider,
            fallback?.Name ?? _options.FallbackProvider);

        return Array.Empty<PriceBar>();
    }

    private IMarketDataSource? ResolveProvider(string configuredName)
    {
        if (_providers.TryGetValue(configuredName, out var provider))
        {
            return provider;
        }

        _logger.LogWarning("Provider {ProviderName} is configured but not registered", configuredName);
        return null;
    }

    private async Task<IReadOnlyList<PriceBar>> TryFetchAsync(IMarketDataSource provider, string symbol, CancellationToken cancellationToken)
    {
        try
        {
            return await provider.FetchDailyPricesAsync(symbol, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Provider {Provider} failed for symbol {Symbol}", provider.Name, symbol);
            return Array.Empty<PriceBar>();
        }
    }
}
