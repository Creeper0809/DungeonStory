using System;
using System.Collections.Generic;
using System.Linq;

[Flags]
public enum DiseaseTransmissionRoute
{
    None = 0,
    Air = 1 << 0,
    Droplet = 1 << 1,
    Blood = 1 << 2,
    Food = 1 << 3,
    Water = 1 << 4,
    ManaExposure = 1 << 5,
    Contact = 1 << 6,
    Environment = 1 << 7
}

public enum DiseaseTargetSystem
{
    Core = 0,
    Consciousness = 1,
    Breathing = 2,
    Digestion = 3,
    Filtration = 4
}

public readonly struct DiseaseDefinition
{
    public DiseaseDefinition(
        string id,
        string displayName,
        DiseaseTransmissionRoute routes,
        int incubationDays,
        int contagiousDays,
        float baseInfectionProbability,
        float baseSeverity,
        DiseaseTargetSystem targetSystem,
        bool vaccineAllowed,
        bool chronic = false)
    {
        Id = id?.Trim() ?? string.Empty;
        DisplayName = displayName?.Trim() ?? string.Empty;
        Routes = routes;
        IncubationDays = Math.Max(0, incubationDays);
        ContagiousDays = Math.Max(0, contagiousDays);
        BaseInfectionProbability = Math.Clamp(baseInfectionProbability, 0f, 1f);
        BaseSeverity = Math.Clamp(baseSeverity, 0f, 100f);
        TargetSystem = targetSystem;
        VaccineAllowed = vaccineAllowed;
        Chronic = chronic;
    }

    public string Id { get; }
    public string DisplayName { get; }
    public DiseaseTransmissionRoute Routes { get; }
    public int IncubationDays { get; }
    public int ContagiousDays { get; }
    public float BaseInfectionProbability { get; }
    public float BaseSeverity { get; }
    public DiseaseTargetSystem TargetSystem { get; }
    public bool VaccineAllowed { get; }
    public bool Chronic { get; }
    public bool Contagious => ContagiousDays > 0
        && (Routes & ~DiseaseTransmissionRoute.Environment) != 0;
    public bool IsValid => Id.Length > 0
        && DisplayName.Length > 0
        && Routes != DiseaseTransmissionRoute.None
        && BaseSeverity > 0f
        && (!Chronic || !Contagious)
        && (!Contagious || BaseInfectionProbability > 0f);
}

public readonly struct PopulationDiseaseRouteExposureEvent
{
    public PopulationDiseaseRouteExposureEvent(
        CharacterId characterId,
        string diseaseId,
        DiseaseTransmissionRoute route,
        float exposureHours,
        float environmentCoefficient)
    {
        CharacterId = characterId;
        DiseaseId = diseaseId?.Trim() ?? string.Empty;
        Route = route;
        ExposureHours = Math.Max(0f, exposureHours);
        EnvironmentCoefficient = Math.Max(0f, environmentCoefficient);
    }

    public CharacterId CharacterId { get; }
    public string DiseaseId { get; }
    public DiseaseTransmissionRoute Route { get; }
    public float ExposureHours { get; }
    public float EnvironmentCoefficient { get; }
}

public interface IDiseaseDefinitionCatalog
{
    IReadOnlyList<DiseaseDefinition> Definitions { get; }
    DiseaseDefinition Require(string diseaseId);
}

[Serializable]
public sealed class DiseaseImmunitySaveData
{
    public string diseaseId = string.Empty;
    public float value;
    public float dailyDecay;
}

[Serializable]
public sealed class ActiveDiseaseSaveData
{
    public string diseaseId = string.Empty;
    public int infectionDay;
    public int symptomDay;
    public int recoveryDay;
    public float severity;
    public bool diagnosed;
}

[Serializable]
public sealed class CharacterPopulationHealthSaveData
{
    public string characterId = string.Empty;
    public List<DiseaseImmunitySaveData> immunity = new();
    public List<ActiveDiseaseSaveData> activeDiseases = new();
}

[Serializable]
public sealed class DiseaseExposureSaveData
{
    public string characterId = string.Empty;
    public string diseaseId = string.Empty;
    public float weightedExposureHours;
    public float susceptibility = 1f;
}

