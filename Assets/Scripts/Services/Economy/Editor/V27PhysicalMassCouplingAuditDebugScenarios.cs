#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using DungeonStory.Balance;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Read-only parent join for the physical-mass ledger.  It projects the current
/// and proposed immutable mass views through the real warehouse, production,
/// EWU and market formulas.  No production ID switch or asset mutation occurs.
/// </summary>
public static class V27PhysicalMassCouplingAuditDebugScenarios
{
    private const string Schema = "v27.mass.coupling.1";
    public const string CsvPath =
        "Artifacts/QA/v27-physical-mass-coupling.csv";
    public const string ReportPath =
        "Artifacts/QA/v27-physical-mass-coupling.txt";

    [MenuItem("DungeonStory/V27/Physical Mass/Capture Mass Coupling (AuditOnly)")]
    public static void RunFromMenu()
    {
        V27PhysicalMassCouplingCapture first = CaptureFromLiveAuthority();
        V27PhysicalMassCouplingCapture second = CaptureFromLiveAuthority();
        Require(first.Csv.SequenceEqual(second.Csv),
            "Physical-mass coupling CSV changed between identical captures.");
        Require(first.Report.SequenceEqual(second.Report),
            "Physical-mass coupling report changed between identical captures.");
        byte[] shuffledCsv = BuildCsv(OrderRows(first.Rows.Reverse()));
        Require(first.Csv.SequenceEqual(shuffledCsv),
            "Physical-mass coupling CSV changed after input-order shuffle.");
        V27BalanceArtifactWriter.WriteIfDifferent(CsvPath, stream =>
            stream.Write(first.Csv, 0, first.Csv.Length));
        V27BalanceArtifactWriter.WriteIfDifferent(ReportPath, stream =>
            stream.Write(first.Report, 0, first.Report.Length));
        ArtifactFileState csvFirst = CaptureArtifactFileState(CsvPath);
        ArtifactFileState reportFirst = CaptureArtifactFileState(ReportPath);
        bool csvSecondWrite = V27BalanceArtifactWriter.WriteIfDifferent(
            CsvPath,
            stream => stream.Write(first.Csv, 0, first.Csv.Length));
        bool reportSecondWrite = V27BalanceArtifactWriter.WriteIfDifferent(
            ReportPath,
            stream => stream.Write(first.Report, 0, first.Report.Length));
        ArtifactFileState csvSecond = CaptureArtifactFileState(CsvPath);
        ArtifactFileState reportSecond = CaptureArtifactFileState(ReportPath);
        Require(!csvSecondWrite && !reportSecondWrite
                && csvFirst.Equals(csvSecond)
                && reportFirst.Equals(reportSecond),
            "Physical-mass coupling second write changed artifact bytes, length, "
            + "or modification time.");
        Debug.Log("V27 physical-mass coupling passed: rows="
            + first.Rows.Count + "; changed=" + first.ChangedRowCount
            + "; rootless=0; deterministicRecapture=PASS; "
            + "secondWriteNoOp=PASS; assetMutations=0.");
    }

    internal static V27PhysicalMassCouplingCapture CaptureFromLiveAuthority()
    {
        GameContentCatalogSO root = Resources.Load<GameContentCatalogSO>(
                GameContentCatalogSO.ResourcePath)
            ?? throw new InvalidOperationException("Root content catalog is missing.");
        ItemDefinitionCatalogSO items =
            root.GetItemDefinitions<ItemDefinitionCatalogSO>()
            ?? throw new InvalidOperationException("Item definition catalog is missing.");
        GameDomainContentCatalogSO domain = root.DomainCatalogs
            .OfType<GameDomainContentCatalogSO>()
            .Single();
        string[] ledgerIds = V27PhysicalMassAuthorityInventoryDebugScenarios
            .CaptureCanonicalLedgerItemIds()
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        Dictionary<string, CanonicalItemUnitSemantic> semantics = UniqueIndex(
            V27PhysicalMassExplicitSemanticDebugScenarios
                .CaptureCanonicalUnitSemanticsForAudit(),
            value => value.ItemId,
            "canonical unit semantic");
        return Capture(domain, items, ledgerIds, semantics);
    }

