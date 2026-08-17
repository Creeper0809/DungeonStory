#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using DungeonStory.Foundation;
using UnityEditor;
using UnityEngine;

public static class V27SpeciesBalanceSimulationDebugScenarios
{
    private const int Samples = 100_000;
    private const int ConditionSamplesPerSpecies = 10_000;
    private const int SimulationDays = 30;
    private const float NeutralDailyWork =
        SettlementLaborAuthority.ActualWuPerAdultDay;
    private const string CatalogPath =
        "Assets/Resources/SO/Content/GameDomainContentCatalog.asset";
    private const string ReportPath =
        "Artifacts/QA/v27-species-capacity-balance.md";

    private sealed class Role
    {
        public Role(string name, string formulaId, string proficiencyId)
        {
            Name = name;
            FormulaId = formulaId;
            ProficiencyId = proficiencyId;
        }

        public string Name { get; }
        public string FormulaId { get; }
        public string ProficiencyId { get; }
    }

    private sealed class SpeciesDistribution
    {
        public SpeciesDistribution(string speciesTag, int roleCount)
        {
            SpeciesTag = speciesTag;
            Values = Enumerable.Range(0, roleCount)
                .Select(_ => new List<float>(Samples / 9 + 8))
                .ToArray();
        }

        public string SpeciesTag { get; }
        public List<float>[] Values { get; }
    }

    private readonly struct UpkeepResult
    {
        public UpkeepResult(
            float grossWork,
            float netWork,
            int crystals,
            int lumber,
            float upkeepWork,
            float endingCharge,
            float endingIntegrity)
        {
            GrossWork = grossWork;
            NetWork = netWork;
            Crystals = crystals;
            Lumber = lumber;
            UpkeepWork = upkeepWork;
            EndingCharge = endingCharge;
            EndingIntegrity = endingIntegrity;
        }

        public float GrossWork { get; }
        public float NetWork { get; }
        public int Crystals { get; }
        public int Lumber { get; }
        public float UpkeepWork { get; }
        public float EndingCharge { get; }
        public float EndingIntegrity { get; }
    }

    private static readonly Role[] Roles =
    {
        new("현장", "performance:work:gather:speed", "proficiency:fieldwork"),
        new("건설", "performance:work:construct:speed", "proficiency:construction-engineering"),
        new("제작", "performance:work:craft:speed", "proficiency:crafting"),
        new("식량", "performance:work:cook:speed", "proficiency:food-production"),
        new("연구", "performance:work:research:speed", "proficiency:scholarship"),
        new("의료", CharacterPerformanceFormulaIds.TreatmentEfficiency, "proficiency:medicine"),
        new("사교", "performance:work:reception:speed", "proficiency:social"),
        new("근접", CharacterPerformanceFormulaIds.MeleePower, "proficiency:melee-combat"),
        new("원거리", "performance:combat:ranged-hit", "proficiency:ranged-combat")
    };

