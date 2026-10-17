using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation.Facts;
using UnityEditor;
using UnityEngine;
using VContainer;
using Object = UnityEngine.Object;

public static class OffenseRewardDebugScenarios
{
    public static void VerifyRuntimeOwnerRestoreIsolation()
    {
        // Controlled production-owner test; actual full-world restore is a separate run.
        using ExpeditionRewardScenario scenario = new(includeExpeditionFixture: false);
        CharacterActor actor = scenario.CreateCharacter(
            "Restore Isolation", CharacterType.NPC, CharacterRole.Regular, 100);
        OffenseTargetDefinition target = OffenseEditorTestDependencies.CreateCampaignCatalog()
            .Targets.First(value => value != null && value.id == "food_farm");
        OffenseExpeditionRun runA = new("restore:owner:a", target, new[] { actor }, 1f);
        OffenseExpeditionRun runB = new("restore:owner:b", target, new[] { actor }, 2f);

        OffenseRewardRuntime reward = scenario.Reward.Runtime;
        reward.PublishRestoreCandidate(OffenseRewardRuntime.BuildRestoreCandidate(
            11, new Dictionary<StockCategory, int>(), Array.Empty<int>(), Array.Empty<int>()));
        var rewardB = OffenseRewardRuntime.BuildRestoreCandidate(
            22, new Dictionary<StockCategory, int>(), Array.Empty<int>(), Array.Empty<int>());
        VerifyRuntimeOwnerPublication(reward,
            () => reward.PublishRestoreCandidate(rewardB),
            () => reward.State.MoneyEarned == 22,
            () => reward.State.MoneyEarned.ToString(), "state");

        GameObject host = new("Offense Restore Owner Fixture");
        try
        {
            OffenseExpeditionRuntime expedition = host.AddComponent<OffenseExpeditionRuntime>();
            OffenseExpeditionResult History(string id, float power) => new(
                id, target.id, target.title, true, power, 1f, 1f, 12f,
                Array.Empty<OffenseExpeditionMemberSnapshot>(), Array.Empty<string>());
            expedition.PublishRestoreCandidate(expedition.BuildRestoreCandidate(
                new[] { runA }, new[] { History("history:a", 1f) }));
            var expeditionB = expedition.BuildRestoreCandidate(
                new[] { runB }, new[] { History("history:b", 2f) });
            // Materialize cached read-only views so rollback must preserve them as well.
            _ = expedition.ActiveExpeditions;
            _ = expedition.ResultHistory;
            int expeditionEvents = 0;
            expedition.StateChanged += () => expeditionEvents++;
            VerifyRuntimeOwnerPublication(expedition,
                () => expedition.PublishRestoreCandidate(expeditionB),
                () => ReferenceEquals(expedition.ActiveExpeditions.Single(), runB)
                    && expedition.ResultHistory.Single().expeditionId == "history:b",
                () => string.Join("|", expedition.ActiveExpeditions.Select(value => value.ExpeditionId))
                    + "/" + string.Join("|", expedition.ResultHistory.Select(value => value.expeditionId)),
                "activeExpeditions", "resultHistory", "activeExpeditionsView",
                "resultHistoryView", "resumeRestoredWorldStatePending");
            if (expeditionEvents != 0)
                throw new InvalidOperationException("Restore publication emitted an expedition event.");
        }
        finally { Object.DestroyImmediate(host); }

        OffenseBattleRuntime battle = scenario.Battle;
        if (!battle.TryStartBattle(runA, out string failure) || battle.Session == null)
            throw new InvalidOperationException("Restore isolation could not start battle: " + failure);
        battle.AdvanceToPlayerDecision();
        if (battle.EnemyTacticalTrace.Count == 0
            && !battle.TryIssuePlayerCommand(OffenseBattleActionType.Guard,
                string.Empty, string.Empty, out _))
            throw new InvalidOperationException("Restore isolation could not advance the first public guard turn.");
        if (battle.EnemyTacticalTrace.Count == 0)
            throw new InvalidOperationException("Restore isolation requires actual enemy execution trace.");
        battle.SetBattleViewVisible(false);
        OffenseBattlePersistenceState savedBattleA = battle.CapturePersistentState();
        if (savedBattleA == null)
            throw new InvalidOperationException("Restore isolation requires an active battle baseline.");
        string battleBefore = JsonUtility.ToJson(savedBattleA);
        var battleA = battle.PreparePersistentRestore(runA, savedBattleA, null);
        var battleB = battle.PrepareEmptyPersistentRestore();
        int battleEvents = 0;
        battle.StateChanged += () => battleEvents++;
        VerifyRuntimeOwnerPublication(battle,
            () => battle.PublishPersistentRestore(battleB),
            () => battle.Session == null && !battle.IsBattleViewVisible
                && battle.EnemyTacticalTrace.Count == 0,
            () => JsonUtility.ToJson(battle.CapturePersistentState()),
            "<Session>k__BackingField", "actorsById", "activeEncounterId",
            "activeEnemyIndividuals", "completionRaised", "<IsBattleViewVisible>k__BackingField",
            "enemyTacticalTrace", "enemyTacticalTraceSequence");
        if (battleEvents != 0 || string.IsNullOrEmpty(battleBefore))
            throw new InvalidOperationException("Battle restore emitted an event or lacked a live baseline.");
        battle.PublishPersistentRestore(battleA);
        if (JsonUtility.ToJson(battle.CapturePersistentState()) != battleBefore
            || !battle.IsBattleViewVisible)
            throw new InvalidOperationException("Validated active battle restore lost its persisted state.");
    }

    public static void RunPersistentBattleSnapshotRestoreRegression()
    {
        VerifyPersistentBattleSnapshotRestore();
        Debug.Log("OFFENSE_PERSISTENT_BATTLE_SNAPSHOT_RESTORE=PASS");
    }

    private static bool VerifyPersistentBattleSnapshotRestore()
    {
        using ExpeditionRewardScenario scenario = new(includeExpeditionFixture: false);
        CharacterActor actor = scenario.CreateCharacter(
            "Persistent Battle Snapshot",
            CharacterType.NPC,
            CharacterRole.Regular,
            100);
        string actorId = scenario.GetPersistentId(actor);
        OffenseTargetDefinition target = OffenseEditorTestDependencies
            .CreateCampaignCatalog()
            .Targets
            .First(value => value != null
                && string.Equals(value.id, "food_farm", StringComparison.Ordinal));
        OffenseExpeditionRun expedition = new(
            "reward:persistent-battle-snapshot-restore",
            target,
            new[] { actor },
            1f);
        OffenseBattleRuntime battle = scenario.Battle;
        Require(battle.TryStartBattle(expedition, out string failure)
                && battle.Session != null,
            "Persistent battle snapshot fixture did not start: " + failure);

        OffenseBattlePersistenceState savedBattle = battle.CapturePersistentState();
        Require(savedBattle != null,
            "Persistent battle snapshot fixture did not capture its live battle.");
        string liveBattleJson = JsonUtility.ToJson(savedBattle);
        OffenseBattleSession liveSession = battle.Session;
        bool liveViewVisible = battle.IsBattleViewVisible;
        int stateEvents = 0;
        battle.StateChanged += () => stateEvents++;
        scenario.RejectBattlePerformanceEvaluation();

        OffenseBattlePersistenceState detachedSavedBattle =
            JsonUtility.FromJson<OffenseBattlePersistenceState>(liveBattleJson);
        OffenseBattleCombatantPersistenceState savedAlly =
            detachedSavedBattle.combatants.Single(value => value != null
                && string.Equals(value.persistentId, actorId, StringComparison.Ordinal));
        savedAlly.maxHealth = 137f;
        savedAlly.attack = 23f;
        savedAlly.strength = 29f;
        savedAlly.toughness = 31f;
        savedAlly.dexterity = 37f;
        savedAlly.moveSpeed = 41f;
        savedAlly.shooting = 43f;
        savedAlly.evasion = 47f;
        savedAlly.currentHealth = 89f;
        savedAlly.formation = OffenseFormationSlot.Rear;
        savedAlly.participatesInInitiative = false;
        savedAlly.arcanePowerMultiplier = 1.75f;
        savedAlly.hasArcanePowerMultiplier = true;
        detachedSavedBattle.initiativeOrder.RemoveAll(value => string.Equals(
            value,
            actorId,
            StringComparison.Ordinal));
        Require(detachedSavedBattle.initiativeOrder.Count > 0,
            "Persistent battle snapshot requires a remaining initiative participant.");
        detachedSavedBattle.currentOrderIndex = Mathf.Clamp(
            detachedSavedBattle.currentOrderIndex,
            0,
            detachedSavedBattle.initiativeOrder.Count - 1);
        string savedBattleJson = JsonUtility.ToJson(detachedSavedBattle);
        OffenseBattleRestoreCandidate prepared = battle.PreparePersistentRestore(
            expedition,
            detachedSavedBattle,
            null);
        Require(prepared != null,
            "Persistent battle snapshot did not prepare a restore candidate.");
        Require(ReferenceEquals(battle.Session, liveSession)
                && string.Equals(
                    JsonUtility.ToJson(battle.CapturePersistentState()),
                    liveBattleJson,
                    StringComparison.Ordinal)
                && battle.IsBattleViewVisible == liveViewVisible
                && stateEvents == 0,
            "Preparing the battle snapshot changed the live battle before publication.");
        Require(scenario.BattlePerformanceEvaluationCount == 0,
            "Preparing the saved battle snapshot re-evaluated live character performance.");

        battle.PublishPersistentRestore(prepared);
        Require(string.Equals(
                JsonUtility.ToJson(battle.CapturePersistentState()),
                savedBattleJson,
                StringComparison.Ordinal),
            "Published battle snapshot did not preserve the complete saved battle JSON.");
        Require(battle.IsBattleViewVisible == liveViewVisible,
            "Published battle snapshot did not preserve battle-view visibility.");
        Require(stateEvents == 0 && scenario.BattlePerformanceEvaluationCount == 0,
            "Publishing the saved battle snapshot emitted an event or re-evaluated performance.");

        void RequireMalformedSnapshotRejected(
            OffenseBattlePersistenceState malformed,
            string description)
        {
            OffenseBattleSession sessionBefore = battle.Session;
            string battleBefore = JsonUtility.ToJson(battle.CapturePersistentState());
            bool viewBefore = battle.IsBattleViewVisible;
            int eventsBefore = stateEvents;
            int performanceBefore = scenario.BattlePerformanceEvaluationCount;
            Require(battle.TryGetActor(actorId, out CharacterActor actorBefore),
                description + " lacked the live actor-map baseline.");
            bool rejected = false;
            try
            {
                battle.PreparePersistentRestore(expedition, malformed, null);
            }
            catch (InvalidOperationException)
            {
                rejected = true;
            }

            Require(rejected, description + " was not rejected.");
            Require(ReferenceEquals(battle.Session, sessionBefore)
                    && string.Equals(
                        JsonUtility.ToJson(battle.CapturePersistentState()),
                        battleBefore,
                        StringComparison.Ordinal)
                    && battle.IsBattleViewVisible == viewBefore
                    && stateEvents == eventsBefore
                    && battle.TryGetActor(actorId, out CharacterActor actorAfterPrepare)
                    && ReferenceEquals(actorAfterPrepare, actorBefore)
                    && scenario.BattlePerformanceEvaluationCount == performanceBefore,
                description + " changed the live battle while staging was rejected.");

            bool publicRejected = false;
            try
            {
                publicRejected = !battle.TryRestoreBattle(
                    expedition,
                    malformed,
                    out _);
            }
            catch (InvalidOperationException)
            {
                publicRejected = true;
            }

            Require(publicRejected,
                description + " was not rejected by the public restore entry.");
            Require(ReferenceEquals(battle.Session, sessionBefore)
                    && string.Equals(
                        JsonUtility.ToJson(battle.CapturePersistentState()),
                        battleBefore,
                        StringComparison.Ordinal)
                    && battle.IsBattleViewVisible == viewBefore
                    && stateEvents == eventsBefore
                    && battle.TryGetActor(actorId, out CharacterActor actorAfterPublic)
                    && ReferenceEquals(actorAfterPublic, actorBefore)
                    && scenario.BattlePerformanceEvaluationCount == performanceBefore,
                description + " changed the live battle while public restore was rejected.");
        }

        OffenseBattlePersistenceState invalidStat =
            JsonUtility.FromJson<OffenseBattlePersistenceState>(savedBattleJson);
        invalidStat.combatants.Single(value => value != null
            && string.Equals(value.persistentId, actorId, StringComparison.Ordinal)).attack =
            float.NaN;
        RequireMalformedSnapshotRejected(invalidStat, "NaN saved ally stat");

        OffenseBattlePersistenceState mismatchedId =
            JsonUtility.FromJson<OffenseBattlePersistenceState>(savedBattleJson);
        mismatchedId.combatants.Single(value => value != null
            && string.Equals(value.persistentId, actorId, StringComparison.Ordinal)).persistentId =
            actorId + ":mismatch";
        RequireMalformedSnapshotRejected(mismatchedId, "Mismatched saved ally ID");
        return true;
    }

    private static void VerifyRuntimeOwnerPublication(
        IDungeonRestoreTransactionParticipant owner, Action stage, Func<bool> isCandidate,
        Func<string> captureEvidence,
        params string[] fieldNames)
    {
        const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Instance
            | System.Reflection.BindingFlags.NonPublic;
        var fields = fieldNames.Select(name => owner.GetType().GetField(name, flags)
            ?? throw new InvalidOperationException("Missing restore observation field: " + name)).ToArray();
        object[] before = fields.Select(field => field.GetValue(owner)).ToArray();
        string beforeEvidence = captureEvidence();
        void RequireOriginal()
        {
            if (captureEvidence() != beforeEvidence)
                throw new InvalidOperationException(owner.ParticipantId + " changed original state content.");
            for (int index = 0; index < fields.Length; index++)
            {
                object actual = fields[index].GetValue(owner);
                bool same = fields[index].FieldType.IsValueType
                    ? Equals(before[index], actual) : ReferenceEquals(before[index], actual);
                if (!same) throw new InvalidOperationException(
                    owner.ParticipantId + " failed exact rollback: " + fields[index].Name);
            }
        }
        OffenseRestorePublicationDebugScenarios.VerifyRegistryRollback(
            owner, stage, RequireOriginal, isCandidate);
        owner.BeginRestoreCandidate();
        stage();
        RequireOriginal();
        owner.DiscardRestoreCandidate();
        RequireOriginal();
        owner.BeginRestoreCandidate();
        stage();
        owner.PublishRestoreCandidate();
        if (!isCandidate()) throw new InvalidOperationException(owner.ParticipantId + " did not publish B.");
        owner.RollbackPublishedRestoreCandidate();
        RequireOriginal();
        owner.BeginRestoreCandidate();
        stage();
        owner.PublishRestoreCandidate();
        owner.CompleteRestoreCandidate();
        if (!isCandidate()) throw new InvalidOperationException(owner.ParticipantId + " lost completed B.");
        stage(); // Existing standalone restore still applies directly outside a transaction.
        if (!isCandidate()) throw new InvalidOperationException(owner.ParticipantId + " broke standalone restore.");
    }

