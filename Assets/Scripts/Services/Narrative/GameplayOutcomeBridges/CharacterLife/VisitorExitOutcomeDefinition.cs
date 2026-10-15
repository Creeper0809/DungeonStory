using System;
using System.Collections.Generic;
using System.Globalization;
using DungeonStory.Narrative.Korean;
using VContainer;
using VContainer.Unity;

public static class VisitorExitOutcomeIds
{
    public const string TheftProducerId = "item.floor-theft";
    public const string DepartureProducerId = "character.visitor-departure";

    public static readonly GameplayOutcomeTypeId TheftCommitted =
        new("item.floor-theft-committed");
    public static readonly GameplayOutcomeTypeId DepartureTerminal =
        new("character.visitor-departure-terminal");
    public static readonly GameplayEntityKindId CharacterKind = new("character");
    public static readonly GameplayRoleId ThiefRole = new("thief");
    public static readonly GameplayRoleId DepartingVisitorRole =
        new("departing-visitor");
    public static readonly GameplayMetricId QuantityMetric = new("item.quantity");
    public static readonly GameplayMetricId MassMetric = new("item.mass-grams");
    public static readonly GameplayMetricUnitId CountUnit = new("count");
    public static readonly GameplayMetricUnitId GramsUnit = new("grams");
    public static readonly GameplayOutcomeTagId CrimeTag = new("crime");
    public static readonly GameplayOutcomeTagId PhysicalTransferTag =
        new("physical-transfer");
    public static readonly GameplayOutcomeTagId VisitorTag = new("visitor");
    public static readonly GameplayOutcomeTagId ExitTag = new("exit-dungeon");
    public static readonly GameplayOutcomeTagId TerminalTag = new("terminal");

    public static readonly GameplayOutcomeFactId SourceStackId =
        new("theft.source-stack-id");
    public static readonly GameplayOutcomeFactId CarriedStackId =
        new("theft.carried-stack-id");
    public static readonly GameplayOutcomeFactId ItemInstanceId =
        new("theft.item-instance-id");
    public static readonly GameplayOutcomeFactId ItemDefinitionId =
        new("theft.item-definition-id");
    public static readonly GameplayOutcomeFactId ComponentFingerprint =
        new("theft.component-fingerprint");
    public static readonly GameplayOutcomeFactId SourceQuantityBefore =
        new("theft.source-quantity-before");
    public static readonly GameplayOutcomeFactId SourceQuantityAfter =
        new("theft.source-quantity-after");
    public static readonly GameplayOutcomeFactId CarryQuantityBefore =
        new("theft.carry-quantity-before");
    public static readonly GameplayOutcomeFactId CarryQuantityAfter =
        new("theft.carry-quantity-after");
    public static readonly GameplayOutcomeFactId Position =
        new("theft.source-position");
    public static readonly GameplayOutcomeFactId Destination =
        new("theft.destination");

    public static readonly GameplayOutcomeFactId SourceMacro =
        new("departure.source-macro");
    public static readonly GameplayOutcomeFactId TerminalKind =
        new("departure.terminal-kind");
    public static readonly GameplayOutcomeFactId FailureReason =
        new("departure.failure-reason");
    public static readonly GameplayOutcomeFactId LifecycleBefore =
        new("departure.lifecycle-before");
    public static readonly GameplayOutcomeFactId LifecycleAfter =
        new("departure.lifecycle-after");
    public static readonly GameplayOutcomeFactId VisitingBefore =
        new("departure.visiting-before");
    public static readonly GameplayOutcomeFactId VisitingAfter =
        new("departure.visiting-after");
    public static readonly GameplayOutcomeFactId VisitCountBefore =
        new("departure.visit-count-before");
    public static readonly GameplayOutcomeFactId VisitCountAfter =
        new("departure.visit-count-after");
    public static readonly GameplayOutcomeFactId EquipmentCountBefore =
        new("departure.equipment-count-before");
    public static readonly GameplayOutcomeFactId EquipmentCountAfter =
        new("departure.equipment-count-after");
    public static readonly GameplayOutcomeFactId EquipmentRetired =
        new("departure.equipment-retired");
    public static readonly GameplayOutcomeFactId HandoffKind =
        new("departure.handoff-kind");
    public static readonly GameplayOutcomeFactId TheftChildResultKeys =
        new("departure.theft-child-result-keys");

