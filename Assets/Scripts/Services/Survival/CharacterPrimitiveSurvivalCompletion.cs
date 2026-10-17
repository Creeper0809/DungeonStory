using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using DungeonStory.Foundation.Facts;
using UnityEngine;

internal sealed class CharacterPrimitiveSurvivalCompletion
{
    private readonly CharacterBreakdownWorld world;
    private readonly CharacterDeprivationStateStore stateStore;
    private readonly IGameClock clock;
    private readonly IGameEventBus events;
    private readonly IGameCalendar calendar;
    private readonly IDomainFactPublisher primitiveFacts;
    private readonly HashSet<string> reportedRetiredPublications = new(StringComparer.Ordinal);
    private float lastRetiredRecoveryTime = float.NegativeInfinity;

    internal CharacterPrimitiveSurvivalCompletion(CharacterBreakdownWorld world,
        CharacterDeprivationStateStore stateStore, IGameClock clock, IGameEventBus events,
        IGameCalendar calendar, IDomainFactPublisher primitiveFacts)
    {
        this.world = world;
        this.stateStore = stateStore;
        this.clock = clock;
        this.events = events;
        this.calendar = calendar;
        this.primitiveFacts = primitiveFacts;
    }

    internal void Reset()
    {
        lastRetiredRecoveryTime = float.NegativeInfinity;
        reportedRetiredPublications.Clear();
    }

    internal void Cancel(PreparedPrimitiveCompletion prepared) => primitiveFacts.Cancel(prepared.Lease);

    internal sealed class PreparedPrimitiveCompletion
    {
        internal PrimitiveSurvivalPendingSaveData Data;
        internal DomainFactLease Lease;
    }

    internal void RecoverRetiredPublications()
    {
        if (stateStore.IsRestoreStaging) return;
        float now = clock.Time;
        if (now >= lastRetiredRecoveryTime && now < lastRetiredRecoveryTime + 1f) return;
        lastRetiredRecoveryTime = now;
        foreach (var pending in stateStore.CaptureRetiredPrimitivePublications())
        {
            try
            {
                var rest = pending.GetRestObservation();
                if (pending.rollbackBlocked || rest?.rollbackBlocked == true)
                    throw new InvalidOperationException("primitive-retired-recovery-rollback-blocked");
                if (rest != null)
                {
                    var results = new DomainFactDelivery[1];
                    ((IDomainFactRecoveryPublisher)primitiveFacts).ReplayBatch(new[]
                    {
                        DomainFactReplay.Create(pending.restPublicationKey,
                            new CharacterRestAppliedFact(rest.fact))
                    }, results, out string failure);
                    var child = results[0];
                    if (child.Phase is not (DomainFactDeliveryPhase.Delivered or DomainFactDeliveryPhase.AcceptedPendingDelivery)
                        || child.OperationKey != pending.restPublicationKey || string.IsNullOrEmpty(child.PublicationId)
                        || child.Fingerprint.Length != 64)
                        throw new InvalidOperationException("primitive-retired-rest-child-pending:" + failure);
                }
                DomainFactDelivery delivered = PublishPending(pending);
                if (!PublicationConfirmed(pending, delivered))
                    throw new InvalidOperationException("primitive-retired-publication-pending:" + delivered.Detail);
                stateStore.ClearRetiredPrimitivePublication(pending.operationId, pending.publicationKey);
                reportedRetiredPublications.Remove(pending.operationId);
                // The actor lifetime has ended: publish the applied facts only.
                // No physical effects, mood or actor-targeted notifications are repeated.
            }
            catch (Exception exception) when (exception is not OutOfMemoryException
                and not StackOverflowException and not AccessViolationException)
            {
                if (reportedRetiredPublications.Add(pending.operationId))
                    Debug.LogError(exception.Message + ":" + pending.operationId);
            }
        }
    }

