using System;
using System.Collections.Generic;
using System.Linq;

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
