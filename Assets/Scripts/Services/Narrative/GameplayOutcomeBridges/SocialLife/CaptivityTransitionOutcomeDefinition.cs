using System;
using DungeonStory.Narrative.Korean;

public static class CaptivityTransitionOutcomeIds
{
    public const string RehabilitationProducer = "captivity.rehabilitation";
    public const string ReleaseProducer = "captivity.release";

    public static readonly GameplayOutcomeTypeId Rehabilitation =
        new("captivity.rehabilitation-progressed");
    public static readonly GameplayOutcomeTypeId Release =
        new("captivity.release-resolved");

    public static readonly GameplayEntityKindId CharacterKind = new("character");
    public static readonly GameplayEntityKindId BuildingKind = new("building");
    public static readonly GameplayEntityKindId StockCategoryKind =
        new("stock-category");
    public static readonly GameplayRoleId CaptiveRole = new("captive");
    public static readonly GameplayRoleId WardenRole = new("warden");
    public static readonly GameplayRoleId DeciderRole = new("decider");
    public static readonly GameplayRoleId FacilityRole = new("facility");
    public static readonly GameplayRoleId FoodRole = new("food");

    public static readonly GameplayMetricUnitId PointUnit = new("point");
    public static readonly GameplayMetricUnitId WorkUnit = new("work-unit");
    public static readonly GameplayMetricUnitId CountUnit = new("count");
    public static readonly GameplayMetricUnitId BooleanUnit = new("boolean");
    public static readonly GameplayMetricUnitId RevisionUnit = new("revision");

    public static readonly GameplayMetricId ApprovedWork =
        new("captivity.rehabilitation.approved-work");
    public static readonly GameplayMetricId WorkBefore =
        new("captivity.rehabilitation.work-before");
    public static readonly GameplayMetricId WorkAfter =
        new("captivity.rehabilitation.work-after");
    public static readonly GameplayMetricId DaysBefore =
        new("captivity.rehabilitation.days-before");
    public static readonly GameplayMetricId DaysAfter =
        new("captivity.rehabilitation.days-after");
    public static readonly GameplayMetricId TrustBefore =
        new("captivity.rehabilitation.trust-before");
    public static readonly GameplayMetricId TrustAfter =
        new("captivity.rehabilitation.trust-after");
    public static readonly GameplayMetricId GrudgeBefore =
        new("captivity.rehabilitation.grudge-before");
    public static readonly GameplayMetricId GrudgeAfter =
        new("captivity.rehabilitation.grudge-after");
    public static readonly GameplayMetricId CorruptionBefore =
        new("captivity.rehabilitation.corruption-before");
    public static readonly GameplayMetricId CorruptionAfter =
        new("captivity.rehabilitation.corruption-after");
    public static readonly GameplayMetricId FoodConsumed =
        new("captivity.rehabilitation.food-consumed");
    public static readonly GameplayMetricId CompletedDay =
        new("captivity.rehabilitation.completed-day");
    public static readonly GameplayMetricId RehabilitationRevision =
        new("captivity.rehabilitation.owner-revision");

    public static readonly GameplayMetricId CustodyBefore =
        new("captivity.release.in-custody-before");
    public static readonly GameplayMetricId CustodyAfter =
        new("captivity.release.in-custody-after");
    public static readonly GameplayMetricId RestrainedBefore =
        new("captivity.release.restrained-before");
    public static readonly GameplayMetricId RestrainedAfter =
        new("captivity.release.restrained-after");
    public static readonly GameplayMetricId AiPausedBefore =
        new("captivity.release.ai-paused-before");
    public static readonly GameplayMetricId AiPausedAfter =
        new("captivity.release.ai-paused-after");
    public static readonly GameplayMetricId DoorCaptiveBefore =
        new("captivity.release.door-captive-before");
    public static readonly GameplayMetricId DoorCaptiveAfter =
        new("captivity.release.door-captive-after");
    public static readonly GameplayMetricId PhysicalInputsReturned =
        new("captivity.release.physical-inputs-returned");
    public static readonly GameplayMetricId ReleaseRevision =
        new("captivity.release.owner-revision");

