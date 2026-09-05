#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Reviewed content-authoring manifest for recipe mass explanations. This is
/// editor data authority only; runtime resolution remains recipe-ID agnostic.
/// </summary>
public static class V27ReviewedProductionMassExplanationCatalog
{
    // The reviewed external-addition assets remain an editor authoring concern,
    // but they may only be applied after prepared-output current-format can
    // persist, route and restore the matching non-physical receipt.
    public const bool ExternalAdditionRuntimeReceiptReady = true;

    private static readonly string[] V22TailoringPhysicalBomLossRecipeIds =
    {
        "recipe:v22:apparel:apron",
        "recipe:v22:apparel:boots",
        "recipe:v22:apparel:ceremonial-dress",
        "recipe:v22:apparel:cloak",
        "recipe:v22:apparel:cold-work-suit",
        "recipe:v22:apparel:daily-robe",
        "recipe:v22:apparel:envoy-coat",
        "recipe:v22:apparel:farmer-workwear",
        "recipe:v22:apparel:festival-vest",
        "recipe:v22:apparel:formal-coat",
        "recipe:v22:apparel:golem-functional-lining",
        "recipe:v22:apparel:hauling-harness",
        "recipe:v22:apparel:heat-work-suit",
        "recipe:v22:apparel:hooded-robe",
        "recipe:v22:apparel:keeper-coat",
        "recipe:v22:apparel:miner-workwear",
        "recipe:v22:apparel:mourning-clothes",
        "recipe:v22:apparel:raincoat",
        "recipe:v22:apparel:ritual-robe",
        "recipe:v22:apparel:rune-cold-suit",
        "recipe:v22:apparel:skirt",
        "recipe:v22:apparel:smith-apron",
        "recipe:v22:apparel:spore-garden-cloak",
        "recipe:v22:apparel:sterile-gown",
        "recipe:v22:apparel:surgical-apron",
        "recipe:v22:apparel:trousers",
        "recipe:v22:apparel:tunic",
        "recipe:v22:apparel:waterproof-work-suit",
        "recipe:v22:apparel:weapon-vigil-cloak",
        "recipe:v22:apparel:wing-cloak",
        "recipe:v22:apparel:wing-harness",
        "recipe:v22:apparel:work-shirt"
    };

    private static readonly string[] V22WeavingPhysicalBomLossRecipeIds =
    {
        "recipe:v22:sewing-thread",
        "recipe:v22:weave:common-wool",
        "recipe:v22:weave:deep-goat-wool",
        "recipe:v22:weave:ember-cotton",
        "recipe:v22:weave:frost-linen",
        "recipe:v22:weave:frost-wool",
        "recipe:v22:weave:mire-canvas",
        "recipe:v22:weave:shade-cloth",
        "recipe:v22:weave:spore-hemp"
    };

    private static readonly HashSet<string> V22TailoringPhysicalBomLossRecipeSet =
        new(V22TailoringPhysicalBomLossRecipeIds, StringComparer.Ordinal);

    private static readonly HashSet<string> V22WeavingPhysicalBomLossRecipeSet =
        new(V22WeavingPhysicalBomLossRecipeIds, StringComparer.Ordinal);

    private static readonly string[] ResearchMechanicalPhysicalBomLossRecipeIds =
    {
        "recipe:component:blacksteel-defense-plate",
        "recipe:component:insulated-wiring",
        "recipe:component:machine-parts",
        "recipe:component:material-test-coupon",
        "recipe:component:reclaimed-water-filter",
        "recipe:material:barrel-steel",
        "recipe:material:chain-mesh",
        "recipe:material:plate-blank",
        "recipe:tool:administrative-seal",
        "recipe:tool:maintenance-kit",
        "recipe:tool:prisoner-work-kit",
        "recipe:tool:prospecting-kit"
    };

    private static readonly string[] ResearchArcanePhysicalBomLossRecipeIds =
    {
        "recipe:component:mana-shield-plate",
        "recipe:component:precision-optics",
        "recipe:component:rune-conductor",
        "recipe:component:rune-leather-lining",
        "recipe:material:mana-alloy",
        "recipe:material:sterile-composite"
    };

    private static readonly string[] CoreTextilePhysicalBomLossRecipeIds =
    {
        "recipe:bedding-straw",
        "recipe:cloth"
    };

    private static readonly string[] CoreCookingPhysicalBomLossRecipeIds =
    {
        "recipe:expedition-ration-pack",
        "recipe:garden-meal",
        "recipe:grain-porridge",
        "recipe:moonflower-tea",
        "recipe:mushroom-soup",
        "recipe:preserved-ration",
        "recipe:roasted-meat",
        "recipe:root-stew",
        "recipe:salted-meat-stew"
    };

    private static readonly string[] CuttingLossRecipeIds =
    {
        "recipe:bedding-animal",
        "recipe:bolt-bone",
        "recipe:cold-work-suit",
        "recipe:component:brigandine-padding",
        "recipe:component:precision-parts",
        "recipe:component:price-board",
        "recipe:component:room-partition-kit",
        "recipe:tool:hauling-harness",
        "recipe:v22:apparel:belt",
        "recipe:v22:apparel:blouse",
        "recipe:v22:apparel:chest-wrap",
        "recipe:v22:apparel:contract-sash",
        "recipe:v22:apparel:footwraps",
        "recipe:v22:apparel:gloves",
        "recipe:v22:apparel:hat",
        "recipe:v22:apparel:horn-ring",
        "recipe:v22:apparel:loincloth-underwear",
        "recipe:v22:apparel:long-underpants",
        "recipe:v22:apparel:lower-underwear",
        "recipe:v22:apparel:scarf",
        "recipe:v22:apparel:shorts",
        "recipe:v22:apparel:sky-chorus-shawl",
        "recipe:v22:apparel:sleep-bottom",
        "recipe:v22:apparel:sleep-top",
        "recipe:v22:apparel:slime-warming-pad",
        "recipe:v22:apparel:smoke-protection-hood",
        "recipe:v22:apparel:socks",
        "recipe:v22:apparel:spore-protection-hood",
        "recipe:v22:apparel:tail-guard",
        "recipe:v22:apparel:tail-ribbon",
        "recipe:v22:apparel:undershirt",
        "recipe:v22:weave:cave-silk"
    };

