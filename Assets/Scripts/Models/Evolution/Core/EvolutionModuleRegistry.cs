using System;
using System.Collections.Generic;
using System.Linq;
using VContainer;

public sealed class EquipmentHistoricalEffectDefinition
{
    public EquipmentHistoricalEffectDefinition(
        string effectId,
        string displayName,
        string description)
    {
        EffectId = Require(effectId, nameof(effectId));
        DisplayName = Require(displayName, nameof(displayName));
        Description = Require(description, nameof(description));
    }

    public string EffectId { get; }
    public string DisplayName { get; }
    public string Description { get; }

    private static string Require(string value, string parameterName)
    {
        string normalized = value?.Trim() ?? string.Empty;
        if (normalized.Length == 0)
        {
            throw new ArgumentException(
                "Equipment historical effect text cannot be blank.",
                parameterName);
        }
        return normalized;
    }
}

public static class EquipmentHistoricalEffectCatalog
{
    private static readonly IReadOnlyDictionary<string, EquipmentHistoricalEffectDefinition> ById;

    static EquipmentHistoricalEffectCatalog()
    {
        EquipmentHistoricalEffectDefinition[] definitions =
        {
            new EquipmentHistoricalEffectDefinition(
                "equipment:force",
                "위력의 계보",
                "정면에서 힘으로 돌파한 역사"),
            new EquipmentHistoricalEffectDefinition(
                "equipment:precision",
                "정밀의 계보",
                "정확한 타격을 되풀이한 역사"),
            new EquipmentHistoricalEffectDefinition(
                "equipment:cadence",
                "속도의 계보",
                "먼 거리에서 거듭 명중시킨 역사"),
            new EquipmentHistoricalEffectDefinition(
                "equipment:control",
                "제압의 계보",
                "위협을 가로막고 전열을 지킨 역사"),
            new EquipmentHistoricalEffectDefinition(
                "equipment:execution",
                "처형의 계보",
                "결정적인 순간의 마무리를 강화하는 역사"),
            new EquipmentHistoricalEffectDefinition(
                "equipment:durability",
                "내구의 계보",
                "주인과 함께 버티고 살아남은 역사")
        };

        Dictionary<string, EquipmentHistoricalEffectDefinition> byId =
            new Dictionary<string, EquipmentHistoricalEffectDefinition>(StringComparer.Ordinal);
        foreach (EquipmentHistoricalEffectDefinition definition in definitions)
        {
            if (!byId.TryAdd(definition.EffectId, definition))
            {
                throw new InvalidOperationException(
                    $"Duplicate equipment historical effect ID '{definition.EffectId}'.");
            }
        }

        ById = byId;
        All = Array.AsReadOnly(byId.Values
            .OrderBy(definition => definition.EffectId, StringComparer.Ordinal)
            .ToArray());
    }

    public static IReadOnlyList<EquipmentHistoricalEffectDefinition> All { get; }

    public static bool TryGet(
        string effectId,
        out EquipmentHistoricalEffectDefinition definition)
    {
        return ById.TryGetValue(effectId?.Trim() ?? string.Empty, out definition);
    }

    public static EquipmentHistoricalEffectDefinition Require(string effectId)
    {
        if (TryGet(effectId, out EquipmentHistoricalEffectDefinition definition))
        {
            return definition;
        }

        throw new KeyNotFoundException(
            $"Unknown equipment historical effect ID '{effectId ?? string.Empty}'.");
    }
}

public sealed class EvolutionModuleRegistry : IEvolutionModuleRegistry
{
    private readonly Dictionary<string, EvolutionModuleDefinition> byId;

    [Inject]
    public EvolutionModuleRegistry()
        : this(CreateBuiltIns())
    {
    }

