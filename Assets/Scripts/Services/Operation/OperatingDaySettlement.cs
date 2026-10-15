using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using DungeonStory.Foundation.Facts;
using DungeonStory.Operation;
using UnityEngine;
using VContainer;

[Serializable]
public sealed class FacilityRevenueSummary
{
    public FacilityRevenueSummary(string facilityName, int revenue)
    {
        this.facilityName = facilityName ?? string.Empty;
        this.revenue = Mathf.Max(0, revenue);
    }

    public string facilityName { get; }
    public int revenue { get; }
}

[Serializable]
public sealed class SpeciesVisitSummary
{
    public SpeciesVisitSummary(string speciesTag, int visitCount)
    {
        this.speciesTag = speciesTag ?? string.Empty;
        this.visitCount = Mathf.Max(0, visitCount);
    }

    public string speciesTag { get; }
    public int visitCount { get; }
}

[Serializable]
public sealed class StockConsumptionSummary
{
    public StockConsumptionSummary(StockCategory category, int amount)
    {
        this.category = category;
        this.amount = Mathf.Max(0, amount);
    }

    public StockCategory category { get; }
    public int amount { get; }
}

[Serializable]
public sealed class WarehouseStockSummary
{
    public WarehouseStockSummary(
        string warehouseName,
        int totalStock,
        long storedMassGrams,
        long maxMassGrams,
        IReadOnlyList<StockConsumptionSummary> stocks)
    {
        this.warehouseName = warehouseName ?? string.Empty;
        this.totalStock = Mathf.Max(0, totalStock);
        this.storedMassGrams = Math.Max(0L, storedMassGrams);
        this.maxMassGrams = Math.Max(0L, maxMassGrams);
        this.stocks = EventPayloadSnapshot.Copy(stocks);
    }

    public string warehouseName { get; }
    public int totalStock { get; }
    public long storedMassGrams { get; }
    public long maxMassGrams { get; }
    public IReadOnlyList<StockConsumptionSummary> stocks { get; }

    public string ToSummaryText()
    {
        string stockText = stocks == null || stocks.Count == 0
            ? "비어 있음"
            : string.Join(", ", stocks.Select((item) =>
                $"{StockCategoryPersistenceId.ToId(item.category)} {item.amount}"));
        return $"{warehouseName}: {WarehouseMassUiFormatter.FormatKilograms(storedMassGrams)}"
            + $"/{WarehouseMassUiFormatter.FormatKilograms(maxMassGrams)} · {totalStock}개 ({stockText})";
    }
}

[Serializable]
public sealed class StaffWorkSummary
{
    public StaffWorkSummary(
        int staffCount,
        int workingCount,
        int offDutyCount,
        float averageSleep,
        float averageMood)
    {
        this.staffCount = Mathf.Max(0, staffCount);
        this.workingCount = Mathf.Max(0, workingCount);
        this.offDutyCount = Mathf.Max(0, offDutyCount);
        this.averageSleep = averageSleep;
        this.averageMood = averageMood;
    }

    public int staffCount { get; }
    public int workingCount { get; }
    public int offDutyCount { get; }
    public float averageSleep { get; }
    public float averageMood { get; }
}

[Serializable]
public sealed class OperatingDayReport
{
    public const string PersistentIdPrefix = "operating-day-report:day=";

