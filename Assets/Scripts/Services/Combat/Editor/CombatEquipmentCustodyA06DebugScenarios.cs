#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

public static class CombatEquipmentCustodyA06DebugScenarios
{
    public static void RunSupplyForBatch()
    {
        Require(Application.isBatchMode, "Equipment supply validation requires isolated batch execution.");
        Debug.Log(VerifyReloadAndRestore());
        VerifyPowerAndRepositoryReplacement();
        Debug.Log("ARCHITECTURE_EQUIPMENT_SUPPLY=PASS; exact ammunition type and quantity; rejected mixing unchanged; JSON restore and resume; power failure unchanged; current repository after replacement");
    }

    public static void RunBatch()
    {
        string output = Environment.GetEnvironmentVariable(
            "DUNGEONSTORY_AUDIT_REMEDIATION_OUTPUT");
        if (string.IsNullOrWhiteSpace(output) || Directory.Exists(output))
        {
            throw new InvalidOperationException(
                "A06 requires a new create-only output directory.");
        }

        Directory.CreateDirectory(output);
        try
        {
            JObject readOnly = VerifyReadOnlyLoadoutQueries();
            JObject picked = VerifyPickedEquipmentCustody();
            JObject unequipped = VerifyBatchUnequipCustody();
            JObject reloaded = VerifyReloadAndRestore();
            WriteReport(output, new JObject
            {
                ["schema"] = "a06-equipment-custody@1",
                ["passed"] = true,
                ["rowId"] = "A06",
                ["controlledIntegration"] = true,
                ["actualGameplayScene"] = false,
                ["coverage"] = new JObject
                {
                    ["sameDefinitionDifferentInstance"] = true,
                    ["magazineQuantity"] = true,
                    ["reservationOrCustodyRejection"] = true,
                    ["partialMutationRollback"] = true,
                    ["saveRestore"] = true,
                    ["readOnlySnapshots"] = true
                },
                ["readOnlyLoadoutQueries"] = readOnly,
                ["pickedEquip"] = picked,
                ["batchUnequip"] = unequipped,
                ["reload"] = reloaded,
                ["fullAuditClosure"] = false,
                ["humanApprovalClaimed"] = false,
                ["trainingEligible"] = false
            });
            Debug.Log("A06 equipment custody scenarios: PASS");
        }
        catch (Exception error)
        {
            WriteReport(output, new JObject
            {
                ["schema"] = "a06-equipment-custody@1",
                ["passed"] = false,
                ["rowId"] = "A06",
                ["failure"] = error.ToString(),
                ["fullAuditClosure"] = false,
                ["humanApprovalClaimed"] = false,
                ["trainingEligible"] = false
            });
            throw;
        }
    }

    private static JObject VerifyReadOnlyLoadoutQueries()
    {
        CombatEquipmentRuntime runtime = CreateRuntime(new ExactEquipmentGateway(), out _);
        JToken before = JToken.FromObject(runtime.Capture());
        var selector = new CombatFallbackWeaponSelector(new CombatFallbackWeaponRuntimeAdapter(runtime));
        string[] battleOnly = { "character:battle:query-test:encounter:01:0:0", "character:battle:query-test:encounter:01:1:0" };
        foreach (string actorId in battleOnly)
        {
            CharacterCombatLoadoutProfile profile = runtime.GetActiveProfileSnapshot(actorId);
            Require(profile != null && !profile.holdFire,
                "unregistered actor lost its default unarmed read projection");
            Require(!selector.TrySelect(actorId, false, out _),
                "unarmed battle-only actor acquired a fallback weapon");
            Require(!runtime.TryGetActiveProfileSnapshot(actorId, out _),
                "battle-only read query admitted a persistent loadout owner");
        }
        Require(JToken.DeepEquals(before, JToken.FromObject(runtime.Capture())),
            "read-only battle weapon selection changed captured equipment state");

        const string ownerId = "character:loadout-query-owner";
        Require(runtime.TrySetHoldFire(ownerId, true), "explicit loadout command was rejected");
        Require(runtime.Capture().loadouts.Count == 1,
            "explicit command did not create exactly one intended owner");
        JToken committed = JToken.FromObject(runtime.Capture());
        CharacterCombatLoadoutProfile detached = runtime.GetActiveProfileSnapshot(ownerId);
        detached.holdFire = false;
        runtime.GetActiveProfileSnapshot(battleOnly[0]).holdFire = true;
        Require(runtime.GetActiveProfileSnapshot(ownerId).holdFire
            && !runtime.GetActiveProfileSnapshot(battleOnly[0]).holdFire
            && JToken.DeepEquals(committed, JToken.FromObject(runtime.Capture())),
            "snapshot edits changed a committed owner or persisted a battle-only owner");
        return new JObject
        {
            ["unregisteredQueriesNoMutation"] = true,
            ["virtualBattleFallbackNoMutation"] = true,
            ["explicitCommandCreatesOnlyIntendedOwner"] = true,
            ["snapshotsDetached"] = true
        };
    }

