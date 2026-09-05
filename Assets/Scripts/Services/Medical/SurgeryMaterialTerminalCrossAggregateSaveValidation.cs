using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Fail-loud raw-envelope join for the Medical-owned terminal receipt and the
/// Items-owned FacilityBuffer custody tombstone. The same validator protects
/// outgoing captures, whole-save restores and direct registry restores.
/// </summary>
public sealed class SurgeryMaterialTerminalCrossAggregateSaveValidation :
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
                ReadRequired<DungeonSurgerySaveData>(
                    saveData,
                    SurgerySaveSection.Id),
                ReadRequired<DungeonPhysicalItemSaveData>(
                    saveData,
                    PhysicalItemsSaveSection.Id));
        }
        catch (Exception exception)
        {
            report.AddError(
                "Surgery material terminal cross-aggregate preflight failed: "
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
                ParseRequired<DungeonSurgerySaveData>(
                    envelopes,
                    SurgerySaveSection.Id,
                    DungeonSurgerySaveData.CurrentVersion),
                ParseRequired<DungeonPhysicalItemSaveData>(
                    envelopes,
                    PhysicalItemsSaveSection.Id,
                    DungeonPhysicalItemSaveData.CurrentVersion));
        }
        catch (Exception exception)
        {
            report.AddError(
                "Surgery material terminal registry preflight failed: "
                + exception.Message);
        }
    }

    internal static void ValidatePayloads(
        DungeonSurgerySaveData surgery,
        DungeonPhysicalItemSaveData physical)
    {
        if (surgery?.orders == null)
        {
            throw new InvalidOperationException(
                "Surgery material terminal join requires the surgery order collection.");
        }
        if (physical?.pendingProductionInputDestinationDrains == null)
        {
            throw new InvalidOperationException(
                "Surgery material terminal join requires the Items custody drain collection.");
        }

        FacilityBufferDestinationCustodyDrainSnapshot[] children = physical
            .pendingProductionInputDestinationDrains
            .Where(value => value != null)
            .Select(FacilityBufferDestinationCustodyDrainProjection
                .ProjectValidated)
            .ToArray();
        SurgeryMaterialTerminalCrossAggregateJoin.Validate(
            surgery.orders,
            children);
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
                out DungeonSaveSectionEnvelope envelope))
        {
            throw new InvalidOperationException(
                "Required save section is missing: " + sectionId);
        }
        if (envelope == null
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

internal static class SurgeryMaterialTerminalCrossAggregateJoin
{
    internal static void Validate(
        IEnumerable<SurgeryOrder> sourceOrders,
        IEnumerable<FacilityBufferDestinationCustodyDrainSnapshot>
            sourceChildren)
    {
        SurgeryOrder[] orders = (sourceOrders ?? Array.Empty<SurgeryOrder>())
            .Where(value => value != null)
            .OrderBy(value => value.orderId, StringComparer.Ordinal)
            .ToArray();
        FacilityBufferDestinationCustodyDrainSnapshot[] children =
            (sourceChildren
                    ?? Array.Empty<
                        FacilityBufferDestinationCustodyDrainSnapshot>())
                .Where(value => value != null)
                .OrderBy(value => value.StepOperationId, StringComparer.Ordinal)
                .ToArray();

        Dictionary<string, FacilityBufferDestinationCustodyDrainSnapshot>
            byStep = new(StringComparer.Ordinal);
        foreach (FacilityBufferDestinationCustodyDrainSnapshot child in children)
        {
            if (!byStep.TryAdd(child.StepOperationId, child))
            {
                throw new InvalidOperationException(
                    "Duplicate FacilityBuffer custody drain step: "
                    + child.StepOperationId);
            }
        }

        Dictionary<string, SurgeryOrder> terminalByStep = new(
            StringComparer.Ordinal);
        foreach (SurgeryOrder order in orders)
        {
            string expectedStep = SurgeryMaterialTerminalIdentity
                .FormatStepOperationId(order.orderId);
            if (order.materialTerminalDrainPhase ==
                SurgeryMaterialTerminalDrainPhase.None)
            {
                if (byStep.ContainsKey(expectedStep))
                {
                    throw new InvalidOperationException(
                        $"Surgery order '{order.orderId}' has an orphan terminal custody child.");
                }
                continue;
            }

            if (!terminalByStep.TryAdd(expectedStep, order))
            {
                throw new InvalidOperationException(
                    "Duplicate Surgery terminal custody owner: "
                    + expectedStep);
            }
            if (!byStep.TryGetValue(expectedStep, out
                    FacilityBufferDestinationCustodyDrainSnapshot child))
            {
                throw new InvalidOperationException(
                    $"Surgery order '{order.orderId}' has no terminal custody child.");
            }
            RequireExactJoin(order, child);
        }

        string surgeryOwnerPrefix = SurgeryMaterialTerminalIdentity
            .FormatOwnerStableId("surgery:");
        foreach (FacilityBufferDestinationCustodyDrainSnapshot child in children)
        {
            if (!child.OwnerStableId.StartsWith(
                    surgeryOwnerPrefix,
                    StringComparison.Ordinal))
            {
                continue;
            }
            if (!terminalByStep.TryGetValue(
                    child.StepOperationId,
                    out SurgeryOrder owner))
            {
                throw new InvalidOperationException(
                    $"Surgery terminal custody child '{child.StepOperationId}' has no Surgery owner.");
            }
            RequireExactJoin(owner, child);
        }
    }

    private static void RequireExactJoin(
        SurgeryOrder owner,
        FacilityBufferDestinationCustodyDrainSnapshot child)
    {
        if (!SurgeryMaterialTerminalJoin.TryValidate(
                owner,
                child,
                out string failureReason))
        {
            throw new InvalidOperationException(
                $"Surgery order '{owner.orderId}' has no exact terminal custody child: "
                + failureReason);
        }
    }
}
