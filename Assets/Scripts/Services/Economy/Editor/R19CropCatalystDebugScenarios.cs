#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using UnityEditor;
using UnityEngine;

public static class R19CropCatalystDebugScenarios
{
    private const string MenuPath =
        "DungeonStory/R19/Validation/Verify Crop And Catalyst Authority";

    [MenuItem(MenuPath)]
    public static void ValidateFromMenu()
    {
        RunAll();
    }

    public static bool RunAll()
    {
        using var fixture = new Fixture();
        VerifyCropBaseGenomeAuthority(fixture);
        VerifyCatalystIdentityProjection(fixture);
        Debug.Log(
            "R19_CROP_CATALYST_PASS;directBaseGenome=true;"
            + "invalidCropReferencesRejected=true;catalystProjection=true");
        return true;
    }

    private static void VerifyCropBaseGenomeAuthority(Fixture fixture)
    {
        CropDefinitionSO sourceCrop = fixture.Baseline
            .GetAll<CropDefinitionSO>()
            .First(value => value?.BaseGenome != null);
        CropGenomeDefinitionSO selected = fixture.Clone(sourceCrop.BaseGenome);
        selected.ConfigureCultivar(
            "genome:r19-direct-selection",
            sourceCrop.CropId,
            "R19 direct selection",
            "R19 direct base genome fixture",
            Array.Empty<string>(),
            sourceCrop.BaseGenome.CreateRuntimeDefinition().loci);
        CropGenomeDefinitionSO suffixBase = fixture.Clone(sourceCrop.BaseGenome);
        CropDefinitionSO validCrop = fixture.Clone(sourceCrop);
        validCrop.ConfigureEcology(
            validCrop.SeedItemId,
            selected,
            validCrop.FamilyGroup,
            validCrop.EndemicDisease);
        IGameContentCatalog validContent = fixture.BuildCropContent(
            new[] { validCrop },
            new[] { suffixBase, selected });
        CropEcologyRuntime validRuntime = CreateRuntime(validContent);
        Require(validRuntime.TryClaim(out IReadOnlyList<SeedLotState> grant),
            "Direct base genome grant was not issued.");
        Require(grant.Count == 1
            && string.Equals(
                grant[0].cultivarGenomeId,
                selected.GenomeId,
                StringComparison.Ordinal),
            "Initial seed grant did not use CropDefinitionSO.BaseGenome.");
        _ = new CropGenomeReachableMaximumWitnessCatalog(validContent)
            .Capture(validCrop.CropId);

        CropDefinitionSO unknownCrop = fixture.Clone(validCrop);
        CropGenomeDefinitionSO unknownSelected = fixture.Clone(selected);
        CropGenomeDefinitionSO unknownSuffix = fixture.Clone(suffixBase);
        unknownCrop.ConfigureEcology(
            unknownCrop.SeedItemId,
            unknownSelected,
            unknownCrop.FamilyGroup,
            unknownCrop.EndemicDisease);
        CropGenomeDefinitionSO unknownGenome = fixture.Clone(suffixBase);
        unknownGenome.ConfigureCultivar(
            "genome:r19-unknown-crop",
            "crop:r19-unknown",
            "R19 unknown crop",
            "R19 unknown crop fixture",
            Array.Empty<string>(),
            suffixBase.CreateRuntimeDefinition().loci);
        RequireCropContentRejected(fixture.BuildCropContent(
            new[] { unknownCrop },
            new[] { unknownSuffix, unknownSelected, unknownGenome }));

        CropDefinitionSO otherSource = fixture.Baseline
            .GetAll<CropDefinitionSO>()
            .First(value => value?.BaseGenome != null
                && !string.Equals(
                    value.CropId,
                    sourceCrop.CropId,
                    StringComparison.Ordinal));
        CropDefinitionSO crossCrop = fixture.Clone(sourceCrop);
        CropDefinitionSO otherCrop = fixture.Clone(otherSource);
        CropGenomeDefinitionSO crossOwnGenome = fixture.Clone(
            sourceCrop.BaseGenome);
        CropGenomeDefinitionSO otherBase = fixture.Clone(otherSource.BaseGenome);
        crossCrop.ConfigureEcology(
            crossCrop.SeedItemId,
            otherBase,
            crossCrop.FamilyGroup,
            crossCrop.EndemicDisease);
        otherCrop.ConfigureEcology(
            otherCrop.SeedItemId,
            otherBase,
            otherCrop.FamilyGroup,
            otherCrop.EndemicDisease);
        RequireCropContentRejected(fixture.BuildCropContent(
            new[] { crossCrop, otherCrop },
            new[] { crossOwnGenome, otherBase }));

        CropDefinitionSO nullCrop = fixture.Clone(sourceCrop);
        CropGenomeDefinitionSO nullSuffix = fixture.Clone(sourceCrop.BaseGenome);
        nullCrop.ConfigureEcology(
            nullCrop.SeedItemId,
            null,
            nullCrop.FamilyGroup,
            nullCrop.EndemicDisease);
        RequireCropContentRejected(fixture.BuildCropContent(
            new[] { nullCrop },
            new[] { nullSuffix }));

        CropDefinitionSO unregisteredCrop = fixture.Clone(sourceCrop);
        CropGenomeDefinitionSO unregisteredBase = fixture.Clone(selected);
        CropGenomeDefinitionSO registeredSuffix = fixture.Clone(
            sourceCrop.BaseGenome);
        unregisteredCrop.ConfigureEcology(
            unregisteredCrop.SeedItemId,
            unregisteredBase,
            unregisteredCrop.FamilyGroup,
            unregisteredCrop.EndemicDisease);
        RequireCropContentRejected(fixture.BuildCropContent(
            new[] { unregisteredCrop },
            new[] { registeredSuffix }));

        IGameContentCatalog repairedContent = fixture.BuildCropContent(
            new[] { unregisteredCrop },
            new[] { registeredSuffix, unregisteredBase });
        CropEcologyRuntime repairedRuntime = CreateRuntime(repairedContent);
        Require(repairedRuntime.TryClaim(out IReadOnlyList<SeedLotState> repaired)
            && repaired.Count == 1
            && string.Equals(
                repaired[0].cultivarGenomeId,
                unregisteredBase.GenomeId,
                StringComparison.Ordinal),
            "Repaired direct base genome did not succeed on retry.");
        _ = new CropGenomeReachableMaximumWitnessCatalog(repairedContent)
            .Capture(unregisteredCrop.CropId);
    }

