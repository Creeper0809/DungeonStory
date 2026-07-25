using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class OffenseJourneyDebugScenarios
{
    [MenuItem("DungeonStory/Debug/Offense/Run Journey Scenarios")]
    public static void RunFromMenu()
    {
        if (!RunAll(true)) Debug.LogError("Offense journey scenarios failed.");
    }

    public static bool RunAll(bool logSuccess)
    {
        List<string> errors = new List<string>();
        Run("branching event and loot", VerifyBranchingEventAndLoot, errors);
        Run("low light stress", VerifyLowLightStress, errors);
        Run("camp recovery and formation", VerifyCampRecoveryAndFormation, errors);
        Run("experience pacing", VerifyExperiencePacing, errors);
        Run("return recovery readiness", VerifyReturnRecoveryReadiness, errors);
        Run("journey state restore", VerifyJourneyStateRestore, errors);
        Run("offense save payload round trip", VerifySavePayloadRoundTrip, errors);

        foreach (string error in errors) Debug.LogError(error);
        if (errors.Count == 0 && logSuccess) Debug.Log("Offense journey scenarios passed.");
        return errors.Count == 0;
    }

    private static bool VerifyBranchingEventAndLoot()
    {
        using ActorFixture fixture = new ActorFixture("Route Tester");
        OffenseSupplyLoadout supplies = Loadout(
            (OffenseSupplyType.Rations, 4),
            (OffenseSupplyType.Tools, 1));
        OffenseExpeditionRun run = CreateRun(fixture.Actor, supplies);

        Require(run.GetAvailableRouteNodes().Count == 2, "Entrance did not expose two route choices.");
        OffenseRouteNode eventNode = run.GetAvailableRouteNodes()
            .Single(node => node.Kind == OffenseRouteNodeKind.Event);
        Require(run.TryEnterNode(eventNode.Id, out _), "Could not enter the event branch.");
        Require(run.Phase == OffenseExpeditionPhase.ResolvingNode, "Event did not enter resolving phase.");
        Require(supplies.Get(OffenseSupplyType.Rations) == 3, "Travel did not consume one ration.");
        Require(run.TryResolveCurrentNode(true, out OffenseExpeditionNodeResult result, out _),
            "Tool-backed event choice failed.");
        Require(result.UsedSupply && result.GainedLoot, "Event result did not record supply and loot.");
        Require(supplies.Get(OffenseSupplyType.Tools) == 0, "Event did not consume a tool.");
        Require(run.CarriedStock.GetValueOrDefault(StockCategory.General) > 0,
            "Event did not add carried stock.");
        Require(run.Phase == OffenseExpeditionPhase.ChoosingRoute,
            "Resolved event did not return to route choice.");
        return true;
    }

    private static bool VerifyLowLightStress()
    {
        using ActorFixture fixture = new ActorFixture("Low Light Tester");
        OffenseExpeditionRun run = new OffenseExpeditionRun(
            "journey:dark",
            Target(),
            new[] { fixture.Actor },
            10f,
            100f,
            null,
            new OffenseSupplyLoadout(),
            new OffenseExpeditionPreparation(startingLight: 10f));
        OffenseRouteNode battle = run.GetAvailableRouteNodes()
            .Single(node => node.Kind == OffenseRouteNodeKind.Battle);
        Require(run.TryEnterNode(battle.Id, out _), "Could not enter dark battle route.");
        Require(run.MemberStates[0].Stress >= 10f,
            "Missing rations and low light did not accumulate stress.");
        Require(run.Light <= 0.01f, "Travel did not drain low starting light.");
        return true;
    }

    private static bool VerifyCampRecoveryAndFormation()
    {
        using ActorFixture first = new ActorFixture("Front Tester");
        using ActorFixture second = new ActorFixture("Rear Tester");
        first.Actor.ApplyDamage(50f, "journey test");
        OffenseExpeditionRun run = new OffenseExpeditionRun(
            "journey:camp",
            Target(),
            new[] { first.Actor, second.Actor },
            20f,
            100f,
            null,
            Loadout((OffenseSupplyType.Rations, 5)),
            new OffenseExpeditionPreparation(campHealRatio: 0.25f, campStressRecovery: 25f));

        OffenseRouteNode eventNode = run.GetAvailableRouteNodes()
            .Single(node => node.Kind == OffenseRouteNodeKind.Event);
        Require(run.TryEnterNode(eventNode.Id, out _), "Could not enter camp approach event.");
        Require(run.TryResolveCurrentNode(false, out _, out _), "Could not resolve camp approach.");
        OffenseRouteNode camp = run.GetAvailableRouteNodes()
            .Single(node => node.Kind == OffenseRouteNodeKind.Camp);
        Require(run.TryEnterNode(camp.Id, out _), "Could not enter camp.");
        float healthBeforeCamp = first.Actor.CurrentHealth;
        Require(run.TryResolveCurrentNode(true, out _, out _), "Camp supply choice failed.");
        Require(first.Actor.CurrentHealth > healthBeforeCamp, "Camp did not heal the injured member.");
        Require(run.MemberStates.All(member => member.Stress < 15f), "Camp did not recover stress.");
        Require(run.TrySwapFormation(0, 1, out _), "Formation swap failed between nodes.");
        Require(run.MemberStates[0].Actor == second.Actor
            && run.MemberStates[0].Formation == OffenseFormationSlot.Front,
            "Formation order did not follow the swap.");
        return true;
    }

    private static bool VerifyExperiencePacing()
    {
        int level50Experience = Enumerable.Range(1, CharacterProgression.MaxLevel - 1)
            .Sum(CharacterProgression.GetExperienceRequired);
        Require(level50Experience == 1460,
            $"Level-50 cumulative XP changed to {level50Experience}.");

        int combatByStage2 = SumRouteExperience(1, 2, combatPath: true);
        int combatByStage3 = SumRouteExperience(1, 3, combatPath: true);
        Require(combatByStage2 < level50Experience,
            $"Combat path reached level 50 too early at stage 2 with {combatByStage2} XP.");
        Require(combatByStage3 >= level50Experience,
            $"Combat path did not reach level 50 by stage 3 with {combatByStage3} XP.");

        int safeByStage3 = SumRouteExperience(1, 3, combatPath: false);
        int safeByStage4 = SumRouteExperience(1, 4, combatPath: false);
        Require(safeByStage3 < level50Experience,
            $"Safe path reached level 50 too early at stage 3 with {safeByStage3} XP.");
        Require(safeByStage4 >= level50Experience,
            $"Safe path did not reach level 50 by stage 4 with {safeByStage4} XP.");
        return true;
    }

    private static int SumRouteExperience(int firstStage, int lastStage, bool combatPath)
    {
        int total = 0;
        for (int stage = firstStage; stage <= lastStage; stage++)
        {
            OffenseRouteGraph route = OffenseRouteGenerator.Create(Target(stage));
            string[] nodeSuffixes = combatPath
                ? new[] { ":approach-battle", ":camp", ":elite-battle", ":boss" }
                : new[] { ":approach-event", ":camp", ":deep-event", ":boss" };
            foreach (string suffix in nodeSuffixes)
            {
                OffenseRouteNode node = route.Nodes.First(item => item.Id.EndsWith(suffix, StringComparison.Ordinal));
                total += OffenseExpeditionRuntime.CalculateNodeExperience(node, stage);
            }

            total += OffenseExpeditionRuntime.CalculateSuccessfulReturnExperience(stage);
        }

        return total;
    }

    private static bool VerifyReturnRecoveryReadiness()
    {
        using ActorFixture fixture = new ActorFixture("Recovery Tester");
        Require(OffenseExpeditionService.CanJoinExpedition(fixture.Actor, out _),
            "Fresh active staff member could not join an expedition.");

        fixture.Actor.ApplyDamage(fixture.Actor.MaxHealth * 0.9f, "recovery test");
        Require(!OffenseExpeditionService.CanJoinExpedition(fixture.Actor, out string healthReason)
                && healthReason == "expedition-health-too-low",
            $"Low-health expedition member was not blocked. reason={healthReason}");

        BuildingExpeditionRecoveryAbility recovery = new BuildingExpeditionRecoveryAbility
        {
            healthHealRatio = 1f,
            injuryReduction = 0f,
            stressRecovery = 0f
        };
        recovery.ApplyUseCompleted(fixture.Actor, null);
        Require(OffenseExpeditionService.CanJoinExpedition(fixture.Actor, out _),
            "Health recovery did not restore expedition readiness.");

        fixture.Actor.Lifecycle.RecordExpeditionReturn(85f, alive: true);
        Require(!OffenseExpeditionService.CanJoinExpedition(fixture.Actor, out string stressReason)
                && stressReason == "expedition-stress-too-high",
            $"High-stress expedition member was not blocked. reason={stressReason}");

        recovery.stressRecovery = 90f;
        recovery.ApplyUseCompleted(fixture.Actor, null);
        Require(OffenseExpeditionService.CanJoinExpedition(fixture.Actor, out _),
            "Stress recovery did not restore expedition readiness.");

        fixture.Actor.Lifecycle.RecordExpeditionReturn(42f, alive: true);
        DungeonCharacterSaveData saved = new DungeonCharacterSaveData
        {
            expeditionRecovery = fixture.Actor.Lifecycle.ExpeditionRecovery.Clone()
        };
        string json = JsonUtility.ToJson(saved);
        DungeonCharacterSaveData restored = JsonUtility.FromJson<DungeonCharacterSaveData>(json);
        Require(restored.expeditionRecovery != null
                && Mathf.Approximately(restored.expeditionRecovery.stress, 42f),
            "Expedition recovery state did not survive JSON save data.");
        return true;
    }

    private static bool VerifyJourneyStateRestore()
    {
        using ActorFixture fixture = new ActorFixture("Restore Tester");
        OffenseExpeditionRun source = CreateRun(
            fixture.Actor,
            Loadout((OffenseSupplyType.Rations, 4), (OffenseSupplyType.Tools, 1)));
        OffenseRouteNode eventNode = source.GetAvailableRouteNodes()
            .Single(node => node.Kind == OffenseRouteNodeKind.Event);
        source.TryEnterNode(eventNode.Id, out _);
        source.TryResolveCurrentNode(false, out _, out _);

        OffenseExpeditionRun restored = CreateRun(
            fixture.Actor,
            new OffenseSupplyLoadout(source.Supplies.Amounts));
        restored.MemberStates[0].Restore(
            OffenseFormationSlot.Rear,
            source.MemberStates[0].Stress,
            12f);
        restored.RestoreJourneyState(
            source.Phase,
            source.CurrentNodeId,
            source.Light,
            source.CompletedNodeIds,
            source.CarriedStock);

        Require(restored.Phase == source.Phase, "Journey phase changed during restore.");
        Require(restored.CurrentNodeId == source.CurrentNodeId, "Current route node changed during restore.");
        Require(Mathf.Approximately(restored.Light, source.Light), "Light changed during restore.");
        Require(new HashSet<string>(restored.CompletedNodeIds).SetEquals(source.CompletedNodeIds),
            "Completed route nodes changed during restore.");
        Require(restored.MemberStates[0].Formation == OffenseFormationSlot.Rear
            && Mathf.Approximately(restored.MemberStates[0].TotalDamageTaken, 12f),
            "Member journey state did not restore.");
        return true;
    }

    private static bool VerifySavePayloadRoundTrip()
    {
        using ActorFixture fixture = new ActorFixture("Save Payload Tester");
        OffenseExpeditionRun run = CreateRun(
            fixture.Actor,
            Loadout((OffenseSupplyType.Rations, 4), (OffenseSupplyType.Tools, 1)));
        OffenseRouteNode eventNode = run.GetAvailableRouteNodes()
            .Single(node => node.Kind == OffenseRouteNodeKind.Event);
        run.TryEnterNode(eventNode.Id, out _);
        run.TryResolveCurrentNode(false, out _, out _);
        run.MemberStates[0].Restore(OffenseFormationSlot.Rear, 37f, 14f);

        GameObject runtimeObject = new GameObject("Offense Save Payload Runtime");
        try
        {
            OffenseExpeditionRuntime runtime = runtimeObject.AddComponent<OffenseExpeditionRuntime>();
            runtime.RestorePersistentState(new[] { run }, Array.Empty<OffenseExpeditionResult>());
            FakeCharacterSaveService characterSave = new FakeCharacterSaveService(fixture.Actor, "actor:save-test");
            OffenseSaveService service = new OffenseSaveService(
                new EmptyWorldMapProvider(),
                new EmptyRewardProvider(),
                new FixedExpeditionProvider(runtime),
                characterSave,
                new EmptyBattleRuntime());

            DungeonOffenseSaveData captured = service.Capture();
            string json = JsonUtility.ToJson(captured);
            DungeonOffenseSaveData restored = JsonUtility.FromJson<DungeonOffenseSaveData>(json);
            DungeonOffenseExpeditionRunSaveData saved = restored.activeExpeditions.Single();
            DungeonOffenseExpeditionMemberStateSaveData savedMember = saved.memberStates.Single();

            Require(saved.journeyVersion == 1, "Journey save version was not written.");
            Require(saved.phase == run.Phase && saved.currentNodeId == run.CurrentNodeId,
                "Journey phase or current node did not round-trip.");
            Require(Mathf.Approximately(saved.light, run.Light), "Journey light did not round-trip.");
            Require(new HashSet<string>(saved.completedNodeIds).SetEquals(run.CompletedNodeIds),
                "Completed route nodes did not round-trip.");
            Require(saved.supplies.Sum(entry => entry.amount) == run.Supplies.TotalCount,
                "Remaining supplies did not round-trip.");
            Require(savedMember.formation == OffenseFormationSlot.Rear
                && Mathf.Approximately(savedMember.stress, 37f)
                && Mathf.Approximately(savedMember.totalDamageTaken, 14f),
                "Member formation, stress, or damage did not round-trip.");
            return true;
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(runtimeObject);
        }
    }

    private static OffenseExpeditionRun CreateRun(CharacterActor actor, OffenseSupplyLoadout supplies)
    {
        return new OffenseExpeditionRun(
            Guid.NewGuid().ToString("N"),
            Target(),
            new[] { actor },
            OffenseExpeditionService.CalculateMemberPower(actor),
            100f,
            null,
            supplies,
            new OffenseExpeditionPreparation());
    }

    private static OffenseTargetDefinition Target()
    {
        return Target(2);
    }

    private static OffenseTargetDefinition Target(int stage)
    {
        return new OffenseTargetDefinition
        {
            id = "journey-test-target",
            title = "Journey Test",
            campaignOrder = Mathf.Clamp(stage, 1, 6),
            requiredMembers = 1,
            requiredPower = 10f,
            durationSeconds = 100f
        };
    }

    private static OffenseSupplyLoadout Loadout(
        params (OffenseSupplyType Type, int Amount)[] values)
    {
        return new OffenseSupplyLoadout(values.ToDictionary(value => value.Type, value => value.Amount));
    }

    private static void Run(string name, Func<bool> scenario, ICollection<string> errors)
    {
        try
        {
            if (!scenario()) errors.Add(name);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            errors.Add($"{name}: {exception.Message}");
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private sealed class ActorFixture : IDisposable
    {
        private readonly GameObject gameObject;
        private readonly CharacterSO data;

        public ActorFixture(string name)
        {
            data = ScriptableObject.CreateInstance<CharacterSO>();
            data.id = Mathf.Abs(name.GetHashCode());
            data.characterName = name;
            data.characterType = CharacterType.NPC;
            data.role = CharacterRole.Regular;
            data.speciesTag = "Orc";
            data.baseStats = CharacterStatBlock.CreateDefault(10);
            data.defaultWorkPriorities = WorkPriorityProfile.CreateDefault();

            gameObject = new GameObject(name);
            gameObject.AddComponent<SpriteRenderer>();
            Actor = gameObject.AddComponent<CharacterActor>();
            gameObject.AddComponent<AbilityMove>();
            gameObject.AddComponent<AbilityWork>();
            Actor.RefreshAbilityCache();
            CharacterAiEditorTestDependencies.Inject(gameObject);
            Actor.Initialization(data);
            Actor.SetLifecycleState(CharacterLifecycleState.Active);
        }

        public CharacterActor Actor { get; }

        public void Dispose()
        {
            UnityEngine.Object.DestroyImmediate(gameObject);
            UnityEngine.Object.DestroyImmediate(data);
        }
    }

    private sealed class EmptyWorldMapProvider : IOffenseWorldMapRuntimeProvider
    {
        public bool TryGetRuntime(out OffenseWorldMapRuntime runtime)
        {
            runtime = null;
            return false;
        }
    }

    private sealed class EmptyRewardProvider : IOffenseRewardRuntimeProvider
    {
        public bool TryGetRuntime(out OffenseRewardRuntime runtime)
        {
            runtime = null;
            return false;
        }
    }

    private sealed class FixedExpeditionProvider : IOffenseExpeditionRuntimeProvider
    {
        private readonly OffenseExpeditionRuntime runtime;

        public FixedExpeditionProvider(OffenseExpeditionRuntime runtime)
        {
            this.runtime = runtime;
        }

        public bool TryGetRuntime(out OffenseExpeditionRuntime value)
        {
            value = runtime;
            return value != null;
        }
    }

    private sealed class FakeCharacterSaveService : ICharacterWorldSaveService
    {
        private readonly CharacterActor actor;
        private readonly string persistentId;

        public FakeCharacterSaveService(CharacterActor actor, string persistentId)
        {
            this.actor = actor;
            this.persistentId = persistentId;
        }

        public DungeonCharacterWorldSaveData Capture(Grid grid) => new DungeonCharacterWorldSaveData();
        public void PrepareForWorldRestore() { }
        public int Restore(Grid grid, DungeonCharacterWorldSaveData source, DungeonGameRestoreReport report) => 0;
        public bool TryGetPersistentId(CharacterActor candidate, out string value)
        {
            value = ReferenceEquals(candidate, actor) ? persistentId : string.Empty;
            return !string.IsNullOrWhiteSpace(value);
        }
        public string GetOrAssignPersistentId(CharacterActor candidate)
        {
            return ReferenceEquals(candidate, actor) ? persistentId : string.Empty;
        }
        public bool TryGetRestoredActor(string id, out CharacterActor value)
        {
            value = string.Equals(id, persistentId, StringComparison.Ordinal) ? actor : null;
            return value != null;
        }
    }

    private sealed class EmptyBattleRuntime : IOffenseBattleRuntime
    {
        public OffenseBattleSession Session => null;
        public bool HasActiveBattle => false;
        public bool IsBattleViewVisible => false;
        public event Action StateChanged { add { } remove { } }
        public event Action<OffenseBattleSession> BattleCompleted { add { } remove { } }
        public bool TryStartBattle(OffenseExpeditionRun expedition, out string message)
        {
            message = string.Empty;
            return false;
        }
        public void AdvanceToPlayerDecision() { }
        public bool TryIssuePlayerCommand(OffenseBattleActionType actionType, string targetId, string abilityId, out OffenseBattleCommandResult result)
        {
            result = null;
            return false;
        }
        public bool TryExecuteCommand(OffenseBattleCommand command, out OffenseBattleCommandResult result)
        {
            result = null;
            return false;
        }
        public bool TryGetActor(string persistentId, out CharacterActor actor)
        {
            actor = null;
            return false;
        }
        public OffenseBattlePersistenceState CapturePersistentState() => null;
        public bool TryRestoreBattle(OffenseExpeditionRun expedition, OffenseBattlePersistenceState state, out string message)
        {
            message = string.Empty;
            return false;
        }
        public void ClearForPersistentRestore() { }
        public void SetBattleViewVisible(bool visible) { }
        public void ClearCompletedBattle() { }
    }
}
