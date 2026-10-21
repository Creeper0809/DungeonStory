#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using DungeonStory.Content.CoreSession;
using DungeonStory.Foundation;
using DungeonStory.Foundation.Facts;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public static class MigratedProducerCharacterSurvivalEntryE2EDebugScenarios
{
    private static DomainFactDispatcher CreateCharacterLifeFacts(
        MigratedProducerOutcomeEditorFixture outcomes,
        MigratedProducerOutcomeProbeTransaction probe)
    {
        var dispatcher = new DomainFactDispatcher();
        try
        {
            dispatcher.Subscribe(
                "fixture:character-life-transition",
                new MigratedProducerDomainFactProbeObserver<
                    CharacterLifeTransitionFact>(
                    CharacterLifeTransitionFactMapping.Capture,
                    outcomes.Transaction,
                    new GameplayDomainFactCommitGroup(
                        outcomes.Recorder,
                        outcomes.Ledger),
                    probe));
            return dispatcher;
        }
        catch
        {
            dispatcher.Dispose();
            throw;
        }
    }

    public static void RunC01O02FromBatch()
    {
        if (!Application.isBatchMode
            || !string.Equals(
                Environment.GetEnvironmentVariable(
                    "DUNGEONSTORY_U0_C01O02_FUNERAL_LINEAGE_VERIFY"),
                "1",
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "C:O01-O02 verification requires explicit isolated batch mode.");
        }

        string output = Environment.GetEnvironmentVariable(
            "DUNGEONSTORY_AUDIT_REMEDIATION_OUTPUT");
        if (string.IsNullOrWhiteSpace(output))
        {
            throw new InvalidOperationException(
                "C:O01-O02 verification requires its isolated output directory.");
        }
        string directory = Path.GetFullPath(output);
        Directory.CreateDirectory(directory);
        string reportPath = Path.Combine(directory, "report.json");
        if (File.Exists(reportPath))
        {
            throw new InvalidOperationException(
                "C:O01-O02 report already exists: " + reportPath);
        }

        var report = new JObject
        {
            ["rowId"] = "C:O01-O02",
            ["passed"] = false,
            ["attendeeQueries"] = false,
            ["deceasedNotActor"] = false,
            ["funeralTypedRoles"] = false,
            ["lineageTypedRoles"] = false,
            ["actualHouseholdPreserved"] = false,
            ["noInventedFamily"] = false,
            ["faultAndDeliveryRetry"] = false,
            ["oldSaveMigrationAdded"] = false,
            ["fullAuditExecuted"] = false,
            ["humanApprovalRecorded"] = false,
            ["trainingExecuted"] = false,
            ["sourceFiles"] = new JArray
            {
                "Assets/Scripts/Services/Run/ObservedCareerLifeEventReceipts.cs",
                "Assets/Scripts/Services/Run/V20CampaignRuntime.cs",
                "Assets/Scripts/Services/Character/PopulationSocialRuntime.cs",
                "Assets/Scripts/Services/Narrative/GameplayOutcomeBridges/MigratedProducers/Editor/MigratedProducerCharacterSurvivalEntryE2EDebugScenarios.cs"
            }
        };

        try
        {
            var observed = new HashSet<MigratedProducerOutcomeKind>();
            VerifyObservedFuneral(observed);
            VerifyLineageCompression(observed);
            Require(
                observed.SetEquals(new[]
                {
                    MigratedProducerOutcomeKind.ObservedFuneralLifeEventReceipt,
                    MigratedProducerOutcomeKind
                        .ObservedLineageCompressionLifeEventReceipt
                }),
                "Focused funeral/lineage verification did not execute both owners.");
            report["faultAndDeliveryRetry"] = true;

            using (var funeral = new ObservedLifeEventScenario(
                       MigratedProducerOutcomeKind
                           .ObservedFuneralLifeEventReceipt,
                       ObservedLifeEventEntry.Funeral,
                       MigratedProducerOutcomeFaultMode.None,
                       deliveryFault: false,
                       funeralParticipantCount: 3))
            {
                funeral.Execute();
                GameplayOutcomeSnapshot snapshot = Exact(
                    funeral,
                    MigratedProducerOutcomeKind
                        .ObservedFuneralLifeEventReceipt);
                bool attendeeQueries =
                    funeral.VerifyEveryFuneralEntityCanQueryTheOutcome();
                bool deceasedNotActor = snapshot.participants.Any(value =>
                        value.entityId == "character:migrated-e2e:deceased"
                        && value.roleId
                            == MigratedProducerOutcomeIds.TargetRole.Value)
                    && !snapshot.participants.Any(value =>
                        value.entityId == "character:migrated-e2e:deceased"
                        && value.roleId
                            == MigratedProducerOutcomeIds.ActorRole.Value);
                bool funeralTypedRoles = snapshot.participants.Count == 5
                    && snapshot.participants.Any(value =>
                        value.entityId == "building:migrated-e2e:memorial"
                        && value.roleId
                            == MigratedProducerOutcomeIds.FacilityRole.Value)
                    && Enumerable.Range(1, 3).All(index =>
                        snapshot.participants.Any(value =>
                            value.entityId
                                == "character:migrated-e2e:mourner-" + index
                            && value.roleId
                                == MigratedProducerOutcomeIds.WitnessRole.Value));
                Require(
                    attendeeQueries && deceasedNotActor && funeralTypedRoles,
                    "Funeral evidence lost attendee query or typed role truth.");
                report["attendeeQueries"] = true;
                report["deceasedNotActor"] = true;
                report["funeralTypedRoles"] = true;
                report["funeralExample"] = JObject.FromObject(snapshot);
            }

            using (var lineage = new LineageCompressionScenario(
                       MigratedProducerOutcomeKind
                           .ObservedLineageCompressionLifeEventReceipt,
                       MigratedProducerOutcomeFaultMode.None,
                       deliveryFault: false))
            {
                lineage.Execute();
                GameplayOutcomeSnapshot snapshot = Exact(
                    lineage,
                    MigratedProducerOutcomeKind
                        .ObservedLineageCompressionLifeEventReceipt);
                const string Character = "character:migrated-e2e:lineage";
                const string Household = "household:migrated-e2e:lineage";
                bool lineageTypedRoles = snapshot.participants.Any(value =>
                        value.entityId == Character
                        && value.roleId
                            == MigratedProducerOutcomeIds.TargetRole.Value)
                    && snapshot.participants.Any(value =>
                        value.entityId == Household
                        && value.roleId
                            == MigratedProducerOutcomeIds.OwnerRole.Value);
                bool actualHousehold = snapshot.facts.Any(value =>
                        value.factId
                            == MigratedProducerOutcomeIds.HouseholdFact.Value
                        && value.value == Household)
                    && lineage.Outcomes.Ledger.GetForEntity(
                        new GameplayEntityId(
                            MigratedProducerOutcomeIds.CharacterKind,
                            Character),
                        OutcomeCursor.FirstPage(10),
                        OutcomeFilter.All).Items.Count == 1
                    && lineage.Outcomes.Ledger.GetForEntity(
                        new GameplayEntityId(
                            MigratedProducerOutcomeIds.OperationKind,
                            Household),
                        OutcomeCursor.FirstPage(10),
                        OutcomeFilter.All).Items.Count == 1;
                bool noInventedFamily = snapshot.participants.Count == 2
                    && snapshot.participants.All(value =>
                        value.entityId == Character
                        || value.entityId == Household);
                Require(
                    lineageTypedRoles && actualHousehold && noInventedFamily,
                    "Lineage evidence lost its archived target/actual household or invented a family subject.");
                report["lineageTypedRoles"] = true;
                report["actualHouseholdPreserved"] = true;
                report["noInventedFamily"] = true;
                report["lineageExample"] = JObject.FromObject(snapshot);
            }

            report["passed"] = true;
            WriteCreateOnlyJson(reportPath, report);
            Debug.Log(
                "[AuditRemediation][C:O01-O02] PASS attendees=3; "
                + "deceased-actor=0; household=1; invented-family=0");
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

    public static void RunC03FromBatch()
    {
        if (!Application.isBatchMode
            || !string.Equals(
                Environment.GetEnvironmentVariable(
                    "DUNGEONSTORY_U0_C03_PRODUCTION_LOSS_VERIFY"),
                "1",
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "C:O03 verification requires explicit isolated batch mode.");
        }

        string output = Environment.GetEnvironmentVariable(
            "DUNGEONSTORY_AUDIT_REMEDIATION_OUTPUT");
        if (string.IsNullOrWhiteSpace(output))
        {
            throw new InvalidOperationException(
                "C:O03 verification requires its isolated output directory.");
        }
        string directory = Path.GetFullPath(output);
        Directory.CreateDirectory(directory);
        string reportPath = Path.Combine(directory, "report.json");
        if (File.Exists(reportPath))
        {
            throw new InvalidOperationException(
                "C:O03 report already exists: " + reportPath);
        }

        var report = new JObject
        {
            ["rowId"] = "C:O03",
            ["passed"] = false,
            ["durableParentChildExact"] = false,
            ["sourceTruthExact"] = false,
            ["observationPhaseSavedAndRestored"] = false,
            ["parentRevalidationFailureRejected"] = false,
            ["duplicateObservationNoOp"] = false,
            ["faultAndDeliveryRetry"] = false,
            ["oldSaveMigrationAdded"] = false,
            ["fullAuditExecuted"] = false,
            ["humanApprovalRecorded"] = false,
            ["trainingExecuted"] = false,
            ["sourceFiles"] = new JArray
            {
                "Assets/Scripts/Services/Economy/ProductionRecipeExecutionReceiptAuthority.cs",
                "Assets/Scripts/Services/Run/ObservedCareerLifeEventReceipts.cs",
                "Assets/Scripts/Services/Run/V20CampaignRuntime.cs",
                "Assets/Scripts/Services/Narrative/GameplayOutcomeBridges/MigratedProducers/Editor/MigratedProducerCharacterSurvivalEntryE2EDebugScenarios.cs"
            }
        };

        try
        {
            var observed = new HashSet<MigratedProducerOutcomeKind>();
            VerifyObservedProductionLoss(observed);
            Require(
                observed.SetEquals(new[]
                {
                    MigratedProducerOutcomeKind
                        .ObservedProductionLossLifeEventReceipt
                }),
                "Focused production-loss verification did not execute its owner.");
            report["parentRevalidationFailureRejected"] = true;
            report["faultAndDeliveryRetry"] = true;

            const MigratedProducerOutcomeKind Kind =
                MigratedProducerOutcomeKind
                    .ObservedProductionLossLifeEventReceipt;
            using var normal = new ObservedProductionLossScenario(
                Kind,
                MigratedProducerOutcomeFaultMode.None,
                deliveryFault: false);
            normal.Execute();
            GameplayOutcomeSnapshot snapshot = Exact(normal, Kind);
            ObservedProductionLossLifeEventReceipt receipt =
                normal.LastReceipt;

            bool parentChildExact = new GameplayOutcomeId(new GameplayOutcomeRunId(snapshot.parentRunId), snapshot.parentSequence).ToString()
                    == receipt.ParentPublication.PublicationId
                && snapshot.causationRelationId
                    == "production-declared-loss-observation";
            bool sourceTruthExact = snapshot.participants.Count == 4
                && snapshot.participants.Any(value =>
                    value.entityId == receipt.StudentCharacterId.Value
                    && value.roleId
                        == MigratedProducerOutcomeIds.ActorRole.Value)
                && snapshot.participants.Any(value =>
                    value.entityId == receipt.MentorCharacterId.Value
                    && value.roleId
                        == MigratedProducerOutcomeIds.WitnessRole.Value)
                && snapshot.participants.Any(value =>
                    value.entityId == receipt.FacilityId.Value
                    && value.roleId
                        == MigratedProducerOutcomeIds.FacilityRole.Value)
                && snapshot.participants.Any(value =>
                    value.entityId == receipt.AcademyBuildingId.Value
                    && value.roleId
                        == MigratedProducerOutcomeIds.SourceRole.Value)
                && snapshot.metrics.Any(value =>
                    value.metricId
                        == MigratedProducerOutcomeIds.MassMetric.Value
                    && value.value == receipt.DeclaredLossMassGrams)
                && snapshot.facts.Any(value =>
                    value.factId == MigratedProducerOutcomeIds.BillFact.Value
                    && value.value == receipt.BillId.Value)
                && snapshot.facts.Any(value =>
                    value.factId == MigratedProducerOutcomeIds.BatchFact.Value
                    && value.value == receipt.BatchCommitId);
            Require(
                parentChildExact && sourceTruthExact,
                "Production-loss child lost its durable parent or exact source truth.");

            SocietyEventWorldSaveData restored =
                normal.RoundTripSocietyForEvidence();
            ObservedLifeEventReceiptSaveData observation = restored
                .successfulLifeEventOperations.Single(value =>
                    value.sourceKind
                        == ObservedLifeEventSourceKind.ProductionDeclaredLoss);
            V20ActiveEventSaveData active = restored.activeEvents.SingleOrDefault(
                value => value != null
                    && value.instanceId == observation.occurrenceInstanceId);
            bool observationPhaseSaved = active != null
                && !restored.recentResolvedEvents.Any(value => value != null
                    && value.instanceId == observation.occurrenceInstanceId)
                && string.Equals(
                    observation.canonicalPayload,
                    receipt.CanonicalPayload,
                    StringComparison.Ordinal);
            Require(
                observationPhaseSaved,
                "Production-loss observation phase did not survive the current society save round trip.");

            normal.Execute();
            SocietyEventWorldSaveData replayed = normal.CaptureSocietyForEvidence();
            bool duplicateNoOp = replayed.successfulLifeEventOperations.Count(
                    value => value != null
                        && value.sourceKind
                            == ObservedLifeEventSourceKind
                                .ProductionDeclaredLoss) == 1
                && normal.Probe.CommitCount(Kind) == 1;
            Require(
                duplicateNoOp,
                "Production-loss replay duplicated its observation or child outcome.");

            report["durableParentChildExact"] = true;
            report["sourceTruthExact"] = true;
            report["observationPhaseSavedAndRestored"] = true;
            report["duplicateObservationNoOp"] = true;
            report["outcomeExample"] = JObject.FromObject(snapshot);
            report["savedObservation"] = JObject.FromObject(observation);
            report["savedActiveOccurrence"] = JObject.FromObject(active);
            report["passed"] = true;
            WriteCreateOnlyJson(reportPath, report);
            Debug.Log(
                "[AuditRemediation][C:O03] PASS parent-child=1; "
                + "observation-save=1; parent-missing-rejected=1; duplicate=0");
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

    public static void RunC04O05FromBatch()
    {
        if (!Application.isBatchMode
            || !string.Equals(
                Environment.GetEnvironmentVariable(
                    "DUNGEONSTORY_U0_C04O05_CAREER_OBSERVATION_VERIFY"),
                "1",
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "C:O04-O05 verification requires explicit isolated batch mode.");
        }

        string output = Environment.GetEnvironmentVariable(
            "DUNGEONSTORY_AUDIT_REMEDIATION_OUTPUT");
        if (string.IsNullOrWhiteSpace(output))
        {
            throw new InvalidOperationException(
                "C:O04-O05 verification requires its isolated output directory.");
        }
        string directory = Path.GetFullPath(output);
        Directory.CreateDirectory(directory);
        string reportPath = Path.Combine(directory, "report.json");
        if (File.Exists(reportPath))
        {
            throw new InvalidOperationException(
                "C:O04-O05 report already exists: " + reportPath);
        }

        var report = new JObject
        {
            ["rowId"] = "C:O04-O05",
            ["passed"] = false,
            ["lastLessonMultiRoleExact"] = false,
            ["awardStackRevisionExact"] = false,
            ["rewardFailureRolledBack"] = false,
            ["saveRestoreRoundTrip"] = false,
            ["quietPromotionXpRankExact"] = false,
            ["faultAndDeliveryRetry"] = false,
            ["oldSaveMigrationAdded"] = false,
            ["fullAuditExecuted"] = false,
            ["humanApprovalRecorded"] = false,
            ["trainingExecuted"] = false,
            ["sourceFiles"] = new JArray
            {
                "Assets/Scripts/Services/Character/CareerApplicationAdapter.cs",
                "Assets/Scripts/Services/Run/ObservedCareerLifeEventReceipts.cs",
                "Assets/Scripts/Services/Narrative/GameplayOutcomeBridges/MigratedProducers/Editor/ObservedCareerArcCausalProductionEntryDebugScenarios.cs",
                "Assets/Scripts/Services/Narrative/GameplayOutcomeBridges/MigratedProducers/Editor/MigratedProducerRunOffenseEntryE2EDebugScenarios.cs",
                "Assets/Scripts/Services/Narrative/GameplayOutcomeBridges/MigratedProducers/Editor/MigratedProducerCharacterSurvivalEntryE2EDebugScenarios.cs"
            }
        };

        try
        {
            var observed = new HashSet<MigratedProducerOutcomeKind>();
            VerifyObservedLastLesson(observed);
            VerifyObservedQuietPromotion(observed);
            Require(
                observed.SetEquals(new[]
                {
                    MigratedProducerOutcomeKind
                        .ObservedLastLessonLifeEventReceipt,
                    MigratedProducerOutcomeKind
                        .ObservedQuietPromotionLifeEventReceipt
                }),
                "Focused lesson/promotion verification did not execute both owners.");
            report["faultAndDeliveryRetry"] = true;

            MigratedProducerRunOffenseEntryE2EDebugScenarios
                .VerifyCareerMentorshipAwardFocused();
            report["rewardFailureRolledBack"] = true;

            Require(
                ObservedCareerArcCausalProductionEntryDebugScenarios
                    .RunCausalOwnerFamily(log: false),
                "Observed career-arc save/restore owner family failed.");
            report["saveRestoreRoundTrip"] = true;

            using (var lesson = new ObservedLifeEventScenario(
                       MigratedProducerOutcomeKind
                           .ObservedLastLessonLifeEventReceipt,
                       ObservedLifeEventEntry.LastLesson,
                       MigratedProducerOutcomeFaultMode.None,
                       deliveryFault: false))
            {
                lesson.Execute();
                GameplayOutcomeSnapshot snapshot = Exact(
                    lesson,
                    MigratedProducerOutcomeKind
                        .ObservedLastLessonLifeEventReceipt);
                ObservedLastLessonLifeEventReceipt receipt =
                    lesson.LastLessonReceipt;
                bool multiRoleExact = snapshot.participants.Count == 5
                    && snapshot.participants.Any(value =>
                        value.entityId == receipt.MentorCharacterId.Value
                        && value.roleId
                            == MigratedProducerOutcomeIds.ActorRole.Value)
                    && snapshot.participants.Any(value =>
                        value.entityId == receipt.StudentCharacterId.Value
                        && value.roleId
                            == MigratedProducerOutcomeIds.TargetRole.Value)
                    && snapshot.participants.Any(value =>
                        value.entityId == receipt.AcademyBuildingId.Value
                        && value.roleId
                            == MigratedProducerOutcomeIds.FacilityRole.Value)
                    && snapshot.participants.Any(value =>
                        value.entityId
                            == receipt.ProtectiveEquipmentInstanceId
                        && value.roleId
                            == MigratedProducerOutcomeIds.ItemRole.Value)
                    && snapshot.participants.Any(value =>
                        value.entityId == receipt.AwardLedgerStackId
                        && value.roleId
                            == MigratedProducerOutcomeIds.DestinationRole.Value);
                bool stackRevisionExact = snapshot.metrics.Any(value =>
                        value.metricId
                            == MigratedProducerOutcomeIds.PreviousMetric.Value
                        && value.value
                            == receipt.AwardBeforeContentRevision)
                    && snapshot.metrics.Any(value =>
                        value.metricId
                            == MigratedProducerOutcomeIds.CurrentMetric.Value
                        && value.value
                            == receipt.AwardAfterContentRevision)
                    && ObservedLastLessonLifeEventReceipt.TryParse(
                        receipt.CanonicalPayload,
                        out ObservedLastLessonLifeEventReceipt restored)
                    && restored.AwardLedgerStackId == receipt.AwardLedgerStackId
                    && restored.AwardBeforeContentRevision
                        == receipt.AwardBeforeContentRevision
                    && restored.AwardAfterContentRevision
                        == receipt.AwardAfterContentRevision;
                Require(
                    multiRoleExact && stackRevisionExact,
                    "Last-lesson evidence lost a role or exact award stack/revision.");
                report["lastLessonMultiRoleExact"] = true;
                report["awardStackRevisionExact"] = true;
                report["lastLessonExample"] = JObject.FromObject(snapshot);
            }

            using (var promotion = new ObservedLifeEventScenario(
                       MigratedProducerOutcomeKind
                           .ObservedQuietPromotionLifeEventReceipt,
                       ObservedLifeEventEntry.QuietPromotion,
                       MigratedProducerOutcomeFaultMode.None,
                       deliveryFault: false))
            {
                promotion.Execute();
                GameplayOutcomeSnapshot snapshot = Exact(
                    promotion,
                    MigratedProducerOutcomeKind
                        .ObservedQuietPromotionLifeEventReceipt);
                ObservedQuietPromotionLifeEventReceipt receipt =
                    promotion.QuietPromotionReceipt;
                bool xpRankExact = snapshot.metrics.Any(value =>
                        value.metricId == MigratedProducerOutcomeIds
                            .CurrentExperienceBeforeMetric.Value
                        && value.value
                            == receipt.BeforeCurrentMilliExperience)
                    && snapshot.metrics.Any(value =>
                        value.metricId == MigratedProducerOutcomeIds
                            .CurrentExperienceAfterMetric.Value
                        && value.value
                            == receipt.AfterCurrentMilliExperience)
                    && snapshot.metrics.Any(value =>
                        value.metricId == MigratedProducerOutcomeIds
                            .LifetimeExperienceBeforeMetric.Value
                        && value.value
                            == receipt.BeforeLifetimeMilliExperience)
                    && snapshot.metrics.Any(value =>
                        value.metricId == MigratedProducerOutcomeIds
                            .LifetimeExperienceAfterMetric.Value
                        && value.value
                            == receipt.AfterLifetimeMilliExperience)
                    && snapshot.metrics.Any(value =>
                        value.metricId
                            == MigratedProducerOutcomeIds.RankBeforeMetric.Value
                        && value.value == (int)receipt.BeforeRank)
                    && snapshot.metrics.Any(value =>
                        value.metricId
                            == MigratedProducerOutcomeIds.RankAfterMetric.Value
                        && value.value == (int)receipt.AfterRank);
                Require(
                    xpRankExact,
                    "Quiet-promotion evidence lost exact XP or rank truth.");
                report["quietPromotionXpRankExact"] = true;
                report["quietPromotionExample"] = JObject.FromObject(snapshot);
            }

            report["passed"] = true;
            WriteCreateOnlyJson(reportPath, report);
            Debug.Log(
                "[AuditRemediation][C:O04-O05] PASS roles=5; "
                + "stack-revision=1; reward-failure=1; save-restore=1; xp-rank=1");
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

    public static void RunTabooSharedLedgerProbeBatch()
    {
        string output = Environment.GetEnvironmentVariable("DUNGEONSTORY_TABOO_BOUNDARY_OUTPUT");
        Require(!string.IsNullOrWhiteSpace(output), "A create-only taboo boundary output is required.");
        Directory.CreateDirectory(output);
        var cases = new JArray();
        bool passed = true;
        foreach (MigratedProducerOutcomeKind target in new[]
        {
            MigratedProducerOutcomeKind.CharacterTabooIncidentEvent,
            MigratedProducerOutcomeKind.PhysicalItemTransformReceipt
        })
        foreach (float initialHygiene in new[] { 75f, 25f })
        foreach (MigratedProducerOutcomeFaultMode fault in new[]
        {
            MigratedProducerOutcomeFaultMode.None,
            MigratedProducerOutcomeFaultMode.RejectReservation,
            MigratedProducerOutcomeFaultMode.RejectCommit
        })
        {
            using var scenario = new TabooIncidentScenario(
                target, fault, deliveryFault: false, initialHygiene: initialHygiene);
            string before = scenario.CaptureDomain();
            string error = null;
            try { scenario.Execute(); }
            catch (Exception exception) { error = exception.ToString(); }
            string after = scenario.CaptureDomain();
            const MigratedProducerOutcomeKind Physical = MigratedProducerOutcomeKind.PhysicalItemTransformReceipt;
            const MigratedProducerOutcomeKind Taboo = MigratedProducerOutcomeKind.CharacterTabooIncidentEvent;
            int physical = scenario.Probe.CommitCount(Physical);
            int taboo = scenario.Probe.CommitCount(Taboo);
            var exact = new JArray();
            foreach (MigratedProducerOutcomeKind kind in new[] { Physical, Taboo })
            {
                if (scenario.Probe.CommitCount(kind) == 0)
                    continue;
                GameplayOutcomeReplayIdentity identity = MigratedProducerOutcomeE2EAssertions.RequireExact(
                    scenario.Outcomes, kind, scenario.Probe);
                Require(scenario.Outcomes.Ledger.TryGetExact(identity.OutcomeId, out GameplayOutcomeSnapshot snapshot),
                    "Committed shared-ledger result is not exact.");
                exact.Add(JObject.FromObject(snapshot));
            }
            bool success = fault == MigratedProducerOutcomeFaultMode.None
                ? error == null && physical == 1 && taboo == 1 && before != after
                    && scenario.Publications.Values.All(count => count > 0) && scenario.VerifyReplayInert()
                : error != null && physical == 0 && taboo == 0 && before == after
                    && scenario.Publications.Values.All(count => count == 0);
            success &= !scenario.PrematurePublication;
            passed &= success;
            cases.Add(new JObject
            {
                ["target"] = target.ToString(), ["initialHygiene"] = initialHygiene,
                ["fault"] = fault.ToString(), ["passed"] = success,
                ["publications"] = JObject.FromObject(scenario.Publications),
                ["prematurePublication"] = scenario.PrematurePublication,
                ["physicalCommits"] = physical, ["tabooCommits"] = taboo,
                ["physicalCommitAttempts"] = scenario.Probe.CommitAttemptCount(Physical),
                ["tabooCommitAttempts"] = scenario.Probe.CommitAttemptCount(Taboo),
                ["domainUnchanged"] = before == after, ["before"] = before,
                ["after"] = after, ["exception"] = error, ["exactOutcomes"] = exact
            });
        }
        var deliveryCases = new JArray();
        foreach (MigratedProducerOutcomeKind target in new[]
        {
            MigratedProducerOutcomeKind.CharacterTabooIncidentEvent,
            MigratedProducerOutcomeKind.PhysicalItemTransformReceipt
        })
        {
            using var scenario = new TabooIncidentScenario(target, MigratedProducerOutcomeFaultMode.None, true);
            scenario.Execute();
            string committed = scenario.CaptureDomain();
            MigratedProducerOutcomeE2EAssertions.RequirePendingThenRetry(
                scenario.Outcomes, target, scenario.Probe, scenario.DeliveryFault);
            Require(committed == scenario.CaptureDomain() && scenario.VerifyReplayInert(),
                "Delivery retry changed a committed taboo action.");
            deliveryCases.Add(new JObject { ["target"] = target.ToString(), ["passed"] = true });
        }
        var observerCases = new JArray();
        foreach (string channel in new[] { "progression", "invalidated", "stats", "mood", "marker" })
        {
            using var scenario = new TabooIncidentScenario(
                MigratedProducerOutcomeKind.CharacterTabooIncidentEvent, MigratedProducerOutcomeFaultMode.None, false,
                initialHygiene: 25f);
            scenario.ThrowObserverChannel = channel;
            string error = null;
            try { scenario.Execute(); }
            catch (AggregateException exception) { error = exception.ToString(); }
            Require(error != null && scenario.Publications.Values.All(count => count > 0)
                && !scenario.PrematurePublication && scenario.VerifyReplayInert()
                && scenario.Probe.CommitCount(MigratedProducerOutcomeKind.CharacterTabooIncidentEvent) == 1
                && scenario.Probe.CommitCount(MigratedProducerOutcomeKind.PhysicalItemTransformReceipt) == 1,
                "Observer failure suppressed another channel or rolled back a durable taboo action: " + channel);
            observerCases.Add(new JObject { ["channel"] = channel, ["passed"] = true,
                ["publications"] = JObject.FromObject(scenario.Publications), ["exception"] = error });
        }
        JObject reentrant;
        using (var scenario = new TabooIncidentScenario(MigratedProducerOutcomeKind.CharacterTabooIncidentEvent,
            MigratedProducerOutcomeFaultMode.None, false, initialHygiene: 25f))
        {
            scenario.RecoverHygieneOnMoodPublication = true;
            scenario.Execute();
            reentrant = scenario.VerifyCapturedHygieneTransition();
        }
        var invalidJoins = new JArray();
        foreach (bool wrongLedger in new[] { true, false })
        {
            using var scenario = new TabooIncidentScenario(MigratedProducerOutcomeKind.CharacterTabooIncidentEvent,
                MigratedProducerOutcomeFaultMode.None, false, shareTransformOutcomeLedger: !wrongLedger,
                transformDayOverride: wrongLedger ? null : EditorFixedGameCalendar.Instance.Day + 1);
            string before = scenario.CaptureDomain();
            string error = null;
            try { scenario.Execute(); }
            catch (InvalidOperationException exception) { error = exception.Message; }
            Require(error != null && before == scenario.CaptureDomain()
                && scenario.Publications.Values.All(count => count == 0)
                && scenario.Probe.CommitCount(MigratedProducerOutcomeKind.CharacterTabooIncidentEvent) == 0
                && scenario.Probe.CommitCount(MigratedProducerOutcomeKind.PhysicalItemTransformReceipt) == 0,
                "Invalid ledger/date join mutated a taboo action.");
            invalidJoins.Add(new JObject { ["case"] = wrongLedger ? "different-ledger" : "different-day",
                ["passed"] = true, ["exception"] = error });
        }
        Require(RunAll(), "Existing character/survival production-entry regression failed.");
        Require(MigratedProducerEconomyServiceEntryE2EDebugScenarios.RunAll(),
            "Existing economy/standalone physical-transform regression failed.");
        var report = new JObject
        {
            ["status"] = passed ? "PASS" : "FAIL_SHARED_LEDGER_ATOMICITY",
            ["cases"] = cases, ["deliveryCases"] = deliveryCases, ["observerCases"] = observerCases,
            ["reentrantHygiene"] = reentrant,
            ["invalidJoins"] = invalidJoins,
            ["existingEconomyKinds"] = MigratedProducerEconomyServiceEntryE2EDebugScenarios.CoveredKinds.Count,
            ["existingCoveredKinds"] = CoveredKinds.Count, ["controlledSimulation"] = true,
            ["sourceCorpseWasFixtureSpawned"] = true, ["wholeWorldSave"] = false,
            ["humanApprovalClaimed"] = false, ["trainingEligible"] = false,
            ["releaseGate"] = "REJECT_BEFORE_DPO"
        };
        using (var stream = new FileStream(Path.Combine(output, "boundary-report.json"), FileMode.CreateNew))
        using (var writer = new StreamWriter(stream))
            writer.Write(report.ToString());
        Require(passed, "Shared-ledger taboo boundary failed; exact evidence preserved in " + output);
        Debug.Log("TABOO_SHARED_LEDGER_BOUNDARY=PASS; cases=12; delivery=2; observer=5; reentrant=1; invalidJoin=2; existingKinds=17+6");
    }

    public static IReadOnlyList<MigratedProducerOutcomeKind> CoveredKinds { get; } =
        new[]
        {
            MigratedProducerOutcomeKind.CharacterConsumablesMealResult,
            MigratedProducerOutcomeKind.CharacterConsumablesSubstanceResult,
            MigratedProducerOutcomeKind.CharacterDetoxTreatmentResult,
            MigratedProducerOutcomeKind.CharacterLifeStageChangedEvent,
            MigratedProducerOutcomeKind.CharacterPrimitiveSurvivalCompletedEvent,
            MigratedProducerOutcomeKind.CharacterProficiencyAwardCommitReceipt,
            MigratedProducerOutcomeKind.CharacterTabooIncidentEvent,
            MigratedProducerOutcomeKind.CharacterWaterConsumedEvent,
            MigratedProducerOutcomeKind.CharacterFoulWaterConsequenceResult,
            MigratedProducerOutcomeKind.MealMissedEvent,
            MigratedProducerOutcomeKind.ObservedFuneralLifeEventReceipt,
            MigratedProducerOutcomeKind.ObservedLastLessonLifeEventReceipt,
            MigratedProducerOutcomeKind.ObservedLineageCompressionLifeEventReceipt,
            MigratedProducerOutcomeKind.ObservedProductionLossLifeEventReceipt,
            MigratedProducerOutcomeKind.ObservedMealIncidentCaptureResult,
            MigratedProducerOutcomeKind.ObservedQuietPromotionLifeEventReceipt,
            MigratedProducerOutcomeKind.OwnerRunEndedEvent,
            MigratedProducerOutcomeKind.RestOutcomeIdentityEvent
        };

    internal static IProductionCausalActionFixture CreateCausalTreatmentFixture(
        string causalRunId = null,
        string episodeOwnerId = null)
    {
        const MigratedProducerOutcomeKind Kind =
            MigratedProducerOutcomeKind.CharacterDetoxTreatmentResult;
        var scenario = new ConsumablesScenario(
            Kind,
            ConsumablesEntry.Detox,
            MigratedProducerOutcomeFaultMode.None,
            deliveryFault: false,
            stableRunId: string.IsNullOrWhiteSpace(causalRunId)
                ? null
                : causalRunId + ":treatment-ledger",
            actorOwnerId: episodeOwnerId);
        return new DelegateProductionCausalActionFixture(
            "action:apply-detox-treatment",
            "injury-treatment",
            Kind,
            () => new Dictionary<string, object>
            {
                ["treatment.ownerSnapshot"] = scenario.CaptureDomain()
            },
            () =>
            {
                scenario.Execute();
                return true;
            },
            () => MigratedProducerOutcomeE2EAssertions.RequireExact(
                scenario.Outcomes,
                Kind,
                scenario.Probe),
            identity => ProductionCausalOutcomeEvidence.Capture(
                scenario.Outcomes.Ledger,
                identity),
            scenario.Dispose);
    }

    public static bool RunAll(bool throwOnFailure = true)
    {
        try
        {
            Require(
                CoveredKinds.Count == CoveredKinds.Distinct().Count(),
                "Character/survival migrated-producer coverage contains a duplicate kind.");
            var observed = new HashSet<MigratedProducerOutcomeKind>();

            VerifyConsumableMeal(observed);
            VerifyConsumableSubstance(observed);
            VerifyDetoxTreatment(observed);
            VerifyLifeStageChanged(observed);
            VerifyPrimitiveSurvivalCompleted(observed);
            VerifyProficiencyAward(observed);
            VerifyTabooIncident(observed);
            VerifyWaterConsumed(observed);
            VerifyMealMissed(observed);
            VerifyObservedFuneral(observed);
            VerifyObservedLastLesson(observed);
            VerifyLineageCompression(observed);
            VerifyObservedProductionLoss(observed);
            VerifyObservedMealIncident(observed);
            VerifyObservedQuietPromotion(observed);
            VerifyOwnerRunEnded(observed);
            VerifyRestOutcome(observed);

            Require(
                observed.SetEquals(CoveredKinds),
                "Character/survival migrated-producer observed coverage did not match CoveredKinds."
                + " Missing=" + string.Join(",", CoveredKinds.Except(observed))
                + "; unexpected=" + string.Join(",", observed.Except(CoveredKinds)));
            return true;
        }
        catch
        {
            if (throwOnFailure)
                throw;
            return false;
        }
    }

    public static string VerifyE11TabooButcheryFocused()
    {
        const MigratedProducerOutcomeKind Taboo =
            MigratedProducerOutcomeKind.CharacterTabooIncidentEvent;
        const MigratedProducerOutcomeKind Physical =
            MigratedProducerOutcomeKind.PhysicalItemTransformReceipt;

        using (var accepted = new TabooIncidentScenario(
                   Taboo,
                   MigratedProducerOutcomeFaultMode.None,
                   deliveryFault: false))
        {
            string before = accepted.CaptureDomain();
            accepted.Execute();
            Require(
                before != accepted.CaptureDomain()
                && accepted.Probe.CommitCount(Physical) == 1
                && accepted.Probe.CommitCount(Taboo) == 1
                && accepted.VerifyReplayInert(),
                "E11 taboo butchery did not commit one physical child and one taboo parent.");
            _ = MigratedProducerOutcomeE2EAssertions.RequireExact(
                accepted.Outcomes,
                Physical,
                accepted.Probe);
            _ = MigratedProducerOutcomeE2EAssertions.RequireExact(
                accepted.Outcomes,
                Taboo,
                accepted.Probe);
        }

        using (var rejected = new TabooIncidentScenario(
                   Taboo,
                   MigratedProducerOutcomeFaultMode.RejectCommit,
                   deliveryFault: false))
        {
            string before = rejected.CaptureDomain();
            rejected.ExecuteExpectingRejectedCommit();
            Require(
                before == rejected.CaptureDomain()
                && rejected.Probe.CommitCount(Physical) == 0
                && rejected.Probe.CommitCount(Taboo) == 0,
                "E11 rejected taboo butchery left a physical or parent commit.");
        }

        using (var pending = new TabooIncidentScenario(
                   Taboo,
                   MigratedProducerOutcomeFaultMode.None,
                   deliveryFault: true))
        {
            pending.Execute();
            string committed = pending.CaptureDomain();
            MigratedProducerOutcomeE2EAssertions.RequirePendingThenRetry(
                pending.Outcomes,
                Taboo,
                pending.Probe,
                pending.DeliveryFault);
            Require(
                committed == pending.CaptureDomain()
                && pending.Probe.CommitCount(Physical) == 1
                && pending.Probe.CommitCount(Taboo) == 1
                && pending.VerifyReplayInert(),
                "E11 taboo delivery retry duplicated or reversed butchery.");
        }

        return "taboo=PASS; physicalTransform=1; tabooParent=1; "
            + "rejectedCommitRollback=PASS; deliveryRetryNoDuplicate=PASS";
    }

    public static JObject RunW07PrimitiveFieldMealNoDuplicate()
    {
        const MigratedProducerOutcomeKind Meal =
            MigratedProducerOutcomeKind.CharacterConsumablesMealResult;
        const MigratedProducerOutcomeKind Primitive =
            MigratedProducerOutcomeKind.CharacterPrimitiveSurvivalCompletedEvent;
        const MigratedProducerOutcomeKind Physical =
            MigratedProducerOutcomeKind.PhysicalItemTransformReceipt;
        using var scenario = new ConsumablesScenario(
            Primitive,
            ConsumablesEntry.Meal,
            MigratedProducerOutcomeFaultMode.None,
            deliveryFault: false,
            primitiveFieldMeal: true,
            stableRunId: "run:w07:primitive-field-meal");

        scenario.Execute();
        GameplayOutcomeReplayIdentity mealIdentity =
            MigratedProducerOutcomeE2EAssertions.RequireExact(
                scenario.Outcomes,
                Meal,
                scenario.Probe);
        GameplayOutcomeReplayIdentity primitiveIdentity =
            MigratedProducerOutcomeE2EAssertions.RequireExact(
                scenario.Outcomes,
                Primitive,
                scenario.Probe);
        int mealCount = scenario.Probe.CommitCount(Meal);
        int primitiveCount = scenario.Probe.CommitCount(Primitive);
        int physicalCount = scenario.Probe.CommitCount(Physical);
        Require(
            scenario.LastMealAccepted
            && scenario.LastMealResult.Success
            && mealCount == 1
            && primitiveCount == 1
            && physicalCount == 0
            && string.Equals(
                scenario.LastMealResult.OutcomeResultKey,
                mealIdentity.ResultKey.ToString(),
                StringComparison.Ordinal),
            "Primitive field meal did not retain exactly one meal and one "
            + "primitive-survival result with the canonical meal result key.");

        return new JObject
        {
            ["passed"] = true,
            ["mealCommits"] = mealCount,
            ["primitiveSurvivalCommits"] = primitiveCount,
            ["physicalTransformCommits"] = physicalCount,
            ["mealResultKey"] = mealIdentity.ResultKey.ToString(),
            ["returnedMealResultKey"] =
                scenario.LastMealResult.OutcomeResultKey,
            ["primitiveResultKey"] = primitiveIdentity.ResultKey.ToString()
        };
    }

    public static void RunD12SurvivalDetailsBatch()
    {
        VerifyD12SurvivalDetails();
        Debug.Log(
            "D12_SURVIVAL_DETAILS=PASS; facilityMeal=1; fieldMeal=1; "
            + "cleanWater=1; foulWaterRoot=1; foulWaterChild=1; "
            + "rest=1; workProficiency=1; combatProficiency=1; "
            + "saveReloadReverseQuery=4");
    }

    internal static bool VerifyD12SurvivalDetails(bool throwOnFailure = true)
    {
        try
        {
            using (var fieldMeal = new ConsumablesScenario(
                       MigratedProducerOutcomeKind.CharacterConsumablesMealResult,
                       ConsumablesEntry.Meal,
                       MigratedProducerOutcomeFaultMode.None,
                       deliveryFault: false))
            {
                fieldMeal.Execute();
                GameplayOutcomeSnapshot snapshot = Exact(
                    fieldMeal,
                    MigratedProducerOutcomeKind.CharacterConsumablesMealResult);
                RequireMealEvidence(snapshot, facilityExpected: false);
            }

            using (var facilityMeal = new ConsumablesScenario(
                       MigratedProducerOutcomeKind.CharacterConsumablesMealResult,
                       ConsumablesEntry.Meal,
                       MigratedProducerOutcomeFaultMode.None,
                       deliveryFault: false,
                       facilityMeal: true))
            {
                facilityMeal.Execute();
                GameplayOutcomeSnapshot snapshot = Exact(
                    facilityMeal,
                    MigratedProducerOutcomeKind.CharacterConsumablesMealResult);
                RequireMealEvidence(snapshot, facilityExpected: true);
            }

            using (var clean = new WaterConsumedScenario(
                       MigratedProducerOutcomeKind.CharacterWaterConsumedEvent,
                       MigratedProducerOutcomeFaultMode.None,
                       deliveryFault: false))
            {
                clean.Execute();
                GameplayOutcomeSnapshot snapshot = Exact(
                    clean,
                    MigratedProducerOutcomeKind.CharacterWaterConsumedEvent);
                RequireWaterEvidence(snapshot, clean.CurrentThirst, "Clean");
                Require(
                    clean.Probe.CommitCount(
                        MigratedProducerOutcomeKind.CharacterFoulWaterConsequenceResult) == 0,
                    "Clean water created a foul-water consequence child.");
            }

            using (var foul = new BreakdownWaterConsumedScenario(
                       MigratedProducerOutcomeKind.CharacterFoulWaterConsequenceResult,
                       MigratedProducerOutcomeFaultMode.None,
                       deliveryFault: false,
                       foulWater: true))
            {
                foul.Execute();
                GameplayOutcomeSnapshot root = Exact(
                    foul,
                    MigratedProducerOutcomeKind.CharacterWaterConsumedEvent);
                GameplayOutcomeSnapshot child = Exact(
                    foul,
                    MigratedProducerOutcomeKind.CharacterFoulWaterConsequenceResult);
                RequireWaterEvidence(root, foul.CurrentThirst, "Foul");
                Require(
                    string.Equals(child.rootOperationId, root.operationId,
                        StringComparison.Ordinal)
                    && string.Equals(
                        child.causationRelationId,
                        "foul-water-consequence-of-consumption",
                        StringComparison.Ordinal),
                    "Foul-water consequence is not causally rooted under consumption.");
                Require(
                    Metric(child, MigratedProducerOutcomeIds.HealthAfterMetric)
                        == foul.CurrentHealth
                    && Metric(child, MigratedProducerOutcomeIds.HygieneAfterMetric)
                        == foul.CurrentHygiene,
                    "Foul-water consequence metrics do not match the mutated actor.");
                RequireRoundTrip(foul, child);
            }

            using (var rest = new RestOutcomeScenario(
                       MigratedProducerOutcomeKind.RestOutcomeIdentityEvent,
                       MigratedProducerOutcomeFaultMode.None,
                       deliveryFault: false,
                       new[] { "condition:night-watch", "condition:wounded" }))
            {
                rest.Execute();
                GameplayOutcomeSnapshot snapshot = Exact(
                    rest,
                    MigratedProducerOutcomeKind.RestOutcomeIdentityEvent);
                Require(
                    Metric(snapshot, MigratedProducerOutcomeIds.SleepBeforeMetric) == 20d
                    && Metric(snapshot, MigratedProducerOutcomeIds.SleepAfterMetric)
                        == rest.CurrentSleep
                    && string.Equals(
                        Fact(snapshot, MigratedProducerOutcomeIds.ActiveConditionsFact),
                        "condition:night-watch,condition:wounded",
                        StringComparison.Ordinal),
                    "Rest evidence does not match sleep mutation and active conditions.");
                RequireRoundTrip(rest, snapshot);
            }

            foreach (ProficiencyEntry entry in new[]
                     {
                         ProficiencyEntry.ApprovedWork,
                         ProficiencyEntry.Combat
                     })
            {
                using var proficiency = new ProficiencyAwardScenario(
                    MigratedProducerOutcomeKind.CharacterProficiencyAwardCommitReceipt,
                    MigratedProducerOutcomeFaultMode.None,
                    deliveryFault: false,
                    entry);
                proficiency.Execute();
                GameplayOutcomeSnapshot snapshot = Exact(
                    proficiency,
                    MigratedProducerOutcomeKind.CharacterProficiencyAwardCommitReceipt);
                Require(
                    HasParticipant(
                        snapshot,
                        MigratedProducerOutcomeIds.ProficiencyKind,
                        MigratedProducerOutcomeIds.SourceRole)
                    && Metric(
                        snapshot,
                        MigratedProducerOutcomeIds.CurrentExperienceAfterMetric)
                        == proficiency.CurrentMilliExperience
                    && string.Equals(
                        Fact(snapshot, MigratedProducerOutcomeIds.SourceOperationFact),
                        proficiency.SourceOperationId,
                        StringComparison.Ordinal)
                    && string.Equals(
                        snapshot.rootOperationId,
                        proficiency.SourceOperationId,
                        StringComparison.Ordinal),
                    entry + " proficiency evidence is not rooted in its source operation.");
                RequireRoundTrip(proficiency, snapshot);
            }

            return true;
        }
        catch
        {
            if (throwOnFailure)
                throw;
            return false;
        }
    }

    private static GameplayOutcomeSnapshot Exact(
        EntryScenarioBase scenario,
        MigratedProducerOutcomeKind kind)
    {
        GameplayOutcomeReplayIdentity identity =
            MigratedProducerOutcomeE2EAssertions.RequireExact(
                scenario.Outcomes,
                kind,
                scenario.Probe);
        Require(
            scenario.Outcomes.Ledger.TryGetExact(
                identity.OutcomeId,
                out GameplayOutcomeSnapshot snapshot),
            kind + " exact snapshot is missing.");
        return snapshot;
    }

    private static void RequireMealEvidence(
        GameplayOutcomeSnapshot snapshot,
        bool facilityExpected)
    {
        Require(
            HasParticipant(
                snapshot,
                MigratedProducerOutcomeIds.CharacterKind,
                MigratedProducerOutcomeIds.ActorRole)
            && HasParticipant(
                snapshot,
                MigratedProducerOutcomeIds.ItemDefinitionKind,
                MigratedProducerOutcomeIds.ItemRole)
            && HasParticipant(
                snapshot,
                MigratedProducerOutcomeIds.ItemStackKind,
                MigratedProducerOutcomeIds.SourceRole)
            && (!facilityExpected || HasParticipant(
                snapshot,
                MigratedProducerOutcomeIds.FacilityKind,
                MigratedProducerOutcomeIds.FacilityRole)),
            "Meal evidence is missing actor, item, stack, or facility custody.");
        Require(
            Metric(snapshot, MigratedProducerOutcomeIds.QuantityMetric) == 1d
            && Metric(snapshot, MigratedProducerOutcomeIds.InputMassMetric) == 100d
            && Metric(snapshot, MigratedProducerOutcomeIds.HungerBeforeMetric) == 0d
            && Metric(snapshot, MigratedProducerOutcomeIds.HungerAfterMetric) == 30d
            && !string.IsNullOrWhiteSpace(
                Fact(snapshot, MigratedProducerOutcomeIds.PhysicalCommitFact)),
            "Meal quantity, mass, effect delta, or physical commit evidence is incorrect.");
    }

    private static void RequireWaterEvidence(
        GameplayOutcomeSnapshot snapshot,
        float currentThirst,
        string expectedQuality)
    {
        Require(
            HasParticipant(
                snapshot,
                MigratedProducerOutcomeIds.CharacterKind,
                MigratedProducerOutcomeIds.ActorRole)
            && snapshot.participants.Any(value => string.Equals(
                value.roleId,
                MigratedProducerOutcomeIds.SourceRole.Value,
                StringComparison.Ordinal))
            && Metric(snapshot, MigratedProducerOutcomeIds.QuantityMetric) > 0d
            && Metric(snapshot, MigratedProducerOutcomeIds.ThirstBeforeMetric) == 15d
            && Metric(snapshot, MigratedProducerOutcomeIds.ThirstAfterMetric)
                == currentThirst
            && string.Equals(
                Fact(snapshot, MigratedProducerOutcomeIds.WaterQualityFact),
                expectedQuality,
                StringComparison.Ordinal),
            "Water source, quantity, quality, or thirst delta evidence is incorrect.");
    }

    private static bool HasParticipant(
        GameplayOutcomeSnapshot snapshot,
        GameplayEntityKindId kind,
        GameplayRoleId role) => snapshot.participants.Any(value =>
        string.Equals(value.entityKindId, kind.Value, StringComparison.Ordinal)
        && string.Equals(value.roleId, role.Value, StringComparison.Ordinal));

    private static double Metric(
        GameplayOutcomeSnapshot snapshot,
        GameplayMetricId metricId) => snapshot.metrics.Single(value =>
        string.Equals(value.metricId, metricId.Value, StringComparison.Ordinal)).value;

    private static string Fact(
        GameplayOutcomeSnapshot snapshot,
        GameplayOutcomeFactId factId) => snapshot.facts.Single(value =>
        string.Equals(value.factId, factId.Value, StringComparison.Ordinal)).value;

    private static void RequireRoundTrip(
        EntryScenarioBase scenario,
        GameplayOutcomeSnapshot expected)
    {
        GameplayOutcomeLedgerSaveData save =
            scenario.Outcomes.Ledger.CaptureGameplayOutcomes();
        var restored = new GameplayOutcomeLedger(
            scenario.Outcomes.Registry,
            new GameplayOutcomeBufferLimits(
                smallPageCount: 128,
                largePageCount: 8,
                knownResultKeyCapacity: 512,
                retainedSmallPageCount: 128,
                retainedLargePageCount: 8),
            new GameplayOutcomeRunId(save.runId),
            save.worldEpoch);
        GameplayOutcomeLedgerRestoreCandidate candidate =
            restored.PrepareGameplayOutcomeRestore(save);
        restored.BeginRestoreCandidate();
        restored.PublishGameplayOutcomeRestore(candidate);
        restored.PublishRestoreCandidate();
        restored.CompleteRestoreCandidate();
        var id = new GameplayOutcomeId(
            new GameplayOutcomeRunId(expected.runId),
            expected.sequence);
        Require(
            restored.TryGetExact(id, out GameplayOutcomeSnapshot actual)
            && string.Equals(actual.immutablePayloadHash,
                expected.immutablePayloadHash, StringComparison.Ordinal)
            && string.Equals(actual.rootOperationId,
                expected.rootOperationId, StringComparison.Ordinal)
            && actual.facts.Count == expected.facts.Count
            && actual.metrics.Count == expected.metrics.Count,
            "Saved survival evidence did not restore as the same exact outcome.");
    }

    private static void VerifyConsumableMeal(
        ISet<MigratedProducerOutcomeKind> observed)
    {
        VerifyTriplet(
            MigratedProducerOutcomeKind.CharacterConsumablesMealResult,
            (kind, fault, delivery) => new ConsumablesScenario(
                kind,
                ConsumablesEntry.Meal,
                fault,
                delivery));
        observed.Add(MigratedProducerOutcomeKind.CharacterConsumablesMealResult);
    }

    private static void VerifyConsumableSubstance(
        ISet<MigratedProducerOutcomeKind> observed)
    {
        VerifyTriplet(
            MigratedProducerOutcomeKind.CharacterConsumablesSubstanceResult,
            (kind, fault, delivery) => new ConsumablesScenario(
                kind,
                ConsumablesEntry.Substance,
                fault,
                delivery));
        observed.Add(
            MigratedProducerOutcomeKind.CharacterConsumablesSubstanceResult);
    }

    private static void VerifyDetoxTreatment(
        ISet<MigratedProducerOutcomeKind> observed)
    {
        VerifyTriplet(
            MigratedProducerOutcomeKind.CharacterDetoxTreatmentResult,
            (kind, fault, delivery) => new ConsumablesScenario(
                kind,
                ConsumablesEntry.Detox,
                fault,
                delivery));
        observed.Add(MigratedProducerOutcomeKind.CharacterDetoxTreatmentResult);
    }

    private static void VerifyLifeStageChanged(
        ISet<MigratedProducerOutcomeKind> observed)
    {
        VerifyTriplet(
            MigratedProducerOutcomeKind.CharacterLifeStageChangedEvent,
            (kind, fault, delivery) => new LifeStageScenario(
                kind,
                fault,
                delivery));
        observed.Add(MigratedProducerOutcomeKind.CharacterLifeStageChangedEvent);
    }

    public static string VerifyProficiencyObserverFocused()
    {
        var observed = new HashSet<MigratedProducerOutcomeKind>();
        VerifyProficiencyAward(observed);
        Require(observed.Count == 1, "Focused proficiency observer fixture did not execute.");
        const MigratedProducerOutcomeKind kind = MigratedProducerOutcomeKind.CharacterProficiencyAwardCommitReceipt;
        using (var rejected = new ProficiencyAwardScenario(kind,
                   MigratedProducerOutcomeFaultMode.RejectReservation, false))
        {
            string before = rejected.CaptureDomain();
            rejected.ExecuteExpectingRejectedCommit();
            Require(before == rejected.CaptureDomain() && rejected.Probe.CommitAttemptCount(kind) == 0,
                "Rejected observation reservation changed proficiency or reached commit.");
        }
        using (var saved = new ProficiencyAwardScenario(kind, MigratedProducerOutcomeFaultMode.None, false))
        {
            saved.Execute();
            RequireRoundTrip(saved, Exact(saved, kind));
        }
        return "proficiency-domain-observer=PASS\nreserve-reject/domain-rollback/delivery-retry/ledger-save-roundtrip=PASS\n";
    }

    private static void VerifyProficiencyAward(
        ISet<MigratedProducerOutcomeKind> observed)
    {
        VerifyTriplet(
            MigratedProducerOutcomeKind.CharacterProficiencyAwardCommitReceipt,
            (kind, fault, delivery) => new ProficiencyAwardScenario(
                kind,
                fault,
                delivery));
        observed.Add(
            MigratedProducerOutcomeKind.CharacterProficiencyAwardCommitReceipt);
    }

    public static void RunPrimitiveObserversFocusedBatch()
    {
        var observed = new HashSet<MigratedProducerOutcomeKind>();
        VerifyPrimitiveSurvivalCompleted(observed);
        PrimitiveSurvivalCausalProductionEntryDebugScenarios.RunB0RollbackBatch();
        PrimitiveSurvivalCausalProductionEntryDebugScenarios.RunMoodImmunityExperienceBatch();
        Debug.Log("PRIMITIVE_SURVIVAL_OBSERVERS_FOCUSED_PASS; controlled-production-owner; not-natural-AI-or-whole-world-save");
        StaffDutyDebugScenarios.RunWorkControlObserversForBatchMode();
    }

    private static void VerifyPrimitiveSurvivalCompleted(
        ISet<MigratedProducerOutcomeKind> observed)
    {
        VerifyTriplet(
            MigratedProducerOutcomeKind.CharacterPrimitiveSurvivalCompletedEvent,
            (kind, fault, delivery) => new PrimitiveSurvivalScenario(
                kind,
                fault,
                delivery));
        VerifyTriplet(
            MigratedProducerOutcomeKind.CharacterPrimitiveSurvivalCompletedEvent,
            (kind, fault, delivery) => new ConsumablesScenario(
                kind,
                ConsumablesEntry.Meal,
                fault,
                delivery,
                primitiveFieldMeal: true));
        VerifyTriplet(
            MigratedProducerOutcomeKind.CharacterPrimitiveSurvivalCompletedEvent,
            (kind, fault, delivery) => new PrimitiveSurvivalScenario(
                kind,
                fault,
                delivery,
                CharacterPrimitiveSurvivalActionKind.Latrine));
        VerifyTriplet(
            MigratedProducerOutcomeKind.CharacterPrimitiveSurvivalCompletedEvent,
            (kind, fault, delivery) => new PrimitiveSurvivalScenario(
                kind,
                fault,
                delivery,
                CharacterPrimitiveSurvivalActionKind.BucketWash));
        observed.Add(
            MigratedProducerOutcomeKind.CharacterPrimitiveSurvivalCompletedEvent);
    }

    private static void VerifyMealMissed(
        ISet<MigratedProducerOutcomeKind> observed)
    {
        VerifyTriplet(
            MigratedProducerOutcomeKind.MealMissedEvent,
            (kind, fault, delivery) => new MealMissedScenario(
                kind,
                fault,
                delivery));
        observed.Add(MigratedProducerOutcomeKind.MealMissedEvent);
    }

    public static string VerifyWaterFactFocused()
    {
        var observed = new HashSet<MigratedProducerOutcomeKind>();
        VerifyWaterConsumed(observed);
        Require(
            observed.SetEquals(new[]
            {
                MigratedProducerOutcomeKind.CharacterWaterConsumedEvent,
                MigratedProducerOutcomeKind
                    .CharacterFoulWaterConsequenceResult
            }),
            "Focused water fact fixture did not execute its exact outcome scope.");
        return "water-fact=PASS; clean=triplet; "
            + "clean-breakdown=triplet; foul-breakdown=triplet";
    }

    private static void VerifyWaterConsumed(
        ISet<MigratedProducerOutcomeKind> observed)
    {
        VerifyTriplet(
            MigratedProducerOutcomeKind.CharacterWaterConsumedEvent,
            (kind, fault, delivery) => new WaterConsumedScenario(
                kind,
                fault,
                delivery));
        VerifyTriplet(
            MigratedProducerOutcomeKind.CharacterWaterConsumedEvent,
            (kind, fault, delivery) => new BreakdownWaterConsumedScenario(
                kind,
                fault,
                delivery));
        VerifyTriplet(
            MigratedProducerOutcomeKind.CharacterFoulWaterConsequenceResult,
            (kind, fault, delivery) => new BreakdownWaterConsumedScenario(
                kind,
                fault,
                delivery,
                foulWater: true));
        observed.Add(MigratedProducerOutcomeKind.CharacterWaterConsumedEvent);
        observed.Add(
            MigratedProducerOutcomeKind.CharacterFoulWaterConsequenceResult);
    }

    private static void VerifyTabooIncident(
        ISet<MigratedProducerOutcomeKind> observed)
    {
        VerifyTriplet(
            MigratedProducerOutcomeKind.CharacterTabooIncidentEvent,
            (kind, fault, delivery) => new TabooIncidentScenario(
                kind,
                fault,
                delivery));
        observed.Add(MigratedProducerOutcomeKind.CharacterTabooIncidentEvent);
    }

    private static void VerifyObservedFuneral(
        ISet<MigratedProducerOutcomeKind> observed)
    {
        VerifyTriplet(
            MigratedProducerOutcomeKind.ObservedFuneralLifeEventReceipt,
            (kind, fault, delivery) => new ObservedLifeEventScenario(
                kind,
                ObservedLifeEventEntry.Funeral,
                fault,
                delivery));

        using (var many = new ObservedLifeEventScenario(
                   MigratedProducerOutcomeKind.ObservedFuneralLifeEventReceipt,
                   ObservedLifeEventEntry.Funeral,
                   MigratedProducerOutcomeFaultMode.None,
                   deliveryFault: false,
                   funeralParticipantCount: 3))
        {
            many.Execute();
            Require(
                many.VerifyEveryFuneralEntityCanQueryTheOutcome(),
                "Funeral outcome was not queryable from every attendee, the deceased, and the facility.");
        }

        bool rejectedZero = false;
        try
        {
            _ = ObservedLifeEventScenario.CreateFuneralReceipt(0);
        }
        catch (ArgumentException)
        {
            rejectedZero = true;
        }
        Require(
            rejectedZero,
            "A funeral without a living attendee must fail before owner mutation or outcome reservation.");
        observed.Add(
            MigratedProducerOutcomeKind.ObservedFuneralLifeEventReceipt);
    }

    private static void VerifyObservedLastLesson(
        ISet<MigratedProducerOutcomeKind> observed)
    {
        VerifyTriplet(
            MigratedProducerOutcomeKind.ObservedLastLessonLifeEventReceipt,
            (kind, fault, delivery) => new ObservedLifeEventScenario(
                kind,
                ObservedLifeEventEntry.LastLesson,
                fault,
                delivery));
        observed.Add(
            MigratedProducerOutcomeKind.ObservedLastLessonLifeEventReceipt);
    }

    private static void VerifyLineageCompression(
        ISet<MigratedProducerOutcomeKind> observed)
    {
        VerifyTriplet(
            MigratedProducerOutcomeKind
                .ObservedLineageCompressionLifeEventReceipt,
            (kind, fault, delivery) => new LineageCompressionScenario(
                kind,
                fault,
                delivery));
        observed.Add(
            MigratedProducerOutcomeKind
                .ObservedLineageCompressionLifeEventReceipt);
    }

    public static void VerifyProductionLossObserverFocused()
    {
        VerifyObservedProductionLoss(new HashSet<MigratedProducerOutcomeKind>());
        Debug.Log("B125_PRODUCTION_LOSS_PARENT_OBSERVER=PASS");
    }

    private static void VerifyObservedProductionLoss(
        ISet<MigratedProducerOutcomeKind> observed)
    {
        const MigratedProducerOutcomeKind Kind =
            MigratedProducerOutcomeKind.ObservedProductionLossLifeEventReceipt;
        VerifyTriplet(
            Kind,
            (kind, fault, delivery) => new ObservedProductionLossScenario(
                kind,
                fault,
                delivery));

        using (var replay = new ObservedProductionLossScenario(
                   Kind,
                   MigratedProducerOutcomeFaultMode.None,
                   deliveryFault: false))
        {
            replay.Execute();
            string afterFirst = replay.CaptureDomain();
            replay.Execute();
            Require(
                replay.CaptureDomain() == afterFirst
                && replay.Probe.CommitCount(Kind) == 1
                && replay.LastReceipt.ParentPublication.Completed
                && ObservedProductionLossLifeEventReceipt.TryParse(
                    replay.LastReceipt.CanonicalPayload,
                    out ObservedProductionLossLifeEventReceipt restored)
                && restored.ParentPublication.PublicationId == replay.LastReceipt.ParentPublication.PublicationId
                && restored.ParentPublication.Fingerprint == replay.LastReceipt.ParentPublication.Fingerprint,
                "Production-loss observation replay or saved parent identity drifted.");
        }

        using (var missing = new ObservedProductionLossScenario(
                   Kind,
                   MigratedProducerOutcomeFaultMode.None,
                   deliveryFault: false,
                   parentAvailable: false))
        {
            string before = missing.CaptureDomain();
            missing.ExecuteExpectingRejectedCommit();
            Require(
                missing.CaptureDomain() == before
                && missing.Probe.ReservationCount(Kind) == 0,
                "Production-loss observation mutated before its durable parent was resolved.");
        }
        observed.Add(Kind);
    }

    private static void VerifyObservedMealIncident(
        ISet<MigratedProducerOutcomeKind> observed)
    {
        VerifyTriplet(
            MigratedProducerOutcomeKind.ObservedMealIncidentCaptureResult,
            (kind, fault, delivery) => new ObservedMealIncidentScenario(
                kind,
                fault,
                delivery));
        observed.Add(
            MigratedProducerOutcomeKind.ObservedMealIncidentCaptureResult);
    }

    private static void VerifyObservedQuietPromotion(
        ISet<MigratedProducerOutcomeKind> observed)
    {
        VerifyTriplet(
            MigratedProducerOutcomeKind.ObservedQuietPromotionLifeEventReceipt,
            (kind, fault, delivery) => new ObservedLifeEventScenario(
                kind,
                ObservedLifeEventEntry.QuietPromotion,
                fault,
                delivery));
        observed.Add(
            MigratedProducerOutcomeKind.ObservedQuietPromotionLifeEventReceipt);
    }

    private static void VerifyOwnerRunEnded(
        ISet<MigratedProducerOutcomeKind> observed)
    {
        VerifyTriplet(
            MigratedProducerOutcomeKind.OwnerRunEndedEvent,
            (kind, fault, delivery) => new OwnerRunEndedScenario(
                kind,
                fault,
                delivery));
        observed.Add(MigratedProducerOutcomeKind.OwnerRunEndedEvent);
    }

    private static void VerifyRestOutcome(
        ISet<MigratedProducerOutcomeKind> observed)
    {
        VerifyTriplet(
            MigratedProducerOutcomeKind.RestOutcomeIdentityEvent,
            (kind, fault, delivery) => new RestOutcomeScenario(
                kind,
                fault,
                delivery));
        observed.Add(MigratedProducerOutcomeKind.RestOutcomeIdentityEvent);
    }

    private static void VerifyTriplet(
        MigratedProducerOutcomeKind kind,
        Func<MigratedProducerOutcomeKind,
            MigratedProducerOutcomeFaultMode,
            bool,
            IEntryScenario> create)
    {
        using (IEntryScenario scenario = create(
                   kind,
                   MigratedProducerOutcomeFaultMode.None,
                   false))
        {
            string before = scenario.CaptureDomain();
            scenario.Execute();
            string after = scenario.CaptureDomain();
            Require(
                before != after,
                kind + " production entry did not mutate its real owner.");
            _ = MigratedProducerOutcomeE2EAssertions.RequireExact(
                scenario.Outcomes,
                kind,
                scenario.Probe,
                before,
                after);
        }

        using (IEntryScenario scenario = create(
                   kind,
                   MigratedProducerOutcomeFaultMode.RejectCommit,
                   false))
        {
            string domainBefore = scenario.CaptureDomain();
            string irreversibleBefore = scenario.CaptureIrreversibleEffects();
            string outcomeOwnerBefore = JsonUtility.ToJson(
                scenario.Outcomes.Transaction.Capture());
            scenario.ExecuteExpectingRejectedCommit();
            Require(
                scenario.Probe.ReservationCount(kind) > 0
                && scenario.Probe.CommitAttemptCount(kind) > 0
                && scenario.Probe.CommitCount(kind) == 0,
                kind + " rejection case did not reach the production outcome commit.");
            if (scenario.PreservesPostPhysicalRecoveryState)
            {
                string recoveryState = scenario.CaptureDomain();
                string irreversibleApplied = scenario.CaptureIrreversibleEffects();
                Require(
                    domainBefore != recoveryState
                    && irreversibleBefore != irreversibleApplied,
                    kind + " did not retain its post-physical recovery phase and applied effects.");
                scenario.Probe.FaultMode = MigratedProducerOutcomeFaultMode.None;
                scenario.ResumePendingAfterCommitRejection();
                Require(
                    scenario.Probe.CommitCount(kind) == 1,
                    kind + " did not resume its pending parent outcome after rejection.");
                Require(
                    irreversibleApplied == scenario.CaptureIrreversibleEffects(),
                    kind + " reapplied irreversible effects while resuming its pending parent outcome.");
            }
            else
            {
                Require(
                    domainBefore == scenario.CaptureDomain(),
                    kind + " left domain mutation after a definite commit rejection.");
            }
            Require(
                scenario.PreservesPostPhysicalRecoveryState
                    || outcomeOwnerBefore == JsonUtility.ToJson(
                        scenario.Outcomes.Transaction.Capture()),
                kind + " advanced the outcome owner after a definite rejection without a recovery retry.");
        }

        using (IEntryScenario scenario = create(
                   kind,
                   MigratedProducerOutcomeFaultMode.None,
                   true))
        {
            string before = scenario.CaptureDomain();
            scenario.Execute();
            string committed = scenario.CaptureDomain();
            Require(
                before != committed,
                kind + " durable pending commit did not mutate its owner.");
            MigratedProducerOutcomeE2EAssertions.RequirePendingThenRetry(
                scenario.Outcomes,
                kind,
                scenario.Probe,
                scenario.DeliveryFault);
            Require(
                committed == scenario.CaptureDomain(),
                kind + " delivery retry duplicated or reversed domain mutation.");
        }
    }

    private interface IEntryScenario : IDisposable
    {
        MigratedProducerOutcomeEditorFixture Outcomes { get; }
        MigratedProducerOutcomeProbeTransaction Probe { get; }
        MigratedProducerOutcomeDeliveryFault DeliveryFault { get; }
        bool PreservesPostPhysicalRecoveryState { get; }
        string CaptureDomain();
        string CaptureIrreversibleEffects();
        void Execute();
        void ResumePendingAfterCommitRejection();
        void ExecuteExpectingRejectedCommit();
    }

    private abstract class EntryScenarioBase : IEntryScenario
    {
        protected EntryScenarioBase(
            string runPrefix,
            MigratedProducerOutcomeKind target,
            MigratedProducerOutcomeFaultMode fault,
            bool deliveryFault,
            string stableRunId = null)
        {
            DeliveryFault = new MigratedProducerOutcomeDeliveryFault
            {
                TargetKind = target,
                Enabled = deliveryFault
            };
            Outcomes = new MigratedProducerOutcomeEditorFixture(
                string.IsNullOrWhiteSpace(stableRunId)
                    ? runPrefix + Guid.NewGuid().ToString("N")
                    : CausalGameplaySyntax.RequireId(
                        stableRunId,
                        nameof(stableRunId)),
                deliveryFault: DeliveryFault,
                includeProductionCompletion: target == MigratedProducerOutcomeKind.ObservedProductionLossLifeEventReceipt);
            Probe = new MigratedProducerOutcomeProbeTransaction(
                Outcomes.Transaction,
                Outcomes.Recorder)
            {
                TargetKind = target,
                FaultMode = fault
            };
        }

        public MigratedProducerOutcomeEditorFixture Outcomes { get; }
        public MigratedProducerOutcomeProbeTransaction Probe { get; }
        public MigratedProducerOutcomeDeliveryFault DeliveryFault { get; }
        public virtual bool PreservesPostPhysicalRecoveryState => false;
        public abstract string CaptureDomain();
        public virtual string CaptureIrreversibleEffects() => CaptureDomain();
        public abstract void Execute();
        public virtual void ResumePendingAfterCommitRejection() => Execute();

        public virtual void ExecuteExpectingRejectedCommit()
        {
            try
            {
                Execute();
            }
            catch (InvalidOperationException)
            {
            }
        }

        public virtual void Dispose()
        {
        }
    }

    private enum ConsumablesEntry
    {
        Meal,
        Substance,
        Detox
    }

    private enum ProficiencyEntry
    {
        Direct,
        ApprovedWork,
        Combat
    }

    private sealed class ConsumablesScenario : EntryScenarioBase
    {
        private readonly CharacterId actorId;
        private static readonly BuildingInstanceId FacilityId = new(
            "building:migrated-e2e:medical");
        private static readonly ConsumableItemDefinitionId MealId = new(
            "food:migrated-e2e:meal");
        private static readonly ConsumableItemDefinitionId SubstanceId = new(
            "drug:migrated-e2e:medicine");
        private static readonly ConsumableItemDefinitionId DetoxId = new(
            "medicine:migrated-e2e:antidote");
        private static readonly ItemStackId MealStack = new(
            "stack:migrated-e2e:meal");
        private static readonly ItemStackId SubstanceStack = new(
            "stack:migrated-e2e:substance");
        private static readonly ItemStackId DetoxStack = new(
            "stack:migrated-e2e:detox");

        private readonly ConsumablesEntry entry;
        private readonly bool primitiveFieldMeal;
        private readonly bool facilityMeal;
        private readonly ConsumablesPort port;
        private readonly CharacterConsumablesRuntime runtime;
        private readonly CharacterConsumablesPolicyFactFixture policyFacts;
        private readonly DrainingGameClock clock = new();

        internal bool LastMealAccepted { get; private set; }
        internal CharacterConsumablesMealResult LastMealResult
        {
            get;
            private set;
        }

        internal ConsumablesScenario(
            MigratedProducerOutcomeKind target,
            ConsumablesEntry entry,
            MigratedProducerOutcomeFaultMode fault,
            bool deliveryFault,
            bool primitiveFieldMeal = false,
            bool facilityMeal = false,
            string stableRunId = null,
            string actorOwnerId = null)
            : base(
                "run:migrated-entry-consumables-",
                target,
                fault,
                deliveryFault,
                stableRunId)
        {
            this.entry = entry;
            this.primitiveFieldMeal = primitiveFieldMeal;
            this.facilityMeal = facilityMeal;
            actorId = new CharacterId(string.IsNullOrWhiteSpace(actorOwnerId)
                ? "character:migrated-e2e:consumables"
                : actorOwnerId);
            port = new ConsumablesPort(
                actorId,
                FacilityId,
                MealId,
                SubstanceId,
                DetoxId,
                MealStack,
                SubstanceStack,
                DetoxStack,
                facilityMeal);
            policyFacts = new CharacterConsumablesPolicyFactFixture(
                "run:migrated-entry-consumables-policy-"
                + Guid.NewGuid().ToString("N"));
            AttachConsumptionFactObservers(
                policyFacts.Dispatcher,
                Outcomes,
                Probe);
            runtime = new CharacterConsumablesRuntime(
                port,
                port,
                port,
                clock,
                new RandomStreamProvider(104729),
                Outcomes.AggregateRootStore,
                DefaultCharacterNeedBalanceRuntime.Instance,
                domainFacts: policyFacts.Dispatcher,
                calendar: new EditorFixedGameCalendar(42));
            CharacterConsumablesPolicyChangeResult policy = runtime.SetSubstancePolicy(
                actorId,
                "substance:migrated-e2e:medicine",
                SubstancePolicyMode.MedicalOnly,
                0f,
                0);
            Require(policy.Code == CharacterConsumablesPolicyChangeCode.Changed
                    && policyFacts.Ledger.CaptureGameplayOutcomes()
                        .exactOutcomes.Count == 1,
                "Consumables setup policy was not recorded as one typed fact outcome.");
            if (entry == ConsumablesEntry.Detox)
            {
                DungeonCharacterConsumablesSaveData save = runtime.Capture();
                save.toxicityStates.Add(new CharacterToxicityState
                {
                    characterId = actorId.Value,
                    toxicity = 60f,
                    lastNaturalRecoveryDay = 1
                });
                runtime.PublishRestoreCandidate(runtime.BuildRestoreCandidate(save));
            }
        }

        public override string CaptureDomain()
        {
            DungeonCharacterConsumablesSaveData save = runtime.Capture();
            CharacterSubstanceState substance = save.substanceStates
                .FirstOrDefault(value => value != null);
            return string.Join("|", new[]
            {
                save.completedOperations.Count.ToString(CultureInfo.InvariantCulture),
                (substance?.tolerance ?? 0f).ToString("R", CultureInfo.InvariantCulture),
                runtime.GetToxicityStatus(actorId).Toxicity.ToString(
                    "R",
                    CultureInfo.InvariantCulture),
                port.HungerRecovered.ToString("R", CultureInfo.InvariantCulture),
                port.MoodApplied.ToString("R", CultureInfo.InvariantCulture),
                port.DamageApplied.ToString("R", CultureInfo.InvariantCulture),
                port.AcknowledgementCount.ToString(CultureInfo.InvariantCulture)
            });
        }

        public override bool PreservesPostPhysicalRecoveryState =>
            entry is ConsumablesEntry.Meal or ConsumablesEntry.Substance;

        public override string CaptureIrreversibleEffects()
        {
            DungeonCharacterConsumablesSaveData save = runtime.Capture();
            CharacterSubstanceState substance = save.substanceStates
                .FirstOrDefault(value => value != null);
            return string.Join("|", new[]
            {
                (substance?.tolerance ?? 0f).ToString(
                    "R",
                    CultureInfo.InvariantCulture),
                runtime.GetToxicityStatus(actorId).Toxicity.ToString(
                    "R",
                    CultureInfo.InvariantCulture),
                port.HungerRecovered.ToString("R", CultureInfo.InvariantCulture),
                port.MoodApplied.ToString("R", CultureInfo.InvariantCulture),
                port.DamageApplied.ToString("R", CultureInfo.InvariantCulture)
            });
        }

        public override void ResumePendingAfterCommitRejection() =>
            runtime.Tick();

        public override void Dispose() => policyFacts.Dispose();

        private static void AttachConsumptionFactObservers(
            DomainFactDispatcher dispatcher,
            MigratedProducerOutcomeEditorFixture outcomes,
            MigratedProducerOutcomeProbeTransaction probe)
        {
            var commits = new GameplayDomainFactCommitGroup(
                outcomes.Recorder,
                outcomes.Ledger);
            dispatcher.Subscribe(
                "test:migrated:character-consumables-meal",
                new MigratedProducerDomainFactProbeObserver<
                    CharacterMealConsumedFact>(
                    CharacterConsumptionFactMapping.Capture,
                    outcomes.Transaction,
                    commits,
                    probe));
            dispatcher.Subscribe(
                "test:migrated:character-consumables-substance",
                new MigratedProducerDomainFactProbeObserver<
                    CharacterSubstanceConsumedFact>(
                    CharacterConsumptionFactMapping.Capture,
                    outcomes.Transaction,
                    commits,
                    probe));
            dispatcher.Subscribe(
                "test:migrated:character-consumables-detox",
                new MigratedProducerDomainFactProbeObserver<
                    CharacterDetoxAppliedFact>(
                    CharacterConsumptionFactMapping.Capture,
                    outcomes.Transaction,
                    commits,
                    probe));
            dispatcher.Subscribe(
                "test:migrated:character-consumables-primitive-meal",
                new MigratedProducerDomainFactProbeObserver<
                    CharacterPrimitiveMealCompletedFact>(
                    CharacterConsumptionFactMapping.Capture,
                    outcomes.Transaction,
                    commits,
                    probe));
        }

        public override void Execute()
        {
            switch (entry)
            {
                case ConsumablesEntry.Meal:
                    if (facilityMeal)
                    {
                        ConsumableOperationId operationId = new(
                            "consumable-operation:migrated-e2e:facility-meal");
                        LastMealAccepted = runtime.TryConsumeMeal(
                            new ConsumeMealCommand(
                                operationId,
                                actorId,
                                FacilityId,
                                MealStack),
                            out CharacterConsumablesMealResult facilityResult);
                        LastMealResult = facilityResult;
                        clock.Advance();
                        runtime.Tick();
                    }
                    else if (primitiveFieldMeal)
                    {
                        LastMealAccepted = runtime.TryConsumePrimitiveFieldMeal(
                            actorId,
                            MealStack,
                            out CharacterConsumablesMealResult primitiveResult);
                        LastMealResult = primitiveResult;
                    }
                    else
                    {
                        LastMealAccepted = runtime.TryConsumeFieldMeal(
                            actorId,
                            MealStack,
                            out CharacterConsumablesMealResult fieldResult);
                        LastMealResult = fieldResult;
                    }
                    return;
                case ConsumablesEntry.Substance:
                    runtime.TryConsumeSubstance(
                        new ConsumeSubstanceByIdCommand(
                            new ConsumableOperationId(
                                "consumable-operation:migrated-e2e:substance"),
                            actorId,
                            SubstanceId,
                            SubstanceStack,
                            medicalContext: true,
                            combatContext: false),
                        out _);
                    return;
                case ConsumablesEntry.Detox:
                    runtime.TryApplyDetoxTreatment(
                        new ConsumableOperationId(
                            "consumable-operation:migrated-e2e:detox"),
                        actorId,
                        FacilityId,
                        out _);
                    return;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
    }

    private sealed class LifeStageScenario : EntryScenarioBase
    {
        private const string CharacterIdValue =
            "character:migrated-e2e:life-stage";
        private readonly CharacterLifeRuntime life;
        private readonly GameEventBus events;
        private readonly DomainFactDispatcher lifeFacts;
        private readonly CharacterLifeApplicationAdapter adapter;

        internal LifeStageScenario(
            MigratedProducerOutcomeKind target,
            MigratedProducerOutcomeFaultMode fault,
            bool deliveryFault)
            : base("run:migrated-entry-life-stage-", target, fault, deliveryFault)
        {
            life = new CharacterLifeRuntime(
                Outcomes.AggregateRootStore,
                CharacterLifeCatalog.Instance,
                new RandomStreamProvider(7103));
            SpeciesLifeHistoryDefinition history =
                CharacterLifeCatalog.Instance.History;
            life.Register(
                new CharacterId(CharacterIdValue),
                history.SpeciesId,
                chronologicalAgeDays: 100,
                biologicalAgeDayUnits: history.AdultAgeDayUnits - 4d,
                birthdayDayOfYear: 1);
            events = new GameEventBus();
            lifeFacts = CreateCharacterLifeFacts(Outcomes, Probe);
            adapter = new CharacterLifeApplicationAdapter(
                life,
                events,
                NeutralHeritableTraitEffects.Instance,
                lifeFacts,
                lifeFacts);
            adapter.Start();
        }

        public override string CaptureDomain() => JsonUtility.ToJson(life.Capture());
        public override void Execute() =>
            events.Publish(new OperatingDayEndedEvent(1));
        public override void Dispose()
        {
            adapter.Dispose();
            lifeFacts.Dispose();
        }
    }

    private sealed class ProficiencyAwardScenario : EntryScenarioBase
    {
        private readonly CharacterNarrativeRuntime narrative;
        private readonly DomainFactDispatcher proficiencyFacts;
        private readonly CharacterId characterId = new(
            "character:migrated-e2e:proficiency");
        private readonly CharacterProficiencyId proficiencyId;
        private readonly ProficiencyEntry entry;

        internal ProficiencyAwardScenario(
            MigratedProducerOutcomeKind target,
            MigratedProducerOutcomeFaultMode fault,
            bool deliveryFault,
            ProficiencyEntry entry = ProficiencyEntry.Direct)
            : base("run:migrated-entry-proficiency-", target, fault, deliveryFault)
        {
            this.entry = entry;
            var content = new ResourceGameContentCatalog(
                new UnityGameContentRootLoader());
            var catalog = new CharacterNarrativeCatalog(content);
            proficiencyFacts = Outcomes.CreateProficiencyAwardFacts(Probe);
            narrative = new CharacterNarrativeRuntime(
                Outcomes.AggregateRootStore,
                catalog,
                content: content,
                domainFacts: proficiencyFacts);
            proficiencyId = entry == ProficiencyEntry.Combat
                ? BuiltInCharacterProficiencyIds.MeleeCombat
                : BuiltInCharacterProficiencyIds.Fieldwork;
            string speciesId = catalog.Cultures[0].defaultSpeciesId;
            IReadOnlyList<CharacterStartingProficiencyExperience> starts =
                CharacterStartingProficiencyRules.Create(7104);
            CharacterStartingProficiencyExperience awarded = starts.Single(
                value => string.Equals(
                    value.proficiencyId,
                    proficiencyId.Value,
                    StringComparison.Ordinal));
            awarded.experience = 99;
            narrative.Register(
                characterId,
                new CharacterSpeciesId(speciesId),
                Array.Empty<string>(),
                Array.Empty<string>(),
                starts);
        }

        public override string CaptureDomain()
        {
            Require(
                narrative.TryGetProficiency(
                    characterId,
                    proficiencyId,
                    24L,
                    out CharacterProficiencySnapshot snapshot),
                "The proficiency fixture lost its registered proficiency.");
            return snapshot.CurrentMilliExperience.ToString(
                CultureInfo.InvariantCulture);
        }

        internal long CurrentMilliExperience
        {
            get
            {
                Require(
                    narrative.TryGetProficiency(
                        characterId,
                        proficiencyId,
                        24L,
                        out CharacterProficiencySnapshot snapshot),
                    "The proficiency fixture lost its current value.");
                return snapshot.CurrentMilliExperience;
            }
        }

        internal string SourceOperationId => entry switch
        {
            ProficiencyEntry.ApprovedWork =>
                "work-order:migrated-e2e:proficiency",
            ProficiencyEntry.Combat =>
                "battle:migrated-e2e:proficiency",
            _ => "proficiency-award:migrated-e2e:direct"
        };

        public override void Execute()
        {
            switch (entry)
            {
                case ProficiencyEntry.ApprovedWork:
                    narrative.AddApprovedWork(
                        characterId,
                        new ProficiencyWorkProfile(proficiencyId),
                        approvedWork: 20f,
                        difficultyMultiplier: 1f,
                        outcome: ProficiencyWorkOutcome.Success,
                        learningMultiplier: 1f,
                        repetitionMultiplier: 1f,
                        absoluteHour: 24L,
                        sourceOperationId: SourceOperationId);
                    break;
                case ProficiencyEntry.Combat:
                    narrative.AddCombatExperience(
                        characterId,
                        proficiencyId,
                        experience: 1f,
                        training: false,
                        stableAwardKey: SourceOperationId,
                        absoluteHour: 24L);
                    break;
                default:
                    narrative.AddDirectExperience(
                        characterId,
                        proficiencyId,
                        1f,
                        absoluteHour: 24L,
                        applyLearningMultiplier: true);
                    break;
            }
        }

        public override void Dispose() => proficiencyFacts.Dispose();
    }

    private sealed class PrimitiveSurvivalScenario : EntryScenarioBase
    {
        private readonly CharacterActor actor;
        private readonly CharacterSO actorData;
        private readonly CharacterPrimitiveSurvivalRunner runner;
        private readonly WorldItemStackRuntime itemStacks;
        private readonly CharacterPrimitiveSurvivalActionKind actionKind;
        private readonly RecoverableWorldFilthQuery filth = new();
        private readonly GameEventBus events = new();
        private readonly DomainFactDispatcher primitiveFacts;
        private readonly PrimitiveSurvivalActivityObserver activityObserver;

        internal PrimitiveSurvivalScenario(
            MigratedProducerOutcomeKind target,
            MigratedProducerOutcomeFaultMode fault,
            bool deliveryFault,
            CharacterPrimitiveSurvivalActionKind actionKind =
                CharacterPrimitiveSurvivalActionKind.FloorRest)
            : base("run:migrated-entry-primitive-survival-", target, fault, deliveryFault)
        {
            this.actionKind = actionKind;
            var grid = new Grid(3, 1);
            for (int x = 0; x < grid.width; x++)
            {
                grid.SetAreaType(
                    new Vector2Int(x, 0),
                    GridCellAreaType.ExteriorPath);
            }
            var gridProvider = new FixedGridSystemProvider(grid);
            itemStacks =
                PhysicalItemDebugScenarios.CreateRuntimeForCrossDomainFixture(
                    gridProvider,
                    out _,
                    out _,
                    out ItemQuantityReservationService quantityReservations,
                    out IReservedItemTransferService reservedTransfers);
            if (actionKind == CharacterPrimitiveSurvivalActionKind.BucketWash)
            {
                itemStacks.Restore(new DungeonPhysicalItemSaveData
                {
                    version = DungeonPhysicalItemSaveData.CurrentVersion,
                    stacks = new List<WorldItemStackSaveData>
                    {
                        new()
                        {
                            stackId = "stack:migrated-e2e:bucket-water",
                            itemId = PrimitiveSurvivalBalanceAuthority
                                .CleanWaterItemId,
                            quantity = 1,
                            state = WorldItemStackState.Loose,
                            gridX = 0,
                            gridY = 0
                        }
                    }
                });
            }
            var stateStore = new CharacterDeprivationStateStore(
                Outcomes.AggregateRootStore);
            var clock = new DrainingGameClock();
            primitiveFacts = Outcomes.CreatePrimitiveSurvivalFacts(Probe);
            runner = new CharacterPrimitiveSurvivalRunner(
                new CharacterBreakdownWorld(
                    new DungeonStory.Foundation.UnityUiClock(),
                    gridProvider,
                    itemStacks,
                    filth,
                    EmptyWorldWaterQuery.Instance,
                    new RoomLayoutCache(),
                    CharacterAiEditorTestDependencies.WorldRegistry),
                new CharacterEmergencyMovement(gridProvider, stateStore),
                stateStore,
                clock,
                events,
                new CharacterPrimitiveSurvivalDependencies(
                    EmptyFieldMealConsumption.Instance,
                    quantityReservations,
                    reservedTransfers,
                    EditorFixedGameCalendar.Instance,
                    primitiveFacts,
                    new DrainingCoroutineScheduler(clock)));

            GameObject actorObject = new("Migrated Primitive Survival Owner");
            actorObject.SetActive(false);
            actorObject.AddComponent<SpriteRenderer>();
            actorObject.AddComponent<AbilityMove>();
            actorObject.AddComponent<AIBrain>();
            actor = actorObject.AddComponent<CharacterActor>();
            CharacterAiEditorTestDependencies.Inject(actorObject);
            actor.Brain.ConstructCharacterAbility(gridProvider);
            actorData = CharacterAiEditorTestDependencies.CreateCharacterFixtureData(
                CharacterType.NPC,
                "초기 생존 검증 직원",
                "Orc");
            actor.Initialization(actorData);
            actor.Identity.SetPersistentId(
                "character:migrated-e2e:primitive:" + Guid.NewGuid().ToString("N"));
            actor.stats[CharacterCondition.SLEEP] = 20f;
            actor.stats[CharacterCondition.HYGIENE] = 60f;
            actor.stats[CharacterCondition.EXCRETION] = 15f;
            activityObserver = new PrimitiveSurvivalActivityObserver(events);
            activityObserver.Start();
        }

        public override string CaptureDomain() => string.Join("|", new[]
        {
            actor.Stats.GetConditionValue(CharacterCondition.SLEEP, 0f)
                .ToString("R", CultureInfo.InvariantCulture),
            actor.Stats.GetConditionValue(CharacterCondition.HYGIENE, 0f)
                .ToString("R", CultureInfo.InvariantCulture),
            actor.Stats.GetConditionValue(CharacterCondition.EXCRETION, 0f)
                .ToString("R", CultureInfo.InvariantCulture),
            JsonUtility.ToJson(itemStacks.Capture()),
            JsonUtility.ToJson(filth.CaptureFilth())
        });

        public override bool PreservesPostPhysicalRecoveryState =>
            actionKind is CharacterPrimitiveSurvivalActionKind.FloorRest
                or CharacterPrimitiveSurvivalActionKind.Latrine;

        public override void ResumePendingAfterCommitRejection()
        {
            Require(
                runner.TryResumePending(actor, out string status),
                "Primitive-survival pending parent did not resume: " + status);
        }

        public override void Execute()
        {
            Require(
                runner.TryStart(
                    actor,
                    actionKind,
                    out string status),
                "Primitive survival TryStart rejected the branch fixture "
                + actionKind + ": "
                + status);
        }

        public override void Dispose()
        {
            activityObserver?.Dispose();
            primitiveFacts?.Dispose();
            if (actor != null)
                runner.ReleaseActor(CharacterPersistentIdentity.Require(actor));
            if (actorData != null)
                Object.DestroyImmediate(actorData);
            if (actor != null)
                Object.DestroyImmediate(actor.gameObject);
        }
    }

    private sealed class MealMissedScenario : EntryScenarioBase
    {
        private readonly GameEventBus events = new();
        private readonly DomainFactDispatcher mealMissedFacts;
        private readonly SurvivalFoodRuntime runtime;
        private readonly CharacterActor actor;
        private readonly CharacterSO actorData;

        internal MealMissedScenario(
            MigratedProducerOutcomeKind target,
            MigratedProducerOutcomeFaultMode fault,
            bool deliveryFault)
            : base("run:migrated-entry-meal-missed-", target, fault, deliveryFault)
        {
            mealMissedFacts = Outcomes.CreateMealMissedFacts(Probe);
            runtime = SurvivalDebugScenarios.CreateMealMissedOutcomeEntryFixture(
                events,
                Outcomes.AggregateRootStore,
                mealMissedFacts);
            GameObject actorObject = new("Migrated Meal Missed Owner");
            actorObject.AddComponent<SpriteRenderer>();
            actor = actorObject.AddComponent<CharacterActor>();
            CharacterAiEditorTestDependencies.Inject(actorObject);
            actorData = CharacterAiEditorTestDependencies.CreateCharacterFixtureData(
                CharacterType.NPC,
                "결식 검증 사장",
                "Orc",
                CharacterRole.Owner);
            actor.Initialization(actorData);
            actor.Identity.SetPersistentId(
                "character:migrated-e2e:meal-missed");
            CharacterAiEditorTestDependencies.WorldRegistry.RegisterCharacter(actor);
            CharacterAiEditorTestDependencies.WorldRegistry
                .RegisterCharacterLifetime(actor);
            runtime.Initialize();
        }

        public override string CaptureDomain() =>
            JsonUtility.ToJson(runtime.Capture());

        public override void Execute() =>
            runtime.OnTriggerEvent(new OperatingDayStartedEvent(2));

        public override void Dispose()
        {
            runtime.Dispose();
            mealMissedFacts.Dispose();
            CharacterAiEditorTestDependencies.WorldRegistry.UnregisterCharacter(actor);
            CharacterAiEditorTestDependencies.WorldRegistry
                .UnregisterCharacterLifetime(actor);
            if (actorData != null)
                Object.DestroyImmediate(actorData);
            if (actor != null)
                Object.DestroyImmediate(actor.gameObject);
        }
    }

    private sealed class WaterConsumedScenario : EntryScenarioBase
    {
        private readonly CharacterActor actor;
        private readonly CharacterSO actorData;
        private readonly MutableWorldWaterQuery water = new();
        private readonly DomainFactDispatcher waterFacts;
        private readonly CharacterWaterConsumptionCoordinator consumption;

        internal WaterConsumedScenario(
            MigratedProducerOutcomeKind target,
            MigratedProducerOutcomeFaultMode fault,
            bool deliveryFault)
            : base("run:migrated-entry-water-", target, fault, deliveryFault)
        {
            var gridProvider = new EmptyGridSystemProvider();
            WorldItemStackRuntime itemStacks =
                PhysicalItemDebugScenarios.CreateRuntimeForCrossDomainFixture(
                    gridProvider,
                    out _,
                    out _,
                    out _,
                    out IReservedItemTransferService reservedTransfers);
            var commits = new GameplayDomainFactCommitGroup(
                Outcomes.Recorder,
                Outcomes.Ledger);
            waterFacts = new DomainFactDispatcher(
                new IDomainFactObserverBinding[]
                {
                    new CharacterWaterFactBinding(
                        new CharacterWaterFactObserver(
                            Outcomes.Transaction,
                            commits))
                });
            var waterPublisher = new CharacterWaterFactPublisherProbe(
                waterFacts,
                Probe);
            var waterPublications = new CharacterWaterPublicationOutbox(
                new DungeonStory.Foundation.UnityUiClock(),
                new CharacterDeprivationStateStore(Outcomes.AggregateRootStore),
                waterPublisher,
                waterPublisher,
                new GameEventBus());
            consumption = new CharacterWaterConsumptionCoordinator(
                itemStacks,
                reservedTransfers,
                water,
                DefaultCharacterNeedBalanceRuntime.Instance,
                EditorFixedGameCalendar.Instance,
                waterPublications);

            GameObject actorObject = new("Migrated Water Consumption Owner");
            actorObject.AddComponent<SpriteRenderer>();
            actor = actorObject.AddComponent<CharacterActor>();
            CharacterAiEditorTestDependencies.Inject(actorObject);
            actorData = CharacterAiEditorTestDependencies.CreateCharacterFixtureData(
                CharacterType.NPC,
                "식수 검증 직원",
                "Orc");
            actor.Initialization(actorData);
            actor.Identity.SetPersistentId(
                "character:migrated-e2e:water:" + Guid.NewGuid().ToString("N"));
            actor.stats[CharacterCondition.THIRST] = 15f;
        }

        public override string CaptureDomain() => string.Join("|", new[]
        {
            actor.Stats.GetConditionValue(CharacterCondition.THIRST, 0f)
                .ToString("R", CultureInfo.InvariantCulture),
            water.Remaining.ToString("R", CultureInfo.InvariantCulture)
        });

        internal float CurrentThirst => actor.Stats.GetConditionValue(
            CharacterCondition.THIRST,
            0f);

        public override void Execute()
        {
            ExecuteWaterWithExpectedDeliveryFault(
                DeliveryFault,
                () => Require(
                    consumption.TryConsumeWorldSource(actor, water.SourceId),
                    "The production water-consumption coordinator rejected clean water."));
        }

        public override void Dispose()
        {
            waterFacts.Dispose();
            if (actorData != null)
                Object.DestroyImmediate(actorData);
            if (actor != null)
                Object.DestroyImmediate(actor.gameObject);
        }
    }

    private sealed class BreakdownWaterConsumedScenario : EntryScenarioBase
    {
        private readonly CharacterActor actor;
        private readonly CharacterSO actorData;
        private readonly CharacterDeprivationStateStore stateStore;
        private readonly CharacterBreakdownActionRunner runner;
        private readonly DrainingCoroutineScheduler scheduler;
        private readonly MutableWorldWaterQuery water;
        private readonly CharacterActionIntentLease intentLease;
        private readonly DomainFactDispatcher notifications;

        internal BreakdownWaterConsumedScenario(
            MigratedProducerOutcomeKind target,
            MigratedProducerOutcomeFaultMode fault,
            bool deliveryFault,
            bool foulWater = false)
            : base("run:migrated-entry-breakdown-water-", target, fault, deliveryFault)
        {
            water = new MutableWorldWaterQuery(
                foulWater
                    ? WorldWaterQuality.Foul
                    : WorldWaterQuality.Clean);
            var grid = new Grid(1, 1);
            grid.SetAreaType(Vector2Int.zero, GridCellAreaType.ExteriorPath);
            var gridProvider = new FixedGridSystemProvider(grid);
            WorldItemStackRuntime itemStacks =
                PhysicalItemDebugScenarios.CreateRuntimeForCrossDomainFixture(
                    gridProvider,
                    out _,
                    out _,
                    out ItemQuantityReservationService quantityReservations,
                    out _);
            stateStore = new CharacterDeprivationStateStore(
                Outcomes.AggregateRootStore);
            var commits = new GameplayDomainFactCommitGroup(
                Outcomes.Recorder,
                Outcomes.Ledger);
            notifications = new DomainFactDispatcher(
                new IDomainFactObserverBinding[]
                {
                    new CharacterDeprivationExperienceFactBinding(),
                    new CharacterWaterFactBinding(
                        new CharacterWaterFactObserver(
                            Outcomes.Transaction,
                            commits))
                });
            var waterPublisher = new CharacterWaterFactPublisherProbe(
                notifications,
                Probe);
            var events = new GameEventBus();
            var waterPublications = new CharacterWaterPublicationOutbox(
                new DungeonStory.Foundation.UnityUiClock(),
                stateStore,
                waterPublisher,
                waterPublisher,
                events);
            var diagnostics = new CharacterDeprivationDiagnostics();
            var clock = new DrainingGameClock();
            scheduler = new DrainingCoroutineScheduler(clock);
            var world = new CharacterBreakdownWorld(
                new DungeonStory.Foundation.UnityUiClock(),
                gridProvider,
                itemStacks,
                EmptyWorldFilthQuery.Instance,
                water,
                new RoomLayoutCache(),
                CharacterAiEditorTestDependencies.WorldRegistry);
            var safeDrinkPlanner = new CharacterSafeDrinkPlanner(
                gridProvider,
                itemStacks,
                water,
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
                    new RandomStreamProvider(104729).Get(
                        "migrated-entry-breakdown-water"),
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
                    waterPublications));

            GameObject actorObject = new("Migrated Breakdown Water Owner");
            actorObject.SetActive(false);
            actorObject.AddComponent<SpriteRenderer>();
            actorObject.AddComponent<AbilityMove>();
            AIBrain brain = actorObject.AddComponent<AIBrain>();
            brain.availableActions = AiDebugScenarioActionFactory.CreateStaffActions();
            actor = actorObject.AddComponent<CharacterActor>();
            CharacterAiEditorTestDependencies.Inject(actorObject);
            actor.Brain.ConstructCharacterAbility(gridProvider);
            actorData = CharacterAiEditorTestDependencies.CreateCharacterFixtureData(
                CharacterType.NPC,
                "갈증 붕괴 검증 직원",
                "Orc");
            actor.Initialization(actorData);
            actor.Identity.SetPersistentId(
                "character:migrated-e2e:breakdown-water:"
                + Guid.NewGuid().ToString("N"));
            actor.stats[CharacterCondition.THIRST] = 15f;
            actor.stats[CharacterCondition.HYGIENE] = 65f;
            CharacterDeprivationState state = stateStore.Ensure(actor);
            state.breakdownGeneration = 1;
            state.dispatchedBreakdownGeneration = 1;
            state.breakdown = new CharacterBreakdownState
            {
                active = true,
                cause = DeprivationKind.Thirst,
                kind = CharacterBreakdownKind.DesperateDrink,
                startedAt = 0f
            };
            Require(
                actor.Brain.TryBeginExternallyDrivenAction(
                    CharacterBreakdownActionRunner.IntentOwnerId,
                    CharacterActionIntentKind.Breakdown,
                    "결핍 붕괴",
                    "갈증 붕괴",
                    "검증",
                    out intentLease),
                "The breakdown-water fixture could not acquire its action intent.");
        }

        public override string CaptureDomain()
        {
            var deprivation = new DungeonDarkSurvivalSaveData
            {
                characters = stateStore.Capture()
            };
            return string.Join("|", new[]
            {
                actor.Stats.GetConditionValue(CharacterCondition.THIRST, 0f)
                    .ToString("R", CultureInfo.InvariantCulture),
                actor.Stats.GetConditionValue(CharacterCondition.HYGIENE, 0f)
                    .ToString("R", CultureInfo.InvariantCulture),
                water.Remaining.ToString("R", CultureInfo.InvariantCulture),
                JsonUtility.ToJson(deprivation)
            });
        }

        internal float CurrentThirst => actor.Stats.GetConditionValue(
            CharacterCondition.THIRST,
            0f);
        internal float CurrentHealth => actor.CurrentHealth;
        internal float CurrentHygiene => actor.Stats.GetConditionValue(
            CharacterCondition.HYGIENE,
            0f);

        public override void Execute()
        {
            System.Reflection.MethodInfo method =
                typeof(CharacterBreakdownActionRunner).GetMethod(
                    "RunDesperateDrink",
                    System.Reflection.BindingFlags.Instance
                    | System.Reflection.BindingFlags.NonPublic)
                ?? throw new InvalidOperationException(
                    "The production breakdown-water routine is missing.");
            IEnumerator routine = method.Invoke(
                    runner,
                    new object[] { actor, intentLease, true, false })
                as IEnumerator
                ?? throw new InvalidOperationException(
                    "The production breakdown-water routine was not executable.");
            ExecuteWaterWithExpectedDeliveryFault(
                DeliveryFault,
                () => scheduler.Start(actor, routine));
        }

        public override void Dispose()
        {
            runner.ReleaseActor(CharacterPersistentIdentity.Require(actor));
            actor.Brain?.EndExternallyDrivenAction(
                CharacterBreakdownActionRunner.IntentOwnerId,
                clearFailures: true);
            if (actorData != null)
                Object.DestroyImmediate(actorData);
            if (actor != null)
                Object.DestroyImmediate(actor.gameObject);
            notifications.Dispose();
        }
    }

    private sealed class TabooIncidentScenario : EntryScenarioBase
    {
        private readonly WorldItemStackRuntime itemStacks;
        private readonly WildlifeCarcassService carcasses;
        private readonly DomainFactDispatcher physicalFacts;
        private readonly DomainFactDispatcher wildlifeFacts;
        private readonly WildlifeCarcassActivityObserver wildlifeActivities;
        private readonly CharacterActor butcher;
        private readonly CharacterActor source;
        private readonly CharacterSO butcherData;
        private readonly CharacterSO sourceData;
        private readonly GameEventBus events = new();
        internal Dictionary<string, int> Publications { get; } = new()
        { ["progression"] = 0, ["invalidated"] = 0, ["stats"] = 0, ["mood"] = 0, ["taboo"] = 0, ["marker"] = 0 };
        internal bool PrematurePublication { get; private set; }
        internal string ThrowObserverChannel { get; set; }
        internal bool RecoverHygieneOnMoodPublication { get; set; }

        private void Observe(string channel)
        {
            Publications[channel]++;
            PrematurePublication |= Probe.CommitCount(MigratedProducerOutcomeKind.CharacterTabooIncidentEvent) != 1
                || Probe.CommitCount(MigratedProducerOutcomeKind.PhysicalItemTransformReceipt) != 1;
            if (channel == "stats" && RecoverHygieneOnMoodPublication)
            {
                RecoverHygieneOnMoodPublication = false;
                butcher.ChangesStat(CharacterCondition.HYGIENE, 100f);
            }
            if (ThrowObserverChannel == channel)
                throw new InvalidOperationException("injected-taboo-observer-" + channel);
        }

        internal TabooIncidentScenario(
            MigratedProducerOutcomeKind target,
            MigratedProducerOutcomeFaultMode fault,
            bool deliveryFault,
            bool shareTransformOutcomeLedger = true,
            float initialHygiene = 75f,
            int? transformDayOverride = null)
            : base("run:migrated-entry-taboo-", target, fault, deliveryFault)
        {
            itemStacks = PhysicalItemDebugScenarios
                .CreateRuntimeForCrossDomainFixture(
                    out WorldItemRepository repository,
                    out _);
            var spawner = new WorldItemSpawner(
                itemStacks.CatalogProvider,
                repository,
                EditorNullItemMarkerPresenter.Instance);
            var transformOutcomes = new MigratedProducerOutcomeEditorFixture(
                "run:migrated-entry-taboo-physical-"
                + Guid.NewGuid().ToString("N"));
            physicalFacts = shareTransformOutcomeLedger
                ? Outcomes.CreatePhysicalTransformationFacts(Probe)
                : transformOutcomes.CreatePhysicalTransformationFacts();
            wildlifeFacts = shareTransformOutcomeLedger
                ? physicalFacts
                : Outcomes.CreatePhysicalTransformationFacts(Probe);
            var transforms = new PhysicalItemTransformService(
                repository,
                spawner,
                itemStacks.MassQuery,
                itemStacks.CatalogProvider,
                new TabooMarkerPresenter(() =>
                {
                    if (Probe.CommitCount(MigratedProducerOutcomeKind.CharacterTabooIncidentEvent) > 0)
                        Observe("marker");
                }),
                new FixedGameSessionStateProvider(transformDayOverride ?? EditorFixedGameCalendar.Instance.Day),
                physicalFacts, clock: new DungeonStory.Foundation.UnityUiClock());
            carcasses = new WildlifeCarcassService(
                itemStacks,
                transforms,
                EmptyWildlifeSpeciesCatalog.Instance,
                events);
            wildlifeActivities = new WildlifeCarcassActivityObserver(events);
            wildlifeActivities.Start();
            carcasses.ConstructFactPublisher(
                EditorFixedGameCalendar.Instance,
                wildlifeFacts);

            (source, sourceData) = CreateActor(
                "금기 원천",
                "character:migrated-e2e:taboo-source:"
                + Guid.NewGuid().ToString("N"));
            (butcher, butcherData) = CreateActor(
                "금기 도축자",
                "character:migrated-e2e:taboo-butcher:"
                + Guid.NewGuid().ToString("N"));
            butcher.gameObject.name = "Taboo butcher presentation-name regression";
            Require(!string.Equals(butcher.name, butcher.Identity.DisplayName, StringComparison.Ordinal),
                "The taboo display-name regression requires a distinct GameObject name.");
            butcher.stats[CharacterCondition.HYGIENE] = initialHygiene;
            Require(
                itemStacks.SpawnHumanoidCorpse(
                    source,
                    new Vector2Int(4, 5),
                    "migrated-e2e",
                    out string stackId)
                && itemStacks.SetEmergencyButcheryAllowed(stackId, true),
                "The taboo fixture could not create an authorized humanoid corpse.");
        }

        public override string CaptureDomain() =>
            JsonUtility.ToJson(itemStacks.Capture())
            + "|" + butcher.Stats.GetConditionValue(
                CharacterCondition.HYGIENE,
                0f).ToString("R", CultureInfo.InvariantCulture)
            + "|" + Newtonsoft.Json.JsonConvert.SerializeObject(
                butcher.Progression.CapturePersistentState())
            + "|" + JObject.FromObject(butcher.Stats.CaptureMoodDeliveryTransactionState())
            + "|" + Newtonsoft.Json.JsonConvert.SerializeObject(carcasses.CaptureFreshness());

        public override void Execute()
        {
            void Progression() => Observe("progression");
            void Invalidated() => Observe("invalidated");
            void Stats(IReadOnlyDictionary<CharacterCondition, float> _) => Observe("stats");
            void Mood(CharacterMoodSnapshot _) => Observe("mood");
            butcher.Progression.Changed += Progression;
            butcher.Stats.OnStatsInvalidated += Invalidated;
            butcher.Stats.OnStatChange += Stats;
            butcher.Stats.OnMoodChange += Mood;
            using var subscription = events.Subscribe<CharacterTabooIncidentEvent<CharacterActor>>(_ => Observe("taboo"));
            try
            {
                Require(carcasses.TryButcherNextCarcass(butcher.BuildingVisitor, null,
                    out int produced, out string message) && produced == 6,
                    "The production humanoid-butchery entry failed: " + message);
            }
            finally
            {
                butcher.Progression.Changed -= Progression;
                butcher.Stats.OnStatsInvalidated -= Invalidated;
                butcher.Stats.OnStatChange -= Stats;
                butcher.Stats.OnMoodChange -= Mood;
            }
        }

        internal bool VerifyReplayInert()
        {
            GameplayOutcomeReplayIdentity identity = MigratedProducerOutcomeE2EAssertions.RequireExact(
                Outcomes, MigratedProducerOutcomeKind.CharacterTabooIncidentEvent, Probe);
            Require(Outcomes.Ledger.TryGetExact(identity.OutcomeId, out GameplayOutcomeSnapshot snapshot),
                "The taboo participant display-name regression requires an exact outcome.");
            GameplayOutcomeParticipantSnapshot participant = snapshot.participants.Single();
            Require(string.Equals(participant.entityId, CharacterPersistentIdentity.Require(butcher).Value,
                    StringComparison.Ordinal)
                && string.Equals(participant.displayText, butcher.Identity.DisplayName, StringComparison.Ordinal),
                "The taboo participant must retain the canonical character identity display name.");
            CharacterNarrativeFact taboo = butcher.Progression.NarrativeLedger.Facts
                .Single(value => value.factId == "survival/taboo-butchery");
            Require(taboo.count == 1 && taboo.totalValue == 6
                && taboo.lastDay == EditorFixedGameCalendar.Instance.Day,
                "Committed taboo narrative count, quantity or date is wrong.");
            string before = CaptureDomain();
            return !carcasses.TryButcherNextCarcass(butcher.BuildingVisitor, null, out int produced, out _)
                && produced == 0 && before == CaptureDomain();
        }

        internal JObject VerifyCapturedHygieneTransition()
        {
            CharacterNarrativeFact need = butcher.Progression.NarrativeLedger.Facts
                .Single(value => value.factId == "need:hygiene");
            Require(need.count == 2 && need.totalValue == 107f && need.outcome == "critical"
                && need.lastDay == EditorFixedGameCalendar.Instance.Day
                && butcher.Stats.GetConditionValue(CharacterCondition.HYGIENE, 0f) == 100f
                && VerifyReplayInert(), "Reentrant hygiene recovery replaced the committed 25-to-7 transition.");
            return new JObject { ["passed"] = true, ["committedBefore"] = 25, ["committedAfter"] = 7,
                ["laterHygiene"] = 100, ["recordedNeed"] = JObject.FromObject(need) };
        }

        private sealed class TabooMarkerPresenter : IItemMarkerPresenter
        {
            private readonly Action refresh;
            internal TabooMarkerPresenter(Action refresh) => this.refresh = refresh;
            public void Initialize(IWorldItemMarkerDataSource source) { }
            public void RefreshAll(IEnumerable<Vector2Int> positions) { foreach (var position in positions) RefreshAt(position); }
            public void RefreshAt(Vector2Int position) => refresh();
            public bool TryGetMarkerAt(Vector2Int position, out Object marker) { marker = null; return false; }
            public void Clear() { }
        }

        public override void Dispose()
        {
            wildlifeActivities?.Dispose();
            wildlifeFacts?.Dispose();
            if (!ReferenceEquals(physicalFacts, wildlifeFacts))
                physicalFacts?.Dispose();
            DestroyActor(source, sourceData);
            DestroyActor(butcher, butcherData);
        }

        private static (CharacterActor Actor, CharacterSO Data) CreateActor(
            string displayName,
            string persistentId)
        {
            GameObject actorObject = new(displayName);
            actorObject.AddComponent<SpriteRenderer>();
            CharacterActor actor = actorObject.AddComponent<CharacterActor>();
            CharacterAiEditorTestDependencies.Inject(actorObject);
            CharacterSO data = CharacterAiEditorTestDependencies
                .CreateCharacterFixtureData(
                    CharacterType.NPC,
                    displayName,
                    "Orc");
            actor.Initialization(data);
            actor.Identity.SetPersistentId(persistentId);
            return (actor, data);
        }

        private static void DestroyActor(CharacterActor actor, CharacterSO data)
        {
            if (data != null)
                Object.DestroyImmediate(data);
            if (actor != null)
                Object.DestroyImmediate(actor.gameObject);
        }
    }

    private enum ObservedLifeEventEntry
    {
        Funeral,
        LastLesson,
        QuietPromotion
    }

    private sealed class ObservedLifeEventScenario : EntryScenarioBase
    {
        private readonly ObservedLifeEventEntry entry;
        private readonly V20CampaignRuntime campaign;
        private readonly ObservedLifeEventOutcomeCoordinator coordinator;
        private readonly ObservedFuneralLifeEventReceipt funeral;
        private readonly ObservedLastLessonLifeEventReceipt lastLesson;
        private readonly ObservedQuietPromotionLifeEventReceipt quietPromotion;

        internal ObservedLifeEventScenario(
            MigratedProducerOutcomeKind target,
            ObservedLifeEventEntry entry,
            MigratedProducerOutcomeFaultMode fault,
            bool deliveryFault,
            int funeralParticipantCount = 1)
            : base("run:migrated-entry-observed-life-", target, fault,
                deliveryFault)
        {
            this.entry = entry;
            V20StoryContentCatalog catalog = new(
                new ResourceGameContentCatalog(
                    new UnityGameContentRootLoader()));
            campaign = new V20CampaignRuntime(
                Outcomes.AggregateRootStore,
                catalog);
            coordinator = new ObservedLifeEventOutcomeCoordinator(Probe);
            funeral = CreateFuneralReceipt(funeralParticipantCount);
            lastLesson = CreateLastLessonReceipt();
            quietPromotion = CreateQuietPromotionReceipt();
        }

        public override string CaptureDomain() =>
            JsonUtility.ToJson(campaign.CaptureSociety());

        internal ObservedLastLessonLifeEventReceipt LastLessonReceipt =>
            lastLesson;

        internal ObservedQuietPromotionLifeEventReceipt
            QuietPromotionReceipt => quietPromotion;

        internal bool VerifyEveryFuneralEntityCanQueryTheOutcome()
        {
            if (entry != ObservedLifeEventEntry.Funeral)
                return false;
            IEnumerable<GameplayEntityId> ids = new[]
                {
                    new GameplayEntityId(
                        MigratedProducerOutcomeIds.CharacterKind,
                        funeral.DeceasedCharacterId.Value),
                    new GameplayEntityId(
                        MigratedProducerOutcomeIds.FacilityKind,
                        funeral.FacilityInstanceId)
                }
                .Concat(funeral.ParticipantCharacterIds.Select(value =>
                    new GameplayEntityId(
                        MigratedProducerOutcomeIds.CharacterKind,
                        value.Value)));
            return ids.All(id => Outcomes.Ledger.GetForEntity(
                    id,
                    OutcomeCursor.FirstPage(10),
                    OutcomeFilter.All).Items.Count == 1);
        }

        public override void Execute()
        {
            bool accepted;
            bool changed;
            string failureReason;
            switch (entry)
            {
                case ObservedLifeEventEntry.Funeral:
                    accepted = coordinator.TryRecordFuneral(
                        campaign,
                        funeral,
                        out changed,
                        out failureReason);
                    break;
                case ObservedLifeEventEntry.LastLesson:
                    accepted = coordinator.TryRecordLastLesson(
                        campaign,
                        lastLesson,
                        out changed,
                        out failureReason);
                    break;
                case ObservedLifeEventEntry.QuietPromotion:
                    accepted = coordinator.TryRecordQuietPromotion(
                        campaign,
                        quietPromotion,
                        out changed,
                        out failureReason);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            Require(
                accepted && changed,
                "Observed life-event production entry failed: " + failureReason);

            MigratedProducerOutcomeKind kind = entry switch
            {
                ObservedLifeEventEntry.Funeral =>
                    MigratedProducerOutcomeKind.ObservedFuneralLifeEventReceipt,
                ObservedLifeEventEntry.LastLesson =>
                    MigratedProducerOutcomeKind.ObservedLastLessonLifeEventReceipt,
                ObservedLifeEventEntry.QuietPromotion =>
                    MigratedProducerOutcomeKind.ObservedQuietPromotionLifeEventReceipt,
                _ => throw new ArgumentOutOfRangeException()
            };
            GameplayResultKey key = Probe.LatestCommittedKey(kind);
            if (!key.IsValid
                || !Outcomes.Ledger.TryGetResultIdentity(
                    key,
                    out GameplayOutcomeReplayIdentity identity)
                || !Outcomes.Ledger.TryGetExact(
                    identity.OutcomeId,
                    out GameplayOutcomeSnapshot exact))
            {
                return;
            }
            if (entry == ObservedLifeEventEntry.Funeral)
            {
                Require(
                    exact.participants.Count
                        == funeral.ParticipantCharacterIds.Count + 2
                    && exact.participants.Any(value =>
                        value.entityId == funeral.DeceasedCharacterId.Value
                        && value.roleId
                            == MigratedProducerOutcomeIds.TargetRole.Value)
                    && exact.participants.Any(value =>
                        value.entityId == funeral.FacilityInstanceId
                        && value.roleId
                            == MigratedProducerOutcomeIds.FacilityRole.Value)
                    && funeral.ParticipantCharacterIds.All(participant =>
                        exact.participants.Any(value =>
                            value.entityId == participant.Value
                            && value.roleId
                                == MigratedProducerOutcomeIds.WitnessRole.Value))
                    && !exact.participants.Any(value =>
                        value.entityId == funeral.DeceasedCharacterId.Value
                        && value.roleId
                            == MigratedProducerOutcomeIds.ActorRole.Value),
                    "Funeral outcome lost deceased/attendee/facility typed roles.");
            }
            else if (entry == ObservedLifeEventEntry.LastLesson)
            {
                Require(
                    exact.participants.Count == 5
                    && exact.participants.Any(value =>
                        value.entityId == lastLesson.MentorCharacterId.Value
                        && value.roleId
                            == MigratedProducerOutcomeIds.ActorRole.Value)
                    && exact.participants.Any(value =>
                        value.entityId == lastLesson.StudentCharacterId.Value
                        && value.roleId
                            == MigratedProducerOutcomeIds.TargetRole.Value)
                    && exact.participants.Any(value =>
                        value.entityId == lastLesson.AcademyBuildingId.Value)
                    && exact.participants.Any(value =>
                        value.entityId
                            == lastLesson.ProtectiveEquipmentInstanceId)
                    && exact.participants.Any(value =>
                        value.entityId == lastLesson.AwardLedgerStackId)
                    && exact.metrics.Any(value =>
                        value.metricId
                            == MigratedProducerOutcomeIds.PreviousMetric.Value
                        && value.value
                            == lastLesson.AwardBeforeContentRevision)
                    && exact.metrics.Any(value =>
                        value.metricId
                            == MigratedProducerOutcomeIds.CurrentMetric.Value
                        && value.value
                            == lastLesson.AwardAfterContentRevision),
                    "Last-lesson outcome lost mentor/student/academy/PPE/award truth.");
            }
            else if (entry == ObservedLifeEventEntry.QuietPromotion)
            {
                Require(
                    exact.participants.Count == 1
                    && exact.participants[0].entityId
                        == quietPromotion.CharacterId.Value
                    && exact.participants[0].roleId
                        == MigratedProducerOutcomeIds.ActorRole.Value
                    && exact.facts.Any(value =>
                        value.factId
                            == MigratedProducerOutcomeIds.ProficiencyFact.Value
                        && value.value
                            == quietPromotion.ProficiencyId.Value)
                    && exact.metrics.Any(value =>
                        value.metricId == MigratedProducerOutcomeIds
                            .CurrentExperienceBeforeMetric.Value
                        && value.value
                            == quietPromotion.BeforeCurrentMilliExperience)
                    && exact.metrics.Any(value =>
                        value.metricId == MigratedProducerOutcomeIds
                            .CurrentExperienceAfterMetric.Value
                        && value.value
                            == quietPromotion.AfterCurrentMilliExperience)
                    && exact.metrics.Any(value =>
                        value.metricId == MigratedProducerOutcomeIds
                            .LifetimeExperienceBeforeMetric.Value
                        && value.value
                            == quietPromotion.BeforeLifetimeMilliExperience)
                    && exact.metrics.Any(value =>
                        value.metricId == MigratedProducerOutcomeIds
                            .LifetimeExperienceAfterMetric.Value
                        && value.value
                            == quietPromotion.AfterLifetimeMilliExperience)
                    && exact.metrics.Any(value =>
                        value.metricId
                            == MigratedProducerOutcomeIds.RankBeforeMetric.Value
                        && value.value == (int)quietPromotion.BeforeRank)
                    && exact.metrics.Any(value =>
                        value.metricId
                            == MigratedProducerOutcomeIds.RankAfterMetric.Value
                        && value.value == (int)quietPromotion.AfterRank)
                    && exact.metrics.Any(value =>
                        value.metricId == MigratedProducerOutcomeIds
                            .AbsoluteHourMetric.Value
                        && value.value == quietPromotion.AbsoluteHour)
                    && exact.facts.Any(value =>
                        value.factId
                            == MigratedProducerOutcomeIds.AwardKindFact.Value
                        && value.value
                            == ((int)quietPromotion.AwardKind).ToString())
                    && ObservedQuietPromotionLifeEventReceipt.TryParse(
                        quietPromotion.CanonicalPayload,
                        out ObservedQuietPromotionLifeEventReceipt restored)
                    && restored.AfterCurrentMilliExperience
                        == quietPromotion.AfterCurrentMilliExperience
                    && restored.AfterLifetimeMilliExperience
                        == quietPromotion.AfterLifetimeMilliExperience
                    && restored.AfterRank == quietPromotion.AfterRank,
                    "Quiet-promotion outcome lost exact proficiency XP or rank transition.");
            }
        }

        internal static ObservedFuneralLifeEventReceipt CreateFuneralReceipt(
            int participantCount) =>
            new(
                "funeral-operation:migrated-e2e",
                new CharacterId("character:migrated-e2e:deceased"),
                "building:migrated-e2e:memorial",
                absoluteDay: 42,
                generation: 1,
                Enumerable.Range(1, participantCount)
                    .Select(index => new CharacterId(
                        "character:migrated-e2e:mourner-" + index)));

        private static ObservedLastLessonLifeEventReceipt
            CreateLastLessonReceipt()
        {
            CharacterId mentorId = new("character:migrated-e2e:mentor");
            CharacterId studentId = new("character:migrated-e2e:student");
            BuildingInstanceId academyId = new(
                "building:migrated-e2e:academy");
            CharacterProficiencyId proficiencyId = new(
                "proficiency:crafting");
            CareerMentorshipSnapshot mentorship = new(
                mentorId,
                studentId,
                academyId,
                proficiencyId,
                1,
                0,
                0f,
                0f);
            CharacterCareerSnapshot retirement = new(
                mentorId,
                retired: false,
                default,
                string.Empty,
                0,
                0f,
                RetirementScheduleStatus.Pending,
                "life-event:retirement-request",
                "one-more-season",
                40,
                70,
                0);
            CombatEquipmentInstance protectiveEquipment = new()
            {
                instanceId = "equipment:migrated-e2e:last-lesson:blast-coat",
                definitionId = "armor:powder-cuirass",
                ownerCharacterId = mentorId.Value,
                worldState = CombatEquipmentWorldState.Equipped
            };
            const string LedgerStackId =
                "stack:migrated-e2e:last-lesson:career-ledger";
            CareerMentorshipAwardCommitReceipt award =
                CareerMentorshipAwardCommitReceipt.Restore(
                    academyId,
                    LedgerStackId,
                    beforeContentRevision: 4L,
                    afterContentRevision: 5L,
                    "career-mentorship-award:" + LedgerStackId + ":5");
            return new ObservedLastLessonLifeEventReceipt(
                mentorship,
                retirement,
                protectiveEquipment,
                award,
                absoluteDay: 42,
                generation: 1);
        }

        private static ObservedQuietPromotionLifeEventReceipt
            CreateQuietPromotionReceipt()
        {
            const long AbsoluteHour = 41L * GameCalendarRules.HoursPerDay;
            CharacterProficiencyAwardCommitReceipt source = new(
                CharacterProficiencyAwardKind.DirectExperience,
                new CharacterId("character:migrated-e2e:promoted"),
                new CharacterProficiencyId("proficiency:crafting"),
                beforeCurrentMilliExperience: 0L,
                afterCurrentMilliExperience: 100_000L,
                beforeLifetimeMilliExperience: 0L,
                afterLifetimeMilliExperience: 100_000L,
                AbsoluteHour);
            return new ObservedQuietPromotionLifeEventReceipt(
                source,
                absoluteDay: 42,
                generation: 1);
        }
    }

    private sealed class LineageCompressionScenario : EntryScenarioBase
    {
        private readonly KinshipHouseholdRuntime social;
        private readonly V20CampaignRuntime campaign;
        private readonly SocialRitualFactEditorFixture socialFacts;

        internal LineageCompressionScenario(
            MigratedProducerOutcomeKind target,
            MigratedProducerOutcomeFaultMode fault,
            bool deliveryFault)
            : base("run:migrated-entry-lineage-", target, fault, deliveryFault)
        {
            V20StoryContentCatalog catalog = new(
                new ResourceGameContentCatalog(
                    new UnityGameContentRootLoader()));
            campaign = new V20CampaignRuntime(
                Outcomes.AggregateRootStore,
                catalog);
            socialFacts = new SocialRitualFactEditorFixture(Outcomes, Probe);
            social = new KinshipHouseholdRuntime(
                Outcomes.AggregateRootStore,
                campaign,
                new GameEventBus(),
                socialFacts.Journal);
            social.ArchiveDeath(
                new CharacterId("character:migrated-e2e:lineage"),
                new CharacterSpeciesId("species:human"),
                birthAbsoluteDay: 1,
                deathAbsoluteDay: 10,
                famous: false,
                new HouseholdId("household:migrated-e2e:lineage"),
                generation: 2);
        }

        public override string CaptureDomain() =>
            JsonUtility.ToJson(social.Capture())
            + "|" + JsonUtility.ToJson(campaign.CaptureSociety())
            + "|" + JsonUtility.ToJson(campaign.CaptureMilestones());

        public override void Execute()
        {
            social.ArchiveColdData(
                currentAbsoluteDay: 200,
                Array.Empty<CharacterId>());
            GameplayResultKey key = Probe.LatestCommittedKey(
                MigratedProducerOutcomeKind
                    .ObservedLineageCompressionLifeEventReceipt);
            if (!key.IsValid
                || !Outcomes.Ledger.TryGetResultIdentity(
                    key,
                    out GameplayOutcomeReplayIdentity identity)
                || !Outcomes.Ledger.TryGetExact(
                    identity.OutcomeId,
                    out GameplayOutcomeSnapshot exact))
            {
                return;
            }
            Require(
                exact.participants.Count == 2
                && exact.participants.Any(value =>
                    value.entityId == "character:migrated-e2e:lineage"
                    && value.roleId
                        == MigratedProducerOutcomeIds.TargetRole.Value)
                && exact.participants.Any(value =>
                    value.entityId == "household:migrated-e2e:lineage"
                    && value.roleId
                        == MigratedProducerOutcomeIds.OwnerRole.Value)
                && exact.facts.Any(value =>
                    value.factId
                        == MigratedProducerOutcomeIds.HouseholdFact.Value
                    && value.value == "household:migrated-e2e:lineage"),
                "Lineage compression invented or lost its archived target/household.");
        }

        public override void Dispose() => socialFacts.Dispose();
    }

    private sealed class ObservedProductionLossScenario : EntryScenarioBase
    {
        private static readonly ProductionBillId BillId = new(
            "production-bill:wim040:career-loss");
        private static readonly BuildingInstanceId FacilityId = new(
            "building:wim040:career-production");
        private const string RecipeId = "recipe:wim040:career-loss";
        private readonly V20CampaignRuntime campaign;
        private readonly ProductionRecipeExecutionReceiptAuthority authority;
        private readonly ObservedProductionLossCommand command;
        private readonly bool parentAvailable;
        private readonly DomainFactDispatcher completionFacts = new();

        internal ObservedProductionLossScenario(
            MigratedProducerOutcomeKind target,
            MigratedProducerOutcomeFaultMode fault,
            bool deliveryFault,
            bool parentAvailable = true)
            : base(
                "run:migrated-entry-production-loss-",
                target,
                fault,
                deliveryFault)
        {
            this.parentAvailable = parentAvailable;
            V20StoryContentCatalog catalog = new(
                new ResourceGameContentCatalog(
                    new UnityGameContentRootLoader()));
            campaign = new V20CampaignRuntime(
                Outcomes.AggregateRootStore,
                catalog);
            CharacterId mentorId = new(
                "character:migrated-e2e:production-loss:mentor");
            CharacterId studentId = new(
                "character:migrated-e2e:production-loss:student");
            command = new ObservedProductionLossCommand(
                campaign,
                Probe,
                new CareerMentorshipSnapshot(
                    mentorId,
                    studentId,
                    new BuildingInstanceId(
                        "building:migrated-e2e:production-loss:academy"),
                    new CharacterProficiencyId("proficiency:crafting"),
                    1,
                    0,
                    0f,
                    0f));

            completionFacts.Subscribe("production.completion-observer",
                new GameplayDomainFactObserver<ProductionCycleCompletedFact, ProductionCompletedOutcomeReceipt>(
                    ProductionCompletionFactObservation.Capture, Outcomes.Registry, Outcomes.Ledger, Outcomes.Recorder,
                    new GameplayDomainFactCommitGroup(Outcomes.Recorder, Outcomes.Ledger)));
            var productionState = new ProductionAggregateStateStore(Outcomes.AggregateRootStore);
            var owner = ProductionBillRecord.Create(BillId, RecipeId, FacilityId, ProductionOrderMode.RepeatCount,
                1, 0, ProductionBatchStage.None, "production-material:" + BillId.Value);
            new ProductionAggregateStateSession(Outcomes.AggregateRootStore).AddBill(owner);
            if (parentAvailable)
            {
                var fact = Wim040ObservedLifeEventReceiptDebugFixture.CreateCompletedLossFact(studentId.Value);
                var observation = new ProductionCompletionObservationRuntime(completionFacts, completionFacts);
                Require(observation.TryPrepare(owner, () => fact, out var stage, out string failure),
                    "Production parent observation prepare failed: " + failure);
                Require(observation.Commit(owner, stage).Completed, "Production parent observation commit failed.");
                stage.Cancel();
            }
            authority = new ProductionRecipeExecutionReceiptAuthority(command, completionFacts, completionFacts, productionState);
        }

        internal ObservedProductionLossLifeEventReceipt LastReceipt =>
            command.LastReceipt;

        internal SocietyEventWorldSaveData CaptureSocietyForEvidence() =>
            campaign.CaptureSociety();

        internal SocietyEventWorldSaveData RoundTripSocietyForEvidence()
        {
            SocietyEventWorldSaveData restored =
                JsonUtility.FromJson<SocietyEventWorldSaveData>(
                    JsonUtility.ToJson(campaign.CaptureSociety()));
            campaign.PublishSociety(campaign.PrepareSociety(restored));
            return campaign.CaptureSociety();
        }

        public override string CaptureDomain() =>
            JsonUtility.ToJson(campaign.CaptureSociety());

        public override void Execute()
        {
            ProductionPreparedOutputBatchSaveData completed =
                Wim040ObservedLifeEventReceiptDebugFixture
                    .CreateCompletedLossBatch();
            bool accepted = authority.TryPublishCompleted(
                BillId,
                1,
                RecipeId,
                FacilityId,
                "character:migrated-e2e:production-loss:student",
                string.Empty,
                0,
                0L,
                completed,
                out string failureReason);
            Require(
                accepted,
                (parentAvailable
                    ? "Production-loss production entry failed: "
                    : "Production-loss missing parent was not rejected: ")
                + failureReason);
            GameplayResultKey key = Probe.LatestCommittedKey(
                MigratedProducerOutcomeKind
                    .ObservedProductionLossLifeEventReceipt);
            if (!key.IsValid
                || !Outcomes.Ledger.TryGetResultIdentity(
                    key,
                    out GameplayOutcomeReplayIdentity identity)
                || !Outcomes.Ledger.TryGetExact(
                    identity.OutcomeId,
                    out GameplayOutcomeSnapshot exact))
            {
                return;
            }
            Require(
                new GameplayOutcomeId(new GameplayOutcomeRunId(exact.parentRunId), exact.parentSequence).ToString()
                    == LastReceipt.ParentPublication.PublicationId
                && exact.causationRelationId
                    == "production-declared-loss-observation"
                && exact.participants.Any(value =>
                    value.entityId == LastReceipt.StudentCharacterId.Value
                    && value.roleId
                        == MigratedProducerOutcomeIds.ActorRole.Value)
                && exact.participants.Any(value =>
                    value.entityId == LastReceipt.MentorCharacterId.Value
                    && value.roleId
                        == MigratedProducerOutcomeIds.WitnessRole.Value)
                && exact.participants.Any(value =>
                    value.entityId == LastReceipt.FacilityId.Value)
                && exact.participants.Any(value =>
                    value.entityId == LastReceipt.AcademyBuildingId.Value)
                && exact.metrics.Any(value =>
                    value.metricId == MigratedProducerOutcomeIds.MassMetric.Value
                    && value.value == LastReceipt.DeclaredLossMassGrams)
                && exact.facts.Any(value =>
                    value.factId == MigratedProducerOutcomeIds.BillFact.Value
                    && value.value == LastReceipt.BillId.Value)
                && exact.facts.Any(value =>
                    value.factId == MigratedProducerOutcomeIds.BatchFact.Value
                    && value.value == LastReceipt.BatchCommitId),
                "Production-loss child lost exact parent or typed source truth.");
        }
        public override void Dispose() => completionFacts.Dispose();
    }

    private sealed class ObservedProductionLossCommand :
        IObservedCareerLifeEventCommand
    {
        private readonly V20CampaignRuntime campaign;
        private readonly ObservedLifeEventOutcomeCoordinator coordinator;
        private readonly CareerMentorshipSnapshot mentorship;

        internal ObservedProductionLossCommand(
            V20CampaignRuntime campaign,
            IMigratedProducerOutcomeTransaction transactions,
            CareerMentorshipSnapshot mentorship)
        {
            this.campaign = campaign;
            coordinator = new ObservedLifeEventOutcomeCoordinator(transactions);
            this.mentorship = mentorship;
        }

        internal ObservedProductionLossLifeEventReceipt LastReceipt
            { get; private set; }

        public bool TryCaptureProductionDeclaredLoss(
            ProductionDeclaredLossCycleReceipt source,
            out bool stateChanged,
            out string failureReason)
        {
            LastReceipt = new ObservedProductionLossLifeEventReceipt(
                source,
                mentorship,
                absoluteDay: 42,
                generation: 1);
            return coordinator.TryRecordProductionLoss(
                campaign,
                LastReceipt,
                out stateChanged,
                out failureReason);
        }

        public bool TryCaptureLastLesson(
            CareerMentorshipSnapshot sourceMentorship,
            CharacterCareerSnapshot retirement,
            CombatEquipmentInstance protectiveEquipment,
            CareerMentorshipAwardCommitReceipt award,
            int absoluteDay,
            out bool stateChanged,
            out string failureReason)
        {
            stateChanged = false;
            failureReason = "unexpected-last-lesson";
            return false;
        }

        public bool TryCaptureQuietPromotion(
            CharacterProficiencyAwardCommitReceipt source,
            out bool stateChanged,
            out string failureReason)
        {
            stateChanged = false;
            failureReason = "unexpected-quiet-promotion";
            return false;
        }
    }

    private sealed class ObservedMealIncidentScenario : EntryScenarioBase
    {
        private readonly SocialRitualFactEditorFixture socialFacts;
        private readonly V20CampaignRuntime campaign;
        private readonly ObservedMealIncidentSnapshot evidence;
        private readonly V20DailyEventContext context;

        internal ObservedMealIncidentScenario(
            MigratedProducerOutcomeKind target,
            MigratedProducerOutcomeFaultMode fault,
            bool deliveryFault)
            : base("run:migrated-entry-observed-meal-", target, fault, deliveryFault)
        {
            V20StoryContentCatalog catalog = new(
                new ResourceGameContentCatalog(
                    new UnityGameContentRootLoader()));
            socialFacts = new SocialRitualFactEditorFixture(Outcomes, Probe);
            campaign = new V20CampaignRuntime(
                Outcomes.AggregateRootStore,
                catalog,
                null,
                null,
                socialFacts.Journal);
            evidence = new ObservedMealIncidentSnapshot(
                new ConsumableOperationId(
                    "consumable-operation:migrated-e2e:observed-meal"),
                new CharacterId("character:migrated-e2e:customer"),
                new ItemDefinitionId("food:lavish-meat"),
                new ItemStackId("stack:migrated-e2e:observed-meal"),
                default,
                fieldMeal: true,
                new CoreGridCell(3, 4),
                absoluteDay: 42,
                observedPolicyViolation: false,
                observedDowned: false,
                observedDead: false,
                observedContaminated: true);
            context = new V20DailyEventContext
            {
                AbsoluteDay = 42,
                RunSeed = 157181,
                Season = Season.Spring
            };
        }

        public override string CaptureDomain() =>
            JsonUtility.ToJson(campaign.CaptureSociety());

        public override void Execute() =>
            campaign.CaptureObservedMealIncident(evidence, context);

        public override void Dispose() => socialFacts.Dispose();
    }

    private sealed class OwnerRunEndedScenario : EntryScenarioBase
    {
        private readonly OwnerRunManager manager;
        private readonly OwnerRunCompletionFactEditorFixture ownerFacts;

        internal OwnerRunEndedScenario(
            MigratedProducerOutcomeKind target,
            MigratedProducerOutcomeFaultMode fault,
            bool deliveryFault)
            : base("run:migrated-entry-owner-run-", target, fault, deliveryFault)
        {
            CoreSessionRulesSO rulesAsset =
                AssetDatabase.LoadAssetAtPath<CoreSessionRulesSO>(
                    "Assets/Resources/SO/Content/CoreSessionRules.asset")
                ?? throw new InvalidOperationException(
                    "Authored core-session rules are missing.");
            manager = new GameObject("Migrated Owner Run Manager")
                .AddComponent<OwnerRunManager>();
            ownerFacts = new OwnerRunCompletionFactEditorFixture(
                Outcomes,
                Probe);
            CharacterAiEditorTestDependencies
                .InjectOwnerRunManagerCompletionFactsForDiagnostics(
                    manager,
                    ownerFacts.Publisher,
                    new FixedRunSeedProvider(104729),
                    Outcomes.AggregateRootStore,
                    new FixedCoreSessionRulesProvider(
                        rulesAsset.CreateRuntimeDefinition()),
                    ownerFacts.Recovery,
                    EmptyRunCompletionPreparation.Instance);
            CharacterSO owner = manager.OwnerCandidates.FirstOrDefault()
                ?? throw new InvalidOperationException(
                    "No authored owner candidate is available.");
            manager.SelectOwner(owner, "결말 검증 사장");
        }

        public override string CaptureDomain() => manager.IsRunEnded.ToString();

        public override void Execute() => manager.CompleteRun(
            DungeonRunOutcome.Victory,
            "migrated-producer-e2e");

        public override void Dispose()
        {
            ownerFacts.Dispose();
            if (manager?.CurrentOwnerActor != null)
                Object.DestroyImmediate(manager.CurrentOwnerActor.gameObject);
            if (manager != null)
                Object.DestroyImmediate(manager.gameObject);
        }
    }

    private sealed class RestOutcomeScenario : EntryScenarioBase
    {
        private readonly CharacterActor actor;
        private readonly CharacterSO actorData;
        private readonly DomainFactDispatcher restFacts;
        private readonly IReadOnlyList<string> activeConditionIds;

        internal RestOutcomeScenario(
            MigratedProducerOutcomeKind target,
            MigratedProducerOutcomeFaultMode fault,
            bool deliveryFault,
            IReadOnlyList<string> activeConditionIds = null)
            : base("run:migrated-entry-rest-", target, fault, deliveryFault)
        {
            this.activeConditionIds = activeConditionIds ?? Array.Empty<string>();
            restFacts = Outcomes.CreateCharacterRestFacts(Probe);
            GameObject actorObject = new("Migrated Rest Owner");
            actorObject.AddComponent<SpriteRenderer>();
            actor = actorObject.AddComponent<CharacterActor>();
            CharacterAiEditorTestDependencies.Inject(actorObject);
            actorData = CharacterAiEditorTestDependencies.CreateCharacterFixtureData(
                CharacterType.NPC,
                "휴식 검증 직원",
                "Orc");
            actor.Initialization(actorData);
            actor.Identity.SetPersistentId("character:migrated-e2e:rest");
            CharacterAiEditorTestDependencies
                .InjectCharacterStatsOutcomeForDiagnostics(
                    actor.Stats,
                    restFacts,
                    restFacts);
            actor.stats[CharacterCondition.SLEEP] = 20f;
        }

        public override string CaptureDomain() => actor.Stats
            .GetConditionValue(CharacterCondition.SLEEP, 0f)
            .ToString("R", CultureInfo.InvariantCulture);

        internal float CurrentSleep => actor.Stats.GetConditionValue(
            CharacterCondition.SLEEP,
            0f);

        public override void Execute() => actor.Stats.RecoverNeed(
            CharacterCondition.SLEEP,
            10f,
            CharacterNeedRecoverySource.Rest,
            activeConditionIds);

        public override void Dispose()
        {
            restFacts?.Dispose();
            if (actorData != null)
                Object.DestroyImmediate(actorData);
            if (actor != null)
                Object.DestroyImmediate(actor.gameObject);
        }
    }

    [Serializable]
    internal sealed class ConsumablesPortCausalState
    {
        public const int CurrentVersion = 1;

        public int version = CurrentVersion;
        public string actorId = string.Empty;
        public float hungerRecovered;
        public float moodApplied;
        public float damageApplied;
        public int acknowledgementCount;
        public int pendingCommitCount;
    }

    internal sealed class ConsumablesPort :
        ICharacterConsumablesWorldPort,
        ICharacterConsumablesInventoryPort,
        ICharacterConsumablesEventPort
    {
        private readonly CharacterId actorId;
        private readonly BuildingInstanceId facilityId;
        private readonly CharacterConsumablesMealDefinitionSnapshot meal;
        private readonly CharacterConsumablesSubstanceDefinitionSnapshot substance;
        private readonly CharacterDetoxMedicineDefinitionSnapshot detox;
        private readonly List<CharacterConsumablesStackSnapshot> stacks;
        private readonly Dictionary<string, string> pendingCommitByOperation = new(
            StringComparer.Ordinal);
        private readonly bool facilityMeal;

        internal ConsumablesPort(
            CharacterId actorId,
            BuildingInstanceId facilityId,
            ConsumableItemDefinitionId mealId,
            ConsumableItemDefinitionId substanceId,
            ConsumableItemDefinitionId detoxId,
            ItemStackId mealStack,
            ItemStackId substanceStack,
            ItemStackId detoxStack,
            bool facilityMeal = false)
        {
            this.facilityMeal = facilityMeal;
            this.actorId = actorId;
            this.facilityId = facilityId;
            meal = new CharacterConsumablesMealDefinitionSnapshot(
                mealId,
                "검증 식사",
                MealDietClass.Vegan,
                MealQualityTier.Simple,
                nutrition: 30f,
                mood: 3f,
                unitPrice: 1,
                forbiddenIngredient: false,
                sweet: false,
                salted: false);
            substance = new CharacterConsumablesSubstanceDefinitionSnapshot(
                substanceId,
                new SubstanceDefinitionView(
                    "substance:migrated-e2e:medicine",
                    substanceId.Value,
                    "검증 약물",
                    SubstanceUseClass.Medicine,
                    addictionChance: 0f,
                    overdoseChance: 0f,
                    toleranceGain: 1f,
                    withdrawalPerHour: 0f,
                    moodEffect: 1f,
                    workSpeedEffect: 0f,
                    combatEffect: 0f,
                    fatigueAccumulationReduction: 0f,
                    researchSpeedEffect: 0f,
                    suppressesPerceivedPain: false,
                    durationSeconds: 60f,
                    requiredResearchId: string.Empty));
            detox = new CharacterDetoxMedicineDefinitionSnapshot(
                detoxId,
                "검증 해독제",
                detoxReduction: 30f);
            stacks = new List<CharacterConsumablesStackSnapshot>
            {
                new(
                    mealStack,
                    mealId,
                    1,
                    facilityMeal
                        ? CharacterConsumablesStackState.FacilityBuffer
                        : CharacterConsumablesStackState.Loose,
                    facilityMeal
                        ? CharacterConsumablesRuntime.GetMealDestinationId(
                            facilityId,
                            mealId)
                        : string.Empty,
                    forbidden: false,
                    reservedQuantity: 0,
                    contamination: 0f,
                    freshness01: 1f,
                    remainingFreshnessSeconds: 100f,
                    preserved: false),
                new(
                    substanceStack,
                    substanceId,
                    1,
                    CharacterConsumablesStackState.Loose,
                    string.Empty,
                    forbidden: false,
                    reservedQuantity: 0,
                    contamination: 0f,
                    freshness01: 1f,
                    remainingFreshnessSeconds: 100f,
                    preserved: false),
                new(
                    detoxStack,
                    detoxId,
                    1,
                    CharacterConsumablesStackState.FacilityBuffer,
                    CharacterConsumablesInputDestinationIdentity.Build(
                        CharacterConsumablesInputKind.MedicalTreatment,
                        facilityId,
                        detoxId),
                    forbidden: false,
                    reservedQuantity: 0,
                    contamination: 0f,
                    freshness01: 1f,
                    remainingFreshnessSeconds: 100f,
                    preserved: false)
            };
        }

        internal float HungerRecovered { get; private set; }
        internal float MoodApplied { get; private set; }
        internal float DamageApplied { get; private set; }
        internal int AcknowledgementCount { get; private set; }
        internal int PendingCommitCount => pendingCommitByOperation.Count;

        internal ConsumablesPortCausalState CaptureCausalState() => new()
        {
            actorId = actorId.Value,
            hungerRecovered = HungerRecovered,
            moodApplied = MoodApplied,
            damageApplied = DamageApplied,
            acknowledgementCount = AcknowledgementCount,
            pendingCommitCount = pendingCommitByOperation.Count
        };

        internal void RestoreCausalState(ConsumablesPortCausalState state)
        {
            if (state == null
                || state.version != ConsumablesPortCausalState.CurrentVersion
                || !string.Equals(state.actorId, actorId.Value,
                    StringComparison.Ordinal)
                || state.hungerRecovered < 0f
                || float.IsNaN(state.hungerRecovered)
                || float.IsInfinity(state.hungerRecovered)
                || float.IsNaN(state.moodApplied)
                || float.IsInfinity(state.moodApplied)
                || state.damageApplied < 0f
                || float.IsNaN(state.damageApplied)
                || float.IsInfinity(state.damageApplied)
                || state.acknowledgementCount < 0
                || state.pendingCommitCount != 0)
            {
                throw new InvalidOperationException(
                    "The consumables causal port checkpoint is invalid.");
            }

            pendingCommitByOperation.Clear();
            HungerRecovered = state.hungerRecovered;
            MoodApplied = state.moodApplied;
            DamageApplied = state.damageApplied;
            AcknowledgementCount = state.acknowledgementCount;
        }

        public IReadOnlyList<CharacterId> CharacterIds => new[] { actorId };
        public IReadOnlyList<BuildingInstanceId> FacilityIds =>
            new[] { facilityId };

        public bool TryGetActor(
            CharacterId id,
            out CharacterConsumablesActorSnapshot actor)
        {
            actor = id.Equals(actorId)
                ? new CharacterConsumablesActorSnapshot(
                    actorId,
                    active: true,
                    health: 100f - DamageApplied,
                    maxHealth: 100f,
                    mood: 50f + MoodApplied,
                    hunger: HungerRecovered,
                    combatStance: false)
                : default;
            return actor.Id.IsValid;
        }

        public bool TryGetFacility(
            BuildingInstanceId id,
            out CharacterConsumablesFacilitySnapshot facility)
        {
            facility = id.Equals(facilityId)
                ? new CharacterConsumablesFacilitySnapshot(
                    facilityId,
                    mealFacility: facilityMeal,
                    recreationalSubstanceFacility: false,
                    Vector2Int.zero)
                : default;
            return facility.Id.IsValid;
        }

        public CharacterCultureMealPreference GetCultureMealPreference(
            CharacterId characterId,
            ConsumableItemDefinitionId itemId) =>
            CharacterCultureMealPreference.Neutral;

        public float ProjectGameplayEffect(
            CharacterId characterId,
            string targetId,
            float baseValue) => baseValue;

        public float GetBehaviorUtilityMultiplier(
            CharacterId characterId,
            IReadOnlyCollection<string> semanticTags) => 1f;

        public void RecoverHunger(CharacterId id, float amount) =>
            HungerRecovered += amount;

        public void ApplyMood(
            CharacterId id,
            string sourceId,
            string label,
            float value,
            float durationSeconds) => MoodApplied += value;

        public void ApplyDamage(CharacterId id, float amount, string reason) =>
            DamageApplied += amount;

        public void RecordNeedNarrative(
            CharacterId id,
            string factId,
            string subjectId,
            string outcome,
            float value)
        {
        }

        public IReadOnlyList<CharacterConsumablesStackSnapshot> GetAllStacks() =>
            stacks;

        public IReadOnlyList<CharacterConsumablesSubstanceDefinitionSnapshot>
            GetSubstances() => new[] { substance };

        public IReadOnlyList<CharacterDetoxMedicineDefinitionSnapshot>
            GetDetoxMedicines() => new[] { detox };

        public bool TryGetMeal(
            ConsumableItemDefinitionId id,
            out CharacterConsumablesMealDefinitionSnapshot value)
        {
            value = id.Equals(meal.Id) ? meal : default;
            return value.Id.IsValid;
        }

        public bool TryResolveSubstance(
            string substanceOrItemId,
            out CharacterConsumablesSubstanceDefinitionSnapshot value)
        {
            value = string.Equals(
                    substanceOrItemId,
                    substance.Id.Value,
                    StringComparison.Ordinal)
                || string.Equals(
                    substanceOrItemId,
                    substance.Definition.SubstanceId,
                    StringComparison.Ordinal)
                    ? substance
                    : default;
            return value.Id.IsValid;
        }

        public bool TryResolveSubstance(
            ConsumableItemDefinitionId id,
            out CharacterConsumablesSubstanceDefinitionSnapshot value)
        {
            value = id.Equals(substance.Id) ? substance : default;
            return value.Id.IsValid;
        }

        public bool TryResolveDetoxMedicine(
            ConsumableItemDefinitionId id,
            out CharacterDetoxMedicineDefinitionSnapshot medicine)
        {
            medicine = id.Equals(detox.Id) ? detox : default;
            return medicine.Id.IsValid;
        }

        public bool TryConsume(ItemStackId stackId, int quantity) => true;

        public bool TryReserveMealQuantity(
            ConsumableOperationId operationId,
            CharacterId characterId,
            BuildingInstanceId targetFacilityId,
            ItemStackId stackId,
            out string leaseId)
        {
            leaseId = "lease:" + operationId.Value;
            return true;
        }

        public bool RevalidateMealQuantity(string leaseId, ItemStackId stackId) =>
            !string.IsNullOrWhiteSpace(leaseId);

        public bool TryCommitReservedMealQuantityPending(
            ConsumableOperationId operationId,
            string leaseId,
            int quantity,
            out CharacterMealPhysicalCommitSnapshot commit,
            out string failureReason)
        {
            string commitId = "commit:" + operationId.Value;
            pendingCommitByOperation[operationId.Value] = commitId;
            commit = new CharacterMealPhysicalCommitSnapshot(
                operationId.Value,
                CharacterConsumablesRuntime.MealPhysicalSinkReason,
                commitId,
                new[] { stacks[0].StackId.Value },
                quantity,
                inputMassGrams: 100L);
            failureReason = string.Empty;
            return true;
        }

        public bool TryAcknowledgeMealConsumption(
            string commitId,
            out string failureReason) => Acknowledge(commitId, out failureReason);

        public bool TryCommitSubstanceConsumptionPending(
            ConsumableOperationId operationId,
            CharacterId characterId,
            ItemStackId stackId,
            out CharacterSubstancePhysicalCommitSnapshot commit,
            out string failureReason)
        {
            string commitId = "commit:" + operationId.Value;
            pendingCommitByOperation[operationId.Value] = commitId;
            commit = new CharacterSubstancePhysicalCommitSnapshot(
                operationId.Value,
                CharacterConsumablesRuntime.SubstancePhysicalSinkReason,
                commitId,
                new[] { stackId.Value },
                quantity: 1,
                inputMassGrams: 10L);
            failureReason = string.Empty;
            return true;
        }

        public bool TryAcknowledgeSubstanceConsumption(
            string commitId,
            out string failureReason) => Acknowledge(commitId, out failureReason);

        public bool TryCommitDetoxConsumptionPending(
            ConsumableOperationId operationId,
            ItemStackId stackId,
            out CharacterDetoxPhysicalCommitSnapshot commit,
            out string failureReason)
        {
            string commitId = "commit:" + operationId.Value;
            pendingCommitByOperation[operationId.Value] = commitId;
            commit = new CharacterDetoxPhysicalCommitSnapshot(
                operationId.Value,
                CharacterConsumablesRuntime.DetoxPhysicalSinkReason,
                commitId,
                new[] { stackId.Value },
                quantity: 1,
                inputMassGrams: 10L);
            failureReason = string.Empty;
            return true;
        }

        public bool TryAcknowledgeDetoxConsumption(
            CharacterId characterId,
            ConsumableItemDefinitionId itemId,
            int quantity,
            string commitId,
            out string failureReason) => Acknowledge(commitId, out failureReason);

        public bool TryRequestDelivery(
            ConsumableItemDefinitionId itemId,
            int quantity,
            Vector2Int position,
            string destinationId,
            out int requested,
            out string failureReason)
        {
            requested = 0;
            failureReason = "not-needed";
            return false;
        }

        public void Publish(CharacterConsumablesMealConsumedEvent consumedEvent)
        {
        }

        private bool Acknowledge(string commitId, out string failureReason)
        {
            string key = pendingCommitByOperation.FirstOrDefault(pair =>
                string.Equals(pair.Value, commitId, StringComparison.Ordinal)).Key;
            if (string.IsNullOrWhiteSpace(key))
            {
                failureReason = "pending-commit-missing";
                return false;
            }
            pendingCommitByOperation.Remove(key);
            AcknowledgementCount++;
            failureReason = string.Empty;
            return true;
        }
    }

    private sealed class CharacterLifeCatalog : ICharacterLifeDefinitionCatalog
    {
        internal static readonly CharacterLifeCatalog Instance = new();
        internal SpeciesLifeHistoryDefinition History { get; } = new(
            new CharacterSpeciesId("species:migrated-e2e"),
            1,
            5,
            10,
            70,
            80f,
            false);

        public SpeciesLifeHistoryDefinition RequireLifeHistory(
            CharacterSpeciesId speciesId) => speciesId.Equals(History.SpeciesId)
                ? History
                : throw new KeyNotFoundException(speciesId.Value);

        public AgeConditionDefinition RequireAgeCondition(string conditionId) =>
            throw new KeyNotFoundException(conditionId);

        public IReadOnlyList<AgeConditionDefinition> GetAgeConditions(
            bool construct) => Array.Empty<AgeConditionDefinition>();
    }

    private sealed class NeutralHeritableTraitEffects : IHeritableTraitEffectQuery
    {
        internal static readonly NeutralHeritableTraitEffects Instance = new();

        public float GetMultiplier(
            CharacterId characterId,
            HeritableTraitConsequenceKind kind,
            string targetId) => 1f;
    }

    private sealed class DrainingGameClock : IGameClock
    {
        public float DeltaTime => 10f;
        public float Time { get; private set; }
        public int FrameCount { get; private set; }
        public bool IsPaused => false;

        internal void Advance()
        {
            Time += DeltaTime;
            FrameCount++;
        }
    }

    private sealed class DrainingCoroutineScheduler :
        ICharacterPrimitiveSurvivalCoroutineScheduler
    {
        private readonly DrainingGameClock clock;

        internal DrainingCoroutineScheduler(DrainingGameClock clock) =>
            this.clock = clock ?? throw new ArgumentNullException(nameof(clock));

        public void Start(CharacterActor owner, IEnumerator routine)
        {
            if (owner == null)
                throw new ArgumentNullException(nameof(owner));
            if (routine == null)
                throw new ArgumentNullException(nameof(routine));

            var stack = new Stack<IEnumerator>();
            stack.Push(routine);
            try
            {
                while (stack.Count > 0)
                {
                    IEnumerator current = stack.Peek();
                    if (!current.MoveNext())
                    {
                        (current as IDisposable)?.Dispose();
                        stack.Pop();
                        continue;
                    }
                    if (current.Current is IEnumerator nested)
                    {
                        stack.Push(nested);
                        continue;
                    }
                    clock.Advance();
                }
            }
            finally
            {
                while (stack.Count > 0)
                    (stack.Pop() as IDisposable)?.Dispose();
            }
        }
    }

    private sealed class EmptyGridSystemProvider : IGridSystemProvider
    {
        public GridSystemManager Manager => null;
        public Grid Grid => null;

        public bool TryGetManager(out GridSystemManager manager)
        {
            manager = null;
            return false;
        }

        public bool TryGetGrid(out Grid grid)
        {
            grid = null;
            return false;
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

    private sealed class EmptyFieldMealConsumption : IFieldMealConsumptionCommand
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

    private sealed class EmptyWildlifeSpeciesCatalog :
        IWildlifeSpeciesCatalogProvider
    {
        internal static readonly EmptyWildlifeSpeciesCatalog Instance = new();
        public IReadOnlyList<WildlifeSpeciesDefinition> All =>
            Array.Empty<WildlifeSpeciesDefinition>();
        public bool TryGetSpecies(
            string speciesId,
            out WildlifeSpeciesDefinition species)
        {
            species = null;
            return false;
        }
        public WildlifeSpeciesDefinition GetRandomSpecies(IRandomStream randomStream) =>
            throw new InvalidOperationException(
                "The humanoid-corpse fixture does not select wildlife species.");
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
                "filth:migrated-e2e:" + type,
                type,
                amount,
                position,
                sourceCharacterId,
                infectionRisk,
                wallStain);
        public bool Clean(string filthId, float workAmount, out float remainingAmount)
        {
            remainingAmount = 0f;
            return false;
        }
        public float GetCleanlinessPenalty(Vector2Int position, int radius = 0) => 0f;
        public List<WorldFilthSaveData> CaptureFilth() => new();
        public void RestoreFilth(
            IEnumerable<WorldFilthSaveData> saveData,
            int nextSequence)
        {
        }
    }

    private sealed class RecoverableWorldFilthQuery : IWorldFilthQuery
    {
        private readonly List<WorldFilthSaveData> rows = new();
        private int nextSequence = 1;

        public int StateVersion { get; private set; }
        public int NextFilthSequence => nextSequence;
        public IReadOnlyList<WorldFilthSnapshot> GetAll() => rows
            .Select(ToSnapshot)
            .ToArray();
        public IReadOnlyList<WorldFilthSnapshot> GetAt(Vector2Int position) =>
            rows.Where(value => value.gridX == position.x
                    && value.gridY == position.y)
                .Select(ToSnapshot)
                .ToArray();

        public WorldFilthSnapshot AddFilth(
            WorldFilthType type,
            Vector2Int position,
            float amount,
            string sourceCharacterId,
            float infectionRisk,
            bool wallStain = false)
        {
            var row = new WorldFilthSaveData
            {
                filthId = $"filth:migrated-e2e:{nextSequence++:D8}",
                type = type,
                amount = amount,
                gridX = position.x,
                gridY = position.y,
                sourceCharacterId = sourceCharacterId ?? string.Empty,
                infectionRisk = infectionRisk,
                wallStain = wallStain
            };
            rows.Add(row);
            StateVersion++;
            return ToSnapshot(row);
        }

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

        public List<WorldFilthSaveData> CaptureFilth() => rows
            .Select(Clone)
            .ToList();

        public void RestoreFilth(
            IEnumerable<WorldFilthSaveData> saveData,
            int restoredNextSequence)
        {
            rows.Clear();
            rows.AddRange((saveData ?? Array.Empty<WorldFilthSaveData>())
                .Where(value => value != null)
                .Select(Clone));
            nextSequence = restoredNextSequence;
            StateVersion++;
        }

        private static WorldFilthSaveData Clone(WorldFilthSaveData value) =>
            new()
            {
                filthId = value.filthId,
                type = value.type,
                amount = value.amount,
                gridX = value.gridX,
                gridY = value.gridY,
                sourceCharacterId = value.sourceCharacterId,
                infectionRisk = value.infectionRisk,
                wallStain = value.wallStain
            };

        private static WorldFilthSnapshot ToSnapshot(
            WorldFilthSaveData value) => new(
            value.filthId,
            value.type,
            value.amount,
            new Vector2Int(value.gridX, value.gridY),
            value.sourceCharacterId,
            value.infectionRisk,
            value.wallStain);
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

    private sealed class MutableWorldWaterQuery : IWorldWaterQuery
    {
        private WorldWaterSourceSaveData state;

        internal MutableWorldWaterQuery(
            WorldWaterQuality quality = WorldWaterQuality.Clean)
        {
            state = new WorldWaterSourceSaveData
            {
                sourceId = quality == WorldWaterQuality.Clean
                    ? "water:migrated-e2e:clean"
                    : "water:migrated-e2e:foul",
                gridX = 0,
                gridY = 0,
                terrainType = GridCellTerrainType.ShallowWater,
                quality = quality,
                capacity = 10f,
                remaining = 10f,
                regenerationPerSecond = 0f,
                pathogenDiseaseId = quality == WorldWaterQuality.Clean
                    ? string.Empty
                    : "disease:migrated-e2e:waterborne"
            };
        }

        internal string SourceId => state.sourceId;
        internal float Remaining => state.remaining;
        public int NextWaterSequence { get; private set; } = 2;

        public IReadOnlyList<WorldWaterSourceSnapshot> GetAllSources() =>
            new[] { Snapshot() };

        public bool TryGetSource(
            string sourceId,
            out WorldWaterSourceSnapshot source)
        {
            source = Snapshot();
            return string.Equals(state.sourceId, sourceId, StringComparison.Ordinal);
        }

        public bool TryFindDrinkSource(
            Vector2Int origin,
            bool allowFoul,
            out WorldWaterSourceSnapshot source)
        {
            source = Snapshot();
            return source.CanDrink;
        }

        public bool TryDrink(
            string sourceId,
            float amount,
            out WorldWaterQuality quality,
            out float consumed)
        {
            quality = state.quality;
            consumed = 0f;
            if (!string.Equals(state.sourceId, sourceId, StringComparison.Ordinal))
                return false;
            consumed = Mathf.Min(Mathf.Max(0f, amount), state.remaining);
            state.remaining -= consumed;
            return consumed > 0f;
        }

        public List<WorldWaterSourceSaveData> CaptureWaterSources() => new()
        {
            Clone(state)
        };

        public void RestoreWaterSources(
            IEnumerable<WorldWaterSourceSaveData> saveData,
            int nextSequence)
        {
            state = Clone(saveData?.FirstOrDefault()
                ?? throw new InvalidOperationException(
                    "Water rollback snapshot was empty."));
            NextWaterSequence = nextSequence;
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

        private WorldWaterSourceSnapshot Snapshot() => new(
            state.sourceId,
            new Vector2Int(state.gridX, state.gridY),
            state.terrainType,
            state.quality,
            state.capacity,
            state.remaining,
            state.regenerationPerSecond,
            state.pathogenDiseaseId);

        private static WorldWaterSourceSaveData Clone(
            WorldWaterSourceSaveData source) => new()
        {
            sourceId = source.sourceId,
            gridX = source.gridX,
            gridY = source.gridY,
            terrainType = source.terrainType,
            quality = source.quality,
            capacity = source.capacity,
            remaining = source.remaining,
            regenerationPerSecond = source.regenerationPerSecond,
            pathogenDiseaseId = source.pathogenDiseaseId
        };
    }

    private sealed class FixedClock : IGameClock
    {
        internal static readonly FixedClock Instance = new();
        public float DeltaTime => 0f;
        public float Time => 0f;
        public int FrameCount => 0;
        public bool IsPaused => false;
    }

    private sealed class FixedRunSeedProvider : IRunSeedProvider
    {
        internal FixedRunSeedProvider(int runSeed) => RunSeed = runSeed;
        public int RunSeed { get; }
    }

    private sealed class FixedCoreSessionRulesProvider : ICoreSessionRulesProvider
    {
        internal FixedCoreSessionRulesProvider(CoreSessionRulesDefinition rules) =>
            CoreSessionRules = rules
                ?? throw new ArgumentNullException(nameof(rules));
        public CoreSessionRulesDefinition CoreSessionRules { get; }
    }

    private static void Require(bool condition, string reason)
    {
        if (!condition)
            throw new InvalidOperationException(reason);
    }

    private static void ExecuteWaterWithExpectedDeliveryFault(
        MigratedProducerOutcomeDeliveryFault fault,
        Action execute)
    {
        bool expected = fault?.Enabled == true;
        try
        {
            execute();
        }
        catch (InvalidOperationException exception) when (
            expected
            && exception.Message.StartsWith(
                "water-effects-applied-publication-pending:",
                StringComparison.Ordinal))
        {
            return;
        }

        if (expected)
        {
            throw new InvalidOperationException(
                "The injected water delivery fault did not surface as pending publication.");
        }
    }
}

internal sealed class CharacterWaterFactPublisherProbe :
    IDomainFactPublisher,
    IDomainFactRecoveryPublisher
{
    private readonly DomainFactDispatcher inner;
    private readonly MigratedProducerOutcomeProbeTransaction probe;

    internal CharacterWaterFactPublisherProbe(
        DomainFactDispatcher inner,
        MigratedProducerOutcomeProbeTransaction probe)
    {
        this.inner = inner ?? throw new ArgumentNullException(nameof(inner));
        this.probe = probe ?? throw new ArgumentNullException(nameof(probe));
    }

    public bool TryReserve<TFact>(
        in TFact proposed,
        out DomainFactLease lease,
        out string failure)
    {
        if (proposed is not CharacterWaterConsumedFact water)
            return inner.TryReserve(proposed, out lease, out failure);

        lease = default;
        CharacterWaterFactData data = water.Data;
        if (!probe.TryObserveDomainFactReservation(
                MigratedProducerOutcomeKind.CharacterWaterConsumedEvent,
                out failure))
        {
            return false;
        }
        if (data.quality != WorldWaterQuality.Clean
            && !probe.TryObserveDomainFactReservation(
                MigratedProducerOutcomeKind.CharacterFoulWaterConsequenceResult,
                out failure))
        {
            return false;
        }
        return inner.TryReserve(proposed, out lease, out failure);
    }

    public DomainFactDelivery Publish<TFact>(
        in DomainFactLease lease,
        in TFact actual)
    {
        if (actual is not CharacterWaterConsumedFact water)
            return inner.Publish(lease, actual);

        bool foul = water.Data.quality != WorldWaterQuality.Clean;
        string publicationKey = lease.PublicationKey;
        if (!TryReadKeys(
                publicationKey,
                foul,
                out GameplayResultKey waterKey,
                out GameplayResultKey consequenceKey))
        {
            inner.Cancel(lease);
            return DomainFactDelivery.Reject(
                "migrated-producer-e2e-water-publication-key-invalid");
        }

        bool reject = probe.ObserveDomainFactCommitAttempt(
            MigratedProducerOutcomeKind.CharacterWaterConsumedEvent);
        if (foul)
        {
            reject |= probe.ObserveDomainFactCommitAttempt(
                MigratedProducerOutcomeKind.CharacterFoulWaterConsequenceResult);
        }
        if (reject)
        {
            inner.Cancel(lease);
            return DomainFactDelivery.Reject(
                "migrated-producer-e2e-injected-batch-commit-rejection");
        }

        DomainFactDelivery delivery = inner.Publish(lease, actual);
        if (delivery.MayBeCommitted)
        {
            probe.ObserveDomainFactCommitted(
                MigratedProducerOutcomeKind.CharacterWaterConsumedEvent,
                waterKey);
            if (foul)
            {
                probe.ObserveDomainFactCommitted(
                    MigratedProducerOutcomeKind
                        .CharacterFoulWaterConsequenceResult,
                    consequenceKey);
            }
        }
        return delivery;
    }

    public bool PublishBatch(
        IReadOnlyList<DomainFactEmission> facts,
        DomainFactDelivery[] results,
        out string failure) => inner.PublishBatch(facts, results, out failure);

    public DomainFactDelivery Replay<TFact>(in TFact fact) =>
        inner.Replay(fact);

    public void Cancel(in DomainFactLease lease) => inner.Cancel(lease);

    public bool ReplayBatch(
        IReadOnlyList<DomainFactReplay> facts,
        DomainFactDelivery[] results,
        out string failure) => inner.ReplayBatch(facts, results, out failure);

    private static bool TryReadKeys(
        string publicationKey,
        bool foul,
        out GameplayResultKey waterKey,
        out GameplayResultKey consequenceKey)
    {
        waterKey = default;
        consequenceKey = default;
        if (!foul)
        {
            return !GameplayDomainFactPairProof.TryReadKeys(
                    publicationKey,
                    out _,
                    out _)
                && GameplayDomainFactDeliveryQuery.TryReadKey(
                    publicationKey,
                    out waterKey);
        }

        return GameplayDomainFactPairProof.TryReadKeys(
                publicationKey,
                out string water,
                out string consequence)
            && GameplayDomainFactDeliveryQuery.TryReadKey(water, out waterKey)
            && GameplayDomainFactDeliveryQuery.TryReadKey(
                consequence,
                out consequenceKey);
    }
}
#endif
