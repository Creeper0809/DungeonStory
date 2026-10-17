using System;
using System.Collections.Generic;
using System.Linq;

public enum FamilyPlanningPolicy
{
    Off = 0,
    Planned = 1,
    Allowed = 2
}

public enum ReproductionProcessStatus
{
    Planned = 0,
    Active = 1,
    WaitingForEnvironment = 2,
    WaitingForEmergencyExtraction = 3,
    Completed = 4,
    Failed = 5
}

public enum ReproductionFailureCode
{
    None = 0,
    KinshipRestricted = 1,
    IncompatibleRole = 2,
    CrossLineageIncubatorRequired = 3,
    GolemGeneticHybridForbidden = 4,
    ConceptionFailed = 5,
    Miscarriage = 6,
    IncubationEnvironmentFailed = 7,
    CarrierDied = 8,
    EmergencyExtractionExpired = 9
}

public static class ReproductionStableIdSyntax
{
    public static bool IsValid(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > 192)
            return false;

        for (int index = 0; index < value.Length; index++)
        {
            char current = value[index];
            bool valid = current >= 'a' && current <= 'z'
                || current >= 'A' && current <= 'Z'
                || current >= '0' && current <= '9'
                || current == '.'
                || current == ':'
                || current == '_'
                || current == '-'
                || current == '/';
            if (!valid)
                return false;
        }

        return true;
    }
}

public readonly struct ReproductionDefinition
{
    public ReproductionDefinition(
        CharacterSpeciesId speciesId,
        ReproductionMode mode,
        float baseSuccessChance,
        float viableTemperatureMinimum,
        float viableTemperatureMaximum,
        IReadOnlyList<ReproductionPhaseDefinition> phases)
    {
        SpeciesId = speciesId;
        Mode = mode;
        BaseSuccessChance = baseSuccessChance;
        ViableTemperatureMinimum = viableTemperatureMinimum;
        ViableTemperatureMaximum = viableTemperatureMaximum;
        Phases = phases ?? Array.Empty<ReproductionPhaseDefinition>();
    }

    public CharacterSpeciesId SpeciesId { get; }
    public ReproductionMode Mode { get; }
    public float BaseSuccessChance { get; }
    public float ViableTemperatureMinimum { get; }
    public float ViableTemperatureMaximum { get; }
    public IReadOnlyList<ReproductionPhaseDefinition> Phases { get; }
    public int TotalDurationDays => Phases.Sum(value => value.durationDays);
}

public interface IReproductionDefinitionCatalog
{
    ReproductionDefinition RequireReproduction(CharacterSpeciesId speciesId);
}

[Serializable]
public sealed class InnateAptitudeSaveData
{
    public string skillId = string.Empty;
    public int value;
}

[Serializable]
public sealed class ReproductionProcessSaveData
{
    public string processId = string.Empty;
    public string firstParentId = string.Empty;
    public string secondParentId = string.Empty;
    public string carrierId = string.Empty;
    public string phenotypeSpeciesId = string.Empty;
    public ReproductionMode mode;
    public ReproductionProcessStatus status;
    public ReproductionFailureCode failure;
    public int startedAbsoluteDay;
    public int currentPhaseIndex;
    public int currentPhaseElapsedDays;
    public int totalProgressDays;
    public int consecutiveUnsafeTemperatureDays;
    public int carrierDeathAbsoluteDay;
    public bool emergencyExtracted;
    public bool crossLineageIncubatorUsed;
    public bool fertilityTreatmentUsed;
    public string supportFacilityInstanceId = string.Empty;
    public bool resultPublished;
    public string resultCharacterId = string.Empty;
    public long transitionRevision;
    public string lastPublicationKey = string.Empty;
    public string lastPublicationId = string.Empty;
    public string lastPublicationFingerprint = string.Empty;
    public List<ReproductionPendingTransitionData> pendingTransitions = new();
    public ReproductionStartTransactionSaveData pendingStart;
    public ReproductionBirthTransactionSaveData pendingBirth;
    public List<string> expressedTraitIds = new();
    public List<string> latentTraitIds = new();
    public List<InnateAptitudeSaveData> innateAptitudes = new();
}

public enum ReproductionStartTransactionPhase
{
    None = 0,
    Prepared = 1,
    MaterialsConsumed = 2,
    ObservationCommitted = 3
}

[Serializable]
public sealed class ReproductionStartCostSaveData
{
    public string stackId = string.Empty;
    public string itemId = string.Empty;
    public int quantity;
    public int quantityBefore;
    public int quantityAfter;
}

[Serializable]
public sealed class ReproductionStartTransactionSaveData
{
    public ReproductionStartTransactionPhase phase;
    public string operationId = string.Empty;
    public int absoluteDay;
    public bool useFertilityTreatment;
    public List<ReproductionStartCostSaveData> costs = new();
    public string publicationKey = string.Empty;
    public string publicationId = string.Empty;
    public string publicationFingerprint = string.Empty;
}

public enum ReproductionBirthTransactionPhase
{
    None = 0,
    Prepared = 1,
    LifeRegistered = 2,
    NarrativeRegistered = 3,
    KinshipRegistered = 4,
    ActorPublished = 5,
    PopulationRegistered = 6,
    HousingResolutionRecorded = 7,
    PublicationPrepared = 8,
    ObservationCommitted = 9
}

[Serializable]
public sealed class ReproductionBirthTransactionSaveData
{
    public ReproductionBirthTransactionPhase phase;
    public string operationId = string.Empty;
    public string childCharacterId = string.Empty;
    public int absoluteDay;
    public long publicationRevision;
    public string publicationKey = string.Empty;
    public string publicationId = string.Empty;
    public string publicationFingerprint = string.Empty;
}

[Serializable]
public sealed class ReproductionCarrierDeathNotificationSaveData
{
    public string notificationId = string.Empty;
    public string carrierId = string.Empty;
    public int absoluteDay;
    public CharacterDeathCauseCode cause;
    public int locationX;
    public int locationY;
    public string parentPublicationKey = string.Empty;
}

[Serializable]
public sealed class FamilyPlanningPolicyPendingChangeSaveData
{
    public FamilyPlanningPolicy before;
    public FamilyPlanningPolicy after;
    public long beforeRevision;
    public long afterRevision;
    public string source = string.Empty;
    public int absoluteDay;
}

[Serializable]
public sealed class ReproductionWorldSaveData
{
    public const int CurrentVersion = 9;
    public int version = CurrentVersion;
    public FamilyPlanningPolicy familyPlanningPolicy = FamilyPlanningPolicy.Planned;
    public long familyPlanningPolicyRevision;
    public List<FamilyPlanningPolicyPendingChangeSaveData>
        pendingFamilyPlanningPolicyChanges = new();
    public int lastAllowedPolicyEvaluationDay;
    public List<ReproductionProcessSaveData> processes = new();
    public List<ReproductionCarrierDeathNotificationSaveData>
        pendingCarrierDeaths = new();
}

