using CycliqueShareTracker.Application.Interfaces;
using CycliqueShareTracker.Application.Models;
using CycliqueShareTracker.Application.Services;
using CycliqueShareTracker.Domain.Entities;
using CycliqueShareTracker.Domain.Enums;
using Microsoft.Extensions.Options;
using Xunit;

namespace CycliqueShareTracker.Tests;

public sealed class DashboardServiceTests
{
    [Fact]
    public async Task GetSnapshotAsync_ShouldUseConfiguredHistoryDays()
    {
        var priceRepository = new FakePriceRepository();
        var service = CreateService(priceRepository, historyDays: 252);

        _ = await service.GetSnapshotAsync();

        Assert.Equal(252, priceRepository.LastRequestedMaxRows);
    }

    [Fact]
    public async Task GetSnapshotAsync_ShouldFallbackToDefault_WhenConfiguredHistoryDaysIsInvalid()
    {
        var priceRepository = new FakePriceRepository();
        var service = CreateService(priceRepository, historyDays: 0);

        _ = await service.GetSnapshotAsync();

        Assert.Equal(252, priceRepository.LastRequestedMaxRows);
    }

    [Fact]
    public async Task GetSignalHistoryAsync_ShouldProjectValuesOrderedByMostRecent()
    {
        var priceRepository = new FakePriceRepository
        {
            Prices = new[]
            {
                new DailyPrice { AssetId = 1, Date = new DateOnly(2026, 04, 02), Close = 80m },
                new DailyPrice { AssetId = 1, Date = new DateOnly(2026, 04, 01), Close = 78m }
            }
        };
        var indicatorRepository = new FakeIndicatorRepository
        {
            Indicators = new[]
            {
                new DailyIndicator { AssetId = 1, Date = new DateOnly(2026, 04, 02), Sma50 = 79m, Sma200 = 75m, Rsi14 = 55m, Drawdown52WeeksPercent = -8m },
                new DailyIndicator { AssetId = 1, Date = new DateOnly(2026, 04, 01), Sma50 = 78m, Sma200 = 74m, Rsi14 = 52m, Drawdown52WeeksPercent = -9m }
            }
        };
        var signalRepository = new FakeSignalRepository
        {
            Signals = new[]
            {
                new DailySignal
                {
                    AssetId = 1,
                    Date = new DateOnly(2026, 04, 02),
                    Score = 65,
                    SignalLabel = SignalLabel.Watch,
                    ExitScore = 72,
                    ExitSignalLabel = ExitSignalLabel.SellZone,
                    ExitPrimaryReason = "Cassure sous SMA200 (tendance fragilisée)."
                },
                new DailySignal
                {
                    AssetId = 1,
                    Date = new DateOnly(2026, 04, 01),
                    Score = 72,
                    SignalLabel = SignalLabel.BuyZone,
                    ExitScore = 25,
                    ExitSignalLabel = ExitSignalLabel.Hold,
                    ExitPrimaryReason = "Aucun signal de sortie fort détecté."
                }
            }
        };

        var service = new DashboardService(
            new FakeAssetRepository(),
            priceRepository,
            indicatorRepository,
            signalRepository,
            new SignalService(),
            new ExitSignalService(),
            Options.Create(new AssetOptions { Symbol = "TTE.PA", Name = "TotalEnergies", Market = "Euronext Paris" }),
            Options.Create(new DashboardOptions { HistoryDays = 252 }));

        var result = await service.GetSignalHistoryAsync();

        Assert.Equal(2, result.Count);
        Assert.Equal(new DateOnly(2026, 04, 02), result[0].Date);
        Assert.Equal(80m, result[0].Close);
        Assert.Equal(79m, result[0].Sma50);
        Assert.Equal(75m, result[0].Sma200);
        Assert.Equal(55m, result[0].Rsi14);
        Assert.Equal(-8m, result[0].Drawdown52WeeksPercent);
        Assert.Equal(65, result[0].Score);
        Assert.Equal(SignalLabel.Watch, result[0].SignalLabel);
        Assert.Equal(72, result[0].ExitScore);
        Assert.Equal(ExitSignalLabel.SellZone, result[0].ExitSignalLabel);
        Assert.Equal("Aucun signal de sortie fort détecté.", result[0].ExitPrimaryReason);
        Assert.NotEmpty(result[0].EntryScoreFactors);
        Assert.NotEmpty(result[0].ExitScoreFactors);
    }

