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

public static class V26FounderIndustryBalanceDebugScenarios
{
    private const string CatalogPath =
        "Assets/Resources/SO/Content/GameDomainContentCatalog.asset";
    private const string ReportPath =
        "Artifacts/QA/v26-founder-industry-bottom-up.md";
    private const int PartyRolls = 10_000;
    private const float ApprovedWorkPerDay = 99f;
    private const float IndustryShare = .35f;

    private static readonly CharacterProficiencyId[] ManufacturingJobs =
    {
        BuiltInCharacterProficiencyIds.Fieldwork,
        BuiltInCharacterProficiencyIds.FoodProduction,
        BuiltInCharacterProficiencyIds.Crafting
    };

    private sealed class Candidate
    {
        public Dictionary<string, float> Speeds = new(StringComparer.Ordinal);
        public Dictionary<string, float> AccidentMultipliers = new(StringComparer.Ordinal);
        public float ConsumptionMultiplier = 1f;
        public float EarnedExperienceMultiplier = 1f;

        public float Speed(CharacterProficiencyId id) =>
            Speeds.TryGetValue(id.Value, out float value) ? value : 0f;

        public float AccidentMultiplier(CharacterProficiencyId id) =>
            AccidentMultipliers.TryGetValue(id.Value, out float value) ? value : 1f;
    }

    private readonly struct PartyMeasure
    {
        public PartyMeasure(
            float assignmentSpeed,
            float craftSpeed,
            IReadOnlyDictionary<string, float> bestSpeeds,
            float consumptionIndex,
            float expectedShiftAccidents,
            float assignedLearningMultiplier)
        {
            AssignmentSpeed = assignmentSpeed;
            CraftSpeed = craftSpeed;
            BestSpeeds = bestSpeeds;
            ConsumptionIndex = consumptionIndex;
            ExpectedShiftAccidents = expectedShiftAccidents;
            AssignedLearningMultiplier = assignedLearningMultiplier;
        }

        public float AssignmentSpeed { get; }
        public float CraftSpeed { get; }
        public IReadOnlyDictionary<string, float> BestSpeeds { get; }
        public float ConsumptionIndex { get; }
        public float ExpectedShiftAccidents { get; }
        public float AssignedLearningMultiplier { get; }
    }

    private readonly struct RegimeMeasure
    {
        public RegimeMeasure(
            string name,
            float assignmentSpeed,
            float craftSpeed,
            IReadOnlyDictionary<string, float> bestSpeeds,
            float consumptionIndex,
            float expectedShiftAccidents,
            float assignedLearningMultiplier,
            string definition)
        {
            Name = name;
            AssignmentSpeed = assignmentSpeed;
            CraftSpeed = craftSpeed;
            BestSpeeds = bestSpeeds;
            ConsumptionIndex = consumptionIndex;
            ExpectedShiftAccidents = expectedShiftAccidents;
            AssignedLearningMultiplier = assignedLearningMultiplier;
            Definition = definition;
        }

        public string Name { get; }
        public float AssignmentSpeed { get; }
        public float CraftSpeed { get; }
        public IReadOnlyDictionary<string, float> BestSpeeds { get; }
        public float ConsumptionIndex { get; }
        public float ExpectedShiftAccidents { get; }
        public float AssignedLearningMultiplier { get; }
        public string Definition { get; }
    }

    public readonly struct FounderIndustryBaseline
    {
        public FounderIndustryBaseline(
            float assignmentSpeed,
            float craftSpeed,
            float researchSpeed,
            float consumptionIndex,
            float expectedShiftAccidents,
            float assignedLearningMultiplier)
        {
            AssignmentSpeed = assignmentSpeed;
            CraftSpeed = craftSpeed;
            ResearchSpeed = researchSpeed;
            ConsumptionIndex = consumptionIndex;
            ExpectedShiftAccidents = expectedShiftAccidents;
            AssignedLearningMultiplier = assignedLearningMultiplier;
        }

        public float AssignmentSpeed { get; }
        public float CraftSpeed { get; }
        public float ResearchSpeed { get; }
        public float ConsumptionIndex { get; }
        public float ExpectedShiftAccidents { get; }
        public float AssignedLearningMultiplier { get; }
    }

    public readonly struct FounderIndustryDistribution
    {
        public FounderIndustryDistribution(
            string regimeId,
            int sampleCount,
            float assignmentP10,
            float assignmentMedian,
            float assignmentP90,
            float craftP10,
            float craftMedian,
            float craftP90,
            float researchP10,
            float researchMedian,
            float researchP90,
            float consumptionP10,
            float consumptionMedian,
            float consumptionP90)
        {
            RegimeId = regimeId ?? string.Empty;
            SampleCount = sampleCount;
            AssignmentP10 = assignmentP10;
            AssignmentMedian = assignmentMedian;
            AssignmentP90 = assignmentP90;
            CraftP10 = craftP10;
            CraftMedian = craftMedian;
            CraftP90 = craftP90;
            ResearchP10 = researchP10;
            ResearchMedian = researchMedian;
            ResearchP90 = researchP90;
            ConsumptionP10 = consumptionP10;
            ConsumptionMedian = consumptionMedian;
            ConsumptionP90 = consumptionP90;
        }

        public string RegimeId { get; }
        public int SampleCount { get; }
        public float AssignmentP10 { get; }
        public float AssignmentMedian { get; }
        public float AssignmentP90 { get; }
        public float CraftP10 { get; }
        public float CraftMedian { get; }
        public float CraftP90 { get; }
        public float ResearchP10 { get; }
        public float ResearchMedian { get; }
        public float ResearchP90 { get; }
        public float ConsumptionP10 { get; }
        public float ConsumptionMedian { get; }
        public float ConsumptionP90 { get; }
    }

    private readonly struct IndustryStage
    {
        public IndustryStage(
            string name,
            int day,
            int maximumResearchDepth,
            float partyDirectWork,
            float partyGrossEwu)
        {
            Name = name;
            Day = day;
            MaximumResearchDepth = maximumResearchDepth;
            PartyDirectWork = partyDirectWork;
            PartyGrossEwu = partyGrossEwu;
        }

        public string Name { get; }
        public int Day { get; }
        public int MaximumResearchDepth { get; }
        public float PartyDirectWork { get; }
        public float PartyGrossEwu { get; }
    }

