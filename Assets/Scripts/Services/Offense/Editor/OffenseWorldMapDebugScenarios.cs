using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public static class OffenseWorldMapDebugScenarios
{
    [MenuItem("DungeonStory/Debug/Offense/Run P3 World Map Scenarios")]
    public static void RunFromMenu()
    {
        bool success = RunAll(true);
        if (!success)
        {
            Debug.LogError("P3 offense world map scenarios failed.");
        }
    }

    public static bool RunAll(bool logSuccess)
    {
        List<string> errors = new List<string>();

        RunScenario("초기 가까운 원정 대상 공개", VerifyInitialNearbyTargets, errors);
        RunScenario("정찰 강화로 원정 대상 추가 공개", VerifyReconUpgradeRevealsMoreTargets, errors);
        RunScenario("원정 대상 정보 필드", VerifyTargetRequirementFields, errors);
        RunScenario("세 전략 원정 설계도 고정 연결", VerifyStrategyBlueprintRoutes, errors);
        RunScenario("원정 대상 선택 이벤트", VerifyTargetSelectionEvent, errors);
        RunScenario("오펜스 캠페인 선행 목표 잠금", VerifyCampaignPrerequisiteChain, errors);
        RunScenario("최종 오펜스 진실 공개", VerifyTerminalTruthReveal, errors);
        RunScenario("Strategic 진실 귀환 승리 연결", VerifyStrategicTruthReveal, errors);
        RunScenario("월드맵 읽기 경계", VerifyReadOnlyWorldMapBoundary, errors);
        RunScenario("월드맵 패널 생성", VerifyPanelCreation, errors);

        if (errors.Count > 0)
        {
            foreach (string error in errors)
            {
                Debug.LogError(error);
            }

            return false;
        }

        if (logSuccess)
        {
            Debug.Log("P3 offense world map scenarios passed.");
        }

        return true;
    }

    private static void RunScenario(string name, Func<bool> scenario, List<string> errors)
    {
        try
        {
            if (scenario()) return;
        }
        catch (Exception ex)
        {
            Debug.LogException(ex);
        }

        errors.Add(name);
    }

    private static bool VerifyInitialNearbyTargets()
    {
        using ScenarioRuntime scenario = new ScenarioRuntime();
        IReadOnlyList<OffenseTargetSnapshot> visible = scenario.Runtime.VisibleTargets;

        return scenario.Runtime.State.ReconLevel == 0
            && visible.Count >= 2
            && visible.All((target) => target.distance <= OffenseWorldMapService.GetScanRange(0))
            && visible.Any((target) => target.id == "food_farm")
            && visible.Any((target) => target.id == "merchant_road")
            && visible.All((target) => target.id != "mana_ruins");
    }

    private static bool VerifyReconUpgradeRevealsMoreTargets()
    {
        using ScenarioRuntime scenario = new ScenarioRuntime();
        int initialCount = scenario.Runtime.VisibleTargets.Count;
        bool upgraded = scenario.Runtime.TryUpgradeRecon(out string message);
        int afterFirstUpgrade = scenario.Runtime.VisibleTargets.Count;

        scenario.Runtime.TryUpgradeRecon(out _);
        int afterSecondUpgrade = scenario.Runtime.VisibleTargets.Count;

        return upgraded
            && message.Contains("정찰 Lv.1")
            && afterFirstUpgrade > initialCount
            && afterSecondUpgrade > afterFirstUpgrade
            && scenario.Runtime.VisibleTargets.Any((target) => target.id == "mana_ruins");
    }

    private static bool VerifyTargetRequirementFields()
    {
        using ScenarioRuntime scenario = new ScenarioRuntime();
        scenario.Runtime.TryUpgradeRecon(out _);

        OffenseTargetSnapshot target = scenario.Runtime.VisibleTargets.FirstOrDefault((candidate) => candidate.id == "old_armory");
        return target != null
            && target.danger > 0f
            && target.durationSeconds > 0f
            && target.requiredMembers >= 1
            && target.requiredPower > 0f
            && target.rewards.Count > 0
            && target.ToDetailText().Contains("필요 인력");
    }

    private static bool VerifyTargetSelectionEvent()
    {
        using ScenarioRuntime scenario = new ScenarioRuntime();
        using CountingSelectionListener selections = new CountingSelectionListener(scenario.Runtime);

        bool selected = scenario.Runtime.TrySelectTarget("food_farm", out OffenseTargetSnapshot snapshot, out string message);
        return selected
            && snapshot != null
            && message.Contains(snapshot.title)
            && scenario.Runtime.State.SelectedTargetId == "food_farm"
            && selections.Count == 1
            && selections.LastTargetId == "food_farm";
    }

    private static bool VerifyStrategyBlueprintRoutes()
    {
        IReadOnlyList<OffenseTargetDefinition> targets =
            OffenseEditorTestDependencies.CreateCampaignCatalog().Targets;
        return HasSpecificBlueprint(targets, "merchant_road", OffenseStrategyBlueprintIds.CommerceLogistics)
            && HasSpecificBlueprint(targets, "old_armory", OffenseStrategyBlueprintIds.FortressDefense)
            && HasSpecificBlueprint(targets, "mana_ruins", OffenseStrategyBlueprintIds.ArcaneResearch);
    }

    private static bool VerifyCampaignPrerequisiteChain()
    {
        using ScenarioRuntime scenario = new ScenarioRuntime();
        OffenseTargetSnapshot first = scenario.Runtime.VisibleTargets
            .FirstOrDefault(target => target.id == "food_farm");
        OffenseTargetSnapshot second = scenario.Runtime.VisibleTargets
            .FirstOrDefault(target => target.id == "merchant_road");
        bool skipped = scenario.Runtime.TrySelectTarget(
            "merchant_road",
            out _,
            out string lockedMessage);
        bool completed = scenario.Runtime.TryRecordSuccessfulExpedition(
            "food_farm",
            out OffenseTargetSnapshot completedFirst,
            out _);
        second = scenario.Runtime.VisibleTargets.FirstOrDefault(target => target.id == "merchant_road");
        bool duplicate = scenario.Runtime.TryRecordSuccessfulExpedition(
            "food_farm",
            out _,
            out string duplicateMessage);

        return first != null
            && first.isAvailable
            && second != null
            && !skipped
            && lockedMessage.Contains("앞선")
            && completed
            && completedFirst.isCompleted
            && scenario.Runtime.State.CompletedTargetCount == 1
            && second.isAvailable
            && !duplicate
            && duplicateMessage.Contains("이미 완료");
    }

    private static bool VerifyTerminalTruthReveal()
    {
        using ScenarioRuntime scenario = new ScenarioRuntime();
        using CountingTruthListener truthListener = new CountingTruthListener(scenario.GameEvents);
        while (scenario.Runtime.State.ReconLevel < OffenseWorldMapService.MaxReconLevel)
        {
            scenario.Runtime.TryUpgradeRecon(out _);
        }

        IReadOnlyList<OffenseTargetDefinition> route = scenario.Runtime.TargetDefinitions
            .OrderBy(target => target.campaignOrder)
            .ToList();
        bool allCompleted = true;
        for (int i = 0; i < route.Count; i++)
        {
            allCompleted &= scenario.Runtime.TryRecordSuccessfulExpedition(
                route[i].id,
                out OffenseTargetSnapshot completed,
                out _);
            if (i < route.Count - 1
                && (truthListener.Count != 0 || scenario.Runtime.State.TruthRevealed))
            {
                return false;
            }

            allCompleted &= completed != null && completed.isCompleted;
        }

        return allCompleted
            && route.Count == 6
            && route[route.Count - 1].id == OffenseWorldMapService.TruthTargetId
            && scenario.Runtime.State.TruthRevealed
            && scenario.Runtime.State.RevealedTruthTargetId == OffenseWorldMapService.TruthTargetId
            && scenario.Runtime.State.CompletedTargetCount == route.Count
            && truthListener.Count == 1
            && truthListener.TruthText.Contains("지상의 왕국");
    }

    private static bool VerifyStrategicTruthReveal()
    {
        using ScenarioRuntime scenario = new ScenarioRuntime(realOutcomes: true);
        using CountingTruthListener truthListener =
            new CountingTruthListener(scenario.GameEvents);
        bool revealed = scenario.Runtime.TryRecordStrategicTruthReveal(
            OffenseWorldMapService.TruthTargetId,
            out string message);
        bool repeated = scenario.Runtime.TryRecordStrategicTruthReveal(
            OffenseWorldMapService.TruthTargetId,
            out _);
        GameplayResultKey expected = new(
            ExternalFactionOutcomeIds.TruthRevealProducerId,
            new GameplayOperationId(
                "offense-truth-reveal:"
                + OffenseWorldMapService.TruthTargetId),
            1L,
            0);

        return revealed
            && repeated
            && message.Contains("진실")
            && scenario.Runtime.State.TruthRevealed
            && scenario.Runtime.State.CompletedTargetCount == 1
            && truthListener.Count == 1
            && scenario.OutcomeLedger != null
            && scenario.OutcomeLedger.TryGetResultIdentity(
                expected,
                out GameplayOutcomeReplayIdentity identity)
            && identity.State == GameplayOutcomeReplayState.PublishedAcknowledged;
    }

    private static bool HasSpecificBlueprint(
        IEnumerable<OffenseTargetDefinition> targets,
        string targetId,
        int blueprintId)
    {
        return targets.FirstOrDefault(target => target.id == targetId)?.rewards
            .Select(reward => reward?.GrantSpec)
            .OfType<OffenseSpecificBlueprintRewardSpec>()
            .Any(spec => spec.BlueprintId == blueprintId) == true;
    }

    private static bool VerifyReadOnlyWorldMapBoundary()
    {
        OffenseTargetDefinition source = new OffenseTargetDefinition
        {
            id = "boundary_target",
            title = "원본 제목",
            description = "원본 설명",
            kind = OffenseTargetKind.ResourceSite,
            distance = 1f,
            danger = 3f,
            durationSeconds = 20f,
            requiredMembers = 1,
            requiredPower = 1f,
            campaignOrder = 1,
            revealsTruth = true,
            truthText = "Boundary truth",
            rewards = new[]
            {
                new OffenseRewardPreview("원본 보상", 1, new OffenseMoneyRewardSpec())
            }
        };
        using ScenarioRuntime scenario = new ScenarioRuntime(
            new TestCampaignCatalog(new[] { source }));
        source.title = "외부에서 변경한 제목";
        source.rewards[0] = new OffenseRewardPreview("외부 변경", 99, new OffenseMoneyRewardSpec());

        OffenseTargetSnapshot visible = scenario.Runtime.VisibleTargets.Single();
        bool rewardMutationRejected = false;
        if (visible.rewards is IList<string> rewards)
        {
            try
            {
                rewards[0] = "침범";
            }
            catch (NotSupportedException)
            {
                rewardMutationRejected = true;
            }
        }

        return visible.title == "원본 제목"
            && visible.rewards.Single() == "원본 보상 x1"
            && rewardMutationRejected
            && scenario.Runtime.State.KnownTargetIds is not HashSet<string>;
    }

    private static bool VerifyPanelCreation()
    {
        using ScenarioRuntime scenario = new ScenarioRuntime();
        OffenseWorldMapPanel panel = scenario.PanelService.ShowWorldMap();

        bool valid = panel != null
            && panel.gameObject.activeSelf
            && Object.FindFirstObjectByType<Canvas>() != null;

        if (panel != null)
        {
            Object.DestroyImmediate(panel.transform.root.gameObject);
        }

        return valid;
    }

    private sealed class ScenarioRuntime : IDisposable
    {
        private readonly GameObject runtimeObject;
        private readonly EditorOffenseTruthFactFixture truthFacts;

        public OffenseWorldMapRuntime Runtime { get; }
        public DungeonStory.Foundation.IGameEventBus GameEvents { get; }
        public IOffensePanelService PanelService { get; }
        public GameplayOutcomeLedger OutcomeLedger { get; }

        public ScenarioRuntime(
            IOffenseCampaignCatalog catalog = null,
            bool realOutcomes = false)
        {
            GameEvents = new DungeonStory.Foundation.GameEventBus();
            runtimeObject = new GameObject("Offense World Map Scenario Runtime");
            Runtime = runtimeObject.AddComponent<OffenseWorldMapRuntime>();
            PanelService = new TestPanelService();
            OffenseCampaignRuntime campaign = new OffenseCampaignRuntime();
            _ = realOutcomes;
            truthFacts = new EditorOffenseTruthFactFixture(
                GameEvents,
                "run:offense-truth-reveal-editor");
            OutcomeLedger = truthFacts.Ledger;
            Runtime.Construct(
                new DungeonStory.Foundation.UnityUiClock(),
                GameEvents,
                externalInfluence: null,
                campaign,
                campaign,
                catalog ?? OffenseEditorTestDependencies.CreateCampaignCatalog(),
                truthFacts.Facts,
                truthFacts.Facts,
                EditorFixedGameCalendar.Instance);
            Runtime.StartWorldMap();
        }

        public void Dispose()
        {
            truthFacts.Dispose();
            Object.DestroyImmediate(runtimeObject);
        }
    }

    private sealed class TestCampaignCatalog : IOffenseCampaignCatalog
    {
        private readonly IReadOnlyList<OffenseTargetDefinition> targets;

        public TestCampaignCatalog(
            IEnumerable<OffenseTargetDefinition> definitions)
        {
            OffenseCampaignCatalogSO asset =
                ScriptableObject.CreateInstance<OffenseCampaignCatalogSO>();
            try
            {
                asset.SetDefinitionsForMigration(definitions
                    ?? throw new ArgumentNullException(nameof(definitions)));
                targets = asset.CreateRuntimeDefinitions();
            }
            finally
            {
                Object.DestroyImmediate(asset);
            }
        }

        public IReadOnlyList<OffenseTargetDefinition> Targets => targets;

        public bool TryGet(
            string targetId,
            out OffenseTargetDefinition definition)
        {
            OffenseTargetDefinition value = targets.FirstOrDefault(candidate =>
                string.Equals(candidate.id, targetId,
                    StringComparison.Ordinal));
            definition = value;
            return definition != null;
        }
    }

    private sealed class TestPanelService : IOffensePanelService
    {
        public OffenseWorldMapPanel ShowWorldMap()
        {
            GameObject canvasObject = new GameObject("World Map Test Canvas", typeof(Canvas));
            GameObject panelObject = new GameObject("World Map Test Panel");
            panelObject.transform.SetParent(canvasObject.transform, false);
            return panelObject.AddComponent<OffenseWorldMapPanel>();
        }

        public OffenseExpeditionPanel ShowExpedition(OffenseExpeditionRuntime runtime)
        {
            return null;
        }
    }

    private sealed class CountingSelectionListener : IDisposable
    {
        private OffenseWorldMapRuntime runtime;
        public int Count { get; private set; }
        public string LastTargetId { get; private set; }

        public CountingSelectionListener(OffenseWorldMapRuntime runtime)
        {
            this.runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
            this.runtime.TargetSelected += OnTargetSelected;
        }

        private void OnTargetSelected(OffenseTargetSnapshot target)
        {
            if (target == null) return;

            Count++;
            LastTargetId = target.id;
        }

        public void Dispose()
        {
            if (runtime == null) return;

            runtime.TargetSelected -= OnTargetSelected;
            runtime = null;
        }
    }

    private sealed class CountingTruthListener : IDisposable
    {
        private readonly IDisposable subscription;
        public int Count { get; private set; }
        public string TruthText { get; private set; }

        public CountingTruthListener(DungeonStory.Foundation.IGameEventBus gameEventBus)
        {
            subscription = gameEventBus.Subscribe<OffenseTruthRevealedEvent>(OnTriggerEvent);
        }

        public void OnTriggerEvent(OffenseTruthRevealedEvent eventType)
        {
            Count++;
            TruthText = eventType.truthText;
        }

        public void Dispose()
        {
            subscription.Dispose();
        }
    }
}

