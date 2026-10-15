#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DungeonStory.Foundation;
using DungeonStory.Foundation.Facts;
using Newtonsoft.Json.Linq;
using UnityEngine;

public static class DiseaseAnatomyBurdenProjectionDebugScenarios
{
    public static void RunCA02FromBatch()
    {
        if (!Application.isBatchMode
            || !string.Equals(
                Environment.GetEnvironmentVariable(
                    "DUNGEONSTORY_U0_CA02_DISEASE_ANATOMY_VERIFY"),
                "1",
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "C:A02 verification requires explicit isolated batch mode.");
        }

        string output = Environment.GetEnvironmentVariable(
            "DUNGEONSTORY_AUDIT_REMEDIATION_OUTPUT");
        if (string.IsNullOrWhiteSpace(output))
        {
            throw new InvalidOperationException(
                "C:A02 verification requires its isolated output directory.");
        }
        string directory = Path.GetFullPath(output);
        Directory.CreateDirectory(directory);
        string reportPath = Path.Combine(directory, "report.json");
        if (File.Exists(reportPath))
        {
            throw new InvalidOperationException(
                "C:A02 report already exists: " + reportPath);
        }

        var report = new JObject
        {
            ["rowId"] = "C:A02",
            ["passed"] = false,
            ["burdenZeroExact"] = false,
            ["applicationRejectionRollbackExact"] = false,
            ["partialEffectExact"] = false,
            ["multipleDiseasesExact"] = false,
            ["saveRestoreExactlyOnce"] = false,
            ["productionProgressionReached"] = false,
            ["productionBodyRuntimeReached"] = false,
            ["returnedChangesMatchProjections"] = false,
            ["nodeBeforeAfterExact"] = false,
            ["oldSaveMigrationAdded"] = false,
            ["fullAuditExecuted"] = false,
            ["humanApprovalRecorded"] = false,
            ["trainingExecuted"] = false,
            ["sourceFiles"] = new JArray
            {
                "Assets/Scripts/Models/Species/Core/PopulationHealthDomain.cs",
                "Assets/Scripts/Models/Species/Core/DiseaseAnatomyBurdenAppliedFact.cs",
                "Assets/Scripts/Services/Character/PopulationHealthRuntime.cs",
                "Assets/Scripts/Services/Character/DiseaseAnatomyBurdenProjectionRuntime.cs",
                "Assets/Scripts/Services/Combat/CharacterBodyHealthRuntime.cs",
                "Assets/Scripts/Services/Narrative/GameplayOutcomeBridges/Environment/DiseaseAnatomyBurdenFactMapping.cs",
                "Assets/Scripts/Services/Narrative/GameplayOutcomeBridges/Environment/DiseaseAnatomyBurdenFactPreflight.cs",
                "Assets/Scripts/Services/Character/Editor/DiseaseAnatomyBurdenProjectionDebugScenarios.cs"
            }
        };

        try
        {
            Require(RunAll(logSuccess: false),
                "C:A02 core scenario returned false.");
            JObject production = VerifyProductionProgressionAndBody();
            report["burdenZeroExact"] = true;
            report["applicationRejectionRollbackExact"] = true;
            report["partialEffectExact"] = true;
            report["multipleDiseasesExact"] = true;
            report["saveRestoreExactlyOnce"] = true;
            report["productionProgressionReached"] = true;
            report["productionBodyRuntimeReached"] = true;
            report["returnedChangesMatchProjections"] = true;
            report["nodeBeforeAfterExact"] = true;
            report["production"] = production;
            report["passed"] = true;
            WriteCreateOnlyJson(reportPath, report);
            Debug.Log(
                "[AuditRemediation][C:A02] PASS burden0=1; rejection=1; "
                + "partial=1; multi-disease=1; save-retry=1; "
                + "production-progression=1; production-body=1");
        }
        catch (Exception exception)
        {
            report["failure"] = exception.GetType().Name
                + ": "
                + exception.Message;
            if (!File.Exists(reportPath))
                WriteCreateOnlyJson(reportPath, report);
            Debug.LogException(exception);
            throw;
        }
    }

