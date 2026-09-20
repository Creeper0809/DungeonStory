using System;
using System.Collections.Generic;

public interface IGameplayOutcomeAdapterRegistration
{
    Type ReceiptType { get; }
    GameplayOutcomeTypeId OutcomeTypeId { get; }
}

/// <summary>
/// Explicit opt-in for one immutable receipt envelope that can encode a
/// closed, pre-registered set of outcome types. This is not an open-ended
/// runtime dispatch hook: every supported type must have a descriptor when
/// the registry is built and the adapter must reject every unlisted type.
/// </summary>
public interface IGameplayOutcomeDynamicAdapterRegistration :
    IGameplayOutcomeAdapterRegistration
{
    IReadOnlyList<GameplayOutcomeTypeId> SupportedOutcomeTypes { get; }
    bool SupportsOutcomeType(GameplayOutcomeTypeId outcomeTypeId);
}

public interface IGameplayOutcomeAdapter<TReceipt> : IGameplayOutcomeAdapterRegistration
{
    OutcomePrepareResult TryGetRequirements(
        in TReceipt receipt,
        long currentWorldEpoch,
        out OutcomeWriteRequirements requirements);
    OutcomePrepareResult TryWrite(in TReceipt receipt, ref OutcomeWriteBuilder builder);
}

public abstract class GameplayOutcomeAdapter<TReceipt> : IGameplayOutcomeAdapter<TReceipt>
{
    public Type ReceiptType => typeof(TReceipt);
    public abstract GameplayOutcomeTypeId OutcomeTypeId { get; }
    public abstract OutcomePrepareResult TryGetRequirements(
        in TReceipt receipt,
        long currentWorldEpoch,
        out OutcomeWriteRequirements requirements);
    public abstract OutcomePrepareResult TryWrite(in TReceipt receipt, ref OutcomeWriteBuilder builder);
}

public readonly struct OutcomeValidationResult
{
    public OutcomeValidationResult(bool valid, string detailCode)
    {
        Valid = valid;
        DetailCode = detailCode ?? string.Empty;
    }
    public bool Valid { get; }
    public string DetailCode { get; }
    public static OutcomeValidationResult Accepted => new OutcomeValidationResult(true, string.Empty);
    public static OutcomeValidationResult Reject(string detailCode) => new OutcomeValidationResult(false, detailCode);
}

public readonly struct OutcomeMemoryEvaluation
{
    public OutcomeMemoryEvaluation(float salience, NarrativeMemoryTier targetTier, int nextEvaluationDay)
    {
        if (float.IsNaN(salience) || float.IsInfinity(salience) || salience < 0f || salience > 1f)
            throw new ArgumentOutOfRangeException(nameof(salience));
        Salience = salience;
        TargetTier = targetTier;
        NextEvaluationDay = nextEvaluationDay;
    }
    public float Salience { get; }
    public NarrativeMemoryTier TargetTier { get; }
    public int NextEvaluationDay { get; }
}

public interface IOutcomeMemoryPolicy
{
    int PolicyVersion { get; }
    GameplayMemorySignature GetSignature(
        in GameplayOutcomeReadView outcome,
        GameplayEntityId subjectId);
    OutcomeMemoryEvaluation Evaluate(
        in GameplayOutcomeReadView outcome,
        GameplayEntityId subjectId,
        int priorMatchingCount,
        int evaluationDay);
}

public interface IOutcomePerceptionPolicy
{
    int MaximumOptionalWitnessLinks { get; }
    bool ShouldCreateOptionalWitnessLink(
        in GameplayOutcomeReadView outcome,
        in GameplayOutcomeSubjectLink candidate);
}

public interface IOutcomeMemoryConsolidator
{
    bool CanCompact(in GameplayOutcomeReadView outcome, GameplayEntityId subjectId);
    bool IsAdditiveMetric(GameplayMetricId metricId);
}

