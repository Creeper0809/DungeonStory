using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VContainer;
using VContainer.Unity;

public sealed class OffenseBattlePanel : MonoBehaviour
{
    private readonly List<GameObject> dynamicObjects = new List<GameObject>();
    private IOffensePanelButtonFactory buttonFactory;
    private IRunCharacterCatalog characterCatalog;
    private IInvasionThreatRuntimeProvider threatProvider;
    private ITmpKoreanFontService fontService;
    private ICombatEquipmentRuntime combatEquipment;
    private ICombatEquipmentCatalog combatEquipmentCatalog;
    private IOffenseBattleRuntime runtime;
    private GameObject contentRoot;
    private GameObject returnButtonObject;
    private TMP_Text headerText;
    private TMP_Text dangerText;
    private TMP_Text initiativeText;
    private TMP_Text logText;
    private TMP_Text promptText;
    private RectTransform allyRoot;
    private RectTransform enemyRoot;
    private RectTransform actionRoot;
    private OffenseBattleActionType selectedAction = OffenseBattleActionType.BasicAttack;
    private string selectedAbilityId = string.Empty;
    private string previewTargetId = string.Empty;
    private string lastActorId = string.Empty;
    private string statusMessage = string.Empty;

    [Inject]
    public void Construct(
        IOffensePanelButtonFactory buttonFactory,
        IRunCharacterCatalog characterCatalog,
        IInvasionThreatRuntimeProvider threatProvider,
        ITmpKoreanFontService fontService,
        ICombatEquipmentRuntime combatEquipment,
        ICombatEquipmentCatalog combatEquipmentCatalog)
    {
        this.buttonFactory = buttonFactory ?? throw new ArgumentNullException(nameof(buttonFactory));
        this.characterCatalog = characterCatalog ?? throw new ArgumentNullException(nameof(characterCatalog));
        this.threatProvider = threatProvider ?? throw new ArgumentNullException(nameof(threatProvider));
        this.fontService = fontService ?? throw new ArgumentNullException(nameof(fontService));
        this.combatEquipment = combatEquipment ?? throw new ArgumentNullException(nameof(combatEquipment));
        this.combatEquipmentCatalog = combatEquipmentCatalog
            ?? throw new ArgumentNullException(nameof(combatEquipmentCatalog));
    }

    public void Bind(IOffenseBattleRuntime source)
    {
        runtime = source ?? throw new ArgumentNullException(nameof(source));
    }

    public void Refresh()
    {
        OffenseBattleSession session = runtime?.Session;
        if (session == null)
        {
            HideAll();
            return;
        }

        gameObject.SetActive(true);
        bool showBattle = runtime.IsBattleViewVisible;
        contentRoot.SetActive(showBattle);
        returnButtonObject.SetActive(!showBattle);
        if (!showBattle) return;

        OffenseBattleCombatant current = session.CurrentActor;
        if (!string.Equals(lastActorId, current?.PersistentId, StringComparison.Ordinal))
        {
            lastActorId = current?.PersistentId ?? string.Empty;
            selectedAction = OffenseBattleActionType.BasicAttack;
            selectedAbilityId = string.Empty;
            previewTargetId = string.Empty;
            statusMessage = string.Empty;
        }

        headerText.text = $"{session.TargetTitle}  ·  {session.Difficulty}  ·  {session.RoundNumber}라운드";
        dangerText.text = BuildThreatText();
        initiativeText.text = BuildInitiativeText(session);
        logText.text = string.Join("\n", session.Log.TakeLast(10));
        promptText.text = BuildPrompt(session, current);
        RebuildDynamicUi(session, current);
    }

    public void HideAll()
    {
        if (contentRoot != null) contentRoot.SetActive(false);
        if (returnButtonObject != null) returnButtonObject.SetActive(false);
        gameObject.SetActive(false);
    }

    internal void BindGeneratedView(
        GameObject contentRoot,
        GameObject returnButtonObject,
        Button dungeonButton,
        Button returnButton,
        TMP_Text headerText,
        TMP_Text dangerText,
        TMP_Text initiativeText,
        TMP_Text logText,
        TMP_Text promptText,
        RectTransform allyRoot,
        RectTransform enemyRoot,
        RectTransform actionRoot)
    {
        this.contentRoot = contentRoot;
        this.returnButtonObject = returnButtonObject;
        this.headerText = headerText;
        this.dangerText = dangerText;
        this.initiativeText = initiativeText;
        this.logText = logText;
        this.promptText = promptText;
        this.allyRoot = allyRoot;
        this.enemyRoot = enemyRoot;
        this.actionRoot = actionRoot;
        dungeonButton.onClick.AddListener(() => runtime?.SetBattleViewVisible(false));
        returnButton.onClick.AddListener(() => runtime?.SetBattleViewVisible(true));
    }

