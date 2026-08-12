using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

public static class SettlementEquipmentReadinessThroughputDebugScenarios
{
    private const string ReportPath =
        "Artifacts/QA/v26-equipment-readiness-throughput.md";
    private const float WorkerDayWork = 99f;
    private const float GrowthProductionShare = 0.35f;
    private const int MaximumQualityAttempts = 10;

    private sealed class Checkpoint
    {
        public int Day;
        public int WorkingMinimum;
        public int CombatReadyMinimum;
        public string TargetId;
        public CombatEquipmentQuality PartyQuality;
        public string[] PartyEquipmentIds;
        public CombatEquipmentQuality ReadinessQuality;
        public string[] ReadinessEquipmentIds;
    }

    private readonly struct EquipmentCost
    {
        public EquipmentCost(
            CombatEquipmentDefinitionSO definition,
            float directWork,
            float embeddedWork,
            float qualityProbability)
        {
            Definition = definition;
            DirectWork = directWork;
            EmbeddedWork = embeddedWork;
            QualityProbability = qualityProbability;
        }

        public CombatEquipmentDefinitionSO Definition { get; }
        public float DirectWork { get; }
        public float EmbeddedWork { get; }
        public float QualityProbability { get; }
        public float ExpectedAttempts => QualityProbability > 0f
            ? 1f / QualityProbability
            : float.PositiveInfinity;
        public bool HasEmbeddedWork =>
            !float.IsNaN(EmbeddedWork) && !float.IsInfinity(EmbeddedWork);
        public float AcceptanceWithinLimit => QualityProbability <= 0f
            ? 0f
            : 1f - Mathf.Pow(1f - QualityProbability, MaximumQualityAttempts);
    }

    private readonly struct ReadinessCrossover
    {
        public ReadinessCrossover(
            int day,
            int windowDays,
            int newReadyQuantity,
            float dailyGrowthCapacity,
            float ewuPerSet,
            float supplySetsPerDay,
            float demandPeoplePerDay,
            float completionDays,
            float crossoverDay)
        {
            Day = day;
            WindowDays = windowDays;
            NewReadyQuantity = newReadyQuantity;
            DailyGrowthCapacity = dailyGrowthCapacity;
            EwuPerSet = ewuPerSet;
            SupplySetsPerDay = supplySetsPerDay;
            DemandPeoplePerDay = demandPeoplePerDay;
            CompletionDays = completionDays;
            CrossoverDay = crossoverDay;
        }

        public int Day { get; }
        public int WindowDays { get; }
        public int NewReadyQuantity { get; }
        public float DailyGrowthCapacity { get; }
        public float EwuPerSet { get; }
        public float SupplySetsPerDay { get; }
        public float DemandPeoplePerDay { get; }
        public float CompletionDays { get; }
        public float CrossoverDay { get; }
    }

    [MenuItem("DungeonStory/Debug/Balance/Validate Equipment Readiness Throughput")]
    public static void RunFromMenu()
    {
        Debug.Log(Run());
    }

