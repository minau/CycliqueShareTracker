namespace CycliqueShareTracker.Domain.Entities;

public class Asset
{
    public int Id { get; set; }
    public string Symbol { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Market { get; set; } = string.Empty;
    public ICollection<DailyPrice> DailyPrices { get; set; } = new List<DailyPrice>();
}
