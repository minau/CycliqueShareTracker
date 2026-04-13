using CycliqueShareTracker.Application.Models;

namespace CycliqueShareTracker.Application.Trading;

public sealed record BacktestParameters(
    string Symbol,
    DateOnly StartDate,
    DateOnly EndDate,
    decimal InitialCapital,
    decimal FixedAmountPerTrade,
    decimal FeePerTrade,
    decimal SlippagePercent,
    bool ForceCloseOnPeriodEnd)
{
    public static BacktestParameters Default(string symbol, DateOnly startDate, DateOnly endDate)
        => new(
            symbol,
            startDate,
            endDate,
            10_000m,
            1_000m,
            0m,
            0m,
            true);
}

public sealed record BacktestTrade(
    DateOnly EntryDate,
    DateOnly ExitDate,
    PositionSide Side,
    ProductType Product,
    decimal EntryPrice,
    decimal ExitPrice,
    decimal Quantity,
    decimal GrossPnl,
    decimal NetPnl,
    decimal ReturnPercent,
    string EntryReason,
    string ExitReason);

public sealed record BacktestSignalPoint(
    DateOnly Date,
    TradeSignalType SignalType,
    decimal Price,
    string Reason,
    string Color,
    string Shape);

public sealed record BacktestEquityPoint(
    DateOnly Date,
    decimal Equity);

public sealed record BacktestResult(
    BacktestParameters Parameters,
    decimal InitialCapital,
    decimal FinalCapital,
    decimal TotalPnl,
    decimal CumulativePerformancePercent,
    decimal AveragePnlPerTrade,
    int TotalTrades,
    int WinningTrades,
    int LosingTrades,
    decimal WinRatePercent,
    decimal MaxDrawdownPercent,
    IReadOnlyList<PriceBar> PriceSeries,
    IReadOnlyList<BacktestTrade> Trades,
    IReadOnlyList<BacktestSignalPoint> SignalMarkers,
    IReadOnlyList<BacktestEquityPoint> EquityCurve);
