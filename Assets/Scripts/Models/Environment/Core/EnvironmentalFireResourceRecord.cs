using System;
using System.Linq;

namespace DungeonStory.Environment
{
    public enum EnvironmentalFireResourceUse { FuelLoss = 1, SuppressionWater = 2 }

    /// <summary>Frozen domain inputs retained only until the physical debit is acknowledged.</summary>
    [Serializable]
    public sealed class EnvironmentalFireResourceRecord
    {
        public EnvironmentalFireResourceUse use;
        public string targetId = string.Empty;
        public string fireId = string.Empty;
        public string leaseId = string.Empty;
        public string workerId = string.Empty;
        public string workerName = string.Empty;
        public int positionX, positionY;
        public int absoluteDay;
        public PhysicalItemBatchDispositionSaveData physical;
        public string publicationKey = string.Empty;

        public EnvironmentalFireResourceRecord Clone()
        {
            var result = (EnvironmentalFireResourceRecord)MemberwiseClone();
            if (physical == null) return result;
            result.physical = new PhysicalItemBatchDispositionSaveData
            {
                kind = physical.kind, operationId = physical.operationId, reasonCode = physical.reasonCode,
                requestFingerprint = physical.requestFingerprint, sourceStackIds = physical.sourceStackIds?.ToList(),
                quantity = physical.quantity, inputMassGrams = physical.inputMassGrams, commitId = physical.commitId,
                ownerRevision = physical.ownerRevision, publicationExpected = physical.publicationExpected,
                publicationKey = physical.publicationKey, publicationId = physical.publicationId,
                publicationFingerprint = physical.publicationFingerprint, publicationDelivered = physical.publicationDelivered,
                defaultObservationDay = physical.defaultObservationDay,
                sourceFacts = physical.sourceFacts?.Select(value => value == null ? null : new PhysicalItemDispositionSourceFactSaveData
                {
                    stackId = value.stackId, itemDefinitionId = value.itemDefinitionId, itemInstanceId = value.itemInstanceId,
                    quantity = value.quantity, massGrams = value.massGrams, sourceX = value.sourceX,
                    sourceY = value.sourceY, displayText = value.displayText
                }).ToList()
            };
            return result;
        }
    }
}
