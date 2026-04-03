using CycliqueShareTracker.Application.Interfaces;
using CycliqueShareTracker.Application.Models;
using CycliqueShareTracker.Application.Services;
using CycliqueShareTracker.Domain.Entities;
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

    private static DashboardService CreateService(FakePriceRepository priceRepository, int historyDays)
    {
        return new DashboardService(
            new FakeAssetRepository(),
            priceRepository,
            new FakeIndicatorRepository(),
            new FakeSignalRepository(),
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

        public Task UpsertDailyPricesAsync(int assetId, IReadOnlyList<PriceBar> prices, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<DailyPrice>> GetPricesAsync(int assetId, int maxRows, CancellationToken cancellationToken = default)
        {
            LastRequestedMaxRows = maxRows;
            return Task.FromResult<IReadOnlyList<DailyPrice>>(Array.Empty<DailyPrice>());
        }

        public Task<DailyPrice?> GetLatestAsync(int assetId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<DailyPrice?>(null);
        }
    }

    private sealed class FakeIndicatorRepository : IIndicatorRepository
    {
        public Task UpsertIndicatorsAsync(int assetId, IReadOnlyList<DailyIndicator> indicators, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task<DailyIndicator?> GetLatestAsync(int assetId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<DailyIndicator?>(null);
        }
    }

    private sealed class FakeSignalRepository : ISignalRepository
    {
        public Task UpsertSignalsAsync(int assetId, IReadOnlyList<DailySignal> signals, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task<DailySignal?> GetLatestAsync(int assetId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<DailySignal?>(null);
        }
    }
}
