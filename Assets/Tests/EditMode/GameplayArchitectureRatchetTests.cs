using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;

namespace DungeonStory.Tests.Architecture
{
    public sealed class GameplayArchitectureRatchetTests
    {
        private const string RuntimeArchitectureMetricsPath =
            "Architecture/runtime-architecture-metrics-current.json";
        private const string RuntimeArchitectureMetricsBaselinePath =
            "Architecture/runtime-architecture-metrics-baseline.json";
        private const int MaximumDirectRandomAccesses = 0;
        private const int MaximumEventObserverReferences = 0;

        private static readonly Regex StaticActiveAccessor = new Regex(
            @"\bstatic\s+[^\r\n;=]+\s+Active\s*(?:\{|=>)",
            RegexOptions.Compiled);

        private static readonly Regex SceneSearch = new Regex(
            @"\b(?:(?:UnityEngine\.)?Object\.)?Find(?:First|Any)?Object[s]?ByType"
            + @"|\bGameObject\.Find\b",
            RegexOptions.Compiled);

        [Test]
        public void PersistentCharacterSchedulingUsesCanonicalCharacterIds()
        {
            RuntimeCharacterIdentityPathContract
                .AssertOperationalCharacterCreationPathsUseTypedCharacterScope();

            SourceFile workTargetSelector = SourceBySuffix(
                "Character/Work/WorkTargetSelector.cs");
            SourceFile decisionCadence = SourceBySuffix(
                "Character/AI/CharacterAiDecisionCadencePolicy.cs");
            SourceFile characterStats = SourceBySuffix(
                "Character/Core/CharacterStats.cs");
            SourceFile maintenanceSchedule = SourceBySuffix(
                "Models/Characters/CharacterStatsMaintenanceSchedule.cs");
            SourceFile persistentEntityIds = SourceBySuffix(
                "Foundation/PersistentEntityIds.cs");

            SourceFile[] sources =
            {
                workTargetSelector,
                decisionCadence,
                characterStats,
                maintenanceSchedule,
            };

            Assert.That(
                sources.SelectMany(source => Regex.Matches(
                    source.Text,
                    @"\bGetInstanceID\s*\(\)").Cast<Match>()),
                Is.Empty,
                "Persistent character scheduling must not depend on Unity instance ids.");
            Assert.That(
                workTargetSelector.Text,
                Does.Contain("PersistentEntityId.GetStableHash32(characterId)"));
            Assert.That(
                decisionCadence.Text,
                Does.Contain("PersistentEntityId.GetStableUnitFraction(characterId)"));
            Assert.That(
                maintenanceSchedule.Text,
                Does.Contain("BeginNeedDecay(CharacterId characterId"));
            Assert.That(
                persistentEntityIds.Text,
                Does.Contain("public static uint GetStableHash32(CharacterId id)"));
            Assert.That(
                persistentEntityIds.Text,
                Does.Contain("uint hash = 2166136261u"));
            Assert.That(
                persistentEntityIds.Text,
                Does.Contain("hash *= 16777619u"));
        }

        [Test]
        public void ProductCodeHasNoStaticActiveRuntimeAccessor()
        {
            IReadOnlyList<SourceFile> offenders = ProductSources()
                .Where(source => StaticActiveAccessor.IsMatch(source.Text))
                .ToArray();

            Assert.That(
                offenders,
                Is.Empty,
                "Static Active accessors reintroduced:\n"
                + string.Join("\n", offenders.Select(source => source.RelativePath)));
        }

        [Test]
        public void HighRiskGlobalAccessDoesNotGrow()
        {
            IReadOnlyList<SourceFile> sources = ProductSources();

            AssertApprovedOccurrences(
                sources,
                SceneSearch,
                new Dictionary<string, int>(StringComparer.Ordinal)
                {
                    ["Services/Character/Core/CharacterSkillRuntimeEffects.cs"] = 1,
                    ["Services/Infrastructure/DungeonPlayerAutomationBridge.cs"] = 1,
                    ["Services/Infrastructure/DungeonSceneNavigation.cs"] = 1,
                    ["Services/Infrastructure/Diagnostics/DungeonGameplayPerformanceProbe.cs"] = 5,
                },
                "Scene searches are limited to explicit editor diagnostics, automation capture, and scene-transition composition paths.");
            AssertApprovedOccurrences(
                sources,
                new Regex(@"\bResources\.Load", RegexOptions.Compiled),
                new Dictionary<string, int>(StringComparer.Ordinal)
                {
                    ["Services/Infrastructure/Core/ResourcesAssetLoader.cs"] = 1,
                },
                "Resources.Load is owned by the centralized asset-loader adapter.");
            Assert.That(
                Count(sources, new Regex(@"\bResources\.FindObjectsOfTypeAll", RegexOptions.Compiled)),
                Is.Zero);
            AssertApprovedOccurrences(
                sources,
                new Regex(@"\bTime\.", RegexOptions.Compiled),
                new Dictionary<string, int>(StringComparer.Ordinal)
                {
                    ["Services/Foundation/Time/GameClock.cs"] = 11,
                    ["Services/Buildings/StructuralDamagePresentation.cs"] = 3,
                    ["Services/Character/AI/CharacterAiNaturalness.cs"] = 5,
                    ["Services/Infrastructure/Core/Diagnostics/GameplayPerformanceMeasurementSession.cs"] = 4,
                    ["Services/Infrastructure/Diagnostics/DungeonGameplayPerformanceProbe.cs"] = 8,
                    ["Views/UI/CharacterSummaryTextFormatter.cs"] = 3,
                    ["Views/UI/ResearchTreeWindow.cs"] = 3,
                },
                "Direct Unity time is limited to the clock adapter, diagnostics, presentation animation, and localization-key false positives.");
            Assert.That(
                Count(sources, new Regex(
                    @"\b(?:UnityEngine\.)?Random\.",
                    RegexOptions.Compiled)),
                Is.LessThanOrEqualTo(MaximumDirectRandomAccesses));
            Assert.That(
                Count(sources, new Regex(@"\bEventObserver\b", RegexOptions.Compiled)),
                Is.LessThanOrEqualTo(MaximumEventObserverReferences));
        }

        [Test]
        public void ProductScriptsUseMvcTopLevelFolders()
        {
            string scriptsRoot = Path.Combine(Application.dataPath, "Scripts");
            string[] allowedRoots =
            {
                "Content",
                "Controllers",
                "Editor",
                "Models",
                "Services",
                "Views",
            };

            string[] actualRoots = Directory
                .GetDirectories(scriptsRoot)
                .Select(Path.GetFileName)
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToArray();

            Assert.That(actualRoots, Is.EquivalentTo(allowedRoots));
            Assert.That(
                Directory.GetFiles(scriptsRoot, "*.cs", SearchOption.TopDirectoryOnly),
                Is.Empty,
                "Runtime scripts must live under Content, Models, Views, Controllers, or Services.");
            Assert.That(
                Directory.GetFiles(scriptsRoot, "*.asmdef", SearchOption.TopDirectoryOnly),
                Is.Empty,
                "Assemblies should belong to an MVC top-level folder.");
        }

        [Test]
        public void GameplaySceneRootHierarchyUsesAuthoringBuckets()
        {
            string scenePath = Path.Combine(Application.dataPath, "Scenes", "GameplayScene.unity");
            string sceneText = File.ReadAllText(scenePath);
            string[] requiredRoots =
            {
                "__Scene",
                "__Systems",
                "__Runtime",
                "__Debug"
            };

            foreach (string rootName in requiredRoots)
            {
                Assert.That(
                    sceneText,
                    Does.Contain($"m_Name: {rootName}"),
                    $"GameplayScene must keep the '{rootName}' hierarchy bucket.");
            }

            Match roots = Regex.Match(
                sceneText,
                @"m_Roots:\s*(?<items>(?:\r?\n\s+- \{fileID: \d+\})+)",
                RegexOptions.Singleline);

            Assert.That(roots.Success, Is.True, "GameplayScene root section was not found.");
            Assert.That(
                Regex.Matches(roots.Groups["items"].Value, @"\{fileID: \d+\}").Count,
                Is.EqualTo(requiredRoots.Length),
                "GameplayScene should expose only the four top-level hierarchy buckets.");
        }

        [Test]
        public void GameplaySceneKeepsAuthoredGroundAndTransientGridVisualsClean()
        {
            string scenePath = Path.Combine(Application.dataPath, "Scenes", "GameplayScene.unity");
            string sceneText = File.ReadAllText(scenePath);
            string groundTilemap = ReadYamlObject(sceneText, 210129473);
            string wallTilemap = ReadYamlObject(sceneText, 1827891644);

            Assert.That(
                groundTilemap,
                Does.Contain("guid: 82c0f0d68d9fce94daa05c4775992629"),
                "Ground surface cells must keep the authored summer edge tile.");
            Assert.That(
                groundTilemap,
                Does.Contain("guid: 23754a2050bae274aa906ca797f12ab7"),
                "Ground fill cells must keep the authored summer soil tile.");
            Assert.That(
                groundTilemap,
                Does.Contain("m_Data: {r: 1, g: 1, b: 1, a: 1}"),
                "Ground tiles must not retain placement/debug tint overrides.");
            Assert.That(
                wallTilemap,
                Does.Contain("m_Tiles: {}"),
                "Runtime wall visuals must not be serialized into GameplayScene.");
            Assert.That(
                sceneText,
                Does.Contain("gridOverlayTile: {fileID: 11400000, guid: 86447a79140ba3e42833190c4cbe8279, type: 2}"),
                "The placement grid must reference its authored outline tile explicitly.");
        }

        [Test]
        public void ProductRuntimeSourcesRespectV18LineLimitsAndBaseline()
        {
            string metricsPath = Path.Combine(
                Application.dataPath,
                RuntimeArchitectureMetricsPath);
            string baselinePath = Path.Combine(
                Application.dataPath,
                RuntimeArchitectureMetricsBaselinePath);
            ArchitectureMetricsDocument metrics = JsonUtility.FromJson<ArchitectureMetricsDocument>(
                File.ReadAllText(metricsPath));
            ArchitectureMetricsBaseline baseline = JsonUtility.FromJson<ArchitectureMetricsBaseline>(
                File.ReadAllText(baselinePath));

            Assert.That(metrics, Is.Not.Null);
            Assert.That(baseline, Is.Not.Null);
            Assert.That(metrics.schemaVersion, Is.EqualTo(2));
            Assert.That(baseline.schemaVersion, Is.EqualTo(metrics.schemaVersion));
            Assert.That(
                metrics.oversizedTypeCount,
                Is.EqualTo(metrics.oversizedTypes?.Count ?? 0),
                "The generated architecture metric count must match its per-type evidence.");
            Assert.That(
                metrics.oversizedTypeCount,
                Is.LessThanOrEqualTo(baseline.maxOversizedType),
                "Runtime type-size limits are measured per declaration by ArchitectureMetricsAnalyzer; whole-file line counts are not architectural violations.");
        }

        [Test]
        public void ProductAssemblyGraphIsCompleteAcyclicAndLayered()
        {
            IReadOnlyDictionary<string, int> expectedRanks =
                new Dictionary<string, int>(StringComparer.Ordinal)
                {
                    ["DungeonStory.Foundation"] = 0,
                    ["DungeonStory.Defense"] = 0,
                    ["DungeonStory.Recruitment"] = 0,
                    ["DungeonStory.Synthesis"] = 0,
                    ["DungeonStory.World"] = 0,
                    ["DungeonStory.Automation"] = 1,
                    ["DungeonStory.Buildings"] = 1,
                    ["DungeonStory.CoreSession"] = 1,
                    ["DungeonStory.FacilityEvolution"] = 1,
                    ["DungeonStory.Factions"] = 1,
                    ["DungeonStory.Grid"] = 1,
                    ["DungeonStory.AI"] = 2,
                    ["DungeonStory.Captivity"] = 2,
                    ["DungeonStory.Evolution"] = 2,
                    ["DungeonStory.Items"] = 2,
                    ["DungeonStory.Operation"] = 2,
                    ["DungeonStory.ServiceRooms"] = 2,
                    ["DungeonStory.SessionRuntime"] = 2,
                    ["DungeonStory.Wildlife"] = 2,
                    ["DungeonStory.Codex"] = 3,
                    ["DungeonStory.Work"] = 3,
                    ["DungeonStory.Characters"] = 4,
                    ["DungeonStory.Production"] = 4,
                    ["DungeonStory.Rooms"] = 4,
                    ["DungeonStory.CharacterNeeds"] = 5,
                    ["DungeonStory.Combat"] = 5,
                    ["DungeonStory.Environment"] = 5,
                    ["DungeonStory.Invasion"] = 5,
                    ["DungeonStory.Exterior"] = 6,
                    ["DungeonStory.Medical"] = 6,
                    ["DungeonStory.Run"] = 6,
                    ["DungeonStory.Species"] = 6,
                    ["DungeonStory.Survival"] = 7,
                    ["DungeonStory.Economy"] = 8,
                    ["DungeonStory.Meta"] = 8,
                    ["DungeonStory.Content"] = 9,
                    ["DungeonStory.FacilityShop"] = 9,
                    ["DungeonStory.Offense"] = 9,
                    ["DungeonStory.Infrastructure"] = 10,
                    ["DungeonStory.Research"] = 10,
                    ["DungeonStory.Presentation"] = 11
                };
            string scriptsRoot = Path.GetFullPath(
                Path.Combine(Application.dataPath, "Scripts"));
            AsmdefSource[] assemblies = Directory
                .EnumerateFiles(scriptsRoot, "*.asmdef", SearchOption.AllDirectories)
                .Select(path => new AsmdefSource(path))
                .Where(source => source.Definition.name.StartsWith(
                    "DungeonStory.",
                    StringComparison.Ordinal)
                    && !source.Definition.name.EndsWith(
                        ".Editor",
                        StringComparison.Ordinal))
                .ToArray();

            IGrouping<string, AsmdefSource> duplicate = assemblies
                .GroupBy(source => source.Definition.name, StringComparer.Ordinal)
                .FirstOrDefault(group => group.Count() > 1);
            Assert.That(
                duplicate,
                Is.Null,
                duplicate == null
                    ? string.Empty
                    : $"Duplicate assembly name '{duplicate.Key}'.");

            IReadOnlyDictionary<string, AsmdefSource> byName = assemblies
                .ToDictionary(source => source.Definition.name, StringComparer.Ordinal);
            Assert.That(
                byName.Keys.OrderBy(name => name, StringComparer.Ordinal),
                Is.EquivalentTo(expectedRanks.Keys),
                "The V18 product assembly set changed without updating its dependency policy.");

            foreach (AsmdefSource assembly in assemblies)
            {
                Assert.That(
                    assembly.Definition.rootNamespace,
                    Is.EqualTo(assembly.Definition.name),
                    $"{assembly.Definition.name} must own its root namespace.");

                foreach (string reference in assembly.Definition.references ?? Array.Empty<string>())
                {
                    string referencedName = NormalizeAssemblyReference(reference);
                    if (!expectedRanks.TryGetValue(referencedName, out int referencedRank))
                    {
                        continue;
                    }

                    int ownerRank = expectedRanks[assembly.Definition.name];
                    Assert.That(
                        referencedRank,
                        Is.LessThanOrEqualTo(ownerRank),
                        $"{assembly.Definition.name} cannot depend on later layer {referencedName}.");
                }
            }

            AssertAssemblyGraphAcyclic(byName);
        }

        [Test]
        public void CharacterNeedAssemblyOwnsStableDefinitionsWithoutRuntimeActors()
        {
            SourceFile definitions = SourceBySuffix(
                "CharacterNeeds/Core/CharacterNeedCatalog.cs");
            SourceFile runtimeExtensions = SourceBySuffix(
                "Character/Core/CharacterNeedDefinitionRuntimeExtensions.cs");

            Assert.That(
                File.Exists(Path.Combine(
                    Application.dataPath,
                    "Scripts/Models/CharacterNeeds/Core/DungeonStory.CharacterNeeds.asmdef")),
                Is.True);
            Assert.That(definitions.Text, Does.Contain("enum CharacterNeedTag"));
            Assert.That(definitions.Text, Does.Contain("class CharacterNeedDefinition"));
            Assert.That(
                definitions.Text,
                Does.Contain("sourceAssembly: \"Assembly-CSharp\""));
            Assert.That(definitions.Text, Does.Not.Contain("CharacterActor"));
            Assert.That(definitions.Text, Does.Not.Contain("CharacterStats"));
            Assert.That(definitions.Text, Does.Not.Contain("CharacterMoodFactorSnapshot"));
            Assert.That(
                runtimeExtensions.Text,
                Does.Contain("class CharacterNeedDefinitionRuntimeExtensions"));
        }

        [Test]
        public void WorkExecutorUsesRegisteredOpenDispatch()
        {
            SourceFile executor = ProductSources().Single(source =>
                source.RelativePath.EndsWith(
                    "Character/Work/WorkTaskExecutor.cs",
                    StringComparison.Ordinal));

            Assert.That(executor.Text, Does.Contain("IWorkExecutionHandlerRegistry"));
            Assert.That(executor.Text, Does.Not.Match(@"switch\s*\(\s*workType\s*\)"));
            Assert.That(
                executor.Text,
                Does.Not.Match(@"workType\s*==\s*FacilityWorkType\.(?:Repair|Research|Craft|Butcher)"));
        }

        [Test]
        public void CharacterSkillWorkEventsExposeStableWorkTypeIdsOnly()
        {
            SourceFile skillRuntime = SourceBySuffix(
                "Character/Core/CharacterSkillRuntimeEffects.cs");

            Assert.That(skillRuntime.Text, Does.Contain("public WorkTypeId WorkTypeId { get; }"));
            Assert.That(skillRuntime.Text, Does.Not.Contain("public FacilityWorkType WorkType"));
            Assert.That(
                skillRuntime.Text,
                Does.Not.Match(@"public static void BeginWork\s*\([^)]*FacilityWorkType"));
            Assert.That(
                skillRuntime.Text,
                Does.Not.Match(@"public static void TriggerWorkCompleted\s*\([^)]*FacilityWorkType"));
            Assert.That(
                skillRuntime.Text,
                Does.Not.Match(@"public CharacterSkillExecutionContext\s*\([^)]*FacilityWorkType"));
        }

        [Test]
        public void BuildingWorkAbilitiesUseRegisteredHandlersOnly()
        {
            IReadOnlyList<SourceFile> sources = ProductSources();
            SourceFile abilityData = SourceBySuffix(
                "Buildings/Abilities/BuildingAbility.cs");
            SourceFile dispatcher = SourceBySuffix(
                "Buildings/Abilities/BuildingAbilityHandlers.cs");
            SourceFile registration = SourceBySuffix(
                "Infrastructure/Registration/DungeonWorkRegistration.cs");

            Assert.That(
                sources.Any(source =>
                    source.Text.Contains("IBuildingWorkCompletedRuntimeAbility")),
                Is.False,
                "The legacy executable ability interface must not return.");
            Assert.That(
                abilityData.Text,
                Does.Not.Contain("ApplyWorkCompleted"),
                "Serialized building ability data must not execute runtime work.");
            Assert.That(dispatcher.Text, Does.Contain("IBuildingWorkCompletionAbility"));
            Assert.That(dispatcher.Text, Does.Contain("No work-completion handler is registered"));

            string[] requiredHandlers =
            {
                "ProductionBuildingAbilityHandler",
                "CleaningBuildingAbilityHandler",
                "SecurityBuildingAbilityHandler",
                "ReceptionBuildingAbilityHandler",
                "PatrolPostBuildingAbilityHandler",
                "OutdoorRestBuildingAbilityHandler",
                "ExteriorMaintenanceBuildingAbilityHandler"
            };
            foreach (string handler in requiredHandlers)
            {
                Assert.That(
                    registration.Text,
                    Does.Contain($"Register<{handler}>"),
                    $"{handler} is not registered in the composition root.");
            }
        }

        [Test]
        public void SaveRootDefaultsToCurrentV24()
        {
            SourceFile saveService = SourceBySuffix(
                "Infrastructure/Core/InfrastructureSavePrimitives.cs");

            Assert.That(
                saveService.Text,
                Does.Match(@"CurrentVersion\s*=\s*24\s*;"));
            Assert.That(saveService.Text, Does.Contain("DungeonSaveSectionEnvelope"));
        }

        [Test]
        public void SaveRootContainsOnlyMetadataAndSectionEnvelopes()
        {
            SourceFile saveService = SourceBySuffix(
                "Infrastructure/Core/InfrastructureSavePrimitives.cs");
            Match root = Regex.Match(
                saveService.Text,
                @"public sealed class DungeonGameSaveData\s*\{(?<body>[^}]*)\}",
                RegexOptions.Singleline);

            Assert.That(root.Success, Is.True, "DungeonGameSaveData root was not found.");
            string body = root.Groups["body"].Value;
            Assert.That(body, Does.Contain("CurrentVersion = 24"));
            Assert.That(body, Does.Contain("savedAtUtc"));
            Assert.That(body, Does.Contain("sceneName"));
            Assert.That(body, Does.Contain("sections"));
            Assert.That(body, Does.Not.Contain("physicalItems"));
            Assert.That(body, Does.Not.Contain("characters"));
            Assert.That(body, Does.Not.Contain("wildlife"));
            Assert.That(body, Does.Not.Contain("survival"));
            Assert.That(body, Does.Not.Contain("offense"));
        }

        [Test]
        public void SaveContractsAreOwnedByFoundation()
        {
            SourceFile contracts = SourceBySuffix(
                "Foundation/Save/DungeonSaveSections.cs");
            SourceFile restoreReport = SourceBySuffix(
                "Foundation/Save/DungeonGameRestoreReport.cs");
            SourceFile saveService = SourceBySuffix(
                "Infrastructure/DungeonGameSaveService.cs");

            Assert.That(contracts.Text, Does.Contain("interface IDungeonSaveSection"));
            Assert.That(contracts.Text, Does.Contain("class DungeonSaveSectionRegistry"));
            Assert.That(restoreReport.Text, Does.Contain("class DungeonGameRestoreReport"));
            Assert.That(
                saveService.Text,
                Does.Not.Contain("class DungeonGameRestoreReport"));
        }

        [Test]
        public void OperatingDaySettlementUsesScopedWorldQueries()
        {
            SourceFile settlement = SourceBySuffix(
                "Operation/OperatingDaySettlementApplicationAdapter.cs");
            SourceFile fixture = SourceBySuffixIncludingEditor(
                "Operation/Editor/OperatingDaySettlementDebugScenarios.cs");

            Assert.That(settlement.Text, Does.Contain("IBuildingWorldQuery"));
            Assert.That(settlement.Text, Does.Contain("ICharacterWorldQuery"));
            Assert.That(
                settlement.Text,
                Does.Not.Contain("IDungeonSceneComponentQuery"));
            Assert.That(fixture.Text, Does.Contain("FixedWorldQuery"));
            Assert.That(
                fixture.Text,
                Does.Not.Contain("FixedSceneComponentQuery"));
        }

        [Test]
        public void CharacterAiSchedulerUsesScopedGameClock()
        {
            SourceFile scheduler = SourceBySuffix(
                "Character/AI/CharacterAiScheduler.cs");

            Assert.That(scheduler.Text, Does.Contain("IGameClock gameClock"));
            Assert.That(scheduler.Text, Does.Contain("gameClock.FrameCount"));
            Assert.That(scheduler.Text, Does.Not.Match(@"\bTime\."));
        }

        [Test]
        public void LocalLlmQueueUsesScopedUiClock()
        {
            SourceFile queue = SourceBySuffix(
                "Models/AI/Core/LocalLlmRequestQueue.cs");

            Assert.That(queue.Text, Does.Contain("IUiClock uiClock"));
            Assert.That(queue.Text, Does.Contain("Construct(IUiClock uiClock)"));
            Assert.That(queue.Text, Does.Contain("request.Attach(webRequest, Now)"));
            Assert.That(queue.Text, Does.Not.Match(@"\bTime\."));
        }

        [Test]
        public void CoreRuntimeProvidersUseCapturedSceneReferences()
        {
            SourceFile providers = SourceBySuffix(
                "Infrastructure/DungeonGridBuildingRuntimeProviders.cs");
            SourceFile gameServices = SourceBySuffix(
                "Infrastructure/GameRuntimeServices.cs");
            SourceFile scope = SourceBySuffix(
                "Infrastructure/DungeonRuntimeLifetimeScope.cs");

            Assert.That(
                providers.Text,
                Does.Not.Contain("IDungeonSceneComponentQuery"));
            Assert.That(
                gameServices.Text,
                Does.Not.Contain("IDungeonSceneComponentQuery"));
            Assert.That(
                scope.Text,
                Does.Contain("CaptureSceneRuntimeReferences(sceneQuery)"));
            Assert.That(
                scope.Text,
                Does.Contain("RegisterDungeonCoreInfrastructure("));
            Assert.That(scope.Text, Does.Contain("userSettingsTargets"));
        }

