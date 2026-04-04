namespace CycliqueShareTracker.Application.Models;

public sealed record AssetSnapshotResult(
    TrackedAssetOptions Asset,
    DashboardSnapshot? Snapshot,
    string? Error);