    private static readonly HashSet<string> CuttingLossRecipeSet =
        new(CuttingLossRecipeIds, StringComparer.Ordinal);

    private static readonly string[] FiberProcessingLossRecipeIds =
    {
        "recipe:v22:spin-powered:cave-silk",
        "recipe:v22:spin-powered:common-wool",
        "recipe:v22:spin-powered:deep-goat-wool",
        "recipe:v22:spin-powered:dreamweave",
        "recipe:v22:spin-powered:ember-cotton",
        "recipe:v22:spin-powered:frost-linen",
        "recipe:v22:spin-powered:frost-wool",
        "recipe:v22:spin-powered:mire-canvas",
        "recipe:v22:spin-powered:shade-cloth",
        "recipe:v22:spin-powered:spore-hemp",
        "recipe:v22:spin:cave-silk",
        "recipe:v22:spin:common-wool",
        "recipe:v22:spin:deep-goat-wool",
        "recipe:v22:spin:dreamweave",
        "recipe:v22:spin:ember-cotton",
        "recipe:v22:spin:frost-linen",
        "recipe:v22:spin:frost-wool",
        "recipe:v22:spin:mire-canvas",
        "recipe:v22:spin:shade-cloth",
        "recipe:v22:spin:spore-hemp"
    };

    private static readonly HashSet<string> FiberProcessingLossRecipeSet =
        new(FiberProcessingLossRecipeIds, StringComparer.Ordinal);

    private static readonly string[] SmeltingLossRecipeIds =
    {
        "recipe:blacksteel",
        "recipe:gold-ingot",
        "recipe:iron-ingot",
        "recipe:iron-slag-block",
        "recipe:material:lead-ingot",
        "recipe:steel-ingot"
    };

    private static readonly string[] CombustionLossRecipeIds =
    {
        "recipe:charcoal"
    };

    private static readonly string[] MoistureLossRecipeIds =
    {
        "recipe:boar-stew",
        "recipe:jerky",
        "recipe:salted-meat"
    };

    private static readonly string[] MillingLossRecipeIds =
    {
        "recipe:starch"
    };

    private static readonly string[] ExtractionLossRecipeIds =
    {
        "recipe:leather",
        "recipe:rot-toxin",
        "recipe:solvent",
        "recipe:tallow"
    };

    private static readonly string[] WeavingLossRecipeIds =
    {
        "recipe:wool-cloth"
    };

    private static readonly string[] ProjectileCraftLossRecipeIds =
    {
        "recipe:ammo:blacksteel-bolt",
        "recipe:arrow-rune",
        "recipe:bolt-rune",
    };

    private static readonly string[] AmmunitionInfusionLossRecipeIds =
    {
        "recipe:ammo:incendiary-arrow",
        "recipe:ammo:incendiary-bolt",
        "recipe:ammo:mana-disruptor-bolt"
    };

    private static readonly string[] AmmunitionPressLossRecipeIds =
    {
        "recipe:ammo:armor-piercing-cartridge",
        "recipe:ammo:blasting-charge",
        "recipe:ammo:paper-cartridge",
        "recipe:ammo:rune-cartridge",
        "recipe:ammo:scatter-cartridge",
        "recipe:ammo:signal-flare",
        "recipe:ammo:smoke-cartridge",
        "recipe:ammo:trap-canister"
    };

    private static readonly string[] PharmacologyLossRecipeIds =
    {
        "recipe:advanced-medicine",
        "recipe:antidote",
        "recipe:antiseptic",
        "recipe:blood-stimulant",
        "recipe:dreamleaf-analgesic",
        "recipe:fang-poison",
        "recipe:hallucinogenic-distillate",
        "recipe:mana-awakener",
        "recipe:resin-balm",
        "recipe:ritual-reagent",
        "recipe:standard-medicine",
        "recipe:vitality-tonic"
    };

    private static readonly string[] ClinicalPreparationLossRecipeIds =
    {
        "recipe:medical:cross-lineage-medium",
        "recipe:medical:fertility-treatment",
        "recipe:medical:isolation-care-kit",
        "recipe:medical:organ-regeneration-scaffold",
        "recipe:medical:regenerative-medium",
        "recipe:medical:rejuvenation-serum",
        "recipe:medical:sterile-bandage",
        "recipe:medical:trauma-care-kit",
        "recipe:medical:whole-body-regeneration-medium"
    };

    private static readonly string[] ClinicalHardwareLossRecipeIds =
    {
        "recipe:medical:organ-preservation-canister",
        "recipe:medical:trait-analysis-kit"
    };

    private static readonly string[] VaccineLossRecipeIds =
    {
        "recipe:medicine:vaccine:blood-wasting",
        "recipe:medicine:vaccine:cave-flu",
        "recipe:medicine:vaccine:gut-rot",
        "recipe:medicine:vaccine:mana-pox",
        "recipe:medicine:vaccine:red-fever",
        "recipe:medicine:vaccine:slime-blight",
        "recipe:medicine:vaccine:spore-lung"
    };

    private static readonly string[] FoodPreparationLossRecipeIds =
    {
        "recipe:brined-vegetable",
        "recipe:dough",
        "recipe:egg-pancake",
        "recipe:lavish-meat",
        "recipe:lavish-vegan",
        "recipe:preserved-vegetable"
    };

    private static readonly string[] FermentationLossRecipeIds =
    {
        "recipe:fermented-pickle",
        "recipe:twilight-beer",
        "recipe:young-wine"
    };

    private static readonly string[] SyrupReductionLossRecipeIds =
    {
        "recipe:grape-syrup",
        "recipe:syrup"
    };

    private static readonly string[] TextileComponentLossRecipeIds =
    {
        "recipe:component:blast-coat-shell",
        "recipe:component:dreamweave-rune-lining",
        "recipe:component:rune-leather-strap"
    };

    private static readonly string[] PaperComponentLossRecipeIds =
    {
        "recipe:component:engineering-drawing",
        "recipe:component:factory-installation-plan",
        "recipe:component:prototype-package"
    };

    private static readonly string[] MechanicalComponentLossRecipeIds =
    {
        "recipe:component:climate-control-manifold",
        "recipe:component:corridor-detonator",
        "recipe:component:golem-core-case",
        "recipe:component:growth-frame",
        "recipe:component:lead-counterweight",
        "recipe:component:powered-armor-joint",
        "recipe:component:sealed-seasonal-container",
        "recipe:component:siege-counterweight",
        "recipe:component:siege-reinforcement-kit",
        "recipe:component:stock-sensor-panel",
        "recipe:component:waterwheel-drive-shaft"
    };

