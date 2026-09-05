using System;
using System.Collections.Generic;
using System.Linq;

internal sealed class CombatEquipmentRuntimeState
{
    internal int NextCraftSequence;
    internal Dictionary<string, CharacterCombatLoadoutState> Loadouts { get; } =
        new(StringComparer.Ordinal);
    internal List<CombatEquipmentCraftOrderSaveData> CraftOrders { get; } = new();
    internal Dictionary<string, CombatEquipmentCraftMaterialPolicySaveData>
        CraftMaterialPolicies { get; } = new(StringComparer.Ordinal);
    internal Dictionary<string, CombatEquipmentCraftTerminalEffectSaveData>
        CraftTerminalEffects { get; } = new(StringComparer.Ordinal);
    internal List<EquipmentHistoryTransferOrder> HistoryTransferOrders { get; } =
        new();
    internal HashSet<string> ClaimedLineageSealRegionIds { get; } =
        new(StringComparer.Ordinal);

    internal CombatEquipmentRuntimeState Clone()
    {
        CombatEquipmentRuntimeState clone = new()
        {
            NextCraftSequence = NextCraftSequence
        };
        foreach (KeyValuePair<string, CharacterCombatLoadoutState> pair in
                 Loadouts)
            clone.Loadouts.Add(pair.Key, CloneLoadout(pair.Value));
        clone.CraftOrders.AddRange(CraftOrders.Select(value => value?.Clone()));
        foreach (KeyValuePair<string,
                     CombatEquipmentCraftMaterialPolicySaveData> pair in
                 CraftMaterialPolicies)
            clone.CraftMaterialPolicies.Add(pair.Key, pair.Value?.Clone());
        foreach (KeyValuePair<string,
                     CombatEquipmentCraftTerminalEffectSaveData> pair in
                 CraftTerminalEffects)
            clone.CraftTerminalEffects.Add(pair.Key, pair.Value?.Clone());
        clone.HistoryTransferOrders.AddRange(
            HistoryTransferOrders.Select(value => value?.Clone()));
        foreach (string id in ClaimedLineageSealRegionIds)
            clone.ClaimedLineageSealRegionIds.Add(id);
        return clone;
    }

    private static CharacterCombatLoadoutState CloneLoadout(
        CharacterCombatLoadoutState source) => source == null
        ? null
        : new CharacterCombatLoadoutState
        {
            characterId = source.characterId ?? string.Empty,
            activeProfileId = source.activeProfileId
                ?? CombatLoadoutPresetIds.Peace,
            profiles = source.profiles?.Select(value => value?.Clone())
                .Where(value => value != null).ToList()
                ?? new List<CharacterCombatLoadoutProfile>()
        };
}

public sealed class CombatEquipmentRestoreCandidate
{
    internal CombatEquipmentRestoreCandidate(
        CombatEquipmentRuntimeState state,
        IReadOnlyList<ProductionDomainOutputRestoreAcknowledgement>
            outputAcknowledgements = null)
    {
        State = state ?? throw new ArgumentNullException(nameof(state));
        OutputAcknowledgements = Array.AsReadOnly((outputAcknowledgements
                ?? Array.Empty<ProductionDomainOutputRestoreAcknowledgement>())
            .ToArray());
    }

    internal CombatEquipmentRuntimeState State { get; }
    internal IReadOnlyList<ProductionDomainOutputRestoreAcknowledgement>
        OutputAcknowledgements { get; }
}

public sealed class CombatEquipmentRuntimeStateStore
{
    private readonly DungeonRuntimeAggregateRootStore rootStore;

    public CombatEquipmentRuntimeStateStore(
        DungeonRuntimeAggregateRootStore rootStore)
    {
        this.rootStore = rootStore
            ?? throw new ArgumentNullException(nameof(rootStore));
    }

    internal CombatEquipmentRuntimeState Current =>
        rootStore.GetOrCreate(() => new CombatEquipmentRuntimeState());

    internal void Replace(CombatEquipmentRuntimeState restored)
    {
        rootStore.Replace(
            restored ?? throw new ArgumentNullException(nameof(restored)));
    }
}

