using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation.Facts;

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
        bool chronic = false,
        string symptomProfileId = "",
        IEnumerable<string> fieldResponseIds = null,
        string vaccineItemId = "")
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
        SymptomProfileId = symptomProfileId?.Trim() ?? string.Empty;
        VaccineItemId = vaccineItemId?.Trim() ?? string.Empty;
        FieldResponseIds = (fieldResponseIds ?? Array.Empty<string>())
            .Select(value => value?.Trim() ?? string.Empty)
            .Where(value => value.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
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
    public string SymptomProfileId { get; }
    public string VaccineItemId { get; }
    public IReadOnlyList<string> FieldResponseIds { get; }
    public bool Contagious => ContagiousDays > 0
        && (Routes & ~DiseaseTransmissionRoute.Environment) != 0;
    public bool IsValid => Id.Length > 0
        && DisplayName.Length > 0
        && Routes != DiseaseTransmissionRoute.None
        && BaseSeverity > 0f
        && SymptomProfileId.Length > 0
        && FieldResponseIds.Count > 0
        && (!Chronic || !Contagious)
        && (!Contagious || BaseInfectionProbability > 0f);
}

public readonly struct DiseaseSymptomEffectSnapshot
{
    public DiseaseSymptomEffectSnapshot(
        string diseaseId,
        string symptomProfileId,
        DiseaseTargetSystem targetSystem,
        float severity,
        float workSpeedMultiplier,
        float moveSpeedMultiplier,
        float moodDelta)
    {
        DiseaseId = diseaseId?.Trim() ?? string.Empty;
        SymptomProfileId = symptomProfileId?.Trim() ?? string.Empty;
        TargetSystem = targetSystem;
        Severity = Math.Clamp(severity, 0f, 100f);
        WorkSpeedMultiplier = Math.Clamp(workSpeedMultiplier, 0.1f, 1f);
        MoveSpeedMultiplier = Math.Clamp(moveSpeedMultiplier, 0.1f, 1f);
        MoodDelta = Math.Clamp(moodDelta, -20f, 0f);
    }

    public string DiseaseId { get; }
    public string SymptomProfileId { get; }
    public DiseaseTargetSystem TargetSystem { get; }
    public float Severity { get; }
    public float WorkSpeedMultiplier { get; }
    public float MoveSpeedMultiplier { get; }
    public float MoodDelta { get; }
}

