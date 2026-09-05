using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using UnityEngine;
using VContainer.Unity;

public sealed class SurgicalPartRuntime :
    ISurgicalPartRuntime,
    ISurgicalPartPreparedOutputRuntime,
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

    private readonly IWorldItemStackRuntime items;
    private readonly IBuildingWorldQuery buildings;
    private readonly ISurgicalFacilityQuery facilities;
    private readonly IAnatomyProfileCatalog anatomyProfiles;
    private readonly IGameClock clock;
    private readonly SurgeryAggregateStateStore stateStore;
    private readonly IPhysicalItemBatchDispositionService batchDispositions;
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
        IBuildingWorldQuery buildings,
        ISurgicalFacilityQuery facilities,
        IAnatomyProfileCatalog anatomyProfiles,
        IGameClock clock,
        SurgeryAggregateStateStore stateStore,
        IPhysicalItemBatchDispositionService batchDispositions,
        IItemDefinitionCatalog itemCatalog,
        IPhysicalItemMassQuery physicalMass,
        IFacilityBufferDestinationClaimAuthorityQuery destinationClaims,
        IFacilityBufferMassCapacityAuthorityQuery destinationCapacities,
        IFacilityBufferDestinationLifecycleCommand destinationLifecycle,
        IFacilityBufferDestinationReleaseService destinationReleases)
    {
        this.items = items ?? throw new ArgumentNullException(nameof(items));
        this.buildings = buildings ?? throw new ArgumentNullException(nameof(buildings));
        this.facilities = facilities ?? throw new ArgumentNullException(nameof(facilities));
        this.anatomyProfiles = anatomyProfiles
            ?? throw new ArgumentNullException(nameof(anatomyProfiles));
        this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
        this.stateStore = stateStore
            ?? throw new ArgumentNullException(nameof(stateStore));
        this.batchDispositions = batchDispositions
            ?? throw new ArgumentNullException(nameof(batchDispositions));
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

        part = new SurgicalPartInstance
        {
            partInstanceId = partInstanceId,
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
                || !string.Equals(existing.nodeId, nodeId, StringComparison.Ordinal))
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

    public bool TryConsumeForInstallation(
        string partInstanceId,
        string orderId,
        string subjectId,
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
            if (!SurgicalPartInstallationOutbox.TryFinalizePending(
                    part,
                    batchDispositions,
                    out string replayFailure))
            {
                failure = new DomainFailure(
                    FailureCode.SurgeryPartUnavailable,
                    partInstanceId ?? string.Empty,
                    replayFailure);
                return false;
            }
            return true;
        }
        if (!string.Equals(part.reservedOrderId, orderId, StringComparison.Ordinal))
        {
            failure = new DomainFailure(
                FailureCode.SurgeryPartUnavailable,
                partInstanceId ?? string.Empty);
            return false;
        }

        bool createdIntent = string.IsNullOrEmpty(part.installationOperationId);
        if (createdIntent)
        {
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

    private static void ClearInstallationIntent(SurgicalPartInstance part)
    {
        part.installationOrderId = string.Empty;
        part.installationOperationId = string.Empty;
        part.installationCommitId = string.Empty;
        part.installationSourceStackId = string.Empty;
        part.installationSubjectId = string.Empty;
    }

    public void TickFreshness(float deltaTime)
    {
        if (deltaTime <= 0f || parts.Count == 0)
        {
            return;
        }

        IReadOnlyList<WorldItemStackSnapshot> stacks = items.GetAllStacks();
        Dictionary<string, WorldItemStackSnapshot> byStack = stacks
            .Where(stack => stack != null)
            .ToDictionary(stack => stack.StackId, StringComparer.Ordinal);
        List<SurgicalPartInstance> expired = null;
        foreach (SurgicalPartInstance part in parts)
        {
            if (part == null
                || part.installed
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
            bool preserved = inWorkingStorage
                && TryEnsurePreservationCanister(part, stack, storageId);
            part.storedFacilityId = preserved ? storageId : string.Empty;
            part.freshnessSeconds -= deltaTime
                * (preserved ? StoredFreshnessRate : 1f);
            if (part.freshnessSeconds <= 0f)
            {
                expired ??= new List<SurgicalPartInstance>();
                expired.Add(part);
            }
        }

        foreach (SurgicalPartInstance part in expired
                     ?? Enumerable.Empty<SurgicalPartInstance>())
        {
            Vector2Int position = default;
            WorldItemStackSnapshot stack = items.GetAllStacks().FirstOrDefault(
                candidate => candidate != null
                    && candidate.StackId == part.worldStackId);
            if (stack != null)
            {
                position = stack.Position;
                items.DeleteStack(stack.StackId);
                items.SpawnItemAt(
                    SurgeryItemDefinitions.ContaminatedTissueId,
                    1,
                    position,
                    WorldItemStackState.Loose,
                    string.Empty,
                    out _);
            }

            parts.Remove(part);
        }
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
        if (stack == null || string.IsNullOrWhiteSpace(stack.DestinationId))
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
