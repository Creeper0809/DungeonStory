#if UNITY_EDITOR
using System;
using System.Globalization;
using DungeonStory.Foundation;
using UnityEngine;

public static class ResearchEquipmentOutcomeDebugScenarios
{
    private const string ProjectId = "research:equipment-outcome";
    private const string FacilityId = "building:equipment-outcome-lab";
    private const string ResearcherId = "character:equipment-outcome-researcher";
    private const string ToolStackId = "stack:equipment-outcome-tool";
    private const string ToolItemId = "item:equipment-outcome-tool";

    public static string RunAll()
    {
        VerifyNormalPositiveWear();
        VerifyFinalClampedWear();
        VerifyInvalidEvidenceRejection();
        VerifyEquipmentQueryAndPerspective();
        VerifyPendingOutboxJsonRestore();
        return "RESEARCH_EQUIPMENT_OUTCOME=PASS (5 groups)";
    }

    private static void VerifyNormalPositiveWear()
    {
        Fixture fixture = new("run:research-equipment-normal");
        ResearchEquipmentOutcomeEvidence evidence = new(
            CreateContext(before: 80d, after: 76.5d, requestedWear: 3.5d));
        GameplayOutcomeId outcomeId = Publish(
            fixture,
            CreateReceipt(revision: 1L, before: 12f, after: 20f,
                required: 100f, completed: false, evidence: evidence));

        Require(fixture.Ledger.TryGetExact(outcomeId, out GameplayOutcomeSnapshot exact),
            "Normal equipment outcome was not retained as exact history.");
        RequireEvidenceSnapshot(exact, evidence, 12d, 20d, 100d, false);
    }

    private static void VerifyFinalClampedWear()
    {
        Fixture fixture = new("run:research-equipment-final");
        ResearchEquipmentOutcomeEvidence evidence = new(
            CreateContext(before: 2d, after: 0d, requestedWear: 5d));
        GameplayOutcomeId outcomeId = Publish(
            fixture,
            CreateReceipt(revision: 2L, before: 96f, after: 100f,
                required: 100f, completed: true, evidence: evidence));

        Require(fixture.Ledger.TryGetExact(outcomeId, out GameplayOutcomeSnapshot exact),
            "Final clamped-wear outcome was not retained as exact history.");
        RequireEvidenceSnapshot(exact, evidence, 96d, 100d, 100d, true);
        Require(Nearly(evidence.Spent, 2d) && Nearly(evidence.RequestedWear, 5d),
            "Final equipment wear did not retain distinct spent and requested values.");
    }

    private static void VerifyInvalidEvidenceRejection()
    {
        RequireThrows<ArgumentException>(
            () => _ = new ResearchEquipmentOutcomeEvidence(CreateContext(
                before: 20d,
                after: 19d,
                requestedWear: 1d,
                subjectItemId: "item:equipment-outcome-other")),
            "Requirement/item mismatch produced research equipment evidence.");
        RequireThrows<ArgumentException>(
            () => _ = CreateContext(
                before: 20d,
                after: 19d,
                requestedWear: 1d,
                beforeRevision: 7L,
                afterRevision: 7L),
            "Non-advancing equipment revision produced a use context.");
        RequireThrows<ArgumentException>(
            () => _ = new ResearchEquipmentOutcomeEvidence(CreateContext(
                before: 20d,
                after: 17d,
                requestedWear: 1d)),
            "Wear delta mismatch produced research equipment evidence.");
    }

    private static void VerifyEquipmentQueryAndPerspective()
    {
        Fixture fixture = new("run:research-equipment-perspective");
        ResearchEquipmentOutcomeEvidence evidence = new(
            CreateContext(before: 40d, after: 38d, requestedWear: 2d));
        GameplayOutcomeId outcomeId = Publish(
            fixture,
            CreateReceipt(revision: 3L, before: 20f, after: 24f,
                required: 100f, completed: false, evidence: evidence));
        GameplayEntityId tool = new(
            ResearchEquipmentOutcomeEncoding.Kind,
            evidence.StackId);
        GameplayOutcomeQueryPage page = fixture.Ledger.GetForEntity(
            tool,
            OutcomeCursor.FirstPage(),
            OutcomeFilter.All);

        Require(page.Items.Count == 1
            && !page.Items[0].IsCompacted
            && page.Items[0].Exact != null
            && string.Equals(page.Items[0].Exact.runId, outcomeId.RunId.Value,
                StringComparison.Ordinal)
            && page.Items[0].Exact.sequence == outcomeId.Sequence,
            "Research-tool entity query did not return the committed outcome ID.");
        Require(fixture.Ledger.TryProject(
                outcomeId,
                new NarrativePerspectiveContext(
                    tool,
                    NarrativePerspectiveKind.Equipment,
                    "ko-KR"),
                out NarrativeView view)
            && view.OutcomeId.Equals(outcomeId)
            && view.PerspectiveKind == NarrativePerspectiveKind.Equipment
            && view.NeutralFrameUsed
            && view.Text.Contains("연구 도구", StringComparison.Ordinal),
            "Research-tool perspective did not preserve the exact outcome ID and neutral tool frame.");
    }

