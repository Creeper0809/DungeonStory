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

    public IReadOnlyList<CharacterTraitSO> ResolveSelectedTraits(
        CharacterActor actor,
        CharacterGrowthState growthState)
    {
        CharacterGrowthState state = RequireState(growthState);
        RequireTraitSelectionAuthority(actor, state);
        CharacterSO data = actor?.Identity?.Data;
        int key = data != null ? BuildEffectiveRuntimeProfileKey(data, state) : 0;
        if (resolvedSelectedTraitsKey == key
            && resolvedSelectedTraitsView != null)
        {
            return resolvedSelectedTraitsView;
        }

        resolvedSelectedTraits.Clear();
        IReadOnlyList<int> traitIds = state.traitIds;
        if (traitIds != null)
        {
            for (int index = 0; index < traitIds.Count; index++)
            {
                int traitId = traitIds[index];
                if (RetiredFounderTraitIds.Contains(traitId))
                {
                    throw new InvalidOperationException(
                        $"RetiredFounderTraitId: character '{ResolveCharacterDiagnosticId(actor)}' "
                        + $"references retired founder trait {traitId}. This development save "
                        + "predates the 100-trait catalog and requires a new game.");
                }
                if (!traitCatalog.TryGetValue(traitId, out CharacterTraitSO trait)
                    || trait == null)
                    throw new InvalidOperationException(
                        $"MissingFounderTraitId: character '{ResolveCharacterDiagnosticId(actor)}' "
                        + $"references missing trait definition {traitId}.");
                resolvedSelectedTraits.Add(trait);
            }
        }

        resolvedSelectedTraitsKey = key;
        resolvedSelectedTraitsView ??= ReadOnlyView.List(resolvedSelectedTraits);
        return resolvedSelectedTraitsView;
    }

    private static readonly HashSet<int> RetiredFounderTraitIds = new()
    {
        231, 232, 233, 234, 236, 237, 238, 240, 241, 242, 243, 244, 246
    };

    private static string ResolveCharacterDiagnosticId(CharacterActor actor) =>
        CharacterPersistentIdentity.TryGet(actor, out CharacterId id)
            ? id.Value
            : actor != null ? actor.name : "<null>";

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
            RequireTraitSelectionAuthority(actor, state);
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
        state.startingProficiencies = CharacterStartingProficiencyRules
            .Create(seed)
            .Select(value => value.Clone())
            .ToList();
        state.potentialGrade = CharacterGrowthRules.RollPotential(
            requiredSettings,
            random);
        state.traitIds = actor.Identity.Data.traits?
            .Where(item => item != null)
            .Select(item => item.id)
            .Distinct()
            .Take(4)
            .ToList() ?? new List<int>();
        state.traitSelectionAuthorityVersion =
            CharacterGrowthState.CurrentTraitSelectionAuthorityVersion;
        state.traitSelectionAuthorityOrigin = state.traitIds.Count > 0
            ? CharacterTraitSelectionAuthorityOrigin.LegacyCharacterDefinitionBootstrap
            : CharacterTraitSelectionAuthorityOrigin.PreparedSelection;
        state.autoChooseDrafts =
            actor.Identity.CharacterType == CharacterType.Customer;
        Invalidate();
    }

    public void Invalidate()
    {
        effectiveRuntimeProfile = null;
        effectiveRuntimeProfileKey = 0;
        resolvedSelectedTraitsKey = int.MinValue;
    }

    private static void RequireTraitSelectionAuthority(
        CharacterActor actor,
        CharacterGrowthState state)
    {
        if (state.traitSelectionAuthorityVersion
                != CharacterGrowthState.CurrentTraitSelectionAuthorityVersion
            || !Enum.IsDefined(
                typeof(CharacterTraitSelectionAuthorityOrigin),
                state.traitSelectionAuthorityOrigin)
            || state.traitSelectionAuthorityOrigin
                == CharacterTraitSelectionAuthorityOrigin.None)
        {
            throw new InvalidOperationException(
                $"UnsupportedTraitSelectionAuthority: character "
                + $"'{ResolveCharacterDiagnosticId(actor)}' has authority version "
                + $"{state.traitSelectionAuthorityVersion} and origin "
                + $"'{state.traitSelectionAuthorityOrigin}'. This development save "
                + "predates the single traitIds authority and requires a new game.");
        }
    }

    public void Warm(CharacterActor actor, CharacterGrowthState growthState)
    {
        if (actor?.Identity?.Data != null)
        {
            GetEffectiveRuntimeProfile(actor, growthState);
        }
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
