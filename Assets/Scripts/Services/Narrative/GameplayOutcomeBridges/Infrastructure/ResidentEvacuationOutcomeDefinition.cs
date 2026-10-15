using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using DungeonStory.Narrative.Korean;
using VContainer;
using VContainer.Unity;
using DungeonStory.Foundation.Facts;

public static class ResidentEvacuationOutcomeIds
{
    public const string ProducerId = "invasion.resident-evacuation";
    public static readonly GameplayOutcomeTypeId Terminal =
        new("invasion.resident-evacuation-terminal");
    public static readonly GameplayEntityKindId CharacterKind =
        new("character");
    public static readonly GameplayRoleId EvacueeRole =
        new("evacuee");
    public static readonly GameplayOutcomeTagId InvasionTag =
        new("domain:invasion");
    public static readonly GameplayOutcomeTagId EvacuationTag =
        new("resident-evacuation");
    public static readonly GameplayOutcomeTagId TerminalTag =
        new("terminal");
    public static readonly GameplayOutcomeFactId RequestGroupId =
        new("evacuation.request-group-id");
    public static readonly GameplayOutcomeFactId InitiationKind =
        new("evacuation.initiation-kind");
    public static readonly GameplayOutcomeFactId ThreatKind =
        new("evacuation.threat-kind");
    public static readonly GameplayOutcomeFactId TerminalKind =
        new("evacuation.terminal-kind");
    public static readonly GameplayOutcomeFactId IntruderRuntimeIds =
        new("evacuation.intruder-runtime-ids");
    public static readonly GameplayOutcomeFactId FireCells =
        new("evacuation.fire-cells");
    public static readonly GameplayOutcomeFactId RequestedDay =
        new("evacuation.requested-day");
    public static readonly GameplayOutcomeFactId OriginCell =
        new("evacuation.origin-cell");
    public static readonly GameplayOutcomeFactId TargetCell =
        new("evacuation.target-cell");
    public static readonly GameplayOutcomeFactId FinalCell =
        new("evacuation.final-cell");
    public const string CauseEvidenceAnchor =
        "resident-evacuation-cause-sha256";

    public static readonly GameplayOutcomeFactId[] OrderedFacts =
    {
        RequestGroupId,
        InitiationKind,
        ThreatKind,
        TerminalKind,
        IntruderRuntimeIds,
        FireCells,
        RequestedDay,
        OriginCell,
        TargetCell,
        FinalCell
    };
}

