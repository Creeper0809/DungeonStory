using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation.Facts;
using UnityEngine;

public enum ProductionCommandKind
{
    BillAdded = 1, BillRemoved = 2, BillPriorityChanged = 3, MinimumCraftQualityChanged = 4,
    SuspensionChanged = 5, StockPolicyChanged = 6, OrderModeChanged = 7, DistributionPolicyChanged = 8,
    WorkerPolicyChanged = 9, EmergencyWorkerChanged = 10, StockSensorInstalled = 11,
    StockSensorRemoved = 12, StockSensorUnlockAcknowledged = 13
}

// Typed domain state, not formatted ledger text. Each command chooses which values it changes.
[Serializable]
public sealed class ProductionCommandStateData
{
    public bool exists;
    public ProductionOrderMode mode, pendingMode;
    public int remainingCycles, minimumReserve, targetStock, minimumQuality = -1, blockedCode, localIndex;
    public bool hasPendingMode, suspended, sensorInstalled, sensorAcknowledged;
    public ProductionDistributionMode distributionMode;
    public List<ProductionConsumerRoutePolicy> routes = new();
    public WorkerSelectionPolicySaveData workerPolicy = WorkerSelectionPolicySaveData.Anyone();
    public string reservedWorkerId = string.Empty, emergencyWorkerId = string.Empty;

    public ProductionCommandStateData Clone()
    {
        var copy = (ProductionCommandStateData)MemberwiseClone();
        copy.routes = routes?.Select(row => row?.Clone()).ToList();
        copy.workerPolicy = workerPolicy?.CloneNormalized();
        return copy;
    }
    public bool SameAs(ProductionCommandStateData other) => other != null
        && JsonUtility.ToJson(this) == JsonUtility.ToJson(other);
    public void Validate()
    {
        if (!Enum.IsDefined(typeof(ProductionOrderMode), mode) || !Enum.IsDefined(typeof(ProductionOrderMode), pendingMode)
            || !Enum.IsDefined(typeof(ProductionDistributionMode), distributionMode)
            || routes == null || routes.Any(row => row == null || !DomainFactValueSyntax.IsIdentity(row.consumerId)
                || !float.IsFinite(row.waitingSeconds) || JsonUtility.ToJson(row) != JsonUtility.ToJson(row.Clone()))
            || workerPolicy == null || !Enum.IsDefined(typeof(WorkerSelectionMode), workerPolicy.mode)
            || !Enum.IsDefined(typeof(WorkerRequirementMatchMode), workerPolicy.matchMode)
            || !Enum.IsDefined(typeof(WorkerCandidateSortMode), workerPolicy.sortMode)
            || JsonUtility.ToJson(workerPolicy) != JsonUtility.ToJson(workerPolicy.CloneNormalized())
            || reservedWorkerId == null || emergencyWorkerId == null || minimumQuality < -1 || localIndex < 0)
            throw new InvalidOperationException("production-command-state-invalid");
    }
}

[Serializable]
public sealed class ProductionCommandFactData
{
    public long ownerRevision;
    public ProductionCommandKind kind;
    public string billId = string.Empty, recipeId = string.Empty, recipeDisplayName = string.Empty;
    public string facilityId = string.Empty, facilityDisplayName = string.Empty;
    public int facilityX, facilityY, absoluteDay;
    public ProductionCommandStateData before = new(), after = new();
    public ProductionCommandFactData Clone()
    {
        var copy = (ProductionCommandFactData)MemberwiseClone();
        copy.before = before?.Clone(); copy.after = after?.Clone(); return copy;
    }
}

public sealed class ProductionCommandAppliedFact
{
    private readonly ProductionCommandFactData data;
    public ProductionCommandAppliedFact(ProductionCommandFactData value)
    {
        if (value?.before == null || value.after == null) throw new InvalidOperationException("production-command-fact-missing");
        value.before.Validate(); value.after.Validate();
        if (value.ownerRevision < 1 || value.ownerRevision == long.MaxValue || value.absoluteDay < 1
            || !Enum.IsDefined(typeof(ProductionCommandKind), value.kind)
            || !DomainFactValueSyntax.IsIdentity(value.facilityId)
            || !DomainFactValueSyntax.IsText(value.facilityDisplayName, 4096, false)
            || !DomainFactValueSyntax.IsText(value.recipeDisplayName, 4096, true)
            || value.billId == null || value.recipeId == null
            || value.billId.Length > 0 && !DomainFactValueSyntax.IsIdentity(value.billId)
            || value.recipeId.Length > 0 && !DomainFactValueSyntax.IsIdentity(value.recipeId))
            throw new InvalidOperationException("production-command-fact-invalid");
        data = value.Clone();
    }
    public long OwnerRevision => data.ownerRevision;
    public ProductionCommandKind Kind => data.kind;
    public string BillId => data.billId;
    public string RecipeId => data.recipeId;
    public string RecipeDisplayName => data.recipeDisplayName;
    public string FacilityId => data.facilityId;
    public string FacilityDisplayName => data.facilityDisplayName;
    public int FacilityX => data.facilityX;
    public int FacilityY => data.facilityY;
    public int AbsoluteDay => data.absoluteDay;
    public ProductionCommandStateData Before => data.before.Clone();
    public ProductionCommandStateData After => data.after.Clone();
    public ProductionCommandFactData Snapshot() => data.Clone();
}

[Serializable]
public sealed class ProductionCommandObservationSaveData
{
    public ProductionCommandFactData fact = new();
    public string publicationKey = string.Empty, publicationId = string.Empty, publicationFingerprint = string.Empty;
    public ProductionCommandObservationSaveData Clone() => new()
    { fact = fact?.Clone(), publicationKey = publicationKey, publicationId = publicationId, publicationFingerprint = publicationFingerprint };
    public void Validate(long nextSequence)
    {
        var value = new ProductionCommandAppliedFact(fact);
        if (value.OwnerRevision >= nextSequence || publicationKey == null || publicationId == null || publicationFingerprint == null
            || !DomainFactValueSyntax.IsText(publicationKey, 4096, true) || publicationKey != publicationKey.Trim()
            || (publicationId.Length == 0) != (publicationFingerprint.Length == 0)
            || publicationId.Length > 0 && (publicationKey.Length == 0
                || !DomainFactValueSyntax.IsText(publicationId, 4096, false) || publicationId != publicationId.Trim()
                || !IsFingerprint(publicationFingerprint)))
            throw new InvalidOperationException("production-command-observation-invalid");
    }
    public static bool IsFingerprint(string value) => value?.Length == 64
        && value.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f');
}
