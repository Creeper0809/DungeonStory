using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using UnityEngine;
using VContainer;

public class FacilitySynthesisRuntime : MonoBehaviour
{
    private readonly List<BuildableObject> selectedMaterials = new List<BuildableObject>();
    private IReadOnlyList<BuildableObject> selectedMaterialsView;
    private GridBuildingFactory buildingFactory;
    private IBlueprintResearchStateService blueprintResearchStateService;
    private IGridTextureProvider gridTextureProvider;
    private IObjectResolver objectResolver;
    private IFacilitySynthesisRecipeQuery recipeQuery;
    private IGridBuildingObjectFactory gridBuildingObjectFactory;
    private IGameEventBus gameEventBus;
    private readonly IProductionFacilityHandleQuery productionFacilityHandles =
        new ProductionFacilityHandleQueryAdapter();

    public IReadOnlyList<BuildableObject> SelectedMaterials =>
        selectedMaterialsView ??= ReadOnlyView.List(selectedMaterials);

    public event Action SelectionChanged;
    public event Action<FacilitySynthesisResult> Completed;

    [Inject]
    public void ConstructFacilitySynthesisRuntime(
        IBlueprintResearchStateService blueprintResearchStateService,
        IGridTextureProvider gridTextureProvider,
        IObjectResolver objectResolver,
        IFacilitySynthesisRecipeQuery recipeQuery,
        IGridBuildingObjectFactory gridBuildingObjectFactory,
        IGameEventBus gameEventBus)
    {
        this.blueprintResearchStateService = blueprintResearchStateService
            ?? throw new ArgumentNullException(nameof(blueprintResearchStateService));
        this.gridTextureProvider = gridTextureProvider
            ?? throw new ArgumentNullException(nameof(gridTextureProvider));
        this.objectResolver = objectResolver
            ?? throw new ArgumentNullException(nameof(objectResolver));
        this.recipeQuery = recipeQuery
            ?? throw new ArgumentNullException(nameof(recipeQuery));
        this.gridBuildingObjectFactory = gridBuildingObjectFactory
            ?? throw new ArgumentNullException(nameof(gridBuildingObjectFactory));
        this.gameEventBus = gameEventBus
            ?? throw new ArgumentNullException(nameof(gameEventBus));
        buildingFactory = null;
    }

    public BlueprintResearchState ResearchState
    {
        get { return ResolveResearchStateService().GetState(); }
    }

    public IReadOnlyList<FacilitySynthesisRecipeSO> VisibleRecipes => ResolveRecipeQuery().GetVisibleRecipes(ResearchState);

    private GridBuildingFactory BuildingFactory => buildingFactory ??= new GridBuildingFactory(
        ResolveGridTextureProvider().Texture,
        InjectCreatedBuilding,
        ResolveGridBuildingObjectFactory());

    public void ToggleMaterialSelection(BuildableObject building)
    {
        if (building == null || building.isDestroy)
        {
            return;
        }

        if (selectedMaterials.Contains(building))
        {
            selectedMaterials.Remove(building);
        }
        else
        {
            selectedMaterials.Add(building);
        }

        SelectionChanged?.Invoke();
    }

    public void ClearSelection()
    {
        selectedMaterials.Clear();
        SelectionChanged?.Invoke();
    }

    public bool TrySynthesizeSelected(FacilitySynthesisRecipeSO recipe, out FacilitySynthesisResult result)
    {
        bool success = TrySynthesize(recipe, selectedMaterials, out result);
        if (success)
        {
            ClearSelection();
        }

        return success;
    }

    public bool TrySynthesizeSelected(string recipeId, out FacilitySynthesisResult result)
    {
        FacilitySynthesisRecipeSO recipe = VisibleRecipes.FirstOrDefault((candidate) => candidate.recipeId == recipeId);
        return TrySynthesizeSelected(recipe, out result);
    }

    public FacilitySynthesisRecipeSnapshot ToSnapshot(FacilitySynthesisRecipeSO recipe)
    {
        return ResolveRecipeQuery().ToSnapshot(recipe, ResearchState);
    }

