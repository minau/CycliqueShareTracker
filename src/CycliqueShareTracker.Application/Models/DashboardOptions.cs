namespace CycliqueShareTracker.Application.Models;

public sealed class DashboardOptions
{
    public const string SectionName = "Dashboard";
    public int HistoryDays { get; set; } = 252;
}
