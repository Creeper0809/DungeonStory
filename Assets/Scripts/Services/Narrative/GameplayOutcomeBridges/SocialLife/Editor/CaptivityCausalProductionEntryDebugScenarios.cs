#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using DungeonStory.Foundation.Facts;
using DungeonStory.Narrative.Korean;
using Newtonsoft.Json.Linq;
using UnityEngine;

public static class CaptivityCausalProductionEntryDebugScenarios
{
    internal const string CausalOwnerRestoreKind =
        "owner-family:captivity-interaction-performer-terminal";
    internal const string InteractionActionId =
        "action:captivity-persuasion-resolve";
    internal const string MilestoneActionId =
        "action:captivity-performer-milestone-unlock";
    internal const string RansomActionId =
        "action:captivity-ransom-resolve";
    internal const string EscapeActionId =
        "action:captivity-escape-resolve";

    private const string OwnerKind =
        "owner-kind:captivity-interaction-performer-terminal";
    private const string DomainStateSchema =
        "dungeonstory-causal-captivity-owner-state@1";
    private const int AbsoluteDay = 37;
    private const int MilestoneThreshold = 50;
    private const string EscapeTrigger = "몸값 협상 중 열린 경계";

    internal static IReadOnlyList<IProductionCausalActionFixture> CreateFixtures(
        string causalRunId,
        string ownerId = null,
        JObject ownerCheckpoint = null)
    {
        if (string.IsNullOrWhiteSpace(causalRunId))
            throw new ArgumentException("A causal run ID is required.", nameof(causalRunId));
        bool persistent = !string.IsNullOrWhiteSpace(ownerId);
        if (!persistent && ownerCheckpoint != null)
        {
            throw new ArgumentException(
                "A captivity owner checkpoint requires an owner ID.",
                nameof(ownerCheckpoint));
        }

        var context = new Context(
            causalRunId,
            persistent ? ownerId : null,
            ownerCheckpoint);
        var lifetime = new SharedProductionCausalFixtureLifetime(4, context.Dispose);
        return new IProductionCausalActionFixture[]
        {
            Fixture(
                InteractionActionId,
                CaptivityInteractionOutcomeIds.Resolved,
                context,
                context.ResolveInteraction,
                context.RequireInteractionOutcome,
                lifetime),
            Fixture(
                MilestoneActionId,
                CaptivePerformerMilestoneOutcomeIds.Unlocked,
                context,
                context.UnlockMilestone,
                context.RequireMilestoneOutcome,
                lifetime),
            Fixture(
                RansomActionId,
                CaptiveRansomOutcomeIds.Ransomed,
                context,
                context.ResolveRansom,
                context.RequireRansomOutcome,
                lifetime),
            Fixture(
                EscapeActionId,
                CaptiveEscapeOutcomeIds.Escaped,
                context,
                context.ResolveEscape,
                context.RequireEscapeOutcome,
                lifetime)
        };
    }

    public static bool RunAll(bool log = false)
    {
        RunTerminalBranch(
            "owner:causal-captivity:ransom-focused",
            "run:causal-captivity:ransom",
            RansomActionId);
        RunTerminalBranch(
            "owner:causal-captivity:escape-focused",
            "run:causal-captivity:escape",
            EscapeActionId);

        JObject checkpoint;
        const string tamperOwner = "owner:causal-captivity:tamper-focused";
        using (var fixtures = new FixtureSet(CreateFixtures(
                   "run:causal-captivity:tamper-seed",
                   tamperOwner)))
        {
            Require(fixtures.Get(InteractionActionId).Execute(),
                "Captivity interaction did not execute before tamper test.");
            checkpoint = ((IProductionCausalOwnerCheckpointFixture)
                fixtures.Get(InteractionActionId)).CaptureOwnerCheckpoint();
        }
        ((JObject)checkpoint["domainState"])["captiveId"] =
            "character:causal-captivity:tampered";
        bool tamperRejected = false;
        try
        {
            using var ignored = new FixtureSet(CreateFixtures(
                "run:causal-captivity:tampered",
                tamperOwner,
                checkpoint));
        }
        catch (InvalidOperationException)
        {
            tamperRejected = true;
        }
        Require(tamperRejected,
            "A tampered captive identity was accepted across restore.");

        if (log)
        {
            Debug.Log(
                "Captivity interaction -> performer milestone -> ransom/escape causal branches PASS");
        }
        return true;
    }

