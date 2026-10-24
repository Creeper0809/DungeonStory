#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public static class R20RelationGuardDebugScenarios
{
    private const string StatChangePath =
        "Assets/Resources/SO/Stock/Item/Onbuy/Hamburger.asset";
    private const string DiseasePath =
        "Assets/Resources/SO/Population/Diseases/Disease_disease-spore-lung.asset";
    private const string CrossDiseasePath =
        "Assets/Resources/SO/Population/Diseases/Disease_disease-red-fever.asset";
    private const string CareerPath =
        "Assets/Resources/SO/Population/Careers/career-position_foreman.asset";
    private const string FuneralPath =
        "Assets/Resources/SO/Population/Funeral/Funeral_Orc.asset";
    private const string RecipePath =
        "Assets/Resources/SO/Economy/Recipes/recipe_boar_stew.asset";
    private const string AmbitionPath =
        "Assets/Resources/SO/V20/Narrative/Ambitions/ambition_master-smith.asset";
    private const string MissingNeed = "need:r20-missing";
    private const string MissingFacility = "facility:r20-missing";
    private const string MissingProficiency = "proficiency:r20-missing";

    private static readonly string[] ExternalProductionFacilities =
    {
        "animal-pen",
        "butcher",
        "spoilage",
        "exterior-grass",
        "exterior-rock",
        "exterior-tree"
    };

    [MenuItem("DungeonStory/R20/Run Relation Guard Scenarios")]
    public static void RunAll()
    {
        Require(!Application.isPlaying,
            "R20 relation guard scenarios must run before Play mode.");

        StatChange statChange = RequireAsset<StatChange>(StatChangePath);
        DiseaseDefinitionSO disease =
            RequireAsset<DiseaseDefinitionSO>(DiseasePath);
        DiseaseDefinitionSO crossDisease =
            RequireAsset<DiseaseDefinitionSO>(CrossDiseasePath);
        CareerPositionDefinitionSO career =
            RequireAsset<CareerPositionDefinitionSO>(CareerPath);
        FuneralCultureSO funeral =
            RequireAsset<FuneralCultureSO>(FuneralPath);
        ProductionRecipeSO recipe =
            RequireAsset<ProductionRecipeSO>(RecipePath);
        CharacterAmbitionDefinitionSO ambition =
            RequireAsset<CharacterAmbitionDefinitionSO>(AmbitionPath);

        SourceSnapshot[] sources =
        {
            SourceSnapshot.Capture(statChange),
            SourceSnapshot.Capture(disease),
            SourceSnapshot.Capture(crossDisease),
            SourceSnapshot.Capture(career),
            SourceSnapshot.Capture(funeral),
            SourceSnapshot.Capture(recipe),
            SourceSnapshot.Capture(ambition)
        };

        Exception failure = null;
        try
        {
            ResourceGameContentCatalog content = new(
                new UnityGameContentRootLoader());
            ContentReferenceCatalog catalog =
                ContentReferenceCatalog.FromRoot(content.Root);

            RequireRegistered(content, disease);
            RequireRegistered(content, crossDisease);
            RequireRegistered(content, career);
            RequireRegistered(content, funeral);
            RequireRegistered(content, recipe);
            RequireRegistered(content, ambition);

            VerifyCharacterNeedGuard(content, catalog, statChange);
            VerifyDiseaseResponseScope(catalog, disease, crossDisease);
            VerifyRequiredFacilityGuards(catalog, career, funeral);
            VerifyProductionFacilityGuard(catalog, recipe);
            VerifyProficiencyRewardGuard(catalog, ambition);
        }
        catch (Exception exception)
        {
            failure = exception;
        }

        try
        {
            foreach (SourceSnapshot source in sources)
                source.RequireUnchanged();
        }
        catch (Exception sourceFailure)
        {
            failure = failure == null
                ? sourceFailure
                : new AggregateException(failure, sourceFailure);
        }

        if (failure != null)
            throw failure;

        Debug.Log(
            "R20_RELATION_GUARD=PASS; needs=6-valid-invalid-repair; "
            + "disease=response-scoped; facilities=career-funeral-recipe; "
            + "external-production=6; proficiency=unknown-blank-negative-repair; "
            + "source-json-dirty=preserved");
    }

    private static void VerifyCharacterNeedGuard(
        ResourceGameContentCatalog content,
        ContentReferenceCatalog catalog,
        StatChange source)
    {
        string[] authoredNeeds = content.Domain.CharacterNeeds
            .Select(value => value.id)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        string[] catalogNeeds = catalog.Entries(
                new ContentReferenceAttribute(
                    ContentReferenceKind.CharacterNeed))
            .Select(value => value.Id)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        Require(authoredNeeds.Length == 6,
            "The current root catalog must publish exactly six character needs.");
        Require(catalogNeeds.SequenceEqual(
                authoredNeeds,
                StringComparer.Ordinal),
            "CharacterNeed candidates do not match the actual root catalog.");
        Require(catalogNeeds.Contains(source.needId, StringComparer.Ordinal),
            "The actual StatChange need is absent from the root catalog.");

        StatChange clone = Object.Instantiate(source);
        clone.hideFlags = HideFlags.HideAndDontSave;
        string originalNeed = clone.needId;
        try
        {
            RequireClean(catalog, clone, "Actual StatChange need");

            clone.needId = MissingNeed;
            RequireError(
                catalog,
                clone,
                "CharacterNeed reference",
                "Unknown StatChange need");

            clone.needId = originalNeed;
            RequireClean(catalog, clone, "Repaired StatChange need");

            clone.needId = " ";
            RequireError(
                catalog,
                clone,
                "requires a character need",
                "Blank StatChange need");

            clone.needId = originalNeed;
            RequireClean(catalog, clone, "Retried StatChange need");
        }
        finally
        {
            Object.DestroyImmediate(clone);
        }
    }

    private static void VerifyDiseaseResponseScope(
        ContentReferenceCatalog catalog,
        DiseaseDefinitionSO source,
        DiseaseDefinitionSO otherDisease)
    {
        Require(source.fieldResponseIds != null
                && source.fieldResponseIds.Count > 0,
            "Disease fixture has no response to repair.");
        Require(otherDisease.fieldResponseIds != null
                && otherDisease.fieldResponseIds.Count > 0,
            "Cross-disease fixture has no response.");

        string crossDiseaseResponse = otherDisease.fieldResponseIds[0];
        Require(!DiseaseFieldResponseRuntime.AvailableResponseIds(
                    source.stableId,
                    source.vaccineAllowed)
                .Contains(crossDiseaseResponse, StringComparer.Ordinal),
            "Selected cross-disease response is unexpectedly valid for the owner.");

        DiseaseDefinitionSO clone = Object.Instantiate(source);
        clone.hideFlags = HideFlags.HideAndDontSave;
        clone.fieldResponseIds = new List<string>(source.fieldResponseIds);
        string originalResponse = clone.fieldResponseIds[0];
        try
        {
            RequireClean(catalog, clone, "Actual disease responses");

            clone.fieldResponseIds[0] = crossDiseaseResponse;
            RequireError(
                catalog,
                clone,
                "owner-scoped DiseaseResponse reference",
                "Cross-disease response");

            clone.fieldResponseIds[0] = originalResponse;
            RequireClean(catalog, clone, "Repaired disease responses");
        }
        finally
        {
            Object.DestroyImmediate(clone);
        }
    }

    private static void VerifyRequiredFacilityGuards(
        ContentReferenceCatalog catalog,
        CareerPositionDefinitionSO careerSource,
        FuneralCultureSO funeralSource)
    {
        CareerPositionDefinitionSO career = Object.Instantiate(careerSource);
        FuneralCultureSO funeral = Object.Instantiate(funeralSource);
        career.hideFlags = HideFlags.HideAndDontSave;
        funeral.hideFlags = HideFlags.HideAndDontSave;
        string careerFacility = career.requiredFacilityTag;
        string funeralFacility = funeral.requiredFacilityTag;
        try
        {
            Require(!string.IsNullOrWhiteSpace(careerFacility),
                "Actual facility-scoped career has no required facility tag.");
            Require(!string.IsNullOrWhiteSpace(funeralFacility),
                "Actual funeral culture has no required facility tag.");
            RequireClean(catalog, career, "Actual career facility tag");
            RequireClean(catalog, funeral, "Actual funeral facility tag");

            career.requiredFacilityTag = MissingFacility;
            RequireError(
                catalog,
                career,
                "BuildingSemanticTag reference",
                "Career facility typo");
            career.requiredFacilityTag = careerFacility;
            RequireClean(catalog, career, "Repaired career facility tag");

            funeral.requiredFacilityTag = MissingFacility;
            RequireError(
                catalog,
                funeral,
                "BuildingSemanticTag reference",
                "Funeral facility typo");
            funeral.requiredFacilityTag = funeralFacility;
            RequireClean(catalog, funeral, "Repaired funeral facility tag");
        }
        finally
        {
            Object.DestroyImmediate(career);
            Object.DestroyImmediate(funeral);
        }
    }

    private static void VerifyProductionFacilityGuard(
        ContentReferenceCatalog catalog,
        ProductionRecipeSO source)
    {
        ProductionRecipeSO clone = Object.Instantiate(source);
        clone.hideFlags = HideFlags.HideAndDontSave;
        string originalFacility = clone.FacilityTag;
        try
        {
            RequireClean(catalog, clone, "Actual production facility tag");

            SetSerializedString(clone, "facilityTag", MissingFacility);
            RequireError(
                catalog,
                clone,
                "ProductionFacilityTag reference",
                "Production facility typo");

            SetSerializedString(clone, "facilityTag", originalFacility);
            RequireClean(catalog, clone, "Repaired production facility tag");

            string[] catalogFacilities = catalog.Entries(
                    new ContentReferenceAttribute(
                        ContentReferenceKind.ProductionFacilityTag))
                .Select(value => value.Id)
                .ToArray();
            foreach (string facility in ExternalProductionFacilities)
            {
                Require(catalogFacilities.Contains(
                        facility,
                        StringComparer.Ordinal),
                    "Missing external production facility candidate '"
                    + facility + "'.");
                SetSerializedString(clone, "facilityTag", facility);
                RequireClean(
                    catalog,
                    clone,
                    "External production facility '" + facility + "'");
            }

            SetSerializedString(clone, "facilityTag", originalFacility);
            RequireClean(catalog, clone, "Restored production facility tag");
        }
        finally
        {
            Object.DestroyImmediate(clone);
        }
    }

    private static void VerifyProficiencyRewardGuard(
        ContentReferenceCatalog catalog,
        CharacterAmbitionDefinitionSO source)
    {
        V20ContentEffect authored = source.completionRewards
            ?.FirstOrDefault(value => value != null
                && value.kind == V20ContentEffectKind.ProficiencyExperience);
        Require(authored != null && authored.IsValid,
            "Actual ambition has no valid proficiency reward.");

        CharacterAmbitionDefinitionSO clone = Object.Instantiate(source);
        clone.hideFlags = HideFlags.HideAndDontSave;
        clone.completionRewards = (source.completionRewards
                ?? new List<V20ContentEffect>())
            .Select(CloneEffect)
            .ToList();
        V20ContentEffect candidate = CloneEffect(authored);
        clone.completionRewards.Add(candidate);
        string validTarget = candidate.targetId;
        float validAmount = candidate.amount;
        try
        {
            RequireClean(catalog, clone, "Actual proficiency reward clone");

            candidate.targetId = MissingProficiency;
            RequireError(
                catalog,
                clone,
                "Proficiency reference",
                "Unknown proficiency reward target");
            candidate.targetId = validTarget;
            RequireClean(catalog, clone, "Repaired proficiency reward target");

            candidate.targetId = " ";
            RequireError(
                catalog,
                clone,
                "proficiency reward requires a target and positive finite experience",
                "Blank proficiency reward target");
            candidate.targetId = validTarget;
            RequireClean(catalog, clone, "Retried proficiency reward target");

            candidate.amount = -1f;
            RequireError(
                catalog,
                clone,
                "proficiency reward requires a target and positive finite experience",
                "Negative proficiency reward amount");
            candidate.amount = validAmount;
            RequireClean(catalog, clone, "Repaired proficiency reward amount");
        }
        finally
        {
            Object.DestroyImmediate(clone);
        }
    }

    private static V20ContentEffect CloneEffect(V20ContentEffect source) =>
        source == null
            ? null
            : new V20ContentEffect
            {
                kind = source.kind,
                targetId = source.targetId,
                amount = source.amount,
                durationDays = source.durationDays
            };

    private static void SetSerializedString(
        ScriptableObject target,
        string fieldName,
        string value)
    {
        using SerializedObject serialized = new(target);
        serialized.Update();
        SerializedProperty property = serialized.FindProperty(fieldName);
        Require(property != null,
            "Missing serialized field '" + fieldName + "'.");
        property.stringValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void RequireError(
        ContentReferenceCatalog catalog,
        ScriptableObject clone,
        string expectedText,
        string label)
    {
        IReadOnlyList<string> errors = catalog.Validate(
            new[] { clone });
        Require(errors.Any(error => error.Contains(
                expectedText,
                StringComparison.Ordinal)),
            label + " was not rejected by ContentReferenceCatalog. Errors="
            + string.Join(" | ", errors));
    }

    private static void RequireClean(
        ContentReferenceCatalog catalog,
        ScriptableObject clone,
        string label)
    {
        IReadOnlyList<string> errors = catalog.Validate(
            new[] { clone });
        Require(errors.Count == 0,
            label + " failed validation: " + string.Join(" | ", errors));
    }

    private static T RequireAsset<T>(string path)
        where T : ScriptableObject
    {
        T asset = AssetDatabase.LoadAssetAtPath<T>(path);
        return asset != null
            ? asset
            : throw new InvalidOperationException(
                "Missing fixture source asset '" + path + "'.");
    }

    private static void RequireRegistered<T>(
        ResourceGameContentCatalog content,
        T source) where T : ScriptableObject
    {
        Require(content.GetAll<T>().Contains(source),
            "Fixture source '" + source.name
            + "' is absent from the actual root catalog.");
    }

    private sealed class SourceSnapshot
    {
        private readonly ScriptableObject source;
        private readonly string json;
        private readonly bool dirty;

        private SourceSnapshot(ScriptableObject source)
        {
            this.source = source;
            json = EditorJsonUtility.ToJson(source);
            dirty = EditorUtility.IsDirty(source);
        }

        internal static SourceSnapshot Capture(ScriptableObject source) =>
            new(source ?? throw new ArgumentNullException(nameof(source)));

        internal void RequireUnchanged()
        {
            Require(string.Equals(
                    EditorJsonUtility.ToJson(source),
                    json,
                    StringComparison.Ordinal),
                "Relation guard fixture changed source JSON for '"
                + source.name + "'.");
            Require(EditorUtility.IsDirty(source) == dirty,
                "Relation guard fixture changed source dirty state for '"
                + source.name + "'.");
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
#endif
