using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Factions;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

public partial class OffenseWorldMapPanel
{
    public sealed class StrategicPresentationServices
    {
        public StrategicPresentationServices(
            ITmpKoreanFontService font,
            IDomainFailureLocalizer failures)
        {
            Font = font ?? throw new ArgumentNullException(nameof(font));
            Failures = failures
                ?? throw new ArgumentNullException(nameof(failures));
        }

        public ITmpKoreanFontService Font { get; }
        public IDomainFailureLocalizer Failures { get; }
    }

    private enum StrategicSurfaceKind
    {
        Map,
        Factions,
        Decision,
        Battle
    }

    private const float HexHorizontalStep = 30f;
    private const float HexVerticalStep = 34f;

    private readonly List<GameObject> spawnedStrategicObjects = new List<GameObject>();
    private readonly List<CharacterActor> selectedStrategicMembers =
        new List<CharacterActor>();
    private readonly Dictionary<OffenseSupplyType, int> selectedStrategicSupplies =
        Enum.GetValues(typeof(OffenseSupplyType))
            .Cast<OffenseSupplyType>()
            .ToDictionary(type => type, _ => 0);
    private IOffenseWorldSimulation strategicWorld;
    private IOffenseTravelRuntime strategicTravel;
    private IOffenseDecisionRuntime strategicDecisions;
    private IOffenseReturnSafetyRuntime strategicSafety;
    private IOffenseBattleDirector strategicBattleDirector;
    private IOffenseBattleRuntime strategicBattleRuntime;
    private ICombatCardPresentationService strategicCardPresentation;
    private IOffenseUrgentMitigationRuntime strategicMitigation;
    private IOffensePreparationService strategicPreparation;
    private IOffenseFieldMedicalRuntime strategicFieldMedical;
    private IAnatomyHealthRuntime strategicAnatomy;
    private IAnatomyProfileCatalog strategicAnatomyProfiles;
    private OffenseExpeditionRuntime strategicExpedition;
    private ITmpKoreanFontService strategicFont;
    private IDomainFailureLocalizer strategicFailureLocalizer;
    private IExternalInfluenceRuntime strategicExternalInfluence;
    private IFactionRuntime strategicFactions;
    private IInvasionCampaignRuntime strategicCampaign;
    private RectTransform strategicMapRoot;
    private OffenseWorldMapResponsiveLayout strategicResponsiveLayout;
    private OffenseWorldMapStrategicViewFactory strategicViewFactory;
    private OffenseStrategicPreparationPresenter strategicPreparationPresenter;
    private string selectedWorldSiteId = string.Empty;
    private OffenseHexCoord selectedStrategicCoord;
    private string pendingCardCharacterId = string.Empty;
    private string pendingCardInstanceId = string.Empty;
    private string pendingIntelSiteId = string.Empty;
    private ExpeditionIntelPaymentMethod pendingIntelPayment;
    private int pendingIntelExpiresDay;
    private string strategicStatus = string.Empty;
    private int selectedStrategicFieldFunds;
    private bool showStrategicFactionSurface;
    private string selectedStrategicFactionId = string.Empty;
    private string selectedStrategicHumanBranchId = string.Empty;
    private string pendingStrategicBetrayalFactionId = string.Empty;
    private StrategicSurfaceKind activeStrategicSurface = StrategicSurfaceKind.Map;