    public bool TrySynthesize(
        FacilitySynthesisRecipeSO recipe,
        IReadOnlyList<BuildableObject> materials,
        out FacilitySynthesisResult result)
    {
        result = new FacilitySynthesisResult(false, recipe, null, 1, "합성할 수 없습니다");

        if (!Validate(recipe, materials, out string errorMessage))
        {
            result = new FacilitySynthesisResult(false, recipe, null, 1, errorMessage);
            return false;
        }

        BuildableObject[] orderedMaterials = OrderMaterialsByPersistentId(materials);
        BuildableObject primary = ResolveDeclaredAnchor(recipe, orderedMaterials);
        Grid grid = primary.Grid;
        Vector2Int resultPosition = primary.centerPos;
        int inheritedLevel = FacilitySynthesisService.CalculateInheritedLevel(recipe, materials);

        if (!TryReplaceMaterialsAtomically(
                recipe,
                orderedMaterials,
                primary,
                grid,
                resultPosition,
                inheritedLevel,
                out BuildableObject resultBuilding,
                out string replacementFailure))
        {
            result = new FacilitySynthesisResult(
                false,
                recipe,
                null,
                inheritedLevel,
                replacementFailure);
            return false;
        }

        result = new FacilitySynthesisResult(
            true,
            recipe,
            resultBuilding,
            inheritedLevel,
            $"{recipe.DisplayName} 합성 완료");
        Completed?.Invoke(result);
        gameEventBus.Publish(new FacilitySynthesisCompletedEvent(result));
        gameEventBus.RaiseAlert(
            "시설 합성 완료",
            $"{recipe.DisplayName}: {FacilityShopService.GetBuildingName(recipe.resultBuilding)} Lv.{inheritedLevel}",
            EventAlertImportance.Medium,
            "합성");
        return true;
    }

    private bool Validate(
        FacilitySynthesisRecipeSO recipe,
        IReadOnlyList<BuildableObject> materials,
        out string errorMessage)
    {
        if (recipe == null || !recipe.HasValidData)
        {
            errorMessage = "조합식 정보가 올바르지 않습니다";
            return false;
        }

        if (!ResolveRecipeQuery().IsVisible(recipe, ResearchState))
        {
            errorMessage = "아직 해금되지 않은 조합식입니다";
            return false;
        }

        if (materials == null || materials.Count == 0)
        {
            errorMessage = "합성 재료 시설을 선택해야 합니다";
            return false;
        }

        if (materials.Any((building) => building == null || building.isDestroy))
        {
            errorMessage = "사용할 수 없는 재료 시설이 있습니다";
            return false;
        }

        if (materials.Any((building) => building.IsDamaged))
        {
            errorMessage = "파손 시설은 수리 전까지 합성할 수 없습니다";
            return false;
        }

        if (materials.Select((building) => building).Distinct().Count() != materials.Count)
        {
            errorMessage = "같은 시설을 중복 재료로 사용할 수 없습니다";
            return false;
        }

        BuildingInstanceId[] materialIds;
        try
        {
            materialIds = materials
                .Select(building => building.RequirePersistentInstanceId())
                .ToArray();
        }
        catch (InvalidOperationException exception)
        {
            errorMessage = "영속 ID가 없는 시설은 합성할 수 없습니다. " + exception.Message;
            return false;
        }

        if (materialIds.Distinct().Count() != materialIds.Length)
        {
            errorMessage = "같은 영속 ID를 가진 시설은 함께 합성할 수 없습니다";
            return false;
        }

        if (!FacilitySynthesisService.MatchesMaterials(recipe, materials))
        {
            errorMessage = "조합식과 재료 시설이 맞지 않습니다";
            return false;
        }

        BuildableObject anchor = ResolveDeclaredAnchor(recipe, materials);
        Grid grid = anchor.Grid;
        if (grid == null || materials.Any((building) => building.Grid != grid))
        {
            errorMessage = "같은 그리드의 시설만 합성할 수 있습니다";
            return false;
        }

        IBuildingWorldRegistryPort worldRegistry = anchor.WorldRegistry;
        if (worldRegistry == null
            || materials.Any(building => !ReferenceEquals(building.WorldRegistry, worldRegistry)))
        {
            errorMessage = "같은 월드 권위에 등록된 시설만 합성할 수 있습니다";
            return false;
        }

        if (!CanPlaceResultOverMaterials(grid, recipe, materials, anchor.centerPos))
        {
            errorMessage = "결과 시설을 배치할 공간이 부족합니다";
            return false;
        }

        errorMessage = string.Empty;
        return true;
    }