    private static readonly string[] RuneComponentLossRecipeIds =
    {
        "recipe:component:rune-bus-coupler",
        "recipe:component:rune-control-panel",
        "recipe:component:rune-purification-crystal",
        "recipe:component:rune-tuning-shield",
        "recipe:component:temporal-stasis-seal"
    };

    private static readonly string[] CompostLossRecipeIds =
    {
        "recipe:compost-animal",
        "recipe:compost-manure",
        "recipe:compost-mixed",
        "recipe:compost-plant"
    };

    private static readonly string[] LowFuelLossRecipeIds =
    {
        "recipe:low-fuel-animal",
        "recipe:low-fuel-manure",
        "recipe:low-fuel-plant",
        "recipe:low-fuel-rot"
    };

    private static readonly string[] DecorationLossRecipeIds =
    {
        "recipe:bone-charm",
        "recipe:craft:dreamweave-ritual-banner",
        "recipe:trail-charm"
    };

    private static readonly string[] GoldCraftLossRecipeIds =
    {
        "recipe:gold-leaf",
        "recipe:gold-ornament"
    };

    private static readonly string[] StoneCraftLossRecipeIds =
    {
        "recipe:stone-block",
        "recipe:stone-ornament"
    };

    private static readonly string[] TextileFinishedLossRecipeIds =
    {
        "recipe:rune-cold-suit",
        "recipe:slime-warming-pad",
        "recipe:v22:mending-scrap"
    };

    private static readonly string[] RecordBindingLossRecipeIds =
    {
        "recipe:book:seasonal-almanac",
        "recipe:record:arcane-index",
        "recipe:record:breeding-ledger",
        "recipe:record:career-ledger"
    };

    private static readonly string[] MedicalComponentLossRecipeIds =
    {
        "recipe:medical:mana-core-case",
        "recipe:medical:rune-hibernation-catalyst",
        "recipe:medical:slime-coagulation-frame",
        "recipe:medical:sterile-mycelium-graft"
    };

    private static readonly string[] ToolCraftLossRecipeIds =
    {
        "recipe:tool:alloy-crucible",
        "recipe:tool:banquet-cart",
        "recipe:tool:deep-shaft-hoist",
        "recipe:tool:inspection-gauge",
        "recipe:tool:mana-probe",
        "recipe:tool:powered-tool-head",
        "recipe:tool:precision-gauge",
        "recipe:tool:reinforced-restraint",
        "recipe:tool:rune-identification-lens",
        "recipe:tool:weather-observation-kit"
    };

    private static readonly string[] ProjectileShaftCraftLossRecipeIds =
    {
        "recipe:arrow-bone",
        "recipe:arrow-iron",
        "recipe:arrow-steel",
        "recipe:bolt-iron",
        "recipe:bolt-steel"
    };

    private static readonly string[] ProstheticCraftLossRecipeIds =
    {
        "recipe:surgery:prosthetic-arm",
        "recipe:surgery:prosthetic-leg"
    };

    private static readonly string[] TreatedLumberLossRecipeIds =
    {
        "recipe:treated-lumber"
    };

    private static readonly string[] NonReproducibleLossRecipeIds =
    {
        "recipe:ration-mixture"
    };

    private static readonly IReadOnlyDictionary<string, ReviewedLossPolicy>
        ReviewedLossPolicies = BuildReviewedLossPolicies();

    static V27ReviewedProductionMassExplanationCatalog()
    {
        if (CuttingLossRecipeSet.Count != CuttingLossRecipeIds.Length
            || !CuttingLossRecipeIds.SequenceEqual(
                CuttingLossRecipeIds.OrderBy(
                    value => value,
                    StringComparer.Ordinal)))
        {
            throw new InvalidOperationException(
                "Reviewed production mass-explanation IDs must be unique and ordinal-sorted.");
        }
        if (FiberProcessingLossRecipeSet.Count !=
                FiberProcessingLossRecipeIds.Length
            || !FiberProcessingLossRecipeIds.SequenceEqual(
                FiberProcessingLossRecipeIds.OrderBy(
                    value => value,
                    StringComparer.Ordinal))
            || FiberProcessingLossRecipeIds.Any(
                CuttingLossRecipeSet.Contains))
        {
            throw new InvalidOperationException(
                "Reviewed fiber-processing IDs must be unique, disjoint and ordinal-sorted.");
        }
        if (ReviewedLossPolicies.Count != 294)
        {
            throw new InvalidOperationException(
                "Reviewed production loss-policy count drifted: "
                + ReviewedLossPolicies.Count + "/294.");
        }
        if (NonReproducibleLossRecipeIds.Length != 1
            || NonReproducibleLossRecipeIds.Distinct(StringComparer.Ordinal)
                .Count() != NonReproducibleLossRecipeIds.Length
            || !NonReproducibleLossRecipeIds.SequenceEqual(
                NonReproducibleLossRecipeIds.OrderBy(
                    value => value,
                    StringComparer.Ordinal))
            || NonReproducibleLossRecipeIds.Any(
                ReviewedLossPolicies.ContainsKey))
        {
            throw new InvalidOperationException(
                "Non-reproducible process-loss exclusions drifted.");
        }
    }

    public static IReadOnlyList<string> CaptureCuttingLossRecipeIds() =>
        Array.AsReadOnly((string[])CuttingLossRecipeIds.Clone());

    public static IReadOnlyList<string> CaptureFiberProcessingLossRecipeIds() =>
        Array.AsReadOnly((string[])FiberProcessingLossRecipeIds.Clone());

    public static bool RequiresV22TailoringPhysicalBom(string recipeId) =>
        !string.IsNullOrWhiteSpace(recipeId)
        && V22TailoringPhysicalBomLossRecipeSet.Contains(recipeId);

    public static bool RequiresV22WeavingPhysicalBom(string recipeId) =>
        !string.IsNullOrWhiteSpace(recipeId)
        && V22WeavingPhysicalBomLossRecipeSet.Contains(recipeId);