    private static void VerifyPendingOutboxJsonRestore()
    {
        Fixture fixture = new("run:research-equipment-outbox");
        ResearchEquipmentOutcomeEvidence evidence = new(
            CreateContext(before: 15d, after: 12d, requestedWear: 3d));
        ResearchWorkOutcomeReceipt receipt = CreateReceipt(
            revision: 4L,
            before: 24f,
            after: 30f,
            required: 100f,
            completed: false,
            evidence: evidence);
        OutcomePrepareResult prepare = fixture.Recorder.TryPrepare(
            receipt,
            out PreparedOutcomeToken prepared);
        Require(prepare.Success, "Pending outbox receipt preparation failed: " + prepare.DetailCode);
        OutcomeCommitResult commit = fixture.Recorder.CommitPrepared(
            prepared,
            receipt.ResultKey.CommitRevision,
            out CommittedOutcomeToken committed);
        Require(commit.Success && committed.OutcomeId.IsValid,
            "Pending outbox receipt commit failed: " + commit.DetailCode);

        string json = JsonUtility.ToJson(fixture.Ledger.CaptureGameplayOutcomes());
        GameplayOutcomeLedgerSaveData saved = JsonUtility.FromJson<
            GameplayOutcomeLedgerSaveData>(json);
        Require(saved.outbox.Count == 1 && saved.exactOutcomes.Count == 0,
            "Committed equipment outcome did not persist in the durable outbox.");

        GameplayOutcomeRegistry registry = CreateRegistry();
        GameplayOutcomeLedger restored = new(
            registry,
            GameplayOutcomeBufferLimits.Default);
        GameplayOutcomeLedgerRestoreCandidate restore =
            restored.PrepareGameplayOutcomeRestore(saved);
        restored.BeginRestoreCandidate();
        restored.PublishGameplayOutcomeRestore(restore);
        restored.PublishRestoreCandidate();
        restored.CompleteRestoreCandidate();
        GameplayOutcomeRecorder recorder = new(restored, registry, new GameEventBus());
        GameplayOutcomeSnapshot exact = null;
        Require(recorder.RetryPendingDeliveries(1) == 1
            && restored.TryGetExact(committed.OutcomeId,
                out exact),
            "Restored research equipment outbox did not publish once.");
        RequireEvidenceSnapshot(exact, evidence, 24d, 30d, 100d, false);
    }

    private static GameplayOutcomeId Publish(
        Fixture fixture,
        ResearchWorkOutcomeReceipt receipt)
    {
        OutcomePrepareResult prepare = fixture.Recorder.TryPrepare(
            receipt,
            out PreparedOutcomeToken prepared);
        Require(prepare.Success, "Research equipment receipt preparation failed: "
            + prepare.DetailCode);
        OutcomeCommitResult commit = fixture.Recorder.CommitPrepared(
            prepared,
            receipt.ResultKey.CommitRevision,
            out CommittedOutcomeToken committed);
        Require(commit.Success, "Research equipment receipt commit failed: "
            + commit.DetailCode);
        OutcomeDeliveryResult delivery = fixture.Recorder.TryDeliver(committed);
        Require(delivery.Published,
            "Research equipment receipt delivery failed: " + delivery.DetailCode);
        Require(fixture.Recorder.Acknowledge(committed).Success,
            "Research equipment receipt acknowledgement failed.");
        return delivery.OutcomeId;
    }

