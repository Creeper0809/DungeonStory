using System;
using System.Collections.Generic;
using System.Linq;

public readonly struct HouseholdId : IEquatable<HouseholdId>
{
    private readonly string value;
    public HouseholdId(string value) => this.value = PersistentEntityId.Normalize(value);
    public string Value => value ?? string.Empty;
    public bool IsValid => PersistentEntityId.IsKind(Value, "household");
    public bool Equals(HouseholdId other) => PersistentEntityId.Equals(Value, other.Value);
    public override bool Equals(object obj) => obj is HouseholdId other && Equals(other);
    public override int GetHashCode() => PersistentEntityId.GetHashCode(Value);
}

[Serializable]
public sealed class CharacterRoomAssignmentSaveData
{
    public string characterId = string.Empty;
    public string householdId = string.Empty;
    public string roomBuildingId = string.Empty;
    public string bedBuildingId = string.Empty;
}

[Serializable]
public sealed class HouseholdWorldSaveData
{
    public const int CurrentVersion = 1;
    public int version = CurrentVersion;
    public List<CharacterRoomAssignmentSaveData> assignments = new();
}

public sealed class CharacterHouseholdAggregate
{
    private readonly Dictionary<CharacterId, CharacterRoomAssignmentSaveData> assignments = new();

    public void Assign(
        CharacterId characterId,
        HouseholdId householdId,
        BuildingInstanceId roomId,
        BuildingInstanceId bedId)
    {
        if (!characterId.IsValid || !householdId.IsValid
            || !roomId.IsValid || !bedId.IsValid)
            throw new InvalidOperationException("Room assignment requires canonical character, household, room, and bed ids.");
        if (assignments.Values.Any(value => string.Equals(
                value.bedBuildingId, bedId.Value, StringComparison.Ordinal)
            && !string.Equals(value.characterId, characterId.Value, StringComparison.Ordinal)))
            throw new InvalidOperationException("A bed can be assigned to only one character.");
        assignments[characterId] = new CharacterRoomAssignmentSaveData
        {
            characterId = characterId.Value,
            householdId = householdId.Value,
            roomBuildingId = roomId.Value,
            bedBuildingId = bedId.Value
        };
    }

    public void Clear(CharacterId characterId) => assignments.Remove(characterId);
    public bool TryGet(CharacterId id, out CharacterRoomAssignmentSaveData assignment) =>
        assignments.TryGetValue(id, out assignment);
    public IReadOnlyList<CharacterId> GetMembers(HouseholdId householdId) =>
        assignments.Values
            .Where(value => string.Equals(
                value.householdId,
                householdId.Value,
                StringComparison.Ordinal))
            .Select(value => new CharacterId(value.characterId))
            .OrderBy(value => value.Value, StringComparer.Ordinal)
            .ToArray();
    public HouseholdWorldSaveData Capture() => new()
    {
        assignments = assignments.Values
            .OrderBy(value => value.characterId, StringComparer.Ordinal)
            .ToList()
    };

    public static CharacterHouseholdAggregate Restore(HouseholdWorldSaveData data)
    {
        if (data == null || data.version != HouseholdWorldSaveData.CurrentVersion
            || data.assignments == null)
            throw new InvalidOperationException("Household payload is incomplete or unsupported.");
        CharacterHouseholdAggregate result = new();
        foreach (CharacterRoomAssignmentSaveData assignment in data.assignments)
        {
            if (assignment == null)
                throw new InvalidOperationException("Household payload contains a null assignment.");
            result.Assign(
                new CharacterId(assignment.characterId),
                new HouseholdId(assignment.householdId),
                new BuildingInstanceId(assignment.roomBuildingId),
                new BuildingInstanceId(assignment.bedBuildingId));
        }
        return result;
    }
}

public enum CareerRank
{
    Apprentice = 0,
    Skilled = 1,
    Technician = 2,
    Expert = 3,
    Master = 4
}

public enum CareerPositionKind
{
    None = 0,
    Steward = 1,
    ChiefResearcher = 2,
    ChiefPhysician = 3,
    GuardCaptain = 4,
    Foreman = 5,
    Mentor = 6
}

