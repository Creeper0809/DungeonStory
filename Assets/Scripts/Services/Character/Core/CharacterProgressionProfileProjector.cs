using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using Unity.Profiling;
using UnityEngine;

/// <summary>
/// Resolves authored traits and projects progression state into effective
/// character profiles and stats. It owns only rebuildable projection caches;
/// CharacterProgression remains the authority for growth state.
/// </summary>
public sealed class CharacterProgressionProfileProjector
{
    private static readonly ProfilerMarker EffectiveProfileBuildMarker =
        new ProfilerMarker("Character.Progression.BuildEffectiveProfile");

    private readonly Dictionary<int, CharacterTraitSO> traitCatalog =
        new Dictionary<int, CharacterTraitSO>();
    private readonly ICharacterRuntimeProfileFactory runtimeProfileFactory;
    private readonly List<CharacterTraitSO> resolvedSelectedTraits =
        new List<CharacterTraitSO>();
    private IReadOnlyList<CharacterTraitSO> resolvedSelectedTraitsView;
    private CharacterRuntimeProfile effectiveRuntimeProfile;
    private int effectiveRuntimeProfileKey;
    private int initializedDataInstanceId;
    private int resolvedSelectedTraitsKey = int.MinValue;

    public CharacterProgressionProfileProjector(
        IGameContentCatalog content,
        ICharacterRuntimeProfileFactory runtimeProfileFactory)
    {
        if (content == null)
        {
            throw new ArgumentNullException(nameof(content));
        }
        this.runtimeProfileFactory = runtimeProfileFactory
            ?? throw new ArgumentNullException(nameof(runtimeProfileFactory));

        foreach (CharacterTraitSO trait in content.GetAll<CharacterTraitSO>())
        {
            if (trait != null && !traitCatalog.TryAdd(trait.id, trait))
            {
                throw new InvalidOperationException(
                    $"Duplicate CharacterTraitSO id '{trait.id}' in the root content catalog.");
            }
        }
    }

    public int GetFinalStat(
        CharacterActor actor,
        CharacterGrowthState growthState,
        CharacterStatType statType)
    {
        CharacterGrowthState state = RequireState(growthState);
        if (!state.initialized)
        {
            return actor?.Identity?.Profile?.GetStat(statType) ?? 5;
        }

        int value = state.initialBaseStats.Get(statType);
        CharacterSO data = actor?.Identity?.Data;
        value += data?.species?.statBonus?.Get(statType) ?? 0;
        foreach (CharacterTraitSO trait in ResolveSelectedTraits(actor, state))
        {
            value += trait?.statBonus?.Get(statType) ?? 0;
        }

        value += state.levelGrowthStats.Get(statType);
        value += GetConditionalPassiveStatBonus(actor, state, statType);
        return Mathf.Max(0, value);
    }

    public CharacterStatBreakdown GetStatBreakdown(
        CharacterActor actor,
        CharacterGrowthState growthState,
        CharacterStatType statType)
    {
        CharacterGrowthState state = RequireState(growthState);
        if (!state.initialized)
        {
            int fallback = actor?.Identity?.Profile?.GetStat(statType) ?? 5;
            return new CharacterStatBreakdown(
                statType,
                fallback,
                0,
                0,
                0,
                fallback);
        }

        int baseValue = state.initialBaseStats.Get(statType);
        int speciesTrait = GetSpeciesTraitStatBonus(actor, state, statType);
        int levelGrowth = state.levelGrowthStats.Get(statType);
        int conditionalPassive = GetConditionalPassiveStatBonus(
            actor,
            state,
            statType);
        int finalValue = Mathf.Max(
            0,
            baseValue + speciesTrait + levelGrowth + conditionalPassive);
        return new CharacterStatBreakdown(
            statType,
            baseValue,
            speciesTrait,
            levelGrowth,
            conditionalPassive,
            finalValue);
    }

    public int GetBaseStat(
        CharacterActor actor,
        CharacterGrowthState growthState,
        CharacterStatType statType)
    {
        CharacterGrowthState state = RequireState(growthState);
        return state.initialized
            ? state.initialBaseStats.Get(statType)
            : actor?.Identity?.Profile?.GetStat(statType) ?? 5;
    }

    public int GetSpeciesTraitStatBonus(
        CharacterActor actor,
        CharacterGrowthState growthState,
        CharacterStatType statType)
    {
        CharacterGrowthState state = RequireState(growthState);
        int value = actor?.Identity?.Data?.species?.statBonus?.Get(statType) ?? 0;
        foreach (CharacterTraitSO trait in ResolveSelectedTraits(actor, state))
        {
            value += trait?.statBonus?.Get(statType) ?? 0;
        }

        return value;
    }

