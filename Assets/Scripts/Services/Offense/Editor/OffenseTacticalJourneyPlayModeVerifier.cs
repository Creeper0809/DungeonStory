#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using VContainer;

/// <summary>
/// Runs the public expedition and battle runtimes end-to-end.  Tactical PASS
/// evidence is emitted only from <see cref="OffenseBattleRuntime"/>'s owned
/// decision-to-command trace; this verifier never calls the enemy decision
/// service or the battle session command executor directly.
/// </summary>
public static class OffenseTacticalJourneyPlayModeVerifier
{
    public const string ReportPath =
        "Artifacts/QA/offense-tactical-journey-playmode.txt";
    private const string PendingPath =
        "Temp/offense-tactical-journey-playmode.flag";
    private const string GameplayScenePath = "Assets/Scenes/GameplayScene.unity";

    [MenuItem("DungeonStory/Debug/QA/Run Offense Tactical Journey PlayMode Verification")]
    public static void RequestRun()
    {
        if (EditorApplication.isPlaying)
        {
            StartRunner(false);
            return;
        }

        Directory.CreateDirectory("Temp");
        File.WriteAllText(PendingPath, DateTime.UtcNow.ToString("O"));
        if (!string.Equals(
                SceneManager.GetActiveScene().path,
                GameplayScenePath,
                StringComparison.OrdinalIgnoreCase))
        {
            EditorSceneManager.OpenScene(GameplayScenePath, OpenSceneMode.Single);
        }
        EditorApplication.EnterPlaymode();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (!File.Exists(PendingPath)) return;
        File.Delete(PendingPath);
        StartRunner(true);
    }

    private static void StartRunner(bool exitPlayMode)
    {
        if (UnityEngine.Object.FindFirstObjectByType<
                OffenseTacticalJourneyPlayModeRunner>() != null)
            return;
        OffenseTacticalJourneyPlayModeRunner runner =
            new GameObject("Offense Tactical Journey PlayMode Runner")
                .AddComponent<OffenseTacticalJourneyPlayModeRunner>();
        runner.ExitPlayModeOnCompletion = exitPlayMode;
    }
}

public sealed class OffenseTacticalJourneyPlayModeRunner : MonoBehaviour
{
    private const float OverallTimeoutRealtime = 240f;
    private const string VerifierRevision = "offense-tactical-journey-v4";
    private readonly List<string> evidence = new List<string>();
    private readonly List<string> failures = new List<string>();
    private readonly List<OffenseEnemyTacticalExecutionTrace> observed =
        new List<OffenseEnemyTacticalExecutionTrace>();

    public bool ExitPlayModeOnCompletion { get; set; }

    private IEnumerator Start()
    {
        Directory.CreateDirectory("Artifacts/QA");
        WriteReport("RUNNING", "resolve-production-world");
        float deadline = Time.realtimeSinceStartup + OverallTimeoutRealtime;
        yield return RunGuarded(deadline);
        WriteReport(failures.Count == 0 ? "PASS" : "FAIL", "complete");
        if (failures.Count == 0)
            Debug.Log("[OffenseTacticalJourney] PASS");
        else
            Debug.LogError("[OffenseTacticalJourney] " + string.Join(" | ", failures));
        Destroy(gameObject);
        if (ExitPlayModeOnCompletion)
        {
            EditorApplication.delayCall += () =>
            {
                if (EditorApplication.isPlaying) EditorApplication.isPlaying = false;
            };
        }
    }

    private IEnumerator RunGuarded(float deadline)
    {
        IEnumerator routine = RunJourney(deadline);
        while (true)
        {
            if (Time.realtimeSinceStartup >= deadline)
            {
                failures.Add("overall-timeout");
                yield break;
            }

            object current;
            try
            {
                if (!routine.MoveNext()) yield break;
                current = routine.Current;
            }
            catch (Exception exception)
            {
                failures.Add(exception.ToString());
                yield break;
            }
            yield return current;
        }
    }

