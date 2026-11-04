#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEngine;

public static partial class RestoredCarrySubsetReleaseDebugScenarios
{
    public static void VerifyCarriedDecayDeathForPlayMode()
    {
        if (!Application.isPlaying)
            throw new InvalidOperationException(
                "Carried decay death recovery requires PlayMode.");

        using Fixture fixture = Fixture.Create();
        Cargo cargo = fixture.CreateCommittedCargo(
            quantity: 1,
            components: new[] { FoodFreshnessComponentCodec.Create(100d, true) });
        long expectedMass = fixture.MassOf(cargo.CarriedStackId);
        CharacterCarryInventorySaveData inventory = fixture.Inventory.Capture();
        CharacterCarriedItemSaveData carried = inventory.items.Single(value =>
            string.Equals(value.carriedStackId, cargo.CarriedStackId, StringComparison.Ordinal));
        Require(FoodFreshnessComponentCodec.TryRead(
                    carried.components, out double initialFreshness, out bool initialPreserved)
                && initialFreshness == 100d && initialPreserved,
            "Carried fixture did not start with the expected freshness.");
        int freshnessIndex = carried.components.FindIndex(component =>
            string.Equals(component.componentTypeId, ItemInstanceComponentIds.Freshness,
                StringComparison.Ordinal));
        Require(freshnessIndex >= 0, "Carried freshness component was missing.");
        carried.components[freshnessIndex] = FoodFreshnessComponentCodec.Create(75d, true);
        fixture.Inventory.Restore(inventory);
        WorldItemStackSnapshot physicalBefore = fixture.RequireStack(cargo.CarriedStackId);
        Require(FoodFreshnessComponentCodec.TryRead(
                    physicalBefore.Components, out double physicalFreshness, out bool physicalPreserved)
                && physicalFreshness == 100d && physicalPreserved,
            "Physical fixture was not kept at the original freshness.");

        fixture.BindActivePlan(cargo);
        fixture.Actor.Die(CharacterDeathCauseCode.Unknown, "qa-carried-decay-death");

        Require(fixture.Actor.CurrentLifecycleState == CharacterLifecycleState.Despawned,
            "Controlled death did not reach the despawned lifecycle state.");
        Require(fixture.Inventory.Items.Count == 0,
            "Controlled death retained logical carried cargo.");
        WorldItemStackSnapshot dropped = fixture.RequireStack(cargo.CarriedStackId);
        Require(dropped.State == WorldItemStackState.Loose
                && dropped.Position == ActorCell
                && dropped.Quantity == cargo.Quantity
                && fixture.MassOf(dropped.StackId) == expectedMass,
            "Death recovery changed stack identity, position, quantity, or mass.");
        Require(FoodFreshnessComponentCodec.TryRead(
                    dropped.Components, out double remaining, out bool preserved)
                && remaining == 75d && preserved,
            "Death recovery did not preserve the carried freshness decay.");
        Require(dropped.DropDisposition == WorldItemDropDisposition.TransientCarryRecoveryDrop
                && dropped.RecoveryInterruptionKind == WorldItemCarryInterruptionKind.Dead
                && string.Equals(dropped.RecoveryOwnerOperationId, cargo.OperationId, StringComparison.Ordinal)
                && string.Equals(dropped.RecoverySourceStackId, cargo.SourceStackId, StringComparison.Ordinal)
                && string.Equals(dropped.RecoveryCarrierPersistentId, ActorId, StringComparison.Ordinal)
                && dropped.RecoveryDeadlineGameTime > dropped.DroppedAtGameTime,
            "Death recovery lost typed recovery provenance.");
        fixture.RequireOperationAuthorityAbsent(cargo.OperationId);
        fixture.RequirePhysicalRestoreExact(cargo, expectedMass);
        WorldItemStackSnapshot restored = fixture.RequireStack(cargo.CarriedStackId);
        Require(FoodFreshnessComponentCodec.TryRead(
                    restored.Components, out double restoredFreshness, out bool restoredPreserved)
                && restoredFreshness == 75d && restoredPreserved,
            "Physical restore changed recovered carried freshness.");
    }
}
#endif
