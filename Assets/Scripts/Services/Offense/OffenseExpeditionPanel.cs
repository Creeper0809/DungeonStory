using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using TMPro;
using UnityEngine;
using VContainer;

public class OffenseExpeditionPanel : MonoBehaviour
{
    private const int MaximumPartySize = 5;

    private enum JourneyButtonStyle
    {
        Action,
        Route,
        Supply,
        Danger,
        Close
    }

    private OffenseExpeditionRuntime runtime;
    private TMP_Text headerText;
    private TMP_Text detailText;
    private RectTransform memberButtonRoot;
    private readonly List<GameObject> spawnedButtons = new List<GameObject>();
    private readonly List<CharacterActor> selectedMembers = new List<CharacterActor>();
    private readonly Dictionary<OffenseSupplyType, int> selectedSupplies =
        Enum.GetValues(typeof(OffenseSupplyType))
            .Cast<OffenseSupplyType>()
            .ToDictionary(type => type, _ => 0);
    private IOffenseCampaignQuery campaign;
    private string statusMessage;
    private IOffensePanelButtonFactory buttonFactory;

    public void Bind(
        OffenseExpeditionRuntime source,
        IOffenseCampaignQuery campaign,
        IOffensePanelButtonFactory buttonFactory)
    {
        if (!ReferenceEquals(runtime, source))
        {
            if (runtime != null) runtime.StateChanged -= Render;
            runtime = source ?? throw new ArgumentNullException(nameof(source));
            runtime.StateChanged += Render;
        }
        this.campaign = campaign
            ?? throw new ArgumentNullException(nameof(campaign));
        this.buttonFactory = buttonFactory
            ?? throw new ArgumentNullException(nameof(buttonFactory));
        EnsureView();
        gameObject.SetActive(true);
        Render();
    }

    public void Render()
    {
        if (runtime == null)
        {
            return;
        }

        EnsureView();
        ClearButtons();
        if (runtime.ActiveExpeditions.Count > 0)
        {
            RenderJourney(runtime.ActiveExpeditions[0]);
            return;
        }

        string selectedTargetId = campaign.State.SelectedTargetId;
        OffenseTargetSnapshot target = null;
        if (!string.IsNullOrWhiteSpace(selectedTargetId))
        {
            campaign.TryGetKnownTargetSnapshot(selectedTargetId, out target);
        }

        headerText.text = target != null
            ? $"전투 편성 / 대상: {target.title} / 필요 {target.requiredMembers}명 / 최대 {MaximumPartySize}명"
            : "원정 편성 / 선택된 대상 없음";

        foreach (CharacterActor member in runtime.GetAvailableMemberActors())
        {
            CharacterActor captured = member;
            string label = BuildMemberLabel(captured);
            GameObject buttonObject = RequireButtonFactory().CreateButton(
                memberButtonRoot,
                selectedMembers.Contains(captured) ? $"[선택] {label}" : label,
                16f,
                () =>
                {
                    if (selectedMembers.Contains(captured))
                    {
                        selectedMembers.Remove(captured);
                    }
                    else if (selectedMembers.Count < MaximumPartySize)
                    {
                        selectedMembers.Add(captured);
                    }
                    else
                    {
                        statusMessage = $"원정대는 최대 {MaximumPartySize}명입니다.";
                    }

                    Render();
                });
            spawnedButtons.Add(buttonObject);
            buttonObject.GetComponent<UnityEngine.UI.LayoutElement>().preferredHeight = 38f;
            StyleJourneyButton(
                buttonObject,
                selectedMembers.Contains(captured) ? JourneyButtonStyle.Supply : JourneyButtonStyle.Action);
        }

        OffensePreparationSnapshot preparation = runtime.GetPreparationSnapshot();
        foreach (OffenseSupplyType type in Enum.GetValues(typeof(OffenseSupplyType)))
        {
            OffenseSupplyType capturedType = type;
            int selected = selectedSupplies[type];
            int available = preparation.GetAvailable(type);
            AddButton(
                $"{OffenseSupplyCatalog.GetDisplayName(type)} {selected}/{available}  +",
                () => IncrementSupply(capturedType, preparation),
                JourneyButtonStyle.Supply);
        }

        if (selectedSupplies.Values.Any(value => value > 0))
        {
            AddButton("보급 초기화", () => ResetSupplies(), JourneyButtonStyle.Action);
        }

        AddButton(
            "원정 출발",
            () =>
            {
                if (string.IsNullOrWhiteSpace(selectedTargetId))
                {
                    statusMessage = "선택된 원정 대상이 없습니다.";
                    Render();
                    return;
                }

                OffenseSupplyLoadout loadout = new OffenseSupplyLoadout(selectedSupplies);
                if (runtime.TryStartExpedition(
                    selectedTargetId,
                    selectedMembers,
                    loadout,
                    preparation.Preparation,
                    out _,
                    out string message))
                {
                    selectedMembers.Clear();
                    ResetSupplies(render: false);
                    statusMessage = message;
                }
                else
                {
                    statusMessage = message;
                }

                Render();
            }, JourneyButtonStyle.Route);
        AddButton("닫기", Hide, JourneyButtonStyle.Close);

        RenderEquipmentButtons();

        float selectedPower = target != null
            ? runtime.CalculatePartyPower(selectedMembers)
            : 0f;
        string detail = target != null
            ? $"{target.ToDetailText()}\n\n선택 인원: {selectedMembers.Count}/3"
                + $"\n원정대 전투력: {selectedPower:0.#} / 권장 {target.requiredPower:0.#}"
                + $"\n보급: {selectedSupplies.Values.Sum()}/{preparation.Preparation.SupplyCapacity}"
                + $"\n시작 조명 {preparation.Preparation.StartingLight:0}"
                + $" · 정찰 {preparation.Preparation.Scouting}"
                + $"\n야영 회복 {preparation.Preparation.CampHealRatio * 100f:0}%"
                + $" · 스트레스 -{preparation.Preparation.CampStressRecovery:0}"
                + BuildPreparationSources(preparation.Preparation)
            : $"선택 인원: {selectedMembers.Count}/3";
        string finalDetail = string.IsNullOrWhiteSpace(statusMessage)
            ? detail
            : $"{detail}\n\n{statusMessage}";
        detailText.text = finalDetail + BuildEquipmentDetail(target);
    }