    private static DelegateProductionCausalActionFixture Fixture(
        string actionId,
        GameplayOutcomeTypeId outcomeTypeId,
        Context context,
        Func<bool> execute,
        Func<GameplayOutcomeReplayIdentity> requireOutcome,
        SharedProductionCausalFixtureLifetime lifetime) => new(
        actionId,
        "captivity",
        outcomeTypeId,
        context.CaptureState,
        execute,
        requireOutcome,
        context.CaptureOutcomeEvidence,
        lifetime.Release,
        context.PersistentOwnerId,
        context.PersistentOwnerKind,
        context.HasPersistentOwner ? context.CaptureOwnerCheckpoint : null,
        context.HasPersistentOwner ? CausalOwnerRestoreKind : null,
        context.HasPersistentOwner ? context : null);

    private static void RunTerminalBranch(
        string ownerId,
        string runPrefix,
        string terminalActionId)
    {
        JObject checkpoint;
        using (var fixtures = new FixtureSet(CreateFixtures(
                   runPrefix + ":interaction",
                   ownerId)))
        {
            IProductionCausalActionFixture interaction =
                fixtures.Get(InteractionActionId);
            Require(interaction.Execute(),
                "The real captivity interaction outcome did not execute.");
            Require(interaction.RequireExactOutcome().HasCanonicalPayloadHash,
                "The captivity interaction has no canonical payload hash.");
            IReadOnlyDictionary<string, object> after = interaction.CaptureState();
            Require((bool)after["captivity.interactionResolved"]
                    && (float)after["captivity.trust"] == 44f
                    && (float)after["captivity.will"] == 62f,
                "The persuasion state transition was not retained.");
            checkpoint = ((IProductionCausalOwnerCheckpointFixture)interaction)
                .CaptureOwnerCheckpoint();
        }

        using (var fixtures = new FixtureSet(CreateFixtures(
                   runPrefix + ":milestone",
                   ownerId,
                   checkpoint)))
        {
            IProductionCausalActionFixture milestone = fixtures.Get(MilestoneActionId);
            Require(milestone.Execute(),
                "The real captive performer milestone did not execute.");
            Require(milestone.RequireExactOutcome().HasCanonicalPayloadHash,
                "The captive performer milestone has no canonical payload hash.");
            IReadOnlyDictionary<string, object> after = milestone.CaptureState();
            Require((bool)after["captivity.milestoneUnlocked"]
                    && (float)after["captivity.performerFame"] == 50f
                    && (int)after["captivity.privilegeTier"] == 1,
                "The performer milestone state transition was not retained.");
            checkpoint = ((IProductionCausalOwnerCheckpointFixture)milestone)
                .CaptureOwnerCheckpoint();
        }

        JObject terminalCheckpoint;
        using (var fixtures = new FixtureSet(CreateFixtures(
                   runPrefix + ":terminal",
                   ownerId,
                   checkpoint)))
        {
            IProductionCausalActionFixture terminal = fixtures.Get(terminalActionId);
            Require(terminal.Execute(),
                "The real captivity terminal outcome did not execute: "
                + terminalActionId);
            Require(terminal.RequireExactOutcome().HasCanonicalPayloadHash,
                "The captivity terminal outcome has no canonical payload hash.");
            IReadOnlyDictionary<string, object> after = terminal.CaptureState();
            bool expectedRansom = terminalActionId == RansomActionId;
            Require((bool)after[expectedRansom
                        ? "captivity.ransomResolved"
                        : "captivity.escaped"]
                    && !(bool)after[expectedRansom
                        ? "captivity.escaped"
                        : "captivity.ransomResolved"],
                "The captivity terminal branch was not exclusive.");
            Require(!fixtures.Get(expectedRansom ? EscapeActionId : RansomActionId)
                    .Execute(),
                "The mutually exclusive captivity terminal branch also executed.");
            terminalCheckpoint = ((IProductionCausalOwnerCheckpointFixture)terminal)
                .CaptureOwnerCheckpoint();
        }

        using (var fixtures = new FixtureSet(CreateFixtures(
                   runPrefix + ":final-restore",
                   ownerId,
                   terminalCheckpoint)))
        {
            IReadOnlyDictionary<string, object> restored = fixtures
                .Get(InteractionActionId).CaptureState();
            bool expectedRansom = terminalActionId == RansomActionId;
            Require((bool)restored[expectedRansom
                        ? "captivity.ransomResolved"
                        : "captivity.escaped"]
                    && !fixtures.Get(terminalActionId).Execute(),
                "The restored captivity terminal outcome executed twice or disappeared.");
        }
    }

