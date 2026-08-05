using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

internal delegate void DefenseGuardMovementStarter(
    Grid grid,
    DefenseEngagement engagement,
    CharacterActor guard,
    Vector2Int target,
    bool reserve,
    Queue<GridMoveStep> initialPath);

internal delegate void DefenseRangedMovementStarter(
    Grid grid,
    DefenseEngagement engagement,
    CharacterActor guard,
    Vector2Int target,
    Queue<GridMoveStep> initialPath,
    bool secondary);

internal sealed class DefenseEngagementPersistence
{
    private readonly DefenseEngagementWorldServices world;
    private readonly IDefenseEngagementStore store;
    private readonly List<DefenseEngagement> restoreCandidates = new();
    private bool restoreCandidatePrepared;
    private bool restorePublicationPending;
    private bool previousProjectionRetired;

    public DefenseEngagementPersistence(
        DefenseEngagementWorldServices world,
        DefenseEngagementCombatServices combat)
    {
        this.world = world ?? throw new ArgumentNullException(nameof(world));
        store = (combat ?? throw new ArgumentNullException(nameof(combat))).Store;
    }

    public DefenseEngagementSaveSnapshot Capture()
    {
        return new DefenseEngagementSaveSnapshot
        {
            engagements = store.Engagements
                .Where(engagement => engagement?.IsActive == true)
                .Select(ToSaveData)
                .ToList()
        };
    }

    public void PrepareRestoreCandidate(
        DefenseEngagementSaveSnapshot snapshot,
        DungeonGameRestoreReport report)
    {
        if (report == null)
        {
            throw new ArgumentNullException(nameof(report));
        }
        if (restoreCandidatePrepared || restoreCandidates.Count > 0)
        {
            report.AddError(
                "A defense engagement restore candidate is already prepared.");
            return;
        }
        if (snapshot?.engagements == null
            || !world.Grid.TryGetGrid(out Grid grid)
            || grid == null)
        {
            report.AddError(
                "Defense engagement restore requires a snapshot and active grid.");
            return;
        }

        foreach (DefenseEngagementSaveData source in snapshot.engagements)
        {
            if (!TryCreateCandidate(source, grid, report, out DefenseEngagement candidate))
            {
                restoreCandidates.Clear();
                return;
            }
            restoreCandidates.Add(candidate);
        }
        restoreCandidatePrepared = true;
    }

    public void PublishRestoreCandidate()
    {
        if (!restoreCandidatePrepared || restorePublicationPending)
        {
            throw new InvalidOperationException(
                "No defense engagement restore candidate is ready to publish.");
        }
        if (!world.Grid.TryGetGrid(out Grid grid) || grid == null)
        {
            throw new InvalidOperationException(
                "Prepared defense engagement grid disappeared before publication.");
        }

        restorePublicationPending = true;
        previousProjectionRetired = false;
    }

    public void RollbackPublishedRestoreCandidate()
    {
        restoreCandidates.Clear();
        restoreCandidatePrepared = false;
        restorePublicationPending = false;
        previousProjectionRetired = false;
    }

    public void RetirePreviousRestoreProjection(
        Action<DefenseEngagement, bool> complete)
    {
        if (!restorePublicationPending || previousProjectionRetired)
        {
            throw new InvalidOperationException(
                "No defense engagement restore projection is ready to retire.");
        }

        foreach (DefenseEngagement engagement in store.Engagements.ToArray())
        {
            complete(engagement, false);
        }
        store.ClearEngagements();
        previousProjectionRetired = true;
    }

    public void ActivateRestoreProjection(
        Action<CharacterActor, string> prepareGuard,
        DefenseGuardMovementStarter startGuardMovement,
        DefenseRangedMovementStarter startRangedMovement)
    {
        if (!restorePublicationPending || !previousProjectionRetired)
        {
            throw new InvalidOperationException(
                "The previous defense engagement projection was not retired.");
        }
        if (!world.Grid.TryGetGrid(out Grid grid) || grid == null)
        {
            throw new InvalidOperationException(
                "Prepared defense engagement grid disappeared before publication.");
        }

        foreach (DefenseEngagement engagement in restoreCandidates)
        {
            store.Add(engagement);
            PrepareAndMove(
                grid,
                engagement,
                prepareGuard,
                startGuardMovement,
                startRangedMovement);
            RestorePresentation(engagement);
        }
        restoreCandidates.Clear();
        restoreCandidatePrepared = false;
        restorePublicationPending = false;
        previousProjectionRetired = false;
    }

    public void DiscardRestoreCandidate()
    {
        if (restorePublicationPending)
        {
            RollbackPublishedRestoreCandidate();
            return;
        }

        restoreCandidates.Clear();
        restoreCandidatePrepared = false;
        previousProjectionRetired = false;
    }