    internal EvolutionModuleRegistry(IEnumerable<EvolutionModuleDefinition> modules)
    {
        byId = new Dictionary<string, EvolutionModuleDefinition>(StringComparer.Ordinal);
        foreach (EvolutionModuleDefinition module in modules
                     ?? Array.Empty<EvolutionModuleDefinition>())
        {
            if (module == null)
            {
                continue;
            }

            if (!byId.TryAdd(module.ModuleId, module))
            {
                throw new InvalidOperationException(
                    $"Duplicate evolution module ID '{module.ModuleId}'.");
            }
        }

        foreach (EvolutionModuleDefinition drawback in byId.Values.Where(value =>
                     value.BurdenKind == EvolutionModuleBurdenKind.OptionalDrawback))
        {
            string scope = drawback.ModuleId.Split(':')[0] + ":";
            string[] burdenStats = drawback.Burdens.Select(value => value.statId)
                .Distinct(StringComparer.Ordinal).ToArray();
            string[] sameAxisBenefits = byId.Values.Where(value =>
                    value.IsPositiveModule
                    && value.ModuleId.StartsWith(scope, StringComparison.Ordinal)
                    && value.Benefits.Any(effect => burdenStats.Contains(
                        effect.statId, StringComparer.Ordinal)))
                .Select(value => value.ModuleId)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();
            string[] missing = sameAxisBenefits.Except(
                    drawback.ForbiddenSynergyModuleIds, StringComparer.Ordinal)
                .ToArray();
            if (missing.Length > 0)
                throw new InvalidOperationException(
                    $"Optional drawback '{drawback.ModuleId}' must forbid same-axis benefits: "
                    + string.Join(",", missing));
            string unknown = drawback.ForbiddenSynergyModuleIds.FirstOrDefault(value =>
                !byId.TryGetValue(value, out EvolutionModuleDefinition candidate)
                || !candidate.IsPositiveModule
                || !candidate.ModuleId.StartsWith(scope, StringComparison.Ordinal));
            if (unknown != null)
                throw new InvalidOperationException(
                    $"Optional drawback '{drawback.ModuleId}' names an invalid forbidden synergy '{unknown}'.");
        }

        All = Array.AsReadOnly(byId.Values
            .OrderBy(module => module.ModuleId, StringComparer.Ordinal)
            .ToArray());
    }

    public IReadOnlyList<EvolutionModuleDefinition> All { get; }

    public bool TryGet(
        string moduleId,
        out EvolutionModuleDefinition definition)
    {
        return byId.TryGetValue(moduleId?.Trim() ?? string.Empty, out definition);
    }

