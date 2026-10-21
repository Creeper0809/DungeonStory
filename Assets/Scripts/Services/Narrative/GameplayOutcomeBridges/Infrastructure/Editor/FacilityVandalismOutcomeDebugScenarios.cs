#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using DungeonStory.Foundation;
using DungeonStory.Foundation.Facts;
using DungeonStory.Narrative.Korean;
using UnityEngine;

public static class FacilityVandalismOutcomeDebugScenarios
{
    private const string ReportEnvironmentVariable =
        "DUNGEONSTORY_A04_REPORT_PATH";
    private const string DefaultReportPath =
        "Artifacts/QA/GameplayActionLedgerAudit-20260926-r1/A04/"
        + "focused-latest.txt";

    public static bool RunAll(bool log = false)
    {
        var evidence = new List<string>();
        try
        {
            VerifyExactOutcomeAndReplay(evidence);
            VerifyRejectedCommitRollsBack(evidence);
            VerifyInvalidTargetsDoNotPrepare(evidence);
            VerifyMacroProductionEntry(evidence);
            VerifyBreakdownProductionEntryAndMovementInvalidation(evidence);
            VerifyBreakdownMovementCancellation(evidence);
            VerifyBreakdownTargetReplacement(evidence);
            VerifyFacilityStateRoundTripAndTamperRejection(evidence);
            VerifyUnknownDeliveryAndDestroyedOwnerRecovery(evidence);
            WriteReport("PASS", evidence, string.Empty);
            if (log)
                Debug.Log("A04 facility-vandalism outcome scenarios passed.");
            return true;
        }
        catch (Exception exception)
        {
            WriteReport("FAIL", evidence, exception.ToString());
            if (log)
                Debug.LogError(exception);
            return false;
        }
    }

    public static string RunObserverFocused()
    {
        var evidence = new List<string>();
        VerifyExactOutcomeAndReplay(evidence);
        VerifyRejectedCommitRollsBack(evidence);
        VerifyMacroProductionEntry(evidence);
        VerifyBreakdownProductionEntryAndMovementInvalidation(evidence);
        VerifyFacilityStateRoundTripAndTamperRejection(evidence);
        VerifyUnknownDeliveryAndDestroyedOwnerRecovery(evidence);
        return "facility-vandalism/observer-focused=PASS\n" + string.Join("\n", evidence);
    }

    private static void VerifyExactOutcomeAndReplay(
        ICollection<string> evidence)
    {
        using var outcome = new OutcomeFixture("run:a04-exact-outcome");
        using var fixture = new DomainFixture(
            "exact",
            "character:a04-exact",
            "building:a04-exact");
        ICharacterVandalismCommand transaction = outcome.Command;
        var witness = new GameplayResultKey(
            "social.witness-mood",
            new GameplayOperationId("witness-mood:a04:exact:1"),
            1L,
            0);

        Require(transaction.TryCommitDamage(
                fixture.Actor,
                fixture.Building,
                FacilityVandalismSourceKind.Macro,
                "retaliation",
                new[] { witness.ToString() },
                out string publicationKey,
                out string failure),
            "Exact transaction failed: " + failure);
        GameplayOutcomeSnapshot snapshot = outcome.Single();
        Require(fixture.Building.IsDamaged
                && fixture.Building.NextVandalismOperationSequence == 2L
                && fixture.Building.LastVandalism.publicationKey
                    == publicationKey
                && snapshot.producerId
                    == FacilityVandalismOutcomeIds.ProducerId
                && snapshot.operationId
                    == "facility-vandalism:building:a04-exact:1"
                && snapshot.commitRevision == 1L
                && snapshot.ownerRevision == 1L
                && snapshot.participants.Count == 2
                && snapshot.participants[0].entityId
                    == "character:a04-exact"
                && snapshot.participants[1].entityId
                    == "building:a04-exact"
                && Fact(snapshot, FacilityVandalismOutcomeIds.SourceFact)
                    == "macro"
                && Fact(snapshot, FacilityVandalismOutcomeIds.ReasonFact)
                    == "retaliation"
                && Fact(snapshot, FacilityVandalismOutcomeIds.DefinitionIdFact)
                    == BuildingDefinitionIdentity.Resolve(fixture.Definition)
                && Fact(snapshot,
                    FacilityVandalismOutcomeIds.WitnessMoodChildrenFact)
                    == witness.ToString(),
            "Exact outcome omitted or changed authoritative vandalism facts.");

        Require(!transaction.TryCommitDamage(
                    fixture.Actor,
                    fixture.Building,
                    FacilityVandalismSourceKind.Macro,
                    "retaliation",
                    new[] { witness.ToString() },
                    out _,
                    out string replayFailure)
                && replayFailure ==
                    "facility-vandalism-target-already-damaged"
                && outcome.Count == 1
                && fixture.Building.NextVandalismOperationSequence == 2L,
            "Durable state replay duplicated damage or ledger output.");
        evidence.Add(
            "exact=actor+facility+source+reason+definition+child;replayDuplicate=0");
    }

    private static void VerifyRejectedCommitRollsBack(
        ICollection<string> evidence)
    {
        using var fixture = new DomainFixture(
            "reject",
            "character:a04-reject",
            "building:a04-reject");
        var facts = new RecordingFacts
        {
            RejectNextCommit = true
        };
        CharacterVandalismCommand transaction = Transaction(facts);
        string stateBefore = JsonUtility.ToJson(fixture.Building.FacilityState);

        Require(!transaction.TryCommitDamage(
                    fixture.Actor,
                    fixture.Building,
                    FacilityVandalismSourceKind.Deprivation,
                    "mental-instability",
                    Array.Empty<string>(),
                    out string publicationKey,
                    out string failure)
                && publicationKey.Length == 0
                && failure.StartsWith(
                    "facility-vandalism-publication-rejected:",
                    StringComparison.Ordinal)
                && !fixture.Building.IsDamaged
                && fixture.Building.NextVandalismOperationSequence == 1L
                && fixture.Building.LastVandalism.IsEmpty
                && JsonUtility.ToJson(fixture.Building.FacilityState)
                    == stateBefore
                && facts.SuccessfulResultCount == 0,
            "Rejected outcome did not fully roll back facility authority.");
        evidence.Add("commit-reject=damage+owner-rollback;durableOutcome=0");
    }