[Serializable]
public sealed class EpidemicStateSaveData
{
    public string diseaseId = string.Empty;
    public bool declared;
    public int lastNewCaseDay;
    public List<int> recentDiagnosisDays = new();
}

[Serializable]
public sealed class PopulationHealthWorldSaveData
{
    public const int CurrentVersion = 1;
    public int version = CurrentVersion;
    public int currentAbsoluteDay = 1;
    public List<CharacterPopulationHealthSaveData> characters = new();
    public List<DiseaseExposureSaveData> pendingExposures = new();
    public List<EpidemicStateSaveData> epidemics = new();
}

public readonly struct PopulationExposureTarget
{
    public PopulationExposureTarget(CharacterId characterId, float susceptibility)
    {
        CharacterId = characterId;
        Susceptibility = Math.Max(0.05f, susceptibility);
    }
    public CharacterId CharacterId { get; }
    public float Susceptibility { get; }
}

public enum PopulationHealthChangeKind
{
    Infected = 0,
    Diagnosed = 1,
    DailyBodyBurden = 2,
    Recovered = 3
}

public readonly struct PopulationHealthChange
{
    public PopulationHealthChange(
        CharacterId characterId,
        string diseaseId,
        PopulationHealthChangeKind kind,
        float severity,
        DiseaseTargetSystem targetSystem)
    {
        CharacterId = characterId;
        DiseaseId = diseaseId;
        Kind = kind;
        Severity = severity;
        TargetSystem = targetSystem;
    }
    public CharacterId CharacterId { get; }
    public string DiseaseId { get; }
    public PopulationHealthChangeKind Kind { get; }
    public float Severity { get; }
    public DiseaseTargetSystem TargetSystem { get; }
}

public readonly struct ContagiousDiseaseSnapshot
{
    public ContagiousDiseaseSnapshot(CharacterId characterId, string diseaseId)
    {
        CharacterId = characterId;
        DiseaseId = diseaseId;
    }
    public CharacterId CharacterId { get; }
    public string DiseaseId { get; }
}

public readonly struct ActiveDiseaseSnapshot
{
    public ActiveDiseaseSnapshot(
        string diseaseId,
        int infectionDay,
        int symptomDay,
        int recoveryDay,
        float severity,
        bool diagnosed)
    {
        DiseaseId = diseaseId?.Trim() ?? string.Empty;
        InfectionDay = infectionDay;
        SymptomDay = symptomDay;
        RecoveryDay = recoveryDay;
        Severity = Math.Clamp(severity, 0f, 100f);
        Diagnosed = diagnosed;
    }

    public string DiseaseId { get; }
    public int InfectionDay { get; }
    public int SymptomDay { get; }
    public int RecoveryDay { get; }
    public float Severity { get; }
    public bool Diagnosed { get; }
}

public readonly struct PopulationCharacterHealthSnapshot
{
    public PopulationCharacterHealthSnapshot(
        CharacterId characterId,
        IReadOnlyList<ActiveDiseaseSnapshot> activeDiseases)
    {
        CharacterId = characterId;
        ActiveDiseases = activeDiseases
            ?? Array.Empty<ActiveDiseaseSnapshot>();
    }

    public CharacterId CharacterId { get; }
    public IReadOnlyList<ActiveDiseaseSnapshot> ActiveDiseases { get; }
}

public readonly struct EpidemicSnapshot
{
    public EpidemicSnapshot(
        string diseaseId,
        bool declared,
        int lastNewCaseDay)
    {
        DiseaseId = diseaseId?.Trim() ?? string.Empty;
        Declared = declared;
        LastNewCaseDay = lastNewCaseDay;
    }

    public string DiseaseId { get; }
    public bool Declared { get; }
    public int LastNewCaseDay { get; }
}

public sealed class PopulationHealthAggregateState
{
    private readonly Dictionary<CharacterId, CharacterPopulationHealthSaveData> characters = new();
    private readonly Dictionary<string, DiseaseExposureSaveData> exposures = new(StringComparer.Ordinal);
    private readonly Dictionary<string, EpidemicStateSaveData> epidemics = new(StringComparer.Ordinal);

