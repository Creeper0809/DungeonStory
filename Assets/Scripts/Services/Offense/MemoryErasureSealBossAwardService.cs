using System;
using System.Collections.Generic;
using UnityEngine;
using VContainer.Unity;

public static class MemoryErasureSealBossAwardRules
{
    public const string OperationPrefix =
        "region-boss-memory-erasure-seal:";
    public const string PublicationReason =
        "region-first-boss-memory-erasure-seal";

    public static string BuildOperationId(string regionId)
    {
        string required = regionId?.Trim() ?? string.Empty;
        if (required.Length == 0
            || !string.Equals(required, regionId, StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "A canonical offense region ID is required.",
                nameof(regionId));
        }
        return OperationPrefix + required;
    }
}

public interface IOffenseRegionMemoryErasureSealAwardAuthority
{
    IReadOnlyList<string> CapturePendingMemoryErasureSealAwardRegionIds();

    bool TryBeginMemoryErasureSealAward(
        string regionId,
        out string operationId,
        out bool alreadyPublished,
        out string failureReason);

    bool TryCompleteMemoryErasureSealAward(
        string regionId,
        string operationId,
        out string failureReason);
}

public enum MemoryErasureSealBossAwardStatus
{
    NotRegionBoss = 1,
    Awarded = 2,
    AlreadyAwarded = 3,
    Failed = 4
}

public readonly struct MemoryErasureSealBossAwardResult
{
    public MemoryErasureSealBossAwardResult(
        MemoryErasureSealBossAwardStatus status,
        string regionId,
        string operationId,
        string physicalCommitId,
        string detail)
    {
        Status = status;
        RegionId = regionId ?? string.Empty;
        OperationId = operationId ?? string.Empty;
        PhysicalCommitId = physicalCommitId ?? string.Empty;
        Detail = detail ?? string.Empty;
    }

    public MemoryErasureSealBossAwardStatus Status { get; }
    public string RegionId { get; }
    public string OperationId { get; }
    public string PhysicalCommitId { get; }
    public string Detail { get; }
}

public interface IMemoryErasureSealBossAwardService
{
    MemoryErasureSealBossAwardResult TryAwardForVictory(
        OffenseExpeditionRun expedition,
        OffenseRouteNode completedNode,
        bool worldObjectiveBattle);

    bool TryReconcilePendingAwards(out string failureReason);
}