public readonly struct ReproductionDailyContext
{
    public ReproductionDailyContext(
        int absoluteDay,
        float carrierHealth,
        float carrierNutrition,
        float environmentTemperature,
        float fertilityAgeCoefficient = 1f,
        float gestationStabilityMultiplier = 1f)
    {
        AbsoluteDay = absoluteDay;
        CarrierHealth = carrierHealth;
        CarrierNutrition = carrierNutrition;
        EnvironmentTemperature = environmentTemperature;
        FertilityAgeCoefficient = Math.Clamp(fertilityAgeCoefficient, 0f, 1f);
        GestationStabilityMultiplier = Math.Clamp(
            gestationStabilityMultiplier,
            0.5f,
            1.5f);
    }
    public int AbsoluteDay { get; }
    public float CarrierHealth { get; }
    public float CarrierNutrition { get; }
    public float EnvironmentTemperature { get; }
    public float FertilityAgeCoefficient { get; }
    public float GestationStabilityMultiplier { get; }
}

public sealed class ReproductionProcess
{
    private readonly ReproductionDefinition definition;

    public ReproductionProcess(
        string processId,
        CharacterId firstParentId,
        CharacterId secondParentId,
        CharacterId carrierId,
        CharacterSpeciesId phenotypeSpeciesId,
        ReproductionDefinition definition,
        int startedAbsoluteDay,
        bool crossLineageIncubatorUsed,
        IEnumerable<string> expressedTraitIds,
        IEnumerable<string> latentTraitIds,
        IEnumerable<InnateAptitudeSaveData> aptitudes)
        : this(
            processId,
            firstParentId,
            secondParentId,
            carrierId,
            phenotypeSpeciesId,
            definition,
            startedAbsoluteDay,
            crossLineageIncubatorUsed,
            string.Empty,
            expressedTraitIds,
            latentTraitIds,
            aptitudes,
            fertilityTreatmentUsed: false)
    {
    }

    public ReproductionProcess(
        string processId,
        CharacterId firstParentId,
        CharacterId secondParentId,
        CharacterId carrierId,
        CharacterSpeciesId phenotypeSpeciesId,
        ReproductionDefinition definition,
        int startedAbsoluteDay,
        bool crossLineageIncubatorUsed,
        string supportFacilityInstanceId,
        IEnumerable<string> expressedTraitIds,
        IEnumerable<string> latentTraitIds,
        IEnumerable<InnateAptitudeSaveData> aptitudes,
        bool startActive = true,
        bool fertilityTreatmentUsed = false)
    {
        if (string.IsNullOrWhiteSpace(processId) || !firstParentId.IsValid
            || definition.Mode != ReproductionMode.GolemAssembly && !secondParentId.IsValid
            || !phenotypeSpeciesId.IsValid || startedAbsoluteDay < 1)
            throw new ArgumentException("Reproduction process identity is incomplete.");
        ProcessId = processId.Trim();
        FirstParentId = firstParentId;
        SecondParentId = secondParentId;
        CarrierId = carrierId;
        PhenotypeSpeciesId = phenotypeSpeciesId;
        this.definition = definition;
        StartedAbsoluteDay = startedAbsoluteDay;
        CrossLineageIncubatorUsed = crossLineageIncubatorUsed;
        FertilityTreatmentUsed = fertilityTreatmentUsed;
        SupportFacilityInstanceId = supportFacilityInstanceId?.Trim()
            ?? string.Empty;
        ExpressedTraitIds = NormalizeTraits(expressedTraitIds, 4);
        LatentTraitIds = NormalizeTraits(latentTraitIds, 2);
        InnateAptitudes = (aptitudes ?? Array.Empty<InnateAptitudeSaveData>())
            .Where(value => value != null && !string.IsNullOrWhiteSpace(value.skillId))
            .OrderBy(value => value.skillId, StringComparer.Ordinal)
            .ToArray();
        Status = startActive
            ? ReproductionProcessStatus.Active
            : ReproductionProcessStatus.Planned;
    }

    public string ProcessId { get; }
    public CharacterId FirstParentId { get; }
    public CharacterId SecondParentId { get; }
    public CharacterId CarrierId { get; }
    public CharacterSpeciesId PhenotypeSpeciesId { get; }
    public ReproductionMode Mode => definition.Mode;
    public ReproductionProcessStatus Status { get; private set; }
    public ReproductionFailureCode Failure { get; private set; }
    public int StartedAbsoluteDay { get; }
    public int CurrentPhaseIndex { get; private set; }
    public int CurrentPhaseElapsedDays { get; private set; }
    public int TotalProgressDays { get; private set; }
    public int ConsecutiveUnsafeTemperatureDays { get; private set; }
    public int CarrierDeathAbsoluteDay { get; private set; }
    public bool EmergencyExtracted { get; private set; }
    public bool CrossLineageIncubatorUsed { get; }
    public bool FertilityTreatmentUsed { get; private set; }
    public string SupportFacilityInstanceId { get; }
    public bool ResultPublished { get; private set; }
    public CharacterId ResultCharacterId { get; private set; }
    public long TransitionRevision { get; private set; }
    public string LastPublicationKey { get; private set; } = string.Empty;
    public string LastPublicationId { get; private set; } = string.Empty;
    public string LastPublicationFingerprint { get; private set; } = string.Empty;
    private ReproductionPendingTransitionData pendingTransition;
    public ReproductionPendingTransitionData PendingTransition => pendingTransition?.Clone();
    public ReproductionStartTransactionSaveData PendingStart { get; private set; }
    public ReproductionBirthTransactionSaveData PendingBirth { get; private set; }
    public IReadOnlyList<string> ExpressedTraitIds { get; }
    public IReadOnlyList<string> LatentTraitIds { get; }
    public IReadOnlyList<InnateAptitudeSaveData> InnateAptitudes { get; }
    public float ProgressRatio => definition.TotalDurationDays > 0
        ? Math.Clamp(TotalProgressDays / (float)definition.TotalDurationDays, 0f, 1f)
        : 0f;

    public void MarkPlannedTransition()
    {
        EnsureNoPendingTransition();
        if (Status != ReproductionProcessStatus.Planned
            || TransitionRevision != 0L)
            throw new InvalidOperationException(
                "Only a new planned process can record its initial transition.");
        TransitionRevision = 1L;
    }

    public void StageTransition(ReproductionTransitionFact fact, string publicationKey)
    {
        EnsureNoPendingTransition();
        if (!Opaque(publicationKey)) throw new ArgumentException("A transition publication reference is required.");
        var pending = new ReproductionPendingTransitionData { publicationKey = publicationKey, fact = fact.Snapshot() };
        ValidatePendingTransition(pending);
        pendingTransition = pending;
    }

    public void AcknowledgeTransition(string publicationKey, string publicationId, string fingerprint)
    {
        if (pendingTransition == null || pendingTransition.publicationKey != publicationKey
            || !Opaque(publicationId) || !Opaque(fingerprint))
            throw new InvalidOperationException("The pending transition acknowledgement does not match.");
        string kind = pendingTransition.fact.transitionKind;
        if (kind == "started")
        {
            MarkStartObservationCommitted(publicationKey, publicationId, fingerprint);
            CompleteStartTransaction();
        }
        else if (kind == "birth-published")
        {
            MarkBirthObservationCommitted(publicationKey, publicationId, fingerprint);
            CompleteBirthTransaction();
        }
        LastPublicationKey = publicationKey; LastPublicationId = publicationId; LastPublicationFingerprint = fingerprint;
        pendingTransition = null;
    }

