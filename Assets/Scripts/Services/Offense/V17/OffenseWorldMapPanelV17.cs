using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

public partial class OffenseWorldMapPanel
{
    private enum V17SurfaceKind
    {
        Map,
        Factions,
        Decision,
        Battle
    }

    private const float HexWidth = 39f;
    private const float HexHeight = 34f;
    private const float HexHorizontalStep = 30f;
    private const float HexVerticalStep = 34f;

    private readonly List<GameObject> spawnedV17Objects = new List<GameObject>();
    private readonly List<CharacterActor> selectedV17Members =
        new List<CharacterActor>();
    private IOffenseWorldSimulation v17World;
    private IOffenseTravelRuntime v17Travel;
    private IOffenseDecisionRuntime v17Decisions;
    private IOffenseReturnSafetyRuntime v17Safety;
    private IOffenseBattleDirector v17BattleDirector;
    private IOffenseBattleRuntime v17BattleRuntime;
    private ICombatCardPresentationService v17CardPresentation;
    private IOffenseUrgentMitigationRuntime v17Mitigation;
    private IOffensePreparationService v17Preparation;
    private IOffenseExpeditionRuntimeProvider v17ExpeditionProvider;
    private ITmpKoreanFontService v17Font;
    private IExternalInfluenceRuntime v17ExternalInfluence;
    private IFactionRuntime v17Factions;
    private IInvasionCampaignRuntime v17Campaign;
    private OffenseExpeditionRuntime boundV17Expedition;
    private RectTransform v17MapRoot;
    private OffenseWorldMapResponsiveLayout v17ResponsiveLayout;
    private string selectedV17SiteId = string.Empty;
    private OffenseHexCoord selectedV17Coord;
    private string pendingCardCharacterId = string.Empty;
    private string pendingCardInstanceId = string.Empty;
    private string pendingIntelSiteId = string.Empty;
    private ExpeditionIntelPaymentMethod pendingIntelPayment;
    private int pendingIntelExpiresDay;
    private string v17Status = string.Empty;
    private int selectedV17FieldFunds;
    private bool showV17FactionSurface;
    private string selectedV17FactionId = string.Empty;
    private string selectedV17HumanBranchId = string.Empty;
    private string pendingV17BetrayalFactionId = string.Empty;
    private V17SurfaceKind activeV17Surface = V17SurfaceKind.Map;

    [Inject]
    public void ConstructV17(
        IOffenseWorldSimulation world,
        IOffenseTravelRuntime travel,
        IOffenseDecisionRuntime decisions,
        IOffenseReturnSafetyRuntime safety,
        IOffenseBattleDirector battleDirector,
        IOffenseBattleRuntime battleRuntime,
        ICombatCardPresentationService cardPresentation,
        IOffenseUrgentMitigationRuntime mitigation,
        IOffensePreparationService preparation,
        IOffenseExpeditionRuntimeProvider expeditionProvider,
        ITmpKoreanFontService font,
        IExternalInfluenceRuntime externalInfluence,
        IFactionRuntime factions,
        IInvasionCampaignRuntime campaign)
    {
        v17World = world ?? throw new ArgumentNullException(nameof(world));
        v17Travel = travel ?? throw new ArgumentNullException(nameof(travel));
        v17Decisions = decisions ?? throw new ArgumentNullException(nameof(decisions));
        v17Safety = safety ?? throw new ArgumentNullException(nameof(safety));
        v17BattleDirector = battleDirector
            ?? throw new ArgumentNullException(nameof(battleDirector));
        v17BattleRuntime = battleRuntime
            ?? throw new ArgumentNullException(nameof(battleRuntime));
        v17CardPresentation = cardPresentation
            ?? throw new ArgumentNullException(nameof(cardPresentation));
        v17Mitigation = mitigation
            ?? throw new ArgumentNullException(nameof(mitigation));
        v17Preparation = preparation
            ?? throw new ArgumentNullException(nameof(preparation));
        v17ExpeditionProvider = expeditionProvider
            ?? throw new ArgumentNullException(nameof(expeditionProvider));
        v17Font = font ?? throw new ArgumentNullException(nameof(font));
        v17ExternalInfluence = externalInfluence
            ?? throw new ArgumentNullException(nameof(externalInfluence));
        v17Factions = factions
            ?? throw new ArgumentNullException(nameof(factions));
        v17Campaign = campaign
            ?? throw new ArgumentNullException(nameof(campaign));
        selectedV17Coord = v17World.DungeonCoord;
        v17World.Changed += RenderV17IfVisible;
        v17BattleDirector.Changed += RenderV17IfVisible;
        v17Mitigation.Changed += RenderV17IfVisible;
        BindV17ExpeditionRuntime();
    }

    internal void BindV17GeneratedView(
        RectTransform mapRoot,
        OffenseWorldMapResponsiveLayout responsiveLayout)
    {
        v17MapRoot = mapRoot
            ?? throw new ArgumentNullException(nameof(mapRoot));
        v17ResponsiveLayout = responsiveLayout
            ?? throw new ArgumentNullException(nameof(responsiveLayout));
    }

    private bool CanRenderV17()
    {
        return v17World != null
            && v17MapRoot != null
            && v17World.Tiles != null
            && v17World.Tiles.Count > 0;
    }

    private void RenderV17()
    {
        if (!CanRenderV17())
        {
            return;
        }

        BindV17ExpeditionRuntime();
        ClearButtons();
        ClearV17Objects();

        OffenseExpeditionRun expedition = boundV17Expedition?
            .ActiveExpeditions
            .FirstOrDefault(active => active != null && active.UsesV17WorldTravel);
        headerText.text = BuildV17Header(expedition);

        if (expedition != null
            && v17BattleDirector.State != null
            && v17BattleRuntime.HasActiveBattle)
        {
            PrepareV17Surface(V17SurfaceKind.Battle);
            RenderV17Battle(expedition);
            return;
        }

        if (expedition != null
            && v17Decisions.TryGetActiveDecision(
                expedition.ExpeditionId,
                out OffenseDecisionView decision))
        {
            PrepareV17Surface(V17SurfaceKind.Decision);
            RenderV17Decision(expedition, decision);
            return;
        }

        if (showV17FactionSurface)
        {
            PrepareV17Surface(V17SurfaceKind.Factions);
            RenderV17HexMap(expedition);
            RenderV17FactionMarkers();
            RenderV17FactionSidebar();
            return;
        }

        PrepareV17Surface(V17SurfaceKind.Map);
        RenderV17HexMap(expedition);
        RenderV17FactionMarkers();
        RenderV17Sidebar(expedition);
    }