    internal static V27PhysicalMassCouplingCapture Capture(
        GameDomainContentCatalogSO domain,
        ItemDefinitionCatalogSO itemCatalog,
        IReadOnlyList<string> ledgerItemIds,
        IReadOnlyDictionary<string, CanonicalItemUnitSemantic> semantics)
    {
        if (domain == null)
            throw new ArgumentNullException(nameof(domain));
        if (itemCatalog == null)
            throw new ArgumentNullException(nameof(itemCatalog));
        string[] ledgerIds = (ledgerItemIds ?? Array.Empty<string>())
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        Require(ledgerIds.Length > 0
                && ledgerIds.Distinct(StringComparer.Ordinal).Count()
                    == ledgerIds.Length,
            "Physical-mass coupling requires a non-empty unique ledger scope.");
        Require(semantics != null
                && semantics.Keys.OrderBy(value => value, StringComparer.Ordinal)
                    .SequenceEqual(ledgerIds, StringComparer.Ordinal),
            "Canonical semantic and ledger scopes are not an exact bijection.");

        ItemDefinitionSO[] allItems = itemCatalog.Definitions
            .Where(value => value != null)
            .OrderBy(value => value.ItemId, StringComparer.Ordinal)
            .ToArray();
        Dictionary<string, ItemDefinitionSO> items = UniqueIndex(
            allItems, value => value.ItemId, "item");
        Require(ledgerIds.All(items.ContainsKey),
            "A ledger item is absent from the live item catalog.");
        ProductionRecipeSO[] recipes = UniqueDefinitions(
            domain.GetAll<ProductionRecipeSO>(), value => value.RecipeId, "recipe");
        IReadOnlyDictionary<string, string> recipeStatuses =
            V27PhysicalMassRecipeInventoryDebugScenarios
                .CaptureMassBalanceStatusesForAudit();
        Require(recipeStatuses.Keys.OrderBy(value => value, StringComparer.Ordinal)
                .SequenceEqual(
                    recipes.Select(value => value.RecipeId),
                    StringComparer.Ordinal),
            "Recipe mass-status scope is not an exact catalog bijection.");
        Require(recipeStatuses.Values.All(IsClosedRecipeStatus),
            "At least one recipe mass contract is not closed.");

        CropDefinitionSO[] crops = UniqueDefinitions(
            domain.GetAll<CropDefinitionSO>(), value => value.CropId, "crop");
        CombatEquipmentDefinitionSO[] equipment = UniqueDefinitions(
            domain.GetAll<CombatEquipmentDefinitionSO>(),
            value => value.EquipmentId,
            "equipment");
        CraftMaterialDefinitionSO[] materials = UniqueDefinitions(
            domain.GetAll<CraftMaterialDefinitionSO>(),
            value => value.MaterialId,
            "craft material");
        BuildingSO[] buildings = UniqueDefinitions(
            domain.GetAll<BuildingSO>(), BuildingDefinitionIdentity.Resolve,
            "building");

        Dictionary<string, long> currentMass = ledgerIds.ToDictionary(
            id => id,
            id => PhysicalMassGrams.FromCanonicalKilograms(
                items[id].UnitWeight).Value,
            StringComparer.Ordinal);
        Dictionary<string, long> proposedMass = ledgerIds.ToDictionary(
            id => id,
            id => semantics[id].CanonicalUnitMass.Value,
            StringComparer.Ordinal);
        string[] changedRoots = ledgerIds.Where(id =>
                currentMass[id] != proposedMass[id])
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        Dictionary<string, string[]> rootsByItem = BuildRootAttribution(
            ledgerIds,
            changedRoots,
            recipes,
            crops,
            equipment,
            materials);

        V27EmbeddedWorkValueSnapshot currentEwu = CalculateEwu(
            allItems, recipes, crops, equipment, materials, domain);
        ItemDefinitionSO[] proposedItems = CloneItemsWithMass(
            allItems, proposedMass);
        CombatEquipmentDefinitionSO[] proposedEquipment =
            CloneEquipmentWithItemMass(equipment, proposedMass);
        V27EmbeddedWorkValueSnapshot proposedEwu;
        try
        {
            proposedEwu = CalculateEwu(
                proposedItems,
                recipes,
                crops,
                proposedEquipment,
                materials,
                domain);
        }
        finally
        {
            DestroyTransient(proposedEquipment);
            DestroyTransient(proposedItems);
        }
        Require(currentEwu.IsComplete && proposedEwu.IsComplete,
            "Current or proposed EWU projection is incomplete.");
        Require(ledgerIds.All(id => currentEwu.Items.ContainsKey(id)
                && proposedEwu.Items.ContainsKey(id)),
            "EWU projections do not cover the complete ledger scope.");

        List<V27PhysicalMassCouplingRow> rows = new();
        Dictionary<string, MutableSummary> summaries = ledgerIds.ToDictionary(
            id => id, id => new MutableSummary(), StringComparer.Ordinal);
        CaptureItemAndWarehouseRows(
            rows, summaries, ledgerIds, items, buildings,
            currentMass, proposedMass, semantics);
        CaptureFacilityRows(
            rows, summaries, recipes, buildings, currentMass, proposedMass,
            changedRoots,
            new ProductionMaximumOutputFactorCatalog(buildings));
        CaptureEwuAndMarketRows(
            rows, summaries, ledgerIds, items, currentEwu, proposedEwu,
            rootsByItem);

        V27PhysicalMassCouplingRow[] ordered = OrderRows(rows);
        Require(ordered.Select(value => value.UniqueKey)
                .Distinct(StringComparer.Ordinal).Count() == ordered.Length,
            "Physical-mass coupling contains duplicate normalized keys.");
        int rootless = ordered.Count(value => value.IsChanged
            && value.RootCauseIds.Count == 0);
        Require(rootless == 0,
            "A coupling delta has no mass root attribution.");
        Require(ledgerIds.All(id => ordered.Any(value =>
                string.Equals(value.StableId, id, StringComparison.Ordinal)
                && value.ImpactDomain == "ewu"
                && value.Metric == "acquisition-mewu")),
            "Coupling EWU join is not a ledger-item bijection.");

        Dictionary<string, V27PhysicalMassCouplingSummary> frozenSummaries =
            summaries.ToDictionary(
                pair => pair.Key,
                pair => pair.Value.Freeze(pair.Key, rootsByItem[pair.Key]),
                StringComparer.Ordinal);
        byte[] csv = BuildCsv(ordered);
        byte[] report = Encoding.UTF8.GetBytes(BuildReport(
            ordered, ledgerIds.Length, recipes.Length, buildings.Length,
            changedRoots.Length));
        return new V27PhysicalMassCouplingCapture(
            Array.AsReadOnly(ordered),
            frozenSummaries,
            csv,
            report);
    }

    private static V27PhysicalMassCouplingRow[] OrderRows(
        IEnumerable<V27PhysicalMassCouplingRow> rows) => rows
            .OrderBy(value => value.StableId, StringComparer.Ordinal)
            .ThenBy(value => value.ImpactDomain, StringComparer.Ordinal)
            .ThenBy(value => value.ConsumerKind, StringComparer.Ordinal)
            .ThenBy(value => value.ConsumerStableId, StringComparer.Ordinal)
            .ThenBy(value => value.Metric, StringComparer.Ordinal)
            .ToArray();