    public static bool ApplyIfReviewed(ProductionRecipeSO recipe)
    {
        if (recipe == null)
            throw new ArgumentNullException(nameof(recipe));
        if (!ReviewedLossPolicies.TryGetValue(
                recipe.RecipeId,
                out ReviewedLossPolicy policy))
        {
            recipe.NormalizeEmptyOptionalAuthoringContracts();
            return false;
        }

        recipe.ConfigureMassExplanation(
            ProcessLossProductionMassExplanationCapability.Id,
            ProcessLossProductionMassExplanationCapability.Version,
            ProcessLossProductionMassExplanationCapability.BuildPayload(
                policy.LossKind,
                policy.ReasonCode));
        recipe.NormalizeEmptyOptionalAuthoringContracts();
        return true;
    }

    [MenuItem("DungeonStory/Build/V27/Apply All Reviewed Process-Loss Descriptors")]
    public static void ApplyAllReviewedLossAssetsMenu() =>
        Debug.Log(ApplyAllReviewedLossAssets());

    public static string ApplyAllReviewedLossAssets()
    {
        Dictionary<string, ProductionRecipeSO> recipes = AssetDatabase
            .FindAssets("t:ProductionRecipeSO", new[] { "Assets/Resources" })
            .Select(guid => AssetDatabase.GUIDToAssetPath(guid))
            .Select(path => AssetDatabase.LoadAssetAtPath<ProductionRecipeSO>(
                path))
            .Where(value => value != null
                && ReviewedLossPolicies.ContainsKey(value.RecipeId))
            .ToDictionary(value => value.RecipeId, StringComparer.Ordinal);
        if (recipes.Count != ReviewedLossPolicies.Count)
        {
            throw new InvalidOperationException(
                "Reviewed process-loss recipe asset count drifted: "
                + recipes.Count + "/" + ReviewedLossPolicies.Count + ".");
        }

        int changed = 0;
        foreach (KeyValuePair<string, ReviewedLossPolicy> pair in
                 ReviewedLossPolicies.OrderBy(
                     value => value.Key,
                     StringComparer.Ordinal))
        {
            ProductionRecipeSO recipe = recipes[pair.Key];
            string payload = ProcessLossProductionMassExplanationCapability
                .BuildPayload(pair.Value.LossKind, pair.Value.ReasonCode);
            ProductionMassExplanationAuthoringSnapshot before =
                recipe.MassExplanation;
            if (IsExact(before, payload))
                continue;
            if (!before.IsEmpty && !IsOwnedProcessLoss(before))
            {
                throw new InvalidOperationException(
                    "Reviewed recipe already has a conflicting descriptor: "
                    + pair.Key);
            }
            ApplyIfReviewed(recipe);
            EditorUtility.SetDirty(recipe);
            changed++;
        }
        if (changed > 0)
            AssetDatabase.SaveAssets();
        VerifyAllReviewedLossAssets(recipes);
        return "V27_REVIEWED_PROCESS_LOSS_APPLY_PASS changed=" + changed
            + " exact=" + ReviewedLossPolicies.Count;
    }

    public static string ClearNonReproducibleLossAssets()
    {
        Dictionary<string, ProductionRecipeSO> recipes = AssetDatabase
            .FindAssets("t:ProductionRecipeSO", new[] { "Assets/Resources" })
            .Select(guid => AssetDatabase.GUIDToAssetPath(guid))
            .Select(path => AssetDatabase.LoadAssetAtPath<ProductionRecipeSO>(
                path))
            .Where(value => value != null
                && NonReproducibleLossRecipeIds.Contains(
                    value.RecipeId,
                    StringComparer.Ordinal))
            .ToDictionary(value => value.RecipeId, StringComparer.Ordinal);
        if (recipes.Count != NonReproducibleLossRecipeIds.Length)
        {
            throw new InvalidOperationException(
                "Non-reproducible process-loss recipe asset count drifted: "
                + recipes.Count + "/" + NonReproducibleLossRecipeIds.Length
                + ".");
        }

        int changed = 0;
        foreach (string recipeId in NonReproducibleLossRecipeIds)
        {
            ProductionRecipeSO recipe = recipes[recipeId];
            ProductionMassExplanationAuthoringSnapshot before =
                recipe.MassExplanation;
            if (before.IsEmpty)
                continue;
            if (!string.Equals(
                    before.CapabilityId,
                    ProcessLossProductionMassExplanationCapability.Id,
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "Excluded recipe has a non-process-loss descriptor: "
                    + recipeId);
            }
            recipe.ConfigureMassExplanation(string.Empty, 0, string.Empty);
            EditorUtility.SetDirty(recipe);
            changed++;
        }
        if (changed > 0)
            AssetDatabase.SaveAssets();
        return "V27_NON_REPRODUCIBLE_PROCESS_LOSS_CLEAR_PASS changed="
            + changed + " exact=" + NonReproducibleLossRecipeIds.Length;
    }

    [MenuItem("DungeonStory/Build/V27/Apply Reviewed Cutting-Loss Descriptors")]
    public static void ApplyReviewedAssetsMenu() =>
        Debug.Log(ApplyReviewedAssets());

    public static string ApplyReviewedAssets()
    {
        Dictionary<string, ProductionRecipeSO> recipes = AssetDatabase
            .FindAssets("t:ProductionRecipeSO", new[] { "Assets/Resources" })
            .Select(guid => AssetDatabase.GUIDToAssetPath(guid))
            .Select(path => AssetDatabase.LoadAssetAtPath<ProductionRecipeSO>(
                path))
            .Where(value => value != null
                && CuttingLossRecipeSet.Contains(value.RecipeId))
            .ToDictionary(value => value.RecipeId, StringComparer.Ordinal);
        if (recipes.Count != CuttingLossRecipeIds.Length)
        {
            throw new InvalidOperationException(
                "Reviewed recipe asset count drifted: " + recipes.Count
                + "/" + CuttingLossRecipeIds.Length);
        }

        string payload = ProcessLossProductionMassExplanationCapability
            .BuildPayload(
                PhysicalMassLossKind.CuttingWaste,
                "cutting-dust-or-offcut");
        int changed = 0;
        foreach (string id in CuttingLossRecipeIds)
        {
            ProductionRecipeSO recipe = recipes[id];
            ProductionMassExplanationAuthoringSnapshot before =
                recipe.MassExplanation;
            bool exact = !before.IsEmpty
                && string.Equals(
                    before.CapabilityId,
                    ProcessLossProductionMassExplanationCapability.Id,
                    StringComparison.Ordinal)
                && before.ContractVersion ==
                    ProcessLossProductionMassExplanationCapability.Version
                && string.Equals(
                    before.CanonicalPayload,
                    payload,
                    StringComparison.Ordinal);
            if (exact)
                continue;
            if (!before.IsEmpty)
            {
                throw new InvalidOperationException(
                    "Reviewed recipe already has a conflicting descriptor: "
                    + id);
            }
            ApplyIfReviewed(recipe);
            EditorUtility.SetDirty(recipe);
            changed++;
        }
        if (changed > 0)
            AssetDatabase.SaveAssets();
        VerifyReviewedAssets();
        return "V27_REVIEWED_CUTTING_LOSS_APPLY_PASS changed=" + changed
            + " exact=" + CuttingLossRecipeIds.Length;
    }