    public static readonly GameplayOutcomeFactId StatusBefore =
        new("captivity.release.status-before");
    public static readonly GameplayOutcomeFactId StatusAfter =
        new("captivity.release.status-after");
    public static readonly GameplayOutcomeFactId ActorTypeBefore =
        new("captivity.release.actor-type-before");
    public static readonly GameplayOutcomeFactId ActorTypeAfter =
        new("captivity.release.actor-type-after");
    public static readonly GameplayOutcomeFactId LifecycleBefore =
        new("captivity.release.lifecycle-before");
    public static readonly GameplayOutcomeFactId LifecycleAfter =
        new("captivity.release.lifecycle-after");

    public static readonly GameplayOutcomeTagId RehabilitationTag =
        new("rehabilitation");
    public static readonly GameplayOutcomeTagId ReleaseTag = new("release");
    public static readonly GameplayOutcomeTagId CaptivityTag = new("captivity");
    public static readonly GameplayOutcomeProvenanceReference Source =
        new("runtime-receipt", "captivity-transition-v1");

    public static GameplayResultKey RehabilitationKey(
        string captiveId,
        long revision) => new(
        RehabilitationProducer,
        new GameplayOperationId("captivity-rehabilitation:" + captiveId),
        revision,
        0);

    public static GameplayResultKey ReleaseKey(string captiveId) => new(
        ReleaseProducer,
        new GameplayOperationId("captivity-release:" + captiveId),
        1L,
        0);
}

public readonly struct CaptivityRehabilitationOutcomeReceipt
{
    public CaptivityRehabilitationOutcomeReceipt(
        string captiveId,
        KoreanNameSnapshot captiveName,
        string wardenId,
        KoreanNameSnapshot wardenName,
        string facilityId,
        KoreanNameSnapshot facilityName,
        int absoluteDay,
        bool completedDay,
        float approvedWork,
        float workBefore,
        float workAfter,
        int daysBefore,
        int daysAfter,
        float trustBefore,
        float trustAfter,
        float grudgeBefore,
        float grudgeAfter,
        float corruptionBefore,
        float corruptionAfter,
        int foodConsumed,
        long ownerRevision)
    {
        CaptiveId = new CharacterId(captiveId);
        WardenId = new CharacterId(wardenId);
        FacilityId = new BuildingInstanceId(facilityId);
        if (!CaptiveId.IsValid || !WardenId.IsValid || !FacilityId.IsValid)
            throw new ArgumentException("Canonical rehabilitation participants are required.");
        if (!GameplayOutcomeLedger.IsValidDisplayNameSnapshot(captiveName)
            || !GameplayOutcomeLedger.IsValidDisplayNameSnapshot(wardenName)
            || !GameplayOutcomeLedger.IsValidDisplayNameSnapshot(facilityName))
        {
            throw new ArgumentException("Frozen rehabilitation display names are required.");
        }
        if (absoluteDay < 0 || ownerRevision <= 0L
            || !float.IsFinite(approvedWork) || approvedWork <= 0f
            || !float.IsFinite(workBefore) || !float.IsFinite(workAfter)
            || workBefore < 0f || workAfter < 0f
            || daysBefore < 0 || daysAfter < 0 || foodConsumed < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(approvedWork));
        }
        if (!IsPercentage(trustBefore) || !IsPercentage(trustAfter)
            || !IsPercentage(grudgeBefore) || !IsPercentage(grudgeAfter)
            || !IsPercentage(corruptionBefore) || !IsPercentage(corruptionAfter))
        {
            throw new ArgumentOutOfRangeException(nameof(trustBefore));
        }
        float accumulated = workBefore + approvedWork;
        bool coherent = completedDay
            ? foodConsumed == CaptivityStateTransitionRules.RehabilitationFoodCost
                && workAfter == 0f
                && daysAfter == daysBefore + 1
                && Approximately(
                    accumulated,
                    CaptivityStateTransitionRules.RehabilitationRequiredWork)
                && trustAfter >= trustBefore
                && grudgeAfter <= grudgeBefore
                && corruptionAfter <= corruptionBefore
            : foodConsumed == 0
                && daysAfter == daysBefore
                && Approximately(workAfter, accumulated)
                && workAfter + 0.001f
                    < CaptivityStateTransitionRules.RehabilitationRequiredWork
                && trustAfter == trustBefore
                && grudgeAfter == grudgeBefore
                && corruptionAfter == corruptionBefore;
        if (!coherent)
            throw new ArgumentException("Rehabilitation transition is incoherent.");
        CaptiveName = captiveName;
        WardenName = wardenName;
        FacilityName = facilityName;
        AbsoluteDay = absoluteDay;
        CompletedDay = completedDay;
        ApprovedWork = approvedWork;
        WorkBefore = workBefore;
        WorkAfter = workAfter;
        DaysBefore = daysBefore;
        DaysAfter = daysAfter;
        TrustBefore = trustBefore;
        TrustAfter = trustAfter;
        GrudgeBefore = grudgeBefore;
        GrudgeAfter = grudgeAfter;
        CorruptionBefore = corruptionBefore;
        CorruptionAfter = corruptionAfter;
        FoodConsumed = foodConsumed;
        OwnerRevision = ownerRevision;
    }

    public CharacterId CaptiveId { get; }
    public KoreanNameSnapshot CaptiveName { get; }
    public CharacterId WardenId { get; }
    public KoreanNameSnapshot WardenName { get; }
    public BuildingInstanceId FacilityId { get; }
    public KoreanNameSnapshot FacilityName { get; }
    public int AbsoluteDay { get; }
    public bool CompletedDay { get; }
    public float ApprovedWork { get; }
    public float WorkBefore { get; }
    public float WorkAfter { get; }
    public int DaysBefore { get; }
    public int DaysAfter { get; }
    public float TrustBefore { get; }
    public float TrustAfter { get; }
    public float GrudgeBefore { get; }
    public float GrudgeAfter { get; }
    public float CorruptionBefore { get; }
    public float CorruptionAfter { get; }
    public int FoodConsumed { get; }
    public long OwnerRevision { get; }
    public GameplayResultKey ResultKey =>
        CaptivityTransitionOutcomeIds.RehabilitationKey(
            CaptiveId.Value,
            OwnerRevision);

    private static bool IsPercentage(float value) =>
        float.IsFinite(value) && value >= 0f && value <= 100f;

    private static bool Approximately(float left, float right) =>
        Math.Abs(left - right) <= 0.001f;
}

