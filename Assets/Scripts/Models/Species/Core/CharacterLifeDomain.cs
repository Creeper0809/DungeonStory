using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;

public enum AgeConditionSeverity
{
    Mild = 0,
    Moderate = 1,
    Severe = 2,
    Critical = 3,
    OrganFunctionLoss = 4
}

public enum AgingCareMode
{
    Normal = 0,
    RuneHibernation = 1,
    TemporalStasis = 2
}

public static class CharacterLifeNumericRules
{
    private const double DayUnitPrecision = 1_000_000d;

    public static double CanonicalizeDayUnits(double value)
    {
        return Math.Round(
            Math.Max(0d, value) * DayUnitPrecision,
            MidpointRounding.AwayFromZero) / DayUnitPrecision;
    }
}

public readonly struct SpeciesLifeHistoryDefinition
{
    public SpeciesLifeHistoryDefinition(
        CharacterSpeciesId speciesId,
        int infantEndAgeYears,
        int adolescentStartAgeYears,
        int adultAgeYears,
        int elderAgeYears,
        float untreatedExpectedLifeYears,
        bool construct)
    {
        SpeciesId = speciesId;
        InfantEndAgeYears = infantEndAgeYears;
        AdolescentStartAgeYears = adolescentStartAgeYears;
        AdultAgeYears = adultAgeYears;
        ElderAgeYears = elderAgeYears;
        UntreatedExpectedLifeYears = untreatedExpectedLifeYears;
        Construct = construct;
    }

    public CharacterSpeciesId SpeciesId { get; }
    public int InfantEndAgeYears { get; }
    public int AdolescentStartAgeYears { get; }
    public int AdultAgeYears { get; }
    public int ElderAgeYears { get; }
    public float UntreatedExpectedLifeYears { get; }
    public bool Construct { get; }
    public double AdultAgeDayUnits => AdultAgeYears * GameCalendarRules.DaysPerYear;
    public double ElderAgeDayUnits => ElderAgeYears * GameCalendarRules.DaysPerYear;

    public CharacterLifeStage ResolveStage(double biologicalAgeDayUnits)
    {
        double years = Math.Max(0d, biologicalAgeDayUnits)
            / GameCalendarRules.DaysPerYear;
        if (years >= ElderAgeYears) return CharacterLifeStage.Elder;
        if (years >= AdultAgeYears) return CharacterLifeStage.Adult;
        if (AdolescentStartAgeYears >= 0 && years >= AdolescentStartAgeYears)
        {
            return CharacterLifeStage.Adolescent;
        }

        if (InfantEndAgeYears >= 0 && years < InfantEndAgeYears)
        {
            return CharacterLifeStage.Infant;
        }

        return CharacterLifeStage.Child;
    }
}

public readonly struct AgeConditionDefinition
{
    public AgeConditionDefinition(
        string conditionId,
        bool constructCondition,
        IReadOnlyList<string> affectedAnatomyNodeIds)
    {
        ConditionId = conditionId?.Trim() ?? string.Empty;
        ConstructCondition = constructCondition;
        AffectedAnatomyNodeIds = affectedAnatomyNodeIds ?? Array.Empty<string>();
    }

    public string ConditionId { get; }
    public bool ConstructCondition { get; }
    public IReadOnlyList<string> AffectedAnatomyNodeIds { get; }
    public bool IsValid => ConditionId.Length > 0 && AffectedAnatomyNodeIds.Count > 0;
}

public interface ICharacterLifeDefinitionCatalog
{
    SpeciesLifeHistoryDefinition RequireLifeHistory(CharacterSpeciesId speciesId);
    AgeConditionDefinition RequireAgeCondition(string conditionId);
    IReadOnlyList<AgeConditionDefinition> GetAgeConditions(bool construct);
}

[Serializable]
public sealed class CharacterAgeConditionSaveData
{
    public string conditionId = string.Empty;
    public AgeConditionSeverity severity;
    public double onsetBiologicalAgeDayUnits;
    public double nextProgressBiologicalAgeDayUnits;
}

[Serializable]
public sealed class CharacterLifeRecordSaveData
{
    public string characterId = string.Empty;
    public string phenotypeSpeciesId = string.Empty;
    public int chronologicalAgeDays;
    public double biologicalAgeDayUnits;
    public int birthdayDayOfYear = 1;
    public CharacterLifeStage lifeStage;
    public int lastBloodRejuvenationAbsoluteDay = int.MinValue;
    public bool geriatricMedicineActive;
    public bool chronicCareActive;
    public AgingCareMode requestedAgingCareMode;
    public AgingCareMode effectiveAgingCareMode;
    public string temporalStasisFacilityId = string.Empty;
    public int temporalStasisNextMaintenanceAbsoluteDay = int.MaxValue;
    public List<CharacterAgeConditionSaveData> ageConditions = new();
}

[Serializable]
public sealed class CharacterLifeWorldSaveData
{
    public const int CurrentVersion = 2;
    public int version = CurrentVersion;
    public List<CharacterLifeRecordSaveData> characters = new();
}

