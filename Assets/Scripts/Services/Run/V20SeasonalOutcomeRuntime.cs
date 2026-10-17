using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation.Facts;
using static V20CampaignSaveCodec;

/// <summary>Owns frozen seasonal wildlife/cargo fulfillment and terminal observation retries.</summary>
internal sealed class V20SeasonalOutcomeRuntime
{
    private readonly DungeonRuntimeAggregateRootStore rootStore;
    private readonly V20StoryContentCatalog catalog;
    private readonly IPhysicalItemBatchDispositionService physicalDispositions;
    private readonly IDomainFactPublisher terminalObservations;
    private readonly IDomainFactRecoveryPublisher terminalRecovery;
    private bool publishingSeasonalObservation;

    internal V20SeasonalOutcomeRuntime(DungeonRuntimeAggregateRootStore rootStore,
        V20StoryContentCatalog catalog, IPhysicalItemBatchDispositionService physicalDispositions,
        IDomainFactPublisher terminalObservations, IDomainFactRecoveryPublisher terminalRecovery)
    {
        this.rootStore = rootStore ?? throw new ArgumentNullException(nameof(rootStore));
        this.catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        this.physicalDispositions = physicalDispositions;
        this.terminalObservations = terminalObservations;
        this.terminalRecovery = terminalRecovery;
    }

    internal void AssertIdle()
    {
        if (publishingSeasonalObservation)
            throw new InvalidOperationException("seasonal-terminal-observation-in-progress");
    }

    private SeasonalEventAggregateState WritableSeasonal
    {
        get
        {
            AssertIdle();
            return rootStore.GetOrCreateWritable(() => new SeasonalEventAggregateState(),
                value => new SeasonalEventAggregateState { Data = Clone(value.Data) });
        }
    }

    internal bool TryRecordSeasonalWildlifeArrivalPlan(
        string occurrenceInstanceId,
        IReadOnlyList<SeasonalWildlifeArrivalMemberSaveData> members,
        out string failureReason)
    {
        failureReason = string.Empty;
        V20ActiveEventSaveData occurrence = WritableSeasonal.Data.activeEvents
            .SingleOrDefault(value => value != null
                && string.Equals(
                    value.instanceId,
                    occurrenceInstanceId,
                    StringComparison.Ordinal));
        if (occurrence?.seasonalWildlifeArrival?.configured != true)
        {
            failureReason = "활성 계절 방문 occurrence를 찾지 못했습니다.";
            return false;
        }

        SeasonalWildlifeArrivalSaveData arrival =
            occurrence.seasonalWildlifeArrival;
        SeasonalWildlifeArrivalMemberSaveData[] proposed =
            (members ?? Array.Empty<SeasonalWildlifeArrivalMemberSaveData>())
            .Select(value => value == null
                ? null
                : SeasonalWildlifeArrivalRules.CloneMember(value))
            .ToArray();
        if (proposed.Any(value => value?.spawned == true))
        {
            failureReason = "새 계절 방문 계획은 생성 완료 상태를 포함할 수 없습니다.";
            return false;
        }
        if (arrival.members.Count > 0)
        {
            if (SeasonalWildlifeArrivalRules.SamePlan(
                    arrival.members,
                    proposed))
            {
                return true;
            }
            failureReason = "계절 방문 계획이 이미 다른 값으로 확정되었습니다.";
            return false;
        }

        SeasonalWildlifeArrivalSaveData candidate = new()
        {
            configured = arrival.configured,
            speciesId = arrival.speciesId,
            exactCount = arrival.exactCount,
            requiredHabitatId = arrival.requiredHabitatId,
            qualification = arrival.qualification,
            members = proposed.ToList(),
            lastFailureReason = string.Empty,
            outcomeResultKey = string.Empty
        };
        SeasonalWorldEventDefinitionSO authored =
            ((IWorldEventCatalog)catalog).Require(occurrence.definitionId);
        try
        {
            SeasonalWildlifeArrivalRules.RequireValidFrozenState(
                candidate,
                authored.wildlifeArrivalProfile,
                occurrence.instanceId);
        }
        catch (InvalidOperationException exception)
        {
            failureReason = exception.Message;
            return false;
        }

        arrival.members = proposed.ToList();
        arrival.lastFailureReason = string.Empty;
        return true;
    }

