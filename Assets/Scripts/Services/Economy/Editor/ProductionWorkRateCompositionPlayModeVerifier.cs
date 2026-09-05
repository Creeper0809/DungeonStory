#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using VContainer;

public static class ProductionWorkRateCompositionPlayModeVerifier
{
    public const string RequestPath =
        "Temp/v27-production-work-rate-composition.request";
    public const string ReportPath =
        "Artifacts/QA/v27-production-work-rate-composition-playmode.txt";
    public const string ProjectionCsvPath =
        "Artifacts/QA/v27-production-recipe-throughput-playmode.csv";
    public const string EnvelopeCsvPath =
        "Artifacts/QA/v27-production-throughput-envelope-playmode.csv";
    public const string ClearanceMeasurementPlanCsvPath =
        "Artifacts/QA/v27-production-output-clearance-measurement-plan.csv";
    public const string ClearanceMeasurementPortfolioCsvPath =
        "Artifacts/QA/v27-production-output-clearance-measurement-portfolio.csv";

    [MenuItem(
        "DungeonStory/V27/Production/Verify Work Rate Composition PlayMode")]
    public static void RequestRun()
    {
        Scene activeScene = SceneManager.GetActiveScene();
        if (!string.Equals(
                activeScene.path,
                V27CurrentSourceEvidenceDigest.GameplayScenePath,
                StringComparison.Ordinal))
        {
            if (activeScene.IsValid() && activeScene.isDirty)
            {
                throw new InvalidOperationException(
                    "Work-rate verification refuses to replace a dirty non-gameplay scene: "
                    + activeScene.path);
            }

            Scene opened = EditorSceneManager.OpenScene(
                V27CurrentSourceEvidenceDigest.GameplayScenePath,
                OpenSceneMode.Single);
            if (!opened.IsValid()
                || !string.Equals(
                    opened.path,
                    V27CurrentSourceEvidenceDigest.GameplayScenePath,
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "Work-rate verification could not open the official gameplay scene.");
            }
        }

        Directory.CreateDirectory("Temp");
        File.WriteAllText(RequestPath, "requested");
        if (EditorApplication.isPlaying)
        {
            ProductionWorkRateCompositionPlayModeRunner.StartPending();
            return;
        }
        EditorApplication.EnterPlaymode();
    }
}

public sealed class ProductionWorkRateCompositionPlayModeRunner : MonoBehaviour
{
    private const float ResolveTimeoutSeconds = 20f;
    private readonly List<string> checks = new();
    private readonly List<string> failures = new();
    private readonly List<string> unexpectedLogs = new();
    private readonly List<ProductionRecipeThroughputCycleCandidateSnapshot>
        recipeCandidates = new();
    private readonly List<ProductionThroughputCoverageGap> recipeGaps = new();
    private readonly List<ProductionOutputThroughputEnvelopeSnapshot>
        throughputEnvelopes = new();
    private readonly List<ProductionOutputClearanceMeasurementPlan>
        clearanceMeasurementPlans = new();