    private bool TryCreateCandidate(
        DefenseEngagementSaveData source,
        Grid grid,
        DungeonGameRestoreReport report,
        out DefenseEngagement engagement)
    {
        engagement = null;
        if (source == null
            || !world.Director.TryGetRestoreCandidate(
                source.intruderId,
                out InvasionIntruderRuntime intruder)
            || FindCharacter(source.leadGuardId) is not CharacterActor lead)
        {
            report.AddError(
                "Defense engagement candidate lost its target or lead guard.");
            return false;
        }
        if (!intruder.HasBreachedDungeonInterior)
        {
            report.AddError(
                $"Defense engagement '{source.id}' references an exterior intruder.");
            return false;
        }

        Vector2Int stopCell = new(source.intruderStopX, source.intruderStopY);
        Vector2Int guardCell = new(source.guardX, source.guardY);
        if (!IsValidFront(grid, stopCell, guardCell))
        {
            report.AddError(
                $"Defense engagement '{source.id}' has an invalid defensive front.");
            return false;
        }

        CharacterActor reserve = FindOptionalCharacter(
            source.reserveGuardId,
            source.id,
            "reserve",
            report);
        CharacterActor ranged = FindOptionalCharacter(
            source.rangedGuardId,
            source.id,
            "ranged",
            report);
        CharacterActor secondaryRanged = FindOptionalCharacter(
            source.secondaryRangedGuardId,
            source.id,
            "secondary ranged",
            report);
        if (!report.Success)
        {
            return false;
        }

        Vector2Int reserveCell = new(source.reserveX, source.reserveY);
        Vector2Int rangedCell = new(source.rangedX, source.rangedY);
        Vector2Int secondaryRangedCell =
            new(source.secondaryRangedX, source.secondaryRangedY);
        if (reserve != null && !IsValidGuardCell(grid, reserveCell)
            || ranged != null && !IsValidGuardCell(grid, rangedCell)
            || secondaryRanged != null
                && !IsValidGuardCell(grid, secondaryRangedCell))
        {
            report.AddError(
                $"Defense engagement '{source.id}' has an invalid support cell.");
            return false;
        }

        float now = world.Clock.Time;
        engagement = new DefenseEngagement
        {
            Id = source.id,
            Intruder = intruder,
            LeadGuard = lead,
            ReserveGuard = reserve,
            RangedGuard = ranged,
            SecondaryRangedGuard = secondaryRanged,
            State = source.state,
            IntruderStopCell = stopCell,
            GuardCell = guardCell,
            ReserveCell = reserveCell,
            RangedCell = rangedCell,
            SecondaryRangedCell = secondaryRangedCell,
            HasReserveCell = source.hasReserveCell,
            IsOwnerFinalDefense = source.ownerFinalDefense,
            Forced = source.forced,
            NextGuardAttackAt = now + source.guardAttackRemaining,
            NextIntruderAttackAt = now + source.intruderAttackRemaining,
            NextRangedAttackAt = now + source.rangedAttackRemaining,
            NextRangedReplanAt = now + 0.25f,
            NextSecondaryRangedAttackAt =
                now + source.secondaryRangedAttackRemaining,
            NextSecondaryRangedReplanAt = now + 0.25f,
            ExchangeCount = source.exchangeCount,
            LeadArrived = lead.GetNowXY() == guardCell,
            ReserveArrived = reserve != null && reserve.GetNowXY() == reserveCell,
            RangedArrived = ranged != null && ranged.GetNowXY() == rangedCell,
            SecondaryRangedArrived = secondaryRanged != null
                && secondaryRanged.GetNowXY() == secondaryRangedCell,
            StatusText = "Restored defense engagement"
        };
        return true;
    }

    private CharacterActor FindOptionalCharacter(
        string persistentId,
        string engagementId,
        string role,
        DungeonGameRestoreReport report)
    {
        if (string.IsNullOrEmpty(persistentId))
        {
            return null;
        }
        CharacterActor actor = FindCharacter(persistentId);
        if (actor == null)
        {
            report.AddError(
                $"Defense engagement '{engagementId}' lost its {role} guard '{persistentId}'.");
        }
        return actor;
    }

    private static bool IsValidGuardCell(Grid grid, Vector2Int cell)
    {
        return grid.IsValidGridPos(cell)
            && grid.IsWalkable(cell)
            && grid.GetGridCell(cell)?.AreaType ==
                GridCellAreaType.DungeonInterior;
    }