public readonly struct CaptivityReleaseOutcomeReceipt
{
    public CaptivityReleaseOutcomeReceipt(
        string captiveId,
        KoreanNameSnapshot captiveName,
        string deciderId,
        KoreanNameSnapshot deciderName,
        string facilityId,
        KoreanNameSnapshot facilityName,
        int absoluteDay,
        CaptivityStatus statusBefore,
        bool inCustodyBefore,
        bool restrainedBefore,
        string actorTypeBefore,
        string lifecycleBefore,
        bool aiPausedBefore,
        bool doorCaptiveBefore)
    {
        CaptiveId = new CharacterId(captiveId);
        DeciderId = new CharacterId(deciderId);
        FacilityId = new BuildingInstanceId(facilityId);
        if (!CaptiveId.IsValid || !DeciderId.IsValid)
            throw new ArgumentException("Canonical release characters are required.");
        if (!GameplayOutcomeLedger.IsValidDisplayNameSnapshot(captiveName)
            || !GameplayOutcomeLedger.IsValidDisplayNameSnapshot(deciderName))
        {
            throw new ArgumentException("Frozen release display names are required.");
        }
        if (FacilityId.IsValid
            != GameplayOutcomeLedger.IsValidDisplayNameSnapshot(facilityName))
        {
            throw new ArgumentException("Release facility identity and name must agree.");
        }
        if (absoluteDay < 0 || !inCustodyBefore
            || !IsCustodyStatus(statusBefore)
            || string.IsNullOrWhiteSpace(actorTypeBefore)
            || !string.Equals(
                actorTypeBefore,
                actorTypeBefore.Trim(),
                StringComparison.Ordinal)
            || string.IsNullOrWhiteSpace(lifecycleBefore)
            || !string.Equals(
                lifecycleBefore,
                lifecycleBefore.Trim(),
                StringComparison.Ordinal))
            throw new ArgumentOutOfRangeException(nameof(absoluteDay));
        CaptiveName = captiveName;
        DeciderName = deciderName;
        FacilityName = facilityName;
        AbsoluteDay = absoluteDay;
        StatusBefore = statusBefore;
        InCustodyBefore = inCustodyBefore;
        RestrainedBefore = restrainedBefore;
        ActorTypeBefore = actorTypeBefore;
        LifecycleBefore = lifecycleBefore;
        AiPausedBefore = aiPausedBefore;
        DoorCaptiveBefore = doorCaptiveBefore;
    }

    public CharacterId CaptiveId { get; }
    public KoreanNameSnapshot CaptiveName { get; }
    public CharacterId DeciderId { get; }
    public KoreanNameSnapshot DeciderName { get; }
    public BuildingInstanceId FacilityId { get; }
    public KoreanNameSnapshot FacilityName { get; }
    public int AbsoluteDay { get; }
    public CaptivityStatus StatusBefore { get; }
    public bool InCustodyBefore { get; }
    public bool RestrainedBefore { get; }
    public string ActorTypeBefore { get; }
    public string LifecycleBefore { get; }
    public bool AiPausedBefore { get; }
    public bool DoorCaptiveBefore { get; }
    public GameplayResultKey ResultKey =>
        CaptivityTransitionOutcomeIds.ReleaseKey(CaptiveId.Value);

    private static bool IsCustodyStatus(CaptivityStatus status) => status is
        CaptivityStatus.Stabilizing
        or CaptivityStatus.AwaitingEscort
        or CaptivityStatus.Escorting
        or CaptivityStatus.Confined
        or CaptivityStatus.Labor
        or CaptivityStatus.Interaction
        or CaptivityStatus.Performer
        or CaptivityStatus.EscapeAttempt
        or CaptivityStatus.Ransom;
}

