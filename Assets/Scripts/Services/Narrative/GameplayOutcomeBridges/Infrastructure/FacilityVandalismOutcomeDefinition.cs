using System;
using System.Collections.Generic;
using System.Globalization;
using DungeonStory.Narrative.Korean;
using VContainer;

public static class FacilityVandalismOutcomeIds
{
    public const string ProducerId = "infrastructure.facility-vandalism";

    public static readonly GameplayOutcomeTypeId FacilityDamaged =
        new("infrastructure.facility-vandalized");
    public static readonly GameplayEntityKindId CharacterKind =
        new("character");
    public static readonly GameplayEntityKindId FacilityKind =
        new("facility");
    public static readonly GameplayRoleId VandalRole = new("vandal");
    public static readonly GameplayRoleId DamagedFacilityRole =
        new("damaged-facility");
    public static readonly GameplayMetricId DamageBeforeMetric =
        new("facility-vandalism.damage-before");
    public static readonly GameplayMetricId DamageAfterMetric =
        new("facility-vandalism.damage-after");
    public static readonly GameplayMetricUnitId StateUnit = new("state");
    public static readonly GameplayOutcomeTagId InfrastructureTag =
        new("domain:infrastructure");
    public static readonly GameplayOutcomeTagId VandalismTag =
        new("facility-vandalism");
    public static readonly GameplayOutcomeTagId DamageTag =
        new("facility-damage");

    public static readonly GameplayOutcomeFactId SourceFact =
        new("vandalism.source");
    public static readonly GameplayOutcomeFactId ReasonFact =
        new("vandalism.reason");
    public static readonly GameplayOutcomeFactId DefinitionIdFact =
        new("vandalism.building-definition-id");
    public static readonly GameplayOutcomeFactId DamageBeforeFact =
        new("vandalism.damage-before");
    public static readonly GameplayOutcomeFactId DamageAfterFact =
        new("vandalism.damage-after");
    public static readonly GameplayOutcomeFactId StructuralBeforeFact =
        new("vandalism.structural-hit-points-before");
    public static readonly GameplayOutcomeFactId StructuralAfterFact =
        new("vandalism.structural-hit-points-after");
    public static readonly GameplayOutcomeFactId WitnessMoodChildrenFact =
        new("vandalism.witness-mood-child-result-keys");

    public static readonly GameplayOutcomeFactId[] Facts =
    {
        SourceFact,
        ReasonFact,
        DefinitionIdFact,
        DamageBeforeFact,
        DamageAfterFact,
        StructuralBeforeFact,
        StructuralAfterFact,
        WitnessMoodChildrenFact
    };

    public static string OperationId(BuildingInstanceId facilityId, long sequence)
    {
        if (!facilityId.IsValid)
            throw new ArgumentException("A valid facility ID is required.", nameof(facilityId));
        if (sequence <= 0L)
            throw new ArgumentOutOfRangeException(nameof(sequence));
        return "facility-vandalism:"
            + facilityId.Value + ":"
            + sequence.ToString(CultureInfo.InvariantCulture);
    }

    public static GameplayResultKey ResultKey(
        BuildingInstanceId facilityId,
        long sequence) => new(
        ProducerId,
        new GameplayOperationId(OperationId(facilityId, sequence)),
        sequence,
        0);

    public static KoreanNameSnapshot Name(string stableId, string displayName)
    {
        string identity = GameplayOutcomeStableIdSyntax.Require(
            stableId,
            nameof(stableId));
        string display = displayName?.Trim() ?? string.Empty;
        if (display.Length == 0)
            throw new ArgumentException("A display-name snapshot is required.", nameof(displayName));
        string revision = "facility-vandalism-name-v1:"
            + NarrativeInferenceHash.ComputeSha256Utf8(identity + "|" + display);
        return new KoreanNameSnapshot(
            display,
            revision,
            KoreanPronunciationHint.AutoHangulDisplay(revision),
            "ko-KR");
    }

    public static string Source(FacilityVandalismSourceKind source) => source switch
    {
        FacilityVandalismSourceKind.Macro => "macro",
        FacilityVandalismSourceKind.Deprivation => "deprivation",
        _ => throw new ArgumentOutOfRangeException(nameof(source))
    };

