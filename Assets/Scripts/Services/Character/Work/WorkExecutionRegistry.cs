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

public readonly struct WorkStatPolicyDefinitionMaximumSnapshot
{
    public WorkStatPolicyDefinitionMaximumSnapshot(
        WorkTypeId workTypeId,
        double maximumMultiplier,
        string sourceDigest)
    {
        if (!workTypeId.IsValid
            || double.IsNaN(maximumMultiplier)
            || double.IsInfinity(maximumMultiplier)
            || maximumMultiplier <= 0d
            || !IsLowercaseSha256(sourceDigest))
        {
            throw new ArgumentException(
                "Work-stat definition maximum is invalid.");
        }

        WorkTypeId = workTypeId;
        MaximumMultiplier = maximumMultiplier;
        SourceDigest = sourceDigest;
    }

    public WorkTypeId WorkTypeId { get; }
    public double MaximumMultiplier { get; }
    public string SourceDigest { get; }

    private static bool IsLowercaseSha256(string value)
    {
        if (value == null || value.Length != 64)
            return false;
        for (int i = 0; i < value.Length; i++)
        {
            char character = value[i];
            if ((character < '0' || character > '9')
                && (character < 'a' || character > 'f'))
            {
                return false;
            }
        }
        return true;
    }
}

public interface IWorkStatPolicyDefinitionMaximumSource
{
    WorkStatPolicyDefinitionMaximumSnapshot CaptureDefinitionMaximum(
        WorkTypeId workTypeId);
}

public interface IWorkStatPolicyDefinitionMaximumQuery
{
    WorkStatPolicyDefinitionMaximumSnapshot CaptureDefinitionMaximum(
        WorkTypeId workTypeId);
}

/// <summary>
/// Single authored execution bound shared by live work and execution-free
/// throughput envelopes. The bounds are gameplay authority, not a fallback for
/// missing maximum-factor provenance.
/// </summary>
public static class WorkRateBoundsAuthority
{
    public const string Schema = "work-rate-bounds-authority@1";
    public const float MinimumWorkPerSecond = 0.05f;
    public const float MaximumWorkPerSecond = 8f;

    public static string SourceDigest { get; } = CaptureSourceDigest();

    public static float Clamp(float workPerSecond) => Mathf.Clamp(
        workPerSecond,
        MinimumWorkPerSecond,
        MaximumWorkPerSecond);

    private static string CaptureSourceDigest()
    {
        CanonicalSemanticDigestBuilder digest = new();
        digest.Append(Schema);
        digest.AppendFloat(MinimumWorkPerSecond);
        digest.AppendFloat(MaximumWorkPerSecond);
        return digest.ComputeSha256();
    }
}

public sealed class WorkExecutionResult
{
    public bool CompletedSuccessfully { get; set; } = true;
    public bool CompletionEffectsAlreadyApplied { get; set; }
}

public sealed class WorkExecutionContext
{
    private readonly Func<float, string, float, IEnumerator> executeWorkAmount;
    private readonly Func<
        float,
        float,
        string,
        float,
        Func<float, bool>,
        IEnumerator> executePersistentWorkAmount;
    private readonly Func<bool> canContinue;
    private readonly Action<float, float> recordApprovedWork;
    private readonly Func<bool> trySuspendAtCheckpoint;
    private readonly Action<IDisposable> registerCancellationResource;

