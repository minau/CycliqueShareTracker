using CycliqueShareTracker.Application.Models;
using CycliqueShareTracker.Application.Services;
using CycliqueShareTracker.Domain.Enums;
using Microsoft.Extensions.Options;
using System;
using Xunit;

namespace CycliqueShareTracker.Tests;

public class SignalServiceTests
{
    private static SignalService CreateService(SignalStrategyOptions? options = null)
        => new(Options.Create(options ?? new SignalStrategyOptions()));

    [Fact]
    public void BuildSignal_ShouldReturnBuyZone_WhenAllFiltersPassAndScoreIsHigh()
    {
        var service = CreateService();
        var indicator = new ComputedIndicator(
            DateOnly.FromDateTime(DateTime.UtcNow),
            Sma50: 120,
            Sma200: 100,
            Rsi14: 52,
            Drawdown52WeeksPercent: -8,
            Close: 121,
            PreviousClose: 118,
            MacdLine: 1.2m,
            MacdSignalLine: 0.9m);

        var result = service.BuildSignal(indicator, includeMacdInScoring: true);

        Assert.Equal(100, result.Score);
        Assert.Equal(SignalLabel.BuyZone, result.Label);
        Assert.Contains("BUY validé", result.PrimaryReason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BuildSignal_ShouldNotReturnBuyZone_WhenRsiIsAboveConfiguredLimit()
    {
        var service = CreateService();
        var indicator = new ComputedIndicator(
            DateOnly.FromDateTime(DateTime.UtcNow),
            Sma50: 120,
            Sma200: 100,
            Rsi14: 66,
            Drawdown52WeeksPercent: -8,
            Close: 121,
            PreviousClose: 118,
            MacdLine: 1.2m,
            MacdSignalLine: 0.9m);

        var result = service.BuildSignal(indicator, includeMacdInScoring: true);

        Assert.NotEqual(SignalLabel.BuyZone, result.Label);
        Assert.Contains("RSI trop élevé", result.PrimaryReason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BuildSignal_ShouldIgnoreMacdConfirmation_WhenDisabledFromOption()
    {
        var service = CreateService(new SignalStrategyOptions { EnableMacdConfirmation = false });
        var indicator = new ComputedIndicator(
            DateOnly.FromDateTime(DateTime.UtcNow),
            Sma50: 120,
            Sma200: 100,
            Rsi14: 50,
            Drawdown52WeeksPercent: -8,
            Close: 121,
            PreviousClose: 118,
            MacdLine: 0.1m,
            MacdSignalLine: 0.4m);

        var result = service.BuildSignal(indicator, includeMacdInScoring: true);

        Assert.Equal(SignalLabel.BuyZone, result.Label);
    }
}