    private IEnumerator RunJourney(float deadline)
    {
        Require(string.Equals(
                SceneManager.GetActiveScene().path,
                "Assets/Scenes/GameplayScene.unity",
                StringComparison.OrdinalIgnoreCase),
            "official GameplayScene is not active");
        if (failures.Count > 0) yield break;
        DungeonRuntimeLifetimeScope scope = null;
        OffenseExpeditionRuntime expeditions = null;
        OffenseWorldMapRuntime worldMap = null;
        float setupDeadline = Mathf.Min(deadline, Time.realtimeSinceStartup + 15f);
        bool prepared = false;
        while (Time.realtimeSinceStartup < setupDeadline)
        {
            scope = FindFirstObjectByType<DungeonRuntimeLifetimeScope>(
                FindObjectsInactive.Include);
            expeditions = FindFirstObjectByType<OffenseExpeditionRuntime>(
                FindObjectsInactive.Include);
            worldMap = FindFirstObjectByType<OffenseWorldMapRuntime>(
                FindObjectsInactive.Include);
            if (scope?.Container != null && expeditions != null && worldMap != null)
                break;
            if (!prepared && scope?.Container != null)
            {
                prepared = true;
                StartPartyPreparationPlayModeVerifier.RunFastCommitForDebug();
            }
            yield return null;
        }

        Require(scope?.Container != null, "production LifetimeScope missing");
        Require(expeditions != null, "production expedition runtime missing");
        Require(worldMap != null, "production world-map runtime missing");
        if (failures.Count > 0) yield break;

        evidence.Add("start-party="
            + StartPartyPreparationPlayModeVerifier.RunFastCommitForDebug());
        yield return null;

        IDungeonSaveSectionRegistry saveSections =
            scope.Container.Resolve<IDungeonSaveSectionRegistry>();
        List<DungeonSaveSectionEnvelope> baseline = saveSections.CaptureAll();
        DungeonSaveSectionEnvelope researchEnvelope = baseline.FirstOrDefault(
            value => string.Equals(
                value.sectionId,
                BlueprintResearchSaveSection.Id,
                StringComparison.Ordinal));
        Require(researchEnvelope != null,
            "research save authority is missing from the official registry");
        if (researchEnvelope == null) yield break;
        DungeonResearchSaveData research = JsonUtility.FromJson<
            DungeonResearchSaveData>(researchEnvelope.payloadJson);
        Require(research != null, "research baseline payload is invalid");
        if (research == null) yield break;
        research.completedProjectIds ??= new List<string>();
        research.projectProgress ??=
            new List<DungeonResearchProjectProgressSaveData>();
        research.projectQueue ??= new List<DungeonResearchQueueEntrySaveData>();
        research.completedProjectIds.RemoveAll(value => string.Equals(
            value,
            OffenseExpeditionAccessRules.RequiredResearchId,
            StringComparison.Ordinal));
        research.completedProjectIds.Add(
            OffenseExpeditionAccessRules.RequiredResearchId);
        research.projectProgress.RemoveAll(value => value != null
            && string.Equals(
                value.projectId,
                OffenseExpeditionAccessRules.RequiredResearchId,
                StringComparison.Ordinal));
        research.projectQueue.RemoveAll(value => value != null
            && string.Equals(
                value.projectId,
                OffenseExpeditionAccessRules.RequiredResearchId,
                StringComparison.Ordinal));
        if (string.Equals(
                research.activeProjectId,
                OffenseExpeditionAccessRules.RequiredResearchId,
                StringComparison.Ordinal))
            research.activeProjectId = string.Empty;
        researchEnvelope.payloadJson = JsonUtility.ToJson(research);
        DungeonGameRestoreReport researchRestore = new DungeonGameRestoreReport();
        Require(saveSections.RestoreAll(baseline, researchRestore)
                && researchRestore.Success,
            "legal research save baseline restore failed: "
                + string.Join(" | ", researchRestore.Errors));
        IBlueprintResearchStateService researchState =
            scope.Container.Resolve<IBlueprintResearchStateService>();
        Require(OffenseExpeditionAccessRules.IsUnlocked(researchState.GetState()),
            "restored research baseline did not unlock expedition access");
        if (failures.Count > 0) yield break;
        evidence.Add("research-prerequisite=save-registry:"
            + OffenseExpeditionAccessRules.RequiredResearchId);

        IOffenseBattleRuntime battle = scope.Container.Resolve<IOffenseBattleRuntime>();
        IOffenseEnemyTacticalTraceQuery trace = battle as IOffenseEnemyTacticalTraceQuery;
        IEnemyArchetypeCatalog enemyArchetypes =
            scope.Container.Resolve<IEnemyArchetypeCatalog>();
        Require(trace != null, "battle runtime does not expose owned tactical trace");
        IReadOnlyList<CharacterActor> members = expeditions.GetAvailableMemberActors();
        Require(members != null && members.Count > 0,
            "no production expedition member is available");
        if (failures.Count > 0) yield break;

        OffenseTargetDefinition target = worldMap.TargetDefinitions
            .Where(value => value != null
                && value.requiredMembers <= members.Count)
            .OrderBy(value => value.campaignOrder)
            .ThenBy(value => value.id, StringComparer.Ordinal)
            .FirstOrDefault();
        Require(target != null, "no authored offense target");
        if (target == null) yield break;

        // The restore candidate only reveals an authored campaign target.  All
        // journey, route, battle and terminal transitions below go through the
        // public production commands.
        worldMap.Campaign.PublishRestoreCandidate(
            worldMap.Campaign.BuildRestoreCandidate(
                new DungeonOffenseCampaignSaveData
                {
                    reconLevel = 1,
                    selectedTargetId = target.id,
                    knownTargetIds = new List<string> { target.id }
                }));
        Require(worldMap.TrySelectTarget(target.id, out _, out string selectMessage),
            "authored target selection failed: " + selectMessage);
        if (failures.Count > 0) yield break;

        Require(expeditions.TryStartExpedition(
                target.id,
                members.Take(Mathf.Max(1, target.requiredMembers)),
                out OffenseExpeditionRun expedition,
                out string startMessage),
            "production expedition start failed: " + startMessage);
        if (expedition == null) yield break;
        evidence.Add("journey:start=" + expedition.ExpeditionId);
        WriteReport("RUNNING", "journey-started");

        bool tacticalConditionRowsRun = false;
        bool tacticalConditionRowsRunning = false;
        bool journeyDriveBlocked = false;
        Action capturePublishedBattle = () =>
        {
            if (tacticalConditionRowsRun
                || tacticalConditionRowsRunning
                || !battle.HasActiveBattle
                || !string.Equals(
                    battle.Session?.ExpeditionId,
                    expedition.ExpeditionId,
                    StringComparison.Ordinal))
            {
                return;
            }

            // Route selection starts the battle and can also run every enemy
            // turn synchronously.  A short encounter may therefore complete
            // before the journey coroutine can ever observe InBattle.  Capture
            // and exercise the exact production session at its publication
            // boundary instead of polling that transient expedition phase.
            OffenseBattlePersistenceState conditionBaseline =
                battle.CapturePersistentState();
            if (conditionBaseline == null) return;

            tacticalConditionRowsRunning = true;
            tacticalConditionRowsRun = true;
            try
            {
                evidence.Add("condition-baseline:session-published; battle="
                    + conditionBaseline.battleId);
                RunTacticalConditionRows(
                    battle,
                    trace,
                    enemyArchetypes,
                    expedition,
                    conditionBaseline);

                // Non-terminal condition rows are legal save-state fixtures,
                // so restore the exact published battle before the route
                // command continues. An accepted enemy Retreat is itself a
                // production victory terminal and advances the expedition via
                // the normal completion handler; resurrecting the old battle
                // after that terminal would corrupt the journey phase.
                if (battle.HasActiveBattle)
                {
                    Require(battle.TryRestoreBattle(
                            expedition,
                            CloneBattleState(conditionBaseline),
                            out string baselineRestoreMessage),
                        "condition baseline restore failed: "
                        + baselineRestoreMessage);
                    CaptureTrace(trace);
                }
                else
                {
                    evidence.Add(
                        "condition-baseline:terminal-consumed-by-production-retreat");
                }
            }
            finally
            {
                tacticalConditionRowsRunning = false;
            }
        };

        battle.StateChanged += capturePublishedBattle;
        try
        {
            // Handles a battle published by another production subscriber in
            // the same command boundary before this verifier attached.
            capturePublishedBattle();

            int safety = 0;
            while (expeditions.ActiveExpeditions.Contains(expedition)
                && Time.realtimeSinceStartup < deadline
                && !journeyDriveBlocked
                && safety++ < 2048)
            {
                CaptureTrace(trace);
                switch (expedition.Phase)
                {
                    case OffenseExpeditionPhase.ChoosingRoute:
                    {
                        OffenseRouteNode next = expedition.GetAvailableRouteNodes()
                            .OrderBy(node => node.Id, StringComparer.Ordinal)
                            .FirstOrDefault();
                        Require(next != null, "route has no available node");
                        if (next == null) yield break;
                        Require(expeditions.TryChooseRouteNode(
                                expedition.ExpeditionId,
                                next.Id,
                                out string routeMessage),
                            "route command failed: " + routeMessage);
                        break;
                    }
                    case OffenseExpeditionPhase.ResolvingNode:
                        Require(expeditions.TryResolveCurrentNode(
                                expedition.ExpeditionId,
                                useSupply: false,
                                out _,
                                out string resolveMessage),
                            "node command failed: " + resolveMessage);
                        break;
                    case OffenseExpeditionPhase.InBattle:
                        journeyDriveBlocked = !RunOnePlayerTurn(battle);
                        CaptureTrace(trace);
                        break;
                    default:
                        yield return null;
                        break;
                }
            }
        }
        finally
        {
            battle.StateChanged -= capturePublishedBattle;
        }

        CaptureTrace(trace);
        Require(tacticalConditionRowsRun,
            "production battle never published a persistence baseline");
        Require(!expeditions.ActiveExpeditions.Contains(expedition),
            "expedition did not reach a terminal lifecycle state");
        Require(observed.Count > 0,
            "no enemy intent was observed through production battle turns");
        foreach (EnemyTacticalIntentKind intent in Enum.GetValues(
            typeof(EnemyTacticalIntentKind)))
        {
            bool covered = observed.Any(value => value.Intent == intent
                && value.CommandId > 0
                && !string.IsNullOrWhiteSpace(value.ActorId)
                && !string.IsNullOrWhiteSpace(value.TerminalMessage));
            Require(covered, "missing intent->command->terminal evidence: " + intent);
        }
        Require(observed.All(value => IntentMapsToCommand(value.Intent, value.Command)),
            "enemy intent mapped to an unexpected command");
        evidence.Add("journey:terminal=true");
    }