    internal PreparedPrimitiveCompletion ReserveCompletion(CharacterActor actor, string actionId, string waterStackId = "")
    {
        var data = new PrimitiveSurvivalPendingSaveData
        {
            operationId = "primitive:" + Guid.NewGuid().ToString("N"),
            characterId = CharacterPersistentIdentity.Require(actor).Value,
            characterName = actor.BuildingDisplayName,
            actionId = actionId, absoluteDay = Math.Max(1, calendar.Day),
            recovery = actionId == "survival:floor-rest" ? PrimitiveSurvivalBalanceAuthority.FloorRestRecovery
                : actionId == "survival:primitive-latrine" ? PrimitiveSurvivalBalanceAuthority.LatrineRecovery
                : PrimitiveSurvivalBalanceAuthority.BucketWashRecovery,
            physicalItemCount = actionId == "survival:bucket-wash" ? 1 : 0,
            waterStackId = waterStackId
        };
        var proposed = new PrimitiveSurvivalCompletedFact(data, reservationOnly: true);
        if (!primitiveFacts.TryReserve(proposed, out DomainFactLease lease, out string failure))
            throw new InvalidOperationException("Primitive survival observation unavailable: " + failure);
        string key = lease.PublicationKey;
        if (!DomainFactValueSyntax.IsText(key, 4096, false))
        {
            primitiveFacts.Cancel(lease);
            throw new InvalidOperationException("Primitive survival publication has no durable identity.");
        }
        data.publicationKey = key;
        return new PreparedPrimitiveCompletion { Data = data, Lease = lease };
    }

    internal void CompletePendingParent(CharacterActor actor, DomainFactLease lease = default)
    {
        CharacterId actorId = CharacterPersistentIdentity.Require(actor);
        if (!stateStore.TryGetPrimitiveSurvivalPending(actorId, out PrimitiveSurvivalPendingSaveData pending))
            throw new InvalidOperationException("Primitive survival applied child has no pending completion.");
        DomainFactDelivery delivered;
        try
        {
            ValidatePendingChild(actor, pending);
            delivered = PublishPending(pending, lease);
        }
        finally { if (lease.IsValid) primitiveFacts.Cancel(lease); }
        if (!PublicationConfirmed(pending, delivered))
            throw new InvalidOperationException("Primitive survival completion remains pending: " + delivered.Detail);
        stateStore.ClearPrimitiveSurvivalPending(actorId, pending.operationId);
        CompleteCommittedPending(actor, pending, delivered);
    }

    internal DomainFactDelivery PublishPending(PrimitiveSurvivalPendingSaveData pending, DomainFactLease lease = default)
    {
        try
        {
            var fact = new PrimitiveSurvivalCompletedFact(pending);
            if (lease.IsValid) return primitiveFacts.Publish(lease, fact);
            var results = new DomainFactDelivery[1];
            return ((IDomainFactRecoveryPublisher)primitiveFacts).ReplayBatch(
                new[] { DomainFactReplay.Create(pending.publicationKey, fact) }, results, out string failure)
                ? results[0] : DomainFactDelivery.Reject(failure);
        }
        catch (Exception exception) when (exception is not OutOfMemoryException
            and not StackOverflowException and not AccessViolationException)
        { return DomainFactDelivery.Uncertain("primitive-survival-publication-unconfirmed:" + exception.Message); }
    }

    internal static bool PublicationConfirmed(PrimitiveSurvivalPendingSaveData pending, DomainFactDelivery delivery) =>
        delivery.Phase is DomainFactDeliveryPhase.Delivered or DomainFactDeliveryPhase.AcceptedPendingDelivery
        && delivery.OperationKey == pending.publicationKey && !string.IsNullOrEmpty(delivery.PublicationId)
        && delivery.Fingerprint.Length == 64;

