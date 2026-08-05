using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using UnityEngine.Scripting.APIUpdating;

namespace DungeonStory.Work
{
    public readonly struct StaffWorkforceSnapshot
    {
        public StaffWorkforceSnapshot(
            CharacterId characterId,
            bool isDead,
            bool hasWorkRole,
            bool isOwner,
            string displayName)
        {
            CharacterId = characterId;
            IsDead = isDead;
            HasWorkRole = hasWorkRole;
            IsOwner = isOwner;
            DisplayName = displayName ?? string.Empty;
        }

        public CharacterId CharacterId { get; }
        public bool IsDead { get; }
        public bool HasWorkRole { get; }
        public bool IsOwner { get; }
        public string DisplayName { get; }
    }

    public interface IStaffWorkforceSnapshotQuery
    {
        IReadOnlyList<StaffWorkforceSnapshot> CaptureWorkforce();
    }

    [MovedFrom(
        true,
        sourceAssembly: "Assembly-CSharp",
        sourceNamespace: "",
        sourceClassName: "StaffWorkforceRuntimeQueryService")]
    public sealed class StaffWorkforceRuntimeQueryService
    {
        private readonly IStaffWorkforceSnapshotQuery workforce;

        public StaffWorkforceRuntimeQueryService(
            IStaffWorkforceSnapshotQuery workforce)
        {
            this.workforce = workforce
                ?? throw new ArgumentNullException(nameof(workforce));
        }

        public IReadOnlyList<StaffWorkforceSnapshot> FindActiveWorkers()
        {
            return (workforce.CaptureWorkforce()
                    ?? Array.Empty<StaffWorkforceSnapshot>())
                .Where(IsActiveWorker)
                .OrderByDescending(character => character.IsOwner)
                .ThenBy(GetDisplayName)
                .ToList();
        }

        public bool IsActiveWorker(StaffWorkforceSnapshot character)
        {
            return character.CharacterId.IsValid
                && !character.IsDead
                && character.HasWorkRole;
        }

        public string GetDisplayName(StaffWorkforceSnapshot character) =>
            character.DisplayName ?? string.Empty;
    }
}