    [MenuItem("DungeonStory/V26/Audit Founder Industry Bottom Up")]
    public static void Run()
    {
        GameDomainContentCatalogSO catalog = AssetDatabase
            .LoadAssetAtPath<GameDomainContentCatalogSO>(CatalogPath)
            ?? throw new InvalidOperationException("Root content catalog is missing.");
        CharacterTraitSO[] traits = catalog.Definitions
            .OfType<CharacterTraitSO>()
            .Where(IsFounderTrait)
            .OrderBy(value => value.id)
            .ToArray();
        Require(traits.Length == 100, $"Founder trait count={traits.Length}.");

        CharacterStartingOriginSO[] origins = LoadAll<CharacterStartingOriginSO>();
        CharacterStartingHistorySO[] histories = LoadAll<CharacterStartingHistorySO>();
        SpeciesLifeHistorySO life = LoadAll<SpeciesLifeHistorySO>()
            .Single(value => string.Equals(
                value.speciesTag,
                "Adventurer",
                StringComparison.Ordinal));
        AgeConditionDefinitionSO[] ageDefinitions =
            LoadAll<AgeConditionDefinitionSO>();
        CharacterStartingAgeCondition[] conditions = ageDefinitions
            .Select(value => new CharacterStartingAgeCondition(
                value.conditionId,
                value.constructCondition))
            .ToArray();
        CharacterSpeciesSO founderSpecies = catalog
            .GetAll<CharacterSpeciesSO>()
            .Single(value => string.Equals(
                value.speciesTag,
                "Adventurer",
                StringComparison.Ordinal));
        AnatomyProfileSO anatomy = LoadAll<AnatomyProfileSO>()
            .Single(value => string.Equals(
                value.ProfileId,
                founderSpecies.anatomyProfileId,
                StringComparison.Ordinal));
        CharacterPerformanceFormulaDefinitionSO[] performanceFormulas =
            catalog.GetAll<CharacterPerformanceFormulaDefinitionSO>()
                .ToArray();
        CharacterStartingLifeHistory startingLife = new(
            life.adultAgeYears,
            life.elderAgeYears,
            life.untreatedExpectedLifeYears,
            life.construct);
        ProductionRecipeSO[] recipes = LoadAll<ProductionRecipeSO>();
        ResourceItemDefinitionSO[] items = LoadAll<ResourceItemDefinitionSO>();
        ApparelDefinitionSO[] apparel = LoadAll<ApparelDefinitionSO>();
        ResearchProjectSO[] researchProjects = LoadAll<ResearchProjectSO>();
        Dictionary<string, int> researchDepth = BuildResearchDepth(
            researchProjects);
        Dictionary<ResourceItemKind, float> firstUnlockWork =
            ResolveFirstRecipeUnlockWork(
                recipes,
                items,
                researchProjects);
        float firstApparelUnlockWork = ResolveFirstApparelUnlockWork(
            apparel,
            researchProjects);

        PartyMeasure[] parties = BuildPartyMeasures(
            PartyRolls,
            startingLife,
            origins,
            histories,
            conditions,
            traits,
            anatomy,
            ageDefinitions,
            performanceFormulas,
            applyTraits: true);
        PartyMeasure[] traitFreeParties = BuildPartyMeasures(
            PartyRolls,
            startingLife,
            origins,
            histories,
            conditions,
            traits,
            anatomy,
            ageDefinitions,
            performanceFormulas,
            applyTraits: false);
        RegimeMeasure traitFreeNatural = MeanRegime(
            "동일 프로필·특성 없음",
            traitFreeParties,
            "같은 시드에서 특성 선택·효과만 제거한 대조군");

        RegimeMeasure[] regimes =
        {
            MeanRegime(
                "무리롤 평균",
                parties,
                "생성한 첫 3명을 그대로 채택"),
            BestOfBlocks(
                "현실적인 타협 리롤",
                parties,
                3,
                "완성 파티 3개 중 가장 나은 결과"),
            BestOfBlocks(
                "상위 리롤",
                parties,
                20,
                "완성 파티 20개 중 가장 나은 결과"),
            TheoreticalRegime()
        };

        IndustryStage[] stages =
        {
            new("연구 없음", 1, -1, 427.5f, 704.3f),
            new("초기 연구", 30, 1, 443f, 979.2f),
            new("중기 연구", 120, 3, 330.6f, 1134f)
        };

        StringBuilder report = new(8192);
        report.AppendLine("# V26 창립자 산업 밸런스 바닥부터 계산");
        report.AppendLine();
        report.AppendLine(
            "실제 시작 프로필 생성 순서(기본 XP 추출→출신→과거 이력→주/부전문→경력 XP→특성 XP→0 하한→나이 상한)와 "
            + "100종 특성 선택기를 함께 사용했다. 조건부 효과는 시작 시 충족되지 않은 것으로 보고, "
            + "무조건 작업 속도 효과만 적용했다.");
        report.AppendLine();
        report.AppendLine("## 리롤 집단 정의와 초기 처리량");
        report.AppendLine();
        report.AppendLine(
            "| 집단 | 표본 정의 | 필수산업 3종 배치 속도 합 | 제작 최고 속도 | 필수산업 합산 WU/일 | 제작 전담 WU/일 | 연구 전담 WU/일 |");
        report.AppendLine("|---|---|---:|---:|---:|---:|---:|");
        foreach (RegimeMeasure regime in regimes)
        {
            report.Append("| ").Append(regime.Name)
                .Append(" | ").Append(regime.Definition)
                .Append(" | ").Append(F(regime.AssignmentSpeed))
                .Append(" | ").Append(F(regime.CraftSpeed))
                .Append(" | ").Append(F(regime.AssignmentSpeed * ApprovedWorkPerDay))
                .Append(" | ").Append(F(regime.CraftSpeed * ApprovedWorkPerDay))
                .Append(" | ").Append(F(
                    Speed(regime, BuiltInCharacterProficiencyIds.Scholarship)
                    * ApprovedWorkPerDay))
                .AppendLine(" |");
        }

        report.AppendLine();
        report.AppendLine("### 3인 파티 실제 분포");
        report.AppendLine();
        report.AppendLine(
            "| 리롤 집단 | 지표 | p10 | 중앙 | p90 |");
        report.AppendLine("|---|---|---:|---:|---:|");
        AppendPartyDistribution(report, "무리롤", parties);
        AppendPartyDistribution(
            report,
            "현실적 타협(3파티 중 선택)",
            SelectBlockWinners(parties, 3));
        AppendPartyDistribution(
            report,
            "상위 리롤(20파티 중 선택)",
            SelectBlockWinners(parties, 20));

        report.AppendLine();
        report.AppendLine("## 특성 영향 벡터");
        report.AppendLine();
        report.AppendLine(
            "작업 속도만 WU에 직접 곱한다. 식량 소비, 사고 위험, 경험치 성장은 서로 단위가 다르므로 "
            + "하나의 가상 WU로 합치지 않고 실제 런타임 수식과 같은 별도 축으로 기록한다.");
        report.AppendLine(
            "사고 노출은 필수 산업 3종을 하루 99 승인 WU씩 수행할 때의 사건 기대 건수이며, "
            + "각 작업자의 숙련 사고 배수와 특성 사고 배수를 함께 적용한다. 사고 1건은 결정론적 해부 노드에 2 피해를 주고 총 생명력에도 반영되며 작업을 중단한다. 손상된 노드가 담당하는 기능과 후속 작업 성능은 단일 성능 Query에서 다시 계산한다.");
        report.AppendLine();
        report.AppendLine(
            "| 집단 | 3인 식량수요 지수 | 기준 3인 대비 | 3교대 사고 기대건/일 | 기대 피해/일 | 배치 작업자 평균 XP 배수 | 성공 작업 XP/인·일 |");
        report.AppendLine("|---|---:|---:|---:|---:|---:|---:|");
        foreach (RegimeMeasure regime in regimes)
        {
            report.Append("| ").Append(regime.Name)
                .Append(" | ").Append(F(regime.ConsumptionIndex))
                .Append(" | ").Append(F(regime.ConsumptionIndex / 3f * 100f)).Append('%')
                .Append(" | ").Append(F(regime.ExpectedShiftAccidents))
                .Append(" | ").Append(F(regime.ExpectedShiftAccidents * 2f))
                .Append(" | ").Append(F(regime.AssignedLearningMultiplier))
                .Append(" | ").Append(F(
                    ApprovedWorkPerDay
                    * ProficiencyProgressionRules.ExperiencePerApprovedWork
                    * regime.AssignedLearningMultiplier))
                .AppendLine(" |");
        }

        RegimeMeasure natural = regimes[0];
        report.AppendLine();
        report.AppendLine("### 동일 시드 대조군에서 특성만의 기여");
        report.AppendLine();
        report.AppendLine("| 축 | 특성 없음 | 실제 자연분포 | 변화 |");
        report.AppendLine("|---|---:|---:|---:|");
        AppendComparison(
            report,
            "필수산업 합산 WU/일",
            traitFreeNatural.AssignmentSpeed * ApprovedWorkPerDay,
            natural.AssignmentSpeed * ApprovedWorkPerDay);
        AppendComparison(
            report,
            "최고 제작자 WU/일",
            traitFreeNatural.CraftSpeed * ApprovedWorkPerDay,
            natural.CraftSpeed * ApprovedWorkPerDay);
        AppendComparison(
            report,
            "3인 식량수요 지수",
            traitFreeNatural.ConsumptionIndex,
            natural.ConsumptionIndex);
        AppendComparison(
            report,
            "3교대 사고 기대건/일",
            traitFreeNatural.ExpectedShiftAccidents,
            natural.ExpectedShiftAccidents);
        AppendComparison(
            report,
            "배치 작업자 평균 XP 배수",
            traitFreeNatural.AssignedLearningMultiplier,
            natural.AssignedLearningMultiplier);

        report.AppendLine();
        report.AppendLine("## 연구 단계별 장비 세트 생산 시간");
        report.AppendLine();
        report.AppendLine(
            "직접 제작일은 해당 파티의 최고 제작자가 전담하는 시간이다. 총산업일은 물리 BOM의 gross EWU를 "
            + "필수산업 3종 처리량 중 산업 배정 35%로 나눈 보수치다. 두 값은 병렬 공정을 고려하지 않은 서로 다른 상한이다.");
        report.AppendLine();
        report.AppendLine(
            "| 집단 | 연구 단계 | 기준 게임일 | 기준 플레이타임 | 직접 제작일 | 직접 플레이타임 | 총산업일 | 총산업 플레이타임 |");
        report.AppendLine("|---|---|---:|---:|---:|---:|---:|---:|");
        foreach (RegimeMeasure regime in regimes)
        foreach (IndustryStage stage in stages)
        {
            float directDays = stage.PartyDirectWork
                / Mathf.Max(.01f, regime.CraftSpeed * ApprovedWorkPerDay);
            float grossDays = stage.PartyGrossEwu
                / Mathf.Max(.01f, regime.AssignmentSpeed * ApprovedWorkPerDay * IndustryShare);
            report.Append("| ").Append(regime.Name)
                .Append(" | ").Append(stage.Name)
                .Append(" | ").Append(stage.Day)
                .Append(" | ").Append(Playtime(stage.Day))
                .Append(" | ").Append(F(directDays))
                .Append(" | ").Append(Playtime(directDays))
                .Append(" | ").Append(F(grossDays))
                .Append(" | ").Append(Playtime(grossDays))
                .AppendLine(" |");
        }

        report.AppendLine();
        report.AppendLine("## 무연구 상태와 첫 생산 해금");
        report.AppendLine();
        report.AppendLine(
            "현재 작성된 벌목·채집·채석·조리·가공·의복 경로는 모두 최소 1개 연구를 요구하므로 "
            + "연구 완료 전 신규 물품 생산량은 0이며 Day 1은 시작 재고만 소비한다. 아래 값은 실제 연구 그래프의 "
            + "해당 출력까지 필요한 최소 선행 연구 WU를 최고 학술 담당자의 처리량으로 나눈 값이다. "
            + "연구 시설 건설·재료·동선 시간은 별도이므로 실제 해금은 이보다 늦다.");
        report.AppendLine();
        report.AppendLine(
            "| 집단 | 연구 전담 WU/일 | 원료 첫 해금 | 중간재 첫 해금 | 식량 첫 해금 | 일반 완제품 첫 해금 | 의복 첫 해금 |");
        report.AppendLine("|---|---:|---:|---:|---:|---:|---:|");
        foreach (RegimeMeasure regime in regimes)
        {
            float researchPerDay = Speed(
                    regime,
                    BuiltInCharacterProficiencyIds.Scholarship)
                * ApprovedWorkPerDay;
            report.Append("| ").Append(regime.Name)
                .Append(" | ").Append(F(researchPerDay))
                .Append(" | ").Append(Unlock(
                    firstUnlockWork,
                    ResourceItemKind.Raw,
                    researchPerDay))
                .Append(" | ").Append(Unlock(
                    firstUnlockWork,
                    ResourceItemKind.Intermediate,
                    researchPerDay))
                .Append(" | ").Append(Unlock(
                    firstUnlockWork,
                    ResourceItemKind.Food,
                    researchPerDay))
                .Append(" | ").Append(Unlock(
                    firstUnlockWork,
                    ResourceItemKind.FinishedGood,
                    researchPerDay))
                .Append(" | ").Append(Unlock(
                    firstApparelUnlockWork,
                    researchPerDay))
                .AppendLine(" |");
        }

        report.AppendLine();
        report.AppendLine("## 식량·원료·중간재·일반 완제품·의복 처리량");
        report.AppendLine();
        report.AppendLine(
            "각 칸은 해당 연구 깊이에서 접근 가능한 실제 레시피의 작업량·기대 출력량과 "
            + "그 작업의 실제 숙련 배치를 결합한 중앙 레시피 생산량이다. 괄호는 한 단위를 만드는 실제 플레이타임이다. "
            + "수동 공정과 배치 대기시간 중 더 느린 쪽을 적용했다.");
        report.AppendLine();
        report.AppendLine(
            "| 집단 | 연구 단계 | 원료 | 중간재 | 식량 | 일반 완제품 | 의복 | 접근 출력 경로 수 |");
        report.AppendLine("|---|---|---:|---:|---:|---:|---:|---:|");
        foreach (RegimeMeasure regime in regimes)
        foreach (IndustryStage stage in stages)
        {
            Dictionary<ResourceItemKind, List<float>> rates = ResolveRecipeRates(
                regime,
                stage,
                recipes,
                items,
                researchDepth);
            float apparelRate = ResolveApparelRate(
                regime,
                stage,
                apparel,
                researchDepth);
            report.Append("| ").Append(regime.Name)
                .Append(" | ").Append(stage.Name)
                .Append(" | ").Append(Rate(rates, ResourceItemKind.Raw))
                .Append(" | ").Append(Rate(rates, ResourceItemKind.Intermediate))
                .Append(" | ").Append(Rate(rates, ResourceItemKind.Food))
                .Append(" | ").Append(Rate(rates, ResourceItemKind.FinishedGood))
                .Append(" | ").Append(Rate(apparelRate))
                .Append(" | ").Append(rates.Values.Sum(value => value.Count))
                .AppendLine(" |");
        }

        report.AppendLine();
        report.AppendLine("## 해석 제한");
        report.AppendLine();
        report.AppendLine("- 신화 제작은 BOM과 작업량을 줄이지 않으므로 생산량 계산 보너스로 넣지 않았다.");
        report.AppendLine("- 연구 단계별 세트 수치는 현재 장비 처리량 감사의 물리 BOM·직접 WU·내재 EWU를 재사용했다.");
        report.AppendLine("- 초기/중기 연구는 실제 연구 그래프의 선행 깊이 1/3 이하를 접근 가능 범위로 삼은 결정론적 기준선이며 실제 선택 순서에 따라 개별 레시피는 늦어질 수 있다.");
        report.AppendLine("- 식량·건설·제작을 동시에 최대 속도로 돌릴 수 없으므로 총산업일에는 35% 산업 배정 상한을 적용했다.");
        report.AppendLine("- 이론상 극단은 나이 상한 399 XP와 서로 다른 가족의 작업속도 상위 4개(지구력·즉흥 제작자·야간 체질·부지런함)가 장시간·대체 공정·야간 조건에서 동시에 유효한 상한이다.");

        Directory.CreateDirectory(Path.GetDirectoryName(ReportPath));
        File.WriteAllText(ReportPath, report.ToString(), Encoding.UTF8);
        Debug.Log(
            $"V26_FOUNDER_INDUSTRY=PASS parties={PartyRolls} "
            + $"no-reroll={regimes[0].AssignmentSpeed:F4} "
            + $"extreme={regimes[^1].AssignmentSpeed:F4}");
    }

