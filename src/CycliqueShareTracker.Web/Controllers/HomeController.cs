using CycliqueShareTracker.Application.Interfaces;
using CycliqueShareTracker.Application.Models;
using CycliqueShareTracker.Application.Trading;
using CycliqueShareTracker.Web.Models;
using ClosedXML.Excel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CycliqueShareTracker.Web.Controllers;

[Authorize]
public class HomeController : Controller
{
    private readonly IDashboardService _dashboardService;
    private readonly IDataSyncService _dataSyncService;
    private readonly IBacktestService _backtestService;

    public HomeController(
        IDashboardService dashboardService,
        IDataSyncService dataSyncService,
        IBacktestService backtestService)
    {
        _dashboardService = dashboardService;
        _dataSyncService = dataSyncService;
        _backtestService = backtestService;
    }

    [HttpGet]
    public async Task<IActionResult> Index(
        [FromQuery] AlgorithmType algorithmType = AlgorithmType.RsiMeanReversion,
        [FromQuery] string sortBy = "ticker",
        CancellationToken cancellationToken = default)
    {
        var snapshots = await _dashboardService.GetWatchlistSnapshotsAsync(algorithmType, cancellationToken);

        if (snapshots.All(x => x.Snapshot?.LastClose is null && string.IsNullOrWhiteSpace(x.Error)))
        {
            await _dataSyncService.RunDailyUpdateAsync(cancellationToken);
            snapshots = await _dashboardService.GetWatchlistSnapshotsAsync(algorithmType, cancellationToken);
        }

        var items = snapshots.Select(item =>
        {
            var snapshot = item.Snapshot;

            var fallbackError = snapshot is not null && snapshot.LastClose is null
                ? "Aucune donnée de marché disponible pour cette action."
                : null;

            return new WatchlistItemViewModel
            {
                Symbol = item.Asset.Symbol,
                Name = item.Asset.Name,
                Sector = item.Asset.Sector,
                LastClose = snapshot?.LastClose,
                Sma50 = snapshot?.Sma50,
                Sma200 = snapshot?.Sma200,
                Rsi14 = snapshot?.Rsi14,
                Drawdown52WeeksPercent = snapshot?.Drawdown52WeeksPercent,
                Error = item.Error ?? fallbackError
            };
        });

        var ordered = ApplySort(items, sortBy).ToList();

        var model = new WatchlistViewModel
        {
            ActiveAlgorithmType = algorithmType.ToString(),
            ActiveAlgorithmName = algorithmType.ToDisplayName(),
            SortBy = sortBy,
            Items = ordered
        };

        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> Detail([FromQuery] string symbol, [FromQuery] AlgorithmType algorithmType = AlgorithmType.RsiMeanReversion, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(symbol))
        {
            return RedirectToAction(nameof(Index), new { algorithmType });
        }

        var trackedAsset = _dashboardService.GetTrackedAssets().FirstOrDefault(x => string.Equals(x.Symbol, symbol, StringComparison.OrdinalIgnoreCase));
        if (trackedAsset is null)
        {
            return NotFound();
        }

        var snapshot = await _dashboardService.GetSnapshotAsync(trackedAsset.Symbol, algorithmType, cancellationToken);

        if (snapshot.LastClose is null)
        {
            await _dataSyncService.RunDailyUpdateAsync(cancellationToken);
            snapshot = await _dashboardService.GetSnapshotAsync(trackedAsset.Symbol, algorithmType, cancellationToken);
        }

        var missingDataNotice = snapshot.LastClose is null
            ? "Aucune donnée marché disponible actuellement pour cette action. Vérifiez la configuration provider/symbol map et relancez une mise à jour."
            : null;

        var feedbackNotice = TempData["DashboardNotice"] as string;
        var notice = string.Join(" ", new[] { missingDataNotice, feedbackNotice }.Where(x => !string.IsNullOrWhiteSpace(x)));

        var model = DashboardViewModel.FromSnapshot(snapshot, trackedAsset.Sector, string.IsNullOrWhiteSpace(notice) ? null : notice);
        return View(model);
    }


    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveIndicatorSettings(IndicatorSettingsFormModel form, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(form.Symbol))
        {
            return RedirectToAction(nameof(Index));
        }