public sealed class CaptivityRehabilitationOutcomeAdapter :
    GameplayOutcomeAdapter<CaptivityRehabilitationOutcomeReceipt>
{
    public override GameplayOutcomeTypeId OutcomeTypeId =>
        CaptivityTransitionOutcomeIds.Rehabilitation;

    public override OutcomePrepareResult TryGetRequirements(
        in CaptivityRehabilitationOutcomeReceipt receipt,
        long currentWorldEpoch,
        out OutcomeWriteRequirements requirements)
    {
        requirements = new OutcomeWriteRequirements(
            receipt.ResultKey,
            OutcomeTypeId,
            receipt.AbsoluteDay,
            GameplayOutcomeStatus.Succeeded,
            currentWorldEpoch,
            receipt.OwnerRevision,
            participantCount: 4,
            subjectCount: 4,
            metricCount: 14,
            tagCount: 2,
            anchorCount: 0,
            provenanceCount: 1);
        return receipt.ResultKey.IsValid
            ? OutcomePrepareResult.Prepared()
            : new OutcomePrepareResult(
                OutcomePrepareCode.InvalidReceipt,
                "captivity-rehabilitation-receipt-invalid");
    }

    public override OutcomePrepareResult TryWrite(
        in CaptivityRehabilitationOutcomeReceipt receipt,
        ref OutcomeWriteBuilder builder)
    {
        GameplayEntityId captive = Character(receipt.CaptiveId);
        GameplayEntityId warden = Character(receipt.WardenId);
        GameplayEntityId facility = new(
            CaptivityTransitionOutcomeIds.BuildingKind,
            receipt.FacilityId.Value);
        GameplayEntityId food = new(
            CaptivityTransitionOutcomeIds.StockCategoryKind,
            "food");
        KoreanNameSnapshot foodName = CaptivityTransitionOutcomeNames.Create(
            "food",
            "음식",
            "captivity-food");
        bool written = AddParticipant(ref builder, captive,
                CaptivityTransitionOutcomeIds.CaptiveRole, receipt.CaptiveName)
            && AddParticipant(ref builder, warden,
                CaptivityTransitionOutcomeIds.WardenRole, receipt.WardenName)
            && AddParticipant(ref builder, facility,
                CaptivityTransitionOutcomeIds.FacilityRole, receipt.FacilityName)
            && AddParticipant(ref builder, food,
                CaptivityTransitionOutcomeIds.FoodRole, foodName)
            && AddSubject(ref builder, captive, 0.78f)
            && AddSubject(ref builder, warden, 0.62f)
            && AddSubject(ref builder, facility, 0.55f)
            && AddSubject(ref builder, food, 0.48f)
            && Metric(ref builder, CaptivityTransitionOutcomeIds.ApprovedWork,
                receipt.ApprovedWork, CaptivityTransitionOutcomeIds.WorkUnit, warden)
            && Metric(ref builder, CaptivityTransitionOutcomeIds.WorkBefore,
                receipt.WorkBefore, CaptivityTransitionOutcomeIds.WorkUnit, captive)
            && Metric(ref builder, CaptivityTransitionOutcomeIds.WorkAfter,
                receipt.WorkAfter, CaptivityTransitionOutcomeIds.WorkUnit, captive)
            && Metric(ref builder, CaptivityTransitionOutcomeIds.DaysBefore,
                receipt.DaysBefore, CaptivityTransitionOutcomeIds.CountUnit, captive)
            && Metric(ref builder, CaptivityTransitionOutcomeIds.DaysAfter,
                receipt.DaysAfter, CaptivityTransitionOutcomeIds.CountUnit, captive)
            && Metric(ref builder, CaptivityTransitionOutcomeIds.TrustBefore,
                receipt.TrustBefore, CaptivityTransitionOutcomeIds.PointUnit, captive)
            && Metric(ref builder, CaptivityTransitionOutcomeIds.TrustAfter,
                receipt.TrustAfter, CaptivityTransitionOutcomeIds.PointUnit, captive)
            && Metric(ref builder, CaptivityTransitionOutcomeIds.GrudgeBefore,
                receipt.GrudgeBefore, CaptivityTransitionOutcomeIds.PointUnit, captive)
            && Metric(ref builder, CaptivityTransitionOutcomeIds.GrudgeAfter,
                receipt.GrudgeAfter, CaptivityTransitionOutcomeIds.PointUnit, captive)
            && Metric(ref builder, CaptivityTransitionOutcomeIds.CorruptionBefore,
                receipt.CorruptionBefore, CaptivityTransitionOutcomeIds.PointUnit, captive)
            && Metric(ref builder, CaptivityTransitionOutcomeIds.CorruptionAfter,
                receipt.CorruptionAfter, CaptivityTransitionOutcomeIds.PointUnit, captive)
            && Metric(ref builder, CaptivityTransitionOutcomeIds.FoodConsumed,
                receipt.FoodConsumed, CaptivityTransitionOutcomeIds.CountUnit, food)
            && Metric(ref builder, CaptivityTransitionOutcomeIds.CompletedDay,
                receipt.CompletedDay ? 1d : 0d,
                CaptivityTransitionOutcomeIds.BooleanUnit, captive)
            && Metric(ref builder,
                CaptivityTransitionOutcomeIds.RehabilitationRevision,
                receipt.OwnerRevision,
                CaptivityTransitionOutcomeIds.RevisionUnit,
                captive)
            && builder.AddTag(CaptivityTransitionOutcomeIds.RehabilitationTag)
            && builder.AddTag(CaptivityTransitionOutcomeIds.CaptivityTag)
            && builder.AddProvenance(CaptivityTransitionOutcomeIds.Source);
        return written
            ? OutcomePrepareResult.Prepared()
            : new OutcomePrepareResult(
                OutcomePrepareCode.AdapterWriteFailed,
                "captivity-rehabilitation-write-failed");
    }

    private static GameplayEntityId Character(CharacterId id) => new(
        CaptivityTransitionOutcomeIds.CharacterKind,
        id.Value);
    internal static bool AddParticipant(ref OutcomeWriteBuilder builder,
        GameplayEntityId entity, GameplayRoleId role, KoreanNameSnapshot name) =>
        builder.AddParticipant(new GameplayOutcomeParticipant(
            entity, role, GameplayParticipationKind.Direct, true, name));
    internal static bool AddSubject(ref OutcomeWriteBuilder builder,
        GameplayEntityId entity, float salience) =>
        builder.AddSubject(new GameplayOutcomeSubjectLink(
            entity, salience, NarrativeMemoryTier.Episodic, false, false, 0));
    internal static bool Metric(ref OutcomeWriteBuilder builder,
        GameplayMetricId id, double value, GameplayMetricUnitId unit,
        GameplayEntityId subject) => builder.AddMetric(
        new GameplayOutcomeMetric(id, value, unit, subject));
}

