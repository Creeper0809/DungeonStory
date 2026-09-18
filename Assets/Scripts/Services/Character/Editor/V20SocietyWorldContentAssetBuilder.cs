#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Buildings;
using UnityEditor;
using UnityEngine;

public static class V20SocietyWorldContentAssetBuilder
{
    private const string CatalogPath = "Assets/Resources/SO/Content/GameDomainContentCatalog.asset";
    private const string FestivalRoot = "Assets/Resources/SO/V20/Society/Festivals";
    private const string WorldRoot = "Assets/Resources/SO/V20/World/SeasonalEvents";

    private sealed class FestivalSpec
    {
        public string Id, Name, Description, Culture, Item;
        public FacilityVenueRequirements Venue;
        public Season Season;
        public int Day, Amount, Participants;
        public bool Grief;
    }

    private sealed class WorldSpec
    {
        public string Id, Name, Description, DomainA, DomainB, Target;
        public Season Season;
        public V20ContentEffectKind Effect;
        public float Amount;
        public float WorldWaterRegenerationMultiplier = 1f;
        public float PowerAvailableSupplyCapacityMultiplier = 1f;
        public float PipedWaterThroughputMultiplier = 1f;
        public float PipedWaterFreezeThresholdC;
        public float PipedWaterRecoveryThresholdC = 2f;
        public int Minimum, Maximum, CropPrimaryBatchLossPercent;
        public string RequiredWeatherFrontId = string.Empty;
        public bool EmitStartEffect = true;
        public bool EmitEndEffect = true;
        public int MinimumDefeatedHumanBranches;
        public SeasonalWildlifeArrivalProfile WildlifeArrival;
        public SeasonalDriftCargoProfile DriftCargo;
        public SeasonalManaLightningProfile ManaLightning;
        public SeasonalSpecialExpeditionProfile SpecialExpedition;
    }

    private static readonly FestivalSpec[] Festivals =
    {
        F("festival:sprout","새싹제","첫 파종과 겨울 비축의 끝을 함께 기념한다.",Season.Spring,15,"",MealVenue(8),"seed-lot:twilight-grain",12,8,false),
        F("festival:high-sun","고일제","가장 긴 작업일 뒤 차가운 음료와 공연으로 여름 피로를 푼다.",Season.Summer,15,"",MealVenue(8),"food:twilight-beer",8,8,false),
        F("festival:storage","저장제","창고를 점검하고 첫 보존식을 나누며 겨울 준비를 선언한다.",Season.Autumn,25,"",MealVenue(10),"food:preserved-ration",16,10,false),
        F("festival:long-night-memorial","긴밤 추모제","한 해의 사망자를 함께 기억하고 남은 슬픔을 결의로 바꾼다.",Season.Winter,30,"",ExactEventVenue(10,"building:8887"),"craft:candle",12,10,true),
        F("festival:frontier-map-night","지도에 불을 밝히는 밤","개척자들이 귀환 경로와 미귀환자의 이름을 지도에 남긴다.",Season.Spring,22,"culture:adventurer-frontier",ExactEventVenue(6,"building:1047"),"material:paper",8,6,false),
        F("festival:pack-first-hunt","무리의 첫사냥","수인 무리가 공동 사냥과 고기 분배로 새 계절의 결속을 확인한다.",Season.Autumn,8,"culture:beastkin-pack",MealVenue(8),"resource:meat",18,8,false),
        F("festival:ash-oath","재의 맹세일","악마들이 지난 계약을 검토하고 지킬 약속만 다시 봉인한다.",Season.Winter,12,"culture:demon-contract",EventVenue(6,FacilityRole.Entertainment,FacilityRole.Entertainment),"material:paper",12,6,false),
        F("festival:core-resonance","핵 공명일","골렘들이 정비된 핵의 주파수를 맞추고 기억판을 교환한다.",Season.Summer,6,"culture:golem-core",ExactEventVenue(5,"building:9826"),"tool:maintenance-kit",2,5,false),
        F("festival:open-sky-chorus","열린 하늘 합창","하피가 높은 통로를 비우고 계절 바람의 변화를 합창으로 기록한다.",Season.Spring,10,"culture:harpy-aerie",ExactEventVenue(6,"building:8851"),"resource:night-grape",10,6,false),
        F("festival:tool-clan-fair","도구씨족 품평회","코볼트 씨족이 수리한 도구를 전시하고 가장 유용한 개선을 뽑는다.",Season.Autumn,18,"culture:kobold-toolclan",ExactEventVenue(6,"building:8861"),"component:machine-parts",2,6,false),
        F("festival:spore-bloom","포자 개화제","균사체가 배양 정원의 향과 포자를 섞어 공동 기억을 갱신한다.",Season.Spring,25,"culture:myconid-grove",ExactEventVenue(6,"building:8803"),"resource:cave-mushroom",14,6,false),
        F("festival:weapon-vigil","무기 철야제","오크들이 밤새 무기를 손질하며 승리와 실수를 같은 무게로 말한다.",Season.Winter,20,"culture:orc-vigil",WeaponVigilVenue(8),"material:charcoal",12,8,true),
        F("festival:clear-confluence","맑은 합류제","슬라임 가구가 깨끗한 수조에서 색과 기억을 나눈다.",Season.Summer,20,"culture:slime-confluence",Venue(FacilityRole.None,FacilityRole.Hygiene,eventCells:8,wetBath:true,exactIds:new[]{"building:1060"}),"resource:clean-water",24,8,false),
        F("festival:blood-lantern","혈향 등불제","뱀파이어가 동의받은 혈향 촛불로 생존자와 사망자의 이름을 밝힌다.",Season.Autumn,30,"culture:vampire-nightcourt",ExactEventVenue(6,"building:8887"),"craft:candle",8,6,true),
        F("festival:many-tables","열 문화의 식탁","서로의 금기를 표시한 열 개의 작은 식탁을 돌며 대체식을 나눈다.",Season.Summer,28,"",MealVenue(20),"food:lavish-vegan",8,20,false),
        F("festival:dungeon-accord-day","던전 협약 기념일","세력 사절과 직원이 공동 방어·구호 계약의 이행을 공개 점검한다.",Season.Autumn,12,"",EventVenue(12,FacilityRole.Entertainment,FacilityRole.Entertainment),"craft:dreamweave-ritual-banner",1,12,false)
    };

