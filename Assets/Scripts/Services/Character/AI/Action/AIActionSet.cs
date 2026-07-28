using Sirenix.OdinInspector;
using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Profiling;
using UnityEngine;

[DrawWithUnity]
public abstract class AIActionSet : SerializedScriptableObject
{
    private static readonly ProfilerMarker CanStartMarker =
        new ProfilerMarker("CharacterAi.ActionCanStart");
    private static readonly ProfilerMarker ResolveDestinationMarker =
        new ProfilerMarker("CharacterAi.ActionResolveDestination");
    private static readonly Dictionary<Type, ProfilerMarker> CanStartTypeMarkers =
        new Dictionary<Type, ProfilerMarker>();
    private static readonly Dictionary<Type, ProfilerMarker> ResolveDestinationTypeMarkers =
        new Dictionary<Type, ProfilerMarker>();

    public string actionName;
    [SerializeField] private int defaultInterruptPriority;

    [field: SerializeField]
    public Consideration[] considerations { get; private set; }
    public virtual bool RequiresDestination => true;
    public virtual bool IsContinuous => false;
    public virtual float MinimumDuration => 0f;
    public virtual int InterruptPriority => defaultInterruptPriority;
    public virtual bool AllowsSurvivalEmergencyInterrupt => true;
    public virtual CharacterAiActionDescriptor Descriptor => CharacterAiActionDescriptor.None;
    public CharacterAiBranch Branch => Descriptor?.Branch ?? CharacterAiBranch.None;

    public bool HasSemanticTag(string tag)
    {
        return Descriptor != null && Descriptor.HasTag(tag);
    }

    public string GetDisplayLabel()
    {
        if (!string.IsNullOrWhiteSpace(actionName))
        {
            return actionName;
        }

        return !string.IsNullOrWhiteSpace(Descriptor?.DefaultLabel)
            ? Descriptor.DefaultLabel
            : GetType().Name;
    }

    public virtual bool CanStart(CharacterActor actor)
    {
        return true;
    }

    public virtual bool CanStart(
        CharacterActor actor,
        in CharacterAiDecisionContext context)
    {
        return CanStart(actor);
    }

    public virtual float AdjustScore(CharacterActor actor, float baseScore)
    {
        return Mathf.Clamp01(baseScore);
    }

    public virtual float AdjustScore(
        CharacterActor actor,
        in CharacterAiDecisionContext context,
        float baseScore)
    {
        return AdjustScore(actor, baseScore);
    }

    public virtual void PrepareScoreContext(
        CharacterActor actor,
        in CharacterAiDecisionContext context)
    {
    }

    public bool CanStartWithContext(
        CharacterActor actor,
        GridPathSearchResult searchResult,
        out string failureReason)
    {
        bool canStart = CanStartWithFailure(actor, searchResult, out AIActionFailure failure);
        failureReason = failure.ToString();
        return canStart;
    }

    public virtual bool CanStartWithFailure(
        CharacterActor actor,
        GridPathSearchResult searchResult,
        out AIActionFailure failure)
    {
        return TryPrepareCandidate(
            actor,
            searchResult,
            out _,
            out failure);
    }

    public virtual bool TryPrepareCandidate(
        CharacterActor actor,
        GridPathSearchResult searchResult,
        out BuildableObject destination,
        out AIActionFailure failure)
    {
        CharacterAiDecisionContext context = default;
        return TryPrepareCandidateCore(
            actor,
            false,
            in context,
            searchResult,
            out destination,
            out failure);
    }

    public virtual bool TryPrepareCandidate(
        CharacterActor actor,
        in CharacterAiDecisionContext context,
        GridPathSearchResult searchResult,
        out BuildableObject destination,
        out AIActionFailure failure)
    {
        return TryPrepareCandidateCore(
            actor,
            true,
            in context,
            searchResult,
            out destination,
            out failure);
    }

