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
        if (_options.SymbolMap.TryGetValue(requestedSymbol, out var map))
        {
            var mapped = providerName switch
            {
                YahooFinanceDataProvider.ProviderName => map.YahooFinance,
                AlphaVantageDataProvider.ProviderName => map.AlphaVantage,
                _ => null
            };

            if (!string.IsNullOrWhiteSpace(mapped))
            {
                return mapped.Trim();
            }
        }

        return requestedSymbol;
    }
}
