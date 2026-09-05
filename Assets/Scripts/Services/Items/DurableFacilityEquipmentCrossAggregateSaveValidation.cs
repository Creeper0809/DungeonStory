using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public sealed class DurableFacilityEquipmentCrossAggregateSaveValidation :
    IDungeonSavePreflightValidator,
    IDungeonSaveRegistryPreflightValidator,
    IDungeonCapturedSavePreflightValidator
{
    public void Validate(
        DungeonGameSaveData saveData,
        DungeonGameRestoreReport report)
    {
        if (saveData == null)
            throw new ArgumentNullException(nameof(saveData));
        if (report == null)
            throw new ArgumentNullException(nameof(report));
        try
        {
            ValidatePayloads(
                ReadRequired<DungeonDurableFacilityEquipmentSaveData>(
                    saveData,
                    DurableFacilityEquipmentSaveSection.Id),
                ReadRequired<DungeonPhysicalItemSaveData>(
                    saveData,
                    PhysicalItemsSaveSection.Id),
                ReadRequired<ModularFacilityWorldSaveData>(
                    saveData,
                    ModularFacilityWorldSaveSection.Id));
        }
        catch (Exception exception)
        {
            report.AddError(
                "Durable facility-equipment cross-aggregate preflight failed: "
                + exception.Message);
        }
    }

    public void Validate(
        IReadOnlyDictionary<string, DungeonSaveSectionEnvelope> envelopes,
        DungeonGameRestoreReport report)
    {
        if (envelopes == null)
            throw new ArgumentNullException(nameof(envelopes));
        if (report == null)
            throw new ArgumentNullException(nameof(report));
        try
        {
            ValidatePayloads(
                ParseRequired<DungeonDurableFacilityEquipmentSaveData>(
                    envelopes,
                    DurableFacilityEquipmentSaveSection.Id,
                    DungeonDurableFacilityEquipmentSaveData.CurrentVersion),
                ParseRequired<DungeonPhysicalItemSaveData>(
                    envelopes,
                    PhysicalItemsSaveSection.Id,
                    DungeonPhysicalItemSaveData.CurrentVersion),
                ParseRequired<ModularFacilityWorldSaveData>(
                    envelopes,
                    ModularFacilityWorldSaveSection.Id,
                    ModularFacilityWorldSaveSection.CurrentSectionVersion));
        }
        catch (Exception exception)
        {
            report.AddError(
                "Durable facility-equipment registry preflight failed: "
                + exception.Message);
        }
    }

    internal static void ValidatePayloads(
        DungeonDurableFacilityEquipmentSaveData upper,
        DungeonPhysicalItemSaveData physical,
        ModularFacilityWorldSaveData facilities)
    {
        if (upper?.slots == null
            || physical?.pendingProductionInputDestinationDrains == null
            || facilities?.buildings == null)
        {
            throw new InvalidOperationException(
                "Durable facility-equipment join collections are missing.");
        }
        FacilityBufferDestinationCustodyDrainSnapshot[] children = physical
            .pendingProductionInputDestinationDrains
            .Where(value => value != null)
            .Select(FacilityBufferDestinationCustodyDrainProjection
                .ProjectValidated)
            .ToArray();
        DurableFacilityEquipmentCrossAggregateJoin.Validate(
            upper.slots,
            children,
            facilities.buildings);
    }

    private static TPayload ReadRequired<TPayload>(
        DungeonGameSaveData saveData,
        string sectionId)
        where TPayload : class, new()
    {
        if (!DungeonSaveSectionPayload.TryRead(
                saveData,
                sectionId,
                out TPayload payload))
        {
            throw new InvalidOperationException(
                "Required save section is missing: " + sectionId);
        }
        return payload;
    }

    private static TPayload ParseRequired<TPayload>(
        IReadOnlyDictionary<string, DungeonSaveSectionEnvelope> envelopes,
        string sectionId,
        int currentVersion)
        where TPayload : class
    {
        if (!envelopes.TryGetValue(
                sectionId,
                out DungeonSaveSectionEnvelope envelope)
            || envelope == null
            || !string.Equals(
                envelope.sectionId,
                sectionId,
                StringComparison.Ordinal)
            || envelope.sectionVersion != currentVersion
            || string.IsNullOrWhiteSpace(envelope.payloadJson))
        {
            throw new InvalidOperationException(
                "Save section envelope is not exact current format: "
                + sectionId);
        }
        return JsonUtility.FromJson<TPayload>(envelope.payloadJson)
            ?? throw new InvalidOperationException(
                "Save section payload deserialized to null: " + sectionId);
    }
}

