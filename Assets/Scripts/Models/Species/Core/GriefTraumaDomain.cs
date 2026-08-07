using System;
using System.Collections.Generic;
using System.Linq;

public enum CharacterDeathCauseCode
{
    Unknown = 0,
    Combat = 1,
    Infection = 2,
    Starvation = 3,
    Execution = 4,
    AgeConditionOrganFailure = 5,
    ConstructFailure = 6,
    Dehydration = 7,
    MedicalProcedureFailure = 8,
    Expedition = 9
}

public sealed class CharacterLifeDeathRecord
{
    public CharacterLifeDeathRecord(
        CharacterId characterId,
        CharacterDeathCauseCode cause,
        int absoluteDay,
        CoreGridCell location,
        IEnumerable<CharacterId> witnessIds)
    {
        if (!characterId.IsValid || absoluteDay < 1)
            throw new ArgumentException("Death events require a valid character and day.");
        CharacterId = characterId;
        Cause = cause;
        AbsoluteDay = absoluteDay;
        Location = location;
        WitnessIds = (witnessIds ?? Array.Empty<CharacterId>())
            .Where(value => value.IsValid)
            .Distinct()
            .OrderBy(value => value.Value, StringComparer.Ordinal)
            .ToArray();
    }

    public CharacterId CharacterId { get; }
    public CharacterDeathCauseCode Cause { get; }
    public int AbsoluteDay { get; }
    public CoreGridCell Location { get; }
    public IReadOnlyList<CharacterId> WitnessIds { get; }
}

public enum GriefRelationshipKind
{
    PartnerOrChild = 0,
    ParentSiblingOrGuardian = 1,
    Household = 2,
    Colleague = 3,
    Acquaintance = 4
}

[Flags]
public enum TraumaThresholdEffect
{
    None = 0,
    IntrusiveMemory = 1,
    WorkEfficiencyPenalty = 2,
    BreakdownRisk = 4
}

[Serializable]
public sealed class GriefIncidentSaveData
{
    public string deceasedCharacterId = string.Empty;
    public GriefRelationshipKind relationship;
    public int deathAbsoluteDay;
    public int durationDays;
    public float initialPenalty;
    public float remainingMultiplier = 1f;
    public bool funeralCompleted;
    public bool missedFuneralTraumaApplied;
}

[Serializable]
public sealed class TraumaEventSaveData
{
    public string eventType = string.Empty;
    public int absoluteDay;
    public float amount;
}

[Serializable]
public sealed class FestivalAttendanceSaveData
{
    public string festivalId = string.Empty;
    public int year;
}

[Serializable]
public sealed class CharacterPsychosocialRecordSaveData
{
    public string characterId = string.Empty;
    public float trauma;
    public float memorialResolveAmount;
    public int memorialResolveExpiresDay;
    public int lastLongNightMemorialYear;
    public List<GriefIncidentSaveData> grief = new();
    public List<TraumaEventSaveData> recentTraumaEvents = new();
    public List<TraumaEventSaveData> compressedTraumaByType = new();
    public List<FestivalAttendanceSaveData> festivalAttendance = new();
}

[Serializable]
public sealed class CharacterPsychosocialWorldSaveData
{
    public const int CurrentVersion = 1;
    public int version = CurrentVersion;
    public List<CharacterPsychosocialRecordSaveData> characters = new();
}

public sealed class CharacterGriefAggregate
{
    public const float MaximumGriefPenalty = -20f;
    public const float MaximumMemorialResolve = 8f;
    public const int MaximumRecentTraumaEvents = 32;

    private readonly Dictionary<CharacterId, GriefIncidentSaveData> grief = new();
    private readonly List<TraumaEventSaveData> traumaEvents = new();
    private readonly Dictionary<string, TraumaEventSaveData> compressedTrauma =
        new(StringComparer.Ordinal);
    private readonly Dictionary<string, int> festivalAttendance =
        new(StringComparer.Ordinal);