    public static string Run()
    {
        IGameContentCatalog content = new ResourceGameContentCatalog(
            new UnityGameContentRootLoader());
        CombatEquipmentDefinitionSO[] equipment = content
            .GetAll<CombatEquipmentDefinitionSO>()
            .Where(value => value != null)
            .OrderBy(value => value.EquipmentId, StringComparer.Ordinal)
            .ToArray();
        CraftMaterialDefinitionSO[] materials = content
            .GetAll<CraftMaterialDefinitionSO>()
            .Where(value => value != null)
            .OrderBy(value => value.MaterialId, StringComparer.Ordinal)
            .ToArray();
        ProductionRecipeSO[] recipes = content
            .GetAll<ProductionRecipeSO>()
            .Where(value => value != null)
            .OrderBy(value => value.RecipeId, StringComparer.Ordinal)
            .ToArray();
        ResourceMaterialEconomicProfileCatalog materialProfiles = new(content);
        V23BalanceWorkCalculator workCalculator = new(materialProfiles);
        EmbeddedWorkValueSnapshot embeddedWork =
            new V23EmbeddedWorkValueCalculator(
                recipes,
                content.Items.Definitions,
                equipment,
                materials,
                workCalculator)
            .Calculate();
        Dictionary<string, ProductionRecipeSO> cheapestRecipeByOutput =
            ResolveCheapestRecipesByOutput(recipes, embeddedWork);
        ResourceResearchProjectCatalog research = new(content);
        IOffenseCampaignCatalog campaign =
            OffenseEditorTestDependencies.CreateCampaignCatalog();
        V26FounderIndustryBalanceDebugScenarios.FounderIndustryBaseline founderBaseline =
            V26FounderIndustryBalanceDebugScenarios.MeasureNaturalFounderBaseline();
        Dictionary<string, CombatEquipmentDefinitionSO> equipmentById = equipment
            .ToDictionary(value => value.EquipmentId, StringComparer.Ordinal);
        Dictionary<string, CraftMaterialDefinitionSO> materialsById = materials
            .ToDictionary(value => value.MaterialId, StringComparer.Ordinal);
        List<string> failures = new();
        StringBuilder report = new(12288);

        report.AppendLine("# V26 equipment production and combat-readiness throughput");
        report.AppendLine();
        report.AppendLine(
            "This audit uses live physical BOM, direct craft work, embedded work, "
            + "research prerequisites and deterministic quality probabilities. "
            + "It does not create equipment or infer costs from campaign power.");
        report.AppendLine(
            "The expedition kit is the authored contemporary party loadout. The readiness kit "
            + "is the minimum weapon and protection already accepted by the day-1 readiness authority; "
            + "new reserve slots are not silently upgraded to expedition-grade equipment.");
        report.AppendLine(
            $"Founder input is the deterministic 10,000-party natural distribution: "
            + $"industry speed-sum {founderBaseline.AssignmentSpeed:0.000}, "
            + $"best craft {founderBaseline.CraftSpeed:0.000}, "
            + $"best research {founderBaseline.ResearchSpeed:0.000}. "
            + "Later workers are conservatively neutral x1.00; founder proficiency growth is not credited.");
        report.AppendLine();
        report.AppendLine("## Checkpoint throughput");
        report.AppendLine();
        report.AppendLine(
            "The period capacity is a conservative floor: the natural founders' measured industry speed sum, "
            + $"plus neutral additional workers, × {SettlementLaborBalanceRules.BaselineWuPerAdultDay:0.##} WU/day × the baseline 35% growth/production share. "
            + "Quality-adjusted EWU is a gross upper envelope because rejected-output salvage "
            + "is not credited here.");
        report.AppendLine();
        report.AppendLine(
            "| Day | Playtime | Window | Crafter rank | Party quality and direct / gross EWU | "
            + "Party qty / growth share | Ready quality and direct / gross EWU | New-ready qty / growth share | "
            + "Full reserve qty / growth share | Research WU / isolated days | Status |");
        report.AppendLine(
            "|---:|---:|---:|---|---:|---:|---:|---:|---:|---|");

        Checkpoint[] checkpoints = CreateCheckpoints();
        List<ReadinessCrossover> readinessCrossovers = new(checkpoints.Length);
        int previousDay = 0;
        int previousWorkingMinimum = checkpoints[0].WorkingMinimum;
        int previousCombatReadyMinimum = 0;
        foreach (Checkpoint checkpoint in checkpoints)
        {
            CharacterProficiencyRank rank = ResolveSpecialistRank(checkpoint.Day);
            List<EquipmentCost> partyCosts = ResolveCosts(
                checkpoint.Day,
                "party",
                checkpoint.PartyEquipmentIds,
                checkpoint.PartyQuality,
                rank,
                equipmentById,
                materialsById,
                workCalculator,
                embeddedWork,
                failures);
            List<EquipmentCost> readinessCosts = ResolveCosts(
                checkpoint.Day,
                "readiness",
                checkpoint.ReadinessEquipmentIds,
                checkpoint.ReadinessQuality,
                rank,
                equipmentById,
                materialsById,
                workCalculator,
                embeddedWork,
                failures);
            (float partyDirectPerSet, float partyEwuPerSet) =
                SumQualityAdjusted(partyCosts);
            (float readinessDirectPerSet, float readinessEwuPerSet) =
                SumQualityAdjusted(readinessCosts);
            int windowDays = checkpoint.Day == 1
                ? 0
                : checkpoint.Day - previousDay;
            float effectiveIndustryWorkers = founderBaseline.AssignmentSpeed
                + Math.Max(0, previousWorkingMinimum - 3);
            float growthCapacity = windowDays <= 0
                ? 0f
                : effectiveIndustryWorkers
                    * WorkerDayWork
                    * windowDays
                    * GrowthProductionShare;
            float specialistCapacity = windowDays <= 0
                ? 0f
                : founderBaseline.CraftSpeed
                    * WorkerDayWork
                    * windowDays;
            OffenseTargetDefinition target = RequireTarget(
                campaign,
                checkpoint.TargetId);
            int partyQuantity = target.requiredMembers;
            int newReadyQuantity = Math.Max(
                0,
                checkpoint.CombatReadyMinimum - previousCombatReadyMinimum);
            int reserveQuantity = checkpoint.CombatReadyMinimum;
            float dailyGrowthCapacity = effectiveIndustryWorkers
                * WorkerDayWork
                * GrowthProductionShare;
            float supplySetsPerDay = readinessEwuPerSet > 0f
                && !float.IsNaN(readinessEwuPerSet)
                && !float.IsInfinity(readinessEwuPerSet)
                ? dailyGrowthCapacity / readinessEwuPerSet
                : 0f;
            float demandPeoplePerDay = windowDays > 0
                ? newReadyQuantity / (float)windowDays
                : 0f;
            float completionDays = supplySetsPerDay > 0f
                ? newReadyQuantity / supplySetsPerDay
                : float.PositiveInfinity;
            float crossoverDay = checkpoint.Day == 1
                ? 1f
                : previousDay + completionDays;
            readinessCrossovers.Add(new ReadinessCrossover(
                checkpoint.Day,
                windowDays,
                newReadyQuantity,
                dailyGrowthCapacity,
                readinessEwuPerSet,
                supplySetsPerDay,
                demandPeoplePerDay,
                completionDays,
                crossoverDay));

            if (checkpoint.Day > 1
                && partyCosts.Count == checkpoint.PartyEquipmentIds.Length)
            {
                ValidateEnvelope(
                    checkpoint.Day,
                    "party",
                    partyQuantity,
                    partyDirectPerSet,
                    partyEwuPerSet,
                    specialistCapacity,
                    growthCapacity,
                    failures);
            }
            if (checkpoint.Day > 1
                && readinessCosts.Count == checkpoint.ReadinessEquipmentIds.Length)
            {
                ValidateEnvelope(
                    checkpoint.Day,
                    "new-ready",
                    newReadyQuantity,
                    readinessDirectPerSet,
                    readinessEwuPerSet,
                    specialistCapacity,
                    growthCapacity,
                    failures);
            }

            HashSet<string> manufacturingResearchIds = new(StringComparer.Ordinal);
            HashSet<string> visitedManufacturingItems = new(StringComparer.Ordinal);
            foreach (EquipmentCost cost in partyCosts.Concat(readinessCosts))
            {
                AddEquipmentManufacturingResearch(
                    cost.Definition,
                    materialsById,
                    cheapestRecipeByOutput,
                    manufacturingResearchIds,
                    visitedManufacturingItems);
            }
            (float researchWork, float researchDays) = ResolveResearchBurden(
                manufacturingResearchIds,
                research,
                failures,
                checkpoint.Day,
                founderBaseline.ResearchSpeed * WorkerDayWork);
            if (checkpoint.Day > 1 && researchDays > checkpoint.Day + 0.001f)
            {
                failures.Add(
                    $"Day {checkpoint.Day}: isolated research burden "
                    + $"{researchDays:0.0} days exceeds the checkpoint.");
            }

            string status = failures.Any(value => value.StartsWith(
                $"Day {checkpoint.Day}:",
                StringComparison.Ordinal))
                ? "FAIL"
                : checkpoint.Day == 1 ? "STARTING STOCK" : "PASS";
            report.Append("| ").Append(checkpoint.Day)
                .Append(" | ").Append(FormatPlaytime(checkpoint.Day))
                .Append(" | ").Append(windowDays == 0 ? "start" : windowDays.ToString())
                .Append(" | ").Append(rank)
                .Append(" | ").Append(checkpoint.PartyQuality).Append(' ')
                .Append(FormatPair(partyDirectPerSet, partyEwuPerSet))
                .Append(" | ").Append(FormatEnvelope(
                    partyQuantity,
                    partyEwuPerSet,
                    growthCapacity))
                .Append(" | ").Append(checkpoint.ReadinessQuality).Append(' ')
                .Append(FormatPair(readinessDirectPerSet, readinessEwuPerSet))
                .Append(" | ").Append(FormatEnvelope(
                    newReadyQuantity,
                    readinessEwuPerSet,
                    growthCapacity))
                .Append(" | ").Append(FormatEnvelope(
                    reserveQuantity,
                    readinessEwuPerSet,
                    growthCapacity))
                .Append(" | ").Append(researchWork.ToString("0.#", CultureInfo.InvariantCulture))
                .Append(" / ").Append(researchDays.ToString("0.0", CultureInfo.InvariantCulture))
                .Append(" | ").Append(status).AppendLine(" |");

            previousDay = checkpoint.Day;
            previousWorkingMinimum = checkpoint.WorkingMinimum;
            previousCombatReadyMinimum = checkpoint.CombatReadyMinimum;
        }

        report.AppendLine();
        report.AppendLine("## Minimum readiness demand crossover");
        report.AppendLine();
        report.AppendLine(
            "Supply is the minimum readiness sets producible per day from the same 35% growth-production allocation. "
            + "Demand is the increase in the lower-bound combat-ready target divided by the checkpoint window. "
            + "Crossover day is the first absolute day on which that window's new minimum kits can be completed if production starts at the previous checkpoint.");
        report.AppendLine();
        report.AppendLine(
            "| Target day | Window | New ready people | Growth EWU/day | Readiness EWU/set | Supply sets/day | Demand people/day | Supply / demand | Completion days | First crossover | Playtime at crossover |");
        report.AppendLine(
            "|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|");
        foreach (ReadinessCrossover crossover in readinessCrossovers)
        {
            bool startingStock = crossover.WindowDays <= 0;
            float headroom = crossover.DemandPeoplePerDay > 0f
                ? crossover.SupplySetsPerDay / crossover.DemandPeoplePerDay
                : float.PositiveInfinity;
            report.Append("| ").Append(crossover.Day)
                .Append(" | ").Append(startingStock ? "start" : crossover.WindowDays.ToString())
                .Append(" | ").Append(crossover.NewReadyQuantity)
                .Append(" | ").Append(F(crossover.DailyGrowthCapacity))
                .Append(" | ").Append(F(crossover.EwuPerSet))
                .Append(" | ").Append(F(crossover.SupplySetsPerDay))
                .Append(" | ").Append(F(crossover.DemandPeoplePerDay))
                .Append(" | ").Append(startingStock
                    ? "starting stock"
                    : float.IsInfinity(headroom) ? "no new demand" : F(headroom) + "x")
                .Append(" | ").Append(startingStock ? "start" : F(crossover.CompletionDays))
                .Append(" | ").Append(startingStock ? "Day 1" : "Day " + F(crossover.CrossoverDay))
                .Append(" | ").Append(startingStock
                    ? FormatPlaytime(1f)
                    : FormatPlaytime(crossover.CrossoverDay))
                .AppendLine(" |");
        }

        report.AppendLine();
        report.AppendLine("## Equipment detail");
        report.AppendLine();
        report.AppendLine(
            "| Day | Purpose | Target quality | Equipment | Material and components | Direct WU | EWU | "
            + "Single attempt | Within 10 | Research |");
        report.AppendLine("|---:|---|---|---|---|---:|---:|---:|---:|---|");
        foreach (Checkpoint checkpoint in checkpoints)
        {
            CharacterProficiencyRank rank = ResolveSpecialistRank(checkpoint.Day);
            (string Purpose, CombatEquipmentQuality Quality, string[] EquipmentIds)[] kits =
            {
                ("party", checkpoint.PartyQuality, checkpoint.PartyEquipmentIds),
                ("readiness", checkpoint.ReadinessQuality, checkpoint.ReadinessEquipmentIds)
            };
            foreach ((string purpose, CombatEquipmentQuality quality, string[] equipmentIds) in kits)
            {
                if (purpose == "readiness"
                    && quality == checkpoint.PartyQuality
                    && equipmentIds.SequenceEqual(checkpoint.PartyEquipmentIds))
                {
                    continue;
                }
                foreach (string equipmentId in equipmentIds)
                {
                    if (!equipmentById.TryGetValue(equipmentId, out CombatEquipmentDefinitionSO definition)
                        || !materialsById.TryGetValue(
                            definition.DefaultMaterialId,
                            out CraftMaterialDefinitionSO material))
                    {
                        continue;
                    }
                    bool hasItemEwu = embeddedWork.TryGetItemWork(
                        definition.ItemId,
                        out float itemEwu);
                    float direct = workCalculator.CalculateEquipment(definition, material.ItemId);
                    float probability = CalculateQualityProbability(rank, direct, quality);
                    string inputs = material.ItemId + " x " + definition.PrimaryMaterialAmount;
                    if (definition.RequiredComponentInputs.Count > 0)
                    {
                        inputs += "; " + string.Join(
                            ", ",
                            definition.RequiredComponentInputs.Select(value =>
                                value.ItemId + " x " + value.Amount));
                    }
                    report.Append("| ").Append(checkpoint.Day)
                        .Append(" | ").Append(purpose)
                        .Append(" | ").Append(quality)
                        .Append(" | ").Append(definition.EquipmentId)
                        .Append(" (item ").Append(definition.ItemId).Append(")")
                        .Append(" | ").Append(inputs)
                        .Append(" | ").Append(direct.ToString("0.#", CultureInfo.InvariantCulture))
                        .Append(" | ").Append(hasItemEwu
                            ? itemEwu.ToString("0.#", CultureInfo.InvariantCulture)
                            : "unresolved")
                        .Append(" | ").Append((probability * 100f).ToString("0.0", CultureInfo.InvariantCulture))
                        .Append("% | ").Append(((1f - Mathf.Pow(
                            1f - probability,
                            MaximumQualityAttempts)) * 100f).ToString("0.0", CultureInfo.InvariantCulture))
                        .Append("% | ").Append(string.IsNullOrWhiteSpace(definition.RequiredResearchId)
                            ? "none"
                            : definition.RequiredResearchId)
                        .AppendLine(" |");
                }
            }
        }

        report.AppendLine();
        report.AppendLine("## Interpretation guardrails");
        report.AppendLine();
        report.AppendLine(
            "- Day-1 equipment is a starting-stock condition; this audit does not pretend it was crafted before play begins.");
        report.AppendLine(
            "- The party envelope answers whether the authored minimum expedition party can receive the contemporary set. "
            + "The new-ready envelope uses the minimum day-1 readiness kit instead of silently giving every reserve the latest party set. "
            + "The full-reserve envelope is a deliberately conservative minimum-kit pressure indicator and is not a pass gate.");
        report.AppendLine(
            "- Old equipment remains usable physical reserve stock. No upgrade, deletion, salvage or sale value is credited automatically.");
        report.AppendLine(
            "- Gross quality EWU assumes a fresh full input on each rejected attempt. Runtime auto-dismantle can reduce net material cost, "
            + "but direct craft time and player attention remain real; a later live production simulation must measure the net value.");
        report.AppendLine(
            "- Research days are an isolated one-researcher lower bound over the de-duplicated prerequisite closure of equipment, "
            + "primary materials and every cheapest-EWU upstream production recipe. "
            + "They do not include competing survival, industry or medical research.");

        report.AppendLine();
        report.AppendLine("## Failures");
        report.AppendLine();
        if (failures.Count == 0)
        {
            report.AppendLine("- none");
        }
        else
        {
            foreach (string failure in failures)
            {
                report.Append("- ").AppendLine(failure);
            }
        }

        string absolutePath = Path.GetFullPath(ReportPath);
        Directory.CreateDirectory(Path.GetDirectoryName(absolutePath)
            ?? throw new InvalidOperationException("Throughput report directory is unavailable."));
        File.WriteAllText(absolutePath, report.ToString(), new UTF8Encoding(false));
        AssetDatabase.Refresh();
        if (failures.Count > 0)
        {
            throw new InvalidOperationException(
                $"Equipment-readiness throughput audit failed with {failures.Count} issue(s). "
                + $"See {ReportPath}.");
        }
        return $"PASS: {checkpoints.Length} equipment-readiness checkpoints -> {ReportPath}";
    }