    public static readonly GameplayOutcomeFactId[] TheftFacts =
    {
        SourceStackId,
        CarriedStackId,
        ItemInstanceId,
        ItemDefinitionId,
        ComponentFingerprint,
        SourceQuantityBefore,
        SourceQuantityAfter,
        CarryQuantityBefore,
        CarryQuantityAfter,
        Position,
        Destination
    };

    public static readonly GameplayOutcomeFactId[] DepartureFacts =
    {
        SourceMacro,
        TerminalKind,
        FailureReason,
        LifecycleBefore,
        LifecycleAfter,
        VisitingBefore,
        VisitingAfter,
        VisitCountBefore,
        VisitCountAfter,
        EquipmentCountBefore,
        EquipmentCountAfter,
        EquipmentRetired,
        HandoffKind,
        TheftChildResultKeys
    };

    public static KoreanNameSnapshot Name(string stableId, string displayName)
    {
        string display = displayName?.Trim() ?? string.Empty;
        if (display.Length == 0)
            throw new ArgumentException("A display-name snapshot is required.");
        string revision = "visitor-exit-name-v1:"
            + NarrativeInferenceHash.ComputeSha256Utf8(stableId + "|" + display);
        return new KoreanNameSnapshot(
            display,
            revision,
            KoreanPronunciationHint.AutoHangulDisplay(revision),
            "ko-KR");
    }

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

