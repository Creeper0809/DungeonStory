using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public static class R26CatalogGuardDebugScenarios
{
    public static void RunAll()
    {
        var owned = new List<Object>();
        try
        {
            VerifyCurrentRegisteredCatalogs();
            VerifyEquipmentModuleIdentityGuards(owned);
            VerifySurgicalProcedureIdentityGuards(owned);
            VerifyResearchProjectIdentityGuards(owned);
            VerifyResearchRewardAuthorityGuards(owned);
            Debug.Log(
                "R26_CATALOG_GUARD_PASS;currentCatalogs=true;"
                + "moduleDuplicateRejected=true;surgeryDuplicateRejected=true;"
                + "projectDuplicateRejected=true;blueprintOwnerRejected=true;"
                + "sameProjectRewardNormalized=true;crossProjectRewardRejected=true");
        }
        finally
        {
            foreach (Object value in owned.Where(value => value != null))
            {
                Object.DestroyImmediate(value);
            }
        }
    }

    private static void VerifyCurrentRegisteredCatalogs()
    {
        var content = new ResourceGameContentCatalog(
            new UnityGameContentRootLoader());
        var modules = new ResourceEquipmentModuleCatalog(content);
        var procedures = new ResourceSurgicalProcedureCatalog(content);
        var projects = new ResourceResearchProjectCatalog(content);
        var rewards = new ResearchRewardCatalog(
            projects,
            new FixedFacilityCatalog(content),
            new ResourceEconomyContentCatalog(content),
            new ResourceCombatEquipmentCatalog(content),
            procedures,
            new ResourceEnvironmentalWorkwearCatalog(content));

        Require(modules.All.Count > 0,
            "Registered equipment module catalog is empty.");
        Require(procedures.Procedures.Count > 0,
            "Registered surgical procedure catalog is empty.");
        Require(projects.Projects.Count > 0,
            "Registered research project catalog is empty.");
        Require(rewards.All.Count > 0,
            "Registered research reward catalog is empty.");
        Require(procedures.Validate().Count == 0,
            "Registered surgical procedure catalog is invalid.");
        Require(projects.Validate().Count == 0,
            "Registered research project catalog is invalid.");
        Require(rewards.Validate().Count == 0,
            "Registered research reward catalog is invalid.");
    }

    private static void VerifyEquipmentModuleIdentityGuards(
        ICollection<Object> owned)
    {
        EquipmentModuleDefinitionSO first = CreateModule(
            "module:r26:duplicate", owned);
        EquipmentModuleDefinitionSO second = CreateModule(
            "module:r26:duplicate", owned);
        ExpectInvalidOperation(
            () => _ = new ResourceEquipmentModuleCatalog(
                new[] { first, second }),
            "Duplicate equipment module ID");

        EquipmentModuleDefinitionSO blank = CreateModule(string.Empty, owned);
        ExpectInvalidOperation(
            () => _ = new ResourceEquipmentModuleCatalog(new[] { blank }),
            "has no stable module ID");
    }

    private static void VerifySurgicalProcedureIdentityGuards(
        ICollection<Object> owned)
    {
        SurgicalProcedureSO first = CreateProcedure(
            "procedure:r26:duplicate", owned);
        SurgicalProcedureSO second = CreateProcedure(
            "procedure:r26:duplicate", owned);
        ExpectInvalidOperation(
            () => _ = new ResourceSurgicalProcedureCatalog(
                new[] { first, second }),
            "Duplicate surgical procedure ID");

        SurgicalProcedureSO blank = CreateProcedure(string.Empty, owned);
        ExpectInvalidOperation(
            () => _ = new ResourceSurgicalProcedureCatalog(new[] { blank }),
            "has no stable procedure ID");
    }

    private static void VerifyResearchProjectIdentityGuards(
        ICollection<Object> owned)
    {
        ResearchProjectSO duplicateFirst = CreateProject(
            "research:r26:duplicate", owned);
        ResearchProjectSO duplicateSecond = CreateProject(
            "research:r26:duplicate", owned);
        ExpectInvalidOperation(
            () => _ = new ResourceResearchProjectCatalog(
                new[] { duplicateFirst, duplicateSecond }),
            "Duplicate research project ID");

        ResearchProjectSO blank = CreateProject(string.Empty, owned);
        ExpectInvalidOperation(
            () => _ = new ResourceResearchProjectCatalog(new[] { blank }),
            "has no stable project ID");

        FacilityBlueprintSO blueprintA =
            ScriptableObject.CreateInstance<FacilityBlueprintSO>();
        blueprintA.id = 26001;
        owned.Add(blueprintA);
        FacilityBlueprintSO blueprintB =
            ScriptableObject.CreateInstance<FacilityBlueprintSO>();
        blueprintB.id = 26001;
        owned.Add(blueprintB);
        ResearchProjectSO ownerA = CreateProject(
            "research:r26:blueprint-owner-a", owned, blueprintA);
        ResearchProjectSO ownerB = CreateProject(
            "research:r26:blueprint-owner-b", owned, blueprintB);
        ExpectInvalidOperation(
            () => _ = new ResourceResearchProjectCatalog(
                new[] { ownerA, ownerB }),
            "is owned by multiple research projects");
    }

    private static void VerifyResearchRewardAuthorityGuards(
        ICollection<Object> owned)
    {
        ResearchProjectSO singleOwner = CreateProject(
            "research:r26:reward-owner", owned);
        singleOwner.UnlockCollection.Add(
            new BlueprintBuildingUnlock { buildingId = 26002 });
        singleOwner.UnlockCollection.Add(
            new BlueprintBuildingUnlock { buildingId = 26002 });
        var sameAuthority = new ResearchRewardCatalog(
            new ResourceResearchProjectCatalog(new[] { singleOwner }),
            null,
            EmptyResourceEconomyContentCatalog.Instance,
            EmptyEquipmentCatalog.Instance,
            new ResourceSurgicalProcedureCatalog(
                Array.Empty<SurgicalProcedureSO>()),
            null);
        Require(
            sameAuthority.All.Count(entry =>
                entry.Kind == ResearchRewardKind.Facility
                && entry.RewardId == "26002") == 1,
            "Same-project duplicate reward was not normalized exactly once.");

        ResearchProjectSO ownerA = CreateProject(
            "research:r26:reward-owner-a", owned);
        ownerA.UnlockCollection.Add(
            new BlueprintBuildingUnlock { buildingId = 26003 });
        ResearchProjectSO ownerB = CreateProject(
            "research:r26:reward-owner-b", owned);
        ownerB.UnlockCollection.Add(
            new BlueprintBuildingUnlock { buildingId = 26003 });
        var crossProject = new ResourceResearchProjectCatalog(
            new[] { ownerA, ownerB });
        ExpectInvalidOperation(
            () => _ = new ResearchRewardCatalog(
                crossProject,
                null,
                EmptyResourceEconomyContentCatalog.Instance,
                EmptyEquipmentCatalog.Instance,
                new ResourceSurgicalProcedureCatalog(
                    Array.Empty<SurgicalProcedureSO>()),
                null),
            "is owned by multiple projects");
    }

    private static EquipmentModuleDefinitionSO CreateModule(
        string moduleId,
        ICollection<Object> owned)
    {
        EquipmentModuleDefinitionSO value =
            ScriptableObject.CreateInstance<EquipmentModuleDefinitionSO>();
        SetString(value, "moduleId", moduleId);
        owned.Add(value);
        return value;
    }

    private static SurgicalProcedureSO CreateProcedure(
        string procedureId,
        ICollection<Object> owned)
    {
        SurgicalProcedureSO value =
            ScriptableObject.CreateInstance<SurgicalProcedureSO>();
        SetString(value, "procedureId", procedureId);
        owned.Add(value);
        return value;
    }

    private static ResearchProjectSO CreateProject(
        string projectId,
        ICollection<Object> owned,
        FacilityBlueprintSO blueprint = null)
    {
        ResearchProjectSO value =
            ScriptableObject.CreateInstance<ResearchProjectSO>();
        SetString(value, "projectId", projectId);
        if (blueprint != null)
        {
            SetObject(value, "blueprint", blueprint);
        }
        owned.Add(value);
        return value;
    }

    private static void SetString(
        ScriptableObject target,
        string propertyName,
        string value)
    {
        var serialized = new SerializedObject(target);
        SerializedProperty property = serialized.FindProperty(propertyName)
            ?? throw new InvalidOperationException(
                $"Missing serialized property '{propertyName}'.");
        property.stringValue = value ?? string.Empty;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetObject(
        ScriptableObject target,
        string propertyName,
        Object value)
    {
        var serialized = new SerializedObject(target);
        SerializedProperty property = serialized.FindProperty(propertyName)
            ?? throw new InvalidOperationException(
                $"Missing serialized property '{propertyName}'.");
        property.objectReferenceValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void ExpectInvalidOperation(
        Action action,
        string expectedMessage)
    {
        try
        {
            action();
        }
        catch (InvalidOperationException exception)
        {
            Require(
                exception.Message.IndexOf(
                    expectedMessage,
                    StringComparison.Ordinal) >= 0,
                $"Unexpected failure message: {exception.Message}");
            return;
        }

        throw new InvalidOperationException(
            $"Expected InvalidOperationException containing '{expectedMessage}'.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    private sealed class FixedFacilityCatalog : IFacilityShopCatalog
    {
        private readonly IReadOnlyDictionary<int, BuildingSO> byId;

        public FixedFacilityCatalog(IGameContentCatalog content)
        {
            Buildings = (content
                    ?? throw new ArgumentNullException(nameof(content)))
                .GetAll<BuildingSO>()
                .Where(value => value != null)
                .ToArray();
            Blueprints = content
                .GetAll<FacilityBlueprintSO>()
                .Where(value => value != null)
                .ToArray();
            byId = Buildings.ToDictionary(value => value.id);
        }

        public IReadOnlyCollection<BuildingSO> Buildings { get; }
        public IReadOnlyCollection<FacilityBlueprintSO> Blueprints { get; }

        public BuildingSO FindBuildingById(int buildingId) =>
            byId.TryGetValue(buildingId, out BuildingSO building)
                ? building
                : null;
    }

    private sealed class EmptyEquipmentCatalog : ICombatEquipmentCatalog
    {
        public static readonly EmptyEquipmentCatalog Instance = new();
        private static readonly IReadOnlyList<CombatEquipmentDefinitionSO>
            NoDefinitions = Array.Empty<CombatEquipmentDefinitionSO>();

        private EmptyEquipmentCatalog()
        {
        }

        public IReadOnlyList<CombatEquipmentDefinitionSO> All => NoDefinitions;

        public bool TryGet(
            string definitionId,
            out CombatEquipmentDefinitionSO definition)
        {
            definition = null;
            return false;
        }
    }
}