public readonly struct ResidentEvacuationOutcomeReceipt
{
    public ResidentEvacuationOutcomeReceipt(
        string operationId,
        string requestGroupId,
        long ownerRevision,
        string characterId,
        string characterDisplayName,
        ResidentEvacuationInitiationKind initiationKind,
        ResidentEvacuationThreatKind threatKind,
        ResidentEvacuationTerminalKind terminalKind,
        IReadOnlyList<string> intruderRuntimeIds,
        IReadOnlyList<string> fireCells,
        int requestedDay,
        int terminalDay,
        int originX,
        int originY,
        bool hasTarget,
        int targetX,
        int targetY,
        int finalX,
        int finalY)
    {
        OperationId = new GameplayOperationId(operationId);
        RequestGroupId = GameplayOutcomeStableIdSyntax.Require(
            requestGroupId,
            nameof(requestGroupId));
        if (ownerRevision <= 0L)
            throw new ArgumentOutOfRangeException(nameof(ownerRevision));
        OwnerRevision = ownerRevision;
        CharacterId = GameplayOutcomeStableIdSyntax.Require(
            characterId,
            nameof(characterId));
        CharacterDisplayName = SnapshotName(
            CharacterId,
            characterDisplayName);
        if (!Enum.IsDefined(typeof(ResidentEvacuationInitiationKind), initiationKind)
            || !Enum.IsDefined(typeof(ResidentEvacuationThreatKind), threatKind)
            || !Enum.IsDefined(typeof(ResidentEvacuationTerminalKind), terminalKind)
            || terminalKind == ResidentEvacuationTerminalKind.None)
        {
            throw new ArgumentException(
                "Canonical resident-evacuation enum values are required.");
        }
        InitiationKind = initiationKind;
        ThreatKind = threatKind;
        TerminalKind = terminalKind;
        IntruderRuntimeIdsCanonical = CanonicalStableIds(
            intruderRuntimeIds,
            nameof(intruderRuntimeIds));
        FireCellsCanonical = CanonicalTokens(fireCells, nameof(fireCells));
        if (requestedDay < 1 || terminalDay < requestedDay)
            throw new ArgumentOutOfRangeException(nameof(terminalDay));
        RequestedDay = requestedDay;
        TerminalDay = terminalDay;
        OriginCellCanonical = Cell(originX, originY);
        TargetCellCanonical = hasTarget ? Cell(targetX, targetY) : "none";
        FinalCellCanonical = Cell(finalX, finalY);
        CauseCanonical = string.Join(
            ";",
            "resident-evacuation-cause-v1",
            RequestGroupId,
            InitiationKind.ToString(),
            ThreatKind.ToString(),
            IntruderRuntimeIdsCanonical,
            FireCellsCanonical,
            RequestedDay.ToString(CultureInfo.InvariantCulture),
            OriginCellCanonical);
        RequireBounded(IntruderRuntimeIdsCanonical, nameof(intruderRuntimeIds));
        RequireBounded(FireCellsCanonical, nameof(fireCells));
        RequireBounded(CauseCanonical, nameof(CauseCanonical));
    }

    public GameplayOperationId OperationId { get; }
    public string RequestGroupId { get; }
    public long OwnerRevision { get; }
    public string CharacterId { get; }
    public KoreanNameSnapshot CharacterDisplayName { get; }
    public ResidentEvacuationInitiationKind InitiationKind { get; }
    public ResidentEvacuationThreatKind ThreatKind { get; }
    public ResidentEvacuationTerminalKind TerminalKind { get; }
    public string IntruderRuntimeIdsCanonical { get; }
    public string FireCellsCanonical { get; }
    public int RequestedDay { get; }
    public int TerminalDay { get; }
    public string OriginCellCanonical { get; }
    public string TargetCellCanonical { get; }
    public string FinalCellCanonical { get; }
    public string CauseCanonical { get; }
    public GameplayOutcomeStatus Status => TerminalKind switch
    {
        ResidentEvacuationTerminalKind.Arrived =>
            GameplayOutcomeStatus.Succeeded,
        ResidentEvacuationTerminalKind.NoCapacity
            or ResidentEvacuationTerminalKind.Unreachable =>
            GameplayOutcomeStatus.Blocked,
        _ => GameplayOutcomeStatus.Cancelled
    };
    public GameplayResultKey ResultKey => new(
        ResidentEvacuationOutcomeIds.ProducerId,
        OperationId,
        OwnerRevision,
        0);

    private static KoreanNameSnapshot SnapshotName(
        string characterId,
        string displayName)
    {
        string display = displayName?.Trim() ?? string.Empty;
        if (display.Length == 0)
            throw new ArgumentException(
                "A resident display-name snapshot is required.",
                nameof(displayName));
        string revision = "resident-evacuation-name-v1:"
            + NarrativeInferenceHash.ComputeSha256Utf8(
                characterId + "|" + display);
        return new KoreanNameSnapshot(
            display,
            revision,
            KoreanPronunciationHint.AutoHangulDisplay(revision),
            "ko-KR");
    }

    private static string CanonicalStableIds(
        IReadOnlyList<string> values,
        string parameterName)
    {
        string[] canonical = (values ?? Array.Empty<string>())
            .Select(value => GameplayOutcomeStableIdSyntax.Require(
                value,
                parameterName))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        return canonical.Length == 0 ? "none" : string.Join(",", canonical);
    }

    private static string CanonicalTokens(
        IReadOnlyList<string> values,
        string parameterName)
    {
        string[] canonical = (values ?? Array.Empty<string>())
            .Select(value => value?.Trim() ?? string.Empty)
            .Where(value => value.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        string result = canonical.Length == 0
            ? "none"
            : string.Join("|", canonical);
        RequireBounded(result, parameterName);
        return result;
    }

    private static string Cell(int x, int y) =>
        x.ToString(CultureInfo.InvariantCulture)
        + ","
        + y.ToString(CultureInfo.InvariantCulture);

    private static void RequireBounded(string value, string parameterName)
    {
        if (!GameplayOutcomeLedger.IsValidBoundedUtf16(
                value,
                GameplayOutcomeBufferLimits.MaximumFactValueUtf16Length,
                allowEmpty: false))
        {
            throw new ArgumentException(
                "A bounded immutable evacuation fact is required.",
                parameterName);
        }
    }
}

public sealed class ResidentEvacuationOutcomeAdapter :
    GameplayOutcomeAdapter<ResidentEvacuationOutcomeReceipt>
{
    public override GameplayOutcomeTypeId OutcomeTypeId =>
        ResidentEvacuationOutcomeIds.Terminal;

    public override OutcomePrepareResult TryGetRequirements(
        in ResidentEvacuationOutcomeReceipt receipt,
        long currentWorldEpoch,
        out OutcomeWriteRequirements requirements)
    {
        requirements = new OutcomeWriteRequirements(
            receipt.ResultKey,
            OutcomeTypeId,
            receipt.TerminalDay,
            receipt.Status,
            currentWorldEpoch,
            receipt.OwnerRevision,
            participantCount: 1,
            metricCount: 0,
            subjectCount: 1,
            tagCount: 3,
            anchorCount: 1,
            provenanceCount: 2,
            factCount: 10);
        bool valid = receipt.ResultKey.IsValid
            && receipt.OwnerRevision > 0L
            && GameplayOutcomeStableIdSyntax.IsValid(receipt.CharacterId)
            && GameplayOutcomeLedger.IsValidDisplayNameSnapshot(
                receipt.CharacterDisplayName)
            && receipt.RequestedDay >= 1
            && receipt.TerminalDay >= receipt.RequestedDay
            && receipt.TerminalKind != ResidentEvacuationTerminalKind.None;
        return valid
            ? OutcomePrepareResult.Prepared()
            : new OutcomePrepareResult(
                OutcomePrepareCode.InvalidReceipt,
                "resident-evacuation-receipt-invalid");
    }

    public override OutcomePrepareResult TryWrite(
        in ResidentEvacuationOutcomeReceipt receipt,
        ref OutcomeWriteBuilder builder)
    {
        GameplayEntityId character = new(
            ResidentEvacuationOutcomeIds.CharacterKind,
            receipt.CharacterId);
        string[] values =
        {
            receipt.RequestGroupId,
            receipt.InitiationKind.ToString(),
            receipt.ThreatKind.ToString(),
            receipt.TerminalKind.ToString(),
            receipt.IntruderRuntimeIdsCanonical,
            receipt.FireCellsCanonical,
            receipt.RequestedDay.ToString(CultureInfo.InvariantCulture),
            receipt.OriginCellCanonical,
            receipt.TargetCellCanonical,
            receipt.FinalCellCanonical
        };
        bool written = builder.AddParticipant(
                new GameplayOutcomeParticipant(
                    character,
                    ResidentEvacuationOutcomeIds.EvacueeRole,
                    GameplayParticipationKind.Direct,
                    true,
                    receipt.CharacterDisplayName))
            && builder.AddSubject(new GameplayOutcomeSubjectLink(
                character,
                receipt.TerminalKind == ResidentEvacuationTerminalKind.Arrived
                    ? 0.54f
                    : 0.68f,
                NarrativeMemoryTier.Recent,
                false,
                false,
                0))
            && builder.AddTag(ResidentEvacuationOutcomeIds.InvasionTag)
            && builder.AddTag(ResidentEvacuationOutcomeIds.EvacuationTag)
            && builder.AddTag(ResidentEvacuationOutcomeIds.TerminalTag)
            && builder.AddProvenance(new GameplayOutcomeProvenanceReference(
                "runtime-receipt",
                "resident-evacuation-v1"))
            && builder.AddProvenance(new GameplayOutcomeProvenanceReference(
                "evacuation-request",
                receipt.RequestGroupId));
        for (int index = 0; written && index < values.Length; index++)
        {
            written = builder.AddFact(new GameplayOutcomeFact(
                ResidentEvacuationOutcomeIds.OrderedFacts[index],
                values[index]));
        }
        written = written
            && builder.AddAnchor(
                character,
                new NarrativeEvidenceReference(
                    ResidentEvacuationOutcomeIds.CauseEvidenceAnchor,
                    NarrativeInferenceHash.ComputeSha256Utf8(
                        receipt.CauseCanonical)));
        return written
            ? OutcomePrepareResult.Prepared()
            : new OutcomePrepareResult(
                OutcomePrepareCode.AdapterWriteFailed,
                "resident-evacuation-write-failed");
    }
}


internal sealed class ResidentEvacuationMemoryPolicy : IOutcomeMemoryPolicy
{
    public int PolicyVersion => 1;
    public GameplayMemorySignature GetSignature(
        in GameplayOutcomeReadView outcome,
        GameplayEntityId subjectId) => new(
            outcome.OutcomeTypeId.Value + ":" + subjectId.Value);
    public OutcomeMemoryEvaluation Evaluate(
        in GameplayOutcomeReadView outcome,
        GameplayEntityId subjectId,
        int priorMatchingCount,
        int evaluationDay) => new(
        outcome.Status == GameplayOutcomeStatus.Succeeded ? 0.54f : 0.68f,
        priorMatchingCount >= 3
            ? NarrativeMemoryTier.Compacted
            : NarrativeMemoryTier.Recent,
        Math.Max(evaluationDay + 2, outcome.AbsoluteDay + 2));
}

internal sealed class ResidentEvacuationPerceptionPolicy :
    IOutcomePerceptionPolicy
{
    public int MaximumOptionalWitnessLinks => 0;
    public bool ShouldCreateOptionalWitnessLink(
        in GameplayOutcomeReadView outcome,
        in GameplayOutcomeSubjectLink candidate) => false;
}

internal sealed class ResidentEvacuationConsolidator :
    IOutcomeMemoryConsolidator
{
    public bool CanCompact(
        in GameplayOutcomeReadView outcome,
        GameplayEntityId subjectId) => true;
    public bool IsAdditiveMetric(GameplayMetricId metricId) => false;
}

internal sealed class ResidentEvacuationPerspectiveProjector :
    INarrativePerspectiveProjector
{
    public NarrativeView Project(
        in GameplayOutcomeReadView outcome,
        NarrativePerspectiveContext perspective)
    {
        string name = outcome.GetParticipant(0).DisplayName.DisplayText;
        string terminal = outcome.GetFact(3).Value;
        string text = terminal switch
        {
            nameof(ResidentEvacuationTerminalKind.Arrived) =>
                name + "은 지정 대피 구역에 도착했다.",
            nameof(ResidentEvacuationTerminalKind.NoCapacity) =>
                name + "은 대피 구역의 빈 안전 칸을 배정받지 못했다.",
            nameof(ResidentEvacuationTerminalKind.Unreachable) =>
                name + "은 지정 대피 구역까지 갈 수 있는 길을 찾지 못했다.",
            nameof(ResidentEvacuationTerminalKind.ThreatEnded) =>
                "위협이 끝나 " + name + "의 대피 명령이 종료됐다.",
            nameof(ResidentEvacuationTerminalKind.ZoneCleared) =>
                "대피 구역 지정이 해제되어 " + name + "의 대피가 취소됐다.",
            nameof(ResidentEvacuationTerminalKind.ZoneLost) =>
                "방 구조가 바뀌어 " + name + "의 대피 구역이 사라졌다.",
            nameof(ResidentEvacuationTerminalKind.ReplacedByRequest) =>
                "새 대피 명령이 내려져 " + name + "의 이전 대피 경로가 교체됐다.",
            _ => name + "은 더 이상 주민 대피 대상이 아니어서 명령에서 빠졌다."
        };
        return new NarrativeView(
            outcome.OutcomeId,
            perspective.Kind,
            text,
            "resident-evacuation-v1",
            true);
    }
}

public sealed class ResidentEvacuationOutcomeDescriptor :
    IGameplayOutcomeDescriptor
{
    private static readonly INarrativePerspectiveProjector Projector =
        new ResidentEvacuationPerspectiveProjector();
    private static readonly IOutcomeMemoryPolicy Memory =
        new ResidentEvacuationMemoryPolicy();
    private static readonly IOutcomePerceptionPolicy Perception =
        new ResidentEvacuationPerceptionPolicy();
    private static readonly IOutcomeMemoryConsolidator Consolidator =
        new ResidentEvacuationConsolidator();

    public GameplayOutcomeTypeId OutcomeTypeId =>
        ResidentEvacuationOutcomeIds.Terminal;
    public INarrativePerspectiveProjector PerspectiveProjector => Projector;
    public IOutcomeMemoryPolicy MemoryPolicy => Memory;
    public IOutcomePerceptionPolicy PerceptionPolicy => Perception;
    public IOutcomeMemoryConsolidator MemoryConsolidator => Consolidator;
    public bool IsKnownRole(GameplayRoleId roleId) =>
        roleId.Equals(ResidentEvacuationOutcomeIds.EvacueeRole);
    public bool IsKnownMetric(
        GameplayMetricId metricId,
        GameplayMetricUnitId unitId) => false;

    public OutcomeValidationResult Validate(
        in GameplayOutcomeReadView outcome)
    {
        if (outcome.OutcomeTypeId != OutcomeTypeId
            || outcome.ParticipantCount != 1
            || outcome.SubjectCount != 1
            || outcome.MetricCount != 0
            || outcome.TagCount != 3
            || outcome.AnchorCount != 1
            || outcome.ProvenanceCount != 2
            || outcome.FactCount != 10
            || !outcome.GetParticipant(0).RoleId.Equals(
                ResidentEvacuationOutcomeIds.EvacueeRole))
        {
            return OutcomeValidationResult.Reject(
                "resident-evacuation-shape-invalid");
        }
        for (int index = 0;
             index < ResidentEvacuationOutcomeIds.OrderedFacts.Length;
             index++)
        {
            if (!outcome.GetFact(index).FactId.Equals(
                    ResidentEvacuationOutcomeIds.OrderedFacts[index]))
            {
                return OutcomeValidationResult.Reject(
                    "resident-evacuation-fact-order-invalid");
            }
        }
        if (!Enum.TryParse(
                outcome.GetFact(3).Value,
                false,
                out ResidentEvacuationTerminalKind terminal)
            || terminal == ResidentEvacuationTerminalKind.None
            || outcome.Status != StatusFor(terminal))
        {
            return OutcomeValidationResult.Reject(
                "resident-evacuation-terminal-status-invalid");
        }
        string cause = string.Join(
            ";",
            "resident-evacuation-cause-v1",
            outcome.GetFact(0).Value,
            outcome.GetFact(1).Value,
            outcome.GetFact(2).Value,
            outcome.GetFact(4).Value,
            outcome.GetFact(5).Value,
            outcome.GetFact(6).Value,
            outcome.GetFact(7).Value);
        NarrativeEvidenceReference anchor = outcome.GetAnchor(0);
        if (outcome.GetAnchorSubject(0)
                != outcome.GetParticipant(0).EntityId
            || anchor.AnchorTypeId
                != ResidentEvacuationOutcomeIds.CauseEvidenceAnchor
            || !string.Equals(
                anchor.AnchorId,
                NarrativeInferenceHash.ComputeSha256Utf8(cause),
                StringComparison.Ordinal))
        {
            return OutcomeValidationResult.Reject(
                "resident-evacuation-cause-anchor-invalid");
        }
        return OutcomeValidationResult.Accepted;
    }

    private static GameplayOutcomeStatus StatusFor(
        ResidentEvacuationTerminalKind terminal) => terminal switch
    {
        ResidentEvacuationTerminalKind.Arrived =>
            GameplayOutcomeStatus.Succeeded,
        ResidentEvacuationTerminalKind.NoCapacity
            or ResidentEvacuationTerminalKind.Unreachable =>
            GameplayOutcomeStatus.Blocked,
        _ => GameplayOutcomeStatus.Cancelled
    };
}

public static class ResidentEvacuationOutcomeRegistration
{
    public static void RegisterResidentEvacuationGameplayOutcomes(
        this IContainerBuilder builder)
    {
        builder.Register(resolver => ResidentEvacuationFactObserver.CreateBinding(
                resolver.Resolve<IGameplayOutcomeRegistry>(),
                resolver.Resolve<GameplayOutcomeLedger>(),
                resolver.Resolve<IGameplayOutcomeRecorder>(),
                resolver.Resolve<GameplayDomainFactCommitGroup>()), Lifetime.Singleton)
            .As<IDomainFactObserverBinding>();
        builder.RegisterEntryPoint<EvacuationActivityObserver>(Lifetime.Singleton);
        builder.RegisterGameplayActionLogs(nameof(ResidentEvacuationOutcomeAdapter));
    }
}