    private DefenseEngagementSaveData ToSaveData(DefenseEngagement engagement)
    {
        float now = world.Clock.Time;
        return new DefenseEngagementSaveData
        {
            id = engagement.Id,
            intruderId = GetPersistentId(engagement.IntruderActor),
            leadGuardId = GetPersistentId(engagement.LeadGuard),
            reserveGuardId = GetPersistentId(engagement.ReserveGuard),
            rangedGuardId = GetPersistentId(engagement.RangedGuard),
            secondaryRangedGuardId = GetPersistentId(engagement.SecondaryRangedGuard),
            state = engagement.State,
            intruderStopX = engagement.IntruderStopCell.x,
            intruderStopY = engagement.IntruderStopCell.y,
            guardX = engagement.GuardCell.x,
            guardY = engagement.GuardCell.y,
            reserveX = engagement.ReserveCell.x,
            reserveY = engagement.ReserveCell.y,
            rangedX = engagement.RangedCell.x,
            rangedY = engagement.RangedCell.y,
            secondaryRangedX = engagement.SecondaryRangedCell.x,
            secondaryRangedY = engagement.SecondaryRangedCell.y,
            hasReserveCell = engagement.HasReserveCell,
            ownerFinalDefense = engagement.IsOwnerFinalDefense,
            forced = engagement.Forced,
            guardAttackRemaining = Mathf.Max(0f, engagement.NextGuardAttackAt - now),
            intruderAttackRemaining = Mathf.Max(0f, engagement.NextIntruderAttackAt - now),
            rangedAttackRemaining = Mathf.Max(0f, engagement.NextRangedAttackAt - now),
            secondaryRangedAttackRemaining = Mathf.Max(
                0f,
                engagement.NextSecondaryRangedAttackAt - now),
            exchangeCount = engagement.ExchangeCount
        };
    }

    private static void PrepareAndMove(
        Grid grid,
        DefenseEngagement engagement,
        Action<CharacterActor, string> prepareGuard,
        DefenseGuardMovementStarter startGuardMovement,
        DefenseRangedMovementStarter startRangedMovement)
    {
        prepareGuard(engagement.LeadGuard, "Restore defensive position");
        if (engagement.ReserveGuard != null)
        {
            prepareGuard(engagement.ReserveGuard, "Restore reserve position");
        }
        if (engagement.RangedGuard != null)
        {
            prepareGuard(engagement.RangedGuard, "Restore ranged position");
        }
        if (engagement.SecondaryRangedGuard != null)
        {
            prepareGuard(engagement.SecondaryRangedGuard, "Restore secondary ranged position");
        }

        if (!engagement.LeadArrived)
        {
            startGuardMovement(
                grid,
                engagement,
                engagement.LeadGuard,
                engagement.GuardCell,
                false,
                null);
        }
        if (engagement.ReserveGuard != null && !engagement.ReserveArrived)
        {
            startGuardMovement(
                grid,
                engagement,
                engagement.ReserveGuard,
                engagement.ReserveCell,
                true,
                null);
        }
        if (engagement.RangedGuard != null && !engagement.RangedArrived)
        {
            startRangedMovement(
                grid,
                engagement,
                engagement.RangedGuard,
                engagement.RangedCell,
                null,
                false);
        }
        if (engagement.SecondaryRangedGuard != null
            && !engagement.SecondaryRangedArrived)
        {
            startRangedMovement(
                grid,
                engagement,
                engagement.SecondaryRangedGuard,
                engagement.SecondaryRangedCell,
                null,
                true);
        }
    }

    private static void RestorePresentation(DefenseEngagement engagement)
    {
        if (engagement.State == DefenseEngagementState.Engaged
            && engagement.LeadArrived
            && engagement.IntruderActor.GetNowXY() == engagement.IntruderStopCell)
        {
            DefenseCombatPresentation.Ensure(engagement.LeadGuard)?.SetEngaged(true);
            DefenseCombatPresentation.Ensure(engagement.IntruderActor)?.SetEngaged(true);
            engagement.Intruder.SetEngagementState(true, engagement.IntruderStopCell);
            return;
        }

        engagement.State = DefenseEngagementState.InterceptPlanned;
        engagement.Intruder.SetEngagementState(false);
    }

    private CharacterActor FindCharacter(string persistentId)
    {
        return string.IsNullOrWhiteSpace(persistentId)
            ? null
            : CharacterActorCollection.DistinctByGameObject(world.Characters.Characters)
                .FirstOrDefault(actor => actor != null
                    && !actor.IsDead
                    && string.Equals(
                        GetPersistentId(actor),
                        persistentId,
                        StringComparison.Ordinal));
    }

    private static bool IsValidFront(
        Grid grid,
        Vector2Int stopCell,
        Vector2Int guardCell)
    {
        return grid.IsValidGridPos(stopCell)
            && grid.IsValidGridPos(guardCell)
            && grid.GetGridCell(stopCell)?.AreaType == GridCellAreaType.DungeonInterior
            && grid.GetGridCell(guardCell)?.AreaType == GridCellAreaType.DungeonInterior
            && stopCell != guardCell
            && Mathf.Abs(stopCell.x - guardCell.x) + Mathf.Abs(stopCell.y - guardCell.y) == 1;
    }

    private static string GetPersistentId(CharacterActor actor)
    {
        return actor?.Identity?.PersistentId ?? string.Empty;
    }
}