    [MenuItem("DungeonStory/Debug/V27/Run Species Capacity Balance Simulation")]
    public static string Run()
    {
        GameDomainContentCatalogSO catalog = AssetDatabase
            .LoadAssetAtPath<GameDomainContentCatalogSO>(CatalogPath)
            ?? throw new InvalidOperationException("Domain catalog is missing.");
        CharacterSpeciesSO[] species = catalog.GetAll<CharacterSpeciesSO>()
            .Where(value => value != null
                && !string.Equals(value.speciesTag, "Adventurer", StringComparison.Ordinal))
            .OrderBy(value => value.speciesTag, StringComparer.Ordinal)
            .ToArray();
        Require(species.Length == 9, $"Expected nine dungeon species, found {species.Length}.");
        CharacterPerformanceFormulaDefinitionSO[] formulas = catalog
            .GetAll<CharacterPerformanceFormulaDefinitionSO>()
            .ToArray();
        Dictionary<string, CharacterPerformanceFormulaDefinitionSO> formulaById = formulas
            .ToDictionary(value => value.FormulaId, StringComparer.Ordinal);
        Require(Roles.All(value => formulaById.ContainsKey(value.FormulaId)),
            "At least one representative role formula is missing.");

        CharacterTraitSO[] traits = catalog.Definitions
            .OfType<CharacterTraitSO>()
            .Where(IsFounderTrait)
            .OrderBy(value => value.id)
            .ToArray();
        Require(traits.Length == 100, $"Founder trait count={traits.Length}.");
        CharacterStartingOriginSO[] origins = LoadAll<CharacterStartingOriginSO>();
        CharacterStartingHistorySO[] histories = LoadAll<CharacterStartingHistorySO>();
        AgeConditionDefinitionSO[] ageDefinitions = LoadAll<AgeConditionDefinitionSO>();
        CharacterStartingAgeCondition[] ageConditions = ageDefinitions
            .Select(value => new CharacterStartingAgeCondition(
                value.conditionId,
                value.constructCondition))
            .ToArray();
        SpeciesLifeHistorySO life = LoadAll<SpeciesLifeHistorySO>()
            .Single(value => string.Equals(
                value.speciesTag,
                "Adventurer",
                StringComparison.Ordinal));
        CharacterStartingLifeHistory startingLife = new(
            life.adultAgeYears,
            life.elderAgeYears,
            life.untreatedExpectedLifeYears,
            life.construct);

        Dictionary<string, SpeciesDistribution> distributions = species
            .ToDictionary(
                value => value.speciesTag,
                value => new SpeciesDistribution(value.speciesTag, Roles.Length),
                StringComparer.Ordinal);
        Dictionary<string, float[]> capacityFactors = species.ToDictionary(
            value => value.speciesTag,
            value => Roles.Select(role => ResolveFunctionalFactor(
                    formulaById[role.FormulaId],
                    CapacityRow(value.speciesTag)))
                .ToArray(),
            StringComparer.Ordinal);

        for (int sample = 0; sample < Samples; sample++)
        {
            CharacterSpeciesSO sampleSpecies = species[sample % species.Length];
            int seed = CharacterGrowthRules.StableHash($"v27-species-person:{sample}");
            int originIndex = PositiveModulo(
                CharacterGrowthRules.StableHash($"{seed}:origin"), origins.Length);
            int historyIndex = PositiveModulo(
                CharacterGrowthRules.StableHash($"{seed}:history"), histories.Length);
            CharacterStartingProfileRoll roll = CharacterStartingProfileRules.Create(
                seed,
                startingLife,
                origins[originIndex],
                histories[historyIndex],
                ageConditions);
            IReadOnlyList<int> selectedIds = CharacterTraitSelectionRules.Select(
                traits,
                Array.Empty<CharacterTraitConflictRule>(),
                new DeterministicRandomSequence(
                    CharacterGrowthRules.StableHash($"{seed}:traits")),
                sampleSpecies.speciesTag);
            CharacterTraitSO[] selected = selectedIds
                .Select(id => traits.Single(value => value.id == id))
                .ToArray();
            List<CharacterStartingProficiencyExperience> starts = roll.Proficiencies
                .Select(value => value.Clone())
                .ToList();
            CharacterTraitStartingProficiencyRules.Apply(
                starts,
                selected,
                CharacterStartingProfileRules.ResolveAgeCap(roll.Profile.ageBand));
            Dictionary<string, CharacterStartingProficiencyExperience> startById = starts
                .ToDictionary(value => value.proficiencyId, StringComparer.Ordinal);
            SpeciesDistribution distribution = distributions[sampleSpecies.speciesTag];
            for (int roleIndex = 0; roleIndex < Roles.Length; roleIndex++)
            {
                Role role = Roles[roleIndex];
                CharacterPerformanceFormulaDefinitionSO formula = formulaById[role.FormulaId];
                float proficiency = ResolveProficiencyFactor(
                    formula,
                    role.ProficiencyId,
                    startById);
                float effect = string.IsNullOrWhiteSpace(formula.GameplayEffectTargetId)
                    ? 1f
                    : CharacterGameplayEffectProjector.Resolve(
                        formula.GameplayEffectTargetId,
                        1f,
                        selected.Cast<IGameplayEffectSource>()).Value;
                distribution.Values[roleIndex].Add(
                    capacityFactors[sampleSpecies.speciesTag][roleIndex]
                    * proficiency
                    * effect);
            }
        }

        foreach (SpeciesDistribution distribution in distributions.Values)
            foreach (List<float> values in distribution.Values)
                values.Sort();

        StringBuilder report = new();
        report.AppendLine("# V27 던전 종족 기능 밸런스 결정론적 감사");
        report.AppendLine();
        report.AppendLine($"- 개인 표본: {Samples:N0}명 (9종 순환 배정, 실제 시작 프로필·특성 선택 규칙)");
        report.AppendLine($"- 유지비 기간: {SimulationDays}일, 중립 승인 작업량 {NeutralDailyWork:0} WU/일");
        report.AppendLine("- 종족 기능 계수는 14개 승인 기능표와 실제 SO 공식을 사용한다.");
        report.AppendLine();
        report.AppendLine("## 개인차 p10 / 중앙 / p90");
        report.AppendLine();
        report.AppendLine("| 역할 | 주력 종족 중앙 | 최고 비주력 p90 | 개인 p90/p10 | 종족 중앙 max/min |");
        report.AppendLine("|---|---:|---:|---:|---:|");
        float maximumPersonalSpread = 0f;
        float maximumSpeciesSpread = 0f;
        for (int roleIndex = 0; roleIndex < Roles.Length; roleIndex++)
        {
            (string Species, float Value)[] medians = species
                .Select(value => (
                    value.speciesTag,
                    Percentile(distributions[value.speciesTag].Values[roleIndex], .50f)))
                .OrderByDescending(value => value.Item2)
                .ToArray();
            string leader = medians[0].Species;
            float leaderMedian = medians[0].Value;
            float bestNonLeaderP90 = species
                .Where(value => !string.Equals(value.speciesTag, leader, StringComparison.Ordinal))
                .Max(value => Percentile(
                    distributions[value.speciesTag].Values[roleIndex],
                    .90f));
            float personalSpread = species.Max(value =>
                Percentile(distributions[value.speciesTag].Values[roleIndex], .90f)
                / Mathf.Max(.0001f, Percentile(
                    distributions[value.speciesTag].Values[roleIndex], .10f)));
            float speciesSpread = medians[0].Value
                / Mathf.Max(.0001f, medians[^1].Value);
            maximumPersonalSpread = Mathf.Max(maximumPersonalSpread, personalSpread);
            maximumSpeciesSpread = Mathf.Max(maximumSpeciesSpread, speciesSpread);
            Require(bestNonLeaderP90 > leaderMedian,
                $"Role '{Roles[roleIndex].Name}' cannot be overturned by a non-leading species p90 individual.");
            report.Append("| ").Append(Roles[roleIndex].Name)
                .Append(" | ").Append(leader).Append(' ').Append(F(leaderMedian))
                .Append(" | ").Append(F(bestNonLeaderP90))
                .Append(" | ").Append(F(personalSpread))
                .Append(" | ").Append(F(speciesSpread)).AppendLine(" |");
        }
        Require(maximumPersonalSpread > maximumSpeciesSpread,
            $"Maximum natural proficiency/trait spread {maximumPersonalSpread:0.000} "
            + $"does not exceed maximum species spread {maximumSpeciesSpread:0.000}.");
        VerifyNoRepresentativeParetoDominance(species, capacityFactors);
        report.AppendLine();
        report.AppendLine($"전체 최대 개인차 {F(maximumPersonalSpread)} > 전체 최대 종족차 {F(maximumSpeciesSpread)}.");

        report.AppendLine();
        report.AppendLine("## 30일 유지비");
        report.AppendLine();
        report.AppendLine("생물 종족의 음식·물·수면은 서로 다른 물리 단위라 WU로 환산하지 않고 별도 축으로 기록한다. 골렘만 승인된 충전·정비 절차가 직접 WU를 가지므로 순 WU에서 차감한다.");
        report.AppendLine();
        report.AppendLine("| 종족 | 일반 역할 계수 | 최적 역할 계수 | 음식 지수 | 물 지수 | 수면 지수 | 골렘 순 WU 비율 | 결정 | 목재 | 유지 WU |");
        report.AppendLine("|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|");
        foreach (CharacterSpeciesSO value in species)
        {
            float general = capacityFactors[value.speciesTag]
                .OrderBy(factor => Mathf.Abs(factor - 1f))
                .First();
            float best = capacityFactors[value.speciesTag].Max();
            Require(general >= .95f && general <= 1.05f,
                $"Species '{value.speciesTag}' neutral-fit assignment {general:0.000} is outside 0.95~1.05.");
            Require(best >= 1.05f && best <= 1.15f,
                $"Species '{value.speciesTag}' representative-role assignment {best:0.000} is outside 1.05~1.15.");
            SpeciesNeedProfile needs = value.needs ?? new SpeciesNeedProfile();
            UpkeepResult upkeep = string.Equals(value.speciesTag, "Golem", StringComparison.Ordinal)
                ? SimulateGolem(best, suppliesAvailable: true)
                : new UpkeepResult(
                    best * NeutralDailyWork * SimulationDays,
                    best * NeutralDailyWork * SimulationDays,
                    0,
                    0,
                    0f,
                    0f,
                    0f);
            float golemRatio = string.Equals(value.speciesTag, "Golem", StringComparison.Ordinal)
                ? upkeep.NetWork / (NeutralDailyWork * SimulationDays)
                : 0f;
            if (string.Equals(value.speciesTag, "Golem", StringComparison.Ordinal))
                Require(golemRatio <= 1.0501f,
                    $"Golem net 30-day work ratio {golemRatio:0.000} exceeds 1.05.");
            report.Append("| ").Append(value.speciesTag)
                .Append(" | ").Append(F(general))
                .Append(" | ").Append(F(best))
                .Append(" | ").Append(F(needs.hungerRateMultiplier))
                .Append(" | ").Append(F(needs.thirstRateMultiplier))
                .Append(" | ").Append(F(needs.sleepRateMultiplier))
                .Append(" | ").Append(string.Equals(value.speciesTag, "Golem", StringComparison.Ordinal)
                    ? F(golemRatio)
                    : "-")
                .Append(" | ").Append(upkeep.Crystals)
                .Append(" | ").Append(upkeep.Lumber)
                .Append(" | ").Append(F(upkeep.UpkeepWork)).AppendLine(" |");
        }
        UpkeepResult disconnected = SimulateGolem(
            capacityFactors["Golem"].Max(),
            suppliesAvailable: false);
        Require(disconnected.Crystals == 0
                && disconnected.Lumber == 0
                && disconnected.UpkeepWork == 0f,
            "Supply-disconnected Golem simulation created free materials or work.");
        report.AppendLine();
        report.AppendLine($"공급 단절 골렘: 종료 충전 {F(disconnected.EndingCharge)}, 종료 건전도 {F(disconnected.EndingIntegrity)}, 무료 충전·정비 0건.");

        AppendConditionMatrix(
            report,
            species,
            formulaById,
            capacityFactors);

        string absolute = Path.GetFullPath(ReportPath);
        Directory.CreateDirectory(Path.GetDirectoryName(absolute) ?? string.Empty);
        File.WriteAllText(absolute, report.ToString(), new UTF8Encoding(false));
        string summary =
            $"PHASE153_SPECIES_BALANCE_SIM=PASS; samples={Samples}; days={SimulationDays}; report={absolute}";
        Debug.Log(summary);
        return summary;
    }

