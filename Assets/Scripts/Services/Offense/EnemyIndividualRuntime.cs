using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public interface IEnemyArchetypeCatalog
{
    IReadOnlyList<EnemyArchetypeDefinitionSO> All { get; }
    bool TryGet(string id, out EnemyArchetypeDefinitionSO definition);
    EnemyArchetypeDefinitionSO Require(string id);
}

public interface IEncounterCatalog
{
    IReadOnlyList<OffenseEncounterSO> All { get; }
    bool TryGet(string id, out OffenseEncounterSO definition);
    OffenseEncounterSO Require(string id);
}

public interface IEnemyAbilityCatalog
{
    IReadOnlyList<EnemyAbilityDefinitionSO> All { get; }
    EnemyAbilityDefinitionSO Require(string id);
}

public interface IBattlefieldModifierCatalog
{
    IReadOnlyList<BattlefieldModifierDefinitionSO> All { get; }
    BattlefieldModifierDefinitionSO Require(string id);
}

public sealed class EnemyCombatContentCatalog :
    IEnemyArchetypeCatalog,
    IEncounterCatalog,
    IEnemyAbilityCatalog,
    IBattlefieldModifierCatalog
{
    private readonly IReadOnlyDictionary<string, EnemyArchetypeDefinitionSO> enemies;
    private readonly IReadOnlyDictionary<string, OffenseEncounterSO> encounters;
    private readonly IReadOnlyList<OffenseEncounterSO> allEncounters;
    private readonly IReadOnlyDictionary<string, EnemyAbilityDefinitionSO> abilities;
    private readonly IReadOnlyList<EnemyAbilityDefinitionSO> allAbilities;
    private readonly IReadOnlyDictionary<string, BattlefieldModifierDefinitionSO> modifiers;
    private readonly IReadOnlyList<BattlefieldModifierDefinitionSO> allModifiers;

    public EnemyCombatContentCatalog(IGameContentDefinitionSource content)
    {
        if (content == null) throw new ArgumentNullException(nameof(content));
        All = RequireExact(content.GetAll<EnemyArchetypeDefinitionSO>(), 36, value => value.stableId, value => value.ValidateDefinition(), "enemy archetype");
        allEncounters = RequireExact(content.GetAll<OffenseEncounterSO>(), 36, value => value.encounterId, value => value.ValidateDefinition(), "encounter");
        allAbilities = RequireExact(content.GetAll<EnemyAbilityDefinitionSO>(), 18, value => value.stableId, value => value.ValidateDefinition(), "enemy ability");
        allModifiers = RequireExact(content.GetAll<BattlefieldModifierDefinitionSO>(), 12, value => value.stableId, value => value.ValidateDefinition(), "battlefield modifier");
        enemies = All.ToDictionary(value => value.stableId, StringComparer.Ordinal);
        encounters = allEncounters.ToDictionary(value => value.encounterId, StringComparer.Ordinal);
        abilities = allAbilities.ToDictionary(value => value.stableId, StringComparer.Ordinal);
        modifiers = allModifiers.ToDictionary(value => value.stableId, StringComparer.Ordinal);

        string missingAbility = All.SelectMany(value => value.abilityIds)
            .FirstOrDefault(id => !abilities.ContainsKey(id));
        string missingEnemy = allEncounters.SelectMany(value => value.enemies)
            .Select(value => value.enemyArchetypeId)
            .FirstOrDefault(id => !enemies.ContainsKey(id));
        string missingModifier = allEncounters
            .SelectMany(value => value.battlefieldModifierIds)
            .FirstOrDefault(id => !modifiers.ContainsKey(id));
        if (!string.IsNullOrWhiteSpace(missingAbility)
            || !string.IsNullOrWhiteSpace(missingEnemy)
            || !string.IsNullOrWhiteSpace(missingModifier))
        {
            throw new InvalidOperationException(
                $"V20 combat content has a broken reference: ability='{missingAbility}', enemy='{missingEnemy}', modifier='{missingModifier}'.");
        }
    }

    public IReadOnlyList<EnemyArchetypeDefinitionSO> All { get; }
    IReadOnlyList<OffenseEncounterSO> IEncounterCatalog.All => allEncounters;
    IReadOnlyList<EnemyAbilityDefinitionSO> IEnemyAbilityCatalog.All => allAbilities;
    IReadOnlyList<BattlefieldModifierDefinitionSO> IBattlefieldModifierCatalog.All => allModifiers;

    public bool TryGet(string id, out EnemyArchetypeDefinitionSO definition) =>
        enemies.TryGetValue(Normalize(id), out definition);

    bool IEncounterCatalog.TryGet(string id, out OffenseEncounterSO definition) =>
        encounters.TryGetValue(Normalize(id), out definition);

    public EnemyArchetypeDefinitionSO Require(string id) => TryGet(id, out EnemyArchetypeDefinitionSO value)
        ? value
        : throw new KeyNotFoundException($"Unknown enemy archetype '{id}'.");

    OffenseEncounterSO IEncounterCatalog.Require(string id) =>
        ((IEncounterCatalog)this).TryGet(id, out OffenseEncounterSO value)
            ? value
            : throw new KeyNotFoundException($"Unknown offense encounter '{id}'.");

    EnemyAbilityDefinitionSO IEnemyAbilityCatalog.Require(string id) =>
        abilities.TryGetValue(Normalize(id), out EnemyAbilityDefinitionSO value)
            ? value
            : throw new KeyNotFoundException($"Unknown enemy ability '{id}'.");

    BattlefieldModifierDefinitionSO IBattlefieldModifierCatalog.Require(string id) =>
        modifiers.TryGetValue(Normalize(id), out BattlefieldModifierDefinitionSO value)
            ? value
            : throw new KeyNotFoundException($"Unknown battlefield modifier '{id}'.");

    private static IReadOnlyList<T> RequireExact<T>(
        IEnumerable<T> source,
        int expected,
        Func<T, string> id,
        Func<T, IReadOnlyList<string>> validate,
        string label)
        where T : UnityEngine.Object
    {
        T[] values = (source ?? Array.Empty<T>()).Where(value => value != null)
            .OrderBy(id, StringComparer.Ordinal).ToArray();
        List<string> errors = values.SelectMany(value => validate(value).Select(error => $"{value.name}: {error}")).ToList();
        if (values.Length != expected) errors.Add($"V20 requires exactly {expected} {label} definitions, found {values.Length}.");
        if (values.Select(id).Distinct(StringComparer.Ordinal).Count() != values.Length) errors.Add($"V20 {label} ids are not unique.");
        if (errors.Count > 0) throw new InvalidOperationException($"V20 {label} content is invalid:\n" + string.Join("\n", errors));
        return values;
    }

    private static string Normalize(string value) => value?.Trim() ?? string.Empty;
}

