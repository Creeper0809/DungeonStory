using System;
using System.Collections.Generic;
using System.Globalization;
using DungeonStory.Narrative.Korean;
using VContainer;

/// <summary>
/// Immutable truth captured by a committed research command, including zero-delta risk attempts.
/// The result key is allocated by the state transaction that owns the saved outcome sequence.
/// </summary>
public enum ResearchWorkCause { Work, ForbiddenLeap, ImmediateCompletion, KnowledgeProgress }

public readonly struct ResearchWorkOutcomeReceipt
{
    public ResearchWorkOutcomeReceipt(
        GameplayResultKey resultKey,
        int absoluteDay,
        string projectId,
        string projectDisplayName,
        string researcherId,
        string researcherDisplayName,
        string facilityId,
        string facilityDisplayName,
        float before,
        float after,
        float required,
        bool completed,
        IReadOnlyList<BlueprintUnlockRecord> unlocks,
        ResearchWorkCause cause = ResearchWorkCause.Work,
        ExtremeRiskResolution leap = default,
        float aftermathUntilSeconds = 0f,
        ResearchEquipmentOutcomeEvidence equipment = null)
    {
        if (unlocks == null)
            throw new ArgumentNullException(nameof(unlocks));

        BlueprintUnlockRecord[] snapshot = new BlueprintUnlockRecord[unlocks.Count];
        for (int index = 0; index < snapshot.Length; index++)
            snapshot[index] = unlocks[index];

        ResultKey = resultKey;
        AbsoluteDay = absoluteDay;
        ProjectId = projectId?.Trim() ?? string.Empty;
        ProjectDisplayName = projectDisplayName?.Trim() ?? string.Empty;
        ResearcherId = researcherId?.Trim() ?? string.Empty;
        ResearcherDisplayName = researcherDisplayName?.Trim() ?? string.Empty;
        FacilityId = facilityId?.Trim() ?? string.Empty;
        FacilityDisplayName = facilityDisplayName?.Trim() ?? string.Empty;
        Before = before;
        After = after;
        Required = required;
        Completed = completed;
        Unlocks = Array.AsReadOnly(snapshot);
        Cause = cause;
        Leap = leap;
        AftermathUntilSeconds = aftermathUntilSeconds;
        Equipment = equipment;

        if (!ResearchWorkOutcomeValidation.TryValidate(this, out string detail))
            throw new ArgumentException(detail, nameof(resultKey));
    }

    public GameplayResultKey ResultKey { get; }
    public int AbsoluteDay { get; }
    public string ProjectId { get; }
    public string ProjectDisplayName { get; }
    public string ResearcherId { get; }
    public string ResearcherDisplayName { get; }
    public string FacilityId { get; }
    public string FacilityDisplayName { get; }
    public float Before { get; }
    public float After { get; }
    public float Required { get; }
    public bool Completed { get; }
    public IReadOnlyList<BlueprintUnlockRecord> Unlocks { get; }
    public ResearchWorkCause Cause { get; }
    public ExtremeRiskResolution Leap { get; }
    public float AftermathUntilSeconds { get; }
    public ResearchEquipmentOutcomeEvidence Equipment { get; }
    internal int SpecialFactCount => Cause == ResearchWorkCause.Work ? 0
        : Cause == ResearchWorkCause.ForbiddenLeap ? 5 : 1;

    internal bool HasResearcher => !string.IsNullOrEmpty(ResearcherId);
    internal bool HasFacility => !string.IsNullOrEmpty(FacilityId);
}

public static class ResearchWorkOutcomeIds
{
    public const string ProducerId = "research.work";
    public const string CompletionAnchorTypeId = "research-work-completion-sha256";

    public static readonly GameplayOutcomeTypeId WorkApplied =
        new GameplayOutcomeTypeId("research.work-applied");
    public static readonly GameplayEntityKindId ProjectKind =
        new GameplayEntityKindId("research-project");
    public static readonly GameplayEntityKindId KnowledgeKind = new("knowledge-task");
    public static readonly GameplayEntityKindId CharacterKind =
        new GameplayEntityKindId("character");
    public static readonly GameplayEntityKindId FacilityKind =
        new GameplayEntityKindId("facility");
    public static readonly GameplayRoleId ProjectRole =
        new GameplayRoleId("research-subject");
    public static readonly GameplayRoleId ResearcherRole =
        new GameplayRoleId("researcher");
    public static readonly GameplayRoleId FacilityRole =
        new GameplayRoleId("research-site");
    public static readonly GameplayMetricId BeforeMetric =
        new GameplayMetricId("research.work-before");
    public static readonly GameplayMetricId AfterMetric =
        new GameplayMetricId("research.work-after");
    public static readonly GameplayMetricId DeltaMetric =
        new GameplayMetricId("research.work-delta");
    public static readonly GameplayMetricId RequiredMetric =
        new GameplayMetricId("research.work-required");
    public static readonly GameplayMetricUnitId WorkUnit =
        new GameplayMetricUnitId("work-unit");
    public static readonly GameplayOutcomeTagId ResearchTag =
        new GameplayOutcomeTagId("research");
    public static readonly GameplayOutcomeFactId CompletedFact =
        new GameplayOutcomeFactId("research.completed");
    public static readonly GameplayOutcomeFactId CauseFact = new("research.cause");
    public static readonly GameplayOutcomeFactId LeapOutcomeFact = new("research.leap-outcome");
    public static readonly GameplayOutcomeFactId LeapHashFact = new("research.leap-roll-hash");
    public static readonly GameplayOutcomeFactId LeapFractionFact = new("research.leap-progress-fraction");
    public static readonly GameplayOutcomeFactId LeapAftermathFact = new("research.leap-aftermath-until");

    public static GameplayOutcomeFactId UnlockTypeFact(int index) =>
        new GameplayOutcomeFactId("research.unlock.type."
            + index.ToString(CultureInfo.InvariantCulture));

    public static GameplayOutcomeFactId UnlockTargetFact(int index) =>
        new GameplayOutcomeFactId("research.unlock.target."
            + index.ToString(CultureInfo.InvariantCulture));
}

