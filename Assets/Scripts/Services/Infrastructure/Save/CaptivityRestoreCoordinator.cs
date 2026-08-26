using System;
using System.Collections.Generic;
using System.Linq;

internal sealed class CaptivityRestoreCoordinator
{
    private const string RestoreParticipantId = "450.world.captivity";

    private readonly ICharacterAiWorldRegistry worldRegistry;
    private readonly IWorldItemStackRuntime itemRuntime;
    private readonly CaptivityInteractionRegistry interactions;
    private readonly DungeonRuntimeAggregateRootStore aggregateRootStore;
    private readonly CaptivityActorAccess actors;
    private readonly IDoorAccessSubjectRegistry doorSubjects;
    private readonly ICaptivityEscortRestoreLifecycle escortRestore;
    private readonly IPhysicalItemBatchDispositionService batchDispositions;
    private bool restoreTransactionActive;
    private bool restoreCandidatePrepared;
    private bool restorePublicationPending;
    private CaptivityRestoreCandidate latestDetachedCandidate;

    internal CaptivityRestoreCoordinator(
        ICharacterAiWorldRegistry worldRegistry,
        IWorldItemStackRuntime itemRuntime,
        CaptivityInteractionRegistry interactions,
        DungeonRuntimeAggregateRootStore aggregateRootStore,
        CaptivityActorAccess actors,
        IDoorAccessSubjectRegistry doorSubjects,
        ICaptivityEscortRestoreLifecycle escortRestore,
        IPhysicalItemBatchDispositionService batchDispositions)
    {
        this.worldRegistry = worldRegistry
            ?? throw new ArgumentNullException(nameof(worldRegistry));
        this.itemRuntime = itemRuntime
            ?? throw new ArgumentNullException(nameof(itemRuntime));
        this.interactions = interactions
            ?? throw new ArgumentNullException(nameof(interactions));
        this.aggregateRootStore = aggregateRootStore
            ?? throw new ArgumentNullException(nameof(aggregateRootStore));
        this.actors = actors ?? throw new ArgumentNullException(nameof(actors));
        this.doorSubjects = doorSubjects
            ?? throw new ArgumentNullException(nameof(doorSubjects));
        this.escortRestore = escortRestore
            ?? throw new ArgumentNullException(nameof(escortRestore));
        this.batchDispositions = batchDispositions
            ?? throw new ArgumentNullException(nameof(batchDispositions));
    }

    internal string ParticipantId => RestoreParticipantId;

    internal void ValidateRestore(
        CaptivitySaveData saveData,
        DungeonGameRestoreReport report)
    {
        TryBuildRestore(saveData, report, out _);
    }

    internal CaptivityRestoreCandidate BuildRestore(CaptivitySaveData saveData)
    {
        latestDetachedCandidate = null;
        DungeonGameRestoreReport report = new();
        if (!TryBuildRestore(saveData, report, out CaptivityRestoreCandidate candidate))
        {
            throw new InvalidOperationException(
                "Captivity restore candidate is invalid: "
                + string.Join(" | ", report.Errors));
        }
        latestDetachedCandidate = candidate;
        return candidate;
    }

    internal bool TryTakePreparedRestoreCandidate(
        out CaptivityRestoreCandidate candidate)
    {
        candidate = latestDetachedCandidate;
        latestDetachedCandidate = null;
        return candidate != null;
    }

    internal void Restore(
        CaptivitySaveData saveData,
        DungeonGameRestoreReport report)
    {
        if (!TryBuildRestore(
                saveData,
                report,
                out CaptivityRestoreCandidate candidate))
        {
            return;
        }
        try
        {
            StageRestore(candidate);
        }
        catch (Exception exception)
        {
            report.AddError(exception.Message);
        }
    }

    internal void StageRestore(CaptivityRestoreCandidate candidate)
    {
        if (candidate == null)
        {
            throw new ArgumentNullException(nameof(candidate));
        }
        CaptivityRestoreTransactionPolicy.RequireStageBoundary(
            restoreTransactionActive,
            restoreCandidatePrepared,
            aggregateRootStore);

        candidate.ReconcileCaptives(captive =>
        {
            if (!CaptivityLaborToolAssignmentOutbox.RequiresFinalization(captive))
            {
                return;
            }
            if (!CaptivityLaborToolAssignmentOutbox.TryFinalizePending(
                    captive,
                    batchDispositions,
                    out string failureReason))
            {
                throw new InvalidOperationException(
                    $"Captive '{captive.captiveId}' labor-tool assignment could not be reconciled: {failureReason}");
            }

            CaptiveLaborPermission permissions = captive.pendingLaborPermissions;
            if (permissions == CaptiveLaborPermission.None)
            {
                throw new InvalidOperationException(
                    $"Captive '{captive.captiveId}' pending labor-tool assignment has no permissions.");
            }
            captive.pendingLaborPermissions = CaptiveLaborPermission.None;
            captive.laborToolDestinationId = string.Empty;
            captive.laborPermissions = permissions;
            captive.status = CaptivityStatus.Labor;
        });

        actors.Replace(candidate);
        doorSubjects.ReplaceCaptiveSubjects(
            candidate.Captives
                .Where(captive => CaptivitySaveValidation.IsDoorCaptive(
                    captive.status))
                .Select(captive => captive.captiveId));
        restoreCandidatePrepared = true;
    }

    private bool TryBuildRestore(
        CaptivitySaveData saveData,
        DungeonGameRestoreReport report,
        out CaptivityRestoreCandidate candidate)
    {
        if (report == null)
        {
            throw new ArgumentNullException(nameof(report));
        }
        candidate = null;
        CaptivitySaveValidation.Validate(saveData, report);
        if (!report.Success)
        {
            return false;
        }

        CaptivityRestoreCandidate restored =
            CaptivityRestoreCandidate.Create(saveData);
        ValidateWorldReferences(restored.Captives, report);
        if (!report.Success)
        {
            return false;
        }

        candidate = restored;
        return true;
    }

