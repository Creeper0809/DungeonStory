using System;
using System.Collections;
using DungeonStory.Foundation;
using UnityEngine;

public sealed class CharacterActorLifecycleCoordinator
{
    private const int VisualRecoveryFrameStride = 8;
    private bool persistentRestorePrepared;
    private bool unpublishedComposition;
    private bool detachedRestoreCandidate;
    private bool initializedBeforeFirstStart;
    private bool hasBeenPublished;
    private int nextVisualRecoveryFrame;

    public bool IsUnpublishedComposition => unpublishedComposition;
    public bool IsDetachedRestoreCandidate => detachedRestoreCandidate;
    public bool HasBeenPublished => hasBeenPublished;

    public void Start(
        CharacterActor actor,
        CharacterIdentity identity,
        CharacterLifecycle lifecycle,
        CharacterStats stats,
        CharacterActorRuntimeBridge runtimeBridge,
        bool explicitInitializationCompleted)
    {
        if (unpublishedComposition || detachedRestoreCandidate)
        {
            throw new InvalidOperationException(
                "A character cannot start before its composition is published.");
        }

        runtimeBridge?.RequireConfigured();
        runtimeBridge?.OnActorEnabled();
        actor.state = CharacterDecisionState.DECIDE;
        bool isPersistentRestore = persistentRestorePrepared;
        bool skipInitialDataInitialization =
            isPersistentRestore
            || initializedBeforeFirstStart
            || hasBeenPublished
            || explicitInitializationCompleted;
        if (identity != null && identity.Data != null)
        {
            if (!skipInitialDataInitialization)
            {
                actor.Initialize(identity.Data);
            }

            if (lifecycle != null
                && lifecycle.CurrentState == CharacterLifecycleState.None)
            {
                lifecycle.SetLifecycleState(CharacterLifecycleState.Active);
            }

            if (!isPersistentRestore)
            {
                actor.StartCoroutine(
                    lifecycle != null
                        ? lifecycle.SnapToWalkableGridWhenReady()
                        : EmptyRoutine());
            }
        }

        persistentRestorePrepared = false;
        initializedBeforeFirstStart = false;
        stats?.BeginNeedDecaySchedule();
    }

    public void TickPresentation(
        CharacterActorPresentationBridge presentationBridge,
        CharacterVisual visual,
        IGameClock clock)
    {
        presentationBridge?.TickPresentationMaintenance();
        if (clock == null || clock.FrameCount < nextVisualRecoveryFrame)
        {
            return;
        }

        nextVisualRecoveryFrame = clock.FrameCount + VisualRecoveryFrameStride;
        visual?.RecoverExpiredTraversalVisibility();
    }

    public void OnEnabled(
        CharacterActorRuntimeBridge runtimeBridge,
        CharacterActorPresentationBridge presentationBridge,
        CustomerPersonaRuntime personaRuntime)
    {
        runtimeBridge?.OnActorEnabled();
        presentationBridge?.OnActorEnabled();
        if (!unpublishedComposition && !detachedRestoreCandidate)
        {
            personaRuntime?.RequestPersonaIfNeeded(logIfMissingQueue: false);
        }
    }

    public void OnDisabled(
        CharacterActor actor,
        CharacterVisual visual,
        CharacterActorRuntimeBridge runtimeBridge,
        CharacterActorPresentationBridge presentationBridge)
    {
        actor?.Brain?.StopAllAiForLifecycleTransition("actor-disabled");
        visual?.RestoreTraversalVisibility();
        presentationBridge?.OnActorDisabled();
        runtimeBridge?.OnActorDisabled();
    }

    public void OnDestroyed(
        CharacterActor actor,
        CharacterActorRuntimeBridge runtimeBridge,
        CharacterActorPresentationBridge presentationBridge)
    {
        actor?.Brain?.StopAllAiForLifecycleTransition("actor-destroyed");
        presentationBridge?.OnActorDestroyed();
        runtimeBridge?.OnActorDestroyed();
    }