public readonly struct AgeConditionChange
{
    public AgeConditionChange(
        CharacterId characterId,
        string conditionId,
        AgeConditionSeverity previous,
        AgeConditionSeverity current,
        bool newlyDiagnosed,
        bool resolved = false)
    {
        CharacterId = characterId;
        ConditionId = conditionId ?? string.Empty;
        Previous = previous;
        Current = current;
        NewlyDiagnosed = newlyDiagnosed;
        Resolved = resolved;
    }

    public CharacterId CharacterId { get; }
    public string ConditionId { get; }
    public AgeConditionSeverity Previous { get; }
    public AgeConditionSeverity Current { get; }
    public bool NewlyDiagnosed { get; }
    public bool Resolved { get; }
    public bool CausesOrganFunctionLoss =>
        Current == AgeConditionSeverity.OrganFunctionLoss;
}

public sealed class CharacterAgeConditionState
{
    public CharacterAgeConditionState(
        string conditionId,
        AgeConditionSeverity severity,
        double onsetBiologicalAgeDayUnits,
        double nextProgressBiologicalAgeDayUnits)
    {
        ConditionId = conditionId?.Trim() ?? string.Empty;
        Severity = severity;
        OnsetBiologicalAgeDayUnits =
            CharacterLifeNumericRules.CanonicalizeDayUnits(
                onsetBiologicalAgeDayUnits);
        NextProgressBiologicalAgeDayUnits =
            CharacterLifeNumericRules.CanonicalizeDayUnits(
                nextProgressBiologicalAgeDayUnits);
    }

    public string ConditionId { get; }
    public AgeConditionSeverity Severity { get; private set; }
    public double OnsetBiologicalAgeDayUnits { get; }
    public double NextProgressBiologicalAgeDayUnits { get; private set; }

    public AgeConditionChange Progress(CharacterId characterId)
    {
        AgeConditionSeverity previous = Severity;
        if (Severity < AgeConditionSeverity.OrganFunctionLoss)
        {
            Severity++;
        }

        NextProgressBiologicalAgeDayUnits =
            CharacterLifeNumericRules.CanonicalizeDayUnits(
                NextProgressBiologicalAgeDayUnits
                + GameCalendarRules.DaysPerYear);
        return new AgeConditionChange(
            characterId,
            ConditionId,
            previous,
            Severity,
            newlyDiagnosed: false);
    }

    public AgeConditionChange Reduce(
        CharacterId characterId,
        int severityLevels)
    {
        if (severityLevels <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(severityLevels));
        }

        AgeConditionSeverity previous = Severity;
        Severity = (AgeConditionSeverity)Math.Max(
            (int)AgeConditionSeverity.Mild,
            (int)Severity - severityLevels);
        NextProgressBiologicalAgeDayUnits =
            CharacterLifeNumericRules.CanonicalizeDayUnits(Math.Max(
                NextProgressBiologicalAgeDayUnits,
                OnsetBiologicalAgeDayUnits + GameCalendarRules.DaysPerYear));
        return new AgeConditionChange(
            characterId,
            ConditionId,
            previous,
            Severity,
            newlyDiagnosed: false);
    }

    public CharacterAgeConditionSaveData Capture() => new()
    {
        conditionId = ConditionId,
        severity = Severity,
        onsetBiologicalAgeDayUnits = OnsetBiologicalAgeDayUnits,
        nextProgressBiologicalAgeDayUnits = NextProgressBiologicalAgeDayUnits
    };
}

public sealed class CharacterLifeRecord
{
    private readonly Dictionary<string, CharacterAgeConditionState> ageConditions =
        new(StringComparer.Ordinal);

    public CharacterLifeRecord(
        CharacterId characterId,
        CharacterSpeciesId phenotypeSpeciesId,
        int chronologicalAgeDays,
        double biologicalAgeDayUnits,
        int birthdayDayOfYear,
        SpeciesLifeHistoryDefinition lifeHistory,
        int lastBloodRejuvenationAbsoluteDay = int.MinValue,
        bool geriatricMedicineActive = false,
        bool chronicCareActive = false,
        AgingCareMode requestedAgingCareMode = AgingCareMode.Normal,
        AgingCareMode effectiveAgingCareMode = AgingCareMode.Normal,
        string temporalStasisFacilityId = "",
        int temporalStasisNextMaintenanceAbsoluteDay = int.MaxValue)
    {
        if (!characterId.IsValid) throw new ArgumentException("A valid character id is required.", nameof(characterId));
        if (!phenotypeSpeciesId.IsValid) throw new ArgumentException("A valid species id is required.", nameof(phenotypeSpeciesId));
        if (!lifeHistory.SpeciesId.Equals(phenotypeSpeciesId)) throw new ArgumentException("Life history does not match phenotype species.", nameof(lifeHistory));
        CharacterId = characterId;
        PhenotypeSpeciesId = phenotypeSpeciesId;
        ChronologicalAgeDays = Math.Max(0, chronologicalAgeDays);
        BiologicalAgeDayUnits =
            CharacterLifeNumericRules.CanonicalizeDayUnits(
                biologicalAgeDayUnits);
        BirthdayDayOfYear = Math.Clamp(birthdayDayOfYear, 1, GameCalendarRules.DaysPerYear);
        LastBloodRejuvenationAbsoluteDay = lastBloodRejuvenationAbsoluteDay;
        GeriatricMedicineActive = geriatricMedicineActive;
        ChronicCareActive = chronicCareActive;
        RequestedAgingCareMode = requestedAgingCareMode;
        EffectiveAgingCareMode = effectiveAgingCareMode;
        TemporalStasisFacilityId = temporalStasisFacilityId?.Trim()
            ?? string.Empty;
        TemporalStasisNextMaintenanceAbsoluteDay =
            temporalStasisNextMaintenanceAbsoluteDay;
        LifeStage = lifeHistory.ResolveStage(BiologicalAgeDayUnits);
    }

