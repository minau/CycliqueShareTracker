namespace CycliqueShareTracker.Web.Models;

public sealed class DashboardHistoryRowViewModel
{
    public string Date { get; init; } = string.Empty;
    public decimal Open { get; init; }
    public decimal High { get; init; }
    public decimal Low { get; init; }
    public decimal Close { get; init; }
    public decimal? Sar { get; init; }
    public decimal? MacdSignal { get; init; }
    public decimal? MacdMacd { get; init; }
    public decimal? MacdDivergence { get; init; }
    public decimal? Rsi { get; init; }
    public decimal? BbTop { get; init; }
    public decimal? BbMiddle { get; init; }
    public decimal? BbBottom { get; init; }
    public decimal? SarWayChange { get; init; }
    public decimal? SarJumpValue { get; init; }
    public string? SarNotifChangeAndGamma { get; init; }
    public string? TrendPositionOnSar { get; init; }
    public int? RsiStrengthAbs { get; init; }
    public string? BbIsBottomUp { get; init; }
    public string? BbMidHitUp { get; init; }
    public string? BbMidHitDown { get; init; }
    public int? MacdInverse { get; init; }
    public string? MacdTrend { get; init; }
    public int? MacdTrendCount { get; init; }
    public string? MacdTrendChg { get; init; }
    public int? CountDaysSinceChgVente { get; init; }
    public int? CountDaysSinceChgAchat { get; init; }
}