public static class ResearchWorkOutcomeRegistration
{
    public static void RegisterResearchWorkGameplayOutcomes(
        this IContainerBuilder builder)
    {
        if (builder == null)
            throw new ArgumentNullException(nameof(builder));

        builder.Register<ResearchWorkOutcomeAdapter>(Lifetime.Singleton)
            .As<IGameplayOutcomeAdapterRegistration>();
        builder.Register<ResearchWorkOutcomeDescriptor>(Lifetime.Singleton)
            .As<IGameplayOutcomeDescriptor>();
    }
}

public sealed class ResearchWorkOutcomeAdapter :
    GameplayOutcomeAdapter<ResearchWorkOutcomeReceipt>
{
    public override GameplayOutcomeTypeId OutcomeTypeId =>
        ResearchWorkOutcomeIds.WorkApplied;

    public override OutcomePrepareResult TryGetRequirements(
        in ResearchWorkOutcomeReceipt receipt,
        long currentWorldEpoch,
        out OutcomeWriteRequirements requirements)
    {
        requirements = default;
        if (!ResearchWorkOutcomeValidation.TryValidate(receipt, out string detail))
        {
            return new OutcomePrepareResult(
                OutcomePrepareCode.InvalidReceipt,
                detail);
        }

        int participantCount = 1
            + (receipt.HasResearcher ? 1 : 0)
            + (receipt.HasFacility ? 1 : 0) + (receipt.Equipment != null ? 1 : 0);
        int factCount = checked(1 + receipt.SpecialFactCount + receipt.Unlocks.Count * 2
            + (receipt.Equipment != null ? ResearchEquipmentOutcomeEncoding.FactCount : 0));
        requirements = new OutcomeWriteRequirements(
            receipt.ResultKey,
            OutcomeTypeId,
            receipt.AbsoluteDay,
            GameplayOutcomeStatus.Succeeded,
            currentWorldEpoch,
            receipt.ResultKey.CommitRevision,
            participantCount,
            receipt.Equipment != null ? 8 : 4,
            participantCount,
            1,
            receipt.Completed ? 1 : 0,
            provenanceCount: 0,
            factCount: factCount);
        return OutcomePrepareResult.Prepared();
    }

    public override OutcomePrepareResult TryWrite(
        in ResearchWorkOutcomeReceipt receipt,
        ref OutcomeWriteBuilder builder)
    {
        if (!ResearchWorkOutcomeValidation.TryValidate(receipt, out string detail))
            return Failed(detail);

        GameplayEntityId project = new GameplayEntityId(
            receipt.Cause == ResearchWorkCause.KnowledgeProgress ? ResearchWorkOutcomeIds.KnowledgeKind : ResearchWorkOutcomeIds.ProjectKind,
            receipt.ProjectId);
        if (!WriteParticipantAndSubject(
                ref builder,
                project,
                ResearchWorkOutcomeIds.ProjectRole,
                ResearchWorkOutcomeNames.Snapshot(
                    receipt.ProjectId,
                    receipt.ProjectDisplayName),
                receipt.Completed))
        {
            return Failed("research-work-project-write-failed");
        }

        if (receipt.HasResearcher)
        {
            GameplayEntityId researcher = new GameplayEntityId(
                ResearchWorkOutcomeIds.CharacterKind,
                receipt.ResearcherId);
            if (!WriteParticipantAndSubject(
                    ref builder,
                    researcher,
                    ResearchWorkOutcomeIds.ResearcherRole,
                    ResearchWorkOutcomeNames.Snapshot(
                        receipt.ResearcherId,
                        receipt.ResearcherDisplayName),
                    receipt.Completed))
            {
                return Failed("research-work-researcher-write-failed");
            }
        }

        if (receipt.HasFacility)
        {
            GameplayEntityId facility = new GameplayEntityId(
                ResearchWorkOutcomeIds.FacilityKind,
                receipt.FacilityId);
            if (!WriteParticipantAndSubject(
                    ref builder,
                    facility,
                    ResearchWorkOutcomeIds.FacilityRole,
                    ResearchWorkOutcomeNames.Snapshot(
                        receipt.FacilityId,
                        receipt.FacilityDisplayName),
                    receipt.Completed))
            {
                return Failed("research-work-facility-write-failed");
            }
        }

        if (!builder.AddMetric(new GameplayOutcomeMetric(
                ResearchWorkOutcomeIds.BeforeMetric,
                receipt.Before,
                ResearchWorkOutcomeIds.WorkUnit,
                project))
            || !builder.AddMetric(new GameplayOutcomeMetric(
                ResearchWorkOutcomeIds.AfterMetric,
                receipt.After,
                ResearchWorkOutcomeIds.WorkUnit,
                project))
            || !builder.AddMetric(new GameplayOutcomeMetric(
                ResearchWorkOutcomeIds.DeltaMetric,
                (double)receipt.After - receipt.Before,
                ResearchWorkOutcomeIds.WorkUnit,
                project))
            || !builder.AddMetric(new GameplayOutcomeMetric(
                ResearchWorkOutcomeIds.RequiredMetric,
                receipt.Required,
                ResearchWorkOutcomeIds.WorkUnit,
                project))
            || !builder.AddTag(ResearchWorkOutcomeIds.ResearchTag)
            || !builder.AddFact(new GameplayOutcomeFact(
                ResearchWorkOutcomeIds.CompletedFact,
                receipt.Completed ? "true" : "false")))
        {
            return Failed("research-work-metric-write-failed");
        }

        if (receipt.Cause != ResearchWorkCause.Work
            && !builder.AddFact(new GameplayOutcomeFact(ResearchWorkOutcomeIds.CauseFact,
                receipt.Cause == ResearchWorkCause.ForbiddenLeap ? "forbidden-leap"
                    : receipt.Cause == ResearchWorkCause.KnowledgeProgress ? "knowledge-progress" : "immediate-completion")))
            return Failed("research-cause-write-failed");
        if (receipt.Cause == ResearchWorkCause.ForbiddenLeap
            && (!builder.AddFact(new GameplayOutcomeFact(ResearchWorkOutcomeIds.LeapOutcomeFact,
                    receipt.Leap.Outcome.ToString().ToLowerInvariant()))
                || !builder.AddFact(new GameplayOutcomeFact(ResearchWorkOutcomeIds.LeapHashFact,
                    receipt.Leap.FixedRollHash.ToString("x16", CultureInfo.InvariantCulture)))
                || !builder.AddFact(new GameplayOutcomeFact(ResearchWorkOutcomeIds.LeapFractionFact,
                    receipt.Leap.ProgressDelta.ToString("R", CultureInfo.InvariantCulture)))
                || !builder.AddFact(new GameplayOutcomeFact(ResearchWorkOutcomeIds.LeapAftermathFact,
                    receipt.AftermathUntilSeconds.ToString("R", CultureInfo.InvariantCulture)))))
            return Failed("research-leap-fact-write-failed");

        if (receipt.Equipment != null
            && (!ResearchEquipmentOutcomeEncoding.Write(receipt.Equipment, ref builder)
                || !ResearchEquipmentOutcomeEncoding.WriteFacts(receipt.Equipment, ref builder)))
            return Failed("research-equipment-write-failed");

        for (int index = 0; index < receipt.Unlocks.Count; index++)
        {
            BlueprintUnlockRecord unlock = receipt.Unlocks[index];
            if (!builder.AddFact(new GameplayOutcomeFact(
                    ResearchWorkOutcomeIds.UnlockTypeFact(index),
                    unlock.UnlockTypeId))
                || !builder.AddFact(new GameplayOutcomeFact(
                    ResearchWorkOutcomeIds.UnlockTargetFact(index),
                    unlock.ValueId)))
            {
                return Failed("research-work-unlock-fact-write-failed");
            }
        }

        if (receipt.Completed
            && !builder.AddAnchor(
                project,
                ResearchWorkOutcomeValidation.CompletionAnchor(receipt)))
        {
            return Failed("research-work-completion-anchor-write-failed");
        }

        return OutcomePrepareResult.Prepared();
    }

    private static bool WriteParticipantAndSubject(
        ref OutcomeWriteBuilder builder,
        GameplayEntityId entity,
        GameplayRoleId role,
        KoreanNameSnapshot name,
        bool completed) =>
        builder.AddParticipant(new GameplayOutcomeParticipant(
            entity,
            role,
            GameplayParticipationKind.Direct,
            true,
            name))
        && builder.AddSubject(new GameplayOutcomeSubjectLink(
            entity,
            completed ? 1f : 0.55f,
            completed ? NarrativeMemoryTier.Core : NarrativeMemoryTier.Episodic,
            false,
            false,
            0));

    private static OutcomePrepareResult Failed(string detail) =>
        new OutcomePrepareResult(OutcomePrepareCode.AdapterWriteFailed, detail);
}

