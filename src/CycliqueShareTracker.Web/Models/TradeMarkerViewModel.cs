namespace CycliqueShareTracker.Web.Models;

public sealed class TradeMarkerViewModel
{
    public string Date { get; init; } = string.Empty;
    public string SignalType { get; init; } = string.Empty;
    public decimal Price { get; init; }
    public string Reason { get; init; } = string.Empty;
    public string Action { get; init; } = string.Empty;
    public string ResultingPosition { get; init; } = string.Empty;
}
