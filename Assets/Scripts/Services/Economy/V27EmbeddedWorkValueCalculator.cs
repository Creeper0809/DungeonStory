using System;
using System.Collections.Generic;
using System.Linq;

namespace DungeonStory.Balance
{
    [BalanceImmutableRecord]
    public sealed class V27ItemValue
    {
        internal V27ItemValue(
            string itemId,
            EwuAmount acquisitionCost,
            EwuAmount recoverableValue,
            string selectedSourceId)
        {
            ItemId = itemId;
            AcquisitionCost = acquisitionCost;
            RecoverableValue = recoverableValue;
            SelectedSourceId = selectedSourceId;
            if (recoverableValue > acquisitionCost)
                throw new InvalidOperationException(
                    $"Recoverable value exceeds acquisition cost for {itemId}.");
        }

        public string ItemId { get; }
        public EwuAmount AcquisitionCost { get; }
        public EwuAmount RecoverableValue { get; }
        public string SelectedSourceId { get; }
    }

    [BalanceImmutableRecord]
    public sealed class V27RecipeValueBreakdown
    {
        internal V27RecipeValueBreakdown(
            string recipeId,
            EwuAmount inputDebit,
            EwuAmount directWorkDebit,
            EwuAmount logisticsDebit,
            EwuAmount infrastructureDebit,
            EwuAmount expectedLossDebit,
            EwuRational expectedOutputUnits,
            EwuAmount perUnitAcquisition,
            EwuAmount totalOutputCredit,
            long transformMarginMilliEwu)
        {
            RecipeId = recipeId;
            InputDebit = inputDebit;
            DirectWorkDebit = directWorkDebit;
            LogisticsDebit = logisticsDebit;
            InfrastructureDebit = infrastructureDebit;
            ExpectedLossDebit = expectedLossDebit;
            ExpectedOutputUnits = expectedOutputUnits;
            PerUnitAcquisition = perUnitAcquisition;
            TotalOutputCredit = totalOutputCredit;
            TransformMarginMilliEwu = transformMarginMilliEwu;
        }

        public string RecipeId { get; }
        public EwuAmount InputDebit { get; }
        public EwuAmount DirectWorkDebit { get; }
        public EwuAmount LogisticsDebit { get; }
        public EwuAmount InfrastructureDebit { get; }
        public EwuAmount ExpectedLossDebit { get; }
        public EwuRational ExpectedOutputUnits { get; }
        public EwuAmount PerUnitAcquisition { get; }
        public EwuAmount TotalOutputCredit { get; }
        public long TransformMarginMilliEwu { get; }
        public EwuAmount TotalDebit => InputDebit + DirectWorkDebit + LogisticsDebit
            + InfrastructureDebit + ExpectedLossDebit;
    }

    [BalanceImmutableRecord]
    public sealed class V27CropValueBreakdown
    {
        internal V27CropValueBreakdown(
            string cropId,
            string harvestItemId,
            string seedItemId,
            int cleanWaterUnits,
            EwuAmount inputDebit,
            EwuAmount directWorkDebit,
            EwuAmount logisticsDebit,
            EwuAmount infrastructureDebit,
            EwuAmount expectedLossDebit,
            int expectedOutputUnits,
            EwuAmount perUnitAcquisition,
            EwuAmount totalOutputCredit,
            long transformMarginMilliEwu)
        {
            CropId = cropId;
            HarvestItemId = harvestItemId;
            SeedItemId = seedItemId;
            CleanWaterUnits = cleanWaterUnits;
            InputDebit = inputDebit;
            DirectWorkDebit = directWorkDebit;
            LogisticsDebit = logisticsDebit;
            InfrastructureDebit = infrastructureDebit;
            ExpectedLossDebit = expectedLossDebit;
            ExpectedOutputUnits = expectedOutputUnits;
            PerUnitAcquisition = perUnitAcquisition;
            TotalOutputCredit = totalOutputCredit;
            TransformMarginMilliEwu = transformMarginMilliEwu;
        }