    public static FounderIndustryBaseline MeasureNaturalFounderBaseline()
    {
        GameDomainContentCatalogSO catalog = AssetDatabase
            .LoadAssetAtPath<GameDomainContentCatalogSO>(CatalogPath)
            ?? throw new InvalidOperationException("Root content catalog is missing.");
        CharacterTraitSO[] traits = catalog.Definitions
            .OfType<CharacterTraitSO>()
            .Where(IsFounderTrait)
            .OrderBy(value => value.id)
            .ToArray();
        Require(traits.Length == 100, $"Founder trait count={traits.Length}.");

        CharacterStartingOriginSO[] origins = LoadAll<CharacterStartingOriginSO>();
        CharacterStartingHistorySO[] histories = LoadAll<CharacterStartingHistorySO>();
        SpeciesLifeHistorySO life = LoadAll<SpeciesLifeHistorySO>()
            .Single(value => string.Equals(
                value.speciesTag,
                "Adventurer",
                StringComparison.Ordinal));
        AgeConditionDefinitionSO[] ageDefinitions =
            LoadAll<AgeConditionDefinitionSO>();
        CharacterStartingAgeCondition[] conditions = ageDefinitions
            .Select(value => new CharacterStartingAgeCondition(
                value.conditionId,
                value.constructCondition))
            .ToArray();
        CharacterSpeciesSO founderSpecies = catalog
            .GetAll<CharacterSpeciesSO>()
            .Single(value => string.Equals(
                value.speciesTag,
                "Adventurer",
                StringComparison.Ordinal));
        AnatomyProfileSO anatomy = LoadAll<AnatomyProfileSO>()
            .Single(value => string.Equals(
                value.ProfileId,
                founderSpecies.anatomyProfileId,
                StringComparison.Ordinal));
        CharacterPerformanceFormulaDefinitionSO[] performanceFormulas =
            catalog.GetAll<CharacterPerformanceFormulaDefinitionSO>()
                .ToArray();
        CharacterStartingLifeHistory startingLife = new(
            life.adultAgeYears,
            life.elderAgeYears,
            life.untreatedExpectedLifeYears,
            life.construct);

        PartyMeasure[] parties = BuildPartyMeasures(
            PartyRolls,
            startingLife,
            origins,
            histories,
            conditions,
            traits,
            anatomy,
            ageDefinitions,
            performanceFormulas,
            applyTraits: true);
        RegimeMeasure natural = MeanRegime(
            "natural",
            parties,
            "first deterministic party");
        return new FounderIndustryBaseline(
            natural.AssignmentSpeed,
            natural.CraftSpeed,
            Speed(natural, BuiltInCharacterProficiencyIds.Scholarship),
            natural.ConsumptionIndex,
            natural.ExpectedShiftAccidents,
            natural.AssignedLearningMultiplier);
    }

