#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DungeonStory.Foundation;
using DungeonStory.Foundation.Facts;
using DungeonStory.Narrative.Korean;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

public static class ReproductionProcessTransitionDebugScenarios
{
    private const string FacilityInstanceId =
        "building:qa:reproduction-transition";
    private const string FertilityItemId = "medical:fertility-treatment";
    private const string FertilityStackId = "stack:qa:fertility-treatment";

    [MenuItem("Dungeon Story/QA/V27/Reproduction Process Transition")]
    public static void RunFromMenu() => RunOrThrow();

    public static void RunCR02R05FromBatch()
    {
        if (!Application.isBatchMode
            || !string.Equals(
                Environment.GetEnvironmentVariable(
                    "DUNGEONSTORY_U0_CR02R05_REPRODUCTION_VERIFY"),
                "1",
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "C:R02-R05 verification requires explicit isolated batch mode.");
        }

        string output = Environment.GetEnvironmentVariable(
            "DUNGEONSTORY_AUDIT_REMEDIATION_OUTPUT");
        if (string.IsNullOrWhiteSpace(output))
        {
            throw new InvalidOperationException(
                "C:R02-R05 verification requires its isolated output directory.");
        }
        string directory = Path.GetFullPath(output);
        Directory.CreateDirectory(directory);
        string reportPath = Path.Combine(directory, "report.json");
        if (File.Exists(reportPath))
        {
            throw new InvalidOperationException(
                "C:R02-R05 report already exists: " + reportPath);
        }

        var report = new JObject
        {
            ["rowId"] = "C:R02-R05",
            ["passed"] = false,
            ["planStartExact"] = false,
            ["materialFailureRecoveryExact"] = false,
            ["waitingFailureCompletionExact"] = false,
            ["carrierDeathChildExact"] = false,
            ["savePhasesRoundTrip"] = false,
            ["birthAndHouseholdRecoveryExact"] = false,
            ["oldSaveMigrationAdded"] = false,
            ["fullAuditExecuted"] = false,
            ["humanApprovalRecorded"] = false,
            ["trainingExecuted"] = false,
            ["sourceFiles"] = new JArray
            {
                "Assets/Scripts/Services/Character/ReproductionCommandRuntime.cs",
                "Assets/Scripts/Services/Character/ReproductionProcessTransitionRuntime.cs",
                "Assets/Scripts/Services/Character/ReproductionEmergencyExtractionCommandRuntime.cs",
                "Assets/Scripts/Services/Character/Editor/ReproductionProcessTransitionDebugScenarios.cs",
                "Assets/Scripts/Services/Narrative/GameplayOutcomeBridges/Evolution/ReproductionProcessTransitionOutcomeDefinition.cs"
            }
        };

        try
        {
            RunCore(out ReproductionTransitionEvidence evidence);
            report["planStartExact"] = true;
            report["materialFailureRecoveryExact"] = true;
            report["waitingFailureCompletionExact"] = true;
            report["carrierDeathChildExact"] = true;
            report["savePhasesRoundTrip"] = true;
            report["birthAndHouseholdRecoveryExact"] = true;
            report["planOutcomeExample"] = JObject.FromObject(
                evidence.Plan.PlanOutcome);
            report["startOutcomeExample"] = JObject.FromObject(
                evidence.Plan.StartOutcome);
            report["dailyCompletionExamples"] = JArray.FromObject(
                evidence.Daily.CompletionOutcomes);
            report["dailyWaitingFailureExamples"] = JArray.FromObject(
                evidence.Daily.WaitingFailureOutcomes);
            report["earlyCarrierDeathChildExample"] = JObject.FromObject(
                evidence.CarrierDeath.EarlyCarrierDeathOutcome);
            report["lateCarrierDeathChildExample"] = JObject.FromObject(
                evidence.CarrierDeath.LateCarrierDeathOutcome);
            report["emergencyExtractionExample"] = JObject.FromObject(
                evidence.CarrierDeath.ExtractionOutcome);
            report["savedPhases"] = new JObject
            {
                ["startPendingPhase"] = evidence.Plan.PendingStartSave
                    .processes.Single().pendingStart.phase.ToString(),
                ["dailyProcesses"] = JArray.FromObject(
                    evidence.Daily.RestoredSave.processes.Select(value => new
                    {
                        value.processId,
                        status = value.status.ToString(),
                        failure = value.failure.ToString(),
                        value.transitionRevision,
                        value.lastPublicationKey,
                        value.lastPublicationId,
                        value.lastPublicationFingerprint
                    })),
                ["carrierWaitingProcess"] = JObject.FromObject(
                    evidence.CarrierDeath.WaitingSave.processes.Single(value =>
                        value.processId
                            == evidence.CarrierDeath.LateProcessId))
            };
            report["passed"] = true;
            WriteCreateOnlyJson(reportPath, report);
            Debug.Log(
                "[AuditRemediation][C:R02-R05] PASS plan-start=1; "
                + "material-recovery=1; waiting-failure-completion=1; "
                + "carrier-death-child=1; save-phases=1");
        }
        catch (Exception exception)
        {
            report["failure"] = exception.GetType().Name
                + ": "
                + exception.Message;
            if (!File.Exists(reportPath))
                WriteCreateOnlyJson(reportPath, report);
            Debug.LogException(exception);
            throw;
        }
    }

    public static void RunCR07FromBatch()
    {
        if (!Application.isBatchMode
            || !string.Equals(
                Environment.GetEnvironmentVariable(
                    "DUNGEONSTORY_U0_CR07_BIRTH_VERIFY"),
                "1",
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "C:R07 verification requires explicit isolated batch mode.");
        }

        string output = Environment.GetEnvironmentVariable(
            "DUNGEONSTORY_AUDIT_REMEDIATION_OUTPUT");
        if (string.IsNullOrWhiteSpace(output))
        {
            throw new InvalidOperationException(
                "C:R07 verification requires its isolated output directory.");
        }
        string directory = Path.GetFullPath(output);
        Directory.CreateDirectory(directory);
        string reportPath = Path.Combine(directory, "report.json");
        if (File.Exists(reportPath))
        {
            throw new InvalidOperationException(
                "C:R07 report already exists: " + reportPath);
        }

        var report = new JObject
        {
            ["rowId"] = "C:R07",
            ["passed"] = false,
            ["boundaryFailuresAndSaveRetry"] = false,
            ["childDuplicateZero"] = false,
            ["publicationMarkerExact"] = false,
            ["productionPortAuthoritiesExact"] = false,
            ["actualParentsAndGuardianExact"] = false,
            ["populationProfileExact"] = false,
            ["applicationAdapterReached"] = false,
            ["oldSaveMigrationAdded"] = false,
            ["fullAuditExecuted"] = false,
            ["humanApprovalRecorded"] = false,
            ["trainingExecuted"] = false,
            ["sourceFiles"] = new JArray
            {
                "Assets/Scripts/Services/Character/ReproductionApplicationAdapter.cs",
                "Assets/Scripts/Services/Character/ReproductionBirthTransactionRuntime.cs",
                "Assets/Scripts/Services/Character/Editor/ReproductionProcessTransitionDebugScenarios.cs",
                "Assets/Scripts/Services/Infrastructure/Registration/DungeonCharacterRegistration.cs"
            }
        };

        try
        {
            BirthTransactionEvidence transaction =
                VerifyBirthTransactionBoundaryRecovery();
            ProductionBirthPublicationEvidence production =
                VerifyProductionBirthPublicationPort();
            report["boundaryFailuresAndSaveRetry"] = true;
            report["childDuplicateZero"] = true;
            report["publicationMarkerExact"] = true;
            report["productionPortAuthoritiesExact"] = true;
            report["actualParentsAndGuardianExact"] = true;
            report["populationProfileExact"] = true;
            report["applicationAdapterReached"] = true;
            report["transactionBirthExample"] = JObject.FromObject(
                transaction.BirthOutcome);
            report["productionBirthExample"] = JObject.FromObject(
                production.BirthOutcome);
            report["productionResult"] = new JObject
            {
                ["processId"] = production.ProcessSave.processId,
                ["childId"] = production.ChildId,
                ["resultPublished"] = production.ProcessSave.resultPublished,
                ["pendingBirth"] = production.ProcessSave.pendingBirth == null
                    ? JValue.CreateNull()
                    : JObject.FromObject(production.ProcessSave.pendingBirth),
                ["actorCount"] = production.ActorCount,
                ["lifeCount"] = production.LifeCount,
                ["narrativeCount"] = production.NarrativeCount,
                ["populationProfileCount"] = production.PopulationProfileCount,
                ["populationStanding"] = production.PopulationStanding
                    .ToString(),
                ["parents"] = new JArray(production.ParentIds),
                ["guardianId"] = production.GuardianId,
                ["housingCallCount"] = production.HousingCallCount,
                ["authorityCountsAfterReplay"] = JObject.FromObject(
                    production.AuthorityCountsAfterReplay)
            };
            report["passed"] = true;
            WriteCreateOnlyJson(reportPath, report);
            Debug.Log(
                "[AuditRemediation][C:R07] PASS boundaries=8; "
                + "production-port=1; child-duplicate=0; parents=2; "
                + "population-profile=1; adapter=1");
        }
        catch (Exception exception)
        {
            report["failure"] = exception.GetType().Name
                + ": "
                + exception.Message;
            if (!File.Exists(reportPath))
                WriteCreateOnlyJson(reportPath, report);
            Debug.LogException(exception);
            throw;
        }
    }

    public static void RunCR09FromBatch()
    {
        if (!Application.isBatchMode
            || !string.Equals(
                Environment.GetEnvironmentVariable(
                    "DUNGEONSTORY_U0_CR09_HOUSEHOLD_VERIFY"),
                "1",
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "C:R09 verification requires explicit isolated batch mode.");
        }

        string output = Environment.GetEnvironmentVariable(
            "DUNGEONSTORY_AUDIT_REMEDIATION_OUTPUT");
        if (string.IsNullOrWhiteSpace(output))
        {
            throw new InvalidOperationException(
                "C:R09 verification requires its isolated output directory.");
        }
        string directory = Path.GetFullPath(output);
        Directory.CreateDirectory(directory);
        string reportPath = Path.Combine(directory, "report.json");
        if (File.Exists(reportPath))
        {
            throw new InvalidOperationException(
                "C:R09 report already exists: " + reportPath);
        }

        var report = new JObject
        {
            ["rowId"] = "C:R09",
            ["passed"] = false,
            ["noParentHouseholdPendingExact"] = false,
            ["noInventedHousehold"] = false,
            ["existingParentHouseholdUsed"] = false,
            ["bedCapacityExhaustionPendingExact"] = false,
            ["saveRestoreAndLaterAssignmentExact"] = false,
            ["stableMultiSlotAllocation"] = false,
            ["oldSaveMigrationAdded"] = false,
            ["fullAuditExecuted"] = false,
            ["humanApprovalRecorded"] = false,
            ["trainingExecuted"] = false,
            ["sourceFiles"] = new JArray
            {
                "Assets/Scripts/Models/Species/Core/CareerHouseholdDomain.cs",
                "Assets/Scripts/Services/Character/HouseholdBirthAssignmentRuntime.cs",
                "Assets/Scripts/Services/Character/Editor/ReproductionProcessTransitionDebugScenarios.cs"
            },
            ["combinedCurrentEvidence"] = new JArray
            {
                "u0-cr01-family-planning-live-r1",
                "u0-cr07-birth-live-r2"
            }
        };

        try
        {
            HouseholdBirthEvidence evidence =
                VerifyHouseholdBirthAssignmentAndRecovery();
            report["noParentHouseholdPendingExact"] = true;
            report["noInventedHousehold"] = true;
            report["existingParentHouseholdUsed"] = true;
            report["bedCapacityExhaustionPendingExact"] = true;
            report["saveRestoreAndLaterAssignmentExact"] = true;
            report["stableMultiSlotAllocation"] = true;
            report["initialPendingSave"] = JObject.FromObject(
                evidence.InitialPendingSave);
            report["capacityPendingSave"] = JObject.FromObject(
                evidence.CapacityPendingSave);
            report["recoveredSave"] = JObject.FromObject(
                evidence.RecoveredSave);
            report["passed"] = true;
            WriteCreateOnlyJson(reportPath, report);
            Debug.Log(
                "[AuditRemediation][C:R09] PASS no-household-pending=1; "
                + "invented-household=0; bed-slots=2; capacity-pending=1; "
                + "save-resume-assignment=1");
        }
        catch (Exception exception)
        {
            report["failure"] = exception.GetType().Name
                + ": "
                + exception.Message;
            if (!File.Exists(reportPath))
                WriteCreateOnlyJson(reportPath, report);
            Debug.LogException(exception);
            throw;
        }
    }

    public static bool RunOrThrow() => RunCore(out _);

    public static string RunCarrierDeathParentageFocused()
    {
        VerifyCarrierDeathParentageAndRecovery();
        VerifyCarrierDeathObservationPendingAndConflict();
        return "REPRODUCTION_CARRIER_DEATH_PARENTAGE=PASS";
    }

    public static string RunObserverFocused()
    {
        VerifyRealPlanStartAndRecovery();
        VerifyDailyTransitionRollbackWaitingFailureAndCompletion();
        RunCarrierDeathParentageFocused();
        VerifyBirthTransactionBoundaryRecovery();
        foreach (bool afterCommit in new[] { false, true })
        {
            using var fixture = new TransitionFixture("run:qa:reproduction-observer-unknown-" + afterCommit.ToString().ToLowerInvariant());
            var process = fixture.AddActiveEgg("reproduction:qa:observer-unknown");
            fixture.Facts.LoseNextReply = true;
            fixture.Facts.CommitBeforeLosingReply = afterCommit;
            Require(fixture.Transitions.TryAdvance(process.ProcessId, SafeDay(70), out _),
                "An unknown observer reply incorrectly authorized effect rollback.");
            var saved = RoundTrip(fixture.Persistence.Capture());
            var pending = saved.processes.Single().pendingTransitions.Single();
            Require(saved.processes.Single().totalProgressDays == 1
                && fixture.OutcomeEnvironment.RowsForProcess(process.ProcessId).Count == (afterCommit ? 1 : 0),
                "Unknown observation lost or repeated the daily domain effect.");
            var ledger = fixture.OutcomeEnvironment.Ledger.CaptureGameplayOutcomes();
            var valid = new DungeonGameRestoreReport();
            ReproductionTransitionFactPreflight.Validate(saved, ledger, fixture.Definitions, valid);
            Require(valid.Success, "The saved frozen pending fact did not join its canonical observation.");
            if (afterCommit)
            {
                var changed = RoundTrip(saved);
                changed.processes.Single().pendingTransitions.Single().fact.people[0].name = "변조된 이름";
                var invalid = new DungeonGameRestoreReport();
                ReproductionTransitionFactPreflight.Validate(changed, ledger, fixture.Definitions, invalid);
                Require(!invalid.Success, "Pending payload drift reused a committed observation identity.");
            }
            var raw = JObject.Parse(JsonUtility.ToJson(saved));
            ((JObject)((JArray)raw["processes"])[0]).Remove("pendingTransitions");
            bool missingRejected = false;
            try { ReproductionSaveSection.ValidateTransitionJson(raw.ToString()); }
            catch (InvalidOperationException) { missingRejected = true; }
            Require(missingRejected, "A missing pending-transition array was silently initialized.");

            fixture.Persistence.Restart(saved);
            var fresh = new ReproductionTransitionPublication(fixture.Persistence,
                new ReproductionTransitionFactSource(fixture.OutcomeEnvironment.Names),
                fixture.Facts, fixture.Facts, fixture.Alerts);
            Require(fresh.TryResume(process.ProcessId, out _) && fresh.TryResume(process.ProcessId, out _),
                "A fresh publication owner could not replay the exact saved fact.");
            var restored = fixture.RequireProcess(process.ProcessId);
            Require(restored.PendingTransition == null && restored.TotalProgressDays == 1
                && restored.LastPublicationKey == pending.publicationKey
                && fixture.OutcomeEnvironment.RowsForProcess(process.ProcessId).Count == 1,
                "Saved replay duplicated progress, identity, or observation.");
            var completed = new DungeonGameRestoreReport();
            ReproductionTransitionFactPreflight.Validate(fixture.Persistence.Capture(),
                fixture.OutcomeEnvironment.Ledger.CaptureGameplayOutcomes(), fixture.Definitions, completed);
            Require(completed.Success, "Acknowledged reproduction proof did not join the canonical save.");
        }
        return "REPRODUCTION_OBSERVER=PASS plan/start/daily/death/extraction/birth; unknown-before/after; frozen-json-replay/canonical-join/dedup";
    }

