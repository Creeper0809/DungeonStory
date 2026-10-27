#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using UnityEngine;

public static class R28AccessAndEcologySnapshotDebugScenarios
{
    public static void RunOrThrow()
    {
        VerifyDoor();
        ResourceGameContentCatalog content = new(new UnityGameContentRootLoader());
        CropDefinitionSO crop = content.GetAll<CropDefinitionSO>()
            .OrderBy(value => value.CropId, StringComparer.Ordinal).First();
        VerifyEcology(content, crop);
        VerifyCarry(crop);
        Debug.Log("R28_ACCESS_ECOLOGY_CARRY_SNAPSHOTS=PASS");
    }

    private static void VerifyDoor()
    {
        int changes = 0;
        DoorAccessStateModule module = new(() => changes++);
        Require(module.SetIndividualRule("character:allowed", DoorAccessIndividualRule.Allow)
            && module.SetIndividualRule("character:denied", DoorAccessIndividualRule.Deny),
            "Door fixture commands did not establish individual rules.");
        string before = module.CaptureState();
        long revision = module.Revision;
        int changesBefore = changes;
        DoorAccessPolicyState snapshot = module.State;
        snapshot.AllowedGroups = DoorAccessGroup.None;
        ((IList<string>)snapshot.IndividuallyAllowedIds).Clear();
        ((IList<string>)snapshot.IndividuallyDeniedIds).Add("character:injected");
        Require(module.State.IsGroupAllowed(DoorAccessGroup.Customer)
            && module.State.GetIndividualRule("character:allowed") == DoorAccessIndividualRule.Allow
            && module.State.GetIndividualRule("character:injected") == DoorAccessIndividualRule.GroupDefault
            && module.CaptureState() == before
            && module.Revision == revision && changes == changesBefore,
            "Door query mutation reached the owner, revision, or change callback.");

        DoorAccessPolicyState beforeCommand = module.State;
        Require(module.SetGroupAllowed(DoorAccessGroup.Customer, false)
            && !module.State.IsGroupAllowed(DoorAccessGroup.Customer)
            && beforeCommand.IsGroupAllowed(DoorAccessGroup.Customer)
            && module.Revision == revision + 1 && changes == changesBefore + 1,
            "Door owner command did not publish exactly once or changed an earlier snapshot.");
        Require(!module.SetGroupAllowed(DoorAccessGroup.Customer, false)
            && module.Revision == revision + 1 && changes == changesBefore + 1,
            "Door no-op command changed revision or notified observers.");

        string saved = module.CaptureState();
        DoorAccessStateModule restored = new();
        Require(restored.TryRestoreState(module.CurrentVersion, saved, out string error),
            "Door JSON restore failed: " + error);
        Require(restored.CaptureState() == saved && restored.Revision == module.Revision,
            "Door JSON restore changed policy or revision.");
    }

    private static void VerifyEcology(IGameContentCatalog content, CropDefinitionSO crop)
    {
        const string plotId = "plot:r28-snapshot";
        const string operationId = "crop-harvest:r28-snapshot";
        DungeonRuntimeAggregateRootStore roots = new();
        ResourceDungeonItemCatalogProvider items = new(new ResourceItemDefinitionCatalog(content));
        PhysicalStockQuery stock = new(
            new WorldItemRepository(new GuidPersistentIdGenerator(), roots),
            items, new PhysicalItemMassQuery(items));
        CropEcologyRuntime ecology = new(roots, content, new RandomStreamProvider(28), stock);
        ecology.Sow(plotId, crop.FamilyGroup, CreateSeed(crop));
        string before = JsonUtility.ToJson(ecology.Capture());
        int version = ecology.Version;
        CropEcologyPlotSaveData snapshot = ecology.Plots.Single();
        snapshot.plotId = "plot:injected";
        snapshot.fertility = 0f;
        snapshot.diseasePressure = 100f;
        snapshot.cropDead = true;
        CropEcologyPlotSaveData fresh = ecology.Plots.Single();
        Require(fresh.plotId == plotId && !fresh.cropDead
            && Mathf.Approximately(fresh.diseasePressure, 10f)
            && JsonUtility.ToJson(ecology.Capture()) == before && ecology.Version == version,
            "Crop plot query mutation reached the aggregate or runtime version.");

        ecology.ApplyFungicide(plotId, 5f);
        Require(Mathf.Approximately(ecology.Plots.Single().diseasePressure, 5f)
            && Mathf.Approximately(fresh.diseasePressure, 10f) && ecology.Version == version + 1,
            "Crop owner command did not publish once or changed an earlier snapshot.");

        CropEcologyPreparedHarvestSnapshot prepared = ecology.PrepareHarvest(operationId, plotId);
        string seedBefore = JsonUtility.ToJson(prepared.Result.ReturnedSeedLot);
        before = JsonUtility.ToJson(ecology.Capture());
        version = ecology.Version;
        prepared.Result.ReturnedSeedLot.pathogenLoad = 99f;
        CropEcologyWorldSaveData detached = ecology.Capture();
        detached.activeCultivars[0].loci[0].alleleA = 99;
        detached.activeCultivars[0].loci.Clear();
        detached.preparedHarvests[0].generatedGenome.loci[0].alleleB = 99;
        detached.preparedHarvests[0].returnedSeedLot.cropId = "crop:injected";
        detached.preparedHarvests[0].plotBefore.fertility = 0f;
        detached.preparedHarvests[0].plotAfter.fertility = 0f;
        Require(ecology.TryGetPreparedHarvest(operationId, out CropEcologyPreparedHarvestSnapshot freshPrepared)
            && JsonUtility.ToJson(freshPrepared.Result.ReturnedSeedLot) == seedBefore
            && JsonUtility.ToJson(ecology.Capture()) == before && ecology.Version == version,
            "Crop nested seed, genome, or receipt snapshot mutation reached the owner.");

        CropEcologyRuntime restored = new(new DungeonRuntimeAggregateRootStore(),
            content, new RandomStreamProvider(28), stock);
        restored.PublishRestore(restored.PrepareRestore(
            JsonUtility.FromJson<CropEcologyWorldSaveData>(before)));
        Require(JsonUtility.ToJson(restored.Capture()) == before,
            "Crop JSON restore changed the plot, genome, or prepared receipt.");
        int restoredVersion = restored.Version;
        CropEcologyPreparedHarvestSnapshot committed = restored.CommitPreparedHarvest(operationId);
        Require(committed.Committed && restored.Version == restoredVersion + 1
            && JsonUtility.ToJson(committed.Result.ReturnedSeedLot) == seedBefore,
            "Restored crop owner could not commit its unchanged prepared harvest.");
    }