    public static bool IsExactTheftChildResultKey(
        string value,
        string departureOperationId,
        long ownerRevision)
    {
        if (string.Equals(value, "none", StringComparison.Ordinal))
            return true;
        string[] parts = (value ?? string.Empty).Split('|');
        if (parts.Length != 4
            || !long.TryParse(
                parts[2],
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out long revision)
            || !int.TryParse(
                parts[3],
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out int localIndex))
        {
            return false;
        }
        try
        {
            GameplayResultKey key = new(
                parts[0],
                new GameplayOperationId(parts[1]),
                revision,
                localIndex);
            return key.IsValid
                && string.Equals(
                    key.ProducerId,
                    TheftProducerId,
                    StringComparison.Ordinal)
                && string.Equals(
                    key.OperationId.Value,
                    departureOperationId + ":floor-theft:1",
                    StringComparison.Ordinal)
                && key.CommitRevision == ownerRevision
                && key.LocalResultIndex == 0;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }
}

public readonly struct FloorTheftOutcomeReceipt
{
    public FloorTheftOutcomeReceipt(
        string operationId,
        long ownerRevision,
        string characterId,
        string characterDisplayName,
        int absoluteDay,
        string sourceStackId,
        string carriedStackId,
        string itemInstanceId,
        string itemDefinitionId,
        string componentFingerprint,
        int sourceQuantityBefore,
        int sourceQuantityAfter,
        int carryQuantityBefore,
        int carryQuantityAfter,
        long unitMassGrams,
        int positionX,
        int positionY)
    {
        OperationId = new GameplayOperationId(operationId);
        if (ownerRevision <= 0L) throw new ArgumentOutOfRangeException(nameof(ownerRevision));
        if (absoluteDay < 0) throw new ArgumentOutOfRangeException(nameof(absoluteDay));
        if (sourceQuantityBefore <= 0
            || sourceQuantityAfter != sourceQuantityBefore - 1
            || carryQuantityBefore < 0
            || carryQuantityAfter != carryQuantityBefore + 1
            || unitMassGrams <= 0L)
        {
            throw new ArgumentException("Exact one-unit theft deltas are required.");
        }
        OwnerRevision = ownerRevision;
        CharacterId = GameplayOutcomeStableIdSyntax.Require(characterId, nameof(characterId));
        CharacterDisplayName = VisitorExitOutcomeIds.Name(CharacterId, characterDisplayName);
        AbsoluteDay = absoluteDay;
        SourceStackId = VisitorExitOutcomeIds.RequiredFact(sourceStackId, nameof(sourceStackId));
        CarriedStackId = VisitorExitOutcomeIds.RequiredFact(carriedStackId, nameof(carriedStackId));
        ItemInstanceId = string.IsNullOrWhiteSpace(itemInstanceId)
            ? "none"
            : VisitorExitOutcomeIds.RequiredFact(itemInstanceId, nameof(itemInstanceId));
        ItemDefinitionId = VisitorExitOutcomeIds.RequiredFact(itemDefinitionId, nameof(itemDefinitionId));
        ComponentFingerprint = string.IsNullOrWhiteSpace(componentFingerprint)
            ? "none"
            : VisitorExitOutcomeIds.RequiredFact(componentFingerprint, nameof(componentFingerprint));
        SourceQuantityBefore = sourceQuantityBefore;
        SourceQuantityAfter = sourceQuantityAfter;
        CarryQuantityBefore = carryQuantityBefore;
        CarryQuantityAfter = carryQuantityAfter;
        UnitMassGrams = unitMassGrams;
        PositionX = positionX;
        PositionY = positionY;
    }

    public GameplayOperationId OperationId { get; }
    public long OwnerRevision { get; }
    public string CharacterId { get; }
    public KoreanNameSnapshot CharacterDisplayName { get; }
    public int AbsoluteDay { get; }
    public string SourceStackId { get; }
    public string CarriedStackId { get; }
    public string ItemInstanceId { get; }
    public string ItemDefinitionId { get; }
    public string ComponentFingerprint { get; }
    public int SourceQuantityBefore { get; }
    public int SourceQuantityAfter { get; }
    public int CarryQuantityBefore { get; }
    public int CarryQuantityAfter { get; }
    public long UnitMassGrams { get; }
    public int PositionX { get; }
    public int PositionY { get; }
    public GameplayResultKey ResultKey => new(
        VisitorExitOutcomeIds.TheftProducerId,
        OperationId,
        OwnerRevision,
        0);
}

public readonly struct VisitorDepartureOutcomeReceipt
{
    public VisitorDepartureOutcomeReceipt(
        string operationId,
        long ownerRevision,
        string characterId,
        string characterDisplayName,
        int absoluteDay,
        string sourceMacro,
        VisitorDepartureTerminalKind terminalKind,
        string failureReason,
        CharacterLifecycleState lifecycleBefore,
        CharacterLifecycleState lifecycleAfter,
        bool visitingBefore,
        bool visitingAfter,
        int visitCountBefore,
        int visitCountAfter,
        int equipmentCountBefore,
        int equipmentCountAfter,
        bool equipmentRetired,
        string handoffKind,
        string theftChildResultKeys)
    {
        OperationId = new GameplayOperationId(operationId);
        if (ownerRevision <= 0L) throw new ArgumentOutOfRangeException(nameof(ownerRevision));
        if (absoluteDay < 0) throw new ArgumentOutOfRangeException(nameof(absoluteDay));
        if (terminalKind == VisitorDepartureTerminalKind.None
            || !Enum.IsDefined(typeof(VisitorDepartureTerminalKind), terminalKind))
        {
            throw new ArgumentException("A terminal visitor departure kind is required.");
        }
        if (visitCountBefore < 0 || visitCountAfter < 0
            || equipmentCountBefore < 0 || equipmentCountAfter < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(visitCountBefore));
        }
        OwnerRevision = ownerRevision;
        CharacterId = GameplayOutcomeStableIdSyntax.Require(characterId, nameof(characterId));
        CharacterDisplayName = VisitorExitOutcomeIds.Name(CharacterId, characterDisplayName);
        AbsoluteDay = absoluteDay;
        SourceMacro = VisitorExitOutcomeIds.RequiredFact(sourceMacro, nameof(sourceMacro));
        TerminalKind = terminalKind;
        FailureReason = string.IsNullOrWhiteSpace(failureReason)
            ? "none"
            : VisitorExitOutcomeIds.RequiredFact(failureReason, nameof(failureReason));
        LifecycleBefore = lifecycleBefore;
        LifecycleAfter = lifecycleAfter;
        VisitingBefore = visitingBefore;
        VisitingAfter = visitingAfter;
        VisitCountBefore = visitCountBefore;
        VisitCountAfter = visitCountAfter;
        EquipmentCountBefore = equipmentCountBefore;
        EquipmentCountAfter = equipmentCountAfter;
        EquipmentRetired = equipmentRetired;
        HandoffKind = VisitorExitOutcomeIds.RequiredFact(handoffKind, nameof(handoffKind));
        TheftChildResultKeys = string.IsNullOrWhiteSpace(theftChildResultKeys)
            ? "none"
            : VisitorExitOutcomeIds.RequiredFact(theftChildResultKeys, nameof(theftChildResultKeys));
        if (!VisitorExitOutcomeIds.IsExactTheftChildResultKey(
                TheftChildResultKeys,
                OperationId.Value,
                OwnerRevision))
        {
            throw new ArgumentException(
                "The theft child must be the exact result of this exit operation.",
                nameof(theftChildResultKeys));
        }
    }

