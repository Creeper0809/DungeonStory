#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DungeonStory.Foundation;
using DungeonStory.Foundation.Facts;
using DungeonStory.Narrative.Korean;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public static partial class MigratedProducerCombatDefenseEntryE2EDebugScenarios
{
    private static readonly MigratedProducerOutcomeKind[] CoveredKindArray =
    {
        MigratedProducerOutcomeKind.CharacterBodyHealthDownedEvent,
        MigratedProducerOutcomeKind.CharacterBodyHealthRecoveredEvent,
        MigratedProducerOutcomeKind.CharacterDeathEvent,
        MigratedProducerOutcomeKind.CharacterKilledEvent,
        MigratedProducerOutcomeKind.CombatAttackResult,
        MigratedProducerOutcomeKind.DefenseFacilityStateChangedEvent,
        MigratedProducerOutcomeKind.DefenseFacilityTriggeredEvent,
        MigratedProducerOutcomeKind.DefenseFrontCollapsedEvent,
        MigratedProducerOutcomeKind.HealthThresholdCrossedEvent,
        MigratedProducerOutcomeKind.InvasionDungeonBreachedEvent,
        MigratedProducerOutcomeKind.InvasionFacilityDamagedEvent,
        MigratedProducerOutcomeKind.InvasionResolvedEvent,
        MigratedProducerOutcomeKind.InvasionStartedEvent
    };

    public static IReadOnlyList<MigratedProducerOutcomeKind> CoveredKinds { get; }
        = Array.AsReadOnly(CoveredKindArray);

    internal static IProductionCausalActionFixture CreateCausalCombatFixture(
        string causalRunId = null,
        MigratedProducerOutcomeFaultMode faultMode =
            MigratedProducerOutcomeFaultMode.None,
        string episodeOwnerId = null)
    {
        const MigratedProducerOutcomeKind Kind =
            MigratedProducerOutcomeKind.CombatAttackResult;
        var harness = new OutcomeHarness(
            Kind,
            "causal-plan",
            includeCombatDamage: true,
            faultMode: faultMode,
            deliveryFault: false,
            stableRunId: string.IsNullOrWhiteSpace(causalRunId)
                ? null
                : causalRunId + ":combat-ledger");
        var entry = new CombatEntryFixture(
            harness,
            Kind,
            string.IsNullOrWhiteSpace(episodeOwnerId)
                ? causalRunId
                : episodeOwnerId);
        return new DelegateProductionCausalActionFixture(
            "action:resolve-combat-attack",
            "combat",
            Kind,
            () => new Dictionary<string, object>
            {
                ["combat.ownerSnapshot"] = entry.CaptureDomainState()
            },
            entry.InvokeProductionEntry,
            () => harness.RequireExact(
                Kind,
                GameplayOutcomeReplayState.PublishedAcknowledged),
            identity => ProductionCausalOutcomeEvidence.Capture(
                harness.Ledger,
                identity),
            () =>
            {
                entry.Dispose();
                harness.Dispose();
            });
    }

    internal static IProductionCausalActionFixture CreateCausalFacilityFixture(
        string causalRunId = null,
        MigratedProducerOutcomeFaultMode faultMode =
            MigratedProducerOutcomeFaultMode.None,
        string ownerId = null,
        JObject ownerCheckpoint = null)
    {
        const string ActionId = "action:trigger-defense-facility";
        const string OwnerKind = "defense-facility";
        const MigratedProducerOutcomeKind Kind =
            MigratedProducerOutcomeKind.DefenseFacilityTriggeredEvent;
        bool persistent = !string.IsNullOrWhiteSpace(ownerId);
        if (!persistent && ownerCheckpoint != null)
        {
            throw new ArgumentException(
                "A facility owner checkpoint requires a stable owner ID.",
                nameof(ownerCheckpoint));
        }
        string ledgerRunId = persistent
            ? ProductionCausalOwnerCheckpoint.LedgerRunId(ownerId, OwnerKind)
            : string.IsNullOrWhiteSpace(causalRunId)
                ? null
                : causalRunId + ":facility-ledger";
        var harness = new OutcomeHarness(
            Kind,
            "causal-plan",
            includeCombatDamage: false,
            faultMode: faultMode,
            deliveryFault: false,
            stableRunId: ledgerRunId);
        var entry = new DefenseFacilityEntryFixture(
            harness,
            persistent ? ownerId : causalRunId,
            causalRunId);
        try
        {
            if (ownerCheckpoint != null)
            {
                JObject domainState = ProductionCausalOwnerCheckpoint.RestoreCommon(
                    ownerCheckpoint,
                    ownerId,
                    OwnerKind,
                    ActionId,
                    harness.Ledger,
                    harness.Transaction);
                entry.RestoreOwnerCheckpoint(domainState);
            }
        }
        catch
        {
            entry.Dispose();
            harness.Dispose();
            throw;
        }
        return new DelegateProductionCausalActionFixture(
            ActionId,
            "facility",
            Kind,
            () => new Dictionary<string, object>
            {
                ["facility.ownerSnapshot"] = entry.CaptureDomainState()
            },
            entry.InvokeProductionEntry,
            () => harness.RequireExact(
                Kind,
                GameplayOutcomeReplayState.PublishedAcknowledged),
            identity => ProductionCausalOutcomeEvidence.Capture(
                harness.Ledger,
                identity),
            () =>
            {
                entry.Dispose();
                harness.Dispose();
            },
            persistent ? ownerId : null,
            persistent ? OwnerKind : null,
            persistent
                ? () => ProductionCausalOwnerCheckpoint.Capture(
                    ownerId,
                    OwnerKind,
                    ActionId,
                    entry.CaptureOwnerCheckpoint(),
                    harness.Ledger,
                    harness.Transaction)
                : null,
            companionOutcomeKinds: new[]
            {
                MigratedProducerOutcomeKind.DefenseFacilityStateChangedEvent
            },
            requireExactCompanions: () => new[]
            {
                harness.RequireExact(
                    MigratedProducerOutcomeKind.DefenseFacilityStateChangedEvent,
                    GameplayOutcomeReplayState.PublishedAcknowledged)
            });
    }

    internal static IReadOnlyList<IProductionCausalActionFixture>
        CreateCausalInjuryRecoveryFixtures(
            string causalRunId,
            string protagonistId) => CreateCausalCombatLifeCycleFixtures(
                causalRunId,
                protagonistId);

    public static bool RunAll(bool throwOnFailure = true)
    {
        try
        {
            var observed = new HashSet<MigratedProducerOutcomeKind>();
            VerifyKinds(
                observed,
                new[]
                {
                    MigratedProducerOutcomeKind.CharacterBodyHealthRecoveredEvent,
                    MigratedProducerOutcomeKind.CharacterDeathEvent,
                    MigratedProducerOutcomeKind.HealthThresholdCrossedEvent
                },
                includeCombatDamage: false,
                (harness, kind) => new HealthEntryFixture(harness, kind));
            VerifyKinds(
                observed,
                new[]
                {
                    MigratedProducerOutcomeKind.CharacterBodyHealthDownedEvent,
                    MigratedProducerOutcomeKind.CharacterKilledEvent,
                    MigratedProducerOutcomeKind.CombatAttackResult
                },
                includeCombatDamage: true,
                (harness, kind) => new CombatEntryFixture(harness, kind));
            VerifyKinds(
                observed,
                new[]
                {
                    MigratedProducerOutcomeKind.DefenseFacilityStateChangedEvent,
                    MigratedProducerOutcomeKind.DefenseFacilityTriggeredEvent
                },
                includeCombatDamage: false,
                (harness, kind) => new DefenseFacilityEntryFixture(harness));
            VerifyKinds(
                observed,
                new[]
                {
                    MigratedProducerOutcomeKind.DefenseFrontCollapsedEvent,
                    MigratedProducerOutcomeKind.InvasionDungeonBreachedEvent,
                    MigratedProducerOutcomeKind.InvasionFacilityDamagedEvent,
                    MigratedProducerOutcomeKind.InvasionResolvedEvent
                },
                includeCombatDamage: false,
                (harness, kind) => new InvasionIntruderEntryFixture(
                    harness,
                    kind));
            VerifyKinds(
                observed,
                new[] { MigratedProducerOutcomeKind.InvasionStartedEvent },
                includeCombatDamage: false,
                (harness, _) => new InvasionDirectorEntryFixture(harness));
            VerifyCellFireOutcomeBoundary();

            Require(
                CoveredKindArray.Distinct().Count() == CoveredKindArray.Length,
                "Combat/defense migrated-producer coverage contains duplicates.");
            Require(
                observed.Count == CoveredKindArray.Length
                && observed.SetEquals(CoveredKindArray),
                "Combat/defense migrated-producer observed coverage drifted from CoveredKinds.");
            return true;
        }
        catch
        {
            if (throwOnFailure)
                throw;
            return false;
        }
    }

    internal static void VerifyInvasionObserverFocused()
    {
        VerifyKind(
            MigratedProducerOutcomeKind.InvasionStartedEvent,
            includeCombatDamage: false,
            (harness, _) => new InvasionDirectorEntryFixture(harness));
        VerifyKind(
            MigratedProducerOutcomeKind.InvasionResolvedEvent,
            includeCombatDamage: false,
            (harness, kind) => new InvasionIntruderEntryFixture(
                harness,
                kind));
        Require(
            RunInvasionLifecycleCausalOwnerFamily(log: true),
            "The focused invasion lifecycle causal owner family returned false.");
    }

    public static void RunD01FromBatch()
    {
        string configured = Environment.GetEnvironmentVariable(
            "DUNGEONSTORY_U0_D01_COMBAT_EVIDENCE_REPORT_PATH");
        if (string.IsNullOrWhiteSpace(configured))
        {
            throw new InvalidOperationException(
                "DUNGEONSTORY_U0_D01_COMBAT_EVIDENCE_REPORT_PATH is required.");
        }

        string projectRoot = Directory.GetParent(Application.dataPath)?.FullName
            ?? throw new InvalidOperationException(
                "Unity project root is unavailable.");
        string fullPath = Path.GetFullPath(Path.Combine(projectRoot, configured));
        string qaRoot = Path.GetFullPath(Path.Combine(projectRoot, "Artifacts/QA"));
        if (!fullPath.StartsWith(
                qaRoot + Path.DirectorySeparatorChar,
                StringComparison.OrdinalIgnoreCase)
            || File.Exists(fullPath))
        {
            throw new InvalidOperationException(
                "D01 combat-evidence report must be a create-only path under Artifacts/QA.");
        }

        VerifyD01FrozenAttackEvidence();
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)
            ?? throw new InvalidOperationException(
                "Report directory is unavailable."));
        using FileStream stream = new(
            fullPath,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None);
        using StreamWriter writer = new(stream);
        writer.Write(
            "U0_D01_COMBAT_EVIDENCE=PASS\n"
            + "variants=hit,miss,cover-blocked,shield-blocked\n"
            + "frozen=weapon,instance,damage-type,fire-mode,ammo-before-consumed-after,result-flags,status-effects,cover,armor\n"
            + "join=attack-parent,damage-child,mechanical-mutation\n"
            + "recovery=commit-reject-rollback,delivery-retry-no-remutation\n"
            + "fullAuditClosure=false\n"
            + "humanApprovalClaimed=false\n"
            + "trainingEligible=false\n");
        Debug.Log("U0_D01_COMBAT_EVIDENCE=PASS; report=" + fullPath);
    }

    private static void VerifyD01FrozenAttackEvidence()
    {
        VerifyKind(
            MigratedProducerOutcomeKind.CombatAttackResult,
            includeCombatDamage: true,
            (harness, kind) => new CombatEntryFixture(harness, kind));

        VerifyD01ResultVariant(
            "miss",
            new CombatAttackResult(
                executed: true,
                hit: false,
                coverBlocked: false,
                evaded: false,
                bodyPart: CombatBodyPart.Torso,
                rawDamage: 0f,
                appliedDamage: 0f,
                bleeding: 0f,
                suppression: 0f,
                armorDurabilityDamage: 0f,
                armorInstanceId: string.Empty,
                failureReason: string.Empty,
                ammunitionItemId: "item:test-arrow",
                ammunitionConsumed: 1),
            "executed=1;hit=0;coverBlocked=0;shieldBlocked=0;evaded=0;nonlethal=0",
            "flags=None;potency=0;turns=0",
            "none",
            "0",
            "none");
        VerifyD01ResultVariant(
            "cover",
            new CombatAttackResult(
                executed: true,
                hit: false,
                coverBlocked: true,
                evaded: false,
                bodyPart: CombatBodyPart.Torso,
                rawDamage: 0f,
                appliedDamage: 0f,
                bleeding: 0f,
                suppression: 0f,
                armorDurabilityDamage: 0f,
                armorInstanceId: string.Empty,
                failureReason: string.Empty,
                coverSourceId: "cover:d01",
                coverDamage: 1.25f,
                ammunitionItemId: "item:test-arrow",
                ammunitionConsumed: 1),
            "executed=1;hit=0;coverBlocked=1;shieldBlocked=0;evaded=0;nonlethal=0",
            "flags=None;potency=0;turns=0",
            "cover:d01",
            "1.25",
            "none");
        VerifyD01ResultVariant(
            "shield",
            new CombatAttackResult(
                executed: true,
                hit: false,
                coverBlocked: false,
                evaded: false,
                bodyPart: CombatBodyPart.Torso,
                rawDamage: 0f,
                appliedDamage: 0f,
                bleeding: 0f,
                suppression: 0f,
                armorDurabilityDamage: 1.5f,
                armorInstanceId: "shield-instance:d01",
                failureReason: string.Empty,
                shieldBlocked: true,
                armorDurabilityHits: new[]
                {
                    new CombatArmorDurabilityHit(
                        "shield-instance:d01",
                        1.5f)
                },
                ammunitionItemId: "item:test-arrow",
                ammunitionConsumed: 1),
            "executed=1;hit=0;coverBlocked=0;shieldBlocked=1;evaded=0;nonlethal=0",
            "flags=None;potency=0;turns=0",
            "none",
            "0",
            "shield-instance:d01=1.5");
    }

    private static void VerifyD01ResultVariant(
        string caseId,
        in CombatAttackResult result,
        string expectedFlags,
        string expectedEffects,
        string expectedCoverSource,
        string expectedCoverDamage,
        string expectedArmorHits)
    {
        using var harness = new OutcomeHarness(
            MigratedProducerOutcomeKind.CombatAttackResult,
            "d01-" + caseId,
            includeCombatDamage: true,
            MigratedProducerOutcomeFaultMode.None,
            deliveryFault: false);
        using var fixture = new CellFireOutcomeFixture(harness);
        CombatOutcomeApplyResult applied = fixture.ExecuteVariant(caseId, result);
        Require(applied.Succeeded
                && fixture.Ammunition == 2
                && fixture.Revision == 2
                && fixture.CompleteCount == 1,
            "D01 attack evidence variant did not commit: " + caseId);
        GameplayOutcomeReplayIdentity identity = harness.RequireExact(
            MigratedProducerOutcomeKind.CombatAttackResult,
            GameplayOutcomeReplayState.PublishedAcknowledged);
        Require(
            harness.Ledger.TryGetExact(
                identity.OutcomeId,
                out GameplayOutcomeSnapshot snapshot)
            && snapshot?.facts != null
            && snapshot.facts.Count == 16,
            "D01 attack evidence snapshot is incomplete: " + caseId);
        RequireFact(snapshot, "weapon-definition-id", "weapon:test-cell-bow");
        RequireFact(snapshot, "weapon-instance-id", "weapon-instance:test-cell-bow");
        RequireFact(snapshot, "damage-type", "Pierce");
        RequireFact(snapshot, "fire-mode", "Aimed");
        RequireFact(snapshot, "ammunition-item-id", "item:test-arrow");
        RequireFact(snapshot, "ammunition-before", "3");
        RequireFact(snapshot, "ammunition-consumed", "1");
        RequireFact(snapshot, "ammunition-after", "2");
        RequireFact(snapshot, "result-flags", expectedFlags);
        RequireFact(snapshot, "special-effects", expectedEffects);
        RequireFact(snapshot, "cover-source-id", expectedCoverSource);
        RequireFact(snapshot, "cover-damage", expectedCoverDamage);
        RequireFact(snapshot, "armor-hits", expectedArmorHits);
    }

    private static void VerifyKinds(
        ISet<MigratedProducerOutcomeKind> observed,
        IEnumerable<MigratedProducerOutcomeKind> kinds,
        bool includeCombatDamage,
        Func<OutcomeHarness, MigratedProducerOutcomeKind, IEntryFixture>
            fixtureFactory)
    {
        foreach (MigratedProducerOutcomeKind kind in kinds)
        {
            VerifyKind(kind, includeCombatDamage, fixtureFactory);
            Require(
                observed.Add(kind),
                "Migrated producer kind was exercised more than once: " + kind);
        }
    }

    private static void VerifyKind(
        MigratedProducerOutcomeKind kind,
        bool includeCombatDamage,
        Func<OutcomeHarness, MigratedProducerOutcomeKind, IEntryFixture>
            fixtureFactory)
    {
        using (var successHarness = new OutcomeHarness(
                   kind,
                   "success",
                   includeCombatDamage,
                   MigratedProducerOutcomeFaultMode.None,
                   deliveryFault: false))
        using (IEntryFixture entry = fixtureFactory(successHarness, kind))
        {
            string before = entry.CaptureDomainState();
            Require(
                Invoke(entry, out Exception failure),
                "Production entry failed in success phase for " + kind
                + FailureSuffix(failure));
            string after = entry.CaptureDomainState();
            Require(
                !string.Equals(before, after, StringComparison.Ordinal),
                "Production entry did not mutate authoritative domain state for "
                + kind);
            GameplayOutcomeReplayIdentity identity = successHarness.RequireExact(
                kind,
                GameplayOutcomeReplayState.PublishedAcknowledged,
                before,
                after);
            if (kind == MigratedProducerOutcomeKind.CombatAttackResult)
                RequireCombatAttackEvidence(successHarness.Ledger, identity);
        }

        using (var rejectedHarness = new OutcomeHarness(
                   kind,
                   "reject",
                   includeCombatDamage,
                   MigratedProducerOutcomeFaultMode.RejectCommit,
                   deliveryFault: false))
        using (IEntryFixture entry = fixtureFactory(rejectedHarness, kind))
        {
            string domainBefore = entry.CaptureDomainState();
            string ownersBefore = rejectedHarness.CaptureOwnerState();
            bool accepted = Invoke(entry, out _);
            Require(
                !accepted,
                "Injected definite commit rejection was accepted for " + kind);
            Require(
                string.Equals(
                    domainBefore,
                    entry.CaptureDomainState(),
                    StringComparison.Ordinal),
                "Definite commit rejection did not roll domain state back for "
                + kind);
            Require(
                string.Equals(
                    ownersBefore,
                    rejectedHarness.CaptureOwnerState(),
                    StringComparison.Ordinal),
                "Definite commit rejection advanced migrated owner state for "
                + kind);
            Require(
                rejectedHarness.Probe.ReservationCount(kind) > 0
                && rejectedHarness.Probe.CommitAttemptCount(kind) > 0
                && rejectedHarness.Probe.CommitCount(kind) == 0,
                "Definite commit rejection did not cross the production reservation boundary for "
                + kind);
        }

        using (var deliveryHarness = new OutcomeHarness(
                   kind,
                   "delivery",
                   includeCombatDamage,
                   MigratedProducerOutcomeFaultMode.None,
                   deliveryFault: true))
        using (IEntryFixture entry = fixtureFactory(deliveryHarness, kind))
        {
            string before = entry.CaptureDomainState();
            Require(
                Invoke(entry, out Exception failure),
                "Production entry failed before durable delivery retry for "
                + kind + FailureSuffix(failure));
            string committed = entry.CaptureDomainState();
            Require(
                !string.Equals(before, committed, StringComparison.Ordinal),
                "Delivery-fault commit did not mutate authoritative state for "
                + kind);
            deliveryHarness.RequirePendingThenRetry(kind);
            Require(
                string.Equals(
                    committed,
                    entry.CaptureDomainState(),
                    StringComparison.Ordinal),
                "Delivery retry duplicated the domain mutation for " + kind);
            Require(
                deliveryHarness.Probe.CommitCount(kind) == 1,
                "Delivery retry crossed the domain commit boundary more than once for "
                + kind);
        }
    }

    private static bool Invoke(IEntryFixture fixture, out Exception failure)
    {
        try
        {
            failure = null;
            return fixture.InvokeProductionEntry();
        }
        catch (Exception exception) when (
            exception is not OutOfMemoryException
            && exception is not StackOverflowException
            && exception is not AccessViolationException)
        {
            failure = exception;
            return false;
        }
    }

    private static string FailureSuffix(Exception exception) => exception == null
        ? string.Empty
        : "; " + exception.GetType().Name + ": " + exception.Message;

    private static void RequireCombatAttackEvidence(
        GameplayOutcomeLedger ledger,
        in GameplayOutcomeReplayIdentity identity)
    {
        Require(
            ledger.TryGetExact(identity.OutcomeId, out GameplayOutcomeSnapshot snapshot)
            && snapshot?.facts != null,
            "Combat attack evidence snapshot is missing.");
        Require(snapshot.facts.Count == 16,
            "Combat attack evidence did not freeze the exact 16-fact contract.");
        RequireFact(snapshot, "weapon-definition-id", "weapon:test-bow");
        RequireFact(snapshot, "weapon-instance-id", "weapon-instance:test-bow");
        RequireFact(snapshot, "damage-type", "Pierce");
        RequireFact(snapshot, "fire-mode", "Rapid");
        RequireFact(snapshot, "ammunition-item-id", "item:test-arrow");
        RequireFact(snapshot, "ammunition-before", "7");
        RequireFact(snapshot, "ammunition-consumed", "1");
        RequireFact(snapshot, "ammunition-after", "6");
        RequireFact(
            snapshot,
            "result-flags",
            "executed=1;hit=1;coverBlocked=0;shieldBlocked=0;evaded=0;nonlethal=0");
        RequireFact(
            snapshot,
            "special-effects",
            "flags=Burning;potency=0.75;turns=3");
        RequireFact(snapshot, "cover-source-id", "none");
        RequireFact(snapshot, "cover-damage", "0");
        RequireFact(snapshot, "armor-hits", "armor-instance:test=2.5");
    }

    private static void RequireFact(
        GameplayOutcomeSnapshot snapshot,
        string factId,
        string expectedValue)
    {
        Require(
            snapshot.facts.Any(fact => fact != null
                && string.Equals(fact.factId, factId, StringComparison.Ordinal)
                && string.Equals(
                    fact.value,
                    expectedValue,
                    StringComparison.Ordinal)),
            "Combat attack evidence fact mismatch: " + factId);
    }

    private static void VerifyCellFireOutcomeBoundary()
    {
        using (var successHarness = new OutcomeHarness(
                   MigratedProducerOutcomeKind.CombatAttackResult,
                   "cell-fire-success",
                   includeCombatDamage: true,
                   MigratedProducerOutcomeFaultMode.None,
                   deliveryFault: false))
        using (var fixture = new CellFireOutcomeFixture(successHarness))
        {
            Require(fixture.Execute(injectAmmunitionFailure: false).Succeeded,
                "Cell fire did not commit through the common attack boundary.");
            Require(fixture.Mana == 15f
                    && fixture.Ammunition == 2
                    && fixture.Revision == 2
                    && fixture.RollbackCount == 0
                    && fixture.CompleteCount == 1,
                "Committed cell fire did not consume costs exactly once.");
            GameplayOutcomeReplayIdentity identity = successHarness.RequireExact(
                MigratedProducerOutcomeKind.CombatAttackResult,
                GameplayOutcomeReplayState.PublishedAcknowledged);
            Require(successHarness.Ledger.TryGetExact(
                    identity.OutcomeId,
                    out GameplayOutcomeSnapshot snapshot)
                && snapshot.subjects.Any(subject => subject != null
                    && string.Equals(
                        subject.entityKindId,
                        "grid-cell",
                        StringComparison.Ordinal)
                    && string.Equals(
                        subject.entityId,
                        "grid-cell:4:7",
                        StringComparison.Ordinal))
                && snapshot.facts.Any(fact => fact != null
                    && string.Equals(fact.factId, "detail", StringComparison.Ordinal)
                    && fact.value.Contains("hit=0;damage=0", StringComparison.Ordinal)),
                "Cell fire invented a victim/damage result or lost its cell subject.");

            Require(fixture.Execute(injectAmmunitionFailure: false).Succeeded,
                "Repeated cell fire did not advance to a distinct revision.");
            Require(fixture.Mana == 10f
                    && fixture.Ammunition == 1
                    && fixture.Revision == 3
                    && fixture.CompleteCount == 2
                    && successHarness.Ledger.CaptureGameplayOutcomes()
                        .exactOutcomes.Count == 2,
                "Repeated cell fire duplicated or skipped its cost/result boundary.");
        }

        using (var rejectedHarness = new OutcomeHarness(
                   MigratedProducerOutcomeKind.CombatAttackResult,
                   "cell-fire-rejected",
                   includeCombatDamage: true,
                   MigratedProducerOutcomeFaultMode.RejectCommit,
                   deliveryFault: false))
        using (var fixture = new CellFireOutcomeFixture(rejectedHarness))
        {
            CombatOutcomeApplyResult rejected = fixture.Execute(
                injectAmmunitionFailure: false);
            Require(!rejected.Succeeded
                    && fixture.Mana == 20f
                    && fixture.Ammunition == 3
                    && fixture.Revision == 1
                    && fixture.RollbackCount == 1
                    && fixture.CompleteCount == 0
                    && rejectedHarness.Probe.CommitCount(
                        MigratedProducerOutcomeKind.CombatAttackResult) == 0,
                "Rejected cell-fire outcome did not restore MP, ammunition and revision.");
        }

        using (var failureHarness = new OutcomeHarness(
                   MigratedProducerOutcomeKind.CombatAttackResult,
                   "cell-fire-ammunition-failure",
                   includeCombatDamage: true,
                   MigratedProducerOutcomeFaultMode.None,
                   deliveryFault: false))
        using (var fixture = new CellFireOutcomeFixture(failureHarness))
        {
            CombatOutcomeApplyResult failed = fixture.Execute(
                injectAmmunitionFailure: true);
            Require(!failed.Succeeded
                    && fixture.Mana == 20f
                    && fixture.Ammunition == 3
                    && fixture.Revision == 1
                    && fixture.RollbackCount == 1
                    && fixture.CompleteCount == 0
                    && failureHarness.Probe.CommitAttemptCount(
                        MigratedProducerOutcomeKind.CombatAttackResult) == 0,
                "Cell-fire ammunition failure consumed MP or leaked an outcome.");
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    private sealed class CellFireOutcomeFixture : IDisposable
    {
        private readonly OutcomeHarness harness;
        private readonly List<UnityEngine.Object> cleanup = new();
        private readonly List<CombatCellShotPendingData> pending = new();
        private readonly CharacterActor attacker;
        private readonly CharacterActor target;
        private readonly CombatCommandResultApplier applier;

        internal CellFireOutcomeFixture(OutcomeHarness harness)
        {
            this.harness = harness ?? throw new ArgumentNullException(nameof(harness));
            CharacterSO data = CharacterAiEditorTestDependencies
                .CreateCharacterFixtureData(
                    CharacterType.Customer,
                    "칸 사격 시험자",
                    "human");
            cleanup.Add(data);
            var actorObject = new GameObject("칸 사격 시험자");
            cleanup.Add(actorObject);
            attacker = actorObject.AddComponent<CharacterActor>();
            CharacterAiEditorTestDependencies.Inject(actorObject);
            attacker.EnsureRuntimeState();
            attacker.data = data;
            attacker.characterType = CharacterType.Customer;
            attacker.Identity.SetPersistentId("character:cell-fire-attacker");
            CharacterAiEditorTestDependencies.WorldRegistry.RegisterCharacter(
                attacker);

            CharacterSO targetData = CharacterAiEditorTestDependencies
                .CreateCharacterFixtureData(
                    CharacterType.NPC,
                    "칸 사격 증거 대상",
                    "human");
            cleanup.Add(targetData);
            var targetObject = new GameObject("칸 사격 증거 대상");
            cleanup.Add(targetObject);
            target = targetObject.AddComponent<CharacterActor>();
            CharacterAiEditorTestDependencies.Inject(targetObject);
            target.EnsureRuntimeState();
            target.data = targetData;
            target.characterType = CharacterType.NPC;
            target.Identity.SetPersistentId("character:cell-fire-target");
            CharacterAiEditorTestDependencies.WorldRegistry.RegisterCharacter(
                target);

            CharacterBodyHealthRuntime health =
                CharacterAiEditorTestDependencies.BodyHealthRuntime;
            health.ConfigureVitals(target, 100f, resetCurrentHealth: true);
            applier = new CombatCommandResultApplier(
                CharacterAiEditorTestDependencies.CombatEquipment,
                health,
                health,
                health,
                harness.CombatActions,
                new NoCoverDurabilityRegistry(),
                CharacterAiEditorTestDependencies.GameClock,
                new NoWorldUiHierarchy(),
                new GameEventBus(),
                CharacterAiEditorTestDependencies.WorldRegistry,
                CharacterAiEditorTestDependencies.WorldRegistry,
                new RoomFacilityPolicyService(RoomRegistry.EditorCache),
                CharacterAiEditorTestDependencies.GameCalendar);
        }

        internal float Mana { get; private set; } = 20f;
        internal int Ammunition { get; private set; } = 3;
        internal int Revision { get; private set; } = 1;
        internal int RollbackCount { get; private set; }
        internal int CompleteCount { get; private set; }

        internal CombatOutcomeApplyResult Execute(bool injectAmmunitionFailure)
        {
            var result = new CombatAttackResult(
                executed: true,
                hit: false,
                coverBlocked: false,
                evaded: false,
                bodyPart: CombatBodyPart.Torso,
                rawDamage: 0f,
                appliedDamage: 0f,
                bleeding: 0f,
                suppression: 0f,
                armorDurabilityDamage: 0f,
                armorInstanceId: string.Empty,
                failureReason: string.Empty,
                ammunitionItemId: "item:test-arrow",
                ammunitionConsumed: 1);
            int revisionBefore = Revision;
            int attackRevision = revisionBefore;
            float manaBefore = Mana;
            int ammunitionBefore = Ammunition;
            int ammunitionConsumed = result.AmmunitionConsumed;
            var fact = new CombatCellShotFact(new CombatCellShotFactData
            {
                commandId = "combat-command:1",
                attackRevision = attackRevision,
                actorId = "character:cell-fire-attacker",
                actorName = attacker.Identity.DisplayName,
                absoluteDay = Mathf.Max(
                    1,
                    CharacterAiEditorTestDependencies.GameCalendar.Day),
                targetX = 4,
                targetY = 7,
                weaponDefinitionId = "weapon:test-cell-bow",
                weaponInstanceId = "item-instance:test-cell-bow",
                damageType = CombatDamageType.Pierce,
                fireMode = CombatFireMode.Aimed,
                requiresAmmunition = true,
                ammunitionItemId = result.AmmunitionItemId,
                ammunitionBefore = ammunitionBefore,
                ammunitionConsumed = ammunitionConsumed,
                ammunitionAfter = ammunitionBefore - ammunitionConsumed,
                manaBefore = manaBefore,
                manaCost = 5f,
                manaSpent = 5f,
                manaAfter = manaBefore - 5f
            });
            bool applied = CombatCellShotJournal.TryApply(
                pending,
                harness.Facts,
                fact,
                apply: () =>
                {
                    Mana -= 5f;
                    if (injectAmmunitionFailure)
                        return "injected-cell-fire-ammunition-failure";
                    Ammunition = Mathf.Max(
                        0,
                        Ammunition - ammunitionConsumed);
                    Revision++;
                    return string.Empty;
                },
                matchesApplied: () => Mana == manaBefore - 5f
                    && Ammunition == ammunitionBefore - ammunitionConsumed
                    && Revision == revisionBefore + 1,
                rollback: () =>
                {
                    Mana = manaBefore;
                    Ammunition = ammunitionBefore;
                    Revision = revisionBefore;
                    RollbackCount++;
                },
                out string failure);
            if (!applied)
                return CombatOutcomeApplyResult.Failed(failure);
            CompleteCount++;
            return CombatOutcomeApplyResult.Success();
        }

        internal CombatOutcomeApplyResult ExecuteVariant(
            string caseId,
            in CombatAttackResult result,
            bool injectAmmunitionFailure = false)
        {
            long attackRevision = Revision;
            string operationId = "combat:d01-evidence:"
                + CausalGameplaySyntax.RequireId(caseId, nameof(caseId))
                + ":command";
            var participant = new CombatParticipantRef(target);
            if (!applier.TryReserveDamageOutcome(
                    participant,
                    operationId,
                    attackRevision,
                    out ReservedCombatAction reserved,
                    out _,
                    out string reserveFailure))
            {
                return CombatOutcomeApplyResult.Failed(reserveFailure);
            }
            var weapon = new CombatWeaponSnapshot(
                "weapon:test-cell-bow",
                "weapon-instance:test-cell-bow",
                CombatEquipmentKind.RangedWeapon,
                new ProjectileVerb
                {
                    damageType = CombatDamageType.Pierce
                },
                Array.Empty<CombatRangeProfile>(),
                maximumRange: 10,
                quality: CombatEquipmentQuality.Normal,
                ammunitionItemId: "item:test-arrow",
                magazineCapacity: 6,
                loadedAmmo: Ammunition,
                reloadSeconds: 1f,
                supportsAimed: true,
                supportsRapid: false,
                supportsSuppressive: false);
            var request = new CombatAttackRequest(
                operationId,
                "character:cell-fire-attacker",
                "character:cell-fire-target",
                default,
                default,
                weapon,
                distance: 5,
                fireMode: CombatFireMode.Aimed,
                cover: default);
            float manaBefore = Mana;
            int ammunitionBefore = Ammunition;
            int ammunitionConsumed = result.AmmunitionConsumed;
            int revisionBefore = Revision;
            return applier.Apply(
                participant,
                result,
                attacker,
                attacker.Identity.DisplayName,
                CombatDamageType.Pierce,
                operationId,
                attackRevision,
                reserved,
                new CombatOutcomeMechanicalMutation(
                    apply: () =>
                    {
                        Mana -= 5f;
                        if (injectAmmunitionFailure)
                            return "injected-cell-fire-ammunition-failure";
                        Ammunition = Mathf.Max(
                            0,
                            Ammunition - ammunitionConsumed);
                        Revision++;
                        return string.Empty;
                    },
                    rollback: () =>
                    {
                        Mana = manaBefore;
                        Ammunition = ammunitionBefore;
                        Revision = revisionBefore;
                        RollbackCount++;
                    },
                    complete: () => CompleteCount++),
                CombatActionEvidenceData.Capture(
                    request,
                    result,
                    CombatDamageType.Pierce));
        }

        public void Dispose()
        {
            CharacterAiEditorTestDependencies.WorldRegistry
                .UnregisterCharacter(attacker);
            CharacterAiEditorTestDependencies.WorldRegistry
                .UnregisterCharacter(target);
            for (int index = cleanup.Count - 1; index >= 0; index--)
            {
                if (cleanup[index] != null)
                    UnityEngine.Object.DestroyImmediate(cleanup[index]);
            }
        }
    }

    private interface IEntryFixture : IDisposable
    {
        string CaptureDomainState();
        bool InvokeProductionEntry();
    }

    private sealed class OutcomeHarness : IDisposable
    {
        internal OutcomeHarness(
            MigratedProducerOutcomeKind kind,
            string phase,
            bool includeCombatDamage,
            MigratedProducerOutcomeFaultMode faultMode,
            bool deliveryFault,
            string stableRunId = null,
            bool includeSocialLife = false,
            bool includeAcquiredTraitReaction = false,
            bool includeResearchWork = false,
            bool includeAcquiredTraitInference = false,
            InvasionLifecycleStage? hideInvasionAck = null)
        {
            Fault = new MigratedProducerOutcomeDeliveryFault
            {
                TargetKind = kind,
                Enabled = deliveryFault
            };
            var migratedAdapter = new MigratedProducerOutcomeAdapter();
            var descriptorList = new List<IGameplayOutcomeDescriptor>();
            if (includeCombatDamage)
            {
                descriptorList.Add(
                    new CombatDamageOutcomeDescriptor());
            }
            if (includeSocialLife)
            {
                descriptorList.Add(
                    new SocialConflictOutcomeDescriptor(
                        new KoreanJosaFormatter()));
            }
            if (includeAcquiredTraitReaction)
                descriptorList.Add(new AcquiredTraitReactionOutcomeDescriptor());
            if (includeResearchWork)
                descriptorList.Add(new ResearchWorkOutcomeDescriptor());
            if (includeAcquiredTraitInference)
                descriptorList.Add(new AcquiredTraitInferenceOutcomeDescriptor());
            bool includeDefenseActivation = kind is
                MigratedProducerOutcomeKind.DefenseFacilityStateChangedEvent
                or MigratedProducerOutcomeKind.DefenseFacilityTriggeredEvent;
            if (includeDefenseActivation)
                descriptorList.Add(new DefenseFacilityEffectOutcomeDescriptor(
                    new KoreanJosaFormatter()));
            IGameplayOutcomeDescriptor[] descriptors =
                descriptorList.ToArray();
            var adapterList = new List<IGameplayOutcomeAdapterRegistration>();
            if (includeCombatDamage)
            {
                adapterList.Add(new CombatDamageOutcomeAdapter());
            }
            if (includeSocialLife)
            {
                adapterList.Add(new SocialConflictOutcomeAdapter());
            }
            if (includeAcquiredTraitReaction)
                adapterList.Add(new AcquiredTraitReactionOutcomeAdapter());
            if (includeResearchWork)
                adapterList.Add(new ResearchWorkOutcomeAdapter());
            if (includeAcquiredTraitInference)
                adapterList.Add(new AcquiredTraitInferenceOutcomeAdapter());
            if (includeDefenseActivation)
                adapterList.Add(new DefenseFacilityEffectOutcomeAdapter());
            adapterList.Add(migratedAdapter);
            IGameplayOutcomeAdapterRegistration[] adapters =
                adapterList.ToArray();
            Registry = new GameplayOutcomeRegistry(
                descriptors,
                adapters,
                new IGameplayOutcomeDescriptorCatalog[]
                {
                    new FaultingMigratedProducerOutcomeDescriptorCatalog(
                        new KoreanJosaFormatter(),
                        Fault)
                });
            var limits = new GameplayOutcomeBufferLimits(
                smallPageCount: 128,
                largePageCount: 8,
                knownResultKeyCapacity: 512,
                retainedSmallPageCount: 128,
                retainedLargePageCount: 8);
            RunId = string.IsNullOrWhiteSpace(stableRunId)
                ? "run:migrated-entry:"
                    + (int)kind + ":" + phase + ":" + Guid.NewGuid().ToString("N")
                : CausalGameplaySyntax.RequireId(stableRunId, nameof(stableRunId));
            Ledger = new GameplayOutcomeLedger(
                Registry,
                limits,
                new GameplayOutcomeRunId(RunId),
                1L);
            Recorder = new GameplayOutcomeRecorder(
                Ledger,
                Registry,
                new GameEventBus());
            TraitReactionOutcomes = includeAcquiredTraitReaction
                ? new EvolutionGameplayOutcomeBridge(Recorder, Ledger, Ledger)
                : null;
            AggregateRootStore = new DungeonRuntimeAggregateRootStore();
            Transaction = new MigratedProducerOutcomeRuntime(
                AggregateRootStore,
                Ledger,
                Recorder,
                Ledger);
            Probe = new MigratedProducerOutcomeProbeTransaction(
                Transaction,
                Recorder)
            {
                TargetKind = kind,
                FaultMode = faultMode
            };
            Facts = new DomainFactDispatcher();
            var commits = new GameplayDomainFactCommitGroup(
                Recorder,
                Ledger);
            Facts.Subscribe(
                "log:character.critical-health-transition",
                new MigratedProducerDomainFactProbeObserver<
                    CharacterCriticalHealthTransitionFact>(
                    CharacterHealingFactMapping.CaptureCritical,
                    Transaction,
                    commits,
                    Probe));
            Facts.Subscribe(
                "log:character.mobility-recovered",
                new MigratedProducerDomainFactProbeObserver<
                    CharacterMobilityRecoveredFact>(
                    CharacterHealingFactMapping.CaptureRecovered,
                    Transaction,
                    commits,
                    Probe));
            Facts.Subscribe(
                "log:character.fatality",
                new MigratedProducerDomainFactProbeObserver<
                    CharacterFatalityFact>(
                    CharacterFatalityFactMapping.CaptureDeath,
                    Transaction,
                    commits,
                    Probe));
            Facts.Subscribe(
                "log:character.fatal-critical-transition",
                new MigratedProducerDomainFactProbeObserver<
                    CharacterFatalCriticalTransitionFact>(
                    CharacterFatalityFactMapping.CaptureCritical,
                    Transaction,
                    commits,
                    Probe));
            Facts.Subscribe(
                "log:character.fatal-downed",
                new MigratedProducerDomainFactProbeObserver<
                    CharacterFatalDownedFact>(
                    CharacterFatalityFactMapping.CaptureDowned,
                    Transaction,
                    commits,
                    Probe));
            // The established probe wrapper retains the real observer and makes
            // reservation/commit evidence visible in every fixture mode.
            Facts.Subscribe(
                "log:combat.cell-shot",
                new MigratedProducerDomainFactProbeObserver<CombatCellShotFact>(
                    CombatCellShotFactMapping.Capture,
                    Transaction,
                    commits,
                    Probe));
            Facts.Subscribe(
                "log:combat.action",
                new CombatActionFactProbeObserver(
                    Ledger,
                    Recorder,
                    Transaction,
                    commits,
                    Probe));
            var invasionObserver = new InvasionLifecycleFactObserver(
                Transaction,
                commits,
                Ledger);
            Facts.Subscribe(
                InvasionLifecycleFactObservation.ObserverId,
                new MigratedProducerDomainFactProbeObserver<
                    InvasionLifecycleFact>(
                        fact => InvasionLifecycleFactObservation.Kind(
                            fact.Snapshot()),
                        invasionObserver,
                        invasionObserver,
                        commits,
                        Probe));
            CombatActions = new CombatActionPublicationRuntime(
                AggregateRootStore,
                Facts,
                Facts);
            InvasionFacts = new InvasionFactPublicationRuntime(
                hideInvasionAck.HasValue ? new B95AckLossPublisher(Facts, hideInvasionAck.Value) : Facts,
                Facts);
        }

        internal string RunId { get; }
        internal MigratedProducerOutcomeDeliveryFault Fault { get; }
        internal GameplayOutcomeRegistry Registry { get; }
        internal GameplayOutcomeLedger Ledger { get; }
        internal GameplayOutcomeRecorder Recorder { get; }
        internal EvolutionGameplayOutcomeBridge TraitReactionOutcomes { get; }
        internal DungeonRuntimeAggregateRootStore AggregateRootStore { get; }
        internal MigratedProducerOutcomeRuntime Transaction { get; }
        internal MigratedProducerOutcomeProbeTransaction Probe { get; }
        internal DomainFactDispatcher Facts { get; }
        internal CombatActionPublicationRuntime CombatActions { get; }
        internal InvasionFactPublicationRuntime InvasionFacts { get; }

        internal string CaptureOwnerState() =>
            JsonUtility.ToJson(Transaction.Capture());

        internal GameplayOutcomeReplayIdentity RequireExact(
            MigratedProducerOutcomeKind kind,
            GameplayOutcomeReplayState expectedState,
            string beforeState = null,
            string afterState = null)
        {
            Require(
                Probe.ReservationCount(kind) > 0
                && Probe.CommitCount(kind) > 0,
                "Production entry did not reserve and commit " + kind);
            GameplayResultKey key = Probe.LatestCommittedKey(kind);
            MigratedProducerOutcomeDefinition definition =
                MigratedProducerOutcomeCatalog.Get(kind);
            GameplayOutcomeReplayIdentity identity = default;
            GameplayOutcomeSnapshot exact = default;
            bool identityFound = key.IsValid
                && Ledger.TryGetResultIdentity(key, out identity);
            bool exactFound = identityFound
                && Ledger.TryGetExact(identity.OutcomeId, out exact);
            Require(
                identityFound
                && identity.State == expectedState
                && exactFound
                && string.Equals(
                    exact.outcomeTypeId,
                    definition.OutcomeTypeId.Value,
                    StringComparison.Ordinal),
                "Production entry did not leave the exact expected ledger row for "
                + kind
                + "; keyValid=" + key.IsValid
                + "; identityFound=" + identityFound
                + "; state=" + identity.State
                + "; expectedState=" + expectedState
                + "; exactFound=" + exactFound
                + "; actualType=" + (exact?.outcomeTypeId ?? string.Empty)
                + "; expectedType=" + definition.OutcomeTypeId.Value);
            MigratedProducerOutcomeEvidenceCapture.Record(
                kind,
                identity,
                exact);
            if (beforeState != null || afterState != null)
            {
                MigratedProducerProductionTransitionEvidenceCapture.Record(
                    kind,
                    identity,
                    exact,
                    beforeState,
                    afterState);
            }
            return identity;
        }

        internal void RequirePendingThenRetry(
            MigratedProducerOutcomeKind kind)
        {
            Require(
                Probe.ReservationCount(kind) > 0
                && Probe.CommitCount(kind) > 0,
                "Production entry did not durably commit pending " + kind);
            GameplayResultKey key = Probe.LatestCommittedKey(kind);
            GameplayOutcomeReplayIdentity pending = default;
            Require(
                key.IsValid
                && Ledger.TryGetResultIdentity(key, out pending)
                && pending.State
                    == GameplayOutcomeReplayState.DeliveryFaultPending,
                "Production entry did not retain the injected delivery fault for "
                + kind + "; state=" + pending.State);
            Fault.Enabled = false;
            bool replayedPending;
            string replayFailure = string.Empty;
            if (IsInvasionLifecycleKind(kind))
            {
                replayedPending = InvasionFacts.RecoverPending(
                    out replayFailure);
            }
            else
            {
                replayedPending = Recorder.RetryPendingDeliveries(32) > 0;
            }
            Require(
                replayedPending
                && Ledger.TryGetResultIdentity(
                    pending.ResultKey,
                    out GameplayOutcomeReplayIdentity replayed)
                && replayed.State
                    == GameplayOutcomeReplayState.PublishedAcknowledged,
                "Durably committed delivery fault did not acknowledge on retry for "
                + kind + "; failure=" + replayFailure);
            _ = RequireExact(
                kind,
                GameplayOutcomeReplayState.PublishedAcknowledged);
        }

        private static bool IsInvasionLifecycleKind(
            MigratedProducerOutcomeKind kind) => kind is
            MigratedProducerOutcomeKind.InvasionStartedEvent
            or MigratedProducerOutcomeKind.InvasionDungeonBreachedEvent
            or MigratedProducerOutcomeKind.InvasionFacilityDamagedEvent
            or MigratedProducerOutcomeKind.DefenseFrontCollapsedEvent
            or MigratedProducerOutcomeKind.InvasionResolvedEvent;

        public void Dispose()
        {
            Facts.Dispose();
        }
    }

    private sealed class HealthEntryFixture : IEntryFixture
    {
        private readonly MigratedProducerOutcomeKind kind;
        private readonly GameObject actorObject;
        private readonly CharacterSO actorData;
        private readonly CharacterBodyHealthRuntime health;
        private readonly CharacterActor actor;
        private readonly CharacterVitalsActivityObserver vitalsActivities;

        internal HealthEntryFixture(
            OutcomeHarness harness,
            MigratedProducerOutcomeKind kind,
            string stableCharacterId = null)
        {
            this.kind = kind;
            actorData = CharacterAiEditorTestDependencies.CreateCharacterFixtureData(
                CharacterType.NPC,
                "Migrated Health Entry",
                "human");
            actorObject = new GameObject("Migrated health entry fixture");
            actor = actorObject.AddComponent<CharacterActor>();
            CharacterAiEditorTestDependencies.Inject(actorObject);
            actor.EnsureRuntimeState();
            actor.data = actorData;
            actor.characterType = CharacterType.NPC;
            actor.Identity.SetPersistentId(
                stableCharacterId ?? "character:migrated-health:" + Guid.NewGuid().ToString("N"));
            var clock = new UnityGameClock();
            var events = new GameEventBus();
            HealingEvents = events;
            vitalsActivities = new CharacterVitalsActivityObserver(
                events,
                CharacterAiEditorTestDependencies.WorldRegistry);
            vitalsActivities.Initialize();
            health = new CharacterBodyHealthRuntime(
                CharacterAiEditorTestDependencies.WorldRegistry,
                clock,
                events,
                new DynamicFrameWorkBudget(clock, new UnityUiClock()),
                new ResourceAnatomyProfileCatalog(
                    new ResourceGameContentCatalog(
                        new UnityGameContentRootLoader())),
                harness.AggregateRootStore,
                CharacterAiEditorTestDependencies.GameCalendar);
            actor.Stats.ConstructCharacterVitals(
                new CharacterStatsVitalsService(
                    health,
                    health,
                    events,
                    new CharacterDeathEventFactory(
                        CharacterAiEditorTestDependencies.WorldRegistry,
                        CharacterAiEditorTestDependencies.GameCalendar),
                    new NoopOwnerRunLifecycleService()));
            health.ConfigureVitals(actor, 100f, resetCurrentHealth: true);
            if (kind == MigratedProducerOutcomeKind.CharacterBodyHealthRecoveredEvent)
                PrepareDownedRecoveryState();
            health.ConfigureFactPublication(harness.Facts, harness.Facts);
        }

        internal CharacterBodyHealthRuntime HealingHealth => health;
        internal CharacterActor HealingActor => actor;
        internal GameEventBus HealingEvents { get; }

        public string CaptureDomainState() => JsonUtility.ToJson(health.Capture());

        public bool InvokeProductionEntry()
        {
            if (kind == MigratedProducerOutcomeKind.CharacterBodyHealthRecoveredEvent)
            {
                health.Heal(actor, 40f, stopBleeding: true);
                return !health.GetSnapshot(actor).Downed
                    && health.GetVitals(actor).CurrentHealth > 20f;
            }

            health.Kill(
                actor,
                CharacterDeathCauseCode.Combat,
                "combat:migrated-entry-e2e");
            return health.GetVitals(actor).CurrentHealth <= 0f;
        }

        public void Dispose()
        {
            vitalsActivities.Dispose();
            Object.DestroyImmediate(actorObject);
            Object.DestroyImmediate(actorData);
        }

        private void PrepareDownedRecoveryState()
        {
            health.RestoreLegacyVitalsProjection(actor, 100f, 10f, 0.9f);
            CharacterBodyHealthSnapshot source = health.GetSnapshot(actor);
            CharacterBodyPartHealthState[] parts = source.Parts
                .Select(part => new CharacterBodyPartHealthState
                {
                    bodyPart = part.bodyPart,
                    maxHealth = part.maxHealth,
                    currentHealth = part.bodyPart is CombatBodyPart.Head
                        or CombatBodyPart.Torso
                            ? part.maxHealth * 0.2f
                            : part.maxHealth,
                    bleedingPerSecond = 0f
                })
                .ToArray();
            health.ApplySnapshot(
                actor,
                new CharacterBodyHealthSnapshot(
                    parts,
                    bloodLoss: 0f,
                    suppression: 0f,
                    consciousness: 0.2f,
                    manipulation: 1f,
                    mobility: 1f,
                    downed: true),
                "migrated-entry-recovery-setup");
            Require(
                health.GetSnapshot(actor).Downed,
                "Recovery fixture did not start from authoritative downed state.");
        }
    }

    private sealed class CombatEntryFixture : IEntryFixture
    {
        private readonly MigratedProducerOutcomeKind kind;
        private readonly OutcomeHarness harness;
        private readonly List<Object> cleanup = new();
        private readonly CharacterBodyHealthRuntime health;
        private readonly CharacterActor attacker;
        private readonly CharacterActor victim;
        private readonly CombatCommandResultApplier applier;
        private readonly string operationId;
        private readonly GameEventBus events;
        private readonly CharacterVitalsActivityObserver vitalsActivities;

        internal CombatEntryFixture(
            OutcomeHarness harness,
            MigratedProducerOutcomeKind kind,
            string stableScope = null)
        {
            this.harness = harness;
            this.kind = kind;
            var clock = new UnityGameClock();
            events = new GameEventBus();
            vitalsActivities = new CharacterVitalsActivityObserver(
                events,
                CharacterAiEditorTestDependencies.WorldRegistry);
            vitalsActivities.Initialize();
            health = new CharacterBodyHealthRuntime(
                CharacterAiEditorTestDependencies.WorldRegistry,
                clock,
                events,
                new DynamicFrameWorkBudget(clock, new UnityUiClock()),
                new ResourceAnatomyProfileCatalog(
                    new ResourceGameContentCatalog(
                        new UnityGameContentRootLoader())),
                new DungeonRuntimeAggregateRootStore(),
                CharacterAiEditorTestDependencies.GameCalendar);
            attacker = CreateActor(
                "Migrated Combat Attacker",
                "character:migrated-combat-attacker:",
                CharacterType.NPC,
                stableScope);
            victim = CreateActor(
                "Migrated Combat Victim",
                "character:migrated-combat-victim:",
                CharacterType.NPC,
                stableScope);
            CharacterAiEditorTestDependencies.WorldRegistry.RegisterCharacter(
                attacker);
            CharacterAiEditorTestDependencies.WorldRegistry.RegisterCharacter(
                victim);
            applier = new CombatCommandResultApplier(
                CharacterAiEditorTestDependencies.CombatEquipment,
                health,
                health,
                health,
                harness.CombatActions,
                new NoCoverDurabilityRegistry(),
                clock,
                new NoWorldUiHierarchy(),
                events,
                CharacterAiEditorTestDependencies.WorldRegistry,
                CharacterAiEditorTestDependencies.WorldRegistry,
                new RoomFacilityPolicyService(new RoomLayoutCache()),
                CharacterAiEditorTestDependencies.GameCalendar);
            operationId = string.IsNullOrWhiteSpace(stableScope)
                ? "combat:migrated-entry:"
                    + (int)kind + ":" + Guid.NewGuid().ToString("N")
                : "combat:causal-plan:" + stableScope;
        }

        public string CaptureDomainState() => JsonUtility.ToJson(health.Capture());

        public bool InvokeProductionEntry()
        {
            var target = new CombatParticipantRef(victim);
            if (!applier.TryReserveDamageOutcome(
                    target,
                    operationId,
                    1L,
                    out ReservedCombatAction reserved,
                    out _,
                    out _))
            {
                return false;
            }
            bool downed = kind
                == MigratedProducerOutcomeKind.CharacterBodyHealthDownedEvent;
            float damage = kind == MigratedProducerOutcomeKind.CharacterKilledEvent
                || downed
                    ? 100f
                    : 5f;
            var weapon = new CombatWeaponSnapshot(
                "weapon:test-bow",
                "weapon-instance:test-bow",
                CombatEquipmentKind.RangedWeapon,
                new ProjectileVerb
                {
                    damageType = CombatDamageType.Pierce
                },
                Array.Empty<CombatRangeProfile>(),
                maximumRange: 8,
                quality: CombatEquipmentQuality.Normal,
                ammunitionItemId: "item:test-arrow",
                magazineCapacity: 12,
                loadedAmmo: 7,
                reloadSeconds: 1f,
                supportsAimed: true,
                supportsRapid: true,
                supportsSuppressive: false);
            var attackRequest = new CombatAttackRequest(
                operationId,
                "character:test-attacker",
                "character:test-victim",
                default,
                default,
                weapon,
                distance: 3,
                fireMode: CombatFireMode.Rapid,
                cover: default);
            var result = new CombatAttackResult(
                executed: true,
                hit: true,
                coverBlocked: false,
                evaded: false,
                bodyPart: CombatBodyPart.Torso,
                rawDamage: damage,
                appliedDamage: damage,
                bleeding: 0f,
                suppression: 3f,
                armorDurabilityDamage: 0f,
                armorInstanceId: string.Empty,
                failureReason: string.Empty,
                armorDurabilityHits: new[]
                {
                    new CombatArmorDurabilityHit(
                        "armor-instance:test",
                        2.5f)
                },
                ammunitionItemId: "item:test-arrow",
                specialEffects: CombatSpecialEffectFlags.Burning,
                statusPotency: 0.75f,
                statusTurns: 3,
                nonlethal: downed,
                ammunitionConsumed: 1);
            CombatOutcomeApplyResult applied = applier.Apply(
                target,
                result,
                attacker,
                attacker.Identity.DisplayName,
                CombatDamageType.Pierce,
                operationId,
                1L,
                reserved,
                new CombatOutcomeMechanicalMutation(
                    apply: () => string.Empty,
                    rollback: () => { },
                    complete: () => { }),
                CombatActionEvidenceData.Capture(
                    attackRequest,
                    result,
                    CombatDamageType.Pierce));
            return applied.Succeeded;
        }

        public void Dispose()
        {
            vitalsActivities.Dispose();
            CharacterAiEditorTestDependencies.WorldRegistry.UnregisterCharacter(
                attacker);
            CharacterAiEditorTestDependencies.WorldRegistry.UnregisterCharacter(
                victim);
            for (int index = cleanup.Count - 1; index >= 0; index--)
            {
                if (cleanup[index] != null)
                    Object.DestroyImmediate(cleanup[index]);
            }
        }

        private CharacterActor CreateActor(
            string displayName,
            string idPrefix,
            CharacterType characterType,
            string stableScope)
        {
            CharacterSO data = CharacterAiEditorTestDependencies
                .CreateCharacterFixtureData(characterType, displayName, "human");
            cleanup.Add(data);
            var actorObject = new GameObject(displayName);
            cleanup.Add(actorObject);
            CharacterActor result = actorObject.AddComponent<CharacterActor>();
            CharacterAiEditorTestDependencies.Inject(actorObject);
            result.EnsureRuntimeState();
            result.data = data;
            result.characterType = characterType;
            result.Identity.SetPersistentId(idPrefix
                + (string.IsNullOrWhiteSpace(stableScope)
                    ? Guid.NewGuid().ToString("N")
                    : stableScope));
            result.Stats.ConstructCharacterVitals(
                new CharacterStatsVitalsService(
                    health,
                    health,
                    events,
                    new CharacterDeathEventFactory(
                        CharacterAiEditorTestDependencies.WorldRegistry,
                        CharacterAiEditorTestDependencies.GameCalendar),
                    new NoopOwnerRunLifecycleService()));
            health.ConfigureVitals(result, 100f, resetCurrentHealth: true);
            return result;
        }
    }

    private sealed class CombatInjuryRecoveryFixture : IDisposable
    {
        private readonly OutcomeHarness harness;
        private readonly List<Object> cleanup = new();
        private readonly CharacterBodyHealthRuntime health;
        private readonly CharacterActor attacker;
        private readonly CharacterActor protagonist;
        private readonly CombatCommandResultApplier applier;
        private readonly string operationId;
        private readonly GameEventBus events;
        private readonly CharacterVitalsActivityObserver vitalsActivities;
        private bool injuryApplied;
        private bool recoveryApplied;

        internal CombatInjuryRecoveryFixture(
            OutcomeHarness harness,
            string stableScope,
            string protagonistId)
        {
            this.harness = harness ?? throw new ArgumentNullException(nameof(harness));
            ProtagonistId = CausalGameplaySyntax.RequireId(
                protagonistId,
                nameof(protagonistId));
            var clock = new UnityGameClock();
            events = new GameEventBus();
            vitalsActivities = new CharacterVitalsActivityObserver(
                events,
                CharacterAiEditorTestDependencies.WorldRegistry);
            vitalsActivities.Initialize();
            health = new CharacterBodyHealthRuntime(
                CharacterAiEditorTestDependencies.WorldRegistry,
                clock,
                events,
                new DynamicFrameWorkBudget(clock, new UnityUiClock()),
                new ResourceAnatomyProfileCatalog(
                    new ResourceGameContentCatalog(
                        new UnityGameContentRootLoader())),
                harness.AggregateRootStore,
                CharacterAiEditorTestDependencies.GameCalendar);
            attacker = CreateActor(
                "Causal Scenario Enemy",
                "character:causal-enemy:" + stableScope);
            protagonist = CreateActor("연화", ProtagonistId);
            CharacterAiEditorTestDependencies.WorldRegistry.RegisterCharacter(
                attacker);
            CharacterAiEditorTestDependencies.WorldRegistry.RegisterCharacter(
                protagonist);
            health.ConfigureFactPublication(harness.Facts, harness.Facts);
            applier = new CombatCommandResultApplier(
                CharacterAiEditorTestDependencies.CombatEquipment,
                health,
                health,
                health,
                harness.CombatActions,
                new NoCoverDurabilityRegistry(),
                clock,
                new NoWorldUiHierarchy(),
                events,
                CharacterAiEditorTestDependencies.WorldRegistry,
                CharacterAiEditorTestDependencies.WorldRegistry,
                new RoomFacilityPolicyService(new RoomLayoutCache()),
                CharacterAiEditorTestDependencies.GameCalendar);
            operationId = "combat:causal-injury:" + stableScope;
        }

        internal string ProtagonistId { get; }
        internal float CurrentHealth => health.GetVitals(protagonist).CurrentHealth;
        internal bool Downed => health.GetSnapshot(protagonist).Downed;
        internal string CaptureDomainState() => JsonUtility.ToJson(health.Capture());

        internal bool Injure()
        {
            if (injuryApplied || recoveryApplied)
                return false;
            var target = new CombatParticipantRef(protagonist);
            if (!applier.TryReserveDamageOutcome(
                    target,
                    operationId,
                    1L,
                    out ReservedCombatAction reserved,
                    out _,
                    out _))
            {
                return false;
            }
            var result = new CombatAttackResult(
                executed: true,
                hit: true,
                coverBlocked: false,
                evaded: false,
                bodyPart: CombatBodyPart.Torso,
                rawDamage: 100f,
                appliedDamage: 100f,
                bleeding: 0f,
                suppression: 3f,
                armorDurabilityDamage: 0f,
                armorInstanceId: string.Empty,
                failureReason: string.Empty,
                nonlethal: true);
            CombatOutcomeApplyResult applied = applier.Apply(
                target,
                result,
                attacker,
                attacker.Identity.DisplayName,
                CombatDamageType.Slash,
                operationId,
                1L,
                reserved,
                new CombatOutcomeMechanicalMutation(
                    apply: () => string.Empty,
                    rollback: () => { },
                    complete: () => { }));
            injuryApplied = applied.Succeeded && Downed;
            return injuryApplied;
        }

        internal bool Recover()
        {
            if (!injuryApplied || recoveryApplied || !Downed)
                return false;
            health.Heal(protagonist, 40f, stopBleeding: true);
            recoveryApplied = !Downed && CurrentHealth > 0f;
            return recoveryApplied;
        }

        public void Dispose()
        {
            vitalsActivities.Dispose();
            CharacterAiEditorTestDependencies.WorldRegistry.UnregisterCharacter(
                attacker);
            CharacterAiEditorTestDependencies.WorldRegistry.UnregisterCharacter(
                protagonist);
            for (int index = cleanup.Count - 1; index >= 0; index--)
            {
                if (cleanup[index] != null)
                    Object.DestroyImmediate(cleanup[index]);
            }
        }

        private CharacterActor CreateActor(string displayName, string id)
        {
            CharacterSO data = CharacterAiEditorTestDependencies
                .CreateCharacterFixtureData(
                    CharacterType.NPC,
                    displayName,
                    "human");
            cleanup.Add(data);
            var actorObject = new GameObject(displayName);
            cleanup.Add(actorObject);
            CharacterActor result = actorObject.AddComponent<CharacterActor>();
            CharacterAiEditorTestDependencies.Inject(actorObject);
            result.EnsureRuntimeState();
            result.data = data;
            result.characterType = CharacterType.NPC;
            result.Identity.SetPersistentId(id);
            result.Stats.ConstructCharacterVitals(
                new CharacterStatsVitalsService(
                    health,
                    health,
                    events,
                    new CharacterDeathEventFactory(
                        CharacterAiEditorTestDependencies.WorldRegistry,
                        CharacterAiEditorTestDependencies.GameCalendar),
                    new NoopOwnerRunLifecycleService()));
            health.ConfigureVitals(result, 100f, resetCurrentHealth: true);
            return result;
        }
    }

    private sealed class DefenseFacilityEntryFixture : IEntryFixture
    {
        private readonly BuildingFixture building;
        private readonly ActorFixture target;
        private readonly DefenseFacility facility;
        private readonly DefenseFacilityRuntime runtime;
        private readonly DomainFactDispatcher facts;
        private readonly IDisposable activationBinding;
        private readonly IDisposable effectBinding;
        private readonly DefenseActivationPublicationRuntime publications;
        private readonly string facilityOwnerId;
        private readonly IDefenseStatusRuntimeService statuses =
            new DefenseStatusRuntimeService(new DefenseStatusRuntimeFactory());

        internal DefenseFacilityEntryFixture(
            OutcomeHarness harness,
            string facilityScope = null,
            string targetScope = null)
        {
            var grid = new Grid(5, 1);
            facilityOwnerId = !string.IsNullOrWhiteSpace(facilityScope)
                    && facilityScope.StartsWith(
                        "building:",
                        StringComparison.Ordinal)
                ? facilityScope
                : "building:migrated-defense:"
                    + (string.IsNullOrWhiteSpace(facilityScope)
                        ? Guid.NewGuid().ToString("N")
                        : facilityScope);
            building = new BuildingFixture(
                grid,
                "Assets/Resources/SO/Building/P1/P1_SpikeTrap.asset",
                new Vector2Int(2, 0),
                facilityOwnerId);
            facility = building.Building as DefenseFacility
                ?? throw new InvalidOperationException(
                    "Defense fixture asset did not create DefenseFacility.");
            target = new ActorFixture(
                CharacterType.Intruder,
                "Migrated Defense Target",
                targetScope);
            var events = new GameEventBus();
            runtime = new DefenseFacilityRuntime(
                UnavailableEquipmentPhysicalItemGateway.Instance,
                new NoopDefensePhysicalGateway(),
                new AlwaysReadyDefenseInputOwner(),
                new UnityGameClock(),
                events,
                new AlwaysPoweredQuery(),
                new NoopDefenseFacilityNetwork(),
                new EmptyFacilityCapabilities(),
                new DungeonRuntimeAggregateRootStore());
            facts = new DomainFactDispatcher();
            var commits = new GameplayDomainFactCommitGroup(
                harness.Recorder,
                harness.Ledger);
            activationBinding = facts.Subscribe(
                DefenseActivationFactObserver.ObserverId,
                new DefenseActivationFactProbeObserver(
                    harness.Ledger,
                    harness.Transaction,
                    commits,
                    harness.Probe));
            effectBinding = DefenseEffectFactMapping.CreateBinding(
                    harness.Registry,
                    harness.Ledger,
                    harness.Recorder,
                    commits)
                .Connect(facts);
            publications = new DefenseActivationPublicationRuntime(
                facts,
                facts,
                facts);
            facility.ConstructDefenseFacilityEventBus(
                events,
                null,
                runtime);
            facility.ConstructDefenseActivation(
                publications,
                CharacterAiEditorTestDependencies.GameCalendar,
                target.Health);
            // A live defense facility is registered with its runtime before a
            // trigger transaction starts. Materialize that authoritative
            // baseline here so rejection compares only activation mutations,
            // not lazy owner registration performed by the first query.
            _ = runtime.GetSnapshot(facility);
        }

        internal JObject CaptureOwnerCheckpoint() => new()
        {
            ["defenseActivationPublications"] =
                CapturePublicationCheckpoint(),
            ["defenseFacility"] = JObject.Parse(
                JsonUtility.ToJson(runtime.CaptureState()))
        };

        internal void RestoreOwnerCheckpoint(JObject domainState)
        {
            if (domainState == null
                || domainState.Properties().Select(value => value.Name)
                    .OrderBy(value => value, StringComparer.Ordinal)
                    .SequenceEqual(
                        new[]
                        {
                            "defenseActivationPublications",
                            "defenseFacility"
                        },
                        StringComparer.Ordinal) == false
                || domainState["defenseFacility"] is not JObject payload
                || domainState["defenseActivationPublications"]
                    is not JObject publicationPayload)
            {
                throw new InvalidOperationException(
                    "Defense facility owner checkpoint shape is invalid.");
            }
            DefenseFacilitySaveData save = JsonUtility.FromJson<
                DefenseFacilitySaveData>(payload.ToString(Formatting.None));
            string publicationJson =
                publicationPayload.ToString(Formatting.None);
            DefenseActivationPublicationSaveSection.ValidateJson(
                publicationJson);
            DefenseActivationPublicationSaveData publicationSave =
                DefenseActivationPublicationRuntime.ValidateAndClone(
                    DungeonSaveJson.Deserialize<
                        DefenseActivationPublicationSaveData>(
                        publicationJson));
            DefenseFacilityRestoreCandidate candidate =
                runtime.PrepareRestoreState(save);
            runtime.PublishRestoreState(candidate);
            publications.Restore(publicationSave);
            DefenseFacilitySaveData restored = runtime.CaptureState();
            if (restored.facilities.Count != 1
                || !string.Equals(
                    restored.facilities[0].facilityPersistentId,
                    facilityOwnerId,
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "Defense facility checkpoint restored a different owner.");
            }
        }

        public string CaptureDomainState() =>
            JsonUtility.ToJson(runtime.CaptureState())
            + "|activationPublications="
            + CapturePublicationJson()
            + "|target=" + target.CaptureHealthState();

        private JObject CapturePublicationCheckpoint() =>
            JObject.Parse(CapturePublicationJson());

        private string CapturePublicationJson()
        {
            string json = DungeonCharacterWorldSaveCodec.SerializeFields(
                publications.Capture());
            DefenseActivationPublicationSaveSection.ValidateJson(json);
            return json;
        }

        public bool InvokeProductionEntry() => facility.Trigger(
            target.Actor,
            DefenseTriggerTiming.OnEnter,
            statuses) != null;

        public void Dispose()
        {
            effectBinding.Dispose();
            activationBinding.Dispose();
            facts.Dispose();
            target.Dispose();
            building.Dispose();
        }
    }

    /// <summary>
    /// Keeps the existing E2E reservation/commit fault probe outside the
    /// production activation observer while exercising that observer's real
    /// atomic trigger/state pair.
    /// </summary>
    private sealed class DefenseActivationFactProbeObserver :
        IDurableDomainFactObserver<DefenseActivationFact>,
        IDurableDomainFactRecoveryObserver<DefenseActivationFact>
    {
        private static readonly MigratedProducerOutcomeKind[] Kinds =
        {
            MigratedProducerOutcomeKind.DefenseFacilityTriggeredEvent,
            MigratedProducerOutcomeKind.DefenseFacilityStateChangedEvent
        };

        private readonly DefenseActivationFactObserver inner;
        private readonly MigratedProducerOutcomeProbeTransaction probe;
        private readonly ProbeCommitGroup commits;

        internal DefenseActivationFactProbeObserver(
            GameplayOutcomeLedger ledger,
            MigratedProducerOutcomeRuntime migrated,
            GameplayDomainFactCommitGroup nativeCommits,
            MigratedProducerOutcomeProbeTransaction probe)
        {
            inner = new DefenseActivationFactObserver(
                migrated ?? throw new ArgumentNullException(nameof(migrated)),
                nativeCommits
                    ?? throw new ArgumentNullException(nameof(nativeCommits)));
            this.probe = probe
                ?? throw new ArgumentNullException(nameof(probe));
            commits = new ProbeCommitGroup(
                ledger ?? throw new ArgumentNullException(nameof(ledger)),
                this.probe);
        }

        public bool TryReserve(
            in DefenseActivationFact proposed,
            out object reservation,
            out string failure)
        {
            reservation = null;
            for (int index = 0; index < Kinds.Length; index++)
            {
                if (!probe.TryObserveDomainFactReservation(
                        Kinds[index],
                        out failure))
                {
                    return false;
                }
            }

            if (!inner.TryReserve(
                    proposed,
                    out object innerReservation,
                    out failure))
            {
                return false;
            }
            if (innerReservation is not IDomainFactReservedIdentity identity)
            {
                inner.Cancel(innerReservation);
                failure =
                    "defense-activation-probe-reserved-identity-missing";
                return false;
            }

            reservation = new ProbeReservation(innerReservation, identity);
            return true;
        }

        public bool TryPrepare(
            in DefenseActivationFact actual,
            object reservation,
            out IDomainFactPreparedPublication prepared,
            out string failure)
        {
            prepared = null;
            if (reservation is not ProbeReservation state)
            {
                failure = "defense-activation-probe-reservation-invalid";
                return false;
            }
            if (!inner.TryPrepare(
                    actual,
                    state.Inner,
                    out IDomainFactPreparedPublication innerPrepared,
                    out failure))
            {
                return false;
            }
            return TryWrap(actual, innerPrepared, out prepared, out failure);
        }

        public bool TryPrepareReplay(
            string publicationKey,
            in DefenseActivationFact actual,
            out IDomainFactPreparedPublication prepared,
            out string failure)
        {
            prepared = null;
            if (!inner.TryPrepareReplay(
                    publicationKey,
                    actual,
                    out IDomainFactPreparedPublication innerPrepared,
                    out failure))
            {
                return false;
            }
            return TryWrap(actual, innerPrepared, out prepared, out failure);
        }

        public DomainFactDelivery Replay(in DefenseActivationFact fact) =>
            inner.Replay(fact);

        public void Cancel(object reservation)
        {
            if (reservation is ProbeReservation state)
                inner.Cancel(state.Inner);
        }

        private bool TryWrap(
            DefenseActivationFact fact,
            IDomainFactPreparedPublication innerPrepared,
            out IDomainFactPreparedPublication prepared,
            out string failure)
        {
            prepared = null;
            if (fact == null
                || innerPrepared is not IDomainFactPreparedIdentity identity)
            {
                innerPrepared?.Cancel();
                failure =
                    "defense-activation-probe-prepared-identity-missing";
                return false;
            }

            prepared = new ProbePublication(
                commits,
                innerPrepared,
                SelectObservations(fact),
                identity);
            failure = string.Empty;
            return true;
        }

        private static Observation[] SelectObservations(
            DefenseActivationFact fact)
        {
            DefenseActivationFactData data = fact.Snapshot();
            return new[]
            {
                new Observation(
                    MigratedProducerOutcomeKind.DefenseFacilityTriggeredEvent,
                    MigratedProducerOutcomeCatalog.ResultKey(
                        MigratedProducerOutcomeKind
                            .DefenseFacilityTriggeredEvent,
                        data.facilityId,
                        data.activationCount)),
                new Observation(
                    MigratedProducerOutcomeKind
                        .DefenseFacilityStateChangedEvent,
                    MigratedProducerOutcomeCatalog.ResultKey(
                        MigratedProducerOutcomeKind
                            .DefenseFacilityStateChangedEvent,
                        data.facilityId,
                        data.activationCount))
            };
        }

        private sealed class ProbeReservation : IDomainFactReservedIdentity
        {
            internal ProbeReservation(
                object inner,
                IDomainFactReservedIdentity identity)
            {
                Inner = inner;
                Identity = identity;
            }

            internal object Inner { get; }
            private IDomainFactReservedIdentity Identity { get; }
            public string PublicationKey => Identity.PublicationKey;
        }

        private readonly struct Observation
        {
            internal Observation(
                MigratedProducerOutcomeKind kind,
                GameplayResultKey key)
            {
                Kind = kind;
                Key = key;
            }

            internal MigratedProducerOutcomeKind Kind { get; }
            internal GameplayResultKey Key { get; }
        }

        private sealed class ProbePublication :
            IDomainFactPreparedPublication,
            IDomainFactPreparedIdentity
        {
            internal ProbePublication(
                ProbeCommitGroup commits,
                IDomainFactPreparedPublication inner,
                Observation[] observations,
                IDomainFactPreparedIdentity identity)
            {
                CommitGroup = commits;
                Inner = inner
                    ?? throw new ArgumentNullException(nameof(inner));
                Observations = observations
                    ?? throw new ArgumentNullException(nameof(observations));
                Identity = identity
                    ?? throw new ArgumentNullException(nameof(identity));
            }

            public IDomainFactCommitGroup CommitGroup { get; }
            internal IDomainFactPreparedPublication Inner { get; }
            internal Observation[] Observations { get; }
            private IDomainFactPreparedIdentity Identity { get; }
            public string OperationKey => Identity.OperationKey;
            public long OwnerRevision => Identity.OwnerRevision;
            public bool IsReplay => Identity.IsReplay;
            public void Cancel() => Inner.Cancel();
        }

        private sealed class ProbeCommitGroup : IDomainFactCommitGroup
        {
            private readonly GameplayOutcomeLedger ledger;
            private readonly MigratedProducerOutcomeProbeTransaction probe;

            internal ProbeCommitGroup(
                GameplayOutcomeLedger ledger,
                MigratedProducerOutcomeProbeTransaction probe)
            {
                this.ledger = ledger;
                this.probe = probe;
            }

            public bool Commit(
                IReadOnlyList<IDomainFactPreparedPublication> publications,
                DomainFactDelivery[] results,
                out string failure)
            {
                if (publications == null
                    || results == null
                    || publications.Count == 0
                    || publications.Count != results.Length)
                {
                    throw new ArgumentException(
                        "Invalid defense-activation probe publication cohort.");
                }

                var values = new ProbePublication[publications.Count];
                IDomainFactCommitGroup innerGroup = null;
                bool reject = false;
                for (int index = 0; index < publications.Count; index++)
                {
                    if (publications[index] is not ProbePublication value
                        || !ReferenceEquals(value.CommitGroup, this))
                    {
                        throw new ArgumentException(
                            "Mismatched defense-activation probe commit group.");
                    }
                    values[index] = value;
                    innerGroup ??= value.Inner.CommitGroup;
                    if (!ReferenceEquals(innerGroup, value.Inner.CommitGroup))
                    {
                        throw new ArgumentException(
                            "Defense-activation inner commit groups differ.");
                    }
                    if (value.IsReplay)
                        continue;
                    for (int item = 0;
                         item < value.Observations.Length;
                         item++)
                    {
                        reject |= probe.ObserveDomainFactCommitAttempt(
                            value.Observations[item].Kind);
                    }
                }

                if (reject)
                {
                    failure =
                        "migrated-producer-e2e-injected-batch-commit-rejection";
                    for (int index = 0; index < values.Length; index++)
                    {
                        values[index].Cancel();
                        results[index] = DomainFactDelivery.Reject(failure);
                    }
                    return false;
                }

                var innerPublications = values
                    .Select(value => value.Inner)
                    .ToArray();
                bool committed = innerGroup.Commit(
                    innerPublications,
                    results,
                    out failure);
                if (!committed)
                    return false;

                for (int index = 0; index < values.Length; index++)
                {
                    if (values[index].IsReplay)
                        continue;
                    for (int item = 0;
                         item < values[index].Observations.Length;
                         item++)
                    {
                        Observation observation =
                            values[index].Observations[item];
                        if (ledger.TryGetResultIdentity(
                                observation.Key,
                                out _))
                        {
                            probe.ObserveDomainFactCommitted(
                                observation.Kind,
                                observation.Key);
                        }
                    }
                }
                return true;
            }
        }
    }

    private sealed class InvasionIntruderEntryFixture : IEntryFixture
    {
        private readonly MigratedProducerOutcomeKind kind;
        private readonly GameObject intruderObject;
        private readonly InvasionIntruderRuntime runtime;
        private readonly Grid grid;
        private readonly GameEventBus events;
        private readonly BuildingFixture damageTarget;
        private readonly DefenseEngagement engagement;
        private readonly DefenseEngagementStore engagementStore;
        private readonly DefenseEngagementCombatRuntime engagementCombat;

        internal InvasionIntruderEntryFixture(
            OutcomeHarness harness,
            MigratedProducerOutcomeKind kind)
        {
            this.kind = kind;
            grid = new Grid(6, 1);
            grid.SetAreaType(new Vector2Int(1, 0), GridCellAreaType.DungeonInterior);
            events = new GameEventBus();
            intruderObject = CreatePreparedIntruderObject(
                harness.InvasionFacts,
                grid,
                events,
                out runtime);
            if (kind == MigratedProducerOutcomeKind.InvasionFacilityDamagedEvent)
            {
                runtime.transform.position = grid.GetWorldPos(new Vector2Int(1, 0));
                damageTarget = new BuildingFixture(
                    grid,
                    "Assets/Resources/SO/Building/P1/P1_SpikeTrap.asset",
                    new Vector2Int(2, 0),
                    "building:migrated-invasion-damage:"
                    + Guid.NewGuid().ToString("N"));
            }
            if (kind == MigratedProducerOutcomeKind.DefenseFrontCollapsedEvent)
            {
                runtime.gameObject.SetActive(false);
                engagementStore = new DefenseEngagementStore();
                engagement = new DefenseEngagement
                {
                    Id = engagementStore.AllocateId(),
                    Intruder = runtime,
                    State = DefenseEngagementState.Engaged,
                    StatusText = "engaged"
                };
                engagementStore.Add(engagement);
                engagementCombat = new DefenseEngagementCombatRuntime(
                    engagementStore,
                    events,
                    new UnityGameClock());
            }
        }

        public string CaptureDomainState()
        {
            string buildingState = damageTarget == null
                ? string.Empty
                : ";damaged=" + damageTarget.Building.IsDamaged;
            string engagementState = engagement == null
                ? string.Empty
                : ";engagement=" + engagement.State
                    + ";stored=" + engagementStore.Engagements.Count;
            return "state=" + runtime.State
                + ";breached=" + runtime.HasBreachedDungeonInterior
                + ";damageCount=" + runtime.FacilityDamageCount
                + buildingState
                + engagementState;
        }

        public bool InvokeProductionEntry()
        {
            switch (kind)
            {
                case MigratedProducerOutcomeKind.DefenseFrontCollapsedEvent:
                    engagementCombat.CollapseFront(
                        engagement,
                        "migrated-entry-front-collapse");
                    return engagement.State == DefenseEngagementState.Completed
                        && engagementStore.Engagements.Count == 0;
                case MigratedProducerOutcomeKind.InvasionDungeonBreachedEvent:
                    new InvasionIntruderExecutionCoordinator(runtime)
                        .MarkDungeonBreached(grid, new Vector2Int(1, 0));
                    return runtime.HasBreachedDungeonInterior;
                case MigratedProducerOutcomeKind.InvasionFacilityDamagedEvent:
                    return runtime.TryDamageNearbyFacility(
                        grid,
                        damageTarget.Building);
                case MigratedProducerOutcomeKind.InvasionResolvedEvent:
                    return runtime.TryCommitResolution(
                        true,
                        1f,
                        InvasionIntruderState.Finished,
                        InvasionResolutionKind.DefenderSurvived);
                default:
                    throw new ArgumentOutOfRangeException(nameof(kind), kind, null);
            }
        }

        public void Dispose()
        {
            damageTarget?.Dispose();
            if (intruderObject != null)
                Object.DestroyImmediate(intruderObject);
        }
    }

    private sealed class InvasionDirectorEntryFixture : IEntryFixture
    {
        private readonly GameObject directorObject;
        private readonly InvasionDirectorRuntime director;
        private readonly FixtureIntruderFactory factory;
        private readonly InvasionThreatSnapshot threat;

        internal InvasionDirectorEntryFixture(OutcomeHarness harness)
        {
            CharacterSO data = AssetDatabase.LoadAssetAtPath<CharacterSO>(
                "Assets/Resources/SO/Character/Intruders/Intruder_Breakthrough.asset")
                ?? throw new InvalidOperationException(
                    "Authored invasion intruder fixture is missing.");
            EnemyArchetypeDefinitionSO archetype = AssetDatabase
                .LoadAssetAtPath<EnemyArchetypeDefinitionSO>(
                    "Assets/Resources/SO/V20/Combat/Enemies/enemy_neutral-mercenary.asset")
                ?? throw new InvalidOperationException(
                    "Authored enemy archetype fixture is missing.");
            var patterns = new AuthoredGameplayCatalog(
                new ResourceGameContentCatalog(
                    new UnityGameContentRootLoader()));
            var grid = new Grid(4, 1);
            var context = new FixtureInvasionContext(grid);
            factory = new FixtureIntruderFactory(patterns);
            var individuals = new FixtureEnemyIndividuals(data, archetype);
            var events = new GameEventBus();
            directorObject = new GameObject("Migrated invasion director entry");
            director = directorObject.AddComponent<InvasionDirectorRuntime>();
            director.Construct(
                context,
                new FixedIntruderDataProvider(data),
                factory,
                new DefenseStatusRuntimeService(
                    new DefenseStatusRuntimeFactory()),
                new UnityGameClock(),
                new RandomStreamProvider(7319),
                events,
                new OffenseRegionRuntime(),
                new NoopTreasuryDefenseRuntime(),
                externalInfluence: null,
                campaignRuntime: null,
                new EmptyFacilityCapabilities(),
                new InvasionSignalHornDurableEquipmentRuntime(
                    new MissingDurableEquipmentPolicies(),
                    new MissingDurableEquipmentSlots(),
                    new MissingDurableEquipmentUse()),
                new FixedCharacterPerformance(),
                harness.InvasionFacts,
                CharacterAiEditorTestDependencies.GameCalendar);
            director.ConfigureEnemyIndividuals(
                new FixtureEnemyArchetypeCatalog(archetype),
                individuals);
            threat = new InvasionThreatSnapshot(
                25f,
                InvasionThreatStage.Candidate,
                new InvasionThreatFactors(10f, 5f, 5f, 5f),
                0f,
                0f);
        }

        public string CaptureDomainState() =>
            "active=" + director.ActiveIntruders.Count
            + ";factory=" + factory.LiveCount;

        public bool InvokeProductionEntry() =>
            director.TrySpawnIntruder(threat, out CharacterActor intruder)
            && intruder != null;

        public void Dispose()
        {
            factory.Dispose();
            Object.DestroyImmediate(directorObject);
        }
    }

    private static GameObject CreatePreparedIntruderObject(
        InvasionFactPublicationRuntime publications,
        Grid grid,
        IGameEventBus events,
        out InvasionIntruderRuntime runtime)
    {
        CharacterSO data = AssetDatabase.LoadAssetAtPath<CharacterSO>(
            "Assets/Resources/SO/Character/Intruders/Intruder_Breakthrough.asset")
            ?? throw new InvalidOperationException(
                "Authored invasion intruder fixture is missing.");
        var intruderObject = new GameObject("Migrated invasion intruder entry");
        intruderObject.AddComponent<AbilityMove>();
        intruderObject.AddComponent<CharacterActor>();
        runtime = intruderObject.AddComponent<InvasionIntruderRuntime>();
        CharacterAiEditorTestDependencies.Inject(intruderObject);
        runtime.ConfigureContent(new AuthoredGameplayCatalog(
            new ResourceGameContentCatalog(new UnityGameContentRootLoader())));
        runtime.Initialize(
            new FixtureInvasionContext(grid),
            new DefenseStatusRuntimeService(new DefenseStatusRuntimeFactory()),
            new UnityGameClock(),
            new RandomStreamProvider(4099),
            events,
            new NoopTreasuryDefenseRuntime(),
            new FixedCharacterPerformance(),
            publications,
            CharacterAiEditorTestDependencies.GameCalendar);
        const string RuntimeIdPrefix = "invasion:migrated-entry:";
        string runtimeId = RuntimeIdPrefix + Guid.NewGuid().ToString("N");
        var threat = new InvasionThreatSnapshot(
            20f,
            InvasionThreatStage.Candidate,
            new InvasionThreatFactors(5f, 5f, 5f, 5f),
            0f,
            0f);
        runtime.PrepareBegin(
            data,
            threat,
            new InvasionIntruderSettings
            {
                patternId = InvasionIntruderPatternIds.Hunter,
                rallyDurationSeconds = 12f,
                facilityDamageIntervalSeconds = 0f
            },
            Vector3.zero,
            preparedRuntimeId: runtimeId);
        PublishPreparedInvasionStart(
            runtime,
            publications,
            threat,
            CharacterAiEditorTestDependencies.GameCalendar.Day);
        return intruderObject;
    }

    private static void PublishPreparedInvasionStart(
        InvasionIntruderRuntime runtime,
        InvasionFactPublicationRuntime publications,
        InvasionThreatSnapshot threat,
        int absoluteDay)
    {
        CharacterActor actor = runtime?.IntruderActor
            ?? throw new ArgumentNullException(nameof(runtime));
        CharacterId characterId = CharacterPersistentIdentity.Require(actor);
        var source = new InvasionLifecycleFactData
        {
            runtimeId = runtime.RuntimeId,
            intruderId = characterId.Value,
            intruderName = actor.Identity?.DisplayName ?? actor.name,
            absoluteDay = absoluteDay,
            damageIndexBefore = 0,
            damageIndexAfter = 0,
            cellX = 0,
            cellY = 0,
            stage = InvasionLifecycleStage.Started,
            stateBefore = InvasionIntruderState.None,
            stateAfter = InvasionIntruderState.Rallying,
            riskBefore = 0f,
            riskAfter = Mathf.Max(0f, threat.threat),
            targetKind = InvasionLifecycleTargetKind.None,
            targetId = string.Empty,
            targetName = string.Empty,
            reason = string.Empty,
            resolution = InvasionResolutionKind.None,
            sourceOwnerId = string.Empty
        };
        if (!publications.TryBegin(
                source,
                out InvasionFactPublicationRuntime.Preparation prepared,
                out string failure))
        {
            throw new InvalidOperationException(
                "Invasion start fixture reservation failed: " + failure);
        }
        using (prepared)
        {
            prepared.SetRollback(() => { });
            DomainFactDelivery delivered = prepared.PublishApplied(
                prepared.Snapshot());
            if (!delivered.Completed)
            {
                throw new InvalidOperationException(
                    "Invasion start fixture publication failed: "
                    + delivered.Detail);
            }
            runtime.InitializeInvasionPublicationLineage(
                prepared.PublicationKey);
        }
    }

    private sealed class BuildingFixture : IDisposable
    {
        internal BuildingFixture(
            Grid grid,
            string assetPath,
            Vector2Int position,
            string persistentId)
        {
            BuildingSO data = AssetDatabase.LoadAssetAtPath<BuildingSO>(assetPath)
                ?? throw new InvalidOperationException(
                    "Authored building fixture is missing: " + assetPath);
            Building = new GridBuildingFactory().Create(grid, data, position)
                ?? throw new InvalidOperationException(
                    "Building fixture could not be created: " + assetPath);
            Building.ConstructBuildableObject(
                new NoopBuildingResearchWork(),
                new NoopBuildingStateChange(),
                new NoopBuildingRoomPolicy(),
                combatEquipmentRuntime: null,
                worldRegistry: null,
                worldItemStackRuntime: null,
                abilityRuntimeDispatcher: CharacterAiEditorTestDependencies.BuildingAbilityRuntimeDispatcher,
                gameClock: new UnityGameClock(),
                paidFacilityContracts: null,
                evolutionState: new FacilityEvolutionStateComponentFactory());
            Building.ConstructDebugRules(AllowBuildingDamageRule.Instance);
            Building.RestorePersistentIdentity((BuildingInstanceId)persistentId);
            Building.SetGrid(grid);
            Building.Initialization(data, position);
            if (!grid.RegisterOccupant(
                    Building,
                    data.Placement.Layer,
                    data.GetGridPosList(position),
                    data.Placement.IsMovement))
            {
                throw new InvalidOperationException(
                    "Building fixture could not register on its production grid.");
            }
        }

        internal BuildableObject Building { get; }

        public void Dispose()
        {
            if (Building != null)
                Object.DestroyImmediate(Building.gameObject);
        }
    }

    private sealed class ActorFixture : IDisposable
    {
        private readonly CharacterSO data;
        private readonly GameObject actorObject;
        private readonly CharacterBodyHealthRuntime health;
        private readonly CharacterVitalsActivityObserver vitalsActivities;

        internal ActorFixture(
            CharacterType type,
            string displayName,
            string stableScope = null)
        {
            data = CharacterAiEditorTestDependencies.CreateCharacterFixtureData(
                type,
                displayName,
                "human");
            actorObject = new GameObject(displayName);
            Actor = actorObject.AddComponent<CharacterActor>();
            CharacterAiEditorTestDependencies.Inject(actorObject);
            Actor.EnsureRuntimeState();
            Actor.data = data;
            Actor.characterType = type;
            Actor.Identity.SetPersistentId(
                "character:migrated-defense-target:"
                + (string.IsNullOrWhiteSpace(stableScope)
                    ? Guid.NewGuid().ToString("N")
                    : stableScope));
            var clock = new UnityGameClock();
            var events = new GameEventBus();
            vitalsActivities = new CharacterVitalsActivityObserver(
                events,
                CharacterAiEditorTestDependencies.WorldRegistry);
            vitalsActivities.Initialize();
            health = new CharacterBodyHealthRuntime(
                CharacterAiEditorTestDependencies.WorldRegistry,
                clock,
                events,
                new DynamicFrameWorkBudget(clock, new UnityUiClock()),
                new ResourceAnatomyProfileCatalog(
                    new ResourceGameContentCatalog(
                        new UnityGameContentRootLoader())),
                new DungeonRuntimeAggregateRootStore(),
                CharacterAiEditorTestDependencies.GameCalendar);
            Actor.Stats.ConstructCharacterVitals(
                new CharacterStatsVitalsService(
                    health,
                    health,
                    events,
                    new CharacterDeathEventFactory(
                        CharacterAiEditorTestDependencies.WorldRegistry,
                        CharacterAiEditorTestDependencies.GameCalendar),
                    new NoopOwnerRunLifecycleService()));
            health.ConfigureVitals(Actor, 100f, resetCurrentHealth: true);
        }

        internal CharacterActor Actor { get; }
        internal CharacterBodyHealthRuntime Health => health;
        internal string CaptureHealthState() => JsonUtility.ToJson(health.Capture());

        public void Dispose()
        {
            vitalsActivities.Dispose();
            Object.DestroyImmediate(actorObject);
            Object.DestroyImmediate(data);
        }
    }

    private sealed class FixtureInvasionContext : IInvasionIntruderContext
    {
        private readonly Grid grid;

        internal FixtureInvasionContext(Grid grid) =>
            this.grid = grid ?? throw new ArgumentNullException(nameof(grid));

        public bool TryGetGrid(out Grid value)
        {
            value = grid;
            return true;
        }

        public bool TryGetOwner(out CharacterActor owner)
        {
            owner = null;
            return false;
        }

        public bool TryResolveBuilding(
            BuildingInstanceId id,
            out BuildableObject building)
        {
            building = null;
            return false;
        }

        public bool TryResolveEntry(out InvasionIntruderEntry entry)
        {
            entry = new InvasionIntruderEntry(
                Vector2Int.zero,
                new Vector3(-1f, 0f, 0f),
                Vector3.zero);
            return true;
        }

        public InvasionIntruderSettings ApplyRunVariables(
            InvasionIntruderSettings source) => source;
    }

    private sealed class FixtureIntruderFactory : IInvasionIntruderFactory,
        IDisposable
    {
        private readonly IInvasionIntruderPatternDefinitionCatalog patterns;
        private readonly List<InvasionIntruderRuntime> live = new();

        internal FixtureIntruderFactory(
            IInvasionIntruderPatternDefinitionCatalog patterns) =>
            this.patterns = patterns
                ?? throw new ArgumentNullException(nameof(patterns));

        internal int LiveCount => live.Count(value => value != null);

        public InvasionIntruderRuntime Create(
            GameObject intruderPrefab,
            Vector3 position) => CreateRuntime(position);

        public InvasionIntruderRuntime CreateDetached(
            GameObject intruderPrefab,
            Vector3 position) => CreateRuntime(position);

        public void Publish(InvasionIntruderRuntime runtime)
        {
            if (runtime == null)
                throw new ArgumentNullException(nameof(runtime));
            runtime.gameObject.SetActive(true);
        }

        public void PublishDetached(InvasionIntruderRuntime runtime) =>
            Publish(runtime);

        public void DestroyDetached(InvasionIntruderRuntime runtime)
        {
            live.Remove(runtime);
            if (runtime != null)
                Object.DestroyImmediate(runtime.gameObject);
        }

        public InvasionIntruderRuntime EnsureRuntime(GameObject intruderObject)
        {
            if (intruderObject == null)
                throw new ArgumentNullException(nameof(intruderObject));
            InvasionIntruderRuntime runtime =
                intruderObject.GetComponent<InvasionIntruderRuntime>()
                ?? intruderObject.AddComponent<InvasionIntruderRuntime>();
            runtime.ConfigureContent(patterns);
            if (!live.Contains(runtime))
                live.Add(runtime);
            return runtime;
        }

        public void Dispose()
        {
            for (int index = live.Count - 1; index >= 0; index--)
            {
                if (live[index] != null)
                    Object.DestroyImmediate(live[index].gameObject);
            }
            live.Clear();
        }

        private InvasionIntruderRuntime CreateRuntime(Vector3 position)
        {
            var value = new GameObject("Migrated director intruder");
            value.transform.position = position;
            value.AddComponent<AbilityMove>();
            value.AddComponent<CharacterActor>();
            InvasionIntruderRuntime runtime =
                value.AddComponent<InvasionIntruderRuntime>();
            CharacterAiEditorTestDependencies.Inject(value);
            runtime.ConfigureContent(patterns);
            live.Add(runtime);
            return runtime;
        }
    }

    private sealed class FixedIntruderDataProvider :
        IInvasionIntruderDataProvider
    {
        private readonly CharacterSO data;

        internal FixedIntruderDataProvider(CharacterSO data) =>
            this.data = data ?? throw new ArgumentNullException(nameof(data));

        public CharacterSO GetRequiredIntruderData(CharacterSO configuredData) =>
            data;
    }

    private sealed class FixtureEnemyArchetypeCatalog : IEnemyArchetypeCatalog
    {
        private readonly EnemyArchetypeDefinitionSO archetype;

        internal FixtureEnemyArchetypeCatalog(
            EnemyArchetypeDefinitionSO archetype)
        {
            this.archetype = archetype
                ?? throw new ArgumentNullException(nameof(archetype));
            All = new[] { archetype };
        }

        public IReadOnlyList<EnemyArchetypeDefinitionSO> All { get; }

        public bool TryGet(
            string id,
            out EnemyArchetypeDefinitionSO definition)
        {
            definition = string.Equals(
                id,
                archetype.stableId,
                StringComparison.Ordinal)
                    ? archetype
                    : null;
            return definition != null;
        }

        public EnemyArchetypeDefinitionSO Require(string id) =>
            TryGet(id, out EnemyArchetypeDefinitionSO value)
                ? value
                : throw new KeyNotFoundException(id);
    }

    private sealed class FixtureEnemyIndividuals : IEnemyIndividualFactory
    {
        private readonly CharacterSO data;
        private readonly EnemyArchetypeDefinitionSO archetype;

        internal FixtureEnemyIndividuals(
            CharacterSO data,
            EnemyArchetypeDefinitionSO archetype)
        {
            this.data = data ?? throw new ArgumentNullException(nameof(data));
            this.archetype = archetype
                ?? throw new ArgumentNullException(nameof(archetype));
        }

        public EnemyIndividualSaveData Create(
            string enemyArchetypeId,
            CharacterId characterId,
            string deterministicContext) => new()
        {
            characterId = characterId.Value,
            enemyArchetypeId = enemyArchetypeId,
            originFactionId = archetype.factionId,
            phenotypeSpeciesId = data.speciesTag,
            displayName = "Migrated Entry Intruder",
            combatStatMultiplier = 1f
        };

        public EnemyIndividualBlueprint RequireBlueprint(
            EnemyIndividualSaveData value) => new(
            value.Clone(),
            archetype,
            CharacterSpawnRequest.FromAuthoring(data));

        public void EnsureCharacterDomains(EnemyIndividualBlueprint blueprint)
        {
        }
    }

    private sealed class FixedCharacterPerformance : ICharacterPerformanceQuery
    {
        public CharacterFunctionalCapacitySnapshot GetFunctionalCapacities(
            CharacterActor actor) => default;

        public CharacterPerformanceSnapshot Evaluate(
            CharacterActor actor,
            string formulaId,
            float contextFactor = 1f,
            GameplayEffectContext effectContext = null) =>
            new CharacterPerformanceSnapshot
            {
                FormulaId = formulaId ?? string.Empty,
                ContextFactor = contextFactor,
                Value = Mathf.Max(0f, contextFactor)
            };

        public CharacterPerformanceSnapshot Evaluate(
            CharacterActor actor,
            string formulaId,
            CharacterPerformanceEvaluationContext context) =>
            Evaluate(actor, formulaId);

        public CharacterPerformanceSnapshot EvaluateWork(
            CharacterActor actor,
            WorkTypeId workTypeId,
            CharacterPerformanceResultChannel resultChannel,
            CharacterPerformanceEvaluationContext context) =>
            new CharacterPerformanceSnapshot
            {
                FormulaId = workTypeId.Value,
                ResultChannel = resultChannel,
                Value = 1f
            };

        public IReadOnlyList<CharacterPerformanceSnapshot> EvaluateDomain(
            CharacterActor actor,
            CharacterPerformanceFormulaDomain domain) =>
            Array.Empty<CharacterPerformanceSnapshot>();
    }

    private sealed class NoopTreasuryDefenseRuntime : ITreasuryDefenseRuntime
    {
        public IReadOnlyList<TreasuryDefensePolicy> Policies =>
            Array.Empty<TreasuryDefensePolicy>();
        public TreasuryDefensePolicy GetPolicy(DefenseFacility facility) => new();
        public void UpsertPolicy(TreasuryDefensePolicy policy) { }
        public TreasuryDefenseAuthorization AuthorizeShot(
            DefenseFacility facility,
            CharacterActor intruder,
            string invasionId,
            float threat,
            bool isBoss) => new(true, false, 1f, 0, string.Empty);
        public int GetSpent(string invasionId, string facilityPersistentId) => 0;
        public string GetLastFailureReason(string facilityPersistentId) =>
            string.Empty;
        public TreasuryDefenseSaveData Capture() => new();
        public void Restore(TreasuryDefenseSaveData saveData) { }
    }

    private sealed class EmptyFacilityCapabilities : IFacilityCapabilityQuery
    {
        public IReadOnlyList<BuildableObject> FindOperational(
            FacilityCapabilityKind capability,
            string buildingDefinitionId = "") =>
            Array.Empty<BuildableObject>();

        public IReadOnlyList<BuildableObject> FindOperational(
            ResearchFacilityCommandKind command) =>
            Array.Empty<BuildableObject>();
    }

    private sealed class MissingDurableEquipmentPolicies :
        IDurableFacilityEquipmentPolicyQuery
    {
        public long Revision => 0L;
        public bool TryGetPolicy(
            string policyId,
            out DurableFacilityEquipmentPolicy policy)
        {
            policy = null;
            return false;
        }
        public IReadOnlyList<DurableFacilityEquipmentPolicy> CapturePolicies() =>
            Array.Empty<DurableFacilityEquipmentPolicy>();
    }

    private sealed class MissingDurableEquipmentSlots :
        IDurableFacilityEquipmentSlotCommand
    {
        public DurableFacilityEquipmentSlotResult TryReconcile(
            DurableFacilityEquipmentAssignment desired) => default;
        public DurableFacilityEquipmentSlotResult TryClose(
            DurableFacilityEquipmentSlotKey key,
            string reasonCode) => default;
        public DurableFacilityEquipmentSlotResult TryEnsureSupply(
            DurableFacilityEquipmentSlotKey key) => default;
        public IReadOnlyList<DurableFacilityEquipmentSlotResult>
            TryAdvancePending() =>
            Array.Empty<DurableFacilityEquipmentSlotResult>();
    }

    private sealed class MissingDurableEquipmentUse :
        IDurableFacilityEquipmentUseCommand
    {
        public DurableFacilityEquipmentUseResult TryApplyWearAndEffect(
            DurableFacilityEquipmentSlotKey key,
            string requirementId,
            double wearAmount,
            IDurableFacilityEquipmentEffectCommit effect) => default;
    }

    private sealed class NoopDefensePhysicalGateway :
        IDefenseFacilityPhysicalItemGateway
    {
        public IReadOnlyList<WorldItemStackSnapshot> GetAllStacks() =>
            Array.Empty<WorldItemStackSnapshot>();
        public bool TryCommitPendingBatchPhysicalDisposition(
            IReadOnlyList<PhysicalItemTransformInput> inputs,
            PhysicalItemDispositionKind kind,
            string operationId,
            string reasonCode,
            out PhysicalItemBatchDispositionReceipt receipt,
            out string failureReason)
        {
            receipt = default;
            failureReason = "unavailable";
            return false;
        }
        public bool TryGetPendingBatchPhysicalDisposition(
            string operationId,
            out PhysicalItemBatchDispositionReceipt receipt)
        {
            receipt = default;
            return false;
        }
        public bool AcknowledgeBatchPhysicalDisposition(
            string commitId,
            out string failureReason)
        {
            failureReason = "unavailable";
            return false;
        }
    }

    private sealed class AlwaysReadyDefenseInputOwner :
        IDefenseFacilityInputOwnerRuntime
    {
        public bool TryEnsureAuthority(
            DefenseFacility facility,
            out string failureReason)
        {
            failureReason = string.Empty;
            return true;
        }
        public bool TryReconcileLive(out string failureReason)
        {
            failureReason = string.Empty;
            return true;
        }
        public bool TryReconcileRestore(
            IReadOnlyCollection<DefenseFacilityState> restoredStates,
            out string failureReason)
        {
            failureReason = string.Empty;
            return true;
        }
    }

    private sealed class AlwaysPoweredQuery : IPowerInfrastructureQuery
    {
        public int Version => 0;
        public IReadOnlyList<PowerNetworkSnapshot> Networks =>
            Array.Empty<PowerNetworkSnapshot>();
        public bool IsPowered(BuildableObject building) => true;
        public bool TryGetNode(
            BuildableObject building,
            out PowerNodeSnapshot snapshot)
        {
            snapshot = null;
            return false;
        }
    }

    private sealed class NoopDefenseFacilityNetwork :
        IDefenseFacilityNetworkRuntime
    {
        public int Version => 0;
        public DefenseFacilityNetworkSnapshot GetSnapshot(
            DefenseFacility facility) => new(
            facility,
            null,
            null,
            null,
            null,
            true);
        public bool HasAutomaticControl(DefenseFacility facility) => true;
        public bool HasMaintenanceCoverage(DefenseFacility facility) => false;
        public bool HasSupplyCoverage(DefenseFacility facility) => false;
        public void DetectIntruder(
            string raidId,
            Vector2Int position,
            IDefenseRaidAwarenessRuntime awareness) { }
    }

    private sealed class NoopBuildingResearchWork : IBuildingResearchWorkPort
    {
        public bool HasResearchWorkFor(IBuildingWorldEntryPort facility) => false;
    }

    private sealed class NoopBuildingStateChange :
        IBuildingFacilityStateChangePort
    {
        public void MarkDynamicStateDirty() { }
    }

    private sealed class NoopBuildingRoomPolicy : IBuildingRoomPolicyPort
    {
        public bool IsFacilityRoleAvailable(
            IBuildingWorldEntryPort building,
            FacilityRole requestedRole,
            out string rejectReason)
        {
            rejectReason = string.Empty;
            return true;
        }
        public float GetRoomUtilityScore(
            IBuildingWorldEntryPort building,
            FacilityRole role) => 1f;
        public int GetEffectiveCapacity(IBuildingWorldEntryPort building) => 1;
        public BuildingRoomOperationalSnapshot GetOperationalProfile(
            IBuildingWorldEntryPort building) => new(
            Array.Empty<IBuildingWorldEntryPort>(),
            hasRoom: false,
            isUsableRoom: true,
            qualityScore: 1f,
            seatCapacity: 0,
            tableCapacity: 0,
            serviceCapacity: 1,
            StockCategory.General,
            new Dictionary<StockCategory, int>());
    }

    private sealed class AllowBuildingDamageRule : IBuildingDamageRulePort
    {
        internal static readonly AllowBuildingDamageRule Instance = new();

        public bool ShouldBlockFacilityDamage(bool damaged) => false;
    }

    private sealed class NoopOwnerRunLifecycleService :
        IOwnerRunLifecycleService
    {
        public void HandleOwnerDeath(CharacterActor owner, string reason) { }
    }

    private sealed class NoCoverDurabilityRegistry :
        ICombatCoverDurabilityRegistry
    {
        public void Register(CombatCoverDurability durability) { }
        public void Unregister(CombatCoverDurability durability) { }
        public bool TryApplyDamage(string sourceId, float damage) => false;
    }

    private sealed class NoWorldUiHierarchy : IWorldUiHierarchy
    {
        public Transform GetWorldUiRoot(GameObject sceneHint = null) => null;
        public void ParentToWorldUi(GameObject child) { }
    }
}
#endif