    public int CurrentAbsoluteDay { get; private set; } = 1;

    public void RecordExposure(
        string diseaseId,
        IReadOnlyList<PopulationExposureTarget> targets,
        float exposureHours,
        float environmentCoefficient,
        IDiseaseDefinitionCatalog definitions)
    {
        DiseaseDefinition disease = definitions.Require(diseaseId);
        if (!disease.Contagious || exposureHours <= 0f || environmentCoefficient <= 0f)
            return;
        foreach (PopulationExposureTarget target in targets
                     ?? Array.Empty<PopulationExposureTarget>())
        {
            if (!target.CharacterId.IsValid || IsActivelyInfected(target.CharacterId, disease.Id))
                continue;
            string key = ExposureKey(target.CharacterId, disease.Id);
            if (!exposures.TryGetValue(key, out DiseaseExposureSaveData exposure))
            {
                exposure = new DiseaseExposureSaveData
                {
                    characterId = target.CharacterId.Value,
                    diseaseId = disease.Id,
                    susceptibility = target.Susceptibility
                };
                exposures.Add(key, exposure);
            }
            exposure.weightedExposureHours = Math.Min(
                24f,
                exposure.weightedExposureHours
                + exposureHours * environmentCoefficient);
            exposure.susceptibility = Math.Max(
                exposure.susceptibility,
                target.Susceptibility);
        }
    }

    public IReadOnlyList<PopulationHealthChange> AdvanceToDay(
        int absoluteDay,
        IDiseaseDefinitionCatalog definitions,
        Func<double> nextUnitRandom)
    {
        if (absoluteDay <= CurrentAbsoluteDay)
            throw new InvalidOperationException("Population health requires strictly increasing daily updates.");
        if (nextUnitRandom == null) throw new ArgumentNullException(nameof(nextUnitRandom));
        List<PopulationHealthChange> changes = new();
        while (CurrentAbsoluteDay < absoluteDay)
        {
            CurrentAbsoluteDay++;
            DecayImmunity();
            ResolveExposures(definitions, nextUnitRandom, changes);
            AdvanceActiveDiseases(definitions, changes);
            AdvanceEpidemics();
        }
        return changes;
    }

    public void Vaccinate(
        CharacterId characterId,
        string diseaseId,
        IDiseaseDefinitionCatalog definitions)
    {
        DiseaseDefinition disease = definitions.Require(diseaseId);
        if (!disease.VaccineAllowed)
            throw new InvalidOperationException($"Disease '{disease.Id}' does not permit vaccination.");
        CharacterPopulationHealthSaveData record = RequireCharacter(characterId);
        SetImmunity(record, disease.Id, 70f, 0.05f);
    }

    public PopulationHealthChange ApplyEnvironmentalCondition(
        CharacterId characterId,
        string diseaseId,
        IDiseaseDefinitionCatalog definitions)
    {
        DiseaseDefinition disease = definitions.Require(diseaseId);
        if (!disease.Chronic || disease.Contagious)
            throw new InvalidOperationException(
                $"Disease '{disease.Id}' is not an environmental chronic condition.");
        CharacterPopulationHealthSaveData record = RequireCharacter(characterId);
        if (record.activeDiseases.Any(value =>
                string.Equals(value.diseaseId, disease.Id, StringComparison.Ordinal)))
            throw new InvalidOperationException(
                $"Character '{characterId.Value}' already has condition '{disease.Id}'.");
        record.activeDiseases.Add(new ActiveDiseaseSaveData
        {
            diseaseId = disease.Id,
            infectionDay = CurrentAbsoluteDay,
            symptomDay = CurrentAbsoluteDay,
            recoveryDay = int.MaxValue,
            severity = disease.BaseSeverity,
            diagnosed = true
        });
        return new PopulationHealthChange(
            characterId,
            disease.Id,
            PopulationHealthChangeKind.Diagnosed,
            disease.BaseSeverity,
            disease.TargetSystem);
    }