    private void RunTacticalConditionRows(
        IOffenseBattleRuntime battle,
        IOffenseEnemyTacticalTraceQuery trace,
        IEnemyArchetypeCatalog archetypes,
        OffenseExpeditionRun expedition,
        OffenseBattlePersistenceState baseline)
    {
        EnemyIndividualSaveData[] individuals = (baseline.enemyIndividuals
                ?? new List<EnemyIndividualSaveData>())
            .Where(value => value != null)
            .ToArray();
        Dictionary<string, EnemyIndividualSaveData> individualById =
            individuals.ToDictionary(
                value => value.characterId,
                value => value,
                StringComparer.Ordinal);
        string allyId = battle.Session?.Combatants
            .Where(value => value.Team == OffenseBattleTeam.Allies
                && !value.IsDead && !value.IsDowned)
            .Select(value => value.PersistentId)
            .FirstOrDefault();
        Require(individuals.Length > 0,
            "condition rows require an authored enemy individual");
        Require(!string.IsNullOrWhiteSpace(allyId),
            "condition rows require a living production ally");
        if (individuals.Length == 0 || string.IsNullOrWhiteSpace(allyId))
            return;

        // Select every row's authored combatant from the immutable production
        // session baseline before the first row is restored and executed.
        // RunEnemyTurns is synchronous and is allowed to down or kill actors;
        // consulting battle.Session again after the Attack row would therefore
        // make later row availability depend on an earlier verifier row.
        Dictionary<EnemyTacticalIntentKind, EnemyIndividualSaveData>
            conditionActors = Enum.GetValues(typeof(EnemyTacticalIntentKind))
                .Cast<EnemyTacticalIntentKind>()
                .ToDictionary(
                    intent => intent,
                    intent => SelectConditionActor(
                        intent,
                        individuals,
                        battle.Session,
                        archetypes));

        foreach (EnemyTacticalIntentKind expected in Enum.GetValues(
                     typeof(EnemyTacticalIntentKind)))
        {
            OffenseBattlePersistenceState row = CloneBattleState(baseline);
            EnemyIndividualSaveData selected = conditionActors[expected];
            Require(selected != null,
                "no authored active enemy can exercise condition row " + expected);
            if (selected == null) continue;

            if (!PrepareConditionState(
                    row,
                    selected,
                    allyId,
                    expected,
                    individualById,
                    archetypes,
                    out string conditionDetail))
            {
                Require(false, expected + " condition fixture invalid: "
                    + conditionDetail);
                continue;
            }

            int observedBefore = observed.Count;
            bool restored = battle.TryRestoreBattle(
                expedition,
                row,
                out string restoreMessage);
            Require(restored,
                expected + " production battle restore failed: " + restoreMessage);
            if (!restored) continue;
            CaptureTrace(trace);

            OffenseEnemyTacticalExecutionTrace[] emitted = observed
                .Skip(observedBefore)
                .Where(value => string.Equals(
                    value.ActorId,
                    selected.characterId,
                    StringComparison.Ordinal))
                .ToArray();
            Require(emitted.Length == 1,
                expected + " row emitted " + emitted.Length
                + " owned tactical terminals instead of exactly one");
            if (emitted.Length != 1) continue;
            OffenseEnemyTacticalExecutionTrace terminal = emitted[0];
            Require(terminal.Intent == expected,
                expected + " condition selected " + terminal.Intent
                + " instead; " + conditionDetail);
            Require(terminal.Accepted
                    && terminal.CommandId > 0
                    && !string.IsNullOrWhiteSpace(terminal.TerminalMessage)
                    && IntentMapsToCommand(terminal.Intent, terminal.Command),
                expected + " row did not reach mapped command terminal");
            if (terminal.Intent == expected
                && terminal.Accepted
                && terminal.CommandId > 0
                && !string.IsNullOrWhiteSpace(terminal.TerminalMessage)
                && IntentMapsToCommand(terminal.Intent, terminal.Command))
            {
                evidence.Add("condition-row:" + expected
                    + "; actor=" + selected.enemyArchetypeId
                    + "; command=" + terminal.Command
                    + "; commandId=" + terminal.CommandId
                    + "; authority=TryRestoreBattle->RunEnemyTurns->trace"
                    + "; fixture=" + conditionDetail);
            }
        }
    }