    private static JObject VerifyPickedEquipmentCustody()
    {
        ExactEquipmentGateway gateway = new();
        CombatEquipmentRuntime runtime = CreateRuntime(gateway, out _);
        CombatEquipmentInstance chosen = runtime.CreateInstance(
            "weapon:dagger",
            CombatEquipmentQuality.Normal);
        CombatEquipmentInstance other = runtime.CreateInstance(
            "weapon:dagger",
            CombatEquipmentQuality.Normal);
        string itemId = PhysicalItemIds.ForEquipment(chosen.definitionId);
        string chosenStack = gateway.SeedUnique(
            itemId,
            chosen.instanceId,
            new Vector2Int(2, 3),
            WorldItemStackState.Carried);
        string otherStack = gateway.SeedUnique(
            itemId,
            other.instanceId,
            new Vector2Int(4, 3),
            WorldItemStackState.Stored);
        Require(runtime.TryLinkToWorldStack(
                chosen.instanceId,
                chosenStack,
                CombatEquipmentWorldState.Carried),
            "chosen instance could not enter carried custody");
        Require(runtime.TryLinkToWorldStack(
                other.instanceId,
                otherStack,
                CombatEquipmentWorldState.Stored),
            "other instance could not enter stored custody");

        GameObject inventoryObject = new("A06_PickedEquipmentInventory");
        try
        {
            CharacterCarryInventory inventory =
                inventoryObject.AddComponent<CharacterCarryInventory>();
            inventory.Restore(Carry(
                chosenStack,
                itemId,
                chosen.instanceId));
            Require(runtime.TryAssignPickedEquipmentToCharacter(
                    "character:a06:picker",
                    chosen.instanceId,
                    inventory,
                    chosenStack,
                    itemId,
                    out string assignFailure),
                "picked equipment assign failed: " + assignFailure);
            Require(inventory.Items.Count == 0,
                "picked equipment carry projection was not consumed");
            Require(!gateway.ContainsStack(chosenStack),
                "picked equipment physical child was not absorbed");
            Require(gateway.ContainsExact(otherStack, other.instanceId),
                "same-definition sibling instance was consumed");
            Require(runtime.TryGetInstance(
                    chosen.instanceId,
                    out CombatEquipmentInstance equipped)
                && equipped.worldState == CombatEquipmentWorldState.Equipped
                && equipped.ownerCharacterId == "character:a06:picker"
                && string.IsNullOrEmpty(equipped.sourceStackId),
                "picked equipment loadout custody did not commit");
            Require(runtime.TryGetInstance(
                    other.instanceId,
                    out CombatEquipmentInstance untouched)
                && untouched.worldState == CombatEquipmentWorldState.Stored
                && untouched.sourceStackId == otherStack,
                "same-definition sibling aggregate changed");

            CharacterCarryInventory empty = inventory;
            Require(!runtime.TryAssignPickedEquipmentToCharacter(
                    "character:a06:picker",
                    other.instanceId,
                    empty,
                    otherStack,
                    itemId,
                    out _),
                "missing exact carry row was accepted");
            Require(gateway.ContainsExact(otherStack, other.instanceId)
                && runtime.TryGetInstance(
                    other.instanceId,
                    out untouched)
                && untouched.worldState == CombatEquipmentWorldState.Stored,
                "carry-row rejection mutated the sibling custody");

            CombatEquipmentInstance rejected = runtime.CreateInstance(
                "weapon:dagger",
                CombatEquipmentQuality.Normal);
            string rejectedStack = gateway.SeedUnique(
                itemId,
                rejected.instanceId,
                new Vector2Int(6, 3),
                WorldItemStackState.Carried);
            Require(runtime.TryLinkToWorldStack(
                    rejected.instanceId,
                    rejectedStack,
                    CombatEquipmentWorldState.Carried),
                "rejection instance could not enter carried custody");
            inventory.Restore(Carry(
                rejectedStack,
                itemId,
                rejected.instanceId));
            gateway.FailNextAbsorb = true;
            Require(!runtime.TryAssignPickedEquipmentToCharacter(
                    "character:a06:picker",
                    rejected.instanceId,
                    inventory,
                    rejectedStack,
                    itemId,
                    out string rejectedFailure)
                && rejectedFailure == "equipment.assign.physical_absorb_failed",
                "absorb rejection did not surface the exact failure");
            Require(inventory.Items.Count == 1
                && inventory.Items[0].sourceStackId == rejectedStack
                && inventory.Items[0].itemInstanceId == rejected.instanceId,
                "absorb rejection did not roll back the carry projection");
            Require(gateway.ContainsExact(rejectedStack, rejected.instanceId)
                && runtime.TryGetInstance(
                    rejected.instanceId,
                    out CombatEquipmentInstance rejectedAfter)
                && rejectedAfter.worldState == CombatEquipmentWorldState.Carried
                && string.IsNullOrEmpty(rejectedAfter.ownerCharacterId),
                "absorb rejection left partial loadout custody");

            return new JObject
            {
                ["equippedInstanceId"] = chosen.instanceId,
                ["untouchedSiblingInstanceId"] = other.instanceId,
                ["rejectedInstanceId"] = rejected.instanceId,
                ["absorbedPhysicalChildren"] = 1,
                ["rejectedMutationCount"] = 0
            };
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(inventoryObject);
        }
    }