    private void RenderV17HexMap(OffenseExpeditionRun expedition)
    {
        OffenseTravelStateData travelState = null;
        if (expedition != null)
        {
            v17Travel.TryGetState(expedition.ExpeditionId, out travelState);
        }

        Dictionary<OffenseHexCoord, OffenseWorldSiteStateData> sites =
            v17World.Sites
                .Where(site => site != null
                    && site.IsActive
                    && site.state != OffenseWorldSiteState.Hidden)
                .GroupBy(site => site.Coord)
                .ToDictionary(group => group.Key, group => group.First());
        Dictionary<OffenseHexCoord, OffenseUrgentSiteStateData> urgentSites =
            v17World.UrgentSites
                .Where(site => site != null && site.IsActive)
                .GroupBy(site => site.Coord)
                .ToDictionary(group => group.Key, group => group.First());

        foreach (OffenseHexTileState tile in v17World.Tiles
                     .Where(value => value != null)
                     .OrderBy(value => value.r)
                     .ThenBy(value => value.q))
        {
            OffenseHexCoord coord = tile.Coord;
            sites.TryGetValue(coord, out OffenseWorldSiteStateData site);
            urgentSites.TryGetValue(coord, out OffenseUrgentSiteStateData urgent);
            bool isDungeon = coord == v17World.DungeonCoord;
            bool isParty = travelState != null && coord == travelState.CurrentCoord;
            CreateHexCell(tile, site, urgent, isDungeon, isParty);
        }
    }

    private void RenderV17Sidebar(OffenseExpeditionRun expedition)
    {
        if (expedition == null)
        {
            RenderV17PreparationSidebar();
            return;
        }

        v17Travel.TryGetState(
            expedition.ExpeditionId,
            out OffenseTravelStateData travel);
        OffenseReturnSafetySnapshot safety = v17Safety.Get(expedition.ExpeditionId);
        OffenseSupplyPackingSnapshot packing =
            v17Preparation.GetPackingSnapshot(expedition.ExpeditionId);
        AddRightButton(
            "선택한 칸으로 이동",
            () =>
            {
                if (travel == null)
                {
                    v17Status = "이동 상태를 찾을 수 없습니다.";
                }
                else if (selectedV17Coord == travel.CurrentCoord)
                {
                    v17Status = "원정대가 이미 이 칸에 있습니다.";
                }
                else
                {
                    bool startsAttack = IsSelectedActiveSite(
                        out string destinationSiteId);
                    if (!boundV17Expedition.TryRedirectV17Expedition(
                            expedition.ExpeditionId,
                            selectedV17Coord,
                            startsAttack ? destinationSiteId : string.Empty,
                            startsAttack,
                            out v17Status))
                    {
                        RenderV17();
                        return;
                    }

                }

                RenderV17();
            });
        AddUrgentMitigationButtonIfSelected();
        AddRightButton("세력", OpenV17FactionSurface);
        AddRightButton("지도 맞춤", ResetV17MapView);
        AddRightButton("닫기", Hide);

        string destination = travel != null
            ? $"({travel.destinationQ}, {travel.destinationR})"
            : "-";
        int remaining = travel?.remainingPath?.Count ?? 0;
        detailText.text =
            $"원정대 이동\n"
            + $"현재: ({travel?.currentQ ?? 0}, {travel?.currentR ?? 0})\n"
            + $"목적지: {destination}\n"
            + $"남은 이동: {remaining}칸\n"
            + $"안전 이동: {safety.SafeStepBudget}칸\n"
            + $"노출도: {travel?.exposure ?? 0f:0.#}\n"
            + BuildPackingStatus(packing)
            + $"단계: {GetExpeditionPhaseLabel(expedition.Phase)}\n\n"
            + BuildSelectedLocationDetail()
            + BuildStatusText();
    }

    private void RenderV17PreparationSidebar()
    {
        selectedV17Members.RemoveAll(member =>
            member == null
            || !boundV17Expedition.GetAvailableMemberActors().Contains(member));
        AddRightButton("원정대 편성", () => { });
        foreach (CharacterActor actor in boundV17Expedition
                     .GetAvailableMemberActors()
                     .Take(8))
        {
            CharacterActor captured = actor;
            bool selected = selectedV17Members.Contains(captured);
            AddRightButton(
                $"{(selected ? "●" : "○")} {GetActorLabel(captured)}",
                () =>
                {
                    if (selectedV17Members.Contains(captured))
                    {
                        selectedV17Members.Remove(captured);
                    }
                    else if (selectedV17Members.Count < 5)
                    {
                        selectedV17Members.Add(captured);
                    }
                    else
                    {
                        v17Status = "원정대는 최대 5명입니다.";
                    }

                    RenderV17();
                },
                selected
                    ? new Color(0.2f, 0.42f, 0.32f, 1f)
                    : new Color(0.18f, 0.2f, 0.23f, 1f));
        }

        AddRightButton(
            $"현장 자금 -100  ({selectedV17FieldFunds})",
            () =>
            {
                selectedV17FieldFunds = Mathf.Max(
                    0,
                    selectedV17FieldFunds - 100);
                RenderV17();
            });
        AddRightButton(
            $"현장 자금 +100  ({selectedV17FieldFunds})",
            () =>
            {
                selectedV17FieldFunds += 100;
                RenderV17();
            });
        AddIntelPurchaseButtonsIfSelected();

        AddRightButton(
            "선택 거점으로 출정",
            () =>
            {
                if (string.IsNullOrWhiteSpace(selectedV17SiteId))
                {
                    v17Status = "지도에서 공격할 거점을 선택하세요.";
                }
                else if (TryStartPreparedV17Expedition(
                    out string message))
                {
                    selectedV17Members.Clear();
                    selectedV17FieldFunds = 0;
                    v17Status = message;
                }
                else
                {
                    v17Status = message;
                }

                RenderV17();
            },
            new Color(0.48f, 0.2f, 0.16f, 1f));
        AddUrgentMitigationButtonIfSelected();
        AddRightButton("세력", OpenV17FactionSurface);
        AddRightButton("지도 맞춤", ResetV17MapView);
        AddRightButton("닫기", Hide);

        detailText.text =
            $"{BuildSelectedLocationDetail()}\n\n"
            + $"선발 인원: {selectedV17Members.Count}/5\n"
            + $"현장 자금: {selectedV17FieldFunds} 골드\n"
            + "전열 2 · 중열 2 · 후열 1 순으로 배치됩니다."
            + BuildThreatDetail()
            + BuildStatusText();
    }

    private bool TryStartPreparedV17Expedition(out string message)
    {
        OffenseExpeditionPreparation source =
            v17Preparation.Evaluate().Preparation;
        OffenseExpeditionPreparation preparation =
            new OffenseExpeditionPreparation(
                source.SupplyCapacity,
                source.StartingLight,
                source.CampHealRatio,
                source.CampStressRecovery,
                source.MedicineHealRatio,
                source.Scouting,
                source.SourceSummaries,
                selectedV17FieldFunds);
        return boundV17Expedition.TryStartExpedition(
            selectedV17SiteId,
            selectedV17Members,
            new OffenseSupplyLoadout(),
            preparation,
            out _,
            out message);
    }

    private void OpenV17FactionSurface()
    {
        showV17FactionSurface = true;
        pendingV17BetrayalFactionId = string.Empty;
        RenderV17();
    }

