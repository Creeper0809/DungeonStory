using System;
using System.Collections.Generic;
using UnityEngine;

public static class ProcedureOperatorRequirementRuntimeExtensions
{
    public static bool IsQualified(
        this ProcedureOperatorRequirement requirement,
        CharacterActor actor,
        MedicalProcedureFamily family,
        out float weightedScore,
        out DomainFailure failure)
    {
        failure = DomainFailure.None;
        weightedScore = 0f;
        if (actor == null)
        {
            failure = new DomainFailure(FailureCode.SurgeryOperatorMissing);
            return false;
        }

        IReadOnlyList<ProcedureOperatorStatRequirement> requirements =
            requirement != null && requirement.IsConfigured
                ? requirement.Stats
                : BuildDefaults(family);
        float totalWeight = 0f;
        foreach (ProcedureOperatorStatRequirement statRequirement in requirements)
        {
            if (statRequirement == null
                || string.IsNullOrWhiteSpace(statRequirement.statId)
                || statRequirement.weight <= 0f)
            {
                continue;
            }

            int value = actor.Stats != null
                ? actor.Stats.GetCharacterStat(statRequirement.statId)
                : 0;
            if (value < statRequirement.minimumValue)
            {
                failure = new DomainFailure(
                    FailureCode.SurgeryOperatorStatInsufficient,
                    statRequirement.statId,
                    statRequirement.minimumValue.ToString(),
                    value.ToString());
                return false;
            }

            weightedScore += value * statRequirement.weight;
            totalWeight += statRequirement.weight;
        }

        weightedScore = totalWeight > 0f ? weightedScore / totalWeight : 0f;
        float required = requirement != null && requirement.IsConfigured
            ? requirement.MinimumWeightedScore
            : 3f;
        if (weightedScore + 0.0001f < required)
        {
            failure = new DomainFailure(
                FailureCode.SurgeryOperatorSkillInsufficient,
                required.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture),
                weightedScore.ToString(
                    "0.#",
                    System.Globalization.CultureInfo.InvariantCulture));
            return false;
        }

        return true;
    }

    public static float GetWorkSpeedMultiplier(
        this ProcedureOperatorRequirement requirement,
        CharacterActor actor,
        MedicalProcedureFamily family)
    {
        requirement.IsQualified(actor, family, out float score, out _);
        return Mathf.Clamp(0.55f + score * 0.09f, 0.45f, 2.5f);
    }

    public static IReadOnlyList<string> Validate(
        this ProcedureOperatorRequirement requirement,
        string procedureId)
    {
        List<string> errors = new();
        if (requirement == null || !requirement.IsConfigured)
        {
            return errors;
        }

        foreach (ProcedureOperatorStatRequirement statRequirement in requirement.Stats)
        {
            if (statRequirement == null
                || string.IsNullOrWhiteSpace(statRequirement.statId)
                || statRequirement.weight <= 0f)
            {
                errors.Add($"{procedureId}: invalid operator stat requirement.");
                continue;
            }

            if (!CharacterStatCatalog.TryGet(statRequirement.statId, out _))
            {
                errors.Add(
                    $"{procedureId}: unknown operator stat '{statRequirement.statId}'.");
            }
        }

        return errors;
    }

    private static IReadOnlyList<ProcedureOperatorStatRequirement> BuildDefaults(
        MedicalProcedureFamily family)
    {
        return family switch
        {
            MedicalProcedureFamily.Construct => new[]
            {
                Stat(CharacterStatIds.Dexterity, 0.45f),
                Stat(CharacterStatIds.Strength, 0.25f),
                Stat(CharacterStatIds.Research, 0.30f)
            },
            MedicalProcedureFamily.Slime or MedicalProcedureFamily.Myconid => new[]
            {
                Stat(CharacterStatIds.Medical, 0.45f),
                Stat(CharacterStatIds.Dexterity, 0.35f),
                Stat(CharacterStatIds.Research, 0.20f)
            },
            MedicalProcedureFamily.Demonic or MedicalProcedureFamily.Arcane => new[]
            {
                Stat(CharacterStatIds.Medical, 0.35f),
                Stat(CharacterStatIds.Dexterity, 0.25f),
                Stat(CharacterStatIds.Research, 0.40f)
            },
            _ => new[]
            {
                Stat(CharacterStatIds.Medical, 0.65f),
                Stat(CharacterStatIds.Dexterity, 0.25f),
                Stat(CharacterStatIds.Research, 0.10f)
            }
        };
    }

    private static ProcedureOperatorStatRequirement Stat(string id, float weight)
    {
        return new ProcedureOperatorStatRequirement { statId = id, weight = weight };
    }

    private static string ResolveStatLabel(string statId)
    {
        return CharacterStatCatalog.TryGet(statId, out CharacterStatDefinition definition)
            ? definition.DisplayName
            : statId;
    }
}

public readonly struct SurgicalFacilitySnapshot
{
    public SurgicalFacilitySnapshot(
        BuildableObject primaryFacility,
        SurgeryFacilityTag availableTags,
        float sterility,
        float speedMultiplier,
        float successBonus,
        float anesthesiaBonus,
        IReadOnlyList<BuildableObject> supportFacilities,
        DomainFailure blockFailure)
    {
        PrimaryFacility = primaryFacility;
        AvailableTags = availableTags;
        Sterility = Mathf.Clamp01(sterility);
        SpeedMultiplier = Mathf.Clamp(speedMultiplier, 0.25f, 3f);
        SuccessBonus = Mathf.Clamp(successBonus, -0.25f, 0.35f);
        AnesthesiaBonus = Mathf.Clamp01(anesthesiaBonus);
        SupportFacilities = supportFacilities ?? Array.Empty<BuildableObject>();
        BlockFailure = blockFailure;
    }

    public BuildableObject PrimaryFacility { get; }
    public SurgeryFacilityTag AvailableTags { get; }
    public float Sterility { get; }
    public float SpeedMultiplier { get; }
    public float SuccessBonus { get; }
    public float AnesthesiaBonus { get; }
    public IReadOnlyList<BuildableObject> SupportFacilities { get; }
    public DomainFailure BlockFailure { get; }
    public bool IsAvailable => PrimaryFacility != null
        && !BlockFailure.IsFailure;
}