public static class SoReferenceRemediationLeafADebugScenarios
{
    private const string ValidItemId = "resource:log";
    private const string ValidGolemMaterialItemId = "resource:mana-crystal";
    private const string CaseVariantSpeciesId = "sLiMe";
    private const string MissingStringId = "missing:so-reference-remediation-leaf-a";
    private const int MissingBlueprintId = int.MaxValue;
    private static readonly int[] CurrentBlueprintIds = { 6191, 6192, 6193 };

    [MenuItem("DungeonStory/Debug/Content/Run SO Reference Remediation Leaf A")]
    public static void RunFromMenu()
    {
        ValidateOrThrow();
        Debug.Log(
            "SO reference remediation leaf A passed: fuel and golem recharge "
            + "materials, species affinity, environmental workwear, "
            + "and specific blueprint references.");
    }

    public static void ValidateOrThrow()
    {
        ResourceGameContentCatalog content = new(
            new UnityGameContentRootLoader());
        ContentReferenceCatalog catalog =
            ContentReferenceCatalog.FromRoot(content.Root);

        ValidateFuelReference(catalog);
        ValidateGolemRechargeReference(catalog);
        ValidateSpeciesAffinityReferences(catalog);
        ValidateEnvironmentalWorkwearReference(catalog);
        ValidateSpecificBlueprintReference(catalog);
    }