    public static bool RunAll(bool logSuccess = false)
    {
        GameObject host = new("Disease Anatomy Burden Projection QA");
        try
        {
            CharacterActor actor = host.AddComponent<CharacterActor>();
            CharacterAiEditorTestDependencies.Inject(host);
            actor.EnsureRuntimeState();
            actor.Identity.SetPersistentId("character:qa:disease-anatomy");
            var catalog = new DiseaseCatalog();
            var profiles = new ProfileCatalog();
            using var outcome = new OutcomeFixture();
            var world = new World(actor);

            PopulationHealthAggregateState zero = Empty(catalog);
            zero.EnqueueAnatomyBurdenProjection(
                Change(actor, DiseaseCatalog.CoreId,
                    DiseaseTargetSystem.Core, 0f, 20),
                0f);
            Require(zero.PendingAnatomyBurdenProjections.Count == 0,
                "Zero disease burden created a projection.");

            var noNodeBody = new BodyFixture(actor.BuildingCharacterId);
            StoreFixture noNodeStore = Store(
                catalog,
                Change(actor, DiseaseCatalog.CoreId,
                    DiseaseTargetSystem.Core, 12f, 21),
                3f);
            var noNodeRuntime = Runtime(
                noNodeStore, world, profiles, noNodeBody, outcome);
            string noNodeOperation = noNodeStore.SingleOperation;
            Require(!noNodeRuntime.TryResume(
                    noNodeOperation,
                    out string noNodeFailure)
                    && noNodeFailure ==
                        "disease-anatomy-burden-node-unavailable"
                    && noNodeStore.PendingAnatomyBurdenProjections.Count == 1
                    && noNodeBody.ApplyCount == 0,
                "Missing anatomy node was hidden or mutated state.");

            var successBody = new BodyFixture(
                actor.BuildingCharacterId,
                "node:core",
                "node:lung");
            StoreFixture successStore = Store(
                catalog,
                Change(actor, DiseaseCatalog.CoreId,
                    DiseaseTargetSystem.Core, 20f, 22),
                4f);
            var successRuntime = Runtime(
                successStore, world, profiles, successBody, outcome);
            string successOperation = successStore.SingleOperation;
            Require(successRuntime.TryResume(
                    successOperation,
                    out string successFailure),
                successFailure);
            Require(successStore.PendingAnatomyBurdenProjections.Count == 0
                    && successBody.ApplyCount == 1
                    && Math.Abs(successBody.Infection("node:core") - 4f) < 0.001f
                    && outcome.HasCommitted(successOperation),
                "Disease burden success was not exact and durable.");

            var retryBody = new BodyFixture(
                actor.BuildingCharacterId,
                "node:core",
                "node:lung");
            StoreFixture retryStore = Store(
                catalog,
                Change(actor, DiseaseCatalog.BreathingId,
                    DiseaseTargetSystem.Breathing, 30f, 23),
                5f);
            var rejecting = new KnownRejectingFactPublisher(
                outcome.Dispatcher);
            var retryRuntime = Runtime(
                retryStore,
                world,
                profiles,
                retryBody,
                rejecting,
                outcome.Dispatcher);
            string retryOperation = retryStore.SingleOperation;
            Require(!retryRuntime.TryResume(
                    retryOperation,
                    out string rejected)
                    && rejected.StartsWith(
                        "disease-anatomy-burden-observation-rejected:",
                        StringComparison.Ordinal)
                    && rejecting.PublishCount == 1
                    && retryBody.RestoreCount == 1
                    && retryBody.Infection("node:lung") == 0f
                    && retryStore.PendingAnatomyBurdenProjections.Single().phase
                        == (int)DiseaseAnatomyBurdenProjectionPhase.Pending,
                "Outcome rejection did not roll back disease anatomy burden.");

            string saved = JsonUtility.ToJson(retryStore.State.Capture());
            PopulationHealthWorldSaveData saveData =
                JsonUtility.FromJson<PopulationHealthWorldSaveData>(saved);
            var restoredStore = new StoreFixture(
                PopulationHealthAggregateState.Restore(saveData, catalog));
            var restoredRuntime = Runtime(
                restoredStore, world, profiles, retryBody, outcome);
            Require(restoredRuntime.TryResume(
                    retryOperation,
                    out string restoredFailure),
                restoredFailure);
            Require(restoredStore.PendingAnatomyBurdenProjections.Count == 0
                    && retryBody.ApplyCount == 2
                    && Math.Abs(retryBody.Infection("node:lung") - 5f) < 0.001f
                    && outcome.HasCommitted(retryOperation),
                "Fresh-runtime save retry did not complete exactly once.");

            var multiBody = new BodyFixture(
                actor.BuildingCharacterId,
                "node:core",
                "node:lung");
            PopulationHealthAggregateState multiState = Empty(catalog);
            multiState.EnqueueAnatomyBurdenProjection(
                Change(actor, DiseaseCatalog.CoreId,
                    DiseaseTargetSystem.Core, 10f, 24),
                2f);
            multiState.EnqueueAnatomyBurdenProjection(
                Change(actor, DiseaseCatalog.BreathingId,
                    DiseaseTargetSystem.Breathing, 15f, 24),
                3f);
            var multiStore = new StoreFixture(multiState);
            string[] multiOperations = multiStore.PendingAnatomyBurdenProjections
                .Select(value => value.operationId)
                .ToArray();
            Runtime(
                    multiStore, world, profiles, multiBody, outcome)
                .ResumePendingRequired();
            Require(multiStore.PendingAnatomyBurdenProjections.Count == 0
                    && multiBody.ApplyCount == 2
                    && Math.Abs(multiBody.Infection("node:core") - 2f) < 0.001f
                    && Math.Abs(multiBody.Infection("node:lung") - 3f) < 0.001f
                    && multiOperations.All(outcome.HasCommitted),
                "Multiple disease projections were conflated or lost.");

            foreach (int unsupportedVersion in new[] { 3, 4, 5, 6 })
            {
                bool legacyRejected = false;
                try
                {
                    PopulationHealthAggregateState.Restore(new PopulationHealthWorldSaveData
                    {
                        version = unsupportedVersion,
                        currentAbsoluteDay = 7
                    }, catalog);
                }
                catch (InvalidOperationException)
                {
                    legacyRejected = true;
                }
                Require(legacyRejected, "Legacy population-health identity was silently migrated.");
            }

            if (logSuccess)
            {
                Debug.Log(
                    "C:A02 disease anatomy burden PASS "
                    + "burden0=1 no-node=1 success=1 rejection-rollback=1 "
                    + "multi-disease=1 save-retry=1 legacy-schema-rejected=1");
            }
            return true;
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(host);
        }
    }

    public static void RunBatch()
    {
        Require(RunAll(logSuccess: true), "C:A02 scenario returned false.");
    }

    public static void VerifyObserverFocused()
    {
        Require(RunAll(logSuccess: false),
            "Disease anatomy burden core observer scenarios failed.");
        _ = VerifyProductionProgressionAndBody();
        VerifyCommittedAmbiguityRecovery();
    }

