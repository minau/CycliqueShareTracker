using CycliqueShareTracker.Application.Interfaces;
using CycliqueShareTracker.Application.Models;
using CycliqueShareTracker.Domain.Enums;

namespace CycliqueShareTracker.Application.Services;

public sealed class ExitSignalService : IExitSignalService
{
    public ExitSignalResult BuildExitSignal(ComputedIndicator current, ComputedIndicator? previous)
    {
        var score = 0;
        var mainReason = "Aucun signal de sortie fort détecté.";

        if (current.Rsi14 is >= 75m)
        {
            score += 25;
            mainReason = "RSI14 en zone de surachat (>= 75).";
        }

        if (current.Sma50.HasValue && current.Sma50.Value != 0)
        {
            var distanceToSma50Percent = ((current.Close / current.Sma50.Value) - 1m) * 100m;
            if (distanceToSma50Percent >= 12m)
            {
                score += 20;
                if (score >= 20)
                {
                    mainReason = "Prix trop éloigné de la SMA50 (> 12%).";
                }
            }
        }

        if (previous is not null && current.PreviousClose.HasValue && current.Close < current.PreviousClose.Value)
        {
            var previousRsi = previous.Rsi14;
            var currentRsi = current.Rsi14;

            if (previousRsi is >= 70m && currentRsi.HasValue && currentRsi.Value < previousRsi.Value)
            {
                score += 20;
                if (score >= 20)
                {
                    mainReason = "Excès haussier suivi d'un retournement baissier.";
                }
            }
        }

        if (previous is not null && current.PreviousClose.HasValue && current.Close < current.PreviousClose.Value)
        {
            score += 15;
            if (score >= 15)
            {
                mainReason = "Dégradation du momentum court terme (clôture sous la veille).";
            }
        }

        if (current.Sma50.HasValue && current.Close < current.Sma50.Value)
        {
            score += 25;
            mainReason = "Cassure sous SMA50.";
        }

        if (current.Sma200.HasValue && current.Close < current.Sma200.Value)
        {
            score += 30;
            mainReason = "Cassure sous SMA200 (tendance fragilisée).";
        }

        score = Math.Clamp(score, 0, 100);

        var label = score switch
        {
            <= 34 => ExitSignalLabel.Hold,
            <= 64 => ExitSignalLabel.TrimTakeProfit,
            _ => ExitSignalLabel.SellZone
        };

        return new ExitSignalResult(score, label, mainReason);
    }
}