    public void PrepareForComposition(
        CharacterActor actor,
        CharacterLifecycle lifecycle,
        CharacterActorRuntimeBridge runtimeBridge,
        CharacterActorPresentationBridge presentationBridge)
    {
        if (actor == null)
        {
            throw new ArgumentNullException(nameof(actor));
        }
        if (unpublishedComposition || detachedRestoreCandidate)
        {
            throw new InvalidOperationException(
                "Character composition mode can only be selected once.");
        }
        if (lifecycle == null || runtimeBridge == null || presentationBridge == null)
        {
            throw new InvalidOperationException(
                "Character composition requires lifecycle, runtime, and presentation bridges.");
        }
        if (runtimeBridge.IsConfigured)
        {
            throw new InvalidOperationException(
                "Character composition mode must be selected before dependency injection.");
        }

        lifecycle.PrepareForComposition(actor);
        runtimeBridge.PrepareForComposition();
        presentationBridge.PrepareForComposition();
        unpublishedComposition = true;
    }

    public void RequireCompositionReadyForPublication(
        CharacterIdentity identity,
        CharacterLifecycle lifecycle,
        CharacterActorRuntimeBridge runtimeBridge,
        CharacterActorPresentationBridge presentationBridge)
    {
        if (!unpublishedComposition || detachedRestoreCandidate)
        {
            throw new InvalidOperationException(
                "Only an unpublished character composition can be published.");
        }
        if (identity == null || identity.Data == null || !identity.TypedPersistentId.IsValid)
        {
            throw new InvalidOperationException(
                "A character requires definition data and a persistent ID before publication.");
        }

        lifecycle?.RequireCompositionReadyForPublication();
        runtimeBridge?.RequireCompositionReadyForPublication();
        presentationBridge?.RequireCompositionReadyForPublication();
        if (lifecycle == null || runtimeBridge == null || presentationBridge == null)
        {
            throw new InvalidOperationException(
                "Character publication requires all composition collaborators.");
        }
    }

    public void PublishComposition(
        CharacterIdentity identity,
        CharacterLifecycle lifecycle,
        CharacterActorRuntimeBridge runtimeBridge,
        CharacterActorPresentationBridge presentationBridge)
    {
        RequireCompositionReadyForPublication(
            identity,
            lifecycle,
            runtimeBridge,
            presentationBridge);
        runtimeBridge.PublishComposition();
        lifecycle.PublishComposition();
        presentationBridge.PublishComposition();
        unpublishedComposition = false;
        hasBeenPublished = true;
    }

    public void PrepareForDetachedRestore(
        CharacterActor actor,
        CharacterLifecycle lifecycle,
        CharacterActorRuntimeBridge runtimeBridge,
        CharacterActorPresentationBridge presentationBridge)
    {
        if (actor == null || lifecycle == null
            || runtimeBridge == null || presentationBridge == null)
        {
            throw new InvalidOperationException(
                "Detached character restore requires all composition collaborators.");
        }
        if (unpublishedComposition || detachedRestoreCandidate
            || runtimeBridge.IsConfigured)
        {
            throw new InvalidOperationException(
                "Detached character restore mode must be selected exactly once before dependency injection.");
        }

        lifecycle.PrepareForDetachedRestore(actor);
        runtimeBridge.PrepareForDetachedRestore();
        presentationBridge.PrepareForDetachedRestore();
        detachedRestoreCandidate = true;
    }

    public void PublishDetachedRestore(
        CharacterLifecycle lifecycle,
        CharacterActorRuntimeBridge runtimeBridge,
        CharacterActorPresentationBridge presentationBridge)
    {
        RequireDetachedReadyForPublication(
            identity: null,
            lifecycle,
            runtimeBridge,
            presentationBridge,
            requireIdentity: false);

        try
        {
            runtimeBridge.PublishDetachedRestore();
            lifecycle.PublishDetachedRestore();
            presentationBridge.PublishDetachedRestore();
            detachedRestoreCandidate = false;
            hasBeenPublished = true;
        }
        catch
        {
            presentationBridge.RollbackDetachedRestorePublication();
            lifecycle.RollbackDetachedRestorePublication();
            runtimeBridge.RollbackDetachedRestorePublication();
            detachedRestoreCandidate = true;
            hasBeenPublished = false;
            throw;
        }
    }

    public void RollbackDetachedRestorePublication(
        CharacterLifecycle lifecycle,
        CharacterActorRuntimeBridge runtimeBridge,
        CharacterActorPresentationBridge presentationBridge)
    {
        if (detachedRestoreCandidate || unpublishedComposition || !hasBeenPublished)
        {
            throw new InvalidOperationException(
                "Only a published detached character can be rolled back.");
        }

        presentationBridge?.RollbackDetachedRestorePublication();
        lifecycle?.RollbackDetachedRestorePublication();
        runtimeBridge?.RollbackDetachedRestorePublication();
        detachedRestoreCandidate = true;
        hasBeenPublished = false;
    }

