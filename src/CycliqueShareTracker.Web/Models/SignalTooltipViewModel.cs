namespace CycliqueShareTracker.Web.Models;

public sealed class SignalTooltipViewModel
{
    public string Title { get; init; } = "N/A";
    public int? Score { get; init; }
    public string PrimaryReason { get; init; } = "N/A";
    public IReadOnlyList<SignalScoreFactorViewModel> Factors { get; init; } = Array.Empty<SignalScoreFactorViewModel>();
}
