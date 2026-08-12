using System;
using System.Collections.Generic;

public enum RegularCustomerStatus
{
    Visitor,
    Regular,
    RecruitCandidate,
    Recruited
}

public enum SettlementImmigrationPolicy
{
    Conservative = 0,
    Balanced = 1,
    Open = 2
}

[Flags]
public enum RecruitCapability
{
    None = 0,
    Staff = 1 << 0,
    Defense = 1 << 1,
    Expedition = 1 << 2,
    All = Staff | Defense | Expedition
}

public static class RecruitProficiencyCatchUpRules
{
    public const int SpecializedProficiencyCount = 2;

    public static int ResolvePrimaryExperienceFloor(int completedTargets) =>
        Math.Clamp(completedTargets, 0, 6) switch
        {
            0 => 0,
            1 => 100,
            2 => 250,
            3 => 400,
            _ => 600
        };
}

[Serializable]
public sealed class DungeonRegularCustomerSaveData
{
    public const int CurrentVersion = 3;
    public int version = CurrentVersion;
    public SettlementImmigrationPolicy immigrationPolicy =
        SettlementImmigrationPolicy.Balanced;
    public List<DungeonRegularCustomerRecordSaveData> records = new();
}

[Serializable]
public sealed class DungeonRegularCustomerRecordSaveData
{
    public string customerId = string.Empty;
    public string displayName = string.Empty;
    public string speciesTag = string.Empty;
    public int sourceDataId = -1;
    public int visitCount;
    public float averageSatisfaction;
    public bool isRegular;
    public bool isRecruitCandidate;
    public bool isRecruited;
    public int recruitedAbsoluteDay;
    public RecruitCapability recruitCapabilities;
}

public interface IRegularCustomerPersistence
{
    DungeonRegularCustomerSaveData CaptureState();
    RegularCustomerRestoreCandidate PrepareRestore(
        DungeonRegularCustomerSaveData snapshot);
    void PublishRestore(RegularCustomerRestoreCandidate candidate);
}

public abstract class RegularCustomerRestoreCandidate
{
    protected RegularCustomerRestoreCandidate()
    {
    }
}

public interface IRecruitmentCharacterDefinitionCatalog
{
    IReadOnlyCollection<int> CharacterDefinitionIds { get; }
}

[Serializable]
public sealed class RegularCustomerRules
{
    public int regularVisitThreshold = 2;
    public float regularAverageSatisfactionThreshold = 65f;
    public int recruitCandidateVisitThreshold = 2;
    public float recruitCandidateAverageSatisfactionThreshold = 65f;
    public int recruitmentCooldownDays = 10;
    public RecruitCapability defaultRecruitCapabilities = RecruitCapability.All;
    public static RegularCustomerRules CreateDefault() => new();
}

public static class RegularCustomerProgressionRules
{
    public static bool MeetsRegularCondition(
        int visitCount,
        float averageSatisfaction,
        RegularCustomerRules rules)
    {
        rules ??= RegularCustomerRules.CreateDefault();
        return visitCount >= Math.Max(1, rules.regularVisitThreshold)
            && averageSatisfaction >= ClampSatisfaction(
                rules.regularAverageSatisfactionThreshold);
    }

    public static bool MeetsRecruitCandidateCondition(
        int visitCount,
        float averageSatisfaction,
        RegularCustomerRules rules)
    {
        rules ??= RegularCustomerRules.CreateDefault();
        return MeetsRegularCondition(visitCount, averageSatisfaction, rules)
            && visitCount >= Math.Max(1, rules.recruitCandidateVisitThreshold)
            && averageSatisfaction >= ClampSatisfaction(
                rules.recruitCandidateAverageSatisfactionThreshold);
    }

    public static RegularCustomerStatus ResolveStatus(
        bool isRegular,
        bool isRecruitCandidate,
        bool isRecruited) =>
        isRecruited
            ? RegularCustomerStatus.Recruited
            : isRecruitCandidate
                ? RegularCustomerStatus.RecruitCandidate
                : isRegular
                    ? RegularCustomerStatus.Regular
                    : RegularCustomerStatus.Visitor;

    private static float ClampSatisfaction(float value) =>
        Math.Max(0f, Math.Min(100f, value));
}