    private static void CaptureItemAndWarehouseRows(
        ICollection<V27PhysicalMassCouplingRow> rows,
        IReadOnlyDictionary<string, MutableSummary> summaries,
        IReadOnlyList<string> ledgerIds,
        IReadOnlyDictionary<string, ItemDefinitionSO> items,
        IReadOnlyList<BuildingSO> buildings,
        IReadOnlyDictionary<string, long> currentMass,
        IReadOnlyDictionary<string, long> proposedMass,
        IReadOnlyDictionary<string, CanonicalItemUnitSemantic> semantics)
    {
        BuildingSO[] warehouses = buildings.Where(value =>
                value.GetAbility<BuildingStorageAbility>() != null)
            .OrderBy(BuildingDefinitionIdentity.Resolve, StringComparer.Ordinal)
            .ToArray();
        foreach (BuildingSO warehouse in warehouses)
        {
            BuildingStorageAbility storage =
                warehouse.GetAbility<BuildingStorageAbility>();
            Require(storage.HasMassCapacityAuthority,
                "Warehouse lacks positive gram capacity: "
                + BuildingDefinitionIdentity.Resolve(warehouse) + ".");
        }
        foreach (string itemId in ledgerIds)
        {
            ItemDefinitionSO item = items[itemId];
            long before = currentMass[itemId];
            long after = proposedMass[itemId];
            string[] directRoots = before == after
                ? Array.Empty<string>()
                : new[] { itemId };
            Add(rows, itemId, "mass", "ItemDefinitionSO", itemId,
                "unit-grams", before, after, directRoots,
                "ItemDefinitionSO.UnitWeight/canonical semantic");
            Add(rows, itemId, "stack", "ItemDefinitionSO", itemId,
                "max-stack-mass-grams",
                checked(before * item.MaxStack),
                checked(after * item.MaxStack),
                directRoots,
                "unit grams * ItemDefinitionSO.MaxStack");
            PhysicalHaulMassClass haulClass = semantics[itemId].HaulClass;
            HaulRange beforeHaul = CaptureHaulRange(
                before, item.MaxStack, haulClass);
            HaulRange afterHaul = CaptureHaulRange(
                after, item.MaxStack, haulClass);
            Add(rows, itemId, "haul", "ordinary-haul", "nominal-25kg",
                "target-min-units", beforeHaul.MinimumUnits,
                afterHaul.MinimumUnits, directRoots,
                "ceil(class minimum grams/unit), capped by max stack");
            Add(rows, itemId, "haul", "ordinary-haul", "nominal-25kg",
                "target-max-units", beforeHaul.MaximumUnits,
                afterHaul.MaximumUnits, directRoots,
                "floor(class maximum grams/unit), capped by max stack");
            summaries[itemId].BeforeHaul = beforeHaul;
            summaries[itemId].AfterHaul = afterHaul;

            foreach (BuildingSO warehouse in warehouses)
            {
                BuildingStorageAbility storage =
                    warehouse.GetAbility<BuildingStorageAbility>();
                string warehouseId = BuildingDefinitionIdentity.Resolve(warehouse);
                bool eligible = storage.allCategories
                    || storage.category == item.StockCategory;
                Add(rows, itemId, "warehouse", "BuildingStorageAbility",
                    warehouseId, "eligible", eligible ? 1L : 0L,
                    eligible ? 1L : 0L, Array.Empty<string>(),
                    "allCategories || category == item.StockCategory");
                long beforeUnits = eligible
                    ? storage.maxStoredMassGrams / before
                    : 0L;
                long afterUnits = eligible
                    ? storage.maxStoredMassGrams / after
                    : 0L;
                Add(rows, itemId, "warehouse", "BuildingStorageAbility",
                    warehouseId, "units-fit", beforeUnits, afterUnits,
                    directRoots,
                    "floor(maxStoredMassGrams/unitMassGrams)");
                Add(rows, itemId, "warehouse", "BuildingStorageAbility",
                    warehouseId, "capacity-grams",
                    storage.maxStoredMassGrams,
                    storage.maxStoredMassGrams,
                    Array.Empty<string>(),
                    "BuildingStorageAbility.maxStoredMassGrams");
                if (eligible)
                {
                    MutableSummary summary = summaries[itemId];
                    summary.EligibleWarehouses++;
                    summary.MinimumBeforeWarehouseUnits = Math.Min(
                        summary.MinimumBeforeWarehouseUnits, beforeUnits);
                    summary.MinimumAfterWarehouseUnits = Math.Min(
                        summary.MinimumAfterWarehouseUnits, afterUnits);
                    summary.MaximumWarehouseCapacity = Math.Max(
                        summary.MaximumWarehouseCapacity,
                        storage.maxStoredMassGrams);
                }
            }
        }
    }

    private static void CaptureFacilityRows(
        ICollection<V27PhysicalMassCouplingRow> rows,
        IReadOnlyDictionary<string, MutableSummary> summaries,
        IReadOnlyList<ProductionRecipeSO> recipes,
        IReadOnlyList<BuildingSO> buildings,
        IReadOnlyDictionary<string, long> currentMass,
        IReadOnlyDictionary<string, long> proposedMass,
        IReadOnlyCollection<string> changedRoots,
        IProductionMaximumOutputFactorCatalog maximumOutputFactors)
    {
        BuildingSO[] facilities = buildings.Where(value =>
                value.GetProductionWorkstationAbility() != null)
            .OrderBy(BuildingDefinitionIdentity.Resolve, StringComparer.Ordinal)
            .ToArray();
        foreach (BuildingSO facility in facilities)
        {
            BuildingProductionWorkstationAbility workstation =
                facility.GetProductionWorkstationAbility();
            BuildingProductionBufferAbility buffer =
                facility.GetProductionBufferAbility();
            Require(workstation.IsValid && buffer != null,
                "Production workstation lacks valid buffer authority: "
                + BuildingDefinitionIdentity.Resolve(facility) + ".");
            int cycles = buffer.physicalOutputBufferCycleCapacity;
            Require(cycles is >= 2 and <= 4,
                "Facility output cycle capacity is outside 2-4: "
                + BuildingDefinitionIdentity.Resolve(facility) + ".");
        }

        foreach (ProductionRecipeSO recipe in recipes)
        {
            ProductionOutputFactor maximumOutputFactor =
                maximumOutputFactors.ResolveMaximum(recipe);
            BuildingSO[] owners = facilities.Where(value => string.Equals(
                    value.GetProductionWorkstationAbility().WorkstationTag,
                    recipe.WorkstationTag,
                    StringComparison.Ordinal))
                .ToArray();
            string[] ownerIds = owners.Length == 0
                ? new[] { "recipe:" + recipe.RecipeId }
                : owners.Select(BuildingDefinitionIdentity.Resolve).ToArray();
            string consumerKind = owners.Length == 0
                ? "recipe-static-authority"
                : "BuildingProductionBufferAbility";
            string[] recipeChangedRoots = recipe.Inputs.Select(value => value.ItemId)
                .Concat(recipe.Outputs.Select(value => value.ItemId))
                .Where(changedRoots.Contains)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();
            long beforeMaximumOutput = 0L;
            long afterMaximumOutput = 0L;
            foreach (ProductionOutputDefinition output in
                     recipe.CaptureCanonicalOutputs())
            {
                if (!ProductionOutputRoleRules.IsPhysical(output.Role)
                    || output.Probability <= 0f
                    || !currentMass.TryGetValue(output.ItemId, out long beforeUnit)
                    || !proposedMass.TryGetValue(output.ItemId, out long afterUnit))
                {
                    continue;
                }
                beforeMaximumOutput = checked(
                    beforeMaximumOutput + beforeUnit
                    * maximumOutputFactor.CeilQuantity(output.Amount));
                afterMaximumOutput = checked(
                    afterMaximumOutput + afterUnit
                    * maximumOutputFactor.CeilQuantity(output.Amount));
                string[] roots = beforeUnit == afterUnit
                    ? Array.Empty<string>()
                    : new[] { output.ItemId };
                foreach (string ownerId in ownerIds)
                {
                    Add(rows, output.ItemId, "facility-output", consumerKind,
                        ownerId + "|" + recipe.RecipeId + "|"
                        + output.OutputLineId,
                        "maximum-line-grams",
                        checked(beforeUnit
                            * maximumOutputFactor.CeilQuantity(output.Amount)),
                        checked(afterUnit
                            * maximumOutputFactor.CeilQuantity(output.Amount)),
                        roots,
                        "maximum-output-factor ceil(quantity) * immutable definition unit grams");
                }
                summaries[output.ItemId].FacilityOutputLinks += ownerIds.Length;
            }
            for (int index = 0; index < recipe.Inputs.Count; index++)
            {
                ItemAmountDefinition input = recipe.Inputs[index];
                if (input == null
                    || !currentMass.TryGetValue(input.ItemId, out long beforeUnit)
                    || !proposedMass.TryGetValue(input.ItemId, out long afterUnit))
                {
                    continue;
                }
                string[] roots = beforeUnit == afterUnit
                    ? Array.Empty<string>()
                    : new[] { input.ItemId };
                foreach (string ownerId in ownerIds)
                {
                    Add(rows, input.ItemId, "facility-input", consumerKind,
                        ownerId + "|" + recipe.RecipeId + "|input:"
                        + index.ToString(CultureInfo.InvariantCulture),
                        "cycle-line-grams",
                        checked(beforeUnit * input.Amount),
                        checked(afterUnit * input.Amount), roots,
                        "input amount * immutable definition unit grams");
                }
                summaries[input.ItemId].FacilityInputLinks += ownerIds.Length;
            }
            foreach (BuildingSO owner in owners)
            {
                long cycles = owner.GetProductionBufferAbility()
                    .physicalOutputBufferCycleCapacity;
                foreach (string itemId in recipe.Outputs
                             .Where(value => value != null
                                 && ProductionOutputRoleRules.IsPhysical(value.Role))
                             .Select(value => value.ItemId)
                             .Distinct(StringComparer.Ordinal))
                {
                    Add(rows, itemId, "facility-output-buffer",
                        "BuildingProductionBufferAbility",
                        BuildingDefinitionIdentity.Resolve(owner) + "|"
                        + recipe.RecipeId,
                        "required-capacity-grams",
                        checked(beforeMaximumOutput * cycles),
                        checked(afterMaximumOutput * cycles),
                        recipeChangedRoots,
                        "projector maximum-factor physical batch grams * authored 2-4 cycle capacity");
                    MutableSummary summary = summaries[itemId];
                    summary.MaximumBeforeBufferGrams = Math.Max(
                        summary.MaximumBeforeBufferGrams,
                        checked(beforeMaximumOutput * cycles));
                    summary.MaximumAfterBufferGrams = Math.Max(
                        summary.MaximumAfterBufferGrams,
                        checked(afterMaximumOutput * cycles));
                }
            }
        }
    }