        public string CropId { get; }
        public string HarvestItemId { get; }
        public string SeedItemId { get; }
        public int CleanWaterUnits { get; }
        public EwuAmount InputDebit { get; }
        public EwuAmount DirectWorkDebit { get; }
        public EwuAmount LogisticsDebit { get; }
        public EwuAmount InfrastructureDebit { get; }
        public EwuAmount ExpectedLossDebit { get; }
        public int ExpectedOutputUnits { get; }
        public EwuAmount PerUnitAcquisition { get; }
        public EwuAmount TotalOutputCredit { get; }
        public long TransformMarginMilliEwu { get; }
        public EwuAmount TotalDebit => InputDebit + DirectWorkDebit + LogisticsDebit
            + InfrastructureDebit + ExpectedLossDebit;
    }

    [BalanceImmutableRecord]
    public sealed class V27EmbeddedWorkValueSnapshot
    {
        internal V27EmbeddedWorkValueSnapshot(
            IReadOnlyDictionary<string, V27ItemValue> items,
            IReadOnlyDictionary<string, V27RecipeValueBreakdown> recipes,
            IReadOnlyDictionary<string, V27CropValueBreakdown> crops,
            IReadOnlyList<string> externalSeedItemIds,
            IReadOnlyList<string> unresolvedItemIds,
            IReadOnlyList<string> nonConvergentRecipeIds)
        {
            Items = items;
            Recipes = recipes;
            Crops = crops;
            ExternalSeedItemIds = externalSeedItemIds;
            UnresolvedItemIds = unresolvedItemIds;
            NonConvergentRecipeIds = nonConvergentRecipeIds;
        }

        public IReadOnlyDictionary<string, V27ItemValue> Items { get; }
        public IReadOnlyDictionary<string, V27RecipeValueBreakdown> Recipes { get; }
        public IReadOnlyDictionary<string, V27CropValueBreakdown> Crops { get; }
        public IReadOnlyList<string> ExternalSeedItemIds { get; }
        public IReadOnlyList<string> UnresolvedItemIds { get; }
        public IReadOnlyList<string> NonConvergentRecipeIds { get; }
        public bool IsComplete => UnresolvedItemIds.Count == 0
            && NonConvergentRecipeIds.Count == 0;
    }

    [BalanceCaptureFactory]
    public sealed class V27EmbeddedWorkValueCalculator
    {
        public const decimal DefaultDurationPreservingScale = 2.25m;

        private readonly ProductionRecipeSO[] recipes;
        private readonly CropDefinitionSO[] crops;
        private readonly Dictionary<string, ItemDefinitionSO> items;
        private readonly CombatEquipmentDefinitionSO[] equipment;
        private readonly Dictionary<string, CraftMaterialDefinitionSO> materials;
        private readonly EmbeddedWorkValueSnapshot before;
        private readonly IBalanceWorkCalculator workCalculator;
        private readonly IMaterialEconomicProfileCatalog materialProfiles;
        private readonly decimal laborScale;
        private readonly IReadOnlyDictionary<string, string> authoredBeforeValues;

        public V27EmbeddedWorkValueCalculator(
            IEnumerable<ProductionRecipeSO> recipes,
            IEnumerable<CropDefinitionSO> crops,
            IEnumerable<ItemDefinitionSO> items,
            IEnumerable<CombatEquipmentDefinitionSO> equipment,
            IEnumerable<CraftMaterialDefinitionSO> materials,
            EmbeddedWorkValueSnapshot before,
            IBalanceWorkCalculator workCalculator,
            IMaterialEconomicProfileCatalog materialProfiles,
            decimal laborScale = DefaultDurationPreservingScale,
            IReadOnlyDictionary<string, string> authoredBeforeValues = null)
        {
            this.recipes = (recipes ?? throw new ArgumentNullException(nameof(recipes)))
                .Where(value => value != null)
                .OrderBy(value => value.RecipeId, StringComparer.Ordinal)
                .ToArray();
            this.crops = (crops ?? throw new ArgumentNullException(nameof(crops)))
                .Where(value => value != null)
                .OrderBy(value => value.CropId, StringComparer.Ordinal)
                .ToArray();
            this.items = (items ?? throw new ArgumentNullException(nameof(items)))
                .Where(value => value != null)
                .ToDictionary(value => BalanceCanonicalText.StableId(
                    value.ItemId,
                    $"item:{value.name}"), StringComparer.Ordinal);
            this.equipment = (equipment ?? throw new ArgumentNullException(nameof(equipment)))
                .Where(value => value != null)
                .OrderBy(value => value.EquipmentId, StringComparer.Ordinal)
                .ToArray();
            this.materials = (materials ?? throw new ArgumentNullException(nameof(materials)))
                .Where(value => value != null)
                .ToDictionary(value => BalanceCanonicalText.StableId(
                    value.MaterialId,
                    $"material:{value.name}"), StringComparer.Ordinal);
            this.before = before ?? throw new ArgumentNullException(nameof(before));
            this.workCalculator = workCalculator
                ?? throw new ArgumentNullException(nameof(workCalculator));
            this.materialProfiles = materialProfiles
                ?? throw new ArgumentNullException(nameof(materialProfiles));
            if (laborScale <= 0m)
                throw new ArgumentOutOfRangeException(nameof(laborScale));
            this.laborScale = laborScale;
            this.authoredBeforeValues = authoredBeforeValues;
        }