    private static UpkeepResult SimulateGolem(float workFactor, bool suppliesAvailable)
    {
        float charge = 100f;
        float integrity = 100f;
        float wearRemainder = 0f;
        float gross = 0f;
        float upkeepWork = 0f;
        int crystals = 0;
        int lumber = 0;
        for (int day = 0; day < SimulationDays; day++)
        {
            charge = Mathf.Max(
                0f,
                charge - .035f * GameCalendarRules.SecondsPerDay);
            if (charge <= 35f && suppliesAvailable)
            {
                charge = Mathf.Min(100f, charge + 50f);
                crystals++;
                upkeepWork += 100f;
            }
            float work = NeutralDailyWork * workFactor;
            if (charge <= 0f)
                work = 0f;
            gross += work;
            wearRemainder += work;
            while (wearRemainder + .0001f >= 100f)
            {
                wearRemainder -= 100f;
                integrity = Mathf.Max(0f, integrity - 2.5f);
            }
            if (integrity <= 50f && suppliesAvailable)
            {
                integrity = Mathf.Min(100f, integrity + 30f);
                lumber++;
                upkeepWork += 26f;
            }
        }
        return new UpkeepResult(
            gross,
            Mathf.Max(0f, gross - upkeepWork),
            crystals,
            lumber,
            upkeepWork,
            charge,
            integrity);
    }

