#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class V20EcologyContentAssetBuilder
{
    private const string CatalogPath="Assets/Resources/SO/Content/GameDomainContentCatalog.asset";
    private const string WildlifeRoot="Assets/Resources/SO/V20/Ecology/Wildlife";
    private const string DiseaseRoot="Assets/Resources/SO/V20/Ecology/Diseases";
    private const string CropRoot="Assets/Resources/SO/V20/Ecology/Cultivars";
    private sealed class WildlifeSpec{public string Id,Name,Description,Nest,Migration,Disease;public WildlifeDietType Diet;public WildlifeHabitatType[] Habitats;public float Aggression;public bool Domesticable;public string[] Prey,Predators;public Season Breed;public Season[] Active;}
    private sealed class DiseaseSpec{public string Id,Name,Description,Symptom;public DiseaseTransmissionRoute Routes;public DiseaseTargetSystem Target;public int Incubation,Contagious;public float Infection,Severity;public string[] Responses;}
    private sealed class CultivarSpec{public string Id,Crop,Name,Description;public string[] Costs;public int Cold,Heat,Growth,Yield,Disease,Seed;}

    private static readonly WildlifeSpec[] NewWildlife=
    {
        W("glow_moth","발광나방","마나 빛을 따라 이동하며 버섯 포자를 옮기는 작은 계절 곤충.",WildlifeDietType.Herbivore,new[]{WildlifeHabitatType.Brush,WildlifeHabitatType.Lair},.05f,false,new string[0],new[]{"cave_rat","silk_spider"},"nest:ceiling-cocoon",Season.Spring,"migration:mana-lamp","disease:mana-pox",new[]{Season.Spring,Season.Summer}),
        W("ash_crawler","재먼지 기는벌레","재 더미를 먹고 환기구에 군집해 재먼지폐를 퍼뜨린다.",WildlifeDietType.Scavenger,new[]{WildlifeHabitatType.Burrow,WildlifeHabitatType.Lair},.2f,false,new string[0],new[]{"ember_lizard","cave_hound"},"nest:ash-vent",Season.Summer,"migration:heat-duct","disease:ash-lung",new[]{Season.Summer,Season.Autumn}),
        W("deep_goat","심층염소","절벽 이끼를 먹고 길들일 수 있어 털과 운반 노동을 제공한다.",WildlifeDietType.Herbivore,new[]{WildlifeHabitatType.Grass,WildlifeHabitatType.Lair},.15f,true,new string[0],new[]{"shadow_wolf","cave_hound"},"nest:stone-ledge",Season.Spring,"migration:vertical-graze","disease:deep-parasitosis",new[]{Season.Spring,Season.Summer,Season.Autumn}),
        W("crystal_beetle","수정딱정벌레","마나 결정 가루를 먹고 단단한 갑각을 남기는 군집 곤충.",WildlifeDietType.Omnivore,new[]{WildlifeHabitatType.Burrow,WildlifeHabitatType.Lair},.1f,true,new string[0],new[]{"moss_boar","ember_lizard"},"nest:crystal-crevice",Season.Winter,"migration:mana-vein","disease:glass-blood",new[]{Season.Autumn,Season.Winter}),
        W("tunnel_mole","굴착두더지","뿌리 작물을 해치지만 길들인 개체는 얕은 광맥을 찾는다.",WildlifeDietType.Herbivore,new[]{WildlifeHabitatType.Burrow,WildlifeHabitatType.Grass},.1f,true,new string[0],new[]{"cave_hound","shadow_wolf"},"nest:root-burrow",Season.Spring,"migration:soft-soil","disease:deep-parasitosis",new[]{Season.Spring,Season.Autumn}),
        W("carrion_drake","시체청소 비룡","전장과 도살장의 사체를 찾아오며 병원체를 먼 거리로 옮긴다.",WildlifeDietType.Scavenger,new[]{WildlifeHabitatType.Lair,WildlifeHabitatType.Brush},.8f,false,new[]{"cave_rat","shadow_hare"},new string[0],"nest:high-bone-pile",Season.Autumn,"migration:battlefield-scent","disease:gut-rot",new[]{Season.Autumn,Season.Winter}),
        W("mire_leech","진흙거머리","오염된 물에서 번식해 가축과 직원을 흡혈한다.",WildlifeDietType.Carnivore,new[]{WildlifeHabitatType.Water},.45f,false,new[]{"rune_deer","deep_goat"},new[]{"spore_elk"},"nest:mud-clutch",Season.Summer,"migration:flood-channel","disease:blood-wasting",new[]{Season.Spring,Season.Summer}),
        W("frost_ram","서리숫양","겨울에 활동하는 대형 초식동물로 길들이면 털과 운반력을 제공한다.",WildlifeDietType.Herbivore,new[]{WildlifeHabitatType.Grass,WildlifeHabitatType.Lair},.55f,true,new string[0],new[]{"shadow_wolf"},"nest:frost-alcove",Season.Winter,"migration:cold-front","disease:cave-flu",new[]{Season.Winter,Season.Spring}),
        W("ember_lizard","잿불도마뱀","뜨거운 배관 주변 해충을 먹지만 잿불열을 매개한다.",WildlifeDietType.Carnivore,new[]{WildlifeHabitatType.Lair,WildlifeHabitatType.Burrow},.35f,true,new[]{"ash_crawler","crystal_beetle"},new[]{"cave_hound"},"nest:warm-pipe",Season.Summer,"migration:heat-source","disease:ember-fever",new[]{Season.Summer,Season.Autumn}),
        W("spore_elk","포자큰사슴","균사 정원의 포자를 멀리 퍼뜨리는 대형 초식동물.",WildlifeDietType.Herbivore,new[]{WildlifeHabitatType.Grass,WildlifeHabitatType.Brush},.25f,true,new string[0],new[]{"shadow_wolf","mire_leech"},"nest:mycelial-grove",Season.Autumn,"migration:spore-bloom","disease:white-spore",new[]{Season.Spring,Season.Autumn}),
        W("mana_wisp","마나도깨비불","전력망 누출을 따라 모이며 다른 비전 생물을 끌어들인다.",WildlifeDietType.Omnivore,new[]{WildlifeHabitatType.Lair,WildlifeHabitatType.Water},.05f,false,new string[0],new[]{"rune_deer"},"nest:rune-node",Season.Winter,"migration:power-flux","disease:mana-pox",new[]{Season.Summer,Season.Winter}),
        W("cave_hound","동굴사냥개","무리 사냥을 하며 길들이면 경비와 추적에 쓸 수 있다.",WildlifeDietType.Carnivore,new[]{WildlifeHabitatType.Lair,WildlifeHabitatType.Brush},.7f,true,new[]{"cave_rat","shadow_hare","tunnel_mole"},new[]{"carrion_drake"},"nest:pack-den",Season.Spring,"migration:prey-trail","disease:red-fever",new[]{Season.Spring,Season.Summer,Season.Autumn,Season.Winter}),
        W("silk_spider","동굴명주거미","발광나방을 잡고 채집 가능한 질긴 거미줄을 만든다.",WildlifeDietType.Carnivore,new[]{WildlifeHabitatType.Lair,WildlifeHabitatType.Brush},.3f,true,new[]{"glow_moth","cave_rat"},new[]{"cave_hound"},"nest:silk-web",Season.Autumn,"migration:insect-swarm","disease:dream-mold",new[]{Season.Spring,Season.Autumn})
    };

    private static readonly DiseaseSpec[] Diseases=
    {
        D("disease:ash-lung","재먼지폐","재와 금속 분진이 폐에 쌓여 호흡과 장시간 작업을 무너뜨린다.",DiseaseTransmissionRoute.Air,DiseaseTargetSystem.Breathing,4,14,.09f,52,"symptom:progressive-breathlessness","response:respirator","response:wet-cleaning"),
        D("disease:deep-parasitosis","심층 기생충증","오염된 고기와 물의 기생충이 영양을 빼앗고 빈혈을 만든다.",DiseaseTransmissionRoute.Food|DiseaseTransmissionRoute.Water,DiseaseTargetSystem.Digestion,3,12,.18f,48,"symptom:wasting-anemia","response:boil-water","response:antiparasitic"),
        D("disease:white-spore","백색포자병","피부와 균사 조직에 흰 균사가 번져 감각과 이동을 둔화한다.",DiseaseTransmissionRoute.Air|DiseaseTransmissionRoute.Contact,DiseaseTargetSystem.Filtration,5,15,.11f,58,"symptom:white-mycelial-plaque","response:fungicide-wash","response:dry-isolation"),
        D("disease:glass-blood","유리혈증","마나 결정 미립자가 혈액에 결정화되어 출혈과 비전 폭주를 일으킨다.",DiseaseTransmissionRoute.ManaExposure,DiseaseTargetSystem.Filtration,6,18,.07f,68,"symptom:crystal-clotting","response:mana-shield","response:blood-filtration"),
        D("disease:night-thirst","밤갈증병","야간에 극심한 갈증과 충동을 유발하며 혈액 접촉으로 번진다.",DiseaseTransmissionRoute.Blood,DiseaseTargetSystem.Consciousness,4,16,.14f,62,"symptom:nocturnal-thirst","response:sealed-blood-reserve","response:night-watch"),
        D("disease:green-swarm","녹무리 감염","미세 곤충 군체가 상처와 털에 붙어 접촉자와 축사를 오간다.",DiseaseTransmissionRoute.Contact,DiseaseTargetSystem.Filtration,2,9,.2f,46,"symptom:moving-green-rash","response:hot-wash","response:pest-lure"),
        D("disease:ember-fever","잿불열","고열과 열 환각을 일으키며 뜨거운 환기구 주변에서 확산된다.",DiseaseTransmissionRoute.Air|DiseaseTransmissionRoute.Contact,DiseaseTargetSystem.Consciousness,2,8,.16f,64,"symptom:heat-hallucination","response:cooling-bed","response:vent-seal"),
        D("disease:dream-mold","꿈곰팡이증","수면 중 포자성 환각을 공유하게 만들어 집단 판단을 흐린다.",DiseaseTransmissionRoute.Air,DiseaseTargetSystem.Consciousness,7,20,.08f,57,"symptom:shared-dream-delirium","response:dreamless-sedative","response:spore-filter")
    };

    private static readonly CultivarSpec[] Cultivars=
    {
        C("genome:twilight-grain:frost","crop:twilight-grain","서리저녁밀","이른 서리에도 이삭을 지키는 저온 저항형.",new[]{"cost:growth-time"},2,0,-1,0,1,0),
        C("genome:shade-fiber:heat","crop:shade-fiber","잿빛그늘섬유","고온 작업장 인근에서도 줄기가 무너지지 않는다.",new[]{"cost:seed-yield"},0,2,0,0,1,-1),
        C("genome:night-grape:cold","crop:night-grape","서리밤포도","차가운 동굴에서도 당도를 유지하지만 성장이 느리다.",new[]{"cost:growth-time"},2,0,-1,1,0,0),
        C("genome:moonflower:heat","crop:moonflower","열달꽃","마나 조명과 고온에서 꽃가루 손실이 적다.",new[]{"cost:disease-resistance"},0,2,0,1,-1,0),
        C("genome:ember-root:cold","crop:ember-root","동토불뿌리","낮은 토온에서도 뿌리를 키우지만 비옥도를 많이 먹는다.",new[]{"cost:fertility-consumption"},2,0,0,1,0,0),
        C("genome:cave-mushroom:dry","crop:cave-mushroom","마른굴버섯","습도 변동에 강하지만 포자 회수량이 적다.",new[]{"cost:seed-yield"},1,1,0,0,1,-2),
        C("genome:bloodleaf:heat","crop:bloodleaf","잿불혈엽","고온에서도 잎이 타지 않지만 병해 저항이 낮다.",new[]{"cost:disease-resistance"},0,2,0,1,-2,0),
        C("genome:dreamleaf:cold","crop:dreamleaf","긴밤꿈잎","겨울 저온에서 향 성분을 유지하지만 성장이 느리다.",new[]{"cost:growth-time"},2,0,-2,1,0,0),
        C("genome:twilight-grain:abundant","crop:twilight-grain","풍작저녁밀","곡물 수확량을 크게 높인 대신 녹병에 약하다.",new[]{"cost:disease-resistance"},0,0,0,2,-2,0),
        C("genome:ember-root:heavy","crop:ember-root","중량불뿌리","큰 뿌리를 맺지만 비옥도 소비와 성장 시간이 늘어난다.",new[]{"cost:fertility-consumption","cost:growth-time"},0,0,-1,2,0,0),
        C("genome:shade-fiber:long","crop:shade-fiber","장섬유그늘풀","섬유 수율이 높지만 허용 온도 폭이 좁다.",new[]{"cost:temperature-range"},-1,-1,0,2,0,0),
        C("genome:night-grape:cluster","crop:night-grape","다발밤포도","과실 송이가 크지만 다음 세대 종자 회수가 줄어든다.",new[]{"cost:seed-yield"},0,0,0,2,0,-2)
    };

    [MenuItem("DungeonStory/V20/Build Ecology Content (33)")]
    public static void Build()
    {
        if(NewWildlife.Length!=13||Diseases.Length!=8||Cultivars.Length!=12)throw new InvalidOperationException("V20 ecology manifest count contract is broken.");
        Ensure("Assets/Resources/SO/V20","Ecology");foreach(string folder in new[]{"Wildlife","Diseases","Cultivars"})Ensure("Assets/Resources/SO/V20/Ecology",folder);
        GameDomainContentCatalogSO catalog=AssetDatabase.LoadAssetAtPath<GameDomainContentCatalogSO>(CatalogPath)??throw new InvalidOperationException("Domain catalog missing.");
        List<WildlifeSpeciesSO> existing=catalog.GetAll<WildlifeSpeciesSO>().Where(x=>x!=null).OrderBy(x=>x.SpeciesId,StringComparer.Ordinal).ToList();if(existing.Count!=5)throw new InvalidOperationException($"Expected five existing wildlife species, found {existing.Count}.");foreach(WildlifeSpeciesSO value in existing)UpgradeExistingWildlife(value);
        List<WildlifeSpeciesSO> wildlife=existing.Concat(NewWildlife.Select(CreateWildlife)).ToList();
        List<DiseaseDefinitionSO> existingDiseases=catalog.GetAll<DiseaseDefinitionSO>().Where(x=>x!=null).ToList();if(existingDiseases.Count!=8)throw new InvalidOperationException($"Expected eight existing diseases, found {existingDiseases.Count}.");foreach(DiseaseDefinitionSO value in existingDiseases)UpgradeExistingDisease(value);
        List<DiseaseDefinitionSO> diseases=existingDiseases.Concat(Diseases.Select(CreateDisease)).ToList();
        List<CropGenomeDefinitionSO> bases=catalog.GetAll<CropGenomeDefinitionSO>().Where(x=>x!=null&&x.GenomeId.EndsWith(":base",StringComparison.Ordinal)).ToList();if(bases.Count!=8)throw new InvalidOperationException($"Expected eight base crop genomes, found {bases.Count}.");foreach(CropGenomeDefinitionSO value in bases){value.Configure(value.GenomeId,value.CropId,value.CreateRuntimeDefinition().loci);Dirty(value);}List<CropGenomeDefinitionSO> cultivars=Cultivars.Select(CreateCultivar).ToList();List<CropGenomeDefinitionSO> genomes=bases.Concat(cultivars).ToList();
        List<string> errors=wildlife.SelectMany(x=>x.ValidateDefinition()).Concat(diseases.SelectMany(x=>x.ValidateDefinition())).Concat(genomes.SelectMany(x=>x.ValidateDefinition())).ToList();if(errors.Count>0)throw new InvalidOperationException(string.Join(" | ",errors));
        catalog.SetDefinitions(catalog.Definitions.Where(x=>x is not WildlifeSpeciesSO&&x is not DiseaseDefinitionSO&&x is not CropGenomeDefinitionSO).Concat(wildlife).Concat(diseases).Concat(genomes));Dirty(catalog);AssetDatabase.SaveAssets();AssetDatabase.Refresh();Debug.Log("V20_ECOLOGY_CONTENT=PASS; wildlifeTotal=18; wildlifeNew=13; diseaseTotal=16; diseaseNew=8; genomeTotal=20; cultivarNew=12; netNew=33");
    }

    private static WildlifeSpeciesSO CreateWildlife(WildlifeSpec s){WildlifeSpeciesSO v=Asset<WildlifeSpeciesSO>($"{WildlifeRoot}/wildlife_{s.Id}.asset");v.ConfigureV20(s.Id,s.Name,s.Description,s.Diet,s.Habitats,s.Aggression,s.Domesticable,s.Prey,s.Predators,s.Nest,s.Breed,s.Migration,new[]{s.Disease},s.Active);Dirty(v);return v;}
    private static void UpgradeExistingWildlife(WildlifeSpeciesSO v){string prey=v.SpeciesId=="shadow_wolf"?"shadow_hare":"cave_rat";string predator=v.SpeciesId=="shadow_wolf"?"carrion_drake":"shadow_wolf";v.ConfigureV20(v.SpeciesId,v.DisplayName,v.Description,v.Diet,v.PreferredHabitats.Count>0?v.PreferredHabitats:new[]{WildlifeHabitatType.Grass},v.Aggression,v.Husbandry.Domesticable,new[]{prey},new[]{predator},$"nest:{v.SpeciesId}",Season.Spring,$"migration:{v.SpeciesId}",new[]{"disease:cave-flu"},new[]{Season.Spring,Season.Summer,Season.Autumn});Dirty(v);}
    private static DiseaseDefinitionSO CreateDisease(DiseaseSpec s){DiseaseDefinitionSO v=Asset<DiseaseDefinitionSO>($"{DiseaseRoot}/{s.Id.Replace(':','_')}.asset");v.stableId=s.Id;v.displayName=s.Name;v.description=s.Description;v.routes=s.Routes;v.targetSystem=s.Target;v.incubationDays=s.Incubation;v.contagiousDays=s.Contagious;v.baseInfectionProbability=s.Infection;v.baseSeverity=s.Severity;v.vaccineAllowed=true;v.chronic=false;v.authoringRevision=1;v.sourceNote="V20 hand-authored disease.";v.symptomProfileId=s.Symptom;v.fieldResponseIds=s.Responses.ToList();Dirty(v);return v;}
    private static void UpgradeExistingDisease(DiseaseDefinitionSO v){v.authoringRevision=1;v.description=string.IsNullOrWhiteSpace(v.description)?$"{v.displayName}의 고유 전파와 기관 증상.":v.description;v.sourceNote="V19 disease upgraded to V20 symptom/response contract.";v.symptomProfileId=$"symptom:{v.stableId.Replace("disease:",string.Empty)}";v.fieldResponseIds=new List<string>{$"response:isolate:{v.stableId}",v.vaccineAllowed?$"response:vaccine:{v.stableId}":$"response:environment:{v.stableId}"};Dirty(v);}
    private static CropGenomeDefinitionSO CreateCultivar(CultivarSpec s){CropGenomeDefinitionSO v=Asset<CropGenomeDefinitionSO>($"{CropRoot}/{s.Id.Replace(':','_')}.asset");v.id=9000+Array.IndexOf(Cultivars,s);v.ConfigureCultivar(s.Id,s.Crop,s.Name,s.Description,s.Costs,Loci(s));Dirty(v);return v;}
    private static IReadOnlyList<DiploidLocusSaveData> Loci(CultivarSpec s)=>new[]{L(CropGenomeLocus.ColdTolerance,s.Cold),L(CropGenomeLocus.HeatTolerance,s.Heat),L(CropGenomeLocus.GrowthSpeed,s.Growth),L(CropGenomeLocus.Yield,s.Yield),L(CropGenomeLocus.DiseaseResistance,s.Disease),L(CropGenomeLocus.SeedYield,s.Seed)};
    private static DiploidLocusSaveData L(CropGenomeLocus locus,int value)=>new(){locus=locus,alleleA=Mathf.Clamp(value,-2,2),alleleB=Mathf.Clamp(value,-2,2)};
    private static T Asset<T>(string path)where T:ScriptableObject{UnityEngine.Object x=AssetDatabase.LoadMainAssetAtPath(path);if(x!=null&&x is not T)throw new InvalidOperationException($"Wrong asset type at '{path}'.");if(x is T t)return t;T v=ScriptableObject.CreateInstance<T>();AssetDatabase.CreateAsset(v,path);return v;}private static void Ensure(string parent,string child){string p=$"{parent}/{child}";if(!AssetDatabase.IsValidFolder(p))AssetDatabase.CreateFolder(parent,child);}private static void Dirty(UnityEngine.Object v)=>EditorUtility.SetDirty(v);
    private static WildlifeSpec W(string id,string name,string description,WildlifeDietType diet,WildlifeHabitatType[] habitats,float aggression,bool domesticable,string[] prey,string[] predators,string nest,Season breed,string migration,string disease,Season[] active)=>new(){Id=id,Name=name,Description=description,Diet=diet,Habitats=habitats,Aggression=aggression,Domesticable=domesticable,Prey=prey,Predators=predators,Nest=nest,Breed=breed,Migration=migration,Disease=disease,Active=active};
    private static DiseaseSpec D(string id,string name,string description,DiseaseTransmissionRoute routes,DiseaseTargetSystem target,int incubation,int contagious,float infection,float severity,string symptom,params string[] responses)=>new(){Id=id,Name=name,Description=description,Routes=routes,Target=target,Incubation=incubation,Contagious=contagious,Infection=infection,Severity=severity,Symptom=symptom,Responses=responses};
    private static CultivarSpec C(string id,string crop,string name,string description,string[] costs,int cold,int heat,int growth,int yield,int disease,int seed)=>new(){Id=id,Crop=crop,Name=name,Description=description,Costs=costs,Cold=cold,Heat=heat,Growth=growth,Yield=yield,Disease=disease,Seed=seed};
}
#endif