    private static bool RunCore(out ReproductionTransitionEvidence evidence)
    {
        evidence = null;
        PlanStartEvidence plan = VerifyRealPlanStartAndRecovery();
        DailyTransitionEvidence daily =
            VerifyDailyTransitionRollbackWaitingFailureAndCompletion();
        CarrierDeathEvidence carrierDeath =
            VerifyCarrierDeathParentageAndRecovery();
        VerifyBirthTransactionBoundaryRecovery();
        VerifyHouseholdBirthAssignmentAndRecovery();
        evidence = new ReproductionTransitionEvidence(
            plan,
            daily,
            carrierDeath);
        Debug.Log(
            "REPRODUCTION_PROCESS_TRANSITION=PASS "
            + "real-plan=1 durable-wear-rollback=1 exact-plan-cost=1 "
            + "start-material-consumed=1 start-save-recovery=1 no-double-consume=1 "
            + "daily-commit-rollback=1 waiting-days=2 incubation-failure=1 "
            + "completion=1 transition-save-roundtrip=1 "
            + "carrier-death-pending-recovery=1 early-failure=1 late-extraction=1 "
            + "character-death-parent=2 exact-participants-facility-costs=1 "
            + "emergency-alert=1 medical-command=1 command-save-restore=1 "
            + "deadline-rejection=1 extraction-causal-parent=1 "
            + "birth-boundary-failures=8 birth-save-recovery=1 "
            + "birth-authorities-exactly-once=6 birth-outcome=1 "
            + "birth-result-marker=1 household-unassigned-pending=1 "
            + "household-existing-parent-only=1 bed-slot-capacity=2 "
            + "household-save-recovery=1 no-invented-household=1");
        return true;
    }

    private static void WriteCreateOnlyJson(string path, JObject value)
    {
        using FileStream stream = new(
            path,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None);
        using StreamWriter writer = new(stream);
        writer.Write(value.ToString());
    }

    public static void RunG11DurablePlanEvidence()
    {
        VerifyRealPlanStartAndRecovery();
        Debug.Log(
            "[AuditRemediation][G11] reproduction durable plan evidence PASS");
    }

    internal static JObject VerifyAllowedPolicyProductionProposal(
        IGameSessionStateProvider session)
    {
        using PlanFixture fixture = new();
        return fixture.VerifyAllowedPolicyProductionProposal(session);
    }

    private static BirthTransactionEvidence
        VerifyBirthTransactionBoundaryRecovery()
    {
        using TransitionFixture fixture = new("run:qa:reproduction-birth");
        ReproductionProcess completed = fixture.AddCompletedEgg(
            "reproduction:qa:birth-transaction");
        CharacterId expectedChild = CharacterId.FromStableSuffix(
            "reproduction-birth:" + completed.ProcessId);

        RequireBirthBoundaryFailure(
            fixture,
            completed.ProcessId,
            ReproductionBirthTransactionPhase.Prepared,
            "life");
        RequireBirthBoundaryFailure(
            fixture,
            completed.ProcessId,
            ReproductionBirthTransactionPhase.LifeRegistered,
            "narrative");
        RequireBirthBoundaryFailure(
            fixture,
            completed.ProcessId,
            ReproductionBirthTransactionPhase.NarrativeRegistered,
            "kinship");
        RequireBirthBoundaryFailure(
            fixture,
            completed.ProcessId,
            ReproductionBirthTransactionPhase.KinshipRegistered,
            "actor");
        RequireBirthBoundaryFailure(
            fixture,
            completed.ProcessId,
            ReproductionBirthTransactionPhase.ActorPublished,
            "population");
        RequireBirthBoundaryFailure(
            fixture,
            completed.ProcessId,
            ReproductionBirthTransactionPhase.PopulationRegistered,
            "housing");

        fixture.BirthPort.FailBoundary = string.Empty;
        fixture.Facts.FailNextPrepare = true;
        Require(!fixture.Births.TryPublish(completed.ProcessId, 70, out _),
            "Injected birth outcome prepare rejection unexpectedly succeeded.");
        Require(fixture.RequireProcess(completed.ProcessId).PendingBirth?.phase
                == ReproductionBirthTransactionPhase.PublicationPrepared,
            "Birth outcome prepare rejection did not retain its publication marker phase.");
        fixture.RestartBirthCommand();

        fixture.Facts.FailNextCommit = true;
        Require(!fixture.Births.TryResumePending(out _),
            "Injected birth outcome commit rejection unexpectedly succeeded.");
        Require(fixture.RequireProcess(completed.ProcessId).PendingBirth?.phase
                == ReproductionBirthTransactionPhase.PublicationPrepared
                && !fixture.RequireProcess(completed.ProcessId).ResultPublished,
            "Rejected birth outcome exposed a result marker.");
        fixture.RestartBirthCommand();

        Require(fixture.Births.TryResumePending(out _),
            "A fresh runtime did not resume the pending birth transaction.");
        ReproductionProcess published = fixture.RequireProcess(completed.ProcessId);
        Require(published.ResultPublished
                && published.ResultCharacterId.Equals(expectedChild)
                && published.PendingBirth == null,
            "Recovered birth did not publish the deterministic child marker.");
        Require(fixture.BirthPort.EnsureCounts.Values.All(value => value == 1)
                && fixture.BirthPort.EnsureCounts.Count == 6
                && fixture.BirthPort.ChildIds.SetEquals(new[] { expectedChild }),
            "Birth recovery duplicated an authority write or changed child identity.");

        GameplayOutcomeSnapshot birth = fixture.OutcomeEnvironment
            .RowsForProcess(completed.ProcessId)
            .Single(value => Fact(
                    value,
                    ReproductionProcessTransitionOutcomeIds.TransitionKind)
                == "birth-published");
        Require(birth.participants.Count(value =>
                    value.roleId
                        == ReproductionProcessTransitionOutcomeIds.OffspringRole.Value
                    && value.entityId == expectedChild.Value) == 1
                && birth.ownerRevision == published.TransitionRevision,
            "Birth outcome lost its exact child participant or publication revision.");

        int rowCount = fixture.OutcomeEnvironment.ReproductionRows().Count;
        Require(fixture.Births.TryPublish(completed.ProcessId, 71, out _)
                && fixture.OutcomeEnvironment.ReproductionRows().Count == rowCount
                && fixture.BirthPort.EnsureCounts.Values.All(value => value == 1),
            "Replaying a completed birth duplicated a child authority or outcome.");
        return new BirthTransactionEvidence(
            birth,
            published.Capture(),
            expectedChild.Value,
            fixture.BirthPort.EnsureCounts);
    }

    private static ProductionBirthPublicationEvidence
        VerifyProductionBirthPublicationPort()
    {
        using var fixture = new ProductionBirthPublicationFixture();
        return fixture.Execute();
    }

    private static HouseholdBirthEvidence
        VerifyHouseholdBirthAssignmentAndRecovery()
    {
        CharacterId firstParent = new("character:qa:household:first-parent");
        CharacterId secondParent = new("character:qa:household:second-parent");
        CharacterId childOne = new("character:qa:household:child-one");
        CharacterId childTwo = new("character:qa:household:child-two");
        CharacterId childThree = new("character:qa:household:child-three");
        HouseholdId household = new("household:qa:existing-parent");
        BuildingInstanceId parentRoom = new("building:qa:parent-room");
        BuildingInstanceId parentBed = new("building:qa:parent-bed");
        BuildingInstanceId nurseryRoom = new("building:qa:nursery-room");
        BuildingInstanceId bunkBed = new("building:qa:bunk-bed");
        BuildingInstanceId addedBed = new("building:qa:added-bed");

        StandaloneHouseholdService service = new();
        MutableHouseholdBedInventory beds = new();
        HouseholdBirthAssignmentRuntime runtime = new(service, beds);

        Require(runtime.TryResolveBirth(
                childOne,
                firstParent,
                secondParent,
                firstParent,
                80,
                out HouseholdBirthAssignmentResolution unassigned,
                out _)
            && unassigned
                == HouseholdBirthAssignmentResolution.PendingParentHousehold
            && service.Assignments.Count == 0
            && service.TryGetPending(childOne, out var pendingWithoutHousehold)
            && string.IsNullOrWhiteSpace(
                pendingWithoutHousehold.preferredHouseholdId),
            "A birth without a parent household invented an assignment or lost its explicit pending state.");

        HouseholdWorldSaveData pendingSave = JsonUtility.FromJson<
            HouseholdWorldSaveData>(JsonUtility.ToJson(service.Capture()));
        service.Restart(pendingSave);
        runtime = new HouseholdBirthAssignmentRuntime(service, beds);
        service.Assign(
            firstParent,
            household,
            parentRoom,
            parentBed,
            0);
        beds.Set(
            new HouseholdBedCandidate(nurseryRoom, bunkBed, 0, 0.8f),
            new HouseholdBedCandidate(nurseryRoom, bunkBed, 1, 0.8f));
        Require(runtime.TryResumePending(out _)
                && service.TryGet(childOne, out var firstAssignment)
                && firstAssignment.householdId == household.Value
                && firstAssignment.bedBuildingId == bunkBed.Value
                && firstAssignment.bedSlotIndex == 0
                && !service.TryGetPending(childOne, out _),
            "A saved pending birth did not join its parent's existing household after capacity appeared.");

        Require(runtime.TryResolveBirth(
                childTwo,
                firstParent,
                secondParent,
                firstParent,
                81,
                out HouseholdBirthAssignmentResolution secondResolution,
                out _)
            && secondResolution == HouseholdBirthAssignmentResolution.Assigned
            && service.TryGet(childTwo, out var secondAssignment)
            && secondAssignment.bedBuildingId == bunkBed.Value
            && secondAssignment.bedSlotIndex == 1,
            "A two-person authored bed did not expose its second stable slot.");

        Require(runtime.TryResolveBirth(
                childThree,
                firstParent,
                secondParent,
                firstParent,
                82,
                out HouseholdBirthAssignmentResolution fullResolution,
                out _)
            && fullResolution
                == HouseholdBirthAssignmentResolution.PendingBedCapacity
            && service.TryGetPending(childThree, out var fullPending)
            && fullPending.preferredHouseholdId == household.Value,
            "Exhausted bed capacity was not saved as an explicit pending assignment.");

        HouseholdWorldSaveData capacitySave = JsonUtility.FromJson<
            HouseholdWorldSaveData>(JsonUtility.ToJson(service.Capture()));
        Require(capacitySave.version == HouseholdWorldSaveData.CurrentVersion
                && capacitySave.assignments.Count == 3
                && capacitySave.pendingAssignments.Count == 1,
            "Household slot and pending state did not survive JSON capture.");
        service.Restart(capacitySave);
        beds.Set(
            new HouseholdBedCandidate(nurseryRoom, bunkBed, 0, 0.8f),
            new HouseholdBedCandidate(nurseryRoom, bunkBed, 1, 0.8f),
            new HouseholdBedCandidate(nurseryRoom, addedBed, 0, 0.9f));
        runtime = new HouseholdBirthAssignmentRuntime(service, beds);
        Require(runtime.TryResumePending(out _)
                && service.TryGet(childThree, out var recovered)
                && recovered.householdId == household.Value
                && recovered.bedBuildingId == addedBed.Value
                && recovered.bedSlotIndex == 0
                && service.PendingAssignments.Count == 0,
            "A fresh runtime did not allocate newly available capacity to the saved pending birth.");
        HouseholdWorldSaveData recoveredSave = JsonUtility.FromJson<
            HouseholdWorldSaveData>(JsonUtility.ToJson(service.Capture()));
        Require(recoveredSave.version == HouseholdWorldSaveData.CurrentVersion
                && recoveredSave.assignments.Count == 4
                && recoveredSave.pendingAssignments.Count == 0,
            "Recovered household state did not retain every exact assignment.");

        HouseholdWorldSaveData legacy = new()
        {
            version = 1,
            assignments = new List<CharacterRoomAssignmentSaveData>
            {
                new()
                {
                    characterId = firstParent.Value,
                    householdId = household.Value,
                    roomBuildingId = parentRoom.Value,
                    bedBuildingId = parentBed.Value
                }
            }
        };
        CharacterHouseholdAggregate migrated =
            CharacterHouseholdAggregate.Restore(legacy);
        Require(migrated.TryGet(firstParent, out var migratedAssignment)
                && migratedAssignment.bedSlotIndex == 0
                && migrated.Capture().version
                    == HouseholdWorldSaveData.CurrentVersion,
            "Household v1 did not migrate its one-person bed to slot zero.");
        return new HouseholdBirthEvidence(
            pendingSave,
            capacitySave,
            recoveredSave);
    }

    private static void RequireBirthBoundaryFailure(
        TransitionFixture fixture,
        string processId,
        ReproductionBirthTransactionPhase retainedPhase,
        string boundary)
    {
        fixture.BirthPort.FailBoundary = boundary;
        Require(!fixture.Births.TryPublish(processId, 70, out _),
            $"Injected birth {boundary} failure unexpectedly succeeded.");
        ReproductionProcess pending = fixture.RequireProcess(processId);
        Require(!pending.ResultPublished
                && pending.PendingBirth?.phase == retainedPhase,
            $"Birth {boundary} failure did not retain the exact recovery phase.");
        ReproductionWorldSaveData roundTrip = RoundTrip(
            fixture.Persistence.Capture());
        Require(roundTrip.version == ReproductionWorldSaveData.CurrentVersion
                && roundTrip.processes.Single(value =>
                        value.processId == processId)
                    .pendingBirth.phase == retainedPhase,
            $"Birth {boundary} recovery phase did not survive JSON save data.");
        fixture.Persistence.Restart(roundTrip);
        fixture.RestartBirthCommand();
    }

    private static PlanStartEvidence VerifyRealPlanStartAndRecovery()
    {
        using PlanFixture fixture = new();
        ReproductionPlanRequest request = fixture.CreateRequest("qa-plan-start");

        fixture.Facts.FailNextCommit = true;
        Require(!fixture.Command.TryPlan(request, out string rejectedId, out _)
                && rejectedId.Length == 0,
            "A rejected plan outcome was reported as successful.");
        Require(fixture.Reproduction.Processes.Count == 0,
            "A rejected plan left a reproduction process behind.");
        Require(Math.Abs(fixture.Equipment.AppliedWear) <= 0.000001d,
            "Rejected plan outcome did not restore breeding-ledger wear.");
        Require(fixture.OutcomeEnvironment.ReproductionRows().Count == 0,
            "Rejected plan outcome reached the durable ledger.");

        Require(fixture.Command.TryPlan(request, out string processId, out _),
            "The real reproduction plan command failed.");
        ReproductionProcess planned = fixture.RequireProcess(processId);
        Require(planned.Status == ReproductionProcessStatus.Planned
                && planned.TransitionRevision == 1L
                && !string.IsNullOrWhiteSpace(planned.LastPublicationKey)
                && !string.IsNullOrWhiteSpace(planned.LastPublicationId)
                && !string.IsNullOrWhiteSpace(planned.LastPublicationFingerprint),
            "The planned process did not retain its committed transition identity.");
        Require(Math.Abs(
                fixture.Equipment.AppliedWear
                - ReproductionDurableEquipmentUseRuntime.LedgerWearPerPlan)
            <= 0.000001d,
            "Successful planning did not apply exactly one breeding-ledger wear cost.");
        GameplayOutcomeSnapshot planOutcome = fixture.RequireOutcome(
            processId,
            "planned");
        string durableCost = Fact(
            planOutcome,
            ReproductionProcessTransitionOutcomeIds.Costs);
        Require(durableCost.StartsWith(
                    "durable:semantics=wear;item="
                    + DurableToolItemRules.BreedingLedger
                    + ";stack=stack:qa:breeding-ledger;facility="
                    + FacilityInstanceId
                    + ";requirement="
                    + ReproductionDurableEquipmentPolicySource.RequirementId,
                    StringComparison.Ordinal)
                && durableCost.Contains(
                    ";revision=1->2;quantity=1->1;durability=10->9;maximum=10;requestedWear=1;spent=1",
                    StringComparison.Ordinal)
                && Fact(planOutcome,
                    ReproductionProcessTransitionOutcomeIds.FacilityId)
                == FacilityInstanceId,
            "The plan outcome lost its exact durable cost or facility.");
        fixture.RequirePlanParticipants(planOutcome);

        fixture.Facts.FailNextPrepare = true;
        Require(!fixture.Command.TryStart(processId, out _),
            "Injected start prepare rejection unexpectedly succeeded.");
        ReproductionProcess consumed = fixture.RequireProcess(processId);
        Require(consumed.Status == ReproductionProcessStatus.Planned
                && consumed.PendingStart?.phase
                    == ReproductionStartTransactionPhase.MaterialsConsumed,
            "Start failure did not persist the recoverable materials-consumed phase.");
        Require(fixture.Stock.Quantity(FertilityStackId) == 1
                && fixture.Atomic.ConsumeCalls == 1,
            "Start did not consume the exact fertility-treatment quantity once.");

        ReproductionWorldSaveData save = RoundTrip(fixture.Reproduction.Capture());
        Require(save.version == ReproductionWorldSaveData.CurrentVersion
                && save.processes.Single().pendingStart.phase
                    == ReproductionStartTransactionPhase.MaterialsConsumed,
            "The start recovery phase did not round-trip through JSON save data.");
        fixture.Restart(save);
        Require(fixture.Command.TryResumePendingStarts(out _),
            "A fresh runtime did not resume the pending start transaction.");
        ReproductionProcess active = fixture.RequireProcess(processId);
        Require(active.Status == ReproductionProcessStatus.Active
                && active.PendingStart == null
                && !string.IsNullOrWhiteSpace(active.LastPublicationKey)
                && !string.IsNullOrWhiteSpace(active.LastPublicationId)
                && !string.IsNullOrWhiteSpace(active.LastPublicationFingerprint)
                && active.LastPublicationKey != planned.LastPublicationKey,
            "Recovered start did not activate and close its transaction.");
        Require(fixture.Stock.Quantity(FertilityStackId) == 1
                && fixture.Atomic.ConsumeCalls == 1,
            "Recovered start consumed the same physical material twice.");

        GameplayOutcomeSnapshot startOutcome = fixture.RequireOutcome(
            processId,
            "started");
        Require(Fact(startOutcome, ReproductionProcessTransitionOutcomeIds.Costs)
                == FertilityItemId + "@" + FertilityStackId + "x1"
                && startOutcome.parentRunId == planOutcome.runId
                && startOutcome.parentSequence == planOutcome.sequence,
            "Start outcome lost its exact material cost or planned parent.");
        return new PlanStartEvidence(planOutcome, startOutcome, save);
    }

