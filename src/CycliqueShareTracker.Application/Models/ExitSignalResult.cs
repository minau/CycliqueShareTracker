using CycliqueShareTracker.Domain.Enums;

namespace CycliqueShareTracker.Application.Models;

public sealed record ExitSignalResult(int ExitScore, ExitSignalLabel ExitSignal, string PrimaryExitReason);