    private void ValidatePendingTransition(ReproductionPendingTransitionData pending)
    {
        ReproductionTransitionFact.Validate(pending?.fact);
        var fact = pending.fact;
        if (!Opaque(pending.publicationKey) || fact.processId != ProcessId || fact.mode != Mode
            || fact.phenotypeSpeciesId != PhenotypeSpeciesId.Value || fact.facilityId != SupportFacilityInstanceId
            || fact.revisionAfter != TransitionRevision || fact.previousPublicationKey != LastPublicationKey
            || fact.statusAfter != Status || fact.failureAfter != Failure
            || fact.phaseAfter != CurrentPhaseIndex || fact.phaseElapsedAfter != CurrentPhaseElapsedDays
            || fact.progressAfter != TotalProgressDays
            || fact.people.Single(value => value.role == "reproduction-carrier").characterId != CarrierId.Value
            || !fact.people.Where(value => value.role == "genetic-parent").Select(value => value.characterId)
                .OrderBy(value => value, StringComparer.Ordinal).SequenceEqual(
                    new[] { FirstParentId, SecondParentId }.Where(value => value.IsValid).Select(value => value.Value)
                        .OrderBy(value => value, StringComparer.Ordinal))
            || fact.transitionKind == "started" && (PendingStart?.phase != ReproductionStartTransactionPhase.MaterialsConsumed
                || Status != ReproductionProcessStatus.Active)
            || fact.transitionKind == "birth-published" && (PendingBirth?.phase != ReproductionBirthTransactionPhase.PublicationPrepared
                || fact.people.SingleOrDefault(value => value.role == "offspring")?.characterId != PendingBirth.childCharacterId))
            throw new InvalidOperationException("Pending reproduction fact does not match its state owner.");
    }

    private void EnsureNoPendingTransition()
    {
        if (pendingTransition != null)
            throw new InvalidOperationException("Reproduction transition delivery is still pending.");
    }
    private static bool Opaque(string value) => !string.IsNullOrWhiteSpace(value) && value.Trim() == value;
    private static bool EmptyProof(string key, string id, string fingerprint) =>
        key == string.Empty && id == string.Empty && fingerprint == string.Empty;
    private static bool FullProof(string key, string id, string fingerprint) =>
        Opaque(key) && Opaque(id) && Opaque(fingerprint);

    public void PrepareStartTransaction(
        string operationId,
        int absoluteDay,
        bool useFertilityTreatment,
        IEnumerable<ReproductionStartCostSaveData> costs)
    {
        EnsureNoPendingTransition();
        if (Status != ReproductionProcessStatus.Planned
            || PendingStart != null
            || !ReproductionStableIdSyntax.IsValid(operationId)
            || absoluteDay < 0)
            throw new InvalidOperationException(
                "A start transaction requires an unclaimed planned process.");
        if (Mode == ReproductionMode.GolemAssembly && useFertilityTreatment)
            throw new InvalidOperationException(
                "Biological fertility treatment cannot be used for golem assembly.");
        ReproductionStartCostSaveData[] copied = (costs
                ?? Array.Empty<ReproductionStartCostSaveData>())
            .Select(CloneStartCost)
            .OrderBy(value => value.stackId, StringComparer.Ordinal)
            .ToArray();
        if (copied.Any(value => !ValidStartCost(value)))
            throw new InvalidOperationException(
                "A reproduction start cost snapshot is invalid.");
        PendingStart = new ReproductionStartTransactionSaveData
        {
            phase = ReproductionStartTransactionPhase.Prepared,
            operationId = operationId,
            absoluteDay = absoluteDay,
            useFertilityTreatment = useFertilityTreatment,
            costs = copied.ToList()
        };
    }

    public void MarkStartMaterialsConsumed()
    {
        RequireStartPhase(ReproductionStartTransactionPhase.Prepared);
        PendingStart.phase = ReproductionStartTransactionPhase.MaterialsConsumed;
    }

    public void ApplyStartTransition()
    {
        EnsureNoPendingTransition();
        RequireStartPhase(ReproductionStartTransactionPhase.MaterialsConsumed);
        if (Status != ReproductionProcessStatus.Planned)
            throw new InvalidOperationException(
                "Only a planned process can apply its start transition.");
        SelectFertilityTreatment(PendingStart.useFertilityTreatment);
        Start();
        IncrementTransitionRevision();
    }

    private void MarkStartObservationCommitted(string publicationKey, string publicationId, string fingerprint)
    {
        RequireStartPhase(ReproductionStartTransactionPhase.MaterialsConsumed);
        if (!FullProof(publicationKey, publicationId, fingerprint))
            throw new ArgumentException("A delivered transition reference is required.");
        PendingStart.phase = ReproductionStartTransactionPhase.ObservationCommitted;
        PendingStart.publicationKey = publicationKey;
        PendingStart.publicationId = publicationId;
        PendingStart.publicationFingerprint = fingerprint;
    }

    public void CompleteStartTransaction()
    {
        RequireStartPhase(ReproductionStartTransactionPhase.ObservationCommitted);
        if (Status != ReproductionProcessStatus.Active)
            throw new InvalidOperationException(
                "A committed start transaction requires an active process.");
        LastPublicationKey = PendingStart.publicationKey;
        LastPublicationId = PendingStart.publicationId;
        LastPublicationFingerprint = PendingStart.publicationFingerprint;
        PendingStart = null;
    }

    public void Start()
    {
        EnsureNoPendingTransition();
        if (Status != ReproductionProcessStatus.Planned)
            throw new InvalidOperationException(
                "Only a planned reproduction process can be started.");
        Status = ReproductionProcessStatus.Active;
    }

    public void SelectFertilityTreatment(bool useTreatment)
    {
        EnsureNoPendingTransition();
        if (Status != ReproductionProcessStatus.Planned)
            throw new InvalidOperationException(
                "Fertility treatment can only be selected for a planned process.");
        if (Mode == ReproductionMode.GolemAssembly && useTreatment)
            throw new InvalidOperationException(
                "Biological fertility treatment cannot be used for golem assembly.");
        FertilityTreatmentUsed = useTreatment;
    }

