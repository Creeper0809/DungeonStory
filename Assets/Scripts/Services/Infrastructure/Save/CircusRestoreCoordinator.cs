using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

internal sealed class CircusRestoreCoordinator : ICircusRestoreLifecycle
{
    private const string RestoreParticipantId = "500.world.circus";

    private readonly CircusProgramRegistry programs;
    private readonly ICaptivityRestoreCandidateSource captivityCandidates;
    private readonly IWildlifeCaptureRuntime wildlifeCapture;
    private readonly ICharacterAiWorldRegistry world;
    private readonly IGridSystemProvider gridProvider;
    private readonly IRoomLayoutCache rooms;
    private readonly DungeonRuntimeAggregateRootStore aggregateRootStore;
    private readonly CircusStateSession stateSession;
    private readonly CircusRestoreTransactionState restoreTransaction = new();
    private CircusRestorePublication activePublication;

    internal CircusRestoreCoordinator(
        CircusProgramContext program,
        CircusWorldContext worldContext,
        CircusRestoreStateContext state)
    {
        program = program ?? throw new ArgumentNullException(nameof(program));
        worldContext = worldContext
            ?? throw new ArgumentNullException(nameof(worldContext));
        state = state ?? throw new ArgumentNullException(nameof(state));
        programs = program.Programs;
        wildlifeCapture = program.WildlifeCapture;
        world = worldContext.World;
        gridProvider = worldContext.GridProvider;
        rooms = worldContext.Rooms;
        aggregateRootStore = state.AggregateRootStore;
        stateSession = state.StateSession;
        captivityCandidates = program.Captivity
            as ICaptivityRestoreCandidateSource
            ?? throw new InvalidOperationException(
                "Circus restore requires the detached captivity candidate source.");
    }

    public string ParticipantId => RestoreParticipantId;

    internal void ValidateRestore(
        CircusSaveData saveData,
        DungeonGameRestoreReport report)
    {
        TryBuildRestore(saveData, report, out _);
    }

    public CircusRestoreCandidate BuildRestore(CircusSaveData saveData)
    {
        DungeonGameRestoreReport report = new();
        if (!TryBuildRestore(saveData, report, out CircusRestoreCandidate candidate))
        {
            throw new InvalidOperationException(
                "Circus restore candidate is invalid: "
                + string.Join(" | ", report.Errors));
        }
        return candidate;
    }

    internal void Restore(
        CircusSaveData saveData,
        DungeonGameRestoreReport report)
    {
        if (!TryBuildRestore(
                saveData,
                report,
                out CircusRestoreCandidate candidate))
        {
            return;
        }
        try
        {
            StageRestore(candidate);
        }
        catch (Exception exception)
        {
            report.AddError(exception.Message);
        }
    }

    public void StageRestore(CircusRestoreCandidate candidate)
    {
        if (candidate == null)
        {
            throw new ArgumentNullException(nameof(candidate));
        }
        restoreTransaction.EnsureCanStage(
            aggregateRootStore.IsRestoreStaging);

        stateSession.Stage(candidate);
        wildlifeCapture.StageRestore(candidate);
        restoreTransaction.MarkPrepared();
    }

    private bool TryBuildRestore(
        CircusSaveData saveData,
        DungeonGameRestoreReport report,
        out CircusRestoreCandidate candidate)
    {
        if (report == null)
        {
            throw new ArgumentNullException(nameof(report));
        }
        candidate = null;
        CircusSaveValidation.Validate(saveData, programs, report);
        if (!report.Success)
        {
            return false;
        }

        CircusRestoreCandidate restored = CircusRestoreCandidate.Create(saveData);
        if (!captivityCandidates.TryTakePreparedRestoreCandidate(
                out CaptivityRestoreCandidate captivityCandidate))
        {
            report.AddError(
                "Circus restore requires the captivity section candidate to be prepared first.");
            return false;
        }
        ValidateWorldReferences(
            restored.Orders,
            restored.CapturedWildlifeStates,
            captivityCandidate,
            report);
        wildlifeCapture.ValidateRestore(saveData, report);
        if (!report.Success)
        {
            return false;
        }

        candidate = restored;
        return true;
    }

    public void BeginRestoreCandidate()
    {
        if (activePublication != null)
        {
            throw new InvalidOperationException(
                "A circus restore publication is already active.");
        }
        restoreTransaction.Begin();
    }