    private void RebuildDynamicUi(
        OffenseBattleSession session,
        OffenseBattleCombatant current)
    {
        ClearDynamicObjects();
        foreach (OffenseBattleCombatant combatant in session.Combatants
            .Where(value => value.Team == OffenseBattleTeam.Allies))
        {
            CreateCombatantCard(allyRoot, current, combatant);
        }

        foreach (OffenseBattleCombatant combatant in session.Combatants
            .Where(value => value.Team == OffenseBattleTeam.Enemies))
        {
            CreateCombatantCard(enemyRoot, current, combatant);
        }

        BuildActionButtons(session, current);
    }

    private void CreateCombatantCard(
        RectTransform parent,
        OffenseBattleCombatant current,
        OffenseBattleCombatant combatant)
    {
        GameObject card = new GameObject(
            $"Combatant_{combatant.PersistentId}",
            typeof(RectTransform),
            typeof(Image),
            typeof(Button),
            typeof(LayoutElement),
            typeof(Outline));
        card.transform.SetParent(parent, false);
        card.GetComponent<LayoutElement>().preferredHeight = 112f;
        Image background = card.GetComponent<Image>();
        background.color = combatant.IsDead
            ? new Color32(36, 34, 38, 230)
            : ReferenceEquals(current, combatant)
                ? new Color32(91, 66, 37, 245)
                : combatant.Team == OffenseBattleTeam.Allies
                    ? new Color32(35, 62, 59, 245)
                    : new Color32(76, 37, 40, 245);
        Outline outline = card.GetComponent<Outline>();
        outline.effectColor = ReferenceEquals(current, combatant)
            ? new Color32(209, 168, 92, 255)
            : IsSelectableTarget(current, combatant)
                ? new Color32(190, 126, 81, 255)
                : new Color32(93, 82, 84, 220);
        outline.effectDistance = new Vector2(2f, -2f);
        Button button = card.GetComponent<Button>();
        button.targetGraphic = background;
        button.interactable = !combatant.IsDead && current != null && current.Team == OffenseBattleTeam.Allies;
        button.onClick.AddListener(() => SelectTarget(combatant));

        GameObject portraitObject = new GameObject("Portrait", typeof(RectTransform), typeof(Image));
        portraitObject.transform.SetParent(card.transform, false);
        RectTransform portraitRect = portraitObject.GetComponent<RectTransform>();
        portraitRect.anchorMin = new Vector2(0f, 0.5f);
        portraitRect.anchorMax = new Vector2(0f, 0.5f);
        portraitRect.pivot = new Vector2(0f, 0.5f);
        portraitRect.anchoredPosition = new Vector2(14f, 0f);
        portraitRect.sizeDelta = new Vector2(78f, 78f);
        Image portrait = portraitObject.GetComponent<Image>();
        portrait.sprite = ResolvePortrait(combatant);
        portrait.preserveAspect = true;
        portrait.color = portrait.sprite != null
            ? Color.white
            : combatant.Team == OffenseBattleTeam.Allies
                ? DungeonUiTheme.Good
                : DungeonUiTheme.Danger;

        TMP_Text name = CreateCardText(
            card.transform,
            "Name",
            20f,
            TextAlignmentOptions.Left,
            new Vector2(0.29f, 0.56f),
            new Vector2(0.97f, 0.94f));
        name.fontStyle = FontStyles.Bold;
        int level = 1;
        if (combatant.Team == OffenseBattleTeam.Allies
            && runtime.TryGetActor(combatant.PersistentId, out CharacterActor actor)
            && actor.Progression != null)
        {
            level = actor.Progression.Level;
        }
        name.text = combatant.Team == OffenseBattleTeam.Allies
            ? $"Lv.{level}  {combatant.DisplayName}"
            : combatant.DisplayName;

        CreateHealthBar(card.transform, combatant);
        TMP_Text status = CreateCardText(
            card.transform,
            "Status",
            15f,
            TextAlignmentOptions.Left,
            new Vector2(0.29f, 0.04f),
            new Vector2(0.97f, 0.28f));
        status.color = DungeonUiTheme.TextSecondary;
        status.text = BuildStatusText(combatant);
        dynamicObjects.Add(card);
    }