    private static string FormatPlaytime(float absoluteDay)
    {
        float hours = Mathf.Max(0, absoluteDay) * GameCalendarRules.SecondsPerDay / 3600f;
        return hours < 1f
            ? $"{hours * 60f:0}m"
            : $"{hours:0.#}h";
    }

    private static string F(float value) =>
        float.IsNaN(value) || float.IsInfinity(value)
            ? "unresolved"
            : value.ToString("0.###", CultureInfo.InvariantCulture);

    private static List<EquipmentCost> ResolveCosts(
        int day,
        string purpose,
        IEnumerable<string> equipmentIds,
        CombatEquipmentQuality quality,
        CharacterProficiencyRank rank,
        IReadOnlyDictionary<string, CombatEquipmentDefinitionSO> equipmentById,
        IReadOnlyDictionary<string, CraftMaterialDefinitionSO> materialsById,
        IBalanceWorkCalculator workCalculator,
        EmbeddedWorkValueSnapshot embeddedWork,
        ICollection<string> failures)
    {
        List<EquipmentCost> costs = new();
        foreach (string equipmentId in equipmentIds ?? Array.Empty<string>())
        {
            if (!equipmentById.TryGetValue(
                    equipmentId,
                    out CombatEquipmentDefinitionSO definition))
            {
                failures.Add(
                    $"Day {day}: {purpose} missing equipment '{equipmentId}'.");
                continue;
            }
            if (!materialsById.TryGetValue(
                    definition.DefaultMaterialId,
                    out CraftMaterialDefinitionSO material))
            {
                failures.Add(
                    $"Day {day}: {purpose} equipment '{equipmentId}' has no default material.");
                continue;
            }
            bool hasEquipmentEmbeddedWork = embeddedWork.TryGetItemWork(
                definition.ItemId,
                out float equipmentEmbeddedWork);
            if (!hasEquipmentEmbeddedWork)
            {
                failures.Add(
                    $"Day {day}: {purpose} equipment '{equipmentId}' item "
                    + $"'{definition.ItemId}' has no embedded work.");
                equipmentEmbeddedWork = float.NaN;
            }

            float directWork = workCalculator.CalculateEquipment(
                definition,
                material.ItemId);
            float qualityProbability = CalculateQualityProbability(
                rank,
                directWork,
                quality);
            costs.Add(new EquipmentCost(
                definition,
                directWork,
                equipmentEmbeddedWork,
                qualityProbability));
            if (qualityProbability <= 0f)
            {
                failures.Add(
                    $"Day {day}: {purpose} '{equipmentId}' cannot reach "
                    + $"{quality} with the projected {rank} specialist.");
            }
            else if (1f - Mathf.Pow(
                         1f - qualityProbability,
                         MaximumQualityAttempts) < 0.50f)
            {
                failures.Add(
                    $"Day {day}: {purpose} '{equipmentId}' has under 50% acceptance "
                    + $"within {MaximumQualityAttempts} attempts at {quality}.");
            }
        }
        return costs;
    }

