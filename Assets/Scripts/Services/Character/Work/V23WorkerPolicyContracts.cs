using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

public interface IWorkerNarrativeQualificationQuery
{
    int GetSkillExperience(string characterId, string skillId);
    CareerRank GetCareerRank(string characterId, string skillId);
    bool HasTrait(string characterId, string traitId);
}

public readonly struct EligibleWorkerCandidate
{
    public EligibleWorkerCandidate(
        CharacterActor actor,
        float estimatedWorkSpeed,
        float estimatedQualitySkill,
        float distance,
        float estimatedWorkload)
    {
        Actor = actor;
        EstimatedWorkSpeed = Mathf.Max(0f, estimatedWorkSpeed);
        EstimatedQualitySkill = Mathf.Clamp(
            estimatedQualitySkill,
            0f,
            100f);
        Distance = Mathf.Max(0f, distance);
        EstimatedWorkload = Mathf.Max(0f, estimatedWorkload);
    }

    public CharacterActor Actor { get; }
    public float EstimatedWorkSpeed { get; }
    public float EstimatedQualitySkill { get; }
    public float Distance { get; }
    public float EstimatedWorkload { get; }
}

public interface IEligibleWorkerQuery
{
    int FindCandidates(
        WorkerSelectionPolicySaveData policy,
        string performanceFormulaId,
        Vector2Int workPosition,
        Span<EligibleWorkerCandidate> destination);
}

/// <summary>
/// Shared bounded worker index for construction and crafting panels. The
/// caller owns the destination buffer, so candidate previews do not allocate.
/// Domain safety remains in the normal work-policy gate when a worker claims
/// the job.
/// </summary>
public sealed class EligibleWorkerQuery : IEligibleWorkerQuery
{
    private readonly ICharacterWorldQuery world;
    private readonly IWorkerNarrativeQualificationQuery narrative;
    private readonly ICharacterPerformanceQuery performance;

    public EligibleWorkerQuery(
        ICharacterWorldQuery world,
        IWorkerNarrativeQualificationQuery narrative = null,
        ICharacterPerformanceQuery performance = null)
    {
        this.world = world ?? throw new ArgumentNullException(nameof(world));
        this.narrative = narrative;
        this.performance = performance
            ?? throw new ArgumentNullException(nameof(performance));
    }

    public int FindCandidates(
        WorkerSelectionPolicySaveData policy,
        string performanceFormulaId,
        Vector2Int workPosition,
        Span<EligibleWorkerCandidate> destination)
    {
        if (destination.Length == 0)
        {
            return 0;
        }
        WorkerSelectionPolicySaveData normalized = policy?.CloneNormalized()
            ?? WorkerSelectionPolicySaveData.Anyone();
        int count = 0;
        foreach (CharacterActor actor in world.Characters)
        {
            if (actor == null
                || actor.IsDead
                || !WorkerSelectionPolicyRules.IsEligible(
                    normalized,
                    actor,
                    narrative,
                    out _))
            {
                continue;
            }
            if (performance == null)
                throw new InvalidOperationException(
                    "Worker candidate ranking requires the character performance query.");
            float stat = Mathf.Max(
                0f,
                performance.Evaluate(actor, performanceFormulaId).Value);
            float workload = CharacterWorkRoleUtility.TryGetWork(
                    actor,
                    out AbilityWork work)
                && work.isWorking
                    ? 1f
                    : 0f;
            EligibleWorkerCandidate candidate = new(
                actor,
                stat,
                stat * 50f,
                Vector2.Distance(actor.transform.position, workPosition),
                workload);
            InsertSorted(normalized, candidate, destination, ref count);
        }
        return count;
    }

    private static void InsertSorted(
        WorkerSelectionPolicySaveData policy,
        EligibleWorkerCandidate candidate,
        Span<EligibleWorkerCandidate> destination,
        ref int count)
    {
        int limit = Mathf.Min(count, destination.Length - 1);
        int insertAt = limit;
        while (insertAt > 0
            && Compare(policy, candidate, destination[insertAt - 1]) < 0)
        {
            if (insertAt < destination.Length)
            {
                destination[insertAt] = destination[insertAt - 1];
            }
            insertAt--;
        }
        if (insertAt < destination.Length)
        {
            destination[insertAt] = candidate;
        }
        count = Mathf.Min(destination.Length, count + 1);
    }