        public V27EmbeddedWorkValueSnapshot Calculate()
        {
            Dictionary<string, V27ItemValue> values =
                new Dictionary<string, V27ItemValue>(StringComparer.Ordinal);
            Dictionary<string, V27RecipeValueBreakdown> breakdowns =
                new Dictionary<string, V27RecipeValueBreakdown>(StringComparer.Ordinal);
            Dictionary<string, V27CropValueBreakdown> cropBreakdowns =
                new Dictionary<string, V27CropValueBreakdown>(StringComparer.Ordinal);
            string[] seeds = before.ExternalSeedItemIds
                .Select(value => BalanceCanonicalText.StableId(value, "V23 external seed"))
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();
            foreach (string itemId in seeds)
            {
                if (!items.TryGetValue(itemId, out ItemDefinitionSO item)
                    || !before.TryGetItemWork(itemId, out float beforeWork))
                {
                    continue;
                }
                EwuAmount acquisition = ScaleInput(beforeWork);
                values[itemId] = CreateItemValue(
                    item,
                    acquisition,
                    "external:" + itemId);
            }

            int maximumPasses = Math.Max(16, checked(items.Count * 4 + recipes.Length));
            bool updated = false;
            HashSet<string> updatedOnLastPass = new HashSet<string>(StringComparer.Ordinal);
            for (int pass = 0; pass < maximumPasses; pass++)
            {
                updated = false;
                updatedOnLastPass.Clear();
                foreach (ProductionRecipeSO recipe in recipes)
                {
                    if (recipe.FlowRole == ProductionFlowRole.Sink || recipe.Outputs.Count == 0)
                        continue;
                    if (!TryCalculateRecipe(recipe, values, out V27RecipeValueBreakdown result))
                        continue;
                    breakdowns[recipe.RecipeId] = result;
                    foreach (ProductionOutputDefinition output in recipe.Outputs)
                    {
                        if (output == null || output.Probability <= 0f)
                            continue;
                        string itemId = BalanceCanonicalText.StableId(
                            output.ItemId,
                            $"recipe:{recipe.RecipeId}:output");
                        if (!items.TryGetValue(itemId, out ItemDefinitionSO item))
                            continue;
                        if (!values.TryGetValue(itemId, out V27ItemValue current)
                            || result.PerUnitAcquisition < current.AcquisitionCost)
                        {
                            values[itemId] = CreateItemValue(
                                item,
                                result.PerUnitAcquisition,
                                recipe.RecipeId);
                            updated = true;
                            updatedOnLastPass.Add(recipe.RecipeId);
                        }
                    }
                }
                foreach (CropDefinitionSO crop in crops)
                {
                    if (!TryCalculateCrop(crop, values, out V27CropValueBreakdown result))
                        continue;
                    cropBreakdowns[crop.CropId] = result;
                    if (!items.TryGetValue(crop.HarvestItemId, out ItemDefinitionSO item))
                        continue;
                    if (!values.TryGetValue(crop.HarvestItemId, out V27ItemValue current)
                        || result.PerUnitAcquisition < current.AcquisitionCost)
                    {
                        values[crop.HarvestItemId] = CreateItemValue(
                            item,
                            result.PerUnitAcquisition,
                            crop.CropId);
                        updated = true;
                        updatedOnLastPass.Add(crop.CropId);
                    }
                }
                if (!updated)
                    break;
            }

            foreach (ProductionRecipeSO recipe in recipes)
            {
                if (recipe.FlowRole == ProductionFlowRole.Sink || recipe.Outputs.Count == 0)
                    continue;
                if (TryCalculateRecipe(recipe, values, out V27RecipeValueBreakdown final))
                    breakdowns[recipe.RecipeId] = final;
            }
            foreach (CropDefinitionSO crop in crops)
            {
                if (TryCalculateCrop(crop, values, out V27CropValueBreakdown final))
                    cropBreakdowns[crop.CropId] = final;
            }

            AddEquipmentValues(values);

            string[] referenced = recipes
                .SelectMany(recipe => recipe.Inputs.Select(value => value?.ItemId)
                    .Concat(recipe.Outputs.Select(value => value?.ItemId)))
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => BalanceCanonicalText.StableId(value, "recipe item"))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();
            referenced = referenced
                .Concat(crops.Select(value => value.HarvestItemId))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();
            string[] unresolved = referenced
                .Where(value => !values.ContainsKey(value))
                .Concat(equipment.Select(value => value.ItemId)
                    .Where(value => !values.ContainsKey(value)))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();
            string[] nonConvergent = updated
                ? updatedOnLastPass.OrderBy(value => value, StringComparer.Ordinal).ToArray()
                : Array.Empty<string>();
            return new V27EmbeddedWorkValueSnapshot(
                FreezeMap(values),
                FreezeMap(breakdowns),
                FreezeMap(cropBreakdowns),
                Array.AsReadOnly(seeds),
                Array.AsReadOnly(unresolved),
                Array.AsReadOnly(nonConvergent));
        }