internal static class DurableFacilityEquipmentCrossAggregateJoin
{
    internal static void Validate(
        IEnumerable<DurableFacilityEquipmentSlotSaveData> sourceUppers,
        IEnumerable<FacilityBufferDestinationCustodyDrainSnapshot>
            sourceChildren,
        IEnumerable<ModularFacilityBuildingSaveData> sourceFacilities = null)
    {
        DurableFacilityEquipmentSlotSaveData[] uppers =
            (sourceUppers
                    ?? throw new ArgumentNullException(nameof(sourceUppers)))
                .ToArray();
        if (uppers.Any(value => value == null))
        {
            throw new InvalidOperationException(
                "Durable facility-equipment upper row is null.");
        }
        FacilityBufferDestinationCustodyDrainSnapshot[] children =
            (sourceChildren
                    ?? throw new ArgumentNullException(nameof(sourceChildren)))
                .Where(value => value != null)
                .OrderBy(value => value.StepOperationId, StringComparer.Ordinal)
                .ToArray();
        Dictionary<string, FacilityBufferDestinationCustodyDrainSnapshot>
            childByStep = new(StringComparer.Ordinal);
        foreach (FacilityBufferDestinationCustodyDrainSnapshot child in children)
        {
            if (!childByStep.TryAdd(child.StepOperationId, child))
            {
                throw new InvalidOperationException(
                    "Duplicate FacilityBuffer custody drain step: "
                    + child.StepOperationId);
            }
        }

        HashSet<string> facilityIds = sourceFacilities == null
            ? null
            : sourceFacilities
                .Where(value => value != null)
                .Select(value => value.persistentInstanceId)
                .ToHashSet(StringComparer.Ordinal);
        HashSet<string> ownedSteps = new(StringComparer.Ordinal);
        HashSet<long> sequences = new();
        HashSet<DurableFacilityEquipmentSlotKey> activeKeys = new();
        foreach (DurableFacilityEquipmentSlotSaveData upper in uppers
                     .OrderBy(value => value.assignmentSequence))
        {
            DurableFacilityEquipmentSlotKey key = new(
                upper.logicalOwnerDomain,
                upper.ownerSubjectId);
            if (upper.assignmentSequence <= 0L
                || !sequences.Add(upper.assignmentSequence))
            {
                throw new InvalidOperationException(
                    "Durable facility-equipment assignment sequence is invalid or duplicated.");
            }
            if (upper.lifecyclePhase !=
                    DurableFacilityEquipmentSlotLifecyclePhase
                        .ClosedAwaitingCheckpointGc
                && !activeKeys.Add(key))
            {
                throw new InvalidOperationException(
                    "Durable facility-equipment key has more than one non-closed row: "
                    + key);
            }
            if (upper.lifecyclePhase ==
                    DurableFacilityEquipmentSlotLifecyclePhase.Active
                && facilityIds != null
                && !facilityIds.Contains(upper.ownerFacilityId))
            {
                throw new InvalidOperationException(
                    "Active durable facility-equipment owner facility is missing: "
                    + upper.ownerFacilityId);
            }

            string expectedStep =
                DurableFacilityEquipmentSlotIdentity.BuildDrainStepOperationId(
                    key,
                    upper.assignmentSequence);
            bool requiresChild = upper.lifecyclePhase is
                DurableFacilityEquipmentSlotLifecyclePhase.Draining
                or DurableFacilityEquipmentSlotLifecyclePhase
                    .ClosedAwaitingCheckpointGc;
            if (!requiresChild)
            {
                if (childByStep.ContainsKey(expectedStep))
                {
                    throw new InvalidOperationException(
                        "Non-draining durable facility-equipment row has a child: "
                        + expectedStep);
                }
                continue;
            }
            if (!ownedSteps.Add(expectedStep)
                || !childByStep.TryGetValue(expectedStep, out
                    FacilityBufferDestinationCustodyDrainSnapshot child))
            {
                throw new InvalidOperationException(
                    "Durable facility-equipment row has no unique child: "
                    + expectedStep);
            }
            RequireExactJoin(upper, child);
        }

        foreach (FacilityBufferDestinationCustodyDrainSnapshot child in children)
        {
            if (ClaimsDurableIdentity(child)
                && !ownedSteps.Contains(child.StepOperationId))
            {
                throw new InvalidOperationException(
                    "Durable facility-equipment child has no upper owner: "
                    + child.StepOperationId);
            }
        }
    }