    public static void RunMemoryErasureBossAwardObserverFocusedForBatchMode()
    {
        string output = Environment.GetEnvironmentVariable(
            "DUNGEONSTORY_AUDIT_REMEDIATION_OUTPUT");
        string qaRoot = System.IO.Path.GetFullPath(System.IO.Path.Combine(
            Application.dataPath,
            "..",
            "Artifacts",
            "QA")) + System.IO.Path.DirectorySeparatorChar;
        if (!Application.isBatchMode
            || string.IsNullOrWhiteSpace(output)
            || !System.IO.Path.IsPathRooted(output)
            || !System.IO.Path.GetFullPath(output).StartsWith(
                qaRoot,
                StringComparison.OrdinalIgnoreCase)
            || System.IO.Directory.Exists(output))
        {
            throw new InvalidOperationException(
                "Boss-award observer checks require new project-local batch evidence.");
        }

        string probe = System.IO.File.ReadAllText(System.IO.Path.Combine(
            System.IO.Directory.GetParent(
                System.IO.Path.GetFullPath(output)).FullName,
            "isolation-probe.txt"));
        Require(probe.Contains("PASS_NONINTERACTIVE_PROBE")
                && probe.Contains("visible=false")
                && probe.Contains("windowCreatedAndDestroyed=true")
                && probe.Contains("inJob=true"),
            "Boss-award observer checks require the noninteractive desktop probe.");
        Require(VerifyMemoryErasureSealBossAwardLifecycle(),
            "Memory-erasure boss-award observer recovery checks failed.");
        Require(
            MemoryErasureSealBossAwardCausalProductionEntryDebugScenarios
                .RunCausalOwnerFamily(log: true),
            "Memory-erasure boss-award causal owner-family checks failed.");
        Debug.Log("B87_BOSS_AWARD_OBSERVER_FOCUSED=PASS");
    }

    [MenuItem("DungeonStory/Debug/Offense/Run P3 Reward Scenarios")]
    public static void RunFromMenu()
    {
        bool success = RunAll(true);
        if (!success)
        {
            Debug.LogError("P3 offense reward scenarios failed.");
        }
    }

    public static bool RunAll(bool logSuccess)
    {
        List<string> errors = new List<string>();

        RunScenario("보상 서비스 돈/재고/실물 귀환 지급", VerifyMoneyStockAndStateRewards, errors);
        RunScenario("희귀 시설과 설계도 지급", VerifyRareFacilityAndBlueprintRewards, errors);
        RunScenario("지정 전략 설계도만 지급", VerifySpecificStrategyBlueprintReward, errors);
        RunScenario("원정 완료 시 보상 지급 연결", VerifyExpeditionCompletionGrantsRewards, errors);
        RunScenario("보상 핸들러 개방형 확장", VerifyOpenRewardHandlerExtension, errors);
        RunScenario("방어 설계도 판정 capability 확장", VerifyDefenseBlueprintUnlockCapability, errors);
        RunScenario("계획 전투 명령 payload 전달 회귀", VerifyPlannedCommandPayloadForwarding, errors);
        RunScenario(
            "지역 보스 망각의 인장 최초 지급과 복원 재조정",
            VerifyMemoryErasureSealBossAwardLifecycle,
            errors);

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
            Debug.Log("P3 offense reward scenarios passed.");
        }