    public void AdvanceDay(ReproductionDailyContext context, double miscarriageRandom)
    {
        EnsureNoPendingTransition();
        ReproductionProcessSaveData before = Capture();
        if (Status is ReproductionProcessStatus.Completed or ReproductionProcessStatus.Failed)
        {
            IncrementIfChanged(before);
            return;
        }
        if (Status == ReproductionProcessStatus.WaitingForEmergencyExtraction)
        {
            if (context.AbsoluteDay - CarrierDeathAbsoluteDay > 1)
                Fail(ReproductionFailureCode.EmergencyExtractionExpired);
            IncrementIfChanged(before);
            return;
        }

        if (Mode == ReproductionMode.Pregnancy
            && definition.Phases[CurrentPhaseIndex].phase
                == ReproductionPhaseKind.Pregnancy
            && !EmergencyExtracted
            && (context.CarrierHealth < 30f || context.CarrierNutrition < 20f)
            && Math.Clamp(miscarriageRandom, 0d, 0.999999d)
                < 0.10d / context.GestationStabilityMultiplier)
        {
            Fail(ReproductionFailureCode.Miscarriage);
            IncrementIfChanged(before);
            return;
        }

        if (definition.Phases[CurrentPhaseIndex].phase
                == ReproductionPhaseKind.Attempt
            && CurrentPhaseElapsedDays == 0)
        {
            float successChance = ReproductionRules.CalculateSuccessChance(
                definition.BaseSuccessChance,
                context.CarrierHealth,
                context.CarrierNutrition,
                context.FertilityAgeCoefficient);
            if (Math.Clamp(miscarriageRandom, 0d, 0.999999d) >= successChance)
            {
                Fail(ReproductionFailureCode.ConceptionFailed);
                IncrementIfChanged(before);
                return;
            }
        }

        bool environmentDependent = Mode is ReproductionMode.Egg
            or ReproductionMode.Spore or ReproductionMode.CoreDivision;
        bool safeTemperature = context.EnvironmentTemperature >= definition.ViableTemperatureMinimum
            && context.EnvironmentTemperature <= definition.ViableTemperatureMaximum;
        if (environmentDependent && !safeTemperature)
        {
            ConsecutiveUnsafeTemperatureDays++;
            if (ConsecutiveUnsafeTemperatureDays >= 3)
                Fail(ReproductionFailureCode.IncubationEnvironmentFailed);
            else
                Status = ReproductionProcessStatus.WaitingForEnvironment;
            IncrementIfChanged(before);
            return;
        }

        ConsecutiveUnsafeTemperatureDays = 0;
        Status = ReproductionProcessStatus.Active;
        CurrentPhaseElapsedDays++;
        TotalProgressDays++;
        int phaseDuration = definition.Phases[CurrentPhaseIndex].durationDays;
        if (CurrentPhaseElapsedDays < phaseDuration)
        {
            IncrementIfChanged(before);
            return;
        }
        CurrentPhaseIndex++;
        CurrentPhaseElapsedDays = 0;
        if (CurrentPhaseIndex >= definition.Phases.Count)
            Status = ReproductionProcessStatus.Completed;
        IncrementIfChanged(before);
    }

    public void NotifyCarrierDeath(int absoluteDay)
    {
        EnsureNoPendingTransition();
        if (CarrierDeathAbsoluteDay > 0)
        {
            if (CarrierDeathAbsoluteDay == absoluteDay) return;
            throw new InvalidOperationException("The reproduction carrier already has a different death day.");
        }
        if (Mode != ReproductionMode.Pregnancy || Status != ReproductionProcessStatus.Active)
            return;
        CarrierDeathAbsoluteDay = absoluteDay;
        if (ProgressRatio >= 0.8f)
            Status = ReproductionProcessStatus.WaitingForEmergencyExtraction;
        else
            Fail(ReproductionFailureCode.CarrierDied);
        IncrementTransitionRevision();
    }

    public void EmergencyExtract(int absoluteDay)
    {
        EnsureNoPendingTransition();
        if (Status != ReproductionProcessStatus.WaitingForEmergencyExtraction
            || absoluteDay - CarrierDeathAbsoluteDay > 1)
            throw new InvalidOperationException("Emergency extraction is not available.");
        EmergencyExtracted = true;
        Status = ReproductionProcessStatus.Active;
        IncrementTransitionRevision();
    }

    public void MarkResultPublished(CharacterId resultCharacterId)
    {
        EnsureNoPendingTransition();
        if (Status != ReproductionProcessStatus.Completed)
            throw new InvalidOperationException("Only a completed reproduction process can publish its result.");
        if (ResultPublished)
            throw new InvalidOperationException("Reproduction result was already published.");
        if (!resultCharacterId.IsValid)
            throw new ArgumentException("A published reproduction result requires a valid character ID.");
        ResultPublished = true;
        ResultCharacterId = resultCharacterId;
    }

    public void PrepareBirthTransaction(CharacterId childCharacterId, int absoluteDay)
    {
        EnsureNoPendingTransition();
        if (Status != ReproductionProcessStatus.Completed
            || ResultPublished
            || PendingBirth != null
            || !childCharacterId.IsValid
            || absoluteDay < 1)
            throw new InvalidOperationException(
                "A birth transaction requires one completed unpublished process.");
        string operationId = "reproduction-birth:"
            + PersistentEntityId.GetStableHash32(ProcessId).ToString("x8");
        PendingBirth = new ReproductionBirthTransactionSaveData
        {
            phase = ReproductionBirthTransactionPhase.Prepared,
            operationId = operationId,
            childCharacterId = childCharacterId.Value,
            absoluteDay = absoluteDay
        };
    }

    public void AdvanceBirthTransaction(
        ReproductionBirthTransactionPhase expected,
        ReproductionBirthTransactionPhase next)
    {
        RequireBirthPhase(expected);
        if ((int)next != (int)expected + 1
            || next > ReproductionBirthTransactionPhase.HousingResolutionRecorded)
            throw new InvalidOperationException(
                "Birth transaction phases must advance one external boundary at a time.");
        PendingBirth.phase = next;
    }

    public void PrepareBirthPublicationMarker()
    {
        EnsureNoPendingTransition();
        RequireBirthPhase(
            ReproductionBirthTransactionPhase.HousingResolutionRecorded);
        IncrementTransitionRevision();
        PendingBirth.phase = ReproductionBirthTransactionPhase.PublicationPrepared;
        PendingBirth.publicationRevision = TransitionRevision;
    }

    private void MarkBirthObservationCommitted(string publicationKey, string publicationId, string fingerprint)
    {
        RequireBirthPhase(ReproductionBirthTransactionPhase.PublicationPrepared);
        if (!FullProof(publicationKey, publicationId, fingerprint))
            throw new ArgumentException("A delivered transition reference is required.");
        PendingBirth.phase = ReproductionBirthTransactionPhase.ObservationCommitted;
        PendingBirth.publicationKey = publicationKey;
        PendingBirth.publicationId = publicationId;
        PendingBirth.publicationFingerprint = fingerprint;
    }

    public void CompleteBirthTransaction()
    {
        RequireBirthPhase(ReproductionBirthTransactionPhase.ObservationCommitted);
        CharacterId child = new(PendingBirth.childCharacterId);
        if (!child.IsValid)
            throw new InvalidOperationException(
                "The committed birth transaction lost its child identity.");
        ResultPublished = true;
        ResultCharacterId = child;
        LastPublicationKey = PendingBirth.publicationKey;
        LastPublicationId = PendingBirth.publicationId;
        LastPublicationFingerprint = PendingBirth.publicationFingerprint;
        PendingBirth = null;
    }