    private static EnemyIndividualSaveData SelectConditionActor(
        EnemyTacticalIntentKind intent,
        IReadOnlyList<EnemyIndividualSaveData> individuals,
        OffenseBattleSession session,
        IEnemyArchetypeCatalog archetypes)
    {
        IEnumerable<EnemyIndividualSaveData> candidates = individuals
            .Where(value => value != null
                && session?.FindCombatant(value.characterId) is
                    { IsDead: false, IsDowned: false });
        return intent switch
        {
            EnemyTacticalIntentKind.Move => candidates
                // A melee combatant in a rear slot has a naturally invalid
                // basic-attack preview and therefore exercises production
                // Advance without mutating the selector. Ranged fallback is
                // retained for authored encounters that contain no melee unit.
                .OrderBy(value => session.FindCombatant(value.characterId)
                    ?.Weapon?.IsRanged == true ? 1 : 0)
                .ThenBy(value => archetypes.Require(value.enemyArchetypeId)
                    .tacticalProfile.protectWeight)
                .ThenBy(value => value.characterId, StringComparer.Ordinal)
                .FirstOrDefault(),
            EnemyTacticalIntentKind.Protect => candidates
                .OrderByDescending(value => archetypes.Require(
                    value.enemyArchetypeId).tacticalProfile.protectWeight)
                .ThenBy(value => value.characterId, StringComparer.Ordinal)
                .FirstOrDefault(),
            EnemyTacticalIntentKind.UseAbility => candidates
                .Where(value => archetypes.Require(value.enemyArchetypeId)
                    .abilityIds.Count > 0)
                .OrderByDescending(value => archetypes.Require(
                    value.enemyArchetypeId).tacticalProfile.abilityWeight)
                .ThenBy(value => value.characterId, StringComparer.Ordinal)
                .FirstOrDefault(),
            EnemyTacticalIntentKind.Retreat => candidates
                .Where(value => archetypes.Require(value.enemyArchetypeId)
                    .tacticalProfile.retreatWeight > 0f)
                .OrderBy(value => archetypes.Require(value.enemyArchetypeId)
                    .tacticalProfile.protectWeight)
                .ThenByDescending(value => archetypes.Require(
                    value.enemyArchetypeId).tacticalProfile.retreatWeight)
                .ThenBy(value => value.characterId, StringComparer.Ordinal)
                .FirstOrDefault(),
            _ => candidates
                // Raw attack weight is insufficient: Protect is compared to
                // Attack by the production selector. Prefer the authored actor
                // with the greatest attack-vs-protect margin.
                .OrderByDescending(value =>
                    archetypes.Require(value.enemyArchetypeId)
                        .tacticalProfile.attackWeight
                    - archetypes.Require(value.enemyArchetypeId)
                        .tacticalProfile.protectWeight)
                .ThenBy(value => value.characterId, StringComparer.Ordinal)
                .FirstOrDefault()
        };
    }