    public static IReadOnlyList<FounderIndustryDistribution>
        MeasureFounderIndustryDistributions()
    {
        PartyMeasure[] parties = BuildNaturalPartyMeasures();
        return new[]
        {
            CreateDistribution("natural", parties),
            CreateDistribution("compromise-3", SelectBlockWinners(parties, 3)),
            CreateDistribution("upper-20", SelectBlockWinners(parties, 20))
        };
    }

    private static PartyMeasure[] BuildNaturalPartyMeasures()
    {
        GameDomainContentCatalogSO catalog = AssetDatabase
            .LoadAssetAtPath<GameDomainContentCatalogSO>(CatalogPath)
            ?? throw new InvalidOperationException("Root content catalog is missing.");
        CharacterTraitSO[] traits = catalog.Definitions
            .OfType<CharacterTraitSO>()
            .Where(IsFounderTrait)
            .OrderBy(value => value.id)
            .ToArray();
        Require(traits.Length == 100, $"Founder trait count={traits.Length}.");

        CharacterStartingOriginSO[] origins = LoadAll<CharacterStartingOriginSO>();
        CharacterStartingHistorySO[] histories = LoadAll<CharacterStartingHistorySO>();
        SpeciesLifeHistorySO life = LoadAll<SpeciesLifeHistorySO>()
            .Single(value => string.Equals(
                value.speciesTag,
                "Adventurer",
                StringComparison.Ordinal));
        AgeConditionDefinitionSO[] ageDefinitions =
            LoadAll<AgeConditionDefinitionSO>();
        CharacterStartingAgeCondition[] conditions = ageDefinitions
            .Select(value => new CharacterStartingAgeCondition(
                value.conditionId,
                value.constructCondition))
            .ToArray();
        CharacterSpeciesSO founderSpecies = catalog
            .GetAll<CharacterSpeciesSO>()
            .Single(value => string.Equals(
                value.speciesTag,
                "Adventurer",
                StringComparison.Ordinal));
        AnatomyProfileSO anatomy = LoadAll<AnatomyProfileSO>()
            .Single(value => string.Equals(
                value.ProfileId,
                founderSpecies.anatomyProfileId,
                StringComparison.Ordinal));
        CharacterPerformanceFormulaDefinitionSO[] performanceFormulas =
            catalog.GetAll<CharacterPerformanceFormulaDefinitionSO>()
                .ToArray();
        CharacterStartingLifeHistory startingLife = new(
            life.adultAgeYears,
            life.elderAgeYears,
            life.untreatedExpectedLifeYears,
            life.construct);

        return BuildPartyMeasures(
            PartyRolls,
            startingLife,
            origins,
            histories,
            conditions,
            traits,
            anatomy,
            ageDefinitions,
            performanceFormulas,
            applyTraits: true);
    }

    private static FounderIndustryDistribution CreateDistribution(
        string regimeId,
        IReadOnlyList<PartyMeasure> source)
    {
        float[] assignment = source
            .Select(value => value.AssignmentSpeed)
            .OrderBy(value => value)
            .ToArray();
        float[] craft = source
            .Select(value => value.CraftSpeed)
            .OrderBy(value => value)
            .ToArray();
        float[] research = source
            .Select(value => value.BestSpeeds[
                BuiltInCharacterProficiencyIds.Scholarship.Value])
            .OrderBy(value => value)
            .ToArray();
        float[] consumption = source
            .Select(value => value.ConsumptionIndex)
            .OrderBy(value => value)
            .ToArray();
        return new FounderIndustryDistribution(
            regimeId,
            source.Count,
            Percentile(assignment, .10f),
            Percentile(assignment, .50f),
            Percentile(assignment, .90f),
            Percentile(craft, .10f),
            Percentile(craft, .50f),
            Percentile(craft, .90f),
            Percentile(research, .10f),
            Percentile(research, .50f),
            Percentile(research, .90f),
            Percentile(consumption, .10f),
            Percentile(consumption, .50f),
            Percentile(consumption, .90f));
    }

    private static PartyMeasure[] BuildPartyMeasures(
        int partyCount,
        CharacterStartingLifeHistory startingLife,
        IReadOnlyList<CharacterStartingOriginSO> origins,
        IReadOnlyList<CharacterStartingHistorySO> histories,
        IReadOnlyList<CharacterStartingAgeCondition> conditions,
        IReadOnlyList<CharacterTraitSO> traits,
        AnatomyProfileSO anatomy,
        IReadOnlyList<AgeConditionDefinitionSO> ageDefinitions,
        IReadOnlyList<CharacterPerformanceFormulaDefinitionSO> performanceFormulas,
        bool applyTraits)
    {
        PartyMeasure[] parties = new PartyMeasure[partyCount];
        for (int partyIndex = 0; partyIndex < parties.Length; partyIndex++)
        {
            Candidate[] members = new Candidate[3];
            for (int memberIndex = 0; memberIndex < members.Length; memberIndex++)
            {
                int seed = CharacterGrowthRules.StableHash(
                    $"v26-founder-industry:{partyIndex}:{memberIndex}");
                members[memberIndex] = CreateCandidate(
                    seed,
                    startingLife,
                    origins,
                    histories,
                    conditions,
                    traits,
                    anatomy,
                    ageDefinitions,
                    performanceFormulas,
                    applyTraits);
            }
            parties[partyIndex] = Measure(members);
        }
        return parties;
    }