public enum CareerHistoryEventKind
{
    RoleChanged = 0,
    RankChanged = 1,
    PositionChanged = 2,
    Retired = 3
}

[Serializable]
public sealed class CareerHistoryEventSaveData
{
    public CareerHistoryEventKind kind;
    public int absoluteDay;
    public string valueId = string.Empty;
}

[Serializable]
public sealed class CharacterCareerSaveData
{
    public string characterId = string.Empty;
    public bool retired;
    public CareerPositionKind position;
    public string positionScopeId = string.Empty;
    public int retiredWorkAbsoluteDay;
    public float retiredWorkSeconds;
    public int summarizedHistoryCount;
    public List<CareerHistoryEventSaveData> recentHistory = new();
}

public readonly struct CharacterCareerSnapshot
{
    public CharacterCareerSnapshot(
        CharacterId characterId,
        bool retired,
        CareerPositionKind position,
        string positionScopeId,
        int retiredWorkAbsoluteDay,
        float retiredWorkSeconds)
    {
        CharacterId = characterId;
        Retired = retired;
        Position = position;
        PositionScopeId = positionScopeId ?? string.Empty;
        RetiredWorkAbsoluteDay = retiredWorkAbsoluteDay;
        RetiredWorkSeconds = Math.Max(0f, retiredWorkSeconds);
    }

    public CharacterId CharacterId { get; }
    public bool Retired { get; }
    public CareerPositionKind Position { get; }
    public string PositionScopeId { get; }
    public int RetiredWorkAbsoluteDay { get; }
    public float RetiredWorkSeconds { get; }
}

[Serializable]
public sealed class CharacterCareerWorldSaveData
{
    public const int CurrentVersion = 1;
    public int version = CurrentVersion;
    public List<CharacterCareerSaveData> characters = new();
    public List<CareerMentorshipSaveData> mentorships = new();
}

[Serializable]
public sealed class CareerMentorshipSaveData
{
    public string mentorCharacterId = string.Empty;
    public string studentCharacterId = string.Empty;
    public string academyBuildingId = string.Empty;
    public int lastAwardAbsoluteDay;
}

public readonly struct CareerMentorshipSnapshot
{
    public CareerMentorshipSnapshot(
        CharacterId mentorCharacterId,
        CharacterId studentCharacterId,
        BuildingInstanceId academyBuildingId,
        int lastAwardAbsoluteDay)
    {
        MentorCharacterId = mentorCharacterId;
        StudentCharacterId = studentCharacterId;
        AcademyBuildingId = academyBuildingId;
        LastAwardAbsoluteDay = lastAwardAbsoluteDay;
    }

    public CharacterId MentorCharacterId { get; }
    public CharacterId StudentCharacterId { get; }
    public BuildingInstanceId AcademyBuildingId { get; }
    public int LastAwardAbsoluteDay { get; }
}

public static class CareerRules
{
    public const int RetireeMaximumSafeWorkHours = 4;
    public const int MaximumDailyMentoringXp = 10;
    public const int MaximumRecentHistory = 64;
    public const float RetireeMaximumSafeWorkSeconds =
        GameCalendarRules.SecondsPerDay
        * RetireeMaximumSafeWorkHours
        / GameCalendarRules.HoursPerDay;

    public static CareerRank ResolveRank(int existingSkillXp)
    {
        int xp = Math.Max(0, existingSkillXp);
        if (xp >= 3000) return CareerRank.Master;
        if (xp >= 1200) return CareerRank.Expert;
        if (xp >= 400) return CareerRank.Technician;
        if (xp >= 100) return CareerRank.Skilled;
        return CareerRank.Apprentice;
    }

    public static int ResolveMentoringXp(int requestedXp) =>
        Math.Clamp(requestedXp, 0, MaximumDailyMentoringXp);
}

public sealed class CharacterCareerAggregate
{
    private readonly Dictionary<CharacterId, CharacterCareerSaveData> careers = new();
    private readonly Dictionary<CharacterId, CareerMentorshipSaveData> mentorships = new();

