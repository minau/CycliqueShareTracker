namespace CycliqueShareTracker.Application.Models;

public sealed record ScoreFactorDetail(
    string Label,
    int Points,
    bool Triggered,
    string? Description = null);