    private static bool PrepareConditionState(
        OffenseBattlePersistenceState state,
        EnemyIndividualSaveData selected,
        string allyId,
        EnemyTacticalIntentKind intent,
        IReadOnlyDictionary<string, EnemyIndividualSaveData> individualById,
        IEnemyArchetypeCatalog archetypes,
        out string detail)
    {
        detail = string.Empty;
        if (state == null || selected == null
            || state.combatants == null || state.combatants.Count == 0)
            return false;
        OffenseBattleCombatantPersistenceState actor = state.combatants
            .FirstOrDefault(value => value != null
                && string.Equals(value.persistentId, selected.characterId,
                    StringComparison.Ordinal));
        if (actor == null) return false;

        foreach (OffenseBattleCombatantPersistenceState combatant in
                 state.combatants.Where(value => value != null))
        {
            combatant.currentHealth = Mathf.Max(1f, combatant.maxHealth);
            combatant.totalDamageTaken = 0f;
            combatant.suppression = 0f;
            combatant.bloodLoss = 0f;
            combatant.statuses?.Clear();
            foreach (CharacterBodyPartHealthState part in
                     combatant.bodyParts ?? new List<CharacterBodyPartHealthState>())
                part.currentHealth = part.maxHealth;

            if (individualById.TryGetValue(
                    combatant.persistentId,
                    out EnemyIndividualSaveData enemy))
            {
                EnemyArchetypeDefinitionSO definition =
                    archetypes.Require(enemy.enemyArchetypeId);
                combatant.cooldowns = definition.abilityIds
                    .OrderBy(value => value, StringComparer.Ordinal)
                    .Select(value => new OffenseBattleCooldownPersistenceState
                    {
                        abilityId = value,
                        remainingTurns = 99
                    })
                    .ToList();
            }
            else
            {
                // Large health keeps a condition row from accidentally ending
                // the real expedition while still preserving a valid living
                // production opponent for the enemy decision.
                combatant.maxHealth = Mathf.Max(10000f, combatant.maxHealth);
                combatant.currentHealth = combatant.maxHealth;
            }
        }

        EnemyArchetypeDefinitionSO archetype =
            archetypes.Require(selected.enemyArchetypeId);
        EnemyTacticalProfile profile = archetype.tacticalProfile;
        state.outcome = OffenseBattleOutcome.InProgress;
        state.roundNumber = Mathf.Max(1, state.roundNumber);
        state.currentOrderIndex = 0;
        state.initiativeOrder = new[] { selected.characterId, allyId }
            .Concat(state.combatants
                .Where(value => value != null
                    && !string.Equals(value.persistentId, selected.characterId,
                        StringComparison.Ordinal)
                    && !string.Equals(value.persistentId, allyId,
                        StringComparison.Ordinal))
                .Select(value => value.persistentId)
                .OrderBy(value => value, StringComparer.Ordinal))
            .ToList();

        switch (intent)
        {
            case EnemyTacticalIntentKind.Attack:
            {
                actor.formation = OffenseFormationSlot.Front;
                // The production utility includes expectedDamage/currentHealth.
                // A wounded but body-functional authored opponent makes Attack
                // dominate even for profiles whose Protect weight is higher.
                OffenseBattleCombatantPersistenceState attackTarget =
                    state.combatants.FirstOrDefault(value => value != null
                        && string.Equals(value.persistentId, allyId,
                            StringComparison.Ordinal));
                if (attackTarget != null)
                {
                    attackTarget.currentHealth = 1f;
                    attackTarget.formation = OffenseFormationSlot.Front;
                }
                break;
            }
            case EnemyTacticalIntentKind.Move:
                actor.formation = OffenseFormationSlot.Rear;
                actor.attack = 0f;
                actor.strength = 0f;
                actor.shooting = 0f;
                break;
            case EnemyTacticalIntentKind.Protect:
                actor.formation = OffenseFormationSlot.Front;
                actor.attack = 0f;
                actor.strength = 0f;
                actor.shooting = 0f;
                actor.currentHealth = actor.maxHealth
                    * Mathf.Clamp(profile.retreatHealthFraction + 0.01f, 0.09f, 0.24f);
                break;
            case EnemyTacticalIntentKind.UseAbility:
                actor.formation = OffenseFormationSlot.Front;
                actor.attack = 0f;
                actor.strength = 0f;
                actor.shooting = 0f;
                actor.currentHealth = actor.maxHealth * 0.5f;
                actor.cooldowns.Clear();
                break;
            case EnemyTacticalIntentKind.Retreat:
                actor.formation = OffenseFormationSlot.Front;
                actor.attack = 0f;
                actor.strength = 0f;
                actor.shooting = 0f;
                actor.currentHealth = actor.maxHealth
                    * Mathf.Max(0.01f, profile.retreatHealthFraction * 0.5f);
                break;
        }

        detail = "health=" + actor.currentHealth.ToString("0.##")
            + "/" + actor.maxHealth.ToString("0.##")
            + "; formation=" + actor.formation
            + "; weights=" + profile.attackWeight + "/"
            + profile.protectWeight + "/" + profile.abilityWeight + "/"
            + profile.retreatWeight
            + "; cooldowns=" + actor.cooldowns.Count
            + (intent == EnemyTacticalIntentKind.Retreat
                ? "; retreatUtility="
                    + (profile.retreatWeight
                        * (1f - actor.currentHealth / actor.maxHealth)
                        * 3f).ToString("0.###")
                    + "; selfOnlyProtectUtility="
                    + (profile.protectWeight
                        + (1f - actor.currentHealth / actor.maxHealth)
                        * 4f).ToString("0.###")
                : string.Empty);
        return true;
    }