    public GameplayOperationId OperationId { get; }
    public long OwnerRevision { get; }
    public string CharacterId { get; }
    public KoreanNameSnapshot CharacterDisplayName { get; }
    public int AbsoluteDay { get; }
    public string SourceMacro { get; }
    public VisitorDepartureTerminalKind TerminalKind { get; }
    public string FailureReason { get; }
    public CharacterLifecycleState LifecycleBefore { get; }
    public CharacterLifecycleState LifecycleAfter { get; }
    public bool VisitingBefore { get; }
    public bool VisitingAfter { get; }
    public int VisitCountBefore { get; }
    public int VisitCountAfter { get; }
    public int EquipmentCountBefore { get; }
    public int EquipmentCountAfter { get; }
    public bool EquipmentRetired { get; }
    public string HandoffKind { get; }
    public string TheftChildResultKeys { get; }
    public GameplayOutcomeStatus Status => TerminalKind == VisitorDepartureTerminalKind.Departed
        ? GameplayOutcomeStatus.Succeeded
        : TheftChildResultKeys == "none"
            ? TerminalKind is VisitorDepartureTerminalKind.CaptiveBlocked
                or VisitorDepartureTerminalKind.IncidentBlocked
                ? GameplayOutcomeStatus.Blocked
                : GameplayOutcomeStatus.Failed
            : GameplayOutcomeStatus.PartiallySucceeded;
    public GameplayResultKey ResultKey => new(
        VisitorExitOutcomeIds.DepartureProducerId,
        OperationId,
        OwnerRevision,
        0);
}