    private static JObject VerifyBatchUnequipCustody()
    {
        ExactEquipmentGateway gateway = new();
        CombatEquipmentRuntime runtime = CreateRuntime(
            gateway,
            out WorldItemRepository repository);
        CombatEquipmentInstance first = runtime.CreateInstance(
            "weapon:dagger",
            CombatEquipmentQuality.Good);
        CombatEquipmentInstance second = runtime.CreateInstance(
            "weapon:dagger",
            CombatEquipmentQuality.Normal);
        const string owner = "character:a06:unequip";
        Require(runtime.TryAssignToCharacter(owner, first.instanceId, out _)
            && runtime.TryAssignToCharacter(owner, second.instanceId, out _),
            "fixture weapons could not be assigned");

        gateway.FailSpawnOnCall = 2;
        Require(!runtime.TryUnequipSlotToWorld(
                owner,
                CombatEquipmentLoadoutSlot.Weapon,
                new Vector2Int(8, 5),
                out _,
                out string spawnFailure)
            && spawnFailure == "equipment.unequip.physical_spawn_failed",
            "second-child spawn rejection was not surfaced");
        Require(gateway.GetAllStacks().Count == 0,
            "failed batch unequip leaked a physical child");
        CharacterCombatLoadoutProfile afterFailure =
            runtime.GetActiveProfileSnapshot(owner);
        Require(afterFailure.weaponInstanceIds.SequenceEqual(
                new[] { first.instanceId, second.instanceId })
            && runtime.TryGetInstance(
                first.instanceId,
                out CombatEquipmentInstance firstAfterFailure)
            && firstAfterFailure.worldState == CombatEquipmentWorldState.Equipped
            && runtime.TryGetInstance(
                second.instanceId,
                out CombatEquipmentInstance secondAfterFailure)
            && secondAfterFailure.worldState == CombatEquipmentWorldState.Equipped,
            "failed batch unequip partially changed the loadout");

        gateway.ResetFaultsAndCounters();
        Require(runtime.TryUnequipSlotToWorld(
                owner,
                CombatEquipmentLoadoutSlot.Weapon,
                new Vector2Int(8, 5),
                out IReadOnlyList<string> stackIds,
                out string unequipFailure),
            "batch unequip failed: " + unequipFailure);
        Require(stackIds.Count == 2
            && stackIds.Distinct(StringComparer.Ordinal).Count() == 2,
            "batch unequip did not publish two exact child stacks");
        CharacterCombatLoadoutProfile afterSuccess =
            runtime.GetActiveProfileSnapshot(owner);
        Require(afterSuccess.weaponInstanceIds.Count == 0,
            "batch unequip retained weapon loadout entries");
        Require(runtime.TryGetInstance(
                first.instanceId,
                out CombatEquipmentInstance firstLoose)
            && runtime.TryGetInstance(
                second.instanceId,
                out CombatEquipmentInstance secondLoose)
            && firstLoose.worldState == CombatEquipmentWorldState.Loose
            && secondLoose.worldState == CombatEquipmentWorldState.Loose
            && gateway.ContainsExact(
                firstLoose.sourceStackId,
                first.instanceId)
            && gateway.ContainsExact(
                secondLoose.sourceStackId,
                second.instanceId),
            "batch unequip instance/child join is incomplete");

        DungeonCombatEquipmentSaveData equipmentSave = RoundTrip(
            runtime.Capture());
        EquipmentRepositorySaveData repositorySave = RoundTrip(
            CaptureRepository(repository));
        ExactEquipmentGatewaySaveData physicalSave = RoundTrip(
            gateway.Capture());
        ExactEquipmentGateway restoredGateway = new();
        restoredGateway.Restore(physicalSave);
        CombatEquipmentRuntime restored = CreateRuntime(
            restoredGateway,
            repositorySave,
            out _);
        restored.PublishRestoreCandidate(
            restored.BuildRestoreCandidate(equipmentSave));
        Require(restored.TryGetInstance(
                first.instanceId,
                out CombatEquipmentInstance restoredFirst)
            && restored.TryGetInstance(
                second.instanceId,
                out CombatEquipmentInstance restoredSecond)
            && restoredFirst.worldState == CombatEquipmentWorldState.Loose
            && restoredSecond.worldState == CombatEquipmentWorldState.Loose
            && restoredGateway.ContainsExact(
                restoredFirst.sourceStackId,
                restoredFirst.instanceId)
            && restoredGateway.ContainsExact(
                restoredSecond.sourceStackId,
                restoredSecond.instanceId),
            "batch unequip exact custody did not survive restore");

        return new JObject
        {
            ["sameDefinition"] = first.definitionId,
            ["instanceIds"] = new JArray(first.instanceId, second.instanceId),
            ["physicalChildStackIds"] = new JArray(stackIds),
            ["failedBatchChildrenAfterRollback"] = 0,
            ["restoredChildren"] = 2
        };
    }