        private bool TryCalculateCrop(
            CropDefinitionSO crop,
            IReadOnlyDictionary<string, V27ItemValue> values,
            out V27CropValueBreakdown result)
        {
            const string cleanWaterItemId = "resource:clean-water";
            decimal growthHours = BalanceCanonicalText.DecimalFromFiniteFloat(
                crop.GrowthHours,
                $"crop:{crop.CropId}:growthHours");
            decimal dailyWater = BalanceCanonicalText.DecimalFromFiniteFloat(
                crop.DailyWater,
                $"crop:{crop.CropId}:dailyWater");
            int cleanWaterUnits = dailyWater <= 0m
                ? 0
                : checked((int)decimal.Ceiling(dailyWater * growthHours / 24m));
            EwuAmount inputDebit = EwuAmount.Zero;
            if (cleanWaterUnits > 0)
            {
                if (!values.TryGetValue(cleanWaterItemId, out V27ItemValue water))
                {
                    result = null;
                    return false;
                }
                inputDebit += water.AcquisitionCost * cleanWaterUnits;
            }

            if (!items.TryGetValue(crop.HarvestItemId, out ItemDefinitionSO harvestItem)
                || !items.TryGetValue(crop.SeedItemId, out ItemDefinitionSO seedItem))
            {
                result = null;
                return false;
            }

            decimal sowWork = ResolveAuthoredBefore(
                crop.CropId,
                "authored-sow-wu",
                crop.SowWork,
                $"crop:{crop.CropId}:sowWork");
            decimal harvestWork = ResolveAuthoredBefore(
                crop.CropId,
                "authored-harvest-wu",
                crop.HarvestWork,
                $"crop:{crop.CropId}:harvestWork");
            decimal directBefore = checked(sowWork + harvestWork);
            // Crop sow/harvest are recurring throughput operations. Their WU
            // is already expressed per physical crop cycle and must not inherit
            // the period-preserving project multiplier.
            EwuAmount direct = V27EwuQuantizer.QuantizeInputDebit(directBefore);

            decimal inputWeight = ResolveWeight(crop.SeedItemId)
                + cleanWaterUnits * ResolveWeight(cleanWaterItemId);
            decimal outputWeight = checked(crop.Yield * ResolveWeight(crop.HarvestItemId));
            decimal logisticsBefore = checked(
                3m
                + (cleanWaterUnits > 0 ? 2m : 1m) * 0.75m
                + 2m * 0.50m
                + DecimalSquareRoot(inputWeight + outputWeight) * 0.60m);
            EwuAmount logistics = V27EwuQuantizer.QuantizeInputDebit(logisticsBefore);

            decimal infrastructureBefore = checked(directBefore * 0.10m
                + growthHours * 0.25m);
            EwuAmount infrastructure = V27EwuQuantizer.QuantizeInputDebit(
                infrastructureBefore);
            EwuAmount subtotal = inputDebit + direct + logistics + infrastructure;
            EwuAmount expectedLoss = V27EwuQuantizer.MultiplyInputDebit(subtotal, 0.05m);
            EwuAmount total = subtotal + expectedLoss;
            EwuAmount perUnit = V27EwuQuantizer.DivideInputCost(total.MilliEwu, crop.Yield);

            EwuAmount outputCredit = values.TryGetValue(
                    crop.HarvestItemId,
                    out V27ItemValue outputValue)
                ? outputValue.RecoverableValue * crop.Yield
                : EwuAmount.Zero;
            long margin = checked(outputCredit.MilliEwu - total.MilliEwu);
            result = new V27CropValueBreakdown(
                crop.CropId,
                crop.HarvestItemId,
                crop.SeedItemId,
                cleanWaterUnits,
                inputDebit,
                direct,
                logistics,
                infrastructure,
                expectedLoss,
                crop.Yield,
                perUnit,
                outputCredit,
                margin);
            return true;
        }