    private static void VerifyInvalidTargetsDoNotPrepare(
        ICollection<string> evidence)
    {
        using (var damaged = new DomainFixture(
                   "already-damaged",
                   "character:a04-damaged",
                   "building:a04-damaged"))
        {
            damaged.Building.SetDamaged(true);
            var facts = new RecordingFacts();
            var transaction = Transaction(facts);
            Require(!transaction.TryCommitDamage(
                        damaged.Actor,
                        damaged.Building,
                        FacilityVandalismSourceKind.Macro,
                        "duplicate",
                        Array.Empty<string>(),
                        out _,
                        out string failure)
                    && failure
                        == "facility-vandalism-target-already-damaged"
                    && facts.PrepareCount == 0,
                "Already damaged facility reached outcome preparation.");
        }

        using (var destroyed = new DomainFixture(
                   "destroyed",
                   "character:a04-destroyed",
                   "building:a04-destroyed"))
        {
            destroyed.Building.isDestroy = true;
            var facts = new RecordingFacts();
            var transaction = Transaction(facts);
            Require(!transaction.TryCommitDamage(
                        destroyed.Actor,
                        destroyed.Building,
                        FacilityVandalismSourceKind.Deprivation,
                        "mental-instability",
                        Array.Empty<string>(),
                        out _,
                        out string failure)
                    && failure == "facility-vandalism-target-destroyed"
                    && facts.PrepareCount == 0
                    && !destroyed.Building.IsDamaged,
                "Destroyed facility reached damage or outcome preparation.");
        }

        Type[] nonPersistentTypes =
        {
            typeof(ConstructionSite),
            typeof(ExteriorZoneMarker)
        };
        foreach (Type nonPersistentType in nonPersistentTypes)
        {
            string slug = nonPersistentType == typeof(ConstructionSite)
                ? "construction-site"
                : "exterior-zone";
            using var nonPersistent = new DomainFixture(
                slug,
                "character:a04-" + slug,
                "building:a04-" + slug,
                nonPersistentType);
            var facts = new RecordingFacts();
            var transaction = Transaction(facts);
            Require(!transaction.TryCommitDamage(
                        nonPersistent.Actor,
                        nonPersistent.Building,
                        FacilityVandalismSourceKind.Deprivation,
                        "mental-instability",
                        Array.Empty<string>(),
                        out _,
                        out string failure)
                    && failure
                        == "facility-vandalism-target-not-persistent-facility"
                    && facts.PrepareCount == 0
                    && !nonPersistent.Building.IsDamaged
                    && nonPersistent.Building.LastVandalism.IsEmpty,
                nonPersistentType.Name
                    + " reached generic facility damage or outcome preparation.");
        }
        evidence.Add(
            "invalid=already-damaged+destroyed+construction-site+exterior-zone;prepare=0");
    }

    private static void VerifyMacroProductionEntry(
        ICollection<string> evidence)
    {
        using var outcome = new OutcomeFixture("run:a04-macro-entry");
        using var fixture = new DomainFixture(
            "macro-entry",
            "character:a04-macro-entry",
            "building:a04-macro-entry");
        var pipeline = new CharacterAiDecisionPipeline(
            NoCharacterDeprivationBoundary.Instance,
            NoCharacterDeprivationBoundary.Instance,
            vandalismCommands: outcome.Command);
        fixture.Actor.Blackboard.SetMacroGoal(new CharacterMacroGoal
        {
            type = CharacterMacroGoalType.Vandalize,
            reason = "macro-a04-production-entry",
            targetFacilityId = fixture.Definition.id,
            validUntil = Time.time + 10f,
            source = "A04"
        });

        CharacterAiDecisionTickResult result = pipeline.RunVandalizeMacro(
            fixture.Actor,
            fixture.Actor.Blackboard,
            fixture.Actor.Blackboard.ActiveMacroGoal);
        GameplayOutcomeSnapshot snapshot = outcome.Single();
        Require(result.Handled
                && fixture.Building.IsDamaged
                && !fixture.Actor.Blackboard.HasActiveMacroGoal()
                && fixture.Actor.Blackboard.CurrentTask == "Vandalize"
                && Fact(snapshot, FacilityVandalismOutcomeIds.SourceFact)
                    == "macro"
                && Fact(snapshot, FacilityVandalismOutcomeIds.ReasonFact)
                    == "macro-a04-production-entry",
            "Actual macro entry did not commit the shared transaction.");
        evidence.Add("entry=CharacterAiDecisionPipeline.RunVandalizeMacro;outcome=1");
    }

    private static void VerifyFacilityStateRoundTripAndTamperRejection(
        ICollection<string> evidence)
    {
        using var outcome = new OutcomeFixture("run:a04-save-roundtrip");
        using var source = new DomainFixture(
            "save-source",
            "character:a04-save",
            "building:a04-save");
        Require(outcome.Command.TryCommitDamage(
                source.Actor,
                source.Building,
                FacilityVandalismSourceKind.Deprivation,
                "mental-instability",
                Array.Empty<string>(),
                out string committedKey,
                out string failure),
            "Save-source commit failed: " + failure);
        var sourceModule = new FacilityRuntimeStateModule(source.Building);
        string payload = sourceModule.CaptureState();
        string roundTripped = JsonUtility.ToJson(
            JsonUtility.FromJson<FacilityRuntimeState>(payload));

        using var restored = new DomainFixture(
            "save-restored",
            "character:a04-save",
            "building:a04-save");
        restored.Building.SetDamaged(true);
        var restoredModule = new FacilityRuntimeStateModule(restored.Building);
        Require(restoredModule.TryRestoreState(
                    restoredModule.CurrentVersion,
                    roundTripped,
                    out string restoreError)
                && restored.Building.IsDamaged
                && restored.Building.NextVandalismOperationSequence == 2L
                && restored.Building.LastVandalism.publicationKey
                    == committedKey
                && restored.Building.LastVandalism.source == "deprivation",
            "Facility vandalism save round-trip failed: " + restoreError);

        FacilityRuntimeState tampered = JsonUtility.FromJson<FacilityRuntimeState>(
            roundTripped);
        tampered.lastVandalism.publicationKey =
            "infrastructure.facility-vandalism|facility-vandalism:building:a04-save:1|2|0";
        string stateBefore = restoredModule.CaptureState();
        var validReport = new DungeonGameRestoreReport();
        FacilityVandalismFactPreflight.Validate(outcome.Publications.Capture(), World(source.Building),
            outcome.Ledger.CaptureGameplayOutcomes(), validReport);
        Require(validReport.Success, "Valid damage source/canonical join was rejected.");
        foreach (var invalid in new[] { tampered, JsonUtility.FromJson<FacilityRuntimeState>(roundTripped) })
        {
            if (invalid.lastVandalism.publicationKey == committedKey) invalid.lastVandalism.actorName += "-tampered";
            var invalidWorld = World(restored.Building);
            invalidWorld.buildings[0].stateModules[0].payload = JsonUtility.ToJson(invalid);
            var report = new DungeonGameRestoreReport();
            FacilityVandalismFactPreflight.Validate(outcome.Publications.Capture(), invalidWorld,
                outcome.Ledger.CaptureGameplayOutcomes(), report);
            Require(!report.Success && restoredModule.CaptureState() == stateBefore,
                "Observer accepted tampered key/name or preflight mutated owner.");
        }
        var raw = Newtonsoft.Json.Linq.JObject.Parse(roundTripped);
        ((Newtonsoft.Json.Linq.JObject)raw["lastVandalism"]).Remove("witnessPublicationKeys");
        Require(!restoredModule.TryRestoreState(restoredModule.CurrentVersion, raw.ToString(), out _)
            && restoredModule.CaptureState() == stateBefore, "Missing frozen witness data was defaulted or partially restored.");

        Require(!outcome.Command.TryCommitDamage(
                    restored.Actor,
                    restored.Building,
                    FacilityVandalismSourceKind.Deprivation,
                    "mental-instability",
                    Array.Empty<string>(),
                    out _,
                    out string replayFailure)
                && replayFailure
                    == "facility-vandalism-target-already-damaged"
                && outcome.Count == 1,
            "Restored terminal state allowed a duplicate facility mutation.");
        evidence.Add(
            "save=v7-json-roundtrip;tamperedPublication=rejected-no-mutation;restoredReplay=0");
    }

