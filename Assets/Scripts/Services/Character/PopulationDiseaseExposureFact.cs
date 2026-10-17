using System.Collections.Generic;

/// <summary>
/// Immutable physical disease-exposure transition. All authored values used by
/// the durable observer are copied; no catalog, scene object, or lease is held.
/// </summary>
public sealed class PopulationDiseaseExposureFact
{
    private readonly DiseaseDefinition disease;

    public PopulationDiseaseExposureFact(
        CharacterId characterId,
        string characterDisplayName,
        DiseaseDefinition disease,
        DiseaseTransmissionRoute route,
        float exposureHours,
        float environmentCoefficient,
        int absoluteDay,
        long ownerRevision,
        string sourceKind,
        string sourceId)
    {
        CharacterId = characterId;
        CharacterDisplayName = characterDisplayName?.Trim() ?? string.Empty;
        this.disease = CopyDisease(disease);
        Route = route;
        ExposureHours = exposureHours;
        EnvironmentCoefficient = environmentCoefficient;
        AbsoluteDay = absoluteDay;
        OwnerRevision = ownerRevision;
        SourceKind = sourceKind?.Trim() ?? string.Empty;
        SourceId = sourceId?.Trim() ?? string.Empty;
    }

    public CharacterId CharacterId { get; }
    public string CharacterDisplayName { get; }
    public DiseaseDefinition Disease => CopyDisease(disease);
    public DiseaseTransmissionRoute Route { get; }
    public float ExposureHours { get; }
    public float EnvironmentCoefficient { get; }
    public int AbsoluteDay { get; }
    public long OwnerRevision { get; }
    public string SourceKind { get; }
    public string SourceId { get; }

    public PopulationDiseaseExposurePendingFactSaveData CapturePending() => new()
    {
        characterId = CharacterId.Value,
        characterDisplayName = CharacterDisplayName,
        diseaseId = disease.Id,
        diseaseDisplayName = disease.DisplayName,
        diseaseRoutes = (int)disease.Routes,
        incubationDays = disease.IncubationDays,
        contagiousDays = disease.ContagiousDays,
        baseInfectionProbability = disease.BaseInfectionProbability,
        baseSeverity = disease.BaseSeverity,
        targetSystem = (int)disease.TargetSystem,
        vaccineAllowed = disease.VaccineAllowed,
        chronic = disease.Chronic,
        symptomProfileId = disease.SymptomProfileId,
        fieldResponseIds = new List<string>(disease.FieldResponseIds),
        route = (int)Route,
        exposureHours = ExposureHours,
        environmentCoefficient = EnvironmentCoefficient,
        absoluteDay = AbsoluteDay,
        ownerRevision = OwnerRevision,
        sourceKind = SourceKind,
        sourceId = SourceId
    };

    public static PopulationDiseaseExposureFact Restore(
        PopulationDiseaseExposurePendingFactSaveData source)
    {
        if (source == null)
            return null;
        return new PopulationDiseaseExposureFact(
            new CharacterId(source.characterId),
            source.characterDisplayName,
            new DiseaseDefinition(
                source.diseaseId,
                source.diseaseDisplayName,
                (DiseaseTransmissionRoute)source.diseaseRoutes,
                source.incubationDays,
                source.contagiousDays,
                source.baseInfectionProbability,
                source.baseSeverity,
                (DiseaseTargetSystem)source.targetSystem,
                source.vaccineAllowed,
                source.chronic,
                source.symptomProfileId,
                source.fieldResponseIds),
            (DiseaseTransmissionRoute)source.route,
            source.exposureHours,
            source.environmentCoefficient,
            source.absoluteDay,
            source.ownerRevision,
            source.sourceKind,
            source.sourceId);
    }

    private static DiseaseDefinition CopyDisease(in DiseaseDefinition source) =>
        new(
            source.Id,
            source.DisplayName,
            source.Routes,
            source.IncubationDays,
            source.ContagiousDays,
            source.BaseInfectionProbability,
            source.BaseSeverity,
            source.TargetSystem,
            source.VaccineAllowed,
            source.Chronic,
            source.SymptomProfileId,
            source.FieldResponseIds);
}