public sealed class ResearchWorkOutcomeDescriptor : IGameplayOutcomeDescriptor
{
    private static readonly IOutcomeMemoryPolicy Memory =
        new ResearchWorkOutcomeMemoryPolicy();
    private static readonly IOutcomePerceptionPolicy Perception =
        new ResearchWorkOutcomePerceptionPolicy();
    private static readonly IOutcomeMemoryConsolidator Consolidator =
        new ResearchWorkOutcomeConsolidator();
    private readonly ResearchWorkOutcomePerspectiveProjector projector;

    public ResearchWorkOutcomeDescriptor()
        : this(new KoreanJosaFormatter())
    {
    }

    [Inject]
    public ResearchWorkOutcomeDescriptor(IKoreanJosaFormatter josa) =>
        projector = new ResearchWorkOutcomePerspectiveProjector(josa);

    public GameplayOutcomeTypeId OutcomeTypeId => ResearchWorkOutcomeIds.WorkApplied;
    public INarrativePerspectiveProjector PerspectiveProjector => projector;
    public IOutcomeMemoryPolicy MemoryPolicy => Memory;
    public IOutcomePerceptionPolicy PerceptionPolicy => Perception;
    public IOutcomeMemoryConsolidator MemoryConsolidator => Consolidator;

    public bool IsKnownRole(GameplayRoleId roleId) =>
        roleId.Equals(ResearchWorkOutcomeIds.ProjectRole)
        || roleId.Equals(ResearchWorkOutcomeIds.ResearcherRole)
        || roleId.Equals(ResearchWorkOutcomeIds.FacilityRole)
        || roleId.Equals(ResearchEquipmentOutcomeEncoding.Role);

    public bool IsKnownMetric(
        GameplayMetricId metricId,
        GameplayMetricUnitId unitId) =>
        ResearchEquipmentOutcomeEncoding.IsMetric(metricId, unitId)
        || unitId.Equals(ResearchWorkOutcomeIds.WorkUnit)
        && (metricId.Equals(ResearchWorkOutcomeIds.BeforeMetric)
            || metricId.Equals(ResearchWorkOutcomeIds.AfterMetric)
            || metricId.Equals(ResearchWorkOutcomeIds.DeltaMetric)
            || metricId.Equals(ResearchWorkOutcomeIds.RequiredMetric));

    public OutcomeValidationResult Validate(in GameplayOutcomeReadView outcome)
    {
        if (outcome.OutcomeTypeId != OutcomeTypeId
            || outcome.Status != GameplayOutcomeStatus.Succeeded
            || outcome.AbsoluteDay < 0
            || !ResearchWorkOutcomeValidation.IsCanonicalResultKey(outcome.ResultKey)
            || outcome.OwnerRevision != outcome.ResultKey.CommitRevision
            || outcome.MetricCount != (ResearchEquipmentOutcomeEncoding.HasEquipment(outcome) ? 8 : 4)
            || outcome.TagCount != 1
            || !outcome.GetTag(0).Equals(ResearchWorkOutcomeIds.ResearchTag)
            || !TryValidateParticipants(outcome, out GameplayEntityId project)
            || !TryValidateMetrics(outcome, project, out bool completed)
            || !TryValidateFacts(outcome, completed)
            || !TryValidateCompletionAnchor(outcome, project, completed))
        {
            return OutcomeValidationResult.Reject("research-work-shape-invalid");
        }

        return OutcomeValidationResult.Accepted;
    }