    private static void ValidateFuelReference(ContentReferenceCatalog catalog)
    {
        ContentReferenceAttribute reference = RequireReference(
            typeof(BuildingFuelConsumerAbility),
            nameof(BuildingFuelConsumerAbility.fuelItemId),
            ContentReferenceKind.Item,
            ignoreCase: false);
        RequireSelectable(catalog, reference, ValidItemId);
        RequireNotSelectable(catalog, reference, MissingStringId);
        RequireBuildingAccepted(
            catalog,
            new BuildingFuelConsumerAbility { fuelItemId = ValidItemId },
            "registered fuel item");
        RequireBuildingRejected(
            catalog,
            new BuildingFuelConsumerAbility { fuelItemId = MissingStringId },
            MissingStringId,
            "unregistered fuel item");
    }

    private static void ValidateGolemRechargeReference(
        ContentReferenceCatalog catalog)
    {
        ContentReferenceAttribute reference = RequireReference(
            typeof(BuildingGolemRechargeAbility),
            nameof(BuildingGolemRechargeAbility.materialItemId),
            ContentReferenceKind.Item,
            ignoreCase: false);
        RequireSelectable(catalog, reference, ValidGolemMaterialItemId);
        RequireNotSelectable(catalog, reference, MissingStringId);
        RequireBuildingAccepted(
            catalog,
            new BuildingGolemRechargeAbility
            {
                materialItemId = ValidGolemMaterialItemId
            },
            "registered golem recharge material");
        RequireBuildingRejected(
            catalog,
            new BuildingGolemRechargeAbility
            {
                materialItemId = MissingStringId
            },
            MissingStringId,
            "mistyped golem recharge material");

        SoReferenceRemediationLeafAProbe source =
            ScriptableObject.CreateInstance<SoReferenceRemediationLeafAProbe>();
        SoReferenceRemediationLeafAProbe restored =
            ScriptableObject.CreateInstance<SoReferenceRemediationLeafAProbe>();
        try
        {
            source.name = "SO Reference Remediation Leaf A Golem Source";
            source.ConfigureBuilding(new BuildingGolemRechargeAbility
            {
                materialItemId = ValidGolemMaterialItemId
            });
            string json = EditorJsonUtility.ToJson(source);
            EditorJsonUtility.FromJsonOverwrite(json, restored);
            RequireAccepted(
                catalog.Validate(new ScriptableObject[] { restored }),
                "golem recharge material JSON restore");
            RequireAccepted(
                catalog.Validate(new ScriptableObject[] { restored }),
                "golem recharge material registration retry");
            if (!string.Equals(
                    EditorJsonUtility.ToJson(restored),
                    json,
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "Golem recharge reference validation mutated restored JSON.");
            }
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(restored);
            UnityEngine.Object.DestroyImmediate(source);
        }
    }

