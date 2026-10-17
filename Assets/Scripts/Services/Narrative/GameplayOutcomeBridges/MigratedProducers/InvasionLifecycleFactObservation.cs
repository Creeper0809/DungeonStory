using System;
using System.Collections.Generic;
using System.Globalization;
using DungeonStory.Foundation.Facts;
using VContainer;

public static class InvasionLifecycleFactObservation
{
    public const string ObserverId = "log:invasion.lifecycle";

    public static MigratedProducerOutcomeKind Kind(
        InvasionLifecycleFactData data)
    {
        if (data == null)
            throw new ArgumentNullException(nameof(data));
        return data.stage switch
        {
            InvasionLifecycleStage.Started =>
                MigratedProducerOutcomeKind.InvasionStartedEvent,
            InvasionLifecycleStage.Breached =>
                MigratedProducerOutcomeKind.InvasionDungeonBreachedEvent,
            InvasionLifecycleStage.FacilityDamaged =>
                MigratedProducerOutcomeKind.InvasionFacilityDamagedEvent,
            InvasionLifecycleStage.FrontCollapsed =>
                MigratedProducerOutcomeKind.DefenseFrontCollapsedEvent,
            InvasionLifecycleStage.Resolved =>
                MigratedProducerOutcomeKind.InvasionResolvedEvent,
            _ => throw new ArgumentOutOfRangeException(nameof(data.stage))
        };
    }

    public static string OwnerIdentity(InvasionLifecycleFactData data)
    {
        if (data == null)
            throw new ArgumentNullException(nameof(data));
        return data.stage switch
        {
            InvasionLifecycleStage.Started or
            InvasionLifecycleStage.Breached or
            InvasionLifecycleStage.Resolved => data.runtimeId,
            InvasionLifecycleStage.FacilityDamaged =>
                data.runtimeId + ":facility-damage:" + data.targetId,
            InvasionLifecycleStage.FrontCollapsed =>
                data.runtimeId + ":front-collapse",
            _ => throw new ArgumentOutOfRangeException(nameof(data.stage))
        };
    }

    public static MigratedDomainFactCapture Capture(
        InvasionLifecycleFact fact,
        IGameplayOutcomeDiagnosticsQuery diagnostics)
    {
        if (fact == null)
            throw new ArgumentNullException(nameof(fact));
        if (diagnostics == null)
            throw new ArgumentNullException(nameof(diagnostics));

        InvasionLifecycleFactData data = fact.Snapshot();
        data.Validate(fact.IsProposal);
        if (data.stage == InvasionLifecycleStage.Started)
            return CaptureCore(fact, data, default);

        GameplayResultKey parentKey = ReadParentKey(data);
        if (!diagnostics.TryGetResultIdentity(
                parentKey,
                out GameplayOutcomeReplayIdentity parent)
            || parent.ResultKey != parentKey
            || parent.State < GameplayOutcomeReplayState.Committed
            || !parent.OutcomeId.IsValid
            || !parent.HasCanonicalPayloadHash)
        {
            throw new InvalidOperationException(
                "invasion-lifecycle-parent-not-committed");
        }

        return CaptureCore(fact, data, parent.OutcomeId);
    }

    public static MigratedDomainFactCapture CaptureWithParent(
        InvasionLifecycleFact fact,
        GameplayOutcomeId parent)
    {
        if (fact == null)
            throw new ArgumentNullException(nameof(fact));
        InvasionLifecycleFactData data = fact.Snapshot();
        data.Validate(fact.IsProposal);
        if (data.stage == InvasionLifecycleStage.Started)
        {
            if (parent.IsValid)
            {
                throw new ArgumentException(
                    "An invasion start cannot have a native parent.",
                    nameof(parent));
            }
        }
        else
        {
            _ = ReadParentKey(data);
            if (!parent.IsValid)
            {
                throw new ArgumentException(
                    "A committed invasion lifecycle parent is required.",
                    nameof(parent));
            }
        }

        return CaptureCore(fact, data, parent);
    }