public sealed class CaptivityReleaseOutcomeAdapter :
    GameplayOutcomeAdapter<CaptivityReleaseOutcomeReceipt>
{
    public override GameplayOutcomeTypeId OutcomeTypeId =>
        CaptivityTransitionOutcomeIds.Release;

    public override OutcomePrepareResult TryGetRequirements(
        in CaptivityReleaseOutcomeReceipt receipt,
        long currentWorldEpoch,
        out OutcomeWriteRequirements requirements)
    {
        int participantCount = receipt.FacilityId.IsValid ? 3 : 2;
        requirements = new OutcomeWriteRequirements(
            receipt.ResultKey,
            OutcomeTypeId,
            receipt.AbsoluteDay,
            GameplayOutcomeStatus.Succeeded,
            currentWorldEpoch,
            1L,
            participantCount,
            subjectCount: participantCount,
            metricCount: 10,
            tagCount: 2,
            anchorCount: 0,
            provenanceCount: 1,
            factCount: 6);
        return receipt.ResultKey.IsValid
            ? OutcomePrepareResult.Prepared()
            : new OutcomePrepareResult(
                OutcomePrepareCode.InvalidReceipt,
                "captivity-release-receipt-invalid");
    }

    public override OutcomePrepareResult TryWrite(
        in CaptivityReleaseOutcomeReceipt receipt,
        ref OutcomeWriteBuilder builder)
    {
        GameplayEntityId captive = new(
            CaptivityTransitionOutcomeIds.CharacterKind,
            receipt.CaptiveId.Value);
        GameplayEntityId decider = new(
            CaptivityTransitionOutcomeIds.CharacterKind,
            receipt.DeciderId.Value);
        bool written = CaptivityRehabilitationOutcomeAdapter.AddParticipant(
                ref builder, captive, CaptivityTransitionOutcomeIds.CaptiveRole,
                receipt.CaptiveName)
            && CaptivityRehabilitationOutcomeAdapter.AddParticipant(
                ref builder, decider, CaptivityTransitionOutcomeIds.DeciderRole,
                receipt.DeciderName)
            && CaptivityRehabilitationOutcomeAdapter.AddSubject(
                ref builder, captive, 0.86f)
            && CaptivityRehabilitationOutcomeAdapter.AddSubject(
                ref builder, decider, 0.7f);
        if (written && receipt.FacilityId.IsValid)
        {
            GameplayEntityId facility = new(
                CaptivityTransitionOutcomeIds.BuildingKind,
                receipt.FacilityId.Value);
            written = CaptivityRehabilitationOutcomeAdapter.AddParticipant(
                    ref builder, facility,
                    CaptivityTransitionOutcomeIds.FacilityRole,
                    receipt.FacilityName)
                && CaptivityRehabilitationOutcomeAdapter.AddSubject(
                    ref builder, facility, 0.58f);
        }
        written = written
            && Bool(ref builder, CaptivityTransitionOutcomeIds.CustodyBefore,
                receipt.InCustodyBefore, captive)
            && Bool(ref builder, CaptivityTransitionOutcomeIds.CustodyAfter,
                false, captive)
            && Bool(ref builder, CaptivityTransitionOutcomeIds.RestrainedBefore,
                receipt.RestrainedBefore, captive)
            && Bool(ref builder, CaptivityTransitionOutcomeIds.RestrainedAfter,
                false, captive)
            && Bool(ref builder, CaptivityTransitionOutcomeIds.AiPausedBefore,
                receipt.AiPausedBefore, captive)
            && Bool(ref builder, CaptivityTransitionOutcomeIds.AiPausedAfter,
                false, captive)
            && Bool(ref builder, CaptivityTransitionOutcomeIds.DoorCaptiveBefore,
                receipt.DoorCaptiveBefore, captive)
            && Bool(ref builder, CaptivityTransitionOutcomeIds.DoorCaptiveAfter,
                false, captive)
            && Bool(ref builder,
                CaptivityTransitionOutcomeIds.PhysicalInputsReturned, true, captive)
            && CaptivityRehabilitationOutcomeAdapter.Metric(
                ref builder, CaptivityTransitionOutcomeIds.ReleaseRevision, 1d,
                CaptivityTransitionOutcomeIds.RevisionUnit, captive)
            && builder.AddFact(new GameplayOutcomeFact(
                CaptivityTransitionOutcomeIds.StatusBefore,
                receipt.StatusBefore.ToString()))
            && builder.AddFact(new GameplayOutcomeFact(
                CaptivityTransitionOutcomeIds.StatusAfter,
                CaptivityStatus.Released.ToString()))
            && builder.AddFact(new GameplayOutcomeFact(
                CaptivityTransitionOutcomeIds.ActorTypeBefore,
                receipt.ActorTypeBefore))
            && builder.AddFact(new GameplayOutcomeFact(
                CaptivityTransitionOutcomeIds.ActorTypeAfter,
                "Intruder"))
            && builder.AddFact(new GameplayOutcomeFact(
                CaptivityTransitionOutcomeIds.LifecycleBefore,
                receipt.LifecycleBefore))
            && builder.AddFact(new GameplayOutcomeFact(
                CaptivityTransitionOutcomeIds.LifecycleAfter,
                "Active"))
            && builder.AddTag(CaptivityTransitionOutcomeIds.ReleaseTag)
            && builder.AddTag(CaptivityTransitionOutcomeIds.CaptivityTag)
            && builder.AddProvenance(CaptivityTransitionOutcomeIds.Source);
        return written
            ? OutcomePrepareResult.Prepared()
            : new OutcomePrepareResult(
                OutcomePrepareCode.AdapterWriteFailed,
                "captivity-release-write-failed");
    }

    private static bool Bool(ref OutcomeWriteBuilder builder,
        GameplayMetricId id, bool value, GameplayEntityId subject) =>
        CaptivityRehabilitationOutcomeAdapter.Metric(
            ref builder, id, value ? 1d : 0d,
            CaptivityTransitionOutcomeIds.BooleanUnit, subject);
}