        return true;
    }

    public static void RunStockRewardObserverFocused()
    {
        Require(VerifyMoneyStockAndStateRewards(),
            "Direct money/stock reward grant checks failed.");
        Require(VerifyExpeditionCompletionGrantsRewards(),
            "Expedition reward delivery checks failed.");
        Debug.Log("B117_OFFENSE_STOCK_REWARD_OBSERVER=PASS");
    }

    public static bool VerifyRegionalPressureReceiptUnits()
    {
        using ScenarioContext context = new ScenarioContext(0);
        using RewardFixture fixture = new RewardFixture(context);
        OffenseTargetDefinition target = new OffenseTargetDefinition
        {
            id = "reward-pressure-receipt",
            title = "Regional pressure receipt",
            regionId = OffenseRegionRuntime.BorderTradeRegionId,
            regionDisplayName = "변경 교역권",
            factionId = OffenseRegionRuntime.HumanFactionId,
            strategicPressureAxis = StrategicPressureAxis.Logistics,
            strategicPressureAmount = 15f
        };
        OffenseRewardContext rewardContext = context.CreateRewardContext(target);
        rewardContext.regionRuntime = fixture.Regions;
        OffenseRegionState region = fixture.Regions.Regions.Single(value =>
            string.Equals(
                value.regionId,
                OffenseRegionRuntime.BorderTradeRegionId,
                StringComparison.Ordinal));

        IReadOnlyList<OffenseRewardGrantResult> fullGrant = CreateGrantService().GrantRewards(
            new[] { Reward(new OffenseRegionalPressureRewardSpec(), "receipt units", 1) },
            rewardContext);
        bool fullGrantValid = fullGrant.Count == 1
            && fullGrant[0].success
            && fullGrant[0].requestedAmount == 15
            && fullGrant[0].grantedAmount == 15
            && Mathf.Approximately(region.logisticsDamage, 15f);

        target.strategicPressureAmount = 14f;
        region.logisticsDamage = 95f;
        IReadOnlyList<OffenseRewardGrantResult> cappedGrant = CreateGrantService().GrantRewards(
            new[] { Reward(new OffenseRegionalPressureRewardSpec(), "receipt units", 1) },
            rewardContext);
        bool cappedGrantValid = cappedGrant.Count == 1
            && cappedGrant[0].success
            && cappedGrant[0].requestedAmount == 14
            && cappedGrant[0].grantedAmount == 5
            && Mathf.Approximately(region.logisticsDamage, 100f);

        IReadOnlyList<OffenseRewardGrantResult> saturatedGrant = CreateGrantService().GrantRewards(
            new[] { Reward(new OffenseRegionalPressureRewardSpec(), "receipt units", 1) },
            rewardContext);
        bool saturatedGrantValid = saturatedGrant.Count == 1
            && !saturatedGrant[0].success
            && saturatedGrant[0].requestedAmount == 14
            && saturatedGrant[0].grantedAmount == 0
            && Mathf.Approximately(region.logisticsDamage, 100f);

        return fullGrantValid && cappedGrantValid && saturatedGrantValid;
    }

    public static bool VerifyMemoryErasureSealBossAwardLifecycle()
    {
        OffenseRegionRuntime sourceRegions = new OffenseRegionRuntime();
        MemorySealPhysicalFixture physical = new MemorySealPhysicalFixture();
        var calendar = new EditorFixedGameCalendar(day: 17, hour: 5);
        using EvolutionGameplayOutcomeEditorFixture outcomes = new(
            "run:memory-erasure-boss-award-editor",
            calendar: calendar);
        var facts = new FaultingMemoryErasureBossAwardFacts(
            outcomes.BossAwardFacts,
            outcomes.BossAwardFacts);
        var publications = new MemoryErasureBossAwardPublicationRuntime(
            facts,
            outcomes.BossAwardFacts);
        MemoryErasureSealBossAwardService sourceService =
            new MemoryErasureSealBossAwardService(
                sourceRegions,
                BatchACoreSessionSaveDebugScenarios.DefaultInterfaceProxy
                    .Create<IOffenseWorldSimulation>(),
                physical.Source,
                MemorySealDropZoneFixture.Instance,
                publications,
                calendar);

        MemoryErasureSealBossAwardResult first = AwardRegionBoss(
            sourceService,
            OffenseRegionRuntime.BorderTradeRegionId,
            "memory-seal-border");
        MemoryErasureSealBossAwardResult duplicate = AwardRegionBoss(
            sourceService,
            OffenseRegionRuntime.BorderTradeRegionId,
            "memory-seal-border-repeat");
        MemoryErasureSealBossAwardResult secondRegion = AwardRegionBoss(
            sourceService,
            OffenseRegionRuntime.RivalOutpostRegionId,
            "memory-seal-rival");

        facts.HideNextCommitAcknowledgement = true;
        MemoryErasureSealBossAwardResult interrupted = AwardRegionBoss(
            sourceService,
            OffenseRegionRuntime.SealedZoneRegionId,
            "memory-seal-pending");
        MemoryErasureBossAwardPublicationData pendingObservation =
            publications.CaptureForRegion(
                OffenseRegionRuntime.SealedZoneRegionId);
        Require(
            interrupted.Status == MemoryErasureSealBossAwardStatus.Failed
                && pendingObservation != null
                && !pendingObservation.delivered
                && !string.IsNullOrWhiteSpace(
                    pendingObservation.fact.physicalCommitId)
                && !string.IsNullOrWhiteSpace(
                    pendingObservation.publicationKey)
                && string.IsNullOrEmpty(pendingObservation.publicationId)
                && string.IsNullOrEmpty(pendingObservation.fingerprint)
                && !sourceRegions.Regions.Single(region => region.regionId
                    == OffenseRegionRuntime.SealedZoneRegionId)
                    .memoryErasureSealAwardPublished
                && sourceRegions.CapturePendingMemoryErasureSealAwardRegionIds()
                    .Single() == OffenseRegionRuntime.SealedZoneRegionId,
            "Committed boss-award observation ACK loss did not retain one frozen pending fact.");

        WorldItemStackSnapshot deliveredSeal = physical.Gateway.GetAllStacks()
            .Single(stack => ProductionOutputCommitComponentCodec.Matches(
                stack.Components,
                pendingObservation.fact.physicalCommitId));
        Require(
            physical.Gateway.DeleteStack(deliveredSeal.StackId),
            "The delivered boss-award seal could not be moved or consumed before observation recovery.");
        int physicalSpawnCallsBeforeRecovery = physical.Gateway.SpawnCallCount;

        string publicationJson =
            new MemoryErasureBossAwardPublicationSaveSection(publications)
                .Capture();
        MemoryErasureBossAwardPublicationSaveSection.ValidateJson(
            publicationJson);
        MemoryErasureBossAwardPublicationSaveData savedPublications =
            DungeonSaveJson.Deserialize<
                MemoryErasureBossAwardPublicationSaveData>(publicationJson);
        GameplayOutcomeLedgerSaveData committedLedger = outcomes.Ledger
            .CaptureGameplayOutcomes();
        MemoryErasureBossAwardFactPreflight.ValidateObservation(
            savedPublications.observations.Single(value =>
                value.fact.regionId
                    == OffenseRegionRuntime.SealedZoneRegionId),
            committedLedger);

        DungeonOffenseRegionSaveData saved = sourceRegions.Capture();
        saved.regions.Single(region => region.regionId
            == OffenseRegionRuntime.SealedZoneRegionId).displayName =
            "복원 뒤 변경된 봉인 지대";

        OffenseRegionRuntime restoredRegions = new OffenseRegionRuntime();
        restoredRegions.PublishRestoreCandidate(
            restoredRegions.BuildRestoreCandidate(saved));
        var restoredPublications =
            new MemoryErasureBossAwardPublicationRuntime(
                outcomes.BossAwardFacts,
                outcomes.BossAwardFacts);
        restoredPublications.Restore(savedPublications);
        MemoryErasureSealBossAwardService restoredService =
            new MemoryErasureSealBossAwardService(
                restoredRegions,
                BatchACoreSessionSaveDebugScenarios.DefaultInterfaceProxy
                    .Create<IOffenseWorldSimulation>(),
                physical.Source,
                MemorySealDropZoneFixture.Instance,
                restoredPublications,
                new EditorFixedGameCalendar(day: 99, hour: 0));
        restoredService.OnRestoreCompleted();
        Require(
            restoredService.TryReconcilePendingAwards(
                out string recoveryFailure),
            "Saved boss-award observation did not replay: "
                + recoveryFailure);

        MemoryErasureSealBossAwardResult afterRecoveryDuplicate =
            AwardRegionBoss(
                restoredService,
                OffenseRegionRuntime.SealedZoneRegionId,
                "memory-seal-pending-repeat");
        IReadOnlyList<string> pending = restoredRegions
            .CapturePendingMemoryErasureSealAwardRegionIds();
        MemoryErasureBossAwardPublicationData recoveredObservation =
            restoredPublications.CaptureForRegion(
                OffenseRegionRuntime.SealedZoneRegionId);
        GameplayOutcomeLedgerSaveData recoveredLedger = outcomes.Ledger
            .CaptureGameplayOutcomes();
        MemoryErasureBossAwardFactPreflight.ValidateObservation(
            recoveredObservation,
            recoveredLedger);
        OffenseRegionState[] restored = restoredRegions.Regions
            .OrderBy(region => region.regionId, StringComparer.Ordinal)
            .ToArray();

        string expectedBorderOperation =
            MemoryErasureSealBossAwardRules.BuildOperationId(
                OffenseRegionRuntime.BorderTradeRegionId);
        string expectedRivalOperation =
            MemoryErasureSealBossAwardRules.BuildOperationId(
                OffenseRegionRuntime.RivalOutpostRegionId);
        string expectedSealedOperation =
            MemoryErasureSealBossAwardRules.BuildOperationId(
                OffenseRegionRuntime.SealedZoneRegionId);
        GameplayResultKey sealedKey =
            MemoryErasureBossAwardFactMapping.ResultKey(
                recoveredObservation.fact);
        int sealedCanonicalCount = recoveredLedger.outbox.Count(row =>
                Matches(row, sealedKey))
            + recoveredLedger.exactOutcomes.Count(row =>
                Matches(row, sealedKey))
            + recoveredLedger.tombstones.Count(row =>
                Matches(row, sealedKey));
        bool valid = first.Status == MemoryErasureSealBossAwardStatus.Awarded
            && first.OperationId == expectedBorderOperation
            && duplicate.Status
                == MemoryErasureSealBossAwardStatus.AlreadyAwarded
            && duplicate.OperationId == expectedBorderOperation
            && secondRegion.Status
                == MemoryErasureSealBossAwardStatus.Awarded
            && secondRegion.OperationId == expectedRivalOperation
            && interrupted.Status == MemoryErasureSealBossAwardStatus.Failed
            && interrupted.OperationId == expectedSealedOperation
            && afterRecoveryDuplicate.Status
                == MemoryErasureSealBossAwardStatus.AlreadyAwarded
            && afterRecoveryDuplicate.OperationId == expectedSealedOperation
            && pending.Count == 0
            && restored.Length == 3
            && restored.All(region =>
                region.memoryErasureSealAwardPublished)
            && recoveredObservation != null
            && recoveredObservation.delivered
            && recoveredObservation.fact.operationId
                == expectedSealedOperation
            && recoveredObservation.fact.regionDisplayName
                == pendingObservation.fact.regionDisplayName
            && recoveredObservation.fact.absoluteDay
                == pendingObservation.fact.absoluteDay
            && recoveredObservation.fact.physicalCommitId
                == pendingObservation.fact.physicalCommitId
            && physical.Gateway.SpawnCallCount
                == physicalSpawnCallsBeforeRecovery
            && physical.Gateway.GetAllStacks().All(stack =>
                !ProductionOutputCommitComponentCodec.Matches(
                    stack.Components,
                    recoveredObservation.fact.physicalCommitId))
            && sealedCanonicalCount == 1;
        if (!valid)
        {
            throw new InvalidOperationException(
                "Region-first memory-erasure seal publication was not "
                + "idempotent across committed ACK loss, strict save restore, "
                + "a consumed physical seal, and exact fact replay.");
        }

        VerifyMemoryErasureSealBossAwardPhysicalRetryFreeze();
        VerifyMemoryErasureSealBossAwardMissingObserverRecovery();
        return true;
    }

    private static void VerifyMemoryErasureSealBossAwardPhysicalRetryFreeze()
    {
        var firstCalendar = new EditorFixedGameCalendar(day: 31, hour: 2);
        using EvolutionGameplayOutcomeEditorFixture outcomes = new(
            "run:memory-erasure-boss-award-physical-retry",
            calendar: firstCalendar);
        OffenseRegionRuntime regions = new OffenseRegionRuntime();
        MemorySealPhysicalFixture physical = new MemorySealPhysicalFixture();
        physical.Gateway.RejectNextSpawn = true;
        MemoryErasureSealBossAwardService firstService = new(
            regions,
            BatchACoreSessionSaveDebugScenarios.DefaultInterfaceProxy
                .Create<IOffenseWorldSimulation>(),
            physical.Source,
            MemorySealDropZoneFixture.Instance,
            outcomes.BossAwards,
            firstCalendar);

        MemoryErasureSealBossAwardResult rejected = AwardRegionBoss(
            firstService,
            OffenseRegionRuntime.BorderTradeRegionId,
            "memory-seal-physical-rejected");
        MemoryErasureBossAwardPublicationData draft = outcomes.BossAwards
            .CaptureForRegion(OffenseRegionRuntime.BorderTradeRegionId);
        Require(rejected.Status == MemoryErasureSealBossAwardStatus.Failed
                && draft != null
                && string.IsNullOrEmpty(draft.fact.physicalCommitId)
                && string.IsNullOrEmpty(draft.publicationKey)
                && !draft.delivered
                && physical.Gateway.SpawnAttemptCount == 1
                && physical.Gateway.SpawnCallCount == 0
                && physical.Gateway.GetAllStacks().Count == 0
                && outcomes.Ledger.GetDiagnostics().KnownResultKeyCount == 0
                && !regions.Regions.Single(region => region.regionId
                    == OffenseRegionRuntime.BorderTradeRegionId)
                    .memoryErasureSealAwardPublished,
            "Rejected boss-award physical publication changed item, ledger, or region authority.");

        const string RetryName = "재시도 시점 변경 교역권";
        regions.Regions.Single(region => region.regionId
            == OffenseRegionRuntime.BorderTradeRegionId).displayName = RetryName;
        firstCalendar.SetDateTime(day: 32, hour: 7);
        var retryCalendar = firstCalendar;
        MemoryErasureSealBossAwardService retryService = new(
            regions,
            BatchACoreSessionSaveDebugScenarios.DefaultInterfaceProxy
                .Create<IOffenseWorldSimulation>(),
            physical.Source,
            MemorySealDropZoneFixture.Instance,
            outcomes.BossAwards,
            retryCalendar);
        MemoryErasureSealBossAwardResult retried = AwardRegionBoss(
            retryService,
            OffenseRegionRuntime.BorderTradeRegionId,
            "memory-seal-physical-retry");
        MemoryErasureBossAwardPublicationData completed = outcomes.BossAwards
            .CaptureForRegion(OffenseRegionRuntime.BorderTradeRegionId);
        Require(completed != null,
            "Boss-award physical retry did not retain its completed observation.");
        GameplayOutcomeLedgerSaveData ledger = outcomes.Ledger
            .CaptureGameplayOutcomes();
        MemoryErasureBossAwardFactPreflight.ValidateObservation(
            completed,
            ledger);
        GameplayResultKey key = MemoryErasureBossAwardFactMapping.ResultKey(
            completed.fact);
        int canonicalCount = ledger.outbox.Count(row => Matches(row, key))
            + ledger.exactOutcomes.Count(row => Matches(row, key))
            + ledger.tombstones.Count(row => Matches(row, key));
        WorldItemStackSnapshot seal = physical.Gateway.GetAllStacks().Single();
        Require(retried.Status == MemoryErasureSealBossAwardStatus.Awarded
                && completed != null
                && completed.delivered
                && completed.fact.regionDisplayName == RetryName
                && completed.fact.absoluteDay == retryCalendar.Day
                && physical.Gateway.SpawnAttemptCount == 2
                && physical.Gateway.SpawnCallCount == 1
                && seal.ItemId == MemoryErasureSealItemRules.ItemId
                && seal.Quantity == MemoryErasureSealItemRules.UseQuantity
                && canonicalCount == 1
                && regions.Regions.Single(region => region.regionId
                    == OffenseRegionRuntime.BorderTradeRegionId)
                    .memoryErasureSealAwardPublished,
            "Boss-award physical retry did not freeze its successful occurrence name/day exactly once: "
                + retried.Status + " / " + retried.Detail);
    }

    private static void VerifyMemoryErasureSealBossAwardMissingObserverRecovery()
    {
        using EvolutionGameplayOutcomeEditorFixture outcomes = new(
            "run:memory-erasure-boss-award-missing-observer",
            calendar: new EditorFixedGameCalendar(day: 23, hour: 4));
        using var facts = new DomainFactDispatcher();
        var publications = new MemoryErasureBossAwardPublicationRuntime(
            facts,
            facts);
        OffenseRegionRuntime regions = new OffenseRegionRuntime();
        MemorySealPhysicalFixture physical = new MemorySealPhysicalFixture();
        MemoryErasureSealBossAwardService service = new(
            regions,
            BatchACoreSessionSaveDebugScenarios.DefaultInterfaceProxy
                .Create<IOffenseWorldSimulation>(),
            physical.Source,
            MemorySealDropZoneFixture.Instance,
            publications,
            new EditorFixedGameCalendar(day: 23, hour: 4));

        MemoryErasureSealBossAwardResult missing = AwardRegionBoss(
            service,
            OffenseRegionRuntime.BorderTradeRegionId,
            "memory-seal-missing-observer");
        MemoryErasureBossAwardPublicationData pending =
            publications.CaptureForRegion(
                OffenseRegionRuntime.BorderTradeRegionId);
        int physicalCalls = physical.Gateway.SpawnCallCount;
        Require(missing.Status == MemoryErasureSealBossAwardStatus.Failed
                && pending != null
                && !pending.delivered
                && !string.IsNullOrWhiteSpace(
                    pending.fact.physicalCommitId)
                && string.IsNullOrEmpty(pending.publicationKey)
                && !regions.Regions.Single(region => region.regionId
                    == OffenseRegionRuntime.BorderTradeRegionId)
                    .memoryErasureSealAwardPublished,
            "Missing boss-award observer did not stop before canonical observation delivery.");

        using IDisposable binding = MemoryErasureBossAwardFactMapping
            .CreateBinding(
                outcomes.Registry,
                outcomes.Ledger,
                outcomes.Recorder,
                new GameplayDomainFactCommitGroup(
                    outcomes.Recorder,
                    outcomes.Ledger))
            .Connect(facts);
        Require(service.TryReconcilePendingAwards(out string recoveryFailure),
            "Boss-award observer rebind did not recover the saved physical receipt: "
                + recoveryFailure);
        MemoryErasureBossAwardPublicationData delivered =
            publications.CaptureForRegion(
                OffenseRegionRuntime.BorderTradeRegionId);
        Require(delivered != null
                && delivered.delivered
                && physical.Gateway.SpawnCallCount == physicalCalls
                && regions.CapturePendingMemoryErasureSealAwardRegionIds()
                    .Count == 0
                && outcomes.Ledger.GetDiagnostics().KnownResultKeyCount == 1,
            "Boss-award observer rebind repeated physical publication or lost its canonical result.");
    }

    private static bool Matches(
        GameplayOutcomeSnapshot row,
        GameplayResultKey key) => row != null
        && row.producerId == key.ProducerId
        && row.operationId == key.OperationId.Value
        && row.commitRevision == key.CommitRevision
        && row.localResultIndex == key.LocalResultIndex;

    private static bool Matches(
        GameplayOutcomeTombstoneSnapshot row,
        GameplayResultKey key) => row != null
        && row.producerId == key.ProducerId
        && row.operationId == key.OperationId.Value
        && row.commitRevision == key.CommitRevision
        && row.localResultIndex == key.LocalResultIndex;

    private static MemoryErasureSealBossAwardResult AwardRegionBoss(
        IMemoryErasureSealBossAwardService service,
        string regionId,
        string expeditionId)
    {
        OffenseTargetDefinition target = new OffenseTargetDefinition
        {
            id = "target:" + expeditionId,
            title = expeditionId,
            regionId = regionId,
            durationSeconds = 1f,
            requiredMembers = 1
        };
        OffenseExpeditionRun expedition = new OffenseExpeditionRun(
            expeditionId,
            target,
            Array.Empty<CharacterActor>(),
            0f);
        OffenseRouteNode boss = expedition.Route.Nodes.Single(node =>
            node.IsBoss);
        return service.TryAwardForVictory(
            expedition,
            boss,
            worldObjectiveBattle: false);
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

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    private static bool VerifyMoneyStockAndStateRewards()
    {
        using ScenarioContext context = new ScenarioContext(100);
        int warehouseFoodBefore = context.Warehouse.Inventory.GetStock(StockCategory.Food);
        IReadOnlyList<OffenseRewardGrantResult> results = CreateGrantService().GrantRewards(
            new[]
            {
                Reward(new OffenseMoneyRewardSpec(), "약탈금", 80),
                Reward(new OffenseStockRewardSpec(StockCategory.Food), "무기처럼 보이는 이름", 40),
                Reward(new OffenseRecruitCandidateRewardSpec(), "직원 후보", 1),
                Reward(new OffensePrisonerRewardSpec(), "포로", 1),
                Reward(new OffenseSpecialMonsterRewardSpec(), "특수 동물", 1)
            },
            context.CreateRewardContext());
        int warehouseFoodDelta = context.Warehouse.Inventory.GetStock(StockCategory.Food) - warehouseFoodBefore;
        int physicalFoodDelta = context.RewardState.StockGrantedByCategory
            .TryGetValue(StockCategory.Food, out int grantedFood)
                ? grantedFood
                : 0;
        int physicalLootDelta = context.RewardState.MoneyEarned;
        OffenseRewardGrantResult stock = results.SingleOrDefault(value =>
            value != null && value.category == OffenseRewardCategory.Stock);
        OffenseStockRewardReceiptData stockSnapshot = stock?.stockSupply?.Snapshot();
        OffenseRewardPhysicalItemGrant stockItem = stock?.physicalItems
            ?.SingleOrDefault();

        bool valid = results.Count == 5
            && results.All((result) => result.success)
            && context.GameSessionState.holdingMoney.Value == 100
            && warehouseFoodDelta == 0
            && physicalFoodDelta == 40
            && physicalLootDelta == 80
            && context.RewardState.MoneyEarned == 80
            && context.ReturnArrivals.PrisonerCount == 1
            && context.ReturnArrivals.WildlifeCount == 1
            && stockSnapshot != null
            && stockSnapshot.category == StockCategory.Food
            && stockSnapshot.requestedAmount == 40
            && stockSnapshot.deliveredAmount == physicalFoodDelta
            && stockSnapshot.success
            && string.Equals(
                stockSnapshot.itemId,
                "fixture:food",
                StringComparison.Ordinal)
            && string.Equals(
                stockSnapshot.sourceLabel,
                "무기처럼 보이는 이름",
                StringComparison.Ordinal)
            && string.IsNullOrEmpty(stockSnapshot.reason)
            && stockItem != null
            && string.Equals(
                stockItem.itemId,
                stockSnapshot.itemId,
                StringComparison.Ordinal)
            && stockItem.quantity == stockSnapshot.deliveredAmount;
        if (!valid)
        {
            throw new InvalidOperationException(
                $"Reward diagnostic: results={results.Count}, " +
                $"success={results.Count(result => result.success)}, " +
                $"money={context.GameSessionState.holdingMoney.Value}, " +
                $"warehouseFoodDelta={warehouseFoodDelta}, physicalFoodDelta={physicalFoodDelta}, " +
                $"physicalLootDelta={physicalLootDelta}, " +
                $"moneyState={context.RewardState.MoneyEarned}, " +
                $"prisoners={context.ReturnArrivals.PrisonerCount}, " +
                $"wildlife={context.ReturnArrivals.WildlifeCount}, " +
                $"stockReceipt={JsonUtility.ToJson(stockSnapshot)}, " +
                $"stockItem={stockItem?.itemId ?? "none"}x{stockItem?.quantity ?? 0}");
        }

        return valid;
    }

    private static bool VerifyRareFacilityAndBlueprintRewards()
    {
        using ScenarioContext context = new ScenarioContext(0);
        IReadOnlyList<OffenseRewardGrantResult> results = CreateGrantService().GrantRewards(
            new[]
            {
                Reward(new OffenseRareFacilityRewardSpec(), "희귀 시설", 1),
                Reward(new OffenseSpecialBlueprintRewardSpec(), "특수 설계도", 1)
            },
            context.CreateRewardContext());

        return results.Count == 2
            && results.All((result) => result.success)
            && context.RewardState.RareFacilityBuildingIds.Count == 1
            && context.RewardState.RareFacilityBuildingIds
                .All(context.ResearchState.IsBuildingUnlocked)
            && context.RewardState.AcquiredBlueprintIds.Count == 1
            && context.ResearchState.Tasks.Count == 0;
    }

    private static bool VerifyOpenRewardHandlerExtension()
    {
        IOffenseRewardGrantService service = new OffenseRewardGrantService(
            new OffenseRewardSelector(new EditorOffenseRewardCatalog()),
            OffenseRewardGrantHandlers.CreateDefaults(null, null)
                .Concat(new IOffenseRewardGrantHandler[] { new TestRewardHandler() }));
        IReadOnlyList<OffenseRewardGrantResult> results = service.GrantRewards(
            new[] { new OffenseRewardPreview("custom", 3, new TestRewardSpec()) },
            new OffenseRewardContext());

        return results.Count == 1
            && results[0].success
            && results[0].grantedAmount == 3
            && results[0].detail == "custom handler";
    }

    private static bool VerifySpecificStrategyBlueprintReward()
    {
        using ScenarioContext context = new ScenarioContext(0);
        IReadOnlyList<OffenseTargetDefinition> targets =
            OffenseEditorTestDependencies.CreateCampaignCatalog().Targets;
        OffenseRewardPreview[] strategyRewards = new[]
        {
            "merchant_road",
            "old_armory",
            "mana_ruins"
        }
            .Select(targetId => targets.First(target => target.id == targetId))
            .SelectMany(target => target.rewards)
            .Where(reward => reward?.GrantSpec is OffenseSpecificBlueprintRewardSpec)
            .ToArray();
        IReadOnlyList<OffenseRewardGrantResult> results = CreateGrantService().GrantRewards(
            strategyRewards,
            context.CreateRewardContext());

        return results.Count == 3
            && results.All(result => result.success)
            && context.RewardState.AcquiredBlueprintIds.Count == 3
            && context.RewardState.AcquiredBlueprintIds.Contains(OffenseStrategyBlueprintIds.CommerceLogistics)
            && context.RewardState.AcquiredBlueprintIds.Contains(OffenseStrategyBlueprintIds.FortressDefense)
            && context.RewardState.AcquiredBlueprintIds.Contains(OffenseStrategyBlueprintIds.ArcaneResearch)
            && context.ResearchState.Tasks.Count == 0;
    }

    private static bool VerifyDefenseBlueprintUnlockCapability()
    {
        BuildingSO defenseBuilding = ScriptableObject.CreateInstance<BuildingSO>();
        FacilityBlueprintSO blueprint = ScriptableObject.CreateInstance<FacilityBlueprintSO>();
        try
        {
            defenseBuilding.id = 91234;
            defenseBuilding.Defense = new DefenseFacilityData
            {
                enabled = true,
                concept = DefenseAttackConcept.Physical
            };
            blueprint.unlocks.Add(new TestBuildingUnlock(defenseBuilding.id));

            return new OffenseDefenseBlueprintRewardSpec().IsEligible(
                blueprint,
                new[] { defenseBuilding });
        }
        finally
        {
            Object.DestroyImmediate(defenseBuilding);
            Object.DestroyImmediate(blueprint);
        }
    }

    private static bool VerifyExpeditionCompletionGrantsRewards()
    {
        using ExpeditionRewardScenario scenario = new ExpeditionRewardScenario();
        using CountingRewardGrantedListener rewardEvents =
            new CountingRewardGrantedListener(scenario.GameEvents);
        CharacterActor worker = scenario.CreateCharacter("RewardWorker", CharacterType.NPC, CharacterRole.Regular, 100);
        CharacterActor[] party =
        {
            worker,
            scenario.CreateCharacter("RewardEscortA", CharacterType.NPC, CharacterRole.Regular, 100),
            scenario.CreateCharacter("RewardEscortB", CharacterType.NPC, CharacterRole.Regular, 100),
            scenario.CreateCharacter("RewardEscortC", CharacterType.NPC, CharacterRole.Regular, 100),
            scenario.CreateCharacter("RewardEscortD", CharacterType.NPC, CharacterRole.Regular, 100)
        };
        worker.ApplyDamage(20f, "원정 전 부상");

        int warehouseFoodBefore = scenario.Context.Warehouse.Inventory.GetStock(StockCategory.Food);

        bool started = scenario.Expedition.Runtime.TryStartExpedition(
            "food_farm",
            party.Select(CharacterActor.From).ToArray(),
            out OffenseExpeditionRun expedition,
            out string startMessage);
        bool journeyCompleted = started && CompleteJourney(scenario, expedition);
        if (journeyCompleted)
            scenario.Expedition.FlushResultNotifications();

        OffenseExpeditionResult result = scenario.Expedition.Runtime.ResultHistory.FirstOrDefault();
        bool completed = result != null && !scenario.Battle.HasActiveBattle;
        int warehouseFoodDelta = scenario.Context.Warehouse.Inventory.GetStock(StockCategory.Food) - warehouseFoodBefore;
        int physicalFoodDelta = scenario.Reward.Runtime.State.StockGrantedByCategory
            .TryGetValue(StockCategory.Food, out int grantedFood)
                ? grantedFood
                : 0;
        int physicalLootDelta = scenario.Reward.Runtime.State.MoneyEarned;
        OffenseStrategicPressureSnapshot pressure =
            scenario.Reward.Regions.GetFactionPressure(OffenseRegionRuntime.HumanFactionId);

        bool valid = started
            && journeyCompleted
            && completed
            && result != null
            && result.success
            && result.grantedRewards.Count == 3
            && result.grantedRewards.All((reward) => reward.success)
            && rewardEvents.Count == 1
            && string.Equals(
                JsonUtility.ToJson(OffenseSaveService.CaptureResult(
                    rewardEvents.LastEvent.expeditionResult)),
                JsonUtility.ToJson(OffenseSaveService.CaptureResult(result)),
                StringComparison.Ordinal)
            && rewardEvents.LastEvent.grantResults.Count == result.grantedRewards.Count
            && scenario.Context.GameSessionState.holdingMoney.Value == 0
            && warehouseFoodDelta == 0
            && physicalFoodDelta == 40
            && physicalLootDelta == 80
            && scenario.Reward.Runtime.State.MoneyEarned == 80
            && Mathf.Approximately(pressure.Logistics, 15f)
            && worker.CurrentHealth < worker.MaxHealth
            && (worker.LogComponent == null
                || !worker.LogComponent.ActivityEntries.Any(activity =>
                    activity.ActionId == "offense:victory-recovery"));
        if (!valid)
        {
            throw new InvalidOperationException(
                $"Expedition reward diagnostic: started={started}, startMessage={startMessage}, journeyCompleted={journeyCompleted}, " +
                $"active={scenario.Expedition.Runtime.ActiveExpeditions.Count}, battle={scenario.Battle.HasActiveBattle}, " +
                $"result={(result == null ? "null" : result.success.ToString())}, grants={result?.grantedRewards.Count ?? -1}, " +
                $"members={result?.members.Count ?? -1}, power={result?.totalPower ?? -1:0.##}/" +
                $"{result?.requiredPower ?? -1:0.##}, danger={result?.danger ?? -1:0.##}, " +
                $"eventCount={rewardEvents.Count}, money={scenario.Context.GameSessionState.holdingMoney.Value}, " +
                $"foodWarehouseDelta={warehouseFoodDelta}, foodPhysicalDelta={physicalFoodDelta}, " +
                $"physicalLootDelta={physicalLootDelta}, " +
                $"logisticsPressure={pressure.Logistics:0.##}, " +
                $"health={worker.CurrentHealth:0.##}/{worker.MaxHealth:0.##}, " +
                $"battleOutcome={scenario.LastBattleOutcome}, " +
                $"battleSummary={scenario.LastBattleSummary}");
        }

        return valid;
    }

    public static bool VerifyPlannedCommandPayloadForwarding()
    {
        using ExpeditionRewardScenario scenario = new ExpeditionRewardScenario(
            includeExpeditionFixture: false);
        CharacterActor unarmedActor = scenario.CreateCharacter(
            "Planned Unarmed",
            CharacterType.NPC,
            CharacterRole.Regular,
            100);
        CharacterActor firearmActor = scenario.CreateCharacter(
            "Planned Firearm",
            CharacterType.NPC,
            CharacterRole.Regular,
            100);
        string firearmActorId = scenario.EquipCharacterWithWeapon(
            firearmActor,
            "weapon:handgonne");
        string unarmedActorId = scenario.EquipCharacterWithWeapon(
            unarmedActor, "weapon:handgonne");
        OffenseTargetDefinition target = OffenseEditorTestDependencies
            .CreateCampaignCatalog()
            .Targets
            .FirstOrDefault(candidate => candidate != null
                && string.Equals(candidate.id, "food_farm", StringComparison.Ordinal));
        if (target == null)
        {
            throw new InvalidOperationException(
                "The authored food_farm target is required for planned-command payload coverage.");
        }

        OffenseExpeditionRun expedition = new OffenseExpeditionRun(
            "reward:planned-command-payload",
            target,
            new[] { unarmedActor, firearmActor },
            totalPower: 1f);
        bool started = scenario.Battle.TryStartBattle(expedition, out string startMessage);
        OffenseBattleSession session = scenario.Battle.Session;
        if (!started || session == null)
        {
            throw new InvalidOperationException(
                "Planned-command payload fixture could not start a public battle session: "
                + startMessage);
        }

        int directorTurn = session.RoundNumber;
        OffenseBattleCombatant unarmedCombatant = session.FindCombatant(unarmedActorId);
        OffenseBattleCombatant firearmCombatant = session.FindCombatant(firearmActorId);
        string previousWeaponId = unarmedCombatant?.Weapon?.InstanceId;
        bool switchAccepted = scenario.Battle.TryExecutePlannedCommand(
            directorTurn,
            unarmedActorId,
            unarmedActorId,
            OffenseBattleActionType.SwitchWeapon,
            "combat:unarmed",
            out OffenseBattleCommandResult switchResult);
        bool switchedToUnarmed = string.Equals(
            unarmedCombatant?.Weapon?.DefinitionId,
            "combat:unarmed",
            StringComparison.Ordinal);
        GameplayOutcomeSnapshot switchOutcome = scenario.OutcomeLedger.GetGlobal(
                OutcomeCursor.FirstPage(32), OutcomeFilter.All).Items
            .Select(value => value.Exact)
            .Single(value => value != null
                && value.outcomeTypeId == "offense.battle-command-resolved"
                && value.participants.Any(participant => participant.entityId == unarmedActorId
                    && participant.roleId == "actor"));
        bool recordedAppliedWeapon = !string.IsNullOrWhiteSpace(previousWeaponId)
            && switchOutcome.participants.Any(participant => participant.entityKindId == "item-definition"
                && participant.entityId == "combat:unarmed" && participant.roleId == "equipment")
            && !switchOutcome.participants.Any(participant => participant.entityId == previousWeaponId
                && participant.roleId == "equipment");

        const CombatFireMode supportedMode = CombatFireMode.Suppressive;
        CombatFireMode? initialFireMode = firearmCombatant?.FireMode;
        bool firearmSupportsMode = firearmCombatant?.Weapon?.IsRanged == true
            && firearmCombatant.Weapon.SupportsSuppressive;
        bool firearmStartsInDifferentMode = initialFireMode.HasValue
            && initialFireMode.Value != supportedMode;
        bool fireModeAccepted = scenario.Battle.TryExecutePlannedCommand(
            directorTurn,
            firearmActorId,
            firearmActorId,
            OffenseBattleActionType.SetFireMode,
            supportedMode.ToString(),
            out OffenseBattleCommandResult fireModeResult);
        bool fireModeChanged = firearmCombatant?.FireMode == supportedMode;

        long commandIdBeforeEmptyAbility = session.LastProcessedCommandId;
        bool emptyAbilityAccepted = scenario.Battle.TryExecutePlannedCommand(
            directorTurn,
            firearmActorId,
            firearmActorId,
            OffenseBattleActionType.Ability,
            string.Empty,
            out OffenseBattleCommandResult emptyAbilityResult);
        bool emptyAbilityDidNotAdvanceCommand =
            session.LastProcessedCommandId == commandIdBeforeEmptyAbility;
        bool emptyAbilityRejectedForMissingId = !emptyAbilityAccepted
            && string.Equals(
                emptyAbilityResult?.Message,
                "Strategic Ability command is missing its source ability ID.",
                StringComparison.Ordinal);

        bool valid = switchAccepted
            && switchedToUnarmed
            && recordedAppliedWeapon
            && firearmSupportsMode
            && firearmStartsInDifferentMode
            && fireModeAccepted
            && fireModeChanged
            && emptyAbilityRejectedForMissingId
            && emptyAbilityDidNotAdvanceCommand;
        if (!valid)
        {
            throw new InvalidOperationException(
                "Planned-command payload diagnostic: "
                + $"turn={directorTurn}/{session.RoundNumber}, "
                + $"switch={switchAccepted}:{switchResult?.Message}, "
                + $"unarmed={unarmedCombatant?.Weapon?.DefinitionId ?? "none"}, "
                + $"firearm={firearmCombatant?.Weapon?.DefinitionId ?? "none"}, "
                + $"supportsSuppressive={firearmSupportsMode}, "
                + $"initialFireMode={initialFireMode?.ToString() ?? "none"}, "
                + $"fireMode={fireModeAccepted}:{fireModeResult?.Message}:"
                + $"{firearmCombatant?.FireMode.ToString() ?? "none"}, "
                + $"emptyAbility={emptyAbilityAccepted}:{emptyAbilityResult?.Message}, "
                + $"command={commandIdBeforeEmptyAbility}->{session.LastProcessedCommandId}");
        }

        return true;
    }

    public static bool VerifyOffenseUltimateAtomicPublicEntry()
    {
        Require(
            CharacterGrowthRules.StableHash("test") < 0
            && CharacterGrowthRules.StableNonNegativeHash("test") >= 0,
            "Stable battle serial normalization did not cover signed hashes.");
        return VerifyOffenseUltimateAtomicPublicEntry(planned: false)
            && VerifyOffenseUltimateAtomicPublicEntry(planned: true);
    }

    private static bool VerifyOffenseUltimateAtomicPublicEntry(bool planned)
    {
        using ExpeditionRewardScenario scenario = new ExpeditionRewardScenario(
            includeExpeditionFixture: false);
        CharacterActor actor = scenario.CreateCharacter(
            "Atomic Ultimate Actor",
            CharacterType.NPC,
            CharacterRole.Regular,
            100);
        string actorId = scenario.GetPersistentId(actor);
        var ultimate = new CharacterSkillInstance
        {
            id = "skill:g13:public-offense-ultimate",
            displayName = "G13 공개 전투 궁극기",
            description = "공개 전투 명령과 원자적으로 확정된다.",
            mechanicalDescription = "적 하나에게 피해를 준다.",
            kind = CharacterSkillKind.Ultimate,
            trigger = CharacterSkillTrigger.ManualCombat,
            target = CharacterSkillTarget.Enemy,
            targetingMode = CharacterSkillTargetingMode.PlayerSelected,
            effectArea = CharacterSkillEffectArea.Single,
            areaSize = 1,
            ultimateDomain = CharacterUltimateDomain.Offense,
            modules = new List<CharacterSkillModuleSelection>
            {
                new()
                {
                    moduleId = "damage",
                    variantId = "standard"
                }
            }
        };
        actor.Progression.GrowthState.ultimate = ultimate.Clone();

        OffenseTargetDefinition target = OffenseEditorTestDependencies
            .CreateCampaignCatalog()
            .Targets
            .First(candidate => candidate != null
                && string.Equals(candidate.id, "food_farm", StringComparison.Ordinal));
        var expedition = new OffenseExpeditionRun(
            "reward:g13-public-offense-ultimate:"
                + (planned ? "planned" : "player"),
            target,
            new[] { actor },
            totalPower: 1f);
        Require(
            scenario.Battle.TryStartBattle(expedition, out string startFailure),
            "Public offense ultimate battle did not start: " + startFailure);
        OffenseBattleSession session = scenario.Battle.Session;
        if (!planned)
        {
            scenario.Battle.AdvanceToPlayerDecision();
            Require(
                session?.CurrentActor != null
                && session.CurrentActor.Team == OffenseBattleTeam.Allies
                && string.Equals(
                    session.CurrentActor.PersistentId,
                    actorId,
                    StringComparison.Ordinal),
                "Public offense ultimate did not reach the owning ally turn.");
        }
        OffenseBattleCombatant enemy = session.Combatants.First(value =>
            value.Team == OffenseBattleTeam.Enemies && !value.IsDead);
        float healthBefore = enemy.CurrentHealth;
        bool accepted = planned
            ? scenario.Battle.TryExecutePlannedCommand(
                session.RoundNumber,
                actorId,
                enemy.PersistentId,
                OffenseBattleActionType.Ability,
                ultimate.id,
                out OffenseBattleCommandResult result)
            : scenario.Battle.TryIssuePlayerCommand(
                OffenseBattleActionType.Ability,
                enemy.PersistentId,
                ultimate.id,
                out result);
        Require(
            accepted && result.Accepted,
            "Public offense ultimate command failed: " + result?.Message);

        MigratedProducerOutcomeDefinition battleDefinition =
            MigratedProducerOutcomeCatalog.Get(
                MigratedProducerOutcomeKind.OffenseBattleCommandResult);
        GameplayOutcomeSnapshot[] exact = scenario.OutcomeLedger.GetGlobal(
                OutcomeCursor.FirstPage(16),
                OutcomeFilter.All)
            .Items
            .Select(value => value.Exact)
            .Where(value => value != null)
            .ToArray();
        GameplayOutcomeSnapshot skillOutcome = exact.Single(value =>
            string.Equals(
                value.producerId,
                CharacterSkillActivationOutcomeIds.ProducerId,
                StringComparison.Ordinal));
        string recordedChild = skillOutcome.facts.Single(value =>
            string.Equals(
                value.factId,
                CharacterSkillActivationOutcomeIds.ChildResultKeys.Value,
                StringComparison.Ordinal)).value;
        GameplayOutcomeSnapshot battleOutcome = exact.Single(value =>
            string.Equals(
                value.producerId,
                battleDefinition.ProducerId,
                StringComparison.Ordinal)
            && string.Equals(
                new GameplayResultKey(
                    value.producerId,
                    new GameplayOperationId(value.operationId),
                    value.commitRevision,
                    value.localResultIndex).ToString(),
                recordedChild,
                StringComparison.Ordinal));
        int battleSerial = CharacterGrowthRules.StableNonNegativeHash(
            session.BattleId);
        Require(
            enemy.CurrentHealth < healthBefore
            && battleOutcome != null
            && actor.Progression.GrowthState.useLimits.offenseBattleSerial
                == battleSerial,
            "Public offense ultimate did not atomically preserve the battle "
            + "child, skill outcome, effect and use limit for "
            + (planned ? "planned" : "player") + " execution.");
        return true;
    }

    private static bool CompleteJourney(ExpeditionRewardScenario scenario, OffenseExpeditionRun expedition)
    {
        int safety = 0;
        while (scenario.Expedition.Runtime.ActiveExpeditions.Count > 0 && safety++ < 100)
        {
            switch (expedition.Phase)
            {
                case OffenseExpeditionPhase.ChoosingRoute:
                    OffenseRouteNode next = expedition.GetAvailableRouteNodes().FirstOrDefault();
                    if (next == null
                        || !scenario.Expedition.Runtime.TryChooseRouteNode(expedition.ExpeditionId, next.Id, out _))
                    {
                        return false;
                    }
                    break;

                case OffenseExpeditionPhase.ResolvingNode:
                    if (!scenario.Expedition.Runtime.TryResolveCurrentNode(
                        expedition.ExpeditionId,
                        useSupply: false,
                        out _,
                        out _))
                    {
                        return false;
                    }
                    break;

                case OffenseExpeditionPhase.InBattle:
                    if (!CompleteCurrentBattle(scenario.Battle)) return false;
                    break;

                case OffenseExpeditionPhase.Completed:
                case OffenseExpeditionPhase.Defeated:
                case OffenseExpeditionPhase.Retreated:
                    return scenario.Expedition.Runtime.ActiveExpeditions.Count == 0;
            }
        }

        return scenario.Expedition.Runtime.ActiveExpeditions.Count == 0;
    }

    private static bool CompleteCurrentBattle(OffenseBattleRuntime battle)
    {
        int safety = 0;
        while (battle.HasActiveBattle && safety++ < 500)
        {
            if (battle.Session.CurrentActor?.Team == OffenseBattleTeam.Enemies)
            {
                battle.AdvanceToPlayerDecision();
                int enemySafety = 0;
                while (battle.HasActiveBattle
                    && battle.Session.CurrentActor?.Team == OffenseBattleTeam.Enemies
                    && enemySafety++ < 20)
                {
                    OffenseBattleCombatant stuckEnemy = battle.Session.CurrentActor;
                    OffenseBattleCommand forcedGuard = new(
                        battle.Session.LastProcessedCommandId + 1,
                        stuckEnemy.PersistentId,
                        OffenseBattleActionType.Guard,
                        stuckEnemy.PersistentId,
                        string.Empty);
                    if (!battle.TryExecuteCommand(
                            forcedGuard,
                            out OffenseBattleCommandResult enemyResult))
                    {
                        throw new InvalidOperationException(
                            "Reward fixture could not advance an enemy turn: "
                            + enemyResult.Message);
                    }
                }
                if (!battle.HasActiveBattle)
                {
                    break;
                }
            }
            OffenseBattleCombatant currentActor = battle.Session.CurrentActor;
            OffenseBattleCombatant[] livingEnemies = battle.Session.Combatants
                .Where(combatant => combatant.Team == OffenseBattleTeam.Enemies
                    && !combatant.IsDead
                    && !combatant.IsDowned)
                .ToArray();
            if (livingEnemies.Length == 0
                || currentActor?.Team != OffenseBattleTeam.Allies)
            {
                throw new InvalidOperationException(
                    "Reward battle did not reach an allied decision. "
                    + $"current={battle.Session.CurrentActor?.PersistentId ?? "none"}, "
                    + $"team={battle.Session.CurrentActor?.Team.ToString() ?? "none"}, "
                    + $"enemyAlive={livingEnemies.Length > 0}.");
            }

            CombatWeaponSnapshot weapon = currentActor.Weapon
                ?? CombatWeaponSnapshot.CreateUnarmed();
            bool hasForwardEnemy = livingEnemies.Any(candidate =>
                candidate.Formation != OffenseFormationSlot.Rear);
            OffenseBattleCombatant target = livingEnemies
                .Where(candidate =>
                {
                    int distance = OffenseBattleSessionRules.GetFormationDistance(
                        currentActor,
                        candidate);
                    return distance <= weapon.MaximumRange
                        && weapon.GetAccuracyMultiplier(
                            CombatRangeRules.GetBand(distance)) > 0f
                        && (weapon.IsRanged
                            || !hasForwardEnemy
                            || candidate.Formation != OffenseFormationSlot.Rear);
                })
                .OrderBy(candidate => candidate.HealthRatio)
                .ThenBy(candidate => candidate.PersistentId, StringComparer.Ordinal)
                .FirstOrDefault();
            OffenseBattleActionType actionType;
            string targetId;
            if (target != null)
            {
                actionType = OffenseBattleActionType.BasicAttack;
                targetId = target.PersistentId;
            }
            else if (currentActor.Formation != OffenseFormationSlot.Front)
            {
                actionType = OffenseBattleActionType.Advance;
                targetId = currentActor.PersistentId;
            }
            else
            {
                actionType = OffenseBattleActionType.Guard;
                targetId = currentActor.PersistentId;
            }
            OffenseBattleCommand playerCommand = new(
                battle.Session.LastProcessedCommandId + 1,
                currentActor.PersistentId,
                actionType,
                targetId,
                string.Empty);
            if (!battle.TryExecuteCommand(
                    playerCommand,
                    out OffenseBattleCommandResult commandResult))
            {
                throw new InvalidOperationException(
                    "Reward battle could not execute its deterministic command: "
                    + commandResult?.Message);
            }
        }

        if (battle.HasActiveBattle)
        {
            throw new InvalidOperationException(
                "Reward battle exceeded its deterministic safety limit. "
                + $"current={battle.Session?.CurrentActor?.PersistentId ?? "none"}, "
                + $"allies={battle.Session?.Combatants.Count(value => value.Team == OffenseBattleTeam.Allies && !value.IsDead) ?? 0}, "
                + $"enemies={battle.Session?.Combatants.Count(value => value.Team == OffenseBattleTeam.Enemies && !value.IsDead) ?? 0}.");
        }
        return true;
    }

    private static OffenseRewardPreview Reward(
        OffenseRewardGrantSpec grantSpec,
        string label,
        int amount)
    {
        return new OffenseRewardPreview(label, amount, grantSpec);
    }

    private static IOffenseRewardGrantService CreateGrantService()
    {
        IWorldItemStackRuntime itemRuntime =
            BatchACoreSessionSaveDebugScenarios.DefaultInterfaceProxy
                .Create<IWorldItemStackRuntime>();
        IExpeditionRewardItemSink rewardItems =
            new RecordingExpeditionRewardItemSink();
        IReadOnlyList<IOffenseRewardGrantHandler> defaults =
            OffenseRewardGrantHandlers.CreateDefaults(rewardItems);
        return new OffenseRewardGrantService(
            new OffenseRewardSelector(new EditorOffenseRewardCatalog()),
            defaults
                .Where(handler => handler.RewardTypeId != OffenseRewardTypeIds.Stock)
                .Concat(new IOffenseRewardGrantHandler[]
                {
                    new OffenseStockRewardGrantHandler(
                        rewardItems,
                        itemRuntime)
                }));
    }

    private sealed class RecordingExpeditionRewardItemSink :
        IExpeditionRewardItemSink
    {
        public bool SpawnLoot(string itemId, int amount, out int spawned)
        {
            spawned = Mathf.Max(0, amount);
            return !string.IsNullOrWhiteSpace(itemId) && spawned > 0;
        }

        public bool SpawnStock(
            StockCategory category,
            int amount,
            string sourceLabel,
            out string itemId,
            out int spawned)
        {
            itemId = $"fixture:{category.ToString().ToLowerInvariant()}";
            spawned = Mathf.Max(0, amount);
            return spawned > 0;
        }
    }

    private static int GetPhysicalItem(string itemId)
    {
        TryResolvePhysicalItemServices(
            out IWorldItemStackRuntime itemRuntime,
            out _);
        return itemRuntime?.GetAllStacks()
            .Where(stack => stack != null
                && string.Equals(stack.ItemId, itemId, StringComparison.Ordinal))
            .Sum(stack => stack.Quantity) ?? 0;
    }

    private static bool TryResolvePhysicalItemServices(
        out IWorldItemStackRuntime itemRuntime,
        out IWorldDropZoneQuery dropZoneQuery)
    {
        DungeonRuntimeLifetimeScope scope = Object.FindFirstObjectByType<DungeonRuntimeLifetimeScope>(
            FindObjectsInactive.Include);
        if (scope == null || scope.Container == null)
        {
            itemRuntime = null;
            dropZoneQuery = null;
            return false;
        }

        itemRuntime = scope.Container.Resolve<IWorldItemStackRuntime>();
        dropZoneQuery = scope.Container.Resolve<IWorldDropZoneQuery>();
        return itemRuntime != null && dropZoneQuery != null;
    }

    private static GameSessionState CreateGameData(int holdingMoney)
    {
        GameSessionState gameData = new GameSessionState();
        gameData.holdingMoney.Initialize(holdingMoney);
        return gameData;
    }

    private sealed class TestRewardSpec : OffenseRewardGrantSpec
    {
        public override string RewardTypeId => "offense.reward.test";
        public override OffenseRewardCategory Category => OffenseRewardCategory.Money;
    }

    private sealed class TestRewardHandler : IOffenseRewardGrantHandler
    {
        public string RewardTypeId => "offense.reward.test";

        public OffenseRewardGrantResult Grant(
            OffenseRewardPreview reward,
            OffenseRewardContext context,
            IOffenseRewardSelector selector)
        {
            return OffenseRewardGrantResultFactory.Success(reward, reward.amount, "custom handler");
        }
    }

    [Serializable]
    private sealed class TestBuildingUnlock : BlueprintUnlock, IBlueprintBuildingUnlock
    {
        public TestBuildingUnlock(int buildingId)
        {
            BuildingId = buildingId;
        }

        public int BuildingId { get; }
        public override string UnlockTypeId => "blueprint.test-building";
        public override bool IsConfigured => BuildingId >= 0;

        public override BlueprintUnlockRecord Apply(BlueprintUnlockContext context)
        {
            return default;
        }
    }

    private sealed class CountingRewardGrantedListener : IDisposable
    {
        private readonly IDisposable subscription;

        public CountingRewardGrantedListener(DungeonStory.Foundation.IGameEventBus gameEventBus)
        {
            subscription = gameEventBus.Subscribe<OffenseRewardGrantedEvent>(OnTriggerEvent);
        }

        public int Count { get; private set; }
        public OffenseRewardGrantedEvent LastEvent { get; private set; }

        public void OnTriggerEvent(OffenseRewardGrantedEvent eventType)
        {
            Count++;
            LastEvent = eventType;
        }

        public void Dispose()
        {
            subscription.Dispose();
        }
    }

    private sealed class ScenarioContext : IDisposable
    {
        public ScenarioContext(int holdingMoney)
        {
            GameSessionState = CreateGameData(holdingMoney);
            Warehouse = new TestWarehouse(500);
            ShopUnlockState = new FacilityShopUnlockState();
            ResearchState = new BlueprintResearchState();
            RewardState = new OffenseRewardState();
            ReturnArrivals = new TestReturnArrivalRuntime();
        }

        public GameSessionState GameSessionState { get; }
        public TestWarehouse Warehouse { get; }
        public FacilityShopUnlockState ShopUnlockState { get; }
        public BlueprintResearchState ResearchState { get; }
        public OffenseRewardState RewardState { get; }
        public TestReturnArrivalRuntime ReturnArrivals { get; }

        public OffenseRewardContext CreateRewardContext(OffenseTargetDefinition target = null)
        {
            return new OffenseRewardContext
            {
                gameData = GameSessionState,
                warehouses = new[] { Warehouse },
                shopUnlockState = ShopUnlockState,
                researchState = ResearchState,
                rewardState = RewardState,
                returnArrivalRuntime = ReturnArrivals,
                expeditionId = "reward-debug-expedition",
                target = target
            };
        }

        public void Dispose()
        {
            if (GameSessionState != null)
            {
            }
        }
    }

    private sealed class TestWarehouse : IWarehouseFacility
    {
        public TestWarehouse(int capacity)
        {
            Inventory = new WarehouseInventory(
                checked((long)capacity * 1000L),
                StockCategory.General,
                restrictCategory: false);
            Inventory.BindPhysicalStock(
                EmptyStockQuery.Instance,
                PersistentInstanceId,
                CharacterAiEditorTestDependencies.AuthoredGameplay);
        }

        public WarehouseInventory Inventory { get; }
        public BuildingInstanceId PersistentInstanceId =>
            (BuildingInstanceId)"building:test-offense-reward-warehouse";
        public bool HasWarehouseInventory => true;
    }

    private sealed class EmptyStockQuery :
        IStockQuery,
        IWarehousePhysicalMassQueryPort
    {
        public static readonly EmptyStockQuery Instance = new EmptyStockQuery();
        private EmptyStockQuery() { }
        public int PhysicalItemStackVersion => 0;
        public long PhysicalMassAuthorityRevision => 0L;
        public IReadOnlyList<WorldItemStackSnapshot> GetAllStacks() =>
            Array.Empty<WorldItemStackSnapshot>();
        public int GetGlobalQuantity(string itemDefinitionId) => 0;
        public int GetWarehouseQuantity(
            BuildingInstanceId warehouseId,
            string itemDefinitionId) => 0;
        public int GetWarehouseQuantity(
            BuildingInstanceId warehouseId,
            StockCategory category) => 0;
        public int GetWarehouseTotal(BuildingInstanceId warehouseId) => 0;
        public long GetWarehouseStoredMassGrams(
            BuildingInstanceId warehouseId) => 0L;
        public long GetWarehouseStoredMassRevision(
            BuildingInstanceId warehouseId) => 0L;
        public long GetDefinitionUnitMassGrams(string itemDefinitionId) => 1L;
    }

    private sealed class TestReturnArrivalRuntime : IOffenseReturnArrivalRuntime
    {
        public int PrisonerCount { get; private set; }
        public int WildlifeCount { get; private set; }
        public IReadOnlyList<OffenseReturnArrivalState> Arrivals =>
            Array.Empty<OffenseReturnArrivalState>();

        public IReadOnlyList<OffenseExpeditionArrivalReceipt> GetSettlementReceipts(
            string expeditionId) => Array.Empty<OffenseExpeditionArrivalReceipt>();

        public void BeginExpeditionReturn(string expeditionId) { }
        public void RegisterReturningMember(string expeditionId) { }
        public void CompleteReturningMember(string expeditionId) { }
        public void SealExpeditionReturn(string expeditionId) { }
        public void RegisterBattlePrisonerCandidates(
            string expeditionId,
            IEnumerable<EnemyIndividualSaveData> individuals) { }
        public void DiscardBattlePrisonerCandidates(string expeditionId) { }

        public int QueueArrival(
            string expeditionId,
            string targetId,
            OffenseReturnArrivalKind kind,
            int amount)
        {
            int safeAmount = Mathf.Max(0, amount);
            if (kind == OffenseReturnArrivalKind.Prisoner)
            {
                PrisonerCount += safeAmount;
            }
            else
            {
                WildlifeCount += safeAmount;
            }
            return safeAmount;
        }

        public DungeonOffenseReturnArrivalSaveData Capture()
        {
            return new DungeonOffenseReturnArrivalSaveData();
        }

        public OffenseReturnArrivalRestoreCandidate BuildRestoreCandidate(
            DungeonOffenseReturnArrivalSaveData saveData,
            DungeonGameRestoreReport report)
        {
            return null;
        }

        public void PublishRestoreCandidate(
            OffenseReturnArrivalRestoreCandidate candidate)
        {
        }
    }

    private sealed class EditorOffenseRewardCatalog : IOffenseRewardCatalog
    {
        private IReadOnlyCollection<BuildingSO> buildings;
        private IReadOnlyCollection<FacilityBlueprintSO> blueprints;

        public IReadOnlyCollection<BuildingSO> Buildings => buildings ??= LoadAssets<BuildingSO>();
        public IReadOnlyCollection<FacilityBlueprintSO> Blueprints => blueprints ??= LoadAssets<FacilityBlueprintSO>();

        private static IReadOnlyCollection<T> LoadAssets<T>()
            where T : Object
        {
            return AssetDatabase.FindAssets($"t:{typeof(T).Name}")
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<T>)
                .Where((asset) => asset != null)
                .ToArray();
        }
    }

    private sealed class ExpeditionRewardScenario : IDisposable
    {
        private readonly List<Object> objects = new List<Object>();
        private readonly TestCharacterSaveService characterSaveService;
        private readonly ICombatEquipmentRuntime battleEquipment;
        private readonly MigratedProducerOutcomeEditorFixture runOutcomeFixture;
        private readonly RewardCombatPerformanceQuery battlePerformance;
        private readonly DomainFactDispatcher runVariableFacts;
        private readonly DomainFactDispatcher battleFacts;
        private readonly IDisposable battleCommandBinding;
        private readonly IDisposable skillActivationBinding;
        private readonly OffenseBattleCommandPublicationRuntime
            battleCommandPublications;
        private readonly CharacterSkillActivationPublicationRuntime
            skillActivationPublications;
        private readonly CharacterSkillActivationTransaction
            skillActivationTransaction;

        public ScenarioContext Context { get; }
        public WorldMapFixture WorldMap { get; }
        public ExpeditionFixture Expedition { get; }
        public RewardFixture Reward { get; }
        public OffenseBattleRuntime Battle { get; }
        public OffenseBattleOutcome LastBattleOutcome { get; private set; } =
            OffenseBattleOutcome.InProgress;
        public string LastBattleSummary { get; private set; } = string.Empty;
        public DungeonStory.Foundation.IGameEventBus GameEvents { get; }
        public GameplayOutcomeLedger OutcomeLedger => runOutcomeFixture.Ledger;

        public ExpeditionRewardScenario(bool includeExpeditionFixture = true)
        {
            GameEvents = new DungeonStory.Foundation.GameEventBus();
            Context = new ScenarioContext(0);
            WorldMap = new WorldMapFixture(GameEvents);
            Reward = new RewardFixture(Context);
            DungeonSceneRuntimeReferences dungeonRuntimes =
                EditorRuntimeReferenceFixtures.DungeonWithRunVariables;
            DungeonRuntimeAggregateRootStore runRoot = new();
            runOutcomeFixture = new MigratedProducerOutcomeEditorFixture(
                "run:offense-reward-variables",
                runRoot,
                includeCharacterSkillActivation: true);
            runVariableFacts = runOutcomeFixture.CreateRunVariableFacts();
            battleFacts = new DomainFactDispatcher();
            var battleCommits = new GameplayDomainFactCommitGroup(
                runOutcomeFixture.Recorder,
                runOutcomeFixture.Ledger);
            battleCommandBinding = new OffenseBattleCommandFactObserver(
                    runOutcomeFixture.Transaction,
                    battleCommits)
                .Connect(battleFacts);
            skillActivationBinding = CharacterSkillActivationFactMapping
                .CreateBinding(
                    runOutcomeFixture.Registry,
                    runOutcomeFixture.Ledger,
                    runOutcomeFixture.Recorder,
                    battleCommits)
                .Connect(battleFacts);
            battleCommandPublications =
                new OffenseBattleCommandPublicationRuntime(
                    battleFacts,
                    battleFacts);
            skillActivationPublications =
                new CharacterSkillActivationPublicationRuntime(
                    battleFacts,
                    battleFacts);
            skillActivationTransaction =
                new CharacterSkillActivationTransaction(
                    new CharacterSkillActivationEffectExecutor(),
                    CharacterAiEditorTestDependencies.GameCalendar,
                    battleFacts,
                    skillActivationPublications);
            dungeonRuntimes.RunVariables.Construct(
                BatchACoreSessionSaveDebugScenarios.DefaultInterfaceProxy
                    .Create<IOwnerRunDataProvider>(),
                EditorRuntimeReferenceFixtures.Invasion,
                BatchACoreSessionSaveDebugScenarios.DefaultInterfaceProxy
                    .Create<IRunStartVariableSelector>(),
                new DungeonStory.Foundation.RandomStreamProvider(runRoot),
                GameEvents,
                BatchACoreSessionSaveDebugScenarios.DefaultInterfaceProxy
                    .Create<IRunVariableDefinitionCatalog>(),
                BatchACoreSessionSaveDebugScenarios.DefaultInterfaceProxy
                    .Create<IOwnerDoctrineDefinitionCatalog>(),
                runRoot,
                runVariableFacts,
                runVariableFacts);
            characterSaveService = new TestCharacterSaveService();
            battleEquipment = OffenseEditorTestDependencies.CreateCombatEquipmentRuntime();
            battlePerformance = new RewardCombatPerformanceQuery(
                CharacterAiEditorTestDependencies.NeutralPerformance);
            Battle = new OffenseBattleRuntime(
                characterSaveService,
                dungeonRuntimes,
                GameEvents,
                OffenseEditorTestDependencies.CreateCombatResolution(),
                battleEquipment,
                bodyHealthQuery: null,
                bodyHealthCommands: null,
                offenseRegionRuntime: null,
                enemyEncounterFactory:
                    OffenseEditorTestDependencies.CreateEnemyEncounterFactory(
                        battleEquipment),
                enemyTactics: new EnemyTacticalDecisionService(
                    new EnemyCombatContentCatalog(
                        new ResourceGameContentCatalog(
                            new UnityGameContentRootLoader()))),
                returnArrivals: Context.ReturnArrivals,
                performance: battlePerformance);
            Battle.ConstructProficiencyProgression(
                BatchACoreSessionSaveDebugScenarios.DefaultInterfaceProxy
                    .Create<ICharacterProficiencyCommand>(),
                CharacterAiEditorTestDependencies.GameCalendar,
                battleCommandPublications);
            Battle.ConstructSkillActivationRuntime(
                skillActivationTransaction);
            Battle.BattleCompleted += OnBattleCompleted;
            if (includeExpeditionFixture)
            {
                Expedition = new ExpeditionFixture(
                    WorldMap.Runtime,
                    Reward.Runtime,
                    GameEvents,
                    Battle,
                    runOutcomeFixture,
                    characterSaveService);
            }
        }

        public CharacterActor CreateCharacter(
            string name,
            CharacterType type,
            CharacterRole role,
            int statValue)
        {
            _ = role;
            _ = statValue;
            CharacterSO data =
                OffenseEditorTestDependencies.RequireCharacterArchetype("Orc");

            GameObject obj = new GameObject(name);
            objects.Add(obj);
            obj.AddComponent<SpriteRenderer>();
            CharacterActor character = obj.AddComponent<CharacterActor>();
            obj.AddComponent<AbilityMove>();
            obj.AddComponent<AbilityWork>();
            AIBrain brain = obj.AddComponent<AIBrain>();
            brain.availableActions = type == CharacterType.Customer
                ? AiDebugScenarioActionFactory.CreateCustomerActions()
                : AiDebugScenarioActionFactory.CreateStaffActions();
            CharacterAiEditorTestDependencies.Inject(obj);
            obj.GetComponent<CharacterIdentity>().SetPersistentId(
                new GuidPersistentIdGenerator().NewCharacterId());
            character.RefreshAbilityCache();
            character.Initialization(data);
            character.characterType = type;
            character.SetLifecycleState(CharacterLifecycleState.Active);
            character.stats[CharacterCondition.SLEEP] = 100f;
            character.stats[CharacterCondition.MOOD] = 100f;
            string persistentId = characterSaveService.GetOrAssignPersistentId(character);
            CombatEquipmentInstance weapon = battleEquipment.CreateExternalInstance(
                "weapon:javelin",
                CombatEquipmentQuality.Normal);
            string activeFailure = string.Empty;
            if (!battleEquipment.TryAssignToCharacter(
                    persistentId,
                    weapon.instanceId,
                    out string assignFailure)
                || !battleEquipment.TrySetActiveWeapon(
                    persistentId,
                    weapon.instanceId,
                    out activeFailure))
            {
                throw new InvalidOperationException(
                    "Reward fixture could not equip its expedition member: "
                    + (string.IsNullOrWhiteSpace(assignFailure)
                        ? activeFailure
                        : assignFailure));
            }
            return character;
        }

        public string GetPersistentId(CharacterActor character)
        {
            return characterSaveService.GetOrAssignPersistentId(character);
        }

        public int BattlePerformanceEvaluationCount =>
            battlePerformance.EvaluationCount;

        public void RejectBattlePerformanceEvaluation()
        {
            battlePerformance.ResetEvaluationCount();
            battlePerformance.ThrowOnEvaluate = true;
        }

        public string EquipCharacterWithWeapon(
            CharacterActor character,
            string weaponDefinitionId)
        {
            string persistentId = GetPersistentId(character);
            CombatEquipmentInstance weapon = battleEquipment.CreateExternalInstance(
                weaponDefinitionId,
                CombatEquipmentQuality.Normal);
            string activeFailure = string.Empty;
            if (!battleEquipment.TryAssignToCharacter(
                    persistentId,
                    weapon.instanceId,
                    out string assignFailure)
                || !battleEquipment.TrySetActiveWeapon(
                    persistentId,
                    weapon.instanceId,
                    out activeFailure))
            {
                throw new InvalidOperationException(
                    $"Reward fixture could not equip '{weaponDefinitionId}' for planned-command coverage: "
                    + (string.IsNullOrWhiteSpace(assignFailure)
                        ? activeFailure
                        : assignFailure));
            }

            return persistentId;
        }

        public void Dispose()
        {
            Battle.BattleCompleted -= OnBattleCompleted;
            Expedition?.Dispose();
            Battle.Dispose();
            Reward.Dispose();
            WorldMap.Dispose();
            Context.Dispose();
            skillActivationBinding.Dispose();
            battleCommandBinding.Dispose();
            battleFacts.Dispose();
            runVariableFacts.Dispose();
            foreach (Object obj in objects.Where((item) => item != null))
            {
                Object.DestroyImmediate(obj);
            }
        }

        private void OnBattleCompleted(OffenseBattleSession session)
        {
            LastBattleOutcome = session?.Outcome
                ?? OffenseBattleOutcome.InProgress;
            LastBattleSummary = session == null
                ? string.Empty
                : string.Join(
                    " | ",
                    session.Combatants.Select(combatant =>
                        $"{combatant.PersistentId}:{combatant.Team}:" +
                        $"{combatant.CurrentHealth:0.##}/{combatant.Stats.MaxHealth:0.##}:" +
                        $"{combatant.Formation}"))
                    + " || "
                    + string.Join(" | ", session.Log);
        }
    }

    private sealed class WorldMapFixture : IDisposable
    {
        private readonly GameObject obj;
        private readonly EditorOffenseTruthFactFixture truthFacts;

        public OffenseWorldMapRuntime Runtime { get; }

        public WorldMapFixture(DungeonStory.Foundation.IGameEventBus gameEventBus)
        {
            obj = new GameObject("Offense Reward World Map Fixture");
            Runtime = obj.AddComponent<OffenseWorldMapRuntime>();
            OffenseCampaignRuntime campaign = new OffenseCampaignRuntime();
            truthFacts = new EditorOffenseTruthFactFixture(
                gameEventBus,
                "run:offense-reward-world-map");
            Runtime.Construct(
                gameEventBus,
                externalInfluence: null,
                campaign,
                campaign,
                OffenseEditorTestDependencies.CreateCampaignCatalog(),
                truthFacts.Facts,
                truthFacts.Facts,
                EditorFixedGameCalendar.Instance);
            Runtime.StartWorldMap();
        }

        public void Dispose()
        {
            truthFacts.Dispose();
            Object.DestroyImmediate(obj);
        }
    }

    private sealed class ExpeditionFixture : IDisposable
    {
        private readonly GameObject obj;
        private readonly EditorOffenseExpeditionResultFactFixture resultFacts;
        private readonly EditorOffenseNodeFactFixture nodeFacts;
        private readonly OffenseBattleExperienceObserver battleExperience;
        private readonly OffenseExpeditionActivityObserver expeditionActivity;

        public OffenseExpeditionRuntime Runtime { get; }

        public ExpeditionFixture(
            OffenseWorldMapRuntime worldMap,
            OffenseRewardRuntime rewards,
            DungeonStory.Foundation.IGameEventBus gameEventBus,
            IOffenseBattleRuntime battleRuntime,
            MigratedProducerOutcomeEditorFixture outcomes,
            ICharacterLifetimeQuery characters)
        {
            _ = outcomes ?? throw new ArgumentNullException(nameof(outcomes));
            _ = characters ?? throw new ArgumentNullException(nameof(characters));
            resultFacts = new EditorOffenseExpeditionResultFactFixture(
                gameEventBus,
                outcomes.Transaction,
                outcomes.Recorder,
                outcomes.Ledger);
            nodeFacts = new EditorOffenseNodeFactFixture(
                gameEventBus,
                outcomes.Transaction,
                outcomes.Recorder,
                outcomes.Ledger,
                characters: characters);
            battleExperience = new OffenseBattleExperienceObserver(gameEventBus, characters);
            expeditionActivity = new OffenseExpeditionActivityObserver(gameEventBus);
            battleExperience.Initialize();
            expeditionActivity.Initialize();
            obj = new GameObject("Offense Reward Expedition Fixture");
            Runtime = obj.AddComponent<OffenseExpeditionRuntime>();
            MetaProgressionRuntime metaProgression =
                obj.AddComponent<MetaProgressionRuntime>();
            BlueprintResearchRuntime expeditionResearch =
                obj.AddComponent<BlueprintResearchRuntime>();
            expeditionResearch.State.Projects.Complete(
                new ResearchProjectId(
                    OffenseExpeditionAccessRules.RequiredResearchId));
            metaProgression.Construct(
                new EmptyMetaRunResultBuilder(),
                new EmptyMetaRuntimeApplicationPort(),
                new DungeonStory.Foundation.UnityGameClock(),
                new AuthoredGameplayCatalog(
                    new ResourceGameContentCatalog(new UnityGameContentRootLoader())),
                new DungeonRuntimeAggregateRootStore());
            OffenseSceneRuntimeReferences offenseRuntimes =
                new OffenseSceneRuntimeReferences(
                    worldMap,
                    rewards,
                    Runtime,
                    null,
                    null);
            OffenseExpeditionResultFinalizer finalizer =
                new OffenseExpeditionResultFinalizer(
                    offenseRuntimes,
                    new ProgressionSceneRuntimeReferences(
                        null,
                        null,
                        metaProgression),
                    worldMap);
            finalizer.ConstructFactPublication(
                CharacterAiEditorTestDependencies.GameCalendar,
                resultFacts.Publications);
            IOffenseWorldSimulation strategicWorld =
                BatchACoreSessionSaveDebugScenarios.DefaultInterfaceProxy
                    .Create<IOffenseWorldSimulation>();
            IOffenseTravelRuntime strategicTravel =
                BatchACoreSessionSaveDebugScenarios.DefaultInterfaceProxy
                    .Create<IOffenseTravelRuntime>();
            IOffenseReturnSafetyRuntime returnSafety =
                BatchACoreSessionSaveDebugScenarios.DefaultInterfaceProxy
                    .Create<IOffenseReturnSafetyRuntime>();
            IOffenseFieldMobilityService fieldMobility =
                BatchACoreSessionSaveDebugScenarios.DefaultInterfaceProxy
                    .Create<IOffenseFieldMobilityService>();
            IOffenseFieldMedicalRuntime fieldMedical =
                BatchACoreSessionSaveDebugScenarios.DefaultInterfaceProxy
                    .Create<IOffenseFieldMedicalRuntime>();
            IOffenseExpeditionBattleCompletionHandler battleCompletion =
                new OffenseExpeditionBattleCompletionHandler(
                    battleRuntime,
                    BatchACoreSessionSaveDebugScenarios.DefaultInterfaceProxy
                        .Create<IOffenseBattleDirector>(),
                    strategicTravel,
                    strategicWorld,
                    returnSafety,
                    fieldMobility,
                    gameEventBus,
                    OffenseEditorTestDependencies.CreateCombatEquipmentRuntime(),
                    NonAwardingMemoryErasureSealFixture.Instance);
            Runtime.Construct(
                new EmptyExpeditionMemberQuery(),
                offenseRuntimes,
                new EmptyPanelService(),
                battleRuntime ?? throw new ArgumentNullException(nameof(battleRuntime)),
                finalizer,
                new OffenseExpeditionReturnCoordinator(
                    NoOpOffenseExpeditionReturnPort.Instance,
                    finalizer,
                    gameEventBus,
                    CharacterAiEditorTestDependencies.NeutralPerformance),
                BatchACoreSessionSaveDebugScenarios.DefaultInterfaceProxy
                    .Create<IOffensePreparationService>(),
                OffenseEditorTestDependencies.CreateCombatEquipmentRuntime(),
                gameEventBus,
                strategicWorld,
                strategicTravel,
                BatchACoreSessionSaveDebugScenarios.DefaultInterfaceProxy
                    .Create<IOffenseDecisionRuntime>(),
                BatchACoreSessionSaveDebugScenarios.DefaultInterfaceProxy
                    .Create<IOffenseDecisionEffectExecutor>(),
                returnSafety,
                LegacyWorldMapStrategicTargetFixture.Instance,
                BatchACoreSessionSaveDebugScenarios.DefaultInterfaceProxy
                    .Create<IOffenseStrategicBattleLauncher>(),
                BatchACoreSessionSaveDebugScenarios.DefaultInterfaceProxy
                    .Create<IOffenseStrategicTravelEventHandler>(),
                battleCompletion,
                BatchACoreSessionSaveDebugScenarios.DefaultInterfaceProxy
                    .Create<IGameMoneyAccount>(),
                new ProgressionSceneRuntimeReferences(
                    null,
                    expeditionResearch,
                    metaProgression),
                departureService: null,
                equipmentPickupRuntime: null,
                fieldMedical,
                fieldMobility,
                calendar: CharacterAiEditorTestDependencies.GameCalendar,
                nodePublications: nodeFacts.Publications,
                performance: CharacterAiEditorTestDependencies.NeutralPerformance,
                settlementStandings: CharacterAiEditorTestDependencies.SettlementStandings);
        }

        internal void FlushResultNotifications() =>
            resultFacts.FlushNotifications();

        public void Dispose()
        {
            expeditionActivity.Dispose();
            battleExperience.Dispose();
            nodeFacts.Dispose();
            resultFacts.Dispose();
            Object.DestroyImmediate(obj);
        }
    }

    private sealed class LegacyWorldMapStrategicTargetFixture :
        IOffenseStrategicTargetService
    {
        public static readonly LegacyWorldMapStrategicTargetFixture Instance = new();

        private LegacyWorldMapStrategicTargetFixture()
        {
        }

        public bool TryCreateTarget(
            string siteId,
            out OffenseTargetDefinition target,
            out OffenseHexCoord destination)
        {
            target = null;
            destination = default;
            return false;
        }

        public bool TryCreateRescueTarget(
            string targetId,
            out OffenseTargetDefinition target,
            out OffenseHexCoord destination,
            out string strandedExpeditionId)
        {
            target = null;
            destination = default;
            strandedExpeditionId = string.Empty;
            return false;
        }

        public bool TryCommitLaunch(string siteId) =>
            !string.IsNullOrWhiteSpace(siteId);

        public bool TryPrepareTravel(
            OffenseExpeditionRun expedition,
            OffenseHexCoord destination,
            bool pauseUntilDepartureCompletes,
            bool startsSiteAttack,
            out string message)
        {
            message = "Legacy world-map targets do not use strategic travel.";
            return false;
        }

        public bool TryRedirect(
            OffenseExpeditionRun expedition,
            OffenseHexCoord destination,
            string siteId,
            bool startsSiteAttack,
            out string message)
        {
            message = "Legacy world-map targets do not use strategic redirection.";
            return false;
        }

        public void RegisterRescueDispatch(
            bool isRescue,
            string strandedExpeditionId,
            OffenseExpeditionRun rescue,
            IEnumerable<CharacterActor> party)
        {
        }
    }

    private sealed class NonAwardingMemoryErasureSealFixture :
        IMemoryErasureSealBossAwardService
    {
        public static readonly NonAwardingMemoryErasureSealFixture Instance = new();

        private NonAwardingMemoryErasureSealFixture()
        {
        }

        public MemoryErasureSealBossAwardResult TryAwardForVictory(
            OffenseExpeditionRun expedition,
            OffenseRouteNode completedNode,
            bool worldObjectiveBattle) =>
            new(
                MemoryErasureSealBossAwardStatus.NotRegionBoss,
                string.Empty,
                string.Empty,
                string.Empty,
                "This reward-focused fixture verifies legacy expedition rewards; " +
                "the physical memory-seal lifecycle has a dedicated scenario.");

        public bool TryReconcilePendingAwards(out string failureReason)
        {
            failureReason = string.Empty;
            return true;
        }
    }

    private sealed class TestCharacterSaveService :
        ICharacterWorldSaveService,
        ICharacterLifetimeQuery
    {
        private readonly Dictionary<CharacterActor, string> ids = new Dictionary<CharacterActor, string>();

        public int LifetimeCharacterVersion => ids.Count;
        public IReadOnlyList<CharacterActor> AllCharacters => ids.Keys.ToArray();

        public DungeonCharacterWorldSaveData Capture(Grid grid) => new DungeonCharacterWorldSaveData();
        public void ValidateRestorePayload(
            Grid grid,
            DungeonCharacterWorldSaveData source) { }
        public CharacterWorldRestoreCandidate PrepareRestoreCandidate(
            Grid grid,
            DungeonCharacterWorldSaveData source) =>
            throw new NotSupportedException();
        public void StageRestoreCandidate(
            CharacterWorldRestoreCandidate candidate) { }

        public bool TryGetPersistentId(CharacterActor actor, out string persistentId)
        {
            return ids.TryGetValue(actor, out persistentId);
        }

        public string GetOrAssignPersistentId(CharacterActor actor)
        {
            if (!ids.TryGetValue(actor, out string persistentId))
            {
                persistentId = CharacterPersistentIdentity.Require(actor).Value;
                ids[actor] = persistentId;
            }

            return persistentId;
        }

        public bool TryGetRestoredActor(string persistentId, out CharacterActor actor)
        {
            actor = ids.FirstOrDefault(pair => pair.Value == persistentId).Key;
            return actor != null;
        }
    }

    private sealed class EmptyExpeditionMemberQuery : IOffenseExpeditionMemberQuery
    {
        public IReadOnlyList<CharacterActor> GetAvailableMemberActors() => Array.Empty<CharacterActor>();
    }

    private sealed class EmptyMetaRunResultBuilder : IMetaRunResultBuilder
    {
        public RunResultSnapshot Build(MetaRunResultBuildContext context)
        {
            return null;
        }
    }

    private sealed class RewardCombatPerformanceQuery : ICharacterPerformanceQuery
    {
        private readonly ICharacterPerformanceQuery inner;

        public bool ThrowOnEvaluate { get; set; }
        public int EvaluationCount { get; private set; }

        public RewardCombatPerformanceQuery(ICharacterPerformanceQuery inner)
        {
            this.inner = inner ?? throw new ArgumentNullException(nameof(inner));
        }

        public CharacterFunctionalCapacitySnapshot GetFunctionalCapacities(
            CharacterActor actor) => inner.GetFunctionalCapacities(actor);

        public CharacterPerformanceSnapshot Evaluate(
            CharacterActor actor,
            string formulaId,
            float contextFactor = 1f,
            GameplayEffectContext effectContext = null)
        {
            RecordEvaluation();
            return Boost(
                inner.Evaluate(actor, formulaId, contextFactor, effectContext),
                formulaId);
        }

        public CharacterPerformanceSnapshot Evaluate(
            CharacterActor actor,
            string formulaId,
            CharacterPerformanceEvaluationContext context)
        {
            RecordEvaluation();
            return Boost(inner.Evaluate(actor, formulaId, context), formulaId);
        }

        public CharacterPerformanceSnapshot EvaluateWork(
            CharacterActor actor,
            WorkTypeId workTypeId,
            CharacterPerformanceResultChannel resultChannel,
            CharacterPerformanceEvaluationContext context) =>
            inner.EvaluateWork(actor, workTypeId, resultChannel, context);

        public IReadOnlyList<CharacterPerformanceSnapshot> EvaluateDomain(
            CharacterActor actor,
            CharacterPerformanceFormulaDomain domain) =>
            inner.EvaluateDomain(actor, domain);

        public void ResetEvaluationCount()
        {
            EvaluationCount = 0;
        }

        private void RecordEvaluation()
        {
            EvaluationCount++;
            if (ThrowOnEvaluate)
            {
                throw new InvalidOperationException(
                    "Saved battle restore must not re-evaluate character performance.");
            }
        }

        private static CharacterPerformanceSnapshot Boost(
            CharacterPerformanceSnapshot snapshot,
            string formulaId)
        {
            if (snapshot != null
                && (formulaId?.StartsWith(
                        "performance:combat:",
                        StringComparison.Ordinal) == true
                    || string.Equals(
                        formulaId,
                        CharacterCompositePerformanceIds.PrecisionExecution,
                        StringComparison.Ordinal)))
            {
                snapshot.GameplayEffectFactor *= 10f;
                snapshot.Value *= 10f;
            }
            return snapshot;
        }
    }

    private sealed class EmptyMetaRuntimeApplicationPort : IMetaRuntimeApplicationPort
    {
        public void Bind(IMetaRuntimeEventSink runtime) { }
        public void Unbind(IMetaRuntimeEventSink runtime) { }

        public MetaRunEnvironmentSnapshot CaptureRunEnvironment() =>
            new MetaRunEnvironmentSnapshot(
                1f,
                DungeonDifficulty.Normal,
                DungeonSurvivalPressure.Standard);

        public void PublishUpgradePurchased(
            MetaUpgradePurchasedEvent purchasedEvent,
            string message) { }

        public void PublishRunResult(RunResultReadyEvent readyEvent) { }
        public void ShowRunResult(RunResultSnapshot result) { }
    }

    private sealed class EmptyPanelService : IOffensePanelService
    {
        public OffenseWorldMapPanel ShowWorldMap() => null;
        public OffenseExpeditionPanel ShowExpedition(OffenseExpeditionRuntime runtime) => null;
    }

    private sealed class FaultingMemoryErasureBossAwardFacts :
        IDomainFactStagingPublisher,
        IDomainFactRecoveryPublisher
    {
        private readonly IDomainFactStagingPublisher staging;
        private readonly IDomainFactRecoveryPublisher recovery;

        internal FaultingMemoryErasureBossAwardFacts(
            IDomainFactStagingPublisher staging,
            IDomainFactRecoveryPublisher recovery)
        {
            this.staging = staging
                ?? throw new ArgumentNullException(nameof(staging));
            this.recovery = recovery
                ?? throw new ArgumentNullException(nameof(recovery));
        }

        internal bool HideNextCommitAcknowledgement { get; set; }

        public bool TryStage<TFact>(
            in TFact fact,
            out IDomainFactStage stage,
            out string failureReason)
        {
            if (!staging.TryStage(fact, out stage, out failureReason))
                return false;
            if (fact is MemoryErasureBossAwardFact)
                stage = new FaultingStage(this, stage);
            return true;
        }

        public bool ReplayBatch(
            IReadOnlyList<DomainFactReplay> facts,
            DomainFactDelivery[] results,
            out string failure) => recovery.ReplayBatch(
                facts,
                results,
                out failure);

        private sealed class FaultingStage : IDomainFactStage
        {
            private readonly FaultingMemoryErasureBossAwardFacts owner;
            private readonly IDomainFactStage inner;

            internal FaultingStage(
                FaultingMemoryErasureBossAwardFacts owner,
                IDomainFactStage inner)
            {
                this.owner = owner;
                this.inner = inner
                    ?? throw new ArgumentNullException(nameof(inner));
            }

            public string OperationKey => inner.OperationKey;
            public long OwnerRevision => inner.OwnerRevision;
            public bool IsReplay => inner.IsReplay;
            public bool IsActive => inner.IsActive;

            public DomainFactDelivery Commit()
            {
                DomainFactDelivery delivery = inner.Commit();
                if (!owner.HideNextCommitAcknowledgement
                    || !delivery.MayBeCommitted)
                {
                    return delivery;
                }

                owner.HideNextCommitAcknowledgement = false;
                return new DomainFactDelivery(
                    DomainFactDeliveryPhase.InDoubt,
                    delivery.OperationKey,
                    delivery.PublicationId,
                    delivery.Fingerprint,
                    "qa-boss-award-commit-acknowledgement-lost");
            }

            public void Cancel() => inner.Cancel();
        }
    }

    private sealed class MemorySealPhysicalFixture
    {
        internal MemorySealPhysicalFixture()
        {
            IDungeonItemCatalogProvider catalog =
                EditorItemCatalogFactory.Create();
            Repository = new WorldItemRepository(
                new GuidPersistentIdGenerator(),
                new DungeonRuntimeAggregateRootStore());
            IPhysicalItemMassQuery mass = new PhysicalItemMassQuery(catalog);
            WorldItemSpawner spawner = new(
                catalog,
                Repository,
                EditorNullItemMarkerPresenter.Instance);
            WorldItemQueryService queries = new(
                catalog,
                mass,
                Repository,
                EditorNullItemMarkerPresenter.Instance);
            Gateway = new MemorySealPhysicalGateway(
                Repository,
                spawner,
                queries);
            Source = new PhysicalItemSourcePublicationService(Gateway, mass);
        }

        internal WorldItemRepository Repository { get; }
        internal MemorySealPhysicalGateway Gateway { get; }
        internal IPhysicalItemSourcePublicationService Source { get; }
    }

    private sealed class MemorySealPhysicalGateway :
        IEquipmentPhysicalItemGateway
    {
        private readonly WorldItemRepository repository;
        private readonly WorldItemSpawner spawner;
        private readonly WorldItemQueryService queries;

        internal MemorySealPhysicalGateway(
            WorldItemRepository repository,
            WorldItemSpawner spawner,
            WorldItemQueryService queries)
        {
            this.repository = repository
                ?? throw new ArgumentNullException(nameof(repository));
            this.spawner = spawner
                ?? throw new ArgumentNullException(nameof(spawner));
            this.queries = queries
                ?? throw new ArgumentNullException(nameof(queries));
        }

        internal bool RejectNextSpawn { get; set; }
        internal int SpawnAttemptCount { get; private set; }
        internal int SpawnCallCount { get; private set; }

        public bool SpawnItemAt(
            string itemId,
            int amount,
            Vector2Int position,
            WorldItemStackState state,
            string destinationId,
            out int spawned) => SpawnItemAtWithComponents(
                itemId,
                amount,
                position,
                state,
                destinationId,
                Array.Empty<ItemInstanceComponentSaveData>(),
                out spawned);

        public bool SpawnItemAtWithComponents(
            string itemId,
            int amount,
            Vector2Int position,
            WorldItemStackState state,
            string destinationId,
            IReadOnlyList<ItemInstanceComponentSaveData> components,
            out int spawned)
        {
            SpawnAttemptCount++;
            if (RejectNextSpawn)
            {
                RejectNextSpawn = false;
                spawned = 0;
                return false;
            }
            SpawnCallCount++;
            spawned = spawner.Spawn(
                itemId,
                amount,
                position,
                state,
                destinationId,
                components: components);
            return spawned == amount;
        }

        public IReadOnlyList<WorldItemStackSnapshot> GetAllStacks() =>
            queries.GetAllStacks();

        public bool DeleteStack(string stackId) =>
            WorldItemRepositoryEditorAccess.TryRemoveStack(
                repository,
                stackId);

        public bool TrySetInstanceComponent(
            string stackId,
            ItemInstanceComponentSaveData component) =>
            repository.SetEditorTestComponent(stackId, component);

        public bool SpawnExistingUniqueItemAt(
            string itemId,
            ItemInstanceId itemInstanceId,
            Vector2Int position,
            WorldItemStackState state,
            string destinationId,
            out string stackId) => throw Unsupported();

        public bool TryAbsorbUniqueItemStack(
            string stackId,
            ItemInstanceId expectedInstanceId) => throw Unsupported();

        public bool TryRequestItemDelivery(
            string itemId,
            int amount,
            Vector2Int destinationPosition,
            string destinationId,
            out int requested,
            out string failureReason) => throw Unsupported();

        public bool TryConsumeFacilityItemBuffer(
            string destinationId,
            IReadOnlyDictionary<string, int> costs,
            out string failureReason) => throw Unsupported();

        public bool TryConsumeStackQuantity(
            string stackId,
            int quantity,
            out WorldItemStackSnapshot consumed) => throw Unsupported();

        public bool TryCommitBatchPhysicalDisposition(
            IReadOnlyList<PhysicalItemTransformInput> inputs,
            PhysicalItemDispositionKind kind,
            string operationId,
            string reasonCode,
            out PhysicalItemBatchDispositionReceipt receipt,
            out string failureReason) => throw Unsupported();

        public bool TryCommitPendingBatchPhysicalDisposition(
            IReadOnlyList<PhysicalItemTransformInput> inputs,
            PhysicalItemDispositionKind kind,
            string operationId,
            string reasonCode,
            out PhysicalItemBatchDispositionReceipt receipt,
            out string failureReason) => throw Unsupported();

        public bool TryGetPendingBatchPhysicalDisposition(
            string operationId,
            out PhysicalItemBatchDispositionReceipt receipt) =>
            throw Unsupported();

        public bool AcknowledgeBatchPhysicalDisposition(
            string commitId,
            out string failureReason) => throw Unsupported();

        public bool TryRemoveInstanceComponent(
            string stackId,
            string componentTypeId) => throw Unsupported();

        public int ReleaseStacksByDestination(
            string destinationId,
            Vector2Int releasePosition) => throw Unsupported();

        private static NotSupportedException Unsupported() => new(
            "The boss-award physical fixture only supports exact loose output publication and explicit test removal.");
    }

    private sealed class MemorySealDropZoneFixture : IWorldDropZoneQuery
    {
        public static readonly MemorySealDropZoneFixture Instance = new();
        private static readonly Vector2Int Dropoff = new Vector2Int(3, 4);

        public bool TryGetDeliveryDropoff(out Vector2Int position)
        {
            position = Dropoff;
            return true;
        }

        public bool TryGetExpeditionLootDropoff(out Vector2Int position)
        {
            position = Dropoff;
            return true;
        }

        public bool TryGetVisitorEntryPoint(out WorldGridEntryPoint entryPoint)
        {
            entryPoint = default;
            return false;
        }
    }

    private sealed class RewardFixture : IDisposable
    {
        private readonly GameObject obj;

        public OffenseRewardRuntime Runtime { get; }
        public OffenseRegionRuntime Regions { get; }

        public RewardFixture(ScenarioContext context)
        {
            obj = new GameObject("Offense Reward Runtime Fixture Ranged Party");
            Runtime = obj.AddComponent<OffenseRewardRuntime>();
            Regions = new OffenseRegionRuntime();
            EmptyRewardContextDependencies dependencies = new EmptyRewardContextDependencies();
            ProgressionSceneRuntimeReferences progressionRuntimes =
                new ProgressionSceneRuntimeReferences(
                    obj.AddComponent<DailyFacilityShopRuntime>(),
                    obj.AddComponent<BlueprintResearchRuntime>(),
                    null);
            Runtime.Construct(
                new OffenseRewardContextBuilder(
                    progressionRuntimes,
                    dependencies,
                    dependencies,
                    Regions, returnArrivalRuntime: null),
                CreateGrantService());
            Runtime.SetDebugContext(
                context.GameSessionState,
                new[] { context.Warehouse },
                context.ShopUnlockState,
                context.ResearchState);
        }

        public void Dispose()
        {
            Runtime.ClearDebugContext();
            Object.DestroyImmediate(obj);
        }
    }

    private sealed class EmptyRewardContextDependencies :
        IGameSessionStateProvider,
        IWarehouseWorldQuery
    {
        public int WarehouseVersion => 0;
        public IReadOnlyList<IWarehouseFacility> Warehouses => Array.Empty<IWarehouseFacility>();

        public bool TryGetSessionState(out GameSessionState gameData)
        {
            gameData = null;
            return false;
        }
    }

    private sealed class UnusedEnemyEncounterFactory : IEnemyEncounterFactory
    {
        public static readonly UnusedEnemyEncounterFactory Instance = new();

        public EnemyEncounterComposition Create(
            OffenseTargetDefinition target,
            DungeonDifficulty difficulty,
            string encounterContext,
            OffenseRouteNode routeNode = null,
            OffenseStrategicPressureSnapshot pressure = default) =>
            throw new InvalidOperationException("This reward fixture does not start battles.");

        public EnemyEncounterComposition Restore(
            string encounterId,
            IEnumerable<EnemyIndividualSaveData> individuals,
            DungeonDifficulty difficulty,
            OffenseRouteNode routeNode = null,
            OffenseStrategicPressureSnapshot pressure = default) =>
            throw new InvalidOperationException("This reward fixture does not restore battles.");

        public string GetSummary(OffenseTargetDefinition target, string context) =>
            string.Empty;
    }

    private sealed class UnusedEnemyTactics : IEnemyTacticalDecisionService
    {
        public static readonly UnusedEnemyTactics Instance = new();

        public EnemyTacticalDecision Decide(
            OffenseBattleSession session,
            EnemyIndividualSaveData individual,
            bool allowAbility = true) =>
            throw new InvalidOperationException(
                "This reward fixture does not execute enemy turns.");
    }
}