    private static readonly WorldSpec[] Worlds =
    {
        W("seasonal:spring-thaw-flood","해빙수 범람","녹은 물이 낮은 농지와 운반 통로를 동시에 덮친다.",Season.Spring,"agriculture","logistics",V20ContentEffectKind.WorkDelayDays,"flood",1,2,3),
        W("seasonal:spring-migrant-herd","이동 초식군","봄 외곽 초지에 심층염소 세 마리가 도착해 사냥·포획하거나 그대로 지나가게 둘 수 있다.",Season.Spring,"wildlife","agriculture",V20ContentEffectKind.Threat,"herd",3,2,4,emitStartEffect:false,wildlifeArrival:Arrival("deep_goat",3,"grass",SeasonalWildlifeArrivalQualification.NonHostile)),
        W("seasonal:spring-spore-rain","포자비","습한 포자가 버섯 생산을 돕지만 호흡기 노출을 높인다.",Season.Spring,"agriculture","disease",V20ContentEffectKind.DiseaseExposure,"disease:spore-lung",8,2,4),
        W("seasonal:spring-washed-road","씻겨나간 길","폭우가 원정로와 외부 물자 도착 시간을 흔든다.",Season.Spring,"expedition","logistics",V20ContentEffectKind.WorkDelayDays,"road",2,1,3),
        W("seasonal:spring-drift-cargo","해빙 표류 화물","해빙수에 떠밀려온 목재 여섯 개가 외곽에 남아 있다. 기한 전 실제 운반으로 회수해야 한다.",Season.Spring,"logistics","facility",default,string.Empty,0,3,5,emitStartEffect:false,driftCargo:Cargo("material:lumber",6)),
        W("seasonal:spring-seed-exchange","떠돌이 종자상","균사 연합 상인이 희귀 종자와 의약품을 생산품 교환으로 제안한다.",Season.Spring,"faction","agriculture",V20ContentEffectKind.FactionRapport,"faction:dungeon:myconid",4,2,2),
        W("seasonal:spring-fever-camp","봄열 피난민","열병이 도는 피난 행렬이 치료와 격리 공간을 요청한다.",Season.Spring,"disease","service",V20ContentEffectKind.DiseaseExposure,"disease:red-fever",10,2,4),
        W("seasonal:summer-heat-grid","폭염 전력부하","사건 동안 실제 전력망의 발전원과 축전지 방전 가용 공급량이 20% 줄어든다. 배선 용량과 저장 전력은 손상되지 않는다.",Season.Summer,"facility","health",V20ContentEffectKind.Threat,"power-grid",4,2,5,emitStartEffect:false,powerAvailableSupplyCapacityMultiplier:.8f),
        W("seasonal:summer-dry-well","마르는 수원","자연 수원 재생 저하가 농업과 손님 서비스를 동시에 압박한다.",Season.Summer,"agriculture","service",3,5,.95f),
        W("seasonal:summer-vermin-bloom","해충 대발생","사건 시작 시 이미 자라는 실제 재배 주기의 주 생산량만 최대 10% 감소하며 저장 식품은 손상시키지 않는다.",Season.Summer,"agriculture","logistics",V20ContentEffectKind.Threat,"crop-pests",5,3,5,cropPrimaryBatchLossPercent:10,emitStartEffect:false),
        W("seasonal:summer-mana-lightning","마나 번개","사건 동안 실제 가동·통전 중인 지정 비전 제련 설비의 Heat/Fault 전기 발화 위험이 높아진다. 진행 자격이 있으면 번개결정 노두 원정을 출발하거나 비용 없이 지나갈 수 있다.",Season.Summer,"facility","expedition",default,string.Empty,0,1,3,emitStartEffect:false,emitEndEffect:false,minimumDefeatedHumanBranches:1,manaLightning:ManaLightning(9825,80f,4f,30f,.08f,.2f),specialExpedition:SpecialExpedition(SeasonalSpecialExpeditionPurpose.ManaCrystalYield,"ritual_site","번개결정 노두","마나 번개가 드러낸 결정 노두다. 출발하면 확정 조우를 돌파하고 실제 결정을 귀환시켜야 한다.",3,5,45f,110f,2,42f,2,"encounter:09","도시연맹 야전병원: 군단 외과의·장창병·화승총병","미감정 전리품 x1","hostile",Reward("resource:mana-crystal","마나 결정",3))),
        W("seasonal:summer-wounded-mercenaries","부상 용병대","수인 연합 용병들이 치료와 탄약을 요구하며 추격자를 끌고 온다.",Season.Summer,"service","faction",V20ContentEffectKind.FactionGrievance,"faction:dungeon:beastkin",5,2,3),
        W("seasonal:summer-smoke-valley","연무 계곡","산불 연기가 환기와 원거리 전투 시야를 악화시킨다.",Season.Summer,"disease","combat",V20ContentEffectKind.DiseaseExposure,"disease:ash-lung",7,2,4),
        W("seasonal:summer-festival-scarcity","축제 식재료 경쟁","주변 정착지의 축제가 고급 식품 가격과 손님 요구를 끌어올린다.",Season.Summer,"service","faction",V20ContentEffectKind.Money,"market",-120,2,3),
        W("seasonal:autumn-early-frost","이른 서리","기존 한파 전선이 이어지는 동안 실제 농지 온도가 작물의 품종 적용 적정 범위보다 낮으면 성장만 멈춘다. 난방으로 적정 온도를 유지한 농지는 영향받지 않는다.",Season.Autumn,"agriculture","environment",default,string.Empty,0f,2,4,emitStartEffect:false,requiredWeatherFrontId:"weather:cold-snap"),
        W("seasonal:autumn-rot-cart","썩은 수레","오염된 교역 식품이 창고와 식당으로 들어올 위험이 생긴다.",Season.Autumn,"logistics","disease",V20ContentEffectKind.DiseaseExposure,"disease:gut-rot",9,1,2),
        W("seasonal:autumn-predator-descent","포식자 하산","가을 외곽 둥지에 시체청소 비룡 두 마리가 내려와 실제 먹잇감을 탐색한다.",Season.Autumn,"wildlife","defense",V20ContentEffectKind.Threat,"predators",5,3,5,emitStartEffect:false,wildlifeArrival:Arrival("carrion_drake",2,"lair",SeasonalWildlifeArrivalQualification.Predatory)),
        W("seasonal:autumn-caravan-rush","겨울 전 대상행렬","대상단이 대량 계약을 제안해 생산과 객실을 동시에 점유한다.",Season.Autumn,"faction","production",V20ContentEffectKind.Money,"contract",180,2,4),
        W("seasonal:autumn-spoiled-silage","사일리지 발열","잘못 쌓인 사료가 발열해 축산과 화재 대응을 압박한다.",Season.Autumn,"husbandry","facility",V20ContentEffectKind.Threat,"silage-fire",4,1,3),
        W("seasonal:autumn-migration-window","짧은 이동창","가을의 짧은 이동창에 외곽 초지로 포자큰사슴 두 마리가 도착해 사냥·포획할 수 있다.",Season.Autumn,"expedition","wildlife",V20ContentEffectKind.WorldFlag,"migration-window",1,2,3,emitStartEffect:false,wildlifeArrival:Arrival("spore_elk",2,"grass",SeasonalWildlifeArrivalQualification.NonHostile)),
        W("seasonal:autumn-harvest-dispute","수확 몫 분쟁","코볼트 연합이 공동 경작지의 수확 몫을 다시 요구한다.",Season.Autumn,"faction","agriculture",V20ContentEffectKind.FactionGrievance,"faction:dungeon:kobold",5,2,3),
        W("seasonal:winter-whiteout","백색 암흑","눈보라가 원정 시야와 외부 물류를 거의 끊는다.",Season.Winter,"expedition","logistics",V20ContentEffectKind.WorkDelayDays,"whiteout",3,1,3),
        W("seasonal:winter-frozen-pipes","동결 배관","사건 동안 실제 시설 온도가 0도 이하면 배관 유량이 80%로 줄고 2도 이상에서 회복한다. 저장된 물과 수동 급수는 손상되지 않는다.",Season.Winter,"facility","health",V20ContentEffectKind.Threat,"frozen-pipes",4,2,4,emitStartEffect:false,pipedWaterThroughputMultiplier:.8f,pipedWaterFreezeThresholdC:0f,pipedWaterRecoveryThresholdC:2f),
        W("seasonal:winter-hungry-pack","굶주린 무리","겨울 외곽 둥지에 동굴사냥개 세 마리가 도착해 실제 먹잇감을 탐색한다.",Season.Winter,"wildlife","defense",V20ContentEffectKind.Threat,"hungry-pack",6,3,5,emitStartEffect:false,wildlifeArrival:Arrival("cave_hound",3,"lair",SeasonalWildlifeArrivalQualification.Predatory)),
        W("seasonal:winter-cave-flu-wave","동굴 독감 유행","밀폐 생활이 직원과 피난 손님 사이의 공기 감염을 키운다.",Season.Winter,"disease","service",V20ContentEffectKind.DiseaseExposure,"disease:cave-flu",12,3,6),
        W("seasonal:winter-fuel-demand","연료 쟁탈","주변 세력이 난방 연료 계약을 제시한다. 수락하면 사건 종료 전 숯 8개를 실제 납품해야 한다.",Season.Winter,"faction","facility",V20ContentEffectKind.ItemConsume,"material:charcoal",8,2,4,false),
        W("seasonal:winter-deep-echo","심층의 메아리","진행 자격을 갖춘 원정대가 진실 수호자의 심층 봉인소를 선택해 출발할 수 있다. 위험·확정 조우·실물 보상을 확인한 뒤 비용 없이 무시할 수 있다.",Season.Winter,"expedition","combat",default,string.Empty,0,1,2,emitStartEffect:false,emitEndEffect:false,minimumDefeatedHumanBranches:4,specialExpedition:SpecialExpedition(SeasonalSpecialExpeditionPurpose.TruthGuardianEcho,"archive","심층의 메아리 원정","심층 봉인소에서 진실 봉인수호자와 무효감시자가 기다린다. 출발 뒤 제안 기한이 지나도 확정 조우와 보상은 바뀌지 않는다.",6,8,96f,180f,4,84f,6,"encounter:31","진실 봉인수호자·진실 무효감시자","미감정 전리품 x1","truth:guardian",Reward("record:arcane-index","비전 색인",1),Reward("resource:mana-crystal","마나 결정",2))),
        W("seasonal:winter-memorial-envoys","추모 사절단","골렘 연합 사절들이 공동 추모에 참여하며 오래된 원한의 처리를 요구한다.",Season.Winter,"faction","psychosocial",V20ContentEffectKind.FactionRapport,"faction:dungeon:golem",5,1,2)
    };