    private static DailyTransitionEvidence
        VerifyDailyTransitionRollbackWaitingFailureAndCompletion()
    {
        using TransitionFixture fixture = new("run:qa:reproduction-daily");
        ReproductionProcess completing = fixture.AddActiveEgg(
            "reproduction:qa:daily-complete");
        ReproductionProcessSaveData before = completing.Capture();
        fixture.Facts.FailNextCommit = true;
        Require(!fixture.Transitions.TryAdvance(
                completing.ProcessId,
                SafeDay(20),
                out _),
            "Injected daily outcome rejection unexpectedly succeeded.");
        ReproductionProcess afterRejected = fixture.RequireProcess(
            completing.ProcessId);
        Require(afterRejected.TransitionRevision == before.transitionRevision
                && afterRejected.TotalProgressDays == before.totalProgressDays
                && fixture.OutcomeEnvironment.ReproductionRows().Count == 0,
            "Rejected daily outcome did not roll back the process exactly.");

        Require(fixture.Transitions.TryAdvance(
                completing.ProcessId,
                SafeDay(20),
                out _)
            && fixture.Transitions.TryAdvance(
                completing.ProcessId,
                SafeDay(21),
                out _),
            "Safe daily transitions failed.");
        ReproductionProcess completed = fixture.RequireProcess(
            completing.ProcessId);
        Require(completed.Status == ReproductionProcessStatus.Completed
                && completed.TotalProgressDays == 2
                && completed.TransitionRevision == 2L,
            "The short egg process did not complete after its authored duration.");

        ReproductionProcess failing = fixture.AddActiveEgg(
            "reproduction:qa:daily-environment-failure");
        for (int day = 30; day <= 32; day++)
            Require(fixture.Transitions.TryAdvance(
                    failing.ProcessId,
                    UnsafeDay(day),
                    out _),
                "Unsafe environment transition failed to commit.");
        ReproductionProcess failed = fixture.RequireProcess(failing.ProcessId);
        Require(failed.Status == ReproductionProcessStatus.Failed
                && failed.Failure
                    == ReproductionFailureCode.IncubationEnvironmentFailed
                && failed.ConsecutiveUnsafeTemperatureDays == 3,
            "Three unsafe incubation days did not produce the exact failure.");
        GameplayOutcomeSnapshot[] environmentRows = fixture.OutcomeEnvironment
            .RowsForProcess(failing.ProcessId)
            .OrderBy(value => value.ownerRevision)
            .ToArray();
        Require(environmentRows.Length == 3
                && Fact(environmentRows[0],
                    ReproductionProcessTransitionOutcomeIds.StatusAfter)
                    == "waiting-environment"
                && Fact(environmentRows[1],
                    ReproductionProcessTransitionOutcomeIds.StatusAfter)
                    == "waiting-environment"
                && Fact(environmentRows[2],
                    ReproductionProcessTransitionOutcomeIds.FailureAfter)
                    == "incubation-environment-failed",
            "Waiting and terminal incubation outcomes were not distinguishable.");

        ReproductionWorldSaveData roundTrip = RoundTrip(fixture.Persistence.Capture());
        fixture.Persistence.Restart(roundTrip);
        ReproductionProcess restoredComplete = fixture.RequireProcess(
            completing.ProcessId);
        ReproductionProcess restoredFailure = fixture.RequireProcess(
            failing.ProcessId);
        Require(restoredComplete.Status == ReproductionProcessStatus.Completed
                && restoredFailure.Status == ReproductionProcessStatus.Failed
                && !string.IsNullOrWhiteSpace(restoredComplete.LastPublicationKey)
                && !string.IsNullOrWhiteSpace(restoredComplete.LastPublicationId)
                && !string.IsNullOrWhiteSpace(restoredComplete.LastPublicationFingerprint)
                && !string.IsNullOrWhiteSpace(restoredFailure.LastPublicationKey)
                && !string.IsNullOrWhiteSpace(restoredFailure.LastPublicationId)
                && !string.IsNullOrWhiteSpace(restoredFailure.LastPublicationFingerprint),
            "Daily transition states or their last outcome identities did not round-trip.");
        return new DailyTransitionEvidence(
            fixture.OutcomeEnvironment
                .RowsForProcess(completing.ProcessId)
                .OrderBy(value => value.ownerRevision)
                .ToArray(),
            environmentRows,
            roundTrip);
    }

    private static CarrierDeathEvidence VerifyCarrierDeathParentageAndRecovery()
    {
        using TransitionFixture fixture = new("run:qa:reproduction-carrier-death");
        using PlanFixture medicalFixture = new();
        ReproductionProcess early = fixture.AddActivePregnancy(
            "reproduction:qa:carrier-death-early");
        MigratedProducerOutcomeCommitResult earlyParent = fixture.CommitDeathParent(
            early.CarrierId,
            40,
            "death:qa:carrier-early");
        CharacterDeathEvent earlyDeath = Death(
            early.CarrierId,
            40,
            earlyParent);

        fixture.Facts.FailNextPrepare = true;
        Require(!fixture.Transitions.TryNotifyCarrierDeath(earlyDeath, out _),
            "Injected carrier-death prepare rejection unexpectedly succeeded.");
        Require(fixture.Persistence.Capture().pendingCarrierDeaths.Count == 1
                && fixture.Persistence.Capture().pendingCarrierDeaths.Single()
                    .parentPublicationKey == earlyParent.ResultKey.ToString()
                && fixture.RequireProcess(early.ProcessId).Status
                    == ReproductionProcessStatus.Active,
            "Rejected carrier-death transition was not retained as pending work.");
        fixture.Persistence.Restart(RoundTrip(fixture.Persistence.Capture()));
        Require(fixture.Transitions.TryResumePendingCarrierDeaths(out _),
            "Pending carrier death did not recover after a save round-trip.");
        Require(fixture.RequireProcess(early.ProcessId).Failure
                == ReproductionFailureCode.CarrierDied
                && fixture.Persistence.Capture().pendingCarrierDeaths.Count == 0,
            "Early carrier death did not fail and clear its pending notification.");
        fixture.RequireDeathChild(early.ProcessId, earlyParent, "failed");
        GameplayOutcomeSnapshot earlyCarrierDeath = fixture.OutcomeEnvironment
            .RowsForProcess(early.ProcessId)
            .Single(value => Fact(
                    value,
                    ReproductionProcessTransitionOutcomeIds.TransitionKind)
                == "carrier-death");

        ReproductionProcess late = fixture.AddActivePregnancy(
            "reproduction:qa:carrier-death-late");
        for (int day = 50; day <= 53; day++)
            Require(fixture.Transitions.TryAdvance(
                    late.ProcessId,
                    SafeDay(day),
                    out _),
                "Late pregnancy setup could not reach 80 percent progress.");
        Require(fixture.RequireProcess(late.ProcessId).ProgressRatio >= 0.8f,
            "Late pregnancy fixture did not reach the extraction threshold.");
        MigratedProducerOutcomeCommitResult lateParent = fixture.CommitDeathParent(
            late.CarrierId,
            54,
            "death:qa:carrier-late");
        Require(fixture.Transitions.TryNotifyCarrierDeath(
                Death(late.CarrierId, 54, lateParent),
                out _),
            "Late carrier-death transition failed.");
        Require(fixture.RequireProcess(late.ProcessId).Status
                == ReproductionProcessStatus.WaitingForEmergencyExtraction
                && fixture.Alerts.IsActive(late.ProcessId),
            "Late carrier death did not enter emergency extraction waiting.");
        fixture.RequireDeathChild(
            late.ProcessId,
            lateParent,
            "waiting-emergency-extraction");

        ReproductionWorldSaveData waitingSave = RoundTrip(
            fixture.Persistence.Capture());
        fixture.Persistence.Restart(waitingSave);
        fixture.Calendar.SetDateTime(54, 9);
        ReproductionEmergencyExtractionCommandRuntime command = new(
            fixture.Persistence,
            fixture.Publications,
            new ReproductionEmergencyResponderQuery(medicalFixture.World),
            fixture.Alerts,
            fixture.Calendar);
        ReproductionEmergencyExtractionRequest request = new(
            late.ProcessId,
            medicalFixture.FirstId);
        fixture.Facts.FailNextPrepare = true;
        Require(!command.TryExecute(request, out _)
                && fixture.RequireProcess(late.ProcessId).Status
                    == ReproductionProcessStatus.WaitingForEmergencyExtraction
                && fixture.Alerts.IsActive(late.ProcessId),
            "Rejected extraction did not retain its waiting process and alert.");
        bool extractionSucceeded = command.TryExecute(
            request,
            out DomainFailure extractionFailure);
        Require(extractionSucceeded,
            "Restored in-deadline emergency extraction command failed: "
            + extractionFailure.Code + " "
            + string.Join(",", extractionFailure.Parameters.ToArray()));
        ReproductionProcess extracted = fixture.RequireProcess(late.ProcessId);
        Require(extracted.Status == ReproductionProcessStatus.Active
                && extracted.EmergencyExtracted
                && !fixture.Alerts.IsActive(late.ProcessId),
            "Committed emergency extraction did not resume the process or clear alert.");
        GameplayOutcomeSnapshot extraction = fixture.OutcomeEnvironment
            .RowsForProcess(late.ProcessId)
            .Single(value => Fact(
                    value,
                    ReproductionProcessTransitionOutcomeIds.TransitionKind)
                == "emergency-extraction");
        GameplayOutcomeSnapshot carrierDeath = fixture.OutcomeEnvironment
            .RowsForProcess(late.ProcessId)
            .Single(value => Fact(
                    value,
                    ReproductionProcessTransitionOutcomeIds.TransitionKind)
                == "carrier-death");
        Require(extraction.participants.Any(value =>
                    value.entityId == medicalFixture.FirstId.Value
                    && value.roleId == ReproductionProcessTransitionOutcomeIds
                        .MedicalResponderRole.Value)
                && Metric(
                    extraction,
                    ReproductionProcessTransitionOutcomeIds.EmergencyDeadlineDay)
                    == 55d
                && extraction.parentRunId == carrierDeath.runId
                && extraction.parentSequence == carrierDeath.sequence
                && extraction.causationRelationId == "process-transition",
            "Extraction lost responder, deadline, target process, or causal parent.");

        using TransitionFixture expiryFixture = new(
            "run:qa:reproduction-extraction-expiry");
        ReproductionEmergencyExtractionCommandRuntime expiryCommand = new(
            expiryFixture.Persistence,
            expiryFixture.Publications,
            new ReproductionEmergencyResponderQuery(medicalFixture.World),
            expiryFixture.Alerts,
            expiryFixture.Calendar);
        ReproductionProcess expired = expiryFixture.AddActivePregnancy(
            "reproduction:qa:carrier-death-expired");
        for (int day = 60; day <= 63; day++)
        {
            bool advanced = expiryFixture.Transitions.TryAdvance(
                expired.ProcessId,
                SafeDay(day),
                out DomainFailure advanceFailure);
            Require(advanced,
                "Expiry fixture could not reach 80 percent progress: "
                + day + " " + advanceFailure.Code + " "
                + string.Join(",", advanceFailure.Parameters.ToArray()));
        }
        MigratedProducerOutcomeCommitResult expiredParent =
            expiryFixture.CommitDeathParent(
                expired.CarrierId,
                64,
                "death:qa:carrier-expired");
        Require(expiryFixture.Transitions.TryNotifyCarrierDeath(
                Death(expired.CarrierId, 64, expiredParent),
                out _)
            && expiryFixture.Alerts.IsActive(expired.ProcessId),
            "Expired extraction fixture did not publish its alert.");
        expiryFixture.Calendar.SetDateTime(66, 9);
        Require(!expiryCommand.TryExecute(
                new ReproductionEmergencyExtractionRequest(
                    expired.ProcessId,
                    medicalFixture.FirstId),
                out _)
            && expiryFixture.RequireProcess(expired.ProcessId).Status
                == ReproductionProcessStatus.WaitingForEmergencyExtraction,
            "An extraction command after the deadline was not rejected exactly.");
        Require(expiryFixture.Transitions.TryAdvance(
                expired.ProcessId,
                SafeDay(66),
                out _)
            && expiryFixture.RequireProcess(expired.ProcessId).Failure
                == ReproductionFailureCode.EmergencyExtractionExpired
            && !expiryFixture.Alerts.IsActive(expired.ProcessId),
            "Daily expiry did not close the process and its red alert.");
        return new CarrierDeathEvidence(
            earlyCarrierDeath,
            carrierDeath,
            extraction,
            waitingSave,
            late.ProcessId);
    }

    private static void VerifyCarrierDeathObservationPendingAndConflict()
    {
        using TransitionFixture fixture = new(
            "run:qa:reproduction-carrier-death-observation-pending",
            observations => new HideFirstOutcomeObservation(observations));
        ReproductionProcess process = fixture.AddActivePregnancy(
            "reproduction:qa:carrier-death-observation-pending");
        MigratedProducerOutcomeCommitResult parent = fixture.CommitDeathParent(
            process.CarrierId,
            70,
            "death:qa:carrier-observation-parent");
        MigratedProducerOutcomeCommitResult changedParent =
            fixture.CommitDeathParent(
                process.CarrierId,
                70,
                "death:qa:carrier-observation-conflict");
        CharacterDeathEvent death = Death(process.CarrierId, 70, parent);

        Require(!fixture.Transitions.TryNotifyCarrierDeath(death, out _)
                && fixture.RequireProcess(process.ProcessId).Status
                    == ReproductionProcessStatus.Active
                && fixture.Persistence.Capture().pendingCarrierDeaths.Count == 1
                && fixture.Persistence.Capture().pendingCarrierDeaths.Single()
                    .parentPublicationKey == parent.ResultKey.ToString(),
            "Temporarily unavailable parent observation changed the process or lost pending work.");
        VerifyCarrierDeathSaveJoin(fixture, parent);

        bool conflict = false;
        try
        {
            fixture.Transitions.TryNotifyCarrierDeath(
                Death(process.CarrierId, 70, changedParent),
                out _);
        }
        catch (InvalidOperationException)
        {
            conflict = true;
        }
        Require(conflict
                && fixture.Persistence.Capture().pendingCarrierDeaths.Single()
                    .parentPublicationKey == parent.ResultKey.ToString(),
            "The same carrier-death notification accepted a changed parent key.");

        Require(fixture.Transitions.TryResumePendingCarrierDeaths(out _)
                && fixture.RequireProcess(process.ProcessId).Failure
                    == ReproductionFailureCode.CarrierDied
                && fixture.Persistence.Capture().pendingCarrierDeaths.Count == 0,
            "Known exact parent observation did not resume the pending carrier death.");
        fixture.RequireDeathChild(process.ProcessId, parent, "failed");
        int committedChildren = fixture.OutcomeEnvironment
            .RowsForProcess(process.ProcessId)
            .Count(value => Fact(
                    value,
                    ReproductionProcessTransitionOutcomeIds.TransitionKind)
                == "carrier-death");
        Require(committedChildren == 1
                && fixture.Transitions.TryResumePendingCarrierDeaths(out _)
                && fixture.OutcomeEnvironment.RowsForProcess(process.ProcessId)
                    .Count(value => Fact(
                            value,
                            ReproductionProcessTransitionOutcomeIds
                                .TransitionKind)
                        == "carrier-death") == 1,
            "Carrier-death parent recovery published more than one child.");
    }

    private static void VerifyCarrierDeathSaveJoin(
        TransitionFixture fixture,
        MigratedProducerOutcomeCommitResult parent)
    {
        ReproductionWorldSaveData reproduction = fixture.Persistence.Capture();
        GameplayOutcomeLedgerSaveData ledger =
            fixture.OutcomeEnvironment.Ledger.CaptureGameplayOutcomes();
        var validReport = new DungeonGameRestoreReport();
        ReproductionDeathParentPreflight.Validate(
            reproduction,
            ledger,
            validReport);
        Require(validReport.Success,
            "Canonical carrier-death pending work did not join its saved parent.");

        ReproductionWorldSaveData wrongKey = RoundTrip(reproduction);
        wrongKey.pendingCarrierDeaths.Single().parentPublicationKey += ":wrong";
        var wrongKeyReport = new DungeonGameRestoreReport();
        ReproductionDeathParentPreflight.Validate(
            wrongKey,
            ledger,
            wrongKeyReport);
        Require(!wrongKeyReport.Success,
            "Carrier-death preflight accepted a changed parent publication key.");

        GameplayOutcomeLedgerSaveData missingParent =
            fixture.OutcomeEnvironment.Ledger.CaptureGameplayOutcomes();
        GameplayResultKey key = parent.ResultKey;
        int removed = missingParent.outbox.RemoveAll(value => value != null
                && value.producerId == key.ProducerId
                && value.operationId == key.OperationId.Value
                && value.commitRevision == key.CommitRevision
                && value.localResultIndex == key.LocalResultIndex)
            + missingParent.exactOutcomes.RemoveAll(value => value != null
                && value.producerId == key.ProducerId
                && value.operationId == key.OperationId.Value
                && value.commitRevision == key.CommitRevision
                && value.localResultIndex == key.LocalResultIndex)
            + missingParent.tombstones.RemoveAll(value => value != null
                && value.producerId == key.ProducerId
                && value.operationId == key.OperationId.Value
                && value.commitRevision == key.CommitRevision
                && value.localResultIndex == key.LocalResultIndex);
        var missingParentReport = new DungeonGameRestoreReport();
        ReproductionDeathParentPreflight.Validate(
            reproduction,
            missingParent,
            missingParentReport);
        Require(removed == 1 && !missingParentReport.Success,
            "Carrier-death preflight accepted a missing canonical parent row.");

        string json = JsonUtility.ToJson(reproduction);
        JObject missingField = JObject.Parse(json);
        ((JObject)missingField["pendingCarrierDeaths"][0])
            .Remove("parentPublicationKey");
        RequireInvalidOperation(
            () => ReproductionSaveSection.ValidateDeathNotificationJson(
                missingField.ToString()),
            "Current reproduction save accepted a missing death parent field.");
        JObject nullField = JObject.Parse(json);
        ((JObject)nullField["pendingCarrierDeaths"][0])
            ["parentPublicationKey"] = JValue.CreateNull();
        RequireInvalidOperation(
            () => ReproductionSaveSection.ValidateDeathNotificationJson(
                nullField.ToString()),
            "Current reproduction save accepted a null death parent field.");
    }