    private static void VerifyNoRepresentativeParetoDominance(
        IReadOnlyList<CharacterSpeciesSO> species,
        IReadOnlyDictionary<string, float[]> factors)
    {
        foreach (CharacterSpeciesSO candidate in species)
        foreach (CharacterSpeciesSO other in species)
        {
            if (ReferenceEquals(candidate, other)) continue;
            float[] left = factors[candidate.speciesTag]
                .Concat(BuiltInWorkTypeIds.All.Select(workType =>
                    AptitudeFactor(candidate, workType)))
                .ToArray();
            float[] right = factors[other.speciesTag]
                .Concat(BuiltInWorkTypeIds.All.Select(workType =>
                    AptitudeFactor(other, workType)))
                .ToArray();
            bool noWorse = left.Zip(right, (a, b) => a + .0001f >= b)
                .All(value => value);
            bool strictlyBetter = left.Zip(right, (a, b) => a > b + .0001f)
                .Any(value => value);
            Require(!(noWorse && strictlyBetter),
                $"Species '{candidate.speciesTag}' Pareto-dominates "
                + $"'{other.speciesTag}' across representative roles.");
        }
    }

    private static float AptitudeFactor(
        CharacterSpeciesSO species,
        WorkTypeId workTypeId)
    {
        bool strong = (species?.strongWorkTypeIds ?? Array.Empty<string>())
            .Contains(workTypeId.Value, StringComparer.Ordinal);
        bool weak = (species?.weakWorkTypeIds ?? Array.Empty<string>())
            .Contains(workTypeId.Value, StringComparer.Ordinal);
        return strong
            ? CharacterSpeciesWorkAptitudeRules.StrongLearningMultiplier
            : weak
                ? CharacterSpeciesWorkAptitudeRules.WeakLearningMultiplier
                : 1f;
    }