    private void RenderEquipmentButtons()
    {
        if (runtime == null || selectedMembers.Count != 1)
        {
            return;
        }

        CharacterActor member = selectedMembers[0];
        foreach (CombatEquipmentLoadoutSlot slot in Enum.GetValues(typeof(CombatEquipmentLoadoutSlot)))
        {
            CombatEquipmentLoadoutSlot capturedSlot = slot;
            if (!runtime.TryGetEquippedEquipment(member, slot, out _))
            {
                continue;
            }

            AddButton(
                $"{GetEquipmentSlotName(slot)} 해제",
                () =>
                {
                    runtime.TryUnequipEquipment(member, capturedSlot, out statusMessage);
                    Render();
                },
                JourneyButtonStyle.Action);
        }

        foreach (CombatEquipmentDefinitionSO definition in runtime.GetEquipmentDefinitions()
            .Where(definition => definition != null
                && !string.IsNullOrWhiteSpace(definition.EquipmentId)
                && runtime.GetAvailableEquipmentCount(definition.EquipmentId) > 0)
            .OrderBy(definition => definition.Kind)
            .ThenBy(definition => definition.DisplayName, StringComparer.Ordinal))
        {
            CombatEquipmentDefinitionSO captured = definition;
            AddButton(
                $"{GetEquipmentSlotName(definition.Kind)} 장착: {definition.DisplayName} x{runtime.GetAvailableEquipmentCount(definition.EquipmentId)}",
                () =>
                {
                    runtime.TryEquipEquipment(member, captured.EquipmentId, out statusMessage);
                    Render();
                },
                JourneyButtonStyle.Supply);
        }
    }