        var algorithmType = ParseAlgorithmType(form.AlgorithmType);
        var validationError = ValidateIndicatorSettings(form);
        if (!string.IsNullOrWhiteSpace(validationError))
        {
            TempData["DashboardNotice"] = validationError;
            return RedirectToAction(nameof(Detail), new { symbol = form.Symbol, algorithmType });
        }

        var settings = new IndicatorComputationSettings(
            form.ParabolicSarStep,
            form.ParabolicSarMax,
            form.BollingerPeriod,
            form.BollingerStdDev,
            form.MacdFastPeriod,
            form.MacdSlowPeriod,
            form.MacdSignalPeriod);

        await _dashboardService.SaveIndicatorSettingsAsync(form.Symbol, settings, cancellationToken);
        TempData["DashboardNotice"] = "Paramètres des indicateurs sauvegardés. Les indicateurs ont été recalculés.";
        return RedirectToAction(nameof(Detail), new { symbol = form.Symbol, algorithmType });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetIndicatorSettings(string symbol, AlgorithmType algorithmType = AlgorithmType.RsiMeanReversion, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(symbol))
        {
            return RedirectToAction(nameof(Index), new { algorithmType });
        }

        await _dashboardService.ResetIndicatorSettingsAsync(symbol, cancellationToken);
        TempData["DashboardNotice"] = "Paramètres des indicateurs réinitialisés avec les valeurs par défaut.";
        return RedirectToAction(nameof(Detail), new { symbol, algorithmType });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Refresh(string? symbol = null, AlgorithmType algorithmType = AlgorithmType.RsiMeanReversion, CancellationToken cancellationToken = default)
    {
        await _dataSyncService.RunDailyUpdateAsync(cancellationToken);
        if (!string.IsNullOrWhiteSpace(symbol))
        {
            return RedirectToAction(nameof(Detail), new { symbol, algorithmType });
        }

        return RedirectToAction(nameof(Index), new { algorithmType });
    }

    [HttpGet]
    public async Task<IActionResult> ExportHistoryXlsx(
        [FromQuery] string symbol,
        [FromQuery] AlgorithmType algorithmType = AlgorithmType.RsiMeanReversion,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(symbol))
        {
            return RedirectToAction(nameof(Index), new { algorithmType });
        }

        var trackedAsset = _dashboardService.GetTrackedAssets().FirstOrDefault(x => string.Equals(x.Symbol, symbol, StringComparison.OrdinalIgnoreCase));
        if (trackedAsset is null)
        {
            return NotFound();
        }

        var snapshot = await _dashboardService.GetSnapshotAsync(trackedAsset.Symbol, algorithmType, cancellationToken);
        if (snapshot.HistoryRows.Count == 0)
        {
            return RedirectToAction(nameof(Detail), new { symbol = trackedAsset.Symbol, algorithmType });
        }

        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add("DashboardHistory");
        WriteHistoryWorksheet(worksheet, snapshot.HistoryRows);

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        stream.Position = 0;

        var fileName = $"{trackedAsset.Symbol}_dashboard_history_{DateTime.UtcNow:yyyyMMdd_HHmmss}.xlsx";
        return File(stream.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
    }

    [HttpGet]
    public IActionResult Documentation([FromQuery] AlgorithmType algorithmType = AlgorithmType.RsiMeanReversion)
    {
        return View(true);
    }