internal static class CaptivityTransitionOutcomeNames
{
    public static KoreanNameSnapshot Create(
        string stableId,
        string displayName,
        string source) => new(
        string.IsNullOrWhiteSpace(displayName) ? stableId : displayName.Trim(),
        source + "-display-v1:"
            + NarrativeInferenceHash.ComputeSha256Utf8(
                stableId + "|" + displayName),
        KoreanPronunciationHint.AutoHangulDisplay(source + ":" + stableId),
        "ko-KR");
}

internal sealed class CaptivityTransitionPerspectiveProjector :
    INarrativePerspectiveProjector
{
    private readonly bool release;
    internal CaptivityTransitionPerspectiveProjector(bool release) =>
        this.release = release;
    public NarrativeView Project(in GameplayOutcomeReadView outcome,
        NarrativePerspectiveContext perspective)
    {
        string name = outcome.ParticipantCount > 0
            ? outcome.GetParticipant(0).DisplayName.DisplayText
            : "대상";
        return new NarrativeView(
            outcome.OutcomeId,
            perspective.Kind,
            release
                ? name + "의 석방이 확정되었다."
                : name + "의 재사회화가 진행되었다.",
            "captivity-transition-v1",
            true);
    }
}

public sealed class CaptivityRehabilitationOutcomeDescriptor :
    IGameplayOutcomeDescriptor
{
    private static readonly INarrativePerspectiveProjector Projector =
        new CaptivityTransitionPerspectiveProjector(false);
    private static readonly IOutcomeMemoryPolicy Memory =
        new SocialLifeOutcomeMemoryPolicy();
    private static readonly IOutcomePerceptionPolicy Perception =
        new SocialLifeOutcomePerceptionPolicy();
    private static readonly IOutcomeMemoryConsolidator Consolidator =
        new SocialConflictOutcomeConsolidator();
    public GameplayOutcomeTypeId OutcomeTypeId =>
        CaptivityTransitionOutcomeIds.Rehabilitation;
    public INarrativePerspectiveProjector PerspectiveProjector => Projector;
    public IOutcomeMemoryPolicy MemoryPolicy => Memory;
    public IOutcomePerceptionPolicy PerceptionPolicy => Perception;
    public IOutcomeMemoryConsolidator MemoryConsolidator => Consolidator;
    public bool IsKnownRole(GameplayRoleId roleId) =>
        roleId.Equals(CaptivityTransitionOutcomeIds.CaptiveRole)
        || roleId.Equals(CaptivityTransitionOutcomeIds.WardenRole)
        || roleId.Equals(CaptivityTransitionOutcomeIds.FacilityRole)
        || roleId.Equals(CaptivityTransitionOutcomeIds.FoodRole);
    public bool IsKnownMetric(GameplayMetricId metricId,
        GameplayMetricUnitId unitId) =>
        unitId.Equals(CaptivityTransitionOutcomeIds.WorkUnit)
            && (metricId.Equals(CaptivityTransitionOutcomeIds.ApprovedWork)
                || metricId.Equals(CaptivityTransitionOutcomeIds.WorkBefore)
                || metricId.Equals(CaptivityTransitionOutcomeIds.WorkAfter))
        || unitId.Equals(CaptivityTransitionOutcomeIds.CountUnit)
            && (metricId.Equals(CaptivityTransitionOutcomeIds.DaysBefore)
                || metricId.Equals(CaptivityTransitionOutcomeIds.DaysAfter)
                || metricId.Equals(CaptivityTransitionOutcomeIds.FoodConsumed))
        || unitId.Equals(CaptivityTransitionOutcomeIds.PointUnit)
            && (metricId.Equals(CaptivityTransitionOutcomeIds.TrustBefore)
                || metricId.Equals(CaptivityTransitionOutcomeIds.TrustAfter)
                || metricId.Equals(CaptivityTransitionOutcomeIds.GrudgeBefore)
                || metricId.Equals(CaptivityTransitionOutcomeIds.GrudgeAfter)
                || metricId.Equals(CaptivityTransitionOutcomeIds.CorruptionBefore)
                || metricId.Equals(CaptivityTransitionOutcomeIds.CorruptionAfter))
        || unitId.Equals(CaptivityTransitionOutcomeIds.BooleanUnit)
            && metricId.Equals(CaptivityTransitionOutcomeIds.CompletedDay)
        || unitId.Equals(CaptivityTransitionOutcomeIds.RevisionUnit)
            && metricId.Equals(
                CaptivityTransitionOutcomeIds.RehabilitationRevision);
    public OutcomeValidationResult Validate(in GameplayOutcomeReadView outcome) =>
        outcome.ParticipantCount == 4 && outcome.SubjectCount == 4
        && outcome.MetricCount == 14 && outcome.TagCount == 2
        && outcome.ProvenanceCount == 1
            ? OutcomeValidationResult.Accepted
            : OutcomeValidationResult.Reject(
                "captivity-rehabilitation-shape-invalid");
}

