using System;
using System.Collections.Generic;

/// <summary>
/// One immutable declaration per supported receipt family. Adding a family
/// changes this data registry and its owner adapter, never a dispatch switch in
/// the ledger or item transaction core.
/// </summary>
internal sealed class TradeInventoryOutcomeDefinition
{
    internal TradeInventoryOutcomeDefinition(
        TradeInventoryOutcomeKind kind,
        string slug,
        string koreanLabel,
        GameplayOutcomeTagId domainTag,
        GameplayRoleId narrativeSubjectRole,
        string globalPredicate,
        TradeInventoryPerspectiveFrame[] perspectiveFrames,
        GameplayRoleId[] requiredRoles,
        GameplayMetricId[] requiredMetrics,
        GameplayOutcomeFactId[] requiredFacts)
    {
        Kind = kind;
        Slug = GameplayOutcomeStableIdSyntax.Require(slug, nameof(slug));
        ProducerId = GameplayOutcomeStableIdSyntax.Require(
            TradeInventoryOutcomeIds.ProducerPrefix + Slug,
            nameof(slug));
        KoreanLabel = string.IsNullOrWhiteSpace(koreanLabel)
            ? throw new ArgumentException("A Korean label is required.", nameof(koreanLabel))
            : koreanLabel.Trim();
        DomainTag = domainTag;
        NarrativeSubjectRole = narrativeSubjectRole;
        GlobalPredicate = string.IsNullOrWhiteSpace(globalPredicate)
            ? throw new ArgumentException("A global predicate is required.", nameof(globalPredicate))
            : globalPredicate.Trim();
        PerspectiveFrames = perspectiveFrames
            ?? Array.Empty<TradeInventoryPerspectiveFrame>();
        RequiredRoles = requiredRoles ?? Array.Empty<GameplayRoleId>();
        RequiredMetrics = requiredMetrics ?? Array.Empty<GameplayMetricId>();
        RequiredFacts = requiredFacts ?? Array.Empty<GameplayOutcomeFactId>();
        bool hasNarrativeSubject = false;
        for (int index = 0; index < RequiredRoles.Count; index++)
            hasNarrativeSubject |= RequiredRoles[index].Equals(
                NarrativeSubjectRole);
        if (!hasNarrativeSubject)
        {
            throw new ArgumentException(
                "The narrative subject role must be required by the definition.",
                nameof(narrativeSubjectRole));
        }
        for (int frameIndex = 0; frameIndex < PerspectiveFrames.Count; frameIndex++)
        {
            bool roleKnown = false;
            for (int roleIndex = 0; roleIndex < RequiredRoles.Count; roleIndex++)
                roleKnown |= RequiredRoles[roleIndex].Equals(
                    PerspectiveFrames[frameIndex].Role);
            if (!roleKnown)
            {
                throw new ArgumentException(
                    "Every perspective frame role must be required by the definition.",
                    nameof(perspectiveFrames));
            }
            for (int earlier = 0; earlier < frameIndex; earlier++)
            {
                if (PerspectiveFrames[earlier].Role.Equals(
                        PerspectiveFrames[frameIndex].Role))
                {
                    throw new ArgumentException(
                        "Perspective frame roles must be unique.",
                        nameof(perspectiveFrames));
                }
            }
        }
    }

    internal TradeInventoryOutcomeKind Kind { get; }
    internal string Slug { get; }
    internal string ProducerId { get; }
    internal string KoreanLabel { get; }
    internal GameplayOutcomeTagId DomainTag { get; }
    internal GameplayRoleId NarrativeSubjectRole { get; }
    internal string GlobalPredicate { get; }
    internal IReadOnlyList<TradeInventoryPerspectiveFrame> PerspectiveFrames { get; }
    internal IReadOnlyList<GameplayRoleId> RequiredRoles { get; }
    internal IReadOnlyList<GameplayMetricId> RequiredMetrics { get; }
    internal IReadOnlyList<GameplayOutcomeFactId> RequiredFacts { get; }

    internal bool TryGetPerspectiveFrame(
        GameplayRoleId role,
        out TradeInventoryPerspectiveFrame frame)
    {
        for (int index = 0; index < PerspectiveFrames.Count; index++)
        {
            TradeInventoryPerspectiveFrame candidate = PerspectiveFrames[index];
            if (candidate.Role.Equals(role))
            {
                frame = candidate;
                return true;
            }
        }
        frame = default;
        return false;
    }
}