    private static ModularFacilityWorldSaveData World(BuildableObject building)
    {
        var result = new ModularFacilityWorldSaveData();
        if (building != null) result.buildings.Add(new ModularFacilityBuildingSaveData
        {
            persistentInstanceId = building.RequirePersistentInstanceId().Value,
            stateModules = new List<BuildingStateModuleSaveData>
            {
                new() { moduleId = BuildingStateModuleIds.FacilityOperation, version = FacilityRuntimeStateModule.Version,
                    payload = new FacilityRuntimeStateModule(building).CaptureState() }
            }
        });
        return result;
    }

    private static void VerifyUnknownDeliveryAndDestroyedOwnerRecovery(ICollection<string> evidence)
    {
        foreach (bool commitBeforeLosingReply in new[] { false, true })
        {
            string suffix = commitBeforeLosingReply ? "after" : "before";
            using var outcome = new OutcomeFixture("run:a04-unknown-" + suffix, commitBeforeLosingReply);
            using var source = new DomainFixture("unknown-" + suffix,
                "character:a04-unknown-" + suffix, "building:a04-unknown-" + suffix);
            Require(outcome.Command.TryCommitDamage(source.Actor, source.Building, FacilityVandalismSourceKind.Macro,
                "frozen-first", Array.Empty<string>(), out string first, out string failure), "Unknown delivery undid damage:" + failure);
            Require(source.Building.IsDamaged && source.Building.NextVandalismOperationSequence == 2
                && outcome.Publications.Capture().pending.Count == 1 && outcome.Count == (commitBeforeLosingReply ? 1 : 0),
                "Unknown delivery lost source or repeated mutation.");
            // A repair and second damage must not overwrite the first frozen publication.
            source.Building.SetDamaged(false);
            source.Definition.objectName += "-renamed";
            Require(outcome.Command.TryCommitDamage(source.Actor, source.Building, FacilityVandalismSourceKind.Macro,
                "second", Array.Empty<string>(), out _, out failure), "Second damage failed:" + failure);
            Require(source.Building.NextVandalismOperationSequence == 3 && outcome.Publications.Capture().pending.Count == 1,
                "New owner receipt overwrote undelivered damage.");
            var pending = DungeonSaveJson.Deserialize<FacilityVandalismPendingSaveData>(JsonUtility.ToJson(outcome.Publications.Capture()));
            var report = new DungeonGameRestoreReport();
            FacilityVandalismFactPreflight.Validate(pending, World(source.Building), outcome.Ledger.CaptureGameplayOutcomes(), report);
            Require(report.Success, "Older pending damage failed live-owner save join.");
            UnityEngine.Object.DestroyImmediate(source.Building.gameObject);
            var ledgerSave = DungeonSaveJson.Deserialize<GameplayOutcomeLedgerSaveData>(JsonUtility.ToJson(outcome.Ledger.CaptureGameplayOutcomes()));
            using var fresh = new OutcomeFixture("run:a04-fresh-" + suffix);
            var candidate = fresh.Ledger.PrepareGameplayOutcomeRestore(ledgerSave);
            fresh.Ledger.BeginRestoreCandidate(); fresh.Ledger.PublishGameplayOutcomeRestore(candidate);
            fresh.Ledger.PublishRestoreCandidate(); fresh.Ledger.CompleteRestoreCandidate();
            fresh.Publications.PublishRestore(fresh.Publications.PrepareRestore(pending));
            var absentOwnerReport = new DungeonGameRestoreReport();
            FacilityVandalismFactPreflight.Validate(pending, World(null), ledgerSave, absentOwnerReport);
            Require(absentOwnerReport.Success && fresh.Publications.Recover() && fresh.Count == 2
                && fresh.Publications.Capture().pending.Count == 0 && fresh.Publications.Recover() && fresh.Count == 2,
                "Destroyed-owner JSON recovery lost facts or duplicated outcomes.");
            var firstRow = fresh.Ledger.CaptureGameplayOutcomes().exactOutcomes.Single(value => value.commitRevision == 1);
            Require(firstRow.operationId == FacilityVandalismOutcomeIds.OperationId(new BuildingInstanceId(pending.pending[0].facilityId), 1)
                && Fact(firstRow, FacilityVandalismOutcomeIds.ReasonFact) == "frozen-first"
                && pending.pending[0].publicationKey == first, "Replay changed frozen identity/facts.");
            evidence.Add("unknown-" + suffix + "/repair-redamage/destroyed-owner/json-new-dispatcher/replay-once=PASS");
        }
    }