    public CharacterId CharacterId { get; }
    public CharacterSpeciesId PhenotypeSpeciesId { get; }
    public int ChronologicalAgeDays { get; private set; }
    public double BiologicalAgeDayUnits { get; private set; }
    public int BirthdayDayOfYear { get; }
    public CharacterLifeStage LifeStage { get; private set; }
    public int LastBloodRejuvenationAbsoluteDay { get; private set; }
    public bool GeriatricMedicineActive { get; private set; }
    public bool ChronicCareActive { get; private set; }
    public AgingCareMode RequestedAgingCareMode { get; private set; }
    public AgingCareMode EffectiveAgingCareMode { get; private set; }
    public string TemporalStasisFacilityId { get; private set; }
    public int TemporalStasisNextMaintenanceAbsoluteDay { get; private set; }
    public IReadOnlyCollection<CharacterAgeConditionState> AgeConditions =>
        ageConditions.Values;

    public IReadOnlyList<AgeConditionChange> AddInitialAgeConditions(
        IReadOnlyList<string> conditionIds,
        IReadOnlyList<AgeConditionDefinition> availableConditions)
    {
        Dictionary<string, AgeConditionDefinition> available =
            (availableConditions ?? Array.Empty<AgeConditionDefinition>())
            .Where(value => value.IsValid)
            .ToDictionary(value => value.ConditionId, StringComparer.Ordinal);
        List<AgeConditionChange> changes = new();
        foreach (string conditionId in (conditionIds ?? Array.Empty<string>())
                     .Where(value => !string.IsNullOrWhiteSpace(value))
                     .Select(value => value.Trim())
                     .Distinct(StringComparer.Ordinal)
                     .OrderBy(value => value, StringComparer.Ordinal))
        {
            if (!available.TryGetValue(
                    conditionId,
                    out _)
                || ageConditions.ContainsKey(conditionId))
            {
                throw new InvalidOperationException(
                    $"Initial age condition '{conditionId}' is invalid or duplicated.");
            }

            double onset = BiologicalAgeDayUnits;
            ageConditions.Add(
                conditionId,
                new CharacterAgeConditionState(
                    conditionId,
                    AgeConditionSeverity.Mild,
                    onset,
                    onset + GameCalendarRules.DaysPerYear));
            changes.Add(new AgeConditionChange(
                CharacterId,
                conditionId,
                AgeConditionSeverity.Mild,
                AgeConditionSeverity.Mild,
                newlyDiagnosed: true));
        }
        return changes;
    }

