using CycliqueShareTracker.Application.Models;

namespace CycliqueShareTracker.Infrastructure.Providers;

public static class PriceBarValidator
{
    public static IReadOnlyList<PriceBar> ValidateAndNormalize(IReadOnlyList<PriceBar> source)
    {
        if (source.Count == 0)
        {
            return source;
        }

        var normalized = source.OrderBy(b => b.Date).ToList();

        for (var i = 0; i < normalized.Count; i++)
        {
            var current = normalized[i];

            if (current.Open <= 0 || current.High <= 0 || current.Low <= 0 || current.Close <= 0)
            {
                return Array.Empty<PriceBar>();
            }

            if (current.High < current.Low)
            {
                return Array.Empty<PriceBar>();
            }

            if (i > 0 && normalized[i - 1].Date == current.Date)
            {
                return Array.Empty<PriceBar>();
            }
        }

        return normalized;
    }
}