    private static void VerifyBreakdownProductionEntryAndMovementInvalidation(
        ICollection<string> evidence)
    {
        using (var outcome = new OutcomeFixture("run:a04-breakdown-entry"))
        using (var fixture = new BreakdownFixture(
                   "entry",
                   "character:a04-breakdown-entry",
                   "building:a04-breakdown-entry",
                   outcome.Command))
        {
            fixture.RunMovementThenResume();
            GameplayOutcomeSnapshot snapshot = outcome.Single();
            Require(fixture.Building.IsDamaged
                    && fixture.Building.NextVandalismOperationSequence == 2L
                    && Fact(snapshot, FacilityVandalismOutcomeIds.SourceFact)
                        == "deprivation"
                    && Fact(snapshot, FacilityVandalismOutcomeIds.ReasonFact)
                        == "mental-instability",
                "Actual violent-breakdown entry did not commit vandalism.");
        }

        using (var outcome = new OutcomeFixture("run:a04-breakdown-invalidated"))
        using (var fixture = new BreakdownFixture(
                   "invalidated",
                   "character:a04-breakdown-invalidated",
                   "building:a04-breakdown-invalidated",
                   outcome.Command))
        {
            fixture.BeginMovement();
            fixture.Building.isDestroy = true;
            fixture.ResumeAfterMovement();
            Require(!fixture.Building.IsDamaged
                    && fixture.Building.NextVandalismOperationSequence == 1L
                    && fixture.Building.LastVandalism.IsEmpty
                    && outcome.Count == 0,
                "Destroyed target committed damage after the movement yield.");
        }
        evidence.Add(
            "entry=CharacterBreakdownActionRunner.RunViolentImpulse;source=deprivation;movementInvalidation=no-mutation");
    }

    private static void VerifyBreakdownMovementCancellation(
        ICollection<string> evidence)
    {
        using var outcome = new OutcomeFixture("run:a04-movement-cancelled");
        using var fixture = new BreakdownFixture(
            "movement-cancelled",
            "character:a04-movement-cancelled",
            "building:a04-movement-cancelled",
            outcome.Command);
        fixture.BeginMovement();
        AIBrain brain = fixture.Actor.Brain;
        string stateBefore = JsonUtility.ToJson(fixture.Building.FacilityState);
        long epochBefore = brain.ExternalIntentEpoch;
        int terminalBefore = brain.ExternalIntentTerminalCount;
        int staleBefore = brain.ExternalIntentStaleCompletionCount;
        int rejectedBefore = brain.ExternalIntentRejectedCount;
        fixture.CancelMovementIntent();
        fixture.ResumeAfterMovement();
        string stateAfter = JsonUtility.ToJson(fixture.Building.FacilityState);
        Require(!brain.IsExternallyDrivenActionActive
                && brain.ExternalIntentOwnerId.Length == 0
                && brain.ExternalIntentKind == CharacterActionIntentKind.None
                && brain.ExternalIntentEpoch == epochBefore
                && brain.LastExternalIntentTerminalKind
                    == CharacterAiActionTerminalKind.Cancelled
                && brain.ExternalIntentTerminalCount == terminalBefore + 1
                && brain.ExternalIntentStaleCompletionCount == staleBefore
                && brain.ExternalIntentRejectedCount == rejectedBefore
                && !fixture.Building.IsDamaged
                && fixture.Building.LastVandalism.IsEmpty
                && stateAfter == stateBefore
                && outcome.Count == 0,
            "Cancelled movement committed facility damage or changed its owner.");
        evidence.Add("movement-cancel-observation="
            + Newtonsoft.Json.JsonConvert.SerializeObject(new
            {
                actorId = fixture.Actor.Identity.PersistentId,
                targetId = fixture.Building.RequirePersistentInstanceId().Value,
                stateBefore,
                stateAfter,
                epochBefore,
                epochAfter = brain.ExternalIntentEpoch,
                terminalBefore,
                terminalAfter = brain.ExternalIntentTerminalCount,
                terminalKind = brain.LastExternalIntentTerminalKind.ToString(),
                staleBefore,
                staleAfter = brain.ExternalIntentStaleCompletionCount,
                rejectedBefore,
                rejectedAfter = brain.ExternalIntentRejectedCount,
                ledger = outcome.Ledger.CaptureGameplayOutcomes()
            }));
    }

    private static void VerifyBreakdownTargetReplacement(
        ICollection<string> evidence)
    {
        using var outcome = new OutcomeFixture("run:a04-target-replaced");
        using var fixture = new BreakdownFixture(
            "target-replaced",
            "character:a04-target-replaced",
            "building:a04-target-original",
            outcome.Command);
        fixture.BeginMovement();
        fixture.Building.isDestroy = true;
        BuildableObject replacement = fixture.CreateCompetingTarget(
            "building:a04-target-replacement");
        Require(fixture.ResolveCurrentVandalismTarget() == replacement,
            "Replacement facility is not the current valid production target.");
        string originalBefore = JsonUtility.ToJson(fixture.Building.FacilityState);
        string replacementBefore = JsonUtility.ToJson(replacement.FacilityState);
        fixture.ResumeAfterMovement();
        string originalAfter = JsonUtility.ToJson(fixture.Building.FacilityState);
        string replacementAfter = JsonUtility.ToJson(replacement.FacilityState);
        Require(!fixture.Building.IsDamaged
                && !replacement.IsDamaged
                && fixture.Building.LastVandalism.IsEmpty
                && replacement.LastVandalism.IsEmpty
                && originalAfter == originalBefore
                && replacementAfter == replacementBefore
                && outcome.Count == 0,
            "Stale movement damaged the original or silently switched targets.");
        evidence.Add("target-change-observation="
            + Newtonsoft.Json.JsonConvert.SerializeObject(new
            {
                actorId = fixture.Actor.Identity.PersistentId,
                originalTargetId = fixture.Building.RequirePersistentInstanceId().Value,
                replacementTargetId = replacement.RequirePersistentInstanceId().Value,
                originalBefore,
                originalAfter,
                replacementBefore,
                replacementAfter,
                ledger = outcome.Ledger.CaptureGameplayOutcomes()
            }));
    }

    private static CharacterVandalismCommand Transaction(
        RecordingFacts facts) => new(
        facts,
        new FacilityVandalismPublicationOutbox(
            new DungeonRuntimeAggregateRootStore(),
            facts, clock: new DungeonStory.Foundation.UnityUiClock()),
        new BuildingStructuralIntegrityRuntime(),
        EditorFixedGameCalendar.Instance);

    private static string Fact(
        GameplayOutcomeSnapshot snapshot,
        GameplayOutcomeFactId factId) => snapshot.facts.Single(value =>
        string.Equals(value.factId, factId.Value, StringComparison.Ordinal)).value;