        private bool TryCalculateRecipe(
            ProductionRecipeSO recipe,
            IReadOnlyDictionary<string, V27ItemValue> values,
            out V27RecipeValueBreakdown result)
        {
            EwuAmount inputDebit = EwuAmount.Zero;
            foreach (ItemAmountDefinition input in recipe.Inputs)
            {
                if (input == null
                    || !values.TryGetValue(input.ItemId, out V27ItemValue itemValue))
                {
                    result = null;
                    return false;
                }
                inputDebit += itemValue.AcquisitionCost * input.Amount;
            }

            EwuRational expectedOutputUnits = EwuRational.Zero;
            foreach (ProductionOutputDefinition output in recipe.Outputs)
            {
                if (output == null)
                    continue;
                decimal probability = BalanceCanonicalText.DecimalFromFiniteFloat(
                    output.Probability,
                    $"recipe:{recipe.RecipeId}:probability");
                expectedOutputUnits += EwuRational.FromDecimal(probability) * output.Amount;
            }
            if (expectedOutputUnits.IsZero)
            {
                result = null;
                return false;
            }

            decimal directBefore = ResolveAuthoredBefore(
                recipe.RecipeId,
                "authored-required-wu",
                recipe.RequiredWork,
                $"recipe:{recipe.RecipeId}:directWork");
            decimal logisticsBefore = CalculateStandardLogisticsWork(recipe);
            decimal infrastructureBefore = CalculateInfrastructureWork(recipe, directBefore);
            // Recipes are repeatable batch throughput. Apply their authored
            // batch WU once; do not scale direct/logistics/infrastructure by
            // the project-duration multiplier.
            EwuAmount direct = V27EwuQuantizer.QuantizeInputDebit(directBefore);
            EwuAmount logistics = V27EwuQuantizer.QuantizeInputDebit(logisticsBefore);
            EwuAmount infrastructure = V27EwuQuantizer.QuantizeInputDebit(
                infrastructureBefore);
            EwuAmount subtotal = inputDebit + direct + logistics + infrastructure;
            decimal lossRate = recipe.FlowRole == ProductionFlowRole.Source
                ? 0.01m
                : recipe.ProcessKind == ProductionProcessKind.PassiveBatch
                    ? 0.05m
                    : 0.02m;
            EwuAmount expectedLoss = V27EwuQuantizer.MultiplyInputDebit(subtotal, lossRate);
            EwuAmount total = subtotal + expectedLoss;
            EwuAmount perUnit = V27EwuQuantizer.DivideInputCost(
                total.MilliEwu,
                expectedOutputUnits);

            EwuAmount outputCredit = EwuAmount.Zero;
            bool creditResolved = true;
            foreach (ProductionOutputDefinition output in recipe.Outputs)
            {
                if (output == null
                    || !values.TryGetValue(output.ItemId, out V27ItemValue outputValue))
                {
                    creditResolved = false;
                    break;
                }
                decimal probability = BalanceCanonicalText.DecimalFromFiniteFloat(
                    output.Probability,
                    $"recipe:{recipe.RecipeId}:probability");
                decimal expectedQuantity = checked(output.Amount * probability);
                outputCredit += V27EwuQuantizer.MultiplyOutputCredit(
                    outputValue.RecoverableValue,
                    expectedQuantity);
            }
            if (!creditResolved)
                outputCredit = EwuAmount.Zero;
            long margin = checked(outputCredit.MilliEwu - total.MilliEwu);
            result = new V27RecipeValueBreakdown(
                recipe.RecipeId,
                inputDebit,
                direct,
                logistics,
                infrastructure,
                expectedLoss,
                expectedOutputUnits,
                perUnit,
                outputCredit,
                margin);
            return true;
        }