    private void BuildActionButtons(
        OffenseBattleSession session,
        OffenseBattleCombatant current)
    {
        if (session.IsComplete)
        {
            string label = session.Outcome == OffenseBattleOutcome.Victory
                ? "승리 확인"
                : session.Outcome == OffenseBattleOutcome.Retreated
                    ? "후퇴 확인"
                    : "결과 확인";
            CreateActionButton(label, true, false, () => runtime.ClearCompletedBattle());
            return;
        }

        if (current == null || current.Team != OffenseBattleTeam.Allies)
        {
            return;
        }

        CreateActionButton(
            "공격",
            selectedAction == OffenseBattleActionType.BasicAttack,
            false,
            () => SelectAction(OffenseBattleActionType.BasicAttack, string.Empty));
        CreateActionButton(
            "방어",
            false,
            false,
            () => ExecuteImmediate(OffenseBattleActionType.Guard, current.PersistentId, string.Empty));

        CombatWeaponSnapshot weapon = current.Weapon ?? CombatWeaponSnapshot.CreateUnarmed();
        if (weapon.RequiresAmmo)
        {
            Button reload = CreateActionButton(
                $"재장전 {weapon.LoadedAmmo}/{weapon.MagazineCapacity}",
                false,
                false,
                () => ExecuteImmediate(
                    OffenseBattleActionType.Reload,
                    current.PersistentId,
                    string.Empty));
            reload.interactable = weapon.LoadedAmmo < weapon.MagazineCapacity;
        }

        IReadOnlyList<CombatEquipmentInstance> alternateWeapons = GetAlternateWeapons(current);
        if (alternateWeapons.Count > 0)
        {
            CreateActionButton(
                "무기 교체",
                false,
                false,
                () => SwitchToNextWeapon(current, alternateWeapons));
        }

        if (weapon.IsRanged)
        {
            CreateFireModeButton(current, weapon, CombatFireMode.Aimed, "조준");
            CreateFireModeButton(current, weapon, CombatFireMode.Rapid, "속사");
            CreateFireModeButton(current, weapon, CombatFireMode.Suppressive, "제압");
        }

        OffenseBattleCombatant previewTarget = session.FindCombatant(previewTargetId);
        if (selectedAction == OffenseBattleActionType.BasicAttack
            && previewTarget != null
            && IsSelectableTarget(current, previewTarget))
        {
            CreateActionButton(
                "공격 확정",
                true,
                false,
                () => ExecuteImmediate(
                    OffenseBattleActionType.BasicAttack,
                    previewTarget.PersistentId,
                    string.Empty));
        }

        foreach (CharacterCombatAbilityDefinition ability in current.Abilities)
        {
            int cooldown = current.GetCooldown(ability.Id);
            bool positionAllowed = IsPositionAllowed(ability.UsableFrom, current.Formation);
            string label = !positionAllowed
                ? $"{ability.DisplayName} (위치)"
                : cooldown > 0
                ? $"{ability.DisplayName} ({cooldown})"
                : ability.DisplayName;
            Button button = CreateActionButton(
                label,
                selectedAction == OffenseBattleActionType.Ability
                    && string.Equals(selectedAbilityId, ability.Id, StringComparison.Ordinal),
                false,
                () => SelectAbility(current, ability));
            button.interactable = cooldown <= 0 && positionAllowed;
        }

        CreateActionButton(
            "후퇴",
            false,
            true,
            () => ExecuteImmediate(OffenseBattleActionType.Retreat, current.PersistentId, string.Empty));
    }

    private Button CreateActionButton(
        string label,
        bool selected,
        bool destructive,
        Action callback)
    {
        GameObject buttonObject = buttonFactory.CreateButton(actionRoot, label, 18f, callback);
        Button button = buttonObject.GetComponent<Button>();
        StyleBattleButton(button, selected, destructive);
        dynamicObjects.Add(buttonObject);
        return button;
    }