    public static void VerifyReviewedAssets()
    {
        string payload = ProcessLossProductionMassExplanationCapability
            .BuildPayload(
                PhysicalMassLossKind.CuttingWaste,
                "cutting-dust-or-offcut");
        Dictionary<string, ProductionMassExplanationAuthoringSnapshot>
            captured = AssetDatabase
                .FindAssets(
                    "t:ProductionRecipeSO",
                    new[] { "Assets/Resources" })
                .Select(guid => AssetDatabase.GUIDToAssetPath(guid))
                .Select(path => AssetDatabase.LoadAssetAtPath<ProductionRecipeSO>(
                    path))
                .Where(value => value != null
                    && CuttingLossRecipeSet.Contains(value.RecipeId))
                .ToDictionary(
                    value => value.RecipeId,
                    value => value.MassExplanation,
                    StringComparer.Ordinal);
        foreach (string id in CuttingLossRecipeIds)
        {
            if (!captured.TryGetValue(
                    id,
                    out ProductionMassExplanationAuthoringSnapshot value)
                || value.IsEmpty
                || !string.Equals(
                    value.CapabilityId,
                    ProcessLossProductionMassExplanationCapability.Id,
                    StringComparison.Ordinal)
                || value.ContractVersion !=
                    ProcessLossProductionMassExplanationCapability.Version
                || !string.Equals(
                    value.CanonicalPayload,
                    payload,
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "Reviewed cutting-loss recipe is not authored exactly: "
                    + id);
            }
        }
    }

    [MenuItem("DungeonStory/Build/V27/Apply Reviewed Fiber-Processing Loss Descriptors")]
    public static void ApplyReviewedFiberAssetsMenu() =>
        Debug.Log(ApplyReviewedFiberAssets());

    public static string ApplyReviewedFiberAssets()
    {
        Dictionary<string, ProductionRecipeSO> recipes = AssetDatabase
            .FindAssets("t:ProductionRecipeSO", new[] { "Assets/Resources" })
            .Select(guid => AssetDatabase.GUIDToAssetPath(guid))
            .Select(path => AssetDatabase.LoadAssetAtPath<ProductionRecipeSO>(
                path))
            .Where(value => value != null
                && FiberProcessingLossRecipeSet.Contains(value.RecipeId))
            .ToDictionary(value => value.RecipeId, StringComparer.Ordinal);
        if (recipes.Count != FiberProcessingLossRecipeIds.Length)
        {
            throw new InvalidOperationException(
                "Reviewed fiber recipe asset count drifted: " + recipes.Count
                + "/" + FiberProcessingLossRecipeIds.Length);
        }

        string payload = ProcessLossProductionMassExplanationCapability
            .BuildPayload(
                PhysicalMassLossKind.FiberProcessingWaste,
                "fiber-carding-and-spinning-waste");
        int changed = 0;
        foreach (string id in FiberProcessingLossRecipeIds)
        {
            ProductionRecipeSO recipe = recipes[id];
            ProductionMassExplanationAuthoringSnapshot before =
                recipe.MassExplanation;
            bool exact = !before.IsEmpty
                && string.Equals(
                    before.CapabilityId,
                    ProcessLossProductionMassExplanationCapability.Id,
                    StringComparison.Ordinal)
                && before.ContractVersion ==
                    ProcessLossProductionMassExplanationCapability.Version
                && string.Equals(
                    before.CanonicalPayload,
                    payload,
                    StringComparison.Ordinal);
            if (exact)
                continue;
            if (!before.IsEmpty)
            {
                throw new InvalidOperationException(
                    "Reviewed fiber recipe has a conflicting descriptor: "
                    + id);
            }
            ApplyIfReviewed(recipe);
            EditorUtility.SetDirty(recipe);
            changed++;
        }
        if (changed > 0)
            AssetDatabase.SaveAssets();
        VerifyReviewedFiberAssets();
        return "V27_REVIEWED_FIBER_PROCESSING_LOSS_APPLY_PASS changed="
            + changed + " exact=" + FiberProcessingLossRecipeIds.Length;
    }

    public static void VerifyReviewedFiberAssets()
    {
        string payload = ProcessLossProductionMassExplanationCapability
            .BuildPayload(
                PhysicalMassLossKind.FiberProcessingWaste,
                "fiber-carding-and-spinning-waste");
        Dictionary<string, ProductionMassExplanationAuthoringSnapshot>
            captured = AssetDatabase
                .FindAssets(
                    "t:ProductionRecipeSO",
                    new[] { "Assets/Resources" })
                .Select(guid => AssetDatabase.GUIDToAssetPath(guid))
                .Select(path => AssetDatabase.LoadAssetAtPath<ProductionRecipeSO>(
                    path))
                .Where(value => value != null
                    && FiberProcessingLossRecipeSet.Contains(value.RecipeId))
                .ToDictionary(
                    value => value.RecipeId,
                    value => value.MassExplanation,
                    StringComparer.Ordinal);
        foreach (string id in FiberProcessingLossRecipeIds)
        {
            if (!captured.TryGetValue(
                    id,
                    out ProductionMassExplanationAuthoringSnapshot value)
                || value.IsEmpty
                || !string.Equals(
                    value.CapabilityId,
                    ProcessLossProductionMassExplanationCapability.Id,
                    StringComparison.Ordinal)
                || value.ContractVersion !=
                    ProcessLossProductionMassExplanationCapability.Version
                || !string.Equals(
                    value.CanonicalPayload,
                    payload,
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "Reviewed fiber-processing recipe is not authored exactly: "
                    + id);
            }
        }
    }

