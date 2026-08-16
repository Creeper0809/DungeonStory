using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Factions;
using TMPro;
using UnityEngine.UI;
using UnityEngine;

public partial class OffenseWorldMapPanel
{
    private string BuildStrategicHeader(OffenseExpeditionRun expedition)
    {
        string expeditionState = expedition == null
            ? "원정대 대기"
            : GetExpeditionPhaseLabel(expedition.Phase);
        int urgentCount = strategicWorld.UrgentSites.Count(site =>
            site != null && site.IsActive);
        return $"오펜스 월드 · Day {strategicWorld.WorldDay} "
            + $"{strategicWorld.WorldHour:0}:00 · {expeditionState}"
            + (expedition != null
                ? $" · 현장 자금 {expedition.FieldFunds}"
                : string.Empty)
            + $" · 긴급 거점 {urgentCount}";
    }

    private string BuildSelectedLocationDetail()
    {
        if (strategicWorld.TryGetUrgentSite(
            selectedWorldSiteId,
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

        if (strategicWorld.TryGetSite(
            selectedWorldSiteId,
            out OffenseWorldSiteStateData site))
        {
            int distance = strategicWorld.GetMinimumStepDistance(
                strategicWorld.DungeonCoord,
                site.Coord);
            bool hasIntel =
                strategicExternalInfluence?.IsIntelUnlocked(site.siteId) == true;
            string expiration = site.fixedBoss
                ? "만료 없음"
                : $"Day {site.expiresDay} 만료"
                    + $" · {Mathf.Max(0, site.expiresDay - strategicWorld.WorldDay)}일 남음";
            if (!site.IsActive)
            {
                string expiredDisplayName = site.displayName;
                pendingIntelSiteId = string.Empty;
                selectedWorldSiteId = string.Empty;
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

        return $"선택 좌표: ({selectedStrategicCoord.Q}, {selectedStrategicCoord.R})";
    }

    private void AddIntelPurchaseButtonsIfSelected()
    {
        if (strategicExternalInfluence == null
            || string.IsNullOrWhiteSpace(selectedWorldSiteId)
            || strategicExternalInfluence.IsIntelUnlocked(selectedWorldSiteId)
            || !strategicWorld.TryGetSite(
                selectedWorldSiteId,
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
        if (!strategicWorld.TryGetSite(
                selectedWorldSiteId,
                out OffenseWorldSiteStateData site)
            || site == null
            || !site.IsActive
            || (!site.fixedBoss
                && (site.expiresDay <= 0
                    || strategicWorld.WorldDay >= site.expiresDay)))
        {
            pendingIntelSiteId = string.Empty;
            strategicStatus =
                "확인 중 거점이 만료되어 결제를 취소했습니다. 재화는 차감되지 않았습니다.";
            selectedWorldSiteId = string.Empty;
            RenderStrategic();
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
                    + $"{Mathf.Max(0, site.expiresDay - strategicWorld.WorldDay)}일 남음";
            strategicStatus =
                $"정보 구매 확인 · {FormatIntelPayment(payment)}"
                + $" · 만료 {expiry}"
                + (site.fixedBoss
                    ? string.Empty
                    : " · 거점 만료 시 환불 없음")
                + " · 같은 결제 버튼을 다시 눌러 확정";
            RenderStrategic();
            return;
        }

        pendingIntelSiteId = string.Empty;
        if (strategicExternalInfluence.TryUnlockIntelForActiveSite(
            site.siteId,
            site.fixedBoss,
            site.expiresDay,
            strategicWorld.WorldDay,
            payment,
            out DomainFailure failure))
        {
            strategicStatus = "원정지의 적 구성·방어구·약점 정보를 확보했습니다.";
        }
        else
        {
            strategicStatus = strategicFailureLocalizer.Localize(failure);
        }

        RenderStrategic();
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
            (strategicWorld as IWorldThreatModifierQuery)?.GetActiveModifiers();
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
                + $" {command.clash.AllyStagesRemaining}단계"
                + $" {command.execution.Outcome}"
                + (string.IsNullOrWhiteSpace(command.execution.FailureReason)
                    ? string.Empty
                    : $" ({command.execution.FailureReason})")));
    }

    private string GetCombatantName(string persistentId)
    {
        OffenseBattleCombatant combatant = strategicBattleRuntime.Session?
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
        return string.IsNullOrWhiteSpace(strategicStatus)
            ? string.Empty
            : $"\n\n{strategicStatus}";
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

    private void OpenStrategicFactionSurface()
    {
        showStrategicFactionSurface = true;
        pendingStrategicBetrayalFactionId = string.Empty;
        RenderStrategic();
    }

    private void RenderStrategicFactionMarkers()
    {
        if (strategicFactions == null || strategicCampaign == null)
        {
            return;
        }

        foreach (DungeonFactionState faction in strategicFactions.Factions)
        {
            FactionDefinitionSnapshot definition =
                strategicFactions.Definitions.FirstOrDefault(value =>
                    value != null
                    && value.StableId == faction.factionId);
            CreateStrategicStrategicMarker(
                faction.discovered
                    ? definition?.DisplayName ?? faction.factionId
                    : "미탐사 던전",
                new OffenseHexCoord(faction.HomeCoord.Q, faction.HomeCoord.R),
                new Color(0.36f, 0.75f, 0.52f, 1f));
        }

        foreach (HumanSupportSiteState site in strategicCampaign.SupportSites
                     .Where(value => value != null && value.alive))
        {
            CreateStrategicStrategicMarker(
                "인간 지원",
                site.Coord,
                site.connected
                    ? new Color(0.9f, 0.32f, 0.24f, 1f)
                    : new Color(0.48f, 0.4f, 0.38f, 1f));
        }

        foreach (FactionRouteState route in strategicFactions.Routes.Where(value =>
                     value != null
                     && value.status is FactionRouteStatus.Traveling
                         or FactionRouteStatus.Delayed))
        {
            CreateStrategicStrategicMarker(
                route.kind == FactionRouteKind.Reinforcement
                    ? "지원군"
                    : "상단",
                new OffenseHexCoord(route.CurrentCoord.Q, route.CurrentCoord.R),
                new Color(0.95f, 0.78f, 0.28f, 1f));
        }
    }

    private void CreateStrategicStrategicMarker(
        string label,
        OffenseHexCoord coord,
        Color color)
    {
        GameObject marker = OffensePanelUiFactory.CreateText(
            strategicMapRoot,
            "FactionMarker",
            10f,
            TextAlignmentOptions.Center,
            strategicFont);
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
        spawnedStrategicObjects.Add(marker);
    }

    private void RenderStrategicFactionSidebar()
    {
        headerText.text =
            $"세력 · 던전 팩션 {strategicFactions.Factions.Count}"
            + $" · 인간 지부 {strategicCampaign.Branches.Count}"
            + $" · 이동 경로 {strategicFactions.Routes.Count}";
        AddRightButton(
            "← 월드 지도",
            () =>
            {
                showStrategicFactionSurface = false;
                pendingStrategicBetrayalFactionId = string.Empty;
                RenderStrategic();
            });

        foreach (DungeonFactionState faction in strategicFactions.Factions)
        {
            DungeonFactionState captured = faction;
            FactionDefinitionSnapshot definition =
                strategicFactions.Definitions.FirstOrDefault(value =>
                    value != null
                    && value.StableId == captured.factionId);
            AddRightButton(
                $"{(captured.factionId == selectedStrategicFactionId ? "●" : "○")} "
                    + $"{definition?.DisplayName ?? captured.factionId} "
                    + $"[{captured.trust}]",
                () =>
                {
                    selectedStrategicFactionId = captured.factionId;
                    selectedStrategicHumanBranchId = string.Empty;
                    pendingStrategicBetrayalFactionId = string.Empty;
                    strategicStatus = string.Empty;
                    RenderStrategic();
                });
        }

        foreach (HumanInvasionBranchState branch in strategicCampaign.Branches)
        {
            HumanInvasionBranchState captured = branch;
            AddRightButton(
                $"{(captured.branchId == selectedStrategicHumanBranchId ? "●" : "○")} "
                    + $"{captured.displayName} {captured.strength:0}",
                () =>
                {
                    selectedStrategicHumanBranchId = captured.branchId;
                    selectedStrategicFactionId = string.Empty;
                    pendingStrategicBetrayalFactionId = string.Empty;
                    strategicStatus = string.Empty;
                    RenderStrategic();
                },
                new Color(0.38f, 0.18f, 0.16f, 1f));
        }

        if (!string.IsNullOrWhiteSpace(selectedStrategicFactionId)
            && strategicFactions.TryGetFaction(
                selectedStrategicFactionId,
                out DungeonFactionState selectedFaction))
        {
            RenderStrategicFactionCommands(selectedFaction);
            detailText.text = BuildStrategicFactionDetail(selectedFaction)
                + BuildStatusText();
        }
        else if (!string.IsNullOrWhiteSpace(selectedStrategicHumanBranchId)
            && strategicCampaign.TryGetBranch(
                selectedStrategicHumanBranchId,
                out HumanInvasionBranchState selectedBranch))
        {
            detailText.text = BuildStrategicHumanBranchDetail(selectedBranch)
                + BuildStatusText();
        }
        else
        {
            detailText.text =
                "던전 팩션을 선택해 신뢰·계약·지원군을 관리하거나,\n"
                + "인간 지부를 선택해 전력과 지원 거점 회복 원인을 확인하십시오.";
        }

        AddRightButton("지도 맞춤", ResetStrategicMapView);
        AddRightButton("닫기", Hide);
    }

    private void RenderStrategicFactionCommands(DungeonFactionState faction)
    {
        AddRightButton(
            "호의 물자 50 전달",
            () =>
            {
                strategicFactions.TryOfferGoodwill(
                    faction.factionId,
                    50,
                    out strategicStatus);
                RenderStrategic();
            },
            new Color(0.18f, 0.34f, 0.25f, 1f));
        if (strategicFactions.IsContractUnlocked(
                faction.factionId,
                FactionContractKind.Trade))
        {
            AddRightButton(
                "교역 상단 요청",
                () =>
                {
                    strategicFactions.TryRequestTrade(
                        faction.factionId,
                        out _,
                        out strategicStatus);
                    RenderStrategic();
                });
        }

        if (strategicFactions.IsContractUnlocked(
                faction.factionId,
                FactionContractKind.Supply))
        {
            AddRightButton(
                "물자 지원 요청",
                () =>
                {
                    strategicFactions.TryRequestSupply(
                        faction.factionId,
                        out _,
                        out strategicStatus);
                    RenderStrategic();
                });
        }

        if (faction.trust >= 70 && !faction.allianceProjectCompleted)
        {
            AddRightButton(
                "동맹 프로젝트 완료",
                () =>
                {
                    strategicFactions.TryCompleteAllianceProject(
                        faction.factionId,
                        out strategicStatus);
                    RenderStrategic();
                });
        }

        if (strategicFactions.IsContractUnlocked(
                faction.factionId,
                FactionContractKind.Reinforcement))
        {
            AddRightButton(
                "지원군 요청",
                () =>
                {
                    strategicFactions.TryRequestReinforcement(
                        faction.factionId,
                        out _,
                        out strategicStatus);
                    RenderStrategic();
                },
                new Color(0.22f, 0.32f, 0.42f, 1f));
        }

        bool confirming = string.Equals(
            pendingStrategicBetrayalFactionId,
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
                    pendingStrategicBetrayalFactionId = faction.factionId;
                    strategicStatus =
                        "대상 신뢰 -100, 다른 던전 -15, 10일 협상 봉쇄. "
                        + "같은 버튼을 다시 눌러 확정하십시오.";
                }
                else
                {
                    strategicFactions.TryBetray(
                        faction.factionId,
                        300,
                        out strategicStatus);
                    pendingStrategicBetrayalFactionId = string.Empty;
                }
                RenderStrategic();
            },
            new Color(0.46f, 0.16f, 0.14f, 1f));
    }

    private string BuildStrategicFactionDetail(DungeonFactionState faction)
    {
        FactionDefinitionSnapshot definition =
            strategicFactions.Definitions.FirstOrDefault(value =>
                value != null && value.StableId == faction.factionId);
        string contracts = string.Join(
            " · ",
            Enum.GetValues(typeof(FactionContractKind))
                .Cast<FactionContractKind>()
                .Where(kind => strategicFactions.IsContractUnlocked(
                    faction.factionId,
                    kind))
                .Select(kind => kind.ToString()));
        return $"{definition?.DisplayName ?? faction.factionId}\n"
            + $"{definition?.Description}\n"
            + $"거점: ({faction.homeQ}, {faction.homeR})\n"
            + $"신뢰 {faction.trust} / 배신의 흔적 {faction.betrayalScars}\n"
            + $"해금 계약: {(string.IsNullOrWhiteSpace(contracts) ? "없음" : contracts)}\n"
            + $"협상 봉쇄 종료: Day {faction.negotiationBlockedUntilDay}\n"
            + $"지원군 손실: 사망 {faction.reinforcementDeaths} · 장비 {faction.equipmentLosses}\n"
            + $"복구 배상 요구: {faction.restitutionRequiredValue}";
    }

    private string BuildStrategicHumanBranchDetail(
        HumanInvasionBranchState branch)
    {
        HumanSupportSiteState[] sites = strategicCampaign.SupportSites
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
}