    public void RemoveEnvironmentalCondition(
        CharacterId characterId,
        string diseaseId,
        IDiseaseDefinitionCatalog definitions)
    {
        DiseaseDefinition disease = definitions.Require(diseaseId);
        if (!disease.Chronic)
            throw new InvalidOperationException(
                $"Disease '{disease.Id}' is not a chronic condition.");
        if (!characters.TryGetValue(characterId, out CharacterPopulationHealthSaveData record))
            throw new InvalidOperationException(
                $"Character '{characterId.Value}' does not have condition '{disease.Id}'.");
        ActiveDiseaseSaveData active = record.activeDiseases.FirstOrDefault(value =>
            string.Equals(value.diseaseId, disease.Id, StringComparison.Ordinal));
        if (active == null)
            throw new InvalidOperationException(
                $"Character '{characterId.Value}' does not have condition '{disease.Id}'.");
        record.activeDiseases.Remove(active);
    }

    public float GetImmunity(CharacterId characterId, string diseaseId)
    {
        if (!characters.TryGetValue(characterId, out CharacterPopulationHealthSaveData record))
            return 0f;
        return record.immunity.FirstOrDefault(value =>
            string.Equals(value.diseaseId, diseaseId, StringComparison.Ordinal))?.value ?? 0f;
    }

    public bool IsEpidemicDeclared(string diseaseId) =>
        epidemics.TryGetValue(diseaseId?.Trim() ?? string.Empty, out EpidemicStateSaveData state)
        && state.declared;

    public IReadOnlyList<ContagiousDiseaseSnapshot> GetContagious(
        IDiseaseDefinitionCatalog definitions)
    {
        List<ContagiousDiseaseSnapshot> result = new();
        foreach (KeyValuePair<CharacterId, CharacterPopulationHealthSaveData> pair in characters)
        {
            foreach (ActiveDiseaseSaveData active in pair.Value.activeDiseases)
            {
                DiseaseDefinition disease = definitions.Require(active.diseaseId);
                if (disease.Contagious
                    && CurrentAbsoluteDay >= active.symptomDay
                    && CurrentAbsoluteDay < active.recoveryDay)
                {
                    result.Add(new ContagiousDiseaseSnapshot(pair.Key, disease.Id));
                }
            }
        }
        return result.OrderBy(value => value.CharacterId.Value, StringComparer.Ordinal)
            .ThenBy(value => value.DiseaseId, StringComparer.Ordinal)
            .ToArray();
    }

    public bool TryGetCharacterSnapshot(
        CharacterId characterId,
        out PopulationCharacterHealthSnapshot snapshot)
    {
        if (!characters.TryGetValue(
                characterId,
                out CharacterPopulationHealthSaveData record))
        {
            snapshot = default;
            return false;
        }

        snapshot = new PopulationCharacterHealthSnapshot(
            characterId,
            (record.activeDiseases ?? new List<ActiveDiseaseSaveData>())
                .Where(value => value != null)
                .OrderBy(value => value.diseaseId, StringComparer.Ordinal)
                .Select(value => new ActiveDiseaseSnapshot(
                    value.diseaseId,
                    value.infectionDay,
                    value.symptomDay,
                    value.recoveryDay,
                    value.severity,
                    value.diagnosed))
                .ToArray());
        return true;
    }

    public IReadOnlyList<EpidemicSnapshot> GetEpidemics(bool declaredOnly) =>
        epidemics.Values
            .Where(value => value != null
                && (!declaredOnly || value.declared))
            .OrderBy(value => value.diseaseId, StringComparer.Ordinal)
            .Select(value => new EpidemicSnapshot(
                value.diseaseId,
                value.declared,
                value.lastNewCaseDay))
            .ToArray();

    public PopulationHealthWorldSaveData Capture() => new()
    {
        currentAbsoluteDay = CurrentAbsoluteDay,
        characters = characters.Values
            .OrderBy(value => value.characterId, StringComparer.Ordinal)
            .Select(CloneCharacter)
            .ToList(),
        pendingExposures = exposures.Values
            .OrderBy(value => value.characterId, StringComparer.Ordinal)
            .ThenBy(value => value.diseaseId, StringComparer.Ordinal)
            .Select(CloneExposure)
            .ToList(),
        epidemics = epidemics.Values
            .OrderBy(value => value.diseaseId, StringComparer.Ordinal)
            .Select(CloneEpidemic)
            .ToList()
    };