    private void RenderV17FactionMarkers()
    {
        if (v17Factions == null || v17Campaign == null)
        {
            return;
        }

        foreach (DungeonFactionState faction in v17Factions.Factions)
        {
            DungeonFactionDefinitionSO definition =
                v17Factions.Definitions.FirstOrDefault(value =>
                    value != null
                    && value.StableId == faction.factionId);
            CreateV17StrategicMarker(
                faction.discovered
                    ? definition?.displayName ?? faction.factionId
                    : "미탐사 던전",
                faction.HomeCoord,
                new Color(0.36f, 0.75f, 0.52f, 1f));
        }

        foreach (HumanSupportSiteState site in v17Campaign.SupportSites
                     .Where(value => value != null && value.alive))
        {
            CreateV17StrategicMarker(
                "인간 지원",
                site.Coord,
                site.connected
                    ? new Color(0.9f, 0.32f, 0.24f, 1f)
                    : new Color(0.48f, 0.4f, 0.38f, 1f));
        }

        foreach (FactionRouteState route in v17Factions.Routes.Where(value =>
                     value != null
                     && value.status is FactionRouteStatus.Traveling
                         or FactionRouteStatus.Delayed))
        {
            CreateV17StrategicMarker(
                route.kind == FactionRouteKind.Reinforcement
                    ? "지원군"
                    : "상단",
                route.CurrentCoord,
                new Color(0.95f, 0.78f, 0.28f, 1f));
        }
    }