    private static MigratedDomainFactCapture CaptureCore(
        InvasionLifecycleFact fact,
        InvasionLifecycleFactData data,
        GameplayOutcomeId parent)
    {
        MigratedProducerOutcomeSubject additional = AdditionalSubject(data);
        GameplayOutcomeCausation causation = new(
            parent,
            new GameplayOperationId(
                data.stage == InvasionLifecycleStage.Started
                    && data.milestonePressure
                    ? data.targetId
                    : data.runtimeId),
            Relation(data.stage, data.milestonePressure));
        MigratedProducerOutcomeDetails details =
            InvasionLifecycleOutcomeDetails.Create(
                data.runtimeId,
                data.intruderId,
                data.intruderName,
                StageName(data.stage),
                data.damageIndexBefore,
                data.damageIndexAfter,
                data.stateBefore,
                data.stateAfter,
                data.riskBefore,
                data.riskAfter,
                Summary(data),
                causation,
                additional,
                DetailReason(data),
                reserveDurableEquipmentShape:
                    fact.IsProposal && data.hornExpected);

        if (!fact.IsProposal && data.horn != null)
        {
            details = DurableFacilityEquipmentOutcomeEncoding.Append(
                details,
                data.horn.stackId,
                data.horn.itemId,
                FormatDurableEquipment(data.horn));
        }

        return new MigratedDomainFactCapture(
            Kind(data),
            OwnerIdentity(data),
            data.absoluteDay,
            Status(data),
            details,
            reservationOnly: fact.IsProposal);
    }

    private static GameplayResultKey ReadParentKey(
        InvasionLifecycleFactData data)
    {
        if (!GameplayDomainFactDeliveryQuery.TryReadKey(
                data.parentPublicationKey,
                out GameplayResultKey key)
            || !IsExpectedParent(data, key))
        {
            throw new InvalidOperationException(
                "invasion-lifecycle-parent-key-invalid");
        }
        return key;
    }

    private static bool IsExpectedParent(
        InvasionLifecycleFactData data,
        GameplayResultKey key)
    {
        bool started = Matches(
            key,
            MigratedProducerOutcomeKind.InvasionStartedEvent,
            data.runtimeId);
        bool breached = Matches(
            key,
            MigratedProducerOutcomeKind.InvasionDungeonBreachedEvent,
            data.runtimeId);
        bool damaged = MatchesFacilityDamage(key, data.runtimeId);
        bool collapsed = Matches(
            key,
            MigratedProducerOutcomeKind.DefenseFrontCollapsedEvent,
            data.runtimeId + ":front-collapse");
        return data.stage switch
        {
            InvasionLifecycleStage.Breached => started,
            InvasionLifecycleStage.FacilityDamaged =>
                started || breached || damaged,
            InvasionLifecycleStage.FrontCollapsed =>
                started || breached || damaged,
            InvasionLifecycleStage.Resolved =>
                started || breached || damaged || collapsed,
            _ => false
        };
    }

    private static bool Matches(
        GameplayResultKey key,
        MigratedProducerOutcomeKind kind,
        string ownerIdentity) =>
        key.CommitRevision >= 1 && key == MigratedProducerOutcomeCatalog.ResultKey(kind, ownerIdentity,
            kind == MigratedProducerOutcomeKind.DefenseFrontCollapsedEvent ? key.CommitRevision : 1);

    private static bool MatchesFacilityDamage(
        GameplayResultKey key,
        string runtimeId)
    {
        // Each facility is damaged once per invasion. Its operation ID is a
        // catalog hash, not a readable runtime-ID prefix. The publication
        // journal validates the exact same-runtime predecessor and preflight
        // joins every hash back to that predecessor's frozen facility fact.
        return key.ProducerId == MigratedProducerOutcomeCatalog.Get(
                MigratedProducerOutcomeKind.InvasionFacilityDamagedEvent)
            .ProducerId
            && key.CommitRevision == 1 && key.LocalResultIndex == 0;
    }

    private static MigratedProducerOutcomeSubject AdditionalSubject(
        InvasionLifecycleFactData data) => data.targetKind switch
        {
            InvasionLifecycleTargetKind.None => default,
            InvasionLifecycleTargetKind.Facility =>
                InvasionLifecycleOutcomeDetails.Facility(
                    data.targetId,
                    data.targetName),
            InvasionLifecycleTargetKind.Defender => new(
                MigratedProducerOutcomeIds.CharacterKind,
                data.targetId,
                data.targetName,
                MigratedProducerOutcomeIds.DefenderRole),
            InvasionLifecycleTargetKind.ScheduledOperation => new(
                MigratedProducerOutcomeIds.OperationKind,
                data.targetId,
                data.targetName,
                MigratedProducerOutcomeIds.SourceRole),
            _ => throw new ArgumentOutOfRangeException(nameof(data.targetKind))
        };

    private static GameplayOutcomeStatus Status(
        InvasionLifecycleFactData data) =>
        data.stage == InvasionLifecycleStage.Resolved && !data.defended
            ? GameplayOutcomeStatus.Failed
            : GameplayOutcomeStatus.Succeeded;

