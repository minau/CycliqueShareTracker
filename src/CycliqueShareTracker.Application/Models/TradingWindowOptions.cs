namespace CycliqueShareTracker.Application.Models;

public sealed class TradingWindowOptions
{
    public const string SectionName = "TradingWindow";

    public string TimeZoneId { get; set; } = "Europe/Paris";
    public string Start { get; set; } = "18:04";
    public string End { get; set; } = "18:20";
}