    private static bool TryValidateParticipants(
        in GameplayOutcomeReadView outcome,
        out GameplayEntityId project)
    {
        project = default;
        if (outcome.ParticipantCount < 1 || outcome.ParticipantCount > 4
            || outcome.SubjectCount != outcome.ParticipantCount)
        {
            return false;
        }

        GameplayOutcomeParticipant projectParticipant = outcome.GetParticipant(0);
        if (!ResearchWorkOutcomeFacts.TryReadCause(outcome, out ResearchWorkCause cause, out _, out _, out _))
            return false;
        if (!IsParticipant(
                projectParticipant,
                cause == ResearchWorkCause.KnowledgeProgress ? ResearchWorkOutcomeIds.KnowledgeKind : ResearchWorkOutcomeIds.ProjectKind,
                ResearchWorkOutcomeIds.ProjectRole))
        {
            return false;
        }
        project = projectParticipant.EntityId;

        int participantIndex = 1;
        if (participantIndex < outcome.ParticipantCount
            && outcome.GetParticipant(participantIndex).RoleId.Equals(
                ResearchWorkOutcomeIds.ResearcherRole))
        {
            if (!IsParticipant(
                    outcome.GetParticipant(participantIndex),
                    ResearchWorkOutcomeIds.CharacterKind,
                    ResearchWorkOutcomeIds.ResearcherRole))
            {
                return false;
            }
            participantIndex++;
        }
        if (participantIndex < outcome.ParticipantCount
            && outcome.GetParticipant(participantIndex).RoleId.Equals(
                ResearchWorkOutcomeIds.FacilityRole))
        {
            if (!IsParticipant(
                    outcome.GetParticipant(participantIndex),
                    ResearchWorkOutcomeIds.FacilityKind,
                    ResearchWorkOutcomeIds.FacilityRole))
            {
                return false;
            }
            participantIndex++;
        }
        if (participantIndex < outcome.ParticipantCount
            && outcome.GetParticipant(participantIndex).RoleId.Equals(ResearchEquipmentOutcomeEncoding.Role))
        {
            if (!IsParticipant(outcome.GetParticipant(participantIndex), ResearchEquipmentOutcomeEncoding.Kind,
                    ResearchEquipmentOutcomeEncoding.Role)
                || participantIndex < 2
                || !outcome.GetParticipant(participantIndex - 1).RoleId.Equals(ResearchWorkOutcomeIds.FacilityRole))
                return false;
            participantIndex++;
        }
        if (participantIndex != outcome.ParticipantCount)
            return false;

        for (int index = 0; index < outcome.SubjectCount; index++)
        {
            GameplayOutcomeSubjectLink subject = outcome.GetSubject(index);
            // The ledger sorts links by subject ID before descriptor validation.
            // Participant order remains receipt order, so compare identities, not slots.
            int matches = 0;
            for (int participant = 0; participant < outcome.ParticipantCount; participant++)
                if (subject.SubjectId == outcome.GetParticipant(participant).EntityId)
                    matches++;
            if (matches != 1 || subject.IsOptionalWitness) return false;
        }
        return true;
    }

    private static bool IsParticipant(
        in GameplayOutcomeParticipant participant,
        GameplayEntityKindId kind,
        GameplayRoleId role) =>
        participant.EntityId.Kind.Equals(kind)
        && participant.RoleId.Equals(role)
        && participant.ParticipationKind == GameplayParticipationKind.Direct
        && participant.HasPerceptionEvidence
        && GameplayOutcomeLedger.IsValidDisplayNameSnapshot(participant.DisplayName);

    private static bool TryValidateMetrics(
        in GameplayOutcomeReadView outcome,
        GameplayEntityId project,
        out bool completed)
    {
        completed = false;
        GameplayOutcomeMetric before = outcome.GetMetric(0);
        GameplayOutcomeMetric after = outcome.GetMetric(1);
        GameplayOutcomeMetric delta = outcome.GetMetric(2);
        GameplayOutcomeMetric required = outcome.GetMetric(3);
        if (!IsMetric(before, ResearchWorkOutcomeIds.BeforeMetric, project)
            || !IsMetric(after, ResearchWorkOutcomeIds.AfterMetric, project)
            || !IsMetric(delta, ResearchWorkOutcomeIds.DeltaMetric, project)
            || !IsMetric(required, ResearchWorkOutcomeIds.RequiredMetric, project)
            || before.Value < 0d
            || required.Value <= 0d
            || before.Value > required.Value
            || after.Value < 0d
            || after.Value > required.Value
            || Math.Abs(delta.Value - (after.Value - before.Value)) > 0.00001d
            || !ResearchWorkOutcomeFacts.TryReadCause(outcome, out ResearchWorkCause cause,
                out ExtremeRiskResolution leap, out float aftermath, out _)
            || !ResearchWorkOutcomeValidation.IsValidChange(cause, leap, aftermath,
                (float)before.Value, (float)after.Value, (float)required.Value)
            || cause == ResearchWorkCause.ForbiddenLeap
                && (outcome.ParticipantCount < 2
                    || !outcome.GetParticipant(1).RoleId.Equals(ResearchWorkOutcomeIds.ResearcherRole)))
        {
            return false;
        }

        completed = cause != ResearchWorkCause.KnowledgeProgress && after.Value >= required.Value;
        return true;
    }

    private static bool IsMetric(
        in GameplayOutcomeMetric metric,
        GameplayMetricId id,
        GameplayEntityId project) =>
        metric.MetricId.Equals(id)
        && metric.UnitId.Equals(ResearchWorkOutcomeIds.WorkUnit)
        && metric.DefinitionOrInstanceId.Equals(project)
        && !double.IsNaN(metric.Value)
        && !double.IsInfinity(metric.Value);