    private static JObject VerifyReloadAndRestore()
    {
        ExactEquipmentGateway gateway = new();
        CombatEquipmentRuntime runtime = CreateRuntime(
            gateway,
            out WorldItemRepository repository);
        CombatEquipmentInstance weapon = runtime.CreateInstance(
            "weapon:repeating-crossbow",
            CombatEquipmentQuality.Normal);
        GameObject inventoryObject = new("A06_ReloadInventory");
        GameObject restoredInventoryObject = new("A06_ReloadInventory_Restored");
        try
        {
            CharacterCarryInventory inventory =
                inventoryObject.AddComponent<CharacterCarryInventory>();
            inventory.Restore(new CharacterCarryInventorySaveData
            {
                items = new List<CharacterCarriedItemSaveData>
                {
                    new()
                    {
                        carriedStackId = "carry:a06:steel",
                        sourceStackId = "stack:a06:steel",
                        itemId = "ammo:bolt-steel",
                        quantity = 2
                    },
                    new()
                    {
                        carriedStackId = "carry:a06:incendiary",
                        sourceStackId = "stack:a06:incendiary",
                        itemId = "ammo:incendiary-bolt",
                        quantity = 3
                    }
                }
            });
            Require(runtime.TryReloadFromInventory(
                    weapon.instanceId,
                    inventory,
                    out ItemDefinitionId firstType,
                    out int firstCount)
                && firstType.Equals((ItemDefinitionId)"ammo:bolt-steel")
                && firstCount == 2
                && inventory.CountItem("ammo:bolt-steel") == 0
                && inventory.CountItem("ammo:incendiary-bolt") == 3,
                "first magazine load did not consume exact quantity/type");
            Require(!runtime.TryReloadFromInventory(
                    weapon.instanceId,
                    inventory,
                    out _,
                    out _)
                && inventory.CountItem("ammo:incendiary-bolt") == 3,
                "partial magazine accepted a different ammunition type");

            DungeonCombatEquipmentSaveData equipmentSave = RoundTrip(
                runtime.Capture());
            EquipmentRepositorySaveData repositorySave = RoundTrip(
                CaptureRepository(repository));
            CharacterCarryInventorySaveData carrySave = RoundTrip(
                inventory.Capture());
            CombatEquipmentRuntime restored = CreateRuntime(
                new ExactEquipmentGateway(),
                repositorySave,
                out _);
            restored.PublishRestoreCandidate(
                restored.BuildRestoreCandidate(equipmentSave));
            CharacterCarryInventory restoredInventory =
                restoredInventoryObject.AddComponent<CharacterCarryInventory>();
            restoredInventory.Restore(carrySave);
            Require(restored.TryGetInstance(
                    weapon.instanceId,
                    out CombatEquipmentInstance restoredWeapon)
                && restoredWeapon.loadedAmmunition.ammunitionItemId
                    == "ammo:bolt-steel"
                && restoredWeapon.loadedAmmunition.remaining == 2
                && restoredInventory.CountItem("ammo:incendiary-bolt") == 3,
                "magazine/carry state did not survive restore");
            ItemDefinitionId secondType = default;
            int secondCount = 0;
            Require(restored.TryConsumeLoadedAmmo(weapon.instanceId)
                && restored.TryConsumeLoadedAmmo(weapon.instanceId)
                && restored.TryReloadFromInventory(
                    weapon.instanceId,
                    restoredInventory,
                    out secondType,
                    out secondCount)
                && secondType.Equals(
                    (ItemDefinitionId)"ammo:incendiary-bolt")
                && secondCount == 3
                && restoredInventory.CountItem("ammo:incendiary-bolt") == 0,
                "restored magazine did not resume exact consumption");

            return new JObject
            {
                ["weaponInstanceId"] = weapon.instanceId,
                ["firstAmmunitionItemId"] = firstType.Value,
                ["firstConsumed"] = firstCount,
                ["restoredMagazineCount"] = 2,
                ["secondAmmunitionItemId"] = secondType.Value,
                ["secondConsumed"] = secondCount,
                ["mixedTypeRejectedWithoutConsumption"] = true
            };
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(inventoryObject);
            UnityEngine.Object.DestroyImmediate(restoredInventoryObject);
        }
    }