    private OperatingDayReport(
        int day,
        int totalRevenue,
        int totalVisits,
        float averageSatisfaction,
        int repairCost,
        int restockFailureCount,
        IReadOnlyList<FacilityRevenueSummary> facilityRevenues,
        IReadOnlyList<SpeciesVisitSummary> speciesVisits,
        IReadOnlyList<string> incidents,
        IReadOnlyList<string> damagedFacilities,
        IReadOnlyList<string> stockShortageFacilities,
        IReadOnlyList<string> staffComplaintEvents,
        IReadOnlyList<string> eventLog,
        IReadOnlyList<string> unlockedCodexInfo,
        StaffWorkSummary staffSummary,
        IReadOnlyList<WarehouseStockSummary> warehouseStocks,
        IReadOnlyList<StockConsumptionSummary> stockConsumed,
        IReadOnlyList<StockSupplyResult> stockSupplyResults,
        IReadOnlyList<StockDeliveryOffer> refreshedDailyShopOffers,
        IReadOnlyList<FacilityShopOfferSnapshot> refreshedFacilityShopOffers,
        int maintenanceCost,
        int payrollCost,
        int previousDebt,
        int paidOperatingCost,
        int unpaidOperatingCost,
        int closingBalance,
        int consecutiveShortfallDays,
        IReadOnlyList<OperatingDayFacilityLedgerEntry> facilityDetails,
        int terminatedContractCost,
        IReadOnlyList<OperatingDayEmploymentSettlementLine> employmentSettlements)
    {
        this.day = Mathf.Max(1, day);
        this.totalRevenue = Mathf.Max(0, totalRevenue);
        this.totalVisits = Mathf.Max(0, totalVisits);
        this.averageSatisfaction = averageSatisfaction;
        this.repairCost = Mathf.Max(0, repairCost);
        this.restockFailureCount = Mathf.Max(0, restockFailureCount);
        this.facilityRevenues = EventPayloadSnapshot.Copy(facilityRevenues);
        this.speciesVisits = EventPayloadSnapshot.Copy(speciesVisits);
        this.incidents = EventPayloadSnapshot.Copy(incidents);
        this.damagedFacilities = EventPayloadSnapshot.Copy(damagedFacilities);
        this.stockShortageFacilities = EventPayloadSnapshot.Copy(stockShortageFacilities);
        this.staffComplaintEvents = EventPayloadSnapshot.Copy(staffComplaintEvents);
        this.eventLog = EventPayloadSnapshot.Copy(eventLog);
        this.unlockedCodexInfo = EventPayloadSnapshot.Copy(unlockedCodexInfo);
        this.staffSummary = staffSummary ?? new StaffWorkSummary(0, 0, 0, 0f, 0f);
        this.warehouseStocks = EventPayloadSnapshot.Copy(warehouseStocks);
        this.stockConsumed = EventPayloadSnapshot.Copy(stockConsumed);
        this.stockSupplyResults = EventPayloadSnapshot.Copy(stockSupplyResults);
        this.refreshedDailyShopOffers = EventPayloadSnapshot.Copy(refreshedDailyShopOffers);
        this.refreshedFacilityShopOffers = EventPayloadSnapshot.Copy(refreshedFacilityShopOffers);
        this.maintenanceCost = Mathf.Max(0, maintenanceCost);
        this.payrollCost = Mathf.Max(0, payrollCost);
        this.previousDebt = Mathf.Max(0, previousDebt);
        this.paidOperatingCost = Mathf.Max(0, paidOperatingCost);
        this.unpaidOperatingCost = Mathf.Max(0, unpaidOperatingCost);
        this.closingBalance = Mathf.Max(0, closingBalance);
        this.consecutiveShortfallDays = Mathf.Max(0, consecutiveShortfallDays);
        this.facilityDetails = EventPayloadSnapshot.Copy(facilityDetails);
        this.terminatedContractCost = Mathf.Max(0, terminatedContractCost);
        this.employmentSettlements = Array.AsReadOnly(
            (employmentSettlements
                ?? Array.Empty<OperatingDayEmploymentSettlementLine>())
            .Select(value => value?.Clone()
                ?? throw new ArgumentException(
                    "Employment settlement reports cannot contain null lines.",
                    nameof(employmentSettlements)))
            .ToArray());
    }

