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

        BuildableObject primary = ResolveDeclaredAnchor(recipe, materials);
        Grid grid = primary.Grid;
        Vector2Int resultPosition = primary.centerPos;
        int inheritedLevel = FacilitySynthesisService.CalculateInheritedLevel(recipe, materials);

        foreach (BuildableObject material in materials)
        {
            RemoveMaterialFromGrid(material);
        }

        BuildableObject resultBuilding = BuildingFactory.Create(grid, recipe.resultBuilding, resultPosition);
        if (resultBuilding == null)
        {
            result = new FacilitySynthesisResult(false, recipe, null, inheritedLevel, "결과 시설 생성 실패");
            return false;
        }

        resultBuilding.SetGrid(grid);
        resultBuilding.Initialization(recipe.resultBuilding, resultPosition);
        resultBuilding.SetFacilityLevel(inheritedLevel);
        bool registered = grid.RegisterOccupant(
            resultBuilding,
            recipe.resultBuilding.Placement.Layer,
            recipe.resultBuilding.GetGridPosList(resultPosition),
            recipe.resultBuilding.Placement.IsMovement);
        if (!registered)
        {
            resultBuilding.DestroySelf();
            result = new FacilitySynthesisResult(false, recipe, null, inheritedLevel, "결과 시설 배치 실패");
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

        if (!CanPlaceResultOverMaterials(grid, recipe, materials, anchor.centerPos))
        {
            errorMessage = "결과 시설을 배치할 공간이 부족합니다";
            return false;
        }

        IProductionFacilityMutationFence productionFence =
            ResolveProductionFacilityMutationFence();
        foreach (BuildableObject material in materials
                     .OrderBy(value => value.PersistentInstanceId.Value, StringComparer.Ordinal))
        {
            if (!productionFence.TryRequireNoAuthority(
                    material,
                    ProductionFacilityMutationKind.Synthesis,
                    out string productionFailure))
            {
                errorMessage = "생산 주문·재공품·출력 권위가 남은 시설은 합성할 수 없습니다. "
                    + productionFailure;
                return false;
            }
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

    private void RemoveMaterialFromGrid(BuildableObject material)
    {
        if (material == null || material.BuildingData == null || material.Grid == null)
        {
            return;
        }

        material.Grid.RemoveOccupant(
            material,
            material.BuildingData.Placement.Layer,
            material.buildPoses,
            material.BuildingData.Placement.IsMovement);
        BuildingFactory.DeleteVisual(material.BuildingData, material.centerPos);
        material.DestroySelf();
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