    private static void VerifyPowerAndRepositoryReplacement()
    {
        CombatEquipmentRuntime runtime = CreateRuntime(
            new ExactEquipmentGateway(), out WorldItemRepository repository);
        CombatEquipmentInstance powered = runtime.CreateInstance(
            "weapon:powered-striking-gauntlet", CombatEquipmentQuality.Normal);
        CombatEquipmentInstance ordinary = runtime.CreateInstance(
            "weapon:dagger", CombatEquipmentQuality.Normal);
        Require(runtime.TryRestorePower(powered.instanceId, 100f)
            && runtime.TryConsumePower(powered.instanceId, 30f)
            && runtime.TryGetInstance(powered.instanceId, out CombatEquipmentInstance charged)
            && Mathf.Approximately(charged.powerCharge, 70f),
            "Powered equipment did not charge and consume through its command owner.");
        JToken beforeRejected = JToken.FromObject(CaptureRepository(repository));
        Require(!runtime.TryConsumePower(powered.instanceId, 0f)
            && !runtime.TryRestorePower(powered.instanceId, 0f)
            && !runtime.TryConsumePower("equipment:qa:missing", 10f)
            && !runtime.TryConsumePower(ordinary.instanceId, 10f)
            && !runtime.TryRestorePower(ordinary.instanceId, 10f)
            && JToken.DeepEquals(beforeRejected, JToken.FromObject(CaptureRepository(repository))),
            "Rejected power command changed equipment repository state.");

        DungeonCombatEquipmentSaveData equipmentSave = RoundTrip(runtime.Capture());
        CombatEquipmentRuntime restored = CreateRuntime(
            new ExactEquipmentGateway(), RoundTrip(CaptureRepository(repository)),
            out WorldItemRepository restoredRepository);
        restored.PublishRestoreCandidate(restored.BuildRestoreCandidate(equipmentSave));
        Require(restored.TryGetInstance(powered.instanceId, out CombatEquipmentInstance restoredPower)
            && Mathf.Approximately(restoredPower.powerCharge, 70f),
            "Power charge did not survive the physical/equipment JSON restore.");

        CombatEquipmentInstance retired = repository.EquipmentInstances[powered.instanceId];
        WorldItemRepositoryState candidate = repository.CreateDetachedState(
            Array.Empty<WorldItemStackRecord>(),
            restoredRepository.EquipmentInstances.ToDictionary(value => value.Key, value => value.Value),
            restoredRepository.EquipmentModules.ToDictionary(value => value.Key, value => value.Value),
            nextHaulOperationSequence: 1L);
        repository.ReplaceState(candidate);
        Require(!ReferenceEquals(retired, repository.EquipmentInstances[powered.instanceId])
            && runtime.TryConsumePower(powered.instanceId, 10f)
            && runtime.TryGetInstance(powered.instanceId, out CombatEquipmentInstance current)
            && Mathf.Approximately(current.powerCharge, 60f)
            && Mathf.Approximately(retired.powerCharge, 70f)
            && runtime.TryRestorePower(powered.instanceId, 10f)
            && runtime.TryGetInstance(powered.instanceId, out current)
            && Mathf.Approximately(current.powerCharge, 70f),
            "Supply commands retained the retired repository after state publication.");
    }

    private static CharacterCarryInventorySaveData Carry(
        string sourceStackId,
        string itemId,
        string itemInstanceId) => new()
    {
        items = new List<CharacterCarriedItemSaveData>
        {
            new()
            {
                carriedStackId = "carry:" + sourceStackId,
                sourceStackId = sourceStackId,
                ownerOperationId = "a06:pickup:" + itemInstanceId,
                itemInstanceId = itemInstanceId,
                itemId = itemId,
                quantity = 1
            }
        }
    };

    private static CombatEquipmentRuntime CreateRuntime(
        IEquipmentPhysicalItemGateway gateway,
        out WorldItemRepository repository) => CreateRuntime(
        gateway,
        null,
        out repository);

    private static CombatEquipmentRuntime CreateRuntime(
        IEquipmentPhysicalItemGateway gateway,
        EquipmentRepositorySaveData repositorySave,
        out WorldItemRepository repository)
    {
        IGameContentCatalog content = new ResourceGameContentCatalog(
            new UnityGameContentRootLoader());
        repository = new WorldItemRepository(
            new GuidPersistentIdGenerator(),
            new DungeonRuntimeAggregateRootStore());
        if (repositorySave != null)
            RestoreRepository(repository, repositorySave);
        return CombatEquipmentEditorTestFactory.Create(
            new ResourceCombatEquipmentCatalog(content),
            repository,
            new CharacterCarryInventoryRegistry(),
            materialCatalog: new ResourceEconomyContentCatalog(content),
            evolutionModules: EmptyEvolutionModuleRegistry.Instance,
            researchProvider: EditorAllResearchRuntimeProvider.Instance,
            moduleCatalog: new ResourceEquipmentModuleCatalog(content),
            itemStackRuntime: gateway,
            calendar: new EditorFixedGameCalendar(137, 9));
    }