public sealed class EnemyIndividualBlueprint
{
    internal EnemyIndividualBlueprint(
        EnemyIndividualSaveData saveData,
        EnemyArchetypeDefinitionSO archetype,
        CharacterSpawnRequest spawnRequest)
    {
        SaveData = saveData ?? throw new ArgumentNullException(nameof(saveData));
        Archetype = archetype ?? throw new ArgumentNullException(nameof(archetype));
        SpawnRequest = spawnRequest ?? throw new ArgumentNullException(nameof(spawnRequest));
    }

    public EnemyIndividualSaveData SaveData { get; }
    public EnemyArchetypeDefinitionSO Archetype { get; }
    public CharacterSpawnRequest SpawnRequest { get; }
    public CharacterId CharacterId => new(SaveData.characterId);
}

public interface IEnemyIndividualFactory
{
    EnemyIndividualSaveData Create(
        string enemyArchetypeId,
        CharacterId characterId,
        string deterministicContext);
    EnemyIndividualBlueprint RequireBlueprint(EnemyIndividualSaveData data);
    void EnsureCharacterDomains(EnemyIndividualBlueprint blueprint);
}

public sealed class EnemyIndividualFactory : IEnemyIndividualFactory
{
    private static readonly string[] SkillIds =
    {
        "skill:combat", "skill:shooting", "skill:medicine",
        "skill:crafting", "skill:research", "skill:social"
    };