    internal PrimitiveSurvivalPendingSaveData CreatePending(CharacterActor actor, PreparedPrimitiveCompletion prepared,
        string actionId, float recovery, int physicalItemCount, in WorldFilthSnapshot waste, in WorldFilthSnapshot stain)
    {
        PrimitiveSurvivalPendingSaveData data = prepared.Data.Clone();
        if (data.actionId != actionId || data.recovery != recovery || data.physicalItemCount != physicalItemCount)
            throw new InvalidOperationException("Primitive survival applied action differs from its reservation.");
        CharacterStats stats = RequireStats(actor);
        data.sleepAfter = stats.GetConditionValue(CharacterCondition.SLEEP, 0f);
        data.hygieneAfter = stats.GetConditionValue(CharacterCondition.HYGIENE, 0f);
        data.excretionAfter = stats.GetConditionValue(CharacterCondition.EXCRETION, 0f);
        if (actionId == "survival:floor-rest")
        {
            var rest = stats.CaptureRestObservation();
            if (rest != null) data.restObservations.Add(rest);
            data.restPublicationKey = rest?.publicationKey ?? stats.LastRestOutcomeResultKey;
        }
        if (actionId == "survival:primitive-latrine")
        {
            data.targetGridX = waste.Position.x; data.targetGridY = waste.Position.y;
            data.wasteFilthId = waste.FilthId; data.wasteAmountAfter = waste.Amount;
            data.stainFilthId = stain.FilthId; data.stainAmountAfter = stain.Amount;
        }
        return data;
    }

    private void ValidatePendingChild(
        CharacterActor actor,
        PrimitiveSurvivalPendingSaveData pending)
    {
        if (pending.rollbackBlocked)
            throw new InvalidOperationException("primitive-survival-rollback-blocked:" + pending.rollbackFailure);
        if (pending.actionId == "survival:bucket-wash")
        {
            PrimitiveSurvivalCompletedFact.ValidateSaved(pending, CharacterPersistentIdentity.Require(actor).Value);
            return;
        }
        bool floorRest = string.Equals(
            pending.actionId,
            "survival:floor-rest",
            StringComparison.Ordinal);
        bool latrine = string.Equals(
            pending.actionId,
            "survival:primitive-latrine",
            StringComparison.Ordinal);
        float expectedRecovery = floorRest
            ? PrimitiveSurvivalBalanceAuthority.FloorRestRecovery
            : latrine
                ? PrimitiveSurvivalBalanceAuthority.LatrineRecovery
                : float.NaN;
        if ((!floorRest && !latrine)
            || !Mathf.Approximately(pending.recovery, expectedRecovery)
            || pending.physicalItemCount != 0)
        {
            throw new InvalidOperationException(
                "Primitive-survival pending receipt contains an unsupported child contract.");
        }
        if (floorRest)
        {
            if (RequireStats(actor).IsRestRollbackBlocked)
                throw new InvalidOperationException("floor-rest-child-rollback-blocked");
            return;
        }

        Vector2Int target = new(pending.targetGridX, pending.targetGridY);
        string actorId = CharacterPersistentIdentity.Require(actor).Value;
        IReadOnlyList<WorldFilthSnapshot> atTarget =
            world.GetFilthAt(target) ?? Array.Empty<WorldFilthSnapshot>();
        RequireFilthReceipt(
            atTarget,
            pending.wasteFilthId,
            WorldFilthType.Waste,
            pending.wasteAmountAfter,
            target,
            actorId);
        RequireFilthReceipt(
            atTarget,
            pending.stainFilthId,
            WorldFilthType.Stain,
            pending.stainAmountAfter,
            target,
            actorId);
    }

