using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Detached save preflight for the Medical-owned semantic half of a pending
/// surgical-part production publication. Production owns the pending commit,
/// Items owns the physical stack, and Surgery owns the part instance. This
/// validator joins all three before any staged aggregate is published.
/// Historical parts whose production owner has already checkpoint-collected
/// are intentionally outside this active-owner join.
/// </summary>
public sealed class SurgicalPartProductionOutputCrossAggregateSaveValidation :
    IDungeonSavePreflightValidator,
    IDungeonSaveRegistryPreflightValidator
{
    public void Validate(
        DungeonGameSaveData saveData,
        DungeonGameRestoreReport report)
    {
        if (saveData == null)
            throw new ArgumentNullException(nameof(saveData));
        if (report == null)
            throw new ArgumentNullException(nameof(report));
        if (!DungeonSaveSectionPayload.TryRead(
                saveData,
                ProductionBillsSaveSection.Id,
                out DungeonProductionBillSaveData production))
        {
            return;
        }

        try
        {
            ValidateIfRequired(
                production,
                ReadRequired<DungeonPhysicalItemSaveData>(
                    saveData,
                    PhysicalItemsSaveSection.Id),
                ReadRequired<DungeonSurgerySaveData>(
                    saveData,
                    SurgerySaveSection.Id));
        }
        catch (Exception exception)
        {
            report.AddError(
                "Surgical-part production cross-aggregate preflight failed: "
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
        if (!envelopes.TryGetValue(
                ProductionBillsSaveSection.Id,
                out DungeonSaveSectionEnvelope productionEnvelope))
        {
            return;
        }

        try
        {
            DungeonProductionBillSaveData production = Parse<
                DungeonProductionBillSaveData>(
                productionEnvelope,
                ProductionBillsSaveSection.Id,
                DungeonProductionBillSaveData.CurrentVersion);
            ValidateIfRequired(
                production,
                ParseRequired<DungeonPhysicalItemSaveData>(
                    envelopes,
                    PhysicalItemsSaveSection.Id,
                    DungeonPhysicalItemSaveData.CurrentVersion),
                ParseRequired<DungeonSurgerySaveData>(
                    envelopes,
                    SurgerySaveSection.Id,
                    DungeonSurgerySaveData.CurrentVersion));
        }
        catch (Exception exception)
        {
            report.AddError(
                "Surgical-part production registry preflight failed: "
                + exception.Message);
        }
    }

    internal static void ValidateIfRequired(
        DungeonProductionBillSaveData production,
        DungeonPhysicalItemSaveData physical,
        DungeonSurgerySaveData surgery)
    {
        ActiveOwner[] owners = CaptureActiveOwners(production);
        if (owners.Length == 0)
            return;
        if (physical?.stacks == null)
        {
            throw new InvalidOperationException(
                "Active surgical output requires the physical stack collection.");
        }
        if (surgery?.parts == null)
        {
            throw new InvalidOperationException(
                "Active surgical output requires the surgery part collection.");
        }

        Dictionary<string, SurgicalPartInstance[]> partsByCommit = surgery.parts
            .Where(value => value != null
                && !string.IsNullOrEmpty(value.sourceProductionCommitId))
            .GroupBy(
                value => value.sourceProductionCommitId,
                StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.ToArray(),
                StringComparer.Ordinal);

        foreach (ActiveOwner owner in owners)
        {
            if (!partsByCommit.TryGetValue(
                    owner.Output.pendingCommitId,
                    out SurgicalPartInstance[] matchingParts)
                || matchingParts.Length != 1)
            {
                throw new InvalidOperationException(
                    "Active surgical output has a missing or duplicate surgery owner: "
                    + owner.Output.pendingCommitId);
            }

            WorldItemStackSaveData[] matchingStacks = physical.stacks
                .Where(value => value != null
                    && PlannedOutputPublicationComponentCodec.HasBatchCommitId(
                        value.components,
                        owner.Output.pendingCommitId))
                .ToArray();
            if (matchingStacks.Length != 1)
            {
                throw new InvalidOperationException(
                    "Active surgical output has a missing or duplicate physical stack: "
                    + owner.Output.pendingCommitId);
            }
            ValidateOwner(
                owner,
                matchingParts[0],
                matchingStacks[0]);
        }
    }

    private static ActiveOwner[] CaptureActiveOwners(
        DungeonProductionBillSaveData production)
    {
        if (production?.bills == null)
            return Array.Empty<ActiveOwner>();
        Dictionary<string, ActiveOwner> owners = new(StringComparer.Ordinal);
        foreach (ProductionBillSaveData bill in production.bills
                     .Where(value => value != null)
                     .OrderBy(value => value.billId, StringComparer.Ordinal))
        {
            foreach (ProductionResolvedOutputSaveData output in
                     (bill.resolvedOutputs
                         ?? new List<ProductionResolvedOutputSaveData>())
                     .Where(value => value != null
                         && string.Equals(
                             value.outputCapabilityId,
                             SurgicalPartProductionOutputHandler
                                 .HandlerCapabilityId,
                             StringComparison.Ordinal)
                         && !string.IsNullOrEmpty(value.pendingCommitId))
                     .OrderBy(value => value.outputLineId, StringComparer.Ordinal))
            {
                if (output.outputCapabilityVersion !=
                        SurgicalPartProductionOutputHandler
                            .HandlerContractVersion
                    || !string.Equals(
                        output.outputComponentCodecId,
                        SurgicalPartProductionOutputHandler
                            .HandlerComponentCodecId,
                        StringComparison.Ordinal)
                    || output.outputComponentCodecVersion !=
                        SurgicalPartProductionOutputHandler
                            .HandlerComponentCodecVersion
                    || !ProductionOutputCommitIdentity.IsOwnedCommitId(
                        output.pendingCommitId)
                    || !owners.TryAdd(
                        output.pendingCommitId,
                        new ActiveOwner(bill, output)))
                {
                    throw new InvalidOperationException(
                        "Active surgical output descriptor or commit identity is invalid: "
                        + output.pendingCommitId);
                }
            }
        }
        return owners.Values
            .OrderBy(value => value.Output.pendingCommitId, StringComparer.Ordinal)
            .ToArray();
    }

    private static void ValidateOwner(
        ActiveOwner owner,
        SurgicalPartInstance part,
        WorldItemStackSaveData stack)
    {
        ProductionResolvedOutputSaveData output = owner.Output;
        if (part == null
            || stack == null
            || stack.quantity != 1
            || stack.state != WorldItemStackState.FacilityOutputBuffer
            || string.IsNullOrWhiteSpace(stack.itemInstanceId)
            || !string.Equals(
                stack.itemId,
                output.itemId,
                StringComparison.Ordinal)
            || !string.Equals(
                stack.destinationId,
                owner.Bill.outputDestinationId,
                StringComparison.Ordinal)
            || !string.Equals(
                part.worldStackId,
                stack.stackId,
                StringComparison.Ordinal)
            || !SurgicalPartPreparedOutputComponentCodec.TryRead(
                stack.components,
                out string componentPartId,
                out string componentNodeId,
                out SurgicalPartKind componentKind,
                out float componentQuality,
                out string componentCommitId)
            || !string.Equals(
                componentPartId,
                part.partInstanceId,
                StringComparison.Ordinal)
            || !string.Equals(
                componentNodeId,
                part.nodeId,
                StringComparison.Ordinal)
            || componentKind != part.kind
            || componentQuality != part.quality
            || !string.Equals(
                componentCommitId,
                output.pendingCommitId,
                StringComparison.Ordinal)
            || !string.Equals(
                part.sourceProductionCommitId,
                output.pendingCommitId,
                StringComparison.Ordinal)
            || !PlannedOutputPublicationComponentCodec.TryRead(
                stack.components,
                out PlannedOutputPublicationMetadata publication)
            || !string.Equals(
                publication.BatchCommitId,
                output.pendingCommitId,
                StringComparison.Ordinal)
            || !string.Equals(
                publication.OutputLineId,
                output.outputLineId,
                StringComparison.Ordinal)
            || !string.Equals(
                publication.ItemId,
                output.itemId,
                StringComparison.Ordinal)
            || publication.Quantity != 1
            || publication.MassGrams <= 0L)
        {
            throw new InvalidOperationException(
                "Active surgical output semantic owner does not match its physical publication: "
                + output.pendingCommitId);
        }

        if (!output.pendingCommitApplied)
        {
            if (publication.Acknowledged
                || output.pendingOutputPublication == null
                || output.pendingOutputPublication.phase !=
                    ProductionExactOutputPublicationPhase.None)
            {
                throw new InvalidOperationException(
                    "Unapplied surgical output has post-commit publication state: "
                    + output.pendingCommitId);
            }
            return;
        }

        ProductionExactOutputPublicationSaveData envelope =
            output.pendingOutputPublication;
        ProductionExactOutputPublicationStackSaveData[] envelopeStacks =
            (envelope?.stacks
                ?? new List<ProductionExactOutputPublicationStackSaveData>())
            .Where(value => value != null)
            .ToArray();
        if (envelope == null
            || envelope.phase != ProductionExactOutputPublicationPhase.Published
            || !string.Equals(
                envelope.commitId,
                output.pendingCommitId,
                StringComparison.Ordinal)
            || !string.Equals(
                envelope.outputCapabilityId,
                SurgicalPartProductionOutputHandler.HandlerCapabilityId,
                StringComparison.Ordinal)
            || envelopeStacks.Length != 1
            || !string.Equals(
                envelopeStacks[0].stackId,
                stack.stackId,
                StringComparison.Ordinal)
            || !string.Equals(
                envelopeStacks[0].itemInstanceId,
                stack.itemInstanceId,
                StringComparison.Ordinal)
            || envelopeStacks[0].quantity != 1
            || envelopeStacks[0].massGrams != publication.MassGrams)
        {
            throw new InvalidOperationException(
                "Applied surgical output envelope does not match its surgery/physical owner: "
                + output.pendingCommitId);
        }
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
        return Parse<TPayload>(envelope, sectionId, currentVersion);
    }

    private static TPayload Parse<TPayload>(
        DungeonSaveSectionEnvelope envelope,
        string sectionId,
        int currentVersion)
        where TPayload : class
    {
        if (envelope == null
            || !string.Equals(
                envelope.sectionId,
                sectionId,
                StringComparison.Ordinal)
            || envelope.sectionVersion != currentVersion
            || string.IsNullOrWhiteSpace(envelope.payloadJson))
        {
            throw new InvalidOperationException(
                "Save section envelope is not exact current format: " + sectionId);
        }
        return JsonUtility.FromJson<TPayload>(envelope.payloadJson)
            ?? throw new InvalidOperationException(
                "Save section payload deserialized to null: " + sectionId);
    }

    private readonly struct ActiveOwner
    {
        internal ActiveOwner(
            ProductionBillSaveData bill,
            ProductionResolvedOutputSaveData output)
        {
            Bill = bill;
            Output = output;
        }

        internal ProductionBillSaveData Bill { get; }
        internal ProductionResolvedOutputSaveData Output { get; }
    }
}