    internal bool TryRecordSeasonalWildlifeArrivalFailure(
        string occurrenceInstanceId,
        string failureReason)
    {
        string normalized = failureReason?.Trim() ?? string.Empty;
        if (normalized.Length == 0 || normalized.Length > 256)
        {
            return false;
        }
        V20ActiveEventSaveData occurrence = WritableSeasonal.Data.activeEvents
            .SingleOrDefault(value => value != null
                && string.Equals(
                    value.instanceId,
                    occurrenceInstanceId,
                    StringComparison.Ordinal));
        if (occurrence?.seasonalWildlifeArrival?.configured != true)
        {
            return false;
        }
        if (!string.IsNullOrEmpty(
                occurrence.seasonalWildlifeArrival.outcomeResultKey))
        {
            return false;
        }
        occurrence.seasonalWildlifeArrival.lastFailureReason = normalized;
        return true;
    }

    internal bool TryObserveSeasonalWildlifeArrival(
        string occurrenceInstanceId,
        string speciesDisplayName,
        int absoluteDay,
        out string failureReason)
    {
        failureReason = string.Empty;
        V20ActiveEventSaveData occurrence = WritableSeasonal.Data.activeEvents
            .SingleOrDefault(value => value != null
                && string.Equals(
                    value.instanceId,
                    occurrenceInstanceId,
                    StringComparison.Ordinal));
        SeasonalWildlifeArrivalSaveData arrival =
            occurrence?.seasonalWildlifeArrival;
        if (arrival?.configured != true)
        {
            failureReason = "활성 계절 방문 occurrence를 찾지 못했습니다.";
            return false;
        }
        if (arrival.members.Count != arrival.exactCount
            || arrival.members.Any(value => value?.spawned != true))
        {
            failureReason = "모든 고정 방문 개체가 생성되기 전에는 결과를 확정할 수 없습니다.";
            return false;
        }

        SeasonalTerminalObservation.ValidateWildlife(occurrence);
        if (!string.IsNullOrEmpty(arrival.outcomeResultKey)) return true;
        if (arrival.observation.IsEmpty)
        {
            var fact = new SeasonalWildlifeArrivalFact(new SeasonalWildlifeArrivalFactData
            {
                occurrenceId = occurrence.instanceId, speciesId = arrival.speciesId,
                speciesDisplayName = speciesDisplayName, habitatId = arrival.requiredHabitatId,
                absoluteDay = absoluteDay,
                wildlifeIds = arrival.members.Select(member => member.wildlifeId)
                    .OrderBy(id => id, StringComparer.Ordinal).ToArray()
            });
            if (absoluteDay < occurrence.startedAbsoluteDay)
            { failureReason = "seasonal-arrival-observation-before-start"; return false; }
            arrival.observation = fact.Snapshot();
        }
        publishingSeasonalObservation = true;
        try
        {
            if (!SeasonalTerminalObservation.TryPublish(new SeasonalWildlifeArrivalFact(arrival.observation),
                    arrival.publication, terminalObservations, terminalRecovery, out failureReason)) return false;
            arrival.outcomeResultKey = arrival.publication.key;
            arrival.lastFailureReason = string.Empty;
            return true;
        }
        finally { publishingSeasonalObservation = false; }
    }

    internal bool TryMarkSeasonalWildlifeArrivalSpawned(
        string occurrenceInstanceId,
        string wildlifeId,
        out string failureReason)
    {
        failureReason = string.Empty;
        V20ActiveEventSaveData occurrence = WritableSeasonal.Data.activeEvents
            .SingleOrDefault(value => value != null
                && string.Equals(
                    value.instanceId,
                    occurrenceInstanceId,
                    StringComparison.Ordinal));
        SeasonalWildlifeArrivalSaveData arrival =
            occurrence?.seasonalWildlifeArrival;
        SeasonalWildlifeArrivalMemberSaveData member = arrival?.members
            ?.SingleOrDefault(value => value != null
                && string.Equals(
                    value.wildlifeId,
                    wildlifeId,
                    StringComparison.Ordinal));
        if (member == null)
        {
            failureReason = "계절 방문 계획에 예약 개체가 없습니다.";
            return false;
        }
        if (member.spawned)
        {
            return true;
        }
        member.spawned = true;
        if (arrival.members.All(value => value?.spawned == true))
        {
            arrival.lastFailureReason = string.Empty;
        }
        return true;
    }

