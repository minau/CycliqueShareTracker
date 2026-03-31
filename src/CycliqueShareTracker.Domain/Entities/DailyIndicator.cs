namespace CycliqueShareTracker.Domain.Entities;

public class DailyIndicator
{
    public int Id { get; set; }
    public int AssetId { get; set; }
    public DateOnly Date { get; set; }
    public decimal? Sma50 { get; set; }
    public decimal? Sma200 { get; set; }
    public decimal? Rsi14 { get; set; }
    public decimal? Drawdown52WeeksPercent { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