    [MenuItem("DungeonStory/V20/Build Festivals and Seasonal Events (40)")]
    public static void Build()
    {
        if (Festivals.Length != 16 || Worlds.Length != 28 || Worlds.GroupBy(x => x.Season).Any(group => group.Count() != 7))
            throw new InvalidOperationException("V20 society/world manifest count contract is broken.");
        Ensure("Assets/Resources/SO/V20", "Society"); Ensure("Assets/Resources/SO/V20/Society", "Festivals");
        Ensure("Assets/Resources/SO/V20", "World"); Ensure("Assets/Resources/SO/V20/World", "SeasonalEvents");
        GameDomainContentCatalogSO catalog = AssetDatabase.LoadAssetAtPath<GameDomainContentCatalogSO>(CatalogPath)
            ?? throw new InvalidOperationException("The root content catalog is missing.");

        List<FestivalDefinitionSO> festivals = Festivals.Select(CreateFestival).ToList();
        List<SeasonalWorldEventDefinitionSO> worlds = Worlds.Select(CreateWorld).ToList();
        List<string> errors = festivals.SelectMany(x => x.ValidateDefinition()).Concat(worlds.SelectMany(x => x.ValidateDefinition())).ToList();
        if (errors.Count > 0) throw new InvalidOperationException(string.Join(" | ", errors));
        catalog.SetDefinitions(catalog.Definitions.Where(x => x is not FestivalDefinitionSO && x is not SeasonalWorldEventDefinitionSO).Concat(festivals).Concat(worlds));
        EditorUtility.SetDirty(catalog); AssetDatabase.SaveAssets(); AssetDatabase.Refresh();
        Debug.Log("V20_SOCIETY_WORLD_CONTENT=PASS; festivals=16; seasonalEvents=28");
    }