    private static IReadOnlyDictionary<string, ReviewedLossPolicy>
        BuildReviewedLossPolicies()
    {
        Dictionary<string, ReviewedLossPolicy> result =
            new(StringComparer.Ordinal);
        AddPolicyFamily(
            result,
            CuttingLossRecipeIds,
            PhysicalMassLossKind.CuttingWaste,
            "cutting-dust-or-offcut");
        AddPolicyFamily(
            result,
            FiberProcessingLossRecipeIds,
            PhysicalMassLossKind.FiberProcessingWaste,
            "fiber-carding-and-spinning-waste");
        AddPolicyFamily(
            result,
            V22TailoringPhysicalBomLossRecipeIds,
            PhysicalMassLossKind.CuttingWaste,
            "v22-apparel-cutting-thread-and-unusable-offcut");
        AddPolicyFamily(
            result,
            V22WeavingPhysicalBomLossRecipeIds,
            PhysicalMassLossKind.FiberProcessingWaste,
            "v22-weaving-twist-and-fiber-waste");
        AddPolicyFamily(
            result,
            ResearchMechanicalPhysicalBomLossRecipeIds,
            PhysicalMassLossKind.CuttingWaste,
            "research-machining-forming-and-assembly-offcut");
        AddPolicyFamily(
            result,
            ResearchArcanePhysicalBomLossRecipeIds,
            PhysicalMassLossKind.ExtractionResidue,
            "research-rune-tuning-and-catalyst-residue");
        AddPolicyFamily(
            result,
            CoreTextilePhysicalBomLossRecipeIds,
            PhysicalMassLossKind.FiberProcessingWaste,
            "core-textile-carding-and-bedding-trim-waste");
        AddPolicyFamily(
            result,
            CoreCookingPhysicalBomLossRecipeIds,
            PhysicalMassLossKind.MoistureEvaporation,
            "cooking-smoking-water-and-trimming-residue");
        AddPolicy(
            result,
            "recipe:herbal-poultice",
            PhysicalMassLossKind.ExtractionResidue,
            "herbal-poultice-infusion-and-fiber-preparation-residue");
        AddPolicy(
            result,
            "recipe:component:paper-paste",
            PhysicalMassLossKind.MoistureEvaporation,
            "paste-cooking-water-and-starch-setting-residue");
        AddPolicy(
            result,
            "recipe:material:hardened-leather",
            PhysicalMassLossKind.ExtractionResidue,
            "leather-hardening-resin-and-trimming-residue");
        AddPolicy(
            result,
            "recipe:material:laminated-lumber",
            PhysicalMassLossKind.CuttingWaste,
            "laminated-lumber-resin-and-planing-residue");
        AddPolicy(
            result,
            "recipe:material:niter",
            PhysicalMassLossKind.FermentationGasLoss,
            "niter-bed-biogenic-gas-and-sediment-residue");
        AddPolicy(
            result,
            "recipe:material:paper",
            PhysicalMassLossKind.FiberProcessingWaste,
            "paper-pulp-screening-and-trim-residue");
        AddPolicy(
            result,
            "recipe:supply:botanical-pesticide",
            PhysicalMassLossKind.ExtractionResidue,
            "botanical-pesticide-steeping-and-filter-residue");
        AddPolicy(
            result,
            "recipe:supply:fungicide",
            PhysicalMassLossKind.ExtractionResidue,
            "fungicide-filtration-and-charcoal-residue");
        AddPolicy(
            result,
            "recipe:supply:greenhouse-nutrient",
            PhysicalMassLossKind.ExtractionResidue,
            "greenhouse-nutrient-mixing-and-settling-residue");
        AddPolicy(
            result,
            "recipe:supply:mushroom-substrate",
            PhysicalMassLossKind.FermentationGasLoss,
            "mushroom-substrate-composting-and-screening-residue");
        AddPolicy(
            result,
            "recipe:supply:nitrate-fertilizer",
            PhysicalMassLossKind.FermentationGasLoss,
            "nitrate-fertilizer-composting-and-screening-residue");
        AddPolicy(
            result,
            "recipe:supply:pest-lure",
            PhysicalMassLossKind.ExtractionResidue,
            "pest-lure-resin-rendering-and-packaging-residue");
        AddPolicyFamily(
            result,
            SmeltingLossRecipeIds,
            PhysicalMassLossKind.SmeltingByproduct,
            "smelting-slag-and-furnace-offgas");
        AddPolicyFamily(
            result,
            CombustionLossRecipeIds,
            PhysicalMassLossKind.Combustion,
            "carbonization-smoke-and-volatile-loss");
        AddPolicy(
            result,
            MoistureLossRecipeIds[0],
            PhysicalMassLossKind.MoistureEvaporation,
            "cooking-moisture-and-trimming-loss");
        AddPolicy(
            result,
            MoistureLossRecipeIds[1],
            PhysicalMassLossKind.MoistureEvaporation,
            "smoking-and-drying-moisture-loss");
        AddPolicy(
            result,
            MoistureLossRecipeIds[2],
            PhysicalMassLossKind.MoistureEvaporation,
            "curing-moisture-loss");
        AddPolicyFamily(
            result,
            MillingLossRecipeIds,
            PhysicalMassLossKind.MillingByproduct,
            "grain-milling-bran-and-starch-residue");
        AddPolicy(
            result,
            ExtractionLossRecipeIds[0],
            PhysicalMassLossKind.ExtractionResidue,
            "tanning-hair-fat-and-brine-residue");
        AddPolicy(
            result,
            ExtractionLossRecipeIds[1],
            PhysicalMassLossKind.ExtractionResidue,
            "toxin-extraction-spent-feedstock-residue");
        AddPolicy(
            result,
            ExtractionLossRecipeIds[2],
            PhysicalMassLossKind.ExtractionResidue,
            "distillation-spent-feedstock-residue");
        AddPolicy(
            result,
            ExtractionLossRecipeIds[3],
            PhysicalMassLossKind.ExtractionResidue,
            "rendering-water-and-tissue-residue");
        AddPolicyFamily(
            result,
            WeavingLossRecipeIds,
            PhysicalMassLossKind.FiberProcessingWaste,
            "fiber-carding-and-weaving-waste");
        AddPolicyFamily(result, ProjectileCraftLossRecipeIds,
            PhysicalMassLossKind.CuttingWaste,
            "ammunition-shaft-fletching-and-fitting-offcuts");
        AddPolicyFamily(result, AmmunitionInfusionLossRecipeIds,
            PhysicalMassLossKind.ExtractionResidue,
            "ammunition-coating-and-infusion-residue");
        AddPolicyFamily(result, AmmunitionPressLossRecipeIds,
            PhysicalMassLossKind.CuttingWaste,
            "ammunition-press-filling-and-casing-trim-residue");
        AddPolicyFamily(result, PharmacologyLossRecipeIds,
            PhysicalMassLossKind.ExtractionResidue,
            "pharmacology-filtration-distillation-and-filling-residue");
        AddPolicyFamily(result, ClinicalPreparationLossRecipeIds,
            PhysicalMassLossKind.ExtractionResidue,
            "clinical-sterile-preparation-and-filtration-residue");
        AddPolicyFamily(result, ClinicalHardwareLossRecipeIds,
            PhysicalMassLossKind.CuttingWaste,
            "clinical-kit-casing-and-instrument-trim-residue");
        AddPolicyFamily(result, VaccineLossRecipeIds,
            PhysicalMassLossKind.ExtractionResidue,
            "vaccine-culture-filtration-and-sterile-fill-residue");
        AddPolicy(
            result,
            "recipe:anesthetic",
            PhysicalMassLossKind.ExtractionResidue,
            "anesthetic-distillation-vapour-and-herbal-filter-residue");
        AddPolicy(
            result,
            "recipe:curd",
            PhysicalMassLossKind.ExtractionResidue,
            "curdling-whey-separation-residue");
        AddPolicy(
            result,
            "recipe:surgery:artificial-eye",
            PhysicalMassLossKind.CuttingWaste,
            "artificial-eye-lens-grinding-and-arcane-calibration-residue");
        AddPolicy(
            result,
            "recipe:ammo:tranquilizer-dart",
            PhysicalMassLossKind.CuttingWaste,
            "tranquilizer-dart-needle-forming-and-dose-fill-residue");
        AddPolicy(
            result,
            "recipe:material:granulated-powder",
            PhysicalMassLossKind.MillingByproduct,
            "granulated-powder-screening-and-paper-trim-residue");
        AddPolicy(
            result,
            "recipe:supply:alliance-signal-kit",
            PhysicalMassLossKind.CuttingWaste,
            "alliance-signal-casing-and-charge-trim-residue");
        AddPolicy(
            result,
            "recipe:supply:funeral-preparation-kit",
            PhysicalMassLossKind.CuttingWaste,
            "funeral-kit-textile-and-paper-trim-residue");
        AddPolicy(
            result,
            "recipe:supply:performance-prop-box",
            PhysicalMassLossKind.CuttingWaste,
            "performance-prop-frame-cutting-and-fabric-trim-residue");
        AddPolicy(
            result,
            "recipe:v22:sewing-kit",
            PhysicalMassLossKind.CuttingWaste,
            "sewing-kit-toolhead-machining-and-assembly-residue");
        AddPolicyFamily(result, FoodPreparationLossRecipeIds,
            PhysicalMassLossKind.MoistureEvaporation,
            "food-preparation-moisture-and-trimming-residue");
        AddPolicyFamily(result, FermentationLossRecipeIds,
            PhysicalMassLossKind.FermentationGasLoss,
            "fermentation-gas-and-brine-release-residue");
        AddPolicy(
            result,
            "recipe:night-spirit",
            PhysicalMassLossKind.ExtractionResidue,
            "spirit-distillation-vapour-and-filter-residue");
        AddPolicy(
            result,
            "recipe:night-wine",
            PhysicalMassLossKind.MoistureEvaporation,
            "night-wine-cask-aging-evaporation-residue");
        AddPolicyFamily(result, SyrupReductionLossRecipeIds,
            PhysicalMassLossKind.MoistureEvaporation,
            "syrup-reduction-evaporation-residue");
        AddPolicy(
            result,
            "recipe:milling-flour",
            PhysicalMassLossKind.MillingByproduct,
            "flour-milling-bran-and-husk-residue");
        AddPolicy(
            result,
            "recipe:cheese",
            PhysicalMassLossKind.MoistureEvaporation,
            "cheese-aging-moisture-loss");
        AddPolicy(
            result,
            "recipe:hay-feed",
            PhysicalMassLossKind.FiberProcessingWaste,
            "hay-feed-screening-and-blending-residue");
        AddPolicy(
            result,
            "recipe:seasoned-filling",
            PhysicalMassLossKind.CuttingWaste,
            "seasoned-filling-trimming-and-preparation-residue");
        AddPolicy(
            result,
            "recipe:material:lead-shot",
            PhysicalMassLossKind.CuttingWaste,
            "lead-shot-casting-sprue-and-screening-residue");
        AddPolicy(
            result,
            "recipe:material:cartridge-paper",
            PhysicalMassLossKind.CuttingWaste,
            "cartridge-paper-trimming-and-starch-screening-residue");
        AddPolicy(
            result,
            "recipe:supply:defense-mixed-ammo-box",
            PhysicalMassLossKind.CuttingWaste,
            "defense-supply-press-sorting-and-boxing-residue");
        AddPolicyFamily(result, TextileComponentLossRecipeIds,
            PhysicalMassLossKind.FiberProcessingWaste,
            "textile-cutting-and-assembly-offcuts");
        AddPolicyFamily(result, PaperComponentLossRecipeIds,
            PhysicalMassLossKind.CuttingWaste,
            "paper-layout-binding-and-package-offcuts");
        AddPolicyFamily(result, MechanicalComponentLossRecipeIds,
            PhysicalMassLossKind.CuttingWaste,
            "component-machining-and-assembly-offcuts");
        AddPolicyFamily(result, RuneComponentLossRecipeIds,
            PhysicalMassLossKind.ExtractionResidue,
            "rune-tuning-and-catalyst-residue");
        AddPolicyFamily(result, CompostLossRecipeIds,
            PhysicalMassLossKind.FermentationGasLoss,
            "composting-moisture-and-biogenic-gas-loss");
        AddPolicyFamily(result, LowFuelLossRecipeIds,
            PhysicalMassLossKind.MoistureEvaporation,
            "fuel-drying-moisture-and-contaminant-loss");
        AddPolicy(result, "recipe:soap",
            PhysicalMassLossKind.MoistureEvaporation,
            "soap-curing-moisture-loss");
        AddPolicy(result, "recipe:candle",
            PhysicalMassLossKind.CuttingWaste,
            "candle-mould-and-wick-trimming-loss");
        AddPolicy(result, "recipe:rune-leather",
            PhysicalMassLossKind.ExtractionResidue,
            "rune-tanning-resin-and-trimming-residue");
        AddPolicy(result, "recipe:bowstring-sinew",
            PhysicalMassLossKind.ExtractionResidue,
            "sinew-separation-and-twisting-residue");
        AddPolicy(result, "recipe:dreamweave",
            PhysicalMassLossKind.ExtractionResidue,
            "dreamweave-infusion-spent-feedstock-residue");
        AddPolicyFamily(result, DecorationLossRecipeIds,
            PhysicalMassLossKind.CuttingWaste,
            "carving-and-decoration-offcuts");
        AddPolicyFamily(result, GoldCraftLossRecipeIds,
            PhysicalMassLossKind.CuttingWaste,
            "gold-working-polishing-and-offcut-loss");
        AddPolicyFamily(result, StoneCraftLossRecipeIds,
            PhysicalMassLossKind.CuttingWaste,
            "stone-dressing-rubble-and-dust");
        AddPolicyFamily(result, TextileFinishedLossRecipeIds,
            PhysicalMassLossKind.CuttingWaste,
            "textile-cutting-lint-and-unusable-offcut");
        AddPolicyFamily(result, RecordBindingLossRecipeIds,
            PhysicalMassLossKind.CuttingWaste,
            "paper-leather-and-binding-offcuts");
        AddPolicyFamily(result, MedicalComponentLossRecipeIds,
            PhysicalMassLossKind.ExtractionResidue,
            "medical-sterile-preparation-and-assembly-residue");
        AddPolicyFamily(result, ToolCraftLossRecipeIds,
            PhysicalMassLossKind.CuttingWaste,
            "tool-machining-and-assembly-offcuts");
        AddPolicyFamily(result, ProjectileShaftCraftLossRecipeIds,
            PhysicalMassLossKind.CuttingWaste,
            "ammunition-shaft-fletching-and-fitting-offcuts");
        AddPolicyFamily(result, ProstheticCraftLossRecipeIds,
            PhysicalMassLossKind.CuttingWaste,
            "prosthetic-machining-and-fitting-offcuts");
        AddPolicyFamily(result, TreatedLumberLossRecipeIds,
            PhysicalMassLossKind.CuttingWaste,
            "lumber-planing-and-resin-treatment-residue");
        return result;
    }