    private void CreateV17StrategicMarker(
        string label,
        OffenseHexCoord coord,
        Color color)
    {
        GameObject marker = OffensePanelUiFactory.CreateText(
            v17MapRoot,
            "FactionMarker",
            10f,
            TextAlignmentOptions.Center,
            v17Font);
        RectTransform rect = marker.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0f);
        rect.sizeDelta = new Vector2(92f, 22f);
        rect.anchoredPosition =
            HexToMapPosition(coord) + new Vector2(0f, 13f);
        TMP_Text text = marker.GetComponent<TMP_Text>();
        text.text = label;
        text.color = color;
        text.raycastTarget = false;
        spawnedV17Objects.Add(marker);
    }

    private void RenderV17FactionSidebar()
    {
        headerText.text =
            $"세력 · 던전 팩션 {v17Factions.Factions.Count}"
            + $" · 인간 지부 {v17Campaign.Branches.Count}"
            + $" · 이동 경로 {v17Factions.Routes.Count}";
        AddRightButton(
            "← 월드 지도",
            () =>
            {
                showV17FactionSurface = false;
                pendingV17BetrayalFactionId = string.Empty;
                RenderV17();
            });

        foreach (DungeonFactionState faction in v17Factions.Factions)
        {
            DungeonFactionState captured = faction;
            DungeonFactionDefinitionSO definition =
                v17Factions.Definitions.FirstOrDefault(value =>
                    value != null
                    && value.StableId == captured.factionId);
            AddRightButton(
                $"{(captured.factionId == selectedV17FactionId ? "●" : "○")} "
                    + $"{definition?.displayName ?? captured.factionId} "
                    + $"[{captured.trust}]",
                () =>
                {
                    selectedV17FactionId = captured.factionId;
                    selectedV17HumanBranchId = string.Empty;
                    pendingV17BetrayalFactionId = string.Empty;
                    v17Status = string.Empty;
                    RenderV17();
                });
        }

        foreach (HumanInvasionBranchState branch in v17Campaign.Branches)
        {
            HumanInvasionBranchState captured = branch;
            AddRightButton(
                $"{(captured.branchId == selectedV17HumanBranchId ? "●" : "○")} "
                    + $"{captured.displayName} {captured.strength:0}",
                () =>
                {
                    selectedV17HumanBranchId = captured.branchId;
                    selectedV17FactionId = string.Empty;
                    pendingV17BetrayalFactionId = string.Empty;
                    v17Status = string.Empty;
                    RenderV17();
                },
                new Color(0.38f, 0.18f, 0.16f, 1f));
        }

        if (!string.IsNullOrWhiteSpace(selectedV17FactionId)
            && v17Factions.TryGetFaction(
                selectedV17FactionId,
                out DungeonFactionState selectedFaction))
        {
            RenderV17FactionCommands(selectedFaction);
            detailText.text = BuildV17FactionDetail(selectedFaction)
                + BuildStatusText();
        }
        else if (!string.IsNullOrWhiteSpace(selectedV17HumanBranchId)
            && v17Campaign.TryGetBranch(
                selectedV17HumanBranchId,
                out HumanInvasionBranchState selectedBranch))
        {
            detailText.text = BuildV17HumanBranchDetail(selectedBranch)
                + BuildStatusText();
        }
        else
        {
            detailText.text =
                "던전 팩션을 선택해 신뢰·계약·지원군을 관리하거나,\n"
                + "인간 지부를 선택해 전력과 지원 거점 회복 원인을 확인하십시오.";
        }

        AddRightButton("지도 맞춤", ResetV17MapView);
        AddRightButton("닫기", Hide);
    }

    private void RenderV17FactionCommands(DungeonFactionState faction)
    {
        AddRightButton(
            "호의 물자 50 전달",
            () =>
            {
                v17Factions.TryOfferGoodwill(
                    faction.factionId,
                    50,
                    out v17Status);
                RenderV17();
            },
            new Color(0.18f, 0.34f, 0.25f, 1f));
        if (v17Factions.IsContractUnlocked(
                faction.factionId,
                FactionContractKind.Trade))
        {
            AddRightButton(
                "교역 상단 요청",
                () =>
                {
                    v17Factions.TryRequestTrade(
                        faction.factionId,
                        out _,
                        out v17Status);
                    RenderV17();
                });
        }

        if (v17Factions.IsContractUnlocked(
                faction.factionId,
                FactionContractKind.Supply))
        {
            AddRightButton(
                "물자 지원 요청",
                () =>
                {
                    v17Factions.TryRequestSupply(
                        faction.factionId,
                        out _,
                        out v17Status);
                    RenderV17();
                });
        }

        if (faction.trust >= 70 && !faction.allianceProjectCompleted)
        {
            AddRightButton(
                "동맹 프로젝트 완료",
                () =>
                {
                    v17Factions.TryCompleteAllianceProject(
                        faction.factionId,
                        out v17Status);
                    RenderV17();
                });
        }

        if (v17Factions.IsContractUnlocked(
                faction.factionId,
                FactionContractKind.Reinforcement))
        {
            AddRightButton(
                "지원군 요청",
                () =>
                {
                    v17Factions.TryRequestReinforcement(
                        faction.factionId,
                        out _,
                        out v17Status);
                    RenderV17();
                },
                new Color(0.22f, 0.32f, 0.42f, 1f));
        }

        bool confirming = string.Equals(
            pendingV17BetrayalFactionId,
            faction.factionId,
            StringComparison.Ordinal);
        AddRightButton(
            confirming
                ? "배신 확정 · 실물 약탈 최대 300"
                : "동맹 던전 공격",
            () =>
            {
                if (!confirming)
                {
                    pendingV17BetrayalFactionId = faction.factionId;
                    v17Status =
                        "대상 신뢰 -100, 다른 던전 -15, 10일 협상 봉쇄. "
                        + "같은 버튼을 다시 눌러 확정하십시오.";
                }
                else
                {
                    v17Factions.TryBetray(
                        faction.factionId,
                        300,
                        out v17Status);
                    pendingV17BetrayalFactionId = string.Empty;
                }
                RenderV17();
            },
            new Color(0.46f, 0.16f, 0.14f, 1f));
    }

    private string BuildV17FactionDetail(DungeonFactionState faction)
    {
        DungeonFactionDefinitionSO definition =
            v17Factions.Definitions.FirstOrDefault(value =>
                value != null && value.StableId == faction.factionId);
        string contracts = string.Join(
            " · ",
            Enum.GetValues(typeof(FactionContractKind))
                .Cast<FactionContractKind>()
                .Where(kind => v17Factions.IsContractUnlocked(
                    faction.factionId,
                    kind))
                .Select(kind => kind.ToString()));
        return $"{definition?.displayName ?? faction.factionId}\n"
            + $"{definition?.description}\n"
            + $"거점: ({faction.homeQ}, {faction.homeR})\n"
            + $"신뢰 {faction.trust} / 배신의 흔적 {faction.betrayalScars}\n"
            + $"해금 계약: {(string.IsNullOrWhiteSpace(contracts) ? "없음" : contracts)}\n"
            + $"협상 봉쇄 종료: Day {faction.negotiationBlockedUntilDay}\n"
            + $"지원군 손실: 사망 {faction.reinforcementDeaths} · 장비 {faction.equipmentLosses}\n"
            + $"복구 배상 요구: {faction.restitutionRequiredValue}";
    }

    private string BuildV17HumanBranchDetail(
        HumanInvasionBranchState branch)
    {
        HumanSupportSiteState[] sites = v17Campaign.SupportSites
            .Where(site => site != null && site.branchId == branch.branchId)
            .ToArray();
        return $"{branch.displayName}\n"
            + $"전력 {branch.strength:0}/100 · "
            + $"{(branch.operational ? "작전 가능" : "작전 불능")}\n"
            + $"회복: {branch.lastRecoveryAmount:0}/일 · {branch.recoveryReason}\n"
            + string.Join(
                "\n",
                sites.Select(site =>
                    $"{site.displayName}: "
                    + $"{(site.alive ? "생존" : "파괴")} / "
                    + $"{(site.connected ? "연결" : "차단")} / "
                    + $"({site.q}, {site.r})"));
    }

    private void RenderV17Decision(
        OffenseExpeditionRun expedition,
        OffenseDecisionView decision)
    {
        TMP_Text title = CreateMapText(
            "DecisionTitle",
            decision.title,
            31f,
            TextAlignmentOptions.Center,
            new Vector2(0.08f, 0.68f),
            new Vector2(0.92f, 0.92f));
        title.color = new Color(0.92f, 0.84f, 0.68f, 1f);
        CreateMapText(
            "DecisionSituation",
            decision.situation,
            22f,
            TextAlignmentOptions.Center,
            new Vector2(0.14f, 0.4f),
            new Vector2(0.86f, 0.68f));

        OffenseDecisionChoiceView[] choices =
            decision.choices?.Take(2).ToArray()
            ?? Array.Empty<OffenseDecisionChoiceView>();
        for (int index = 0; index < choices.Length; index++)
        {
            OffenseDecisionChoiceView choice = choices[index];
            float left = index == 0 ? 0.08f : 0.52f;
            CreateMapButton(
                $"{choice.Label}\n{choice.DirectionLabel}",
                new Vector2(left, 0.12f),
                new Vector2(left + 0.4f, 0.38f),
                () =>
                {
                    boundV17Expedition.TryResolveV17Decision(
                        expedition.ExpeditionId,
                        choice.ChoiceId,
                        out v17Status);
                    RenderV17();
                },
                index == 0
                    ? new Color(0.18f, 0.3f, 0.34f, 1f)
                    : new Color(0.38f, 0.2f, 0.18f, 1f));
        }

        AddRightButton("사건 선택 대기", () => { });
        AddRightButton("닫기", Hide);
        OffenseReturnSafetySnapshot safety = v17Safety.Get(expedition.ExpeditionId);
        detailText.text =
            $"단계: {GetDecisionStageLabel(decision.stage)}\n"
            + $"안전 이동: {safety.SafeStepBudget}칸\n\n"
            + string.Join(
                "\n\n",
                choices.Select(choice =>
                    $"{choice.Label}\n{choice.Description}\n"
                    + $"{choice.DirectionLabel}"))
            + BuildStatusText();
    }

    private void RenderV17Battle(OffenseExpeditionRun expedition)
    {
        OffenseBattleDirectorStateData state = v17BattleDirector.State;
        CreateMapText(
            "BattleTitle",
            $"명령열 전투 · 턴 {state.turn}",
            29f,
            TextAlignmentOptions.Center,
            new Vector2(0.08f, 0.9f),
            new Vector2(0.92f, 0.99f));

        for (int intentIndex = 0;
             intentIndex < state.enemyIntents.Count;
             intentIndex++)
        {
            OffenseEnemyIntentStateData intent = state.enemyIntents[intentIndex];
            float width = 0.8f / Mathf.Max(1, state.enemyIntents.Count);
            float left = 0.1f + width * intentIndex;
            CreateMapButton(
                $"{GetCombatantName(intent.enemyId)}\n"
                + $"{GetTagLabel(intent.tacticalTag)} {intent.executionStages}단계",
                new Vector2(left, 0.7f),
                new Vector2(left + width - 0.015f, 0.86f),
                () => CommitPendingCard(intent),
                string.IsNullOrWhiteSpace(pendingCardInstanceId)
                    ? new Color(0.28f, 0.14f, 0.15f, 1f)
                    : new Color(0.52f, 0.2f, 0.16f, 1f));
        }

        for (int deckIndex = 0; deckIndex < state.decks.Count; deckIndex++)
        {
            OffenseCommandDeckStateData deck = state.decks[deckIndex];
            float rowTop = 0.62f - deckIndex * 0.115f;
            CreateMapText(
                $"DeckName_{deckIndex}",
                GetCombatantName(deck.characterId),
                16f,
                TextAlignmentOptions.MidlineLeft,
                new Vector2(0.04f, rowTop - 0.085f),
                new Vector2(0.19f, rowTop));

            bool committed = state.commandQueue.Any(entry =>
                entry.characterId == deck.characterId);
            for (int cardIndex = 0;
                 cardIndex < deck.candidates.Count;
                 cardIndex++)
            {
                OffenseCommandCardStateData card = deck.candidates[cardIndex];
                float left = 0.2f + cardIndex * 0.31f;
                bool selected = pendingCardInstanceId == card.instanceId;
                CreateMapButton(
                    $"{card.displayName}\n"
                    + $"{GetTagLabel(card.tacticalTag)} · {card.executionStages}단계"
                    + $" · 속도 {card.speed}",
                    new Vector2(left, rowTop - 0.09f),
                    new Vector2(left + 0.29f, rowTop),
                    () =>
                    {
                        if (!committed)
                        {
                            pendingCardCharacterId = deck.characterId;
                            pendingCardInstanceId = card.instanceId;
                            v17Status = "맞대응할 적 의도를 선택하세요.";
                        }

                        RenderV17();
                    },
                    committed
                        ? new Color(0.12f, 0.15f, 0.16f, 0.45f)
                        : selected
                            ? GetTagColor(card.tacticalTag)
                            : string.IsNullOrWhiteSpace(
                                pendingCardInstanceId)
                                ? new Color(0.2f, 0.23f, 0.26f, 1f)
                                : new Color(0.11f, 0.13f, 0.15f, 0.32f));
            }

            if (committed)
            {
                CreateMapButton(
                    "명령 취소",
                    new Vector2(0.83f, rowTop - 0.09f),
                    new Vector2(0.96f, rowTop),
                    () =>
                    {
                        v17BattleDirector.TryRemoveCommittedCommand(
                            deck.characterId);
                        RenderV17();
                    },
                    new Color(0.32f, 0.2f, 0.18f, 1f));
            }
        }

        AddRightButton(
            $"명령 실행 {state.commandQueue.Count}/{state.decks.Count}",
            () =>
            {
                if (state.commandQueue.Count == 0)
                {
                    v17Status = "실행할 명령이 없습니다.";
                }
                else
                {
                    List<CombatCardPresentationRecipe> presentations =
                        BuildCardPresentationRecipes(state);
                    IReadOnlyList<OffenseResolvedCommand> resolved =
                        v17BattleDirector.ResolveTurn();
                    ApplyCardPresentationResults(presentations, resolved);
                    v17CardPresentation.Present(presentations);
                    v17Status = BuildResolutionSummary(resolved);
                    pendingCardCharacterId = string.Empty;
                    pendingCardInstanceId = string.Empty;
                    if (v17BattleDirector.State != null
                        && v17BattleRuntime.HasActiveBattle)
                    {
                        v17BattleDirector.TryReplaceEnemyIntents(
                            OffenseV17BattleSetupFactory.CreateEnemyIntents(
                                v17BattleRuntime.Session,
                                state.turn + 1),
                            out _);
                        v17BattleDirector.TryDrawTurn(out _);
                    }
                }

                RenderV17();
            },
            new Color(0.48f, 0.2f, 0.14f, 1f));
        AddRightButton("카드 선택 해제", ClearPendingCard);
        AddRightButton("닫기", Hide);

        detailText.text = BuildBattleSidebar(state) + BuildStatusText();
    }

    private List<CombatCardPresentationRecipe> BuildCardPresentationRecipes(
        OffenseBattleDirectorStateData state)
    {
        List<CombatCardPresentationRecipe> recipes =
            new List<CombatCardPresentationRecipe>();
        if (state?.commandQueue == null)
        {
            return recipes;
        }

        HashSet<string> interceptedIntentIds =
            new HashSet<string>(StringComparer.Ordinal);
        foreach (OffenseCommandQueueEntryData entry in state.commandQueue
                     .Where(item => item != null)
                     .OrderBy(item => item.order))
        {
            OffenseCommandDeckStateData deck = state.decks?.FirstOrDefault(
                item => item != null
                    && item.characterId == entry.characterId);
            OffenseCommandCardStateData card = deck?.candidates?.FirstOrDefault(
                item => item != null
                    && item.instanceId == entry.cardInstanceId);
            OffenseEnemyIntentStateData intent =
                state.enemyIntents?.FirstOrDefault(
                    item => item != null
                        && item.intentId == entry.targetIntentId);
            if (card == null)
            {
                continue;
            }

            bool firstInterception = intent != null
                && interceptedIntentIds.Add(intent.intentId);
            recipes.Add(new CombatCardPresentationRecipe
            {
                allyName = GetCombatantName(entry.characterId),
                enemyName = GetCombatantName(
                    intent?.enemyId ?? entry.targetCombatantId),
                commandName = card.displayName,
                tacticalTag = card.tacticalTag,
                damageType = card.damageType,
                allyStages = card.executionStages,
                enemyStages = firstInterception
                    ? intent.executionStages
                    : 0,
                allyStagesRemaining = card.executionStages,
                enemyStagesRemaining = firstInterception
                    ? intent.executionStages
                    : 0,
                ultimate = false
            });
        }

        foreach (OffenseEnemyIntentStateData intent in state.enemyIntents
                     .Where(item => item != null
                         && !interceptedIntentIds.Contains(item.intentId)))
        {
            recipes.Add(new CombatCardPresentationRecipe
            {
                allyName = GetCombatantName(intent.targetCharacterId),
                enemyName = GetCombatantName(intent.enemyId),
                commandName = "대응 없음",
                tacticalTag = intent.tacticalTag,
                damageType = CombatDamageType.Blunt,
                allyStages = 0,
                enemyStages = intent.executionStages,
                allyStagesRemaining = 0,
                enemyStagesRemaining = intent.executionStages,
                ultimate = false
            });
        }

        return recipes;
    }

    private static void ApplyCardPresentationResults(
        IReadOnlyList<CombatCardPresentationRecipe> recipes,
        IReadOnlyList<OffenseResolvedCommand> resolved)
    {
        if (recipes == null || resolved == null)
        {
            return;
        }

        int count = Mathf.Min(recipes.Count, resolved.Count);
        for (int index = 0; index < count; index++)
        {
            CombatCardPresentationRecipe recipe = recipes[index];
            OffenseResolvedCommand command = resolved[index];
            recipe.allyStagesRemaining = command.clash.AllyStagesRemaining;
            recipe.enemyStagesRemaining = command.clash.EnemyStagesRemaining;
        }
    }

    private void CommitPendingCard(OffenseEnemyIntentStateData intent)
    {
        if (string.IsNullOrWhiteSpace(pendingCardCharacterId)
            || string.IsNullOrWhiteSpace(pendingCardInstanceId))
        {
            v17Status = "먼저 아군 명령 카드를 선택하세요.";
            RenderV17();
            return;
        }

        if (v17BattleDirector.TryCommitCommand(
                pendingCardCharacterId,
                pendingCardInstanceId,
                intent.intentId,
                intent.enemyId,
                out string reason))
        {
            v17Status = "명령열에 추가했습니다.";
            pendingCardCharacterId = string.Empty;
            pendingCardInstanceId = string.Empty;
        }
        else
        {
            v17Status = reason;
        }

        RenderV17();
    }

    private void CreateHexCell(
        OffenseHexTileState tile,
        OffenseWorldSiteStateData site,
        OffenseUrgentSiteStateData urgent,
        bool isDungeon,
        bool isParty)
    {
        GameObject cell = new GameObject(
            $"Hex_{tile.q}_{tile.r}",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(OffenseHexTileGraphic),
            typeof(Button));
        cell.transform.SetParent(v17MapRoot, false);
        RectTransform rect = cell.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(HexWidth, HexHeight);
        rect.anchoredPosition = HexToMapPosition(tile.Coord);

        OffenseHexTileGraphic graphic = cell.GetComponent<OffenseHexTileGraphic>();
        graphic.color = ResolveTerrainColor(tile);
        Button button = cell.GetComponent<Button>();
        button.targetGraphic = graphic;
        OffenseHexCoord capturedCoord = tile.Coord;
        string capturedSiteId = urgent?.siteId ?? site?.siteId ?? string.Empty;
        button.interactable = !tile.blocked;
        button.onClick.AddListener(() =>
        {
            selectedV17Coord = capturedCoord;
            if (!string.Equals(
                    selectedV17SiteId,
                    capturedSiteId,
                    StringComparison.Ordinal))
            {
                pendingIntelSiteId = string.Empty;
            }
            selectedV17SiteId = capturedSiteId;
            v17Status = string.Empty;
            RenderV17();
        });
        spawnedV17Objects.Add(cell);

        string label = isParty
            ? "원정대"
            : urgent != null
                ? $"! {urgent.displayName}"
                : site != null
                    ? site.displayName
                    : isDungeon
                        ? "던전"
                        : string.Empty;
        if (string.IsNullOrWhiteSpace(label))
        {
            return;
        }

        TMP_Text text = CreateChildLabel(cell.transform, label, 10f);
        text.color = isParty
            ? new Color(0.35f, 0.95f, 0.72f, 1f)
            : urgent != null
                ? new Color(1f, 0.42f, 0.3f, 1f)
                : isDungeon
                    ? new Color(0.96f, 0.82f, 0.42f, 1f)
                    : Color.white;
        text.raycastTarget = false;
        text.transform.SetAsLastSibling();
    }

    private TMP_Text CreateChildLabel(
        Transform parent,
        string text,
        float fontSize)
    {
        GameObject labelObject = OffensePanelUiFactory.CreateText(
            parent,
            "HexLabel",
            fontSize,
            TextAlignmentOptions.Center,
            v17Font);
        RectTransform rect = labelObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(-0.45f, -0.55f);
        rect.anchorMax = new Vector2(1.45f, 1.55f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        TMP_Text label = labelObject.GetComponent<TMP_Text>();
        label.text = text;
        label.enableAutoSizing = true;
        label.fontSizeMin = 7f;
        label.fontSizeMax = fontSize;
        return label;
    }

    private TMP_Text CreateMapText(
        string name,
        string text,
        float fontSize,
        TextAlignmentOptions alignment,
        Vector2 anchorMin,
        Vector2 anchorMax)
    {
        GameObject textObject = OffensePanelUiFactory.CreateText(
            v17MapRoot,
            name,
            fontSize,
            alignment,
            v17Font);
        RectTransform rect = textObject.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        TMP_Text label = textObject.GetComponent<TMP_Text>();
        label.text = text;
        spawnedV17Objects.Add(textObject);
        return label;
    }

    private GameObject CreateMapButton(
        string label,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Action callback,
        Color color)
    {
        GameObject buttonObject = RequireButtonFactory().CreateButton(
            v17MapRoot,
            label,
            15f,
            callback);
        RectTransform rect = buttonObject.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = new Vector2(3f, 3f);
        rect.offsetMax = new Vector2(-3f, -3f);
        buttonObject.GetComponent<Image>().color = color;
        LayoutElement layout = buttonObject.GetComponent<LayoutElement>();
        if (layout != null)
        {
            layout.ignoreLayout = true;
        }

        spawnedV17Objects.Add(buttonObject);
        return buttonObject;
    }

    private void AddRightButton(
        string label,
        Action callback,
        Color? color = null)
    {
        GameObject button = RequireButtonFactory().CreateButton(
            targetButtonRoot,
            label,
            15f,
            callback);
        button.GetComponent<LayoutElement>().preferredHeight = 40f;
        if (color.HasValue)
        {
            button.GetComponent<Image>().color = color.Value;
        }

        spawnedButtons.Add(button);
    }

    private bool IsSelectedActiveSite(out string siteId)
    {
        siteId = selectedV17SiteId;
        if (string.IsNullOrWhiteSpace(siteId))
        {
            return false;
        }

        return v17World.TryGetSite(siteId, out OffenseWorldSiteStateData site)
                && site.IsActive
            || v17World.TryGetUrgentSite(
                    siteId,
                    out OffenseUrgentSiteStateData urgent)
                && urgent.IsActive;
    }

    private string BuildV17Header(OffenseExpeditionRun expedition)
    {
        string expeditionState = expedition == null
            ? "원정대 대기"
            : GetExpeditionPhaseLabel(expedition.Phase);
        int urgentCount = v17World.UrgentSites.Count(site =>
            site != null && site.IsActive);
        return $"오펜스 월드 · Day {v17World.WorldDay} "
            + $"{v17World.WorldHour:0}:00 · {expeditionState}"
            + (expedition != null
                ? $" · 현장 자금 {expedition.FieldFunds}"
                : string.Empty)
            + $" · 긴급 거점 {urgentCount}";
    }

    private string BuildSelectedLocationDetail()
    {
        if (v17World.TryGetUrgentSite(
            selectedV17SiteId,
            out OffenseUrgentSiteStateData urgent))
        {
            string mitigationOrder = BuildMitigationOrderDetail(
                urgent.siteId);
            return $"{urgent.displayName}\n"
                + $"긴급 단계: {GetUrgentStageLabel(urgent.stage)}\n"
                + $"던전 교란: {GetModifierLabel(urgent.modifierKind)} "
                + $"{urgent.Intensity * 100f:0}%\n"
                + $"완화: {urgent.mitigation * 100f:0}%"
                + mitigationOrder;
        }

        if (v17World.TryGetSite(
            selectedV17SiteId,
            out OffenseWorldSiteStateData site))
        {
            int distance = v17World.GetMinimumStepDistance(
                v17World.DungeonCoord,
                site.Coord);
            bool hasIntel =
                v17ExternalInfluence?.IsIntelUnlocked(site.siteId) == true;
            string expiration = site.fixedBoss
                ? "만료 없음"
                : $"Day {site.expiresDay} 만료"
                    + $" · {Mathf.Max(0, site.expiresDay - v17World.WorldDay)}일 남음";
            if (!site.IsActive)
            {
                string expiredDisplayName = site.displayName;
                pendingIntelSiteId = string.Empty;
                selectedV17SiteId = string.Empty;
                return $"{expiredDisplayName}\n거점 만료 · {expiration}\n"
                    + "선택 중 거점이 만료되어 상세와 결제를 닫았습니다.";
            }

            return $"{site.displayName}\n"
                + $"{expiration}\n"
                + $"지역: {site.regionId}\n"
                + $"전력: {(hasIntel ? site.strength.ToString() : "미확인")}\n"
                + $"압력 축: {(hasIntel ? site.pressureAxis.ToString() : "미확인")}\n"
                + $"거리: {distance}칸";
        }

        return $"선택 좌표: ({selectedV17Coord.Q}, {selectedV17Coord.R})";
    }

    private void AddIntelPurchaseButtonsIfSelected()
    {
        if (v17ExternalInfluence == null
            || string.IsNullOrWhiteSpace(selectedV17SiteId)
            || v17ExternalInfluence.IsIntelUnlocked(selectedV17SiteId)
            || !v17World.TryGetSite(
                selectedV17SiteId,
                out OffenseWorldSiteStateData site)
            || site == null
            || !site.IsActive)
        {
            return;
        }

        AddRightButton("정보 확보 · 명성 10", () =>
            TryPurchaseSelectedIntel(ExpeditionIntelPaymentMethod.Renown));
        AddRightButton("정보 확보 · 골드 200", () =>
            TryPurchaseSelectedIntel(ExpeditionIntelPaymentMethod.Gold));
        AddRightButton("정보 확보 · 정찰 노동 60", () =>
            TryPurchaseSelectedIntel(
                ExpeditionIntelPaymentMethod.ScoutingLabor));
        AddRightButton("정보 확보 · 길잡이 부적 1", () =>
            TryPurchaseSelectedIntel(
                ExpeditionIntelPaymentMethod.TrailCharm));
    }

    private void TryPurchaseSelectedIntel(
        ExpeditionIntelPaymentMethod payment)
    {
        if (!v17World.TryGetSite(
                selectedV17SiteId,
                out OffenseWorldSiteStateData site)
            || site == null
            || !site.IsActive
            || (!site.fixedBoss
                && (site.expiresDay <= 0
                    || v17World.WorldDay >= site.expiresDay)))
        {
            pendingIntelSiteId = string.Empty;
            v17Status =
                "확인 중 거점이 만료되어 결제를 취소했습니다. 재화는 차감되지 않았습니다.";
            selectedV17SiteId = string.Empty;
            RenderV17();
            return;
        }

        bool confirmed = string.Equals(
                pendingIntelSiteId,
                site.siteId,
                StringComparison.Ordinal)
            && pendingIntelPayment == payment
            && pendingIntelExpiresDay == site.expiresDay;
        if (!confirmed)
        {
            pendingIntelSiteId = site.siteId;
            pendingIntelPayment = payment;
            pendingIntelExpiresDay = site.expiresDay;
            string expiry = site.fixedBoss
                ? "만료 없음"
                : $"Day {site.expiresDay} · "
                    + $"{Mathf.Max(0, site.expiresDay - v17World.WorldDay)}일 남음";
            v17Status =
                $"정보 구매 확인 · {FormatIntelPayment(payment)}"
                + $" · 만료 {expiry}"
                + (site.fixedBoss
                    ? string.Empty
                    : " · 거점 만료 시 환불 없음")
                + " · 같은 결제 버튼을 다시 눌러 확정";
            RenderV17();
            return;
        }

        pendingIntelSiteId = string.Empty;
        if (v17ExternalInfluence.TryUnlockIntelForActiveSite(
            site.siteId,
            site.fixedBoss,
            site.expiresDay,
            v17World.WorldDay,
            payment,
            out string failureReason))
        {
            v17Status = "원정지의 적 구성·방어구·약점 정보를 확보했습니다.";
        }
        else
        {
            v17Status = failureReason;
        }

        RenderV17();
    }

    private static string FormatIntelPayment(
        ExpeditionIntelPaymentMethod payment)
    {
        return payment switch
        {
            ExpeditionIntelPaymentMethod.Renown => "명성 10",
            ExpeditionIntelPaymentMethod.Gold => "골드 200",
            ExpeditionIntelPaymentMethod.ScoutingLabor => "정찰 노동 60",
            ExpeditionIntelPaymentMethod.TrailCharm => "길잡이 부적 1",
            _ => payment.ToString()
        };
    }

    private string BuildThreatDetail()
    {
        IReadOnlyList<OffenseThreatModifierSnapshot> modifiers =
            (v17World as IWorldThreatModifierQuery)?.GetActiveModifiers();
        if (modifiers == null || modifiers.Count == 0)
        {
            return "\n\n활성 던전 교란 없음";
        }

        return "\n\n활성 던전 교란\n" + string.Join(
            "\n",
            modifiers.Select(modifier =>
                $"{GetModifierLabel(modifier.Kind)} "
                + $"{modifier.EffectiveStrength * 100f:0}%"));
    }

    private string BuildBattleSidebar(OffenseBattleDirectorStateData state)
    {
        string queue = state.commandQueue.Count == 0
            ? "아직 확정된 명령이 없습니다."
            : string.Join(
                "\n",
                state.commandQueue
                    .OrderBy(entry => entry.order)
                    .Select(entry =>
                    {
                        OffenseCommandDeckStateData deck = state.decks
                            .FirstOrDefault(candidate =>
                                candidate.characterId == entry.characterId);
                        OffenseCommandCardStateData card = deck?.candidates
                            .FirstOrDefault(candidate =>
                                candidate.instanceId == entry.cardInstanceId);
                        return $"{entry.order}. {GetCombatantName(entry.characterId)}"
                            + $" · {card?.displayName ?? "명령"}";
                    }));
        return $"명령열\n{queue}\n\n"
            + "카드를 고른 뒤 적 의도를 선택하면 맞대응이 연결됩니다.\n"
            + "실행 중에는 실제 남은 단계만 전투 판정으로 넘어갑니다.";
    }

    private string BuildResolutionSummary(
        IReadOnlyList<OffenseResolvedCommand> resolved)
    {
        if (resolved == null || resolved.Count == 0)
        {
            return "해결된 명령이 없습니다.";
        }

        return string.Join(
            " · ",
            resolved.Select(command =>
                $"{command.order}:{GetChainStateLabel(command.chain.State)}"
                + $" {command.clash.AllyStagesRemaining}단계"));
    }

    private string GetCombatantName(string persistentId)
    {
        OffenseBattleCombatant combatant = v17BattleRuntime.Session?
            .FindCombatant(persistentId);
        return combatant?.DisplayName ?? persistentId;
    }

    private static string GetActorLabel(CharacterActor actor)
    {
        if (actor == null)
        {
            return "알 수 없음";
        }

        actor.EnsureRuntimeState();
        string name = actor.Identity?.DisplayName ?? actor.name;
        int level = actor.Progression?.Level ?? 1;
        return $"Lv.{level} {name} · 체력 {actor.CurrentHealth:0}";
    }

    private string BuildStatusText()
    {
        return string.IsNullOrWhiteSpace(v17Status)
            ? string.Empty
            : $"\n\n{v17Status}";
    }

    private static string BuildPackingStatus(
        OffenseSupplyPackingSnapshot packing)
    {
        if (!packing.Exists)
        {
            return string.Empty;
        }

        if (packing.Consumed)
        {
            return "보급: 적재 완료\n";
        }

        return packing.IsReady
            ? $"보급: 집결 완료 {packing.Required}/{packing.Required}\n"
            : $"보급 운반 중: {packing.Delivered}/{packing.Required}\n";
    }

    private void ClearPendingCard()
    {
        pendingCardCharacterId = string.Empty;
        pendingCardInstanceId = string.Empty;
        v17Status = string.Empty;
        RenderV17();
    }

    private void ResetV17MapView()
    {
        OffenseV17MapInput input =
            v17MapRoot?.GetComponentInParent<OffenseV17MapInput>();
        input?.ResetView();
    }

    private void PrepareV17Surface(V17SurfaceKind surface)
    {
        if (activeV17Surface == surface)
        {
            return;
        }

        activeV17Surface = surface;
        ResetV17MapView();
    }

    private void BindV17ExpeditionRuntime()
    {
        if (boundV17Expedition != null
            || v17ExpeditionProvider == null
            || !v17ExpeditionProvider.TryGetRuntime(
                out OffenseExpeditionRuntime expedition))
        {
            return;
        }

        boundV17Expedition = expedition;
        boundV17Expedition.StateChanged += RenderV17IfVisible;
    }

    private void RenderV17IfVisible()
    {
        if (this != null && isActiveAndEnabled && CanRenderV17())
        {
            RenderV17();
        }
    }

    private void ClearV17Objects()
    {
        foreach (GameObject item in spawnedV17Objects)
        {
            if (item == null)
            {
                continue;
            }

            item.SetActive(false);
            item.transform.SetParent(null, false);
            if (Application.isPlaying)
            {
                Destroy(item);
            }
            else
            {
                DestroyImmediate(item);
            }
        }

        spawnedV17Objects.Clear();
    }

    private void OnDestroy()
    {
        if (v17World != null)
        {
            v17World.Changed -= RenderV17IfVisible;
        }

        if (v17BattleDirector != null)
        {
            v17BattleDirector.Changed -= RenderV17IfVisible;
        }

        if (v17Mitigation != null)
        {
            v17Mitigation.Changed -= RenderV17IfVisible;
        }

        if (boundV17Expedition != null)
        {
            boundV17Expedition.StateChanged -= RenderV17IfVisible;
        }
    }

    private void AddUrgentMitigationButtonIfSelected()
    {
        if (v17Mitigation == null
            || !v17World.TryGetUrgentSite(
                selectedV17SiteId,
                out OffenseUrgentSiteStateData urgent)
            || urgent == null
            || !urgent.IsActive)
        {
            return;
        }

        if (v17Mitigation.TryGetOrder(
                urgent.siteId,
                out OffenseUrgentMitigationOrderStateData order))
        {
            AddRightButton(
                $"완화 취소 · {GetMitigationProgress(order):0}%",
                () =>
                {
                    v17Mitigation.TryCancel(
                        urgent.siteId,
                        out v17Status);
                    RenderV17();
                },
                new Color(0.38f, 0.23f, 0.16f, 1f));
            return;
        }

        AddRightButton(
            "던전에서 완화 작업",
            () =>
            {
                v17Mitigation.TryStart(
                    urgent.siteId,
                    out v17Status);
                RenderV17();
            },
            new Color(0.2f, 0.38f, 0.34f, 1f));
    }

    private string BuildMitigationOrderDetail(string siteId)
    {
        if (v17Mitigation == null
            || !v17Mitigation.TryGetOrder(
                siteId,
                out OffenseUrgentMitigationOrderStateData order))
        {
            return string.Empty;
        }

        string facility = string.IsNullOrWhiteSpace(
                order.facilityPersistentId)
            ? "시설 재배정 대기"
            : $"시설 ({order.facilityX}, {order.facilityY})";
        return $"\n완화 작업: {GetMitigationProgress(order):0}%"
            + $"\n{facility} · {order.statusText}";
    }

    private float GetMitigationProgress(
        OffenseUrgentMitigationOrderStateData order)
    {
        if (order == null)
        {
            return 0f;
        }

        return order.requiredWork > 0f
            ? Mathf.Clamp01(
                order.completedWork / order.requiredWork) * 100f
            : 0f;
    }

    private static Vector2 HexToMapPosition(OffenseHexCoord coord)
    {
        return new Vector2(
            coord.Q * HexHorizontalStep,
            (coord.R + coord.Q * 0.5f) * HexVerticalStep);
    }

    private static Color ResolveTerrainColor(OffenseHexTileState tile)
    {
        if (tile.blocked)
        {
            return new Color(0.08f, 0.085f, 0.09f, 1f);
        }

        Color color = tile.terrain switch
        {
            OffenseHexTerrain.Forest => new Color(0.13f, 0.25f, 0.19f, 1f),
            OffenseHexTerrain.Hills => new Color(0.31f, 0.28f, 0.22f, 1f),
            OffenseHexTerrain.Marsh => new Color(0.18f, 0.27f, 0.25f, 1f),
            OffenseHexTerrain.Mountain => new Color(0.24f, 0.25f, 0.28f, 1f),
            OffenseHexTerrain.River => new Color(0.12f, 0.28f, 0.4f, 1f),
            _ => new Color(0.24f, 0.3f, 0.2f, 1f)
        };
        if (tile.hasRoad)
        {
            color = Color.Lerp(color, new Color(0.52f, 0.42f, 0.28f, 1f), 0.35f);
        }

        if (tile.hasRiver)
        {
            color = Color.Lerp(color, new Color(0.12f, 0.34f, 0.52f, 1f), 0.5f);
        }

        return color;
    }

    private static Color GetTagColor(OffenseTacticalTag tag)
    {
        return tag switch
        {
            OffenseTacticalTag.Intercept => new Color(0.26f, 0.39f, 0.45f, 1f),
            OffenseTacticalTag.Maneuver => new Color(0.24f, 0.42f, 0.31f, 1f),
            OffenseTacticalTag.Break => new Color(0.52f, 0.28f, 0.18f, 1f),
            OffenseTacticalTag.Support => new Color(0.34f, 0.3f, 0.5f, 1f),
            OffenseTacticalTag.Execute => new Color(0.52f, 0.16f, 0.2f, 1f),
            _ => new Color(0.25f, 0.26f, 0.28f, 1f)
        };
    }

    private static string GetTagLabel(OffenseTacticalTag tag)
    {
        return tag switch
        {
            OffenseTacticalTag.Intercept => "저지",
            OffenseTacticalTag.Maneuver => "기동",
            OffenseTacticalTag.Break => "파쇄",
            OffenseTacticalTag.Support => "지원",
            OffenseTacticalTag.Execute => "집행",
            _ => "일반"
        };
    }

    private static string GetChainStateLabel(OffenseChainState state)
    {
        return state switch
        {
            OffenseChainState.Full => "완전",
            OffenseChainState.Degraded => "약화",
            OffenseChainState.Residual => "잔여",
            _ => "단절"
        };
    }

    private static string GetUrgentStageLabel(OffenseUrgentSiteStage stage)
    {
        return stage switch
        {
            OffenseUrgentSiteStage.Signal => "징후",
            OffenseUrgentSiteStage.Warning => "경고",
            OffenseUrgentSiteStage.Crisis => "위기",
            OffenseUrgentSiteStage.Withdrawing => "철수 준비",
            OffenseUrgentSiteStage.Destroyed => "파괴됨",
            _ => "종료"
        };
    }

    private static string GetDecisionStageLabel(OffenseDecisionStage stage)
    {
        return stage switch
        {
            OffenseDecisionStage.Travel => "이동",
            OffenseDecisionStage.Reconnaissance => "정찰",
            OffenseDecisionStage.Negotiation => "협상",
            OffenseDecisionStage.Infiltration => "잠입",
            OffenseDecisionStage.Camp => "야영",
            OffenseDecisionStage.Loot => "전리품",
            OffenseDecisionStage.Return => "귀환",
            _ => "사건"
        };
    }

    private static string GetExpeditionPhaseLabel(OffenseExpeditionPhase phase)
    {
        return phase switch
        {
            OffenseExpeditionPhase.Traveling => "이동 중",
            OffenseExpeditionPhase.AwaitingDecision => "사건 선택",
            OffenseExpeditionPhase.InBattle => "교전 중",
            OffenseExpeditionPhase.Returning => "귀환 중",
            OffenseExpeditionPhase.Completed => "완료",
            OffenseExpeditionPhase.Defeated => "전멸",
            OffenseExpeditionPhase.Retreated => "후퇴",
            _ => "준비 중"
        };
    }

    private static string GetModifierLabel(OffenseThreatModifierKind kind)
    {
        return kind switch
        {
            OffenseThreatModifierKind.Temperature => "온도",
            OffenseThreatModifierKind.FuelConsumption => "연료 부담",
            OffenseThreatModifierKind.AutomatedDefense => "자동 방어",
            OffenseThreatModifierKind.Mood => "기분",
            OffenseThreatModifierKind.Rest => "휴식",
            OffenseThreatModifierKind.Sanitation => "위생",
            OffenseThreatModifierKind.Disease => "질병",
            OffenseThreatModifierKind.Lighting => "조명",
            OffenseThreatModifierKind.Accuracy => "명중",
            OffenseThreatModifierKind.InvasionWarning => "침공 경고",
            OffenseThreatModifierKind.DefenseEvasion => "방어 회피",
            _ => "교란"
        };
    }
}