    private static OffenseBattlePersistenceState CloneBattleState(
        OffenseBattlePersistenceState source) =>
        JsonUtility.FromJson<OffenseBattlePersistenceState>(
            JsonUtility.ToJson(source));

    private bool RunOnePlayerTurn(IOffenseBattleRuntime battle)
    {
        battle.AdvanceToPlayerDecision();
        OffenseBattleSession session = battle.Session;
        if (session == null || session.IsComplete) return true;
        if (session.CurrentActor == null
            || session.CurrentActor.Team != OffenseBattleTeam.Allies)
            return true;
        OffenseBattleCombatant actor = session.CurrentActor;
        OffenseBattleCombatant[] enemies = session.Combatants
            .Where(value => value.Team == OffenseBattleTeam.Enemies
                && !value.IsDead && !value.IsDowned)
            .ToArray();
        bool hasForwardEnemy = enemies.Any(value =>
            value.Formation != OffenseFormationSlot.Rear);
        OffenseBattleCombatant target = enemies
            .Where(value => session.PreviewBasicAttack(actor, value).Valid
                && (actor.Weapon?.IsRanged == true
                    || !hasForwardEnemy
                    || value.Formation != OffenseFormationSlot.Rear))
            .OrderBy(value => value.CurrentHealth)
            .ThenBy(value => value.PersistentId, StringComparer.Ordinal)
            .FirstOrDefault();
        OffenseBattleActionType action;
        string targetId;
        if (target != null)
        {
            action = OffenseBattleActionType.BasicAttack;
            targetId = target.PersistentId;
        }
        else if (actor.Formation != OffenseFormationSlot.Front)
        {
            action = OffenseBattleActionType.Advance;
            targetId = actor.PersistentId;
        }
        else
        {
            action = OffenseBattleActionType.Guard;
            targetId = actor.PersistentId;
        }

        bool accepted = battle.TryIssuePlayerCommand(
                action,
                targetId,
                string.Empty,
                out OffenseBattleCommandResult result);
        Require(accepted, "player command rejected once: " + result?.Message);
        return accepted;
    }