    private static CharacterDeathEvent Death(
        CharacterId carrier,
        int day,
        MigratedProducerOutcomeCommitResult parent) => new(
        carrier,
        CharacterDeathCauseCode.Combat,
        day,
        new CoreGridCell(4, 5),
        Array.Empty<CharacterId>(),
        parent.ResultKey.ToString());

    private static ReproductionDailyContext SafeDay(int day) => new(
        day,
        carrierHealth: 100f,
        carrierNutrition: 100f,
        environmentTemperature: 20f);

    private static ReproductionDailyContext UnsafeDay(int day) => new(
        day,
        carrierHealth: 100f,
        carrierNutrition: 100f,
        environmentTemperature: -50f);

    private static ReproductionWorldSaveData RoundTrip(
        ReproductionWorldSaveData value) =>
        JsonUtility.FromJson<ReproductionWorldSaveData>(
            JsonUtility.ToJson(value));

    private static string Fact(
        GameplayOutcomeSnapshot row,
        GameplayOutcomeFactId id) => row.facts.Single(value =>
            string.Equals(value.factId, id.Value, StringComparison.Ordinal)).value;

    private static double Metric(
        GameplayOutcomeSnapshot row,
        GameplayMetricId id) => row.metrics.Single(value =>
            string.Equals(value.metricId, id.Value, StringComparison.Ordinal)).value;

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    private static void RequireInvalidOperation(Action action, string message)
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

    private sealed class ReproductionTransitionEvidence
    {
        internal ReproductionTransitionEvidence(
            PlanStartEvidence plan,
            DailyTransitionEvidence daily,
            CarrierDeathEvidence carrierDeath)
        {
            Plan = plan ?? throw new ArgumentNullException(nameof(plan));
            Daily = daily ?? throw new ArgumentNullException(nameof(daily));
            CarrierDeath = carrierDeath
                ?? throw new ArgumentNullException(nameof(carrierDeath));
        }

        internal PlanStartEvidence Plan { get; }
        internal DailyTransitionEvidence Daily { get; }
        internal CarrierDeathEvidence CarrierDeath { get; }
    }

    private sealed class PlanStartEvidence
    {
        internal PlanStartEvidence(
            GameplayOutcomeSnapshot planOutcome,
            GameplayOutcomeSnapshot startOutcome,
            ReproductionWorldSaveData pendingStartSave)
        {
            PlanOutcome = planOutcome
                ?? throw new ArgumentNullException(nameof(planOutcome));
            StartOutcome = startOutcome
                ?? throw new ArgumentNullException(nameof(startOutcome));
            PendingStartSave = pendingStartSave
                ?? throw new ArgumentNullException(nameof(pendingStartSave));
        }

        internal GameplayOutcomeSnapshot PlanOutcome { get; }
        internal GameplayOutcomeSnapshot StartOutcome { get; }
        internal ReproductionWorldSaveData PendingStartSave { get; }
    }

    private sealed class DailyTransitionEvidence
    {
        internal DailyTransitionEvidence(
            GameplayOutcomeSnapshot[] completionOutcomes,
            GameplayOutcomeSnapshot[] waitingFailureOutcomes,
            ReproductionWorldSaveData restoredSave)
        {
            CompletionOutcomes = completionOutcomes
                ?? throw new ArgumentNullException(nameof(completionOutcomes));
            WaitingFailureOutcomes = waitingFailureOutcomes
                ?? throw new ArgumentNullException(
                    nameof(waitingFailureOutcomes));
            RestoredSave = restoredSave
                ?? throw new ArgumentNullException(nameof(restoredSave));
        }

        internal GameplayOutcomeSnapshot[] CompletionOutcomes { get; }
        internal GameplayOutcomeSnapshot[] WaitingFailureOutcomes { get; }
        internal ReproductionWorldSaveData RestoredSave { get; }
    }

    private sealed class CarrierDeathEvidence
    {
        internal CarrierDeathEvidence(
            GameplayOutcomeSnapshot earlyCarrierDeathOutcome,
            GameplayOutcomeSnapshot lateCarrierDeathOutcome,
            GameplayOutcomeSnapshot extractionOutcome,
            ReproductionWorldSaveData waitingSave,
            string lateProcessId)
        {
            EarlyCarrierDeathOutcome = earlyCarrierDeathOutcome
                ?? throw new ArgumentNullException(
                    nameof(earlyCarrierDeathOutcome));
            LateCarrierDeathOutcome = lateCarrierDeathOutcome
                ?? throw new ArgumentNullException(
                    nameof(lateCarrierDeathOutcome));
            ExtractionOutcome = extractionOutcome
                ?? throw new ArgumentNullException(nameof(extractionOutcome));
            WaitingSave = waitingSave
                ?? throw new ArgumentNullException(nameof(waitingSave));
            LateProcessId = string.IsNullOrWhiteSpace(lateProcessId)
                ? throw new ArgumentException(
                    "Late process identity is required.",
                    nameof(lateProcessId))
                : lateProcessId;
        }

        internal GameplayOutcomeSnapshot EarlyCarrierDeathOutcome { get; }
        internal GameplayOutcomeSnapshot LateCarrierDeathOutcome { get; }
        internal GameplayOutcomeSnapshot ExtractionOutcome { get; }
        internal ReproductionWorldSaveData WaitingSave { get; }
        internal string LateProcessId { get; }
    }

    private sealed class HouseholdBirthEvidence
    {
        internal HouseholdBirthEvidence(
            HouseholdWorldSaveData initialPendingSave,
            HouseholdWorldSaveData capacityPendingSave,
            HouseholdWorldSaveData recoveredSave)
        {
            InitialPendingSave = initialPendingSave
                ?? throw new ArgumentNullException(nameof(initialPendingSave));
            CapacityPendingSave = capacityPendingSave
                ?? throw new ArgumentNullException(nameof(capacityPendingSave));
            RecoveredSave = recoveredSave
                ?? throw new ArgumentNullException(nameof(recoveredSave));
        }

        internal HouseholdWorldSaveData InitialPendingSave { get; }
        internal HouseholdWorldSaveData CapacityPendingSave { get; }
        internal HouseholdWorldSaveData RecoveredSave { get; }
    }

    private sealed class BirthTransactionEvidence
    {
        internal BirthTransactionEvidence(
            GameplayOutcomeSnapshot birthOutcome,
            ReproductionProcessSaveData processSave,
            string childId,
            IReadOnlyDictionary<string, int> authorityCounts)
        {
            BirthOutcome = birthOutcome
                ?? throw new ArgumentNullException(nameof(birthOutcome));
            ProcessSave = processSave
                ?? throw new ArgumentNullException(nameof(processSave));
            ChildId = childId?.Trim() ?? string.Empty;
            AuthorityCounts = new Dictionary<string, int>(
                authorityCounts
                    ?? throw new ArgumentNullException(nameof(authorityCounts)),
                StringComparer.Ordinal);
        }

        internal GameplayOutcomeSnapshot BirthOutcome { get; }
        internal ReproductionProcessSaveData ProcessSave { get; }
        internal string ChildId { get; }
        internal IReadOnlyDictionary<string, int> AuthorityCounts { get; }
    }

    private sealed class ProductionBirthPublicationEvidence
    {
        internal ProductionBirthPublicationEvidence(
            GameplayOutcomeSnapshot birthOutcome,
            ReproductionProcessSaveData processSave,
            string childId,
            int actorCount,
            int lifeCount,
            int narrativeCount,
            int populationProfileCount,
            CharacterSettlementStanding populationStanding,
            string[] parentIds,
            string guardianId,
            int housingCallCount,
            IReadOnlyDictionary<string, int> authorityCountsAfterReplay)
        {
            BirthOutcome = birthOutcome
                ?? throw new ArgumentNullException(nameof(birthOutcome));
            ProcessSave = processSave
                ?? throw new ArgumentNullException(nameof(processSave));
            ChildId = childId?.Trim() ?? string.Empty;
            ActorCount = actorCount;
            LifeCount = lifeCount;
            NarrativeCount = narrativeCount;
            PopulationProfileCount = populationProfileCount;
            PopulationStanding = populationStanding;
            ParentIds = parentIds ?? Array.Empty<string>();
            GuardianId = guardianId?.Trim() ?? string.Empty;
            HousingCallCount = housingCallCount;
            AuthorityCountsAfterReplay = new Dictionary<string, int>(
                authorityCountsAfterReplay
                    ?? throw new ArgumentNullException(
                        nameof(authorityCountsAfterReplay)),
                StringComparer.Ordinal);
        }

        internal GameplayOutcomeSnapshot BirthOutcome { get; }
        internal ReproductionProcessSaveData ProcessSave { get; }
        internal string ChildId { get; }
        internal int ActorCount { get; }
        internal int LifeCount { get; }
        internal int NarrativeCount { get; }
        internal int PopulationProfileCount { get; }
        internal CharacterSettlementStanding PopulationStanding { get; }
        internal string[] ParentIds { get; }
        internal string GuardianId { get; }
        internal int HousingCallCount { get; }
        internal IReadOnlyDictionary<string, int> AuthorityCountsAfterReplay
        {
            get;
        }
    }

    private sealed class ProductionBirthPublicationFixture : IDisposable
    {
        private const string ProcessId =
            "reproduction:qa:production-birth-publication";
        private static readonly CharacterId FirstParentId = new(
            "character:qa:production-birth:first-parent");
        private static readonly CharacterId SecondParentId = new(
            "character:qa:production-birth:second-parent");

        private readonly ResourceGameContentCatalog content;
        private readonly ResourceCharacterSpeciesCatalog definitions;
        private readonly CharacterLifeRuntime life;
        private readonly CharacterNarrativeRuntime narrative;
        private readonly CharacterKinshipAggregate kinship = new();
        private readonly MutableBirthWorld world = new();
        private readonly RecordingBirthPopulationService population = new();
        private readonly RecordingHouseholdBirthCommand housing = new();
        private readonly BirthSpawnObjectFactory characterObjects;
        private readonly StandaloneReproductionPersistence persistence;
        private readonly OutcomeEnvironment outcomes;
        private readonly GameEventBus events = new();
        private readonly ReproductionApplicationAdapter adapter;
        private readonly GameObject spawnerObject;
        private readonly GameObject prefabMarker;
        private readonly CharacterSO authored;

        internal ProductionBirthPublicationFixture()
        {
            content = new ResourceGameContentCatalog(
                new UnityGameContentRootLoader());
            definitions = new ResourceCharacterSpeciesCatalog(content);
            authored = content.GetAll<CharacterSO>()
                .Where(value => value != null
                    && value.species != null
                    && value.species.reproduction != null
                    && value.species.reproduction.mode
                        != ReproductionMode.GolemAssembly
                    && value.role == CharacterRole.Regular)
                .OrderBy(value => value.DefinitionId.Value, StringComparer.Ordinal)
                .FirstOrDefault()
                ?? throw new InvalidOperationException(
                    "No authored reproductive character is available for the production birth fixture.");
            CharacterSpeciesId speciesId = new(authored.SpeciesTag);
            ReproductionDefinition definition = definitions
                .RequireReproduction(speciesId);

            CharacterActor parent = CreateParentActor(
                authored,
                speciesId,
                FirstParentId,
                FirstParentRole(definition.Mode));
            world.Add(parent);

            var lifeStore = new DungeonRuntimeAggregateRootStore();
            life = new CharacterLifeRuntime(
                lifeStore,
                definitions,
                new RandomStreamProvider(70117));
            narrative = new CharacterNarrativeRuntime(
                new DungeonRuntimeAggregateRootStore(),
                new CharacterNarrativeCatalog(content));

            characterObjects = new BirthSpawnObjectFactory(world);
            spawnerObject = new GameObject("Production Birth Test Spawner");
            CharacterSpawner spawner = spawnerObject
                .AddComponent<CharacterSpawner>();
            prefabMarker = new GameObject("Production Birth Prefab Marker");
            spawner.characterPrefab = prefabMarker;
            var spawners = new FixedBirthSpawnerProvider(spawner);

            var publication = new ReproductionBirthPublicationPort(
                world,
                definitions,
                life,
                life,
                narrative,
                narrative,
                kinship,
                kinship,
                population,
                housing,
                content,
                characterObjects,
                spawners);

            persistence = new StandaloneReproductionPersistence(definitions);
            ReproductionWorldAggregate candidate = persistence.PrepareRestore(
                persistence.Capture());
            var process = new ReproductionProcess(
                ProcessId,
                FirstParentId,
                SecondParentId,
                FirstParentId,
                speciesId,
                definition,
                startedAbsoluteDay: 1,
                crossLineageIncubatorUsed: false,
                supportFacilityInstanceId: FacilityInstanceId,
                expressedTraitIds: Array.Empty<string>(),
                latentTraitIds: Array.Empty<string>(),
                aptitudes: Array.Empty<InnateAptitudeSaveData>(),
                startActive: true);
            float safeTemperature = (definition.ViableTemperatureMinimum
                + definition.ViableTemperatureMaximum) * 0.5f;
            int day = 2;
            int lastDay = checked(2 + Math.Max(1, definition.TotalDurationDays));
            while (process.Status == ReproductionProcessStatus.Active
                   && day <= lastDay + 4)
            {
                process.AdvanceDay(
                    new ReproductionDailyContext(
                        day,
                        100f,
                        100f,
                        safeTemperature),
                    0d);
                day++;
            }
            Require(process.Status == ReproductionProcessStatus.Completed,
                "Authored production birth fixture did not reach completion.");
            candidate.Add(process);
            persistence.PublishRestore(candidate);

            outcomes = new OutcomeEnvironment(
                "run:qa:reproduction-production-birth");
            var transitionFacts = new FaultingTransitionFacts(outcomes.Facts);
            var transitionPublications = new ReproductionTransitionPublication(
                persistence,
                new ReproductionTransitionFactSource(outcomes.Names),
                transitionFacts,
                transitionFacts,
                new RecordingSettlementAlertService());
            var births = new ReproductionBirthTransactionRuntime(
                persistence,
                publication,
                transitionPublications);
            adapter = new ReproductionApplicationAdapter(
                new PersistenceBackedReproductionService(persistence),
                new UnexpectedReproductionTransitionCommand(),
                world,
                new UnusedBodyHealthQuery(),
                new FixedClimateQuery(safeTemperature),
                definitions,
                life,
                births,
                events,
                NeutralBirthHeritableTraits.Instance);
            adapter.Start();
        }

