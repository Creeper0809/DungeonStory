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
    private const float GrowthProductionShare = 0.37f;
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
            float qualityProbability,
            float rejectedDismantleWork,
            float rejectedRecoveryCredit)
        {
            Definition = definition;
            DirectWork = directWork;
            EmbeddedWork = embeddedWork;
            QualityProbability = qualityProbability;
            RejectedDismantleWork = rejectedDismantleWork;
            RejectedRecoveryCredit = rejectedRecoveryCredit;
        }

        public CombatEquipmentDefinitionSO Definition { get; }
        public float DirectWork { get; }
        public float EmbeddedWork { get; }
        public float QualityProbability { get; }
        public float RejectedDismantleWork { get; }
        public float RejectedRecoveryCredit { get; }
        public float ExpectedAttempts => QualityProbability > 0f
            ? 1f / QualityProbability
            : float.PositiveInfinity;
        public float ExpectedRejectedAttempts => QualityProbability > 0f
            ? (1f - QualityProbability) / QualityProbability
            : float.PositiveInfinity;
        public float ExpectedDirectWork => QualityProbability > 0f
            ? DirectWork * ExpectedAttempts
                + RejectedDismantleWork * ExpectedRejectedAttempts
            : float.PositiveInfinity;
        public float GrossExpectedEwu => QualityProbability > 0f
            ? EmbeddedWork * ExpectedAttempts
            : float.PositiveInfinity;
        public float NetExpectedEwu => QualityProbability > 0f
            ? GrossExpectedEwu
                + RejectedDismantleWork * ExpectedRejectedAttempts
                - RejectedRecoveryCredit * ExpectedRejectedAttempts
            : float.PositiveInfinity;
        public bool HasEmbeddedWork =>
            !float.IsNaN(EmbeddedWork) && !float.IsInfinity(EmbeddedWork)
            && !float.IsNaN(RejectedRecoveryCredit)
            && !float.IsInfinity(RejectedRecoveryCredit);
        public float AcceptanceWithinLimit => QualityProbability <= 0f
            ? 0f
            : 1f - Mathf.Pow(1f - QualityProbability, MaximumQualityAttempts);
    }

    private readonly struct EquipmentCostSummary
    {
        public EquipmentCostSummary(float direct, float grossEwu, float netEwu)
        {
            Direct = direct;
            GrossEwu = grossEwu;
            NetEwu = netEwu;
        }

        public float Direct { get; }
        public float GrossEwu { get; }
        public float NetEwu { get; }
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
        V23MaterialSalvageCalculator salvageCalculator = new(materialProfiles);
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
            + $"plus neutral additional workers, × {SettlementLaborAuthority.EffectiveOutputWuPerAdultDay:0.##} effective WU/day × the V27 37% equipment growth/production share. "
            + "Quality-adjusted EWU reports both gross fresh-input pressure and production-exact "
            + "net pressure after rejected-output dismantle work and recovered physical inputs.");
        report.AppendLine();
        report.AppendLine(
            "| Day | Playtime | Window | Crafter rank | Party quality and direct / gross / net EWU | "
            + "Party qty / net growth share | Ready quality and direct / gross / net EWU | New-ready qty / net growth share | "
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
            TechnologyWuCheckpoint laborCheckpoint = ResolveWindowLaborCheckpoint(
                checkpoint.Day);
            float actualWorkerDayWork = laborCheckpoint.ActualLaborWu;
            float effectiveWorkerDayWork = laborCheckpoint.OutputEquivalentWu;
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
                salvageCalculator,
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
                salvageCalculator,
                failures);
            EquipmentCostSummary partySummary = SumQualityAdjusted(partyCosts);
            EquipmentCostSummary readinessSummary = SumQualityAdjusted(readinessCosts);
            int windowDays = checkpoint.Day == 1
                ? 0
                : checkpoint.Day - previousDay;
            float effectiveIndustryWorkers = founderBaseline.AssignmentSpeed
                + Math.Max(0, previousWorkingMinimum - 3);
            float growthCapacity = windowDays <= 0
                ? 0f
                : effectiveIndustryWorkers
                    * effectiveWorkerDayWork
                    * windowDays
                    * GrowthProductionShare;
            float specialistCapacity = windowDays <= 0
                ? 0f
                : founderBaseline.CraftSpeed
                    * actualWorkerDayWork
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
                * effectiveWorkerDayWork
                * GrowthProductionShare;
            float supplySetsPerDay = readinessSummary.NetEwu > 0f
                && !float.IsNaN(readinessSummary.NetEwu)
                && !float.IsInfinity(readinessSummary.NetEwu)
                ? dailyGrowthCapacity / readinessSummary.NetEwu
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
                readinessSummary.NetEwu,
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
                    partySummary.Direct,
                    partySummary.NetEwu,
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
                    readinessSummary.Direct,
                    readinessSummary.NetEwu,
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
                founderBaseline.ResearchSpeed * effectiveWorkerDayWork);
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
                .Append(FormatSummary(partySummary))
                .Append(" | ").Append(FormatEnvelope(
                    partyQuantity,
                    partySummary.NetEwu,
                    growthCapacity))
                .Append(" | ").Append(checkpoint.ReadinessQuality).Append(' ')
                .Append(FormatSummary(readinessSummary))
                .Append(" | ").Append(FormatEnvelope(
                    newReadyQuantity,
                    readinessSummary.NetEwu,
                    growthCapacity))
                .Append(" | ").Append(FormatEnvelope(
                    reserveQuantity,
                    readinessSummary.NetEwu,
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
            "Supply is the minimum readiness sets producible per day from the same 37% equipment growth-production allocation. "
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
            "| Day | Purpose | Target quality | Equipment | Material and components | Direct WU | Item EWU | "
            + "Single attempt | Expected attempts / rejects | Rejected recovery EWU / dismantle WU | Net expected EWU | Within 10 | Research |");
        report.AppendLine("|---:|---|---|---|---|---:|---:|---:|---:|---:|---:|---:|---|");
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
                    (float recoveryCredit, float dismantleWork) = ResolveRejectedRecovery(
                        definition,
                        material,
                        direct,
                        rank,
                        embeddedWork,
                        salvageCalculator,
                        failures,
                        checkpoint.Day,
                        purpose);
                    float expectedAttempts = probability > 0f
                        ? 1f / probability
                        : float.PositiveInfinity;
                    float expectedRejects = probability > 0f
                        ? (1f - probability) / probability
                        : float.PositiveInfinity;
                    float netExpectedEwu = hasItemEwu && probability > 0f
                        ? itemEwu * expectedAttempts
                            + dismantleWork * expectedRejects
                            - recoveryCredit * expectedRejects
                        : float.NaN;
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
                        .Append("% | ").Append(F(expectedAttempts)).Append(" / ").Append(F(expectedRejects))
                        .Append(" | ").Append(F(recoveryCredit)).Append(" / ").Append(F(dismantleWork))
                        .Append(" | ").Append(F(netExpectedEwu))
                        .Append(" | ").Append(((1f - Mathf.Pow(
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
            "- Gross quality EWU assumes a fresh full input on each rejected attempt. Net expected EWU uses the same production "
            + "V23MaterialSalvageCalculator, rank-derived relevant skill, Floor recovery quantities and 25% rejected dismantle WU. "
            + "Recovered inputs reduce only material acquisition pressure; craft and dismantle labor remain real.");
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
        IMaterialSalvageCalculator salvageCalculator,
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
            (float recoveryCredit, float dismantleWork) = ResolveRejectedRecovery(
                definition,
                material,
                directWork,
                rank,
                embeddedWork,
                salvageCalculator,
                failures,
                day,
                purpose);
            costs.Add(new EquipmentCost(
                definition,
                directWork,
                equipmentEmbeddedWork,
                qualityProbability,
                dismantleWork,
                recoveryCredit));
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

    private static EquipmentCostSummary SumQualityAdjusted(
        IReadOnlyCollection<EquipmentCost> costs)
    {
        float direct = costs.Sum(value => value.ExpectedDirectWork);
        float grossEwu = costs.Any(value => !value.HasEmbeddedWork)
            ? float.NaN
            : costs.Sum(value => value.GrossExpectedEwu);
        float netEwu = costs.Any(value => !value.HasEmbeddedWork)
            ? float.NaN
            : costs.Sum(value => value.NetExpectedEwu);
        return new EquipmentCostSummary(direct, grossEwu, netEwu);
    }

    private static (float RecoveryCredit, float DismantleWork) ResolveRejectedRecovery(
        CombatEquipmentDefinitionSO definition,
        CraftMaterialDefinitionSO material,
        float directWork,
        CharacterProficiencyRank rank,
        EmbeddedWorkValueSnapshot embeddedWork,
        IMaterialSalvageCalculator salvageCalculator,
        ICollection<string> failures,
        int day,
        string purpose)
    {
        Dictionary<string, int> inputs = new Dictionary<string, int>(StringComparer.Ordinal);
        if (material != null && !string.IsNullOrWhiteSpace(material.ItemId))
        {
            inputs[material.ItemId] = Mathf.Max(1, definition.PrimaryMaterialAmount);
        }
        foreach (ItemAmountDefinition component in definition.RequiredComponentInputs
                     ?? Array.Empty<ItemAmountDefinition>())
        {
            if (component == null
                || string.IsNullOrWhiteSpace(component.ItemId)
                || component.Amount <= 0)
            {
                continue;
            }
            inputs.TryGetValue(component.ItemId, out int current);
            inputs[component.ItemId] = current + component.Amount;
        }

        MaterialSalvageResult salvage = salvageCalculator.Calculate(
            DismantleTargetKind.CombatEquipment,
            directWork,
            inputs.Select(pair => new ItemAmountDefinition(pair.Key, pair.Value)),
            ProficiencyProgressionRules.ResolveQualityScore(rank));
        float credit = 0f;
        foreach (ItemAmountDefinition recovered in salvage.RecoveredMaterials)
        {
            if (!embeddedWork.TryGetItemWork(recovered.ItemId, out float itemEwu))
            {
                failures.Add(
                    $"Day {day}: {purpose} rejected recovery item "
                    + $"'{recovered.ItemId}' has no embedded work.");
                return (float.NaN, salvage.RequiredWork);
            }
            credit += recovered.Amount * itemEwu;
        }
        return (credit, salvage.RequiredWork);
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
            researchIds.Add(V21ResearchConsolidation.Normalize(researchId));
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
                $"Day {day}: {label} net equipment EWU {ewu:0.0} exceeds "
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
            + ResolveCumulativeActualWork(day)
            * ProficiencyProgressionRules.ExperiencePerApprovedWork;
        long milliExperience = checked((long)Math.Round(
            experience * ProficiencyProgressionRules.MilliPerExperience,
            MidpointRounding.AwayFromZero));
        return ProficiencyProgressionRules.ResolveRank(milliExperience);
    }

    private static TechnologyWuCheckpoint ResolveWindowLaborCheckpoint(int day)
    {
        IReadOnlyList<TechnologyWuCheckpoint> checkpoints =
            SettlementLaborBalanceRules.TechnologyCheckpoints;
        TechnologyWuCheckpoint result = checkpoints[0];
        for (int index = 1; index < checkpoints.Count; index++)
        {
            if (day <= checkpoints[index].AbsoluteDay)
                break;
            result = checkpoints[index];
        }
        return result;
    }

    private static float ResolveCumulativeActualWork(int day)
    {
        int remainingDays = Math.Max(0, day - 1);
        int previousDay = 1;
        float total = 0f;
        IReadOnlyList<TechnologyWuCheckpoint> checkpoints =
            SettlementLaborBalanceRules.TechnologyCheckpoints;
        for (int index = 0; index < checkpoints.Count && remainingDays > 0; index++)
        {
            int nextDay = index + 1 < checkpoints.Count
                ? checkpoints[index + 1].AbsoluteDay
                : day;
            int segmentDays = Math.Min(remainingDays, Math.Max(0, nextDay - previousDay));
            total += segmentDays * checkpoints[index].ActualLaborWu;
            remainingDays -= segmentDays;
            previousDay = nextDay;
        }
        return total;
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

    private static string FormatSummary(EquipmentCostSummary summary) =>
        F(summary.Direct) + " / " + F(summary.GrossEwu) + " / " + F(summary.NetEwu);

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

    private static Checkpoint[] CreateCheckpoints() =>
        CombatBalanceCheckpointAuthority.All.Select(value => new Checkpoint
        {
            Day = value.Day,
            WorkingMinimum = value.WorkingMinimum,
            CombatReadyMinimum = value.CombatReadyMinimum,
            TargetId = value.TargetId,
            PartyQuality = value.Quality,
            PartyEquipmentIds = new[]
                {
                    value.WeaponId,
                    value.ArmorId,
                    value.ShieldId
                }
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .ToArray(),
            ReadinessQuality = CombatEquipmentQuality.Normal,
            ReadinessEquipmentIds = new[] { "weapon:spear", "armor:cloth-hood" }
        }).ToArray();
}
