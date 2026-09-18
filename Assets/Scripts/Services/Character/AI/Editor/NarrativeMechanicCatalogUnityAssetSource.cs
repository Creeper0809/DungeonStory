#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using DungeonStory.Foundation;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Captures catalog data directly from the gameplay authorities.  This type must
/// not invent a runtime section when authored assets are absent: unavailable
/// content remains observable as a blocker and keeps training eligibility false.
/// </summary>
public sealed class NarrativeMechanicCatalogUnityAssetSource
    : INarrativeMechanicCatalogSnapshotProvider
{
    private const string CatalogId = "dungeonstory-v25-mechanic-catalog";

    public NarrativeMechanicCatalogSnapshot CaptureSnapshot()
    {
        List<string> relevantPaths = new List<string>();
        NarrativeMechanicCatalogCanonicalJsonObject characterSkill = CaptureCharacterSkill(
            relevantPaths,
            out CharacterSkillSystemSettingsSO characterSkillSettings);
        NarrativeMechanicCatalogCanonicalJsonObject facilityEvolution = CaptureFacilityEvolution(relevantPaths);
        NarrativeMechanicCatalogCanonicalJsonObject equipmentEvolution =
            CaptureEquipmentEvolution();

        List<NarrativeMechanicCatalogParityBlocker> blockers = new List<NarrativeMechanicCatalogParityBlocker>();
        List<NarrativeMechanicCatalogCanonicalJsonObject> parityCases =
            new List<NarrativeMechanicCatalogCanonicalJsonObject>();
        parityCases.AddRange(BuildCharacterSkillParityCases(characterSkillSettings));
        parityCases.AddRange(BuildPresentationContractParityCases(
            LocalLlmRequestProfiles.FacilityEvolution.Id,
            "facility",
            "시설 계보"));
        parityCases.AddRange(BuildPresentationContractParityCases(
            LocalLlmRequestProfiles.EvolutionHistory.Id,
            "equipment",
            "장비 진화"));
        parityCases.AddRange(BuildPresentationContractParityCases(
            LocalLlmRequestProfiles.AcquiredTrait.Id,
            "trait",
            "후천 특성"));
        parityCases.AddRange(
            NarrativeMechanicCatalogExtendedParityCases.BuildFacilityEvolutionParityCases());
        parityCases.AddRange(
            NarrativeMechanicCatalogExtendedParityCases.BuildEquipmentChoiceParityCases());
        parityCases.AddRange(
            NarrativeMechanicCatalogExtendedParityCases.BuildEvolutionHistoryParityCases());
        parityCases.AddRange(
            NarrativeMechanicCatalogExtendedParityCases.BuildPersonaParityCases());
        NarrativeMechanicCatalogCanonicalJsonObject acquiredTrait;
        if (TryCaptureAcquiredTrait(
                relevantPaths,
                out acquiredTrait,
                out CharacterAcquiredTraitSettingsSO acquiredSettings,
                out CharacterAcquiredTraitModuleSO[] acquiredModules,
                out string unavailableReason))
        {
            parityCases.AddRange(BuildAcquiredTraitParityCases(
                acquiredSettings,
                acquiredModules));
        }
        else
        {
            acquiredTrait = BuildUnavailableAcquiredTrait(unavailableReason);
            blockers.Add(new NarrativeMechanicCatalogParityBlocker(
                "acquired-trait-assets-unavailable",
                unavailableReason,
                "Run V25AcquiredTraitContentAssetBuilder.Build and ValidateAuthoring, then recapture the catalog from the indexed assets."));
        }

        NarrativeMechanicCatalogProvenance.AddRelevantPaths(relevantPaths);

        NarrativeMechanicCatalogPacketParity packetParity =
            new NarrativeMechanicCatalogPacketParity(parityCases, blockers);
        NarrativeMechanicCatalogTrainingPolicy policy =
            new NarrativeMechanicCatalogTrainingPolicy(
                new[]
                {
                    "canonical-json-python-parity",
                    "catalog-byte-determinism",
                    "packet-parity",
                    "v25-focused-unity-regression"
                },
                blockers);
        NarrativeMechanicScenarioBundle scenarios = NarrativeMechanicScenarioCatalog.Capture(
            new NarrativeMechanicScenarioCharacterSkillSource(),
            new NarrativeMechanicScenarioFacilityEvolutionSource(),
            new NarrativeMechanicScenarioEquipmentChoiceSource(),
            new NarrativeMechanicScenarioEvolutionHistorySource(),
            new NarrativeMechanicScenarioAcquiredTraitSource(),
            new NarrativeMechanicScenarioPersonaSource());

        return new NarrativeMechanicCatalogSnapshot(
            CatalogId,
            characterSkill,
            facilityEvolution,
            equipmentEvolution,
            acquiredTrait,
            relevantPaths,
            policy,
            packetParity,
            scenarios);
    }

    private static NarrativeMechanicCatalogCanonicalJsonObject CaptureCharacterSkill(
        ICollection<string> relevantPaths,
        out CharacterSkillSystemSettingsSO settings)
    {
        string[] assetPaths = FindAssetPaths<CharacterSkillSystemSettingsSO>();
        if (assetPaths.Length != 1)
        {
            throw new NarrativeMechanicCatalogUnsupportedSourceException(
                "character-skill-settings-cardinality",
                $"Expected exactly one CharacterSkillSystemSettingsSO asset, found {assetPaths.Length}.");
        }

        settings = AssetDatabase.LoadAssetAtPath<CharacterSkillSystemSettingsSO>(assetPaths[0]);
        if (settings == null)
        {
            throw new NarrativeMechanicCatalogUnsupportedSourceException(
                "character-skill-settings-load-failed",
                $"Could not load character skill settings at '{assetPaths[0]}'.");
        }

        AddAssetAndMeta(relevantPaths, assetPaths[0]);
        Dictionary<string, NarrativeMechanicCatalogCanonicalJsonValue> budgets =
            new Dictionary<string, NarrativeMechanicCatalogCanonicalJsonValue>(StringComparer.Ordinal);
        foreach (CharacterRarityBudget budget in RequireItems(
                     settings.rarityBudgets,
                     "character-skill-rarity-budgets",
                     assetPaths[0]))
        {
            string rarity = ToCatalogRarityToken(budget.rarity);
            if (budget.budget <= 0 || !budgets.TryAdd(
                    rarity,
                    NarrativeMechanicCatalogCanonicalJson.Integer(budget.budget)))
            {
                throw new NarrativeMechanicCatalogUnsupportedSourceException(
                    "character-skill-rarity-budget-invalid",
                    $"Character skill rarity budget '{rarity}' is missing, duplicate, or not positive.");
            }
        }

        List<NarrativeMechanicCatalogCanonicalJsonValue> modules =
            new List<NarrativeMechanicCatalogCanonicalJsonValue>();
        HashSet<string> moduleIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (CharacterSkillModuleRule module in RequireItems(
                     settings.Modules,
                     "character-skill-modules",
                     assetPaths[0]))
        {
            string moduleId = RequireAuthoredText(module.id, "module ID", assetPaths[0]);
            string capabilityId = CharacterSkillModuleCapabilityRegistry
                .Require(module)
                .CapabilityId;
            if (!moduleIds.Add(moduleId))
            {
                throw new NarrativeMechanicCatalogUnsupportedSourceException(
                    "character-skill-module-duplicate",
                    $"Character skill settings contain duplicate module ID '{moduleId}'.");
            }

            List<NarrativeMechanicCatalogCanonicalJsonValue> variants =
                new List<NarrativeMechanicCatalogCanonicalJsonValue>();
            HashSet<string> variantIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (CharacterSkillNumericVariant variant in RequireItems(
                         module.variants,
                         "character-skill-module-variants",
                         moduleId))
            {
                string variantId = RequireAuthoredText(variant.id, "variant ID", moduleId);
                if (variant.cost <= 0 || !variantIds.Add(variantId))
                {
                    throw new NarrativeMechanicCatalogUnsupportedSourceException(
                        "character-skill-variant-invalid",
                        $"Character skill module '{moduleId}' has duplicate or non-positive-cost variant '{variantId}'.");
                }

                variants.Add(NarrativeMechanicCatalogCanonicalJson.Object(
                    NarrativeMechanicCatalogCanonicalJson.Property(
                        "cost", NarrativeMechanicCatalogCanonicalJson.Integer(variant.cost)),
                    NarrativeMechanicCatalogCanonicalJson.Property(
                        "displayName", NarrativeMechanicCatalogCanonicalJson.String(
                            RequireAuthoredText(variant.displayName, "variant display name", variantId))),
                    NarrativeMechanicCatalogCanonicalJson.Property(
                        "variantId", NarrativeMechanicCatalogCanonicalJson.String(variantId))));
            }

            modules.Add(NarrativeMechanicCatalogCanonicalJson.Object(
                NarrativeMechanicCatalogCanonicalJson.Property(
                    "allowedKinds", EnumArray(module.allowedKinds, "allowed kinds", moduleId)),
                NarrativeMechanicCatalogCanonicalJson.Property(
                    "allowedTargets", EnumArray(module.allowedTargets, "allowed targets", moduleId)),
                NarrativeMechanicCatalogCanonicalJson.Property(
                    "allowedTriggers", EnumArray(module.allowedTriggers, "allowed triggers", moduleId)),
                NarrativeMechanicCatalogCanonicalJson.Property(
                    "capabilityId", NarrativeMechanicCatalogCanonicalJson.String(capabilityId)),
                NarrativeMechanicCatalogCanonicalJson.Property(
                    "displayName", NarrativeMechanicCatalogCanonicalJson.String(
                        RequireAuthoredText(module.displayName, "module display name", moduleId))),
                NarrativeMechanicCatalogCanonicalJson.Property(
                    "formula", CharacterSkillFormulaJson(module, assetPaths[0])),
                NarrativeMechanicCatalogCanonicalJson.Property(
                    "moduleClass", NarrativeMechanicCatalogCanonicalJson.String(
                        module.GetType().Name)),
                NarrativeMechanicCatalogCanonicalJson.Property(
                    "moduleId", NarrativeMechanicCatalogCanonicalJson.String(moduleId)),
                NarrativeMechanicCatalogCanonicalJson.Property(
                    "variants", new NarrativeMechanicCatalogCanonicalJsonArray(variants))));
        }

        List<NarrativeMechanicCatalogCanonicalJsonValue> drawbackModules = settings
            .RequireDrawbackCatalog()
            .OrderBy(value => value.DrawbackId, StringComparer.Ordinal)
            .Select(value => (NarrativeMechanicCatalogCanonicalJsonValue)
                NarrativeMechanicCatalogCanonicalJson.Object(
                    NarrativeMechanicCatalogCanonicalJson.Property(
                        "allowedKinds", EnumArray(value.allowedKinds,
                            "allowed kinds", value.DrawbackId)),
                    NarrativeMechanicCatalogCanonicalJson.Property(
                        "allowedTriggers", EnumArray(value.allowedTriggers,
                            "allowed triggers", value.DrawbackId)),
                    NarrativeMechanicCatalogCanonicalJson.Property(
                        "capabilityId", NarrativeMechanicCatalogCanonicalJson.String(
                            value.CapabilityId)),
                    NarrativeMechanicCatalogCanonicalJson.Property(
                        "applicationKind", NarrativeMechanicCatalogCanonicalJson.String(
                            value.applicationKind.ToString())),
                    NarrativeMechanicCatalogCanonicalJson.Property(
                        "description", NarrativeMechanicCatalogCanonicalJson.String(
                            value.Description)),
                    NarrativeMechanicCatalogCanonicalJson.Property(
                        "displayName", NarrativeMechanicCatalogCanonicalJson.String(
                            value.DisplayName)),
                    NarrativeMechanicCatalogCanonicalJson.Property(
                        "domainAffinities", EnumArray(value.domainAffinities,
                            "domain affinities", value.DrawbackId)),
                    NarrativeMechanicCatalogCanonicalJson.Property(
                        "drawbackId", NarrativeMechanicCatalogCanonicalJson.String(
                            value.DrawbackId)),
                    NarrativeMechanicCatalogCanonicalJson.Property(
                        "formula", FormulaDescriptorJson(
                            value.RequireFormulaDescriptor(), value.DrawbackId)),
                    NarrativeMechanicCatalogCanonicalJson.Property(
                        "runtimeEffectId", NarrativeMechanicCatalogCanonicalJson.String(
                            value.effectDefinition?.EffectId ?? string.Empty)),
                    NarrativeMechanicCatalogCanonicalJson.Property(
                        "runtimeTargetId", NarrativeMechanicCatalogCanonicalJson.String(
                            value.effectDefinition?.TargetId ?? string.Empty)),
                    NarrativeMechanicCatalogCanonicalJson.Property(
                        "statDeltaPerCredit", NarrativeMechanicCatalogCanonicalJson.String(
                            value.statDeltaPerCredit.ToString(
                                "R", CultureInfo.InvariantCulture))),
                    NarrativeMechanicCatalogCanonicalJson.Property(
                        "maximumCredit", NarrativeMechanicCatalogCanonicalJson.Integer(
                            value.MaximumCredit))))
            .ToList();

        IReadOnlyList<CharacterSkillSemanticsCatalogEntryDto> semanticsEntries =
            CharacterSkillCombinationSemanticsFactory.CreateCatalogCoverage(settings);
        int coveredModules = semanticsEntries.Select(value => value.moduleId)
            .Distinct(StringComparer.Ordinal)
            .Count();
        int coveredVariants = semanticsEntries
            .Select(value => value.moduleId + "|" + value.variantId)
            .Distinct(StringComparer.Ordinal)
            .Count();
        NarrativeMechanicCatalogCanonicalJsonObject semanticsCoverage =
            NarrativeMechanicCatalogCanonicalJson.Object(
                NarrativeMechanicCatalogCanonicalJson.Property(
                    "coveragePolicy", NarrativeMechanicCatalogCanonicalJson.String(
                        CharacterSkillCombinationSemanticsFactory.CatalogCoveragePolicy)),
                NarrativeMechanicCatalogCanonicalJson.Property(
                    "entries", new NarrativeMechanicCatalogCanonicalJsonArray(
                        semanticsEntries.Select(CharacterSkillSemanticsCatalogEntryJson))),
                NarrativeMechanicCatalogCanonicalJson.Property(
                    "entryCount", NarrativeMechanicCatalogCanonicalJson.Integer(
                        semanticsEntries.Count)),
                NarrativeMechanicCatalogCanonicalJson.Property(
                    "moduleCount", NarrativeMechanicCatalogCanonicalJson.Integer(coveredModules)),
                NarrativeMechanicCatalogCanonicalJson.Property(
                    "schemaVersion", NarrativeMechanicCatalogCanonicalJson.Integer(
                        CharacterSkillCombinationSemanticsFactory.SchemaVersion)),
                NarrativeMechanicCatalogCanonicalJson.Property(
                    "variantCount", NarrativeMechanicCatalogCanonicalJson.Integer(coveredVariants)));

        return NarrativeMechanicCatalogCanonicalJson.Object(
            NarrativeMechanicCatalogCanonicalJson.Property(
                "capabilityBindingVersion", NarrativeMechanicCatalogCanonicalJson.Integer(1)),
            NarrativeMechanicCatalogCanonicalJson.Property(
                "drawbackModules", new NarrativeMechanicCatalogCanonicalJsonArray(
                    drawbackModules)),
            NarrativeMechanicCatalogCanonicalJson.Property(
                "modules", new NarrativeMechanicCatalogCanonicalJsonArray(modules)),
            NarrativeMechanicCatalogCanonicalJson.Property(
                "formulaPolicy", CharacterSkillFormulaPolicyJson(settings, assetPaths[0])),
            NarrativeMechanicCatalogCanonicalJson.Property(
                "rarityProtocol", BuildRarityProtocol()),
            NarrativeMechanicCatalogCanonicalJson.Property(
                "rarityBudgets", new NarrativeMechanicCatalogCanonicalJsonObject(budgets)),
            NarrativeMechanicCatalogCanonicalJson.Property(
                "semanticsCoverage", semanticsCoverage));
    }

    private static NarrativeMechanicCatalogCanonicalJsonObject CharacterSkillFormulaPolicyJson(
        CharacterSkillSystemSettingsSO settings,
        string sourcePath)
    {
        CharacterSkillFormulaPolicyDefinition definition = settings.formulaPolicy
            ?? throw new NarrativeMechanicCatalogUnsupportedSourceException(
                "character-skill-formula-policy-missing",
                $"Character skill settings at '{sourcePath}' have no formula policy.");
        NarrativeFormulaStrengthPolicy runtime;
        NarrativeFormulaDrawbackCreditPolicy drawback;
        string catalogSha256;
        try
        {
            runtime = settings.RequireFormulaPolicy();
            drawback = definition.RequireDrawbackPolicy();
            catalogSha256 = definition.RequireCatalogSha256();
        }
        catch (Exception exception)
        {
            throw new NarrativeMechanicCatalogUnsupportedSourceException(
                "character-skill-formula-policy-invalid",
                $"Character skill formula policy at '{sourcePath}' is invalid: {exception.Message}");
        }
        return NarrativeMechanicCatalogCanonicalJson.Object(
            NarrativeMechanicCatalogCanonicalJson.Property(
                "baseBudget", NarrativeMechanicCatalogCanonicalJson.Integer(runtime.BaseBudget)),
            NarrativeMechanicCatalogCanonicalJson.Property(
                "catalogSha256", NarrativeMechanicCatalogCanonicalJson.String(catalogSha256)),
            NarrativeMechanicCatalogCanonicalJson.Property(
                "drawbackCredit", NarrativeMechanicCatalogCanonicalJson.Object(
                    NarrativeMechanicCatalogCanonicalJson.Property(
                        "absoluteMaximumCredit", NarrativeMechanicCatalogCanonicalJson.Integer(
                            drawback.AbsoluteMaximumCredit)),
                    NarrativeMechanicCatalogCanonicalJson.Property(
                        "automaticMaximumBudgetFractionDecimal",
                        NarrativeMechanicCatalogCanonicalJson.String(
                            drawback.AutomaticMaximumBudgetFraction.ToString(
                                "R", CultureInfo.InvariantCulture))),
                    NarrativeMechanicCatalogCanonicalJson.Property(
                        "playerChoiceMaximumBudgetFractionDecimal",
                        NarrativeMechanicCatalogCanonicalJson.String(
                            drawback.PlayerChoiceMaximumBudgetFraction.ToString(
                                "R", CultureInfo.InvariantCulture))),
                    NarrativeMechanicCatalogCanonicalJson.Property(
                        "requireNegativeEvidenceForAutomatic",
                        NarrativeMechanicCatalogCanonicalJson.Boolean(
                            drawback.RequireNegativeEvidenceForAutomatic)))),
            NarrativeMechanicCatalogCanonicalJson.Property(
                "formulaVersion", NarrativeMechanicCatalogCanonicalJson.Integer(runtime.FormulaVersion)),
            NarrativeMechanicCatalogCanonicalJson.Property(
                "maximumImportanceDecimal", NarrativeMechanicCatalogCanonicalJson.String(
                    runtime.MaximumImportance.ToString("R", CultureInfo.InvariantCulture))),
            NarrativeMechanicCatalogCanonicalJson.Property(
                "milestoneWeightsDecimal", new NarrativeMechanicCatalogCanonicalJsonArray(
                    runtime.MilestoneWeights.Select(value => NarrativeMechanicCatalogCanonicalJson.String(
                        value.ToString("R", CultureInfo.InvariantCulture))))),
            NarrativeMechanicCatalogCanonicalJson.Property(
                "minimumImportanceDecimal", NarrativeMechanicCatalogCanonicalJson.String(
                    runtime.MinimumImportance.ToString("R", CultureInfo.InvariantCulture))),
            NarrativeMechanicCatalogCanonicalJson.Property(
                "powerScale", NarrativeMechanicCatalogCanonicalJson.Integer(runtime.PowerScale)),
            NarrativeMechanicCatalogCanonicalJson.Property(
                "softCapKDecimal", NarrativeMechanicCatalogCanonicalJson.String(
                    runtime.SoftCapK.ToString("R", CultureInfo.InvariantCulture))),
            NarrativeMechanicCatalogCanonicalJson.Property(
                "targetCosts", new NarrativeMechanicCatalogCanonicalJsonArray(
                    (definition.targetCosts ?? new List<CharacterSkillTargetFormulaCostDefinition>())
                    .OrderBy(value => value.target)
                    .Select(value => NarrativeMechanicCatalogCanonicalJson.Object(
                        NarrativeMechanicCatalogCanonicalJson.Property(
                            "target", NarrativeMechanicCatalogCanonicalJson.String(value.target.ToString())),
                        NarrativeMechanicCatalogCanonicalJson.Property(
                            "targetCount", NarrativeMechanicCatalogCanonicalJson.Integer(value.targetCount)))))),
            NarrativeMechanicCatalogCanonicalJson.Property(
                "triggerCosts", new NarrativeMechanicCatalogCanonicalJsonArray(
                    (definition.triggerCosts ?? new List<CharacterSkillTriggerFormulaCostDefinition>())
                    .OrderBy(value => value.trigger)
                    .Select(value => NarrativeMechanicCatalogCanonicalJson.Object(
                        NarrativeMechanicCatalogCanonicalJson.Property(
                            "frequencyUnits", NarrativeMechanicCatalogCanonicalJson.Integer(value.frequencyUnits)),
                        NarrativeMechanicCatalogCanonicalJson.Property(
                            "guaranteedProc", NarrativeMechanicCatalogCanonicalJson.Boolean(value.guaranteedProc)),
                        NarrativeMechanicCatalogCanonicalJson.Property(
                            "trigger", NarrativeMechanicCatalogCanonicalJson.String(value.trigger.ToString())))))));
    }

    private static NarrativeMechanicCatalogCanonicalJsonObject CharacterSkillFormulaJson(
        CharacterSkillModuleRule module,
        string sourcePath)
    {
        NarrativeFormulaCapabilityDescriptor descriptor;
        try
        {
            descriptor = module.formula?.ToRuntime(module.capabilityId)
                ?? throw new InvalidOperationException("formula metadata is missing");
        }
        catch (Exception exception)
        {
            throw new NarrativeMechanicCatalogUnsupportedSourceException(
                "character-skill-formula-invalid",
                $"Character skill module '{module.id}' at '{sourcePath}' has invalid formula metadata: {exception.Message}");
        }
        return FormulaDescriptorJson(descriptor, module.id);
    }

    private static NarrativeMechanicCatalogCanonicalJsonObject FormulaDescriptorJson(
        NarrativeFormulaCapabilityDescriptor descriptor,
        string authorityId)
    {
        return NarrativeMechanicCatalogCanonicalJson.Object(
            NarrativeMechanicCatalogCanonicalJson.Property(
                "appliedParameterIds", StringArray(
                    descriptor.AppliedParameterIds, "formula applied parameter IDs", authorityId)),
            NarrativeMechanicCatalogCanonicalJson.Property(
                "affinityKeys", StringArray(descriptor.AffinityKeys, "formula affinity keys", authorityId)),
            NarrativeMechanicCatalogCanonicalJson.Property(
                "applicatorId", NarrativeMechanicCatalogCanonicalJson.String(descriptor.ApplicatorId)),
            NarrativeMechanicCatalogCanonicalJson.Property(
                "areaCostPerExtraTarget", NarrativeMechanicCatalogCanonicalJson.Integer(
                    descriptor.AreaCostPerExtraTarget)),
            NarrativeMechanicCatalogCanonicalJson.Property(
                "baseCost", NarrativeMechanicCatalogCanonicalJson.Integer(descriptor.BaseCost)),
            NarrativeMechanicCatalogCanonicalJson.Property(
                "guaranteedProcCost", NarrativeMechanicCatalogCanonicalJson.Integer(descriptor.GuaranteedProcCost)),
            NarrativeMechanicCatalogCanonicalJson.Property(
                "conflictGroups", StringArray(descriptor.ConflictGroups, "formula conflict groups", authorityId)),
            NarrativeMechanicCatalogCanonicalJson.Property(
                "forbiddenSynergies", StringArray(descriptor.ForbiddenSynergies, "formula forbidden synergies", authorityId)),
            NarrativeMechanicCatalogCanonicalJson.Property(
                "formatterId", NarrativeMechanicCatalogCanonicalJson.String(descriptor.FormatterId)),
            NarrativeMechanicCatalogCanonicalJson.Property(
                "multiEffectCostPerExtraEffect", NarrativeMechanicCatalogCanonicalJson.Integer(
                    descriptor.MultiEffectCostPerExtraEffect)),
            NarrativeMechanicCatalogCanonicalJson.Property(
                "narrativeAffinityDecimal", NarrativeMechanicCatalogCanonicalJson.String(
                    descriptor.NarrativeAffinity.ToString("R", CultureInfo.InvariantCulture))),
            NarrativeMechanicCatalogCanonicalJson.Property(
                "pairSynergyCosts", new NarrativeMechanicCatalogCanonicalJsonArray(
                    descriptor.PairSynergyCosts.Select(value =>
                        NarrativeMechanicCatalogCanonicalJson.Object(
                            NarrativeMechanicCatalogCanonicalJson.Property(
                                "cost", NarrativeMechanicCatalogCanonicalJson.Integer(value.Cost)),
                            NarrativeMechanicCatalogCanonicalJson.Property(
                                "otherCapabilityId", NarrativeMechanicCatalogCanonicalJson.String(
                                    value.OtherCapabilityId)))))),
            NarrativeMechanicCatalogCanonicalJson.Property(
                "parameterRanges", new NarrativeMechanicCatalogCanonicalJsonArray(
                    NarrativeFormulaParameterIds.Required.Select(parameterId =>
                        CharacterSkillFormulaRangeJson(descriptor.RequireRange(parameterId))))),
            NarrativeMechanicCatalogCanonicalJson.Property(
                "triggerFrequencyCostPerUnit", NarrativeMechanicCatalogCanonicalJson.Integer(
                    descriptor.TriggerFrequencyCostPerUnit)));
    }

    private static NarrativeMechanicCatalogCanonicalJsonObject CharacterSkillFormulaRangeJson(
        NarrativeFormulaQuantizedRange range)
    {
        return NarrativeMechanicCatalogCanonicalJson.Object(
            NarrativeMechanicCatalogCanonicalJson.Property(
                "costPerQuantum", NarrativeMechanicCatalogCanonicalJson.Integer(range.CostPerQuantum)),
            NarrativeMechanicCatalogCanonicalJson.Property(
                "decimalPlaces", NarrativeMechanicCatalogCanonicalJson.Integer(range.DecimalPlaces)),
            NarrativeMechanicCatalogCanonicalJson.Property(
                "maximumUnits", NarrativeMechanicCatalogCanonicalJson.Integer(range.MaximumUnits)),
            NarrativeMechanicCatalogCanonicalJson.Property(
                "minimumUnits", NarrativeMechanicCatalogCanonicalJson.Integer(range.MinimumUnits)),
            NarrativeMechanicCatalogCanonicalJson.Property(
                "parameterId", NarrativeMechanicCatalogCanonicalJson.String(range.ParameterId)),
            NarrativeMechanicCatalogCanonicalJson.Property(
                "quantumUnits", NarrativeMechanicCatalogCanonicalJson.Integer(range.QuantumUnits)));
    }

    private static NarrativeMechanicCatalogCanonicalJsonObject CaptureFacilityEvolution(
        ICollection<string> relevantPaths)
    {
        List<FacilityEvolutionRecipeSO> recipes = FindAssetPaths<FacilityEvolutionRecipeSO>()
            .Select(path => new
            {
                Path = path,
                Recipe = AssetDatabase.LoadAssetAtPath<FacilityEvolutionRecipeSO>(path)
            })
            .Select(value => value.Recipe == null
                ? throw new NarrativeMechanicCatalogUnsupportedSourceException(
                    "facility-recipe-load-failed",
                    $"Could not load facility evolution recipe at '{value.Path}'.")
                : value)
            .OrderBy(value => RequireAuthoredText(value.Recipe.evolutionId, "recipe ID", value.Path), StringComparer.Ordinal)
            .Select(value => value.Recipe)
            .ToList();
        if (recipes.Count == 0)
        {
            throw new NarrativeMechanicCatalogUnsupportedSourceException(
                "facility-recipe-missing",
                "No FacilityEvolutionRecipeSO assets were found.");
        }

        HashSet<string> recipeIds = new HashSet<string>(StringComparer.Ordinal);
        List<NarrativeMechanicCatalogCanonicalJsonValue> exportedRecipes =
            new List<NarrativeMechanicCatalogCanonicalJsonValue>();
        foreach (FacilityEvolutionRecipeSO recipe in recipes)
        {
            string recipePath = AssetDatabase.GetAssetPath(recipe);
            string recipeId = RequireAuthoredText(recipe.evolutionId, "recipe ID", recipePath);
            if (!recipeIds.Add(recipeId))
            {
                throw new NarrativeMechanicCatalogUnsupportedSourceException(
                    "facility-recipe-duplicate",
                    $"Facility evolution recipe ID '{recipeId}' is duplicated.");
            }

            AddAssetAndMeta(relevantPaths, recipePath);
            BuildingSO[] sources = recipe.fromFacilities;
            if (sources == null || sources.Length == 0)
            {
                string lineage = recipe.fromLineageTags == null
                    ? string.Empty
                    : string.Join(",", recipe.fromLineageTags);
                throw new NarrativeMechanicCatalogUnsupportedSourceException(
                    "facility-lineage-only-schema-unsupported",
                    $"Recipe '{recipeId}' has no authored source facility. Lineage-only source '{lineage}' cannot be represented by catalog schema v1.");
            }

            List<NarrativeMechanicCatalogCanonicalJsonValue> sourceFacilities =
                new List<NarrativeMechanicCatalogCanonicalJsonValue>();
            foreach (BuildingSO source in sources)
            {
                sourceFacilities.Add(CaptureFacility(source, "source", recipeId, relevantPaths));
            }

            exportedRecipes.Add(NarrativeMechanicCatalogCanonicalJson.Object(
                NarrativeMechanicCatalogCanonicalJson.Property(
                    "allowedMutationTags", StringArray(
                        recipe.allowedMutationTags,
                        "allowed mutation tags",
                        recipeId)),
                NarrativeMechanicCatalogCanonicalJson.Property(
                    "assetPath", NarrativeMechanicCatalogCanonicalJson.String(recipePath)),
                NarrativeMechanicCatalogCanonicalJson.Property(
                    "displayName", NarrativeMechanicCatalogCanonicalJson.String(
                        RequireAuthoredText(recipe.displayName, "recipe display name", recipeId))),
                NarrativeMechanicCatalogCanonicalJson.Property(
                    "identityPressureWeights", FacilityEvolutionValuesJson(
                        recipe.identityPressureWeights,
                        "identity pressure weights",
                        recipeId)),
                NarrativeMechanicCatalogCanonicalJson.Property(
                    "minimumIdentityScoreDecimal", NarrativeMechanicCatalogCanonicalJson.String(
                        CanonicalFloat(recipe.minimumIdentityScore, recipeId))),
                NarrativeMechanicCatalogCanonicalJson.Property(
                    "recipeId", NarrativeMechanicCatalogCanonicalJson.String(recipeId)),
                NarrativeMechanicCatalogCanonicalJson.Property(
                    "requiredRecordTokens", FacilityEvolutionTokenRequirementsJson(
                        recipe.requiredRecordTokens,
                        recipeId)),
                NarrativeMechanicCatalogCanonicalJson.Property(
                    "requiredRoomMetrics", FacilityEvolutionMetricRequirementsJson(
                        recipe.requiredRoomMetrics,
                        "required room metrics",
                        recipeId)),
                NarrativeMechanicCatalogCanonicalJson.Property(
                    "requiredRoomScores", FacilityEvolutionMetricRequirementsJson(
                        recipe.requiredRoomScores,
                        "required room scores",
                        recipeId)),
                NarrativeMechanicCatalogCanonicalJson.Property(
                    "requiredRoomTags", StringArray(
                        recipe.requiredRoomTags ?? Array.Empty<string>(),
                        "required room tags",
                        recipeId)),
                NarrativeMechanicCatalogCanonicalJson.Property(
                    "resultFacility", CaptureFacility(
                        recipe.resultBuilding,
                        "result",
                        recipeId,
                        relevantPaths)),
                NarrativeMechanicCatalogCanonicalJson.Property(
                    "sourceFacilities", new NarrativeMechanicCatalogCanonicalJsonArray(sourceFacilities))));
        }

        return NarrativeMechanicCatalogCanonicalJson.Object(
            NarrativeMechanicCatalogCanonicalJson.Property(
                "recipes", new NarrativeMechanicCatalogCanonicalJsonArray(exportedRecipes)));
    }

    private static NarrativeMechanicCatalogCanonicalJsonArray
        FacilityEvolutionMetricRequirementsJson(
            IEnumerable<FacilityEvolutionMetricRequirement> requirements,
            string label,
            string sourceId)
    {
        return new NarrativeMechanicCatalogCanonicalJsonArray(
            (requirements ?? Array.Empty<FacilityEvolutionMetricRequirement>())
                .OrderBy(
                    value => RequireAuthoredText(value.key, label, sourceId),
                    StringComparer.Ordinal)
                .Select(value =>
                    (NarrativeMechanicCatalogCanonicalJsonValue)
                    NarrativeMechanicCatalogCanonicalJson.Object(
                        NarrativeMechanicCatalogCanonicalJson.Property(
                            "key", NarrativeMechanicCatalogCanonicalJson.String(
                                RequireAuthoredText(value.key, label, sourceId))),
                        NarrativeMechanicCatalogCanonicalJson.Property(
                            "maximumDecimal", NarrativeMechanicCatalogCanonicalJson.String(
                                CanonicalFloat(value.maxValue, sourceId + ":" + value.key))),
                        NarrativeMechanicCatalogCanonicalJson.Property(
                            "minimumDecimal", NarrativeMechanicCatalogCanonicalJson.String(
                                CanonicalFloat(value.minValue, sourceId + ":" + value.key))),
                        NarrativeMechanicCatalogCanonicalJson.Property(
                            "requireMaximum", NarrativeMechanicCatalogCanonicalJson.Boolean(
                                value.requireMax)),
                        NarrativeMechanicCatalogCanonicalJson.Property(
                            "requireMinimum", NarrativeMechanicCatalogCanonicalJson.Boolean(
                                value.requireMin)))));
    }

    private static NarrativeMechanicCatalogCanonicalJsonArray
        FacilityEvolutionTokenRequirementsJson(
            IEnumerable<FacilityEvolutionTokenRequirement> requirements,
            string sourceId)
    {
        const string label = "required record token";
        return new NarrativeMechanicCatalogCanonicalJsonArray(
            (requirements ?? Array.Empty<FacilityEvolutionTokenRequirement>())
                .OrderBy(
                    value => RequireAuthoredText(value.key, label, sourceId),
                    StringComparer.Ordinal)
                .Select(value =>
                    (NarrativeMechanicCatalogCanonicalJsonValue)
                    NarrativeMechanicCatalogCanonicalJson.Object(
                        NarrativeMechanicCatalogCanonicalJson.Property(
                            "key", NarrativeMechanicCatalogCanonicalJson.String(
                                RequireAuthoredText(value.key, label, sourceId))),
                        NarrativeMechanicCatalogCanonicalJson.Property(
                            "minimumCount", NarrativeMechanicCatalogCanonicalJson.Integer(
                                value.minCount)))));
    }

    private static NarrativeMechanicCatalogCanonicalJsonArray FacilityEvolutionValuesJson(
        IEnumerable<FacilityEvolutionValue> values,
        string label,
        string sourceId)
    {
        return new NarrativeMechanicCatalogCanonicalJsonArray(
            (values ?? Array.Empty<FacilityEvolutionValue>())
                .OrderBy(
                    value => RequireAuthoredText(value.key, label, sourceId),
                    StringComparer.Ordinal)
                .Select(value =>
                    (NarrativeMechanicCatalogCanonicalJsonValue)
                    NarrativeMechanicCatalogCanonicalJson.Object(
                        NarrativeMechanicCatalogCanonicalJson.Property(
                            "key", NarrativeMechanicCatalogCanonicalJson.String(
                                RequireAuthoredText(value.key, label, sourceId))),
                        NarrativeMechanicCatalogCanonicalJson.Property(
                            "valueDecimal", NarrativeMechanicCatalogCanonicalJson.String(
                                CanonicalFloat(value.value, sourceId + ":" + value.key))))));
    }

    private static NarrativeMechanicCatalogCanonicalJsonObject CaptureFacility(
        BuildingSO building,
        string role,
        string recipeId,
        ICollection<string> relevantPaths)
    {
        if (building == null)
        {
            throw new NarrativeMechanicCatalogUnsupportedSourceException(
                "facility-reference-missing",
                $"Recipe '{recipeId}' has a missing {role} facility reference.");
        }

        if (building.id <= 0)
        {
            throw new NarrativeMechanicCatalogUnsupportedSourceException(
                "facility-id-invalid",
                $"Recipe '{recipeId}' {role} facility '{building.name}' has no positive DataScriptableObject ID.");
        }

        string assetPath = AssetDatabase.GetAssetPath(building);
        if (string.IsNullOrWhiteSpace(assetPath))
        {
            throw new NarrativeMechanicCatalogUnsupportedSourceException(
                "facility-asset-path-missing",
                $"Recipe '{recipeId}' {role} facility '{building.name}' is not an asset.");
        }

        string displayName = RequireAuthoredText(
            building.objectName,
            "facility display name",
            assetPath);
        AddAssetAndMeta(relevantPaths, assetPath);
        return NarrativeMechanicCatalogCanonicalJson.Object(
            NarrativeMechanicCatalogCanonicalJson.Property(
                "assetPath", NarrativeMechanicCatalogCanonicalJson.String(assetPath)),
            NarrativeMechanicCatalogCanonicalJson.Property(
                "displayName", NarrativeMechanicCatalogCanonicalJson.String(displayName)),
            NarrativeMechanicCatalogCanonicalJson.Property(
                "facilityId", NarrativeMechanicCatalogCanonicalJson.String($"building:{building.id}")));
    }

    /// <summary>
    /// Catalog schema v1 uses the consumer vocabulary Uncommon/Epic while gameplay
    /// deliberately retains Advanced/Heroic.  This is a wire-protocol projection,
    /// not a gameplay rarity fallback or a change to authored balance.
    /// </summary>
    private static string ToCatalogRarityToken(CharacterSkillRarity rarity)
    {
        return rarity switch
        {
            CharacterSkillRarity.Common => "Common",
            CharacterSkillRarity.Advanced => "Uncommon",
            CharacterSkillRarity.Rare => "Rare",
            CharacterSkillRarity.Heroic => "Epic",
            CharacterSkillRarity.Legendary => "Legendary",
            _ => throw new NarrativeMechanicCatalogUnsupportedSourceException(
                "character-skill-rarity-unmapped",
                $"Character skill rarity '{rarity}' has no catalog schema v1 token mapping.")
        };
    }

    private static NarrativeMechanicCatalogCanonicalJsonObject BuildRarityProtocol()
    {
        List<NarrativeMechanicCatalogCanonicalJsonValue> mappings =
            Enum.GetValues(typeof(CharacterSkillRarity))
                .Cast<CharacterSkillRarity>()
                .OrderBy(value => value)
                .Select(value =>
                    (NarrativeMechanicCatalogCanonicalJsonValue)
                    NarrativeMechanicCatalogCanonicalJson.Object(
                        NarrativeMechanicCatalogCanonicalJson.Property(
                            "catalogToken", NarrativeMechanicCatalogCanonicalJson.String(
                                ToCatalogRarityToken(value))),
                        NarrativeMechanicCatalogCanonicalJson.Property(
                            "gameplayToken", NarrativeMechanicCatalogCanonicalJson.String(
                                value.ToString()))))
                .ToList();
        return NarrativeMechanicCatalogCanonicalJson.Object(
            NarrativeMechanicCatalogCanonicalJson.Property(
                "mapping", new NarrativeMechanicCatalogCanonicalJsonArray(mappings)),
            NarrativeMechanicCatalogCanonicalJson.Property(
                "policy", NarrativeMechanicCatalogCanonicalJson.String(
                    "wire-token-projection-only; gameplay enum order and authored budgets are unchanged")),
            NarrativeMechanicCatalogCanonicalJson.Property(
                "protocolId", NarrativeMechanicCatalogCanonicalJson.String(
                    "character-skill-rarity-v1")),
            NarrativeMechanicCatalogCanonicalJson.Property(
                "schemaVersion", NarrativeMechanicCatalogCanonicalJson.Integer(1)));
    }

    private static NarrativeMechanicCatalogCanonicalJsonObject CaptureEquipmentEvolution()
    {
        EquipmentHistoricalEffectDefinition[] effects = EquipmentHistoricalEffectCatalog.All
            .Where(value => value != null)
            .OrderBy(value => value.EffectId, StringComparer.Ordinal)
            .ToArray();
        if (effects.Length == 0)
        {
            throw new NarrativeMechanicCatalogUnsupportedSourceException(
                "equipment-historical-effect-catalog-empty",
                "EquipmentHistoricalEffectCatalog has no exportable historical effects.");
        }

        HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);
        List<NarrativeMechanicCatalogCanonicalJsonValue> captured =
            new List<NarrativeMechanicCatalogCanonicalJsonValue>(effects.Length);
        for (int ordinal = 0; ordinal < effects.Length; ordinal++)
        {
            EquipmentHistoricalEffectDefinition effect = effects[ordinal];
            string effectId = RequireAuthoredText(
                effect.EffectId,
                "equipment historical effect ID",
                nameof(EquipmentHistoricalEffectCatalog));
            if (!ids.Add(effectId))
            {
                throw new NarrativeMechanicCatalogUnsupportedSourceException(
                    "equipment-historical-effect-duplicate",
                    $"Equipment historical effect ID '{effectId}' is duplicated.");
            }

            captured.Add(NarrativeMechanicCatalogCanonicalJson.Object(
                NarrativeMechanicCatalogCanonicalJson.Property(
                    "description", NarrativeMechanicCatalogCanonicalJson.String(
                        RequireAuthoredText(effect.Description, "equipment effect description", effectId))),
                NarrativeMechanicCatalogCanonicalJson.Property(
                    "displayName", NarrativeMechanicCatalogCanonicalJson.String(
                        RequireAuthoredText(effect.DisplayName, "equipment effect display name", effectId))),
                NarrativeMechanicCatalogCanonicalJson.Property(
                    "effectId", NarrativeMechanicCatalogCanonicalJson.String(effectId)),
                NarrativeMechanicCatalogCanonicalJson.Property(
                    "ordinal", NarrativeMechanicCatalogCanonicalJson.Integer(ordinal))));
        }

        return NarrativeMechanicCatalogCanonicalJson.Object(
            NarrativeMechanicCatalogCanonicalJson.Property(
                "effects", new NarrativeMechanicCatalogCanonicalJsonArray(captured)),
            NarrativeMechanicCatalogCanonicalJson.Property(
                "implementationStatus", NarrativeMechanicCatalogCanonicalJson.String("implemented")),
            NarrativeMechanicCatalogCanonicalJson.Property(
                "sourceCatalog", NarrativeMechanicCatalogCanonicalJson.String(
                    nameof(EquipmentHistoricalEffectCatalog))));
    }

    private static bool TryCaptureAcquiredTrait(
        ICollection<string> relevantPaths,
        out NarrativeMechanicCatalogCanonicalJsonObject section,
        out CharacterAcquiredTraitSettingsSO settings,
        out CharacterAcquiredTraitModuleSO[] modules,
        out string unavailableReason)
    {
        section = null;
        settings = null;
        modules = Array.Empty<CharacterAcquiredTraitModuleSO>();
        unavailableReason = string.Empty;

        string[] rootPaths = FindAssetPaths<GameContentCatalogSO>();
        if (rootPaths.Length == 0)
        {
            unavailableReason = "The GameContentCatalogSO root asset is absent, so acquired-trait authoring cannot be resolved.";
            return false;
        }
        if (rootPaths.Length != 1)
        {
            throw new NarrativeMechanicCatalogUnsupportedSourceException(
                "game-content-catalog-cardinality",
                $"Expected exactly one GameContentCatalogSO asset, found {rootPaths.Length}.");
        }

        GameContentCatalogSO root = AssetDatabase.LoadAssetAtPath<GameContentCatalogSO>(rootPaths[0]);
        if (root == null)
        {
            throw new NarrativeMechanicCatalogUnsupportedSourceException(
                "game-content-catalog-load-failed",
                $"Could not load game content catalog at '{rootPaths[0]}'.");
        }
        GameDomainContentCatalogSO[] domains = root.DomainCatalogs
            .OfType<GameDomainContentCatalogSO>()
            .ToArray();
        if (domains.Length != 1)
        {
            throw new NarrativeMechanicCatalogUnsupportedSourceException(
                "game-domain-catalog-cardinality",
                $"Root game content catalog must reference exactly one GameDomainContentCatalogSO; found {domains.Length}.");
        }
        ItemDefinitionCatalogSO itemCatalog = root.GetItemDefinitions<ItemDefinitionCatalogSO>();
        if (itemCatalog == null)
        {
            throw new NarrativeMechanicCatalogUnsupportedSourceException(
                "item-definition-catalog-missing",
                "Root game content catalog has no ItemDefinitionCatalogSO reference.");
        }

        GameDomainContentCatalogSO domainCatalog = domains[0];
        CharacterAcquiredTraitSettingsSO[] indexedSettings = domainCatalog
            .GetAll<CharacterAcquiredTraitSettingsSO>()
            .Where(value => string.Equals(
                value.SettingsId,
                V25AcquiredTraitContentAssetBuilder.SettingsId,
                StringComparison.Ordinal))
            .ToArray();
        if (indexedSettings.Length == 0)
        {
            bool authoredButUnindexed = FindAssetPaths<CharacterAcquiredTraitSettingsSO>()
                .Select(path => AssetDatabase.LoadAssetAtPath<CharacterAcquiredTraitSettingsSO>(path))
                .Any(value => value != null && string.Equals(
                    value.SettingsId,
                    V25AcquiredTraitContentAssetBuilder.SettingsId,
                    StringComparison.Ordinal));
            if (authoredButUnindexed)
            {
                throw new NarrativeMechanicCatalogUnsupportedSourceException(
                    "acquired-trait-settings-not-catalogued",
                    "The acquired-trait settings asset exists but is not indexed by GameDomainContentCatalogSO.");
            }

            unavailableReason = "The V25 acquired-trait settings asset is not yet indexed by GameDomainContentCatalogSO.";
            return false;
        }
        if (indexedSettings.Length != 1)
        {
            throw new NarrativeMechanicCatalogUnsupportedSourceException(
                "acquired-trait-settings-cardinality",
                $"Expected one indexed acquired-trait settings asset, found {indexedSettings.Length}.");
        }

        settings = indexedSettings[0];
        RequireNoValidationErrors(
            settings.ValidateDefinition(),
            "acquired-trait-settings-invalid",
            settings.SettingsId);

        modules = domainCatalog.GetAll<CharacterAcquiredTraitModuleSO>()
            .OrderBy(value => value.ModuleId, StringComparer.Ordinal)
            .ToArray();
        if (modules.Length == 0)
        {
            if (FindAssetPaths<CharacterAcquiredTraitModuleSO>().Length > 0)
            {
                throw new NarrativeMechanicCatalogUnsupportedSourceException(
                    "acquired-trait-modules-not-catalogued",
                    "Acquired-trait module assets exist but are not indexed by GameDomainContentCatalogSO.");
            }
            unavailableReason = "No acquired-trait module assets are indexed by GameDomainContentCatalogSO.";
            return false;
        }
        if (modules.Select(value => value.ModuleId)
            .Distinct(StringComparer.Ordinal).Count() != modules.Length)
        {
            throw new NarrativeMechanicCatalogUnsupportedSourceException(
                "acquired-trait-module-duplicate",
                "GameDomainContentCatalogSO contains duplicate acquired-trait module IDs.");
        }

        ItemDefinitionSO[] seals = itemCatalog.Definitions
            .Where(value => value != null && string.Equals(
                value.ItemId,
                V25AcquiredTraitContentAssetBuilder.SealItemId,
                StringComparison.Ordinal))
            .ToArray();
        if (seals.Length == 0)
        {
            if (AssetDatabase.LoadAssetAtPath<ItemDefinitionSO>(
                    V25AcquiredTraitContentAssetBuilder.SealItemPath) != null)
            {
                throw new NarrativeMechanicCatalogUnsupportedSourceException(
                    "acquired-trait-erase-item-not-catalogued",
                    "The acquired-trait erasure seal asset exists but is not indexed by ItemDefinitionCatalogSO.");
            }
            unavailableReason = "The physical acquired-trait erasure seal is not indexed by ItemDefinitionCatalogSO.";
            return false;
        }
        if (seals.Length != 1)
        {
            throw new NarrativeMechanicCatalogUnsupportedSourceException(
                "acquired-trait-erase-item-cardinality",
                $"Expected one acquired-trait erasure seal item, found {seals.Length}.");
        }
        ItemDefinitionSO seal = seals[0];
        RequireNoValidationErrors(
            seal.ValidateDefinition(),
            "acquired-trait-erase-item-invalid",
            seal.ItemId);
        RequireNoValidationErrors(
            V25AcquiredTraitContentAssetBuilder.ValidateApprovedAuthoringPolicy(
                settings,
                modules,
                seal),
            "acquired-trait-policy-drift",
            settings.SettingsId);
        RequireNoValidationErrors(
            V25AcquiredTraitContentAssetBuilder.ValidateRewardOnlySealTradeExposure(
                (GenericItemDefinitionSO)seal,
                itemCatalog),
            "acquired-trait-seal-trade-exposure",
            seal.ItemId);

        AddAssetAndMeta(relevantPaths, rootPaths[0]);
        AddAssetObjectAndMeta(relevantPaths, domainCatalog, "game domain content catalog");
        AddAssetObjectAndMeta(relevantPaths, itemCatalog, "item definition catalog");
        AddAssetObjectAndMeta(relevantPaths, settings, "acquired-trait settings");
        AddAssetObjectAndMeta(relevantPaths, seal, "acquired-trait erasure seal");

        List<NarrativeMechanicCatalogCanonicalJsonValue> capturedModules =
            new List<NarrativeMechanicCatalogCanonicalJsonValue>(modules.Length);
        for (int ordinal = 0; ordinal < modules.Length; ordinal++)
        {
            capturedModules.Add(CaptureAcquiredTraitModule(
                modules[ordinal],
                ordinal,
                relevantPaths));
        }
        List<NarrativeMechanicCatalogCanonicalJsonValue> capturedDrawbacks =
            settings.DrawbackCapabilities
                .OrderBy(value => value.DrawbackId, StringComparer.Ordinal)
                .Select((value, ordinal) => CaptureAcquiredTraitDrawback(
                    value, ordinal, relevantPaths))
                .ToList();

        List<NarrativeMechanicCatalogCanonicalJsonValue> gates =
            new List<NarrativeMechanicCatalogCanonicalJsonValue>();
        List<NarrativeMechanicCatalogCanonicalJsonValue> gatePolicyEntries =
            new List<NarrativeMechanicCatalogCanonicalJsonValue>();
        for (int ordinal = 0; ordinal < settings.ManifestationGates.Count; ordinal++)
        {
            CharacterAcquiredTraitManifestationGateDefinition gate =
                settings.ManifestationGates[ordinal];
            gates.Add(NarrativeMechanicCatalogCanonicalJson.Integer(
                gate.MeaningfulRecordMilestone));
            gatePolicyEntries.Add(NarrativeMechanicCatalogCanonicalJson.Object(
                NarrativeMechanicCatalogCanonicalJson.Property(
                    "budget", NarrativeMechanicCatalogCanonicalJson.Integer(gate.Budget)),
                NarrativeMechanicCatalogCanonicalJson.Property(
                    "catalogRarity", NarrativeMechanicCatalogCanonicalJson.String(
                        ToCatalogRarityToken(gate.Rarity))),
                NarrativeMechanicCatalogCanonicalJson.Property(
                    "gameplayRarity", NarrativeMechanicCatalogCanonicalJson.String(
                        gate.Rarity.ToString())),
                NarrativeMechanicCatalogCanonicalJson.Property(
                    "milestone", NarrativeMechanicCatalogCanonicalJson.Integer(
                        gate.MeaningfulRecordMilestone)),
                NarrativeMechanicCatalogCanonicalJson.Property(
                    "ordinal", NarrativeMechanicCatalogCanonicalJson.Integer(ordinal))));
        }

        section = NarrativeMechanicCatalogCanonicalJson.Object(
            NarrativeMechanicCatalogCanonicalJson.Property(
                "activeConflictPolicy", NarrativeMechanicCatalogCanonicalJson.Object(
                    NarrativeMechanicCatalogCanonicalJson.Property(
                        "activeModule", NarrativeMechanicCatalogCanonicalJson.String("reject-duplicate-module-id")),
                    NarrativeMechanicCatalogCanonicalJson.Property(
                        "conflictGroup", NarrativeMechanicCatalogCanonicalJson.String("reject-shared-conflict-group")),
                    NarrativeMechanicCatalogCanonicalJson.Property(
                        "effectBinding", NarrativeMechanicCatalogCanonicalJson.String("reject-shared-effect-binding-id")))),
            NarrativeMechanicCatalogCanonicalJson.Property(
                "combinationIdentityPolicy", NarrativeMechanicCatalogCanonicalJson.Object(
                    NarrativeMechanicCatalogCanonicalJson.Property(
                        "algorithm", NarrativeMechanicCatalogCanonicalJson.String("sha256-utf8")),
                    NarrativeMechanicCatalogCanonicalJson.Property(
                        "moduleIdOrder", NarrativeMechanicCatalogCanonicalJson.String("ordinal-ascending")),
                    NarrativeMechanicCatalogCanonicalJson.Property(
                        "prefix", NarrativeMechanicCatalogCanonicalJson.String(
                            CharacterAcquiredTraitCombinationIdentity.Prefix)))),
            NarrativeMechanicCatalogCanonicalJson.Property(
                "drawbackCapabilities",
                new NarrativeMechanicCatalogCanonicalJsonArray(capturedDrawbacks)),
            NarrativeMechanicCatalogCanonicalJson.Property(
                "eraseDisplayName", NarrativeMechanicCatalogCanonicalJson.String(
                    RequireAuthoredText(seal.DisplayName, "erasure seal display name", seal.ItemId))),
            NarrativeMechanicCatalogCanonicalJson.Property(
                "eraseItemId", NarrativeMechanicCatalogCanonicalJson.String(seal.ItemId)),
            NarrativeMechanicCatalogCanonicalJson.Property(
                "experienceScorePolicy", NarrativeMechanicCatalogCanonicalJson.Object(
                    NarrativeMechanicCatalogCanonicalJson.Property(
                        "groupKey", StringArray(new[] { "domain", "factId" }, "experience score group key", settings.SettingsId)),
                    NarrativeMechanicCatalogCanonicalJson.Property(
                        "thresholds", IntegerArray(CharacterAcquiredTraitExperienceScore.Thresholds)))),
            NarrativeMechanicCatalogCanonicalJson.Property(
                "gates", new NarrativeMechanicCatalogCanonicalJsonArray(gates)),
            NarrativeMechanicCatalogCanonicalJson.Property(
                "gatePolicy", NarrativeMechanicCatalogCanonicalJson.Object(
                    NarrativeMechanicCatalogCanonicalJson.Property(
                        "entries", new NarrativeMechanicCatalogCanonicalJsonArray(
                            gatePolicyEntries)),
                    NarrativeMechanicCatalogCanonicalJson.Property(
                        "policy", NarrativeMechanicCatalogCanonicalJson.String(
                            "gates is the catalog-v1 milestone list; this object preserves authored gameplay rarity and budget")),
                    NarrativeMechanicCatalogCanonicalJson.Property(
                        "rarityProtocolId", NarrativeMechanicCatalogCanonicalJson.String(
                            "character-skill-rarity-v1")),
                    NarrativeMechanicCatalogCanonicalJson.Property(
                        "schemaVersion", NarrativeMechanicCatalogCanonicalJson.Integer(1)))),
            NarrativeMechanicCatalogCanonicalJson.Property(
                "implementationStatus", NarrativeMechanicCatalogCanonicalJson.String("implemented")),
            NarrativeMechanicCatalogCanonicalJson.Property(
                "formulaPolicy", AcquiredTraitFormulaPolicyJson(settings)),
            NarrativeMechanicCatalogCanonicalJson.Property(
                "maxActive", NarrativeMechanicCatalogCanonicalJson.Integer(settings.MaximumActiveTraits)),
            NarrativeMechanicCatalogCanonicalJson.Property(
                "modules", new NarrativeMechanicCatalogCanonicalJsonArray(capturedModules)),
            NarrativeMechanicCatalogCanonicalJson.Property(
                "responseContract", NarrativeMechanicCatalogCanonicalJson.Object(
                    NarrativeMechanicCatalogCanonicalJson.Property(
                        "exactRootKeys", StringArray(CharacterSkillResponseKeys, "response keys", settings.SettingsId)),
                    NarrativeMechanicCatalogCanonicalJson.Property(
                        "profileId", NarrativeMechanicCatalogCanonicalJson.String(
                            LocalLlmRequestProfiles.AcquiredTrait.Id)))),
            NarrativeMechanicCatalogCanonicalJson.Property(
                "settingsId", NarrativeMechanicCatalogCanonicalJson.String(settings.SettingsId)));
        return true;
    }

    private static NarrativeMechanicCatalogCanonicalJsonObject AcquiredTraitFormulaPolicyJson(
        CharacterAcquiredTraitSettingsSO settings)
    {
        NarrativeFormulaStrengthPolicy policy = settings.RequireFormulaPolicy();
        NarrativeFormulaDrawbackCreditPolicy drawback = settings.FormulaPolicy
            .RequireDrawbackPolicy();
        NarrativeFormulaGenerationCostContext context = settings.FormulaPolicy
            .RequireGenerationContext();
        return NarrativeMechanicCatalogCanonicalJson.Object(
            NarrativeMechanicCatalogCanonicalJson.Property(
                "baseBudget", NarrativeMechanicCatalogCanonicalJson.Integer(policy.BaseBudget)),
            NarrativeMechanicCatalogCanonicalJson.Property(
                "catalogSha256", NarrativeMechanicCatalogCanonicalJson.String(
                    settings.FormulaPolicy.RequireCatalogSha256())),
            NarrativeMechanicCatalogCanonicalJson.Property(
                "drawbackCredit", NarrativeMechanicCatalogCanonicalJson.Object(
                    NarrativeMechanicCatalogCanonicalJson.Property(
                        "absoluteMaximumCredit", NarrativeMechanicCatalogCanonicalJson.Integer(
                            drawback.AbsoluteMaximumCredit)),
                    NarrativeMechanicCatalogCanonicalJson.Property(
                        "automaticMaximumBudgetFractionDecimal",
                        NarrativeMechanicCatalogCanonicalJson.String(
                            drawback.AutomaticMaximumBudgetFraction.ToString(
                                "R", CultureInfo.InvariantCulture))),
                    NarrativeMechanicCatalogCanonicalJson.Property(
                        "playerChoiceMaximumBudgetFractionDecimal",
                        NarrativeMechanicCatalogCanonicalJson.String(
                            drawback.PlayerChoiceMaximumBudgetFraction.ToString(
                                "R", CultureInfo.InvariantCulture))),
                    NarrativeMechanicCatalogCanonicalJson.Property(
                        "requireNegativeEvidenceForAutomatic",
                        NarrativeMechanicCatalogCanonicalJson.Boolean(
                            drawback.RequireNegativeEvidenceForAutomatic)))),
            NarrativeMechanicCatalogCanonicalJson.Property(
                "formulaVersion", NarrativeMechanicCatalogCanonicalJson.Integer(
                    policy.FormulaVersion)),
            NarrativeMechanicCatalogCanonicalJson.Property(
                "generationContext", NarrativeMechanicCatalogCanonicalJson.Object(
                    NarrativeMechanicCatalogCanonicalJson.Property(
                        "guaranteedProc", NarrativeMechanicCatalogCanonicalJson.Boolean(
                            context.GuaranteedProc)),
                    NarrativeMechanicCatalogCanonicalJson.Property(
                        "targetCount", NarrativeMechanicCatalogCanonicalJson.Integer(
                            context.TargetCount)),
                    NarrativeMechanicCatalogCanonicalJson.Property(
                        "triggerFrequencyUnits", NarrativeMechanicCatalogCanonicalJson.Integer(
                            context.TriggerFrequencyUnits)))),
            NarrativeMechanicCatalogCanonicalJson.Property(
                "maximumImportanceDecimal", NarrativeMechanicCatalogCanonicalJson.String(
                    policy.MaximumImportance.ToString("R", CultureInfo.InvariantCulture))),
            NarrativeMechanicCatalogCanonicalJson.Property(
                "milestoneWeightsDecimal", new NarrativeMechanicCatalogCanonicalJsonArray(
                    policy.MilestoneWeights.Select(value =>
                        NarrativeMechanicCatalogCanonicalJson.String(
                            value.ToString("R", CultureInfo.InvariantCulture))))),
            NarrativeMechanicCatalogCanonicalJson.Property(
                "minimumImportanceDecimal", NarrativeMechanicCatalogCanonicalJson.String(
                    policy.MinimumImportance.ToString("R", CultureInfo.InvariantCulture))),
            NarrativeMechanicCatalogCanonicalJson.Property(
                "powerScale", NarrativeMechanicCatalogCanonicalJson.Integer(policy.PowerScale)),
            NarrativeMechanicCatalogCanonicalJson.Property(
                "softCapKDecimal", NarrativeMechanicCatalogCanonicalJson.String(
                    policy.SoftCapK.ToString("R", CultureInfo.InvariantCulture))));
    }

    private static NarrativeMechanicCatalogCanonicalJsonValue CaptureAcquiredTraitDrawback(
        CharacterAcquiredTraitDrawbackCapabilityDefinition drawback,
        int ordinal,
        ICollection<string> relevantPaths)
    {
        if (drawback == null || drawback.ValidateDefinition().Count > 0)
            throw new NarrativeMechanicCatalogUnsupportedSourceException(
                "acquired-trait-drawback-invalid",
                "Acquired-trait drawback authoring is null or invalid.");
        List<NarrativeMechanicCatalogCanonicalJsonValue> effects = drawback.Effects
            .OrderBy(value => value.bindingId, StringComparer.Ordinal)
            .Select((value, effectOrdinal) => CaptureEffectBinding(
                drawback.DrawbackId, value, effectOrdinal, relevantPaths))
            .ToList();
        return NarrativeMechanicCatalogCanonicalJson.Object(
            NarrativeMechanicCatalogCanonicalJson.Property(
                "conflictGroups", StringArray(drawback.ConflictGroups,
                    "drawback conflict groups", drawback.DrawbackId)),
            NarrativeMechanicCatalogCanonicalJson.Property(
                "description", NarrativeMechanicCatalogCanonicalJson.String(
                    drawback.Description)),
            NarrativeMechanicCatalogCanonicalJson.Property(
                "displayName", NarrativeMechanicCatalogCanonicalJson.String(
                    drawback.DisplayName)),
            NarrativeMechanicCatalogCanonicalJson.Property(
                "domainAffinities", StringArray(drawback.DomainAffinities
                    .Select(value => value.ToString()).OrderBy(value => value,
                        StringComparer.Ordinal), "drawback domains", drawback.DrawbackId)),
            NarrativeMechanicCatalogCanonicalJson.Property(
                "drawbackId", NarrativeMechanicCatalogCanonicalJson.String(
                    drawback.DrawbackId)),
            NarrativeMechanicCatalogCanonicalJson.Property(
                "effectBindings", new NarrativeMechanicCatalogCanonicalJsonArray(effects)),
            NarrativeMechanicCatalogCanonicalJson.Property(
                "formula", FormulaDescriptorJson(
                    drawback.RequireFormulaDescriptor(), drawback.DrawbackId)),
            NarrativeMechanicCatalogCanonicalJson.Property(
                "maximumCredit", NarrativeMechanicCatalogCanonicalJson.Integer(
                    drawback.MaximumCredit)),
            NarrativeMechanicCatalogCanonicalJson.Property(
                "ordinal", NarrativeMechanicCatalogCanonicalJson.Integer(ordinal)));
    }

    private static NarrativeMechanicCatalogCanonicalJsonValue CaptureAcquiredTraitModule(
        CharacterAcquiredTraitModuleSO module,
        int ordinal,
        ICollection<string> relevantPaths)
    {
        if (module == null)
        {
            throw new NarrativeMechanicCatalogUnsupportedSourceException(
                "acquired-trait-module-null",
                "GameDomainContentCatalogSO contains a null acquired-trait module.");
        }
        RequireNoValidationErrors(
            module.ValidateDefinition(),
            "acquired-trait-module-invalid",
            module.ModuleId);
        AddAssetObjectAndMeta(relevantPaths, module, "acquired-trait module");

        List<NarrativeMechanicCatalogCanonicalJsonValue> effects = module.Effects
            .OrderBy(value => value.bindingId, StringComparer.Ordinal)
            .Select((value, effectOrdinal) => CaptureEffectBinding(
                module,
                value,
                effectOrdinal,
                relevantPaths))
            .ToList();
        List<NarrativeMechanicCatalogCanonicalJsonValue> reactions =
            module.SpecialReactions
                .Where(value => value != null)
                .OrderBy(value => value.ReactionId, StringComparer.Ordinal)
                .Select((value, reactionOrdinal) =>
                    CaptureAcquiredTraitReaction(value, reactionOrdinal))
                .ToList();
        return NarrativeMechanicCatalogCanonicalJson.Object(
            NarrativeMechanicCatalogCanonicalJson.Property(
                "conflictGroups", StringArray(module.ConflictGroups
                    .Select(value => value.Trim())
                    .OrderBy(value => value, StringComparer.Ordinal), "conflict groups", module.ModuleId)),
            NarrativeMechanicCatalogCanonicalJson.Property(
                "cost", NarrativeMechanicCatalogCanonicalJson.Integer(module.Cost)),
            NarrativeMechanicCatalogCanonicalJson.Property(
                "description", NarrativeMechanicCatalogCanonicalJson.String(module.Description)),
            NarrativeMechanicCatalogCanonicalJson.Property(
                "displayName", NarrativeMechanicCatalogCanonicalJson.String(module.DisplayName)),
            NarrativeMechanicCatalogCanonicalJson.Property(
                "domainAffinities", StringArray(module.DomainAffinities
                    .Select(value => value.ToString())
                    .OrderBy(value => value, StringComparer.Ordinal), "domain affinities", module.ModuleId)),
            NarrativeMechanicCatalogCanonicalJson.Property(
                "drawbackBindingIds", StringArray(module.DrawbackBindingIds,
                    "legacy drawback binding IDs", module.ModuleId)),
            NarrativeMechanicCatalogCanonicalJson.Property(
                "effectBindings", new NarrativeMechanicCatalogCanonicalJsonArray(effects)),
            NarrativeMechanicCatalogCanonicalJson.Property(
                "formula", FormulaDescriptorJson(
                    module.RequireFormulaDescriptor(), module.ModuleId)),
            NarrativeMechanicCatalogCanonicalJson.Property(
                "moduleId", NarrativeMechanicCatalogCanonicalJson.String(module.ModuleId)),
            NarrativeMechanicCatalogCanonicalJson.Property(
                "ordinal", NarrativeMechanicCatalogCanonicalJson.Integer(ordinal)),
            NarrativeMechanicCatalogCanonicalJson.Property(
                "specialReactions",
                new NarrativeMechanicCatalogCanonicalJsonArray(reactions)));
    }

    private static NarrativeMechanicCatalogCanonicalJsonValue
        CaptureAcquiredTraitReaction(
            CharacterAcquiredTraitSpecialReactionDefinition reaction,
            int ordinal) => NarrativeMechanicCatalogCanonicalJson.Object(
            NarrativeMechanicCatalogCanonicalJson.Property(
                "action", NarrativeMechanicCatalogCanonicalJson.String(
                    reaction.Action.ToString())),
            NarrativeMechanicCatalogCanonicalJson.Property(
                "baseValue", NarrativeMechanicCatalogCanonicalJson.String(
                    reaction.BaseValue.ToString("R", CultureInfo.InvariantCulture))),
            NarrativeMechanicCatalogCanonicalJson.Property(
                "condition", NarrativeMechanicCatalogCanonicalJson.String(
                    reaction.Condition.ToString())),
            NarrativeMechanicCatalogCanonicalJson.Property(
                "cooldownDays", NarrativeMechanicCatalogCanonicalJson.Integer(
                    reaction.CooldownDays)),
            NarrativeMechanicCatalogCanonicalJson.Property(
                "displayLabel", NarrativeMechanicCatalogCanonicalJson.String(
                    reaction.DisplayLabel)),
            NarrativeMechanicCatalogCanonicalJson.Property(
                "durationDays", NarrativeMechanicCatalogCanonicalJson.Integer(
                    reaction.DurationDays)),
            NarrativeMechanicCatalogCanonicalJson.Property(
                "maximumLifetimeTriggers",
                NarrativeMechanicCatalogCanonicalJson.Integer(
                    reaction.MaximumLifetimeTriggers)),
            NarrativeMechanicCatalogCanonicalJson.Property(
                "maximumTriggersPerDay",
                NarrativeMechanicCatalogCanonicalJson.Integer(
                    reaction.MaximumTriggersPerDay)),
            NarrativeMechanicCatalogCanonicalJson.Property(
                "ordinal", NarrativeMechanicCatalogCanonicalJson.Integer(ordinal)),
            NarrativeMechanicCatalogCanonicalJson.Property(
                "ownerRole", NarrativeMechanicCatalogCanonicalJson.String(
                    reaction.OwnerRole.ToString())),
            NarrativeMechanicCatalogCanonicalJson.Property(
                "reactionId", NarrativeMechanicCatalogCanonicalJson.String(
                    reaction.ReactionId)),
            NarrativeMechanicCatalogCanonicalJson.Property(
                "trigger", NarrativeMechanicCatalogCanonicalJson.String(
                    reaction.Trigger.ToString())));

    private static NarrativeMechanicCatalogCanonicalJsonValue CaptureEffectBinding(
        CharacterAcquiredTraitModuleSO module,
        GameplayEffectBinding binding,
        int ordinal,
        ICollection<string> relevantPaths) => CaptureEffectBinding(
            module.ModuleId, binding, ordinal, relevantPaths);

    private static NarrativeMechanicCatalogCanonicalJsonValue CaptureEffectBinding(
        string sourceId,
        GameplayEffectBinding binding,
        int ordinal,
        ICollection<string> relevantPaths)
    {
        GameplayEffectSourceRef source = new GameplayEffectSourceRef(
            GameplayEffectSourceKind.AcquiredTrait,
            sourceId);
        string reason = "null binding";
        if (binding == null || !binding.IsValidFor(source, out reason))
        {
            throw new NarrativeMechanicCatalogUnsupportedSourceException(
                "acquired-trait-effect-binding-invalid",
                $"Acquired-trait source '{sourceId}' has an invalid effect binding: {reason}.");
        }
        GameplayEffectDefinitionSO definition = binding.definition;
        RequireNoValidationErrors(
            definition.ValidateDefinition(),
            "acquired-trait-effect-definition-invalid",
            definition.EffectId);
        AddAssetObjectAndMeta(relevantPaths, definition, "acquired-trait effect definition");
        if (binding.condition != null)
        {
            if (string.IsNullOrWhiteSpace(binding.condition.ConditionId))
            {
                throw new NarrativeMechanicCatalogUnsupportedSourceException(
                    "acquired-trait-effect-condition-invalid",
                    $"Acquired-trait effect binding '{binding.bindingId}' has a blank condition ID.");
            }
            AddAssetObjectAndMeta(relevantPaths, binding.condition, "acquired-trait effect condition");
        }

        return NarrativeMechanicCatalogCanonicalJson.Object(
            NarrativeMechanicCatalogCanonicalJson.Property(
                "bindingId", NarrativeMechanicCatalogCanonicalJson.String(binding.bindingId.Trim())),
            NarrativeMechanicCatalogCanonicalJson.Property(
                "conditionId", NarrativeMechanicCatalogCanonicalJson.String(
                    binding.condition?.ConditionId ?? string.Empty)),
            NarrativeMechanicCatalogCanonicalJson.Property(
                "definition", NarrativeMechanicCatalogCanonicalJson.Object(
                    NarrativeMechanicCatalogCanonicalJson.Property(
                        "allowedSources", NarrativeMechanicCatalogCanonicalJson.String(
                            definition.AllowedSources.ToString())),
                    NarrativeMechanicCatalogCanonicalJson.Property(
                        "effectId", NarrativeMechanicCatalogCanonicalJson.String(definition.EffectId)),
                    NarrativeMechanicCatalogCanonicalJson.Property(
                        "maximumResultDecimal", NarrativeMechanicCatalogCanonicalJson.String(
                            CanonicalFloat(definition.MaximumResult, definition.EffectId))),
                    NarrativeMechanicCatalogCanonicalJson.Property(
                        "minimumResultDecimal", NarrativeMechanicCatalogCanonicalJson.String(
                            CanonicalFloat(definition.MinimumResult, definition.EffectId))),
                    NarrativeMechanicCatalogCanonicalJson.Property(
                        "operation", NarrativeMechanicCatalogCanonicalJson.String(definition.Operation.ToString())),
                    NarrativeMechanicCatalogCanonicalJson.Property(
                        "projectionPhase", NarrativeMechanicCatalogCanonicalJson.String(definition.ProjectionPhase.ToString())),
                    NarrativeMechanicCatalogCanonicalJson.Property(
                        "stackingPolicy", NarrativeMechanicCatalogCanonicalJson.String(definition.StackingPolicy.ToString())),
                    NarrativeMechanicCatalogCanonicalJson.Property(
                        "targetId", NarrativeMechanicCatalogCanonicalJson.String(definition.TargetId)))),
            NarrativeMechanicCatalogCanonicalJson.Property(
                "ordinal", NarrativeMechanicCatalogCanonicalJson.Integer(ordinal)),
            NarrativeMechanicCatalogCanonicalJson.Property(
                "valueDecimal", NarrativeMechanicCatalogCanonicalJson.String(
                    CanonicalFloat(binding.value, binding.bindingId))));
    }

    private static NarrativeMechanicCatalogCanonicalJsonObject BuildUnavailableAcquiredTrait(
        string reason)
    {
        return NarrativeMechanicCatalogCanonicalJson.Object(
            NarrativeMechanicCatalogCanonicalJson.Property(
                "eraseDisplayName", NarrativeMechanicCatalogCanonicalJson.String(string.Empty)),
            NarrativeMechanicCatalogCanonicalJson.Property(
                "eraseItemId", NarrativeMechanicCatalogCanonicalJson.String(string.Empty)),
            NarrativeMechanicCatalogCanonicalJson.Property(
                "gates", NarrativeMechanicCatalogCanonicalJson.Array()),
            NarrativeMechanicCatalogCanonicalJson.Property(
                "implementationStatus", NarrativeMechanicCatalogCanonicalJson.String("assets-unavailable")),
            NarrativeMechanicCatalogCanonicalJson.Property(
                "maxActive", NarrativeMechanicCatalogCanonicalJson.Integer(0)),
            NarrativeMechanicCatalogCanonicalJson.Property(
                "modules", NarrativeMechanicCatalogCanonicalJson.Array()),
            NarrativeMechanicCatalogCanonicalJson.Property(
                "unavailableReason", NarrativeMechanicCatalogCanonicalJson.String(
                    NarrativeMechanicCatalogExportException.Require(reason, nameof(reason)))));
    }

    private static readonly string[] AcquiredTraitResponseKeys =
    {
        "combinationId", "displayName", "description", "narrativeReason", "evidenceFactIds"
    };
    private static readonly string[] CharacterSkillResponseKeys =
    {
        "selectionId",
        "positiveModuleIds",
        "drawbackModuleIds",
        "evidenceFactIds",
        "displayName",
        "narrativeFlavor"
    };
    private static List<NarrativeMechanicCatalogCanonicalJsonObject>
        BuildCharacterSkillParityCases(CharacterSkillSystemSettingsSO settings)
    {
        CharacterSkillParityFixture fixture = FindCharacterSkillParityFixture(settings);
        CharacterSkillGenerationService authority = new CharacterSkillGenerationService(
            new FixedCharacterSkillSettingsProvider(settings),
            new UnavailableLocalLlmRuntimeProvider(),
            new FixedUiClock());
        CharacterSkillModuleOfferState offer = fixture.Draft.moduleSelectionOffers[0];
        string validResponse = CharacterSkillResponseJson(
            offer.selectionId, offer.positiveModuleIds[0], offer.evidenceFactIds[0]);
        if (!authority.TryValidateResponse(
                fixture.Draft,
                validResponse,
                out List<CharacterSkillInstance> accepted,
                out _)
            || accepted.Count != 1)
        {
            throw new NarrativeMechanicCatalogUnsupportedSourceException(
                "character-skill-parity-valid-rejected",
                "CharacterSkillGenerationService rejected the catalog's authority-generated valid parity response.");
        }

        const int selectionPrefixLength = 16;
        char replacement = offer.selectionId[selectionPrefixLength] == '0' ? '1' : '0';
        string rejectedSelectionId = offer.selectionId.Substring(0, selectionPrefixLength)
            + replacement + offer.selectionId.Substring(selectionPrefixLength + 1);
        string rejectedResponse = CharacterSkillResponseJson(
            rejectedSelectionId, offer.positiveModuleIds[0], offer.evidenceFactIds[0]);
        if (authority.TryValidateResponse(
                fixture.Draft,
                rejectedResponse,
                out _,
                out _))
        {
            throw new NarrativeMechanicCatalogUnsupportedSourceException(
                "character-skill-parity-invalid-accepted",
                "CharacterSkillGenerationService accepted a forged combination identity.");
        }

        NarrativeMechanicCatalogCanonicalJsonObject request =
            CharacterSkillRequestJson(fixture, settings);
        return new List<NarrativeMechanicCatalogCanonicalJsonObject>
        {
            BuildParityCase(
                LocalLlmRequestProfiles.CharacterSkillModuleSelection.Id,
                "character-skill-reject-presentation-identity",
                "CharacterSkillGenerationService.TryValidateResponse",
                request,
                rejectedResponse,
                accepted: false,
                expectedIssueCode: "PresentationIdentityMismatch",
                responseKeys: CharacterSkillResponseKeys),
            BuildParityCase(
                LocalLlmRequestProfiles.CharacterSkillModuleSelection.Id,
                "character-skill-valid-response",
                "CharacterSkillGenerationService.TryValidateResponse",
                request,
                validResponse,
                accepted: true,
                expectedIssueCode: "None",
                responseKeys: CharacterSkillResponseKeys)
        };
    }

    private static List<NarrativeMechanicCatalogCanonicalJsonObject>
        BuildPresentationContractParityCases(
            string profileId,
            string presentationDomain,
            string targetDisplayName)
    {
        string presentationId = "presentation:" + presentationDomain + ":"
            + new string(presentationDomain[0] is >= 'a' and <= 'f'
                ? presentationDomain[0]
                : 'a', 64);
        NarrativeMechanicCatalogCanonicalJsonObject response =
            NarrativeMechanicCatalogCanonicalJson.Object(
                NarrativeMechanicCatalogCanonicalJson.Property(
                    "displayName", NarrativeMechanicCatalogCanonicalJson.String(targetDisplayName)),
                NarrativeMechanicCatalogCanonicalJson.Property(
                    "narrativeFlavor", NarrativeMechanicCatalogCanonicalJson.String(
                        "실제 기록이 이 결과의 이름과 이야기를 빚었습니다.")),
                NarrativeMechanicCatalogCanonicalJson.Property(
                    "presentationId", NarrativeMechanicCatalogCanonicalJson.String(presentationId)));
        string validResponse = response.ToCanonicalString();
        if (!NarrativeExactKeyContract.TryValidateProfileResponse(
                profileId, validResponse, out _, out string validError))
        {
            throw new NarrativeMechanicCatalogUnsupportedSourceException(
                "presentation-parity-valid-rejected",
                $"{profileId} rejected its exact presentation response: {validError}");
        }
        string rejectedResponse = validResponse.Insert(
            validResponse.Length - 1,
            ",\"selectedIndex\":0");
        if (NarrativeExactKeyContract.TryValidateProfileResponse(
                profileId, rejectedResponse, out _, out _))
        {
            throw new NarrativeMechanicCatalogUnsupportedSourceException(
                "presentation-parity-mechanic-key-accepted",
                $"{profileId} accepted a model-owned mechanical selection field.");
        }
        NarrativeMechanicCatalogCanonicalJsonObject request =
            NarrativeMechanicCatalogCanonicalJson.Object(
                NarrativeMechanicCatalogCanonicalJson.Property(
                    "evidenceFactIds", StringArray(
                        new[] { "public-fact:sha256:" + new string('d', 64) },
                        "presentation evidence", profileId)),
                NarrativeMechanicCatalogCanonicalJson.Property(
                    "mechanicalDescription", NarrativeMechanicCatalogCanonicalJson.String(
                        "C#이 확정한 기계 설명")),
                NarrativeMechanicCatalogCanonicalJson.Property(
                    "presentationId", NarrativeMechanicCatalogCanonicalJson.String(presentationId)),
                NarrativeMechanicCatalogCanonicalJson.Property(
                    "publicFacts", StringArray(
                        new[] { "공개된 실제 기록" }, "presentation public facts", profileId)),
                NarrativeMechanicCatalogCanonicalJson.Property(
                    "style", NarrativeMechanicCatalogCanonicalJson.String("fantasy-martial")),
                NarrativeMechanicCatalogCanonicalJson.Property(
                    "targetDisplayName", NarrativeMechanicCatalogCanonicalJson.String(targetDisplayName)));
        return new List<NarrativeMechanicCatalogCanonicalJsonObject>
        {
            BuildParityCase(
                profileId,
                profileId + "-presentation-reject-mechanical-key",
                "NarrativeExactKeyContract.TryValidateProfileResponse",
                request,
                rejectedResponse,
                accepted: false,
                expectedIssueCode: "ExtraResponseKey",
                responseKeys: CharacterSkillResponseKeys),
            BuildParityCase(
                profileId,
                profileId + "-presentation-valid-exact-response",
                "NarrativeExactKeyContract.TryValidateProfileResponse",
                request,
                validResponse,
                accepted: true,
                expectedIssueCode: "None",
                responseKeys: CharacterSkillResponseKeys)
        };
    }

    private static List<NarrativeMechanicCatalogCanonicalJsonObject>
        BuildAcquiredTraitParityCases(
            CharacterAcquiredTraitSettingsSO settings,
            IReadOnlyList<CharacterAcquiredTraitModuleSO> modules)
    {
        CharacterAcquiredTraitManifestationGateDefinition firstGate = settings.ManifestationGates[0];
        CharacterAcquiredTraitModuleSO firstModule = modules
            .OrderBy(value => value.ModuleId, StringComparer.Ordinal)
            .First();
        CharacterAcquiredTraitRequestPacketDto validPacket = BuildAcquiredTraitPacket(
            settings,
            modules,
            firstGate.MeaningfulRecordMilestone,
            firstModule.DomainAffinities,
            "valid",
            out CharacterNarrativeLedger validLedger);
        RequireAcquiredRequestValidation(
            validPacket,
            validLedger,
            settings,
            modules,
            expectedAccepted: true,
            expectedIssueCode: CharacterAcquiredTraitInferenceIssueCode.None,
            label: "valid request");

        CharacterAcquiredTraitCombinationPacketDto validOption = validPacket.combinationOptions[0];
        string validResponse = AcquiredTraitResponseJson(
            validOption.combinationId,
            validPacket.evidenceFactIds[0],
            includeEvidence: true,
            includeExtraKey: false);
        RequireAcquiredResponseValidation(
            validPacket,
            validResponse,
            validLedger,
            settings,
            modules,
            expectedAccepted: true,
            expectedIssueCode: CharacterAcquiredTraitInferenceIssueCode.None,
            label: "valid exact response");

        List<NarrativeMechanicCatalogCanonicalJsonObject> cases =
            new List<NarrativeMechanicCatalogCanonicalJsonObject>
            {
                BuildParityCase(
                    LocalLlmRequestProfiles.AcquiredTraitLegacyV2.Id,
                    "acquired-trait-valid-request-and-response",
                    "CharacterAcquiredTraitRequestPacketAuthority.TryBuild+CharacterAcquiredTraitResponseAuthority.TryValidate",
                    AcquiredTraitRequestJson(validPacket),
                    validResponse,
                    accepted: true,
                    expectedIssueCode: "None")
            };

        CharacterAcquiredTraitRequestPacketDto invalidIdentity = validPacket.Clone();
        invalidIdentity.combinationOptions[0].combinationId =
            CharacterAcquiredTraitCombinationIdentity.Prefix + "invalid";
        invalidIdentity.candidatePacketHash =
            CharacterAcquiredTraitRequestPacketAuthority.ComputeHash(invalidIdentity);
        RequireAcquiredRequestValidation(
            invalidIdentity,
            validLedger,
            settings,
            modules,
            expectedAccepted: false,
            expectedIssueCode: CharacterAcquiredTraitInferenceIssueCode.InvalidCombination,
            label: "forged combination identity");
        cases.Add(BuildParityCase(
            LocalLlmRequestProfiles.AcquiredTraitLegacyV2.Id,
            "acquired-trait-reject-combination-identity",
            "CharacterAcquiredTraitRequestPacketAuthority.TryValidate",
            AcquiredTraitRequestJson(invalidIdentity),
            string.Empty,
            accepted: false,
            expectedIssueCode: CharacterAcquiredTraitInferenceIssueCode.InvalidCombination.ToString()));

        CharacterAcquiredTraitModuleSO[] overBudgetModules = FindModuleSet(
            modules,
            values => values.Sum(value => value.Cost) > firstGate.Budget
                && !PairHasConflict(values),
            "a conflict-free two- or three-module set above the first acquired-trait gate budget");
        CharacterAcquiredTraitRequestPacketDto budgetPacket = BuildAcquiredTraitPacket(
            settings,
            modules,
            firstGate.MeaningfulRecordMilestone,
            overBudgetModules.SelectMany(value => value.DomainAffinities),
            "budget",
            out CharacterNarrativeLedger budgetLedger);
        budgetPacket.combinationOptions[0] = BuildAcquiredTraitCombination(
            overBudgetModules,
            budgetPacket.eligibleDomains);
        budgetPacket.candidatePacketHash =
            CharacterAcquiredTraitRequestPacketAuthority.ComputeHash(budgetPacket);
        RequireAcquiredRequestValidation(
            budgetPacket,
            budgetLedger,
            settings,
            modules,
            expectedAccepted: false,
            expectedIssueCode: CharacterAcquiredTraitInferenceIssueCode.BudgetExceeded,
            label: "gate budget rejection");
        cases.Add(BuildParityCase(
            LocalLlmRequestProfiles.AcquiredTraitLegacyV2.Id,
            "acquired-trait-reject-gate-budget",
            "CharacterAcquiredTraitRequestPacketAuthority.TryValidate",
            AcquiredTraitRequestJson(budgetPacket),
            string.Empty,
            accepted: false,
            expectedIssueCode: CharacterAcquiredTraitInferenceIssueCode.BudgetExceeded.ToString()));

        CharacterAcquiredTraitManifestationGateDefinition conflictGate = settings.ManifestationGates
            .Single(value => value.MeaningfulRecordMilestone == 20);
        CharacterAcquiredTraitModuleSO[] conflictingModules = FindModulePair(
            modules,
            pair => pair.Sum(value => value.Cost) <= conflictGate.Budget
                && PairHasConflict(pair),
            "a pair sharing an acquired-trait conflict group or effect binding");
        CharacterAcquiredTraitRequestPacketDto conflictPacket = BuildAcquiredTraitPacket(
            settings,
            modules,
            conflictGate.MeaningfulRecordMilestone,
            conflictingModules.SelectMany(value => value.DomainAffinities),
            "conflict",
            out CharacterNarrativeLedger conflictLedger);
        conflictPacket.combinationOptions[0] = BuildAcquiredTraitCombination(
            conflictingModules,
            conflictPacket.eligibleDomains);
        conflictPacket.candidatePacketHash =
            CharacterAcquiredTraitRequestPacketAuthority.ComputeHash(conflictPacket);
        RequireAcquiredRequestValidation(
            conflictPacket,
            conflictLedger,
            settings,
            modules,
            expectedAccepted: false,
            expectedIssueCode: CharacterAcquiredTraitInferenceIssueCode.Conflict,
            label: "conflict rejection");
        cases.Add(BuildParityCase(
            LocalLlmRequestProfiles.AcquiredTraitLegacyV2.Id,
            "acquired-trait-reject-conflict",
            "CharacterAcquiredTraitRequestPacketAuthority.TryValidate",
            AcquiredTraitRequestJson(conflictPacket),
            string.Empty,
            accepted: false,
            expectedIssueCode: CharacterAcquiredTraitInferenceIssueCode.Conflict.ToString()));

        string extraKeyResponse = AcquiredTraitResponseJson(
            validOption.combinationId,
            validPacket.evidenceFactIds[0],
            includeEvidence: true,
            includeExtraKey: true);
        RequireAcquiredResponseValidation(
            validPacket,
            extraKeyResponse,
            validLedger,
            settings,
            modules,
            expectedAccepted: false,
            expectedIssueCode: CharacterAcquiredTraitInferenceIssueCode.ExtraResponseKey,
            label: "extra exact response key");
        cases.Add(BuildParityCase(
            LocalLlmRequestProfiles.AcquiredTraitLegacyV2.Id,
            "acquired-trait-reject-extra-response-key",
            "CharacterAcquiredTraitResponseAuthority.TryValidate",
            AcquiredTraitRequestJson(validPacket),
            extraKeyResponse,
            accepted: false,
            expectedIssueCode: CharacterAcquiredTraitInferenceIssueCode.ExtraResponseKey.ToString()));

        string missingKeyResponse = AcquiredTraitResponseJson(
            validOption.combinationId,
            validPacket.evidenceFactIds[0],
            includeEvidence: false,
            includeExtraKey: false);
        RequireAcquiredResponseValidation(
            validPacket,
            missingKeyResponse,
            validLedger,
            settings,
            modules,
            expectedAccepted: false,
            expectedIssueCode: CharacterAcquiredTraitInferenceIssueCode.MissingResponseKey,
            label: "missing exact response key");
        cases.Add(BuildParityCase(
            LocalLlmRequestProfiles.AcquiredTraitLegacyV2.Id,
            "acquired-trait-reject-missing-response-key",
            "CharacterAcquiredTraitResponseAuthority.TryValidate",
            AcquiredTraitRequestJson(validPacket),
            missingKeyResponse,
            accepted: false,
            expectedIssueCode: CharacterAcquiredTraitInferenceIssueCode.MissingResponseKey.ToString()));
        return cases;
    }

    private static CharacterSkillParityFixture FindCharacterSkillParityFixture(
        CharacterSkillSystemSettingsSO settings)
    {
        NarrativeFormulaStrengthPolicy policy = settings.RequireFormulaPolicy();
        string catalogSha256 = settings.formulaPolicy.RequireCatalogSha256();
        CharacterSkillRarity rarity = (settings.rarityBudgets ?? new List<CharacterRarityBudget>())
            .Where(value => value != null && value.budget > 0)
            .OrderBy(value => value.rarity)
            .Select(value => value.rarity)
            .DefaultIfEmpty(CharacterSkillRarity.Common)
            .First();
        foreach (CharacterSkillModuleRule module in settings.Modules
                     .Where(value => value != null)
                     .OrderBy(value => value.id, StringComparer.Ordinal))
        {
            CharacterSkillKind[] kinds = module.allowedKinds != null && module.allowedKinds.Count > 0
                ? module.allowedKinds.Distinct().OrderBy(value => value).ToArray()
                : (CharacterSkillKind[])Enum.GetValues(typeof(CharacterSkillKind));
            CharacterSkillTrigger[] triggers = module.allowedTriggers != null && module.allowedTriggers.Count > 0
                ? module.allowedTriggers.Distinct().OrderBy(value => value).ToArray()
                : (CharacterSkillTrigger[])Enum.GetValues(typeof(CharacterSkillTrigger));
            CharacterSkillTarget[] targets = module.allowedTargets != null && module.allowedTargets.Count > 0
                ? module.allowedTargets.Distinct().OrderBy(value => value).ToArray()
                : (CharacterSkillTarget[])Enum.GetValues(typeof(CharacterSkillTarget));
            foreach (CharacterSkillKind kind in kinds)
            foreach (CharacterSkillTrigger trigger in triggers)
            foreach (CharacterSkillTarget target in targets)
            {
                if (!CharacterSkillValidation.IsTargetCompatible(module, target)
                    || CharacterSkillValidation.WouldSelfTrigger(module, trigger)
                    || (module is CharacterManagementSkillModuleRule
                        && !CharacterSkillModuleCapabilityRegistry.Require(module)
                            .IsManagementModuleReachable(kind, trigger)))
                {
                    continue;
                }
                NarrativeFormulaCapabilityDescriptor descriptor =
                    settings.RequireFormulaDescriptor(module);
                NarrativeFormulaCapabilityAllocation allocation =
                    new NarrativeFormulaCapabilityAllocation(
                        descriptor,
                        NarrativeFormulaParameterIds.Required.Select(parameterId =>
                            new NarrativeFormulaParameterValue(
                                parameterId,
                                descriptor.RequireRange(parameterId).MinimumUnits)));
                CharacterSkillCandidateRule rule = new CharacterSkillCandidateRule
                {
                    rarity = rarity,
                    trigger = trigger,
                    target = target,
                    ultimateDomain = kind == CharacterSkillKind.Ultimate
                        ? module is CharacterManagementSkillModuleRule
                            ? CharacterUltimateDomain.Management
                            : CharacterUltimateDomain.Offense
                        : CharacterUltimateDomain.None,
                    cooldownTurns = 0,
                    mechanicalPolicySource = CharacterSkillMechanicalPolicySource.AuthoredRule,
                    allowedModuleIds = new List<string> { module.id },
                    allowedVariantIds = new List<string>()
                };
                CharacterSkillFormationRules.Resolve(
                    target,
                    new[] { new CharacterSkillModuleSelection { moduleId = module.id } },
                    out OffenseFormationMask usableFrom,
                    out OffenseFormationMask targetPositions);
                NarrativeFormulaGenerationCostContext generationContext =
                    settings.formulaPolicy.RequireGenerationCostContext(trigger, target);
                int cost = NarrativeFormulaCore.CalculateCost(
                    new[] { allocation }, generationContext);
                rule.budget = cost;
                string evidenceId = "skill-evidence:"
                    + NarrativeInferenceHash.ComputeSha256Utf8("catalog-parity:character-skill");
                NarrativeFormulaCandidate formula = new NarrativeFormulaCandidate(
                    new[] { allocation },
                    cost,
                    descriptor.NarrativeAffinity,
                    1d,
                    new[] { evidenceId });
                CharacterSkillDraft draft = new CharacterSkillDraft
                {
                    kind = kind,
                    requestKey = "catalog-parity:character-skill",
                    rules = new List<CharacterSkillCandidateRule> { rule },
                    formulaVersion = policy.FormulaVersion,
                    formulaCatalogSha256 = catalogSha256,
                    formulaBudget = cost,
                    nextPresentationIndex = 0,
                    presentationState = CharacterSkillPresentationState.PresentationPending
                };
                CharacterSkillRuleIdentity.Ensure(draft);
                string presentationId = "presentation:skill:"
                    + NarrativeInferenceHash.ComputeSha256Utf8(
                        draft.requestKey + "\n" + descriptor.CapabilityId + "\n" + catalogSha256)
                        .Substring("sha256:".Length);
                CharacterSkillInstance skill = new CharacterSkillInstance
                {
                    id = draft.requestKey + ":formula:" + descriptor.CapabilityId,
                    ruleId = rule.ruleId,
                    kind = kind,
                    rarity = rarity,
                    trigger = trigger,
                    target = target,
                    ultimateDomain = rule.ultimateDomain,
                    cooldownTurns = 0,
                    usableFrom = usableFrom,
                    targetPositions = targetPositions,
                    modules = new List<CharacterSkillModuleSelection>
                    {
                        new CharacterSkillModuleSelection
                            { moduleId = module.id, variantId = string.Empty }
                    },
                    requestKey = draft.requestKey,
                    formulaVersion = policy.FormulaVersion,
                    formulaCatalogSha256 = catalogSha256,
                    calculatedCost = cost,
                    positiveCost = cost,
                    drawbackCredit = 0,
                    drawbackId = string.Empty,
                    narrativeBudget = cost,
                    evidenceIds = new List<string> { evidenceId },
                    formulaCapabilities = new List<CharacterSkillFormulaCapabilityEnvelope>
                    {
                        new CharacterSkillFormulaCapabilityEnvelope
                        {
                            capabilityId = descriptor.CapabilityId,
                            formatterId = descriptor.FormatterId,
                            applicatorId = descriptor.ApplicatorId,
                            parameters = allocation.Parameters.Select(value =>
                                new CharacterSkillFormulaParameter
                                {
                                    parameterId = value.ParameterId,
                                    units = value.Units
                                }).ToList()
                        }
                    },
                    presentationId = presentationId,
                    mechanicalDescription = CharacterSkillFormulaPresentation
                        .FormatMechanicalDescription(new[] { module }, rule, formula)
                };
                string selectionId = "selection:skill:"
                    + NarrativeInferenceHash.ComputeSha256Utf8(
                        draft.requestKey + "\n" + rule.ruleId + "\n" + catalogSha256)
                        .Substring("sha256:".Length);
                draft.moduleSelectionOffers = new List<CharacterSkillModuleOfferState>
                {
                    new CharacterSkillModuleOfferState
                    {
                        selectionId = selectionId,
                        ruleId = rule.ruleId,
                        positiveModuleIds = new List<string> { module.id },
                        drawbackModuleIds = new List<string>(),
                        evidenceFactIds = new List<string> { evidenceId },
                        maximumPositiveModules = 1,
                        maximumDrawbackModules = 0
                    }
                };
                draft.frozenMechanics = new List<CharacterSkillInstance>();
                return new CharacterSkillParityFixture(draft, skill);
            }
        }

        throw new NarrativeMechanicCatalogUnsupportedSourceException(
            "character-skill-parity-fixture-missing",
            "Current character-skill formula catalog contains no runtime-reachable parity capability.");
    }

    private static CharacterAcquiredTraitRequestPacketDto BuildAcquiredTraitPacket(
        CharacterAcquiredTraitSettingsSO settings,
        IReadOnlyList<CharacterAcquiredTraitModuleSO> modules,
        int milestone,
        IEnumerable<CharacterNarrativeDomain> domains,
        string caseSuffix,
        out CharacterNarrativeLedger ledger)
    {
        ledger = new CharacterNarrativeLedger();
        CharacterNarrativeDomain[] selectedDomains = (domains ?? Array.Empty<CharacterNarrativeDomain>())
            .Distinct()
            .OrderBy(value => value)
            .ToArray();
        if (selectedDomains.Length == 0)
        {
            throw new NarrativeMechanicCatalogUnsupportedSourceException(
                "acquired-trait-parity-domain-missing",
                $"Acquired-trait parity case '{caseSuffix}' has no authored domain.");
        }
        for (int index = 0; index < milestone; index++)
        {
            CharacterNarrativeDomain domain = selectedDomains[index % selectedDomains.Length];
            ledger.Record(
                domain,
                $"catalog-parity:{caseSuffix}:{index:D2}",
                "catalog-parity-actor",
                "completed",
                day: 1);
        }
        string[] evidence = ledger.Facts
            .Select(value => value.factId)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        CharacterAcquiredTraitSubmissionCommand command =
            new CharacterAcquiredTraitSubmissionCommand(
                "catalog-parity-actor",
                "catalog-parity-request-" + caseSuffix,
                "catalog-parity-key-" + caseSuffix,
                milestone,
                evidence,
                expectedRevision: 0,
                timestamp: default);
        if (!CharacterAcquiredTraitRequestPacketAuthority.TryBuild(
                command,
                ledger,
                new CharacterAcquiredTraitAggregateState(),
                settings,
                modules,
                out CharacterAcquiredTraitRequestPacketDto packet,
                out CharacterAcquiredTraitInferenceIssueCode issueCode,
                out string error))
        {
            throw new NarrativeMechanicCatalogUnsupportedSourceException(
                "acquired-trait-parity-request-build-failed",
                $"Could not build '{caseSuffix}' through CharacterAcquiredTraitRequestPacketAuthority: {issueCode} {error}");
        }
        return packet;
    }

    private static CharacterAcquiredTraitModuleSO[] FindModulePair(
        IReadOnlyList<CharacterAcquiredTraitModuleSO> modules,
        Func<CharacterAcquiredTraitModuleSO[], bool> predicate,
        string requiredDescription)
    {
        CharacterAcquiredTraitModuleSO[] ordered = modules
            .OrderBy(value => value.ModuleId, StringComparer.Ordinal)
            .ToArray();
        for (int first = 0; first < ordered.Length; first++)
        {
            for (int second = first + 1; second < ordered.Length; second++)
            {
                CharacterAcquiredTraitModuleSO[] pair = { ordered[first], ordered[second] };
                if (predicate(pair))
                {
                    return pair;
                }
            }
        }
        throw new NarrativeMechanicCatalogUnsupportedSourceException(
            "acquired-trait-parity-pair-missing",
            $"Acquired-trait parity requires {requiredDescription}, but the indexed modules do not provide one.");
    }

    private static CharacterAcquiredTraitModuleSO[] FindModuleSet(
        IReadOnlyList<CharacterAcquiredTraitModuleSO> modules,
        Func<CharacterAcquiredTraitModuleSO[], bool> predicate,
        string requiredDescription)
    {
        CharacterAcquiredTraitModuleSO[] ordered = modules
            .OrderBy(value => value.ModuleId, StringComparer.Ordinal)
            .ToArray();
        for (int first = 0; first < ordered.Length; first++)
        {
            for (int second = first + 1; second < ordered.Length; second++)
            {
                CharacterAcquiredTraitModuleSO[] pair = { ordered[first], ordered[second] };
                if (predicate(pair))
                    return pair;
                for (int third = second + 1; third < ordered.Length; third++)
                {
                    CharacterAcquiredTraitModuleSO[] triple =
                        { ordered[first], ordered[second], ordered[third] };
                    if (predicate(triple))
                        return triple;
                }
            }
        }
        throw new NarrativeMechanicCatalogUnsupportedSourceException(
            "acquired-trait-parity-module-set-missing",
            $"Acquired-trait parity requires {requiredDescription}, but the indexed modules do not provide one.");
    }

    private static bool PairHasConflict(
        IReadOnlyList<CharacterAcquiredTraitModuleSO> pair) =>
        pair.SelectMany(value => value.ConflictGroups)
            .Select(value => value.Trim())
            .GroupBy(value => value, StringComparer.Ordinal)
            .Any(group => group.Count() > 1)
        || pair.SelectMany(value => value.Effects)
            .Select(value => value.bindingId.Trim())
            .GroupBy(value => value, StringComparer.Ordinal)
            .Any(group => group.Count() > 1);

    private static CharacterAcquiredTraitCombinationPacketDto BuildAcquiredTraitCombination(
        IEnumerable<CharacterAcquiredTraitModuleSO> source,
        IEnumerable<string> eligibleDomains)
    {
        CharacterAcquiredTraitModuleSO[] modules = source
            .OrderBy(value => value.ModuleId, StringComparer.Ordinal)
            .ToArray();
        HashSet<string> domains = new HashSet<string>(
            eligibleDomains ?? Array.Empty<string>(),
            StringComparer.Ordinal);
        string[] moduleIds = modules.Select(value => value.ModuleId).ToArray();
        return new CharacterAcquiredTraitCombinationPacketDto
        {
            combinationId = CharacterAcquiredTraitCombinationIdentity.Build(moduleIds),
            moduleIds = moduleIds.ToList(),
            totalCost = modules.Sum(value => value.Cost),
            domainAffinities = modules
                .SelectMany(value => value.DomainAffinities)
                .Select(value => value.ToString())
                .Where(domains.Contains)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToList(),
            conflictGroups = modules
                .SelectMany(value => value.ConflictGroups)
                .Select(value => value.Trim())
                .Distinct(StringComparer.Ordinal)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToList()
        };
    }

    private static void RequireAcquiredRequestValidation(
        CharacterAcquiredTraitRequestPacketDto packet,
        CharacterNarrativeLedger ledger,
        CharacterAcquiredTraitSettingsSO settings,
        IReadOnlyList<CharacterAcquiredTraitModuleSO> modules,
        bool expectedAccepted,
        CharacterAcquiredTraitInferenceIssueCode expectedIssueCode,
        string label)
    {
        bool accepted = CharacterAcquiredTraitRequestPacketAuthority.TryValidate(
            packet,
            ledger,
            new CharacterAcquiredTraitAggregateState(),
            settings,
            modules,
            out CharacterAcquiredTraitInferenceIssueCode issueCode,
            out string error);
        if (accepted != expectedAccepted || issueCode != expectedIssueCode)
        {
            throw new NarrativeMechanicCatalogUnsupportedSourceException(
                "acquired-trait-parity-request-unexpected",
                $"Acquired-trait {label} produced {accepted}/{issueCode}, expected {expectedAccepted}/{expectedIssueCode}: {error}");
        }
    }

    private static void RequireAcquiredResponseValidation(
        CharacterAcquiredTraitRequestPacketDto packet,
        string responseJson,
        CharacterNarrativeLedger ledger,
        CharacterAcquiredTraitSettingsSO settings,
        IReadOnlyList<CharacterAcquiredTraitModuleSO> modules,
        bool expectedAccepted,
        CharacterAcquiredTraitInferenceIssueCode expectedIssueCode,
        string label)
    {
        bool accepted = CharacterAcquiredTraitResponseAuthority.TryValidate(
            packet,
            responseJson,
            ledger,
            new CharacterAcquiredTraitAggregateState(),
            settings,
            modules,
            out _,
            out _,
            out CharacterAcquiredTraitInferenceIssueCode issueCode,
            out string error);
        if (accepted != expectedAccepted || issueCode != expectedIssueCode)
        {
            throw new NarrativeMechanicCatalogUnsupportedSourceException(
                "acquired-trait-parity-response-unexpected",
                $"Acquired-trait {label} produced {accepted}/{issueCode}, expected {expectedAccepted}/{expectedIssueCode}: {error}");
        }
    }

    private static NarrativeMechanicCatalogCanonicalJsonObject BuildParityCase(
        string profileId,
        string caseId,
        string authority,
        NarrativeMechanicCatalogCanonicalJsonObject request,
        string responseJson,
        bool accepted,
        string expectedIssueCode,
        IEnumerable<string> responseKeys = null)
    {
        return NarrativeMechanicCatalogCanonicalJson.Object(
            NarrativeMechanicCatalogCanonicalJson.Property(
                "authority", NarrativeMechanicCatalogCanonicalJson.String(authority)),
            NarrativeMechanicCatalogCanonicalJson.Property(
                "caseId", NarrativeMechanicCatalogCanonicalJson.String(caseId)),
            NarrativeMechanicCatalogCanonicalJson.Property(
                "expected", NarrativeMechanicCatalogCanonicalJson.Object(
                    NarrativeMechanicCatalogCanonicalJson.Property(
                        "accepted", NarrativeMechanicCatalogCanonicalJson.Boolean(accepted)),
                    NarrativeMechanicCatalogCanonicalJson.Property(
                        "issueCode", NarrativeMechanicCatalogCanonicalJson.String(expectedIssueCode)),
                    NarrativeMechanicCatalogCanonicalJson.Property(
                        "responseKeys", StringArray(
                            responseKeys ?? AcquiredTraitResponseKeys,
                            "response keys",
                            caseId)))),
            NarrativeMechanicCatalogCanonicalJson.Property(
                "profileId", NarrativeMechanicCatalogCanonicalJson.String(
                    NarrativeMechanicCatalogExportException.Require(
                        profileId,
                        nameof(profileId)))),
            NarrativeMechanicCatalogCanonicalJson.Property("request", request),
            NarrativeMechanicCatalogCanonicalJson.Property(
                "responseJson", NarrativeMechanicCatalogCanonicalJson.String(responseJson)));
    }

    private static NarrativeMechanicCatalogCanonicalJsonObject CharacterSkillRequestJson(
        CharacterSkillParityFixture fixture,
        CharacterSkillSystemSettingsSO settings)
    {
        if (fixture == null || settings == null)
        {
            throw new ArgumentNullException(fixture == null ? nameof(fixture) : nameof(settings));
        }
        string authorityPacket = CharacterSkillPromptBuilder.BuildModuleSelectionPacket(
            fixture.Draft, settings);
        if (string.IsNullOrWhiteSpace(authorityPacket))
        {
            throw new NarrativeMechanicCatalogUnsupportedSourceException(
                "character-skill-parity-packet-empty",
                "CharacterSkillPromptBuilder emitted an empty presentation packet for parity.");
        }

        CharacterSkillModuleOfferState offer = fixture.Draft.moduleSelectionOffers[0];
        return NarrativeMechanicCatalogCanonicalJson.Object(
            NarrativeMechanicCatalogCanonicalJson.Property(
                "evidenceIds", StringArray(
                    offer.evidenceFactIds,
                    "formula evidence IDs",
                    fixture.Draft.requestKey)),
            NarrativeMechanicCatalogCanonicalJson.Property(
                "formulaCatalogSha256", NarrativeMechanicCatalogCanonicalJson.String(
                    fixture.Draft.formulaCatalogSha256)),
            NarrativeMechanicCatalogCanonicalJson.Property(
                "formulaVersion", NarrativeMechanicCatalogCanonicalJson.Integer(
                    fixture.Draft.formulaVersion)),
            NarrativeMechanicCatalogCanonicalJson.Property(
                "kind", NarrativeMechanicCatalogCanonicalJson.String(fixture.Draft.kind.ToString())),
            NarrativeMechanicCatalogCanonicalJson.Property(
                "positiveModuleIds", StringArray(
                    offer.positiveModuleIds, "positive module IDs", fixture.Draft.requestKey)),
            NarrativeMechanicCatalogCanonicalJson.Property(
                "selectionId", NarrativeMechanicCatalogCanonicalJson.String(offer.selectionId)),
            NarrativeMechanicCatalogCanonicalJson.Property(
                "presentationPacket", NarrativeMechanicCatalogCanonicalJson.String(authorityPacket)),
            NarrativeMechanicCatalogCanonicalJson.Property(
                "requestKey", NarrativeMechanicCatalogCanonicalJson.String(fixture.Draft.requestKey)));
    }

    private static NarrativeMechanicCatalogCanonicalJsonValue CharacterSkillRuleRequestJson(
        CharacterSkillCandidateRule rule,
        CharacterSkillSystemSettingsSO settings,
        CharacterSkillKind kind)
    {
        List<NarrativeMechanicCatalogCanonicalJsonValue> combinations =
            CharacterSkillCombinationCatalog.Build(rule, settings, kind)
                .OrderBy(value => value.Id, StringComparer.Ordinal)
                .Select(value => CharacterSkillCombinationRequestJson(
                    value,
                    rule,
                    kind,
                    settings))
                .ToList();
        if (combinations.Count == 0)
        {
            throw new NarrativeMechanicCatalogUnsupportedSourceException(
                "character-skill-parity-combination-empty",
                $"Character skill parity rule '{rule.ruleId}' has no legal combination.");
        }
        return NarrativeMechanicCatalogCanonicalJson.Object(
            NarrativeMechanicCatalogCanonicalJson.Property(
                "allowedModuleIds", StringArray(rule.allowedModuleIds, "allowed module IDs", rule.ruleId)),
            NarrativeMechanicCatalogCanonicalJson.Property(
                "allowedVariantIds", StringArray(rule.allowedVariantIds, "allowed variant IDs", rule.ruleId)),
            NarrativeMechanicCatalogCanonicalJson.Property(
                "budget", NarrativeMechanicCatalogCanonicalJson.Integer(rule.budget)),
            NarrativeMechanicCatalogCanonicalJson.Property(
                "combinationOptions", new NarrativeMechanicCatalogCanonicalJsonArray(combinations)),
            NarrativeMechanicCatalogCanonicalJson.Property(
                "cooldownTurns", NarrativeMechanicCatalogCanonicalJson.Integer(rule.cooldownTurns)),
            NarrativeMechanicCatalogCanonicalJson.Property(
                "rarity", NarrativeMechanicCatalogCanonicalJson.String(rule.rarity.ToString())),
            NarrativeMechanicCatalogCanonicalJson.Property(
                "ruleId", NarrativeMechanicCatalogCanonicalJson.String(rule.ruleId)),
            NarrativeMechanicCatalogCanonicalJson.Property(
                "target", NarrativeMechanicCatalogCanonicalJson.String(rule.target.ToString())),
            NarrativeMechanicCatalogCanonicalJson.Property(
                "targetPositions", NarrativeMechanicCatalogCanonicalJson.String(
                    CharacterSkillFormationRules.Format(rule.targetPositions))),
            NarrativeMechanicCatalogCanonicalJson.Property(
                "trigger", NarrativeMechanicCatalogCanonicalJson.String(rule.trigger.ToString())),
            NarrativeMechanicCatalogCanonicalJson.Property(
                "ultimateDomain", NarrativeMechanicCatalogCanonicalJson.String(rule.ultimateDomain.ToString())),
            NarrativeMechanicCatalogCanonicalJson.Property(
                "usableFrom", NarrativeMechanicCatalogCanonicalJson.String(
                    CharacterSkillFormationRules.Format(rule.usableFrom))));
    }

    private static NarrativeMechanicCatalogCanonicalJsonValue CharacterSkillCombinationRequestJson(
        CharacterSkillAllowedCombination combination,
        CharacterSkillCandidateRule rule,
        CharacterSkillKind kind,
        CharacterSkillSystemSettingsSO settings)
    {
        List<NarrativeMechanicCatalogCanonicalJsonValue> modules = (combination.Modules
                ?? new List<CharacterSkillModuleSelection>())
            .Where(value => value != null)
            .Select(value => (NarrativeMechanicCatalogCanonicalJsonValue)
                NarrativeMechanicCatalogCanonicalJson.Object(
                    NarrativeMechanicCatalogCanonicalJson.Property(
                        "moduleId", NarrativeMechanicCatalogCanonicalJson.String(value.moduleId)),
                    NarrativeMechanicCatalogCanonicalJson.Property(
                        "variantId", NarrativeMechanicCatalogCanonicalJson.String(value.variantId))))
            .ToList();
        return NarrativeMechanicCatalogCanonicalJson.Object(
            NarrativeMechanicCatalogCanonicalJson.Property(
                "combinationId", NarrativeMechanicCatalogCanonicalJson.String(combination.Id)),
            NarrativeMechanicCatalogCanonicalJson.Property(
                "cost", NarrativeMechanicCatalogCanonicalJson.Integer(combination.Cost)),
            NarrativeMechanicCatalogCanonicalJson.Property(
                "mechanicalIdentity", NarrativeMechanicCatalogCanonicalJson.String(
                    combination.MechanicalIdentity ?? string.Empty)),
            NarrativeMechanicCatalogCanonicalJson.Property(
                "modules", new NarrativeMechanicCatalogCanonicalJsonArray(modules)),
            NarrativeMechanicCatalogCanonicalJson.Property(
                "semantics", CharacterSkillSemanticsJson(
                    CharacterSkillCombinationSemanticsFactory.Create(
                        combination,
                        rule,
                        kind,
                        settings))),
            NarrativeMechanicCatalogCanonicalJson.Property(
                "signature", NarrativeMechanicCatalogCanonicalJson.String(combination.Signature)));
    }

    private static NarrativeMechanicCatalogCanonicalJsonValue CharacterSkillSemanticsJson(
        CharacterSkillCombinationSemanticsDto semantics)
    {
        NarrativeMechanicCatalogCanonicalJsonObject result =
            NarrativeMechanicCatalogCanonicalJson.Object(
                NarrativeMechanicCatalogCanonicalJson.Property(
                    "combinationId",
                    NarrativeMechanicCatalogCanonicalJson.String(semantics.combinationId)),
                NarrativeMechanicCatalogCanonicalJson.Property(
                    "descriptionKo",
                    NarrativeMechanicCatalogCanonicalJson.String(semantics.descriptionKo)),
                NarrativeMechanicCatalogCanonicalJson.Property(
                    "modules",
                    new NarrativeMechanicCatalogCanonicalJsonArray(semantics.modules.Select(module =>
                        (NarrativeMechanicCatalogCanonicalJsonValue)
                        NarrativeMechanicCatalogCanonicalJson.Object(
                            NarrativeMechanicCatalogCanonicalJson.Property(
                                "descriptionKo",
                                NarrativeMechanicCatalogCanonicalJson.String(module.descriptionKo)),
                            NarrativeMechanicCatalogCanonicalJson.Property(
                                "effectKind",
                                NarrativeMechanicCatalogCanonicalJson.String(module.effectKind)),
                            NarrativeMechanicCatalogCanonicalJson.Property(
                                "executionScope",
                                NarrativeMechanicCatalogCanonicalJson.String(module.executionScope)),
                            NarrativeMechanicCatalogCanonicalJson.Property(
                                "moduleId",
                                NarrativeMechanicCatalogCanonicalJson.String(module.moduleId)),
                            NarrativeMechanicCatalogCanonicalJson.Property(
                                "terms",
                                new NarrativeMechanicCatalogCanonicalJsonArray(module.terms.Select(
                                    value => (NarrativeMechanicCatalogCanonicalJsonValue)
                                        NarrativeMechanicCatalogCanonicalJson.String(value)))),
                            NarrativeMechanicCatalogCanonicalJson.Property(
                                "variantId",
                                NarrativeMechanicCatalogCanonicalJson.String(module.variantId)))))),
                NarrativeMechanicCatalogCanonicalJson.Property(
                    "schemaVersion",
                    NarrativeMechanicCatalogCanonicalJson.Integer(semantics.schemaVersion)));
        string authority = CharacterSkillCombinationSemanticsFactory
            .SerializeCanonical(semantics);
        if (!string.Equals(result.ToCanonicalString(), authority, StringComparison.Ordinal))
        {
            throw new NarrativeMechanicCatalogUnsupportedSourceException(
                "character-skill-catalog-semantics-serialization-divergence",
                "Catalog canonical JSON diverged from the production CharacterSkill semantics serializer.");
        }
        return result;
    }

    private static NarrativeMechanicCatalogCanonicalJsonValue
        CharacterSkillSemanticsCatalogEntryJson(CharacterSkillSemanticsCatalogEntryDto entry)
    {
        if (entry == null || entry.semantics == null)
        {
            throw new NarrativeMechanicCatalogUnsupportedSourceException(
                "character-skill-catalog-semantics-entry-null",
                "CharacterSkill semantics coverage cannot contain a null entry or projection.");
        }
        return NarrativeMechanicCatalogCanonicalJson.Object(
            NarrativeMechanicCatalogCanonicalJson.Property(
                "applicability",
                NarrativeMechanicCatalogCanonicalJson.String(entry.applicability)),
            NarrativeMechanicCatalogCanonicalJson.Property(
                "contextId", NarrativeMechanicCatalogCanonicalJson.String(entry.contextId)),
            NarrativeMechanicCatalogCanonicalJson.Property(
                "kind", NarrativeMechanicCatalogCanonicalJson.String(entry.kind)),
            NarrativeMechanicCatalogCanonicalJson.Property(
                "moduleId", NarrativeMechanicCatalogCanonicalJson.String(entry.moduleId)),
            NarrativeMechanicCatalogCanonicalJson.Property(
                "semantics", CharacterSkillSemanticsJson(entry.semantics)),
            NarrativeMechanicCatalogCanonicalJson.Property(
                "target", NarrativeMechanicCatalogCanonicalJson.String(entry.target)),
            NarrativeMechanicCatalogCanonicalJson.Property(
                "trigger", NarrativeMechanicCatalogCanonicalJson.String(entry.trigger)),
            NarrativeMechanicCatalogCanonicalJson.Property(
                "ultimateDomain",
                NarrativeMechanicCatalogCanonicalJson.String(entry.ultimateDomain)),
            NarrativeMechanicCatalogCanonicalJson.Property(
                "variantId", NarrativeMechanicCatalogCanonicalJson.String(entry.variantId)));
    }

    private static NarrativeMechanicCatalogCanonicalJsonObject AcquiredTraitRequestJson(
        CharacterAcquiredTraitRequestPacketDto packet)
    {
        return NarrativeMechanicCatalogCanonicalJson.Object(
            NarrativeMechanicCatalogCanonicalJson.Property(
                "budget", NarrativeMechanicCatalogCanonicalJson.Integer(packet.budget)),
            NarrativeMechanicCatalogCanonicalJson.Property(
                "candidatePacketHash", NarrativeMechanicCatalogCanonicalJson.String(packet.candidatePacketHash)),
            NarrativeMechanicCatalogCanonicalJson.Property(
                "combinationOptions", new NarrativeMechanicCatalogCanonicalJsonArray(
                    packet.combinationOptions.Select(AcquiredTraitCombinationJson))),
            NarrativeMechanicCatalogCanonicalJson.Property(
                "eligibleDomains", StringArray(packet.eligibleDomains, "eligible domains", packet.requestKey)),
            NarrativeMechanicCatalogCanonicalJson.Property(
                "evidenceFactIds", StringArray(packet.evidenceFactIds, "evidence fact IDs", packet.requestKey)),
            NarrativeMechanicCatalogCanonicalJson.Property(
                "manifestationMilestone", NarrativeMechanicCatalogCanonicalJson.Integer(packet.manifestationMilestone)),
            NarrativeMechanicCatalogCanonicalJson.Property(
                "maximumActiveTraits", NarrativeMechanicCatalogCanonicalJson.Integer(packet.maximumActiveTraits)),
            NarrativeMechanicCatalogCanonicalJson.Property(
                "modules", new NarrativeMechanicCatalogCanonicalJsonArray(
                    packet.modules.Select(AcquiredTraitModulePacketJson))),
            NarrativeMechanicCatalogCanonicalJson.Property(
                "rarity", NarrativeMechanicCatalogCanonicalJson.String(packet.rarity)),
            NarrativeMechanicCatalogCanonicalJson.Property(
                "requestId", NarrativeMechanicCatalogCanonicalJson.String(packet.requestId)),
            NarrativeMechanicCatalogCanonicalJson.Property(
                "requestKey", NarrativeMechanicCatalogCanonicalJson.String(packet.requestKey)),
            NarrativeMechanicCatalogCanonicalJson.Property(
                "settingsId", NarrativeMechanicCatalogCanonicalJson.String(packet.settingsId)),
            NarrativeMechanicCatalogCanonicalJson.Property(
                "targetPersistentId", NarrativeMechanicCatalogCanonicalJson.String(packet.targetPersistentId)));
    }

    private static NarrativeMechanicCatalogCanonicalJsonValue AcquiredTraitModulePacketJson(
        CharacterAcquiredTraitModulePacketDto module) =>
        NarrativeMechanicCatalogCanonicalJson.Object(
            NarrativeMechanicCatalogCanonicalJson.Property(
                "conflictGroups", StringArray(module.conflictGroups, "packet conflict groups", module.moduleId)),
            NarrativeMechanicCatalogCanonicalJson.Property(
                "cost", NarrativeMechanicCatalogCanonicalJson.Integer(module.cost)),
            NarrativeMechanicCatalogCanonicalJson.Property(
                "description", NarrativeMechanicCatalogCanonicalJson.String(module.description)),
            NarrativeMechanicCatalogCanonicalJson.Property(
                "displayName", NarrativeMechanicCatalogCanonicalJson.String(module.displayName)),
            NarrativeMechanicCatalogCanonicalJson.Property(
                "domainAffinities", StringArray(module.domainAffinities, "packet domains", module.moduleId)),
            NarrativeMechanicCatalogCanonicalJson.Property(
                "moduleId", NarrativeMechanicCatalogCanonicalJson.String(module.moduleId)));

    private static NarrativeMechanicCatalogCanonicalJsonValue AcquiredTraitCombinationJson(
        CharacterAcquiredTraitCombinationPacketDto option) =>
        NarrativeMechanicCatalogCanonicalJson.Object(
            NarrativeMechanicCatalogCanonicalJson.Property(
                "combinationId", NarrativeMechanicCatalogCanonicalJson.String(option.combinationId)),
            NarrativeMechanicCatalogCanonicalJson.Property(
                "conflictGroups", StringArray(option.conflictGroups, "packet conflict groups", option.combinationId)),
            NarrativeMechanicCatalogCanonicalJson.Property(
                "domainAffinities", StringArray(option.domainAffinities, "packet domains", option.combinationId)),
            NarrativeMechanicCatalogCanonicalJson.Property(
                "moduleIds", StringArray(option.moduleIds, "packet module IDs", option.combinationId)),
            NarrativeMechanicCatalogCanonicalJson.Property(
                "totalCost", NarrativeMechanicCatalogCanonicalJson.Integer(option.totalCost)));

    private static string CharacterSkillResponseJson(
        string selectionId,
        string positiveModuleId,
        string evidenceFactId) =>
        NarrativeMechanicCatalogCanonicalJson.Object(
            NarrativeMechanicCatalogCanonicalJson.Property(
                "drawbackModuleIds", NarrativeMechanicCatalogCanonicalJson.Array()),
            NarrativeMechanicCatalogCanonicalJson.Property(
                "displayName", NarrativeMechanicCatalogCanonicalJson.String("검증의 서약")),
            NarrativeMechanicCatalogCanonicalJson.Property(
                "evidenceFactIds", NarrativeMechanicCatalogCanonicalJson.Array(
                    NarrativeMechanicCatalogCanonicalJson.String(evidenceFactId))),
            NarrativeMechanicCatalogCanonicalJson.Property(
                "narrativeFlavor", NarrativeMechanicCatalogCanonicalJson.String("기록을 품은 서약이 새로운 힘으로 피어났습니다.")),
            NarrativeMechanicCatalogCanonicalJson.Property(
                "positiveModuleIds", NarrativeMechanicCatalogCanonicalJson.Array(
                    NarrativeMechanicCatalogCanonicalJson.String(positiveModuleId))),
            NarrativeMechanicCatalogCanonicalJson.Property(
                "selectionId", NarrativeMechanicCatalogCanonicalJson.String(selectionId))).ToCanonicalString();

    private static string AcquiredTraitResponseJson(
        string combinationId,
        string evidenceFactId,
        bool includeEvidence,
        bool includeExtraKey)
    {
        List<KeyValuePair<string, NarrativeMechanicCatalogCanonicalJsonValue>> properties =
            new List<KeyValuePair<string, NarrativeMechanicCatalogCanonicalJsonValue>>
            {
                NarrativeMechanicCatalogCanonicalJson.Property(
                    "combinationId", NarrativeMechanicCatalogCanonicalJson.String(combinationId)),
                NarrativeMechanicCatalogCanonicalJson.Property(
                    "description", NarrativeMechanicCatalogCanonicalJson.String("실제 기록이 남긴 특성입니다.")),
                NarrativeMechanicCatalogCanonicalJson.Property(
                    "displayName", NarrativeMechanicCatalogCanonicalJson.String("기록의 결실")),
                NarrativeMechanicCatalogCanonicalJson.Property(
                    "narrativeReason", NarrativeMechanicCatalogCanonicalJson.String("권위 검증용 고정 문장입니다."))
            };
        if (includeEvidence)
        {
            properties.Add(NarrativeMechanicCatalogCanonicalJson.Property(
                "evidenceFactIds", StringArray(new[] { evidenceFactId }, "response evidence", combinationId)));
        }
        if (includeExtraKey)
        {
            properties.Add(NarrativeMechanicCatalogCanonicalJson.Property(
                "mechanicalValue", NarrativeMechanicCatalogCanonicalJson.Integer(1)));
        }
        return new NarrativeMechanicCatalogCanonicalJsonObject(properties)
            .ToCanonicalString();
    }

    private static void RequireNoValidationErrors(
        IEnumerable<string> errors,
        string code,
        string sourceId)
    {
        string[] materialized = (errors ?? Array.Empty<string>())
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .ToArray();
        if (materialized.Length > 0)
        {
            throw new NarrativeMechanicCatalogUnsupportedSourceException(
                code,
                $"'{sourceId}' is invalid: {string.Join(" | ", materialized)}");
        }
    }

    private static string CanonicalFloat(float value, string bindingId)
    {
        if (float.IsNaN(value) || float.IsInfinity(value))
        {
            throw new NarrativeMechanicCatalogUnsupportedSourceException(
                "acquired-trait-effect-value-non-finite",
                $"Effect binding '{bindingId}' has a non-finite value.");
        }
        // The canonical catalog intentionally carries authored floating-point
        // values as invariant round-trip text: its JSON subset is shared with
        // Python and currently has only integral number nodes.
        return value.ToString("R", CultureInfo.InvariantCulture);
    }

    private static NarrativeMechanicCatalogCanonicalJsonArray IntegerArray(
        IEnumerable<int> values)
    {
        if (values == null)
        {
            throw new NarrativeMechanicCatalogUnsupportedSourceException(
                "integer-array-missing",
                "A required integer array is missing.");
        }
        return new NarrativeMechanicCatalogCanonicalJsonArray(values.Select(value =>
            (NarrativeMechanicCatalogCanonicalJsonValue)NarrativeMechanicCatalogCanonicalJson.Integer(value)));
    }

    private static void AddAssetObjectAndMeta(
        ICollection<string> relevantPaths,
        UnityEngine.Object asset,
        string label)
    {
        if (asset == null)
        {
            throw new NarrativeMechanicCatalogUnsupportedSourceException(
                "catalog-asset-reference-missing",
                $"{label} is missing.");
        }
        string assetPath = AssetDatabase.GetAssetPath(asset);
        if (string.IsNullOrWhiteSpace(assetPath))
        {
            throw new NarrativeMechanicCatalogUnsupportedSourceException(
                "catalog-asset-path-missing",
                $"{label} '{asset.name}' is not a persisted Unity asset.");
        }
        AddAssetAndMeta(relevantPaths, assetPath);
    }

    private sealed class CharacterSkillParityFixture
    {
        public CharacterSkillParityFixture(
            CharacterSkillDraft draft,
            CharacterSkillInstance skill)
        {
            Draft = draft;
            Skill = skill;
        }

        public CharacterSkillDraft Draft { get; }
        public CharacterSkillInstance Skill { get; }
    }

    private sealed class FixedCharacterSkillSettingsProvider
        : ICharacterSkillSystemSettingsProvider
    {
        public FixedCharacterSkillSettingsProvider(CharacterSkillSystemSettingsSO settings)
        {
            Settings = settings ?? throw new ArgumentNullException(nameof(settings));
        }

        public CharacterSkillSystemSettingsSO Settings { get; }
    }

    private sealed class UnavailableLocalLlmRuntimeProvider : ILocalLlmRuntimeProvider
    {
        public bool TryGetRuntime(out ILocalLlmRuntime runtime)
        {
            runtime = null;
            return false;
        }

        public ILocalLlmRuntime GetRequiredRuntime() => throw new InvalidOperationException(
            "Catalog packet parity validates deterministic responses and never invokes a local LLM runtime.");
    }

    private sealed class FixedUiClock : IUiClock
    {
        public float DeltaTime => 0f;
        public float Time => 0f;
    }

    private static string[] FindAssetPaths<T>() where T : UnityEngine.Object
    {
        return AssetDatabase.FindAssets($"t:{typeof(T).Name}")
            .Select(AssetDatabase.GUIDToAssetPath)
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();
    }

    private static IEnumerable<T> RequireItems<T>(
        IEnumerable<T> values,
        string sourceKind,
        string sourceId) where T : class
    {
        if (values == null)
        {
            throw new NarrativeMechanicCatalogUnsupportedSourceException(
                $"{sourceKind}-missing",
                $"'{sourceId}' has no authored {sourceKind} collection.");
        }

        foreach (T value in values)
        {
            if (value == null)
            {
                throw new NarrativeMechanicCatalogUnsupportedSourceException(
                    $"{sourceKind}-contains-null",
                    $"'{sourceId}' contains a null {sourceKind} entry.");
            }

            yield return value;
        }
    }

    private static NarrativeMechanicCatalogCanonicalJsonArray EnumArray<TEnum>(
        IEnumerable<TEnum> values,
        string sourceKind,
        string sourceId) where TEnum : struct, Enum
    {
        if (values == null)
        {
            throw new NarrativeMechanicCatalogUnsupportedSourceException(
                $"{sourceKind.Replace(' ', '-')}-missing",
                $"'{sourceId}' has no authored {sourceKind} collection.");
        }

        return new NarrativeMechanicCatalogCanonicalJsonArray(values.Select(value =>
            (NarrativeMechanicCatalogCanonicalJsonValue)NarrativeMechanicCatalogCanonicalJson.String(value.ToString())));
    }

    private static NarrativeMechanicCatalogCanonicalJsonArray StringArray(
        IEnumerable<string> values,
        string sourceKind,
        string sourceId)
    {
        if (values == null)
        {
            throw new NarrativeMechanicCatalogUnsupportedSourceException(
                $"{sourceKind.Replace(' ', '-')}-missing",
                $"'{sourceId}' has no authored {sourceKind} collection.");
        }

        return new NarrativeMechanicCatalogCanonicalJsonArray(values.Select(value =>
            (NarrativeMechanicCatalogCanonicalJsonValue)NarrativeMechanicCatalogCanonicalJson.String(
                RequireAuthoredText(value, sourceKind, sourceId))));
    }

    private static string RequireAuthoredText(string value, string field, string sourceId)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new NarrativeMechanicCatalogUnsupportedSourceException(
                "authored-text-missing",
                $"'{sourceId}' has no authored {field}.");
        }

        return value;
    }

    private static void AddAssetAndMeta(ICollection<string> relevantPaths, string assetPath)
    {
        relevantPaths.Add(NarrativeMechanicCatalogExportException.Require(assetPath, nameof(assetPath))
            .Replace('\\', '/'));
        relevantPaths.Add((assetPath + ".meta").Replace('\\', '/'));
    }
}
#endif