        [Test]
        public void DomainRuntimeProvidersUseCompositionTimeReferences()
        {
            SourceFile offense = SourceBySuffix(
                "Offense/OffenseRuntimeServices.cs");
            SourceFile offenseFactory = SourceBySuffix(
                "Offense/OffensePanelFactory.cs");
            SourceFile invasion = SourceBySuffix(
                "Infrastructure/Core/InvasionThreatRuntimeProvider.cs");
            SourceFile featureRuntimes = SourceBySuffix(
                "Infrastructure/Core/RuntimePanelProviders.cs");
            SourceFile references = SourceBySuffix(
                "Infrastructure/SceneDomainRuntimeReferences.cs");

            Assert.That(offense.Text, Does.Contain("OffenseSceneRuntimeReferences"));
            Assert.That(offense.Text, Does.Not.Contain("IDungeonSceneComponentQuery"));
            Assert.That(offense.Text, Does.Not.Contain("CachedSceneRuntimeProvider"));
            Assert.That(
                offenseFactory.Text,
                Does.Contain("RegisterWorldMapPanel(panel)"));
            Assert.That(
                offenseFactory.Text,
                Does.Contain("RegisterExpeditionPanel(panel)"));
            Assert.That(invasion.Text, Does.Contain("InvasionSceneRuntimeReferences"));
            Assert.That(invasion.Text, Does.Not.Contain("IDungeonSceneComponentQuery"));
            Assert.That(
                featureRuntimes.Text,
                Does.Contain("FacilityFeatureSceneRuntimeReferences"));
            Assert.That(
                featureRuntimes.Text,
                Does.Not.Contain("CachedSceneRuntimeProvider"));
            Assert.That(
                references.Text,
                Does.Contain("sealed class OffenseSceneRuntimeReferences"));
            Assert.That(
                references.Text,
                Does.Contain("sealed class InvasionSceneRuntimeReferences"));
            Assert.That(
                ProductSources().Where(source =>
                    source.Text.Contains("CachedSceneRuntimeProvider")),
                Is.Empty);
        }

        [Test]
        public void WorldSimulationAndDebugCommandsUseScopedReferences()
        {
            SourceFile exterior = SourceBySuffix(
                "Exterior/ExteriorActivityRuntime.cs");
            SourceFile habitats = SourceBySuffix(
                "Infrastructure/WildlifeHabitatMarkerRegistry.cs");
            SourceFile navigation = SourceBySuffix(
                "Infrastructure/DungeonSceneNavigation.cs");
            SourceFile debugCommands = SourceBySuffix(
                "Debugging/DungeonDebugCommandProviders.cs");
            SourceFile validation = SourceBySuffix(
                "Infrastructure/SceneBuildableLeakValidator.cs");
            SourceFile automation = SourceBySuffix(
                "Infrastructure/DungeonPlayerAutomationBridge.cs");
            SourceFile scope = SourceBySuffix(
                "Infrastructure/DungeonRuntimeLifetimeScope.cs");

            Assert.That(
                exterior.Text,
                Does.Contain("WorldSimulationSceneReferences"));
            Assert.That(
                habitats.Text,
                Does.Contain("WorldSimulationSceneReferences"));
            Assert.That(
                navigation.Text,
                Does.Contain("DungeonSceneRuntimeReferences"));
            Assert.That(
                debugCommands.Text,
                Does.Contain("ICharacterWorldQuery"));
            Assert.That(
                validation.Text,
                Does.Contain("SceneValidationReferences"));
            Assert.That(
                automation.Text,
                Does.Contain("IDungeonUiCanvasProvider"));
            Assert.That(
                automation.Text,
                Does.Contain("DungeonUserSettingsRuntimeTargets"));
            Assert.That(automation.Text, Does.Contain("IGameClock gameClock"));
            Assert.That(automation.Text, Does.Contain("IUiClock uiClock"));
            Assert.That(
                automation.Text,
                Does.Contain("IGameTimeScaleController timeScaleController"));
            Assert.That(automation.Text, Does.Not.Match(@"\bTime\."));
            Assert.That(
                scope.Text,
                Does.Contain("CaptureWorldSimulationReferences(sceneQuery)"));
            Assert.That(
                exterior.Text
                + habitats.Text
                + navigation.Text
                + debugCommands.Text
                + validation.Text
                + automation.Text,
                Does.Not.Contain("IDungeonSceneComponentQuery"));
            Assert.That(
                ProductSources().Where(source =>
                    source.Text.Contains("IDungeonSceneComponentQuery")),
                Is.Empty);
        }

        [Test]
        public void SocialRumorModelsReceiveTimeFromTheirOwner()
        {
            SourceFile memory = SourceBySuffix(
                "Character/AI/CharacterSocialMemory.cs");

            Assert.That(memory.Text, Does.Contain("IsExpiredAt(float now)"));
            Assert.That(memory.Text, Does.Contain("Capture(SocialRumor rumor, float now)"));
            Assert.That(memory.Text, Does.Contain("Restore(float now)"));
            Assert.That(memory.Text, Does.Contain("Construct(IGameClock gameClock)"));
            Assert.That(memory.Text, Does.Not.Match(@"\bTime\."));
        }

        [Test]
        public void BuildableReservationsUseScopedGameTime()
        {
            SourceFile buildable = SourceBySuffix(
                "Buildings/BuildableObject.cs");
            SourceFile occupancy = SourceBySuffix(
                "Buildings/BuildingOccupancyAssignment.cs");
            SourceFile filth = SourceBySuffix(
                "Survival/WorldFilthRuntime.cs");

            Assert.That(buildable.Text, Does.Contain("IGameClock gameClock"));
            Assert.That(
                occupancy.Text,
                Does.Contain("float expiry = Now + Mathf.Max(0.1f, seconds)"));
            Assert.That(occupancy.Text, Does.Contain("visitReservations[visitor] = expiry"));
            Assert.That(buildable.Text, Does.Not.Match(@"\bTime\."));
            Assert.That(buildable.Text, Does.Contain("this.gameClock = gameClock"));
            Assert.That(filth.Text, Does.Contain("gameClock = runtime.GameClock"));
            Assert.That(
                filth.Text,
                Does.Match(
                    @"target\.ConstructBuildableObject\s*\([\s\S]*?\bgameClock\b[\s\S]*?\);"));
        }

        [Test]
        public void SaveUiUsesClockAndGameSpeedPorts()
        {
            SourceFile saveUi = SourceBySuffix("UI/DungeonSaveUi.cs");
            SourceFile sessionContracts = SourceBySuffix(
                "Models/CoreSession/CoreSessionContracts.cs");

            Assert.That(saveUi.Text, Does.Contain("IUiClock uiClock"));
            Assert.That(
                saveUi.Text,
                Does.Contain("IGameSpeedController gameSpeedController"));
            Assert.That(
                saveUi.Text,
                Does.Not.Contain("IDungeonSceneComponentQuery"));
            Assert.That(saveUi.Text, Does.Not.Match(@"\bTime\."));
            Assert.That(
                sessionContracts.Text,
                Does.Contain("interface IGameSpeedController"));
        }

        [Test]
        public void SaveDtosAreOwnedByTheirDomains()
        {
            SourceFile saveService = SourceBySuffix(
                "Infrastructure/DungeonGameSaveService.cs");

            Assert.That(
                saveService.Text,
                Does.Not.Contain("class DungeonCharacterWorldSaveData"));
            Assert.That(
                saveService.Text,
                Does.Not.Contain("class DungeonResearchSaveData"));
            Assert.That(
                saveService.Text,
                Does.Not.Contain("class DungeonFacilityShopSaveData"));
            Assert.That(
                saveService.Text,
                Does.Not.Contain("class DungeonMetaProgressionSaveData"));
            Assert.That(
                saveService.Text,
                Does.Not.Contain("class DungeonRegularCustomerSaveData"));
            Assert.That(
                saveService.Text,
                Does.Not.Contain("class DungeonStaffDiscontentSaveData"));
            Assert.That(
                saveService.Text,
                Does.Not.Contain("class DungeonCodexSaveData"));
            Assert.That(
                SourceBySuffix("Character/Core/DungeonCharacterSaveData.cs").Text,
                Does.Contain("class DungeonCharacterWorldSaveData"));
            Assert.That(
                SourceBySuffix("Models/CoreSession/RunVariableContracts.cs").Text,
                Does.Contain("class DungeonRunFlowSaveData"));
            Assert.That(
                SourceBySuffix("Meta/Core/DungeonMetaProgressionSaveData.cs").Text,
                Does.Contain("class DungeonMetaProgressionSaveData"));
        }

        [Test]
        public void CompositionRootDelegatesDomainRegistration()
        {
            SourceFile scope = SourceBySuffix(
                "Infrastructure/DungeonRuntimeLifetimeScope.cs");

            Assert.That(scope.Text, Does.Contain("RegisterDungeonFoundation()"));
            Assert.That(scope.Text, Does.Contain("RegisterDungeonWork()"));
            Assert.That(
                scope.Text,
                Does.Contain("RegisterDungeonCombatAndInvasion("));
            Assert.That(
                scope.Text,
                Does.Contain("playerCombatCommands"));
            Assert.That(
                scope.Text,
                Does.Contain("RegisterDungeonWorldSimulation("));
            Assert.That(
                scope.Text,
                Does.Contain("worldSimulationReferences"));
            Assert.That(
                scope.Text,
                Does.Contain("RegisterDungeonSaveInfrastructure()"));
            Assert.That(
                scope.Text,
                Does.Contain("RegisterDungeonCoreInfrastructure("));
            Assert.That(scope.Text, Does.Contain("userSettingsTargets"));
            Assert.That(
                scope.Text,
                Does.Contain("RegisterDungeonFacilitySystems(facilityRuntimeReferences)"));
            Assert.That(
                scope.Text,
                Does.Contain("RegisterDungeonCharacterSystems(characterRuntimeReferences)"));
            Assert.That(
                scope.Text,
                Does.Contain("RegisterDungeonPresentation("));
            Assert.That(
                scope.Text,
                Does.Contain("RegisterDungeonAiAndRooms("));
            Assert.That(
                scope.Text,
                Does.Contain("progressionRuntimeReferences)"));
            Assert.That(
                Regex.Matches(scope.Text, @"\bbuilder\.Register").Count,
                Is.LessThanOrEqualTo(13));
            Assert.That(scope.Text, Does.Not.Contain("PhysicalItemsSaveSection"));
            Assert.That(scope.Text, Does.Not.Contain("DefenseCombatExecutor"));
            Assert.That(scope.Text, Does.Not.Contain("WarehouseFeatureSurfacePresenter"));
        }