    public WorkExecutionContext(
        int runId,
        AbilityWork work,
        CharacterActor actor,
        BuildableObject target,
        WorkTypeId workTypeId,
        Func<float, string, float, IEnumerator> executeWorkAmount,
        Func<bool> canContinue,
            Func<
                float,
                float,
                string,
                float,
                Func<float, bool>,
                IEnumerator> executePersistentWorkAmount = null,
            Action<float, float> recordApprovedWork = null,
            Func<bool> trySuspendAtCheckpoint = null,
            Action<IDisposable> registerCancellationResource = null)
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
            ? FacilityWorkTypeMap.GetRequired(definition)
            : FacilityWorkType.None;
        WorkTypeId = workTypeId;
        this.executeWorkAmount = executeWorkAmount
            ?? throw new ArgumentNullException(nameof(executeWorkAmount));
        this.canContinue = canContinue
            ?? throw new ArgumentNullException(nameof(canContinue));
        this.executePersistentWorkAmount = executePersistentWorkAmount;
        this.recordApprovedWork = recordApprovedWork;
        this.trySuspendAtCheckpoint = trySuspendAtCheckpoint;
        this.registerCancellationResource = registerCancellationResource;
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

    public IEnumerator ExecutePersistentWorkAmount(
        float requiredWork,
        float completedWork,
        string label,
        Func<float, bool> applyDelta,
        float extraMultiplier = 1f)
    {
        if (executePersistentWorkAmount == null)
        {
            throw new InvalidOperationException(
                "This work execution context does not support persistent progress.");
        }

        return executePersistentWorkAmount(
            requiredWork,
            completedWork,
            label,
            extraMultiplier,
            applyDelta ?? throw new ArgumentNullException(nameof(applyDelta)));
    }

    /// <summary>
    /// Records work accepted by a domain-owned progress loop. Handlers that use
    /// ExecuteWorkAmount or ExecutePersistentWorkAmount must not call this again.
    /// </summary>
    public void RecordApprovedWork(float amount, float remainingWork = -1f)
    {
        if (recordApprovedWork == null)
        {
            throw new InvalidOperationException(
                "This work execution context does not support external approved-work accounting.");
        }

        if (amount <= 0f || float.IsNaN(amount) || float.IsInfinity(amount))
        {
            throw new ArgumentOutOfRangeException(
                nameof(amount),
                "Approved work must be finite and greater than zero.");
        }

        recordApprovedWork(amount, remainingWork);
    }

    public bool TrySuspendAtCheckpoint() =>
        trySuspendAtCheckpoint?.Invoke() == true;