    private static bool TryValidateFacts(
        in GameplayOutcomeReadView outcome,
        bool completed)
    {
        if (outcome.FactCount < 1
            || outcome.FactCount > GameplayOutcomeBufferLimits.AbsoluteMaximumFactsPerOutcome
            || !outcome.GetFact(0).FactId.Equals(ResearchWorkOutcomeIds.CompletedFact)
            || !string.Equals(
                outcome.GetFact(0).Value,
                completed ? "true" : "false",
                StringComparison.Ordinal))
        {
            return false;
        }

        if (!ResearchWorkOutcomeFacts.TryReadCause(outcome, out ResearchWorkCause cause, out _, out _, out int offset))
            return false;
        if (ResearchEquipmentOutcomeEncoding.HasEquipment(outcome)
            && cause != ResearchWorkCause.Work && cause != ResearchWorkCause.KnowledgeProgress)
            return false;
        if (!ResearchEquipmentOutcomeEncoding.Validate(outcome, ref offset)) return false;
        int unlockCount = (outcome.FactCount - offset) / 2;
        if (outcome.FactCount != offset + unlockCount * 2
            || !completed && unlockCount != 0)
        {
            return false;
        }

        for (int index = 0; index < unlockCount; index++)
        {
            GameplayOutcomeFact type = outcome.GetFact(offset + index * 2);
            GameplayOutcomeFact target = outcome.GetFact(offset + 1 + index * 2);
            if (!type.FactId.Equals(ResearchWorkOutcomeIds.UnlockTypeFact(index))
                || !target.FactId.Equals(ResearchWorkOutcomeIds.UnlockTargetFact(index))
                || !GameplayOutcomeStableIdSyntax.IsValid(type.Value)
                || !GameplayOutcomeStableIdSyntax.IsValid(target.Value))
            {
                return false;
            }
        }
        return true;
    }

    private static bool TryValidateCompletionAnchor(
        in GameplayOutcomeReadView outcome,
        GameplayEntityId project,
        bool completed)
    {
        if (!completed)
            return outcome.AnchorCount == 0;
        if (outcome.AnchorCount != 1
            || outcome.GetAnchorSubject(0) != project)
        {
            return false;
        }
        NarrativeEvidenceReference expected =
            ResearchWorkOutcomeValidation.CompletionAnchor(
                outcome.ResultKey,
                project.Value);
        return outcome.GetAnchor(0).Equals(expected);
    }
}

internal static class ResearchWorkOutcomeValidation
{
    public static bool IsValidChange(ResearchWorkCause cause, ExtremeRiskResolution leap,
        float aftermath, float before, float after, float required)
    {
        if (cause == ResearchWorkCause.Work || cause == ResearchWorkCause.KnowledgeProgress) return after > before;
        if (cause == ResearchWorkCause.ImmediateCompletion) return after == required;
        if (cause != ResearchWorkCause.ForbiddenLeap || !float.IsFinite(aftermath) || aftermath <= 0f
            || !float.IsFinite(leap.ProgressDelta)) return false;
        bool signMatches = leap.Outcome == ExtremeRiskOutcome.Normal ? leap.ProgressDelta == 0f
            : leap.Outcome == ExtremeRiskOutcome.Breakthrough ? leap.ProgressDelta > 0f
            : leap.Outcome == ExtremeRiskOutcome.Setback && leap.ProgressDelta < 0f;
        return signMatches && Math.Abs(after - Math.Clamp(before + leap.ProgressDelta * required, 0f, required)) < 0.00001f;
    }

    public static bool TryValidate(
        in ResearchWorkOutcomeReceipt receipt,
        out string detail)
    {
        detail = "research-work-receipt-invalid";
        if (!IsCanonicalResultKey(receipt.ResultKey)
            || receipt.AbsoluteDay < 0
            || !GameplayOutcomeStableIdSyntax.IsValid(receipt.ProjectId)
            || !IsDisplayText(receipt.ProjectDisplayName)
            || !IsOptionalIdentity(
                receipt.ResearcherId,
                receipt.ResearcherDisplayName)
            || !IsOptionalIdentity(
                receipt.FacilityId,
                receipt.FacilityDisplayName)
            || !float.IsFinite(receipt.Before)
            || !float.IsFinite(receipt.After)
            || !float.IsFinite(receipt.Required)
            || receipt.Before < 0f
            || receipt.Required <= 0f
            || receipt.Before > receipt.Required
            || receipt.After < 0f
            || receipt.After > receipt.Required
            || !IsValidChange(receipt.Cause, receipt.Leap, receipt.AftermathUntilSeconds,
                receipt.Before, receipt.After, receipt.Required)
            || receipt.Cause == ResearchWorkCause.ForbiddenLeap && !receipt.HasResearcher
            || receipt.Completed != (receipt.Cause != ResearchWorkCause.KnowledgeProgress && receipt.After >= receipt.Required)
            || receipt.Equipment != null && (!receipt.HasFacility || receipt.Equipment.FacilityId != receipt.FacilityId
                || receipt.Cause != ResearchWorkCause.Work && receipt.Cause != ResearchWorkCause.KnowledgeProgress)
            || receipt.Unlocks == null
            || receipt.Unlocks.Count > (GameplayOutcomeBufferLimits.AbsoluteMaximumFactsPerOutcome - 1 - receipt.SpecialFactCount
                - (receipt.Equipment != null ? ResearchEquipmentOutcomeEncoding.FactCount : 0)) / 2
            || !receipt.Completed && receipt.Unlocks.Count != 0)
        {
            return false;
        }

        for (int index = 0; index < receipt.Unlocks.Count; index++)
        {
            BlueprintUnlockRecord unlock = receipt.Unlocks[index];
            if (!unlock.IsApplied
                || !GameplayOutcomeStableIdSyntax.IsValid(unlock.UnlockTypeId)
                || !GameplayOutcomeStableIdSyntax.IsValid(unlock.ValueId))
            {
                detail = "research-work-unlock-invalid";
                return false;
            }
        }
        detail = string.Empty;
        return true;
    }

    public static bool IsCanonicalResultKey(GameplayResultKey resultKey) =>
        resultKey.IsValid
        && string.Equals(
            resultKey.ProducerId,
            ResearchWorkOutcomeIds.ProducerId,
            StringComparison.Ordinal)
        && resultKey.CommitRevision > 0L
        && resultKey.LocalResultIndex == 0
        && string.Equals(
            resultKey.OperationId.Value,
            "research-work:" + resultKey.CommitRevision.ToString(
                CultureInfo.InvariantCulture),
            StringComparison.Ordinal);

