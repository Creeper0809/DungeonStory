using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using DungeonStory.Foundation.Facts;
using DungeonStory.Operation;
using UnityEngine;

internal sealed class V20FactionContractQuery : IFactionContractDeliveryQuery
{
    private readonly V20CampaignRuntime live;
    private readonly V20StoryContentCatalog catalog;
    private readonly IWorldItemStackRuntime items;
    private readonly IFacilityCapabilityQuery facilities;
    private readonly IWorldDropZoneQuery dropZones;

    internal V20FactionContractQuery(
        V20CampaignRuntime live,
        V20StoryContentCatalog catalog,
        IWorldItemStackRuntime items,
        IFacilityCapabilityQuery facilities,
        IWorldDropZoneQuery dropZones)
    {
        this.live = live;
        this.catalog = catalog;
        this.items = items;
        this.facilities = facilities;
        this.dropZones = dropZones;
    }

    public IReadOnlyList<FactionContractView> GetContracts(string factionId)
    {
        string normalizedFactionId = factionId?.Trim() ?? string.Empty;
        if (!live.TryGetFaction(
                normalizedFactionId,
                out FactionCampaignStateSaveData state))
            return Array.Empty<FactionContractView>();

        WorldItemStackSnapshot[] physical = items.GetAllStacks()
            .Where(value => value != null && value.Quantity > 0)
            .ToArray();
        bool hasAdministration = facilities
            .FindOperational(FacilityCapabilityKind.Administration)
            .Count > 0;
        bool hasDeliveryDropoff = dropZones.TryGetDeliveryDropoff(out _);
        return catalog.Contracts
            .Where(value => string.Equals(
                value.factionId,
                normalizedFactionId,
                StringComparison.Ordinal))
            .Select(contract => (
                Contract: contract,
                Occurrence: FindActiveSeasonalContractOccurrence(
                    contract,
                    normalizedFactionId)))
            .Where(value => string.IsNullOrEmpty(
                    value.Contract.seasonalEventId)
                || value.Occurrence != null)
            .OrderBy(value => value.Contract.kind)
            .ThenBy(value => value.Contract.StableId, StringComparer.Ordinal)
            .Select(value => BuildFactionContractView(
                value.Contract,
                value.Occurrence,
                state,
                physical,
                hasAdministration,
                hasDeliveryDropoff))
            .ToArray();
    }

    private V20ActiveEventSaveData FindActiveSeasonalContractOccurrence(
        FactionContractDefinitionSO contract,
        string factionId)
    {
        if (contract == null || string.IsNullOrEmpty(contract.seasonalEventId))
            return null;
        return live.ActiveSeasonalEvents.FirstOrDefault(value => value != null
            && !value.resolved
            && string.Equals(
                value.definitionId,
                contract.seasonalEventId,
                StringComparison.Ordinal)
            && string.Equals(
                value.contextFactionId,
                factionId,
                StringComparison.Ordinal));
    }

