using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using UnityEngine;

public static class OffenseFormationUtility
{
    public static OffenseFormationMask ToMask(OffenseFormationSlot slot)
    {
        return slot switch
        {
            OffenseFormationSlot.Front => OffenseFormationMask.Front,
            OffenseFormationSlot.Middle => OffenseFormationMask.Middle,
            _ => OffenseFormationMask.Rear
        };
    }

    public static string GetDisplayName(OffenseFormationSlot slot)
    {
        return slot switch
        {
            OffenseFormationSlot.Front => "전열",
            OffenseFormationSlot.Middle => "중열",
            _ => "후열"
        };
    }
}

public static class OffenseRouteGenerator
{
    public static OffenseRouteGraph Create(OffenseTargetDefinition target)
    {
        string targetId = !string.IsNullOrWhiteSpace(target?.id) ? target.id : "unknown";
        int stage = Mathf.Max(1, target?.campaignOrder ?? 1);
        string entrance = $"{targetId}:entrance";
        string firstBattle = $"{targetId}:approach-battle";
        string firstEvent = $"{targetId}:approach-event";
        string camp = $"{targetId}:camp";
        string cache = $"{targetId}:cache";
        string elite = $"{targetId}:elite-battle";
        string lateEvent = $"{targetId}:deep-event";
        string boss = $"{targetId}:boss";

        return new OffenseRouteGraph(new[]
        {
            Node(entrance, 0, 0, OffenseRouteNodeKind.Entrance,
                "원정 입구", "보급을 점검하고 첫 경로를 고릅니다.", 0f,
                firstBattle, firstEvent),
            Node(firstBattle, 1, 0, OffenseRouteNodeKind.Battle,
                "경계 병력", "정면 경계를 돌파하는 빠르고 위험한 길입니다.", 0.75f,
                camp, cache),
            Node(firstEvent, 1, 1, OffenseRouteNodeKind.Event,
                "수상한 흔적", "도구를 쓰면 위험을 줄이고 정보를 얻을 수 있습니다.", 0.55f,
                camp, cache),
            Node(camp, 2, 0, OffenseRouteNodeKind.Camp,
                "숨 돌릴 곳", "식량을 사용해 체력과 스트레스를 회복할 수 있습니다.", 0.35f,
                elite, lateEvent),
            Node(cache, 2, 1, OffenseRouteNodeKind.Cache,
                "버려진 보급고", "운반 가능한 전리품이 있지만 더 깊은 길로 이어집니다.", 0.6f,
                elite, lateEvent),
            Node(elite, 3, 0, OffenseRouteNodeKind.Battle,
                "정예 수비대", "강한 적을 쓰러뜨리면 추가 전리품을 확보합니다.", 0.95f + stage * 0.03f,
                boss),
            Node(lateEvent, 3, 1, OffenseRouteNodeKind.Event,
                "봉인된 통로", "도구로 봉인을 해제하거나 위험을 감수해야 합니다.", 0.8f,
                boss),
            Node(boss, 4, 0, OffenseRouteNodeKind.Boss,
                target?.title ?? "지역 지휘관", "이 지역의 지휘관을 쓰러뜨려 원정을 끝냅니다.", 1f,
                Array.Empty<string>())
        }, entrance);
    }

    private static OffenseRouteNode Node(
        string id,
        int depth,
        int lane,
        OffenseRouteNodeKind kind,
        string title,
        string description,
        float danger,
        params string[] next)
    {
        return new OffenseRouteNode(id, depth, lane, kind, title, description, danger, next);
    }
}

public static class OffenseSupplyCatalog
{
    public static StockCategory GetStockCategory(OffenseSupplyType type)
    {
        return type switch
        {
            OffenseSupplyType.Rations => StockCategory.Food,
            OffenseSupplyType.Medicine => StockCategory.General,
            OffenseSupplyType.Tools => StockCategory.Weapon,
            _ => StockCategory.Mana
        };
    }

    public static string GetDisplayName(OffenseSupplyType type)
    {
        return type switch
        {
            OffenseSupplyType.Rations => "식량",
            OffenseSupplyType.Medicine => "치료약",
            OffenseSupplyType.Tools => "원정 도구",
            _ => "마력등"
        };
    }
}

public sealed class OffenseExpeditionMemberState
{
    public OffenseExpeditionMemberState(
        CharacterActor actor,
        OffenseFormationSlot formation,
        float stress = 0f)
    {
        Actor = actor;
        Formation = formation;
        Stress = Mathf.Clamp(stress, 0f, 100f);
    }

    public CharacterActor Actor { get; }
    public OffenseFormationSlot Formation { get; internal set; }
    public float Stress { get; private set; }
    public float TotalDamageTaken { get; private set; }
    public bool IsAlive => Actor != null && !Actor.IsDead;

    public void AddStress(float amount)
    {
        Stress = Mathf.Clamp(Stress + Mathf.Max(0f, amount), 0f, 100f);
    }

    public void RecoverStress(float amount)
    {
        Stress = Mathf.Clamp(Stress - Mathf.Max(0f, amount), 0f, 100f);
    }

    public void RecordDamage(float amount)
    {
        TotalDamageTaken += Mathf.Max(0f, amount);
    }

    public void Restore(
        OffenseFormationSlot formation,
        float stress,
        float totalDamageTaken)
    {
        Formation = formation;
        Stress = Mathf.Clamp(stress, 0f, 100f);
        TotalDamageTaken = Mathf.Max(0f, totalDamageTaken);
    }
}