    public IReadOnlyList<AgeConditionChange> AdvanceOneChronologicalDay(
        SpeciesLifeHistoryDefinition lifeHistory,
        IReadOnlyList<AgeConditionDefinition> availableConditions,
        Func<double> nextUnitRandom,
        double agingMultiplier = 1d,
        bool preventNewAgeConditions = false,
        double conditionProgressMultiplier = 1d)
    {
        if (!lifeHistory.SpeciesId.Equals(PhenotypeSpeciesId))
        {
            throw new ArgumentException("Life history does not match the character.", nameof(lifeHistory));
        }

        if (nextUnitRandom == null) throw new ArgumentNullException(nameof(nextUnitRandom));
        if (agingMultiplier < 0d) throw new ArgumentOutOfRangeException(nameof(agingMultiplier));
        if (conditionProgressMultiplier < 0d) throw new ArgumentOutOfRangeException(nameof(conditionProgressMultiplier));

        ChronologicalAgeDays++;
        double previousAge = BiologicalAgeDayUnits;
        bool adultOrOlder = LifeStage >= CharacterLifeStage.Adult;
        double dailyRate = adultOrOlder ? 6d : 4d;
        BiologicalAgeDayUnits =
            CharacterLifeNumericRules.CanonicalizeDayUnits(
                BiologicalAgeDayUnits + dailyRate * agingMultiplier);
        LifeStage = lifeHistory.ResolveStage(BiologicalAgeDayUnits);

        List<AgeConditionChange> changes = ProgressExistingConditions(
            previousAge,
            BiologicalAgeDayUnits,
            conditionProgressMultiplier);
        if (preventNewAgeConditions)
        {
            return changes;
        }

        int firstBirthday = (int)Math.Floor(previousAge / GameCalendarRules.DaysPerYear) + 1;
        int lastBirthday = (int)Math.Floor(BiologicalAgeDayUnits / GameCalendarRules.DaysPerYear);
        for (int birthdayYear = firstBirthday; birthdayYear <= lastBirthday; birthdayYear++)
        {
            if (birthdayYear < lifeHistory.ElderAgeYears)
            {
                continue;
            }

            double probability = CalculateAgeConditionProbability(
                birthdayYear,
                lifeHistory.ElderAgeYears);
            if (ClampUnit(nextUnitRandom()) >= probability)
            {
                continue;
            }

            AgeConditionDefinition[] eligible = (availableConditions
                    ?? Array.Empty<AgeConditionDefinition>())
                .Where(value => value.IsValid
                    && value.ConstructCondition == lifeHistory.Construct
                    && !ageConditions.ContainsKey(value.ConditionId))
                .OrderBy(value => value.ConditionId, StringComparer.Ordinal)
                .ToArray();
            if (eligible.Length == 0)
            {
                continue;
            }

            int selected = Math.Min(
                eligible.Length - 1,
                (int)Math.Floor(ClampUnit(nextUnitRandom()) * eligible.Length));
            AgeConditionDefinition definition = eligible[selected];
            double onset = birthdayYear * GameCalendarRules.DaysPerYear;
            CharacterAgeConditionState state = new(
                definition.ConditionId,
                AgeConditionSeverity.Mild,
                onset,
                onset + GameCalendarRules.DaysPerYear);
            ageConditions.Add(definition.ConditionId, state);
            changes.Add(new AgeConditionChange(
                CharacterId,
                definition.ConditionId,
                AgeConditionSeverity.Mild,
                AgeConditionSeverity.Mild,
                newlyDiagnosed: true));
        }

        return changes;
    }

    public IReadOnlyList<AgeConditionChange> AdvanceOneChronologicalDayWithCare(
        SpeciesLifeHistoryDefinition lifeHistory,
        IReadOnlyList<AgeConditionDefinition> availableConditions,
        Func<double> nextUnitRandom,
        double hereditaryAgingMultiplier = 1d)
    {
        double agingMultiplier = EffectiveAgingCareMode switch
        {
            AgingCareMode.RuneHibernation => 0.25d,
            AgingCareMode.TemporalStasis => 0d,
            _ => 1d
        };
        agingMultiplier *= Math.Clamp(hereditaryAgingMultiplier, 0.5d, 1.5d);
        double conditionProgressMultiplier = ChronicCareActive
            ? 0d
            : GeriatricMedicineActive ? 0.70d : 1d;
        return AdvanceOneChronologicalDay(
            lifeHistory,
            availableConditions,
            nextUnitRandom,
            agingMultiplier,
            preventNewAgeConditions:
                EffectiveAgingCareMode == AgingCareMode.TemporalStasis,
            conditionProgressMultiplier);
    }

    public void ConfigureLongTermCare(
        bool geriatricMedicineActive,
        bool chronicCareActive,
        AgingCareMode requestedMode)
    {
        if (!Enum.IsDefined(typeof(AgingCareMode), requestedMode))
        {
            throw new ArgumentOutOfRangeException(nameof(requestedMode));
        }

        GeriatricMedicineActive = geriatricMedicineActive;
        ChronicCareActive = chronicCareActive;
        RequestedAgingCareMode = requestedMode;
        if (requestedMode != AgingCareMode.TemporalStasis)
        {
            EffectiveAgingCareMode = requestedMode;
            TemporalStasisFacilityId = string.Empty;
            TemporalStasisNextMaintenanceAbsoluteDay = int.MaxValue;
        }
    }

    public void ConfigureTemporalStasis(
        string facilityId,
        bool operational,
        int nextMaintenanceAbsoluteDay)
    {
        string requiredFacilityId = facilityId?.Trim() ?? string.Empty;
        if (requiredFacilityId.Length == 0)
        {
            throw new ArgumentException(
                "Temporal stasis requires a facility ID.",
                nameof(facilityId));
        }

        RequestedAgingCareMode = AgingCareMode.TemporalStasis;
        EffectiveAgingCareMode = operational
            ? AgingCareMode.TemporalStasis
            : AgingCareMode.Normal;
        TemporalStasisFacilityId = requiredFacilityId;
        TemporalStasisNextMaintenanceAbsoluteDay =
            nextMaintenanceAbsoluteDay;
    }

