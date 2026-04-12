using CycliqueShareTracker.Application.Interfaces;
using CycliqueShareTracker.Application.Models;
using CycliqueShareTracker.Application.Trading;
using CycliqueShareTracker.Web.Models;
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

        var notice = snapshot.LastClose is null
            ? "Aucune donnée marché disponible actuellement pour cette action. Vérifiez la configuration provider/symbol map et relancez une mise à jour."
            : null;

        var model = DashboardViewModel.FromSnapshot(snapshot, trackedAsset.Sector, notice);
        return View(model);
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

    private static IEnumerable<WatchlistItemViewModel> ApplySort(IEnumerable<WatchlistItemViewModel> items, string sortBy)
    {
        return sortBy?.ToLowerInvariant() switch
        {
            "name" => items.OrderBy(x => x.Name),
            _ => items.OrderBy(x => x.Symbol)
        };
    }
}
