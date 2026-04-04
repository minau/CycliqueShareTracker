namespace CycliqueShareTracker.Web.Models;

public sealed class SignalScoreFactorViewModel
{
    public string Label { get; init; } = string.Empty;
    public int Points { get; init; }
    public bool Triggered { get; init; }
    public string? Description { get; init; }
}