    public int day { get; }
    public string reportId => CreatePersistentId(day);
    public int totalRevenue { get; }
    public int totalVisits { get; }
    public float averageSatisfaction { get; }
    public int repairCost { get; }
    public int restockFailureCount { get; }
    public IReadOnlyList<FacilityRevenueSummary> facilityRevenues { get; }
    public IReadOnlyList<OperatingDayFacilityLedgerEntry> facilityDetails { get; }
    public IReadOnlyList<SpeciesVisitSummary> speciesVisits { get; }
    public IReadOnlyList<string> incidents { get; }
    public IReadOnlyList<string> damagedFacilities { get; }
    public IReadOnlyList<string> stockShortageFacilities { get; }
    public IReadOnlyList<string> staffComplaintEvents { get; }
    public IReadOnlyList<string> eventLog { get; }
    public IReadOnlyList<string> unlockedCodexInfo { get; }
    public StaffWorkSummary staffSummary { get; }
    public IReadOnlyList<WarehouseStockSummary> warehouseStocks { get; }
    public IReadOnlyList<StockConsumptionSummary> stockConsumed { get; }
    public IReadOnlyList<StockSupplyResult> stockSupplyResults { get; }
    public IReadOnlyList<StockDeliveryOffer> refreshedDailyShopOffers { get; }
    public IReadOnlyList<FacilityShopOfferSnapshot> refreshedFacilityShopOffers { get; }
    public int maintenanceCost { get; }
    public int payrollCost { get; }
    public int previousDebt { get; }
    public int totalOperatingCost => maintenanceCost + payrollCost + previousDebt;
    public int paidOperatingCost { get; }
    public int unpaidOperatingCost { get; }
    public int terminatedContractCost { get; }
    public int closingBalance { get; }
    public int consecutiveShortfallDays { get; }
    public IReadOnlyList<OperatingDayEmploymentSettlementLine>
        employmentSettlements { get; }

    public static string CreatePersistentId(int day) =>
        PersistentIdPrefix + Mathf.Max(1, day);

    public static OperatingDayReport Create(
        int day,
        int totalRevenue = 0,
        int totalVisits = 0,
        float averageSatisfaction = 0f,
        int repairCost = 0,
        int restockFailureCount = 0,
        IReadOnlyList<FacilityRevenueSummary> facilityRevenues = null,
        IReadOnlyList<SpeciesVisitSummary> speciesVisits = null,
        IReadOnlyList<string> incidents = null,
        IReadOnlyList<string> damagedFacilities = null,
        IReadOnlyList<string> stockShortageFacilities = null,
        IReadOnlyList<string> staffComplaintEvents = null,
        IReadOnlyList<string> eventLog = null,
        IReadOnlyList<string> unlockedCodexInfo = null,
        StaffWorkSummary staffSummary = null,
        IReadOnlyList<WarehouseStockSummary> warehouseStocks = null,
        IReadOnlyList<StockConsumptionSummary> stockConsumed = null,
        IReadOnlyList<StockSupplyResult> stockSupplyResults = null,
        IReadOnlyList<StockDeliveryOffer> refreshedDailyShopOffers = null,
        IReadOnlyList<FacilityShopOfferSnapshot> refreshedFacilityShopOffers = null,
        int maintenanceCost = 0,
        int payrollCost = 0,
        int previousDebt = 0,
        int paidOperatingCost = 0,
        int unpaidOperatingCost = 0,
        int closingBalance = 0,
        int consecutiveShortfallDays = 0,
        IReadOnlyList<OperatingDayFacilityLedgerEntry> facilityDetails = null,
        int terminatedContractCost = 0,
        IReadOnlyList<OperatingDayEmploymentSettlementLine>
            employmentSettlements = null)
    {
        return new OperatingDayReport(
            day,
            totalRevenue,
            totalVisits,
            averageSatisfaction,
            repairCost,
            restockFailureCount,
            facilityRevenues,
            speciesVisits,
            incidents,
            damagedFacilities,
            stockShortageFacilities,
            staffComplaintEvents,
            eventLog,
            unlockedCodexInfo,
            staffSummary,
            warehouseStocks,
            stockConsumed,
            stockSupplyResults,
            refreshedDailyShopOffers,
            refreshedFacilityShopOffers,
            maintenanceCost,
            payrollCost,
            previousDebt,
            paidOperatingCost,
            unpaidOperatingCost,
            closingBalance,
            consecutiveShortfallDays,
            facilityDetails,
            terminatedContractCost,
            employmentSettlements);
    }

