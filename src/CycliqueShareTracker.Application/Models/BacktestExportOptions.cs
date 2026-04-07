namespace CycliqueShareTracker.Application.Models;

public sealed class BacktestExportOptions
{
    public const string SectionName = "BacktestExport";

    public string DirectoryPath { get; set; } = "/var/cyclique/exports";
}
