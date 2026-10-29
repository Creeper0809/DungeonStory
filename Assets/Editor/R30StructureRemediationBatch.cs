using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using DungeonStory.Foundation;
using UnityEngine;

public static class R30StructureRemediationBatch
{
    public static void RunFirstContracts() => RunContracts("first", new (string Name, Action Run)[]
    {
        ("pause", VerifyPauseRequests),
        ("capture-start", VerifyRejectedCaptureStarts),
        ("loadout", VerifyLoadoutQueries),
        ("defense-policy", () => Require(DefenseEngagementDebugScenarios.RunPolicyRoundTripFocused(),
            "Defense policy query/create/duplicate isolation or restore failed.")),
        ("weather", () => Debug.Log(SurvivalDebugScenarios.RunWeatherFrontMappingFocused())),
        ("worker-policy", () => Debug.Log(WimCraftQualityEstimateDebugScenarios.RunWorkerPolicyPresentationFocused()))
    });

    public static void RunSecondContracts() => RunContracts("second", SecondSteps());

    public static void RunSecondRemainingContracts() => RunContracts("second-remaining",
        SecondSteps().SkipWhile(step => step.Name != "farm-delay").ToArray());

    public static void RunThirdContracts() => RunContracts("third", ThirdSteps());

    public static void RunThirdRemainingContracts() => RunContracts("third-remaining",
        ThirdSteps().SkipWhile(step => step.Name != "living-membership").ToArray());

    public static void RunThirdRepairsContracts()
    {
        var steps = ThirdSteps().Where(step => step.Name is
            "research-shop" or "generation-command" or "battle-query").ToArray();
        Require(steps.Length == 3, "The three failed R30 boundaries must all be selected.");
        RunContracts("third-repairs", steps);
    }

    public static void RunThirdPendingContracts() => RunContracts("third-pending",
        new (string Name, Action Run)[]
        {
            ("research-shop", () => Require(ResearchTreeDebugScenarios.RunR30StateBoundaryFocused(),
                "Research/shop query, command or restore boundary failed.")),
            ("core-ammo-authoring", ResourceEconomyAssetBuilder.RunR30AmmunitionAuthoringFocused),
            ("research-ammo-authoring", ResearchOverhaulContentAssetBuilder.RunR30AmmunitionAuthoringFocused)
        });

    public static void RunFourthContracts() => RunContracts("fourth",
        new (string Name, Action Run)[]
        {
            ("natural-grafts", () => Debug.Log(SurgeryDebugScenarios.RunR30NaturalGraftEffectsFocused())),
            ("defense-preparation", MigratedProducerCombatDefenseEntryE2EDebugScenarios.RunR30DefensePreparationFocused),
            ("core-ammo-authoring", ResourceEconomyAssetBuilder.RunR30AmmunitionAuthoringFocused),
            ("research-ammo-authoring", ResearchOverhaulContentAssetBuilder.RunR30AmmunitionAuthoringFocused)
        });

    public static void RunAmmunitionAuthoringContracts() => RunContracts("ammunition-authoring",
        new (string Name, Action Run)[]
        {
            ("core-ammo-authoring", ResourceEconomyAssetBuilder.RunR30AmmunitionAuthoringFocused),
            ("research-ammo-authoring", ResearchOverhaulContentAssetBuilder.RunR30AmmunitionAuthoringFocused)
        });