    private static (float Direct, float Ewu) SumQualityAdjusted(
        IReadOnlyCollection<EquipmentCost> costs)
    {
        float direct = costs.Sum(value =>
            value.QualityProbability > 0f
                ? value.DirectWork * value.ExpectedAttempts
                : 0f);
        float ewu = costs.Any(value => !value.HasEmbeddedWork)
            ? float.NaN
            : costs.Sum(value =>
                value.QualityProbability > 0f
                    ? value.EmbeddedWork * value.ExpectedAttempts
                    : 0f);
        return (direct, ewu);
    }

    private static Dictionary<string, ProductionRecipeSO> ResolveCheapestRecipesByOutput(
        IEnumerable<ProductionRecipeSO> recipes,
        EmbeddedWorkValueSnapshot embeddedWork)
    {
        Dictionary<string, ProductionRecipeSO> result = new(StringComparer.Ordinal);
        foreach (ProductionRecipeSO recipe in recipes ?? Array.Empty<ProductionRecipeSO>())
        {
            if (recipe == null
                || !embeddedWork.Recipes.TryGetValue(
                    recipe.RecipeId,
                    out EmbeddedWorkValueRecipeBreakdown candidate))
            {
                continue;
            }
            foreach (ProductionOutputDefinition output in recipe.Outputs)
            {
                string itemId = output?.ItemId?.Trim() ?? string.Empty;
                if (itemId.Length == 0)
                {
                    continue;
                }
                if (!result.TryGetValue(itemId, out ProductionRecipeSO current)
                    || !embeddedWork.Recipes.TryGetValue(
                        current.RecipeId,
                        out EmbeddedWorkValueRecipeBreakdown currentBreakdown)
                    || candidate.OutputUnitWork < currentBreakdown.OutputUnitWork - 0.001f
                    || (Mathf.Abs(candidate.OutputUnitWork - currentBreakdown.OutputUnitWork) <= 0.001f
                        && string.CompareOrdinal(recipe.RecipeId, current.RecipeId) < 0))
                {
                    result[itemId] = recipe;
                }
            }
        }
        return result;
    }

