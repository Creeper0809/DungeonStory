using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using DungeonStory.Foundation;
using UnityEngine;
using static R31StructureRemediationBatch;

// Controlled presenter calls with snapshot ports. This does not exercise the
// maintenance owner's save/restore lifecycle or natural UI interaction.
public static class R31RepairSelectionDebugScenarios
{
    public static void RunContracts()
    {
        const string characterId = "character:r31:repair";
        const string armorId = "r31-repair-armor-a";
        const string shieldId = "r31-repair-shield-b";
        GameObject actorObject = new GameObject("R31RepairSelection");
        actorObject.SetActive(false);
        try
        {
            CharacterActor actor = actorObject.AddComponent<CharacterActor>();
            CharacterIdentity identity = actorObject.GetComponent<CharacterIdentity>();
            Require(identity != null, "R31 repair fixture requires the actual identity component.");
            identity.SetPersistentId(characterId);
            FieldInfo identityField = typeof(CharacterActor).GetField(
                "identity", BindingFlags.Instance | BindingFlags.NonPublic)
                ?? throw new MissingFieldException(typeof(CharacterActor).FullName, "identity");
            identityField.SetValue(actor, identity);
            Require(!actorObject.activeInHierarchy && actor.Identity.PersistentId == characterId,
                "R31 repair fixture must remain inactive with its requested persistent identity.");

            CharacterCombatLoadoutProfile profile = new CharacterCombatLoadoutProfile
            {
                profileId = CombatLoadoutPresetIds.Combat,
                armorInstanceIds = new List<string> { armorId },
                shieldInstanceId = shieldId
            };
            Dictionary<string, CombatEquipmentInstance> instances = new(StringComparer.Ordinal)
            {
                [armorId] = new CombatEquipmentInstance
                {
                    instanceId = armorId,
                    durabilityRatio = 0.25f,
                    ownerCharacterId = characterId,
                    worldState = CombatEquipmentWorldState.Equipped
                },
                [shieldId] = new CombatEquipmentInstance
                {
                    instanceId = shieldId,
                    durabilityRatio = 0.5f,
                    ownerCharacterId = characterId,
                    worldState = CombatEquipmentWorldState.Equipped
                }
            };
            List<CombatEquipmentRepairOrder> orders = new();
            List<string> requestedIds = new();
            int orderQueryCount = 0;
            int profileQueryCount = 0;
            ICombatEquipmentRuntime equipment = StrictProxy<ICombatEquipmentRuntime>((method, arguments) =>
            {
                if (method.Name == nameof(ICombatEquipmentRuntime.GetActiveProfileSnapshot))
                {
                    Require((string)arguments[0] == characterId,
                        "R31 repair requested another character's profile.");
                    profileQueryCount++;
                    return profile.Clone();
                }
                if (method.Name == nameof(ICombatEquipmentRuntime.TryGetInstance))
                {
                    Require(instances.TryGetValue((string)arguments[0], out CombatEquipmentInstance instance),
                        "R31 repair requested an equipment instance outside the fixture.");
                    arguments[1] = instance.Clone();
                    return true;
                }
                return UnexpectedCall(method, arguments);
            });
            ICombatEquipmentMaintenanceRuntime maintenance =
                StrictProxy<ICombatEquipmentMaintenanceRuntime>((method, arguments) =>
                {
                    if (method.Name == "get_Orders")
                    {
                        orderQueryCount++;
                        return orders.Select(order => order.Clone()).ToArray();
                    }
                    if (method.Name == nameof(ICombatEquipmentMaintenanceRuntime.TryRequestManualRepair))
                    {
                        string instanceId = (string)arguments[0];
                        Require(instances.ContainsKey(instanceId),
                            "R31 repair requested an unknown equipment instance.");
                        Require(!orders.Any(order => string.Equals(
                                order.equipmentInstanceId, instanceId, StringComparison.Ordinal)),
                            "R31 repair selected an equipment instance that already has an order.");
                        requestedIds.Add(instanceId);
                        orders.Add(new CombatEquipmentRepairOrder
                        {
                            orderId = "r31-repair-order-" + requestedIds.Count,
                            equipmentInstanceId = instanceId,
                            originalOwnerCharacterId = characterId,
                            state = CombatEquipmentRepairOrderState.PendingCombatEnd,
                            manuallyRequested = true
                        });
                        arguments[1] = "r31-repair-requested:" + instanceId;
                        return true;
                    }
                    return UnexpectedCall(method, arguments);
                });
            CharacterSummaryCombatPresenter presenter = new CharacterSummaryCombatPresenter(
                StrictProxy<ICharacterBodyHealthQuery>(UnexpectedCall),
                equipment,
                StrictProxy<ICombatEquipmentCatalog>(UnexpectedCall),
                maintenance,
                new GameEventBus(),
                StrictProxy<ICharacterPerformanceQuery>(UnexpectedCall));

            presenter.RequestRepair(actor);
            Require(requestedIds.SequenceEqual(new[] { armorId }),
                "R31 repair must first request the lowest-durability armor A.");
            Require(orderQueryCount == 1 && profileQueryCount == 1,
                "R31 repair must read one orders snapshot and one profile per request.");
            Require(orders.Count == 1 && orders[0].state == CombatEquipmentRepairOrderState.PendingCombatEnd,
                "R31 repair must preserve the first pending-combat order.");

            List<CombatEquipmentRepairOrder> beforeRoundTrip = orders;
            string json = JsonUtility.ToJson(new CombatEquipmentMaintenanceSaveData { orders = orders });
            CombatEquipmentMaintenanceSaveData restored =
                JsonUtility.FromJson<CombatEquipmentMaintenanceSaveData>(json);
            Require(restored?.orders != null && restored.orders.Count == 1,
                "R31 repair DTO JSON round trip lost the first order.");
            orders = restored.orders;
            Require(!ReferenceEquals(orders, beforeRoundTrip)
                    && !ReferenceEquals(orders[0], beforeRoundTrip[0])
                    && orders[0].equipmentInstanceId == armorId
                    && orders[0].originalOwnerCharacterId == characterId
                    && orders[0].state == CombatEquipmentRepairOrderState.PendingCombatEnd,
                "R31 repair DTO round trip must supply a new list with the same pending armor order.");

            presenter.RequestRepair(actor);
            Require(requestedIds.SequenceEqual(new[] { armorId, shieldId }),
                "R31 repair must skip restored pending armor A and request shield B next.");
            Require(orderQueryCount == 2 && profileQueryCount == 2,
                "R31 repair must query the new orders snapshot once on the second request.");
            Require(orders.Count == 2
                    && orders.All(order => order.state == CombatEquipmentRepairOrderState.PendingCombatEnd),
                "R31 repair must retain distinct pending orders for both equipment instances.");
            Require(instances[armorId].durabilityRatio == 0.25f
                    && instances[shieldId].durabilityRatio == 0.5f
                    && profile.armorInstanceIds.SequenceEqual(new[] { armorId })
                    && profile.shieldInstanceId == shieldId,
                "R31 repair selection must not change the source loadout or durability.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(actorObject);
        }
        Debug.Log("R31_REPAIR_SELECTION=PASS; boundary=controlled presenter and DTO JSON round trip");
    }

    private static object UnexpectedCall(MethodInfo method, object[] arguments) =>
        throw new InvalidOperationException(
            "R31 repair unexpected port call: " + method.DeclaringType?.FullName + "." + method.Name);
}