    private static void AppendConditionMatrix(
        StringBuilder report,
        IReadOnlyList<CharacterSpeciesSO> species,
        IReadOnlyDictionary<string, CharacterPerformanceFormulaDefinitionSO> formulas,
        IReadOnlyDictionary<string, float[]> roleFactors)
    {
        DiseaseDefinitionSO[] diseases = LoadAll<DiseaseDefinitionSO>();
        Require(diseases.Length == 16,
            $"Expected 16 authored diseases, found {diseases.Length}.");
        Require(formulas.TryGetValue(
                "performance:survival:cold-exposure",
                out CharacterPerformanceFormulaDefinitionSO coldFormula),
            "Cold exposure formula is missing.");
        Require(formulas.TryGetValue(
                "performance:survival:heat-exposure",
                out CharacterPerformanceFormulaDefinitionSO heatFormula),
            "Heat exposure formula is missing.");

        report.AppendLine();
        report.AppendLine("## Condition matrix (deterministic)");
        report.AppendLine();
        report.AppendLine("Normal climate is 1.000. Cold and heat use the actual exposure formulas (lower is safer). Damage applies 25/50/75% health to the representative formula's highest-weight capacity. Disease applies authored severity to its target system through anatomy infection burden.");
        report.AppendLine();
        report.AppendLine("| Species | neutral-fit | representative | cold exposure | heat exposure | condition mean | condition min | work unavailable | samples |");
        report.AppendLine("|---|---:|---:|---:|---:|---:|---:|---:|---:|");
        float[] healthRatios = { .25f, .50f, .75f };
        foreach (CharacterSpeciesSO value in species)
        {
            IReadOnlyDictionary<CharacterFunctionalCapacityId, float> baseRow =
                CapacityRow(value.speciesTag);
            float sum = 0f;
            float minimum = float.PositiveInfinity;
            int unavailable = 0;
            for (int sample = 0; sample < ConditionSamplesPerSpecies; sample++)
            {
                int hash = CharacterGrowthRules.StableHash(
                    $"v27-species-condition:{value.speciesTag}:{sample}");
                int roleIndex = PositiveModulo(hash, Roles.Length);
                int diseaseIndex = PositiveModulo(hash / 17, diseases.Length);
                int damageIndex = PositiveModulo(hash / 31, healthRatios.Length);
                CharacterPerformanceFormulaDefinitionSO role =
                    formulas[Roles[roleIndex].FormulaId];
                Dictionary<CharacterFunctionalCapacityId, float> conditioned =
                    baseRow.ToDictionary(pair => pair.Key, pair => pair.Value);
                CharacterPerformanceCapacityInput primary = role.CapacityInputs
                    .Where(input => input.Weight > 0f)
                    .OrderByDescending(input => input.Weight)
                    .ThenBy(input => input.CapacityId)
                    .First();
                conditioned[primary.CapacityId] *= healthRatios[damageIndex];
                DiseaseDefinitionSO disease = diseases[diseaseIndex];
                CharacterFunctionalCapacityId diseaseCapacity =
                    DiseaseCapacity(disease.targetSystem);
                conditioned[diseaseCapacity] *= 1f
                    - .5f * Mathf.Clamp01(disease.baseSeverity / 100f);
                float result = ResolveFunctionalFactor(role, conditioned);
                Require(!float.IsNaN(result)
                        && !float.IsInfinity(result)
                        && result >= 0f,
                    $"Condition sample produced invalid result for '{value.speciesTag}'.");
                sum += result;
                minimum = Mathf.Min(minimum, result);
                if (result <= .0001f) unavailable++;
            }
            float neutral = roleFactors[value.speciesTag]
                .OrderBy(factor => Mathf.Abs(factor - 1f))
                .First();
            float representative = roleFactors[value.speciesTag].Max();
            float cold = ResolveFunctionalFactor(coldFormula, baseRow);
            float heat = ResolveFunctionalFactor(heatFormula, baseRow);
            report.Append("| ").Append(value.speciesTag)
                .Append(" | ").Append(F(neutral))
                .Append(" | ").Append(F(representative))
                .Append(" | ").Append(F(cold))
                .Append(" | ").Append(F(heat))
                .Append(" | ").Append(F(sum / ConditionSamplesPerSpecies))
                .Append(" | ").Append(F(minimum))
                .Append(" | ").Append(unavailable)
                .Append(" | ").Append(ConditionSamplesPerSpecies)
                .AppendLine(" |");
        }
        report.AppendLine();
        report.AppendLine("Disease recovery duration remains the authored incubation/contagious/chronic domain value; this capacity audit does not invent treatment-WU or death conversions.");
    }

