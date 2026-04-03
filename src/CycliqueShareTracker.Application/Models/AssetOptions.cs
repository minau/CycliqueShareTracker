namespace CycliqueShareTracker.Application.Models;

public sealed class AssetOptions
{
    public const string SectionName = "Asset";
    public string Symbol { get; set; } = "TTE.PA";
    public string Name { get; set; } = "TotalEnergies";
    public string Market { get; set; } = "Euronext Paris";
}