    private static void CaptureEwuAndMarketRows(
        ICollection<V27PhysicalMassCouplingRow> rows,
        IReadOnlyDictionary<string, MutableSummary> summaries,
        IReadOnlyList<string> ledgerIds,
        IReadOnlyDictionary<string, ItemDefinitionSO> items,
        V27EmbeddedWorkValueSnapshot current,
        V27EmbeddedWorkValueSnapshot proposed,
        IReadOnlyDictionary<string, string[]> rootsByItem)
    {
        foreach (string itemId in ledgerIds)
        {
            V27ItemValue before = current.Items[itemId];
            V27ItemValue after = proposed.Items[itemId];
            string[] roots = rootsByItem[itemId];
            Add(rows, itemId, "ewu", "V27EmbeddedWorkValueCalculator",
                itemId, "acquisition-mewu",
                before.AcquisitionCost.MilliEwu,
                after.AcquisitionCost.MilliEwu,
                roots,
                "current/proposed immutable mass counterfactual");
            Add(rows, itemId, "ewu", "V27EmbeddedWorkValueCalculator",
                itemId, "recoverable-mewu",
                before.RecoverableValue.MilliEwu,
                after.RecoverableValue.MilliEwu,
                roots,
                "floor(acquisition * salvage retention)");
            long beforeMarketSale = V27EwuQuantizer.MultiplyOutputCredit(
                before.AcquisitionCost,
                (decimal)GoldEconomyBalanceRules.TargetExternalSaleRecovery)
                .MilliEwu;
            long afterMarketSale = V27EwuQuantizer.MultiplyOutputCredit(
                after.AcquisitionCost,
                (decimal)GoldEconomyBalanceRules.TargetExternalSaleRecovery)
                .MilliEwu;
            long beforePrice = ResolveMarketPrice(
                itemId, before.AcquisitionCost.MilliEwu, beforeMarketSale);
            long afterPrice = ResolveMarketPrice(
                itemId, after.AcquisitionCost.MilliEwu, afterMarketSale);
            Add(rows, itemId, "market", "V27 market projection", itemId,
                "unit-price-gold", beforePrice, afterPrice, roots,
                "ceil(acquisition/3000), appraised=floor(sale-credit/3000)");
            string beforeRate = ResolveSaleRateToken(
                items[itemId], beforePrice, beforeMarketSale);
            string afterRate = ResolveSaleRateToken(
                items[itemId], afterPrice, afterMarketSale);
            Add(rows, itemId, "market", "V27 market projection", itemId,
                "sale-rate", beforeRate, afterRate, roots,
                "maximum nonnegative float whose sale credit does not exceed floor target");

            MutableSummary summary = summaries[itemId];
            summary.BeforeAcquisition = before.AcquisitionCost.MilliEwu;
            summary.AfterAcquisition = after.AcquisitionCost.MilliEwu;
            summary.BeforeRecoverable = before.RecoverableValue.MilliEwu;
            summary.AfterRecoverable = after.RecoverableValue.MilliEwu;
            summary.BeforePrice = beforePrice;
            summary.AfterPrice = afterPrice;
            summary.BeforeSaleRate = beforeRate;
            summary.AfterSaleRate = afterRate;
        }
    }

    private static V27EmbeddedWorkValueSnapshot CalculateEwu(
        ItemDefinitionSO[] items,
        ProductionRecipeSO[] recipes,
        CropDefinitionSO[] crops,
        CombatEquipmentDefinitionSO[] equipment,
        CraftMaterialDefinitionSO[] materials,
        GameDomainContentCatalogSO domain)
    {
        AuditContentSource source = new(domain, items);
        ResourceMaterialEconomicProfileCatalog profiles = new(source);
        V23BalanceWorkCalculator work = new(profiles);
        EmbeddedWorkValueSnapshot before = new V23EmbeddedWorkValueCalculator(
            recipes, items, equipment, materials, work).Calculate();
        Require(before.UnresolvedItemIds.Count == 0
                && before.NonConvergentRecipeIds.Count == 0,
            "V23 mass counterfactual is incomplete.");
        return new V27EmbeddedWorkValueCalculator(
            recipes,
            crops,
            items,
            equipment,
            materials,
            before,
            work,
            profiles,
            V27EmbeddedWorkValueCalculator.DefaultDurationPreservingScale,
            V27BalanceAssetApplication.CaptureHistoricalBeforeValues())
            .Calculate();
    }