    private static string StageName(InvasionLifecycleStage stage) => stage switch
    {
        InvasionLifecycleStage.Started => "start",
        InvasionLifecycleStage.Breached => "breach",
        InvasionLifecycleStage.FacilityDamaged => "facility-damage",
        InvasionLifecycleStage.FrontCollapsed => "front-collapse",
        InvasionLifecycleStage.Resolved => "resolved",
        _ => throw new ArgumentOutOfRangeException(nameof(stage))
    };

    private static string Relation(
        InvasionLifecycleStage stage,
        bool milestonePressure) => stage switch
        {
            InvasionLifecycleStage.Started => milestonePressure
                ? "milestone-pressure-invasion-start"
                : "invasion-start",
            InvasionLifecycleStage.Breached => "invasion-breach",
            InvasionLifecycleStage.FacilityDamaged =>
                "invasion-facility-damage",
            InvasionLifecycleStage.FrontCollapsed =>
                "defense-front-collapse",
            InvasionLifecycleStage.Resolved => "invasion-resolved",
            _ => throw new ArgumentOutOfRangeException(nameof(stage))
        };

    private static string DetailReason(InvasionLifecycleFactData data) =>
        data.stage switch
        {
            InvasionLifecycleStage.Started when data.milestonePressure =>
                data.sourceOwnerId,
            InvasionLifecycleStage.FrontCollapsed => data.reason,
            InvasionLifecycleStage.Resolved => data.defended
                ? "defended"
                : "defense-failed",
            _ => string.Empty
        };

    private static string Summary(InvasionLifecycleFactData data) =>
        data.stage switch
        {
            InvasionLifecycleStage.Started =>
                $"{data.intruderName}의 침공이 위협 {data.riskAfter:0.##}로 시작됐다.",
            InvasionLifecycleStage.Breached =>
                $"{data.runtimeId} 침공대가 던전 내부 {data.cellX},{data.cellY}에 진입했다.",
            InvasionLifecycleStage.FacilityDamaged =>
                $"{data.targetName}이(가) 침입자에게 손상됐다;damageIndex={data.damageIndexAfter}.",
            InvasionLifecycleStage.FrontCollapsed =>
                "방어선이 붕괴했다: " + data.reason.Trim(),
            InvasionLifecycleStage.Resolved =>
                ResolutionSummary(data.resolution)
                + ";defended=" + data.defended
                + ";residualRisk=" + data.riskAfter.ToString(
                    "0.###",
                    CultureInfo.InvariantCulture),
            _ => throw new ArgumentOutOfRangeException(nameof(data.stage))
        };

    private static string ResolutionSummary(
        InvasionResolutionKind resolution) => resolution switch
        {
            InvasionResolutionKind.Suppressed =>
                "침입자가 제압되어 침공이 종결됐다.",
            InvasionResolutionKind.DefenseFailed =>
                "최종 방어선이 돌파되어 침공이 종결됐다.",
            InvasionResolutionKind.DefenderSurvived =>
                "최종 교전에서 방어자가 버텨 침공이 종결됐다.",
            InvasionResolutionKind.DefenderFell =>
                "최종 교전에서 방어자가 쓰러져 침공이 종결됐다.",
            InvasionResolutionKind.IntruderDefeated =>
                "침입자가 패배하여 침공이 종결됐다.",
            InvasionResolutionKind.Retreated =>
                "부상당한 침입자가 철수하여 침공이 종결됐다.",
            _ => throw new ArgumentOutOfRangeException(nameof(resolution))
        };

    private static string FormatDurableEquipment(
        DurableEquipmentUseFactData value)
    {
        value.Validate();
        return string.Concat(
            "semantics=wear;item=", value.itemId,
            ";stack=", value.stackId,
            ";facility=", value.facilityId,
            ";requirement=", value.requirementId,
            ";slotSequence=", value.assignmentSequence.ToString(
                CultureInfo.InvariantCulture),
            ";revision=", value.revisionBefore.ToString(
                CultureInfo.InvariantCulture),
            "->", value.revisionAfter.ToString(CultureInfo.InvariantCulture),
            ";quantity=", value.quantityBefore.ToString(
                CultureInfo.InvariantCulture),
            "->", value.quantityAfter.ToString(CultureInfo.InvariantCulture),
            ";durability=", Format(value.durabilityBefore),
            "->", Format(value.durabilityAfter),
            ";maximum=", Format(value.maximumDurability),
            ";requestedWear=", Format(value.requestedWear),
            ";spent=", Format(value.durabilityBefore - value.durabilityAfter));
    }

    private static string Format(double value) =>
        value.ToString("R", CultureInfo.InvariantCulture);
}

