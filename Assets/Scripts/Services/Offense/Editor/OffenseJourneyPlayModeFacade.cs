using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DungeonStory.Foundation;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using VContainer;

[InitializeOnLoad]
public static class OffenseJourneyPlayModeFacade
{
    public const string ReportPath =
        "Artifacts/QA/offense-journey-playmode.txt";
    public const string RequestPath =
        "Temp/offense-journey-playmode.request";
    private const string GameplayScenePath = "Assets/Scenes/GameplayScene.unity";
    private static readonly List<UnityEngine.Object> CreatedObjects = new List<UnityEngine.Object>();
    private static readonly List<IDisposable> ActiveSubscriptions = new List<IDisposable>();
    private static bool runnerCreated;

    static OffenseJourneyPlayModeFacade()
    {
        EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
    }

    [MenuItem("DungeonStory/Debug/QA/Run Strategic Offense Journey PlayMode Verification")]
    public static void RequestRun()
    {
        runnerCreated = false;
        Directory.CreateDirectory("Temp");
        File.WriteAllText(RequestPath, DateTime.UtcNow.ToString("O"));
        if (EditorApplication.isPlaying)
        {
            StartRunner(exitPlayMode: false);
            return;
        }

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
    private static void BootstrapPendingRun()
    {
        if (File.Exists(RequestPath))
        {
            StartRunner(exitPlayMode: true);
        }
    }

    private static void OnPlayModeStateChanged(PlayModeStateChange change)
    {
        if (change == PlayModeStateChange.EnteredEditMode)
        {
            runnerCreated = false;
            return;
        }

        if (change == PlayModeStateChange.EnteredPlayMode
            && File.Exists(RequestPath))
        {
            StartRunner(exitPlayMode: true);
        }
    }

    private static void StartRunner(bool exitPlayMode)
    {
        OffenseJourneyFullCampaignPlayModeRunner existing =
            UnityEngine.Object.FindFirstObjectByType<
                OffenseJourneyFullCampaignPlayModeRunner>();
        if (existing != null)
        {
            existing.ExitPlayModeOnCompletion |= exitPlayMode;
            runnerCreated = true;
            if (File.Exists(RequestPath)) File.Delete(RequestPath);
            return;
        }
        if (runnerCreated) return;

        GameObject runnerObject = new GameObject(
            "Offense Journey Full Campaign PlayMode Runner");
        UnityEngine.Object.DontDestroyOnLoad(runnerObject);
        OffenseJourneyFullCampaignPlayModeRunner runner =
            runnerObject.AddComponent<OffenseJourneyFullCampaignPlayModeRunner>();
        if (runner == null)
        {
            UnityEngine.Object.Destroy(runnerObject);
            return;
        }

        runner.ExitPlayModeOnCompletion = exitPlayMode;
        runnerCreated = true;
        if (File.Exists(RequestPath)) File.Delete(RequestPath);
    }

    public static void Cleanup()
    {
        for (int index = ActiveSubscriptions.Count - 1; index >= 0; index--)
        {
            ActiveSubscriptions[index]?.Dispose();
        }
        ActiveSubscriptions.Clear();
        for (int index = CreatedObjects.Count - 1; index >= 0; index--)
        {
            UnityEngine.Object value = CreatedObjects[index];
            if (value == null)
            {
                continue;
            }
            if (Application.isPlaying)
            {
                UnityEngine.Object.Destroy(value);
            }
            else
            {
                UnityEngine.Object.DestroyImmediate(value);
            }
        }
        CreatedObjects.Clear();
    }

    public static string Setup()
    {
        if (!Application.isPlaying) return "FAIL: PlayMode가 아닙니다.";
        OffenseExpeditionRuntime runtime = UnityEngine.Object.FindFirstObjectByType<OffenseExpeditionRuntime>();
        OffenseWorldMapRuntime worldMap = UnityEngine.Object.FindFirstObjectByType<OffenseWorldMapRuntime>();
        if (runtime == null || worldMap == null) return "FAIL: 오펜스 런타임이 없습니다.";

        if (runtime.ActiveExpeditions.Count == 0)
        {
            OffenseTargetDefinition target = worldMap.TargetDefinitions
                .Where(value => value != null && value.campaignOrder == 1)
                .OrderBy(value => value.id, StringComparer.Ordinal)
                .FirstOrDefault();
            if (target == null) return "FAIL: 첫 원정 대상이 없습니다.";

            worldMap.Campaign.PublishRestoreCandidate(
                worldMap.Campaign.BuildRestoreCandidate(
                    new DungeonOffenseCampaignSaveData
                    {
                        reconLevel = 1,
                        selectedTargetId = target.id,
                        knownTargetIds = new List<string> { target.id }
                    }));
            if (!worldMap.TrySelectTarget(target.id, out _, out string selectMessage))
            {
                return $"FAIL: {selectMessage}";
            }

            CharacterActor actor = runtime.GetAvailableMemberActors().FirstOrDefault()
                ?? CreateActor();
            if (!runtime.TryStartExpedition(
                target.id,
                new[] { actor },
                out _,
                out string startMessage))
            {
                return $"FAIL: {startMessage}";
            }
        }

        runtime.ShowExpeditionPanel();
        OffenseExpeditionRun expedition = runtime.ActiveExpeditions[0];
        return $"PASS: {expedition.Target.title}; phase={expedition.Phase}; next={expedition.GetAvailableRouteNodes().Count}";
    }

    public static string ClickButton(string labelPrefix)
    {
        Button button = UnityEngine.Object.FindObjectsByType<Button>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
            .Where(value => value != null && value.gameObject.activeInHierarchy && value.interactable)
            .FirstOrDefault(value => GetLabel(value).StartsWith(labelPrefix, StringComparison.Ordinal));
        if (button == null) return $"FAIL: '{labelPrefix}' 버튼이 없습니다.";
        EventSystem eventSystem = EventSystem.current;
        if (eventSystem == null) return "FAIL: EventSystem이 없습니다.";

        RectTransform rect = button.transform as RectTransform;
        Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(
            null,
            rect != null ? rect.TransformPoint(rect.rect.center) : button.transform.position);
        PointerEventData pointer = new PointerEventData(eventSystem)
        {
            button = PointerEventData.InputButton.Left,
            position = screenPoint,
            pointerPress = button.gameObject,
            pointerEnter = button.gameObject
        };
        ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerEnterHandler);
        ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerDownHandler);
        ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerUpHandler);
        ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerClickHandler);
        return $"PASS: clicked={GetLabel(button)}; {GetState()}";
    }

    public static string GetState()
    {
        OffenseExpeditionRuntime runtime = UnityEngine.Object.FindFirstObjectByType<OffenseExpeditionRuntime>();
        OffenseExpeditionRun expedition = runtime?.ActiveExpeditions.FirstOrDefault();
        if (expedition == null) return "active=none";
        return $"phase={expedition.Phase}; node={expedition.CurrentNodeId};"
            + $" completed={expedition.CompletedNodeIds.Count}; light={expedition.Light:0};"
            + $" stress={string.Join(",", expedition.MemberStates.Select(member => member.Stress.ToString("0")))}";
    }

    public static string RunFullCampaignThroughUi() =>
        RunFullCampaignThroughUi(Array.Empty<string>());

    internal static string RunFullCampaignThroughUi(
        IEnumerable<string> preflightEvidence)
    {
        List<string> evidence = preflightEvidence?
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .ToList()
            ?? new List<string>();
        string terminal;
        try
        {
            terminal = RunFullCampaignThroughUiCore(evidence);
        }
        catch (Exception exception)
        {
            terminal = "FAIL: unhandled " + exception.GetType().Name
                + ": " + exception.Message;
        }

        WriteReport(
            terminal.StartsWith("PASS:", StringComparison.Ordinal) ? "PASS" : "FAIL",
            terminal,
            evidence);
        return terminal;
    }

    internal static void WriteReport(
        string result,
        string terminal,
        IEnumerable<string> evidence)
    {
        List<string> report = new List<string>
        {
            "# Strategic offense journey production-live PlayMode verification",
            "result=" + (string.IsNullOrWhiteSpace(result) ? "FAIL" : result),
            "scope=production-strategic-ui-pointer+world-travel+decision+battle+return+reward-terminal",
            "utc=" + DateTime.UtcNow.ToString("O"),
            "authority=production UI pointer->OffenseExpeditionRuntime->IOffenseBattleRuntime->reward terminal"
        };
        if (evidence != null) report.AddRange(evidence);
        report.Add("terminal=" + (terminal ?? string.Empty));
        Directory.CreateDirectory(Path.GetDirectoryName(ReportPath)
            ?? "Artifacts/QA");
        File.WriteAllLines(ReportPath, report);
    }

    internal static IEnumerator RunStrategicJourneyThroughUi(
        DungeonRuntimeLifetimeScope scope,
        ICollection<string> evidence,
        Action<string> complete)
    {
        if (scope?.Container == null)
        {
            complete?.Invoke("FAIL: offense lifetime scope is missing.");
            yield break;
        }

        OffenseExpeditionRuntime expeditions =
            UnityEngine.Object.FindFirstObjectByType<OffenseExpeditionRuntime>();
        IOffensePanelService panels = scope.Container.Resolve<IOffensePanelService>();
        IOffenseWorldSimulation world =
            scope.Container.Resolve<IOffenseWorldSimulation>();
        IOffenseStrategicTargetService targets =
            scope.Container.Resolve<IOffenseStrategicTargetService>();
        IOffenseDecisionRuntime decisions =
            scope.Container.Resolve<IOffenseDecisionRuntime>();
        IOffenseBattleRuntime battle =
            scope.Container.Resolve<IOffenseBattleRuntime>();
        IOffenseBattleDirector director =
            scope.Container.Resolve<IOffenseBattleDirector>();
        IOffenseTravelRuntime travel =
            scope.Container.Resolve<IOffenseTravelRuntime>();
        IGameEventBus events = scope.Container.Resolve<IGameEventBus>();
        OffenseRewardRuntime rewards =
            UnityEngine.Object.FindFirstObjectByType<OffenseRewardRuntime>();
        if (expeditions == null || panels == null || world == null
            || targets == null || decisions == null || battle == null
            || director == null || travel == null || events == null
            || rewards == null)
        {
            complete?.Invoke("FAIL: strategic offense authorities are missing.");
            yield break;
        }

        if (!EnsureExpeditionResearchPrerequisite(
                scope,
                out string researchEvidence,
                out string researchFailure))
        {
            complete?.Invoke("FAIL: " + researchFailure);
            yield break;
        }
        AddEvidence(evidence, "STRATEGIC_RESEARCH_PREREQUISITE",
            researchEvidence);
        yield return null;

        CharacterActor[] available = expeditions.GetAvailableMemberActors()
            .Where(actor => actor != null
                && actor.CurrentLifecycleState == CharacterLifecycleState.Active)
            .Take(5)
            .ToArray();
        OffenseWorldSiteStateData selectedSite = null;
        OffenseTargetDefinition selectedTarget = null;
        foreach (OffenseWorldSiteStateData site in world.Sites
                     .Where(site => site != null
                         && site.IsActive
                         && site.state == OffenseWorldSiteState.Revealed)
                     .OrderBy(site => site.strength)
                     .ThenBy(site => world.GetMinimumStepDistance(
                         world.DungeonCoord,
                         site.Coord))
                     .ThenBy(site => site.siteId, StringComparer.Ordinal))
        {
            if (targets.TryCreateTarget(
                    site.siteId,
                    out OffenseTargetDefinition candidate,
                    out _)
                && candidate != null
                && available.Length >= candidate.requiredMembers)
            {
                selectedSite = site;
                selectedTarget = candidate;
                break;
            }
        }

        if (selectedSite == null || selectedTarget == null)
        {
            complete?.Invoke("FAIL: fresh strategic map has no revealed, "
                + "staffed expedition site. revealed="
                + string.Join(",", world.Sites
                    .Where(site => site != null
                        && site.state == OffenseWorldSiteState.Revealed)
                    .Select(site => site.siteId))
                + $"; available={available.Length}");
            yield break;
        }

        int historyBefore = expeditions.ResultHistory.Count;
        int rewardAuthorityBefore = GetRewardAuthorityTotal(rewards.State);
        int rewardEvents = 0;
        string rewardEventExpeditionId = string.Empty;
        IDisposable rewardSubscription = events.Subscribe<OffenseRewardGrantedEvent>(
            gameEvent =>
            {
                rewardEvents++;
                rewardEventExpeditionId =
                    gameEvent.expeditionResult?.expeditionId ?? string.Empty;
            });
        ActiveSubscriptions.Add(rewardSubscription);
        Dictionary<string, (int Level, int Experience)> progressionBefore =
            available.ToDictionary(
            actor => actor.Identity.PersistentId,
            actor => (
                actor.Progression?.Level ?? 0,
                actor.Progression?.CurrentExperience ?? 0),
            StringComparer.Ordinal);
        panels.ShowWorldMap();
        yield return null;
        Canvas.ForceUpdateCanvases();
        if (!ClickButtonByName(
                $"Hex_{selectedSite.q}_{selectedSite.r}"))
        {
            complete?.Invoke("FAIL: revealed strategic site was not pointer-clickable. "
                + $"site={selectedSite.siteId}; title={selectedSite.displayName}; buttons="
                + DescribeActiveOffenseButtons());
            yield break;
        }
        AddEvidence(evidence, "STRATEGIC_SITE_POINTER",
            $"site={selectedSite.siteId}; title={selectedSite.displayName}; "
            + $"strength={selectedSite.strength}; requiredMembers={selectedTarget.requiredMembers}; "
            + $"requiredPower={selectedTarget.requiredPower:0.##}; "
            + $"distance={world.GetMinimumStepDistance(world.DungeonCoord, selectedSite.Coord)}");
        yield return null;

        foreach (CharacterActor actor in available)
        {
            string actorName = actor.Identity?.DisplayName ?? actor.name;
            if (!ClickButtonContaining(actorName))
            {
                complete?.Invoke("FAIL: strategic party member was not pointer-clickable. "
                    + $"actor={actorName}; buttons={DescribeActiveOffenseButtons()}");
                yield break;
            }
            yield return null;
        }

        IOffensePreparationService preparation =
            scope.Container.Resolve<IOffensePreparationService>();
        OffensePreparationSnapshot preparationSnapshot = preparation?.Evaluate();
        int rationAvailable = preparationSnapshot?.GetAvailable(
            OffenseSupplyType.Rations) ?? 0;
        int supplyCapacity = preparationSnapshot?.Preparation?.SupplyCapacity ?? 0;
        if (rationAvailable <= 0 || supplyCapacity <= 0)
        {
            complete?.Invoke("FAIL: minimum lawful physical expedition supply is unavailable. "
                + $"rations={rationAvailable}; capacity={supplyCapacity}; detail="
                + DescribeActiveOffensePanelText());
            yield break;
        }
        string rationLabel = $"{OffenseSupplyCatalog.GetDisplayName(OffenseSupplyType.Rations)} "
            + $"0/{rationAvailable}";
        if (!ClickButtonExact(rationLabel))
        {
            complete?.Invoke("FAIL: minimum ration supply was not pointer-clickable. "
                + $"expected={rationLabel}; buttons={DescribeActiveOffenseButtons()}; detail="
                + DescribeActiveOffensePanelText());
            yield break;
        }
        AddEvidence(evidence, "STRATEGIC_PHYSICAL_SUPPLY_POINTER",
            $"type={OffenseSupplyType.Rations}; selected=1; available={rationAvailable}; "
            + $"capacity={supplyCapacity}");
        yield return null;

        if (!ClickButtonExact("선택 거점으로 출정"))
        {
            complete?.Invoke("FAIL: strategic departure command was not pointer-clickable. "
                + "buttons=" + DescribeActiveOffenseButtons());
            yield break;
        }
        AddEvidence(evidence, "STRATEGIC_DEPARTURE_POINTER",
            $"site={selectedSite.siteId}; members={available.Length}");
        yield return null;

        float startDeadline = Time.realtimeSinceStartup + 20f;
        OffenseExpeditionRun expedition = null;
        while (Time.realtimeSinceStartup < startDeadline)
        {
            expedition = expeditions.ActiveExpeditions.FirstOrDefault(run =>
                run != null
                && string.Equals(run.WorldSiteId,
                    selectedSite.siteId,
                    StringComparison.Ordinal));
            if (expedition != null) break;
            yield return null;
        }
        if (expedition == null)
        {
            complete?.Invoke("FAIL: strategic pointer command did not create an expedition. "
                + "buttons=" + DescribeActiveOffenseButtons()
                + "; detail=" + DescribeActiveOffensePanelText());
            yield break;
        }

        string expeditionId = expedition.ExpeditionId;
        if (!expedition.UsesWorldTravel)
        {
            ReleaseSubscription(rewardSubscription);
            complete?.Invoke("FAIL: strategic pointer created a non-world-travel expedition.");
            yield break;
        }
        AddEvidence(evidence, "STRATEGIC_REAL_PARTY_WORLD_TRAVEL",
            $"expedition={expeditionId}; usesWorldTravel=True; "
            + $"members={expedition.MemberActors.Count}");
        int decisionCommands = 0;
        int battleCommands = 0;
        bool returnCommandIssued = false;
        bool departureObserved = false;
        bool battleTerminalObserved = false;
        bool battleWasActive = false;
        bool decisionDetourIssued = false;
        float battleEnemyDamageObserved = 0f;
        float battleAllyDamageObserved = 0f;
        int consecutiveNoEffectBattleTurns = 0;
        float deadline = Time.realtimeSinceStartup + 300f;
        while (Time.realtimeSinceStartup < deadline
            && expeditions.ActiveExpeditions.Any(run =>
                run != null
                && string.Equals(run.ExpeditionId,
                    expeditionId,
                    StringComparison.Ordinal)))
        {
            expedition = expeditions.ActiveExpeditions.First(run =>
                run != null
                && string.Equals(run.ExpeditionId,
                    expeditionId,
                    StringComparison.Ordinal));
            if (expedition.DepartureCompleted && !departureObserved)
            {
                departureObserved = true;
                AddEvidence(evidence, "STRATEGIC_DEPARTURE_TERMINAL",
                    $"expedition={expeditionId}; departureCompleted=True");
            }
            if (decisions.TryGetActiveDecision(
                    expeditionId,
                    out OffenseDecisionView decision))
            {
                if (decision.choices == null || decision.choices.Count == 0)
                {
                    complete?.Invoke("FAIL: strategic decision was not pointer-clickable. "
                        + $"decision={decision.cardId}; buttons={DescribeActiveOffenseButtons()}");
                    yield break;
                }
                OffenseDecisionStateData activeDecisionState = decisions.Capture()
                    .FirstOrDefault(state => state != null
                        && string.Equals(
                            state.expeditionId,
                            expeditionId,
                            StringComparison.Ordinal));
                if (activeDecisionState == null)
                {
                    complete?.Invoke("FAIL: strategic decision view has no matching "
                        + $"authority state. expedition={expeditionId}; "
                        + $"decision={decision.cardId}");
                    yield break;
                }
                int decisionSequence = activeDecisionState.sequence;
                OffenseDecisionChoiceView resolvedChoice = default;
                bool resolvedChoiceFound = false;
                List<string> rejectedChoices = new List<string>();
                foreach (OffenseDecisionChoiceView choice in decision.choices)
                {
                    if (!ClickButtonContaining(choice.Label))
                    {
                        rejectedChoices.Add(choice.ChoiceId + ":not-pointer-clickable");
                        continue;
                    }

                    yield return null;
                    bool hasRemainingActiveDecision = decisions.TryGetActiveDecision(
                        expeditionId,
                        out _);
                    OffenseDecisionStateData remainingDecisionState =
                        hasRemainingActiveDecision
                            ? decisions.Capture().FirstOrDefault(state => state != null
                                && string.Equals(
                                    state.expeditionId,
                                    expeditionId,
                                    StringComparison.Ordinal))
                            : null;
                    bool sameDecisionStillActive = hasRemainingActiveDecision
                        && remainingDecisionState != null
                        && remainingDecisionState.sequence == decisionSequence;
                    if (!sameDecisionStillActive)
                    {
                        resolvedChoice = choice;
                        resolvedChoiceFound = true;
                        break;
                    }

                    rejectedChoices.Add(choice.ChoiceId + ":domain-rejected");
                }
                if (!resolvedChoiceFound)
                {
                    complete?.Invoke("FAIL: no strategic decision choice was accepted "
                        + "through the production UI. "
                        + $"decision={decision.cardId}; attempts="
                        + string.Join(",", rejectedChoices)
                        + "; detail=" + DescribeActiveOffensePanelText());
                    yield break;
                }

                decisionCommands++;
                AddEvidence(evidence, "STRATEGIC_DECISION_POINTER",
                    $"decision={decision.cardId}; sequence={decisionSequence}; "
                    + $"choice={resolvedChoice.ChoiceId}; "
                    + "rejected=" + string.Join(",", rejectedChoices));
                continue;
            }

            if (battle.HasActiveBattle && director.State != null)
            {
                battleWasActive = true;
                StrategicBattlePointerTurnResult pointerTurn = null;
                yield return IssueStrategicBattlePointerTurn(
                    battle,
                    director,
                    result => pointerTurn = result);
                if (pointerTurn == null || !pointerTurn.Success)
                {
                    complete?.Invoke("FAIL: "
                        + (pointerTurn?.Failure
                            ?? "strategic battle pointer turn returned no result")
                        + "; buttons=" + DescribeActiveOffenseButtons());
                    yield break;
                }
                battleCommands++;
                battleEnemyDamageObserved += pointerTurn.EnemyDamage;
                battleAllyDamageObserved += pointerTurn.AllyDamage;
                if (!pointerTurn.BattleTerminal
                    && pointerTurn.CommandIdAfter
                        <= pointerTurn.CommandIdBefore)
                {
                    complete?.Invoke("FAIL: strategic battle resolved a pointer turn "
                        + "without accepting any typed combat command. "
                        + $"turn={pointerTurn.Turn}; commandId="
                        + $"{pointerTurn.CommandIdBefore}"
                        + $"->{pointerTurn.CommandIdAfter}; "
                        + $"resolution={pointerTurn.ResolutionSummary}");
                    yield break;
                }
                if (!pointerTurn.BattleTerminal
                    && pointerTurn.EnemyDamage <= 0f
                    && pointerTurn.AllyDamage <= 0f
                    && !pointerTurn.FormationChanged)
                {
                    consecutiveNoEffectBattleTurns++;
                }
                else
                {
                    consecutiveNoEffectBattleTurns = 0;
                }
                AddEvidence(evidence, "STRATEGIC_BATTLE_TURN",
                    $"turn={pointerTurn.Turn}; queued={pointerTurn.QueuedCommands}; "
                    + $"cards={pointerTurn.CardSummary}; "
                    + $"resolution={pointerTurn.ResolutionSummary}; "
                    + $"round={pointerTurn.RoundBefore}->{pointerTurn.RoundAfter}; "
                    + $"commandId={pointerTurn.CommandIdBefore}"
                    + $"->{pointerTurn.CommandIdAfter}; "
                    + $"enemyHp={pointerTurn.EnemyHealthBefore:0.##}"
                    + $"->{pointerTurn.EnemyHealthAfter:0.##}; "
                    + $"allyHp={pointerTurn.AllyHealthBefore:0.##}"
                    + $"->{pointerTurn.AllyHealthAfter:0.##}; "
                    + $"formationChanged={pointerTurn.FormationChanged}; "
                    + $"terminal={pointerTurn.BattleTerminal}");
                if (battleCommands == 1)
                {
                    AddEvidence(evidence, "STRATEGIC_BATTLE_COMMAND_POINTER",
                        $"expedition={expeditionId}; command=all-decks-card-intent-execute; "
                        + $"queued={pointerTurn.QueuedCommands}; cards={pointerTurn.CardSummary}");
                }
                if (consecutiveNoEffectBattleTurns >= 3)
                {
                    complete?.Invoke("FAIL: strategic battle made no gameplay progress "
                        + "for three consecutive command turns. "
                        + $"turn={pointerTurn.Turn}; round={pointerTurn.RoundBefore}"
                        + $"->{pointerTurn.RoundAfter}; resolution={pointerTurn.ResolutionSummary}; "
                        + "formation=" + DescribeBattleFormation(battle.Session));
                    yield break;
                }
                continue;
            }
            if (battleWasActive && !battle.HasActiveBattle
                && expedition.WorldObjectiveCompleted
                && !battleTerminalObserved)
            {
                battleTerminalObserved = true;
                AddEvidence(evidence, "STRATEGIC_BATTLE_TERMINAL",
                    $"expedition={expeditionId}; objectiveCompleted=True; "
                    + $"enemyDamage={battleEnemyDamageObserved:0.##}; "
                    + $"allyDamage={battleAllyDamageObserved:0.##}");
            }

            if (expedition.WorldObjectiveCompleted
                && decisionCommands == 0
                && !decisionDetourIssued)
            {
                if (!travel.TryGetState(
                        expeditionId,
                        out OffenseTravelStateData detourState)
                    || !TryFindDecisionDetour(
                        world,
                        expeditionId,
                        detourState,
                        out OffenseHexCoord detour))
                {
                    complete?.Invoke("FAIL: no lawful strategic detour can exercise "
                        + "the production travel-decision branch.");
                    yield break;
                }
                panels.ShowWorldMap();
                yield return null;
                if (!ClickButtonByName($"Hex_{detour.Q}_{detour.R}"))
                {
                    complete?.Invoke("FAIL: decision detour hex was not pointer-clickable. "
                        + $"coord=({detour.Q},{detour.R}); buttons="
                        + DescribeActiveOffenseButtons());
                    yield break;
                }
                yield return null;
                if (!ClickButtonExact("선택한 칸으로 이동"))
                {
                    complete?.Invoke("FAIL: decision detour movement was not pointer-clickable. "
                        + "buttons=" + DescribeActiveOffenseButtons());
                    yield break;
                }
                decisionDetourIssued = true;
                AddEvidence(evidence, "STRATEGIC_DECISION_DETOUR_POINTER",
                    $"coord=({detour.Q},{detour.R}); eventSequence={detourState.eventSequence}");
                yield return null;
                continue;
            }

            if (expedition.WorldObjectiveCompleted
                && decisionDetourIssued
                && decisionCommands == 0
                && travel.TryGetState(
                    expeditionId,
                    out OffenseTravelStateData exhaustedDetour)
                && (exhaustedDetour.remainingPath == null
                    || exhaustedDetour.remainingPath.Count == 0))
            {
                complete?.Invoke("FAIL: deterministic decision detour completed "
                    + "without publishing a travel decision.");
                yield break;
            }

            if (expedition.WorldObjectiveCompleted
                && !returnCommandIssued
                && (!decisionDetourIssued || decisionCommands > 0))
            {
                panels.ShowWorldMap();
                yield return null;
                if (!ClickButtonByName(
                        $"Hex_{world.DungeonCoord.Q}_{world.DungeonCoord.R}"))
                {
                    complete?.Invoke("FAIL: dungeon return hex was not pointer-clickable. "
                        + "buttons=" + DescribeActiveOffenseButtons());
                    yield break;
                }
                yield return null;
                if (!ClickButtonExact("선택한 칸으로 이동"))
                {
                    complete?.Invoke("FAIL: dungeon return command was not pointer-clickable. "
                        + "buttons=" + DescribeActiveOffenseButtons());
                    yield break;
                }
                returnCommandIssued = true;
                AddEvidence(evidence, "STRATEGIC_RETURN_POINTER",
                    "destination=dungeon");
                yield return null;
                continue;
            }

            yield return null;
        }

        if (expeditions.ActiveExpeditions.Any(run => run != null
            && string.Equals(run.ExpeditionId, expeditionId, StringComparison.Ordinal)))
        {
            string phase = expedition?.Phase.ToString() ?? "missing";
            string travelState = travel.TryGetState(
                    expeditionId,
                    out OffenseTravelStateData currentTravel)
                ? $"({currentTravel.currentQ},{currentTravel.currentR})->"
                    + $"({currentTravel.destinationQ},{currentTravel.destinationR});"
                    + $"remaining={currentTravel.remainingPath?.Count ?? 0}"
                : "missing";
            complete?.Invoke("FAIL: strategic journey timed out. "
                + $"phase={phase}; travel={travelState}; decisions={decisionCommands}; "
                + $"battleCommands={battleCommands}; return={returnCommandIssued}");
            yield break;
        }

        OffenseExpeditionResult result = null;
        float resultDeadline = Time.realtimeSinceStartup + 60f;
        while (Time.realtimeSinceStartup < resultDeadline && result == null)
        {
            result = expeditions.ResultHistory.FirstOrDefault(candidate =>
                candidate != null
                && string.Equals(candidate.expeditionId,
                    expeditionId,
                    StringComparison.Ordinal));
            if (result == null) yield return null;
        }
        ReleaseSubscription(rewardSubscription);
        if (result == null || !result.success)
        {
            complete?.Invoke("FAIL: strategic journey did not publish a successful result. "
                + $"history={historyBefore}->{expeditions.ResultHistory.Count}; "
                + $"result={(result == null ? "missing" : result.success.ToString())}");
            yield break;
        }
        if (!departureObserved || decisionCommands == 0
            || battleCommands == 0 || !battleTerminalObserved
            || battleEnemyDamageObserved <= 0f || !returnCommandIssued)
        {
            complete?.Invoke("FAIL: strategic journey skipped a required live stage. "
                + $"departure={departureObserved}; decisions={decisionCommands}; "
                + $"battleCommands={battleCommands}; battleTerminal={battleTerminalObserved}; "
                + $"enemyDamage={battleEnemyDamageObserved:0.##}; "
                + $"allyDamage={battleAllyDamageObserved:0.##}; "
                + $"return={returnCommandIssued}");
            yield break;
        }
        if (result.grantedRewards == null
            || result.grantedRewards.Count == 0
            || result.grantedRewards.Any(grant => grant == null || !grant.success))
        {
            complete?.Invoke("FAIL: strategic result reward conservation failed. "
                + $"grants={result.grantedRewards?.Count ?? -1}; "
                + $"failed={result.grantedRewards?.Count(grant => grant == null || !grant.success) ?? -1}");
            yield break;
        }
        int rewardAuthorityAfter = GetRewardAuthorityTotal(rewards.State);
        if (rewardEvents != 1
            || !string.Equals(
                rewardEventExpeditionId,
                expeditionId,
                StringComparison.Ordinal)
            || rewardAuthorityAfter <= rewardAuthorityBefore)
        {
            complete?.Invoke("FAIL: strategic reward event/authority delta mismatch. "
                + $"events={rewardEvents}; eventExpedition={rewardEventExpeditionId}; "
                + $"authority={rewardAuthorityBefore}->{rewardAuthorityAfter}");
            yield break;
        }

        int grown = available.Count(actor =>
            actor != null
            && progressionBefore.TryGetValue(
                actor.Identity.PersistentId,
                out (int Level, int Experience) before)
            && ((actor.Progression?.Level ?? 0) > before.Level
                || ((actor.Progression?.Level ?? 0) == before.Level
                    && (actor.Progression?.CurrentExperience ?? 0)
                        > before.Experience)));
        if (grown == 0)
        {
            complete?.Invoke("FAIL: successful strategic journey granted no participant growth.");
            yield break;
        }
        CharacterActor[] ownershipLeaks = available
            .Where(actor => actor != null && actor.IsOnExpedition)
            .ToArray();
        bool travelLeak = travel.TryGetState(expeditionId, out _);
        bool decisionLeak = decisions.TryGetActiveDecision(expeditionId, out _);
        bool directorLeak = director.State != null;
        bool battleLeak = battle.HasActiveBattle;
        if (ownershipLeaks.Length > 0
            || travelLeak
            || decisionLeak
            || directorLeak
            || battleLeak)
        {
            complete?.Invoke("FAIL: strategic journey left expedition ownership active. "
                + "actors=" + string.Join(",", ownershipLeaks.Select(actor =>
                    actor.Identity?.PersistentId ?? actor.name))
                + $"; travel={travelLeak}; decision={decisionLeak}; "
                + $"director={directorLeak}; battle={battleLeak}");
            yield break;
        }

        AddEvidence(evidence, "STRATEGIC_JOURNEY_TERMINAL",
            $"site={selectedSite.siteId}; success=True; decisions={decisionCommands}; "
            + $"battleCommands={battleCommands}; return={returnCommandIssued}");
        AddEvidence(evidence, "STRATEGIC_REWARD_HISTORY",
            $"history={historyBefore}->{expeditions.ResultHistory.Count}; "
            + $"grants={result.grantedRewards.Count}; rewardEvents={rewardEvents}; "
            + $"authority={rewardAuthorityBefore}->{rewardAuthorityAfter}; grown={grown}");
        AddEvidence(evidence, "STRATEGIC_OWNERSHIP_CLEAN",
            $"expedition={expeditionId}; active=False; memberLeaks=0; "
            + "travel=False; decision=False; director=False; battle=False");
        complete?.Invoke($"PASS: strategicSite={selectedSite.siteId}; "
            + $"decisions={decisionCommands}; battleCommands={battleCommands}; "
            + $"rewards={result.grantedRewards.Count}; grown={grown}");
    }

    private static int GetRewardAuthorityTotal(IOffenseRewardStateView state)
    {
        if (state == null) return 0;
        return state.MoneyEarned
            + state.StockGrantedByCategory.Values.Sum()
            + state.RareFacilityBuildingIds.Count
            + state.AcquiredBlueprintIds.Count;
    }

    private static bool EnsureExpeditionResearchPrerequisite(
        DungeonRuntimeLifetimeScope scope,
        out string evidence,
        out string failure)
    {
        evidence = string.Empty;
        failure = string.Empty;
        IDungeonSaveSectionRegistry sections =
            scope?.Container?.Resolve<IDungeonSaveSectionRegistry>();
        IBlueprintResearchStateService researchState =
            scope?.Container?.Resolve<IBlueprintResearchStateService>();
        if (sections == null || researchState == null)
        {
            failure = "official research save/state authority is missing.";
            return false;
        }
        if (OffenseExpeditionAccessRules.IsUnlocked(researchState.GetState()))
        {
            evidence = "already-completed:"
                + OffenseExpeditionAccessRules.RequiredResearchId;
            return true;
        }

        List<DungeonSaveSectionEnvelope> snapshot = sections.CaptureAll();
        DungeonSaveSectionEnvelope envelope = snapshot.FirstOrDefault(value =>
            value != null
            && string.Equals(
                value.sectionId,
                BlueprintResearchSaveSection.Id,
                StringComparison.Ordinal));
        DungeonResearchSaveData research = envelope != null
            ? JsonUtility.FromJson<DungeonResearchSaveData>(envelope.payloadJson)
            : null;
        if (research == null)
        {
            failure = "official research save payload is missing or invalid.";
            return false;
        }
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
        {
            research.activeProjectId = string.Empty;
        }
        envelope.payloadJson = JsonUtility.ToJson(research);
        DungeonGameRestoreReport report = new DungeonGameRestoreReport();
        if (!sections.RestoreAll(snapshot, report) || !report.Success)
        {
            failure = "official research prerequisite restore failed: "
                + string.Join(" | ", report.Errors);
            return false;
        }
        if (!OffenseExpeditionAccessRules.IsUnlocked(researchState.GetState()))
        {
            failure = "official research state did not publish expedition access.";
            return false;
        }
        evidence = "save-registry:"
            + OffenseExpeditionAccessRules.RequiredResearchId;
        return true;
    }

    private static bool TryFindDecisionDetour(
        IOffenseWorldSimulation world,
        string expeditionId,
        OffenseTravelStateData travel,
        out OffenseHexCoord destination)
    {
        destination = default;
        if (world == null || travel == null)
        {
            return false;
        }
        HashSet<OffenseHexCoord> occupied = world.Sites
            .Where(site => site != null && site.IsActive)
            .Select(site => site.Coord)
            .Concat(world.UrgentSites
                .Where(site => site != null && site.IsActive)
                .Select(site => site.Coord))
            .ToHashSet();
        foreach (OffenseHexTileState tile in world.Tiles
                     .Where(tile => tile != null
                         && !tile.blocked
                         && tile.Coord != world.DungeonCoord
                         && !occupied.Contains(tile.Coord))
                     .OrderByDescending(tile =>
                         travel.CurrentCoord.DistanceTo(tile.Coord))
                     .ThenBy(tile => tile.q)
                     .ThenBy(tile => tile.r))
        {
            if (!world.TryFindPath(
                    travel.CurrentCoord,
                    tile.Coord,
                    OffenseTravelProfile.Default,
                    out IReadOnlyList<OffenseHexCoord> path,
                    out _)
                || path == null
                || path.Count < 2)
            {
                continue;
            }
            for (int index = 0; index < path.Count - 1; index++)
            {
                OffenseHexCoord step = path[index];
                if (DeterministicTravelDecisionHash(
                        expeditionId,
                        travel.eventSequence + index,
                        step.Q,
                        step.R) % 100u < 32u)
                {
                    destination = tile.Coord;
                    return true;
                }
            }
        }
        return false;
    }

    private static uint DeterministicTravelDecisionHash(
        string expeditionId,
        int sequence,
        int q,
        int r)
    {
        unchecked
        {
            uint hash = 2166136261u;
            string value = expeditionId ?? string.Empty;
            for (int index = 0; index < value.Length; index++)
            {
                hash ^= value[index];
                hash *= 16777619u;
            }
            hash ^= (uint)sequence * 0x9E3779B9u;
            hash ^= (uint)q * 0x85EBCA6Bu;
            hash ^= (uint)r * 0xC2B2AE35u;
            hash ^= hash >> 16;
            return hash;
        }
    }

    private static void ReleaseSubscription(IDisposable subscription)
    {
        if (subscription == null) return;
        if (ActiveSubscriptions.Remove(subscription))
        {
            subscription.Dispose();
        }
    }

    private static IEnumerator IssueStrategicBattlePointerTurn(
        IOffenseBattleRuntime battle,
        IOffenseBattleDirector director,
        Action<StrategicBattlePointerTurnResult> complete)
    {
        OffenseBattleDirectorStateData state = director.State;
        OffenseBattleSession session = battle?.Session;
        if (state == null || session == null)
        {
            complete?.Invoke(StrategicBattlePointerTurnResult.Fail(
                "strategic battle director state or session is missing"));
            yield break;
        }

        int turn = state.turn;
        int roundBefore = session.RoundNumber;
        long commandIdBefore = session.LastProcessedCommandId;
        string formationBefore = DescribeBattleFormation(session);
        float allyHealthBefore = GetLivingTeamHealth(
            session,
            OffenseBattleTeam.Allies);
        float enemyHealthBefore = GetLivingTeamHealth(
            session,
            OffenseBattleTeam.Enemies);
        string[] availableDeckIds = state.decks
            .Where(deck => deck != null
                && deck.candidates != null
                && deck.candidates.Count > 0
                && session.FindCombatant(deck.characterId)?.CanTakeTurn == true)
            .Select(deck => deck.characterId)
            .OrderBy(characterId => characterId, StringComparer.Ordinal)
            .ToArray();
        if (availableDeckIds.Length == 0)
        {
            complete?.Invoke(StrategicBattlePointerTurnResult.Fail(
                "strategic battle has no available command deck"));
            yield break;
        }

        List<string> committedCards = new List<string>(availableDeckIds.Length);
        HashSet<string> committedIntentIds = new HashSet<string>(StringComparer.Ordinal);
        for (int deckIndex = 0; deckIndex < availableDeckIds.Length; deckIndex++)
        {
            string characterId = availableDeckIds[deckIndex];
            state = director.State;
            if (state == null || !battle.HasActiveBattle)
            {
                complete?.Invoke(StrategicBattlePointerTurnResult.Fail(
                    "strategic battle ended while command decks were being queued"));
                yield break;
            }

            if (state.commandQueue.Any(entry => entry != null
                && string.Equals(entry.characterId,
                    characterId,
                    StringComparison.Ordinal)))
            {
                continue;
            }

            OffenseCommandDeckStateData deck = state.decks.FirstOrDefault(candidate =>
                candidate != null
                && string.Equals(candidate.characterId,
                    characterId,
                    StringComparison.Ordinal));
            int deckRowIndex = state.decks.IndexOf(deck);
            OffenseCommandCardStateData card = SelectOffensiveCard(
                deck?.candidates,
                session.FindCombatant(characterId));
            OffenseEnemyIntentStateData intent = SelectLivingEnemyIntent(
                state.enemyIntents,
                session,
                committedIntentIds);
            if (card == null || intent == null)
            {
                complete?.Invoke(StrategicBattlePointerTurnResult.Fail(
                    $"strategic battle deck has no offensive card or living enemy intent; "
                    + $"character={characterId}"));
                yield break;
            }

            string cardLabel = $"{card.displayName}\n"
                + $"{GetTacticalTagLabel(card.tacticalTag)} · {card.executionStages}단계"
                + $" · 속도 {card.speed}";
            if (!ClickBattleCardButtonExact(cardLabel, deckRowIndex))
            {
                complete?.Invoke(StrategicBattlePointerTurnResult.Fail(
                    $"strategic offensive command card was not pointer-clickable; "
                    + $"character={characterId}; card={card.displayName}"));
                yield break;
            }
            yield return null;

            state = director.State;
            intent = state?.enemyIntents?.FirstOrDefault(candidate => candidate != null
                && string.Equals(
                    candidate.intentId,
                    intent.intentId,
                    StringComparison.Ordinal)
                && session.FindCombatant(candidate.enemyId) is
                    { IsDead: false, IsDowned: false });
            if (intent == null)
            {
                complete?.Invoke(StrategicBattlePointerTurnResult.Fail(
                    "strategic enemy intent disappeared before pointer commit"));
                yield break;
            }
            OffenseBattleCombatant enemy = session.FindCombatant(intent.enemyId);
            string intentLabel = $"{enemy?.DisplayName ?? intent.enemyId}\n"
                + $"{GetTacticalTagLabel(intent.tacticalTag)} {intent.executionStages}단계";
            if (!ClickButtonExact(intentLabel))
            {
                complete?.Invoke(StrategicBattlePointerTurnResult.Fail(
                    $"strategic enemy intent was not pointer-clickable; "
                    + $"character={characterId}; intent={intent.intentId}"));
                yield break;
            }
            yield return null;

            state = director.State;
            if (state?.commandQueue?.Any(entry => entry != null
                && string.Equals(entry.characterId,
                    characterId,
                    StringComparison.Ordinal)
                && string.Equals(entry.cardInstanceId,
                    card.instanceId,
                    StringComparison.Ordinal)
                && string.Equals(entry.targetIntentId,
                    intent.intentId,
                    StringComparison.Ordinal)
                && string.Equals(entry.targetCombatantId,
                    intent.enemyId,
                    StringComparison.Ordinal)) != true)
            {
                complete?.Invoke(StrategicBattlePointerTurnResult.Fail(
                    $"strategic command pointer did not commit the selected card; "
                    + $"character={characterId}; card={card.instanceId}"));
                yield break;
            }
            committedIntentIds.Add(intent.intentId);
            committedCards.Add($"{characterId}:{card.displayName}:{card.actionType}:"
                + $"{card.tacticalTag}"
                + $"->{intent.intentId}:{intent.enemyId}");
        }

        state = director.State;
        int queuedCommands = state?.commandQueue?.Count ?? 0;
        if (queuedCommands != availableDeckIds.Length)
        {
            complete?.Invoke(StrategicBattlePointerTurnResult.Fail(
                $"strategic battle did not queue every available deck; "
                + $"queued={queuedCommands}; available={availableDeckIds.Length}"));
            yield break;
        }

        if (!ClickButtonExact($"명령 실행 {queuedCommands}/{state.decks.Count}"))
        {
            complete?.Invoke(StrategicBattlePointerTurnResult.Fail(
                $"strategic command execution was not pointer-clickable; "
                + $"queued={queuedCommands}; decks={state.decks.Count}"));
            yield break;
        }
        yield return null;

        OffenseBattleDirectorStateData stateAfter = director.State;
        bool battleTerminal = !battle.HasActiveBattle;
        if (!battleTerminal
            && (stateAfter == null
                || stateAfter.turn <= turn
                || stateAfter.commandQueue.Count != 0))
        {
            complete?.Invoke(StrategicBattlePointerTurnResult.Fail(
                "strategic command execution did not atomically clear the queue "
                + "and advance the director turn; "
                + $"turn={turn}->{stateAfter?.turn ?? -1}; "
                + $"queue={stateAfter?.commandQueue?.Count ?? -1}"));
            yield break;
        }

        float allyHealthAfter = GetLivingTeamHealth(
            session,
            OffenseBattleTeam.Allies);
        float enemyHealthAfter = GetLivingTeamHealth(
            session,
            OffenseBattleTeam.Enemies);
        long commandIdAfter = session.LastProcessedCommandId;
        string formationAfter = DescribeBattleFormation(session);
        complete?.Invoke(new StrategicBattlePointerTurnResult
        {
            Success = true,
            Turn = turn,
            QueuedCommands = queuedCommands,
            CardSummary = string.Join(",", committedCards),
            AllyHealthBefore = allyHealthBefore,
            AllyHealthAfter = allyHealthAfter,
            EnemyHealthBefore = enemyHealthBefore,
            EnemyHealthAfter = enemyHealthAfter,
            BattleTerminal = battleTerminal,
            RoundBefore = roundBefore,
            RoundAfter = session.RoundNumber,
            CommandIdBefore = commandIdBefore,
            CommandIdAfter = commandIdAfter,
            FormationBefore = formationBefore,
            FormationAfter = formationAfter,
            ResolutionSummary = DescribeResolvedCommands(
                director.LastResolvedTurn)
        });
    }

    private static OffenseCommandCardStateData SelectOffensiveCard(
        IReadOnlyList<OffenseCommandCardStateData> candidates,
        OffenseBattleCombatant combatant)
    {
        return candidates?
            .Where(card => card != null)
            .OrderBy(card => GetDamagingCardRank(card, combatant))
            .ThenBy(card => GetOffensiveCardRank(card.tacticalTag))
            .ThenByDescending(card => card.power)
            .ThenByDescending(card => card.executionStages)
            .ThenByDescending(card => card.speed)
            .ThenBy(card => card.instanceId, StringComparer.Ordinal)
            .FirstOrDefault();
    }

    private static int GetDamagingCardRank(
        OffenseCommandCardStateData card,
        OffenseBattleCombatant combatant)
    {
        if (card == null) return 5;
        if (card.actionType == OffenseBattleActionType.Advance)
        {
            return combatant?.Formation == OffenseFormationSlot.Front ? 4 : 0;
        }
        if (card.actionType == OffenseBattleActionType.BasicAttack) return 1;
        if (card.actionType != OffenseBattleActionType.Ability) return 4;
        CharacterCombatAbilityDefinition ability = combatant?.Abilities?
            .FirstOrDefault(candidate => candidate != null
                && string.Equals(candidate.Id,
                    card.sourceSkillId,
                    StringComparison.Ordinal));
        return ability?.TargetRule == OffenseBattleTargetRule.Enemy
            && ability.Effects.Any(effect => effect is OffenseDamageEffect damage
                && (damage.BasicDamageMultiplier > 0f || damage.FlatDamage > 0f))
            ? 2
            : 3;
    }

    private static int GetOffensiveCardRank(OffenseTacticalTag tag)
    {
        return tag switch
        {
            OffenseTacticalTag.Execute => 0,
            OffenseTacticalTag.Break => 1,
            OffenseTacticalTag.Maneuver => 2,
            OffenseTacticalTag.Intercept => 3,
            OffenseTacticalTag.Support => 4,
            _ => 5
        };
    }

    private static OffenseEnemyIntentStateData SelectLivingEnemyIntent(
        IReadOnlyList<OffenseEnemyIntentStateData> intents,
        OffenseBattleSession session,
        ISet<string> alreadyCommittedIntentIds = null)
    {
        OffenseEnemyIntentStateData[] living = intents?
            .Where(intent => intent != null
                && session?.FindCombatant(intent.enemyId) is
                    { IsDead: false, IsDowned: false })
            .OrderByDescending(intent => intent.threat)
            .ThenBy(intent => intent.intentId, StringComparer.Ordinal)
            .ToArray() ?? Array.Empty<OffenseEnemyIntentStateData>();
        return living.FirstOrDefault(intent => alreadyCommittedIntentIds == null
                || !alreadyCommittedIntentIds.Contains(intent.intentId))
            ?? living.FirstOrDefault();
    }

    private static float GetLivingTeamHealth(
        OffenseBattleSession session,
        OffenseBattleTeam team)
    {
        return session?.Combatants
            .Where(combatant => combatant != null
                && combatant.Team == team
                && !combatant.IsDead)
            .Sum(combatant => Mathf.Max(0f, combatant.CurrentHealth)) ?? 0f;
    }

    private static string DescribeResolvedCommands(
        IReadOnlyList<OffenseResolvedCommand> resolved)
    {
        if (resolved == null || resolved.Count == 0)
        {
            return "none";
        }

        return string.Join(",", resolved.Select(command =>
            $"{command.characterId}:{command.execution.Outcome}:"
            + $"effect={command.execution.AppliedAtLeastOneEffect}:"
            + $"reason={command.execution.FailureReason}"));
    }

    private static string DescribeBattleFormation(OffenseBattleSession session)
    {
        return session == null
            ? "missing"
            : string.Join(",", session.Combatants
                .Where(combatant => combatant != null)
                .OrderBy(combatant => combatant.Team)
                .ThenBy(combatant => combatant.Formation)
                .ThenBy(combatant => combatant.PersistentId, StringComparer.Ordinal)
                .Select(combatant =>
                    $"{combatant.PersistentId}:{combatant.Team}:{combatant.Formation}:"
                    + $"hp={combatant.CurrentHealth:0.##}:downed={combatant.IsDowned}:"
                    + $"dead={combatant.IsDead}"));
    }

    private sealed class StrategicBattlePointerTurnResult
    {
        public bool Success { get; set; }
        public string Failure { get; set; } = string.Empty;
        public int Turn { get; set; }
        public int QueuedCommands { get; set; }
        public string CardSummary { get; set; } = string.Empty;
        public float AllyHealthBefore { get; set; }
        public float AllyHealthAfter { get; set; }
        public float EnemyHealthBefore { get; set; }
        public float EnemyHealthAfter { get; set; }
        public bool BattleTerminal { get; set; }
        public int RoundBefore { get; set; }
        public int RoundAfter { get; set; }
        public long CommandIdBefore { get; set; }
        public long CommandIdAfter { get; set; }
        public string FormationBefore { get; set; } = string.Empty;
        public string FormationAfter { get; set; } = string.Empty;
        public string ResolutionSummary { get; set; } = string.Empty;
        public float AllyDamage => Mathf.Max(0f, AllyHealthBefore - AllyHealthAfter);
        public float EnemyDamage => Mathf.Max(0f, EnemyHealthBefore - EnemyHealthAfter);
        public bool FormationChanged => !string.Equals(
            FormationBefore,
            FormationAfter,
            StringComparison.Ordinal);

        public static StrategicBattlePointerTurnResult Fail(string failure)
        {
            return new StrategicBattlePointerTurnResult
            {
                Success = false,
                Failure = failure ?? string.Empty
            };
        }
    }

    private static string GetTacticalTagLabel(OffenseTacticalTag tag)
    {
        return tag switch
        {
            OffenseTacticalTag.Intercept => "저지",
            OffenseTacticalTag.Maneuver => "기동",
            OffenseTacticalTag.Break => "파쇄",
            OffenseTacticalTag.Support => "지원",
            OffenseTacticalTag.Execute => "집행",
            _ => "일반"
        };
    }

    private static string DescribeActiveOffenseButtons()
    {
        return string.Join(" | ", FindActiveOffenseButtons()
            .Select(button => GetLabel(button))
            .Where(label => !string.IsNullOrWhiteSpace(label))
            .Take(16));
    }

    private static string DescribeActiveOffensePanelText()
    {
        List<string> labels = new List<string>();
        foreach (OffenseWorldMapPanel panel in
                 UnityEngine.Object.FindObjectsByType<OffenseWorldMapPanel>(
                     FindObjectsInactive.Exclude,
                     FindObjectsSortMode.None))
        {
            if (panel == null || !panel.gameObject.activeInHierarchy) continue;
            labels.AddRange(panel.GetComponentsInChildren<TMP_Text>(false)
                .Where(text => text != null
                    && text.gameObject.activeInHierarchy
                    && !string.IsNullOrWhiteSpace(text.text))
                .Select(text => text.text.Replace('\n', ' ')));
        }
        return string.Join(" | ", labels.Distinct().Take(20));
    }

    private static string RunFullCampaignThroughUiCore(
        ICollection<string> evidence)
    {
        if (!Application.isPlaying) return "FAIL: PlayMode is required.";
        OffenseExpeditionRuntime runtime = UnityEngine.Object.FindFirstObjectByType<OffenseExpeditionRuntime>();
        OffenseWorldMapRuntime worldMap = UnityEngine.Object.FindFirstObjectByType<OffenseWorldMapRuntime>();
        DungeonRuntimeLifetimeScope scope = UnityEngine.Object.FindFirstObjectByType<DungeonRuntimeLifetimeScope>();
        IOffenseBattleRuntime battle = scope?.Container?.Resolve<IOffenseBattleRuntime>();
        IOffensePanelService panelService = scope?.Container?.Resolve<IOffensePanelService>();
        if (runtime == null || worldMap == null || battle == null || panelService == null)
        {
            return "FAIL: offense runtime is missing.";
        }
        AddEvidence(evidence, "RUNTIME_AUTHORITIES", "expedition+battle+panel resolved");

        OwnerRunManager ownerManager = UnityEngine.Object.FindFirstObjectByType<OwnerRunManager>();
        if (ownerManager != null && ownerManager.CurrentOwnerActor == null)
        {
            Button ownerButton = UnityEngine.Object.FindObjectsByType<Button>(
                    FindObjectsInactive.Exclude,
                    FindObjectsSortMode.None)
                .FirstOrDefault(value => value != null
                    && value.gameObject.activeInHierarchy
                    && value.interactable
                    && value.name.StartsWith("OwnerOption_", StringComparison.Ordinal));
            ExecutePointerClick(ownerButton);
        }
        if (ownerManager != null && ownerManager.CurrentOwnerActor == null)
        {
            return "FAIL: owner selection did not start the run.";
        }
        AddEvidence(evidence, "COMMAND_OWNER_SELECTION", "production pointer accepted");

        OffenseTargetDefinition first = worldMap.TargetDefinitions
            .Where(target => target != null)
            .OrderBy(target => target.campaignOrder)
            .FirstOrDefault();
        if (first == null) return "FAIL: campaign target is missing.";

        worldMap.Campaign.PublishRestoreCandidate(
            worldMap.Campaign.BuildRestoreCandidate(
                new DungeonOffenseCampaignSaveData
                {
                    reconLevel = 1,
                    selectedTargetId = first.id,
                    knownTargetIds = new List<string> { first.id }
                }));

        CharacterActor[] party =
        {
            CreateActor(991241, "UI 원정대 선봉", 100),
            CreateActor(991242, "UI 원정대 중열", 100),
            CreateActor(991243, "UI 원정대 후열", 100)
        };
        List<string> completedTargets = new List<string>();
        foreach (OffenseTargetDefinition target in worldMap.TargetDefinitions
            .Where(value => value != null)
            .OrderBy(value => value.campaignOrder))
        {
            int resultHistoryBefore = runtime.ResultHistory.Count;
            bool issuedJourneyCommand = false;
            panelService.ShowWorldMap();
            int reconSafety = 0;
            while (!HasActiveButtonContaining(target.title)
                && reconSafety++ < OffenseWorldMapService.MaxReconLevel)
            {
                if (!ClickButtonExact("정찰 강화"))
                {
                    return $"FAIL: recon could not reveal {target.id}.";
                }
            }
            if (!ClickButtonContaining(target.title))
            {
                return $"FAIL: target button '{target.title}' was not clickable.";
            }
            ClickButtonExact("닫기");

            runtime.ShowExpeditionPanel();
            for (int index = 0; index < target.requiredMembers; index++)
            {
                if (!ClickButtonContaining(party[index].Identity.DisplayName))
                {
                    return $"FAIL: party member {index} was not selectable for {target.id}.";
                }
            }

            if (!ClickButtonExact("원정 출발") || runtime.ActiveExpeditions.Count != 1)
            {
                return $"FAIL: expedition did not start for {target.id}.";
            }
            AddEvidence(evidence, "COMMAND_EXPEDITION_START", "target=" + target.id);

            int safety = 0;
            while (runtime.ActiveExpeditions.Count > 0 && safety++ < 200)
            {
                OffenseExpeditionRun expedition = runtime.ActiveExpeditions[0];
                if (expedition.Phase == OffenseExpeditionPhase.ChoosingRoute)
                {
                    OffenseRouteNode next = expedition.GetAvailableRouteNodes().FirstOrDefault();
                    if (next == null || !ClickButtonContaining(next.Title))
                    {
                        return $"FAIL: route choice failed at {target.id}.";
                    }
                    issuedJourneyCommand = true;
                    AddEvidence(evidence, "COMMAND_ROUTE_OR_BATTLE", "target="
                        + target.id + "; command=route; node=" + next.Id);
                }
                else if (expedition.Phase == OffenseExpeditionPhase.ResolvingNode)
                {
                    string choice = expedition.CurrentNode?.Kind switch
                    {
                        OffenseRouteNodeKind.Cache => "보급고 수색",
                        OffenseRouteNodeKind.Camp => "쉬지 않고 전진",
                        _ => "위험 감수"
                    };
                    if (!ClickButtonExact(choice))
                    {
                        return $"FAIL: node resolution failed at {target.id}.";
                    }
                    issuedJourneyCommand = true;
                    AddEvidence(evidence, "COMMAND_ROUTE_OR_BATTLE", "target="
                        + target.id + "; command=node-resolution; choice=" + choice);
                }
                else if (expedition.Phase == OffenseExpeditionPhase.InBattle)
                {
                    OffenseBattleCombatant enemy = battle.Session?.Combatants
                        .FirstOrDefault(combatant => combatant.Team == OffenseBattleTeam.Enemies && !combatant.IsDead);
                    if (enemy == null
                        || !ClickButtonExact("공격")
                        || !ClickButtonByName($"Combatant_{enemy.PersistentId}"))
                    {
                        return $"FAIL: battle command failed at {target.id}.";
                    }
                    issuedJourneyCommand = true;
                    AddEvidence(evidence, "COMMAND_ROUTE_OR_BATTLE", "target="
                        + target.id + "; command=battle-attack; enemy="
                        + enemy.PersistentId);
                }
            }

            if (runtime.ActiveExpeditions.Count > 0
                || !worldMap.State.CompletedTargetIds.Contains(target.id))
            {
                return $"FAIL: target {target.id} did not complete.";
            }
            if (!issuedJourneyCommand)
            {
                return $"FAIL: target {target.id} completed without a production journey command.";
            }
            AddEvidence(evidence, "TARGET_TERMINAL", "target=" + target.id
                + "; completed=True; steps=" + safety);
            if (runtime.ResultHistory.Count <= resultHistoryBefore)
            {
                return $"FAIL: target {target.id} completed without a reward result.";
            }
            AddEvidence(evidence, "REWARD_HISTORY_ADVANCED", "target="
                + target.id + "; history=" + resultHistoryBefore + "->"
                + runtime.ResultHistory.Count);
            completedTargets.Add(target.id);
        }

        string growth = string.Join(" | ", party.Select(actor =>
            $"{actor.Identity.DisplayName}:Lv.{actor.Progression?.Level ?? 0},XP={actor.Progression?.CurrentExperience ?? 0},skills={actor.Progression?.LearnedSkillIds.Count ?? 0}"));
        if (!worldMap.State.TruthRevealed)
        {
            return "FAIL: truth was not revealed after the final boss.";
        }
        AddEvidence(evidence, "CAMPAIGN_TRUTH_REVEALED", "targets="
            + completedTargets.Count + "; history=" + runtime.ResultHistory.Count);
        return $"PASS: completed={string.Join(",", completedTargets)}; truth={worldMap.State.TruthRevealed}; history={runtime.ResultHistory.Count}; growth={growth}";
    }

    private static void AddEvidence(
        ICollection<string> evidence,
        string rowId,
        string detail)
    {
        evidence?.Add("PASS\t" + rowId + "\t" + (detail ?? string.Empty));
    }

    private static string GetLabel(Button button)
    {
        return button != null
            ? button.GetComponentInChildren<TMP_Text>(true)?.text ?? string.Empty
            : string.Empty;
    }

    private static bool ClickButtonContaining(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return false;
        Button button = FindActiveButtonContaining(text);
        return ExecutePointerClick(button);
    }

    private static bool HasActiveButtonContaining(string text)
    {
        return FindActiveButtonContaining(text) != null;
    }

    private static Button FindActiveButtonContaining(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        return FindActiveOffenseButtons()
            .FirstOrDefault(value => GetLabel(value).Contains(text, StringComparison.Ordinal));
    }

    private static bool ClickButtonExact(string text)
    {
        Button button = FindActiveOffenseButtons()
            .FirstOrDefault(value => string.Equals(GetLabel(value), text, StringComparison.Ordinal));
        return ExecutePointerClick(button);
    }

    private static bool ClickBattleCardButtonExact(string text, int deckRowIndex)
    {
        if (deckRowIndex < 0 || string.IsNullOrWhiteSpace(text)) return false;
        TMP_Text deckRow = UnityEngine.Object.FindObjectsByType<TMP_Text>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None)
            .FirstOrDefault(value => value != null
                && value.gameObject.activeInHierarchy
                && string.Equals(value.name,
                    $"DeckName_{deckRowIndex}",
                    StringComparison.Ordinal));
        if (deckRow?.transform is not RectTransform deckRect) return false;

        Button closest = null;
        float closestDistance = float.PositiveInfinity;
        foreach (Button candidate in FindActiveOffenseButtons())
        {
            if (!string.Equals(GetLabel(candidate), text, StringComparison.Ordinal)
                || candidate.transform is not RectTransform candidateRect)
            {
                continue;
            }

            float distance = Mathf.Abs(candidateRect.anchorMax.y - deckRect.anchorMax.y);
            if (distance < closestDistance)
            {
                closest = candidate;
                closestDistance = distance;
            }
        }

        return closestDistance <= 0.001f && ExecutePointerClick(closest);
    }

    private static bool ClickButtonByName(string name)
    {
        Button button = FindActiveOffenseButtons()
            .FirstOrDefault(value => value != null
                && string.Equals(value.name, name, StringComparison.Ordinal));
        return ExecutePointerClick(button);
    }

    private static IReadOnlyList<Button> FindActiveOffenseButtons()
    {
        List<Button> buttons = new List<Button>();
        AddButtonsFromActivePanel<OffenseBattlePanel>(buttons);
        AddButtonsFromActivePanel<OffenseExpeditionPanel>(buttons);
        AddButtonsFromActivePanel<OffenseWorldMapPanel>(buttons);
        return buttons
            .Where(value => value != null && value.gameObject.activeInHierarchy && value.interactable)
            .ToArray();
    }

    private static void AddButtonsFromActivePanel<T>(ICollection<Button> buttons)
        where T : Component
    {
        foreach (T panel in UnityEngine.Object.FindObjectsByType<T>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.None))
        {
            if (panel == null || !panel.gameObject.activeInHierarchy) continue;
            foreach (Button button in panel.GetComponentsInChildren<Button>(false))
            {
                buttons.Add(button);
            }
        }
    }

    internal static bool ExecutePointerClick(Button button)
    {
        EventSystem eventSystem = EventSystem.current;
        if (button == null || eventSystem == null) return false;
        RectTransform rect = button.transform as RectTransform;
        Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(
            null,
            rect != null ? rect.TransformPoint(rect.rect.center) : button.transform.position);
        PointerEventData pointer = new PointerEventData(eventSystem)
        {
            button = PointerEventData.InputButton.Left,
            position = screenPoint,
            pointerPress = button.gameObject,
            pointerEnter = button.gameObject
        };
        ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerEnterHandler);
        ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerDownHandler);
        ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerUpHandler);
        ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerClickHandler);
        return true;
    }

    private static CharacterActor CreateActor()
    {
        return CreateActor(991234, "원정 검증대원", 14);
    }

    private static CharacterActor CreateActor(int id, string name, int statValue)
    {
        _ = id;
        _ = statValue;
        CharacterSO data =
            OffenseEditorTestDependencies.RequireCharacterArchetype("Orc");

        GameObject actorObject = new GameObject("OffenseJourneyQaActor");
        actorObject.AddComponent<SpriteRenderer>();
        CharacterActor actor = actorObject.AddComponent<CharacterActor>();
        actorObject.AddComponent<AbilityMove>();
        actorObject.AddComponent<AbilityWork>();
        actor.RefreshAbilityCache();
        CharacterAiEditorTestDependencies.Inject(actorObject);
        actor.Initialization(data);
        actor.characterType = CharacterType.NPC;
        actor.SetLifecycleState(CharacterLifecycleState.Active);
        CreatedObjects.Add(actorObject);
        return actor;
    }
}

