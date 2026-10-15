using System;
using DungeonStory.Foundation.Facts;

namespace DungeonStory.Infrastructure
{
    [Serializable]
    public sealed class EmergencyWorkSuspensionFactSaveData
    {
        public string characterId = string.Empty;
        public string workerDisplayName = string.Empty;
        public string workTypeId = string.Empty;
        public string workDisplayName = string.Empty;
        public string targetBuildingId = string.Empty;
        public string targetDisplayName = string.Empty;
        public long alertEpochId;
        public long suspendedAtAbsoluteHour;
        public long ownerRevision;
        public int absoluteDay;
        public bool progressExternallyPersisted;
        public float inlineCompletedWork;
        public float inlineRequiredWork;

        public EmergencyWorkSuspensionFactSaveData Clone() =>
            (EmergencyWorkSuspensionFactSaveData)MemberwiseClone();

        public void Validate()
        {
            if (!DomainFactValueSyntax.IsIdentity(characterId)
                || !new CharacterId(characterId).IsValid
                || !Name(workerDisplayName)
                || !DomainFactValueSyntax.IsIdentity(workTypeId)
                || !Name(workDisplayName)
                || !DomainFactValueSyntax.IsIdentity(targetBuildingId)
                || !new BuildingInstanceId(targetBuildingId).IsValid
                || !Name(targetDisplayName)
                || alertEpochId <= 0L
                || suspendedAtAbsoluteHour < 0L
                || ownerRevision <= 0L
                || absoluteDay <= 0
                || !float.IsFinite(inlineCompletedWork)
                || !float.IsFinite(inlineRequiredWork)
                || progressExternallyPersisted
                    && (inlineCompletedWork != 0f
                        || inlineRequiredWork != 0f)
                || !progressExternallyPersisted
                    && (inlineCompletedWork < 0f
                        || inlineRequiredWork <= 0f
                        || inlineCompletedWork >= inlineRequiredWork))
            {
                throw new ArgumentException(
                    "emergency-work-suspension-fact-data-invalid");
            }
        }

        private static bool Name(string value) =>
            DomainFactValueSyntax.IsText(
                value,
                DomainFactValueSyntax.MaximumNameCharacters,
                allowEmpty: false)
            && value == value.Trim();
    }

    [Serializable]
    public sealed class EmergencyWorkSuspensionObservationSaveData
    {
        public EmergencyWorkSuspensionFactSaveData fact = new();
        public string publicationKey = string.Empty;
        public bool pending;

        public EmergencyWorkSuspensionObservationSaveData Clone() => new()
        {
            fact = fact?.Clone(),
            publicationKey = publicationKey,
            pending = pending
        };

        public void Validate()
        {
            if (fact == null
                || !DomainFactValueSyntax.IsText(
                    publicationKey,
                    1024,
                    allowEmpty: false)
                || publicationKey != publicationKey.Trim())
            {
                throw new ArgumentException(
                    "emergency-work-suspension-observation-invalid");
            }
            fact.Validate();
        }
    }
}
