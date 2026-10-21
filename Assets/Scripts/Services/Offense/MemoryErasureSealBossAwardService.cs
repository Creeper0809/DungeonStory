using System;
using System.Collections.Generic;
using DungeonStory.Foundation;
using DungeonStory.Foundation.Facts;
using UnityEngine;
using VContainer;
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
        out string regionDisplayName,
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
/// physical stack. After its exact receipt returns, the saved domain journal
/// retries only observation delivery; an already-used seal is never minted again.
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
    private readonly MemoryErasureBossAwardPublicationRuntime publications;
    private readonly IGameCalendar calendar;
    private readonly IUiClock clock;
    private bool reconciliationPending;
    private float nextReconciliationAt;

#if UNITY_EDITOR
    public MemoryErasureSealBossAwardService(
        IOffenseRegionMemoryErasureSealAwardAuthority regions,
        IOffenseWorldSimulation world,
        IPhysicalItemSourcePublicationService physicalSources,
        IWorldDropZoneQuery dropZones) =>
        throw new InvalidOperationException(
            "Memory-erasure boss awards require their domain observation journal and game calendar.");
#endif

    [Inject]
    public MemoryErasureSealBossAwardService(
        IUiClock clock,
        IOffenseRegionMemoryErasureSealAwardAuthority regions,
        IOffenseWorldSimulation world,
        IPhysicalItemSourcePublicationService physicalSources,
        IWorldDropZoneQuery dropZones,
        MemoryErasureBossAwardPublicationRuntime publications,
        IGameCalendar calendar)
    {
        this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
        this.regions = regions
            ?? throw new ArgumentNullException(nameof(regions));
        this.world = world ?? throw new ArgumentNullException(nameof(world));
        this.physicalSources = physicalSources
            ?? throw new ArgumentNullException(nameof(physicalSources));
        this.dropZones = dropZones
            ?? throw new ArgumentNullException(nameof(dropZones));
        this.publications = publications
            ?? throw new ArgumentNullException(nameof(publications));
        this.calendar = calendar ?? throw new ArgumentNullException(nameof(calendar));
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
            || clock.Time < nextReconciliationAt)
        {
            return;
        }

        if (!TryReconcilePendingAwards(out _))
        {
            nextReconciliationAt = clock.Time
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

        if (!DomainFactValueSyntax.IsIdentity(regionId))
        {
            return Result(
                MemoryErasureSealBossAwardStatus.Failed,
                detail: "memory-erasure-seal-boss-region-missing");
        }
        if (publications.IsBusy || publications.IsRestoring)
        {
            reconciliationPending = true;
            return Result(MemoryErasureSealBossAwardStatus.Failed, regionId,
                detail: "memory-erasure-seal-publication-active");
        }
        if (!regions.TryBeginMemoryErasureSealAward(
                regionId,
                out string operationId,
                out string regionDisplayName,
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
            var observed = publications.CaptureForRegion(regionId);
            if (observed == null || !observed.delivered || observed.fact.operationId != operationId)
                return Result(MemoryErasureSealBossAwardStatus.Failed, regionId, operationId,
                    detail: "memory-erasure-seal-completed-observation-missing");
            return Result(
                MemoryErasureSealBossAwardStatus.AlreadyAwarded,
                regionId,
                operationId,
                detail: "this region's first-boss seal was already published");
        }
        return TryPublishClaim(regionId, regionDisplayName, operationId);
    }

    [GameplayInternalOnly(
        "Lifecycle and focused verification reconcile every claimed-but-unpublished region with its deterministic physical publication operation.",
        "VContainer lifecycle and V25 focused verifier")]
    public bool TryReconcilePendingAwards(out string failureReason)
    {
        failureReason = string.Empty;
        if (publications.IsBusy || publications.IsRestoring)
        {
            reconciliationPending = true;
            failureReason = "memory-erasure-seal-publication-active";
            return false;
        }
        IReadOnlyList<string> pending = regions
            .CapturePendingMemoryErasureSealAwardRegionIds();
        foreach (string regionId in pending)
        {
            if (!regions.TryBeginMemoryErasureSealAward(
                    regionId,
                    out string operationId,
                    out string regionDisplayName,
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
                regionDisplayName,
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
        string regionDisplayName,
        string operationId)
    {
        if (!publications.TryBegin(regionId, out string beginFailure))
        {
            reconciliationPending = true;
            return Result(MemoryErasureSealBossAwardStatus.Failed, regionId, operationId, detail: beginFailure);
        }
        try
        {
            MemoryErasureBossAwardPublicationData observation = publications.CaptureForRegion(regionId);
            if (observation == null)
            {
                if (!dropZones.TryGetExpeditionLootDropoff(out Vector2Int dropoff))
                {
                    reconciliationPending = true;
                    return Result(MemoryErasureSealBossAwardStatus.Failed, regionId, operationId,
                        detail: "memory-erasure-seal-loot-dropoff-missing");
                }
                observation = publications.Prepare(regionId, operationId, regionDisplayName,
                    calendar.Day, dropoff.x, dropoff.y);
            }
            if (observation.fact.operationId != operationId)
                throw new InvalidOperationException("memory-erasure-seal-observation-operation-conflict");

            // Once an exact physical receipt has returned, future retries never create or query the item again.
            // The player may already have hauled, equipped, or consumed it while observation delivery waits.
            if (observation.fact.physicalCommitId.Length == 0)
            {
                int appliedDay = calendar.Day;
                var physicalFact = observation.fact.Clone();
                physicalFact.regionDisplayName = regionDisplayName;
                physicalFact.absoluteDay = appliedDay;
                physicalFact.Validate(requirePhysical: false);
                IReadOnlyDictionary<string, int> exactOutput = new Dictionary<string, int>(StringComparer.Ordinal)
                { [MemoryErasureSealItemRules.ItemId] = MemoryErasureSealItemRules.UseQuantity };
                if (!physicalSources.TryEnsureLooseOutputs(exactOutput,
                        new Vector2Int(observation.dropoffX, observation.dropoffY), operationId,
                        MemoryErasureSealBossAwardRules.PublicationReason,
                        out PhysicalItemSourcePublicationReceipt receipt, out string physicalFailure))
                {
                    reconciliationPending = true;
                    return Result(MemoryErasureSealBossAwardStatus.Failed, regionId, operationId,
                        detail: "memory-erasure-seal-physical-publication-pending:" + physicalFailure);
                }
                publications.RecordPhysical(regionId, receipt, regionDisplayName, appliedDay);
                observation = publications.CaptureForRegion(regionId);
            }

            if (!publications.TryDeliver(regionId, out string deliveryFailure))
            {
                reconciliationPending = true;
                return Result(MemoryErasureSealBossAwardStatus.Failed, regionId, operationId,
                    observation.fact.physicalCommitId, "physical award applied; observation pending: " + deliveryFailure);
            }
            if (!regions.TryCompleteMemoryErasureSealAward(regionId, operationId, out string completionFailure))
            {
                reconciliationPending = true;
                return Result(MemoryErasureSealBossAwardStatus.Failed, regionId, operationId,
                    observation.fact.physicalCommitId, "physical award and observation applied; claim completion pending: " + completionFailure);
            }
            reconciliationPending = regions.CapturePendingMemoryErasureSealAwardRegionIds().Count > 0;
            return Result(MemoryErasureSealBossAwardStatus.Awarded, regionId, operationId,
                observation.fact.physicalCommitId, "exactly one physical memory-erasure seal was published");
        }
        catch (Exception exception) when (exception is not OutOfMemoryException
            and not StackOverflowException and not AccessViolationException)
        {
            reconciliationPending = true;
            string commit = publications.CaptureForRegion(regionId)?.fact.physicalCommitId ?? string.Empty;
            return Result(MemoryErasureSealBossAwardStatus.Failed, regionId, operationId, commit,
                "memory-erasure-seal-publication-pending:" + exception.Message);
        }
        finally { publications.End(regionId); }
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
