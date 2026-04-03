using CycliqueShareTracker.Application.Interfaces;
using CycliqueShareTracker.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CycliqueShareTracker.Web.Controllers;

[Authorize]
public class HomeController : Controller
{
    private readonly IDashboardService _dashboardService;
    private readonly IDataSyncService _dataSyncService;

    public HomeController(IDashboardService dashboardService, IDataSyncService dataSyncService)
    {
        _dashboardService = dashboardService;
        _dataSyncService = dataSyncService;
    }

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var snapshot = await _dashboardService.GetSnapshotAsync(cancellationToken);

        if (snapshot.LastClose is null)
        {
            await _dataSyncService.RunDailyUpdateAsync(cancellationToken);
            snapshot = await _dashboardService.GetSnapshotAsync(cancellationToken);
        }

        var notice = snapshot.LastClose is null
            ? "Aucune donnée marché disponible actuellement. Vérifiez la configuration MarketData (provider principal/fallback), la clé API `MarketData__AlphaVantage__ApiKey` et l'accès réseau sortant, puis relancez une mise à jour."
            : null;

        var model = DashboardViewModel.FromSnapshot(snapshot, notice);
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Refresh(CancellationToken cancellationToken)
    {
        await _dataSyncService.RunDailyUpdateAsync(cancellationToken);
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> SignalHistory(CancellationToken cancellationToken)
    {
        var snapshot = await _dashboardService.GetSnapshotAsync(cancellationToken);
        var history = await _dashboardService.GetSignalHistoryAsync(cancellationToken);

        var model = new SignalHistoryViewModel
        {
            AssetSymbol = snapshot.AssetSymbol,
            AssetName = snapshot.AssetName,
            Rows = history.Select(row => new SignalHistoryRowViewModel
            {
                Date = row.Date.ToString("dd/MM/yyyy"),
                Close = row.Close,
                Sma50 = row.Sma50,
                Sma200 = row.Sma200,
                Rsi14 = row.Rsi14,
                Drawdown52WeeksPercent = row.Drawdown52WeeksPercent,
                Score = row.Score,
                Signal = DashboardViewModel.FormatSignal(row.SignalLabel?.ToString())
            }).ToList()
        };

        return View(model);
    }
}
