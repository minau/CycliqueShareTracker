using CycliqueShareTracker.Application.Models;
using CycliqueShareTracker.Application.Services;
using CycliqueShareTracker.Domain.Enums;
using System.Linq;
using Xunit;

namespace CycliqueShareTracker.Tests;

public class SignalServiceTests
{
    private readonly SignalService _service = new();

    [Fact]
    public void BuildSignal_ShouldReturnBuyZone_WhenMostCriteriaAreMet()
    {
        var indicator = new ComputedIndicator(
            DateOnly.FromDateTime(DateTime.UtcNow),
            Sma50: 60,
            Sma200: 50,
            Rsi14: 45,
            Drawdown52WeeksPercent: -10,
            Close: 61,
            PreviousClose: 59,
            MacdLine: 1.2m,
            MacdSignalLine: 1.0m,
            MacdHistogram: 0.2m,
            PreviousMacdHistogram: 0.1m);

        var result = _service.BuildSignal(indicator);

        Assert.Equal(100, result.Score);
        Assert.Equal(SignalLabel.BuyZone, result.Label);
        Assert.Equal(9, result.ScoreFactors.Count);
        Assert.Equal(7, result.ScoreFactors.Count(x => x.Triggered));
        Assert.Equal("Tendance haussière de fond avec repli modéré.", result.PrimaryReason);
    }

    [Fact]
    public void BuildSignal_ShouldMapNoBuy_WhenScoreBelow40()
    {
        var indicator = new ComputedIndicator(
            DateOnly.FromDateTime(DateTime.UtcNow),
            Sma50: null,
            Sma200: 100,
            Rsi14: 20,
            Drawdown52WeeksPercent: -30,
            Close: 90,
            PreviousClose: 100,
            MacdLine: -0.5m,
            MacdSignalLine: -0.2m,
            MacdHistogram: -0.3m,
            PreviousMacdHistogram: -0.1m);

        var result = _service.BuildSignal(indicator);

        Assert.Equal(0, result.Score);
        Assert.Equal(SignalLabel.NoBuy, result.Label);
        Assert.Equal("Conditions d'entrée insuffisantes pour le moment.", result.PrimaryReason);
    }

    [Fact]
    public void BuildSignal_ShouldMapWatch_WhenScoreBetween40And69()
    {
        var indicator = new ComputedIndicator(
            DateOnly.FromDateTime(DateTime.UtcNow),
            Sma50: null,
            Sma200: 100,
            Rsi14: 40,
            Drawdown52WeeksPercent: -30,
            Close: 110,
            PreviousClose: 100,
            MacdLine: 0.5m,
            MacdSignalLine: 0.4m,
            MacdHistogram: 0.1m,
            PreviousMacdHistogram: 0.2m);

        var result = _service.BuildSignal(indicator);

        Assert.Equal(58, result.Score);
        Assert.Equal(SignalLabel.Watch, result.Label);
    }

    [Fact]
    public void BuildSignal_ShouldMapBuyZone_WhenScoreAtLeast70()
    {
        var indicator = new ComputedIndicator(
            DateOnly.FromDateTime(DateTime.UtcNow),
            Sma50: 101,
            Sma200: 100,
            Rsi14: 40,
            Drawdown52WeeksPercent: -30,
            Close: 110,
            PreviousClose: 100,
            MacdLine: 0.7m,
            MacdSignalLine: 0.5m,
            MacdHistogram: 0.2m,
            PreviousMacdHistogram: 0.1m);

        var result = _service.BuildSignal(indicator);

        Assert.Equal(94, result.Score);
        Assert.Equal(SignalLabel.BuyZone, result.Label);
    }

    [Fact]
    public void BuildSignal_ShouldStayBuyZone_WhenRsiOutOfRangeButOtherCriteriaStrong()
    {
        var indicator = new ComputedIndicator(
            DateOnly.FromDateTime(DateTime.UtcNow),
            Sma50: 105,
            Sma200: 100,
            Rsi14: 70,
            Drawdown52WeeksPercent: -12,
            Close: 106,
            PreviousClose: 103,
            MacdLine: 0.4m,
            MacdSignalLine: 0.5m,
            MacdHistogram: -0.1m,
            PreviousMacdHistogram: 0.0m);

        var result = _service.BuildSignal(indicator);

        Assert.Equal(74, result.Score);
        Assert.Equal(SignalLabel.BuyZone, result.Label);
    }
}
