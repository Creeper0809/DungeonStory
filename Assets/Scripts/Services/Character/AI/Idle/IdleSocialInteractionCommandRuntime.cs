using System;
using DungeonStory.Foundation;
using DungeonStory.Foundation.Facts;
using UnityEngine;
using VContainer;
using VContainer.Unity;

public sealed class IdleSocialInteractionCommandRuntime :
    IIdleSocialInteractionCommand, IStartable, ITickable, IDungeonSaveRestoreCompletedHook
{
    private readonly IDomainFactPublisher publisher;
    private readonly IDomainFactRecoveryPublisher recovery;
    private readonly DungeonRuntimeAggregateRootStore roots;
    private readonly IGameCalendar calendar;
    private readonly Func<string> operationIdFactory;
    private bool active;
    private float nextRecovery;
    private int recoveryCursor;
    private string lastFailure = string.Empty;

    [Inject]
    public IdleSocialInteractionCommandRuntime(IDomainFactPublisher publisher, IDomainFactRecoveryPublisher recovery,
        DungeonRuntimeAggregateRootStore roots, IGameCalendar calendar)
        : this(publisher, recovery, roots, calendar, null) { }

    internal IdleSocialInteractionCommandRuntime(IDomainFactPublisher publisher, IDomainFactRecoveryPublisher recovery,
        DungeonRuntimeAggregateRootStore roots, IGameCalendar calendar, Func<string> operationIdFactory)
    {
        this.publisher = publisher ?? throw new ArgumentNullException(nameof(publisher));
        this.recovery = recovery ?? throw new ArgumentNullException(nameof(recovery));
        this.roots = roots ?? throw new ArgumentNullException(nameof(roots));
        this.calendar = calendar ?? throw new ArgumentNullException(nameof(calendar));
        this.operationIdFactory = operationIdFactory ?? (() => "idle-social:" + Guid.NewGuid().ToString("N"));
    }
    private IdleSocialInteractionPendingState Current => roots.GetOrCreate(() => new IdleSocialInteractionPendingState());
    private IdleSocialInteractionPendingState Writable => roots.GetOrCreateWritable(() => new IdleSocialInteractionPendingState(),
        value => IdleSocialInteractionPendingState.Restore(value.Capture()));

    public IdleSocialInteractionPendingSaveData Capture()
    {
        if (active) throw new InvalidOperationException("idle-social-capture-during-mutation");
        return Current.Capture();
    }
    public IdleSocialInteractionPendingState PrepareRestore(IdleSocialInteractionPendingSaveData save)
    {
        if (active) throw new InvalidOperationException("idle-social-restore-during-mutation");
        return IdleSocialInteractionPendingState.Restore(save);
    }
    public void PublishRestore(IdleSocialInteractionPendingState candidate)
    {
        if (active) throw new InvalidOperationException("idle-social-restore-during-mutation");
        roots.Replace(candidate ?? throw new ArgumentNullException(nameof(candidate)));
    }

    public bool TryExecute(CharacterActor actor, CharacterActor target, string behaviorId, out string failureReason)
    {
        failureReason = string.Empty;
        if (actor == null || target == null || actor == target || actor.IsDead || target.IsDead)
        { failureReason = "서로 다른 두 생존 인물이 필요합니다."; return false; }
        if (actor.SocialMemory == null || target.SocialMemory == null || target.Stats == null)
        { failureReason = "idle-social-state-owner-missing"; return false; }
        string behavior = behaviorId?.Trim() ?? string.Empty;
        string reason = IdleSocialInteractionResolvedFact.ReasonFor(behavior);
        if (reason.Length == 0)
        { failureReason = "지원하지 않는 자율 사회 행동입니다."; return false; }
        CharacterId actorId = CharacterPersistentIdentity.Require(actor);
        CharacterId targetId = CharacterPersistentIdentity.Require(target);
        if (active || roots.IsRestoreStaging || Current.Count >= IdleSocialInteractionPendingState.Capacity
            || Current.ContainsParticipant(actorId.Value) || Current.ContainsParticipant(targetId.Value))
        { failureReason = "idle-social-observation-pending-or-busy"; return false; }

        active = true;
        DomainFactLease lease = default;
        CharacterSocialMemorySnapshot actorMemory = null, targetMemory = null;
        CharacterMoodDeliveryTransactionSnapshot targetMood = null;
        bool mutated = false, handedOff = false;
        string pendingKey = string.Empty;
        try
        {
            actorMemory = actor.SocialMemory.CaptureSnapshot();
            targetMemory = target.SocialMemory.CaptureSnapshot();
            targetMood = target.Stats.CaptureMoodDeliveryTransactionState();
            float actorBefore = actor.SocialMemory.GetRelationshipSentiment(target);
            float targetBefore = target.SocialMemory.GetRelationshipSentiment(actor);
            float moodBefore = target.Stats.Mood;
            var proposal = new IdleSocialInteractionResolvedFact(new IdleSocialInteractionFactData
            {
                operationId = operationIdFactory(), actorId = actorId.Value,
                actorName = actor.Identity?.DisplayName ?? actor.name,
                targetId = targetId.Value, targetName = target.Identity?.DisplayName ?? target.name,
                behaviorId = behavior, reasonId = reason, absoluteDay = Math.Max(0, calendar.Day),
                actorRelationshipBefore = actorBefore, actorRelationshipAfter = actorBefore,
                targetRelationshipBefore = targetBefore, targetRelationshipAfter = targetBefore,
                moodBefore = moodBefore, moodAfter = moodBefore
            }, isProposal: true);
            if (!publisher.TryReserve(proposal, out lease, out failureReason)) return false;
            if (!DomainFactValueSyntax.IsText(lease.PublicationKey, 1024, false))
            { failureReason = "idle-social-publication-key-missing"; return false; }

            mutated = true;
            ApplyBehavior(actor, target, behavior);
            var fact = proposal.WithResult(actor.SocialMemory.GetRelationshipSentiment(target),
                target.SocialMemory.GetRelationshipSentiment(actor), target.Stats.Mood);
            if (!fact.MatchesProposal(proposal)) throw new InvalidOperationException("idle-social-proposal-drift");
            pendingKey = lease.PublicationKey;
            Writable.Add(new IdleSocialInteractionPendingData { publicationKey = pendingKey, fact = fact.Snapshot() });
            // From here, an exception may conceal a committed observation.
            // Retain applied state and the exact fact; recovery only delivers it.
            handedOff = true;
            DomainFactDelivery delivery;
            try { delivery = publisher.Publish(lease, fact); }
            catch (Exception exception) when (Recoverable(exception))
            { delivery = DomainFactDelivery.Uncertain("idle-social-observer-reply:" + exception.GetType().Name); }
            if (!delivery.MayBeCommitted)
            {
                handedOff = false;
                Restore(actor, target, actorMemory, targetMemory, targetMood);
                mutated = false;
                Writable.Remove(pendingKey);
                failureReason = delivery.Detail;
                return false;
            }
            if (Delivered(pendingKey, delivery)) Writable.Remove(pendingKey);
            else Report("idle-social-observation-pending:" + proposal.OperationId + ":" + delivery.Detail);

            actor.AddActivity(CharacterActivityEvent.Create(
                CharacterActivityKinds.Social, CharacterActivityOutcomes.Responded,
                BuildActivity(actor, target, behavior), actionId: behavior, reasonCode: reason,
                sentiment: behavior == "social:formal-etiquette" ? 0.25f : 0.4f, bubbleEligible: true));
            return true; // Applied, including an explicitly retained delivery uncertainty.
        }
        catch (Exception exception) when (Recoverable(exception))
        {
            if (handedOff)
            {
                Report("idle-social-post-apply-pending:" + exception.GetType().Name);
                return true;
            }
            if (mutated) Restore(actor, target, actorMemory, targetMemory, targetMood);
            if (pendingKey.Length > 0) Writable.Remove(pendingKey);
            throw;
        }
        finally
        {
            try { publisher.Cancel(lease); }
            catch (Exception exception) when (Recoverable(exception))
            { Report("idle-social-reservation-release-failed:" + exception.GetType().Name); }
            active = false;
        }
    }

    public void Start() => Recover();
    public void OnRestoreCompleted() { recoveryCursor = 0; nextRecovery = 0; lastFailure = string.Empty; Recover(); }
    public void Tick()
    {
        if (active || roots.IsRestoreStaging || Current.Count == 0 || Time.unscaledTime < nextRecovery) return;
        nextRecovery = Time.unscaledTime + 1f;
        Recover();
    }
    public bool Recover()
    {
        if (active || roots.IsRestoreStaging) return false;
        var saved = Current.Capture().pending;
        if (saved.Count == 0) { recoveryCursor = 0; lastFailure = string.Empty; return true; }
        int start = recoveryCursor % saved.Count, count = Math.Min(8, saved.Count);
        string failure = string.Empty;
        for (int index = 0; index < count; index++)
        {
            var value = saved[(start + index) % saved.Count];
            var replies = new DomainFactDelivery[1];
            try
            {
                if (recovery.ReplayBatch(new[] { DomainFactReplay.Create(value.publicationKey,
                        new IdleSocialInteractionResolvedFact(value.fact)) }, replies, out string reason)
                    && Delivered(value.publicationKey, replies[0])) Writable.Remove(value.publicationKey);
                else failure = "idle-social-recovery-pending:" + reason + ":" + replies[0].Detail;
            }
            catch (Exception exception) when (Recoverable(exception))
            { failure = "idle-social-recovery:" + exception.GetType().Name; }
        }
        recoveryCursor = (start + count) % saved.Count;
        if (failure.Length > 0) Report(failure); else lastFailure = string.Empty;
        return failure.Length == 0 && Current.Count == 0;
    }
    private void Report(string failure)
    { if (lastFailure != failure) Debug.LogWarning(failure); lastFailure = failure; }
    private static bool Delivered(string key, DomainFactDelivery result)
    {
        if (!result.Completed || result.OperationKey != key || string.IsNullOrWhiteSpace(result.PublicationId)
            || result.Fingerprint?.Length != 64) return false;
        foreach (char value in result.Fingerprint)
            if (!(value >= '0' && value <= '9' || value >= 'a' && value <= 'f' || value >= 'A' && value <= 'F')) return false;
        return true;
    }
    private static bool Recoverable(Exception value) => value is not OutOfMemoryException
        && value is not StackOverflowException && value is not AccessViolationException;

    private static void ApplyBehavior(
        CharacterActor actor,
        CharacterActor target,
        string behavior)
    {
        switch (behavior)
        {
            case "social:reconcile":
                actor.SocialMemory?.RememberCharacterExperience(
                    target, 0.3f, "자율 화해 대화");
                target.SocialMemory?.RememberCharacterExperience(
                    actor, 0.3f, "자율 화해 대화");
                break;
            case "social:encourage":
                target.ApplyMoodFactor(
                    "social:encouraged",
                    $"{actor.Identity?.DisplayName ?? actor.name}의 격려",
                    1f,
                    GameCalendarRules.SecondsPerDay,
                    1);
                break;
            case "social:formal-etiquette":
                target.SocialMemory?.RememberCharacterExperience(
                    actor, 0.1f, "공식 예절을 갖춘 대화");
                break;
        }
    }

    private static void Restore(
        CharacterActor actor,
        CharacterActor target,
        CharacterSocialMemorySnapshot actorMemory,
        CharacterSocialMemorySnapshot targetMemory,
        CharacterMoodDeliveryTransactionSnapshot targetMood)
    {
        actor.SocialMemory?.RestoreSnapshot(actorMemory);
        target.SocialMemory?.RestoreSnapshot(targetMemory);
        if (targetMood != null)
            target.Stats?.RestoreMoodDeliveryTransactionState(targetMood);
    }

    private static string BuildActivity(
        CharacterActor actor,
        CharacterActor target,
        string behavior) => behavior switch
    {
        "social:reconcile" =>
            $"{target.Identity?.DisplayName ?? target.name}에게 화해를 제안했다.",
        "social:encourage" =>
            $"{target.Identity?.DisplayName ?? target.name}을 격려했다.",
        _ => $"{target.Identity?.DisplayName ?? target.name}에게 공식 예절을 갖춰 인사했다."
    };
}
