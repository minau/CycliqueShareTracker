namespace CycliqueShareTracker.Application.Models;

public sealed record AlgorithmSignalPoint(
    DateOnly Date,
    bool IsBuyZone,
    bool IsSellZone,
    bool BuySignal,
    bool SellSignal,
    int? BuyScore,
    int? SellScore,
    decimal? Confidence,
    string BuyReason,
    string SellReason,
    IReadOnlyList<ScoreFactorDetail> BuyDetails,
    IReadOnlyList<ScoreFactorDetail> SellDetails)
{
    public IReadOnlyDictionary<string, object?> DebugValues { get; init; } = new Dictionary<string, object?>();
}
