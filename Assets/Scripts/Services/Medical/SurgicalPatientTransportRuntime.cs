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
        out string status)
    {
        status = string.Empty;
        if (order == null || patient == null || !patient.IsAlive)
        {
            status = "살아 있는 동물 환자를 찾을 수 없습니다.";
            return false;
        }

        if (Manhattan(patient.GridPosition, destination) <= 1)
        {
            order.patientAdmitted = true;
            order.patientTransportInProgress = false;
            order.patientTransporterId = string.Empty;
            status = "동물 환자 입실 완료";
            return true;
        }

        if (!order.subject.willing && !capture.IsCaptured(patient.WildlifeId))
        {
            status = "비동의 동물은 먼저 제압하고 포획해야 합니다.";
            return false;
        }

        if (active.TryGetValue(order.orderId, out TransportState current))
        {
            status = current.Returning
                ? "동물 환자를 우리로 돌려보내는 중"
                : current.Started
                    ? "직원이 동물 환자를 수술실로 운반 중"
                    : "동물 환자 운반자를 기다리는 중";
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
        if (!TryStart(state, out status))
        {
            active.Remove(order.orderId);
            order.patientTransportInProgress = false;
            order.patientTransporterId = string.Empty;
            return false;
        }

        status = "직원이 동물 환자를 수술실로 운반 중";
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

    public void CancelTransport(SurgeryOrder order, string reason)
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
        order.status = string.IsNullOrWhiteSpace(reason)
            ? order.status
            : reason;
    }

    public bool TryGetTransport(
        string orderId,
        CharacterActor carrier,
        out WildlifeActor patient,
        out Vector2Int destination,
        out bool returning,
        out string failureReason)
    {
        patient = null;
        destination = default;
        returning = false;
        failureReason = string.Empty;
        if (!active.TryGetValue(orderId?.Trim() ?? string.Empty, out TransportState state)
            || state?.Order == null)
        {
            failureReason = "동물 환자 운반 주문이 없습니다.";
            return false;
        }

        string carrierId = GetCharacterId(carrier);
        if (!string.Equals(state.CarrierId, carrierId, StringComparison.Ordinal))
        {
            failureReason = "예약된 동물 환자 운반자가 아닙니다.";
            return false;
        }

        patient = FindWildlife(state.Order.subject?.subjectId);
        if (patient == null || !patient.IsAlive)
        {
            failureReason = "운반할 동물 환자를 찾을 수 없습니다.";
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
        out string failureReason)
    {
        if (!TryGetTransport(
                orderId,
                carrier,
                out WildlifeActor patient,
                out _,
                out _,
                out failureReason))
        {
            return false;
        }

        if (Manhattan(carrier.GetNowXY(), patient.GridPosition) > 1)
        {
            failureReason = "동물 환자와 너무 멀리 떨어져 있습니다.";
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
        out string failureReason)
    {
        if (!TryGetTransport(
                orderId,
                carrier,
                out WildlifeActor patient,
                out Vector2Int destination,
                out bool returning,
                out failureReason))
        {
            return false;
        }

        if (carrier.GetNowXY() != destination)
        {
            failureReason = "동물 환자 운반 목적지에 도착하지 못했습니다.";
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
            order.status = "동물 환자 우리 복귀 완료";
        }
        else
        {
            order.patientAdmitted = true;
            order.status = "동물 환자 입실 완료";
            if (order.patientReturnRequested)
            {
                RequestWildlifeReturn(order);
            }
        }

        return true;
    }

    public void FailCarry(
        string orderId,
        CharacterActor carrier,
        string reason)
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
        state.Order.status = string.IsNullOrWhiteSpace(reason)
            ? "동물 환자 운반 중단"
            : reason;
        if (state.Returning && !pendingReturns.Contains(state.Order.orderId))
        {
            state.Started = false;
            state.Carrying = false;
            active[state.Order.orderId] = state;
            pendingReturns.Add(state.Order.orderId);
        }
    }

    private bool TryStart(TransportState state, out string failureReason)
    {
        failureReason = string.Empty;
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
            failureReason = "운반할 동물 환자를 찾을 수 없습니다.";
            return false;
        }

        if (carrier == null)
        {
            failureReason = "동물 환자를 운반할 직원이 없습니다.";
            return false;
        }

        AbilitySurgicalWildlifeTransport ability =
            AbilitySurgicalWildlifeTransport.Ensure(carrier);
        if (ability == null)
        {
            failureReason = "동물 환자 운반 행동을 시작할 수 없습니다.";
            return false;
        }

        carrier.Brain?.StopCurrentActionForReplan("동물 환자 긴급 이송");
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
