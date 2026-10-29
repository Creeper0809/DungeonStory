using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using DungeonStory.Foundation;
using UnityEngine;
using VContainer.Unity;

public sealed class SurgicalPartRuntime :
    ISurgicalPartRuntime,
    ISurgicalPartPreparedOutputRuntime,
    ISurgicalPartReplacementRuntime,
    ISurgicalAugmentationQuery,
    ITickable
{
    private const float SecondsPerDay = 180f;
    private const float LooseFreshnessSeconds = SecondsPerDay * 2f;
    private const float StoredFreshnessRate = 2f / 15f;
    private const float FuelRefreshInterval = 0.75f;
    private const string FuelDestinationPrefix =
        "surgery-organ-storage-fuel:";
    private const string OrganPreservationCanisterItemId =
        "medical:organ-preservation-canister";
    private const string FreshnessExpiryTransformReason =
        "surgical-organ-expired-to-contaminated-tissue";
    private const string FreshnessExpiryReleaseReason =
        "surgical-organ-expiry-owned-release";

    private readonly IWorldItemStackRuntime items;
    private readonly IItemTransferService itemTransfers;
    private readonly IBuildingWorldQuery buildings;
    private readonly ISurgicalFacilityQuery facilities;
    private readonly IEnvironmentalFieldQuery environment;
    private readonly IAnatomyProfileCatalog anatomyProfiles;
    private readonly SurgicalGraftEffectCatalog graftEffects;
    private readonly IGameClock clock;
    private readonly SurgeryAggregateStateStore stateStore;
    private readonly IPhysicalItemBatchDispositionService batchDispositions;
    private readonly IPhysicalItemTransformService physicalTransforms;
    private readonly IPhysicalItemMassQuery physicalMass;
    private readonly IFacilityBufferDestinationClaimAuthorityQuery
        destinationClaims;
    private readonly ISurgicalPartStorageInputOwnerAuthority storageInputOwners;
    private readonly SurgicalPartInstallationRuntime installations;
    private readonly SurgicalPartReplacementTransaction replacements;
    private float nextFuelRefreshAt;

    private List<SurgicalPartInstance> parts => stateStore.State.Parts;
    private Dictionary<string, SurgicalOrganStorageState> storageStates =>
        stateStore.State.OrganStorage;
    private int sequence
    {
        get => stateStore.State.PartSequence;
        set => stateStore.State.PartSequence = value;
    }

    public SurgicalPartRuntime(
        IWorldItemStackRuntime items,
        IItemTransferService itemTransfers,
        IBuildingWorldQuery buildings,
        ISurgicalFacilityQuery facilities,
        IEnvironmentalFieldQuery environment,
        IAnatomyProfileCatalog anatomyProfiles,
        IGameClock clock,
        SurgeryAggregateStateStore stateStore,
        IPhysicalItemBatchDispositionService batchDispositions,
        IPhysicalItemTransformService physicalTransforms,
        IItemDefinitionCatalog itemCatalog,
        IPhysicalItemMassQuery physicalMass,
        IFacilityBufferDestinationClaimAuthorityQuery destinationClaims,
        IFacilityBufferMassCapacityAuthorityQuery destinationCapacities,
        IFacilityBufferDestinationLifecycleCommand destinationLifecycle,
        IFacilityBufferDestinationReleaseService destinationReleases,
        SurgicalGraftEffectCatalog graftEffects)
    {
        this.items = items ?? throw new ArgumentNullException(nameof(items));
        this.itemTransfers = itemTransfers
            ?? throw new ArgumentNullException(nameof(itemTransfers));
        this.buildings = buildings ?? throw new ArgumentNullException(nameof(buildings));
        this.facilities = facilities ?? throw new ArgumentNullException(nameof(facilities));
        this.environment = environment
            ?? throw new ArgumentNullException(nameof(environment));
        this.anatomyProfiles = anatomyProfiles
            ?? throw new ArgumentNullException(nameof(anatomyProfiles));
        this.graftEffects = graftEffects ?? throw new ArgumentNullException(nameof(graftEffects));
        this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
        this.stateStore = stateStore
            ?? throw new ArgumentNullException(nameof(stateStore));
        this.batchDispositions = batchDispositions
            ?? throw new ArgumentNullException(nameof(batchDispositions));
        this.physicalTransforms = physicalTransforms
            ?? throw new ArgumentNullException(nameof(physicalTransforms));
        this.physicalMass = physicalMass
            ?? throw new ArgumentNullException(nameof(physicalMass));
        this.destinationClaims = destinationClaims
            ?? throw new ArgumentNullException(nameof(destinationClaims));
        storageInputOwners = new SurgicalPartStorageInputOwnerAuthority(
            this.buildings,
            this.facilities,
            itemCatalog,
            physicalMass,
            destinationClaims,
            destinationCapacities,
            destinationLifecycle,
            destinationReleases);
        installations = new SurgicalPartInstallationRuntime(
            this.items, this.stateStore, this.batchDispositions);
        replacements = new SurgicalPartReplacementTransaction(
            installations, this.physicalMass, this.batchDispositions);
    }

    public IReadOnlyList<SurgicalPartInstance> Parts => CaptureParts();

    public void Tick()
    {
        EnsureStorageInputOwners();
        if (!clock.IsPaused && clock.DeltaTime > 0f)
        {
            TickOrganStorageFuel(clock.DeltaTime);
            TickFreshness(clock.DeltaTime);
        }
    }

    public bool TryGet(string partInstanceId, out SurgicalPartInstance part)
    {
        bool found = installations.TryGet(partInstanceId, out SurgicalPartInstance owned);
        part = owned == null ? null : SurgeryStateCloner.ClonePart(owned);
        return found;
    }

    public bool TryCreateExtractedPart(
        SurgicalSubjectRef donor,
        string nodeId,
        SurgicalPartKind kind,
        float quality,
        Vector2Int position,
        out SurgicalPartInstance part,
        out DomainFailure failure)
    {
        part = null;
        failure = DomainFailure.None;
        if (donor == null || !donor.IsValid || string.IsNullOrWhiteSpace(nodeId))
        {
            failure = new DomainFailure(
                FailureCode.SurgeryTargetNodeMissing,
                nodeId ?? string.Empty);
            return false;
        }

        var donorProfile = anatomyProfiles.GetForSpecies(donor.speciesId);
        if (!donorProfile.TryGetNode(nodeId.Trim(), out var donorNode))
        {
            failure = new DomainFailure(FailureCode.SurgeryTargetNodeMissing, nodeId);
            return false;
        }
        var graft = graftEffects.FindForExtraction(donor.speciesId, donorNode);
        SurgeryAggregateState state = stateStore.State;
        if (!state.TryPrepareNextPartIdentity(
                out int nextPartSequence,
                out string partInstanceId,
                out failure))
        {
            return false;
        }

        string itemId = kind == SurgicalPartKind.Prosthetic
            ? SurgeryItemDefinitions.GetProstheticItemId(nodeId)
            : SurgeryItemDefinitions.GetOrganItemId(nodeId);
        if (!items.SpawnUniqueItemAt(
                itemId,
                position,
                WorldItemStackState.Loose,
                string.Empty,
                out string stackId))
        {
            failure = new DomainFailure(FailureCode.SurgeryEffectFailed, itemId);
            return false;
        }

        WorldItemStackSnapshot physical = items.GetAllStacks().SingleOrDefault(
            candidate => candidate != null
                && string.Equals(
                    candidate.StackId,
                    stackId,
                    StringComparison.Ordinal));
        if (physical == null
            || string.IsNullOrWhiteSpace(physical.ItemInstanceId))
        {
            items.DeleteStack(stackId);
            failure = new DomainFailure(
                FailureCode.SurgeryEffectFailed,
                itemId,
                "surgical-part-physical-identity-missing");
            return false;
        }

        part = new SurgicalPartInstance
        {
            partInstanceId = partInstanceId,
            itemDefinitionId = itemId,
            physicalItemInstanceId = physical.ItemInstanceId,
            kind = kind,
            nodeId = nodeId.Trim(),
            displayName = items.CatalogProvider.GetDefinition(itemId).DisplayName,
            donorId = donor.subjectId,
            donorName = donor.displayName,
            donorSpeciesId = donor.speciesId,
            anatomyFamily = ResolveAnatomyFamily(donor),
            quality = Mathf.Clamp(quality, 0.1f, 1.75f),
            specialEffectId = graft != null ? graft.GraftId : string.Empty,
            specialEffectStrength = graft != null ? 1f : 0f,
            freshnessSeconds = kind == SurgicalPartKind.NaturalOrgan
                ? LooseFreshnessSeconds
                : 0f,
            worldStackId = stackId
        };
        sequence = nextPartSequence;
        parts.Add(part);
        RequestOrganStorage(part, position);
        part = SurgeryStateCloner.ClonePart(part);
        return true;
    }

    public string GetSpecialEffectLabel(SurgicalPartInstance part)
    {
        if (string.IsNullOrWhiteSpace(part?.specialEffectId)) return string.Empty;
        var definition = graftEffects.Require(part.specialEffectId);
        return definition.DisplayName + ": " + definition.Description;
    }

    public bool TryCreateCraftedPart(
        string nodeId,
        string displayName,
        SurgicalPartKind kind,
        float quality,
        Vector2Int position,
        string sourceProductionCommitId,
        out SurgicalPartInstance part,
        out DomainFailure failure)
    {
        part = null;
        failure = new DomainFailure(
            FailureCode.ProductionOutputUnavailable,
            sourceProductionCommitId ?? string.Empty,
            "surgical-part-prepared-output-route-required");
        return false;
    }

    bool ISurgicalPartPreparedOutputRuntime.TryPrepareCraftedOutput(
        string itemId,
        string nodeId,
        string displayName,
        SurgicalPartKind kind,
        float quality,
        string commitId,
        out SurgicalPartPreparedOutput prepared,
        out DomainFailure failure)
    {
        prepared = null;
        failure = DomainFailure.None;
        string canonicalCommit = commitId ?? string.Empty;
        if (string.IsNullOrWhiteSpace(itemId)
            || !string.Equals(itemId, itemId.Trim(), StringComparison.Ordinal)
            || string.IsNullOrWhiteSpace(nodeId)
            || !string.Equals(nodeId, nodeId.Trim(), StringComparison.Ordinal)
            || kind == SurgicalPartKind.NaturalOrgan
            || string.IsNullOrWhiteSpace(canonicalCommit)
            || !string.Equals(
                canonicalCommit,
                canonicalCommit.Trim(),
                StringComparison.Ordinal))
        {
            failure = new DomainFailure(
                FailureCode.SurgeryEffectFailed,
                canonicalCommit,
                "crafted-output-identity-invalid");
            return false;
        }

        SurgicalPartInstance existing = parts.SingleOrDefault(candidate =>
            candidate != null
            && string.Equals(
                candidate.sourceProductionCommitId,
                canonicalCommit,
                StringComparison.Ordinal));
        if (existing != null)
        {
            if (existing.kind != kind
                || !string.Equals(existing.nodeId, nodeId, StringComparison.Ordinal)
                || !string.Equals(
                    existing.itemDefinitionId,
                    itemId,
                    StringComparison.Ordinal)
                || !((ItemInstanceId)existing.physicalItemInstanceId).IsValid)
            {
                failure = new DomainFailure(
                    FailureCode.SurgeryEffectFailed,
                    canonicalCommit,
                    "production-commit-conflict");
                return false;
            }
            prepared = new SurgicalPartPreparedOutput
            {
                ItemId = itemId,
                PhysicalItemInstanceId = existing.physicalItemInstanceId,
                PartInstanceId = existing.partInstanceId,
                NodeId = existing.nodeId,
                DisplayName = existing.displayName,
                Kind = existing.kind,
                Quality = existing.quality,
                CommitId = canonicalCommit,
                ExpectedSequence = sequence,
                IsReplay = true
            };
            return true;
        }

        SurgeryAggregateState state = stateStore.State;
        if (!state.TryPrepareNextPartIdentity(
                out int nextPartSequence,
                out string partInstanceId,
                out failure))
        {
            return false;
        }
        DungeonItemDefinition definition = items.CatalogProvider.GetDefinition(itemId);
        if (definition == null || definition.MaxStack != 1)
        {
            failure = new DomainFailure(
                FailureCode.ProductionOutputUnavailable,
                itemId,
                "surgical-part-definition-must-be-unique");
            return false;
        }
        prepared = new SurgicalPartPreparedOutput
        {
            ItemId = itemId,
            PartInstanceId = partInstanceId,
            NodeId = nodeId,
            DisplayName = string.IsNullOrWhiteSpace(displayName)
                ? definition.DisplayName
                : displayName.Trim(),
            Kind = kind,
            Quality = Mathf.Clamp(quality, 0.1f, 1.75f),
            CommitId = canonicalCommit,
            ExpectedSequence = nextPartSequence,
            IsReplay = false
        };
        return true;
    }

    bool ISurgicalPartPreparedOutputRuntime.TryCommitCraftedOutput(
        SurgicalPartPreparedOutput prepared,
        FacilityBufferPlannedOutputPublicationReceipt published,
        out DomainFailure failure)
    {
        failure = DomainFailure.None;
        if (!TryValidatePublishedCandidate(
                prepared,
                published,
                out WorldItemStackSnapshot stack,
                out _,
                out failure))
        {
            return false;
        }
        SurgicalPartInstance existing = parts.SingleOrDefault(candidate =>
            candidate != null
            && string.Equals(
                candidate.sourceProductionCommitId,
                prepared.CommitId,
                StringComparison.Ordinal));
        if (existing != null)
        {
            return string.Equals(existing.partInstanceId, prepared.PartInstanceId,
                    StringComparison.Ordinal)
                && string.Equals(existing.worldStackId, stack.StackId,
                    StringComparison.Ordinal)
                && string.Equals(existing.itemDefinitionId, prepared.ItemId,
                    StringComparison.Ordinal)
                && string.Equals(
                    existing.physicalItemInstanceId,
                    prepared.PhysicalItemInstanceId,
                    StringComparison.Ordinal)
                || FailCraftedOutput(
                    prepared.CommitId,
                    "crafted-output-replay-conflict",
                    out failure);
        }
        if (prepared.IsReplay
            || sequence != prepared.ExpectedSequence - 1
            || parts.Any(candidate => candidate != null
                && string.Equals(
                    candidate.partInstanceId,
                    prepared.PartInstanceId,
                    StringComparison.Ordinal)))
        {
            return FailCraftedOutput(
                prepared.CommitId,
                "crafted-output-sequence-conflict",
                out failure);
        }

        parts.Add(new SurgicalPartInstance
        {
            partInstanceId = prepared.PartInstanceId,
            itemDefinitionId = prepared.ItemId,
            physicalItemInstanceId = prepared.PhysicalItemInstanceId,
            kind = prepared.Kind,
            nodeId = prepared.NodeId,
            displayName = prepared.DisplayName,
            donorId = string.Empty,
            donorName = "제작품",
            donorSpeciesId = string.Empty,
            anatomyFamily = "humanoid",
            quality = prepared.Quality,
            // Freshness is a finite countdown owned only by natural organs.
            // A zero value is the canonical non-perishable sentinel for
            // prosthetics/implants and remains valid in deterministic saves.
            freshnessSeconds = 0f,
            worldStackId = stack.StackId,
            sourceProductionCommitId = prepared.CommitId
        });
        sequence = prepared.ExpectedSequence;
        return true;
    }

    bool ISurgicalPartPreparedOutputRuntime.TryRollbackCraftedOutput(
        SurgicalPartPreparedOutput prepared,
        FacilityBufferPlannedOutputPublicationReceipt published,
        out string failureReason)
    {
        failureReason = string.Empty;
        if (prepared == null || prepared.IsReplay)
            return true;
        SurgicalPartInstance[] matches = parts.Where(candidate => candidate != null
                && string.Equals(
                    candidate.sourceProductionCommitId,
                    prepared.CommitId,
                    StringComparison.Ordinal))
            .ToArray();
        if (matches.Length == 0)
            return true;
        if (matches.Length != 1
            || sequence != prepared.ExpectedSequence
            || published.Stacks.Count != 1
            || !string.Equals(
                matches[0].worldStackId,
                published.Stacks[0].StackId,
                StringComparison.Ordinal))
        {
            failureReason = "crafted-output-runtime-rollback-conflict";
            return false;
        }
        parts.Remove(matches[0]);
        sequence = prepared.ExpectedSequence - 1;
        return true;
    }

    bool ISurgicalPartPreparedOutputRuntime.TryValidateCommittedCraftedOutput(
        string commitId,
        bool requireAcknowledged,
        out SurgicalPartPublishedOutputSnapshot joined,
        out DomainFailure failure)
    {
        joined = default;
        failure = DomainFailure.None;
        SurgicalPartInstance[] matches = parts.Where(candidate => candidate != null
                && string.Equals(
                    candidate.sourceProductionCommitId,
                    commitId,
                    StringComparison.Ordinal))
            .ToArray();
        if (matches.Length != 1)
        {
            return FailCraftedOutput(
                commitId,
                "crafted-output-owner-missing-or-duplicate",
                out failure);
        }
        SurgicalPartInstance part = matches[0];
        WorldItemStackSnapshot[] stacks = items.GetAllStacks()
            .Where(candidate => candidate != null
                && string.Equals(
                    candidate.StackId,
                    part.worldStackId,
                    StringComparison.Ordinal))
            .ToArray();
        if (stacks.Length != 1
            || !TryValidatePhysicalJoin(
                part,
                stacks[0],
                requireAcknowledged,
                out PlannedOutputPublicationMetadata metadata,
                out failure))
        {
            if (!failure.IsFailure)
            {
                FailCraftedOutput(
                    commitId,
                    "crafted-output-physical-owner-missing",
                    out failure);
            }
            return false;
        }
        joined = new SurgicalPartPublishedOutputSnapshot(
            stacks[0].StackId,
            stacks[0].ItemInstanceId,
            metadata.MassGrams,
            metadata.Acknowledged);
        return true;
    }

    private bool TryValidatePublishedCandidate(
        SurgicalPartPreparedOutput prepared,
        FacilityBufferPlannedOutputPublicationReceipt published,
        out WorldItemStackSnapshot stack,
        out PlannedOutputPublicationMetadata metadata,
        out DomainFailure failure)
    {
        stack = null;
        metadata = default;
        failure = DomainFailure.None;
        if (prepared == null
            || published.Stacks.Count != 1
            || !string.Equals(
                published.BatchCommitId,
                prepared.CommitId,
                StringComparison.Ordinal)
            || !string.Equals(
                published.Stacks[0].ItemDefinitionId.Value,
                prepared.ItemId,
                StringComparison.Ordinal)
            || published.Stacks[0].Quantity != 1
            || !string.Equals(
                published.Stacks[0].ItemInstanceId,
                prepared.PhysicalItemInstanceId,
                StringComparison.Ordinal)
            || published.Stacks[0].MassGrams <= 0L)
        {
            return FailCraftedOutput(
                prepared?.CommitId,
                "crafted-output-publication-receipt-invalid",
                out failure);
        }
        stack = items.GetAllStacks().SingleOrDefault(candidate => candidate != null
            && string.Equals(
                candidate.StackId,
                published.Stacks[0].StackId,
                StringComparison.Ordinal));
        if (stack == null
            || stack.State != WorldItemStackState.FacilityOutputBuffer
            || !string.Equals(
                stack.DestinationId,
                published.DestinationId,
                StringComparison.Ordinal)
            || !TryValidatePreparedComponent(prepared, stack, out failure)
            || !PlannedOutputPublicationComponentCodec.TryRead(
                stack.Components,
                out metadata)
            || metadata.Acknowledged
            || metadata.MassGrams != published.Stacks[0].MassGrams)
        {
            if (!failure.IsFailure)
            {
                FailCraftedOutput(
                    prepared.CommitId,
                    "crafted-output-publication-join-invalid",
                    out failure);
            }
            return false;
        }
        return true;
    }

    private static bool TryValidatePreparedComponent(
        SurgicalPartPreparedOutput prepared,
        WorldItemStackSnapshot stack,
        out DomainFailure failure)
    {
        failure = DomainFailure.None;
        if (stack.Quantity != 1
            || string.IsNullOrWhiteSpace(stack.ItemInstanceId)
            || !string.Equals(
                stack.ItemInstanceId,
                prepared.PhysicalItemInstanceId,
                StringComparison.Ordinal)
            || !string.Equals(stack.ItemId, prepared.ItemId, StringComparison.Ordinal)
            || !SurgicalPartPreparedOutputComponentCodec.TryRead(
                stack.Components,
                out string partId,
                out string nodeId,
                out SurgicalPartKind kind,
                out float quality,
                out string commitId)
            || !string.Equals(partId, prepared.PartInstanceId, StringComparison.Ordinal)
            || !string.Equals(nodeId, prepared.NodeId, StringComparison.Ordinal)
            || kind != prepared.Kind
            || quality != prepared.Quality
            || !string.Equals(commitId, prepared.CommitId, StringComparison.Ordinal))
        {
            return FailCraftedOutput(
                prepared.CommitId,
                "crafted-output-component-join-invalid",
                out failure);
        }
        return true;
    }

    private static bool TryValidatePhysicalJoin(
        SurgicalPartInstance part,
        WorldItemStackSnapshot stack,
        bool requireAcknowledged,
        out PlannedOutputPublicationMetadata metadata,
        out DomainFailure failure)
    {
        metadata = default;
        failure = DomainFailure.None;
        if (stack.Quantity != 1
            || string.IsNullOrWhiteSpace(stack.ItemInstanceId)
            || !string.Equals(
                stack.ItemInstanceId,
                part.physicalItemInstanceId,
                StringComparison.Ordinal)
            || !string.Equals(
                stack.ItemId,
                part.itemDefinitionId,
                StringComparison.Ordinal)
            || !SurgicalPartPreparedOutputComponentCodec.TryRead(
                stack.Components,
                out string partId,
                out string nodeId,
                out SurgicalPartKind kind,
                out float quality,
                out string componentCommit)
            || !string.Equals(partId, part.partInstanceId, StringComparison.Ordinal)
            || !string.Equals(nodeId, part.nodeId, StringComparison.Ordinal)
            || kind != part.kind
            || quality != part.quality
            || !string.Equals(
                componentCommit,
                part.sourceProductionCommitId,
                StringComparison.Ordinal)
            || !PlannedOutputPublicationComponentCodec.TryRead(
                stack.Components,
                out metadata)
            || !string.Equals(
                metadata.BatchCommitId,
                part.sourceProductionCommitId,
                StringComparison.Ordinal)
            || metadata.Quantity != 1
            || metadata.MassGrams <= 0L
            || requireAcknowledged && !metadata.Acknowledged)
        {
            return FailCraftedOutput(
                part.sourceProductionCommitId,
                "crafted-output-physical-join-invalid",
                out failure);
        }
        return true;
    }

    private static bool FailCraftedOutput(
        string commitId,
        string detail,
        out DomainFailure failure)
    {
        failure = new DomainFailure(
            FailureCode.ProductionOutputUnavailable,
            commitId ?? string.Empty,
            detail);
        return false;
    }

    public bool TryReserveForOrder(string partInstanceId, string orderId,
        out DomainFailure failure) =>
        installations.TryReserveForOrder(partInstanceId, orderId, out failure);

    public void ReleaseReservation(string partInstanceId, string orderId) =>
        installations.ReleaseReservation(partInstanceId, orderId);

    public bool TryValidateReservationForOrder(string partInstanceId, string orderId,
        out DomainFailure failure) =>
        installations.TryValidateReservationForOrder(partInstanceId, orderId, out failure);

    public bool TryConsumeForInstallation(string partInstanceId, string orderId, string subjectId,
        out SurgicalPartInstance part, out DomainFailure failure)
    {
        bool consumed = installations.TryConsumeForInstallation(partInstanceId, orderId, subjectId,
            out SurgicalPartInstance owned, out failure);
        part = owned == null ? null : SurgeryStateCloner.ClonePart(owned);
        return consumed;
    }

    bool ISurgicalPartReplacementRuntime.TryReserveReplacementOutput(
        SurgeryOrder order, AnatomyNodeHealthState currentNode, Vector2Int outputPosition,
        IFacilityBufferMassAdmissionService admission, out DomainFailure failure) =>
        ((ISurgicalPartReplacementRuntime)replacements).TryReserveReplacementOutput(
            order, currentNode, outputPosition, admission, out failure);

    bool ISurgicalPartReplacementRuntime.TryCommitReplacement(
        SurgeryOrder order, CharacterActor character, SurgicalPartKind incomingKind,
        float efficiency, IAnatomyHealthRuntime anatomy,
        IFacilityBufferMassAdmissionService admission,
        IFacilityBufferPlannedOutputPublicationService publication, out DomainFailure failure) =>
        ((ISurgicalPartReplacementRuntime)replacements).TryCommitReplacement(
            order, character, incomingKind, efficiency, anatomy, admission, publication, out failure);

    bool ISurgicalPartReplacementRuntime.TryAbortReplacement(
        SurgeryOrder order, IFacilityBufferMassAdmissionService admission, out string failureReason) =>
        ((ISurgicalPartReplacementRuntime)replacements).TryAbortReplacement(
            order, admission, out failureReason);

    internal static bool ReplacementOutcomeFingerprintMatches(
        SurgeryOrder order, SurgicalPartInstance part) =>
        SurgicalPartReplacementTransaction.ReplacementOutcomeFingerprintMatches(order, part);

    internal static string CreateReplacementOutcomeFingerprintForValidation(
        SurgeryOrder order, SurgicalPartInstance part) =>
        SurgicalPartReplacementTransaction.CreateReplacementOutcomeFingerprintForValidation(order, part);

    public void TickFreshness(float deltaTime)
    {
        foreach (SurgicalPartInstance pendingDiscard in parts
                     .Where(HasPendingManualDiscard)
                     .ToArray())
        {
            TryFinalizeManualDiscard(pendingDiscard, out _);
        }

        if (deltaTime <= 0f || parts.Count == 0)
        {
            return;
        }

        IReadOnlyList<WorldItemStackSnapshot> stacks = items.GetAllStacks();
        Dictionary<string, WorldItemStackSnapshot> byStack = stacks
            .Where(stack => stack != null)
            .ToDictionary(stack => stack.StackId, StringComparer.Ordinal);
        HashSet<string> bodyCommittedIncomingPartIds = stateStore.State.Orders
            .Where(order => order?.IsActive == true
                && order.replacementPhase ==
                    SurgicalPartReplacementPhase.BodyCommitted)
            .Select(order => order.replacementIncomingPartId)
            .Where(value => !string.IsNullOrEmpty(value))
            .ToHashSet(StringComparer.Ordinal);
        List<SurgicalPartInstance> expired = null;
        foreach (SurgicalPartInstance part in parts)
        {
            if (part == null
                || part.installed
                || HasPendingManualDiscard(part)
                // The body CAS already owns this exact part. It is no longer
                // eligible loose inventory even if its Transfer outbox retries.
                || bodyCommittedIncomingPartIds.Contains(part.partInstanceId)
                || part.kind != SurgicalPartKind.NaturalOrgan
                || float.IsPositiveInfinity(part.freshnessSeconds))
            {
                continue;
            }

            string storageId = string.Empty;
            bool inWorkingStorage = byStack.TryGetValue(
                    part.worldStackId,
                    out WorldItemStackSnapshot stack)
                && IsInWorkingOrganStorage(stack, out storageId);
            bool preserved = part.freshnessSeconds > 0f
                && inWorkingStorage
                && TryEnsurePreservationCanister(part, stack, storageId);
            part.storedFacilityId = preserved ? storageId : string.Empty;
            part.freshnessSeconds = Mathf.Max(
                0f,
                part.freshnessSeconds - deltaTime
                * (preserved ? StoredFreshnessRate : 1f));
            if (!(part.freshnessSeconds > 0f))
            {
                expired ??= new List<SurgicalPartInstance>();
                expired.Add(part);
            }
        }

        foreach (SurgicalPartInstance part in expired
                     ?? Enumerable.Empty<SurgicalPartInstance>())
        {
            WorldItemStackSnapshot stack = items.GetAllStacks().FirstOrDefault(
                candidate => candidate != null
                    && candidate.StackId == part.worldStackId);
            if (stack == null
                || HasProtectedExpiryCustody(part)
                || !MatchesExactDetachedPhysicalOwner(part, stack))
            {
                continue;
            }
            if (stack.State is WorldItemStackState.Stored
                    or WorldItemStackState.FacilityBuffer)
            {
                ExactOwnedItemReleaseRequest release = new(
                    stack.StackId,
                    part.itemDefinitionId,
                    part.physicalItemInstanceId,
                    1,
                    stack.State,
                    stack.DestinationId,
                    stack.State == WorldItemStackState.FacilityBuffer
                        ? SurgicalPartStorageInputOwnerAuthority.OwnerDomain
                        : string.Empty,
                    "surgical-organ-expiry-release:" + part.partInstanceId,
                    FreshnessExpiryReleaseReason);
                if (!itemTransfers.TryReleaseExactOwnedWholeStack(
                        release,
                        out _,
                        out _))
                {
                    continue;
                }
                stack = items.GetAllStacks().SingleOrDefault(candidate =>
                    candidate != null
                    && string.Equals(
                        candidate.StackId,
                        part.worldStackId,
                        StringComparison.Ordinal));
                if (stack == null)
                {
                    throw new InvalidOperationException(
                        $"Expired surgical organ '{part.partInstanceId}' was released without its exact physical stack.");
                }
                if (!MatchesExactDetachedPhysicalOwner(part, stack))
                {
                    throw new InvalidOperationException(
                        $"Expired surgical organ '{part.partInstanceId}' changed physical identity during release.");
                }
            }
            if (!CanTransformExpiredOrganAtCurrentPlacement(stack))
            {
                continue;
            }
            if (physicalTransforms.TryTransformWholeStack(
                    stack.StackId,
                    new[]
                    {
                        new PhysicalItemTransformOutput(
                            SurgeryItemDefinitions.ContaminatedTissueId,
                            1,
                            stack.Position)
                    },
                    "surgical-organ-expiry:" + part.partInstanceId,
                    FreshnessExpiryTransformReason,
                    out _,
                    out _,
                    out _))
            {
                parts.Remove(part);
            }
        }
    }

    private static bool CanTransformExpiredOrganAtCurrentPlacement(
        WorldItemStackSnapshot stack) => stack.State == WorldItemStackState.Loose
        && string.IsNullOrEmpty(stack.DestinationId);

    private static bool HasProtectedExpiryCustody(
        SurgicalPartInstance part) =>
        !string.IsNullOrEmpty(part.reservedOrderId)
        || !string.IsNullOrEmpty(part.installationOperationId)
        || !string.IsNullOrEmpty(part.preservationOperationId);

    private static bool MatchesExactDetachedPhysicalOwner(
        SurgicalPartInstance part,
        WorldItemStackSnapshot stack) => part != null
        && stack != null
        && stack.Quantity == 1
        && string.Equals(
            stack.ItemId,
            part.itemDefinitionId,
            StringComparison.Ordinal)
        && string.Equals(
            stack.ItemInstanceId,
            part.physicalItemInstanceId,
            StringComparison.Ordinal);

    [GameplayEntryPoint(
        "ItemPileInfoPanel discard action; OrganPreservationRestoreJoinFixture")]
    public SurgicalPartDiscardResult TryDiscardOwnedStack(string stackId)
    {
        string sourceStackId = stackId ?? string.Empty;
        SurgicalPartInstance[] owners = parts.Where(part =>
                part != null
                && string.Equals(
                    part.worldStackId,
                    sourceStackId,
                    StringComparison.Ordinal))
            .ToArray();
        if (owners.Length == 0)
        {
            return new SurgicalPartDiscardResult(
                SurgicalPartDiscardStatus.NotOwned,
                string.Empty);
        }
        if (owners.Length != 1)
        {
            return RejectDiscard("medical-discard-duplicate-owner");
        }

        SurgicalPartInstance part = owners[0];
        if (HasPendingManualDiscard(part))
        {
            return TryFinalizeManualDiscard(part, out string retryFailure)
                ? new SurgicalPartDiscardResult(
                    SurgicalPartDiscardStatus.Completed,
                    string.Empty)
                : new SurgicalPartDiscardResult(
                    SurgicalPartDiscardStatus.Pending,
                    retryFailure);
        }
        bool bodyCommittedIncoming = stateStore.State.Orders.Any(order =>
            order?.IsActive == true
            && order.replacementPhase ==
                SurgicalPartReplacementPhase.BodyCommitted
            && string.Equals(
                order.replacementIncomingPartId,
                part.partInstanceId,
                StringComparison.Ordinal));
        if (part.installed
            || bodyCommittedIncoming
            || !string.IsNullOrEmpty(part.reservedOrderId)
            || !string.IsNullOrEmpty(part.installationOperationId)
            || !string.IsNullOrEmpty(part.preservationOperationId))
        {
            return RejectDiscard("medical-discard-owned-custody-protected");
        }

        WorldItemStackSnapshot[] matchingStacks = items.GetAllStacks()
            .Where(candidate => candidate != null
                && string.Equals(
                    candidate.StackId,
                    part.worldStackId,
                    StringComparison.Ordinal))
            .ToArray();
        if (matchingStacks.Length != 1)
        {
            return RejectDiscard("medical-discard-exact-stack-missing");
        }
        WorldItemStackSnapshot stack = matchingStacks[0];
        if (stack.Quantity != 1
            || stack.ReservedQuantity != 0
            || !string.IsNullOrEmpty(stack.ReservedByPersistentId)
            || !string.Equals(
                stack.ItemId,
                part.itemDefinitionId,
                StringComparison.Ordinal)
            || !string.Equals(
                stack.ItemInstanceId,
                part.physicalItemInstanceId,
                StringComparison.Ordinal)
            || stack.State is not (WorldItemStackState.Loose
                or WorldItemStackState.Stored
                or WorldItemStackState.FacilityBuffer)
            || !string.IsNullOrEmpty(stack.SourceStorageDestinationId)
            || FacilityOutputExactRouteCustodyCodec.HasAnyCustody(
                stack.Components)
            || stack.State == WorldItemStackState.Loose
                && !string.IsNullOrEmpty(stack.DestinationId)
            || stack.State != WorldItemStackState.Loose
                && string.IsNullOrEmpty(stack.DestinationId))
        {
            return RejectDiscard("medical-discard-physical-owner-mismatch");
        }
        if (stack.State == WorldItemStackState.FacilityBuffer
            && !HasExactSurgicalStorageClaim(
                stack.DestinationId,
                stack.Position))
        {
            return RejectDiscard("medical-discard-facility-owner-mismatch");
        }

        string operationId = SurgicalPartDiscardIdentity.FormatOperationId(
            part.partInstanceId);
        if (!batchDispositions.TryCommitPending(
                new[]
                {
                    new PhysicalItemTransformInput(stack.StackId, 1)
                },
                PhysicalItemDispositionKind.Sink,
                operationId,
                SurgicalPartDiscardIdentity.ReasonCode,
                out PhysicalItemBatchDispositionReceipt receipt,
                out string commitFailure))
        {
            return RejectDiscard(commitFailure);
        }
        if (!receipt.IsCommitted
            || receipt.Kind != PhysicalItemDispositionKind.Sink
            || !string.Equals(
                receipt.OperationId,
                operationId,
                StringComparison.Ordinal)
            || !string.Equals(
                receipt.ReasonCode,
                SurgicalPartDiscardIdentity.ReasonCode,
                StringComparison.Ordinal)
            || receipt.SourceStackIds.Count != 1
            || !string.Equals(
                receipt.SourceStackIds[0],
                stack.StackId,
                StringComparison.Ordinal)
            || receipt.Quantity != 1)
        {
            throw new InvalidOperationException(
                "Manual surgical-part discard committed a non-canonical physical Sink receipt.");
        }
        part.discardOperationId = receipt.OperationId;
        part.discardCommitId = receipt.CommitId;
        part.discardSourceStackId = receipt.SourceStackIds[0];
        part.discardInputMassGrams = receipt.InputMassGrams;
        if (TryFinalizeManualDiscard(part, out string finalizeFailure))
        {
            return new SurgicalPartDiscardResult(
                SurgicalPartDiscardStatus.Completed,
                string.Empty);
        }
        return new SurgicalPartDiscardResult(
            SurgicalPartDiscardStatus.Pending,
            finalizeFailure);
    }

    private static SurgicalPartDiscardResult RejectDiscard(string reason) =>
        new(
            SurgicalPartDiscardStatus.Rejected,
            string.IsNullOrWhiteSpace(reason)
                ? "medical-discard-rejected"
                : reason);

    private static bool HasPendingManualDiscard(
        SurgicalPartInstance part) => part != null
        && !string.IsNullOrEmpty(part.discardOperationId);

    private bool HasExactSurgicalStorageClaim(
        string destinationId,
        Vector2Int position) => destinationClaims.CaptureAuthorityClaims()
        .Count(claim => claim != null
            && string.Equals(
                claim.DestinationId,
                destinationId,
                StringComparison.Ordinal)
            && claim.DropPosition == position
            && string.Equals(
                claim.OwnerDomain,
                SurgicalPartStorageInputOwnerAuthority.OwnerDomain,
                StringComparison.Ordinal)
            && string.Equals(
                claim.OwnerFacilityId,
                destinationId,
                StringComparison.Ordinal)
            && claim.AnchorKind ==
                FacilityBufferDestinationAnchorKind.LiveFacility) == 1;

    private bool TryFinalizeManualDiscard(
        SurgicalPartInstance part,
        out string failureReason)
    {
        failureReason = string.Empty;
        if (!HasPendingManualDiscard(part)
            || !string.Equals(
                part.discardOperationId,
                SurgicalPartDiscardIdentity.FormatOperationId(
                    part.partInstanceId),
                StringComparison.Ordinal))
        {
            failureReason = "medical-discard-pending-invalid";
            return false;
        }
        bool pending = batchDispositions.TryGetPending(
            part.discardOperationId,
            out PhysicalItemBatchDispositionReceipt receipt);
        if (pending
            && (receipt.Kind != PhysicalItemDispositionKind.Sink
                || !string.Equals(
                    receipt.CommitId,
                    part.discardCommitId,
                    StringComparison.Ordinal)
                || !string.Equals(
                    receipt.ReasonCode,
                    SurgicalPartDiscardIdentity.ReasonCode,
                    StringComparison.Ordinal)
                || receipt.SourceStackIds.Count != 1
                || !string.Equals(
                    receipt.SourceStackIds[0],
                    part.discardSourceStackId,
                    StringComparison.Ordinal)
                || receipt.Quantity != 1
                || receipt.InputMassGrams != part.discardInputMassGrams))
        {
            failureReason = "medical-discard-pending-receipt-mismatch";
            return false;
        }
        if (!part.discardOutcomePublished)
        {
            if (!pending)
            {
                failureReason = "medical-discard-pending-receipt-missing";
                return false;
            }
            part.discardOutcomePublished = true;
        }
        if (pending
            && !batchDispositions.Acknowledge(
                part.discardCommitId,
                out failureReason))
        {
            return false;
        }
        parts.Remove(part);
        return true;
    }

    private bool TryEnsurePreservationCanister(
        SurgicalPartInstance part,
        WorldItemStackSnapshot organStack,
        string storageId)
    {
        if (!storageInputOwners.TryEnsure(
                storageId,
                organStack.Position,
                out string ownerFailure))
        {
            throw new InvalidOperationException(
                "Surgical organ-storage authority is unavailable: "
                + ownerFailure);
        }
        if (SurgicalOrganPreservationOutbox.HasPending(part))
        {
            return SurgicalOrganPreservationOutbox.TryFinalize(part,batchDispositions,out _);
        }
        if (part.preservationCanisterApplied)
        {
            return true;
        }

        WorldItemStackSnapshot canister = items.GetAllStacks().FirstOrDefault(candidate => candidate != null
            && candidate.ItemId == OrganPreservationCanisterItemId
            && candidate.DestinationId == storageId
            && candidate.State == WorldItemStackState.FacilityBuffer
            && candidate.AvailableQuantity > 0);
        if (canister != null)
        {
            string operationId = SurgicalOrganPreservationOutbox.FormatOperationId(part.partInstanceId);
            if (!batchDispositions.TryCommitPending(new[] { new PhysicalItemTransformInput(canister.StackId, 1) },
                    PhysicalItemDispositionKind.Sink, operationId, SurgicalOrganPreservationOutbox.ReasonCode,
                    out PhysicalItemBatchDispositionReceipt receipt, out _)) return false;
            SurgicalOrganPreservationOutbox.Record(part,receipt);
            return SurgicalOrganPreservationOutbox.TryFinalize(part,batchDispositions,out _);
        }

        bool deliveryPending = items.GetAllStacks().Any(candidate =>
            candidate != null
            && string.Equals(
                candidate.ItemId,
                OrganPreservationCanisterItemId,
                StringComparison.Ordinal)
            && string.Equals(
                candidate.DestinationId,
                storageId,
                StringComparison.Ordinal));
        if (!deliveryPending)
        {
            items.TryRequestItemDelivery(
                OrganPreservationCanisterItemId,
                1,
                organStack.Position,
                storageId,
                out _,
                out _);
        }

        return false;
    }


    public IReadOnlyList<SurgicalPartInstance> CaptureParts()
    {
        return parts.Select(SurgeryStateCloner.ClonePart).ToArray();
    }

    public IReadOnlyList<SurgicalOrganStorageState> CaptureStorageStates()
    {
        EnsureStorageInputOwners();
        return storageStates.Values
            .Where(state => state != null
                && !string.IsNullOrWhiteSpace(state.facilityId))
            .OrderBy(state => state.facilityId, StringComparer.Ordinal)
            .Select(state => state.Clone())
            .ToArray();
    }

    public bool TryGetOrganStorageStatus(
        BuildableObject storage,
        out SurgicalOrganStorageSnapshot snapshot)
    {
        snapshot = default;
        BuildingOrganStorageAbility ability =
            storage?.BuildingData?.GetAbility<BuildingOrganStorageAbility>();
        if (storage == null || ability == null)
        {
            return false;
        }

        EnsureStorageInputOwners();

        string facilityId = facilities.GetFacilityId(storage);
        SurgicalOrganStorageState state = GetOrCreateStorageState(facilityId);
        int stored = CountPartsRoutedTo(facilityId);
        bool powered = ability.fuelPerDay <= 0
            || state.fuelSecondsRemaining > 0.001f;
        snapshot = new SurgicalOrganStorageSnapshot(
            facilityId,
            stored,
            ability.capacity,
            powered,
            state.fuelSecondsRemaining);
        return true;
    }

    private void RequestOrganStorage(
        SurgicalPartInstance part,
        Vector2Int origin)
    {
        Dictionary<string, int> routedCounts = items.GetAllStacks()
            .Where(stack => stack != null
                && stack.ItemId.StartsWith(
                    SurgeryItemDefinitions.OrganPrefix,
                    StringComparison.Ordinal)
                && !string.IsNullOrWhiteSpace(stack.DestinationId))
            .GroupBy(stack => stack.DestinationId, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.Sum(stack => stack.Quantity),
                StringComparer.Ordinal);
        BuildableObject storage = buildings.Buildings
            .Where(building => building != null
                && !building.isDestroy
                && !building.IsDamaged
                && building.BuildingData?
                    .GetAbility<BuildingOrganStorageAbility>() != null)
            .Where(building =>
            {
                string id = facilities.GetFacilityId(building);
                routedCounts.TryGetValue(id, out int count);
                int capacity = building.BuildingData
                    .GetAbility<BuildingOrganStorageAbility>()
                    .capacity;
                return count < Mathf.Max(1, capacity);
            })
            .OrderBy(building =>
                Mathf.Abs(building.centerPos.x - origin.x)
                + Mathf.Abs(building.centerPos.y - origin.y))
            .FirstOrDefault();
        if (storage == null)
        {
            return;
        }

        string destinationId = facilities.GetFacilityId(storage);
        if (!storageInputOwners.TryEnsure(
                destinationId,
                storage.centerPos,
                out string ownerFailure))
        {
            throw new InvalidOperationException(
                "Surgical organ-storage authority is unavailable: "
                + ownerFailure);
        }
        if (items.TryRequestStackDelivery(
                part.worldStackId,
                1,
                storage.centerPos,
                destinationId,
                out _,
                out _))
        {
            part.storedFacilityId = string.Empty;
        }
    }

    private bool IsInWorkingOrganStorage(
        WorldItemStackSnapshot stack,
        out string storageId)
    {
        storageId = string.Empty;
        if (stack == null
            || stack.State != WorldItemStackState.FacilityBuffer
            || string.IsNullOrWhiteSpace(stack.DestinationId)
            || !environment.IsOrganPreservationSafe(stack.Position))
        {
            return false;
        }

        BuildableObject storage = buildings.Buildings.FirstOrDefault(building =>
            building != null
            && !building.isDestroy
            && !building.IsDamaged
            && building.BuildingData?.GetAbility<BuildingOrganStorageAbility>() != null
            && string.Equals(
                facilities.GetFacilityId(building),
                stack.DestinationId,
                StringComparison.Ordinal));
        if (storage == null)
        {
            return false;
        }

        BuildingOrganStorageAbility storageAbility =
            storage.BuildingData.GetAbility<BuildingOrganStorageAbility>();
        SurgicalOrganStorageState state = GetOrCreateStorageState(
            stack.DestinationId);
        if (storageAbility.fuelPerDay > 0
            && state.fuelSecondsRemaining <= 0.001f)
        {
            return false;
        }

        storageId = stack.DestinationId;
        return true;
    }

    private void TickOrganStorageFuel(float deltaTime)
    {
        foreach (SurgicalOrganStorageState state in storageStates.Values)
        {
            if (state != null && state.fuelSecondsRemaining > 0f)
            {
                state.fuelSecondsRemaining = Mathf.Max(
                    0f,
                    state.fuelSecondsRemaining - deltaTime);
            }
        }

        if (clock.Time < nextFuelRefreshAt)
        {
            return;
        }

        nextFuelRefreshAt = clock.Time + FuelRefreshInterval;
        foreach (BuildableObject storage in buildings.Buildings.Where(building =>
                     building != null
                     && !building.isDestroy
                     && !building.IsDamaged
                     && building.BuildingData?
                         .GetAbility<BuildingOrganStorageAbility>() != null))
        {
            BuildingOrganStorageAbility ability =
                storage.BuildingData.GetAbility<BuildingOrganStorageAbility>();
            string facilityId = facilities.GetFacilityId(storage);
            SurgicalOrganStorageState state = GetOrCreateStorageState(facilityId);
            if (ability.fuelPerDay <= 0)
            {
                state.fuelSecondsRemaining = SecondsPerDay;
                state.fuelDeliveryRequested = false;
                continue;
            }

            string destinationId = GetFuelDestinationId(facilityId);
            if (!storageInputOwners.TryEnsure(
                    destinationId,
                    storage.centerPos,
                    out string ownerFailure))
            {
                throw new InvalidOperationException(
                    "Surgical fuel destination authority is unavailable: "
                    + ownerFailure);
            }
            if (!storageInputOwners.TryGetFuelItemId(out string fuelItemId))
            {
                throw new InvalidOperationException(
                    "Surgical fuel exact item authority is unavailable.");
            }
            WorldItemStackSnapshot fuelStack = items.GetAllStacks()
                .Where(stack => stack != null
                    && string.Equals(stack.DestinationId, destinationId,
                        StringComparison.Ordinal)
                    && string.Equals(stack.ItemId, fuelItemId,
                        StringComparison.Ordinal)
                    && stack.State == WorldItemStackState.FacilityBuffer
                    && stack.AvailableQuantity > 0)
                .OrderBy(stack => stack.StackId, StringComparer.Ordinal)
                .FirstOrDefault();
            if (state.fuelSecondsRemaining <= SecondsPerDay * 0.25f
                && fuelStack != null
                && batchDispositions.TryCommit(
                    new[] { new PhysicalItemTransformInput(fuelStack.StackId, 1) },
                    PhysicalItemDispositionKind.Sink,
                    "surgical-organ-storage-fuel:" + facilityId + ":"
                        + fuelStack.StackId,
                    "surgical-organ-storage-fuel",
                    out _,
                    out _))
            {
                state.fuelSecondsRemaining +=
                    SecondsPerDay / Mathf.Max(1, ability.fuelPerDay);
                state.fuelDeliveryRequested = false;
            }

            int routedFuel = items.GetAllStacks()
                .Where(stack => stack != null
                    && string.Equals(
                        stack.DestinationId,
                        destinationId,
                        StringComparison.Ordinal)
                    && string.Equals(
                        stack.ItemId,
                        fuelItemId,
                        StringComparison.Ordinal))
                .Sum(stack => stack.Quantity);
            state.fuelDeliveryRequested = routedFuel > 0;
            if (state.fuelSecondsRemaining <= SecondsPerDay * 0.5f
                && routedFuel == 0)
            {
                state.fuelDeliveryRequested =
                    items.TryRequestItemDelivery(
                        fuelItemId,
                        1,
                        storage.centerPos,
                        destinationId,
                        out int requested,
                        out _)
                    && requested > 0;
            }
        }
    }

    private SurgicalOrganStorageState GetOrCreateStorageState(
        string facilityId)
    {
        string normalized = facilityId?.Trim() ?? string.Empty;
        if (!storageStates.TryGetValue(
                normalized,
                out SurgicalOrganStorageState state))
        {
            state = new SurgicalOrganStorageState
            {
                facilityId = normalized
            };
            storageStates.Add(normalized, state);
        }

        return state;
    }

    private void EnsureStorageInputOwners()
    {
        if (!storageInputOwners.TryReconcile(out string failureReason))
        {
            throw new InvalidOperationException(
                "Surgical storage input-owner reconciliation failed: "
                + failureReason);
        }
    }

    private int CountPartsRoutedTo(string destinationId)
    {
        return items.GetAllStacks()
            .Where(stack => stack != null
                && string.Equals(
                    stack.DestinationId,
                    destinationId,
                    StringComparison.Ordinal)
                && stack.ItemId.StartsWith(
                    SurgeryItemDefinitions.OrganPrefix,
                    StringComparison.Ordinal))
            .Sum(stack => stack.Quantity);
    }

    private static string GetFuelDestinationId(string facilityId)
    {
        return FuelDestinationPrefix + (facilityId?.Trim() ?? string.Empty);
    }

    private string ResolveAnatomyFamily(SurgicalSubjectRef donor)
    {
        if (!string.IsNullOrWhiteSpace(donor.anatomyProfileId)
            && anatomyProfiles.TryGet(
                donor.anatomyProfileId,
                out AnatomyProfileDefinition profile))
        {
            return profile.AnatomyFamily;
        }

        return anatomyProfiles.GetForSpecies(donor.speciesId).AnatomyFamily;
    }


}