/// <summary>
/// Explicit opt-in for lossy compaction. Descriptors without this contract
/// retain exact episodic rows even if a legacy consolidator returns true.
/// </summary>
public interface IOutcomeCompactionContract
{
    bool SupportsCompaction { get; }
    bool CanMerge(
        in CompactedNarrativeMemoryReadView existing,
        in GameplayOutcomeReadView incoming,
        GameplayEntityId subjectId);
}

public enum NarrativePerspectiveKind
{
    Global = 1,
    Character = 2,
    Facility = 3,
    Equipment = 4,
    Expedition = 5
}

public readonly struct NarrativePerspectiveContext
{
    public NarrativePerspectiveContext(
        GameplayEntityId viewerId,
        NarrativePerspectiveKind kind,
        string locale)
    {
        ViewerId = viewerId;
        Kind = kind;
        Locale = string.IsNullOrWhiteSpace(locale) ? "ko-KR" : locale;
    }
    public GameplayEntityId ViewerId { get; }
    public NarrativePerspectiveKind Kind { get; }
    public string Locale { get; }
}

public readonly struct NarrativeView
{
    public NarrativeView(
        GameplayOutcomeId outcomeId,
        NarrativePerspectiveKind perspectiveKind,
        string text,
        string rendererVersion,
        bool neutralFrameUsed)
    {
        OutcomeId = outcomeId;
        PerspectiveKind = perspectiveKind;
        Text = text ?? string.Empty;
        RendererVersion = rendererVersion ?? string.Empty;
        NeutralFrameUsed = neutralFrameUsed;
    }
    public GameplayOutcomeId OutcomeId { get; }
    public NarrativePerspectiveKind PerspectiveKind { get; }
    public string Text { get; }
    public string RendererVersion { get; }
    public bool NeutralFrameUsed { get; }
}

public interface INarrativePerspectiveProjector
{
    NarrativeView Project(
        in GameplayOutcomeReadView outcome,
        NarrativePerspectiveContext perspective);
}

public interface ICompactedNarrativePerspectiveProjector
{
    NarrativeMemoryView ProjectCompacted(
        in CompactedNarrativeMemoryReadView memory,
        NarrativePerspectiveContext perspective);
}

public interface IGameplayOutcomeDescriptor
{
    GameplayOutcomeTypeId OutcomeTypeId { get; }
    bool IsKnownRole(GameplayRoleId roleId);
    bool IsKnownMetric(GameplayMetricId metricId, GameplayMetricUnitId unitId);
    OutcomeValidationResult Validate(in GameplayOutcomeReadView outcome);
    INarrativePerspectiveProjector PerspectiveProjector { get; }
    IOutcomeMemoryPolicy MemoryPolicy { get; }
    IOutcomePerceptionPolicy PerceptionPolicy { get; }
    IOutcomeMemoryConsolidator MemoryConsolidator { get; }
}

public interface IGameplayOutcomeDescriptorCatalog
{
    IReadOnlyList<IGameplayOutcomeDescriptor> Descriptors { get; }
}

public interface IGameplayOutcomeRegistry
{
    bool TryGetDescriptor(GameplayOutcomeTypeId outcomeTypeId, out IGameplayOutcomeDescriptor descriptor);
    bool TryGetAdapter<TReceipt>(out IGameplayOutcomeAdapter<TReceipt> adapter);
    IReadOnlyList<GameplayOutcomeTypeId> RegisteredOutcomeTypes { get; }
    IReadOnlyList<Type> RegisteredReceiptTypes { get; }
    int ConsolidationPolicyVersion { get; }
}

public sealed class GameplayOutcomeRegistry : IGameplayOutcomeRegistry
{
    private readonly Dictionary<GameplayOutcomeTypeId, IGameplayOutcomeDescriptor> descriptors;
    private readonly Dictionary<Type, IGameplayOutcomeAdapterRegistration> adapters;
    private readonly GameplayOutcomeTypeId[] registeredOutcomeTypes;
    private readonly Type[] registeredReceiptTypes;
    private readonly int consolidationPolicyVersion;