    public static PopulationHealthAggregateState Restore(
        PopulationHealthWorldSaveData data,
        IDiseaseDefinitionCatalog definitions)
    {
        if (data == null || data.version != PopulationHealthWorldSaveData.CurrentVersion
            || data.currentAbsoluteDay < 1)
            throw new InvalidOperationException("Population-health payload is missing or invalid.");
        PopulationHealthAggregateState state = new() { CurrentAbsoluteDay = data.currentAbsoluteDay };
        foreach (CharacterPopulationHealthSaveData source in data.characters
                     ?? new List<CharacterPopulationHealthSaveData>())
        {
            CharacterId id = new(source?.characterId);
            if (!id.IsValid || !state.characters.TryAdd(id, CloneCharacter(source)))
                throw new InvalidOperationException("Population-health character records are invalid or duplicated.");
            ValidateCharacter(source, definitions);
        }
        foreach (DiseaseExposureSaveData source in data.pendingExposures
                     ?? new List<DiseaseExposureSaveData>())
        {
            CharacterId id = new(source?.characterId);
            definitions.Require(source?.diseaseId);
            if (!id.IsValid || source.weightedExposureHours < 0f
                || !state.exposures.TryAdd(ExposureKey(id, source.diseaseId), CloneExposure(source)))
                throw new InvalidOperationException("Pending disease exposures are invalid or duplicated.");
        }
        foreach (EpidemicStateSaveData source in data.epidemics
                     ?? new List<EpidemicStateSaveData>())
        {
            DiseaseDefinition disease = definitions.Require(source?.diseaseId);
            if (!state.epidemics.TryAdd(disease.Id, CloneEpidemic(source)))
                throw new InvalidOperationException("Epidemic states are duplicated.");
        }
        return state;
    }

    public static double CalculateInfectionProbability(
        float baseProbability,
        float exposureHours,
        float immunity,
        float susceptibility,
        float environmentCoefficient) => Math.Min(
            0.80d,
            Math.Max(0d, baseProbability)
            * Math.Clamp(exposureHours, 0f, 24f) / 24d
            * (1d - Math.Clamp(immunity, 0f, 100f) / 100d)
            * Math.Max(0d, susceptibility)
            * Math.Max(0d, environmentCoefficient));

    private void ResolveExposures(
        IDiseaseDefinitionCatalog definitions,
        Func<double> nextUnitRandom,
        ICollection<PopulationHealthChange> changes)
    {
        foreach (DiseaseExposureSaveData exposure in exposures.Values
                     .OrderBy(value => value.characterId, StringComparer.Ordinal)
                     .ThenBy(value => value.diseaseId, StringComparer.Ordinal))
        {
            CharacterId characterId = new(exposure.characterId);
            DiseaseDefinition disease = definitions.Require(exposure.diseaseId);
            float immunity = GetImmunity(characterId, disease.Id);
            double probability = CalculateInfectionProbability(
                disease.BaseInfectionProbability,
                exposure.weightedExposureHours,
                immunity,
                exposure.susceptibility,
                1f);
            if (ClampUnit(nextUnitRandom()) >= probability) continue;
            CharacterPopulationHealthSaveData record = RequireCharacter(characterId);
            record.activeDiseases.Add(new ActiveDiseaseSaveData
            {
                diseaseId = disease.Id,
                infectionDay = CurrentAbsoluteDay,
                symptomDay = CurrentAbsoluteDay + disease.IncubationDays,
                recoveryDay = CurrentAbsoluteDay + disease.IncubationDays + disease.ContagiousDays,
                severity = disease.BaseSeverity
            });
            changes.Add(new PopulationHealthChange(
                characterId,
                disease.Id,
                PopulationHealthChangeKind.Infected,
                disease.BaseSeverity,
                disease.TargetSystem));
        }
        exposures.Clear();
    }