    public static string RequiredFact(string value, string parameterName)
    {
        string normalized = value?.Trim() ?? string.Empty;
        if (!GameplayOutcomeLedger.IsValidBoundedUtf16(
                normalized,
                GameplayOutcomeBufferLimits.MaximumFactValueUtf16Length,
                allowEmpty: false))
        {
            throw new ArgumentException(
                "A bounded immutable outcome fact is required.",
                parameterName);
        }
        return normalized;
    }

    public static bool IsCanonicalChildResultKey(string value)
    {
        string[] parts = (value ?? string.Empty).Split('|');
        if (parts.Length != 4
            || !GameplayOutcomeStableIdSyntax.IsValid(parts[0])
            || !GameplayOutcomeStableIdSyntax.IsValid(parts[1])
            || !long.TryParse(
                parts[2],
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out long revision)
            || revision <= 0L
            || !int.TryParse(
                parts[3],
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out int localIndex)
            || localIndex < 0)
        {
            return false;
        }
        return true;
    }
}

public readonly struct FacilityVandalismOutcomeReceipt
{
    public FacilityVandalismOutcomeReceipt(
        BuildingInstanceId facilityId,
        long ownerRevision,
        string actorId,
        string actorDisplayName,
        string facilityDisplayName,
        string buildingDefinitionId,
        FacilityVandalismSourceKind source,
        string reason,
        int absoluteDay,
        int positionX,
        int positionY,
        bool damagedBefore,
        bool damagedAfter,
        bool hasStructuralIntegrity,
        float structuralHitPointsBefore,
        float structuralHitPointsAfter,
        IReadOnlyList<string> witnessMoodChildResultKeys)
    {
        if (!facilityId.IsValid)
            throw new ArgumentException("A valid facility ID is required.", nameof(facilityId));
        if (ownerRevision <= 0L)
            throw new ArgumentOutOfRangeException(nameof(ownerRevision));
        if (absoluteDay < 1)
            throw new ArgumentOutOfRangeException(nameof(absoluteDay));
        if (damagedBefore || !damagedAfter)
            throw new ArgumentException("Vandalism must describe an undamaged-to-damaged transition.");
        bool structuralValid = !hasStructuralIntegrity
            ? Math.Abs(structuralHitPointsBefore) <= float.Epsilon
                && Math.Abs(structuralHitPointsAfter) <= float.Epsilon
            : IsFinite(structuralHitPointsBefore)
                && IsFinite(structuralHitPointsAfter)
                && structuralHitPointsBefore >= 0f
                && structuralHitPointsAfter >= 0f;
        if (!structuralValid)
            throw new ArgumentException("Structural snapshots are invalid.");

        FacilityId = facilityId;
        OwnerRevision = ownerRevision;
        OperationId = new GameplayOperationId(
            FacilityVandalismOutcomeIds.OperationId(facilityId, ownerRevision));
        ActorId = GameplayOutcomeStableIdSyntax.Require(actorId, nameof(actorId));
        ActorDisplayName = FacilityVandalismOutcomeIds.Name(
            ActorId,
            actorDisplayName);
        FacilityDisplayName = FacilityVandalismOutcomeIds.Name(
            facilityId.Value,
            facilityDisplayName);
        BuildingDefinitionId = GameplayOutcomeStableIdSyntax.Require(
            buildingDefinitionId,
            nameof(buildingDefinitionId));
        Source = source;
        SourceValue = FacilityVandalismOutcomeIds.Source(source);
        Reason = FacilityVandalismOutcomeIds.RequiredFact(reason, nameof(reason));
        AbsoluteDay = absoluteDay;
        PositionX = positionX;
        PositionY = positionY;
        DamagedBefore = damagedBefore;
        DamagedAfter = damagedAfter;
        HasStructuralIntegrity = hasStructuralIntegrity;
        StructuralHitPointsBefore = structuralHitPointsBefore;
        StructuralHitPointsAfter = structuralHitPointsAfter;

        var children = new List<string>();
        string previous = null;
        foreach (string child in witnessMoodChildResultKeys
                 ?? Array.Empty<string>())
        {
            string normalized = FacilityVandalismOutcomeIds.RequiredFact(
                child,
                nameof(witnessMoodChildResultKeys));
            if (!FacilityVandalismOutcomeIds.IsCanonicalChildResultKey(normalized)
                || previous != null
                    && string.CompareOrdinal(previous, normalized) >= 0)
            {
                throw new ArgumentException(
                    "Witness child result keys must be canonical, ordered and unique.",
                    nameof(witnessMoodChildResultKeys));
            }
            children.Add(normalized);
            previous = normalized;
        }
        WitnessMoodChildResultKeys = children;
    }

    public GameplayOperationId OperationId { get; }
    public BuildingInstanceId FacilityId { get; }
    public long OwnerRevision { get; }
    public string ActorId { get; }
    public KoreanNameSnapshot ActorDisplayName { get; }
    public KoreanNameSnapshot FacilityDisplayName { get; }
    public string BuildingDefinitionId { get; }
    public FacilityVandalismSourceKind Source { get; }
    public string SourceValue { get; }
    public string Reason { get; }
    public int AbsoluteDay { get; }
    public int PositionX { get; }
    public int PositionY { get; }
    public bool DamagedBefore { get; }
    public bool DamagedAfter { get; }
    public bool HasStructuralIntegrity { get; }
    public float StructuralHitPointsBefore { get; }
    public float StructuralHitPointsAfter { get; }
    public IReadOnlyList<string> WitnessMoodChildResultKeys { get; }
    public GameplayResultKey ResultKey => new(
        FacilityVandalismOutcomeIds.ProducerId,
        OperationId,
        OwnerRevision,
        0);

    private static bool IsFinite(float value) =>
        !float.IsNaN(value) && !float.IsInfinity(value);
}