    private static void StyleBattleButton(Button button, bool selected, bool destructive)
    {
        if (button == null) return;
        Image image = button.GetComponent<Image>();
        image.color = destructive
            ? new Color32(126, 40, 42, 255)
            : selected
                ? new Color32(101, 48, 43, 255)
                : new Color32(50, 49, 54, 255);
        button.colors = new ColorBlock
        {
            normalColor = Color.white,
            highlightedColor = new Color(1.14f, 1.1f, 1.03f, 1f),
            pressedColor = new Color(0.7f, 0.68f, 0.66f, 1f),
            selectedColor = Color.white,
            disabledColor = new Color(0.35f, 0.35f, 0.35f, 0.7f),
            colorMultiplier = 1f,
            fadeDuration = 0.06f
        };
        Outline outline = button.GetComponent<Outline>() ?? button.gameObject.AddComponent<Outline>();
        outline.effectColor = selected
            ? new Color32(209, 168, 92, 245)
            : new Color32(91, 82, 88, 190);
        outline.effectDistance = new Vector2(1f, -1f);
    }

    private void SelectAction(OffenseBattleActionType action, string abilityId)
    {
        selectedAction = action;
        selectedAbilityId = abilityId ?? string.Empty;
        previewTargetId = string.Empty;
        statusMessage = action == OffenseBattleActionType.BasicAttack
            ? "공격할 적을 선택하세요."
            : "대상을 선택하세요.";
        Refresh();
    }

    private void SelectAbility(
        OffenseBattleCombatant current,
        CharacterCombatAbilityDefinition ability)
    {
        if (ability.TargetRule == OffenseBattleTargetRule.Self)
        {
            ExecuteImmediate(
                OffenseBattleActionType.Ability,
                current.PersistentId,
                ability.Id);
            return;
        }

        selectedAction = OffenseBattleActionType.Ability;
        selectedAbilityId = ability.Id;
        previewTargetId = string.Empty;
        statusMessage = ability.TargetRule == OffenseBattleTargetRule.Ally
            ? "능력을 적용할 아군을 선택하세요."
            : "능력을 사용할 적을 선택하세요.";
        Refresh();
    }

    private void SelectTarget(OffenseBattleCombatant target)
    {
        OffenseBattleCombatant current = runtime?.Session?.CurrentActor;
        if (!IsSelectableTarget(current, target))
        {
            statusMessage = "이 행동의 대상이 아닙니다.";
            Refresh();
            return;
        }

        if (selectedAction == OffenseBattleActionType.BasicAttack)
        {
            previewTargetId = target.PersistentId;
            statusMessage = BuildAttackPreview(runtime.Session, current, target);
            Refresh();
            return;
        }

        ExecuteImmediate(selectedAction, target.PersistentId, selectedAbilityId);
    }

    private void ExecuteImmediate(
        OffenseBattleActionType action,
        string targetId,
        string abilityId)
    {
        if (runtime.TryIssuePlayerCommand(action, targetId, abilityId, out OffenseBattleCommandResult result))
        {
            statusMessage = result.Message;
            selectedAction = OffenseBattleActionType.BasicAttack;
            selectedAbilityId = string.Empty;
            previewTargetId = string.Empty;
        }
        else
        {
            statusMessage = result?.Message ?? "행동을 실행하지 못했습니다.";
        }

        Refresh();
    }

    private void CreateFireModeButton(
        OffenseBattleCombatant current,
        CombatWeaponSnapshot weapon,
        CombatFireMode mode,
        string label)
    {
        bool supported = mode switch
        {
            CombatFireMode.Aimed => weapon.SupportsAimed,
            CombatFireMode.Rapid => weapon.SupportsRapid,
            CombatFireMode.Suppressive => weapon.SupportsSuppressive,
            _ => false
        };
        if (!supported)
        {
            return;
        }

        CreateActionButton(
            label,
            current.FireMode == mode,
            false,
            () => ExecuteImmediate(
                OffenseBattleActionType.SetFireMode,
                current.PersistentId,
                mode.ToString()));
    }