    public void RegisterCancellationResource(IDisposable resource)
    {
        if (resource == null)
            throw new ArgumentNullException(nameof(resource));
        if (registerCancellationResource == null)
        {
            throw new InvalidOperationException(
                "This work execution context does not support cancellation resources.");
        }
        registerCancellationResource(resource);
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
    private readonly ICareerService careers;
    private readonly IGameCalendar calendar;

    public WorkExecutionHandlerRegistry(
        IReadOnlyList<IWorkExecutionHandler> registeredHandlers,
        IReadOnlyList<IWorkCandidateProvider> registeredCandidateProviders,
        IReadOnlyList<IWorkUrgencyProvider> registeredUrgencyProviders,
        ICareerService careers,
        IGameCalendar calendar)
    {
        this.careers = careers ?? throw new ArgumentNullException(nameof(careers));
        this.calendar = calendar ?? throw new ArgumentNullException(nameof(calendar));
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
        if (actor != null
            && CharacterPersistentIdentity.TryGet(actor, out CharacterId characterId)
            && !careers.CanPerformRetiredWork(
                characterId,
                calendar.Day,
                CareerWorkEligibilityRules.IsSafeRetireeWork(workTypeId),
                out reason))
        {
            return false;
        }
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

public static class CareerWorkEligibilityRules
{
    private static readonly HashSet<WorkTypeId> SafeRetireeWorkTypes = new()
    {
        BuiltInWorkTypeIds.Clean,
        BuiltInWorkTypeIds.Research,
        BuiltInWorkTypeIds.Reception,
        BuiltInWorkTypeIds.Craft,
        BuiltInWorkTypeIds.Cook,
        BuiltInWorkTypeIds.Perform,
        BuiltInWorkTypeIds.Sow,
        BuiltInWorkTypeIds.Harvest,
        BuiltInWorkTypeIds.AnimalCare
    };

    public static bool IsSafeRetireeWork(WorkTypeId workTypeId) =>
        SafeRetireeWorkTypes.Contains(workTypeId);
}

public abstract class CharacterContextWorkPolicy :
    IWorkStatPolicy,
    IWorkStatPolicyDefinitionMaximumSource
{
    private readonly WorkTypeId[] workTypeIds;

    protected CharacterContextWorkPolicy(WorkTypeId[] workTypeIds)
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
    }

    public IReadOnlyCollection<WorkTypeId> WorkTypeIds => workTypeIds;

    public virtual float GetWorkSpeedMultiplier(CharacterActor actor, BuildableObject target)
    {
        // V26: the assigned proficiency profile is the sole worker-growth
        // authority. Policies may still add facility/context modifiers in
        // overrides, but must not apply legacy detailed stats a second time.
        return 1f;
    }

    public virtual WorkStatPolicyDefinitionMaximumSnapshot
        CaptureDefinitionMaximum(WorkTypeId workTypeId)
    {
        if (Array.IndexOf(workTypeIds, workTypeId) < 0)
        {
            throw new InvalidOperationException(
                "Work-stat policy does not own work type '"
                + workTypeId.Value + "'.");
        }
        CanonicalSemanticDigestBuilder digest = new();
        digest.Append("character-context-work-stat-maximum@1");
        digest.Append(workTypeId.Value);
        digest.AppendDouble(1d);
        return new WorkStatPolicyDefinitionMaximumSnapshot(
            workTypeId,
            1d,
            digest.ComputeSha256());
    }
}

public sealed class ConstructionRepairStatPolicy : CharacterContextWorkPolicy
{
    public ConstructionRepairStatPolicy()
        : base(new[] { BuiltInWorkTypeIds.Construct, BuiltInWorkTypeIds.Repair })
    {
    }
}

public sealed class CookingButcherStatPolicy : CharacterContextWorkPolicy
{
    public CookingButcherStatPolicy()
        : base(new[] { BuiltInWorkTypeIds.Cook, BuiltInWorkTypeIds.Butcher })
    {
    }
}

public sealed class ResearchStatPolicy : CharacterContextWorkPolicy
{
    public ResearchStatPolicy()
        : base(new[] { BuiltInWorkTypeIds.Research })
    {
    }
}

public sealed class CleaningStatPolicy : CharacterContextWorkPolicy
{
    public CleaningStatPolicy()
        : base(new[] { BuiltInWorkTypeIds.Clean })
    {
    }
}

public sealed class HaulStatPolicy : CharacterContextWorkPolicy
{
    public HaulStatPolicy()
        : base(
            new[]
            {
                BuiltInWorkTypeIds.Haul,
                BuiltInWorkTypeIds.DrawWater,
                BuiltInWorkTypeIds.Refuel
            })
    {
    }
}

public sealed class TreatmentStatPolicy : CharacterContextWorkPolicy
{
    public TreatmentStatPolicy()
        : base(new[] { BuiltInWorkTypeIds.Treat })
    {
    }
}

public sealed class SurgeryStatPolicy :
    IWorkStatPolicy,
    IWorkStatPolicyDefinitionMaximumSource
{
    private static readonly WorkTypeId[] WorkTypes =
    {
        BuiltInWorkTypeIds.Surgery
    };

    public IReadOnlyCollection<WorkTypeId> WorkTypeIds => WorkTypes;

    private readonly ISurgeryQuery surgery;
    private readonly ISurgicalProcedureCatalog procedures;

    public SurgeryStatPolicy(
        ISurgeryQuery surgery,
        ISurgicalProcedureCatalog procedures)
    {
        this.surgery = surgery
            ?? throw new ArgumentNullException(nameof(surgery));
        this.procedures = procedures
            ?? throw new ArgumentNullException(nameof(procedures));
    }

    public float GetWorkSpeedMultiplier(
        CharacterActor actor,
        BuildableObject target)
    {
        return 1f;
    }

    public WorkStatPolicyDefinitionMaximumSnapshot CaptureDefinitionMaximum(
        WorkTypeId workTypeId)
    {
        if (workTypeId != BuiltInWorkTypeIds.Surgery)
            throw new InvalidOperationException(
                "Surgery work-stat policy received an unrelated work type.");
        CanonicalSemanticDigestBuilder digest = new();
        digest.Append("surgery-work-stat-maximum@1");
        digest.Append(workTypeId.Value);
        digest.AppendDouble(1d);
        return new WorkStatPolicyDefinitionMaximumSnapshot(
            workTypeId,
            1d,
            digest.ComputeSha256());
    }
}

public sealed class GuardHuntStatPolicy : CharacterContextWorkPolicy
{
    public GuardHuntStatPolicy()
        : base(new[] { BuiltInWorkTypeIds.Guard, BuiltInWorkTypeIds.Hunt })
    {
    }
}

public sealed class GatheringStatPolicy : CharacterContextWorkPolicy
{
    private readonly IFacilityCapabilityQuery facilities;

    public GatheringStatPolicy(IFacilityCapabilityQuery facilities)
        : base(
            new[]
            {
                BuiltInWorkTypeIds.Gather,
                BuiltInWorkTypeIds.Sow,
                BuiltInWorkTypeIds.Harvest,
                BuiltInWorkTypeIds.Logging,
                BuiltInWorkTypeIds.Quarry
            })
    {
        this.facilities = facilities
            ?? throw new ArgumentNullException(nameof(facilities));
    }

    public override float GetWorkSpeedMultiplier(
        CharacterActor actor,
        BuildableObject target)
    {
        float result = base.GetWorkSpeedMultiplier(actor, target);
        WorkTypeId workType = CharacterWorkRoleUtility.TryGetWork(
                actor,
                out AbilityWork work)
            ? work.AssignedWorkTypeId
            : default;
        if (workType == BuiltInWorkTypeIds.Gather
            && facilities.FindOperational(
                ResearchFacilityCommandKind.GatheringPreparation).Count > 0)
        {
            result *= 1.10f;
        }
        if (workType == BuiltInWorkTypeIds.Logging)
        {
            if (facilities.FindOperational(
                    ResearchFacilityCommandKind.LoggingPreparation).Count > 0)
            {
                result *= 1.08f;
            }
            if (facilities.FindOperational(
                    ResearchFacilityCommandKind.DirectionalFelling).Count > 0)
            {
                result *= 1.08f;
            }
        }
        return Mathf.Clamp(result, 0.45f, 3f);
    }

    public override WorkStatPolicyDefinitionMaximumSnapshot
        CaptureDefinitionMaximum(WorkTypeId workTypeId)
    {
        double maximum = workTypeId == BuiltInWorkTypeIds.Gather
            ? 1.10d
            : workTypeId == BuiltInWorkTypeIds.Logging
                ? 1.08d * 1.08d
                : workTypeId == BuiltInWorkTypeIds.Sow
                    || workTypeId == BuiltInWorkTypeIds.Harvest
                    || workTypeId == BuiltInWorkTypeIds.Quarry
                    ? 1d
                    : throw new InvalidOperationException(
                        "Gathering work-stat policy received an unrelated work type.");
        CanonicalSemanticDigestBuilder digest = new();
        digest.Append("gathering-work-stat-maximum@1");
        digest.Append(workTypeId.Value);
        digest.AppendDouble(1.10d);
        digest.AppendDouble(1.08d);
        digest.AppendDouble(maximum);
        return new WorkStatPolicyDefinitionMaximumSnapshot(
            workTypeId,
            maximum,
            digest.ComputeSha256());
    }
}

public sealed class AnimalCareStatPolicy : CharacterContextWorkPolicy
{
    private readonly IFacilityCapabilityQuery facilities;

    public AnimalCareStatPolicy(IFacilityCapabilityQuery facilities)
        : base(new[] { BuiltInWorkTypeIds.AnimalCare })
    {
        this.facilities = facilities
            ?? throw new ArgumentNullException(nameof(facilities));
    }

    public override float GetWorkSpeedMultiplier(
        CharacterActor actor,
        BuildableObject target)
    {
        float result = base.GetWorkSpeedMultiplier(actor, target);
        ResearchFacilityCommandKind[] supports =
        {
            ResearchFacilityCommandKind.SelectiveBreeding,
            ResearchFacilityCommandKind.StableHarnessing,
            ResearchFacilityCommandKind.WildlifeTaming,
            ResearchFacilityCommandKind.BreedingSchedule
        };
        foreach (ResearchFacilityCommandKind support in supports)
        {
            if (facilities.FindOperational(support).Count > 0)
            {
                result *= 1.04f;
            }
        }
        return Mathf.Clamp(result, 0.45f, 3f);
    }

    public override WorkStatPolicyDefinitionMaximumSnapshot
        CaptureDefinitionMaximum(WorkTypeId workTypeId)
    {
        if (workTypeId != BuiltInWorkTypeIds.AnimalCare)
            throw new InvalidOperationException(
                "Animal-care work-stat policy received an unrelated work type.");
        double maximum = 1.04d * 1.04d * 1.04d * 1.04d;
        CanonicalSemanticDigestBuilder digest = new();
        digest.Append("animal-care-work-stat-maximum@1");
        digest.Append(workTypeId.Value);
        digest.AppendDouble(1.04d);
        digest.Append(4);
        digest.AppendDouble(maximum);
        return new WorkStatPolicyDefinitionMaximumSnapshot(
            workTypeId,
            maximum,
            digest.ComputeSha256());
    }
}

public sealed class GrandProjectStatPolicy : CharacterContextWorkPolicy
{
    public GrandProjectStatPolicy()
        : base(new[] { BuiltInWorkTypeIds.GrandProject })
    {
    }
}

public sealed class ThreatMitigationStatPolicy : CharacterContextWorkPolicy
{
    public ThreatMitigationStatPolicy()
        : base(new[] { BuiltInWorkTypeIds.ThreatMitigation })
    {
    }
}

public sealed class PlumbingStatPolicy : CharacterContextWorkPolicy
{
    public PlumbingStatPolicy()
        : base(new[] { BuiltInWorkTypeIds.Plumbing })
    {
    }
}

public sealed class WorkStatPolicyRegistry :
    IWorkStatPolicyRegistry,
    IWorkStatPolicyDefinitionMaximumQuery
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

    public WorkStatPolicyDefinitionMaximumSnapshot CaptureDefinitionMaximum(
        WorkTypeId workTypeId)
    {
        if (!WorkTypeCatalog.TryGet(
                workTypeId,
                out WorkTypeDefinition definition))
        {
            throw new InvalidOperationException(
                "Unknown work type has no definition maximum: "
                + workTypeId.Value);
        }
        WorkTypeId canonicalId = definition.WorkTypeId;
        WorkStatPolicyDefinitionMaximumSnapshot inner;
        bool hasPolicy = policies.TryGetValue(
            canonicalId,
            out IWorkStatPolicy policy);
        if (hasPolicy)
        {
            if (policy is not IWorkStatPolicyDefinitionMaximumSource source)
            {
                throw new InvalidOperationException(
                    "Work-stat policy has no definition maximum source: "
                    + canonicalId.Value);
            }
            inner = source.CaptureDefinitionMaximum(canonicalId);
        }
        else
        {
            CanonicalSemanticDigestBuilder neutralDigest = new();
            neutralDigest.Append("work-stat-policy-neutral-maximum@1");
            neutralDigest.Append(canonicalId.Value);
            neutralDigest.AppendDouble(1d);
            inner = new WorkStatPolicyDefinitionMaximumSnapshot(
                canonicalId,
                1d,
                neutralDigest.ComputeSha256());
        }

        if (inner.WorkTypeId != canonicalId)
            throw new InvalidOperationException(
                "Work-stat maximum source returned the wrong work type.");
        CanonicalSemanticDigestBuilder digest = new();
        digest.Append("work-stat-policy-registry-maximum@1");
        digest.Append(canonicalId.Value);
        digest.Append(hasPolicy);
        digest.AppendDouble(inner.MaximumMultiplier);
        digest.Append(inner.SourceDigest);
        return new WorkStatPolicyDefinitionMaximumSnapshot(
            canonicalId,
            inner.MaximumMultiplier,
            digest.ComputeSha256());
    }

}

public sealed class WorkAmountCalculator : IWorkAmountCalculator
{
    private readonly IWorkStatPolicyRegistry policies;
    private readonly IFacilityEvolutionModifierQuery facilityEvolution;
    private readonly IAutomationInfrastructureQuery automation;
    private readonly ICharacterPerformanceQuery performance;
    private readonly CharacterWorkPerformanceContextResolver performanceContext;

