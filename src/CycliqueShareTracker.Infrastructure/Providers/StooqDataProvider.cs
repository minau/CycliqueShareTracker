using System.Globalization;
using CycliqueShareTracker.Application.Interfaces;
using CycliqueShareTracker.Application.Models;
using Microsoft.Extensions.Logging;

namespace CycliqueShareTracker.Infrastructure.Providers;

public sealed class StooqDataProvider : IDataProvider
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<StooqDataProvider> _logger;

    public StooqDataProvider(HttpClient httpClient, ILogger<StooqDataProvider> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<IReadOnlyList<PriceBar>> FetchDailyPricesAsync(string symbol, CancellationToken cancellationToken = default)
    {
        var normalized = symbol.ToLowerInvariant();
        var url = $"https://stooq.com/q/d/l/?s={normalized}&i=d";

        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.TryAddWithoutValidation("User-Agent", "Mozilla/5.0 (compatible; CycliqueShareTracker/1.0)");
        request.Headers.TryAddWithoutValidation("Accept", "text/csv,text/plain;q=0.9,*/*;q=0.8");

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("Stooq provider returned HTTP {StatusCode} for {Symbol}", (int)response.StatusCode, symbol);
            return Array.Empty<PriceBar>();
        }

        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        if (content.StartsWith("<", StringComparison.Ordinal))
        {
            _logger.LogWarning("Stooq provider returned non-CSV content for {Symbol}", symbol);
            return Array.Empty<PriceBar>();
        }

        var lines = content.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        if (lines.Length <= 1)
        {
            return Array.Empty<PriceBar>();
        }

        var result = new List<PriceBar>();

        foreach (var line in lines.Skip(1))
        {
            var cols = line.Split(',');
            if (cols.Length < 6)
            {
                continue;
            }

            if (!DateOnly.TryParse(cols[0], out var date)) continue;
            if (!decimal.TryParse(cols[1], CultureInfo.InvariantCulture, out var open)) continue;
            if (!decimal.TryParse(cols[2], CultureInfo.InvariantCulture, out var high)) continue;
            if (!decimal.TryParse(cols[3], CultureInfo.InvariantCulture, out var low)) continue;
            if (!decimal.TryParse(cols[4], CultureInfo.InvariantCulture, out var close)) continue;
            _ = long.TryParse(cols[5], CultureInfo.InvariantCulture, out var volume);

            result.Add(new PriceBar(date, open, high, low, close, volume));
        }

        _logger.LogInformation("Stooq provider returned {Count} rows for {Symbol}", result.Count, symbol);
        return result.OrderBy(x => x.Date).ToList();
    }
}