    public CharacterGriefAggregate(CharacterId characterId)
    {
        if (!characterId.IsValid) throw new ArgumentException("A valid character is required.", nameof(characterId));
        CharacterId = characterId;
    }

    public CharacterId CharacterId { get; }
    public float Trauma { get; private set; }
    public float MemorialResolveAmount { get; private set; }
    public int MemorialResolveExpiresDay { get; private set; }
    public int LastLongNightMemorialYear { get; private set; }

    public bool HasAttendedFestival(string festivalId, int year) =>
        festivalAttendance.TryGetValue(
            festivalId?.Trim() ?? string.Empty,
            out int attendedYear)
        && attendedYear == year;

    public void RecordFestivalAttendance(string festivalId, int year)
    {
        string normalized = festivalId?.Trim() ?? string.Empty;
        if (normalized.Length == 0 || year < 1)
            throw new ArgumentException("Festival attendance requires an id and year.");
        if (HasAttendedFestival(normalized, year))
            throw new InvalidOperationException(
                "The character already attended this festival this year.");
        festivalAttendance[normalized] = year;
    }

    public void RecordDeath(
        CharacterLifeDeathRecord death,
        GriefRelationshipKind relationship)
    {
        if (death == null) throw new ArgumentNullException(nameof(death));
        if (death.CharacterId.Equals(CharacterId)) return;
        (float penalty, int duration) = GetGriefRule(relationship);
        grief[death.CharacterId] = new GriefIncidentSaveData
        {
            deceasedCharacterId = death.CharacterId.Value,
            relationship = relationship,
            deathAbsoluteDay = death.AbsoluteDay,
            durationDays = duration,
            initialPenalty = penalty,
            remainingMultiplier = 1f
        };
    }

    public float GetProjectedGriefMood(int currentAbsoluteDay)
    {
        float total = grief.Values.Sum(value => RemainingPenalty(value, currentAbsoluteDay));
        return Math.Max(MaximumGriefPenalty, total);
    }

    public float GetProjectedMemorialResolve(int currentAbsoluteDay) =>
        currentAbsoluteDay <= MemorialResolveExpiresDay
            ? Math.Clamp(MemorialResolveAmount, 0f, MaximumMemorialResolve)
            : 0f;

    public void CompleteFuneral(
        CharacterId deceasedCharacterId,
        int funeralAbsoluteDay,
        bool matchingSpeciesRitual)
    {
        if (!grief.TryGetValue(deceasedCharacterId, out GriefIncidentSaveData incident))
            throw new InvalidOperationException("No active grief incident exists for that deceased character.");
        if (incident.funeralCompleted)
            throw new InvalidOperationException("The funeral was already applied to this grief incident.");
        if (funeralAbsoluteDay < incident.deathAbsoluteDay)
            throw new InvalidOperationException("A funeral cannot precede death.");
        incident.funeralCompleted = true;
        if (funeralAbsoluteDay - incident.deathAbsoluteDay <= 7)
            incident.remainingMultiplier *= 0.4f;
        MemorialResolveAmount = Math.Min(
            MaximumMemorialResolve,
            6f + (matchingSpeciesRitual ? 2f : 0f));
        MemorialResolveExpiresDay = funeralAbsoluteDay + 15;
        ReduceTrauma(8f);
    }

    public void CompleteJointMemorial(
        IReadOnlyList<CharacterId> deceasedCharacterIds,
        int funeralAbsoluteDay,
        bool matchingSpeciesRitual)
    {
        CharacterId[] ids = (deceasedCharacterIds ?? Array.Empty<CharacterId>())
            .Where(value => value.IsValid)
            .Distinct()
            .ToArray();
        if (ids.Length < 3) throw new InvalidOperationException("A joint memorial requires at least three deaths.");
        int[] deathDays = ids.Select(id => grief.TryGetValue(id, out GriefIncidentSaveData incident)
                ? incident.deathAbsoluteDay
                : throw new InvalidOperationException("Joint memorial contains an unknown grief incident."))
            .ToArray();
        if (deathDays.Max() - deathDays.Min() > 10)
            throw new InvalidOperationException("Joint memorial deaths must fall within ten days.");
        foreach (CharacterId id in ids) CompleteFuneral(id, funeralAbsoluteDay, matchingSpeciesRitual);
    }