    private static CharacterFunctionalCapacityId DiseaseCapacity(
        DiseaseTargetSystem targetSystem) => targetSystem switch
        {
            DiseaseTargetSystem.Consciousness =>
                CharacterFunctionalCapacityId.MentalMaintenance,
            DiseaseTargetSystem.Breathing =>
                CharacterFunctionalCapacityId.RespiratoryExchange,
            DiseaseTargetSystem.Digestion =>
                CharacterFunctionalCapacityId.IntakeProcessing,
            DiseaseTargetSystem.Filtration =>
                CharacterFunctionalCapacityId.PurificationProcessing,
            _ => CharacterFunctionalCapacityId.PowerCirculation
        };

    private static float ResolveProficiencyFactor(
        CharacterPerformanceFormulaDefinitionSO formula,
        string selectorFallback,
        IReadOnlyDictionary<string, CharacterStartingProficiencyExperience> starts)
    {
        string primaryId = ResolveProficiencyId(
            formula.PrimaryProficiencyId,
            selectorFallback);
        if (primaryId.Length == 0) return 1f;
        float primary = ResolveOne(primaryId, starts, formula.ResultChannel);
        if (formula.SecondaryProficiencyWeight <= 0f) return primary;
        string secondaryId = ResolveProficiencyId(
            formula.SecondaryProficiencyId,
            selectorFallback);
        float secondary = ResolveOne(secondaryId, starts, formula.ResultChannel);
        return primary * (1f - formula.SecondaryProficiencyWeight)
            + secondary * formula.SecondaryProficiencyWeight;
    }

    private static string ResolveProficiencyId(string authored, string fallback) =>
        string.IsNullOrWhiteSpace(authored)
            ? string.Empty
            : authored.StartsWith("selector:", StringComparison.Ordinal)
                ? fallback
                : authored;

