using System.Collections.Generic;
using UnityEngine;

public sealed class DefenseEngagement
{
    public string Id { get; internal set; } = string.Empty;
    public InvasionIntruderRuntime Intruder { get; internal set; }
    public CharacterActor LeadGuard { get; internal set; }
    public CharacterActor ReserveGuard { get; internal set; }
    public CharacterActor RangedGuard { get; internal set; }
    public CharacterActor SecondaryRangedGuard { get; internal set; }
    public DefenseEngagementState State { get; internal set; }
    public Vector2Int IntruderStopCell { get; internal set; }
    public Vector2Int GuardCell { get; internal set; }
    public Vector2Int ReserveCell { get; internal set; }
    public Vector2Int RangedCell { get; internal set; }
    public Vector2Int SecondaryRangedCell { get; internal set; }
    public bool HasReserveCell { get; internal set; }
    public bool IsOwnerFinalDefense { get; internal set; }
    public bool Forced { get; internal set; }
    public float NextGuardAttackAt { get; internal set; }
    public float NextIntruderAttackAt { get; internal set; }
    public float NextRangedAttackAt { get; internal set; }
    public float NextRangedReplanAt { get; internal set; }
    public float NextSecondaryRangedAttackAt { get; internal set; }
    public float NextSecondaryRangedReplanAt { get; internal set; }
    public int ExchangeCount { get; internal set; }
    public string StatusText { get; internal set; } = string.Empty;
    internal Coroutine LeadMovement { get; set; }
    internal Coroutine ReserveMovement { get; set; }
    internal Coroutine RangedMovement { get; set; }
    internal Coroutine SecondaryRangedMovement { get; set; }
    internal bool LeadArrived { get; set; }
    internal bool ReserveArrived { get; set; }
    internal bool RangedArrived { get; set; }
    internal bool SecondaryRangedArrived { get; set; }

    public CharacterActor IntruderActor => Intruder != null
        ? Intruder.IntruderActor
        : null;
    public bool IsActive => State != DefenseEngagementState.Completed;

    public DefenseEngagementIdentitySnapshot CaptureIdentitySnapshot()
    {
        return new DefenseEngagementIdentitySnapshot(
            Id,
            Intruder != null ? Intruder.RuntimeId : string.Empty,
            GetCharacterId(IntruderActor),
            GetCharacterId(LeadGuard),
            GetCharacterId(ReserveGuard),
            GetCharacterId(RangedGuard),
            GetCharacterId(SecondaryRangedGuard),
            State);
    }

    private static CharacterId GetCharacterId(CharacterActor actor) =>
        actor != null ? actor.BuildingCharacterId : default;
}

internal static class DefenseRangedSupportAccess
{
    public static CharacterActor GetRangedGuard(
        DefenseEngagement engagement,
        bool secondary) => engagement == null
            ? null
            : secondary
                ? engagement.SecondaryRangedGuard
                : engagement.RangedGuard;

    public static void SetRangedGuard(
        DefenseEngagement engagement,
        bool secondary,
        CharacterActor guard)
    {
        if (secondary)
        {
            engagement.SecondaryRangedGuard = guard;
            return;
        }
        engagement.RangedGuard = guard;
    }

    public static Vector2Int GetRangedCell(
        DefenseEngagement engagement,
        bool secondary) => secondary
            ? engagement.SecondaryRangedCell
            : engagement.RangedCell;

    public static void SetRangedCell(
        DefenseEngagement engagement,
        bool secondary,
        Vector2Int cell)
    {
        if (secondary)
        {
            engagement.SecondaryRangedCell = cell;
            return;
        }
        engagement.RangedCell = cell;
    }

    public static Coroutine GetRangedMovement(
        DefenseEngagement engagement,
        bool secondary) => secondary
            ? engagement.SecondaryRangedMovement
            : engagement.RangedMovement;

    public static void SetRangedMovement(
        DefenseEngagement engagement,
        bool secondary,
        Coroutine movement)
    {
        if (secondary)
        {
            engagement.SecondaryRangedMovement = movement;
            return;
        }
        engagement.RangedMovement = movement;
    }

    public static bool GetRangedArrived(
        DefenseEngagement engagement,
        bool secondary) => secondary
            ? engagement.SecondaryRangedArrived
            : engagement.RangedArrived;

