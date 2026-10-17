using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using DungeonStory.Foundation.Facts;
using UnityEngine;
using VContainer.Unity;

public sealed class CropPlotWorldDependencies
{
    public CropPlotWorldDependencies(
        IBuildingWorldQuery buildingWorld,
        IResourceEconomyContentCatalog catalog,
        ICropCycleInputRequirementQuery inputRequirements,
        IProductionItemGateway items,
        IPhysicalSeedLotGateway seedLots,
        IPhysicalFacilityItemSinkGateway treatmentItems,
        IPackagedLotTareDispositionService packagedTare,
        ICropEcologyService ecology,
        ICropEcologyHarvestTransactionService ecologyHarvests,
        IProductionOutputCapabilityRegistry outputCapabilities,
        IProductionDomainOutputPublicationService outputPublication,
        ICropPlotInputOwnerRuntime inputOwners,
        ICharacterPerformanceDefinitionMaximumQuery performanceMaximum,
        IGameplayEffectResultBoundsQuery effectBounds,
        IFacilityCapabilityQuery facilities,
        ICropIrrigationRuntime irrigation,
        IFacilityCandidateCache facilityCandidates,
        IWorkforceReplanService workforce)
    {
        BuildingWorld = buildingWorld
            ?? throw new ArgumentNullException(nameof(buildingWorld));
        Catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        InputRequirements = inputRequirements
            ?? throw new ArgumentNullException(nameof(inputRequirements));
        Items = items ?? throw new ArgumentNullException(nameof(items));
        SeedLots = seedLots ?? throw new ArgumentNullException(nameof(seedLots));
        TreatmentItems = treatmentItems
            ?? throw new ArgumentNullException(nameof(treatmentItems));
        PackagedTare = packagedTare
            ?? throw new ArgumentNullException(nameof(packagedTare));
        Ecology = ecology ?? throw new ArgumentNullException(nameof(ecology));
        EcologyHarvests = ecologyHarvests
            ?? throw new ArgumentNullException(nameof(ecologyHarvests));
        OutputCapabilities = outputCapabilities
            ?? throw new ArgumentNullException(nameof(outputCapabilities));
        OutputPublication = outputPublication
            ?? throw new ArgumentNullException(nameof(outputPublication));
        InputOwners = inputOwners
            ?? throw new ArgumentNullException(nameof(inputOwners));
        PerformanceMaximum = performanceMaximum
            ?? throw new ArgumentNullException(nameof(performanceMaximum));
        EffectBounds = effectBounds
            ?? throw new ArgumentNullException(nameof(effectBounds));
        Facilities = facilities ?? throw new ArgumentNullException(nameof(facilities));
        Irrigation = irrigation ?? throw new ArgumentNullException(nameof(irrigation));
        FacilityCandidates = facilityCandidates
            ?? throw new ArgumentNullException(nameof(facilityCandidates));
        Workforce = workforce ?? throw new ArgumentNullException(nameof(workforce));
    }

    public IBuildingWorldQuery BuildingWorld { get; }
    public IResourceEconomyContentCatalog Catalog { get; }
    public ICropCycleInputRequirementQuery InputRequirements { get; }
    public IProductionItemGateway Items { get; }
    public IPhysicalSeedLotGateway SeedLots { get; }
    public IPhysicalFacilityItemSinkGateway TreatmentItems { get; }
    public IPackagedLotTareDispositionService PackagedTare { get; }
    public ICropEcologyService Ecology { get; }
    public ICropEcologyHarvestTransactionService EcologyHarvests { get; }
    public IProductionOutputCapabilityRegistry OutputCapabilities { get; }
    public IProductionDomainOutputPublicationService OutputPublication { get; }
    public ICropPlotInputOwnerRuntime InputOwners { get; }
    public ICharacterPerformanceDefinitionMaximumQuery PerformanceMaximum { get; }
    public IGameplayEffectResultBoundsQuery EffectBounds { get; }
    public IFacilityCapabilityQuery Facilities { get; }
    public ICropIrrigationRuntime Irrigation { get; }
    public IFacilityCandidateCache FacilityCandidates { get; }
    public IWorkforceReplanService Workforce { get; }
}
