using CycliqueShareTracker.Application.Interfaces;
using CycliqueShareTracker.Web.Configuration;
using Microsoft.Extensions.Options;

namespace CycliqueShareTracker.Web.Background;

public sealed class DailyUpdateBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<DailyUpdateBackgroundService> _logger;
    private readonly SchedulerOptions _options;

    public DailyUpdateBackgroundService(
        IServiceProvider serviceProvider,
        IOptions<SchedulerOptions> options,
        ILogger<DailyUpdateBackgroundService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _options = options.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (_options.RunOnStartup)
        {
            await RunOnce(stoppingToken);
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            var delay = GetDelayUntilNextRunUtc(_options.DailyRunTimeUtc);
            _logger.LogInformation("Next daily update in {Delay}", delay);
            await Task.Delay(delay, stoppingToken);
            await RunOnce(stoppingToken);
        }
    }

    private async Task RunOnce(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var syncService = scope.ServiceProvider.GetRequiredService<IDataSyncService>();
            await syncService.RunDailyUpdateAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Daily update background run failed");
        }
    }

    private static TimeSpan GetDelayUntilNextRunUtc(string time)
    {
        if (!TimeOnly.TryParse(time, out var runTime))
        {
            runTime = new TimeOnly(18, 0);
        }

        var now = DateTime.UtcNow;
        var next = now.Date.Add(runTime.ToTimeSpan());
        if (next <= now)
        {
            next = next.AddDays(1);
        }

        return next - now;
    }
}