internal static class SurgicalPartRecoveryComponentCodec
{
    internal const string ComponentTypeId =
        "medical:surgical-part-recovery";
    private const string PartIdKey = "part-instance-id";
    private const string NodeIdKey = "node-id";
    private const string KindKey = "kind";
    private const string QualityKey = "quality";
    private const string CurrentHealthKey = "current-health";
    private const string MaxHealthKey = "max-health";
    private const string OrderIdKey = "order-id";
    private const string OperationIdKey = "operation-id";

    internal static ItemInstanceComponentSaveData Create(
        SurgicalPartInstance part,
        string orderId,
        string operationId,
        float currentHealth,
        float maxHealth) => new()
    {
        componentTypeId = ComponentTypeId,
        schemaVersion = 1,
        affectsStacking = true,
        values = new List<ItemStateValueSaveData>
        {
            String(PartIdKey, part.partInstanceId),
            String(NodeIdKey, part.nodeId),
            Integer(KindKey, (int)part.kind),
            Decimal(QualityKey, part.quality),
            Decimal(CurrentHealthKey, currentHealth),
            Decimal(MaxHealthKey, maxHealth),
            String(OrderIdKey, orderId),
            String(OperationIdKey, operationId)
        }
    };

    internal static bool TryRead(
        IEnumerable<ItemInstanceComponentSaveData> components,
        out string partId,
        out string nodeId,
        out SurgicalPartKind kind,
        out float quality,
        out float currentHealth,
        out float maxHealth,
        out string orderId,
        out string operationId)
    {
        partId = string.Empty;
        nodeId = string.Empty;
        kind = default;
        quality = 0f;
        currentHealth = 0f;
        maxHealth = 0f;
        orderId = string.Empty;
        operationId = string.Empty;
        ItemInstanceComponentSaveData[] matches = (components
                ?? Array.Empty<ItemInstanceComponentSaveData>())
            .Where(component => component != null
                && string.Equals(
                    component.componentTypeId,
                    ComponentTypeId,
                    StringComparison.Ordinal))
            .ToArray();
        if (matches.Length != 1
            || matches[0].schemaVersion != 1
            || !matches[0].affectsStacking
            || (matches[0].values?.Count ?? 0) != 8
            || !TryString(matches[0].values, PartIdKey, out partId)
            || !TryString(matches[0].values, NodeIdKey, out nodeId)
            || !TryInteger(matches[0].values, KindKey, out long kindValue)
            || kindValue < int.MinValue
            || kindValue > int.MaxValue
            || !Enum.IsDefined(typeof(SurgicalPartKind), (int)kindValue)
            || !TryDecimal(matches[0].values, QualityKey, out double qualityValue)
            || !TryDecimal(
                matches[0].values,
                CurrentHealthKey,
                out double currentValue)
            || !TryDecimal(matches[0].values, MaxHealthKey, out double maxValue)
            || currentValue < 0d
            || maxValue <= 0d
            || currentValue > maxValue
            || !TryString(matches[0].values, OrderIdKey, out orderId)
            || !TryString(
                matches[0].values,
                OperationIdKey,
                out operationId))
        {
            return false;
        }
        kind = (SurgicalPartKind)kindValue;
        quality = (float)qualityValue;
        currentHealth = (float)currentValue;
        maxHealth = (float)maxValue;
        return quality is >= 0.1f and <= 1.75f;
    }