    [HttpGet]
    public async Task<IActionResult> Backtest(
        [FromQuery] string? symbol,
        [FromQuery] DateOnly? startDate,
        [FromQuery] DateOnly? endDate,
        [FromQuery] decimal? initialCapital,
        [FromQuery] decimal? fixedAmountPerTrade,
        [FromQuery] decimal? feePerTrade,
        [FromQuery] decimal? slippagePercent,
        [FromQuery] bool forceCloseOnPeriodEnd = true,
        [FromQuery] bool run = false,
        CancellationToken cancellationToken = default)
    {
        var symbols = _backtestService.GetTrackedSymbols();
        var defaultEnd = DateOnly.FromDateTime(DateTime.UtcNow.Date);
        var defaultStart = defaultEnd.AddYears(-1);
        if (!run)
        {
            var defaultModel = BacktestViewModel.CreateDefault(symbols, defaultStart, defaultEnd);
            return View(defaultModel);
        }

        var selectedSymbol = string.IsNullOrWhiteSpace(symbol) ? symbols.FirstOrDefault() ?? string.Empty : symbol;
        var safeStart = startDate ?? defaultStart;
        var safeEnd = endDate ?? defaultEnd;
        var parameters = new BacktestParameters(
            selectedSymbol,
            safeStart,
            safeEnd,
            initialCapital ?? 10_000m,
            fixedAmountPerTrade ?? 1_000m,
            feePerTrade ?? 0m,
            slippagePercent ?? 0m,
            forceCloseOnPeriodEnd);

        var result = await _backtestService.RunAsync(parameters, cancellationToken);
        var model = BacktestViewModel.FromResult(result, symbols);
        return View(model);
    }


    private static string? ValidateIndicatorSettings(IndicatorSettingsFormModel form)
    {
        if (form.MacdFastPeriod >= form.MacdSlowPeriod)
        {
            return "Validation: MACD Fast doit être strictement inférieur à MACD Slow.";
        }

        if (form.BollingerPeriod < 2)
        {
            return "Validation: BollingerPeriod doit être supérieur ou égal à 2.";
        }

        if (form.ParabolicSarStep <= 0 || form.ParabolicSarMax <= 0)
        {
            return "Validation: Parabolic SAR step et max doivent être strictement positifs.";
        }

        if (form.ParabolicSarStep >= form.ParabolicSarMax)
        {
            return "Validation: Parabolic SAR step doit être strictement inférieur à max.";
        }

        if (form.BollingerStdDev <= 0 || form.MacdSignalPeriod <= 0 || form.MacdFastPeriod <= 0 || form.MacdSlowPeriod <= 0)
        {
            return "Validation: toutes les périodes et écarts-types doivent être strictement positifs.";
        }

        return null;
    }

    private static AlgorithmType ParseAlgorithmType(string algorithmType)
    {
        return Enum.TryParse<AlgorithmType>(algorithmType, true, out var parsed)
            ? parsed
            : AlgorithmType.RsiMeanReversion;
    }

    private static IEnumerable<WatchlistItemViewModel> ApplySort(IEnumerable<WatchlistItemViewModel> items, string sortBy)
    {
        return sortBy?.ToLowerInvariant() switch
        {
            "name" => items.OrderBy(x => x.Name),
            _ => items.OrderBy(x => x.Symbol)
        };
    }