    private void CaptureTrace(IOffenseEnemyTacticalTraceQuery query)
    {
        foreach (OffenseEnemyTacticalExecutionTrace entry in query.EnemyTacticalTrace)
        {
            if (observed.Any(value => value.BattleId == entry.BattleId
                && value.Sequence == entry.Sequence))
                continue;
            observed.Add(entry);
            evidence.Add($"enemy:{entry.Intent}->{entry.Command}:"
                + $"accepted={entry.Accepted}:terminal={entry.TerminalMessage}");
        }
    }

    private static bool IntentMapsToCommand(
        EnemyTacticalIntentKind intent,
        OffenseBattleActionType command)
    {
        return intent switch
        {
            EnemyTacticalIntentKind.Attack => command == OffenseBattleActionType.BasicAttack,
            EnemyTacticalIntentKind.Move => command == OffenseBattleActionType.Advance,
            EnemyTacticalIntentKind.Protect => command == OffenseBattleActionType.Guard,
            EnemyTacticalIntentKind.UseAbility => command == OffenseBattleActionType.Ability,
            EnemyTacticalIntentKind.Retreat => command == OffenseBattleActionType.Retreat,
            _ => false
        };
    }

    private void Require(bool condition, string failure)
    {
        if (!condition) failures.Add(failure);
    }

    private void WriteReport(string result, string phase)
    {
        List<string> lines = new List<string>
        {
            "# Offense Tactical Journey PlayMode Verification",
            "result=" + result,
            "scope=production-expedition-runtime+production-battle-runtime+owned-tactical-trace",
            "utc=" + DateTime.UtcNow.ToString("O"),
            "verifierRevision=" + VerifierRevision,
            "phase=" + phase,
            "observed=" + observed.Count
        };
        lines.AddRange(evidence.Select(value => "PASS\t" + value));
        lines.AddRange(failures.Select(value => "FAIL\t" + value));
        File.WriteAllLines(OffenseTacticalJourneyPlayModeVerifier.ReportPath, lines);
    }
}
#endif
