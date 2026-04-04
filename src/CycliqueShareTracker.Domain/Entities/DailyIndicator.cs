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
    public decimal? Ema12 { get; set; }
    public decimal? Ema26 { get; set; }
    public decimal? MacdLine { get; set; }
    public decimal? MacdSignalLine { get; set; }
    public decimal? MacdHistogram { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