    public GameplayOutcomeRegistry(
        IEnumerable<IGameplayOutcomeDescriptor> descriptorSource,
        IEnumerable<IGameplayOutcomeAdapterRegistration> adapterSource,
        IEnumerable<IGameplayOutcomeDescriptorCatalog> descriptorCatalogs = null)
    {
        descriptors = new Dictionary<GameplayOutcomeTypeId, IGameplayOutcomeDescriptor>();
        adapters = new Dictionary<Type, IGameplayOutcomeAdapterRegistration>();

        int observedPolicyVersion = 0;
        if (descriptorSource != null)
        {
            foreach (IGameplayOutcomeDescriptor descriptor in descriptorSource)
                RegisterDescriptor(descriptor, ref observedPolicyVersion);
        }

        if (descriptorCatalogs != null)
        {
            foreach (IGameplayOutcomeDescriptorCatalog catalog in descriptorCatalogs)
            {
                IReadOnlyList<IGameplayOutcomeDescriptor> catalogDescriptors =
                    catalog?.Descriptors
                    ?? throw new InvalidOperationException(
                        "Every gameplay outcome descriptor catalog must provide a descriptor collection.");
                for (int index = 0; index < catalogDescriptors.Count; index++)
                    RegisterDescriptor(catalogDescriptors[index], ref observedPolicyVersion);
            }
        }

        if (adapterSource != null)
        {
            foreach (IGameplayOutcomeAdapterRegistration adapter in adapterSource)
            {
                if (adapter == null || adapter.ReceiptType == null || !adapter.OutcomeTypeId.IsValid)
                    throw new InvalidOperationException("Every gameplay outcome adapter registration must provide receipt and outcome types.");
                if (adapter is IGameplayOutcomeDynamicAdapterRegistration dynamicAdapter)
                {
                    IReadOnlyList<GameplayOutcomeTypeId> supported =
                        dynamicAdapter.SupportedOutcomeTypes;
                    if (supported == null || supported.Count == 0)
                    {
                        throw new InvalidOperationException(
                            $"Dynamic gameplay outcome adapter '{adapter.ReceiptType.FullName}' has no supported outcome types.");
                    }
                    var uniqueTypes = new HashSet<GameplayOutcomeTypeId>();
                    for (int index = 0; index < supported.Count; index++)
                    {
                        GameplayOutcomeTypeId outcomeTypeId = supported[index];
                        if (!outcomeTypeId.IsValid
                            || !uniqueTypes.Add(outcomeTypeId)
                            || !dynamicAdapter.SupportsOutcomeType(outcomeTypeId)
                            || !descriptors.ContainsKey(outcomeTypeId))
                        {
                            throw new InvalidOperationException(
                                $"Dynamic gameplay outcome adapter '{adapter.ReceiptType.FullName}' has an invalid, duplicate, unsupported, or descriptor-less outcome type '{outcomeTypeId}'.");
                        }
                    }
                    if (!dynamicAdapter.SupportsOutcomeType(adapter.OutcomeTypeId))
                    {
                        throw new InvalidOperationException(
                            $"Dynamic gameplay outcome adapter '{adapter.ReceiptType.FullName}' does not include its primary outcome type '{adapter.OutcomeTypeId}'.");
                    }
                }
                else if (!descriptors.ContainsKey(adapter.OutcomeTypeId))
                {
                    throw new InvalidOperationException(
                        $"Gameplay outcome adapter '{adapter.ReceiptType.FullName}' has no descriptor for '{adapter.OutcomeTypeId}'.");
                }
                if (!adapters.TryAdd(adapter.ReceiptType, adapter))
                    throw new InvalidOperationException($"Duplicate gameplay outcome adapter for receipt '{adapter.ReceiptType.FullName}'.");
            }
        }

        foreach (GameplayOutcomeTypeId descriptorId in descriptors.Keys)
        {
            bool hasAdapter = false;
            foreach (IGameplayOutcomeAdapterRegistration adapter in adapters.Values)
            {
                if (adapter.OutcomeTypeId == descriptorId
                    || adapter is IGameplayOutcomeDynamicAdapterRegistration dynamicAdapter
                    && dynamicAdapter.SupportsOutcomeType(descriptorId))
                {
                    hasAdapter = true;
                    break;
                }
            }
            if (!hasAdapter)
            {
                throw new InvalidOperationException(
                    $"Gameplay outcome descriptor '{descriptorId}' has no registered receipt adapter.");
            }
        }

        registeredOutcomeTypes = new GameplayOutcomeTypeId[descriptors.Count];
        descriptors.Keys.CopyTo(registeredOutcomeTypes, 0);
        Array.Sort(registeredOutcomeTypes, CompareOutcomeTypeIds);
        registeredReceiptTypes = new Type[adapters.Count];
        adapters.Keys.CopyTo(registeredReceiptTypes, 0);
        Array.Sort(registeredReceiptTypes, CompareTypes);
        consolidationPolicyVersion = observedPolicyVersion == 0 ? 1 : observedPolicyVersion;
    }

