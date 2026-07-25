using System;
using System.Collections.Generic;
using System.Linq;

public static class OwnerDoctrineIds
{
    public const string SlimeStewardship = "owner:doctrine:slime-stewardship";
    public const string OrcWarCamp = "owner:doctrine:orc-war-camp";
    public const string VampireForbiddenStudy = "owner:doctrine:vampire-forbidden-study";
}

public sealed class OwnerDoctrineDefinition
{
    public OwnerDoctrineDefinition(
        string id,
        string speciesTag,
        string title,
        string benefit,
        string tradeoff,
        IReadOnlyList<IRunVariableEffect> effects)
    {
        this.id = id?.Trim() ?? string.Empty;
        this.speciesTag = speciesTag?.Trim() ?? string.Empty;
        this.title = title?.Trim() ?? string.Empty;
        this.benefit = benefit?.Trim() ?? string.Empty;
        this.tradeoff = tradeoff?.Trim() ?? string.Empty;
        this.effects = EventPayloadSnapshot.Copy(effects);
    }

    public string id { get; }
    public string speciesTag { get; }
    public string title { get; }
    public string benefit { get; }
    public string tradeoff { get; }
    public IReadOnlyList<IRunVariableEffect> effects { get; }
}

public static class OwnerDoctrineCatalog
{
    private static Dictionary<string, OwnerDoctrineDefinition> definitions = BuildDefinitions();

    public static IReadOnlyCollection<OwnerDoctrineDefinition> All => definitions.Values;

    public static OwnerDoctrineDefinition Get(string id)
    {
        return !string.IsNullOrWhiteSpace(id)
            && definitions.TryGetValue(id, out OwnerDoctrineDefinition definition)
                ? definition
                : null;
    }

    public static OwnerDoctrineDefinition ResolveFor(CharacterSO owner)
    {
        return ResolveForSpecies(owner?.SpeciesTag);
    }

    public static OwnerDoctrineDefinition ResolveForSpecies(string speciesTag)
    {
        return string.IsNullOrWhiteSpace(speciesTag)
            ? null
            : definitions.Values.FirstOrDefault(definition => string.Equals(
                definition.speciesTag,
                speciesTag,
                StringComparison.OrdinalIgnoreCase));
    }

    public static void Register(OwnerDoctrineDefinition definition, bool replace = false)
    {
        Validate(definition);
        if (!replace && definitions.ContainsKey(definition.id))
        {
            throw new InvalidOperationException($"Owner doctrine '{definition.id}' is already registered.");
        }

        definitions[definition.id] = definition;
    }

    public static void ResetToBuiltIns()
    {
        definitions = BuildDefinitions();
    }

    [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetRuntimeCatalog()
    {
        ResetToBuiltIns();
    }

    private static Dictionary<string, OwnerDoctrineDefinition> BuildDefinitions()
    {
        OwnerDoctrineDefinition[] builtIns =
        {
            Definition(
                OwnerDoctrineIds.SlimeStewardship,
                "Slime",
                "점액 유지보수",
                "식량·잡화 보급비 -15%",
                "시설 구매가 +10%",
                new RunStockCostEffect(StockCategory.Food, 0.85f),
                new RunStockCostEffect(StockCategory.General, 0.85f),
                new RunFacilityShopCostEffect(1.1f)),
            Definition(
                OwnerDoctrineIds.OrcWarCamp,
                "Orc",
                "전쟁 준비",
                "방어 시설 구매가 -25%",
                "위협 증가 속도 +15%",
                new RunFacilityShopCostEffect(0.75f, defenseOnly: true),
                new RunThreatRiseEffect(1.15f)),
            Definition(
                OwnerDoctrineIds.VampireForbiddenStudy,
                "Vampire",
                "금단 연구",
                "설계도 구매가 -25%",
                "시설 구매가 +10%",
                new RunBlueprintCostEffect(0.75f),
                new RunFacilityShopCostEffect(1.1f))
        };

        Dictionary<string, OwnerDoctrineDefinition> result =
            new Dictionary<string, OwnerDoctrineDefinition>(StringComparer.Ordinal);
        foreach (OwnerDoctrineDefinition definition in builtIns)
        {
            Validate(definition);
            result.Add(definition.id, definition);
        }

        return result;
    }

    private static OwnerDoctrineDefinition Definition(
        string id,
        string speciesTag,
        string title,
        string benefit,
        string tradeoff,
        params IRunVariableEffect[] effects)
    {
        return new OwnerDoctrineDefinition(
            id,
            speciesTag,
            title,
            benefit,
            tradeoff,
            effects ?? Array.Empty<IRunVariableEffect>());
    }

    private static void Validate(OwnerDoctrineDefinition definition)
    {
        if (definition == null)
        {
            throw new ArgumentNullException(nameof(definition));
        }

        if (string.IsNullOrWhiteSpace(definition.id)
            || string.IsNullOrWhiteSpace(definition.speciesTag)
            || string.IsNullOrWhiteSpace(definition.title))
        {
            throw new InvalidOperationException("Owner doctrines require an id, species tag, and title.");
        }

        if (definition.effects == null
            || definition.effects.Count == 0
            || definition.effects.Any(effect => effect == null))
        {
            throw new InvalidOperationException(
                $"Owner doctrine '{definition.id}' requires non-null gameplay effects.");
        }
    }
}