    private sealed class CurrentCommandExecutionConnectionQuery :
        IResearchFacilityCommandExecutionConnectionQuery
    {
        public bool IsConnected(ResearchFacilityCommandKind command) =>
            ResearchFacilityCommandConsumerRegistry.HasExecutionContract(command);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap() => StartPending();

    internal static void StartPending()
    {
        if (!File.Exists(ProductionWorkRateCompositionPlayModeVerifier.RequestPath))
            return;
        File.Delete(ProductionWorkRateCompositionPlayModeVerifier.RequestPath);
        if (FindFirstObjectByType<ProductionWorkRateCompositionPlayModeRunner>()
            != null)
        {
            return;
        }
        new GameObject(nameof(ProductionWorkRateCompositionPlayModeRunner))
            .AddComponent<ProductionWorkRateCompositionPlayModeRunner>();
    }

    private IEnumerator Start()
    {
        Directory.CreateDirectory("Artifacts/QA");
        Application.logMessageReceived += CaptureLog;
        IEnumerator execution = Execute();
        while (true)
        {
            bool hasNext;
            object current = null;
            try
            {
                hasNext = execution.MoveNext();
                if (hasNext)
                    current = execution.Current;
            }
            catch (Exception exception)
            {
                failures.Add(exception.ToString());
                break;
            }

            if (!hasNext)
                break;
            yield return current;
        }

        Application.logMessageReceived -= CaptureLog;
        WriteReport();
        Destroy(gameObject);
        EditorApplication.ExitPlaymode();
    }

    private IEnumerator Execute()
    {
        DungeonRuntimeLifetimeScope scope = null;
        float deadline = Time.realtimeSinceStartup + ResolveTimeoutSeconds;
        while (Time.realtimeSinceStartup < deadline)
        {
            scope = FindObjectsByType<DungeonRuntimeLifetimeScope>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None)
                .FirstOrDefault(value => value != null && value.Container != null);
            if (scope?.Container != null)
                break;
            yield return null;
        }
        Require(scope?.Container != null,
            "DungeonRuntimeLifetimeScope/container was not published.");

        IObjectResolver container = scope.Container;
        ProductionWorkRateContributorManifest manifest =
            container.Resolve<ProductionWorkRateContributorManifest>();
        IProductionRecipeWorkRateMaximumContributor[] contributors = container
            .Resolve<IEnumerable<IProductionRecipeWorkRateMaximumContributor>>()
            .OrderBy(value => value.ContributorId, StringComparer.Ordinal)
            .ToArray();
        string[] contributorIds = contributors
            .Select(value => value.ContributorId)
            .ToArray();
        string[] manifestIds = manifest.RequiredContributorIds
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        Check(contributorIds.Length == 7
            && contributorIds.Distinct(StringComparer.Ordinal).Count() == 7
            && contributorIds.SequenceEqual(manifestIds, StringComparer.Ordinal),
            "WORK_RATE_MANIFEST_EXACT",
            "contributors=" + string.Join("|", contributorIds));

        Require(container.Resolve<IProductionAutomaticWorkRateMaximumQuery>()
            != null, "Automatic work-rate maximum query is unresolved.");
        Require(container.Resolve<IProductionRecipeWorkRateMaximumQuery>()
            != null, "Recipe work-rate maximum authority is unresolved.");
        ProductionFacilityDefinitionCatalog facilityDefinitions =
            container.Resolve<ProductionFacilityDefinitionCatalog>();
        Check(facilityDefinitions.DefinitionCount == 377
                && facilityDefinitions.IgnoredRuntimeArchetypeCount == 42,
            "FACILITY_DEFINITION_RUNTIME_ARCHETYPE_BOUNDARY",
            "definitions=" + facilityDefinitions.DefinitionCount
            + ";ignoredRuntimeArchetypes="
            + facilityDefinitions.IgnoredRuntimeArchetypeCount);
        Require(container.Resolve<ICharacterPerformanceDefinitionMaximumQuery>()
            != null, "Character performance maximum query is unresolved.");
        Require(container.Resolve<ICharacterWorkContextDefinitionMaximumQuery>()
            != null, "Character work-context maximum query is unresolved.");
        Require(container.Resolve<IBuildingCraftsmanshipDefinitionMaximumQuery>()
            != null, "Craftsmanship maximum query is unresolved.");
        Require(container.Resolve<IAutomationAssistedWorkDefinitionMaximumQuery>()
            != null, "Automation-assisted maximum query is unresolved.");
        Require(container.Resolve<IFacilityEvolutionWorkSpeedDefinitionMaximumQuery>()
            != null, "Facility-evolution maximum query is unresolved.");

        IWorkStatPolicyRegistry workRegistry =
            container.Resolve<IWorkStatPolicyRegistry>();
        IWorkStatPolicyDefinitionMaximumQuery workMaximums =
            container.Resolve<IWorkStatPolicyDefinitionMaximumQuery>();
        Check(ReferenceEquals(workRegistry, workMaximums),
            "WORK_STAT_SINGLETON_IDENTITY",
            workRegistry.GetType().FullName ?? string.Empty);

        IRoomEnvironmentQuery roomQuery =
            container.Resolve<IRoomEnvironmentQuery>();
        IWorkEnvironmentDefinitionMaximumQuery roomMaximums =
            container.Resolve<IWorkEnvironmentDefinitionMaximumQuery>();
        Check(ReferenceEquals(roomQuery, roomMaximums),
            "ROOM_ENVIRONMENT_SINGLETON_IDENTITY",
            roomQuery.GetType().FullName ?? string.Empty);

        ProductionSpecialThroughputContributorRegistry specialRegistry =
            container.Resolve<ProductionSpecialThroughputContributorRegistry>();
        IProductionSpecialThroughputContributor[] special = container
            .Resolve<IEnumerable<IProductionSpecialThroughputContributor>>()
            .OrderBy(value => value.ContributorId, StringComparer.Ordinal)
            .ToArray();
        string[] expectedSpecial =
        {
            ApparelSpecialThroughputGapContributor.Id,
            CertifiedSeedSpecialThroughputGapContributor.Id,
            CombatCraftSpecialThroughputGapContributor.Id,
            CropHarvestSpecialThroughputGapContributor.Id
        };
        Array.Sort(expectedSpecial, StringComparer.Ordinal);
        Check(special.Length == 4
            && special.Select(value => value.ContributorId).SequenceEqual(
                expectedSpecial,
                StringComparer.Ordinal)
            && !string.IsNullOrEmpty(specialRegistry.RegistryFingerprint),
            "SPECIAL_THROUGHPUT_OWNER_SET_EXACT",
            "owners=" + string.Join("|", special.Select(value =>
                value.ContributorId)));

        IProductionOutputMaximumMassRegistry maximumMass =
            container.Resolve<IProductionOutputMaximumMassRegistry>();
        string[] expectedMaximumMassCapabilities =
        {
            ProductionOutputCapabilityIds.StandardDefinition,
            ProductionOutputCapabilityIds.ApparelWorkOrder,
            ProductionOutputCapabilityIds.CombatEquipmentCraft,
            ProductionOutputCapabilityIds.CombatAmmunitionCraft,
            ProductionOutputCapabilityIds.PerishableFood,
            ProductionOutputCapabilityIds.CertifiedSeed,
            ProductionOutputCapabilityIds.CropHarvestSeedLot,
            EnvironmentalWorkwearProductionOutputHandler.HandlerCapabilityId,
            SurgicalPartProductionOutputHandler.HandlerCapabilityId
        };
        Array.Sort(expectedMaximumMassCapabilities, StringComparer.Ordinal);
        Check(maximumMass.CapabilityIds.SequenceEqual(
                    expectedMaximumMassCapabilities,
                    StringComparer.Ordinal)
                && maximumMass.CapabilityContracts.Count == 9
                && !string.IsNullOrEmpty(maximumMass.RegistryFingerprint),
            "OUTPUT_MAXIMUM_MASS_CAPABILITY_SET_EXACT",
            "capabilities=" + string.Join("|", maximumMass.CapabilityIds)
                + ";fingerprint=" + maximumMass.RegistryFingerprint);

        VerifyProducerWideThroughput(container, facilityDefinitions);
        ProductionAuthoredThroughputFacilityScopeSnapshot sharedScope =
            ProductionAuthoredThroughputFacilityScopeDebugScenarios.Capture(
                container);
        Check(sharedScope.AutomaticProducerCount == 92
                && sharedScope.RecipeOnlyProducerCount == 85
                && sharedScope.SpecialProducerCount == 7
                && sharedScope.DistinctRecipeCount == 271
                && sharedScope.SpecialCandidateCount == 904
                && sharedScope.SpecialGapCount == 0
                && sharedScope.Coverage.CompleteEnvelopes.Count == 92
                && sharedScope.Coverage.Gaps.Count == 0,
            "SHARED_PRODUCER_SCOPE_QUERY_EXACT",
            "facilities=" + sharedScope.AutomaticProducerCount
                + ";recipeOnly=" + sharedScope.RecipeOnlyProducerCount
                + ";special=" + sharedScope.SpecialProducerCount
                + ";recipes=" + sharedScope.DistinctRecipeCount
                + ";specialCandidates=" + sharedScope.SpecialCandidateCount
                + ";envelopes="
                + sharedScope.Coverage.CompleteEnvelopes.Count
                + ";gaps=" + sharedScope.Coverage.Gaps.Count
                + ";sourceDigest=" + sharedScope.SourceDigest);
        ProductionOutputClearanceMeasurementScopeSnapshot measurementScope =
            ProductionAuthoredThroughputFacilityScopeDebugScenarios
                .CaptureMeasurementScope(container, sharedScope);
        int recipeMeasurementBranches = measurementScope.Contexts.Sum(value =>
            value.RecipeBranches.Count);
        int specialMeasurementBranches = measurementScope.Contexts.Sum(value =>
            value.CapacityContributions.Sum(contribution =>
                contribution.Branches.Count));
        int measurementCandidates = measurementScope.Plans.Sum(value =>
            value.Candidates.Count);
        Check(measurementScope.Contexts.Count == 92
                && measurementScope.Plans.Count == 92
                && measurementScope.Gaps.Count == 0
                && recipeMeasurementBranches == 278
                && specialMeasurementBranches == 904
                && measurementCandidates == 1182,
            "OUTPUT_CLEARANCE_MEASUREMENT_SCOPE_EXACT",
            "contexts=" + measurementScope.Contexts.Count
                + ";plans=" + measurementScope.Plans.Count
                + ";gaps=" + measurementScope.Gaps.Count
                + ";recipeBranches=" + recipeMeasurementBranches
                + ";specialBranches=" + specialMeasurementBranches
                + ";candidates=" + measurementCandidates
                + ";sourceDigest=" + measurementScope.SourceDigest);
        ProductionOutputClearanceExecutableDescriptorCoverage
            executableCoverage =
                ProductionAuthoredThroughputFacilityScopeDebugScenarios
                    .CaptureRecipeExecutableDescriptors(
                        container,
                        measurementScope);
        ProductionOutputClearanceRecipeExecutablePayload[] recipePayloads =
            executableCoverage.Descriptors
                .Select(value => value.Payload as
                    ProductionOutputClearanceRecipeExecutablePayload)
                .Where(value => value != null)
                .ToArray();
        ProductionOutputClearanceCombatCraftExecutablePayload[] combatPayloads =
            executableCoverage.Descriptors
                .Select(value => value.Payload as
                    ProductionOutputClearanceCombatCraftExecutablePayload)
                .Where(value => value != null)
                .ToArray();
        ProductionOutputClearanceCropHarvestExecutablePayload[] cropPayloads =
            executableCoverage.Descriptors
                .Select(value => value.Payload as
                    ProductionOutputClearanceCropHarvestExecutablePayload)
                .Where(value => value != null)
                .ToArray();
        ProductionOutputClearanceApparelExecutablePayload[] apparelPayloads =
            executableCoverage.Descriptors
                .Select(value => value.Payload as
                    ProductionOutputClearanceApparelExecutablePayload)
                .Where(value => value != null)
                .ToArray();
        ProductionOutputClearanceCertifiedSeedExecutablePayload[]
            certifiedSeedPayloads = executableCoverage.Descriptors
                .Select(value => value.Payload as
                    ProductionOutputClearanceCertifiedSeedExecutablePayload)
                .Where(value => value != null)
                .ToArray();
        int passiveExecutableCount = recipePayloads.Count(value =>
            value?.ProcessKind == ProductionProcessKind.PassiveBatch);
        Check(executableCoverage.Descriptors.Count == 92
                && executableCoverage.Gaps.Count == 0
                && recipePayloads.Length == 85
                && recipePayloads.All(value => value != null
                    && value.Outputs.Count > 0
                    && value.Outputs.Aggregate(
                        0L,
                        (sum, output) => checked(sum + output.MassGrams))
                    == executableCoverage.Descriptors.Single(descriptor =>
                        ReferenceEquals(descriptor.Payload, value))
                        .Plan.Winner.Source.MaximumSingleCompletionMassGrams)
                && cropPayloads.Length == 4
                && cropPayloads.All(value => value.Inputs.Count > 0
                    && value.Outputs.Count == 2
                    && value.Outputs.Aggregate(
                        0L,
                        (sum, output) => checked(sum + output.MassGrams))
                    == executableCoverage.Descriptors.Single(descriptor =>
                        ReferenceEquals(descriptor.Payload, value))
                        .Plan.Winner.Source.MaximumSingleCompletionMassGrams)
                && combatPayloads.Length == 1
                && combatPayloads[0].Inputs.Count > 0
                && combatPayloads[0].Outputs.Count > 0
                && combatPayloads[0].Inputs.Any(value => string.Equals(
                    value.ItemId,
                    combatPayloads[0].SelectedMaterialItemId,
                    StringComparison.Ordinal))
                && combatPayloads[0].Outputs.Aggregate(
                    0L,
                    (sum, output) => checked(sum + output.MassGrams))
                == executableCoverage.Descriptors.Single(descriptor =>
                    ReferenceEquals(descriptor.Payload, combatPayloads[0]))
                    .Plan.Winner.Source.MaximumSingleCompletionMassGrams
                && apparelPayloads.Length == 1
                && apparelPayloads[0].Inputs.Count == 1
                && apparelPayloads[0].Outputs.Count > 0
                && string.Equals(
                    apparelPayloads[0].Inputs[0].ItemId,
                    apparelPayloads[0].SelectedPhysicalItemId,
                    StringComparison.Ordinal)
                && apparelPayloads[0].Outputs.Aggregate(
                    0L,
                    (sum, output) => checked(sum + output.MassGrams))
                == executableCoverage.Descriptors.Single(descriptor =>
                    ReferenceEquals(descriptor.Payload, apparelPayloads[0]))
                    .Plan.Winner.Source.MaximumSingleCompletionMassGrams
                && certifiedSeedPayloads.Length == 1
                && certifiedSeedPayloads[0].Inputs.Count == 2
                && certifiedSeedPayloads[0].Outputs.Count == 1
                && certifiedSeedPayloads[0].InputSeedLot != null
                && certifiedSeedPayloads[0].OutputSeedLot != null
                && certifiedSeedPayloads[0].InputSeedLot.Generation == 0
                && certifiedSeedPayloads[0].InputSeedLot.PathogenLoad == 0f
                && certifiedSeedPayloads[0].OutputSeedLot.PathogenLoad == 0f
                && string.Equals(
                    certifiedSeedPayloads[0].InputSeedLot.CultivarGenomeId,
                    certifiedSeedPayloads[0].OutputSeedLot.CultivarGenomeId,
                    StringComparison.Ordinal)
                && certifiedSeedPayloads[0].Inputs.Any(value => string.Equals(
                    value.ItemId,
                    certifiedSeedPayloads[0].SeedItemId,
                    StringComparison.Ordinal))
                && certifiedSeedPayloads[0].Inputs.Any(value => string.Equals(
                    value.ItemId,
                    certifiedSeedPayloads[0].CertificationKitItemId,
                    StringComparison.Ordinal))
                && certifiedSeedPayloads[0].Outputs.Aggregate(
                    0L,
                    (sum, output) => checked(sum + output.MassGrams))
                == executableCoverage.Descriptors.Single(descriptor =>
                    ReferenceEquals(
                        descriptor.Payload,
                        certifiedSeedPayloads[0]))
                    .Plan.Winner.Source.MaximumSingleCompletionMassGrams,
            "OUTPUT_CLEARANCE_EXECUTABLE_DESCRIPTOR_COVERAGE_EXACT",
            "descriptors=" + executableCoverage.Descriptors.Count
                + ";typedSpecialGaps=" + executableCoverage.Gaps.Count
                + ";recipePayloads=" + recipePayloads.Length
                + ";cropPayloads=" + cropPayloads.Length
                + ";combatPayloads=" + combatPayloads.Length
                + ";apparelPayloads=" + apparelPayloads.Length
                + ";certifiedSeedPayloads=" + certifiedSeedPayloads.Length
                + ";passive=" + passiveExecutableCount
                + ";sourceDigest=" + executableCoverage.SourceDigest);
        clearanceMeasurementPlans.Clear();
        clearanceMeasurementPlans.AddRange(measurementScope.Plans);
        WriteClearanceMeasurementPlanCsv(measurementScope.SourceDigest);
        ProductionOutputClearanceMeasurementPortfolioSnapshot portfolio =
            ProductionOutputClearanceMeasurementPortfolioAuthority
                .CaptureCurrent(measurementScope);
        int expectedPortfolioFixtures = checked(
            portfolio.Scope.Plans.Count * portfolio.Seeds.Count);
        Check(portfolio.Scope.Plans.Count > 0
                && portfolio.Seeds.Count == 32
                && portfolio.Fixtures.Count == expectedPortfolioFixtures
                && portfolio.Fixtures.Select(value =>
                        value.Plan.DefinitionId + "\n"
                        + value.Plan.WorkstationTag)
                    .Distinct(StringComparer.Ordinal).Count()
                    == portfolio.Scope.Plans.Count
                && portfolio.Fixtures.Select(value => value.ObservationId)
                    .Distinct(StringComparer.Ordinal).Count()
                    == expectedPortfolioFixtures,
            "OUTPUT_CLEARANCE_MEASUREMENT_PORTFOLIO_EXACT",
            "plans=" + portfolio.Scope.Plans.Count
                + ";seeds=" + portfolio.Seeds.Count
                + ";fixtures=" + portfolio.Fixtures.Count
                + ";sourceDigest=" + portfolio.SourceDigest);
        VerifyClearanceObservationPortfolioGate(portfolio);
        WriteClearanceMeasurementPortfolioCsv(portfolio);

        Check(unexpectedLogs.Count == 0,
            "PLAYMODE_WARNING_ERROR_ZERO",
            string.Join(" | ", unexpectedLogs));
    }

