using CycliqueShareTracker.Application.Models;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CycliqueShareTracker.Web.Models;

public sealed class BacktestPageViewModel
{
    public string SelectedSymbol { get; set; } = "";
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public bool IncludeMacdInScoring { get; set; } = true;
    public IReadOnlyList<SelectListItem> SymbolOptions { get; set; } = Array.Empty<SelectListItem>();
    public BacktestResult? Result { get; set; }
    public string? Error { get; set; }
}