    private static ItemStateValueSaveData String(string key, string value) =>
        new()
        {
            key = key,
            kind = ItemStateValueKind.String,
            stringValue = value ?? string.Empty
        };

    private static ItemStateValueSaveData Integer(string key, long value) =>
        new()
        {
            key = key,
            kind = ItemStateValueKind.Integer,
            integerValue = value
        };

    private static ItemStateValueSaveData Decimal(string key, double value) =>
        new()
        {
            key = key,
            kind = ItemStateValueKind.Decimal,
            decimalValue = value
        };

    private static bool TryString(
        IEnumerable<ItemStateValueSaveData> values,
        string key,
        out string value)
    {
        ItemStateValueSaveData[] matches = (values
                ?? Array.Empty<ItemStateValueSaveData>())
            .Where(entry => entry != null
                && entry.kind == ItemStateValueKind.String
                && string.Equals(entry.key, key, StringComparison.Ordinal))
            .ToArray();
        value = matches.Length == 1
            ? matches[0].stringValue ?? string.Empty
            : string.Empty;
        return matches.Length == 1
            && !string.IsNullOrWhiteSpace(value)
            && string.Equals(value, value.Trim(), StringComparison.Ordinal);
    }

    private static bool TryInteger(
        IEnumerable<ItemStateValueSaveData> values,
        string key,
        out long value)
    {
        ItemStateValueSaveData[] matches = (values
                ?? Array.Empty<ItemStateValueSaveData>())
            .Where(entry => entry != null
                && entry.kind == ItemStateValueKind.Integer
                && string.Equals(entry.key, key, StringComparison.Ordinal))
            .ToArray();
        value = matches.Length == 1 ? matches[0].integerValue : 0L;
        return matches.Length == 1;
    }

