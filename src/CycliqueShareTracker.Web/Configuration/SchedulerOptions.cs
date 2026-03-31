namespace CycliqueShareTracker.Web.Configuration;

public sealed class SchedulerOptions
{
    public const string SectionName = "Scheduler";
    public string DailyRunTimeUtc { get; set; } = "18:00";
    public bool RunOnStartup { get; set; } = true;
}
