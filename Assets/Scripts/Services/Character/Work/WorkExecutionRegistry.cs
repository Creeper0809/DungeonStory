using System;
using System.Collections.Generic;
using System.Collections;
using UnityEngine;

public interface IWorkCandidateProvider
{
    IReadOnlyCollection<WorkTypeId> WorkTypeIds { get; }
    bool IsAvailable(
        WorkTypeId workTypeId,
        CharacterActor actor,
        BuildableObject target,
        out string reason);
}

public interface IWorkUrgencyProvider
{
    IReadOnlyCollection<WorkTypeId> WorkTypeIds { get; }
    float GetUrgency(
        WorkTypeId workTypeId,
        CharacterActor actor,
        BuildableObject target);
}

public interface IWorkStatPolicy
{
    IReadOnlyCollection<WorkTypeId> WorkTypeIds { get; }
    float GetWorkSpeedMultiplier(CharacterActor actor, BuildableObject target);
}

public interface IWorkStatPolicyRegistry
{
    float GetStatMultiplier(
        WorkTypeId workTypeId,
        CharacterActor actor,
        BuildableObject target);
}

public interface IWorkAmountCalculator
{
    float CalculateWorkPerSecond(
        CharacterActor actor,
        BuildableObject target,
        WorkTypeId workTypeId,
        float environmentDurationMultiplier);
}

public sealed class WorkExecutionResult
{
    public bool CompletedSuccessfully { get; set; } = true;
    public bool CompletionEffectsAlreadyApplied { get; set; }
}

public sealed class WorkExecutionContext
{
    private readonly Func<float, string, float, IEnumerator> executeWorkAmount;
    private readonly Func<bool> canContinue;

    public WorkExecutionContext(
        int runId,
        AbilityWork work,
        CharacterActor actor,
        BuildableObject target,
        WorkTypeId workTypeId,
        Func<float, string, float, IEnumerator> executeWorkAmount,
        Func<bool> canContinue)
    {
        if (!workTypeId.IsValid)
        {
            throw new ArgumentException(
                "Work execution context requires a valid work type id.",
                nameof(workTypeId));
        }

        RunId = runId;
        Work = work ?? throw new ArgumentNullException(nameof(work));
        Actor = actor;
        Target = target;
        LegacyWorkType = WorkTypeCatalog.TryGet(
                workTypeId,
                out WorkTypeDefinition definition)
            ? definition.Type
            : FacilityWorkType.None;
        WorkTypeId = workTypeId;
        this.executeWorkAmount = executeWorkAmount
            ?? throw new ArgumentNullException(nameof(executeWorkAmount));
        this.canContinue = canContinue
            ?? throw new ArgumentNullException(nameof(canContinue));
    }

    public int RunId { get; }
    public AbilityWork Work { get; }
    public CharacterActor Actor { get; }
    public BuildableObject Target { get; }
    internal FacilityWorkType LegacyWorkType { get; }
    public WorkTypeId WorkTypeId { get; }
    public bool CanContinue => canContinue();

    public IEnumerator ExecuteWorkAmount(
        float requiredWork,
        string label,
        float extraMultiplier = 1f)
    {
        return executeWorkAmount(requiredWork, label, extraMultiplier);
    }
}

public interface IWorkExecutionHandler
{
    IReadOnlyCollection<WorkTypeId> WorkTypeIds { get; }
    IEnumerator Execute(WorkExecutionContext context, WorkExecutionResult result);
}

public interface IWorkExecutionHandlerRegistry
{
    bool TryGet(WorkTypeId workTypeId, out IWorkExecutionHandler handler);
}

public interface IWorkPolicyRegistry
{
    bool IsAvailable(
        WorkTypeId workTypeId,
        CharacterActor actor,
        BuildableObject target,
        out string reason);

    float GetAdditionalUrgency(
        WorkTypeId workTypeId,
        CharacterActor actor,
        BuildableObject target);
}