public readonly struct PopulationDiseaseRouteExposureEvent
{
    public PopulationDiseaseRouteExposureEvent(
        CharacterId characterId,
        string diseaseId,
        DiseaseTransmissionRoute route,
        float exposureHours,
        float environmentCoefficient,
        string sourceKind = "population-route-event",
        string sourceId = "unspecified-source")
    {
        CharacterId = characterId;
        DiseaseId = diseaseId?.Trim() ?? string.Empty;
        Route = route;
        ExposureHours = Math.Max(0f, exposureHours);
        EnvironmentCoefficient = Math.Max(0f, environmentCoefficient);
        SourceKind = sourceKind?.Trim() ?? string.Empty;
        SourceId = sourceId?.Trim() ?? string.Empty;
    }

    public CharacterId CharacterId { get; }
    public string DiseaseId { get; }
    public DiseaseTransmissionRoute Route { get; }
    public float ExposureHours { get; }
    public float EnvironmentCoefficient { get; }
    public string SourceKind { get; }
    public string SourceId { get; }
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

/// <summary>
/// Bounded durable outbox for one disease-exposure fact whose gameplay-ledger
/// commit is unresolved. It contains value snapshots only, never a runtime lease.
/// </summary>
[Serializable]
public sealed class PopulationDiseaseExposurePendingFactSaveData
{
    public string characterId = string.Empty;
    public string characterDisplayName = string.Empty;
    public string diseaseId = string.Empty;
    public string diseaseDisplayName = string.Empty;
    public int diseaseRoutes;
    public int incubationDays;
    public int contagiousDays;
    public float baseInfectionProbability;
    public float baseSeverity;
    public int targetSystem;
    public bool vaccineAllowed;
    public bool chronic;
    public string symptomProfileId = string.Empty;
    public List<string> fieldResponseIds = new();
    public int route;
    public float exposureHours;
    public float environmentCoefficient;
    public int absoluteDay;
    public long ownerRevision;
    public string sourceKind = string.Empty;
    public string sourceId = string.Empty;
}

[Serializable]
public sealed class EpidemicStateSaveData
{
    public string diseaseId = string.Empty;
    public bool declared;
    public int lastNewCaseDay;
    public List<int> recentDiagnosisDays = new();
}

public enum DiseaseFieldResponseCommitPhase
{
    None = 0,
    IntentRecorded = 1,
    OutcomePublished = 2
}

[Serializable]
public sealed class DiseaseFieldResponseCommitSaveData
{
    public int phase;
    public int operationSequence;
    public string operationId = string.Empty;
    public string reasonCode = string.Empty;
    public string characterId = string.Empty;
    public string diseaseId = string.Empty;
    public string responseId = string.Empty;
    public string facilityInstanceId = string.Empty;
    public int outputGridX;
    public int outputGridY;
    public string itemId = string.Empty;
    public int quantity;
    public float severityReduction;
    public List<string> sourceStackIds = new();
    public long inputMassGrams;
    public string commitId = string.Empty;
}

public enum VaccinationCommitPhase
{
    None = 0,
    IntentRecorded = 1,
    OutcomePublished = 2
}

[Serializable]
public sealed class VaccinationCommitSaveData
{
    public int phase;
    public int operationSequence;
    public string operationId = string.Empty;
    public string reasonCode = string.Empty;
    public string characterId = string.Empty;
    public string diseaseId = string.Empty;
    public string facilityInstanceId = string.Empty;
    public int outputGridX;
    public int outputGridY;
    public string itemId = string.Empty;
    public int quantity;
    public List<string> sourceStackIds = new();
    public long inputMassGrams;
    public string commitId = string.Empty;
}

[Serializable]
public sealed class PopulationHealthWorldSaveData
{
    public const int CurrentVersion = 7;
    public int version = CurrentVersion;
    public int currentAbsoluteDay = 1;
    public long exposureRevision = 1;
    public int nextFieldResponseOperationSequence = 1;
    public DiseaseFieldResponseCommitSaveData pendingFieldResponse = new();
    public int nextVaccinationOperationSequence = 1;
    public VaccinationCommitSaveData pendingVaccination = new();
    public List<CharacterPopulationHealthSaveData> characters = new();
    public List<DiseaseExposureSaveData> pendingExposures = new();
    public List<PopulationDiseaseExposurePendingFactSaveData>
        pendingExposureFacts = new();
    public List<EpidemicStateSaveData> epidemics = new();
    public List<DiseaseAnatomyBurdenProjectionSaveData>
        pendingAnatomyBurdenProjections = new();
}

public enum DiseaseAnatomyBurdenProjectionPhase
{
    None = 0,
    Pending = 1,
    AnatomyApplied = 2
}

[Serializable]
public sealed class DiseaseAnatomyBurdenProjectionSaveData
{
    public int phase;
    public string operationId = string.Empty;
    public int absoluteDay;
    public string characterId = string.Empty;
    public string diseaseId = string.Empty;
    public int targetSystem;
    public float severity;
    public float requestedInfectionBurden;
    public string nodeId = string.Empty;
    public float infectionBefore;
    public float infectionAfter;
    public int attemptCount;
    public string lastFailureCode = string.Empty;
    public string characterDisplayName = string.Empty;
    public string publicationKey = string.Empty;
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

public readonly struct PopulationDiseaseStatModifiers
{
    public PopulationDiseaseStatModifiers(
        float susceptibility,
        float recoverySpeed,
        float immunityGain,
        float immunityRetention)
    {
        Susceptibility = Math.Clamp(susceptibility, 0.05f, 3f);
        RecoverySpeed = Math.Clamp(recoverySpeed, 0.05f, 10f);
        ImmunityGain = Math.Clamp(immunityGain, 0.05f, 10f);
        ImmunityRetention = Math.Clamp(immunityRetention, 0.05f, 10f);
    }

    public static PopulationDiseaseStatModifiers Neutral => new(1f, 1f, 1f, 1f);

    public float Susceptibility { get; }
    public float RecoverySpeed { get; }
    public float ImmunityGain { get; }
    public float ImmunityRetention { get; }
}

public interface IPopulationDiseaseModifierQuery
{
    PopulationDiseaseStatModifiers Resolve(
        CharacterId characterId,
        DiseaseDefinition disease);
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
        DiseaseTargetSystem targetSystem,
        int absoluteDay = 0)
    {
        CharacterId = characterId;
        DiseaseId = diseaseId;
        Kind = kind;
        Severity = severity;
        TargetSystem = targetSystem;
        AbsoluteDay = Math.Max(0, absoluteDay);
    }
    public CharacterId CharacterId { get; }
    public string DiseaseId { get; }
    public PopulationHealthChangeKind Kind { get; }
    public float Severity { get; }
    public DiseaseTargetSystem TargetSystem { get; }
    public int AbsoluteDay { get; }
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
    private readonly List<PopulationDiseaseExposurePendingFactSaveData>
        pendingExposureFacts = new();
    private readonly List<DiseaseAnatomyBurdenProjectionSaveData>
        pendingAnatomyBurdenProjections = new();

    public int CurrentAbsoluteDay { get; private set; } = 1;
    // Durable exposure command identity, independent of transient query-cache versions.
    public long ExposureRevision { get; private set; } = 1;
    public IReadOnlyList<PopulationDiseaseExposurePendingFactSaveData>
        PendingExposureFacts => pendingExposureFacts
            .Select(ClonePendingExposureFact)
            .ToArray();
    public int NextFieldResponseOperationSequence { get; private set; } = 1;
    public DiseaseFieldResponseCommitSaveData PendingFieldResponse { get; private set; } =
        new();
    public int NextVaccinationOperationSequence { get; private set; } = 1;
    public VaccinationCommitSaveData PendingVaccination { get; private set; } =
        new();
    public IReadOnlyList<DiseaseAnatomyBurdenProjectionSaveData>
        PendingAnatomyBurdenProjections => pendingAnatomyBurdenProjections
            .OrderBy(value => value.operationId, StringComparer.Ordinal)
            .Select(CloneAnatomyBurdenProjection)
            .ToArray();

    public void EnqueueAnatomyBurdenProjection(
        PopulationHealthChange change,
        float requestedInfectionBurden)
    {
        if (change.Kind != PopulationHealthChangeKind.DailyBodyBurden
            || change.AbsoluteDay <= 0
            || requestedInfectionBurden <= 0f)
            return;
        string operationId = string.Join(":", new[]
        {
            "disease-anatomy-burden",
            change.CharacterId.Value,
            change.DiseaseId,
            change.AbsoluteDay.ToString("D8")
        });
        DiseaseAnatomyBurdenProjectionSaveData existing =
            pendingAnatomyBurdenProjections.SingleOrDefault(value =>
                string.Equals(value.operationId, operationId,
                    StringComparison.Ordinal));
        if (existing != null)
            return;
        pendingAnatomyBurdenProjections.Add(new DiseaseAnatomyBurdenProjectionSaveData
        {
            phase = (int)DiseaseAnatomyBurdenProjectionPhase.Pending,
            operationId = operationId,
            absoluteDay = change.AbsoluteDay,
            characterId = change.CharacterId.Value,
            diseaseId = change.DiseaseId,
            targetSystem = (int)change.TargetSystem,
            severity = change.Severity,
            requestedInfectionBurden = requestedInfectionBurden
        });
    }

    public void RecordAnatomyBurdenApplied(
        string operationId,
        string nodeId,
        float infectionBefore,
        float infectionAfter,
        string characterDisplayName)
    {
        DiseaseAnatomyBurdenProjectionSaveData projection =
            RequireAnatomyBurdenProjection(operationId);
        if ((DiseaseAnatomyBurdenProjectionPhase)projection.phase
            == DiseaseAnatomyBurdenProjectionPhase.AnatomyApplied)
        {
            if (projection.nodeId != nodeId || projection.infectionBefore != infectionBefore
                || projection.infectionAfter != infectionAfter || projection.characterDisplayName != characterDisplayName)
                throw new InvalidOperationException("Disease anatomy burden application conflicts with its frozen fact.");
            return;
        }
        if ((DiseaseAnatomyBurdenProjectionPhase)projection.phase
                != DiseaseAnatomyBurdenProjectionPhase.Pending
            || string.IsNullOrWhiteSpace(nodeId)
            || infectionBefore < 0f
            || infectionAfter < infectionBefore)
        {
            throw new InvalidOperationException(
                "Disease anatomy burden application is invalid.");
        }
        var candidate = CloneAnatomyBurdenProjection(projection);
        candidate.phase = (int)DiseaseAnatomyBurdenProjectionPhase.AnatomyApplied;
        candidate.nodeId = nodeId; candidate.infectionBefore = infectionBefore; candidate.infectionAfter = infectionAfter;
        candidate.characterDisplayName = characterDisplayName;
        _ = new DiseaseAnatomyBurdenAppliedFact(candidate);
        projection.phase = candidate.phase;
        projection.nodeId = nodeId.Trim();
        projection.infectionBefore = infectionBefore;
        projection.infectionAfter = infectionAfter;
        projection.characterDisplayName = characterDisplayName;
        projection.lastFailureCode = string.Empty;
    }

    public void RecordAnatomyBurdenPublication(string operationId, string publicationKey)
    {
        var projection = RequireAnatomyBurdenProjection(operationId);
        if (projection.phase != (int)DiseaseAnatomyBurdenProjectionPhase.AnatomyApplied
            || !DomainFactValueSyntax.IsText(publicationKey, 1024, false)
            || projection.publicationKey.Length > 0 && projection.publicationKey != publicationKey)
            throw new InvalidOperationException("Disease anatomy burden publication conflicts with its applied fact.");
        projection.publicationKey = publicationKey;
    }

    public void ResetAnatomyBurdenProjection(string operationId)
    {
        DiseaseAnatomyBurdenProjectionSaveData projection =
            RequireAnatomyBurdenProjection(operationId);
        projection.phase = (int)DiseaseAnatomyBurdenProjectionPhase.Pending;
        projection.nodeId = string.Empty;
        projection.infectionBefore = 0f;
        projection.infectionAfter = 0f;
        projection.characterDisplayName = string.Empty;
        projection.publicationKey = string.Empty;
    }

    public void RecordAnatomyBurdenFailure(
        string operationId,
        string failureCode)
    {
        DiseaseAnatomyBurdenProjectionSaveData projection =
            RequireAnatomyBurdenProjection(operationId);
        projection.attemptCount = checked(projection.attemptCount + 1);
        projection.lastFailureCode = string.IsNullOrWhiteSpace(failureCode)
            ? "disease-anatomy-burden-failed"
            : failureCode.Trim();
    }

    public void CompleteAnatomyBurdenProjection(string operationId)
    {
        var projection = RequireAnatomyBurdenProjection(operationId);
        if (projection.phase != (int)DiseaseAnatomyBurdenProjectionPhase.AnatomyApplied
            || !DomainFactValueSyntax.IsText(projection.publicationKey, 1024, false))
            throw new InvalidOperationException("Disease anatomy burden cannot complete before observation.");
        int removed = pendingAnatomyBurdenProjections.RemoveAll(value =>
            string.Equals(value.operationId, operationId,
                StringComparison.Ordinal));
        if (removed != 1)
            throw new InvalidOperationException(
                "Disease anatomy burden completion was not exact.");
    }

    private DiseaseAnatomyBurdenProjectionSaveData
        RequireAnatomyBurdenProjection(string operationId) =>
        pendingAnatomyBurdenProjections.SingleOrDefault(value =>
            string.Equals(value.operationId, operationId,
                StringComparison.Ordinal))
        ?? throw new InvalidOperationException(
            "Disease anatomy burden projection is missing.");

    public void RecordExposure(
        string diseaseId,
        IReadOnlyList<PopulationExposureTarget> targets,
        float exposureHours,
        float environmentCoefficient,
        IDiseaseDefinitionCatalog definitions)
    {
        DiseaseDefinition disease = definitions.Require(diseaseId);
        long nextExposureRevision = checked(ExposureRevision + 1L);
        if (!disease.Contagious || exposureHours <= 0f || environmentCoefficient <= 0f)
        {
            ExposureRevision = nextExposureRevision;
            return;
        }
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
        ExposureRevision = nextExposureRevision;
    }

    public void StagePendingExposureFact(
        PopulationDiseaseExposurePendingFactSaveData fact,
        IDiseaseDefinitionCatalog definitions)
    {
        if (pendingExposureFacts.Count != 0)
            throw new InvalidOperationException(
                "A population disease-exposure publication is already pending.");
        ValidatePendingExposureFact(fact, ExposureRevision, definitions);
        pendingExposureFacts.Add(ClonePendingExposureFact(fact));
    }

    public void CompletePendingExposureFact(long ownerRevision)
    {
        if (pendingExposureFacts.Count != 1
            || pendingExposureFacts[0].ownerRevision != ownerRevision)
        {
            throw new InvalidOperationException(
                "Population disease-exposure completion does not match its pending fact.");
        }
        pendingExposureFacts.Clear();
    }

    public int RemovePendingExposures(CharacterId characterId)
    {
        if (!characterId.IsValid)
        {
            return 0;
        }

        string[] keys = exposures
            .Where(pair => string.Equals(
                pair.Value?.characterId,
                characterId.Value,
                StringComparison.Ordinal))
            .Select(pair => pair.Key)
            .ToArray();
        for (int index = 0; index < keys.Length; index++)
        {
            exposures.Remove(keys[index]);
        }
        return keys.Length;
    }

    public IReadOnlyList<PopulationHealthChange> AdvanceToDay(
        int absoluteDay,
        IDiseaseDefinitionCatalog definitions,
        Func<double> nextUnitRandom,
        Func<CharacterId, DiseaseDefinition, PopulationDiseaseStatModifiers>
            resolveModifiers = null)
    {
        if (absoluteDay <= CurrentAbsoluteDay)
            throw new InvalidOperationException("Population health requires strictly increasing daily updates.");
        if (nextUnitRandom == null) throw new ArgumentNullException(nameof(nextUnitRandom));
        List<PopulationHealthChange> changes = new();
        while (CurrentAbsoluteDay < absoluteDay)
        {
            CurrentAbsoluteDay++;
            DecayImmunity(definitions, resolveModifiers);
            ResolveExposures(definitions, nextUnitRandom, changes, resolveModifiers);
            AdvanceActiveDiseases(definitions, changes, resolveModifiers);
            AdvanceEpidemics();
        }
        return changes;
    }

    public void Vaccinate(
        CharacterId characterId,
        string diseaseId,
        IDiseaseDefinitionCatalog definitions)
    {
        Vaccinate(
            characterId,
            diseaseId,
            definitions,
            PopulationDiseaseStatModifiers.Neutral);
    }

    public void Vaccinate(
        CharacterId characterId,
        string diseaseId,
        IDiseaseDefinitionCatalog definitions,
        PopulationDiseaseStatModifiers modifiers)
    {
        DiseaseDefinition disease = definitions.Require(diseaseId);
        if (!disease.VaccineAllowed)
            throw new InvalidOperationException($"Disease '{disease.Id}' does not permit vaccination.");
        CharacterPopulationHealthSaveData record = RequireCharacter(characterId);
        SetImmunity(record, disease.Id, 70f, 0.05f, modifiers.ImmunityGain);
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

    public float ApplyFieldResponse(
        CharacterId characterId,
        string diseaseId,
        float severityReduction,
        IDiseaseDefinitionCatalog definitions)
    {
        return ApplyFieldResponse(
            characterId,
            diseaseId,
            severityReduction,
            definitions,
            PopulationDiseaseStatModifiers.Neutral);
    }

    public float ApplyFieldResponse(
        CharacterId characterId,
        string diseaseId,
        float severityReduction,
        IDiseaseDefinitionCatalog definitions,
        PopulationDiseaseStatModifiers modifiers)
    {
        DiseaseDefinition disease = definitions.Require(diseaseId);
        if (!characters.TryGetValue(characterId, out CharacterPopulationHealthSaveData record))
            throw new InvalidOperationException(
                $"Character '{characterId.Value}' has no population-health record.");
        ActiveDiseaseSaveData active = record.activeDiseases.FirstOrDefault(value =>
            string.Equals(value.diseaseId, disease.Id, StringComparison.Ordinal));
        if (active == null
            || CurrentAbsoluteDay < active.symptomDay
            || CurrentAbsoluteDay >= active.recoveryDay)
            throw new InvalidOperationException(
                $"Character '{characterId.Value}' has no active symptoms for '{disease.Id}'.");

        active.diagnosed = true;
        active.severity = Math.Max(0f, active.severity - Math.Max(0f, severityReduction));
        if (active.severity <= 0.001f)
        {
            record.activeDiseases.Remove(active);
            if (disease.Contagious)
                SetImmunity(record, disease.Id, 35f, 0.08f, modifiers.ImmunityGain);
            return 0f;
        }
        return active.severity;
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
        exposureRevision = ExposureRevision,
        nextFieldResponseOperationSequence = NextFieldResponseOperationSequence,
        pendingFieldResponse = CloneFieldResponseCommit(PendingFieldResponse),
        nextVaccinationOperationSequence = NextVaccinationOperationSequence,
        pendingVaccination = CloneVaccinationCommit(PendingVaccination),
        characters = characters.Values
            .OrderBy(value => value.characterId, StringComparer.Ordinal)
            .Select(CloneCharacter)
            .ToList(),
        pendingExposures = exposures.Values
            .OrderBy(value => value.characterId, StringComparer.Ordinal)
            .ThenBy(value => value.diseaseId, StringComparer.Ordinal)
            .Select(CloneExposure)
            .ToList(),
        pendingExposureFacts = pendingExposureFacts
            .Select(ClonePendingExposureFact)
            .ToList(),
        epidemics = epidemics.Values
            .OrderBy(value => value.diseaseId, StringComparer.Ordinal)
            .Select(CloneEpidemic)
            .ToList(),
        pendingAnatomyBurdenProjections = pendingAnatomyBurdenProjections
            .OrderBy(value => value.operationId, StringComparer.Ordinal)
            .Select(CloneAnatomyBurdenProjection)
            .ToList()
    };

    public static PopulationHealthAggregateState Restore(
        PopulationHealthWorldSaveData data,
        IDiseaseDefinitionCatalog definitions)
    {
        if (data == null || data.version != PopulationHealthWorldSaveData.CurrentVersion
            || data.currentAbsoluteDay < 1 || data.exposureRevision < 1
            || data.pendingExposureFacts == null
            || data.pendingExposureFacts.Count > 1
            || (data.pendingExposureFacts.Count == 1
                && data.pendingExposureFacts[0] == null))
            throw new InvalidOperationException("Population-health payload is missing or invalid.");
        if (data.nextFieldResponseOperationSequence <= 0)
            throw new InvalidOperationException(
                "Population-health field-response sequence is invalid.");
        if (data.nextVaccinationOperationSequence <= 0)
            throw new InvalidOperationException(
                "Population-health vaccination sequence is invalid.");
        ValidateFieldResponseCommit(
            data.pendingFieldResponse,
            data.nextFieldResponseOperationSequence,
            definitions);
        ValidateVaccinationCommit(
            data.pendingVaccination,
            data.nextVaccinationOperationSequence,
            definitions);
        PopulationHealthAggregateState state = new()
        {
            CurrentAbsoluteDay = data.currentAbsoluteDay,
            ExposureRevision = data.exposureRevision,
            NextFieldResponseOperationSequence =
                data.nextFieldResponseOperationSequence,
            PendingFieldResponse = CloneFieldResponseCommit(
                data.pendingFieldResponse),
            NextVaccinationOperationSequence =
                data.nextVaccinationOperationSequence,
            PendingVaccination = CloneVaccinationCommit(
                data.pendingVaccination)
        };
        foreach (CharacterPopulationHealthSaveData source in data.characters
                     ?? new List<CharacterPopulationHealthSaveData>())
        {
            CharacterId id = new(source?.characterId);
            if (!id.IsValid || !state.characters.TryAdd(id, CloneCharacter(source)))
                throw new InvalidOperationException("Population-health character records are invalid or duplicated.");
            ValidateCharacter(source, definitions);
        }
        DiseaseFieldResponseCommitPhase pendingPhase =
            (DiseaseFieldResponseCommitPhase)state.PendingFieldResponse.phase;
        if (pendingPhase != DiseaseFieldResponseCommitPhase.None)
        {
            CharacterId pendingCharacter = new(
                state.PendingFieldResponse.characterId);
            if (!state.characters.TryGetValue(
                    pendingCharacter,
                    out CharacterPopulationHealthSaveData pendingRecord))
            {
                throw new InvalidOperationException(
                    "Population-health field-response character is missing.");
            }
            if (pendingPhase == DiseaseFieldResponseCommitPhase.IntentRecorded)
            {
                ActiveDiseaseSaveData active = pendingRecord.activeDiseases
                    .FirstOrDefault(value => string.Equals(
                        value.diseaseId,
                        state.PendingFieldResponse.diseaseId,
                        StringComparison.Ordinal));
                if (active == null
                    || state.CurrentAbsoluteDay < active.symptomDay
                    || state.CurrentAbsoluteDay >= active.recoveryDay)
                {
                    throw new InvalidOperationException(
                        "Population-health field-response intent has no active target.");
                }
            }
        }
        if ((VaccinationCommitPhase)state.PendingVaccination.phase
                != VaccinationCommitPhase.None
            && !state.characters.ContainsKey(
                new CharacterId(state.PendingVaccination.characterId)))
        {
            throw new InvalidOperationException(
                "Population-health vaccination character is missing.");
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
        foreach (PopulationDiseaseExposurePendingFactSaveData source in
                 data.pendingExposureFacts)
        {
            ValidatePendingExposureFact(
                source,
                state.ExposureRevision,
                definitions);
            state.pendingExposureFacts.Add(
                ClonePendingExposureFact(source));
        }
        foreach (EpidemicStateSaveData source in data.epidemics
                     ?? new List<EpidemicStateSaveData>())
        {
            DiseaseDefinition disease = definitions.Require(source?.diseaseId);
            if (!state.epidemics.TryAdd(disease.Id, CloneEpidemic(source)))
                throw new InvalidOperationException("Epidemic states are duplicated.");
        }
        foreach (DiseaseAnatomyBurdenProjectionSaveData source in
                 data.pendingAnatomyBurdenProjections
                 ?? new List<DiseaseAnatomyBurdenProjectionSaveData>())
        {
            ValidateAnatomyBurdenProjection(source, definitions);
            if (state.pendingAnatomyBurdenProjections.Any(value =>
                    string.Equals(value.operationId, source.operationId,
                        StringComparison.Ordinal)))
            {
                throw new InvalidOperationException(
                    "Disease anatomy burden projections are duplicated.");
            }
            state.pendingAnatomyBurdenProjections.Add(
                CloneAnatomyBurdenProjection(source));
        }
        return state;
    }

    private static void ValidateAnatomyBurdenProjection(
        DiseaseAnatomyBurdenProjectionSaveData source,
        IDiseaseDefinitionCatalog definitions)
    {
        if (source == null)
            throw new InvalidOperationException(
                "Disease anatomy burden projection is missing.");
        definitions.Require(source.diseaseId);
        DiseaseAnatomyBurdenProjectionPhase phase =
            (DiseaseAnatomyBurdenProjectionPhase)source.phase;
        if (phase is not DiseaseAnatomyBurdenProjectionPhase.Pending
                and not DiseaseAnatomyBurdenProjectionPhase.AnatomyApplied
            || string.IsNullOrWhiteSpace(source.operationId)
            || !new CharacterId(source.characterId).IsValid
            || source.absoluteDay <= 0
            || !Enum.IsDefined(typeof(DiseaseTargetSystem), source.targetSystem)
            || source.severity < 0f
            || source.requestedInfectionBurden <= 0f
            || float.IsNaN(source.severity) || float.IsInfinity(source.severity)
            || float.IsNaN(source.requestedInfectionBurden) || float.IsInfinity(source.requestedInfectionBurden)
            || source.attemptCount < 0
            || !DomainFactValueSyntax.IsText(source.publicationKey, 1024, true)
            || (phase == DiseaseAnatomyBurdenProjectionPhase.Pending
                && (!string.IsNullOrEmpty(source.nodeId)
                    || source.infectionBefore != 0f
                    || source.infectionAfter != 0f
                    || source.characterDisplayName != string.Empty || source.publicationKey != string.Empty))
            || (phase == DiseaseAnatomyBurdenProjectionPhase.AnatomyApplied
                && (string.IsNullOrWhiteSpace(source.nodeId)
                    || source.infectionBefore < 0f
                    || source.infectionAfter < source.infectionBefore)))
        {
            throw new InvalidOperationException(
                "Disease anatomy burden projection is invalid.");
        }
        if (phase == DiseaseAnatomyBurdenProjectionPhase.AnatomyApplied)
            _ = new DiseaseAnatomyBurdenAppliedFact(source);
    }

    private static DiseaseAnatomyBurdenProjectionSaveData
        CloneAnatomyBurdenProjection(
            DiseaseAnatomyBurdenProjectionSaveData source) => new()
        {
            phase = source?.phase ?? 0,
            operationId = source?.operationId ?? string.Empty,
            absoluteDay = source?.absoluteDay ?? 0,
            characterId = source?.characterId ?? string.Empty,
            diseaseId = source?.diseaseId ?? string.Empty,
            targetSystem = source?.targetSystem ?? 0,
            severity = source?.severity ?? 0f,
            requestedInfectionBurden = source?.requestedInfectionBurden ?? 0f,
            nodeId = source?.nodeId ?? string.Empty,
            infectionBefore = source?.infectionBefore ?? 0f,
            infectionAfter = source?.infectionAfter ?? 0f,
            attemptCount = source?.attemptCount ?? 0,
            lastFailureCode = source?.lastFailureCode ?? string.Empty,
            characterDisplayName = source?.characterDisplayName ?? string.Empty,
            publicationKey = source?.publicationKey ?? string.Empty
        };

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
        ICollection<PopulationHealthChange> changes,
        Func<CharacterId, DiseaseDefinition, PopulationDiseaseStatModifiers>
            resolveModifiers)
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
            PopulationDiseaseStatModifiers modifiers = ResolveModifiers(
                resolveModifiers,
                characterId,
                disease);
            int contagiousDays = ResolveContagiousDurationDays(
                disease.ContagiousDays,
                modifiers.RecoverySpeed);
            CharacterPopulationHealthSaveData record = RequireCharacter(characterId);
            record.activeDiseases.Add(new ActiveDiseaseSaveData
            {
                diseaseId = disease.Id,
                infectionDay = CurrentAbsoluteDay,
                symptomDay = CurrentAbsoluteDay + disease.IncubationDays,
                recoveryDay = CurrentAbsoluteDay + disease.IncubationDays + contagiousDays,
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
        ICollection<PopulationHealthChange> changes,
        Func<CharacterId, DiseaseDefinition, PopulationDiseaseStatModifiers>
            resolveModifiers)
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
                    PopulationDiseaseStatModifiers modifiers = ResolveModifiers(
                        resolveModifiers,
                        pair.Key,
                        disease);
                    SetImmunity(
                        pair.Value,
                        disease.Id,
                        80f,
                        0.02f,
                        modifiers.ImmunityGain);
                    changes.Add(new PopulationHealthChange(
                        pair.Key, disease.Id, PopulationHealthChangeKind.Recovered,
                        active.severity, disease.TargetSystem));
                }
                else if (active.diagnosed)
                {
                    changes.Add(new PopulationHealthChange(
                        pair.Key, disease.Id, PopulationHealthChangeKind.DailyBodyBurden,
                        active.severity, disease.TargetSystem,
                        CurrentAbsoluteDay));
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

    private void DecayImmunity(
        IDiseaseDefinitionCatalog definitions,
        Func<CharacterId, DiseaseDefinition, PopulationDiseaseStatModifiers>
            resolveModifiers)
    {
        foreach (KeyValuePair<CharacterId, CharacterPopulationHealthSaveData> pair in characters)
        {
            CharacterPopulationHealthSaveData record = pair.Value;
            foreach (DiseaseImmunitySaveData immunity in record.immunity)
            {
                DiseaseDefinition disease = definitions.Require(immunity.diseaseId);
                PopulationDiseaseStatModifiers modifiers = ResolveModifiers(
                    resolveModifiers,
                    pair.Key,
                    disease);
                immunity.value = Math.Max(
                    0f,
                    immunity.value - ResolveDailyImmunityDecay(
                        immunity.dailyDecay,
                        modifiers.ImmunityRetention));
            }
        }
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
        float decay,
        float immunityGain)
    {
        DiseaseImmunitySaveData state = record.immunity.FirstOrDefault(value =>
            string.Equals(value.diseaseId, diseaseId, StringComparison.Ordinal));
        if (state == null)
        {
            state = new DiseaseImmunitySaveData { diseaseId = diseaseId };
            record.immunity.Add(state);
        }
        state.value = Math.Max(
            state.value,
            ResolveImmunityAward(minimum, immunityGain));
        state.dailyDecay = decay;
    }

    public static int ResolveContagiousDurationDays(
        int baseDurationDays,
        float diseaseRecoverySpeed) => Math.Max(
            1,
            (int)Math.Ceiling(
                Math.Max(1, baseDurationDays)
                / Math.Max(0.05f, diseaseRecoverySpeed)));

    public static float ResolveImmunityAward(
        float baseAward,
        float immunityGain) => Math.Clamp(
            Math.Max(0f, baseAward) * Math.Max(0.05f, immunityGain),
            0f,
            100f);

    public static float ResolveDailyImmunityDecay(
        float baseDailyDecay,
        float immunityRetention) => Math.Max(0f, baseDailyDecay)
            / Math.Max(0.05f, immunityRetention);

    private static PopulationDiseaseStatModifiers ResolveModifiers(
        Func<CharacterId, DiseaseDefinition, PopulationDiseaseStatModifiers>
            resolveModifiers,
        CharacterId characterId,
        DiseaseDefinition disease) => resolveModifiers == null
            ? PopulationDiseaseStatModifiers.Neutral
            : resolveModifiers(characterId, disease);

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

    private static void ValidatePendingExposureFact(
        PopulationDiseaseExposurePendingFactSaveData source,
        long expectedRevision,
        IDiseaseDefinitionCatalog definitions)
    {
        if (source == null || definitions == null)
            throw new InvalidOperationException(
                "Population disease-exposure pending fact is missing.");
        var characterId = new CharacterId(source.characterId);
        DiseaseDefinition disease = definitions.Require(source.diseaseId);
        var route = (DiseaseTransmissionRoute)source.route;
        int routeBits = source.route;
        IReadOnlyList<string> fieldResponses =
            source.fieldResponseIds ?? new List<string>();
        bool canonicalFieldResponses = fieldResponses.All(IsCanonicalRequired)
            && fieldResponses.Distinct(StringComparer.Ordinal).Count()
                == fieldResponses.Count;
        bool exactDisease = string.Equals(
                source.diseaseDisplayName,
                disease.DisplayName,
                StringComparison.Ordinal)
            && source.diseaseRoutes == (int)disease.Routes
            && source.incubationDays == disease.IncubationDays
            && source.contagiousDays == disease.ContagiousDays
            && source.baseInfectionProbability.Equals(
                disease.BaseInfectionProbability)
            && source.baseSeverity.Equals(disease.BaseSeverity)
            && source.targetSystem == (int)disease.TargetSystem
            && source.vaccineAllowed == disease.VaccineAllowed
            && source.chronic == disease.Chronic
            && string.Equals(
                source.symptomProfileId,
                disease.SymptomProfileId,
                StringComparison.Ordinal)
            && fieldResponses.SequenceEqual(disease.FieldResponseIds);
        if (!characterId.IsValid
            || !IsCanonicalRequired(source.characterId)
            || !IsCanonicalRequired(source.characterDisplayName)
            || !IsCanonicalRequired(source.diseaseId)
            || !IsCanonicalRequired(source.diseaseDisplayName)
            || !IsCanonicalRequired(source.symptomProfileId)
            || !canonicalFieldResponses
            || !exactDisease
            || route == DiseaseTransmissionRoute.None
            || (routeBits & (routeBits - 1)) != 0
            || (disease.Routes & route) == 0
            || !float.IsFinite(source.exposureHours)
            || source.exposureHours <= 0f
            || !float.IsFinite(source.environmentCoefficient)
            || source.environmentCoefficient <= 0f
            || source.absoluteDay < 1
            || source.ownerRevision != expectedRevision
            || source.ownerRevision < 1L
            || !IsCanonicalRequired(source.sourceKind)
            || !IsCanonicalRequired(source.sourceId))
        {
            throw new InvalidOperationException(
                "Population disease-exposure pending fact is invalid or disagrees with its owner revision.");
        }
    }

    private static PopulationDiseaseExposurePendingFactSaveData
        ClonePendingExposureFact(
            PopulationDiseaseExposurePendingFactSaveData source) => new()
        {
            characterId = source.characterId,
            characterDisplayName = source.characterDisplayName,
            diseaseId = source.diseaseId,
            diseaseDisplayName = source.diseaseDisplayName,
            diseaseRoutes = source.diseaseRoutes,
            incubationDays = source.incubationDays,
            contagiousDays = source.contagiousDays,
            baseInfectionProbability = source.baseInfectionProbability,
            baseSeverity = source.baseSeverity,
            targetSystem = source.targetSystem,
            vaccineAllowed = source.vaccineAllowed,
            chronic = source.chronic,
            symptomProfileId = source.symptomProfileId,
            fieldResponseIds = new List<string>(
                source.fieldResponseIds ?? new List<string>()),
            route = source.route,
            exposureHours = source.exposureHours,
            environmentCoefficient = source.environmentCoefficient,
            absoluteDay = source.absoluteDay,
            ownerRevision = source.ownerRevision,
            sourceKind = source.sourceKind,
            sourceId = source.sourceId
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

    private static DiseaseFieldResponseCommitSaveData CloneFieldResponseCommit(
        DiseaseFieldResponseCommitSaveData source)
    {
        source ??= new DiseaseFieldResponseCommitSaveData();
        return new DiseaseFieldResponseCommitSaveData
        {
            phase = source.phase,
            operationSequence = source.operationSequence,
            operationId = source.operationId ?? string.Empty,
            reasonCode = source.reasonCode ?? string.Empty,
            characterId = source.characterId ?? string.Empty,
            diseaseId = source.diseaseId ?? string.Empty,
            responseId = source.responseId ?? string.Empty,
            facilityInstanceId = source.facilityInstanceId ?? string.Empty,
            outputGridX = source.outputGridX,
            outputGridY = source.outputGridY,
            itemId = source.itemId ?? string.Empty,
            quantity = source.quantity,
            severityReduction = source.severityReduction,
            sourceStackIds = new List<string>(
                source.sourceStackIds ?? new List<string>()),
            inputMassGrams = source.inputMassGrams,
            commitId = source.commitId ?? string.Empty
        };
    }

    private static VaccinationCommitSaveData CloneVaccinationCommit(
        VaccinationCommitSaveData source)
    {
        source ??= new VaccinationCommitSaveData();
        return new VaccinationCommitSaveData
        {
            phase = source.phase,
            operationSequence = source.operationSequence,
            operationId = source.operationId ?? string.Empty,
            reasonCode = source.reasonCode ?? string.Empty,
            characterId = source.characterId ?? string.Empty,
            diseaseId = source.diseaseId ?? string.Empty,
            facilityInstanceId = source.facilityInstanceId ?? string.Empty,
            outputGridX = source.outputGridX,
            outputGridY = source.outputGridY,
            itemId = source.itemId ?? string.Empty,
            quantity = source.quantity,
            sourceStackIds = new List<string>(
                source.sourceStackIds ?? new List<string>()),
            inputMassGrams = source.inputMassGrams,
            commitId = source.commitId ?? string.Empty
        };
    }

    private static void ValidateFieldResponseCommit(
        DiseaseFieldResponseCommitSaveData pending,
        int nextSequence,
        IDiseaseDefinitionCatalog definitions)
    {
        pending ??= new DiseaseFieldResponseCommitSaveData();
        DiseaseFieldResponseCommitPhase phase =
            (DiseaseFieldResponseCommitPhase)pending.phase;
        if (phase == DiseaseFieldResponseCommitPhase.None)
        {
            if (pending.operationSequence != 0
                || !string.IsNullOrEmpty(pending.operationId)
                || !string.IsNullOrEmpty(pending.reasonCode)
                || !string.IsNullOrEmpty(pending.characterId)
                || !string.IsNullOrEmpty(pending.diseaseId)
                || !string.IsNullOrEmpty(pending.responseId)
                || !string.IsNullOrEmpty(pending.facilityInstanceId)
                || pending.outputGridX != 0
                || pending.outputGridY != 0
                || !string.IsNullOrEmpty(pending.itemId)
                || pending.quantity != 0
                || pending.severityReduction != 0f
                || (pending.sourceStackIds?.Count ?? 0) != 0
                || pending.inputMassGrams != 0L
                || !string.IsNullOrEmpty(pending.commitId))
            {
                throw new InvalidOperationException(
                    "Population-health empty field-response provenance is invalid.");
            }
            return;
        }

        if (phase is not (DiseaseFieldResponseCommitPhase.IntentRecorded
                or DiseaseFieldResponseCommitPhase.OutcomePublished)
            || pending.operationSequence != nextSequence
            || pending.operationSequence <= 0
            || !IsCanonicalRequired(pending.operationId)
            || !IsCanonicalRequired(pending.reasonCode)
            || !new CharacterId(pending.characterId).IsValid
            || !IsCanonicalRequired(pending.responseId)
            || !IsCanonicalRequired(pending.facilityInstanceId)
            || !IsCanonicalRequired(pending.itemId)
            || pending.quantity <= 0
            || float.IsNaN(pending.severityReduction)
            || float.IsInfinity(pending.severityReduction)
            || pending.severityReduction <= 0f)
        {
            throw new InvalidOperationException(
                "Population-health field-response intent is invalid.");
        }
        DiseaseDefinition disease = definitions.Require(pending.diseaseId);
        if (!disease.FieldResponseIds.Contains(
                pending.responseId,
                StringComparer.Ordinal))
        {
            throw new InvalidOperationException(
                "Population-health field-response is not authored for its disease.");
        }

        string expectedOperation =
            $"disease-field-response:{pending.characterId}:"
            + $"{pending.diseaseId}:{pending.responseId}:"
            + $"{pending.operationSequence:D8}";
        if (!string.Equals(
                pending.operationId,
                expectedOperation,
                StringComparison.Ordinal)
            || !string.Equals(
                pending.reasonCode,
                "disease-field-response-consumed",
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Population-health field-response identity is invalid.");
        }

        IReadOnlyList<string> sources = pending.sourceStackIds
            ?? new List<string>();
        if (phase == DiseaseFieldResponseCommitPhase.IntentRecorded)
        {
            if (sources.Count != 0
                || pending.inputMassGrams != 0L
                || !string.IsNullOrEmpty(pending.commitId))
            {
                throw new InvalidOperationException(
                    "Population-health field-response intent contains terminal provenance.");
            }
            return;
        }

        if (sources.Count == 0
            || sources.Any(value => !IsCanonicalRequired(value))
            || sources.Distinct(StringComparer.Ordinal).Count() != sources.Count
            || !sources.SequenceEqual(
                sources.OrderBy(value => value, StringComparer.Ordinal),
                StringComparer.Ordinal)
            || pending.inputMassGrams <= 0L)
        {
            throw new InvalidOperationException(
                "Population-health field-response receipt provenance is invalid.");
        }
        const int sinkDispositionKindCode = 3;
        string expectedCommit =
            $"physical-batch-disposition:{sinkDispositionKindCode}:"
            + $"{pending.operationId}:{pending.quantity}:"
            + pending.inputMassGrams;
        if (!string.Equals(
                pending.commitId,
                expectedCommit,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Population-health field-response commit identity is invalid.");
        }
    }

    private static void ValidateVaccinationCommit(
        VaccinationCommitSaveData pending,
        int nextSequence,
        IDiseaseDefinitionCatalog definitions)
    {
        pending ??= new VaccinationCommitSaveData();
        VaccinationCommitPhase phase = (VaccinationCommitPhase)pending.phase;
        if (phase == VaccinationCommitPhase.None)
        {
            if (pending.operationSequence != 0
                || !string.IsNullOrEmpty(pending.operationId)
                || !string.IsNullOrEmpty(pending.reasonCode)
                || !string.IsNullOrEmpty(pending.characterId)
                || !string.IsNullOrEmpty(pending.diseaseId)
                || !string.IsNullOrEmpty(pending.facilityInstanceId)
                || pending.outputGridX != 0
                || pending.outputGridY != 0
                || !string.IsNullOrEmpty(pending.itemId)
                || pending.quantity != 0
                || (pending.sourceStackIds?.Count ?? 0) != 0
                || pending.inputMassGrams != 0L
                || !string.IsNullOrEmpty(pending.commitId))
            {
                throw new InvalidOperationException(
                    "Population-health empty vaccination provenance is invalid.");
            }
            return;
        }

        if (phase is not (VaccinationCommitPhase.IntentRecorded
                or VaccinationCommitPhase.OutcomePublished)
            || pending.operationSequence != nextSequence
            || pending.operationSequence <= 0
            || !IsCanonicalRequired(pending.operationId)
            || !IsCanonicalRequired(pending.reasonCode)
            || !new CharacterId(pending.characterId).IsValid
            || !IsCanonicalRequired(pending.diseaseId)
            || !IsCanonicalRequired(pending.facilityInstanceId)
            || !IsCanonicalRequired(pending.itemId)
            || pending.quantity != 1)
        {
            throw new InvalidOperationException(
                "Population-health vaccination intent is invalid.");
        }

        DiseaseDefinition disease = definitions.Require(pending.diseaseId);
        if (!disease.VaccineAllowed)
        {
            throw new InvalidOperationException(
                "Population-health vaccination disease disallows vaccines.");
        }
        string expectedOperation =
            $"vaccination:{pending.characterId}:{pending.diseaseId}:"
            + $"{pending.operationSequence:D8}";
        if (!string.Equals(
                pending.operationId,
                expectedOperation,
                StringComparison.Ordinal)
            || !string.Equals(
                pending.reasonCode,
                "vaccination-dose-administered",
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Population-health vaccination identity is invalid.");
        }

        IReadOnlyList<string> sources = pending.sourceStackIds
            ?? new List<string>();
        if (phase == VaccinationCommitPhase.IntentRecorded)
        {
            if (sources.Count != 0
                || pending.inputMassGrams != 0L
                || !string.IsNullOrEmpty(pending.commitId))
            {
                throw new InvalidOperationException(
                    "Population-health vaccination intent contains terminal provenance.");
            }
            return;
        }

        if (sources.Count == 0
            || sources.Any(value => !IsCanonicalRequired(value))
            || sources.Distinct(StringComparer.Ordinal).Count() != sources.Count
            || !sources.SequenceEqual(
                sources.OrderBy(value => value, StringComparer.Ordinal),
                StringComparer.Ordinal)
            || pending.inputMassGrams <= 0L)
        {
            throw new InvalidOperationException(
                "Population-health vaccination receipt provenance is invalid.");
        }
        const int sinkDispositionKindCode = 3;
        string expectedCommit =
            $"physical-batch-disposition:{sinkDispositionKindCode}:"
            + $"{pending.operationId}:{pending.quantity}:"
            + pending.inputMassGrams;
        if (!string.Equals(
                pending.commitId,
                expectedCommit,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Population-health vaccination commit identity is invalid.");
        }
    }

    private static bool IsCanonicalRequired(string value) =>
        !string.IsNullOrWhiteSpace(value)
        && string.Equals(value, value.Trim(), StringComparison.Ordinal);

    private static string ExposureKey(CharacterId id, string diseaseId) =>
        id.Value + "\n" + (diseaseId?.Trim() ?? string.Empty);
    private static double ClampUnit(double value) =>
        Math.Max(0d, Math.Min(0.999999999999d, value));
}

public interface IPopulationHealthService
{
    int Version { get; }
    long ExposureRevision { get; }
    IReadOnlyList<PopulationDiseaseExposurePendingFactSaveData>
        PendingExposureFacts { get; }
    void RecordExposure(
        string diseaseId,
        IReadOnlyList<PopulationExposureTarget> targets,
        float exposureHours,
        float environmentCoefficient);
    void RecordExposureWithPendingFact(
        string diseaseId,
        IReadOnlyList<PopulationExposureTarget> targets,
        float exposureHours,
        float environmentCoefficient,
        PopulationDiseaseExposurePendingFactSaveData pendingFact);
    void CompletePendingExposureFact(long ownerRevision);
    IReadOnlyList<PopulationHealthChange> AdvanceToDay(int absoluteDay);
    void Vaccinate(CharacterId characterId, string diseaseId);
    PopulationHealthChange ApplyEnvironmentalCondition(CharacterId characterId, string diseaseId);
    void RemoveEnvironmentalCondition(CharacterId characterId, string diseaseId);
    int RemovePendingExposures(CharacterId characterId);
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

public interface IDiseaseSymptomEffectQuery
{
    IReadOnlyList<DiseaseSymptomEffectSnapshot> GetActiveSymptoms(
        CharacterId characterId);
    float GetWorkSpeedMultiplier(CharacterId characterId);
    float GetMoveSpeedMultiplier(CharacterId characterId);
}

public interface IPopulationHealthPersistence
{
    PopulationHealthWorldSaveData Capture();
    PopulationHealthAggregateState PrepareRestore(PopulationHealthWorldSaveData data);
    void PublishRestore(PopulationHealthAggregateState candidate);
}