        internal ProductionBirthPublicationEvidence Execute()
        {
            events.Publish(new OperatingDayEndedEvent(69));
            ReproductionProcessSaveData saved = persistence.Capture()
                .processes.Single(value => value.processId == ProcessId);
            Require(saved.resultPublished
                    && saved.pendingBirth == null
                    && !string.IsNullOrWhiteSpace(saved.resultCharacterId),
                "Production birth did not finalize its result marker.");
            CharacterId childId = new(saved.resultCharacterId);

            CharacterActor[] actors = world.Characters.Where(value =>
                    value != null
                    && CharacterPersistentIdentity.TryGet(
                        value,
                        out CharacterId id)
                    && id.Equals(childId))
                .ToArray();
            Require(actors.Length == 1,
                "Production birth did not publish exactly one child actor.");
            Require(life.TryGet(childId, out CharacterLifeRecord lifeRecord)
                    && lifeRecord.PhenotypeSpeciesId.Value
                        == saved.phenotypeSpeciesId,
                "Production birth did not publish the exact child life record.");
            CharacterNarrativeSaveData narrativeRecord = narrative.Capture()
                .characters.SingleOrDefault(value =>
                    value.characterId == childId.Value);
            Require(narrativeRecord != null
                    && narrativeRecord.phenotypeSpeciesId
                        == saved.phenotypeSpeciesId,
                "Production birth did not publish the exact child narrative record.");
            string[] parents = kinship.GetParents(childId, includeAdoptive: true)
                .Select(value => value.Value)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();
            string[] expectedParents = new[]
                {
                    FirstParentId.Value,
                    SecondParentId.Value
                }
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();
            CharacterId guardian = kinship.GetGuardian(childId);
            Require(parents.SequenceEqual(expectedParents, StringComparer.Ordinal)
                    && !guardian.IsValid,
                "Production birth lost its exact genetic parents or invented a guardian.");
            WorldCharacterProfile profile = population.Profiles.Single(value =>
                value.persistentId == childId.Value);
            Require(profile.settlementStanding
                    == CharacterSettlementStanding.Resident,
                "Production birth did not create one resident population profile.");
            Require(housing.CallCount == 1
                    && housing.ChildId.Equals(childId)
                    && housing.FirstParentId.Equals(FirstParentId)
                    && housing.SecondParentId.Equals(SecondParentId)
                    && housing.CarrierId.Equals(FirstParentId),
                "Production birth housing command lost child or parent identity.");

            GameplayOutcomeSnapshot birth = outcomes.RowsForProcess(ProcessId)
                .Single(value => Fact(
                        value,
                        ReproductionProcessTransitionOutcomeIds.TransitionKind)
                    == "birth-published");
            Require(birth.participants.Any(value =>
                        value.entityId == childId.Value
                        && value.roleId == ReproductionProcessTransitionOutcomeIds
                            .OffspringRole.Value)
                    && birth.participants.Count(value =>
                        value.roleId == ReproductionProcessTransitionOutcomeIds
                            .GeneticParentRole.Value) == 2,
                "Production birth outcome lost child or genetic parents.");

            events.Publish(new OperatingDayEndedEvent(69));
            ReproductionProcessSaveData replayed = persistence.Capture()
                .processes.Single(value => value.processId == ProcessId);
            var counts = new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["actor"] = world.Characters.Count(value =>
                    value != null
                    && CharacterPersistentIdentity.TryGet(
                        value,
                        out CharacterId id)
                    && id.Equals(childId)),
                ["life"] = life.Records.Count(value =>
                    value.CharacterId.Equals(childId)),
                ["narrative"] = narrative.Capture().characters.Count(value =>
                    value.characterId == childId.Value),
                ["kinship-parent"] = kinship.GetParents(
                    childId,
                    includeAdoptive: true).Count,
                ["population-profile"] = population.Profiles.Count(value =>
                    value.persistentId == childId.Value),
                ["housing-call"] = housing.CallCount,
                ["birth-outcome"] = outcomes.RowsForProcess(ProcessId).Count(
                    value => Fact(
                            value,
                            ReproductionProcessTransitionOutcomeIds
                                .TransitionKind)
                        == "birth-published")
            };
            Require(replayed.resultPublished
                    && replayed.resultCharacterId == childId.Value
                    && counts.All(value => value.Key == "kinship-parent"
                        ? value.Value == 2
                        : value.Value == 1),
                "Production birth replay duplicated an authority or changed its marker.");

            return new ProductionBirthPublicationEvidence(
                birth,
                replayed,
                childId.Value,
                counts["actor"],
                counts["life"],
                counts["narrative"],
                counts["population-profile"],
                profile.settlementStanding,
                parents,
                guardian.Value,
                housing.CallCount,
                counts);
        }

        public void Dispose()
        {
            adapter.Dispose();
            events.Clear();
            outcomes.Dispose();
            foreach (CharacterActor actor in world.Characters.ToArray())
            {
                if (actor != null)
                    UnityEngine.Object.DestroyImmediate(actor.gameObject);
            }
            if (spawnerObject != null)
                UnityEngine.Object.DestroyImmediate(spawnerObject);
            if (prefabMarker != null)
                UnityEngine.Object.DestroyImmediate(prefabMarker);
        }

        private static CharacterActor CreateParentActor(
            CharacterSO data,
            CharacterSpeciesId speciesId,
            CharacterId characterId,
            ReproductiveRole role)
        {
            GameObject actorObject = CharacterAiPlanDebugFixtures
                .CreateActorObject("Production Birth Parent");
            CharacterAiEditorTestDependencies.Inject(actorObject);
            CharacterActor actor = actorObject.GetComponent<CharacterActor>();
            actor.EnsureRuntimeState();
            actor.Initialize(
                data,
                new CharacterSpawnRequest(
                    data.DefinitionId,
                    speciesId,
                    data.VisualVariantId,
                    role,
                    Array.Empty<CharacterTraitId>()));
            actor.Identity.SetPersistentId(characterId);
            actor.characterType = CharacterType.NPC;
            return actor;
        }