    public string ToDetailText()
    {
        List<string> lines = new List<string>
        {
            $"Day {day} 운영 정산",
            string.Empty,
            $"총 매출: {totalRevenue}",
            $"방문 손님 수: {totalVisits}",
            $"평균 만족도: {averageSatisfaction:0.#}",
            $"시설 계약비: {maintenanceCost}",
            $"직원 급여: {payrollCost}",
            $"이전 미납금: {previousDebt}",
            $"운영비 납부: {paidOperatingCost}/{totalOperatingCost}",
            $"새 미납금: {unpaidOperatingCost}",
            $"종료 계약 미지급액: {terminatedContractCost}",
            $"마감 자금: {closingBalance}",
            $"수리 비용 예상: {repairCost}",
            $"보충 실패 횟수: {restockFailureCount}",
            string.Empty,
            FormatList(
                "시설별 운영 상세",
                facilityDetails.Select((item) =>
                    $"{item.FacilityName} [{item.FacilityId}]: 매출 {item.Revenue}, 비용 {item.OperatingCost}, 방문 {item.VisitCount}, 만족도 {item.AverageSatisfaction:0.#}, 소비 "
                    + (item.ConsumedStock.Count == 0
                        ? "없음"
                        : string.Join(", ", item.ConsumedStock
                            .OrderBy(pair => pair.Key)
                            .Select(pair =>
                                $"{GetStockCategoryName((StockCategory)pair.Key)} {pair.Value}"))))),
            FormatList("시설별 매출", facilityRevenues.Select((item) => $"{item.facilityName}: {item.revenue}")),
            FormatList("종족별 방문", speciesVisits.Select((item) => $"{TextOrDefault(item.speciesTag, "Unknown")}: {item.visitCount}")),
            FormatList("소비된 재고", stockConsumed.Select((item) => $"{GetStockCategoryName(item.category)}: {item.amount}")),
            FormatList("창고 재고", warehouseStocks.Select((item) => item.ToSummaryText())),
            FormatList("재고 부족 시설", stockShortageFacilities),
            FormatList("파손 시설", damagedFacilities),
            FormatList("발생한 사고", incidents),
            FormatList("이벤트 로그", eventLog),
            FormatList("직원 불만 사건", staffComplaintEvents),
            FormatList(
                "개인별 고용 정산",
                employmentSettlements.Select(item =>
                    $"{item.characterId}: 지급 {item.paid}/{item.due}, 채무 {item.unpaidDebtAfter}, 계약종료 {item.contractEnded}, 이탈시작 {item.departureStarted}, finance {TextOrDefault(item.financeTransactionId, "없음")}")),
            $"직원 요약: 총 {staffSummary.staffCount}, 근무 {staffSummary.workingCount}, 비번 {staffSummary.offDutyCount}, 평균 피로 {staffSummary.averageSleep:0.#}, 평균 기분 {staffSummary.averageMood:0.#}",
            FormatList("재고 수급 결과", stockSupplyResults.Select((result) => result.ToSummaryText())),
            FormatList("상점 판매 목록 갱신", refreshedDailyShopOffers.Select((offer) => $"{GetStockCategoryName(offer.category)} {offer.amount}개 / 비용 {offer.cost}")),
            FormatList("시설 상점 갱신", refreshedFacilityShopOffers.Select((offer) => offer.ToSummaryText())),
            FormatList("신규 도감 정보", unlockedCodexInfo)
        };

        return string.Join("\n", lines.Where((line) => line != null));
    }

    private static string FormatList(string title, IEnumerable<string> rows)
    {
        List<string> validRows = rows?
            .Where((row) => !string.IsNullOrWhiteSpace(row))
            .ToList()
            ?? new List<string>();

        if (validRows.Count == 0)
        {
            return $"{title}: 없음";
        }

        return $"{title}:\n- {string.Join("\n- ", validRows)}";
    }

    private static string TextOrDefault(string value, string defaultValue)
    {
        return string.IsNullOrWhiteSpace(value) ? defaultValue : value;
    }

