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
        private const int MaximumProductFileLines = 2169;
        private const int MaximumSceneSearches = 0;
        private const int MaximumResourcesLoads = 1;
        private const int MaximumDirectTimeAccesses = 8;
        private const int MaximumDirectRandomAccesses = 0;
        private const int MaximumMutableStaticDeclarations = 38;
        private const int MaximumEventObserverReferences = 0;

        private static readonly Regex StaticActiveAccessor = new Regex(
            @"\bstatic\s+[^\r\n;=]+\s+Active\s*(?:\{|=>)",
            RegexOptions.Compiled);

        private static readonly Regex SceneSearch = new Regex(
            @"\b(?:(?:UnityEngine\.)?Object\.)?Find(?:First|Any)?Object[s]?ByType"
            + @"|\bGameObject\.Find\b",
            RegexOptions.Compiled);

        private static readonly Regex MutableStaticDeclaration = new Regex(
            @"^\s*(?:public|private|protected|internal)?\s*static\s+"
            + @"(?!readonly\b|const\b)[^\r\n\(=;]+\s+\w+\s*(?:=(?!>)|;)",
            RegexOptions.Compiled | RegexOptions.Multiline);

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

            Assert.That(Count(sources, SceneSearch), Is.LessThanOrEqualTo(MaximumSceneSearches));
            Assert.That(
                Count(sources, new Regex(@"\bResources\.Load", RegexOptions.Compiled)),
                Is.LessThanOrEqualTo(MaximumResourcesLoads));
            Assert.That(
                Count(sources, new Regex(@"\bResources\.FindObjectsOfTypeAll", RegexOptions.Compiled)),
                Is.Zero);
            Assert.That(
                Count(sources, new Regex(@"\bTime\.", RegexOptions.Compiled)),
                Is.LessThanOrEqualTo(MaximumDirectTimeAccesses));
            Assert.That(
                Count(sources, new Regex(
                    @"\b(?:UnityEngine\.)?Random\.",
                    RegexOptions.Compiled)),
                Is.LessThanOrEqualTo(MaximumDirectRandomAccesses));
            Assert.That(
                Count(sources, MutableStaticDeclaration),
                Is.LessThanOrEqualTo(MaximumMutableStaticDeclarations));
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
                "Runtime scripts must live under Models, Views, Controllers, or Services.");
            Assert.That(
                Directory.GetFiles(scriptsRoot, "*.asmdef", SearchOption.TopDirectoryOnly),
                Is.Empty,
                "Assemblies should belong to an MVC top-level folder.");
        }

        [Test]
        public void ProductGodObjectsStayBelowRatchetLimit()
        {
            SourceFile[] offenders = ProductSources()
                .Where(source => source.LineCount > MaximumProductFileLines)
                .OrderByDescending(source => source.LineCount)
                .ToArray();

            Assert.That(
                offenders,
                Is.Empty,
                $"Product files must stay at or below {MaximumProductFileLines} lines:\n"
                + string.Join(
                    "\n",
                    offenders.Select(source =>
                        $"{source.RelativePath}: {source.LineCount}")));
        }

        [Test]
        public void ProductAssemblyGraphIsCompleteAcyclicAndLayered()
        {
            IReadOnlyDictionary<string, int> expectedRanks =
                new Dictionary<string, int>(StringComparer.Ordinal)
                {
                    ["DungeonStory.Foundation"] = 0,
                    ["DungeonStory.World"] = 1,
                    ["DungeonStory.Characters"] = 2,
                    ["DungeonStory.Items"] = 2,
                    ["DungeonStory.Buildings"] = 2,
                    ["DungeonStory.Work"] = 3,
                    ["DungeonStory.Rooms"] = 3,
                    ["DungeonStory.Combat"] = 3,
                    ["DungeonStory.Survival"] = 3,
                    ["DungeonStory.Wildlife"] = 3,
                    ["DungeonStory.AI"] = 4,
                    ["DungeonStory.Invasion"] = 4,
                    ["DungeonStory.Offense"] = 4,
                    ["DungeonStory.Presentation"] = 5,
                    ["DungeonStory.Infrastructure"] = 5
                };
            string scriptsRoot = Path.GetFullPath(
                Path.Combine(Application.dataPath, "Scripts"));
            AsmdefSource[] assemblies = Directory
                .EnumerateFiles(scriptsRoot, "*.asmdef", SearchOption.AllDirectories)
                .Select(path => new AsmdefSource(path))
                .Where(source => source.Definition.name.StartsWith(
                    "DungeonStory.",
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
                "The V15 product assembly set changed without updating its dependency policy.");

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
        public void SaveRootDefaultsToV15()
        {
            SourceFile saveService = SourceBySuffix(
                "Infrastructure/Core/InfrastructureSavePrimitives.cs");

            Assert.That(
                saveService.Text,
                Does.Match(@"CurrentVersion\s*=\s*15\s*;"));
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
            Assert.That(body, Does.Contain("CurrentVersion = 15"));
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
                "Operation/OperatingDaySettlement.cs");
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
                "Character/AI/LocalLlmRequestQueue.cs");

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
                "Infrastructure/InvasionThreatRuntimeProvider.cs");
            SourceFile featureRuntimes = SourceBySuffix(
                "Infrastructure/RuntimePanelProviders.cs");
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
            SourceFile filth = SourceBySuffix(
                "Survival/WorldFilthRuntime.cs");

            Assert.That(buildable.Text, Does.Contain("IGameClock gameClock"));
            Assert.That(
                buildable.Text,
                Does.Contain("visitReservations[visitor] = Now"));
            Assert.That(buildable.Text, Does.Not.Match(@"\bTime\."));
            Assert.That(filth.Text, Does.Contain("gameClock: gameClock"));
        }

        [Test]
        public void SaveUiUsesClockAndTimeScalePorts()
        {
            SourceFile saveUi = SourceBySuffix("UI/DungeonSaveUi.cs");
            SourceFile clock = SourceBySuffix("Foundation/Time/GameClock.cs");

            Assert.That(saveUi.Text, Does.Contain("IUiClock uiClock"));
            Assert.That(
                saveUi.Text,
                Does.Contain("IGameTimeScaleController timeScaleController"));
            Assert.That(
                saveUi.Text,
                Does.Not.Contain("IDungeonSceneComponentQuery"));
            Assert.That(saveUi.Text, Does.Not.Match(@"\bTime\."));
            Assert.That(
                clock.Text,
                Does.Contain("interface IGameTimeScaleController"));
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
                SourceBySuffix("Run/DungeonRunSaveData.cs").Text,
                Does.Contain("class DungeonRunFlowSaveData"));
            Assert.That(
                SourceBySuffix("Meta/DungeonMetaProgressionSaveData.cs").Text,
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
        public void FoundationRegistrationSuppliesRandomSeedExplicitly()
        {
            SourceFile registration = SourceBySuffix(
                "Infrastructure/Registration/DungeonFoundationRegistration.cs");

            Assert.That(
                registration.Text,
                Does.Not.Contain("Register<RandomStreamProvider>"));
            Assert.That(
                registration.Text,
                Does.Match(@"new\s+RandomStreamProvider\s*\(\s*rootSeed\s*:\s*\d+\s*\)"));
        }

        [Test]
        public void RandomStreamsHaveAnIndependentV15SaveSection()
        {
            SourceFile provider = SourceBySuffix(
                "Foundation/Random/RandomStreamProvider.cs");
            SourceFile runVariables = SourceBySuffix(
                "Run/RunVariableSystem.cs");
            SourceFile saveSection = SourceBySuffix(
                "Infrastructure/Save/RandomStreamSaveSection.cs");
            SourceFile registration = SourceBySuffix(
                "Infrastructure/Registration/DungeonSaveRegistration.cs");

            Assert.That(provider.Text, Does.Contain("CaptureStates()"));
            Assert.That(provider.Text, Does.Contain("RestoreStates("));
            Assert.That(
                provider.Text,
                Does.Contain("pair.Value.Restore(CombineSeed"));
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
                Does.Contain("new[] { RunVariableSaveSection.Id }"));
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
            Assert.That(world.Text, Does.Contain("public sealed class GridMoveStep"));
            Assert.That(grid.Text, Does.Not.Contain("public enum GridLayer"));
            Assert.That(grid.Text, Does.Not.Contain("public enum GridMoveType"));
            Assert.That(grid.Text, Does.Not.Contain("public interface IGridOccupant"));
            Assert.That(grid.Text, Does.Not.Contain("public class GridMoveStep"));
            Assert.That(
                survival.Text,
                Does.Not.Contain("public enum GridCellTerrainType"));
        }

        [Test]
        public void CharacterAssemblyOwnsSharedCharacterPrimitives()
        {
            SourceFile primitives = SourceBySuffix(
                "Characters/CharacterPrimitives.cs");
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
            Assert.That(primitives.Text, Does.Contain("public enum CharacterStatType"));
            Assert.That(primitives.Text, Does.Contain("public enum CharacterCondition"));
            Assert.That(primitives.Text, Does.Contain("public enum CharacterLifecycleState"));
            Assert.That(modelData.Text, Does.Not.Contain("public enum CharacterStatType"));
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
                "Character/Work/WorkTypeCatalog.cs");
            SourceFile executionRegistry = SourceBySuffix(
                "Character/Work/WorkExecutionRegistry.cs");
            SourceFile executor = SourceBySuffix(
                "Character/Work/WorkTaskExecutor.cs");
            SourceFile workAmount = SourceBySuffix(
                "Character/Work/WorkAmountSystem.cs");
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
                "Character/AI/Action/AIWait.cs");
            SourceFile aiHaul = SourceBySuffix("Character/AI/Action/AIHaul.cs");
            SourceFile aiHunt = SourceBySuffix("Character/AI/Action/AIHunt.cs");
            SourceFile aiRescue = SourceBySuffix("Character/AI/Action/AIRescue.cs");
            SourceFile considerationWorkNeed = SourceBySuffix(
                "Character/AI/Consideration/ConsiderationWorkNeed.cs");
            SourceFile combatLoadout = SourceBySuffix(
                "Combat/CombatLoadoutPreparationRuntime.cs");
            SourceFile staffDiscontent = SourceBySuffix(
                "Character/Work/StaffDiscontentSystem.cs");
            SourceFile deprivation = SourceBySuffix(
                "Survival/CharacterDeprivationRuntime.cs");
            SourceFile defenseUi = SourceBySuffix(
                "UI/DefenseFeatureSurfacePresenter.cs");
            SourceFile researchUi = SourceBySuffix(
                "UI/ResearchFeatureSurfacePresenter.cs");
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
            SourceFile blueprintResearch = SourceBySuffix(
                "Research/BlueprintResearchSystem.cs");
            SourceFile characterActor = SourceBySuffix(
                "Character/Core/CharacterActor.cs");
            SourceFile characterActivity = SourceBySuffix(
                "Character/Core/CharacterActivityEvent.cs");
            SourceFile characterStats = SourceBySuffix(
                "Character/Core/CharacterStats.cs");
            SourceFile characterModelData = SourceBySuffix(
                "Character/SO/CharacterModelData.cs");
            SourceFile characterSo = SourceBySuffix(
                "Character/SO/CharacterSO.cs");
            SourceFile equipmentCrafting = SourceBySuffix(
                "Combat/Buildings/EquipmentCraftingBuildingAbilityHandler.cs");
            SourceFile abilityRescue = SourceBySuffix(
                "Combat/AbilityRescue.cs");
            SourceFile workTargetCandidate = SourceBySuffix(
                "Character/Work/WorkTargetCandidate.cs");
            SourceFile workTargetSelector = SourceBySuffix(
                "Character/Work/WorkTargetSelector.cs");
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
                "Codex/CodexTextFormatter.cs");
            SourceFile cleanWork = SourceBySuffix(
                "Survival/Work/CleanWorkExecutionHandler.cs");
            SourceFile survivalFood = SourceBySuffix(
                "Survival/SurvivalFoodRuntime.cs");
            SourceFile survivalFacilityUtility = SourceBySuffix(
                "Survival/SurvivalFacilityUtility.cs");
            SourceFile researchWork = SourceBySuffix(
                "Research/Work/ResearchWorkExecutionHandler.cs");
            SourceFile wildlifeModels = SourceBySuffix(
                "Wildlife/WildlifeModels.cs");
            SourceFile buildableObject = SourceBySuffix(
                "Buildings/BuildableObject.cs");
            SourceFile buildingSo = SourceBySuffix("Buildings/SO/BuildingSO.cs");
            SourceFile facility = SourceBySuffix("Buildings/Facility.cs");
            SourceFile shop = SourceBySuffix("Buildings/Shop.cs");
            SourceFile roomEnvironment = SourceBySuffix(
                "Rooms/RoomEnvironment.cs");
            SourceFile roomEnvironmentExperience = SourceBySuffix(
                "Rooms/RoomEnvironmentExperience.cs");
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
            Assert.That(catalog.Text, Does.Contain("public WorkTypeId WorkTypeId"));
            Assert.That(catalog.Text, Does.Not.Contain("public FacilityWorkType Type"));
            Assert.That(
                catalog.Text,
                Does.Contain("public static WorkTypeDefinition Register(\n        WorkTypeId id"));
            Assert.That(
                catalog.Text,
                Does.Not.Contain("public WorkTypeDefinition(\n        string id,\n        FacilityWorkType"));
            Assert.That(
                catalog.Text,
                Does.Not.Contain("public WorkTypeDefinition(\n        WorkTypeId id,\n        FacilityWorkType"));
            Assert.That(
                catalog.Text,
                Does.Contain("RegisterBuiltIn(BuiltInWorkTypeIds.Operate"));
            Assert.That(
                catalog.Text,
                Does.Not.Match(@"RegisterBuiltIn\(\s*""work:"));
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
                Does.Contain("float CalculateWorkPerSecond(\n        CharacterActor actor,\n        BuildableObject target,\n        WorkTypeId workTypeId"));
            Assert.That(
                executionRegistry.Text,
                Does.Not.Contain("FacilityWorkType legacyWorkType,\n        float environmentDurationMultiplier"));
            Assert.That(
                executionRegistry.Text,
                Does.Contain("bool IsAvailable(\n        WorkTypeId workTypeId"));
            Assert.That(
                executionRegistry.Text,
                Does.Not.Contain("bool IsAvailable(\n        FacilityWorkType"));
            Assert.That(
                executionRegistry.Text,
                Does.Not.Contain("provider.IsAvailable(definition.Type"));
            Assert.That(
                executionRegistry.Text,
                Does.Contain("float GetAdditionalUrgency(\n        WorkTypeId workTypeId"));
            Assert.That(
                executionRegistry.Text,
                Does.Contain("float GetUrgency(\n        WorkTypeId workTypeId"));
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
                Does.Contain("actor.GetWorkSpeedMultiplier(definition.WorkTypeId"));
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
                Does.Contain("workAmountCalculator.CalculateWorkPerSecond(\n                    actor,\n                    target,\n                    workTypeId"));
            Assert.That(
                workAmount.Text,
                Does.Contain("bool TryGetOrderFor(BuildableObject target, WorkTypeId workTypeId"));
            Assert.That(
                workAmount.Text,
                Does.Contain("bool ApplyWork(CharacterActor worker, BuildableObject target, WorkTypeId workTypeId"));
            Assert.That(
                workAmount.Text,
                Does.Contain("public string workTypeId"));
            Assert.That(
                workAmount.Text,
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
                Does.Contain("workOrderRuntime.ApplyWork(\n                    actor,\n                    target,\n                    workTypeId"));
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
                Does.Contain("TryGetOrderFor(site, BuiltInWorkTypeIds.Construct"));
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
                codexFormatter.Text,
                Does.Contain("public static string FormatWorkTypes(IEnumerable<WorkTypeId> workTypeIds)"));
            Assert.That(
                codexFormatter.Text,
                Does.Not.Contain("public static string FormatWorkTypes(FacilityWorkType"));
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
                Does.Contain("public BuildingAbilityWorkContext(\n        CharacterActor actor,\n        BuildableObject building,\n        WorkTypeId workTypeId"));
            Assert.That(
                buildingAbilityHandlers.Text,
                Does.Not.Contain("public BuildingAbilityWorkContext(\n        CharacterActor actor,\n        BuildableObject building,\n        FacilityWorkType"));
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
                Does.Contain("public bool CanAssignWork(WorkTypeId workTypeId"));
            Assert.That(
                buildableObject.Text,
                Does.Not.Contain("public bool CanAssignWork(FacilityWorkType"));
            Assert.That(
                buildableObject.Text,
                Does.Contain("public FacilityAssignmentStatus GetWorkAssignmentStatus(WorkTypeId workTypeId)"));
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
                Does.Contain("GetRequiredWork(BuiltInWorkTypeIds.Repair"));
            Assert.That(
                cleanWork.Text,
                Does.Contain("GetRequiredWork(BuiltInWorkTypeIds.Clean"));
            Assert.That(
                researchWork.Text,
                Does.Contain("GetRequiredWork(BuiltInWorkTypeIds.Research"));
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
                Does.Contain("TryGetBestWorkCandidate(\n        WorkTypeId requestedWorkTypeId"));
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
                Does.Contain("public void StartWorking(\n        WorkTypeId requestedWorkTypeId"));
            Assert.That(
                abilityWork.Text,
                Does.Not.Contain("public void StartWorking(\n        FacilityWorkType"));
            Assert.That(
                abilityWork.Text,
                Does.Contain("public bool TryAssignWorkTarget(\n        BuildableObject target,\n        WorkTypeId requestedWorkTypeId"));
            Assert.That(
                abilityWork.Text,
                Does.Not.Contain("public bool TryAssignWorkTarget(\n        BuildableObject target,\n        FacilityWorkType"));
            Assert.That(
                abilityWork.Text,
                Does.Contain("public bool TrySetPriorityWorkTarget(\n        BuildableObject building,\n        WorkTypeId preferredWorkTypeId"));
            Assert.That(
                abilityWork.Text,
                Does.Not.Contain("public bool TrySetPriorityWorkTarget(\n        BuildableObject building,\n        FacilityWorkType"));
            Assert.That(
                workCommandHandler.Text,
                Does.Contain("public bool TrySetPriorityWorkTarget(\n        BuildableObject building,\n        WorkTypeId preferredWorkTypeId"));
            Assert.That(
                workCommandHandler.Text,
                Does.Not.Contain("public bool TrySetPriorityWorkTarget(\n        BuildableObject building,\n        FacilityWorkType"));
            Assert.That(
                workTargetSelector.Text,
                Does.Contain("TryAssignAnyWork(GridPathSearchResult searchResult"));
            Assert.That(
                workTargetSelector.Text,
                Does.Contain("TryAssignWork(\n        GridPathSearchResult searchResult,\n        WorkTypeId requestedWorkTypeId"));
            Assert.That(
                workTargetSelector.Text,
                Does.Not.Contain("public bool TryAssignWork(\n        GridPathSearchResult searchResult = null,\n        FacilityWorkType"));
            Assert.That(
                workTargetSelector.Text,
                Does.Contain("HasUrgentAvailableWork(\n        GridPathSearchResult searchResult,\n        WorkTypeId requestedWorkTypeId"));
            Assert.That(
                workTargetSelector.Text,
                Does.Not.Contain("public bool HasUrgentAvailableWork(\n        GridPathSearchResult searchResult,\n        FacilityWorkType"));
            Assert.That(
                workTargetSelector.Text,
                Does.Contain("TryGetBestCandidate(\n        WorkTypeId requestedWorkTypeId"));
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
                Does.Contain("work.GetAnyWorkUtilityScore(searchResult"));
            Assert.That(
                aiWorkAction.Text,
                Does.Not.Contain("work.GetWorkUtilityScore(FacilityWorkType.None"));
            Assert.That(
                aiWorkAction.Text,
                Does.Contain("work.CanStartWorkAction(workTypeId"));
            Assert.That(
                aiWorkAction.Text,
                Does.Contain("work.CanStartAnyWorkAction(searchResult"));
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
                Does.Contain("work.GetAnyWorkUtilityScore(searchResult"));
            Assert.That(
                aiWaitAction.Text,
                Does.Not.Contain("work.GetWorkUtilityScore(FacilityWorkType.None"));
            Assert.That(
                considerationWorkNeed.Text,
                Does.Contain("work.GetAnyWorkUtilityScore(searchResult"));
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
                Does.Contain("work.TryGetBestWorkCandidate(requestedWorkTypeId"));
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
                Does.Contain("public void RecordWork(\n        WorkTypeId workTypeId"));
            Assert.That(
                aiMemory.Text,
                Does.Not.Contain("public FacilityWorkType workType"));
            Assert.That(
                aiMemory.Text,
                Does.Not.Contain("public void RecordWork(\n        FacilityWorkType"));
            Assert.That(
                characterActivity.Text,
                Does.Contain("public static CharacterActivityEvent Work(\n        WorkTypeId workTypeId"));
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
                Does.Not.Contain("WorkPriorities.IsEnabled(FacilityWorkType.Research"));
            Assert.That(
                researchUi.Text,
                Does.Not.Contain("WorkPriorities.GetPriority(FacilityWorkType.Research"));
            Assert.That(
                researchUi.Text,
                Does.Contain("WorkPriorities.IsEnabled(BuiltInWorkTypeIds.Research"));
            Assert.That(
                researchUi.Text,
                Does.Contain("WorkPriorities.GetPriority(BuiltInWorkTypeIds.Research"));
            Assert.That(
                researchUi.Text,
                Does.Contain("work.IsAssignedWork(BuiltInWorkTypeIds.Research"));
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
                Does.Contain("void RequestOneWorkerToReplanFor(WorkTypeId workTypeId"));
            Assert.That(
                workforceReplan.Text,
                Does.Not.Contain("RequestOneWorkerToReplanFor(FacilityWorkType"));
            Assert.That(
                blueprintResearch.Text,
                Does.Not.Contain("RequestOneWorkerToReplanFor(FacilityWorkType.Research"));
            Assert.That(
                blueprintResearch.Text,
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
                Does.Contain("GetWorkModifierOnly(WorkTypeId workTypeId)"));
            Assert.That(
                characterModelData.Text,
                Does.Contain("GetWorkSpeedMultiplier(WorkTypeId workTypeId)"));
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
                characterModelData.Text,
                Does.Not.Contain("public FacilityWorkType preferredWorkTypes"));
            Assert.That(
                characterModelData.Text,
                Does.Not.Contain("public FacilityWorkType dislikedWorkTypes"));
            Assert.That(
                characterModelData.Text,
                Does.Contain("public IEnumerable<WorkTypeId> PreferredWorkTypeIds"));
            Assert.That(
                characterModelData.Text,
                Does.Contain("public IEnumerable<WorkTypeId> DislikedWorkTypeIds"));
            Assert.That(
                equipmentCrafting.Text,
                Does.Contain("GetWorkSpeedMultiplier(BuiltInWorkTypeIds.Craft"));
            Assert.That(
                equipmentCrafting.Text,
                Does.Not.Contain("GetWorkSpeedMultiplier(FacilityWorkType.Craft"));
            Assert.That(
                abilityRescue.Text,
                Does.Contain("GetWorkSpeedMultiplier(BuiltInWorkTypeIds.Treat"));
            Assert.That(
                abilityRescue.Text,
                Does.Not.Contain("GetWorkSpeedMultiplier(FacilityWorkType.Treat"));
            Assert.That(
                blueprintResearch.Text,
                Does.Contain("GetWorkSpeedMultiplier(BuiltInWorkTypeIds.Research"));
            Assert.That(
                blueprintResearch.Text,
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
                Does.Contain("foreach (WorkTypeDefinition definition in WorkTypeCatalog.Enumerate(supportedTypes))"));
            Assert.That(
                workTargetSelector.Text,
                Does.Contain("actor.GetWorkPreferenceScore(workTypeId)"));
            Assert.That(
                workTargetSelector.Text,
                Does.Contain("actor.GetWorkSpeedMultiplier(workTypeId)"));
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
                workTargetSelector.Text,
                Does.Not.Contain("priorities.GetPriority(workType)"));
            Assert.That(
                workTargetSelector.Text,
                Does.Contain("priorities.GetPriority(workTypeId)"));
            Assert.That(
                workTargetSelector.Text,
                Does.Contain("workPolicyRegistry.IsAvailable(\n                    workTypeId"));
            Assert.That(
                workTargetSelector.Text,
                Does.Contain("workPolicyRegistry?.GetAdditionalUrgency(workTypeId"));
            Assert.That(
                facility.Text,
                Does.Not.Contain("WorkTaskCatalog.GetSingleTypes"));
            Assert.That(
                facility.Text,
                Does.Contain("WorkTypeCatalog.Enumerate("));
            Assert.That(
                shop.Text,
                Does.Not.Contain("WorkTaskCatalog.GetSingleTypes"));
            Assert.That(
                shop.Text,
                Does.Contain("WorkTypeCatalog.Enumerate("));
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
                Does.Contain("WorkTypeCatalog.Enumerate("));
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
                "Buildings/SO/SaleItem.cs");

            Assert.That(
                File.Exists(Path.Combine(
                    Application.dataPath,
                    "Scripts/Models/Buildings/Core/DungeonStory.Buildings.asmdef")),
                Is.True);
            Assert.That(primitives.Text, Does.Contain("public enum BuildingCategory"));
            Assert.That(primitives.Text, Does.Contain("public enum FacilityRole"));
            Assert.That(primitives.Text, Does.Contain("internal enum FacilityWorkType"));
            Assert.That(primitives.Text, Does.Not.Contain("public enum FacilityWorkType"));
            Assert.That(
                SourceBySuffix("Buildings/Core/BuildingAssemblyInfo.cs").Text,
                Does.Contain("InternalsVisibleTo(\"Assembly-CSharp\")"));
            Assert.That(
                SourceBySuffix("Buildings/Core/BuildingAssemblyInfo.cs").Text,
                Does.Contain("InternalsVisibleTo(\"Assembly-CSharp-Editor\")"));
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
        public void RoomAssemblyOwnsStableRoleCatalog()
        {
            SourceFile roleCatalog = SourceBySuffix(
                "Rooms/Core/RoomRole.cs");
            SourceFile roomEnvironment = SourceBySuffix(
                "Rooms/RoomEnvironment.cs");

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
        public void CombatAssemblyOwnsResolutionPrimitives()
        {
            SourceFile models = SourceBySuffix(
                "Combat/Core/CombatModels.cs");
            SourceFile weapons = SourceBySuffix(
                "Combat/Core/CombatWeaponPrimitives.cs");
            SourceFile definitions = SourceBySuffix(
                "Combat/CombatEquipmentDefinitions.cs");

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
        public void InvasionAssemblyOwnsPolicyAndThreatPrimitives()
        {
            SourceFile primitives = SourceBySuffix(
                "Invasion/Core/InvasionPrimitives.cs");
            SourceFile engagement = SourceBySuffix(
                "Invasion/DefenseEngagementModels.cs");
            SourceFile threat = SourceBySuffix(
                "Invasion/InvasionThreatSystem.cs");

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
                "UI/UITabIdentity.cs");

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
                primitives.Text,
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
                "Combat/CharacterCombatCommandModels.cs");
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
                "Meta/MetaProgressionRunResultServices.cs");
            SourceFile progressTracker = SourceBySuffix(
                "Meta/MetaRunProgressTracker.cs");
            SourceFile runtime = SourceBySuffix(
                "Meta/MetaProgressionSystem.cs");

            Assert.That(resultBuilder.Text, Does.Contain("IGameClock"));
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
                "Meta/MetaProgressionRunResultServices.cs");
            SourceFile panelFactory = SourceBySuffix(
                "Meta/RunResultPanelFactory.cs");
            SourceFile panel = SourceBySuffix(
                "Meta/RunResultPanel.cs");
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
        public void SettingsUiUsesCapturedRuntimeAndTimeScalePort()
        {
            SourceFile settings = SourceBySuffix(
                "UI/DungeonSettingsUi.cs");

            Assert.That(
                settings.Text,
                Does.Contain("DungeonSceneRuntimeReferences sceneReferences"));
            Assert.That(
                settings.Text,
                Does.Contain("IGameTimeScaleController timeScaleController"));
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
                Does.Contain("SceneUiBootstrapReferences runtimeReferences"));
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
                "UI/CharacterSummeryInfo.cs");
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
                characterSummary.Text,
                Does.Contain("ICharacterAiDiagnosticsQuery aiDiagnostics"));
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
                Does.Contain("IGameTimeScaleController timeScaleController"));
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
                "Character/Core/CharacterActorBridges.cs");
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
            Assert.That(actor.Text, Does.Contain("OnActorDestroyed()"));
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
                "Invasion/InvasionIntruderPlanner.cs");
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

            Assert.That(exterior.Text, Does.Contain("IGameClock gameClock"));
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
            SourceFile movement = SourceBySuffix(
                "Character/Ability/AbilityMove.cs");

            Assert.That(
                grid.Text,
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
            SourceFile lookAround = SourceBySuffix(
                "Character/AI/Action/AILookAround.cs");
            SourceFile wait = SourceBySuffix(
                "Character/AI/Action/AIWait.cs");
            SourceFile consideration = SourceBySuffix(
                "Character/AI/Consideration/ConsiderationRandom.cs");

            Assert.That(
                brain.Text,
                Does.Contain(".Get(\"character-ai\")"));
            Assert.That(
                lookAround.Text,
                Does.Contain("brain.NextRandomIndex"));
            Assert.That(
                lookAround.Text,
                Does.Not.Contain("OrderBy((_) => Random.value)"));
            Assert.That(
                wait.Text,
                Does.Contain("actor.Brain.NextRandom"));
            Assert.That(
                consideration.Text,
                Does.Contain("actor.Brain.NextRandom"));
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
                "Wildlife/WildlifeEcosystemRuntime.cs");
            SourceFile markerRegistry = SourceBySuffix(
                "Infrastructure/WildlifeHabitatMarkerRegistry.cs");
            SourceFile viewToggle = SourceBySuffix(
                "UI/WildlifeEcosystemViewToggleRuntime.cs");

            Assert.That(runtime.Text, Does.Contain("IRandomStreamProvider"));
            Assert.That(runtime.Text, Does.Contain("\"wildlife-ecosystem\""));
            Assert.That(runtime.Text, Does.Contain("IWildlifeHabitatMarkerQuery"));
            Assert.That(
                runtime.Text,
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
                "Character/Work/StaffWorkforceQueryService.cs",
                "Character/Work/WorkforceReplanService.cs",
                "Buildings/BuildingManagementSummaryQuery.cs",
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
                "Codex/CodexRecordSummaryQuery.cs",
                "Research/ResearchCraftingSummaryQuery.cs",
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
            SourceFile panel = SourceBySuffix("UI/P0FeatureSurfacePanel.cs");
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
            SourceFile panel = SourceBySuffix("UI/P0FeatureSurfacePanel.cs");
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
            SourceFile panel = SourceBySuffix("UI/P0FeatureSurfacePanel.cs");
            SourceFile presenter = SourceBySuffix(
                "UI/ResearchFeatureSurfacePresenter.cs");
            SourceFile presenterCatalog = SourceBySuffix(
                "UI/Core/PresentationPrimitives.cs");

            Assert.That(panel.Text, Does.Not.Contain("BuildResearch("));
            Assert.That(panel.Text, Does.Not.Contain("IBlueprintResearchRuntimeProvider"));
            Assert.That(panel.Text, Does.Not.Contain("IFacilityShopCatalog"));
            Assert.That(presenter.Text, Does.Contain("IResearchFeatureQueryService"));
            Assert.That(presenter.Text, Does.Contain("IResearchFeatureCommandService"));
            Assert.That(presenter.Text, Does.Contain("IBuildingWorldQuery"));
            Assert.That(presenter.Text, Does.Contain("IStaffWorkforceQueryService"));
            Assert.That(
                presenter.Text,
                Does.Not.Contain("IDungeonSceneComponentQuery"));
            Assert.That(presenterCatalog.Text, Does.Not.Contain("surface.BuildResearch"));
        }

        [Test]
        public void CodexFeatureOwnsQueryCommandAndPresentation()
        {
            SourceFile panel = SourceBySuffix("UI/P0FeatureSurfacePanel.cs");
            SourceFile presenter = SourceBySuffix(
                "UI/CodexFeatureSurfacePresenter.cs");
            SourceFile presenterCatalog = SourceBySuffix(
                "UI/Core/PresentationPrimitives.cs");

            Assert.That(panel.Text, Does.Not.Contain("ICodexRuntimeProvider"));
            Assert.That(presenter.Text, Does.Contain("ICodexFeatureQueryService"));
            Assert.That(presenter.Text, Does.Contain("ICodexFeatureCommandService"));
            Assert.That(presenter.Text, Does.Contain("ICodexRuntimeProvider"));
            Assert.That(presenter.Text, Does.Contain("IEventAlertRuntimeProvider"));
            Assert.That(presenter.Text, Does.Contain("IInvasionCombatReportRuntimeProvider"));
            Assert.That(presenter.Text, Does.Contain("IOffenseExpeditionRuntimeProvider"));
            Assert.That(presenter.Text, Does.Contain("IOperatingDaySettlementRuntimeProvider"));
            Assert.That(
                presenter.Text,
                Does.Not.Contain("IDungeonSceneComponentQuery"));
            Assert.That(
                presenterCatalog.Text,
                Does.Not.Contain("surface.BuildCodexAndHistory"));
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
            SourceFile panel = SourceBySuffix("UI/P0FeatureSurfacePanel.cs");
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
                "UI/OperationsFeatureSurfacePresenter.cs");
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
            SourceFile panel = SourceBySuffix("UI/P0FeatureSurfacePanel.cs");

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
            SourceFile bridges = SourceBySuffix(
                "Character/Core/CharacterActorBridges.cs");

            Assert.That(actor.Text, Does.Contain("CharacterActorRuntimeBridge"));
            Assert.That(actor.Text, Does.Contain("CharacterActorPresentationBridge"));
            Assert.That(actor.Text, Does.Not.Contain("registeredWithAiScheduler"));
            Assert.That(actor.Text, Does.Not.Contain("registeredWithWorldRegistry"));
            Assert.That(actor.Text, Does.Not.Contain("feedbackBubbleFactory;"));
            Assert.That(bridges.Text, Does.Contain("RegisterCharacter(actor)"));
            Assert.That(bridges.Text, Does.Contain("WorldCharacterNameplate.Ensure(actor, TmpKoreanFontService)"));
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
            Assert.That(wildlife.Text, Does.Contain("IRandomStreamProvider"));
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

        private static int Count(IEnumerable<SourceFile> sources, Regex pattern)
        {
            return sources.Sum(source => pattern.Matches(source.Text).Count);
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