    private static void WriteReport(
        string status,
        IEnumerable<string> evidence,
        string error)
    {
        string relative = Environment.GetEnvironmentVariable(
            ReportEnvironmentVariable);
        if (string.IsNullOrWhiteSpace(relative))
            relative = DefaultReportPath;
        relative = relative.Trim().Replace('\\', '/');
        if (Path.IsPathRooted(relative)
            || relative.Contains("../", StringComparison.Ordinal)
            || relative.Contains("/..", StringComparison.Ordinal)
            || !relative.StartsWith("Artifacts/QA/", StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "A04 report path must stay under Artifacts/QA.");
        }
        string absolute = Path.GetFullPath(Path.Combine(
            Application.dataPath,
            "..",
            relative));
        Directory.CreateDirectory(Path.GetDirectoryName(absolute)
            ?? throw new InvalidOperationException(
                "A04 report directory is missing."));
        var lines = new List<string>
        {
            "status=" + status,
            "humanApprovalClaimed=false",
            "trainingEligible=false"
        };
        lines.AddRange(evidence.Select(value => "evidence=" + value));
        if (!string.IsNullOrWhiteSpace(error))
            lines.Add("error=" + error.Replace('\r', ' ').Replace('\n', ' '));
        File.WriteAllLines(absolute, lines, new UTF8Encoding(false));
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException("A04: " + message);
    }

    private sealed class OutcomeFixture : IDisposable
    {
        private readonly DomainFactDispatcher facts;

        internal OutcomeFixture(string runId, bool? loseReplyAfterCommit = null)
        {
            GameplayOutcomeRegistry registry = new(
                new IGameplayOutcomeDescriptor[]
                {
                    new FacilityVandalismOutcomeDescriptor(
                        new KoreanJosaFormatter())
                },
                new IGameplayOutcomeAdapterRegistration[]
                {
                    new FacilityVandalismOutcomeAdapter()
                });
            Ledger = new GameplayOutcomeLedger(
                registry,
                GameplayOutcomeBufferLimits.Default,
                new GameplayOutcomeRunId(runId),
                1L);
            var recorder = new GameplayOutcomeRecorder(
                Ledger,
                registry,
                new GameEventBus());
            facts = new DomainFactDispatcher();
            facts.Subscribe(
                "test:facility-vandalism",
                new GameplayDomainFactObserver<
                    FacilityVandalismFact,
                    FacilityVandalismOutcomeReceipt>(
                    FacilityVandalismFactMapping.Capture,
                    registry,
                    Ledger,
                    recorder,
                    new GameplayDomainFactCommitGroup(recorder, Ledger)));
            Publications = new FacilityVandalismPublicationOutbox(
                new DungeonRuntimeAggregateRootStore(),
                facts, clock: new DungeonStory.Foundation.UnityUiClock());
            Command = new CharacterVandalismCommand(
                loseReplyAfterCommit.HasValue ? new LostReplyPublisher(facts, loseReplyAfterCommit.Value) : facts,
                Publications,
                new BuildingStructuralIntegrityRuntime(),
                EditorFixedGameCalendar.Instance);
        }

        internal GameplayOutcomeLedger Ledger { get; }
        internal FacilityVandalismPublicationOutbox Publications { get; }
        internal ICharacterVandalismCommand Command { get; }
        internal int Count => Ledger.CaptureGameplayOutcomes()
            .exactOutcomes.Count(value => value != null
                && value.outcomeTypeId
                    == FacilityVandalismOutcomeIds.FacilityDamaged.Value);
        internal GameplayOutcomeSnapshot Single() => Ledger
            .CaptureGameplayOutcomes().exactOutcomes.Single(value =>
                value != null
                && value.outcomeTypeId
                    == FacilityVandalismOutcomeIds.FacilityDamaged.Value);
        public void Dispose()
        {
            facts.Dispose();
        }
    }

    private sealed class LostReplyPublisher : IDomainFactStagingPublisher
    {
        private readonly IDomainFactStagingPublisher inner;
        private readonly bool afterCommit;
        private bool injected;
        internal LostReplyPublisher(IDomainFactStagingPublisher inner, bool afterCommit)
        { this.inner = inner; this.afterCommit = afterCommit; }
        public bool TryStage<TFact>(in TFact fact, out IDomainFactStage stage, out string failure)
        {
            if (!inner.TryStage(fact, out stage, out failure)) return false;
            if (!injected) { injected = true; stage = new LostReplyStage(stage, afterCommit); }
            return true;
        }
        private sealed class LostReplyStage : IDomainFactStage
        {
            private readonly IDomainFactStage inner;
            private readonly bool afterCommit;
            internal LostReplyStage(IDomainFactStage inner, bool afterCommit) { this.inner = inner; this.afterCommit = afterCommit; }
            public string OperationKey => inner.OperationKey;
            public long OwnerRevision => inner.OwnerRevision;
            public bool IsReplay => inner.IsReplay;
            public bool IsActive => inner.IsActive;
            public DomainFactDelivery Commit()
            {
                if (afterCommit) Require(inner.Commit().Completed, "Fault injection expected a completed real publication.");
                else inner.Cancel();
                return DomainFactDelivery.Uncertain("a04-test-lost-reply");
            }
            public void Cancel() => inner.Cancel();
        }
    }