    [MenuItem("DungeonStory/V20/Apply Festival Venue Authoring (16)")]
    public static void ApplyFestivalVenueAuthoring()
    {
        List<(FestivalDefinitionSO Value,FestivalSpec Spec)> festivals = Festivals.Select(spec =>
        {
            FestivalDefinitionSO value = AssetDatabase.LoadAssetAtPath<FestivalDefinitionSO>(
                $"{FestivalRoot}/{spec.Id.Replace(':','_')}.asset")
                ?? throw new InvalidOperationException($"Festival '{spec.Id}' is missing.");
            return (value,spec);
        }).ToList();

        List<(FestivalDefinitionSO Value,FestivalSpec Spec)> changes = new();
        List<string> errors = new();
        foreach ((FestivalDefinitionSO value,FestivalSpec spec) in festivals)
        {
            FestivalDefinitionSO staged = UnityEngine.Object.Instantiate(value);
            try
            {
                ApplyVenue(staged,spec);
                errors.AddRange(staged.ValidateDefinition().Select(error => $"{spec.Id}: {error}"));
                if (VenueAuthoringDiffers(value,staged)) changes.Add((value,spec));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(staged);
            }
        }
        if(errors.Count>0) throw new InvalidOperationException(string.Join(" | ",errors));
        List<string> dirty = changes.Where(change => EditorUtility.IsDirty(change.Value)).Select(change => change.Spec.Id).ToList();
        if(dirty.Count>0) throw new InvalidOperationException($"Venue authoring refuses dirty festival targets: {string.Join(",",dirty)}");
        foreach ((FestivalDefinitionSO value,FestivalSpec spec) in changes)
        {
            ApplyVenue(value,spec);
            EditorUtility.SetDirty(value);
            AssetDatabase.SaveAssetIfDirty(value);
        }
        Debug.Log($"V20_FESTIVAL_VENUES=PASS; festivals={festivals.Count}; changed={changes.Count}; saved={changes.Count}");
    }

