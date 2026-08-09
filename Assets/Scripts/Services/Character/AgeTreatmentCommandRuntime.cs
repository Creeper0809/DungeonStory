using System;
using System.Linq;

public enum AgeTreatmentKind
{
    OrganRegeneration = 0,
    BloodRejuvenation = 1,
    RuneHibernation = 2,
    WholeBodyRegeneration = 3,
    TemporalStasis = 4
}

public readonly struct AgeTreatmentOrderRequest
{
    public AgeTreatmentOrderRequest(
        CharacterId patientId,
        AgeTreatmentKind treatment,
        string preferredDoctorId,
        string facilityInstanceId)
    {
        PatientId = patientId;
        Treatment = treatment;
        PreferredDoctorId = preferredDoctorId?.Trim() ?? string.Empty;
        FacilityInstanceId = facilityInstanceId?.Trim() ?? string.Empty;
    }

    public CharacterId PatientId { get; }
    public AgeTreatmentKind Treatment { get; }
    public string PreferredDoctorId { get; }
    public string FacilityInstanceId { get; }
}

public interface IAgeTreatmentCommand
{
    bool TryCreateOrder(
        AgeTreatmentOrderRequest request,
        out SurgeryOrder order,
        out DomainFailure failure);
}

/// <summary>
/// Routes every age treatment through the normal surgery aggregate. The
/// resulting order owns patient admission, hauling, clinician work,
/// environment checks, material consumption and save/restore.
/// </summary>
public sealed class AgeTreatmentCommandRuntime : IAgeTreatmentCommand
{
    private readonly ISurgeryCommandService surgery;
    private readonly ICharacterWorldQuery characters;
    private readonly ICharacterLifeQuery life;
    private readonly ICharacterLifeDefinitionCatalog lifeDefinitions;
    private readonly IBuildingWorldQuery buildings;
    private readonly IGameCalendar calendar;

    public AgeTreatmentCommandRuntime(
        ISurgeryCommandService surgery,
        ICharacterWorldQuery characters,
        ICharacterLifeQuery life,
        ICharacterLifeDefinitionCatalog lifeDefinitions,
        IBuildingWorldQuery buildings,
        IGameCalendar calendar)
    {
        this.surgery = surgery ?? throw new ArgumentNullException(nameof(surgery));
        this.characters = characters ?? throw new ArgumentNullException(nameof(characters));
        this.life = life ?? throw new ArgumentNullException(nameof(life));
        this.lifeDefinitions = lifeDefinitions
            ?? throw new ArgumentNullException(nameof(lifeDefinitions));
        this.buildings = buildings ?? throw new ArgumentNullException(nameof(buildings));
        this.calendar = calendar ?? throw new ArgumentNullException(nameof(calendar));
    }

    public bool TryCreateOrder(
        AgeTreatmentOrderRequest request,
        out SurgeryOrder order,
        out DomainFailure failure)
    {
        order = null;
        if (!request.PatientId.IsValid
            || !life.TryGet(request.PatientId, out CharacterLifeRecord record))
        {
            failure = new DomainFailure(
                FailureCode.AgeTreatmentCharacterMissing,
                request.PatientId.Value);
            return false;
        }

        CharacterActor patient = characters.Characters.FirstOrDefault(candidate =>
            candidate != null
            && !candidate.IsDead
            && CharacterPersistentIdentity.TryGet(candidate, out CharacterId id)
            && id.Equals(request.PatientId));
        if (patient == null)
        {
            failure = new DomainFailure(
                FailureCode.AgeTreatmentCharacterMissing,
                request.PatientId.Value);
            return false;
        }

        TreatmentContract contract = Resolve(request.Treatment);
        BuildableObject facility = buildings.Buildings.FirstOrDefault(candidate =>
            candidate != null
            && !candidate.isDestroy
            && string.Equals(
                candidate.PersistentInstanceId.Value,
                request.FacilityInstanceId,
                StringComparison.Ordinal));
        if (facility == null || facility.BuildingData?.id != contract.BuildingId)
        {
            failure = new DomainFailure(
                FailureCode.SurgeryFacilityUnavailable,
                request.FacilityInstanceId,
                $"building:{contract.BuildingId}");
            return false;
        }

        if (request.Treatment == AgeTreatmentKind.BloodRejuvenation
            && !CanReceiveBloodRejuvenation(record, out failure))
        {
            return false;
        }

        SurgicalSubjectRef subject = SurgeryRuntimeSupport.CreateCharacterSubject(
            patient,
            automaticEmergencyDefault: false);
        subject.willing = true;
        return surgery.TrySchedule(
            subject,
            contract.ProcedureId,
            targetNodeId: string.Empty,
            selectedPartInstanceId: string.Empty,
            request.PreferredDoctorId,
            request.FacilityInstanceId,
            out order,
            out failure);
    }

    private bool CanReceiveBloodRejuvenation(
        CharacterLifeRecord record,
        out DomainFailure failure)
    {
        SpeciesLifeHistoryDefinition history =
            lifeDefinitions.RequireLifeHistory(record.PhenotypeSpeciesId);
        int minimumAgeYears = history.AdultAgeYears + 5;
        if (record.BiologicalAgeDayUnits
            <= minimumAgeYears * GameCalendarRules.DaysPerYear)
        {
            failure = new DomainFailure(
                FailureCode.AgeTreatmentTooYoung,
                record.CharacterId.Value,
                minimumAgeYears.ToString());
            return false;
        }

        long elapsed = (long)calendar.Day
            - record.LastBloodRejuvenationAbsoluteDay;
        if (record.LastBloodRejuvenationAbsoluteDay != int.MinValue
            && elapsed < GameCalendarRules.DaysPerYear)
        {
            failure = new DomainFailure(
                FailureCode.AgeTreatmentCooldownActive,
                record.CharacterId.Value,
                Math.Max(0L, GameCalendarRules.DaysPerYear - elapsed).ToString());
            return false;
        }

        failure = DomainFailure.None;
        return true;
    }

    private static TreatmentContract Resolve(AgeTreatmentKind treatment) =>
        treatment switch
        {
            AgeTreatmentKind.OrganRegeneration =>
                new TreatmentContract("procedure:organ-regeneration", 8868),
            AgeTreatmentKind.BloodRejuvenation =>
                new TreatmentContract("procedure:blood-rejuvenation", 8869),
            AgeTreatmentKind.RuneHibernation =>
                new TreatmentContract("procedure:rune-hibernation", 8870),
            AgeTreatmentKind.WholeBodyRegeneration =>
                new TreatmentContract("procedure:whole-body-regeneration", 8871),
            AgeTreatmentKind.TemporalStasis =>
                new TreatmentContract("procedure:temporal-stasis", 8872),
            _ => throw new ArgumentOutOfRangeException(nameof(treatment))
        };

    private readonly struct TreatmentContract
    {
        internal TreatmentContract(string procedureId, int buildingId)
        {
            ProcedureId = procedureId;
            BuildingId = buildingId;
        }

        internal string ProcedureId { get; }
        internal int BuildingId { get; }
    }
}
