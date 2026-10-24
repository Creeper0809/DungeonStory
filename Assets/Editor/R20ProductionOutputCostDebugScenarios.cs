using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Balance;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public static class R20ProductionOutputCostDebugScenarios
{
    private static readonly string[] RecipePaths =
    {
        "Assets/Resources/SO/Economy/Recipes/source_logging.asset",
        "Assets/Resources/SO/Economy/Recipes/source_saltstone.asset",
        "Assets/Resources/SO/Economy/Recipes/source_quarry.asset"
    };

    [MenuItem("DungeonStory/R20/Run Production Output Cost Scenarios")]
    public static void RunAll()
    {
        ProductionRecipeSO[] sources = RecipePaths
            .Select(RequireRecipe)
            .ToArray();
        SourceSnapshot[] snapshots = sources
            .Select(SourceSnapshot.Capture)
            .ToArray();
        ProductionRecipeSO[] clones = sources
            .Select(source => Object.Instantiate(source))
            .ToArray();
        try
        {
            VerifyExistingPayloads(clones);
            VerifyInvalidPayloadsRejectAndRestoreRetries(clones[0]);
            VerifyPendingOutputsDriveInspectorChoices(clones[0]);
            Debug.Log(
                "R20_PRODUCTION_OUTPUT_COST_SCENARIOS=PASS; valid3; typo; missing; extra; negative; restore retry; pending outputs; source JSON and dirty preserved");
        }
        finally
        {
            foreach (ProductionRecipeSO clone in clones)
                Object.DestroyImmediate(clone);
            foreach (SourceSnapshot snapshot in snapshots)
                snapshot.RequireUnchanged();
        }
    }

    private static void VerifyExistingPayloads(
        IEnumerable<ProductionRecipeSO> recipes)
    {
        ProductionOutputCostAllocationCapabilityRegistry registry =
            ProductionOutputCostAllocationCapabilityRegistry.CreateDefault();
        foreach (ProductionRecipeSO recipe in recipes)
        {
            ProductionOutputCostAllocationAuthoringSnapshot allocation =
                recipe.OutputCostAllocation;
            registry.ValidateForOutputs(allocation, recipe.Outputs);
            IReadOnlyList<ProductionOutputCostAllocationWeight> decoded =
                WeightedOutputShareProductionOutputCostAllocationCapability
                    .DecodePayload(allocation.CanonicalPayload);
            string encoded =
                WeightedOutputShareProductionOutputCostAllocationCapability
                    .EncodePayload(decoded);
            Require(encoded == allocation.CanonicalPayload,
                recipe.RecipeId + " payload did not round-trip byte-for-byte.");
            CreateCatalog(recipe);
        }

        registry.ValidateForOutputs(
            ProductionOutputCostAllocationAuthoringSnapshot.Empty,
            recipes.First().Outputs);
    }

    private static void VerifyInvalidPayloadsRejectAndRestoreRetries(
        ProductionRecipeSO recipe)
    {
        ProductionOutputCostAllocationAuthoringSnapshot original =
            recipe.OutputCostAllocation;
        IReadOnlyList<ProductionOutputCostAllocationWeight> valid =
            WeightedOutputShareProductionOutputCostAllocationCapability
                .DecodePayload(original.CanonicalPayload);

        string typoPayload =
            WeightedOutputShareProductionOutputCostAllocationCapability
                .EncodePayload(valid.Select((weight, index) =>
                    index == 0
                        ? new ProductionOutputCostAllocationWeight(
                            weight.OutputLineId + "-typo",
                            weight.Weight)
                        : weight));
        ExpectRejected(recipe, typoPayload, "typo");

        string missingPayload =
            WeightedOutputShareProductionOutputCostAllocationCapability
                .EncodePayload(valid.Skip(1));
        ExpectRejected(recipe, missingPayload, "missing");

        string extraPayload =
            WeightedOutputShareProductionOutputCostAllocationCapability
                .EncodePayload(valid.Concat(new[]
                {
                    new ProductionOutputCostAllocationWeight(
                        "output:zz-r20/999/byproduct/resource:foreign",
                        1L)
                }));
        ExpectRejected(recipe, extraPayload, "extra");

        string negativePayload = original.CanonicalPayload.Replace(
            "=" + valid[0].Weight,
            "=-1");
        ExpectRejected(recipe, negativePayload, "negative");

        Restore(recipe, original);
        CreateCatalog(recipe);
        Require(recipe.OutputCostAllocation.CanonicalPayload
                    == original.CanonicalPayload,
            "Restored payload changed before retry.");
    }

    private static void VerifyPendingOutputsDriveInspectorChoices(
        ProductionRecipeSO recipe)
    {
        Restore(recipe, recipe.OutputCostAllocation);
        string objectLineId = recipe.Outputs[0].OutputLineId;
        using SerializedObject serialized = new(recipe);
        SerializedProperty outputs = Required(serialized, "outputs");
        SerializedProperty pendingLineId = outputs.GetArrayElementAtIndex(0)
            .FindPropertyRelative("outputLineId");
        SerializedProperty allocation = Required(
            serialized,
            "outputCostAllocation");
        SerializedProperty payload = allocation.FindPropertyRelative(
            "canonicalPayload");
        string payloadBefore = payload.stringValue;
        string pendingId = objectLineId + "-pending";
        pendingLineId.stringValue = pendingId;

        R20ProductionOutputCostInspection inspection =
            R20ProductionOutputCostDrawer.Inspect(allocation);
        Require(!inspection.IsValid
                && inspection.Error.Contains("Missing output-cost weight",
                    StringComparison.Ordinal),
            "Pending output mismatch did not surface the registry error.");
        Require(inspection.OutputLineIds.Contains(pendingId, StringComparer.Ordinal),
            "Inspector choices did not use the pending SerializedProperty outputs.");
        Require(payload.stringValue == payloadBefore,
            "Inspector validation changed invalid payload bytes.");
        Require(recipe.Outputs[0].OutputLineId == objectLineId,
            "Inspector proof applied pending outputs to the recipe asset.");

        Require(R20ProductionOutputCostDrawer.TryBuildCorrectionPayload(
                allocation,
                out string correction,
                out string correctionError),
            "Pending correction payload failed: " + correctionError);
        Require(correction.Contains(pendingId, StringComparison.Ordinal),
            "Correction payload did not derive its IDs from pending outputs.");
        Require(payload.stringValue == payloadBefore
                && recipe.Outputs[0].OutputLineId == objectLineId,
            "Correction preview mutated serialized bytes or the recipe asset.");
    }

    private static void ExpectRejected(
        ProductionRecipeSO recipe,
        string payload,
        string context)
    {
        recipe.ConfigureOutputCostAllocation(
            WeightedOutputShareProductionOutputCostAllocationCapability.Id,
            WeightedOutputShareProductionOutputCostAllocationCapability.Version,
            payload);
        try
        {
            CreateCatalog(recipe);
        }
        catch (InvalidOperationException)
        {
            return;
        }
        throw new InvalidOperationException(
            "Resource catalog accepted the " + context + " payload.");
    }

    private static void CreateCatalog(ProductionRecipeSO recipe)
    {
        ResourceItemDefinitionSO[] items = Resources
            .LoadAll<ResourceItemDefinitionSO>(ItemDefinitionSO.UnifiedResourcePath);
        _ = new ResourceEconomyContentCatalog(
            items,
            new[] { recipe },
            Array.Empty<CropDefinitionSO>(),
            Array.Empty<CraftMaterialDefinitionSO>());
    }

    private static void Restore(
        ProductionRecipeSO recipe,
        ProductionOutputCostAllocationAuthoringSnapshot snapshot)
    {
        recipe.ConfigureOutputCostAllocation(
            snapshot.CapabilityId,
            snapshot.ContractVersion,
            snapshot.CanonicalPayload);
    }

    private static ProductionRecipeSO RequireRecipe(string path) =>
        AssetDatabase.LoadAssetAtPath<ProductionRecipeSO>(path)
        ?? throw new InvalidOperationException(
            "Missing R20 fixture recipe at " + path + ".");

    private static SerializedProperty Required(
        SerializedObject serialized,
        string path) => serialized.FindProperty(path)
        ?? throw new InvalidOperationException(
            "Missing serialized property '" + path + "'.");

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    private readonly struct SourceSnapshot
    {
        private readonly ProductionRecipeSO asset;
        private readonly string json;
        private readonly bool dirty;

        private SourceSnapshot(
            ProductionRecipeSO asset,
            string json,
            bool dirty)
        {
            this.asset = asset;
            this.json = json;
            this.dirty = dirty;
        }

        public static SourceSnapshot Capture(ProductionRecipeSO asset) =>
            new(asset, EditorJsonUtility.ToJson(asset), EditorUtility.IsDirty(asset));

        public void RequireUnchanged()
        {
            Require(EditorJsonUtility.ToJson(asset) == json,
                $"Source asset '{asset.name}' JSON changed.");
            Require(EditorUtility.IsDirty(asset) == dirty,
                $"Source asset '{asset.name}' dirty state changed.");
        }
    }
}