    private static bool CanPlaceResultOverMaterials(
        Grid grid,
        FacilitySynthesisRecipeSO recipe,
        IReadOnlyList<BuildableObject> materials,
        Vector2Int resultPosition)
    {
        HashSet<IGridOccupant> materialSet = materials.Cast<IGridOccupant>().ToHashSet();
        foreach (Vector2Int pos in recipe.resultBuilding.GetGridPosList(resultPosition))
        {
            if (!grid.IsValidGridPos(pos))
            {
                return false;
            }

            GridCell cell = grid.GetGridCell(pos);
            IGridOccupant occupant = cell?.GetOccupant(recipe.resultBuilding.Placement.Layer);
            if (occupant != null && !materialSet.Contains(occupant))
            {
                return false;
            }
        }

        return true;
    }

    private static BuildableObject ResolveDeclaredAnchor(
        FacilitySynthesisRecipeSO recipe,
        IReadOnlyList<BuildableObject> materials)
    {
        int declaredAnchorId = recipe?.materialBuildings?
            .FirstOrDefault(building => building != null)?.id
            ?? 0;
        return materials?.FirstOrDefault(material => material != null && material.id == declaredAnchorId)
            ?? materials?[0];
    }

    private bool TryReplaceMaterialsAtomically(
        FacilitySynthesisRecipeSO recipe,
        IReadOnlyList<BuildableObject> orderedMaterials,
        BuildableObject anchor,
        Grid grid,
        Vector2Int resultPosition,
        int inheritedLevel,
        out BuildableObject resultBuilding,
        out string failureReason)
    {
        resultBuilding = null;
        failureReason = string.Empty;
        IProductionFacilityRetargetTransaction retarget =
            ResolveProductionFacilityRetargetTransaction();
        ProductionFacilityRetargetRequest[] requests = orderedMaterials
            .Select(material => new ProductionFacilityRetargetRequest(
                productionFacilityHandles.CaptureFacility(material),
                ProductionFacilityMutationKind.Synthesis))
            .ToArray();
        string operationId = "synthesis:" + recipe.recipeId + ":"
            + anchor.RequirePersistentInstanceId().Value;
        if (!retarget.TryBegin(
                requests,
                operationId,
                out ProductionFacilityRetargetTransactionState transaction,
                out string beginFailure))
        {
            failureReason = "생산 권위 합성 사전 검증 실패: " + beginFailure;
            return false;
        }

        BuildableObject candidate = null;
        try
        {
            candidate = BuildingFactory.CreateDetached(
                grid,
                recipe.resultBuilding,
                resultPosition);
            if (candidate == null)
            {
                RollbackRetargetOrThrow(retarget, transaction, "candidate-creation");
                failureReason = "결과 시설 후보 생성 실패";
                return false;
            }

            candidate.RestorePersistentIdentity(anchor.RequirePersistentInstanceId());
            candidate.SetGrid(grid);
            candidate.Initialization(recipe.resultBuilding, resultPosition);
            candidate.SetFacilityLevel(inheritedLevel);
        }
        catch (Exception exception)
        {
            DiscardCandidate(candidate, recipe.resultBuilding, resultPosition);
            RollbackRetargetOrThrow(retarget, transaction, "candidate-initialization");
            failureReason = "결과 시설 후보 초기화 실패: " + exception.Message;
            return false;
        }

        List<BuildableObject> removedMaterials = new List<BuildableObject>(orderedMaterials.Count);
        foreach (BuildableObject material in orderedMaterials)
        {
            if (!grid.RemoveOccupant(
                    material,
                    material.BuildingData.Placement.Layer,
                    material.buildPoses,
                    material.BuildingData.Placement.IsMovement))
            {
                DiscardCandidate(candidate, recipe.resultBuilding, resultPosition);
                RestoreMaterialOccupancies(grid, removedMaterials);
                RollbackRetargetOrThrow(retarget, transaction, "occupancy-removal");
                failureReason = "재료 시설 점유를 해제할 수 없습니다: "
                    + material.PersistentInstanceId.Value;
                return false;
            }
            removedMaterials.Add(material);
        }

        if (!grid.RegisterOccupant(
                candidate,
                recipe.resultBuilding.Placement.Layer,
                recipe.resultBuilding.GetGridPosList(resultPosition),
                recipe.resultBuilding.Placement.IsMovement))
        {
            DiscardCandidate(candidate, recipe.resultBuilding, resultPosition);
            RestoreMaterialOccupancies(grid, removedMaterials);
            RollbackRetargetOrThrow(retarget, transaction, "candidate-registration");
            failureReason = "결과 시설 배치 실패";
            return false;
        }

        IBuildingWorldRegistryPort worldRegistry = anchor.WorldRegistry;
        string registryFailure = "building-world-registry-unavailable";
        if (worldRegistry == null
            || !ReferenceEquals(candidate.WorldRegistry, worldRegistry)
            || !worldRegistry.TryReplaceBuilding(
                anchor,
                candidate,
                out registryFailure))
        {
            grid.RemoveOccupant(
                candidate,
                recipe.resultBuilding.Placement.Layer,
                candidate.buildPoses,
                recipe.resultBuilding.Placement.IsMovement);
            DiscardCandidate(candidate, recipe.resultBuilding, resultPosition);
            RestoreMaterialOccupancies(grid, removedMaterials);
            RollbackRetargetOrThrow(retarget, transaction, "world-registry-handoff");
            failureReason = "결과 시설 월드 권위 교체 실패: "
                + (registryFailure ?? "building-world-registry-unavailable");
            return false;
        }

        bool authorityCommitted;
        string commitFailure;
        try
        {
            ProductionFacilityHandle targetFacility =
                productionFacilityHandles.CaptureFacility(candidate);
            ProductionFacilityRetargetBinding[] bindings = requests
                .Select(request => new ProductionFacilityRetargetBinding(
                    request.SourceFacilityId,
                    targetFacility))
                .ToArray();
            authorityCommitted = retarget.TryCommit(
                transaction,
                bindings,
                out commitFailure);
        }
        catch (Exception exception)
        {
            authorityCommitted = false;
            commitFailure = "target-capture-or-commit-exception:"
                + exception.GetType().Name + ":" + exception.Message;
        }
        if (!authorityCommitted)
        {
            RollbackRetargetOrThrow(retarget, transaction, "authority-commit");
            if (!worldRegistry.TryRollbackBuildingReplacement(
                    candidate,
                    anchor,
                    out string registryRollbackFailure))
            {
                throw new InvalidOperationException(
                    "Synthesis authority commit failed and world authority rollback also failed: "
                    + registryRollbackFailure);
            }
            grid.RemoveOccupant(
                candidate,
                recipe.resultBuilding.Placement.Layer,
                candidate.buildPoses,
                recipe.resultBuilding.Placement.IsMovement);
            DiscardCandidate(candidate, recipe.resultBuilding, resultPosition);
            RestoreMaterialOccupancies(grid, removedMaterials);
            failureReason = "생산 권위 합성 반영 실패로 재료 시설을 복구했습니다: "
                + commitFailure;
            return false;
        }

        try
        {
            BuildingFactory.PublishDetached(candidate, recipe.resultBuilding, resultPosition);
        }
        catch (Exception exception)
        {
            RollbackRetargetOrThrow(retarget, transaction, "publication");
            if (!worldRegistry.TryRollbackBuildingReplacement(
                    candidate,
                    anchor,
                    out string rollbackFailure))
            {
                throw new InvalidOperationException(
                    "Synthesis publication failed and world authority rollback also failed: "
                    + rollbackFailure,
                    exception);
            }

            grid.RemoveOccupant(
                candidate,
                recipe.resultBuilding.Placement.Layer,
                candidate.buildPoses,
                recipe.resultBuilding.Placement.IsMovement);
            DiscardCandidate(candidate, recipe.resultBuilding, resultPosition);
            RestoreMaterialOccupancies(grid, removedMaterials);
            failureReason = "결과 시설 게시 실패로 재료 시설을 복구했습니다: "
                + exception.Message;
            return false;
        }

        if (!retarget.TryComplete(transaction, out string completionFailure))
        {
            RollbackRetargetOrThrow(retarget, transaction, "completion");
            if (!worldRegistry.TryRollbackBuildingReplacement(
                    candidate,
                    anchor,
                    out string registryRollbackFailure))
            {
                throw new InvalidOperationException(
                    "Synthesis authority completion failed and world authority rollback also failed: "
                    + registryRollbackFailure);
            }
            grid.RemoveOccupant(
                candidate,
                recipe.resultBuilding.Placement.Layer,
                candidate.buildPoses,
                recipe.resultBuilding.Placement.IsMovement);
            DiscardCandidate(candidate, recipe.resultBuilding, resultPosition);
            RestoreMaterialOccupancies(grid, removedMaterials);
            failureReason = "생산 권위 합성 완료 검증 실패로 재료 시설을 복구했습니다: "
                + completionFailure;
            return false;
        }

        foreach (BuildableObject material in orderedMaterials)
        {
            BuildingFactory.DeleteVisual(material.BuildingData, material.centerPos);
            material.SetGrid(null);
            material.RetireForWorldReplacement();
        }

        resultBuilding = candidate;
        return true;
    }

