using CycliqueShareTracker.Application.Models;

namespace CycliqueShareTracker.Web.Models;

public sealed class DashboardViewModel
{
    public string AssetSector { get; init; } = "Unknown";
    public string AssetSymbol { get; init; } = string.Empty;
    public string AssetName { get; init; } = string.Empty;
    public DateOnly? LastUpdateDate { get; init; }
    public decimal? LastClose { get; init; }
    public decimal? DayChangePercent { get; init; }
    public decimal? Sma50 { get; init; }
    public decimal? Sma200 { get; init; }
    public decimal? Ema12 { get; init; }
    public decimal? Ema26 { get; init; }
    public decimal? Rsi14 { get; init; }
    public decimal? Drawdown52WeeksPercent { get; init; }
    public decimal? MacdLine { get; init; }
    public decimal? MacdSignalLine { get; init; }
    public decimal? MacdHistogram { get; init; }
    public int? Score { get; init; }
    public string Signal { get; init; } = "N/A";
    public string EntryPrimaryReason { get; init; } = "N/A";
    public IReadOnlyList<SignalScoreFactorViewModel> EntryScoreFactors { get; init; } = Array.Empty<SignalScoreFactorViewModel>();
    public int? ExitScore { get; init; }
    public string ExitSignal { get; init; } = "N/A";
    public string ExitPrimaryReason { get; init; } = "N/A";
    public IReadOnlyList<SignalScoreFactorViewModel> ExitScoreFactors { get; init; } = Array.Empty<SignalScoreFactorViewModel>();
    public bool IncludeMacdInScoring { get; init; } = true;
    public SignalTooltipViewModel EntryTooltip { get; init; } = new();
    public SignalTooltipViewModel ExitTooltip { get; init; } = new();
    public string? Notice { get; init; }
    public IReadOnlyList<DashboardChartPointViewModel> ChartPoints { get; init; } = Array.Empty<DashboardChartPointViewModel>();
    public IReadOnlyList<PriceBar> RecentPrices { get; init; } = Array.Empty<PriceBar>();

    public static DashboardViewModel FromSnapshot(DashboardSnapshot snapshot, bool includeMacdInScoring, string assetSector, string? notice = null)
    {
        return new DashboardViewModel
        {
            AssetSector = assetSector,
            AssetSymbol = snapshot.AssetSymbol,
            AssetName = snapshot.AssetName,
            LastUpdateDate = snapshot.LastUpdateDate,
            LastClose = snapshot.LastClose,
            DayChangePercent = snapshot.DayChangePercent,
            Sma50 = snapshot.Sma50,
            Sma200 = snapshot.Sma200,
            Ema12 = snapshot.Ema12,
            Ema26 = snapshot.Ema26,
            Rsi14 = snapshot.Rsi14,
            Drawdown52WeeksPercent = snapshot.Drawdown52WeeksPercent,
            MacdLine = snapshot.MacdLine,
            MacdSignalLine = snapshot.MacdSignalLine,
            MacdHistogram = snapshot.MacdHistogram,
            Score = snapshot.Score,
            Signal = FormatSignal(snapshot.SignalLabel?.ToString()),
            EntryPrimaryReason = string.IsNullOrWhiteSpace(snapshot.EntryPrimaryReason) ? "N/A" : snapshot.EntryPrimaryReason,
            EntryScoreFactors = snapshot.EntryScoreFactors.Select(MapFactor).ToList(),
            ExitScore = snapshot.ExitScore,
            ExitSignal = FormatExitSignal(snapshot.ExitSignalLabel?.ToString()),
            ExitPrimaryReason = string.IsNullOrWhiteSpace(snapshot.ExitPrimaryReason) ? "N/A" : snapshot.ExitPrimaryReason,
            ExitScoreFactors = snapshot.ExitScoreFactors.Select(MapFactor).ToList(),
            IncludeMacdInScoring = includeMacdInScoring,
            EntryTooltip = new SignalTooltipViewModel
            {
                Title = FormatSignal(snapshot.SignalLabel?.ToString()),
                Score = snapshot.Score,
                PrimaryReason = string.IsNullOrWhiteSpace(snapshot.EntryPrimaryReason) ? "N/A" : snapshot.EntryPrimaryReason,
                Factors = snapshot.EntryScoreFactors.Select(MapFactor).ToList()
            },
            ExitTooltip = new SignalTooltipViewModel
            {
                Title = FormatExitSignal(snapshot.ExitSignalLabel?.ToString()),
                Score = snapshot.ExitScore,
                PrimaryReason = string.IsNullOrWhiteSpace(snapshot.ExitPrimaryReason) ? "N/A" : snapshot.ExitPrimaryReason,
                Factors = snapshot.ExitScoreFactors.Select(MapFactor).ToList()
            },
            Notice = notice,
            ChartPoints = snapshot.ChartPoints.Select(x => new DashboardChartPointViewModel
            {
                Date = x.Date.ToString("yyyy-MM-dd"),
                Open = x.Open,
                High = x.High,
                Low = x.Low,
                Close = x.Close,
                Sma50 = x.Sma50,
                Sma200 = x.Sma200,
                Rsi14 = x.Rsi14,
                MacdLine = x.MacdLine,
                MacdSignalLine = x.MacdSignalLine,
                MacdHistogram = x.MacdHistogram,
                EntryScore = x.EntryScore,
                EntryPrimaryReason = string.IsNullOrWhiteSpace(x.EntryPrimaryReason) ? "N/A" : x.EntryPrimaryReason,
                EntryScoreFactors = x.EntryScoreFactors.Select(MapFactor).ToList(),
                IsBuyZone = string.Equals(x.SignalLabel?.ToString(), "BuyZone", StringComparison.Ordinal),
                ExitScore = x.ExitScore,
                ExitPrimaryReason = string.IsNullOrWhiteSpace(x.ExitPrimaryReason) ? "N/A" : x.ExitPrimaryReason,
                ExitScoreFactors = x.ExitScoreFactors.Select(MapFactor).ToList(),
                IsSellZone = string.Equals(x.ExitSignalLabel?.ToString(), "SellZone", StringComparison.Ordinal)
            }).ToList(),
            RecentPrices = snapshot.RecentPrices
        };
    }

    public static string FormatSignal(string? signal)
    {
        return signal?.ToUpperInvariant().Replace("NOBUY", "NO BUY").Replace("BUYZONE", "BUY ZONE") ?? "N/A";
    }

    public static string FormatExitSignal(string? signal)
    {
        return signal?.ToUpperInvariant().Replace("TRIMTAKEPROFIT", "TRIM / TAKE PROFIT").Replace("SELLZONE", "SELL ZONE") ?? "N/A";
    }

    private static SignalScoreFactorViewModel MapFactor(ScoreFactorDetail factor)
    {
        return new SignalScoreFactorViewModel
        {
            Label = factor.Label,
            Points = factor.Points,
            Triggered = factor.Triggered,
            Description = factor.Description
        };
    }
}