    private static int Compare(
        WorkerSelectionPolicySaveData policy,
        EligibleWorkerCandidate left,
        EligibleWorkerCandidate right)
    {
        int comparison = policy.sortMode switch
        {
            WorkerCandidateSortMode.Fastest =>
                right.EstimatedWorkSpeed.CompareTo(left.EstimatedWorkSpeed),
            WorkerCandidateSortMode.Nearest =>
                left.Distance.CompareTo(right.Distance),
            WorkerCandidateSortMode.LeastWorkload =>
                left.EstimatedWorkload.CompareTo(right.EstimatedWorkload),
            _ => right.EstimatedQualitySkill.CompareTo(
                left.EstimatedQualitySkill)
        };
        if (comparison != 0)
        {
            return comparison;
        }
        return string.CompareOrdinal(
            left.Actor?.Identity?.PersistentId,
            right.Actor?.Identity?.PersistentId);
    }
}

public static class WorkerSelectionPolicyRules
{
    public static bool IsEligible(
        WorkerSelectionPolicySaveData source,
        CharacterActor actor,
        IWorkerNarrativeQualificationQuery narrative,
        out string reason)
    {
        reason = string.Empty;
        if (actor == null || actor.Identity == null)
        {
            reason = "worker identity missing";
            return false;
        }

        WorkerSelectionPolicySaveData policy = source?.CloneNormalized()
            ?? WorkerSelectionPolicySaveData.Anyone();
        string characterId = actor.Identity.PersistentId?.Trim() ?? string.Empty;
        if (characterId.Length == 0)
        {
            reason = "worker persistent id missing";
            return false;
        }
        if (policy.excludedCharacterIds.Contains(characterId, StringComparer.Ordinal))
        {
            reason = "worker explicitly excluded";
            return false;
        }

        bool specified = policy.specificCharacterIds.Contains(
            characterId,
            StringComparer.Ordinal);
        bool rulesMatch = MatchesRules(policy, actor, narrative, characterId);
        bool eligible = policy.mode switch
        {
            WorkerSelectionMode.Anyone => true,
            WorkerSelectionMode.SpecificCharacters => specified,
            WorkerSelectionMode.RuleSet => rulesMatch,
            WorkerSelectionMode.SpecificOrRuleSet => specified || rulesMatch,
            _ => false
        };
        if (!eligible)
        {
            reason = "worker does not satisfy the order policy";
        }
        return eligible;
    }

    private static bool MatchesRules(
        WorkerSelectionPolicySaveData policy,
        CharacterActor actor,
        IWorkerNarrativeQualificationQuery narrative,
        string characterId)
    {
        List<bool> results = new();
        string skillId = policy.minimumSkillId?.Trim() ?? string.Empty;
        if (skillId.Length > 0 || policy.minimumSkillExperience > 0
            || policy.minimumCareerRank > (int)CareerRank.Apprentice)
        {
            results.Add(narrative != null
                && narrative.GetSkillExperience(characterId, skillId)
                    >= Mathf.Max(0, policy.minimumSkillExperience)
                && (int)narrative.GetCareerRank(characterId, skillId)
                    >= policy.minimumCareerRank);
        }

        foreach (string traitId in policy.requiredTraitIds ?? new List<string>())
        {
            results.Add(narrative != null && narrative.HasTrait(characterId, traitId));
        }
        foreach (string traitId in policy.excludedTraitIds ?? new List<string>())
        {
            results.Add(narrative == null || !narrative.HasTrait(characterId, traitId));
        }

        if (results.Count == 0)
        {
            return true;
        }
        return policy.matchMode == WorkerRequirementMatchMode.Any
            ? results.Any(value => value)
            : results.All(value => value);
    }
}

