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
    IDungeonSaveRegistryPreflightValidator,
    IDungeonCapturedSavePreflightValidator
{
    private readonly IAnatomyProfileCatalog anatomyProfiles;

    public SurgicalPartProductionOutputCrossAggregateSaveValidation(
        IAnatomyProfileCatalog anatomyProfiles)
    {
        this.anatomyProfiles = anatomyProfiles
            ?? throw new ArgumentNullException(nameof(anatomyProfiles));
    }

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
            DungeonSaveSectionPayload.TryRead(
                saveData,
                ProductionBillsSaveSection.Id,
                out DungeonProductionBillSaveData production);
            ValidateAll(
                production,
                ReadRequired<DungeonPhysicalItemSaveData>(
                    saveData,
                    PhysicalItemsSaveSection.Id),
                ReadRequired<DungeonSurgerySaveData>(
                    saveData,
                    SurgerySaveSection.Id),
                ReadRequired<DungeonCharacterBodyHealthSaveData>(
                    saveData,
                    CharacterBodyHealthSaveSection.Id));
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
        try
        {
            DungeonProductionBillSaveData production =
                envelopes.TryGetValue(
                    ProductionBillsSaveSection.Id,
                    out DungeonSaveSectionEnvelope productionEnvelope)
                    ? Parse<DungeonProductionBillSaveData>(
                        productionEnvelope,
                        ProductionBillsSaveSection.Id,
                        DungeonProductionBillSaveData.CurrentVersion)
                    : new DungeonProductionBillSaveData();
            ValidateAll(
                production,
                ParseRequired<DungeonPhysicalItemSaveData>(
                    envelopes,
                    PhysicalItemsSaveSection.Id,
                    DungeonPhysicalItemSaveData.CurrentVersion),
                ParseRequired<DungeonSurgerySaveData>(
                    envelopes,
                    SurgerySaveSection.Id,
                    DungeonSurgerySaveData.CurrentVersion),
                ParseRequired<DungeonCharacterBodyHealthSaveData>(
                    envelopes,
                    CharacterBodyHealthSaveSection.Id,
                    DungeonCharacterBodyHealthSaveData.CurrentVersion));
        }
        catch (Exception exception)
        {
            report.AddError(
                "Surgical-part production registry preflight failed: "
                + exception.Message);
        }
    }

    private void ValidateAll(
        DungeonProductionBillSaveData production,
        DungeonPhysicalItemSaveData physical,
        DungeonSurgerySaveData surgery,
        DungeonCharacterBodyHealthSaveData body)
    {
        ValidateIfRequired(production, physical, surgery);
        ValidatePartOwnership(physical, surgery, body, anatomyProfiles);
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

    // Editor fixtures may exercise detached physical/surgery joins without a
    // content catalog. That legacy seam deliberately validates exact node IDs
    // only; production preflight always uses the authoritative overload below.
#if UNITY_EDITOR
    public static void ValidatePartOwnership(
        DungeonPhysicalItemSaveData physical,
        DungeonSurgerySaveData surgery,
        DungeonCharacterBodyHealthSaveData body)
    {
        ValidatePartOwnership(
            physical,
            surgery,
            body,
            anatomyProfiles: null,
            exactNodeOnly: true);
    }
#endif

    public static void ValidatePartOwnership(
        DungeonPhysicalItemSaveData physical,
        DungeonSurgerySaveData surgery,
        DungeonCharacterBodyHealthSaveData body,
        IAnatomyProfileCatalog anatomyProfiles)
    {
        if (anatomyProfiles == null)
            throw new ArgumentNullException(nameof(anatomyProfiles));
        ValidatePartOwnership(
            physical,
            surgery,
            body,
            anatomyProfiles,
            exactNodeOnly: false);
    }

    private static void ValidatePartOwnership(
        DungeonPhysicalItemSaveData physical,
        DungeonSurgerySaveData surgery,
        DungeonCharacterBodyHealthSaveData body,
        IAnatomyProfileCatalog anatomyProfiles,
        bool exactNodeOnly)
    {
        if (physical?.stacks == null
            || physical.pendingBatchDispositions == null
            || surgery?.parts == null
            || surgery.orders == null
            || surgery.wildlifeAnatomy == null
            || body?.characters == null)
        {
            throw new InvalidOperationException(
                "Surgical-part ownership join requires physical, surgery, and body collections.");
        }

        SurgicalPartInstance[] parts = surgery.parts
            .Where(value => value != null)
            .ToArray();
        if (parts.Any(value =>
                !((ItemDefinitionId)value.itemDefinitionId).IsValid
                || !string.Equals(value.itemDefinitionId,
                    ((ItemDefinitionId)value.itemDefinitionId).Value,
                    StringComparison.Ordinal)
                || !((ItemInstanceId)value.physicalItemInstanceId).IsValid
                || !string.Equals(value.physicalItemInstanceId,
                    ((ItemInstanceId)value.physicalItemInstanceId).Value,
                    StringComparison.Ordinal)))
        {
            throw new InvalidOperationException(
                "Surgical parts contain an invalid typed physical identity.");
        }
        if (parts.Select(value => value.physicalItemInstanceId)
            .Distinct(StringComparer.Ordinal).Count() != parts.Length)
        {
            throw new InvalidOperationException(
                "Surgical parts duplicate a physical item-instance identity.");
        }
        SurgeryOrder[] activeOrders = surgery.orders
            .Where(value => value?.IsActive == true)
            .ToArray();
        if (activeOrders.Any(order => order.HasAnyMaterialAuthority
                && !order.OwnsMaterialAuthority)
            || activeOrders.Where(order => order.OwnsMaterialAuthority)
                .GroupBy(order => order.facilityId, StringComparer.Ordinal)
                .Any(group => group.Count() != 1))
        {
            throw new InvalidOperationException(
                "Active surgery material authority is partial or has multiple facility owners.");
        }
        SurgeryOrder[] transitionalReplacements = surgery.orders
            .Where(order => order?.IsActive == true
                && (order.replacementPhase is
                    SurgicalPartReplacementPhase.OutputReservationPending
                    or SurgicalPartReplacementPhase.OutputReserved
                    or SurgicalPartReplacementPhase.BodyCommitted
                    or SurgicalPartReplacementPhase.OutputPublished))
            .ToArray();
        if (transitionalReplacements
                .GroupBy(order => order.replacementExpectedOldPartId,
                    StringComparer.Ordinal)
                .Any(group => group.Count() != 1)
            || transitionalReplacements
                .GroupBy(order => order.replacementIncomingPartId,
                    StringComparer.Ordinal)
                .Any(group => group.Count() != 1))
        {
            throw new InvalidOperationException(
                "Active surgical replacements duplicate an old or incoming part owner.");
        }
        Dictionary<string, SurgeryOrder> transitionalOldOwners =
            transitionalReplacements.ToDictionary(
                order => order.replacementExpectedOldPartId,
                StringComparer.Ordinal);
        Dictionary<string, SurgeryOrder> transitionalIncomingOwners =
            transitionalReplacements.ToDictionary(
                order => order.replacementIncomingPartId,
                StringComparer.Ordinal);
        HashSet<string> releasedTerminalRecoveryParts = surgery.orders
            .Where(order => order != null
                && !order.IsActive
                && order.replacementPhase ==
                    SurgicalPartReplacementPhase.Completed)
            .Select(order => order.replacementExpectedOldPartId)
            .Where(value => !string.IsNullOrEmpty(value))
            .ToHashSet(StringComparer.Ordinal);

        List<AnatomyOwner> anatomyOwners = new();
        foreach (CharacterBodyHealthState character in body.characters
                     .Where(value => value != null))
        {
            anatomyOwners.AddRange((character.anatomyNodes
                    ?? new List<AnatomyNodeHealthState>())
                .Where(node => node != null
                    && !string.IsNullOrEmpty(node.installedPartId))
                .Select(node => new AnatomyOwner(
                    character.characterId,
                    character.anatomyProfileId,
                    node)));
        }
        foreach (WildlifeAnatomyState wildlife in surgery.wildlifeAnatomy
                     .Where(value => value != null))
        {
            anatomyOwners.AddRange((wildlife.nodes
                    ?? new List<AnatomyNodeHealthState>())
                .Where(node => node != null
                    && !string.IsNullOrEmpty(node.installedPartId))
                .Select(node => new AnatomyOwner(
                    wildlife.wildlifeId,
                    wildlife.profileId,
                    node)));
        }

        Dictionary<string, SurgicalPartInstance> partsById = parts.ToDictionary(
            value => value.partInstanceId,
            StringComparer.Ordinal);
        foreach (AnatomyOwner owner in anatomyOwners)
        {
            if (!partsById.TryGetValue(
                    owner.Node.installedPartId,
                    out SurgicalPartInstance part))
            {
                throw new InvalidOperationException(
                    "Anatomy installed-part reference has no exact surgery owner: "
                    + owner.Node.installedPartId);
            }
            bool transitionalIncoming = transitionalIncomingOwners.TryGetValue(
                    part.partInstanceId,
                    out SurgeryOrder replacementOwner)
                && (replacementOwner.replacementPhase is
                    SurgicalPartReplacementPhase.BodyCommitted
                    or SurgicalPartReplacementPhase.OutputPublished);
            if ((!part.installed && !transitionalIncoming)
                || !string.Equals(
                    transitionalIncoming
                        ? replacementOwner.subject?.subjectId
                        : part.installedSubjectId,
                    owner.SubjectId,
                    StringComparison.Ordinal)
                || !IsPartCompatibleWithOwner(
                    anatomyProfiles,
                    owner,
                    part.nodeId,
                    exactNodeOnly)
                || part.kind != owner.Node.installedPartKind)
            {
                throw new InvalidOperationException(
                    "Anatomy installed-part reference has no exact surgery owner: "
                    + owner.Node.installedPartId);
            }
        }

        foreach (SurgicalPartInstance part in parts)
        {
            WorldItemStackSaveData[] stacks = physical.stacks
                .Where(stack => stack != null
                    && string.Equals(
                        stack.itemInstanceId,
                        part.physicalItemInstanceId,
                        StringComparison.Ordinal))
                .ToArray();
            AnatomyOwner[] owners = anatomyOwners
                .Where(owner => string.Equals(
                    owner.Node.installedPartId,
                    part.partInstanceId,
                    StringComparison.Ordinal))
                .ToArray();
            if (transitionalOldOwners.TryGetValue(
                    part.partInstanceId,
                    out SurgeryOrder oldOwner)
                && oldOwner.replacementPhase ==
                    SurgicalPartReplacementPhase.BodyCommitted)
            {
                if (!part.installed
                    || stacks.Length != 0
                    || owners.Length != 0
                    || !string.Equals(
                        part.installedSubjectId,
                        oldOwner.subject?.subjectId,
                        StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        "Body-committed replacement old part has conflicting ownership: "
                        + part.partInstanceId);
                }
                continue;
            }
            if (transitionalIncomingOwners.TryGetValue(
                    part.partInstanceId,
                    out SurgeryOrder incomingOwner)
                && (incomingOwner.replacementPhase is
                    SurgicalPartReplacementPhase.BodyCommitted
                    or SurgicalPartReplacementPhase.OutputPublished))
            {
                ValidateTransitionalIncomingOwner(
                    physical,
                    incomingOwner,
                    part,
                    stacks,
                    owners);
                continue;
            }
            if (part.installed)
            {
                if (stacks.Length != 0
                    || owners.Length != 1
                    || !string.IsNullOrEmpty(part.worldStackId))
                {
                    throw new InvalidOperationException(
                        "Installed surgical part lacks one anatomy owner: "
                        + part.partInstanceId);
                }
                continue;
            }

            if (!string.IsNullOrEmpty(part.installationCommitId))
            {
                if (owners.Length != 0 || stacks.Length > 1)
                {
                    throw new InvalidOperationException(
                        "Pending surgical installation has conflicting physical ownership: "
                        + part.partInstanceId);
                }
                continue;
            }

            if (!string.IsNullOrEmpty(part.discardOperationId))
            {
                if (owners.Length != 0 || stacks.Length != 0)
                {
                    throw new InvalidOperationException(
                        "Pending surgical-part discard has conflicting ownership: "
                        + part.partInstanceId);
                }
                ValidateManualDiscardPending(physical, part);
                continue;
            }

            if (owners.Length != 0
                || stacks.Length == 0
                    && !releasedTerminalRecoveryParts.Contains(
                        part.partInstanceId)
                || stacks.Length > 1
                || stacks.Length == 1
                    && (stacks[0].quantity != 1
                        || !string.Equals(
                            stacks[0].stackId,
                            part.worldStackId,
                            StringComparison.Ordinal)
                        || !string.Equals(
                            stacks[0].itemId,
                            part.itemDefinitionId,
                            StringComparison.Ordinal)))
            {
                throw new InvalidOperationException(
                    "Detached surgical part lacks one exact physical owner: "
                    + part.partInstanceId);
            }
            if (stacks.Length == 0)
            {
                // After terminal closure, recovered items resume their ordinary
                // installation/expiry/disposal lifetime. Historical replacement
                // state must not pin the old physical stack forever.
                continue;
            }
            if (!string.IsNullOrEmpty(part.recoveryOrderId)
                && (!SurgicalPartRecoveryComponentCodec.TryRead(
                    stacks[0].components,
                    out string componentPartId,
                    out string componentNodeId,
                    out SurgicalPartKind componentKind,
                    out float componentQuality,
                    out float componentCurrent,
                    out float componentMaximum,
                    out string componentOrderId,
                    out string componentOperationId)
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
                    || componentCurrent != part.detachedDurabilityCurrent
                    || componentMaximum != part.detachedDurabilityMaximum
                    || !string.Equals(
                        componentOrderId,
                        part.recoveryOrderId,
                        StringComparison.Ordinal)
                    || !string.Equals(
                        componentOperationId,
                        part.recoveryOperationId,
                        StringComparison.Ordinal)))
            {
                throw new InvalidOperationException(
                    "Recovered surgical part component does not match its aggregate state: "
                    + part.partInstanceId);
            }
        }


        HashSet<string> manualDiscardOwners = parts
            .Where(part => !string.IsNullOrEmpty(part.discardOperationId))
            .Select(part => part.discardOperationId)
            .ToHashSet(StringComparer.Ordinal);
        foreach (PhysicalItemBatchDispositionSaveData pending in
                 physical.pendingBatchDispositions.Where(value =>
                     value?.operationId?.StartsWith(
                         SurgicalPartDiscardIdentity.OperationPrefix,
                         StringComparison.Ordinal) == true))
        {
            if (!manualDiscardOwners.Contains(pending.operationId))
            {
                throw new InvalidOperationException(
                    "Pending surgical-part discard has no exact medical owner: "
                    + pending.operationId);
            }
        }

        foreach (WorldItemStackSaveData stack in physical.stacks
                     .Where(value => value?.components?.Any(component =>
                         component != null
                         && string.Equals(
                             component.componentTypeId,
                             SurgicalPartRecoveryComponentCodec.ComponentTypeId,
                             StringComparison.Ordinal)) == true))
        {
            if (!SurgicalPartRecoveryComponentCodec.TryRead(
                    stack.components,
                    out string componentPartId,
                    out _,
                    out _,
                    out _,
                    out _,
                    out _,
                    out _,
                    out _)
                || !partsById.TryGetValue(
                    componentPartId,
                    out SurgicalPartInstance componentOwner)
                || !string.Equals(
                    componentOwner.physicalItemInstanceId,
                    stack.itemInstanceId,
                    StringComparison.Ordinal)
                || !string.Equals(
                    componentOwner.itemDefinitionId,
                    stack.itemId,
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "Recovered surgical physical component has no exact medical owner: "
                    + stack.stackId);
            }
        }

        foreach (SurgeryOrder order in transitionalReplacements)
        {
            ValidateTransitionalReplacement(
                physical,
                partsById,
                anatomyOwners,
                order);
        }

        foreach (SurgeryOrder order in surgery.orders.Where(value =>
                     value?.replacementPhase ==
                         SurgicalPartReplacementPhase.Completed))
        {
            if (!order.IsActive)
                continue;
            if (!string.Equals(
                    order.replacementOperationId,
                    SurgicalPartReplacementIdentity.FormatOperationId(
                        order.orderId),
                    StringComparison.Ordinal)
                || !string.Equals(
                    order.replacementBatchCommitId,
                    SurgicalPartReplacementIdentity.FormatBatchCommitId(
                        order.orderId),
                    StringComparison.Ordinal)
                || order.replacementOutputMassGrams <= 0L
                || !partsById.TryGetValue(
                    order.replacementExpectedOldPartId,
                    out SurgicalPartInstance previous)
                || !partsById.TryGetValue(
                    order.replacementIncomingPartId,
                    out SurgicalPartInstance incoming))
            {
                throw new InvalidOperationException(
                    "Completed surgical replacement receipt references missing parts: "
                    + order.orderId);
            }
            if (previous.installed
                || !incoming.installed
                || !SurgicalPartRuntime.ReplacementOutcomeFingerprintMatches(
                    order,
                    previous)
                || !string.Equals(
                    previous.reservedOrderId,
                    order.orderId,
                    StringComparison.Ordinal)
                || !string.Equals(
                    incoming.installedSubjectId,
                    order.subject?.subjectId,
                    StringComparison.Ordinal)
                || !string.Equals(
                    previous.worldStackId,
                    order.replacementOutputStackId,
                    StringComparison.Ordinal)
                || !string.Equals(
                    previous.physicalItemInstanceId,
                    order.replacementOutputItemInstanceId,
                    StringComparison.Ordinal)
                || !string.Equals(
                    previous.recoveryCommitId,
                    order.replacementBatchCommitId,
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "Completed surgical replacement receipt does not join its old and incoming owners: "
                    + order.orderId);
            }
            WorldItemStackSaveData[] completedStacks = physical.stacks
                .Where(value => value != null
                    && string.Equals(
                        value.stackId,
                        order.replacementOutputStackId,
                        StringComparison.Ordinal))
                .ToArray();
            if (completedStacks.Length != 1)
            {
                throw new InvalidOperationException(
                    "Completed surgical replacement lacks its acknowledged physical receipt: "
                    + order.orderId);
            }
            ValidateReplacementPhysicalPublication(
                order,
                previous,
                completedStacks[0],
                requireAcknowledged: true);
        }
    }

    private static bool IsPartCompatibleWithOwner(
        IAnatomyProfileCatalog anatomyProfiles,
        AnatomyOwner owner,
        string partNodeId,
        bool exactNodeOnly)
    {
        if (exactNodeOnly)
        {
            return string.Equals(
                partNodeId,
                owner.Node.nodeId,
                StringComparison.Ordinal);
        }
        if (string.IsNullOrWhiteSpace(owner.ProfileId)
            || !anatomyProfiles.TryGet(
                owner.ProfileId,
                out AnatomyProfileDefinition profile))
        {
            throw new InvalidOperationException(
                "Anatomy installed-part owner references an unknown anatomy profile: "
                + owner.ProfileId);
        }
        return SurgicalPartAnatomyCompatibility.IsCompatible(
            profile,
            partNodeId,
            owner.Node.nodeId);
    }

    private static void ValidateManualDiscardPending(
        DungeonPhysicalItemSaveData physical,
        SurgicalPartInstance part)
    {
        PhysicalItemBatchDispositionSaveData[] pending =
            physical.pendingBatchDispositions
                .Where(value => value != null
                    && string.Equals(
                        value.operationId,
                        part.discardOperationId,
                        StringComparison.Ordinal))
                .ToArray();
        if (pending.Length != 1
            || pending[0].kind != (int)PhysicalItemDispositionKind.Sink
            || !string.Equals(
                pending[0].reasonCode,
                SurgicalPartDiscardIdentity.ReasonCode,
                StringComparison.Ordinal)
            || !string.Equals(
                pending[0].commitId,
                part.discardCommitId,
                StringComparison.Ordinal)
            || pending[0].quantity != 1
            || pending[0].inputMassGrams != part.discardInputMassGrams
            || pending[0].sourceStackIds?.Count != 1
            || !string.Equals(
                pending[0].sourceStackIds[0],
                part.discardSourceStackId,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Pending surgical-part discard lacks its exact physical Sink receipt: "
                + part.partInstanceId);
        }
    }

    private static void ValidateTransitionalIncomingOwner(
        DungeonPhysicalItemSaveData physical,
        SurgeryOrder order,
        SurgicalPartInstance incoming,
        IReadOnlyList<WorldItemStackSaveData> stacks,
        IReadOnlyList<AnatomyOwner> owners)
    {
        if (owners.Count != 1
            || !string.Equals(
                owners[0].SubjectId,
                order.subject?.subjectId,
                StringComparison.Ordinal)
            || !string.Equals(
                owners[0].Node.nodeId,
                order.targetNodeId,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Body-committed replacement incoming part lacks its anatomy owner: "
                + incoming.partInstanceId);
        }

        if (incoming.installed)
        {
            if (stacks.Count != 0
                || !string.IsNullOrEmpty(incoming.worldStackId)
                || !string.IsNullOrEmpty(incoming.reservedOrderId)
                || !string.Equals(
                    incoming.installedSubjectId,
                    order.subject?.subjectId,
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "Body-committed replacement incoming part has conflicting terminal ownership: "
                    + incoming.partInstanceId);
            }
            ValidateIncomingPendingTransfer(
                physical,
                incoming,
                pendingRequired: false);
            return;
        }

        if (!string.IsNullOrEmpty(incoming.installationCommitId))
        {
            if (stacks.Count != 0)
            {
                throw new InvalidOperationException(
                    "Body-committed replacement incoming part remains both physical and pending: "
                    + incoming.partInstanceId);
            }
            ValidateIncomingPendingTransfer(
                physical,
                incoming,
                pendingRequired: true);
            return;
        }

        if (stacks.Count != 1
            || stacks[0].quantity != 1
            || !string.Equals(stacks[0].stackId,
                incoming.worldStackId, StringComparison.Ordinal)
            || !string.Equals(stacks[0].itemId,
                incoming.itemDefinitionId, StringComparison.Ordinal)
            || !string.Equals(stacks[0].itemInstanceId,
                incoming.physicalItemInstanceId, StringComparison.Ordinal)
            || !string.Equals(incoming.reservedOrderId,
                order.orderId, StringComparison.Ordinal)
            || !string.IsNullOrEmpty(incoming.installationOperationId))
        {
            throw new InvalidOperationException(
                "Body-committed replacement incoming cargo was not preserved before transfer: "
                + incoming.partInstanceId);
        }
    }

    private static void ValidateIncomingPendingTransfer(
        DungeonPhysicalItemSaveData physical,
        SurgicalPartInstance incoming,
        bool pendingRequired)
    {
        PhysicalItemBatchDispositionSaveData[] pending =
            (physical.pendingBatchDispositions
                ?? new List<PhysicalItemBatchDispositionSaveData>())
            .Where(value => value != null
                && string.Equals(value.operationId,
                    incoming.installationOperationId,
                    StringComparison.Ordinal))
            .ToArray();
        if (pending.Length == 0 && !pendingRequired)
            return;
        if (pending.Length != 1
            || pending[0].kind != (int)PhysicalItemDispositionKind.Transfer
            || !string.Equals(pending[0].reasonCode,
                SurgicalPartInstallationOutbox.TransferReason,
                StringComparison.Ordinal)
            || !string.Equals(pending[0].commitId,
                incoming.installationCommitId,
                StringComparison.Ordinal)
            || pending[0].quantity != 1
            || pending[0].inputMassGrams <= 0L
            || pending[0].sourceStackIds?.Count != 1
            || !string.Equals(pending[0].sourceStackIds[0],
                incoming.installationSourceStackId,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Replacement incoming transfer has no exact pending physical receipt: "
                + incoming.partInstanceId);
        }
    }

    private static void ValidateTransitionalReplacement(
        DungeonPhysicalItemSaveData physical,
        IReadOnlyDictionary<string, SurgicalPartInstance> partsById,
        IReadOnlyList<AnatomyOwner> anatomyOwners,
        SurgeryOrder order)
    {
        if (!partsById.TryGetValue(
                order.replacementExpectedOldPartId,
                out SurgicalPartInstance previous)
            || !partsById.TryGetValue(
                order.replacementIncomingPartId,
                out SurgicalPartInstance incoming))
        {
            throw new InvalidOperationException(
                "Active replacement references missing surgery parts: "
                + order.orderId);
        }
        bool reservationPending = order.replacementPhase ==
            SurgicalPartReplacementPhase.OutputReservationPending;
        if (!reservationPending
            && !SurgicalPartRuntime.ReplacementOutcomeFingerprintMatches(
                order,
                previous))
        {
            throw new InvalidOperationException(
                "Active replacement outcome fingerprint does not match its frozen recovery payload: "
                + order.orderId);
        }
        AnatomyOwner[] subjectNodes = anatomyOwners.Where(owner =>
                string.Equals(owner.SubjectId,
                    order.subject?.subjectId, StringComparison.Ordinal)
                && string.Equals(owner.Node.nodeId,
                    order.targetNodeId, StringComparison.Ordinal))
            .ToArray();
        string expectedBodyPart = order.replacementPhase is
                SurgicalPartReplacementPhase.OutputReservationPending
                or SurgicalPartReplacementPhase.OutputReserved
            ? previous.partInstanceId
            : incoming.partInstanceId;
        if (subjectNodes.Length != 1
            || !string.Equals(subjectNodes[0].Node.installedPartId,
                expectedBodyPart, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Active replacement does not join its exact body node: "
                + order.orderId);
        }

        WorldItemStackSaveData[] recoveryStacks = physical.stacks
            .Where(value => value != null
                && PlannedOutputPublicationComponentCodec.HasBatchCommitId(
                    value.components,
                    order.replacementBatchCommitId))
                .ToArray();
        if (reservationPending)
        {
            WorldItemStackSaveData[] incomingStacks = physical.stacks
                .Where(value => value != null
                    && string.Equals(value.itemInstanceId,
                        incoming.physicalItemInstanceId,
                        StringComparison.Ordinal))
                .ToArray();
            if (!previous.installed
                || incoming.installed
                || incomingStacks.Length != 1
                || incomingStacks[0].quantity != 1
                || !string.Equals(incomingStacks[0].stackId,
                    incoming.worldStackId, StringComparison.Ordinal)
                || !string.Equals(incomingStacks[0].itemId,
                    incoming.itemDefinitionId, StringComparison.Ordinal)
                || !string.Equals(incoming.reservedOrderId,
                    order.orderId, StringComparison.Ordinal)
                || !string.IsNullOrEmpty(incoming.installationOperationId)
                || recoveryStacks.Length != 0)
            {
                throw new InvalidOperationException(
                    "Pending replacement reservation lost its body or incoming cargo: "
                    + order.orderId);
            }
            return;
        }
        if (order.replacementPhase ==
            SurgicalPartReplacementPhase.OutputReserved)
        {
            WorldItemStackSaveData[] incomingStacks = physical.stacks
                .Where(value => value != null
                    && string.Equals(value.itemInstanceId,
                        incoming.physicalItemInstanceId,
                        StringComparison.Ordinal))
                .ToArray();
            if (!previous.installed
                || incoming.installed
                || incomingStacks.Length != 1
                || incomingStacks[0].quantity != 1
                || !string.Equals(incomingStacks[0].stackId,
                    incoming.worldStackId, StringComparison.Ordinal)
                || !string.Equals(incomingStacks[0].itemId,
                    incoming.itemDefinitionId, StringComparison.Ordinal)
                || !string.Equals(incoming.reservedOrderId,
                    order.orderId, StringComparison.Ordinal)
                || !string.IsNullOrEmpty(incoming.installationOperationId)
                || recoveryStacks.Length != 0)
            {
                throw new InvalidOperationException(
                    "Output-reserved replacement does not preserve its body and incoming cargo: "
                    + order.orderId);
            }
            return;
        }

        if (order.replacementPhase ==
            SurgicalPartReplacementPhase.BodyCommitted)
        {
            if (!previous.installed || recoveryStacks.Length != 0)
            {
                throw new InvalidOperationException(
                    "Body-committed replacement published or detached its old part early: "
                    + order.orderId);
            }
            return;
        }

        if (recoveryStacks.Length != 1
            || previous.installed
            || !incoming.installed
            || !string.Equals(previous.worldStackId,
                order.replacementOutputStackId, StringComparison.Ordinal)
            || !string.Equals(previous.physicalItemInstanceId,
                order.replacementOutputItemInstanceId,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Output-published replacement has conflicting part ownership: "
                + order.orderId);
        }
        ValidateReplacementPhysicalPublication(
            order,
            previous,
            recoveryStacks[0],
            requireAcknowledged: false);
    }

    private static void ValidateReplacementPhysicalPublication(
        SurgeryOrder order,
        SurgicalPartInstance previous,
        WorldItemStackSaveData stack,
        bool requireAcknowledged)
    {
        if (stack == null
            || stack.quantity != 1
            || stack.state != WorldItemStackState.FacilityOutputBuffer
            || stack.gridX != order.replacementOutputX
            || stack.gridY != order.replacementOutputY
            || !string.Equals(stack.destinationId,
                order.materialDestinationId, StringComparison.Ordinal)
            || !string.Equals(stack.itemId,
                previous.itemDefinitionId, StringComparison.Ordinal)
            || !string.Equals(stack.itemInstanceId,
                previous.physicalItemInstanceId, StringComparison.Ordinal)
            || !PlannedOutputPublicationComponentCodec.TryRead(
                stack.components,
                out PlannedOutputPublicationMetadata publication)
            || requireAcknowledged && !publication.Acknowledged
            || !string.Equals(publication.BatchCommitId,
                order.replacementBatchCommitId, StringComparison.Ordinal)
            || !string.Equals(publication.OutcomeFingerprint,
                order.replacementOutcomeFingerprint, StringComparison.Ordinal)
            || !string.Equals(publication.PlannedOutputFingerprint,
                order.replacementPlannedOutputFingerprint,
                StringComparison.Ordinal)
            || !string.Equals(publication.OutputLineId,
                SurgicalPartReplacementIdentity.OutputLineId,
                StringComparison.Ordinal)
            || !string.Equals(publication.ItemId,
                previous.itemDefinitionId, StringComparison.Ordinal)
            || publication.Quantity != 1
            || publication.MassGrams != order.replacementOutputMassGrams)
        {
            throw new InvalidOperationException(
                "Replacement physical publication does not match its durable receipt: "
                + order.orderId);
        }
    }

#if UNITY_EDITOR
    public static string CreateReplacementOutcomeFingerprintForEditor(
        SurgicalPartInstance previous,
        SurgeryOrder order)
    {
        return SurgicalPartRuntime.CreateReplacementOutcomeFingerprintForValidation(
            order,
            previous);
    }

    public static IReadOnlyList<ItemInstanceComponentSaveData>
        CreateReplacementPhysicalComponentsForEditor(
            SurgicalPartInstance previous,
            SurgeryOrder order,
            long massGrams)
    {
        ItemInstanceComponentSaveData recovery =
            SurgicalPartRecoveryComponentCodec.Create(
                previous,
                order.orderId,
                order.replacementOperationId,
                order.replacementDetachedCurrentHealth,
                order.replacementDetachedMaxHealth);
        ItemInstanceComponentSaveData provenance =
            PlannedOutputPublicationComponentCodec.CreateProvenance(
                new PlannedOutputPublicationMetadata(
                    order.replacementBatchCommitId,
                    order.replacementOutcomeFingerprint,
                    order.replacementPlannedOutputFingerprint,
                    SurgicalPartReplacementIdentity.OutputLineId,
                    0,
                    1,
                    1,
                    massGrams,
                    1,
                    1,
                    massGrams,
                    previous.itemDefinitionId,
                    1,
                    massGrams,
                    string.Empty,
                    string.Empty,
                    acknowledged: true));
        return new[] { recovery, provenance };
    }
#endif

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
            || !string.Equals(
                part.itemDefinitionId,
                stack.itemId,
                StringComparison.Ordinal)
            || !string.Equals(
                part.physicalItemInstanceId,
                stack.itemInstanceId,
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

    private readonly struct AnatomyOwner
    {
        internal AnatomyOwner(
            string subjectId,
            string profileId,
            AnatomyNodeHealthState node)
        {
            SubjectId = subjectId ?? string.Empty;
            ProfileId = profileId ?? string.Empty;
            Node = node;
        }

        internal string SubjectId { get; }
        internal string ProfileId { get; }
        internal AnatomyNodeHealthState Node { get; }
    }
}