    private static string GetStockCategoryName(StockCategory category)
    {
        return StockCategoryPersistenceId.ToId(category);
    }

}

public struct OperatingDayStartedEvent
{
    public int day;

    public OperatingDayStartedEvent(int day)
    {
        this.day = day;
    }

}

public struct OperatingDayEndedEvent
{
    public int day;

    public OperatingDayEndedEvent(int day)
    {
        this.day = day;
    }

}

public readonly struct OperatingDayReportEvent
{
    public OperatingDayReport report { get; }

    public OperatingDayReportEvent(OperatingDayReport report)
    {
        this.report = report;
    }

}

public struct FacilityVisitEvent
{
    public CharacterActor visitorActor;
    public BuildableObject facility;
    public string operationId;

    public FacilityVisitEvent(IBuildingCharacterPort visitor, BuildableObject facility)
        : this(visitor, facility, string.Empty)
    {
    }

    public FacilityVisitEvent(
        IBuildingCharacterPort visitor,
        BuildableObject facility,
        string operationId)
    {
        visitorActor = CharacterBuildingVisitorAdapter.GetActorOrNull(visitor);
        this.facility = facility;
        this.operationId = operationId?.Trim() ?? string.Empty;
    }

}

public sealed class BuildingVisitEventPublisher : IBuildingVisitEventPort
{
    private readonly IGameEventBus gameEventBus;
    private readonly IGameSessionStateProvider gameDataProvider;
    private readonly IDomainFactPublisher facts;
    private readonly IDomainFactRecoveryPublisher recovery;

    [Inject]
    public BuildingVisitEventPublisher(
        IGameEventBus gameEventBus,
        IGameSessionStateProvider gameDataProvider,
        IDomainFactPublisher facts,
        IDomainFactRecoveryPublisher recovery)
    {
        this.gameEventBus = gameEventBus
            ?? throw new ArgumentNullException(nameof(gameEventBus));
        this.gameDataProvider = gameDataProvider
            ?? throw new ArgumentNullException(nameof(gameDataProvider));
        this.facts = facts ?? throw new ArgumentNullException(nameof(facts));
        this.recovery = recovery ?? throw new ArgumentNullException(nameof(recovery));
    }

    public void CommitVisit(
        IBuildingCharacterPort visitor,
        IBuildingWorldEntryPort facility)
    {
        if (facility is not BuildableObject buildableObject)
        {
            throw new ArgumentException(
                $"{nameof(IBuildingVisitEventPort)} only accepts {nameof(BuildableObject)} facilities.",
                nameof(facility));
        }

        long sequence = buildableObject.AllocateFacilityUseOperationSequence();
        BuildingVisitorSnapshot snapshot = visitor is IBuildingVisitorPort full
            ? full.VisitorSnapshot
            : default;
        string facilityId = buildableObject.RequirePersistentInstanceId().Value;
        FacilityVisitCommitResult result = CommitVisit(
            visitor,
            facility,
            new CharacterFacilityUseOutcomeReceipt
            {
                OperationSequence = sequence,
                OperationId = FacilityUseCompletedFact.OperationIdentity(facilityId, sequence),
                ActorId = visitor?.BuildingCharacterId.Value ?? string.Empty,
                ActorDisplayName = visitor?.BuildingDisplayName ?? string.Empty,
                FacilityRole = buildableObject.Facility?.roles.ToString()
                    ?? FacilityRole.None.ToString(),
                Status = CharacterFacilityUseOutcomeStatus.Completed,
                EffectsCommitted = true,
                Before = snapshot,
                After = snapshot
            });
        if (!result.DurablyCommitted)
            throw new InvalidOperationException(
                "Facility visit outcome commit deferred: " + result.DetailCode);
    }