    private static ResearchWorkOutcomeReceipt CreateReceipt(
        long revision,
        float before,
        float after,
        float required,
        bool completed,
        ResearchEquipmentOutcomeEvidence evidence) => new(
        resultKey: new GameplayResultKey(
            ResearchWorkOutcomeIds.ProducerId,
            new GameplayOperationId(
                "research-work:" + revision.ToString(CultureInfo.InvariantCulture)),
            revision,
            0),
        absoluteDay: 0,
        projectId: ProjectId,
        projectDisplayName: "도구 연구",
        researcherId: ResearcherId,
        researcherDisplayName: "연구자",
        facilityId: FacilityId,
        facilityDisplayName: "연구실",
        before: before,
        after: after,
        required: required,
        completed: completed,
        unlocks: Array.Empty<BlueprintUnlockRecord>(),
        cause: ResearchWorkCause.Work,
        leap: default,
        aftermathUntilSeconds: 0f,
        equipment: evidence);

    private static DurableFacilityEquipmentUseContext CreateContext(
        double before,
        double after,
        double requestedWear,
        long beforeRevision = 7L,
        long afterRevision = 8L,
        string subjectItemId = ToolItemId)
    {
        DurableFacilityEquipmentRequirement requirement = new(
            "research-equipment-outcome-requirement",
            (ItemDefinitionId)ToolItemId,
            requiredQuantity: 1);
        return new DurableFacilityEquipmentUseContext(
            CreateSlot(requirement),
            requirement,
            CreateSubject(ToolStackId, beforeRevision, subjectItemId, before),
            CreateSubject(ToolStackId, afterRevision, subjectItemId, after),
            requestedWear);
    }

    private static DurableFacilityEquipmentSlotSnapshot CreateSlot(
        DurableFacilityEquipmentRequirement requirement)
    {
        DurableFacilityEquipmentPolicy policy = new(
            "research-equipment-outcome-policy",
            1L,
            "research-equipment-outcome",
            DurableFacilityEquipmentSlotIdentity.DefinitionMassPolicyKind,
            DurableFacilityEquipmentPolicyKinds.PositiveDurabilityComponent,
            new[] { requirement });
        DurableFacilityEquipmentAssignment assignment = policy.CreateAssignment(
            ProjectId,
            (BuildingInstanceId)FacilityId,
            new Vector2Int(3, 2));
        const long sequence = 1L;
        return new DurableFacilityEquipmentSlotSnapshot(
            assignment,
            sequence,
            DurableFacilityEquipmentSlotIdentity.BuildDestinationId(
                assignment.Key,
                sequence),
            DurableFacilityEquipmentSlotIdentity.BuildOwnerOperationId(
                assignment.Key,
                sequence),
            DurableFacilityEquipmentFingerprint.CreateAssignment(assignment),
            new DurableFacilityEquipmentCapacityProjection(
                DurableFacilityEquipmentSlotIdentity.DefinitionMassPolicyKind,
                new PhysicalMassGrams(1_300L),
                1L,
                new string('a', 64)),
            new[]
            {
                new DurableFacilityEquipmentRequirementStatus(
                    requirement,
                    pendingQuantity: 0,
                    bufferedUsableQuantity: 1)
            });
    }

