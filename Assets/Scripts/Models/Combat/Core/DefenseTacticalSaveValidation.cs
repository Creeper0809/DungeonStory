using System;
using System.Collections.Generic;

internal static class DefenseTacticalSaveValidation
{
    internal const int MaximumReservations = 256;

    internal static void Validate(
        DefenseTacticalCoordinatorSaveData payload,
        DungeonGameRestoreReport report,
        IDefenseTacticalWorldQuery world)
    {
        if (report == null)
        {
            throw new ArgumentNullException(nameof(report));
        }
        if (payload == null)
        {
            report.AddError("Defense-tactical payload is null.");
            return;
        }
        if (payload.sequence < 0 || payload.reservations == null)
        {
            report.AddError("Defense-tactical payload has a negative sequence or missing reservation list.");
            return;
        }
        if (payload.reservations.Count > MaximumReservations)
        {
            report.AddError($"Defense-tactical payload exceeds {MaximumReservations} reservations.");
        }

        HashSet<string> ids = new(StringComparer.Ordinal);
        HashSet<string> actors = new(StringComparer.Ordinal);
        HashSet<UnityEngine.Vector2Int> cells = new();
        int highestSequence = 0;
        foreach (CombatPositionReservation reservation in payload.reservations)
        {
            string id = reservation?.reservationId ?? string.Empty;
            if (reservation == null
                || !TryParseReservationId(id, out int sequence)
                || !ids.Add(id)
                || !((CharacterId)(reservation.actorId ?? string.Empty)).IsValid
                || !actors.Add(reservation.actorId)
                || reservation.targetId == null
                || !Enum.IsDefined(
                    typeof(CombatPositionReservationKind),
                    reservation.kind)
                || float.IsNaN(reservation.targetScore)
                || float.IsInfinity(reservation.targetScore)
                || !cells.Add(reservation.Cell))
            {
                report.AddError($"Defense-tactical reservation '{id}' is invalid or duplicated.");
                continue;
            }

            highestSequence = Math.Max(highestSequence, sequence);
        }
        if (payload.sequence < highestSequence)
        {
            report.AddError(
                $"Defense-tactical sequence {payload.sequence} is below saved reservation {highestSequence}.");
        }

        ValidateWorld(payload, report, world);
    }

    internal static DefenseTacticalAggregateState CreateState(
        DefenseTacticalCoordinatorSaveData payload)
    {
        DefenseTacticalAggregateState state = new() { Sequence = payload.sequence };
        foreach (CombatPositionReservation reservation in payload.reservations)
        {
            state.ByActor.Add(reservation.actorId, reservation.Clone());
        }

        return state;
    }

    private static void ValidateWorld(
        DefenseTacticalCoordinatorSaveData payload,
        DungeonGameRestoreReport report,
        IDefenseTacticalWorldQuery world)
    {
        if (world == null || !world.HasRestoreGrid)
        {
            report.AddError("Defense-tactical restore requires the detached facility Grid.");
            return;
        }

        Dictionary<string, DefenseTacticalActorSnapshot> characters = new(StringComparer.Ordinal);
        foreach (DefenseTacticalActorSnapshot actor in world.CaptureActors())
        {
            if (!characters.TryAdd(actor.ActorId, actor))
            {
                report.AddError($"Detached character world duplicates defense actor '{actor.ActorId}'.");
            }
        }
        HashSet<string> targets = new(world.CaptureTargetIds(), StringComparer.Ordinal);
        foreach (CombatPositionReservation reservation in payload.reservations)
        {
            if (!characters.TryGetValue(
                    reservation.actorId,
                    out DefenseTacticalActorSnapshot actor)
                || !actor.IsAvailable)
            {
                report.AddError(
                    $"Defense reservation '{reservation.reservationId}' references unavailable actor '{reservation.actorId}'.");
            }
            if (!world.IsRestoreCellWalkable(reservation.Cell))
            {
                report.AddError(
                    $"Defense reservation '{reservation.reservationId}' has invalid cell {reservation.Cell}.");
            }
            if (reservation.targetId.Length > 0
                && !targets.Contains(reservation.targetId))
            {
                report.AddError(
                    $"Defense reservation '{reservation.reservationId}' references missing target '{reservation.targetId}'.");
            }
        }
    }

    private static bool TryParseReservationId(string value, out int sequence)
    {
        const string prefix = "combat-position:";
        sequence = 0;
        return value.StartsWith(prefix, StringComparison.Ordinal)
            && int.TryParse(value.Substring(prefix.Length), out sequence)
            && sequence > 0;
    }
}