public sealed class WorkExecutionHandlerRegistry :
    IWorkExecutionHandlerRegistry,
    IWorkPolicyRegistry
{
    private readonly Dictionary<WorkTypeId, IWorkExecutionHandler> handlers;
    private readonly Dictionary<WorkTypeId, IWorkCandidateProvider> candidateProviders;
    private readonly Dictionary<WorkTypeId, IWorkUrgencyProvider> urgencyProviders;

    public WorkExecutionHandlerRegistry(
        IReadOnlyList<IWorkExecutionHandler> registeredHandlers,
        IReadOnlyList<IWorkCandidateProvider> registeredCandidateProviders,
        IReadOnlyList<IWorkUrgencyProvider> registeredUrgencyProviders)
    {
        handlers = BuildIndex(
            registeredHandlers,
            handler => handler.WorkTypeIds,
            "execution handler");
        candidateProviders = BuildIndex(
            registeredCandidateProviders,
            provider => provider.WorkTypeIds,
            "candidate provider");
        urgencyProviders = BuildIndex(
            registeredUrgencyProviders,
            provider => provider.WorkTypeIds,
            "urgency provider");
    }

    public bool TryGet(WorkTypeId workTypeId, out IWorkExecutionHandler handler)
    {
        return handlers.TryGetValue(workTypeId, out handler);
    }

    public bool IsAvailable(
        WorkTypeId workTypeId,
        CharacterActor actor,
        BuildableObject target,
        out string reason)
    {
        reason = string.Empty;
        return !WorkTypeCatalog.TryGet(workTypeId, out WorkTypeDefinition definition)
            || !candidateProviders.TryGetValue(definition.WorkTypeId, out IWorkCandidateProvider provider)
            || provider.IsAvailable(definition.WorkTypeId, actor, target, out reason);
    }

    public float GetAdditionalUrgency(
        WorkTypeId workTypeId,
        CharacterActor actor,
        BuildableObject target)
    {
        return WorkTypeCatalog.TryGet(workTypeId, out WorkTypeDefinition definition)
            && urgencyProviders.TryGetValue(definition.WorkTypeId, out IWorkUrgencyProvider provider)
                ? provider.GetUrgency(definition.WorkTypeId, actor, target)
                : 0f;
    }

    private static Dictionary<WorkTypeId, TProvider> BuildIndex<TProvider>(
        IReadOnlyList<TProvider> providers,
        Func<TProvider, IReadOnlyCollection<WorkTypeId>> getIds,
        string providerLabel)
        where TProvider : class
    {
        Dictionary<WorkTypeId, TProvider> index =
            new Dictionary<WorkTypeId, TProvider>();
        foreach (TProvider provider in providers ?? Array.Empty<TProvider>())
        {
            if (provider == null)
            {
                throw new InvalidOperationException(
                    $"A null work {providerLabel} was registered.");
            }

            IReadOnlyCollection<WorkTypeId> ids = getIds(provider);
            if (ids == null || ids.Count == 0)
            {
                throw new InvalidOperationException(
                    $"{provider.GetType().Name} does not declare a work type id.");
            }

            foreach (WorkTypeId id in ids)
            {
                if (!id.IsValid)
                {
                    throw new InvalidOperationException(
                        $"{provider.GetType().Name} declares an invalid work type id.");
                }

                if (!index.TryAdd(id, provider))
                {
                    throw new InvalidOperationException(
                        $"Duplicate work {providerLabel} id '{id}'.");
                }
            }
        }

        return index;
    }
}

public abstract class CharacterStatWorkPolicy : IWorkStatPolicy
{
    private readonly WorkTypeId[] workTypeIds;
    private readonly CharacterStatType[] stats;

    protected CharacterStatWorkPolicy(
        WorkTypeId[] workTypeIds,
        params CharacterStatType[] stats)
    {
        this.workTypeIds = workTypeIds != null && workTypeIds.Length > 0
            ? (WorkTypeId[])workTypeIds.Clone()
            : throw new ArgumentException(
                "At least one work type id is required.",
                nameof(workTypeIds));
        foreach (WorkTypeId id in this.workTypeIds)
        {
            if (!id.IsValid)
            {
                throw new ArgumentException(
                    "Every work type id must be valid.",
                    nameof(workTypeIds));
            }
        }

        this.stats = stats != null && stats.Length > 0
            ? (CharacterStatType[])stats.Clone()
            : throw new ArgumentException("At least one character stat is required.", nameof(stats));
    }

    public IReadOnlyCollection<WorkTypeId> WorkTypeIds => workTypeIds;

    public float GetWorkSpeedMultiplier(CharacterActor actor, BuildableObject target)
    {
        if (actor == null)
        {
            return 1f;
        }

        float total = 0f;
        foreach (CharacterStatType stat in stats)
        {
            total += actor.GetCharacterStat(stat);
        }

        float average = total / stats.Length;
        return Mathf.Clamp(0.55f + average * 0.09f, 0.45f, 2.5f);
    }
}