    private static bool TryDecimal(
        IEnumerable<ItemStateValueSaveData> values,
        string key,
        out double value)
    {
        ItemStateValueSaveData[] matches = (values
                ?? Array.Empty<ItemStateValueSaveData>())
            .Where(entry => entry != null
                && entry.kind == ItemStateValueKind.Decimal
                && string.Equals(entry.key, key, StringComparison.Ordinal))
            .ToArray();
        value = matches.Length == 1 ? matches[0].decimalValue : 0d;
        return matches.Length == 1
            && !double.IsNaN(value)
            && !double.IsInfinity(value);
    }
}

public static class SurgicalPartInstallationOutbox
{
    public const string TransferReason =
        "surgical-part-transferred-to-subject";

    public static bool TryFinalizePending(
        SurgicalPartInstance part,
        IPhysicalItemBatchDispositionService batchDispositions,
        out string failureReason)
    {
        failureReason = string.Empty;
        if (part == null
            || batchDispositions == null
            || string.IsNullOrEmpty(part.installationOperationId)
            || string.IsNullOrEmpty(part.installationCommitId)
            || string.IsNullOrEmpty(part.installationSourceStackId)
            || string.IsNullOrEmpty(part.installationSubjectId))
        {
            failureReason = "surgical-part-installation-outbox-invalid";
            return false;
        }

        bool hasPending = batchDispositions.TryGetPending(
            part.installationOperationId,
            out PhysicalItemBatchDispositionReceipt receipt);
        if (hasPending
            && (receipt.Kind != PhysicalItemDispositionKind.Transfer
                || !string.Equals(
                    receipt.OperationId,
                    part.installationOperationId,
                    StringComparison.Ordinal)
                || !string.Equals(
                    receipt.ReasonCode,
                    TransferReason,
                    StringComparison.Ordinal)
                || !string.Equals(
                    receipt.CommitId,
                    part.installationCommitId,
                    StringComparison.Ordinal)
                || receipt.Quantity != 1
                || receipt.SourceStackIds.Count != 1
                || !string.Equals(
                    receipt.SourceStackIds[0],
                    part.installationSourceStackId,
                    StringComparison.Ordinal)))
        {
            failureReason = "surgical-part-installation-outbox-mismatch";
            return false;
        }
        if (!hasPending && !part.installed)
        {
            failureReason = "surgical-part-installation-outbox-missing";
            return false;
        }

        if (!part.installed)
        {
            part.installed = true;
            part.installedSubjectId = part.installationSubjectId;
            part.worldStackId = string.Empty;
            part.storedFacilityId = string.Empty;
            part.reservedOrderId = string.Empty;
        }
        if (hasPending
            && !batchDispositions.Acknowledge(
                receipt.CommitId,
                out failureReason))
        {
            return false;
        }
        return true;
    }
}