public sealed class FacilityVandalismOutcomeAdapter :
    GameplayOutcomeAdapter<FacilityVandalismOutcomeReceipt>
{
    public override GameplayOutcomeTypeId OutcomeTypeId =>
        FacilityVandalismOutcomeIds.FacilityDamaged;

    public override OutcomePrepareResult TryGetRequirements(
        in FacilityVandalismOutcomeReceipt receipt,
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
            participantCount: 2,
            metricCount: 2,
            subjectCount: 2,
            tagCount: 3,
            anchorCount: 0,
            provenanceCount: 2,
            factCount: FacilityVandalismOutcomeIds.Facts.Length);
        return receipt.ResultKey.IsValid
            && receipt.OwnerRevision > 0L
            && !receipt.DamagedBefore
            && receipt.DamagedAfter
                ? OutcomePrepareResult.Prepared()
                : new OutcomePrepareResult(
                    OutcomePrepareCode.InvalidReceipt,
                    "facility-vandalism-receipt-invalid");
    }

    public override OutcomePrepareResult TryWrite(
        in FacilityVandalismOutcomeReceipt receipt,
        ref OutcomeWriteBuilder builder)
    {
        GameplayEntityId actor = new(
            FacilityVandalismOutcomeIds.CharacterKind,
            receipt.ActorId);
        GameplayEntityId facility = new(
            FacilityVandalismOutcomeIds.FacilityKind,
            receipt.FacilityId.Value);
        string structuralBefore = receipt.HasStructuralIntegrity
            ? receipt.StructuralHitPointsBefore.ToString(
                "R",
                CultureInfo.InvariantCulture)
            : "none";
        string structuralAfter = receipt.HasStructuralIntegrity
            ? receipt.StructuralHitPointsAfter.ToString(
                "R",
                CultureInfo.InvariantCulture)
            : "none";
        string childKeys = receipt.WitnessMoodChildResultKeys.Count == 0
            ? "none"
            : string.Join(";", receipt.WitnessMoodChildResultKeys);
        string[] values =
        {
            receipt.SourceValue,
            receipt.Reason,
            receipt.BuildingDefinitionId,
            "false",
            "true",
            structuralBefore,
            structuralAfter,
            childKeys
        };

        bool written = builder.AddParticipant(new GameplayOutcomeParticipant(
                actor,
                FacilityVandalismOutcomeIds.VandalRole,
                GameplayParticipationKind.Direct,
                true,
                receipt.ActorDisplayName))
            && builder.AddParticipant(new GameplayOutcomeParticipant(
                facility,
                FacilityVandalismOutcomeIds.DamagedFacilityRole,
                GameplayParticipationKind.Direct,
                false,
                receipt.FacilityDisplayName))
            && builder.AddSubject(new GameplayOutcomeSubjectLink(
                actor,
                0.7f,
                NarrativeMemoryTier.Episodic,
                false,
                false,
                0))
            && builder.AddSubject(new GameplayOutcomeSubjectLink(
                facility,
                0.68f,
                NarrativeMemoryTier.Recent,
                false,
                false,
                0))
            && builder.AddMetric(new GameplayOutcomeMetric(
                FacilityVandalismOutcomeIds.DamageBeforeMetric,
                0d,
                FacilityVandalismOutcomeIds.StateUnit,
                facility))
            && builder.AddMetric(new GameplayOutcomeMetric(
                FacilityVandalismOutcomeIds.DamageAfterMetric,
                1d,
                FacilityVandalismOutcomeIds.StateUnit,
                facility))
            && builder.AddTag(FacilityVandalismOutcomeIds.InfrastructureTag)
            && builder.AddTag(FacilityVandalismOutcomeIds.VandalismTag)
            && builder.AddTag(FacilityVandalismOutcomeIds.DamageTag)
            && builder.AddProvenance(new GameplayOutcomeProvenanceReference(
                "runtime-receipt",
                "facility-vandalism-v1"))
            && builder.AddProvenance(new GameplayOutcomeProvenanceReference(
                "domain-commit",
                receipt.OperationId.Value))
            && builder.SetLocation(new GameplayLocationReference(
                "dungeon",
                string.Empty,
                receipt.PositionX,
                receipt.PositionY));
        for (int index = 0; written && index < values.Length; index++)
        {
            written = builder.AddFact(new GameplayOutcomeFact(
                FacilityVandalismOutcomeIds.Facts[index],
                values[index]));
        }
        return written
            ? OutcomePrepareResult.Prepared()
            : new OutcomePrepareResult(
                OutcomePrepareCode.AdapterWriteFailed,
                "facility-vandalism-write-failed");
    }
}

