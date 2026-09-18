using System;
using System.Collections.Generic;
using System.Linq;

[Serializable]
public sealed class SeasonalWildlifeArrivalMemberSaveData
{
    public string wildlifeId = string.Empty;
    public int positionX;
    public int positionY;
    public bool spawned;
}

[Serializable]
public sealed class SeasonalWildlifeArrivalSaveData
{
    public bool configured;
    public string speciesId = string.Empty;
    public int exactCount;
    public string requiredHabitatId = string.Empty;
    public SeasonalWildlifeArrivalQualification qualification;
    public List<SeasonalWildlifeArrivalMemberSaveData> members = new();
    public string lastFailureReason = string.Empty;

    public static SeasonalWildlifeArrivalSaveData FromProfile(
        SeasonalWildlifeArrivalProfile profile)
    {
        if (profile?.IsConfigured != true)
        {
            return new SeasonalWildlifeArrivalSaveData();
        }

        return new SeasonalWildlifeArrivalSaveData
        {
            configured = true,
            speciesId = profile.speciesId,
            exactCount = profile.exactCount,
            requiredHabitatId = profile.requiredHabitatId,
            qualification = profile.qualification
        };
    }
}

public interface ISeasonalWildlifeArrivalCommand
{
    bool TryRecordSeasonalWildlifeArrivalPlan(
        string occurrenceInstanceId,
        IReadOnlyList<SeasonalWildlifeArrivalMemberSaveData> members,
        out string failureReason);
    bool TryRecordSeasonalWildlifeArrivalFailure(
        string occurrenceInstanceId,
        string failureReason);
    bool TryMarkSeasonalWildlifeArrivalSpawned(
        string occurrenceInstanceId,
        string wildlifeId,
        out string failureReason);
}

public static class SeasonalWildlifeArrivalRules
{
    private const int MaximumFailureReasonLength = 256;

    public static bool TryParseHabitat(
        string habitatId,
        out WildlifeHabitatType habitat)
    {
        switch (habitatId)
        {
            case "grass":
                habitat = WildlifeHabitatType.Grass;
                return true;
            case "water":
                habitat = WildlifeHabitatType.Water;
                return true;
            case "burrow":
                habitat = WildlifeHabitatType.Burrow;
                return true;
            case "brush":
                habitat = WildlifeHabitatType.Brush;
                return true;
            case "lair":
                habitat = WildlifeHabitatType.Lair;
                return true;
            default:
                habitat = default;
                return false;
        }
    }

    public static bool MatchesQualification(
        WildlifeSpeciesDefinition species,
        SeasonalWildlifeArrivalQualification qualification) =>
        qualification switch
        {
            SeasonalWildlifeArrivalQualification.Predatory =>
                species != null
                && species.Diet is WildlifeDietType.Carnivore
                    or WildlifeDietType.Scavenger
                && species.PredationDrive > 0f
                && species.PreySpeciesIds.Count > 0
                && species.IsDangerous,
            SeasonalWildlifeArrivalQualification.NonHostile =>
                species != null
                && species.Diet == WildlifeDietType.Herbivore
                && !species.IsDangerous,
            _ => false
        };

    public static void RequireValidAuthoredProfile(
        SeasonalWorldEventDefinitionSO definition,
        WildlifeSpeciesDefinition species)
    {
        if (definition == null)
            throw new ArgumentNullException(nameof(definition));
        SeasonalWildlifeArrivalProfile profile =
            definition.wildlifeArrivalProfile;
        if (profile?.IsConfigured != true)
        {
            return;
        }

        IReadOnlyList<string> localErrors = profile.Validate(
            definition.StableId);
        if (localErrors.Count > 0)
            throw new InvalidOperationException(string.Join(" | ", localErrors));
        if (species == null
            || !string.Equals(
                profile.speciesId,
                species.SpeciesId,
                StringComparison.Ordinal))
            throw new InvalidOperationException(
                $"Seasonal wildlife event '{definition.StableId}' references unknown species '{profile.speciesId}'.");
        if (!TryParseHabitat(
                profile.requiredHabitatId,
                out WildlifeHabitatType habitat)
            || !species.PreferredHabitats.Contains(habitat))
            throw new InvalidOperationException(
                $"Seasonal wildlife event '{definition.StableId}' requires a non-preferred habitat '{profile.requiredHabitatId}'.");
        if (!species.IsActiveIn(definition.season))
            throw new InvalidOperationException(
                $"Seasonal wildlife event '{definition.StableId}' uses species '{species.SpeciesId}' outside its active season.");
        if (!MatchesQualification(species, profile.qualification))
            throw new InvalidOperationException(
                $"Seasonal wildlife event '{definition.StableId}' species '{species.SpeciesId}' does not satisfy {profile.qualification} qualification.");
    }