    public void RequireDetachedReadyForPublication(
        CharacterIdentity identity,
        CharacterLifecycle lifecycle,
        CharacterActorRuntimeBridge runtimeBridge,
        CharacterActorPresentationBridge presentationBridge)
    {
        RequireDetachedReadyForPublication(
            identity,
            lifecycle,
            runtimeBridge,
            presentationBridge,
            requireIdentity: true);
    }

    private void RequireDetachedReadyForPublication(
        CharacterIdentity identity,
        CharacterLifecycle lifecycle,
        CharacterActorRuntimeBridge runtimeBridge,
        CharacterActorPresentationBridge presentationBridge,
        bool requireIdentity)
    {
        if (!detachedRestoreCandidate || unpublishedComposition)
        {
            throw new InvalidOperationException(
                "Only a detached character restore candidate can be published.");
        }
        if (lifecycle == null
            || runtimeBridge?.IsConfigured != true
            || presentationBridge == null)
        {
            throw new InvalidOperationException(
                "A detached character requires complete runtime configuration before publication.");
        }
        if (requireIdentity
            && (identity == null
                || identity.Data == null
                || !identity.TypedPersistentId.IsValid))
        {
            throw new InvalidOperationException(
                "A detached character requires definition data and a persistent ID before publication.");
        }
    }

    public void MarkInitializedBeforeFirstStart()
    {
        if (unpublishedComposition || detachedRestoreCandidate)
        {
            initializedBeforeFirstStart = true;
        }
    }

    public void PrepareForPersistentRestore(
        CharacterActorPresentationBridge presentationBridge)
    {
        presentationBridge?.ResetProceduralPresentation(recaptureBaseline: false);
        persistentRestorePrepared = true;
    }

    private static IEnumerator EmptyRoutine()
    {
        yield break;
    }
}

public sealed class CharacterActorAbilityBridge
{
    public CharacterCarryInventory EnsureRuntimeAbilities(
        CharacterActor actor,
        IWildlifeRuntime wildlifeRuntime)
    {
        CharacterCarryInventory inventory = CharacterCarryInventory.Ensure(actor);
        AbilityHaul.Ensure(actor);
        AbilityHunt.Ensure(actor, wildlifeRuntime);
        AbilityUseSubstance.Ensure(actor);
        return inventory;
    }

    public void EnsureInjectedAbilities(
        CharacterActor actor,
        IWildlifeRuntime wildlifeRuntime)
    {
        AbilityHunt.Ensure(actor, wildlifeRuntime);
        AbilityRescue.Ensure(actor);
        AbilityUseSubstance.Ensure(actor);
    }

    public void Initialize(CharacterAbilityCache cache, CharacterSO data)
    {
        cache.CacheAbility();
        foreach (CharacterAbility ability in cache.Abilities)
        {
            ability.Initializtion(data);
        }
    }

    public T Get<T>(CharacterAbilityCache cache) where T : CharacterAbility =>
        cache != null ? cache.GetAbility<T>() : null;

    public bool TryGet<T>(CharacterAbilityCache cache, out T result)
        where T : CharacterAbility
    {
        if (cache != null)
        {
            return cache.TryGetAbility(out result);
        }

        result = null;
        return false;
    }
}

public sealed class CharacterActorActivityBridge
{
    public void AddLog(CharacterLog log, string message) => log?.AddLog(message);

    public void AddActivity(
        CharacterLog log,
        CharacterProgression progression,
        CharacterActivityEvent activity)
    {
        log?.AddActivity(activity);
        if (activity == null
            || !activity.NarrativeEligible
            || string.Equals(
                activity.OutcomeId,
                CharacterActivityOutcomes.Started,
                StringComparison.Ordinal)
            || string.Equals(
                activity.OutcomeId,
                CharacterActivityOutcomes.Progress,
                StringComparison.Ordinal))
        {
            return;
        }

        progression?.RecordNarrative(
            CharacterNarrativeDomainUtility.FromActivity(activity),
            !string.IsNullOrWhiteSpace(activity.ActionId)
                ? activity.ActionId
                : activity.KindId,
            !string.IsNullOrWhiteSpace(activity.TargetId)
                ? activity.TargetId
                : activity.PlaceId,
            activity.OutcomeId,
            !Mathf.Approximately(activity.Value, 0f)
                ? activity.Value
                : activity.Quantity);
    }
}