    private static FestivalDefinitionSO CreateFestival(FestivalSpec spec)
    {
        FestivalDefinitionSO value = Asset<FestivalDefinitionSO>($"{FestivalRoot}/{spec.Id.Replace(':','_')}.asset");
        value.festivalId=spec.Id; value.displayName=spec.Name; value.description=spec.Description; value.authoringRevision=1;
        value.sourceNote="V20 hand-authored festival manifest."; value.season=spec.Season; value.dayOfSeason=spec.Day;
        value.convertsActiveGrief=spec.Grief; value.cultureId=spec.Culture; ApplyVenue(value,spec);
        value.requiredItems=new List<FestivalItemRequirement>{new(){itemDefinitionId=spec.Item,amount=spec.Amount}}; value.minimumParticipants=spec.Participants;
        value.successOutcome=new FestivalOutcomeDefinition{moodDelta=6,moodDurationDays=10,factionRapportDelta=spec.Culture.Length==0?3:0,griefConversionPercent=spec.Grief?25:0};
        value.partialOutcome=new FestivalOutcomeDefinition{moodDelta=2,moodDurationDays=5,factionRapportDelta=0,griefConversionPercent=spec.Grief?10:0};
        value.failureOutcome=new FestivalOutcomeDefinition{moodDelta=-3,moodDurationDays=4,factionRapportDelta=spec.Culture.Length==0?-2:0};
        EditorUtility.SetDirty(value); return value;
    }

