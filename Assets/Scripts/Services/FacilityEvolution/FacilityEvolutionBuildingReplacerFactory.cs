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
                gridBuildingObjectFactory),
            ResolveProductionFacilityMutationFence(),
            ResolveProductionFacilityRetargetTransaction());
    }

    private void InjectCreatedBuilding(BuildableObject building)
    {
        if (building != null)
        {
            objectResolver.Inject(building);
        }
    }

    private IProductionFacilityMutationFence ResolveProductionFacilityMutationFence()
    {
        if (objectResolver.TryResolve(
                typeof(IProductionFacilityMutationFence),
                out object resolved)
            && resolved is IProductionFacilityMutationFence fence)
        {
            return fence;
        }
        throw new InvalidOperationException(
            $"{nameof(GridFacilityEvolutionBuildingReplacerFactory)} requires "
            + $"{nameof(IProductionFacilityMutationFence)}.");
    }

    private IProductionFacilityRetargetTransaction ResolveProductionFacilityRetargetTransaction()
    {
        if (objectResolver.TryResolve(
                typeof(IProductionFacilityRetargetTransaction),
                out object resolved)
            && resolved is IProductionFacilityRetargetTransaction transaction)
        {
            return transaction;
        }
        throw new InvalidOperationException(
            $"{nameof(GridFacilityEvolutionBuildingReplacerFactory)} requires "
            + $"{nameof(IProductionFacilityRetargetTransaction)}.");
    }
}