    public int GetLevelGrowthStat(
        CharacterGrowthState growthState,
        CharacterStatType statType)
    {
        CharacterGrowthState state = RequireState(growthState);
        return state.initialized ? state.levelGrowthStats.Get(statType) : 0;
    }

    public int GetConditionalPassiveStatBonus(
        CharacterActor actor,
        CharacterGrowthState growthState,
        CharacterStatType statType)
    {
        CharacterGrowthState state = RequireState(growthState);
        if (!state.initialized)
        {
            return 0;
        }

        int bonus = 0;
        IReadOnlyList<CharacterSkillInstance> passives = state.passiveSkills;
        for (int passiveIndex = 0; passiveIndex < passives.Count; passiveIndex++)
        {
            CharacterSkillInstance passive = passives[passiveIndex];
            if (passive == null || !IsPassiveConditionActive(actor, passive.trigger))
            {
                continue;
            }

            IReadOnlyList<CharacterSkillModuleSelection> modules = passive.modules;
            if (modules == null)
            {
                continue;
            }

            for (int moduleIndex = 0; moduleIndex < modules.Count; moduleIndex++)
            {
                CharacterSkillModuleSelection module = modules[moduleIndex];
                if ((statType == CharacterStatType.Attack
                        || statType == CharacterStatType.Strength)
                    && string.Equals(module.moduleId, "buff", StringComparison.Ordinal))
                {
                    bonus += 2;
                }
                else if (statType == CharacterStatType.Endurance
                    && string.Equals(
                        module.moduleId,
                        "protect",
                        StringComparison.Ordinal))
                {
                    bonus += 2;
                }
            }
        }

        return bonus;
    }

    public IReadOnlyList<CharacterTraitSO> ResolveSelectedTraits(
        CharacterActor actor,
        CharacterGrowthState growthState)
    {
        CharacterGrowthState state = RequireState(growthState);
        CharacterSO data = actor?.Identity?.Data;
        int key = data != null ? BuildEffectiveRuntimeProfileKey(data, state) : 0;
        if (resolvedSelectedTraitsKey == key
            && resolvedSelectedTraitsView != null)
        {
            return resolvedSelectedTraitsView;
        }

        resolvedSelectedTraits.Clear();
        IReadOnlyList<int> traitIds = state.traitIds;
        if (traitIds == null || traitIds.Count == 0)
        {
            IReadOnlyList<CharacterTraitSO> sourceTraits = data?.traits;
            if (sourceTraits != null)
            {
                for (int index = 0; index < sourceTraits.Count; index++)
                {
                    CharacterTraitSO trait = sourceTraits[index];
                    if (trait != null)
                    {
                        resolvedSelectedTraits.Add(trait);
                    }
                }
            }
        }
        else
        {
            for (int index = 0; index < traitIds.Count; index++)
            {
                if (traitCatalog.TryGetValue(
                        traitIds[index],
                        out CharacterTraitSO trait)
                    && trait != null)
                {
                    resolvedSelectedTraits.Add(trait);
                }
            }
        }

        resolvedSelectedTraitsKey = key;
        resolvedSelectedTraitsView ??= ReadOnlyView.List(resolvedSelectedTraits);
        return resolvedSelectedTraitsView;
    }

    public CharacterRuntimeProfile GetEffectiveRuntimeProfile(
        CharacterActor actor,
        CharacterGrowthState growthState)
    {
        CharacterGrowthState state = RequireState(growthState);
        CharacterSO data = actor?.Identity?.Data;
        if (data == null)
        {
            return actor?.Identity?.Profile;
        }

        int key = BuildEffectiveRuntimeProfileKey(data, state);
        if (effectiveRuntimeProfile == null || effectiveRuntimeProfileKey != key)
        {
            effectiveRuntimeProfileKey = key;
            using (EffectiveProfileBuildMarker.Auto())
            {
                effectiveRuntimeProfile = runtimeProfileFactory.Create(
                    CharacterSpawnRequest.FromAuthoring(
                        data,
                        ResolveSelectedTraits(actor, state)));
            }
        }

        return effectiveRuntimeProfile;
    }

