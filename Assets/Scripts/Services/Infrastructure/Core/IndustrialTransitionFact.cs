using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation.Facts;

public enum IndustrialTransitionKind
{
    PowerConnectionChanged = 1, PowerPriorityChanged, PowerBreakerReset, FluidBlockageCleared,
    WaterTransferModeChanged, FluidLeakRepaired, ConveyorNodeEnabledChanged, ConveyorDestinationChanged,
    ConveyorOverflowPolicyChanged, ConveyorFilterChanged, ConveyorOverflowApproved, AutomationModeChanged,
    AutomationMaintained, AutomationMaintenanceRequiredEntered, PowerFuelConsumed, PowerBreakerTripped,
    WastewaterBatchCompleted, FluidLeakEpisode, WastewaterBackflow, PipeFreezeLatched, PipeFreezeRecovered
}

// Values at the transition boundary, not a rendered message or a second writable authority.
[Serializable]
public sealed class IndustrialStateData
{
    public bool enabled, connected, breakerTripped, powered, overflowApproved, freezeLatched, hasTemperature;
    public PowerPriority powerPriority = PowerPriority.Critical;
    public AutomationMode automationMode;
    public WaterContainerTransferMode transferMode;
    public ConveyorOverflowPolicy overflowPolicy;
    public InfrastructureStatusCode status;
    public List<string> statusParameters = new();
    public float fault, heat, fuelSeconds, maintenance, blockage, leak, work;
    public float cleanWater, unsafeWater, foulWater, wastewater, outputWater, filthAmount, temperatureC;
    public string destinationId = "", warehouseId = "", occurrenceId = "", filthId = "";
    public ConveyorFilterCriteria filter = new();

    public IndustrialStateData Clone()
    {
        var copy = (IndustrialStateData)MemberwiseClone();
        copy.statusParameters = statusParameters?.ToList();
        copy.filter = CloneFilter(filter);
        return copy;
    }

    internal void Validate()
    {
        if (!Enum.IsDefined(typeof(PowerPriority), powerPriority) || !Enum.IsDefined(typeof(AutomationMode), automationMode)
            || !Enum.IsDefined(typeof(WaterContainerTransferMode), transferMode) || !Enum.IsDefined(typeof(ConveyorOverflowPolicy), overflowPolicy)
            || !Enum.IsDefined(typeof(InfrastructureStatusCode), status)
            || statusParameters == null || statusParameters.Any(value => !DomainFactValueSyntax.IsText(value, 4096, true))
            || new[] { fault, heat, fuelSeconds, maintenance, blockage, leak, work, cleanWater, unsafeWater, foulWater,
                wastewater, outputWater, filthAmount }.Any(value => !float.IsFinite(value) || value < 0)
            || !float.IsFinite(temperatureC) || new[] { destinationId, warehouseId, occurrenceId, filthId }.Any(value => !IndustrialTransitionFact.OptionalId(value))
            || filter == null || filter.itemIds == null || filter.stockCategories == null || filter.materialIds == null
            || filter.itemIds.Any(value => !DomainFactValueSyntax.IsIdentity(value)) || filter.materialIds.Any(value => !DomainFactValueSyntax.IsIdentity(value))
            || filter.itemIds.Distinct(StringComparer.Ordinal).Count() != filter.itemIds.Count
            || filter.materialIds.Distinct(StringComparer.Ordinal).Count() != filter.materialIds.Count
            || filter.stockCategories.Distinct().Count() != filter.stockCategories.Count
            || filter.stockCategories.Any(value => !Enum.IsDefined(typeof(StockCategory), value))
            || !Enum.IsDefined(typeof(CombatEquipmentQuality), filter.minimumQuality) || !Enum.IsDefined(typeof(CombatEquipmentQuality), filter.maximumQuality)
            || filter.minimumQuality > filter.maximumQuality || !float.IsFinite(filter.minimumFreshness01) || !float.IsFinite(filter.maximumFreshness01)
            || filter.minimumFreshness01 < 0 || filter.maximumFreshness01 > 1 || filter.minimumFreshness01 > filter.maximumFreshness01)
            throw new InvalidOperationException("industrial-transition-state-invalid");
    }

    private static ConveyorFilterCriteria CloneFilter(ConveyorFilterCriteria value) => value == null ? null : new()
    {
        itemIds = value.itemIds?.ToList(), stockCategories = value.stockCategories?.ToList(), materialIds = value.materialIds?.ToList(),
        allowForbidden = value.allowForbidden, filterQuality = value.filterQuality, minimumQuality = value.minimumQuality,
        maximumQuality = value.maximumQuality, filterFreshness = value.filterFreshness, minimumFreshness01 = value.minimumFreshness01,
        maximumFreshness01 = value.maximumFreshness01, allowContaminated = value.allowContaminated
    };
}

[Serializable]
public sealed class IndustrialItemDeltaData
{
    public string stackId = "";
    public int quantity;
    public IndustrialItemDeltaData Clone() => (IndustrialItemDeltaData)MemberwiseClone();
}

