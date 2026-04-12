using CycliqueShareTracker.Application.Trading;

namespace CycliqueShareTracker.Web.Models;

public sealed class BacktestViewModel
{
    public string SelectedSymbol { get; init; } = string.Empty;
    public DateOnly StartDate { get; init; }
    public DateOnly EndDate { get; init; }
    public decimal InitialCapital { get; init; }
    public decimal FixedAmountPerTrade { get; init; }
    public decimal FeePerTrade { get; init; }
    public decimal SlippagePercent { get; init; }
    public bool ForceCloseOnPeriodEnd { get; init; }
    public IReadOnlyList<string> AvailableSymbols { get; init; } = Array.Empty<string>();
    public bool HasResult { get; init; }
    public string? Notice { get; init; }
    public BacktestSummaryViewModel? Summary { get; init; }
    public IReadOnlyList<BacktestTradeViewModel> Trades { get; init; } = Array.Empty<BacktestTradeViewModel>();
    public IReadOnlyList<BacktestSignalMarkerViewModel> Markers { get; init; } = Array.Empty<BacktestSignalMarkerViewModel>();
    public IReadOnlyList<BacktestPricePointViewModel> PricePoints { get; init; } = Array.Empty<BacktestPricePointViewModel>();
    public IReadOnlyList<BacktestEquityPointViewModel> EquityPoints { get; init; } = Array.Empty<BacktestEquityPointViewModel>();

    public static BacktestViewModel CreateDefault(IReadOnlyList<string> symbols, DateOnly startDate, DateOnly endDate)
    {
        var selectedSymbol = symbols.FirstOrDefault() ?? string.Empty;
        return new BacktestViewModel
        {
            SelectedSymbol = selectedSymbol,
            StartDate = startDate,
            EndDate = endDate,
            InitialCapital = 10_000m,
            FixedAmountPerTrade = 1_000m,
            FeePerTrade = 0m,
            SlippagePercent = 0m,
            ForceCloseOnPeriodEnd = true,
            AvailableSymbols = symbols
        };
    }

    public static BacktestViewModel FromResult(BacktestResult result, IReadOnlyList<string> symbols)
    {
        return new BacktestViewModel
        {
            SelectedSymbol = result.Parameters.Symbol,
            StartDate = result.Parameters.StartDate,
            EndDate = result.Parameters.EndDate,
            InitialCapital = result.Parameters.InitialCapital,
            FixedAmountPerTrade = result.Parameters.FixedAmountPerTrade,
            FeePerTrade = result.Parameters.FeePerTrade,
            SlippagePercent = result.Parameters.SlippagePercent,
            ForceCloseOnPeriodEnd = result.Parameters.ForceCloseOnPeriodEnd,
            AvailableSymbols = symbols,
            HasResult = true,
            Summary = new BacktestSummaryViewModel
            {
                TotalTrades = result.TotalTrades,
                WinningTrades = result.WinningTrades,
                LosingTrades = result.LosingTrades,
                WinRatePercent = result.WinRatePercent,
                TotalPnl = result.TotalPnl,
                AveragePnlPerTrade = result.AveragePnlPerTrade,
                CumulativePerformancePercent = result.CumulativePerformancePercent,
                MaxDrawdownPercent = result.MaxDrawdownPercent,
                FinalCapital = result.FinalCapital
            },
            Trades = result.Trades.Select(x => new BacktestTradeViewModel
            {
                EntryDate = x.EntryDate.ToString("yyyy-MM-dd"),
                ExitDate = x.ExitDate.ToString("yyyy-MM-dd"),
                Side = x.Side.ToString(),
                Product = x.Product.ToString(),
                EntryPrice = x.EntryPrice,
                ExitPrice = x.ExitPrice,
                Quantity = x.Quantity,
                GrossPnl = x.GrossPnl,
                NetPnl = x.NetPnl,
                ReturnPercent = x.ReturnPercent,
                EntryReason = x.EntryReason,
                ExitReason = x.ExitReason
            }).ToList(),
            Markers = result.SignalMarkers.Select(x => new BacktestSignalMarkerViewModel
            {
                Date = x.Date.ToString("yyyy-MM-dd"),
                Price = x.Price,
                SignalType = x.SignalType.ToString(),
                Reason = x.Reason,
                Color = x.Color,
                Shape = x.Shape
            }).ToList(),
            PricePoints = result.PriceSeries.Select(x => new BacktestPricePointViewModel
            {
                Date = x.Date.ToString("yyyy-MM-dd"),
                Close = x.Close
            }).ToList(),
            EquityPoints = result.EquityCurve.Select(x => new BacktestEquityPointViewModel
            {
                Date = x.Date.ToString("yyyy-MM-dd"),
                Equity = x.Equity
            }).ToList()
        };
    }
}

public sealed class BacktestSummaryViewModel
{
    public int TotalTrades { get; init; }
    public int WinningTrades { get; init; }
    public int LosingTrades { get; init; }
    public decimal WinRatePercent { get; init; }
    public decimal TotalPnl { get; init; }
    public decimal AveragePnlPerTrade { get; init; }
    public decimal CumulativePerformancePercent { get; init; }
    public decimal MaxDrawdownPercent { get; init; }
    public decimal FinalCapital { get; init; }
}

public sealed class BacktestTradeViewModel
{
    public string EntryDate { get; init; } = string.Empty;
    public string ExitDate { get; init; } = string.Empty;
    public string Side { get; init; } = string.Empty;
    public string Product { get; init; } = string.Empty;
    public decimal EntryPrice { get; init; }
    public decimal ExitPrice { get; init; }
    public decimal Quantity { get; init; }
    public decimal GrossPnl { get; init; }
    public decimal NetPnl { get; init; }
    public decimal ReturnPercent { get; init; }
    public string EntryReason { get; init; } = string.Empty;
    public string ExitReason { get; init; } = string.Empty;
}

public sealed class BacktestSignalMarkerViewModel
{
    public string Date { get; init; } = string.Empty;
    public decimal Price { get; init; }
    public string SignalType { get; init; } = string.Empty;
    public string Reason { get; init; } = string.Empty;
    public string Color { get; init; } = "#64748b";
    public string Shape { get; init; } = "circle";
}

public sealed class BacktestPricePointViewModel
{
    public string Date { get; init; } = string.Empty;
    public decimal Close { get; init; }
}

public sealed class BacktestEquityPointViewModel
{
    public string Date { get; init; } = string.Empty;
    public decimal Equity { get; init; }
}
