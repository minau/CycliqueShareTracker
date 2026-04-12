using CycliqueShareTracker.Application.Trading;

namespace CycliqueShareTracker.Application.Interfaces;

public interface IBacktestService
{
    IReadOnlyList<string> GetTrackedSymbols();
    Task<BacktestResult> RunAsync(BacktestParameters parameters, CancellationToken cancellationToken = default);
}