    [Inject]
    public void ConstructStrategic(
        IOffenseWorldSimulation world,
        IOffenseTravelRuntime travel,
        IOffenseDecisionRuntime decisions,
        IOffenseReturnSafetyRuntime safety,
        IOffenseBattleDirector battleDirector,
        IOffenseBattleRuntime battleRuntime,
        ICombatCardPresentationService cardPresentation,
        IOffenseUrgentMitigationRuntime mitigation,
        IOffensePreparationService preparation,
        IOffenseFieldMedicalRuntime fieldMedical,
        IAnatomyHealthRuntime anatomy,
        IAnatomyProfileCatalog anatomyProfiles,
        OffenseSceneRuntimeReferences offenseRuntimes,
        StrategicPresentationServices presentation,
        IExternalInfluenceRuntime externalInfluence,
        IFactionRuntime factions,
        IInvasionCampaignRuntime campaign)
    {
        strategicWorld = world ?? throw new ArgumentNullException(nameof(world));
        strategicTravel = travel ?? throw new ArgumentNullException(nameof(travel));
        strategicDecisions = decisions ?? throw new ArgumentNullException(nameof(decisions));
        strategicSafety = safety ?? throw new ArgumentNullException(nameof(safety));
        strategicBattleDirector = battleDirector
            ?? throw new ArgumentNullException(nameof(battleDirector));
        strategicBattleRuntime = battleRuntime
            ?? throw new ArgumentNullException(nameof(battleRuntime));
        strategicCardPresentation = cardPresentation
            ?? throw new ArgumentNullException(nameof(cardPresentation));
        strategicMitigation = mitigation
            ?? throw new ArgumentNullException(nameof(mitigation));
        strategicPreparation = preparation
            ?? throw new ArgumentNullException(nameof(preparation));
        strategicFieldMedical = fieldMedical
            ?? throw new ArgumentNullException(nameof(fieldMedical));
        strategicAnatomy = anatomy
            ?? throw new ArgumentNullException(nameof(anatomy));
        strategicAnatomyProfiles = anatomyProfiles
            ?? throw new ArgumentNullException(nameof(anatomyProfiles));
        strategicExpedition = (offenseRuntimes
                ?? throw new ArgumentNullException(nameof(offenseRuntimes)))
            .Expedition
            ?? throw new InvalidOperationException(
                $"{nameof(OffenseWorldMapPanel)} requires a loaded {nameof(OffenseExpeditionRuntime)}.");
        presentation = presentation
            ?? throw new ArgumentNullException(nameof(presentation));
        strategicFont = presentation.Font;
        strategicFailureLocalizer = presentation.Failures;
        strategicExternalInfluence = externalInfluence
            ?? throw new ArgumentNullException(nameof(externalInfluence));
        strategicFactions = factions
            ?? throw new ArgumentNullException(nameof(factions));
        strategicCampaign = campaign
            ?? throw new ArgumentNullException(nameof(campaign));
        selectedStrategicCoord = strategicWorld.DungeonCoord;
        strategicWorld.Changed += RenderStrategicIfVisible;
        strategicDecisions.Changed += RenderStrategicIfVisible;
        strategicBattleDirector.Changed += RenderStrategicIfVisible;
        strategicMitigation.Changed += RenderStrategicIfVisible;
    }

    internal void BindStrategicGeneratedView(
        RectTransform mapRoot,
        OffenseWorldMapResponsiveLayout responsiveLayout)
    {
        strategicMapRoot = mapRoot
            ?? throw new ArgumentNullException(nameof(mapRoot));
        strategicResponsiveLayout = responsiveLayout
            ?? throw new ArgumentNullException(nameof(responsiveLayout));
        strategicViewFactory = null;
        strategicPreparationPresenter = null;
    }

    private bool CanRenderStrategic()
    {
        return strategicWorld != null
            && strategicMapRoot != null
            && strategicWorld.Tiles != null
            && strategicWorld.Tiles.Count > 0;
    }

    private void RenderStrategic()
    {
        if (!CanRenderStrategic())
        {
            return;
        }

        ClearButtons();
        ClearStrategicObjects();

        OffenseExpeditionRun expedition = RequireStrategicExpedition()
            .ActiveExpeditions
            .Where(active => active != null && active.UsesWorldTravel)
            .OrderBy(active => strategicFieldMedical != null
                && strategicFieldMedical.IsStranded(active.ExpeditionId)
                    ? 1
                    : 0)
            .FirstOrDefault();
        headerText.text = BuildStrategicHeader(expedition);

        if (expedition != null
            && strategicBattleDirector.State != null
            && strategicBattleRuntime.HasActiveBattle)
        {
            PrepareStrategicSurface(StrategicSurfaceKind.Battle);
            RenderStrategicBattle(expedition);
            return;
        }

        if (expedition != null
            && strategicDecisions.TryGetActiveDecision(
                expedition.ExpeditionId,
                out OffenseDecisionView decision))
        {
            PrepareStrategicSurface(StrategicSurfaceKind.Decision);
            RenderStrategicDecision(expedition, decision);
            return;
        }

        if (showStrategicFactionSurface)
        {
            PrepareStrategicSurface(StrategicSurfaceKind.Factions);
            RenderStrategicHexMap(expedition);
            RenderStrategicFactionMarkers();
            RenderStrategicFactionSidebar();
            return;
        }

        PrepareStrategicSurface(StrategicSurfaceKind.Map);
        RenderStrategicHexMap(expedition);
        RenderStrategicFactionMarkers();
        RenderStrategicSidebar(expedition);
    }