    private sealed class Context : IDisposable
    {
        private readonly string ownerId;
        private readonly string captiveId;
        private readonly string wardenId;
        private readonly string facilityId;
        private readonly GameEventBus events;
        private readonly GameplayOutcomeLedger ledger;
        private readonly DomainFactDispatcher interactionFacts;
        private readonly IDisposable interactionObservation;
        private readonly DomainFactDispatcher milestoneFacts;
        private readonly IDisposable milestoneObservation;
        private readonly GameplayDomainFactObserver<CaptivePerformerMilestoneUnlockedFact, CaptivePerformerMilestoneOutcomeReceipt> milestoneObserver;
        private readonly DomainFactDispatcher ransomFacts;
        private readonly IDisposable ransomObservation;
        private readonly DomainFactDispatcher escapeFacts;
        private readonly IDisposable escapeObservation;
        private readonly CaptivityInteractionOutcomeRuntime interactionOutcomes;
        private readonly CaptivePerformerMilestoneOutcomeRuntime milestoneOutcomes;
        private readonly CaptiveRansomOutcomeRuntime ransomOutcomes;
        private readonly CaptiveEscapeOutcomeRuntime escapeOutcomes;
        private readonly CaptivityPerformerRuntime performer;
        private readonly CaptivePolicyData performerPolicy;
        private readonly FixedClock clock;
        private CaptiveState state;
        private bool interactionResolved;
        private bool milestoneUnlocked;
        private bool ransomResolved;
        private bool escaped;
        private bool disposed;