    private void AdvanceActiveDiseases(
        IDiseaseDefinitionCatalog definitions,
        ICollection<PopulationHealthChange> changes)
    {
        foreach (KeyValuePair<CharacterId, CharacterPopulationHealthSaveData> pair in characters)
        {
            foreach (ActiveDiseaseSaveData active in pair.Value.activeDiseases.ToArray())
            {
                DiseaseDefinition disease = definitions.Require(active.diseaseId);
                if (!active.diagnosed && CurrentAbsoluteDay >= active.symptomDay)
                {
                    active.diagnosed = true;
                    RecordDiagnosis(disease.Id);
                    changes.Add(new PopulationHealthChange(
                        pair.Key, disease.Id, PopulationHealthChangeKind.Diagnosed,
                        active.severity, disease.TargetSystem));
                }
                if (CurrentAbsoluteDay >= active.recoveryDay)
                {
                    pair.Value.activeDiseases.Remove(active);
                    SetImmunity(pair.Value, disease.Id, 80f, 0.02f);
                    changes.Add(new PopulationHealthChange(
                        pair.Key, disease.Id, PopulationHealthChangeKind.Recovered,
                        active.severity, disease.TargetSystem));
                }
                else if (active.diagnosed)
                {
                    changes.Add(new PopulationHealthChange(
                        pair.Key, disease.Id, PopulationHealthChangeKind.DailyBodyBurden,
                        active.severity, disease.TargetSystem));
                }
            }
        }
    }

    private void RecordDiagnosis(string diseaseId)
    {
        if (!epidemics.TryGetValue(diseaseId, out EpidemicStateSaveData state))
        {
            state = new EpidemicStateSaveData { diseaseId = diseaseId };
            epidemics.Add(diseaseId, state);
        }
        state.lastNewCaseDay = CurrentAbsoluteDay;
        state.recentDiagnosisDays.Add(CurrentAbsoluteDay);
        state.recentDiagnosisDays.RemoveAll(day => CurrentAbsoluteDay - day > 10);
        if (state.recentDiagnosisDays.Count >= 3) state.declared = true;
    }

    private void AdvanceEpidemics()
    {
        foreach (EpidemicStateSaveData state in epidemics.Values)
        {
            state.recentDiagnosisDays.RemoveAll(day => CurrentAbsoluteDay - day > 10);
            if (state.declared && CurrentAbsoluteDay - state.lastNewCaseDay >= 14)
                state.declared = false;
        }
    }

    private void DecayImmunity()
    {
        foreach (CharacterPopulationHealthSaveData record in characters.Values)
            foreach (DiseaseImmunitySaveData immunity in record.immunity)
                immunity.value = Math.Max(0f, immunity.value - immunity.dailyDecay);
    }

    private CharacterPopulationHealthSaveData RequireCharacter(CharacterId id)
    {
        if (!id.IsValid) throw new ArgumentException("A valid character id is required.", nameof(id));
        if (!characters.TryGetValue(id, out CharacterPopulationHealthSaveData record))
        {
            record = new CharacterPopulationHealthSaveData { characterId = id.Value };
            characters.Add(id, record);
        }
        return record;
    }

    private bool IsActivelyInfected(CharacterId id, string diseaseId) =>
        characters.TryGetValue(id, out CharacterPopulationHealthSaveData record)
        && record.activeDiseases.Any(value => string.Equals(value.diseaseId, diseaseId, StringComparison.Ordinal));

    private static void SetImmunity(
        CharacterPopulationHealthSaveData record,
        string diseaseId,
        float minimum,
        float decay)
    {
        DiseaseImmunitySaveData state = record.immunity.FirstOrDefault(value =>
            string.Equals(value.diseaseId, diseaseId, StringComparison.Ordinal));
        if (state == null)
        {
            state = new DiseaseImmunitySaveData { diseaseId = diseaseId };
            record.immunity.Add(state);
        }
        state.value = Math.Max(state.value, minimum);
        state.dailyDecay = decay;
    }