        [Test]
        public void GameContentCatalogRegistrationExposesOneExplicitContractSet()
        {
            SourceFile registration = SourceBySuffix(
                "Infrastructure/Registration/DungeonCoreInfrastructureRegistration.cs");
            SourceFile rootCatalog = SourceBySuffix(
                "Content/GameContentCatalogSO.cs");
            SourceFile contentBuilder = SourceBySuffixIncludingEditor(
                "Items/Editor/GameContentCatalogAssetBuilder.cs");
            SourceFile researchBuilder = SourceBySuffixIncludingEditor(
                "Research/Editor/ResearchProjectAssetBuilder.cs");
            SourceFile localizationBuilder = SourceBySuffixIncludingEditor(
                "Items/Editor/DomainFailureLocalizationAssetBuilder.cs");
            SourceFile[] directRegistrations = ProductSources()
                .Where(source => source.Text.Contains(
                    "Register<ResourceGameContentCatalog>"))
                .ToArray();
            Match registrationBlock = Regex.Match(
                registration.Text,
                @"builder\.Register<ResourceGameContentCatalog>\(Lifetime\.Singleton\)"
                + @"(?<contracts>[\s\S]*?);",
                RegexOptions.CultureInvariant);

            Assert.That(registrationBlock.Success, Is.True);
            Assert.That(
                registrationBlock.Groups["contracts"].Value,
                Does.Contain(".As<IGameContentCatalog>()"));
            Assert.That(
                registrationBlock.Groups["contracts"].Value,
                Does.Contain(".As<IGameContentDefinitionSource>()"));
            Assert.That(
                registrationBlock.Groups["contracts"].Value,
                Does.Contain(".As<ICoreSessionRulesProvider>()"));
            Assert.That(
                registrationBlock.Groups["contracts"].Value,
                Does.Not.Contain("AsImplementedInterfaces"));
            Assert.That(directRegistrations, Has.Length.EqualTo(1));
            Assert.That(
                directRegistrations[0].RelativePath,
                Does.EndWith(
                    "Infrastructure/Registration/DungeonCoreInfrastructureRegistration.cs"));

            string[] compositionCallers =
            {
                "Infrastructure/Registration/DungeonWorldSimulationRegistration.cs",
                "Infrastructure/DungeonTitleLifetimeScope.cs",
                "Infrastructure/DungeonPreparationLifetimeScope.cs"
            };
            foreach (string callerPath in compositionCallers)
            {
                SourceFile caller = SourceBySuffix(callerPath);
                Assert.That(
                    caller.Text,
                    Does.Contain("RegisterDungeonGameContentCatalog()"),
                    $"Composition root '{callerPath}' must use the shared content registration.");
                Assert.That(
                    caller.Text,
                    Does.Not.Contain("Register<ResourceGameContentCatalog>"),
                    $"Composition root '{callerPath}' must not duplicate the catalog contract set.");
            }

            Assert.That(contentBuilder.Text, Does.Contain("EditorUtility.DisplayDialog("));
            Assert.That(contentBuilder.Text, Does.Contain("IsExplicitBatchModeInvocation()"));
            Assert.That(contentBuilder.Text, Does.Contain("ReindexResearchProjects()"));
            Assert.That(contentBuilder.Text, Does.Contain("IsLegacyDungeonFactionShadow("));
            Assert.That(
                researchBuilder.Text,
                Does.Contain("GameContentCatalogAssetBuilder.ReindexResearchProjects()"));
            Assert.That(contentBuilder.Text, Does.Contain("WriteGenerationManifest("));
            Assert.That(contentBuilder.Text, Does.Contain("AssetDatabase.SaveAssetIfDirty(asset)"));
            Assert.That(
                contentBuilder.Text,
                Does.Contain("RecordTouchedOutput)"));
            Assert.That(
                contentBuilder.Text,
                Does.Contain("activeTouchedOutputPaths"));
            Assert.That(contentBuilder.Text, Does.Contain("ValidateGeneratedCatalogsBeforeSave("));
            Assert.That(contentBuilder.Text, Does.Contain("RequireNoDirtyOwnedAssets()"));
            Assert.That(
                contentBuilder.Text,
                Does.Contain("GetPotentialOutputPathsForPreflight()"));
            Assert.That(
                contentBuilder.Text,
                Does.Not.Contain("path.StartsWith(\n                \"Assets/AddressableAssetsData/\""));
            Assert.That(contentBuilder.Text, Does.Not.Contain("AssetDatabase.SaveAssets()"));
            Assert.That(
                localizationBuilder.Text,
                Does.Contain("internal static IReadOnlyList<string> RebuildWithoutSaving("));
            Assert.That(
                localizationBuilder.Text,
                Does.Contain("Action<string> recordTouchedOutput = null"));
            Assert.That(
                localizationBuilder.Text,
                Does.Contain("RecordChangedOutput(changedOutputPaths, recordTouchedOutput, collection)"));
            Assert.That(
                localizationBuilder.Text,
                Does.Not.Contain("AddOutputPath(outputPaths, koreanLocale)"));
            Assert.That(
                localizationBuilder.Text,
                Does.Contain("AssetDatabase.SaveAssetIfDirty(asset)"));
            Assert.That(
                localizationBuilder.Text,
                Does.Not.Contain("AssetDatabase.SaveAssets()"));
            Assert.That(
                contentBuilder.Text,
                Does.Contain("Artifacts/QA/game-content-catalog-generation-manifest.json"));
            Assert.That(
                contentBuilder.Text,
                Does.Contain("GetCanonicalProvenanceInput()"));
            Assert.That(
                contentBuilder.Text,
                Does.Contain("localizationContractHashSha256"));
            Assert.That(contentBuilder.Text, Does.Contain("ComputeMetaFileHash("));
            Assert.That(contentBuilder.Text, Does.Contain("AssetPathToGUID(path)"));
            Assert.That(contentBuilder.Text, Does.Contain("File.Replace("));
            Assert.That(contentBuilder.Text, Does.Contain("GenerationManifestPath + \".tmp\""));
            Assert.That(
                contentBuilder.Text,
                Does.Contain("GeneratorDependencySourcePaths"));
            Assert.That(
                contentBuilder.Text,
                Does.Contain("Services/Combat/EquipmentEvolutionContracts.cs"));
            Assert.That(
                contentBuilder.Text,
                Does.Contain("Services/Evolution/EvolutionCatalystEconomyRuntime.cs"));
            Assert.That(
                contentBuilder.Text,
                Does.Contain("Services/FacilityShop/FacilityShopSystem.cs"));
            Assert.That(
                contentBuilder.Text,
                Does.Contain("Generated-content provenance dependency is missing"));
            Assert.That(
                rootCatalog.Text,
                Does.Contain("domainCatalogs == null"));
            Assert.That(
                rootCatalog.Text,
                Does.Contain("Array.Empty<ScriptableObject>()"));
            Assert.That(
                rootCatalog.Text,
                Does.Contain("ItemDefinitionsTypeName = \"ItemDefinitionCatalogSO\""));
            Assert.That(
                rootCatalog.Text,
                Does.Contain(
                    "WorldInteractionPresentationCatalogSO"));
            Assert.That(
                rootCatalog.Text,
                Does.Contain("CharacterSkillSystemSettingsSO"));

            Type rootCatalogType = AppDomain.CurrentDomain.GetAssemblies()
                .Select(assembly => assembly.GetType(
                    "GameContentCatalogSO",
                    throwOnError: false))
                .FirstOrDefault(type => type != null);
            Assert.That(rootCatalogType, Is.Not.Null);
            System.Reflection.MethodInfo validateCatalog = rootCatalogType.GetMethod(
                "ValidateCatalog",
                System.Reflection.BindingFlags.Instance
                | System.Reflection.BindingFlags.Public);
            System.Reflection.MethodInfo configureCatalog = rootCatalogType.GetMethod(
                "Configure",
                System.Reflection.BindingFlags.Instance
                | System.Reflection.BindingFlags.Public);
            Assert.That(validateCatalog, Is.Not.Null);
            Assert.That(configureCatalog, Is.Not.Null);

            ScriptableObject emptyRoot = ScriptableObject.CreateInstance(rootCatalogType);
            ScriptableObject invalidDomain =
                ScriptableObject.CreateInstance(rootCatalogType);
            ScriptableObject wrongRootReference =
                ScriptableObject.CreateInstance(rootCatalogType);
            try
            {
                IReadOnlyList<string> emptyErrors = null;
                Assert.DoesNotThrow(() => emptyErrors =
                    (IReadOnlyList<string>)validateCatalog.Invoke(emptyRoot, null));
                Assert.That(emptyErrors, Is.Not.Null);
                Assert.That(emptyErrors, Has.Count.GreaterThanOrEqualTo(5));

                configureCatalog.Invoke(
                    emptyRoot,
                    new object[]
                    {
                        wrongRootReference,
                        wrongRootReference,
                        wrongRootReference,
                        wrongRootReference,
                        new ScriptableObject[] { invalidDomain }
                    });
                IReadOnlyList<string> typeErrors =
                    (IReadOnlyList<string>)validateCatalog.Invoke(emptyRoot, null);
                Assert.That(
                    typeErrors.Count(error => error.Contains("expected")),
                    Is.GreaterThanOrEqualTo(5),
                    "Wrong root and domain SO types must fail catalog validation.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(emptyRoot);
                UnityEngine.Object.DestroyImmediate(invalidDomain);
                UnityEngine.Object.DestroyImmediate(wrongRootReference);
            }

            string[] forbiddenBuilderCallers =
            {
                "Editor/DungeonStoryFinalAcceptanceRunner.cs",
                "Editor/DungeonFinalPlayModeAcceptanceRequestFacade.cs",
                "Items/Editor/RuntimeAuthorityV18Validator.cs",
                "Items/Editor/ItemArchitectureV6Validator.cs"
            };
            foreach (string callerPath in forbiddenBuilderCallers)
            {
                Assert.That(
                    SourceBySuffixIncludingEditor(callerPath).Text,
                    Does.Not.Contain("GameContentCatalogAssetBuilder"),
                    $"Acceptance or validation path '{callerPath}' must remain read-only.");
            }
        }

        [Test]
        public void FoundationRegistrationSuppliesTheAggregateBackedRandomProviderExplicitly()
        {
            SourceFile registration = SourceBySuffix(
                "Infrastructure/Registration/DungeonFoundationRegistration.cs");

            Assert.That(
                registration.Text,
                Does.Not.Contain("Register<RandomStreamProvider>"));
            Assert.That(
                registration.Text,
                Does.Contain("new RandomStreamProvider("));
            Assert.That(
                registration.Text,
                Does.Contain("resolver.Resolve<DungeonRuntimeAggregateRootStore>()"));
        }

        [Test]
        public void RandomStreamsHaveAnIndependentV18SaveSection()
        {
            SourceFile provider = SourceBySuffix(
                "Foundation/Random/RandomStreamProvider.cs");
            SourceFile runVariables = SourceBySuffix(
                "Run/RunVariableSystem.cs");
            SourceFile saveSection = SourceBySuffix(
                "Services/Infrastructure/Core/Save/RandomStreamSaveSection.cs");
            SourceFile sectionIds = SourceBySuffix(
                "Foundation/Save/DungeonSaveSectionIds.cs");
            SourceFile runVariableSaveSection = SourceBySuffix(
                "Run/RunVariableSaveSection.cs");
            SourceFile registration = SourceBySuffix(
                "Infrastructure/Registration/DungeonSaveRegistration.cs");

            Assert.That(provider.Text, Does.Contain("CaptureStates()"));
            Assert.That(provider.Text, Does.Contain("RestoreStates("));
            Assert.That(
                provider.Text,
                Does.Contain("state.StreamStates[streamId] = CombineSeed("));
            Assert.That(
                runVariables.Text,
                Does.Contain("IRandomStreamProvider randomStreamProvider"));
            Assert.That(
                runVariables.Text,
                Does.Contain("provider.Get(\"run-variables\")"));
            Assert.That(
                runVariables.Text,
                Does.Not.Contain("System.Random"));
            Assert.That(
                saveSection.Text,
                Does.Contain("foundation.random-streams"));
            Assert.That(
                saveSection.Text,
                Does.Match(
                    @"DependsOn\s*=>\s*new\[\]\s*\{\s*DungeonSaveSectionIds\.RunVariables\s*\}"));
            Assert.That(
                sectionIds.Text,
                Does.Match(
                    @"public\s+const\s+string\s+RunVariables\s*=\s*""run\.variables""\s*;"));
            Assert.That(
                runVariableSaveSection.Text,
                Does.Match(
                    @"public\s+const\s+string\s+Id\s*=\s*DungeonSaveSectionIds\.RunVariables\s*;"));
            Assert.That(
                saveSection.Text,
                Does.Not.Contain("RunVariableSaveSection.Id"));
            Assert.That(
                registration.Text,
                Does.Contain("Register<RandomStreamSaveSection>"));
        }

        [Test]
        public void WorldAssemblyOwnsSharedGridPrimitives()
        {
            SourceFile world = SourceBySuffix(
                "World/WorldGridPrimitives.cs");
            SourceFile grid = SourceBySuffix("Grid/Core/Grid.cs");
            SourceFile survival = SourceBySuffix(
                "Survival/DarkSurvivalModels.cs");

            Assert.That(
                File.Exists(Path.Combine(
                    Application.dataPath,
                    "Scripts/Models/World/DungeonStory.World.asmdef")),
                Is.True);
            Assert.That(world.Text, Does.Contain("public enum GridLayer"));
            Assert.That(world.Text, Does.Contain("public enum GridCellAreaType"));
            Assert.That(world.Text, Does.Contain("public enum GridCellTerrainType"));
            Assert.That(world.Text, Does.Contain("public enum GridMoveType"));
            Assert.That(world.Text, Does.Contain("public interface IGridOccupant"));
            Assert.That(world.Text, Does.Contain("public readonly struct GridMoveStep"));
            Assert.That(grid.Text, Does.Not.Contain("public enum GridLayer"));
            Assert.That(grid.Text, Does.Not.Contain("public enum GridMoveType"));
            Assert.That(grid.Text, Does.Not.Contain("public interface IGridOccupant"));
            Assert.That(grid.Text, Does.Not.Contain("public class GridMoveStep"));
            Assert.That(
                survival.Text,
                Does.Not.Contain("public enum GridCellTerrainType"));
        }

        [Test]
        public void GridCoreUsesNamedAssemblyAndReadOnlyOccupantCapabilities()
        {
            string gridDirectory = Path.Combine(
                Application.dataPath,
                "Scripts/Models/Grid/Core");
            string asmdefPath = Path.Combine(
                gridDirectory,
                "DungeonStory.Grid.asmdef");
            Assert.That(File.Exists(asmdefPath), Is.True);

            string asmdef = File.ReadAllText(asmdefPath);
            Assert.That(asmdef, Does.Contain("\"name\": \"DungeonStory.Grid\""));
            Assert.That(asmdef, Does.Contain("\"DungeonStory.Foundation\""));
            Assert.That(asmdef, Does.Contain("\"DungeonStory.World\""));
            Assert.That(asmdef, Does.Not.Contain("Assembly-CSharp"));
            Assert.That(asmdef, Does.Not.Contain("DungeonStory.Buildings"));

            string[] extractedSources =
            {
                "Grid.cs",
                "GridCell.cs",
                "GridCellAreaRules.cs",
                "GridNavigationCost.cs",
                "GridPathSearchBroker.cs",
                "GridPathSearchResult.cs",
                "GridSearchWorkspaces.cs",
                "GridTraversalHeuristicIndex.cs"
            };
            foreach (string source in extractedSources)
            {
                Assert.That(File.Exists(Path.Combine(gridDirectory, source)), Is.True);
            }

            SourceFile grid = SourceBySuffix("Grid/Core/Grid.cs");
            SourceFile areaRules = SourceBySuffix(
                "Grid/Core/GridCellAreaRules.cs");
            SourceFile capabilities = SourceBySuffix(
                "World/GridOccupantCapabilities.cs");
            SourceFile buildable = SourceBySuffix(
                "Buildings/BuildableObject.cs");
            SourceFile buildingDefinition = SourceBySuffix(
                "Buildings/SO/BuildingSO.cs");
            SourceFile doorModels = SourceBySuffix(
                "Buildings/Access/DoorAccessModels.cs");

            Assert.That(grid.Text, Does.Contain("IGridBuildingOccupantCapability"));
            Assert.That(grid.Text, Does.Not.Contain("BuildableObject"));
            Assert.That(grid.Text, Does.Not.Contain("BuildingSO"));
            Assert.That(grid.Text, Does.Not.Contain("FacilityData"));
            Assert.That(areaRules.Text, Does.Contain("IGridBuildAreaCapability"));
            Assert.That(areaRules.Text, Does.Not.Contain("BuildingSO"));
            Assert.That(capabilities.Text,
                Does.Contain("public interface IGridBuildingOccupantCapability"));
            Assert.That(capabilities.Text,
                Does.Contain("public interface IGridBuildAreaCapability"));
            Assert.That(buildable.Text,
                Does.Contain("IGridBuildingOccupantCapability"));
            Assert.That(buildingDefinition.Text,
                Does.Contain("IGridBuildAreaCapability"));
            Assert.That(doorModels.Text,
                Does.Contain("IDoorAccessQuery : IGridTraversalAccessQuery"));
            Assert.That(doorModels.Text,
                Does.Not.Contain("struct GridTraversalContext"));
        }

        [Test]
        public void GridPathBrokerPortsStayExplicitlyRegisteredWithoutDebugCycle()
        {
            SourceFile registration = SourceBySuffix(
                "Infrastructure/Registration/DungeonAiRegistration.cs");
            SourceFile performanceRecorder = SourceBySuffix(
                "Character/AI/CharacterAiPerformanceRecorder.cs");

            Assert.That(
                registration.Text,
                Does.Match(@"As<ICharacterAiPerformanceRecorder>\(\)[\s\S]*?"
                    + @"As<IGridPathPerformanceRecorder>\(\)"));
            Assert.That(
                registration.Text,
                Does.Match(@"As<IDoorAccessQuery>\(\)[\s\S]*?"
                    + @"As<IGridTraversalAccessQuery>\(\)"));
            Assert.That(
                registration.Text,
                Does.Not.Contain("resolver => (IGridPathPerformanceRecorder)"));
            Assert.That(
                registration.Text,
                Does.Not.Contain("resolver => (IGridTraversalAccessQuery)"));
            Assert.That(
                performanceRecorder.Text,
                Does.Not.Contain("IDungeonDebugModeService debugMode"));
        }

        [Test]
        public void ProductionSurgeryDemandReadsAggregateWithoutRuntimeCycle()
        {
            SourceFile demand = SourceBySuffix(
                "Economy/ProductionConsumerDemandAdapters.cs");
            SourceFile state = SourceBySuffix(
                "Medical/Core/SurgeryAggregateState.cs");

            Assert.That(demand.Text,
                Does.Contain("ISurgeryOrderDemandQuery surgery"));
            Assert.That(demand.Text,
                Does.Not.Contain("ISurgeryQuery surgery"));
            Assert.That(state.Text,
                Does.Contain("interface ISurgeryOrderDemandQuery"));
            Assert.That(state.Text,
                Does.Contain("SurgeryAggregateStateStore : ISurgeryOrderDemandQuery"));
        }

        [Test]
        public void CharacterNeedQueryBasePortStaysExplicitlyRegistered()
        {
            SourceFile registration = SourceBySuffix(
                "Infrastructure/Registration/DungeonWorldSimulationRegistration.cs");

            Assert.That(registration.Text,
                Does.Match(@"As<ICharacterNeedDefinitionCatalog>\(\)[\s\S]*?"
                    + @"As<ICharacterNeedDefinitionQuery>\(\)"));
        }

        [Test]
        public void CharacterAssemblyOwnsIdentityPrimitivesAndPerformanceHasSingleAuthority()
        {
            const string legacyStatTypeToken = "Character" + "StatType";
            SourceFile primitives = SourceBySuffix(
                "Characters/CharacterPrimitives.cs");
            SourceFile performanceContracts = SourceBySuffix(
                "Foundation/CharacterPerformanceContracts.cs");
            SourceFile performanceQuery = SourceBySuffix(
                "Character/Core/CharacterPerformanceQuery.cs");
            SourceFile modelData = SourceBySuffix(
                "Character/SO/CharacterModelData.cs");
            SourceFile characterData = SourceBySuffix(
                "Character/SO/CharacterSO.cs");

            Assert.That(
                File.Exists(Path.Combine(
                    Application.dataPath,
                    "Scripts/Models/Characters/DungeonStory.Characters.asmdef")),
                Is.True);
            Assert.That(primitives.Text, Does.Contain("public enum CharacterType"));
            Assert.That(primitives.Text, Does.Contain("public enum CharacterRole"));
            Assert.That(primitives.Text, Does.Contain("public enum CharacterCondition"));
            Assert.That(primitives.Text, Does.Contain("public enum CharacterLifecycleState"));
            Assert.That(
                performanceContracts.Text,
                Does.Contain("public enum CharacterFunctionalCapacityId"));
            Assert.That(
                performanceContracts.Text,
                Does.Contain("public sealed class CharacterPerformanceSnapshot"));
            Assert.That(
                performanceQuery.Text,
                Does.Contain("public interface ICharacterPerformanceQuery"));
            Assert.That(
                File.Exists(Path.Combine(
                    Application.dataPath,
                    "Scripts/Models/Characters/Character" + "StatCatalog.cs")),
                Is.False);
            Assert.That(primitives.Text, Does.Not.Contain(legacyStatTypeToken));
            Assert.That(modelData.Text, Does.Not.Contain(legacyStatTypeToken));
            Assert.That(characterData.Text, Does.Not.Contain("public enum CharacterType"));
            Assert.That(characterData.Text, Does.Not.Contain("public enum CharacterRole"));
        }

        [Test]
        public void CharacterStaticDataDoesNotRollRuntimeRandom()
        {
            SourceFile characterData = SourceBySuffix(
                "Character/SO/CharacterSO.cs");
            SourceFile spawner = SourceBySuffix(
                "Character/CharacterSpawner.cs");
            SourceFile shopping = SourceBySuffix(
                "Character/Ability/AbilityShopping.cs");

            Assert.That(characterData.Text, Does.Contain("IRandomStream randomStream"));
            Assert.That(
                characterData.Text,
                Does.Not.Match(@"\b(?:UnityEngine\.)?Random\."));
            Assert.That(spawner.Text, Does.Contain("\"character-spawner\""));
            Assert.That(shopping.Text, Does.Contain("GetRandomStream()"));
        }

        [Test]
        public void ShopUsesScopedRandomAndHasNoDeadRandomPositionApi()
        {
            SourceFile facility = SourceBySuffix("Buildings/Facility.cs");
            SourceFile shop = SourceBySuffix("Buildings/Shop.cs");

            Assert.That(
                facility.Text,
                Does.Not.Contain("GetRandomUsePosition"));
            Assert.That(
                shop.Text,
                Does.Not.Contain("GetRandomBuyPos"));
            Assert.That(
                shop.Text,
                Does.Contain(".Get(\"shop-runtime\")"));
            Assert.That(
                shop.Text,
                Does.Not.Match(@"\b(?:UnityEngine\.)?Random\."));
        }

        [Test]
        public void CombatResolutionUsesTheScopedRandomStream()
        {
            SourceFile combat = SourceBySuffix(
                "Combat/CombatResolutionService.cs");

            Assert.That(
                combat.Text,
                Does.Contain("IRandomStreamProvider randomStreamProvider"));
            Assert.That(
                combat.Text,
                Does.Contain(".Get(\"combat-resolution\")"));
            Assert.That(
                combat.Text,
                Does.Not.Match(@"\bUnityEngine\.Random\."));
        }

        [Test]
        public void WorkAssemblyOwnsStableWorkIds()
        {
            SourceFile workIds = SourceBySuffix("Work/WorkTypeId.cs");
            SourceFile catalog = SourceBySuffix(
                "Work/WorkTypeCatalog.cs");
            SourceFile priorityContract = SourceBySuffix(
                "Work/WorkPriorityLevel.cs");
            SourceFile facilityWorkTypeMap = SourceBySuffix(
                "Models/Work/FacilityWorkTypeMap.cs");
            SourceFile executionRegistry = SourceBySuffix(
                "Character/Work/WorkExecutionRegistry.cs");
            SourceFile executor = SourceBySuffix(
                "Character/Work/WorkTaskExecutor.cs");
            SourceFile workAmount = SourceBySuffix(
                "Character/Work/WorkAmountSystem.cs");
            SourceFile workOrderContracts = SourceBySuffix(
                "Character/Work/WorkOrderContracts.cs");
            SourceFile abilityWork = SourceBySuffix(
                "Character/Ability/AbilityWork.cs");
            SourceFile priorities = SourceBySuffix(
                "Character/Work/WorkPriorityProfile.cs");
            SourceFile survivalWork = SourceBySuffix(
                "Survival/Work/SurvivalWorkExecutionHandler.cs");
            SourceFile repairWork = SourceBySuffix(
                "Combat/Work/RepairWorkExecutionHandler.cs");
            SourceFile characterSave = SourceBySuffix(
                "Infrastructure/CharacterWorldSaveService.cs");
            SourceFile aiUtility = SourceBySuffix(
                "Character/AI/CharacterAiUtilityModels.cs");
            SourceFile aiMemory = SourceBySuffix(
                "Character/AI/CharacterAiMemoryRuntime.cs");
            SourceFile aiWorkAction = SourceBySuffix(
                "Character/AI/Action/AIWork.cs");
            SourceFile aiWaitAction = SourceBySuffix(
                "Infrastructure/AI/Actions/AIWaitAdapter.cs");
            SourceFile aiHaul = SourceBySuffix("Character/AI/Action/AIHaul.cs");
            SourceFile aiHunt = SourceBySuffix("Character/AI/Action/AIHunt.cs");
            SourceFile aiRescue = SourceBySuffix("Character/AI/Action/AIRescue.cs");
            SourceFile considerationWorkNeed = SourceBySuffix(
                "Infrastructure/AI/Considerations/ConsiderationWorkNeedAdapter.cs");
            SourceFile combatLoadout = SourceBySuffix(
                "Combat/CombatLoadoutPreparationRuntime.cs");
            SourceFile staffDiscontent = SourceBySuffix(
                "Character/Work/StaffDiscontentRuntime.cs");
            SourceFile deprivation = SourceBySuffix(
                "Survival/CharacterDeprivationRuntime.cs");
            SourceFile defenseUi = SourceBySuffix(
                "UI/DefenseFeatureQueryService.cs");
            SourceFile researchUi = SourceBySuffix(
                "Character/AI/CharacterAiUtilityModels.cs");
            SourceFile defenseEngagement = SourceBySuffix(
                "Invasion/DefenseEngagementRuntime.cs");
            SourceFile workPriorityPanel = SourceBySuffix(
                "Character/UI/StaffWorkPriorityPanel.cs");
            SourceFile workPriorityPanelModel = SourceBySuffix(
                "Character/UI/StaffWorkPriorityPanelModel.cs");
            SourceFile workDutyController = SourceBySuffix(
                "Character/Work/WorkDutyController.cs");
            SourceFile workforceReplan = SourceBySuffix(
                "Character/Work/WorkforceReplanService.cs");
            SourceFile blueprintResearchRuntime = SourceBySuffix(
                "Infrastructure/BlueprintResearchRuntime.cs");
            SourceFile blueprintResearchContracts = SourceBySuffix(
                "Infrastructure/BlueprintResearchContracts.cs");
            SourceFile characterActor = SourceBySuffix(
                "Character/Core/CharacterActor.cs");
            SourceFile characterActivity = SourceBySuffix(
                "Character/Core/CharacterActivityEvent.cs");
            SourceFile characterStats = SourceBySuffix(
                "Character/Core/CharacterStats.cs");
            SourceFile characterModelData = SourceBySuffix(
                "Character/SO/CharacterModelData.cs");
            SourceFile characterAuthoredModel = SourceBySuffix(
                "Models/Characters/CharacterAuthoredModel.cs");
            SourceFile characterSo = SourceBySuffix(
                "Character/SO/CharacterSO.cs");
            SourceFile equipmentCrafting = SourceBySuffix(
                "Combat/Buildings/EquipmentCraftingBuildingAbilityHandler.cs");
            SourceFile abilityRescue = SourceBySuffix(
                "Combat/AbilityRescue.cs");
            SourceFile workTargetCandidate = SourceBySuffix(
                "Models/Work/WorkTargetCandidate.cs");
            SourceFile workTargetSelector = SourceBySuffix(
                "Character/Work/WorkTargetSelector.cs");
            SourceFile workTargetEvaluator = SourceBySuffix(
                "Character/Work/WorkTargetEvaluator.cs");
            SourceFile workCommandHandler = SourceBySuffix(
                "Character/Work/WorkCommandHandler.cs");
            SourceFile constructionSite = SourceBySuffix(
                "Grid/Building/ConstructionSite.cs");
            SourceFile buildingSummaryFormatter = SourceBySuffix(
                "Buildings/BuildingSummaryFormatter.cs");
            SourceFile uiBuildingInfo = SourceBySuffix(
                "Buildings/UI/UIBuildingInfo.cs");
            SourceFile debugCommands = SourceBySuffix(
                "Debugging/DungeonDebugCommandProviders.cs");
            SourceFile buildingAbilityRuntime = SourceBySuffix(
                "Buildings/Abilities/BuildingAbilityRuntime.cs");
            SourceFile buildingAbilityAccessors = SourceBySuffix(
                "Buildings/Abilities/BuildingAbilityAccessors.cs");
            SourceFile buildingAbilityHandlers = SourceBySuffix(
                "Buildings/Abilities/BuildingAbilityHandlers.cs");
            SourceFile buildingAbility = SourceBySuffix(
                "Buildings/Abilities/BuildingAbility.cs");
            SourceFile codexFormatter = SourceBySuffix(
                "Models/Codex/Core/CodexTextFormatter.cs");
            SourceFile codexDomainFormatter = SourceBySuffix(
                "Codex/CodexRuntimeApplicationAdapter.cs");
            SourceFile cleanWork = SourceBySuffix(
                "Survival/Work/CleanWorkExecutionHandler.cs");
            SourceFile survivalFood = SourceBySuffix(
                "Survival/SurvivalFoodRuntime.cs");
            SourceFile survivalFacilityUtility = SourceBySuffix(
                "Survival/SurvivalFacilityUtility.cs");
            SourceFile researchWork = SourceBySuffix(
                "Infrastructure/ResearchWorkExecutionAdapter.cs");
            SourceFile wildlifeModels = SourceBySuffix(
                "Wildlife/WildlifeModels.cs");
            SourceFile buildableObject = SourceBySuffix(
                "Buildings/BuildableObject.cs");
            SourceFile buildingSo = SourceBySuffix("Buildings/SO/BuildingSO.cs");
            SourceFile facility = SourceBySuffix("Buildings/Facility.cs");
            SourceFile shop = SourceBySuffix("Buildings/Shop.cs");
            SourceFile roomEnvironment = SourceBySuffix(
                "Infrastructure/Rooms/RoomEnvironmentAdapter.cs");
            SourceFile roomEnvironmentExperience = SourceBySuffix(
                "Infrastructure/Rooms/RoomEnvironmentExperienceAdapter.cs");
            string workPolicyRegistryInterface = Regex.Match(
                    executionRegistry.Text,
                    @"public interface IWorkPolicyRegistry\s*\{(?<body>[\s\S]*?)\n\}")
                .Groups["body"]
                .Value;

            Assert.That(
                File.Exists(Path.Combine(
                    Application.dataPath,
                    "Scripts/Models/Work/DungeonStory.Work.asmdef")),
                Is.True);
            Assert.That(workIds.Text, Does.Contain("public readonly struct WorkTypeId"));
            Assert.That(workIds.Text, Does.Contain("public static class BuiltInWorkTypeIds"));
            Assert.That(
                priorityContract.Text,
                Does.Contain("public enum WorkPriorityLevel"));
            Assert.That(
                priorityContract.Text,
                Does.Contain("public static class WorkPriorityLevelExtensions"));
            Assert.That(catalog.Text, Does.Contain("public WorkTypeId WorkTypeId"));
            Assert.That(catalog.Text, Does.Not.Contain("FacilityWorkType"));
            Assert.That(
                facilityWorkTypeMap.Text,
                Does.Contain("public static class FacilityWorkTypeMap"));
            Assert.That(
                facilityWorkTypeMap.Text,
                Does.Contain("Map(FacilityWorkType.Operate, BuiltInWorkTypeIds.Operate)"));
            Assert.That(
                priorities.Text,
                Does.Not.Contain("public enum WorkPriorityLevel"));
            Assert.That(
                catalog.Text,
                Does.Contain("private static readonly WorkTypeDefinition[] Definitions"));
            Assert.That(
                catalog.Text,
                Does.Not.Contain("public WorkTypeDefinition(\n        string id,\n        FacilityWorkType"));
            Assert.That(
                catalog.Text,
                Does.Not.Contain("public WorkTypeDefinition(\n        WorkTypeId id,\n        FacilityWorkType"));
            Assert.That(
                catalog.Text,
                Does.Contain("Definition(BuiltInWorkTypeIds.Operate"));
            Assert.That(
                catalog.Text,
                Does.Not.Match(@"Definition\(\s*new WorkTypeId\(\s*""work:"));
            Assert.That(
                executionRegistry.Text,
                Does.Not.Contain("public readonly struct WorkTypeId"));
            Assert.That(
                executionRegistry.Text,
                Does.Not.Contain("public static class BuiltInWorkTypeIds"));
            Assert.That(
                executionRegistry.Text,
                Does.Contain("bool TryGet(WorkTypeId workTypeId"));
            Assert.That(
                executionRegistry.Text,
                Does.Not.Contain("bool TryGet(FacilityWorkType legacyWorkType"));
            Assert.That(
                catalog.Text,
                Does.Not.Contain("public static bool TryGet(FacilityWorkType"));
            Assert.That(
                catalog.Text,
                Does.Not.Contain("public static WorkTypeDefinition GetRequired(FacilityWorkType"));
            Assert.That(
                catalog.Text,
                Does.Not.Contain("public static IEnumerable<WorkTypeDefinition> Enumerate(FacilityWorkType"));
            Assert.That(
                executionRegistry.Text,
                Does.Match(@"float\s+GetStatMultiplier\s*\(\s*WorkTypeId workTypeId"));
            Assert.That(
                executionRegistry.Text,
                Does.Not.Contain("float GetStatMultiplier(\n        FacilityWorkType legacyWorkType"));
            Assert.That(
                executionRegistry.Text,
                Does.Match(
                    @"float\s+CalculateWorkPerSecond\s*\(\s*CharacterActor\s+actor\s*,\s*BuildableObject\s+target\s*,\s*WorkTypeId\s+workTypeId\b"));
            Assert.That(
                executionRegistry.Text,
                Does.Not.Contain("FacilityWorkType legacyWorkType,\n        float environmentDurationMultiplier"));
            Assert.That(
                executionRegistry.Text,
                Does.Match(
                    @"bool\s+IsAvailable\s*\(\s*WorkTypeId\s+workTypeId\b"));
            Assert.That(
                executionRegistry.Text,
                Does.Not.Contain("bool IsAvailable(\n        FacilityWorkType"));
            Assert.That(
                executionRegistry.Text,
                Does.Not.Contain("provider.IsAvailable(definition.Type"));
            Assert.That(
                executionRegistry.Text,
                Does.Match(
                    @"float\s+GetAdditionalUrgency\s*\(\s*WorkTypeId\s+workTypeId\b"));
            Assert.That(
                executionRegistry.Text,
                Does.Match(
                    @"float\s+GetUrgency\s*\(\s*WorkTypeId\s+workTypeId\b"));
            Assert.That(
                executionRegistry.Text,
                Does.Not.Contain("float GetUrgency(\n        FacilityWorkType"));
            Assert.That(
                executionRegistry.Text,
                Does.Not.Contain("provider.GetUrgency(definition.Type"));
            Assert.That(
                workPolicyRegistryInterface,
                Does.Not.Contain("FacilityWorkType"));
            Assert.That(
                executionRegistry.Text,
                Does.Contain("policies.GetStatMultiplier(definition.WorkTypeId"));
            Assert.That(
                executionRegistry.Text,
                Does.Contain("performance.EvaluateWork("));
            Assert.That(
                executionRegistry.Text,
                Does.Contain("CharacterPerformanceResultChannel.Speed"));
            Assert.That(
                executionRegistry.Text,
                Does.Not.Contain("actor.GetWorkSpeedMultiplier(definition.Type"));
            Assert.That(
                executionRegistry.Text,
                Does.Contain("public WorkTypeId WorkTypeId"));
            Assert.That(
                executionRegistry.Text,
                Does.Not.Contain("public FacilityWorkType LegacyWorkType"));
            Assert.That(
                executionRegistry.Text,
                Does.Not.Contain("FacilityWorkType legacyWorkType"));
            Assert.That(
                executionRegistry.Text,
                Does.Not.Contain("ResolveWorkTypeId"));
            Assert.That(
                executionRegistry.Text,
                Does.Not.Contain("new WorkTypeId($\"work:{(int)legacyWorkType}\""));
            Assert.That(
                executor.Text,
                Does.Match(@"executionHandlers\.TryGet\s*\(\s*workTypeId"));
            Assert.That(
                executor.Text,
                Does.Match(
                    @"WorkExecutionRules\.CalculateWorkPerSecond\s*\(\s*calculator\s*,\s*actor\s*,\s*target\s*,\s*workTypeId\b"));
            Assert.That(
                workAmount.Text,
                Does.Match(
                    @"bool\s+TryGetOrderFor\s*\(\s*BuildableObject\s+target\s*,\s*WorkTypeId\s+workTypeId"));
            Assert.That(
                workAmount.Text,
                Does.Match(
                    @"bool\s+ApplyWork\s*\(\s*CharacterActor\s+worker\s*,\s*BuildableObject\s+target\s*,\s*WorkTypeId\s+workTypeId"));
            Assert.That(
                workOrderContracts.Text,
                Does.Contain("public string workTypeId"));
            Assert.That(
                workOrderContracts.Text,
                Does.Contain("public WorkTypeId WorkTypeId { get; set; }"));
            Assert.That(
                workAmount.Text,
                Does.Not.Contain("public FacilityWorkType workType"));
            Assert.That(
                workAmount.Text,
                Does.Not.Contain("public FacilityWorkType WorkType"));
            Assert.That(
                workAmount.Text,
                Does.Not.Contain("TryGetOrderFor(BuildableObject target, FacilityWorkType"));
            Assert.That(
                workAmount.Text,
                Does.Not.Contain("ApplyWork(CharacterActor worker, BuildableObject target, FacilityWorkType"));
            Assert.That(
                workAmount.Text,
                Does.Contain("private WorkOrderRecord FindOrder(BuildableObject target, WorkTypeId workTypeId"));
            Assert.That(
                workAmount.Text,
                Does.Not.Contain("FindOrder(BuildableObject target, FacilityWorkType"));
            Assert.That(
                executor.Text,
                Does.Contain("workOrderRuntime.TryGetOrderFor(assignedTarget, workTypeId"));
            Assert.That(
                executor.Text,
                Does.Match(
                    @"workOrderRuntime\.ApplyWork\s*\(\s*actor\s*,\s*target\s*,\s*workTypeId\b"));
            Assert.That(
                executor.Text,
                Does.Not.Contain("workOrderRuntime.ApplyWork(\n                    actor,\n                    target,\n                    workType,"));
            Assert.That(
                constructionSite.Text,
                Does.Contain("TryGetOrderFor(this, BuiltInWorkTypeIds.Construct"));
            Assert.That(
                constructionSite.Text,
                Does.Not.Contain("TryGetOrderFor(this, FacilityWorkType.Construct"));
            Assert.That(
                buildingSummaryFormatter.Text,
                Does.Match(
                    @"workOrderSummaryQuery\.TryGetOrder\s*\(\s*site\s*,\s*BuiltInWorkTypeIds\.Construct"));
            Assert.That(
                buildingSummaryFormatter.Text,
                Does.Not.Contain("TryGetOrderFor(site, FacilityWorkType.Construct"));
            Assert.That(
                uiBuildingInfo.Text,
                Does.Contain("TryGetOrderFor(site, BuiltInWorkTypeIds.Construct"));
            Assert.That(
                uiBuildingInfo.Text,
                Does.Not.Contain("TryGetOrderFor(site, FacilityWorkType.Construct"));
            Assert.That(
                debugCommands.Text,
                Does.Contain("foreach (WorkTypeDefinition definition in WorkTypeCatalog.All)"));
            Assert.That(
                debugCommands.Text,
                Does.Not.Contain("Enum.GetValues(typeof(FacilityWorkType))"));
            Assert.That(
                buildingAbilityRuntime.Text,
                Does.Contain("GetRequiredWork(BuildableObject building, WorkTypeId workTypeId"));
            Assert.That(
                buildingAbilityRuntime.Text,
                Does.Not.Contain("GetRequiredWork(BuildableObject building, FacilityWorkType"));
            Assert.That(
                buildingAbilityRuntime.Text,
                Does.Contain("SupportsExteriorWork(WorkTypeId workTypeId)"));
            Assert.That(
                buildingAbilityRuntime.Text,
                Does.Not.Contain("SupportsExteriorWork(FacilityWorkType"));
            Assert.That(
                buildingAbilityRuntime.Text,
                Does.Not.Contain("IsExteriorWorkAvailable(CharacterActor actor, BuildableObject building, FacilityWorkType"));
            Assert.That(
                buildingAbility.Text,
                Does.Contain("SupportsExteriorWork(WorkTypeId workTypeId)"));
            Assert.That(
                buildingAbility.Text,
                Does.Not.Contain("SupportsExteriorWork(FacilityWorkType"));
            Assert.That(
                codexDomainFormatter.Text,
                Does.Contain("public static string FormatWorkTypes(IEnumerable<WorkTypeId> workTypeIds)"));
            Assert.That(
                codexDomainFormatter.Text,
                Does.Not.Contain("public static string FormatWorkTypes(FacilityWorkType"));
            Assert.That(
                codexFormatter.Text,
                Does.Not.Contain("FacilityWorkType"));
            Assert.That(
                buildingAbilityAccessors.Text,
                Does.Contain("GetRequiredWork(this BuildingSO building, WorkTypeId workTypeId"));
            Assert.That(
                buildingAbilityAccessors.Text,
                Does.Not.Contain("GetRequiredWork(this BuildingSO building, FacilityWorkType"));
            Assert.That(
                buildingAbility.Text,
                Does.Contain("GetRequiredWork(BuildableObject building, WorkTypeId workTypeId"));
            Assert.That(
                buildingAbility.Text,
                Does.Not.Contain("GetRequiredWork(BuildableObject building, FacilityWorkType"));
            Assert.That(
                buildingAbilityHandlers.Text,
                Does.Contain("public WorkTypeId WorkTypeId { get; }"));
            Assert.That(
                buildingAbilityHandlers.Text,
                Does.Not.Contain("public FacilityWorkType WorkType { get; }"));
            Assert.That(
                buildingAbilityHandlers.Text,
                Does.Match(
                    @"public\s+BuildingAbilityWorkContext\s*\(\s*IBuildingVisitorPort\s+actor\s*,\s*BuildableObject\s+building\s*,\s*WorkTypeId\s+workTypeId"));
            Assert.That(
                buildingAbilityHandlers.Text,
                Does.Not.Match(
                    @"public\s+BuildingAbilityWorkContext\s*\([^)]*FacilityWorkType"));
            Assert.That(
                buildingAbilityHandlers.Text,
                Does.Not.Contain("int ApplyWorkCompleted(\n        CharacterActor actor,\n        BuildableObject building,\n        FacilityWorkType"));
            Assert.That(
                buildingSo.Text,
                Does.Contain("public bool SupportsWork(WorkTypeId workTypeId)"));
            Assert.That(
                buildingSo.Text,
                Does.Not.Contain("public bool SupportsWork(FacilityWorkType"));
            Assert.That(
                buildableObject.Text,
                Does.Contain("public bool SupportsWork(WorkTypeId workTypeId)"));
            Assert.That(
                buildableObject.Text,
                Does.Not.Contain("public bool SupportsWork(FacilityWorkType"));
            Assert.That(
                buildableObject.Text,
                Does.Match(
                    @"public\s+bool\s+CanAssignWork\s*\(\s*WorkTypeId\s+workTypeId"));
            Assert.That(
                buildableObject.Text,
                Does.Not.Contain("public bool CanAssignWork(FacilityWorkType"));
            Assert.That(
                buildableObject.Text,
                Does.Match(
                    @"public\s+FacilityAssignmentStatus\s+GetWorkAssignmentStatus\s*\(\s*WorkTypeId\s+workTypeId\s*\)"));
            Assert.That(
                buildableObject.Text,
                Does.Not.Contain("public FacilityAssignmentStatus GetWorkAssignmentStatus(FacilityWorkType"));
            Assert.That(
                buildableObject.Text,
                Does.Contain("public float GetWorkUrgency(WorkTypeId workTypeId)"));
            Assert.That(
                buildableObject.Text,
                Does.Not.Contain("public virtual float GetWorkUrgency(FacilityWorkType"));
            Assert.That(
                buildableObject.Text,
                Does.Contain("internal virtual float GetLegacyWorkUrgency(FacilityWorkType"));
            Assert.That(
                roomEnvironment.Text,
                Does.Contain("float GetWorkDurationMultiplier(BuildableObject facility, WorkTypeId workTypeId)"));
            Assert.That(
                roomEnvironment.Text,
                Does.Not.Contain("float GetWorkDurationMultiplier(BuildableObject facility, FacilityWorkType"));
            Assert.That(
                roomEnvironment.Text,
                Does.Contain("internal float GetLegacyWorkDurationMultiplier(BuildableObject facility, FacilityWorkType workType)"));
            Assert.That(
                roomEnvironmentExperience.Text,
                Does.Contain("public WorkTypeId WorkTypeId { get; }"));
            Assert.That(
                roomEnvironmentExperience.Text,
                Does.Not.Contain("public FacilityWorkType WorkType { get; }"));
            Assert.That(
                roomEnvironmentExperience.Text,
                Does.Not.Contain("FacilityWorkType workType = FacilityWorkType.None"));
            Assert.That(
                survivalFood.Text,
                Does.Contain("public bool TryApplySurvivalWork("));
            Assert.That(
                survivalFood.Text,
                Does.Contain("WorkTypeId workTypeId"));
            Assert.That(
                survivalFood.Text,
                Does.Not.Contain("public bool TryApplySurvivalWork(CharacterActor actor, BuildableObject building, FacilityWorkType"));
            Assert.That(
                survivalFood.Text,
                Does.Not.Contain("public bool HasSurvivalWorkAvailable(BuildableObject building, FacilityWorkType"));
            Assert.That(
                survivalFood.Text,
                Does.Not.Contain("public float GetSurvivalWorkUrgency(BuildableObject building, FacilityWorkType"));
            Assert.That(
                wildlifeModels.Text,
                Does.Contain("bool HasSurvivalWorkAvailable(BuildableObject building, WorkTypeId workTypeId)"));
            Assert.That(
                wildlifeModels.Text,
                Does.Not.Contain("HasSurvivalWorkAvailable(BuildableObject building, FacilityWorkType"));
            Assert.That(
                wildlifeModels.Text,
                Does.Not.Contain("public static FacilityWorkType AddFallbackWorkTypes"));
            Assert.That(
                survivalFacilityUtility.Text,
                Does.Not.Contain("public static FacilityWorkType AddFallbackWorkTypes"));
            Assert.That(
                survivalFacilityUtility.Text,
                Does.Not.Contain("public static bool IsSurvivalWork(FacilityWorkType"));
            Assert.That(
                SourceBySuffix("Combat/EquipmentMaintenanceRuntime.cs").Text,
                Does.Not.Contain("public static FacilityWorkType AddFallbackWorkTypes"));
            Assert.That(
                workAmount.Text,
                Does.Contain("building.GetRequiredWork(BuiltInWorkTypeIds.Construct"));
            Assert.That(
                repairWork.Text,
                Does.Match(
                    @"GetRequiredWork\s*\(\s*BuiltInWorkTypeIds\.Repair\s*\)"));
            Assert.That(
                cleanWork.Text,
                Does.Contain("GetRequiredWork(BuiltInWorkTypeIds.Clean"));
            Assert.That(
                researchWork.Text,
                Does.Match(
                    @"GetRequiredWork\s*\(\s*BuiltInWorkTypeIds\.Research\s*\)"));
            Assert.That(
                executor.Text,
                Does.Contain("GetWorkEnvironmentDurationMultiplier(BuiltInWorkTypeIds.Restock"));
            Assert.That(
                executor.Text,
                Does.Not.Contain("GetWorkEnvironmentDurationMultiplier(FacilityWorkType.Restock"));
            Assert.That(
                executor.Text,
                Does.Not.Contain("actor.GetWorkSpeedMultiplier(workType)"));
            Assert.That(
                survivalWork.Text,
                Does.Contain("context.WorkTypeId"));
            Assert.That(
                survivalWork.Text,
                Does.Not.Contain("TryResolveWorkTypeId"));
            Assert.That(
                repairWork.Text,
                Does.Contain("context.WorkTypeId"));
            Assert.That(
                repairWork.Text,
                Does.Not.Contain("GetWorkEnvironmentDurationMultiplier(FacilityWorkType.Repair"));
            Assert.That(
                priorities.Text,
                Does.Contain("public WorkPriorityLevel GetPriority(WorkTypeId workTypeId)"));
            Assert.That(
                priorities.Text,
                Does.Not.Contain("public WorkPriorityLevel GetPriority(FacilityWorkType"));
            Assert.That(
                priorities.Text,
                Does.Not.Contain("public void SetPriority(FacilityWorkType"));
            Assert.That(
                priorities.Text,
                Does.Not.Contain("public bool IsEnabled(FacilityWorkType"));
            Assert.That(
                priorities.Text,
                Does.Not.Contain("public void ApplyPreferredTypes(FacilityWorkType"));
            Assert.That(
                priorities.Text,
                Does.Not.Contain("public static string GetDisplayName(FacilityWorkType"));
            Assert.That(
                priorities.Text,
                Does.Contain("internal static string GetLegacyDisplayName(FacilityWorkType"));
            Assert.That(
                abilityWork.Text,
                Does.Contain("public void SetWorkPriority(WorkTypeId workTypeId"));
            Assert.That(
                abilityWork.Text,
                Does.Contain("public WorkTypeId AssignedWorkTypeId"));
            Assert.That(
                abilityWork.Text,
                Does.Contain("public WorkTypeId PriorityWorkTypeId"));
            Assert.That(
                abilityWork.Text,
                Does.Not.Contain("public FacilityWorkType AssignedWorkType"));
            Assert.That(
                abilityWork.Text,
                Does.Not.Contain("public FacilityWorkType PriorityWorkType"));
            Assert.That(
                workCommandHandler.Text,
                Does.Not.Contain("public FacilityWorkType PriorityWorkType"));
            Assert.That(
                abilityWork.Text,
                Does.Contain("public bool IsAssignedWork(WorkTypeId workTypeId)"));
            Assert.That(
                abilityWork.Text,
                Does.Contain("GetWorkEnvironmentDurationMultiplier(WorkTypeId workTypeId)"));
            Assert.That(
                abilityWork.Text,
                Does.Contain("ShouldThrottleRoutineWork(WorkTypeId workTypeId)"));
            Assert.That(
                abilityWork.Text,
                Does.Contain("BeginRoutineWorkCooldown(WorkTypeId workTypeId)"));
            Assert.That(
                abilityWork.Text,
                Does.Match(
                    @"TryGetBestWorkCandidate\s*\(\s*WorkTypeId\s+requestedWorkTypeId\b"));
            Assert.That(
                abilityWork.Text,
                Does.Not.Contain("public bool TryGetBestWorkCandidate(\n        FacilityWorkType"));
            Assert.That(
                abilityWork.Text,
                Does.Contain("TryAssignWork(WorkTypeId requestedWorkTypeId"));
            Assert.That(
                abilityWork.Text,
                Does.Contain("GetWorkUtilityScore(WorkTypeId requestedWorkTypeId"));
            Assert.That(
                abilityWork.Text,
                Does.Contain("CanStartWorkAction(WorkTypeId requestedWorkTypeId"));
            Assert.That(
                abilityWork.Text,
                Does.Not.Contain("public bool TryAssignWork(FacilityWorkType"));
            Assert.That(
                abilityWork.Text,
                Does.Not.Contain("public float GetWorkUtilityScore(FacilityWorkType"));
            Assert.That(
                abilityWork.Text,
                Does.Not.Contain("public bool CanStartWorkAction(FacilityWorkType"));
            Assert.That(
                abilityWork.Text,
                Does.Not.Contain("public void SetWorkPriority(FacilityWorkType"));
            Assert.That(
                abilityWork.Text,
                Does.Not.Contain("public bool ShouldThrottleRoutineWork(FacilityWorkType"));
            Assert.That(
                abilityWork.Text,
                Does.Not.Contain("public void BeginRoutineWorkCooldown(FacilityWorkType"));
            Assert.That(
                abilityWork.Text,
                Does.Contain("public void StartAnyWork(BuildableObject preferredTarget = null)"));
            Assert.That(
                abilityWork.Text,
                Does.Match(
                    @"public\s+void\s+StartWorking\s*\(\s*WorkTypeId\s+requestedWorkTypeId\b"));
            Assert.That(
                abilityWork.Text,
                Does.Not.Contain("public void StartWorking(\n        FacilityWorkType"));
            Assert.That(
                abilityWork.Text,
                Does.Match(
                    @"public\s+bool\s+TryAssignWorkTarget\s*\(\s*BuildableObject\s+target\s*,\s*WorkTypeId\s+requestedWorkTypeId\b"));
            Assert.That(
                abilityWork.Text,
                Does.Not.Contain("public bool TryAssignWorkTarget(\n        BuildableObject target,\n        FacilityWorkType"));
            Assert.That(
                abilityWork.Text,
                Does.Match(
                    @"public\s+bool\s+TrySetPriorityWorkTarget\s*\(\s*BuildableObject\s+building\s*,\s*WorkTypeId\s+preferredWorkTypeId\b"));
            Assert.That(
                abilityWork.Text,
                Does.Not.Contain("public bool TrySetPriorityWorkTarget(\n        BuildableObject building,\n        FacilityWorkType"));
            Assert.That(
                workCommandHandler.Text,
                Does.Match(
                    @"public\s+bool\s+TrySetPriorityWorkTarget\s*\(\s*BuildableObject\s+building\s*,\s*WorkTypeId\s+preferredWorkTypeId\b"));
            Assert.That(
                workCommandHandler.Text,
                Does.Not.Contain("public bool TrySetPriorityWorkTarget(\n        BuildableObject building,\n        FacilityWorkType"));
            Assert.That(
                workTargetSelector.Text,
                Does.Contain("TryAssignAnyWork(GridPathSearchResult searchResult"));
            Assert.That(
                workTargetSelector.Text,
                Does.Match(
                    @"TryAssignWork\s*\(\s*GridPathSearchResult\s+searchResult\s*,\s*WorkTypeId\s+requestedWorkTypeId\b"));
            Assert.That(
                workTargetSelector.Text,
                Does.Not.Contain("public bool TryAssignWork(\n        GridPathSearchResult searchResult = null,\n        FacilityWorkType"));
            Assert.That(
                workTargetSelector.Text,
                Does.Match(
                    @"HasUrgentAvailableWork\s*\(\s*GridPathSearchResult\s+searchResult\s*,\s*WorkTypeId\s+requestedWorkTypeId\b"));
            Assert.That(
                workTargetSelector.Text,
                Does.Not.Contain("public bool HasUrgentAvailableWork(\n        GridPathSearchResult searchResult,\n        FacilityWorkType"));
            Assert.That(
                workTargetSelector.Text,
                Does.Match(
                    @"TryGetBestCandidate\s*\(\s*WorkTypeId\s+requestedWorkTypeId\b"));
            Assert.That(
                workTargetSelector.Text,
                Does.Not.Contain("public bool TryGetBestCandidate(\n        FacilityWorkType"));
            Assert.That(
                workTargetSelector.Text,
                Does.Contain("GetUtilityScore(WorkTypeId requestedWorkTypeId"));
            Assert.That(
                workTargetSelector.Text,
                Does.Not.Contain("public float GetUtilityScore(FacilityWorkType"));
            Assert.That(
                workTargetSelector.Text,
                Does.Not.Contain("public bool TryEvaluateWorkTarget(\n        BuildableObject building,\n        GridPathSearchResult searchResult,\n        FacilityWorkType"));
            Assert.That(
                aiWorkAction.Text,
                Does.Contain("work.StartAnyWork(selectedDestination)"));
            Assert.That(
                aiWorkAction.Text,
                Does.Not.Contain("work.StartWorking(workType, selectedDestination)"));
            Assert.That(
                aiWorkAction.Text,
                Does.Contain("work.GetWorkUtilityScore(workTypeId"));
            Assert.That(
                aiWorkAction.Text,
                Does.Contain("public WorkTypeId WorkTypeId"));
            Assert.That(
                aiWorkAction.Text,
                Does.Not.Contain("public FacilityWorkType WorkType"));
            Assert.That(
                aiWorkAction.Text,
                Does.Match(@"work\.GetAnyWorkUtilityScore\s*\("));
            Assert.That(
                aiWorkAction.Text,
                Does.Not.Contain("work.GetWorkUtilityScore(FacilityWorkType.None"));
            Assert.That(
                aiWorkAction.Text,
                Does.Contain("work.CanStartWorkAction(workTypeId"));
            Assert.That(
                aiWorkAction.Text,
                Does.Match(@"work\.CanStartAnyWorkAction\s*\("));
            Assert.That(
                aiWorkAction.Text,
                Does.Not.Contain("work.CanStartWorkAction(FacilityWorkType.None"));
            Assert.That(
                aiWorkAction.Text,
                Does.Contain("work.TryGetBestWorkCandidate(workTypeId"));
            Assert.That(
                aiWorkAction.Text,
                Does.Contain("work.TryGetBestAnyWorkCandidate(searchResult"));
            Assert.That(
                aiWorkAction.Text,
                Does.Not.Contain("work.TryGetBestWorkCandidate(FacilityWorkType.None"));
            Assert.That(
                aiWaitAction.Text,
                Does.Match(@"work\.GetAnyWorkUtilityScore\s*\("));
            Assert.That(
                aiWaitAction.Text,
                Does.Not.Contain("work.GetWorkUtilityScore(FacilityWorkType.None"));
            Assert.That(
                considerationWorkNeed.Text,
                Does.Match(@"work\.GetAnyWorkUtilityScore\s*\("));
            Assert.That(
                considerationWorkNeed.Text,
                Does.Contain("public WorkTypeId WorkTypeId"));
            Assert.That(
                considerationWorkNeed.Text,
                Does.Not.Contain("public FacilityWorkType WorkType"));
            Assert.That(
                considerationWorkNeed.Text,
                Does.Not.Contain("work.GetWorkUtilityScore(FacilityWorkType.None"));
            Assert.That(
                workforceReplan.Text,
                Does.Contain("work.CanStartWorkAction(requestedWorkTypeId"));
            Assert.That(
                workforceReplan.Text,
                Does.Match(
                    @"work\.TryGetBestWorkCandidate\s*\(\s*requestedWorkTypeId\b"));
            Assert.That(
                workforceReplan.Text,
                Does.Not.Contain("work.CanStartWorkAction(workType"));
            Assert.That(
                workforceReplan.Text,
                Does.Not.Contain("work.TryGetBestWorkCandidate(workType"));
            Assert.That(
                aiWorkAction.Text,
                Does.Not.Contain("work.GetWorkUtilityScore(workType,"));
            Assert.That(
                aiWorkAction.Text,
                Does.Not.Contain("work.CanStartWorkAction(workType,"));
            Assert.That(
                considerationWorkNeed.Text,
                Does.Contain("work.GetWorkUtilityScore(workTypeId"));
            Assert.That(
                considerationWorkNeed.Text,
                Does.Not.Contain("work.GetWorkUtilityScore(workType,"));
            Assert.That(
                characterSave.Text,
                Does.Contain("work.SetWorkPriority(definition.WorkTypeId"));
            Assert.That(
                characterSave.Text,
                Does.Not.Contain("work.SetWorkPriority(definition.Type"));
            Assert.That(
                aiUtility.Text,
                Does.Not.Contain("WorkPriorities.GetPriority(FacilityWorkType."));
            Assert.That(aiUtility.Text, Does.Contain("BuiltInWorkTypeIds.Operate"));
            Assert.That(aiHaul.Text, Does.Contain("BuiltInWorkTypeIds.Haul"));
            Assert.That(aiHunt.Text, Does.Contain("BuiltInWorkTypeIds.Hunt"));
            Assert.That(aiRescue.Text, Does.Contain("BuiltInWorkTypeIds.Rescue"));
            Assert.That(aiMemory.Text, Does.Contain("public string workTypeId"));
            Assert.That(
                aiMemory.Text,
                Does.Match(
                    @"public\s+void\s+RecordWork\s*\(\s*WorkTypeId\s+workTypeId\b"));
            Assert.That(
                aiMemory.Text,
                Does.Not.Contain("public FacilityWorkType workType"));
            Assert.That(
                aiMemory.Text,
                Does.Not.Contain("public void RecordWork(\n        FacilityWorkType"));
            Assert.That(
                characterActivity.Text,
                Does.Match(
                    @"public\s+static\s+CharacterActivityEvent\s+Work\s*\(\s*WorkTypeId\s+workTypeId\b"));
            Assert.That(
                characterActivity.Text,
                Does.Not.Contain("public static CharacterActivityEvent Work(\n        FacilityWorkType"));
            Assert.That(
                combatLoadout.Text,
                Does.Not.Contain("WorkPriorities.IsEnabled(FacilityWorkType.Guard"));
            Assert.That(
                combatLoadout.Text,
                Does.Contain("WorkPriorities.IsEnabled(BuiltInWorkTypeIds.Guard"));
            Assert.That(
                staffDiscontent.Text,
                Does.Not.Contain("WorkPriorities.IsEnabled(FacilityWorkType.Guard"));
            Assert.That(
                staffDiscontent.Text,
                Does.Contain("WorkPriorities.IsEnabled(BuiltInWorkTypeIds.Guard"));
            Assert.That(
                deprivation.Text,
                Does.Not.Contain("WorkPriorities.IsEnabled(FacilityWorkType.Guard"));
            Assert.That(
                deprivation.Text,
                Does.Contain("WorkPriorities.IsEnabled(BuiltInWorkTypeIds.Guard"));
            Assert.That(
                defenseUi.Text,
                Does.Not.Contain("WorkPriorities.GetPriority(FacilityWorkType.Guard"));
            Assert.That(
                defenseUi.Text,
                Does.Contain("WorkPriorities.GetPriority(BuiltInWorkTypeIds.Guard"));
            Assert.That(
                researchUi.Text,
                Does.Not.Contain("WorkPriorities.GetPriority(FacilityWorkType.Research"));
            Assert.That(
                researchUi.Text,
                Does.Contain("WorkPriorities.GetPriority(BuiltInWorkTypeIds.Research"));
            Assert.That(
                defenseEngagement.Text,
                Does.Not.Contain("WorkPriorities.GetPriority(FacilityWorkType.Guard"));
            Assert.That(
                defenseEngagement.Text,
                Does.Contain("WorkPriorities.GetPriority(BuiltInWorkTypeIds.Guard"));
            Assert.That(
                workPriorityPanel.Text,
                Does.Not.Contain("WorkTaskCatalog.TaskTypes"));
            Assert.That(
                workPriorityPanel.Text,
                Does.Contain("WorkTaskCatalog.Definitions"));
            Assert.That(
                workPriorityPanel.Text,
                Does.Contain("WorkTypeId capturedType"));
            Assert.That(
                workPriorityPanel.Text,
                Does.Contain("SetWorkPriority(capturedType"));
            Assert.That(
                workPriorityPanelModel.Text,
                Does.Not.Contain("WorkTaskCatalog.TaskTypes"));
            Assert.That(
                workPriorityPanelModel.Text,
                Does.Contain("definition.WorkTypeId"));
            Assert.That(
                workDutyController.Text,
                Does.Not.Contain("WorkPriorities.IsEnabled(workType"));
            Assert.That(
                workDutyController.Text,
                Does.Not.Contain("IsEnabled(FacilityWorkType.Rest"));
            Assert.That(
                workDutyController.Text,
                Does.Not.Contain("AssignedWorkType != FacilityWorkType.Operate"));
            Assert.That(
                workDutyController.Text,
                Does.Not.Contain("AssignedWorkType == FacilityWorkType.Guard"));
            Assert.That(
                workDutyController.Text,
                Does.Contain("work.IsAssignedWork(BuiltInWorkTypeIds.Operate"));
            Assert.That(
                workDutyController.Text,
                Does.Contain("WorkPriorities.IsEnabled(definition.WorkTypeId"));
            Assert.That(
                workDutyController.Text,
                Does.Contain("GetWorkEnvironmentDurationMultiplier(work.AssignedWorkTypeId"));
            Assert.That(
                workDutyController.Text,
                Does.Contain("BeginRoutineWorkCooldown(work.AssignedWorkTypeId"));
            Assert.That(
                workforceReplan.Text,
                Does.Not.Contain("WorkPriorities.IsEnabled(workType"));
            Assert.That(
                workforceReplan.Text,
                Does.Not.Contain("WorkPriorities.GetPriority(workType"));
            Assert.That(
                workforceReplan.Text,
                Does.Not.Contain("WorkPriorities.GetPriority(work.AssignedWorkType"));
            Assert.That(
                workforceReplan.Text,
                Does.Contain("requestedWorkTypeId"));
            Assert.That(
                workforceReplan.Text,
                Does.Not.Contain("WorkTypeCatalog.TryGet(work.AssignedWorkType"));
            Assert.That(
                workforceReplan.Text,
                Does.Contain("assignedWorkTypeId"));
            Assert.That(
                workforceReplan.Text,
                Does.Match(
                    @"void\s+RequestOneWorkerToReplanFor\s*\(\s*WorkTypeId\s+workTypeId\b"));
            Assert.That(
                workforceReplan.Text,
                Does.Not.Contain("RequestOneWorkerToReplanFor(FacilityWorkType"));
            Assert.That(
                blueprintResearchRuntime.Text,
                Does.Not.Contain("RequestOneWorkerToReplanFor(FacilityWorkType.Research"));
            Assert.That(
                blueprintResearchRuntime.Text,
                Does.Contain("RequestOneWorkerToReplanFor(BuiltInWorkTypeIds.Research"));
            Assert.That(
                characterActor.Text,
                Does.Contain("GetWorkSpeedMultiplier(WorkTypeId workTypeId)"));
            Assert.That(
                characterActor.Text,
                Does.Contain("GetWorkPreferenceScore(WorkTypeId workTypeId)"));
            Assert.That(
                characterActor.Text,
                Does.Not.Contain("GetWorkSpeedMultiplier(FacilityWorkType"));
            Assert.That(
                characterActor.Text,
                Does.Not.Contain("GetWorkPreferenceScore(FacilityWorkType"));
            Assert.That(
                characterStats.Text,
                Does.Contain("GetWorkSpeedMultiplier(WorkTypeId workTypeId)"));
            Assert.That(
                characterStats.Text,
                Does.Contain("GetWorkPreferenceScore(WorkTypeId workTypeId)"));
            Assert.That(
                characterStats.Text,
                Does.Not.Contain("GetWorkSpeedMultiplier(FacilityWorkType"));
            Assert.That(
                characterStats.Text,
                Does.Not.Contain("GetWorkPreferenceScore(FacilityWorkType"));
            Assert.That(
                characterStats.Text,
                Does.Not.Contain("GetWorkSpeedMultiplier(definition.Type"));
            Assert.That(
                characterModelData.Text,
                Does.Not.Contain("GetWorkModifierOnly(WorkTypeId workTypeId)"));
            Assert.That(
                characterModelData.Text,
                Does.Not.Contain("GetWorkSpeedMultiplier(WorkTypeId workTypeId)"));
            Assert.That(
                characterModelData.Text,
                Does.Contain("GetWorkPreferenceScore(WorkTypeId workTypeId)"));
            Assert.That(
                characterModelData.Text,
                Does.Not.Contain("GetWorkModifierOnly(FacilityWorkType"));
            Assert.That(
                characterModelData.Text,
                Does.Not.Contain("GetWorkSpeedMultiplier(FacilityWorkType"));
            Assert.That(
                characterModelData.Text,
                Does.Not.Contain("GetWorkPreferenceScore(FacilityWorkType"));
            Assert.That(
                characterModelData.Text,
                Does.Not.Contain("GetWorkSpeedMultiplier(definition.Type"));
            Assert.That(
                buildingSo.Text,
                Does.Not.Contain("public FacilityWorkType supportedWorkTypes"));
            Assert.That(
                buildingSo.Text,
                Does.Contain("public IEnumerable<WorkTypeId> SupportedWorkTypeIds"));
            Assert.That(
                buildingSo.Text,
                Does.Contain("public void SetSupportedWorkTypeIds(IEnumerable<WorkTypeId> workTypeIds)"));
            Assert.That(
                characterSo.Text,
                Does.Not.Contain("public FacilityWorkType ownerPreferredWorkTypes"));
            Assert.That(
                characterSo.Text,
                Does.Contain("public IEnumerable<WorkTypeId> OwnerPreferredWorkTypeIds"));
            Assert.That(
                characterAuthoredModel.Text,
                Does.Not.Contain("public FacilityWorkType preferredWorkTypes"));
            Assert.That(
                characterAuthoredModel.Text,
                Does.Not.Contain("public FacilityWorkType dislikedWorkTypes"));
            Assert.That(
                characterAuthoredModel.Text,
                Does.Match(
                    @"public\s+IEnumerable<WorkTypeId>\s+PreferredWorkTypeIds\b"));
            Assert.That(
                characterAuthoredModel.Text,
                Does.Match(
                    @"public\s+IEnumerable<WorkTypeId>\s+DislikedWorkTypeIds\b"));
            Assert.That(
                equipmentCrafting.Text,
                Does.Match(
                    @"workTypeId\s*!=\s*BuiltInWorkTypeIds\.Craft\b"));
            Assert.That(
                equipmentCrafting.Text,
                Does.Not.Match(
                    @"workTypeId\s*!=\s*FacilityWorkType\.Craft\b"));
            Assert.That(
                abilityRescue.Text,
                Does.Contain("GetWorkSpeedMultiplier(BuiltInWorkTypeIds.Treat"));
            Assert.That(
                abilityRescue.Text,
                Does.Not.Contain("GetWorkSpeedMultiplier(FacilityWorkType.Treat"));
            Assert.That(
                blueprintResearchContracts.Text,
                Does.Contain("GetWorkSpeedMultiplier(BuiltInWorkTypeIds.Research"));
            Assert.That(
                blueprintResearchContracts.Text,
                Does.Not.Contain("GetWorkSpeedMultiplier(FacilityWorkType.Research"));
            Assert.That(
                workTargetCandidate.Text,
                Does.Contain("public WorkTypeId WorkTypeId"));
            Assert.That(
                workTargetCandidate.Text,
                Does.Not.Contain("public FacilityWorkType WorkType"));
            Assert.That(
                workTargetCandidate.Text,
                Does.Contain("public string DisplayName"));
            Assert.That(
                workTargetCandidate.Text,
                Does.Not.Contain("ResolveDefinition"));
            Assert.That(
                workTargetCandidate.Text,
                Does.Not.Contain("FacilityWorkType workType,\n        WorkPriorityLevel priority"));
            Assert.That(
                workTargetSelector.Text,
                Does.Contain("FacilityWorkTypeMap"));
            Assert.That(
                workTargetSelector.Text,
                Does.Contain("actor.GetWorkPreferenceScore(workTypeId)"));
            Assert.That(
                workTargetSelector.Text,
                Does.Contain("actor.GetWorkSpeedMultiplier(workTypeId, target)"));
            Assert.That(
                workTargetSelector.Text,
                Does.Contain("actor.AiMemory.GetRepeatedWorkFatigue(workTypeId)"));
            Assert.That(
                workTargetSelector.Text,
                Does.Contain("actor.AiMemory.GetRecentTargetWorkFatigue(building, workTypeId)"));
            Assert.That(
                workTargetSelector.Text,
                Does.Not.Contain("actor.GetWorkPreferenceScore(workType)"));
            Assert.That(
                workTargetSelector.Text,
                Does.Not.Contain("actor.GetWorkSpeedMultiplier(workType)"));
            Assert.That(
                workTargetSelector.Text,
                Does.Not.Contain("GetRepeatedWorkFatigue(workType)"));
            Assert.That(
                workTargetSelector.Text,
                Does.Not.Contain("GetRecentTargetWorkFatigue(building, workType)"));
            Assert.That(
                workTargetEvaluator.Text,
                Does.Not.Contain("priorities.GetPriority(workType)"));
            Assert.That(
                workTargetEvaluator.Text,
                Does.Match(
                    @"priorities\.GetPriority\s*\(\s*workTypeId\s*\)"));
            Assert.That(
                workTargetEvaluator.Text,
                Does.Match(
                    @"workPolicyRegistry\.IsAvailable\s*\(\s*workTypeId\b"));
            Assert.That(
                workTargetEvaluator.Text,
                Does.Not.Match(
                    @"workPolicyRegistry\.IsAvailable\s*\(\s*workType\b"));
            Assert.That(
                workTargetSelector.Text,
                Does.Contain("workPolicyRegistry?.GetAdditionalUrgency(workTypeId"));
            Assert.That(
                facility.Text,
                Does.Not.Contain("WorkTaskCatalog.GetSingleTypes"));
            Assert.That(
                facility.Text,
                Does.Contain("FacilityWorkTypeMap.Enumerate("));
            Assert.That(
                shop.Text,
                Does.Not.Contain("WorkTaskCatalog.GetSingleTypes"));
            Assert.That(
                shop.Text,
                Does.Contain("FacilityWorkTypeMap.Enumerate("));
            Assert.That(
                priorities.Text,
                Does.Not.Contain("WorkTaskCatalog.GetSingleTypes("));
            Assert.That(
                priorities.Text,
                Does.Not.Contain("GetSingleTypes(FacilityWorkType"));
            Assert.That(
                priorities.Text,
                Does.Not.Contain("TaskTypes =>"));
            Assert.That(
                priorities.Text,
                Does.Contain("FacilityWorkTypeMap.Enumerate("));
            Assert.That(
                aiMemory.Text,
                Does.Contain("GetRepeatedWorkFatigue(WorkTypeId workTypeId)"));
            Assert.That(
                aiMemory.Text,
                Does.Contain("GetRecentTargetWorkFatigue(BuildableObject building, WorkTypeId workTypeId)"));
            Assert.That(
                aiMemory.Text,
                Does.Not.Contain("GetRepeatedWorkFatigue(FacilityWorkType"));
            Assert.That(
                aiMemory.Text,
                Does.Not.Contain("GetRecentTargetWorkFatigue(BuildableObject building, FacilityWorkType"));
            Assert.That(
                ProductSources().Where(source =>
                    source.Text.Contains("WorkTaskCatalog.GetSingleTypes(")
                    || source.Text.Contains("WorkTaskCatalog.TaskTypes")),
                Is.Empty);
            Assert.That(
                ProductSources().Where(source =>
                    source.Text.Contains("new WorkTypeId(definition.Id)")),
                Is.Empty);
        }

        [Test]
        public void BuildingAssemblyOwnsStableBuildingPrimitives()
        {
            SourceFile primitives = SourceBySuffix(
                "Buildings/Core/BuildingPrimitives.cs");
            SourceFile buildingData = SourceBySuffix(
                "Buildings/SO/BuildingSO.cs");
            SourceFile saleItem = SourceBySuffix(
                "Models/Economy/Content/SaleItem.cs");

            Assert.That(
                File.Exists(Path.Combine(
                    Application.dataPath,
                    "Scripts/Models/Buildings/Core/DungeonStory.Buildings.asmdef")),
                Is.True);
            Assert.That(primitives.Text, Does.Contain("public enum BuildingCategory"));
            Assert.That(primitives.Text, Does.Contain("public enum FacilityRole"));
            Assert.That(primitives.Text, Does.Contain("public enum FacilityWorkType"));
            Assert.That(
                File.Exists(Path.Combine(
                    Application.dataPath,
                    "Scripts/Models/Buildings/Core/BuildingAssemblyInfo.cs")),
                Is.False);
            Assert.That(
                ProductSources().Where(source =>
                    source.Text.Contains("InternalsVisibleTo(")),
                Is.Empty);
            Assert.That(primitives.Text, Does.Contain("public enum StockCategory"));
            Assert.That(
                buildingData.Text,
                Does.Not.Contain("public enum BuildingCategory"));
            Assert.That(
                buildingData.Text,
                Does.Not.Contain("public enum FacilityWorkType"));
            Assert.That(
                saleItem.Text,
                Does.Not.Contain("public enum StockCategory"));
        }

        [Test]
        public void BatchCProductionAndAutomationAssembliesOwnPureState()
        {
            const string productionAssembly = "DungeonStory.Production";
            const string automationAssembly = "DungeonStory.Automation";

            Assert.That(
                typeof(ProductionBillId).Assembly.GetName().Name,
                Is.EqualTo(productionAssembly));
            Assert.That(
                typeof(ProductionConsumerRoutePolicy).Assembly.GetName().Name,
                Is.EqualTo(productionAssembly));
            Assert.That(
                typeof(DungeonProductionBillSaveData).Assembly.GetName().Name,
                Is.EqualTo(productionAssembly));
            Assert.That(
                typeof(AutomationMode).Assembly.GetName().Name,
                Is.EqualTo(automationAssembly));
            Assert.That(
                typeof(DungeonAutomationSaveData).Assembly.GetName().Name,
                Is.EqualTo(automationAssembly));
            Assert.That(
                typeof(AutomationFacilitySnapshot).Assembly.GetName().Name,
                Is.EqualTo(automationAssembly));

            string productionAsmdef = File.ReadAllText(Path.Combine(
                Application.dataPath,
                "Scripts/Models/Production/Core/DungeonStory.Production.asmdef"));
            string automationAsmdef = File.ReadAllText(Path.Combine(
                Application.dataPath,
                "Scripts/Models/Automation/Core/DungeonStory.Automation.asmdef"));
            Assert.That(productionAsmdef, Does.Contain("DungeonStory.Foundation"));
            Assert.That(productionAsmdef, Does.Contain("DungeonStory.Work"));
            Assert.That(productionAsmdef, Does.Not.Contain("Assembly-CSharp"));
            Assert.That(automationAsmdef, Does.Contain("DungeonStory.Foundation"));
            Assert.That(automationAsmdef, Does.Not.Contain("Assembly-CSharp"));

            SourceFile productionModels = SourceBySuffix(
                "Production/Core/ProductionBillModels.cs");
            SourceFile productionRuntimePorts = SourceBySuffix(
                "Economy/Core/ProductionRuntimeContracts.cs");
            SourceFile automationModels = SourceBySuffix(
                "Automation/Core/AutomationCoreModels.cs");
            Assert.That(productionModels.Text, Does.Not.Contain("BuildableObject"));
            Assert.That(productionModels.Text, Does.Not.Contain("CharacterActor"));
            Assert.That(productionRuntimePorts.Text, Does.Contain("BuildableObject"));
            Assert.That(automationModels.Text, Does.Not.Contain("BuildableObject"));

            string movedMeta = File.ReadAllText(Path.Combine(
                Application.dataPath,
                "Scripts/Models/Production/Core/ProductionBillModels.cs.meta"));
            Assert.That(
                movedMeta,
                Does.Contain("guid: e592c14eebd69644b96bd9afe78afbb8"));
        }

        [Test]
        public void RoomAssemblyOwnsStableRoleCatalog()
        {
            SourceFile roleCatalog = SourceBySuffix(
                "Rooms/Core/RoomRole.cs");
            SourceFile roomEnvironment = SourceBySuffix(
                "Infrastructure/Rooms/RoomEnvironmentAdapter.cs");

            Assert.That(
                File.Exists(Path.Combine(
                    Application.dataPath,
                    "Scripts/Models/Rooms/Core/DungeonStory.Rooms.asmdef")),
                Is.True);
            Assert.That(
                roleCatalog.Text,
                Does.Contain("public sealed class FacilityRoleDefinition"));
            Assert.That(
                roleCatalog.Text,
                Does.Contain("public static class FacilityRoleCatalog"));
            Assert.That(
                roomEnvironment.Text,
                Does.Not.Contain("public static class FacilityRoleCatalog"));
        }

        [Test]
        public void SurvivalAssemblyOwnsStableSurvivalModels()
        {
            SourceFile primitives = SourceBySuffix(
                "Survival/Core/SurvivalPrimitives.cs");
            SourceFile balance = SourceBySuffix(
                "Survival/Core/SurvivalBalanceSettingsSO.cs");
            SourceFile pressureRules = SourceBySuffix(
                "Survival/Core/DungeonSurvivalPressureRules.cs");
            SourceFile wildlifeModels = SourceBySuffix(
                "Wildlife/WildlifeModels.cs");
            SourceFile darkSurvivalModels = SourceBySuffix(
                "Survival/DarkSurvivalModels.cs");

            Assert.That(
                File.Exists(Path.Combine(
                    Application.dataPath,
                    "Scripts/Models/Survival/Core/DungeonStory.Survival.asmdef")),
                Is.True);
            Assert.That(
                primitives.Text,
                Does.Contain("class DungeonSurvivalSaveData"));
            Assert.That(
                primitives.Text,
                Does.Contain("class DungeonDarkSurvivalSaveData"));
            Assert.That(
                primitives.Text,
                Does.Contain("public interface IWorldFilthQuery"));
            Assert.That(
                primitives.Text,
                Does.Contain("public interface IWorldWaterQuery"));
            Assert.That(
                primitives.Text,
                Does.Contain("sourceAssembly: \"Assembly-CSharp\""));
            Assert.That(
                balance.Text,
                Does.Contain("public sealed class SurvivalBalanceSettingsSO"));
            Assert.That(
                balance.Text,
                Does.Contain("sourceAssembly: \"Assembly-CSharp\""));
            Assert.That(
                pressureRules.Text,
                Does.Contain("public static class DungeonSurvivalPressureRules"));
            Assert.That(balance.Text, Does.Not.Contain("CharacterActor"));
            Assert.That(pressureRules.Text, Does.Not.Contain("GameManager"));
            Assert.That(
                wildlifeModels.Text,
                Does.Not.Contain("class DungeonSurvivalSaveData"));
            Assert.That(
                darkSurvivalModels.Text,
                Does.Not.Contain("class CharacterDeprivationState"));
            Assert.That(
                darkSurvivalModels.Text,
                Does.Not.Contain("public interface IWorldWaterQuery"));
        }

        [Test]
        public void MedicalAssemblyOwnsAuthoredDefinitionsWithoutUnityActorPorts()
        {
            SourceFile anatomy = SourceBySuffix(
                "Medical/Core/AnatomyModels.cs");
            SourceFile profile = SourceBySuffix(
                "Medical/Core/AnatomyProfileSO.cs");
            SourceFile lexicon = SourceBySuffix(
                "Medical/Core/AnatomyConditionLexiconSO.cs");
            SourceFile runtimeContracts = SourceBySuffix(
                "Medical/AnatomyRuntimeContracts.cs");
            SourceFile surgeryModels = SourceBySuffix(
                "Medical/Core/SurgeryModels.cs");
            SourceFile surgeryProcedure = SourceBySuffix(
                "Medical/Core/SurgicalProcedureSO.cs");
            SourceFile surgeryContracts = SourceBySuffix(
                "Medical/Core/SurgeryContracts.cs");
            SourceFile surgeryRuntimeContracts = SourceBySuffix(
                "Medical/SurgeryRuntimeContracts.cs");

            Assert.That(
                File.Exists(Path.Combine(
                    Application.dataPath,
                    "Scripts/Models/Medical/Core/DungeonStory.Medical.asmdef")),
                Is.True);
            Assert.That(
                anatomy.Text,
                Does.Contain("public sealed class AnatomyProfileDefinition"));
            Assert.That(
                anatomy.Text,
                Does.Contain("public interface IAnatomyProfileCatalog"));
            Assert.That(
                profile.Text,
                Does.Contain("public sealed class AnatomyProfileSO"));
            Assert.That(
                lexicon.Text,
                Does.Contain("public sealed class AnatomyConditionLexiconSO"));
            Assert.That(anatomy.Text, Does.Not.Contain("CharacterActor"));
            Assert.That(anatomy.Text, Does.Not.Contain("WildlifeActor"));
            Assert.That(
                anatomy.Text,
                Does.Not.Contain("public interface IAnatomyHealthRuntime"));
            Assert.That(
                runtimeContracts.Text,
                Does.Contain("public interface IAnatomyHealthRuntime"));
            Assert.That(
                runtimeContracts.Text,
                Does.Contain("public interface IWildlifeAnatomyHealthRuntime"));
            Assert.That(
                surgeryModels.Text,
                Does.Contain("public sealed class DungeonSurgerySaveData"));
            Assert.That(
                surgeryProcedure.Text,
                Does.Contain("public sealed class SurgicalProcedureSO"));
            Assert.That(
                surgeryProcedure.Text,
                Does.Contain("sourceAssembly: \"Assembly-CSharp\""));
            Assert.That(
                surgeryContracts.Text,
                Does.Contain("public interface ISurgicalProcedureCatalog"));
            Assert.That(surgeryModels.Text, Does.Not.Contain("CharacterActor"));
            Assert.That(surgeryModels.Text, Does.Not.Contain("WildlifeActor"));
            Assert.That(surgeryModels.Text, Does.Not.Contain("BuildableObject"));
            Assert.That(
                surgeryRuntimeContracts.Text,
                Does.Contain("public interface ISurgeryQuery"));
            Assert.That(
                surgeryRuntimeContracts.Text,
                Does.Contain("public interface ISurgeryWorkCommand"));
            Assert.That(
                surgeryRuntimeContracts.Text,
                Does.Contain("public interface ISurgeryPersistence"));
            Assert.That(
                surgeryRuntimeContracts.Text,
                Does.Not.Contain("public interface ISurgeryRuntime"));
            Assert.That(
                surgeryRuntimeContracts.Text,
                Does.Contain("CharacterActor"));
            Assert.That(
                surgeryRuntimeContracts.Text,
                Does.Contain("WildlifeActor"));
            Assert.That(
                surgeryRuntimeContracts.Text,
                Does.Contain("BuildableObject"));
        }

        [Test]
        public void CombatAssemblyOwnsResolutionPrimitives()
        {
            SourceFile models = SourceBySuffix(
                "Combat/Core/CombatModels.cs");
            SourceFile weapons = SourceBySuffix(
                "Combat/Core/CombatWeaponPrimitives.cs");
            SourceFile definitions = SourceBySuffix(
                "Models/Economy/Content/CombatEquipmentDefinitions.cs");

            Assert.That(
                File.Exists(Path.Combine(
                    Application.dataPath,
                    "Scripts/Models/Combat/Core/DungeonStory.Combat.asmdef")),
                Is.True);
            Assert.That(
                models.Text,
                Does.Contain("public readonly struct CombatAttackRequest"));
            Assert.That(
                weapons.Text,
                Does.Contain("public abstract class CombatAttackVerb"));
            Assert.That(
                weapons.Text,
                Does.Contain("public sealed class CombatWeaponSnapshot"));
            Assert.That(
                weapons.Text,
                Does.Contain("sourceAssembly: \"Assembly-CSharp\""));
            Assert.That(
                definitions.Text,
                Does.Not.Contain("public abstract class CombatAttackVerb"));
            Assert.That(
                definitions.Text,
                Does.Not.Contain("public sealed class CombatWeaponSnapshot"));
        }

        [Test]
        public void CharacterMedicalRuntimeUsesNarrowAuthorityFacets()
        {
            SourceFile models = SourceBySuffix(
                "Combat/CharacterMedicalModels.cs");
            SourceFile runtime = SourceBySuffix(
                "Combat/CharacterMedicalRuntime.cs");
            SourceFile saveSections = SourceBySuffix(
                "Combat/CombatSaveSections.cs");
            SourceFile registration = SourceBySuffix(
                "Registration/DungeonCombatRegistration.cs");

            Assert.That(
                models.Text,
                Does.Contain("public interface ICharacterMedicalQuery"));
            Assert.That(
                models.Text,
                Does.Contain("public interface ICharacterMedicalCommand"));
            Assert.That(
                models.Text,
                Does.Contain("public interface ICharacterMedicalPersistence"));
            Assert.That(
                models.Text,
                Does.Not.Contain(
                    "public interface ICharacterMedical" + "Runtime"));
            Assert.That(
                runtime.Text,
                Does.Contain("ICharacterMedicalQuery,"));
            Assert.That(
                runtime.Text,
                Does.Contain("ICharacterMedicalCommand,"));
            Assert.That(
                runtime.Text,
                Does.Contain("ICharacterMedicalPersistence,"));
            Assert.That(
                saveSections.Text,
                Does.Contain("private readonly ICharacterMedicalPersistence persistence;"));
            Assert.That(
                registration.Text,
                Does.Contain(".As<ICharacterMedicalQuery>()"));
            Assert.That(
                registration.Text,
                Does.Contain(".As<ICharacterMedicalCommand>()"));
            Assert.That(
                registration.Text,
                Does.Contain(".As<ICharacterMedicalPersistence>()"));
        }

        [Test]
        public void InvasionAssemblyOwnsPolicyAndThreatPrimitives()
        {
            SourceFile primitives = SourceBySuffix(
                "Invasion/Core/InvasionPrimitives.cs");
            SourceFile engagement = SourceBySuffix(
                "Models/Invasion/Core/DefenseEngagementModels.cs");
            SourceFile threat = SourceBySuffix(
                "Models/Invasion/Core/InvasionThreatSystem.cs");

            Assert.That(
                File.Exists(Path.Combine(
                    Application.dataPath,
                    "Scripts/Models/Invasion/Core/DungeonStory.Invasion.asmdef")),
                Is.True);
            Assert.That(
                primitives.Text,
                Does.Contain("class DefenseResponsePolicyData"));
            Assert.That(
                primitives.Text,
                Does.Contain("class DefenseEngagementSaveData"));
            Assert.That(
                primitives.Text,
                Does.Contain("class InvasionThreatSettings"));
            Assert.That(
                primitives.Text,
                Does.Contain("sourceAssembly: \"Assembly-CSharp\""));
            Assert.That(
                engagement.Text,
                Does.Not.Contain("class DefenseResponsePolicyData"));
            Assert.That(
                threat.Text,
                Does.Not.Contain("class InvasionThreatSettings"));
        }

        [Test]
        public void OffenseAssemblyOwnsRouteAndPreparationPrimitives()
        {
            SourceFile primitives = SourceBySuffix(
                "Offense/Core/OffensePrimitives.cs");
            SourceFile journey = SourceBySuffix(
                "Offense/OffenseJourneyModel.cs");
            SourceFile worldMap = SourceBySuffix(
                "Offense/OffenseWorldMapModel.cs");

            Assert.That(
                File.Exists(Path.Combine(
                    Application.dataPath,
                    "Scripts/Models/Offense/Core/DungeonStory.Offense.asmdef")),
                Is.True);
            Assert.That(
                primitives.Text,
                Does.Contain("public sealed class OffenseRouteGraph"));
            Assert.That(
                primitives.Text,
                Does.Contain("public sealed class OffenseSupplyLoadout"));
            Assert.That(
                primitives.Text,
                Does.Contain("public sealed class OffenseExpeditionPreparation"));
            Assert.That(
                primitives.Text,
                Does.Contain("public static class OffenseRewardTypeIds"));
            Assert.That(
                primitives.Text,
                Does.Contain("sourceAssembly: \"Assembly-CSharp\""));
            Assert.That(
                journey.Text,
                Does.Not.Contain("public sealed class OffenseRouteGraph"));
            Assert.That(
                worldMap.Text,
                Does.Not.Contain("public static class OffenseRewardTypeIds"));
        }

        [Test]
        public void AiAssemblyOwnsStableDecisionIdentifiers()
        {
            SourceFile primitives = SourceBySuffix(
                "AI/Core/AiPrimitives.cs");
            SourceFile blackboard = SourceBySuffix(
                "Character/AI/CharacterBlackboard.cs");
            SourceFile utility = SourceBySuffix(
                "Character/AI/CharacterAiUtilityModels.cs");
            SourceFile failure = SourceBySuffix(
                "Character/AI/AIActionFailure.cs");

            Assert.That(
                File.Exists(Path.Combine(
                    Application.dataPath,
                    "Scripts/Models/AI/Core/DungeonStory.AI.asmdef")),
                Is.True);
            Assert.That(
                primitives.Text,
                Does.Contain("public enum CharacterAiBranch"));
            Assert.That(
                primitives.Text,
                Does.Contain("public sealed class CharacterMacroGoal"));
            Assert.That(
                primitives.Text,
                Does.Contain("public enum CharacterAiUtilityFactorKind"));
            Assert.That(
                primitives.Text,
                Does.Contain("public enum AIActionFailureKind"));
            Assert.That(
                primitives.Text,
                Does.Contain("sourceAssembly: \"Assembly-CSharp\""));
            Assert.That(
                blackboard.Text,
                Does.Not.Contain("public enum CharacterAiBranch"));
            Assert.That(
                utility.Text,
                Does.Not.Contain("public enum CharacterAiIntentionType"));
            Assert.That(
                failure.Text,
                Does.Not.Contain("public enum AIActionFailureKind"));
        }

        [Test]
        public void PresentationAssemblyOwnsTabAndFeaturePresenterContracts()
        {
            SourceFile primitives = SourceBySuffix(
                "UI/Core/PresentationPrimitives.cs");
            SourceFile identities = SourceBySuffix(
                "UI/Core/UITabIdentity.cs");

            Assert.That(
                File.Exists(Path.Combine(
                    Application.dataPath,
                    "Scripts/Views/UI/Core/DungeonStory.Presentation.asmdef")),
                Is.True);
            Assert.That(
                primitives.Text,
                Does.Contain("public enum TabId"));
            Assert.That(
                primitives.Text,
                Does.Contain("public static class UITabCatalog"));
            Assert.That(
                primitives.Text,
                Does.Contain("public interface IFeatureSurfaceTabPresenter"));
            Assert.That(
                primitives.Text,
                Does.Contain("class FeatureSurfaceTabPresenterRegistry"));
            Assert.That(
                primitives.Text,
                Does.Contain("sourceAssembly: \"Assembly-CSharp\""));
            Assert.That(
                identities.Text,
                Does.Not.Contain("public enum TabId"));
            Assert.That(
                primitives.Text,
                Does.Contain("\"건축\""));
            Assert.That(
                primitives.Text,
                Does.Contain("\"직원 관리\""));
        }

        [Test]
        public void InfrastructureAssemblyOwnsSaveContractsAndEnvelope()
        {
            SourceFile primitives = SourceBySuffix(
                "Infrastructure/Core/InfrastructureSavePrimitives.cs");
            SourceFile slotContracts = SourceBySuffix(
                "Models/CoreSession/DungeonSaveSlotContracts.cs");
            SourceFile implementation = SourceBySuffix(
                "Infrastructure/DungeonGameSaveService.cs");

            Assert.That(
                File.Exists(Path.Combine(
                    Application.dataPath,
                    "Scripts/Services/Infrastructure/Core/DungeonStory.Infrastructure.asmdef")),
                Is.True);
            Assert.That(
                primitives.Text,
                Does.Contain("public interface IDungeonGameSaveService"));
            Assert.That(
                primitives.Text,
                Does.Contain("public sealed class DungeonGameSaveData"));
            Assert.That(
                slotContracts.Text,
                Does.Contain("public sealed class DungeonSaveSlotInfo"));
            Assert.That(
                primitives.Text,
                Does.Contain("sourceAssembly: \"Assembly-CSharp\""));
            Assert.That(
                implementation.Text,
                Does.Not.Contain("public sealed class DungeonGameSaveData"));
            Assert.That(
                implementation.Text,
                Does.Not.Contain("internal set;"));
        }

        [Test]
        public void WildlifeAssemblyOwnsStateAndSavePrimitives()
        {
            SourceFile primitives = SourceBySuffix(
                "Wildlife/Core/WildlifePrimitives.cs");
            SourceFile models = SourceBySuffix(
                "Wildlife/WildlifeModels.cs");

            Assert.That(
                File.Exists(Path.Combine(
                    Application.dataPath,
                    "Scripts/Models/Wildlife/Core/DungeonStory.Wildlife.asmdef")),
                Is.True);
            Assert.That(
                primitives.Text,
                Does.Contain("public enum WildlifeState"));
            Assert.That(
                primitives.Text,
                Does.Contain("public enum WildlifeIntent"));
            Assert.That(
                primitives.Text,
                Does.Contain("public sealed class DungeonWildlifeSaveData"));
            Assert.That(
                primitives.Text,
                Does.Contain("public readonly struct WildlifeEcosystemOverview"));
            Assert.That(
                primitives.Text,
                Does.Contain("sourceAssembly: \"Assembly-CSharp\""));
            Assert.That(
                models.Text,
                Does.Not.Contain("public enum WildlifeState"));
            Assert.That(
                models.Text,
                Does.Not.Contain("public sealed class DungeonWildlifeSaveData"));
        }

        [Test]
        public void ItemAssemblyOwnsStableItemPrimitives()
        {
            SourceFile primitives = SourceBySuffix(
                "Items/Core/ItemPrimitives.cs");
            SourceFile runtimeModels = SourceBySuffix(
                "Items/WorldItemModels.cs");
            SourceFile carryInventory = SourceBySuffix(
                "Items/CharacterCarryInventory.cs");
            SourceFile haulingSettings = SourceBySuffix(
                "Items/ItemHaulingSettingsSO.cs");

            Assert.That(
                File.Exists(Path.Combine(
                    Application.dataPath,
                    "Scripts/Models/Items/Core/DungeonStory.Items.asmdef")),
                Is.True);
            Assert.That(
                primitives.Text,
                Does.Contain("public enum WorldItemStackState"));
            Assert.That(
                primitives.Text,
                Does.Contain("class DungeonPhysicalItemSaveData"));
            Assert.That(
                primitives.Text,
                Does.Contain("class CharacterCarryInventorySaveData"));
            Assert.That(
                primitives.Text,
                Does.Contain("sourceAssembly: \"Assembly-CSharp\""));
            Assert.That(
                runtimeModels.Text,
                Does.Not.Contain("public enum WorldItemStackState"));
            Assert.That(
                carryInventory.Text,
                Does.Not.Contain("class CharacterCarriedItemSaveData"));
            Assert.That(
                haulingSettings.Text,
                Does.Not.Contain("class ItemHaulingSettingsSnapshot"));
        }

        [Test]
        public void WorldViewTogglesUseScopedCanvasAndUiClock()
        {
            SourceFile itemToggle = SourceBySuffix(
                "Items/ItemStackViewToggleRuntime.cs");
            SourceFile wildlifeToggle = SourceBySuffix(
                "UI/WildlifeEcosystemViewToggleRuntime.cs");
            SourceFile roomInspection = SourceBySuffix(
                "Rooms/RoomInspectionRuntime.cs");

            Assert.That(itemToggle.Text, Does.Contain("IDungeonUiCanvasProvider"));
            Assert.That(
                itemToggle.Text,
                Does.Not.Contain("IDungeonSceneComponentQuery"));
            Assert.That(
                wildlifeToggle.Text,
                Does.Contain("IDungeonUiCanvasProvider"));
            Assert.That(
                wildlifeToggle.Text,
                Does.Not.Contain("IDungeonSceneComponentQuery"));
            Assert.That(
                roomInspection.Text,
                Does.Contain("IDungeonUiCanvasProvider"));
            Assert.That(roomInspection.Text, Does.Contain("IUiClock"));
            Assert.That(
                roomInspection.Text,
                Does.Not.Contain("IDungeonSceneComponentQuery"));
            Assert.That(roomInspection.Text, Does.Not.Match(@"\bTime\."));
        }

        [Test]
        public void CombatPresentationUsesRegisteredCommandPortAndUiClock()
        {
            SourceFile commandModels = SourceBySuffix(
                "Combat/CharacterCombatCommandRuntimeContracts.cs");
            SourceFile ownerCommands = SourceBySuffix(
                "Character/Input/OwnerCommandController.cs");
            SourceFile commandBar = SourceBySuffix(
                "Combat/CombatCommandBarUiController.cs");
            SourceFile overlay = SourceBySuffix(
                "Combat/CombatTacticalOverlayPresenter.cs");
            SourceFile registration = SourceBySuffix(
                "Infrastructure/Registration/DungeonCombatRegistration.cs");

            Assert.That(
                commandModels.Text,
                Does.Contain("interface IPlayerCombatCommandSource"));
            Assert.That(
                ownerCommands.Text,
                Does.Contain("IPlayerCombatCommandSource"));
            Assert.That(
                commandBar.Text,
                Does.Contain("IPlayerCombatCommandSource commands"));
            Assert.That(commandBar.Text, Does.Contain("IUiClock uiClock"));
            Assert.That(
                commandBar.Text,
                Does.Not.Contain("IDungeonSceneComponentQuery"));
            Assert.That(commandBar.Text, Does.Not.Match(@"\bTime\."));
            Assert.That(
                overlay.Text,
                Does.Contain("IPlayerCombatCommandSource ownerCommands"));
            Assert.That(
                overlay.Text,
                Does.Not.Contain("IDungeonSceneComponentQuery"));
            Assert.That(
                registration.Text,
                Does.Contain("As<IPlayerCombatCommandSource>()"));
        }

        [Test]
        public void MetaProgressionUsesInjectedGameClock()
        {
            SourceFile resultBuilder = SourceBySuffix(
                "Models/Meta/Core/MetaProgressionRunResultServices.cs");
            SourceFile progressTracker = SourceBySuffix(
                "Models/Meta/Core/MetaRunProgressTracker.cs");
            SourceFile runtime = SourceBySuffix(
                "Infrastructure/Core/MetaProgressionRuntime.cs");

            Assert.That(resultBuilder.Text, Does.Not.Match(@"\bTime\."));
            Assert.That(resultBuilder.Text, Does.Not.Contain("Time.time"));
            Assert.That(progressTracker.Text, Does.Contain("IGameClock"));
            Assert.That(progressTracker.Text, Does.Not.Contain("Time.time"));
            Assert.That(runtime.Text, Does.Contain("IGameClock"));
            Assert.That(runtime.Text, Does.Not.Contain("Time.time"));
        }

        [Test]
        public void RunResultPresentationUsesRegistryAndTimeScalePort()
        {
            SourceFile resultService = SourceBySuffix(
                "Views/UI/Core/RunResultPresentationServices.cs");
            SourceFile panelFactory = SourceBySuffix(
                "Views/UI/Core/RunResultPanelFactory.cs");
            SourceFile panel = SourceBySuffix(
                "Views/UI/Core/RunResultPanel.cs");
            SourceFile registration = SourceBySuffix(
                "Infrastructure/Registration/DungeonPresentationRegistration.cs");

            Assert.That(
                resultService.Text,
                Does.Contain("interface IRunResultPanelRegistry"));
            Assert.That(
                resultService.Text,
                Does.Not.Contain("IDungeonSceneComponentQuery"));
            Assert.That(
                panelFactory.Text,
                Does.Contain("panelRegistry.Register(panel)"));
            Assert.That(
                panel.Text,
                Does.Contain("IGameTimeScaleController"));
            Assert.That(panel.Text, Does.Not.Match(@"\bTime\."));
            Assert.That(
                registration.Text,
                Does.Contain("new RunResultPanelRegistry(initialRunResultPanel)"));
        }

        [Test]
        public void FloatingIconFeedbackUsesCapturedGameManager()
        {
            SourceFile feedback = SourceBySuffix(
                "Character/UI/CharacterFloatingIcon.cs");

            Assert.That(
                feedback.Text,
                Does.Contain("DungeonSceneRuntimeReferences sceneReferences"));
            Assert.That(
                feedback.Text,
                Does.Not.Contain("IDungeonSceneComponentQuery"));
            Assert.That(
                feedback.Text,
                Does.Contain("sceneReferences.GameManager"));
        }

        [Test]
        public void SettingsUiUsesCapturedRuntimeAndGameSpeedPort()
        {
            SourceFile settings = SourceBySuffix(
                "UI/DungeonSettingsUi.cs");

            Assert.That(
                settings.Text,
                Does.Contain("DungeonUserSettingsRuntimeTargets runtimeTargets"));
            Assert.That(
                settings.Text,
                Does.Contain("IGameSpeedController gameSpeedController"));
            Assert.That(
                settings.Text,
                Does.Not.Contain("IDungeonSceneComponentQuery"));
            Assert.That(settings.Text, Does.Not.Match(@"\bTime\."));
        }

        [Test]
        public void UserSettingsAndDedicatedCanvasesUseCapturedTargets()
        {
            SourceFile settings = SourceBySuffix(
                "Infrastructure/DungeonUserSettings.cs");
            SourceFile titleUi = SourceBySuffix(
                "UI/DungeonTitleUi.cs");
            SourceFile titleScope = SourceBySuffix(
                "Infrastructure/DungeonTitleLifetimeScope.cs");
            SourceFile preparationScope = SourceBySuffix(
                "Infrastructure/DungeonPreparationLifetimeScope.cs");

            Assert.That(
                settings.Text,
                Does.Contain("DungeonUserSettingsRuntimeTargets runtimeTargets"));
            Assert.That(
                settings.Text,
                Does.Not.Contain("IDungeonSceneComponentQuery"));
            Assert.That(
                titleUi.Text,
                Does.Contain("IDungeonTitleUiEnvironment environment"));
            Assert.That(
                titleUi.Text,
                Does.Not.Contain("IDungeonSceneComponentQuery"));
            Assert.That(
                titleScope.Text,
                Does.Not.Contain("As<IDungeonSceneComponentQuery>()"));
            Assert.That(
                preparationScope.Text,
                Does.Contain("SceneUiBootstrapReferences"));
            Assert.That(
                preparationScope.Text,
                Does.Not.Contain("As<IDungeonSceneComponentQuery>()"));
        }

        [Test]
        public void CharacterPanelsUseScopedTimeAndDiagnosticPorts()
        {
            SourceFile workPriority = SourceBySuffix(
                "Character/UI/StaffWorkPriorityPanel.cs");
            SourceFile characterSummary = SourceBySuffix(
                "UI/CharacterSummaryInfo.cs");
            SourceFile characterSummaryAi = SourceBySuffix(
                "UI/CharacterSummaryAiPresenter.cs");
            SourceFile ownerSelection = SourceBySuffix(
                "Character/UI/OwnerSelectionPanel.cs");
            SourceFile scheduling = SourceBySuffix(
                "Infrastructure/CharacterAiSchedulingService.cs");
            SourceFile registration = SourceBySuffix(
                "Infrastructure/Registration/DungeonAiRegistration.cs");

            Assert.That(workPriority.Text, Does.Contain("IUiClock uiClock"));
            Assert.That(
                workPriority.Text,
                Does.Not.Contain("IDungeonSceneComponentQuery"));
            Assert.That(workPriority.Text, Does.Not.Match(@"\bTime\."));
            Assert.That(
                characterSummaryAi.Text,
                Does.Contain("ICharacterAiDiagnosticsQuery diagnostics"));
            Assert.That(characterSummary.Text, Does.Contain("IUiClock uiClock"));
            Assert.That(
                characterSummary.Text,
                Does.Not.Contain("IDungeonSceneComponentQuery"));
            Assert.That(characterSummary.Text, Does.Not.Match(@"\bTime\."));
            Assert.That(
                ownerSelection.Text,
                Does.Contain("DungeonSceneRuntimeReferences runtimeReferences"));
            Assert.That(
                ownerSelection.Text,
                Does.Contain("IGameSpeedController gameSpeedController"));
            Assert.That(
                ownerSelection.Text,
                Does.Not.Contain("IDungeonSceneComponentQuery"));
            Assert.That(ownerSelection.Text, Does.Not.Match(@"\bTime\."));
            Assert.That(
                scheduling.Text,
                Does.Contain("interface ICharacterAiDiagnosticsQuery"));
            Assert.That(
                registration.Text,
                Does.Contain("As<ICharacterAiDiagnosticsQuery>()"));
        }

        [Test]
        public void CharacterSaveUsesActiveAndLifetimeRegistries()
        {
            SourceFile worldRegistry = SourceBySuffix(
                "Character/AI/CharacterAiWorldRegistry.cs");
            SourceFile bridge = SourceBySuffix(
                "Character/Core/CharacterActorRuntimeBridge.cs");
            SourceFile actor = SourceBySuffix(
                "Character/Core/CharacterActor.cs");
            SourceFile saveService = SourceBySuffix(
                "Infrastructure/CharacterWorldSaveService.cs");

            Assert.That(
                worldRegistry.Text,
                Does.Contain("interface ICharacterLifetimeQuery"));
            Assert.That(
                worldRegistry.Text,
                Does.Contain("IReadOnlyList<CharacterActor> AllCharacters"));
            Assert.That(
                bridge.Text,
                Does.Contain("RegisterCharacterLifetime(actor)"));
            Assert.That(
                bridge.Text,
                Does.Contain("UnregisterCharacterLifetime(actor)"));
            Assert.That(
                actor.Text,
                Does.Contain("lifecycleCoordinator.OnDestroyed(runtimeBridge, presentationBridge)"));
            Assert.That(
                saveService.Text,
                Does.Contain("ICharacterLifetimeQuery characterLifetimeQuery"));
            Assert.That(
                saveService.Text,
                Does.Contain("ICharacterWorldQuery characterWorldQuery"));
            Assert.That(
                saveService.Text,
                Does.Not.Contain("IDungeonSceneComponentQuery"));
        }

        [Test]
        public void StartPartyPreparationOwnsOnlyPreparationState()
        {
            SourceFile preparation = SourceBySuffix(
                "Character/Core/StartPartyPreparationService.cs");
            SourceFile applier = SourceBySuffix(
                "Character/Core/PreparedStartPartyGameplayApplier.cs");
            SourceFile scope = SourceBySuffix(
                "Infrastructure/DungeonPreparationLifetimeScope.cs");
            SourceFile providers = SourceBySuffix(
                "Infrastructure/LocalLlmRuntimeProvider.cs");

            Assert.That(
                preparation.Text,
                Does.Not.Contain("IDungeonSceneComponentQuery"));
            Assert.That(
                preparation.Text,
                Does.Not.Contain("IOwnerRunManagerProvider"));
            Assert.That(
                preparation.Text,
                Does.Not.Contain("ICharacterSpawnerProvider"));
            Assert.That(
                preparation.Text,
                Does.Not.Contain("ICharacterSpawnObjectFactory"));
            Assert.That(
                preparation.Text,
                Does.Not.Match(@"\bTryCommit\s*\("));
            Assert.That(applier.Text, Does.Contain("IPreparedStartPartyCommitService"));
            Assert.That(applier.Text, Does.Contain("ICharacterLifetimeQuery"));
            Assert.That(
                scope.Text,
                Does.Not.Contain("Register<OwnerRunDataProvider>"));
            Assert.That(
                scope.Text,
                Does.Not.Contain("Register<CharacterSpawnerProvider>"));
            Assert.That(
                scope.Text,
                Does.Not.Contain("Register<GridSystemProvider>"));
            Assert.That(
                scope.Text,
                Does.Not.Contain("Register<RunVariableRuntimeProvider>"));
            Assert.That(
                scope.Text,
                Does.Contain("PreparationLocalLlmRuntimeProvider"));
            Assert.That(
                providers.Text,
                Does.Contain("class PreparationLocalLlmRuntimeProvider"));
        }

        [Test]
        public void InvasionIntruderUsesInjectedGameClock()
        {
            SourceFile runtime = SourceBySuffix(
                "Invasion/InvasionIntruderSystem.cs");

            Assert.That(runtime.Text, Does.Contain("IGameClock"));
            Assert.That(runtime.Text, Does.Contain("ResolveGameClock()"));
            Assert.That(runtime.Text, Does.Not.Match(@"\bTime\."));
        }

        [Test]
        public void InvasionPathPlanningUsesAnInjectedRandomStream()
        {
            SourceFile planner = SourceBySuffix(
                "Models/Invasion/Core/InvasionIntruderPlanner.cs");
            SourceFile runtime = SourceBySuffix(
                "Invasion/InvasionIntruderSystem.cs");

            Assert.That(
                planner.Text,
                Does.Contain("IRandomStream randomStream"));
            Assert.That(
                planner.Text,
                Does.Not.Match(@"\b(?:UnityEngine\.)?Random\."));
            Assert.That(
                runtime.Text,
                Does.Contain("invasion-intruder:{runtimeId}"));
            Assert.That(
                runtime.Text,
                Does.Contain("IRandomStreamProvider randomStreamProvider"));
        }

        [Test]
        public void ExteriorActivityUsesInjectedClockAndRandomStream()
        {
            SourceFile exterior = SourceBySuffix(
                "Exterior/ExteriorActivityRuntime.cs");
            SourceFile executionServices = SourceBySuffix(
                "Exterior/ExteriorActivityRuntimeServices.cs");
            SourceFile applicationAdapter = SourceBySuffix(
                "Exterior/ExteriorActivityApplicationAdapter.cs");

            Assert.That(executionServices.Text, Does.Contain("IGameClock clock"));
            Assert.That(applicationAdapter.Text, Does.Contain("IGameClock gameClock"));
            Assert.That(
                exterior.Text,
                Does.Contain(".Get(\"exterior-incidents\")"));
            Assert.That(exterior.Text, Does.Not.Match(@"\bTime\."));
            Assert.That(
                exterior.Text,
                Does.Not.Match(@"\b(?:UnityEngine\.)?Random\."));
        }

        [Test]
        public void CharacterDeprivationUsesInjectedClockAndRandomStream()
        {
            SourceFile deprivation = SourceBySuffix(
                "Survival/CharacterDeprivationRuntime.cs");

            Assert.That(deprivation.Text, Does.Contain("IGameClock gameClock"));
            Assert.That(
                deprivation.Text,
                Does.Contain(".Get(\"character-deprivation\")"));
            Assert.That(deprivation.Text, Does.Not.Match(@"\bTime\."));
            Assert.That(
                deprivation.Text,
                Does.Not.Match(@"\b(?:UnityEngine\.)?Random\."));
        }

        [Test]
        public void GridRandomPathSelectionRequiresACallerOwnedStream()
        {
            SourceFile grid = SourceBySuffix("Grid/Core/Grid.cs");
            SourceFile searchResult = SourceBySuffix(
                "Grid/Core/GridPathSearchResult.cs");
            SourceFile movement = SourceBySuffix(
                "Character/Ability/AbilityMove.cs");

            Assert.That(
                searchResult.Text,
                Does.Contain("IRandomStream randomStream"));
            Assert.That(
                grid.Text,
                Does.Not.Match(@"\b(?:UnityEngine\.)?Random\."));
            Assert.That(
                movement.Text,
                Does.Contain(".Get(\"character-movement\")"));
        }

        [Test]
        public void AiActionsUseTheBrainOwnedRandomStream()
        {
            SourceFile brain = SourceBySuffix(
                "Character/AI/AIBrain.cs");
            SourceFile brainServices = SourceBySuffix(
                "Character/AI/AIBrainServices.cs");
            SourceFile lookAround = SourceBySuffix(
                "Models/AI/Core/AILookAround.cs");
            SourceFile lookAroundAdapter = SourceBySuffix(
                "Infrastructure/AI/Actions/AILookAroundAdapter.cs");
            SourceFile wait = SourceBySuffix(
                "Models/AI/Core/AIWait.cs");
            SourceFile waitAdapter = SourceBySuffix(
                "Infrastructure/AI/Actions/AIWaitAdapter.cs");
            SourceFile consideration = SourceBySuffix(
                "Models/AI/Core/ConsiderationRandom.cs");
            SourceFile considerationAdapter = SourceBySuffix(
                "Infrastructure/AI/Considerations/ConsiderationRandomAdapter.cs");

            Assert.That(
                brainServices.Text,
                Does.Contain(".Get(\"character-ai\")"));
            Assert.That(
                lookAroundAdapter.Text,
                Does.Match(@"\bbrain\.NextRandomIndex\s*\("));
            Assert.That(
                lookAround.Text,
                Does.Not.Match(@"\b(?:UnityEngine\.)?Random\."));
            Assert.That(
                lookAroundAdapter.Text,
                Does.Not.Match(@"\b(?:UnityEngine\.)?Random\."));
            Assert.That(
                waitAdapter.Text,
                Does.Contain("actor.Brain.NextRandom"));
            Assert.That(
                considerationAdapter.Text,
                Does.Contain("actor.Brain.NextRandom"));
            Assert.That(
                wait.Text,
                Does.Not.Match(@"\b(?:UnityEngine\.)?Random\."));
            Assert.That(
                consideration.Text,
                Does.Not.Match(@"\b(?:UnityEngine\.)?Random\."));
        }

        [Test]
        public void InvasionThreatUsesInjectedClockAndRandomStream()
        {
            SourceFile runtime = SourceBySuffix(
                "Invasion/InvasionThreatRuntime.cs");
            SourceFile settings = SourceBySuffix(
                "Invasion/Core/InvasionPrimitives.cs");

            Assert.That(runtime.Text, Does.Contain("IGameClock"));
            Assert.That(runtime.Text, Does.Contain("\"invasion-threat\""));
            Assert.That(runtime.Text, Does.Not.Match(@"\bTime\."));
            Assert.That(settings.Text, Does.Contain("IRandomStream"));
            Assert.That(
                settings.Text,
                Does.Not.Match(@"\b(?:UnityEngine\.)?Random\."));
        }

        [Test]
        public void WildlifeEcosystemUsesInjectedRandomStream()
        {
            SourceFile runtime = SourceBySuffix(
                "Models/Wildlife/Core/WildlifeEcosystemRuntime.cs");
            SourceFile markerRegistry = SourceBySuffix(
                "Infrastructure/WildlifeHabitatMarkerRegistry.cs");
            SourceFile applicationPorts = SourceBySuffix(
                "Wildlife/WildlifeEcosystemApplicationAdapters.cs");
            SourceFile viewToggle = SourceBySuffix(
                "UI/WildlifeEcosystemViewToggleRuntime.cs");

            Assert.That(runtime.Text, Does.Contain("IRandomStreamProvider"));
            Assert.That(runtime.Text, Does.Contain("\"wildlife-ecosystem\""));
            Assert.That(applicationPorts.Text, Does.Contain("IWildlifeHabitatMarkerQuery"));
            Assert.That(
                runtime.Text,
                Does.Not.Contain("IDungeonSceneComponentQuery"));
            Assert.That(
                applicationPorts.Text,
                Does.Not.Contain("IDungeonSceneComponentQuery"));
            Assert.That(
                runtime.Text,
                Does.Not.Match(@"\b(?:UnityEngine\.)?Random\."));
            Assert.That(
                markerRegistry.Text,
                Does.Contain("sceneReferences.WildlifeHabitats"));
            Assert.That(
                markerRegistry.Text,
                Does.Not.Contain("IDungeonSceneComponentQuery"));
            Assert.That(
                runtime.Text,
                Does.Not.Contain("WildlifeEcosystemViewToggleRuntime"));
            Assert.That(
                viewToggle.Text,
                Does.Contain("WildlifeEcosystemViewToggleRuntime"));
        }

        [Test]
        public void SocialReputationUsesScopedWorldDependencies()
        {
            SourceFile reputation = SourceBySuffix(
                "Character/AI/SocialReputationRuntime.cs");
            SourceFile ledger = SourceBySuffix(
                "Character/AI/GlobalFacilityReputationLedger.cs");
            SourceFile promptComposer = SourceBySuffix(
                "Character/AI/SocialRumorPromptComposer.cs");
            SourceFile characterSave = SourceBySuffix(
                "Infrastructure/CharacterWorldSaveService.cs");

            Assert.That(reputation.Text, Does.Contain("ICharacterWorldQuery"));
            Assert.That(reputation.Text, Does.Contain("IBuildingWorldQuery"));
            Assert.That(reputation.Text, Does.Contain("IGameClock"));
            Assert.That(reputation.Text, Does.Contain("IRandomStreamProvider"));
            Assert.That(reputation.Text, Does.Not.Contain("IDungeonSceneComponentQuery"));
            Assert.That(reputation.Text, Does.Not.Contain("Time.time"));
            Assert.That(reputation.Text, Does.Not.Contain("UnityEngine.Random"));
            Assert.That(
                reputation.Text,
                Does.Not.Match(@"static\s+SocialReputationRuntime\s+(?:instance|Current)"));
            Assert.That(
                characterSave.Text,
                Does.Not.Contain("SocialReputationRuntime.Current"));
            Assert.That(
                reputation.Text,
                Does.Contain("List<SocialRumor> globalFacilityRumors"),
                "The MonoBehaviour must remain the serialized rumor authority.");
            Assert.That(
                reputation.Text,
                Does.Contain("new GlobalFacilityReputationLedger("));
            Assert.That(
                ledger.Text,
                Does.Contain("List<SocialRumor> rumors"));
            Assert.That(
                ledger.Text,
                Does.Not.Contain("MonoBehaviour"));
            Assert.That(
                promptComposer.Text,
                Does.Contain("IBuildingWorldQuery"));
            Assert.That(
                promptComposer.Text,
                Does.Contain("Return exactly one JSON object"));
            Assert.That(
                reputation.Text,
                Does.Not.Contain("StringBuilder"));
        }

        [Test]
        public void HotWorldQueriesUseScopedRegistriesInsteadOfSceneSearches()
        {
            SourceFile worldRegistry = SourceBySuffix(
                "Character/AI/CharacterAiWorldRegistry.cs");
            string[] migratedConsumers =
            {
                "Character/AI/AiDirectorContextSceneQuery.cs",
                "Character/AI/CharacterAiDecisionPipeline.cs",
                "Character/AI/CharacterAiScheduler.cs",
                "Character/AI/SocialReputationRuntime.cs",
                "Character/Core/CharacterSkillRuntimeEffects.cs",
                "Character/Work/StaffDiscontentSystem.cs",
                "Models/Work/StaffWorkforceQueryService.cs",
                "Character/Work/WorkforceReplanService.cs",
                "Models/Buildings/Core/BuildingManagementSummaryQuery.cs",
                "Combat/CombatLoadoutPreparationRuntime.cs",
                "Invasion/InvasionThreatWorldSampler.cs",
                "Invasion/DefenseEngagementRuntime.cs",
                "Invasion/InvasionIntruderContext.cs",
                "Items/ItemTransferService.cs",
                "Items/WorldItemHaulPlanningService.cs",
                "FacilityEvolution/WarehouseFacilityEvolutionResourceProvider.cs",
                "Recruitment/RecruitedCharacterActivationService.cs",
                "Survival/SampleSceneRationRuntime.cs"
            };

            Assert.That(worldRegistry.Text, Does.Contain("ICharacterWorldQuery"));
            Assert.That(worldRegistry.Text, Does.Contain("IBuildingWorldQuery"));
            Assert.That(worldRegistry.Text, Does.Contain("IRetailWorldQuery"));
            Assert.That(worldRegistry.Text, Does.Contain("IWarehouseWorldQuery"));

            foreach (string suffix in migratedConsumers)
            {
                SourceFile consumer = SourceBySuffix(suffix);
                Assert.That(
                    consumer.Text,
                    Does.Not.Contain("IDungeonSceneComponentQuery"),
                    $"{suffix} regressed to scene hierarchy searches.");
            }

            SourceFile offenseServices = SourceBySuffix(
                "Offense/OffenseRuntimeServices.cs");
            Match expeditionMembers = Regex.Match(
                offenseServices.Text,
                @"public sealed class OffenseExpeditionMemberQuery.*?"
                    + @"(?=public sealed class DataCatalogOffenseRewardCatalog)",
                RegexOptions.Singleline);
            Assert.That(expeditionMembers.Success, Is.True);
            Assert.That(
                expeditionMembers.Value,
                Does.Not.Contain("IDungeonSceneComponentQuery"));
            Assert.That(
                SourceBySuffix("Survival/SampleSceneRationRuntime.cs").Text,
                Does.Not.Match(@"\bTime\."));
        }

        [Test]
        public void DomainSummariesUseScopedProvidersInsteadOfSceneSearches()
        {
            string[] migratedConsumers =
            {
                "Models/Codex/Core/CodexRecordSummaryQuery.cs",
                "Models/Research/Core/ResearchCraftingSummary.cs",
                "Offense/OffenseTabSummaryQuery.cs",
                "Invasion/InvasionDefenseSummaryQuery.cs",
                "Offense/OffenseRewardContextResolver.cs"
            };

            foreach (string suffix in migratedConsumers)
            {
                SourceFile consumer = SourceBySuffix(suffix);
                Assert.That(
                    consumer.Text,
                    Does.Not.Contain("IDungeonSceneComponentQuery"),
                    $"{suffix} regressed to scene hierarchy searches.");
            }

            Assert.That(
                SourceBySuffix("Invasion/InvasionDefenseSummaryQuery.cs").Text,
                Does.Contain("IBuildingWorldQuery"));
            Assert.That(
                SourceBySuffix("Offense/OffenseRewardContextResolver.cs").Text,
                Does.Contain("IWarehouseWorldQuery"));
        }

        [Test]
        public void DefenseCoordinatorDelegatesCombatExecution()
        {
            SourceFile runtime = SourceBySuffix(
                "Invasion/DefenseEngagementRuntime.cs");
            SourceFile executor = SourceBySuffix(
                "Invasion/DefenseCombatExecutor.cs");

            Assert.That(runtime.Text, Does.Contain("IDefenseCombatExecutor"));
            Assert.That(runtime.Text, Does.Not.Contain("CombatAttackRequest"));
            Assert.That(runtime.Text, Does.Not.Contain("ICombatResolutionService"));
            Assert.That(executor.Text, Does.Contain("CombatAttackRequest"));
            Assert.That(executor.Text, Does.Contain("ICombatResolutionService"));
        }

        [Test]
        public void DefenseCoordinatorDelegatesEngagementStorage()
        {
            SourceFile runtime = SourceBySuffix(
                "Invasion/DefenseEngagementRuntime.cs");
            SourceFile store = SourceBySuffix(
                "Invasion/DefenseEngagementStore.cs");

            Assert.That(runtime.Text, Does.Contain("IDefenseEngagementStore"));
            Assert.That(
                runtime.Text,
                Does.Not.Contain("new List<DefenseEngagement>"));
            Assert.That(runtime.Text, Does.Not.Contain("retreatedGuardIds"));
            Assert.That(store.Text, Does.Contain("ObserveSequence"));
            Assert.That(store.Text, Does.Contain("Duplicate defense engagement id"));
        }

        [Test]
        public void WarehouseFeatureOwnsQueryCommandAndPresentation()
        {
            SourceFile panel = SourceBySuffix("Views/UI/Core/P0FeatureSurfacePanel.cs");
            SourceFile presenter = SourceBySuffix(
                "UI/WarehouseFeatureSurfacePresenter.cs");
            SourceFile presenterCatalog = SourceBySuffix(
                "UI/Core/PresentationPrimitives.cs");

            Assert.That(panel.Text, Does.Not.Contain("BuildWarehouse("));
            Assert.That(panel.Text, Does.Not.Contain("IBuildingManagementSummaryService"));
            Assert.That(panel.Text, Does.Not.Contain("IRunVariableRuntimeReader"));
            Assert.That(presenter.Text, Does.Contain("IWarehouseFeatureQueryService"));
            Assert.That(presenter.Text, Does.Contain("IWarehouseFeatureCommandService"));
            Assert.That(presenter.Text, Does.Contain("IFeatureSurfaceView"));
            Assert.That(presenter.Text, Does.Contain("IWarehouseWorldQuery"));
            Assert.That(presenter.Text, Does.Contain("IBuildingWorldQuery"));
            Assert.That(presenter.Text, Does.Contain("ICharacterWorldQuery"));
            Assert.That(
                presenter.Text,
                Does.Not.Contain("IDungeonSceneComponentQuery"));
            Assert.That(
                presenterCatalog.Text,
                Does.Not.Contain("surface.BuildWarehouse"));
        }

        [Test]
        public void BuildingFeatureOwnsQueryCommandAndPresentation()
        {
            SourceFile presenter = SourceBySuffix(
                "UI/BuildingFeatureSurfacePresenter.cs");
            SourceFile presenterCatalog = SourceBySuffix(
                "UI/Core/PresentationPrimitives.cs");

            Assert.That(presenter.Text, Does.Contain("IBuildingFeatureQueryService"));
            Assert.That(presenter.Text, Does.Contain("IBuildingFeatureCommandService"));
            Assert.That(presenter.Text, Does.Contain("IFeatureSurfaceView"));
            Assert.That(
                presenterCatalog.Text,
                Does.Not.Contain("BuildFacilitiesManagement"));
            Assert.That(
                presenterCatalog.Text,
                Does.Not.Contain("The building presenter requires the legacy feature surface"));
        }

        [Test]
        public void ShopFeatureOwnsQueryCommandAndPresentation()
        {
            SourceFile panel = SourceBySuffix("Views/UI/Core/P0FeatureSurfacePanel.cs");
            SourceFile presenter = SourceBySuffix(
                "UI/ShopFeatureSurfacePresenter.cs");
            SourceFile presenterCatalog = SourceBySuffix(
                "UI/Core/PresentationPrimitives.cs");

            Assert.That(panel.Text, Does.Not.Contain("BuildFacilityShop("));
            Assert.That(panel.Text, Does.Not.Contain("BuildShopOperationsDetail("));
            Assert.That(panel.Text, Does.Not.Contain("IDailyFacilityShopRuntimeProvider"));
            Assert.That(panel.Text, Does.Not.Contain("completedUiActions"));
            Assert.That(presenter.Text, Does.Contain("IShopFeatureQueryService"));
            Assert.That(presenter.Text, Does.Contain("IShopFeatureCommandService"));
            Assert.That(presenter.Text, Does.Contain("IFeatureSurfaceView"));
            Assert.That(presenter.Text, Does.Contain("IRetailWorldQuery"));
            Assert.That(
                presenter.Text,
                Does.Not.Contain("IDungeonSceneComponentQuery"));
            Assert.That(presenterCatalog.Text, Does.Not.Contain("surface.BuildFacilityShop"));
        }

        [Test]
        public void ResearchFeatureOwnsQueryCommandAndPresentation()
        {
            SourceFile panel = SourceBySuffix("Views/UI/Core/P0FeatureSurfacePanel.cs");
            SourceFile presenter = SourceBySuffix(
                "UI/ResearchTreeWindow.cs");
            SourceFile presenterCatalog = SourceBySuffix(
                "UI/Core/PresentationPrimitives.cs");

            Assert.That(panel.Text, Does.Not.Contain("BuildResearch("));
            Assert.That(panel.Text, Does.Not.Contain("IBlueprintResearchRuntimeProvider"));
            Assert.That(panel.Text, Does.Not.Contain("IFacilityShopCatalog"));
            Assert.That(presenter.Text, Does.Contain("IResearchProjectCatalog"));
            Assert.That(presenter.Text, Does.Contain("IResearchQueueCommandService"));
            Assert.That(presenter.Text, Does.Contain("IResearchRewardCatalog"));
            Assert.That(presenter.Text, Does.Not.Contain("IBuildingWorldQuery"));
            Assert.That(presenter.Text, Does.Not.Contain("IStaffWorkforceQueryService"));
            Assert.That(
                presenter.Text,
                Does.Not.Contain("IDungeonSceneComponentQuery"));
            Assert.That(presenterCatalog.Text, Does.Not.Contain("surface.BuildResearch"));
        }

        [Test]
        public void ResearchTreePlayModeVerifierPreparesAnIndependentPlayableRun()
        {
            SourceFile verifier = SourceBySuffixIncludingEditor(
                "Views/UI/Editor/ResearchTreePlayModeVerifier.cs");

            int ownerSelection = verifier.Text.IndexOf(
                "yield return CompleteOwnerSelectionIfVisible();",
                StringComparison.Ordinal);
            int partyPreparation = verifier.Text.IndexOf(
                "yield return StartPartyPlayModeTestDriver.CompleteIfVisible(45f);",
                StringComparison.Ordinal);
            int overlayCleanup = verifier.Text.IndexOf(
                "yield return ClearBlockingRunOverlays();",
                StringComparison.Ordinal);
            int firstResearchInteraction = verifier.Text.IndexOf(
                "yield return SelectResolution(1600, 900);",
                StringComparison.Ordinal);

            Assert.That(ownerSelection, Is.GreaterThanOrEqualTo(0));
            Assert.That(partyPreparation, Is.GreaterThan(ownerSelection));
            Assert.That(overlayCleanup, Is.GreaterThan(partyPreparation));
            Assert.That(firstResearchInteraction, Is.GreaterThan(overlayCleanup));
            Assert.That(verifier.Text, Does.Contain("OwnerRunManager"));
            Assert.That(verifier.Text, Does.Contain("PLAYABLE_RUN_READY"));
            Assert.That(verifier.Text, Does.Contain("OwnerSelectionSurface"));
            Assert.That(verifier.Text, Does.Contain("RUN_OVERLAYS_CLEARED"));
        }

        [Test]
        public void CodexFeatureOwnsQueryCommandAndPresentation()
        {
            SourceFile panel = SourceBySuffix("Views/UI/Core/P0FeatureSurfacePanel.cs");
            SourceFile presenter = SourceBySuffix(
                "UI/CodexFeatureSurfacePresenter.cs");
            SourceFile presenterCatalog = SourceBySuffix(
                "UI/Core/PresentationPrimitives.cs");
            SourceFile registration = SourceBySuffix(
                "Infrastructure/Registration/DungeonPresentationRegistration.cs");

            Assert.That(panel.Text, Does.Not.Contain("ICodexRuntimeProvider"));
            Assert.That(presenter.Text, Does.Contain("ICodexFeatureQueryService"));
            Assert.That(presenter.Text, Does.Contain("ICodexFeatureCommandService"));
            Assert.That(presenter.Text, Does.Not.Contain("ICodexRuntimeProvider"));
            Assert.That(presenter.Text, Does.Not.Contain("IEventAlertRuntimeProvider"));
            Assert.That(presenter.Text, Does.Not.Contain("IInvasionCombatReportRuntimeProvider"));
            Assert.That(presenter.Text, Does.Not.Contain("IOffenseExpeditionRuntimeProvider"));
            Assert.That(presenter.Text, Does.Not.Contain("IOperatingDaySettlementRuntimeProvider"));
            Assert.That(
                presenter.Text,
                Does.Not.Contain("IDungeonSceneComponentQuery"));
            Assert.That(
                presenterCatalog.Text,
                Does.Not.Contain("surface.BuildCodexAndHistory"));
            Assert.That(registration.Text, Does.Contain("As<ICodexFeatureQueryService>()"));
            Assert.That(registration.Text, Does.Contain("As<ICodexFeatureCommandService>()"));
        }

        [Test]
        public void ExpeditionFeatureOwnsQueryCommandAndPresentation()
        {
            SourceFile presenter = SourceBySuffix(
                "UI/ExpeditionFeatureSurfacePresenter.cs");
            SourceFile presenterCatalog = SourceBySuffix(
                "UI/Core/PresentationPrimitives.cs");

            Assert.That(presenter.Text, Does.Contain("IExpeditionFeatureQueryService"));
            Assert.That(presenter.Text, Does.Contain("IExpeditionFeatureCommandService"));
            Assert.That(presenter.Text, Does.Contain("IFeatureSurfaceView"));
            Assert.That(
                presenter.Text,
                Does.Not.Contain("IDungeonSceneComponentQuery"));
            Assert.That(
                presenterCatalog.Text,
                Does.Not.Contain("BuildOffenseOperations"));
            Assert.That(
                presenterCatalog.Text,
                Does.Not.Contain("The expedition presenter requires the legacy feature surface"));
        }

        [Test]
        public void DefenseFeatureOwnsQueryCommandAndPresentation()
        {
            SourceFile panel = SourceBySuffix("Views/UI/Core/P0FeatureSurfacePanel.cs");
            SourceFile presenter = SourceBySuffix(
                "UI/DefenseFeatureSurfacePresenter.cs");
            SourceFile presenterCatalog = SourceBySuffix(
                "UI/Core/PresentationPrimitives.cs");

            Assert.That(presenter.Text, Does.Contain("IDefenseFeatureQueryService"));
            Assert.That(presenter.Text, Does.Contain("IDefenseFeatureCommandService"));
            Assert.That(presenter.Text, Does.Contain("IFeatureSurfaceView"));
            Assert.That(
                presenter.Text,
                Does.Not.Contain("IDungeonSceneComponentQuery"));
            Assert.That(panel.Text, Does.Not.Contain("IDefenseEngagementRuntime"));
            Assert.That(panel.Text, Does.Not.Contain("IDefenseResponsePolicyRuntime"));
            Assert.That(
                presenterCatalog.Text,
                Does.Not.Contain("BuildDefenseOperations"));
            Assert.That(
                presenterCatalog.Text,
                Does.Not.Contain("The defense presenter requires the legacy feature surface"));
        }

        [Test]
        public void OperationsFeatureOwnsQueryCommandAndPresentation()
        {
            SourceFile presenter = SourceBySuffix(
                "Views/UI/Core/OperationsFeatureSurfacePresenter.cs");
            SourceFile presenterCatalog = SourceBySuffix(
                "UI/Core/PresentationPrimitives.cs");

            Assert.That(presenter.Text, Does.Contain("IOperationsFeatureQueryService"));
            Assert.That(presenter.Text, Does.Contain("IOperationsFeatureCommandService"));
            Assert.That(presenter.Text, Does.Contain("IFeatureSurfaceView"));
            Assert.That(
                presenter.Text,
                Does.Not.Contain("IDungeonSceneComponentQuery"));
            Assert.That(
                presenterCatalog.Text,
                Does.Not.Contain("BuildOperationHub"));
            Assert.That(
                presenterCatalog.Text,
                Does.Not.Contain("RequireLegacySurface"));
            Assert.That(
                presenterCatalog.Text,
                Does.Not.Contain("P0FeatureSurfacePanel"));
        }

        [Test]
        public void FeatureSurfaceShellOnlyOwnsLayoutAndViewContracts()
        {
            SourceFile panel = SourceBySuffix("Views/UI/Core/P0FeatureSurfacePanel.cs");

            Assert.That(panel.Text, Does.Not.Contain("IDungeonSceneComponentQuery"));
            Assert.That(panel.Text, Does.Not.Contain("ICombatEquipmentMaintenanceRuntime"));
            Assert.That(panel.Text, Does.Not.Contain("IWildlifeEcosystemRuntime"));
            Assert.That(panel.Text, Does.Not.Contain("IRegularCustomerRuntimeProvider"));
            Assert.That(panel.Text, Does.Not.Contain("IMetaProgressionRuntimeProvider"));
            Assert.That(panel.Text, Does.Contain("IFeatureSurfaceTabPresenterRegistry"));
            Assert.That(panel.Text, Does.Contain("IFeatureSurfaceView"));
        }

        [Test]
        public void WorldItemRuntimeDelegatesHaulPlanning()
        {
            SourceFile runtime = SourceBySuffix(
                "Items/WorldItemStackRuntime.cs");
            SourceFile planner = SourceBySuffix(
                "Items/WorldItemHaulPlanningService.cs");

            Assert.That(runtime.Text, Does.Contain("IWorldItemHaulPlanningService"));
            Assert.That(runtime.Text, Does.Not.Contain("HaulCandidate"));
            Assert.That(runtime.Text, Does.Not.Contain("TryFindBestHaulPlan"));
            Assert.That(runtime.Text, Does.Not.Contain("TryFindBestHaulJob"));
            Assert.That(runtime.Text, Does.Not.Contain("IDungeonSceneComponentQuery"));
            Assert.That(planner.Text, Does.Contain("TryBuildBestPlan"));
            Assert.That(planner.Text, Does.Contain("GetDetour"));
            Assert.That(planner.Text, Does.Contain("TryFindWarehouse"));
        }

        [Test]
        public void WorldItemRuntimeDelegatesInventoryTransfer()
        {
            SourceFile runtime = SourceBySuffix(
                "Items/WorldItemStackRuntime.cs");
            SourceFile transfer = SourceBySuffix(
                "Items/ItemTransferService.cs");

            Assert.That(runtime.Text, Does.Contain("IItemTransferService"));
            Assert.That(
                runtime.Text,
                Does.Not.Contain("inventory.RemoveAllItems()"));
            Assert.That(
                runtime.Text,
                Does.Not.Contain("CombatEquipmentWorldState.MaintenanceBuffer"));
            Assert.That(
                transfer.Text,
                Does.Contain("TryPickupReservedStackQuantity"));
            Assert.That(
                transfer.Text,
                Does.Contain("TryDepositCarriedItemsToFacility"));
            Assert.That(
                transfer.Text,
                Does.Contain("TryConsumeFacilityBuffer"));
            Assert.That(
                transfer.Text,
                Does.Not.Contain("IDungeonSceneComponentQuery"));
            Assert.That(
                transfer.Text,
                Does.Not.Contain(".All<MonoBehaviour>"));
            Assert.That(
                SourceBySuffix("Items/WorldItemHaulPlanningService.cs").Text,
                Does.Not.Contain("IDungeonSceneComponentQuery"));
        }

        [Test]
        public void CharacterActorDelegatesRuntimeAndPresentationLifecycle()
        {
            SourceFile actor = SourceBySuffix(
                "Character/Core/CharacterActor.cs");
            SourceFile runtimeBridge = SourceBySuffix(
                "Character/Core/CharacterActorRuntimeBridge.cs");
            SourceFile presentationBridge = SourceBySuffix(
                "Character/Core/CharacterActorPresentationBridge.cs");

            Assert.That(actor.Text, Does.Contain("CharacterActorRuntimeBridge"));
            Assert.That(actor.Text, Does.Contain("CharacterActorPresentationBridge"));
            Assert.That(actor.Text, Does.Not.Contain("registeredWithAiScheduler"));
            Assert.That(actor.Text, Does.Not.Contain("registeredWithWorldRegistry"));
            Assert.That(actor.Text, Does.Not.Contain("feedbackBubbleFactory;"));
            Assert.That(runtimeBridge.Text, Does.Contain("worldRegistry.RegisterCharacter(actor)"));
            Assert.That(presentationBridge.Text, Does.Contain("WorldCharacterNameplate.Ensure(actor)"));
        }

        [Test]
        public void AiStateAndWildlifeUseInjectedTimeAndRandom()
        {
            SourceFile blackboard = SourceBySuffix(
                "Character/AI/CharacterBlackboard.cs");
            SourceFile memory = SourceBySuffix(
                "Character/AI/CharacterAiMemoryRuntime.cs");
            SourceFile brain = SourceBySuffix(
                "Character/AI/AIBrain.cs");
            SourceFile wildlife = SourceBySuffix(
                "Wildlife/WildlifeRuntime.cs");
            SourceFile wildlifeActor = SourceBySuffix(
                "Wildlife/WildlifeActor.cs");
            SourceFile wildlifeModels = SourceBySuffix(
                "Wildlife/WildlifeModels.cs");
            SourceFile shopping = SourceBySuffix(
                "Character/Ability/AbilityShopping.cs");
            SourceFile stats = SourceBySuffix(
                "Character/Core/CharacterStats.cs");

            Assert.That(blackboard.Text, Does.Contain("IGameClock"));
            Assert.That(memory.Text, Does.Contain("IGameClock"));
            Assert.That(brain.Text, Does.Contain("IGameClock"));
            Assert.That(wildlife.Text, Does.Contain("IRandomStream randomStream"));
            Assert.That(wildlifeActor.Text, Does.Contain("IRandomStreamProvider"));
            Assert.That(
                wildlifeModels.Text,
                Does.Contain("GetRandomSpecies(IRandomStream randomStream)"));
            Assert.That(shopping.Text, Does.Contain("IRandomStreamProvider"));
            Assert.That(shopping.Text, Does.Contain("IGameClock"));
            Assert.That(stats.Text, Does.Contain("IGameClock"));
            Assert.That(blackboard.Text, Does.Not.Match(@"\bTime\."));
            Assert.That(memory.Text, Does.Not.Match(@"\bTime\."));
            Assert.That(brain.Text, Does.Not.Match(@"\bTime\."));
            Assert.That(wildlife.Text, Does.Not.Match(@"\b(?:UnityEngine\.)?Random\."));
            Assert.That(wildlifeActor.Text, Does.Not.Match(@"\b(?:UnityEngine\.)?Random\."));
            Assert.That(wildlifeModels.Text, Does.Not.Match(@"\b(?:UnityEngine\.)?Random\."));
            Assert.That(shopping.Text, Does.Not.Match(@"\b(?:UnityEngine\.)?Random\."));
            Assert.That(shopping.Text, Does.Not.Match(@"\bTime\."));
            Assert.That(stats.Text, Does.Not.Match(@"\bTime\."));
        }

        [Test]
        public void CharacterProgressionCompletesConfigurationInEitherBindOrder()
        {
            SourceFile progression = SourceBySuffix(
                "Character/Core/CharacterProgression.cs");

            Match construct = Regex.Match(
                progression.Text,
                @"public void ConstructCharacterProgression\("
                    + @"(?<body>.*?)"
                    + @"public void ConfigurePreview\(",
                RegexOptions.Singleline);
            Match preview = Regex.Match(
                progression.Text,
                @"public void ConfigurePreview\("
                    + @"(?<body>.*?)"
                    + @"public static int GetExperienceRequired\(",
                RegexOptions.Singleline);
            Match bind = Regex.Match(
                progression.Text,
                @"public void Bind\(CharacterActor owner\)"
                    + @"(?<body>.*?)"
                    + @"public int AddExperience\(",
                RegexOptions.Singleline);
            Match completion = Regex.Match(
                progression.Text,
                @"private void CompleteConfigurationIfReady\(\)"
                    + @"(?<body>.*?)"
                    + @"private void EnsureInitialized\(\)",
                RegexOptions.Singleline);

            Assert.That(construct.Success, Is.True);
            Assert.That(preview.Success, Is.True);
            Assert.That(bind.Success, Is.True);
            Assert.That(completion.Success, Is.True);

            Assert.That(
                construct.Groups["body"].Value,
                Does.Match(
                    @"this\.profileProjector\s*=\s*profileProjector[\s\S]*?"
                        + @"CompleteConfigurationIfReady\(\);"),
                "Injection must commit the projector before completing configuration.");
            Assert.That(
                preview.Groups["body"].Value,
                Does.Match(
                    @"this\.profileProjector\s*=\s*profileProjector[\s\S]*?"
                        + @"CompleteConfigurationIfReady\(\);"),
                "Preview configuration must use the same completion phase.");
            Assert.That(
                bind.Groups["body"].Value,
                Does.Match(
                    @"actor\s*=\s*owner;[\s\S]*?"
                        + @"CompleteConfigurationIfReady\(\);"),
                "Bind must record the actor before attempting completion.");
            Assert.That(
                completion.Groups["body"].Value,
                Does.Match(@"if\s*\(profileProjector\s*==\s*null\)\s*\{\s*return;"),
                "Bind-before-inject must remain a safe deferred configuration phase.");
            Assert.That(
                completion.Groups["body"].Value,
                Does.Match(
                    @"EnsureInitialized\(\);[\s\S]*?"
                        + @"WarmEffectiveRuntimeProfile\(\);[\s\S]*?"
                        + @"EnsureUnlockedDrafts\(\);"),
                "Inject-before-bind must rerun the complete phase after the actor is assigned.");
        }

        [Test]
        public void PlayModePersistenceCaptureFailsClosedAndFinalFacadeOwnsCaptureTiming()
        {
            Type reportPolicyType = AppDomain.CurrentDomain.GetAssemblies()
                .Select(assembly => assembly.GetType(
                    "FinalAcceptanceReportPolicy",
                    throwOnError: false))
                .FirstOrDefault(type => type != null);
            Assert.That(reportPolicyType, Is.Not.Null);
            System.Reflection.MethodInfo isFreshPassMethod =
                reportPolicyType.GetMethod(
                    "IsFreshPass",
                    System.Reflection.BindingFlags.Static
                    | System.Reflection.BindingFlags.NonPublic);
            Assert.That(isFreshPassMethod, Is.Not.Null);
            Func<string, long, long, bool> isFreshPass =
                (report, writtenTicks, startedTicks) => (bool)isFreshPassMethod.Invoke(
                    null,
                    new object[] { report, writtenTicks, startedTicks });

            const long targetStartedUtcTicks = 100;
            const long freshReportWrittenUtcTicks = 101;
            Assert.That(
                isFreshPass(
                    "RESULT=PASS",
                    freshReportWrittenUtcTicks,
                    targetStartedUtcTicks),
                Is.True);
            Assert.That(
                isFreshPass(
                    "header\nRESULT=PASS; failures=0\nfooter",
                    freshReportWrittenUtcTicks,
                    targetStartedUtcTicks),
                Is.True);
            Assert.That(
                isFreshPass(
                    "RESULT=PASS",
                    targetStartedUtcTicks - 1,
                    targetStartedUtcTicks),
                Is.False,
                "A passing declaration from a stale report must fail closed.");
            Assert.That(
                isFreshPass(
                    "No final result was written.",
                    freshReportWrittenUtcTicks,
                    targetStartedUtcTicks),
                Is.False,
                "A fresh report without a result declaration must fail closed.");
            Assert.That(
                isFreshPass(
                    "RESULT=FAIL; failures=1",
                    freshReportWrittenUtcTicks,
                    targetStartedUtcTicks),
                Is.False);
            Assert.That(
                isFreshPass(
                    "RESULT=PASS\nRESULT=PASS",
                    freshReportWrittenUtcTicks,
                    targetStartedUtcTicks),
                Is.False,
                "Duplicate result declarations are ambiguous and must fail closed.");
            Assert.That(
                isFreshPass(
                    "RESULT=PASS\nRESULT=FAIL; failures=1",
                    freshReportWrittenUtcTicks,
                    targetStartedUtcTicks),
                Is.False,
                "Conflicting result declarations must fail closed.");
            Assert.That(
                isFreshPass(
                    "RESULT=PASSENGER",
                    freshReportWrittenUtcTicks,
                    targetStartedUtcTicks),
                Is.False,
                "Only the exact PASS result token is valid.");

            SourceFile snapshot = SourceBySuffixIncludingEditor(
                "Utils/Editor/PlayModeVerificationPersistenceSnapshot.cs");
            SourceFile facade = SourceBySuffixIncludingEditor(
                "Editor/DungeonFinalPlayModeAcceptanceRequestFacade.cs");
            SourceFile fullWorld = SourceBySuffixIncludingEditor(
                "Infrastructure/Editor/DungeonFullWorldRoundTripPlayModeFacade.cs");
            SourceFile architectureRunner = SourceByAssetsRelativePath(
                "Tests/EditMode/ArchitectureTestBatchRunner.cs");
            SourceFile transactionalRunner = SourceByAssetsRelativePath(
                "Tests/EditMode/TransactionalRestoreTestRunner.cs");
            SourceFile synchronousAcceptance = SourceBySuffixIncludingEditor(
                "Editor/DungeonStoryFinalAcceptanceRunner.cs");

            Assert.That(
                architectureRunner.Text,
                Does.Contain("public const int ExpectedTestCount = 160"));
            Assert.That(
                architectureRunner.Text,
                Does.Match(
                    @"tests\.Length\s*==\s*ExpectedTestCount[\s\S]*?"
                        + @"passed\s*==\s*ExpectedTestCount"));
            Assert.That(
                transactionalRunner.Text,
                Does.Contain("public const int ExpectedTestCount = 33"));
            Assert.That(
                transactionalRunner.Text,
                Does.Match(
                    @"startedTestCases\s*==\s*ExpectedTestCount[\s\S]*?"
                        + @"passed\s*==\s*ExpectedTestCount"));
            Assert.That(
                synchronousAcceptance.Text,
                Does.Contain("public const int ExpectedAcceptanceStepCount = 33"));
            Assert.That(
                synchronousAcceptance.Text,
                Does.Match(
                    @"steps\.Count\s*==\s*ExpectedAcceptanceStepCount[\s\S]*?"
                        + @"steps\.All\(step\s*=>\s*step\.Success\)"));

            Match captureCurrent = Regex.Match(
                snapshot.Text,
                @"public static void CaptureCurrent\(string snapshotId\)"
                    + @"(?<body>.*?)"
                    + @"public static bool Restore\(string snapshotId\)",
                RegexOptions.Singleline);
            Assert.That(captureCurrent.Success, Is.True);
            Assert.That(
                captureCurrent.Groups["body"].Value,
                Does.Contain("if (Directory.Exists(snapshotPath))"));
            Assert.That(
                captureCurrent.Groups["body"].Value,
                Does.Contain("already exists"));
            Assert.That(
                captureCurrent.Groups["body"].Value,
                Does.Not.Contain("Restore(id)"),
                "Capture must never mutate persistence by implicitly restoring an old snapshot.");
            Assert.That(snapshot.Text, Does.Contain("public List<string> directories"));
            Assert.That(snapshot.Text, Does.Contain("public string sha256"));
            Assert.That(snapshot.Text, Does.Contain("ValidateSnapshotManifest"));
            Assert.That(snapshot.Text, Does.Contain("VerifyRestoredState"));
            Assert.That(snapshot.Text, Does.Contain("ComputeSha256"));
            Assert.That(
                snapshot.Text,
                Does.Contain("Directory.Delete(persistentRoot, true)"),
                "Restore must remove extra files, directories, and an originally absent root before rebuilding the exact snapshot.");

            Match requestRun = Regex.Match(
                facade.Text,
                @"public static void RequestRunFromMenu\(\)"
                    + @"(?<body>.*?)"
                    + @"\[MenuItem\(""DungeonStory/QA/Log Final PlayMode Acceptance Status""\)\]",
                RegexOptions.Singleline);
            Assert.That(requestRun.Success, Is.True);
            Assert.That(
                requestRun.Groups["body"].Value,
                Does.Not.Contain("CaptureCurrent"),
                "The queued request must not create a duplicate persistence snapshot.");
            string requestBody = requestRun.Groups["body"].Value;
            int beginConsoleIndex = requestBody.IndexOf(
                "TryBeginConsoleCapture",
                StringComparison.Ordinal);
            int cleanupIndex = requestBody.IndexOf(
                "CleanupAllKnownMarkers",
                StringComparison.Ordinal);
            int preflightIndex = requestBody.IndexOf(
                "RunSynchronousPreflightForMcp",
                StringComparison.Ordinal);
            Assert.That(beginConsoleIndex, Is.GreaterThanOrEqualTo(0));
            Assert.That(cleanupIndex, Is.GreaterThan(beginConsoleIndex));
            Assert.That(preflightIndex, Is.GreaterThan(cleanupIndex));
            Assert.That(
                facade.Text,
                Does.Contain(
                    "Application.logMessageReceivedThreaded += OnLogMessage"));
            Assert.That(
                facade.Text,
                Does.Contain("Application.logMessageReceived -= OnLogMessage"));
            Assert.That(
                facade.Text,
                Does.Not.Contain("Application.logMessageReceived += OnLogMessage"));
            Assert.That(facade.Text, Does.Contain("lock (ConsoleIoSync)"));
            Assert.That(
                facade.Text,
                Does.Contain("Interlocked.Increment(ref activeConsoleCallbacks)"));
            Assert.That(
                facade.Text,
                Does.Contain("Interlocked.Decrement(ref activeConsoleCallbacks)"));
            Assert.That(
                facade.Text,
                Does.Contain("ConsoleCallbackDrainTimeoutMilliseconds"));
            Assert.That(
                facade.Text,
                Does.Contain("ReadConsoleEvidence(requireActiveMarker: false)"));
            Assert.That(
                facade.Text,
                Does.Contain("private const double TargetTimeoutSeconds = 1800d"),
                "The final runner must cover observed fifteen-minute scene integration stalls.");
            Assert.That(
                facade.Text,
                Does.Contain("ResumeAfterInfrastructureTimeoutFromMenu"));
            Assert.That(
                facade.Text,
                Does.Contain("QueueInfrastructureTimeoutResumeForMcp"));
            Assert.That(
                facade.Text,
                Does.Contain("TryValidateInfrastructureTimeoutResumeEvidence"));
            Assert.That(
                facade.Text,
                Does.Contain("persistenceRestoredNow=True"));
            Assert.That(
                facade.Text,
                Does.Contain("consoleWarnings=0"));

            Match validateTargets = Regex.Match(
                facade.Text,
                @"private static string ValidateTargetsAndCaptures\(\)"
                    + @"(?<body>.*?)"
                    + @"private static void EnsureSceneCanOpenWithoutPrompt",
                RegexOptions.Singleline);
            Assert.That(validateTargets.Success, Is.True);
            string targetContract = validateTargets.Groups["body"].Value;
            Assert.That(
                targetContract,
                Does.Match(@"Targets\.Length\s*!=\s*expected\.Count"));
            Assert.That(
                targetContract,
                Does.Contain("if (totalCaptures != 32)"));
            Assert.That(targetContract, Does.Contain("target.CaptureArtifacts"));
            Assert.That(targetContract, Does.Contain("capture.Width == 1600"));
            Assert.That(targetContract, Does.Contain("capture.Height == 1600"));
            string[] expectedTargetNames =
            {
                "ResolutionMatrix",
                "FullWorldRoundTrip",
                "ResearchTree",
                "Production",
                "ServiceRoom",
                "CharacterSummaryMedical",
                "EquipmentExpeditionUiMatrix"
            };
            foreach (string targetName in expectedTargetNames)
            {
                Assert.That(
                    targetContract,
                    Does.Contain("{ \"" + targetName + "\", new[]"),
                    targetName);
            }
            Assert.That(facade.Text, Does.Contain("AreFreshPngArtifacts"));
            Assert.That(facade.Text, Does.Contain("TryReadPngDimensions"));
            Assert.That(facade.Text, Does.Contain("IHDR chunk"));
            Assert.That(facade.Text, Does.Contain("wrongDimensions="));
            foreach (string fullWorldMarker in new[]
                     {
                        "registeredSections=68",
                        "capturedSections=68",
                        "postRoundTripSections=68",
                         "baselineRestored=True",
                         "canonicalBaselineMatched=True"
                     })
            {
                Assert.That(facade.Text, Does.Contain(fullWorldMarker));
            }
            Assert.That(
                facade.Text,
                Does.Contain("reportLines.Contains(marker)"),
                "Required report markers must match complete lines rather than substrings.");
            Assert.That(
                fullWorld.Text,
                Does.Contain("\"postRoundTripSections=\" + postRoundTripSections"));
            Assert.That(
                facade.Text,
                Does.Contain(
                    "Artifacts/QA/final-playmode-acceptance-preflight-report.txt"));
            Assert.That(facade.Text, Does.Contain("consoleCaptureHealthy="));
            Assert.That(facade.Text, Does.Contain("consoleWarnings="));
            Assert.That(facade.Text, Does.Contain("consoleErrors="));
            Assert.That(facade.Text, Does.Contain("consoleExceptions="));
            Assert.That(facade.Text, Does.Contain("consoleAsserts="));
            Assert.That(facade.Text, Does.Contain("offendingLogPreview:"));
            Assert.That(
                facade.Text,
                Does.Contain("ValidateCompletedTargetProgress(progress"));
            Assert.That(facade.Text, Does.Contain("targetCount="));
            Assert.That(facade.Text, Does.Contain("captureCount="));
            Assert.That(facade.Text, Does.Contain("completedTargetCount="));

            Match completeFinish = Regex.Match(
                facade.Text,
                @"private static void CompleteFinish\("
                    + @"(?<body>.*?)"
                    + @"private static void CleanupAllKnownMarkers",
                RegexOptions.Singleline);
            Assert.That(completeFinish.Success, Is.True);
            string finishBody = completeFinish.Groups["body"].Value;
            int endConsoleIndex = finishBody.IndexOf(
                "TryEndConsoleCapture",
                StringComparison.Ordinal);
            int reportWriteIndex = finishBody.IndexOf(
                "File.WriteAllText(ReportPath",
                StringComparison.Ordinal);
            int summaryLogIndex = finishBody.IndexOf(
                "Debug.Log(summary)",
                StringComparison.Ordinal);
            Assert.That(endConsoleIndex, Is.GreaterThanOrEqualTo(0));
            Assert.That(reportWriteIndex, Is.GreaterThan(endConsoleIndex));
            Assert.That(summaryLogIndex, Is.GreaterThan(reportWriteIndex));

            Match startTarget = Regex.Match(
                facade.Text,
                @"private static void StartCurrentTarget\(AcceptanceState state\)"
                    + @"(?<body>.*?)"
                    + @"private static void RequestResolutionMatrix\(\)",
                RegexOptions.Singleline);
            Assert.That(startTarget.Success, Is.True);
            Assert.That(
                Regex.Matches(startTarget.Groups["body"].Value, "CaptureCurrent").Count,
                Is.EqualTo(1));
            Assert.That(
                facade.Text,
                Does.Contain("internal static bool IsPersistenceCoordinatorActive"));
        }

        [Test]
        public void FinalChildVerifiersDoNotCaptureNestedPersistenceSnapshots()
        {
            SourceFile[] childVerifiers =
            {
                SourceBySuffixIncludingEditor(
                    "Views/UI/Editor/DungeonResolutionPlayModeVerifier.cs"),
                SourceBySuffixIncludingEditor(
                    "Infrastructure/Editor/DungeonFullWorldRoundTripPlayModeFacade.cs"),
                SourceBySuffixIncludingEditor(
                    "ServiceRooms/Editor/ServiceRoomVisualValidationFacade.cs"),
                SourceBySuffixIncludingEditor(
                    "UI/Editor/CharacterSummaryMedicalUiMatrixPlayModeVerifier.cs")
            };

            foreach (SourceFile verifier in childVerifiers)
            {
                Assert.That(
                    Regex.Matches(verifier.Text, "CaptureCurrent").Count,
                    Is.EqualTo(1),
                    verifier.RelativePath);
                Assert.That(
                    verifier.Text,
                    Does.Match(
                        @"if\s*\(\s*!DungeonFinalPlayModeAcceptanceRequestFacade"
                        + @"\s*\.IsPersistenceCoordinatorActive\s*\)\s*\{"
                        + @"[\s\S]*?CaptureCurrent\s*\("),
                    verifier.RelativePath
                        + " must retain standalone capture while skipping nested final runs.");
            }
        }

        [Test]
        public void ResolutionVerifierCommitsPartyBeforeGameplayHudChecks()
        {
            SourceFile verifier = SourceBySuffixIncludingEditor(
                "Views/UI/Editor/DungeonResolutionPlayModeVerifier.cs");
            Match ensurePlayableRun = Regex.Match(
                verifier.Text,
                @"private IEnumerator EnsurePlayableRun\(\)"
                    + @"(?<body>.*?)"
                    + @"private void VerifyGameplayHud",
                RegexOptions.Singleline);

            Assert.That(ensurePlayableRun.Success, Is.True);
            Assert.That(
                ensurePlayableRun.Groups["body"].Value,
                Does.Contain(
                    "StartPartyPreparationPlayModeVerifier.RunFastCommitForDebug()"));
            Assert.That(
                ensurePlayableRun.Groups["body"].Value,
                Does.Contain("ownerManager?.CurrentOwnerActor != null"));
            Assert.That(
                ensurePlayableRun.Groups["body"].Value,
                Does.Contain("!ownerSelectionVisible"));
        }

        private static string ReadYamlObject(string sceneText, long fileId)
        {
            Match match = Regex.Match(
                sceneText,
                $@"--- !u!\d+ &{fileId}\r?\n.*?(?=\r?\n--- !u!|\z)",
                RegexOptions.Singleline);
            Assert.That(match.Success, Is.True, $"Scene object {fileId} was not found.");
            return match.Value;
        }

        private static int Count(IEnumerable<SourceFile> sources, Regex pattern)
        {
            return sources.Sum(source => pattern.Matches(source.Text).Count);
        }

        private static void AssertApprovedOccurrences(
            IEnumerable<SourceFile> sources,
            Regex pattern,
            IReadOnlyDictionary<string, int> approvedMaximums,
            string policy)
        {
            List<string> offenders = sources
                .Select(source => new
                {
                    source.RelativePath,
                    Count = pattern.Matches(source.Text).Count,
                })
                .Where(match => match.Count > 0
                    && (!approvedMaximums.TryGetValue(match.RelativePath, out int maximum)
                        || match.Count > maximum))
                .Select(match => approvedMaximums.TryGetValue(match.RelativePath, out int maximum)
                    ? $"{match.RelativePath}: {match.Count} occurrences, approved maximum {maximum}"
                    : $"{match.RelativePath}: {match.Count} unapproved occurrences")
                .ToList();

            Assert.That(
                offenders,
                Is.Empty,
                policy + "\n" + string.Join("\n", offenders));
        }

        private static SourceFile SourceBySuffix(string suffix)
        {
            return ProductSources().Single(source =>
                source.RelativePath.EndsWith(
                    suffix,
                    StringComparison.Ordinal));
        }

        private static SourceFile SourceBySuffixIncludingEditor(string suffix)
        {
            string scriptsRoot = Path.GetFullPath(Path.Combine(Application.dataPath, "Scripts"));
            return Directory
                .EnumerateFiles(scriptsRoot, "*.cs", SearchOption.AllDirectories)
                .Select(path => new SourceFile(scriptsRoot, path))
                .Single(source => source.RelativePath.EndsWith(
                    suffix,
                    StringComparison.Ordinal));
        }

        private static SourceFile SourceByAssetsRelativePath(string relativePath)
        {
            string assetsRoot = Path.GetFullPath(Application.dataPath);
            string path = Path.GetFullPath(Path.Combine(
                assetsRoot,
                relativePath.Replace('/', Path.DirectorySeparatorChar)));
            return new SourceFile(assetsRoot, path);
        }

        private static IReadOnlyList<SourceFile> ProductSources()
        {
            string scriptsRoot = Path.GetFullPath(Path.Combine(Application.dataPath, "Scripts"));
            return Directory
                .EnumerateFiles(scriptsRoot, "*.cs", SearchOption.AllDirectories)
                .Where(path => !HasDirectorySegment(path, "Editor"))
                .Select(path => new SourceFile(scriptsRoot, path))
                .ToArray();
        }

        private static bool HasDirectorySegment(string path, string segment)
        {
            string marker = Path.DirectorySeparatorChar + segment + Path.DirectorySeparatorChar;
            return path.IndexOf(marker, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static string NormalizeAssemblyReference(string reference)
        {
            const string GuidPrefix = "GUID:";
            string normalized = reference?.Trim() ?? string.Empty;
            return normalized.StartsWith(GuidPrefix, StringComparison.Ordinal)
                ? normalized.Substring(GuidPrefix.Length)
                : normalized;
        }

        private static string NormalizeRuntimePath(string path) =>
            (path ?? string.Empty).Replace('\\', '/').Trim();

        private static bool IsPresentationSource(string relativePath)
        {
            string normalized = NormalizeRuntimePath(relativePath);
            return normalized.StartsWith("Views/", StringComparison.Ordinal)
                || string.Equals(
                    normalized,
                    "Services/Offense/Strategic/OffenseWorldMapPanelStrategic.cs",
                    StringComparison.Ordinal);
        }

        private static void AssertAssemblyGraphAcyclic(
            IReadOnlyDictionary<string, AsmdefSource> assemblies)
        {
            Dictionary<string, int> visitState =
                new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (string assemblyName in assemblies.Keys)
            {
                VisitAssembly(assemblyName, assemblies, visitState, new List<string>());
            }
        }

        private static void VisitAssembly(
            string assemblyName,
            IReadOnlyDictionary<string, AsmdefSource> assemblies,
            IDictionary<string, int> visitState,
            IList<string> path)
        {
            if (visitState.TryGetValue(assemblyName, out int state))
            {
                if (state == 1)
                {
                    Assert.Fail(
                        "Assembly dependency cycle: "
                        + string.Join(" -> ", path.Concat(new[] { assemblyName })));
                }

                return;
            }

            visitState[assemblyName] = 1;
            path.Add(assemblyName);
            foreach (string reference in assemblies[assemblyName].Definition.references
                         ?? Array.Empty<string>())
            {
                string referencedName = NormalizeAssemblyReference(reference);
                if (assemblies.ContainsKey(referencedName))
                {
                    VisitAssembly(referencedName, assemblies, visitState, path);
                }
            }

            path.RemoveAt(path.Count - 1);
            visitState[assemblyName] = 2;
        }

        [Serializable]
        private sealed class AsmdefDefinition
        {
            public string name = string.Empty;
            public string rootNamespace = string.Empty;
            public string[] references = Array.Empty<string>();
        }

        [Serializable]
        private sealed class ArchitectureMetricsDocument
        {
            public int schemaVersion;
            public int oversizedTypeCount;
            public List<string> oversizedTypes = new();
        }

        [Serializable]
        private sealed class ArchitectureMetricsBaseline
        {
            public int schemaVersion;
            public int maxOversizedType;
        }

        private sealed class AsmdefSource
        {
            public AsmdefSource(string path)
            {
                Path = path;
                Definition = JsonUtility.FromJson<AsmdefDefinition>(File.ReadAllText(path))
                    ?? throw new InvalidOperationException($"Invalid asmdef JSON: {path}");
            }

            public string Path { get; }
            public AsmdefDefinition Definition { get; }
        }

        private sealed class SourceFile
        {
            public SourceFile(string root, string path)
            {
                RelativePath = path
                    .Substring(root.Length)
                    .TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                    .Replace(Path.DirectorySeparatorChar, '/');
                Text = File.ReadAllText(path);
                LineCount = Text.Length == 0
                    ? 0
                    : Text.Count(character => character == '\n') + 1;
            }

            public string RelativePath { get; }
            public string Text { get; }
            public int LineCount { get; }
        }
    }
}
