namespace CycliqueShareTracker.Application.Trading;

public enum TradeSignalType
{
    None = 0,
    Long = 1,
    Short = 2,
    LeaveLong = 3,
    LeaveShort = 4
}

public enum PositionSide
{
    None = 0,
    Long = 1,
    Short = 2
}

public enum ProductType
{
    None = 0,
    Call = 1,
    Put = 2
}

public enum TradeActionType
{
    None = 0,
    BuyCall = 1,
    SellCall = 2,
    BuyPut = 3,
    SellPut = 4
}

public enum TradeExecutionStatus
{
    Executed = 0,
    PendingWindow = 1,
    AlreadyProcessed = 2,
    NoAction = 3
}

public sealed record TradeSignal(
    DateOnly Date,
    TradeSignalType Type,
    string Reason,
    bool IsEntry,
    bool IsExit);

public sealed record TradeAction(
    DateOnly Date,
    TradeSignalType SignalType,
    TradeActionType ActionType,
    string Reason,
    TradeExecutionStatus Status);

public sealed record TrackedPosition(
    string Symbol,
    PositionSide Side,
    ProductType Product,
    decimal Quantity,
    DateOnly? EntryDate,
    decimal? EntryPrice,
    string? ProductId)
{
    public static TrackedPosition Empty(string symbol) => new(symbol, PositionSide.None, ProductType.None, 0m, null, null, null);
}

public sealed record TradeMarker(
    DateOnly Date,
    TradeSignalType SignalType,
    decimal Price,
    string Reason,
    string Action,
    string ResultingPosition);

public sealed record DailySignalResult(
    DateOnly Date,
    TradeSignal? EntrySignal,
    TradeSignal? ExitSignal,
    IReadOnlyList<TradeAction> Actions,
    TrackedPosition PositionAfter,
    bool IsInTradingWindow,
    bool IsPending);
