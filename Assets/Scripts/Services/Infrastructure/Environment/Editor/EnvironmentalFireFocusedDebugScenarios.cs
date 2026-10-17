#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DungeonStory.Environment;
using DungeonStory.Foundation;
using DungeonStory.Foundation.Facts;
using DungeonStory.Narrative.Korean;
using UnityEditor;
using UnityEngine;

public static class EnvironmentalFireFocusedDebugScenarios
{
    private const string ReportPath =
        "Artifacts/QA/GameplayOutcomeLedgerPhase80FireGroup7-20260919-r6/"
        + "environmental-fire-focused.txt";

    [MenuItem("DungeonStory/QA/Run FIRE-01 Environmental Fire Focused")]
    public static void Run()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(ReportPath));
        if (File.Exists(ReportPath))
        {
            throw new InvalidOperationException(
                "Environmental-fire focused evidence is immutable and already exists: "
                + ReportPath);
        }
        try
        {
            RunAll();
            File.WriteAllText(
                ReportPath,
                "result=PASS\n"
                + "PASS electrical contact, breaker, generation and stored-power isolation gate\n"
                + "PASS exact ignition replay and 13 WU initial attack\n"
                + "PASS physical water pending receipt, current-format restore and exact acknowledgement\n"
                + "PASS strict v8 restore, terminal target/origin/deferred-ACK preservation and malformed v2/v3/v4/v5/v6/v7/origin/target/ACK rejection\n"
                + "PASS deterministic direct-neighbour spread, duplicate collapse and non-combustible rejection\n"
                + "PASS process-accident receipt producer request contract, ineligible rejection and cause replay\n"
                + "PASS fire-damage exact replay, conflict rejection, save restore, false/throw retention and retry\n"
                + "scope=isolated production EnvironmentalFireRuntime and process-accident producer with controlled target/access/power/water/command authorities; registered worker selection, hauling and scene overlay require the focused Play witness\n");
            Debug.Log("FIRE01_ENVIRONMENTAL_FIRE=PASS");
        }
        catch (Exception error)
        {
            File.WriteAllText(ReportPath, "result=FAIL\n" + error + "\n");
            Debug.LogException(error);
            throw;
        }
    }

    public static void RunAll()
    {
        VerifyElectricalIsolationAndInitialAttack();
        VerifyWaterReceiptRestoreAndAcknowledgement();
        VerifyRestoreTransactionIsolation();
        VerifyCurrentV8RestoreAndIdentityValidation();
        VerifyDeterministicAdjacentSpread();
        VerifyProcessAccidentProducerContract();
        VerifyDamageOutcomeReplayRestoreAndFailureRetry();
    }

    public static string RunDamageObserverFocused()
    {
        VerifyDamageRestoreTransactionIsolation();
        VerifyDamageOutcomeReplayRestoreAndFailureRetry();
        VerifyDamageReplyLossAndSavedReplay();
        return "fire-damage/actual-domain-authority-and-observer/rejection/unknown-before-after/saved-new-dispatcher/dedup/canonical-tamper=PASS;physical-damage-and-whole-save=NOT-RUN";
    }

    public static string RunActionObserverFocused()
    {
        VerifyElectricalIsolationAndInitialAttack();
        VerifyWaterReceiptRestoreAndAcknowledgement();
        VerifyDeterministicAdjacentSpread();
        VerifyActionReplyLoss();
        VerifyActionKnownRejection();
        return "fire-actions/actual-runtime-and-observer/ignition-suppression/unknown-before-after/saved-new-dispatcher/exact-request/dedup=PASS;water-port-controlled;Unity-scene-and-whole-save=NOT-RUN";
    }

    public static string RunTemporalJoinForBatch()
    {
        using var outcomes = new OutcomeFixture(includeDamage: true,
            deliveryCalendar: new EditorFixedGameCalendar(23, 3));
        var fixture = new Fixture(false, 0.4f, outcomes: outcomes);
        Require(fixture.Ignite("cause:clocked-save-join").Created,
            "Clocked join fixture did not execute real ignition.");
        var damage = new EnvironmentalFireDamageAuthority(outcomes.Facts, outcomes.Facts);
        var command = DamageCommand("fire-damage:clocked-save-join");
        Require(damage.TryBegin(command, "시각 검사 시설", 23, out _, out _)
            && damage.TryCommitApplied(command.OperationId, 7, true, out var result, out _)
            && result.Committed, "Clocked join fixture did not commit its real damage receipt.");
        var owner = fixture.Runtime.Capture();
        var ledger = JsonUtility.FromJson<GameplayOutcomeLedgerSaveData>(
            JsonUtility.ToJson(outcomes.Ledger.CaptureGameplayOutcomes()));
        Require(ledger.exactOutcomes.Count == 2
            && ledger.exactOutcomes.All(value => value.deliveryMetadataVersion
                == GameplayOutcomeDeliveryTime.CurrentVersion && value.publishedRevision > 0L),
            "Clocked join fixture did not observe actual publication time.");
        string unchanged = JsonUtility.ToJson(fixture.Runtime.Capture());
        Verify(ledger, true);
        Action<GameplayOutcomeSnapshot>[] corruptions =
        {
            value => value.recordedAbsoluteHour = -1L,
            value => value.publishedRevision = checked(ledger.revision + 1L),
            value => value.deliveryDelayReason = "fabricated-delay",
            value => value.deliveryMetadataVersion = 0,
            value => { value.parentRunId = ledger.runId; value.parentSequence = ledger.nextSequence; }
        };
        foreach (var corrupt in corruptions)
        {
            var changed = JsonUtility.FromJson<GameplayOutcomeLedgerSaveData>(JsonUtility.ToJson(ledger));
            foreach (var row in changed.exactOutcomes) corrupt(row);
            Verify(changed, false);
            Verify(ledger, true);
            Require(JsonUtility.ToJson(fixture.Runtime.Capture()) == unchanged,
                "Rejected temporal join changed the live fire owner.");
        }
        using var restored = new OutcomeFixture(includeDamage: true,
            deliveryCalendar: new EditorFixedGameCalendar(23, 3));
        RestoreActionLedger(restored, ledger);
        var resumed = new Fixture(false, 0.4f, outcomes: restored);
        resumed.Runtime.Restore(resumed.Runtime.PrepareRestore(owner));
        Require(resumed.Ignite("cause:clocked-save-join").Disposition
                == EnvironmentalFireIgnitionDisposition.PreviouslyProcessed
            && restored.Ledger.CaptureGameplayOutcomes().exactOutcomes.Count == 2,
            "Clocked restore/retry duplicated the original ignition outcome.");
        RunActionObserverFocused();
        RunDamageObserverFocused();
        EnvironmentalFireResourceObserverDebugScenarios.Run();
        return "ARCHITECTURE_FIRE_TEMPORAL_JOIN=PASS; clocked ignition/damage; temporal/parent corruption rejection unchanged; restored same-operation retry; action/damage/resource observer recovery";

        void Verify(GameplayOutcomeLedgerSaveData candidate, bool expected)
        {
            var actionReport = new DungeonGameRestoreReport();
            EnvironmentalFireActionFactPreflight.Validate(owner, candidate, actionReport);
            var damageReport = new DungeonGameRestoreReport();
            EnvironmentalFireDamageFactPreflight.Validate(damage.Capture(), candidate, damageReport);
            Require(actionReport.Success == expected && damageReport.Success == expected,
                "Fire temporal join expected=" + expected
                + ";action=" + string.Join(" | ", actionReport.Errors)
                + ";damage=" + string.Join(" | ", damageReport.Errors));
        }
    }

    private static void VerifyActionReplyLoss()
    {
        foreach (var mode in new[] { ControlledCommitMode.Throw, ControlledCommitMode.HideAfterCommit })
        {
            using var outcomes = new OutcomeFixture();
            var fixture = new Fixture(false, 0.8f, outcomes: outcomes, maximumIntensity: 0.6f);
            fixture.StageFaults.Mode = mode;
            RequireThrows(() => fixture.Ignite("cause:reply-loss"), "Ignition reply loss was hidden.");
            var saved = JsonUtility.FromJson<DungeonEnvironmentalFireSaveData>(JsonUtility.ToJson(fixture.Runtime.Capture()));
            var ledgerSave = JsonUtility.FromJson<GameplayOutcomeLedgerSaveData>(JsonUtility.ToJson(outcomes.Ledger.CaptureGameplayOutcomes()));
            Require(saved.activeFires.Count == 1 && saved.processedCauses.Single().outcomePending
                && saved.processedCauses[0].requestedIntensity == 0.8f && saved.processedCauses[0].appliedIntensity == 0.6f,
                "Unknown ignition did not retain the original request and actual intensity.");
            RequireActionJoin(saved, ledgerSave, true);
            using var restoredOutcomes = new OutcomeFixture();
            RestoreActionLedger(restoredOutcomes, ledgerSave);
            var restored = new Fixture(false, 0.8f, outcomes: restoredOutcomes, maximumIntensity: 0.6f);
            restored.Runtime.Restore(restored.Runtime.PrepareRestore(saved));
            Require(restored.Ignite("cause:reply-loss").Disposition == EnvironmentalFireIgnitionDisposition.PreviouslyProcessed
                && restored.PublishedOutcomeCount == 1 && restored.Runtime.ActiveFires.Single().Intensity == 0.6f
                && !restored.Runtime.Capture().processedCauses.Single().outcomePending,
                "Restored ignition repeated its effect or failed exact publication replay.");
            var delivered = restored.Runtime.Capture();
            var canonical = restoredOutcomes.Ledger.CaptureGameplayOutcomes();
            RequireActionJoin(delivered, canonical, true);
            var changed = JsonUtility.FromJson<DungeonEnvironmentalFireSaveData>(JsonUtility.ToJson(delivered));
            var cause = changed.processedCauses.Single();
            cause.requestedIntensity = 0.7f;
            cause.fingerprint = new EnvironmentalFireIgnitionRequest(cause.causeId,
                (EnvironmentalFireIgnitionKind)cause.ignitionKind, cause.producerId,
                new EnvironmentalFireTargetRef((EnvironmentalFireTargetKind)cause.targetKind, cause.targetId),
                cause.requestedIntensity, cause.evidenceId, targetDisplayName: cause.targetDisplayName).Fingerprint;
            RequireActionJoin(changed, canonical, false);
            changed = JsonUtility.FromJson<DungeonEnvironmentalFireSaveData>(JsonUtility.ToJson(delivered));
            changed.processedCauses[0].publicationKey += ":changed";
            RequireActionJoin(changed, canonical, false);
            var raw = Newtonsoft.Json.Linq.JObject.Parse(JsonUtility.ToJson(delivered));
            ((Newtonsoft.Json.Linq.JObject)raw["processedCauses"][0]).Remove("requestedIntensity");
            RequireThrows(() => EnvironmentalFireSaveSection.ValidateActionPublicationJson(raw), "Absent original request was defaulted.");

            foreach (bool water in new[] { false, true })
            {
                using var source = new OutcomeFixture();
                var active = new Fixture(false, water ? 0.8f : 0.25f, outcomes: source);
                var ignition = active.Ignite("cause:suppression-reply");
                var command = water ? new EnvironmentalFireSuppressionCommand("suppression:reply", ignition.FireId,
                    "character:worker", new Vector2Int(1, 0), EnvironmentalFireSuppressionMode.Water, 2.5f,
                    "lease:reply", 2, "진압 작업자") : active.InitialAttack("suppression:reply", ignition.FireId, 13);
                active.StageFaults.Mode = mode;
                RequireThrows(() => active.Runtime.TryApplySuppression(command), "Suppression reply loss was hidden.");
                var pending = JsonUtility.FromJson<DungeonEnvironmentalFireSaveData>(JsonUtility.ToJson(active.Runtime.Capture()));
                var pendingLedger = JsonUtility.FromJson<GameplayOutcomeLedgerSaveData>(JsonUtility.ToJson(source.Ledger.CaptureGameplayOutcomes()));
                Require(pending.activeFires.Count == 0 && pending.history.Count == 1
                    && pending.suppressionOperations.Single().phase == (int)EnvironmentalFireSuppressionPhase.Applied
                    && pending.suppressionOperations.Single().outcomePending
                    && active.Water.CommitCount == (water ? 1 : 0), "Unknown suppression rolled back irreversible work or consumed water twice.");
                RequireActionJoin(pending, pendingLedger, true);
                using var destination = new OutcomeFixture();
                RestoreActionLedger(destination, pendingLedger);
                var recovered = new Fixture(false, water ? 0.8f : 0.25f, active.Water, outcomes: destination);
                recovered.Runtime.Restore(recovered.Runtime.PrepareRestore(pending));
                recovered.Runtime.ResumePendingSuppressions();
                recovered.Runtime.TryApplySuppression(command);
                var after = recovered.Runtime.Capture();
                Require(recovered.PublishedOutcomeCount == 2 && active.Water.CommitCount == (water ? 1 : 0)
                    && after.history.Single().fire.totalSuppressionWork == command.ApprovedWork
                    && after.history.Single().fire.totalWaterConsumed == (water ? 2 : 0)
                    && !after.suppressionOperations.Single().outcomePending
                    && !after.suppressionOperations.Single().completionIdentityAcknowledged,
                    "Restored suppression duplicated work/water/log or fabricated absent completion observer acknowledgement.");
                RequireActionJoin(after, destination.Ledger.CaptureGameplayOutcomes(), true);
                after.suppressionOperations[0].intensityBefore -= 0.01f;
                RequireActionJoin(after, destination.Ledger.CaptureGameplayOutcomes(), false);
            }
        }
    }

    private static void VerifyActionKnownRejection()
    {
        using var source = new OutcomeFixture();
        var fixture = new Fixture(false, 0.25f, outcomes: source);
        fixture.StageFaults.Mode = ControlledCommitMode.Fail;
        string before = JsonUtility.ToJson(fixture.Runtime.Capture());
        RequireThrows(() => fixture.Ignite("cause:rejected"), "Known ignition rejection was hidden.");
        Require(JsonUtility.ToJson(fixture.Runtime.Capture()) == before && fixture.PublishedOutcomeCount == 0,
            "Known ignition rejection left a partial domain mutation.");
        fixture.StageFaults.Mode = ControlledCommitMode.Success;
        var ignition = fixture.Ignite("cause:accepted");
        var command = fixture.InitialAttack("suppression:rejected", ignition.FireId, 13);
        before = JsonUtility.ToJson(fixture.Runtime.Capture());
        fixture.StageFaults.Mode = ControlledCommitMode.Fail;
        RequireThrows(() => fixture.Runtime.TryApplySuppression(command), "Known suppression rejection was hidden.");
        Require(JsonUtility.ToJson(fixture.Runtime.Capture()) == before && fixture.PublishedOutcomeCount == 1,
            "Known reversible suppression rejection retained effects or an outcome.");
    }

    private static void RequireActionJoin(DungeonEnvironmentalFireSaveData save, GameplayOutcomeLedgerSaveData ledger, bool expected)
    {
        var report = new DungeonGameRestoreReport();
        EnvironmentalFireActionFactPreflight.Validate(save, ledger, report);
        Require(report.Success == expected, "Fire action canonical save join returned an unexpected result.");
    }
    private static void RestoreActionLedger(OutcomeFixture fixture, GameplayOutcomeLedgerSaveData save)
    {
        var candidate = fixture.Ledger.PrepareGameplayOutcomeRestore(save);
        fixture.Ledger.BeginRestoreCandidate(); fixture.Ledger.PublishGameplayOutcomeRestore(candidate);
        fixture.Ledger.PublishRestoreCandidate(); fixture.Ledger.CompleteRestoreCandidate();
    }

    private static void VerifyDamageReplyLossAndSavedReplay()
    {
        foreach (var mode in new[] { ControlledCommitMode.Throw, ControlledCommitMode.HideAfterCommit })
        {
            using var source = new OutcomeFixture(includeDamage: true);
            var faults = new ControlledDamagePublisher(source.Facts) { Mode = mode };
            var authority = new EnvironmentalFireDamageAuthority(faults, faults);
            var command = DamageCommand("fire-damage:unknown:" + mode);
            Require(authority.TryBegin(command, "당시 시설", 23, out _, out _), "Unknown damage could not reserve.");
            Require(authority.TryCommitApplied(command.OperationId, 7, true, out var result, out _)
                && result.Committed && authority.Capture().Single().phase == EnvironmentalFireDamagePhase.Applied
                && source.Ledger.GetDiagnostics().PublishedCount == (mode == ControlledCommitMode.Throw ? 0 : 1),
                "Unknown reply rolled back applied damage or fabricated delivery.");
            var envelope = new DungeonEnvironmentalFireSaveData { damageOperations = authority.Capture().Select(value => value.Clone()).ToList() };
            var saved = DungeonSaveJson.Deserialize<DungeonEnvironmentalFireSaveData>(JsonUtility.ToJson(envelope));
            var ledgerSave = DungeonSaveJson.Deserialize<GameplayOutcomeLedgerSaveData>(JsonUtility.ToJson(source.Ledger.CaptureGameplayOutcomes()));
            var valid = new DungeonGameRestoreReport();
            EnvironmentalFireDamageFactPreflight.Validate(saved.damageOperations, ledgerSave, valid);
            Require(valid.Success, "Valid applied pending fire damage failed canonical preflight.");
            using var target = new OutcomeFixture(includeDamage: true);
            var restoredLedger = target.Ledger.PrepareGameplayOutcomeRestore(ledgerSave);
            target.Ledger.BeginRestoreCandidate(); target.Ledger.PublishGameplayOutcomeRestore(restoredLedger);
            target.Ledger.PublishRestoreCandidate(); target.Ledger.CompleteRestoreCandidate();
            var restored = new EnvironmentalFireDamageAuthority(target.Facts, target.Facts);
            restored.Restore(saved.damageOperations);
            restored.OnRestoreCompleted();
            var delivered = restored.Capture().Single();
            Require(delivered.phase == EnvironmentalFireDamagePhase.Delivered && delivered.appliedDamage == 7
                && delivered.publicationKey == saved.damageOperations[0].publicationKey
                && target.Ledger.GetDiagnostics().PublishedCount == 1 && restored.Recover()
                && target.Ledger.GetDiagnostics().PublishedCount == 1,
                "Fresh owner replay lost/doubled damage publication or changed its identity.");
            var correct = new DungeonGameRestoreReport();
            EnvironmentalFireDamageFactPreflight.Validate(restored.Capture(), target.Ledger.CaptureGameplayOutcomes(), correct);
            Require(correct.Success, "Delivered fire damage proof failed save join.");
            foreach (string field in new[] { "name", "key", "fingerprint" })
            {
                var corrupt = delivered.Clone();
                if (field == "name")
                {
                    corrupt.targetDisplayName += "-changed";
                    corrupt.requestFingerprint = EnvironmentalFireDamageFactIdentity.RequestFingerprint(corrupt);
                }
                else if (field == "key") corrupt.publicationKey += "-changed";
                else corrupt.publicationFingerprint = new string('a', 64);
                var rejected = new DungeonGameRestoreReport();
                EnvironmentalFireDamageFactPreflight.Validate(new[] { corrupt }, target.Ledger.CaptureGameplayOutcomes(), rejected);
                Require(!rejected.Success, "Tampered fire damage " + field + " was accepted.");
            }
            var raw = Newtonsoft.Json.Linq.JObject.Parse(JsonUtility.ToJson(saved));
            ((Newtonsoft.Json.Linq.JObject)raw["damageOperations"][0]).Remove("publicationKey");
            bool missingRejected = false;
            try { EnvironmentalFireSaveSection.ValidateDamagePublicationJson(raw); }
            catch (InvalidOperationException) { missingRejected = true; }
            Require(missingRejected, "Missing saved fire publication key was defaulted.");
            string before = DamageState(restored);
            bool duplicateRejected = false;
            try { restored.Restore(new[] { delivered, delivered.Clone() }); }
            catch (InvalidOperationException) { duplicateRejected = true; }
            Require(duplicateRejected && DamageState(restored) == before, "Duplicate fire damage restore partially mutated authority.");
        }

        using var rejectedSource = new OutcomeFixture(includeDamage: true);
        var reject = new ControlledDamagePublisher(rejectedSource.Facts) { Mode = ControlledCommitMode.Fail };
        var rejectedOwner = new EnvironmentalFireDamageAuthority(reject, reject);
        var rejectedCommand = DamageCommand("fire-damage:known-rejection");
        Require(rejectedOwner.TryBegin(rejectedCommand, "거절 시설", 24, out _, out _)
            && !rejectedOwner.TryCommitApplied(rejectedCommand.OperationId, 7, true, out _, out _)
            && rejectedOwner.Capture().Single().phase == EnvironmentalFireDamagePhase.Prepared
            && rejectedOwner.Capture().Single().appliedDamage == 0
            && rejectedSource.Ledger.GetDiagnostics().PublishedCount == 0,
            "Definite rejection did not preserve the caller's rollback boundary.");
        rejectedOwner.CancelUncommitted(rejectedCommand.OperationId);
        Require(rejectedOwner.Capture().Count == 0, "Rejected source cancellation retained a ghost damage receipt.");
    }

    private static void VerifyElectricalIsolationAndInitialAttack()
    {
        var fixture = new Fixture(electrical: true, intensity: 0.25f);
        EnvironmentalFireIgnitionResult ignition = fixture.Ignite("cause:electrical");
        Require(ignition.Created, "Electrical ignition was not accepted.");
        Require(fixture.PublishedOutcomeCount == 1,
            "Ignition did not publish exactly one gameplay outcome.");
        Require(
            fixture.Ignite("cause:electrical").Disposition
                == EnvironmentalFireIgnitionDisposition.PreviouslyProcessed,
            "Exact ignition replay was not idempotent.");
        Require(fixture.PublishedOutcomeCount == 1,
            "Ignition replay published a duplicate gameplay outcome.");
        EnvironmentalFireIgnitionResult conflictingIgnition =
            fixture.Runtime.TryIgnite(new EnvironmentalFireIgnitionRequest(
                "cause:electrical",
                EnvironmentalFireIgnitionKind.ElectricalFault,
                "focused-test",
                fixture.Target,
                fixture.IgnitionIntensity,
                "evidence:different",
                targetDisplayName: "전기 설비"));
        Require(
            conflictingIgnition.Disposition
                == EnvironmentalFireIgnitionDisposition.CauseConflict,
            "Same ignition cause with different evidence was not rejected as a conflict.");
        Require(fixture.PublishedOutcomeCount == 1,
            "Conflicting ignition published a gameplay outcome.");

        EnvironmentalFireSuppressionCommand command = fixture.InitialAttack(
            "suppression:electrical",
            ignition.FireId,
            13f);
        Require(
            fixture.Runtime.TryApplySuppression(command).Disposition
                == EnvironmentalFireSuppressionDisposition
                    .ElectricalIsolationRequired,
            "Live electrical contact did not block suppression.");

        fixture.Safety = new EnvironmentalFireElectricalSafetySnapshot(
            fixture.Target,
            hasElectricalContact: false,
            connectionEnabled: false,
            breakerTripped: true,
            hasLiveGeneration: false,
            hasStoredPower: true);
        Require(
            fixture.Runtime.TryApplySuppression(command).Disposition
                == EnvironmentalFireSuppressionDisposition
                    .ElectricalIsolationRequired,
            "Stored power was incorrectly ignored after disconnect.");

        fixture.Safety = new EnvironmentalFireElectricalSafetySnapshot(
            fixture.Target,
            hasElectricalContact: false,
            connectionEnabled: false,
            breakerTripped: true,
            hasLiveGeneration: false,
            hasStoredPower: false);
        EnvironmentalFireSuppressionResult applied =
            fixture.Runtime.TryApplySuppression(command);
        Require(
            applied.Disposition
                == EnvironmentalFireSuppressionDisposition.Extinguished,
            "The authored 13 WU initial attack did not extinguish intensity 0.25.");
        Require(fixture.Runtime.ActiveFires.Count == 0, "Extinguished fire remained active.");
        Require(fixture.PublishedOutcomeCount == 2,
            "Successful suppression did not publish exactly one gameplay outcome.");
        Require(
            fixture.Runtime.TryApplySuppression(command).Disposition
                == EnvironmentalFireSuppressionDisposition.PreviouslyApplied,
            "Committed suppression replay was not idempotent.");
        Require(fixture.PublishedOutcomeCount == 2,
            "Suppression replay published a duplicate gameplay outcome.");
        EnvironmentalFireSuppressionResult conflictingSuppression =
            fixture.Runtime.TryApplySuppression(fixture.InitialAttack(
                "suppression:electrical",
                ignition.FireId,
                12f));
        Require(
            conflictingSuppression.Disposition
                == EnvironmentalFireSuppressionDisposition.OperationConflict,
            "Same suppression operation with different work was not rejected as a conflict.");
        Require(fixture.PublishedOutcomeCount == 2,
            "Conflicting suppression published a gameplay outcome.");
    }

    private static void VerifyWaterReceiptRestoreAndAcknowledgement()
    {
        var fixture = new Fixture(electrical: false, intensity: 0.8f);
        fixture.Water.Acknowledge = false;
        EnvironmentalFireIgnitionResult ignition = fixture.Ignite("cause:process");
        Require(ignition.Created, "Process ignition was not accepted.");

        var command = new EnvironmentalFireSuppressionCommand(
            "suppression:water",
            ignition.FireId,
            "character:worker",
            new Vector2Int(1, 0),
            EnvironmentalFireSuppressionMode.Water,
            2.5f,
            "lease:clean-water",
            2,
            "진압 작업자");
        EnvironmentalFireSuppressionResult applied =
            fixture.Runtime.TryApplySuppression(command);
        Require(
            applied.Disposition
                == EnvironmentalFireSuppressionDisposition
                    .AppliedAwaitingWaterAcknowledgement,
            "Failed acknowledgement did not preserve a pending physical-water receipt.");
        Require(fixture.Water.CommitCount == 1, "Water was not committed exactly once.");

        DungeonEnvironmentalFireSaveData saved = fixture.Runtime.Capture();
        string json = JsonUtility.ToJson(saved);
        var restoredFixture = new Fixture(
            electrical: false,
            intensity: 0.8f,
            fixture.Water,
            outcomes: fixture.Outcomes);
        restoredFixture.Runtime.Restore(restoredFixture.Runtime.PrepareRestore(
            JsonUtility.FromJson<DungeonEnvironmentalFireSaveData>(json)));
        restoredFixture.Water.Acknowledge = true;
        IReadOnlyList<EnvironmentalFireSuppressionResult> resumed =
            restoredFixture.Runtime.ResumePendingSuppressions();
        Require(resumed.Count == 1, "Pending acknowledgement was not resumed once.");
        Require(
            restoredFixture.Water.CommitCount == 1,
            "Restore replay recommitted already consumed water.");
        Require(
            restoredFixture.Water.AcknowledgeCount == 2,
            "Physical-water receipt was not acknowledged exactly once after restore.");
    }

    private static void VerifyRestoreTransactionIsolation()
    {
        var live = new Fixture(electrical: false, intensity: 0.4f);
        Require(live.Ignite("cause:restore-live").Created,
            "Live fire restore-transaction fixture did not ignite.");
        live.Runtime.Advance(1.25f);
        DungeonEnvironmentalFireSaveData original = live.Runtime.Capture();
        string originalJson = JsonUtility.ToJson(original);

        var incoming = new Fixture(electrical: false, intensity: 0.75f);
        Require(incoming.Ignite("cause:restore-incoming").Created,
            "Incoming fire restore-transaction fixture did not ignite.");
        incoming.Runtime.Advance(2.5f);
        DungeonEnvironmentalFireSaveData replacement = incoming.Runtime.Capture();
        string replacementJson = JsonUtility.ToJson(replacement);
        Require(original.activeFires.Count == 1
                && original.processedCauses.Count == 1
                && original.accumulator > 0f
                && replacement.activeFires.Count == 1
                && replacement.processedCauses.Count == 1
                && replacement.accumulator > 0f
                && !string.Equals(originalJson, replacementJson, StringComparison.Ordinal),
            "Fire restore-transaction fixtures must contain distinct active fire and clock state.");

        void StageReplacement()
        {
            live.Runtime.BeginRestoreCandidate();
            live.Runtime.Restore(live.Runtime.PrepareRestore(Clone(replacement)));
        }

        StageReplacement();
        Require(JsonUtility.ToJson(live.Runtime.Capture()) == originalJson,
            "Staging a fire restore candidate changed the live fire or accumulator.");
        live.Runtime.DiscardRestoreCandidate();
        Require(JsonUtility.ToJson(live.Runtime.Capture()) == originalJson,
            "Discarding a staged fire restore changed the original live state.");

        StageReplacement();
        live.Runtime.PublishRestoreCandidate();
        Require(JsonUtility.ToJson(live.Runtime.Capture()) == replacementJson,
            "Publishing a fire restore candidate did not adopt the incoming state.");
        live.Runtime.RollbackPublishedRestoreCandidate();
        Require(JsonUtility.ToJson(live.Runtime.Capture()) == originalJson,
            "Rolling back a published fire restore did not recover the original state.");

        StageReplacement();
        live.Runtime.PublishRestoreCandidate();
        live.Runtime.CompleteRestoreCandidate();
        Require(JsonUtility.ToJson(live.Runtime.Capture()) == replacementJson,
            "Completing a published fire restore did not retain the incoming state.");
        live.Runtime.BeginRestoreCandidate();
        live.Runtime.DiscardRestoreCandidate();
        Require(JsonUtility.ToJson(live.Runtime.Capture()) == replacementJson,
            "Completed fire restore transaction did not reset for a later transaction.");

        VerifyDamageRestoreTransactionIsolation();
    }

    private static void VerifyDamageRestoreTransactionIsolation()
    {
        using var outcomes = new OutcomeFixture(includeDamage: true);
        var live = new EnvironmentalFireDamageAuthority(outcomes.Facts, outcomes.Facts);
        EnvironmentalFireDamageCommand liveCommand = DamageCommand(
            "fire-damage:restore-live");
        Require(live.TryBegin(
                liveCommand,
                "화재 복원 원본 시설",
                21,
                out _,
                out string liveBeginFailure),
            "Live fire-damage restore fixture could not reserve: " + liveBeginFailure);
        string preparedLive = DamageState(live);

        var incoming = new EnvironmentalFireDamageAuthority(outcomes.Facts, outcomes.Facts);
        EnvironmentalFireDamageCommand incomingCommand = DamageCommand(
            "fire-damage:restore-incoming");
        Require(incoming.TryBegin(
                incomingCommand,
                "화재 복원 수신 시설",
                22,
                out _,
                out string incomingBeginFailure),
            "Incoming fire-damage restore fixture could not reserve: "
            + incomingBeginFailure);
        string preparedIncoming = DamageState(incoming);
        Require(!string.Equals(preparedLive, preparedIncoming, StringComparison.Ordinal),
            "Fire-damage restore fixtures did not contain distinct records.");

        live.BeginRestoreCandidate();
        live.Restore(incoming.Capture());
        Require(DamageState(live) == preparedLive,
            "Staging fire-damage records changed the live record authority.");
        live.DiscardRestoreCandidate();
        bool liveCommitted = live.TryCommitApplied(
            liveCommand.OperationId,
            7f,
            targetRemainsCombustible: true,
            out EnvironmentalFireDamageResult liveCommit,
            out string liveCommitFailure);
        Require(DamageState(live) != preparedLive
                && liveCommitted
                && liveCommit.Committed,
            "Discarding staged fire-damage records changed live state or cancelled its reservation: "
            + liveCommitFailure);
        string committedLive = DamageState(live);

        Require(incoming.TryCommitApplied(
                incomingCommand.OperationId,
                8f,
                targetRemainsCombustible: true,
                out EnvironmentalFireDamageResult incomingCommit,
                out string incomingCommitFailure)
            && incomingCommit.Committed,
            "Incoming fire-damage restore fixture could not commit: "
            + incomingCommitFailure);
        string committedIncoming = DamageState(incoming);

        void StageIncomingDamage()
        {
            live.BeginRestoreCandidate();
            live.Restore(incoming.Capture());
        }

        StageIncomingDamage();
        Require(DamageState(live) == committedLive,
            "Staging committed fire-damage records changed the live authority.");
        live.PublishRestoreCandidate();
        Require(DamageState(live) == committedIncoming,
            "Publishing fire-damage restore records did not adopt incoming authority.");
        live.RollbackPublishedRestoreCandidate();
        Require(DamageState(live) == committedLive,
            "Rolling back fire-damage restore records did not recover original authority.");

        StageIncomingDamage();
        live.PublishRestoreCandidate();
        live.CompleteRestoreCandidate();
        Require(DamageState(live) == committedIncoming,
            "Completing fire-damage restore records did not retain incoming authority.");
        live.BeginRestoreCandidate();
        live.DiscardRestoreCandidate();
        Require(DamageState(live) == committedIncoming,
            "Completed fire-damage transaction did not reset for a later transaction.");
    }

    private static string DamageState(
        EnvironmentalFireDamageAuthority authority) =>
        string.Join("|", authority.Capture().Select(value =>
            JsonUtility.ToJson(value)));

    private static void VerifyProcessAccidentProducerContract()
    {
        const string operationId = "work-operation:fire-01-process";
        const string facilityId = "building:fire-01-process";
        const float intensity = 0.4f;
        var command = new RecordingEnvironmentalFireCommand();
        BuildableObject facility = CreateProcessAccidentFacility(
            facilityId,
            BuiltInWorkTypeIds.Craft,
            intensity);
        CharacterActor worker = CreateWorker("character:fire-01-worker");
        try
        {
            var producer = new ProcessAccidentEnvironmentalFireProducer(command);
            var eligible = new ProcessAccidentFireReceipt(
                operationId,
                worker,
                facility,
                BuiltInWorkTypeIds.Craft,
                "body:hand",
                3.5f);

            EnvironmentalFireIgnitionResult result = producer.TryPublish(eligible);
            Require(result.Created, "Eligible process accident was not accepted.");
            Require(
                command.AcceptedRequests.Count == 1,
                "Eligible process accident did not reach the environmental-fire command once.");
            EnvironmentalFireIgnitionRequest request = command.AcceptedRequests[0];
            Require(
                request.Kind == EnvironmentalFireIgnitionKind.ProcessAccident,
                "Process accident used the wrong ignition kind.");
            Require(
                request.CauseId
                == "environmental-fire-process-accident:" + operationId,
                "Process accident used the wrong stable cause ID.");
            Require(
                request.Target.Equals(new EnvironmentalFireTargetRef(
                    EnvironmentalFireTargetKind.Building,
                    facilityId)),
                "Process accident targeted a building other than its receipt facility.");
            Require(
                Mathf.Approximately(request.IgnitionIntensity, intensity),
                "Process accident did not preserve its authored ignition intensity.");

            int invocationsBeforeReplay = command.InvocationCount;
            EnvironmentalFireIgnitionResult replay = producer.TryPublish(eligible);
            Require(
                replay.Disposition
                    == EnvironmentalFireIgnitionDisposition.PreviouslyProcessed,
                "Repeated process accident cause was not reported as already processed.");
            Require(
                command.InvocationCount == invocationsBeforeReplay + 1,
                "Process accident replay did not reach the common cause ledger.");
            Require(
                command.AcceptedRequests.Count == 1,
                "Repeated process accident cause created a second ignition.");

            int invocationsBeforeInvalid = command.InvocationCount;
            EnvironmentalFireIgnitionResult ineligible = producer.TryPublish(
                new ProcessAccidentFireReceipt(
                    "work-operation:fire-01-ineligible",
                    worker,
                    facility,
                    BuiltInWorkTypeIds.Repair,
                    "body:hand",
                    3.5f));
            Require(
                ineligible.Disposition
                    == EnvironmentalFireIgnitionDisposition.InvalidRequest,
                "Ineligible process accident was not rejected.");
            Require(
                command.InvocationCount == invocationsBeforeInvalid,
                "Ineligible process accident reached the environmental-fire command.");

            EnvironmentalFireIgnitionResult malformed = producer.TryPublish(default);
            Require(
                malformed.Disposition
                    == EnvironmentalFireIgnitionDisposition.InvalidRequest,
                "Malformed process accident receipt was not rejected.");
            Require(
                command.InvocationCount == invocationsBeforeInvalid,
                "Malformed process accident reached the environmental-fire command.");
        }
        finally
        {
            if (worker != null)
            {
                UnityEngine.Object.DestroyImmediate(worker.gameObject);
            }

            if (facility != null)
            {
                BuildingSO definition = facility.BuildingData;
                UnityEngine.Object.DestroyImmediate(facility.gameObject);
                if (definition != null)
                {
                    UnityEngine.Object.DestroyImmediate(definition);
                }
            }
        }
    }

    private static void VerifyCurrentV8RestoreAndIdentityValidation()
    {
        var source = new Fixture(electrical: false, intensity: 0.25f);
        EnvironmentalFireIgnitionResult ignition = source.Ignite("cause:v8-terminal");
        Require(ignition.Created, "V8 terminal source ignition failed.");
        EnvironmentalFireSuppressionCommand suppression = source.InitialAttack(
            "suppression:v8-terminal",
            ignition.FireId,
            13f,
            CharacterCommandOrigin.DirectPlayerOrder);
        Require(
            source.Runtime.TryApplySuppression(suppression).Disposition
                == EnvironmentalFireSuppressionDisposition.Extinguished,
            "V8 terminal initial attack did not extinguish the fire.");

        DungeonEnvironmentalFireSaveData saved = source.Runtime.Capture();
        EnvironmentalFireSuppressionSaveRecord savedOperation =
            saved.suppressionOperations.Single();
        Require(
            saved.version == DungeonEnvironmentalFireSaveData.CurrentVersion
                && savedOperation.targetId == source.Target.TargetId
                && savedOperation.commandOrigin
                    == (int)CharacterCommandOrigin.DirectPlayerOrder
                && !savedOperation.completionIdentityAcknowledged
                && source.Runtime.LastCompletionIdentityFailureForDebug
                    == "fire-completion-publication-pending:domain-fact-observer-missing:"
                    + typeof(EnvironmentalFireSuppressionCompletedFact).FullName,
            "A missing completion observer did not retain the terminal operation as pending.");

        var restored = new Fixture(electrical: false, intensity: 0.25f);
        restored.Runtime.Restore(restored.Runtime.PrepareRestore(Clone(saved)));
        EnvironmentalFireSuppressionSaveRecord restoredOperation =
            restored.Runtime.Capture().suppressionOperations.Single();
        Require(
            restoredOperation.targetId == source.Target.TargetId
                && restoredOperation.commandOrigin
                    == (int)CharacterCommandOrigin.DirectPlayerOrder
                && !restoredOperation.completionIdentityAcknowledged,
            "V8 restore did not preserve terminal target, origin and deferred completion acknowledgement.");

        foreach (int unsupportedVersion in new[] { 2, 3, 4, 5, 6, 7 })
        {
            DungeonEnvironmentalFireSaveData unsupported = Clone(saved);
            unsupported.version = unsupportedVersion;
            RequireThrows(
                () => source.Runtime.PrepareRestore(unsupported),
                "Unsupported V" + unsupportedVersion
                    + " environmental-fire payload was accepted.");
        }

        DungeonEnvironmentalFireSaveData malformedOrigin = Clone(saved);
        malformedOrigin.suppressionOperations[0].commandOrigin = int.MaxValue;
        RequireThrows(
            () => source.Runtime.PrepareRestore(malformedOrigin),
            "V8 suppression accepted an unknown command origin.");

        DungeonEnvironmentalFireSaveData malformedTarget = Clone(saved);
        malformedTarget.suppressionOperations[0].targetId = string.Empty;
        RequireThrows(
            () => source.Runtime.PrepareRestore(malformedTarget),
            "V8 suppression accepted a missing frozen target.");

        var intermediate = new Fixture(electrical: false, intensity: 0.25f);
        EnvironmentalFireIgnitionResult intermediateIgnition =
            intermediate.Ignite("cause:v8-intermediate");
        Require(intermediateIgnition.Created, "V8 intermediate source ignition failed.");
        Require(
            intermediate.Runtime.TryApplySuppression(intermediate.InitialAttack(
                    "suppression:v8-intermediate",
                    intermediateIgnition.FireId,
                    1f)).Disposition
                == EnvironmentalFireSuppressionDisposition.Applied,
            "V8 intermediate initial attack did not remain nonterminal.");
        DungeonEnvironmentalFireSaveData malformedIntermediateAcknowledgement =
            intermediate.Runtime.Capture();
        malformedIntermediateAcknowledgement
            .suppressionOperations[0].completionIdentityAcknowledged = true;
        RequireThrows(
            () => intermediate.Runtime.PrepareRestore(
                malformedIntermediateAcknowledgement),
            "V8 intermediate suppression accepted a completion acknowledgement.");

        DungeonEnvironmentalFireSaveData malformedPendingAcknowledgement = Clone(saved);
        malformedPendingAcknowledgement.suppressionOperations[0].outcomePending = true;
        malformedPendingAcknowledgement
            .suppressionOperations[0].completionIdentityAcknowledged = true;
        RequireThrows(
            () => source.Runtime.PrepareRestore(malformedPendingAcknowledgement),
            "V8 pending suppression accepted a completion acknowledgement.");
    }

    private static void VerifyDeterministicAdjacentSpread()
    {
        var fixture = new Fixture(
            electrical: false,
            intensity: 0.8f,
            spreadChancePerTick: 1f,
            minimumSpreadIntensity: 0.1f);
        EnvironmentalFireTargetSnapshot combustible = fixture.AddAdjacent(
            "building:spread-combustible",
            combustible: true,
            acceptsSpread: true);
        fixture.AdjacentTargets.Add(combustible);
        fixture.AddAdjacent(
            "building:spread-noncombustible",
            combustible: false,
            acceptsSpread: true);
        fixture.AddAdjacent(
            "building:spread-not-authored",
            combustible: true,
            acceptsSpread: false);

        EnvironmentalFireIgnitionResult ignition = fixture.Ignite(
            "cause:deterministic-spread");
        Require(ignition.Created,
            "The deterministic spread source was not ignited.");
        EnvironmentalFireTickReport first = fixture.Runtime.Advance(5f);
        Require(first.NewFires == 1
            && fixture.Runtime.ActiveFires.Count == 2
            && fixture.Runtime.ActiveFires.Count(value =>
                value.Target.TargetId == combustible.Target.TargetId) == 1,
            "Direct-neighbour spread did not create exactly one authored combustible target.");

        EnvironmentalFireTickReport second = fixture.Runtime.Advance(5f);
        Require(second.NewFires == 0
            && fixture.Runtime.ActiveFires.Count == 2
            && fixture.Runtime.ActiveFires.Select(value => value.Target)
                .Distinct()
                .Count() == 2,
            "Repeated adjacency evaluation created a duplicate fire target.");
    }

    private static void VerifyDamageOutcomeReplayRestoreAndFailureRetry()
    {
        using var outcomes = new OutcomeFixture(includeDamage: true);
        var authority = new EnvironmentalFireDamageAuthority(
            outcomes.Facts, outcomes.Facts);
        EnvironmentalFireDamageCommand command = DamageCommand(
            "fire-damage:replay");
        Require(authority.TryBegin(
                command,
                "화재 시험 시설",
                17,
                out _,
                out string beginFailure),
            "Fire-damage prepare failed: " + beginFailure);
        Require(authority.TryCommitApplied(
                command.OperationId,
                7f,
                targetRemainsCombustible: true,
                out EnvironmentalFireDamageResult committed,
                out string commitFailure)
            && committed.Committed
            && Math.Abs(committed.AppliedDamage - 7f) < 0.001f
            && outcomes.Ledger.GetDiagnostics().PublishedCount == 1,
            "Fire-damage outcome did not commit exactly once: " + commitFailure);
        Require(authority.TryBegin(
                command,
                "화재 시험 시설",
                17,
                out EnvironmentalFireDamageRecord replay,
                out string replayFailure)
            && replay.phase == EnvironmentalFireDamagePhase.Delivered,
            "Exact fire-damage replay was not recovered: " + replayFailure);
        Require(!authority.TryBegin(
                new EnvironmentalFireDamageCommand(
                    command.OperationId,
                    command.FireId,
                    command.Target,
                    command.Position,
                    command.Intensity,
                    command.RequestedDamage + 1f),
                "화재 시험 시설",
                17,
                out _,
                out string conflictFailure)
            && conflictFailure == "environmental-fire-damage-operation-conflict",
            "Conflicting fire-damage replay was accepted.");

        var restored = new EnvironmentalFireDamageAuthority(
            outcomes.Facts, outcomes.Facts);
        restored.Restore(authority.Capture());
        Require(restored.TryCommitApplied(
                command.OperationId,
                7f,
                targetRemainsCombustible: true,
                out _,
                out string restoreFailure)
            && outcomes.Ledger.GetDiagnostics().PublishedCount == 1,
            "Restored fire-damage replay duplicated or lost its outcome: "
            + restoreFailure);

        VerifyLethalDamageFailureRetry(
            "fire-damage:false-retry",
            ControlledCommitMode.Fail);
        VerifyLethalDamageFailureRetry(
            "fire-damage:throw-retry",
            ControlledCommitMode.Throw);
    }

    private static void VerifyLethalDamageFailureRetry(
        string operationId,
        ControlledCommitMode initialMode)
    {
        using var outcomes = new OutcomeFixture(includeDamage: true);
        var committer = new ControlledDamagePublisher(outcomes.Facts) { Mode = initialMode };
        var authority = new EnvironmentalFireDamageAuthority(committer, committer);
        EnvironmentalFireDamageCommand command = DamageCommand(operationId);
        Require(authority.TryBegin(
                command,
                "소실 시험 시설",
                18,
                out _,
                out string beginFailure),
            "Lethal fire-damage prepare failed: " + beginFailure);
        Require(authority.TryStageAwaitingWorldRemoval(
                operationId,
                command.RequestedDamage,
                out string stageFailure),
            "Lethal fire-damage staging failed: " + stageFailure);
        bool accepted = authority.TryFinalizeAfterWorldRemoval(operationId, out _);
        Require(accepted == (initialMode != ControlledCommitMode.Fail)
            && authority.TryGet(operationId, out var pending)
            && pending.phase == EnvironmentalFireDamagePhase.Applied,
            "Failed lethal fire-damage commit did not remain retryable.");

        var restored = new EnvironmentalFireDamageAuthority(committer, committer);
        restored.Restore(authority.Capture());
        committer.Mode = ControlledCommitMode.Success;
        Require(restored.TryFinalizeAfterWorldRemoval(
                operationId,
                out string retryFailure)
            && restored.TryGet(operationId, out var finalized)
            && finalized.phase
                == EnvironmentalFireDamagePhase.Delivered
            && finalized.publicationFingerprint.Length == 64,
            "Restored lethal fire-damage retry did not commit: " + retryFailure);
    }

    private static EnvironmentalFireDamageCommand DamageCommand(
        string operationId) => new(
        operationId,
        "fire:damage-focused",
        new EnvironmentalFireTargetRef(
            EnvironmentalFireTargetKind.Building,
            "building:damage-focused"),
        new Vector2Int(4, 5),
        0.75f,
        9f);

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private static DungeonEnvironmentalFireSaveData Clone(
        DungeonEnvironmentalFireSaveData value) =>
        JsonUtility.FromJson<DungeonEnvironmentalFireSaveData>(
            JsonUtility.ToJson(value));

    private static void RequireThrows(Action action, string message)
    {
        try
        {
            action();
        }
        catch (InvalidOperationException)
        {
            return;
        }

        throw new InvalidOperationException(message);
    }

    private static BuildableObject CreateProcessAccidentFacility(
        string persistentId,
        WorkTypeId supportedWork,
        float ignitionIntensity)
    {
        BuildingAbilityCollection abilities = new();
        abilities.Add(new BuildingEnvironmentalFireAbility
        {
            acceptedSources = EnvironmentalFireIgnitionSources.ProcessAccident
        });
        abilities.Add(new BuildingProcessAccidentFireSourceAbility
        {
            workTypeId = supportedWork.Value,
            ignitionIntensity = ignitionIntensity
        });
        BuildingSO definition = ScriptableObject.CreateInstance<BuildingSO>();
        definition.hideFlags = HideFlags.HideAndDontSave;
        definition.objectName = "공정 시험 시설";
        definition.ReplaceAbilities(abilities);

        var host = new GameObject("FIRE-01 Process Accident Facility")
        {
            hideFlags = HideFlags.HideAndDontSave
        };
        BuildableObject facility = host.AddComponent<BuildableObject>();
        facility.ConstructBuildableObject(
            new EmptyResearchWorkPort(),
            new EmptyFacilityStateChangePort(),
            new EmptyRoomPolicyPort(),
            combatEquipmentRuntime: null,
            worldRegistry: null,
            worldItemStackRuntime: null,
            abilityRuntimeDispatcher: CharacterAiEditorTestDependencies.BuildingAbilityRuntimeDispatcher,
            gameClock: null,
            paidFacilityContracts: null,
            evolutionState: new FacilityEvolutionStateComponentFactory());
        facility.RestorePersistentIdentity((BuildingInstanceId)persistentId);
        facility.Initialization(definition, Vector2Int.zero);
        return facility;
    }

    private static CharacterActor CreateWorker(string persistentId)
    {
        var host = new GameObject("FIRE-01 Process Accident Worker")
        {
            hideFlags = HideFlags.HideAndDontSave
        };
        host.AddComponent<CharacterIdentity>();
        CharacterActor worker = host.AddComponent<CharacterActor>();
        worker.EnsureRuntimeState();
        Require(worker.Identity != null, "Process accident fixture worker has no identity.");
        worker.Identity.SetPersistentId(persistentId);
        return worker;
    }

    private sealed class RecordingEnvironmentalFireCommand :
        IEnvironmentalFireCommand
    {
        private readonly Dictionary<string, string> fingerprintByCause =
            new(StringComparer.Ordinal);

        public List<EnvironmentalFireIgnitionRequest> AcceptedRequests { get; } =
            new();
        public int InvocationCount { get; private set; }

        public EnvironmentalFireIgnitionResult TryIgnite(
            EnvironmentalFireIgnitionRequest request)
        {
            InvocationCount++;
            if (request == null || !request.IsValid)
            {
                return new EnvironmentalFireIgnitionResult(
                    EnvironmentalFireIgnitionDisposition.InvalidRequest,
                    string.Empty,
                    "recording command requires a valid request");
            }

            if (fingerprintByCause.TryGetValue(
                    request.CauseId,
                    out string fingerprint))
            {
                return new EnvironmentalFireIgnitionResult(
                    string.Equals(fingerprint, request.Fingerprint, StringComparison.Ordinal)
                        ? EnvironmentalFireIgnitionDisposition.PreviouslyProcessed
                        : EnvironmentalFireIgnitionDisposition.CauseConflict,
                    string.Empty,
                    "recording command cause ledger result");
            }

            fingerprintByCause.Add(request.CauseId, request.Fingerprint);
            AcceptedRequests.Add(request);
            return new EnvironmentalFireIgnitionResult(
                EnvironmentalFireIgnitionDisposition.Ignited,
                "fire:recording:" + AcceptedRequests.Count,
                string.Empty);
        }

        public EnvironmentalFireSuppressionResult TryApplySuppression(
            EnvironmentalFireSuppressionCommand command) =>
            throw new NotSupportedException("Ignition producer fixture does not suppress fires.");

        public EnvironmentalFireSuppressionResult TryCancelSuppression(
            string operationId) =>
            throw new NotSupportedException("Ignition producer fixture does not cancel suppression.");

        public IReadOnlyList<EnvironmentalFireSuppressionResult>
            ResumePendingSuppressions() =>
            throw new NotSupportedException("Ignition producer fixture does not resume suppression.");
    }

    private sealed class EmptyResearchWorkPort : IBuildingResearchWorkPort
    {
        public bool HasResearchWorkFor(IBuildingWorldEntryPort facility) => false;
    }

    private sealed class EmptyFacilityStateChangePort :
        IBuildingFacilityStateChangePort
    {
        public void MarkDynamicStateDirty()
        {
        }
    }

    private sealed class EmptyRoomPolicyPort : IBuildingRoomPolicyPort
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
            FacilityRole role) => 0f;

        public int GetEffectiveCapacity(IBuildingWorldEntryPort building) => 0;

        public BuildingRoomOperationalSnapshot GetOperationalProfile(
            IBuildingWorldEntryPort building) =>
            new(
                Array.Empty<IBuildingWorldEntryPort>(),
                hasRoom: false,
                isUsableRoom: false,
                qualityScore: 0f,
                seatCapacity: 0,
                tableCapacity: 0,
                serviceCapacity: 0,
                retailCategory: StockCategory.General,
                storage: null);
    }

    private sealed class Fixture :
        IEnvironmentalFireTargetQuery,
        IEnvironmentalFireDamageCommand,
        IEnvironmentalFireSuppressionAccessQuery,
        IEnvironmentalFireElectricalSafetyQuery
    {
        private readonly EnvironmentalFireTargetSnapshot snapshot;

        public Fixture(
            bool electrical,
            float intensity,
            RecordingWaterSink water = null,
            float spreadChancePerTick = 0f,
            float minimumSpreadIntensity = 0.55f,
            OutcomeFixture outcomes = null,
            float maximumIntensity = 1f)
        {
            Outcomes = outcomes ?? new OutcomeFixture();
            Target = new EnvironmentalFireTargetRef(
                EnvironmentalFireTargetKind.Building,
                electrical ? "building:electrical" : "building:process");
            snapshot = new EnvironmentalFireTargetSnapshot(
                Target,
                Vector2Int.zero,
                exists: true,
                combustible: true,
                hasElectricalHazard: electrical,
                profile: new EnvironmentalFireProfile(
                    fuelCapacity: 1f,
                    maximumIntensity: maximumIntensity,
                    growthPerTick: 0.05f,
                    fuelConsumedPerTick: 0.1f,
                    damagePerTick: 12f,
                    minimumSpreadIntensity: minimumSpreadIntensity,
                    spreadChancePerTick: spreadChancePerTick,
                    spreadIgnitionMultiplier: 0.5f,
                    waterSuppressionPerUnit: 0.5f),
                displayName: electrical ? "전기 설비" : "작업 시설");
            IgnitionIntensity = intensity;
            Safety = new EnvironmentalFireElectricalSafetySnapshot(
                Target,
                hasElectricalContact: electrical,
                connectionEnabled: electrical,
                breakerTripped: false,
                hasLiveGeneration: electrical,
                hasStoredPower: electrical);
            Water = water ?? new RecordingWaterSink();
            StageFaults = new ControlledActionPublisher(Outcomes.Facts);
            Runtime = new EnvironmentalFireRuntime(
                this,
                this,
                this,
                this,
                Water,
                new EnvironmentalFireRuntimeSettings(5f, 0.35f, 0.02f),
                new RandomStreamProvider(157181),
                StageFaults,
                Outcomes.Facts,
                Outcomes.Facts,
                EditorFixedGameCalendar.Instance);
        }

        public EnvironmentalFireRuntime Runtime { get; }
        public OutcomeFixture Outcomes { get; }
        public ControlledActionPublisher StageFaults { get; }
        public int PublishedOutcomeCount =>
            Outcomes.Ledger.GetDiagnostics().PublishedCount;
        public EnvironmentalFireTargetRef Target { get; }
        public RecordingWaterSink Water { get; }
        public List<EnvironmentalFireTargetSnapshot> AdjacentTargets { get; } =
            new();
        public float IgnitionIntensity { get; }
        public EnvironmentalFireElectricalSafetySnapshot Safety { get; set; }

        public EnvironmentalFireIgnitionRequest CreateIgnitionRequest(
            string causeId) => new(
                causeId,
                Target.TargetId.EndsWith("electrical", StringComparison.Ordinal)
                    ? EnvironmentalFireIgnitionKind.ElectricalFault
                    : EnvironmentalFireIgnitionKind.ProcessAccident,
                "focused-test",
                Target,
                IgnitionIntensity,
                "evidence:" + causeId,
                targetDisplayName: snapshot.DisplayName);

        public EnvironmentalFireIgnitionResult Ignite(string causeId) =>
            Runtime.TryIgnite(CreateIgnitionRequest(causeId));

        public EnvironmentalFireSuppressionCommand InitialAttack(
            string operationId,
            string fireId,
            float work,
            CharacterCommandOrigin commandOrigin =
                CharacterCommandOrigin.Autonomous) => new(
                operationId,
                fireId,
                "character:worker",
                new Vector2Int(1, 0),
                EnvironmentalFireSuppressionMode.InitialAttack,
                work,
                workerDisplayName: "진압 작업자",
                commandOrigin: commandOrigin);

        public bool TryGetTarget(
            EnvironmentalFireTargetRef target,
            out EnvironmentalFireTargetSnapshot result)
        {
            if (target.Equals(Target))
            {
                result = snapshot;
                return true;
            }

            for (int index = 0; index < AdjacentTargets.Count; index++)
            {
                if (!AdjacentTargets[index].Target.Equals(target))
                    continue;
                result = AdjacentTargets[index];
                return true;
            }

            result = default;
            return false;
        }

        public IReadOnlyList<EnvironmentalFireTargetSnapshot> GetAdjacentTargets(
            EnvironmentalFireTargetRef target) => target.Equals(Target)
                ? AdjacentTargets
                : Array.Empty<EnvironmentalFireTargetSnapshot>();

        public EnvironmentalFireTargetSnapshot AddAdjacent(
            string targetId,
            bool combustible,
            bool acceptsSpread)
        {
            var target = new EnvironmentalFireTargetRef(
                EnvironmentalFireTargetKind.Building,
                targetId);
            var adjacent = new EnvironmentalFireTargetSnapshot(
                target,
                new Vector2Int(1, 0),
                exists: true,
                combustible: combustible,
                hasElectricalHazard: false,
                profile: new EnvironmentalFireProfile(
                    fuelCapacity: 1f,
                    maximumIntensity: 1f,
                    growthPerTick: 0f,
                    fuelConsumedPerTick: 0.01f,
                    damagePerTick: 1f,
                    minimumSpreadIntensity: 1f,
                    spreadChancePerTick: 0f,
                    spreadIgnitionMultiplier: 0.5f,
                    waterSuppressionPerUnit: 0.5f),
                acceptedSources: acceptsSpread
                    ? EnvironmentalFireIgnitionSources.Spread
                    : EnvironmentalFireIgnitionSources.ProcessAccident,
                displayName: "인접 시설");
            AdjacentTargets.Add(adjacent);
            return adjacent;
        }

        public bool TryApply(
            EnvironmentalFireDamageCommand command,
            out EnvironmentalFireDamageResult result)
        {
            result = new EnvironmentalFireDamageResult(
                committed: true,
                appliedDamage: command.RequestedDamage,
                targetRemainsCombustible: true);
            return true;
        }

        public bool CanSuppress(
            string workerId,
            Vector2Int standPosition,
            EnvironmentalFireTargetRef target,
            out string failureReason)
        {
            failureReason = string.Empty;
            return workerId == "character:worker"
                && standPosition == new Vector2Int(1, 0)
                && target.Equals(Target);
        }

        public bool TryGetSafety(
            EnvironmentalFireTargetRef target,
            out EnvironmentalFireElectricalSafetySnapshot result)
        {
            result = Safety;
            return target.Equals(Target);
        }
    }

    internal sealed class OutcomeFixture : IDisposable
    {
        internal OutcomeFixture(bool includeDamage = false, IGameCalendar deliveryCalendar = null)
        {
            var josa = new KoreanJosaFormatter();
            List<IGameplayOutcomeDescriptor> descriptors = new()
            {
                new FireIgnitionOutcomeDescriptor(josa),
                new FireSuppressionOutcomeDescriptor(josa)
            };
            List<IGameplayOutcomeAdapterRegistration> adapters = new()
            {
                new EnvironmentalFireIgnitionOutcomeAdapter(),
                new EnvironmentalFireSuppressionOutcomeAdapter()
            };
            if (includeDamage)
            {
                descriptors.Add(new FireDamageOutcomeDescriptor(josa));
                adapters.Add(new EnvironmentalFireDamageOutcomeAdapter());
            }
            GameplayOutcomeRegistry registry = new(
                descriptors,
                adapters);
            Ledger = new GameplayOutcomeLedger(
                registry,
                new GameplayOutcomeBufferLimits(),
                new GameplayOutcomeRunId("run:fire-01-focused"),
                1L,
                deliveryCalendar);
            GameplayOutcomeRecorder recorder = new(
                Ledger,
                registry,
                new GameEventBus());
            Facts = new DomainFactDispatcher();
            GameplayDomainFactCommitGroup commitGroup = new(recorder, Ledger);
            Facts.Subscribe("test:fire-ignition", new GameplayDomainFactObserver<
                EnvironmentalFireIgnitedFact, EnvironmentalFireIgnitionOutcomeReceipt>(
                    EnvironmentalFireActionFactMapping.Capture, registry, Ledger, recorder, commitGroup));
            Facts.Subscribe("test:fire-suppression", new GameplayDomainFactObserver<
                EnvironmentalFireSuppressedFact, EnvironmentalFireSuppressionOutcomeReceipt>(
                    EnvironmentalFireActionFactMapping.Capture, registry, Ledger, recorder, commitGroup));
            if (includeDamage)
            {
                Facts.Subscribe("test:fire-damage", new GameplayDomainFactObserver<EnvironmentalFireDamageFact, EnvironmentalFireDamageOutcomeReceipt>(
                    EnvironmentalFireDamageFactMapping.Capture, registry, Ledger, recorder,
                    commitGroup, EnvironmentalFireDamageFactMapping.Reserve));
            }
        }

        internal GameplayOutcomeLedger Ledger { get; }
        internal DomainFactDispatcher Facts { get; }
        public void Dispose() => Facts.Dispose();
    }

    private enum ControlledCommitMode
    {
        Success,
        Fail,
        Throw,
        HideAfterCommit
    }

    private sealed class ControlledDamagePublisher : IDomainFactPublisher, IDomainFactRecoveryPublisher
    {
        private readonly DomainFactDispatcher inner;
        internal ControlledDamagePublisher(DomainFactDispatcher inner) => this.inner = inner;
        public ControlledCommitMode Mode { get; set; }
        public bool TryReserve<TFact>(in TFact fact, out DomainFactLease lease, out string failure) => inner.TryReserve(fact, out lease, out failure);
        public DomainFactDelivery Publish<TFact>(in DomainFactLease lease, in TFact fact)
        {
            if (Mode == ControlledCommitMode.Success) return inner.Publish(lease, fact);
            if (Mode == ControlledCommitMode.HideAfterCommit)
            {
                Require(inner.Publish(lease, fact).Completed, "Expected a real fire publication before losing its reply.");
                throw new InvalidOperationException("controlled-reply-lost-after-commit");
            }
            inner.Cancel(lease);
            if (Mode == ControlledCommitMode.Throw) throw new InvalidOperationException("controlled-before-commit");
            return DomainFactDelivery.Reject("controlled-rejected");
        }
        public bool PublishBatch(IReadOnlyList<DomainFactEmission> emissions, DomainFactDelivery[] results, out string failure) =>
            inner.PublishBatch(emissions, results, out failure);
        public DomainFactDelivery Replay<TFact>(in TFact fact) => inner.Replay(fact);
        public void Cancel(in DomainFactLease lease) => inner.Cancel(lease);
        public bool ReplayBatch(IReadOnlyList<DomainFactReplay> facts, DomainFactDelivery[] results, out string failure) =>
            inner.ReplayBatch(facts, results, out failure);
    }

    private sealed class ControlledActionPublisher : IDomainFactStagingPublisher
    {
        private readonly DomainFactDispatcher inner;
        public ControlledActionPublisher(DomainFactDispatcher inner) => this.inner = inner;
        public ControlledCommitMode Mode { get; set; }
        public bool TryStage<TFact>(in TFact fact, out IDomainFactStage stage, out string failure)
        {
            stage = null;
            if (!inner.TryStage(fact, out var prepared, out failure)) return false;
            stage = new Stage(prepared, Mode); return true;
        }
        private sealed class Stage : IDomainFactStage
        {
            private readonly IDomainFactStage inner;
            private readonly ControlledCommitMode mode;
            public Stage(IDomainFactStage inner, ControlledCommitMode mode) { this.inner = inner; this.mode = mode; }
            public string OperationKey => inner.OperationKey;
            public long OwnerRevision => inner.OwnerRevision;
            public bool IsReplay => inner.IsReplay;
            public bool IsActive => inner.IsActive;
            public void Cancel() => inner.Cancel();
            public DomainFactDelivery Commit()
            {
                if (mode == ControlledCommitMode.Success) return inner.Commit();
                if (mode == ControlledCommitMode.HideAfterCommit)
                {
                    Require(inner.Commit().Completed, "Expected a real action publication before losing its response.");
                    throw new InvalidOperationException("controlled-action-reply-lost");
                }
                inner.Cancel();
                if (mode == ControlledCommitMode.Throw) throw new InvalidOperationException("controlled-action-before-commit");
                return DomainFactDelivery.Reject("controlled-action-rejected");
            }
        }
    }

    private sealed class RecordingWaterSink : IEnvironmentalFireWaterSink
    {
        private readonly Dictionary<string, EnvironmentalFireWaterReceipt> pending =
            new(StringComparer.Ordinal);

        public bool Acknowledge { get; set; } = true;
        public int CommitCount { get; private set; }
        public int AcknowledgeCount { get; private set; }

        public bool TryCommitReservedWaterPending(
            string leaseId,
            int quantity,
            string operationId,
            string workerId,
            string fireId,
            Vector2Int standPosition,
            Vector2Int targetPosition,
            out EnvironmentalFireWaterReceipt receipt,
            out string failureReason)
        {
            if (!pending.TryGetValue(operationId, out receipt))
            {
                receipt = new EnvironmentalFireWaterReceipt(
                    operationId,
                    leaseId,
                    quantity,
                    "commit:" + operationId);
                pending.Add(operationId, receipt);
                CommitCount++;
            }
            failureReason = string.Empty;
            return true;
        }

        public bool TryGetPending(
            string operationId,
            string leaseId,
            out EnvironmentalFireWaterReceipt receipt) =>
            pending.TryGetValue(operationId, out receipt)
            && string.Equals(receipt.LeaseId, leaseId, StringComparison.Ordinal);

        public bool TryAcknowledge(string commitId, out string failureReason)
        {
            AcknowledgeCount++;
            failureReason = Acknowledge ? string.Empty : "controlled pending ACK";
            return Acknowledge;
        }
    }
}
#endif
