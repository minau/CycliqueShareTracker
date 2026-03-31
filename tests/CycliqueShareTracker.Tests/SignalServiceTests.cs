using CycliqueShareTracker.Application.Models;
using CycliqueShareTracker.Application.Services;
using CycliqueShareTracker.Domain.Enums;

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
            PreviousClose: 59);

        var result = _service.BuildSignal(indicator);

        Assert.Equal(100, result.Score);
        Assert.Equal(SignalLabel.BuyZone, result.Label);
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
            PreviousClose: 100);

        var result = _service.BuildSignal(indicator);

        Assert.Equal(0, result.Score);
        Assert.Equal(SignalLabel.NoBuy, result.Label);
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
            PreviousClose: 100);

        var result = _service.BuildSignal(indicator);

        Assert.Equal(60, result.Score);
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
            PreviousClose: 100);

        var result = _service.BuildSignal(indicator);

        Assert.Equal(80, result.Score);
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
            PreviousClose: 103);

        var result = _service.BuildSignal(indicator);

        Assert.Equal(80, result.Score);
        Assert.Equal(SignalLabel.BuyZone, result.Label);
    }
}