    private static IReadOnlyList<(string Name, Action Run)> ThirdSteps() =>
        SecondSteps().SkipWhile(step => step.Name != "runtime-profile")
        .Concat(new (string Name, Action Run)[]
        {
            ("research-shop", () => Require(ResearchTreeDebugScenarios.RunR30StateBoundaryFocused(),
                "Research/shop query, command or restore boundary failed.")),
            ("generation-command", InstanceEvolutionDebugScenarios.RunR30GenerationCommandFocused),
            ("grand-project-benefits", ProductionEconomyDebugScenarios.RunR30GrandProjectBenefitsFocused),
            ("expedition-supply", () => Debug.Log(OffenseStrategicDebugScenarios.RunR30ExpeditionSupplyCapacityFocused())),
            ("ammo-profiles", CombatSystemDebugScenarios.RunR30AmmunitionProfilesFocused),
            ("counter-metadata", () => Debug.Log(OffenseStrategicDebugScenarios.RunR30CounterMetadataFocused())),
            ("battle-query", OffenseRewardDebugScenarios.RunR30BattleQueryFocused),
            ("manual-location", CharacterSkillActivationOutcomeDebugScenarios.RunR30ManualLocationFocused),
            ("crop-cache", EnvironmentCropProductionEntryDebugScenarios.VerifyCropQueryCacheBoundaryForBatch),
            ("carry-restore-atomic", VerifyCarryRestoreAtomicity),
            ("seed-corruption", VerifyPhysicalSeedCorruption)
        }).ToArray();