    private static void AddEquipmentManufacturingResearch(
        CombatEquipmentDefinitionSO definition,
        IReadOnlyDictionary<string, CraftMaterialDefinitionSO> materialsById,
        IReadOnlyDictionary<string, ProductionRecipeSO> cheapestRecipeByOutput,
        ISet<string> researchIds,
        ISet<string> visitedItems)
    {
        if (definition == null)
        {
            return;
        }
        AddResearchId(definition.RequiredResearchId, researchIds);
        if (materialsById.TryGetValue(
                definition.DefaultMaterialId,
                out CraftMaterialDefinitionSO material))
        {
            AddManufacturingResearch(
                material.ItemId,
                cheapestRecipeByOutput,
                researchIds,
                visitedItems);
        }
        foreach (ItemAmountDefinition input in definition.RequiredComponentInputs)
        {
            AddManufacturingResearch(
                input?.ItemId,
                cheapestRecipeByOutput,
                researchIds,
                visitedItems);
        }
    }

    private static void AddManufacturingResearch(
        string itemId,
        IReadOnlyDictionary<string, ProductionRecipeSO> cheapestRecipeByOutput,
        ISet<string> researchIds,
        ISet<string> visitedItems)
    {
        string normalized = itemId?.Trim() ?? string.Empty;
        if (normalized.Length == 0 || !visitedItems.Add(normalized)
            || !cheapestRecipeByOutput.TryGetValue(
                normalized,
                out ProductionRecipeSO recipe))
        {
            return;
        }
        AddResearchId(recipe.RequiredResearchId, researchIds);
        foreach (ItemAmountDefinition input in recipe.Inputs)
        {
            AddManufacturingResearch(
                input?.ItemId,
                cheapestRecipeByOutput,
                researchIds,
                visitedItems);
        }
    }

