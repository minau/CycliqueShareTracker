using System.Text.Json;
using CycliqueShareTracker.Application.Models;
using Microsoft.Extensions.Logging;

namespace CycliqueShareTracker.Infrastructure.Providers;

public sealed class YahooFinanceDataProvider : IMarketDataSource
{
    public const string ProviderName = "YahooFinance";

    private readonly HttpClient _httpClient;
    private readonly ProviderSymbolMapper _symbolMapper;
    private readonly ILogger<YahooFinanceDataProvider> _logger;

    public YahooFinanceDataProvider(
        HttpClient httpClient,
        ProviderSymbolMapper symbolMapper,
        ILogger<YahooFinanceDataProvider> logger)
    {
        _httpClient = httpClient;
        _symbolMapper = symbolMapper;
        _logger = logger;
    }

    public string Name => ProviderName;

    public async Task<IReadOnlyList<PriceBar>> FetchDailyPricesAsync(string symbol, CancellationToken cancellationToken = default)
    {
        var providerSymbol = _symbolMapper.Resolve(Name, symbol);
        var url = $"https://query1.finance.yahoo.com/v8/finance/chart/{providerSymbol}?interval=1d&range=10y&events=history&includeAdjustedClose=true";

        using var response = await _httpClient.GetAsync(url, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("Yahoo Finance returned HTTP {StatusCode} for symbol {Symbol}", (int)response.StatusCode, providerSymbol);
            return Array.Empty<PriceBar>();
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var json = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

        var bars = ParseBars(json.RootElement);
        var validated = PriceBarValidator.ValidateAndNormalize(bars);

        if (validated.Count == 0 && bars.Count > 0)
        {
            _logger.LogWarning("Yahoo Finance returned invalid data for symbol {Symbol}", providerSymbol);
        }

        _logger.LogInformation("Yahoo Finance returned {Count} rows for requested symbol {RequestedSymbol} (provider symbol {ProviderSymbol})", validated.Count, symbol, providerSymbol);
        return validated;
    }

    private static IReadOnlyList<PriceBar> ParseBars(JsonElement root)
    {
        if (!root.TryGetProperty("chart", out var chart) ||
            !chart.TryGetProperty("result", out var resultArray) ||
            resultArray.ValueKind != JsonValueKind.Array ||
            resultArray.GetArrayLength() == 0)
        {
            return Array.Empty<PriceBar>();
        }

        var result = resultArray[0];
        if (!result.TryGetProperty("timestamp", out var timestamps) || timestamps.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<PriceBar>();
        }

        if (!result.TryGetProperty("indicators", out var indicators) ||
            !indicators.TryGetProperty("quote", out var quoteArray) ||
            quoteArray.ValueKind != JsonValueKind.Array ||
            quoteArray.GetArrayLength() == 0)
        {
            return Array.Empty<PriceBar>();
        }

        var quote = quoteArray[0];
        if (!quote.TryGetProperty("open", out var opens) ||
            !quote.TryGetProperty("high", out var highs) ||
            !quote.TryGetProperty("low", out var lows) ||
            !quote.TryGetProperty("close", out var closes) ||
            !quote.TryGetProperty("volume", out var volumes))
        {
            return Array.Empty<PriceBar>();
        }

        JsonElement? adjCloseValues = null;
        if (indicators.TryGetProperty("adjclose", out var adjCloseArray) &&
            adjCloseArray.ValueKind == JsonValueKind.Array &&
            adjCloseArray.GetArrayLength() > 0 &&
            adjCloseArray[0].TryGetProperty("adjclose", out var adjCloseProperty))
        {
            adjCloseValues = adjCloseProperty;
        }

        var count = timestamps.GetArrayLength();
        var bars = new List<PriceBar>(count);

        for (var i = 0; i < count; i++)
        {
            if (!TryGetUnixDate(timestamps, i, out var date)) continue;
            if (!TryGetDecimal(opens, i, out var open)) continue;
            if (!TryGetDecimal(highs, i, out var high)) continue;
            if (!TryGetDecimal(lows, i, out var low)) continue;
            if (!TryGetDecimal(closes, i, out var close)) continue;
            var volume = TryGetLong(volumes, i, out var parsedVolume) ? parsedVolume : 0;
            var adjustedClose = adjCloseValues.HasValue && TryGetDecimal(adjCloseValues.Value, i, out var adj) ? (decimal?)adj : (decimal?)null;

            bars.Add(new PriceBar(date, open, high, low, close, volume, adjustedClose));
        }

        return bars;
    }

    private static bool TryGetUnixDate(JsonElement values, int index, out DateOnly date)
    {
        date = default;
        if (index >= values.GetArrayLength()) return false;
        var item = values[index];
        if (item.ValueKind != JsonValueKind.Number || !item.TryGetInt64(out var unixTimestamp)) return false;

        date = DateOnly.FromDateTime(DateTimeOffset.FromUnixTimeSeconds(unixTimestamp).UtcDateTime);
        return true;
    }

    private static bool TryGetDecimal(JsonElement values, int index, out decimal value)
    {
        value = default;
        if (index >= values.GetArrayLength()) return false;
        var item = values[index];
        if (item.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined) return false;
        if (item.ValueKind == JsonValueKind.Number && item.TryGetDecimal(out value)) return true;
        if (item.ValueKind == JsonValueKind.Number && item.TryGetDouble(out var doubleValue))
        {
            value = Convert.ToDecimal(doubleValue);
            return true;
        }

        return false;
    }

    private static bool TryGetLong(JsonElement values, int index, out long value)
    {
        value = 0;
        if (index >= values.GetArrayLength()) return false;
        var item = values[index];
        if (item.ValueKind != JsonValueKind.Number) return false;
        return item.TryGetInt64(out value);
    }
}