        private void AddEquipmentValues(IDictionary<string, V27ItemValue> values)
        {
            foreach (CombatEquipmentDefinitionSO definition in equipment)
            {
                if (!materials.TryGetValue(
                        definition.DefaultMaterialId,
                        out CraftMaterialDefinitionSO material)
                    || !values.TryGetValue(material.ItemId, out V27ItemValue materialValue))
                {
                    continue;
                }

                EwuAmount inputs = materialValue.AcquisitionCost * definition.PrimaryMaterialAmount;
                bool resolved = true;
                foreach (ItemAmountDefinition component in definition.RequiredComponentInputs)
                {
                    if (component == null
                        || !values.TryGetValue(component.ItemId, out V27ItemValue componentValue))
                    {
                        resolved = false;
                        break;
                    }
                    inputs += componentValue.AcquisitionCost * component.Amount;
                }
                if (!resolved || !items.TryGetValue(definition.ItemId, out ItemDefinitionSO item))
                    continue;

                decimal directBefore = BalanceCanonicalText.DecimalFromFiniteFloat(
                    workCalculator.CalculateEquipment(definition, material.ItemId),
                    $"equipment:{definition.EquipmentId}:directWork");
                decimal weight = BalanceCanonicalText.DecimalFromFiniteFloat(
                    definition.Weight,
                    $"equipment:{definition.EquipmentId}:weight");
                decimal logisticsBefore = 3m
                    + (definition.RequiredComponentInputs.Count + 1) * 0.75m
                    + DecimalSquareRoot(weight) * 0.60m;
                decimal infrastructureRate = definition.Era switch
                {
                    EquipmentEra.RuneAbyssal => 0.25m,
                    EquipmentEra.MatureIndustrial => 0.22m,
                    EquipmentEra.EarlyIndustrial => 0.18m,
                    _ => 0.16m
                };
                EwuAmount direct = V27EwuQuantizer.QuantizeInputDebit(
                    checked(directBefore * laborScale));
                EwuAmount logistics = V27EwuQuantizer.QuantizeInputDebit(
                    checked(logisticsBefore * laborScale));
                EwuAmount infrastructure = V27EwuQuantizer.QuantizeInputDebit(
                    checked(directBefore * infrastructureRate * laborScale));
                EwuAmount subtotal = inputs + direct + logistics + infrastructure;
                EwuAmount loss = V27EwuQuantizer.MultiplyInputDebit(subtotal, 0.02m);
                EwuAmount acquisition = subtotal + loss;
                values[definition.ItemId] = CreateItemValue(
                    item,
                    acquisition,
                    "equipment:" + definition.EquipmentId);
            }
        }

        private V27ItemValue CreateItemValue(
            ItemDefinitionSO item,
            EwuAmount acquisition,
            string selectedSourceId)
        {
            decimal retention = BalanceCanonicalText.DecimalFromFiniteFloat(
                materialProfiles.GetSalvageRetention(item.ItemId),
                $"item:{item.ItemId}:salvageRetention");
            EwuAmount recoverable = V27EwuQuantizer.MultiplyOutputCredit(
                acquisition,
                retention);
            return new V27ItemValue(
                item.ItemId,
                acquisition,
                recoverable,
                selectedSourceId);
        }

        private EwuAmount ScaleInput(float beforeWork)
        {
            decimal beforeDecimal = BalanceCanonicalText.DecimalFromFiniteFloat(
                beforeWork,
                "V23 embedded work");
            return V27EwuQuantizer.QuantizeInputDebit(
                checked(beforeDecimal * laborScale));
        }

