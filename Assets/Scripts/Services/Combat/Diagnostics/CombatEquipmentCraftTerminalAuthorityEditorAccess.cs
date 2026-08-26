#if UNITY_EDITOR
using System;
using System.Linq;

public static class CombatEquipmentCraftTerminalAuthorityEditorAccess
{
    public static void AddOrder(
        CombatEquipmentRuntimeStateStore stateStore,
        CombatEquipmentCraftOrderSaveData order)
    {
        if (stateStore == null || order == null)
            throw new ArgumentNullException(stateStore == null
                ? nameof(stateStore)
                : nameof(order));
        CombatEquipmentRuntimeState next = stateStore.Current.Clone();
        if (next.CraftOrders.Any(value => value != null && string.Equals(
                value.orderId,
                order.orderId,
                StringComparison.Ordinal)))
            throw new InvalidOperationException("Duplicate craft terminal test order.");
        next.CraftOrders.Add(order.Clone());
        stateStore.Replace(next);
    }

    public static void ReplaceOrder(
        CombatEquipmentRuntimeStateStore stateStore,
        CombatEquipmentCraftOrderSaveData order)
    {
        if (stateStore == null || order == null)
            throw new ArgumentNullException(stateStore == null
                ? nameof(stateStore)
                : nameof(order));
        CombatEquipmentRuntimeState next = stateStore.Current.Clone();
        int index = next.CraftOrders.FindIndex(value => value != null
            && string.Equals(value.orderId, order.orderId,
                StringComparison.Ordinal));
        if (index < 0)
            throw new InvalidOperationException("Craft terminal test order missing.");
        next.CraftOrders[index] = order.Clone();
        stateStore.Replace(next);
    }

    public static CombatEquipmentCraftOrderSaveData[] CaptureOrders(
        CombatEquipmentRuntimeStateStore stateStore) => (stateStore
            ?? throw new ArgumentNullException(nameof(stateStore))).Current
        .CraftOrders.Where(value => value != null)
        .Select(value => value.Clone()).ToArray();

    public static CombatEquipmentCraftTerminalEffectSaveData[] CaptureEffects(
        CombatEquipmentRuntimeStateStore stateStore) => (stateStore
            ?? throw new ArgumentNullException(nameof(stateStore))).Current
        .CraftTerminalEffects.Values.Where(value => value != null)
        .OrderBy(value => value.sourceId, StringComparer.Ordinal)
        .Select(value => value.Clone()).ToArray();
}
#endif