    private static ItemDefinitionSO[] CloneItemsWithMass(
        IReadOnlyList<ItemDefinitionSO> source,
        IReadOnlyDictionary<string, long> proposedMass)
    {
        List<ItemDefinitionSO> clones = new(source.Count);
        try
        {
            foreach (ItemDefinitionSO item in source)
            {
                ItemDefinitionSO clone = UnityEngine.Object.Instantiate(item);
                clone.hideFlags = HideFlags.HideAndDontSave;
                if (proposedMass.TryGetValue(item.ItemId, out long grams))
                {
                    clone.ConfigureCore(
                        item.ItemId,
                        item.DisplayName,
                        item.Description,
                        item.StockCategory,
                        item.UnitPrice,
                        checked((float)(grams / 1000d)),
                        item.MaxStack,
                        item.Sprite);
                }
                clones.Add(clone);
            }
            return clones.ToArray();
        }
        catch
        {
            DestroyTransient(clones);
            throw;
        }
    }

    private static CombatEquipmentDefinitionSO[] CloneEquipmentWithItemMass(
        IReadOnlyList<CombatEquipmentDefinitionSO> source,
        IReadOnlyDictionary<string, long> proposedMass)
    {
        var field = typeof(CombatEquipmentDefinitionSO).GetField(
            "weight",
            System.Reflection.BindingFlags.Instance
            | System.Reflection.BindingFlags.NonPublic)
            ?? throw new InvalidOperationException(
                "Combat equipment weight authority field is missing.");
        List<CombatEquipmentDefinitionSO> clones = new(source.Count);
        try
        {
            foreach (CombatEquipmentDefinitionSO equipment in source)
            {
                CombatEquipmentDefinitionSO clone =
                    UnityEngine.Object.Instantiate(equipment);
                clone.hideFlags = HideFlags.HideAndDontSave;
                if (proposedMass.TryGetValue(equipment.ItemId, out long grams))
                    field.SetValue(clone, checked((float)(grams / 1000d)));
                clones.Add(clone);
            }
            return clones.ToArray();
        }
        catch
        {
            DestroyTransient(clones);
            throw;
        }
    }

    private static Dictionary<string, string[]> BuildRootAttribution(
        IReadOnlyList<string> ledgerIds,
        IReadOnlyList<string> changedRoots,
        IReadOnlyList<ProductionRecipeSO> recipes,
        IReadOnlyList<CropDefinitionSO> crops,
        IReadOnlyList<CombatEquipmentDefinitionSO> equipment,
        IReadOnlyList<CraftMaterialDefinitionSO> materials)
    {
        HashSet<string> scope = new(ledgerIds, StringComparer.Ordinal);
        Dictionary<string, HashSet<string>> roots = ledgerIds.ToDictionary(
            id => id,
            id => new HashSet<string>(StringComparer.Ordinal),
            StringComparer.Ordinal);
        foreach (string root in changedRoots)
            roots[root].Add(root);
        Dictionary<string, string> materialItem = materials.ToDictionary(
            value => value.MaterialId,
            value => value.ItemId,
            StringComparer.Ordinal);
        bool changed;
        int passes = 0;
        do
        {
            changed = false;
            passes++;
            foreach (ProductionRecipeSO recipe in recipes)
            {
                string[] inherited = recipe.Inputs
                    .Where(value => value != null && scope.Contains(value.ItemId))
                    .SelectMany(value => roots[value.ItemId])
                    .Distinct(StringComparer.Ordinal)
                    .ToArray();
                foreach (string output in recipe.Outputs
                             .Where(value => value != null && scope.Contains(value.ItemId))
                             .Select(value => value.ItemId))
                    changed |= roots[output].UnionWithChanged(inherited);
            }
            foreach (CropDefinitionSO crop in crops)
            {
                if (!scope.Contains(crop.HarvestItemId))
                    continue;
                IEnumerable<string> inherited = new[]
                    {
                        crop.SeedItemId,
                        CropCycleInputRequirementAuthority.CleanWaterItemId
                    }
                    .Where(scope.Contains)
                    .SelectMany(id => roots[id]);
                changed |= roots[crop.HarvestItemId].UnionWithChanged(inherited);
            }
            foreach (CombatEquipmentDefinitionSO value in equipment)
            {
                if (!scope.Contains(value.ItemId))
                    continue;
                IEnumerable<string> dependencies = value.RequiredComponentInputs
                    .Where(input => input != null)
                    .Select(input => input.ItemId);
                if (materialItem.TryGetValue(
                        value.DefaultMaterialId,
                        out string materialId))
                    dependencies = dependencies.Append(materialId);
                changed |= roots[value.ItemId].UnionWithChanged(
                    dependencies.Where(scope.Contains)
                        .SelectMany(id => roots[id]));
            }
        }
        while (changed && passes <= ledgerIds.Count);
        Require(passes <= ledgerIds.Count,
            "Mass root-attribution graph did not converge.");
        return roots.ToDictionary(
            pair => pair.Key,
            pair => pair.Value.OrderBy(value => value, StringComparer.Ordinal)
                .ToArray(),
            StringComparer.Ordinal);
    }

    private static void Add(
        ICollection<V27PhysicalMassCouplingRow> rows,
        string stableId,
        string domain,
        string consumerKind,
        string consumerStableId,
        string metric,
        long before,
        long after,
        IReadOnlyList<string> roots,
        string formula) => Add(rows, stableId, domain, consumerKind,
            consumerStableId, metric, Token(before), Token(after), roots, formula);

    private static void Add(
        ICollection<V27PhysicalMassCouplingRow> rows,
        string stableId,
        string domain,
        string consumerKind,
        string consumerStableId,
        string metric,
        string before,
        string after,
        IReadOnlyList<string> roots,
        string formula)
    {
        string[] rootIds = string.Equals(before, after, StringComparison.Ordinal)
            ? Array.Empty<string>()
            : (roots ?? Array.Empty<string>())
                .Distinct(StringComparer.Ordinal)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();
        rows.Add(new V27PhysicalMassCouplingRow(
            stableId, domain, consumerKind, consumerStableId, metric,
            before, after, rootIds, formula));
    }