    private static void RequireFilthReceipt(
        IReadOnlyList<WorldFilthSnapshot> filth,
        string expectedId,
        WorldFilthType expectedType,
        float expectedAmount,
        Vector2Int expectedPosition,
        string expectedActorId)
    {
        WorldFilthSnapshot receipt = filth.FirstOrDefault(value =>
            string.Equals(value.FilthId, expectedId, StringComparison.Ordinal));
        if (!string.Equals(receipt.FilthId, expectedId, StringComparison.Ordinal)
            || receipt.Type != expectedType
            || receipt.Position != expectedPosition
            || !string.Equals(
                receipt.SourceCharacterId,
                expectedActorId,
                StringComparison.Ordinal)
            || !Mathf.Approximately(receipt.Amount, expectedAmount))
        {
            throw new InvalidOperationException(
                "Primitive-latrine recovery could not verify its durable filth child: "
                + expectedId);
        }
    }

    internal void CompleteCommittedPending(CharacterActor actor, PrimitiveSurvivalPendingSaveData pending,
        in DomainFactDelivery delivered)
    {
        CharacterEventContext context = BuildCompletedContext(pending, delivered.PublicationId);
        bool floor = pending.actionId == "survival:floor-rest";
        bool latrine = pending.actionId == "survival:primitive-latrine";
        // Effects are settled and pending cleared. Attempt each remaining reaction
        // once, retaining failures rather than replaying physical effects.
        List<Exception> failures = null;
        void Publish(Action callback)
        {
            try { callback(); }
            catch (Exception exception) when (exception is not OutOfMemoryException
                and not StackOverflowException and not AccessViolationException)
            { (failures ??= new List<Exception>()).Add(exception); }
        }
        if (floor || latrine)
            Publish(() => actor.ApplyMoodFactorFromEvent(pending.actionId,
                floor ? "맨바닥에서 잠" : "임시 변소를 사용함",
                floor ? PrimitiveSurvivalBalanceAuthority.FloorRestMoodDelta : PrimitiveSurvivalBalanceAuthority.LatrineMoodDelta,
                180f, 1, pending.absoluteDay, context));
        string publicationId = delivered.PublicationId;
        Publish(() => events.Publish(new PrimitiveSurvivalChangedEvent(PrimitiveSurvivalTransition.Completed,
            actor, pending: pending, publicationId: publicationId)));
        Publish(() => PublishCompletedEvent(actor, pending.actionId, pending.recovery, pending.physicalItemCount));
        if (failures != null) throw new AggregateException("Primitive survival completion reactions failed.", failures);
    }

    internal void PublishCompletedEvent(CharacterActor actor, string actionId, float recovery, int physicalItemCount) =>
        events.Publish(new CharacterPrimitiveSurvivalCompletedEvent(
            CharacterPersistentIdentity.Require(actor), actionId, recovery, physicalItemCount));

    internal static CharacterEventContext BuildCompletedContext(PrimitiveSurvivalPendingSaveData data, string eventId) => new()
    {
        eventInstanceId = eventId, chainId = eventId, actorId = data.characterId,
        actorDisplayName = data.characterName, resultDetail = "생존 행동 완료", occurredDay = data.absoluteDay
    };

    internal bool TryResumePending(CharacterActor actor, out string status)
    {
        status = string.Empty;
        if (actor == null || actor.IsDead)
        {
            return false;
        }

        CharacterId actorId = CharacterPersistentIdentity.Require(actor);
        if (!stateStore.TryGetPrimitiveSurvivalPending(
                actorId,
                out PrimitiveSurvivalPendingSaveData pending))
        {
            return false;
        }

        if (pending.actionId == "survival:floor-rest" && !RequireStats(actor).TryCompleteRestObservation())
        {
            status = "이전 휴식 적용 사실 전달 대기";
            return false;
        }
        ValidatePendingChild(actor, pending);
        CompletePendingParent(actor);
        status = "이전 생존 행동 결과 복구 완료";
        return true;
    }

    private static CharacterStats RequireStats(CharacterActor actor)
    {
        if (actor?.Stats != null)
            return actor.Stats;
        throw new InvalidOperationException(
            "Primitive survival requires an initialized character stats owner.");
    }
}