    private static void AddResearchId(string researchId, ISet<string> researchIds)
    {
        if (!string.IsNullOrWhiteSpace(researchId))
        {
            researchIds.Add(researchId.Trim());
        }
    }

    private static void ValidateEnvelope(
        int day,
        string label,
        int quantity,
        float directPerSet,
        float ewuPerSet,
        float specialistCapacity,
        float growthCapacity,
        ICollection<string> failures)
    {
        if (quantity <= 0)
        {
            return;
        }
        float direct = directPerSet * quantity;
        float ewu = ewuPerSet * quantity;
        if (direct > specialistCapacity + 0.01f)
        {
            failures.Add(
                $"Day {day}: {label} direct craft work {direct:0.0} exceeds "
                + $"one specialist capacity {specialistCapacity:0.0}.");
        }
        if (!float.IsNaN(ewu) && !float.IsInfinity(ewu)
            && ewu > growthCapacity + 0.01f)
        {
            failures.Add(
                $"Day {day}: {label} gross equipment EWU {ewu:0.0} exceeds "
                + $"the lower growth/production capacity {growthCapacity:0.0}.");
        }
    }

    private static float CalculateQualityProbability(
        CharacterProficiencyRank rank,
        float directWork,
        CombatEquipmentQuality target)
    {
        int successes = 0;
        DeterministicCraftQualityResolver resolver = new();
        float skill = ProficiencyProgressionRules.ResolveQualityScore(rank);
        float complexity = Mathf.Clamp(directWork / 20f, 0f, 25f);
        for (int a = -10; a <= 10; a++)
        for (int b = -10; b <= 10; b++)
        for (int c = -10; c <= 10; c++)
        {
            CraftQualityResolution resolution = resolver.Resolve(
                new CraftQualityRollSaveData
                {
                    randomA = a,
                    randomB = b,
                    randomC = c
                },
                skill,
                0f,
                0f,
                complexity);
            if ((int)resolution.Tier >= (int)target)
            {
                successes++;
            }
        }
        return successes / 9261f;
    }