    private static Candidate CreateCandidate(
        int seed,
        CharacterStartingLifeHistory life,
        IReadOnlyList<CharacterStartingOriginSO> origins,
        IReadOnlyList<CharacterStartingHistorySO> histories,
        IReadOnlyList<CharacterStartingAgeCondition> conditions,
        IReadOnlyList<CharacterTraitSO> traits,
        AnatomyProfileSO anatomy,
        IReadOnlyList<AgeConditionDefinitionSO> ageDefinitions,
        IReadOnlyList<CharacterPerformanceFormulaDefinitionSO> performanceFormulas,
        bool applyTraits)
    {
        int originIndex = PositiveModulo(
            CharacterGrowthRules.StableHash($"{seed}:origin"), origins.Count);
        int historyIndex = PositiveModulo(
            CharacterGrowthRules.StableHash($"{seed}:history"), histories.Count);
        CharacterStartingProfileRoll roll = CharacterStartingProfileRules.Create(
            seed,
            life,
            origins[originIndex],
            histories[historyIndex],
            conditions);
        IReadOnlyList<int> selectedIds = CharacterTraitSelectionRules.Select(
            traits,
            Array.Empty<CharacterTraitConflictRule>(),
            new DeterministicRandomSequence(
                CharacterGrowthRules.StableHash($"{seed}:traits")),
            "Adventurer");
        CharacterTraitSO[] selected = applyTraits
            ? selectedIds
                .Select(id => traits.Single(value => value.id == id))
                .ToArray()
            : Array.Empty<CharacterTraitSO>();
        List<CharacterStartingProficiencyExperience> starts = roll.Proficiencies
            .Select(value => value.Clone())
            .ToList();
        CharacterTraitStartingProficiencyRules.Apply(
            starts,
            selected,
            CharacterStartingProfileRules.ResolveAgeCap(roll.Profile.ageBand));
        float generalWork = CharacterGameplayEffectProjector.Resolve(
            GameplayEffectTargetIds.WorkSpeed,
            1f,
            selected.Cast<IGameplayEffectSource>()).Value;
        float researchWork = CharacterGameplayEffectProjector.Resolve(
            GameplayEffectTargetIds.ResearchSpeed,
            1f,
            selected.Cast<IGameplayEffectSource>()).Value;
        float accidentChance = CharacterGameplayEffectProjector.Resolve(
            GameplayEffectTargetIds.AccidentChance,
            1f,
            selected.Cast<IGameplayEffectSource>()).Value;
        IReadOnlyDictionary<CharacterFunctionalCapacityId, float> capacities =
            BuildInitialFunctionalCapacities(
                roll.Profile.initialAgeConditionIds,
                anatomy,
                ageDefinitions);
        CharacterPerformanceFormulaDefinitionSO consumptionFormula =
            performanceFormulas.Single(value => value.FormulaId
                == "performance:survival:food-consumption");
        Candidate candidate = new()
        {
            ConsumptionMultiplier = CharacterGameplayEffectProjector.Resolve(
                GameplayEffectTargetIds.Consumption,
                1f,
                selected.Cast<IGameplayEffectSource>()).Value
                * ResolveFunctionalFactor(
                    consumptionFormula,
                    capacities),
            EarnedExperienceMultiplier = CharacterGameplayEffectProjector.Resolve(
                GameplayEffectTargetIds.EarnedWorkExperience,
                1f,
                selected.Cast<IGameplayEffectSource>()).Value
        };
        foreach (CharacterStartingProficiencyExperience start in starts)
        {
            CharacterProficiencyEffectSnapshot proficiency =
                ProficiencyProgressionRules.ResolveEffects(
                    start.experience * ProficiencyProgressionRules.MilliPerExperience);
            CharacterPerformanceFormulaDefinitionSO speedFormula =
                ResolveRepresentativeFormula(
                    start.proficiencyId,
                    CharacterPerformanceResultChannel.Speed,
                    performanceFormulas);
            CharacterPerformanceFormulaDefinitionSO accidentFormula =
                ResolveRepresentativeFormula(
                    start.proficiencyId,
                    CharacterPerformanceResultChannel.AccidentRisk,
                    performanceFormulas);
            candidate.Speeds[start.proficiencyId] = proficiency.WorkSpeedMultiplier
                * ResolveFunctionalFactor(speedFormula, capacities)
                * generalWork
                * (start.proficiencyId
                        == BuiltInCharacterProficiencyIds.Scholarship.Value
                    ? researchWork
                    : 1f);
            candidate.AccidentMultipliers[start.proficiencyId] =
                proficiency.AccidentMultiplier
                * ResolveFunctionalFactor(accidentFormula, capacities)
                * accidentChance;
        }
        return candidate;
    }

    private static CharacterPerformanceFormulaDefinitionSO
        ResolveRepresentativeFormula(
            string proficiencyId,
            CharacterPerformanceResultChannel channel,
            IReadOnlyList<CharacterPerformanceFormulaDefinitionSO> formulas)
    {
        WorkTypeId workTypeId = proficiencyId switch
        {
            "proficiency:fieldwork" => BuiltInWorkTypeIds.Gather,
            "proficiency:construction-engineering" =>
                BuiltInWorkTypeIds.Construct,
            "proficiency:crafting" => BuiltInWorkTypeIds.Craft,
            "proficiency:food-production" => BuiltInWorkTypeIds.Cook,
            "proficiency:scholarship" => BuiltInWorkTypeIds.Research,
            "proficiency:medicine" => BuiltInWorkTypeIds.Treat,
            "proficiency:social" => BuiltInWorkTypeIds.Reception,
            "proficiency:melee-combat" => BuiltInWorkTypeIds.Guard,
            "proficiency:ranged-combat" => BuiltInWorkTypeIds.Guard,
            _ => throw new InvalidOperationException(
                $"No representative performance formula for '{proficiencyId}'.")
        };
        return formulas.Single(value =>
            value.ExecutionWorkTypeId == workTypeId.Value
                && value.ResultChannel == channel);
    }

    private static IReadOnlyDictionary<CharacterFunctionalCapacityId, float>
        BuildInitialFunctionalCapacities(
            IReadOnlyList<string> initialAgeConditionIds,
            AnatomyProfileSO anatomy,
            IReadOnlyList<AgeConditionDefinitionSO> ageDefinitions)
    {
        if (anatomy == null)
            throw new ArgumentNullException(nameof(anatomy));
        Dictionary<string, int> damageStacks = anatomy.Nodes
            .ToDictionary(
                value => value.NodeId,
                _ => 0,
                StringComparer.Ordinal);
        Dictionary<string, AgeConditionDefinitionSO> conditions =
            ageDefinitions
                .Where(value => value != null
                    && !string.IsNullOrWhiteSpace(value.conditionId))
                .ToDictionary(
                    value => value.conditionId,
                    StringComparer.Ordinal);
        foreach (string conditionId in initialAgeConditionIds
                     ?? Array.Empty<string>())
        {
            if (!conditions.TryGetValue(
                    conditionId,
                    out AgeConditionDefinitionSO condition))
                throw new InvalidOperationException(
                    $"Initial age condition '{conditionId}' is not authored.");
            bool matched = false;
            foreach (string nodeId in condition.affectedAnatomyNodeIds
                         ?? new List<string>())
            {
                if (!damageStacks.ContainsKey(nodeId))
                    continue;
                damageStacks[nodeId]++;
                matched = true;
            }
            if (!matched)
                throw new InvalidOperationException(
                    $"Initial age condition '{conditionId}' has no node in "
                    + $"anatomy '{anatomy.ProfileId}'.");
        }

        Dictionary<CharacterFunctionalCapacityId, float> result = new();
        foreach (CharacterFunctionalCapacityId capacityId in Enum
                     .GetValues(typeof(CharacterFunctionalCapacityId))
                     .Cast<CharacterFunctionalCapacityId>())
        {
            AnatomyFunction function = ToAnatomyFunction(capacityId);
            AnatomyNodeDefinition[] producers = anatomy.Nodes
                .Where(value => (value.ExpandedFunctions & function) != 0)
                .ToArray();
            if (producers.Length == 0)
                throw new InvalidOperationException(
                    $"Founder anatomy '{anatomy.ProfileId}' has no producer "
                    + $"for {CharacterFunctionalCapacityIds.GetStableId(capacityId)}.");
            float total = 0f;
            float weights = 0f;
            foreach (AnatomyNodeDefinition producer in producers)
            {
                float weight = Mathf.Max(.01f, producer.CapacityWeight);
                float efficiency = Mathf.Max(
                    0f,
                    1f - .05f * damageStacks[producer.NodeId]);
                total += efficiency * weight;
                weights += weight;
            }
            result[capacityId] = total / weights;
        }
        return result;
    }