    internal void BeginRestoreCandidate()
    {
        if (restorePublicationPending)
        {
            throw new InvalidOperationException(
                "A published captivity restore candidate is awaiting completion.");
        }
        CaptivityRestoreTransactionPolicy.RequireBeginBoundary(
            restoreTransactionActive);
        restoreTransactionActive = true;
        restoreCandidatePrepared = false;
        latestDetachedCandidate = null;
    }

    internal void PublishRestoreCandidate()
    {
        CaptivityRestoreTransactionPolicy.RequirePublishBoundary(
            restoreTransactionActive,
            restoreCandidatePrepared);
        restorePublicationPending = true;
        restoreCandidatePrepared = false;
        restoreTransactionActive = false;
        latestDetachedCandidate = null;
    }

    internal void RollbackPublishedRestoreCandidate()
    {
        restorePublicationPending = false;
        restoreCandidatePrepared = false;
        restoreTransactionActive = false;
        latestDetachedCandidate = null;
    }

    internal void CompleteRestoreCandidate()
    {
        if (!restorePublicationPending)
        {
            return;
        }

        escortRestore.ClearTransientState();
        restorePublicationPending = false;
    }

    internal void DiscardRestoreCandidate()
    {
        restoreCandidatePrepared = false;
        restoreTransactionActive = false;
        latestDetachedCandidate = null;
    }

    private void ValidateWorldReferences(
        IReadOnlyList<CaptiveState> restored,
        DungeonGameRestoreReport report)
    {
        Dictionary<string, CharacterActor> characters = worldRegistry
            .AllCharacters
            .Where(actor => actor != null)
            .GroupBy(
                actor => CharacterPersistentIdentity.Require(actor).Value,
                StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.First(),
                StringComparer.Ordinal);
        Dictionary<string, BuildableObject> buildings = worldRegistry.Buildings
            .Where(building => building != null
                && !building.isDestroy
                && building.PersistentInstanceId.IsValid)
            .ToDictionary(
                building => building.PersistentInstanceId.Value,
                StringComparer.Ordinal);
        Dictionary<string, WorldItemStackSnapshot> stacks = itemRuntime
            .GetAllStacks()
            .Where(stack => stack != null
                && !string.IsNullOrWhiteSpace(stack.StackId))
            .GroupBy(stack => stack.StackId, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.First(),
                StringComparer.Ordinal);
        Dictionary<string, int> housingOccupancy =
            new(StringComparer.Ordinal);

        foreach (CaptiveState captive in restored)
        {
            characters.TryGetValue(captive.captiveId, out CharacterActor actor);
            if (captive.IsActive && (actor == null || actor.IsDead))
            {
                report.AddError(
                    $"Active captive '{captive.captiveId}' references a missing or dead character.");
            }

            ValidateCharacterReference(
                captive.captiveId,
                "carrier",
                captive.reservedCarrierId,
                characters,
                report);
            ValidateCharacterReference(
                captive.captiveId,
                "warden",
                captive.reservedWardenId,
                characters,
                report);

            if (captive.housingBuildingId.Length > 0)
            {
                if (!buildings.TryGetValue(
                        captive.housingBuildingId,
                        out BuildableObject housing)
                    || housing.BuildingData
                        .GetCaptiveHousingAbility()?.IsValid != true)
                {
                    report.AddError(
                        $"Captive '{captive.captiveId}' references invalid housing '{captive.housingBuildingId}'.");
                }
                else if (CaptivitySaveValidation.RequiresHousing(
                             captive.status))
                {
                    housingOccupancy.TryGetValue(
                        captive.housingBuildingId,
                        out int count);
                    housingOccupancy[captive.housingBuildingId] = count + 1;
                }
            }

            if (captive.currentInteractionId.Length > 0
                && !interactions.TryGet(
                    captive.currentInteractionId,
                    out ICaptivityInteractionHandler _))
            {
                report.AddError(
                    $"Captive '{captive.captiveId}' references unknown interaction '{captive.currentInteractionId}'.");
            }

            if (captive.restraintStackId.Length > 0
                && stacks.TryGetValue(
                    captive.restraintStackId,
                    out WorldItemStackSnapshot stack)
                && (!string.Equals(
                        stack.ItemId,
                        captive.restraintItemId,
                        StringComparison.Ordinal)
                    || stack.Quantity < captive.restraintQuantity))
            {
                report.AddError(
                    $"Captive '{captive.captiveId}' has a mismatched restraint stack '{captive.restraintStackId}'.");
            }
        }

        foreach (KeyValuePair<string, int> occupancy in housingOccupancy)
        {
            BuildingCaptiveHousingAbility ability = buildings[occupancy.Key]
                .BuildingData.GetCaptiveHousingAbility();
            if (occupancy.Value > ability.capacity)
            {
                report.AddError(
                    $"Captivity housing '{occupancy.Key}' exceeds capacity {ability.capacity} with {occupancy.Value} occupants.");
            }
        }
    }

    private static void ValidateCharacterReference(
        string captiveId,
        string role,
        string characterId,
        IReadOnlyDictionary<string, CharacterActor> characters,
        DungeonGameRestoreReport report)
    {
        if (characterId.Length == 0)
        {
            return;
        }
        if (!characters.TryGetValue(characterId, out CharacterActor actor)
            || actor == null
            || actor.IsDead)
        {
            report.AddError(
                $"Captive '{captiveId}' references missing {role} '{characterId}'.");
        }
    }
}