    public void EnsureInitialized(
        CharacterActor actor,
        CharacterGrowthState growthState,
        CharacterSkillSystemSettingsSO settings)
    {
        CharacterGrowthState state = RequireState(growthState);
        if (actor?.Identity?.Data == null)
        {
            return;
        }

        int dataInstanceId = actor.Identity.Data.GetInstanceID();
        if (initializedDataInstanceId == dataInstanceId && state.initialized)
        {
            return;
        }

        initializedDataInstanceId = dataInstanceId;
        if (state.initialized)
        {
            return;
        }

        CharacterSkillSystemSettingsSO requiredSettings = settings
            ?? throw new ArgumentNullException(nameof(settings));
        string seedSource = CharacterPersistentIdentity.Require(actor).Value;
        int seed = CharacterGrowthRules.StableHash(seedSource);
        IRandomStream random = new DeterministicRandomSequence(seed);
        state.initialized = true;
        state.generationSeed = seed;
        state.displayName = actor.Identity.DisplayName;
        state.origin = actor.Identity.GetSpeciesShortDescription();
        state.initialBaseStats = actor.Identity.Data.baseStats != null
            ? CharacterSkillModelUtility.CopyStats(actor.Identity.Data.baseStats)
            : CharacterGrowthRules.RollInitialStats(requiredSettings, random);
        state.levelGrowthStats = new CharacterStatBlock();
        state.potentialGrade = CharacterGrowthRules.RollPotential(
            requiredSettings,
            random);
        state.traitIds = actor.Identity.Data.traits?
            .Where(item => item != null)
            .Select(item => item.id)
            .Distinct()
            .Take(3)
            .ToList() ?? new List<int>();
        state.autoChooseDrafts =
            actor.Identity.CharacterType == CharacterType.Customer;
        EnsureMedicalStat(state);
        Invalidate();
    }

    public void EnsureMedicalStat(CharacterGrowthState growthState)
    {
        CharacterGrowthState state = RequireState(growthState);
        state.initialBaseStats ??= new CharacterStatBlock();
        if (state.initialBaseStats.Contains(CharacterStatIds.Medical))
        {
            return;
        }

        int migratedValue = Mathf.RoundToInt(
            state.initialBaseStats.Get(CharacterStatType.Research) * 0.6f
            + state.initialBaseStats.Get(CharacterStatType.Dexterity) * 0.4f);
        state.initialBaseStats.Set(
            CharacterStatType.Medical,
            Mathf.Clamp(migratedValue, 1, 10));
    }

    public void Invalidate()
    {
        effectiveRuntimeProfile = null;
        effectiveRuntimeProfileKey = 0;
        resolvedSelectedTraitsKey = int.MinValue;
    }

    public void Warm(CharacterActor actor, CharacterGrowthState growthState)
    {
        if (actor?.Identity?.Data != null)
        {
            GetEffectiveRuntimeProfile(actor, growthState);
        }
    }

    private static bool IsPassiveConditionActive(
        CharacterActor actor,
        CharacterSkillTrigger trigger)
    {
        return trigger switch
        {
            CharacterSkillTrigger.DamageTaken =>
                actor != null && actor.InjurySeverity >= 0.25f,
            CharacterSkillTrigger.NeedChanged =>
                actor?.Stats != null && actor.Stats.Mood < 50f,
            CharacterSkillTrigger.MoodChanged =>
                actor?.Stats != null && actor.Stats.Mood >= 70f,
            _ => false
        };
    }

    private static int BuildEffectiveRuntimeProfileKey(
        CharacterSO data,
        CharacterGrowthState growthState)
    {
        IReadOnlyList<int> traitIds = growthState.traitIds;
        if (traitIds == null || traitIds.Count == 0)
        {
            return data.GetInstanceID();
        }

        unchecked
        {
            uint sum = 0u;
            uint squaredSum = 0u;
            uint xor = 0u;
            for (int index = 0; index < traitIds.Count; index++)
            {
                uint mixed = MixTraitId((uint)traitIds[index]);
                sum += mixed;
                squaredSum += mixed * mixed;
                xor ^= mixed;
            }

            int key = data.GetInstanceID();
            key = (key * 397) ^ traitIds.Count;
            key = (key * 397) ^ (int)sum;
            key = (key * 397) ^ (int)squaredSum;
            key = (key * 397) ^ (int)xor;
            return key;
        }
    }

    private static uint MixTraitId(uint value)
    {
        unchecked
        {
            value ^= value >> 16;
            value *= 0x7FEB352Du;
            value ^= value >> 15;
            value *= 0x846CA68Bu;
            value ^= value >> 16;
            return value;
        }
    }

    private static CharacterGrowthState RequireState(
        CharacterGrowthState growthState)
    {
        CharacterGrowthState state = growthState
            ?? throw new ArgumentNullException(nameof(growthState));
        state.EnsureCollections();
        return state;
    }
}