    private static void VerifyCatalystIdentityProjection(Fixture fixture)
    {
        GenericItemDefinitionSO catalyst = fixture.Clone(fixture.Baseline
            .GetAll<GenericItemDefinitionSO>()
            .First(value => EvolutionCatalystItemId.TryParseCatalyst(
                value.ItemId,
                out _)));
        Require(EvolutionCatalystItemId.TryParseCatalyst(
                catalyst.ItemId,
                out EquipmentCatalystDefinition parsedCatalyst),
            "Catalyst fixture did not use the production parser.");
        catalyst.SetFeature(new EvolutionCatalystItemFeature
        {
            family = parsedCatalyst.family + "-mismatch",
            potency = parsedCatalyst.potency == 1 ? 2 : 1,
            residue = true
        });
        RequireItemRejected(catalyst);
        catalyst.SetFeature(new EvolutionCatalystItemFeature
        {
            family = parsedCatalyst.family,
            potency = parsedCatalyst.potency,
            residue = false
        });
        RequireItemAccepted(catalyst);

        GenericItemDefinitionSO residue = fixture.Clone(fixture.Baseline
            .GetAll<GenericItemDefinitionSO>()
            .First(value => EvolutionCatalystItemId.TryParseResidue(
                value.ItemId,
                out _)));
        Require(EvolutionCatalystItemId.TryParseResidue(
                residue.ItemId,
                out int progressionLevel),
            "Residue fixture did not use the production parser.");
        int residuePotency = EvolutionCatalystProgression.GetPotencyGrade(
            progressionLevel);
        residue.SetFeature(new EvolutionCatalystItemFeature
        {
            family = "offense",
            potency = residuePotency == 1 ? 2 : 1,
            residue = false
        });
        RequireItemRejected(residue);
        residue.SetFeature(new EvolutionCatalystItemFeature
        {
            family = "universal",
            potency = residuePotency,
            residue = true
        });
        RequireItemAccepted(residue);

        GenericItemDefinitionSO ordinary = fixture.Clone(fixture.Baseline
            .GetAll<GenericItemDefinitionSO>()
            .First(value => !value.TryGetFeature(
                out EvolutionCatalystItemFeature _)));
        RequireItemAccepted(ordinary);
    }

    private static CropEcologyRuntime CreateRuntime(
        IGameContentCatalog content) => new(
            new DungeonRuntimeAggregateRootStore(),
            content,
            new RandomStreamProvider(190809),
            EmptyStock.Instance);

    private static void RequireCropContentRejected(
        IGameContentCatalog content)
    {
        Reject(() => _ = CreateRuntime(content));
        Reject(() => _ = new CropGenomeReachableMaximumWitnessCatalog(
            content));
    }