    private static DurableFacilityEquipmentUseSubject CreateSubject(
        string stackId,
        long revision,
        string itemId,
        double current) => new(
        stackId,
        revision,
        (ItemDefinitionId)itemId,
        1,
        new[]
        {
            new DurableFacilityEquipmentComponentSnapshot(
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
                        100d,
                        false)
                })
        });

    private static void RequireEvidenceSnapshot(
        GameplayOutcomeSnapshot snapshot,
        ResearchEquipmentOutcomeEvidence evidence,
        double workBefore,
        double workAfter,
        double workRequired,
        bool completed)
    {
        Require(snapshot != null
            && snapshot.participants.Count == 4
            && snapshot.metrics.Count == 8
            && snapshot.facts.Count == 5
            && snapshot.subjects.Count == 4
            && snapshot.anchors.Count == (completed ? 1 : 0),
            "Research equipment outcome shape is incomplete.");
        RequireParticipant(snapshot.participants[0], "research-project", ProjectId,
            "research-subject");
        RequireParticipant(snapshot.participants[1], "character", ResearcherId,
            "researcher");
        RequireParticipant(snapshot.participants[2], "facility", FacilityId,
            "research-site");
        RequireParticipant(snapshot.participants[3], "item-stack", evidence.StackId,
            "research-tool");
        RequireMetric(snapshot.metrics[0], "research.work-before", workBefore,
            "work-unit", "research-project", ProjectId);
        RequireMetric(snapshot.metrics[1], "research.work-after", workAfter,
            "work-unit", "research-project", ProjectId);
        RequireMetric(snapshot.metrics[2], "research.work-delta", workAfter - workBefore,
            "work-unit", "research-project", ProjectId);
        RequireMetric(snapshot.metrics[3], "research.work-required", workRequired,
            "work-unit", "research-project", ProjectId);
        RequireMetric(snapshot.metrics[4], "research.tool-durability-before", evidence.Before,
            "durability-point", "item-stack", evidence.StackId);
        RequireMetric(snapshot.metrics[5], "research.tool-durability-after", evidence.After,
            "durability-point", "item-stack", evidence.StackId);
        RequireMetric(snapshot.metrics[6], "research.tool-durability-spent", evidence.Spent,
            "durability-point", "item-stack", evidence.StackId);
        RequireMetric(snapshot.metrics[7], "research.tool-durability-requested", evidence.RequestedWear,
            "durability-point", "item-stack", evidence.StackId);
        RequireFact(snapshot.facts[0], "research.completed", completed ? "true" : "false");
        RequireFact(snapshot.facts[1], "research.tool-definition", evidence.ItemId);
        RequireFact(snapshot.facts[2], "research.tool-slot-sequence",
            evidence.AssignmentSequence.ToString(CultureInfo.InvariantCulture));
        RequireFact(snapshot.facts[3], "research.tool-revision-before",
            evidence.RevisionBefore.ToString(CultureInfo.InvariantCulture));
        RequireFact(snapshot.facts[4], "research.tool-revision-after",
            evidence.RevisionAfter.ToString(CultureInfo.InvariantCulture));
    }

    private static void RequireParticipant(
        GameplayOutcomeParticipantSnapshot participant,
        string expectedKind,
        string expectedId,
        string expectedRole) => Require(participant != null
            && participant.entityKindId == expectedKind
            && participant.entityId == expectedId
            && participant.roleId == expectedRole
            && participant.participationKind == GameplayParticipationKind.Direct
            && participant.hasPerceptionEvidence,
            "Research equipment participant identity is incorrect.");

    private static void RequireMetric(
        GameplayOutcomeMetricSnapshot metric,
        string expectedId,
        double expectedValue,
        string expectedUnit,
        string expectedReferenceKind,
        string expectedReferenceId) => Require(metric != null
            && metric.metricId == expectedId
            && Nearly(metric.value, expectedValue)
            && metric.unitId == expectedUnit
            && metric.referenceKindId == expectedReferenceKind
            && metric.referenceId == expectedReferenceId,
            "Research equipment metric is incorrect: " + expectedId);

    private static void RequireFact(
        GameplayOutcomeFactSnapshot fact,
        string expectedId,
        string expectedValue) => Require(fact != null
            && fact.factId == expectedId
            && fact.value == expectedValue,
            "Research equipment fact is incorrect: " + expectedId);

    private static GameplayOutcomeRegistry CreateRegistry() => new(
        new IGameplayOutcomeDescriptor[] { new ResearchWorkOutcomeDescriptor() },
        new IGameplayOutcomeAdapterRegistration[] { new ResearchWorkOutcomeAdapter() });

    private static bool Nearly(double left, double right) =>
        Math.Abs(left - right) <= 0.0000001d;

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(
                "Research equipment outcome regression: " + message);
    }

    private static void RequireThrows<TException>(Action action, string message)
        where TException : Exception
    {
        try
        {
            action();
        }
        catch (TException)
        {
            return;
        }
        throw new InvalidOperationException(
            "Research equipment outcome regression: " + message);
    }

    private sealed class Fixture
    {
        internal Fixture(string runId)
        {
            Registry = CreateRegistry();
            Ledger = new GameplayOutcomeLedger(
                Registry,
                GameplayOutcomeBufferLimits.Default,
                new GameplayOutcomeRunId(runId),
                1L);
            Recorder = new GameplayOutcomeRecorder(
                Ledger,
                Registry,
                new GameEventBus());
        }

        internal GameplayOutcomeRegistry Registry { get; }
        internal GameplayOutcomeLedger Ledger { get; }
        internal GameplayOutcomeRecorder Recorder { get; }
    }
}
#endif
