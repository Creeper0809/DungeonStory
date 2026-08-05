using System;
using System.Collections.Generic;
using UnityEngine;

public readonly struct DefenseInterceptPlan
{
    public DefenseInterceptPlan(
        Vector2Int intruderStopCell,
        Vector2Int guardCell,
        Vector2Int reserveCell,
        Queue<GridMoveStep> leadPath,
        int intruderSteps)
    {
        IntruderStopCell = intruderStopCell;
        GuardCell = guardCell;
        ReserveCell = reserveCell;
        LeadPath = leadPath ?? new Queue<GridMoveStep>();
        IntruderSteps = Mathf.Max(0, intruderSteps);
    }

    public Vector2Int IntruderStopCell { get; }
    public Vector2Int GuardCell { get; }
    public Vector2Int ReserveCell { get; }
    public Queue<GridMoveStep> LeadPath { get; }
    public int IntruderSteps { get; }
}

public readonly struct DefenseEngagementIdentitySnapshot
{
    public DefenseEngagementIdentitySnapshot(
        string engagementId,
        string intruderRuntimeId,
        CharacterId intruderCharacterId,
        CharacterId leadGuardId,
        CharacterId reserveGuardId,
        CharacterId rangedGuardId,
        CharacterId secondaryRangedGuardId,
        DefenseEngagementState state)
    {
        EngagementId = engagementId?.Trim() ?? string.Empty;
        IntruderRuntimeId = intruderRuntimeId?.Trim() ?? string.Empty;
        IntruderCharacterId = intruderCharacterId;
        LeadGuardId = leadGuardId;
        ReserveGuardId = reserveGuardId;
        RangedGuardId = rangedGuardId;
        SecondaryRangedGuardId = secondaryRangedGuardId;
        State = state;
    }

    public string EngagementId { get; }
    public string IntruderRuntimeId { get; }
    public CharacterId IntruderCharacterId { get; }
    public CharacterId LeadGuardId { get; }
    public CharacterId ReserveGuardId { get; }
    public CharacterId RangedGuardId { get; }
    public CharacterId SecondaryRangedGuardId { get; }
    public DefenseEngagementState State { get; }
}

public static class DefenseCombatFormulaRules
{
    public static float CalculateDamage(
        float attack,
        float strength,
        float combatPowerMultiplier,
        float defenderToughness,
        float attackMultiplier = 1f)
    {
        float raw = 4f + Mathf.Max(0f, attack) * 1.2f + Mathf.Max(0f, strength) * 0.6f;
        float mitigation = Mathf.Clamp(Mathf.Max(0f, defenderToughness) * 0.025f, 0f, 0.45f);
        return Mathf.Max(1f, raw
            * Mathf.Max(0.01f, combatPowerMultiplier)
            * Mathf.Max(0.01f, attackMultiplier)
            * (1f - mitigation));
    }

    public static float CalculateAttackInterval(float dexterity, float attackSpeedMultiplier = 1f)
    {
        float interval = Mathf.Clamp(1.25f - Mathf.Max(0f, dexterity) * 0.05f, 0.55f, 1.2f);
        return Mathf.Clamp(interval / Mathf.Max(0.1f, attackSpeedMultiplier), 0.35f, 1.5f);
    }
}
