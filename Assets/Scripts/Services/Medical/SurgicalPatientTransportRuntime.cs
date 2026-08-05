using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using UnityEngine;
using VContainer.Unity;

public sealed class SurgicalPatientTransportRuntime :
    ISurgicalPatientTransportRuntime,
    ITickable
{
    private sealed class TransportState
    {
        public SurgeryOrder Order;
        public string CarrierId = string.Empty;
        public Vector2Int Destination;
        public bool Returning;
        public Transform OriginalParent;
        public bool Started;
        public bool Carrying;
    }

    private readonly ICharacterWorldQuery characters;
    private readonly IWildlifeWorldQuery wildlife;
    private readonly IWildlifeCaptureRuntime capture;
    private readonly IDoorAccessCommandService doorAccess;
    private readonly IGameClock clock;
    private readonly Dictionary<string, TransportState> active =
        new(StringComparer.Ordinal);
    private readonly List<string> pendingReturns = new();

    public SurgicalPatientTransportRuntime(
        ICharacterWorldQuery characters,
        IWildlifeWorldQuery wildlife,
        IWildlifeCaptureRuntime capture,
        IDoorAccessCommandService doorAccess,
        IGameClock clock)
    {
        this.characters = characters
            ?? throw new ArgumentNullException(nameof(characters));
        this.wildlife = wildlife
            ?? throw new ArgumentNullException(nameof(wildlife));
        this.capture = capture
            ?? throw new ArgumentNullException(nameof(capture));
        this.doorAccess = doorAccess
            ?? throw new ArgumentNullException(nameof(doorAccess));
        this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    public void Tick()
    {
        if (clock.IsPaused || pendingReturns.Count == 0)
        {
            return;
        }

        for (int index = pendingReturns.Count - 1; index >= 0; index--)
        {
            string orderId = pendingReturns[index];
            if (!active.TryGetValue(orderId, out TransportState state)
                || state == null
                || state.Started)
            {
                pendingReturns.RemoveAt(index);
                continue;
            }

            if (TryStart(state, out _))
            {
                pendingReturns.RemoveAt(index);
            }
        }
    }

    public bool EnsureWildlifeAdmission(
        SurgeryOrder order,
        WildlifeActor patient,
        Vector2Int destination,
        out SurgeryStatusData status)
    {
        status = new SurgeryStatusData();
        if (order == null || patient == null || !patient.IsAlive)
        {
            status.Set(
                SurgeryStatusCode.WildlifePatientMissing,
                patient?.WildlifeId ?? string.Empty);
            return false;
        }

        if (Manhattan(patient.GridPosition, destination) <= 1)
        {
            order.patientAdmitted = true;
            order.patientTransportInProgress = false;
            order.patientTransporterId = string.Empty;
            status.Set(
                SurgeryStatusCode.WildlifePatientReady,
                patient.WildlifeId);
            return true;
        }

        if (!order.subject.willing && !capture.IsCaptured(patient.WildlifeId))
        {
            status.Set(
                SurgeryStatusCode.WildlifeRestraintRequired,
                patient.WildlifeId);
            return false;
        }

        if (active.TryGetValue(order.orderId, out TransportState current))
        {
            status.Set(
                current.Returning
                    ? SurgeryStatusCode.WildlifePatientReturning
                    : SurgeryStatusCode.WildlifePatientTransporting,
                patient.WildlifeId);
            return false;
        }

        Vector2Int origin = patient.GridPosition;
        if (capture.TryGetCaptured(patient.WildlifeId, out CapturedWildlifeState captive))
        {
            origin = captive.penPosition;
        }

        order.patientOriginX = origin.x;
        order.patientOriginY = origin.y;
        order.admissionX = destination.x;
        order.admissionY = destination.y;
        TransportState state = new TransportState
        {
            Order = order,
            Destination = destination,
            Returning = false
        };
        active.Add(order.orderId, state);
        if (!TryStart(state, out DomainFailure failure))
        {
            active.Remove(order.orderId);
            order.patientTransportInProgress = false;
            order.patientTransporterId = string.Empty;
            status.Set(
                SurgeryStatusCode.WildlifePatientTransporting,
                patient.WildlifeId,
                failure.Code.ToString());
            return false;
        }

        status.Set(
            SurgeryStatusCode.WildlifePatientTransporting,
            patient.WildlifeId);
        return false;
    }

    public void RequestWildlifeReturn(SurgeryOrder order)
    {
        if (order?.subject?.kind != SurgicalSubjectKind.Wildlife)
        {
            return;
        }

        order.patientReturnRequested = true;
        if (active.TryGetValue(order.orderId, out TransportState current))
        {
            if (!current.Returning)
            {
                current.Order.patientReturnRequested = true;
            }

            return;
        }

        WildlifeActor patient = FindWildlife(order.subject.subjectId);
        if (patient == null || !patient.IsAlive)
        {
            order.patientReturnRequested = false;
            return;
        }

        TransportState state = new TransportState
        {
            Order = order,
            Destination = new Vector2Int(
                order.patientOriginX,
                order.patientOriginY),
            Returning = true
        };
        active[order.orderId] = state;
        if (!TryStart(state, out _)
            && !pendingReturns.Contains(order.orderId))
        {
            pendingReturns.Add(order.orderId);
        }
    }

    public void CancelTransport(SurgeryOrder order)
    {
        if (order == null)
        {
            return;
        }

        if (active.Remove(order.orderId, out TransportState state))
        {
            WildlifeActor patient = FindWildlife(order.subject?.subjectId);
            if (patient != null && state.Carrying)
            {
                CharacterActor carrier = FindCharacter(state.CarrierId);
                patient.EndManagedCarry(
                    carrier != null ? carrier.GetNowXY() : patient.GridPosition,
                    state.OriginalParent);
                state.Carrying = false;
            }
        }

        pendingReturns.Remove(order.orderId);
        order.patientTransportInProgress = false;
        order.patientTransporterId = string.Empty;
        order.statusData.Set(
            SurgeryStatusCode.ProcedurePaused,
            order.orderId);
    }

    public bool TryGetTransport(
        string orderId,
        CharacterActor carrier,
        out WildlifeActor patient,
        out Vector2Int destination,
        out bool returning,
        out DomainFailure failure)
    {
        patient = null;
        destination = default;
        returning = false;
        failure = DomainFailure.None;
        if (!active.TryGetValue(orderId?.Trim() ?? string.Empty, out TransportState state)
            || state?.Order == null)
        {
            failure = new DomainFailure(
                FailureCode.SurgeryTransportOrderMissing,
                orderId ?? string.Empty);
            return false;
        }

        string carrierId = GetCharacterId(carrier);
        if (!string.Equals(state.CarrierId, carrierId, StringComparison.Ordinal))
        {
            failure = new DomainFailure(
                FailureCode.SurgeryTransportCarrierMismatch,
                state.CarrierId,
                carrierId);
            return false;
        }

        patient = FindWildlife(state.Order.subject?.subjectId);
        if (patient == null || !patient.IsAlive)
        {
            failure = new DomainFailure(
                FailureCode.SurgeryWildlifeSubjectUnavailable,
                state.Order.subject?.subjectId ?? string.Empty);
            return false;
        }

        destination = state.Destination;
        returning = state.Returning;
        return true;
    }

    public IDisposable BeginTransportPass(
        CharacterActor carrier,
        string orderId)
    {
        DoorAccessSubjectRef subject = new DoorAccessSubjectRef(
            GetCharacterId(carrier),
            carrier != null && carrier.IsOwner
                ? DoorAccessGroup.Owner
                : DoorAccessGroup.Staff,
            character: carrier);
        return doorAccess.BeginTemporaryOverride(
            subject,
            DoorAccessOverrideKind.EscortPass,
            $"surgery-patient:{orderId?.Trim() ?? string.Empty}");
    }

    public bool TryBeginCarry(
        string orderId,
        CharacterActor carrier,
        out DomainFailure failure)
    {
        if (!TryGetTransport(
                orderId,
                carrier,
                out WildlifeActor patient,
                out _,
                out _,
                out failure))
        {
            return false;
        }

        if (Manhattan(carrier.GetNowXY(), patient.GridPosition) > 1)
        {
            failure = new DomainFailure(
                FailureCode.SurgeryTransportUnavailable,
                orderId ?? string.Empty);
            return false;
        }

        TransportState state = active[orderId];
        state.OriginalParent = patient.transform.parent;
        patient.BeginManagedCarry(carrier.transform);
        state.Carrying = true;
        return true;
    }

    public bool TryCompleteCarry(
        string orderId,
        CharacterActor carrier,
        out DomainFailure failure)
    {
        if (!TryGetTransport(
                orderId,
                carrier,
                out WildlifeActor patient,
                out Vector2Int destination,
                out bool returning,
                out failure))
        {
            return false;
        }

        if (carrier.GetNowXY() != destination)
        {
            failure = new DomainFailure(
                FailureCode.SurgeryTransportUnavailable,
                orderId ?? string.Empty);
            return false;
        }

        TransportState state = active[orderId];
        patient.EndManagedCarry(destination, state.OriginalParent);
        state.Carrying = false;
        SurgeryOrder order = state.Order;
        active.Remove(orderId);
        order.patientTransportInProgress = false;
        order.patientTransporterId = string.Empty;
        if (returning)
        {
            order.patientAdmitted = false;
            order.patientReturnRequested = false;
            order.statusData.Set(
                SurgeryStatusCode.WildlifePatientReturnCompleted,
                order.subject.subjectId);
        }
        else
        {
            order.patientAdmitted = true;
            order.statusData.Set(
                SurgeryStatusCode.WildlifePatientReady,
                order.subject.subjectId);
            if (order.patientReturnRequested)
            {
                RequestWildlifeReturn(order);
            }
        }

        return true;
    }

    public void FailCarry(
        string orderId,
        CharacterActor carrier)
    {
        if (!active.Remove(orderId?.Trim() ?? string.Empty, out TransportState state)
            || state?.Order == null)
        {
            return;
        }

        WildlifeActor patient = FindWildlife(state.Order.subject?.subjectId);
        if (patient != null && state.Carrying)
        {
            patient.EndManagedCarry(
                carrier != null ? carrier.GetNowXY() : patient.GridPosition,
                state.OriginalParent);
            state.Carrying = false;
        }

        state.Order.patientTransportInProgress = false;
        state.Order.patientTransporterId = string.Empty;
        state.Order.statusData.Set(
            SurgeryStatusCode.ProcedurePaused,
            state.Order.orderId);
        if (state.Returning && !pendingReturns.Contains(state.Order.orderId))
        {
            state.Started = false;
            state.Carrying = false;
            active[state.Order.orderId] = state;
            pendingReturns.Add(state.Order.orderId);
        }
    }

    private bool TryStart(TransportState state, out DomainFailure failure)
    {
        failure = DomainFailure.None;
        WildlifeActor patient = FindWildlife(state.Order.subject?.subjectId);
        CharacterActor carrier = characters.Characters
            .Where(candidate =>
                candidate != null
                && !candidate.IsDead
                && candidate.characterType == CharacterType.NPC
                && candidate.CurrentLifecycleState == CharacterLifecycleState.Active
                && candidate.TryGetAbility(out AbilityMove _))
            .Where(candidate =>
            {
                AbilitySurgicalWildlifeTransport existing =
                    candidate.GetComponent<AbilitySurgicalWildlifeTransport>();
                return existing == null || !existing.IsTransporting;
            })
            .OrderBy(candidate => patient != null
                ? Manhattan(candidate.GetNowXY(), patient.GridPosition)
                : int.MaxValue)
            .FirstOrDefault();
        if (patient == null || !patient.IsAlive)
        {
            failure = new DomainFailure(
                FailureCode.SurgeryWildlifeSubjectUnavailable,
                state.Order.subject?.subjectId ?? string.Empty);
            return false;
        }

        if (carrier == null)
        {
            failure = new DomainFailure(
                FailureCode.SurgeryTransportUnavailable,
                state.Order.orderId);
            return false;
        }

        AbilitySurgicalWildlifeTransport ability =
            AbilitySurgicalWildlifeTransport.Ensure(carrier);
        if (ability == null)
        {
            failure = new DomainFailure(
                FailureCode.SurgeryTransportUnavailable,
                state.Order.orderId);
            return false;
        }

        carrier.Brain?.StopCurrentActionForReplan(
            SurgeryStatusCode.WildlifePatientTransporting.ToString());
        state.CarrierId = GetCharacterId(carrier);
        state.Started = true;
        state.Order.patientTransporterId = state.CarrierId;
        state.Order.patientTransportInProgress = true;
        ability.Configure(this);
        ability.StartTransport(state.Order.orderId);
        return true;
    }

    private CharacterActor FindCharacter(string persistentId)
    {
        return characters.Characters.FirstOrDefault(actor =>
            actor != null
            && string.Equals(
                GetCharacterId(actor),
                persistentId,
                StringComparison.Ordinal));
    }

    private WildlifeActor FindWildlife(string wildlifeId)
    {
        return wildlife.Wildlife.FirstOrDefault(actor =>
            actor != null
            && string.Equals(
                actor.WildlifeId,
                wildlifeId,
                StringComparison.Ordinal));
    }

    private static string GetCharacterId(CharacterActor actor)
    {
        return actor?.Identity?.PersistentId?.Trim() ?? string.Empty;
    }

    private static int Manhattan(Vector2Int left, Vector2Int right)
    {
        return Mathf.Abs(left.x - right.x) + Mathf.Abs(left.y - right.y);
    }
}