    private static void ValidateSpeciesAffinityReferences(
        ContentReferenceCatalog catalog)
    {
        ContentReferenceAttribute preferredReference = RequireReference(
            typeof(BuildingSpeciesAffinityAbility),
            nameof(BuildingSpeciesAffinityAbility.preferredTags),
            ContentReferenceKind.Species,
            ignoreCase: true);
        ContentReferenceAttribute dislikedReference = RequireReference(
            typeof(BuildingSpeciesAffinityAbility),
            nameof(BuildingSpeciesAffinityAbility.dislikedTags),
            ContentReferenceKind.Species,
            ignoreCase: true);

        RequireSelectable(catalog, preferredReference, CaseVariantSpeciesId);
        RequireSelectable(catalog, dislikedReference, CaseVariantSpeciesId);
        RequireNotSelectable(catalog, preferredReference, MissingStringId);
        RequireNotSelectable(catalog, dislikedReference, MissingStringId);
        RequireBuildingAccepted(
            catalog,
            new BuildingSpeciesAffinityAbility
            {
                preferredTags = new[] { CaseVariantSpeciesId }
            },
            "case-variant preferred species");
        RequireBuildingAccepted(
            catalog,
            new BuildingSpeciesAffinityAbility
            {
                dislikedTags = new[] { CaseVariantSpeciesId }
            },
            "case-variant disliked species");
        RequireBuildingRejected(
            catalog,
            new BuildingSpeciesAffinityAbility
            {
                preferredTags = new[] { MissingStringId }
            },
            MissingStringId,
            "unregistered preferred species");
        RequireBuildingRejected(
            catalog,
            new BuildingSpeciesAffinityAbility
            {
                dislikedTags = new[] { MissingStringId }
            },
            MissingStringId,
            "unregistered disliked species");
    }