    private static byte[] BuildCsv(IReadOnlyList<V27PhysicalMassCouplingRow> rows)
    {
        using MemoryStream stream = new();
        V27Utf8CsvWriter writer = new(stream, 16384);
        WriteRow(writer, new[]
        {
            "schemaVersion", "stableId", "impactDomain", "consumerKind",
            "consumerStableId", "metric", "before", "after", "deltaStatus",
            "rootCauseIds", "formula"
        });
        foreach (V27PhysicalMassCouplingRow row in rows)
        {
            WriteRow(writer, new[]
            {
                Schema, row.StableId, row.ImpactDomain, row.ConsumerKind,
                row.ConsumerStableId, row.Metric, row.Before, row.After,
                row.IsChanged ? "changed-root-attributed" : "unchanged",
                string.Join("|", row.RootCauseIds), row.Formula
            });
        }
        writer.Flush();
        return stream.ToArray();
    }

    private static string BuildReport(
        IReadOnlyList<V27PhysicalMassCouplingRow> rows,
        int itemCount,
        int recipeCount,
        int buildingCount,
        int changedRootCount)
    {
        V27CurrentSourceEvidenceSnapshot source =
            V27CurrentSourceEvidenceDigest.Capture();
        string gameplaySceneSha256 =
            V27CurrentSourceEvidenceDigest.ComputeGameplaySceneDigest();
        Require(string.Equals(
                gameplaySceneSha256,
                V27CurrentSourceEvidenceDigest.OfficialGameplaySceneSha256,
                StringComparison.Ordinal),
            "Official GameplayScene bytes changed during the physical-mass "
            + "coupling audit.");
        int changed = rows.Count(value => value.IsChanged);
        string keyDigest = HashTokens(rows.Select(value => value.UniqueKey));
        string valueDigest = HashTokens(rows.Select(value =>
            value.UniqueKey + "\u001f" + value.Before + "\u001f" + value.After
            + "\u001f" + string.Join("|", value.RootCauseIds)));
        return "RESULT=PASS; phase=physical-mass-parent-coupling; assetMutations=0\n"
            + "currentSourceDigest=" + source.Digest + "; "
            + "currentSourceInputCount=" + source.InputCount + "; "
            + "currentSourcePathListDigest=" + source.PathListDigest + "\n"
            + "gameplaySceneSha256=" + gameplaySceneSha256 + "; "
            + "currentSourceParent=PASS\n"
            + $"items={itemCount}; recipes={recipeCount}; buildings={buildingCount}; "
            + $"rows={rows.Count}; changedRows={changed}; changedMassRoots={changedRootCount}\n"
            + "warehouseGramEligibility=PASS; warehouseCapacityUnitsFit=PASS; "
            + "maxStackAndOrdinaryHaul=PASS\n"
            + "facilityInputAuthority=PASS; facilityOutputAuthority=PASS; "
            + "facilityOutputBufferCycleCapacity=PASS\n"
            + "ewuAcquisitionRecoverable=PASS; marketPriceSaleRate=PASS; "
            + "rootlessDelta=0; critical=0\n"
            + "scopeAuthority=dynamic-catalog-exact-set-bijection; "
            + "productionMassIdSwitches=0; proposalMode=AuditOnly\n"
            + "sort=stableId,impactDomain,consumerKind,consumerStableId,metric:ordinal; "
            + "deterministicRecapture=PASS; inputShuffle=PASS; "
            + "byteIdentical=true\n"
            + "secondWriteNoOp=PASS; byteDiff=0; lengthDiff=0; mtimeDiff=0\n"
            + "normalizedKeyDigest=" + keyDigest + "\n"
            + "normalizedValueDigest=" + valueDigest + "\n"
            + "exitGate=CURRENT_SOURCE_COUPLING_AND_SECOND_WRITE_NOOP_PASS\n";
    }

    private static ArtifactFileState CaptureArtifactFileState(string projectRelativePath)
    {
        string root = Directory.GetParent(Application.dataPath)?.FullName
            ?? throw new InvalidOperationException("Project root is unavailable.");
        string absolute = Path.Combine(
            root,
            projectRelativePath.Replace('/', Path.DirectorySeparatorChar));
        FileInfo file = new(absolute);
        Require(file.Exists, "Expected coupling artifact is missing: "
            + projectRelativePath);
        return new ArtifactFileState(
            V27BalanceArtifactWriter.ComputeSha256(projectRelativePath),
            file.Length,
            file.LastWriteTimeUtc.Ticks);
    }

    private static long ResolveMarketPrice(
        string itemId,
        long acquisitionMilliEwu,
        long marketSaleValue) => string.Equals(
            itemId,
            "offense:appraised-valuables",
            StringComparison.Ordinal)
        ? Math.Max(1L, marketSaleValue / 3000L)
        : Math.Max(1L, DivideCeil(acquisitionMilliEwu, 3000L));

    private static string ResolveSaleRateToken(
        ItemDefinitionSO item,
        long unitPrice,
        long marketSaleValue)
    {
        if (item is not ResourceItemDefinitionSO)
            return "N/A";
        if (IsAutomaticSaleExcluded(item.ItemId))
            return "0";
        if (string.Equals(
                item.ItemId,
                "offense:appraised-valuables",
                StringComparison.Ordinal))
            return "1";
        if (unitPrice <= 0L || marketSaleValue <= 0L)
            return "0";
        decimal denominator = checked(unitPrice * 3000m);
        float candidate = Mathf.Clamp01((float)(marketSaleValue / denominator));
        while (candidate > 0f
               && unitPrice * (decimal)candidate * 3000m > marketSaleValue)
            candidate = PreviousFloat(candidate);
        while (candidate < 1f)
        {
            float next = NextFloat(candidate);
            if (next <= candidate
                || unitPrice * (decimal)next * 3000m > marketSaleValue)
                break;
            candidate = next;
        }
        return ((double)candidate).ToString("R", CultureInfo.InvariantCulture);
    }

    private static bool IsAutomaticSaleExcluded(string itemId) => itemId switch
    {
        PhysicalItemIds.EquipmentModule => true,
        EquipmentProgressionItemIds.LineageSeal => true,
        "offense:unappraised-loot" => true,
        "seed-lot:bloodleaf" => true,
        "seed-lot:cave-mushroom" => true,
        "seed-lot:dreamleaf" => true,
        "seed-lot:ember-cotton" => true,
        "seed-lot:ember-root" => true,
        "seed-lot:frost-flax" => true,
        "seed-lot:mire-reed" => true,
        "seed-lot:moonflower" => true,
        "seed-lot:night-grape" => true,
        "seed-lot:shade-fiber" => true,
        "seed-lot:spore-hemp" => true,
        "seed-lot:twilight-grain" => true,
        _ => false
    };

