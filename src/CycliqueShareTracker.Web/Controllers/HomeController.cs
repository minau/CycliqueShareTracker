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
            ? "Aucune donnée marché disponible actuellement. Vérifiez l'accès réseau sortant vers Yahoo Finance et Alpha Vantage, puis relancez une mise à jour."
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
}
