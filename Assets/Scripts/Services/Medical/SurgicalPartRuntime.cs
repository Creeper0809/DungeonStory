using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
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
    private readonly IGameClock clock;
    private readonly SurgeryAggregateStateStore stateStore;
    private readonly IPhysicalItemBatchDispositionService batchDispositions;
    private readonly IPhysicalItemTransformService physicalTransforms;
    private readonly IPhysicalItemMassQuery physicalMass;
    private readonly IFacilityBufferDestinationClaimAuthorityQuery
        destinationClaims;
    private readonly ISurgicalPartStorageInputOwnerAuthority storageInputOwners;
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
        IFacilityBufferDestinationReleaseService destinationReleases)
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
    }

    public IReadOnlyList<SurgicalPartInstance> Parts => parts;

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
        part = parts.FirstOrDefault(candidate =>
            candidate != null
            && string.Equals(
                candidate.partInstanceId,
                partInstanceId?.Trim(),
                StringComparison.Ordinal));
        return part != null;
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
            specialEffectId = ResolveSpecialEffectId(
                donor.speciesId,
                nodeId),
            specialEffectStrength = ResolveSpecialEffectStrength(
                donor.speciesId,
                nodeId),
            freshnessSeconds = kind == SurgicalPartKind.NaturalOrgan
                ? LooseFreshnessSeconds
                : 0f,
            worldStackId = stackId
        };
        sequence = nextPartSequence;
        parts.Add(part);
        RequestOrganStorage(part, position);
        return true;
    }

    public string GetSpecialEffectLabel(SurgicalPartInstance part)
    {
        return part?.specialEffectId switch
        {
            "graft:rune-deer-night-sight" => "룬사슴의 야간 시야",
            "graft:shadow-wolf-endurance" => "그림자늑대의 지구력",
            "graft:moss-boar-toughness" => "이끼멧돼지의 강인함",
            _ => string.Empty
        };
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

    public bool TryReserveForOrder(
        string partInstanceId,
        string orderId,
        out DomainFailure failure)
    {
        failure = DomainFailure.None;
        if (!TryGet(partInstanceId, out SurgicalPartInstance part)
            || part.installed)
        {
            failure = new DomainFailure(
                FailureCode.SurgeryPartUnavailable,
                partInstanceId ?? string.Empty);
            return false;
        }

        if (!string.IsNullOrWhiteSpace(part.reservedOrderId)
            && !string.Equals(part.reservedOrderId, orderId, StringComparison.Ordinal))
        {
            failure = new DomainFailure(
                FailureCode.SurgeryPartUnavailable,
                partInstanceId ?? string.Empty,
                part.reservedOrderId);
            return false;
        }

        if (part.kind == SurgicalPartKind.NaturalOrgan
            && part.freshnessSeconds <= 0f)
        {
            failure = new DomainFailure(
                FailureCode.SurgeryCorpseStale,
                partInstanceId ?? string.Empty);
            return false;
        }

        part.reservedOrderId = orderId ?? string.Empty;
        return true;
    }

    public void ReleaseReservation(string partInstanceId, string orderId)
    {
        if (TryGet(partInstanceId, out SurgicalPartInstance part)
            && string.Equals(part.reservedOrderId, orderId, StringComparison.Ordinal))
        {
            part.reservedOrderId = string.Empty;
        }
    }

    public bool TryValidateReservationForOrder(
        string partInstanceId,
        string orderId,
        out DomainFailure failure) =>
        TryValidateReservationForOrder(
            partInstanceId,
            orderId,
            requireFreshness: true,
            out failure);

    private bool TryValidateReservationForOrder(
        string partInstanceId,
        string orderId,
        bool requireFreshness,
        out DomainFailure failure)
    {
        failure = DomainFailure.None;
        if (!TryGet(partInstanceId, out SurgicalPartInstance part)
            || part.installed
            || string.IsNullOrWhiteSpace(part.worldStackId)
            || !string.Equals(
                part.reservedOrderId,
                orderId,
                StringComparison.Ordinal)
            || requireFreshness
                && part.kind == SurgicalPartKind.NaturalOrgan
                && !(part.freshnessSeconds > 0f))
        {
            failure = new DomainFailure(
                FailureCode.SurgeryPartUnavailable,
                partInstanceId ?? string.Empty,
                orderId ?? string.Empty);
            return false;
        }

        WorldItemStackSnapshot[] matching = items.GetAllStacks()
            .Where(stack => stack != null
                && string.Equals(
                    stack.StackId,
                    part.worldStackId,
                    StringComparison.Ordinal))
            .ToArray();
        if (matching.Length != 1
            || matching[0].Quantity != 1
            || !string.Equals(
                matching[0].ItemId,
                part.itemDefinitionId,
                StringComparison.Ordinal)
            || !string.Equals(
                matching[0].ItemInstanceId,
                part.physicalItemInstanceId,
                StringComparison.Ordinal))
        {
            failure = new DomainFailure(
                FailureCode.SurgeryPartUnavailable,
                partInstanceId ?? string.Empty,
                "surgical-part-physical-identity-changed");
            return false;
        }
        return true;
    }

    public bool TryConsumeForInstallation(
        string partInstanceId,
        string orderId,
        string subjectId,
        out SurgicalPartInstance part,
        out DomainFailure failure)
    {
        if (!TryPrepareForInstallation(
                partInstanceId,
                orderId,
                subjectId,
                allowExpiredCommittedPart: false,
                out part,
                out failure))
        {
            return false;
        }
        if (!SurgicalPartInstallationOutbox.TryFinalizePending(
                part,
                batchDispositions,
                out string finalizeFailure))
        {
            failure = new DomainFailure(
                FailureCode.SurgeryPartUnavailable,
                partInstanceId ?? string.Empty,
                finalizeFailure);
            return false;
        }
        return true;
    }

    private bool TryPrepareForInstallation(
        string partInstanceId,
        string orderId,
        string subjectId,
        bool allowExpiredCommittedPart,
        out SurgicalPartInstance part,
        out DomainFailure failure)
    {
        part = null;
        failure = DomainFailure.None;
        if (string.IsNullOrEmpty(partInstanceId)
            || string.IsNullOrEmpty(orderId)
            || string.IsNullOrEmpty(subjectId)
            || !string.Equals(
                partInstanceId,
                partInstanceId.Trim(),
                StringComparison.Ordinal)
            || !string.Equals(orderId, orderId.Trim(), StringComparison.Ordinal)
            || !string.Equals(subjectId, subjectId.Trim(), StringComparison.Ordinal)
            || !TryGet(partInstanceId, out part))
        {
            failure = new DomainFailure(
                FailureCode.SurgeryPartUnavailable,
                partInstanceId ?? string.Empty);
            return false;
        }

        string operationId = SurgicalPartInstallationIdentity.FormatOperationId(
            orderId,
            partInstanceId);
        if (part.installed)
        {
            if (!string.Equals(
                    part.installationOrderId,
                    orderId,
                    StringComparison.Ordinal)
                || !string.Equals(
                    part.installationOperationId,
                    operationId,
                    StringComparison.Ordinal)
                || !string.Equals(
                    part.installationSubjectId,
                    subjectId,
                    StringComparison.Ordinal)
                || !string.Equals(
                    part.installedSubjectId,
                    subjectId,
                    StringComparison.Ordinal))
            {
                failure = new DomainFailure(
                    FailureCode.SurgeryPartUnavailable,
                    partInstanceId ?? string.Empty,
                    "installation-replay-conflict");
                return false;
            }
            return true;
        }
        bool createdIntent = string.IsNullOrEmpty(part.installationOperationId);
        if (createdIntent)
        {
            if (!string.Equals(
                    part.reservedOrderId,
                    orderId,
                    StringComparison.Ordinal))
            {
                failure = new DomainFailure(
                    FailureCode.SurgeryPartUnavailable,
                    partInstanceId ?? string.Empty);
                return false;
            }
            if (!allowExpiredCommittedPart
                && part.kind == SurgicalPartKind.NaturalOrgan
                && !(part.freshnessSeconds > 0f))
            {
                failure = new DomainFailure(
                    FailureCode.SurgeryCorpseStale,
                    partInstanceId);
                return false;
            }
            if (!TryValidateReservationForOrder(
                    partInstanceId,
                    orderId,
                    requireFreshness: !allowExpiredCommittedPart,
                    out failure))
            {
                return false;
            }
            part.installationOrderId = orderId ?? string.Empty;
            part.installationOperationId = operationId;
            part.installationSourceStackId = part.worldStackId ?? string.Empty;
            part.installationSubjectId = subjectId ?? string.Empty;
        }
        if (!string.Equals(part.installationOrderId, orderId, StringComparison.Ordinal)
            || !string.Equals(
                part.installationOperationId,
                operationId,
                StringComparison.Ordinal)
            || !string.Equals(
                part.installationSourceStackId,
                part.worldStackId,
                StringComparison.Ordinal)
            || !string.Equals(
                part.installationSubjectId,
                subjectId,
                StringComparison.Ordinal)
            || string.IsNullOrEmpty(part.installationSourceStackId))
        {
            if (createdIntent)
            {
                ClearInstallationIntent(part);
            }
            failure = new DomainFailure(
                FailureCode.SurgeryPartUnavailable,
                partInstanceId ?? string.Empty,
                "installation-intent-conflict");
            return false;
        }
        if (!batchDispositions.TryCommitPending(
                new[]
                {
                    new PhysicalItemTransformInput(
                        part.installationSourceStackId,
                        1)
                },
                PhysicalItemDispositionKind.Transfer,
                operationId,
                SurgicalPartInstallationOutbox.TransferReason,
                out PhysicalItemBatchDispositionReceipt disposition,
                out string dispositionFailure))
        {
            if (createdIntent)
            {
                ClearInstallationIntent(part);
            }
            failure = new DomainFailure(
                FailureCode.SurgeryPartUnavailable,
                partInstanceId ?? string.Empty,
                dispositionFailure ?? string.Empty);
            return false;
        }

        part.installationCommitId = disposition.CommitId;
        return true;
    }

    private bool TryValidateForInstallation(
        string partInstanceId,
        string orderId,
        string subjectId,
        bool allowExpiredCommittedPart,
        out SurgicalPartInstance part,
        out DomainFailure failure)
    {
        part = null;
        failure = DomainFailure.None;
        if (string.IsNullOrEmpty(partInstanceId)
            || string.IsNullOrEmpty(orderId)
            || string.IsNullOrEmpty(subjectId)
            || !string.Equals(
                partInstanceId,
                partInstanceId.Trim(),
                StringComparison.Ordinal)
            || !string.Equals(orderId, orderId.Trim(), StringComparison.Ordinal)
            || !string.Equals(subjectId, subjectId.Trim(), StringComparison.Ordinal)
            || !TryGet(partInstanceId, out part))
        {
            failure = new DomainFailure(
                FailureCode.SurgeryPartUnavailable,
                partInstanceId ?? string.Empty);
            return false;
        }

        string operationId = SurgicalPartInstallationIdentity.FormatOperationId(
            orderId,
            partInstanceId);
        if (part.installed)
        {
            if (!string.Equals(part.installationOrderId, orderId,
                    StringComparison.Ordinal)
                || !string.Equals(part.installationOperationId, operationId,
                    StringComparison.Ordinal)
                || !string.Equals(part.installationSubjectId, subjectId,
                    StringComparison.Ordinal)
                || !string.Equals(part.installedSubjectId, subjectId,
                    StringComparison.Ordinal))
            {
                failure = new DomainFailure(
                    FailureCode.SurgeryPartUnavailable,
                    partInstanceId,
                    "installation-replay-conflict");
                return false;
            }
            return true;
        }

        if (string.IsNullOrEmpty(part.installationOperationId))
        {
            return TryValidateReservationForOrder(
                partInstanceId,
                orderId,
                requireFreshness: !allowExpiredCommittedPart,
                out failure);
        }

        if (!string.Equals(part.installationOrderId, orderId,
                StringComparison.Ordinal)
            || !string.Equals(part.installationOperationId, operationId,
                StringComparison.Ordinal)
            || !string.Equals(part.installationSubjectId, subjectId,
                StringComparison.Ordinal)
            || !string.Equals(part.installationSourceStackId,
                part.worldStackId, StringComparison.Ordinal)
            || string.IsNullOrEmpty(part.installationSourceStackId))
        {
            failure = new DomainFailure(
                FailureCode.SurgeryPartUnavailable,
                partInstanceId,
                "installation-intent-conflict");
            return false;
        }

        if (string.IsNullOrEmpty(part.installationCommitId))
        {
            return TryValidateReservationForOrder(
                partInstanceId,
                orderId,
                requireFreshness: !allowExpiredCommittedPart,
                out failure);
        }
        return true;
    }

    private static void ClearInstallationIntent(SurgicalPartInstance part)
    {
        part.installationOrderId = string.Empty;
        part.installationOperationId = string.Empty;
        part.installationCommitId = string.Empty;
        part.installationSourceStackId = string.Empty;
        part.installationSubjectId = string.Empty;
    }

    bool ISurgicalPartReplacementRuntime.TryReserveReplacementOutput(
        SurgeryOrder order,
        AnatomyNodeHealthState currentNode,
        Vector2Int outputPosition,
        IFacilityBufferMassAdmissionService admission,
        out DomainFailure failure)
    {
        failure = DomainFailure.None;
        if (order == null
            || currentNode == null
            || admission == null
            || order.replacementPhase is not
                (SurgicalPartReplacementPhase.None
                or SurgicalPartReplacementPhase.OutputReservationPending)
            || order.replacementPhase ==
                    SurgicalPartReplacementPhase.OutputReservationPending
                && !string.Equals(
                    order.replacementIncomingPartId,
                    order.selectedPartInstanceId,
                    StringComparison.Ordinal)
            || !order.OwnsMaterialAuthority
            || string.IsNullOrWhiteSpace(currentNode.installedPartId)
            || !TryGet(
                currentNode.installedPartId,
                out SurgicalPartInstance previous)
            || !previous.installed
            || !string.Equals(
                previous.installedSubjectId,
                order.subject?.subjectId,
                StringComparison.Ordinal)
            || !string.Equals(
                previous.nodeId,
                order.targetNodeId,
                StringComparison.Ordinal)
            || string.IsNullOrWhiteSpace(previous.itemDefinitionId)
            || string.IsNullOrWhiteSpace(previous.physicalItemInstanceId)
            || !string.Equals(
                currentNode.installedPartId,
                order.replacementExpectedOldPartId,
                StringComparison.Ordinal)
            || !TryValidateReservationForOrder(
                order.selectedPartInstanceId,
                order.orderId,
                out _))
        {
            failure = new DomainFailure(
                FailureCode.SurgeryPartUnavailable,
                currentNode?.installedPartId ?? string.Empty,
                "replacement-owner-invalid");
            return false;
        }

        string operationId = SurgicalPartReplacementIdentity
            .FormatOperationId(order.orderId);
        string batchCommitId = SurgicalPartReplacementIdentity
            .FormatBatchCommitId(order.orderId);
        if (order.replacementReservationAttempt <= 0)
        {
            order.replacementReservationAttempt = 1;
        }
        if (string.IsNullOrEmpty(order.replacementPublicationOperationId))
        {
            order.replacementPublicationOperationId =
                SurgicalPartReplacementIdentity.FormatPublicationOperationId(
                    order.orderId,
                    order.replacementReservationAttempt);
        }
        bool preservePendingIntent = order.replacementPhase ==
            SurgicalPartReplacementPhase.OutputReservationPending;
        order.replacementDetachedCurrentHealth = currentNode.currentHealth;
        order.replacementDetachedMaxHealth = currentNode.maxHealth;
        if (!TryCreateReplacementOutputRequest(
                order,
                previous,
                outputPosition,
                operationId,
                batchCommitId,
                outcomeFingerprint: string.Empty,
                out FacilityBufferPlannedOutputRequest request,
                out failure))
        {
            if (!preservePendingIntent)
            {
                order.replacementDetachedCurrentHealth = 0f;
                order.replacementDetachedMaxHealth = 0f;
            }
            return false;
        }

        if (!admission.TryReservePlannedOutput(
                request,
                out FacilityBufferPlannedOutputToken token,
                out FacilityBufferMassAdmissionFailureCode code,
                out string reason))
        {
            failure = new DomainFailure(
                code == FacilityBufferMassAdmissionFailureCode
                    .CapacityUnavailable
                    ? FailureCode.ProductionOutputSpaceUnavailable
                    : FailureCode.ProductionOutputUnavailable,
                order.orderId,
                reason ?? string.Empty);
            if (!preservePendingIntent)
            {
                order.replacementDetachedCurrentHealth = 0f;
                order.replacementDetachedMaxHealth = 0f;
            }
            return false;
        }

        order.replacementPhase = SurgicalPartReplacementPhase.OutputReserved;
        order.replacementOperationId = operationId;
        order.replacementExpectedOldPartId = previous.partInstanceId;
        order.replacementIncomingPartId = order.selectedPartInstanceId;
        order.replacementAdmissionTokenId = token.TokenId;
        order.replacementPublicationOperationId =
            request.PublicationOperationId;
        order.replacementBatchCommitId = batchCommitId;
        order.replacementOutcomeFingerprint = request.OutcomeFingerprint;
        order.replacementPlannedOutputFingerprint =
            token.PlannedOutput.Fingerprint;
        order.replacementOutputX = outputPosition.x;
        order.replacementOutputY = outputPosition.y;
        order.replacementOutputMassGrams = token.ReservedMassGrams;
        return true;
    }

    bool ISurgicalPartReplacementRuntime.TryCommitReplacement(
        SurgeryOrder order,
        CharacterActor character,
        SurgicalPartKind incomingKind,
        float efficiency,
        IAnatomyHealthRuntime anatomy,
        IFacilityBufferMassAdmissionService admission,
        IFacilityBufferPlannedOutputPublicationService publication,
        out DomainFailure failure)
    {
        failure = DomainFailure.None;
        if (order == null
            || character == null
            || anatomy == null
            || admission == null
            || publication == null
            || order.replacementPhase is SurgicalPartReplacementPhase.None
                or SurgicalPartReplacementPhase.Completed
            || !TryGet(
                order.replacementExpectedOldPartId,
                out SurgicalPartInstance previous)
            || !TryGet(
                order.replacementIncomingPartId,
                out SurgicalPartInstance incoming))
        {
            failure = ReplacementFailure(
                order?.orderId,
                "replacement-transaction-invalid");
            return false;
        }

        string subjectId = character.Identity?.PersistentId ?? string.Empty;
        bool bodyCommitted = order.replacementPhase is
            SurgicalPartReplacementPhase.BodyCommitted
            or SurgicalPartReplacementPhase.OutputPublished;
        if (bodyCommitted)
        {
            AnatomyNodeHealthState bodyOwner = anatomy
                .GetAnatomySnapshot(character)
                .Nodes.FirstOrDefault(node => node != null
                    && string.Equals(
                        node.nodeId,
                        order.targetNodeId,
                        StringComparison.Ordinal));
            if (bodyOwner == null
                || !string.Equals(
                    bodyOwner.installedPartId,
                    incoming.partInstanceId,
                    StringComparison.Ordinal)
                || bodyOwner.installedPartKind != incomingKind)
            {
                failure = ReplacementFailure(
                    order.orderId,
                    "replacement-body-owner-conflict");
                return false;
            }
        }
        if (!TryValidateForInstallation(
                incoming.partInstanceId,
                order.orderId,
                subjectId,
                allowExpiredCommittedPart: bodyCommitted,
                out incoming,
                out failure))
        {
            return false;
        }

        if (order.replacementPhase ==
            SurgicalPartReplacementPhase.OutputReservationPending)
        {
            AnatomyNodeHealthState current = anatomy
                .GetAnatomySnapshot(character)
                .Nodes.FirstOrDefault(node => node != null
                    && string.Equals(
                        node.nodeId,
                        order.targetNodeId,
                        StringComparison.Ordinal));
            if (!((ISurgicalPartReplacementRuntime)this)
                    .TryReserveReplacementOutput(
                        order,
                        current,
                        new Vector2Int(
                            order.replacementOutputX,
                            order.replacementOutputY),
                        admission,
                        out failure))
            {
                return false;
            }
        }

        FacilityBufferPlannedOutputToken token = default;
        FacilityBufferMassAdmissionTokenStatus tokenStatus =
            FacilityBufferMassAdmissionTokenStatus.Released;
        if (order.replacementPhase ==
            SurgicalPartReplacementPhase.OutputReserved)
        {
            if (!TryGetOrRestoreReplacementToken(
                    order,
                    previous,
                    admission,
                    out token,
                    out tokenStatus,
                    out failure))
            {
                return false;
            }
            bool preserveDurability =
                incoming.detachedDurabilityMaximum > 0f;
            if (!anatomy.TryReplaceNodePart(
                    character,
                    order.targetNodeId,
                    previous.partInstanceId,
                    order.replacementDetachedCurrentHealth,
                    order.replacementDetachedMaxHealth,
                    incoming.partInstanceId,
                    incomingKind,
                    efficiency,
                    incoming.detachedDurabilityCurrent,
                    preserveDurability,
                    out AnatomyNodeHealthState replaced,
                    out failure))
            {
                AnatomyNodeHealthState current = anatomy
                    .GetAnatomySnapshot(character)
                    .Nodes.FirstOrDefault(node => node != null
                        && string.Equals(
                            node.nodeId,
                            order.targetNodeId,
                            StringComparison.Ordinal));
                if (current != null
                    && string.Equals(
                        current.installedPartId,
                        previous.partInstanceId,
                        StringComparison.Ordinal)
                    && (current.currentHealth !=
                            order.replacementDetachedCurrentHealth
                        || current.maxHealth !=
                            order.replacementDetachedMaxHealth))
                {
                    DomainFailure originalFailure = failure;
                    if (!TryRefreshReplacementReservation(
                            order,
                            current,
                            token.Request.DropPosition,
                            admission,
                            out DomainFailure refreshFailure))
                    {
                        failure = refreshFailure;
                        return false;
                    }
                    failure = originalFailure;
                }
                return false;
            }
            if (replaced.currentHealth !=
                    order.replacementDetachedCurrentHealth
                || replaced.maxHealth != order.replacementDetachedMaxHealth)
            {
                failure = ReplacementFailure(
                    order.orderId,
                    "replacement-detached-health-conflict");
                return false;
            }
            order.replacementPhase = SurgicalPartReplacementPhase.BodyCommitted;
        }

        if (!TryPrepareForInstallation(
                incoming.partInstanceId,
                order.orderId,
                subjectId,
                allowExpiredCommittedPart: true,
                out incoming,
                out failure))
        {
            return false;
        }
        if (!SurgicalPartInstallationOutbox.TryFinalizePending(
                incoming,
                batchDispositions,
                out string installationFailure))
        {
            failure = ReplacementFailure(order.orderId, installationFailure);
            return false;
        }
        character.Stats.RefreshDerivedMaximumHealthPreservingCurrent();

        if (publication.TryCaptureBatch(
                order.replacementBatchCommitId,
                allowAcknowledged: true,
                out FacilityBufferPlannedOutputRestoreBatchSnapshot restored,
                out bool acknowledged,
                out _,
                out string captureFailure))
        {
            if (!ValidateReplacementPublication(
                    order,
                    previous,
                    restored,
                    out string restoredJoinFailure))
            {
                failure = ReplacementFailure(
                    order.orderId,
                    restoredJoinFailure);
                return false;
            }
            AdoptReplacementPublication(order, previous, restored.Stacks.Single());

            if (admission.TryGetPlannedOutputToken(
                    order.replacementAdmissionTokenId,
                    out token,
                    out tokenStatus))
            {
                if (tokenStatus == FacilityBufferMassAdmissionTokenStatus.Released
                    || !ReplacementTokenMatches(order, previous, token))
                {
                    failure = ReplacementFailure(
                        order.orderId,
                        "replacement-admission-state-conflict");
                    return false;
                }
                if (tokenStatus == FacilityBufferMassAdmissionTokenStatus.Reserved)
                {
                    if (!TryCreateReplacementPublicationReceipt(
                            token,
                            restored,
                            out FacilityBufferPlannedOutputPublicationReceipt
                                restoredReceipt,
                            out string restoredReceiptFailure)
                        || !admission.TryCommitPlannedOutput(
                            token,
                            restoredReceipt,
                            out _,
                            out _,
                            out restoredReceiptFailure))
                    {
                        failure = ReplacementFailure(
                            order.orderId,
                            restoredReceiptFailure);
                        return false;
                    }
                }
            }

            if (!acknowledged
                && !publication.TryAcknowledgeRestoreCandidate(
                    restored,
                    out _,
                    out string restoreAcknowledgementFailure))
            {
                failure = ReplacementFailure(
                    order.orderId,
                    restoreAcknowledgementFailure);
                return false;
            }
            order.replacementPhase = SurgicalPartReplacementPhase.Completed;
            return true;
        }
        if (!captureFailure.StartsWith(
                "planned-output-batch-missing:",
                StringComparison.Ordinal))
        {
            failure = ReplacementFailure(order.orderId, captureFailure);
            return false;
        }
        if (order.replacementPhase ==
            SurgicalPartReplacementPhase.OutputPublished)
        {
            failure = ReplacementFailure(
                order.orderId,
                "replacement-published-output-missing");
            return false;
        }

        if (string.IsNullOrEmpty(token.TokenId)
            && !TryGetOrRestoreReplacementToken(
                order,
                previous,
                admission,
                out token,
                out tokenStatus,
                out failure))
        {
            return false;
        }
        if (!publication.TryPublishFullBatch(
                token,
                out FacilityBufferPlannedOutputPublicationReceipt published,
                out _,
                out string publicationFailure))
        {
            failure = ReplacementFailure(order.orderId, publicationFailure);
            return false;
        }
        if (!ValidateReplacementPublication(
                order,
                previous,
                published,
                out string joinFailure))
        {
            failure = ReplacementFailure(order.orderId, joinFailure);
            return false;
        }
        AdoptReplacementPublication(order, previous, published.Stacks.Single());

        if (tokenStatus != FacilityBufferMassAdmissionTokenStatus.Routed
            && !admission.TryCommitPlannedOutput(
                token,
                published,
                out _,
                out _,
                out string commitFailure))
        {
            failure = ReplacementFailure(order.orderId, commitFailure);
            return false;
        }
        if (!publication.TryAcknowledgePublishedBatch(
                published,
                out _,
                out string acknowledgementFailure))
        {
            failure = ReplacementFailure(
                order.orderId,
                acknowledgementFailure);
            return false;
        }

        order.replacementPhase = SurgicalPartReplacementPhase.Completed;
        return true;
    }

    bool ISurgicalPartReplacementRuntime.TryAbortReplacement(
        SurgeryOrder order,
        IFacilityBufferMassAdmissionService admission,
        out string failureReason)
    {
        failureReason = string.Empty;
        if (order == null)
        {
            return true;
        }
        if (order.replacementPhase == SurgicalPartReplacementPhase.None)
        {
            ClearReplacement(order);
            return true;
        }
        if (order.replacementPhase !=
                SurgicalPartReplacementPhase.OutputReserved
            || admission == null)
        {
            failureReason = "replacement-abort-not-safe";
            return false;
        }
        if (admission.TryGetPlannedOutputToken(
                order.replacementAdmissionTokenId,
                out FacilityBufferPlannedOutputToken token,
                out FacilityBufferMassAdmissionTokenStatus status)
            && status != FacilityBufferMassAdmissionTokenStatus.Released
            && !admission.TryReleasePlannedOutput(
                token,
                FacilityBufferMassAdmissionReleaseReason.TransactionRollback,
                out _,
                out failureReason))
        {
            return false;
        }
        ClearReplacement(order);
        return true;
    }

    private bool TryGetOrRestoreReplacementToken(
        SurgeryOrder order,
        SurgicalPartInstance previous,
        IFacilityBufferMassAdmissionService admission,
        out FacilityBufferPlannedOutputToken token,
        out FacilityBufferMassAdmissionTokenStatus status,
        out DomainFailure failure)
    {
        token = default;
        status = FacilityBufferMassAdmissionTokenStatus.Released;
        failure = DomainFailure.None;
        if (admission.TryGetPlannedOutputToken(
                order.replacementAdmissionTokenId,
                out token,
                out status))
        {
            if (status != FacilityBufferMassAdmissionTokenStatus.Released
                && ReplacementTokenMatches(order, previous, token))
            {
                return true;
            }
            failure = ReplacementFailure(
                order.orderId,
                "replacement-admission-state-conflict");
            return false;
        }

        Vector2Int outputPosition = new(
            order.replacementOutputX,
            order.replacementOutputY);
        if (!TryCreateReplacementOutputRequest(
                order,
                previous,
                outputPosition,
                order.replacementOperationId,
                order.replacementBatchCommitId,
                order.replacementOutcomeFingerprint,
                out FacilityBufferPlannedOutputRequest request,
                out failure))
        {
            return false;
        }
        if (!admission.TryReservePlannedOutput(
                request,
                out token,
                out FacilityBufferMassAdmissionFailureCode code,
                out string reason))
        {
            failure = new DomainFailure(
                code == FacilityBufferMassAdmissionFailureCode
                    .CapacityUnavailable
                    ? FailureCode.ProductionOutputSpaceUnavailable
                    : FailureCode.ProductionOutputUnavailable,
                order.orderId,
                reason ?? string.Empty);
            return false;
        }
        if (token.ReservedMassGrams != order.replacementOutputMassGrams
            || !string.Equals(
                token.PlannedOutput.Fingerprint,
                order.replacementPlannedOutputFingerprint,
                StringComparison.Ordinal))
        {
            admission.TryReleasePlannedOutput(
                token,
                FacilityBufferMassAdmissionReleaseReason.TransactionRollback,
                out _,
                out _);
            failure = ReplacementFailure(
                order.orderId,
                "replacement-restored-reservation-drift");
            return false;
        }
        order.replacementAdmissionTokenId = token.TokenId;
        status = FacilityBufferMassAdmissionTokenStatus.Reserved;
        return true;
    }

    private bool TryRefreshReplacementReservation(
        SurgeryOrder order,
        AnatomyNodeHealthState current,
        Vector2Int outputPosition,
        IFacilityBufferMassAdmissionService admission,
        out DomainFailure failure)
    {
        failure = DomainFailure.None;
        string expectedOldPartId = order.replacementExpectedOldPartId;
        string incomingPartId = order.replacementIncomingPartId;
        int nextAttempt = checked(order.replacementReservationAttempt + 1);
        if (!((ISurgicalPartReplacementRuntime)this).TryAbortReplacement(
                order,
                admission,
                out string abortFailure))
        {
            failure = ReplacementFailure(order.orderId, abortFailure);
            return false;
        }
        order.replacementPhase =
            SurgicalPartReplacementPhase.OutputReservationPending;
        order.replacementOperationId = SurgicalPartReplacementIdentity
            .FormatOperationId(order.orderId);
        order.replacementExpectedOldPartId = expectedOldPartId;
        order.replacementIncomingPartId = incomingPartId;
        order.replacementReservationAttempt = nextAttempt;
        order.replacementPublicationOperationId =
            SurgicalPartReplacementIdentity.FormatPublicationOperationId(
                order.orderId,
                nextAttempt);
        order.replacementBatchCommitId = SurgicalPartReplacementIdentity
            .FormatBatchCommitId(order.orderId);
        order.replacementOutputX = outputPosition.x;
        order.replacementOutputY = outputPosition.y;
        order.replacementDetachedCurrentHealth = current.currentHealth;
        order.replacementDetachedMaxHealth = current.maxHealth;
        return ((ISurgicalPartReplacementRuntime)this).TryReserveReplacementOutput(
            order,
            current,
            outputPosition,
            admission,
            out failure);
    }

    private bool TryCreateReplacementOutputRequest(
        SurgeryOrder order,
        SurgicalPartInstance previous,
        Vector2Int outputPosition,
        string operationId,
        string batchCommitId,
        string outcomeFingerprint,
        out FacilityBufferPlannedOutputRequest request,
        out DomainFailure failure)
    {
        request = default;
        failure = DomainFailure.None;
        try
        {
            FacilityBufferCapacityProfile profile =
                new FacilityBufferCapacityProfile(
                    order.materialDestinationId,
                    outputPosition,
                    SurgeryMaterialDestinationAuthority.OwnerDomain,
                    order.orderId,
                    order.facilityId,
                    new PhysicalMassGrams(order.materialBufferCapacityGrams),
                    SurgeryMaterialDestinationAuthority
                        .InputBufferCapacitySchemaRevision);
            List<ItemInstanceComponentSaveData> components =
                CreateRecoveryPhysicalComponents(
                    previous,
                    order,
                    operationId,
                    order.replacementDetachedCurrentHealth,
                    order.replacementDetachedMaxHealth);
            PhysicalItemMassSubject subject =
                PhysicalItemMassSubjectAdapter.Create(
                    physicalMass,
                    (ItemDefinitionId)previous.itemDefinitionId,
                    previous.physicalItemInstanceId,
                    components);
            string computedOutcome = CreateReplacementOutcomeFingerprint(
                order,
                previous,
                order.replacementDetachedCurrentHealth,
                order.replacementDetachedMaxHealth,
                components);
            if (!string.IsNullOrEmpty(outcomeFingerprint)
                && !string.Equals(
                    outcomeFingerprint,
                    computedOutcome,
                    StringComparison.Ordinal))
            {
                failure = ReplacementFailure(
                    order.orderId,
                    "replacement-outcome-fingerprint-drift");
                return false;
            }
            string frozenOutcome = computedOutcome;
            request = new FacilityBufferPlannedOutputRequest(
                order.replacementPublicationOperationId,
                batchCommitId,
                frozenOutcome,
                order.materialDestinationId,
                outputPosition,
                profile.OwnerDomain,
                profile.OwnerOperationId,
                profile.OwnerFacilityId,
                profile.CapacityRevision,
                new[]
                {
                    new FacilityBufferPlannedOutputSlice(
                        SurgicalPartReplacementIdentity.OutputLineId,
                        subject,
                        1,
                        components)
                });
            if (order.replacementReservationAttempt <= 0
                || !string.Equals(
                    order.replacementPublicationOperationId,
                    SurgicalPartReplacementIdentity.FormatPublicationOperationId(
                        order.orderId,
                        order.replacementReservationAttempt),
                    StringComparison.Ordinal))
            {
                failure = ReplacementFailure(
                    order.orderId,
                    "replacement-publication-operation-drift");
                request = default;
                return false;
            }
            return true;
        }
        catch (Exception exception) when (exception is ArgumentException
                                           or InvalidOperationException
                                           or OverflowException)
        {
            failure = new DomainFailure(
                FailureCode.ProductionOutputUnavailable,
                order.orderId,
                exception.Message);
            return false;
        }
    }

    private static bool ValidateReplacementPublication(
        SurgeryOrder order,
        SurgicalPartInstance previous,
        FacilityBufferPlannedOutputPublicationReceipt published,
        out string failureReason)
    {
        failureReason = string.Empty;
        if (published.Stacks.Count == 1
            && string.Equals(
                published.AdmissionTokenId,
                order.replacementAdmissionTokenId,
                StringComparison.Ordinal)
            && string.Equals(
                published.BatchCommitId,
                order.replacementBatchCommitId,
                StringComparison.Ordinal)
            && string.Equals(
                published.OutcomeFingerprint,
                order.replacementOutcomeFingerprint,
                StringComparison.Ordinal)
            && string.Equals(
                published.DestinationId,
                order.materialDestinationId,
                StringComparison.Ordinal)
            && string.Equals(
                published.Stacks[0].OutputLineId,
                SurgicalPartReplacementIdentity.OutputLineId,
                StringComparison.Ordinal)
            && string.Equals(
                published.Stacks[0].ItemDefinitionId.Value,
                previous.itemDefinitionId,
                StringComparison.Ordinal)
            && string.Equals(
                published.Stacks[0].ItemInstanceId,
                previous.physicalItemInstanceId,
                StringComparison.Ordinal)
            && published.Stacks[0].Quantity == 1
            && published.Stacks[0].MassGrams ==
                order.replacementOutputMassGrams)
        {
            return true;
        }
        failureReason = "replacement-publication-join-invalid";
        return false;
    }

    private static bool ValidateReplacementPublication(
        SurgeryOrder order,
        SurgicalPartInstance previous,
        FacilityBufferPlannedOutputRestoreBatchSnapshot restored,
        out string failureReason)
    {
        failureReason = string.Empty;
        if (restored != null
            && restored.Stacks.Count == 1
            && restored.TotalQuantity == 1
            && restored.TotalMassGrams == order.replacementOutputMassGrams
            && string.Equals(restored.BatchCommitId,
                order.replacementBatchCommitId, StringComparison.Ordinal)
            && string.Equals(restored.OutcomeFingerprint,
                order.replacementOutcomeFingerprint, StringComparison.Ordinal)
            && string.Equals(restored.PlannedOutputFingerprint,
                order.replacementPlannedOutputFingerprint,
                StringComparison.Ordinal)
            && string.Equals(restored.Stacks[0].OutputLineId,
                SurgicalPartReplacementIdentity.OutputLineId,
                StringComparison.Ordinal)
            && string.Equals(restored.Stacks[0].ItemId,
                previous.itemDefinitionId, StringComparison.Ordinal)
            && string.Equals(restored.Stacks[0].ItemInstanceId,
                previous.physicalItemInstanceId, StringComparison.Ordinal)
            && restored.Stacks[0].Quantity == 1
            && restored.Stacks[0].MassGrams ==
                order.replacementOutputMassGrams
            && restored.Stacks[0].State ==
                WorldItemStackState.FacilityOutputBuffer
            && restored.Stacks[0].Position == new Vector2Int(
                order.replacementOutputX,
                order.replacementOutputY)
            && string.Equals(restored.Stacks[0].DestinationId,
                order.materialDestinationId, StringComparison.Ordinal))
        {
            return true;
        }
        failureReason = "replacement-restored-publication-join-invalid";
        return false;
    }

    private static bool TryCreateReplacementPublicationReceipt(
        FacilityBufferPlannedOutputToken token,
        FacilityBufferPlannedOutputRestoreBatchSnapshot restored,
        out FacilityBufferPlannedOutputPublicationReceipt receipt,
        out string failureReason)
    {
        receipt = default;
        if (restored == null
            || !string.Equals(restored.BatchCommitId,
                token.Request.BatchCommitId, StringComparison.Ordinal)
            || !string.Equals(restored.OutcomeFingerprint,
                token.Request.OutcomeFingerprint, StringComparison.Ordinal)
            || !string.Equals(restored.PlannedOutputFingerprint,
                token.PlannedOutput.Fingerprint, StringComparison.Ordinal)
            || restored.TotalMassGrams != token.ReservedMassGrams
            || restored.TotalQuantity != token.PlannedOutput.TotalQuantity
            || restored.Stacks.Any(value => value == null
                || value.State != WorldItemStackState.FacilityOutputBuffer
                || value.Position != token.Request.DropPosition
                || !string.Equals(value.DestinationId,
                    token.Request.DestinationId, StringComparison.Ordinal)))
        {
            failureReason = "replacement-physical-ahead-conflict";
            return false;
        }

        FacilityBufferPublishedOutputStackReceipt[] stacks = restored.Stacks
            .OrderBy(value => value.OutputLineId, StringComparer.Ordinal)
            .ThenBy(value => value.StackOrdinal)
            .Select(value => new FacilityBufferPublishedOutputStackReceipt(
                value.StackId,
                value.OutputLineId,
                (ItemDefinitionId)value.ItemId,
                value.Quantity,
                new PhysicalMassGrams(value.MassGrams),
                value.ItemInstanceId))
            .ToArray();
        receipt = new FacilityBufferPlannedOutputPublicationReceipt(
            token.TokenId,
            token.Request.BatchCommitId,
            token.Request.OutcomeFingerprint,
            token.Request.DestinationId,
            token.Request.DropPosition,
            token.Request.ExpectedOwnerDomain,
            token.Request.ExpectedOwnerOperationId,
            token.Request.ExpectedOwnerFacilityId,
            token.Request.ExpectedCapacityRevision,
            token.PlannedOutput.Fingerprint,
            stacks);
        failureReason = string.Empty;
        return true;
    }

    private static void AdoptReplacementPublication(
        SurgeryOrder order,
        SurgicalPartInstance previous,
        FacilityBufferPublishedOutputStackReceipt output) =>
        AdoptReplacementPublication(
            order,
            previous,
            output.StackId,
            output.ItemInstanceId);

    private static void AdoptReplacementPublication(
        SurgeryOrder order,
        SurgicalPartInstance previous,
        FacilityBufferPlannedOutputRestoreStackSnapshot output) =>
        AdoptReplacementPublication(
            order,
            previous,
            output.StackId,
            output.ItemInstanceId);

    private static void AdoptReplacementPublication(
        SurgeryOrder order,
        SurgicalPartInstance previous,
        string stackId,
        string itemInstanceId)
    {
        previous.worldStackId = stackId;
        previous.storedFacilityId = string.Empty;
        previous.reservedOrderId = order.orderId;
        previous.installed = false;
        previous.installedSubjectId = string.Empty;
        previous.detachedDurabilityCurrent =
            order.replacementDetachedCurrentHealth;
        previous.detachedDurabilityMaximum =
            order.replacementDetachedMaxHealth;
        previous.recoveryOperationId = order.replacementOperationId;
        previous.recoveryOrderId = order.orderId;
        previous.recoveryCommitId = order.replacementBatchCommitId;
        ClearInstallationIntent(previous);
        order.replacementOutputStackId = stackId;
        order.replacementOutputItemInstanceId = itemInstanceId;
        order.replacementPhase = SurgicalPartReplacementPhase.OutputPublished;
    }

    private static bool ReplacementTokenMatches(
        SurgeryOrder order,
        SurgicalPartInstance previous,
        FacilityBufferPlannedOutputToken token) =>
        string.Equals(token.TokenId, order.replacementAdmissionTokenId,
            StringComparison.Ordinal)
        && string.Equals(token.Request.BatchCommitId,
            order.replacementBatchCommitId, StringComparison.Ordinal)
        && string.Equals(token.Request.OutcomeFingerprint,
            order.replacementOutcomeFingerprint, StringComparison.Ordinal)
        && string.Equals(token.Request.PublicationOperationId,
            order.replacementPublicationOperationId, StringComparison.Ordinal)
        && string.Equals(token.Request.DestinationId,
            order.materialDestinationId, StringComparison.Ordinal)
        && token.Request.DropPosition == new Vector2Int(
            order.replacementOutputX,
            order.replacementOutputY)
        && token.ReservedMassGrams == order.replacementOutputMassGrams
        && string.Equals(token.PlannedOutput.Fingerprint,
            order.replacementPlannedOutputFingerprint,
            StringComparison.Ordinal)
        && token.PlannedOutput.Slices.Count == 1
        && string.Equals(token.PlannedOutput.Slices[0].ItemDefinitionId.Value,
            previous.itemDefinitionId, StringComparison.Ordinal)
        && string.Equals(token.PlannedOutput.Slices[0].Source.Subject?.ItemInstanceId,
            previous.physicalItemInstanceId, StringComparison.Ordinal);

    private static List<ItemInstanceComponentSaveData>
        CreateRecoveryPhysicalComponents(
            SurgicalPartInstance part,
            SurgeryOrder order,
            string operationId,
            float currentHealth,
            float maxHealth)
    {
        List<ItemInstanceComponentSaveData> components =
            CreateBasePhysicalComponents(part);
        components.Add(SurgicalPartRecoveryComponentCodec.Create(
            part,
            order.orderId,
            operationId,
            currentHealth,
            maxHealth));
        return components;
    }

    private static List<ItemInstanceComponentSaveData>
        CreateBasePhysicalComponents(SurgicalPartInstance part)
    {
        List<ItemInstanceComponentSaveData> components = new();
        if (!string.IsNullOrWhiteSpace(part.sourceProductionCommitId))
        {
            components.Add(SurgicalPartPreparedOutputComponentCodec.Create(
                new SurgicalPartPreparedOutput
                {
                    ItemId = part.itemDefinitionId,
                    PhysicalItemInstanceId = part.physicalItemInstanceId,
                    PartInstanceId = part.partInstanceId,
                    NodeId = part.nodeId,
                    DisplayName = part.displayName,
                    Kind = part.kind,
                    Quality = part.quality,
                    CommitId = part.sourceProductionCommitId,
                    IsReplay = true
                }));
        }
        return components;
    }

    private static string CreateReplacementOutcomeFingerprint(
        SurgeryOrder order,
        SurgicalPartInstance part,
        float currentHealth,
        float maxHealth,
        IEnumerable<ItemInstanceComponentSaveData> components)
    {
        string canonical = string.Join("|", new[]
        {
            "surgical-part-replacement-v1",
            order.orderId,
            order.subject?.subjectId ?? string.Empty,
            order.targetNodeId,
            order.selectedPartInstanceId,
            part.partInstanceId,
            part.itemDefinitionId,
            part.physicalItemInstanceId,
            currentHealth.ToString("R", CultureInfo.InvariantCulture),
            maxHealth.ToString("R", CultureInfo.InvariantCulture),
            string.Join(";", (components
                    ?? Array.Empty<ItemInstanceComponentSaveData>())
                .Select(component => component.ToCanonicalString())
                .OrderBy(value => value, StringComparer.Ordinal))
        });
        using SHA256 sha = SHA256.Create();
        byte[] digest = sha.ComputeHash(Encoding.UTF8.GetBytes(canonical));
        StringBuilder hex = new(digest.Length * 2);
        foreach (byte value in digest)
            hex.Append(value.ToString("x2", CultureInfo.InvariantCulture));
        return hex.ToString();
    }

    internal static bool ReplacementOutcomeFingerprintMatches(
        SurgeryOrder order,
        SurgicalPartInstance part)
    {
        if (order == null || part == null)
            return false;
        try
        {
            return string.Equals(
                order.replacementOutcomeFingerprint,
                CreateReplacementOutcomeFingerprintForValidation(order, part),
                StringComparison.Ordinal);
        }
        catch (Exception exception) when (exception is ArgumentException
                                           or InvalidOperationException
                                           or OverflowException)
        {
            return false;
        }
    }

    internal static string CreateReplacementOutcomeFingerprintForValidation(
        SurgeryOrder order,
        SurgicalPartInstance part)
    {
        List<ItemInstanceComponentSaveData> components =
            CreateRecoveryPhysicalComponents(
                part,
                order,
                order.replacementOperationId,
                order.replacementDetachedCurrentHealth,
                order.replacementDetachedMaxHealth);
        return CreateReplacementOutcomeFingerprint(
            order,
            part,
            order.replacementDetachedCurrentHealth,
            order.replacementDetachedMaxHealth,
            components);
    }

    private static DomainFailure ReplacementFailure(
        string orderId,
        string detail) => new(
        FailureCode.ProductionOutputUnavailable,
        orderId ?? string.Empty,
        detail ?? string.Empty);

    private static void ClearReplacement(SurgeryOrder order)
    {
        order.replacementPhase = SurgicalPartReplacementPhase.None;
        order.replacementOperationId = string.Empty;
        order.replacementExpectedOldPartId = string.Empty;
        order.replacementIncomingPartId = string.Empty;
        order.replacementAdmissionTokenId = string.Empty;
        order.replacementPublicationOperationId = string.Empty;
        order.replacementReservationAttempt = 0;
        order.replacementBatchCommitId = string.Empty;
        order.replacementOutcomeFingerprint = string.Empty;
        order.replacementPlannedOutputFingerprint = string.Empty;
        order.replacementOutputX = 0;
        order.replacementOutputY = 0;
        order.replacementOutputStackId = string.Empty;
        order.replacementOutputItemInstanceId = string.Empty;
        order.replacementOutputMassGrams = 0L;
        order.replacementDetachedCurrentHealth = 0f;
        order.replacementDetachedMaxHealth = 0f;
    }

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

    private static string ResolveSpecialEffectId(
        string speciesId,
        string nodeId)
    {
        if (string.Equals(speciesId, "rune_deer", StringComparison.OrdinalIgnoreCase)
            && nodeId?.StartsWith("eye:", StringComparison.Ordinal) == true)
        {
            return "graft:rune-deer-night-sight";
        }

        if (string.Equals(speciesId, "shadow_wolf", StringComparison.OrdinalIgnoreCase)
            && nodeId?.StartsWith("lung:", StringComparison.Ordinal) == true)
        {
            return "graft:shadow-wolf-endurance";
        }

        if (string.Equals(speciesId, "moss_boar", StringComparison.OrdinalIgnoreCase)
            && string.Equals(nodeId, "heart", StringComparison.Ordinal))
        {
            return "graft:moss-boar-toughness";
        }

        return string.Empty;
    }

    private static float ResolveSpecialEffectStrength(
        string speciesId,
        string nodeId)
    {
        return string.IsNullOrWhiteSpace(
            ResolveSpecialEffectId(speciesId, nodeId))
                ? 0f
                : 1f;
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