    private static float ResolveOne(
        string proficiencyId,
        IReadOnlyDictionary<string, CharacterStartingProficiencyExperience> starts,
        CharacterPerformanceResultChannel channel)
    {
        if (!starts.TryGetValue(proficiencyId, out CharacterStartingProficiencyExperience start))
            throw new InvalidOperationException($"Starting proficiency '{proficiencyId}' is missing.");
        CharacterProficiencyEffectSnapshot effects = ProficiencyProgressionRules.ResolveEffects(
            start.experience * ProficiencyProgressionRules.MilliPerExperience);
        return channel switch
        {
            CharacterPerformanceResultChannel.AccidentRisk => effects.AccidentMultiplier,
            CharacterPerformanceResultChannel.Quality
                or CharacterPerformanceResultChannel.Yield
                or CharacterPerformanceResultChannel.SuccessChance =>
                    Mathf.Max(0f, effects.QualityScore / 58f),
            _ => effects.WorkSpeedMultiplier
        };
    }

    private static IReadOnlyDictionary<CharacterFunctionalCapacityId, float>
        CapacityRow(string speciesTag)
    {
        if (!V27CharacterPerformanceContentAssetBuilder.TryGetSpeciesCapacityMultipliers(
                speciesTag,
                out float[] row))
            throw new InvalidOperationException($"Species capacity row '{speciesTag}' is missing.");
        CharacterFunctionalCapacityId[] ids = Enum
            .GetValues(typeof(CharacterFunctionalCapacityId))
            .Cast<CharacterFunctionalCapacityId>()
            .ToArray();
        Require(row.Length == ids.Length,
            $"Species '{speciesTag}' capacity row length={row.Length}.");
        return ids.Select((id, index) => (id, row[index]))
            .ToDictionary(value => value.id, value => value.Item2);
    }

    private static float ResolveFunctionalFactor(
        CharacterPerformanceFormulaDefinitionSO formula,
        IReadOnlyDictionary<CharacterFunctionalCapacityId, float> capacities)
    {
        float weighted = 0f;
        float totalWeight = 0f;
        float bottleneck = float.PositiveInfinity;
        foreach (CharacterPerformanceCapacityInput input in formula.CapacityInputs)
        {
            float value = capacities[input.CapacityId];
            if ((input.Role & CharacterPerformanceInputRole.Required) != 0
                && value < (input.RequiredThreshold > 0f ? input.RequiredThreshold : .10f))
                return 0f;
            if ((input.Role & CharacterPerformanceInputRole.Contribution) != 0
                && input.Weight > 0f)
            {
                weighted += value * input.Weight;
                totalWeight += input.Weight;
            }
            if ((input.Role & CharacterPerformanceInputRole.Bottleneck) != 0)
                bottleneck = Mathf.Min(bottleneck, .25f + .75f * value);
        }
        Require(totalWeight > 0f, $"Formula '{formula.FormulaId}' has no contributions.");
        float raw = Mathf.Min(weighted / totalWeight, bottleneck);
        return formula.ResultChannel is
                CharacterPerformanceResultChannel.AccidentRisk
                or CharacterPerformanceResultChannel.Consumption
                or CharacterPerformanceResultChannel.Exposure
            ? 1f / Mathf.Max(.05f, raw)
            : raw;
    }

    private static float Percentile(IReadOnlyList<float> sorted, float percentile)
    {
        Require(sorted != null && sorted.Count > 0, "Percentile source is empty.");
        float position = Mathf.Clamp01(percentile) * (sorted.Count - 1);
        int lower = Mathf.FloorToInt(position);
        int upper = Mathf.CeilToInt(position);
        return Mathf.Lerp(sorted[lower], sorted[upper], position - lower);
    }

    private static int PositiveModulo(int value, int modulo)
    {
        int result = value % modulo;
        return result < 0 ? result + modulo : result;
    }

    private static string F(float value) =>
        value.ToString("0.000", CultureInfo.InvariantCulture);

    private static T[] LoadAll<T>() where T : UnityEngine.Object =>
        AssetDatabase.FindAssets($"t:{typeof(T).Name}")
            .Select(AssetDatabase.GUIDToAssetPath)
            .Select(AssetDatabase.LoadAssetAtPath<T>)
            .Where(value => value != null)
            .OrderBy(value => value.name, StringComparer.Ordinal)
            .ToArray();

    private static bool IsFounderTrait(CharacterTraitSO value) =>
        value != null
        && (V26FounderTraitContentBuilder.RetainedIds.Contains(value.id)
            || value.id is >= 247 and <= 259
            || value.id is >= 300 and <= 306
            || value.id is >= 400 and <= 417
            || value.id is >= 500 and <= 518);

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
#endif