    public FacilityVisitCommitResult CommitVisit(
        IBuildingCharacterPort visitor,
        IBuildingWorldEntryPort facility,
        CharacterFacilityUseOutcomeReceipt receipt)
    {
        if (facility is not BuildableObject buildableObject)
            return FacilityVisitCommitResult.Rejected(
                "facility-visit-target-invalid");
        if (receipt == null || !receipt.IsValid)
            return FacilityVisitCommitResult.Rejected(
                "facility-visit-receipt-invalid");
        string facilityId = buildableObject.RequirePersistentInstanceId().Value;
        if (receipt.OperationId != FacilityUseCompletedFact.OperationIdentity(facilityId, receipt.OperationSequence)
            || receipt.OperationSequence >= buildableObject.FacilityState.nextUseOperationSequence
            || buildableObject.FacilityState.IsUseAcknowledged(receipt.OperationSequence))
            return FacilityVisitCommitResult.Rejected("facility-visit-operation-retired-or-invalid");
        buildableObject.BeginFacilityUseObservation();
        try
        {
            FacilityUseCommitState pending = buildableObject.FacilityState.pendingUses
                .FirstOrDefault(value => value != null
                    && value.operationSequence == receipt.OperationSequence);
            if (pending == null)
            {
                pending = Freeze(receipt);
                pending.absoluteDay = TryResolveCurrentOutcomeDay(out int day) ? day : -1;
                pending.facilityDisplayName = GetFacilityDisplayName(buildableObject, facilityId);
                buildableObject.SavePendingFacilityUse(
                    pending,
                    applyCompletionMutation:
                        receipt.Status == CharacterFacilityUseOutcomeStatus.Completed
                        || (receipt.Status == CharacterFacilityUseOutcomeStatus.Partial
                            && receipt.EffectsCommitted));
            }
            else if (!MatchesReceipt(pending, receipt))
            {
                return FacilityVisitCommitResult.Rejected(
                    "facility-visit-pending-identity-conflict");
            }

            return TryResumePending(buildableObject, pending, visitor, facilityId);
        }
        finally { buildableObject.EndFacilityUseObservation(); }
    }

    public FacilityVisitCommitResult ReconcilePendingVisit(
        IBuildingWorldEntryPort facility)
    {
        if (facility is not BuildableObject buildableObject)
            return FacilityVisitCommitResult.Rejected(
                "facility-visit-target-invalid");
        string facilityId = buildableObject.RequirePersistentInstanceId().Value;
        buildableObject.BeginFacilityUseObservation();
        try
        {
            FacilityVisitCommitResult last = FacilityVisitCommitResult.Committed(string.Empty);
            foreach (FacilityUseCommitState pending in buildableObject.FacilityState.pendingUses
                         .OrderBy(value => value.operationSequence).Select(value => value.Clone()).ToArray())
            {
                last = TryResumePending(buildableObject, pending, visitor: null, facilityId);
                if (!last.DurablyCommitted) return last;
            }
            return last;
        }
        finally { buildableObject.EndFacilityUseObservation(); }
    }

    private bool TryResolveCurrentOutcomeDay(out int absoluteDay)
    {
        absoluteDay = 0;
        return gameDataProvider != null
            && gameDataProvider.TryGetSessionState(out GameSessionState gameData)
            && gameData?.day != null
            && (absoluteDay = gameData.day.Value) >= 0;
    }