    private static float ResolveFunctionalFactor(
        CharacterPerformanceFormulaDefinitionSO formula,
        IReadOnlyDictionary<CharacterFunctionalCapacityId, float> capacities)
    {
        float weightedTotal = 0f;
        float totalWeight = 0f;
        float bottleneck = float.PositiveInfinity;
        foreach (CharacterPerformanceCapacityInput input in formula.CapacityInputs)
        {
            float capacity = capacities[input.CapacityId];
            if ((input.Role & CharacterPerformanceInputRole.Required) != 0
                && capacity < (input.RequiredThreshold > 0f
                    ? input.RequiredThreshold
                    : .10f))
                return 0f;
            if ((input.Role & CharacterPerformanceInputRole.Contribution) != 0
                && input.Weight > 0f)
            {
                weightedTotal += input.Weight * capacity;
                totalWeight += input.Weight;
            }
            if ((input.Role & CharacterPerformanceInputRole.Bottleneck) != 0)
                bottleneck = Mathf.Min(
                    bottleneck,
                    .25f + .75f * capacity);
        }
        if (totalWeight <= 0f)
            throw new InvalidOperationException(
                $"Formula '{formula.FormulaId}' has no capacity contribution.");
        float raw = Mathf.Min(weightedTotal / totalWeight, bottleneck);
        return formula.ResultChannel is
                CharacterPerformanceResultChannel.AccidentRisk
                or CharacterPerformanceResultChannel.Consumption
                or CharacterPerformanceResultChannel.Exposure
            ? 1f / Mathf.Max(.05f, raw)
            : raw;
    }

    private static AnatomyFunction ToAnatomyFunction(
        CharacterFunctionalCapacityId capacityId) => capacityId switch
    {
        CharacterFunctionalCapacityId.MentalMaintenance =>
            AnatomyFunction.MentalMaintenance,
        CharacterFunctionalCapacityId.VisualDiscernment =>
            AnatomyFunction.VisualDiscernment,
        CharacterFunctionalCapacityId.AuditorySensing =>
            AnatomyFunction.AuditorySensing,
        CharacterFunctionalCapacityId.RespiratoryExchange =>
            AnatomyFunction.RespiratoryExchange,
        CharacterFunctionalCapacityId.PowerCirculation =>
            AnatomyFunction.PowerCirculation,
        CharacterFunctionalCapacityId.IntakeProcessing =>
            AnatomyFunction.IntakeProcessing,
        CharacterFunctionalCapacityId.PurificationProcessing =>
            AnatomyFunction.PurificationProcessing,
        CharacterFunctionalCapacityId.VitalityResponse =>
            AnatomyFunction.VitalityResponse,
        CharacterFunctionalCapacityId.PhysicalPower =>
            AnatomyFunction.PhysicalPower,
        CharacterFunctionalCapacityId.PrecisionManipulation =>
            AnatomyFunction.PrecisionManipulation,
        CharacterFunctionalCapacityId.PhysicalMobility =>
            AnatomyFunction.PhysicalMobility,
        CharacterFunctionalCapacityId.Communication =>
            AnatomyFunction.Communication,
        CharacterFunctionalCapacityId.ArcaneConduction =>
            AnatomyFunction.ArcaneConduction,
        CharacterFunctionalCapacityId.ImmuneDefense =>
            AnatomyFunction.ImmuneDefense,
        _ => throw new ArgumentOutOfRangeException(
            nameof(capacityId),
            capacityId,
            null)
    };

    private static PartyMeasure Measure(IReadOnlyList<Candidate> party)
    {
        float bestAssignment = 0f;
        int bestFirst = 0;
        int bestSecond = 1;
        int bestThird = 2;
        for (int first = 0; first < 3; first++)
        for (int second = 0; second < 3; second++)
        for (int third = 0; third < 3; third++)
        {
            if (first == second || first == third || second == third)
                continue;
            float assignment = party[first].Speed(ManufacturingJobs[0])
                + party[second].Speed(ManufacturingJobs[1])
                + party[third].Speed(ManufacturingJobs[2]);
            if (assignment <= bestAssignment)
                continue;
            bestAssignment = assignment;
            bestFirst = first;
            bestSecond = second;
            bestThird = third;
        }
        int[] assigned = { bestFirst, bestSecond, bestThird };
        float expectedShiftAccidents = 0f;
        float learning = 0f;
        for (int index = 0; index < ManufacturingJobs.Length; index++)
        {
            Candidate member = party[assigned[index]];
            expectedShiftAccidents += 1f - Mathf.Exp(
                -.001f
                * ApprovedWorkPerDay
                * member.AccidentMultiplier(ManufacturingJobs[index]));
            learning += member.EarnedExperienceMultiplier;
        }
        return new PartyMeasure(
            bestAssignment,
            party.Max(value => value.Speed(
                BuiltInCharacterProficiencyIds.Crafting)),
            BuiltInCharacterProficiencyIds.All.ToDictionary(
                id => id.Value,
                id => party.Max(value => value.Speed(id)),
                StringComparer.Ordinal),
            party.Sum(value => value.ConsumptionMultiplier),
            expectedShiftAccidents,
            learning / ManufacturingJobs.Length);
    }

    private static RegimeMeasure MeanRegime(
        string name,
        IReadOnlyList<PartyMeasure> source,
        string definition) =>
        new(
            name,
            source.Average(value => value.AssignmentSpeed),
            source.Average(value => value.CraftSpeed),
            BuiltInCharacterProficiencyIds.All.ToDictionary(
                id => id.Value,
                id => source.Average(value => value.BestSpeeds[id.Value]),
                StringComparer.Ordinal),
            source.Average(value => value.ConsumptionIndex),
            source.Average(value => value.ExpectedShiftAccidents),
            source.Average(value => value.AssignedLearningMultiplier),
            definition);

    private static RegimeMeasure BestOfBlocks(
        string name,
        IReadOnlyList<PartyMeasure> source,
        int blockSize,
        string definition)
    {
        IReadOnlyList<PartyMeasure> winners =
            SelectBlockWinners(source, blockSize);
        return new RegimeMeasure(
            name,
            winners.Average(value => value.AssignmentSpeed),
            winners.Average(value => value.CraftSpeed),
            BuiltInCharacterProficiencyIds.All.ToDictionary(
                id => id.Value,
                id => winners.Average(value => value.BestSpeeds[id.Value]),
                StringComparer.Ordinal),
            winners.Average(value => value.ConsumptionIndex),
            winners.Average(value => value.ExpectedShiftAccidents),
            winners.Average(value => value.AssignedLearningMultiplier),
            definition);
    }

    private static IReadOnlyList<PartyMeasure> SelectBlockWinners(
        IReadOnlyList<PartyMeasure> source,
        int blockSize)
    {
        List<PartyMeasure> winners = new();
        for (int start = 0; start + blockSize <= source.Count; start += blockSize)
        {
            winners.Add(source.Skip(start).Take(blockSize)
                .OrderByDescending(value => value.AssignmentSpeed)
                .ThenByDescending(value => value.CraftSpeed)
                .First());
        }
        return winners;
    }