    internal bool TryRecordSeasonalDriftCargoPlan(
        string occurrenceInstanceId,
        int positionX,
        int positionY,
        out string failureReason)
    {
        failureReason = string.Empty;
        SeasonalDriftCargoSaveData cargo = FindSeasonalDriftCargo(
            occurrenceInstanceId);
        if (cargo == null)
        {
            failureReason = "활성 표류 화물 occurrence를 찾지 못했습니다.";
            return false;
        }
        if (cargo.positionFrozen)
        {
            if (cargo.positionX == positionX && cargo.positionY == positionY)
                return true;
            failureReason = "표류 화물 위치가 이미 다른 값으로 확정되었습니다.";
            return false;
        }
        cargo.positionFrozen = true;
        cargo.positionX = positionX;
        cargo.positionY = positionY;
        cargo.lastFailureReason = string.Empty;
        return true;
    }

    internal bool TryMarkSeasonalDriftCargoSpawned(
        string occurrenceInstanceId,
        IReadOnlyList<string> sourceStackIds,
        out string failureReason)
    {
        failureReason = string.Empty;
        SeasonalDriftCargoSaveData cargo = FindSeasonalDriftCargo(
            occurrenceInstanceId);
        string[] ids = (sourceStackIds ?? Array.Empty<string>())
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        if (cargo == null || !cargo.positionFrozen
            || ids.Length == 0
            || ids.Any(string.IsNullOrWhiteSpace)
            || ids.Distinct(StringComparer.Ordinal).Count() != ids.Length)
        {
            failureReason = "표류 화물 생성 receipt가 유효하지 않습니다.";
            return false;
        }
        if (cargo.spawned)
        {
            if (cargo.sourceStackIds.SequenceEqual(ids, StringComparer.Ordinal))
                return true;
            failureReason = "표류 화물 원본 스택이 이미 다른 값으로 확정되었습니다.";
            return false;
        }
        cargo.spawned = true;
        cargo.sourceStackIds = ids.ToList();
        cargo.lastFailureReason = string.Empty;
        return true;
    }

    internal bool TryRecordSeasonalDriftCargoSecured(
        string occurrenceInstanceId,
        int securedQuantity,
        out string failureReason)
    {
        failureReason = string.Empty;
        SeasonalDriftCargoSaveData cargo = FindSeasonalDriftCargo(
            occurrenceInstanceId);
        if (cargo == null
            || securedQuantity < cargo.securedQuantity
            || securedQuantity + cargo.expiredQuantity > cargo.exactQuantity)
        {
            failureReason = "표류 화물 확보 수량이 기존 물리 진행과 충돌합니다.";
            return false;
        }
        cargo.securedQuantity = securedQuantity;
        cargo.lastFailureReason = string.Empty;
        return true;
    }

    internal bool TryExpireSeasonalDriftCargo(
        string occurrenceInstanceId,
        IReadOnlyList<PhysicalItemTransformInput> unpickedSourceLots,
        out string failureReason)
    {
        failureReason = string.Empty;
        SeasonalDriftCargoSaveData cargo = FindSeasonalDriftCargo(
            occurrenceInstanceId);
        if (cargo == null)
        {
            failureReason = "활성 표류 화물 occurrence를 찾지 못했습니다.";
            return false;
        }
        if (cargo.expirationCompleted)
            return true;

        PhysicalItemTransformInput[] inputs = (unpickedSourceLots
                ?? Array.Empty<PhysicalItemTransformInput>())
            .OrderBy(value => value.StackId, StringComparer.Ordinal)
            .ToArray();
        if (inputs.Length == 0
            && string.IsNullOrEmpty(cargo.dispositionOperationId))
        {
            if (cargo.spawned)
            {
                cargo.securedQuantity = Math.Max(
                    cargo.securedQuantity,
                    cargo.exactQuantity - cargo.expiredQuantity);
            }
            cargo.expirationCompleted = true;
            cargo.lastFailureReason = string.Empty;
            return true;
        }
        if (physicalDispositions == null)
        {
            failureReason = "표류 화물 물리 손실 권위를 찾지 못했습니다.";
            return false;
        }

        string operationId = SeasonalDriftCargoRules.ExpirationOperationId(
            occurrenceInstanceId);
        PhysicalItemBatchDispositionReceipt receipt;
        if (!string.IsNullOrEmpty(cargo.dispositionOperationId))
        {
            if (!physicalDispositions.TryGetPending(operationId, out receipt))
            {
                failureReason = "표류 화물 손실 receipt를 복원하지 못했습니다.";
                return false;
            }
        }
        else if (!physicalDispositions.TryCommitPending(
                     inputs,
                     PhysicalItemDispositionKind.Sink,
                     operationId,
                     SeasonalDriftCargoRules.ExpirationReason,
                     out receipt,
                     out failureReason))
        {
            return false;
        }

        if (receipt.Kind != PhysicalItemDispositionKind.Sink
            || !string.Equals(receipt.OperationId, operationId, StringComparison.Ordinal)
            || !string.Equals(
                receipt.ReasonCode,
                SeasonalDriftCargoRules.ExpirationReason,
                StringComparison.Ordinal)
            || receipt.Quantity <= 0
            || cargo.securedQuantity + receipt.Quantity > cargo.exactQuantity)
        {
            failureReason = "표류 화물 손실 receipt가 occurrence와 일치하지 않습니다.";
            return false;
        }
        cargo.dispositionOperationId = operationId;
        cargo.dispositionCommitId = receipt.CommitId;
        cargo.dispositionSourceStackIds = receipt.SourceStackIds.ToList();
        cargo.expiredQuantity = receipt.Quantity;
        cargo.expiredMassGrams = receipt.InputMassGrams;
        cargo.securedQuantity = cargo.exactQuantity - cargo.expiredQuantity;
        if (!physicalDispositions.Acknowledge(
                receipt.CommitId,
                out failureReason))
        {
            cargo.lastFailureReason = failureReason;
            return false;
        }
        cargo.expirationCompleted = true;
        cargo.lastFailureReason = string.Empty;
        return true;
    }