public sealed class OffenseJourneyFullCampaignPlayModeRunner : MonoBehaviour
{
    private const float SetupTimeoutRealtime = 60f;
    private readonly List<string> preflightEvidence = new List<string>();
    private readonly List<string> consoleErrors = new List<string>();
    private string startupFailure = string.Empty;
    private bool campaignInvoked;
    private bool cleanupApplied;

    public bool ExitPlayModeOnCompletion { get; set; }

    private void OnEnable()
    {
        Application.logMessageReceived += OnLogMessageReceived;
    }

    private void OnDisable()
    {
        Application.logMessageReceived -= OnLogMessageReceived;
        CleanupOnce();
    }

    private void OnDestroy()
    {
        Application.logMessageReceived -= OnLogMessageReceived;
        CleanupOnce();
    }

    private IEnumerator Start()
    {
        OffenseJourneyPlayModeFacade.WriteReport(
            "RUNNING",
            "preparing production start-party UI",
            preflightEvidence);
        IEnumerator routine = PrepareAndRun();
        while (true)
        {
            object current;
            try
            {
                if (!routine.MoveNext()) break;
                current = routine.Current;
            }
            catch (Exception exception)
            {
                startupFailure = "FAIL: unhandled runner "
                    + exception.GetType().Name + ": " + exception.Message;
                break;
            }
            yield return current;
        }

        if (!campaignInvoked)
        {
            if (string.IsNullOrWhiteSpace(startupFailure))
            {
                startupFailure = "FAIL: start-party preparation did not complete.";
            }
            OffenseJourneyPlayModeFacade.WriteReport(
                "FAIL",
                startupFailure,
                preflightEvidence);
            Debug.LogError("[OffenseJourney] " + startupFailure);
        }

        CleanupOnce();
        Destroy(gameObject);
        if (ExitPlayModeOnCompletion)
        {
            EditorApplication.delayCall += () =>
            {
                if (EditorApplication.isPlaying)
                    EditorApplication.isPlaying = false;
            };
        }
    }