    public IReadOnlyList<CareerMentorshipSnapshot> Mentorships => mentorships.Values
        .OrderBy(value => value.studentCharacterId, StringComparer.Ordinal)
        .Select(Snapshot)
        .ToArray();

    public CharacterCareerSaveData Require(CharacterId characterId)
    {
        if (!characterId.IsValid) throw new ArgumentException("A valid character is required.", nameof(characterId));
        if (!careers.TryGetValue(characterId, out CharacterCareerSaveData state))
        {
            state = new CharacterCareerSaveData { characterId = characterId.Value };
            careers.Add(characterId, state);
        }
        return state;
    }

    public bool TryGet(
        CharacterId characterId,
        out CharacterCareerSnapshot snapshot)
    {
        if (careers.TryGetValue(characterId, out CharacterCareerSaveData state))
        {
            snapshot = Snapshot(state);
            return true;
        }
        snapshot = default;
        return false;
    }

    public bool CanPerformRetiredWork(
        CharacterId characterId,
        int absoluteDay,
        bool safeWork,
        out string reason)
    {
        reason = string.Empty;
        if (!careers.TryGetValue(characterId, out CharacterCareerSaveData state)
            || !state.retired)
            return true;
        if (!safeWork)
        {
            reason = "career:retiree-unsafe-work";
            return false;
        }
        float worked = state.retiredWorkAbsoluteDay == absoluteDay
            ? state.retiredWorkSeconds
            : 0f;
        if (worked + 0.0001f >= CareerRules.RetireeMaximumSafeWorkSeconds)
        {
            reason = "career:retiree-daily-limit";
            return false;
        }
        return true;
    }

    public void RecordRetiredWork(
        CharacterId characterId,
        int absoluteDay,
        float elapsedSeconds)
    {
        if (absoluteDay < 1 || elapsedSeconds < 0f
            || float.IsNaN(elapsedSeconds) || float.IsInfinity(elapsedSeconds))
            throw new ArgumentOutOfRangeException(nameof(elapsedSeconds));
        CharacterCareerSaveData state = Require(characterId);
        if (!state.retired || elapsedSeconds <= 0f)
            return;
        if (state.retiredWorkAbsoluteDay != absoluteDay)
        {
            state.retiredWorkAbsoluteDay = absoluteDay;
            state.retiredWorkSeconds = 0f;
        }
        state.retiredWorkSeconds = Math.Min(
            CareerRules.RetireeMaximumSafeWorkSeconds,
            state.retiredWorkSeconds + elapsedSeconds);
    }

    public void Retire(CharacterId characterId, int absoluteDay)
    {
        CharacterCareerSaveData state = Require(characterId);
        if (state.retired) return;
        state.retired = true;
        AddHistory(state, CareerHistoryEventKind.Retired, absoluteDay, "retired");
    }

    public void AssignPosition(
        CharacterId characterId,
        CareerPositionKind position,
        string scopeId,
        int absoluteDay)
    {
        string normalizedScope = scopeId?.Trim() ?? string.Empty;
        if (position == CareerPositionKind.None) normalizedScope = string.Empty;
        if (position is CareerPositionKind.Foreman or CareerPositionKind.Mentor
            && normalizedScope.Length == 0)
            throw new InvalidOperationException("Scoped career positions require a facility or academy id.");
        bool global = position is CareerPositionKind.Steward
            or CareerPositionKind.ChiefResearcher
            or CareerPositionKind.ChiefPhysician
            or CareerPositionKind.GuardCaptain;
        if (careers.Values.Any(value => value.position == position
            && (global || string.Equals(value.positionScopeId, normalizedScope, StringComparison.Ordinal))
            && !string.Equals(value.characterId, characterId.Value, StringComparison.Ordinal)))
            throw new InvalidOperationException("That unique career position is already occupied.");
        CharacterCareerSaveData state = Require(characterId);
        state.position = position;
        state.positionScopeId = normalizedScope;
        AddHistory(state, CareerHistoryEventKind.PositionChanged, absoluteDay, position.ToString());
    }