    private string BuildEquipmentDetail(OffenseTargetSnapshot target)
    {
        if (runtime == null)
        {
            return string.Empty;
        }

        List<string> lines = new List<string> { "장비" };

        foreach (CharacterActor member in selectedMembers)
        {
            string readiness = OffenseExpeditionService.CanJoinExpedition(member, out string reason)
                ? "출정 가능"
                : $"출정 불가: {reason}";
            CombatEquipmentDefinitionSO weapon = runtime.TryGetEquippedEquipment(
                member,
                CombatEquipmentLoadoutSlot.Weapon,
                out CombatEquipmentDefinitionSO equippedWeapon)
                    ? equippedWeapon
                    : null;
            CombatEquipmentDefinitionSO armor = runtime.TryGetEquippedEquipment(
                member,
                CombatEquipmentLoadoutSlot.Armor,
                out CombatEquipmentDefinitionSO equippedArmor)
                    ? equippedArmor
                    : null;
            lines.Add(
                $"{GetActorName(member)} - 무기 {GetEquipmentName(weapon)}, 방어구 {GetEquipmentName(armor)}"
                + $" / {readiness}");
        }

        if (target != null && selectedMembers.Count < target.requiredMembers)
        {
            lines.Add($"인원 부족: {selectedMembers.Count}/{target.requiredMembers}");
        }

        string inventory = string.Join(", ", runtime.GetEquipmentDefinitions()
            .Where(definition => definition != null && !string.IsNullOrWhiteSpace(definition.EquipmentId))
            .Select(definition =>
                $"{definition.DisplayName} {runtime.GetAvailableEquipmentCount(definition.EquipmentId)}/{GetOwnedEquipmentCount(definition.EquipmentId)}"));
        lines.Add(string.IsNullOrWhiteSpace(inventory) ? "재고 없음" : $"재고: {inventory}");

        string queue = string.Join(", ", runtime.GetEquipmentCraftQueue()
            .Where(order => order != null && !string.IsNullOrWhiteSpace(order.definitionId))
            .Select(order =>
            {
                string name = runtime.GetEquipmentDefinitions()
                    .FirstOrDefault(definition => string.Equals(definition.EquipmentId, order.definitionId, StringComparison.Ordinal))
                    ?.DisplayName ?? order.definitionId;
                return $"{name} 작업량 {order.RemainingWork:0.#}";
            }));
        if (!string.IsNullOrWhiteSpace(queue))
        {
            lines.Add($"제작 대기: {queue}");
        }

        return "\n\n" + string.Join("\n", lines);
    }

    private int GetOwnedEquipmentCount(string equipmentId)
    {
        IReadOnlyDictionary<string, int> inventory = runtime?.GetEquipmentInventory();
        return inventory != null && inventory.TryGetValue(equipmentId, out int count)
            ? count
            : 0;
    }

    public void Hide()
    {
        gameObject.SetActive(false);
    }

    private void OnDestroy()
    {
        if (runtime != null) runtime.StateChanged -= Render;
    }

    private void RenderJourney(OffenseExpeditionRun expedition)
    {
        OffenseRouteNode current = expedition.CurrentNode;
        headerText.text = $"{expedition.Target.title}  ·  {GetPhaseName(expedition.Phase)}";
        detailText.text = BuildJourneyDetail(expedition, current);

        if (expedition.Phase == OffenseExpeditionPhase.ChoosingRoute)
        {
            foreach (OffenseRouteNode node in expedition.GetAvailableRouteNodes())
            {
                OffenseRouteNode captured = node;
                AddButton($"{GetNodeIcon(node.Kind)} {node.Title}", () =>
                {
                    statusMessage = runtime.TryChooseRouteNode(
                        expedition.ExpeditionId,
                        captured.Id,
                        out string message)
                            ? message
                            : message;
                    Render();
                }, node.IsBoss ? JourneyButtonStyle.Danger : JourneyButtonStyle.Route);
            }

            if (expedition.Supplies.Get(OffenseSupplyType.Rations) > 0)
            {
                AddButton("식량 나누기  ·  스트레스 회복", () =>
                    UseSupply(expedition, OffenseSupplyType.Rations, -1), JourneyButtonStyle.Supply);
            }
            if (expedition.Supplies.Get(OffenseSupplyType.ManaLantern) > 0
                && expedition.Light < 100f)
            {
                AddButton("마력등 밝히기  ·  조명 +35", () =>
                    UseSupply(expedition, OffenseSupplyType.ManaLantern, -1), JourneyButtonStyle.Supply);
            }
            if (expedition.Supplies.Get(OffenseSupplyType.Medicine) > 0)
            {
                for (int index = 0; index < expedition.MemberStates.Count; index++)
                {
                    int capturedIndex = index;
                    OffenseExpeditionMemberState member = expedition.MemberStates[index];
                    if (!member.IsAlive || member.Actor.CurrentHealth >= member.Actor.MaxHealth) continue;
                    AddButton($"{GetActorName(member.Actor)} 치료", () =>
                        UseSupply(expedition, OffenseSupplyType.Medicine, capturedIndex), JourneyButtonStyle.Supply);
                }
            }

            for (int index = 0; index + 1 < expedition.MemberStates.Count; index++)
            {
                int capturedIndex = index;
                AddButton(
                    $"{OffenseFormationUtility.GetDisplayName(expedition.MemberStates[index].Formation)}"
                    + $" ↔ {OffenseFormationUtility.GetDisplayName(expedition.MemberStates[index + 1].Formation)}",
                    () =>
                    {
                        runtime.TrySwapFormation(
                            expedition.ExpeditionId,
                            capturedIndex,
                            capturedIndex + 1,
                            out statusMessage);
                        Render();
                    }, JourneyButtonStyle.Action);
            }

            AddButton("원정 철수", () =>
            {
                runtime.TryRetreat(expedition.ExpeditionId, out statusMessage);
                Render();
            }, JourneyButtonStyle.Danger);
        }
        else if (expedition.Phase == OffenseExpeditionPhase.ResolvingNode)
        {
            if (current?.Kind == OffenseRouteNodeKind.Cache)
            {
                AddNodeResolutionButton(expedition, "보급고 수색", false);
            }
            else
            {
                string supplyChoice = current?.Kind == OffenseRouteNodeKind.Camp
                    ? "야영하기  ·  식량 2"
                    : "원정 도구 사용";
                string riskChoice = current?.Kind == OffenseRouteNodeKind.Camp
                    ? "쉬지 않고 전진"
                    : "위험 감수";
                AddNodeResolutionButton(expedition, supplyChoice, true);
                AddNodeResolutionButton(expedition, riskChoice, false);
            }
        }

        AddButton("닫기", Hide, JourneyButtonStyle.Close);
    }