    private static void ValidateEnvironmentalWorkwearReference(
        ContentReferenceCatalog catalog)
    {
        ContentReferenceAttribute reference = RequireReference(
            typeof(EnvironmentalWorkwearSO),
            "allowedSpecies",
            ContentReferenceKind.Species,
            ignoreCase: true);
        RequireSelectable(catalog, reference, CaseVariantSpeciesId);
        RequireNotSelectable(catalog, reference, MissingStringId);
        RequireWorkwearAccepted(
            catalog,
            new[] { CaseVariantSpeciesId },
            "case-variant environmental workwear species");
        RequireWorkwearRejected(
            catalog,
            new[] { MissingStringId },
            MissingStringId,
            "unregistered environmental workwear species");
    }

    private static void ValidateSpecificBlueprintReference(
        ContentReferenceCatalog catalog)
    {
        ContentReferenceAttribute reference = RequireReference(
            typeof(OffenseSpecificBlueprintRewardSpec),
            "blueprintId",
            ContentReferenceKind.Definition,
            ignoreCase: false,
            definitionTypeName: "FacilityBlueprintSO",
            idMemberName: "id",
            emptyNumericId: -1);
        foreach (int blueprintId in CurrentBlueprintIds)
        {
            string id = blueprintId.ToString(
                System.Globalization.CultureInfo.InvariantCulture);
            RequireSelectable(catalog, reference, id);
            RequireOffenseRewardAccepted(
                catalog,
                new OffenseSpecificBlueprintRewardSpec(blueprintId),
                $"registered blueprint {id}");
        }

        string missingId = MissingBlueprintId.ToString(
            System.Globalization.CultureInfo.InvariantCulture);
        RequireNotSelectable(catalog, reference, missingId);
        RequireOffenseRewardRejected(
            catalog,
            new OffenseSpecificBlueprintRewardSpec(MissingBlueprintId),
            missingId,
            "unregistered specific blueprint");
    }