    private FactionContractView BuildFactionContractView(
        FactionContractDefinitionSO contract,
        V20ActiveEventSaveData occurrence,
        FactionCampaignStateSaveData state,
        IReadOnlyList<WorldItemStackSnapshot> physical,
        bool hasAdministration,
        bool hasDeliveryDropoff)
    {
        bool seasonal = !string.IsNullOrEmpty(contract.seasonalEventId);
        string occurrenceId = occurrence?.instanceId ?? string.Empty;
        bool lifecycleOccurrenceMatches = !seasonal
            || string.Equals(
                state.activeContractOccurrenceId,
                occurrenceId,
                StringComparison.Ordinal);
        bool active = lifecycleOccurrenceMatches
            && string.Equals(
                state.activeContractId,
                contract.StableId,
                StringComparison.Ordinal);
        bool acceptedThisOccurrence = !seasonal
            || string.Equals(
                    state.lastSeasonalContractId,
                    contract.StableId,
                    StringComparison.Ordinal)
                && string.Equals(
                    state.lastSeasonalOccurrenceId,
                    occurrenceId,
                    StringComparison.Ordinal);
        bool rewardPublished = acceptedThisOccurrence
            && state.completedContractIds.Contains(
            contract.StableId,
            StringComparer.Ordinal);
        bool deliveryForView = lifecycleOccurrenceMatches
            && string.Equals(
                state.deliveryContractId,
                contract.StableId,
                StringComparison.Ordinal);
        bool settlingCompletedDelivery = rewardPublished
            && deliveryForView
            && FactionContractDeliveryOutbox.HasPending(state);
        bool completed = rewardPublished && !settlingCompletedDelivery;
        bool failed = acceptedThisOccurrence
            && state.failedContractIds.Contains(
                contract.StableId,
                StringComparer.Ordinal);
        bool terminalBusy = FactionContractDeliveryOutbox.HasPending(state)
            || state.deliveryTerminalCleanupPending;
        bool stateAllowsAccept = !active
            && !rewardPublished
            && !failed
            && string.IsNullOrWhiteSpace(state.activeContractId)
            && !terminalBusy;
        bool material = FactionContractMaterialRules.HasMaterialRequirements(
            contract);
        bool canAccept = stateAllowsAccept
            && hasAdministration
            && (!material || hasDeliveryDropoff);
        string disabledReason = string.Empty;
        if (!canAccept)
        {
            if (!stateAllowsAccept)
            {
                disabledReason = active
                    ? "진행 중"
                    : settlingCompletedDelivery
                        ? "보상 반영·인도 확인 중"
                        : completed
                            ? "완료됨"
                            : failed
                                ? "실패 이력"
                                : terminalBusy
                                    ? "이전 인도 정산 중"
                                    : "다른 계약 진행 중";
            }
            else
            {
                disabledReason = !hasAdministration
                    ? "운영 중인 행정 시설이 필요함"
                    : "유효한 물자 인도 지점이 필요함";
            }
        }

        string destinationId = active
            || deliveryForView
                ? state.activeContractDestinationId
                : string.Empty;
        IReadOnlyList<FactionContractItemProgressView> itemViews =
            FactionContractMaterialRules.BuildMaterialRequirements(contract)
                .Select(requirement =>
                {
                    int assigned = CountPhysical(
                        physical,
                        destinationId,
                        requirement.Key,
                        stack => stack.State is WorldItemStackState.Loose
                            or WorldItemStackState.Stored
                            or WorldItemStackState.FacilityOutputBuffer);
                    int hauling = CountPhysical(
                        physical,
                        destinationId,
                        requirement.Key,
                        stack => stack.State is WorldItemStackState.Carried
                            or WorldItemStackState.InTransit);
                    int arrived = CountPhysical(
                        physical,
                        destinationId,
                        requirement.Key,
                        stack => stack.State ==
                            WorldItemStackState.FacilityBuffer);
                    int delivered = deliveryForView
                        && FactionContractDeliveryOutbox.HasPending(state)
                            ? requirement.Value
                            : rewardPublished
                                ? requirement.Value
                                : 0;
                    string displayName = items.CatalogProvider.TryGetDefinition(
                            requirement.Key,
                            out DungeonItemDefinition definition)
                        ? definition.DisplayName
                        : requirement.Key;
                    return new FactionContractItemProgressView(
                        requirement.Key,
                        displayName,
                        requirement.Value,
                        assigned,
                        hauling,
                        arrived,
                        delivered);
                })
                .ToArray();
        return new FactionContractView
        {
            ContractId = contract.StableId,
            OccurrenceId = occurrenceId,
            AcceptActionId = seasonal
                ? V21ContentAlertActionIds.SeasonalFactionContractAccept(
                    occurrenceId,
                    contract.StableId)
                : V21ContentAlertActionIds.FactionContractAccept(
                    contract.factionId,
                    contract.StableId),
            DisplayName = contract.DisplayName,
            Description = contract.Description,
            Kind = contract.kind,
            DeadlineDays = contract.deadlineDays,
            DeadlineAbsoluteDay = active
                ? state.activeContractDeadlineAbsoluteDay
                : occurrence?.deadlineAbsoluteDay ?? 0,
            IsActive = active,
            IsCompleted = completed,
            IsFailed = failed,
            CanAccept = canAccept,
            DisabledReason = disabledReason,
            EligibilityReason = canAccept
                ? "수락 가능 · 행정 시설의 관리 인장 사용 시 확정"
                : disabledReason,
            StatusReason = active
                || deliveryForView
                    ? state.deliveryFailureReason
                    : failed
                        ? "기한 만료 또는 계약 실패"
                    : string.Empty,
            CompletionConditions = FormatRequirements(
                contract.completionRequirements),
            SuccessEffects = FormatEffects(contract.successEffects),
            FailureEffects = FormatEffects(contract.failureEffects),
            Items = itemViews
        };
    }