    public void ApplyLongNightMemorial(int absoluteDay)
    {
        if (absoluteDay < 1)
            throw new ArgumentOutOfRangeException(nameof(absoluteDay));
        CalendarDateTime date = GameCalendarRules.Project(absoluteDay, 0);
        if (date.Season != Season.Winter
            || date.DayOfSeason != GameCalendarRules.DaysPerSeason)
        {
            throw new InvalidOperationException(
                "The long-night memorial can only be held on winter day 30.");
        }
        if (LastLongNightMemorialYear == date.Year)
            throw new InvalidOperationException(
                "The long-night memorial was already applied this year.");

        foreach (GriefIncidentSaveData incident in grief.Values)
        {
            incident.remainingMultiplier *= 0.75f;
        }
        LastLongNightMemorialYear = date.Year;
    }

    public void AdvanceToDay(int currentAbsoluteDay)
    {
        foreach (GriefIncidentSaveData incident in grief.Values)
        {
            bool closeFamily = incident.relationship is GriefRelationshipKind.PartnerOrChild
                or GriefRelationshipKind.ParentSiblingOrGuardian;
            if (closeFamily && !incident.funeralCompleted
                && !incident.missedFuneralTraumaApplied
                && currentAbsoluteDay - incident.deathAbsoluteDay >= 7)
            {
                incident.missedFuneralTraumaApplied = true;
                AddTrauma("trauma:missed-family-funeral", currentAbsoluteDay, 5f);
            }
        }
        CharacterId[] expired = grief
            .Where(pair => currentAbsoluteDay - pair.Value.deathAbsoluteDay
                >= pair.Value.durationDays)
            .Select(pair => pair.Key)
            .ToArray();
        foreach (CharacterId id in expired) grief.Remove(id);
        if (currentAbsoluteDay > MemorialResolveExpiresDay)
            MemorialResolveAmount = 0f;
    }

    public void AddTrauma(string eventType, int absoluteDay, float amount)
    {
        if (string.IsNullOrWhiteSpace(eventType) || absoluteDay < 1 || amount <= 0f)
            throw new ArgumentException("Trauma events require an id, day, and positive amount.");
        Trauma = Math.Clamp(Trauma + amount, 0f, 100f);
        traumaEvents.Add(new TraumaEventSaveData
        {
            eventType = eventType.Trim(),
            absoluteDay = absoluteDay,
            amount = amount
        });
        CompressOldTrauma();
    }

    public void ApplyCounseling() => ReduceTrauma(5f);

    public TraumaThresholdEffect GetThresholdEffects()
    {
        TraumaThresholdEffect effects = TraumaThresholdEffect.None;
        if (Trauma >= 25f) effects |= TraumaThresholdEffect.IntrusiveMemory;
        if (Trauma >= 50f) effects |= TraumaThresholdEffect.WorkEfficiencyPenalty;
        if (Trauma >= 75f) effects |= TraumaThresholdEffect.BreakdownRisk;
        return effects;
    }

    public CharacterPsychosocialRecordSaveData Capture() => new()
    {
        characterId = CharacterId.Value,
        trauma = Trauma,
        memorialResolveAmount = MemorialResolveAmount,
        memorialResolveExpiresDay = MemorialResolveExpiresDay,
        lastLongNightMemorialYear = LastLongNightMemorialYear,
        grief = grief.Values.OrderBy(value => value.deceasedCharacterId, StringComparer.Ordinal).ToList(),
        recentTraumaEvents = traumaEvents.OrderBy(value => value.absoluteDay).ToList(),
        compressedTraumaByType = compressedTrauma.Values
            .OrderBy(value => value.eventType, StringComparer.Ordinal).ToList(),
        festivalAttendance = festivalAttendance
            .OrderBy(value => value.Key, StringComparer.Ordinal)
            .Select(value => new FestivalAttendanceSaveData
            {
                festivalId = value.Key,
                year = value.Value
            }).ToList()
    };

