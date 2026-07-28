using System;
using System.Collections.Generic;
using System.Linq;
using VContainer;

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
            Burden("fuel.use", 0f, 1.08f));
        yield return Module(
            "facility:service",
            "봉사의 흔적",
            "service",
            Benefit("service.speed", 0f, 1.1f),
            Burden("staff.required", 0.25f));
        yield return Module(
            "facility:research",
            "탐구의 흔적",
            "research",
            Benefit("research.output", 0f, 1.12f),
            Burden("heat.output", 1f));
        yield return Module(
            "facility:survival",
            "생존의 흔적",
            "survival",
            Benefit("survival.output", 0f, 1.1f),
            Burden("maintenance.work", 2f));
        yield return Module(
            "facility:defense",
            "수호의 흔적",
            "defense",
            Benefit("defense.output", 0f, 1.12f),
            Burden("space.use", 1f));
        yield return Module(
            "facility:entertainment",
            "흥행의 흔적",
            "entertainment",
            Benefit("entertainment.output", 0f, 1.12f),
            Burden("accident.risk", 0.03f));
        yield return Module(
            "facility:room-synergy",
            "공간 공명",
            "room",
            Benefit("room.synergy", 0f, 1.15f),
            Burden("maintenance.work", 1.5f),
            new EvolutionModuleActivationRule
            {
                kind = EvolutionModuleActivationKind.RoomConditional,
                minimumCleanliness = 40f,
                minimumSpace = 35f
            });
        yield return Module(
            "facility:risky-overdrive",
            "위험 과부하",
            "risk",
            Benefit("work.output", 0f, 1.25f),
            Burden("accident.risk", 0.08f),
            riskWeight: 3);

        yield return Module(
            "equipment:melee",
            "근접 각인",
            "melee",
            Benefit("combat.damage", 0f, 1.08f),
            Burden("combat.weight", 0.08f));
        yield return Module(
            "equipment:ranged",
            "원거리 각인",
            "ranged",
            Benefit("combat.accuracy", 0f, 1.08f),
            Burden("combat.reload", 0f, 1.05f));
        yield return Module(
            "equipment:guard",
            "수호 각인",
            "guard",
            Benefit("combat.defense", 0f, 1.1f),
            Burden("combat.move", 0f, 0.96f));
        yield return Module(
            "equipment:survivor",
            "생환 각인",
            "survival",
            Benefit("combat.durability", 0f, 1.12f),
            Burden("combat.value", 0f, 1.12f));
        yield return Module(
            "equipment:risky",
            "불안정 각인",
            "risk",
            Benefit("combat.damage", 0f, 1.16f),
            Burden("combat.accident", 0.04f),
            riskWeight: 3);
    }

    private static EvolutionModuleDefinition Module(
        string id,
        string name,
        string role,
        EvolutionEffectModifier benefit,
        EvolutionEffectModifier burden,
        EvolutionModuleActivationRule activation = null,
        int riskWeight = 0)
    {
        return new EvolutionModuleDefinition(
            id,
            name,
            role,
            new[] { benefit },
            new[] { burden },
            activation,
            riskWeight);
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