    public static void SetRangedArrived(
        DefenseEngagement engagement,
        bool secondary,
        bool arrived)
    {
        if (secondary)
        {
            engagement.SecondaryRangedArrived = arrived;
            return;
        }
        engagement.RangedArrived = arrived;
    }

    public static float GetNextRangedAttackAt(
        DefenseEngagement engagement,
        bool secondary) => secondary
            ? engagement.NextSecondaryRangedAttackAt
            : engagement.NextRangedAttackAt;

    public static void SetNextRangedAttackAt(
        DefenseEngagement engagement,
        bool secondary,
        float time)
    {
        if (secondary)
        {
            engagement.NextSecondaryRangedAttackAt = time;
            return;
        }
        engagement.NextRangedAttackAt = time;
    }

    public static float GetNextRangedReplanAt(
        DefenseEngagement engagement,
        bool secondary) => secondary
            ? engagement.NextSecondaryRangedReplanAt
            : engagement.NextRangedReplanAt;

    public static void SetNextRangedReplanAt(
        DefenseEngagement engagement,
        bool secondary,
        float time)
    {
        if (secondary)
        {
            engagement.NextSecondaryRangedReplanAt = time;
            return;
        }
        engagement.NextRangedReplanAt = time;
    }
}

public interface IDefenseEngagementRuntime
{
    IReadOnlyList<DefenseEngagement> ActiveEngagements { get; }
    IInvasionOwnerEvacuationService OwnerEvacuation { get; }
    IDefenseResponsePolicyRuntime PolicyRuntime { get; }
    string BuildDebugSummary();
    bool TryGetEngagement(
        InvasionIntruderRuntime intruder,
        out DefenseEngagement engagement);
    bool TryGetActorDefenseStatus(
        CharacterActor actor,
        out DefenseEngagement engagement,
        out string role,
        out string status);
    bool IsCellReservedForOther(CharacterActor actor, Vector2Int cell);
    bool ShouldHoldIntruder(InvasionIntruderRuntime intruder);
    bool CanIntruderAdvanceTo(
        InvasionIntruderRuntime intruder,
        Vector2Int nextCell);
    void NotifyIntruderInterceptPathUnavailable(
        InvasionIntruderRuntime intruder,
        string reason);
    bool TryResolveIntruderDefeated(
        InvasionIntruderRuntime intruder,
        out string failureReason);
    bool TryAssignManual(
        CharacterActor defender,
        InvasionIntruderRuntime intruder,
        out string failureReason);
    bool TryBeginOwnerFinalDefense(
        InvasionIntruderRuntime intruder,
        CharacterActor owner);
    void NotifyActorDowned(CharacterActor actor);
    void NotifyIntruderFinished(InvasionIntruderRuntime intruder);
    DefenseEngagementSaveSnapshot Capture();
    void PrepareRestoreCandidate(
        DefenseEngagementSaveSnapshot snapshot,
        DungeonGameRestoreReport report);
    void PublishRestoreCandidate();
    void RollbackPublishedRestoreCandidate();
    void RetirePreviousRestoreProjection();
    void ActivateRestoreProjection();
    void CompleteRestoreCandidate();
    void DiscardRestoreCandidate();
}

public interface IInvasionOwnerEvacuationService
{
    bool IsEvacuating { get; }
    bool HasReachedTarget { get; }
    CharacterActor Owner { get; }
    Vector2Int TargetCell { get; }
    string StatusText { get; }
    OwnerEvacuationSaveSnapshot Capture();
    void PrepareRestoreCandidate(
        OwnerEvacuationSaveSnapshot snapshot,
        DungeonGameRestoreReport report);
    void PublishRestoreCandidate();
    void RollbackPublishedRestoreCandidate();
    void RetirePreviousRestoreProjection();
    void ActivateRestoreProjection();
    void CompleteRestoreCandidate();
    void DiscardRestoreCandidate();
}

public static class DefenseCombatFormula
{
    public static float CalculateDamage(
        float attack,
        float strength,
        float combatPowerMultiplier,
        float defenderToughness,
        float attackMultiplier = 1f) =>
        DefenseCombatFormulaRules.CalculateDamage(
            attack,
            strength,
            combatPowerMultiplier,
            defenderToughness,
            attackMultiplier);

    public static float CalculateAttackInterval(
        float dexterity,
        float attackSpeedMultiplier = 1f) =>
        DefenseCombatFormulaRules.CalculateAttackInterval(
            dexterity,
            attackSpeedMultiplier);
}