    private static ContentReferenceAttribute RequireReference(
        Type ownerType,
        string fieldName,
        ContentReferenceKind expectedKind,
        bool ignoreCase,
        string definitionTypeName = null,
        string idMemberName = null,
        int emptyNumericId = -1)
    {
        FieldInfo field = ownerType.GetField(
            fieldName,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException(
                $"{ownerType.Name}.{fieldName} field is missing.");
        ContentReferenceAttribute reference =
            field.GetCustomAttribute<ContentReferenceAttribute>()
            ?? throw new InvalidOperationException(
                $"{ownerType.Name}.{fieldName} has no ContentReference metadata.");
        if (reference.Kind != expectedKind
            || reference.IgnoreCase != ignoreCase
            || !string.Equals(
                reference.DefinitionTypeName,
                definitionTypeName,
                StringComparison.Ordinal)
            || !string.Equals(
                reference.IdMemberName,
                idMemberName,
                StringComparison.Ordinal)
            || reference.EmptyNumericId != emptyNumericId)
        {
            throw new InvalidOperationException(
                $"{ownerType.Name}.{fieldName} ContentReference metadata is not the expected contract.");
        }

        return reference;
    }

    private static void RequireSelectable(
        ContentReferenceCatalog catalog,
        ContentReferenceAttribute reference,
        string id)
    {
        int count = catalog.Entries(reference).Count(entry =>
            ContentReferenceCatalog.Matches(reference, entry.Id, id));
        if (count != 1)
        {
            throw new InvalidOperationException(
                $"Expected one selectable {reference.Kind} target for '{id}', found {count}.");
        }
    }

    private static void RequireNotSelectable(
        ContentReferenceCatalog catalog,
        ContentReferenceAttribute reference,
        string id)
    {
        if (catalog.Entries(reference).Any(entry =>
                ContentReferenceCatalog.Matches(reference, entry.Id, id)))
        {
            throw new InvalidOperationException(
                $"Unexpected selectable {reference.Kind} target for '{id}'.");
        }
    }

    private static void RequireBuildingAccepted(
        ContentReferenceCatalog catalog,
        BuildingAbility ability,
        string description)
    {
        IReadOnlyList<string> errors = ValidateBuilding(catalog, ability);
        RequireAccepted(errors, description);
    }

    private static void RequireBuildingRejected(
        ContentReferenceCatalog catalog,
        BuildingAbility ability,
        string missingId,
        string description)
    {
        IReadOnlyList<string> errors = ValidateBuilding(catalog, ability);
        RequireRejected(errors, missingId, description);
    }

    private static IReadOnlyList<string> ValidateBuilding(
        ContentReferenceCatalog catalog,
        BuildingAbility ability)
    {
        SoReferenceRemediationLeafAProbe probe =
            ScriptableObject.CreateInstance<SoReferenceRemediationLeafAProbe>();
        try
        {
            probe.name = "SO Reference Remediation Leaf A Building Probe";
            probe.ConfigureBuilding(ability);
            return catalog.Validate(new ScriptableObject[] { probe });
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(probe);
        }
    }

    private static void RequireOffenseRewardAccepted(
        ContentReferenceCatalog catalog,
        OffenseRewardGrantSpec reward,
        string description)
    {
        IReadOnlyList<string> errors = ValidateOffenseReward(catalog, reward);
        RequireAccepted(errors, description);
    }

    private static void RequireOffenseRewardRejected(
        ContentReferenceCatalog catalog,
        OffenseRewardGrantSpec reward,
        string missingId,
        string description)
    {
        IReadOnlyList<string> errors = ValidateOffenseReward(catalog, reward);
        RequireRejected(errors, missingId, description);
    }

    private static IReadOnlyList<string> ValidateOffenseReward(
        ContentReferenceCatalog catalog,
        OffenseRewardGrantSpec reward)
    {
        SoReferenceRemediationLeafAProbe probe =
            ScriptableObject.CreateInstance<SoReferenceRemediationLeafAProbe>();
        try
        {
            probe.name = "SO Reference Remediation Leaf A Offense Probe";
            probe.ConfigureOffenseReward(reward);
            return catalog.Validate(new ScriptableObject[] { probe });
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(probe);
        }
    }

    private static void RequireWorkwearAccepted(
        ContentReferenceCatalog catalog,
        IEnumerable<string> speciesIds,
        string description)
    {
        IReadOnlyList<string> errors = ValidateWorkwear(catalog, speciesIds);
        RequireAccepted(errors, description);
    }

    private static void RequireWorkwearRejected(
        ContentReferenceCatalog catalog,
        IEnumerable<string> speciesIds,
        string missingId,
        string description)
    {
        IReadOnlyList<string> errors = ValidateWorkwear(catalog, speciesIds);
        RequireRejected(errors, missingId, description);
    }

    private static IReadOnlyList<string> ValidateWorkwear(
        ContentReferenceCatalog catalog,
        IEnumerable<string> speciesIds)
    {
        EnvironmentalWorkwearSO workwear =
            ScriptableObject.CreateInstance<EnvironmentalWorkwearSO>();
        try
        {
            workwear.name = "SO Reference Remediation Leaf A Workwear Probe";
            workwear.Configure(
                "test:so-reference-remediation-leaf-a",
                ValidItemId,
                "Reference Probe",
                string.Empty,
                speciesIds,
                new ThermalProtectionProfile(),
                string.Empty);
            return catalog.Validate(new ScriptableObject[] { workwear });
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(workwear);
        }
    }

    private static void RequireAccepted(
        IReadOnlyList<string> errors,
        string description)
    {
        if (errors.Count == 0)
        {
            return;
        }

        throw new InvalidOperationException(
            $"Content reference validation rejected {description}: "
            + string.Join(" | ", errors));
    }

    private static void RequireRejected(
        IReadOnlyList<string> errors,
        string missingId,
        string description)
    {
        if (errors.Any(error => error.Contains(
                missingId,
                StringComparison.Ordinal)))
        {
            return;
        }

        throw new InvalidOperationException(
            $"Content reference validation accepted {description} '{missingId}'.");
    }
}

internal sealed class SoReferenceRemediationLeafAProbe : ScriptableObject
{
    [SerializeReference] private BuildingAbility buildingAbility;
    [SerializeReference] private OffenseRewardGrantSpec offenseReward;

    public void ConfigureBuilding(BuildingAbility ability)
    {
        buildingAbility = ability;
        offenseReward = null;
    }

    public void ConfigureOffenseReward(OffenseRewardGrantSpec reward)
    {
        buildingAbility = null;
        offenseReward = reward;
    }
}