    private static void AppendPartyDistribution(
        StringBuilder report,
        string label,
        IReadOnlyList<PartyMeasure> source)
    {
        AppendDistributionRow(
            report,
            label,
            "필수산업 합산 WU/일",
            source.Select(value =>
                value.AssignmentSpeed * ApprovedWorkPerDay));
        AppendDistributionRow(
            report,
            label,
            "최고 제작자 WU/일",
            source.Select(value =>
                value.CraftSpeed * ApprovedWorkPerDay));
        AppendDistributionRow(
            report,
            label,
            "최고 연구자 WU/일",
            source.Select(value =>
                value.BestSpeeds[
                    BuiltInCharacterProficiencyIds.Scholarship.Value]
                * ApprovedWorkPerDay));
        AppendDistributionRow(
            report,
            label,
            "3인 음식 소비 지수",
            source.Select(value => value.ConsumptionIndex));
        AppendDistributionRow(
            report,
            label,
            "사고 기대건/일",
            source.Select(value => value.ExpectedShiftAccidents));
    }

    private static void AppendDistributionRow(
        StringBuilder report,
        string label,
        string metric,
        IEnumerable<float> values)
    {
        float[] ordered = values.OrderBy(value => value).ToArray();
        report.Append("| ").Append(label)
            .Append(" | ").Append(metric)
            .Append(" | ").Append(F(Percentile(ordered, .10f)))
            .Append(" | ").Append(F(Percentile(ordered, .50f)))
            .Append(" | ").Append(F(Percentile(ordered, .90f)))
            .AppendLine(" |");
    }

    private static RegimeMeasure TheoreticalRegime()
    {
        float proficiency = ProficiencyProgressionRules.ResolveEffects(
                399L * ProficiencyProgressionRules.MilliPerExperience)
            .WorkSpeedMultiplier;
        float traitMultiplier = 1.05f * 1.03f * 1.08f * 1.08f * 1.05f;
        float speed = proficiency * traitMultiplier;
        return new RegimeMeasure(
            "이론상 극단 리롤",
            speed * ManufacturingJobs.Length,
            speed,
            BuiltInCharacterProficiencyIds.All.ToDictionary(
                id => id.Value,
                _ => speed,
                StringComparer.Ordinal),
            3f,
            3f * (1f - Mathf.Exp(-.001f * ApprovedWorkPerDay * .60f)),
            1.30f,
            "나이 상한 XP와 합법적인 서로 다른 가족 작업속도 특성 4개를 모두 충족");
    }

    private static Dictionary<ResourceItemKind, List<float>> ResolveRecipeRates(
        RegimeMeasure regime,
        IndustryStage stage,
        IEnumerable<ProductionRecipeSO> recipes,
        IEnumerable<ResourceItemDefinitionSO> items,
        IReadOnlyDictionary<string, int> researchDepth)
    {
        Dictionary<string, ResourceItemDefinitionSO> itemById = items
            .Where(value => value != null)
            .ToDictionary(value => value.ItemId, StringComparer.Ordinal);
        Dictionary<ResourceItemKind, List<float>> result = new();
        foreach (ProductionRecipeSO recipe in recipes
                     .Where(value => value != null)
                     .Where(value => IsAccessible(
                         value.RequiredResearchId,
                         stage.MaximumResearchDepth,
                         researchDepth)))
        {
            float workerSpeed = ResolveWorkerSpeed(
                regime,
                recipe.Proficiency,
                recipe.WorkTypeId);
            float manualWork = recipe.ProcessKind == ProductionProcessKind.PassiveBatch
                ? recipe.PreparationWork + recipe.FinishingWork
                : recipe.RequiredWork;
            float batchesPerDay = ApprovedWorkPerDay * workerSpeed
                / Mathf.Max(.1f, manualWork);
            if (recipe.ProcessKind == ProductionProcessKind.PassiveBatch)
                batchesPerDay = Mathf.Min(
                    batchesPerDay,
                    24f / Mathf.Max(.1f, recipe.ProcessingGameHours));
            foreach (ProductionOutputDefinition output in recipe.Outputs)
            {
                if (output == null
                    || !itemById.TryGetValue(output.ItemId, out ResourceItemDefinitionSO item)
                    || !IsAccessible(
                        item.RequiredResearchId,
                        stage.MaximumResearchDepth,
                        researchDepth))
                    continue;
                if (!result.TryGetValue(item.Kind, out List<float> values))
                {
                    values = new List<float>();
                    result.Add(item.Kind, values);
                }
                values.Add(batchesPerDay * output.Amount * output.Probability);
            }
        }
        return result;
    }

    private static float ResolveApparelRate(
        RegimeMeasure regime,
        IndustryStage stage,
        IEnumerable<ApparelDefinitionSO> apparel,
        IReadOnlyDictionary<string, int> researchDepth)
    {
        float speed = Speed(
            regime,
            BuiltInCharacterProficiencyIds.Crafting);
        float[] rates = apparel
            .Where(value => value != null)
            .Where(value => IsAccessible(
                value.RequiredResearchId,
                stage.MaximumResearchDepth,
                researchDepth))
            .Select(value =>
            {
                int points = CountBits((uint)value.OccupiedPoints);
                int area = Mathf.Clamp(
                    Mathf.CeilToInt(value.TailoringCoefficient),
                    1,
                    5);
                float work = 10f + area * 12f + points * 4f;
                return ApprovedWorkPerDay * speed / work;
            })
            .OrderBy(value => value)
            .ToArray();
        return Median(rates);
    }

    private static float ResolveWorkerSpeed(
        RegimeMeasure regime,
        ProficiencyWorkProfileAuthoring authored,
        WorkTypeId workTypeId)
    {
        if (authored != null && authored.IsValid)
        {
            float primary = Speed(regime, authored.Primary);
            if (!authored.Secondary.IsValid
                || authored.CombinationMode == ProficiencyCombinationMode.PrimaryOnly)
                return primary;
            float secondary = Speed(regime, authored.Secondary);
            return authored.CombinationMode == ProficiencyCombinationMode.Higher
                ? Mathf.Max(primary, secondary)
                : primary * authored.PrimaryWeight
                    + secondary * authored.SecondaryWeight;
        }
        if (WorkTypeProficiencyRules.TryResolve(
                workTypeId,
                out ProficiencyWorkProfile profile))
            return Speed(regime, profile.Primary) * profile.PrimaryWeight
                + Speed(regime, profile.Secondary) * profile.SecondaryWeight;
        return Speed(regime, BuiltInCharacterProficiencyIds.Crafting);
    }

    private static float Speed(
        RegimeMeasure regime,
        CharacterProficiencyId proficiencyId) =>
        proficiencyId.IsValid
        && regime.BestSpeeds.TryGetValue(proficiencyId.Value, out float value)
            ? value
            : 0f;

    private static Dictionary<string, int> BuildResearchDepth(
        IEnumerable<ResearchProjectSO> projects)
    {
        Dictionary<string, ResearchProjectSO> byId = projects
            .Where(value => value != null && value.ProjectId.IsValid)
            .ToDictionary(value => value.ProjectId.Value, StringComparer.Ordinal);
        Dictionary<string, int> result = new(StringComparer.Ordinal);
        foreach (ResearchProjectSO project in byId.Values)
            ResolveResearchDepth(project, result, new HashSet<string>(StringComparer.Ordinal));
        return result;
    }