    private static void VerifyCommittedAmbiguityRecovery()
    {
        GameObject host = new("Disease Burden Ambiguous Recovery QA");
        try
        {
            CharacterActor actor = host.AddComponent<CharacterActor>();
            CharacterAiEditorTestDependencies.Inject(host);
            actor.EnsureRuntimeState();
            actor.Identity.SetPersistentId(
                "character:qa:disease-anatomy:ambiguous");
            CharacterGrowthState growth = actor.Progression.GrowthState;
            growth.initialized = true;
            growth.displayName = "동결 환자";
            string frozenName = actor.Identity.DisplayName;
            Require(string.Equals(
                    frozenName,
                    "동결 환자",
                    StringComparison.Ordinal),
                "Ambiguous disease burden fixture did not initialize the actor display-name authority.");
            var catalog = new DiseaseCatalog();
            var profiles = new ProfileCatalog();
            var world = new World(actor);
            var body = new BodyFixture(
                actor.BuildingCharacterId,
                "node:core",
                "node:lung");
            StoreFixture store = Store(
                catalog,
                Change(actor, DiseaseCatalog.CoreId,
                    DiseaseTargetSystem.Core, 18f, 31),
                6f);
            string operationId = store.SingleOperation;
            using var outcome = new OutcomeFixture();
            var ambiguous = new AmbiguousAfterCommitFactPublisher(
                outcome.Dispatcher);
            DiseaseAnatomyBurdenProjectionRuntime runtime = Runtime(
                store,
                world,
                profiles,
                body,
                ambiguous,
                ambiguous);

            Require(!runtime.TryResume(
                    operationId,
                    out string ambiguousFailure)
                    && ambiguousFailure.StartsWith(
                        "disease-anatomy-burden-observation-pending:",
                        StringComparison.Ordinal)
                    && ambiguous.PublishCount == 1
                    && body.ApplyCount == 1
                    && Math.Abs(body.Infection("node:core") - 6f) < 0.001f
                    && outcome.HasCommitted(operationId)
                    && outcome.ExactCount == 1,
                "Committed ambiguous disease burden was not retained exactly once.");
            Require(!runtime.TryResume(
                    operationId,
                    out string blockedFailure)
                    && blockedFailure.Contains(
                        AmbiguousAfterCommitFactPublisher.RecoveryFailure,
                        StringComparison.Ordinal)
                    && ambiguous.RecoveryCount == 1
                    && body.ApplyCount == 1
                    && Math.Abs(body.Infection("node:core") - 6f) < 0.001f
                    && outcome.ExactCount == 1,
                "Blocked disease burden recovery reapplied the body mutation or ledger result.");

            string saved = JsonUtility.ToJson(store.State.Capture());
            PopulationHealthWorldSaveData saveData =
                JsonUtility.FromJson<PopulationHealthWorldSaveData>(saved);
            DiseaseAnatomyBurdenProjectionSaveData savedProjection =
                saveData.pendingAnatomyBurdenProjections.Single(value =>
                    value.operationId == operationId);
            Require(savedProjection.phase
                        == (int)DiseaseAnatomyBurdenProjectionPhase
                            .AnatomyApplied
                    && savedProjection.characterDisplayName == frozenName
                    && !string.IsNullOrWhiteSpace(
                        savedProjection.publicationKey),
                "Applied ambiguous disease burden did not save its frozen fact and publication identity.");

            growth.displayName = "개명 환자";
            Require(string.Equals(
                    actor.Identity.DisplayName,
                    "개명 환자",
                    StringComparison.Ordinal),
                "Ambiguous disease burden rename fixture did not expose the renamed live authority.");
            var restoredStore = new StoreFixture(
                PopulationHealthAggregateState.Restore(saveData, catalog));
            DiseaseAnatomyBurdenProjectionRuntime restoredRuntime = Runtime(
                restoredStore,
                world,
                profiles,
                body,
                outcome);
            restoredRuntime.OnRestoreCompleted();
            Require(restoredStore.PendingAnatomyBurdenProjections.Count == 0
                    && body.ApplyCount == 1
                    && Math.Abs(body.Infection("node:core") - 6f) < 0.001f
                    && outcome.ExactCount == 1,
                "Post-restore disease burden hook did not replay its saved observation exactly once.");
            GameplayOutcomeSnapshot exact = outcome.Ledger
                .CaptureGameplayOutcomes()
                .exactOutcomes
                .Single(value => value.operationId == operationId);
            Require(exact.participants.Single().displayText == frozenName,
                "Disease burden replay replaced the frozen name with the renamed actor.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(host);
        }
    }

    private static JObject VerifyProductionProgressionAndBody()
    {
        GameObject actorObject = null;
        CharacterActor actor = null;
        var worldRegistry = CharacterAiEditorTestDependencies.WorldRegistry;
        bool registered = false;
        try
        {
            actorObject = new GameObject("C:A02 Production Disease Burden");
            actor = actorObject.AddComponent<CharacterActor>();
            CharacterAiEditorTestDependencies.Inject(actorObject);
            actor.EnsureRuntimeState();
            actor.Identity.SetPersistentId(
                "character:qa:disease-anatomy:production");
            actor.SetLifecycleState(CharacterLifecycleState.Active);
            worldRegistry.RegisterCharacter(actor);
            worldRegistry.RegisterCharacterLifetime(actor);
            registered = true;

            var catalog = new DiseaseCatalog();
            var profiles = new ProfileCatalog();
            var clock = new UnityGameClock();
            var events = new GameEventBus();
            var root = new DungeonRuntimeAggregateRootStore();
            var body = new CharacterBodyHealthRuntime(
                worldRegistry,
                clock,
                events,
                new DynamicFrameWorkBudget(clock, new UnityUiClock()),
                profiles,
                root,
                CharacterAiEditorTestDependencies.GameCalendar);
            body.ConfigureVitals(actor, 100f, resetCurrentHealth: true);

            var health = new PopulationHealthRuntime(
                root,
                catalog,
                new NeutralDiseaseModifiers(),
                CharacterAiEditorTestDependencies.GameCalendar,
                new RandomStreamProvider(73021));
            health.PublishRestore(health.PrepareRestore(
                new PopulationHealthWorldSaveData
                {
                    currentAbsoluteDay = 20
                }));
            health.ApplyEnvironmentalCondition(
                actor.BuildingCharacterId,
                DiseaseCatalog.CoreId);
            health.ApplyEnvironmentalCondition(
                actor.BuildingCharacterId,
                DiseaseCatalog.BreathingId);

            AnatomyHealthSnapshot before = body.GetAnatomySnapshot(actor);
            IReadOnlyList<PopulationHealthChange> changes = health.AdvanceToDay(21)
                .Where(value =>
                    value.Kind == PopulationHealthChangeKind.DailyBodyBurden)
                .OrderBy(value => value.DiseaseId, StringComparer.Ordinal)
                .ToArray();
            DiseaseAnatomyBurdenProjectionSaveData[] projections =
                health.PendingAnatomyBurdenProjections
                    .OrderBy(value => value.diseaseId, StringComparer.Ordinal)
                    .ToArray();
            Require(changes.Count == 2 && projections.Length == 2,
                "Production disease progression did not return two exact burdens.");
            foreach (PopulationHealthChange change in changes)
            {
                DiseaseAnatomyBurdenProjectionSaveData projection =
                    projections.Single(value => string.Equals(
                        value.diseaseId,
                        change.DiseaseId,
                        StringComparison.Ordinal));
                float expected = change.Severity / 30f;
                Require(change.CharacterId.Equals(actor.BuildingCharacterId)
                        && change.AbsoluteDay == 21
                        && projection.characterId == actor.BuildingCharacterId.Value
                        && projection.absoluteDay == change.AbsoluteDay
                        && Math.Abs(projection.severity - change.Severity) < 0.001f
                        && Math.Abs(
                            projection.requestedInfectionBurden - expected) < 0.001f,
                    "Returned disease burden did not match its durable projection.");
            }

            using var outcome = new OutcomeFixture();
            var projectionRuntime = new DiseaseAnatomyBurdenProjectionRuntime(
                health,
                new World(actor),
                profiles,
                body,
                body,
                outcome.Dispatcher,
                outcome.Dispatcher);
            projectionRuntime.ResumePendingRequired();
            AnatomyHealthSnapshot after = body.GetAnatomySnapshot(actor);

            float coreBefore = Infection(before, "node:core");
            float lungBefore = Infection(before, "node:lung");
            float coreAfter = Infection(after, "node:core");
            float lungAfter = Infection(after, "node:lung");
            float expectedCore = projections.Single(value =>
                value.diseaseId == DiseaseCatalog.CoreId)
                .requestedInfectionBurden;
            float expectedLung = projections.Single(value =>
                value.diseaseId == DiseaseCatalog.BreathingId)
                .requestedInfectionBurden;
            GameplayOutcomeSnapshot[] exactOutcomes = outcome.Ledger
                .CaptureGameplayOutcomes()
                .exactOutcomes
                .Where(value => string.Equals(
                    value.producerId,
                    DiseaseAnatomyBurdenOutcomeIds.ProducerId,
                    StringComparison.Ordinal))
                .OrderBy(value => value.sequence)
                .ToArray();
            Require(health.PendingAnatomyBurdenProjections.Count == 0
                    && Math.Abs(coreAfter - coreBefore - expectedCore) < 0.001f
                    && Math.Abs(lungAfter - lungBefore - expectedLung) < 0.001f
                    && projections.All(value =>
                        outcome.HasCommitted(value.operationId))
                    && exactOutcomes.Length == 2,
                "Production body burden or exact outcomes were not applied once.");

            var changesJson = new JArray();
            foreach (PopulationHealthChange change in changes)
            {
                changesJson.Add(new JObject
                {
                    ["characterId"] = change.CharacterId.Value,
                    ["diseaseId"] = change.DiseaseId,
                    ["kind"] = change.Kind.ToString(),
                    ["severity"] = change.Severity,
                    ["targetSystem"] = change.TargetSystem.ToString(),
                    ["absoluteDay"] = change.AbsoluteDay
                });
            }
            var projectionsJson = new JArray();
            foreach (DiseaseAnatomyBurdenProjectionSaveData projection in projections)
                projectionsJson.Add(JObject.FromObject(projection));
            var outcomesJson = new JArray();
            foreach (GameplayOutcomeSnapshot exactOutcome in exactOutcomes)
                outcomesJson.Add(JObject.FromObject(exactOutcome));

            var evidence = new JObject
            {
                ["returnedChanges"] = changesJson,
                ["durableProjectionsBeforeApply"] = projectionsJson,
                ["coreNode"] = new JObject
                {
                    ["nodeId"] = "node:core",
                    ["infectionBefore"] = coreBefore,
                    ["infectionAfter"] = coreAfter,
                    ["requestedBurden"] = expectedCore
                },
                ["breathingNode"] = new JObject
                {
                    ["nodeId"] = "node:lung",
                    ["infectionBefore"] = lungBefore,
                    ["infectionAfter"] = lungAfter,
                    ["requestedBurden"] = expectedLung
                },
                ["pendingAfterApply"] =
                    health.PendingAnatomyBurdenProjections.Count,
                ["exactOutcomes"] = outcomesJson
            };

            // Explicit QA preparation, not disease progression or a training event.
            Require(body.TryAddNodeBurden(actor, "node:core", 0f, 0f,
                    99.9f - coreAfter, out DomainFailure preparationFailure),
                "Partial-burden preparation failed: " + preparationFailure);
            float partialBefore = Infection(body.GetAnatomySnapshot(actor), "node:core");
            PopulationHealthChange partialChange = health.AdvanceToDay(22)
                .Single(value => value.Kind == PopulationHealthChangeKind.DailyBodyBurden
                    && value.DiseaseId == DiseaseCatalog.CoreId);
            DiseaseAnatomyBurdenProjectionSaveData partialProjection =
                health.PendingAnatomyBurdenProjections.Single(value =>
                    value.diseaseId == DiseaseCatalog.CoreId);
            string partialOperation = partialProjection.operationId;
            float partialRequested = partialProjection.requestedInfectionBurden;
            Require(partialChange.AbsoluteDay == 22
                    && partialChange.CharacterId.Equals(actor.BuildingCharacterId)
                    && Math.Abs(partialRequested - partialChange.Severity / 30f) < 0.001f
                    && partialBefore < 100f && partialBefore + partialRequested > 100f,
                "Partial-burden case did not actually cross the infection limit.");
            var partialRejecting = new KnownRejectingFactPublisher(
                outcome.Dispatcher);
            var partialRuntime = new DiseaseAnatomyBurdenProjectionRuntime(
                health,
                new World(actor),
                profiles,
                body,
                body,
                partialRejecting,
                outcome.Dispatcher);
            Require(!partialRuntime.TryResume(partialOperation, out string partialFailure)
                    && partialFailure.StartsWith(
                        "disease-anatomy-burden-observation-rejected:",
                        StringComparison.Ordinal)
                    && partialRejecting.PublishCount == 1
                    && Math.Abs(Infection(body.GetAnatomySnapshot(actor), "node:core")
                        - partialBefore) < 0.001f
                    && !outcome.HasCommitted(partialOperation)
                    && health.PendingAnatomyBurdenProjections.Single(value =>
                        value.operationId == partialOperation).phase
                        == (int)DiseaseAnatomyBurdenProjectionPhase.Pending,
                "Rejected partial burden was not rolled back exactly.");

            string partialSaved = JsonUtility.ToJson(health.Capture());
            var restoredHealth = new PopulationHealthRuntime(
                new DungeonRuntimeAggregateRootStore(), catalog,
                new NeutralDiseaseModifiers(), CharacterAiEditorTestDependencies.GameCalendar,
                new RandomStreamProvider(73022));
            restoredHealth.PublishRestore(restoredHealth.PrepareRestore(
                JsonUtility.FromJson<PopulationHealthWorldSaveData>(partialSaved)));
            var restoredProjectionRuntime = new DiseaseAnatomyBurdenProjectionRuntime(
                restoredHealth,
                new World(actor),
                profiles,
                body,
                body,
                outcome.Dispatcher,
                outcome.Dispatcher);
            restoredProjectionRuntime.ResumePendingRequired();
            float partialAfter = Infection(body.GetAnatomySnapshot(actor), "node:core");
            GameplayOutcomeSnapshot partialOutcome = outcome.Ledger.CaptureGameplayOutcomes()
                .exactOutcomes.Single(value => value.operationId == partialOperation);
            Require(Math.Abs(partialAfter - 100f) < 0.001f
                    && partialAfter > partialBefore
                    && partialAfter - partialBefore < partialRequested
                    && restoredHealth.PendingAnatomyBurdenProjections.Count == 0
                    && outcome.HasCommitted(partialOperation)
                    && Math.Abs(partialOutcome.metrics.Single(value =>
                        value.metricId == "disease.infection-burden-requested").value
                        - partialRequested) < 0.001f
                    && Math.Abs(partialOutcome.metrics.Single(value =>
                        value.metricId == "disease.node-infection-before").value
                        - partialBefore) < 0.001f
                    && Math.Abs(partialOutcome.metrics.Single(value =>
                        value.metricId == "disease.node-infection-after").value
                        - partialAfter) < 0.001f,
                "Partial burden ledger overstated or lost the actual body effect.");
            string ledgerBeforeReplay = JsonUtility.ToJson(outcome.Ledger.CaptureGameplayOutcomes());
            string bodyBeforeReplay = JsonUtility.ToJson(body.CaptureCombatMutation(actor).State);
            string healthBeforeReplay = JsonUtility.ToJson(restoredHealth.Capture());
            bool sameDayRejected = false;
            try
            {
                restoredHealth.AdvanceToDay(22);
            }
            catch (InvalidOperationException exception) when (exception.Message
                == "Population health requires strictly increasing daily updates.")
            {
                sameDayRejected = true;
            }
            Require(sameDayRejected,
                "Same-day disease progression bypassed its monotonic-day guard.");
            restoredProjectionRuntime.ResumePendingRequired();
            Require(ledgerBeforeReplay == JsonUtility.ToJson(outcome.Ledger.CaptureGameplayOutcomes())
                    && bodyBeforeReplay == JsonUtility.ToJson(body.CaptureCombatMutation(actor).State)
                    && healthBeforeReplay == JsonUtility.ToJson(restoredHealth.Capture()),
                "Same-day partial-burden replay changed body, health owner or ledger.");
            evidence["partialEffect"] = new JObject
            {
                ["preparationIsTestOnly"] = true,
                ["absoluteDay"] = partialChange.AbsoluteDay,
                ["requestedBurden"] = partialRequested,
                ["infectionBefore"] = partialBefore,
                ["infectionAfter"] = partialAfter,
                ["appliedBurden"] = partialAfter - partialBefore,
                ["rejectedCommitRolledBack"] = true,
                ["freshHealthOwnerRestored"] = true,
                ["sameDayProgressionExplicitlyRejected"] = true,
                ["sameDayReplayUnchanged"] = true,
                ["exactOutcome"] = JObject.FromObject(partialOutcome),
                ["scope"] = "Fresh population-health owner; real body and canonical ledger retained. Not a whole-world or cold restore."
            };
            return evidence;
        }
        finally
        {
            if (registered && actor != null)
            {
                worldRegistry.UnregisterCharacter(actor);
                worldRegistry.UnregisterCharacterLifetime(actor);
            }
            if (actorObject != null)
                UnityEngine.Object.DestroyImmediate(actorObject);
        }
    }

    private static float Infection(
        AnatomyHealthSnapshot snapshot,
        string nodeId) => snapshot.Nodes.Single(value =>
            string.Equals(value.nodeId, nodeId, StringComparison.Ordinal)).infection;

    private static DiseaseAnatomyBurdenProjectionRuntime Runtime(
        IDiseaseAnatomyBurdenProjectionStore store,
        ICharacterWorldQuery world,
        IAnatomyProfileCatalog profiles,
        BodyFixture body,
        OutcomeFixture outcomes) => Runtime(
        store,
        world,
        profiles,
        body,
        outcomes.Dispatcher,
        outcomes.Dispatcher);

    private static DiseaseAnatomyBurdenProjectionRuntime Runtime(
        IDiseaseAnatomyBurdenProjectionStore store,
        ICharacterWorldQuery world,
        IAnatomyProfileCatalog profiles,
        BodyFixture body,
        IDomainFactPublisher facts,
        IDomainFactRecoveryPublisher recovery) => new(
        store,
        world,
        profiles,
        body,
        body,
        facts,
        recovery);

    private static StoreFixture Store(
        DiseaseCatalog catalog,
        PopulationHealthChange change,
        float burden)
    {
        PopulationHealthAggregateState state = Empty(catalog);
        state.EnqueueAnatomyBurdenProjection(change, burden);
        return new StoreFixture(state);
    }

    private static PopulationHealthAggregateState Empty(DiseaseCatalog catalog) =>
        PopulationHealthAggregateState.Restore(
            new PopulationHealthWorldSaveData { currentAbsoluteDay = 1 },
            catalog);

    private static PopulationHealthChange Change(
        CharacterActor actor,
        string diseaseId,
        DiseaseTargetSystem target,
        float severity,
        int day) => new(
        actor.BuildingCharacterId,
        diseaseId,
        PopulationHealthChangeKind.DailyBodyBurden,
        severity,
        target,
        day);

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    private static void WriteCreateOnlyJson(string path, JObject value)
    {
        using FileStream stream = new(
            path,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.Read);
        using StreamWriter writer = new(stream);
        writer.Write(value.ToString());
    }

    private sealed class NeutralDiseaseModifiers :
        IPopulationDiseaseModifierQuery
    {
        public PopulationDiseaseStatModifiers Resolve(
            CharacterId characterId,
            DiseaseDefinition disease) => PopulationDiseaseStatModifiers.Neutral;
    }

    private sealed class StoreFixture : IDiseaseAnatomyBurdenProjectionStore
    {
        internal StoreFixture(PopulationHealthAggregateState state) =>
            State = state ?? throw new ArgumentNullException(nameof(state));
        internal PopulationHealthAggregateState State { get; }
        internal string SingleOperation =>
            PendingAnatomyBurdenProjections.Single().operationId;
        public IReadOnlyList<DiseaseAnatomyBurdenProjectionSaveData>
            PendingAnatomyBurdenProjections =>
            State.PendingAnatomyBurdenProjections;
        public void RecordAnatomyBurdenApplied(string operationId,
            string nodeId, float infectionBefore, float infectionAfter,
            string characterDisplayName) =>
            State.RecordAnatomyBurdenApplied(
                operationId, nodeId, infectionBefore, infectionAfter,
                characterDisplayName);
        public void RecordAnatomyBurdenPublication(
            string operationId,
            string publicationKey) =>
            State.RecordAnatomyBurdenPublication(
                operationId,
                publicationKey);
        public void ResetAnatomyBurdenProjection(string operationId) =>
            State.ResetAnatomyBurdenProjection(operationId);
        public void RecordAnatomyBurdenFailure(string operationId,
            string failureCode) =>
            State.RecordAnatomyBurdenFailure(operationId, failureCode);
        public void CompleteAnatomyBurdenProjection(string operationId) =>
            State.CompleteAnatomyBurdenProjection(operationId);
    }

    private sealed class DiseaseCatalog : IDiseaseDefinitionCatalog
    {
        internal const string CoreId = "disease:qa-core";
        internal const string BreathingId = "disease:qa-breathing";
        private readonly Dictionary<string, DiseaseDefinition> values = new(
            StringComparer.Ordinal)
        {
            [CoreId] = Definition(CoreId, DiseaseTargetSystem.Core),
            [BreathingId] = Definition(
                BreathingId, DiseaseTargetSystem.Breathing)
        };
        public IReadOnlyList<DiseaseDefinition> Definitions =>
            values.Values.ToArray();
        public DiseaseDefinition Require(string diseaseId) =>
            values.TryGetValue(diseaseId ?? string.Empty, out DiseaseDefinition value)
                ? value
                : throw new KeyNotFoundException(diseaseId);
        private static DiseaseDefinition Definition(
            string id,
            DiseaseTargetSystem target) => new(
            id,
            id,
            DiseaseTransmissionRoute.Environment,
            0,
            0,
            0f,
            10f,
            target,
            false,
            chronic: true,
            symptomProfileId: "symptom:qa",
            fieldResponseIds: new[] { "response:qa" });
    }

    private sealed class ProfileCatalog : IAnatomyProfileCatalog
    {
        private readonly AnatomyProfileDefinition profile = new(
            "profile:qa-disease",
            "QA",
            "qa",
            new[] { "human" },
            new[]
            {
                new AnatomyNodeDefinition(
                    "node:core", "핵", string.Empty, AnatomyNodeKind.Core,
                    AnatomyFunction.Core, 100f, 1f, true, false),
                new AnatomyNodeDefinition(
                    "node:lung", "폐", string.Empty, AnatomyNodeKind.Organ,
                    AnatomyFunction.Breathing, 100f, 1f, true, false)
            });
        public IReadOnlyList<AnatomyProfileDefinition> Profiles =>
            new[] { profile };
        public AnatomyProfileDefinition GetDefaultHumanoid() => profile;
        public AnatomyProfileDefinition GetForSpecies(string speciesId) => profile;
        public bool TryGet(string profileId, out AnatomyProfileDefinition value)
        {
            value = profile;
            return string.Equals(profileId, profile.ProfileId,
                StringComparison.Ordinal);
        }
        public IReadOnlyList<string> Validate() => Array.Empty<string>();
    }

    private sealed class World : ICharacterWorldQuery
    {
        internal World(CharacterActor actor) => Characters = new[] { actor };
        public int CharacterVersion => 1;
        public IReadOnlyList<CharacterActor> Characters { get; }
    }

    private sealed class BodyFixture :
        IDiseaseAnatomyBurdenMutationTransaction,
        IAnatomyHealthRuntime
    {
        private readonly CharacterId id;
        internal BodyFixture(CharacterId id, params string[] nodeIds)
        {
            this.id = id;
            Nodes = nodeIds.Select(value => new AnatomyNodeHealthState
            {
                nodeId = value,
                maxHealth = 100f,
                currentHealth = 100f
            }).ToList();
        }
        private List<AnatomyNodeHealthState> Nodes { get; set; }
        internal int ApplyCount { get; private set; }
        internal int RestoreCount { get; private set; }
        internal float Infection(string nodeId) =>
            Nodes.Single(value => value.nodeId == nodeId).infection;

        public CharacterPreparedDiseaseAnatomyBurdenReceipt
            ApplyPreparedDiseaseAnatomyBurden(
                CharacterActor actor,
                string nodeId,
                float infectionBurden,
                string reasonCode)
        {
            ApplyCount++;
            CharacterBodyHealthMutationSnapshot before = new(
                id,
                State());
            AnatomyNodeHealthState node = Nodes.Single(value =>
                value.nodeId == nodeId);
            float previous = node.infection;
            node.infection = Mathf.Clamp(previous + infectionBurden, 0f, 100f);
            return new CharacterPreparedDiseaseAnatomyBurdenReceipt(
                before,
                nodeId,
                infectionBurden,
                previous,
                node.infection,
                reasonCode);
        }

        public void RestorePreparedDiseaseAnatomyBurden(
            CharacterActor actor,
            in CharacterBodyHealthMutationSnapshot snapshot,
            string reason)
        {
            RestoreCount++;
            Nodes = snapshot.State.anatomyNodes.Select(Clone).ToList();
        }

        public AnatomyHealthSnapshot GetAnatomySnapshot(CharacterActor actor) =>
            Snapshot();
        public AnatomyHealthSnapshot GetAnatomySnapshot(string characterId) =>
            Snapshot();
        private AnatomyHealthSnapshot Snapshot() => new(
            "profile:qa-disease",
            Nodes.Select(Clone).ToArray(),
            1f, 1f, 1f, 1f, 1f, 1f, 1f);
        private CharacterBodyHealthState State() => new()
        {
            characterId = id.Value,
            maxHealth = 100f,
            currentHealth = 100f,
            anatomyNodes = Nodes.Select(Clone).ToList(),
            parts = new List<CharacterBodyPartHealthState>()
        };
        private static AnatomyNodeHealthState Clone(AnatomyNodeHealthState value) =>
            new()
            {
                nodeId = value.nodeId,
                maxHealth = value.maxHealth,
                currentHealth = value.currentHealth,
                infection = value.infection,
                missing = value.missing
            };

        public bool TryDamageNode(CharacterActor actor, string nodeId,
            float damage, float bleeding, string reason) =>
            throw new NotSupportedException();
        public bool TryDamageNodeWithCause(CharacterActor actor, string nodeId,
            float damage, float bleeding, CharacterDeathCauseCode deathCause,
            string reasonCode) => throw new NotSupportedException();
        public bool TryHealNode(CharacterActor actor, string nodeId, float health,
            float infectionReduction) => throw new NotSupportedException();
        public bool TryStopBleeding(CharacterActor actor, string nodeId,
            out DomainFailure failure) => throw new NotSupportedException();
        public PartRecoveryPolicy GetRecoveryPolicy(CharacterActor actor,
            string nodeId) => throw new NotSupportedException();
        public bool CanRecoverNaturally(CharacterActor actor, string nodeId) =>
            throw new NotSupportedException();
        public bool TryMaintainNode(CharacterActor actor, string nodeId,
            float durability, float contaminationReduction,
            out DomainFailure failure) => throw new NotSupportedException();
        public bool TryRemoveNode(CharacterActor actor, string nodeId,
            out AnatomyNodeHealthState removedNode, out DomainFailure failure) =>
            throw new NotSupportedException();
        public bool TryInstallPart(CharacterActor actor, string nodeId,
            string partInstanceId, SurgicalPartKind partKind, float efficiency,
            float restoredCurrentHealth, bool preserveRestoredHealth,
            out DomainFailure failure) => throw new NotSupportedException();
        public bool TryReplaceNodePart(CharacterActor actor, string nodeId,
            string expectedPartInstanceId, float expectedCurrentHealth,
            float expectedMaxHealth, string partInstanceId,
            SurgicalPartKind partKind, float efficiency,
            float restoredCurrentHealth, bool preserveRestoredHealth,
            out AnatomyNodeHealthState replacedNode,
            out DomainFailure failure) => throw new NotSupportedException();
        public bool TryAddNodeBurden(CharacterActor actor, string nodeId,
            float rejection, float mutation, float infection,
            out DomainFailure failure) => throw new NotSupportedException();
        public bool TryReduceNodeBurden(CharacterActor actor, string nodeId,
            float rejection, float mutation, float infection,
            out DomainFailure failure) => throw new NotSupportedException();
    }

    private sealed class KnownRejectingFactPublisher :
        IDomainFactPublisher
    {
        private readonly IDomainFactPublisher inner;
        private bool rejectNext = true;

        internal KnownRejectingFactPublisher(
            IDomainFactPublisher inner) =>
            this.inner = inner
                ?? throw new ArgumentNullException(nameof(inner));

        internal int PublishCount { get; private set; }

        public bool TryReserve<TFact>(in TFact proposed,
            out DomainFactLease lease, out string failure) =>
            inner.TryReserve(proposed, out lease, out failure);

        public DomainFactDelivery Publish<TFact>(
            in DomainFactLease lease,
            in TFact actual)
        {
            PublishCount++;
            if (rejectNext)
            {
                rejectNext = false;
                return DomainFactDelivery.Reject(
                    "qa-disease-burden-observer-rejected");
            }
            return inner.Publish(lease, actual);
        }

        public bool PublishBatch(IReadOnlyList<DomainFactEmission> facts,
            DomainFactDelivery[] results, out string failure) =>
            inner.PublishBatch(facts, results, out failure);

        public DomainFactDelivery Replay<TFact>(in TFact fact) =>
            inner.Replay(fact);

        public void Cancel(in DomainFactLease lease) => inner.Cancel(lease);
    }

    private sealed class AmbiguousAfterCommitFactPublisher :
        IDomainFactPublisher,
        IDomainFactRecoveryPublisher
    {
        internal const string RecoveryFailure =
            "qa-disease-burden-recovery-unavailable";
        private const string DeliveryFailure =
            "qa-disease-burden-delivery-ambiguous";
        private readonly IDomainFactPublisher inner;

        internal AmbiguousAfterCommitFactPublisher(
            IDomainFactPublisher inner) =>
            this.inner = inner
                ?? throw new ArgumentNullException(nameof(inner));

        internal int PublishCount { get; private set; }
        internal int RecoveryCount { get; private set; }

        public bool TryReserve<TFact>(in TFact proposed,
            out DomainFactLease lease, out string failure) =>
            inner.TryReserve(proposed, out lease, out failure);

        public DomainFactDelivery Publish<TFact>(
            in DomainFactLease lease,
            in TFact actual)
        {
            DomainFactDelivery delivered = inner.Publish(lease, actual);
            if (!delivered.Completed)
                throw new InvalidOperationException(
                    "Ambiguity fixture requires a real committed delivery.");
            PublishCount++;
            return InDoubt(delivered);
        }

        public bool PublishBatch(IReadOnlyList<DomainFactEmission> facts,
            DomainFactDelivery[] results, out string failure)
        {
            bool accepted = inner.PublishBatch(facts, results, out failure);
            if (!accepted || results.Any(value => !value.Completed))
                throw new InvalidOperationException(
                    "Ambiguity fixture requires a real committed cohort.");
            PublishCount += results.Length;
            for (int index = 0; index < results.Length; index++)
                results[index] = InDoubt(results[index]);
            failure = DeliveryFailure;
            return true;
        }

        public DomainFactDelivery Replay<TFact>(in TFact fact)
        {
            _ = fact;
            RecoveryCount++;
            return DomainFactDelivery.Reject(RecoveryFailure);
        }

        public bool ReplayBatch(IReadOnlyList<DomainFactReplay> facts,
            DomainFactDelivery[] results, out string failure)
        {
            if (facts == null || results == null || facts.Count == 0
                || facts.Count != results.Length)
            {
                throw new ArgumentException(
                    "Invalid blocked disease burden replay shape.");
            }
            RecoveryCount++;
            for (int index = 0; index < results.Length; index++)
            {
                results[index] = DomainFactDelivery.Reject(
                    RecoveryFailure);
            }
            failure = RecoveryFailure;
            return false;
        }

        public void Cancel(in DomainFactLease lease) => inner.Cancel(lease);

        private static DomainFactDelivery InDoubt(
            in DomainFactDelivery delivered) => new(
            DomainFactDeliveryPhase.InDoubt,
            delivered.OperationKey,
            string.Empty,
            string.Empty,
            DeliveryFailure);
    }

    private sealed class OutcomeFixture : IDisposable
    {
        private readonly IDisposable binding;

        internal OutcomeFixture()
        {
            Registry = new GameplayOutcomeRegistry(
                new IGameplayOutcomeDescriptor[]
                {
                    new DiseaseAnatomyBurdenOutcomeDescriptor()
                },
                new IGameplayOutcomeAdapterRegistration[]
                {
                    new DiseaseAnatomyBurdenOutcomeAdapter()
                });
            Ledger = new GameplayOutcomeLedger(
                Registry,
                new GameplayOutcomeBufferLimits(64, 4, 256),
                new GameplayOutcomeRunId("qa-disease-anatomy"),
                1L);
            Recorder = new GameplayOutcomeRecorder(
                Ledger,
                Registry,
                new GameEventBus());
            CommitGroup = new GameplayDomainFactCommitGroup(
                Recorder,
                Ledger);
            Dispatcher = new DomainFactDispatcher();
            binding = DiseaseAnatomyBurdenFactMapping.CreateBinding(
                    Registry,
                    Ledger,
                    Recorder,
                    CommitGroup)
                .Connect(Dispatcher);
        }
        internal GameplayOutcomeRegistry Registry { get; }
        internal GameplayOutcomeLedger Ledger { get; }
        internal GameplayOutcomeRecorder Recorder { get; }
        internal GameplayDomainFactCommitGroup CommitGroup { get; }
        internal DomainFactDispatcher Dispatcher { get; }
        internal int ExactCount => Ledger.CaptureGameplayOutcomes()
            .exactOutcomes.Count(value => string.Equals(
                value.producerId,
                DiseaseAnatomyBurdenOutcomeIds.ProducerId,
                StringComparison.Ordinal));
        internal bool HasCommitted(string operationId) =>
            Ledger.TryGetResultIdentity(
                new GameplayResultKey(
                    DiseaseAnatomyBurdenOutcomeIds.ProducerId,
                    new GameplayOperationId(operationId),
                    1L,
                    0),
                out GameplayOutcomeReplayIdentity identity)
            && identity.State >= GameplayOutcomeReplayState.Committed;

        public void Dispose()
        {
            binding.Dispose();
            Dispatcher.Dispose();
        }
    }
}
#endif
