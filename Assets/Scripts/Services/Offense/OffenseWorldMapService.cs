using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public static class OffenseWorldMapService
{
    public const int MaxReconLevel = 3;
    public const string TruthTargetId = "truth_core";
    public const string TruthTitle = "던전의 진실";
    public const string TruthRevealText =
        "이 던전은 몬스터를 가두기 위한 감옥이 아니었습니다. 지상의 왕국이 마력과 노동을 수확하려고 만든 거대한 장치였고, 반복된 침공은 그 증거를 지우기 위한 봉쇄였습니다.";

    public static float GetScanRange(int reconLevel)
    {
        return Mathf.Max(0, reconLevel) switch
        {
            0 => 8f,
            1 => 14f,
            2 => 22f,
            _ => 999f
        };
    }

    public static IReadOnlyList<OffenseTargetDefinition> CreateDefaultTargets()
    {
        OffenseTargetDefinition[] targets = new[]
        {
            CreateTarget(
                "food_farm",
                "외곽 식재료 농장",
                "던전 근처의 작은 보급지입니다. 초반 식재료 수급을 노리기 쉽습니다.",
                OffenseTargetKind.ResourceSite,
                4f,
                8f,
                55f,
                1,
                10f,
                Reward(new OffenseStockRewardSpec(StockCategory.Food), "식재료", 40),
                Reward(new OffenseMoneyRewardSpec(), "약탈금", 80)),
            CreateTarget(
                "merchant_road",
                "상단 교역로",
                "인간 상단이 오가는 교역로입니다. 상업·물류 계보를 여는 설계도와 잡화를 노릴 수 있습니다.",
                OffenseTargetKind.HumanOutpost,
                6f,
                14f,
                70f,
                1,
                16f,
                Reward(new OffenseStockRewardSpec(StockCategory.General), "잡화", 30),
                Reward(new OffenseMoneyRewardSpec(), "약탈금", 120),
                Reward(
                    new OffenseSpecificBlueprintRewardSpec(OffenseStrategyBlueprintIds.CommerceLogistics),
                    "상권 통합 설계도",
                    1)),
            CreateTarget(
                "old_armory",
                "낡은 무기고",
                "방치된 무기고입니다. 방어 시설 합성 재료와 무기 재고를 기대할 수 있습니다.",
                OffenseTargetKind.HumanOutpost,
                12f,
                28f,
                100f,
                2,
                32f,
                Reward(new OffenseStockRewardSpec(StockCategory.Weapon), "무기", 25),
                Reward(
                    new OffenseSpecificBlueprintRewardSpec(OffenseStrategyBlueprintIds.FortressDefense),
                    "전술 지휘 설계도",
                    1)),
            CreateTarget(
                "mana_ruins",
                "마력 유적",
                "마력 흔적이 강한 폐허입니다. 위험하지만 연구와 합성 보상이 좋습니다.",
                OffenseTargetKind.SpecialEvent,
                18f,
                38f,
                130f,
                2,
                42f,
                Reward(new OffenseStockRewardSpec(StockCategory.Mana), "마력", 35),
                Reward(
                    new OffenseSpecificBlueprintRewardSpec(OffenseStrategyBlueprintIds.ArcaneResearch),
                    "비전 공명 설계도",
                    1)),
            CreateTarget(
                "rival_dungeon",
                "경쟁 던전 전초기지",
                "다른 몬스터 세력의 전초기지입니다. 성공하면 희귀 시설과 세력 약화를 기대할 수 있습니다.",
                OffenseTargetKind.RivalDungeon,
                26f,
                55f,
                180f,
                3,
                60f,
                Reward(new OffenseRareFacilityRewardSpec(), "희귀 시설", 1),
                Reward(new OffenseHumanFactionWeakeningRewardSpec(), "인간 세력 약화", 1),
                Reward(new OffenseRecruitCandidateRewardSpec(), "직원 후보", 1),
                Reward(new OffenseSpecialMonsterRewardSpec(), "특수 몬스터", 1)),
            CreateTarget(
                TruthTargetId,
                "봉인된 진실의 심장부",
                "모든 원정 기록이 가리키는 지하 심층부입니다. 봉인을 깨고 내부 기록을 확보하면 이 던전과 침공의 진실을 밝혀낼 수 있습니다.",
                OffenseTargetKind.SpecialEvent,
                36f,
                75f,
                240f,
                3,
                85f,
                Reward(new OffenseMoneyRewardSpec(), "회수 자금", 500),
                Reward(new OffenseRivalFactionWeakeningRewardSpec(), "적대 세력 붕괴", 2))
        };

        for (int i = 0; i < targets.Length; i++)
        {
            targets[i].campaignOrder = i + 1;
            targets[i].prerequisiteTargetId = i > 0 ? targets[i - 1].id : string.Empty;
        }

        OffenseTargetDefinition truthTarget = targets[targets.Length - 1];
        truthTarget.revealsTruth = true;
        truthTarget.truthText = TruthRevealText;
        return targets;
    }

    public static IReadOnlyList<OffenseTargetDefinition> NormalizeTargets(IEnumerable<OffenseTargetDefinition> targets)
    {
        List<OffenseTargetDefinition> source = targets?
            .Where((target) => target != null && target.IsValid)
            .Select((target) => target.CreateRuntimeCopy())
            .OrderBy((target) => target.distance)
            .ThenBy((target) => target.id)
            .ToList()
            ?? new List<OffenseTargetDefinition>();

        if (source.Count == 0)
        {
            return CreateDefaultTargets();
        }

        bool hasUniqueOrders = source.All(target => target.campaignOrder > 0)
            && source.Select(target => target.campaignOrder).Distinct().Count() == source.Count;
        if (!hasUniqueOrders)
        {
            for (int i = 0; i < source.Count; i++)
            {
                source[i].campaignOrder = i + 1;
            }
        }

        source = source
            .OrderBy(target => target.campaignOrder)
            .ThenBy(target => target.distance)
            .ThenBy(target => target.id)
            .ToList();
        for (int i = 1; i < source.Count; i++)
        {
            if (string.IsNullOrWhiteSpace(source[i].prerequisiteTargetId))
            {
                source[i].prerequisiteTargetId = source[i - 1].id;
            }
        }

        if (!source.Any(target => target.revealsTruth))
        {
            OffenseTargetDefinition terminal = source[source.Count - 1];
            terminal.revealsTruth = true;
            terminal.truthText = TruthRevealText;
        }

        return source;
    }

    public static int RevealTargetsInRange(
        OffenseWorldMapState state,
        IEnumerable<OffenseTargetDefinition> targets)
    {
        if (state == null)
        {
            return 0;
        }

        float scanRange = GetScanRange(state.ReconLevel);
        int added = 0;
        foreach (OffenseTargetDefinition target in NormalizeTargets(targets))
        {
            if (target.distance <= scanRange && state.AddKnownTarget(target.id))
            {
                added++;
            }
        }

        return added;
    }

    public static IReadOnlyList<OffenseTargetSnapshot> GetVisibleTargetSnapshots(
        IOffenseWorldMapStateView state,
        IEnumerable<OffenseTargetDefinition> targets,
        bool preciseIntel)
    {
        if (state == null)
        {
            return Array.Empty<OffenseTargetSnapshot>();
        }

        return NormalizeTargets(targets)
            .Where((target) => state.KnowTarget(target.id))
            .Select((target) => target.ToSnapshot(preciseIntel, state))
            .ToList();
    }

    public static bool CanAttemptTarget(
        IOffenseWorldMapStateView state,
        OffenseTargetDefinition target,
        out string reason)
    {
        if (state == null || target == null)
        {
            reason = "오펜스 캠페인이 준비되지 않았습니다.";
            return false;
        }

        if (state.TruthRevealed)
        {
            reason = "이미 던전의 진실을 밝혔습니다.";
            return false;
        }

        if (state.IsTargetCompleted(target.id))
        {
            reason = "이미 완료한 오펜스 목표입니다.";
            return false;
        }

        if (!string.IsNullOrWhiteSpace(target.prerequisiteTargetId)
            && !state.IsTargetCompleted(target.prerequisiteTargetId))
        {
            reason = "앞선 오펜스 목표를 먼저 완료해야 합니다.";
            return false;
        }

        reason = string.Empty;
        return true;
    }

    public static OffenseTargetDefinition FindKnownTarget(
        IOffenseWorldMapStateView state,
        IEnumerable<OffenseTargetDefinition> targets,
        string targetId)
    {
        if (state == null || string.IsNullOrWhiteSpace(targetId) || !state.KnowTarget(targetId))
        {
            return null;
        }

        return NormalizeTargets(targets).FirstOrDefault((target) => target.id == targetId);
    }

    private static OffenseTargetDefinition CreateTarget(
        string id,
        string title,
        string description,
        OffenseTargetKind kind,
        float distance,
        float danger,
        float durationSeconds,
        int requiredMembers,
        float requiredPower,
        params OffenseRewardPreview[] rewards)
    {
        return new OffenseTargetDefinition
        {
            id = id,
            title = title,
            description = description,
            kind = kind,
            distance = distance,
            danger = danger,
            durationSeconds = durationSeconds,
            requiredMembers = requiredMembers,
            requiredPower = requiredPower,
            rewards = rewards ?? Array.Empty<OffenseRewardPreview>()
        };
    }

    private static OffenseRewardPreview Reward(
        OffenseRewardGrantSpec grantSpec,
        string label,
        int amount)
    {
        return new OffenseRewardPreview(label, amount, grantSpec);
    }
}
