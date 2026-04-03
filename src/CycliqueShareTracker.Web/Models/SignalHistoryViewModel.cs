namespace CycliqueShareTracker.Web.Models;

public sealed class SignalHistoryViewModel
{
    public string AssetSymbol { get; init; } = string.Empty;
    public string AssetName { get; init; } = string.Empty;
    public IReadOnlyList<SignalHistoryRowViewModel> Rows { get; init; } = Array.Empty<SignalHistoryRowViewModel>();
}