    private FacilityVisitCommitResult TryResumePending(
        BuildableObject facility,
        FacilityUseCommitState pending,
        IBuildingCharacterPort visitor,
        string facilityId)
    {
        // Effects already belong to the facility/visitor. Retry only the frozen fact.
        pending = pending.Clone();
        if (pending.absoluteDay < 0)
        {
            if (!TryResolveCurrentOutcomeDay(out int day))
                return FacilityVisitCommitResult.Deferred(pending.publicationKey, "facility-visit-outcome-day-unavailable");
            pending.absoluteDay = day;
            facility.SavePendingFacilityUse(pending);
        }
        var fact = new FacilityUseCompletedFact(facilityId, pending.facilityDisplayName, pending);
        DomainFactDelivery delivery;
        DomainFactLease lease = default;
        try
        {
            if (pending.publicationKey.Length == 0)
            {
                if (!facts.TryReserve(fact, out lease, out string failure))
                    return FacilityVisitCommitResult.Deferred(string.Empty, "facility-visit-publication-reservation-failed:" + failure);
                if (!lease.IsValid || string.IsNullOrWhiteSpace(lease.PublicationKey))
                    return FacilityVisitCommitResult.Deferred(string.Empty, "facility-visit-publication-reservation-invalid");
                pending.publicationKey = lease.PublicationKey;
                pending.phase = (int)FacilityUseCommitPhase.SessionCompleted;
                facility.SavePendingFacilityUse(pending);
                delivery = facts.Publish(lease, fact);
            }
            else
            {
                var results = new DomainFactDelivery[1];
                if (!recovery.ReplayBatch(new[] { DomainFactReplay.Create(pending.publicationKey, fact) }, results, out string failure))
                    return FacilityVisitCommitResult.Deferred(pending.publicationKey, "facility-visit-publication-replay-failed:" + failure);
                delivery = results[0];
            }
        }
        catch (Exception exception) when (exception is not OutOfMemoryException
            && exception is not StackOverflowException && exception is not AccessViolationException)
        {
            return FacilityVisitCommitResult.Deferred(pending.publicationKey, "facility-visit-publication-exception:" + exception.GetType().Name);
        }
        finally { if (lease.IsValid) facts.Cancel(lease); }
        // A durable observer outbox is an acknowledgement too; delayed projection
        // must not turn an already committed facility visit into a failed command.
        if (delivery.Phase is not (DomainFactDeliveryPhase.Delivered or DomainFactDeliveryPhase.AcceptedPendingDelivery)
            || delivery.OperationKey != pending.publicationKey
            || string.IsNullOrWhiteSpace(delivery.PublicationId)
            || string.IsNullOrEmpty(delivery.Fingerprint) || delivery.Fingerprint.Length != 64 || !delivery.Fingerprint.All(Uri.IsHexDigit))
            return FacilityVisitCommitResult.Deferred(pending.publicationKey, "facility-visit-publication-unconfirmed:" + delivery.Detail);

        facility.AcknowledgeFacilityUse(pending.operationSequence);
        PublishPostCommitObserver(
            () => gameEventBus.Publish(
                new FacilityVisitEvent(visitor, facility, pending.operationId)),
            "facility-visit-post-commit-event");
        return FacilityVisitCommitResult.Committed(pending.publicationKey);
    }

    private static bool MatchesReceipt(FacilityUseCommitState pending, CharacterFacilityUseOutcomeReceipt receipt)
    {
        var actual = Freeze(receipt);
        var expected = pending.Clone();
        expected.phase = actual.phase;
        expected.publicationKey = actual.publicationKey;
        expected.absoluteDay = actual.absoluteDay;
        expected.facilityDisplayName = actual.facilityDisplayName;
        return JsonUtility.ToJson(expected) == JsonUtility.ToJson(actual);
    }

    private static FacilityUseCommitState Freeze(
        CharacterFacilityUseOutcomeReceipt receipt)
    {
        string[] conditions = (receipt.After.ActiveConditionIds
                ?? Array.Empty<string>())
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        string[] children = (receipt.ChildResultKeys ?? Array.Empty<string>())
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        return new FacilityUseCommitState
        {
            phase = (int)FacilityUseCommitPhase.EffectsCommitted,
            operationSequence = receipt.OperationSequence,
            operationId = receipt.OperationId.Trim(),
            actorId = receipt.ActorId.Trim(),
            actorDisplayName = string.IsNullOrWhiteSpace(receipt.ActorDisplayName)
                ? receipt.ActorId.Trim()
                : receipt.ActorDisplayName.Trim(),
            facilityRole = receipt.FacilityRole?.Trim() ?? string.Empty,
            serviceSessionId = receipt.ServiceSessionId?.Trim() ?? string.Empty,
            outcomeStatus = (int)receipt.Status,
            effectsCommitted = receipt.EffectsCommitted,
            failureCode = receipt.FailureCode?.Trim() ?? string.Empty,
            healthBefore = receipt.Before.CurrentHealth,
            healthAfter = receipt.After.CurrentHealth,
            maximumHealthBefore = receipt.Before.MaximumHealth,
            maximumHealthAfter = receipt.After.MaximumHealth,
            injuryBefore = receipt.Before.InjurySeverity,
            injuryAfter = receipt.After.InjurySeverity,
            expeditionStressBefore = receipt.Before.ExpeditionStress,
            expeditionStressAfter = receipt.After.ExpeditionStress,
            moodBefore = receipt.Before.Mood,
            moodAfter = receipt.After.Mood,
            hungerBefore = receipt.Before.Hunger,
            hungerAfter = receipt.After.Hunger,
            funBefore = receipt.Before.Fun,
            funAfter = receipt.After.Fun,
            sleepBefore = receipt.Before.Sleep,
            sleepAfter = receipt.After.Sleep,
            excretionBefore = receipt.Before.Excretion,
            excretionAfter = receipt.After.Excretion,
            hygieneBefore = receipt.Before.Hygiene,
            hygieneAfter = receipt.After.Hygiene,
            activeConditionIds = conditions.ToList(),
            childResultKeys = children.ToList()
        };
    }