public interface ISurgicalFacilityQuery
{
    SurgicalFacilitySnapshot Evaluate(
        BuildableObject primaryFacility,
        SurgeryFacilityTag requiredTags);
    bool TryFindBestFacility(
        SurgicalSubjectRef subject,
        SurgicalProcedureSO procedure,
        out SurgicalFacilitySnapshot facility,
        out DomainFailure failure);
    IReadOnlyList<SurgicalFacilitySnapshot> GetCandidateFacilities(
        SurgicalProcedureSO procedure,
        bool includeBlocked = false);
    string GetFacilityId(BuildableObject facility);
}

public interface ISurgicalPatientTransportRuntime
{
    bool EnsureWildlifeAdmission(
        SurgeryOrder order,
        WildlifeActor patient,
        Vector2Int destination,
        out SurgeryStatusData status);
    void RequestWildlifeReturn(SurgeryOrder order);
    void CancelTransport(SurgeryOrder order);
    bool TryGetTransport(
        string orderId,
        CharacterActor carrier,
        out WildlifeActor patient,
        out Vector2Int destination,
        out bool returning,
        out DomainFailure failure);
    IDisposable BeginTransportPass(
        CharacterActor carrier,
        string orderId);
    bool TryBeginCarry(
        string orderId,
        CharacterActor carrier,
        out DomainFailure failure);
    bool TryCompleteCarry(
        string orderId,
        CharacterActor carrier,
        out DomainFailure failure);
    void FailCarry(
        string orderId,
        CharacterActor carrier);
}

public interface ISurgeryRiskEvaluator
{
    SurgeryRiskBreakdown Evaluate(
        CharacterActor doctor,
        SurgicalSubjectRef subject,
        SurgicalProcedureSO procedure,
        SurgicalFacilitySnapshot facility,
        float patientInstability,
        float compatibilityPenalty);
}

public interface ISurgeryEnvironmentRiskEvaluator
{
    SurgeryEnvironmentRiskSnapshot Evaluate(
        Vector2Int facilityPosition,
        CharacterActor doctor,
        SurgicalSubjectRef subject);
    SurgeryRiskBreakdown Apply(
        SurgeryRiskBreakdown baseline,
        SurgeryEnvironmentRiskSnapshot snapshot,
        float stageWeight);
}

public interface ISurgicalPartRuntime
{
    IReadOnlyList<SurgicalPartInstance> Parts { get; }
    bool TryGet(string partInstanceId, out SurgicalPartInstance part);
    bool TryCreateExtractedPart(
        SurgicalSubjectRef donor,
        string nodeId,
        SurgicalPartKind kind,
        float quality,
        Vector2Int position,
        out SurgicalPartInstance part,
        out DomainFailure failure);
    bool TryCreateCraftedPart(
        string nodeId,
        string displayName,
        SurgicalPartKind kind,
        float quality,
        Vector2Int position,
        out SurgicalPartInstance part,
        out DomainFailure failure);
    bool TryReserveForOrder(
        string partInstanceId,
        string orderId,
        out DomainFailure failure);
    void ReleaseReservation(string partInstanceId, string orderId);
    bool TryConsumeForInstallation(
        string partInstanceId,
        string orderId,
        string subjectId,
        out SurgicalPartInstance part,
        out DomainFailure failure);
    void TickFreshness(float deltaTime);
    IReadOnlyList<SurgicalPartInstance> CaptureParts();
    IReadOnlyList<SurgicalOrganStorageState> CaptureStorageStates();
    bool TryGetOrganStorageStatus(
        BuildableObject storage,
        out SurgicalOrganStorageSnapshot snapshot);
}

public interface ISurgicalAugmentationQuery
{
    int GetStatBonus(string subjectId, CharacterStatType statType);
    string GetSpecialEffectLabel(SurgicalPartInstance part);
}

public interface ISurgeryQuery
{
    IReadOnlyList<SurgeryOrder> ActiveOrders { get; }
    bool TryGetOrder(string orderId, out SurgeryOrder order);
    bool HasWorkFor(BuildableObject facility);
    bool TryGetWorkFor(
        BuildableObject facility,
        out SurgeryOrder order);
    bool CanOperate(
        SurgeryOrder order,
        CharacterActor doctor,
        out DomainFailure failure);
}

public interface ISurgeryWorkCommand
{
    bool TryReserveWork(
        BuildableObject facility,
        CharacterActor doctor,
        out SurgeryOrder order,
        out DomainFailure failure);
    bool ApplyWork(
        string orderId,
        CharacterActor doctor,
        float work,
        out bool completed,
        out DomainFailure failure);
    void ReleaseDoctor(string orderId, CharacterActor doctor);
}

public interface ISurgeryPersistence
{
    DungeonSurgerySaveData Capture();
}

public sealed class SurgeryRestoreCandidate
{
    internal SurgeryRestoreCandidate(SurgeryAggregateState state)
    {
        State = state ?? throw new ArgumentNullException(nameof(state));
    }

    internal SurgeryAggregateState State { get; }
}

public interface ISurgicalProcedureEffectHandler
{
    Type EffectType { get; }
    bool Apply(
        SurgeryOrder order,
        SurgicalProcedureEffect effect,
        BuildableObject facility,
        out DomainFailure failure);
}