    public void ReduceBiologicalAgeYears(
        SpeciesLifeHistoryDefinition lifeHistory,
        int years,
        int minimumYears)
    {
        if (years <= 0) throw new ArgumentOutOfRangeException(nameof(years));
        double minimum = Math.Max(0, minimumYears) * GameCalendarRules.DaysPerYear;
        BiologicalAgeDayUnits =
            CharacterLifeNumericRules.CanonicalizeDayUnits(Math.Max(
                minimum,
                BiologicalAgeDayUnits - years * GameCalendarRules.DaysPerYear));
        LifeStage = lifeHistory.ResolveStage(BiologicalAgeDayUnits);
    }

    public IReadOnlyList<AgeConditionChange> ApplyWholeBodyRegeneration(
        SpeciesLifeHistoryDefinition lifeHistory)
    {
        ReduceBiologicalAgeYears(
            lifeHistory,
            years: 30,
            minimumYears: lifeHistory.AdultAgeYears);

        List<AgeConditionChange> changes = new();
        string[] removable = ageConditions.Values
            .Where(value => value.Severity is AgeConditionSeverity.Mild
                or AgeConditionSeverity.Moderate)
            .Select(value => value.ConditionId)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        foreach (string conditionId in removable)
        {
            CharacterAgeConditionState condition = ageConditions[conditionId];
            changes.Add(new AgeConditionChange(
                CharacterId,
                conditionId,
                condition.Severity,
                AgeConditionSeverity.Mild,
                newlyDiagnosed: false,
                resolved: true));
            ageConditions.Remove(conditionId);
        }

        foreach (CharacterAgeConditionState condition in ageConditions.Values
                     .Where(value => value.Severity == AgeConditionSeverity.Severe)
                     .OrderBy(value => value.ConditionId, StringComparer.Ordinal))
        {
            changes.Add(condition.Reduce(CharacterId, severityLevels: 2));
        }

        return changes;
    }

    public IReadOnlyList<AgeConditionChange> ReduceAgeConditions(int severityLevels)
    {
        if (severityLevels <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(severityLevels));
        }

        List<AgeConditionChange> changes = new();
        string[] ids = ageConditions.Keys
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        foreach (string conditionId in ids)
        {
            CharacterAgeConditionState condition = ageConditions[conditionId];
            AgeConditionChange change = condition.Reduce(
                CharacterId,
                severityLevels);
            changes.Add(change);
            if (change.Resolved)
            {
                ageConditions.Remove(conditionId);
            }
        }

        return changes;
    }

    public bool TryApplyBloodRejuvenation(
        SpeciesLifeHistoryDefinition lifeHistory,
        int absoluteDay,
        out DomainFailure failure)
    {
        int minimumAgeYears = lifeHistory.AdultAgeYears + 5;
        if (BiologicalAgeDayUnits
            <= minimumAgeYears * GameCalendarRules.DaysPerYear)
        {
            failure = new DomainFailure(
                FailureCode.AgeTreatmentTooYoung,
                CharacterId.Value,
                minimumAgeYears.ToString());
            return false;
        }

        long elapsed = (long)absoluteDay - LastBloodRejuvenationAbsoluteDay;
        if (LastBloodRejuvenationAbsoluteDay != int.MinValue
            && elapsed < GameCalendarRules.DaysPerYear)
        {
            failure = new DomainFailure(
                FailureCode.AgeTreatmentCooldownActive,
                CharacterId.Value,
                Math.Max(0L, GameCalendarRules.DaysPerYear - elapsed).ToString());
            return false;
        }

        ReduceBiologicalAgeYears(
            lifeHistory,
            years: 10,
            minimumYears: minimumAgeYears);
        LastBloodRejuvenationAbsoluteDay = absoluteDay;
        failure = DomainFailure.None;
        return true;
    }

    public CharacterLifeRecordSaveData Capture() => new()
    {
        characterId = CharacterId.Value,
        phenotypeSpeciesId = PhenotypeSpeciesId.Value,
        chronologicalAgeDays = ChronologicalAgeDays,
        biologicalAgeDayUnits = BiologicalAgeDayUnits,
        birthdayDayOfYear = BirthdayDayOfYear,
        lifeStage = LifeStage,
        lastBloodRejuvenationAbsoluteDay = LastBloodRejuvenationAbsoluteDay,
        geriatricMedicineActive = GeriatricMedicineActive,
        chronicCareActive = ChronicCareActive,
        requestedAgingCareMode = RequestedAgingCareMode,
        effectiveAgingCareMode = EffectiveAgingCareMode,
        temporalStasisFacilityId = TemporalStasisFacilityId,
        temporalStasisNextMaintenanceAbsoluteDay =
            TemporalStasisNextMaintenanceAbsoluteDay,
        ageConditions = ageConditions.Values
            .OrderBy(value => value.ConditionId, StringComparer.Ordinal)
            .Select(value => value.Capture())
            .ToList()
    };

    public static CharacterLifeRecord Restore(
        CharacterLifeRecordSaveData data,
        SpeciesLifeHistoryDefinition lifeHistory)
    {
        if (data == null) throw new ArgumentNullException(nameof(data));
        CharacterLifeRecord record = new(
            new CharacterId(data.characterId),
            new CharacterSpeciesId(data.phenotypeSpeciesId),
            data.chronologicalAgeDays,
            data.biologicalAgeDayUnits,
            data.birthdayDayOfYear,
            lifeHistory,
            data.lastBloodRejuvenationAbsoluteDay,
            data.geriatricMedicineActive,
            data.chronicCareActive,
            data.requestedAgingCareMode,
            data.effectiveAgingCareMode,
            data.temporalStasisFacilityId,
            data.temporalStasisNextMaintenanceAbsoluteDay);
        if (record.LifeStage != data.lifeStage)
        {
            throw new InvalidOperationException(
                $"Saved life stage for '{data.characterId}' does not match biological age.");
        }

        foreach (CharacterAgeConditionSaveData condition in data.ageConditions
                     ?? new List<CharacterAgeConditionSaveData>())
        {
            if (condition == null || string.IsNullOrWhiteSpace(condition.conditionId)
                || !record.ageConditions.TryAdd(
                    condition.conditionId,
                    new CharacterAgeConditionState(
                        condition.conditionId,
                        condition.severity,
                        condition.onsetBiologicalAgeDayUnits,
                        condition.nextProgressBiologicalAgeDayUnits)))
            {
                throw new InvalidOperationException(
                    $"Character '{data.characterId}' has invalid age-condition state.");
            }
        }

        return record;
    }

    public static double CalculateAgeConditionProbability(
        double biologicalAgeYears,
        int elderAgeYears)
    {
        double exponent = (biologicalAgeYears - elderAgeYears) / 8d;
        return Math.Min(0.65d, 0.005d * Math.Pow(2d, exponent));
    }

    private List<AgeConditionChange> ProgressExistingConditions(
        double previousBiologicalAge,
        double currentBiologicalAge,
        double progressMultiplier)
    {
        List<AgeConditionChange> changes = new();
        double effectiveProgress = Math.Max(0d, currentBiologicalAge - previousBiologicalAge)
            * progressMultiplier;
        if (effectiveProgress <= 0d)
        {
            return changes;
        }

        double progressEnd = previousBiologicalAge + effectiveProgress;
        foreach (CharacterAgeConditionState condition in ageConditions.Values
                     .OrderBy(value => value.ConditionId, StringComparer.Ordinal))
        {
            while (condition.NextProgressBiologicalAgeDayUnits <= progressEnd
                   && condition.Severity < AgeConditionSeverity.OrganFunctionLoss)
            {
                changes.Add(condition.Progress(CharacterId));
            }
        }

        return changes;
    }

    private static double ClampUnit(double value) =>
        Math.Max(0d, Math.Min(0.999999999999d, value));
}