    private IReadOnlyList<string> FormatRequirements(
        V20ContentRequirementSet source)
    {
        source ??= new V20ContentRequirementSet();
        List<string> lines = new();
        foreach (V20ItemAmountRequirement value in source.items
                     ?? new List<V20ItemAmountRequirement>())
        {
            if (value == null)
                continue;
            string itemName = items.CatalogProvider.TryGetDefinition(
                    value.itemDefinitionId,
                    out DungeonItemDefinition definition)
                ? definition.DisplayName
                : value.itemDefinitionId;
            lines.Add($"{itemName} {value.amount}개 · "
                + (value.consume ? "실물 인도" : "보유"));
        }
        foreach (V20FacilityRequirement value in source.facilities
                     ?? new List<V20FacilityRequirement>())
        {
            if (value == null)
                continue;
            string facility = !string.IsNullOrWhiteSpace(value.capabilityId)
                ? value.capabilityId
                : value.buildingDefinitionId;
            lines.Add($"시설 {facility} {value.minimumCount}개"
                + (value.mustBeOperational ? " · 운영 중" : string.Empty));
        }
        foreach (V20ResearchRequirement value in source.research
                     ?? new List<V20ResearchRequirement>())
        {
            if (value != null)
                lines.Add($"연구 #{value.researchNumericId} 완료");
        }
        foreach (V20CharacterRequirement value in source.characters
                     ?? new List<V20CharacterRequirement>())
        {
            if (value == null)
                continue;
            string traits = string.IsNullOrWhiteSpace(value.requiredTraitId)
                ? string.Empty
                : $" · 특성 {value.requiredTraitId}";
            string excluded = string.IsNullOrWhiteSpace(value.excludedTraitId)
                ? string.Empty
                : $" · 제외 특성 {value.excludedTraitId}";
            lines.Add($"인물 {value.minimumLifeStage}~{value.maximumLifeStage}"
                + $" · 건강 {value.minimumHealth} 이상{traits}{excluded}");
        }
        foreach (V20FactionRequirement value in source.factions
                     ?? new List<V20FactionRequirement>())
        {
            if (value != null)
            {
                lines.Add($"세력 {value.factionId} · 관계 {value.minimumRapport} 이상"
                    + $" · 불만 {value.maximumGrievance} 이하"
                    + $" · 의무 {value.minimumObligationTokens} 이상");
            }
        }
        foreach (V20WorldMetricRequirement value in source.worldMetrics
                     ?? new List<V20WorldMetricRequirement>())
        {
            if (value != null)
                lines.Add($"{value.kind} {value.minimumValue:0.##} 이상");
        }
        lines.AddRange((source.requiredFlags ?? new List<string>())
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => $"상태 {value} 필요"));
        lines.AddRange((source.excludedFlags ?? new List<string>())
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => $"상태 {value} 없어야 함"));
        return lines.Count > 0
            ? lines.AsReadOnly()
            : new[] { "추가 완료 조건 없음" };
    }

    private static IReadOnlyList<string> FormatEffects(
        IEnumerable<V20ContentEffect> source)
    {
        string[] lines = (source ?? Array.Empty<V20ContentEffect>())
            .Where(value => value != null && value.IsValid)
            .Select(value =>
            {
                string target = string.IsNullOrWhiteSpace(value.targetId)
                    ? string.Empty
                    : $" · 대상 {value.targetId}";
                string duration = value.durationDays > 0
                    ? $" · {value.durationDays}일"
                    : string.Empty;
                return $"{value.kind} {value.amount:+0.##;-0.##;0}"
                    + target
                    + duration;
            })
            .ToArray();
        return lines.Length > 0 ? lines : new[] { "없음" };
    }

    internal int CountFactionContractQuantity(
        string destinationId,
        string itemId,
        Func<WorldItemStackSnapshot, bool> predicate) =>
        CountPhysical(
            items.GetAllStacks(),
            destinationId,
            itemId,
            predicate);

    private static int CountPhysical(
        IEnumerable<WorldItemStackSnapshot> source,
        string destinationId,
        string itemId,
        Func<WorldItemStackSnapshot, bool> predicate) =>
        string.IsNullOrWhiteSpace(destinationId)
            ? 0
            : (source ?? Array.Empty<WorldItemStackSnapshot>())
                .Where(stack => stack != null
                    && stack.Quantity > 0
                    && string.Equals(
                        stack.DestinationId,
                        destinationId,
                        StringComparison.Ordinal)
                    && string.Equals(
                        stack.ItemId,
                        itemId,
                        StringComparison.Ordinal)
                    && (predicate?.Invoke(stack) ?? true))
                .Sum(stack => stack.Quantity);
}