/// <summary>
/// Writes the repository-owned equipment aggregate back to its physical stack.
/// </summary>
public sealed class CombatEquipmentPhysicalStateWriter
{
    private readonly IItemInstanceRepository itemInstances;
    private readonly IEquipmentPhysicalItemGateway physicalItems;
    private readonly IProductionOutputCapabilityRegistry outputCapabilities;

    public CombatEquipmentPhysicalStateWriter(
        IItemInstanceRepository itemInstances,
        IEquipmentPhysicalItemGateway physicalItems,
        IProductionOutputCapabilityRegistry outputCapabilities)
    {
        this.itemInstances = itemInstances
            ?? throw new ArgumentNullException(nameof(itemInstances));
        this.physicalItems = physicalItems
            ?? throw new ArgumentNullException(nameof(physicalItems));
        this.outputCapabilities = outputCapabilities
            ?? throw new ArgumentNullException(nameof(outputCapabilities));
    }

    public ProductionOutputCapabilityDescriptor CaptureOutputCapability(
        string outputLineId,
        string itemId,
        string capabilityId) => outputCapabilities.CaptureDeclaredDescriptor(
        outputLineId,
        itemId,
        capabilityId);

    public bool TryValidateOutputCapability(
        ProductionOutputCapabilitySaveData frozen,
        out DomainFailure failure)
    {
        failure = DomainFailure.None;
        if (frozen == null || frozen.IsEmpty)
        {
            failure = new DomainFailure(
                FailureCode.ProductionOutputUnavailable,
                string.Empty,
                "combat-output-capability-missing");
            return false;
        }
        return outputCapabilities.TryValidateExact(
            frozen.ToDescriptor(),
            out _,
            out failure);
    }

    public void Persist(CombatEquipmentInstance equipment)
    {
        if (equipment == null || string.IsNullOrWhiteSpace(equipment.sourceStackId))
        {
            return;
        }

        IDictionary<string, EquipmentModuleInstance> modules =
            itemInstances.EquipmentModules;
        if (!physicalItems.TrySetInstanceComponent(
                equipment.sourceStackId,
                EquipmentItemStateCodec.Encode(
                    equipment,
                    (equipment.moduleSlots ?? new List<EquipmentModuleSlotState>())
                        .Where(slot => slot != null
                            && !string.IsNullOrWhiteSpace(slot.moduleInstanceId)
                            && modules.ContainsKey(slot.moduleInstanceId))
                        .Select(slot => modules[slot.moduleInstanceId]))))
        {
            throw new InvalidOperationException(
                $"Failed to persist equipment '{equipment.instanceId}' to physical item "
                + $"stack '{equipment.sourceStackId}'.");
        }
    }
}

/// <summary>
/// Owns character loadout references. Equipment state itself remains in the item
/// repository; loadouts store only persistent equipment instance IDs.
/// </summary>
public sealed class CombatEquipmentLoadoutStore
{
    private readonly CombatEquipmentRuntimeStateStore stateStore;

    public CombatEquipmentLoadoutStore(
        CombatEquipmentRuntimeStateStore stateStore)
    {
        this.stateStore = stateStore
            ?? throw new ArgumentNullException(nameof(stateStore));
    }

    public IDictionary<string, CharacterCombatLoadoutState> States =>
        stateStore.Current.Loadouts;

    public void RemoveEquipment(string instanceId)
    {
        foreach (CharacterCombatLoadoutState state in States.Values)
        {
            foreach (CharacterCombatLoadoutProfile profile in state.profiles)
            {
                profile.weaponInstanceIds.RemoveAll(id =>
                    string.Equals(id, instanceId, StringComparison.Ordinal));
                profile.armorInstanceIds.RemoveAll(id =>
                    string.Equals(id, instanceId, StringComparison.Ordinal));
                if (string.Equals(
                        profile.shieldInstanceId,
                        instanceId,
                        StringComparison.Ordinal))
                {
                    profile.shieldInstanceId = string.Empty;
                }

                if (string.Equals(
                        profile.activeWeaponInstanceId,
                        instanceId,
                        StringComparison.Ordinal))
                {
                    profile.activeWeaponInstanceId =
                        profile.weaponInstanceIds.FirstOrDefault() ?? string.Empty;
                }
            }
        }
    }
}