    private static void RollbackRetargetOrThrow(
        IProductionFacilityRetargetTransaction retarget,
        ProductionFacilityRetargetTransactionState transaction,
        string phase)
    {
        if (transaction == null
            || transaction.Phase is ProductionFacilityRetargetTransactionPhase
                .RolledBack or ProductionFacilityRetargetTransactionPhase.Completed)
        {
            return;
        }

        if (!retarget.TryRollback(transaction, out string failureReason))
        {
            throw new InvalidOperationException(
                "Facility synthesis production retarget rollback failed during "
                + phase + ":" + failureReason);
        }
    }

    private void DiscardCandidate(
        BuildableObject candidate,
        BuildingSO resultBuilding,
        Vector2Int resultPosition)
    {
        if (candidate == null)
        {
            return;
        }

        if (candidate.IsDetachedRestoreCandidate)
        {
            candidate.SetGrid(null);
            BuildingFactory.DiscardDetached(candidate);
        }
        else
        {
            BuildingFactory.DeleteVisual(resultBuilding, resultPosition);
            candidate.SetGrid(null);
            candidate.RetireForWorldReplacement();
        }
    }

    private static void RestoreMaterialOccupancies(
        Grid grid,
        IReadOnlyList<BuildableObject> removedMaterials)
    {
        for (int index = removedMaterials.Count - 1; index >= 0; index--)
        {
            BuildableObject material = removedMaterials[index];
            if (!grid.RegisterOccupant(
                    material,
                    material.BuildingData.Placement.Layer,
                    material.buildPoses,
                    material.BuildingData.Placement.IsMovement))
            {
                throw new InvalidOperationException(
                    "Facility synthesis rollback could not restore material occupancy: "
                    + material.PersistentInstanceId.Value);
            }
        }
    }