    private IEnumerator PrepareAndRun()
    {
        float deadline = Time.realtimeSinceStartup + SetupTimeoutRealtime;
        DungeonRuntimeLifetimeScope scope = null;
        OwnerRunManager ownerManager = null;
        while (Time.realtimeSinceStartup < deadline)
        {
            scope = FindFirstObjectByType<DungeonRuntimeLifetimeScope>(
                FindObjectsInactive.Include);
            ownerManager = FindFirstObjectByType<OwnerRunManager>(
                FindObjectsInactive.Include);
            if (scope?.Container != null
                && ownerManager != null
                && EventSystem.current != null)
            {
                break;
            }
            yield return null;
        }

        if (scope?.Container == null
            || ownerManager == null
            || EventSystem.current == null)
        {
            startupFailure = "FAIL: official GameplayScene UI authorities did not become ready.";
            yield break;
        }
        preflightEvidence.Add("PASS\tSTART_PARTY_UI_AUTHORITIES\t"
            + "lifetime-scope+owner-manager+event-system");

        if (ownerManager.CurrentOwnerActor == null)
        {
            Button ownerButton = null;
            while (Time.realtimeSinceStartup < deadline && ownerButton == null)
            {
                ownerButton = Resources.FindObjectsOfTypeAll<Button>()
                    .Where(button => button != null
                        && button.gameObject.scene.IsValid()
                        && button.gameObject.activeInHierarchy
                        && button.interactable)
                    .OrderBy(button => button.name, StringComparer.Ordinal)
                    .FirstOrDefault(button => button.name.StartsWith(
                        "OwnerOption_",
                        StringComparison.Ordinal));
                if (ownerButton == null) yield return null;
            }

            if (!OffenseJourneyPlayModeFacade.ExecutePointerClick(ownerButton))
            {
                startupFailure = "FAIL: production owner option was not pointer-clickable.";
                yield break;
            }
            preflightEvidence.Add("PASS\tSTART_PARTY_OWNER_POINTER\t"
                + (ownerButton != null ? ownerButton.name : "missing"));

            while (Time.realtimeSinceStartup < deadline
                && ownerManager.CurrentOwnerActor == null
                && StartPartyPlayModeTestDriver.FindButton(
                    "PreparationStartRunButton",
                    requireInteractable: false) == null
                && StartPartyPlayModeTestDriver.FindButton(
                    "StartPartyConfirm",
                    requireInteractable: false) == null)
            {
                yield return null;
            }

            IEnumerator startParty =
                StartPartyPlayModeTestDriver.CompleteIfVisible(
                    Mathf.Max(1f, deadline - Time.realtimeSinceStartup));
            while (true)
            {
                object current;
                try
                {
                    if (!startParty.MoveNext()) break;
                    current = startParty.Current;
                }
                catch (Exception exception)
                {
                    startupFailure = "FAIL: start-party UI coroutine threw "
                        + exception.GetType().Name + ": " + exception.Message;
                    yield break;
                }
                yield return current;
            }
        }

        while (Time.realtimeSinceStartup < deadline
            && (ownerManager.CurrentOwnerActor == null
                || ownerManager.CurrentOwnerActor.CurrentLifecycleState
                    != CharacterLifecycleState.Active))
        {
            yield return null;
        }
        if (ownerManager.CurrentOwnerActor == null
            || ownerManager.CurrentOwnerActor.CurrentLifecycleState
                != CharacterLifecycleState.Active)
        {
            startupFailure = "FAIL: start-party UI did not publish an active owner actor.";
            yield break;
        }

        preflightEvidence.Add("PASS\tSTART_PARTY_UI_COMPLETED\towner="
            + ownerManager.CurrentOwnerActor.Identity?.DisplayName);
        preflightEvidence.Add("PASS\tCOMMAND_OWNER_SELECTION\tproduction pointer accepted");
        campaignInvoked = true;
        string terminal = string.Empty;
        IEnumerator journey = OffenseJourneyPlayModeFacade
            .RunStrategicJourneyThroughUi(
                scope,
                preflightEvidence,
                value => terminal = value);
        while (true)
        {
            object current;
            try
            {
                if (!journey.MoveNext()) break;
                current = journey.Current;
            }
            catch (Exception exception)
            {
                terminal = "FAIL: unhandled strategic journey "
                    + exception.GetType().Name + ": " + exception.Message;
                break;
            }
            yield return current;
        }
        if (string.IsNullOrWhiteSpace(terminal))
        {
            terminal = "FAIL: strategic journey ended without a terminal result.";
        }
        if (terminal.StartsWith("PASS:", StringComparison.Ordinal)
            && consoleErrors.Count > 0)
        {
            terminal = "FAIL: production journey emitted Console Error/Exception. "
                + string.Join(" | ", consoleErrors.Take(4));
        }
        else if (consoleErrors.Count == 0)
        {
            preflightEvidence.Add("PASS\tCONSOLE_CLEAN\terrors=0; exceptions=0");
        }
        OffenseJourneyPlayModeFacade.WriteReport(
            terminal.StartsWith("PASS:", StringComparison.Ordinal)
                ? "PASS"
                : "FAIL",
            terminal,
            preflightEvidence);
        if (terminal.StartsWith("PASS:", StringComparison.Ordinal))
            Debug.Log("[OffenseJourney] " + terminal);
        else
            Debug.LogError("[OffenseJourney] " + terminal);
    }

    private void OnLogMessageReceived(
        string condition,
        string stackTrace,
        LogType type)
    {
        if (type is not LogType.Error and not LogType.Exception)
        {
            return;
        }
        if (condition != null
            && condition.StartsWith("[OffenseJourney]", StringComparison.Ordinal))
        {
            return;
        }
        consoleErrors.Add((condition ?? string.Empty).Replace('\n', ' '));
    }

    private void CleanupOnce()
    {
        if (cleanupApplied)
        {
            return;
        }

        cleanupApplied = true;
        OffenseJourneyPlayModeFacade.Cleanup();
    }
}
