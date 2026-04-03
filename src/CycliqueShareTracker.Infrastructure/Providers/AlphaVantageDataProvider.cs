using System.Globalization;
using System.Text.Json;
using CycliqueShareTracker.Application.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CycliqueShareTracker.Infrastructure.Providers;

public sealed class AlphaVantageDataProvider : IMarketDataSource
{
    public const string ProviderName = "AlphaVantage";

    private readonly HttpClient _httpClient;
    private readonly ProviderSymbolMapper _symbolMapper;
    private readonly MarketDataOptions _options;
    private readonly ILogger<AlphaVantageDataProvider> _logger;

    public AlphaVantageDataProvider(
        HttpClient httpClient,
        ProviderSymbolMapper symbolMapper,
        IOptions<MarketDataOptions> options,
        ILogger<AlphaVantageDataProvider> logger)
    {
        _httpClient = httpClient;
        _symbolMapper = symbolMapper;
        _options = options.Value;
        _logger = logger;
    }

    public string Name => ProviderName;

    public async Task<IReadOnlyList<PriceBar>> FetchDailyPricesAsync(string symbol, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_options.AlphaVantage.ApiKey))
        {
            _logger.LogWarning("Alpha Vantage API key is missing. Set MarketData__AlphaVantage__ApiKey to enable fallback provider.");
            return Array.Empty<PriceBar>();
        }

        var providerSymbol = _symbolMapper.Resolve(Name, symbol);
        var url = $"https://www.alphavantage.co/query?function=TIME_SERIES_DAILY_ADJUSTED&symbol={providerSymbol}&outputsize=full&apikey={Uri.EscapeDataString(_options.AlphaVantage.ApiKey)}";

        using var response = await _httpClient.GetAsync(url, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("Alpha Vantage returned HTTP {StatusCode} for symbol {Symbol}", (int)response.StatusCode, providerSymbol);
            return Array.Empty<PriceBar>();
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var json = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

        var bars = ParseBars(json.RootElement);
        var validated = PriceBarValidator.ValidateAndNormalize(bars);

        if (validated.Count == 0 && bars.Count > 0)
        {
            _logger.LogWarning("Alpha Vantage returned invalid data for symbol {Symbol}", providerSymbol);
        }

        _logger.LogInformation("Alpha Vantage returned {Count} rows for requested symbol {RequestedSymbol} (provider symbol {ProviderSymbol})", validated.Count, symbol, providerSymbol);
        return validated;
    }

    private static IReadOnlyList<PriceBar> ParseBars(JsonElement root)
    {
        if (!root.TryGetProperty("Time Series (Daily)", out var series) || series.ValueKind != JsonValueKind.Object)
        {
            return Array.Empty<PriceBar>();
        }

        var bars = new List<PriceBar>();

        foreach (var day in series.EnumerateObject())
        {
            if (!DateOnly.TryParse(day.Name, out var date)) continue;

            var item = day.Value;
            if (!TryReadDecimal(item, "1. open", out var open)) continue;
            if (!TryReadDecimal(item, "2. high", out var high)) continue;
            if (!TryReadDecimal(item, "3. low", out var low)) continue;
            if (!TryReadDecimal(item, "4. close", out var close)) continue;

            _ = TryReadLong(item, "6. volume", out var volume);
            var adjustedClose = TryReadDecimal(item, "5. adjusted close", out var adj) ? (decimal?)adj : (decimal?)null;

            bars.Add(new PriceBar(date, open, high, low, close, volume, adjustedClose));
        }

        return bars;
    }

    private static bool TryReadDecimal(JsonElement root, string propertyName, out decimal value)
    {
        value = default;
        if (!root.TryGetProperty(propertyName, out var property) || property.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        return decimal.TryParse(property.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out value);
    }

    private static bool TryReadLong(JsonElement root, string propertyName, out long value)
    {
        value = 0;
        if (!root.TryGetProperty(propertyName, out var property) || property.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        return long.TryParse(property.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out value);
    }
}