    public ReproductionProcessSaveData Capture() => new()
    {
        processId = ProcessId,
        firstParentId = FirstParentId.Value,
        secondParentId = SecondParentId.Value,
        carrierId = CarrierId.Value,
        phenotypeSpeciesId = PhenotypeSpeciesId.Value,
        mode = Mode,
        status = Status,
        failure = Failure,
        startedAbsoluteDay = StartedAbsoluteDay,
        currentPhaseIndex = CurrentPhaseIndex,
        currentPhaseElapsedDays = CurrentPhaseElapsedDays,
        totalProgressDays = TotalProgressDays,
        consecutiveUnsafeTemperatureDays = ConsecutiveUnsafeTemperatureDays,
        carrierDeathAbsoluteDay = CarrierDeathAbsoluteDay,
        emergencyExtracted = EmergencyExtracted,
        crossLineageIncubatorUsed = CrossLineageIncubatorUsed,
        fertilityTreatmentUsed = FertilityTreatmentUsed,
        supportFacilityInstanceId = SupportFacilityInstanceId,
        resultPublished = ResultPublished,
        resultCharacterId = ResultCharacterId.Value,
        transitionRevision = TransitionRevision,
        lastPublicationKey = LastPublicationKey,
        lastPublicationId = LastPublicationId,
        lastPublicationFingerprint = LastPublicationFingerprint,
        pendingTransitions = pendingTransition == null ? new() : new() { pendingTransition.Clone() },
        pendingStart = CloneStartTransaction(PendingStart),
        pendingBirth = CloneBirthTransaction(PendingBirth),
        expressedTraitIds = ExpressedTraitIds.ToList(),
        latentTraitIds = LatentTraitIds.ToList(),
        innateAptitudes = InnateAptitudes.ToList()
    };

    public static ReproductionProcess Restore(
        ReproductionProcessSaveData data,
        ReproductionDefinition definition)
    {
        if (data == null || !Enum.IsDefined(typeof(ReproductionProcessStatus), data.status)
            || !Enum.IsDefined(typeof(ReproductionFailureCode), data.failure)
            || data.mode != definition.Mode || data.startedAbsoluteDay < 1
            || data.currentPhaseIndex < 0 || data.currentPhaseIndex > definition.Phases.Count
            || data.currentPhaseElapsedDays < 0 || data.totalProgressDays < 0
            || data.consecutiveUnsafeTemperatureDays < 0
            || data.transitionRevision < 0L
            || !(EmptyProof(data.lastPublicationKey, data.lastPublicationId, data.lastPublicationFingerprint)
                || FullProof(data.lastPublicationKey, data.lastPublicationId, data.lastPublicationFingerprint))
            || data.pendingTransitions == null || data.pendingTransitions.Count > 1
            || data.pendingTransitions.Any(value => value == null))
            throw new InvalidOperationException("Reproduction process payload is invalid.");
        ReproductionProcess process = new(
            data.processId,
            new CharacterId(data.firstParentId),
            new CharacterId(data.secondParentId),
            new CharacterId(data.carrierId),
            new CharacterSpeciesId(data.phenotypeSpeciesId),
            definition,
            data.startedAbsoluteDay,
            data.crossLineageIncubatorUsed,
            data.supportFacilityInstanceId,
            data.expressedTraitIds,
            data.latentTraitIds,
            data.innateAptitudes,
            startActive: data.status != ReproductionProcessStatus.Planned,
            fertilityTreatmentUsed: data.fertilityTreatmentUsed)
        {
            Status = data.status,
            Failure = data.failure,
            CurrentPhaseIndex = data.currentPhaseIndex,
            CurrentPhaseElapsedDays = data.currentPhaseElapsedDays,
            TotalProgressDays = data.totalProgressDays,
            ConsecutiveUnsafeTemperatureDays = data.consecutiveUnsafeTemperatureDays,
            CarrierDeathAbsoluteDay = data.carrierDeathAbsoluteDay,
            EmergencyExtracted = data.emergencyExtracted,
            TransitionRevision = data.transitionRevision,
            LastPublicationKey = data.lastPublicationKey,
            LastPublicationId = data.lastPublicationId,
            LastPublicationFingerprint = data.lastPublicationFingerprint,
            pendingTransition = data.pendingTransitions.SingleOrDefault()?.Clone(),
            PendingStart = NormalizeRestoredStartTransaction(data.pendingStart),
            PendingBirth = NormalizeRestoredBirthTransaction(data.pendingBirth),
            ResultPublished = data.resultPublished,
            ResultCharacterId = string.IsNullOrWhiteSpace(data.resultCharacterId)
                ? default
                : new CharacterId(data.resultCharacterId)
        };
        bool startInconsistent = process.PendingStart != null
            && ((process.PendingStart.phase
                        == ReproductionStartTransactionPhase.ObservationCommitted
                    || process.pendingTransition?.fact?.transitionKind == "started"
                    ? process.Status != ReproductionProcessStatus.Active
                    : process.Status != ReproductionProcessStatus.Planned)
                || !ValidStartTransaction(process.PendingStart));
        bool birthInconsistent = process.PendingBirth != null
            && (process.Status != ReproductionProcessStatus.Completed
                || process.ResultPublished
                || !ValidBirthTransaction(
                    process.PendingBirth,
                    process.TransitionRevision));
        if (process.TotalProgressDays > definition.TotalDurationDays
            || process.CurrentPhaseIndex < definition.Phases.Count
                && process.CurrentPhaseElapsedDays >= definition.Phases[process.CurrentPhaseIndex].durationDays
            || process.Status == ReproductionProcessStatus.Completed
                && process.CurrentPhaseIndex != definition.Phases.Count
            || process.ResultPublished
                && (process.Status != ReproductionProcessStatus.Completed
                    || !process.ResultCharacterId.IsValid)
            || !process.ResultPublished && process.ResultCharacterId.IsValid
            || startInconsistent
            || birthInconsistent)
            throw new InvalidOperationException("Reproduction process progress is inconsistent.");
        if (process.pendingTransition != null) process.ValidatePendingTransition(process.pendingTransition);
        return process;
    }

    private void Fail(ReproductionFailureCode failure)
    {
        Failure = failure;
        Status = ReproductionProcessStatus.Failed;
    }

    private void IncrementIfChanged(ReproductionProcessSaveData before)
    {
        if (before.status != Status
            || before.failure != Failure
            || before.currentPhaseIndex != CurrentPhaseIndex
            || before.currentPhaseElapsedDays != CurrentPhaseElapsedDays
            || before.totalProgressDays != TotalProgressDays
            || before.consecutiveUnsafeTemperatureDays
                != ConsecutiveUnsafeTemperatureDays
            || before.carrierDeathAbsoluteDay != CarrierDeathAbsoluteDay
            || before.emergencyExtracted != EmergencyExtracted)
            IncrementTransitionRevision();
    }

