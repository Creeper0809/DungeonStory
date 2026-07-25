using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using VContainer;

public static class OffenseJourneyPlayModeFacade
{
    private static readonly List<UnityEngine.Object> CreatedObjects = new List<UnityEngine.Object>();

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

            worldMap.RestorePersistentState(
                1,
                target.id,
                new[] { target.id },
                Array.Empty<string>(),
                string.Empty);
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

    public static string RunFullCampaignThroughUi()
    {
        if (!Application.isPlaying) return "FAIL: PlayMode is required.";
        OffenseExpeditionRuntime runtime = UnityEngine.Object.FindFirstObjectByType<OffenseExpeditionRuntime>();
        OffenseWorldMapRuntime worldMap = UnityEngine.Object.FindFirstObjectByType<OffenseWorldMapRuntime>();
        DungeonRuntimeLifetimeScope scope = UnityEngine.Object.FindFirstObjectByType<DungeonRuntimeLifetimeScope>();
        IOffenseBattleRuntime battle = scope?.Container?.Resolve<IOffenseBattleRuntime>();
        if (runtime == null || worldMap == null || battle == null)
        {
            return "FAIL: offense runtime is missing.";
        }

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

        OffenseTargetDefinition first = worldMap.TargetDefinitions
            .Where(target => target != null)
            .OrderBy(target => target.campaignOrder)
            .FirstOrDefault();
        if (first == null) return "FAIL: campaign target is missing.";

        worldMap.RestorePersistentState(
            1,
            first.id,
            new[] { first.id },
            Array.Empty<string>(),
            string.Empty);

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
            worldMap.ShowWorldMap();
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
                }
            }

            if (runtime.ActiveExpeditions.Count > 0
                || !worldMap.State.CompletedTargetIds.Contains(target.id))
            {
                return $"FAIL: target {target.id} did not complete.";
            }
            completedTargets.Add(target.id);
        }

        string growth = string.Join(" | ", party.Select(actor =>
            $"{actor.Identity.DisplayName}:Lv.{actor.Progression?.Level ?? 0},XP={actor.Progression?.CurrentExperience ?? 0},skills={actor.Progression?.LearnedSkillIds.Count ?? 0}"));
        return worldMap.State.TruthRevealed
            ? $"PASS: completed={string.Join(",", completedTargets)}; truth={worldMap.State.TruthRevealed}; history={runtime.ResultHistory.Count}; growth={growth}"
            : "FAIL: truth was not revealed after the final boss.";
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

    private static bool ExecutePointerClick(Button button)
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
        CharacterSO data = ScriptableObject.CreateInstance<CharacterSO>();
        data.id = id;
        data.characterName = name;
        data.characterType = CharacterType.NPC;
        data.role = CharacterRole.Regular;
        data.speciesTag = "Orc";
        data.baseStats = CharacterStatBlock.CreateDefault(statValue);
        data.defaultWorkPriorities = WorkPriorityProfile.CreateDefault();

        GameObject actorObject = new GameObject("OffenseJourneyQaActor");
        actorObject.AddComponent<SpriteRenderer>();
        CharacterActor actor = actorObject.AddComponent<CharacterActor>();
        actorObject.AddComponent<AbilityMove>();
        actorObject.AddComponent<AbilityWork>();
        actor.RefreshAbilityCache();
        CharacterAiEditorTestDependencies.Inject(actorObject);
        actor.Initialization(data);
        actor.SetLifecycleState(CharacterLifecycleState.Active);
        CreatedObjects.Add(actorObject);
        CreatedObjects.Add(data);
        return actor;
    }
}