    private IReadOnlyList<CombatEquipmentInstance> GetAlternateWeapons(
        OffenseBattleCombatant current)
    {
        if (current == null)
        {
            return Array.Empty<CombatEquipmentInstance>();
        }

        return combatEquipment.Instances
            .Where(instance =>
                instance != null
                && string.Equals(
                    instance.ownerCharacterId,
                    current.PersistentId,
                    StringComparison.Ordinal)
                && !string.Equals(
                    instance.instanceId,
                    current.Weapon?.InstanceId,
                    StringComparison.Ordinal)
                && combatEquipmentCatalog.TryGet(
                    instance.definitionId,
                    out CombatEquipmentDefinitionSO definition)
                && definition.Kind is CombatEquipmentKind.MeleeWeapon
                    or CombatEquipmentKind.RangedWeapon
                    or CombatEquipmentKind.RecoverableThrowingWeapon)
            .OrderBy(instance => instance.instanceId, StringComparer.Ordinal)
            .ToArray();
    }

    private void SwitchToNextWeapon(
        OffenseBattleCombatant current,
        IReadOnlyList<CombatEquipmentInstance> candidates)
    {
        CombatEquipmentInstance next = candidates?.FirstOrDefault();
        if (next == null)
        {
            statusMessage = "교체할 무기가 없습니다.";
            Refresh();
            return;
        }

        ExecuteImmediate(
            OffenseBattleActionType.SwitchWeapon,
            current.PersistentId,
            next.instanceId);
    }

    private static string BuildAttackPreview(
        OffenseBattleSession session,
        OffenseBattleCombatant source,
        OffenseBattleCombatant target)
    {
        CombatAttackPreview preview = session.PreviewBasicAttack(source, target);
        int distance = session.GetFormationDistanceForPreview(source, target);
        if (!preview.Valid)
        {
            return $"{target.DisplayName}: {preview.FailureReason}";
        }

        return $"{target.DisplayName} 공격 예상 · {FormatRangeBand(preview.RangeBand)} {distance}칸"
            + $" · 명중 {preview.HitChance * 100f:0}%"
            + $" · 엄폐 {preview.CoverBlockChance * 100f:0}%"
            + $" · 회피 {preview.EvasionChance * 100f:0}%"
            + $" · 적중 피해 {preview.DamageOnHit:0.#}"
            + $" · 기대 피해 {preview.ExpectedDamage:0.#}";
    }

    private static string FormatRangeBand(CombatRangeBand band)
    {
        return band switch
        {
            CombatRangeBand.Contact => "접촉",
            CombatRangeBand.Near => "근거리",
            CombatRangeBand.Medium => "중거리",
            CombatRangeBand.Long => "장거리",
            _ => "사거리 밖"
        };
    }

    private bool IsSelectableTarget(
        OffenseBattleCombatant current,
        OffenseBattleCombatant target)
    {
        if (current == null || target == null || target.IsDead
            || current.Team != OffenseBattleTeam.Allies)
        {
            return false;
        }

        if (selectedAction == OffenseBattleActionType.BasicAttack)
        {
            if (current.Team == target.Team) return false;
            bool hasForwardTarget = runtime.Session.Combatants.Any(candidate =>
                candidate.Team == target.Team
                && !candidate.IsDead
                && candidate.Formation != OffenseFormationSlot.Rear);
            return !hasForwardTarget || target.Formation != OffenseFormationSlot.Rear;
        }

        if (selectedAction != OffenseBattleActionType.Ability)
        {
            return false;
        }

        CharacterCombatAbilityDefinition ability = current.Abilities.FirstOrDefault(value => string.Equals(
            value.Id,
            selectedAbilityId,
            StringComparison.Ordinal));
        if (ability == null
            || !IsPositionAllowed(ability.UsableFrom, current.Formation)
            || !IsPositionAllowed(ability.TargetPositions, target.Formation))
        {
            return false;
        }

        return ability.TargetRule switch
        {
            OffenseBattleTargetRule.Self => ReferenceEquals(current, target),
            OffenseBattleTargetRule.Ally => current.Team == target.Team,
            OffenseBattleTargetRule.Enemy => current.Team != target.Team,
            _ => false
        };
    }