    public void PublishRestoreCandidate()
    {
        restoreTransaction.EnsureCanPublish();
        if (activePublication != null)
        {
            throw new InvalidOperationException(
                "A circus restore candidate was already published.");
        }

        CircusRestorePublication publication = new();
        activePublication = publication;
        publication.CircusProjection =
            stateSession.BeginProjectionPublication();
        publication.WildlifeProjection =
            wildlifeCapture.BeginRestoreProjectionPublication();
    }

    public void RollbackPublishedRestoreCandidate()
    {
        if (activePublication == null)
        {
            restoreTransaction.Discard();
            return;
        }

        CircusRestorePublication publication = activePublication;
        List<Exception> failures = new();
        void Attempt(Action rollback)
        {
            try
            {
                rollback();
            }
            catch (Exception exception)
            {
                failures.Add(exception);
            }
        }

        if (publication.WildlifeProjection != null)
        {
            Attempt(() => wildlifeCapture.RollbackRestoreProjection(
                publication.WildlifeProjection));
        }
        if (publication.CircusProjection != null)
        {
            Attempt(() => stateSession.RollbackProjection(
                publication.CircusProjection));
        }

        activePublication = null;
        restoreTransaction.Discard();
        if (failures.Count > 0)
        {
            throw new AggregateException(
                "Circus publication rollback encountered one or more failures after attempting every reversal.",
                failures);
        }
    }

    public void CompleteRestoreCandidate()
    {
        if (activePublication == null)
        {
            throw new InvalidOperationException(
                "No published circus restore candidate is ready to complete.");
        }

        CircusRestorePublication publication = activePublication;
        try
        {
            if (publication.CircusProjection != null)
            {
                stateSession.CompleteProjection(
                    publication.CircusProjection);
            }
        }
        catch
        {
            // Aggregate completion cannot be invalidated by projection retirement.
        }
        try
        {
            if (publication.WildlifeProjection != null)
            {
                wildlifeCapture.CompleteRestoreProjection(
                    publication.WildlifeProjection);
            }
        }
        catch
        {
            // Unity projection finalization is best effort after aggregate commit.
        }

        activePublication = null;
        try
        {
            restoreTransaction.CompletePublish();
        }
        catch
        {
            restoreTransaction.Discard();
        }
    }

    public void DiscardRestoreCandidate()
    {
        if (activePublication != null)
        {
            RollbackPublishedRestoreCandidate();
            return;
        }
        restoreTransaction.Discard();
    }

    private void ValidateWorldReferences(
        IReadOnlyList<CircusShowOrder> circusOrders,
        IReadOnlyList<CapturedWildlifeState> wildlifeStates,
        CaptivityRestoreCandidate captivityCandidate,
        DungeonGameRestoreReport report)
    {
        if (!gridProvider.TryGetGrid(out Grid grid) || grid == null)
        {
            report.AddError("Circus restore requires an active grid.");
            return;
        }

        Dictionary<string, CharacterActor> characters = world.Characters
            .Where(actor => CharacterPersistentIdentity.TryGet(actor, out _))
            .GroupBy(
                actor => CharacterPersistentIdentity.Require(actor).Value,
                StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.First(),
                StringComparer.Ordinal);
        Dictionary<string, BuildableObject> buildings = world.Buildings
            .Where(building => building != null
                && !building.isDestroy
                && building.PersistentInstanceId.IsValid)
            .GroupBy(
                building => building.PersistentInstanceId.Value,
                StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.First(),
                StringComparer.Ordinal);
        HashSet<string> activeStages = new(StringComparer.Ordinal);
        HashSet<string> activePerformers = new(StringComparer.Ordinal);
        HashSet<string> activeWildlife = new(StringComparer.Ordinal);

        IReadOnlyDictionary<string, CapturedWildlifeState> wildlifeById =
            wildlifeStates
                .Where(item => item != null)
                .ToDictionary(item => item.wildlifeId, StringComparer.Ordinal);
        foreach (CircusShowOrder order in circusOrders.Where(
                     order => order != null && !order.IsTerminal))
        {
            ValidateActiveOrder(
                order,
                wildlifeById,
                captivityCandidate,
                grid,
                characters,
                buildings,
                activeStages,
                activePerformers,
                activeWildlife,
                report);
        }
    }

