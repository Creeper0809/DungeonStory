using System;
using System.Collections.Generic;
using System.Linq;

public interface IFacilityShopCatalog
{
    IReadOnlyCollection<BuildingSO> Buildings { get; }
    IReadOnlyCollection<FacilityBlueprintSO> Blueprints { get; }
    BuildingSO FindBuildingById(int buildingId);
}

public interface IFacilityShopUnlockStateService
{
    FacilityShopUnlockState GetUnlockState();
}

public sealed class FacilityShopUnlockStateService : IFacilityShopUnlockStateService
{
    private readonly DailyFacilityShopRuntime runtime;

    public FacilityShopUnlockStateService(
        ProgressionSceneRuntimeReferences runtimeReferences)
    {
        runtime = (runtimeReferences
                ?? throw new ArgumentNullException(nameof(runtimeReferences)))
            .FacilityShop
            ?? throw new InvalidOperationException(
                $"{nameof(FacilityShopUnlockStateService)} requires a loaded {nameof(DailyFacilityShopRuntime)}.");
    }

    public FacilityShopUnlockState GetUnlockState()
    {
        return runtime.UnlockState;
    }
}

public sealed class DataCatalogFacilityShopCatalog :
    IFacilityShopCatalog,
    IFacilityShopDefinitionCatalog
{
    private readonly IDataCatalog catalog;

    public DataCatalogFacilityShopCatalog(IDataCatalog catalog)
    {
        this.catalog = catalog
            ?? throw new ArgumentNullException(nameof(catalog));
    }

    public IReadOnlyCollection<BuildingSO> Buildings => catalog
        .GetData<BuildingSO>()
        .Values
        .Where((building) => building != null)
        .ToArray();

    public IReadOnlyCollection<FacilityBlueprintSO> Blueprints => catalog
        .GetData<FacilityBlueprintSO>()
        .Values
        .Where((blueprint) => blueprint != null)
        .ToArray();

    IReadOnlyCollection<FacilityShopCatalogDefinition>
        IFacilityShopDefinitionCatalog.Buildings => Buildings
            .Select(building => new FacilityShopCatalogDefinition(
                building.id,
                FacilityShopService.GetBuildingName(building),
                FacilityShopService.GetBuildingStar(building)))
            .ToArray();

    IReadOnlyCollection<int> IFacilityShopDefinitionCatalog.BlueprintIds =>
        Blueprints.Select(blueprint => blueprint.id).ToArray();

    public BuildingSO FindBuildingById(int buildingId)
    {
        if (buildingId < 0)
        {
            return null;
        }

        return catalog.GetData<BuildingSO>().TryGetValue(buildingId, out BuildingSO building)
            ? building
            : null;
    }
}
