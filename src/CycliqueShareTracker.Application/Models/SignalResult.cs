using CycliqueShareTracker.Domain.Enums;

namespace CycliqueShareTracker.Application.Models;

public sealed record SignalResult(int Score, SignalLabel Label, string Explanation);
