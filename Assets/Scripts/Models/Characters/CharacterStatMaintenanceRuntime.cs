using System;
using System.Collections.Generic;
using DungeonStory.Foundation;
using UnityEngine.Scripting.APIUpdating;

namespace DungeonStory.Characters
{
    public interface ICharacterStatMaintenancePort
    {
        int CharacterVersion { get; }
        IReadOnlyList<CharacterId> CaptureCharacterIds();
        void RunScheduledMaintenance(CharacterId characterId, float now);
    }

    [MovedFrom(
        true,
        sourceAssembly: "Assembly-CSharp",
        sourceNamespace: "",
        sourceClassName: "CharacterStatMaintenanceRuntime")]
    public sealed class CharacterStatMaintenanceRuntime
    {
        private readonly ICharacterStatMaintenancePort characters;
        private readonly IGameClock gameClock;
        private readonly IDynamicFrameWorkBudget frameWorkBudget;
        private readonly List<CharacterId> characterIds = new();
        private int characterIndex;
        private int capturedCharacterVersion = -1;

        public CharacterStatMaintenanceRuntime(
            ICharacterStatMaintenancePort characters,
            IGameClock gameClock,
            IDynamicFrameWorkBudget frameWorkBudget)
        {
            this.characters = characters
                ?? throw new ArgumentNullException(nameof(characters));
            this.gameClock = gameClock
                ?? throw new ArgumentNullException(nameof(gameClock));
            this.frameWorkBudget = frameWorkBudget
                ?? throw new ArgumentNullException(nameof(frameWorkBudget));
        }

        public void Tick()
        {
            if (gameClock.DeltaTime <= 0f)
            {
                return;
            }

            RefreshSnapshotWhenNeeded();
            if (characterIds.Count == 0)
            {
                frameWorkBudget.SetBacklog(
                    DynamicFrameWorkDomain.CharacterNeeds,
                    0);
                return;
            }

            if (characterIndex >= characterIds.Count)
            {
                characterIndex = 0;
            }

            int backlog = characterIds.Count - characterIndex;
            frameWorkBudget.SetBacklog(
                DynamicFrameWorkDomain.CharacterNeeds,
                backlog);
            double sliceMilliseconds = frameWorkBudget.GetSliceMilliseconds(
                DynamicFrameWorkDomain.CharacterNeeds,
                0.04,
                0.45);
            long started = System.Diagnostics.Stopwatch.GetTimestamp();
            int processed = 0;
            float now = gameClock.Time;
            while (characterIndex < characterIds.Count)
            {
                CharacterId characterId = characterIds[characterIndex++];
                processed++;
                if (characterId.IsValid)
                {
                    characters.RunScheduledMaintenance(characterId, now);
                }

                if (processed >= 8
                    && ElapsedMilliseconds(started) >= sliceMilliseconds)
                {
                    break;
                }
            }

            frameWorkBudget.ReportConsumed(
                DynamicFrameWorkDomain.CharacterNeeds,
                ElapsedMilliseconds(started));
            if (characterIndex >= characterIds.Count)
            {
                characterIndex = 0;
                frameWorkBudget.SetBacklog(
                    DynamicFrameWorkDomain.CharacterNeeds,
                    0);
            }
        }

        private void RefreshSnapshotWhenNeeded()
        {
            if (capturedCharacterVersion == characters.CharacterVersion
                && characterIds.Count > 0)
            {
                return;
            }

            characterIds.Clear();
            IReadOnlyList<CharacterId> current =
                characters.CaptureCharacterIds()
                ?? Array.Empty<CharacterId>();
            for (int i = 0; i < current.Count; i++)
            {
                characterIds.Add(current[i]);
            }

            capturedCharacterVersion = characters.CharacterVersion;
            characterIndex = 0;
        }

        private static double ElapsedMilliseconds(long started)
        {
            return (System.Diagnostics.Stopwatch.GetTimestamp() - started)
                * 1000.0
                / System.Diagnostics.Stopwatch.Frequency;
        }
    }
}