    private void AddNodeResolutionButton(
        OffenseExpeditionRun expedition,
        string label,
        bool useSupply)
    {
        AddButton(label, () =>
        {
            runtime.TryResolveCurrentNode(
                expedition.ExpeditionId,
                useSupply,
                out _,
                out statusMessage);
            Render();
        }, useSupply ? JourneyButtonStyle.Supply : JourneyButtonStyle.Route);
    }

    private void UseSupply(
        OffenseExpeditionRun expedition,
        OffenseSupplyType type,
        int memberIndex)
    {
        runtime.TryUseSupply(expedition.ExpeditionId, type, memberIndex, out statusMessage);
        Render();
    }

    private void IncrementSupply(
        OffenseSupplyType type,
        OffensePreparationSnapshot preparation)
    {
        int current = selectedSupplies[type];
        int totalWithoutCurrent = selectedSupplies.Values.Sum() - current;
        int maximum = Mathf.Min(
            preparation.GetAvailable(type),
            Mathf.Max(0, preparation.Preparation.SupplyCapacity - totalWithoutCurrent));
        selectedSupplies[type] = current >= maximum ? 0 : current + 1;
        statusMessage = string.Empty;
        Render();
    }

    private void ResetSupplies(bool render = true)
    {
        foreach (OffenseSupplyType type in selectedSupplies.Keys.ToArray())
        {
            selectedSupplies[type] = 0;
        }
        if (render) Render();
    }

    private void AddButton(
        string label,
        Action callback,
        JourneyButtonStyle style = JourneyButtonStyle.Action)
    {
        GameObject button = RequireButtonFactory().CreateButton(
            memberButtonRoot,
            label,
            15f,
            callback);
        button.GetComponent<UnityEngine.UI.LayoutElement>().preferredHeight = 38f;
        StyleJourneyButton(button, style);
        spawnedButtons.Add(button);
    }

