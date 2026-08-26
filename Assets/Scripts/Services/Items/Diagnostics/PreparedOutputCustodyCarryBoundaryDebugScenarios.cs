#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class PreparedOutputCustodyCarryBoundaryDebugScenarios
{
    private const string ItemId = "material:lumber";
    private const string OwnerId = "haul:qa:prepared-output";

    private static readonly CarryMutationCallsiteManifestEntry[] Manifest =
    {
        new(
            "CharacterCarryInventory.TryConsume* / TryTakeItem",
            "combat, survival, captivity",
            "skip protected custody; insufficient ordinary quantity is atomic false"),
        new(
            "CharacterCarryInventory.RemoveAllItems",
            "ItemTransferService contextless whole-carry drop",
            "typed ProtectedRouteBypass before mutation"),
        new(
            "CharacterCarryInventory.RemoveItemsOwnedByOperations",
            "ItemTransferService retail and owner-scoped contextless drop",
            "retail/domain preflight plus inventory typed fail-close"),
        new(
            "CharacterCarryInventory.*ForPhysicalTransfer",
            "warehouse/facility deposit and typed Downed/Dead recovery",
            "explicit ownership transfer; components preserved"),
        new(
            "CharacterCarryInventory.DiscardAllItemsForRestoredWorldReplacement",
            "restore retirement only",
            "explicit old-world authority retirement; not gameplay disposal")
    };

    [MenuItem("DungeonStory/Debug/Items/Run Prepared Output Carry Boundary Guards")]
    public static void RunAll()
    {
        VerifyDirectCarryMutationGuards();
        VerifyStaticCallsiteManifest();
        Debug.Log(
            "Prepared-output carry boundary guards PASS.\n"
            + string.Join("\n", Manifest.Select(entry => entry.ToString())));
    }

    private static void VerifyDirectCarryMutationGuards()
    {
        GameObject target = new("PreparedOutputCarryBoundaryFixture");
        try
        {
            CharacterCarryInventory inventory =
                target.AddComponent<CharacterCarryInventory>();
            CharacterCarriedItemSaveData protectedItem = CreateItem(
                "carried:qa:protected",
                "source:qa:protected",
                OwnerId,
                2,
                protectedCustody: true);
            inventory.Restore(new CharacterCarryInventorySaveData
            {
                items = new List<CharacterCarriedItemSaveData>
                {
                    protectedItem,
                    CreateItem(
                        "carried:qa:ordinary",
                        "source:qa:ordinary",
                        "haul:qa:ordinary",
                        1,
                        protectedCustody: false)
                }
            });

            string before = CaptureSignature(inventory);
            Require(!inventory.TryConsumeItem(ItemId, 2),
                "TryConsumeItem counted protected custody as consumable.");
            Require(CaptureSignature(inventory) == before,
                "Failed TryConsumeItem partially changed carry authority.");
            Require(inventory.TryConsumeItem(ItemId, 1)
                && inventory.Items.Count == 1
                && FacilityOutputExactRouteCustodyCodec.HasAnyCustody(
                    inventory.Items[0].components),
                "TryConsumeItem did not consume only the ordinary lot.");

            RestoreProtectedOnly(inventory);
            before = CaptureSignature(inventory);
            Require(!inventory.TryTakeItem(ItemId, out _)
                && !inventory.TryConsumeSourceStack(
                    "source:qa:protected",
                    ItemId,
                    1)
                && !inventory.TryConsumeCarriedStack(
                    "carried:qa:protected",
                    ItemId,
                    1)
                && CaptureSignature(inventory) == before,
                "A direct carried-item consumer mutated protected custody.");

            RequireThrows<FacilityOutputExactRouteBypassException>(
                () => inventory.RemoveAllItems(),
                "RemoveAllItems accepted protected custody.");
            Require(CaptureSignature(inventory) == before,
                "RemoveAllItems mutated inventory before failing loud.");
            RequireThrows<FacilityOutputExactRouteBypassException>(
                () => inventory.RemoveItemsOwnedByOperations(new[] { OwnerId }),
                "Owner-scoped removal accepted protected custody.");
            Require(CaptureSignature(inventory) == before,
                "Owner-scoped removal mutated inventory before failing loud.");

            List<CharacterCarriedItemSaveData> transferred =
                inventory.RemoveItemsOwnedByOperationsForPhysicalTransfer(
                    new[] { OwnerId });
            Require(transferred.Count == 1
                && transferred[0].quantity == 2
                && FacilityOutputExactRouteCustodyCodec.HasAnyCustody(
                    transferred[0].components)
                && inventory.Items.Count == 0,
                "Explicit physical transfer did not preserve exact custody.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(target);
        }
    }

    private static void VerifyStaticCallsiteManifest()
    {
        string projectRoot = Path.GetFullPath(
            Path.Combine(Application.dataPath, ".."));
        string carryPath = Path.Combine(
            projectRoot,
            "Assets/Scripts/Services/Items/CharacterCarryInventory.cs");
        string transferPath = Path.Combine(
            projectRoot,
            "Assets/Scripts/Services/Items/ItemTransferService.cs");
        string carrySource = File.ReadAllText(carryPath);
        string transferSource = File.ReadAllText(transferPath);

        Require(Count(carrySource, "HasPreparedOutputCustody(item)") >= 6,
            "Direct carry consumer custody guards are incomplete.");
        Require(carrySource.Contains(
                "CharacterCarryInventory.RemoveAllItems\");",
                StringComparison.Ordinal)
            && carrySource.Contains(
                "CharacterCarryInventory.RemoveItemsOwnedByOperations\");",
                StringComparison.Ordinal),
            "Direct destructive carry APIs do not fail through the typed boundary.");
        Require(Count(
                transferSource,
                "prepared-output-route-protected-contextless-drop") == 2,
            "Contextless drop guard count changed; update the audited manifest.");
        Require(transferSource.Contains(
                "retail-transfer-prepared-output-route-protected",
                StringComparison.Ordinal)
            && transferSource.Contains(
                "retail-transfer-prepared-output-route-protected-precommit",
                StringComparison.Ordinal),
            "Retail transfer lacks preflight or immediate precommit custody guard.");
        Require(Count(
                transferSource,
                "RemoveItemsOwnedByOperationsForPhysicalTransfer(") >= 4,
            "Typed recovery drop no longer uses explicit physical transfer.");

        string[] productionFiles = Directory.GetFiles(
                Path.Combine(projectRoot, "Assets/Scripts"),
                "*.cs",
                SearchOption.AllDirectories)
            .Where(path => !path.Contains(
                $"{Path.DirectorySeparatorChar}Editor{Path.DirectorySeparatorChar}",
                StringComparison.OrdinalIgnoreCase))
            .Where(path => !path.Contains(
                $"{Path.DirectorySeparatorChar}Diagnostics{Path.DirectorySeparatorChar}",
                StringComparison.OrdinalIgnoreCase))
            .ToArray();
        string[] directRemoveCallsites = productionFiles
            .SelectMany(path => File.ReadAllLines(path)
                .Select((line, index) => new { path, line, index })
                .Where(value => value.line.Contains(
                        ".RemoveAllItems(",
                        StringComparison.Ordinal)
                    || value.line.Contains(
                        ".RemoveItemsOwnedByOperations(",
                        StringComparison.Ordinal)))
            .Select(value =>
                $"{Path.GetRelativePath(projectRoot, value.path)}:{value.index + 1}")
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        Require(directRemoveCallsites.Length == 3
            && directRemoveCallsites.All(value => value.Contains(
                "ItemTransferService.cs:",
                StringComparison.Ordinal)),
            "Unaudited production direct carry-removal callsite(s): "
            + string.Join(", ", directRemoveCallsites));
    }

    private static void RestoreProtectedOnly(CharacterCarryInventory inventory) =>
        inventory.Restore(new CharacterCarryInventorySaveData
        {
            items = new List<CharacterCarriedItemSaveData>
            {
                CreateItem(
                    "carried:qa:protected",
                    "source:qa:protected",
                    OwnerId,
                    2,
                    protectedCustody: true)
            }
        });

    private static CharacterCarriedItemSaveData CreateItem(
        string carriedStackId,
        string sourceStackId,
        string ownerOperationId,
        int quantity,
        bool protectedCustody) => new()
    {
        carriedStackId = carriedStackId,
        sourceStackId = sourceStackId,
        ownerOperationId = ownerOperationId,
        itemId = ItemId,
        quantity = quantity,
        components = protectedCustody
            ? new List<ItemInstanceComponentSaveData>
            {
                new()
                {
                    componentTypeId =
                        FacilityOutputExactRouteCustodyCodec.ComponentTypeId,
                    schemaVersion =
                        FacilityOutputExactRouteCustodyCodec.SchemaVersion,
                    affectsStacking = true,
                    values = new List<ItemStateValueSaveData>()
                }
            }
            : new List<ItemInstanceComponentSaveData>()
    };

    private static string CaptureSignature(CharacterCarryInventory inventory) =>
        string.Join("|", inventory.Capture().items
            .OrderBy(item => item.carriedStackId, StringComparer.Ordinal)
            .Select(item => string.Join(
                ":",
                item.carriedStackId,
                item.sourceStackId,
                item.ownerOperationId,
                item.itemId,
                item.quantity,
                ItemStackSignature.Create(item.itemId, item.components))));

    private static int Count(string text, string value)
    {
        int count = 0;
        int offset = 0;
        while ((offset = text.IndexOf(value, offset, StringComparison.Ordinal)) >= 0)
        {
            count++;
            offset += value.Length;
        }
        return count;
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    private static void RequireThrows<TException>(Action action, string message)
        where TException : Exception
    {
        try
        {
            action();
        }
        catch (TException)
        {
            return;
        }
        throw new InvalidOperationException(message);
    }

    private readonly struct CarryMutationCallsiteManifestEntry
    {
        public CarryMutationCallsiteManifestEntry(
            string symbol,
            string liveCallers,
            string guard)
        {
            Symbol = symbol;
            LiveCallers = liveCallers;
            Guard = guard;
        }

        public string Symbol { get; }
        public string LiveCallers { get; }
        public string Guard { get; }

        public override string ToString() =>
            $"{Symbol} | {LiveCallers} | {Guard}";
    }
}
#endif