    private void CreateHealthBar(Transform parent, OffenseBattleCombatant combatant)
    {
        GameObject trackObject = new GameObject("HealthTrack", typeof(RectTransform), typeof(Image));
        trackObject.transform.SetParent(parent, false);
        RectTransform trackRect = trackObject.GetComponent<RectTransform>();
        trackRect.anchorMin = new Vector2(0.29f, 0.32f);
        trackRect.anchorMax = new Vector2(0.97f, 0.5f);
        trackRect.offsetMin = Vector2.zero;
        trackRect.offsetMax = Vector2.zero;
        trackObject.GetComponent<Image>().color = DungeonUiTheme.SurfaceMuted;

        GameObject fillObject = new GameObject("Fill", typeof(RectTransform), typeof(Image));
        fillObject.transform.SetParent(trackObject.transform, false);
        RectTransform fillRect = fillObject.GetComponent<RectTransform>();
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = new Vector2(combatant.HealthRatio, 1f);
        fillRect.offsetMin = new Vector2(2f, 2f);
        fillRect.offsetMax = new Vector2(-2f, -2f);
        fillObject.GetComponent<Image>().color = DungeonUiTheme.GetMeterColor(combatant.HealthRatio);

        TMP_Text value = CreateCardText(
            trackObject.transform,
            "Value",
            13f,
            TextAlignmentOptions.Center,
            Vector2.zero,
            Vector2.one);
        value.text = $"{combatant.CurrentHealth:0}/{combatant.Stats.MaxHealth:0}";
    }

    private TMP_Text CreateCardText(
        Transform parent,
        string name,
        float size,
        TextAlignmentOptions alignment,
        Vector2 anchorMin,
        Vector2 anchorMax)
    {
        GameObject textObject = OffensePanelUiFactory.CreateText(parent, name, size, alignment, fontService);
        RectTransform rect = textObject.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        return textObject.GetComponent<TMP_Text>();
    }

    private Sprite ResolvePortrait(OffenseBattleCombatant combatant)
    {
        CharacterSO data = characterCatalog.Characters.FirstOrDefault(value => value != null
            && value.id == combatant.PortraitDataId);
        data ??= characterCatalog.Characters.FirstOrDefault(value => value != null
            && string.Equals(value.SpeciesTag, combatant.SpeciesTag, StringComparison.OrdinalIgnoreCase));
        return data != null ? data.characterSprite : null;
    }

    private string BuildThreatText()
    {
        if (threatProvider.TryGetRuntime(out InvasionThreatRuntime threat))
        {
            return $"던전 실시간 진행  ·  침공 {threat.CurrentStage} {threat.CurrentThreat:0}";
        }

        return "던전 실시간 진행";
    }

    private static string BuildInitiativeText(OffenseBattleSession session)
    {
        return "행동 순서  " + string.Join("  >  ", session.InitiativeOrder
            .Select(id => session.FindCombatant(id))
            .Where(value => value != null && !value.IsDead)
            .Select(value => value.DisplayName));
    }

    private string BuildPrompt(
        OffenseBattleSession session,
        OffenseBattleCombatant current)
    {
        if (session.IsComplete)
        {
            return session.Outcome switch
            {
                OffenseBattleOutcome.Victory => "적을 모두 쓰러뜨렸습니다.",
                OffenseBattleOutcome.Retreated => "원정대가 전투에서 후퇴했습니다.",
                OffenseBattleOutcome.AbortedOwnerDeath => "사장이 쓰러져 전투가 중단됐습니다.",
                _ => "원정대가 패배했습니다."
            };
        }

        if (!string.IsNullOrWhiteSpace(statusMessage)) return statusMessage;
        return current != null
            ? $"{current.DisplayName}의 차례입니다. 행동을 선택하세요."
            : "다음 행동을 처리하고 있습니다.";
    }

    private static string BuildStatusText(OffenseBattleCombatant combatant)
    {
        if (combatant.IsDead) return "사망";
        if (combatant.IsDowned) return "쓰러짐 · 구조 필요";
        string formation = OffenseFormationUtility.GetDisplayName(combatant.Formation);
        CombatWeaponSnapshot weapon = combatant.Weapon ?? CombatWeaponSnapshot.CreateUnarmed();
        string weaponState = weapon.RequiresAmmo
            ? $"탄약 {weapon.LoadedAmmo}/{weapon.MagazineCapacity} · {FormatFireMode(combatant.FireMode)}"
            : weapon.IsRanged
                ? FormatFireMode(combatant.FireMode)
                : "근접";
        if (combatant.Statuses.Count == 0)
        {
            return $"{formation} · {weaponState} · 정상";
        }

        return formation + $" · {weaponState} · " + string.Join(" · ", combatant.Statuses.Select(status => status.Type switch
        {
            OffenseBattleStatusType.Guard => $"방어 {Mathf.RoundToInt(status.Value * 100f)}%",
            OffenseBattleStatusType.Vulnerability => $"취약 {Mathf.RoundToInt(status.Value * 100f)}%",
            OffenseBattleStatusType.DamageOverTime => $"지속 피해 {status.Value:0.#}",
            _ => status.Type.ToString()
        }));
    }