    public static NarrativeEvidenceReference CompletionAnchor(
        in ResearchWorkOutcomeReceipt receipt) =>
        CompletionAnchor(receipt.ResultKey, receipt.ProjectId);

    public static NarrativeEvidenceReference CompletionAnchor(
        GameplayResultKey resultKey,
        string projectId) => new NarrativeEvidenceReference(
        ResearchWorkOutcomeIds.CompletionAnchorTypeId,
        NarrativeInferenceHash.ComputeSha256Utf8(
            resultKey.ProducerId + "|"
            + resultKey.OperationId.Value + "|"
            + resultKey.CommitRevision.ToString(CultureInfo.InvariantCulture) + "|"
            + resultKey.LocalResultIndex.ToString(CultureInfo.InvariantCulture)
            + "|" + projectId));

    private static bool IsOptionalIdentity(string id, string display) =>
        string.IsNullOrEmpty(id)
            ? string.IsNullOrEmpty(display)
            : GameplayOutcomeStableIdSyntax.IsValid(id)
                && IsDisplayText(display);

    private static bool IsDisplayText(string value) =>
        GameplayOutcomeLedger.IsValidBoundedUtf16(
            value,
            GameplayOutcomeBufferLimits.MaximumDisplayTextUtf16Length,
            allowEmpty: false);
}

internal static class ResearchWorkOutcomeNames
{
    public static KoreanNameSnapshot Snapshot(string stableId, string displayText)
    {
        string identity = GameplayOutcomeStableIdSyntax.Require(
            stableId,
            nameof(stableId));
        string display = displayText?.Trim() ?? string.Empty;
        if (!GameplayOutcomeLedger.IsValidBoundedUtf16(
                display,
                GameplayOutcomeBufferLimits.MaximumDisplayTextUtf16Length,
                allowEmpty: false))
        {
            throw new ArgumentException(
                "An immutable participant display name is required.",
                nameof(displayText));
        }

        string revision = "research-work-name-v1:"
            + NarrativeInferenceHash.ComputeSha256Utf8(identity + "|" + display);
        return new KoreanNameSnapshot(
            display,
            revision,
            KoreanPronunciationHint.AutoHangulDisplay(revision),
            "ko-KR");
    }
}

internal sealed class ResearchWorkOutcomeMemoryPolicy : IOutcomeMemoryPolicy
{
    public int PolicyVersion => 1;

    public GameplayMemorySignature GetSignature(
        in GameplayOutcomeReadView outcome,
        GameplayEntityId subjectId)
    {
        if (!ResearchWorkOutcomeFacts.TryProject(outcome, out GameplayEntityId project))
            return new GameplayMemorySignature("research-work-progress-invalid");

        return new GameplayMemorySignature("research-work-progress:"
            + NarrativeInferenceHash.ComputeSha256Utf8(project.ToString()));
    }

    public OutcomeMemoryEvaluation Evaluate(
        in GameplayOutcomeReadView outcome,
        GameplayEntityId subjectId,
        int priorMatchingCount,
        int evaluationDay)
    {
        bool completed = ResearchWorkOutcomeFacts.IsCompleted(outcome);
        float salience = completed ? 1f : 0.55f;
        NarrativeMemoryTier tier = completed
            ? NarrativeMemoryTier.Core
            : priorMatchingCount > 0
                ? NarrativeMemoryTier.Compacted
                : NarrativeMemoryTier.Episodic;
        return new OutcomeMemoryEvaluation(
            salience,
            tier,
            completed
                ? int.MaxValue
                : Math.Max(evaluationDay + 1, outcome.AbsoluteDay + 1));
    }
}

internal sealed class ResearchWorkOutcomePerceptionPolicy : IOutcomePerceptionPolicy
{
    public int MaximumOptionalWitnessLinks => 0;

    public bool ShouldCreateOptionalWitnessLink(
        in GameplayOutcomeReadView outcome,
        in GameplayOutcomeSubjectLink candidate) => false;
}

internal sealed class ResearchWorkOutcomeConsolidator :
    IOutcomeMemoryConsolidator,
    IOutcomeCompactionContract
{
    public bool SupportsCompaction => true;

    public bool CanCompact(
        in GameplayOutcomeReadView outcome,
        GameplayEntityId subjectId) =>
        ResearchWorkOutcomeFacts.IsOrdinary(outcome)
        && !ResearchWorkOutcomeFacts.IsCompleted(outcome);

    public bool IsAdditiveMetric(GameplayMetricId metricId) =>
        metricId.Equals(ResearchWorkOutcomeIds.DeltaMetric);

    public bool CanMerge(
        in CompactedNarrativeMemoryReadView existing,
        in GameplayOutcomeReadView incoming,
        GameplayEntityId subjectId) =>
        existing.OutcomeTypeId.Equals(ResearchWorkOutcomeIds.WorkApplied)
        && incoming.OutcomeTypeId.Equals(ResearchWorkOutcomeIds.WorkApplied)
        && existing.SubjectId.Equals(subjectId)
        && ResearchWorkOutcomeFacts.TryProject(existing, out GameplayEntityId existingProject)
        && ResearchWorkOutcomeFacts.TryProject(incoming, out GameplayEntityId incomingProject)
        && existingProject.Equals(incomingProject)
        && ResearchWorkOutcomeFacts.IsOrdinary(incoming)
        && !ResearchWorkOutcomeFacts.IsCompleted(incoming);
}

