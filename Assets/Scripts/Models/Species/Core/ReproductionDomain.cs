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
    public bool resultPublished;
    public string resultCharacterId = string.Empty;
    public List<string> expressedTraitIds = new();
    public List<string> latentTraitIds = new();
    public List<InnateAptitudeSaveData> innateAptitudes = new();
}

[Serializable]
public sealed class ReproductionWorldSaveData
{
    public const int CurrentVersion = 1;
    public int version = CurrentVersion;
    public FamilyPlanningPolicy familyPlanningPolicy = FamilyPlanningPolicy.Planned;
    public int lastAllowedPolicyEvaluationDay;
    public List<ReproductionProcessSaveData> processes = new();
}

public readonly struct ReproductionDailyContext
{
    public ReproductionDailyContext(
        int absoluteDay,
        float carrierHealth,
        float carrierNutrition,
        float environmentTemperature,
        float fertilityAgeCoefficient = 1f)
    {
        AbsoluteDay = absoluteDay;
        CarrierHealth = carrierHealth;
        CarrierNutrition = carrierNutrition;
        EnvironmentTemperature = environmentTemperature;
        FertilityAgeCoefficient = Math.Clamp(fertilityAgeCoefficient, 0f, 1f);
    }
    public int AbsoluteDay { get; }
    public float CarrierHealth { get; }
    public float CarrierNutrition { get; }
    public float EnvironmentTemperature { get; }
    public float FertilityAgeCoefficient { get; }
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
        ExpressedTraitIds = NormalizeTraits(expressedTraitIds, 4);
        LatentTraitIds = NormalizeTraits(latentTraitIds, 2);
        InnateAptitudes = (aptitudes ?? Array.Empty<InnateAptitudeSaveData>())
            .Where(value => value != null && !string.IsNullOrWhiteSpace(value.skillId))
            .OrderBy(value => value.skillId, StringComparer.Ordinal)
            .ToArray();
        Status = ReproductionProcessStatus.Active;
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
    public bool ResultPublished { get; private set; }
    public CharacterId ResultCharacterId { get; private set; }
    public IReadOnlyList<string> ExpressedTraitIds { get; }
    public IReadOnlyList<string> LatentTraitIds { get; }
    public IReadOnlyList<InnateAptitudeSaveData> InnateAptitudes { get; }
    public float ProgressRatio => definition.TotalDurationDays > 0
        ? Math.Clamp(TotalProgressDays / (float)definition.TotalDurationDays, 0f, 1f)
        : 0f;

    public void AdvanceDay(ReproductionDailyContext context, double miscarriageRandom)
    {
        if (Status is ReproductionProcessStatus.Completed or ReproductionProcessStatus.Failed)
            return;
        if (Status == ReproductionProcessStatus.WaitingForEmergencyExtraction)
        {
            if (context.AbsoluteDay - CarrierDeathAbsoluteDay > 1)
                Fail(ReproductionFailureCode.EmergencyExtractionExpired);
            return;
        }

        if (Mode == ReproductionMode.Pregnancy
            && definition.Phases[CurrentPhaseIndex].phase
                == ReproductionPhaseKind.Pregnancy
            && !EmergencyExtracted
            && (context.CarrierHealth < 30f || context.CarrierNutrition < 20f)
            && Math.Clamp(miscarriageRandom, 0d, 0.999999d) < 0.10d)
        {
            Fail(ReproductionFailureCode.Miscarriage);
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
            return;
        }

        ConsecutiveUnsafeTemperatureDays = 0;
        Status = ReproductionProcessStatus.Active;
        CurrentPhaseElapsedDays++;
        TotalProgressDays++;
        int phaseDuration = definition.Phases[CurrentPhaseIndex].durationDays;
        if (CurrentPhaseElapsedDays < phaseDuration) return;
        CurrentPhaseIndex++;
        CurrentPhaseElapsedDays = 0;
        if (CurrentPhaseIndex >= definition.Phases.Count)
            Status = ReproductionProcessStatus.Completed;
    }

    public void NotifyCarrierDeath(int absoluteDay)
    {
        if (Mode != ReproductionMode.Pregnancy || Status != ReproductionProcessStatus.Active)
            return;
        CarrierDeathAbsoluteDay = absoluteDay;
        if (ProgressRatio >= 0.8f)
            Status = ReproductionProcessStatus.WaitingForEmergencyExtraction;
        else
            Fail(ReproductionFailureCode.CarrierDied);
    }

    public void EmergencyExtract(int absoluteDay)
    {
        if (Status != ReproductionProcessStatus.WaitingForEmergencyExtraction
            || absoluteDay - CarrierDeathAbsoluteDay > 1)
            throw new InvalidOperationException("Emergency extraction is not available.");
        EmergencyExtracted = true;
        Status = ReproductionProcessStatus.Active;
    }

    public void MarkResultPublished(CharacterId resultCharacterId)
    {
        if (Status != ReproductionProcessStatus.Completed)
            throw new InvalidOperationException("Only a completed reproduction process can publish its result.");
        if (ResultPublished)
            throw new InvalidOperationException("Reproduction result was already published.");
        if (!resultCharacterId.IsValid)
            throw new ArgumentException("A published reproduction result requires a valid character ID.");
        ResultPublished = true;
        ResultCharacterId = resultCharacterId;
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
        resultPublished = ResultPublished,
        resultCharacterId = ResultCharacterId.Value,
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
            || data.consecutiveUnsafeTemperatureDays < 0)
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
            data.expressedTraitIds,
            data.latentTraitIds,
            data.innateAptitudes)
        {
            Status = data.status,
            Failure = data.failure,
            CurrentPhaseIndex = data.currentPhaseIndex,
            CurrentPhaseElapsedDays = data.currentPhaseElapsedDays,
            TotalProgressDays = data.totalProgressDays,
            ConsecutiveUnsafeTemperatureDays = data.consecutiveUnsafeTemperatureDays,
            CarrierDeathAbsoluteDay = data.carrierDeathAbsoluteDay,
            EmergencyExtracted = data.emergencyExtracted,
            ResultPublished = data.resultPublished,
            ResultCharacterId = string.IsNullOrWhiteSpace(data.resultCharacterId)
                ? default
                : new CharacterId(data.resultCharacterId)
        };
        if (process.TotalProgressDays > definition.TotalDurationDays
            || process.CurrentPhaseIndex < definition.Phases.Count
                && process.CurrentPhaseElapsedDays >= definition.Phases[process.CurrentPhaseIndex].durationDays
            || process.Status == ReproductionProcessStatus.Completed
                && process.CurrentPhaseIndex != definition.Phases.Count
            || process.ResultPublished
                && (process.Status != ReproductionProcessStatus.Completed
                    || !process.ResultCharacterId.IsValid)
            || !process.ResultPublished && process.ResultCharacterId.IsValid)
            throw new InvalidOperationException("Reproduction process progress is inconsistent.");
        return process;
    }

    private void Fail(ReproductionFailureCode failure)
    {
        Failure = failure;
        Status = ReproductionProcessStatus.Failed;
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
    public FamilyPlanningPolicy FamilyPlanningPolicy { get; private set; } = FamilyPlanningPolicy.Planned;
    public int LastAllowedPolicyEvaluationDay { get; private set; }
    public IReadOnlyList<ReproductionProcess> Processes => processes.Values
        .OrderBy(value => value.ProcessId, StringComparer.Ordinal).ToArray();

    public void SetFamilyPlanningPolicy(FamilyPlanningPolicy policy)
    {
        if (!Enum.IsDefined(typeof(FamilyPlanningPolicy), policy))
            throw new ArgumentOutOfRangeException(nameof(policy));
        FamilyPlanningPolicy = policy;
    }

    public void Add(ReproductionProcess process)
    {
        if (process == null || !processes.TryAdd(process.ProcessId, process))
            throw new InvalidOperationException("Reproduction process is null or duplicated.");
    }

    public bool TryGet(string processId, out ReproductionProcess process) =>
        processes.TryGetValue(processId?.Trim() ?? string.Empty, out process);

    public void MarkAllowedPolicyEvaluation(int absoluteDay)
    {
        if (absoluteDay < LastAllowedPolicyEvaluationDay)
            throw new InvalidOperationException("Family-planning evaluation day cannot move backward.");
        LastAllowedPolicyEvaluationDay = absoluteDay;
    }

    public ReproductionWorldSaveData Capture() => new()
    {
        familyPlanningPolicy = FamilyPlanningPolicy,
        lastAllowedPolicyEvaluationDay = LastAllowedPolicyEvaluationDay,
        processes = Processes.Select(value => value.Capture()).ToList()
    };

    public static ReproductionWorldAggregate Restore(
        ReproductionWorldSaveData data,
        IReproductionDefinitionCatalog definitions)
    {
        if (data == null || data.version != ReproductionWorldSaveData.CurrentVersion
            || data.processes == null || data.lastAllowedPolicyEvaluationDay < 0
            || !Enum.IsDefined(typeof(FamilyPlanningPolicy), data.familyPlanningPolicy))
            throw new InvalidOperationException("Reproduction payload is incomplete or unsupported.");
        ReproductionWorldAggregate result = new()
        {
            FamilyPlanningPolicy = data.familyPlanningPolicy,
            LastAllowedPolicyEvaluationDay = data.lastAllowedPolicyEvaluationDay
        };
        foreach (ReproductionProcessSaveData source in data.processes)
        {
            CharacterSpeciesId species = new(source?.phenotypeSpeciesId);
            ReproductionProcess process = ReproductionProcess.Restore(
                source,
                definitions.RequireReproduction(species));
            result.Add(process);
        }
        return result;
    }
}

public interface IReproductionService
{
    FamilyPlanningPolicy FamilyPlanningPolicy { get; }
    IReadOnlyList<ReproductionProcess> Processes { get; }
    void SetFamilyPlanningPolicy(FamilyPlanningPolicy policy);
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
