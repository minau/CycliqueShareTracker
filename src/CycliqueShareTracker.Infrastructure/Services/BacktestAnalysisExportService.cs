using CycliqueShareTracker.Application.Interfaces;
using CycliqueShareTracker.Application.Models;
using CycliqueShareTracker.Application.Models.BacktestAnalysisExport;
using System.Text.Json;

namespace CycliqueShareTracker.Infrastructure.Services;

public sealed class BacktestAnalysisExportService : IBacktestAnalysisExportService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public async Task<string> ExportAsync(
        BacktestResult result,
        string symbolSelection,
        DateTime? executedAtUtc,
        CancellationToken cancellationToken = default)
    {
        var generatedAtUtc = DateTime.UtcNow;
        var exportDirectory = Path.Combine(AppContext.BaseDirectory, "exports");
        Directory.CreateDirectory(exportDirectory);

        var fileName = BuildFileName(symbolSelection, result.Request.AlgorithmType.ToString(), generatedAtUtc);
        var fullPath = Path.Combine(exportDirectory, fileName);

        var strategy = result.Request.StrategyConfig ?? StrategyConfig.Default;
        var dto = new BacktestAnalysisExportDto(
            new BacktestAnalysisMetadataDto(
                generatedAtUtc,
                result.Request.StartDate,
                result.Request.EndDate,
                symbolSelection,
                result.Request.Symbols,
                result.Request.IncludeMacdInScoring,
                executedAtUtc),
            new BacktestAnalysisAlgorithmDto(
                result.Request.AlgorithmType.ToDisplayName(),
                result.Request.AlgorithmType.ToString(),
                new BacktestAnalysisAlgorithmParametersDto(
                    strategy.FeePercentPerSide,
                    strategy.BuyScoreThreshold,
                    strategy.SellScoreThreshold,
                    strategy.MinRsiForBuy,
                    strategy.MaxRsiForBuy,
                    strategy.MinRsiWeaknessForSell,
                    strategy.EnableMacdConfirmation,
                    strategy.MinimumBarsBetweenSameSignal,
                    strategy.MaxDistanceAboveSma50ForBuyPct,
                    strategy.MinSma50SlopeForBuy,
                    strategy.MaxFlatSlopeThreshold,
                    strategy.MinGapBetweenSma50AndSma200Pct,
                    strategy.EarlySellEnabled,
                    strategy.EarlySellWeaknessScoreThreshold)),
            result.Assets.Select(MapAsset).ToList(),
            result.AggregateMetrics);

        await using var stream = File.Create(fullPath);
        await JsonSerializer.SerializeAsync(stream, dto, JsonOptions, cancellationToken);

        return fullPath;
    }

    private static BacktestAnalysisAssetDto MapAsset(BacktestAssetResult asset)
    {
        var barsByDate = (asset.OhlcvBars ?? Array.Empty<PriceBar>()).ToDictionary(x => x.Date);
        var indicatorsByDate = (asset.Indicators ?? Array.Empty<ComputedIndicator>()).ToDictionary(x => x.Date);
        var pointsByDate = asset.AlgorithmResult?.Points.ToDictionary(x => x.Date)
            ?? new Dictionary<DateOnly, AlgorithmSignalPoint>();

        var allDates = new SortedSet<DateOnly>(barsByDate.Keys);
        allDates.UnionWith(indicatorsByDate.Keys);
        allDates.UnionWith(pointsByDate.Keys);

        var candles = allDates.Select(date =>
        {
            barsByDate.TryGetValue(date, out var bar);
            indicatorsByDate.TryGetValue(date, out var indicator);
            pointsByDate.TryGetValue(date, out var point);

            return new BacktestAnalysisCandleDto(
                date,
                bar?.Open,
                bar?.High,
                bar?.Low,
                bar?.Close,
                bar?.Volume,
                new BacktestAnalysisIndicatorsDto(
                    indicator?.Sma50,
                    indicator?.Sma200,
                    indicator?.Ema12,
                    indicator?.Ema26,
                    indicator?.Rsi14,
                    indicator?.Drawdown52WeeksPercent,
                    indicator?.MacdLine,
                    indicator?.MacdSignalLine,
                    indicator?.MacdHistogram),
                new BacktestAnalysisAlgorithmPointDto(
                    point?.IsBuyZone,
                    point?.IsSellZone,
                    point?.BuyScore,
                    point?.SellScore,
                    ResolveSignalType(point),
                    BuildReasons(point),
                    BuildDebugValues(point)));
        }).ToList();

        return new BacktestAnalysisAssetDto(
            asset.Symbol,
            asset.AssetName,
            candles,
            asset.Signals ?? Array.Empty<BacktestSignal>(),
            asset.Trades,
            asset.Metrics,
            asset.Error);
    }

    private static string? ResolveSignalType(AlgorithmSignalPoint? point)
    {
        if (point is null)
        {
            return null;
        }

        if (point.BuySignal)
        {
            return "Buy";
        }

        if (point.SellSignal)
        {
            return "Sell";
        }

        return "Neutral";
    }

    private static IReadOnlyList<string>? BuildReasons(AlgorithmSignalPoint? point)
    {
        if (point is null)
        {
            return null;
        }

        return new[] { point.BuyReason, point.SellReason };
    }

    private static IReadOnlyDictionary<string, object?>? BuildDebugValues(AlgorithmSignalPoint? point)
    {
        if (point is null)
        {
            return null;
        }

        return new Dictionary<string, object?>
        {
            ["confidence"] = point.Confidence,
            ["buyDetails"] = point.BuyDetails,
            ["sellDetails"] = point.SellDetails
        };
    }

    private static string BuildFileName(string symbolSelection, string algorithm, DateTime generatedAtUtc)
    {
        var tickerPart = SanitizeForFileName(symbolSelection);
        var algorithmPart = SanitizeForFileName(algorithm);
        var timestamp = generatedAtUtc.ToString("yyyyMMdd_HHmmss");
        return $"backtest_analysis_{tickerPart}_{algorithmPart}_{timestamp}.json";
    }

    private static string SanitizeForFileName(string value)
    {
        var invalidChars = Path.GetInvalidFileNameChars();
        var clean = new string(value
            .Trim()
            .Select(ch => invalidChars.Contains(ch) || char.IsWhiteSpace(ch) ? '_' : ch)
            .ToArray());

        return string.IsNullOrWhiteSpace(clean) ? "unknown" : clean;
    }
}