    private static EquipmentRepositorySaveData CaptureRepository(
        WorldItemRepository repository) => new()
    {
        uniqueItems = repository.EquipmentInstances.Values
            .Where(value => value != null)
            .OrderBy(value => value.instanceId, StringComparer.Ordinal)
            .Select(value => new UniqueItemInstanceSaveData
            {
                itemInstanceId = value.instanceId,
                definitionId = PhysicalItemIds.ForEquipment(value.definitionId),
                components = new List<ItemInstanceComponentSaveData>
                {
                    EquipmentItemStateCodec.Encode(value)
                }
            })
            .ToList()
    };

    private static void RestoreRepository(
        WorldItemRepository repository,
        EquipmentRepositorySaveData saveData)
    {
        foreach (UniqueItemInstanceSaveData saved in saveData?.uniqueItems
                     ?? new List<UniqueItemInstanceSaveData>())
        {
            ItemInstanceComponentSaveData component = saved?.components?
                .SingleOrDefault(value => value != null
                    && value.componentTypeId == ItemInstanceComponentIds.Equipment);
            EquipmentPhysicalStatePayload payload = null;
            string failureReason = "missing equipment component";
            bool decoded = saved != null
                && component != null
                && EquipmentItemStateCodec.TryDecodeFull(
                    component,
                    out payload,
                    out failureReason);
            Require(decoded
                && string.Equals(
                    saved.itemInstanceId,
                    payload.equipment.instanceId,
                    StringComparison.Ordinal)
                && string.Equals(
                    saved.definitionId,
                    PhysicalItemIds.ForEquipment(payload.equipment.definitionId),
                    StringComparison.Ordinal),
                "equipment repository restore failed: " + failureReason);
            repository.EquipmentInstances.Add(
                payload.equipment.instanceId,
                payload.equipment.Clone());
            foreach (EquipmentModuleInstance module in payload.attachedModules
                         ?? new List<EquipmentModuleInstance>())
            {
                repository.EquipmentModules.Add(module.instanceId, module.Clone());
            }
        }
    }

    private static T RoundTrip<T>(T value) =>
        JsonUtility.FromJson<T>(JsonUtility.ToJson(value));

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    private static void WriteReport(string output, JObject report)
    {
        string path = Path.Combine(output, "report.json");
        if (File.Exists(path))
            throw new IOException("A06 report already exists: " + path);
        using FileStream stream = new(path, FileMode.CreateNew);
        using StreamWriter writer = new(stream);
        writer.Write(report.ToString(Formatting.Indented));
    }

    [Serializable]
    private sealed class EquipmentRepositorySaveData
    {
        public List<UniqueItemInstanceSaveData> uniqueItems = new();
    }

    [Serializable]
    private sealed class ExactEquipmentGatewaySaveData
    {
        public int nextStackOrdinal;
        public List<ExactEquipmentStackSaveData> stacks = new();
    }

    [Serializable]
    private sealed class ExactEquipmentStackSaveData
    {
        public string stackId = string.Empty;
        public string itemId = string.Empty;
        public string itemInstanceId = string.Empty;
        public int quantity;
        public int state;
        public int x;
        public int y;
        public string destinationId = string.Empty;
        public List<ItemInstanceComponentSaveData> components = new();
    }

    private sealed class ExactEquipmentGateway : IEquipmentPhysicalItemGateway
    {
        private readonly List<WorldItemStackSnapshot> stacks = new();
        private int nextStackOrdinal;
        private int spawnCalls;

        internal int FailSpawnOnCall { get; set; }
        internal bool FailNextAbsorb { get; set; }

        internal void ResetFaultsAndCounters()
        {
            FailSpawnOnCall = 0;
            FailNextAbsorb = false;
            spawnCalls = 0;
        }

        internal string SeedUnique(
            string itemId,
            string itemInstanceId,
            Vector2Int position,
            WorldItemStackState state)
        {
            int originalFail = FailSpawnOnCall;
            FailSpawnOnCall = 0;
            bool spawned = SpawnExistingUniqueItemAt(
                itemId,
                (ItemInstanceId)itemInstanceId,
                position,
                state,
                string.Empty,
                out string stackId);
            FailSpawnOnCall = originalFail;
            Require(spawned, "fixture could not seed unique equipment");
            return stackId;
        }