    private void ValidateActiveOrder(
        CircusShowOrder order,
        IReadOnlyDictionary<string, CapturedWildlifeState> wildlifeById,
        CaptivityRestoreCandidate captivityCandidate,
        Grid grid,
        IReadOnlyDictionary<string, CharacterActor> characters,
        IReadOnlyDictionary<string, BuildableObject> buildings,
        ISet<string> activeStages,
        ISet<string> activePerformers,
        ISet<string> activeWildlife,
        DungeonGameRestoreReport report)
    {
        if (!buildings.TryGetValue(order.stageId, out BuildableObject stage)
            || stage.BuildingData.GetCircusStageAbility() is not { IsValid: true }
            || stage.centerPos != order.stagePosition)
        {
            report.AddError(
                $"Active circus order '{order.orderId}' references invalid stage '{order.stageId}'.");
            return;
        }
        if (!activeStages.Add(order.stageId))
        {
            report.AddError(
                $"Circus stage '{order.stageId}' has multiple active orders.");
        }
        if (!rooms.TryGetRoom(stage, out RoomInstance room)
            || room == null
            || !room.IsUsable
            || room.Id != order.roomId)
        {
            report.AddError(
                $"Active circus order '{order.orderId}' has an invalid saved room.");
            return;
        }

        ValidatePositions(order, "performer", order.performerPositions, room, grid, report);
        ValidatePositions(order, "wildlife", order.wildlifePositions, room, grid, report);
        ValidatePositions(order, "audience", order.audiencePositions, room, grid, report);

        List<CaptiveState> performers = new();
        foreach (string performerId in order.performerIds)
        {
            if (!activePerformers.Add(performerId))
            {
                report.AddError(
                    $"Captive '{performerId}' is assigned to multiple active circus orders.");
            }
            if (!captivityCandidate.TryGetCaptive(
                    performerId,
                    out CaptiveState captive)
                || captive == null
                || !captive.IsInCustody
                || !characters.TryGetValue(performerId, out CharacterActor actor)
                || actor == null
                || actor.IsDead)
            {
                report.AddError(
                    $"Active circus order '{order.orderId}' references unavailable performer '{performerId}'.");
                continue;
            }
            performers.Add(captive);
        }

        BuildingCircusStageAbility stageAbility =
            stage.BuildingData.GetCircusStageAbility();
        if (order.performerIds.Count > stageAbility.performerCapacity)
        {
            report.AddError(
                $"Active circus order '{order.orderId}' exceeds stage performer capacity.");
        }
        if (programs.TryGet(order.programId, out ICircusProgramHandler program)
            && !program.Validate(order, performers, out string reason))
        {
            report.AddError(
                $"Active circus order '{order.orderId}' violates program rules: {reason}");
        }

        foreach (string wildlifeId in order.wildlifeIds)
        {
            if (!activeWildlife.Add(wildlifeId))
            {
                report.AddError(
                    $"Wildlife '{wildlifeId}' is assigned to multiple active circus orders.");
            }
            if (!wildlifeById.TryGetValue(
                    wildlifeId,
                    out CapturedWildlifeState captured)
                || captured.escaped)
            {
                report.AddError(
                    $"Active circus order '{order.orderId}' references unavailable wildlife '{wildlifeId}'.");
            }
        }

        foreach (string audienceId in order.audienceIds)
        {
            if (!characters.TryGetValue(audienceId, out CharacterActor actor)
                || actor == null
                || actor.IsDead)
            {
                report.AddError(
                    $"Active circus order '{order.orderId}' references unavailable audience '{audienceId}'.");
            }
        }
    }

    private static void ValidatePositions(
        CircusShowOrder order,
        string group,
        IEnumerable<Vector2Int> positions,
        RoomInstance room,
        Grid grid,
        DungeonGameRestoreReport report)
    {
        foreach (Vector2Int position in positions)
        {
            if (!grid.IsValidGridPos(position) || !room.ContainsCell(position))
            {
                report.AddError(
                    $"Circus order '{order.orderId}' has {group} position {position} outside its room.");
            }
        }
    }

    private sealed class CircusRestorePublication
    {
        internal CircusProjectionPublication CircusProjection { get; set; }
        internal WildlifeCaptureProjectionPublication WildlifeProjection { get; set; }
    }
}