public sealed class CaptivityReleaseOutcomeDescriptor : IGameplayOutcomeDescriptor
{
    private static readonly INarrativePerspectiveProjector Projector =
        new CaptivityTransitionPerspectiveProjector(true);
    private static readonly IOutcomeMemoryPolicy Memory =
        new SocialLifeOutcomeMemoryPolicy();
    private static readonly IOutcomePerceptionPolicy Perception =
        new SocialLifeOutcomePerceptionPolicy();
    private static readonly IOutcomeMemoryConsolidator Consolidator =
        new SocialConflictOutcomeConsolidator();
    public GameplayOutcomeTypeId OutcomeTypeId =>
        CaptivityTransitionOutcomeIds.Release;
    public INarrativePerspectiveProjector PerspectiveProjector => Projector;
    public IOutcomeMemoryPolicy MemoryPolicy => Memory;
    public IOutcomePerceptionPolicy PerceptionPolicy => Perception;
    public IOutcomeMemoryConsolidator MemoryConsolidator => Consolidator;
    public bool IsKnownRole(GameplayRoleId roleId) =>
        roleId.Equals(CaptivityTransitionOutcomeIds.CaptiveRole)
        || roleId.Equals(CaptivityTransitionOutcomeIds.DeciderRole)
        || roleId.Equals(CaptivityTransitionOutcomeIds.FacilityRole);
    public bool IsKnownMetric(GameplayMetricId metricId,
        GameplayMetricUnitId unitId) =>
        unitId.Equals(CaptivityTransitionOutcomeIds.BooleanUnit)
            && (metricId.Equals(CaptivityTransitionOutcomeIds.CustodyBefore)
                || metricId.Equals(CaptivityTransitionOutcomeIds.CustodyAfter)
                || metricId.Equals(CaptivityTransitionOutcomeIds.RestrainedBefore)
                || metricId.Equals(CaptivityTransitionOutcomeIds.RestrainedAfter)
                || metricId.Equals(CaptivityTransitionOutcomeIds.AiPausedBefore)
                || metricId.Equals(CaptivityTransitionOutcomeIds.AiPausedAfter)
                || metricId.Equals(CaptivityTransitionOutcomeIds.DoorCaptiveBefore)
                || metricId.Equals(CaptivityTransitionOutcomeIds.DoorCaptiveAfter)
                || metricId.Equals(
                    CaptivityTransitionOutcomeIds.PhysicalInputsReturned))
        || unitId.Equals(CaptivityTransitionOutcomeIds.RevisionUnit)
            && metricId.Equals(CaptivityTransitionOutcomeIds.ReleaseRevision);
    public OutcomeValidationResult Validate(in GameplayOutcomeReadView outcome) =>
        outcome.ParticipantCount is 2 or 3
        && outcome.SubjectCount == outcome.ParticipantCount
        && outcome.MetricCount == 10 && outcome.TagCount == 2
        && outcome.ProvenanceCount == 1 && outcome.FactCount == 6
            ? OutcomeValidationResult.Accepted
            : OutcomeValidationResult.Reject(
                "captivity-release-shape-invalid");
}
