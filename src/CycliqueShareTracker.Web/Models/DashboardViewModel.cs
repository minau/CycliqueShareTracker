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
    public decimal ParabolicSarStep { get; init; }
    public decimal ParabolicSarMax { get; init; }
    public int BollingerPeriod { get; init; }
    public decimal BollingerStdDev { get; init; }
    public int MacdFastPeriod { get; init; }
    public int MacdSlowPeriod { get; init; }
    public int MacdSignalPeriod { get; init; }
    public DateTime IndicatorSettingsUpdatedAtUtc { get; init; }
    public IReadOnlyList<DashboardChartPointViewModel> ChartPoints { get; init; } = Array.Empty<DashboardChartPointViewModel>();
    public IReadOnlyList<TradeMarkerViewModel> TradeMarkers { get; init; } = Array.Empty<TradeMarkerViewModel>();
    public PositionSide CurrentPositionSide { get; init; }
    public ProductType CurrentProduct { get; init; }
    public decimal CurrentQuantity { get; init; }
    public DateOnly? CurrentEntryDate { get; init; }
    public decimal? CurrentEntryPrice { get; init; }
    public string? CurrentProductId { get; init; }
    public IReadOnlyList<DashboardHistoryRowViewModel> HistoryRows { get; init; } = Array.Empty<DashboardHistoryRowViewModel>();

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
            ParabolicSarStep = snapshot.IndicatorSettings.ParabolicSarStep,
            ParabolicSarMax = snapshot.IndicatorSettings.ParabolicSarMax,
            BollingerPeriod = snapshot.IndicatorSettings.BollingerPeriod,
            BollingerStdDev = snapshot.IndicatorSettings.BollingerStdDev,
            MacdFastPeriod = snapshot.IndicatorSettings.MacdFastPeriod,
            MacdSlowPeriod = snapshot.IndicatorSettings.MacdSlowPeriod,
            MacdSignalPeriod = snapshot.IndicatorSettings.MacdSignalPeriod,
            IndicatorSettingsUpdatedAtUtc = snapshot.IndicatorSettings.UpdatedAtUtc,
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
            HistoryRows = snapshot.HistoryRows.Select(x => new DashboardHistoryRowViewModel
            {
                Date = x.Date.ToString("dd/MM/yyyy"),
                Open = x.Open,
                High = x.High,
                Low = x.Low,
                Close = x.Close,
                Sar = x.Sar,
                MacdSignal = x.MacdSignal,
                MacdMacd = x.MacdMacd,
                MacdDivergence = x.MacdDivergence,
                Rsi = x.Rsi,
                BbTop = x.BbTop,
                BbMiddle = x.BbMiddle,
                BbBottom = x.BbBottom,
                SarWayChange = x.SarWayChange,
                SarJumpValue = x.SarJumpValue,
                SarNotifChangeAndGamma = x.SarNotifChangeAndGamma,
                TrendPositionOnSar = x.TrendPositionOnSar,
                RsiStrengthAbs = x.RsiStrengthAbs,
                BbIsBottomUp = x.BbIsBottomUp,
                BbMidHitUp = x.BbMidHitUp,
                BbMidHitDown = x.BbMidHitDown,
                MacdInverse = x.MacdInverse,
                MacdTrend = x.MacdTrend,
                MacdTrendCount = x.MacdTrendCount,
                MacdTrendChg = x.MacdTrendChg,
                CountDaysSinceChgVente = x.CountDaysSinceChgVente,
                CountDaysSinceChgAchat = x.CountDaysSinceChgAchat
            }).ToList(),
            CurrentPositionSide = snapshot.CurrentPosition.Side,
            CurrentProduct = snapshot.CurrentPosition.Product,
            CurrentQuantity = snapshot.CurrentPosition.Quantity,
            CurrentEntryDate = snapshot.CurrentPosition.EntryDate,
            CurrentEntryPrice = snapshot.CurrentPosition.EntryPrice,
            CurrentProductId = snapshot.CurrentPosition.ProductId
        };
    }
}