    private void IncrementTransitionRevision()
    {
        if (TransitionRevision == long.MaxValue)
            throw new InvalidOperationException(
                "Reproduction transition revision is exhausted.");
        TransitionRevision++;
    }

    private void RequireStartPhase(ReproductionStartTransactionPhase phase)
    {
        if (PendingStart == null || PendingStart.phase != phase)
            throw new InvalidOperationException(
                $"Reproduction start transaction is not in phase '{phase}'.");
    }

    private void RequireBirthPhase(ReproductionBirthTransactionPhase phase)
    {
        if (PendingBirth == null || PendingBirth.phase != phase)
            throw new InvalidOperationException(
                $"Reproduction birth transaction is not in phase '{phase}'.");
    }

    private static bool ValidStartCost(ReproductionStartCostSaveData value) =>
        value != null
        && ReproductionStableIdSyntax.IsValid(value.stackId)
        && ReproductionStableIdSyntax.IsValid(value.itemId)
        && value.quantity > 0
        && value.quantityBefore >= value.quantity
        && value.quantityAfter == value.quantityBefore - value.quantity;

    private static bool ValidStartTransaction(
        ReproductionStartTransactionSaveData value) => value != null
        && Enum.IsDefined(typeof(ReproductionStartTransactionPhase), value.phase)
        && value.phase != ReproductionStartTransactionPhase.None
        && ReproductionStableIdSyntax.IsValid(value.operationId)
        && value.absoluteDay >= 0
        && value.costs != null
        && value.costs.All(ValidStartCost)
        && (value.phase == ReproductionStartTransactionPhase.ObservationCommitted
            ? FullProof(value.publicationKey, value.publicationId, value.publicationFingerprint)
            : EmptyProof(value.publicationKey, value.publicationId, value.publicationFingerprint));

    private static ReproductionStartCostSaveData CloneStartCost(
        ReproductionStartCostSaveData value) => value == null
        ? null
        : new ReproductionStartCostSaveData
        {
            stackId = value.stackId,
            itemId = value.itemId,
            quantity = value.quantity,
            quantityBefore = value.quantityBefore,
            quantityAfter = value.quantityAfter
        };

    private static ReproductionStartTransactionSaveData CloneStartTransaction(
        ReproductionStartTransactionSaveData value) => value == null
        ? null
        : new ReproductionStartTransactionSaveData
        {
            phase = value.phase,
            operationId = value.operationId,
            absoluteDay = value.absoluteDay,
            useFertilityTreatment = value.useFertilityTreatment,
            costs = (value.costs ?? new List<ReproductionStartCostSaveData>())
                .Select(CloneStartCost)
                .ToList(),
            publicationKey = value.publicationKey,
            publicationId = value.publicationId,
            publicationFingerprint = value.publicationFingerprint
        };

    private static ReproductionStartTransactionSaveData
        NormalizeRestoredStartTransaction(
            ReproductionStartTransactionSaveData value)
    {
        if (value == null)
            return null;
        bool jsonUtilityNullPlaceholder =
            value.phase == ReproductionStartTransactionPhase.None
            && string.IsNullOrWhiteSpace(value.operationId)
            && value.absoluteDay == 0
            && !value.useFertilityTreatment
            && (value.costs == null || value.costs.Count == 0)
            && EmptyProof(value.publicationKey, value.publicationId, value.publicationFingerprint);
        return jsonUtilityNullPlaceholder
            ? null
            : CloneStartTransaction(value);
    }

    private static bool ValidBirthTransaction(
        ReproductionBirthTransactionSaveData value,
        long transitionRevision)
    {
        if (value == null
            || !Enum.IsDefined(typeof(ReproductionBirthTransactionPhase), value.phase)
            || value.phase == ReproductionBirthTransactionPhase.None
            || !ReproductionStableIdSyntax.IsValid(value.operationId)
            || !new CharacterId(value.childCharacterId).IsValid
            || value.absoluteDay < 1
            || value.publicationRevision < 0L)
            return false;
        bool publicationPrepared = value.phase
            >= ReproductionBirthTransactionPhase.PublicationPrepared;
        if (publicationPrepared
            != (value.publicationRevision > 0L)
            || publicationPrepared
                && value.publicationRevision != transitionRevision)
            return false;
        bool outcomeCommitted = value.phase
            == ReproductionBirthTransactionPhase.ObservationCommitted;
        return outcomeCommitted
            ? FullProof(value.publicationKey, value.publicationId, value.publicationFingerprint)
            : EmptyProof(value.publicationKey, value.publicationId, value.publicationFingerprint);
    }

    private static ReproductionBirthTransactionSaveData CloneBirthTransaction(
        ReproductionBirthTransactionSaveData value) => value == null
        ? null
        : new ReproductionBirthTransactionSaveData
        {
            phase = value.phase,
            operationId = value.operationId,
            childCharacterId = value.childCharacterId,
            absoluteDay = value.absoluteDay,
            publicationRevision = value.publicationRevision,
            publicationKey = value.publicationKey,
            publicationId = value.publicationId,
            publicationFingerprint = value.publicationFingerprint
        };

    private static ReproductionBirthTransactionSaveData
        NormalizeRestoredBirthTransaction(
            ReproductionBirthTransactionSaveData value)
    {
        if (value == null)
            return null;
        bool jsonUtilityNullPlaceholder =
            value.phase == ReproductionBirthTransactionPhase.None
            && string.IsNullOrWhiteSpace(value.operationId)
            && string.IsNullOrWhiteSpace(value.childCharacterId)
            && value.absoluteDay == 0
            && value.publicationRevision == 0L
            && EmptyProof(value.publicationKey, value.publicationId, value.publicationFingerprint);
        return jsonUtilityNullPlaceholder
            ? null
            : CloneBirthTransaction(value);
    }

    private static IReadOnlyList<string> NormalizeTraits(IEnumerable<string> values, int maximum) =>
        (values ?? Array.Empty<string>())
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .Take(maximum)
            .ToArray();
}

public sealed class ReproductionWorldAggregate
{
    private readonly Dictionary<string, ReproductionProcess> processes = new(StringComparer.Ordinal);
    private readonly Dictionary<string, ReproductionCarrierDeathNotificationSaveData>
        pendingCarrierDeaths = new(StringComparer.Ordinal);
    public FamilyPlanningPolicy FamilyPlanningPolicy { get; private set; } = FamilyPlanningPolicy.Planned;
    public long FamilyPlanningPolicyRevision { get; private set; }
    private FamilyPlanningPolicyPendingChangeSaveData
        pendingFamilyPlanningPolicyChange;
    public FamilyPlanningPolicyPendingChangeSaveData
        PendingFamilyPlanningPolicyChange =>
            ClonePolicyChange(pendingFamilyPlanningPolicyChange);
    public bool HasPendingFamilyPlanningPolicyChange =>
        pendingFamilyPlanningPolicyChange != null;
    public int LastAllowedPolicyEvaluationDay { get; private set; }
    public IReadOnlyList<ReproductionProcess> Processes => processes.Values
        .OrderBy(value => value.ProcessId, StringComparer.Ordinal).ToArray();
    public IReadOnlyList<ReproductionCarrierDeathNotificationSaveData>
        PendingCarrierDeaths => pendingCarrierDeaths.Values
            .OrderBy(value => value.notificationId, StringComparer.Ordinal)
            .Select(CloneCarrierDeath)
            .ToArray();