    private static BuildableObject[] OrderMaterialsByPersistentId(
        IReadOnlyList<BuildableObject> materials)
    {
        return materials
            .OrderBy(value => value.RequirePersistentInstanceId().Value, StringComparer.Ordinal)
            .ToArray();
    }

    private IBlueprintResearchStateService ResolveResearchStateService()
    {
        return blueprintResearchStateService
            ?? throw new InvalidOperationException($"{nameof(FacilitySynthesisRuntime)} requires {nameof(IBlueprintResearchStateService)} injection.");
    }

    private IGridTextureProvider ResolveGridTextureProvider()
    {
        return gridTextureProvider
            ?? throw new InvalidOperationException($"{nameof(FacilitySynthesisRuntime)} requires {nameof(IGridTextureProvider)} injection.");
    }

    private void InjectCreatedBuilding(BuildableObject building)
    {
        if (building == null)
        {
            return;
        }

        ResolveObjectResolver().Inject(building);
    }

    private IObjectResolver ResolveObjectResolver()
    {
        return objectResolver
            ?? throw new InvalidOperationException($"{nameof(FacilitySynthesisRuntime)} requires {nameof(IObjectResolver)} injection.");
    }

    private IGridBuildingObjectFactory ResolveGridBuildingObjectFactory()
    {
        return gridBuildingObjectFactory
            ?? throw new InvalidOperationException($"{nameof(FacilitySynthesisRuntime)} requires {nameof(IGridBuildingObjectFactory)} injection.");
    }

    private IFacilitySynthesisRecipeQuery ResolveRecipeQuery()
    {
        return recipeQuery
            ?? throw new InvalidOperationException($"{nameof(FacilitySynthesisRuntime)} requires {nameof(IFacilitySynthesisRecipeQuery)} injection.");
    }

    private IProductionFacilityRetargetTransaction ResolveProductionFacilityRetargetTransaction()
    {
        if (ResolveObjectResolver().TryResolve(
                typeof(IProductionFacilityRetargetTransaction),
                out object resolved)
            && resolved is IProductionFacilityRetargetTransaction transaction)
        {
            return transaction;
        }
        throw new InvalidOperationException(
            $"{nameof(FacilitySynthesisRuntime)} requires "
            + $"{nameof(IProductionFacilityRetargetTransaction)}.");
    }

    private IProductionFacilityMutationFence ResolveProductionFacilityMutationFence()
    {
        if (ResolveObjectResolver().TryResolve(
                typeof(IProductionFacilityMutationFence),
                out object resolved)
            && resolved is IProductionFacilityMutationFence fence)
        {
            return fence;
        }
        throw new InvalidOperationException(
            $"{nameof(FacilitySynthesisRuntime)} requires "
            + $"{nameof(IProductionFacilityMutationFence)}.");
    }
}