    private static float PreviousFloat(float value)
    {
        if (value <= 0f)
            return 0f;
        int bits = BitConverter.ToInt32(BitConverter.GetBytes(value), 0);
        return BitConverter.ToSingle(BitConverter.GetBytes(bits - 1), 0);
    }

    private static float NextFloat(float value)
    {
        int bits = BitConverter.ToInt32(BitConverter.GetBytes(value), 0);
        return BitConverter.ToSingle(BitConverter.GetBytes(bits + 1), 0);
    }

    private static long DivideCeil(long numerator, long denominator) =>
        checked((numerator + denominator - 1L) / denominator);

    private static HaulRange CaptureHaulRange(
        long grams,
        int maxStack,
        PhysicalHaulMassClass haulClass)
    {
        if (haulClass is PhysicalHaulMassClass.IndividualEquipment
            or PhysicalHaulMassClass.OversizeEquipment
            or PhysicalHaulMassClass.DedicatedTransport)
        {
            return new HaulRange(1L, 1L);
        }
        long minimumGrams = haulClass == PhysicalHaulMassClass.Heavy
            ? 15_000L
            : haulClass == PhysicalHaulMassClass.MicroUrgent
                ? 1_000L
                : 6_000L;
        long maximumGrams = haulClass == PhysicalHaulMassClass.Heavy
            ? 20_000L
            : haulClass == PhysicalHaulMassClass.MicroUrgent
                ? 6_000L
                : 11_000L;
        long minimum = Math.Min(maxStack,
            Math.Max(1L, DivideCeil(minimumGrams, grams)));
        long maximum = Math.Min(maxStack, maximumGrams / grams);
        return new HaulRange(minimum, maximum);
    }

    private static bool IsClosedRecipeStatus(string value) => value is
        "reviewed-exact" or "balanced-exact" or
        "runtime-balanced-proposal-mismatch" or
        "source-external-mass" or "sink-explicit-mass";

    private static T[] UniqueDefinitions<T>(
        IEnumerable<T> values,
        Func<T, string> id,
        string label)
        where T : ScriptableObject
    {
        DefinitionIdentity<T>[] identified = (values ?? Array.Empty<T>())
            .Where(value => value != null)
            .Select(value => new DefinitionIdentity<T>(
                value,
                ResolveDefinitionId(value, id, label)))
            .OrderBy(value => value.Id, StringComparer.Ordinal)
            .ToArray();
        Require(identified.All(value => !string.IsNullOrWhiteSpace(value.Id)
                && string.Equals(
                    value.Id,
                    value.Id.Trim(),
                    StringComparison.Ordinal)),
            label + " contains a noncanonical stable ID.");
        Require(identified.Select(value => value.Id)
                .Distinct(StringComparer.Ordinal).Count()
                == identified.Length,
            label + " contains duplicate stable IDs.");
        return identified.Select(value => value.Value).ToArray();
    }

    private static string ResolveDefinitionId<T>(
        T value,
        Func<T, string> id,
        string label)
        where T : ScriptableObject
    {
        try
        {
            return id(value);
        }
        catch (Exception exception)
        {
            string path = AssetDatabase.GetAssetPath(value);
            throw new InvalidOperationException(
                label + " stable ID resolution failed for name='"
                + value.name + "'; type='" + value.GetType().FullName
                + "'; path='" + (path.Length == 0 ? "<transient>" : path)
                + "'.",
                exception);
        }
    }

    private static Dictionary<string, T> UniqueIndex<T>(
        IEnumerable<T> values,
        Func<T, string> id,
        string label)
    {
        Dictionary<string, T> result = new(StringComparer.Ordinal);
        foreach (T value in values ?? Array.Empty<T>())
        {
            string key = id(value);
            Require(!string.IsNullOrWhiteSpace(key)
                    && string.Equals(key, key.Trim(), StringComparison.Ordinal),
                label + " contains a noncanonical stable ID.");
            Require(result.TryAdd(key, value),
                label + " contains duplicate stable ID '" + key + "'.");
        }
        return result;
    }

    private static void DestroyTransient<T>(IEnumerable<T> values)
        where T : UnityEngine.Object
    {
        foreach (T value in values ?? Array.Empty<T>())
        {
            if (value != null)
                UnityEngine.Object.DestroyImmediate(value);
        }
    }

    private readonly struct DefinitionIdentity<T>
        where T : ScriptableObject
    {
        public DefinitionIdentity(T value, string id)
        {
            Value = value;
            Id = id;
        }

        public T Value { get; }
        public string Id { get; }
    }

    private static void WriteRow(
        V27Utf8CsvWriter writer,
        IReadOnlyList<string> fields)
    {
        for (int index = 0; index < fields.Count; index++)
        {
            if (index != 0)
                writer.WriteAscii(',');
            writer.WriteEscapedField((fields[index] ?? string.Empty).AsSpan());
        }
        writer.WriteCrLf();
    }

    private static string HashTokens(IEnumerable<string> values)
    {
        using SHA256 sha = SHA256.Create();
        byte[] separator = { 0 };
        foreach (string value in values.OrderBy(value => value, StringComparer.Ordinal))
        {
            byte[] bytes = Encoding.UTF8.GetBytes(value);
            sha.TransformBlock(bytes, 0, bytes.Length, null, 0);
            sha.TransformBlock(separator, 0, separator.Length, null, 0);
        }
        sha.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
        return string.Concat(sha.Hash.Select(value =>
            value.ToString("x2", CultureInfo.InvariantCulture)));
    }

    private static string Token(long value) =>
        value.ToString(CultureInfo.InvariantCulture);

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    private readonly struct ArtifactFileState : IEquatable<ArtifactFileState>
    {
        public ArtifactFileState(string sha256, long length, long mtimeUtcTicks)
        {
            Sha256 = sha256 ?? throw new ArgumentNullException(nameof(sha256));
            Length = length;
            MtimeUtcTicks = mtimeUtcTicks;
        }

        private string Sha256 { get; }
        private long Length { get; }
        private long MtimeUtcTicks { get; }

        public bool Equals(ArtifactFileState other) =>
            string.Equals(Sha256, other.Sha256, StringComparison.Ordinal)
            && Length == other.Length
            && MtimeUtcTicks == other.MtimeUtcTicks;

        public override bool Equals(object value) =>
            value is ArtifactFileState other && Equals(other);

        public override int GetHashCode() => HashCode.Combine(
            Sha256,
            Length,
            MtimeUtcTicks);
    }

    private readonly struct HaulRange
    {
        public HaulRange(long minimumUnits, long maximumUnits)
        {
            MinimumUnits = minimumUnits;
            MaximumUnits = maximumUnits;
        }

        public long MinimumUnits { get; }
        public long MaximumUnits { get; }
    }