    private static string FormatFireMode(CombatFireMode mode)
    {
        return mode switch
        {
            CombatFireMode.Rapid => "속사",
            CombatFireMode.Suppressive => "제압",
            _ => "조준"
        };
    }

    private static bool IsPositionAllowed(OffenseFormationMask mask, OffenseFormationSlot slot)
    {
        return (mask & OffenseFormationUtility.ToMask(slot)) != 0;
    }

    private void ClearDynamicObjects()
    {
        foreach (GameObject item in dynamicObjects)
        {
            if (item == null) continue;
            item.SetActive(false);
            if (Application.isPlaying) Destroy(item);
            else DestroyImmediate(item);
        }

        dynamicObjects.Clear();
    }
}

public interface IOffenseBattlePanelFactory
{
    OffenseBattlePanel Create();
}

public sealed class OffenseBattlePanelFactory : IOffenseBattlePanelFactory
{
    private readonly ITmpKoreanFontService fontService;
    private readonly IObjectResolver objectResolver;

    public OffenseBattlePanelFactory(
        ITmpKoreanFontService fontService,
        IObjectResolver objectResolver)
    {
        this.fontService = fontService ?? throw new ArgumentNullException(nameof(fontService));
        this.objectResolver = objectResolver ?? throw new ArgumentNullException(nameof(objectResolver));
    }

    public OffenseBattlePanel Create()
    {
        GameObject canvasObject = OffensePanelUiFactory.CreateOverlayCanvas(
            "OffenseBattleCanvas",
            520,
            new Vector2(1920f, 1080f));
        GameObject content = OffensePanelUiFactory.CreatePanel(
            canvasObject.transform,
            "OffenseBattleContent",
            Vector2.zero,
            Vector2.one,
            new Color32(21, 19, 24, 250));

        GameObject stage = OffensePanelUiFactory.CreatePanel(
            content.transform,
            "BattleStage",
            new Vector2(0.018f, 0.18f),
            new Vector2(0.982f, 0.84f),
            new Color32(34, 30, 35, 245));
        Outline stageOutline = stage.AddComponent<Outline>();
        stageOutline.effectColor = new Color32(100, 76, 61, 210);
        stageOutline.effectDistance = new Vector2(1f, -1f);

        TMP_Text header = CreateText(
            content.transform,
            "BattleHeader",
            34f,
            TextAlignmentOptions.Left,
            new Vector2(0.025f, 0.9f),
            new Vector2(0.72f, 0.975f));
        header.color = new Color32(226, 209, 174, 255);
        TMP_Text danger = CreateText(
            content.transform,
            "BattleDanger",
            20f,
            TextAlignmentOptions.Right,
            new Vector2(0.55f, 0.92f),
            new Vector2(0.82f, 0.97f));
        TMP_Text initiative = CreateText(
            content.transform,
            "BattleInitiative",
            19f,
            TextAlignmentOptions.Center,
            new Vector2(0.22f, 0.83f),
            new Vector2(0.78f, 0.9f));
        initiative.color = new Color32(194, 171, 136, 255);

        RectTransform allyRoot = CreateVerticalRoot(
            content.transform,
            "BattleAllies",
            new Vector2(0.025f, 0.2f),
            new Vector2(0.315f, 0.82f));
        RectTransform enemyRoot = CreateVerticalRoot(
            content.transform,
            "BattleEnemies",
            new Vector2(0.685f, 0.2f),
            new Vector2(0.975f, 0.82f));
        TMP_Text log = CreateText(
            content.transform,
            "BattleLog",
            20f,
            TextAlignmentOptions.BottomLeft,
            new Vector2(0.35f, 0.25f),
            new Vector2(0.65f, 0.78f));
        TMP_Text prompt = CreateText(
            content.transform,
            "BattlePrompt",
            21f,
            TextAlignmentOptions.Center,
            new Vector2(0.32f, 0.17f),
            new Vector2(0.68f, 0.25f));
        RectTransform actionRoot = CreateHorizontalRoot(
            content.transform,
            "BattleActions",
            new Vector2(0.025f, 0.035f),
            new Vector2(0.975f, 0.15f));

        GameObject dungeonButton = OffensePanelUiFactory.CreateButton(
            content.GetComponent<RectTransform>(),
            "던전 보기",
            20f,
            null,
            fontService);
        SetTopRight(dungeonButton.GetComponent<RectTransform>(), new Vector2(-28f, -28f), new Vector2(150f, 52f));
        StyleUtilityButton(dungeonButton.GetComponent<Button>());

        GameObject returnButton = OffensePanelUiFactory.CreateButton(
            canvasObject.GetComponent<RectTransform>(),
            "전투 복귀",
            20f,
            null,
            fontService);
        SetTopRight(returnButton.GetComponent<RectTransform>(), new Vector2(-24f, -92f), new Vector2(150f, 52f));
        returnButton.SetActive(false);
        StyleUtilityButton(returnButton.GetComponent<Button>());

        OffenseBattlePanel panel = canvasObject.AddComponent<OffenseBattlePanel>();
        panel.BindGeneratedView(
            content,
            returnButton,
            dungeonButton.GetComponent<Button>(),
            returnButton.GetComponent<Button>(),
            header,
            danger,
            initiative,
            log,
            prompt,
            allyRoot,
            enemyRoot,
            actionRoot);
        objectResolver.Inject(panel);
        return panel;
    }

