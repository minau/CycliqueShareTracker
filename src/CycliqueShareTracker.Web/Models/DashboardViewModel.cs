using CycliqueShareTracker.Application.Models;
using CycliqueShareTracker.Application.Trading;

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
    public string ActiveAlgorithmType { get; init; } = "RsiMeanReversion";
    public string ActiveAlgorithmName { get; init; } = "RSI Mean Reversion";
    public string? Notice { get; init; }
    public IReadOnlyList<DashboardChartPointViewModel> ChartPoints { get; init; } = Array.Empty<DashboardChartPointViewModel>();
    public IReadOnlyList<TradeMarkerViewModel> TradeMarkers { get; init; } = Array.Empty<TradeMarkerViewModel>();
    public PositionSide CurrentPositionSide { get; init; }
    public ProductType CurrentProduct { get; init; }
    public decimal CurrentQuantity { get; init; }
    public DateOnly? CurrentEntryDate { get; init; }
    public decimal? CurrentEntryPrice { get; init; }
    public string? CurrentProductId { get; init; }
    public IReadOnlyList<PriceBar> RecentPrices { get; init; } = Array.Empty<PriceBar>();

    public static DashboardViewModel FromSnapshot(DashboardSnapshot snapshot, string assetSector, string? notice = null)
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
            ActiveAlgorithmType = snapshot.AlgorithmType.ToString(),
            ActiveAlgorithmName = snapshot.AlgorithmName,
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
                BollingerMiddle = x.BollingerMiddle,
                BollingerUpper = x.BollingerUpper,
                BollingerLower = x.BollingerLower,
                ParabolicSar = x.ParabolicSar
            }).ToList(),
            TradeMarkers = snapshot.TradeMarkers.Select(x => new TradeMarkerViewModel
            {
                Date = x.Date.ToString("yyyy-MM-dd"),
                SignalType = x.SignalType.ToString(),
                Price = x.Price,
                Reason = x.Reason,
                Action = x.Action,
                ResultingPosition = x.ResultingPosition
            }).ToList(),
            CurrentPositionSide = snapshot.CurrentPosition.Side,
            CurrentProduct = snapshot.CurrentPosition.Product,
            CurrentQuantity = snapshot.CurrentPosition.Quantity,
            CurrentEntryDate = snapshot.CurrentPosition.EntryDate,
            CurrentEntryPrice = snapshot.CurrentPosition.EntryPrice,
            CurrentProductId = snapshot.CurrentPosition.ProductId,
            RecentPrices = snapshot.RecentPrices
        };
    }
}