    private static void VerifyCarry(CropDefinitionSO crop)
    {
        GameObject sourceObject = new("R28 carry snapshot source") { hideFlags = HideFlags.HideAndDontSave };
        GameObject restoredObject = new("R28 carry snapshot restored") { hideFlags = HideFlags.HideAndDontSave };
        try
        {
            CharacterCarryInventory carry = sourceObject.AddComponent<CharacterCarryInventory>();
            int changes = 0;
            carry.Changed += () => changes++;
            carry.Restore(new CharacterCarryInventorySaveData
            {
                items = new List<CharacterCarriedItemSaveData>
                {
                    new()
                    {
                        carriedStackId = "stack:r28-carried",
                        sourceStackId = "stack:r28-source",
                        itemId = crop.SeedItemId,
                        quantity = 3,
                        contamination = 10f,
                        components = new List<ItemInstanceComponentSaveData>
                        {
                            SeedLotItemStateCodec.Encode(CreateSeed(crop))
                        }
                    }
                }
            });
            string before = JsonUtility.ToJson(carry.Capture());
            int changesBefore = changes;
            IReadOnlyList<CharacterCarriedItemSaveData> snapshot = carry.Items;
            snapshot[0].quantity = 99;
            snapshot[0].contamination = 99f;
            snapshot[0].components[0].values[0].stringValue = "crop:injected";
            snapshot[0].components[0].values.Clear();
            snapshot[0].components.Clear();
            ((IList<CharacterCarriedItemSaveData>)snapshot)[0] = new CharacterCarriedItemSaveData();
            CharacterCarriedItemSaveData fresh = carry.Items.Single();
            Require(fresh.quantity == 3 && Mathf.Approximately(fresh.contamination, 10f)
                && SeedLotItemStateCodec.Decode(fresh.components).cropId == crop.CropId
                && JsonUtility.ToJson(carry.Capture()) == before && changes == changesBefore,
                "Carry query collection, scalar, or nested component mutation reached the owner.");

            Require(!carry.TryConsumeItem(crop.SeedItemId, 4)
                && JsonUtility.ToJson(carry.Capture()) == before && changes == changesBefore,
                "Failed carry consume changed inventory or notified observers.");
            Require(carry.TryConsumeItem(crop.SeedItemId, 1)
                && carry.Items.Single().quantity == 2 && fresh.quantity == 3
                && changes == changesBefore + 1,
                "Carry owner command did not publish once or changed an earlier snapshot.");

            string saved = JsonUtility.ToJson(carry.Capture());
            CharacterCarryInventory restored = restoredObject.AddComponent<CharacterCarryInventory>();
            restored.Restore(JsonUtility.FromJson<CharacterCarryInventorySaveData>(saved));
            Require(JsonUtility.ToJson(restored.Capture()) == saved,
                "Carry JSON restore changed IDs, quantities, or component values.");
            Require(restored.TryTakeItem(crop.SeedItemId, out CharacterCarriedItemSaveData taken)
                && taken.quantity == 1 && restored.Items.Single().quantity == 1
                && SeedLotItemStateCodec.Decode(taken.components).cropId == crop.CropId,
                "Restored carry owner could not take an item with its seed component.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(restoredObject);
            UnityEngine.Object.DestroyImmediate(sourceObject);
        }
    }

    private static SeedLotState CreateSeed(CropDefinitionSO crop) => new()
    {
        cropId = crop.CropId,
        cultivarGenomeId = crop.BaseGenome.GenomeId,
        generation = 0,
        pathogenLoad = 40f
    };

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
#endif
