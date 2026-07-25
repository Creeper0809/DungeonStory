using System;
using VContainer;

public interface IFacilityEvolutionBuildingReplacerFactory
{
    IFacilityEvolutionBuildingReplacer Create();
}

public sealed class GridFacilityEvolutionBuildingReplacerFactory : IFacilityEvolutionBuildingReplacerFactory
{
    private readonly IGridTextureProvider gridTextureProvider;
    private readonly IGridBuildingObjectFactory gridBuildingObjectFactory;
    private readonly IObjectResolver objectResolver;

    public GridFacilityEvolutionBuildingReplacerFactory(
        IGridTextureProvider gridTextureProvider,
        IGridBuildingObjectFactory gridBuildingObjectFactory,
        IObjectResolver objectResolver)
    {
        this.gridTextureProvider = gridTextureProvider
            ?? throw new ArgumentNullException(nameof(gridTextureProvider));
        this.gridBuildingObjectFactory = gridBuildingObjectFactory
            ?? throw new ArgumentNullException(nameof(gridBuildingObjectFactory));
        this.objectResolver = objectResolver
            ?? throw new ArgumentNullException(nameof(objectResolver));
    }

    public IFacilityEvolutionBuildingReplacer Create()
    {
        return new GridFacilityEvolutionBuildingReplacer(
            new GridBuildingFactory(
                gridTextureProvider.Texture,
                InjectCreatedBuilding,
                gridBuildingObjectFactory));
    }

    private void InjectCreatedBuilding(BuildableObject building)
    {
        if (building != null)
        {
            objectResolver.Inject(building);
        }
    }
}