    private bool TryPrepareCandidateCore(
        CharacterActor actor,
        bool hasDecisionContext,
        in CharacterAiDecisionContext context,
        GridPathSearchResult searchResult,
        out BuildableObject destination,
        out AIActionFailure failure)
    {
        destination = null;
        failure = AIActionFailure.None;
        ICharacterAiPerformanceRecorder recorder = actor?.Brain?.PerformanceRecorder;
        long canStartStarted = recorder?.DetailedCollectionEnabled == true
            ? System.Diagnostics.Stopwatch.GetTimestamp()
            : 0L;
        bool canStart;
        using (CanStartMarker.Auto())
        using (GetTypeMarker(CanStartTypeMarkers, "CharacterAi.CanStart.", GetType()).Auto())
        {
            canStart = hasDecisionContext
                ? CanStart(actor, in context)
                : CanStart(actor);
        }
        if (canStartStarted != 0L)
        {
            double elapsedMilliseconds =
                (System.Diagnostics.Stopwatch.GetTimestamp() - canStartStarted)
                * 1000.0
                / System.Diagnostics.Stopwatch.Frequency;
            recorder.Record(
                AiPerformanceCategory.ActionCanStart,
                elapsedMilliseconds);
            CharacterAiSlowOperationTrace.Record(
                "can-start",
                actor,
                this,
                null,
                elapsedMilliseconds);
        }

        if (!canStart)
        {
            failure = AIActionFailure.Create(AIActionFailureKind.CannotStart);
            return false;
        }

        if (!RequiresDestination)
        {
            return true;
        }

        long destinationStarted = recorder?.DetailedCollectionEnabled == true
            ? System.Diagnostics.Stopwatch.GetTimestamp()
            : 0L;
        bool resolved;
        using (ResolveDestinationMarker.Auto())
        using (GetTypeMarker(
                   ResolveDestinationTypeMarkers,
                   "CharacterAi.ResolveDestination.",
                   GetType()).Auto())
        {
            resolved = TryResolveDestinationWithFailure(
                actor,
                searchResult,
                out destination,
                out failure);
        }
        if (destinationStarted != 0L)
        {
            double elapsedMilliseconds =
                (System.Diagnostics.Stopwatch.GetTimestamp() - destinationStarted)
                * 1000.0
                / System.Diagnostics.Stopwatch.Frequency;
            recorder.Record(
                AiPerformanceCategory.ActionResolveDestination,
                elapsedMilliseconds);
            CharacterAiSlowOperationTrace.Record(
                "resolve-destination",
                actor,
                this,
                null,
                elapsedMilliseconds);
        }

        if (!resolved)
        {
            if (!failure.HasFailure)
            {
                failure = AIActionFailure.Create(AIActionFailureKind.NoDestination);
            }

            return false;
        }

        if (destination == null)
        {
            failure = AIActionFailure.Create(AIActionFailureKind.NoDestination);
            return false;
        }

        return true;
    }

    private static ProfilerMarker GetTypeMarker(
        Dictionary<Type, ProfilerMarker> markers,
        string prefix,
        Type type)
    {
        if (markers.TryGetValue(type, out ProfilerMarker marker))
        {
            return marker;
        }

        marker = new ProfilerMarker(prefix + type.Name);
        markers[type] = marker;
        return marker;
    }

    public virtual bool CanContinue(CharacterActor actor, AIAction runningAction, out string stopReason)
    {
        stopReason = string.Empty;
        return true;
    }

    public virtual bool CanInterrupt(CharacterActor actor, AIAction runningAction, out string interruptReason)
    {
        interruptReason = string.Empty;
        return false;
    }

    public virtual void Execute(CharacterActor actor)
    {
    }

    public virtual void OnStop(CharacterActor actor, AIAction runningAction, string reason)
    {
    }

    public virtual IReadOnlyList<BuildableObject> GetDestinationCandidates(
        CharacterActor actor,
        GridPathSearchResult searchResult)
    {
        BuildableObject destination = GetDestination(actor);
        return destination != null
            ? new[] { destination }
            : Array.Empty<BuildableObject>();
    }

    public virtual BuildableObject SelectDestination(
        CharacterActor actor,
        IReadOnlyList<BuildableObject> candidates)
    {
        return candidates != null
            ? candidates.FirstOrDefault((building) => building != null && !building.isDestroy)
            : null;
    }

    public bool TryResolveDestination(
        CharacterActor actor,
        GridPathSearchResult searchResult,
        out BuildableObject destination,
        out string failureReason)
    {
        bool resolved = TryResolveDestinationWithFailure(
            actor,
            searchResult,
            out destination,
            out AIActionFailure failure);
        failureReason = failure.ToString();
        return resolved;
    }

    public virtual bool TryResolveDestinationWithFailure(
        CharacterActor actor,
        GridPathSearchResult searchResult,
        out BuildableObject destination,
        out AIActionFailure failure)
    {
        destination = null;
        failure = AIActionFailure.None;

        if (!RequiresDestination)
        {
            return true;
        }

        IReadOnlyList<BuildableObject> candidates = GetDestinationCandidates(actor, searchResult);
        if (candidates == null || candidates.Count == 0)
        {
            failure = AIActionFailure.Create(AIActionFailureKind.NoDestination);
            return false;
        }

        destination = SelectDestination(actor, candidates);
        if (destination == null)
        {
            failure = AIActionFailure.Create(AIActionFailureKind.DestinationSelectionFailed);
            return false;
        }

        return true;
    }

    public virtual bool TryReserveDestination(
        CharacterActor actor,
        BuildableObject destination,
        out AIActionFailure failure)
    {
        failure = AIActionFailure.None;
        return true;
    }

    public virtual void RefreshDestinationReservation(
        CharacterActor actor,
        BuildableObject destination)
    {
    }

    public virtual void ReleaseDestinationReservation(
        CharacterActor actor,
        BuildableObject destination)
    {
    }

    public virtual BuildableObject GetDestination(CharacterActor actor)
    {
        return null;
    }

}