    internal static void RequireExactJoin(
        DurableFacilityEquipmentSlotSaveData upper,
        FacilityBufferDestinationCustodyDrainSnapshot child)
    {
        if (upper == null || child == null)
            throw new ArgumentNullException(upper == null ? nameof(upper) : nameof(child));
        DurableFacilityEquipmentSlotKey key = new(
            upper.logicalOwnerDomain,
            upper.ownerSubjectId);
        string parent = DurableFacilityEquipmentSlotIdentity
            .BuildDrainParentOperationId(key, upper.assignmentSequence);
        string step = DurableFacilityEquipmentSlotIdentity
            .BuildDrainStepOperationId(key, upper.assignmentSequence);
        string owner = DurableFacilityEquipmentSlotIdentity
            .BuildOwnerStableId(key, upper.assignmentSequence);
        string destination = DurableFacilityEquipmentSlotIdentity
            .BuildDestinationId(key, upper.assignmentSequence);
        bool exact =
            string.Equals(upper.drainParentOperationId, parent,
                StringComparison.Ordinal)
            && string.Equals(upper.drainStepOperationId, step,
                StringComparison.Ordinal)
            && string.Equals(upper.drainOwnerStableId, owner,
                StringComparison.Ordinal)
            && string.Equals(upper.drainOwnerSubjectId, key.OwnerSubjectId,
                StringComparison.Ordinal)
            && string.Equals(upper.drainOwnerFacilityId, upper.ownerFacilityId,
                StringComparison.Ordinal)
            && string.Equals(upper.drainSourceDestinationId, destination,
                StringComparison.Ordinal)
            && string.Equals(upper.drainSourceAuthorityFingerprint,
                upper.sourceAuthorityFingerprint, StringComparison.Ordinal)
            && string.Equals(child.ParentOperationId,
                upper.drainParentOperationId, StringComparison.Ordinal)
            && string.Equals(child.StepOperationId,
                upper.drainStepOperationId, StringComparison.Ordinal)
            && string.Equals(child.OwnerStableId,
                upper.drainOwnerStableId, StringComparison.Ordinal)
            && string.Equals(child.OwnerSubjectId,
                upper.drainOwnerSubjectId, StringComparison.Ordinal)
            && string.Equals(child.OwnerFacilityId,
                upper.drainOwnerFacilityId, StringComparison.Ordinal)
            && string.Equals(child.SourceDestinationId,
                upper.drainSourceDestinationId, StringComparison.Ordinal)
            && string.Equals(child.SourceAuthorityFingerprint,
                upper.drainSourceAuthorityFingerprint, StringComparison.Ordinal)
            && string.Equals(child.RequestFingerprint,
                upper.drainRequestFingerprint, StringComparison.Ordinal)
            && child.OwnerGridX == upper.drainOwnerGridX
            && child.OwnerGridY == upper.drainOwnerGridY
            && child.Phase == upper.drainPhase
            && child.SourceActorCount == upper.drainSourceActorCount
            && child.CompletedActorCount == upper.drainCompletedActorCount
            && child.SourceOperationCount == upper.drainSourceOperationCount
            && child.ReleasedOperationCount == upper.drainReleasedOperationCount
            && child.InputQuantity == upper.drainInputQuantity
            && child.InputMassGrams == upper.drainInputMassGrams
            && child.ReleasedQuantity == upper.drainReleasedQuantity
            && child.ReleasedMassGrams == upper.drainReleasedMassGrams
            && string.Equals(child.CommitId, upper.drainCommitId,
                StringComparison.Ordinal)
            && string.Equals(child.ReceiptFingerprint,
                upper.drainReceiptFingerprint, StringComparison.Ordinal);
        if (!exact)
        {
            throw new InvalidOperationException(
                "Durable facility-equipment upper/child join is not exact: "
                + step);
        }
        if (upper.lifecyclePhase ==
                DurableFacilityEquipmentSlotLifecyclePhase.Draining
            && child.OwnerAcknowledged)
        {
            throw new InvalidOperationException(
                "Draining durable facility-equipment child is already owner-acknowledged: "
                + step);
        }
        if (upper.lifecyclePhase ==
                DurableFacilityEquipmentSlotLifecyclePhase
                    .ClosedAwaitingCheckpointGc
            && (!upper.authoritiesRevoked || !child.OwnerAcknowledged))
        {
            throw new InvalidOperationException(
                "Closed durable facility-equipment join is not terminal: "
                + step);
        }
    }

    internal static bool ClaimsDurableIdentity(
        FacilityBufferDestinationCustodyDrainSnapshot child) =>
        child != null
        && (child.OwnerStableId.StartsWith(
                DurableFacilityEquipmentSlotIdentity.OwnerStableIdPrefix,
                StringComparison.Ordinal)
            || child.ParentOperationId.StartsWith(
                DurableFacilityEquipmentSlotIdentity
                    .DrainParentOperationPrefix,
                StringComparison.Ordinal)
            || child.StepOperationId.StartsWith(
                DurableFacilityEquipmentSlotIdentity
                    .DrainParentOperationPrefix,
                StringComparison.Ordinal)
            || child.SourceDestinationId.StartsWith(
                DurableFacilityEquipmentSlotIdentity.DestinationPrefix,
                StringComparison.Ordinal));
}