    private static SeasonalWorldEventDefinitionSO CreateWorld(WorldSpec spec)
    {
        SeasonalWorldEventDefinitionSO value = Asset<SeasonalWorldEventDefinitionSO>($"{WorldRoot}/{spec.Id.Replace(':','_')}.asset");
        int revision=spec.ManaLightning!=null||spec.SpecialExpedition!=null?2:1;
        value.ConfigureMetadata(spec.Id,spec.Name,spec.Description,revision,revision==2?"WIM-009 authored seasonal arcane facility/expedition contract.":"V20 hand-authored seasonal world event manifest.");
        value.season=spec.Season; value.minimumDurationDays=spec.Minimum; value.maximumDurationDays=spec.Maximum;
        value.worldWaterRegenerationMultiplier=spec.WorldWaterRegenerationMultiplier;
        value.powerAvailableSupplyCapacityMultiplier=spec.PowerAvailableSupplyCapacityMultiplier;
        value.pipedWaterThroughputMultiplier=spec.PipedWaterThroughputMultiplier;
        value.pipedWaterFreezeThresholdC=spec.PipedWaterFreezeThresholdC;
        value.pipedWaterRecoveryThresholdC=spec.PipedWaterRecoveryThresholdC;
        value.cropPrimaryBatchLossPercent=spec.CropPrimaryBatchLossPercent;
        value.requiredWeatherFrontId=spec.RequiredWeatherFrontId;
        value.wildlifeArrivalProfile=spec.WildlifeArrival ?? new SeasonalWildlifeArrivalProfile();
        value.driftCargoProfile=spec.DriftCargo ?? new SeasonalDriftCargoProfile();
        value.manaLightningProfile=spec.ManaLightning ?? new SeasonalManaLightningProfile();
        value.specialExpeditionProfile=spec.SpecialExpedition ?? new SeasonalSpecialExpeditionProfile();
        value.affectedDomainIds=new[]{spec.DomainA,spec.DomainB}.Where(domain=>!string.IsNullOrWhiteSpace(domain)).Distinct(StringComparer.Ordinal).ToList();
        value.triggerRequirements=new V20ContentRequirementSet();
        if(spec.MinimumDefeatedHumanBranches>0)value.triggerRequirements.worldMetrics.Add(new V20WorldMetricRequirement{kind=V20WorldMetricKind.DefeatedHumanBranches,minimumValue=spec.MinimumDefeatedHumanBranches});
        value.startEffects=spec.EmitStartEffect
            ? new List<V20ContentEffect>{new(){kind=spec.Effect,targetId=spec.Target,amount=spec.Amount,durationDays=spec.Maximum}}
            : new List<V20ContentEffect>();
        value.dailyEffects=new List<V20ContentEffect>(); value.endEffects=spec.EmitEndEffect?new List<V20ContentEffect>{new(){kind=V20ContentEffectKind.WorldFlag,targetId=$"resolved:{spec.Id}",amount=1}}:new List<V20ContentEffect>();
        EditorUtility.SetDirty(value); return value;
    }