    public void SetFamilyPlanningPolicy(FamilyPlanningPolicy policy)
    {
        if (!Enum.IsDefined(typeof(FamilyPlanningPolicy), policy))
            throw new ArgumentOutOfRangeException(nameof(policy));
        if (FamilyPlanningPolicy == policy)
            return;
        if (pendingFamilyPlanningPolicyChange != null)
            throw new InvalidOperationException(
                "A pending family-planning policy change must be resolved first.");
        if (FamilyPlanningPolicyRevision == long.MaxValue)
            throw new InvalidOperationException(
                "Family-planning policy revision is exhausted.");
        FamilyPlanningPolicy = policy;
        FamilyPlanningPolicyRevision++;
    }

    public void StageFamilyPlanningPolicyChange(
        FamilyPlanningPolicyPendingChangeSaveData pending)
    {
        if (!ValidPolicyChange(pending)
            || pendingFamilyPlanningPolicyChange != null
            || FamilyPlanningPolicy != pending.before
            || FamilyPlanningPolicyRevision != pending.beforeRevision)
        {
            throw new InvalidOperationException(
                "A valid family-planning policy change is required.");
        }
        FamilyPlanningPolicy = pending.after;
        FamilyPlanningPolicyRevision = pending.afterRevision;
        pendingFamilyPlanningPolicyChange = ClonePolicyChange(pending);
    }

    public void AcknowledgeFamilyPlanningPolicyChange(long afterRevision)
    {
        if (pendingFamilyPlanningPolicyChange == null
            || pendingFamilyPlanningPolicyChange.afterRevision != afterRevision
            || FamilyPlanningPolicyRevision != afterRevision
            || FamilyPlanningPolicy
                != pendingFamilyPlanningPolicyChange.after)
        {
            throw new InvalidOperationException(
                "The family-planning policy change is not pending.");
        }
        pendingFamilyPlanningPolicyChange = null;
    }

    public void Add(ReproductionProcess process)
    {
        if (process == null || !processes.TryAdd(process.ProcessId, process))
            throw new InvalidOperationException("Reproduction process is null or duplicated.");
    }

    public bool TryGet(string processId, out ReproductionProcess process) =>
        processes.TryGetValue(processId?.Trim() ?? string.Empty, out process);

    public void EnqueueCarrierDeath(
        ReproductionCarrierDeathNotificationSaveData notification)
    {
        if (!ValidCarrierDeath(notification))
            throw new InvalidOperationException(
                "A valid carrier-death notification is required.");
        if (pendingCarrierDeaths.TryGetValue(
                notification.notificationId,
                out ReproductionCarrierDeathNotificationSaveData existing))
        {
            if (!SameCarrierDeath(existing, notification))
                throw new InvalidOperationException(
                    "A carrier-death notification ID was reused with different facts.");
            return;
        }
        pendingCarrierDeaths.Add(
            notification.notificationId,
            CloneCarrierDeath(notification));
    }

    public void RemoveCarrierDeath(string notificationId)
    {
        if (!pendingCarrierDeaths.Remove(notificationId?.Trim() ?? string.Empty))
            throw new InvalidOperationException(
                "The carrier-death notification is not pending.");
    }

    public void MarkAllowedPolicyEvaluation(int absoluteDay)
    {
        if (absoluteDay < LastAllowedPolicyEvaluationDay)
            throw new InvalidOperationException("Family-planning evaluation day cannot move backward.");
        LastAllowedPolicyEvaluationDay = absoluteDay;
    }

    public ReproductionWorldSaveData Capture() => new()
    {
        familyPlanningPolicy = FamilyPlanningPolicy,
        familyPlanningPolicyRevision = FamilyPlanningPolicyRevision,
        pendingFamilyPlanningPolicyChanges =
            pendingFamilyPlanningPolicyChange == null
                ? new List<FamilyPlanningPolicyPendingChangeSaveData>()
                : new List<FamilyPlanningPolicyPendingChangeSaveData>
                {
                    ClonePolicyChange(pendingFamilyPlanningPolicyChange)
                },
        lastAllowedPolicyEvaluationDay = LastAllowedPolicyEvaluationDay,
        processes = Processes.Select(value => value.Capture()).ToList(),
        pendingCarrierDeaths = PendingCarrierDeaths
            .Select(CloneCarrierDeath)
            .ToList()
    };

    public static ReproductionWorldAggregate Restore(
        ReproductionWorldSaveData data,
        IReproductionDefinitionCatalog definitions)
    {
        if (data == null
            || data.version != ReproductionWorldSaveData.CurrentVersion
            || data.processes == null || data.lastAllowedPolicyEvaluationDay < 0
            || data.familyPlanningPolicyRevision < 0L
            || data.pendingCarrierDeaths == null
            || (data.pendingFamilyPlanningPolicyChanges == null
                    || data.pendingFamilyPlanningPolicyChanges.Count > 1
                    || data.pendingFamilyPlanningPolicyChanges.Any(
                        value => !ValidPolicyChange(value)))
            || !Enum.IsDefined(typeof(FamilyPlanningPolicy), data.familyPlanningPolicy))
            throw new InvalidOperationException("Reproduction payload is incomplete or unsupported.");
        ReproductionWorldAggregate result = new()
        {
            FamilyPlanningPolicy = data.familyPlanningPolicy,
            FamilyPlanningPolicyRevision = data.familyPlanningPolicyRevision,
            LastAllowedPolicyEvaluationDay = data.lastAllowedPolicyEvaluationDay
        };
        if (data.pendingFamilyPlanningPolicyChanges?.Count == 1)
        {
            FamilyPlanningPolicyPendingChangeSaveData pending =
                data.pendingFamilyPlanningPolicyChanges[0];
            if (result.FamilyPlanningPolicy != pending.after
                || result.FamilyPlanningPolicyRevision
                    != pending.afterRevision)
            {
                throw new InvalidOperationException(
                    "Pending family-planning policy change does not match state.");
            }
            result.pendingFamilyPlanningPolicyChange =
                ClonePolicyChange(pending);
        }
        foreach (ReproductionProcessSaveData source in data.processes)
        {
            CharacterSpeciesId species = new(source?.phenotypeSpeciesId);
            ReproductionProcess process = ReproductionProcess.Restore(
                source,
                definitions.RequireReproduction(species));
            result.Add(process);
        }
        foreach (ReproductionCarrierDeathNotificationSaveData source in
                 data.pendingCarrierDeaths)
            result.EnqueueCarrierDeath(source);
        return result;
    }

