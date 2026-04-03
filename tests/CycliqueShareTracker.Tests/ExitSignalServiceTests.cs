using CycliqueShareTracker.Application.Models;
using CycliqueShareTracker.Application.Services;
using CycliqueShareTracker.Domain.Enums;
using Xunit;

namespace CycliqueShareTracker.Tests;

public class ExitSignalServiceTests
{
    private readonly ExitSignalService _service = new();

    [Fact]
    public void BuildExitSignal_ShouldReturnHold_WhenNoExitCriteriaMet()
    {
        var current = new ComputedIndicator(
            DateOnly.FromDateTime(DateTime.UtcNow),
            Sma50: 100m,
            Sma200: 90m,
            Rsi14: 55m,
            Drawdown52WeeksPercent: -8m,
            Close: 101m,
            PreviousClose: 100m);

        var result = _service.BuildExitSignal(current, previous: null);

        Assert.Equal(0, result.ExitScore);
        Assert.Equal(ExitSignalLabel.Hold, result.ExitSignal);
        Assert.Equal("Aucun signal de sortie fort détecté.", result.PrimaryExitReason);
    }

    [Fact]
    public void BuildExitSignal_ShouldReturnTrimTakeProfit_WhenOverextensionCriteriaMet()
    {
        var current = new ComputedIndicator(
            DateOnly.FromDateTime(DateTime.UtcNow),
            Sma50: 100m,
            Sma200: 90m,
            Rsi14: 78m,
            Drawdown52WeeksPercent: -3m,
            Close: 114m,
            PreviousClose: 113m);

        var result = _service.BuildExitSignal(current, previous: null);

        Assert.Equal(45, result.ExitScore);
        Assert.Equal(ExitSignalLabel.TrimTakeProfit, result.ExitSignal);
    }

    [Fact]
    public void BuildExitSignal_ShouldReturnSellZone_WhenTrendBreakCriteriaMet()
    {
        var previous = new ComputedIndicator(new DateOnly(2026, 04, 01), 101m, 97m, 74m, -4m, 110m, 108m);
        var current = new ComputedIndicator(new DateOnly(2026, 04, 02), 100m, 105m, 68m, -6m, 99m, 110m);

        var result = _service.BuildExitSignal(current, previous);

        Assert.Equal(100, result.ExitScore);
        Assert.Equal(ExitSignalLabel.SellZone, result.ExitSignal);
        Assert.Equal("Cassure sous SMA200 (tendance fragilisée).", result.PrimaryExitReason);
    }

    [Fact]
    public void BuildExitSignal_ShouldRespectSignalBoundaries()
    {
        var hold = _service.BuildExitSignal(
            new ComputedIndicator(new DateOnly(2026, 04, 02), 100m, 90m, 50m, -8m, 100m, 99m),
            previous: null);

        var trim = _service.BuildExitSignal(
            new ComputedIndicator(new DateOnly(2026, 04, 02), 100m, 90m, 76m, -8m, 112m, 111m),
            previous: null);

        var sell = _service.BuildExitSignal(
            new ComputedIndicator(new DateOnly(2026, 04, 02), 100m, 95m, 76m, -8m, 90m, 100m),
            new ComputedIndicator(new DateOnly(2026, 04, 01), 101m, 96m, 80m, -6m, 100m, 99m));

        Assert.Equal(ExitSignalLabel.Hold, hold.ExitSignal);
        Assert.Equal(ExitSignalLabel.TrimTakeProfit, trim.ExitSignal);
        Assert.Equal(ExitSignalLabel.SellZone, sell.ExitSignal);
    }
}