        private decimal ResolveAuthoredBefore(
            string stableId,
            string metric,
            float current,
            string context)
        {
            if (authoredBeforeValues != null
                && authoredBeforeValues.TryGetValue(
                    stableId + "\u001f" + metric,
                    out string token))
            {
                return decimal.Parse(
                    token,
                    System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture);
            }
            return BalanceCanonicalText.DecimalFromFiniteFloat(current, context);
        }

        private decimal CalculateStandardLogisticsWork(ProductionRecipeSO recipe)
        {
            decimal totalWeight = 0m;
            foreach (ItemAmountDefinition input in recipe.Inputs)
            {
                if (input == null)
                    continue;
                totalWeight += checked(input.Amount * ResolveWeight(input.ItemId));
            }
            foreach (ProductionOutputDefinition output in recipe.Outputs)
            {
                if (output == null)
                    continue;
                decimal probability = BalanceCanonicalText.DecimalFromFiniteFloat(
                    output.Probability,
                    $"recipe:{recipe.RecipeId}:probability");
                totalWeight += checked(output.Amount * probability * ResolveWeight(output.ItemId));
            }
            decimal squareRoot = DecimalSquareRoot(totalWeight);
            return checked(3m + recipe.Inputs.Count * 0.75m
                + recipe.Outputs.Count * 0.50m + squareRoot * 0.60m);
        }

        private static decimal CalculateInfrastructureWork(
            ProductionRecipeSO recipe,
            decimal directWork)
        {
            decimal rate = recipe.ProcessClass switch
            {
                ProductionProcessClass.Gathering => 0.05m,
                ProductionProcessClass.CuttingGrindingWashing => 0.08m,
                ProductionProcessClass.CookingSimpleMixing => 0.12m,
                ProductionProcessClass.SpinningWeavingWoodworking => 0.10m,
                ProductionProcessClass.ForgingHeavyAssembly => 0.16m,
                ProductionProcessClass.Chemical => 0.18m,
                ProductionProcessClass.Precision => 0.15m,
                ProductionProcessClass.Medical => 0.20m,
                ProductionProcessClass.Rune => 0.25m,
                ProductionProcessClass.HeavyIndustrial => 0.22m,
                _ => 0.10m
            };
            decimal passive = recipe.ProcessKind == ProductionProcessKind.PassiveBatch
                ? BalanceCanonicalText.DecimalFromFiniteFloat(
                    recipe.ProcessingGameHours,
                    $"recipe:{recipe.RecipeId}:processingHours") * 0.25m
                : 0m;
            decimal cleanWater = BalanceCanonicalText.DecimalFromFiniteFloat(
                recipe.CleanWaterPerCycle,
                $"recipe:{recipe.RecipeId}:cleanWater");
            decimal wastewater = BalanceCanonicalText.DecimalFromFiniteFloat(
                recipe.WastewaterPerCycle,
                $"recipe:{recipe.RecipeId}:wastewater");
            return checked(directWork * rate + passive + cleanWater * 0.5m + wastewater * 0.35m);
        }

        private decimal ResolveWeight(string itemId)
        {
            if (!items.TryGetValue(itemId, out ItemDefinitionSO item))
                throw new InvalidOperationException($"Missing item weight authority: {itemId}");
            return BalanceCanonicalText.DecimalFromFiniteFloat(
                item.UnitWeight,
                $"item:{itemId}:unitWeight");
        }

        private static decimal DecimalSquareRoot(decimal value)
        {
            if (value < 0m)
                throw new ArgumentOutOfRangeException(nameof(value));
            if (value == 0m)
                return 0m;
            decimal estimate = value >= 1m ? value : 1m;
            for (int iteration = 0; iteration < 32; iteration++)
                estimate = (estimate + value / estimate) / 2m;
            return estimate;
        }

        private static IReadOnlyDictionary<string, TValue> FreezeMap<TValue>(
            Dictionary<string, TValue> source)
        {
            Dictionary<string, TValue> copy = new Dictionary<string, TValue>(
                source.Count,
                StringComparer.Ordinal);
            foreach (KeyValuePair<string, TValue> pair in source
                         .OrderBy(value => value.Key, StringComparer.Ordinal))
            {
                copy.Add(pair.Key, pair.Value);
            }
            return new System.Collections.ObjectModel.ReadOnlyDictionary<string, TValue>(copy);
        }
    }
}