    public WorkAmountCalculator(
        IWorkStatPolicyRegistry policies,
        IFacilityEvolutionModifierQuery facilityEvolution,
        IAutomationInfrastructureQuery automation,
        ICharacterPerformanceQuery performance = null,
        CharacterWorkPerformanceContextResolver performanceContext = null)
    {
        this.policies = policies ?? throw new ArgumentNullException(nameof(policies));
        this.facilityEvolution = facilityEvolution
            ?? throw new ArgumentNullException(nameof(facilityEvolution));
        this.automation = automation
            ?? throw new ArgumentNullException(nameof(automation));
        this.performance = performance
            ?? throw new ArgumentNullException(nameof(performance));
        this.performanceContext = performanceContext
            ?? throw new ArgumentNullException(nameof(performanceContext));
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
        float workSpeed = 1f;
        if (actor != null)
        {
            if (performance == null || performanceContext == null)
                throw new InvalidOperationException(
                    "Work performance query and context resolver are required.");
            if (!performanceContext.TryResolve(
                    actor,
                    target,
                    definition.WorkTypeId,
                    out ProficiencyWorkProfile profile,
                    out string failureReason))
                throw new InvalidOperationException(failureReason);
            CharacterPerformanceSnapshot snapshot = performance.EvaluateWork(
                actor,
                definition.WorkTypeId,
                CharacterPerformanceResultChannel.Speed,
                performanceContext.BuildEvaluationContext(
                    profile,
                    new GameplayEffectContext(new[] { definition.WorkTypeId.Value }),
                    actor.GetWorkContextMultiplier(definition.WorkTypeId)));
            if (!snapshot.IsApplicable)
                throw new InvalidOperationException(
                    snapshot.Failure?.Message
                    ?? $"Work performance '{definition.WorkTypeId.Value}' is unavailable.");
            workSpeed = snapshot.Value;
            CharacterPerformanceExecutionTrace.Record(
                snapshot.FormulaId,
                "WorkAmountCalculator.CalculateWorkPerSecond",
                1f,
                workSpeed,
                definition.WorkTypeId.Value);
        }
        float environment = 1f / Mathf.Max(0.1f, environmentDurationMultiplier);
        float evolution = facilityEvolution.GetWorkSpeedMultiplier(
            target,
            definition.WorkTypeId);
        float poweredAssist = automation.GetWorkSpeedMultiplier(target);
        float craftsmanship = target == null
            ? 1f
            : CraftsmanshipQualityRules.ProjectionMultiplier(
                target.Craftsmanship.Quality);
        return WorkRateBoundsAuthority.Clamp(
            SettlementLaborBalanceRules.RuntimeLaborCalibrationMultiplier
            * statMultiplier
            * workSpeed
            * environment
            * evolution
            * poweredAssist
            * craftsmanship);
    }

}