internal sealed class ResearchWorkOutcomePerspectiveProjector :
    INarrativePerspectiveProjector,
    ICompactedNarrativePerspectiveProjector
{
    private readonly IKoreanJosaFormatter josa;

    public ResearchWorkOutcomePerspectiveProjector(IKoreanJosaFormatter josa) =>
        this.josa = josa ?? throw new ArgumentNullException(nameof(josa));

    public NarrativeView Project(
        in GameplayOutcomeReadView outcome,
        NarrativePerspectiveContext perspective)
    {
        TryParticipant(
            outcome,
            ResearchWorkOutcomeIds.ProjectRole,
            out GameplayOutcomeParticipant project);
        TryParticipant(
            outcome,
            ResearchWorkOutcomeIds.ResearcherRole,
            out GameplayOutcomeParticipant researcher);
        TryParticipant(
            outcome,
            ResearchWorkOutcomeIds.FacilityRole,
            out GameplayOutcomeParticipant facility);
        bool hasEquipment = TryParticipant(outcome, ResearchEquipmentOutcomeEncoding.Role,
            out GameplayOutcomeParticipant equipment);
        bool completed = ResearchWorkOutcomeFacts.IsCompleted(outcome);
        double after = ResearchWorkOutcomeFacts.Metric(
            outcome,
            ResearchWorkOutcomeIds.AfterMetric);
        double required = ResearchWorkOutcomeFacts.Metric(
            outcome,
            ResearchWorkOutcomeIds.RequiredMetric);

        bool neutral;
        string text;
        if (perspective.Kind == NarrativePerspectiveKind.Equipment
            && hasEquipment && perspective.ViewerId == equipment.EntityId)
        {
            neutral = true;
            text = $"연구 도구 · {project.DisplayName.DisplayText} · {after:0.##}/{required:0.##}"
                + (completed ? " · 연구 완료" : string.Empty);
        }
        else if (perspective.Kind == NarrativePerspectiveKind.Character
            && researcher.EntityId.IsValid
            && perspective.ViewerId == researcher.EntityId)
        {
            neutral = !TryWithJosa(
                project.DisplayName,
                KoreanJosaKind.Object,
                out string projectObject);
            text = neutral
                ? ResearcherFrame(researcher, project, after, required, completed)
                : completed
                    ? $"{projectObject} 완료했다."
                    : $"{projectObject} {after:0.##}/{required:0.##}까지 진행했다.";
        }
        else if (perspective.Kind == NarrativePerspectiveKind.Facility
            && facility.EntityId.IsValid
            && perspective.ViewerId == facility.EntityId)
        {
            neutral = !TryWithJosa(
                project.DisplayName,
                KoreanJosaKind.Subject,
                out string projectSubject);
            text = neutral
                ? FacilityFrame(facility, project, after, required, completed)
                : completed
                    ? $"{facility.DisplayName.DisplayText}에서 {projectSubject} 완료됐다."
                    : $"{facility.DisplayName.DisplayText}에서 {projectSubject} {after:0.##}/{required:0.##}까지 진행됐다.";
        }
        else
        {
            neutral = !TryWithJosa(
                project.DisplayName,
                KoreanJosaKind.Subject,
                out string projectSubject);
            text = neutral
                ? GlobalFrame(project, after, required, completed)
                : completed
                    ? $"{projectSubject} 완료됐다."
                    : $"{projectSubject} {after:0.##}/{required:0.##}까지 진행됐다.";
        }

        if (ResearchWorkOutcomeFacts.TryReadCause(outcome, out ResearchWorkCause cause,
                out ExtremeRiskResolution leap, out _, out _))
        {
            if (cause == ResearchWorkCause.ForbiddenLeap)
            {
                string label = leap.Outcome == ExtremeRiskOutcome.Breakthrough ? "돌파"
                    : leap.Outcome == ExtremeRiskOutcome.Setback ? "후퇴" : "진척 변화 없음";
                double before = ResearchWorkOutcomeFacts.Metric(outcome, ResearchWorkOutcomeIds.BeforeMetric);
                string actor = perspective.Kind == NarrativePerspectiveKind.Character
                    && perspective.ViewerId == researcher.EntityId ? string.Empty
                    : researcher.DisplayName.DisplayText + " · ";
                text = $"금단의 도약 · {actor}{project.DisplayName.DisplayText} · {label} · {before:0.##} → {after:0.##}/{required:0.##}"
                    + (completed ? " · 연구 완료" : string.Empty);
                neutral = true;
            }
            else if (cause == ResearchWorkCause.ImmediateCompletion)
                text = "즉시 완료 · " + text;
            else if (cause == ResearchWorkCause.KnowledgeProgress && after + .001d >= required)
                text += " · 결과 처리 대기";
        }

        if (hasEquipment)
        {
            double beforeDurability = ResearchWorkOutcomeFacts.Metric(outcome, ResearchEquipmentOutcomeEncoding.Before);
            double afterDurability = ResearchWorkOutcomeFacts.Metric(outcome, ResearchEquipmentOutcomeEncoding.After);
            double spent = ResearchWorkOutcomeFacts.Metric(outcome, ResearchEquipmentOutcomeEncoding.Spent);
            text += $" · 도구 내구도 {beforeDurability:0.##} → {afterDurability:0.##} (소모 {spent:0.##})";
        }

        return new NarrativeView(
            outcome.OutcomeId,
            perspective.Kind,
            text,
            "research-work-v2+" + josa.FormatterVersion,
            neutral);
    }

    public NarrativeMemoryView ProjectCompacted(
        in CompactedNarrativeMemoryReadView memory,
        NarrativePerspectiveContext perspective)
    {
        KoreanNameSnapshot name = memory.ParticipantCount > 0
            ? memory.GetParticipant(0).DisplayName
            : default;
        bool neutral = !TryWithJosa(name, KoreanJosaKind.Subject, out string subject);
        string display = name.DisplayText;
        string text = neutral
            ? $"연구 진행 누적 · {display} · {memory.OccurrenceCount}회"
            : $"{subject} 연구 진행을 {memory.OccurrenceCount}회 누적했다.";
        return new NarrativeMemoryView(
            memory.MemoryId,
            perspective.Kind,
            text,
            "research-work-v1+" + josa.FormatterVersion,
            neutral);
    }

    private bool TryWithJosa(
        in KoreanNameSnapshot name,
        KoreanJosaKind kind,
        out string text)
    {
        KoreanJosaFormatResult result = josa.Format(
            new KoreanJosaRequest(name, kind));
        text = result.Text;
        return !result.RequiresNeutralFrame;
    }

    private static bool TryParticipant(
        in GameplayOutcomeReadView outcome,
        GameplayRoleId role,
        out GameplayOutcomeParticipant participant)
    {
        for (int index = 0; index < outcome.ParticipantCount; index++)
        {
            GameplayOutcomeParticipant current = outcome.GetParticipant(index);
            if (current.RoleId.Equals(role))
            {
                participant = current;
                return true;
            }
        }
        participant = default;
        return false;
    }

    private static string GlobalFrame(
        in GameplayOutcomeParticipant project,
        double after,
        double required,
        bool completed) => completed
        ? $"연구 완료 · {project.DisplayName.DisplayText}"
        : $"연구 진행 · {project.DisplayName.DisplayText} · {after:0.##}/{required:0.##}";

    private static string ResearcherFrame(
        in GameplayOutcomeParticipant researcher,
        in GameplayOutcomeParticipant project,
        double after,
        double required,
        bool completed) => completed
        ? $"연구자: {researcher.DisplayName.DisplayText} · 연구 완료: {project.DisplayName.DisplayText}"
        : $"연구자: {researcher.DisplayName.DisplayText} · 연구 진행: {project.DisplayName.DisplayText} · {after:0.##}/{required:0.##}";

    private static string FacilityFrame(
        in GameplayOutcomeParticipant facility,
        in GameplayOutcomeParticipant project,
        double after,
        double required,
        bool completed) => completed
        ? $"연구 시설: {facility.DisplayName.DisplayText} · 연구 완료: {project.DisplayName.DisplayText}"
        : $"연구 시설: {facility.DisplayName.DisplayText} · 연구 진행: {project.DisplayName.DisplayText} · {after:0.##}/{required:0.##}";
}

