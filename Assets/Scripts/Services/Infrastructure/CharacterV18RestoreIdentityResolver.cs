using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

internal static class CharacterV18RestoreIdentityResolver
{
    public static bool TryGetActor(
        IReadOnlyDictionary<string, CharacterActor> actorsById,
        IReadOnlyDictionary<string, string> legacyActorIds,
        string persistentId,
        out CharacterActor actor)
    {
        actor = null;
        if (string.IsNullOrEmpty(persistentId)
            || !string.Equals(
                persistentId,
                persistentId.Trim(),
                StringComparison.Ordinal))
        {
            return false;
        }

        CharacterId direct = new CharacterId(persistentId);
        if (direct.IsValid
            && string.Equals(
                direct.Value,
                persistentId,
                StringComparison.Ordinal)
            && actorsById.TryGetValue(persistentId, out actor)
            && actor != null)
        {
            return true;
        }

        return legacyActorIds.TryGetValue(persistentId, out string canonicalId)
            && actorsById.TryGetValue(canonicalId, out actor)
            && actor != null;
    }

    public static Dictionary<DungeonCharacterSaveData, CharacterId>
        BuildCanonicalActorIds(IEnumerable<DungeonCharacterSaveData> actors)
    {
        return (actors ?? Enumerable.Empty<DungeonCharacterSaveData>())
            .ToDictionary(
                actor => actor,
                actor => RequireCharacterId(actor.persistentId));
    }

    public static List<WorldCharacterProfile> CloneCanonicalProfiles(
        IEnumerable<WorldCharacterProfile> profiles)
    {
        return (profiles ?? Enumerable.Empty<WorldCharacterProfile>())
            .Select(profile =>
            {
                WorldCharacterProfile clone = profile.Clone();
                clone.persistentId = RequireCharacterId(profile.persistentId).Value;
                return clone;
            })
            .ToList();
    }

