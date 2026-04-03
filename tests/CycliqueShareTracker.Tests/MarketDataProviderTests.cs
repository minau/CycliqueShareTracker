using System.Net;
using CycliqueShareTracker.Application.Models;
using CycliqueShareTracker.Infrastructure.Providers;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace CycliqueShareTracker.Tests;

public sealed class MarketDataProviderTests
{
    [Fact]
    public async Task YahooProvider_ShouldMapToCommonModel_WithAdjustedClose()
    {
        const string yahooJson = """
        {
          "chart": {
            "result": [
              {
                "timestamp": [1704067200, 1704153600],
                "indicators": {
                  "quote": [
                    {
                      "open": [100.0, 102.0],
                      "high": [101.0, 103.0],
                      "low": [99.0, 101.0],
                      "close": [100.5, 102.5],
                      "volume": [1000, 1200]
                    }
                  ],
                  "adjclose": [
                    {
                      "adjclose": [100.3, 102.3]
                    }
                  ]
                }
              }
            ]
          }
        }
        """;

        var provider = new YahooFinanceDataProvider(
            new HttpClient(new FakeHttpMessageHandler(_ => FakeHttpMessageHandler.Json(HttpStatusCode.OK, yahooJson))),
            Options.Create(new MarketDataOptions()).ToSymbolMapper(),
            NullLogger<YahooFinanceDataProvider>.Instance);

        var result = await provider.FetchDailyPricesAsync("TTE.PA");

        Assert.Equal(2, result.Count);
        Assert.Equal(new DateOnly(2024, 1, 1), result[0].Date);
        Assert.Equal(100.0m, result[0].Open);
        Assert.Equal(100.3m, result[0].AdjustedClose);
    }


    [Fact]
    public async Task YahooProvider_ShouldConvertLegacyFrSymbol_ToPaSymbol()
    {
        const string yahooEmptyJson = """
        {
          "chart": {
            "result": []
          }
        }
        """;

        string? requestedUrl = null;
        var provider = new YahooFinanceDataProvider(
            new HttpClient(new FakeHttpMessageHandler(req =>
            {
                requestedUrl = req.RequestUri?.ToString();
                return FakeHttpMessageHandler.Json(HttpStatusCode.OK, yahooEmptyJson);
            })),
            Options.Create(new MarketDataOptions()).ToSymbolMapper(),
            NullLogger<YahooFinanceDataProvider>.Instance);

        _ = await provider.FetchDailyPricesAsync("tte.fr");

        Assert.NotNull(requestedUrl);
        Assert.Contains("/TTE.PA?", requestedUrl);
    }

    [Fact]
    public async Task YahooProvider_ShouldRejectInvalidBars_WhenPriceIsNotPositive()
    {
        const string yahooInvalidJson = """
        {
          "chart": {
            "result": [
              {
                "timestamp": [1704067200],
                "indicators": {
                  "quote": [
                    {
                      "open": [0.0],
                      "high": [101.0],
                      "low": [99.0],
                      "close": [100.5],
                      "volume": [1000]
                    }
                  ]
                }
              }
            ]
          }
        }
        """;

        var provider = new YahooFinanceDataProvider(
            new HttpClient(new FakeHttpMessageHandler(_ => FakeHttpMessageHandler.Json(HttpStatusCode.OK, yahooInvalidJson))),
            Options.Create(new MarketDataOptions()).ToSymbolMapper(),
            NullLogger<YahooFinanceDataProvider>.Instance);

        var result = await provider.FetchDailyPricesAsync("TTE.PA");

        Assert.Empty(result);
    }

    [Fact]
    public async Task FallbackProvider_ShouldUseFallback_WhenPrimaryFails()
    {
        var options = Options.Create(new MarketDataOptions
        {
            PrimaryProvider = YahooFinanceDataProvider.ProviderName,
            FallbackProvider = AlphaVantageDataProvider.ProviderName
        });

        var providers = new IMarketDataSource[]
        {
            new StubProvider(YahooFinanceDataProvider.ProviderName, throwError: true),
            new StubProvider(AlphaVantageDataProvider.ProviderName, rows: [new PriceBar(new DateOnly(2024,1,2),1,2,1,2,100)])
        };

        var fallbackProvider = new FallbackMarketDataProvider(providers, options, NullLogger<FallbackMarketDataProvider>.Instance);

        var result = await fallbackProvider.FetchDailyPricesAsync("TTE.PA");

        Assert.Single(result);
        Assert.Equal(new DateOnly(2024, 1, 2), result[0].Date);
    }

    [Fact]
    public async Task FallbackProvider_ShouldUseConfiguredPrimary_WhenAvailable()
    {
        var options = Options.Create(new MarketDataOptions
        {
            PrimaryProvider = AlphaVantageDataProvider.ProviderName,
            FallbackProvider = YahooFinanceDataProvider.ProviderName
        });

        var expected = new PriceBar(new DateOnly(2024, 1, 3), 1, 2, 1, 2, 100);
        var providers = new IMarketDataSource[]
        {
            new StubProvider(YahooFinanceDataProvider.ProviderName, rows: [new PriceBar(new DateOnly(2024,1,2),1,2,1,2,100)]),
            new StubProvider(AlphaVantageDataProvider.ProviderName, rows: [expected])
        };

        var fallbackProvider = new FallbackMarketDataProvider(providers, options, NullLogger<FallbackMarketDataProvider>.Instance);

        var result = await fallbackProvider.FetchDailyPricesAsync("TTE.PA");

        Assert.Single(result);
        Assert.Equal(expected.Date, result[0].Date);
    }

    private sealed class StubProvider : IMarketDataSource
    {
        private readonly IReadOnlyList<PriceBar> _rows;
        private readonly bool _throwError;

        public StubProvider(string name, IReadOnlyList<PriceBar>? rows = null, bool throwError = false)
        {
            Name = name;
            _rows = rows ?? Array.Empty<PriceBar>();
            _throwError = throwError;
        }

        public string Name { get; }

        public Task<IReadOnlyList<PriceBar>> FetchDailyPricesAsync(string symbol, CancellationToken cancellationToken = default)
        {
            if (_throwError)
            {
                throw new HttpRequestException("boom");
            }

            return Task.FromResult(_rows);
        }
    }
}

internal static class MarketDataOptionsExtensions
{
    public static ProviderSymbolMapper ToSymbolMapper(this IOptions<MarketDataOptions> options)
    {
        return new ProviderSymbolMapper(options);
    }
}