    private static string GetFacilityDisplayName(
        BuildableObject facility,
        string fallbackId)
    {
        string authoredName = facility?.BuildingData?.objectName;
        return string.IsNullOrWhiteSpace(authoredName)
            ? fallbackId
            : authoredName.Trim();
    }

    private static void PublishPostCommitObserver(
        Action observer,
        string label)
    {
        try
        {
            observer?.Invoke();
        }
        catch (Exception exception) when (
            exception is not OutOfMemoryException
            && exception is not StackOverflowException
            && exception is not AccessViolationException)
        {
            Debug.LogError(label + ":" + exception.GetType().Name);
        }
    }
}

public struct FacilityRevenueEvent
{
    public CharacterActor customerActor;
    public BuildableObject facility;
    public int revenue;

    public FacilityRevenueEvent(IBuildingCharacterPort customer, BuildableObject facility, int revenue)
    {
        customerActor = CharacterBuildingVisitorAdapter.GetActorOrNull(customer);
        this.facility = facility;
        this.revenue = revenue;
    }

}

public struct FacilityStockConsumedEvent
{
    public CharacterActor consumerActor;
    public BuildableObject facility;
    public StockCategory category;
    public int amount;

    public FacilityStockConsumedEvent(IBuildingCharacterPort consumer, BuildableObject facility, StockCategory category, int amount)
    {
        consumerActor = CharacterBuildingVisitorAdapter.GetActorOrNull(consumer);
        this.facility = facility;
        this.category = category;
        this.amount = amount;
    }

}

public enum FacilityCrimeKind
{
    Shoplifting
}

public struct FacilityCrimeEvent
{
    public CharacterActor actor;
    public BuildableObject facility;
    public FacilityCrimeKind kind;
    public string detail;
    public int lossValue;
    public RetailStockLotSnapshot committedLot;
    public string commitOperationId;

    public FacilityCrimeEvent(
        IBuildingCharacterPort actor,
        BuildableObject facility,
        FacilityCrimeKind kind,
        string detail,
        int lossValue,
        RetailStockLotSnapshot committedLot = null,
        string commitOperationId = "")
    {
        this.actor = CharacterBuildingVisitorAdapter.GetActorOrNull(actor);
        this.facility = facility;
        this.kind = kind;
        this.detail = detail ?? string.Empty;
        this.lossValue = Mathf.Max(0, lossValue);
        this.committedLot = committedLot?.Clone();
        this.commitOperationId = commitOperationId ?? string.Empty;
    }

}

public struct FacilityRestockEvent
{
    public BuildableObject facility;
    public int requestedAmount;
    public int restockedAmount;
    public string message;

    public FacilityRestockEvent(BuildableObject facility, int requestedAmount, int restockedAmount, string message)
    {
        this.facility = facility;
        this.requestedAmount = requestedAmount;
        this.restockedAmount = restockedAmount;
        this.message = message;
    }

}