    private static void WriteHistoryWorksheet(IXLWorksheet worksheet, IReadOnlyList<DashboardHistoryRow> rows)
    {
        var headers = new[]
        {
            "date", "open", "high", "low", "close", "sar", "macd/signal", "macd/macd", "macd/divergence", "rsi",
            "bb/top", "bb/middle", "bb/bottom", "SAR_WAY_CHANGE", "SAR_JUMP_VALUE", "SAR_NOTIF_CHANGE_AND_GAMMA",
            "TREND_POSITION_ON_SAR", "RSI_STRENGTH_ABS", "BB_IS_BOTTOM_UP", "BB_MID_HIT_UP", "BB_MID_HIT_DOWN",
            "MACD_INVERSE", "MACD_TREND", "MACD_TREND_COUNT", "MACD_TREND_CHG", "COUNT_DAYS_SINCE_CHG_VENTE", "COUNT_DAYS_SINCE_CHG_ACHAT",
            "Signal", "SignalReason", "SignalDirection", "SignalCategory"
        };

        for (var i = 0; i < headers.Length; i++)
        {
            worksheet.Cell(1, i + 1).Value = headers[i];
        }

        var headerRange = worksheet.Range(1, 1, 1, headers.Length);
        headerRange.Style.Font.Bold = true;
        headerRange.Style.Fill.BackgroundColor = XLColor.LightGray;

        for (var rowIndex = 0; rowIndex < rows.Count; rowIndex++)
        {
            var source = rows[rowIndex];
            var excelRow = rowIndex + 2;

            worksheet.Cell(excelRow, 1).Value = source.Date.ToDateTime(TimeOnly.MinValue);
            worksheet.Cell(excelRow, 2).Value = source.Open;
            worksheet.Cell(excelRow, 3).Value = source.High;
            worksheet.Cell(excelRow, 4).Value = source.Low;
            worksheet.Cell(excelRow, 5).Value = source.Close;
            worksheet.Cell(excelRow, 6).Value = source.Sar;
            worksheet.Cell(excelRow, 7).Value = source.MacdSignal;
            worksheet.Cell(excelRow, 8).Value = source.MacdMacd;
            worksheet.Cell(excelRow, 9).Value = source.MacdDivergence;
            worksheet.Cell(excelRow, 10).Value = source.Rsi;
            worksheet.Cell(excelRow, 11).Value = source.BbTop;
            worksheet.Cell(excelRow, 12).Value = source.BbMiddle;
            worksheet.Cell(excelRow, 13).Value = source.BbBottom;
            worksheet.Cell(excelRow, 14).Value = source.SarWayChange;
            worksheet.Cell(excelRow, 15).Value = source.SarJumpValue;
            worksheet.Cell(excelRow, 16).Value = source.SarNotifChangeAndGamma;
            worksheet.Cell(excelRow, 17).Value = source.TrendPositionOnSar;
            worksheet.Cell(excelRow, 18).Value = source.RsiStrengthAbs;
            worksheet.Cell(excelRow, 19).Value = source.BbIsBottomUp;
            worksheet.Cell(excelRow, 20).Value = source.BbMidHitUp;
            worksheet.Cell(excelRow, 21).Value = source.BbMidHitDown;
            worksheet.Cell(excelRow, 22).Value = source.MacdInverse;
            worksheet.Cell(excelRow, 23).Value = source.MacdTrend;
            worksheet.Cell(excelRow, 24).Value = source.MacdTrendCount;
            worksheet.Cell(excelRow, 25).Value = source.MacdTrendChg;
            worksheet.Cell(excelRow, 26).Value = source.CountDaysSinceChgVente;
            worksheet.Cell(excelRow, 27).Value = source.CountDaysSinceChgAchat;
            worksheet.Cell(excelRow, 28).Value = source.SignalType.ToExportLabel();
            worksheet.Cell(excelRow, 29).Value = source.SignalReason;
            worksheet.Cell(excelRow, 30).Value = source.SignalDirection == SignalDirection.None ? string.Empty : source.SignalDirection.ToString().ToUpperInvariant();
            worksheet.Cell(excelRow, 31).Value = source.SignalCategory == SignalCategory.None ? string.Empty : source.SignalCategory.ToString();
        }

        worksheet.Column(1).Style.DateFormat.Format = "yyyy-mm-dd";
        worksheet.Columns(2, 15).Style.NumberFormat.Format = "0.0000";
        worksheet.Column(18).Style.NumberFormat.Format = "0";
        worksheet.Column(22).Style.NumberFormat.Format = "0";
        worksheet.Column(24).Style.NumberFormat.Format = "0";
        worksheet.Column(26).Style.NumberFormat.Format = "0";
        worksheet.Column(27).Style.NumberFormat.Format = "0";
        worksheet.SheetView.FreezeRows(1);
        worksheet.Columns().AdjustToContents();
    }
}