    [Fact]
    public async Task GetSnapshotAsync_ShouldFallbackToLatestAvailableMacd_WhenLatestIndicatorIsNull()
    {
        var priceRepository = new FakePriceRepository
        {
            Prices = new[]
            {
                new DailyPrice { AssetId = 1, Date = new DateOnly(2026, 04, 03), Open = 91m, High = 93m, Low = 90m, Close = 92m },
                new DailyPrice { AssetId = 1, Date = new DateOnly(2026, 04, 02), Open = 90m, High = 92m, Low = 89m, Close = 91m }
            }
        };
        var indicatorRepository = new FakeIndicatorRepository
        {
            Indicators = new[]
            {
                new DailyIndicator { AssetId = 1, Date = new DateOnly(2026, 04, 03), Sma50 = 89m, Sma200 = 85m, Rsi14 = 52m, Drawdown52WeeksPercent = -7m },
                new DailyIndicator { AssetId = 1, Date = new DateOnly(2026, 04, 02), Sma50 = 88m, Sma200 = 84m, Rsi14 = 50m, Drawdown52WeeksPercent = -8m, MacdLine = 0.45m, MacdSignalLine = 0.30m, MacdHistogram = 0.15m }
            }
        };
        var service = new DashboardService(
            new FakeAssetRepository(),
            priceRepository,
            indicatorRepository,
            new FakeSignalRepository(),
            new SignalService(),
            new ExitSignalService(),
            Options.Create(new AssetOptions { Symbol = "TTE.PA", Name = "TotalEnergies", Market = "Euronext Paris" }),
            Options.Create(new DashboardOptions { HistoryDays = 252 }));

        var snapshot = await service.GetSnapshotAsync();

        Assert.Equal(0.45m, snapshot.MacdLine);
        Assert.Equal(0.30m, snapshot.MacdSignalLine);
        Assert.Equal(0.15m, snapshot.MacdHistogram);
    }

    private static DashboardService CreateService(FakePriceRepository priceRepository, int historyDays)
    {
        return new DashboardService(
            new FakeAssetRepository(),
            priceRepository,
            new FakeIndicatorRepository(),
            new FakeSignalRepository(),
            new SignalService(),
            new ExitSignalService(),
            Options.Create(new AssetOptions
            {
                Symbol = "TTE.PA",
                Name = "TotalEnergies",
                Market = "Euronext Paris"
            }),
            Options.Create(new DashboardOptions
            {
                HistoryDays = historyDays
            }));
    }

    private sealed class FakeAssetRepository : IAssetRepository
    {
        public Task<Asset> GetOrCreateAsync(string symbol, string name, string market, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new Asset
            {
                Id = 1,
                Symbol = symbol,
                Name = name,
                Market = market
            });
        }
    }

    private sealed class FakePriceRepository : IPriceRepository
    {
        public int LastRequestedMaxRows { get; private set; }
        public IReadOnlyList<DailyPrice> Prices { get; init; } = Array.Empty<DailyPrice>();

        public Task UpsertDailyPricesAsync(int assetId, IReadOnlyList<PriceBar> prices, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<DailyPrice>> GetPricesAsync(int assetId, int maxRows, CancellationToken cancellationToken = default)
        {
            LastRequestedMaxRows = maxRows;
            return Task.FromResult(Prices);
        }

        public Task<DailyPrice?> GetLatestAsync(int assetId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<DailyPrice?>(null);
        }
    }

    private sealed class FakeIndicatorRepository : IIndicatorRepository
    {
        public IReadOnlyList<DailyIndicator> Indicators { get; init; } = Array.Empty<DailyIndicator>();

        public Task UpsertIndicatorsAsync(int assetId, IReadOnlyList<DailyIndicator> indicators, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task<DailyIndicator?> GetLatestAsync(int assetId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<DailyIndicator?>(null);
        }

        public Task<IReadOnlyList<DailyIndicator>> GetIndicatorsAsync(int assetId, int maxRows, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Indicators);
        }
    }

    private sealed class FakeSignalRepository : ISignalRepository
    {
        public IReadOnlyList<DailySignal> Signals { get; init; } = Array.Empty<DailySignal>();

        public Task UpsertSignalsAsync(int assetId, IReadOnlyList<DailySignal> signals, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task<DailySignal?> GetLatestAsync(int assetId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<DailySignal?>(null);
        }

        public Task<IReadOnlyList<DailySignal>> GetSignalsAsync(int assetId, int maxRows, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Signals);
        }
    }
}
