using Sirenix.OdinInspector;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[DrawWithUnity]
public abstract class AIActionSet : SerializedScriptableObject
{
    public string actionName;
    [SerializeField] private int defaultInterruptPriority;

    [field: SerializeField]
    public Consideration[] considerations { get; private set; }
    public virtual bool RequiresDestination => true;
    public virtual bool IsContinuous => false;
    public virtual float MinimumDuration => 0f;
    public virtual int InterruptPriority => defaultInterruptPriority;
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

    public virtual float AdjustScore(CharacterActor actor, float baseScore)
    {
        return Mathf.Clamp01(baseScore);
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
        failure = AIActionFailure.None;
        if (!CanStart(actor))
        {
            failure = AIActionFailure.Create(AIActionFailureKind.CannotStart);
            return false;
        }

        if (!RequiresDestination)
        {
            return true;
        }

        if (!TryResolveDestinationWithFailure(actor, searchResult, out BuildableObject destination, out failure))
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