public sealed class FloorTheftOutcomeAdapter :
    GameplayOutcomeAdapter<FloorTheftOutcomeReceipt>
{
    public override GameplayOutcomeTypeId OutcomeTypeId =>
        VisitorExitOutcomeIds.TheftCommitted;

    public override OutcomePrepareResult TryGetRequirements(
        in FloorTheftOutcomeReceipt receipt,
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
            participantCount: 1,
            metricCount: 2,
            subjectCount: 1,
            tagCount: 2,
            anchorCount: 0,
            provenanceCount: 2,
            factCount: VisitorExitOutcomeIds.TheftFacts.Length);
        return receipt.ResultKey.IsValid
            && receipt.OwnerRevision > 0L
            && receipt.SourceQuantityAfter == receipt.SourceQuantityBefore - 1
            && receipt.CarryQuantityAfter == receipt.CarryQuantityBefore + 1
            && receipt.UnitMassGrams > 0L
                ? OutcomePrepareResult.Prepared()
                : new OutcomePrepareResult(
                    OutcomePrepareCode.InvalidReceipt,
                    "floor-theft-receipt-invalid");
    }

    public override OutcomePrepareResult TryWrite(
        in FloorTheftOutcomeReceipt receipt,
        ref OutcomeWriteBuilder builder)
    {
        GameplayEntityId character = new(
            VisitorExitOutcomeIds.CharacterKind,
            receipt.CharacterId);
        string[] values =
        {
            receipt.SourceStackId,
            receipt.CarriedStackId,
            receipt.ItemInstanceId,
            receipt.ItemDefinitionId,
            receipt.ComponentFingerprint,
            receipt.SourceQuantityBefore.ToString(CultureInfo.InvariantCulture),
            receipt.SourceQuantityAfter.ToString(CultureInfo.InvariantCulture),
            receipt.CarryQuantityBefore.ToString(CultureInfo.InvariantCulture),
            receipt.CarryQuantityAfter.ToString(CultureInfo.InvariantCulture),
            receipt.PositionX.ToString(CultureInfo.InvariantCulture) + ","
                + receipt.PositionY.ToString(CultureInfo.InvariantCulture),
            "character-carry:" + receipt.CharacterId
        };
        bool written = builder.AddParticipant(new GameplayOutcomeParticipant(
                character,
                VisitorExitOutcomeIds.ThiefRole,
                GameplayParticipationKind.Direct,
                true,
                receipt.CharacterDisplayName))
            && builder.AddSubject(new GameplayOutcomeSubjectLink(
                character,
                0.72f,
                NarrativeMemoryTier.Episodic,
                false,
                false,
                0))
            && builder.AddMetric(new GameplayOutcomeMetric(
                VisitorExitOutcomeIds.QuantityMetric,
                1d,
                VisitorExitOutcomeIds.CountUnit,
                character))
            && builder.AddMetric(new GameplayOutcomeMetric(
                VisitorExitOutcomeIds.MassMetric,
                receipt.UnitMassGrams,
                VisitorExitOutcomeIds.GramsUnit,
                character))
            && builder.AddTag(VisitorExitOutcomeIds.CrimeTag)
            && builder.AddTag(VisitorExitOutcomeIds.PhysicalTransferTag)
            && builder.AddProvenance(new GameplayOutcomeProvenanceReference(
                "physical-source-stack",
                receipt.SourceStackId))
            && builder.AddProvenance(new GameplayOutcomeProvenanceReference(
                "carried-stack",
                receipt.CarriedStackId))
            && builder.SetLocation(new GameplayLocationReference(
                "dungeon",
                string.Empty,
                receipt.PositionX,
                receipt.PositionY));
        for (int index = 0; written && index < values.Length; index++)
        {
            written = builder.AddFact(new GameplayOutcomeFact(
                VisitorExitOutcomeIds.TheftFacts[index],
                values[index]));
        }
        return written
            ? OutcomePrepareResult.Prepared()
            : new OutcomePrepareResult(
                OutcomePrepareCode.AdapterWriteFailed,
                "floor-theft-write-failed");
    }
}

