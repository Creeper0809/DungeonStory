#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class V20SocietyWorldContentAssetBuilder
{
    private const string CatalogPath = "Assets/Resources/SO/Content/GameDomainContentCatalog.asset";
    private const string FestivalRoot = "Assets/Resources/SO/V20/Society/Festivals";
    private const string WorldRoot = "Assets/Resources/SO/V20/World/SeasonalEvents";

    private sealed class FestivalSpec
    {
        public string Id, Name, Description, Culture, Facility, Item;
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
        public int Minimum, Maximum;
    }

    private static readonly FestivalSpec[] Festivals =
    {
        F("festival:sprout","새싹제","첫 파종과 겨울 비축의 끝을 함께 기념한다.",Season.Spring,15,"","building:festival-common-hall","item:seed-lot-grain",12,8,false),
        F("festival:high-sun","고일제","가장 긴 작업일 뒤 차가운 음료와 공연으로 여름 피로를 푼다.",Season.Summer,15,"","building:festival-common-hall","food:twilight-beer",10,8,false),
        F("festival:storage","저장제","창고를 점검하고 첫 보존식을 나누며 겨울 준비를 선언한다.",Season.Autumn,25,"","building:festival-common-hall","food:preserved-ration",16,10,false),
        F("festival:long-night-memorial","긴밤 추모제","한 해의 사망자를 함께 기억하고 남은 슬픔을 결의로 바꾼다.",Season.Winter,30,"","workstation:v19:memorial-room","item:candle",20,10,true),
        F("festival:frontier-map-night","지도에 불을 밝히는 밤","개척자들이 귀환 경로와 미귀환자의 이름을 지도에 남긴다.",Season.Spring,22,"culture:adventurer-frontier","building:expedition-map-room","material:paper",8,6,false),
        F("festival:pack-first-hunt","무리의 첫사냥","수인 무리가 공동 사냥과 고기 분배로 새 계절의 결속을 확인한다.",Season.Autumn,8,"culture:beastkin-pack","building:festival-common-hall","resource:meat",18,8,false),
        F("festival:ash-oath","재의 맹세일","악마들이 지난 계약을 검토하고 지킬 약속만 다시 봉인한다.",Season.Winter,12,"culture:demon-contract","building:faction-audience-hall","material:paper",12,6,false),
        F("festival:core-resonance","핵 공명일","골렘들이 정비된 핵의 주파수를 맞추고 기억판을 교환한다.",Season.Summer,6,"culture:golem-core","building:rune-tuning-room","component:maintenance-parts",10,5,false),
        F("festival:open-sky-chorus","열린 하늘 합창","하피가 높은 통로를 비우고 계절 바람의 변화를 합창으로 기록한다.",Season.Spring,10,"culture:harpy-aerie","building:weather-observation-tower","food:dried-fruit",10,6,false),
        F("festival:tool-clan-fair","도구씨족 품평회","코볼트 씨족이 수리한 도구를 전시하고 가장 유용한 개선을 뽑는다.",Season.Autumn,18,"culture:kobold-toolclan","building:apprentice-workbench","component:mechanical-parts",8,6,false),
        F("festival:spore-bloom","포자 개화제","균사체가 배양 정원의 향과 포자를 섞어 공동 기억을 갱신한다.",Season.Spring,25,"culture:myconid-grove","building:cave-growing-rack","food:cultured-mushroom",14,6,false),
        F("festival:weapon-vigil","무기 철야제","오크들이 밤새 무기를 손질하며 승리와 실수를 같은 무게로 말한다.",Season.Winter,20,"culture:orc-vigil","building:armory","material:charcoal",12,8,true),
        F("festival:clear-confluence","맑은 합류제","슬라임 가구가 깨끗한 수조에서 색과 기억을 나눈다.",Season.Summer,20,"culture:slime-confluence","building:clean-water-reservoir","resource:clean-water",24,8,false),
        F("festival:blood-lantern","혈향 등불제","뱀파이어가 동의받은 혈향 촛불로 생존자와 사망자의 이름을 밝힌다.",Season.Autumn,30,"culture:vampire-nightcourt","workstation:v19:memorial-room","item:candle",16,6,true),
        F("festival:many-tables","열 문화의 식탁","서로의 금기를 표시한 열 개의 작은 식탁을 돌며 대체식을 나눈다.",Season.Summer,28,"","building:festival-common-hall","food:festival-sampler",20,20,false),
        F("festival:dungeon-accord-day","던전 협약 기념일","세력 사절과 직원이 공동 방어·구호 계약의 이행을 공개 점검한다.",Season.Autumn,12,"","building:faction-audience-hall","item:faction-accord-banner",6,12,false)
    };

    private static readonly WorldSpec[] Worlds =
    {
        W("seasonal:spring-thaw-flood","해빙수 범람","녹은 물이 낮은 농지와 운반 통로를 동시에 덮친다.",Season.Spring,"agriculture","logistics",V20ContentEffectKind.WorkDelayDays,"flood",1,2,3),
        W("seasonal:spring-migrant-herd","이동 초식군","번식지로 향하는 초식동물이 밭을 지나며 포식자도 끌어들인다.",Season.Spring,"wildlife","agriculture",V20ContentEffectKind.Threat,"herd",3,2,4),
        W("seasonal:spring-spore-rain","포자비","습한 포자가 버섯 생산을 돕지만 호흡기 노출을 높인다.",Season.Spring,"agriculture","disease",V20ContentEffectKind.DiseaseExposure,"disease:spore-lung",8,2,4),
        W("seasonal:spring-washed-road","씻겨나간 길","폭우가 원정로와 외부 물자 도착 시간을 흔든다.",Season.Spring,"expedition","logistics",V20ContentEffectKind.WorkDelayDays,"road",2,1,3),
        W("seasonal:spring-nesting-season","둥지철","야생동물이 시설 틈에 둥지를 틀어 생산과 포획 기회를 함께 만든다.",Season.Spring,"wildlife","facility",V20ContentEffectKind.Threat,"nests",2,3,5),
        W("seasonal:spring-seed-exchange","떠돌이 종자상","균사 연합 상인이 희귀 종자와 의약품을 생산품 교환으로 제안한다.",Season.Spring,"faction","agriculture",V20ContentEffectKind.FactionRapport,"faction:dungeon:myconid",4,2,2),
        W("seasonal:spring-fever-camp","봄열 피난민","열병이 도는 피난 행렬이 치료와 격리 공간을 요청한다.",Season.Spring,"disease","service",V20ContentEffectKind.DiseaseExposure,"disease:red-fever",10,2,4),
        W("seasonal:summer-heat-grid","폭염 전력부하","냉각과 의료 시설의 동시 가동이 전력망을 압박한다.",Season.Summer,"facility","health",V20ContentEffectKind.Threat,"power-grid",4,2,5),
        W("seasonal:summer-dry-well","마르는 수원","깨끗한 물 생산 저하가 농업과 손님 서비스를 동시에 압박한다.",Season.Summer,"agriculture","service",V20ContentEffectKind.ItemConsume,"resource:clean-water",6,3,5),
        W("seasonal:summer-vermin-bloom","해충 대발생","고온에 늘어난 해충이 밭과 식량 창고를 오간다.",Season.Summer,"agriculture","logistics",V20ContentEffectKind.Threat,"crop-pests",5,3,5),
        W("seasonal:summer-mana-lightning","마나 번개","비전 장치가 과충전되고 원정지에는 희귀 결정이 노출된다.",Season.Summer,"facility","expedition",V20ContentEffectKind.Threat,"mana-surge",5,1,3),
        W("seasonal:summer-wounded-mercenaries","부상 용병대","수인 연합 용병들이 치료와 탄약을 요구하며 추격자를 끌고 온다.",Season.Summer,"service","faction",V20ContentEffectKind.FactionGrievance,"faction:dungeon:beastkin",5,2,3),
        W("seasonal:summer-smoke-valley","연무 계곡","산불 연기가 환기와 원거리 전투 시야를 악화시킨다.",Season.Summer,"disease","combat",V20ContentEffectKind.DiseaseExposure,"disease:ash-lung",7,2,4),
        W("seasonal:summer-festival-scarcity","축제 식재료 경쟁","주변 정착지의 축제가 고급 식품 가격과 손님 요구를 끌어올린다.",Season.Summer,"service","faction",V20ContentEffectKind.Money,"market",-120,2,3),
        W("seasonal:autumn-early-frost","이른 서리","서리가 수확 직전 작물과 야외 원정 보급을 동시에 위협한다.",Season.Autumn,"agriculture","expedition",V20ContentEffectKind.Threat,"early-frost",4,2,4),
        W("seasonal:autumn-rot-cart","썩은 수레","오염된 교역 식품이 창고와 식당으로 들어올 위험이 생긴다.",Season.Autumn,"logistics","disease",V20ContentEffectKind.DiseaseExposure,"disease:gut-rot",9,1,2),
        W("seasonal:autumn-predator-descent","포식자 하산","먹이를 쫓는 포식자가 축사와 외곽 운반로를 노린다.",Season.Autumn,"wildlife","defense",V20ContentEffectKind.Threat,"predators",5,3,5),
        W("seasonal:autumn-caravan-rush","겨울 전 대상행렬","대상단이 대량 계약을 제안해 생산과 객실을 동시에 점유한다.",Season.Autumn,"faction","production",V20ContentEffectKind.Money,"contract",180,2,4),
        W("seasonal:autumn-spoiled-silage","사일리지 발열","잘못 쌓인 사료가 발열해 축산과 화재 대응을 압박한다.",Season.Autumn,"husbandry","facility",V20ContentEffectKind.Threat,"silage-fire",4,1,3),
        W("seasonal:autumn-migration-window","짧은 이동창","안정된 날씨가 원정과 야생동물 포획에 모두 좋은 기회를 연다.",Season.Autumn,"expedition","wildlife",V20ContentEffectKind.WorldFlag,"migration-window",1,2,3),
        W("seasonal:autumn-harvest-dispute","수확 몫 분쟁","코볼트 연합이 공동 경작지의 수확 몫을 다시 요구한다.",Season.Autumn,"faction","agriculture",V20ContentEffectKind.FactionGrievance,"faction:dungeon:kobold",5,2,3),
        W("seasonal:winter-whiteout","백색 암흑","눈보라가 원정 시야와 외부 물류를 거의 끊는다.",Season.Winter,"expedition","logistics",V20ContentEffectKind.WorkDelayDays,"whiteout",3,1,3),
        W("seasonal:winter-frozen-pipes","동결 배관","동결이 물 공급과 의료실 운영을 함께 위협한다.",Season.Winter,"facility","health",V20ContentEffectKind.Threat,"frozen-pipes",4,2,4),
        W("seasonal:winter-hungry-pack","굶주린 무리","먹이를 잃은 야생 무리가 가축과 쓰레기장을 습격한다.",Season.Winter,"wildlife","defense",V20ContentEffectKind.Threat,"hungry-pack",6,3,5),
        W("seasonal:winter-cave-flu-wave","동굴 독감 유행","밀폐 생활이 직원과 피난 손님 사이의 공기 감염을 키운다.",Season.Winter,"disease","service",V20ContentEffectKind.DiseaseExposure,"disease:cave-flu",12,3,6),
        W("seasonal:winter-fuel-demand","연료 쟁탈","주변 세력이 연료 계약을 요구해 난방과 외교가 충돌한다.",Season.Winter,"faction","facility",V20ContentEffectKind.ItemConsume,"material:charcoal",8,2,4),
        W("seasonal:winter-deep-echo","심층의 메아리","조용해진 동굴에서 진실 수호자의 이동과 희귀 야생동물의 흔적이 함께 드러난다.",Season.Winter,"expedition","wildlife",V20ContentEffectKind.Threat,"truth-guardian",5,1,2),
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

    private static FestivalDefinitionSO CreateFestival(FestivalSpec spec)
    {
        FestivalDefinitionSO value = Asset<FestivalDefinitionSO>($"{FestivalRoot}/{spec.Id.Replace(':','_')}.asset");
        value.festivalId=spec.Id; value.displayName=spec.Name; value.description=spec.Description; value.authoringRevision=1;
        value.sourceNote="V20 hand-authored festival manifest."; value.season=spec.Season; value.dayOfSeason=spec.Day;
        value.convertsActiveGrief=spec.Grief; value.cultureId=spec.Culture; value.requiredBuildingDefinitionId=spec.Facility;
        value.requiredItems=new List<FestivalItemRequirement>{new(){itemDefinitionId=spec.Item,amount=spec.Amount}}; value.minimumParticipants=spec.Participants;
        value.successOutcome=new FestivalOutcomeDefinition{moodDelta=6,moodDurationDays=10,factionRapportDelta=spec.Culture.Length==0?3:0,griefConversionPercent=spec.Grief?25:0};
        value.partialOutcome=new FestivalOutcomeDefinition{moodDelta=2,moodDurationDays=5,factionRapportDelta=0,griefConversionPercent=spec.Grief?10:0};
        value.failureOutcome=new FestivalOutcomeDefinition{moodDelta=-3,moodDurationDays=4,factionRapportDelta=spec.Culture.Length==0?-2:0};
        EditorUtility.SetDirty(value); return value;
    }

    private static SeasonalWorldEventDefinitionSO CreateWorld(WorldSpec spec)
    {
        SeasonalWorldEventDefinitionSO value = Asset<SeasonalWorldEventDefinitionSO>($"{WorldRoot}/{spec.Id.Replace(':','_')}.asset");
        value.ConfigureMetadata(spec.Id,spec.Name,spec.Description,1,"V20 hand-authored seasonal world event manifest.");
        value.season=spec.Season; value.minimumDurationDays=spec.Minimum; value.maximumDurationDays=spec.Maximum;
        value.affectedDomainIds=new List<string>{spec.DomainA,spec.DomainB}; value.triggerRequirements=new V20ContentRequirementSet();
        value.startEffects=new List<V20ContentEffect>{new(){kind=spec.Effect,targetId=spec.Target,amount=spec.Amount,durationDays=spec.Maximum}};
        value.dailyEffects=new List<V20ContentEffect>(); value.endEffects=new List<V20ContentEffect>{new(){kind=V20ContentEffectKind.WorldFlag,targetId=$"resolved:{spec.Id}",amount=1}};
        EditorUtility.SetDirty(value); return value;
    }

    private static T Asset<T>(string path) where T:ScriptableObject { UnityEngine.Object existing=AssetDatabase.LoadMainAssetAtPath(path); if(existing!=null&&existing is not T) throw new InvalidOperationException($"Wrong asset type at '{path}'."); if(existing is T typed)return typed; T value=ScriptableObject.CreateInstance<T>();AssetDatabase.CreateAsset(value,path);return value; }
    private static void Ensure(string parent,string child){string path=$"{parent}/{child}";if(!AssetDatabase.IsValidFolder(path))AssetDatabase.CreateFolder(parent,child);}
    private static FestivalSpec F(string id,string name,string description,Season season,int day,string culture,string facility,string item,int amount,int participants,bool grief)=>new(){Id=id,Name=name,Description=description,Season=season,Day=day,Culture=culture,Facility=facility,Item=item,Amount=amount,Participants=participants,Grief=grief};
    private static WorldSpec W(string id,string name,string description,Season season,string a,string b,V20ContentEffectKind effect,string target,float amount,int min,int max)=>new(){Id=id,Name=name,Description=description,Season=season,DomainA=a,DomainB=b,Effect=effect,Target=target,Amount=amount,Minimum=min,Maximum=max};
}
#endif
