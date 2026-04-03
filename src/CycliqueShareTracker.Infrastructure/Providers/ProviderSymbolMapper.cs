using Microsoft.Extensions.Options;

namespace CycliqueShareTracker.Infrastructure.Providers;

public sealed class ProviderSymbolMapper
{
    private readonly MarketDataOptions _options;

    public ProviderSymbolMapper(IOptions<MarketDataOptions> options)
    {
        _options = options.Value;
    }

    public string Resolve(string providerName, string requestedSymbol)
    {
        var normalizedRequested = requestedSymbol.Trim().ToUpperInvariant();

        var mapped = TryResolveFromMap(providerName, requestedSymbol)
            ?? TryResolveFromMap(providerName, normalizedRequested)
            ?? TryResolveByLegacySuffix(providerName, normalizedRequested);

        return string.IsNullOrWhiteSpace(mapped) ? normalizedRequested : mapped.Trim();
    }

    private string? TryResolveFromMap(string providerName, string symbolKey)
    {
        if (!_options.SymbolMap.TryGetValue(symbolKey, out var map))
        {
            return null;
        }

        return providerName switch
        {
            YahooFinanceDataProvider.ProviderName => map.YahooFinance,
            AlphaVantageDataProvider.ProviderName => map.AlphaVantage,
            _ => null
        };
    }

    private string? TryResolveByLegacySuffix(string providerName, string normalizedSymbol)
    {
        if (!normalizedSymbol.EndsWith(".FR", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var parisSymbol = normalizedSymbol[..^3] + ".PA";
        return TryResolveFromMap(providerName, parisSymbol) ?? parisSymbol;
    }
}