public sealed class ConstructionRepairStatPolicy : CharacterStatWorkPolicy
{
    public ConstructionRepairStatPolicy()
        : base(
            new[] { BuiltInWorkTypeIds.Construct, BuiltInWorkTypeIds.Repair },
            CharacterStatType.Dexterity,
            CharacterStatType.Strength)
    {
    }
}

public sealed class CookingButcherStatPolicy : CharacterStatWorkPolicy
{
    public CookingButcherStatPolicy()
        : base(
            new[] { BuiltInWorkTypeIds.Cook, BuiltInWorkTypeIds.Butcher },
            CharacterStatType.Dexterity)
    {
    }
}

public sealed class ResearchStatPolicy : CharacterStatWorkPolicy
{
    public ResearchStatPolicy()
        : base(
            new[] { BuiltInWorkTypeIds.Research },
            CharacterStatType.Research)
    {
    }
}

public sealed class CleaningStatPolicy : CharacterStatWorkPolicy
{
    public CleaningStatPolicy()
        : base(
            new[] { BuiltInWorkTypeIds.Clean },
            CharacterStatType.Cleaning)
    {
    }
}

public sealed class HaulStatPolicy : CharacterStatWorkPolicy
{
    public HaulStatPolicy()
        : base(
            new[]
            {
                BuiltInWorkTypeIds.Haul,
                BuiltInWorkTypeIds.DrawWater,
                BuiltInWorkTypeIds.Refuel
            },
            CharacterStatType.Strength,
            CharacterStatType.Endurance)
    {
    }
}

public sealed class TreatmentStatPolicy : CharacterStatWorkPolicy
{
    public TreatmentStatPolicy()
        : base(
            new[] { BuiltInWorkTypeIds.Treat },
            CharacterStatType.Research,
            CharacterStatType.Dexterity)
    {
    }
}

public sealed class GuardHuntStatPolicy : CharacterStatWorkPolicy
{
    public GuardHuntStatPolicy()
        : base(
            new[] { BuiltInWorkTypeIds.Guard, BuiltInWorkTypeIds.Hunt },
            CharacterStatType.Attack,
            CharacterStatType.Dexterity,
            CharacterStatType.Strength)
    {
    }
}

public sealed class WorkStatPolicyRegistry : IWorkStatPolicyRegistry
{
    private readonly Dictionary<WorkTypeId, IWorkStatPolicy> policies;

    public WorkStatPolicyRegistry(IReadOnlyList<IWorkStatPolicy> registeredPolicies)
    {
        policies = new Dictionary<WorkTypeId, IWorkStatPolicy>();
        foreach (IWorkStatPolicy policy in
                 registeredPolicies ?? Array.Empty<IWorkStatPolicy>())
        {
            if (policy == null || policy.WorkTypeIds == null || policy.WorkTypeIds.Count == 0)
            {
                throw new InvalidOperationException(
                    "A null or invalid work stat policy was registered.");
            }

            foreach (WorkTypeId id in policy.WorkTypeIds)
            {
                if (!id.IsValid || !policies.TryAdd(id, policy))
                {
                    throw new InvalidOperationException(
                        $"Duplicate or invalid work stat policy id '{id}'.");
                }
            }
        }
    }

    public float GetStatMultiplier(
        WorkTypeId workTypeId,
        CharacterActor actor,
        BuildableObject target)
    {
        return policies.TryGetValue(workTypeId, out IWorkStatPolicy policy)
            ? policy.GetWorkSpeedMultiplier(actor, target)
            : 1f;
    }

}

public sealed class WorkAmountCalculator : IWorkAmountCalculator
{
    private readonly IWorkStatPolicyRegistry policies;

    public WorkAmountCalculator(IWorkStatPolicyRegistry policies)
    {
        this.policies = policies ?? throw new ArgumentNullException(nameof(policies));
    }

    public float CalculateWorkPerSecond(
        CharacterActor actor,
        BuildableObject target,
        WorkTypeId workTypeId,
        float environmentDurationMultiplier)
    {
        if (!WorkTypeCatalog.TryGet(workTypeId, out WorkTypeDefinition definition))
        {
            return 1f;
        }

        float statMultiplier = policies.GetStatMultiplier(definition.WorkTypeId, actor, target);
        float workSpeed = actor != null
            ? Mathf.Max(0.1f, actor.GetWorkSpeedMultiplier(definition.WorkTypeId))
            : 1f;
        float environment = 1f / Mathf.Max(0.1f, environmentDurationMultiplier);
        return Mathf.Clamp(
            statMultiplier * workSpeed * environment,
            0.05f,
            8f);
    }

}