internal static class ResearchWorkOutcomeFacts
{
    // Ordinary v1 receipts keep their original shape; extra facts identify special commands.
    public static bool TryReadCause(in GameplayOutcomeReadView outcome,
        out ResearchWorkCause cause, out ExtremeRiskResolution leap,
        out float aftermath, out int unlockOffset)
    {
        cause = ResearchWorkCause.Work;
        leap = default;
        aftermath = 0f;
        unlockOffset = 1;
        if (outcome.FactCount < 2 || !outcome.GetFact(1).FactId.Equals(ResearchWorkOutcomeIds.CauseFact))
            return true;
        string method = outcome.GetFact(1).Value;
        if (method == "immediate-completion" || method == "knowledge-progress")
        {
            cause = method == "knowledge-progress" ? ResearchWorkCause.KnowledgeProgress : ResearchWorkCause.ImmediateCompletion;
            unlockOffset = 2;
            return true;
        }
        if (method != "forbidden-leap" || outcome.FactCount < 6) return false;
        cause = ResearchWorkCause.ForbiddenLeap;
        unlockOffset = 6;
        if (!outcome.GetFact(2).FactId.Equals(ResearchWorkOutcomeIds.LeapOutcomeFact)
            || !outcome.GetFact(3).FactId.Equals(ResearchWorkOutcomeIds.LeapHashFact)
            || !outcome.GetFact(4).FactId.Equals(ResearchWorkOutcomeIds.LeapFractionFact)
            || !outcome.GetFact(5).FactId.Equals(ResearchWorkOutcomeIds.LeapAftermathFact)) return false;
        string risk = outcome.GetFact(2).Value;
        ExtremeRiskOutcome riskOutcome;
        if (risk == "normal") riskOutcome = ExtremeRiskOutcome.Normal;
        else if (risk == "breakthrough") riskOutcome = ExtremeRiskOutcome.Breakthrough;
        else if (risk == "setback") riskOutcome = ExtremeRiskOutcome.Setback;
        else return false;
        if (!ulong.TryParse(outcome.GetFact(3).Value, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out ulong hash)
            || hash.ToString("x16", CultureInfo.InvariantCulture) != outcome.GetFact(3).Value
            || !float.TryParse(outcome.GetFact(4).Value, NumberStyles.Float, CultureInfo.InvariantCulture, out float fraction)
            || !float.TryParse(outcome.GetFact(5).Value, NumberStyles.Float, CultureInfo.InvariantCulture, out aftermath)) return false;
        leap = new ExtremeRiskResolution(riskOutcome, 1f, 1f, fraction, hash);
        return true;
    }

    public static bool IsOrdinary(in GameplayOutcomeReadView outcome) =>
        !ResearchEquipmentOutcomeEncoding.HasEquipment(outcome)
        && (outcome.FactCount < 2 || !outcome.GetFact(1).FactId.Equals(ResearchWorkOutcomeIds.CauseFact));

    public static bool TryProject(
        in GameplayOutcomeReadView outcome,
        out GameplayEntityId project)
    {
        for (int index = 0; index < outcome.ParticipantCount; index++)
        {
            GameplayOutcomeParticipant participant = outcome.GetParticipant(index);
            if (participant.RoleId.Equals(ResearchWorkOutcomeIds.ProjectRole))
            {
                project = participant.EntityId;
                return project.IsValid;
            }
        }
        project = default;
        return false;
    }

    public static bool TryProject(
        in CompactedNarrativeMemoryReadView memory,
        out GameplayEntityId project)
    {
        for (int index = 0; index < memory.ParticipantCount; index++)
        {
            CompactedNarrativeParticipant participant = memory.GetParticipant(index);
            if (participant.RoleId.Equals(ResearchWorkOutcomeIds.ProjectRole))
            {
                project = participant.EntityId;
                return project.IsValid;
            }
        }
        project = default;
        return false;
    }

    public static bool IsCompleted(in GameplayOutcomeReadView outcome)
    {
        for (int index = 0; index < outcome.FactCount; index++)
        {
            GameplayOutcomeFact fact = outcome.GetFact(index);
            if (fact.FactId.Equals(ResearchWorkOutcomeIds.CompletedFact))
            {
                return string.Equals(fact.Value, "true", StringComparison.Ordinal);
            }
        }
        return false;
    }

    public static double Metric(
        in GameplayOutcomeReadView outcome,
        GameplayMetricId id)
    {
        for (int index = 0; index < outcome.MetricCount; index++)
        {
            GameplayOutcomeMetric metric = outcome.GetMetric(index);
            if (metric.MetricId.Equals(id))
                return metric.Value;
        }
        return 0d;
    }
}