    private void VerifyProducerWideThroughput(
        IObjectResolver container,
        ProductionFacilityDefinitionCatalog facilityDefinitions)
    {
        IGameContentDefinitionSource content =
            container.Resolve<IGameContentDefinitionSource>();
        ProductionFacilityOutputCensus censusQuery = new(
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
        ProductionFacilityOutputCensusSnapshot census = censusQuery.Capture(
            content.GetAll<BuildingSO>());
        Check(census.SpecialBranchCount == 904
                && census.SpecialCandidateCount == 904
                && census.SpecialGapCount == 0
                && census.SpecialAuthoredCycleGapCount == 0
                && census.SpecialExecutionUnsupportedGapCount == 0
                && census.SpecialUnregisteredGapCount == 0,
            "SPECIAL_THROUGHPUT_CURRENT_SOURCE_COMPLETE",
            "branches=" + census.SpecialBranchCount
                + ";candidates=" + census.SpecialCandidateCount
                + ";gaps=" + census.SpecialGapCount
                + ";authoredCycle=" + census.SpecialAuthoredCycleGapCount
                + ";unsupported="
                + census.SpecialExecutionUnsupportedGapCount);
        ProductionFacilityOutputCensusRow[] producerRows = census.Rows
            .Where(value => value.IsAutomaticProducer)
            .OrderBy(value => value.DefinitionId, StringComparer.Ordinal)
            .ToArray();
        Dictionary<string, ProductionRecipeSO> recipesById = content
            .GetAll<ProductionRecipeSO>()
            .Where(value => value != null)
            .ToDictionary(value => value.RecipeId, StringComparer.Ordinal);
        List<ProductionAuthoredThroughputFacilitySubject> subjects = new();
        List<ProductionRecipeSO> recipes = new();
        foreach (ProductionFacilityOutputCensusRow row in producerRows)
        {
            BuildingSO definition = facilityDefinitions.Require(row.DefinitionId);
            IReadOnlyList<string> reachableRecipeIds =
                row.CapacityContributorIds.Count == 0
                    ? row.RecipeIds
                    : Array.Empty<string>();
            ProductionRecipeSO[] joined = reachableRecipeIds.Select(recipeId =>
                    recipesById.TryGetValue(recipeId, out ProductionRecipeSO recipe)
                        ? recipe
                        : throw new InvalidOperationException(
                            "Recipe-only census references an unknown recipe: "
                            + recipeId))
                .OrderBy(value => value.RecipeId, StringComparer.Ordinal)
                .ToArray();
            string[] joinedDigests = joined
                .Select(ProductionRecipeSemanticDigest.Capture)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();
            if (row.CapacityContributorIds.Count == 0)
            {
                Require(joinedDigests.SequenceEqual(
                        row.RecipeSourceDigests.OrderBy(value => value,
                            StringComparer.Ordinal),
                        StringComparer.Ordinal),
                    "Recipe-only census semantic digest join drifted: "
                        + row.DefinitionId);
            }
            subjects.Add(new ProductionAuthoredThroughputFacilitySubject(
                row.DefinitionId,
                row.WorkstationTag,
                ProductionFacilityCapacitySubjectAdapter
                    .CaptureWorkstationLaneProfile(definition),
                ProductionFacilityCapacitySubjectAdapter
                    .CaptureProcessFluidProfile(definition),
                joined,
                row.SpecialThroughputCandidates,
                row.SpecialThroughputGaps));
            recipes.AddRange(joined);
        }

        ProductionRecipeSO[] distinctRecipes = recipes
            .GroupBy(value => value.RecipeId, StringComparer.Ordinal)
            .Select(group => group.Single())
            .OrderBy(value => value.RecipeId, StringComparer.Ordinal)
            .ToArray();
        string workTypeSummary = string.Join("|", distinctRecipes
            .GroupBy(value => value.WorkTypeId.Value, StringComparer.Ordinal)
            .OrderBy(group => group.Key, StringComparer.Ordinal)
            .Select(group => group.Key + ":" + group.Count()));
        Check(producerRows.Length == 92
                && subjects.Count == 92
                && subjects.Count(value => value.Recipes.Count > 0) == 85
                && subjects.Count(value =>
                    value.SpecialCandidates.Count > 0) == 7
                && distinctRecipes.Length == 271,
            "PRODUCER_WIDE_SCOPE_EXACT",
            "facilities=" + subjects.Count + ";recipes="
                + distinctRecipes.Length + ";recipeKeys="
                + subjects.Count(value => value.Recipes.Count > 0)
                + ";specialKeys=" + subjects.Count(value =>
                    value.SpecialCandidates.Count > 0));
        Check(subjects.Sum(value => value.SpecialCandidates.Count) == 904
                && subjects.Sum(value => value.SpecialGaps.Count) == 0,
            "PRODUCER_WIDE_SPECIAL_BRANCHES_EXACT",
            "candidates=" + subjects.Sum(value =>
                value.SpecialCandidates.Count) + ";gaps="
                + subjects.Sum(value => value.SpecialGaps.Count));
        Check(string.Equals(
                workTypeSummary,
                "work:cook:28|work:craft:242|work:quarry:1",
                StringComparison.Ordinal),
            "RECIPE_ONLY_WORK_TYPE_SET_EXACT",
            workTypeSummary);
        Check(distinctRecipes.Count(value => value.ProcessKind
                    == ProductionProcessKind.WorkOnly) == 263
                && distinctRecipes.Count(value => value.ProcessKind
                    == ProductionProcessKind.PassiveBatch) == 8,
            "RECIPE_PROCESS_KIND_SET_EXACT",
            "workOnly=" + distinctRecipes.Count(value => value.ProcessKind
                == ProductionProcessKind.WorkOnly)
                + ";passiveBatch=" + distinctRecipes.Count(value =>
                    value.ProcessKind == ProductionProcessKind.PassiveBatch));

        IProductionMaximumOutputFactorCatalog factors =
            container.Resolve<IProductionMaximumOutputFactorCatalog>();
        int supportAssignments = distinctRecipes.Sum(value =>
            factors.CaptureFeasibleAssignments(value).Count);
        Check(supportAssignments == 278,
            "RECIPE_SUPPORT_ASSIGNMENTS_EXACT",
            "assignments=" + supportAssignments);

        ProductionThroughputTimeScaleSnapshot timeScale =
            ProductionThroughputTimeScaleAuthority.Capture();
        Check(timeScale.RealTimeMicrosecondsPerGameHour == 7_500_000L
                && timeScale.RealTimeSecondsPerGameHour == 7.5m,
            "GAME_CALENDAR_THROUGHPUT_TIME_SCALE_EXACT",
            "microsecondsPerGameHour="
                + timeScale.RealTimeMicrosecondsPerGameHour
                + ";sourceDigest=" + timeScale.SourceDigest);
        ProductionRecipeThroughputCycleProjector projector = new(
            factors,
            new ProductionRecipeThroughputBranchAuthority(
                container.Resolve<IProductionOutputMaximumMassRegistry>(),
                container.Resolve<
                    IProductionPassiveBatchOutputPortfolioQuery>()),
            container.Resolve<IProductionRecipeWorkRateMaximumQuery>(),
            timeScale);
        recipeCandidates.Clear();
        recipeGaps.Clear();
        foreach (ProductionAuthoredThroughputFacilitySubject subject in subjects)
        {
            ProductionRecipeThroughputProjectionResult result =
                projector.Capture(subject);
            Require(subject.Recipes.Count == 0
                    || result.Candidates.Count + result.Gaps.Count > 0,
                "Recipe-backed facility produced neither a candidate nor a typed gap: "
                    + subject.DefinitionId);
            recipeCandidates.AddRange(result.Candidates);
            recipeGaps.AddRange(result.Gaps);
        }

        ProductionAuthoredThroughputCoverageSnapshot coverage =
            new ProductionAuthoredThroughputEnvelopeAuthority(projector)
                .Capture(subjects);
        string[] expectedWithheld = Array.Empty<string>();
        string[] actualWithheld = recipeGaps
            .Select(value => value.DefinitionId + "/" + value.WorkstationTag)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        Check(recipeCandidates.Count == 278
                && recipeGaps.Count == 0
                && recipeCandidates.Count + recipeGaps.Count == 278,
            "RECIPE_PROJECTION_CANDIDATE_GAP_EXACT",
            "candidates=" + recipeCandidates.Count + ";gaps="
                + recipeGaps.Count + ";firstGap="
                + (recipeGaps.Count == 0
                    ? "none"
                    : recipeGaps[0].Reason + ":" + recipeGaps[0].Detail));
        Check(actualWithheld.SequenceEqual(expectedWithheld,
                    StringComparer.Ordinal),
            "PASSIVE_BATCH_PRODUCTIVE_BRANCHES_COMPLETE",
            "withheld=" + string.Join("|", actualWithheld));
        Check(coverage.CompleteEnvelopes.Count == 92
                && coverage.Gaps.Count == 0,
            "PRODUCER_WIDE_ENVELOPE_PUBLICATION_EXACT",
            "complete=" + coverage.CompleteEnvelopes.Count
                + ";withheld=0;gaps=" + coverage.Gaps.Count
                + ";sourceDigest=" + coverage.SourceDigest);
        Check(recipeCandidates.Select(value => value.SourceDigest)
                    .Distinct(StringComparer.Ordinal).Count()
                == recipeCandidates.Count
                && recipeGaps.Select(value => value.SourceDigest)
                    .Distinct(StringComparer.Ordinal).Count() == recipeGaps.Count,
            "RECIPE_PROJECTION_PROVENANCE_UNIQUE",
            "candidateDigests=" + recipeCandidates.Count
                + ";gapDigests=" + recipeGaps.Count);

        throughputEnvelopes.Clear();
        throughputEnvelopes.AddRange(coverage.CompleteEnvelopes);
        WriteProjectionCsv();
        WriteEnvelopeCsv(coverage.SourceDigest);
    }

    private void WriteEnvelopeCsv(string catalogSourceDigest)
    {
        V27BalanceArtifactWriter.WriteIfDifferent(
            ProductionWorkRateCompositionPlayModeVerifier.EnvelopeCsvPath,
            stream =>
            {
                using StreamWriter writer = new(
                    stream,
                    new UTF8Encoding(false, true),
                    8192,
                    leaveOpen: true);
                writer.NewLine = "\r\n";
                writer.WriteLine(
                    "schemaVersion,definitionId,workstationTag,peakOutputMassGramsPerHour,envelopeSourceDigest,catalogSourceDigest");
                foreach (ProductionOutputThroughputEnvelopeSnapshot value in
                         throughputEnvelopes
                             .OrderBy(envelope => envelope.DefinitionId,
                                 StringComparer.Ordinal)
                             .ThenBy(envelope => envelope.WorkstationTag,
                                 StringComparer.Ordinal))
                {
                    WriteProjectionField(
                        writer,
                        "v27-production-throughput-envelope-playmode@1");
                    writer.Write(',');
                    WriteProjectionField(writer, value.DefinitionId);
                    writer.Write(',');
                    WriteProjectionField(writer, value.WorkstationTag);
                    writer.Write(',');
                    writer.Write(value.PeakOutputMassGramsPerHour);
                    writer.Write(',');
                    writer.Write(value.SourceDigest);
                    writer.Write(',');
                    writer.Write(catalogSourceDigest);
                    writer.Write('\r');
                    writer.Write('\n');
                }
                writer.Flush();
            });
    }

    private void WriteClearanceMeasurementPlanCsv(string scopeSourceDigest)
    {
        V27BalanceArtifactWriter.WriteIfDifferent(
            ProductionWorkRateCompositionPlayModeVerifier
                .ClearanceMeasurementPlanCsvPath,
            stream =>
            {
                using StreamWriter writer = new(
                    stream,
                    new UTF8Encoding(false, true),
                    8192,
                    leaveOpen: true);
                writer.NewLine = "\r\n";
                writer.WriteLine(
                    "schemaVersion,definitionId,workstationTag,winnerSourceKind,winnerSourceCapabilityId,winnerProducerId,winnerBranchId,maximumSingleCompletionMassGrams,measurementCapabilityId,outputCapabilityIds,contextSourceDigest,planSourceDigest,scopeSourceDigest");
                foreach (ProductionOutputClearanceMeasurementPlan plan in
                         clearanceMeasurementPlans
                             .OrderBy(value => value.DefinitionId,
                                 StringComparer.Ordinal)
                             .ThenBy(value => value.WorkstationTag,
                                 StringComparer.Ordinal))
                {
                    ProductionOutputClearanceMeasurementCandidate winner =
                        plan.Winner;
                    WriteProjectionField(writer,
                        "v27-production-output-clearance-measurement-plan@1");
                    writer.Write(',');
                    WriteProjectionField(writer, plan.DefinitionId);
                    writer.Write(',');
                    WriteProjectionField(writer, plan.WorkstationTag);
                    writer.Write(',');
                    writer.Write((int)winner.Source.SourceKind);
                    writer.Write(',');
                    WriteProjectionField(writer,
                        winner.Source.SourceCapabilityId);
                    writer.Write(',');
                    WriteProjectionField(writer, winner.Source.ProducerId);
                    writer.Write(',');
                    WriteProjectionField(writer, winner.Source.BranchId);
                    writer.Write(',');
                    writer.Write(winner.Source.MaximumSingleCompletionMassGrams);
                    writer.Write(',');
                    WriteProjectionField(writer, winner.MeasurementCapabilityId);
                    writer.Write(',');
                    WriteProjectionField(writer, string.Join("|",
                        winner.Source.OutputCapabilityIds));
                    writer.Write(',');
                    writer.Write(plan.ContextSourceDigest);
                    writer.Write(',');
                    writer.Write(plan.SourceDigest);
                    writer.Write(',');
                    writer.Write(scopeSourceDigest);
                    writer.Write('\r');
                    writer.Write('\n');
                }
                writer.Flush();
            });
    }

    private static void WriteClearanceMeasurementPortfolioCsv(
        ProductionOutputClearanceMeasurementPortfolioSnapshot portfolio)
    {
        if (portfolio == null)
            throw new ArgumentNullException(nameof(portfolio));
        V27BalanceArtifactWriter.WriteIfDifferent(
            ProductionWorkRateCompositionPlayModeVerifier
                .ClearanceMeasurementPortfolioCsvPath,
            stream =>
            {
                using StreamWriter writer = new(
                    stream,
                    new UTF8Encoding(false, true),
                    16384,
                    leaveOpen: true);
                writer.NewLine = "\r\n";
                writer.WriteLine(
                    "schemaVersion,definitionId,workstationTag,seedIndex,deterministicSeed,observationId,measurementCapabilityId,winnerSourceKind,winnerSourceCapabilityId,winnerProducerId,winnerBranchId,maximumSingleCompletionMassGrams,outputCapabilityIds,planSourceDigest,fixtureSourceDigest,portfolioSourceDigest");
                foreach (ProductionOutputClearanceMeasurementFixture fixture in
                         portfolio.Fixtures)
                {
                    ProductionOutputClearanceMeasurementCandidate winner =
                        fixture.Winner;
                    WriteProjectionField(writer,
                        "v27-production-output-clearance-measurement-portfolio@1");
                    writer.Write(',');
                    WriteProjectionField(writer, fixture.Plan.DefinitionId);
                    writer.Write(',');
                    WriteProjectionField(writer, fixture.Plan.WorkstationTag);
                    writer.Write(',');
                    writer.Write(fixture.SeedIndex);
                    writer.Write(',');
                    writer.Write(fixture.DeterministicSeed);
                    writer.Write(',');
                    WriteProjectionField(writer, fixture.ObservationId);
                    writer.Write(',');
                    WriteProjectionField(writer, winner.MeasurementCapabilityId);
                    writer.Write(',');
                    writer.Write((int)winner.Source.SourceKind);
                    writer.Write(',');
                    WriteProjectionField(writer,
                        winner.Source.SourceCapabilityId);
                    writer.Write(',');
                    WriteProjectionField(writer, winner.Source.ProducerId);
                    writer.Write(',');
                    WriteProjectionField(writer, winner.Source.BranchId);
                    writer.Write(',');
                    writer.Write(winner.Source.MaximumSingleCompletionMassGrams);
                    writer.Write(',');
                    WriteProjectionField(writer, string.Join("|",
                        winner.Source.OutputCapabilityIds));
                    writer.Write(',');
                    writer.Write(fixture.Plan.SourceDigest);
                    writer.Write(',');
                    writer.Write(fixture.SourceDigest);
                    writer.Write(',');
                    writer.Write(portfolio.SourceDigest);
                    writer.Write('\r');
                    writer.Write('\n');
                }
                writer.Flush();
            });
    }

    private void VerifyClearanceObservationPortfolioGate(
        ProductionOutputClearanceMeasurementPortfolioSnapshot portfolio)
    {
        int expectedObservationCount = portfolio.Fixtures.Count;
        ProductionOutputClearanceNaturalObservationRecord[] records = portfolio
            .Fixtures.Select(CreateSyntheticObservation)
            .ToArray();
        ProductionOutputClearanceNaturalObservationPortfolioSnapshot accepted =
            ProductionOutputClearanceNaturalObservationPortfolioSnapshot.Build(
                portfolio,
                records);
        bool missingRejected = Rejects(() =>
            ProductionOutputClearanceNaturalObservationPortfolioSnapshot.Build(
                portfolio,
                records.Take(records.Length - 1).ToArray()));
        ProductionOutputClearanceNaturalObservationRecord[] sharedCommit =
            records.ToArray();
        sharedCommit[1] = CreateSyntheticObservation(
            portfolio.Fixtures[1],
            records[0].BatchCommitId);
        bool sharedCommitAcrossActionsAccepted = !Rejects(() =>
            ProductionOutputClearanceNaturalObservationPortfolioSnapshot.Build(
                portfolio,
                sharedCommit));
        ProductionOutputClearanceNaturalObservationRecord[] duplicateObservation =
            records.ToArray();
        duplicateObservation[1] = records[0];
        bool duplicateObservationRejected = Rejects(() =>
            ProductionOutputClearanceNaturalObservationPortfolioSnapshot.Build(
                portfolio,
                duplicateObservation));
        ProductionOutputClearanceMeasurementFixture subMaximumFixture =
            portfolio.Fixtures.First(value => value.Winner.Source
                .MaximumSingleCompletionMassGrams > 1L);
        bool positiveSubMaximumAccepted = !RejectsInvalidObservation(() =>
            CreateSyntheticObservation(
                subMaximumFixture,
                null,
                subMaximumFixture.Winner.Source
                    .MaximumSingleCompletionMassGrams - 1L));
        bool zeroMassRejected = RejectsInvalidObservation(() =>
            CreateSyntheticObservation(portfolio.Fixtures[0], null, 0L));
        bool aboveMaximumRejected = RejectsInvalidObservation(() =>
            CreateSyntheticObservation(
                portfolio.Fixtures[0],
                null,
                checked(portfolio.Fixtures[0].Winner.Source
                    .MaximumSingleCompletionMassGrams + 1L)));
        Check(accepted.Records.Count == expectedObservationCount
                && accepted.ProfileObservations.Count == expectedObservationCount
                && missingRejected
                && sharedCommitAcrossActionsAccepted
                && duplicateObservationRejected
                && positiveSubMaximumAccepted
                && zeroMassRejected
                && aboveMaximumRejected,
            "OUTPUT_CLEARANCE_NATURAL_OBSERVATION_GATE_STRUCTURAL",
            "accepted=" + accepted.Records.Count
                + ";profileInputs=" + accepted.ProfileObservations.Count
                + ";missingRejected=" + missingRejected
                + ";sharedCommitAcrossActionsAccepted="
                + sharedCommitAcrossActionsAccepted
                + ";duplicateObservationRejected="
                + duplicateObservationRejected
                + ";positiveSubMaximumAccepted="
                + positiveSubMaximumAccepted
                + ";zeroMassRejected=" + zeroMassRejected
                + ";aboveMaximumRejected=" + aboveMaximumRejected
                + ";sourceDigest=" + accepted.SourceDigest);
    }

    private static ProductionOutputClearanceNaturalObservationRecord
        CreateSyntheticObservation(
            ProductionOutputClearanceMeasurementFixture fixture) =>
        CreateSyntheticObservation(fixture, null);

    private static ProductionOutputClearanceNaturalObservationRecord
        CreateSyntheticObservation(
            ProductionOutputClearanceMeasurementFixture fixture,
            string batchCommitIdOverride,
            long? actualBatchMassGramsOverride = null)
    {
        string batchCommitId = batchCommitIdOverride
            ?? "batch:structural-clearance:"
            + fixture.Plan.DefinitionId + ":"
            + fixture.DeterministicSeed;
        return new ProductionOutputClearanceNaturalObservationRecord(
            fixture,
            "facility:structural-clearance:" + fixture.Plan.DefinitionId,
            fixture.SourceDigest,
            actualBatchMassGramsOverride
                ?? fixture.Winner.Source.MaximumSingleCompletionMassGrams,
            batchCommitId,
            fixture.Plan.ContextSourceDigest,
            topologyStable: true,
            facilityAttributionExact: true,
            ownerRosterKey: "owner-roster:structural-clearance",
            actionEpochDelta: 1L,
            actionStartDelta: 1L,
            haulStartDelta: 1L,
            clearanceMicroHours: checked(1_000L + fixture.SeedIndex),
            telemetryCompletedCount: 1,
            telemetryActiveCount: 0,
            orphanPickupCount: 0,
            conflictingPublicationCount: 0,
            overPickupCount: 0,
            capacityExceededCount: 0,
            restoreInterruptionCount: 0,
            telemetryClean: true,
            schedulerProvenanceExact: true,
            deliveryExact: true,
            randomStateDigest: fixture.SourceDigest,
            randomDrawDelta: 0L);
    }

    private static bool Rejects(Action action)
    {
        try
        {
            action();
            return false;
        }
        catch (InvalidOperationException)
        {
            return true;
        }
    }

    private static bool RejectsInvalidObservation(Action action)
    {
        try
        {
            action();
            return false;
        }
        catch (ArgumentOutOfRangeException)
        {
            return true;
        }
    }

    private void WriteProjectionCsv()
    {
        V27BalanceArtifactWriter.WriteIfDifferent(
            ProductionWorkRateCompositionPlayModeVerifier.ProjectionCsvPath,
            stream =>
            {
                using StreamWriter writer = new(
                    stream,
                    new UTF8Encoding(false, true),
                    16384,
                    leaveOpen: true);
                writer.NewLine = "\r\n";
                writer.WriteLine(
                    "recordKind,definitionId,workstationTag,producerId,branchId,executionPath,bottleneck,maximumOutputMassGrams,cyclesPerGameHour,peakOutputMassGramsPerHour,gapReason,gapDetail,sourceDigest");
                foreach (ProductionRecipeThroughputCycleCandidateSnapshot value in
                         recipeCandidates
                             .OrderBy(candidate => candidate.DefinitionId,
                                 StringComparer.Ordinal)
                             .ThenBy(candidate => candidate.WorkstationTag,
                                 StringComparer.Ordinal)
                             .ThenBy(candidate => candidate.RecipeId,
                                 StringComparer.Ordinal)
                             .ThenBy(candidate => candidate.BranchId,
                                 StringComparer.Ordinal)
                             .ThenBy(candidate =>
                                 candidate.SupportAssignmentSourceDigest,
                                 StringComparer.Ordinal))
                {
                    WriteProjectionField(writer, "candidate");
                    writer.Write(',');
                    WriteProjectionField(writer, value.DefinitionId);
                    writer.Write(',');
                    WriteProjectionField(writer, value.WorkstationTag);
                    writer.Write(',');
                    WriteProjectionField(writer, value.RecipeId);
                    writer.Write(',');
                    WriteProjectionField(writer, value.BranchId);
                    writer.Write(',');
                    writer.Write((int)value.ExecutionPath);
                    writer.Write(',');
                    writer.Write((int)value.Bottleneck);
                    writer.Write(',');
                    writer.Write(value.MaximumOutputMassGrams);
                    writer.Write(',');
                    WriteProjectionField(writer, value.CyclesPerGameHourToken);
                    writer.Write(',');
                    writer.Write(value.PeakOutputMassGramsPerHour);
                    writer.Write(',');
                    writer.Write(',');
                    writer.Write(',');
                    writer.Write(value.SourceDigest);
                    writer.Write('\r');
                    writer.Write('\n');
                }
                foreach (ProductionThroughputCoverageGap value in recipeGaps
                             .OrderBy(gap => gap.DefinitionId,
                                 StringComparer.Ordinal)
                             .ThenBy(gap => gap.WorkstationTag,
                                 StringComparer.Ordinal)
                             .ThenBy(gap => gap.ProducerId,
                                 StringComparer.Ordinal)
                             .ThenBy(gap => gap.BranchId,
                                 StringComparer.Ordinal))
                {
                    WriteProjectionField(writer, "gap");
                    writer.Write(',');
                    WriteProjectionField(writer, value.DefinitionId);
                    writer.Write(',');
                    WriteProjectionField(writer, value.WorkstationTag);
                    writer.Write(',');
                    WriteProjectionField(writer, value.ProducerId);
                    writer.Write(',');
                    WriteProjectionField(writer, value.BranchId);
                    writer.Write(",,,,,,");
                    writer.Write((int)value.Reason);
                    writer.Write(',');
                    WriteProjectionField(writer, value.Detail);
                    writer.Write(',');
                    writer.Write(value.SourceDigest);
                    writer.Write('\r');
                    writer.Write('\n');
                }
                writer.Flush();
            });
    }

    private static void WriteProjectionField(StreamWriter writer, string value) =>
        V27BalanceCsvSerializer.WriteEscapedField(
            writer,
            (value ?? string.Empty).AsSpan());

    private void CaptureLog(string condition, string stackTrace, LogType type)
    {
        if (type != LogType.Warning
            && type != LogType.Error
            && type != LogType.Exception
            && type != LogType.Assert)
        {
            return;
        }
        unexpectedLogs.Add(type + ":" + condition);
    }

    private void Check(bool condition, string id, string detail)
    {
        if (condition)
        {
            checks.Add(id + "=PASS;" + detail);
            return;
        }
        failures.Add(id + "=FAIL;" + detail);
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    private void WriteReport()
    {
        StringBuilder report = new();
        report.Append("schema=v27-production-work-rate-composition-playmode@3\n");
        foreach (string check in checks.OrderBy(value => value,
                     StringComparer.Ordinal))
            report.Append(check).Append('\n');
        foreach (string failure in failures.OrderBy(value => value,
                     StringComparer.Ordinal))
            report.Append("FAILURE=").Append(failure.Replace('\r', ' ')
                .Replace('\n', ' ')).Append('\n');
        report.Append("RESULT=").Append(failures.Count == 0 ? "PASS" : "FAIL")
            .Append("; checks=").Append(checks.Count)
            .Append("; failures=").Append(failures.Count).Append('\n');
        byte[] bytes = new UTF8Encoding(false, true).GetBytes(report.ToString());
        V27BalanceArtifactWriter.WriteIfDifferent(
            ProductionWorkRateCompositionPlayModeVerifier.ReportPath,
            stream => stream.Write(bytes, 0, bytes.Length));
    }
}
#endif