    public static void RequireValidFrozenState(
        SeasonalWildlifeArrivalSaveData state,
        SeasonalWildlifeArrivalProfile authored,
        string occurrenceInstanceId)
    {
        state ??= new SeasonalWildlifeArrivalSaveData();
        bool authoredConfigured = authored?.IsConfigured == true;
        if (!authoredConfigured)
        {
            if (HasAnyState(state))
                throw new InvalidOperationException(
                    $"Seasonal event '{occurrenceInstanceId}' contains an unauthored wildlife arrival.");
            return;
        }

        if (!state.configured
            || !string.Equals(
                state.speciesId,
                authored.speciesId,
                StringComparison.Ordinal)
            || state.exactCount != authored.exactCount
            || !string.Equals(
                state.requiredHabitatId,
                authored.requiredHabitatId,
                StringComparison.Ordinal)
            || state.qualification != authored.qualification)
            throw new InvalidOperationException(
                $"Seasonal event '{occurrenceInstanceId}' wildlife arrival does not match its authored frozen profile.");
        if (state.members == null
            || state.members.Count != 0
                && state.members.Count != state.exactCount)
            throw new InvalidOperationException(
                $"Seasonal event '{occurrenceInstanceId}' wildlife arrival must be unplanned or contain its exact count.");
        if (!CanonicalOptionalFailure(state.lastFailureReason))
            throw new InvalidOperationException(
                $"Seasonal event '{occurrenceInstanceId}' wildlife arrival failure state is invalid.");

        HashSet<string> ids = new(StringComparer.Ordinal);
        HashSet<(int X, int Y)> positions = new();
        foreach (SeasonalWildlifeArrivalMemberSaveData member in state.members)
        {
            if (member == null
                || !TryParseWildlifeId(member.wildlifeId, out _)
                || !ids.Add(member.wildlifeId)
                || !positions.Add((member.positionX, member.positionY)))
                throw new InvalidOperationException(
                    $"Seasonal event '{occurrenceInstanceId}' wildlife arrival plan contains invalid or duplicate members.");
        }
    }

    public static void ValidateRestoreJoin(
        SeasonalEventWorldSaveData payload,
        IReadOnlyList<WildlifeActor> candidateWildlife)
    {
        if (payload?.activeEvents == null || candidateWildlife == null)
            throw new InvalidOperationException(
                "Seasonal wildlife restore requires seasonal and wildlife candidates.");

        Dictionary<string, WildlifeActor> actors = candidateWildlife
            .Where(value => value != null && value.IsAlive)
            .ToDictionary(value => value.WildlifeId, StringComparer.Ordinal);
        HashSet<string> plannedIds = new(StringComparer.Ordinal);
        foreach (V20ActiveEventSaveData occurrence in payload.activeEvents
                     .Where(value =>
                         value?.seasonalWildlifeArrival?.configured == true))
        {
            SeasonalWildlifeArrivalSaveData arrival =
                occurrence.seasonalWildlifeArrival;
            foreach (SeasonalWildlifeArrivalMemberSaveData member in
                     arrival.members)
            {
                if (!plannedIds.Add(member.wildlifeId))
                    throw new InvalidOperationException(
                        $"Seasonal wildlife restore duplicates planned ID '{member.wildlifeId}'.");
                bool exists = actors.TryGetValue(
                    member.wildlifeId,
                    out WildlifeActor actor);
                if (exists
                    && !string.Equals(
                        actor.SpeciesId,
                        arrival.speciesId,
                        StringComparison.Ordinal))
                    throw new InvalidOperationException(
                        $"Seasonal wildlife '{member.wildlifeId}' has an incoming species mismatch.");
            }
        }
    }

    public static bool SamePlan(
        IReadOnlyList<SeasonalWildlifeArrivalMemberSaveData> left,
        IReadOnlyList<SeasonalWildlifeArrivalMemberSaveData> right)
    {
        if (left == null || right == null || left.Count != right.Count)
        {
            return false;
        }
        for (int index = 0; index < left.Count; index++)
        {
            SeasonalWildlifeArrivalMemberSaveData a = left[index];
            SeasonalWildlifeArrivalMemberSaveData b = right[index];
            if (a == null || b == null
                || !string.Equals(a.wildlifeId, b.wildlifeId, StringComparison.Ordinal)
                || a.positionX != b.positionX
                || a.positionY != b.positionY)
            {
                return false;
            }
        }
        return true;
    }

    public static bool TryParseWildlifeId(string wildlifeId, out int sequence)
    {
        const string prefix = "wild:";
        sequence = 0;
        return !string.IsNullOrWhiteSpace(wildlifeId)
            && string.Equals(
                wildlifeId,
                wildlifeId.Trim(),
                StringComparison.Ordinal)
            && wildlifeId.StartsWith(prefix, StringComparison.Ordinal)
            && int.TryParse(wildlifeId.Substring(prefix.Length), out sequence)
            && sequence > 0;
    }

    public static SeasonalWildlifeArrivalMemberSaveData CloneMember(
        SeasonalWildlifeArrivalMemberSaveData source) => new()
    {
        wildlifeId = source.wildlifeId,
        positionX = source.positionX,
        positionY = source.positionY,
        spawned = source.spawned
    };

    private static bool HasAnyState(SeasonalWildlifeArrivalSaveData state) =>
        state.configured
        || !string.IsNullOrEmpty(state.speciesId)
        || state.exactCount != 0
        || !string.IsNullOrEmpty(state.requiredHabitatId)
        || state.qualification != SeasonalWildlifeArrivalQualification.None
        || (state.members?.Count ?? 0) > 0
        || !string.IsNullOrEmpty(state.lastFailureReason);

    private static bool CanonicalOptionalFailure(string value) =>
        value != null
        && value.Length <= MaximumFailureReasonLength
        && (value.Length == 0
            || !string.IsNullOrWhiteSpace(value)
                && string.Equals(
                    value,
                    value.Trim(),
                    StringComparison.Ordinal));
}
