#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using DungeonStory.Foundation;
using DungeonStory.Foundation.Facts;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

public static class AnimalHusbandryOutcomeDebugScenarios
{
    public static void RunFromBatch()
    {
        string configured = Environment.GetEnvironmentVariable(
            "DUNGEON_ANIMAL_HUSBANDRY_OUTCOME_REPORT_PATH");
        if (string.IsNullOrWhiteSpace(configured))
            throw new InvalidOperationException(
                "DUNGEON_ANIMAL_HUSBANDRY_OUTCOME_REPORT_PATH is required.");
        string projectRoot = Directory.GetParent(Application.dataPath)?.FullName
            ?? throw new InvalidOperationException("Unity project root is unavailable.");
        string fullPath = Path.GetFullPath(Path.Combine(projectRoot, configured));
        string physicalReportPath = Path.ChangeExtension(fullPath, ".physical.json");
        string qaRoot = Path.GetFullPath(Path.Combine(projectRoot, "Artifacts/QA"));
        if (!fullPath.StartsWith(
                qaRoot + Path.DirectorySeparatorChar,
                StringComparison.OrdinalIgnoreCase)
            || File.Exists(fullPath)
            || File.Exists(physicalReportPath))
        {
            throw new InvalidOperationException(
                "Husbandry outcome report must be a create-only path under Artifacts/QA.");
        }
        if (!RunAll())
            throw new InvalidOperationException("Husbandry outcome scenarios failed.");
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)
            ?? throw new InvalidOperationException("Report directory is unavailable."));
        VerifyRuntimePhysicalEntries(physicalReportPath);
        WriteCreateOnly(
            fullPath,
            "ANIMAL_HUSBANDRY_OUTCOME=PASS\n"
            + "covered=AnimalHusbandryCommandResult,AnimalHusbandryWorkResult\n"
            + "forwardRecovery=commit-reject,save-roundtrip,retry-once\n"
            + "runtimeEntries=policy-rollback,no-op,partial-save-restore,tame,product,manure,slaughter\n"
            + "partialRoundTrip=incomplete-without-output,worker-contribution-restored,terminal-once\n"
            + "physicalChecks=product-stack,manure-stack,death,carcass,release,registry-removal\n"
            + "productionPhysicalPorts=PASS; raw=" + Path.GetFileName(physicalReportPath) + "\n"
            + "saveVersion=v6-exact;legacy=unsupported-no-migration\n"
            + "fullAuditClosure=false\n"
            + "humanApprovalClaimed=false\n"
            + "trainingEligible=false\n");
        Debug.Log("ANIMAL_HUSBANDRY_OUTCOME=PASS; report=" + fullPath);
    }

    private static void WriteCreateOnly(string path, string text)
    {
        using FileStream stream = new(path, FileMode.CreateNew, FileAccess.Write, FileShare.Read);
        using StreamWriter writer = new(stream, new System.Text.UTF8Encoding(false));
        writer.Write(text);
    }

    // Only the explicit batch entry adds this integration. The original RunAll
    // fault fixtures remain unchanged, including their explicit proxy ports.
    private static void VerifyRuntimePhysicalEntries(string reportPath)
    {
        using var runtime = new RuntimeEntryHarness(useProductionPhysicalPorts: true);
        var cases = new JArray();
        var report = new JObject
        {
            ["schema"] = "dungeonstory-e02-production-physical-fixture@1",
            ["passed"] = false,
            ["scope"] = "public husbandry owner with production item runtime/spawner/mass/carcass services",
            ["cases"] = cases,
            ["checkpoints"] = runtime.PhysicalCheckpoints,
            ["captureAndWildlifeRegistryAreTestPorts"] = true,
            ["timeEnvironmentAndActorPreparationAreFixtures"] = true,
            ["brainAiExecuted"] = false,
            ["wholeWorldRestoreExecuted"] = false,
            ["rowCurrentEvidenceComplete"] = false,
            ["fullAuditClosure"] = false,
            ["humanApprovalClaimed"] = false,
            ["trainingEligible"] = false
        };
        try
        {
            Run("policy", runtime.VerifyCommandRollbackAndNoOp);
            Run("partial-manure", runtime.VerifyPartialWorkRoundTrip);
            Run("tame", runtime.VerifyTaming);
            Run("product", runtime.VerifyProductForwardRecovery);
            Run("manure", runtime.VerifyManureReservationRollback);
            Run("slaughter", runtime.VerifySlaughter);
            Require(runtime.Probe.CommitCount(MigratedProducerOutcomeKind.AnimalHusbandryCommandResult) == 2
                    && runtime.Probe.CommitCount(MigratedProducerOutcomeKind.AnimalHusbandryWorkResult) == 5,
                "Production physical fixture did not retain exactly two commands and five work results.");
            report["commandCommits"] = 2;
            report["workCommits"] = 5;
            report["passed"] = true;
        }
        catch (Exception exception)
        {
            report["failure"] = exception.ToString();
            throw;
        }
        finally
        {
            WriteCreateOnly(reportPath, report.ToString(Formatting.Indented));
        }

        void Run(string id, Action execute)
        {
            execute();
            cases.Add(runtime.CapturePhysicalProof(id));
        }
    }

    public static IReadOnlyList<MigratedProducerOutcomeKind> CoveredKinds { get; } =
        new[]
        {
            MigratedProducerOutcomeKind.AnimalHusbandryCommandResult,
            MigratedProducerOutcomeKind.AnimalHusbandryWorkResult
        };

    public static bool RunAll(bool throwOnFailure = true)
    {
        try
        {
            VerifyCommandFactContract();
            VerifyWorkFactContract();
            VerifyCurrentV6AndRejectLegacy();
            VerifyRuntimeProductionEntries();
            return true;
        }
        catch
        {
            if (throwOnFailure)
                throw;
            return false;
        }
    }

    public static void RunCommandDomainFactFocused()
    {
        VerifyCommandFactContract();
        VerifyCurrentV6AndRejectLegacy();
        using var runtime = new RuntimeEntryHarness();
        runtime.VerifyCommandRollbackAndNoOp();
        runtime.VerifyHiddenCommandAcknowledgementRecovery();
        runtime.VerifyPendingCommandRawShape();
    }

    public static void RunWorkDomainFactFocused()
    {
        VerifyWorkFactContract();
        VerifyCurrentV6AndRejectLegacy();
        using var runtime = new RuntimeEntryHarness(useProductionPhysicalPorts: true);
        runtime.VerifyPartialWorkRoundTrip();
        runtime.VerifyTaming();
        runtime.VerifyProductForwardRecovery();
        runtime.VerifyManureReservationRollback();
        runtime.VerifySlaughter();
        runtime.VerifyHiddenWorkAcknowledgementRecovery();
        runtime.VerifyPendingWorkRawShape();
    }

    private static void VerifyCommandFactContract()
    {
        var fixture = new MigratedProducerOutcomeEditorFixture(
            "run:animal-husbandry-command");
        var probe = new MigratedProducerOutcomeProbeTransaction(
            fixture.Transaction,
            fixture.Recorder)
        {
            TargetKind = MigratedProducerOutcomeKind.AnimalHusbandryCommandResult
        };
        using var facts = new DomainFactDispatcher();
        var commits = new GameplayDomainFactCommitGroup(
            fixture.Recorder,
            fixture.Ledger);
        facts.Subscribe(
            "fixture:animal-husbandry-command",
            new MigratedProducerDomainFactProbeObserver<
                AnimalHusbandryCommandFact>(
                AnimalHusbandryCommandFactMapping.Capture,
                fixture.Transaction,
                commits,
                probe));

        var beforePolicy = new AnimalPenPolicyData
        {
            PenId = new BuildingInstanceId("building:pen-qa"),
            maximumAnimals = 4,
            AllowedSpeciesIds = new List<WildlifeSpeciesId>
            {
                new("deep_goat")
            }
        };
        AnimalPenPolicyData afterPolicy = beforePolicy.Clone();
        afterPolicy.maximumAnimals = 6;
        var policyFact = new AnimalHusbandryCommandFact(
            AnimalHusbandryDomainOperationIdentity.Format(
                AnimalHusbandryOutcomeOperationKind.SetPenPolicy,
                beforePolicy.PenId.Value,
                1L),
            9,
            beforePolicy,
            afterPolicy);
        string immutablePolicy = JsonUtility.ToJson(
            policyFact.CaptureSaveData());
        beforePolicy.AllowedSpeciesIds.Clear();
        afterPolicy.maximumAnimals = 99;
        AnimalPenPolicyData returnedPolicy = policyFact.CaptureBeforePolicy();
        returnedPolicy.maximumAnimals = 77;
        returnedPolicy.AllowedSpeciesIds.Clear();
        AnimalHusbandryCommandFactSaveData returnedPolicySave =
            policyFact.CaptureSaveData();
        returnedPolicySave.policyStates[0].allowedSpeciesDefinitionIds.Clear();
        Require(JsonUtility.ToJson(policyFact.CaptureSaveData()) == immutablePolicy,
            "Husbandry policy fact retained caller or returned mutable state.");

        var beforeAnimal = new HusbandryAnimalState
        {
            AnimalId = new WildlifeInstanceId("wild:qa-goat"),
            SpeciesId = new WildlifeSpeciesId("deep_goat"),
            PenId = new BuildingInstanceId("building:pen-qa"),
            Sex = AnimalSex.Female,
            AgeDays = 20f,
            Tamed = true,
            TamingProgress = 1f,
            Products = new List<AnimalProductProgressState>
            {
                new()
                {
                    ItemId = new ItemDefinitionId("resource:manure"),
                    ReadyCycles = 1
                }
            },
            StatusParameters = new List<string> { "ready" },
            PendingWorkContributions = new List<CraftContributionSaveData>()
        };
        HusbandryAnimalState afterAnimal = beforeAnimal.Clone();
        AnimalHusbandryCommandService.ApplySlaughterDesignation(
            afterAnimal,
            designated: true);
        var animalFact = new AnimalHusbandryCommandFact(
            AnimalHusbandryDomainOperationIdentity.Format(
                AnimalHusbandryOutcomeOperationKind.DesignateSlaughter,
                beforeAnimal.AnimalId.Value,
                2L),
            9,
            beforeAnimal,
            afterAnimal);
        string immutableAnimal = JsonUtility.ToJson(
            animalFact.CaptureSaveData());
        beforeAnimal.Products[0].ReadyCycles = 9;
        afterAnimal.StatusParameters.Add("mutated");
        HusbandryAnimalState returnedAnimal = animalFact.CaptureAfterAnimal();
        returnedAnimal.Products[0].ReadyCycles = 10;
        returnedAnimal.StatusParameters.Clear();
        AnimalHusbandryCommandFactSaveData returnedAnimalSave =
            animalFact.CaptureSaveData();
        returnedAnimalSave.animalStates[0].products.Clear();
        Require(JsonUtility.ToJson(animalFact.CaptureSaveData()) == immutableAnimal,
            "Husbandry animal fact retained caller or returned mutable state.");
        AnimalHusbandryCommandFact restoredAnimal =
            AnimalHusbandryCommandFact.Restore(
                JsonUtility.FromJson<AnimalHusbandryCommandFactSaveData>(immutableAnimal),
                _ => new[] { new ItemDefinitionId("resource:manure") },
                itemId => itemId.Value == "resource:manure");
        Require(JsonUtility.ToJson(restoredAnimal.CaptureSaveData()) == immutableAnimal,
            "Husbandry animal command fact did not restore through an explicit catalog.");

        Require(facts.TryReserve(
                policyFact,
                out DomainFactLease lease,
                out string failure),
            "Husbandry command fact reservation failed: " + failure);
        DomainFactDelivery committed = facts.Publish(lease, policyFact);
        Require(committed.Phase is DomainFactDeliveryPhase.Delivered
                or DomainFactDeliveryPhase.AcceptedPendingDelivery,
            "Husbandry command fact did not commit: " + committed.Detail);
        _ = MigratedProducerOutcomeE2EAssertions.RequireExact(
            fixture,
            MigratedProducerOutcomeKind.AnimalHusbandryCommandResult,
            probe);
    }

    private static void VerifyWorkFactContract()
    {
        var fixture = new MigratedProducerOutcomeEditorFixture(
            "run:animal-husbandry-work");
        var probe = new MigratedProducerOutcomeProbeTransaction(
            fixture.Transaction,
            fixture.Recorder)
        {
            TargetKind = MigratedProducerOutcomeKind.AnimalHusbandryWorkResult
        };
        using var facts = new DomainFactDispatcher();
        facts.Subscribe(
            "fixture:animal-husbandry-work",
            new MigratedProducerDomainFactProbeObserver<
                AnimalHusbandryWorkFact>(
                AnimalHusbandryWorkFactMapping.Capture,
                fixture.Transaction,
                new GameplayDomainFactCommitGroup(
                    fixture.Recorder,
                    fixture.Ledger),
                probe));

        AnimalHusbandryPendingOutcomeState pending = Pending();
        var actual = new AnimalHusbandryWorkFact(pending);
        string immutable = JsonConvert.SerializeObject(
            actual.CaptureDomainState());
        pending.AnimalStatesBefore[0].StatusParameters.Add("mutated-input");
        pending.Contributions[0].contributedWork = 99f;
        pending.OutputStacks[0].Quantity = 99;
        AnimalHusbandryPendingOutcomeState returned = actual.CaptureDomainState();
        returned.AnimalStatesBefore[0].StatusParameters.Add("mutated-return");
        returned.Contributions.Clear();
        returned.OutputStacks.Clear();
        Require(JsonConvert.SerializeObject(actual.CaptureDomainState()) == immutable,
            "Husbandry work fact retained mutable input or returned state.");

        AnimalHusbandryPendingOutcomeState proposalState = Pending();
        proposalState.OutputStacks.Clear();
        proposalState.CarcassStackId = string.Empty;
        var proposal = new AnimalHusbandryWorkFact(
            proposalState,
            proposal: true);
        Require(facts.TryReserve(
                proposal,
                out DomainFactLease proposalLease,
                out string reserveFailure),
            "Husbandry work proposal reservation failed: " + reserveFailure);
        DomainFactDelivery rejected = facts.Publish(proposalLease, proposal);
        Require(rejected.Phase == DomainFactDeliveryPhase.Rejected
                && probe.CommitCount(
                    MigratedProducerOutcomeKind.AnimalHusbandryWorkResult) == 0
                && fixture.Ledger.CaptureGameplayOutcomes().exactOutcomes.Count == 0,
            "A reservation-only husbandry proposal committed as an actual fact.");

        Require(facts.TryReserve(
                proposal,
                out DomainFactLease actualLease,
                out string actualFailure),
            "Husbandry actual reservation failed: " + actualFailure);
        DomainFactDelivery committed = facts.Publish(actualLease, actual);
        Require(committed.Phase is DomainFactDeliveryPhase.Delivered
                or DomainFactDeliveryPhase.AcceptedPendingDelivery,
            "Husbandry actual fact did not commit: " + committed.Detail);
        _ = MigratedProducerOutcomeE2EAssertions.RequireExact(
            fixture,
            MigratedProducerOutcomeKind.AnimalHusbandryWorkResult,
            probe);
    }

    private static void VerifyCurrentV6AndRejectLegacy()
    {
        DungeonAnimalHusbandrySaveData current = new()
        {
            nextOutcomeSequence = 1L,
            animals = new List<HusbandryAnimalSaveData>(),
            penPolicies = new List<AnimalPenPolicySaveData>(),
            pendingOutcomes = new List<AnimalHusbandryPendingOutcomeSaveData>(),
            pendingCommandFacts = new List<AnimalHusbandryPendingCommandSaveData>()
        };
        Require(current.version == 6,
            "Beta husbandry saves must use exact V6 without migration.");
        AnimalHusbandryAggregateState restored =
            AnimalHusbandryStateCodec.BuildRestore(
                current,
                5f,
                _ => Array.Empty<ItemDefinitionId>(),
                _ => true)
            .State;
        Require(restored.NextOutcomeSequence == 1L
            && restored.PendingOutcomes.Count == 0
            && restored.PendingCommandFacts.Count == 0,
            "Current V6 empty husbandry payload did not retain canonical initial state.");
        RequireThrows(
            () => AnimalHusbandryStateCodec.BuildRestore(
                new DungeonAnimalHusbandrySaveData
                {
                    nextOutcomeSequence = 0L
                },
                5f,
                _ => Array.Empty<ItemDefinitionId>(),
                _ => true),
            "Current V6 accepted the removed zero-sequence default.");
        RequireThrows(
            () => AnimalHusbandryStateCodec.BuildRestore(
                new DungeonAnimalHusbandrySaveData
                {
                    pendingOutcomes = null
                },
                5f,
                _ => Array.Empty<ItemDefinitionId>(),
                _ => true),
            "Current V6 accepted a null work pending-outcome list.");
        RequireThrows(
            () => AnimalHusbandryStateCodec.BuildRestore(
                new DungeonAnimalHusbandrySaveData
                {
                    pendingCommandFacts = null
                },
                5f,
                _ => Array.Empty<ItemDefinitionId>(),
                _ => true),
            "Current V6 accepted a null command-fact list.");
        foreach (int priorVersion in new[] { 1, 2, 3, 4, 5 })
        {
            DungeonAnimalHusbandrySaveData unsupported = new() { version = priorVersion };
            bool rejected = false;
            try
            {
                _ = AnimalHusbandryStateCodec.BuildRestore(
                    unsupported, 5f, _ => Array.Empty<ItemDefinitionId>(), _ => true);
            }
            catch (InvalidOperationException exception)
            {
                rejected = exception.Message ==
                    $"Animal-husbandry payload version {priorVersion} is unsupported.";
            }
            Require(rejected, $"Unsupported V{priorVersion} must be rejected, not migrated.");
        }
    }

    private static void VerifyRuntimeProductionEntries()
    {
        using var runtime = new RuntimeEntryHarness();
        runtime.VerifyCommandRollbackAndNoOp();
        runtime.VerifyPartialWorkRoundTrip();
        runtime.VerifyTaming();
        runtime.VerifyProductForwardRecovery();
        runtime.VerifyManureReservationRollback();
        runtime.VerifySlaughter();
        Require(runtime.Probe.CommitCount(
                MigratedProducerOutcomeKind.AnimalHusbandryCommandResult) == 2,
            "Actual husbandry command path committed an unexpected number of results.");
        Require(runtime.Probe.CommitCount(
                MigratedProducerOutcomeKind.AnimalHusbandryWorkResult) == 5,
            "Actual husbandry work paths did not commit exactly five terminal results.");
    }

    private static AnimalHusbandryPendingOutcomeState Pending()
    {
        var penId = new BuildingInstanceId("building:pen-qa");
        var animalId = new WildlifeInstanceId("wild:qa-goat");
        var speciesId = new WildlifeSpeciesId("species:qa-goat");
        var before = new HusbandryAnimalState
        {
            AnimalId = animalId,
            SpeciesId = speciesId,
            PenId = penId,
            Sex = AnimalSex.Female,
            AgeDays = 20f,
            Tamed = true,
            TamingProgress = 1f,
            SlaughterDesignated = true,
            Products = new List<AnimalProductProgressState>(),
            StatusParameters = new List<string> { "ready" },
            PendingWorkContributions = new List<CraftContributionSaveData>()
        };
        return new AnimalHusbandryPendingOutcomeState
        {
            OperationId =
                "animal-husbandry:000000000001:slaughter:wild:qa-goat",
            Kind = AnimalHusbandryOutcomeOperationKind.Slaughter,
            AbsoluteDay = 10,
            PenId = penId,
            AnimalId = animalId,
            SpeciesId = speciesId,
            WorkerId = "character:qa-rancher",
            WorkerDisplayName = "목동",
            PublicationKey = "fixture:publication-key",
            AnimalStatesBefore = new List<HusbandryAnimalState> { before },
            AnimalStatesAfter = new List<HusbandryAnimalState>(),
            CompletedWorkMilliUnits = 12000,
            Contributions = new List<CraftContributionSaveData>
            {
                new()
                {
                    characterId = "character:qa-rancher",
                    contributedWork = 12f,
                    relevantSkill = 1f
                }
            },
            OutputItemId = new ItemDefinitionId("item:qa-goat-carcass"),
            OutputQuantity = 1,
            OutputMassGrams = 1250L,
            OutputStacks = new List<AnimalHusbandryOutputStack>
            {
                new() { StackId = "stack:qa-carcass", Quantity = 1 }
            },
            CarcassStackId = "stack:qa-carcass"
        };
    }

    private sealed class RuntimeEntryHarness : IDisposable
    {
        private const string PenId = "building:e02:husbandry-pen";
        private const string WorkerId = "character:e02:rancher";
        private static readonly Vector2Int PenCell = new(7, 3);

        private readonly IGameContentCatalog content;
        private readonly IItemDefinitionCatalog items;
        private readonly IWildlifeSpeciesCatalogProvider speciesCatalog;
        private readonly WildlifeSpeciesDefinition species;
        private readonly List<CapturedWildlifeState> captured = new();
        private readonly List<WildlifeActor> wildlife = new();
        private readonly RuntimeItemStore itemStore = new();
        private readonly IWildlifeCaptureRuntime capturePort;
        private readonly IWildlifeRuntime wildlifePort;
        private readonly IWorldItemStackRuntime itemPort;
        private readonly IWildlifeCarcassService carcassPort;
        private readonly IGameCalendar calendar;
        private readonly IEnvironmentalFieldQuery environmentalField;
        private readonly IFacilityCapabilityQuery facilities;
        private readonly IBuildingFacilityStateChangePort invalidations;
        private readonly WildlifeCarcassService productionCarcasses;
        private readonly FixedClock clock = new();
        private readonly GameObject penObject;
        private readonly GameObject workerObject;
        private readonly CharacterSO workerData;
        private readonly Grid grid = new(20, 12);
        private readonly MigratedProducerOutcomeEditorFixture fixture;
        private readonly DomainFactDispatcher physicalFacts;
        private readonly DomainFactDispatcher domainFacts;
        private readonly ControlledDomainFactPublisher domainPublisher;
        private readonly AnimalHusbandryRuntime runtime;
        private readonly BuildableObject pen;
        private readonly CharacterActor worker;
        private int nextAnimalSequence = 1;
        private string currentAnimalId = string.Empty;

        internal RuntimeEntryHarness(bool useProductionPhysicalPorts = false)
        {
            content = new ResourceGameContentCatalog(
                new UnityGameContentRootLoader());
            items = new ResourceItemDefinitionCatalog(content);
            speciesCatalog = new ResourceWildlifeSpeciesCatalogProvider(
                content,
                items);
            Require(speciesCatalog.TryGetSpecies(
                    "deep_goat",
                    out species)
                && species.Husbandry.Products.Count > 0,
                "The authored deep_goat husbandry product is unavailable.");

            BuildingSO penAsset = UnityEditor.AssetDatabase
                .LoadAssetAtPath<BuildingSO>(
                    "Assets/Resources/SO/Building/Captivity/CB01_야수우리.asset");
            Require(penAsset != null
                && penAsset.GetBeastPenAbility()?.IsValid == true,
                "The authored beast pen is unavailable.");
            penObject = new GameObject("E02 husbandry runtime pen");
            pen = penObject.AddComponent<BuildableObject>();
            pen.PrepareForDetachedRestore();
            pen.RestorePersistentIdentity(new BuildingInstanceId(PenId));
            CharacterAiEditorTestDependencies.Inject(pen);
            pen.SetGrid(grid);
            pen.Initialization(penAsset, PenCell);

            workerObject = new GameObject("E02 rancher");
            workerObject.AddComponent<SpriteRenderer>();
            worker = workerObject.AddComponent<CharacterActor>();
            CharacterAiEditorTestDependencies.Inject(workerObject);
            workerData = CharacterAiEditorTestDependencies
                .CreateCharacterFixtureData(
                    CharacterType.NPC,
                    "축산 담당자",
                    "human");
            worker.Initialization(workerData);
            worker.Identity.SetPersistentId(WorkerId);

            fixture = new MigratedProducerOutcomeEditorFixture(
                "run:animal-husbandry-runtime-entry");
            Probe = new MigratedProducerOutcomeProbeTransaction(
                fixture.Transaction,
                fixture.Recorder);

            capturePort = CreatePort<IWildlifeCaptureRuntime>(
                HandleCapturePort);
            wildlifePort = CreatePort<IWildlifeRuntime>(
                HandleWildlifePort);
            IWorldItemStackRuntime selectedItemPort = CreatePort<IWorldItemStackRuntime>(
                itemStore.Invoke);
            IWildlifeCarcassService selectedCarcassPort =
                CreatePort<IWildlifeCarcassService>(HandleCarcassPort);
            calendar = CreatePort<IGameCalendar>((method, _) =>
                method.Name == "get_Day"
                    ? 23
                    : throw Unexpected(method));
            var aggregateStore = new DungeonRuntimeAggregateRootStore();
            if (useProductionPhysicalPorts)
            {
                var catalog = new ResourceDungeonItemCatalogProvider(items);
                WorldItemStackRuntime physical = PhysicalItemDebugScenarios
                    .CreateRuntimeForCrossDomainFixture(catalog, aggregateStore,
                        out WorldItemRepository repository, out _, out _, out _, out _, out _);
                itemStore.PhysicalRuntime = physical;
                selectedItemPort = physical;
                var spawner = new WorldItemSpawner(catalog, repository,
                    EditorNullItemMarkerPresenter.Instance);
                physicalFacts = fixture.CreatePhysicalTransformationFacts(Probe);
                var transforms = new PhysicalItemTransformService(repository, spawner,
                    physical.MassQuery, catalog, EditorNullItemMarkerPresenter.Instance,
                    new FixedGameSessionStateProvider(23), physicalFacts);
                productionCarcasses = new WildlifeCarcassService(physical, transforms,
                    speciesCatalog, new GameEventBus());
                productionCarcasses.ConstructFactPublisher(calendar, physicalFacts);
                selectedCarcassPort = productionCarcasses;
            }
            itemPort = selectedItemPort;
            carcassPort = selectedCarcassPort;
            environmentalField =
                CreatePort<IEnvironmentalFieldQuery>((method, _) =>
                    throw Unexpected(method));
            facilities =
                CreatePort<IFacilityCapabilityQuery>((method, _) =>
                    throw Unexpected(method));
            invalidations =
                CreatePort<IBuildingFacilityStateChangePort>((method, _) =>
                    method.Name == "MarkDynamicStateDirty"
                        ? null
                        : throw Unexpected(method));
            domainFacts = new DomainFactDispatcher();
            var commits = new GameplayDomainFactCommitGroup(
                fixture.Recorder,
                fixture.Ledger);
            domainFacts.Subscribe(
                "fixture:animal-husbandry-command-runtime",
                new MigratedProducerDomainFactProbeObserver<
                    AnimalHusbandryCommandFact>(
                    AnimalHusbandryCommandFactMapping.Capture,
                    fixture.Transaction,
                    commits,
                    Probe));
            domainFacts.Subscribe(
                "fixture:animal-husbandry-work-runtime",
                new MigratedProducerDomainFactProbeObserver<
                    AnimalHusbandryWorkFact>(
                    AnimalHusbandryWorkFactMapping.Capture,
                    fixture.Transaction,
                    commits,
                    Probe));
            domainPublisher = new ControlledDomainFactPublisher(domainFacts);
            runtime = CreateRuntime(aggregateStore);
        }

        internal MigratedProducerOutcomeProbeTransaction Probe { get; }
        internal JArray PhysicalCheckpoints { get; } = new();

        private AnimalHusbandryRuntime CreateRuntime(
            DungeonRuntimeAggregateRootStore aggregateStore)
        {
            var value = new AnimalHusbandryRuntime(
                capturePort,
                wildlifePort,
                speciesCatalog,
                items,
                itemPort,
                carcassPort,
                clock,
                calendar,
                environmentalField,
                facilities,
                invalidations,
                aggregateStore);
            value.ConstructDomainFacts(domainPublisher);
            return value;
        }

        private void CapturePhysicalCheckpoint(string stage)
        {
            if (itemStore.PhysicalRuntime == null)
                return;
            PhysicalCheckpoints.Add(new JObject
            {
                ["stage"] = stage,
                ["animalId"] = currentAnimalId,
                ["physical"] = JObject.Parse(JsonUtility.ToJson(itemStore.PhysicalRuntime.Capture())),
                ["husbandry"] = JObject.Parse(JsonUtility.ToJson(runtime.Capture())),
                ["freshness"] = JArray.FromObject(productionCarcasses.CaptureFreshness()),
                ["ledger"] = JObject.FromObject(fixture.Ledger.CaptureGameplayOutcomes())
            });
        }

        internal JObject CapturePhysicalProof(string id)
        {
            WorldItemStackRuntime physical = itemStore.PhysicalRuntime
                ?? throw new InvalidOperationException("Production physical proof requires real item ports.");
            var parents = fixture.Ledger.CaptureGameplayOutcomes().exactOutcomes
                .Where(row => row.outcomeTypeId == MigratedProducerOutcomeCatalog
                    .Get(MigratedProducerOutcomeKind.AnimalHusbandryWorkResult).OutcomeTypeId.Value
                    && row.facts.Any(fact => fact.factId == "animal-id"
                        && fact.value == currentAnimalId)).ToArray();
            Require(parents.Length == (id == "policy" ? 0 : 1),
                "Production physical proof has a missing or duplicate animal work result.");
            var parent = parents.SingleOrDefault();
            string itemId = id switch
            {
                "partial-manure" or "manure" => "resource:manure",
                "product" => species.Husbandry.Products[0].ItemId,
                "slaughter" => species.CarcassItemId,
                _ => string.Empty
            };
            var stacks = physical.GetAllStacks().Where(value => value != null
                && value.Quantity > 0 && value.Position == PenCell
                && value.ItemId == itemId).OrderBy(value => value.StackId, StringComparer.Ordinal).ToArray();
            int quantity = stacks.Sum(value => value.Quantity);
            long mass = itemId.Length == 0 ? 0L : checked(physical.MassQuery
                .GetDefinitionUnitMass(new ItemDefinitionId(itemId)).Value * quantity);
            string vector = string.Join(",", stacks.Select(value => value.StackId + ":" + value.Quantity));
            if (parent != null)
            {
                Require(Fact("worker-id") == WorkerId && Fact("pen-id") == PenId,
                    "Physical output parent lost its exact worker or pen identity.");
                string output = itemId.Length == 0 ? "none" : "item=" + itemId
                    + ";quantity=" + quantity + ";massGrams=" + mass + ";stacks=" + vector
                    + ";carcass=" + (id == "slaughter" ? stacks.Single().StackId : "none");
                Require(Fact("physical-output") == output,
                    "Parent physical-output fact does not match the actual item IDs, quantities and mass.");
                if (id == "slaughter")
                {
                    var freshness = productionCarcasses.CaptureFreshness();
                    Require(freshness.Count == 1 && freshness[0].stackId == stacks.Single().StackId
                            && freshness[0].speciesId == species.SpeciesId,
                        "Actual slaughter carcass lost its freshness/stack/species join.");
                }
            }

            string physicalJson = JsonUtility.ToJson(physical.Capture());
            string husbandryJson = JsonUtility.ToJson(runtime.Capture());
            string freshnessJson = JsonConvert.SerializeObject(productionCarcasses.CaptureFreshness());
            string ledgerJson = JsonConvert.SerializeObject(fixture.Ledger.CaptureGameplayOutcomes());
            physical.Restore(JsonUtility.FromJson<DungeonPhysicalItemSaveData>(physicalJson));
            productionCarcasses.ReplaceFreshnessValidated(JsonConvert.DeserializeObject
                <List<WildlifeCarcassFreshnessSaveData>>(freshnessJson));
            runtime.Restore(runtime.BuildRestore(JsonUtility
                .FromJson<DungeonAnimalHusbandrySaveData>(husbandryJson)));
            Require(physicalJson == JsonUtility.ToJson(physical.Capture())
                    && husbandryJson == JsonUtility.ToJson(runtime.Capture())
                    && freshnessJson == JsonConvert.SerializeObject(productionCarcasses.CaptureFreshness())
                    && ledgerJson == JsonConvert.SerializeObject(fixture.Ledger.CaptureGameplayOutcomes()),
                "Production physical/husbandry/freshness save round trip changed items or repeated an outcome.");
            CapturePhysicalCheckpoint("roundtrip-" + id);
            return new JObject
            {
                ["id"] = id, ["passed"] = true, ["animalId"] = currentAnimalId,
                ["penId"] = PenId, ["workerId"] = WorkerId,
                ["itemId"] = itemId, ["quantity"] = quantity, ["massGrams"] = mass,
                ["stackVector"] = vector,
                ["parent"] = parent == null ? JValue.CreateNull() : JObject.FromObject(parent),
                ["canonicalResultKey"] = parent == null ? "none" : Probe
                    .LatestCommittedKey(MigratedProducerOutcomeKind.AnimalHusbandryWorkResult).ToString(),
                ["saveRoundTripExact"] = true
            };

            string Fact(string name) => parent.facts.Single(value => value.factId == name).value;
        }

        internal void VerifyCommandRollbackAndNoOp()
        {
            WildlifeInstanceId animalId = RestoreAnimal(
                AnimalHusbandryWorkKind.CollectManure,
                nextSequence: 10L);
            AnimalPenPolicyData policy = runtime.GetPenPolicy(
                new BuildingInstanceId(PenId));
            string before = JsonUtility.ToJson(runtime.Capture());
            policy.maximumAnimals = 5;
            Probe.TargetKind =
                MigratedProducerOutcomeKind.AnimalHusbandryCommandResult;
            Probe.FaultMode = MigratedProducerOutcomeFaultMode.RejectReservation;
            Require(!runtime.SetPenPolicy(policy, out AnimalHusbandryFailure reserveRejected)
                    && reserveRejected.Code == AnimalHusbandryFailureCode.OutcomeUnavailable
                    && JsonUtility.ToJson(runtime.Capture()) == before,
                "Actual pen policy mutation ignored a fact reservation rejection.");

            Probe.FaultMode = MigratedProducerOutcomeFaultMode.RejectCommit;
            Require(!runtime.SetPenPolicy(policy, out AnimalHusbandryFailure rejected)
                    && rejected.Code == AnimalHusbandryFailureCode.OutcomeUnavailable,
                "Actual pen policy mutation ignored a fact commit rejection.");
            Require(JsonUtility.ToJson(runtime.Capture()) == before,
                "Rejected pen policy command did not roll back the aggregate state.");

            Probe.FaultMode = MigratedProducerOutcomeFaultMode.None;
            Require(runtime.SetPenPolicy(policy, out AnimalHusbandryFailure accepted)
                    && !accepted.IsFailure
                    && runtime.GetPenPolicy(new BuildingInstanceId(PenId))
                        .maximumAnimals == 5,
                "Actual pen policy command did not commit the normalized policy.");
            int reservations = Probe.ReservationCount(
                MigratedProducerOutcomeKind.AnimalHusbandryCommandResult);
            Require(!runtime.SetPenPolicy(policy, out AnimalHusbandryFailure noOp)
                    && !noOp.IsFailure
                    && Probe.ReservationCount(
                        MigratedProducerOutcomeKind.AnimalHusbandryCommandResult)
                        == reservations,
                "No-op pen policy command emitted a terminal result.");

            Require(runtime.TryGetAnimal(animalId, out HusbandryAnimalState initial)
                    && !initial.SlaughterDesignated,
                "Designation fixture did not retain an undesignated animal.");
            string beforeDesignation = JsonUtility.ToJson(runtime.Capture());
            Probe.FaultMode = MigratedProducerOutcomeFaultMode.RejectReservation;
            Require(!runtime.DesignateSlaughter(
                    animalId,
                    designated: true,
                    out AnimalHusbandryFailure designationReserveRejected)
                    && designationReserveRejected.Code
                        == AnimalHusbandryFailureCode.OutcomeUnavailable
                    && JsonUtility.ToJson(runtime.Capture()) == beforeDesignation,
                "Slaughter designation ignored a fact reservation rejection.");

            Probe.FaultMode = MigratedProducerOutcomeFaultMode.RejectCommit;
            Require(!runtime.DesignateSlaughter(
                    animalId,
                    designated: true,
                    out AnimalHusbandryFailure designationCommitRejected)
                    && designationCommitRejected.Code
                        == AnimalHusbandryFailureCode.OutcomeUnavailable
                    && JsonUtility.ToJson(runtime.Capture()) == beforeDesignation,
                "Rejected slaughter designation did not roll back its aggregate state.");

            Probe.FaultMode = MigratedProducerOutcomeFaultMode.None;
            Require(runtime.DesignateSlaughter(
                    animalId,
                    designated: true,
                    out AnimalHusbandryFailure designationAccepted)
                    && !designationAccepted.IsFailure
                    && runtime.TryGetAnimal(animalId, out HusbandryAnimalState designated)
                    && designated.SlaughterDesignated,
                "Actual slaughter designation did not commit its typed fact and state.");
            int designationReservations = Probe.ReservationCount(
                MigratedProducerOutcomeKind.AnimalHusbandryCommandResult);
            Require(!runtime.DesignateSlaughter(
                    animalId,
                    designated: true,
                    out AnimalHusbandryFailure designationNoOp)
                    && !designationNoOp.IsFailure
                    && Probe.ReservationCount(
                        MigratedProducerOutcomeKind.AnimalHusbandryCommandResult)
                        == designationReservations,
                "No-op slaughter designation emitted a typed fact.");

            Require(Probe.CommitCount(
                    MigratedProducerOutcomeKind.AnimalHusbandryCommandResult) == 2
                    && fixture.Ledger.CaptureGameplayOutcomes().exactOutcomes.Count(
                        value => value.outcomeTypeId == MigratedProducerOutcomeCatalog
                            .Get(MigratedProducerOutcomeKind.AnimalHusbandryCommandResult)
                            .OutcomeTypeId.Value) == 2,
                "Actual husbandry commands did not leave exactly two canonical results.");
            _ = MigratedProducerOutcomeE2EAssertions.RequireExact(
                fixture,
                MigratedProducerOutcomeKind.AnimalHusbandryCommandResult,
                Probe);
        }

        internal void VerifyHiddenCommandAcknowledgementRecovery()
        {
            RestoreAnimal(
                AnimalHusbandryWorkKind.CollectManure,
                nextSequence: 100L);
            Probe.TargetKind =
                MigratedProducerOutcomeKind.AnimalHusbandryCommandResult;
            Probe.FaultMode = MigratedProducerOutcomeFaultMode.None;
            int commitsBefore = Probe.CommitCount(
                MigratedProducerOutcomeKind.AnimalHusbandryCommandResult);
            int ledgerBefore = fixture.Ledger.CaptureGameplayOutcomes()
                .exactOutcomes.Count;
            domainPublisher.HideNextAcknowledgement = true;
            AnimalPenPolicyData policy = runtime.GetPenPolicy(
                new BuildingInstanceId(PenId));
            policy.maximumAnimals = 7;
            Require(!runtime.SetPenPolicy(
                    policy,
                    out AnimalHusbandryFailure hidden)
                    && hidden.Code == AnimalHusbandryFailureCode.OutcomeUnavailable
                    && runtime.GetPenPolicy(new BuildingInstanceId(PenId))
                        .maximumAnimals == 7
                    && runtime.LastOutcomeFailureForDebug
                        == "husbandry-command-delivery-unconfirmed:qa:hidden-husbandry-domain-fact-ack",
                "Hidden husbandry acknowledgement did not retain the applied command state.");

            DungeonAnimalHusbandrySaveData pending = runtime.Capture();
            Require(pending.nextOutcomeSequence == 101L
                    && pending.pendingCommandFacts.Count == 1
                    && pending.pendingCommandFacts[0]?.fact != null
                    && pending.pendingCommandFacts[0].fact.kind
                        == AnimalHusbandryOutcomeOperationKind.SetPenPolicy
                    && pending.pendingCommandFacts[0].fact.policyStates.Count == 2
                    && pending.pendingCommandFacts[0].fact.animalStates.Count == 0
                    && !string.IsNullOrEmpty(
                        pending.pendingCommandFacts[0].publicationKey)
                    && Probe.CommitCount(
                        MigratedProducerOutcomeKind.AnimalHusbandryCommandResult)
                        == commitsBefore + 1
                    && fixture.Ledger.CaptureGameplayOutcomes()
                        .exactOutcomes.Count == ledgerBefore + 1,
                "Hidden husbandry acknowledgement did not preserve one exact saved fact.");
            string publicationKey =
                pending.pendingCommandFacts[0].publicationKey;
            Require(Probe.LatestCommittedKey(
                    MigratedProducerOutcomeKind.AnimalHusbandryCommandResult)
                    .ToString() == publicationKey,
                "Saved husbandry publication key drifted from the canonical result key.");

            string checkpoint = JsonUtility.ToJson(pending);
            int replaysBefore = domainPublisher.ReplayBatchCount;
            AnimalHusbandryRuntime restored = CreateRuntime(
                new DungeonRuntimeAggregateRootStore());
            restored.Restore(restored.BuildRestore(
                JsonUtility.FromJson<DungeonAnimalHusbandrySaveData>(checkpoint)));
            restored.Tick();
            DungeonAnimalHusbandrySaveData completed = restored.Capture();
            Require(completed.nextOutcomeSequence == 101L
                    && completed.pendingCommandFacts.Count == 0
                    && restored.GetPenPolicy(new BuildingInstanceId(PenId))
                        .maximumAnimals == 7
                    && domainPublisher.ReplayBatchCount == replaysBefore + 1
                    && domainPublisher.LastReplayPublicationKey == publicationKey
                    && Probe.CommitCount(
                        MigratedProducerOutcomeKind.AnimalHusbandryCommandResult)
                        == commitsBefore + 1
                    && fixture.Ledger.CaptureGameplayOutcomes()
                        .exactOutcomes.Count == ledgerBefore + 1,
                "Fresh husbandry owner replayed state or duplicated the command outcome.");

            int reservations = Probe.ReservationCount(
                MigratedProducerOutcomeKind.AnimalHusbandryCommandResult);
            Require(!restored.SetPenPolicy(
                    policy,
                    out AnimalHusbandryFailure noOp)
                    && !noOp.IsFailure
                    && restored.Capture().nextOutcomeSequence == 101L
                    && Probe.ReservationCount(
                        MigratedProducerOutcomeKind.AnimalHusbandryCommandResult)
                        == reservations
                    && domainPublisher.ReplayBatchCount == replaysBefore + 1
                    && fixture.Ledger.CaptureGameplayOutcomes()
                        .exactOutcomes.Count == ledgerBefore + 1,
                "Recovered husbandry command accepted a duplicate no-op.");

            string terminal = JsonUtility.ToJson(restored.Capture());
            AnimalHusbandryRuntime terminalOwner = CreateRuntime(
                new DungeonRuntimeAggregateRootStore());
            terminalOwner.Restore(terminalOwner.BuildRestore(
                JsonUtility.FromJson<DungeonAnimalHusbandrySaveData>(terminal)));
            terminalOwner.Tick();
            Require(JsonUtility.ToJson(terminalOwner.Capture()) == terminal
                    && domainPublisher.ReplayBatchCount == replaysBefore + 1
                    && fixture.Ledger.CaptureGameplayOutcomes()
                        .exactOutcomes.Count == ledgerBefore + 1,
                "Terminal husbandry command restore replayed an acknowledged fact.");
        }

        internal void VerifyHiddenWorkAcknowledgementRecovery()
        {
            WildlifeInstanceId animalId = RestoreAnimal(
                AnimalHusbandryWorkKind.CollectManure,
                nextSequence: 110L);
            Probe.TargetKind =
                MigratedProducerOutcomeKind.AnimalHusbandryWorkResult;
            Probe.FaultMode = MigratedProducerOutcomeFaultMode.None;
            int commitsBefore = Probe.CommitCount(
                MigratedProducerOutcomeKind.AnimalHusbandryWorkResult);
            int ledgerBefore = fixture.Ledger.CaptureGameplayOutcomes()
                .exactOutcomes.Count;
            int physicalBefore = itemStore.QuantityAt(
                "resource:manure",
                PenCell);
            domainPublisher.HideNextAcknowledgement = true;
            Require(runtime.ApplyWork(
                    pen,
                    worker,
                    animalId,
                    AnimalHusbandryWorkKind.CollectManure,
                    100000f,
                    out bool completed)
                    && completed
                    && itemStore.QuantityAt("resource:manure", PenCell)
                        == physicalBefore + 1
                    && runtime.TryGetAnimal(
                        animalId,
                        out HusbandryAnimalState applied)
                    && applied.ReadyManureCycles == 0
                    && runtime.LastOutcomeFailureForDebug
                        == "husbandry-work-delivery-unconfirmed:qa:hidden-husbandry-domain-fact-ack",
                "Hidden husbandry work acknowledgement did not retain the applied domain and physical state.");

            DungeonAnimalHusbandrySaveData pending = runtime.Capture();
            Require(pending.nextOutcomeSequence == 111L
                    && pending.pendingOutcomes.Count == 1
                    && pending.pendingOutcomes[0] != null
                    && !string.IsNullOrEmpty(
                        pending.pendingOutcomes[0].publicationKey)
                    && pending.pendingOutcomes[0].animalStatesBefore.Count == 1
                    && pending.pendingOutcomes[0].animalStatesAfter.Count == 1
                    && pending.pendingOutcomes[0].contributions.Count == 1
                    && pending.pendingOutcomes[0].outputStacks.Sum(
                        value => value.quantity) == 1
                    && Probe.CommitCount(
                        MigratedProducerOutcomeKind.AnimalHusbandryWorkResult)
                        == commitsBefore + 1
                    && fixture.Ledger.CaptureGameplayOutcomes()
                        .exactOutcomes.Count == ledgerBefore + 1,
                "Hidden husbandry work acknowledgement did not preserve one exact saved fact.");
            string publicationKey = pending.pendingOutcomes[0].publicationKey;
            Require(Probe.LatestCommittedKey(
                    MigratedProducerOutcomeKind.AnimalHusbandryWorkResult)
                    .ToString() == publicationKey,
                "Saved husbandry work publication key drifted from the canonical result key.");

            string checkpoint = JsonUtility.ToJson(pending);
            int replaysBefore = domainPublisher.ReplayBatchCount;
            AnimalHusbandryRuntime restored = CreateRuntime(
                new DungeonRuntimeAggregateRootStore());
            restored.Restore(restored.BuildRestore(
                JsonUtility.FromJson<DungeonAnimalHusbandrySaveData>(checkpoint)));
            restored.Tick();
            Require(restored.Capture().pendingOutcomes.Count == 0
                    && restored.Capture().nextOutcomeSequence == 111L
                    && restored.TryGetAnimal(
                        animalId,
                        out HusbandryAnimalState recovered)
                    && recovered.ReadyManureCycles == 0
                    && itemStore.QuantityAt("resource:manure", PenCell)
                        == physicalBefore + 1
                    && domainPublisher.ReplayBatchCount == replaysBefore + 1
                    && domainPublisher.LastReplayPublicationKey == publicationKey
                    && Probe.CommitCount(
                        MigratedProducerOutcomeKind.AnimalHusbandryWorkResult)
                        == commitsBefore + 1
                    && fixture.Ledger.CaptureGameplayOutcomes()
                        .exactOutcomes.Count == ledgerBefore + 1,
                "Fresh husbandry owner replayed work or duplicated its physical output/outcome.");

            string terminal = JsonUtility.ToJson(restored.Capture());
            AnimalHusbandryRuntime terminalOwner = CreateRuntime(
                new DungeonRuntimeAggregateRootStore());
            terminalOwner.Restore(terminalOwner.BuildRestore(
                JsonUtility.FromJson<DungeonAnimalHusbandrySaveData>(terminal)));
            terminalOwner.Tick();
            Require(JsonUtility.ToJson(terminalOwner.Capture()) == terminal
                    && itemStore.QuantityAt("resource:manure", PenCell)
                        == physicalBefore + 1
                    && domainPublisher.ReplayBatchCount == replaysBefore + 1
                    && fixture.Ledger.CaptureGameplayOutcomes()
                        .exactOutcomes.Count == ledgerBefore + 1,
                "Terminal husbandry work restore replayed an acknowledged fact.");
        }

        internal void VerifyPendingCommandRawShape()
        {
            var section = new AnimalHusbandrySaveSection(runtime);
            string canonical = section.Capture();
            JObject missing = JObject.Parse(canonical);
            missing.Property("pendingCommandFacts")?.Remove();
            RequireThrows(
                () => ((IDungeonSaveSectionPreflight)section).ValidatePayload(
                    missing.ToString(Formatting.None),
                    section.SectionVersion,
                    new DungeonGameRestoreReport()),
                "Husbandry save admitted a missing pendingCommandFacts array.");

            JObject explicitNull = JObject.Parse(canonical);
            explicitNull["pendingCommandFacts"] = JValue.CreateNull();
            RequireThrows(
                () => ((IDungeonSaveSectionPreflight)section).ValidatePayload(
                    explicitNull.ToString(Formatting.None),
                    section.SectionVersion,
                    new DungeonGameRestoreReport()),
                "Husbandry save admitted a null pendingCommandFacts array.");
        }

        internal void VerifyPendingWorkRawShape()
        {
            var section = new AnimalHusbandrySaveSection(runtime);
            string canonical = section.Capture();
            JObject missing = JObject.Parse(canonical);
            missing.Property("pendingOutcomes")?.Remove();
            RequireThrows(
                () => ((IDungeonSaveSectionPreflight)section).ValidatePayload(
                    missing.ToString(Formatting.None),
                    section.SectionVersion,
                    new DungeonGameRestoreReport()),
                "Husbandry save admitted a missing pendingOutcomes array.");

            JObject explicitNull = JObject.Parse(canonical);
            explicitNull["pendingOutcomes"] = JValue.CreateNull();
            RequireThrows(
                () => ((IDungeonSaveSectionPreflight)section).ValidatePayload(
                    explicitNull.ToString(Formatting.None),
                    section.SectionVersion,
                    new DungeonGameRestoreReport()),
                "Husbandry save admitted a null pendingOutcomes array.");

            JObject missingKey = JObject.Parse(canonical);
            JObject pending = (JObject)missingKey["pendingOutcomes"]?[0];
            if (pending != null)
                pending.Property("publicationKey")?.Remove();
            Require(pending != null,
                "Husbandry work raw-shape fixture requires one saved pending fact.");
            RequireThrows(
                () => ((IDungeonSaveSectionPreflight)section).ValidatePayload(
                    missingKey.ToString(Formatting.None),
                    section.SectionVersion,
                    new DungeonGameRestoreReport()),
                "Husbandry save admitted a pending work fact without its publication key.");
        }

        internal void VerifyPartialWorkRoundTrip()
        {
            WildlifeInstanceId animalId = RestoreAnimal(
                AnimalHusbandryWorkKind.CollectManure,
                nextSequence: 15L);
            Require(runtime.TryGetWork(pen, worker, out AnimalHusbandryWorkSnapshot before)
                    && before.Available
                    && before.AnimalId.Equals(animalId)
                    && before.Kind == AnimalHusbandryWorkKind.CollectManure
                    && Mathf.Approximately(before.CompletedWork, 0f),
                "Actual husbandry partial path did not expose fresh pending work.");
            int reservationsBefore = Probe.ReservationCount(
                MigratedProducerOutcomeKind.AnimalHusbandryWorkResult);
            int commitsBefore = Probe.CommitCount(
                MigratedProducerOutcomeKind.AnimalHusbandryWorkResult);
            float partial = before.RequiredWork * 0.4f;
            Require(runtime.ApplyWork(
                    pen,
                    worker,
                    animalId,
                    AnimalHusbandryWorkKind.CollectManure,
                    partial,
                    out bool partialCompleted)
                && !partialCompleted,
                "Sub-terminal husbandry work did not remain incomplete.");
            Require(runtime.TryGetAnimal(animalId, out HusbandryAnimalState partialState)
                    && partialState.PendingWorkKind
                        == AnimalHusbandryWorkKind.CollectManure
                    && Mathf.Abs(partialState.PendingWorkCompleted - partial) <= 0.001f
                    && partialState.PendingWorkContributions.Count == 1
                    && string.Equals(
                        partialState.PendingWorkContributions[0].characterId,
                        WorkerId,
                        StringComparison.Ordinal)
                    && Mathf.Abs(
                        partialState.PendingWorkContributions[0].contributedWork
                        - partial) <= 0.001f
                    && runtime.Capture().pendingOutcomes.Count == 0
                    && itemStore.QuantityAt("resource:manure", PenCell) == 0
                    && Probe.ReservationCount(
                        MigratedProducerOutcomeKind.AnimalHusbandryWorkResult)
                        == reservationsBefore
                    && Probe.CommitCount(
                        MigratedProducerOutcomeKind.AnimalHusbandryWorkResult)
                        == commitsBefore,
                "Partial husbandry work emitted output/result or lost its exact contribution.");
            CapturePhysicalCheckpoint("partial-manure-before-restore");

            string checkpoint = JsonUtility.ToJson(runtime.Capture());
            runtime.Restore(runtime.BuildRestore(
                JsonUtility.FromJson<DungeonAnimalHusbandrySaveData>(checkpoint)));
            Require(string.Equals(
                    JsonUtility.ToJson(runtime.Capture()),
                    checkpoint,
                    StringComparison.Ordinal)
                && runtime.TryGetWork(
                    pen,
                    worker,
                    out AnimalHusbandryWorkSnapshot restored)
                && restored.AnimalId.Equals(animalId)
                && restored.Kind == AnimalHusbandryWorkKind.CollectManure
                && Mathf.Abs(restored.RequiredWork - before.RequiredWork) <= 0.001f
                && Mathf.Abs(restored.CompletedWork - partial) <= 0.001f,
                "Partial husbandry work did not round-trip exactly.");

            Require(runtime.ApplyWork(
                    pen,
                    worker,
                    animalId,
                    AnimalHusbandryWorkKind.CollectManure,
                    before.RequiredWork,
                    out bool terminalCompleted)
                && terminalCompleted
                && runtime.TryGetAnimal(animalId, out HusbandryAnimalState terminalState)
                && terminalState.ReadyManureCycles == 0
                && itemStore.QuantityAt("resource:manure", PenCell) == 1
                && runtime.Capture().pendingOutcomes.Count == 0
                && Probe.ReservationCount(
                    MigratedProducerOutcomeKind.AnimalHusbandryWorkResult)
                    == reservationsBefore + 1
                && Probe.CommitCount(
                    MigratedProducerOutcomeKind.AnimalHusbandryWorkResult)
                    == commitsBefore + 1,
                "Restored partial husbandry work did not finish with one output and parent result.");
            RequireWorkReceipt();

            DungeonAnimalHusbandrySaveData terminal = runtime.Capture();
            runtime.Restore(runtime.BuildRestore(
                JsonUtility.FromJson<DungeonAnimalHusbandrySaveData>(
                    JsonUtility.ToJson(terminal))));
            Require(itemStore.QuantityAt("resource:manure", PenCell) == 1
                    && Probe.ReservationCount(
                        MigratedProducerOutcomeKind.AnimalHusbandryWorkResult)
                        == reservationsBefore + 1
                    && Probe.CommitCount(
                        MigratedProducerOutcomeKind.AnimalHusbandryWorkResult)
                        == commitsBefore + 1,
                "Terminal husbandry restore duplicated output or parent result.");
        }

        internal void VerifyTaming()
        {
            WildlifeInstanceId animalId = RestoreAnimal(
                AnimalHusbandryWorkKind.Tame,
                nextSequence: 20L);
            Require(runtime.ApplyWork(
                    pen,
                    worker,
                    animalId,
                    AnimalHusbandryWorkKind.Tame,
                    100000f,
                    out bool completed)
                && completed
                && runtime.TryGetAnimal(animalId, out HusbandryAnimalState state)
                && state.Tamed
                && captured.Single().isTamed
                && runtime.Capture().pendingOutcomes.Count == 0,
                "Actual taming path did not commit capture, aggregate, and ledger state.");
            RequireWorkReceipt();
        }

        internal void VerifyProductForwardRecovery()
        {
            WildlifeInstanceId animalId = RestoreAnimal(
                AnimalHusbandryWorkKind.CollectProduct,
                nextSequence: 30L);
            WildlifeHusbandryProductDefinition product =
                species.Husbandry.Products[0];
            Probe.TargetKind = MigratedProducerOutcomeKind.AnimalHusbandryWorkResult;
            Probe.FaultMode = MigratedProducerOutcomeFaultMode.RejectCommit;
            Require(runtime.ApplyWork(
                    pen,
                    worker,
                    animalId,
                    AnimalHusbandryWorkKind.CollectProduct,
                    100000f,
                    out bool completed)
                && completed,
                "Actual product collection did not finish its domain mutation.");
            int physicalQuantity = itemStore.QuantityAt(
                product.ItemId,
                PenCell);
            DungeonAnimalHusbandrySaveData pending = runtime.Capture();
            Require(pending.pendingOutcomes.Count == 1
                    && physicalQuantity == product.Amount,
                "Rejected product parent did not preserve one saved-forward row and exact output.");
            CapturePhysicalCheckpoint("product-parent-rejected");
            string checkpoint = JsonUtility.ToJson(pending);
            runtime.Restore(runtime.BuildRestore(
                JsonUtility.FromJson<DungeonAnimalHusbandrySaveData>(checkpoint)));
            Probe.FaultMode = MigratedProducerOutcomeFaultMode.None;
            runtime.Tick();
            Require(runtime.Capture().pendingOutcomes.Count == 0
                    && itemStore.QuantityAt(product.ItemId, PenCell)
                        == physicalQuantity,
                "Product parent retry duplicated output or failed to clear the saved-forward row.");
            Require(runtime.TryGetAnimal(animalId, out HusbandryAnimalState state)
                    && state.Products.Single(value =>
                        value.ItemId.Value == product.ItemId).ReadyCycles == 0,
                "Product ready cycle was not consumed exactly once.");
            RequireWorkReceipt();
        }

        internal void VerifyManureReservationRollback()
        {
            WildlifeInstanceId animalId = RestoreAnimal(
                AnimalHusbandryWorkKind.CollectManure,
                nextSequence: 40L);
            string before = JsonUtility.ToJson(runtime.Capture());
            Probe.TargetKind = MigratedProducerOutcomeKind.AnimalHusbandryWorkResult;
            Probe.FaultMode = MigratedProducerOutcomeFaultMode.RejectReservation;
            Require(!runtime.ApplyWork(
                    pen,
                    worker,
                    animalId,
                    AnimalHusbandryWorkKind.CollectManure,
                    100000f,
                    out bool rejectedCompleted)
                && !rejectedCompleted
                && JsonUtility.ToJson(runtime.Capture()) == before
                && itemStore.QuantityAt("resource:manure", PenCell) == 0,
                "Rejected manure reservation changed domain or physical state.");
            CapturePhysicalCheckpoint("manure-reservation-rejected");
            Probe.FaultMode = MigratedProducerOutcomeFaultMode.None;
            Require(runtime.ApplyWork(
                    pen,
                    worker,
                    animalId,
                    AnimalHusbandryWorkKind.CollectManure,
                    100000f,
                    out bool completed)
                && completed
                && itemStore.QuantityAt("resource:manure", PenCell) == 1
                && runtime.TryGetAnimal(animalId, out HusbandryAnimalState state)
                && state.ReadyManureCycles == 0,
                "Actual manure path did not consume one cycle and spawn one physical item.");
            RequireWorkReceipt();
        }

        internal void VerifySlaughter()
        {
            WildlifeInstanceId animalId = RestoreAnimal(
                AnimalHusbandryWorkKind.Slaughter,
                nextSequence: 50L);
            WildlifeActor target = CreateWildlife(animalId.Value);
            string carcassItemId = species.CarcassItemId;
            Probe.TargetKind = MigratedProducerOutcomeKind.AnimalHusbandryWorkResult;
            Probe.FaultMode = MigratedProducerOutcomeFaultMode.RejectCommit;
            Require(runtime.ApplyWork(
                    pen,
                    worker,
                    animalId,
                    AnimalHusbandryWorkKind.Slaughter,
                    100000f,
                    out bool completed)
                && completed
                && !target.IsAlive
                && wildlife.Count == 0
                && captured.Count == 0
                && !runtime.TryGetAnimal(animalId, out _)
                && itemStore.QuantityAt(carcassItemId, PenCell) == 1,
                "Actual slaughter path did not align death, carcass, capture, registry, and aggregate state.");
            Require(runtime.Capture().pendingOutcomes.Count == 1,
                "Rejected slaughter parent did not retain one saved-forward row.");
            CapturePhysicalCheckpoint("slaughter-parent-rejected");
            int carcasses = itemStore.QuantityAt(carcassItemId, PenCell);
            Probe.FaultMode = MigratedProducerOutcomeFaultMode.None;
            runtime.Tick();
            Require(runtime.Capture().pendingOutcomes.Count == 0
                    && itemStore.QuantityAt(carcassItemId, PenCell) == carcasses,
                "Slaughter parent retry duplicated the physical carcass.");
            RequireWorkReceipt();
        }

        private WildlifeInstanceId RestoreAnimal(
            AnimalHusbandryWorkKind kind,
            long nextSequence)
        {
            DestroyWildlife();
            itemStore.Clear();
            productionCarcasses?.ReplaceFreshnessValidated(Array.Empty<WildlifeCarcassFreshnessSaveData>());
            string animalValue = "wild:e02:" + kind.ToString().ToLowerInvariant()
                + ":" + nextAnimalSequence++;
            var animalId = new WildlifeInstanceId(animalValue);
            currentAnimalId = animalValue;
            bool tamed = kind != AnimalHusbandryWorkKind.Tame;
            captured.Clear();
            captured.Add(new CapturedWildlifeState
            {
                wildlifeId = animalValue,
                speciesId = species.SpeciesId,
                penId = PenId,
                penPosition = PenCell,
                capturePosition = PenCell,
                isTamed = tamed,
                transportState = CapturedWildlifeTransportState.Penned
            });
            var products = species.Husbandry.Products
                .Select((value, index) => new AnimalProductProgressSaveData
                {
                    itemDefinitionId = value.ItemId,
                    progressDays = 0f,
                    readyCycles = kind == AnimalHusbandryWorkKind.CollectProduct
                        && index == 0 ? 1 : 0
                })
                .ToList();
            var save = new DungeonAnimalHusbandrySaveData
            {
                nextOutcomeSequence = nextSequence,
                animals = new List<HusbandryAnimalSaveData>
                {
                    new()
                    {
                        animalInstanceId = animalValue,
                        speciesDefinitionId = species.SpeciesId,
                        penBuildingInstanceId = PenId,
                        sex = AnimalSex.Female,
                        ageDays = species.Husbandry.AdultAgeDays + 1f,
                        tamed = tamed,
                        tamingProgress = tamed ? 1f : 0f,
                        readyManureCycles =
                            kind == AnimalHusbandryWorkKind.CollectManure ? 1 : 0,
                        slaughterDesignated =
                            kind == AnimalHusbandryWorkKind.Slaughter,
                        statusCode = tamed
                            ? AnimalHusbandryStatusCode.TamedAnimal
                            : AnimalHusbandryStatusCode.AwaitingTaming,
                        products = products
                    }
                },
                penPolicies = new List<AnimalPenPolicySaveData>
                {
                    new() { penBuildingInstanceId = PenId }
                }
            };
            runtime.Restore(runtime.BuildRestore(save));
            Require(runtime.TryGetAnimal(animalId, out _),
                "Restored animal was not reachable through the production query.");
            CapturePhysicalCheckpoint("prepared-" + kind);
            return animalId;
        }

        private WildlifeActor CreateWildlife(string animalId)
        {
            var value = new GameObject("E02 slaughter target");
            value.AddComponent<SpriteRenderer>();
            WildlifeActor actor = value.AddComponent<WildlifeActor>();
            actor.ConfigureRuntimeServices(
                TabooButcheryCausalTestPorts.Guard<IGridPathSearchBroker>(),
                CharacterAiEditorTestDependencies.WorldRegistry,
                clock,
                new RandomStreamProvider(32452843),
                TabooButcheryCausalTestPorts.Guard<IDoorAccessQuery>());
            actor.Initialize(grid, species, animalId, PenCell);
            wildlife.Add(actor);
            return actor;
        }

        private void RequireWorkReceipt()
        {
            _ = MigratedProducerOutcomeE2EAssertions.RequireExact(
                fixture,
                MigratedProducerOutcomeKind.AnimalHusbandryWorkResult,
                Probe);
        }

        private object HandleCapturePort(MethodInfo method, object[] args)
        {
            switch (method.Name)
            {
                case "CopyCapturedAnimalReferences":
                    var destination = (List<CapturedWildlifeState>)args[0];
                    destination.Clear();
                    destination.AddRange(captured);
                    return null;
                case "TryGetCaptured":
                    CapturedWildlifeState found = captured.FirstOrDefault(value =>
                        value.wildlifeId == (string)args[0]);
                    args[1] = found;
                    return found != null;
                case "TrySetTamed":
                    CapturedWildlifeState tame = captured.FirstOrDefault(value =>
                        value.wildlifeId == (string)args[0]);
                    if (tame == null)
                    {
                        args[2] = "missing";
                        return false;
                    }
                    tame.isTamed = (bool)args[1];
                    args[2] = string.Empty;
                    return true;
                case "TryRelease":
                    int removed = captured.RemoveAll(value =>
                        value.wildlifeId == (string)args[0]);
                    args[1] = removed > 0 ? string.Empty : "missing";
                    return removed > 0;
                case "TryGetPenCapacity":
                    args[1] = 8;
                    return string.Equals((string)args[0], PenId,
                        StringComparison.Ordinal);
                case "get_CapturedAnimals":
                    return captured.AsReadOnly();
                default:
                    throw Unexpected(method);
            }
        }

        private object HandleWildlifePort(MethodInfo method, object[] args)
        {
            switch (method.Name)
            {
                case "get_Wildlife":
                    return wildlife.AsReadOnly();
                case "TryRemoveArrival":
                    WildlifeActor found = wildlife.FirstOrDefault(value =>
                        value != null
                        && value.WildlifeId == (string)args[0]);
                    return found != null && wildlife.Remove(found);
                default:
                    throw Unexpected(method);
            }
        }

        private object HandleCarcassPort(MethodInfo method, object[] args)
        {
            if (method.Name != "TrySpawnCarcass")
                throw Unexpected(method);
            WildlifeActor target = (WildlifeActor)args[0];
            string stackId = itemStore.Add(
                target.Species.CarcassItemId,
                1,
                target.GridPosition);
            args[1] = stackId;
            return true;
        }

        public void Dispose()
        {
            physicalFacts?.Dispose();
            domainFacts.Dispose();
            itemStore.PhysicalRuntime?.Dispose();
            DestroyWildlife();
            if (worker != null)
                CharacterAiEditorTestDependencies.WorldRegistry
                    .UnregisterCharacter(worker);
            if (workerObject != null)
                UnityEngine.Object.DestroyImmediate(workerObject);
            if (workerData != null)
                UnityEngine.Object.DestroyImmediate(workerData);
            if (penObject != null)
                UnityEngine.Object.DestroyImmediate(penObject);
        }

        private void DestroyWildlife()
        {
            foreach (WildlifeActor actor in wildlife.ToArray())
            {
                if (actor == null)
                    continue;
                actor.PrepareForDespawn();
                UnityEngine.Object.DestroyImmediate(actor.gameObject);
            }
            wildlife.Clear();
        }
    }

    private sealed class ControlledDomainFactPublisher :
        IDomainFactPublisher,
        IDomainFactRecoveryPublisher
    {
        private const string HiddenDetail =
            "qa:hidden-husbandry-domain-fact-ack";
        private readonly DomainFactDispatcher inner;

        internal ControlledDomainFactPublisher(
            DomainFactDispatcher inner) => this.inner = inner
            ?? throw new ArgumentNullException(nameof(inner));

        internal bool HideNextAcknowledgement { get; set; }
        internal int ReplayBatchCount { get; private set; }
        internal string LastReplayPublicationKey { get; private set; } =
            string.Empty;

        public bool TryReserve<TFact>(
            in TFact proposed,
            out DomainFactLease lease,
            out string failure) => inner.TryReserve(
            proposed,
            out lease,
            out failure);

        public DomainFactDelivery Publish<TFact>(
            in DomainFactLease lease,
            in TFact actual) => Hide(inner.Publish(lease, actual));

        public bool PublishBatch(
            IReadOnlyList<DomainFactEmission> facts,
            DomainFactDelivery[] results,
            out string failure)
        {
            bool accepted = inner.PublishBatch(facts, results, out failure);
            if (accepted)
            {
                for (int index = 0; index < results.Length; index++)
                    results[index] = Hide(results[index]);
            }
            return accepted;
        }

        public DomainFactDelivery Replay<TFact>(in TFact fact) =>
            Hide(inner.Replay(fact));

        public bool ReplayBatch(
            IReadOnlyList<DomainFactReplay> facts,
            DomainFactDelivery[] results,
            out string failure)
        {
            ReplayBatchCount++;
            LastReplayPublicationKey = facts?.Count == 1
                ? facts[0]?.PublicationKey ?? string.Empty
                : string.Empty;
            bool accepted = inner.ReplayBatch(facts, results, out failure);
            if (accepted)
            {
                for (int index = 0; index < results.Length; index++)
                    results[index] = Hide(results[index]);
            }
            return accepted;
        }

        public void Cancel(in DomainFactLease lease) => inner.Cancel(lease);

        private DomainFactDelivery Hide(DomainFactDelivery delivery)
        {
            if (!HideNextAcknowledgement
                || delivery.Phase is not (DomainFactDeliveryPhase.Delivered
                    or DomainFactDeliveryPhase.AcceptedPendingDelivery))
            {
                return delivery;
            }
            HideNextAcknowledgement = false;
            return new DomainFactDelivery(
                DomainFactDeliveryPhase.InDoubt,
                delivery.OperationKey,
                delivery.PublicationId,
                delivery.Fingerprint,
                HiddenDetail);
        }
    }

    private sealed class RuntimeItemStore
    {
        internal WorldItemStackRuntime PhysicalRuntime { get; set; }
        private readonly List<WorldItemStackSnapshot> stacks = new();
        private readonly FixedMassQuery mass = new();
        private int nextStack = 1;

        internal object Invoke(MethodInfo method, object[] args)
        {
            switch (method.Name)
            {
                case "get_MassQuery":
                    return mass;
                case "SpawnItemAt":
                    string itemId = (string)args[0];
                    int quantity = (int)args[1];
                    Vector2Int position = (Vector2Int)args[2];
                    if (quantity <= 0)
                    {
                        args[5] = 0;
                        return false;
                    }
                    Add(itemId, quantity, position);
                    args[5] = quantity;
                    return true;
                case "GetStacksAt":
                    Vector2Int cell = (Vector2Int)args[0];
                    return stacks.Where(value => value.Position == cell)
                        .ToArray();
                default:
                    throw Unexpected(method);
            }
        }

        internal string Add(string itemId, int quantity, Vector2Int position)
        {
            string id = "stack:e02:" + nextStack++.ToString("D4");
            stacks.Add(new WorldItemStackSnapshot
            {
                StackId = id,
                ItemId = itemId,
                DisplayName = itemId,
                Quantity = quantity,
                UnitWeight = 1f,
                State = WorldItemStackState.Loose,
                Position = position
            });
            return id;
        }

        internal int QuantityAt(string itemId, Vector2Int position) =>
            (PhysicalRuntime == null ? stacks : PhysicalRuntime.GetAllStacks())
            .Where(value => value.Position == position
                && string.Equals(value.ItemId, itemId, StringComparison.Ordinal))
            .Sum(value => value.Quantity);

        internal void Clear()
        {
            if (PhysicalRuntime != null)
            {
                DungeonPhysicalItemSaveData empty = PhysicalRuntime.Capture();
                empty.stacks.Clear();
                PhysicalRuntime.Restore(empty);
            }
            stacks.Clear();
            nextStack = 1;
        }
    }

    private sealed class FixedMassQuery : IPhysicalItemMassQuery
    {
        public long AuthorityRevision => 1L;
        public PhysicalMassGrams GetDefinitionUnitMass(ItemDefinitionId itemId) =>
            itemId.IsValid
                ? new PhysicalMassGrams(1000L)
                : throw new InvalidOperationException("Invalid mass item ID.");
        public PhysicalMassGrams GetPreparedStackUnitMass(
            PhysicalItemMassSubject subject) =>
            GetDefinitionUnitMass(subject.ItemId);
        public PhysicalMassGrams GetStackUnitMass(
            ItemDefinitionId itemId,
            PhysicalItemMassSubject subject) =>
            GetDefinitionUnitMass(itemId);
        public PhysicalMassGrams GetStackTotalMass(PhysicalItemLotSnapshot lot) =>
            GetQuantityMass(lot.Subject.ItemId, lot.Subject, lot.Quantity);
        public PhysicalMassGrams GetQuantityMass(
            ItemDefinitionId itemId,
            PhysicalItemMassSubject subject,
            int quantity) =>
            GetDefinitionUnitMass(itemId).Multiply(quantity);
    }

    private sealed class FixedClock : IGameClock
    {
        public float DeltaTime => 0f;
        public float Time => 100f;
        public int FrameCount => 100;
        public bool IsPaused => true;
    }

    private static T CreatePort<T>(
        Func<MethodInfo, object[], object> handler) where T : class
    {
        T result = DispatchProxy.Create<T, LocalPort>();
        ((LocalPort)(object)result).Handler = handler;
        return result;
    }

    public class LocalPort : DispatchProxy
    {
        internal Func<MethodInfo, object[], object> Handler;
        protected override object Invoke(MethodInfo method, object[] args) =>
            Handler(method, args);
    }

    private static Exception Unexpected(MethodInfo method) =>
        new InvalidOperationException(
            "Unexpected husbandry runtime test port call: "
            + method.DeclaringType?.Name + "." + method.Name);

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    private static void RequireThrows(Action action, string message)
    {
        try
        {
            action();
        }
        catch (InvalidOperationException)
        {
            return;
        }
        throw new InvalidOperationException(message);
    }
}
#endif