    private static Dictionary<ResourceItemKind, float>
        ResolveFirstRecipeUnlockWork(
            IEnumerable<ProductionRecipeSO> recipes,
            IEnumerable<ResourceItemDefinitionSO> items,
            IEnumerable<ResearchProjectSO> projects)
    {
        Dictionary<string, ResourceItemDefinitionSO> itemById = items
            .Where(value => value != null)
            .ToDictionary(value => value.ItemId, StringComparer.Ordinal);
        Dictionary<string, ResearchProjectSO> projectById = projects
            .Where(value => value != null && value.ProjectId.IsValid)
            .ToDictionary(value => value.ProjectId.Value, StringComparer.Ordinal);
        Dictionary<ResourceItemKind, float> result = new();
        foreach (ProductionRecipeSO recipe in recipes.Where(value => value != null))
        foreach (ProductionOutputDefinition output in recipe.Outputs)
        {
            if (output == null
                || !itemById.TryGetValue(
                    output.ItemId,
                    out ResourceItemDefinitionSO item))
                continue;
            float work = ResolveResearchClosureWork(
                projectById,
                recipe.RequiredResearchId,
                item.RequiredResearchId);
            if (!result.TryGetValue(item.Kind, out float current)
                || work < current)
                result[item.Kind] = work;
        }
        return result;
    }

    private static float ResolveFirstApparelUnlockWork(
        IEnumerable<ApparelDefinitionSO> apparel,
        IEnumerable<ResearchProjectSO> projects)
    {
        Dictionary<string, ResearchProjectSO> projectById = projects
            .Where(value => value != null && value.ProjectId.IsValid)
            .ToDictionary(value => value.ProjectId.Value, StringComparer.Ordinal);
        float[] values = apparel
            .Where(value => value != null)
            .Select(value => ResolveResearchClosureWork(
                projectById,
                value.RequiredResearchId))
            .OrderBy(value => value)
            .ToArray();
        return values.Length == 0 ? float.PositiveInfinity : values[0];
    }

    private static float ResolveResearchClosureWork(
        IReadOnlyDictionary<string, ResearchProjectSO> projectById,
        params string[] projectIds)
    {
        HashSet<string> closure = new(StringComparer.Ordinal);
        HashSet<string> visiting = new(StringComparer.Ordinal);
        foreach (string projectId in projectIds)
            AddResearchClosure(projectId, projectById, closure, visiting);
        return closure.Sum(id => projectById[id].RequiredWork);
    }

    private static void AddResearchClosure(
        string projectId,
        IReadOnlyDictionary<string, ResearchProjectSO> projectById,
        ISet<string> closure,
        ISet<string> visiting)
    {
        string id = projectId?.Trim() ?? string.Empty;
        if (id.Length == 0 || closure.Contains(id))
            return;
        if (!projectById.TryGetValue(id, out ResearchProjectSO project))
            throw new InvalidOperationException(
                $"Research definition '{id}' is missing from the root catalog.");
        if (!visiting.Add(id))
            throw new InvalidOperationException(
                $"Research prerequisite cycle at '{id}'.");
        foreach (ResearchProjectSO prerequisite in project.Prerequisites)
        {
            if (prerequisite == null || !prerequisite.ProjectId.IsValid)
                throw new InvalidOperationException(
                    $"Research '{id}' has an invalid prerequisite.");
            AddResearchClosure(
                prerequisite.ProjectId.Value,
                projectById,
                closure,
                visiting);
        }
        visiting.Remove(id);
        closure.Add(id);
    }

    private static int ResolveResearchDepth(
        ResearchProjectSO project,
        IDictionary<string, int> resolved,
        ISet<string> visiting)
    {
        string id = project.ProjectId.Value;
        if (resolved.TryGetValue(id, out int known))
            return known;
        if (!visiting.Add(id))
            throw new InvalidOperationException($"Research prerequisite cycle at '{id}'.");
        int depth = project.Prerequisites.Count == 0
            ? 0
            : 1 + project.Prerequisites.Max(value => ResolveResearchDepth(
                value,
                resolved,
                visiting));
        visiting.Remove(id);
        resolved[id] = depth;
        return depth;
    }

    private static bool IsAccessible(
        string requiredResearchId,
        int maximumDepth,
        IReadOnlyDictionary<string, int> researchDepth)
    {
        string id = requiredResearchId?.Trim() ?? string.Empty;
        if (id.Length == 0)
            return true;
        return maximumDepth >= 0
            && researchDepth.TryGetValue(id, out int depth)
            && depth <= maximumDepth;
    }

    private static int CountBits(uint value)
    {
        int count = 0;
        while (value != 0)
        {
            count += (int)(value & 1u);
            value >>= 1;
        }
        return count;
    }

    private static string Rate(
        IReadOnlyDictionary<ResourceItemKind, List<float>> rates,
        ResourceItemKind kind) => rates.TryGetValue(kind, out List<float> values)
            ? Rate(Median(values.OrderBy(value => value).ToArray()))
            : "0";

    private static string Rate(float unitsPerDay) => unitsPerDay <= 0f
        ? "0"
        : $"{F(unitsPerDay)}/일 ({Playtime(1f / unitsPerDay)})";

    private static string Unlock(
        IReadOnlyDictionary<ResourceItemKind, float> workByKind,
        ResourceItemKind kind,
        float researchPerDay) => workByKind.TryGetValue(kind, out float work)
            ? Unlock(work, researchPerDay)
            : "경로 없음";

    private static string Unlock(float work, float researchPerDay)
    {
        if (!float.IsFinite(work) || researchPerDay <= 0f)
            return "경로 없음";
        float days = work / researchPerDay;
        return $"{F(days)}일 ({Playtime(days)})";
    }

    private static float Median(IReadOnlyList<float> ordered) =>
        ordered == null || ordered.Count == 0
            ? 0f
            : ordered.Count % 2 == 1
                ? ordered[ordered.Count / 2]
                : (ordered[ordered.Count / 2 - 1]
                    + ordered[ordered.Count / 2]) * .5f;

    private static float Percentile(
        IReadOnlyList<float> ordered,
        float percentile)
    {
        if (ordered == null || ordered.Count == 0)
            return 0f;
        float position = Mathf.Clamp01(percentile)
            * (ordered.Count - 1);
        int lower = Mathf.FloorToInt(position);
        int upper = Mathf.CeilToInt(position);
        return Mathf.Lerp(
            ordered[lower],
            ordered[upper],
            position - lower);
    }

    private static bool IsFounderTrait(CharacterTraitSO value) =>
        value != null && (V26FounderTraitContentBuilder.RetainedIds.Contains(value.id)
            || value.id is >= 247 and <= 259
            || value.id is >= 300 and <= 306
            || value.id is >= 400 and <= 417
            || value.id is >= 500 and <= 518);

    private static T[] LoadAll<T>() where T : UnityEngine.Object =>
        AssetDatabase.FindAssets($"t:{typeof(T).Name}")
            .Select(AssetDatabase.GUIDToAssetPath)
            .Select(AssetDatabase.LoadAssetAtPath<T>)
            .Where(value => value != null)
            .OrderBy(value => value.name, StringComparer.Ordinal)
            .ToArray();

    private static int PositiveModulo(int value, int count) =>
        (int)((uint)value % (uint)count);

    private static void AppendComparison(
        StringBuilder report,
        string label,
        float traitFree,
        float actual)
    {
        float percent = traitFree > .0001f
            ? (actual / traitFree - 1f) * 100f
            : 0f;
        report.Append("| ").Append(label)
            .Append(" | ").Append(F(traitFree))
            .Append(" | ").Append(F(actual))
            .Append(" | ").Append(percent >= 0f ? "+" : string.Empty)
            .Append(percent.ToString("0.00", CultureInfo.InvariantCulture))
            .AppendLine("% |");
    }

    private static string F(float value) =>
        value.ToString("0.000", CultureInfo.InvariantCulture);

    private static string Playtime(float days)
    {
        float hours = Mathf.Max(0f, days)
            * GameCalendarRules.SecondsPerDay
            / 3600f;
        return hours < 1f ? $"{hours * 60f:0.0}m" : $"{hours:0.00}h";
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
#endif