public interface ICharacterLifeQuery
{
    int Version { get; }
    IReadOnlyCollection<CharacterLifeRecord> Records { get; }
    bool TryGet(CharacterId characterId, out CharacterLifeRecord record);
}

public interface ICharacterLifeCommand
{
    CharacterLifeRecord Register(
        CharacterId characterId,
        CharacterSpeciesId phenotypeSpeciesId,
        int chronologicalAgeDays,
        double biologicalAgeDayUnits,
        int birthdayDayOfYear);
    IReadOnlyList<AgeConditionChange> AdvanceDay(CharacterId characterId);
    IReadOnlyList<AgeConditionChange> AddInitialAgeConditions(
        CharacterId characterId,
        IReadOnlyList<string> conditionIds);
    void ApplyRejuvenation(CharacterId characterId, int biologicalYears);
    bool TryApplyBloodRejuvenation(
        CharacterId characterId,
        int absoluteDay,
        out DomainFailure failure);
    IReadOnlyList<AgeConditionChange> ApplyWholeBodyRegeneration(
        CharacterId characterId);
    IReadOnlyList<AgeConditionChange> ReduceAgeConditions(
        CharacterId characterId,
        int severityLevels);
    void ConfigureLongTermCare(
        CharacterId characterId,
        bool geriatricMedicineActive,
        bool chronicCareActive,
        AgingCareMode requestedMode);
    void ConfigureTemporalStasis(
        CharacterId characterId,
        string facilityId,
        bool operational,
        int nextMaintenanceAbsoluteDay);
}

public interface ICharacterLifePersistence
{
    CharacterLifeWorldSaveData Capture();
    CharacterLifeRestoreCandidate PrepareRestore(CharacterLifeWorldSaveData data);
    void PublishRestore(CharacterLifeRestoreCandidate candidate);
}

public sealed class CharacterLifeRestoreCandidate
{
    internal CharacterLifeRestoreCandidate(CharacterLifeAggregateState state)
    {
        State = state ?? throw new ArgumentNullException(nameof(state));
    }

    internal CharacterLifeAggregateState State { get; }
}

internal sealed class CharacterLifeAggregateState
{
    internal Dictionary<CharacterId, CharacterLifeRecord> Characters { get; } = new();