    private readonly IEnemyArchetypeCatalog archetypes;
    private readonly ICharacterNarrativeCatalog narrativeCatalog;
    private readonly ICharacterNarrativeQuery narrativeQuery;
    private readonly ICharacterNarrativeCommand narrativeCommands;
    private readonly ICharacterLifeQuery lifeQuery;
    private readonly ICharacterLifeCommand lifeCommands;
    private readonly ICharacterLifeDefinitionCatalog lifeDefinitions;
    private readonly IReadOnlyList<CharacterTraitSO> generalTraits;
    private readonly IReadOnlyList<HeritableTraitDefinitionSO> heritableTraits;
    private readonly HashSet<string> generalTraitIds;

    public EnemyIndividualFactory(
        IEnemyArchetypeCatalog archetypes,
        ICharacterNarrativeCatalog narrativeCatalog,
        ICharacterNarrativeQuery narrativeQuery,
        ICharacterNarrativeCommand narrativeCommands,
        ICharacterLifeQuery lifeQuery,
        ICharacterLifeCommand lifeCommands,
        ICharacterLifeDefinitionCatalog lifeDefinitions,
        IGameContentDefinitionSource content)
    {
        this.archetypes = archetypes ?? throw new ArgumentNullException(nameof(archetypes));
        this.narrativeCatalog = narrativeCatalog ?? throw new ArgumentNullException(nameof(narrativeCatalog));
        this.narrativeQuery = narrativeQuery ?? throw new ArgumentNullException(nameof(narrativeQuery));
        this.narrativeCommands = narrativeCommands ?? throw new ArgumentNullException(nameof(narrativeCommands));
        this.lifeQuery = lifeQuery ?? throw new ArgumentNullException(nameof(lifeQuery));
        this.lifeCommands = lifeCommands ?? throw new ArgumentNullException(nameof(lifeCommands));
        this.lifeDefinitions = lifeDefinitions ?? throw new ArgumentNullException(nameof(lifeDefinitions));
        if (content == null) throw new ArgumentNullException(nameof(content));
        generalTraits = content.GetAll<CharacterTraitSO>().Where(value => value != null)
            .OrderBy(value => value.DefinitionId.Value, StringComparer.Ordinal).ToArray();
        heritableTraits = content.GetAll<HeritableTraitDefinitionSO>().Where(value => value != null)
            .OrderBy(value => value.traitId, StringComparer.Ordinal).ToArray();
        generalTraitIds = generalTraits.Select(value => value.DefinitionId.Value)
            .ToHashSet(StringComparer.Ordinal);
        if (generalTraits.Count != 56 || heritableTraits.Count != 24)
            throw new InvalidOperationException($"V20 enemy generation requires 56 general and 24 heritable traits; found {generalTraits.Count}/{heritableTraits.Count}.");
    }

