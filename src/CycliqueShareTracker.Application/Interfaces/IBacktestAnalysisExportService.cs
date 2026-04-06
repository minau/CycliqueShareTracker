using CycliqueShareTracker.Application.Models;

namespace CycliqueShareTracker.Application.Interfaces;

public interface IBacktestAnalysisExportService
{
    Task<string> ExportAsync(
        BacktestResult result,
        string symbolSelection,
        DateTime? executedAtUtc,
        CancellationToken cancellationToken = default);
}
