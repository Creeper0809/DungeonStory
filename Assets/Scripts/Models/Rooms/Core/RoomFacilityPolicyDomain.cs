using System;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

namespace DungeonStory.Rooms
{
    public enum RoomFacilityRejection
    {
        None,
        MissingRoom,
        UnusableRoom,
        RoleMismatch
    }

    public readonly struct RoomFacilityPolicyInput
    {
        public RoomFacilityPolicyInput(
            bool hasFacility,
            bool requiresRoom,
            FacilityRole facilityRoles,
            FacilityRole requestedRole,
            RoomPolicyRoomSnapshot room,
            int baseCapacity,
            int seatCapacity,
            int tableCapacity,
            int serviceCapacity)
        {
            HasFacility = hasFacility;
            RequiresRoom = requiresRoom;
            FacilityRoles = facilityRoles;
            RequestedRole = requestedRole;
            Room = room;
            BaseCapacity = Math.Max(0, baseCapacity);
            SeatCapacity = Math.Max(0, seatCapacity);
            TableCapacity = Math.Max(0, tableCapacity);
            ServiceCapacity = Math.Max(0, serviceCapacity);
        }

        public bool HasFacility { get; }
        public bool RequiresRoom { get; }
        public FacilityRole FacilityRoles { get; }
        public FacilityRole RequestedRole { get; }
        public RoomPolicyRoomSnapshot Room { get; }
        public int BaseCapacity { get; }
        public int SeatCapacity { get; }
        public int TableCapacity { get; }
        public int ServiceCapacity { get; }
    }

    public sealed class RoomPolicyRoomSnapshot
    {
        public RoomPolicyRoomSnapshot(
            bool isUsable,
            bool isSelfContained,
            FacilityRole roles,
            float qualityScore)
        {
            IsUsable = isUsable;
            IsSelfContained = isSelfContained;
            Roles = roles;
            QualityScore = Mathf.Clamp01(qualityScore);
        }

        public bool IsUsable { get; }
        public bool IsSelfContained { get; }
        public FacilityRole Roles { get; }
        public float QualityScore { get; }
        public bool Supports(FacilityRole role) =>
            role != FacilityRole.None && IsUsable && (Roles & role) != 0;
    }

    [MovedFrom(true, sourceNamespace: "", sourceAssembly: "Assembly-CSharp", sourceClassName: "RoomFacilityPolicyService")]
    public static class RoomFacilityPolicyRules
    {
        public static RoomFacilityRejection GetRejection(RoomFacilityPolicyInput input)
        {
            if (!input.HasFacility || !input.RequiresRoom)
            {
                return RoomFacilityRejection.None;
            }

            if (input.Room == null)
            {
                return RoomFacilityRejection.MissingRoom;
            }

            if (!input.Room.IsUsable)
            {
                return RoomFacilityRejection.UnusableRoom;
            }

            FacilityRole relevant = input.RequestedRole & input.FacilityRoles;
            if (relevant == FacilityRole.None)
            {
                relevant = input.RequestedRole;
            }

            return (input.Room.Roles & relevant) == 0
                ? RoomFacilityRejection.RoleMismatch
                : RoomFacilityRejection.None;
        }

        public static float GetUtilityScore(RoomFacilityPolicyInput input)
        {
            if (!input.HasFacility || !input.RequiresRoom || input.Room == null)
            {
                return 0.5f;
            }

            return input.Room.Supports(input.RequestedRole)
                ? input.Room.QualityScore
                : 0f;
        }

        public static int GetEffectiveCapacity(RoomFacilityPolicyInput input)
        {
            if (!input.HasFacility
                || input.BaseCapacity == 0
                || !input.RequiresRoom
                || input.Room == null
                || input.Room.IsSelfContained
                || !input.Room.IsUsable)
            {
                return input.BaseCapacity;
            }

            if ((input.FacilityRoles & FacilityRole.Meal) != 0)
            {
                return Math.Max(input.BaseCapacity, Math.Min(input.SeatCapacity, input.TableCapacity));
            }

            if ((input.FacilityRoles & FacilityRole.Purchase) != 0)
            {
                return Math.Max(input.BaseCapacity, input.BaseCapacity + input.ServiceCapacity);
            }

            return input.BaseCapacity;
        }
    }
}