    public static IReadOnlyDictionary<string, string> BuildLegacyMappings(
        IEnumerable<DungeonCharacterSaveData> actors,
        IEnumerable<WorldCharacterProfile> profiles)
    {
        Dictionary<string, string> mappings = new(StringComparer.Ordinal);
        foreach (string sourceId in (actors ?? Enumerable.Empty<DungeonCharacterSaveData>())
                     .Where(actor => actor != null)
                     .Select(actor => actor.persistentId)
                     .Concat((profiles ?? Enumerable.Empty<WorldCharacterProfile>())
                         .Where(profile => profile != null)
                         .Select(profile => profile.persistentId)))
        {
            CharacterId canonical = RequireCharacterId(sourceId);
            string legacy = sourceId ?? string.Empty;
            if (string.Equals(legacy, canonical.Value, StringComparison.Ordinal))
            {
                continue;
            }
            if (mappings.TryGetValue(legacy, out string existing)
                && !string.Equals(existing, canonical.Value, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Legacy CharacterId '{legacy}' resolves to conflicting canonical IDs.");
            }
            mappings[legacy] = canonical.Value;
        }
        return mappings;
    }

    public static void AddCandidate(
        IDictionary<string, CharacterActor> actorsById,
        IDictionary<string, string> legacyActorIds,
        DungeonCharacterSaveData source,
        CharacterId canonicalId,
        CharacterActor actor)
    {
        if (source == null)
        {
            throw new ArgumentNullException(nameof(source));
        }
        if (actor == null)
        {
            throw new ArgumentNullException(nameof(actor));
        }
        if (!canonicalId.IsValid)
        {
            throw new InvalidOperationException(
                "A restore candidate cannot be indexed without a persistent character ID.");
        }
        if (actorsById.ContainsKey(canonicalId.Value))
        {
            throw new InvalidOperationException(
                $"Duplicate persistent character ID '{canonicalId.Value}' encountered during restore.");
        }

        actor.Identity?.SetPersistentId(canonicalId);
        actorsById.Add(canonicalId.Value, actor);

        string sourceId = source.persistentId ?? string.Empty;
        if (string.Equals(sourceId, canonicalId.Value, StringComparison.Ordinal))
        {
            return;
        }
        if (legacyActorIds.ContainsKey(sourceId))
        {
            throw new InvalidOperationException(
                $"Duplicate legacy character ID alias '{sourceId}' encountered during restore.");
        }

        legacyActorIds.Add(sourceId, canonicalId.Value);
    }

    public static void EnsureUniqueIds(
        IEnumerable<string> ids,
        string operation,
        bool allowActorProfileOverlap = false)
    {
        HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (string rawId in ids ?? Enumerable.Empty<string>())
        {
            if (string.IsNullOrEmpty(rawId)
                || !string.Equals(
                    rawId,
                    rawId.Trim(),
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"A character without an exact persistent ID was found during {operation}.");
            }
            string id = rawId;
            if (!seen.Add(id) && !allowActorProfileOverlap)
            {
                throw new InvalidOperationException(
                    $"Duplicate persistent character ID '{id}' was found during {operation}.");
            }
        }
    }

    public static void ValidateUniqueIds(
        IEnumerable<string> ids,
        string label,
        DungeonGameRestoreReport report,
        bool allowLegacyCharacterIds)
    {
        HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (string rawId in ids ?? Enumerable.Empty<string>())
        {
            if (string.IsNullOrEmpty(rawId))
            {
                report.AddError($"A {label} has an empty persistent ID.");
            }
            else if (!TryResolve(
                         rawId,
                         allowLegacyCharacterIds,
                         out CharacterId canonicalId,
                         out _))
            {
                report.AddError(
                    $"A {label} has invalid persistent ID '{rawId}'.");
            }
            else if (!seen.Add(canonicalId.Value))
            {
                report.AddError(
                    $"Duplicate {label} persistent ID '{canonicalId.Value}' after V18 normalization.");
            }
        }
    }

    public static bool TryResolve(
        string value,
        bool allowLegacyCharacterIds,
        out CharacterId canonicalId,
        out bool wasLegacy)
    {
        CharacterId direct = (CharacterId)value;
        if (direct.IsValid
            && string.Equals(
                direct.Value,
                value,
                StringComparison.Ordinal))
        {
            canonicalId = direct;
            wasLegacy = false;
            return true;
        }

        if (allowLegacyCharacterIds)
        {
            return CharacterId.TryCanonicalizeV18Restore(
                value,
                out canonicalId,
                out wasLegacy);
        }

        canonicalId = default;
        wasLegacy = false;
        return false;
    }

    public static CharacterId RequireCharacterId(string value)
    {
        if (CharacterId.TryCanonicalizeV18Restore(
                value,
                out CharacterId canonicalId,
                out _))
        {
            return canonicalId;
        }

        throw new InvalidOperationException(
            $"Character ID '{value}' is neither canonical nor a supported V18 legacy ID.");
    }
}

internal static class CharacterWorldRestorePayloadValidator
{
    public static void Validate(
        Grid grid,
        DungeonCharacterWorldSaveData source,
        DungeonGameRestoreReport report,
        bool allowLegacyCharacterIds,
        IReadOnlyCollection<CharacterSO> characterDefinitions)
    {
        if (grid == null)
        {
            throw new ArgumentNullException(nameof(grid));
        }
        if (report == null)
        {
            throw new ArgumentNullException(nameof(report));
        }
        if (source == null)
        {
            report.AddError("Character world payload is missing.");
            return;
        }

        if (source.actors == null)
        {
            report.AddError("Character world actor collection is missing.");
        }
        if (source.populationProfiles == null)
        {
            report.AddError("Character world population profile collection is missing.");
        }
        if (source.globalFacilityReputation == null)
        {
            report.AddError("Character world reputation snapshot is missing.");
        }

        List<DungeonCharacterSaveData> actors = source.actors
            ?? new List<DungeonCharacterSaveData>();
        List<WorldCharacterProfile> profiles = source.populationProfiles
            ?? new List<WorldCharacterProfile>();
        if (actors.Any(actor => actor == null))
        {
            report.AddError("Character world payload contains a null actor entry.");
        }
        if (profiles.Any(profile => profile == null))
        {
            report.AddError("Character world payload contains a null population profile.");
        }

        DungeonCharacterSaveData[] concreteActors = actors
            .Where(actor => actor != null)
            .ToArray();
        WorldCharacterProfile[] concreteProfiles = profiles
            .Where(profile => profile != null)
            .ToArray();
        CharacterV18RestoreIdentityResolver.ValidateUniqueIds(
            concreteActors.Select(actor => actor.persistentId),
            "character actor",
            report,
            allowLegacyCharacterIds);
        CharacterV18RestoreIdentityResolver.ValidateUniqueIds(
            concreteProfiles.Select(profile => profile.persistentId),
            "population profile",
            report,
            allowLegacyCharacterIds);

        int ownerCount = concreteActors.Count(actor => actor.isOwner);
        if (ownerCount != 1)
        {
            report.AddError(
                $"Character world payload requires exactly one owner actor, but contains {ownerCount}.");
        }

        Dictionary<int, CharacterSO> charactersById = characterDefinitions
            .Where(data => data != null)
            .GroupBy(data => data.id)
            .ToDictionary(group => group.Key, group => group.First());
        foreach (DungeonCharacterSaveData actor in concreteActors)
        {
            ValidateActor(
                grid,
                actor,
                charactersById,
                report,
                allowLegacyCharacterIds);
        }
        foreach (WorldCharacterProfile profile in concreteProfiles)
        {
            ValidateProfile(
                profile,
                charactersById,
                report,
                allowLegacyCharacterIds);
        }

        CharacterWorldSaveValidation.ValidatePopulationProfiles(
            concreteProfiles,
            report);
        CharacterWorldSaveValidation.ValidateReputation(
            source.globalFacilityReputation,
            report);
    }