public sealed class InvasionLifecycleFactObserver :
    IDurableDomainFactObserver<InvasionLifecycleFact>,
    IDurableDomainFactRecoveryObserver<InvasionLifecycleFact>
{
    private readonly MigratedDomainFactObserver<InvasionLifecycleFact> inner;

    public InvasionLifecycleFactObserver(
        MigratedProducerOutcomeRuntime runtime,
        GameplayDomainFactCommitGroup commits,
        IGameplayOutcomeDiagnosticsQuery diagnostics)
    {
        if (diagnostics == null)
            throw new ArgumentNullException(nameof(diagnostics));
        inner = new MigratedDomainFactObserver<InvasionLifecycleFact>(
            fact => InvasionLifecycleFactObservation.Capture(
                fact,
                diagnostics),
            runtime,
            commits);
    }

    public bool TryReserve(
        in InvasionLifecycleFact proposed,
        out object reservation,
        out string failure)
    {
        reservation = null;
        if (proposed == null)
        {
            failure = "invasion-lifecycle-fact-missing";
            return false;
        }
        if (!inner.TryReserve(proposed, out object innerReservation, out failure))
            return false;
        reservation = new Reservation(proposed, innerReservation);
        return true;
    }

    public bool TryPrepare(
        in InvasionLifecycleFact actual,
        object reservation,
        out IDomainFactPreparedPublication prepared,
        out string failure)
    {
        prepared = null;
        if (actual == null || actual.IsProposal)
        {
            failure = "invasion-lifecycle-actual-fact-invalid";
            return false;
        }
        if (reservation is not Reservation state)
        {
            failure = "invasion-lifecycle-reservation-invalid";
            return false;
        }
        if (state.Proposed.IsProposal
            && !actual.MatchesProposal(state.Proposed))
        {
            failure = "invasion-lifecycle-proposal-mismatch";
            return false;
        }
        return inner.TryPrepare(
            actual,
            state.Inner,
            out prepared,
            out failure);
    }

    public DomainFactDelivery Replay(in InvasionLifecycleFact fact) =>
        inner.Replay(fact);

    public bool TryPrepareReplay(
        string publicationKey,
        in InvasionLifecycleFact fact,
        out IDomainFactPreparedPublication prepared,
        out string failure)
    {
        if (fact == null || fact.IsProposal)
        {
            prepared = null;
            failure = "invasion-lifecycle-replay-fact-invalid";
            return false;
        }
        return inner.TryPrepareReplay(
            publicationKey,
            fact,
            out prepared,
            out failure);
    }

    public void Cancel(object reservation)
    {
        if (reservation is Reservation state)
            inner.Cancel(state.Inner);
    }

    private sealed class Reservation : IDomainFactReservedIdentity
    {
        public Reservation(InvasionLifecycleFact proposed, object inner)
        {
            Proposed = proposed;
            Inner = inner;
        }

        public InvasionLifecycleFact Proposed { get; }
        public object Inner { get; }
        public string PublicationKey =>
            ((IDomainFactReservedIdentity)Inner).PublicationKey;
    }
}

public sealed class InvasionLifecycleFactObserverBinding :
    IDomainFactObserverBinding
{
    private readonly InvasionLifecycleFactObserver observer;
    public IReadOnlyCollection<Type> FactTypes { get; } =
        Array.AsReadOnly(new[] { typeof(InvasionLifecycleFact) });

    public InvasionLifecycleFactObserverBinding(
        MigratedProducerOutcomeRuntime runtime,
        GameplayDomainFactCommitGroup commits,
        IGameplayOutcomeDiagnosticsQuery diagnostics) =>
        observer = new InvasionLifecycleFactObserver(
            runtime,
            commits,
            diagnostics);

    public IDisposable Connect(DomainFactDispatcher dispatcher) =>
        dispatcher.Subscribe(
            InvasionLifecycleFactObservation.ObserverId,
            observer);
}

public static class InvasionLifecycleFactRegistration
{
    public static void RegisterInvasionLifecycleFacts(
        this IContainerBuilder builder)
    {
        if (builder == null)
            throw new ArgumentNullException(nameof(builder));
        builder.Register(
                resolver => new InvasionLifecycleFactObserverBinding(
                    resolver.Resolve<MigratedProducerOutcomeRuntime>(),
                    resolver.Resolve<GameplayDomainFactCommitGroup>(),
                    resolver.Resolve<IGameplayOutcomeDiagnosticsQuery>()),
                Lifetime.Singleton)
            .As<IDomainFactObserverBinding>();
    }
}