    private TMP_Text CreateText(
        Transform parent,
        string name,
        float size,
        TextAlignmentOptions alignment,
        Vector2 anchorMin,
        Vector2 anchorMax)
    {
        GameObject textObject = OffensePanelUiFactory.CreateText(parent, name, size, alignment, fontService);
        RectTransform rect = textObject.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        return textObject.GetComponent<TMP_Text>();
    }

    private static RectTransform CreateVerticalRoot(
        Transform parent,
        string name,
        Vector2 anchorMin,
        Vector2 anchorMax)
    {
        GameObject root = new GameObject(name, typeof(RectTransform), typeof(VerticalLayoutGroup));
        root.transform.SetParent(parent, false);
        RectTransform rect = root.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        VerticalLayoutGroup layout = root.GetComponent<VerticalLayoutGroup>();
        layout.spacing = 12f;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandHeight = false;
        return rect;
    }

    private static RectTransform CreateHorizontalRoot(
        Transform parent,
        string name,
        Vector2 anchorMin,
        Vector2 anchorMax)
    {
        GameObject root = new GameObject(name, typeof(RectTransform), typeof(HorizontalLayoutGroup));
        root.transform.SetParent(parent, false);
        RectTransform rect = root.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        HorizontalLayoutGroup layout = root.GetComponent<HorizontalLayoutGroup>();
        layout.spacing = 10f;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = true;
        return rect;
    }

    private static void SetTopRight(RectTransform rect, Vector2 position, Vector2 size)
    {
        rect.anchorMin = Vector2.one;
        rect.anchorMax = Vector2.one;
        rect.pivot = Vector2.one;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    private static void StyleUtilityButton(Button button)
    {
        if (button == null) return;
        Image image = button.GetComponent<Image>();
        image.color = new Color32(45, 43, 48, 255);
        Outline outline = button.GetComponent<Outline>() ?? button.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color32(131, 106, 73, 220);
        outline.effectDistance = new Vector2(1f, -1f);
    }
}

public sealed class OffenseBattleUiController : IStartable, IDisposable
{
    private readonly IOffenseBattleRuntime battleRuntime;
    private readonly IOffenseBattlePanelFactory panelFactory;
    private OffenseBattlePanel panel;

    public OffenseBattleUiController(
        IOffenseBattleRuntime battleRuntime,
        IOffenseBattlePanelFactory panelFactory)
    {
        this.battleRuntime = battleRuntime ?? throw new ArgumentNullException(nameof(battleRuntime));
        this.panelFactory = panelFactory ?? throw new ArgumentNullException(nameof(panelFactory));
    }

    public void Start()
    {
        battleRuntime.StateChanged += Refresh;
        Refresh();
    }

    public void Dispose()
    {
        battleRuntime.StateChanged -= Refresh;
    }

    private void Refresh()
    {
        if (battleRuntime.Session == null)
        {
            if (panel != null) panel.HideAll();
            return;
        }

        panel ??= panelFactory.Create();
        panel.Bind(battleRuntime);
        panel.Refresh();
    }
}