    private static T Asset<T>(string path) where T:ScriptableObject { UnityEngine.Object existing=AssetDatabase.LoadMainAssetAtPath(path); if(existing!=null&&existing is not T) throw new InvalidOperationException($"Wrong asset type at '{path}'."); if(existing is T typed)return typed; T value=ScriptableObject.CreateInstance<T>();AssetDatabase.CreateAsset(value,path);return value; }
    private static void Ensure(string parent,string child){string path=$"{parent}/{child}";if(!AssetDatabase.IsValidFolder(path))AssetDatabase.CreateFolder(parent,child);}
    private static FestivalSpec F(string id,string name,string description,Season season,int day,string culture,FacilityVenueRequirements venue,string item,int amount,int participants,bool grief)=>new(){Id=id,Name=name,Description=description,Season=season,Day=day,Culture=culture,Venue=venue,Item=item,Amount=amount,Participants=participants,Grief=grief};
    private static void ApplyVenue(FestivalDefinitionSO value,FestivalSpec spec)
    {
        value.requiredBuildingDefinitionId=string.Empty;
        value.venueRequirements=spec.Venue;
    }
    private static bool VenueAuthoringDiffers(FestivalDefinitionSO value,FestivalDefinitionSO staged) =>
        !string.Equals(value.requiredBuildingDefinitionId,staged.requiredBuildingDefinitionId,StringComparison.Ordinal)
        || !string.Equals(JsonUtility.ToJson(value.venueRequirements),JsonUtility.ToJson(staged.venueRequirements),StringComparison.Ordinal);
    private static FacilityVenueRequirements MealVenue(int participants) => Venue(FacilityRole.Meal,FacilityRole.Meal,seats:participants,tables:participants,serviceCapacity:1,eventCells:participants);
    private static FacilityVenueRequirements ExactEventVenue(int participants,string exactId) => Venue(FacilityRole.None,FacilityRole.None,eventCells:participants,exactIds:new[]{exactId});
    private static FacilityVenueRequirements EventVenue(int participants,FacilityRole anchorRoles,FacilityRole roomRoles) => Venue(anchorRoles,roomRoles,eventCells:participants);
    private static FacilityVenueRequirements WeaponVigilVenue(int participants) => Venue(FacilityRole.None,FacilityRole.None,eventCells:participants,exactIds:new[]{"building:1055"},requiredRoomExactIds:new[]{"building:8830"});
    private static FacilityVenueRequirements Venue(FacilityRole anchorRoles,FacilityRole roomRoles,int seats=0,int tables=0,int serviceCapacity=0,int eventCells=1,bool wetBath=false,string[] exactIds=null,string[] requiredRoomExactIds=null)
    {
        return new FacilityVenueRequirements{anchor=new FacilityVenueAnchorSelector{exactBuildingDefinitionIds=exactIds?.ToList()??new List<string>(),facilityRoles=anchorRoles},requiredRoomFacilityRoles=roomRoles,requiredRoomFacilities=requiredRoomExactIds?.Select(id=>new FacilityVenueAnchorSelector{exactBuildingDefinitionIds=new List<string>{id}}).ToList()??new List<FacilityVenueAnchorSelector>(),minimumSeats=seats,minimumTables=tables,minimumServiceCapacity=serviceCapacity,minimumEventCells=eventCells,requireWetBath=wetBath};
    }
    private static WorldSpec W(string id,string name,string description,Season season,string a,string b,V20ContentEffectKind effect,string target,float amount,int min,int max,int cropPrimaryBatchLossPercent=0,bool emitStartEffect=true,string requiredWeatherFrontId="",float powerAvailableSupplyCapacityMultiplier=1f,float pipedWaterThroughputMultiplier=1f,float pipedWaterFreezeThresholdC=0f,float pipedWaterRecoveryThresholdC=2f,SeasonalWildlifeArrivalProfile wildlifeArrival=null,SeasonalDriftCargoProfile driftCargo=null,bool emitEndEffect=true,int minimumDefeatedHumanBranches=0,SeasonalManaLightningProfile manaLightning=null,SeasonalSpecialExpeditionProfile specialExpedition=null)=>new(){Id=id,Name=name,Description=description,Season=season,DomainA=a,DomainB=b,Effect=effect,Target=target,Amount=amount,Minimum=min,Maximum=max,CropPrimaryBatchLossPercent=cropPrimaryBatchLossPercent,EmitStartEffect=emitStartEffect,EmitEndEffect=emitEndEffect,MinimumDefeatedHumanBranches=minimumDefeatedHumanBranches,RequiredWeatherFrontId=requiredWeatherFrontId,PowerAvailableSupplyCapacityMultiplier=powerAvailableSupplyCapacityMultiplier,PipedWaterThroughputMultiplier=pipedWaterThroughputMultiplier,PipedWaterFreezeThresholdC=pipedWaterFreezeThresholdC,PipedWaterRecoveryThresholdC=pipedWaterRecoveryThresholdC,WildlifeArrival=wildlifeArrival,DriftCargo=driftCargo,ManaLightning=manaLightning,SpecialExpedition=specialExpedition};
    private static WorldSpec W(string id,string name,string description,Season season,string a,string b,V20ContentEffectKind effect,string target,float amount,int min,int max,bool emitStartEffect)=>new(){Id=id,Name=name,Description=description,Season=season,DomainA=a,DomainB=b,Effect=effect,Target=target,Amount=amount,Minimum=min,Maximum=max,EmitStartEffect=emitStartEffect};
    private static WorldSpec W(string id,string name,string description,Season season,string a,string b,int min,int max,float worldWaterRegenerationMultiplier)=>new(){Id=id,Name=name,Description=description,Season=season,DomainA=a,DomainB=b,Minimum=min,Maximum=max,WorldWaterRegenerationMultiplier=worldWaterRegenerationMultiplier,EmitStartEffect=false};
    private static SeasonalWildlifeArrivalProfile Arrival(string speciesId,int exactCount,string requiredHabitatId,SeasonalWildlifeArrivalQualification qualification)=>new(){speciesId=speciesId,exactCount=exactCount,requiredHabitatId=requiredHabitatId,qualification=qualification};
    private static SeasonalDriftCargoProfile Cargo(string itemId,int exactQuantity)=>new(){itemId=itemId,exactQuantity=exactQuantity};
    private static SeasonalManaLightningProfile ManaLightning(int buildingId,float heat,float fault,float window,float chance,float intensity)=>new(){eligibleBuildingDefinitionIds=new List<int>{buildingId},minimumHeat=heat,minimumFault=fault,riskWindowSeconds=window,additionalIgnitionChancePerWindow=chance,ignitionIntensity=intensity};
    private static SeasonalExpeditionPhysicalRewardProfile Reward(string itemId,string displayLabel,int exactQuantity)=>new(){itemId=itemId,displayLabel=displayLabel,exactQuantity=exactQuantity};
    private static SeasonalSpecialExpeditionProfile SpecialExpedition(SeasonalSpecialExpeditionPurpose purpose,string siteArchetypeId,string title,string description,int minDistance,int maxDistance,float danger,float duration,int members,float power,int campaign,string encounterId,string encounterPreview,string encounterRewardPreview,string factionId,params SeasonalExpeditionPhysicalRewardProfile[] rewards)=>new(){purpose=purpose,siteArchetypeId=siteArchetypeId,title=title,description=description,minimumDistanceSteps=minDistance,maximumDistanceSteps=maxDistance,recommendedDanger=danger,durationSeconds=duration,requiredMembers=members,recommendedPower=power,campaignOrder=campaign,authoredEncounterId=encounterId,encounterPreviewText=encounterPreview,encounterRewardPreviewText=encounterRewardPreview,factionId=factionId,physicalRewards=rewards.ToList()};
}
#endif
