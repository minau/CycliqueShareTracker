namespace CycliqueShareTracker.Domain.Entities;

public class DailyPrice
{
    public int Id { get; set; }
    public int AssetId { get; set; }
    public DateOnly Date { get; set; }
    public decimal Open { get; set; }
    public decimal High { get; set; }
    public decimal Low { get; set; }
    public decimal Close { get; set; }
    public long Volume { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public Asset? Asset { get; set; }
}
