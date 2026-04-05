using CycliqueShareTracker.Application.Interfaces;
using CycliqueShareTracker.Domain.Enums;
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
    private readonly ILogger<HomeController> _logger;

    public HomeController(
        IDashboardService dashboardService,
        IDataSyncService dataSyncService,
        IBacktestService backtestService,
        ILogger<HomeController> logger)
    {
        _dashboardService = dashboardService;
        _dataSyncService = dataSyncService;
        _backtestService = backtestService;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> Index(
        [FromQuery] bool includeMacdInScoring = true,
        [FromQuery] string sortBy = "buy",
        [FromQuery] string filter = "all",
        CancellationToken cancellationToken = default)
    {
        var snapshots = await _dashboardService.GetWatchlistSnapshotsAsync(includeMacdInScoring, cancellationToken);

        if (snapshots.All(x => x.Snapshot?.LastClose is null && string.IsNullOrWhiteSpace(x.Error)))
        {
            await _dataSyncService.RunDailyUpdateAsync(cancellationToken);
            snapshots = await _dashboardService.GetWatchlistSnapshotsAsync(includeMacdInScoring, cancellationToken);
        }

        var items = snapshots.Select(item =>
        {
            var snapshot = item.Snapshot;
            var status = ResolveStatus(snapshot?.SignalLabel, snapshot?.ExitSignalLabel);
            var primaryReason = status == "Sell candidate"
                ? snapshot?.ExitPrimaryReason
                : snapshot?.EntryPrimaryReason;

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
                BuyScore = snapshot?.Score,
                SellScore = snapshot?.ExitScore,
                Status = status,
                PrimaryReason = string.IsNullOrWhiteSpace(primaryReason) ? "N/A" : primaryReason,
                Error = item.Error ?? fallbackError
            };
        });

        var filtered = ApplyFilter(items, filter);
        var ordered = ApplySort(filtered, sortBy).ToList();

        var model = new WatchlistViewModel
        {
            IncludeMacdInScoring = includeMacdInScoring,
            SortBy = sortBy,
            Filter = filter,
            TopBuySymbol = items.OrderByDescending(x => x.BuyScore ?? int.MinValue).FirstOrDefault()?.Symbol,
            TopSellSymbol = items.OrderByDescending(x => x.SellScore ?? int.MinValue).FirstOrDefault()?.Symbol,
            Items = ordered
        };

        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> Detail([FromQuery] string symbol, [FromQuery] bool includeMacdInScoring = true, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(symbol))
        {
            return RedirectToAction(nameof(Index), new { includeMacdInScoring });
        }

        var trackedAsset = _dashboardService.GetTrackedAssets().FirstOrDefault(x => string.Equals(x.Symbol, symbol, StringComparison.OrdinalIgnoreCase));
        if (trackedAsset is null)
        {
            return NotFound();
        }

        var snapshot = await _dashboardService.GetSnapshotAsync(trackedAsset.Symbol, includeMacdInScoring, cancellationToken);

        if (snapshot.LastClose is null)
        {
            await _dataSyncService.RunDailyUpdateAsync(cancellationToken);
            snapshot = await _dashboardService.GetSnapshotAsync(trackedAsset.Symbol, includeMacdInScoring, cancellationToken);
        }

        var notice = snapshot.LastClose is null
            ? "Aucune donnée marché disponible actuellement pour cette action. Vérifiez la configuration provider/symbol map et relancez une mise à jour."
            : null;

        var model = DashboardViewModel.FromSnapshot(snapshot, includeMacdInScoring, trackedAsset.Sector, notice);
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Refresh(string? symbol = null, bool includeMacdInScoring = true, CancellationToken cancellationToken = default)
    {
        await _dataSyncService.RunDailyUpdateAsync(cancellationToken);
        if (!string.IsNullOrWhiteSpace(symbol))
        {
            return RedirectToAction(nameof(Detail), new { symbol, includeMacdInScoring });
        }

        return RedirectToAction(nameof(Index), new { includeMacdInScoring });
    }



    [HttpGet]
    public async Task<IActionResult> Backtest(
        [FromQuery] string symbol = "__WATCHLIST__",
        [FromQuery] string? startDate = null,
        [FromQuery] string? endDate = null,
        [FromQuery] bool includeMacdInScoring = true,
        [FromQuery] bool runBacktest = false,
        CancellationToken cancellationToken = default)
    {
        var trackedAssets = _dashboardService.GetTrackedAssets();
        var end = DateOnly.TryParse(endDate, out var parsedEnd) ? parsedEnd : DateOnly.FromDateTime(DateTime.UtcNow);
        var start = DateOnly.TryParse(startDate, out var parsedStart) ? parsedStart : end.AddYears(-3);

        var model = new BacktestPageViewModel
        {
            SelectedSymbol = string.IsNullOrWhiteSpace(symbol) ? "__WATCHLIST__" : symbol,
            StartDate = start,
            EndDate = end,
            IncludeMacdInScoring = includeMacdInScoring,
            SymbolOptions = new List<Microsoft.AspNetCore.Mvc.Rendering.SelectListItem>
            {
                new("Watchlist complète", "__WATCHLIST__")
            }.Concat(trackedAssets.Select(asset => new Microsoft.AspNetCore.Mvc.Rendering.SelectListItem($"{asset.Symbol} - {asset.Name}", asset.Symbol))).ToList()
        };

        if (runBacktest)
        {
            try
            {
                var symbols = model.SelectedSymbol == "__WATCHLIST__"
                    ? trackedAssets.Select(x => x.Symbol).ToList()
                    : new List<string> { model.SelectedSymbol };

                _logger.LogInformation("Backtest requested. Symbols={Symbols}; Start={StartDate}; End={EndDate}; IncludeMacd={IncludeMacd}",
                    string.Join(",", symbols), model.StartDate, model.EndDate, model.IncludeMacdInScoring);

                var request = new CycliqueShareTracker.Application.Models.BacktestRequest(
                    model.StartDate,
                    model.EndDate,
                    symbols,
                    model.IncludeMacdInScoring);

                model.Result = await _backtestService.RunAsync(request, cancellationToken);

                if (model.Result.Assets.All(a => a.Metrics.TotalTrades == 0 && !string.IsNullOrWhiteSpace(a.Error)))
                {
                    _logger.LogWarning("Backtest returned no usable data for requested symbols. Triggering daily sync and retrying once.");
                    await _dataSyncService.RunDailyUpdateAsync(cancellationToken);
                    model.Result = await _backtestService.RunAsync(request, cancellationToken);
                }

                _logger.LogInformation("Backtest completed. AggregateTrades={Trades}; AggregatePerf={Performance}; Assets={AssetCount}",
                    model.Result.AggregateMetrics.TotalTrades,
                    model.Result.AggregateMetrics.TotalPerformancePercent,
                    model.Result.Assets.Count);

                model.HasExecuted = true;
                model.ExecutedAtUtc = DateTime.UtcNow;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Backtest execution failed for symbol selection {SelectedSymbol}", model.SelectedSymbol);
                model.Error = ex.Message;
                model.HasExecuted = true;
            }
        }

        return View(model);
    }

    [HttpGet]
    public IActionResult Documentation([FromQuery] bool includeMacdInScoring = true)
    {
        return View(includeMacdInScoring);
    }

    [HttpGet]
    public async Task<IActionResult> SignalHistory([FromQuery] string symbol, [FromQuery] bool includeMacdInScoring = true, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(symbol))
        {
            return RedirectToAction(nameof(Index), new { includeMacdInScoring });
        }

        var trackedAsset = _dashboardService.GetTrackedAssets().FirstOrDefault(x => string.Equals(x.Symbol, symbol, StringComparison.OrdinalIgnoreCase));
        if (trackedAsset is null)
        {
            return NotFound();
        }

        var snapshot = await _dashboardService.GetSnapshotAsync(trackedAsset.Symbol, includeMacdInScoring, cancellationToken);
        var history = await _dashboardService.GetSignalHistoryAsync(trackedAsset.Symbol, includeMacdInScoring, cancellationToken);

        var model = new SignalHistoryViewModel
        {
            AssetSymbol = snapshot.AssetSymbol,
            AssetName = snapshot.AssetName,
            IncludeMacdInScoring = includeMacdInScoring,
            Rows = history.Select(row => new SignalHistoryRowViewModel
            {
                Date = row.Date.ToString("dd/MM/yyyy"),
                Close = row.Close,
                Sma50 = row.Sma50,
                Sma200 = row.Sma200,
                Rsi14 = row.Rsi14,
                Drawdown52WeeksPercent = row.Drawdown52WeeksPercent,
                Score = row.Score,
                Signal = DashboardViewModel.FormatSignal(row.SignalLabel?.ToString()),
                EntryPrimaryReason = string.IsNullOrWhiteSpace(row.EntryPrimaryReason) ? "N/A" : row.EntryPrimaryReason,
                EntryTooltip = new SignalTooltipViewModel
                {
                    Title = DashboardViewModel.FormatSignal(row.SignalLabel?.ToString()),
                    Score = row.Score,
                    PrimaryReason = string.IsNullOrWhiteSpace(row.EntryPrimaryReason) ? "N/A" : row.EntryPrimaryReason,
                    Factors = row.EntryScoreFactors.Select(f => new SignalScoreFactorViewModel
                    {
                        Label = f.Label,
                        Points = f.Points,
                        Triggered = f.Triggered,
                        Description = f.Description
                    }).ToList()
                },
                ExitScore = row.ExitScore,
                ExitSignal = DashboardViewModel.FormatExitSignal(row.ExitSignalLabel?.ToString()),
                ExitPrimaryReason = string.IsNullOrWhiteSpace(row.ExitPrimaryReason) ? "N/A" : row.ExitPrimaryReason,
                ExitTooltip = new SignalTooltipViewModel
                {
                    Title = DashboardViewModel.FormatExitSignal(row.ExitSignalLabel?.ToString()),
                    Score = row.ExitScore,
                    PrimaryReason = string.IsNullOrWhiteSpace(row.ExitPrimaryReason) ? "N/A" : row.ExitPrimaryReason,
                    Factors = row.ExitScoreFactors.Select(f => new SignalScoreFactorViewModel
                    {
                        Label = f.Label,
                        Points = f.Points,
                        Triggered = f.Triggered,
                        Description = f.Description
                    }).ToList()
                }
            }).ToList()
        };

        return View(model);
    }

    private static IEnumerable<WatchlistItemViewModel> ApplySort(IEnumerable<WatchlistItemViewModel> items, string sortBy)
    {
        return sortBy?.ToLowerInvariant() switch
        {
            "sell" => items.OrderByDescending(x => x.SellScore ?? int.MinValue),
            "name" => items.OrderBy(x => x.Name),
            "ticker" => items.OrderBy(x => x.Symbol),
            _ => items.OrderByDescending(x => x.BuyScore ?? int.MinValue)
        };
    }

    private static IEnumerable<WatchlistItemViewModel> ApplyFilter(IEnumerable<WatchlistItemViewModel> items, string filter)
    {
        return filter?.ToLowerInvariant() switch
        {
            "buy" => items.Where(x => x.Status == "Buy candidate"),
            "sell" => items.Where(x => x.Status == "Sell candidate"),
            "neutral" => items.Where(x => x.Status == "Neutral"),
            _ => items
        };
    }

    private static string ResolveStatus(SignalLabel? entrySignal, ExitSignalLabel? exitSignal)
    {
        if (exitSignal == ExitSignalLabel.SellZone)
        {
            return "Sell candidate";
        }

        if (entrySignal == SignalLabel.BuyZone)
        {
            return "Buy candidate";
        }

        return "Neutral";
    }
}