internal sealed class FacilityVandalismMemoryPolicy : IOutcomeMemoryPolicy
{
    public int PolicyVersion => 1;
    public GameplayMemorySignature GetSignature(
        in GameplayOutcomeReadView outcome,
        GameplayEntityId subjectId) => new(
        outcome.OutcomeTypeId.Value + ":" + subjectId.Kind.Value);

    public OutcomeMemoryEvaluation Evaluate(
        in GameplayOutcomeReadView outcome,
        GameplayEntityId subjectId,
        int priorMatchingCount,
        int evaluationDay)
    {
        int age = Math.Max(0, evaluationDay - outcome.AbsoluteDay);
        float salience = Math.Clamp(
            0.72f - Math.Min(0.2f, age * 0.006f),
            0f,
            1f);
        return new OutcomeMemoryEvaluation(
            salience,
            NarrativeMemoryTier.Episodic,
            Math.Max(evaluationDay + 5, outcome.AbsoluteDay + 5));
    }
}

internal sealed class FacilityVandalismPerceptionPolicy :
    IOutcomePerceptionPolicy
{
    public int MaximumOptionalWitnessLinks => 0;
    public bool ShouldCreateOptionalWitnessLink(
        in GameplayOutcomeReadView outcome,
        in GameplayOutcomeSubjectLink candidate) => false;
}

internal sealed class FacilityVandalismConsolidator :
    IOutcomeMemoryConsolidator
{
    public bool CanCompact(
        in GameplayOutcomeReadView outcome,
        GameplayEntityId subjectId) => false;
    public bool IsAdditiveMetric(GameplayMetricId metricId) => false;
}

internal sealed class FacilityVandalismPerspectiveProjector :
    INarrativePerspectiveProjector
{
    private const string RendererVersion = "facility-vandalism-v1";
    private readonly IKoreanJosaFormatter josa;

    public FacilityVandalismPerspectiveProjector(IKoreanJosaFormatter josa) =>
        this.josa = josa ?? throw new ArgumentNullException(nameof(josa));

    public NarrativeView Project(
        in GameplayOutcomeReadView outcome,
        NarrativePerspectiveContext perspective)
    {
        GameplayOutcomeParticipant actor = outcome.GetParticipant(0);
        GameplayOutcomeParticipant facility = outcome.GetParticipant(1);
        KoreanJosaFormatResult actorSubject = josa.Format(
            new KoreanJosaRequest(actor.DisplayName, KoreanJosaKind.Subject));
        KoreanJosaFormatResult facilityObject = josa.Format(
            new KoreanJosaRequest(facility.DisplayName, KoreanJosaKind.Object));
        bool neutral = actorSubject.RequiresNeutralFrame
            || facilityObject.RequiresNeutralFrame;
        string text = neutral
            ? "시설 파손: " + actor.DisplayName.DisplayText + "의 행동으로 "
                + facility.DisplayName.DisplayText + "이 손상되었다."
            : actorSubject.Text + " " + facilityObject.Text + " 파손했다.";
        return new NarrativeView(
            outcome.OutcomeId,
            perspective.Kind,
            text,
            RendererVersion,
            neutral);
    }
}