    internal bool TryRecordSeasonalDriftCargoFailure(
        string occurrenceInstanceId,
        string failureReason)
    {
        string normalized = failureReason?.Trim() ?? string.Empty;
        SeasonalDriftCargoSaveData cargo = FindSeasonalDriftCargo(
            occurrenceInstanceId);
        if (cargo == null
            || normalized.Length is < 1 or > 256
            || !string.IsNullOrEmpty(cargo.outcomeResultKey))
            return false;
        cargo.lastFailureReason = normalized;
        return true;
    }

    internal bool TryObserveSeasonalDriftCargo(
        string occurrenceInstanceId,
        string itemDisplayName,
        long unitMassGrams,
        int absoluteDay,
        out string failureReason)
    {
        failureReason = string.Empty;
        V20ActiveEventSaveData occurrence = WritableSeasonal.Data.activeEvents
            .SingleOrDefault(value => value != null && value.instanceId == occurrenceInstanceId);
        SeasonalDriftCargoSaveData cargo = occurrence?.seasonalDriftCargo;
        if (cargo?.configured != true)
        {
            failureReason = "활성 표류 화물 occurrence를 찾지 못했습니다.";
            return false;
        }
        if (!cargo.spawned || !cargo.expirationCompleted)
        {
            failureReason = "생성된 표류 화물의 만료 정산 전에는 결과를 확정할 수 없습니다.";
            return false;
        }

        SeasonalTerminalObservation.ValidateCargo(occurrence);
        if (!string.IsNullOrEmpty(cargo.outcomeResultKey)) return true;
        if (cargo.observation.IsEmpty)
        {
            var fact = new SeasonalDriftCargoFact(new SeasonalDriftCargoFactData
            {
                occurrenceId = occurrence.instanceId, itemId = cargo.itemId, itemDisplayName = itemDisplayName,
                absoluteDay = absoluteDay, unitMassGrams = unitMassGrams, exactQuantity = cargo.exactQuantity,
                securedQuantity = cargo.securedQuantity, expiredQuantity = cargo.expiredQuantity,
                expiredMassGrams = cargo.expiredMassGrams, dispositionCommitId = cargo.dispositionCommitId,
                positionX = cargo.positionX, positionY = cargo.positionY,
                sourceStackIds = cargo.sourceStackIds.OrderBy(id => id, StringComparer.Ordinal).ToArray()
            });
            if (absoluteDay < occurrence.startedAbsoluteDay)
            { failureReason = "seasonal-cargo-observation-before-start"; return false; }
            cargo.observation = fact.Snapshot();
        }
        publishingSeasonalObservation = true;
        try
        {
            if (!SeasonalTerminalObservation.TryPublish(new SeasonalDriftCargoFact(cargo.observation),
                    cargo.publication, terminalObservations, terminalRecovery, out failureReason)) return false;
            cargo.outcomeResultKey = cargo.publication.key;
            cargo.lastFailureReason = string.Empty;
            return true;
        }
        finally { publishingSeasonalObservation = false; }
    }

    private SeasonalDriftCargoSaveData FindSeasonalDriftCargo(
        string occurrenceInstanceId) => WritableSeasonal.Data.activeEvents
        .SingleOrDefault(value => value != null
            && string.Equals(
                value.instanceId,
                occurrenceInstanceId,
                StringComparison.Ordinal))
        ?.seasonalDriftCargo is { configured: true } cargo
            ? cargo
            : null;
}