    public EnemyIndividualSaveData Create(
        string enemyArchetypeId,
        CharacterId characterId,
        string deterministicContext)
    {
        if (!characterId.IsValid) throw new ArgumentException("A valid CharacterId is required.", nameof(characterId));
        EnemyArchetypeDefinitionSO archetype = archetypes.Require(enemyArchetypeId);
        EnemyIndividualGenerationProfile generation = archetype.individualGeneration;
        DeterministicCursor random = new(characterId.Value + ":" + Normalize(deterministicContext) + ":" + archetype.stableId);
        CharacterSpeciesId phenotype = new(MapPhenotype(archetype.speciesTag));
        int generalCount = random.RangeInclusive(generation.minimumGeneralTraits, generation.maximumGeneralTraits);
        string[] general = PickDistinct(generalTraits, generalCount, random, value => value.DefinitionId.Value);
        HeritableTraitDefinitionSO[] compatible = heritableTraits.Where(value =>
            value.compatibleSpeciesTags == null
            || value.compatibleSpeciesTags.Count == 0
            || value.compatibleSpeciesTags.Contains(phenotype.Value, StringComparer.Ordinal)).ToArray();
        int expressedCount = Math.Min(compatible.Length, random.RangeInclusive(generation.minimumExpressedHeritableTraits, generation.maximumExpressedHeritableTraits));
        string[] expressed = PickDistinct(compatible, expressedCount, random, value => value.traitId);
        HeritableTraitDefinitionSO[] latentPool = compatible.Where(value => !expressed.Contains(value.traitId, StringComparer.Ordinal)).ToArray();
        int latentCount = Math.Min(latentPool.Length, random.RangeInclusive(0, generation.maximumLatentHeritableTraits));
        string[] latent = PickDistinct(latentPool, latentCount, random, value => value.traitId);
        string[] backgroundPool = (generation.allowedBackgroundIds ?? new List<string>()).Where(value => !string.IsNullOrWhiteSpace(value)).ToArray();
        if (backgroundPool.Length == 0) backgroundPool = narrativeCatalog.Backgrounds.Select(value => value.StableId).ToArray();
        string backgroundId = backgroundPool[random.Range(0, backgroundPool.Length)];
        narrativeCatalog.Require(new CharacterBackgroundId(backgroundId));
        SpeciesCultureDefinitionSO[] cultures = narrativeCatalog.Cultures
            .OrderBy(value => value.StableId, StringComparer.Ordinal)
            .ToArray();
        string cultureId = cultures[random.Range(0, cultures.Length)].StableId;
        string ambitionId = narrativeCatalog.Ambitions[
            random.Range(0, narrativeCatalog.Ambitions.Count)].StableId;
        SpeciesLifeHistoryDefinition lifeHistory = lifeDefinitions.RequireLifeHistory(phenotype);
        double biologicalAgeYears = SampleInitialBiologicalAgeYears(lifeHistory, random);
        double biologicalAgeUnits = biologicalAgeYears * GameCalendarRules.DaysPerYear;

        return new EnemyIndividualSaveData
        {
            characterId = characterId.Value,
            enemyArchetypeId = archetype.stableId,
            originFactionId = archetype.factionId,
            phenotypeSpeciesId = phenotype.Value,
            visualVariantId = "enemy:" + random.NextUInt().ToString("X8"),
            displayName = CreateLocalizedDisplayName(archetype, random),
            backgroundId = backgroundId,
            cultureId = cultureId,
            ambitionId = ambitionId,
            militaryTrainingId = generation.militaryTrainingId,
            chronologicalAgeDays = CalculateChronologicalAgeDays(lifeHistory, biologicalAgeUnits),
            biologicalAgeDayUnits = biologicalAgeUnits,
            birthdayDayOfYear = 1 + random.Range(0, GameCalendarRules.DaysPerYear),
            loyalty = Mathf.Lerp(generation.minimumLoyalty, generation.maximumLoyalty, random.NextUnit()),
            combatStatMultiplier = 1f + Mathf.Lerp(-generation.combatStatVariance, generation.combatStatVariance, random.NextUnit()),
            generalTraitIds = general.ToList(),
            expressedHeritableTraitIds = expressed.ToList(),
            latentHeritableTraitIds = latent.ToList(),
            innateAptitudes = SkillIds.Select(skillId => new EnemyIndividualAptitudeSaveData
            {
                skillId = skillId,
                value = Mathf.Clamp(RoleAptitude(archetype.role, skillId) + random.RangeInclusive(-generation.aptitudeVariance, generation.aptitudeVariance), 0, 100)
            }).ToList()
        };
    }