    private void RenderStrategicHexMap(OffenseExpeditionRun expedition)
    {
        OffenseTravelStateData travelState = null;
        if (expedition != null)
        {
            strategicTravel.TryGetState(expedition.ExpeditionId, out travelState);
        }

        Dictionary<OffenseHexCoord, OffenseWorldSiteStateData> sites =
            strategicWorld.Sites
                .Where(site => site != null
                    && site.IsActive
                    && site.state != OffenseWorldSiteState.Hidden)
                .GroupBy(site => site.Coord)
                .ToDictionary(group => group.Key, group => group.First());
        Dictionary<OffenseHexCoord, OffenseUrgentSiteStateData> urgentSites =
            strategicWorld.UrgentSites
                .Where(site => site != null && site.IsActive)
                .GroupBy(site => site.Coord)
                .ToDictionary(group => group.Key, group => group.First());

        foreach (OffenseHexTileState tile in strategicWorld.Tiles
                     .Where(value => value != null)
                     .OrderBy(value => value.r)
                     .ThenBy(value => value.q))
        {
            OffenseHexCoord coord = tile.Coord;
            sites.TryGetValue(coord, out OffenseWorldSiteStateData site);
            urgentSites.TryGetValue(coord, out OffenseUrgentSiteStateData urgent);
            bool isDungeon = coord == strategicWorld.DungeonCoord;
            bool isParty = travelState != null && coord == travelState.CurrentCoord;
            CreateHexCell(tile, site, urgent, isDungeon, isParty);
        }
    }

    private void CreateHexCell(
        OffenseHexTileState tile,
        OffenseWorldSiteStateData site,
        OffenseUrgentSiteStateData urgent,
        bool isDungeon,
        bool isParty)
    {
        OffenseHexCoord coord = tile.Coord;
        string siteId = urgent?.siteId ?? site?.siteId ?? string.Empty;
        string label = isParty
            ? "원정대"
            : urgent != null
                ? $"! {urgent.displayName}"
                : site != null
                    ? site.displayName
                    : isDungeon
                        ? "던전"
                        : string.Empty;
        Color labelColor = isParty
            ? new Color(0.35f, 0.95f, 0.72f, 1f)
            : urgent != null
                ? new Color(1f, 0.42f, 0.3f, 1f)
                : isDungeon
                    ? new Color(0.96f, 0.82f, 0.42f, 1f)
                    : Color.white;
        RequireStrategicViewFactory().CreateHexCell(
            $"Hex_{tile.q}_{tile.r}",
            tile.blocked,
            label,
            HexToMapPosition(coord),
            ResolveTerrainColor(tile),
            labelColor,
            () => SelectStrategicHex(coord, siteId));
    }

    private void SelectStrategicHex(
        OffenseHexCoord coord,
        string siteId)
    {
        selectedStrategicCoord = coord;
        if (!string.Equals(
                selectedWorldSiteId,
                siteId,
                StringComparison.Ordinal))
        {
            pendingIntelSiteId = string.Empty;
        }

        selectedWorldSiteId = siteId;
        strategicStatus = string.Empty;
        RenderStrategic();
    }

    private TMP_Text CreateMapText(
        string name,
        string text,
        float fontSize,
        TextAlignmentOptions alignment,
        Vector2 anchorMin,
        Vector2 anchorMax) =>
        RequireStrategicViewFactory().CreateMapText(
            name,
            text,
            fontSize,
            alignment,
            anchorMin,
            anchorMax);

    private GameObject CreateMapButton(
        string label,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Action callback,
        Color color) =>
        RequireStrategicViewFactory().CreateMapButton(
            label,
            anchorMin,
            anchorMax,
            callback,
            color);

    private void AddRightButton(
        string label,
        Action callback,
        Color? color = null) =>
        RequireStrategicViewFactory().AddRightButton(label, callback, color);

    private void AddPreparationAction(
        string label,
        Action callback,
        Color? color = null) =>
        RequireStrategicPreparationPresenter().AddAction(
            label,
            callback,
            color);

    private void SetPreparationDetail(string text) =>
        RequireStrategicPreparationPresenter().SetDetail(text);

