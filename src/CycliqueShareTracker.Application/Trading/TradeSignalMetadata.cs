namespace CycliqueShareTracker.Application.Trading;

public static class TradeSignalMetadata
{
    public const string SarReversalDetected = "SAR reversal detected";
    public const string RsiValidated = "RSI filter validated";
    public const string MacdValidated = "MACD filter validated";
    public const string ExitTargetReached = "Exit target reached";
    public const string TrendWeakening = "Trend weakening";
    public const string MomentumBreakdown = "Momentum breakdown";

    // Priority order is explicit and must remain stable for deterministic outputs:
    // 1) Exit target reached
    // 2) Trend weakening
    // 3) Momentum breakdown
    // 4) Generic fallback
    public static string BuildSignalReason(IReadOnlyList<string> reasons)
    {
        if (reasons.Count == 0)
        {
            return "No actionable condition detected";
        }

        if (reasons.Contains(ExitTargetReached, StringComparer.Ordinal))
        {
            return ExitTargetReached;
        }

        if (reasons.Contains(TrendWeakening, StringComparer.Ordinal))
        {
            return TrendWeakening;
        }

        if (reasons.Contains(MomentumBreakdown, StringComparer.Ordinal))
        {
            return MomentumBreakdown;
        }

        return reasons[0];
    }

    public static SignalDirection GetDirection(TradeSignalType type)
        => type switch
        {
            TradeSignalType.Long or TradeSignalType.LeaveLong => SignalDirection.Long,
            TradeSignalType.Short or TradeSignalType.LeaveShort => SignalDirection.Short,
            _ => SignalDirection.None
        };

    public static SignalCategory GetCategory(TradeSignalType type)
        => type switch
        {
            TradeSignalType.Long or TradeSignalType.Short => SignalCategory.Entry,
            TradeSignalType.LeaveLong or TradeSignalType.LeaveShort => SignalCategory.Exit,
            _ => SignalCategory.None
        };

    public static string ToDisplayLabel(this TradeSignalType type)
        => type switch
        {
            TradeSignalType.Long => "BUY LONG",
            TradeSignalType.LeaveLong => "SELL LONG",
            TradeSignalType.Short => "BUY SHORT",
            TradeSignalType.LeaveShort => "SELL SHORT",
            _ => "NONE"
        };

    public static string ToExportLabel(this TradeSignalType type) => type.ToDisplayLabel();

    public static string ToReasonText(this IReadOnlyList<string> reasons)
        => reasons.Count == 0 ? string.Empty : string.Join(" | ", reasons);
}