[Serializable]
public sealed class IndustrialTransitionDetails
{
    public string operationId = "", itemId = "", physicalCommitId = "", wasteNetworkId = "", waterNetworkId = "", definitionId = "", reason = "";
    public int quantity;
    public long massGrams;
    public bool continuousWear;
    public WorldWaterQuality outputQuality;
    public float loadRatio, tripHeat, overloadTolerance, inputUsed, waterProduced, cleanLoss, unsafeLoss, foulLoss, totalLoss, overflow, filthAdded;
    public float freezeThresholdC, recoveryThresholdC, throughputMultiplier;
    public List<string> sourceStackIds = new(), filthIds = new();
    public List<IndustrialItemDeltaData> itemDeltas = new();
    public IndustrialTransitionDetails Clone()
    {
        var copy = (IndustrialTransitionDetails)MemberwiseClone();
        copy.sourceStackIds = sourceStackIds?.ToList(); copy.filthIds = filthIds?.ToList();
        copy.itemDeltas = itemDeltas?.Select(value => value?.Clone()).ToList(); return copy;
    }
    internal void Validate()
    {
        if (new[] { operationId, itemId, physicalCommitId, wasteNetworkId, waterNetworkId, definitionId }.Any(value => !IndustrialTransitionFact.OptionalId(value))
            || !DomainFactValueSyntax.IsText(reason, 4096, true) || quantity < 0 || massGrams < 0
            || !Enum.IsDefined(typeof(WorldWaterQuality), outputQuality)
            || new[] { loadRatio, tripHeat, overloadTolerance, inputUsed, waterProduced, cleanLoss, unsafeLoss, foulLoss,
                totalLoss, overflow, filthAdded, throughputMultiplier }.Any(value => !float.IsFinite(value) || value < 0)
            || !float.IsFinite(freezeThresholdC) || !float.IsFinite(recoveryThresholdC)
            || sourceStackIds == null || filthIds == null || itemDeltas == null
            || sourceStackIds.Any(value => !DomainFactValueSyntax.IsIdentity(value)) || filthIds.Any(value => !DomainFactValueSyntax.IsIdentity(value))
            || sourceStackIds.Distinct(StringComparer.Ordinal).Count() != sourceStackIds.Count
            || filthIds.Distinct(StringComparer.Ordinal).Count() != filthIds.Count
            || itemDeltas.Any(value => value == null || !DomainFactValueSyntax.IsIdentity(value.stackId) || value.quantity <= 0)
            || itemDeltas.Select(value => value.stackId).Distinct(StringComparer.Ordinal).Count() != itemDeltas.Count)
            throw new InvalidOperationException("industrial-transition-details-invalid");
    }
}

[Serializable]
public sealed class IndustrialTransitionFactData
{
    public long ownerRevision;
    public IndustrialTransitionKind kind;
    public string targetId = "", facilityId = "", facilityDisplayName = "";
    public int facilityX, facilityY, absoluteDay;
    public IndustrialStateData before = new(), after = new();
    public IndustrialTransitionDetails details = new();
    public IndustrialTransitionFactData Clone()
    {
        var copy = (IndustrialTransitionFactData)MemberwiseClone();
        copy.before = before?.Clone(); copy.after = after?.Clone(); copy.details = details?.Clone(); return copy;
    }
}

public sealed class IndustrialTransitionFact
{
    private readonly IndustrialTransitionFactData data;
    public IndustrialTransitionFact(IndustrialTransitionFactData value)
    {
        ValidateDraft(value);
        if (value.ownerRevision < 1 || value.ownerRevision == long.MaxValue) throw new InvalidOperationException("industrial-transition-revision-invalid");
        data = value.Clone();
    }
    public IndustrialTransitionFactData Snapshot() => data.Clone();
    public long OwnerRevision => data.ownerRevision;
    public IndustrialTransitionKind Kind => data.kind;
    public static bool IsAutonomous(IndustrialTransitionKind kind) => kind >= IndustrialTransitionKind.PowerFuelConsumed;
    public static void ValidateDraft(IndustrialTransitionFactData value)
    {
        if (value?.before == null || value.after == null || value.details == null || value.ownerRevision < 0
            || !Enum.IsDefined(typeof(IndustrialTransitionKind), value.kind) || value.absoluteDay < 1
            || !DomainFactValueSyntax.IsIdentity(value.targetId) || !DomainFactValueSyntax.IsIdentity(value.facilityId)
            || !DomainFactValueSyntax.IsText(value.facilityDisplayName, 4096, false))
            throw new InvalidOperationException("industrial-transition-fact-invalid");
        value.before.Validate(); value.after.Validate(); value.details.Validate();
    }
    internal static bool OptionalId(string value) => value != null && (value.Length == 0 || DomainFactValueSyntax.IsIdentity(value));
}

[Serializable]
public sealed class InfrastructureObservationSaveData
{
    public IndustrialTransitionFactData fact = new();
    public string publicationKey = "", publicationId = "", publicationFingerprint = "";
    public InfrastructureObservationSaveData Clone() => new()
    { fact = fact?.Clone(), publicationKey = publicationKey, publicationId = publicationId, publicationFingerprint = publicationFingerprint };
    public void Validate(long nextRevision)
    {
        _ = new IndustrialTransitionFact(fact);
        if (fact.ownerRevision >= nextRevision || !DomainFactValueSyntax.IsText(publicationKey, 4096, false) || publicationKey != publicationKey.Trim()
            || publicationId == null || publicationFingerprint == null || (publicationId.Length == 0) != (publicationFingerprint.Length == 0)
            || publicationId.Length > 0 && (!DomainFactValueSyntax.IsText(publicationId, 4096, false) || publicationId != publicationId.Trim()
                || !IsDigest(publicationFingerprint))) throw new InvalidOperationException("industrial-observation-proof-invalid");
    }
    public static bool IsDigest(string value) => value?.Length == 64 && value.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f');
}