    private static IEnumerable<EvolutionModuleDefinition> CreateBuiltIns()
    {
        yield return Module(
            "facility:output",
            "생산의 흔적",
            "production",
            Benefit("work.output", 0f, 1.12f),
            Burden("service.speed", 0f, 0.96f),
            burdenKind: EvolutionModuleBurdenKind.OperatingCost);
        yield return Module(
            "facility:service",
            "봉사의 흔적",
            "service",
            Benefit("service.speed", 0f, 1.1f),
            Burden("work.output", 0f, 0.96f),
            burdenKind: EvolutionModuleBurdenKind.OperatingCost);
        yield return Module(
            "facility:research",
            "탐구의 흔적",
            "research",
            Benefit("research.output", 0f, 1.12f),
            Burden("work.output", 0f, 0.97f),
            burdenKind: EvolutionModuleBurdenKind.OperatingCost);
        yield return Module(
            "facility:survival",
            "생존의 흔적",
            "survival",
            Benefit("survival.output", 0f, 1.1f),
            Burden("service.speed", 0f, 0.96f),
            burdenKind: EvolutionModuleBurdenKind.OperatingCost);
        yield return Module(
            "facility:defense",
            "수호의 흔적",
            "defense",
            Benefit("defense.output", 0f, 1.12f),
            Burden("work.output", 0f, 0.97f),
            burdenKind: EvolutionModuleBurdenKind.OperatingCost);
        yield return Module(
            "facility:entertainment",
            "흥행의 흔적",
            "entertainment",
            Benefit("entertainment.output", 0f, 1.12f),
            Burden("work.output", 0f, 0.97f),
            burdenKind: EvolutionModuleBurdenKind.OperatingCost);
        // These are intentionally pure positive modules. Each projects only
        // through a distinct, concrete target ability/work consumer; inventing
        // a cross-axis operating burden would make the content appear balanced
        // while either doing nothing or reintroducing a self-cancelling axis.
        yield return Module(
            "facility:training-operations",
            "훈련 운용의 흔적",
            "training",
            Benefit("training.speed", 0f, 1.1f),
            burden: null);
        yield return Module(
            "facility:security-operations",
            "경비 운용의 흔적",
            "security",
            Benefit("security.speed", 0f, 1.1f),
            burden: null);
        yield return Module(
            "facility:service-support",
            "서비스 보조의 흔적",
            "service",
            Benefit("service.support-speed", 0f, 1.1f),
            burden: null);
        yield return Module(
            "facility:room-synergy",
            "공간 공명",
            "room",
            Benefit("service.speed", 0f, 1.15f),
            Burden("service.speed", 0f, 0.95f),
            new EvolutionModuleActivationRule
            {
                kind = EvolutionModuleActivationKind.RoomConditional,
                minimumCleanliness = 40f,
                minimumSpace = 35f
            },
            burdenKind: EvolutionModuleBurdenKind.OperatingCost);
        yield return Module(
            "facility:risky-overdrive",
            "위험 과부하",
            "risk",
            Benefit("work.output", 0f, 1.25f),
            Burden("service.speed", 0f, 0.9f),
            riskWeight: 3,
            burdenKind: EvolutionModuleBurdenKind.InseparableRisk);
        yield return DrawbackModule(
            "facility:drawback-maintenance",
            "누적 성능 저하",
            "maintenance",
            Burden("work.output", 0f, 0.98f),
            3,
            new[] { "failed", "damage", "shortage", "파손", "손상", "고장", "부족" },
            new[] { "facility:output", "facility:risky-overdrive" });
        yield return DrawbackModule(
            "facility:drawback-accident",
            "불안정한 운용",
            "accident",
            Burden("service.speed", 0f, 0.98f),
            3,
            new[] { "failed", "damage", "accident", "invasion", "실패", "손상", "사고", "침공" },
            new[] { "facility:room-synergy", "facility:service" });
        yield return DrawbackModule(
            "facility:drawback-research-drag",
            "연구 정체",
            "research",
            Burden("research.output", 0f, 0.98f),
            3,
            new[] { "failed", "shortage", "delay", "실패", "부족", "지연" },
            new[] { "facility:research" });
        yield return DrawbackModule(
            "facility:drawback-defense-gap",
            "방어 공백",
            "defense",
            Burden("defense.output", 0f, 0.98f),
            3,
            new[] { "damage", "invasion", "failed", "손상", "침공", "실패" },
            new[] { "facility:defense" });
        yield return DrawbackModule(
            "facility:drawback-survival-strain",
            "생존 지원 저하",
            "survival",
            Burden("survival.output", 0f, 0.98f),
            3,
            new[] { "shortage", "injury", "failed", "부족", "부상", "실패" },
            new[] { "facility:survival" });
        yield return DrawbackModule(
            "facility:drawback-entertainment-slump",
            "흥행 침체",
            "entertainment",
            Burden("entertainment.output", 0f, 0.98f),
            3,
            new[] { "loss", "failed", "shortage", "손실", "실패", "부족" },
            new[] { "facility:entertainment" });

        yield return Module(
            "equipment:melee",
            "근접 각인",
            "melee",
            Benefit("combat.damage", 0f, 1.08f),
            Burden("combat.accuracy", 0f, 0.96f),
            burdenKind: EvolutionModuleBurdenKind.OperatingCost);
        yield return Module(
            "equipment:ranged",
            "원거리 각인",
            "ranged",
            Benefit("combat.accuracy", 0f, 1.08f),
            Burden("combat.reload", 0f, 1.05f),
            burdenKind: EvolutionModuleBurdenKind.None);
        yield return Module(
            "equipment:guard",
            "수호 각인",
            "guard",
            Benefit("combat.defense", 0f, 1.1f),
            Burden("combat.reload", 0f, 1.05f),
            burdenKind: EvolutionModuleBurdenKind.OperatingCost);
        yield return Module(
            "equipment:survivor",
            "생환 각인",
            "survival",
            Benefit("combat.durability", 0f, 1.12f),
            Burden("combat.reload", 0f, 1.04f),
            burdenKind: EvolutionModuleBurdenKind.OperatingCost);
        yield return HistoricalEquipmentModule("equipment:force", "force",
            Benefit("combat.damage", 0f, 1.08f), Burden("combat.accuracy", 0f, 0.96f),
            EvolutionModuleBurdenKind.OperatingCost);
        yield return Module("equipment:penetration", "관통의 계보", "penetration",
            Benefit("combat.penetration", 0f, 1.08f), Burden("combat.durability", 0f, 0.97f),
            riskWeight: 2,
            burdenKind: EvolutionModuleBurdenKind.InseparableRisk);
        yield return HistoricalEquipmentModule("equipment:precision", "precision",
            Benefit("combat.accuracy", 0f, 1.08f), Burden("combat.damage", 0f, 0.98f),
            EvolutionModuleBurdenKind.InseparableRisk);
        yield return HistoricalEquipmentModule("equipment:cadence", "cadence",
            Benefit("combat.reload", 0f, 0.92f), Burden("combat.durability", 0f, 0.96f),
            EvolutionModuleBurdenKind.InseparableRisk,
            riskWeight: 2);
        yield return HistoricalEquipmentModule("equipment:control", "control",
            Benefit("combat.accuracy", 0f, 1.05f), Burden("combat.damage", 0f, 0.97f),
            EvolutionModuleBurdenKind.OperatingCost);
        yield return HistoricalEquipmentModule("equipment:execution", "execution",
            Benefit("combat.damage", 0f, 1.1f), Burden("combat.accuracy", 0f, 0.97f),
            EvolutionModuleBurdenKind.InseparableRisk);
        yield return HistoricalEquipmentModule("equipment:durability", "durability",
            Benefit("combat.durability", 0f, 1.12f), Burden("combat.reload", 0f, 1.04f),
            EvolutionModuleBurdenKind.OperatingCost);
        yield return Module(
            "equipment:reinforced-durability",
            "보강 내구 각인",
            "durability",
            Benefit("combat.durability", 0f, 1.12f),
            burden: null);
        yield return Module(
            "equipment:risky",
            "불안정 각인",
            "risk",
            Benefit("combat.damage", 0f, 1.16f),
            Burden("combat.accuracy", 0f, 0.94f),
            riskWeight: 3,
            burdenKind: EvolutionModuleBurdenKind.InseparableRisk);
        yield return DrawbackModule(
            "equipment:drawback-heavy",
            "과중량",
            "weight",
            Burden("combat.defense", 0f, 0.97f),
            3,
            new[] { "blocked", "protected", "armor", "방어", "보호", "장갑" },
            new[] { "equipment:guard" });
        yield return DrawbackModule(
            "equipment:drawback-fragile",
            "취약한 구조",
            "fragility",
            Burden("combat.durability", 0f, 0.99f),
            3,
            new[] { "broken", "damaged", "failed", "파손", "손상", "실패" },
            new[] {
                "equipment:durability",
                "equipment:reinforced-durability",
                "equipment:survivor"
            });
        yield return DrawbackModule(
            "equipment:drawback-slow-reload",
            "느린 재정비",
            "reload",
            Burden("combat.reload", 0f, 1.02f),
            3,
            new[] { "miss", "failed", "blocked", "빗나", "실패", "막힘" },
            new[] { "equipment:cadence" });
        yield return DrawbackModule(
            "equipment:drawback-blunted",
            "무뎌진 날",
            "damage",
            Burden("combat.damage", 0f, 0.98f),
            3,
            new[] { "blocked", "failed", "armor", "막힘", "실패", "장갑" },
            new[] { "equipment:execution", "equipment:force", "equipment:melee", "equipment:risky" });
        yield return DrawbackModule(
            "equipment:drawback-inaccurate",
            "흐트러진 조준",
            "accuracy",
            Burden("combat.accuracy", 0f, 0.98f),
            3,
            new[] { "miss", "failed", "retreat", "빗나", "실패", "후퇴" },
            new[] { "equipment:control", "equipment:precision", "equipment:ranged" });
        yield return DrawbackModule(
            "equipment:drawback-poor-penetration",
            "둔한 관통",
            "penetration",
            Burden("combat.penetration", 0f, 0.98f),
            3,
            new[] { "blocked", "armor", "failed", "막힘", "장갑", "실패" },
            new[] { "equipment:penetration" });
        yield return DrawbackModule(
            "equipment:drawback-devalued",
            "훼손된 가치",
            "value",
            Burden("combat.value", 0f, 0.95f),
            3,
            new[] { "broken", "damaged", "lost", "파손", "손상", "분실" });
    }

