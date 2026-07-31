#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class SurgeryDebugScenarios
{
    private const string ReportPath = "Temp/surgery-system-contracts.tsv";

    private static readonly string[] RequiredResearchIds =
    {
        "research:medical:anatomy",
        "research:medical:surgery",
        "research:medical:prosthetics",
        "research:medical:organ-preservation",
        "research:medical:xenotransplant",
        "research:medical:aberrant-augmentation"
    };

    [MenuItem("DungeonStory/Debug/Medical/Run Surgery System Contracts")]
    public static void RunFromMenu()
    {
        if (!RunAll(logSuccess: true))
        {
            Debug.LogError("Surgery system contracts failed.");
        }
    }

    public static bool RunAll(bool logSuccess)
    {
        SurgeryContentAssetBuilder.RebuildAll();
        Directory.CreateDirectory("Temp");
        List<string> lines = new List<string> { "case\tresult\tdetails" };
        List<string> errors = new List<string>();

        Run("specialized_facilities", VerifySpecializedFacilities, lines, errors);
        Run("procedure_catalog", VerifyProcedureCatalog, lines, errors);
        Run("anatomy_profiles", VerifyAnatomyProfiles, lines, errors);
        Run("research_branch", VerifyResearchBranch, lines, errors);
        Run("prosthetic_recipes", VerifyProstheticRecipes, lines, errors);
        Run("risk_formula", VerifyRiskFormula, lines, errors);
        Run("corpse_extraction_ledger", VerifyExtractionLedger, lines, errors);
        Run("unique_part_save_data", VerifyUniquePartSaveData, lines, errors);
        Run("work_and_stat_contract", VerifyWorkAndStatContract, lines, errors);

        File.WriteAllLines(ReportPath, lines);
        foreach (string error in errors)
        {
            Debug.LogError(error);
        }

        if (errors.Count == 0 && logSuccess)
        {
            Debug.Log($"Surgery system contracts PASS. Report: {ReportPath}");
        }

        return errors.Count == 0;
    }

    private static string VerifySpecializedFacilities()
    {
        BuildingSO[] buildings = LoadAssets<BuildingSO>(
            "Assets/Resources/SO/Building/Medical");
        Require(buildings.Length == 13, $"expected 13 medical facilities, got {buildings.Length}");
        Require(
            buildings.Select(building => building.id).Distinct().Count() == buildings.Length,
            "medical building ids were not unique");
        Require(
            buildings.All(building => building.sprite != null),
            "a medical facility had no authored sprite");
        Require(
            buildings.All(building =>
                building.Abilities.OfType<ISurgicalFacilityAbility>().Any()),
            "a medical facility had no surgical ability");
        Require(
            buildings.All(building =>
                building.Facility?.SupportsRole(FacilityRole.Medical) == true),
            "a medical facility was not assigned FacilityRole.Medical");

        SurgeryFacilityTag covered = buildings
            .SelectMany(building => building.Abilities.OfType<ISurgicalFacilityAbility>())
            .Aggregate(
                SurgeryFacilityTag.None,
                (current, ability) => current | ability.FacilityTags);
        SurgeryFacilityTag required = Enum.GetValues(typeof(SurgeryFacilityTag))
            .Cast<SurgeryFacilityTag>()
            .Aggregate(SurgeryFacilityTag.None, (current, value) => current | value);
        Require((covered & required) == required, $"facility tag coverage incomplete: {covered}");
        Require(
            buildings.Any(building =>
                building.Abilities.OfType<BuildingOrganStorageAbility>().Any()),
            "organ storage facility was missing");
        Require(
            buildings.Any(building =>
                building.Abilities.OfType<BuildingProstheticAssemblyAbility>().Any()),
            "prosthetic assembly facility was missing");
        return "13 medical facilities cover every surgery and support tag";
    }

    private static string VerifyProcedureCatalog()
    {
        SurgicalProcedureSO[] procedures = LoadAssets<SurgicalProcedureSO>(
            "Assets/Resources/SO/Medical/Procedures");
        ResourceSurgicalProcedureCatalog catalog =
            new ResourceSurgicalProcedureCatalog(procedures);
        Require(procedures.Length == 13, $"expected 13 procedures, got {procedures.Length}");
        Require(catalog.Validate().Count == 0, string.Join(" | ", catalog.Validate()));
        Require(
            procedures.All(procedure =>
                procedure.RequiredWork > 0f
                && procedure.RequiredFacilityTags != SurgeryFacilityTag.None),
            "a procedure had no work or facility requirement");
        Require(
            procedures.All(procedure =>
                procedure.Materials.Count > 0
                && procedure.Materials.All(material =>
                    !string.IsNullOrWhiteSpace(material.itemId)
                    && material.quantity > 0)),
            "a procedure did not require physical material delivery");
        Require(
            procedures.Any(procedure =>
                procedure.Kind == SurgicalProcedureKind.ExtractOrgan
                && procedure.AllowsCorpseSubject),
            "corpse organ extraction procedure was missing");
        Require(
            procedures.Any(procedure =>
                procedure.Kind == SurgicalProcedureKind.Rehabilitation
                && procedure.Effects.OfType<ReduceSurgicalBurdenEffect>().Any()),
            "rehabilitation did not reduce post-operative burdens");
        return "13 procedures require work, facilities, hauled materials, and valid effects";
    }

    private static string VerifyAnatomyProfiles()
    {
        AnatomyProfileSO[] profiles = LoadAssets<AnatomyProfileSO>(
            "Assets/Resources/SO/Medical/Anatomy");
        ResourceAnatomyProfileCatalog catalog =
            new ResourceAnatomyProfileCatalog(profiles);
        Require(profiles.Length == 6, $"expected 6 anatomy profiles, got {profiles.Length}");
        Require(catalog.Validate().Count == 0, string.Join(" | ", catalog.Validate()));

        AnatomyProfileDefinition humanoid = catalog.GetDefaultHumanoid();
        Require(humanoid.Nodes.Count >= 16, "humanoid anatomy did not contain the full node set");
        RequireNode(humanoid, "brain", vital: true);
        RequireNode(humanoid, "heart", vital: true);
        RequireNode(humanoid, "torso", vital: true);
        RequirePaired(humanoid, "eyes", 2);
        RequirePaired(humanoid, "lungs", 2);
        RequirePaired(humanoid, "kidneys", 2);
        RequirePaired(humanoid, "arms", 2);
        RequirePaired(humanoid, "legs", 2);
        Require(
            catalog.GetForSpecies("shadow_wolf").AnatomyFamily == "quadruped",
            "quadruped wildlife did not resolve to its anatomy");
        Require(
            catalog.GetForSpecies("Slime").ProfileId == "anatomy:slime",
            "slime did not resolve to its dedicated anatomy");
        return "six anatomy profiles resolve stable vital and paired nodes";
    }

    private static string VerifyResearchBranch()
    {
        ResearchProjectSO[] projects = LoadAssets<ResearchProjectSO>(
            "Assets/Resources/SO/Research/Projects");
        ResourceResearchProjectCatalog catalog =
            new ResourceResearchProjectCatalog(projects);
        Require(
            projects.Length >= 78,
            $"expected at least 78 research projects, got {projects.Length}");
        Require(catalog.Validate().Count == 0, string.Join(" | ", catalog.Validate()));
        foreach (string id in RequiredResearchIds)
        {
            Require(
                catalog.TryGet(id, out ResearchProjectSO project)
                && project.Field == ResearchField.SurgeryAndTransplant,
                $"missing surgery research node {id}");
        }

        RequirePrerequisite(
            catalog,
            "research:medical:surgery",
            "research:medical:anatomy");
        RequirePrerequisite(
            catalog,
            "research:medical:prosthetics",
            "research:medical:surgery");
        RequirePrerequisite(
            catalog,
            "research:medical:xenotransplant",
            "research:medical:organ-preservation");
        RequirePrerequisite(
            catalog,
            "research:medical:aberrant-augmentation",
            "research:medical:xenotransplant");
        return "the research graph includes the six surgery nodes and prerequisites";
    }

    private static string VerifyProstheticRecipes()
    {
        ProductionRecipeSO[] recipes = LoadAssets<ProductionRecipeSO>(
                "Assets/Resources/SO/Economy/Recipes")
            .Where(recipe => recipe.RecipeId.StartsWith(
                "recipe:surgery:",
                StringComparison.Ordinal))
            .ToArray();
        Require(recipes.Length == 3, $"expected 3 prosthetic recipes, got {recipes.Length}");
        Require(
            recipes.All(recipe =>
                recipe.WorkTypeId == BuiltInWorkTypeIds.Craft
                && recipe.RequiredWork > 0f
                && recipe.Inputs.Count > 0
                && recipe.Outputs.Count == 1),
            "prosthetic recipes did not use work, materials, and a unique output");
        return "three prosthetic recipes use physical inputs and cumulative craft work";
    }

    private static string VerifyRiskFormula()
    {
        SurgicalProcedureSO procedure = LoadAssets<SurgicalProcedureSO>(
                "Assets/Resources/SO/Medical/Procedures")
            .First();
        SurgeryRiskEvaluator evaluator = new SurgeryRiskEvaluator();
        SurgicalFacilitySnapshot poor = new SurgicalFacilitySnapshot(
            null,
            procedure.RequiredFacilityTags,
            0f,
            1f,
            0f,
            0f,
            Array.Empty<BuildableObject>(),
            string.Empty);
        SurgicalFacilitySnapshot good = new SurgicalFacilitySnapshot(
            null,
            procedure.RequiredFacilityTags,
            1f,
            1.5f,
            0.25f,
            1f,
            Array.Empty<BuildableObject>(),
            string.Empty);
        SurgicalSubjectRef subject = new SurgicalSubjectRef
        {
            kind = SurgicalSubjectKind.Character,
            subjectId = "patient:test",
            speciesId = "Human"
        };
        SurgeryRiskBreakdown poorRisk =
            evaluator.Evaluate(null, subject, procedure, poor, 0.8f, 0.5f);
        SurgeryRiskBreakdown goodRisk =
            evaluator.Evaluate(null, subject, procedure, good, 0f, 0f);
        Require(goodRisk.successChance > poorRisk.successChance, "facility and stability did not affect success");
        Require(
            poorRisk.successChance >= 0.05f && goodRisk.successChance <= 0.98f,
            "success clamp was violated");
        Require(
            Mathf.Approximately(
                poorRisk.deathChance,
                (1f - poorRisk.successChance) * 0.1f),
            "fatal failure weighting changed");
        return "risk uses facility, cleanliness, instability, compatibility, and fixed clamps";
    }

    private static string VerifyExtractionLedger()
    {
        SurgeryExtractionLedger ledger = new SurgeryExtractionLedger();
        Require(
            ledger.TryMarkExtracted("corpse:test", "heart", out _),
            "first extraction was rejected");
        Require(
            !ledger.TryMarkExtracted("corpse:test", "heart", out string reason)
            && !string.IsNullOrWhiteSpace(reason),
            "duplicate extraction was accepted");
        Require(
            ledger.TryMarkExtracted("corpse:test", "lung:left", out _),
            "another organ could not be extracted");

        SurgeryExtractionLedger restored = new SurgeryExtractionLedger();
        restored.Restore(ledger.Capture(), new List<string>());
        Require(restored.IsExtracted("corpse:test", "heart"), "extraction state was not restored");
        return "corpse organs are extracted once and survive save restoration";
    }

    private static string VerifyUniquePartSaveData()
    {
        SurgicalPartInstance part = new SurgicalPartInstance
        {
            partInstanceId = "surgical-part:test",
            kind = SurgicalPartKind.NaturalOrgan,
            nodeId = "eye:left",
            displayName = "룬사슴의 눈",
            donorId = "wildlife:test",
            donorSpeciesId = "rune_deer",
            anatomyFamily = "quadruped",
            quality = 1.2f,
            freshnessSeconds = 360f,
            specialEffectId = "graft:rune-deer-night-sight",
            specialEffectStrength = 1f,
            worldStackId = "stack:test"
        };
        DungeonSurgerySaveData data = new DungeonSurgerySaveData
        {
            parts = new List<SurgicalPartInstance> { part },
            orders = new List<SurgeryOrder>
            {
                new SurgeryOrder
                {
                    orderId = "surgery:test",
                    procedureId = "procedure:emergency-suture",
                    state = SurgeryOrderState.Procedure,
                    reachedClinicalStages = new List<SurgeryOrderState>
                    {
                        SurgeryOrderState.Anesthetizing,
                        SurgeryOrderState.Incision,
                        SurgeryOrderState.Procedure
                    }
                }
            },
            corpseFreshness = new List<SurgicalCorpseFreshnessState>
            {
                new SurgicalCorpseFreshnessState
                {
                    stackId = "corpse:test",
                    remainingFreshnessSeconds = 180f
                }
            }
        };
        DungeonSurgerySaveData restored = JsonUtility.FromJson<DungeonSurgerySaveData>(
            JsonUtility.ToJson(data));
        Require(restored.parts.Count == 1, "unique surgical part was lost");
        Require(
            restored.parts[0].partInstanceId == part.partInstanceId
            && restored.parts[0].specialEffectId == part.specialEffectId,
            "donor or graft metadata changed during save");
        Require(
            restored.corpseFreshness.Single().remainingFreshnessSeconds == 180f,
            "corpse freshness changed during save");
        Require(
            restored.orders.Single().reachedClinicalStages.SequenceEqual(
                data.orders.Single().reachedClinicalStages),
            "clinical stage history changed during save");
        return "unique donor, graft, freshness, and clinical stages round-trip through V16 section data";
    }

    private static string VerifyWorkAndStatContract()
    {
        Require(
            BuiltInWorkTypeIds.Surgery.Value == "work:surgery",
            "surgery work id was unstable");
        Require(
            WorkTypeCatalog.TryGet(
                BuiltInWorkTypeIds.Surgery,
                out WorkTypeDefinition definition)
            && definition.DefaultPriority == WorkPriorityLevel.Priority1,
            "surgery was not Priority1");
        Require(
            Enum.GetValues(typeof(CharacterStatType)).Length == 12,
            "character stat count was not 12");
        CharacterSkillSystemSettingsSO settings =
            ScriptableObject.CreateInstance<CharacterSkillSystemSettingsSO>();
        try
        {
            Require(settings.initialStatTotal == 60, "initial stat total was not 60");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(settings);
        }

        return "surgery is a registered Priority1 work type and Medical is the twelfth stat";
    }

    private static T[] LoadAssets<T>(string folder)
        where T : UnityEngine.Object
    {
        return AssetDatabase.FindAssets($"t:{typeof(T).Name}", new[] { folder })
            .Select(AssetDatabase.GUIDToAssetPath)
            .Select(AssetDatabase.LoadAssetAtPath<T>)
            .Where(asset => asset != null)
            .ToArray();
    }

    private static void RequireNode(
        AnatomyProfileDefinition profile,
        string nodeId,
        bool vital)
    {
        Require(profile.TryGetNode(nodeId, out AnatomyNodeDefinition node), $"missing anatomy node {nodeId}");
        Require(node.Vital == vital, $"anatomy node {nodeId} vital flag was incorrect");
    }

    private static void RequirePaired(
        AnatomyProfileDefinition profile,
        string pairedGroupId,
        int count)
    {
        Require(
            profile.Nodes.Count(node =>
                string.Equals(
                    node.PairedGroupId,
                    pairedGroupId,
                    StringComparison.Ordinal)) == count,
            $"paired anatomy group {pairedGroupId} did not contain {count} nodes");
    }

    private static void RequirePrerequisite(
        IResearchProjectCatalog catalog,
        string projectId,
        string prerequisiteId)
    {
        Require(catalog.TryGet(projectId, out ResearchProjectSO project), $"missing project {projectId}");
        Require(
            project.Prerequisites.Any(prerequisite =>
                string.Equals(
                    prerequisite.ProjectId.Value,
                    prerequisiteId,
                    StringComparison.Ordinal)),
            $"{projectId} did not depend on {prerequisiteId}");
    }

    private static void Run(
        string name,
        Func<string> test,
        ICollection<string> lines,
        ICollection<string> errors)
    {
        try
        {
            string details = test();
            lines.Add($"{name}\tPASS\t{details}");
        }
        catch (Exception exception)
        {
            string message = $"{name}: {exception.Message}";
            lines.Add($"{name}\tFAIL\t{exception.Message}");
            errors.Add(message);
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
#endif