    public IReadOnlyList<GameplayOutcomeTypeId> RegisteredOutcomeTypes => registeredOutcomeTypes;
    public IReadOnlyList<Type> RegisteredReceiptTypes => registeredReceiptTypes;
    public int ConsolidationPolicyVersion => consolidationPolicyVersion;

    public bool TryGetDescriptor(
        GameplayOutcomeTypeId outcomeTypeId,
        out IGameplayOutcomeDescriptor descriptor) =>
        descriptors.TryGetValue(outcomeTypeId, out descriptor);

    public bool TryGetAdapter<TReceipt>(out IGameplayOutcomeAdapter<TReceipt> adapter)
    {
        if (adapters.TryGetValue(typeof(TReceipt), out IGameplayOutcomeAdapterRegistration registration)
            && registration is IGameplayOutcomeAdapter<TReceipt> typed)
        {
            adapter = typed;
            return true;
        }
        adapter = null;
        return false;
    }

    private void RegisterDescriptor(
        IGameplayOutcomeDescriptor descriptor,
        ref int observedPolicyVersion)
    {
        if (descriptor == null
            || !descriptor.OutcomeTypeId.IsValid
            || descriptor.PerspectiveProjector == null
            || descriptor.MemoryPolicy == null
            || descriptor.PerceptionPolicy == null
            || descriptor.MemoryConsolidator == null)
        {
            throw new InvalidOperationException(
                "Every gameplay outcome descriptor must provide a valid ID, projector, memory policy, perception policy, and consolidator.");
        }
        if (descriptor.MemoryPolicy.PolicyVersion <= 0)
        {
            throw new InvalidOperationException(
                $"Outcome descriptor '{descriptor.OutcomeTypeId}' has an invalid policy version.");
        }
        if (observedPolicyVersion == 0)
            observedPolicyVersion = descriptor.MemoryPolicy.PolicyVersion;
        else if (observedPolicyVersion != descriptor.MemoryPolicy.PolicyVersion)
            throw new InvalidOperationException(
                "All gameplay outcome descriptors must share one consolidation policy version.");
        if (descriptor.PerceptionPolicy.MaximumOptionalWitnessLinks < 0)
        {
            throw new InvalidOperationException(
                $"Outcome descriptor '{descriptor.OutcomeTypeId}' has an invalid witness limit.");
        }
        if (descriptor.MemoryConsolidator is IOutcomeCompactionContract compaction
            && compaction.SupportsCompaction
            && descriptor.PerspectiveProjector is not ICompactedNarrativePerspectiveProjector)
        {
            throw new InvalidOperationException(
                $"Compactable outcome descriptor '{descriptor.OutcomeTypeId}' must provide a compacted narrative projector.");
        }
        if (!descriptors.TryAdd(descriptor.OutcomeTypeId, descriptor))
        {
            throw new InvalidOperationException(
                $"Duplicate gameplay outcome descriptor '{descriptor.OutcomeTypeId}'.");
        }
    }

    private static int CompareOutcomeTypeIds(GameplayOutcomeTypeId left, GameplayOutcomeTypeId right) =>
        string.CompareOrdinal(left.Value, right.Value);
    private static int CompareTypes(Type left, Type right) =>
        string.CompareOrdinal(left?.AssemblyQualifiedName, right?.AssemblyQualifiedName);
}
