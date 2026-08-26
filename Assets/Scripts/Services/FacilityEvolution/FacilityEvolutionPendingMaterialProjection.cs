using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using VContainer.Unity;

/// <summary>
/// Resumes the exact facility-evolution result that was persisted after its
/// physical material debit. The projection never rebuilds proposals, mutation
/// choices, record-token outcomes, or material selections.
/// </summary>
public sealed class FacilityEvolutionPendingMaterialProjection :
    IInitializable,
    ITickable
{
    private readonly IBuildingWorldQuery buildings;
    private readonly IFacilityCandidateCache facilityStateVersions;
    private readonly FacilityFeatureSceneRuntimeReferences runtimeReferences;
    private readonly HashSet<string> reportedFailures =
        new HashSet<string>(StringComparer.Ordinal);

    private int observedBuildingVersion = -1;
    private int observedFacilityStateVersion = -1;
    private bool reconciling;

    public FacilityEvolutionPendingMaterialProjection(
        IBuildingWorldQuery buildings,
        IFacilityCandidateCache facilityStateVersions,
        FacilityFeatureSceneRuntimeReferences runtimeReferences)
    {
        this.buildings = buildings
            ?? throw new ArgumentNullException(nameof(buildings));
        this.facilityStateVersions = facilityStateVersions
            ?? throw new ArgumentNullException(nameof(facilityStateVersions));
        this.runtimeReferences = runtimeReferences
            ?? throw new ArgumentNullException(nameof(runtimeReferences));
    }

    public void Initialize() => Reconcile(force: true);

    public void Tick() => Reconcile(force: false);

    private void Reconcile(bool force)
    {
        if (reconciling)
        {
            return;
        }

        int buildingVersion = buildings.BuildingVersion;
        int facilityStateVersion = facilityStateVersions.DynamicStateVersion;
        if (!force
            && observedBuildingVersion == buildingVersion
            && observedFacilityStateVersion == facilityStateVersion)
        {
            return;
        }

        IReadOnlyList<BuildableObject> current =
            buildings.Buildings ?? Array.Empty<BuildableObject>();
        BuildableObject[] pass = new BuildableObject[current.Count];
        for (int index = 0; index < current.Count; index++)
        {
            pass[index] = current[index];
        }

        reconciling = true;
        try
        {
            foreach (BuildableObject building in pass)
            {
                if (building == null
                    || building.isDestroy
                    || !building.TryGetComponent(
                        out FacilityEvolutionStateComponent state)
                    || !state.HasPendingMaterialCommit)
                {
                    continue;
                }

                FacilityEvolutionPendingMaterialCommitSnapshot pending =
                    state.PendingMaterialCommit;
                FacilityEvolutionRuntime evolution = runtimeReferences.Evolution;
                string failureReason = string.Empty;
                if (evolution != null
                    && evolution.TryReconcilePendingMaterialEvolution(
                        building,
                        out _,
                        out failureReason))
                {
                    reportedFailures.Remove(pending.operationId);
                    continue;
                }

                if (reportedFailures.Add(pending.operationId))
                {
                    Debug.LogError(
                        "Facility evolution pending material reconciliation failed for '"
                        + pending.operationId
                        + "': "
                        + (evolution == null
                            ? "scene FacilityEvolutionRuntime is missing"
                            : failureReason));
                }
            }
        }
        finally
        {
            reconciling = false;
            observedBuildingVersion = buildings.BuildingVersion;
            observedFacilityStateVersion =
                facilityStateVersions.DynamicStateVersion;
        }
    }
}

