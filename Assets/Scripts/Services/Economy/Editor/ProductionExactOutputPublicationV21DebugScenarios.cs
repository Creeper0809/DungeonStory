#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Reflection;
using DungeonStory.Foundation;
using UnityEditor;
using UnityEngine;

public static class ProductionExactOutputPublicationV21DebugScenarios
{
    private const string RecipeId = "recipe:qa:v21-exact-output-envelope";
    private const string ItemId = "material:qa-v21-exact-output";
    private const string OutputLineId = "output:main";
    private const string CapabilityId = "production-output:qa-v21-exact";
    private const string ComponentCodecId =
        "production-output-codec:qa-v21-exact";
    private const string FacilityId = "building:qa-v21-exact-output";
    private const string BillId = "production-bill:1";
    private const string ShaA =
        "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    private const string ShaB =
        "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";
    private const string ShaC =
        "cccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccc";
    private const string ShaD =
        "dddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddd";
    private const string ShaE =
        "eeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeee";

    [MenuItem(
        "DungeonStory/V27/Production/Verify Exact Pending Output V21 Envelope")]
    public static void RunFromMenu()
    {
        RunAll();
    }

    public static void RunAll()
    {
        ProductionRecipeSO recipe =
            ScriptableObject.CreateInstance<ProductionRecipeSO>();
        ResourceItemDefinitionSO item =
            ScriptableObject.CreateInstance<ResourceItemDefinitionSO>();
        try
        {
            Configure(recipe, item);
            ResourceEconomyContentCatalog catalog =
                new ResourceEconomyContentCatalog(
                    new[] { item },
                    new[] { recipe },
                    Array.Empty<CropDefinitionSO>(),
                    Array.Empty<CraftMaterialDefinitionSO>());

            ProductionBillRecord record = CreateResolvedRecord();
            string commitId = BeginAndMark(record);
            ProductionResolvedOutputSaveData marked = record.resolvedOutputs[0];

            VerifyMarkedEnvelope(marked, commitId);
            VerifyDeepClone(marked);

            DungeonProductionBillSaveData baseline = Capture(record);
            Require(
                BuildRestoreViaCodec(baseline, catalog) != null,
                "A valid V21 exact pending-output envelope did not restore.");
            VerifyTamperMatrix(baseline, catalog);

            ExpectInvalidOperation(
                record.ClearResolvedOutputs,
                "ClearResolvedOutputs accepted a live pending-output envelope.");
            record.ClearResolvedOutputPendingCommit(OutputLineId, commitId);
            Require(
                IsEmpty(record.resolvedOutputs[0].pendingOutputPublication),
                "Acknowledgement did not clear the frozen pending-output envelope.");
            record.ClearResolvedOutputs();
            Require(
                !record.outputOutcomeResolved && record.resolvedOutputs.Count == 0,
                "Resolved-output clearing did not remove acknowledged output state.");

            Debug.Log(
                "Production V21 exact pending-output aggregate/codec contracts passed.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(recipe);
            UnityEngine.Object.DestroyImmediate(item);
        }
    }

    private static void Configure(
        ProductionRecipeSO recipe,
        ResourceItemDefinitionSO item)
    {
        item.Configure(
            ItemId,
            "V21 exact output QA item",
            "Editor-only V21 pending-output envelope fixture.",
            StockCategory.General,
            ResourceItemKind.Intermediate,
            ResourceIngredientTag.None,
            1,
            1f,
            100,
            string.Empty);
        recipe.Configure(
            RecipeId,
            "V21 exact output QA recipe",
            "Exercises the generic exact-output pending publication envelope.",
            "qa-v21-envelope",
            BuiltInWorkTypeIds.Craft.Value,
            string.Empty,
            1f,
            Array.Empty<ItemAmountDefinition>(),
            new[]
            {
                new ProductionOutputDefinition(
                    OutputLineId,
                    ProductionOutputRole.Main,
                    ItemId,
                    2)
            });
    }

    private static ProductionBillRecord CreateResolvedRecord()
    {
        ProductionBillId billId = (ProductionBillId)BillId;
        BuildingInstanceId facilityId = (BuildingInstanceId)FacilityId;
        ProductionBillRecord record = ProductionBillRecord.Create(
            billId,
            RecipeId,
            facilityId,
            ProductionOrderMode.RepeatCount,
            1,
            0,
            ProductionBatchStage.None,
            ProductionBillRuntime.DestinationPrefix + billId.Value);
        record.SetOutputDestination(
            ProductionBillRuntime.OutputDestinationPrefix + facilityId.Value);
        record.SetMaterialsConsumed(true);
        record.SetResolvedOutputs(new[]
        {
            new ProductionResolvedOutputSaveData
            {
                outputLineId = OutputLineId,
                itemId = ItemId,
                outputCapabilityId = CapabilityId,
                outputCapabilityVersion = 1,
                outputComponentCodecId = ComponentCodecId,
                outputComponentCodecVersion = 1,
                outputCapabilityFingerprint = ShaE,
                amount = 2,
                committedAmount = 0,
                committedMassGrams = 0L,
                pendingCommitId = string.Empty,
                pendingCommitApplied = false,
                pendingOutputPublication =
                    ProductionExactOutputPublicationSaveData.Empty(),
                qualityModifier = 1f,
                workerQuality = 1f
            }
        });
        return record;
    }

    private static string BeginAndMark(ProductionBillRecord record)
    {
        string commitId = ProductionOutputCommitIdentity.Format(
            record.billId,
            record.cycleSequence,
            OutputLineId,
            ItemId,
            0);
        record.BeginResolvedOutputUnit(OutputLineId, commitId);
        ProductionResolvedOutputSaveData begun = record.resolvedOutputs[0];
        Require(
            string.Equals(
                begun.pendingCommitId,
                commitId,
                StringComparison.Ordinal)
            && !begun.pendingCommitApplied
            && IsEmpty(begun.pendingOutputPublication),
            "Begin did not retain an empty exact pending-output envelope.");

        List<ProductionCommittedOutputStackSnapshot> mutableSource = new()
        {
            new ProductionCommittedOutputStackSnapshot(
                OutputLineId,
                "stack:qa-v21-exact-output:a",
                ItemId,
                1,
                400L,
                "component:qa:a",
                "item-instance:qa:a"),
            new ProductionCommittedOutputStackSnapshot(
                OutputLineId,
                "stack:qa-v21-exact-output:b",
                ItemId,
                1,
                600L,
                "component:qa:b",
                "item-instance:qa:b")
        };
        ProductionCommittedOutputSnapshot snapshot = new(
            commitId,
            FacilityId,
            CapabilityId,
            1,
            ComponentCodecId,
            1,
            ShaA,
            1_200L,
            ShaB,
            1_000L,
            1_000L,
            ShaC,
            ShaD,
            ProductionBillRuntime.OutputDestinationPrefix + FacilityId,
            17,
            23,
            "qa.production",
            "qa-v21-publication:" + commitId,
            FacilityId,
            7L,
            false,
            mutableSource);
        mutableSource.Clear();
        Require(
            snapshot.Stacks.Count == 2,
            "Committed-output snapshot retained its mutable source collection.");

        record.MarkResolvedOutputUnitCommitted(OutputLineId, commitId, snapshot);
        return commitId;
    }

    private static void VerifyMarkedEnvelope(
        ProductionResolvedOutputSaveData output,
        string commitId)
    {
        ProductionExactOutputPublicationSaveData publication =
            output.pendingOutputPublication;
        Require(
            output.committedAmount == 1
            && output.committedMassGrams == 1_000L
            && output.pendingCommitApplied
            && string.Equals(output.pendingCommitId, commitId, StringComparison.Ordinal)
            && publication != null
            && publication.phase == ProductionExactOutputPublicationPhase.Published
            && publication.exactMassGrams == 1_000L
            && publication.maximumMassGrams == 1_200L
            && publication.requiredMinimumCapacityGrams == 1_000L
            && publication.capacityRevision == 7L
            && publication.stacks.Count == 2
            && publication.stacks[0].stackOrdinal == 0
            && publication.stacks[1].stackOrdinal == 1,
            "Mark did not atomically freeze publication and committed-mass state.");
    }

    private static void VerifyDeepClone(ProductionResolvedOutputSaveData source)
    {
        ProductionResolvedOutputSaveData clone = source.Clone();
        Require(
            !ReferenceEquals(source, clone)
            && !ReferenceEquals(
                source.pendingOutputPublication,
                clone.pendingOutputPublication)
            && !ReferenceEquals(
                source.pendingOutputPublication.stacks,
                clone.pendingOutputPublication.stacks)
            && !ReferenceEquals(
                source.pendingOutputPublication.stacks[0],
                clone.pendingOutputPublication.stacks[0]),
            "Resolved-output Clone retained mutable V21 envelope references.");

        string originalStackId = source.pendingOutputPublication.stacks[0].stackId;
        clone.pendingOutputPublication.stacks[0].stackId =
            "stack:qa-v21-exact-output:clone-only";
        clone.pendingOutputPublication.ownerOperationId =
            "qa-v21-publication:clone-only";
        Require(
            string.Equals(
                source.pendingOutputPublication.stacks[0].stackId,
                originalStackId,
                StringComparison.Ordinal)
            && !string.Equals(
                source.pendingOutputPublication.ownerOperationId,
                clone.pendingOutputPublication.ownerOperationId,
                StringComparison.Ordinal),
            "Mutating a cloned V21 envelope changed the aggregate authority.");
    }

    private static void VerifyTamperMatrix(
        DungeonProductionBillSaveData baseline,
        IResourceEconomyContentCatalog catalog)
    {
        RequireRestoreFailure(
            baseline,
            catalog,
            save => Publication(save).maximumMassGrams = 999L,
            "maximum mass below exact mass");
        RequireRestoreFailure(
            baseline,
            catalog,
            save => Publication(save).maximumProofDigest = "tampered-proof",
            "maximum proof digest");
        RequireRestoreFailure(
            baseline,
            catalog,
            save => Publication(save).requiredMinimumCapacityGrams = 0L,
            "capacity proof");
        RequireRestoreFailure(
            baseline,
            catalog,
            save => Publication(save).destinationId =
                "production-output:building:qa-v21-other",
            "destination");
        RequireRestoreFailure(
            baseline,
            catalog,
            save => Publication(save).stacks[0].stackId = string.Empty,
            "stack identity");
        RequireRestoreFailure(
            baseline,
            catalog,
            save => Publication(save).stacks[0].massGrams++,
            "exact stack mass");
        RequireRestoreFailure(
            baseline,
            catalog,
            save =>
            {
                List<ProductionExactOutputPublicationStackSaveData> stacks =
                    Publication(save).stacks;
                (stacks[0], stacks[1]) = (stacks[1], stacks[0]);
            },
            "canonical stack order");
        RequireRestoreFailure(
            baseline,
            catalog,
            save => Publication(save).ownerStableId = "production-bill:2",
            "owner");
        RequireRestoreFailure(
            baseline,
            catalog,
            save => Publication(save).outputCapabilityId =
                "production-output:qa-v21-other",
            "capability");
    }

    private static ProductionExactOutputPublicationSaveData Publication(
        DungeonProductionBillSaveData save) =>
        save.bills[0].resolvedOutputs[0].pendingOutputPublication;

    private static DungeonProductionBillSaveData Capture(
        ProductionBillRecord record)
    {
        Type codec = RequireCodecType();
        MethodInfo capture = codec.GetMethod(
            "ToSaveData",
            BindingFlags.Static | BindingFlags.NonPublic,
            null,
            new[] { typeof(ProductionBillRecord) },
            null);
        Require(capture != null, "ProductionBillStateCodec.ToSaveData was not found.");
        ProductionBillSaveData bill = Invoke<ProductionBillSaveData>(
            capture,
            record);
        return new DungeonProductionBillSaveData
        {
            version = DungeonProductionBillSaveData.CurrentVersion,
            nextBillSequence = 2,
            bills = new List<ProductionBillSaveData> { bill }
        };
    }

    private static ProductionBillRestoreCandidate BuildRestoreViaCodec(
        DungeonProductionBillSaveData save,
        IResourceEconomyContentCatalog catalog)
    {
        Type codec = RequireCodecType();
        MethodInfo build = codec.GetMethod(
            "CreateRestoreCandidate",
            BindingFlags.Static | BindingFlags.NonPublic,
            null,
            new[]
            {
                typeof(DungeonProductionBillSaveData),
                typeof(IResourceEconomyContentCatalog),
                typeof(int),
                typeof(int)
            },
            null);
        Require(
            build != null,
            "ProductionBillStateCodec.CreateRestoreCandidate was not found.");
        return Invoke<ProductionBillRestoreCandidate>(build, save, catalog, 1, 1);
    }

    private static Type RequireCodecType()
    {
        Type codec = typeof(ProductionBillRuntime).Assembly.GetType(
            "ProductionBillStateCodec",
            throwOnError: false);
        Require(codec != null, "ProductionBillStateCodec runtime type was not found.");
        return codec;
    }

    private static T Invoke<T>(MethodInfo method, params object[] arguments)
    {
        try
        {
            return (T)method.Invoke(null, arguments);
        }
        catch (TargetInvocationException exception)
            when (exception.InnerException != null)
        {
            throw exception.InnerException;
        }
    }

    private static void RequireRestoreFailure(
        DungeonProductionBillSaveData baseline,
        IResourceEconomyContentCatalog catalog,
        Action<DungeonProductionBillSaveData> mutate,
        string label)
    {
        DungeonProductionBillSaveData tampered = Clone(baseline);
        mutate(tampered);
        ExpectInvalidOperation(
            () => BuildRestoreViaCodec(tampered, catalog),
            $"ProductionBillStateCodec accepted tampered {label} authority.");
    }

    private static DungeonProductionBillSaveData Clone(
        DungeonProductionBillSaveData source) =>
        JsonUtility.FromJson<DungeonProductionBillSaveData>(
            JsonUtility.ToJson(source));

    private static void ExpectInvalidOperation(Action action, string message)
    {
        try
        {
            action();
        }
        catch (InvalidOperationException)
        {
            return;
        }
        throw new InvalidOperationException(message);
    }

    private static bool IsEmpty(
        ProductionExactOutputPublicationSaveData publication) =>
        publication != null
        && publication.phase == ProductionExactOutputPublicationPhase.None
        && string.IsNullOrEmpty(publication.ownerStableId)
        && string.IsNullOrEmpty(publication.commitId)
        && string.IsNullOrEmpty(publication.facilityInstanceId)
        && string.IsNullOrEmpty(publication.outputCapabilityId)
        && publication.outputCapabilityVersion == 0
        && string.IsNullOrEmpty(publication.outputComponentCodecId)
        && publication.outputComponentCodecVersion == 0
        && string.IsNullOrEmpty(publication.maximumProofDigest)
        && publication.maximumMassGrams == 0L
        && string.IsNullOrEmpty(publication.capacitySourceDigest)
        && publication.requiredMinimumCapacityGrams == 0L
        && publication.exactMassGrams == 0L
        && string.IsNullOrEmpty(publication.outcomeFingerprint)
        && string.IsNullOrEmpty(publication.plannedOutputFingerprint)
        && string.IsNullOrEmpty(publication.destinationId)
        && publication.dropPositionX == 0
        && publication.dropPositionY == 0
        && string.IsNullOrEmpty(publication.ownerDomain)
        && string.IsNullOrEmpty(publication.ownerOperationId)
        && string.IsNullOrEmpty(publication.ownerFacilityId)
        && publication.capacityRevision == 0L
        && !publication.acknowledgedAtCapture
        && publication.stacks != null
        && publication.stacks.Count == 0;

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
#endif