/// <summary>
/// Converts a persisted per-region first-boss claim into one exact loose
/// physical stack. Publication is deterministic by region operation ID, so a
/// retry validates the same stack and cannot mint another seal.
/// </summary>
public sealed class MemoryErasureSealBossAwardService :
    IMemoryErasureSealBossAwardService,
    IStartable,
    ITickable,
    IDungeonSaveRestoreCompletedHook
{
    private const float RetryIntervalSeconds = 1f;
    private readonly IOffenseRegionMemoryErasureSealAwardAuthority regions;
    private readonly IOffenseWorldSimulation world;
    private readonly IPhysicalItemSourcePublicationService physicalSources;
    private readonly IWorldDropZoneQuery dropZones;
    private bool reconciliationPending;
    private float nextReconciliationAt;

    public MemoryErasureSealBossAwardService(
        IOffenseRegionMemoryErasureSealAwardAuthority regions,
        IOffenseWorldSimulation world,
        IPhysicalItemSourcePublicationService physicalSources,
        IWorldDropZoneQuery dropZones)
    {
        this.regions = regions
            ?? throw new ArgumentNullException(nameof(regions));
        this.world = world ?? throw new ArgumentNullException(nameof(world));
        this.physicalSources = physicalSources
            ?? throw new ArgumentNullException(nameof(physicalSources));
        this.dropZones = dropZones
            ?? throw new ArgumentNullException(nameof(dropZones));
    }

    [GameplayInternalOnly(
        "Composition requests reconciliation for any restored claimed-but-unpublished region award.",
        "VContainer entry point")]
    public void Start()
    {
        reconciliationPending = true;
        nextReconciliationAt = 0f;
    }

    [GameplayInternalOnly(
        "A bounded lifecycle retry republishes pending deterministic boss-award operations without another battle.",
        "VContainer entry point")]
    public void Tick()
    {
        if (!reconciliationPending
            || Time.unscaledTime < nextReconciliationAt)
        {
            return;
        }

        if (!TryReconcilePendingAwards(out _))
        {
            nextReconciliationAt = Time.unscaledTime
                + RetryIntervalSeconds;
        }
    }

    [GameplayInternalOnly(
        "The save coordinator requests pending award reconciliation after all restored authorities publish together.",
        "DungeonSaveService")]
    public void OnRestoreCompleted()
    {
        reconciliationPending = true;
        nextReconciliationAt = 0f;
    }

    [GameplayInternalOnly(
        "The expedition battle completion handler calls this only after an authoritative victory.",
        "OffenseExpeditionBattleCompletionHandler")]
    public MemoryErasureSealBossAwardResult TryAwardForVictory(
        OffenseExpeditionRun expedition,
        OffenseRouteNode completedNode,
        bool worldObjectiveBattle)
    {
        if (expedition == null)
        {
            return Result(
                MemoryErasureSealBossAwardStatus.Failed,
                detail: "memory-erasure-seal-expedition-missing");
        }

        string regionId;
        if (expedition.UsesWorldTravel)
        {
            if (!worldObjectiveBattle
                || !world.TryGetSite(
                    expedition.WorldSiteId,
                    out OffenseWorldSiteStateData site)
                || site == null
                || !site.fixedBoss)
            {
                return Result(MemoryErasureSealBossAwardStatus.NotRegionBoss);
            }
            regionId = site.regionId?.Trim() ?? string.Empty;
        }
        else
        {
            if (completedNode?.IsBoss != true)
            {
                return Result(MemoryErasureSealBossAwardStatus.NotRegionBoss);
            }
            regionId = expedition.Target?.regionId?.Trim() ?? string.Empty;
        }

        if (regionId.Length == 0)
        {
            return Result(
                MemoryErasureSealBossAwardStatus.Failed,
                detail: "memory-erasure-seal-boss-region-missing");
        }
        if (!regions.TryBeginMemoryErasureSealAward(
                regionId,
                out string operationId,
                out bool alreadyPublished,
                out string claimFailure))
        {
            return Result(
                MemoryErasureSealBossAwardStatus.Failed,
                regionId,
                detail: claimFailure);
        }
        if (alreadyPublished)
        {
            return Result(
                MemoryErasureSealBossAwardStatus.AlreadyAwarded,
                regionId,
                operationId,
                detail: "this region's first-boss seal was already published");
        }
        return TryPublishClaim(regionId, operationId);
    }

    [GameplayInternalOnly(
        "Lifecycle and focused verification reconcile every claimed-but-unpublished region with its deterministic physical publication operation.",
        "VContainer lifecycle and V25 focused verifier")]
    public bool TryReconcilePendingAwards(out string failureReason)
    {
        failureReason = string.Empty;
        IReadOnlyList<string> pending = regions
            .CapturePendingMemoryErasureSealAwardRegionIds();
        foreach (string regionId in pending)
        {
            if (!regions.TryBeginMemoryErasureSealAward(
                    regionId,
                    out string operationId,
                    out bool alreadyPublished,
                    out failureReason))
            {
                reconciliationPending = true;
                return false;
            }
            if (alreadyPublished)
            {
                continue;
            }

            MemoryErasureSealBossAwardResult result = TryPublishClaim(
                regionId,
                operationId);
            if (result.Status == MemoryErasureSealBossAwardStatus.Failed)
            {
                failureReason = result.Detail;
                reconciliationPending = true;
                return false;
            }
        }

        reconciliationPending = false;
        return true;
    }

    [GameplayInternalOnly(
        "One claimed region operation idempotently ensures its exact loose output before completing the persisted authority bit.",
        "TryAwardForVictory and TryReconcilePendingAwards")]
    private MemoryErasureSealBossAwardResult TryPublishClaim(
        string regionId,
        string operationId)
    {
        if (!dropZones.TryGetExpeditionLootDropoff(out Vector2Int dropoff))
        {
            reconciliationPending = true;
            return Result(
                MemoryErasureSealBossAwardStatus.Failed,
                regionId,
                operationId,
                detail: "memory-erasure-seal-loot-dropoff-missing");
        }

        IReadOnlyDictionary<string, int> exactOutput =
            new Dictionary<string, int>(StringComparer.Ordinal)
            {
                [MemoryErasureSealItemRules.ItemId] =
                    MemoryErasureSealItemRules.UseQuantity
            };
        if (!physicalSources.TryEnsureLooseOutputs(
                exactOutput,
                dropoff,
                operationId,
                MemoryErasureSealBossAwardRules.PublicationReason,
                out PhysicalItemSourcePublicationReceipt receipt,
                out string publicationFailure)
            || !receipt.IsCommitted
            || receipt.OutputQuantity != MemoryErasureSealItemRules.UseQuantity)
        {
            reconciliationPending = true;
            return Result(
                MemoryErasureSealBossAwardStatus.Failed,
                regionId,
                operationId,
                detail: publicationFailure.Length > 0
                    ? publicationFailure
                    : "memory-erasure-seal-physical-receipt-invalid");
        }
        if (!regions.TryCompleteMemoryErasureSealAward(
                regionId,
                operationId,
                out string completionFailure))
        {
            reconciliationPending = true;
            return Result(
                MemoryErasureSealBossAwardStatus.Failed,
                regionId,
                operationId,
                receipt.OutputCommitIds.Count == 1
                    ? receipt.OutputCommitIds[0]
                    : string.Empty,
                completionFailure);
        }

        reconciliationPending = regions
            .CapturePendingMemoryErasureSealAwardRegionIds().Count > 0;
        return Result(
            MemoryErasureSealBossAwardStatus.Awarded,
            regionId,
            operationId,
            receipt.OutputCommitIds.Count == 1
                ? receipt.OutputCommitIds[0]
                : string.Empty,
            "exactly one physical memory-erasure seal was published");
    }

    private static MemoryErasureSealBossAwardResult Result(
        MemoryErasureSealBossAwardStatus status,
        string regionId = "",
        string operationId = "",
        string physicalCommitId = "",
        string detail = "") => new(
            status,
            regionId,
            operationId,
            physicalCommitId,
            detail);
}