    private sealed class RecordingFacts :
        IDomainFactStagingPublisher,
        IDomainFactRecoveryPublisher
    {
        private readonly HashSet<string> committed = new(StringComparer.Ordinal);
        internal bool RejectNextCommit { get; set; }
        internal int PrepareCount { get; private set; }
        internal int SuccessfulResultCount => committed.Count;

        public bool TryStage<TFact>(
            in TFact fact,
            out IDomainFactStage stage,
            out string failure)
        {
            PrepareCount++;
            if (fact is not FacilityVandalismFact vandalism)
            {
                stage = null;
                failure = "a04-unexpected-domain-fact";
                return false;
            }
            FacilityVandalismCommitState snapshot = vandalism.Snapshot();
            string publicationKey = "qa:facility-vandalism:"
                + snapshot.facilityId + ":" + snapshot.operationSequence;
            stage = new RecordingStage(
                this,
                publicationKey,
                snapshot.operationSequence,
                committed.Contains(publicationKey));
            failure = string.Empty;
            return true;
        }

        public bool ReplayBatch(
            IReadOnlyList<DomainFactReplay> facts,
            DomainFactDelivery[] results,
            out string failure)
        {
            if (facts == null || results == null || facts.Count != results.Length)
                throw new ArgumentException("Invalid A04 replay batch.");
            bool complete = true;
            for (int index = 0; index < facts.Count; index++)
            {
                string key = facts[index]?.PublicationKey ?? string.Empty;
                if (committed.Contains(key))
                    results[index] = Delivered(key);
                else
                {
                    complete = false;
                    results[index] = DomainFactDelivery.Reject(
                        "a04-publication-not-committed");
                }
            }
            failure = complete ? string.Empty : "a04-publication-not-committed";
            return complete;
        }

        private DomainFactDelivery Commit(string publicationKey)
        {
            if (RejectNextCommit)
            {
                RejectNextCommit = false;
                return DomainFactDelivery.Reject(
                    "a04-injected-commit-rejection");
            }
            committed.Add(publicationKey);
            return Delivered(publicationKey);
        }

        private static DomainFactDelivery Delivered(string publicationKey) =>
            new(
                DomainFactDeliveryPhase.Delivered,
                publicationKey,
                "qa:facility-vandalism-publication",
                new string('a', 64),
                string.Empty);

        private sealed class RecordingStage : IDomainFactStage
        {
            private readonly RecordingFacts owner;
            private bool active = true;

            internal RecordingStage(
                RecordingFacts owner,
                string operationKey,
                long ownerRevision,
                bool replay)
            {
                this.owner = owner;
                OperationKey = operationKey;
                OwnerRevision = ownerRevision;
                IsReplay = replay;
            }

            public string OperationKey { get; }
            public long OwnerRevision { get; }
            public bool IsReplay { get; }
            public bool IsActive => active;

            public DomainFactDelivery Commit()
            {
                if (!active)
                    return DomainFactDelivery.Reject(
                        "a04-publication-stage-inactive");
                active = false;
                return owner.Commit(OperationKey);
            }

            public void Cancel() => active = false;
        }
    }

    private sealed class DomainFixture : IDisposable
    {
        private readonly GameObject actorObject;
        private readonly GameObject buildingObject;
        private readonly CharacterSO actorDefinition;
        private readonly List<GameObject> additionalBuildingObjects = new();

        internal DomainFixture(
            string suffix,
            string actorId,
            string buildingId,
            Type buildingType = null)
        {
            actorObject = CharacterAiPlanDebugFixtures.CreateActorObject(
                "A04 Actor " + suffix);
            actorDefinition = CharacterAiPlanDebugFixtures.CreateCharacterData(
                CharacterType.NPC,
                "A04 Actor " + suffix,
                "Orc");
            Actor = actorObject.GetComponent<CharacterActor>();
            Actor.EnsureRuntimeState();
            Actor.Identity.SetPersistentId(actorId);
            Actor.Initialization(actorDefinition);
            Actor.SetLifecycleState(CharacterLifecycleState.Active);

            Definition = CharacterAiPlanDebugFixtures.CreateBuildingData(
                984000 + StableSuffix(suffix),
                "A04 Facility " + suffix);
            Type componentType = buildingType ?? typeof(BuildableObject);
            if (!typeof(BuildableObject).IsAssignableFrom(componentType))
            {
                throw new ArgumentException(
                    "A04 facility fixture requires a BuildableObject component type.",
                    nameof(buildingType));
            }
            buildingObject = new GameObject(
                "A04 Facility " + suffix,
                componentType);
            Building = buildingObject.GetComponent<BuildableObject>();
            CharacterAiEditorTestDependencies.Inject(Building);
            Building.RestorePersistentIdentity(
                new BuildingInstanceId(buildingId));
            Building.Initialization(Definition, Vector2Int.zero);
        }

        internal CharacterActor Actor { get; }
        internal BuildableObject Building { get; }
        internal BuildingSO Definition { get; }

        internal BuildableObject CreateAdditionalBuilding(string buildingId)
        {
            var additional = new GameObject("A04 Replacement Facility",
                typeof(BuildableObject));
            additionalBuildingObjects.Add(additional);
            BuildableObject building = additional.GetComponent<BuildableObject>();
            CharacterAiEditorTestDependencies.Inject(building);
            building.RestorePersistentIdentity(new BuildingInstanceId(buildingId));
            building.Initialization(Definition, Vector2Int.zero);
            return building;
        }

        public void Dispose()
        {
            foreach (GameObject additional in additionalBuildingObjects)
                if (additional != null)
                    UnityEngine.Object.DestroyImmediate(additional);
            if (actorObject != null)
                UnityEngine.Object.DestroyImmediate(actorObject);
            if (buildingObject != null)
                UnityEngine.Object.DestroyImmediate(buildingObject);
            if (actorDefinition != null)
                UnityEngine.Object.DestroyImmediate(actorDefinition);
            if (Definition != null)
                UnityEngine.Object.DestroyImmediate(Definition);
        }

        private static int StableSuffix(string value)
        {
            unchecked
            {
                int hash = 17;
                foreach (char character in value ?? string.Empty)
                    hash = hash * 31 + character;
                return Math.Abs(hash % 1000);
            }
        }
    }

    private sealed class BreakdownFixture : IDisposable
    {
        private readonly DomainFixture domain;
        private readonly CharacterBreakdownActionRunner runner;
        private readonly CharacterActionIntentLease intentLease;
        private readonly DomainFactDispatcher notifications;
        private IEnumerator routine;
        private IEnumerator movement;