    public EnemyIndividualBlueprint RequireBlueprint(EnemyIndividualSaveData data)
    {
        if (data == null) throw new ArgumentNullException(nameof(data));
        CharacterId characterId = new(data.characterId);
        EnemyArchetypeDefinitionSO archetype = archetypes.Require(data.enemyArchetypeId);
        CharacterSpeciesId phenotype = new(data.phenotypeSpeciesId);
        SpeciesLifeHistoryDefinition lifeHistory = lifeDefinitions.RequireLifeHistory(phenotype);
        if (!characterId.IsValid || !phenotype.IsValid
            || !string.Equals(data.originFactionId, archetype.factionId, StringComparison.Ordinal)
            || !new CharacterBackgroundId(data.backgroundId).IsValid
            || !new SpeciesCultureId(data.cultureId).IsValid
            || !new CharacterAmbitionId(data.ambitionId).IsValid
            || !data.militaryTrainingId.StartsWith("training:", StringComparison.Ordinal)
            || data.chronologicalAgeDays < 0
            || double.IsNaN(data.biologicalAgeDayUnits) || double.IsInfinity(data.biologicalAgeDayUnits)
            || data.biologicalAgeDayUnits < lifeHistory.AdultAgeDayUnits
            || data.birthdayDayOfYear < 1 || data.birthdayDayOfYear > GameCalendarRules.DaysPerYear
            || float.IsNaN(data.loyalty) || float.IsInfinity(data.loyalty) || data.loyalty < 0f || data.loyalty > 100f
            || float.IsNaN(data.combatStatMultiplier) || float.IsInfinity(data.combatStatMultiplier) || data.combatStatMultiplier < 0.75f || data.combatStatMultiplier > 1.25f)
            throw new InvalidOperationException($"Enemy individual '{data.characterId}' is invalid.");
        narrativeCatalog.Require(new CharacterBackgroundId(data.backgroundId));
        narrativeCatalog.Require(new SpeciesCultureId(data.cultureId));
        narrativeCatalog.Require(new CharacterAmbitionId(data.ambitionId));
        CharacterTraitId[] general = RequireUnique(data.generalTraitIds, 4, "general trait").Select(value => new CharacterTraitId(value)).ToArray();
        if (general.Any(value => !generalTraitIds.Contains(value.Value)))
            throw new InvalidOperationException("Enemy individual references an unknown general trait.");
        string[] expressed = RequireUnique(data.expressedHeritableTraitIds, 4, "expressed heritable trait");
        string[] latent = RequireUnique(data.latentHeritableTraitIds, 2, "latent heritable trait");
        if (expressed.Intersect(latent, StringComparer.Ordinal).Any()) throw new InvalidOperationException("Enemy hereditary trait sets overlap.");
        HashSet<string> authoredHeritable = heritableTraits.Select(value => value.traitId).ToHashSet(StringComparer.Ordinal);
        if (expressed.Concat(latent).Any(value => !authoredHeritable.Contains(value))) throw new InvalidOperationException("Enemy individual references an unknown heritable trait.");
        Dictionary<string, int> aptitudes = (data.innateAptitudes ?? new List<EnemyIndividualAptitudeSaveData>())
            .ToDictionary(value => Normalize(value.skillId), value => value.value, StringComparer.Ordinal);
        if (aptitudes.Count != SkillIds.Length || SkillIds.Any(value => !aptitudes.ContainsKey(value)) || aptitudes.Values.Any(value => value < 0 || value > 100))
            throw new InvalidOperationException("Enemy individual aptitude profile is invalid.");
        CharacterSpawnRequest request = new(
            CharacterArchetypeIdForIntruder(archetype),
            phenotype,
            data.visualVariantId,
            ReproductiveRole.None,
            general,
            latentTraitIds: null,
            aptitudes);
        return new EnemyIndividualBlueprint(data.Clone(), archetype, request);
    }

    public void EnsureCharacterDomains(EnemyIndividualBlueprint blueprint)
    {
        if (blueprint == null) throw new ArgumentNullException(nameof(blueprint));
        CharacterId id = blueprint.CharacterId;
        CharacterSpeciesId species = blueprint.SpawnRequest.PhenotypeSpeciesId;
        if (!lifeQuery.TryGet(id, out _))
        {
            EnemyIndividualSaveData data = blueprint.SaveData;
            lifeCommands.Register(
                id,
                species,
                data.chronologicalAgeDays,
                data.biologicalAgeDayUnits,
                data.birthdayDayOfYear);
        }
        if (!narrativeQuery.TryGet(id, out _))
        {
            EnemyIndividualSaveData data = blueprint.SaveData;
            narrativeCommands.RegisterEnemyOrigin(
                id,
                species,
                data.expressedHeritableTraitIds,
                data.latentHeritableTraitIds,
                new CharacterBackgroundId(data.backgroundId),
                new SpeciesCultureId(data.cultureId),
                data.enemyArchetypeId,
                data.originFactionId,
                data.militaryTrainingId,
                data.loyalty);
            narrativeCommands.StartAmbition(
                id,
                new CharacterAmbitionId(data.ambitionId),
                absoluteDay: 0);
        }
    }

    private static CharacterArchetypeId CharacterArchetypeIdForIntruder(EnemyArchetypeDefinitionSO archetype) =>
        new("character-archetype:2001");

    private static string MapPhenotype(string speciesTag) => Normalize(speciesTag) switch
    {
        "Human" => "Adventurer",
        "Truth" => "Adventurer",
        "Construct" => "Golem",
        string value when value.Length > 0 => value,
        _ => throw new InvalidOperationException("Enemy archetype has no phenotype species.")
    };

