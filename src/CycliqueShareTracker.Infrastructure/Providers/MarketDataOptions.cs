namespace CycliqueShareTracker.Infrastructure.Providers;

public sealed class MarketDataOptions
{
    public const string SectionName = "MarketData";

    public string PrimaryProvider { get; set; } = YahooFinanceDataProvider.ProviderName;
    public string FallbackProvider { get; set; } = AlphaVantageDataProvider.ProviderName;
    public Dictionary<string, ProviderSymbolMap> SymbolMap { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public AlphaVantageProviderOptions AlphaVantage { get; set; } = new();
}

public sealed class ProviderSymbolMap
{
    public string? YahooFinance { get; set; }
    public string? AlphaVantage { get; set; }
}

public sealed class AlphaVantageProviderOptions
{
    public string? ApiKey { get; set; }
}