public sealed class FacilityVandalismOutcomeDescriptor :
    IGameplayOutcomeDescriptor
{
    private static readonly IOutcomeMemoryPolicy Memory =
        new FacilityVandalismMemoryPolicy();
    private static readonly IOutcomePerceptionPolicy Perception =
        new FacilityVandalismPerceptionPolicy();
    private static readonly IOutcomeMemoryConsolidator Consolidator =
        new FacilityVandalismConsolidator();
    private readonly INarrativePerspectiveProjector projector;

    public FacilityVandalismOutcomeDescriptor(IKoreanJosaFormatter josa) =>
        projector = new FacilityVandalismPerspectiveProjector(josa);

    public GameplayOutcomeTypeId OutcomeTypeId =>
        FacilityVandalismOutcomeIds.FacilityDamaged;
    public INarrativePerspectiveProjector PerspectiveProjector => projector;
    public IOutcomeMemoryPolicy MemoryPolicy => Memory;
    public IOutcomePerceptionPolicy PerceptionPolicy => Perception;
    public IOutcomeMemoryConsolidator MemoryConsolidator => Consolidator;

    public bool IsKnownRole(GameplayRoleId roleId) =>
        roleId.Equals(FacilityVandalismOutcomeIds.VandalRole)
        || roleId.Equals(FacilityVandalismOutcomeIds.DamagedFacilityRole);

    public bool IsKnownMetric(
        GameplayMetricId metricId,
        GameplayMetricUnitId unitId) =>
        unitId.Equals(FacilityVandalismOutcomeIds.StateUnit)
        && (metricId.Equals(FacilityVandalismOutcomeIds.DamageBeforeMetric)
            || metricId.Equals(FacilityVandalismOutcomeIds.DamageAfterMetric));

    public OutcomeValidationResult Validate(in GameplayOutcomeReadView outcome)
    {
        bool shape = outcome.OutcomeTypeId == OutcomeTypeId
            && outcome.ParticipantCount == 2
            && outcome.SubjectCount == 2
            && outcome.MetricCount == 2
            && outcome.TagCount == 3
            && outcome.ProvenanceCount == 2
            && outcome.FactCount == FacilityVandalismOutcomeIds.Facts.Length
            && outcome.GetParticipant(0).RoleId.Equals(
                FacilityVandalismOutcomeIds.VandalRole)
            && outcome.GetParticipant(1).RoleId.Equals(
                FacilityVandalismOutcomeIds.DamagedFacilityRole);
        if (!shape)
            return OutcomeValidationResult.Reject(
                "facility-vandalism-shape-invalid");
        for (int index = 0; index < FacilityVandalismOutcomeIds.Facts.Length; index++)
        {
            if (!outcome.GetFact(index).FactId.Equals(
                    FacilityVandalismOutcomeIds.Facts[index]))
            {
                return OutcomeValidationResult.Reject(
                    "facility-vandalism-fact-order-invalid");
            }
        }
        return OutcomeValidationResult.Accepted;
    }
}

public static class FacilityVandalismOutcomeRegistration
{
    public static void RegisterFacilityVandalismGameplayOutcomes(
        this IContainerBuilder builder)
    {
        builder.RegisterGameplayDomainFact<FacilityVandalismFact, FacilityVandalismOutcomeReceipt>(
            "log:infrastructure.facility-vandalized", FacilityVandalismFactMapping.Capture);
        builder.Register<FacilityVandalismFactPreflight>(Lifetime.Singleton)
            .As<IDungeonSavePreflightValidator>().As<IDungeonCapturedSavePreflightValidator>();
        builder.RegisterGameplayActionLogs(nameof(FacilityVandalismOutcomeAdapter));
    }
}
