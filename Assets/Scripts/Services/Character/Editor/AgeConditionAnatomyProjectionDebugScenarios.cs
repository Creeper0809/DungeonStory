#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DungeonStory.Foundation;
using DungeonStory.Foundation.Facts;
using DungeonStory.Narrative.Korean;
using Newtonsoft.Json.Linq;
using UnityEngine;

public static class AgeConditionAnatomyProjectionDebugScenarios
{
    public static void RunCA01FromBatch()
    {
        if (!Application.isBatchMode
            || !string.Equals(
                Environment.GetEnvironmentVariable(
                    "DUNGEONSTORY_U0_CA01_AGE_ANATOMY_VERIFY"),
                "1",
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "C:A01 verification requires explicit isolated batch mode.");
        }

        string output = Environment.GetEnvironmentVariable(
            "DUNGEONSTORY_AUDIT_REMEDIATION_OUTPUT");
        if (string.IsNullOrWhiteSpace(output))
        {
            throw new InvalidOperationException(
                "C:A01 verification requires its isolated output directory.");
        }
        string directory = Path.GetFullPath(output);
        Directory.CreateDirectory(directory);
        string reportPath = Path.Combine(directory, "report.json");
        if (File.Exists(reportPath))
        {
            throw new InvalidOperationException(
                "C:A01 report already exists: " + reportPath);
        }

        var report = new JObject
        {
            ["rowId"] = "C:A01",
            ["passed"] = false,
            ["zeroOneManyNodesExact"] = false,
            ["childCommitRejectionRollbackExact"] = false,
            ["saveRetryExactlyOnce"] = false,
            ["durableParentPreserved"] = false,
            ["productionBodyRuntimeReached"] = false,
            ["actualNonlethalSurvived"] = false,
            ["actualVitalNodeDeath"] = false,
            ["oldSaveMigrationAdded"] = false,
            ["fullAuditExecuted"] = false,
            ["humanApprovalRecorded"] = false,
            ["trainingExecuted"] = false,
            ["sourceFiles"] = new JArray
            {
                "Assets/Scripts/Services/Character/CharacterLifeApplicationAdapter.cs",
                "Assets/Scripts/Services/Character/AgeConditionAnatomyProjectionRuntime.cs",
                "Assets/Scripts/Services/Combat/CharacterBodyHealthRuntime.cs",
                "Assets/Scripts/Services/Character/Editor/AgeConditionAnatomyProjectionDebugScenarios.cs"
            }
        };

        try
        {
            Require(RunAll(logSuccess: false),
                "C:A01 core scenario returned false.");
            ProductionBodyLifecycleEvidence production =
                VerifyProductionBodyLifecycle();
            report["zeroOneManyNodesExact"] = true;
            report["childCommitRejectionRollbackExact"] = true;
            report["saveRetryExactlyOnce"] = true;
            report["durableParentPreserved"] = true;
            report["productionBodyRuntimeReached"] = true;
            report["actualNonlethalSurvived"] = production.NonlethalSurvived;
            report["actualVitalNodeDeath"] = production.VitalNodeKilled;
            report["nonlethal"] = production.NonlethalJson();
            report["lethal"] = production.LethalJson();
            report["passed"] = true;
            WriteCreateOnlyJson(reportPath, report);
            Debug.Log(
                "[AuditRemediation][C:A01] PASS nodes=0/1/many; "
                + "rejection-rollback=1; save-retry=1; parent-preserved=1; "
                + "production-nonlethal=alive; production-vital=dead");
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
        GameObject host = new("Age Condition Anatomy Projection QA");
        try
        {
            CharacterActor actor = host.AddComponent<CharacterActor>();
            CharacterAiEditorTestDependencies.Inject(host);
            actor.EnsureRuntimeState();
            actor.Identity.SetPersistentId("character:qa:age-condition-anatomy");

            var catalog = new Catalog();
            using var outcome = new OutcomeFixture();
            var body = new BodyFixture(actor.BuildingCharacterId, "node:a", "node:b");
            var anatomy = new AnatomyFixture(body);
            var world = new World(actor);
            CharacterLifeRuntime life = CreateLife(catalog);
            var runtime = CreateRuntime(
                life, catalog, world, anatomy, body, outcome);

            AgeConditionChange oneChange = Change(
                actor,
                AgeConditionSeverity.Mild);
            DomainFactDelivery oneParent = outcome.Parent(oneChange, 10);
            anatomy.VisibleNodeIds = new[] { "node:a" };
            string oneOperation = runtime.Enqueue(
                oneChange,
                10,
                oneParent);
            Require(runtime.TryResume(oneOperation, out string oneFailure), oneFailure);
            Require(body.ApplyCount == 1
                    && body.CompleteCount == 1
                    && !body.LastDestroyNode
                    && body.LastAppliedNodeCount == 1
                    && life.PendingAgeConditionAnatomyProjections.Count == 0,
                "One-node nonlethal projection was not exact.");
            RequireChild(outcome.Ledger, oneChange, 10, oneParent);

            AgeConditionChange manyChange = Change(
                actor,
                AgeConditionSeverity.OrganFunctionLoss);
            DomainFactDelivery manyParent = outcome.Parent(manyChange, 11);
            anatomy.VisibleNodeIds = new[] { "node:a", "node:b" };
            string manyOperation = runtime.Enqueue(
                manyChange,
                11,
                manyParent);
            Require(runtime.TryResume(manyOperation, out string manyFailure),
                manyFailure);
            Require(body.ApplyCount == 2
                    && body.CompleteCount == 2
                    && body.LastDestroyNode
                    && body.LastAppliedNodeCount == 2,
                "Multi-node lethal projection was not exact.");
            RequireChild(outcome.Ledger, manyChange, 11, manyParent);

            CharacterLifeRuntime zeroLife = CreateLife(catalog);
            var zeroBody = new BodyFixture(actor.BuildingCharacterId);
            var zeroAnatomy = new AnatomyFixture(zeroBody)
            {
                VisibleNodeIds = Array.Empty<string>()
            };
            var zeroRuntime = CreateRuntime(
                zeroLife,
                catalog,
                world,
                zeroAnatomy,
                zeroBody,
                outcome);
            AgeConditionChange zeroChange = Change(
                actor,
                AgeConditionSeverity.Moderate);
            DomainFactDelivery zeroParent = outcome.Parent(zeroChange, 12);
            string zeroOperation = zeroRuntime.Enqueue(
                zeroChange,
                12,
                zeroParent);
            Require(!zeroRuntime.TryResume(zeroOperation, out string zeroFailure)
                    && zeroFailure == "age-condition-anatomy-node-unavailable"
                    && zeroLife.PendingAgeConditionAnatomyProjections.Count == 1
                    && zeroBody.ApplyCount == 0,
                "Zero-node rejection was hidden or mutated anatomy.");

            CharacterLifeRuntime retryLife = CreateLife(catalog);
            var retryBody = new BodyFixture(
                actor.BuildingCharacterId,
                "node:a",
                "node:b");
            var retryAnatomy = new AnatomyFixture(retryBody)
            {
                VisibleNodeIds = new[] { "node:a", "node:b" }
            };
            var retryPublisher = new KnownRejectingFactPublisher(
                outcome.Facts);
            var retryRuntime = CreateRuntime(
                retryLife,
                catalog,
                world,
                retryAnatomy,
                retryBody,
                retryPublisher,
                outcome.Facts);
            AgeConditionChange retryChange = Change(
                actor,
                AgeConditionSeverity.Severe);
            DomainFactDelivery retryParent = outcome.Parent(retryChange, 13);
            float healthBefore = retryBody.TotalNodeHealth;
            string retryOperation = retryRuntime.Enqueue(
                retryChange,
                13,
                retryParent);
            Require(!retryRuntime.TryResume(retryOperation, out string rejected)
                    && rejected.StartsWith(
                        "age-condition-observation-rejected:",
                        StringComparison.Ordinal)
                    && retryPublisher.PublishCount == 1
                    && Math.Abs(retryBody.TotalNodeHealth - healthBefore) < 0.001f
                    && retryBody.RestoreCount == 1
                    && retryLife.PendingAgeConditionAnatomyProjections.Single()
                        .phase == (int)AgeConditionAnatomyProjectionPhase.Pending
                    && outcome.HasPublication(retryParent.PublicationId),
                "Child rejection did not roll back anatomy while preserving the parent.");

            string saved = JsonUtility.ToJson(retryLife.Capture());
            CharacterLifeWorldSaveData saveData =
                JsonUtility.FromJson<CharacterLifeWorldSaveData>(saved);
            CharacterLifeRuntime restoredLife = CreateLife(catalog);
            restoredLife.PublishRestore(restoredLife.PrepareRestore(saveData));
            var restoredRuntime = CreateRuntime(
                restoredLife,
                catalog,
                world,
                retryAnatomy,
                retryBody,
                outcome);
            Require(restoredRuntime.TryResume(
                    retryOperation,
                    out string restoredFailure),
                restoredFailure);
            Require(restoredLife.PendingAgeConditionAnatomyProjections.Count == 0
                    && retryBody.ApplyCount == 2
                    && retryBody.CompleteCount == 1,
                "Fresh-runtime save retry did not complete exactly once.");
            RequireChild(outcome.Ledger, retryChange, 13, retryParent);

            foreach (int unsupportedVersion in new[] { 3, 4 })
            {
                bool rejectedVersion = false;
                try
                {
                    CreateLife(catalog).PrepareRestore(
                        new CharacterLifeWorldSaveData
                        {
                            version = unsupportedVersion
                        });
                }
                catch (InvalidOperationException)
                {
                    rejectedVersion = true;
                }
                Require(rejectedVersion,
                    "Legacy character-life schema was silently accepted.");
            }

            if (logSuccess)
            {
                Debug.Log(
                    "C:A01 age-condition anatomy PASS "
                    + "nodes=0/1/many nonlethal=1 lethal=1 "
                    + "child-rejection-rollback=1 parent-preserved=1 "
                    + "save-retry=1 legacy-schema-rejected=1");
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
        Require(RunAll(logSuccess: true), "C:A01 scenario returned false.");
    }

    public static void VerifyObserverFocused()
    {
        Require(RunAll(logSuccess: false),
            "Age-condition anatomy core observer scenarios failed.");
        _ = VerifyProductionBodyLifecycle();
        Require(CharacterLifeCausalProductionEntryDebugScenarios
                .RunCausalOwnerFamily(log: false),
            "Character-life parent observer scenarios failed.");
        CharacterLifeCausalProductionEntryDebugScenarios
            .VerifyAmbiguousObservationRecovery();
        VerifyCommittedAmbiguityRecovery();
    }

    private static void VerifyCommittedAmbiguityRecovery()
    {
        GameObject host = new("Age Anatomy Ambiguous Recovery QA");
        try
        {
            CharacterActor actor = host.AddComponent<CharacterActor>();
            CharacterAiEditorTestDependencies.Inject(host);
            actor.EnsureRuntimeState();
            actor.Identity.SetPersistentId(
                "character:qa:age-anatomy:ambiguous");
            CharacterGrowthState growth = actor.Progression.GrowthState;
            growth.initialized = true;
            growth.displayName = "동결 노화 환자";
            string frozenName = actor.Identity.DisplayName;
            Require(string.Equals(
                    frozenName,
                    "동결 노화 환자",
                    StringComparison.Ordinal),
                "Age anatomy fixture did not initialize the display-name authority.");

            var catalog = new Catalog();
            var body = new BodyFixture(
                actor.BuildingCharacterId,
                "node:a",
                "node:b");
            var anatomy = new AnatomyFixture(body)
            {
                VisibleNodeIds = new[] { "node:a", "node:b" }
            };
            var world = new World(actor);
            CharacterLifeRuntime life = CreateLife(catalog);
            AgeConditionChange change = Change(
                actor,
                AgeConditionSeverity.Severe);
            using var outcome = new OutcomeFixture();
            DomainFactDelivery parent = outcome.Parent(change, 31);
            var ambiguous = new AmbiguousAfterCommitFactPublisher(
                outcome.Facts);
            AgeConditionAnatomyProjectionRuntime runtime = CreateRuntime(
                life,
                catalog,
                world,
                anatomy,
                body,
                ambiguous,
                ambiguous);
            string operationId = runtime.Enqueue(change, 31, parent);
            float healthBefore = body.TotalNodeHealth;

            Require(!runtime.TryResume(
                    operationId,
                    out string ambiguousFailure)
                    && ambiguousFailure.StartsWith(
                        "age-condition-observation-pending:",
                        StringComparison.Ordinal)
                    && ambiguous.PublishCount == 1
                    && body.ApplyCount == 1
                    && body.TotalNodeHealth < healthBefore
                    && outcome.ExactChildCount == 1,
                "Committed ambiguous age anatomy was not retained exactly once.");
            float healthAfter = body.TotalNodeHealth;
            Require(!runtime.TryResume(
                    operationId,
                    out string blockedFailure)
                    && blockedFailure.Contains(
                        AmbiguousAfterCommitFactPublisher.RecoveryFailure,
                        StringComparison.Ordinal)
                    && ambiguous.RecoveryCount == 1
                    && body.ApplyCount == 1
                    && Math.Abs(body.TotalNodeHealth - healthAfter) < 0.001f
                    && outcome.ExactChildCount == 1,
                "Blocked age anatomy recovery repeated damage or its ledger result.");

            string saved = JsonUtility.ToJson(life.Capture());
            CharacterLifeWorldSaveData saveData =
                JsonUtility.FromJson<CharacterLifeWorldSaveData>(saved);
            AgeConditionAnatomyProjectionSaveData savedProjection =
                saveData.pendingAgeConditionAnatomyProjections.Single(value =>
                    value.operationId == operationId);
            Require(savedProjection.phase
                        == (int)AgeConditionAnatomyProjectionPhase
                            .AnatomyApplied
                    && savedProjection.characterDisplayName == frozenName
                    && !string.IsNullOrWhiteSpace(
                        savedProjection.publicationKey),
                "Ambiguous age anatomy did not save its frozen applied observation.");

            growth.displayName = "개명 노화 환자";
            Require(string.Equals(
                    actor.Identity.DisplayName,
                    "개명 노화 환자",
                    StringComparison.Ordinal),
                "Age anatomy fixture did not expose the renamed live authority.");
            CharacterLifeRuntime restoredLife = CreateLife(catalog);
            restoredLife.PublishRestore(
                restoredLife.PrepareRestore(saveData));
            AgeConditionAnatomyProjectionRuntime restoredRuntime =
                CreateRuntime(
                    restoredLife,
                    catalog,
                    world,
                    anatomy,
                    body,
                    outcome);
            Require(restoredRuntime.TryResume(
                    operationId,
                    out string restoredFailure)
                    && string.IsNullOrEmpty(restoredFailure)
                    && restoredLife.PendingAgeConditionAnatomyProjections.Count
                        == 0
                    && body.ApplyCount == 1
                    && body.CompleteCount == 1
                    && Math.Abs(body.TotalNodeHealth - healthAfter) < 0.001f
                    && outcome.ExactChildCount == 1,
                "Fresh age anatomy runtime did not replay exactly once: "
                + restoredFailure);
            GameplayOutcomeSnapshot child = RequireChild(
                outcome.Ledger,
                change,
                31,
                parent);
            Require(child.participants.Single().displayText == frozenName,
                "Age anatomy replay replaced the frozen name with the renamed actor.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(host);
        }
    }

    private static ProductionBodyLifecycleEvidence VerifyProductionBodyLifecycle()
    {
        GameObject nonlethalObject = null;
        GameObject lethalObject = null;
        CharacterActor nonlethal = null;
        CharacterActor lethal = null;
        var worldRegistry = CharacterAiEditorTestDependencies.WorldRegistry;
        bool nonlethalRegistered = false;
        bool lethalRegistered = false;
        try
        {
            nonlethalObject = new GameObject("C:A01 Production Nonlethal");
            nonlethal = nonlethalObject.AddComponent<CharacterActor>();
            CharacterAiEditorTestDependencies.Inject(nonlethalObject);
            nonlethal.EnsureRuntimeState();
            nonlethal.Identity.SetPersistentId(
                "character:qa:age-anatomy:nonlethal");
            nonlethal.SetLifecycleState(CharacterLifecycleState.Active);
            worldRegistry.RegisterCharacter(nonlethal);
            worldRegistry.RegisterCharacterLifetime(nonlethal);
            nonlethalRegistered = true;

            lethalObject = new GameObject("C:A01 Production Lethal");
            lethal = lethalObject.AddComponent<CharacterActor>();
            CharacterAiEditorTestDependencies.Inject(lethalObject);
            lethal.EnsureRuntimeState();
            lethal.Identity.SetPersistentId(
                "character:qa:age-anatomy:lethal");
            lethal.SetLifecycleState(CharacterLifecycleState.Active);
            worldRegistry.RegisterCharacter(lethal);
            worldRegistry.RegisterCharacterLifetime(lethal);
            lethalRegistered = true;

            var clock = new UnityGameClock();
            var events = new GameEventBus();
            using var vitalsActivities = new CharacterVitalsActivityObserver(
                events,
                worldRegistry);
            vitalsActivities.Initialize();
            var body = new CharacterBodyHealthRuntime(new DungeonStory.Foundation.UnityRealtimeClock(),
                worldRegistry,
                clock,
                events,
                new DynamicFrameWorkBudget(clock, new UnityUiClock()),
                new ProductionAnatomyCatalog(),
                new DungeonRuntimeAggregateRootStore(),
                CharacterAiEditorTestDependencies.GameCalendar);
            var deathFactory = new CharacterDeathEventFactory(
                worldRegistry,
                CharacterAiEditorTestDependencies.GameCalendar);
            nonlethal.Stats.ConstructCharacterVitals(
                new CharacterStatsVitalsService(
                    body,
                    body,
                    events,
                    deathFactory,
                    new NoOwnerDeathLifecycle()));
            lethal.Stats.ConstructCharacterVitals(
                new CharacterStatsVitalsService(
                    body,
                    body,
                    events,
                    deathFactory,
                    new NoOwnerDeathLifecycle()));
            body.ConfigureVitals(nonlethal, 100f, resetCurrentHealth: true);
            body.ConfigureVitals(lethal, 100f, resetCurrentHealth: true);

            var catalog = new ProductionLifeCatalog();
            using var outcome = new OutcomeFixture();
            body.ConfigureFactPublication(outcome.Facts, outcome.Facts);
            CharacterLifeRuntime life = CreateLife(catalog);
            var runtime = new AgeConditionAnatomyProjectionRuntime(
                life,
                catalog,
                new World(nonlethal, lethal),
                body,
                body,
                outcome.Facts,
                outcome.Facts,
                events);

            float nonlethalBefore = body.GetAnatomySnapshot(nonlethal)
                .Nodes.Single(value =>
                    value.nodeId == ProductionAnatomyCatalog.NonvitalNodeId)
                .currentHealth;
            AgeConditionChange nonlethalChange = new(
                nonlethal.BuildingCharacterId,
                ProductionLifeCatalog.NonlethalConditionId,
                AgeConditionSeverity.Mild,
                AgeConditionSeverity.Moderate,
                newlyDiagnosed: false);
            DomainFactDelivery nonlethalParent = outcome.Parent(
                nonlethalChange,
                21);
            string nonlethalOperation = runtime.Enqueue(
                nonlethalChange,
                21,
                nonlethalParent);
            Require(runtime.TryResume(
                    nonlethalOperation,
                    out string nonlethalFailure),
                nonlethalFailure);
            float nonlethalAfter = body.GetAnatomySnapshot(nonlethal)
                .Nodes.Single(value =>
                    value.nodeId == ProductionAnatomyCatalog.NonvitalNodeId)
                .currentHealth;
            GameplayOutcomeSnapshot nonlethalChild = RequireChild(
                outcome.Ledger,
                nonlethalChange,
                21,
                nonlethalParent);
            Require(!nonlethal.IsDead
                    && nonlethalAfter < nonlethalBefore
                    && nonlethalAfter > 0f,
                "Production non-vital age-condition anatomy did not remain nonlethal.");

            float lethalBefore = body.GetAnatomySnapshot(lethal)
                .Nodes.Single(value =>
                    value.nodeId == ProductionAnatomyCatalog.VitalNodeId)
                .currentHealth;
            AgeConditionChange lethalChange = new(
                lethal.BuildingCharacterId,
                ProductionLifeCatalog.LethalConditionId,
                AgeConditionSeverity.Critical,
                AgeConditionSeverity.OrganFunctionLoss,
                newlyDiagnosed: false);
            DomainFactDelivery lethalParent = outcome.Parent(
                lethalChange,
                22);
            string lethalOperation = runtime.Enqueue(
                lethalChange,
                22,
                lethalParent);
            Require(runtime.TryResume(lethalOperation, out string lethalFailure),
                lethalFailure);
            float lethalAfter = body.GetAnatomySnapshot(lethal)
                .Nodes.Single(value =>
                    value.nodeId == ProductionAnatomyCatalog.VitalNodeId)
                .currentHealth;
            GameplayOutcomeSnapshot lethalChild = RequireChild(
                outcome.Ledger,
                lethalChange,
                22,
                lethalParent);
            Require(lethal.IsDead && lethalAfter <= 0f,
                "Production vital-node age condition did not cause actual death.");

            return new ProductionBodyLifecycleEvidence(
                nonlethal.BuildingCharacterId.Value,
                ProductionAnatomyCatalog.NonvitalNodeId,
                nonlethalBefore,
                nonlethalAfter,
                nonlethal.IsDead,
                nonlethalChild,
                lethal.BuildingCharacterId.Value,
                ProductionAnatomyCatalog.VitalNodeId,
                lethalBefore,
                lethalAfter,
                lethal.IsDead,
                lethalChild);
        }
        finally
        {
            if (nonlethalRegistered && nonlethal != null)
            {
                worldRegistry.UnregisterCharacter(nonlethal);
                worldRegistry.UnregisterCharacterLifetime(nonlethal);
            }
            if (lethalRegistered && lethal != null)
            {
                worldRegistry.UnregisterCharacter(lethal);
                worldRegistry.UnregisterCharacterLifetime(lethal);
            }
            if (nonlethalObject != null)
                UnityEngine.Object.DestroyImmediate(nonlethalObject);
            if (lethalObject != null)
                UnityEngine.Object.DestroyImmediate(lethalObject);
        }
    }

    private static AgeConditionAnatomyProjectionRuntime CreateRuntime(
        CharacterLifeRuntime life,
        ICharacterLifeDefinitionCatalog catalog,
        ICharacterWorldQuery world,
        IAnatomyHealthRuntime anatomy,
        IAgeConditionAnatomyMutationTransaction body,
        OutcomeFixture outcomes) => CreateRuntime(
        life,
        catalog,
        world,
        anatomy,
        body,
        outcomes.Facts,
        outcomes.Facts);

    private static AgeConditionAnatomyProjectionRuntime CreateRuntime(
        CharacterLifeRuntime life,
        ICharacterLifeDefinitionCatalog catalog,
        ICharacterWorldQuery world,
        IAnatomyHealthRuntime anatomy,
        IAgeConditionAnatomyMutationTransaction body,
        IDomainFactPublisher facts,
        IDomainFactRecoveryPublisher recovery) => new(
        life,
        catalog,
        world,
        anatomy,
        body,
        facts,
        recovery,
        new GameEventBus());

    private static CharacterLifeRuntime CreateLife(
        ICharacterLifeDefinitionCatalog catalog) => new(
        new DungeonRuntimeAggregateRootStore(),
        catalog,
        new RandomStreamProvider(7919));

    private static AgeConditionChange Change(
        CharacterActor actor,
        AgeConditionSeverity severity)
    {
        AgeConditionSeverity previous = severity switch
        {
            AgeConditionSeverity.Mild => AgeConditionSeverity.Mild,
            AgeConditionSeverity.Moderate => AgeConditionSeverity.Mild,
            AgeConditionSeverity.Severe => AgeConditionSeverity.Moderate,
            AgeConditionSeverity.Critical => AgeConditionSeverity.Severe,
            AgeConditionSeverity.OrganFunctionLoss =>
                AgeConditionSeverity.Critical,
            _ => throw new ArgumentOutOfRangeException(nameof(severity))
        };
        return new AgeConditionChange(
            actor.BuildingCharacterId,
            Catalog.ConditionId,
            previous,
            severity,
            newlyDiagnosed: severity == AgeConditionSeverity.Mild);
    }

    private static GameplayOutcomeSnapshot RequireChild(
        GameplayOutcomeLedger ledger,
        in AgeConditionChange change,
        int absoluteDay,
        in DomainFactDelivery parent)
    {
        CharacterLifeTransitionFact parentFact =
            CharacterLifeTransitionFact.FromAgeCondition(
                change,
                absoluteDay);
        GameplayResultKey key = new(
            AgeConditionAnatomyOutcomeIds.ProducerId,
            new GameplayOperationId(parentFact.AnatomyOperationId),
            1L,
            0);
        Require(ledger.TryGetResultIdentity(key, out GameplayOutcomeReplayIdentity id)
                && id.State >= GameplayOutcomeReplayState.Committed,
            "Age-condition anatomy child outcome is missing.");
        GameplayOutcomeSnapshot child = ledger.CaptureGameplayOutcomes()
            .exactOutcomes.Single(value =>
                value.runId == id.OutcomeId.RunId.Value
                && value.sequence == id.OutcomeId.Sequence);
        Require(new GameplayOutcomeId(
                    new GameplayOutcomeRunId(child.parentRunId),
                    child.parentSequence).ToString()
                    == parent.PublicationId
                    && child.causationRelationId == "age-condition-effect",
            "Age-condition anatomy child lost its exact parent causation.");
        return child;
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

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    private sealed class ProductionBodyLifecycleEvidence
    {
        private readonly string nonlethalCharacterId;
        private readonly string nonlethalNodeId;
        private readonly float nonlethalBefore;
        private readonly float nonlethalAfter;
        private readonly bool nonlethalWasDead;
        private readonly GameplayOutcomeSnapshot nonlethalChild;
        private readonly string lethalCharacterId;
        private readonly string lethalNodeId;
        private readonly float lethalBefore;
        private readonly float lethalAfter;
        private readonly bool lethalWasDead;
        private readonly GameplayOutcomeSnapshot lethalChild;

        internal ProductionBodyLifecycleEvidence(
            string nonlethalCharacterId,
            string nonlethalNodeId,
            float nonlethalBefore,
            float nonlethalAfter,
            bool nonlethalWasDead,
            GameplayOutcomeSnapshot nonlethalChild,
            string lethalCharacterId,
            string lethalNodeId,
            float lethalBefore,
            float lethalAfter,
            bool lethalWasDead,
            GameplayOutcomeSnapshot lethalChild)
        {
            this.nonlethalCharacterId = nonlethalCharacterId;
            this.nonlethalNodeId = nonlethalNodeId;
            this.nonlethalBefore = nonlethalBefore;
            this.nonlethalAfter = nonlethalAfter;
            this.nonlethalWasDead = nonlethalWasDead;
            this.nonlethalChild = nonlethalChild
                ?? throw new ArgumentNullException(nameof(nonlethalChild));
            this.lethalCharacterId = lethalCharacterId;
            this.lethalNodeId = lethalNodeId;
            this.lethalBefore = lethalBefore;
            this.lethalAfter = lethalAfter;
            this.lethalWasDead = lethalWasDead;
            this.lethalChild = lethalChild
                ?? throw new ArgumentNullException(nameof(lethalChild));
        }

        internal bool NonlethalSurvived => !nonlethalWasDead;
        internal bool VitalNodeKilled => lethalWasDead;

        internal JObject NonlethalJson() => new()
        {
            ["characterId"] = nonlethalCharacterId,
            ["nodeId"] = nonlethalNodeId,
            ["healthBefore"] = nonlethalBefore,
            ["healthAfter"] = nonlethalAfter,
            ["isDead"] = nonlethalWasDead,
            ["childOutcome"] = JObject.FromObject(nonlethalChild)
        };

        internal JObject LethalJson() => new()
        {
            ["characterId"] = lethalCharacterId,
            ["nodeId"] = lethalNodeId,
            ["healthBefore"] = lethalBefore,
            ["healthAfter"] = lethalAfter,
            ["isDead"] = lethalWasDead,
            ["childOutcome"] = JObject.FromObject(lethalChild)
        };
    }

    private sealed class NoOwnerDeathLifecycle : IOwnerRunLifecycleService
    {
        public void HandleOwnerDeath(CharacterActor owner, string reason)
        {
        }
    }

    private sealed class ProductionLifeCatalog : ICharacterLifeDefinitionCatalog
    {
        internal const string NonlethalConditionId =
            "condition:qa-age-anatomy-production-nonlethal";
        internal const string LethalConditionId =
            "condition:qa-age-anatomy-production-lethal";
        private static readonly AgeConditionDefinition Nonlethal = new(
            NonlethalConditionId,
            false,
            new[] { ProductionAnatomyCatalog.NonvitalNodeId });
        private static readonly AgeConditionDefinition Lethal = new(
            LethalConditionId,
            false,
            new[] { ProductionAnatomyCatalog.VitalNodeId });

        public SpeciesLifeHistoryDefinition RequireLifeHistory(
            CharacterSpeciesId speciesId) => new(
            speciesId,
            1,
            4,
            18,
            60,
            80f,
            false);

        public AgeConditionDefinition RequireAgeCondition(string conditionId) =>
            conditionId switch
            {
                NonlethalConditionId => Nonlethal,
                LethalConditionId => Lethal,
                _ => throw new KeyNotFoundException(conditionId)
            };

        public IReadOnlyList<AgeConditionDefinition> GetAgeConditions(
            bool construct) => new[] { Nonlethal, Lethal };
    }

    private sealed class ProductionAnatomyCatalog : IAnatomyProfileCatalog
    {
        internal const string ProfileId = "anatomy:qa-age-condition";
        internal const string VitalNodeId = "node:qa-vital";
        internal const string NonvitalNodeId = "node:qa-nonvital";
        private readonly AnatomyProfileDefinition profile = new(
            ProfileId,
            "C:A01 QA anatomy",
            "qa-age-condition",
            Array.Empty<string>(),
            new[]
            {
                new AnatomyNodeDefinition(
                    VitalNodeId,
                    "QA vital",
                    string.Empty,
                    AnatomyNodeKind.Organ,
                    AnatomyFunction.MentalMaintenance,
                    50f,
                    1f,
                    vital: true,
                    removable: false),
                new AnatomyNodeDefinition(
                    NonvitalNodeId,
                    "QA non-vital",
                    string.Empty,
                    AnatomyNodeKind.Limb,
                    AnatomyFunction.PhysicalMobility,
                    50f,
                    1f,
                    vital: false,
                    removable: true)
            });

        public IReadOnlyList<AnatomyProfileDefinition> Profiles =>
            new[] { profile };
        public AnatomyProfileDefinition GetDefaultHumanoid() => profile;
        public AnatomyProfileDefinition GetForSpecies(string speciesId) =>
            profile;
        public bool TryGet(
            string profileId,
            out AnatomyProfileDefinition value)
        {
            value = string.Equals(
                    profileId,
                    ProfileId,
                    StringComparison.Ordinal)
                ? profile
                : null;
            return value != null;
        }
        public IReadOnlyList<string> Validate() => Array.Empty<string>();
    }

    private sealed class Catalog : ICharacterLifeDefinitionCatalog
    {
        internal const string ConditionId = "condition:qa-age-anatomy";
        private static readonly AgeConditionDefinition Condition = new(
            ConditionId,
            false,
            new[] { "node:a", "node:b" });
        public SpeciesLifeHistoryDefinition RequireLifeHistory(
            CharacterSpeciesId speciesId) => new(
            speciesId,
            1,
            4,
            18,
            60,
            80f,
            false);
        public AgeConditionDefinition RequireAgeCondition(string conditionId) =>
            conditionId == ConditionId
                ? Condition
                : throw new KeyNotFoundException(conditionId);
        public IReadOnlyList<AgeConditionDefinition> GetAgeConditions(bool construct) =>
            new[] { Condition };
    }

    private sealed class World : ICharacterWorldQuery
    {
        internal World(params CharacterActor[] actors) => Characters =
            (actors ?? Array.Empty<CharacterActor>())
            .Where(value => value != null)
            .ToArray();
        public int CharacterVersion => 1;
        public IReadOnlyList<CharacterActor> Characters { get; }
    }

    private sealed class AnatomyFixture : IAnatomyHealthRuntime
    {
        private readonly BodyFixture body;
        internal AnatomyFixture(BodyFixture body) => this.body = body;
        internal IReadOnlyList<string> VisibleNodeIds { get; set; } =
            Array.Empty<string>();
        public AnatomyHealthSnapshot GetAnatomySnapshot(CharacterActor actor) =>
            Snapshot();
        public AnatomyHealthSnapshot GetAnatomySnapshot(string characterId) =>
            Snapshot();
        private AnatomyHealthSnapshot Snapshot() => new(
            "profile:qa-age-anatomy",
            body.Nodes.Where(value => VisibleNodeIds.Contains(
                value.nodeId,
                StringComparer.Ordinal)).ToArray(),
            1f, 1f, 1f, 1f, 1f, 1f, 1f);
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
            out AnatomyNodeHealthState replacedNode, out DomainFailure failure) =>
            throw new NotSupportedException();
        public bool TryAddNodeBurden(CharacterActor actor, string nodeId,
            float rejection, float mutation, float infection,
            out DomainFailure failure) => throw new NotSupportedException();
        public bool TryReduceNodeBurden(CharacterActor actor, string nodeId,
            float rejection, float mutation, float infection,
            out DomainFailure failure) => throw new NotSupportedException();
    }

    private sealed class BodyFixture : IAgeConditionAnatomyMutationTransaction
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
        internal List<AnatomyNodeHealthState> Nodes { get; private set; }
        internal int ApplyCount { get; private set; }
        internal int CompleteCount { get; private set; }
        internal int RestoreCount { get; private set; }
        internal bool LastDestroyNode { get; private set; }
        internal int LastAppliedNodeCount { get; private set; }
        internal float TotalNodeHealth => Nodes.Sum(value => value.currentHealth);

        public CharacterPreparedAgeConditionAnatomyReceipt
            ApplyPreparedAgeConditionAnatomy(
                CharacterActor actor,
                IReadOnlyList<string> nodeIds,
                float damageFraction,
                bool destroyNode,
                string reasonCode)
        {
            ApplyCount++;
            LastDestroyNode = destroyNode;
            CharacterBodyHealthState beforeState = State();
            var deltas = new List<CharacterAnatomyDamageDelta>();
            foreach (string nodeId in nodeIds)
            {
                AnatomyNodeHealthState node = Nodes.Single(value =>
                    value.nodeId == nodeId);
                float before = node.currentHealth;
                float damage = destroyNode
                    ? before
                    : node.maxHealth * damageFraction;
                node.currentHealth = Mathf.Max(0f, before - damage);
                deltas.Add(new CharacterAnatomyDamageDelta(
                    node.nodeId,
                    node.maxHealth,
                    before,
                    damage,
                    node.currentHealth,
                    0f,
                    0f));
            }
            LastAppliedNodeCount = deltas.Count;
            float aggregateBefore = beforeState.currentHealth;
            float aggregateAfter = Mathf.Max(
                1f,
                aggregateBefore - deltas.Sum(value => value.RequestedDamage));
            return new CharacterPreparedAgeConditionAnatomyReceipt(
                new CharacterBodyHealthMutationSnapshot(id, beforeState),
                deltas,
                aggregateBefore,
                aggregateAfter,
                reasonCode);
        }

        public void CompletePreparedAgeConditionAnatomy(
            CharacterActor actor,
            in CharacterPreparedAgeConditionAnatomyReceipt receipt) =>
            CompleteCount++;

        public void RestorePreparedAgeConditionAnatomy(
            CharacterActor actor,
            in CharacterBodyHealthMutationSnapshot snapshot,
            string reason)
        {
            RestoreCount++;
            Nodes = snapshot.State.anatomyNodes.Select(Clone).ToList();
        }

        private CharacterBodyHealthState State() => new()
        {
            characterId = id.Value,
            maxHealth = Math.Max(1f, TotalNodeHealth),
            currentHealth = Math.Max(1f, TotalNodeHealth),
            anatomyNodes = Nodes.Select(Clone).ToList(),
            parts = new List<CharacterBodyPartHealthState>()
        };
        private static AnatomyNodeHealthState Clone(AnatomyNodeHealthState value) =>
            new()
            {
                nodeId = value.nodeId,
                maxHealth = value.maxHealth,
                currentHealth = value.currentHealth,
                bleedingPerSecond = value.bleedingPerSecond,
                missing = value.missing
            };
    }

    private sealed class KnownRejectingFactPublisher : IDomainFactPublisher
    {
        private readonly IDomainFactPublisher inner;
        private bool rejectNext = true;

        internal KnownRejectingFactPublisher(IDomainFactPublisher inner) =>
            this.inner = inner
                ?? throw new ArgumentNullException(nameof(inner));

        internal int PublishCount { get; private set; }

        public bool TryReserve<TFact>(in TFact proposed,
            out DomainFactLease lease,
            out string failure) =>
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
                    "qa-age-anatomy-observer-rejected");
            }
            return inner.Publish(lease, actual);
        }

        public bool PublishBatch(
            IReadOnlyList<DomainFactEmission> facts,
            DomainFactDelivery[] results,
            out string failure) =>
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
            "qa-age-anatomy-recovery-unavailable";
        private const string DeliveryFailure =
            "qa-age-anatomy-delivery-ambiguous";
        private readonly IDomainFactPublisher inner;

        internal AmbiguousAfterCommitFactPublisher(
            IDomainFactPublisher inner) =>
            this.inner = inner
                ?? throw new ArgumentNullException(nameof(inner));

        internal int PublishCount { get; private set; }
        internal int RecoveryCount { get; private set; }

        public bool TryReserve<TFact>(in TFact proposed,
            out DomainFactLease lease,
            out string failure) =>
            inner.TryReserve(proposed, out lease, out failure);

        public DomainFactDelivery Publish<TFact>(
            in DomainFactLease lease,
            in TFact actual)
        {
            DomainFactDelivery delivered = inner.Publish(lease, actual);
            if (!delivered.Completed)
            {
                throw new InvalidOperationException(
                    "Age anatomy ambiguity fixture requires a committed delivery.");
            }
            PublishCount++;
            return InDoubt(delivered);
        }

        public bool PublishBatch(
            IReadOnlyList<DomainFactEmission> facts,
            DomainFactDelivery[] results,
            out string failure)
        {
            bool accepted = inner.PublishBatch(facts, results, out failure);
            if (!accepted || results.Any(value => !value.Completed))
            {
                throw new InvalidOperationException(
                    "Age anatomy ambiguity fixture requires a committed cohort.");
            }
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

        public bool ReplayBatch(
            IReadOnlyList<DomainFactReplay> facts,
            DomainFactDelivery[] results,
            out string failure)
        {
            if (facts == null || results == null || facts.Count == 0
                || facts.Count != results.Length)
            {
                throw new ArgumentException(
                    "Invalid blocked age anatomy replay shape.");
            }
            RecoveryCount++;
            for (int index = 0; index < results.Length; index++)
                results[index] = DomainFactDelivery.Reject(RecoveryFailure);
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
        private readonly MigratedProducerOutcomeRuntime parents;
        private readonly IDisposable parentBinding;
        private readonly IDisposable childBinding;
        internal OutcomeFixture()
        {
            var migratedAdapter = new MigratedProducerOutcomeAdapter();
            Registry = new GameplayOutcomeRegistry(
                new IGameplayOutcomeDescriptor[]
                {
                    new AgeConditionAnatomyOutcomeDescriptor()
                },
                new IGameplayOutcomeAdapterRegistration[]
                {
                    migratedAdapter,
                    new AgeConditionAnatomyOutcomeAdapter()
                },
                new IGameplayOutcomeDescriptorCatalog[]
                {
                    new MigratedProducerOutcomeDescriptorCatalog(
                        new KoreanJosaFormatter())
                });
            Ledger = new GameplayOutcomeLedger(
                Registry,
                new GameplayOutcomeBufferLimits(64, 4, 256),
                new GameplayOutcomeRunId("qa-age-condition-anatomy"),
                1L);
            Recorder = new GameplayOutcomeRecorder(
                Ledger,
                Registry,
                new GameEventBus());
            parents = new MigratedProducerOutcomeRuntime(
                new DungeonRuntimeAggregateRootStore(),
                Ledger,
                Recorder,
                Ledger);
            Facts = new DomainFactDispatcher();
            var commits = new GameplayDomainFactCommitGroup(
                Recorder,
                Ledger);
            parentBinding = CharacterLifeTransitionFactMapping
                .CreateBinding(parents, commits)
                .Connect(Facts);
            childBinding = AgeConditionAnatomyFactMapping.CreateBinding(
                    Registry,
                    Ledger,
                    Recorder,
                    commits)
                .Connect(Facts);
            Facts.Subscribe(
                "log:character.fatality",
                new MigratedDomainFactObserver<CharacterFatalityFact>(
                    CharacterFatalityFactMapping.CaptureDeath,
                    parents,
                    commits));
            Facts.Subscribe(
                "log:character.fatal-critical-transition",
                new MigratedDomainFactObserver<
                    CharacterFatalCriticalTransitionFact>(
                    CharacterFatalityFactMapping.CaptureCritical,
                    parents,
                    commits));
            Facts.Subscribe(
                "log:character.fatal-downed",
                new MigratedDomainFactObserver<CharacterFatalDownedFact>(
                    CharacterFatalityFactMapping.CaptureDowned,
                    parents,
                    commits));
        }
        internal GameplayOutcomeRegistry Registry { get; }
        internal GameplayOutcomeLedger Ledger { get; }
        internal GameplayOutcomeRecorder Recorder { get; }
        internal DomainFactDispatcher Facts { get; }
        internal int ExactChildCount => Ledger.CaptureGameplayOutcomes()
            .exactOutcomes.Count(value => string.Equals(
                value.producerId,
                AgeConditionAnatomyOutcomeIds.ProducerId,
                StringComparison.Ordinal));

        internal DomainFactDelivery Parent(
            in AgeConditionChange change,
            int absoluteDay)
        {
            CharacterLifeTransitionFact fact =
                CharacterLifeTransitionFact.FromAgeCondition(
                    change,
                    absoluteDay);
            Require(Facts.TryReserve(
                    fact,
                    out DomainFactLease lease,
                    out string failure),
                failure);
            try
            {
                string key = lease.PublicationKey;
                DomainFactDelivery delivered = Facts.Publish(lease, fact);
                CharacterLifeObservationState.RequireDelivery(delivered, key);
                return delivered;
            }
            finally
            {
                Facts.Cancel(lease);
            }
        }

        internal bool HasPublication(string publicationId) =>
            Ledger.CaptureGameplayOutcomes().exactOutcomes.Any(value =>
                new GameplayOutcomeId(
                    new GameplayOutcomeRunId(value.runId),
                    value.sequence).ToString() == publicationId);

        public void Dispose()
        {
            childBinding.Dispose();
            parentBinding.Dispose();
            Facts.Dispose();
        }
    }
}
#endif