public sealed class RegularCustomerProgressState
{
    private float satisfactionTotal;

    public RegularCustomerProgressState(
        string customerId,
        string displayName,
        string speciesTag,
        int visitCount,
        float averageSatisfaction,
        bool isRegular,
        bool isRecruitCandidate,
        bool isRecruited,
        int recruitedAbsoluteDay,
        RecruitCapability recruitCapabilities)
    {
        CustomerId = customerId?.Trim() ?? string.Empty;
        DisplayName = string.IsNullOrWhiteSpace(displayName)
            ? $"Customer {CustomerId}"
            : displayName;
        SpeciesTag = string.IsNullOrWhiteSpace(speciesTag)
            ? "Unknown"
            : speciesTag;
        VisitCount = Math.Max(0, visitCount);
        satisfactionTotal = ClampSatisfaction(averageSatisfaction) * VisitCount;
        IsRegular = isRegular || isRecruitCandidate || isRecruited;
        IsRecruitCandidate = isRecruitCandidate || isRecruited;
        IsRecruited = isRecruited;
        RecruitedAbsoluteDay = IsRecruited
            ? Math.Max(1, recruitedAbsoluteDay)
            : 0;
        RecruitCapabilities = recruitCapabilities == RecruitCapability.None
            ? RecruitCapability.All
            : recruitCapabilities;
    }

    public string CustomerId { get; }
    public string DisplayName { get; private set; }
    public string SpeciesTag { get; private set; }
    public int VisitCount { get; private set; }
    public float AverageSatisfaction =>
        VisitCount > 0 ? satisfactionTotal / VisitCount : 0f;
    public bool IsRegular { get; private set; }
    public bool IsRecruitCandidate { get; private set; }
    public bool IsRecruited { get; private set; }
    public int RecruitedAbsoluteDay { get; private set; }
    public RecruitCapability RecruitCapabilities { get; }
    public RegularCustomerStatus Status =>
        RegularCustomerProgressionRules.ResolveStatus(
            IsRegular,
            IsRecruitCandidate,
            IsRecruited);

    public void UpdateIdentity(string displayName, string speciesTag)
    {
        if (!string.IsNullOrWhiteSpace(displayName))
        {
            DisplayName = displayName;
        }
        if (!string.IsNullOrWhiteSpace(speciesTag))
        {
            SpeciesTag = speciesTag;
        }
    }

    public void RecordVisit(float satisfaction, RegularCustomerRules rules)
    {
        RecordVisit(satisfaction, rules, allowRecruitCandidate: true);
    }

    public void RecordVisit(
        float satisfaction,
        RegularCustomerRules rules,
        bool allowRecruitCandidate)
    {
        rules ??= RegularCustomerRules.CreateDefault();
        VisitCount++;
        satisfactionTotal += ClampSatisfaction(satisfaction);
        if (!IsRegular
            && RegularCustomerProgressionRules.MeetsRegularCondition(
                VisitCount,
                AverageSatisfaction,
                rules))
        {
            IsRegular = true;
        }
        if (allowRecruitCandidate
            && !IsRecruitCandidate
            && RegularCustomerProgressionRules.MeetsRecruitCandidateCondition(
                VisitCount,
                AverageSatisfaction,
                rules))
        {
            IsRegular = true;
            IsRecruitCandidate = true;
        }
    }

    public bool MarkRecruitCandidate()
    {
        if (IsRecruited || IsRecruitCandidate)
        {
            return false;
        }
        IsRegular = true;
        IsRecruitCandidate = true;
        return true;
    }

    public bool MarkRecruited(int absoluteDay)
    {
        if (IsRecruited || !IsRecruitCandidate)
        {
            return false;
        }
        IsRegular = true;
        IsRecruited = true;
        RecruitedAbsoluteDay = Math.Max(1, absoluteDay);
        return true;
    }

    public RegularCustomerProgressState DeepClone() => new(
        CustomerId,
        DisplayName,
        SpeciesTag,
        VisitCount,
        AverageSatisfaction,
        IsRegular,
        IsRecruitCandidate,
        IsRecruited,
        RecruitedAbsoluteDay,
        RecruitCapabilities);

    private static float ClampSatisfaction(float value) =>
        Math.Max(0f, Math.Min(100f, value));
}
