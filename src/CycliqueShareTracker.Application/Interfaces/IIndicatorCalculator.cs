using CycliqueShareTracker.Application.Models;

namespace CycliqueShareTracker.Application.Interfaces;

public interface IIndicatorCalculator
{
    IReadOnlyList<ComputedIndicator> Compute(IReadOnlyList<PriceBar> prices);
}