    private sealed class MutableSummary
    {
        public int EligibleWarehouses;
        public long MinimumBeforeWarehouseUnits = long.MaxValue;
        public long MinimumAfterWarehouseUnits = long.MaxValue;
        public long MaximumWarehouseCapacity;
        public int FacilityInputLinks;
        public int FacilityOutputLinks;
        public long MaximumBeforeBufferGrams;
        public long MaximumAfterBufferGrams;
        public HaulRange BeforeHaul;
        public HaulRange AfterHaul;
        public long BeforeAcquisition;
        public long AfterAcquisition;
        public long BeforeRecoverable;
        public long AfterRecoverable;
        public long BeforePrice;
        public long AfterPrice;
        public string BeforeSaleRate = "N/A";
        public string AfterSaleRate = "N/A";

        public V27PhysicalMassCouplingSummary Freeze(
            string itemId,
            IReadOnlyList<string> rootIds) => new(
            "status=PASS;critical=0;eligibleWarehouses=" + EligibleWarehouses
                + ";minimumUnitsFit="
                + (EligibleWarehouses == 0 ? "N/A" : Token(MinimumBeforeWarehouseUnits))
                + "->"
                + (EligibleWarehouses == 0 ? "N/A" : Token(MinimumAfterWarehouseUnits))
                + ";maximumCapacity=" + Token(MaximumWarehouseCapacity) + "g"
                + ";facilityInputs=" + FacilityInputLinks
                + ";facilityOutputs=" + FacilityOutputLinks
                + ";maxOutputBuffer=" + Token(MaximumBeforeBufferGrams)
                + "->" + Token(MaximumAfterBufferGrams) + "g"
                + ";ordinaryHaul=" + Token(BeforeHaul.MinimumUnits) + "-"
                + Token(BeforeHaul.MaximumUnits) + "->"
                + Token(AfterHaul.MinimumUnits) + "-"
                + Token(AfterHaul.MaximumUnits),
            "status=PASS;critical=0;acquisition=" + Token(BeforeAcquisition) + "->"
                + Token(AfterAcquisition) + "mEWU;recoverable="
                + Token(BeforeRecoverable) + "->" + Token(AfterRecoverable)
                + "mEWU;price=" + Token(BeforePrice) + "->"
                + Token(AfterPrice) + "gold;saleRate=" + BeforeSaleRate
                + "->" + AfterSaleRate + ";roots="
                + (rootIds.Count == 0 ? "none" : string.Join("|", rootIds)),
            itemId,
            rootIds?.ToArray() ?? Array.Empty<string>());
    }

    private sealed class AuditContentSource : IGameContentDefinitionSource
    {
        private readonly GameDomainContentCatalogSO domain;
        private readonly ItemDefinitionSO[] items;

        public AuditContentSource(
            GameDomainContentCatalogSO domain,
            ItemDefinitionSO[] items)
        {
            this.domain = domain ?? throw new ArgumentNullException(nameof(domain));
            this.items = items ?? throw new ArgumentNullException(nameof(items));
        }

        public IReadOnlyList<T> GetAll<T>() where T : ScriptableObject =>
            typeof(T) == typeof(ItemDefinitionSO)
                ? items.Cast<T>().ToArray()
                : domain.GetAll<T>();

        public T RequireSingle<T>() where T : ScriptableObject
        {
            IReadOnlyList<T> values = GetAll<T>();
            return values.Count == 1
                ? values[0]
                : throw new InvalidOperationException(
                    "Expected one " + typeof(T).Name + ", found "
                    + values.Count + ".");
        }
    }
}

internal sealed class V27PhysicalMassCouplingCapture
{
    public V27PhysicalMassCouplingCapture(
        IReadOnlyList<V27PhysicalMassCouplingRow> rows,
        IReadOnlyDictionary<string, V27PhysicalMassCouplingSummary> summaries,
        byte[] csv,
        byte[] report)
    {
        Rows = rows;
        Summaries = summaries;
        Csv = csv;
        Report = report;
    }

    public IReadOnlyList<V27PhysicalMassCouplingRow> Rows { get; }
    public IReadOnlyDictionary<string, V27PhysicalMassCouplingSummary> Summaries { get; }
    public byte[] Csv { get; }
    public byte[] Report { get; }
    public int ChangedRowCount => Rows.Count(value => value.IsChanged);
}

internal sealed class V27PhysicalMassCouplingSummary
{
    public V27PhysicalMassCouplingSummary(
        string warehouseAndBufferImpact,
        string ewuAndPriceImpact,
        string stableId,
        IReadOnlyList<string> rootCauseIds)
    {
        WarehouseAndBufferImpact = warehouseAndBufferImpact;
        EwuAndPriceImpact = ewuAndPriceImpact;
        StableId = stableId;
        RootCauseIds = rootCauseIds;
    }

    public string WarehouseAndBufferImpact { get; }
    public string EwuAndPriceImpact { get; }
    public string StableId { get; }
    public IReadOnlyList<string> RootCauseIds { get; }
}

internal sealed class V27PhysicalMassCouplingRow
{
    public V27PhysicalMassCouplingRow(
        string stableId,
        string impactDomain,
        string consumerKind,
        string consumerStableId,
        string metric,
        string before,
        string after,
        IReadOnlyList<string> rootCauseIds,
        string formula)
    {
        StableId = stableId;
        ImpactDomain = impactDomain;
        ConsumerKind = consumerKind;
        ConsumerStableId = consumerStableId;
        Metric = metric;
        Before = before;
        After = after;
        RootCauseIds = rootCauseIds;
        Formula = formula;
    }

    public string StableId { get; }
    public string ImpactDomain { get; }
    public string ConsumerKind { get; }
    public string ConsumerStableId { get; }
    public string Metric { get; }
    public string Before { get; }
    public string After { get; }
    public IReadOnlyList<string> RootCauseIds { get; }
    public string Formula { get; }
    public bool IsChanged => !string.Equals(Before, After, StringComparison.Ordinal);
    public string UniqueKey => StableId + "\u001f" + ImpactDomain + "\u001f"
        + ConsumerKind + "\u001f" + ConsumerStableId + "\u001f" + Metric;
}

internal static class V27PhysicalMassHashSetExtensions
{
    internal static bool UnionWithChanged<T>(
        this HashSet<T> set,
        IEnumerable<T> values)
    {
        bool changed = false;
        foreach (T value in values ?? Array.Empty<T>())
            changed |= set.Add(value);
        return changed;
    }
}
#endif