    private static void AddPolicyFamily(
        IDictionary<string, ReviewedLossPolicy> output,
        IReadOnlyList<string> recipeIds,
        PhysicalMassLossKind lossKind,
        string reasonCode)
    {
        if (recipeIds == null
            || recipeIds.Count == 0
            || recipeIds.Distinct(StringComparer.Ordinal).Count()
                != recipeIds.Count
            || !recipeIds.SequenceEqual(
                recipeIds.OrderBy(value => value, StringComparer.Ordinal)))
        {
            throw new InvalidOperationException(
                "Reviewed loss-policy family must be non-empty, unique and ordinal sorted: "
                + reasonCode);
        }
        foreach (string recipeId in recipeIds)
        {
            AddPolicy(output, recipeId, lossKind, reasonCode);
        }
    }

    private static void AddPolicy(
        IDictionary<string, ReviewedLossPolicy> output,
        string recipeId,
        PhysicalMassLossKind lossKind,
        string reasonCode)
    {
        if (!output.TryAdd(
                recipeId,
                new ReviewedLossPolicy(lossKind, reasonCode)))
        {
            throw new InvalidOperationException(
                "Reviewed loss-policy recipe is duplicated: " + recipeId);
        }
    }

    private static void VerifyAllReviewedLossAssets(
        IReadOnlyDictionary<string, ProductionRecipeSO> recipes)
    {
        foreach (KeyValuePair<string, ReviewedLossPolicy> pair in
                 ReviewedLossPolicies)
        {
            if (!recipes.TryGetValue(pair.Key, out ProductionRecipeSO recipe))
            {
                throw new InvalidOperationException(
                    "Reviewed process-loss recipe asset is missing: "
                    + pair.Key);
            }
            string payload = ProcessLossProductionMassExplanationCapability
                .BuildPayload(pair.Value.LossKind, pair.Value.ReasonCode);
            if (!IsExact(recipe.MassExplanation, payload))
            {
                throw new InvalidOperationException(
                    "Reviewed process-loss recipe is not authored exactly: "
                    + pair.Key);
            }
        }
    }

