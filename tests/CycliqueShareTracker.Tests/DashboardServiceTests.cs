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

        _ = await service.GetSnapshotAsync("TTE.PA");

        Assert.Equal(252, priceRepository.LastRequestedMaxRows);
    }

    [Fact]
    public async Task GetSnapshotAsync_ShouldExposeSelectedAlgorithm()
    {
        var service = CreateService(new FakePriceRepository(), historyDays: 120);

        var snapshot = await service.GetSnapshotAsync("TTE.PA", AlgorithmType.MacdReversal);

        Assert.Equal(AlgorithmType.MacdReversal, snapshot.AlgorithmType);
        Assert.Equal("Test Algo", snapshot.AlgorithmName);
    }

    private static DashboardService CreateService(FakePriceRepository priceRepository, int historyDays)
    {
        return new DashboardService(
            new FakeAssetRepository(),
            priceRepository,
            new IndicatorCalculator(),
            new FakeAlgorithmRegistry(),
            new SignalEngine(new InMemoryPositionStore(), new InMemoryTradeExecutionLedger(), new AlwaysClosedWindowService(), new FixedTradingClock()),
            Options.Create(new WatchlistOptions
            {
                Assets = new List<TrackedAssetOptions>
                {
                    new() { Symbol = "TTE.PA", Name = "TotalEnergies", Sector = "Energy", Market = "Euronext Paris" }
                }
            }),
            Options.Create(new DashboardOptions { HistoryDays = historyDays }));
    }

    private sealed class AlwaysClosedWindowService : ITradingWindowService
    {
        public bool IsInWindow(DateTimeOffset now) => false;
    }

    private sealed class FixedTradingClock : ITradingClock
    {
        public DateTimeOffset UtcNow => new(2026, 4, 2, 12, 0, 0, TimeSpan.Zero);
    }

    private sealed class FakeAlgorithmRegistry : ISignalAlgorithmRegistry
    {
        private readonly ISignalAlgorithm _algorithm = new FakeAlgorithm();
        public ISignalAlgorithm Get(AlgorithmType algorithmType) => _algorithm;
        public IReadOnlyList<ISignalAlgorithm> GetAll() => new[] { _algorithm };
    }

    private sealed class FakeAlgorithm : ISignalAlgorithm
    {
        public AlgorithmType AlgorithmType => AlgorithmType.RsiMeanReversion;
        public string DisplayName => "Test Algo";

        public AlgorithmResult ComputeSignals(IReadOnlyList<PriceBar> bars, AlgorithmContext context)
            => new(AlgorithmType, DisplayName, Array.Empty<AlgorithmSignalPoint>());
    }

    private sealed class FakeAssetRepository : IAssetRepository
    {
        public Task<Asset> GetOrCreateAsync(string symbol, string name, string market, CancellationToken cancellationToken = default)
            => Task.FromResult(new Asset { Id = 1, Symbol = symbol, Name = name, Market = market });
    }

    private sealed class FakePriceRepository : IPriceRepository
    {
        public int LastRequestedMaxRows { get; private set; }

        public Task UpsertDailyPricesAsync(int assetId, IReadOnlyList<PriceBar> prices, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<IReadOnlyList<DailyPrice>> GetPricesAsync(int assetId, int maxRows, CancellationToken cancellationToken = default)
        {
            LastRequestedMaxRows = maxRows;
            return Task.FromResult<IReadOnlyList<DailyPrice>>(new[]
            {
                new DailyPrice { AssetId = 1, Date = new DateOnly(2026,4,1), Open = 10, High = 10, Low = 10, Close = 10 },
                new DailyPrice { AssetId = 1, Date = new DateOnly(2026,4,2), Open = 11, High = 11, Low = 11, Close = 11 }
            });
        }

        public Task<DailyPrice?> GetLatestAsync(int assetId, CancellationToken cancellationToken = default)
            => Task.FromResult<DailyPrice?>(new DailyPrice { AssetId = 1, Date = new DateOnly(2026,4,2), Close = 11 });

        public Task<IReadOnlyList<DailyPrice>> GetPricesInRangeAsync(int assetId, DateOnly startDate, DateOnly endDate, CancellationToken cancellationToken = default)
            => GetPricesAsync(assetId, 500, cancellationToken);
    }
}