    private static CharacterProficiencyRank ResolveSpecialistRank(int day)
    {
        float experience = 30f
            + Math.Max(0, day - 1)
            * WorkerDayWork
            * ProficiencyProgressionRules.ExperiencePerApprovedWork;
        long milliExperience = checked((long)Math.Round(
            experience * ProficiencyProgressionRules.MilliPerExperience,
            MidpointRounding.AwayFromZero));
        return ProficiencyProgressionRules.ResolveRank(milliExperience);
    }

    private static (float Work, float Days) ResolveResearchBurden(
        IEnumerable<string> researchIds,
        IResearchProjectCatalog catalog,
        ICollection<string> failures,
        int checkpointDay,
        float researchWorkPerDay)
    {
        HashSet<ResearchProjectSO> closure = new();
        foreach (string researchId in (researchIds ?? Array.Empty<string>())
                     .Where(value => !string.IsNullOrWhiteSpace(value))
                     .Distinct(StringComparer.Ordinal))
        {
            ResearchProjectId projectId = new(researchId);
            if (!catalog.TryGet(projectId, out ResearchProjectSO project))
            {
                failures.Add(
                    $"Day {checkpointDay}: missing research '{researchId}'.");
                continue;
            }
            AddResearchClosure(project, closure);
        }
        float work = closure.Sum(value => Mathf.Max(0f, value.RequiredWork));
        return (work, work / Mathf.Max(.01f, researchWorkPerDay));
    }