[Serializable]
public sealed class QualityTargetPipelineSaveData
{
    public string pipelineId = string.Empty;
    public string definitionId = string.Empty;
    public bool facilityPipeline;
    public CraftsmanshipQualityTier minimumQuality = CraftsmanshipQualityTier.Normal;
    [Min(1)] public int requiredAcceptedCount = 1;
    [Min(0)] public int acceptedCount;
    public bool countExistingQualifiedStock;
    public WorkerSelectionPolicySaveData workerPolicy =
        WorkerSelectionPolicySaveData.Anyone(
            WorkerCandidateSortMode.BestExpectedQuality);
    public RejectedOutputDisposition rejectedDisposition =
        RejectedOutputDisposition.AutoDismantle;
    public QualityRepeatLimitMode limitMode = QualityRepeatLimitMode.SafeLimits;
    [Min(1)] public int maximumAttempts = 10;
    [Min(0f)] public float workBudget;
    [Min(0f)] public float consumedWork;
    [Min(0)] public int attemptIndex;
    public List<ItemAmountDefinition> minimumMaterialReserve = new();
    public CraftQualityRollSaveData currentRoll;
    public QualityTargetPipelineStage stage =
        QualityTargetPipelineStage.WaitingForMaterials;
    public int footprintX;
    public int footprintY;
    public int footprintWidth = 1;
    public int footprintHeight = 1;

    public bool IsTerminal => stage is QualityTargetPipelineStage.Completed
        or QualityTargetPipelineStage.Cancelled;

    public bool HasReachedSafeLimit => limitMode == QualityRepeatLimitMode.SafeLimits
        && ((maximumAttempts > 0 && attemptIndex >= maximumAttempts)
            || (workBudget > 0f && consumedWork >= workBudget));

    public QualityTargetPipelineSaveData CloneNormalized() => new()
    {
        pipelineId = pipelineId?.Trim() ?? string.Empty,
        definitionId = definitionId?.Trim() ?? string.Empty,
        facilityPipeline = facilityPipeline,
        minimumQuality = minimumQuality,
        requiredAcceptedCount = Mathf.Max(1, requiredAcceptedCount),
        acceptedCount = Mathf.Max(0, acceptedCount),
        countExistingQualifiedStock = countExistingQualifiedStock,
        workerPolicy = workerPolicy?.CloneNormalized()
            ?? WorkerSelectionPolicySaveData.Anyone(
                WorkerCandidateSortMode.BestExpectedQuality),
        rejectedDisposition = rejectedDisposition,
        limitMode = limitMode,
        maximumAttempts = Mathf.Max(1, maximumAttempts),
        workBudget = Mathf.Max(0f, workBudget),
        consumedWork = Mathf.Max(0f, consumedWork),
        attemptIndex = Mathf.Max(0, attemptIndex),
        minimumMaterialReserve = (minimumMaterialReserve
                ?? new List<ItemAmountDefinition>())
            .Where(value => value != null && value.Amount > 0)
            .OrderBy(value => value.ItemId, StringComparer.Ordinal)
            .Select(value => new ItemAmountDefinition(value.ItemId, value.Amount))
            .ToList(),
        currentRoll = currentRoll == null ? null : new CraftQualityRollSaveData
        {
            attemptIndex = currentRoll.attemptIndex,
            randomA = currentRoll.randomA,
            randomB = currentRoll.randomB,
            randomC = currentRoll.randomC
        },
        stage = stage,
        footprintX = footprintX,
        footprintY = footprintY,
        footprintWidth = Mathf.Max(1, footprintWidth),
        footprintHeight = Mathf.Max(1, footprintHeight)
    };
}

public interface IQualityTargetPipelineQuery
{
    IReadOnlyList<QualityTargetPipelineSaveData> QualityPipelines { get; }
    bool TryGetQualityPipeline(
        string pipelineId,
        out QualityTargetPipelineSaveData pipeline);
}

public interface IQualityTargetPipelineCommand
{
    bool CreateForWorkOrder(
        string workOrderId,
        QualityTargetPipelineSaveData request,
        out string pipelineId,
        out DomainFailure failure);
    bool PauseQualityPipeline(string pipelineId, out DomainFailure failure);
    bool ResumeQualityPipeline(string pipelineId, out DomainFailure failure);
    bool CancelQualityPipeline(string pipelineId, out DomainFailure failure);
}