    private static void VerifyCarryRestoreAtomicity()
    {
        var host = new GameObject("R30 carry restore");
        var restoredHost = new GameObject("R30 restored carry");
        host.SetActive(false);
        restoredHost.SetActive(false);
        try
        {
            CharacterCarryInventory inventory = host.AddComponent<CharacterCarryInventory>();
            inventory.Restore(new CharacterCarryInventorySaveData { items = new()
            {
                new() { itemId = "food:preserved-ration", quantity = 2 }
            }});
            int changes = 0;
            inventory.Changed += () => changes++;
            string before = JsonUtility.ToJson(inventory.Capture());
            var invalid = new CharacterCarryInventorySaveData { items = new()
            {
                new() { itemId = "equipment:query-fixture", quantity = 1, itemInstanceId = "item-instance:r30:duplicate" },
                new() { itemId = "equipment:query-fixture", quantity = 1, itemInstanceId = "item-instance:r30:duplicate" }
            }};
            bool rejected = false;
            try { inventory.Restore(invalid); }
            catch (InvalidOperationException) { rejected = true; }
            Require(rejected && changes == 0 && JsonUtility.ToJson(inventory.Capture()) == before,
                "Failed carry restore erased or partly replaced live inventory.");
            invalid.items[1].itemInstanceId = "item-instance:r30:repaired";
            inventory.Restore(invalid);
            string repaired = JsonUtility.ToJson(inventory.Capture());
            CharacterCarryInventory fresh = restoredHost.AddComponent<CharacterCarryInventory>();
            fresh.Restore(JsonUtility.FromJson<CharacterCarryInventorySaveData>(repaired));
            Require(changes == 1 && JsonUtility.ToJson(fresh.Capture()) == repaired,
                "Repaired carry restore failed to publish once or preserve fresh-owner JSON state.");
            inventory.Restore(JsonUtility.FromJson<CharacterCarryInventorySaveData>(repaired));
            Require(JsonUtility.ToJson(inventory.Capture()) == repaired,
                "Carry restore retry duplicated cargo.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(host);
            UnityEngine.Object.DestroyImmediate(restoredHost);
        }
    }

    private static void VerifyPhysicalSeedCorruption()
    {
        var content = new ResourceGameContentCatalog(new UnityGameContentRootLoader());
        CropDefinitionSO crop = content.GetAll<CropDefinitionSO>().First();
        var seed = new SeedLotState { cropId = crop.CropId, cultivarGenomeId = crop.BaseGenome.GenomeId };
        ItemInstanceComponentSaveData valid = SeedLotItemStateCodec.Encode(seed);
        ItemInstanceComponentSaveData malformed = valid.Clone();
        malformed.schemaVersion = 0;
        var physicalSeed = new WorldItemStackSnapshot
        {
            StackId = "stack:r30:seed", ItemId = "seed:r30:fixture", Quantity = 1,
            Components = new[] { malformed }
        };
        var stock = new SeedReferenceProbe { Rows = new[]
        {
            new WorldItemStackSnapshot { ItemId = "material:lumber", Quantity = 1 }, physicalSeed
        }};
        var random = new DungeonStory.Foundation.RandomStreamProvider(51003);
        var runtime = new CropEcologyRuntime(new DungeonRuntimeAggregateRootStore(), content, random, stock);
        runtime.Sow("plot:r30:seed", crop.FamilyGroup, seed);
        string before = JsonUtility.ToJson(runtime.Capture());
        int version = runtime.Version;
        ulong randomBefore = random.Get(CropEcologyRuntime.GeneticsRandomStreamId).State;
        bool rejected = false;
        try { runtime.Harvest("plot:r30:seed"); }
        catch (InvalidOperationException) { rejected = true; }
        Require(rejected && runtime.Version == version && JsonUtility.ToJson(runtime.Capture()) == before
            && random.Get(CropEcologyRuntime.GeneticsRandomStreamId).State == randomBefore,
            "Malformed physical seed was ignored or partially changed harvest state/randomness.");
        physicalSeed.Components = new[] { valid };
        runtime.Harvest("plot:r30:seed");
        runtime.Sow("plot:r30:pending", crop.FamilyGroup, seed);
        runtime.PrepareHarvest("r30:prepared-harvest", "plot:r30:pending");
        var restored = new CropEcologyRuntime(new DungeonRuntimeAggregateRootStore(), content,
            new DungeonStory.Foundation.RandomStreamProvider(51003), stock);
        restored.PublishRestore(restored.PrepareRestore(JsonUtility.FromJson<CropEcologyWorldSaveData>(
            JsonUtility.ToJson(runtime.Capture()))));
        string pending = JsonUtility.ToJson(restored.Capture());
        physicalSeed.Components = new[] { malformed };
        rejected = false;
        try { restored.CommitPreparedHarvest("r30:prepared-harvest"); }
        catch (InvalidOperationException) { rejected = true; }
        Require(rejected && JsonUtility.ToJson(restored.Capture()) == pending,
            "Restored harvest accepted a malformed physical genome reference.");
        physicalSeed.Components = new[] { valid };
        restored.CommitPreparedHarvest("r30:prepared-harvest");
        string committed = JsonUtility.ToJson(restored.Capture());
        restored.CommitPreparedHarvest("r30:prepared-harvest");
        Require(JsonUtility.ToJson(restored.Capture()) == committed,
            "Repaired prepared-harvest retry applied genetics twice.");
    }

    private sealed class SeedReferenceProbe : IStockQuery
    {
        public IReadOnlyList<WorldItemStackSnapshot> Rows;
        public IReadOnlyList<WorldItemStackSnapshot> GetAllStacks() => Rows;
        public int GetGlobalQuantity(string itemDefinitionId) => throw new InvalidOperationException("Unexpected stock query.");
        public int GetWarehouseQuantity(BuildingInstanceId warehouseId, string itemDefinitionId) =>
            throw new InvalidOperationException("Unexpected warehouse query.");
        public int GetWarehouseQuantity(BuildingInstanceId warehouseId, StockCategory category) =>
            throw new InvalidOperationException("Unexpected warehouse query.");
        public int GetWarehouseTotal(BuildingInstanceId warehouseId) =>
            throw new InvalidOperationException("Unexpected warehouse query.");
    }

    private static IReadOnlyList<(string Name, Action Run)> SecondSteps() => new (string Name, Action Run)[]
    {
        ("derived-stats", VerifyDerivedStatsIsolation),
        ("usage-links", VerifyUsageLinkIsolation),
        ("reference-context-basic", VerifyExplicitReferenceContext),
        ("reference-context-six", ContentReferenceSelectionDebugScenarios.RunR30ExplicitContextFocused),
        ("legacy-capacity", ProductionEconomyDebugScenarios.RunR30LegacyOutputCapacityFocused),
        ("mass-registry", ProductionMassExplanationCapabilityDebugScenarios.RunR30RegistryConsistencyFocused),
        ("regular-customer", () => Require(RegularCustomerDebugScenarios.RunR30SnapshotIsolationFocused(),
            "Regular-customer query/save/retry isolation failed.")),
        ("meta", () => Require(MetaProgressionDebugScenarios.RunR30SnapshotIsolationFocused(),
            "Meta query/save/retry isolation failed.")),
        ("expedition", OffenseJourneyDebugScenarios.RunExpeditionQueryProjection),
        ("population", () => Require(CharacterPopulationDebugScenarios.RunR30PopulationQueryFocused(),
            "Population query/save/rebind isolation failed.")),
        ("visitor-exit", () => Require(VisitorExitOutcomeDebugScenarios.RunAll(true),
            "Visitor exit rollback/save/retry contracts failed.")),
        ("circus", CaptivityCircusDebugScenarios.RunR30PerformerEligibilityFocused),
        ("farm-delay", V20CampaignDebugScenarios.RunR30FarmDelayFocused),
        ("runtime-profile", () => Require(CharacterModelDebugScenarios.RunR30RuntimeProfileFocused(),
            "Runtime-profile collection isolation failed.")),
        ("alerts", EmergencyLaborDebugScenarios.RunR30AlertQueryFocused),
        ("all-stacks", PhysicalStockQueryV18DebugScenarios.RunR30AllStackQueryFocused),
        ("living-membership", R19ProgressionDebugScenarios.RunR30LivingMembershipFocused),
        ("regional-contract", () => Debug.Log(RegionalSupplyContractTransferOutboxDebugScenarios.Verify()))
    };

    private static void RunContracts(string batch, IReadOnlyList<(string Name, Action Run)> steps)
    {
        Require(steps != null && steps.Count > 0, "No R30 verification steps were selected.");
        Require(Application.isBatchMode && !Application.isPlaying
            && DungeonPersistencePaths.IsIsolatedVerification,
            "R30 contracts require isolated edit-mode persistence.");
        string output = Environment.GetEnvironmentVariable("DUNGEONSTORY_AUDIT_REMEDIATION_OUTPUT");
        Require(!string.IsNullOrWhiteSpace(output), "Missing R30 evidence directory.");
        string source = V27CurrentSourceEvidenceDigest.ComputeAllScriptsDigest();
        var errors = new List<string>();
        void Observe(string message, string stack, LogType type)
        {
            if (type is LogType.Error or LogType.Exception or LogType.Assert)
                errors.Add(message);
        }
        Application.logMessageReceived += Observe;
        try
        {
            var failures = new List<Exception>();
            foreach (var step in steps)
            {
                Debug.Log("R30_STEP_STARTED=" + step.Name);
                var elapsed = System.Diagnostics.Stopwatch.StartNew();
                int previousErrors = errors.Count;
                try
                {
                    step.Run();
                    Require(errors.Count == previousErrors,
                        step.Name + ": " + string.Join(" | ", errors.Skip(previousErrors)));
                    Debug.Log($"R30_STEP=PASS; name={step.Name}; seconds={elapsed.Elapsed.TotalSeconds:0.###}");
                }
                catch (Exception failure)
                {
                    failures.Add(new InvalidOperationException(step.Name, failure));
                    Debug.Log($"R30_STEP=FAIL; name={step.Name}; seconds={elapsed.Elapsed.TotalSeconds:0.###}; error={failure}");
                }
            }
            if (failures.Count != 0)
                throw new AggregateException("R30 contracts failed; no batch acceptance was written.", failures);
            Require(errors.Count == 0, string.Join(" | ", errors));
            Require(source == V27CurrentSourceEvidenceDigest.ComputeAllScriptsDigest(),
                "Source changed during R30 verification.");
            using var writer = new StreamWriter(new FileStream(
                Path.Combine(output, "r30-" + batch + "-contracts.txt"), FileMode.CreateNew));
            writer.WriteLine("result=PASS\nsource=" + source
                + "\nconsoleErrors=0\nboundary=native controlled " + batch + " contracts");
            Debug.Log("R30_" + batch.ToUpperInvariant() + "_CONTRACTS=PASS");
        }
        finally { Application.logMessageReceived -= Observe; }
    }

    private static void VerifyPauseRequests()
    {
        var host = new GameObject("R30 pause owner");
        host.SetActive(false);
        var settings = ScriptableObject.CreateInstance<GameData>();
        try
        {
            GameManager manager = host.AddComponent<GameManager>();
            typeof(GameManager).GetField("gameDataSettings", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(manager, settings);
            var references = new DungeonSceneRuntimeReferences(
                new DungeonSceneServiceReferences(null, null, null, null),
                new DungeonSceneViewReferences(null, manager, null, null, null, null, null, null));
            var time = new ProbeTimeScale();
            var store = new ScopedGameSessionStateStore(references, time);
            var safety = new ProbeRestoreSafety();
            var speed = new GameSpeedController(store, store, time, safety);
            speed.SetSpeed(3);
            IDisposable research = speed.AcquirePause();
            IDisposable options = speed.AcquirePause();
            Require(speed.IsPaused && time.Scale == 0f && !store.CaptureSession().isPaused,
                "UI pause either failed or became the persisted player pause preference.");
            research.Dispose();
            research.Dispose();
            Require(speed.IsPaused && time.Scale == 0f, "First/duplicate release resumed another window.");
            options.Dispose();
            Require(!speed.IsPaused && time.Scale == 3f, "Last release did not resume the current speed.");

            speed.SetPaused(true);
            research = speed.AcquirePause();
            options = speed.AcquirePause();
            options.Dispose();
            research.Dispose();
            Require(speed.IsPaused && store.CaptureSession().isPaused && time.Scale == 0f,
                "Reverse window close erased manual pause.");
            speed.SetPaused(false);

            research = speed.AcquirePause();
            store.Restore(new GameSessionSnapshot(5000, 2, 4, 0f, 0, TimeOfDay.Night, false));
            Require(speed.IsPaused && time.Scale == 0f, "Restore discarded an open pause request.");
            GameSessionSaveData saved = JsonUtility.FromJson<GameSessionSaveData>(
                JsonUtility.ToJson(store.CaptureSession()));
            Require(!saved.isPaused && saved.gameSpeed == 4, "Pause/save preference or speed changed.");
            store.BeginRestoreCandidate();
            store.StageSessionRestore(new GameSessionSnapshot(4000, 3, 2, 0f, 0, TimeOfDay.Night, false));
            store.PublishRestoreCandidate();
            research.Dispose();
            Require(!speed.IsPaused && time.Scale == 2f, "Closing UI during restore retained its pause.");
            store.RollbackPublishedRestoreCandidate();
            Require(!speed.IsPaused && time.Scale == 4f && store.CaptureSession().absoluteDay == 2,
                "Rollback restored a stale UI pause or failed to restore the session.");
            store.Restore(saved.ToSnapshot());
            Require(!speed.IsPaused && time.Scale == 4f, "Serialized preference did not restore.");

            research = speed.AcquirePause();
            safety.RequiresRecovery = true;
            research.Dispose();
            Require(speed.IsPaused && time.Scale == 0f, "Window release bypassed restore recovery pause.");
            bool refused = false;
            try { speed.SetPaused(false); }
            catch (InvalidOperationException) { refused = true; }
            Require(refused && speed.IsPaused && time.Scale == 0f,
                "Manual resume bypassed restore recovery admission.");

            var titleTime = new ProbeTimeScale();
            var title = new DungeonTitleLifetimeScope.TitleGameSpeedController(titleTime);
            research = title.AcquirePause();
            options = title.AcquirePause();
            research.Dispose();
            Require(title.IsPaused && titleTime.Scale == 0f, "Title released another window pause.");
            options.Dispose();
            Require(!title.IsPaused && titleTime.Scale == 1f, "Title retained a released pause.");
            Debug.Log("R30_PAUSE_OWNER=PASS; nested/reverse/duplicate/manual/save/restore/rollback/recovery/title");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(host);
            UnityEngine.Object.DestroyImmediate(settings);
        }
    }

    private static void VerifyRejectedCaptureStarts()
    {
        var host = new GameObject("R30 rejected capture starts");
        host.SetActive(false);
        try
        {
            var escort = host.AddComponent<AbilityCaptiveEscort>();
            var wildlife = host.AddComponent<AbilityWildlifeCaptureTransport>();
            var escortPort = new RejectedCapturePort();
            var wildlifePort = new RejectedCapturePort();
            escort.Configure(escortPort, new ProbeClock());
            wildlife.Configure(wildlifePort);
            Require(!escort.TryStartEscort("captive:r30", out string escortFailure)
                && escortFailure == RejectedCapturePort.Reason
                && !escort.IsEscorting && escortPort.Failures == 1,
                "Escort ownership refusal did not reach its caller/cleanup.");
            Require(!wildlife.TryStartTransport("wildlife:r30", out string wildlifeFailure)
                && wildlifeFailure == RejectedCapturePort.Reason
                && !wildlife.IsTransporting && wildlifePort.Failures == 1,
                "Wildlife ownership refusal did not reach its caller/cleanup.");
            Require(!escort.TryStartEscort(" ", out _) && escortPort.Failures == 1
                && !wildlife.TryStartTransport(" ", out _) && wildlifePort.Failures == 1,
                "Invalid target unexpectedly started or repeated physical cleanup.");
            Debug.Log("R30_CAPTURE_START_REJECTION=PASS; controlled rejecting ports; natural capture is separate");
        }
        finally { UnityEngine.Object.DestroyImmediate(host); }
    }

    private static void VerifyLoadoutQueries()
    {
        var runtime = (CombatEquipmentRuntime)OffenseEditorTestDependencies.CreateCombatEquipmentRuntime();
        const string id = "character:r30:loadout";
        CharacterCombatLoadoutState view = runtime.GetOrCreateLoadout(id);
        string original = Newtonsoft.Json.JsonConvert.SerializeObject(runtime.Capture());
        view.activeProfileId = "profile:query-only";
        view.profiles[0].desiredWeaponDefinitionIds.Add("weapon:query-only");
        view.profiles.Clear();
        Require(Newtonsoft.Json.JsonConvert.SerializeObject(runtime.Capture()) == original
            && runtime.GetOrCreateLoadout(id).activeProfileId == CombatLoadoutPresetIds.Peace,
            "Public loadout query changed persisted profiles.");
        Require(runtime.TrySetActiveProfile(id, CombatLoadoutPresetIds.Combat)
            && runtime.GetOrCreateLoadout(id).activeProfileId == CombatLoadoutPresetIds.Combat,
            "Loadout command updated only a detached query.");
        string changed = Newtonsoft.Json.JsonConvert.SerializeObject(runtime.Capture());
        Require(!runtime.TrySetActiveProfile(id, "profile:missing")
            && Newtonsoft.Json.JsonConvert.SerializeObject(runtime.Capture()) == changed,
            "Rejected loadout command changed owner state.");
        var saved = Newtonsoft.Json.JsonConvert.DeserializeObject<DungeonCombatEquipmentSaveData>(changed);
        var restored = (CombatEquipmentRuntime)OffenseEditorTestDependencies.CreateCombatEquipmentRuntime();
        restored.PublishRestoreCandidate(restored.BuildRestoreCandidate(saved));
        Require(restored.GetOrCreateLoadout(id).activeProfileId == CombatLoadoutPresetIds.Combat,
            "Loadout profile did not survive a fresh-owner JSON restore.");
        Debug.Log("R30_LOADOUT_QUERY=PASS; nested isolation/owner command/rejection/fresh-owner JSON restore");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private static void VerifyDerivedStatsIsolation()
    {
        var values = new Dictionary<string, float> { ["r30:stat"] = 2f };
        var row = new GameplayEffectContribution
        {
            EffectId = "r30:effect", BindingId = "r30:binding", AuthoredValue = 2f,
            AppliedValue = 2f, Source = new GameplayEffectSourceRef(GameplayEffectSourceKind.Status, "r30:source")
        };
        var rows = new[] { row };
        var snapshot = new CharacterDerivedStatsSnapshot("r30:revision", values, rows);
        values["r30:stat"] = 999f;
        row.AppliedValue = 999f;
        rows[0] = null;
        snapshot.Contributions[0].AppliedValue = 888f;
        snapshot.Contributions[0].Source = default;
        var exposed = (IDictionary<string, float>)snapshot.Values;
        bool rejected = false;
        try { exposed["r30:stat"] = 888f; }
        catch (NotSupportedException) { rejected = true; }
        Require(rejected && snapshot.Get("r30:stat") == 2f
            && snapshot.Contributions[0].AppliedValue == 2f
            && snapshot.Contributions[0].Source.SourceId == "r30:source",
            "Derived snapshot accepted caller mutations at the same revision.");
        Debug.Log("R30_DERIVED_STATS_QUERY=PASS");
    }

    private static void VerifyUsageLinkIsolation()
    {
        var content = new ResourceGameContentCatalog(new UnityGameContentRootLoader());
        IWorldItemStackRuntime items = BranchedProductionNetworkDebugScenarios.R28CallProxy.Create<IWorldItemStackRuntime>(
            (method, _) => method.Name switch
            {
                "get_ItemStackVersion" => 0,
                nameof(IWorldItemStackRuntime.GetAllStacks) => Array.Empty<WorldItemStackSnapshot>(),
                _ => throw new InvalidOperationException("Unexpected item call: " + method.Name)
            });
        var index = new ResourceUsageIndex(new ResourceEconomyContentCatalog(content), items,
            new ResourceCombatEquipmentCatalog(content), content);
        var first = index.GetConsumers("resource:log");
        Require(first.Count > 0, "Expected authored log consumers.");
        string expected = first[0].consumerId;
        first[0].consumerId = "r30:external";
        first[0].requiredResearchId = "r30:external";
        first[0].kind = ProductionConsumerKind.DefenseAmmunition;
        var second = index.GetDependency("resource:log").ConsumerLinks;
        Require(second[0].consumerId == expected
            && second.All(link => link.consumerId != "r30:external"
                && link.requiredResearchId != "r30:external"),
            "A usage link query changed the shared dependency catalog.");
        Debug.Log("R30_USAGE_LINK_QUERY=PASS");
    }

    private static void VerifyExplicitReferenceContext()
    {
        var definition = ScriptableObject.CreateInstance<LifeEventDefinitionSO>();
        var fixture = ScriptableObject.CreateInstance<RenamedReferenceFixture>();
        var missing = ScriptableObject.CreateInstance<MissingReferenceContextFixture>();
        try
        {
            definition.ConfigureMetadata("r30:life-event", "R30", "R30", 1, "R30");
            fixture.renamedRows.Add(new V20WeightedId { id = definition.StableId });
            missing.rows.Add(new V20WeightedId { id = definition.StableId });
            var catalog = new ContentReferenceCatalog(new ScriptableObject[] { definition });
            Require(catalog.Validate(new ScriptableObject[] { fixture }).Count == 0,
                "An explicitly typed renamed reference was rejected.");
            using var serialized = new UnityEditor.SerializedObject(fixture);
            var property = serialized.FindProperty("renamedRows.Array.data[0].id");
            Require(ContentReferenceSelection.ResolveReference(
                    new ContentReferenceAttribute(ContentReferenceKind.WeightedTarget), property)?.Kind
                    == ContentReferenceKind.LifeEvent,
                "Inspector lost reference context after a container rename.");
            fixture.renamedRows[0].id = "r30:missing";
            Require(catalog.Validate(new ScriptableObject[] { fixture }).Count > 0,
                "A renamed reference field bypassed invalid-ID validation.");
            Require(catalog.Validate(new ScriptableObject[] { missing })
                    .Any(error => error.Contains("missing explicit container reference context")),
                "An unresolved reference annotation was silently skipped.");
            Require(ContentReferenceCatalog.ResolveContainerContext(typeof(FacilityEvolutionRecipeSO),
                    "requiredRoomScores.Array.data[0].key") == ContentReferenceKind.RoomScore
                && ContentReferenceCatalog.ResolveContainerContext(typeof(FacilityEvolutionRecipeSO),
                    "requiredRoomMetrics.Array.data[0].key") == ContentReferenceKind.RoomMetric,
                "Room score and metric metadata were conflated.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(definition);
            UnityEngine.Object.DestroyImmediate(fixture);
            UnityEngine.Object.DestroyImmediate(missing);
        }
        Debug.Log("R30_EXPLICIT_REFERENCE_CONTEXT=PASS");
    }

    private sealed class RenamedReferenceFixture : ScriptableObject
    {
        [ContentReferenceContext(ContentReferenceKind.LifeEvent)]
        public List<V20WeightedId> renamedRows = new();
    }

    private sealed class MissingReferenceContextFixture : ScriptableObject
    {
        public List<V20WeightedId> rows = new();
    }

    private sealed class ProbeTimeScale : IGameTimeScaleController
    { public float Scale { get; set; } = 1f; }

    private sealed class ProbeRestoreSafety : IDungeonRestoreSafetyQuery
    {
        public bool RequiresRecovery { get; set; }
        public string RecoveryMessage => "R30 controlled recovery requirement";
    }

    private sealed class ProbeClock : IGameClock
    {
        public float Time => 1f;
        public float DeltaTime => 0f;
        public int FrameCount => 1;
        public bool IsPaused => true;
    }

    private sealed class RejectedCapturePort : ICaptiveEscortAbilityPort, IWildlifeCaptureTransportAbilityPort
    {
        public const string Reason = "r30:action-owner-busy";
        public int Failures { get; private set; }
        public Vector2Int Position => default;
        public bool TryBeginActionOwnership(string id, out string reason)
        { reason = Reason; return false; }
        public bool HasActionOwnership() => false;
        public void EndActionOwnership(bool clearFailures) { }
        public void CancelActionOwnership() { }
        public void FailEscort(string id, string reason) { Require(reason == Reason, "Escort reason changed."); Failures++; }
        public void FailCarry(string id, string reason) { Require(reason == Reason, "Wildlife reason changed."); Failures++; }
        public void SetActionPhase(string phase, string detail) { }
        public void RequestImmediateReplan(bool clearFailures) { }
        public bool TryGetState(string id, out CaptiveState state, out Vector2Int position,
            out string name, out string reason) => throw Unexpected();
        public bool TryGetTransportState(string id, out CapturedWildlifeState state,
            out Vector2Int position, out string name, out string reason) => throw Unexpected();
        public CaptivityMovementResolution ResolveMovement(Vector2Int destination,
            CaptivityAbilityAccessKind access, out IEnumerator movement) => throw Unexpected();
        public bool TryCreateMovement(Vector2Int destination, CaptivityAbilityAccessKind access,
            out IEnumerator movement) => throw Unexpected();
        public CaptivityMovementResolution ResolvePickupMovement(Vector2Int position,
            out Vector2Int stand, out IEnumerator movement) => throw Unexpected();
        public bool TryCreateDeliveryMovement(Vector2Int destination, out IEnumerator movement) => throw Unexpected();
        public bool TryValidateMovementArrival(Vector2Int destination, out string reason) => throw Unexpected();
        public bool TryPickupReservedRestraint(CaptiveState state, out string reason) => throw Unexpected();
        public float AdvanceStabilization(string id, float delta) => throw Unexpected();
        public bool TryBeginEscort(string id, out string reason) => throw Unexpected();
        public bool TryBeginCarry(string id, out string reason) => throw Unexpected();
        public IDisposable BeginEscortPass(string id) => throw Unexpected();
        public IDisposable BeginTransportPass(string id) => throw Unexpected();
        public bool TryCompleteEscort(string id, out string reason) => throw Unexpected();
        public bool TryCompleteCarry(string id, out string reason) => throw Unexpected();
        public WildlifeDeliveryStandResolution ResolveDeliveryStand(string id,
            out CapturedWildlifeState state, out string reason) => throw Unexpected();
        private static Exception Unexpected() => new InvalidOperationException(
            "Rejected capture attempted movement, physical work or completion.");
    }
}