        private static ReproductiveRole FirstParentRole(ReproductionMode mode) =>
            mode switch
            {
                ReproductionMode.Pregnancy => ReproductiveRole.Carrier,
                ReproductionMode.Egg => ReproductiveRole.Layer,
                ReproductionMode.Spore => ReproductiveRole.SporeContributor,
                ReproductionMode.CoreDivision => ReproductiveRole.DivisionCore,
                _ => throw new InvalidOperationException(
                    "Production birth fixture requires a non-golem reproduction mode.")
            };
    }

    private sealed class PersistenceBackedReproductionService :
        IReproductionService
    {
        private readonly StandaloneReproductionPersistence persistence;

        internal PersistenceBackedReproductionService(
            StandaloneReproductionPersistence persistence) =>
            this.persistence = persistence
                ?? throw new ArgumentNullException(nameof(persistence));

        public FamilyPlanningPolicy FamilyPlanningPolicy =>
            persistence.Current.FamilyPlanningPolicy;
        public long FamilyPlanningPolicyRevision =>
            persistence.Current.FamilyPlanningPolicyRevision;
        public bool HasPendingFamilyPlanningPolicyChange =>
            persistence.Current.HasPendingFamilyPlanningPolicyChange;
        public int LastAllowedPolicyEvaluationDay =>
            persistence.Current.LastAllowedPolicyEvaluationDay;
        public IReadOnlyList<ReproductionProcess> Processes =>
            persistence.Current.Processes;
        public void SetFamilyPlanningPolicy(FamilyPlanningPolicy policy) =>
            persistence.Current.SetFamilyPlanningPolicy(policy);
        public void MarkAllowedPolicyEvaluation(int absoluteDay) =>
            persistence.Current.MarkAllowedPolicyEvaluation(absoluteDay);
        public void AddProcess(ReproductionProcess process) =>
            persistence.Current.Add(process);
        public void AdvanceProcess(
            string processId,
            ReproductionDailyContext context) =>
            throw new NotSupportedException();
        public void NotifyCarrierDeath(CharacterId carrierId, int absoluteDay) =>
            throw new NotSupportedException();
        public void EmergencyExtract(string processId, int absoluteDay) =>
            throw new NotSupportedException();
        public void MarkResultPublished(
            string processId,
            CharacterId resultCharacterId) =>
            throw new NotSupportedException();
    }

    private sealed class MutableBirthWorld : ICharacterWorldQuery
    {
        private readonly List<CharacterActor> actors = new();
        public int CharacterVersion { get; private set; } = 1;
        public IReadOnlyList<CharacterActor> Characters => actors.ToArray();

        internal void Add(CharacterActor actor)
        {
            if (actor == null || actors.Contains(actor))
                return;
            actors.Add(actor);
            CharacterVersion++;
        }
    }

    private sealed class BirthSpawnObjectFactory :
        ICharacterSpawnObjectFactory
    {
        private readonly MutableBirthWorld world;

        internal BirthSpawnObjectFactory(MutableBirthWorld world) =>
            this.world = world ?? throw new ArgumentNullException(nameof(world));

        public GameObject CreateInactive(GameObject characterPrefab) =>
            CreateInactive(characterPrefab, compose: null);

        public GameObject CreateInactive(
            GameObject characterPrefab,
            Action<GameObject> compose)
        {
            var value = CharacterAiPlanDebugFixtures.CreateActorObject(
                "Production Birth Child Candidate");
            value.SetActive(false);
            CharacterAiEditorTestDependencies.Inject(value);
            compose?.Invoke(value);
            return value;
        }

        public void Publish(GameObject characterObject)
        {
            CharacterActor actor = characterObject != null
                ? characterObject.GetComponent<CharacterActor>()
                : null;
            if (actor == null)
                throw new InvalidOperationException(
                    "Production birth candidate has no character actor.");
            world.Add(actor);
            characterObject.SetActive(true);
        }

        public void Destroy(GameObject characterObject)
        {
            if (characterObject != null)
                UnityEngine.Object.DestroyImmediate(characterObject);
        }

        public GameObject CreateDetached(GameObject characterPrefab) =>
            throw new NotSupportedException();
        public GameObject CreateDetached(
            GameObject characterPrefab,
            Action<GameObject> compose) => throw new NotSupportedException();
        public void ComposeDetached(GameObject characterObject) =>
            throw new NotSupportedException();
        public void Inject(GameObject characterObject) =>
            throw new NotSupportedException();
        public void InjectAddedAbility(CharacterAbility ability) =>
            throw new NotSupportedException();
        public void PublishDetached(GameObject characterObject) =>
            throw new NotSupportedException();
        public DetachedCharacterPublication PublishDetachedInactive(
            GameObject characterObject) => throw new NotSupportedException();
        public void ValidateDetachedPublication(
            DetachedCharacterPublication publication) =>
            throw new NotSupportedException();
        public void CompleteDetachedPublication(
            DetachedCharacterPublication publication) =>
            throw new NotSupportedException();
        public void RollbackDetachedPublication(
            DetachedCharacterPublication publication) =>
            throw new NotSupportedException();
    }

    private sealed class FixedBirthSpawnerProvider : ICharacterSpawnerProvider
    {
        private readonly CharacterSpawner spawner;
        internal FixedBirthSpawnerProvider(CharacterSpawner spawner) =>
            this.spawner = spawner
                ?? throw new ArgumentNullException(nameof(spawner));
        public bool TryGetSpawner(out CharacterSpawner value)
        {
            value = spawner;
            return true;
        }
    }

    private sealed class RecordingBirthPopulationService :
        ICharacterPopulationService
    {
        private readonly List<WorldCharacterProfile> profiles = new();
        public IReadOnlyList<WorldCharacterProfile> Profiles => profiles;

        public CharacterSettlementStanding SetSettlementStanding(
            CharacterActor actor,
            CharacterSettlementStanding standing)
        {
            CharacterId id = CharacterPersistentIdentity.Require(actor);
            WorldCharacterProfile profile = profiles.SingleOrDefault(value =>
                value.persistentId == id.Value);
            if (profile == null)
            {
                profile = new WorldCharacterProfile
                {
                    persistentId = id.Value,
                    characterDataId = actor.Identity?.TemplateId ?? -1,
                    displayName = actor.Identity?.DisplayName ?? string.Empty,
                    isAlive = true
                };
                profiles.Add(profile);
            }
            profile.settlementStanding = standing;
            return standing;
        }

        public bool TryGetProfile(
            CharacterActor actor,
            out WorldCharacterProfile profile)
        {
            string id = actor?.Identity?.PersistentId ?? string.Empty;
            profile = profiles.SingleOrDefault(value =>
                value.persistentId == id);
            return profile != null;
        }

        public bool TryRebindRestoredProfile(CharacterActor actor) =>
            throw new NotSupportedException("Birth population fixture does not rebind restored actors.");

        public List<WorldCharacterProfile> CaptureProfiles() =>
            profiles.Select(value => value.Clone()).ToList();
        public void RestoreProfiles(IEnumerable<WorldCharacterProfile> values)
        {
            profiles.Clear();
            profiles.AddRange((values ?? Array.Empty<WorldCharacterProfile>())
                .Select(value => value.Clone()));
        }

        public WorldCharacterProfile AcquireVisitor(
            CharacterSO characterData,
            IEnumerable<string> unavailableProfileIds = null) =>
            throw new NotSupportedException();
        public bool TryCreateRecruitCandidate(
            out WorldCharacterProfile profile,
            out CharacterSO sourceData)
        {
            profile = null;
            sourceData = null;
            return false;
        }
        public void BindActor(WorldCharacterProfile profile, CharacterActor actor) =>
            throw new NotSupportedException();
        public void RefreshProfile(CharacterActor actor) =>
            throw new NotSupportedException();
        public void ReleaseVisitor(CharacterActor actor) =>
            throw new NotSupportedException();
        public void PromoteToStaff(CharacterActor actor) =>
            throw new NotSupportedException();
        public void PromoteToMinion(CharacterActor actor) =>
            throw new NotSupportedException();
        public void RestoreSettlementStanding(
            CharacterActor actor,
            CharacterSettlementStanding standing) =>
            SetSettlementStanding(actor, standing);
        public CharacterSettlementStandingTransaction
            BeginSettlementStandingTransition(
                CharacterActor actor,
                CharacterSettlementStanding standing) =>
            throw new NotSupportedException();
        public void RollbackSettlementStandingTransition(
            CharacterSettlementStandingTransaction transaction) =>
            throw new NotSupportedException();
        public void CompleteSettlementStandingTransition(
            CharacterSettlementStandingTransaction transaction) =>
            throw new NotSupportedException();
        public CharacterPopulationRestoreCandidate BuildRestoreCandidate(
            IEnumerable<WorldCharacterProfile> values) =>
            throw new NotSupportedException();
        public CharacterPopulationRestoreTransaction ApplyRestoreCandidate(
            CharacterPopulationRestoreCandidate candidate) =>
            throw new NotSupportedException();
        public void RollbackRestore(
            CharacterPopulationRestoreTransaction transaction) =>
            throw new NotSupportedException();
        public void CompleteRestore(
            CharacterPopulationRestoreTransaction transaction) =>
            throw new NotSupportedException();
    }

    private sealed class RecordingHouseholdBirthCommand :
        IHouseholdBirthAssignmentCommand
    {
        internal int CallCount { get; private set; }
        internal CharacterId ChildId { get; private set; }
        internal CharacterId FirstParentId { get; private set; }
        internal CharacterId SecondParentId { get; private set; }
        internal CharacterId CarrierId { get; private set; }

        public bool TryResolveBirth(
            CharacterId childId,
            CharacterId firstParentId,
            CharacterId secondParentId,
            CharacterId carrierId,
            int absoluteDay,
            out HouseholdBirthAssignmentResolution resolution,
            out string failureReason)
        {
            CallCount++;
            ChildId = childId;
            FirstParentId = firstParentId;
            SecondParentId = secondParentId;
            CarrierId = carrierId;
            resolution = HouseholdBirthAssignmentResolution
                .PendingParentHousehold;
            failureReason = string.Empty;
            return absoluteDay >= 1;
        }
    }

    private sealed class UnexpectedReproductionTransitionCommand :
        IReproductionProcessTransitionCommand
    {
        public bool TryAdvance(
            string processId,
            ReproductionDailyContext context,
            out DomainFailure failure) =>
            throw new InvalidOperationException(
                "Completed production birth unexpectedly advanced a day.");
        public bool TryNotifyCarrierDeath(
            CharacterDeathEvent death,
            out DomainFailure failure) => throw new NotSupportedException();
        public bool TryResumePendingCarrierDeaths(out DomainFailure failure) =>
            throw new NotSupportedException();
    }

    private sealed class UnusedBodyHealthQuery : ICharacterBodyHealthQuery
    {
        public CharacterVitalsSnapshot GetVitals(CharacterActor actor) =>
            throw new NotSupportedException();
        public CharacterVitalsSnapshot GetVitals(string characterId) =>
            throw new NotSupportedException();
        public CharacterBodyHealthSnapshot GetSnapshot(CharacterActor actor) =>
            throw new NotSupportedException();
        public CharacterBodyHealthSnapshot GetSnapshot(string characterId) =>
            throw new NotSupportedException();
        public float GetTotalBleeding(CharacterActor target) =>
            throw new NotSupportedException();
        public float GetMissingPartHealth(CharacterActor target) =>
            throw new NotSupportedException();
    }

    private sealed class FixedClimateQuery : IClimateQuery
    {
        internal FixedClimateQuery(float temperature) =>
            OutdoorTemperatureC = temperature;
        public int Version => 1;
        public int AbsoluteDay => 70;
        public string ClimateZoneId => "climate:qa:birth";
        public string WeatherFrontId => string.Empty;
        public int FrontRemainingDays => 0;
        public float OutdoorTemperatureC { get; }
    }

    private sealed class NeutralBirthHeritableTraits :
        IHeritableTraitEffectQuery
    {
        internal static readonly NeutralBirthHeritableTraits Instance = new();
        public float GetMultiplier(
            CharacterId characterId,
            HeritableTraitConsequenceKind kind,
            string targetId) => 1f;
    }

    private sealed class PlanFixture : IDisposable
    {
        private readonly ResourceCharacterSpeciesCatalog definitions;
        private readonly FixedLifeQuery life;
        private readonly FixedCharacterWorld world;
        private readonly FixedKinshipQuery kinship = new();
        private readonly CharacterNarrativeRuntime narratives;
        private readonly FixedFacilityQuery facilities;
        private readonly FixedCalendar calendar = new(12);
        private readonly RecordingSettlementAlertService alerts = new();
        private readonly DurableFacilityEquipmentPolicyRegistry equipmentPolicies;
        private readonly RecordingEquipmentSlots equipmentSlots = new();
        private readonly GameObject facilityObject;
        private readonly GameObject[] actorObjects;
        private readonly CharacterSO[] actorData;

        internal PlanFixture()
        {
            ResourceGameContentCatalog content = new(
                new UnityGameContentRootLoader());
            definitions = new ResourceCharacterSpeciesCatalog(content);
            CharacterSO authored = content.GetAll<CharacterSO>()
                .Where(value => value != null
                    && value.species?.reproduction != null
                    && value.species.reproduction.mode == ReproductionMode.Pregnancy
                    && value.role == CharacterRole.Regular)
                .OrderBy(value => value.DefinitionId.Value, StringComparer.Ordinal)
                .FirstOrDefault()
                ?? throw new InvalidOperationException(
                    "No authored pregnancy character exists for the focused test.");
            SpeciesId = new CharacterSpeciesId(authored.SpeciesTag);
            FirstId = new CharacterId("character:qa:reproduction-parent-a");
            SecondId = new CharacterId("character:qa:reproduction-parent-b");
            actorData = new[]
            {
                Clone(authored, "연화"),
                Clone(authored, "도윤")
            };
            actorObjects = new[]
            {
                CreateActor(
                    "Reproduction Parent A",
                    actorData[0],
                    FirstId,
                    SpeciesId,
                    ReproductiveRole.Carrier),
                CreateActor(
                    "Reproduction Parent B",
                    actorData[1],
                    SecondId,
                    SpeciesId,
                    ReproductiveRole.Contributor)
            };
            CharacterActor[] actors = actorObjects
                .Select(value => value.GetComponent<CharacterActor>())
                .ToArray();
            Require(actors[0].TryGetAbility(out AbilityWork medicalWork),
                "The focused reproduction responder has no work ability.");
            medicalWork.WorkPriorities.SetPriority(
                BuiltInWorkTypeIds.Treat,
                WorkPriorityLevel.Priority1);
            world = new FixedCharacterWorld(actors);
            SpeciesLifeHistoryDefinition history =
                definitions.RequireLifeHistory(SpeciesId);
            int adultDays = checked(
                (history.AdultAgeYears + 1) * GameCalendarRules.DaysPerYear);
            life = new FixedLifeQuery(actors.Select(actor =>
                new CharacterLifeRecord(
                    CharacterPersistentIdentity.Require(actor),
                    SpeciesId,
                    adultDays,
                    adultDays,
                    1,
                    history)));

            CharacterNarrativeCatalog narrativeCatalog = new(content);
            narratives = new CharacterNarrativeRuntime(
                new DungeonRuntimeAggregateRootStore(),
                narrativeCatalog);
            narratives.Register(
                FirstId,
                SpeciesId,
                Array.Empty<string>(),
                Array.Empty<string>());
            narratives.Register(
                SecondId,
                SpeciesId,
                Array.Empty<string>(),
                Array.Empty<string>());

            facilityObject = new GameObject("번식 전이 검증 산실");
            BuildableObject facility = facilityObject.AddComponent<BuildableObject>();
            facility.RestorePersistentIdentity(
                new BuildingInstanceId(FacilityInstanceId));
            facilities = new FixedFacilityQuery(facility, "building:8859");

            Stock = new MutableStock(new WorldItemStackSnapshot
            {
                StackId = FertilityStackId,
                ItemId = FertilityItemId,
                DisplayName = "생식 치료제",
                Quantity = 2,
                State = WorldItemStackState.FacilityBuffer,
                DestinationId = FacilityInstanceId
            });
            Reservations = new MutableReservations(Stock);
            Atomic = new MutableAtomicConsumption(Stock, Reservations);
            equipmentPolicies = new DurableFacilityEquipmentPolicyRegistry(
                new IDurableFacilityEquipmentPolicySource[]
                {
                    new ReproductionDurableEquipmentPolicySource()
                });
            Equipment = new RecordingEquipmentUse(equipmentSlots);
            OutcomeEnvironment = new OutcomeEnvironment(
                "run:qa:reproduction-plan-start");
            Facts = new FaultingTransitionFacts(OutcomeEnvironment.Facts);
            Restart(new ReproductionWorldSaveData());
        }

        internal CharacterSpeciesId SpeciesId { get; }
        internal CharacterId FirstId { get; }
        internal CharacterId SecondId { get; }
        internal ICharacterWorldQuery World => world;
        internal MutableStock Stock { get; }
        internal MutableReservations Reservations { get; }
        internal MutableAtomicConsumption Atomic { get; }
        internal RecordingEquipmentUse Equipment { get; }
        internal OutcomeEnvironment OutcomeEnvironment { get; }
        internal FaultingTransitionFacts Facts { get; }
        internal ReproductionRuntime Reproduction { get; private set; }
        internal ReproductionCommandRuntime Command { get; private set; }

        internal ReproductionPlanRequest CreateRequest(string actionId) => new()
        {
            ActionId = actionId,
            FirstParentId = FirstId.Value,
            SecondParentId = SecondId.Value,
            CarrierId = FirstId.Value,
            PhenotypeSpeciesId = SpeciesId.Value,
            AbsoluteDay = calendar.Day,
            UseFertilityTreatment = true
        };

        internal JObject VerifyAllowedPolicyProductionProposal(
            IGameSessionStateProvider session)
        {
            Require(Reproduction.FamilyPlanningPolicy == FamilyPlanningPolicy.Planned
                    && Reproduction.FamilyPlanningPolicyRevision == 0L
                    && Reproduction.Processes.Count == 0,
                "Production proposal fixture did not start with the default Planned policy.");
            var events = new GameEventBus();
            using var initial = new ReproductionProposalAdapter(
                Reproduction, Command, world, life, events);
            initial.Start();
            events.Publish(new OperatingDayEndedEvent(9));
            Require(Reproduction.Processes.Count == 0
                    && OutcomeEnvironment.AllRows().Count == 0,
                "Default Planned policy unexpectedly generated an automatic proposal.");

            using var policyFacts = new DungeonStory.Foundation.Facts.DomainFactDispatcher();
            policyFacts.Subscribe("test:family-planning", new GameplayDomainFactObserver<
                FamilyPlanningPolicyChangedFact, PolicyValueChangeReceipt>(
                FamilyPlanningPolicyFactMapping.Capture, OutcomeEnvironment.Registry,
                OutcomeEnvironment.Ledger, OutcomeEnvironment.Recorder,
                new GameplayDomainFactCommitGroup(OutcomeEnvironment.Recorder, OutcomeEnvironment.Ledger)));
            var policy = new FamilyPlanningPolicyCommandRuntime(
                Reproduction, Reproduction, policyFacts, session);
            Require(policy.TrySet(FamilyPlanningPolicy.Allowed,
                    "character-summary-ui", out DomainFailure failure),
                "Real family-planning policy command failed: " + failure.Code);
            Require(Reproduction.FamilyPlanningPolicy == FamilyPlanningPolicy.Allowed
                    && Reproduction.FamilyPlanningPolicyRevision == 1L,
                "Allowed policy did not commit its exact revision.");
            GameplayOutcomeSnapshot policyOutcome = OutcomeEnvironment.AllRows()
                .Single(value => value.outcomeTypeId == "policy.value-changed");
            ReproductionWorldSaveData allowed = RoundTrip(Reproduction.Capture());
            initial.Dispose();
            Restart(allowed);

            using var restored = new ReproductionProposalAdapter(
                Reproduction, Command, world, life, events);
            restored.Start();
            events.Publish(new OperatingDayEndedEvent(9));
            string expectedProcessId =
                $"reproduction:allowed:10:{FirstId.Value}:{SecondId.Value}";
            ReproductionProcess proposal = Reproduction.Processes.Single();
            Require(proposal.ProcessId == expectedProcessId
                    && proposal.Status == ReproductionProcessStatus.Planned
                    && proposal.TransitionRevision == 1L
                    && Reproduction.LastAllowedPolicyEvaluationDay == 10,
                "Restored policy did not reach the real reproduction plan command.");
            GameplayOutcomeSnapshot planOutcome = RequireOutcome(
                expectedProcessId, "planned");
            RequirePlanParticipants(planOutcome);
            Require(Math.Abs(Equipment.AppliedWear
                        - ReproductionDurableEquipmentUseRuntime.LedgerWearPerPlan)
                    <= 0.000001d
                    && Atomic.ConsumeCalls == 0
                    && OutcomeEnvironment.AllRows().Count == 2,
                "Automatic planning duplicated wear, consumed start material, or lost an outcome.");

            ReproductionWorldSaveData planned = RoundTrip(Reproduction.Capture());
            string beforeReplay = JsonUtility.ToJson(planned);
            restored.Dispose();
            Restart(planned);
            using var replay = new ReproductionProposalAdapter(
                Reproduction, Command, world, life, events);
            replay.Start();
            events.Publish(new OperatingDayEndedEvent(9));
            Require(JsonUtility.ToJson(Reproduction.Capture()) == beforeReplay
                    && OutcomeEnvironment.AllRows().Count == 2
                    && Math.Abs(Equipment.AppliedWear
                        - ReproductionDurableEquipmentUseRuntime.LedgerWearPerPlan)
                    <= 0.000001d
                    && Atomic.ConsumeCalls == 0,
                "Same-day replay after restore duplicated the real proposal or its costs.");
            return new JObject
            {
                ["scope"] = "real policy/adapter/plan commands; controlled actors, facility, equipment and stock ports; scoped reproduction JSON restore",
                ["commandType"] = Command.GetType().FullName,
                ["policyOutcome"] = JObject.FromObject(policyOutcome),
                ["planOutcome"] = JObject.FromObject(planOutcome),
                ["savedAllowed"] = JObject.FromObject(allowed),
                ["savedProposal"] = JObject.FromObject(planned),
                ["restoredReplay"] = JObject.FromObject(Reproduction.Capture()),
                ["appliedWear"] = Equipment.AppliedWear,
                ["materialConsumeCalls"] = Atomic.ConsumeCalls,
                ["outcomeCountAfterReplay"] = OutcomeEnvironment.AllRows().Count
            };
        }

        internal void Restart(ReproductionWorldSaveData save)
        {
            Reproduction = new ReproductionRuntime(
                new DungeonRuntimeAggregateRootStore(),
                definitions,
                life,
                kinship,
                world,
                new RandomStreamProvider(91021));
            Reproduction.PublishRestore(Reproduction.PrepareRestore(save));
            var publications = new ReproductionTransitionPublication(
                Reproduction,
                new ReproductionTransitionFactSource(OutcomeEnvironment.Names),
                Facts,
                Facts,
                alerts);
            Command = new ReproductionCommandRuntime(
                Reproduction,
                Reproduction,
                definitions,
                life,
                world,
                narratives,
                kinship,
                facilities,
                Stock,
                Reservations,
                Atomic,
                equipmentPolicies,
                equipmentSlots,
                Equipment,
                publications,
                calendar);
        }

        internal ReproductionProcess RequireProcess(string processId) =>
            Reproduction.Processes.Single(value => string.Equals(
                value.ProcessId,
                processId,
                StringComparison.Ordinal));

        internal GameplayOutcomeSnapshot RequireOutcome(
            string processId,
            string transitionKind) => OutcomeEnvironment.RowsForProcess(processId)
            .Single(value => Fact(
                    value,
                    ReproductionProcessTransitionOutcomeIds.TransitionKind)
                == transitionKind);

        internal void RequirePlanParticipants(GameplayOutcomeSnapshot row)
        {
            Require(row.participants.Any(value =>
                    value.entityId == FirstId.Value
                    && value.roleId == ReproductionProcessTransitionOutcomeIds
                        .GeneticParentRole.Value)
                && row.participants.Any(value =>
                    value.entityId == SecondId.Value
                    && value.roleId == ReproductionProcessTransitionOutcomeIds
                        .GeneticParentRole.Value)
                && row.participants.Any(value =>
                    value.entityId == FirstId.Value
                    && value.roleId == ReproductionProcessTransitionOutcomeIds
                        .CarrierRole.Value)
                && row.participants.Any(value =>
                    value.entityId == FacilityInstanceId
                    && value.roleId == ReproductionProcessTransitionOutcomeIds
                        .FacilityRole.Value),
                "Plan outcome lost a parent, carrier, or support facility.");
        }

        public void Dispose()
        {
            OutcomeEnvironment.Dispose();
            if (facilityObject != null)
                UnityEngine.Object.DestroyImmediate(facilityObject);
            foreach (GameObject value in actorObjects)
                if (value != null)
                    UnityEngine.Object.DestroyImmediate(value);
            foreach (CharacterSO value in actorData)
                if (value != null)
                    UnityEngine.Object.DestroyImmediate(value);
        }
    }

    private sealed class TransitionFixture : IDisposable
    {
        private static readonly CharacterSpeciesId EggSpecies =
            new("species:qa:egg");
        private static readonly CharacterSpeciesId PregnancySpecies =
            new("species:qa:pregnancy");
        private readonly FixedReproductionDefinitions definitions;

        internal TransitionFixture(
            string runId,
            Func<IGameplayOutcomeDiagnosticsQuery,
                IGameplayOutcomeDiagnosticsQuery> observationDecorator = null)
        {
            definitions = new FixedReproductionDefinitions(
                EggDefinition(),
                PregnancyDefinition());
            Persistence = new StandaloneReproductionPersistence(definitions);
            OutcomeEnvironment = new OutcomeEnvironment(runId, observationDecorator);
            Facts = new FaultingTransitionFacts(OutcomeEnvironment.Facts);
            Alerts = new RecordingSettlementAlertService();
            Calendar = new FixedCalendar(1);
            Publications = new ReproductionTransitionPublication(
                Persistence,
                new ReproductionTransitionFactSource(OutcomeEnvironment.Names),
                Facts,
                Facts,
                Alerts);
            Transitions = new ReproductionProcessTransitionRuntime(
                Persistence,
                Publications,
                Alerts,
                new RandomStreamProvider(30117));
            BirthPort = new RecordingBirthPublicationPort();
            RestartBirthCommand();
        }

        internal StandaloneReproductionPersistence Persistence { get; }
        internal IReproductionDefinitionCatalog Definitions => definitions;
        internal OutcomeEnvironment OutcomeEnvironment { get; }
        internal FaultingTransitionFacts Facts { get; }
        internal RecordingSettlementAlertService Alerts { get; }
        internal FixedCalendar Calendar { get; }
        internal ReproductionTransitionPublication Publications { get; }
        internal ReproductionProcessTransitionRuntime Transitions { get; }
        internal RecordingBirthPublicationPort BirthPort { get; }
        internal ReproductionBirthTransactionRuntime Births { get; private set; }

        internal ReproductionProcess AddActiveEgg(string processId) => AddActive(
            processId,
            EggSpecies,
            EggDefinition(),
            Character(processId, "layer"));

        internal ReproductionProcess AddActivePregnancy(string processId) => AddActive(
            processId,
            PregnancySpecies,
            PregnancyDefinition(),
            Character(processId, "carrier"));

        internal ReproductionProcess AddCompletedEgg(string processId)
        {
            ReproductionProcess process = AddActiveEgg(processId);
            ReproductionWorldAggregate candidate = Persistence.PrepareRestore(
                Persistence.Capture());
            candidate.TryGet(processId, out process);
            process.AdvanceDay(SafeDay(68), 0d);
            process.AdvanceDay(SafeDay(69), 0d);
            Require(process.Status == ReproductionProcessStatus.Completed,
                "Focused birth process did not reach completion.");
            Persistence.PublishRestore(candidate);
            return RequireProcess(processId);
        }

        internal void RestartBirthCommand()
        {
            Births = new ReproductionBirthTransactionRuntime(
                Persistence,
                BirthPort,
                Publications);
        }

        internal ReproductionProcess RequireProcess(string processId) =>
            Persistence.Current.Processes.Single(value => string.Equals(
                value.ProcessId,
                processId,
                StringComparison.Ordinal));

        internal MigratedProducerOutcomeCommitResult CommitDeathParent(
            CharacterId carrier,
            int day,
            string ownerIdentity) => OutcomeEnvironment.Migrated
            .CommitRequiredSingleSubject(
                MigratedProducerOutcomeKind.CharacterDeathEvent,
                ownerIdentity,
                day,
                new MigratedProducerOutcomeSubject(
                    MigratedProducerOutcomeIds.CharacterKind,
                    carrier.Value,
                    "운반자",
                    MigratedProducerOutcomeIds.ActorRole),
                "운반자가 전투에서 사망했다.");

        internal void RequireDeathChild(
            string processId,
            MigratedProducerOutcomeCommitResult parent,
            string statusAfter)
        {
            GameplayOutcomeSnapshot row = OutcomeEnvironment.RowsForProcess(processId)
                .Single(value => Fact(
                        value,
                        ReproductionProcessTransitionOutcomeIds.TransitionKind)
                    == "carrier-death");
            Require(row.parentRunId == parent.OutcomeId.RunId.Value
                    && row.parentSequence == parent.OutcomeId.Sequence
                    && row.causationRelationId == "direct-effect"
                    && Fact(row,
                        ReproductionProcessTransitionOutcomeIds.StatusAfter)
                        == statusAfter
                    && Fact(row,
                        ReproductionProcessTransitionOutcomeIds.Cause)
                        == "carrier-death-combat",
                "Carrier-death child did not point to the exact character.death parent.");
            GameplayOutcomeSnapshot parentRow = OutcomeEnvironment.AllRows()
                .Single(value => value.runId == parent.OutcomeId.RunId.Value
                    && value.sequence == parent.OutcomeId.Sequence);
            string deathType = MigratedProducerOutcomeCatalog.Definitions
                .Single(value => value.Kind
                    == MigratedProducerOutcomeKind.CharacterDeathEvent)
                .OutcomeTypeId.Value;
            Require(parentRow.outcomeTypeId == deathType,
                "Carrier-death parent was not the character.death outcome.");
        }

        public void Dispose()
        {
            OutcomeEnvironment.Dispose();
        }

        private ReproductionProcess AddActive(
            string processId,
            CharacterSpeciesId speciesId,
            ReproductionDefinition definition,
            CharacterId carrier)
        {
            ReproductionWorldAggregate candidate = Persistence.PrepareRestore(
                Persistence.Capture());
            ReproductionProcess process = new(
                processId,
                carrier,
                Character(processId, "second-parent"),
                carrier,
                speciesId,
                definition,
                1,
                crossLineageIncubatorUsed: false,
                supportFacilityInstanceId: FacilityInstanceId,
                expressedTraitIds: Array.Empty<string>(),
                latentTraitIds: Array.Empty<string>(),
                aptitudes: Array.Empty<InnateAptitudeSaveData>(),
                startActive: true);
            candidate.Add(process);
            Persistence.PublishRestore(candidate);
            return RequireProcess(processId);
        }

        private static CharacterId Character(string processId, string role) =>
            new("character:qa:reproduction:"
                + NarrativeInferenceHash.ComputeSha256Utf8(
                    processId + "\n" + role));

        private static ReproductionDefinition EggDefinition() => new(
            EggSpecies,
            ReproductionMode.Egg,
            baseSuccessChance: 1f,
            viableTemperatureMinimum: 10f,
            viableTemperatureMaximum: 30f,
            phases: new[]
            {
                new ReproductionPhaseDefinition
                {
                    phase = ReproductionPhaseKind.Attempt,
                    durationDays = 1
                },
                new ReproductionPhaseDefinition
                {
                    phase = ReproductionPhaseKind.Incubation,
                    durationDays = 1
                }
            });

        private static ReproductionDefinition PregnancyDefinition() => new(
            PregnancySpecies,
            ReproductionMode.Pregnancy,
            baseSuccessChance: 1f,
            viableTemperatureMinimum: 10f,
            viableTemperatureMaximum: 30f,
            phases: new[]
            {
                new ReproductionPhaseDefinition
                {
                    phase = ReproductionPhaseKind.Attempt,
                    durationDays = 1
                },
                new ReproductionPhaseDefinition
                {
                    phase = ReproductionPhaseKind.Pregnancy,
                    durationDays = 4
                }
            });
    }

    private sealed class RecordingBirthPublicationPort :
        IReproductionBirthPublicationPort
    {
        private readonly HashSet<string> ensured = new(StringComparer.Ordinal);

        internal string FailBoundary { get; set; } = string.Empty;
        internal Dictionary<string, int> EnsureCounts { get; } =
            new(StringComparer.Ordinal);
        internal HashSet<CharacterId> ChildIds { get; } = new();

        public bool TryEnsureLife(
            ReproductionProcess process,
            CharacterId childId,
            int absoluteDay,
            out string failureReason) => TryEnsure(
            "life",
            childId,
            absoluteDay >= 1,
            out failureReason);

        public bool TryEnsureNarrative(
            ReproductionProcess process,
            CharacterId childId,
            out string failureReason) => TryEnsure(
            "narrative",
            childId,
            process.ExpressedTraitIds != null,
            out failureReason);

        public bool TryEnsureKinship(
            ReproductionProcess process,
            CharacterId childId,
            out string failureReason) => TryEnsure(
            "kinship",
            childId,
            process.FirstParentId.IsValid && process.SecondParentId.IsValid,
            out failureReason);

        public bool TryEnsureActor(
            ReproductionProcess process,
            CharacterId childId,
            out string failureReason) => TryEnsure(
            "actor",
            childId,
            process.PhenotypeSpeciesId.IsValid,
            out failureReason);

        public bool TryEnsurePopulation(
            ReproductionProcess process,
            CharacterId childId,
            out string failureReason) => TryEnsure(
            "population",
            childId,
            ensured.Contains("actor"),
            out failureReason);

        public bool TryEnsureHousingResolution(
            ReproductionProcess process,
            CharacterId childId,
            int absoluteDay,
            out string failureReason) => TryEnsure(
            "housing",
            childId,
            ensured.Contains("population") && absoluteDay >= 1,
            out failureReason);

        public ReproductionFactLocation ResolveLocation(
            ReproductionProcess process) => new(
            process.SupportFacilityInstanceId,
            string.Empty,
            4,
            7);

        private bool TryEnsure(
            string boundary,
            CharacterId childId,
            bool valid,
            out string failureReason)
        {
            ChildIds.Add(childId);
            if (!valid)
                throw new InvalidOperationException(
                    "Focused birth boundary received invalid authoritative facts.");
            if (FailBoundary == boundary)
            {
                failureReason = "injected-reproduction-birth-" + boundary;
                return false;
            }
            if (ensured.Add(boundary))
                EnsureCounts[boundary] = 1;
            failureReason = string.Empty;
            return true;
        }
    }

    private sealed class MutableHouseholdBedInventory : IHouseholdBedInventory
    {
        private IReadOnlyList<HouseholdBedCandidate> candidates =
            Array.Empty<HouseholdBedCandidate>();

        public IReadOnlyList<HouseholdBedCandidate> GetOperationalBedSlots() =>
            candidates;

        internal void Set(params HouseholdBedCandidate[] values) =>
            candidates = values ?? Array.Empty<HouseholdBedCandidate>();
    }

    private sealed class StandaloneHouseholdService : IHouseholdService
    {
        private CharacterHouseholdAggregate aggregate = new();

        public IReadOnlyList<CharacterRoomAssignmentSaveData> Assignments =>
            aggregate.Assignments;
        public IReadOnlyList<HouseholdPendingAssignmentSaveData>
            PendingAssignments => aggregate.PendingAssignments;

        public void Assign(
            CharacterId characterId,
            HouseholdId householdId,
            BuildingInstanceId roomId,
            BuildingInstanceId bedId,
            int bedSlotIndex = 0) => aggregate.Assign(
            characterId,
            householdId,
            roomId,
            bedId,
            bedSlotIndex);

        public void MarkPending(
            CharacterId characterId,
            HouseholdId preferredHouseholdId,
            CharacterId firstParentId,
            CharacterId secondParentId,
            CharacterId carrierId,
            int requestedAbsoluteDay,
            string reasonCode) => aggregate.MarkPending(
            characterId,
            preferredHouseholdId,
            firstParentId,
            secondParentId,
            carrierId,
            requestedAbsoluteDay,
            reasonCode);

        public void ClearPending(CharacterId characterId) =>
            aggregate.ClearPending(characterId);
        public void Clear(CharacterId characterId) =>
            aggregate.Clear(characterId);
        public bool TryGet(
            CharacterId characterId,
            out CharacterRoomAssignmentSaveData assignment) =>
            aggregate.TryGet(characterId, out assignment);
        public bool TryGetPending(
            CharacterId characterId,
            out HouseholdPendingAssignmentSaveData pending) =>
            aggregate.TryGetPending(characterId, out pending);
        public IReadOnlyList<CharacterId> GetMembers(HouseholdId householdId) =>
            aggregate.GetMembers(householdId);
        internal HouseholdWorldSaveData Capture() => aggregate.Capture();
        internal void Restart(HouseholdWorldSaveData data) =>
            aggregate = CharacterHouseholdAggregate.Restore(data);
    }

    private sealed class OutcomeEnvironment : IDisposable
    {
        internal OutcomeEnvironment(
            string runId,
            Func<IGameplayOutcomeDiagnosticsQuery,
                IGameplayOutcomeDiagnosticsQuery> observationDecorator = null)
        {
            Names = new AnyDisplayNames();
            MigratedProducerOutcomeDescriptorCatalog migratedDescriptors = new(
                new KoreanJosaFormatter());
            Registry = new GameplayOutcomeRegistry(
                new IGameplayOutcomeDescriptor[]
                {
                    new ReproductionProcessTransitionOutcomeDescriptor(),
                    new PolicyValueChangeOutcomeDescriptor()
                },
                new IGameplayOutcomeAdapterRegistration[]
                {
                    new MigratedProducerOutcomeAdapter(),
                    new ReproductionProcessTransitionOutcomeAdapter(),
                    new PolicyValueChangeOutcomeAdapter()
                },
                new IGameplayOutcomeDescriptorCatalog[]
                {
                    migratedDescriptors
                });
            Ledger = new GameplayOutcomeLedger(
                Registry,
                new GameplayOutcomeBufferLimits(128, 8, 512, 128, 8),
                new GameplayOutcomeRunId(runId),
                1L);
            GameplayOutcomeRecorder recorder = new(
                Ledger,
                Registry,
                new GameEventBus());
            Recorder = recorder;
            IGameplayOutcomeDiagnosticsQuery observations =
                observationDecorator?.Invoke(Ledger) ?? Ledger;
            Facts = new DomainFactDispatcher();
            Facts.Subscribe(
                "test:reproduction-transition",
                new GameplayDomainFactObserver<
                    ReproductionTransitionFact,
                    ReproductionProcessTransitionOutcomeReceipt>(
                    new ReproductionTransitionFactMapping(observations).Capture,
                    Registry,
                    Ledger,
                    Recorder,
                    new GameplayDomainFactCommitGroup(Recorder, Ledger)));
            Migrated = new MigratedProducerOutcomeRuntime(
                new DungeonRuntimeAggregateRootStore(),
                Ledger,
                recorder,
                Ledger);
        }

        internal AnyDisplayNames Names { get; }
        internal GameplayOutcomeRegistry Registry { get; }
        internal GameplayOutcomeLedger Ledger { get; }
        internal GameplayOutcomeRecorder Recorder { get; }
        internal DomainFactDispatcher Facts { get; }
        internal MigratedProducerOutcomeRuntime Migrated { get; }

        internal IReadOnlyList<GameplayOutcomeSnapshot> AllRows() => Ledger
            .GetGlobal(OutcomeCursor.FirstPage(), OutcomeFilter.All)
            .Items
            .Select(value => value.Exact)
            .Where(value => value != null)
            .ToArray();

        internal IReadOnlyList<GameplayOutcomeSnapshot> ReproductionRows() =>
            AllRows().Where(value => value.outcomeTypeId
                == ReproductionProcessTransitionOutcomeIds.Transitioned.Value)
            .ToArray();

        internal IReadOnlyList<GameplayOutcomeSnapshot> RowsForProcess(
            string processId) => ReproductionRows().Where(value =>
                Fact(value, ReproductionProcessTransitionOutcomeIds.ProcessId)
                    == processId).ToArray();

        public void Dispose() => Facts.Dispose();
    }

    private sealed class HideFirstOutcomeObservation :
        IGameplayOutcomeDiagnosticsQuery
    {
        private readonly IGameplayOutcomeDiagnosticsQuery inner;
        private bool hideNext = true;

        internal HideFirstOutcomeObservation(
            IGameplayOutcomeDiagnosticsQuery inner) =>
            this.inner = inner ?? throw new ArgumentNullException(nameof(inner));

        public GameplayOutcomeLedgerDiagnostics GetDiagnostics() =>
            inner.GetDiagnostics();

        public IReadOnlyList<GameplayOutcomeOutboxSnapshot>
            GetOutboxSnapshot() => inner.GetOutboxSnapshot();

        public bool TryGetResultIdentity(
            GameplayResultKey resultKey,
            out GameplayOutcomeReplayIdentity identity)
        {
            if (hideNext)
            {
                hideNext = false;
                identity = default;
                return false;
            }
            return inner.TryGetResultIdentity(resultKey, out identity);
        }
    }

    private sealed class FaultingTransitionFacts :
        IDomainFactStagingPublisher,
        IDomainFactRecoveryPublisher
    {
        private readonly IDomainFactStagingPublisher staging;
        private readonly IDomainFactRecoveryPublisher recovery;

        internal FaultingTransitionFacts(DomainFactDispatcher inner)
        {
            staging = inner ?? throw new ArgumentNullException(nameof(inner));
            recovery = inner;
        }

        internal bool FailNextPrepare { get; set; }
        internal bool FailNextCommit { get; set; }
        internal bool LoseNextReply { get; set; }
        internal bool CommitBeforeLosingReply { get; set; }

        public bool TryStage<TFact>(
            in TFact fact,
            out IDomainFactStage stage,
            out string failure)
        {
            if (FailNextPrepare)
            {
                FailNextPrepare = false;
                stage = null;
                failure = "injected-reproduction-transition-prepare-rejection";
                return false;
            }
            if (!staging.TryStage(fact, out IDomainFactStage prepared, out failure))
            {
                stage = null;
                return false;
            }
            stage = new FaultingStage(this, prepared);
            return true;
        }

        public bool ReplayBatch(
            IReadOnlyList<DomainFactReplay> facts,
            DomainFactDelivery[] results,
            out string failure)
        {
            if (FailNextPrepare || FailNextCommit)
            {
                string detail = FailNextPrepare
                    ? "injected-reproduction-transition-prepare-rejection"
                    : "injected-reproduction-transition-commit-rejection";
                FailNextPrepare = false;
                FailNextCommit = false;
                for (int index = 0; index < results.Length; index++)
                    results[index] = DomainFactDelivery.Reject(detail);
                failure = detail;
                return false;
            }
            return recovery.ReplayBatch(facts, results, out failure);
        }

        private DomainFactDelivery Commit(IDomainFactStage prepared)
        {
            if (LoseNextReply)
            {
                LoseNextReply = false;
                if (CommitBeforeLosingReply) prepared.Commit();
                else prepared.Cancel();
                return DomainFactDelivery.Uncertain("injected-reproduction-observer-reply-lost");
            }
            if (!FailNextCommit)
                return prepared.Commit();
            FailNextCommit = false;
            prepared.Cancel();
            return DomainFactDelivery.Reject(
                "injected-reproduction-transition-commit-rejection");
        }

        private sealed class FaultingStage : IDomainFactStage
        {
            private readonly FaultingTransitionFacts owner;
            private readonly IDomainFactStage inner;

            internal FaultingStage(
                FaultingTransitionFacts owner,
                IDomainFactStage inner)
            {
                this.owner = owner;
                this.inner = inner;
            }

            public string OperationKey => inner.OperationKey;
            public long OwnerRevision => inner.OwnerRevision;
            public bool IsReplay => inner.IsReplay;
            public bool IsActive => inner.IsActive;
            public DomainFactDelivery Commit() => owner.Commit(inner);
            public void Cancel() => inner.Cancel();
        }
    }

    private sealed class StandaloneReproductionPersistence :
        IReproductionPersistence
    {
        private readonly IReproductionDefinitionCatalog definitions;

        internal StandaloneReproductionPersistence(
            IReproductionDefinitionCatalog definitions)
        {
            this.definitions = definitions;
            Current = new ReproductionWorldAggregate();
        }

        internal ReproductionWorldAggregate Current { get; private set; }
        public ReproductionWorldSaveData Capture() => Current.Capture();
        public ReproductionWorldAggregate PrepareRestore(
            ReproductionWorldSaveData data) =>
            ReproductionWorldAggregate.Restore(data, definitions);
        public void PublishRestore(ReproductionWorldAggregate candidate) =>
            Current = candidate ?? throw new ArgumentNullException(nameof(candidate));
        internal void Restart(ReproductionWorldSaveData data) =>
            PublishRestore(PrepareRestore(data));
    }

    private sealed class FixedReproductionDefinitions :
        IReproductionDefinitionCatalog
    {
        private readonly Dictionary<CharacterSpeciesId, ReproductionDefinition>
            values;

        internal FixedReproductionDefinitions(
            params ReproductionDefinition[] definitions) =>
            values = definitions.ToDictionary(value => value.SpeciesId);

        public ReproductionDefinition RequireReproduction(
            CharacterSpeciesId speciesId) => values.TryGetValue(
                speciesId,
                out ReproductionDefinition value)
            ? value
            : throw new KeyNotFoundException(speciesId.Value);
    }

    private sealed class AnyDisplayNames :
        IGameplayOutcomeDisplayNameQuery,
        IReproductionFactNameQuery
    {
        public bool TryCharacter(CharacterId id, out string name)
        {
            if (!id.IsValid)
            {
                name = string.Empty;
                return false;
            }
            name = id.Value.EndsWith("second-parent", StringComparison.Ordinal)
                ? "도윤"
                : "연화";
            return true;
        }

        public bool TryFacility(string id, out string name)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                name = string.Empty;
                return false;
            }
            name = "검증 산실";
            return true;
        }

        public bool TryGetCurrentName(
            GameplayEntityId entityId,
            out KoreanNameSnapshot name)
        {
            string display = entityId.Kind.Value == "facility"
                ? "검증 산실"
                : entityId.Value.EndsWith("second-parent", StringComparison.Ordinal)
                    ? "도윤"
                    : "연화";
            name = new KoreanNameSnapshot(
                display,
                "reproduction-transition-name-v1",
                KoreanPronunciationHint.AutoHangulDisplay(
                    "reproduction-transition-pronunciation-v1"),
                "ko-KR");
            return entityId.IsValid;
        }
    }

    private sealed class MutableStock : IStockQuery
    {
        private readonly Dictionary<string, WorldItemStackSnapshot> stacks;

        internal MutableStock(params WorldItemStackSnapshot[] stacks) =>
            this.stacks = (stacks ?? Array.Empty<WorldItemStackSnapshot>())
                .ToDictionary(value => value.StackId, StringComparer.Ordinal);

        public IReadOnlyList<WorldItemStackSnapshot> GetAllStacks() =>
            stacks.Values.OrderBy(value => value.StackId, StringComparer.Ordinal)
                .ToArray();
        public int GetGlobalQuantity(string itemDefinitionId) => stacks.Values
            .Where(value => value.ItemId == itemDefinitionId)
            .Sum(value => value.Quantity);
        public int GetWarehouseQuantity(
            BuildingInstanceId warehouseId,
            string itemDefinitionId) => 0;
        public int GetWarehouseQuantity(
            BuildingInstanceId warehouseId,
            StockCategory category) => 0;
        public int GetWarehouseTotal(BuildingInstanceId warehouseId) => 0;
        internal int Quantity(string stackId) => stacks.TryGetValue(
            stackId,
            out WorldItemStackSnapshot value)
            ? value.Quantity
            : 0;
        internal bool TryConsume(string stackId, int quantity)
        {
            if (!stacks.TryGetValue(stackId, out WorldItemStackSnapshot value)
                || quantity <= 0
                || value.Quantity < quantity)
                return false;
            value.Quantity -= quantity;
            return true;
        }
    }

    private sealed class MutableReservations : IItemReservationService
    {
        private readonly MutableStock stock;
        private readonly Dictionary<string, ReservedItemConsumption[]> byOwner =
            new(StringComparer.Ordinal);

        internal MutableReservations(MutableStock stock) => this.stock = stock;
        public bool TryReserve(IEnumerable<string> stackIds, string persistentId) =>
            false;
        public bool TryReserveQuantities(
            IEnumerable<ReservedItemConsumption> quantities,
            string ownerOperationId,
            ItemReservationPurpose purpose,
            string aggregationCohortId)
        {
            string owner = ownerOperationId?.Trim() ?? string.Empty;
            ReservedItemConsumption[] rows = (quantities
                    ?? Array.Empty<ReservedItemConsumption>())
                .ToArray();
            if (owner.Length == 0 || rows.Length == 0
                || rows.Any(value => !value.IsValid
                    || stock.Quantity(value.StackId) < value.Quantity))
                return false;
            if (byOwner.TryGetValue(owner, out ReservedItemConsumption[] existing))
                return existing.Select(value => (value.StackId, value.Quantity))
                    .SequenceEqual(rows.Select(value =>
                        (value.StackId, value.Quantity)));
            byOwner.Add(owner, rows);
            return true;
        }
        public void Release(string stackId, string persistentId)
        {
            string owner = persistentId?.Trim() ?? string.Empty;
            if (!byOwner.TryGetValue(owner, out ReservedItemConsumption[] rows))
                return;
            ReservedItemConsumption[] remaining = rows
                .Where(value => value.StackId != stackId)
                .ToArray();
            if (remaining.Length == 0)
                byOwner.Remove(owner);
            else
                byOwner[owner] = remaining;
        }
        public bool TryClear(string stackId) => true;
        public bool SetForbidden(string stackId, bool forbidden) => true;
        public bool PrioritizeHaul(string stackId) => true;
        internal bool TryTake(
            string owner,
            IReadOnlyList<ReservedItemConsumption> expected,
            out ReservedItemConsumption[] rows)
        {
            if (!byOwner.TryGetValue(owner, out rows)
                || !rows.Select(value => (value.StackId, value.Quantity))
                    .SequenceEqual(expected.Select(value =>
                        (value.StackId, value.Quantity))))
                return false;
            byOwner.Remove(owner);
            return true;
        }
    }

    private sealed class MutableAtomicConsumption :
        IAtomicItemConsumptionService
    {
        private readonly MutableStock stock;
        private readonly MutableReservations reservations;

        internal MutableAtomicConsumption(
            MutableStock stock,
            MutableReservations reservations)
        {
            this.stock = stock;
            this.reservations = reservations;
        }

        internal int ConsumeCalls { get; private set; }
        public bool TryConsumeReserved(
            IReadOnlyList<ReservedItemConsumption> consumptions,
            string reservationOwnerId,
            out DomainFailure failure)
        {
            failure = DomainFailure.None;
            if (!reservations.TryTake(
                    reservationOwnerId,
                    consumptions,
                    out ReservedItemConsumption[] rows)
                || rows.Any(value => stock.Quantity(value.StackId)
                    < value.Quantity))
            {
                failure = new DomainFailure(
                    FailureCode.ItemTransferConsumptionFailed);
                return false;
            }
            foreach (ReservedItemConsumption row in rows)
                if (!stock.TryConsume(row.StackId, row.Quantity))
                    throw new InvalidOperationException(
                        "Prevalidated mutable stock consumption diverged.");
            ConsumeCalls++;
            return true;
        }
    }

    private sealed class RecordingEquipmentSlots :
        IDurableFacilityEquipmentSlotCommand
    {
        internal DurableFacilityEquipmentSlotSnapshot Snapshot { get; private set; }
        public DurableFacilityEquipmentSlotResult TryReconcile(
            DurableFacilityEquipmentAssignment desired)
        {
            Snapshot = new DurableFacilityEquipmentSlotSnapshot(
                desired,
                1L,
                DurableFacilityEquipmentSlotIdentity.BuildDestinationId(
                    desired.Key,
                    1L),
                DurableFacilityEquipmentSlotIdentity.BuildOwnerOperationId(
                    desired.Key,
                    1L),
                DurableFacilityEquipmentFingerprint.CreateAssignment(desired),
                new DurableFacilityEquipmentCapacityProjection(
                    DurableFacilityEquipmentSlotIdentity.DefinitionMassPolicyKind,
                    new PhysicalMassGrams(1_000L),
                    1L,
                    new string('a', 64)),
                new[]
                {
                    new DurableFacilityEquipmentRequirementStatus(
                        desired.Requirements[0],
                        0,
                        1)
                });
            return new DurableFacilityEquipmentSlotResult(
                DurableFacilityEquipmentSlotStatus.Applied,
                Snapshot,
                string.Empty);
        }
        public DurableFacilityEquipmentSlotResult TryEnsureSupply(
            DurableFacilityEquipmentSlotKey key) =>
            new(
                DurableFacilityEquipmentSlotStatus.Replay,
                Snapshot,
                string.Empty);
        public DurableFacilityEquipmentSlotResult TryClose(
            DurableFacilityEquipmentSlotKey key,
            string reasonCode) => throw new NotSupportedException();
        public IReadOnlyList<DurableFacilityEquipmentSlotResult>
            TryAdvancePending() => Array.Empty<DurableFacilityEquipmentSlotResult>();
    }

    private sealed class RecordingEquipmentUse :
        IDurableFacilityEquipmentUseCommand
    {
        private readonly RecordingEquipmentSlots slots;
        internal RecordingEquipmentUse(RecordingEquipmentSlots slots) =>
            this.slots = slots;
        internal double AppliedWear { get; private set; }
        public DurableFacilityEquipmentUseResult TryApplyWearAndEffect(
            DurableFacilityEquipmentSlotKey key,
            string requirementId,
            double wearAmount,
            IDurableFacilityEquipmentEffectCommit effect)
        {
            DurableFacilityEquipmentSlotSnapshot snapshot = slots.Snapshot;
            DurableFacilityEquipmentRequirement requirement =
                snapshot.Assignment.Requirements[0];
            DurableFacilityEquipmentUseSubject before = new(
                "stack:qa:breeding-ledger",
                1L,
                requirement.ItemId,
                1,
                new[] { Durability(current: 10d, maximum: 10d) });
            if (!effect.TryPreflight(
                    snapshot,
                    requirement,
                    before,
                    wearAmount,
                    out string failure))
                return Failed(snapshot, failure);
            AppliedWear += wearAmount;
            DurableFacilityEquipmentUseSubject after = new(
                "stack:qa:breeding-ledger",
                2L,
                requirement.ItemId,
                1,
                new[] { Durability(current: 9d, maximum: 10d) });
            DurableFacilityEquipmentUseContext context = new(
                snapshot,
                requirement,
                before,
                after,
                wearAmount);
            if (!effect.TryCommit(context, out failure))
            {
                AppliedWear -= wearAmount;
                return Failed(snapshot, failure);
            }
            return new DurableFacilityEquipmentUseResult(
                DurableFacilityEquipmentUseStatus.Applied,
                snapshot,
                before.StackId,
                string.Empty,
                committedContext: context);
        }

        private static DurableFacilityEquipmentComponentSnapshot Durability(
            double current,
            double maximum) => new(
            ItemInstanceComponentIds.Durability,
            1,
            new[]
            {
                new DurableFacilityEquipmentComponentValueSnapshot(
                    "current",
                    ItemStateValueKind.Decimal,
                    string.Empty,
                    0L,
                    current,
                    false),
                new DurableFacilityEquipmentComponentValueSnapshot(
                    "maximum",
                    ItemStateValueKind.Decimal,
                    string.Empty,
                    0L,
                    maximum,
                    false)
            });
        private static DurableFacilityEquipmentUseResult Failed(
            DurableFacilityEquipmentSlotSnapshot snapshot,
            string failure) => new(
            DurableFacilityEquipmentUseStatus.Deferred,
            snapshot,
            string.Empty,
            failure);
    }

    private sealed class FixedFacilityQuery : IFacilityCapabilityQuery
    {
        private readonly BuildableObject facility;
        private readonly string definitionId;
        internal FixedFacilityQuery(
            BuildableObject facility,
            string definitionId)
        {
            this.facility = facility;
            this.definitionId = definitionId;
        }
        public IReadOnlyList<BuildableObject> FindOperational(
            FacilityCapabilityKind capability,
            string buildingDefinitionId = "") =>
            string.Equals(
                buildingDefinitionId,
                definitionId,
                StringComparison.Ordinal)
                ? new[] { facility }
                : Array.Empty<BuildableObject>();
        public IReadOnlyList<BuildableObject> FindOperational(
            ResearchFacilityCommandKind command) =>
            Array.Empty<BuildableObject>();
    }

    private sealed class FixedLifeQuery : ICharacterLifeQuery
    {
        private readonly Dictionary<CharacterId, CharacterLifeRecord> records;
        internal FixedLifeQuery(IEnumerable<CharacterLifeRecord> values) =>
            records = values.ToDictionary(value => value.CharacterId);
        public int Version => 1;
        public IReadOnlyCollection<CharacterLifeRecord> Records =>
            records.Values.ToArray();
        public bool TryGet(
            CharacterId characterId,
            out CharacterLifeRecord record) =>
            records.TryGetValue(characterId, out record);
    }

    private sealed class FixedCharacterWorld : ICharacterWorldQuery
    {
        internal FixedCharacterWorld(IReadOnlyList<CharacterActor> actors) =>
            Characters = actors;
        public int CharacterVersion => 1;
        public IReadOnlyList<CharacterActor> Characters { get; }
    }

    private sealed class FixedKinshipQuery : IKinshipQuery
    {
        public IReadOnlyCollection<CharacterTombstoneSaveData> Tombstones =>
            Array.Empty<CharacterTombstoneSaveData>();
        public IReadOnlyList<CharacterId> GetParents(
            CharacterId child,
            bool includeAdoptive) => Array.Empty<CharacterId>();
        public IReadOnlyList<CharacterId> GetChildren(
            CharacterId parent,
            bool includeAdoptive) => Array.Empty<CharacterId>();
        public int GetGeneration(CharacterId characterId) => 0;
        public bool IsAncestor(
            CharacterId possibleAncestor,
            CharacterId descendant,
            int maximumDepth) => false;
        public bool IsSibling(CharacterId left, CharacterId right) => false;
        public KinshipRestriction GetPartnershipOrReproductionRestriction(
            CharacterId left,
            CharacterId right) => KinshipRestriction.None;
        public CharacterId GetPartner(CharacterId characterId) => default;
        public CharacterId GetGuardian(CharacterId child) => default;
        public bool TryGetTombstone(
            CharacterId characterId,
            out CharacterTombstoneSaveData tombstone)
        {
            tombstone = null;
            return false;
        }
    }

    private sealed class RecordingSettlementAlertService :
        ISettlementAlertService
    {
        private readonly Dictionary<string, long> revisions =
            new(StringComparer.Ordinal);
        private readonly HashSet<string> active = new(StringComparer.Ordinal);

        internal bool IsActive(string processId) => active.Contains(
            ReproductionEmergencyExtractionCommandRuntime.IncidentId(processId));

        public SettlementAlertSnapshot Capture()
        {
            SettlementThreatAlertLevel level = active.Count > 0
                ? SettlementThreatAlertLevel.Red
                : SettlementThreatAlertLevel.Green;
            return new SettlementAlertSnapshot(
                level,
                level,
                1L,
                0L,
                -1L,
                0,
                active.Count,
                active.OrderBy(value => value, StringComparer.Ordinal).ToArray(),
                Array.Empty<SettlementSuspendedWorkSnapshot>(),
                EmergencyReserveCoverageBand.Adequate,
                1f,
                -1L);
        }

        public EmergencyAccountingResult PublishIncidentSignal(
            SettlementIncidentSignal signal)
        {
            if (string.IsNullOrWhiteSpace(signal.IncidentId)
                || signal.RequiredLevel == SettlementThreatAlertLevel.Green
                || signal.Revision < 0L)
                return EmergencyAccountingResult.Fail(
                    "invalid-signal",
                    "invalid test alert signal");
            if (revisions.TryGetValue(signal.IncidentId, out long previous)
                && signal.Revision <= previous)
                return EmergencyAccountingResult.Ok("duplicate-signal");
            revisions[signal.IncidentId] = signal.Revision;
            active.Add(signal.IncidentId);
            return EmergencyAccountingResult.Ok("published");
        }

        public EmergencyAccountingResult ResolveIncident(
            string incidentId,
            long revision)
        {
            if (!revisions.TryGetValue(incidentId, out long previous))
                return EmergencyAccountingResult.Fail(
                    "missing-signal",
                    "missing test alert signal");
            if (revision <= previous)
                return EmergencyAccountingResult.Ok("duplicate-resolution");
            revisions[incidentId] = revision;
            active.Remove(incidentId);
            return EmergencyAccountingResult.Ok("resolved");
        }

        public long GetNextIncidentRevision(string incidentId) =>
            revisions.TryGetValue(incidentId, out long previous)
                ? checked(previous + 1L)
                : 0L;

        public EmergencyAccountingResult UpdateReserveCoverage(
            long currentMilliWu,
            long targetMilliWu) => EmergencyAccountingResult.Ok();
        public bool TryClaimContextTransition(
            string characterId,
            long epochId,
            bool toEmergency) => false;
        public EmergencyAccountingResult RecordSuspendedWork(
            SettlementSuspendedWorkSnapshot suspendedWork) =>
            EmergencyAccountingResult.Ok();
        public bool TryGetSuspendedWork(
            string characterId,
            out SettlementSuspendedWorkSnapshot suspendedWork)
        {
            suspendedWork = default;
            return false;
        }
        public EmergencyAccountingResult MarkSuspendedWorkResumed(
            string characterId,
            long epochId) => EmergencyAccountingResult.Ok();
        public EmergencyAccountingResult MarkSuspendedWorkAbandoned(
            string characterId,
            long epochId,
            string reasonCode) => EmergencyAccountingResult.Ok();
    }

    private sealed class FixedCalendar : IGameCalendar
    {
        internal FixedCalendar(int day) => Day = day;
        public int Day { get; private set; }
        public int Hour { get; private set; } = 9;
        public int Year => Current.Year;
        public int DayOfYear => Current.DayOfYear;
        public Season Season => Current.Season;
        public int DayOfSeason => Current.DayOfSeason;
        public long AbsoluteHour => Current.AbsoluteHour;
        public float ElapsedSeconds => 0f;
        public TimeOfDay TimeOfDay => TimeOfDay.Morning;
        public bool IsRunning => true;
        public CalendarDateTime Current => GameCalendarRules.Project(Day, Hour);
        public CalendarDateTime GetRegionalTime(int utcOffsetHours) =>
            GameCalendarRules.ProjectRegional(Day, Hour, utcOffsetHours);
        public void Start()
        {
        }
        public void SetDateTime(int day, int hour)
        {
            Day = day;
            Hour = hour;
        }
    }

    private static CharacterSO Clone(CharacterSO authored, string name)
    {
        CharacterSO clone = UnityEngine.Object.Instantiate(authored);
        clone.hideFlags = HideFlags.HideAndDontSave;
        clone.characterType = CharacterType.NPC;
        clone.characterName = name;
        return clone;
    }

    private static GameObject CreateActor(
        string objectName,
        CharacterSO data,
        CharacterId id,
        CharacterSpeciesId speciesId,
        ReproductiveRole role)
    {
        GameObject actorObject = CharacterAiPlanDebugFixtures.CreateActorObject(
            objectName);
        actorObject.SetActive(false);
        if (actorObject.GetComponent<AbilityWork>() == null)
            actorObject.AddComponent<AbilityWork>();
        CharacterAiEditorTestDependencies.Inject(actorObject);
        actorObject.SetActive(true);
        CharacterActor actor = actorObject.GetComponent<CharacterActor>();
        actor.EnsureRuntimeState();
        actor.RefreshAbilityCache();
        actor.Identity.SetPersistentId(id);
        actor.Initialize(
            data,
            new CharacterSpawnRequest(
                data.DefinitionId,
                speciesId,
                data.VisualVariantId,
                role,
                Array.Empty<CharacterTraitId>()));
        actor.characterType = CharacterType.NPC;
        return actorObject;
    }
}
#endif