/// <summary>
/// Validates the cross-section join between published facility state and the
/// published Physical Items pending receipt before the restore transaction can
/// complete. It is intentionally read-only so a later participant failure can
/// still roll the whole restore back without compensating gameplay mutations.
/// </summary>
public sealed class FacilityEvolutionPendingMaterialRestoreGuard :
    IDungeonRestoreTransactionParticipant
{
    private const string RestoreParticipantId =
        "224.world.facility-evolution-materials";

    private readonly IBuildingWorldQuery buildings;
    private readonly IFacilityEvolutionResourceProvider resources;
    private readonly IFacilityEvolutionRecipeQuery recipes;
    private readonly IPhysicalItemRestoreCandidateQuery physicalCandidates;
    private bool active;
    private bool published;

    public FacilityEvolutionPendingMaterialRestoreGuard(
        IBuildingWorldQuery buildings,
        IFacilityEvolutionResourceProvider resources,
        IFacilityEvolutionRecipeQuery recipes)
    {
        this.buildings = buildings
            ?? throw new ArgumentNullException(nameof(buildings));
        this.resources = resources
            ?? throw new ArgumentNullException(nameof(resources));
        this.recipes = recipes
            ?? throw new ArgumentNullException(nameof(recipes));
        physicalCandidates = null;
    }

    [VContainer.Inject]
    public FacilityEvolutionPendingMaterialRestoreGuard(
        IBuildingWorldQuery buildings,
        IFacilityEvolutionResourceProvider resources,
        IFacilityEvolutionRecipeQuery recipes,
        IPhysicalItemRestoreCandidateQuery physicalCandidates)
    {
        this.buildings = buildings
            ?? throw new ArgumentNullException(nameof(buildings));
        this.resources = resources
            ?? throw new ArgumentNullException(nameof(resources));
        this.recipes = recipes
            ?? throw new ArgumentNullException(nameof(recipes));
        this.physicalCandidates = physicalCandidates
            ?? throw new ArgumentNullException(nameof(physicalCandidates));
    }

    public string ParticipantId => RestoreParticipantId;

    public void BeginRestoreCandidate()
    {
        if (active)
        {
            throw new InvalidOperationException(
                "Facility evolution material restore validation is already active.");
        }
        active = true;
        published = false;
    }

    public void PublishRestoreCandidate()
    {
        if (!active || published)
        {
            throw new InvalidOperationException(
                "Facility evolution material restore validation is not ready to publish.");
        }

        HashSet<string> operations = new HashSet<string>(StringComparer.Ordinal);
        IReadOnlyList<BuildableObject> current =
            buildings.Buildings ?? Array.Empty<BuildableObject>();
        foreach (BuildableObject building in current)
        {
            if (building == null
                || building.isDestroy
                || !building.TryGetComponent(
                    out FacilityEvolutionStateComponent state)
                || !state.HasPendingMaterialCommit)
            {
                continue;
            }

            FacilityEvolutionStateSnapshot snapshot = state.CreateSnapshot();
            FacilityEvolutionAggregateAdapter.ValidatePendingMaterialCommit(snapshot);
            FacilityEvolutionPendingMaterialCommitSnapshot pending =
                snapshot.pendingMaterialCommit;
            if (!operations.Add(pending.operationId))
            {
                throw new InvalidOperationException(
                    "Duplicate facility evolution pending material operation: "
                    + pending.operationId);
            }

            FacilityEvolutionRecipeSO recipe = null;
            foreach (FacilityEvolutionRecipeSO candidate in recipes.GetRecipes())
            {
                if (candidate == null
                    || !string.Equals(
                        candidate.EffectiveId,
                        pending.recipeId,
                        StringComparison.Ordinal))
                {
                    continue;
                }
                if (recipe != null)
                {
                    throw new InvalidOperationException(
                        "Duplicate facility evolution recipe identity: "
                        + pending.recipeId);
                }
                recipe = candidate;
            }
            FacilityEvolutionStateSnapshot resolved =
                pending.ReadResolvedResultState();
            bool exactRecipe = recipe != null
                && recipe.resultBuilding != null
                && string.Equals(
                    FacilityEvolutionUtility.GetFacilityId(recipe.resultBuilding),
                    pending.resultFacilityDefinitionId,
                    StringComparison.Ordinal)
                && (recipe.fromFacilities ?? Array.Empty<BuildingSO>()).Any(
                    source => source != null
                        && string.Equals(
                            FacilityEvolutionUtility.GetFacilityId(source),
                            pending.sourceFacilityDefinitionId,
                            StringComparison.Ordinal))
                && resolved.starGrade == Mathf.Max(1, recipe.resultStarGrade)
                && (pending.resolvedMutationTags ?? Array.Empty<string>()).All(
                    tag => recipe.allowedMutationTags != null
                        && recipe.allowedMutationTags.Contains(tag));
            if (!exactRecipe)
            {
                throw new InvalidOperationException(
                    "Facility evolution pending authored recipe authority does not match '"
                    + pending.operationId
                    + "'.");
            }

            if (!resources.TryGetPendingMaterialCommit(
                    pending.operationId,
                    pending.reasonCode,
                    out FacilityEvolutionMaterialCommitReceipt receipt,
                    out string failureReason)
                || !FacilityEvolutionMaterialCommitAuthority.Matches(
                    pending,
                    receipt))
            {
                throw new InvalidOperationException(
                    "Facility evolution pending material restore join failed for '"
                    + pending.operationId
                    + "': "
                    + (string.IsNullOrWhiteSpace(failureReason)
                        ? "physical receipt mismatch"
                        : failureReason));
            }
        }

        ValidateModificationMaterialJoins(current, physicalCandidates);
        ValidateRelocationPackageJoins(current, physicalCandidates);
        ValidateRecalibrationMaterialJoins(current, physicalCandidates);

        published = true;
    }

    public static void ValidateRelocationPackageJoins(
        IReadOnlyList<BuildableObject> buildings,
        IPhysicalItemRestoreCandidateQuery query)
    {
        List<FacilityRelocationOrder> orders=new();
        foreach(BuildableObject building in buildings??Array.Empty<BuildableObject>())
        {
            if(building==null||building.isDestroy||!building.TryGetComponent(out FacilityEvolutionStateComponent component))continue;
            FacilityRelocationOrder order=component.InstanceEvolution?.relocationOrder;
            if(order!=null&&!string.IsNullOrEmpty(order.packageTransferOperationId))orders.Add(order);
        }
        ValidateRelocationPackageOwnerSet(orders,query);
    }

    public static void ValidateModificationMaterialJoins(
        IReadOnlyList<BuildableObject> buildings,
        IPhysicalItemRestoreCandidateQuery query)
    {
        List<FacilityModificationOrder> orders = new();
        foreach (BuildableObject building in
                 buildings ?? Array.Empty<BuildableObject>())
        {
            if (building == null
                || building.isDestroy
                || !building.TryGetComponent(
                    out FacilityEvolutionStateComponent component))
            {
                continue;
            }

            FacilityModificationOrder order =
                component.InstanceEvolution?.modificationOrder;
            if (order != null
                && !string.IsNullOrEmpty(order.materialTransferOperationId))
            {
                orders.Add(order);
            }
        }

        ValidateModificationMaterialOwnerSet(orders, query);
    }

    public static void ValidateModificationMaterialOwnerSet(
        IReadOnlyList<FacilityModificationOrder> orders,
        IPhysicalItemRestoreCandidateQuery query)
    {
        const string operationPrefix = "facility-modification-material:";
        Dictionary<string, FacilityModificationOrder> owners =
            new(StringComparer.Ordinal);
        foreach (FacilityModificationOrder order in
                 orders ?? Array.Empty<FacilityModificationOrder>())
        {
            if (order == null
                || string.IsNullOrEmpty(order.materialTransferOperationId))
            {
                continue;
            }

            FacilityEvolutionAggregateAdapter
                .ValidateModificationMaterialTransfer(order);
            if (!owners.TryAdd(order.materialTransferOperationId, order))
            {
                throw new InvalidOperationException(
                    "Duplicate facility modification material operation: "
                    + order.materialTransferOperationId);
            }
        }

        if (query == null || !query.IsCandidateAvailable)
        {
            if (owners.Count == 0)
            {
                return;
            }

            throw new InvalidOperationException(
                "Facility modification restore requires the incoming physical candidate.");
        }

        foreach (FacilityModificationOrder order in owners.Values)
        {
            string[] sourceStackIds = order.materialTransferInputs
                .Select(input => input.sourceStackId)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();
            int quantity = checked(order.materialTransferInputs.Sum(
                input => input.quantity));
            if (!query.TryGetPendingBatchDisposition(
                    order.materialTransferOperationId,
                    out PhysicalItemRestoreCandidateDispositionSnapshot receipt)
                || receipt.Kind != PhysicalItemDispositionKind.Transfer
                || !string.Equals(
                    receipt.ReasonCode,
                    FacilityModificationMaterialOutbox.ReasonCode,
                    StringComparison.Ordinal)
                || !string.Equals(
                    receipt.RequestFingerprint,
                    order.materialTransferRequestFingerprint,
                    StringComparison.Ordinal)
                || !string.Equals(
                    receipt.CommitId,
                    order.materialTransferCommitId,
                    StringComparison.Ordinal)
                || receipt.Quantity != quantity
                || receipt.InputMassGrams != order.materialTransferMassGrams
                || !receipt.SourceStackIds.SequenceEqual(
                    sourceStackIds,
                    StringComparer.Ordinal))
            {
                throw new InvalidOperationException(
                    "Facility modification has no exact incoming material Transfer receipt: "
                    + order.materialTransferOperationId);
            }
        }

        foreach (PhysicalItemRestoreCandidateDispositionSnapshot receipt in
                 query.PendingBatchDispositions)
        {
            if (receipt?.OperationId == null
                || !receipt.OperationId.StartsWith(
                    operationPrefix,
                    StringComparison.Ordinal))
            {
                continue;
            }

            if (!owners.ContainsKey(receipt.OperationId))
            {
                throw new InvalidOperationException(
                    "Incoming facility modification material Transfer has no facility owner: "
                    + receipt.OperationId);
            }
        }
    }

    public static void ValidateRelocationPackageOwnerSet(
        IReadOnlyList<FacilityRelocationOrder> orders,
        IPhysicalItemRestoreCandidateQuery query)
    {
        const string prefix="facility-relocation-package:";
        Dictionary<string,FacilityRelocationOrder> owners=new(StringComparer.Ordinal);
        foreach(FacilityRelocationOrder order in orders??Array.Empty<FacilityRelocationOrder>())
        {
            if(order==null||string.IsNullOrEmpty(order.packageTransferOperationId))continue;
            FacilityEvolutionAggregateAdapter.ValidateRelocationPackageTransfer(order);
            if(!owners.TryAdd(order.packageTransferOperationId,order))throw new InvalidOperationException("Duplicate relocation package operation: "+order.packageTransferOperationId);
        }
        if(query==null)
        {
            if(owners.Count==0)return;
            throw new InvalidOperationException("Facility relocation restore requires the incoming physical candidate.");
        }
        if(!query.IsCandidateAvailable)throw new InvalidOperationException("Facility relocation restore requires the incoming physical candidate.");
        foreach(FacilityRelocationOrder order in owners.Values)
        {
            if(!query.TryGetPendingBatchDisposition(order.packageTransferOperationId,out PhysicalItemRestoreCandidateDispositionSnapshot receipt)
                ||receipt.Kind!=PhysicalItemDispositionKind.Transfer
                ||!string.Equals(receipt.ReasonCode,FacilityRelocationPackageOutbox.ReasonCode,StringComparison.Ordinal)
                ||!string.Equals(receipt.CommitId,order.packageTransferCommitId,StringComparison.Ordinal)
                ||receipt.Quantity!=1||receipt.InputMassGrams!=order.packageTransferMassGrams
                ||receipt.SourceStackIds.Count!=1||!string.Equals(receipt.SourceStackIds[0],order.packageStackId,StringComparison.Ordinal))
                throw new InvalidOperationException("Relocation package has no exact incoming Transfer receipt: "+order.packageTransferOperationId);
        }
        foreach(PhysicalItemRestoreCandidateDispositionSnapshot receipt in query.PendingBatchDispositions)
        {
            if(receipt?.OperationId==null||!receipt.OperationId.StartsWith(prefix,StringComparison.Ordinal))continue;
            if(!owners.ContainsKey(receipt.OperationId))throw new InvalidOperationException("Incoming relocation package Transfer has no facility owner: "+receipt.OperationId);
        }
    }

    public static void ValidateRecalibrationMaterialJoins(
        IReadOnlyList<BuildableObject> buildings,
        IPhysicalItemRestoreCandidateQuery query)
    {
        List<FacilityRecalibrationOrder> orders = new();
        foreach (BuildableObject building in
                 buildings ?? Array.Empty<BuildableObject>())
        {
            if (building == null
                || building.isDestroy
                || !building.TryGetComponent(
                    out FacilityEvolutionStateComponent component))
            {
                continue;
            }

            FacilityRecalibrationOrder order =
                component.InstanceEvolution?.recalibrationOrder;
            if (order != null
                && !string.IsNullOrEmpty(order.materialTransferOperationId))
            {
                orders.Add(order);
            }
        }

        ValidateRecalibrationMaterialOwnerSet(orders, query);
    }

    public static void ValidateRecalibrationMaterialOwnerSet(
        IReadOnlyList<FacilityRecalibrationOrder> orders,
        IPhysicalItemRestoreCandidateQuery query)
    {
        const string operationPrefix = "facility-recalibration-material:";
        Dictionary<string, FacilityRecalibrationOrder> owners =
            new(StringComparer.Ordinal);

        foreach (FacilityRecalibrationOrder order in
                 orders ?? Array.Empty<FacilityRecalibrationOrder>())
        {
            if (order == null
                || string.IsNullOrEmpty(order.materialTransferOperationId))
            {
                continue;
            }

            FacilityEvolutionAggregateAdapter
                .ValidateRecalibrationMaterialTransfer(order);
            if (!owners.TryAdd(order.materialTransferOperationId, order))
            {
                throw new InvalidOperationException(
                    "Duplicate facility recalibration material operation: "
                    + order.materialTransferOperationId);
            }
        }

        if (query == null || !query.IsCandidateAvailable)
        {
            if (owners.Count == 0)
            {
                return;
            }

            throw new InvalidOperationException(
                "Facility recalibration restore requires the incoming physical candidate.");
        }

        foreach (FacilityRecalibrationOrder order in owners.Values)
        {
            if (!query.TryGetPendingBatchDisposition(
                    order.materialTransferOperationId,
                    out PhysicalItemRestoreCandidateDispositionSnapshot receipt)
                || receipt.Kind != PhysicalItemDispositionKind.Transfer
                || !string.Equals(
                    receipt.ReasonCode,
                    FacilityRecalibrationMaterialOutbox.ReasonCode,
                    StringComparison.Ordinal)
                || !string.Equals(
                    receipt.CommitId,
                    order.materialTransferCommitId,
                    StringComparison.Ordinal)
                || receipt.Quantity != 1
                || receipt.InputMassGrams != order.materialTransferMassGrams
                || receipt.SourceStackIds.Count != 1
                || !string.Equals(
                    receipt.SourceStackIds[0],
                    order.materialTransferSourceStackId,
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "Facility recalibration material has no exact incoming Transfer receipt: "
                    + order.materialTransferOperationId);
            }
        }

        foreach (PhysicalItemRestoreCandidateDispositionSnapshot receipt in
                 query.PendingBatchDispositions)
        {
            if (receipt?.OperationId == null
                || !receipt.OperationId.StartsWith(
                    operationPrefix,
                    StringComparison.Ordinal))
            {
                continue;
            }

            if (!owners.ContainsKey(receipt.OperationId))
            {
                throw new InvalidOperationException(
                    "Incoming facility recalibration material Transfer has no facility owner: "
                    + receipt.OperationId);
            }
        }
    }

    public void RollbackPublishedRestoreCandidate() => Reset();

    public void CompleteRestoreCandidate() => Reset();

    public void DiscardRestoreCandidate() => Reset();

    private void Reset()
    {
        active = false;
        published = false;
    }
}
