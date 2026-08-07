#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class V20TraitContentAssetBuilder
{
    private const string CatalogPath = "Assets/Resources/SO/Content/GameDomainContentCatalog.asset";
    private const string GeneralRoot = "Assets/Resources/SO/V20/Traits/General";
    private const string HeritableRoot = "Assets/Resources/SO/V20/Traits/Heritable";

    private sealed class GeneralSpec
    {
        public int Id;
        public string Key, Name, Description, Behavior, Trigger, EventCategory, Conflict;
        public float Utility, Mood, EventWeight, Work, Research, Combat, Move, Consumption, Accident;
    }

    private sealed class HeritableSpec
    {
        public string Id, Name, Description, Target, Conflict;
        public HeritableTraitCategory Category;
        public HeritableTraitConsequenceKind Kind;
        public float Delta;
        public int Aptitude;
    }

    private static readonly GeneralSpec[] General =
    {
        G(200,"tireless","지구력","긴 교대 뒤에도 작업 리듬을 쉽게 잃지 않는다.","work:long-shift",.25f,"mood:rested",2,"career",1.2f,work:1.05f),
        G(201,"light-sleeper","얕은 잠","작은 경보에도 깨지만 소음이 있는 방에서는 쉬기 어렵다.","safety:answer-alarm",.35f,"room:noise",-3,"emergency",1.3f),
        G(202,"sure-footed","안정된 발놀림","잔해와 비탈에서 속도를 유지하며 추락 위험을 싫어하지 않는다.","travel:rough-ground",.3f,"event:safe-crossing",2,"expedition",1.2f,move:1.04f),
        G(203,"delicate-frame","섬세한 체격","정밀 작업에는 유리하지만 무거운 운반과 충격을 피한다.","work:precision",.35f,"injury:blunt",-3,"craft",1.25f,work:1.03f,combat:.96f),
        G(204,"scarred","오래된 흉터","고통에 익숙하지만 의료실 냄새가 과거의 기억을 건드린다.","danger:hold-position",.2f,"facility:clinic",-2,"combat",1.25f,combat:1.03f),
        G(205,"keen-senses","예민한 감각","숨은 위험과 상한 식품을 빨리 알아차리지만 악취에 민감하다.","safety:inspect",.35f,"air:foul",-3,"service-incident",1.3f),
        G(206,"heavy-build","육중한 체격","운반과 제압에 자신 있지만 좁고 더운 방을 견디지 못한다.","work:heavy-haul",.3f,"room:cramped",-3,"defense",1.2f,combat:1.04f,move:.97f),
        G(207,"quick-healer","빠른 회복","치료 지시를 잘 따르고 회복 과정의 작은 진전을 기뻐한다.","health:seek-treatment",.3f,"health:recovered",3,"medical",1.3f),
        G(208,"slow-metabolism","느린 대사","적게 먹지만 추위와 장시간 작업에서 쉽게 굳어진다.","food:delay-meal",.2f,"weather:cold",-3,"survival",1.15f,consumption:.88f,move:.98f),
        G(209,"hot-blooded","뜨거운 피","추위에 대담하고 모욕에 즉각 반응한다.","conflict:confront",.4f,"social:insult",-4,"service-incident",1.4f,combat:1.03f),
        G(210,"sugar-seeker","단맛 탐닉","과실과 시럽이 있으면 우선 찾고 부족하면 집중력이 떨어진다.","food:sweet",.45f,"food:no-sweets",-2,"festival",1.25f,consumption:1.05f),
        G(211,"iron-stomach","튼튼한 위장","낯선 배급식을 잘 먹지만 오염을 대수롭지 않게 여긴다.","food:unfamiliar",.3f,"food:variety",2,"disease",.8f,accident:1.03f),
        G(212,"temperature-sensitive","온도 민감","쾌적한 온도에서 능률이 높지만 기후 변동에 쉽게 지친다.","room:climate-controlled",.35f,"temperature:uncomfortable",-4,"seasonal",1.35f,work:1.03f),
        G(213,"ritual-faster","의식 단식가","중요한 의식 전 식사를 거르는 것을 자부심으로 여긴다.","culture:fast",.4f,"festival:prepared",3,"festival",1.35f,consumption:.95f),
        G(214,"salt-craver","소금 갈망","염장식을 선호하고 담백한 식단을 불충분하게 느낀다.","food:salted",.4f,"food:bland",-2,"service",1.2f,consumption:1.03f),
        G(215,"calm","침착함","긴급 상황에서 먼저 상황을 정리하고 주변의 공포에 덜 휩쓸린다.","emergency:assess",.4f,"event:panic",-1,"emergency",.75f),
        G(216,"brooding","곱씹는 성격","실패를 오래 기억해 재발을 줄이지만 기분 회복이 느리다.","event:review-failure",.35f,"event:failure",-4,"life-event",1.25f),
        G(217,"cheerful","낙천가","작은 성공을 함께 축하하고 공동 작업의 분위기를 끌어올린다.","social:encourage",.4f,"event:minor-success",3,"festival",1.25f),
        G(218,"grudge-holder","원한을 품음","배신을 오래 기억하고 같은 상대와의 타협을 꺼린다.","faction:retaliate",.4f,"event:betrayal",-5,"faction",1.5f,conflict:"temper:forgiving"),
        G(219,"forgiving","관대한 마음","사과와 보상을 받으면 갈등을 빨리 끝내려 한다.","conflict:reconcile",.4f,"event:apology",3,"faction",.75f,conflict:"temper:grudge"),
        G(220,"anxious-planner","불안한 계획가","비상 비축과 대피로를 반복 확인해야 마음이 놓인다.","safety:prepare",.45f,"stock:shortage",-4,"seasonal",1.4f),
        G(221,"thrill-seeker","자극 추구자","위험한 원정과 실험을 선호하며 평온한 교대에 싫증낸다.","danger:volunteer",.5f,"work:routine",-2,"expedition",1.45f,accident:1.08f),
        G(222,"private","과묵함","혼자 쉬는 시간을 중시하고 공개적인 질문을 부담스러워한다.","rest:private",.45f,"room:overcrowded",-3,"social",.8f),
        G(223,"meticulous","꼼꼼함","검사표를 끝까지 확인해 느리지만 재작업과 사고를 줄인다.","work:inspect",.5f,"product:defect",-3,"craft",1.35f,work:.97f,accident:.88f),
        G(224,"improviser","즉흥 제작자","부족한 자재로 해결책을 찾지만 표준 절차를 답답해한다.","work:prototype",.45f,"work:strict-procedure",-2,"craft",1.35f,work:1.03f,accident:1.05f),
        G(225,"routine-keeper","규칙 준수자","예측 가능한 교대와 정리된 작업 목록에서 최고의 능률을 낸다.","work:routine",.45f,"work:reassigned",-3,"career",1.25f,work:1.04f),
        G(226,"teacher","가르치는 버릇","혼자 빨리 끝내기보다 옆 사람에게 과정을 설명하려 한다.","work:mentor",.5f,"event:student-progress",3,"apprenticeship",1.45f,work:.98f),
        G(227,"salvager","회수 전문가","버려진 장비와 부산물에서 쓸 만한 부품을 먼저 찾는다.","work:salvage",.5f,"stock:waste",-2,"production",1.35f),
        G(228,"night-worker","야간 체질","밤 교대에 집중하며 한낮의 소란스러운 작업을 피한다.","shift:night",.55f,"shift:day",-2,"career",1.2f,work:1.03f),
        G(229,"quality-proud","품질 자부심","불량품을 넘기느니 납기를 늦추는 편을 택한다.","work:quality",.55f,"product:poor-quality",-4,"guest-request",1.35f,work:.98f),
        G(230,"fast-learner","빠른 학습자","새 공정을 직접 시험할 기회를 선호하고 첫 성공에 크게 고무된다.","work:training",.45f,"event:first-success",3,"apprenticeship",1.35f,research:1.04f),
        G(231,"host","손님맞이꾼","낯선 이를 편하게 만들며 식사와 좌석의 작은 결례를 먼저 발견한다.","service:greet",.5f,"guest:satisfied",3,"guest-request",1.4f),
        G(232,"blunt","직설적","문제를 숨기지 않지만 체면을 중시하는 상대와 자주 충돌한다.","social:speak-plainly",.45f,"social:formal-etiquette",-3,"service-incident",1.35f),
        G(233,"mediator","중재자","갈등 당사자의 요구를 정리하고 체면을 살리는 절충안을 찾는다.","conflict:mediate",.55f,"event:reconciliation",3,"faction",1.45f),
        G(234,"loyal","충성심","가구와 오래된 동료의 부탁을 우선하며 배신에 깊게 상처받는다.","social:help-household",.5f,"event:betrayal",-5,"life-event",1.35f),
        G(235,"status-conscious","체면 중시","직위와 공식 예절을 중시하며 공개적인 무시를 참지 못한다.","career:seek-position",.45f,"social:public-slight",-4,"career",1.4f),
        G(236,"outsider-friendly","낯선 문화에 호의적","다른 문화의 관습에 참여하며 새로운 식사와 의식을 즐긴다.","culture:join-other",.5f,"culture:harmony",3,"festival",1.45f),
        G(237,"clannish","우리 편 우선","가구와 같은 문화의 안전을 우선하고 외부 계약을 의심한다.","social:protect-in-group",.5f,"faction:outside-demand",-3,"faction",1.3f),
        G(238,"merciful","자비로움","부상자와 포로를 처형하기보다 치료와 교환으로 해결하려 한다.","combat:spare",.55f,"event:execution",-5,"captivity",1.4f),
        G(239,"ruthless","냉혹함","위협을 빠르게 제거하려 하지만 무고한 피해에는 공동체 반발을 산다.","combat:finish-threat",.55f,"event:enemy-escaped",-3,"combat",1.35f,combat:1.03f),
        G(240,"cautious","위험 회피","정찰과 보호구가 없으면 위험 작업을 거부한다.","safety:wait-for-gear",.55f,"danger:unprepared",-4,"emergency",.8f,accident:.9f),
        G(241,"daredevil","무모한 용기","준비가 부족해도 구조와 추격에 뛰어들며 성공에 강한 만족을 느낀다.","danger:immediate-action",.6f,"event:risky-success",5,"emergency",1.5f,accident:1.12f),
        G(242,"truth-seeker","진실 집착","평판 손해를 감수하고 숨겨진 기록과 모순을 파헤친다.","research:forbidden-record",.55f,"event:truth-revealed",4,"discovery",1.5f,research:1.05f),
        G(243,"pragmatist","실용주의","상징보다 실제 생존과 생산 효과가 큰 선택을 선호한다.","choice:practical",.5f,"event:wasteful-ceremony",-2,"contract",1.25f),
        G(244,"collector","수집벽","희귀한 유물과 기록을 보관하려 하며 소비 결정에 저항한다.","item:preserve-relic",.6f,"item:relic-consumed",-4,"milestone",1.45f),
        G(245,"compulsive-cleaner","청소 강박","오염을 발견하면 현재 작업보다 청소를 먼저 끝내려 한다.","work:clean",.65f,"room:dirty",-4,"disease",1.5f,work:.99f),
        G(246,"storyteller","이야기꾼","식사와 장례에서 사건을 이야기로 엮어 공동체의 기억을 붙든다.","social:tell-story",.55f,"event:audience",3,"life-event",1.45f)
    };

    private static readonly HeritableSpec[] Heritable =
    {
        H("heritable:reinforced-joints","강화 관절","관절 지지조직이 튼튼해 운반과 착지 충격에 강하다.",HeritableTraitCategory.Anatomy,HeritableTraitConsequenceKind.AnatomyCapacity,"joints",.12f,5,"anatomy:flexible-joints"),
        H("heritable:flexible-joints","유연 관절","좁은 공간과 정밀 동작에 유리하지만 무거운 충격에는 약하다.",HeritableTraitCategory.Anatomy,HeritableTraitConsequenceKind.AnatomyCapacity,"dexterity",.12f,5,"anatomy:reinforced-joints"),
        H("heritable:dense-bone","고밀도 골격","골절에 강하지만 이동과 수영에 더 많은 에너지가 든다.",HeritableTraitCategory.Anatomy,HeritableTraitConsequenceKind.AnatomyCapacity,"bone-density",.15f,3,"anatomy:hollow-bone"),
        H("heritable:hollow-bone","공동 골격","가볍고 민첩하지만 압궤 손상에 취약하다.",HeritableTraitCategory.Anatomy,HeritableTraitConsequenceKind.AnatomyCapacity,"mobility",.12f,4,"anatomy:dense-bone"),
        H("heritable:regrowing-tissue","재생 조직","경미한 조직 손상을 빠르게 복구하지만 영양 소모가 늘어난다.",HeritableTraitCategory.Anatomy,HeritableTraitConsequenceKind.AnatomyCapacity,"regeneration",.1f,4,""),
        H("heritable:expanded-lung","확장 폐낭","저산소 환경에서 더 오래 활동한다.",HeritableTraitCategory.Anatomy,HeritableTraitConsequenceKind.EnvironmentalTolerance,"low-oxygen",.15f,4,""),
        H("heritable:efficient-digestion","고효율 소화","같은 식사에서 더 많은 영양을 얻는다.",HeritableTraitCategory.Metabolism,HeritableTraitConsequenceKind.EnvironmentalTolerance,"nutrition",.12f,4,"metabolism:rapid-burn"),
        H("heritable:rapid-burn","고속 대사","순간 작업 능률이 높지만 식량 요구가 늘어난다.",HeritableTraitCategory.Metabolism,HeritableTraitConsequenceKind.Aptitude,"work-burst",.1f,6,"metabolism:efficient-digestion"),
        H("heritable:cold-blood-adaptation","저온 대사 적응","추운 환경에서 기관 기능 저하가 늦다.",HeritableTraitCategory.Metabolism,HeritableTraitConsequenceKind.EnvironmentalTolerance,"cold",.15f,3,"metabolism:heat-shedding"),
        H("heritable:heat-shedding","열 배출 피부","고온 작업의 열 축적을 줄인다.",HeritableTraitCategory.Metabolism,HeritableTraitConsequenceKind.EnvironmentalTolerance,"heat",.15f,3,"metabolism:cold-blood"),
        H("heritable:toxin-filter","독소 여과 기관","음식과 공기 독소의 축적을 늦춘다.",HeritableTraitCategory.Metabolism,HeritableTraitConsequenceKind.DiseaseResistance,"toxin",.14f,4,""),
        H("heritable:water-retention","수분 보존 조직","건조 환경에서 탈수 진행이 늦다.",HeritableTraitCategory.Metabolism,HeritableTraitConsequenceKind.EnvironmentalTolerance,"dryness",.14f,3,""),
        H("heritable:mana-reservoir","마나 저장낭","마나 수용량이 늘지만 폭주 시 손상도 커진다.",HeritableTraitCategory.Arcane,HeritableTraitConsequenceKind.ManaAffinity,"capacity",.15f,6,"arcane:mana-grounding"),
        H("heritable:mana-grounding","마나 접지맥","비전 교란을 흘려보내지만 주문 증폭은 약하다.",HeritableTraitCategory.Arcane,HeritableTraitConsequenceKind.ManaAffinity,"stability",.15f,5,"arcane:mana-reservoir"),
        H("heritable:rune-sight","룬시","미세한 룬 오차를 감지해 조율 작업에 유리하다.",HeritableTraitCategory.Arcane,HeritableTraitConsequenceKind.Aptitude,"rune-tuning",.12f,8,""),
        H("heritable:dream-reception","꿈 수신","비전 꿈과 기억에 민감해 예지 단서를 얻지만 수면 장애 위험이 있다.",HeritableTraitCategory.Arcane,HeritableTraitConsequenceKind.ManaAffinity,"dream",.1f,5,""),
        H("heritable:stable-gestation","안정 임신","생식 과정의 건강·영양 저하에 대한 실패 위험이 줄어든다.",HeritableTraitCategory.Reproduction,HeritableTraitConsequenceKind.Fertility,"gestation-stability",.15f,3,""),
        H("heritable:abundant-seed","풍부한 종자","생식 성공률이 높지만 회복 기간의 영양 요구가 늘어난다.",HeritableTraitCategory.Reproduction,HeritableTraitConsequenceKind.Fertility,"success-rate",.12f,2,"reproduction:slow-fertility"),
        H("heritable:slow-fertility","느린 가임 주기","성공률은 낮지만 성공한 배아·알·포자의 안정성이 높다.",HeritableTraitCategory.Reproduction,HeritableTraitConsequenceKind.Fertility,"offspring-stability",.14f,3,"reproduction:abundant-seed"),
        H("heritable:cross-lineage-tolerance","교차계통 내성","다른 계통과의 배양에서 거부 반응이 줄어든다.",HeritableTraitCategory.Reproduction,HeritableTraitConsequenceKind.Fertility,"cross-lineage",.15f,4,""),
        H("heritable:broad-immunity","광범위 면역","여러 감염병의 발병 확률을 조금씩 낮춘다.",HeritableTraitCategory.ImmunityLongevity,HeritableTraitConsequenceKind.DiseaseResistance,"all",.12f,4,"immunity:focused-antibodies"),
        H("heritable:focused-antibodies","집중 항체","한 번 겪은 질병의 면역 유지가 길어진다.",HeritableTraitCategory.ImmunityLongevity,HeritableTraitConsequenceKind.DiseaseResistance,"memory",.16f,4,"immunity:broad"),
        H("heritable:slow-aging","완만한 노화","성년 이후 생물학적 노화 속도가 소폭 느려진다.",HeritableTraitCategory.ImmunityLongevity,HeritableTraitConsequenceKind.AgingRate,"biological-age",-.12f,3,"longevity:rapid-repair"),
        H("heritable:rapid-repair","고속 면역 복구","감염 후 장기 손상 회복이 빠르지만 세포 노화 부담이 늘어난다.",HeritableTraitCategory.ImmunityLongevity,HeritableTraitConsequenceKind.DiseaseResistance,"recovery",.15f,5,"longevity:slow-aging")
    };

    [MenuItem("DungeonStory/V20/Build Trait Content (71)")]
    public static void Build()
    {
        if (General.Length != 47 || Heritable.Length != 24
            || Heritable.Count(x => x.Category == HeritableTraitCategory.Anatomy) != 6
            || Heritable.Count(x => x.Category == HeritableTraitCategory.Metabolism) != 6
            || Heritable.Count(x => x.Category == HeritableTraitCategory.Arcane) != 4
            || Heritable.Count(x => x.Category == HeritableTraitCategory.Reproduction) != 4
            || Heritable.Count(x => x.Category == HeritableTraitCategory.ImmunityLongevity) != 4)
            throw new InvalidOperationException("V20 trait manifest count contract is broken.");

        Ensure("Assets/Resources/SO/V20", "Traits"); Ensure("Assets/Resources/SO/V20/Traits", "General"); Ensure("Assets/Resources/SO/V20/Traits", "Heritable");
        GameDomainContentCatalogSO catalog = AssetDatabase.LoadAssetAtPath<GameDomainContentCatalogSO>(CatalogPath)
            ?? throw new InvalidOperationException("The root content catalog is missing.");

        CharacterTraitSO[] legacy = catalog.GetAll<CharacterTraitSO>().Where(x => x.id >= 101 && x.id <= 109).OrderBy(x => x.id).ToArray();
        if (legacy.Length != 9) throw new InvalidOperationException($"Expected nine legacy traits, found {legacy.Length}.");
        foreach (CharacterTraitSO value in legacy) UpgradeLegacy(value);

        List<CharacterTraitSO> general = General.Select(CreateGeneral).ToList();
        List<HeritableTraitDefinitionSO> heritable = Heritable.Select(CreateHeritable).ToList();
        List<string> errors = legacy.Concat(general).SelectMany(x => x.ValidateDefinition()).Concat(heritable.SelectMany(x => x.ValidateDefinition())).ToList();
        if (legacy.Concat(general).GroupBy(x => x.id).Any(group => group.Count() > 1)) errors.Add("Duplicate general trait numeric id.");
        if (heritable.GroupBy(x => x.traitId, StringComparer.Ordinal).Any(group => group.Count() > 1)) errors.Add("Duplicate heritable trait id.");
        if (errors.Count > 0) throw new InvalidOperationException(string.Join(" | ", errors));

        catalog.SetDefinitions(catalog.Definitions
            .Where(x => x != null && x is not HeritableTraitDefinitionSO && !(x is CharacterTraitSO trait && trait.id >= 200 && trait.id <= 246))
            .Concat(general).Concat(heritable));
        EditorUtility.SetDirty(catalog); AssetDatabase.SaveAssets(); AssetDatabase.Refresh();
        Debug.Log("V20_TRAIT_CONTENT=PASS; generalExisting=9; generalNew=47; generalTotal=56; heritable=24");
    }

    private static CharacterTraitSO CreateGeneral(GeneralSpec spec)
    {
        CharacterTraitSO value = LoadOrCreate<CharacterTraitSO>($"{GeneralRoot}/Trait_{spec.Id}_{spec.Key}.asset");
        value.id = spec.Id; value.traitName = spec.Name; value.description = spec.Description;
        value.statBonus ??= new CharacterStatBlock(); value.modifiers ??= new CharacterModelModifiers();
        value.modifiers.workSpeedMultiplier = spec.Work == 0 ? 1f : spec.Work;
        value.modifiers.researchSpeedMultiplier = spec.Research == 0 ? 1f : spec.Research;
        value.modifiers.combatPowerMultiplier = spec.Combat == 0 ? 1f : spec.Combat;
        value.modifiers.moveSpeedMultiplier = spec.Move == 0 ? 1f : spec.Move;
        value.modifiers.consumptionMultiplier = spec.Consumption == 0 ? 1f : spec.Consumption;
        value.modifiers.accidentChanceMultiplier = spec.Accident == 0 ? 1f : spec.Accident;
        value.incompatibilityGroups = string.IsNullOrWhiteSpace(spec.Conflict) ? new List<string>() : new List<string> { spec.Conflict };
        value.behaviorPreferences = new List<CharacterTraitBehaviorPreference> { new() { behaviorTag = spec.Behavior, utilityDelta = spec.Utility } };
        value.moodReactions = new List<CharacterTraitMoodReaction> { new() { triggerTag = spec.Trigger, moodDelta = spec.Mood, durationDays = 3 } };
        value.eventWeights = new List<CharacterTraitEventWeight> { new() { eventCategoryId = spec.EventCategory, multiplier = spec.EventWeight } };
        EditorUtility.SetDirty(value); return value;
    }

    private static HeritableTraitDefinitionSO CreateHeritable(HeritableSpec spec)
    {
        HeritableTraitDefinitionSO value = LoadOrCreate<HeritableTraitDefinitionSO>($"{HeritableRoot}/{spec.Id.Replace(':','_')}.asset");
        value.traitId = spec.Id; value.displayName = spec.Name; value.description = spec.Description; value.authoringRevision = 1;
        value.sourceNote = "V20 hand-authored hereditary trait manifest."; value.category = spec.Category; value.incompatibilityGroup = spec.Conflict;
        value.aptitudeModifier = spec.Aptitude; value.compatibleSpeciesTags = new List<string>();
        value.consequences = new List<HeritableTraitConsequence> { new() { kind = spec.Kind, targetId = spec.Target, multiplierDelta = spec.Delta } };
        EditorUtility.SetDirty(value); return value;
    }

    private static void UpgradeLegacy(CharacterTraitSO value)
    {
        if (value.behaviorPreferences == null || value.behaviorPreferences.Count == 0)
            value.behaviorPreferences = new List<CharacterTraitBehaviorPreference> { new() { behaviorTag = $"legacy-trait:{value.id}", utilityDelta = value.id % 2 == 0 ? .2f : -.2f } };
        if (value.moodReactions == null) value.moodReactions = new List<CharacterTraitMoodReaction>();
        if (value.eventWeights == null) value.eventWeights = new List<CharacterTraitEventWeight>();
        if (value.incompatibilityGroups == null) value.incompatibilityGroups = new List<string>();
        EditorUtility.SetDirty(value);
    }

    public static float ResolveCappedModifier(IEnumerable<HeritableTraitDefinitionSO> traits, HeritableTraitConsequenceKind kind, string targetId)
    {
        float sum = (traits ?? Array.Empty<HeritableTraitDefinitionSO>()).Where(x => x != null)
            .SelectMany(x => x.consequences ?? new List<HeritableTraitConsequence>())
            .Where(x => x != null && x.kind == kind && string.Equals(x.targetId, targetId, StringComparison.Ordinal))
            .Sum(x => x.multiplierDelta);
        return Mathf.Clamp(sum, -.25f, .25f);
    }

    private static T LoadOrCreate<T>(string path) where T : ScriptableObject
    {
        UnityEngine.Object existing = AssetDatabase.LoadMainAssetAtPath(path);
        if (existing != null && existing is not T) throw new InvalidOperationException($"Wrong asset type at '{path}'.");
        if (existing is T typed) return typed;
        T created = ScriptableObject.CreateInstance<T>(); AssetDatabase.CreateAsset(created, path); return created;
    }
    private static void Ensure(string parent, string child) { string path = $"{parent}/{child}"; if (!AssetDatabase.IsValidFolder(path)) AssetDatabase.CreateFolder(parent, child); }
    private static GeneralSpec G(int id,string key,string name,string description,string behavior,float utility,string trigger,float mood,string eventCategory,float eventWeight,float work=0,float research=0,float combat=0,float move=0,float consumption=0,float accident=0,string conflict="") => new() { Id=id,Key=key,Name=name,Description=description,Behavior=behavior,Utility=utility,Trigger=trigger,Mood=mood,EventCategory=eventCategory,EventWeight=eventWeight,Work=work,Research=research,Combat=combat,Move=move,Consumption=consumption,Accident=accident,Conflict=conflict };
    private static HeritableSpec H(string id,string name,string description,HeritableTraitCategory category,HeritableTraitConsequenceKind kind,string target,float delta,int aptitude,string conflict) => new() { Id=id,Name=name,Description=description,Category=category,Kind=kind,Target=target,Delta=delta,Aptitude=aptitude,Conflict=conflict };
}
#endif
