using CycliqueShareTracker.Application.Models;

namespace CycliqueShareTracker.Application.Interfaces;

public interface IBacktestEngine
{
    BacktestAssetResult RunForAsset(
        string symbol,
        string assetName,
        IReadOnlyList<PriceBar> priceBars,
        DateOnly simulationStartDate,
        DateOnly simulationEndDate,
        AlgorithmType algorithmType,
        StrategyConfig config);
}