    private static void AddResearchClosure(
        ResearchProjectSO project,
        ISet<ResearchProjectSO> closure)
    {
        if (project == null || !closure.Add(project))
        {
            return;
        }
        foreach (ResearchProjectSO prerequisite in project.Prerequisites
                     ?? Array.Empty<ResearchProjectSO>())
        {
            AddResearchClosure(prerequisite, closure);
        }
    }

    private static OffenseTargetDefinition RequireTarget(
        IOffenseCampaignCatalog catalog,
        string targetId)
    {
        if (catalog == null || !catalog.TryGet(targetId, out OffenseTargetDefinition target))
        {
            throw new InvalidOperationException(
                $"Missing authored campaign target '{targetId}'.");
        }
        return target;
    }

    private static string FormatPair(float direct, float ewu) =>
        direct.ToString("0.#", CultureInfo.InvariantCulture)
        + " / "
        + (float.IsNaN(ewu) || float.IsInfinity(ewu)
            ? "unresolved"
            : ewu.ToString("0.#", CultureInfo.InvariantCulture));

    private static string FormatEnvelope(
        int quantity,
        float ewuPerSet,
        float capacity)
    {
        if (capacity <= 0f)
        {
            return quantity + " / start";
        }
        if (float.IsNaN(ewuPerSet) || float.IsInfinity(ewuPerSet))
        {
            return quantity + " / unresolved";
        }
        float share = quantity * ewuPerSet / capacity * 100f;
        return quantity + " / " + share.ToString("0.0", CultureInfo.InvariantCulture) + "%";
    }

    private static Checkpoint[] CreateCheckpoints() => new[]
    {
        Point(1, 3, 2, "food_farm", CombatEquipmentQuality.Normal,
            new[] { "weapon:spear", "armor:cloth-hood" }),
        Point(30, 3, 2, "merchant_road", CombatEquipmentQuality.Normal,
            new[] { "weapon:falchion", "armor:leather", "shield:wood" }),
        Point(120, 5, 3, "old_armory", CombatEquipmentQuality.Normal,
            new[] { "weapon:mace", "armor:mail-shirt", "shield:wood" }),
        Point(240, 8, 5, "mana_ruins", CombatEquipmentQuality.Good,
            new[] { "weapon:estoc", "armor:articulated-plate", "shield:iron" }),
        Point(400, 15, 10, "rival_dungeon", CombatEquipmentQuality.Good,
            new[] { "weapon:powered-striking-gauntlet", "armor:powered-harness", "shield:powered" }),
        Point(960, 55, 25, "truth_core", CombatEquipmentQuality.Excellent,
            new[] { "weapon:rune-blade", "armor:rune-ward-mail", "shield:rune" })
    };

    private static Checkpoint Point(
        int day,
        int workingMinimum,
        int combatReadyMinimum,
        string targetId,
        CombatEquipmentQuality partyQuality,
        string[] partyEquipmentIds) => new()
    {
        Day = day,
        WorkingMinimum = workingMinimum,
        CombatReadyMinimum = combatReadyMinimum,
        TargetId = targetId,
        PartyQuality = partyQuality,
        PartyEquipmentIds = partyEquipmentIds ?? Array.Empty<string>(),
        ReadinessQuality = CombatEquipmentQuality.Normal,
        ReadinessEquipmentIds = new[] { "weapon:spear", "armor:cloth-hood" }
    };
}