        internal BreakdownFixture(
            string suffix,
            string actorId,
            string buildingId,
            ICharacterVandalismCommand vandalismCommands)
        {
            domain = new DomainFixture(suffix, actorId, buildingId);
            var grid = new Grid(3, 3);
            grid.SetAreaType(Vector2Int.zero, GridCellAreaType.ExteriorPath);
            var gridProvider = new FixedGridSystemProvider(grid);
            WorldItemStackRuntime itemStacks = PhysicalItemDebugScenarios
                .CreateRuntimeForCrossDomainFixture(
                    gridProvider,
                    out _,
                    out _,
                    out ItemQuantityReservationService quantityReservations,
                    out _);
            var migrated = new MigratedProducerOutcomeEditorFixture(
                "run:a04-breakdown-support:" + suffix);
            var stateStore = new CharacterDeprivationStateStore(
                migrated.AggregateRootStore);
            var commits = new GameplayDomainFactCommitGroup(
                migrated.Recorder,
                migrated.Ledger);
            notifications = new DomainFactDispatcher(
                new IDomainFactObserverBinding[]
                {
                    new CharacterDeprivationExperienceFactBinding(),
                    new CharacterWaterFactBinding(
                        new CharacterWaterFactObserver(
                            migrated.Transaction,
                            commits))
                });
            var events = new GameEventBus();
            var waterPublications = new CharacterWaterPublicationOutbox(
                new DungeonStory.Foundation.UnityUiClock(),
                stateStore,
                notifications,
                notifications,
                events);
            var diagnostics = new CharacterDeprivationDiagnostics();
            var world = new CharacterBreakdownWorld(
                new DungeonStory.Foundation.UnityUiClock(),
                gridProvider,
                itemStacks,
                EmptyWorldFilthQuery.Instance,
                EmptyWorldWaterQuery.Instance,
                new RoomLayoutCache(),
                CharacterAiEditorTestDependencies.WorldRegistry);
            var safeDrinkPlanner = new CharacterSafeDrinkPlanner(
                gridProvider,
                itemStacks,
                EmptyWorldWaterQuery.Instance,
                quantityReservations,
                OpenDoorAccessQuery.Instance,
                SafeEnvironmentWorkPolicy.Instance,
                diagnostics);
            var consequences = new CharacterDeprivationConsequences(
                stateStore,
                CharacterAiEditorTestDependencies.WorldRegistry,
                EditorFixedGameCalendar.Instance,
                notifications);
            runner = new CharacterBreakdownActionRunner(
                world,
                new CharacterBreakdownActionPolicyDependencies(
                    FixedRandomStream.Instance,
                    DefaultCharacterNeedBalanceRuntime.Instance,
                    new ResourceItemDefinitionCatalog(
                        Resources.LoadAll<ItemDefinitionSO>(
                            ItemDefinitionSO.UnifiedResourcePath)),
                    CharacterAiEditorTestDependencies.BodyHealthRuntime,
                    CharacterAiEditorTestDependencies.NeutralPerformance),
                new CharacterBreakdownActionExecutionDependencies(
                    stateStore,
                    safeDrinkPlanner,
                    new CharacterEmergencyMovement(gridProvider, stateStore),
                    diagnostics,
                    consequences,
                    EmptyFieldMealConsumption.Instance,
                    EditorFixedGameCalendar.Instance,
                    waterPublications,
                    vandalismCommands));

            Actor.Brain.ConstructCharacterAbility(gridProvider);
            CharacterDeprivationState state = stateStore.Ensure(Actor);
            state.breakdownGeneration = 1;
            state.dispatchedBreakdownGeneration = 1;
            state.breakdown = new CharacterBreakdownState
            {
                active = true,
                cause = DeprivationKind.MentalInstability,
                kind = CharacterBreakdownKind.ViolentImpulse,
                startedAt = 0f
            };
            Require(Actor.Brain.TryBeginExternallyDrivenAction(
                    CharacterBreakdownActionRunner.IntentOwnerId,
                    CharacterActionIntentKind.Breakdown,
                    "결핍 붕괴",
                    "난동",
                    "A04",
                    out intentLease),
                "Violent-breakdown fixture could not acquire action intent.");
        }

        internal CharacterActor Actor => domain.Actor;
        internal BuildableObject Building => domain.Building;

        internal BuildableObject CreateCompetingTarget(string buildingId) =>
            domain.CreateAdditionalBuilding(buildingId);

        internal BuildableObject ResolveCurrentVandalismTarget()
        {
            MethodInfo method = typeof(CharacterBreakdownActionRunner).GetMethod(
                    "TryFindVandalismTarget",
                    BindingFlags.Instance | BindingFlags.NonPublic)
                ?? throw new InvalidOperationException(
                    "The production vandalism target query is missing.");
            object[] arguments = { Actor, null };
            Require(method.Invoke(runner, arguments) is true,
                "The production vandalism target query found no target.");
            return arguments[1] as BuildableObject
                ?? throw new InvalidOperationException("The target query returned null.");
        }

        internal void CancelMovementIntent()
        {
            Require(routine != null && movement != null
                    && Actor.Brain.IsExternalIntentCurrent(intentLease),
                "Movement cancellation has no current intent lease.");
            Require(Actor.Brain.CancelExternallyDrivenAction(intentLease)
                    && !Actor.Brain.IsExternalIntentCurrent(intentLease),
                "The production cancellation did not release the exact lease.");
        }

        internal void RunMovementThenResume()
        {
            BeginMovement();
            ResumeAfterMovement();
        }

        internal void BeginMovement()
        {
            Require(routine == null, "Violent-breakdown routine already started.");
            MethodInfo method = typeof(CharacterBreakdownActionRunner).GetMethod(
                    "RunViolentImpulse",
                    BindingFlags.Instance | BindingFlags.NonPublic)
                ?? throw new InvalidOperationException(
                    "The production violent-breakdown routine is missing.");
            routine = method.Invoke(runner, new object[] { Actor, intentLease })
                as IEnumerator
                ?? throw new InvalidOperationException(
                    "The production violent-breakdown routine is not executable.");
            Require(routine.MoveNext() && routine.Current is IEnumerator,
                "Violent breakdown did not reach its movement boundary.");
            movement = (IEnumerator)routine.Current;
        }

        internal void ResumeAfterMovement()
        {
            Require(routine != null && movement != null,
                "Violent-breakdown movement was not started.");
            Drain(movement);
            movement = null;
            _ = routine.MoveNext();
        }

        public void Dispose()
        {
            (movement as IDisposable)?.Dispose();
            (routine as IDisposable)?.Dispose();
            if (Actor != null)
            {
                runner.ReleaseActor(CharacterPersistentIdentity.Require(Actor));
                if (Actor.Brain != null
                    && Actor.Brain.IsExternalIntentCurrent(intentLease))
                    Actor.Brain.EndExternallyDrivenAction(
                        intentLease,
                        clearFailures: true);
            }
            notifications.Dispose();
            domain.Dispose();
        }

        private static void Drain(IEnumerator root)
        {
            var stack = new Stack<IEnumerator>();
            stack.Push(root);
            while (stack.Count > 0)
            {
                IEnumerator current = stack.Peek();
                if (!current.MoveNext())
                {
                    (stack.Pop() as IDisposable)?.Dispose();
                    continue;
                }
                if (current.Current is IEnumerator nested)
                    stack.Push(nested);
            }
        }
    }