    internal CharacterLifeAggregateState DeepClone(
        ICharacterLifeDefinitionCatalog definitions)
    {
        CharacterLifeAggregateState clone = new();
        foreach (KeyValuePair<CharacterId, CharacterLifeRecord> pair in Characters)
        {
            CharacterLifeRecordSaveData data = pair.Value.Capture();
            SpeciesLifeHistoryDefinition history = definitions.RequireLifeHistory(
                new CharacterSpeciesId(data.phenotypeSpeciesId));
            clone.Characters.Add(pair.Key, CharacterLifeRecord.Restore(data, history));
        }

        return clone;
    }
}

public sealed class CharacterLifeRuntime :
    ICharacterLifeQuery,
    ICharacterLifeCommand,
    ICharacterLifePersistence
{
    private const string AgingRandomStreamId = "population:aging";
    private readonly DungeonRuntimeAggregateRootStore rootStore;
    private readonly ICharacterLifeDefinitionCatalog definitions;
    private readonly IRandomStream agingRandom;
    private int version = 1;

    public CharacterLifeRuntime(
        DungeonRuntimeAggregateRootStore rootStore,
        ICharacterLifeDefinitionCatalog definitions,
        IRandomStreamProvider randomStreams)
    {
        this.rootStore = rootStore ?? throw new ArgumentNullException(nameof(rootStore));
        this.definitions = definitions ?? throw new ArgumentNullException(nameof(definitions));
        agingRandom = (randomStreams ?? throw new ArgumentNullException(nameof(randomStreams)))
            .Get(AgingRandomStreamId);
    }

    public int Version => version;
    public IReadOnlyCollection<CharacterLifeRecord> Records =>
        Current.Characters.Values;

    public bool TryGet(CharacterId characterId, out CharacterLifeRecord record) =>
        Current.Characters.TryGetValue(characterId, out record);

    public CharacterLifeRecord Register(
        CharacterId characterId,
        CharacterSpeciesId phenotypeSpeciesId,
        int chronologicalAgeDays,
        double biologicalAgeDayUnits,
        int birthdayDayOfYear)
    {
        CharacterLifeAggregateState state = Writable;
        if (state.Characters.ContainsKey(characterId))
        {
            throw new InvalidOperationException(
                $"Character life state '{characterId.Value}' is already registered.");
        }

        CharacterLifeRecord record = new(
            characterId,
            phenotypeSpeciesId,
            chronologicalAgeDays,
            biologicalAgeDayUnits,
            birthdayDayOfYear,
            definitions.RequireLifeHistory(phenotypeSpeciesId));
        state.Characters.Add(characterId, record);
        version = unchecked(version + 1);
        return record;
    }

    public IReadOnlyList<AgeConditionChange> AdvanceDay(CharacterId characterId) =>
        AdvanceDay(characterId, 1d);

    public IReadOnlyList<AgeConditionChange> AddInitialAgeConditions(
        CharacterId characterId,
        IReadOnlyList<string> conditionIds)
    {
        CharacterLifeRecord record = RequireWritableRecord(characterId);
        SpeciesLifeHistoryDefinition history =
            definitions.RequireLifeHistory(record.PhenotypeSpeciesId);
        IReadOnlyList<AgeConditionChange> changes = record.AddInitialAgeConditions(
            conditionIds,
            definitions.GetAgeConditions(history.Construct));
        if (changes.Count > 0)
            version = unchecked(version + 1);
        return changes;
    }

    public IReadOnlyList<AgeConditionChange> AdvanceDay(
        CharacterId characterId,
        double hereditaryAgingMultiplier)
    {
        if (!Writable.Characters.TryGetValue(characterId, out CharacterLifeRecord record))
        {
            throw new KeyNotFoundException(
                $"Character life state '{characterId.Value}' is not registered.");
        }

        SpeciesLifeHistoryDefinition history =
            definitions.RequireLifeHistory(record.PhenotypeSpeciesId);
        IReadOnlyList<AgeConditionChange> changes = record.AdvanceOneChronologicalDayWithCare(
            history,
            definitions.GetAgeConditions(history.Construct),
            () => agingRandom.NextFloat(),
            hereditaryAgingMultiplier);
        version = unchecked(version + 1);
        return changes;
    }

    public IReadOnlyList<AgeConditionChange> AdvanceAllOneDay()
    {
        List<AgeConditionChange> changes = new();
        CharacterId[] ids = Current.Characters.Keys
            .OrderBy(value => value.Value, StringComparer.Ordinal)
            .ToArray();
        foreach (CharacterId id in ids)
        {
            changes.AddRange(AdvanceDay(id));
        }

        return changes;
    }

    public void ApplyRejuvenation(CharacterId characterId, int biologicalYears)
    {
        if (!Writable.Characters.TryGetValue(characterId, out CharacterLifeRecord record))
        {
            throw new KeyNotFoundException(
                $"Character life state '{characterId.Value}' is not registered.");
        }

        SpeciesLifeHistoryDefinition history =
            definitions.RequireLifeHistory(record.PhenotypeSpeciesId);
        record.ReduceBiologicalAgeYears(
            history,
            biologicalYears,
            history.AdultAgeYears + 5);
        version = unchecked(version + 1);
    }

    public IReadOnlyList<AgeConditionChange> ApplyWholeBodyRegeneration(
        CharacterId characterId)
    {
        if (!Writable.Characters.TryGetValue(
                characterId,
                out CharacterLifeRecord record))
        {
            throw new KeyNotFoundException(
                $"Character life state '{characterId.Value}' is not registered.");
        }

        SpeciesLifeHistoryDefinition history =
            definitions.RequireLifeHistory(record.PhenotypeSpeciesId);
        IReadOnlyList<AgeConditionChange> changes =
            record.ApplyWholeBodyRegeneration(history);
        version = unchecked(version + 1);
        return changes;
    }

    public IReadOnlyList<AgeConditionChange> ReduceAgeConditions(
        CharacterId characterId,
        int severityLevels)
    {
        CharacterLifeRecord record = RequireWritableRecord(characterId);
        IReadOnlyList<AgeConditionChange> changes =
            record.ReduceAgeConditions(severityLevels);
        version = unchecked(version + 1);
        return changes;
    }

    public bool TryApplyBloodRejuvenation(
        CharacterId characterId,
        int absoluteDay,
        out DomainFailure failure)
    {
        if (!Writable.Characters.TryGetValue(
                characterId,
                out CharacterLifeRecord record))
        {
            failure = new DomainFailure(
                FailureCode.AgeTreatmentCharacterMissing,
                characterId.Value);
            return false;
        }

        SpeciesLifeHistoryDefinition history =
            definitions.RequireLifeHistory(record.PhenotypeSpeciesId);
        if (!record.TryApplyBloodRejuvenation(
                history,
                absoluteDay,
                out failure))
        {
            return false;
        }

        version = unchecked(version + 1);
        return true;
    }

    public void ConfigureLongTermCare(
        CharacterId characterId,
        bool geriatricMedicineActive,
        bool chronicCareActive,
        AgingCareMode requestedMode)
    {
        CharacterLifeRecord record = RequireWritableRecord(characterId);
        record.ConfigureLongTermCare(
            geriatricMedicineActive,
            chronicCareActive,
            requestedMode);
        version = unchecked(version + 1);
    }

    public void ConfigureTemporalStasis(
        CharacterId characterId,
        string facilityId,
        bool operational,
        int nextMaintenanceAbsoluteDay)
    {
        CharacterLifeRecord record = RequireWritableRecord(characterId);
        record.ConfigureTemporalStasis(
            facilityId,
            operational,
            nextMaintenanceAbsoluteDay);
        version = unchecked(version + 1);
    }

    private CharacterLifeRecord RequireWritableRecord(CharacterId characterId)
    {
        return Writable.Characters.TryGetValue(
            characterId,
            out CharacterLifeRecord record)
            ? record
            : throw new KeyNotFoundException(
                $"Character life state '{characterId.Value}' is not registered.");
    }

    public CharacterLifeWorldSaveData Capture() => new()
    {
        characters = Current.Characters.Values
            .OrderBy(value => value.CharacterId.Value, StringComparer.Ordinal)
            .Select(value => value.Capture())
            .ToList()
    };

    public CharacterLifeRestoreCandidate PrepareRestore(CharacterLifeWorldSaveData data)
    {
        if (data == null || data.version != CharacterLifeWorldSaveData.CurrentVersion
            || data.characters == null)
        {
            throw new InvalidOperationException(
                "Character-life payload is missing or has an unsupported version.");
        }

        CharacterLifeAggregateState restored = new();
        string previousId = string.Empty;
        foreach (CharacterLifeRecordSaveData source in data.characters)
        {
            if (source == null
                || previousId.Length > 0
                    && string.CompareOrdinal(previousId, source.characterId) >= 0)
            {
                throw new InvalidOperationException(
                    "Character-life records must be non-null and sorted by canonical id.");
            }

            CharacterSpeciesId speciesId = new(source.phenotypeSpeciesId);
            CharacterLifeRecord record = CharacterLifeRecord.Restore(
                source,
                definitions.RequireLifeHistory(speciesId));
            if (!restored.Characters.TryAdd(record.CharacterId, record))
            {
                throw new InvalidOperationException(
                    $"Duplicate character-life record '{record.CharacterId.Value}'.");
            }

            previousId = source.characterId;
        }

        return new CharacterLifeRestoreCandidate(restored);
    }

    public void PublishRestore(CharacterLifeRestoreCandidate candidate)
    {
        rootStore.Replace((candidate ?? throw new ArgumentNullException(nameof(candidate))).State);
        version = unchecked(version + 1);
    }

    private CharacterLifeAggregateState Current =>
        rootStore.GetOrCreate(() => new CharacterLifeAggregateState());

    private CharacterLifeAggregateState Writable =>
        rootStore.GetOrCreateWritable(
            () => new CharacterLifeAggregateState(),
            state => state.DeepClone(definitions));
}
