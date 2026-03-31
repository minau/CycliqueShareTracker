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

        using var response = await _httpClient.GetAsync(url, cancellationToken);
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync(cancellationToken);
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