    private static void StyleJourneyButton(GameObject buttonObject, JourneyButtonStyle style)
    {
        if (buttonObject == null) return;
        UnityEngine.UI.Button button = buttonObject.GetComponent<UnityEngine.UI.Button>();
        UnityEngine.UI.Image image = buttonObject.GetComponent<UnityEngine.UI.Image>();
        Color baseColor = style switch
        {
            JourneyButtonStyle.Route => new Color32(96, 45, 42, 255),
            JourneyButtonStyle.Supply => new Color32(42, 72, 60, 255),
            JourneyButtonStyle.Danger => new Color32(122, 39, 40, 255),
            JourneyButtonStyle.Close => new Color32(40, 39, 44, 255),
            _ => new Color32(54, 52, 57, 255)
        };
        image.color = baseColor;
        button.colors = new UnityEngine.UI.ColorBlock
        {
            normalColor = Color.white,
            highlightedColor = new Color(1.15f, 1.12f, 1.05f, 1f),
            pressedColor = new Color(0.72f, 0.7f, 0.68f, 1f),
            selectedColor = Color.white,
            disabledColor = new Color(0.35f, 0.35f, 0.35f, 0.7f),
            colorMultiplier = 1f,
            fadeDuration = 0.06f
        };
        UnityEngine.UI.Outline outline = buttonObject.GetComponent<UnityEngine.UI.Outline>()
            ?? buttonObject.AddComponent<UnityEngine.UI.Outline>();
        outline.effectColor = style is JourneyButtonStyle.Route or JourneyButtonStyle.Danger
            ? new Color32(188, 151, 83, 220)
            : new Color32(102, 96, 105, 180);
        outline.effectDistance = new Vector2(1f, -1f);
    }

    private string BuildJourneyDetail(OffenseExpeditionRun expedition, OffenseRouteNode current)
    {
        List<string> lines = new List<string>
        {
            $"현재 위치  {current?.Title ?? "입구"}",
            $"조명  {expedition.Light:0}/100  ·  전리품 {expedition.CarriedStock.Values.Sum()}",
            $"보급  식량 {expedition.Supplies.Get(OffenseSupplyType.Rations)}"
                + $"  치료약 {expedition.Supplies.Get(OffenseSupplyType.Medicine)}"
                + $"  도구 {expedition.Supplies.Get(OffenseSupplyType.Tools)}"
                + $"  마력등 {expedition.Supplies.Get(OffenseSupplyType.ManaLantern)}",
            string.Empty,
            "원정대"
        };
        foreach (OffenseExpeditionMemberState member in expedition.MemberStates.OrderBy(value => value.Formation))
        {
            lines.Add(
                $"{OffenseFormationUtility.GetDisplayName(member.Formation)}  Lv.{member.Actor.Progression?.Level ?? 1}  {GetActorName(member.Actor)}"
                + $"  체력 {member.Actor.CurrentHealth:0}/{member.Actor.MaxHealth:0}"
                + $"  스트레스 {member.Stress:0}");
        }

        lines.Add(string.Empty);
        lines.Add("경로");
        int currentDepth = current?.Depth ?? 0;
        foreach (IGrouping<int, OffenseRouteNode> depth in expedition.Route.Nodes
            .OrderBy(node => node.Depth)
            .ThenBy(node => node.Lane)
            .GroupBy(node => node.Depth))
        {
            lines.Add(string.Join("  /  ", depth.Select(node =>
            {
                if (expedition.CompletedNodeIds.Contains(node.Id)) return $"완료 {node.Title}";
                if (string.Equals(node.Id, expedition.CurrentNodeId, StringComparison.Ordinal)) return $"현재 {node.Title}";
                if (node.Depth > currentDepth + 1 + expedition.Preparation.Scouting) return "미확인";
                return node.Title;
            })));
        }

        if (!string.IsNullOrWhiteSpace(current?.Description))
        {
            lines.Add(string.Empty);
            lines.Add(current.Description);
        }
        if (!string.IsNullOrWhiteSpace(statusMessage))
        {
            lines.Add(string.Empty);
            lines.Add(statusMessage);
        }
        return string.Join("\n", lines);
    }

    private static string BuildPreparationSources(OffenseExpeditionPreparation preparation)
    {
        return preparation.SourceSummaries.Count > 0
            ? $"\n지원 시설: {string.Join(", ", preparation.SourceSummaries)}"
            : "\n지원 시설 없음";
    }

    private static string GetPhaseName(OffenseExpeditionPhase phase)
    {
        return phase switch
        {
            OffenseExpeditionPhase.ChoosingRoute => "경로 선택",
            OffenseExpeditionPhase.ResolvingNode => "현장 판단",
            OffenseExpeditionPhase.InBattle => "교전 중",
            OffenseExpeditionPhase.Completed => "완수",
            OffenseExpeditionPhase.Retreated => "철수",
            _ => "패배"
        };
    }