    private static bool ValidPolicyChange(
        FamilyPlanningPolicyPendingChangeSaveData value) => value != null
        && Enum.IsDefined(typeof(FamilyPlanningPolicy), value.before)
        && Enum.IsDefined(typeof(FamilyPlanningPolicy), value.after)
        && value.before != value.after
        && value.beforeRevision >= 0L
        && value.beforeRevision < long.MaxValue
        && value.afterRevision == value.beforeRevision + 1L
        && ReproductionStableIdSyntax.IsValid(value.source)
        && value.absoluteDay >= 0;

    private static FamilyPlanningPolicyPendingChangeSaveData ClonePolicyChange(
        FamilyPlanningPolicyPendingChangeSaveData value) => value == null
        ? null
        : new FamilyPlanningPolicyPendingChangeSaveData
        {
            before = value.before,
            after = value.after,
            beforeRevision = value.beforeRevision,
            afterRevision = value.afterRevision,
            source = value.source,
            absoluteDay = value.absoluteDay
        };

    private static bool ValidCarrierDeath(
        ReproductionCarrierDeathNotificationSaveData value) => value != null
        && ReproductionStableIdSyntax.IsValid(value.notificationId)
        && ReproductionStableIdSyntax.IsValid(value.carrierId)
        && value.absoluteDay > 0
        && Enum.IsDefined(typeof(CharacterDeathCauseCode), value.cause)
        && value.parentPublicationKey != null
        && (value.parentPublicationKey.Length == 0
            || !string.IsNullOrWhiteSpace(value.parentPublicationKey));

    private static bool SameCarrierDeath(
        ReproductionCarrierDeathNotificationSaveData left,
        ReproductionCarrierDeathNotificationSaveData right) =>
        left.carrierId == right.carrierId
        && left.absoluteDay == right.absoluteDay
        && left.cause == right.cause
        && left.locationX == right.locationX
        && left.locationY == right.locationY
        && left.parentPublicationKey == right.parentPublicationKey;

    private static ReproductionCarrierDeathNotificationSaveData CloneCarrierDeath(
        ReproductionCarrierDeathNotificationSaveData value) => value == null
        ? null
        : new ReproductionCarrierDeathNotificationSaveData
        {
            notificationId = value.notificationId,
            carrierId = value.carrierId,
            absoluteDay = value.absoluteDay,
            cause = value.cause,
            locationX = value.locationX,
            locationY = value.locationY,
            parentPublicationKey = value.parentPublicationKey
        };
}

public interface IReproductionService
{
    FamilyPlanningPolicy FamilyPlanningPolicy { get; }
    long FamilyPlanningPolicyRevision { get; }
    bool HasPendingFamilyPlanningPolicyChange { get; }
    int LastAllowedPolicyEvaluationDay { get; }
    IReadOnlyList<ReproductionProcess> Processes { get; }
    void SetFamilyPlanningPolicy(FamilyPlanningPolicy policy);
    void MarkAllowedPolicyEvaluation(int absoluteDay);
    void AddProcess(ReproductionProcess process);
    void AdvanceProcess(string processId, ReproductionDailyContext context);
    void NotifyCarrierDeath(CharacterId carrierId, int absoluteDay);
    void EmergencyExtract(string processId, int absoluteDay);
    void MarkResultPublished(string processId, CharacterId resultCharacterId);
}

public interface IReproductionPersistence
{
    ReproductionWorldSaveData Capture();
    ReproductionWorldAggregate PrepareRestore(ReproductionWorldSaveData data);
    void PublishRestore(ReproductionWorldAggregate candidate);
}

public static class ReproductionRules
{
    public static ReproductionFailureCode ValidatePair(
        CharacterId first,
        CharacterId second,
        ReproductionDefinition firstDefinition,
        ReproductionDefinition secondDefinition,
        IKinshipQuery kinship,
        bool crossLineageIncubatorAvailable)
    {
        if (firstDefinition.Mode == ReproductionMode.GolemAssembly
            || secondDefinition.Mode == ReproductionMode.GolemAssembly)
            return ReproductionFailureCode.GolemGeneticHybridForbidden;
        if ((kinship ?? throw new ArgumentNullException(nameof(kinship)))
                .GetPartnershipOrReproductionRestriction(first, second)
            != KinshipRestriction.None)
            return ReproductionFailureCode.KinshipRestricted;
        if (firstDefinition.Mode != secondDefinition.Mode && !crossLineageIncubatorAvailable)
            return ReproductionFailureCode.CrossLineageIncubatorRequired;
        return ReproductionFailureCode.None;
    }

    public static float CalculateSuccessChance(
        float baseChance,
        float health,
        float nutrition,
        float fertilityAgeCoefficient)
    {
        float condition = Math.Clamp((health + nutrition) / 200f, 0.1f, 1f);
        return Math.Clamp(baseChance * condition
            * Math.Clamp(fertilityAgeCoefficient, 0f, 1f), 0f, 1f);
    }

    public static float ApplyFertilityTreatmentToCoefficient(
        float coefficient,
        bool treatmentUsed) =>
        Math.Clamp(coefficient * (treatmentUsed ? 1.2f : 1f), 0f, 1f);

    public static float ApplyFertilityTreatmentToGestationStability(
        float stability,
        bool treatmentUsed) =>
        Math.Clamp(stability * (treatmentUsed ? 1.15f : 1f), 0.5f, 1.5f);

    public static CharacterSpeciesId SelectPhenotype(
        CharacterSpeciesId first,
        CharacterSpeciesId second,
        string deterministicSeed)
    {
        if (!first.IsValid || !second.IsValid)
            throw new ArgumentException("Two valid phenotype candidates are required.");
        uint hash = PersistentEntityId.GetStableHash32(deterministicSeed ?? string.Empty);
        return (hash & 1u) == 0u ? first : second;
    }

    public static void SelectInheritedTraits(
        IEnumerable<string> firstParentCandidates,
        IEnumerable<string> secondParentCandidates,
        string deterministicSeed,
        out IReadOnlyList<string> expressed,
        out IReadOnlyList<string> latent)
    {
        string[] candidates = (firstParentCandidates ?? Array.Empty<string>()).Take(3)
            .Concat((secondParentCandidates ?? Array.Empty<string>()).Take(3))
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => PersistentEntityId.GetStableHash32(
                (deterministicSeed ?? string.Empty) + ":" + value))
            .ThenBy(value => value, StringComparer.Ordinal)
            .ToArray();
        expressed = candidates.Take(4).ToArray();
        latent = candidates.Skip(4).Take(2).ToArray();
    }

    public static int InheritAptitude(
        int firstParent,
        int secondParent,
        string deterministicSeed,
        string skillId)
    {
        int average = (Math.Clamp(firstParent, 0, 100)
            + Math.Clamp(secondParent, 0, 100)) / 2;
        int variance = (int)(PersistentEntityId.GetStableHash32(
            (deterministicSeed ?? string.Empty) + ":" + (skillId ?? string.Empty)) % 11u) - 5;
        return Math.Clamp(average + variance, 0, 100);
    }

    public static bool ShouldEvaluateAllowedPolicy(
        FamilyPlanningPolicy policy,
        int daysSinceLastEvaluation) =>
        policy == FamilyPlanningPolicy.Allowed && daysSinceLastEvaluation >= 10;
}
