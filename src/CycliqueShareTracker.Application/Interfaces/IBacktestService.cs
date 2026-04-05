using CycliqueShareTracker.Application.Models;

namespace CycliqueShareTracker.Application.Interfaces;

public interface IBacktestService
{
    Task<BacktestResult> RunAsync(BacktestRequest request, CancellationToken cancellationToken = default);
}