    private static int RoleAptitude(EnemyCombatRole role, string skillId)
    {
        if (skillId == "skill:combat") return role is EnemyCombatRole.Vanguard or EnemyCombatRole.Defender or EnemyCombatRole.Boss ? 75 : 50;
        if (skillId == "skill:shooting") return role == EnemyCombatRole.Marksman ? 80 : 35;
        if (skillId == "skill:medicine") return role == EnemyCombatRole.Support ? 70 : 25;
        if (skillId == "skill:social") return role is EnemyCombatRole.Controller or EnemyCombatRole.Boss ? 65 : 35;
        return role == EnemyCombatRole.Support ? 55 : 40;
    }

    private static double SampleInitialBiologicalAgeYears(
        SpeciesLifeHistoryDefinition history,
        DeterministicCursor random)
    {
        double selector = random.NextUnit();
        double withinBand = random.NextUnit();
        double adult = history.AdultAgeYears;
        double elder = history.ElderAgeYears;
        double adultSpan = Math.Max(0d, elder - adult);
        if (selector < 0.40d) return adult + adultSpan * 0.25d * withinBand;
        if (selector < 0.75d) return adult + adultSpan * (0.25d + 0.35d * withinBand);
        if (selector < 0.95d) return adult + adultSpan * (0.60d + 0.40d * withinBand);
        return elder + 10d * withinBand;
    }

    private static int CalculateChronologicalAgeDays(
        SpeciesLifeHistoryDefinition history,
        double biologicalAgeUnits)
    {
        double adultUnits = history.AdultAgeDayUnits;
        double result = Math.Min(adultUnits, biologicalAgeUnits) / 4d;
        if (biologicalAgeUnits > adultUnits) result += (biologicalAgeUnits - adultUnits) / 6d;
        return Math.Max(0, (int)Math.Floor(result));
    }

    private static string CreateLocalizedDisplayName(
        EnemyArchetypeDefinitionSO archetype,
        DeterministicCursor random)
    {
        string[] givenNames =
        {
            "아렌", "벨라", "카이", "세라", "에녹", "미라",
            "로웬", "타린", "제다", "엘릭", "브란", "유나"
        };
        string[] familyNames =
        {
            "하르트", "벨", "카로", "리안", "모른", "파인",
            "로크", "베인", "아스", "세른", "돌", "카른"
        };
        return $"{givenNames[random.Range(0, givenNames.Length)]} "
            + $"{familyNames[random.Range(0, familyNames.Length)]} · "
            + archetype.displayName;
    }

    private static string[] PickDistinct<T>(IReadOnlyList<T> source, int count, DeterministicCursor random, Func<T, string> id)
    {
        List<T> pool = new(source ?? Array.Empty<T>());
        List<string> result = new();
        while (result.Count < count && pool.Count > 0)
        {
            int index = random.Range(0, pool.Count);
            T selected = pool[index];
            pool.RemoveAt(index);
            result.Add(id(selected));
        }
        return result.ToArray();
    }

    private static string[] RequireUnique(IEnumerable<string> values, int maximum, string label)
    {
        string[] result = (values ?? Array.Empty<string>()).Select(Normalize).ToArray();
        if (result.Any(value => value.Length == 0) || result.Distinct(StringComparer.Ordinal).Count() != result.Length || result.Length > maximum)
            throw new InvalidOperationException($"Enemy individual {label} ids are invalid.");
        return result;
    }

    private static string Normalize(string value) => value?.Trim() ?? string.Empty;

    private sealed class DeterministicCursor
    {
        private readonly string seed;
        private int index;
        public DeterministicCursor(string seed) => this.seed = seed ?? string.Empty;
        public uint NextUInt() => Avalanche(
            PersistentEntityId.GetStableHash32(seed + ":" + index++));
        public float NextUnit() => (NextUInt() & 0x00FFFFFFu) / 16777216f;
        public int Range(int minimum, int maximumExclusive) =>
            maximumExclusive <= minimum
                ? minimum
                : minimum + (int)(NextUInt()
                    % (uint)(maximumExclusive - minimum));
        public int RangeInclusive(int minimum, int maximum) => Range(minimum, maximum + 1);

        private static uint Avalanche(uint value)
        {
            value ^= value >> 16;
            value *= 0x7FEB352Du;
            value ^= value >> 15;
            value *= 0x846CA68Bu;
            value ^= value >> 16;
            return value;
        }
    }
}