        internal Context(string causalRunId, string ownerId, JObject checkpoint)
        {
            this.ownerId = string.IsNullOrWhiteSpace(ownerId)
                ? string.Empty
                : CausalGameplaySyntax.RequireId(ownerId, nameof(ownerId));
            string stableSeed = HasPersistentOwner ? this.ownerId : causalRunId;
            string scope = ProductionCausalOutcomeEvidence.Sha256Text(stableSeed)
                .Substring("sha256:".Length, 24);
            captiveId = "character:causal-captivity:" + scope;
            wardenId = "character:causal-captivity-warden:" + scope;
            facilityId = "building:causal-captivity-cell:" + scope;
            clock = new FixedClock();
            events = new GameEventBus();

            IKoreanJosaFormatter josa = new KoreanJosaFormatter();
            GameplayOutcomeRegistry registry = new(
                new IGameplayOutcomeDescriptor[]
                {
                    new CaptivityInteractionOutcomeDescriptor(josa),
                    new CaptivePerformerMilestoneOutcomeDescriptor(josa),
                    new CaptiveRansomOutcomeDescriptor(josa),
                    new CaptiveEscapeOutcomeDescriptor(josa)
                },
                new IGameplayOutcomeAdapterRegistration[]
                {
                    new CaptivityInteractionOutcomeAdapter(),
                    new CaptivePerformerMilestoneOutcomeAdapter(),
                    new CaptiveRansomOutcomeAdapter(),
                    new CaptiveEscapeOutcomeAdapter()
                });
            string ledgerRunId = HasPersistentOwner
                ? ProductionCausalOwnerCheckpoint.LedgerRunId(this.ownerId, OwnerKind)
                : causalRunId + ":captivity-ledger";
            ledger = new GameplayOutcomeLedger(
                registry,
                new GameplayOutcomeBufferLimits(24, 0, 192),
                new GameplayOutcomeRunId(ledgerRunId),
                1L);
            GameplayOutcomeRecorder recorder = new(ledger, registry, events);
            interactionFacts = new DomainFactDispatcher();
            interactionObservation = interactionFacts.Subscribe(
                "captivity-interaction:causal-fixture",
                new GameplayDomainFactObserver<
                    CaptivityInteractionResolvedFact,
                    CaptivityInteractionOutcomeReceipt>(
                    CaptivityInteractionFactMapping.Capture,
                    registry,
                    ledger,
                    recorder,
                    new GameplayDomainFactCommitGroup(recorder, ledger)));
            milestoneFacts = new DomainFactDispatcher();
            milestoneObserver = new GameplayDomainFactObserver<
                    CaptivePerformerMilestoneUnlockedFact,
                    CaptivePerformerMilestoneOutcomeReceipt>(
                    CaptivePerformerMilestoneFactMapping.Capture,
                    registry,
                    ledger,
                    recorder,
                    new GameplayDomainFactCommitGroup(recorder, ledger));
            milestoneObservation = milestoneFacts.Subscribe(
                "captive-performer-milestone:causal-fixture", milestoneObserver);
            ransomFacts = new DomainFactDispatcher();
            ransomObservation = ransomFacts.Subscribe(
                "captive-ransom:causal-fixture",
                new GameplayDomainFactObserver<
                    CaptiveRansomAcceptedFact,
                    CaptiveRansomOutcomeReceipt>(
                    CaptiveRansomFactMapping.Capture,
                    registry,
                    ledger,
                    recorder,
                    new GameplayDomainFactCommitGroup(recorder, ledger)));
            escapeFacts = new DomainFactDispatcher();
            escapeObservation = escapeFacts.Subscribe(
                "captive-escape:causal-fixture",
                new GameplayDomainFactObserver<
                    CaptiveEscapedFact,
                    CaptiveEscapeOutcomeReceipt>(
                    CaptiveEscapeFactMapping.Capture,
                    registry,
                    ledger,
                    recorder,
                    new GameplayDomainFactCommitGroup(recorder, ledger)));
            interactionOutcomes = new CaptivityInteractionOutcomeRuntime(
                interactionFacts,
                interactionFacts,
                new RejectUnexpectedPhysicalOutput());
            milestoneOutcomes = new CaptivePerformerMilestoneOutcomeRuntime(
                milestoneFacts,
                milestoneFacts,
                new PerformerPort());
            ransomOutcomes = new CaptiveRansomOutcomeRuntime(
                ransomFacts,
                ransomFacts,
                events,
                CharacterAiEditorTestDependencies.WorldRegistry);
            escapeOutcomes = new CaptiveEscapeOutcomeRuntime(
                escapeFacts,
                escapeFacts,
                events);
            performerPolicy = new CaptivePolicyData
            {
                policyId = CaptivityPolicyIds.Performer,
                displayName = "공연 허용 수용",
                allowPerformance = true,
                allowRansom = true
            };
            performer = new CaptivityPerformerRuntime(
                id => string.Equals(id, captiveId, StringComparison.Ordinal)
                    ? state
                    : null,
                id => string.Equals(id, performerPolicy.policyId,
                    StringComparison.Ordinal)
                        ? performerPolicy
                        : null,
                (string _, out string reason) =>
                {
                    reason = "causal-recruit-not-used";
                    return false;
                },
                (string _, out string reason) =>
                {
                    reason = "causal-release-not-used";
                    return false;
                },
                new PerformerPort());

            try
            {
                if (checkpoint == null)
                    state = SeedState();
                else
                    RestoreOwnerCheckpoint(checkpoint);
                ValidateState();
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        internal bool HasPersistentOwner => ownerId.Length > 0;
        internal string PersistentOwnerId => ownerId;
        internal string PersistentOwnerKind => HasPersistentOwner ? OwnerKind : string.Empty;

        internal IReadOnlyDictionary<string, object> CaptureState()
        {
            ThrowIfDisposed();
            return new Dictionary<string, object>
            {
                ["captivity.ownerId"] = HasPersistentOwner
                    ? ownerId
                    : "owner:ephemeral-captivity",
                ["captivity.captiveId"] = captiveId,
                ["captivity.status"] = state.status.ToString(),
                ["captivity.will"] = state.will,
                ["captivity.fear"] = state.fear,
                ["captivity.trust"] = state.trust,
                ["captivity.grudge"] = state.grudge,
                ["captivity.performerFame"] = state.performerFame,
                ["captivity.performerSkill"] = state.performerSkill,
                ["captivity.privilegeTier"] = state.privilegeTier,
                ["captivity.carePriorityUnlocked"] = state.carePriorityUnlocked,
                ["captivity.interactionOutcomeRevision"] =
                    state.interactionOutcomeRevision,
                ["captivity.performerMilestoneOutcomeRevision"] =
                    state.performerMilestoneOutcomeRevision,
                ["captivity.ransomOutcomeRevision"] = state.ransomOutcomeRevision,
                ["captivity.escapeOutcomeRevision"] = state.escapeOutcomeRevision,
                ["captivity.retaliationPressure"] = state.retaliationPressure,
                ["captivity.ransomAmount"] = state.ransomAcceptedAmount,
                ["captivity.interactionResolved"] = interactionResolved,
                ["captivity.milestoneUnlocked"] = milestoneUnlocked,
                ["captivity.ransomResolved"] = ransomResolved,
                ["captivity.escaped"] = escaped
            };
        }

        internal bool ResolveInteraction()
        {
            ThrowIfDisposed();
            if (interactionResolved || milestoneUnlocked || ransomResolved || escaped)
                return false;
            var handler = new CaptivityPersuasionHandler();
            var context = new CaptivityInteractionContext(
                state,
                subjectAvailable: true,
                wardenAvailable: true,
                facilityAvailable: true,
                resultPosition: new Vector2Int(4, 7));
            if (!handler.CanExecute(context, out _))
                return false;
            CaptivityInteractionResult result = handler.Execute(context);
            string message = result.Message?.Trim() ?? string.Empty;
            if (message.Length == 0)
            {
                message = handler.DisplayName
                    + (result.Success ? " 완료" : " 실패");
            }
            int attempt = checked(state.interactionAttemptSequence + 1);
            state.interactionAttemptSequence = attempt;
            state.currentInteractionAttemptId = attempt;
            state.currentInteractionId = handler.InteractionId;
            state.status = CaptivityStatus.Interaction;
            state.interactionTerminal = new CaptivityInteractionTerminalState
            {
                captiveDisplayName = state.displayName,
                absoluteDay = AbsoluteDay,
                attemptId = attempt,
                interactionId = handler.InteractionId,
                interactionKind = handler.Kind,
                interactionDisplayName = handler.DisplayName,
                wardenId = wardenId,
                wardenDisplayName = "서린",
                facilityId = facilityId,
                facilityDisplayName = "북문 감방",
                resultGridX = context.ResultPosition.x,
                resultGridY = context.ResultPosition.y,
                success = result.Success,
                message = message,
                willBefore = state.will,
                willAfter = Mathf.Clamp(state.will + result.WillDelta, 0f, 100f),
                fearBefore = state.fear,
                fearAfter = Mathf.Clamp(state.fear + result.FearDelta, 0f, 100f),
                trustBefore = state.trust,
                trustAfter = Mathf.Clamp(state.trust + result.TrustDelta, 0f, 100f),
                grudgeBefore = state.grudge,
                grudgeAfter = Mathf.Clamp(state.grudge + result.GrudgeDelta, 0f, 100f),
                corruptionBefore = state.corruption,
                corruptionAfter = Mathf.Clamp(
                    state.corruption + result.CorruptionDelta,
                    0f,
                    100f)
            };
            if (!interactionOutcomes.TryPrepare(
                    state,
                    out CaptivityInteractionPublication prepared,
                    out long revision,
                    out _))
            {
                return false;
            }
            string stateSnapshot =
                CaptivityStateTransitionRules.CaptureStateSnapshot(state);
            DomainFactDelivery commit;
            using (prepared)
            {
                CaptivityInteractionTerminalState terminal = state.interactionTerminal;
                state.will = terminal.willAfter;
                state.fear = terminal.fearAfter;
                state.trust = terminal.trustAfter;
                state.grudge = terminal.grudgeAfter;
                state.corruption = terminal.corruptionAfter;
                terminal.outcomeRevision = revision;
                state.interactionOutcomeRevision = revision;
                commit = interactionOutcomes.Commit(prepared);
            }
            if (!commit.MayBeCommitted)
            {
                CaptivityStateTransitionRules.RestoreStateSnapshot(
                    stateSnapshot,
                    state);
                return false;
            }
            if (!interactionOutcomes.TryEnsureDelivered(state, out _))
                return false;
            interactionResolved = true;
            state.status = CaptivityStatus.Confined;
            state.lastResult = message;
            return true;
        }

        internal bool UnlockMilestone()
        {
            ThrowIfDisposed();
            if (!interactionResolved || milestoneUnlocked || ransomResolved || escaped)
                return false;
            state.policyId = performerPolicy.policyId;
            if (!performer.TryAssign(captiveId, true, out _)
                || !performer.TryRecord(captiveId, 1f, 2f, false, out _)
                || !milestoneOutcomes.TryCommitEligibleMilestones(
                    state,
                    AbsoluteDay,
                    out _)
                || !state.carePriorityUnlocked
                || state.performerMilestoneOutcomeRevision != 1L)
            {
                return false;
            }
            if (!performer.TryAssign(captiveId, false, out _))
                return false;
            milestoneUnlocked = true;
            return true;
        }

        internal bool ResolveRansom()
        {
            ThrowIfDisposed();
            if (!CanResolveTerminal())
                return false;
            CaptivityStatus previous = state.status;
            int amount = state.CalculateRansomValue(100f);
            float pressure = Mathf.Clamp(state.retaliationPressure + 12f, 0f, 100f);
            if (!ransomOutcomes.TryPrepare(
                    state,
                    previous,
                    amount,
                    pressure,
                    AbsoluteDay,
                    out CaptiveRansomPublication prepared,
                    out long revision,
                    out _))
            {
                return false;
            }
            string stateSnapshot =
                CaptivityStateTransitionRules.CaptureStateSnapshot(state);
            DomainFactDelivery commit;
            using (prepared)
            {
                state.status = CaptivityStatus.Ransom;
                state.ransomOutcomeRevision = revision;
                state.ransomAcceptedAmount = amount;
                state.ransomIncomeCredited = false;
                state.retaliationPressure = pressure;
                commit = ransomOutcomes.Commit(prepared);
            }
            if (!commit.MayBeCommitted)
            {
                CaptivityStateTransitionRules.RestoreStateSnapshot(
                    stateSnapshot,
                    state);
                return false;
            }
            if (!ransomOutcomes.TryBeginFinalization(
                    state,
                    out IDisposable finalization,
                    out _))
            {
                return false;
            }
            using (finalization)
            {
                state.ransomIncomeCredited = true;
                state.status = CaptivityStatus.Released;
                ransomResolved = true;
                state.lastResult = "몸값 " + amount + "골드로 석방";
                ransomOutcomes.NotifyCommitted(state);
            }
            return true;
        }

        internal bool ResolveEscape()
        {
            ThrowIfDisposed();
            if (!CanResolveTerminal())
                return false;
            state.falseCompliance = true;
            CaptivityStatus previous = state.status;
            float pressure = Mathf.Clamp(state.retaliationPressure + 15f, 0f, 100f);
            if (!escapeOutcomes.TryPrepare(
                    state,
                    CaptiveEscapeKind.FalseComplianceBetrayal,
                    EscapeTrigger,
                    previous,
                    pressure,
                    AbsoluteDay,
                    out CaptiveEscapePublication prepared,
                    out long revision,
                    out string trigger,
                    out _))
            {
                return false;
            }
            using (prepared)
            {
                state.status = CaptivityStatus.Escaped;
                state.escapeOutcomeRevision = revision;
                state.retaliationPressure = pressure;
                state.betrayalTrigger = trigger;
                DomainFactDelivery commit = escapeOutcomes.Commit(prepared);
                if (!commit.MayBeCommitted)
                    return false;
                escaped = true;
                state.lastResult = "거짓 순응 뒤 탈출";
                escapeOutcomes.NotifyCommitted(prepared);
                return true;
            }
        }

        internal GameplayOutcomeReplayIdentity RequireInteractionOutcome() =>
            RequireExact(
                CaptivityInteractionOutcomeIds.ResultKey(
                    captiveId,
                    1,
                    1L),
                CaptivityInteractionOutcomeIds.Resolved);

        internal GameplayOutcomeReplayIdentity RequireMilestoneOutcome() =>
            RequireExact(
                CaptivePerformerMilestoneOutcomeIds.ResultKey(
                    captiveId,
                    MilestoneThreshold,
                    1L),
                CaptivePerformerMilestoneOutcomeIds.Unlocked);

        internal GameplayOutcomeReplayIdentity RequireRansomOutcome() =>
            RequireExact(
                CaptiveRansomOutcomeIds.ResultKey(captiveId, 1L),
                CaptiveRansomOutcomeIds.Ransomed);

        internal GameplayOutcomeReplayIdentity RequireEscapeOutcome() =>
            RequireExact(
                CaptiveEscapeOutcomeIds.ResultKey(captiveId, 1L),
                CaptiveEscapeOutcomeIds.Escaped);

        internal JObject CaptureOutcomeEvidence(GameplayOutcomeReplayIdentity identity) =>
            ProductionCausalOutcomeEvidence.Capture(ledger, identity);

        internal JObject CaptureOwnerCheckpoint()
        {
            ThrowIfDisposed();
            if (!HasPersistentOwner)
                throw new InvalidOperationException("This captivity context has no owner.");
            return ProductionCausalOwnerCheckpoint.CaptureFamily(
                ownerId,
                OwnerKind,
                CausalOwnerRestoreKind,
                new JObject
                {
                    ["schemaVersion"] = 1,
                    ["schema"] = DomainStateSchema,
                    ["captiveId"] = captiveId,
                    ["wardenId"] = wardenId,
                    ["facilityId"] = facilityId,
                    ["captive"] = JObject.Parse(JsonUtility.ToJson(state)),
                    ["interactionResolved"] = interactionResolved,
                    ["milestoneUnlocked"] = milestoneUnlocked,
                    ["ransomResolved"] = ransomResolved,
                    ["escaped"] = escaped
                },
                ledger);
        }

        public void Dispose()
        {
            if (disposed)
                return;
            disposed = true;
            interactionObservation.Dispose();
            interactionFacts.Dispose();
            milestoneObservation.Dispose();
            milestoneFacts.Dispose();
            ransomObservation.Dispose();
            ransomFacts.Dispose();
            escapeObservation.Dispose();
            escapeFacts.Dispose();
        }

        private CaptiveState SeedState() => new()
        {
            captiveId = captiveId,
            displayName = "가람",
            speciesTag = "human",
            status = CaptivityStatus.Confined,
            policyId = CaptivityPolicyIds.Standard,
            housingBuildingId = facilityId,
            will = 65f,
            fear = 20f,
            trust = 35f,
            grudge = 14f,
            corruption = 0f,
            compliance = 52f,
            escapeRisk = 24f,
            performerFame = 49f,
            performerSkill = 10f,
            retaliationPressure = 8f,
            capturedAbsoluteDay = 29
        };

        private bool CanResolveTerminal() =>
            interactionResolved
            && milestoneUnlocked
            && !ransomResolved
            && !escaped
            && state.status == CaptivityStatus.Confined;

        private GameplayOutcomeReplayIdentity RequireExact(
            GameplayResultKey key,
            GameplayOutcomeTypeId expectedType)
        {
            bool valid = ledger.TryGetResultIdentity(
                    key,
                    out GameplayOutcomeReplayIdentity identity)
                && identity.State == GameplayOutcomeReplayState.PublishedAcknowledged
                && identity.HasCanonicalPayloadHash
                && ledger.TryGetExact(identity.OutcomeId, out GameplayOutcomeSnapshot exact)
                && exact != null
                && string.Equals(
                    exact.outcomeTypeId,
                    expectedType.Value,
                    StringComparison.Ordinal);
            if (!valid)
            {
                throw new InvalidOperationException(
                    "Causal captivity production entry did not publish exact outcome "
                    + expectedType.Value + ".");
            }
            return identity;
        }

        private void RestoreOwnerCheckpoint(JObject checkpoint)
        {
            JObject domain = ProductionCausalOwnerCheckpoint.RestoreFamilyCommon(
                checkpoint,
                ownerId,
                OwnerKind,
                CausalOwnerRestoreKind,
                ledger);
            RequireFields(
                domain,
                "causal captivity owner state",
                "schemaVersion",
                "schema",
                "captiveId",
                "wardenId",
                "facilityId",
                "captive",
                "interactionResolved",
                "milestoneUnlocked",
                "ransomResolved",
                "escaped");
            if ((int)domain["schemaVersion"] != 1
                || !string.Equals(
                    (string)domain["schema"],
                    DomainStateSchema,
                    StringComparison.Ordinal)
                || !string.Equals(
                    (string)domain["captiveId"],
                    captiveId,
                    StringComparison.Ordinal)
                || !string.Equals(
                    (string)domain["wardenId"],
                    wardenId,
                    StringComparison.Ordinal)
                || !string.Equals(
                    (string)domain["facilityId"],
                    facilityId,
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "Causal captivity owner identity changed across restore.");
            }
            state = JsonUtility.FromJson<CaptiveState>(
                domain["captive"]?.ToString() ?? string.Empty)
                ?? throw new InvalidOperationException(
                    "Causal captivity owner state could not be restored.");
            interactionResolved = (bool)domain["interactionResolved"];
            milestoneUnlocked = (bool)domain["milestoneUnlocked"];
            ransomResolved = (bool)domain["ransomResolved"];
            escaped = (bool)domain["escaped"];
        }

        private void ValidateState()
        {
            if (state == null
                || !string.Equals(state.captiveId, captiveId, StringComparison.Ordinal)
                || ransomResolved && escaped
                || interactionResolved != (state.interactionOutcomeRevision == 1L)
                || milestoneUnlocked != (state.performerMilestoneOutcomeRevision == 1L)
                || state.performerMilestones == null
                || state.performerMilestones.Count != (milestoneUnlocked ? 1 : 0)
                || ransomResolved != (state.ransomOutcomeRevision == 1L)
                || escaped != (state.escapeOutcomeRevision == 1L)
                || milestoneUnlocked && !interactionResolved
                || (ransomResolved || escaped) && !milestoneUnlocked
                || ransomResolved && state.status != CaptivityStatus.Released
                || escaped && state.status != CaptivityStatus.Escaped)
            {
                throw new InvalidOperationException(
                    "Causal captivity flags disagree with the authoritative captive state.");
            }
            foreach (var observation in state.performerMilestones)
            {
                if (observation?.fact == null || observation.pending || observation.observersPending
                    || observation.fact.captiveId != captiveId || observation.fact.threshold != 50
                    || observation.fact.revision != state.carePriorityOutcomeRevision)
                    throw new InvalidOperationException("Causal captivity milestone observation is incomplete.");
                var fact = new CaptivePerformerMilestoneUnlockedFact(observation.fact);
                var key = CaptivePerformerMilestoneFactMapping.Capture(fact).ResultKey;
                if (key.ToString() != observation.publicationKey || !ledger.TryGetResultIdentity(key, out _))
                    throw new InvalidOperationException("Causal captivity milestone canonical identity is missing.");
                bool exact = milestoneObserver.TryPrepareReplay(observation.publicationKey, fact, out var prepared, out string failure);
                prepared?.Cancel();
                if (!exact) throw new InvalidOperationException("Causal captivity milestone canonical payload changed: " + failure);
            }
        }

        private void ThrowIfDisposed()
        {
            if (disposed)
                throw new ObjectDisposedException(GetType().Name);
        }
    }

    private sealed class PerformerPort : ICaptivityPerformerPort
    {
        public bool IsActorAvailable(string captiveId) =>
            !string.IsNullOrWhiteSpace(captiveId);
        public void ApplyAssignmentState(string captiveId, bool assigned)
        {
        }
        public void Publish(CaptivePerformerMilestoneEvent gameEvent)
        {
        }
        public void RaiseAlert(
            string title,
            string message,
            CaptivityMilestoneImportance importance,
            string category)
        {
        }
    }

    private sealed class RejectUnexpectedPhysicalOutput :
        IPhysicalItemSourcePublicationService
    {
        public bool TryEnsureLooseOutputs(
            IReadOnlyDictionary<string, int> outputs,
            Vector2Int outputPosition,
            string operationId,
            string reasonCode,
            out PhysicalItemSourcePublicationReceipt receipt,
            out string failureReason)
        {
            receipt = default;
            failureReason = "causal-captivity-unexpected-physical-output";
            return false;
        }
    }

    private sealed class FixedClock : IGameClock
    {
        public float DeltaTime => 1f / 60f;
        public float Time => AbsoluteDay * GameCalendarRules.SecondsPerDay;
        public int FrameCount => 1;
        public bool IsPaused => false;
    }

    private sealed class FixtureSet : IDisposable
    {
        private readonly IReadOnlyList<IProductionCausalActionFixture> fixtures;

        internal FixtureSet(IReadOnlyList<IProductionCausalActionFixture> fixtures) =>
            this.fixtures = fixtures;

        internal IProductionCausalActionFixture Get(string actionId) => fixtures.Single(
            value => string.Equals(value.ActionId, actionId, StringComparison.Ordinal));

        public void Dispose()
        {
            foreach (IProductionCausalActionFixture fixture in fixtures)
                fixture.Dispose();
        }
    }

    private static void RequireFields(
        JObject value,
        string label,
        params string[] fields)
    {
        string[] actual = value.Properties()
            .Select(property => property.Name)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();
        string[] expected = fields
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();
        if (!actual.SequenceEqual(expected, StringComparer.Ordinal))
        {
            throw new InvalidOperationException(
                label + " fields mismatch. actual=" + string.Join(",", actual)
                + "; expected=" + string.Join(",", expected));
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
#endif
