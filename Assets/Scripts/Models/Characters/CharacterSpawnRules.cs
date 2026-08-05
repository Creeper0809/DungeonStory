using System;
using System.Collections.Generic;

namespace DungeonStory.Characters
{
    public static class CharacterSpawnRules
    {
        public static bool IsRecruitmentEligible(
            bool hasSpecies,
            bool ownerSelectable,
            string homeFactionId,
            bool recruitmentContractUnlocked)
        {
            if (!hasSpecies || ownerSelectable)
            {
                return true;
            }

            return !string.IsNullOrWhiteSpace(homeFactionId)
                && recruitmentContractUnlocked;
        }
    }

    public sealed class CharacterRespawnSchedule
    {
        private readonly Dictionary<string, CharacterRespawnData> entries =
            new(StringComparer.Ordinal);
        private float elapsedTime;

        public IEnumerable<string> UnavailableProfileIds => entries.Keys;

        public void Advance(float deltaTime)
        {
            elapsedTime += deltaTime;
            string readyId = null;
            foreach (KeyValuePair<string, CharacterRespawnData> pair in entries)
            {
                if (pair.Value.CheckResapwn(elapsedTime))
                {
                    readyId = pair.Key;
                    break;
                }
            }

            if (!string.IsNullOrWhiteSpace(readyId))
            {
                entries.Remove(readyId);
            }
        }

        public void Register(string profileId, int characterDataId, float respawnTime)
        {
            if (string.IsNullOrWhiteSpace(profileId))
            {
                throw new ArgumentException(
                    "A character respawn profile ID is required.",
                    nameof(profileId));
            }

            entries[profileId] = new CharacterRespawnData(
                profileId,
                characterDataId,
                respawnTime);
        }

        public bool MarkDisabled(string profileId)
        {
            if (!entries.TryGetValue(profileId ?? string.Empty, out CharacterRespawnData data))
            {
                return false;
            }

            data.StartCheckRespawn(elapsedTime);
            return true;
        }

        public void Remove(string profileId)
        {
            entries.Remove(profileId ?? string.Empty);
        }
    }

    public sealed class CharacterRespawnData
    {
        public string id;
        public int characterDataId;
        public float lastDisabledTime;
        public float respawnTime;
        public bool isDiabled;

        public CharacterRespawnData(
            string id,
            int characterDataId,
            float respawnTime)
        {
            this.respawnTime = respawnTime;
            this.id = id;
            this.characterDataId = characterDataId;
            isDiabled = false;
        }

        public void StartCheckRespawn(float lastDisabledTime)
        {
            isDiabled = true;
            this.lastDisabledTime = lastDisabledTime;
        }

        public bool CheckResapwn(float time)
        {
            return isDiabled && time - lastDisabledTime >= respawnTime;
        }
    }
}
