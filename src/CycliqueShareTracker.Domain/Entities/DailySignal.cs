using CycliqueShareTracker.Domain.Enums;

namespace CycliqueShareTracker.Domain.Entities;

public class DailySignal
{
    public int Id { get; set; }
    public int AssetId { get; set; }
    public DateOnly Date { get; set; }
    public int Score { get; set; }
    public SignalLabel SignalLabel { get; set; }
    public string Explanation { get; set; } = string.Empty;
    public int ExitScore { get; set; }
    public ExitSignalLabel ExitSignalLabel { get; set; }
    public string ExitPrimaryReason { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
