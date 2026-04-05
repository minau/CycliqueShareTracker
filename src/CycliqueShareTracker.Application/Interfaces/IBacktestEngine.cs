using CycliqueShareTracker.Application.Models;

namespace CycliqueShareTracker.Application.Interfaces;

public interface IBacktestEngine
{
    BacktestAssetResult RunForAsset(string symbol, string assetName, IReadOnlyList<PriceBar> priceBars, bool includeMacdInScoring, StrategyConfig config);
}