    private static string GetNodeIcon(OffenseRouteNodeKind kind)
    {
        return kind switch
        {
            OffenseRouteNodeKind.Battle => "교전",
            OffenseRouteNodeKind.Event => "사건",
            OffenseRouteNodeKind.Camp => "야영",
            OffenseRouteNodeKind.Cache => "보급",
            OffenseRouteNodeKind.Boss => "목표",
            _ => "입구"
        };
    }

    private static string BuildMemberLabel(CharacterActor member)
    {
        if (member == null)
        {
            return "알 수 없음";
        }

        member.EnsureRuntimeState();
        CharacterIdentity identity = member.Identity;
        string name = identity != null ? identity.DisplayName : member.name;
        string speciesTag = identity != null ? identity.SpeciesTag : string.Empty;
        int level = member.Progression != null ? member.Progression.Level : 1;
        return $"Lv.{level} / {name} / {speciesTag} / 체력 {member.CurrentHealth:0}/{member.MaxHealth:0}";
    }

    private static string GetEquipmentSlotName(CombatEquipmentKind kind)
    {
        return kind is CombatEquipmentKind.MeleeWeapon
            or CombatEquipmentKind.RangedWeapon
            or CombatEquipmentKind.RecoverableThrowingWeapon
            ? "무기"
            : "방어구";
    }

    private static string GetEquipmentSlotName(CombatEquipmentLoadoutSlot slot)
    {
        return slot == CombatEquipmentLoadoutSlot.Weapon ? "무기" : "방어구";
    }

    private static string GetEquipmentName(CombatEquipmentDefinitionSO definition)
    {
        return definition != null && !string.IsNullOrWhiteSpace(definition.DisplayName)
            ? definition.DisplayName
            : "없음";
    }

    private static string FormatEquipmentStats(CombatEquipmentUiStatBlock stats)
    {
        if (stats == null)
        {
            return "보정 없음";
        }

        List<string> parts = new List<string>();
        AddStat(parts, "체력", stats.maxHealth);
        AddStat(parts, "공격", stats.attack);
        AddStat(parts, "근력", stats.strength);
        AddStat(parts, "맷집", stats.toughness);
        AddStat(parts, "민첩", stats.dexterity);
        AddStat(parts, "이동", stats.moveSpeed);
        return parts.Count > 0 ? string.Join(" ", parts) : "보정 없음";
    }

    private static void AddStat(ICollection<string> parts, string label, int value)
    {
        if (value != 0)
        {
            parts.Add($"{label} {value:+#;-#;0}");
        }
    }

    private static string GetActorName(CharacterActor actor)
    {
        actor?.EnsureRuntimeState();
        return actor != null && actor.Identity != null
            ? actor.Identity.DisplayName
            : actor != null ? actor.name : "대원";
    }

    private void EnsureView()
    {
        if (headerText != null && detailText != null && memberButtonRoot != null) return;

        TMP_Text[] texts = GetComponentsInChildren<TMP_Text>(true);
        headerText = texts.FirstOrDefault((text) => text.name == "OffenseExpeditionHeader");
        detailText = texts.FirstOrDefault((text) => text.name == "OffenseExpeditionDetail");
        memberButtonRoot = GetComponentsInChildren<RectTransform>(true)
            .FirstOrDefault((rect) => rect.name == "OffenseExpeditionMembers");
    }

    private void ClearButtons()
    {
        foreach (GameObject button in spawnedButtons)
        {
            if (button != null)
            {
                RequireButtonFactory().Release(button);
            }
        }

        spawnedButtons.Clear();
    }

    internal void BindGeneratedView(
        TMP_Text headerText,
        TMP_Text detailText,
        RectTransform memberButtonRoot)
    {
        this.headerText = headerText != null
            ? headerText
            : throw new ArgumentNullException(nameof(headerText));
        this.detailText = detailText != null
            ? detailText
            : throw new ArgumentNullException(nameof(detailText));
        this.memberButtonRoot = memberButtonRoot != null
            ? memberButtonRoot
            : throw new ArgumentNullException(nameof(memberButtonRoot));
    }

    private IOffensePanelButtonFactory RequireButtonFactory()
    {
        return buttonFactory
            ?? throw new InvalidOperationException(
                $"{nameof(OffenseExpeditionPanel)} requires {nameof(IOffensePanelButtonFactory)} binding.");
    }
}
