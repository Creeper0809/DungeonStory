#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using VContainer;

public static class ProductionAuthoredThroughputFacilityScopeDebugScenarios
{
    private sealed class FrozenScopeQuery :
        IProductionAuthoredThroughputFacilityScopeQuery
    {
        private readonly ProductionAuthoredThroughputFacilityScopeSnapshot value;

        internal FrozenScopeQuery(
            ProductionAuthoredThroughputFacilityScopeSnapshot value)
        {
            this.value = value ?? throw new ArgumentNullException(nameof(value));
        }

        public ProductionAuthoredThroughputFacilityScopeSnapshot Capture() =>
            value;
    }

    private sealed class CurrentCommandExecutionConnectionQuery :
        IResearchFacilityCommandExecutionConnectionQuery
    {
        public bool IsConnected(ResearchFacilityCommandKind command) =>
            ResearchFacilityCommandConsumerRegistry.HasExecutionContract(command);
    }

    [MenuItem(
        "DungeonStory/V27/Production/Validate Current Throughput Facility Scope")]
    public static void ValidateCurrentPlayMode()
    {
        if (!EditorApplication.isPlaying)
        {
            throw new InvalidOperationException(
                "Current throughput facility scope validation requires Play Mode.");
        }

        DungeonRuntimeLifetimeScope scope = UnityEngine.Object
            .FindObjectsByType<DungeonRuntimeLifetimeScope>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None)
            .FirstOrDefault(value => value != null && value.Container != null)
            ?? throw new InvalidOperationException(
                "Dungeon runtime composition root is unavailable.");
        ProductionAuthoredThroughputFacilityScopeSnapshot snapshot =
            Capture(scope.Container);
        Require(snapshot.AutomaticProducerCount == 92,
            "Expected 92 current automatic producer facilities.");
        Require(snapshot.RecipeOnlyProducerCount == 85,
            "Expected 85 current recipe-only producer facilities.");
        Require(snapshot.SpecialProducerCount == 7,
            "Expected 7 current capacity/special producer facilities.");
        Require(snapshot.DistinctRecipeCount == 271,
            "Expected 271 distinct current recipe definitions in scope.");
        Require(snapshot.SpecialCandidateCount == 904
                && snapshot.SpecialGapCount == 0,
            "Current special throughput branches are incomplete.");
        Require(snapshot.Coverage.CompleteEnvelopes.Count == 92
                && snapshot.Coverage.Gaps.Count == 0,
            "Current authored throughput envelope publication is incomplete.");
        Debug.Log(
            "[ProductionAuthoredThroughputFacilityScope] PASS;facilities=92;recipeOnly=85;special=7;recipes=271;specialCandidates=904;envelopes=92;gaps=0;sourceDigest="
            + snapshot.SourceDigest);
    }

    public static ProductionAuthoredThroughputFacilityScopeSnapshot Capture(
        IObjectResolver container)
    {
        if (container == null)
            throw new ArgumentNullException(nameof(container));

        IGameContentDefinitionSource content =
            container.Resolve<IGameContentDefinitionSource>();
        ProductionFacilityOutputCensus census = new(
            container.Resolve<IResourceEconomyContentCatalog>().Recipes,
            container.Resolve<
                IProductionFacilityOutputCapacityContributorRegistry>(),
            container.Resolve<ProductionSpecialThroughputContributorRegistry>(),
            new ProductionFacilityOutputDispositionRegistry(new
                IProductionFacilityOutputDispositionContributor[]
                {
                    new AuthoredFacilityOutputDispositionContributor(),
                    new CoreAbilityFacilityOutputDispositionContributor(),
                    new EquipmentProgressionFacilityOutputDispositionContributor(),
                    new ResearchCommandFacilityOutputDispositionContributor(
                        new CurrentCommandExecutionConnectionQuery())
                }));
        ProductionRecipeThroughputCycleProjector projector = new(
            container.Resolve<IProductionMaximumOutputFactorCatalog>(),
            new ProductionRecipeThroughputBranchAuthority(
                container.Resolve<IProductionOutputMaximumMassRegistry>(),
                container.Resolve<
                    IProductionPassiveBatchOutputPortfolioQuery>()),
            container.Resolve<IProductionRecipeWorkRateMaximumQuery>(),
            ProductionThroughputTimeScaleAuthority.Capture());

        return new ProductionAuthoredThroughputFacilityScopeAuthority(
                content,
                census,
                container.Resolve<ProductionFacilityDefinitionCatalog>(),
                new ProductionAuthoredThroughputEnvelopeAuthority(projector))
            .Capture();
    }

    public static ProductionOutputClearanceMeasurementScopeSnapshot
        CaptureMeasurementScope(
            IObjectResolver container,
            ProductionAuthoredThroughputFacilityScopeSnapshot frozenScope = null)
    {
        if (container == null)
            throw new ArgumentNullException(nameof(container));
        ProductionAuthoredThroughputFacilityScopeSnapshot scope =
            frozenScope ?? Capture(container);
        IProductionFacilityOutputCapacityBranchMassQuery branchMasses =
            container.Resolve<
                IProductionFacilityOutputCapacityBranchMassQuery>();
        ProductionOutputClearanceMeasurementPlanRegistry planRegistry = new(
            ProductionOutputClearanceMeasurementContributorIds.CreateCurrent(),
            branchMasses);
        return new ProductionOutputClearanceMeasurementScopeAuthority(
                new FrozenScopeQuery(scope),
                container.Resolve<ProductionFacilityDefinitionCatalog>(),
                container.Resolve<
                    IProductionFacilityOutputCapacityContributorRegistry>(),
                container.Resolve<IProductionMaximumOutputFactorCatalog>(),
                new ProductionRecipeThroughputBranchAuthority(
                    container.Resolve<IProductionOutputMaximumMassRegistry>(),
                    container.Resolve<
                        IProductionPassiveBatchOutputPortfolioQuery>()),
                planRegistry)
            .Capture();
    }

    public static ProductionOutputClearanceExecutableDescriptorCoverage
        CaptureRecipeExecutableDescriptors(
            IObjectResolver container,
            ProductionOutputClearanceMeasurementScopeSnapshot frozenScope = null)
    {
        if (container == null)
            throw new ArgumentNullException(nameof(container));
        ProductionOutputClearanceMeasurementScopeSnapshot scope =
            frozenScope ?? CaptureMeasurementScope(container);
        IProductionOutputMaximumMassRegistry maximumMass =
            container.Resolve<IProductionOutputMaximumMassRegistry>();
        IProductionPassiveBatchOutputPortfolioQuery passive =
            container.Resolve<IProductionPassiveBatchOutputPortfolioQuery>();
        ProductionRecipeThroughputBranchAuthority branches = new(
            maximumMass,
            passive);
        ProductionOutputClearanceExecutableDescriptorRegistry registry = new(
            new IProductionOutputClearanceExecutableDescriptorContributor[]
            {
                new ProductionOutputClearanceRecipeExecutableDescriptorContributor(
                    container.Resolve<IProductionMaximumOutputFactorCatalog>(),
                    branches,
                    maximumMass,
                    passive),
                new ProductionOutputClearanceCropHarvestExecutableDescriptorContributor(
                    container.Resolve<ICropHarvestCycleMaximumQuery>(),
                    container.Resolve<
                        IProductionFacilityOutputCapacityBranchMassQuery>(),
                    container.Resolve<ICropCycleInputRequirementQuery>(),
                    container.Resolve<IResourceEconomyContentCatalog>(),
                    container.Resolve<IGameContentDefinitionSource>()),
                new ProductionOutputClearanceCombatCraftExecutableDescriptorContributor(
                    container.Resolve<ICombatCraftCycleMaximumQuery>(),
                    container.Resolve<
                        IProductionFacilityOutputCapacityBranchMassQuery>()),
                new ProductionOutputClearanceApparelExecutableDescriptorContributor(
                    container.Resolve<IApparelCraftCycleMaximumQuery>(),
                    container.Resolve<
                        IProductionFacilityOutputCapacityBranchMassQuery>()),
                new ProductionOutputClearanceCertifiedSeedExecutableDescriptorContributor(
                    container.Resolve<ICertifiedSeedCycleMaximumQuery>(),
                    container.Resolve<
                        IProductionFacilityOutputCapacityBranchMassQuery>())
            });
        return registry.Capture(scope);
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
#endif