        internal bool ContainsStack(string stackId) => stacks.Any(value =>
            string.Equals(value.StackId, stackId, StringComparison.Ordinal));

        internal bool ContainsExact(string stackId, string itemInstanceId) =>
            stacks.Any(value => value.Quantity == 1
                && string.Equals(value.StackId, stackId, StringComparison.Ordinal)
                && string.Equals(
                    value.ItemInstanceId,
                    itemInstanceId,
                    StringComparison.Ordinal));

        internal ExactEquipmentGatewaySaveData Capture() => new()
        {
            nextStackOrdinal = nextStackOrdinal,
            stacks = stacks.Select(value => new ExactEquipmentStackSaveData
            {
                stackId = value.StackId,
                itemId = value.ItemId,
                itemInstanceId = value.ItemInstanceId,
                quantity = value.Quantity,
                state = (int)value.State,
                x = value.Position.x,
                y = value.Position.y,
                destinationId = value.DestinationId,
                components = (value.Components
                        ?? Array.Empty<ItemInstanceComponentSaveData>())
                    .Where(component => component != null)
                    .Select(component => component.Clone())
                    .ToList()
            }).ToList()
        };

        internal void Restore(ExactEquipmentGatewaySaveData snapshot)
        {
            stacks.Clear();
            nextStackOrdinal = Math.Max(0, snapshot?.nextStackOrdinal ?? 0);
            foreach (ExactEquipmentStackSaveData value in snapshot?.stacks
                         ?? new List<ExactEquipmentStackSaveData>())
            {
                if (value == null)
                    continue;
                stacks.Add(new WorldItemStackSnapshot
                {
                    StackId = value.stackId,
                    ItemId = value.itemId,
                    ItemInstanceId = value.itemInstanceId,
                    Quantity = value.quantity,
                    State = (WorldItemStackState)value.state,
                    Position = new Vector2Int(value.x, value.y),
                    DestinationId = value.destinationId,
                    Components = (value.components
                            ?? new List<ItemInstanceComponentSaveData>())
                        .Where(component => component != null)
                        .Select(component => component.Clone())
                        .ToArray()
                });
            }
        }

        public bool SpawnItemAt(
            string itemId,
            int amount,
            Vector2Int position,
            WorldItemStackState state,
            string destinationId,
            out int spawned)
        {
            spawned = 0;
            return false;
        }

        public bool SpawnItemAtWithComponents(
            string itemId,
            int amount,
            Vector2Int position,
            WorldItemStackState state,
            string destinationId,
            IReadOnlyList<ItemInstanceComponentSaveData> components,
            out int spawned)
        {
            spawned = 0;
            return false;
        }

        public bool SpawnExistingUniqueItemAt(
            string itemId,
            ItemInstanceId itemInstanceId,
            Vector2Int position,
            WorldItemStackState state,
            string destinationId,
            out string stackId)
        {
            stackId = string.Empty;
            spawnCalls++;
            if (FailSpawnOnCall > 0 && spawnCalls == FailSpawnOnCall)
                return false;
            if (string.IsNullOrWhiteSpace(itemId)
                || !itemInstanceId.IsValid
                || stacks.Any(value => string.Equals(
                    value.ItemInstanceId,
                    itemInstanceId.Value,
                    StringComparison.Ordinal)))
            {
                return false;
            }
            stackId = $"stack:a06:{++nextStackOrdinal:D4}";
            stacks.Add(new WorldItemStackSnapshot
            {
                StackId = stackId,
                ItemId = itemId,
                ItemInstanceId = itemInstanceId.Value,
                Quantity = 1,
                State = state,
                Position = position,
                DestinationId = destinationId ?? string.Empty,
                Components = Array.Empty<ItemInstanceComponentSaveData>()
            });
            return true;
        }

        public bool TryAbsorbUniqueItemStack(
            string stackId,
            ItemInstanceId expectedInstanceId)
        {
            if (FailNextAbsorb)
            {
                FailNextAbsorb = false;
                return false;
            }
            int index = stacks.FindIndex(value => value.Quantity == 1
                && string.Equals(value.StackId, stackId, StringComparison.Ordinal)
                && string.Equals(
                    value.ItemInstanceId,
                    expectedInstanceId.Value,
                    StringComparison.Ordinal));
            if (index < 0)
                return false;
            stacks.RemoveAt(index);
            return true;
        }

        public bool TryRequestItemDelivery(
            string itemId,
            int amount,
            Vector2Int destinationPosition,
            string destinationId,
            out int requested,
            out string failureReason)
        {
            requested = 0;
            failureReason = "a06-fixture-delivery-unavailable";
            return false;
        }

        public IReadOnlyList<WorldItemStackSnapshot> GetAllStacks() =>
            stacks.Select(Clone).ToArray();