    private static EvolutionModuleDefinition Module(
        string id,
        string name,
        string role,
        EvolutionEffectModifier benefit,
        EvolutionEffectModifier burden,
        EvolutionModuleActivationRule activation = null,
        int riskWeight = 0,
        EvolutionModuleBurdenKind burdenKind = EvolutionModuleBurdenKind.None)
    {
        return new EvolutionModuleDefinition(
            id,
            name,
            role,
            new[] { benefit },
            burden == null ? Array.Empty<EvolutionEffectModifier>() : new[] { burden },
            activation,
            riskWeight,
            burdenKind);
    }

    private static EvolutionModuleDefinition HistoricalEquipmentModule(
        string effectId,
        string role,
        EvolutionEffectModifier benefit,
        EvolutionEffectModifier burden,
        EvolutionModuleBurdenKind burdenKind,
        int riskWeight = 0)
    {
        EquipmentHistoricalEffectDefinition definition =
            EquipmentHistoricalEffectCatalog.Require(effectId);
        return Module(
            definition.EffectId,
            definition.DisplayName,
            role,
            benefit,
            burden,
            riskWeight: riskWeight,
            burdenKind: burdenKind);
    }

    private static EvolutionModuleDefinition DrawbackModule(
        string id,
        string name,
        string role,
        EvolutionEffectModifier burden,
        int maximumSeverity,
        IEnumerable<string> negativeEvidenceMarkers,
        IEnumerable<string> forbiddenSynergies = null)
    {
        return new EvolutionModuleDefinition(
            id,
            name,
            role,
            Array.Empty<EvolutionEffectModifier>(),
            new[] { burden },
            burdenKind: EvolutionModuleBurdenKind.OptionalDrawback,
            maximumDrawbackSeverity: maximumSeverity,
            negativeEvidenceMarkers: negativeEvidenceMarkers,
            forbiddenSynergyModuleIds: forbiddenSynergies);
    }

    private static EvolutionEffectModifier Benefit(
        string id,
        float additive = 0f,
        float multiplier = 1f)
    {
        return new EvolutionEffectModifier
        {
            statId = id,
            additive = additive,
            multiplier = multiplier
        };
    }

    private static EvolutionEffectModifier Burden(
        string id,
        float additive = 0f,
        float multiplier = 1f)
    {
        return Benefit(id, additive, multiplier);
    }
}
