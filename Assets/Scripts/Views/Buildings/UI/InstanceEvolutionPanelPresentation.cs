using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public sealed class InstanceEvolutionPanelPresentation
{
    private readonly IEvolutionModuleRegistry modules;
    private readonly ICombatEquipmentRuntime equipment;

    public InstanceEvolutionPanelPresentation(
        IEvolutionModuleRegistry modules,
        ICombatEquipmentRuntime equipment)
    {
        this.modules = modules ?? throw new ArgumentNullException(nameof(modules));
        this.equipment = equipment ?? throw new ArgumentNullException(nameof(equipment));
    }

    public string GetEquipmentName(CombatEquipmentInstance instance)
    {
        string definitionName = equipment.TryGetDefinition(
                instance.definitionId,
                out CombatEquipmentDefinitionSO definition)
            ? definition.DisplayName
            : instance.definitionId;
        return $"{definitionName} ({CombatQualityRules.GetDisplayName(instance.quality)})";
    }

    public string ResolveNodeName(EvolutionNode node)
    {
        if (!string.IsNullOrWhiteSpace(node.displayName))
        {
            return node.displayName;
        }

        return modules.TryGet(node.effectId, out EvolutionModuleDefinition module)
            ? module.DisplayName
            : FormatEffectId(node.effectId);
    }

    public string FormatModulePair(EvolutionNode node)
    {
        return $"{FormatModule(node.effectId, true)} / "
            + $"{FormatModule(node.burdenEffectId, false)}";
    }

    public string FormatModule(string moduleId, bool benefit)
    {
        if (!modules.TryGet(moduleId, out EvolutionModuleDefinition definition))
        {
            return FormatEffectId(moduleId);
        }

        IReadOnlyList<EvolutionEffectModifier> modifiers = benefit
            ? definition.Benefits
            : definition.Burdens;
        string values = string.Join(", ", modifiers.Select(modifier =>
            $"{FormatEffectId(modifier.statId)} {FormatModifier(modifier)}"));
        return $"{definition.DisplayName}: {values}";
    }

    public static string FormatModifier(EvolutionEffectModifier modifier)
    {
        List<string> parts = new List<string>();
        if (!Mathf.Approximately(modifier.multiplier, 1f))
        {
            parts.Add($"×{modifier.multiplier:0.00}");
        }
        if (!Mathf.Approximately(modifier.additive, 0f))
        {
            parts.Add($"{modifier.additive:+0.##;-0.##}");
        }
        return parts.Count > 0 ? string.Join(" ", parts) : "변화";
    }

    public static string FormatActivationRule(EvolutionModuleActivationRule rule)
    {
        if (rule == null || rule.kind == EvolutionModuleActivationKind.Always)
        {
            return "방과 무관하게 적용";
        }

        List<string> parts = new List<string>();
        if (rule.requiredRoomTags.Count > 0) parts.Add("필수 " + string.Join("+", rule.requiredRoomTags));
        if (rule.forbiddenRoomTags.Count > 0) parts.Add("금지 " + string.Join("+", rule.forbiddenRoomTags));
        if (rule.minimumCleanliness > 0f) parts.Add($"청결 {rule.minimumCleanliness:0}+");
        if (rule.minimumBeauty > 0f) parts.Add($"미관 {rule.minimumBeauty:0}+");
        if (rule.minimumSpace > 0f) parts.Add($"공간 {rule.minimumSpace:0}+");
        if (rule.minimumTemperature > 0f) parts.Add($"온도 {rule.minimumTemperature:0}+");
        return parts.Count == 0 ? "방 조건 필요" : string.Join(" · ", parts);
    }

    public static string FormatCandidateKind(FacilityGenerationCandidateKind kind) => kind switch
    {
        FacilityGenerationCandidateKind.PrimaryRole => "주력 역할 강화",
        FacilityGenerationCandidateKind.RoomSynergy => "방 시너지 결합",
        FacilityGenerationCandidateKind.RiskyCatalyst => "고위험 촉매 개조",
        _ => "시설 개조"
    };

    public static string FormatDirection(EquipmentEvolutionDirection direction) => direction switch
    {
        EquipmentEvolutionDirection.Melee => "근접 특화",
        EquipmentEvolutionDirection.Ranged => "원거리 특화",
        EquipmentEvolutionDirection.Accuracy => "명중 특화",
        EquipmentEvolutionDirection.Execution => "처형 특화",
        EquipmentEvolutionDirection.Interception => "저지 특화",
        EquipmentEvolutionDirection.Protection => "보호 특화",
        EquipmentEvolutionDirection.Survival => "생존 특화",
        _ => "균형"
    };

    public static string FormatCatalystFamily(string family)
    {
        string normalized = family?.ToLowerInvariant() ?? string.Empty;
        if (normalized.Contains("offense")) return "공세";
        if (normalized.Contains("defense")) return "방어";
        if (normalized.Contains("industry")) return "산업";
        if (normalized.Contains("survival")) return "생존";
        if (normalized.Contains("arcane")) return "비전";
        if (normalized.Contains("authority")) return "권위";
        return string.IsNullOrWhiteSpace(family) ? "범용" : family;
    }

    public static string ResolveFacilityModuleForCatalyst(string family)
    {
        string normalized = family?.ToLowerInvariant() ?? string.Empty;
        if (normalized.Contains("offense") || normalized.Contains("defense")) return "facility:defense";
        if (normalized.Contains("survival")) return "facility:survival";
        if (normalized.Contains("arcane")) return "facility:research";
        if (normalized.Contains("authority")) return "facility:service";
        return "facility:output";
    }

    public static string FormatEffectId(string effectId) => effectId switch
    {
        "work.output" => "작업 산출", "service.speed" => "서비스 속도",
        "research.output" => "연구 산출", "survival.output" => "생존 지원",
        "defense.output" => "방어 성능", "entertainment.output" => "흥행 성능",
        "room.synergy" => "방 시너지", "fuel.use" => "연료 소비",
        "staff.required" => "필요 인력", "heat.output" => "발열",
        "maintenance.work" => "유지 작업", "space.use" => "공간 부담",
        "accident.risk" => "사고 위험", "combat.damage" => "피해",
        "combat.accuracy" => "명중", "combat.reload" => "재장전 부담",
        "combat.defense" => "방어", "combat.move" => "이동",
        "combat.durability" => "내구", "combat.value" => "가치",
        "combat.weight" => "무게", "combat.accident" => "전투 사고 위험",
        _ => string.IsNullOrWhiteSpace(effectId) ? "없음" : effectId
    };

    public static string FormatRelocationPhase(FacilityRelocationPhase phase) => phase switch
    {
        FacilityRelocationPhase.Dismantling => "해체 중",
        FacilityRelocationPhase.WaitingForPackage => "포장 운반 중",
        FacilityRelocationPhase.Reinstalling => "재설치 중",
        FacilityRelocationPhase.Blocked => "막힘",
        _ => phase.ToString()
    };

    public static string FormatOrderState(EvolutionReforgeOrderState state) => state switch
    {
        EvolutionReforgeOrderState.WaitingForMaterials => "재료 운반 중",
        EvolutionReforgeOrderState.Ready => "작업 대기",
        EvolutionReforgeOrderState.InProgress => "작업 중",
        EvolutionReforgeOrderState.Completed => "완료",
        EvolutionReforgeOrderState.Cancelled => "취소",
        EvolutionReforgeOrderState.Blocked => "막힘",
        _ => state.ToString()
    };

    public static string FormatOverclockTier(OverclockTier tier) => tier switch
    {
        OverclockTier.Controlled => "통제",
        OverclockTier.Aggressive => "공격적",
        OverclockTier.Critical => "임계",
        _ => "없음"
    };

    public static bool TryGetCatalyst(
        string itemId,
        out EquipmentCatalystDefinition catalyst)
    {
        return EvolutionCatalystItemId.TryParseCatalyst(itemId, out catalyst);
    }
}