        public bool TryConsumeFacilityItemBuffer(
            string destinationId,
            IReadOnlyDictionary<string, int> costs,
            out string failureReason)
        {
            failureReason = "a06-fixture-buffer-unavailable";
            return false;
        }

        public bool DeleteStack(string stackId)
        {
            int index = stacks.FindIndex(value => string.Equals(
                value.StackId,
                stackId,
                StringComparison.Ordinal));
            if (index < 0)
                return false;
            stacks.RemoveAt(index);
            return true;
        }

        public bool TryConsumeStackQuantity(
            string stackId,
            int quantity,
            out WorldItemStackSnapshot consumed)
        {
            consumed = null;
            WorldItemStackSnapshot source = stacks.FirstOrDefault(value =>
                string.Equals(value.StackId, stackId, StringComparison.Ordinal));
            if (source == null || quantity <= 0 || source.Quantity < quantity)
                return false;
            consumed = Clone(source);
            consumed.Quantity = quantity;
            source.Quantity -= quantity;
            if (source.Quantity == 0)
                stacks.Remove(source);
            return true;
        }

        public bool TryCommitBatchPhysicalDisposition(
            IReadOnlyList<PhysicalItemTransformInput> inputs,
            PhysicalItemDispositionKind kind,
            string operationId,
            string reasonCode,
            out PhysicalItemBatchDispositionReceipt receipt,
            out string failureReason)
        {
            receipt = default;
            failureReason = "a06-fixture-disposition-unavailable";
            return false;
        }

        public bool TryCommitPendingBatchPhysicalDisposition(
            IReadOnlyList<PhysicalItemTransformInput> inputs,
            PhysicalItemDispositionKind kind,
            string operationId,
            string reasonCode,
            out PhysicalItemBatchDispositionReceipt receipt,
            out string failureReason) => TryCommitBatchPhysicalDisposition(
            inputs,
            kind,
            operationId,
            reasonCode,
            out receipt,
            out failureReason);

        public bool TryGetPendingBatchPhysicalDisposition(
            string operationId,
            out PhysicalItemBatchDispositionReceipt receipt)
        {
            receipt = default;
            return false;
        }

        public bool AcknowledgeBatchPhysicalDisposition(
            string commitId,
            out string failureReason)
        {
            failureReason = "a06-fixture-disposition-unavailable";
            return false;
        }

        public bool TrySetInstanceComponent(
            string stackId,
            ItemInstanceComponentSaveData component)
        {
            WorldItemStackSnapshot stack = stacks.FirstOrDefault(value =>
                string.Equals(value.StackId, stackId, StringComparison.Ordinal));
            if (stack == null || component == null)
                return false;
            List<ItemInstanceComponentSaveData> components = (stack.Components
                    ?? Array.Empty<ItemInstanceComponentSaveData>())
                .Where(value => value != null)
                .Select(value => value.Clone())
                .ToList();
            components.RemoveAll(value => string.Equals(
                value.componentTypeId,
                component.componentTypeId,
                StringComparison.Ordinal));
            components.Add(component.Clone());
            stack.Components = components;
            return true;
        }

        public bool TryRemoveInstanceComponent(
            string stackId,
            string componentTypeId)
        {
            WorldItemStackSnapshot stack = stacks.FirstOrDefault(value =>
                string.Equals(value.StackId, stackId, StringComparison.Ordinal));
            if (stack == null)
                return false;
            List<ItemInstanceComponentSaveData> components = (stack.Components
                    ?? Array.Empty<ItemInstanceComponentSaveData>())
                .Where(value => value != null
                    && !string.Equals(
                        value.componentTypeId,
                        componentTypeId,
                        StringComparison.Ordinal))
                .Select(value => value.Clone())
                .ToList();
            stack.Components = components;
            return true;
        }

        public int ReleaseStacksByDestination(
            string destinationId,
            Vector2Int releasePosition)
        {
            WorldItemStackSnapshot[] released = stacks.Where(value =>
                    string.Equals(
                        value.DestinationId,
                        destinationId,
                        StringComparison.Ordinal))
                .ToArray();
            foreach (WorldItemStackSnapshot value in released)
                stacks.Remove(value);
            return released.Sum(value => value.Quantity);
        }

        private static WorldItemStackSnapshot Clone(
            WorldItemStackSnapshot value) => new()
        {
            StackId = value.StackId,
            ItemId = value.ItemId,
            ItemInstanceId = value.ItemInstanceId,
            Quantity = value.Quantity,
            ReservedQuantity = value.ReservedQuantity,
            State = value.State,
            Position = value.Position,
            DestinationId = value.DestinationId,
            Components = (value.Components
                    ?? Array.Empty<ItemInstanceComponentSaveData>())
                .Where(component => component != null)
                .Select(component => component.Clone())
                .ToArray()
        };
    }
}
#endif