    private sealed class FixedGridSystemProvider : IGridSystemProvider
    {
        internal FixedGridSystemProvider(Grid grid) =>
            Grid = grid ?? throw new ArgumentNullException(nameof(grid));
        public GridSystemManager Manager => null;
        public Grid Grid { get; }
        public bool TryGetManager(out GridSystemManager manager)
        {
            manager = null;
            return false;
        }
        public bool TryGetGrid(out Grid grid)
        {
            grid = Grid;
            return true;
        }
    }

    private sealed class FixedRandomStream : IRandomStream
    {
        internal static readonly FixedRandomStream Instance = new();
        public ulong State => 1UL;
        public int NextInt(int minInclusive, int maxExclusive) => minInclusive;
        public float NextFloat() => 0f;
        public bool Chance(float probability) => probability > 0f;
        public void Restore(ulong state)
        {
        }
    }

    private sealed class OpenDoorAccessQuery : IDoorAccessQuery
    {
        internal static readonly OpenDoorAccessQuery Instance = new();
        public int DoorAccessVersion => 0;
        public DoorAccessSubjectRef ResolveSubject(
            GridTraversalContext context) => default;
        public bool CanUse(
            Door door,
            GridTraversalContext context,
            out string denialReason)
        {
            denialReason = string.Empty;
            return true;
        }
        public bool CanTraverse(
            Grid grid,
            Vector2Int position,
            GridTraversalContext context,
            out string denialReason)
        {
            denialReason = string.Empty;
            return true;
        }
    }

    private sealed class SafeEnvironmentWorkPolicy : IEnvironmentWorkPolicy
    {
        internal static readonly SafeEnvironmentWorkPolicy Instance = new();
        private static WorkEnvironmentAssessment Safe => new(
            canStart: true,
            needsProtection: false,
            projectedExposure: 0f,
            workSpeedMultiplier: 1f,
            failure: DomainFailure.None);
        public WorkEnvironmentAssessment Assess(
            CharacterActor actor,
            Vector2Int destination,
            float expectedSeconds,
            EnvironmentalWorkKind workKind,
            bool forced) => Safe;
        public WorkEnvironmentAssessment AssessStart(
            CharacterActor actor,
            Vector2Int destination,
            IReadOnlyList<GridMoveStep> route,
            float expectedSeconds,
            EnvironmentalWorkKind workKind,
            bool forced) => Safe;
        public WorkEnvironmentAssessment PrepareActiveWork(
            CharacterActor actor,
            Vector2Int destination,
            IReadOnlyList<GridMoveStep> route,
            float expectedSeconds,
            EnvironmentalWorkKind workKind,
            bool forced) => Safe;
        public WorkEnvironmentAssessment RecheckActive(
            CharacterActor actor,
            Vector2Int currentPosition,
            float remainingSeconds,
            EnvironmentalWorkKind workKind,
            bool forced) => Safe;
        public bool TryFindEvacuationCell(
            CharacterActor actor,
            Grid grid,
            out Vector2Int destination,
            out bool fullySafe,
            out DomainFailure failure)
        {
            destination = actor != null ? actor.GetNowXY() : Vector2Int.zero;
            fullySafe = true;
            failure = DomainFailure.None;
            return true;
        }
    }

    private sealed class EmptyFieldMealConsumption :
        IFieldMealConsumptionCommand
    {
        internal static readonly EmptyFieldMealConsumption Instance = new();
        public bool TryFindFieldMeal(
            CharacterActor actor,
            out ItemStackId stackId,
            out Vector2Int position,
            out CharacterConsumablesFailure failure)
        {
            stackId = default;
            position = default;
            failure = default;
            return false;
        }
        public bool TryConsumeFieldMeal(
            CharacterActor actor,
            ItemStackId stackId,
            out MealConsumptionResult result)
        {
            result = default;
            return false;
        }
    }

    private sealed class EmptyWorldFilthQuery : IWorldFilthQuery
    {
        internal static readonly EmptyWorldFilthQuery Instance = new();
        public int StateVersion => 0;
        public int NextFilthSequence => 1;
        public IReadOnlyList<WorldFilthSnapshot> GetAll() =>
            Array.Empty<WorldFilthSnapshot>();
        public IReadOnlyList<WorldFilthSnapshot> GetAt(Vector2Int position) =>
            Array.Empty<WorldFilthSnapshot>();
        public WorldFilthSnapshot AddFilth(
            WorldFilthType type,
            Vector2Int position,
            float amount,
            string sourceCharacterId,
            float infectionRisk,
            bool wallStain = false) => new(
                "filth:a04:" + type,
                type,
                amount,
                position,
                sourceCharacterId,
                infectionRisk,
                wallStain);
        public bool Clean(
            string filthId,
            float workAmount,
            out float remainingAmount)
        {
            remainingAmount = 0f;
            return false;
        }
        public float GetCleanlinessPenalty(
            Vector2Int position,
            int radius = 0) => 0f;
        public List<WorldFilthSaveData> CaptureFilth() => new();
        public void RestoreFilth(
            IEnumerable<WorldFilthSaveData> saveData,
            int nextSequence)
        {
        }
    }

    private sealed class EmptyWorldWaterQuery : IWorldWaterQuery
    {
        internal static readonly EmptyWorldWaterQuery Instance = new();
        public int NextWaterSequence => 1;
        public IReadOnlyList<WorldWaterSourceSnapshot> GetAllSources() =>
            Array.Empty<WorldWaterSourceSnapshot>();
        public bool TryGetSource(
            string sourceId,
            out WorldWaterSourceSnapshot source)
        {
            source = default;
            return false;
        }
        public bool TryFindDrinkSource(
            Vector2Int origin,
            bool allowFoul,
            out WorldWaterSourceSnapshot source)
        {
            source = default;
            return false;
        }
        public bool TryDrink(
            string sourceId,
            float amount,
            out WorldWaterQuality quality,
            out float consumed)
        {
            quality = default;
            consumed = 0f;
            return false;
        }
        public List<WorldWaterSourceSaveData> CaptureWaterSources() => new();
        public void RestoreWaterSources(
            IEnumerable<WorldWaterSourceSaveData> saveData,
            int nextSequence)
        {
        }
        public bool DebugCreateSource(
            Vector2Int position,
            WorldWaterQuality quality,
            float capacity,
            GridCellTerrainType terrainType,
            out string sourceId)
        {
            sourceId = string.Empty;
            return false;
        }
        public bool DebugSetSource(
            string sourceId,
            WorldWaterQuality quality,
            float capacity,
            float remaining) => false;
    }
}
#endif
