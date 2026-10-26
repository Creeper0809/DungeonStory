using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public static class R26ReferenceOwnerGuardDebugScenarios
{
    private const string RootCatalogPath =
        "Assets/Resources/SO/GameContentCatalog.asset";

    [MenuItem("DungeonStory/R26/Content/Validate Reference Owner Guards")]
    public static void RunAll()
    {
        GameContentCatalogSO root = AssetDatabase
            .LoadAssetAtPath<GameContentCatalogSO>(RootCatalogPath)
            ?? throw new InvalidOperationException(
                "Registered root content catalog is missing.");
        ContentReferenceCatalog catalog = ContentReferenceCatalog.FromRoot(root);
        var owned = new List<Object>();
        try
        {
            RequireClean(
                catalog.Validate(new ScriptableObject[] { root }),
                "Registered root content relations");
            VerifyEquipmentReverseMapping(catalog, owned);
            VerifyServiceProcessOwnerAndPendingSelection(catalog, owned);
            VerifyStockShopOwner(catalog, owned);
            VerifyBuildingConnectionOwner(catalog, owned);
            VerifyEffectiveProductionWorkstation(catalog, owned);
            Debug.Log(
                "R26_REFERENCE_OWNER_GUARDS=PASS; root clean; equipment reciprocal; "
                + "service owner/category and pending picker; stock shop owner; "
                + "building connection mode; effective production workstation");
        }
        finally
        {
            ContentReferenceSelection.Invalidate();
            foreach (Object value in owned)
            {
                if (value != null)
                    Object.DestroyImmediate(value);
            }
        }
    }

    private static void VerifyEquipmentReverseMapping(
        ContentReferenceCatalog catalog,
        ICollection<Object> owned)
    {
        CombatEquipmentDefinitionSO[] equipment = Assets<
            CombatEquipmentDefinitionSO>(
            catalog,
            new ContentReferenceAttribute(ContentReferenceKind.Equipment));
        CombatEquipmentDefinitionSO source = equipment.FirstOrDefault(value =>
            equipment.Any(other => other != value
                && !string.Equals(
                    other.ItemId,
                    value.ItemId,
                    StringComparison.Ordinal)))
            ?? throw new InvalidOperationException(
                "Two registered equipment definitions with different items are required.");
        CombatEquipmentDefinitionSO other = equipment.First(value =>
            value != source
            && !string.Equals(value.ItemId, source.ItemId, StringComparison.Ordinal));
        CombatEquipmentDefinitionSO clone = Clone(source, owned);
        using (var serialized = new SerializedObject(clone))
        {
            Required(serialized, "itemId").stringValue = other.ItemId;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        RequireError(
            catalog.Validate(new ScriptableObject[] { clone }),
            "equipment physical item must link back to the same equipment definition.",
            "An equipment definition accepted another definition's physical item.");
    }

    private static void VerifyServiceProcessOwnerAndPendingSelection(
        ContentReferenceCatalog catalog,
        ICollection<Object> owned)
    {
        BuildingSO[] buildings = Assets<BuildingSO>(
            catalog,
            new ContentReferenceAttribute(ContentReferenceKind.BuildingNumeric));
        ServiceProcessSO[] processes = Assets<ServiceProcessSO>(
            catalog,
            new ContentReferenceAttribute(nameof(ServiceProcessSO), "ProcessId"));
        BuildingSO source = buildings.FirstOrDefault(value =>
            value.GetAbility<BuildingServiceHubAbility>() != null)
            ?? throw new InvalidOperationException(
                "A registered service-hub building is required.");
        BuildingServiceHubAbility sourceHub = source
            .GetAbility<BuildingServiceHubAbility>();
        string sourceJson = EditorJsonUtility.ToJson(source);
        bool sourceDirty = EditorUtility.IsDirty(source);
        ServiceProcessSO mismatch = processes.FirstOrDefault(value =>
            !string.Equals(
                value.OwnerHubTag,
                sourceHub.ServiceHubTag,
                StringComparison.Ordinal)
            || value.ServiceCategory != sourceHub.serviceCategory)
            ?? throw new InvalidOperationException(
                "A registered process outside the source hub/category is required.");
        BuildingSO clone = Clone(source, owned);
        BuildingServiceHubAbility cloneHub = clone
            .GetAbility<BuildingServiceHubAbility>();
        string cloneJson = EditorJsonUtility.ToJson(clone);
        string originalTag = cloneHub.ServiceHubTag;
        ServiceCategory originalCategory = cloneHub.serviceCategory;

        using (var serialized = new SerializedObject(clone))
        {
            SerializedProperty modules = Required(
                serialized,
                BuildingSO.AbilityModulesFieldName);
            SerializedProperty items = modules.FindPropertyRelative("items")
                ?? throw new InvalidOperationException(
                    "Building ability serialization has no items list.");
            int hubIndex = clone.Abilities
                .Select((ability, index) => (ability, index))
                .First(pair => pair.ability is BuildingServiceHubAbility)
                .index;
            SerializedProperty pendingHub = items.GetArrayElementAtIndex(hubIndex);
            pendingHub.FindPropertyRelative("serviceHubTag").stringValue =
                mismatch.OwnerHubTag;
            pendingHub.FindPropertyRelative("serviceCategory").intValue =
                (int)mismatch.ServiceCategory;
            SerializedProperty supported = pendingHub
                .FindPropertyRelative("supportedProcessIds");
            supported.arraySize = Math.Max(1, supported.arraySize);
            SerializedProperty pendingProcess = supported
                .GetArrayElementAtIndex(0);
            pendingProcess.stringValue = mismatch.ProcessId;

            ContentReferenceSelection.Invalidate();
            ContentReferenceAttribute reference = new(
                nameof(ServiceProcessSO),
                "ProcessId");
            ContentReferenceSelection.Entry[] candidates =
                ContentReferenceSelection.Entries(reference, pendingProcess)
                    .ToArray();
            Require(
                candidates.Any(value => ReferenceEquals(value.Asset, mismatch))
                && candidates.All(value => value.Asset is ServiceProcessSO process
                    && string.Equals(
                        process.OwnerHubTag,
                        mismatch.OwnerHubTag,
                        StringComparison.Ordinal)
                    && process.ServiceCategory == mismatch.ServiceCategory),
                "Pending service-hub values did not filter process candidates.");
            Require(
                EditorJsonUtility.ToJson(clone) == cloneJson
                && cloneHub.ServiceHubTag == originalTag
                && cloneHub.serviceCategory == originalCategory,
                "Pending service-hub selection mutated the clone before Apply.");
        }

        cloneHub.supportedProcessIds = new[] { mismatch.ProcessId };
        RequireError(
            catalog.Validate(new ScriptableObject[] { clone }),
            "belongs to a different hub/category or is missing.",
            "A service hub accepted a process from another hub/category.");
        Require(
            EditorJsonUtility.ToJson(source) == sourceJson
            && EditorUtility.IsDirty(source) == sourceDirty,
            "Pending service selection changed the registered source asset.");
    }

    private static void VerifyStockShopOwner(
        ContentReferenceCatalog catalog,
        ICollection<Object> owned)
    {
        BuildingSO[] buildings = Assets<BuildingSO>(
            catalog,
            new ContentReferenceAttribute(ContentReferenceKind.BuildingNumeric));
        BuildingSO nonShop = buildings.FirstOrDefault(value =>
            value.id > 0 && value.category != BuildingCategory.Shop)
            ?? throw new InvalidOperationException(
                "A registered non-shop building is required.");
        StockInfo stock = ScriptableObject.CreateInstance<StockInfo>();
        owned.Add(stock);
        using (var serialized = new SerializedObject(stock))
        {
            SerializedProperty shopId = Required(serialized, nameof(StockInfo.shopId));
            ContentReferenceAttribute reference = new(
                ContentReferenceKind.BuildingNumeric,
                emptyNumericId: 0);
            ContentReferenceSelection.Invalidate();
            ContentReferenceSelection.Entry[] candidates =
                ContentReferenceSelection.Entries(reference, shopId).ToArray();
            Require(
                candidates.Length > 0
                && candidates.All(value => value.Asset is BuildingSO
                    { category: BuildingCategory.Shop })
                && candidates.All(value => !ReferenceEquals(value.Asset, nonShop)),
                "StockInfo shop candidates included a registered non-shop building.");
            Require(
                !ContentReferenceSelection.TryAssign(
                    shopId,
                    reference,
                    nonShop,
                    out _),
                "StockInfo accepted a registered non-shop building selection.");

            shopId.intValue = nonShop.id;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        RequireError(
            catalog.Validate(new ScriptableObject[] { stock }),
            "stock owner must be a registered shop building.",
            "StockInfo validation accepted a registered non-shop owner.");
    }

    private static void VerifyBuildingConnectionOwner(
        ContentReferenceCatalog catalog,
        ICollection<Object> owned)
    {
        BuildingSO source = Assets<BuildingSO>(
                catalog,
                new ContentReferenceAttribute(ContentReferenceKind.BuildingNumeric))
            .First();
        BuildingSO clone = Clone(source, owned);
        var condition = new ConditionNeedToConnect
        {
            connectWithEntrance = false,
            associatedId = -1
        };
        SetBuildingConditions(clone, condition);
        RequireError(
            catalog.Validate(new ScriptableObject[] { clone }),
            "a building connection condition requires a non-negative building definition ID.",
            "A non-entrance building connection accepted an unset ID.");

        condition.associatedId = 0;
        RequireClean(catalog.Validate(new ScriptableObject[] { clone }),
            "A registered corridor building ID zero must remain valid.");
        condition.connectWithEntrance = true;
        RequireClean(
            catalog.Validate(new ScriptableObject[] { clone }),
            "Entrance building connection with inactive ID zero");
    }

    private static void VerifyEffectiveProductionWorkstation(
        ContentReferenceCatalog catalog,
        ICollection<Object> owned)
    {
        ProductionRecipeSO source = Assets<ProductionRecipeSO>(
                catalog,
                new ContentReferenceAttribute(
                    nameof(ProductionRecipeSO),
                    "RecipeId"))
            .First(value => value.FlowRole == ProductionFlowRole.Transform);
        ProductionRecipeSO clone = Clone(source, owned);
        const string missingProducer = "r26:missing-production-workstation";
        using (var serialized = new SerializedObject(clone))
        {
            Required(serialized, "flowRole").intValue =
                (int)ProductionFlowRole.Transform;
            Required(serialized, "workstationTag").stringValue = string.Empty;
            Required(serialized, "facilityTag").stringValue = missingProducer;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
        Require(
            string.Equals(
                clone.WorkstationTag,
                missingProducer,
                StringComparison.Ordinal),
            "An empty workstation tag did not fall back to FacilityTag.");
        RequireError(
            catalog.Validate(new ScriptableObject[] { clone }),
            "effective workstation tag '" + missingProducer
                + "' has no valid registered producer.",
            "A Transform recipe accepted an effective workstation with no producer.");
    }

    private static T[] Assets<T>(
        ContentReferenceCatalog catalog,
        ContentReferenceAttribute reference)
        where T : ScriptableObject
    {
        T[] values = catalog.Entries(reference)
            .Select(value => value.Asset)
            .OfType<T>()
            .Distinct()
            .ToArray();
        if (values.Length == 0)
        {
            throw new InvalidOperationException(
                "No registered " + typeof(T).Name + " definitions were found.");
        }
        return values;
    }

    private static T Clone<T>(T source, ICollection<Object> owned)
        where T : ScriptableObject
    {
        T clone = Object.Instantiate(source);
        clone.hideFlags = HideFlags.HideAndDontSave;
        owned.Add(clone);
        return clone;
    }

    private static void SetBuildingConditions(
        BuildingSO building,
        params IBuildingCondition[] conditions)
    {
        FieldInfo field = typeof(BuildingSO).GetField(
            "OnBuildCondition",
            BindingFlags.Instance | BindingFlags.NonPublic);
        if (field == null)
        {
            throw new InvalidOperationException(
                "BuildingSO.OnBuildCondition serialization field is missing.");
        }
        field.SetValue(
            building,
            new List<IBuildingCondition>(conditions ?? Array.Empty<IBuildingCondition>()));
    }

    private static SerializedProperty Required(
        SerializedObject serialized,
        string path)
    {
        return serialized.FindProperty(path)
            ?? throw new InvalidOperationException(
                "Missing serialized property '" + path + "'.");
    }

    private static void RequireError(
        IReadOnlyList<string> errors,
        string expected,
        string message)
    {
        Require(
            errors.Any(error => error.Contains(expected, StringComparison.Ordinal)),
            message + " Errors: " + string.Join(" | ", errors));
    }

    private static void RequireClean(
        IReadOnlyList<string> errors,
        string context)
    {
        Require(
            errors.Count == 0,
            context + ": " + string.Join(" | ", errors));
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
