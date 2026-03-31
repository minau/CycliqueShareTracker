namespace CycliqueShareTracker.Application.Interfaces;

public interface IDataSyncService
{
    Task RunDailyUpdateAsync(CancellationToken cancellationToken = default);
}