public sealed class VisitorDepartureOutcomeAdapter :
    GameplayOutcomeAdapter<VisitorDepartureOutcomeReceipt>
{
    public override GameplayOutcomeTypeId OutcomeTypeId =>
        VisitorExitOutcomeIds.DepartureTerminal;

    public override OutcomePrepareResult TryGetRequirements(
        in VisitorDepartureOutcomeReceipt receipt,
        long currentWorldEpoch,
        out OutcomeWriteRequirements requirements)
    {
        requirements = new OutcomeWriteRequirements(
            receipt.ResultKey,
            OutcomeTypeId,
            receipt.AbsoluteDay,
            receipt.Status,
            currentWorldEpoch,
            receipt.OwnerRevision,
            participantCount: 1,
            metricCount: 0,
            subjectCount: 1,
            tagCount: 3,
            anchorCount: 0,
            provenanceCount: 2,
            factCount: VisitorExitOutcomeIds.DepartureFacts.Length);
        return receipt.ResultKey.IsValid
            && receipt.OwnerRevision > 0L
            && receipt.TerminalKind != VisitorDepartureTerminalKind.None
                ? OutcomePrepareResult.Prepared()
                : new OutcomePrepareResult(
                    OutcomePrepareCode.InvalidReceipt,
                    "visitor-departure-receipt-invalid");
    }

    public override OutcomePrepareResult TryWrite(
        in VisitorDepartureOutcomeReceipt receipt,
        ref OutcomeWriteBuilder builder)
    {
        GameplayEntityId character = new(
            VisitorExitOutcomeIds.CharacterKind,
            receipt.CharacterId);
        string[] values =
        {
            receipt.SourceMacro,
            receipt.TerminalKind.ToString(),
            receipt.FailureReason,
            receipt.LifecycleBefore.ToString(),
            receipt.LifecycleAfter.ToString(),
            receipt.VisitingBefore ? "true" : "false",
            receipt.VisitingAfter ? "true" : "false",
            receipt.VisitCountBefore.ToString(CultureInfo.InvariantCulture),
            receipt.VisitCountAfter.ToString(CultureInfo.InvariantCulture),
            receipt.EquipmentCountBefore.ToString(CultureInfo.InvariantCulture),
            receipt.EquipmentCountAfter.ToString(CultureInfo.InvariantCulture),
            receipt.EquipmentRetired ? "true" : "false",
            receipt.HandoffKind,
            receipt.TheftChildResultKeys
        };
        bool written = builder.AddParticipant(new GameplayOutcomeParticipant(
                character,
                VisitorExitOutcomeIds.DepartingVisitorRole,
                GameplayParticipationKind.Direct,
                true,
                receipt.CharacterDisplayName))
            && builder.AddSubject(new GameplayOutcomeSubjectLink(
                character,
                receipt.Status == GameplayOutcomeStatus.Succeeded ? 0.42f : 0.58f,
                NarrativeMemoryTier.Recent,
                false,
                false,
                0))
            && builder.AddTag(VisitorExitOutcomeIds.VisitorTag)
            && builder.AddTag(VisitorExitOutcomeIds.ExitTag)
            && builder.AddTag(VisitorExitOutcomeIds.TerminalTag)
            && builder.AddProvenance(new GameplayOutcomeProvenanceReference(
                "exit-operation",
                receipt.OperationId.Value))
            && builder.AddProvenance(new GameplayOutcomeProvenanceReference(
                "exit-terminal-kind",
                receipt.TerminalKind.ToString()));
        for (int index = 0; written && index < values.Length; index++)
        {
            written = builder.AddFact(new GameplayOutcomeFact(
                VisitorExitOutcomeIds.DepartureFacts[index],
                values[index]));
        }
        return written
            ? OutcomePrepareResult.Prepared()
            : new OutcomePrepareResult(
                OutcomePrepareCode.AdapterWriteFailed,
                "visitor-departure-write-failed");
    }
}

internal sealed class VisitorExitMemoryPolicy : IOutcomeMemoryPolicy
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
        0.48f,
        NarrativeMemoryTier.Recent,
        Math.Max(evaluationDay + 7, outcome.AbsoluteDay + 7));
}

internal sealed class VisitorExitPerceptionPolicy : IOutcomePerceptionPolicy
{
    public int MaximumOptionalWitnessLinks => 0;
    public bool ShouldCreateOptionalWitnessLink(
        in GameplayOutcomeReadView outcome,
        in GameplayOutcomeSubjectLink candidate) => false;
}

internal sealed class VisitorExitConsolidator : IOutcomeMemoryConsolidator
{
    public bool CanCompact(
        in GameplayOutcomeReadView outcome,
        GameplayEntityId subjectId) => false;
    public bool IsAdditiveMetric(GameplayMetricId metricId) => false;
}

internal sealed class VisitorExitPerspectiveProjector :
    INarrativePerspectiveProjector
{
    public NarrativeView Project(
        in GameplayOutcomeReadView outcome,
        NarrativePerspectiveContext perspective)
    {
        string character = outcome.ParticipantCount == 0
            ? "알 수 없는 방문객"
            : outcome.GetParticipant(0).DisplayName.DisplayText;
        string text = outcome.OutcomeTypeId == VisitorExitOutcomeIds.TheftCommitted
            ? character + "이 바닥 물건 한 개를 가져갔다."
            : character + "의 방문 종료가 " + outcome.Status + " 상태로 확정되었다.";
        return new NarrativeView(
            outcome.OutcomeId,
            perspective.Kind,
            text,
            "visitor-exit-v1",
            true);
    }
}

