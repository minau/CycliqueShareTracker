using CycliqueShareTracker.Application.Interfaces;
using CycliqueShareTracker.Application.Models;
using CycliqueShareTracker.Application.Models.BacktestAnalysisExport;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace CycliqueShareTracker.Infrastructure.Services;

public sealed class BacktestAnalysisExportService : IBacktestAnalysisExportService
{
    private readonly ILogger<BacktestAnalysisExportService> _logger;
    private readonly BacktestExportOptions _options;

    public BacktestAnalysisExportService(ILogger<BacktestAnalysisExportService> logger, IOptions<BacktestExportOptions> options)
    {
        _logger = logger;
        _options = options.Value;
    }

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
        var exportDirectory = ResolveExportDirectory();
        var fileName = BuildFileName(symbolSelection, result.Request.AlgorithmType.ToString(), generatedAtUtc);
        var fullPath = Path.GetFullPath(Path.Combine(exportDirectory, fileName));

        _logger.LogInformation("Preparing backtest analysis JSON export. Directory={ExportDirectory}; FileName={FileName}; FullPath={FullPath}",
            exportDirectory,
            fileName,
            fullPath);

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
                    strategy.EarlySellWeaknessScoreThreshold,
                    strategy.StrongExtensionAboveSma50ForSellPct,
                    strategy.MetaAlgoParameters)),
            result.Assets.Select(MapAsset).ToList(),
            result.AggregateMetrics);

        await using var stream = File.Create(fullPath);
        await JsonSerializer.SerializeAsync(stream, dto, JsonOptions, cancellationToken);

        _logger.LogInformation("Backtest analysis JSON export completed. FullPath={FullPath}", fullPath);

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

        var orderedBars = (asset.OhlcvBars ?? Array.Empty<PriceBar>())
            .OrderBy(x => x.Date)
            .ToList();

        return new BacktestAnalysisAssetDto(
            asset.Symbol,
            asset.AssetName,
            candles,
            asset.Signals ?? Array.Empty<BacktestSignal>(),
            BuildTradeAnalysis(asset.Trades, orderedBars),
            asset.Metrics,
            asset.Error);
    }

    private static IReadOnlyList<BacktestAnalysisTradeEnvelopeDto> BuildTradeAnalysis(
        IReadOnlyList<Trade> trades,
        IReadOnlyList<PriceBar> orderedBars)
    {
        if (trades.Count == 0)
        {
            return Array.Empty<BacktestAnalysisTradeEnvelopeDto>();
        }

        return trades
            .Select(trade => new BacktestAnalysisTradeEnvelopeDto(
                new BacktestAnalysisTradeDto(
                    trade.EntryDate,
                    trade.ExitDate,
                    trade.EntryPrice,
                    trade.ExitPrice,
                    trade.PerformancePercent,
                    ComputeMaxDrawdownPercent(trade, orderedBars),
                    ComputeMaxProfitPercent(trade, orderedBars),
                    trade.DurationDays,
                    BuildReasonsArray(trade.EntryReason),
                    BuildReasonsArray(trade.ExitReason))))
            .ToList();
    }

    private static decimal ComputeMaxDrawdownPercent(Trade trade, IReadOnlyList<PriceBar> orderedBars)
    {
        var tradeBars = orderedBars
            .Where(x => x.Date >= trade.EntryDate && x.Date <= trade.ExitDate)
            .ToList();

        if (tradeBars.Count == 0 || trade.EntryPrice == 0m)
        {
            return 0m;
        }

        var worstMove = tradeBars
            .Select(bar => ((bar.Close / trade.EntryPrice) - 1m) * 100m)
            .Min();

        return decimal.Round(Math.Abs(Math.Min(worstMove, 0m)), 2);
    }

    private static decimal ComputeMaxProfitPercent(Trade trade, IReadOnlyList<PriceBar> orderedBars)
    {
        var tradeBars = orderedBars
            .Where(x => x.Date >= trade.EntryDate && x.Date <= trade.ExitDate)
            .ToList();

        if (tradeBars.Count == 0 || trade.EntryPrice == 0m)
        {
            return 0m;
        }

        var bestMove = tradeBars
            .Select(bar => ((bar.Close / trade.EntryPrice) - 1m) * 100m)
            .Max();

        return decimal.Round(Math.Max(bestMove, 0m), 2);
    }

    private static IReadOnlyList<string> BuildReasonsArray(string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            return Array.Empty<string>();
        }

        return new[] { reason };
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

        var debugValues = new Dictionary<string, object?>(point.DebugValues)
        {
            ["confidence"] = point.Confidence,
            ["buyDetails"] = point.BuyDetails,
            ["sellDetails"] = point.SellDetails
        };

        return debugValues;
    }


    private string ResolveExportDirectory()
    {
        var configuredDirectory = Environment.GetEnvironmentVariable("BACKTEST_EXPORT_DIRECTORY");
        var primaryFromConfig = string.IsNullOrWhiteSpace(_options.DirectoryPath)
            ? "/var/cyclique/exports"
            : _options.DirectoryPath;

        var primaryDirectory = string.IsNullOrWhiteSpace(configuredDirectory)
            ? Path.GetFullPath(primaryFromConfig)
            : Path.GetFullPath(configuredDirectory);

        try
        {
            Directory.CreateDirectory(primaryDirectory);
            return primaryDirectory;
        }
        catch (Exception ex)
        {
            var fallbackDirectory = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "CycliqueShareTracker", "exports"));
            Directory.CreateDirectory(fallbackDirectory);
            _logger.LogWarning(ex,
                "Failed to create primary export directory {PrimaryDirectory}. Falling back to {FallbackDirectory}",
                primaryDirectory,
                fallbackDirectory);
            return fallbackDirectory;
        }
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