    public void AssignMentorship(
        CharacterId mentorCharacterId,
        CharacterId studentCharacterId,
        BuildingInstanceId academyBuildingId)
    {
        if (!mentorCharacterId.IsValid || !studentCharacterId.IsValid
            || mentorCharacterId.Equals(studentCharacterId)
            || !academyBuildingId.IsValid)
        {
            throw new InvalidOperationException(
                "Mentorship requires different canonical characters and an academy building.");
        }
        Require(mentorCharacterId);
        Require(studentCharacterId);
        mentorships[studentCharacterId] = new CareerMentorshipSaveData
        {
            mentorCharacterId = mentorCharacterId.Value,
            studentCharacterId = studentCharacterId.Value,
            academyBuildingId = academyBuildingId.Value
        };
    }

    public void ClearMentorship(CharacterId studentCharacterId) =>
        mentorships.Remove(studentCharacterId);

    public bool TryMarkMentoringAwarded(
        CharacterId studentCharacterId,
        int absoluteDay)
    {
        if (absoluteDay < 1)
            throw new ArgumentOutOfRangeException(nameof(absoluteDay));
        if (!mentorships.TryGetValue(
                studentCharacterId,
                out CareerMentorshipSaveData assignment))
            throw new KeyNotFoundException(
                $"No mentorship exists for '{studentCharacterId.Value}'.");
        if (assignment.lastAwardAbsoluteDay >= absoluteDay)
            return false;
        assignment.lastAwardAbsoluteDay = absoluteDay;
        return true;
    }

    public IReadOnlyList<CharacterCareerSaveData> Capture() => careers.Values
        .OrderBy(value => value.characterId, StringComparer.Ordinal)
        .ToArray();

    public CharacterCareerWorldSaveData CaptureWorld() => new()
    {
        characters = Capture().Select(Clone).ToList(),
        mentorships = mentorships.Values
            .OrderBy(value => value.studentCharacterId, StringComparer.Ordinal)
            .Select(Clone)
            .ToList()
    };

    public static CharacterCareerAggregate Restore(CharacterCareerWorldSaveData data)
    {
        if (data == null || data.version != CharacterCareerWorldSaveData.CurrentVersion
            || data.characters == null)
            throw new InvalidOperationException("Career payload is incomplete or unsupported.");
        CharacterCareerAggregate result = new();
        foreach (CharacterCareerSaveData source in data.characters)
        {
            CharacterId id = new(source?.characterId);
            if (source == null || !id.IsValid || result.careers.ContainsKey(id)
                || source.summarizedHistoryCount < 0
                || source.retiredWorkAbsoluteDay < 0
                || source.retiredWorkSeconds < 0f
                || float.IsNaN(source.retiredWorkSeconds)
                || float.IsInfinity(source.retiredWorkSeconds)
                || source.retiredWorkSeconds
                    > CareerRules.RetireeMaximumSafeWorkSeconds + 0.001f
                || source.recentHistory == null
                || source.recentHistory.Count > CareerRules.MaximumRecentHistory
                || source.recentHistory.Any(value => value == null || value.absoluteDay < 1))
                throw new InvalidOperationException("Career record is invalid or duplicated.");
            result.careers.Add(id, Clone(source));
        }
        foreach (IGrouping<(CareerPositionKind Position, string Scope), CharacterCareerSaveData> duplicate in
                 result.careers.Values
                     .Where(value => value.position != CareerPositionKind.None)
                     .GroupBy(value =>
                     {
                         bool global = value.position is CareerPositionKind.Steward
                             or CareerPositionKind.ChiefResearcher
                             or CareerPositionKind.ChiefPhysician
                             or CareerPositionKind.GuardCaptain;
                         return (Position: value.position,
                             Scope: global ? string.Empty : value.positionScopeId ?? string.Empty);
                     }).Where(group => group.Count() > 1))
            throw new InvalidOperationException(
                $"Career position '{duplicate.Key.Position}' has duplicate occupants.");
        foreach (CareerMentorshipSaveData source in data.mentorships
                     ?? new List<CareerMentorshipSaveData>())
        {
            CharacterId mentorId = new(source?.mentorCharacterId);
            CharacterId studentId = new(source?.studentCharacterId);
            BuildingInstanceId academyId = new(source?.academyBuildingId);
            if (source == null || !mentorId.IsValid || !studentId.IsValid
                || mentorId.Equals(studentId) || !academyId.IsValid
                || source.lastAwardAbsoluteDay < 0
                || !result.careers.ContainsKey(mentorId)
                || !result.careers.ContainsKey(studentId)
                || !result.mentorships.TryAdd(studentId, Clone(source)))
            {
                throw new InvalidOperationException(
                    "Career mentorship is invalid or duplicated.");
            }
        }
        return result;
    }