public abstract class VisitorExitOutcomeDescriptorBase :
    IGameplayOutcomeDescriptor
{
    private static readonly INarrativePerspectiveProjector Projector =
        new VisitorExitPerspectiveProjector();
    private static readonly IOutcomeMemoryPolicy Memory =
        new VisitorExitMemoryPolicy();
    private static readonly IOutcomePerceptionPolicy Perception =
        new VisitorExitPerceptionPolicy();
    private static readonly IOutcomeMemoryConsolidator Consolidator =
        new VisitorExitConsolidator();

    public abstract GameplayOutcomeTypeId OutcomeTypeId { get; }
    protected abstract GameplayRoleId RoleId { get; }
    protected abstract int MetricCount { get; }
    protected abstract int TagCount { get; }
    protected abstract int FactCount { get; }
    public INarrativePerspectiveProjector PerspectiveProjector => Projector;
    public IOutcomeMemoryPolicy MemoryPolicy => Memory;
    public IOutcomePerceptionPolicy PerceptionPolicy => Perception;
    public IOutcomeMemoryConsolidator MemoryConsolidator => Consolidator;
    public bool IsKnownRole(GameplayRoleId roleId) => roleId.Equals(RoleId);
    public virtual bool IsKnownMetric(
        GameplayMetricId metricId,
        GameplayMetricUnitId unitId) => false;
    public virtual OutcomeValidationResult Validate(
        in GameplayOutcomeReadView outcome) =>
        outcome.OutcomeTypeId == OutcomeTypeId
        && outcome.ParticipantCount == 1
        && outcome.SubjectCount == 1
        && outcome.MetricCount == MetricCount
        && outcome.TagCount == TagCount
        && outcome.ProvenanceCount == 2
        && outcome.FactCount == FactCount
        && outcome.GetParticipant(0).RoleId.Equals(RoleId)
            ? OutcomeValidationResult.Accepted
            : OutcomeValidationResult.Reject("visitor-exit-outcome-shape-invalid");
}

public sealed class FloorTheftOutcomeDescriptor :
    VisitorExitOutcomeDescriptorBase
{
    public override GameplayOutcomeTypeId OutcomeTypeId =>
        VisitorExitOutcomeIds.TheftCommitted;
    protected override GameplayRoleId RoleId => VisitorExitOutcomeIds.ThiefRole;
    protected override int MetricCount => 2;
    protected override int TagCount => 2;
    protected override int FactCount => VisitorExitOutcomeIds.TheftFacts.Length;
    public override bool IsKnownMetric(
        GameplayMetricId metricId,
        GameplayMetricUnitId unitId) =>
        metricId.Equals(VisitorExitOutcomeIds.QuantityMetric)
            && unitId.Equals(VisitorExitOutcomeIds.CountUnit)
        || metricId.Equals(VisitorExitOutcomeIds.MassMetric)
            && unitId.Equals(VisitorExitOutcomeIds.GramsUnit);
    public override OutcomeValidationResult Validate(
        in GameplayOutcomeReadView outcome)
    {
        OutcomeValidationResult shape = base.Validate(outcome);
        if (!shape.Valid || outcome.Status != GameplayOutcomeStatus.Succeeded)
            return OutcomeValidationResult.Reject("floor-theft-shape-invalid");
        for (int index = 0; index < VisitorExitOutcomeIds.TheftFacts.Length; index++)
        {
            if (!outcome.GetFact(index).FactId.Equals(VisitorExitOutcomeIds.TheftFacts[index]))
                return OutcomeValidationResult.Reject("floor-theft-fact-order-invalid");
        }
        return OutcomeValidationResult.Accepted;
    }
}