    private static void ValidateCharacter(
        CharacterPopulationHealthSaveData source,
        IDiseaseDefinitionCatalog definitions)
    {
        HashSet<string> immunityIds = new(StringComparer.Ordinal);
        foreach (DiseaseImmunitySaveData immunity in source.immunity ?? new())
        {
            DiseaseDefinition disease = definitions.Require(immunity?.diseaseId);
            if (!immunityIds.Add(disease.Id) || immunity.value < 0f || immunity.value > 100f
                || immunity.dailyDecay < 0f)
                throw new InvalidOperationException("Disease immunity state is invalid or duplicated.");
        }
        HashSet<string> activeIds = new(StringComparer.Ordinal);
        foreach (ActiveDiseaseSaveData active in source.activeDiseases ?? new())
        {
            DiseaseDefinition disease = definitions.Require(active?.diseaseId);
            if (!activeIds.Add(disease.Id) || active.infectionDay < 1
                || active.symptomDay < active.infectionDay
                || active.recoveryDay < active.symptomDay)
                throw new InvalidOperationException("Active disease state is invalid or duplicated.");
        }
    }

    private static CharacterPopulationHealthSaveData CloneCharacter(
        CharacterPopulationHealthSaveData source) => new()
    {
        characterId = source.characterId,
        immunity = (source.immunity ?? new()).Select(value => new DiseaseImmunitySaveData
        {
            diseaseId = value.diseaseId,
            value = value.value,
            dailyDecay = value.dailyDecay
        }).ToList(),
        activeDiseases = (source.activeDiseases ?? new()).Select(value => new ActiveDiseaseSaveData
        {
            diseaseId = value.diseaseId,
            infectionDay = value.infectionDay,
            symptomDay = value.symptomDay,
            recoveryDay = value.recoveryDay,
            severity = value.severity,
            diagnosed = value.diagnosed
        }).ToList()
    };

    private static DiseaseExposureSaveData CloneExposure(DiseaseExposureSaveData value) => new()
    {
        characterId = value.characterId,
        diseaseId = value.diseaseId,
        weightedExposureHours = value.weightedExposureHours,
        susceptibility = value.susceptibility
    };

    private static EpidemicStateSaveData CloneEpidemic(EpidemicStateSaveData value) => new()
    {
        diseaseId = value.diseaseId,
        declared = value.declared,
        lastNewCaseDay = value.lastNewCaseDay,
        recentDiagnosisDays = new List<int>(value.recentDiagnosisDays ?? new())
    };

    private static string ExposureKey(CharacterId id, string diseaseId) =>
        id.Value + "\n" + (diseaseId?.Trim() ?? string.Empty);
    private static double ClampUnit(double value) =>
        Math.Max(0d, Math.Min(0.999999999999d, value));
}

public interface IPopulationHealthService
{
    int Version { get; }
    void RecordExposure(
        string diseaseId,
        IReadOnlyList<PopulationExposureTarget> targets,
        float exposureHours,
        float environmentCoefficient);
    IReadOnlyList<PopulationHealthChange> AdvanceToDay(int absoluteDay);
    void Vaccinate(CharacterId characterId, string diseaseId);
    PopulationHealthChange ApplyEnvironmentalCondition(CharacterId characterId, string diseaseId);
    void RemoveEnvironmentalCondition(CharacterId characterId, string diseaseId);
    float GetImmunity(CharacterId characterId, string diseaseId);
    bool IsEpidemicDeclared(string diseaseId);
    IReadOnlyList<ContagiousDiseaseSnapshot> GetContagious();
}

public interface IPopulationHealthQuery
{
    int Version { get; }
    float GetImmunity(CharacterId characterId, string diseaseId);
    bool TryGetCharacterSnapshot(
        CharacterId characterId,
        out PopulationCharacterHealthSnapshot snapshot);
    IReadOnlyList<EpidemicSnapshot> GetEpidemics(bool declaredOnly);
    IReadOnlyList<ContagiousDiseaseSnapshot> GetContagious();
}

public interface IPopulationHealthPersistence
{
    PopulationHealthWorldSaveData Capture();
    PopulationHealthAggregateState PrepareRestore(PopulationHealthWorldSaveData data);
    void PublishRestore(PopulationHealthAggregateState candidate);
}