    private static void RequireItemAccepted(ItemDefinitionSO item)
    {
        IReadOnlyList<string> errors = new ResourceItemDefinitionCatalog(
            new[] { item }).Validate();
        Require(errors.Count == 0,
            "Expected valid item definition: " + string.Join(" | ", errors));
    }

    private static void RequireItemRejected(ItemDefinitionSO item)
    {
        IReadOnlyList<string> errors = new ResourceItemDefinitionCatalog(
            new[] { item }).Validate();
        Require(errors.Any(value => value.Contains(
                "does not match its item ID semantics",
                StringComparison.Ordinal)),
            "Expected catalyst identity projection mismatch rejection.");
    }

    private static void Reject(Action action)
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
            "Expected invalid crop content to be rejected.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private sealed class Fixture : IDisposable
    {
        private readonly List<UnityEngine.Object> owned = new();

        internal Fixture()
        {
            Baseline = new ResourceGameContentCatalog(
                new UnityGameContentRootLoader());
        }

        internal ResourceGameContentCatalog Baseline { get; }

        internal T Clone<T>(T source) where T : UnityEngine.Object
        {
            T clone = UnityEngine.Object.Instantiate(source);
            owned.Add(clone);
            return clone;
        }

        internal IGameContentCatalog BuildCropContent(
            IReadOnlyList<CropDefinitionSO> crops,
            IReadOnlyList<CropGenomeDefinitionSO> genomes) =>
            new CropContent(Baseline, crops, genomes);

        public void Dispose()
        {
            for (int index = owned.Count - 1; index >= 0; index--)
            {
                UnityEngine.Object.DestroyImmediate(owned[index]);
            }
        }
    }

    private sealed class CropContent : IGameContentCatalog
    {
        private readonly ResourceGameContentCatalog baseline;
        private readonly IReadOnlyList<CropDefinitionSO> crops;
        private readonly IReadOnlyList<CropGenomeDefinitionSO> genomes;

        internal CropContent(
            ResourceGameContentCatalog baseline,
            IReadOnlyList<CropDefinitionSO> crops,
            IReadOnlyList<CropGenomeDefinitionSO> genomes)
        {
            this.baseline = baseline;
            this.crops = crops;
            this.genomes = genomes;
        }

        public GameContentCatalogSO Root => baseline.Root;
        public ItemDefinitionCatalogSO Items => baseline.Items;
        public WorldInteractionPresentationCatalogSO WorldPresentation =>
            baseline.WorldPresentation;
        public CharacterSkillSystemSettingsSO CharacterSkillSettings =>
            baseline.CharacterSkillSettings;
        public GameMediaCatalogSO Media => baseline.Media;
        public GameDomainContentCatalogSO Domain => baseline.Domain;
        public IReadOnlyList<ServiceProcessSO> ServiceProcesses =>
            baseline.ServiceProcesses;
        public RoomEnvironmentSettingsSO RoomEnvironmentSettings =>
            baseline.RoomEnvironmentSettings;
        public IReadOnlyList<OffenseSiteArchetypeSO> SiteArchetypes =>
            baseline.SiteArchetypes;
        public IReadOnlyList<OffenseUrgentSiteDefinitionSO> UrgentSites =>
            baseline.UrgentSites;
        public IReadOnlyList<OffenseDecisionCardSO> DecisionCards =>
            baseline.DecisionCards;
        public IReadOnlyList<OffenseEncounterSO> Encounters =>
            baseline.Encounters;

        public IReadOnlyList<T> GetAll<T>() where T : ScriptableObject
        {
            if (typeof(T) == typeof(CropDefinitionSO))
            {
                return crops.Cast<T>().ToArray();
            }

            if (typeof(T) == typeof(CropGenomeDefinitionSO))
            {
                return genomes.Cast<T>().ToArray();
            }

            return baseline.GetAll<T>();
        }

        public T RequireSingle<T>() where T : ScriptableObject
        {
            IReadOnlyList<T> values = GetAll<T>();
            return values.Count == 1
                ? values[0]
                : throw new InvalidOperationException(
                    $"Expected one {typeof(T).Name}, found {values.Count}.");
        }
    }

    private sealed class EmptyStock : IStockQuery
    {
        internal static readonly EmptyStock Instance = new();

        public IReadOnlyList<WorldItemStackSnapshot> GetAllStacks() =>
            Array.Empty<WorldItemStackSnapshot>();
        public int GetGlobalQuantity(string itemDefinitionId) => 0;
        public int GetWarehouseQuantity(
            BuildingInstanceId warehouseId,
            string itemDefinitionId) => 0;
        public int GetWarehouseQuantity(
            BuildingInstanceId warehouseId,
            StockCategory category) => 0;
        public int GetWarehouseTotal(BuildingInstanceId warehouseId) => 0;
    }
}
#endif