public sealed class VisitorDepartureOutcomeDescriptor :
    VisitorExitOutcomeDescriptorBase
{
    public override GameplayOutcomeTypeId OutcomeTypeId =>
        VisitorExitOutcomeIds.DepartureTerminal;
    protected override GameplayRoleId RoleId =>
        VisitorExitOutcomeIds.DepartingVisitorRole;
    protected override int MetricCount => 0;
    protected override int TagCount => 3;
    protected override int FactCount => VisitorExitOutcomeIds.DepartureFacts.Length;
    public override OutcomeValidationResult Validate(
        in GameplayOutcomeReadView outcome)
    {
        OutcomeValidationResult shape = base.Validate(outcome);
        if (!shape.Valid) return shape;
        for (int index = 0; index < VisitorExitOutcomeIds.DepartureFacts.Length; index++)
        {
            if (!outcome.GetFact(index).FactId.Equals(VisitorExitOutcomeIds.DepartureFacts[index]))
                return OutcomeValidationResult.Reject("visitor-departure-fact-order-invalid");
        }
        if (!Enum.TryParse(
                outcome.GetFact(1).Value,
                false,
                out VisitorDepartureTerminalKind terminal)
            || terminal == VisitorDepartureTerminalKind.None)
        {
            return OutcomeValidationResult.Reject(
                "visitor-departure-terminal-invalid");
        }
        string theftChildren = outcome.GetFact(13).Value;
        if (!VisitorExitOutcomeIds.IsExactTheftChildResultKey(
                theftChildren,
                outcome.OperationId.Value,
                outcome.OwnerRevision))
        {
            return OutcomeValidationResult.Reject(
                "visitor-departure-theft-child-invalid");
        }
        GameplayOutcomeStatus expectedStatus = terminal
            == VisitorDepartureTerminalKind.Departed
                ? GameplayOutcomeStatus.Succeeded
                : string.Equals(theftChildren, "none", StringComparison.Ordinal)
                    ? terminal is VisitorDepartureTerminalKind.CaptiveBlocked
                        or VisitorDepartureTerminalKind.IncidentBlocked
                            ? GameplayOutcomeStatus.Blocked
                            : GameplayOutcomeStatus.Failed
                    : GameplayOutcomeStatus.PartiallySucceeded;
        if (outcome.Status != expectedStatus)
        {
            return OutcomeValidationResult.Reject(
                "visitor-departure-status-terminal-mismatch");
        }
        return OutcomeValidationResult.Accepted;
    }
}

public static class VisitorExitOutcomeRegistration
{
    public static void RegisterVisitorExitGameplayOutcomes(
        this IContainerBuilder builder)
    {
        builder.Register<VisitorExitFactPreflight>(Lifetime.Singleton)
            .As<IDungeonSavePreflightValidator>()
            .As<IDungeonCapturedSavePreflightValidator>();
        builder.Register(resolver => FloorItemStolenFactMapping.CreateBinding(
                resolver.Resolve<IGameplayOutcomeRegistry>(),
                resolver.Resolve<GameplayOutcomeLedger>(),
                resolver.Resolve<IGameplayOutcomeRecorder>(),
                resolver.Resolve<GameplayDomainFactCommitGroup>()),
                Lifetime.Singleton)
            .As<DungeonStory.Foundation.Facts.IDomainFactObserverBinding>();
        builder.Register(resolver => VisitorDepartureFactMapping.CreateBinding(
                resolver.Resolve<IGameplayOutcomeRegistry>(),
                resolver.Resolve<GameplayOutcomeLedger>(),
                resolver.Resolve<IGameplayOutcomeRecorder>(),
                resolver.Resolve<GameplayDomainFactCommitGroup>()),
                Lifetime.Singleton)
            .As<DungeonStory.Foundation.Facts.IDomainFactObserverBinding>();
        builder.RegisterEntryPoint<VisitorDepartureActivityObserver>(
            Lifetime.Singleton);
        builder.RegisterGameplayActionLogs(nameof(FloorTheftOutcomeAdapter));
        builder.RegisterGameplayActionLogs(nameof(VisitorDepartureOutcomeAdapter));
    }
}
