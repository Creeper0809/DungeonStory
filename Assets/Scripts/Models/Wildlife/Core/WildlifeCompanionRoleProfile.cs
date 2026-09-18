using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

[Serializable]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class WildlifeCompanionRoleProfile
{
    public const float MaximumDecisionIntervalSeconds = 0.5f;

    [SerializeField, Min(1)] private int closeRadiusCells;
    [SerializeField, Min(1)] private int resumeFollowDistanceCells;
    [SerializeField, Min(1)] private int ownerTargetLeashCells;
    [SerializeField, Min(0.01f)] private float decisionIntervalSeconds;
    [SerializeField, Min(0.01f)] private float baseAttackDamage;
    [SerializeField, Min(1)] private int meleeRangeCells;
    [SerializeField, Min(0.01f)] private float attackCooldownSeconds;
    [SerializeField, Min(1)] private int maximumCompanionsPerOwner;

    public WildlifeCompanionRoleProfile(
        int closeRadiusCells,
        int resumeFollowDistanceCells,
        int ownerTargetLeashCells,
        float decisionIntervalSeconds,
        float baseAttackDamage,
        int meleeRangeCells,
        float attackCooldownSeconds,
        int maximumCompanionsPerOwner)
    {
        this.closeRadiusCells = closeRadiusCells;
        this.resumeFollowDistanceCells = resumeFollowDistanceCells;
        this.ownerTargetLeashCells = ownerTargetLeashCells;
        this.decisionIntervalSeconds = decisionIntervalSeconds;
        this.baseAttackDamage = baseAttackDamage;
        this.meleeRangeCells = meleeRangeCells;
        this.attackCooldownSeconds = attackCooldownSeconds;
        this.maximumCompanionsPerOwner = maximumCompanionsPerOwner;
        RequireValid();
    }

    public int CloseRadiusCells => closeRadiusCells;
    public int ResumeFollowDistanceCells => resumeFollowDistanceCells;
    public int OwnerTargetLeashCells => ownerTargetLeashCells;
    public float DecisionIntervalSeconds => decisionIntervalSeconds;
    public float BaseAttackDamage => baseAttackDamage;
    public int MeleeRangeCells => meleeRangeCells;
    public float AttackCooldownSeconds => attackCooldownSeconds;
    public int MaximumCompanionsPerOwner => maximumCompanionsPerOwner;

    public static WildlifeCompanionRoleProfile OwnerCompanion(
        int closeRadiusCells = 2,
        int resumeFollowDistanceCells = 3,
        int ownerTargetLeashCells = 6,
        float decisionIntervalSeconds = MaximumDecisionIntervalSeconds,
        float baseAttackDamage = 6f,
        int meleeRangeCells = 1,
        float attackCooldownSeconds = 2f,
        int maximumCompanionsPerOwner = 1) =>
        new(
            closeRadiusCells,
            resumeFollowDistanceCells,
            ownerTargetLeashCells,
            decisionIntervalSeconds,
            baseAttackDamage,
            meleeRangeCells,
            attackCooldownSeconds,
            maximumCompanionsPerOwner);

    public WildlifeCompanionRoleProfile Snapshot()
    {
        RequireValid();
        return new WildlifeCompanionRoleProfile(
            closeRadiusCells,
            resumeFollowDistanceCells,
            ownerTargetLeashCells,
            decisionIntervalSeconds,
            baseAttackDamage,
            meleeRangeCells,
            attackCooldownSeconds,
            maximumCompanionsPerOwner);
    }

    public IReadOnlyList<string> Validate(string speciesId = "")
    {
        List<string> errors = new();
        string owner = string.IsNullOrWhiteSpace(speciesId)
            ? "Wildlife companion role profile"
            : $"Wildlife species '{speciesId.Trim()}' companion role profile";
        if (closeRadiusCells < 1)
        {
            errors.Add($"{owner} close radius must be positive.");
        }
        if (resumeFollowDistanceCells <= closeRadiusCells)
        {
            errors.Add($"{owner} resume distance must exceed its close radius.");
        }
        if (ownerTargetLeashCells < resumeFollowDistanceCells)
        {
            errors.Add($"{owner} target leash must cover its resume distance.");
        }
        if (!IsFinitePositive(decisionIntervalSeconds)
            || decisionIntervalSeconds > MaximumDecisionIntervalSeconds)
        {
            errors.Add(
                $"{owner} decision interval must be positive and at most "
                + $"{MaximumDecisionIntervalSeconds:0.###} game seconds.");
        }
        if (!IsFinitePositive(baseAttackDamage))
        {
            errors.Add($"{owner} attack damage must be finite and positive.");
        }
        if (meleeRangeCells != 1)
        {
            errors.Add($"{owner} currently supports exactly one-cell melee attacks.");
        }
        if (!IsFinitePositive(attackCooldownSeconds))
        {
            errors.Add($"{owner} attack cooldown must be finite and positive.");
        }
        if (maximumCompanionsPerOwner != 1)
        {
            errors.Add($"{owner} currently supports exactly one companion per owner.");
        }
        return errors;
    }

    public void RequireValid(string speciesId = "")
    {
        IReadOnlyList<string> errors = Validate(speciesId);
        if (errors.Count > 0)
        {
            throw new InvalidOperationException(string.Join(" | ", errors));
        }
    }

    private static bool IsFinitePositive(float value) =>
        !float.IsNaN(value) && !float.IsInfinity(value) && value > 0f;
}
