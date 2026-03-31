using CycliqueShareTracker.Application.Interfaces;
using CycliqueShareTracker.Application.Models;
using CycliqueShareTracker.Domain.Enums;

namespace CycliqueShareTracker.Application.Services;

public sealed class SignalService : ISignalService
{
    public SignalResult BuildSignal(ComputedIndicator indicator)
    {
        var score = 0;
        var reasons = new List<string>();

        if (indicator.Sma200.HasValue && indicator.Close > indicator.Sma200.Value)
        {
            score += 30;
            reasons.Add("Prix au-dessus de la SMA200");
        }

        if (indicator.Sma50.HasValue && indicator.Sma200.HasValue && indicator.Sma50.Value > indicator.Sma200.Value)
        {
            score += 20;
            reasons.Add("SMA50 au-dessus de la SMA200");
        }

        if (indicator.Rsi14.HasValue && indicator.Rsi14.Value >= 35m && indicator.Rsi14.Value <= 55m)
        {
            score += 20;
            reasons.Add("RSI14 entre 35 et 55");
        }

        if (indicator.Drawdown52WeeksPercent.HasValue && indicator.Drawdown52WeeksPercent.Value >= -15m && indicator.Drawdown52WeeksPercent.Value <= -5m)
        {
            score += 20;
            reasons.Add("Drawdown 52 semaines entre -15% et -5%");
        }

        if (indicator.PreviousClose.HasValue && indicator.Close > indicator.PreviousClose.Value)
        {
            score += 10;
            reasons.Add("Prix en hausse par rapport à la veille");
        }

        score = Math.Clamp(score, 0, 100);
        var label = score switch
        {
            <= 39 => SignalLabel.NoBuy,
            <= 69 => SignalLabel.Watch,
            _ => SignalLabel.BuyZone
        };

        var explanation = reasons.Count == 0
            ? "Aucun critère haussier validé."
            : string.Join("; ", reasons);

        return new SignalResult(score, label, explanation);
    }
}