    private static void AddHistory(
        CharacterCareerSaveData state,
        CareerHistoryEventKind kind,
        int absoluteDay,
        string valueId)
    {
        state.recentHistory ??= new List<CareerHistoryEventSaveData>();
        state.recentHistory.Add(new CareerHistoryEventSaveData
        {
            kind = kind,
            absoluteDay = absoluteDay,
            valueId = valueId ?? string.Empty
        });
        while (state.recentHistory.Count > CareerRules.MaximumRecentHistory)
        {
            state.recentHistory.RemoveAt(0);
            state.summarizedHistoryCount++;
        }
    }

    private static CharacterCareerSaveData Clone(CharacterCareerSaveData value) => new()
    {
        characterId = value.characterId,
        retired = value.retired,
        position = value.position,
        positionScopeId = value.positionScopeId,
        retiredWorkAbsoluteDay = value.retiredWorkAbsoluteDay,
        retiredWorkSeconds = value.retiredWorkSeconds,
        summarizedHistoryCount = value.summarizedHistoryCount,
        recentHistory = (value.recentHistory ?? new()).Select(entry => new CareerHistoryEventSaveData
        {
            kind = entry.kind,
            absoluteDay = entry.absoluteDay,
            valueId = entry.valueId
        }).ToList()
    };

    private static CharacterCareerSnapshot Snapshot(CharacterCareerSaveData value) =>
        new(
            new CharacterId(value.characterId),
            value.retired,
            value.position,
            value.positionScopeId,
            value.retiredWorkAbsoluteDay,
            value.retiredWorkSeconds);

    private static CareerMentorshipSnapshot Snapshot(CareerMentorshipSaveData value) =>
        new(
            new CharacterId(value.mentorCharacterId),
            new CharacterId(value.studentCharacterId),
            new BuildingInstanceId(value.academyBuildingId),
            value.lastAwardAbsoluteDay);

    private static CareerMentorshipSaveData Clone(CareerMentorshipSaveData value) =>
        new()
        {
            mentorCharacterId = value.mentorCharacterId,
            studentCharacterId = value.studentCharacterId,
            academyBuildingId = value.academyBuildingId,
            lastAwardAbsoluteDay = value.lastAwardAbsoluteDay
        };
}

public interface ICareerService
{
    IReadOnlyList<CareerMentorshipSnapshot> Mentorships { get; }
    bool TryGet(CharacterId characterId, out CharacterCareerSnapshot snapshot);
    void Retire(CharacterId characterId, int absoluteDay);
    void AssignPosition(CharacterId characterId, CareerPositionKind position, string scopeId, int absoluteDay);
    bool CanPerformRetiredWork(
        CharacterId characterId,
        int absoluteDay,
        bool safeWork,
        out string reason);
    void RecordRetiredWork(
        CharacterId characterId,
        int absoluteDay,
        float elapsedSeconds);
    void AssignMentorship(
        CharacterId mentorCharacterId,
        CharacterId studentCharacterId,
        BuildingInstanceId academyBuildingId);
    void ClearMentorship(CharacterId studentCharacterId);
    bool TryMarkMentoringAwarded(
        CharacterId studentCharacterId,
        int absoluteDay);
    int ResolveMentoringXp(int requestedXp);
}
