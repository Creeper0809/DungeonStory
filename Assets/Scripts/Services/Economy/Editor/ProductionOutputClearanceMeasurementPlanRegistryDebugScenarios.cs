#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class
    ProductionOutputClearanceMeasurementPlanRegistryDebugScenarios
{
    private const string DefinitionId = "building:qa-clearance-plan";
    private const string WorkstationTag = "workstation:qa-clearance-plan";
    private const string RecipeCapability =
        "clearance-measurement:recipe-execution";
    private const string CropCapability =
        "clearance-measurement:crop-harvest-execution";
    private const string ApparelCapability =
        "clearance-measurement:apparel-execution";
    private const string CombatCapability =
        "clearance-measurement:combat-craft-execution";
    private const string CertifiedSeedCapability =
        "clearance-measurement:certified-seed-execution";

    [MenuItem(
        "DungeonStory/V27/Production/Validate Clearance Measurement Plan Registry")]
    public static void Validate()
    {
        VerifyAllProducerCapabilitiesAndStableWinner();
        VerifyUnregisteredCapabilityProducesTypedGap();
        VerifyUniquePayloadCannotFallBackToGenericItem();
        VerifyDuplicateCapabilityOwnerFailsLoudly();
        Debug.Log(
            "[ProductionOutputClearanceMeasurementPlanRegistry] focused scenarios passed.");
    }

    private static void VerifyAllProducerCapabilitiesAndStableWinner()
    {
        ProductionOutputClearanceMeasurementFacilityContext forward = Context(
            false);
        ProductionOutputClearanceMeasurementFacilityContext reverse = Context(
            true);
        ProductionOutputClearanceMeasurementPlanRegistry first = Registry(
            Contributors(false));
        ProductionOutputClearanceMeasurementPlanRegistry shuffled = Registry(
            Contributors(true));

        ProductionOutputClearanceMeasurementPlanResult a = first.Capture(
            forward);
        ProductionOutputClearanceMeasurementPlanResult b = shuffled.Capture(
            reverse);
        Require(a.IsComplete
            && b.IsComplete
            && a.Plan.Candidates.Count == 5
            && string.Equals(a.SourceDigest, b.SourceDigest,
                StringComparison.Ordinal)
            && string.Equals(a.Plan.SourceDigest, b.Plan.SourceDigest,
                StringComparison.Ordinal),
            "Complete clearance plan is not insertion-order deterministic.");
        Require(a.Plan.Winner.Source.MaximumSingleCompletionMassGrams == 9_000L
            && string.Equals(a.Plan.Winner.MeasurementCapabilityId,
                ApparelCapability, StringComparison.Ordinal)
            && string.Equals(a.Plan.Winner.Source.SourceCapabilityId,
                ApparelFacilityOutputCapacityContributor.Id,
                StringComparison.Ordinal),
            "Maximum single-completion footprint or stable tie-break drifted.");
        Require(a.Plan.Winner.Source.OutputCapabilityIds.SequenceEqual(
                new[] { ProductionOutputCapabilityIds.ApparelWorkOrder },
                StringComparer.Ordinal),
            "Unique apparel payload capability was erased from the plan.");
        Require(a.Plan.Candidates.Select(value =>
                value.Source.SourceCapabilityId)
            .OrderBy(value => value, StringComparer.Ordinal)
            .SequenceEqual(new[]
            {
                ApparelFacilityOutputCapacityContributor.Id,
                CertifiedSeedFacilityOutputCapacityContributor.Id,
                CombatCraftFacilityOutputCapacityContributor.Id,
                CropHarvestFacilityOutputCapacityContributor.Id,
                ProductionOutputClearanceMeasurementPlanRegistry
                    .RecipeSourceCapabilityId
            }.OrderBy(value => value, StringComparer.Ordinal),
                StringComparer.Ordinal),
            "Recipe/crop/apparel/combat/certified-seed capability coverage drifted.");
    }

    private static void VerifyUnregisteredCapabilityProducesTypedGap()
    {
        IProductionOutputClearanceMeasurementPlanContributor[] withoutApparel =
            Contributors(false)
                .Where(value => !string.Equals(value.SourceCapabilityId,
                    ApparelFacilityOutputCapacityContributor.Id,
                    StringComparison.Ordinal))
                .ToArray();
        ProductionOutputClearanceMeasurementPlanResult result = Registry(
            withoutApparel).Capture(Context(false));

        Require(!result.IsComplete
            && result.Plan == null
            && result.Gaps.Count == 1
            && result.Gaps[0].Reason
                == ProductionOutputClearanceMeasurementGapReason
                    .MeasurementCapabilityUnregistered
            && string.Equals(result.Gaps[0].Source.SourceCapabilityId,
                ApparelFacilityOutputCapacityContributor.Id,
                StringComparison.Ordinal),
            "An unregistered producer capability was omitted or defaulted.");
    }

    private static void VerifyUniquePayloadCannotFallBackToGenericItem()
    {
        IProductionOutputClearanceMeasurementPlanContributor[] contributors =
            Contributors(false)
                .Where(value => !string.Equals(value.SourceCapabilityId,
                    ApparelFacilityOutputCapacityContributor.Id,
                    StringComparison.Ordinal))
                .Concat(new[]
                {
                    new RejectingApparelContributor()
                })
                .ToArray();
        ProductionOutputClearanceMeasurementPlanResult result = Registry(
            contributors).Capture(Context(false));

        Require(!result.IsComplete
            && result.Gaps.Count == 1
            && result.Gaps[0].Reason
                == ProductionOutputClearanceMeasurementGapReason
                    .PhysicalPayloadUnsupported
            && result.Gaps[0].Source.OutputCapabilityIds.SequenceEqual(
                new[] { ProductionOutputCapabilityIds.ApparelWorkOrder },
                StringComparer.Ordinal),
            "An unsupported unique payload was converted to a generic item fallback.");
    }

    private static void VerifyDuplicateCapabilityOwnerFailsLoudly()
    {
        ExpectFailure(() => Registry(Contributors(false).Concat(new[]
        {
            Contributor(
                "clearance-plan-contributor:duplicate-recipe",
                ProductionOutputClearanceMeasurementPlanRegistry
                    .RecipeSourceCapabilityId,
                ProductionOutputClearanceMeasurementPlanRegistry
                    .RecipeSourceCapabilityVersion,
                "clearance-measurement:duplicate-recipe")
        }).ToArray()));
    }

    private static ProductionOutputClearanceMeasurementFacilityContext Context(
        bool reverse)
    {
        ProductionOutputClearanceRecipeMeasurementBranch recipe = new(
            "recipe:qa-clearance",
            "recipe-branch:qa-clearance",
            8_000L,
            new[] { ProductionOutputCapabilityIds.StandardDefinition },
            Digest('1'));
        ProductionFacilityOutputCapacityContribution[] capacity =
        {
            Capacity(
                CropHarvestFacilityOutputCapacityContributor.Id,
                CropHarvestFacilityOutputCapacityContributor.Version,
                "crop-harvest:qa",
                "output:qa-crop",
                "item:qa-crop",
                ProductionOutputCapabilityIds.CropHarvestSeedLot,
                7),
            Capacity(
                ApparelFacilityOutputCapacityContributor.Id,
                ApparelFacilityOutputCapacityContributor.Version,
                "apparel:qa",
                "output:qa-apparel",
                "item:qa-apparel",
                ProductionOutputCapabilityIds.ApparelWorkOrder,
                9),
            Capacity(
                CombatCraftFacilityOutputCapacityContributor.Id,
                CombatCraftFacilityOutputCapacityContributor.Version,
                "combat-craft:qa",
                "output:qa-combat",
                "item:qa-combat",
                ProductionOutputCapabilityIds.CombatEquipmentCraft,
                9),
            Capacity(
                CertifiedSeedFacilityOutputCapacityContributor.Id,
                CertifiedSeedFacilityOutputCapacityContributor.Version,
                "certified-seed:qa",
                "output:qa-seed",
                "item:qa-seed",
                ProductionOutputCapabilityIds.CertifiedSeed,
                1)
        };
        if (reverse)
            Array.Reverse(capacity);
        return new ProductionOutputClearanceMeasurementFacilityContext(
            DefinitionId,
            WorkstationTag,
            new[] { recipe },
            capacity);
    }

    private static ProductionFacilityOutputCapacityContribution Capacity(
        string contributorId,
        int contributorVersion,
        string branchId,
        string outputLineId,
        string itemId,
        string outputCapabilityId,
        int quantity) => new(
        contributorId,
        contributorVersion,
        true,
        new[]
        {
            new ProductionFacilityOutputCapacityBranch(
                branchId,
                new[]
                {
                    new ProductionFacilityOutputMaximumMassRequest(
                        outputLineId,
                        itemId,
                        outputCapabilityId,
                        quantity)
                })
        });

    private static IProductionOutputClearanceMeasurementPlanContributor[]
        Contributors(bool reverse)
    {
        IProductionOutputClearanceMeasurementPlanContributor[] contributors =
        {
            Contributor(
                "clearance-plan-contributor:recipe",
                ProductionOutputClearanceMeasurementPlanRegistry
                    .RecipeSourceCapabilityId,
                ProductionOutputClearanceMeasurementPlanRegistry
                    .RecipeSourceCapabilityVersion,
                RecipeCapability),
            Contributor(
                "clearance-plan-contributor:crop-harvest",
                CropHarvestFacilityOutputCapacityContributor.Id,
                CropHarvestFacilityOutputCapacityContributor.Version,
                CropCapability),
            Contributor(
                "clearance-plan-contributor:apparel",
                ApparelFacilityOutputCapacityContributor.Id,
                ApparelFacilityOutputCapacityContributor.Version,
                ApparelCapability),
            Contributor(
                "clearance-plan-contributor:combat-craft",
                CombatCraftFacilityOutputCapacityContributor.Id,
                CombatCraftFacilityOutputCapacityContributor.Version,
                CombatCapability),
            Contributor(
                "clearance-plan-contributor:certified-seed",
                CertifiedSeedFacilityOutputCapacityContributor.Id,
                CertifiedSeedFacilityOutputCapacityContributor.Version,
                CertifiedSeedCapability)
        };
        if (reverse)
            Array.Reverse(contributors);
        return contributors;
    }

    private static IProductionOutputClearanceMeasurementPlanContributor
        Contributor(
            string contributorId,
            string sourceCapabilityId,
            int sourceCapabilityVersion,
            string measurementCapabilityId) =>
        new ProductionOutputClearanceMeasurementPlanContributor(
            contributorId,
            1,
            sourceCapabilityId,
            sourceCapabilityVersion,
            measurementCapabilityId);

    private static ProductionOutputClearanceMeasurementPlanRegistry Registry(
        IEnumerable<IProductionOutputClearanceMeasurementPlanContributor>
            contributors) => new(
        contributors,
        new ProductionFacilityOutputCapacityBranchMassAuthority(
            new FixedMassSelector()));

    private static string Digest(char character) => new(character, 64);

    private static void ExpectFailure(Action action)
    {
        try
        {
            action();
        }
        catch (InvalidOperationException)
        {
            return;
        }
        throw new InvalidOperationException(
            "Expected clearance measurement validation failure did not occur.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private sealed class FixedMassSelector :
        IProductionOutputMaximumMassCapabilitySelector
    {
        public ProductionOutputMaximumMassProjection CaptureForCapability(
            string outputLineId,
            string itemId,
            string capabilityId,
            int maximumQuantity)
        {
            const string codecId = "production-output-codec:qa-clearance";
            string fingerprint =
                ProductionOutputCapabilityDescriptorFingerprint.Capture(
                    outputLineId,
                    itemId,
                    capabilityId,
                    1,
                    codecId,
                    1);
            ProductionOutputCapabilityDescriptor descriptor = new(
                outputLineId,
                itemId,
                capabilityId,
                1,
                codecId,
                1,
                fingerprint);
            return new ProductionOutputMaximumMassProjection(
                descriptor,
                maximumQuantity,
                1_000L,
                checked(1_000L * maximumQuantity),
                1L,
                Digest('a'));
        }
    }

    private sealed class RejectingApparelContributor :
        IProductionOutputClearanceMeasurementPlanContributor
    {
        public string ContributorId =>
            "clearance-plan-contributor:apparel-rejecting";
        public int ContractVersion => 1;
        public string SourceCapabilityId =>
            ApparelFacilityOutputCapacityContributor.Id;
        public int SourceCapabilityVersion =>
            ApparelFacilityOutputCapacityContributor.Version;
        public string MeasurementCapabilityId => ApparelCapability;

        public ProductionOutputClearanceMeasurementContribution Capture(
            ProductionOutputClearanceMeasurementSourceBranch source)
        {
            if (source == null
                || !string.Equals(source.SourceCapabilityId,
                    SourceCapabilityId, StringComparison.Ordinal)
                || source.SourceCapabilityVersion != SourceCapabilityVersion
                || !source.OutputCapabilityIds.Contains(
                    ProductionOutputCapabilityIds.ApparelWorkOrder,
                    StringComparer.Ordinal))
            {
                throw new InvalidOperationException(
                    "Rejecting apparel fixture received the wrong source.");
            }
            return ProductionOutputClearanceMeasurementContribution.Unsupported(
                source,
                ProductionOutputClearanceMeasurementGapReason
                    .PhysicalPayloadUnsupported,
                "fixture has no exact apparel component materializer");
        }
    }
}
#endif