    public static CharacterGriefAggregate Restore(CharacterPsychosocialRecordSaveData data)
    {
        if (data == null) throw new ArgumentNullException(nameof(data));
        CharacterGriefAggregate result = new(new CharacterId(data.characterId))
        {
            Trauma = Math.Clamp(data.trauma, 0f, 100f),
            MemorialResolveAmount = Math.Clamp(data.memorialResolveAmount, 0f, MaximumMemorialResolve),
            MemorialResolveExpiresDay = data.memorialResolveExpiresDay,
            LastLongNightMemorialYear = Math.Max(0, data.lastLongNightMemorialYear)
        };
        foreach (GriefIncidentSaveData incident in data.grief ?? new List<GriefIncidentSaveData>())
        {
            CharacterId deceased = new(incident?.deceasedCharacterId);
            if (incident == null || !deceased.IsValid || !result.grief.TryAdd(deceased, incident))
                throw new InvalidOperationException("Invalid or duplicate grief incident.");
        }
        result.traumaEvents.AddRange(data.recentTraumaEvents ?? new List<TraumaEventSaveData>());
        if (result.traumaEvents.Count > MaximumRecentTraumaEvents)
            throw new InvalidOperationException("Too many uncompressed trauma events.");
        foreach (TraumaEventSaveData summary in data.compressedTraumaByType
                     ?? new List<TraumaEventSaveData>())
        {
            if (summary == null || string.IsNullOrWhiteSpace(summary.eventType)
                || !result.compressedTrauma.TryAdd(summary.eventType, summary))
                throw new InvalidOperationException("Invalid compressed trauma summary.");
        }
        foreach (FestivalAttendanceSaveData attendance in data.festivalAttendance
                     ?? new List<FestivalAttendanceSaveData>())
        {
            string festivalId = attendance?.festivalId?.Trim() ?? string.Empty;
            if (festivalId.Length == 0 || attendance.year < 1
                || !result.festivalAttendance.TryAdd(
                    festivalId,
                    attendance.year))
            {
                throw new InvalidOperationException(
                    "Invalid or duplicate festival attendance.");
            }
        }
        return result;
    }

    private static (float Penalty, int Days) GetGriefRule(GriefRelationshipKind relationship) =>
        relationship switch
        {
            GriefRelationshipKind.PartnerOrChild => (-12f, 60),
            GriefRelationshipKind.ParentSiblingOrGuardian => (-8f, 45),
            GriefRelationshipKind.Household => (-5f, 30),
            GriefRelationshipKind.Colleague => (-3f, 20),
            _ => (-1f, 10)
        };

    private static float RemainingPenalty(GriefIncidentSaveData incident, int day)
    {
        int elapsed = Math.Max(0, day - incident.deathAbsoluteDay);
        if (elapsed >= incident.durationDays) return 0f;
        float timeRemaining = 1f - elapsed / (float)incident.durationDays;
        return incident.initialPenalty * timeRemaining
            * Math.Clamp(incident.remainingMultiplier, 0f, 1f);
    }

    private void ReduceTrauma(float amount) => Trauma = Math.Max(0f, Trauma - amount);

    private void CompressOldTrauma()
    {
        while (traumaEvents.Count > MaximumRecentTraumaEvents)
        {
            TraumaEventSaveData oldest = traumaEvents[0];
            traumaEvents.RemoveAt(0);
            if (!compressedTrauma.TryGetValue(oldest.eventType, out TraumaEventSaveData summary))
            {
                summary = new TraumaEventSaveData { eventType = oldest.eventType };
                compressedTrauma.Add(oldest.eventType, summary);
            }
            summary.amount += oldest.amount;
            summary.absoluteDay = Math.Max(summary.absoluteDay, oldest.absoluteDay);
        }
    }
}