internal readonly struct TradeInventoryPerspectiveFrame
{
    internal TradeInventoryPerspectiveFrame(GameplayRoleId role, string viewerText)
    {
        Role = role;
        ViewerText = string.IsNullOrWhiteSpace(viewerText)
            ? throw new ArgumentException("A viewer frame is required.", nameof(viewerText))
            : viewerText.Trim();
    }

    internal GameplayRoleId Role { get; }
    internal string ViewerText { get; }
}

internal static class TradeInventoryOutcomeDefinitions
{
    private static readonly TradeInventoryOutcomeDefinition[] Definitions =
    {
        D(TradeInventoryOutcomeKind.FacilityCrime, "facility-crime-event", "시설 범죄", TradeInventoryOutcomeIds.FacilityTag,
            TradeInventoryOutcomeIds.ActorRole, "시설에서 물품을 빼돌렸다",
            V((TradeInventoryOutcomeIds.ActorRole, "시설에서 물품을 빼돌렸다"), (TradeInventoryOutcomeIds.FacilityRole, "보관 중인 물품을 도난당했다"), (TradeInventoryOutcomeIds.ItemRole, "시설에서 도난품으로 반출됐다")),
            R(TradeInventoryOutcomeIds.ActorRole, TradeInventoryOutcomeIds.FacilityRole, TradeInventoryOutcomeIds.ItemRole),
            M(TradeInventoryOutcomeIds.QuantityMetric, TradeInventoryOutcomeIds.LossValueMetric)),
        D(TradeInventoryOutcomeKind.FacilityRevenue, "facility-revenue-event", "시설 매출", TradeInventoryOutcomeIds.EconomyTag,
            TradeInventoryOutcomeIds.FacilityRole, "고객에게 서비스를 제공해 매출을 올렸다",
            V((TradeInventoryOutcomeIds.CustomerRole, "시설 서비스를 이용하고 대금을 지불했다"), (TradeInventoryOutcomeIds.FacilityRole, "고객에게 서비스를 제공해 매출을 올렸다")),
            R(TradeInventoryOutcomeIds.CustomerRole, TradeInventoryOutcomeIds.FacilityRole), M(TradeInventoryOutcomeIds.RevenueMetric)),
        D(TradeInventoryOutcomeKind.FacilityShopPurchased, "facility-shop-purchased-event", "시설 상품 구매", TradeInventoryOutcomeIds.EconomyTag,
            TradeInventoryOutcomeIds.BeneficiaryRole, "시설 상품을 구매했다",
            V((TradeInventoryOutcomeIds.BeneficiaryRole, "시설 상품을 구매했다"), (TradeInventoryOutcomeIds.ItemRole, "구매자에게 판매됐다")),
            R(TradeInventoryOutcomeIds.BeneficiaryRole, TradeInventoryOutcomeIds.ItemRole), M(TradeInventoryOutcomeIds.CostMetric, TradeInventoryOutcomeIds.QuantityMetric)),
        D(TradeInventoryOutcomeKind.FacilityStockConsumed, "facility-stock-consumed-event", "시설 재고 소비", TradeInventoryOutcomeIds.FacilityTag,
            TradeInventoryOutcomeIds.FacilityRole, "운영에 필요한 재고를 소비했다",
            V((TradeInventoryOutcomeIds.FacilityRole, "운영에 필요한 재고를 소비했다"), (TradeInventoryOutcomeIds.ItemRole, "시설 운영 재고로 소비됐다")),
            R(TradeInventoryOutcomeIds.FacilityRole, TradeInventoryOutcomeIds.ItemRole), M(TradeInventoryOutcomeIds.QuantityMetric, TradeInventoryOutcomeIds.CategoryMetric)),
        D(TradeInventoryOutcomeKind.FacilityVisit, "facility-visit-event", "시설 방문", TradeInventoryOutcomeIds.FacilityTag,
            TradeInventoryOutcomeIds.ActorRole, "시설을 방문했다",
            V((TradeInventoryOutcomeIds.ActorRole, "시설을 방문했다"), (TradeInventoryOutcomeIds.FacilityRole, "방문객을 맞았다")),
            R(TradeInventoryOutcomeIds.ActorRole, TradeInventoryOutcomeIds.FacilityRole), M()),
        D(TradeInventoryOutcomeKind.GrandProjectPhysicalInput, "grand-project-physical-input-receipt", "대형 사업 자재 투입", TradeInventoryOutcomeIds.ProgressionTag,
            TradeInventoryOutcomeIds.ProjectRole, "필요한 자재를 넘겨받았다",
            V((TradeInventoryOutcomeIds.ProjectRole, "필요한 자재를 넘겨받았다"), (TradeInventoryOutcomeIds.ItemRole, "대형 사업의 자재로 투입됐다")),
            R(TradeInventoryOutcomeIds.ProjectRole, TradeInventoryOutcomeIds.ItemRole), M(TradeInventoryOutcomeIds.InputQuantityMetric, TradeInventoryOutcomeIds.InputMassMetric)),
        D(TradeInventoryOutcomeKind.GuestRequestDelivery, "guest-request-delivery-receipt", "손님 요청 납품", TradeInventoryOutcomeIds.FacilityTag,
            TradeInventoryOutcomeIds.CustomerRole, "요청한 물품을 납품받았다",
            V((TradeInventoryOutcomeIds.CustomerRole, "요청한 물품을 납품받았다"), (TradeInventoryOutcomeIds.FacilityRole, "손님의 요청 물품을 인도했다"), (TradeInventoryOutcomeIds.ItemRole, "손님의 요청에 따라 납품됐다")),
            R(TradeInventoryOutcomeIds.CustomerRole, TradeInventoryOutcomeIds.FacilityRole, TradeInventoryOutcomeIds.ItemRole), M(TradeInventoryOutcomeIds.InputQuantityMetric, TradeInventoryOutcomeIds.InputMassMetric)),
        D(TradeInventoryOutcomeKind.ItemExtraction, "item-extraction-receipt", "물품 분리", TradeInventoryOutcomeIds.PhysicalTag,
            TradeInventoryOutcomeIds.ItemRole, "원래 묶음에서 분리됐다",
            V((TradeInventoryOutcomeIds.ItemRole, "원래 묶음에서 분리됐다"), (TradeInventoryOutcomeIds.SourceRole, "일부 물품이 분리됐다"), (TradeInventoryOutcomeIds.DestinationRole, "분리된 물품을 넘겨받았다")),
            R(TradeInventoryOutcomeIds.ItemRole, TradeInventoryOutcomeIds.SourceRole, TradeInventoryOutcomeIds.DestinationRole), M(TradeInventoryOutcomeIds.QuantityMetric)),
        D(TradeInventoryOutcomeKind.MetaUpgradePurchased, "meta-upgrade-purchased-event", "계승 강화 구매", TradeInventoryOutcomeIds.ProgressionTag,
            TradeInventoryOutcomeIds.BeneficiaryRole, "계승 강화를 구매했다",
            V((TradeInventoryOutcomeIds.BeneficiaryRole, "계승 강화를 구매했다"), (TradeInventoryOutcomeIds.UpgradeRole, "구매되어 계승 강화에 적용됐다")),
            R(TradeInventoryOutcomeIds.BeneficiaryRole, TradeInventoryOutcomeIds.UpgradeRole), M(TradeInventoryOutcomeIds.CostMetric, TradeInventoryOutcomeIds.BeforeValueMetric, TradeInventoryOutcomeIds.AfterValueMetric)),
        D(TradeInventoryOutcomeKind.PackagedLotTareOutput, "packaged-lot-tare-output-receipt", "포장 용기 배출", TradeInventoryOutcomeIds.PhysicalTag,
            TradeInventoryOutcomeIds.ItemRole, "포장을 벗고 배출됐다",
            V((TradeInventoryOutcomeIds.ItemRole, "포장을 벗고 배출됐다"), (TradeInventoryOutcomeIds.DestinationRole, "포장을 벗은 물품을 넘겨받았다")),
            R(TradeInventoryOutcomeIds.ItemRole, TradeInventoryOutcomeIds.DestinationRole), M(TradeInventoryOutcomeIds.OutputQuantityMetric, TradeInventoryOutcomeIds.OutputMassMetric, TradeInventoryOutcomeIds.TareDestroyedMassMetric)),
        D(TradeInventoryOutcomeKind.PhysicalItemBatchDisposition, "physical-item-batch-disposition-receipt", "물품 묶음 처분", TradeInventoryOutcomeIds.PhysicalTag,
            TradeInventoryOutcomeIds.ItemRole, "한 묶음으로 처분됐다",
            V((TradeInventoryOutcomeIds.ItemRole, "한 묶음으로 처분됐다"), (TradeInventoryOutcomeIds.SourceRole, "보유 물품 일부가 한 묶음으로 처분됐다")),
            R(TradeInventoryOutcomeIds.ItemRole, TradeInventoryOutcomeIds.SourceRole), M(TradeInventoryOutcomeIds.InputQuantityMetric, TradeInventoryOutcomeIds.InputMassMetric, TradeInventoryOutcomeIds.DispositionMetric),
            F(TradeInventoryOutcomeIds.ReasonCodeFact, TradeInventoryOutcomeIds.RequestFingerprintFact)),
        D(TradeInventoryOutcomeKind.PhysicalItemDisposition, "physical-item-disposition-receipt", "물품 처분", TradeInventoryOutcomeIds.PhysicalTag,
            TradeInventoryOutcomeIds.ItemRole, "처분됐다",
            V((TradeInventoryOutcomeIds.ItemRole, "처분됐다"), (TradeInventoryOutcomeIds.SourceRole, "보유 물품 일부가 처분됐다")),
            R(TradeInventoryOutcomeIds.ItemRole, TradeInventoryOutcomeIds.SourceRole), M(TradeInventoryOutcomeIds.InputQuantityMetric, TradeInventoryOutcomeIds.InputMassMetric, TradeInventoryOutcomeIds.DispositionMetric)),
        D(TradeInventoryOutcomeKind.PhysicalItemExactSourcePublication, "physical-item-exact-source-publication-receipt", "정확 물품 생성", TradeInventoryOutcomeIds.PhysicalTag,
            TradeInventoryOutcomeIds.ItemRole, "정확한 출처와 함께 생성됐다",
            V((TradeInventoryOutcomeIds.ItemRole, "정확한 출처와 함께 생성됐다"), (TradeInventoryOutcomeIds.SourceRole, "물품을 생성해 내보냈다"), (TradeInventoryOutcomeIds.DestinationRole, "출처가 확인된 물품을 넘겨받았다")),
            R(TradeInventoryOutcomeIds.ItemRole, TradeInventoryOutcomeIds.SourceRole, TradeInventoryOutcomeIds.DestinationRole), M(TradeInventoryOutcomeIds.OutputQuantityMetric, TradeInventoryOutcomeIds.OutputMassMetric)),
        D(TradeInventoryOutcomeKind.PhysicalItemRelocation, "physical-item-relocation-receipt", "물품 이동", TradeInventoryOutcomeIds.PhysicalTag,
            TradeInventoryOutcomeIds.ItemRole, "새 위치로 옮겨졌다",
            V((TradeInventoryOutcomeIds.ItemRole, "새 위치로 옮겨졌다"), (TradeInventoryOutcomeIds.SourceRole, "보유 물품을 다른 위치로 보냈다"), (TradeInventoryOutcomeIds.DestinationRole, "옮겨진 물품을 넘겨받았다")),
            R(TradeInventoryOutcomeIds.ItemRole, TradeInventoryOutcomeIds.SourceRole, TradeInventoryOutcomeIds.DestinationRole), M(TradeInventoryOutcomeIds.QuantityMetric, TradeInventoryOutcomeIds.MassMetric, TradeInventoryOutcomeIds.DestinationXMetric, TradeInventoryOutcomeIds.DestinationYMetric)),
        D(TradeInventoryOutcomeKind.PhysicalItemSourcePublication, "physical-item-source-publication-receipt", "물품 생성", TradeInventoryOutcomeIds.PhysicalTag,
            TradeInventoryOutcomeIds.ItemRole, "생성되어 배치됐다",
            V((TradeInventoryOutcomeIds.ItemRole, "생성되어 배치됐다"), (TradeInventoryOutcomeIds.SourceRole, "물품을 생성해 내보냈다"), (TradeInventoryOutcomeIds.DestinationRole, "새로 생성된 물품을 넘겨받았다")),
            R(TradeInventoryOutcomeIds.ItemRole, TradeInventoryOutcomeIds.SourceRole, TradeInventoryOutcomeIds.DestinationRole), M(TradeInventoryOutcomeIds.OutputQuantityMetric, TradeInventoryOutcomeIds.OutputMassMetric)),
        D(TradeInventoryOutcomeKind.PhysicalItemTransform, "physical-item-transform-receipt", "물품 변환", TradeInventoryOutcomeIds.PhysicalTag,
            TradeInventoryOutcomeIds.ItemRole, "새 물품으로 변환됐다",
            V((TradeInventoryOutcomeIds.SourceRole, "원재료 일부가 변환에 사용됐다"), (TradeInventoryOutcomeIds.ItemRole, "새 물품으로 변환됐다"), (TradeInventoryOutcomeIds.DestinationRole, "변환된 물품을 넘겨받았다")),
            R(TradeInventoryOutcomeIds.SourceRole, TradeInventoryOutcomeIds.ItemRole, TradeInventoryOutcomeIds.DestinationRole), M(TradeInventoryOutcomeIds.InputQuantityMetric, TradeInventoryOutcomeIds.InputMassMetric, TradeInventoryOutcomeIds.OutputQuantityMetric, TradeInventoryOutcomeIds.OutputMassMetric, TradeInventoryOutcomeIds.LossMassMetric)),
        D(TradeInventoryOutcomeKind.RegionalSupplyDeliveryTransfer, "regional-supply-delivery-transfer-receipt", "지역 공급 납품", TradeInventoryOutcomeIds.EconomyTag,
            TradeInventoryOutcomeIds.ContractRole, "약정된 지역 공급 물품을 인도했다",
            V((TradeInventoryOutcomeIds.ContractRole, "약정된 지역 공급 물품을 인도했다"), (TradeInventoryOutcomeIds.ItemRole, "지역 공급 물품으로 납품됐다"), (TradeInventoryOutcomeIds.DestinationRole, "지역 공급 물품을 넘겨받았다")),
            R(TradeInventoryOutcomeIds.ContractRole, TradeInventoryOutcomeIds.ItemRole, TradeInventoryOutcomeIds.DestinationRole), M(TradeInventoryOutcomeIds.InputQuantityMetric, TradeInventoryOutcomeIds.InputMassMetric)),
        D(TradeInventoryOutcomeKind.ReservedRetailStockTransfer, "reserved-retail-stock-transfer-receipt", "예약 판매 재고 이전", TradeInventoryOutcomeIds.PhysicalTag,
            TradeInventoryOutcomeIds.ItemRole, "예약 판매 재고로 옮겨졌다",
            V((TradeInventoryOutcomeIds.ItemRole, "예약 판매 재고로 옮겨졌다"), (TradeInventoryOutcomeIds.SourceRole, "보유 재고를 예약 판매용으로 보냈다"), (TradeInventoryOutcomeIds.DestinationRole, "예약 판매 재고를 넘겨받았다")),
            R(TradeInventoryOutcomeIds.ItemRole, TradeInventoryOutcomeIds.SourceRole, TradeInventoryOutcomeIds.DestinationRole), M(TradeInventoryOutcomeIds.QuantityMetric, TradeInventoryOutcomeIds.MassMetric)),
        D(TradeInventoryOutcomeKind.ResourceStockPolicySaleTransfer, "resource-stock-policy-sale-transfer-receipt", "초과 재고 판매", TradeInventoryOutcomeIds.EconomyTag,
            TradeInventoryOutcomeIds.ItemRole, "초과 재고로 판매됐다",
            V((TradeInventoryOutcomeIds.ItemRole, "초과 재고로 판매됐다"), (TradeInventoryOutcomeIds.SourceRole, "초과 재고를 판매처로 보냈다"), (TradeInventoryOutcomeIds.DestinationRole, "판매된 초과 재고를 넘겨받았다")),
            R(TradeInventoryOutcomeIds.ItemRole, TradeInventoryOutcomeIds.SourceRole, TradeInventoryOutcomeIds.DestinationRole), M(TradeInventoryOutcomeIds.InputQuantityMetric, TradeInventoryOutcomeIds.InputMassMetric, TradeInventoryOutcomeIds.RevenueMetric)),
        D(TradeInventoryOutcomeKind.StockSupply, "stock-supply-event", "재고 입고", TradeInventoryOutcomeIds.EconomyTag,
            TradeInventoryOutcomeIds.DestinationRole, "새 재고를 입고했다",
            V((TradeInventoryOutcomeIds.ItemRole, "새 재고로 입고됐다"), (TradeInventoryOutcomeIds.DestinationRole, "새 재고를 입고했다")),
            R(TradeInventoryOutcomeIds.ItemRole, TradeInventoryOutcomeIds.DestinationRole), M(TradeInventoryOutcomeIds.QuantityMetric, TradeInventoryOutcomeIds.CostMetric)),
        D(TradeInventoryOutcomeKind.WarehouseMassAdmission, "warehouse-mass-admission-receipt", "창고 반입", TradeInventoryOutcomeIds.PhysicalTag,
            TradeInventoryOutcomeIds.WarehouseRole, "물품을 창고에 반입했다",
            V((TradeInventoryOutcomeIds.WarehouseRole, "물품을 창고에 반입했다"), (TradeInventoryOutcomeIds.ItemRole, "창고에 반입됐다")),
            R(TradeInventoryOutcomeIds.WarehouseRole, TradeInventoryOutcomeIds.ItemRole), M(TradeInventoryOutcomeIds.QuantityMetric, TradeInventoryOutcomeIds.MassMetric)),
        D(TradeInventoryOutcomeKind.WorldResourceRenewableDebit, "world-resource-renewable-debit-receipt", "재생 자원 채취", TradeInventoryOutcomeIds.WorldResourceTag,
            TradeInventoryOutcomeIds.ResourceRole, "일부가 채취됐다",
            V((TradeInventoryOutcomeIds.ResourceRole, "일부가 채취됐다"), (TradeInventoryOutcomeIds.SourceRole, "재생 자원에서 자원을 채취했다")),
            R(TradeInventoryOutcomeIds.ResourceRole, TradeInventoryOutcomeIds.SourceRole), M(TradeInventoryOutcomeIds.QuantityMetric, TradeInventoryOutcomeIds.BeforeValueMetric, TradeInventoryOutcomeIds.AfterValueMetric)),
        D(TradeInventoryOutcomeKind.WastePolicyCommand, "waste-policy-command-result", "폐기물 정책 변경", TradeInventoryOutcomeIds.EconomyTag,
            TradeInventoryOutcomeIds.PolicyRole, "처리 정책이 변경됐다",
            V((TradeInventoryOutcomeIds.PolicyRole, "처리 정책이 변경됐다")),
            R(TradeInventoryOutcomeIds.PolicyRole),
            M(TradeInventoryOutcomeIds.BeforeDispositionMetric, TradeInventoryOutcomeIds.AfterDispositionMetric, TradeInventoryOutcomeIds.BeforeEnabledMetric, TradeInventoryOutcomeIds.AfterEnabledMetric, TradeInventoryOutcomeIds.BeforePercentMetric, TradeInventoryOutcomeIds.AfterPercentMetric))
    };