    private static bool IsExact(
        ProductionMassExplanationAuthoringSnapshot value,
        string payload) =>
        !value.IsEmpty
        && string.Equals(
            value.CapabilityId,
            ProcessLossProductionMassExplanationCapability.Id,
            StringComparison.Ordinal)
        && value.ContractVersion ==
            ProcessLossProductionMassExplanationCapability.Version
        && string.Equals(
            value.CanonicalPayload,
            payload,
            StringComparison.Ordinal);

    private static bool IsOwnedProcessLoss(
        ProductionMassExplanationAuthoringSnapshot value) =>
        !value.IsEmpty
        && string.Equals(
            value.CapabilityId,
            ProcessLossProductionMassExplanationCapability.Id,
            StringComparison.Ordinal)
        && value.ContractVersion
            == ProcessLossProductionMassExplanationCapability.Version;

    private readonly struct ReviewedLossPolicy
    {
        public ReviewedLossPolicy(
            PhysicalMassLossKind lossKind,
            string reasonCode)
        {
            if (lossKind == PhysicalMassLossKind.None)
                throw new ArgumentOutOfRangeException(nameof(lossKind));
            if (string.IsNullOrWhiteSpace(reasonCode)
                || !string.Equals(
                    reasonCode,
                    reasonCode.Trim(),
                    StringComparison.Ordinal))
            {
                throw new ArgumentException(
                    "A canonical loss reason is required.",
                    nameof(reasonCode));
            }
            LossKind = lossKind;
            ReasonCode = reasonCode;
        }

        public PhysicalMassLossKind LossKind { get; }
        public string ReasonCode { get; }
    }
}
#endif