    private static void ValidateActor(
        Grid grid,
        DungeonCharacterSaveData actor,
        IReadOnlyDictionary<int, CharacterSO> charactersById,
        DungeonGameRestoreReport report,
        bool allowLegacyCharacterIds)
    {
        if (!CharacterV18RestoreIdentityResolver.TryResolve(
                actor.persistentId,
                allowLegacyCharacterIds,
                out CharacterId id,
                out _))
        {
            report.AddError(
                $"Character data {actor.dataId} has no valid persistent ID.");
        }
        else if (actor.isOwner && !id.Equals(CharacterId.Owner))
        {
            report.AddError(
                $"Owner character uses persistent ID '{id.Value}' instead of '{CharacterId.Owner.Value}'.");
        }
        else if (!actor.isOwner && id.Equals(CharacterId.Owner))
        {
            report.AddError(
                "A non-owner character uses the reserved owner persistent ID.");
        }

        if (!charactersById.TryGetValue(actor.dataId, out CharacterSO definition))
        {
            report.AddError(
                $"Character definition {actor.dataId} does not exist in the run catalog.");
        }
        else if (actor.role != definition.role)
        {
            report.AddError(
                $"Character '{id.Value}' role '{actor.role}' does not match authored role '{definition.role}'.");
        }

        if (!Enum.IsDefined(typeof(CharacterType), actor.characterType)
            || !Enum.IsDefined(typeof(CharacterRole), actor.role)
            || !Enum.IsDefined(
                typeof(CharacterLifecycleState),
                actor.lifecycleState)
            || !Enum.IsDefined(
                typeof(AbilityWork.DutyState),
                actor.dutyState))
        {
            report.AddError(
                $"Character '{id.Value}' contains an unknown enum value.");
        }
        if (!IsFinite(actor.currentHealth)
            || !IsFinite(actor.injurySeverity)
            || !IsFinite(actor.baseMood))
        {
            report.AddError(
                $"Character '{id.Value}' contains a non-finite health or mood value.");
        }

        Vector2Int position = new Vector2Int(actor.gridX, actor.gridY);
        if (RequiresWalkableRestoreCell(actor.lifecycleState)
            && (!grid.IsValidGridPos(position) || !grid.IsWalkable(position)))
        {
            report.AddError(
                $"Character '{id.Value}' restore cell ({actor.gridX}, {actor.gridY}) is not walkable in the candidate grid.");
        }

        CharacterWorldSaveValidation.ValidateActor(actor, id.Value, report);
    }

    private static void ValidateProfile(
        WorldCharacterProfile profile,
        IReadOnlyDictionary<int, CharacterSO> charactersById,
        DungeonGameRestoreReport report,
        bool allowLegacyCharacterIds)
    {
        if (!CharacterV18RestoreIdentityResolver.TryResolve(
                profile.persistentId,
                allowLegacyCharacterIds,
                out CharacterId id,
                out _))
        {
            report.AddError(
                $"Population profile {profile.characterDataId} has no valid persistent ID.");
        }
        if (!charactersById.ContainsKey(profile.characterDataId))
        {
            report.AddError(
                $"Population profile '{id.Value}' references missing character definition {profile.characterDataId}.");
        }
    }

    private static bool IsFinite(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value);
    }

    public static bool RequiresWalkableRestoreCell(
        CharacterLifecycleState lifecycleState)
    {
        return lifecycleState == CharacterLifecycleState.Active
            || lifecycleState == CharacterLifecycleState.Downed;
    }
}