    private OffenseStrategicPreparationPresenter
        RequireStrategicPreparationPresenter()
    {
        return strategicPreparationPresenter ??=
            new OffenseStrategicPreparationPresenter(
                RequireStrategicViewFactory(),
                detailText);
    }

    private OffenseWorldMapStrategicViewFactory RequireStrategicViewFactory()
    {
        return strategicViewFactory ??=
            new OffenseWorldMapStrategicViewFactory(
                strategicMapRoot,
                targetButtonRoot,
                (parent, name, fontSize, alignment) =>
                    OffensePanelUiFactory.CreateText(
                        parent,
                        name,
                        fontSize,
                        alignment,
                        strategicFont),
                (parent, label, fontSize, callback) =>
                    RequireButtonFactory().CreateButton(
                        parent,
                        label,
                        fontSize,
                        callback),
                spawnedStrategicObjects,
                spawnedButtons);
    }

    private bool IsSelectedActiveSite(out string siteId)
    {
        siteId = selectedWorldSiteId;
        if (string.IsNullOrWhiteSpace(siteId))
        {
            return false;
        }

        return strategicWorld.TryGetSite(
                    siteId,
                    out OffenseWorldSiteStateData site)
                && site.IsActive
            || strategicWorld.TryGetUrgentSite(
                    siteId,
                    out OffenseUrgentSiteStateData urgent)
                && urgent.IsActive;
    }





    private void ClearPendingCard()
    {
        pendingCardCharacterId = string.Empty;
        pendingCardInstanceId = string.Empty;
        strategicStatus = string.Empty;
        RenderStrategic();
    }

    private void ResetStrategicMapView()
    {
        OffenseStrategicMapInput input =
            strategicMapRoot?.GetComponentInParent<OffenseStrategicMapInput>();
        input?.ResetView();
    }

    private void PrepareStrategicSurface(StrategicSurfaceKind surface)
    {
        if (activeStrategicSurface == surface)
        {
            return;
        }

        activeStrategicSurface = surface;
        ResetStrategicMapView();
    }

    private void RenderStrategicIfVisible()
    {
        if (this != null && isActiveAndEnabled && CanRenderStrategic())
        {
            RenderStrategic();
        }
    }

    private OffenseExpeditionRuntime RequireStrategicExpedition()
    {
        if (strategicExpedition != null)
        {
            return strategicExpedition;
        }

        throw new InvalidOperationException(
            "The strategic expedition runtime is unavailable.");
    }

    private void ClearStrategicObjects()
    {
        foreach (GameObject item in spawnedStrategicObjects)
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

        spawnedStrategicObjects.Clear();
    }

    private void OnDestroy()
    {
        if (strategicWorld != null)
        {
            strategicWorld.Changed -= RenderStrategicIfVisible;
        }

        if (strategicBattleDirector != null)
        {
            strategicBattleDirector.Changed -= RenderStrategicIfVisible;
        }

        if (strategicDecisions != null)
        {
            strategicDecisions.Changed -= RenderStrategicIfVisible;
        }

        if (strategicMitigation != null)
        {
            strategicMitigation.Changed -= RenderStrategicIfVisible;
        }

    }

    private void AddUrgentMitigationButtonIfSelected()
    {
        if (strategicMitigation == null
            || !strategicWorld.TryGetUrgentSite(
                selectedWorldSiteId,
                out OffenseUrgentSiteStateData urgent)
            || urgent == null
            || !urgent.IsActive)
        {
            return;
        }

        if (strategicMitigation.TryGetOrder(
                urgent.siteId,
                out OffenseUrgentMitigationOrderStateData order))
        {
            AddRightButton(
                $"완화 취소 · {GetMitigationProgress(order):0}%",
                () =>
                {
                    strategicMitigation.TryCancel(
                        urgent.siteId,
                        out strategicStatus);
                    RenderStrategic();
                },
                new Color(0.38f, 0.23f, 0.16f, 1f));
            return;
        }

        AddRightButton(
            "던전에서 완화 작업",
            () =>
            {
                strategicMitigation.TryStart(
                    urgent.siteId,
                    out strategicStatus);
                RenderStrategic();
            },
            new Color(0.2f, 0.38f, 0.34f, 1f));
    }

    private string BuildMitigationOrderDetail(string siteId)
    {
        if (strategicMitigation == null
            || !strategicMitigation.TryGetOrder(
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
