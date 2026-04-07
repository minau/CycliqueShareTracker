using CycliqueShareTracker.Application.Models;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CycliqueShareTracker.Web.Models;

public sealed class BacktestPageViewModel
{
    public string SelectedSymbol { get; set; } = "";
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public bool IncludeMacdInScoring { get; set; } = true;
    public bool GenerateAnalysisJson { get; set; }
    public string SelectedAlgorithmType { get; set; } = "RsiMeanReversion";
    public string SelectedAlgorithmName { get; set; } = "RSI Mean Reversion";
    public IReadOnlyList<SelectListItem> SymbolOptions { get; set; } = Array.Empty<SelectListItem>();
    public IReadOnlyList<SelectListItem> AlgorithmOptions { get; set; } = Array.Empty<SelectListItem>();
    public BacktestResult? Result { get; set; }
    public bool HasExecuted { get; set; }
    public DateTime? ExecutedAtUtc { get; set; }
    public string? AnalysisJsonPath { get; set; }
    public string? AnalysisJsonDownloadUrl { get; set; }
    public string? Error { get; set; }
}