    internal static TradeInventoryOutcomeDefinition Get(
        TradeInventoryOutcomeKind kind)
    {
        int index = (int)kind - 1;
        if (index < 0 || index >= Definitions.Length
            || Definitions[index].Kind != kind)
            throw new ArgumentOutOfRangeException(nameof(kind));
        return Definitions[index];
    }

    internal static IReadOnlyList<TradeInventoryOutcomeDefinition> All =>
        Definitions;

    private static TradeInventoryOutcomeDefinition D(
        TradeInventoryOutcomeKind kind,
        string slug,
        string label,
        GameplayOutcomeTagId tag,
        GameplayRoleId narrativeSubjectRole,
        string globalPredicate,
        TradeInventoryPerspectiveFrame[] perspectiveFrames,
        GameplayRoleId[] roles,
        GameplayMetricId[] metrics,
        GameplayOutcomeFactId[] facts = null) =>
        new(
            kind,
            slug,
            label,
            tag,
            narrativeSubjectRole,
            globalPredicate,
            perspectiveFrames,
            roles,
            metrics,
            facts);

    private static GameplayRoleId[] R(params GameplayRoleId[] values) => values;
    private static GameplayMetricId[] M(params GameplayMetricId[] values) => values;
    private static GameplayOutcomeFactId[] F(
        params GameplayOutcomeFactId[] values) => values;
    private static TradeInventoryPerspectiveFrame[] V(
        params (GameplayRoleId role, string text)[] values)
    {
        TradeInventoryPerspectiveFrame[] frames =
            new TradeInventoryPerspectiveFrame[values.Length];
        for (int index = 0; index < values.Length; index++)
            frames[index] = new TradeInventoryPerspectiveFrame(
                values[index].role,
                values[index].text);
        return frames;
    }
}
