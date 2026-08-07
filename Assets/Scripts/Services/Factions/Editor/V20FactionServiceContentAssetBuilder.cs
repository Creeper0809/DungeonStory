#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class V20FactionServiceContentAssetBuilder
{
    private const string DomainCatalogPath = "Assets/Resources/SO/Content/GameDomainContentCatalog.asset";
    private const string ItemCatalogPath = "Assets/Resources/SO/Content/ItemDefinitionCatalog.asset";
    private const string Root = "Assets/Resources/SO/V20/Factions";
    private const string ServiceRoot = "Assets/Resources/SO/V20/Society/Services";
    private const string RelicRoot = "Assets/Resources/SO/V20/Items/FactionRelics";

    private sealed class FactionSpec
    {
        public string Key, Id, Name, Description, CrossFaction, SupplyItem, CrisisItem, StrategicItem;
        public string[] ChapterNames, ChapterDescriptions, RelicNames, RelicDescriptions;
    }

    private sealed class RequestSpec
    {
        public string Id, Name, Description, Facility, Item;
        public GuestRequestKind Kind;
        public int Amount, Deadline;
    }

    private static readonly FactionSpec[] Factions =
    {
        S("beastkin","faction:dungeon:beastkin","붉은발톱 교역소","사냥터와 피난민 사이에서 무리의 생존 규칙을 다시 정하려 한다.","faction:dungeon:demon","food:salted-meat-stew","medicine:standard","equipment:large-shield",
            new[]{"피 묻은 사냥깃","비어 가는 사냥터","재의 계약 사냥꾼","누구의 무리인가","우두머리의 실종","새로운 사냥 경계"},
            new[]{"붉은발톱 사절이 부상한 사냥꾼과 함께 식량을 요구한다.","과도한 사냥으로 먹잇감이 줄고 젊은 무리가 이탈한다.","악마 계약단이 같은 사냥터의 포획권을 주장한다.","던전은 보존 사육, 공동 사냥, 무기 지원 중 개입 방식을 정한다.","강경파가 우두머리의 실종을 던전의 배신으로 몰아간다.","무리와 던전이 동맹·종속·결별 중 새로운 경계를 확정한다."},
            new[]{"첫발톱 인장","무리뼈 호각","붉은사냥 망토핀"},
            new[]{"무리의 첫 공동 사냥에서 승자를 표시하던 인장.","흩어진 사냥대를 한 번만 되돌려 부르는 호각.","무리의 보호를 받은 외부인에게만 주는 핀."}),
        S("demon","faction:dungeon:demon","잿빛 계약정","불완전한 계약과 계승 분쟁을 이용해 세력을 재편하려 한다.","faction:dungeon:golem","material:paper","component:rune-conductor","item:lineage-seal",
            new[]{"세 번 접힌 계약","서명 없는 채무","돌맥 주조소의 담보","계약을 고치는 손","불타지 않는 조항","재 위의 새 인장"},
            new[]{"계약정 사절이 세 번 봉인된 협정의 증인을 요구한다.","주인의 죽음으로 서명 없는 채무가 여러 가구에 흩어진다.","골렘 주조소가 계약 담보로 잡힌 룬 도체의 반환을 거부한다.","던전은 공개 중재, 채무 매입, 계약 파기 중 하나를 선택한다.","강경 계약자가 파기해도 사라지지 않는 숨은 조항을 발동한다.","계약정과의 관계를 동맹·종속·결별로 새 인장에 새긴다."},
            new[]{"삼중계약 인장","재판관의 불씨","무효조항 두루마리"},
            new[]{"세 당사자가 같은 의무를 인정했다는 인장.","거짓 서명을 태울 때만 붉게 타는 작은 불씨.","한 번의 부당한 세력 의무를 무효화하는 원본 문서."}),
        S("golem","faction:dungeon:golem","돌맥 주조소","마모된 핵과 생산 할당량 사이에서 인격을 가진 구성체의 권리를 묻는다.","faction:dungeon:kobold","component:precision-parts","component:maintenance-parts","component:rune-conductor",
            new[]{"멈춘 운반자","기억 누락 보고","도구씨족의 설계권","수리인가 해방인가","주조핵의 명령","스스로 선택한 형상"},
            new[]{"정지한 골렘 운반자가 던전 문 앞에서 긴급 정비를 요청한다.","정기 초기화 과정에서 여러 골렘의 기억 일부가 사라진다.","코볼트 도구씨족이 골렘 관절 설계의 소유권을 주장한다.","던전은 정비 지원, 독립 작업반, 생산 계약 중 개입 방식을 정한다.","중앙 주조핵이 모든 골렘에게 강제 귀환 명령을 송신한다.","돌맥 구성체가 동맹·종속·결별 뒤 자신의 형상을 선택한다."},
            new[]{"자기각인 핵편","돌맥 조율쇠","첫자유 관절핀"},
            new[]{"타인이 덮어쓸 수 없는 개인 인격 각인 조각.","흐트러진 핵 공명을 한 번 안정시키는 조율쇠.","강제 명령을 거부한 첫 골렘의 관절에서 나온 핀."}),
        S("harpy","faction:dungeon:harpy","폭풍둥지","하늘길과 노래 기록의 독점을 둘러싸고 둥지 내부가 갈라졌다.","faction:dungeon:myconid","material:rope","medicine:antiseptic","item:weather-chart",
            new[]{"끊어진 전령줄","침묵한 합창단","포자구름의 하늘길","높이를 나누는 법","폭풍여왕의 거짓 노래","열린 바람의 맹세"},
            new[]{"폭풍둥지 전령이 끊어진 공중 운반로의 밧줄을 요구한다.","젊은 합창단이 장로의 전령 노래를 더는 부르지 않는다.","균사 정원의 포자구름이 전통 하늘길을 막는다.","던전은 새 통로, 공동 관측, 전령 호위 중 개입 방식을 정한다.","폭풍여왕이 조작된 경보 노래로 경쟁 둥지를 공격한다.","둥지는 동맹·종속·결별 뒤 하늘길을 누구에게 열지 맹세한다."},
            new[]{"첫바람 깃인장","폭풍음 쇳조각","귀환노래 방울"},
            new[]{"외부인에게 하늘길 통행을 허가하는 깃인장.","가장 큰 폭풍의 천둥을 기록한 공명 금속.","실종 전령의 귀환 노래를 기억하는 작은 방울."}),
        S("kobold","faction:dungeon:kobold","깊은톱니 굴","도구와 설계의 소유를 씨족이 가질지 제작자가 가질지 다툰다.","faction:dungeon:harpy","component:mechanical-parts","item:field-repair-kit","material:engineering-blueprint",
            new[]{"한 톱니 모자란 상자","도제의 이름 없는 설계","폭풍둥지 운반 특허","공방 문을 여는 대가","복제된 대가의 도장","공유 설계의 첫 장"},
            new[]{"코볼트 사절이 기계 부품 상자를 내밀며 누락 책임을 따진다.","씨족 공방이 죽은 도제의 설계를 장로 이름으로 등록한다.","하피가 공동 운반장치의 특허와 통행료를 요구한다.","던전은 제작자 권리, 씨족 공유, 공동 특허 중 방식을 정한다.","누군가 대가의 도장을 복제해 불량 시설을 대량 납품한다.","도구씨족은 동맹·종속·결별 뒤 설계 공개 범위를 확정한다."},
            new[]{"깊은톱니 대가도장","무결점 태엽","도제명판 원본"},
            new[]{"설계자가 직접 검사했음을 증명하는 대가도장.","오래 움직여도 오차가 쌓이지 않는 희귀 태엽.","지워진 도제의 이름을 복구한 첫 명판."}),
        S("myconid","faction:dungeon:myconid","균사정원","공동 기억망이 병든 포자와 오래된 조상의 의지를 함께 퍼뜨린다.","faction:dungeon:beastkin","food:cultured-mushroom","medicine:fungicide","item:climate-chart",
            new[]{"낯선 향의 사절","갈색으로 변한 기억","무리의 불길","어디까지 잘라낼까","조상균의 개화","새 정원의 경계"},
            new[]{"균사 사절이 말 대신 낯선 향과 배양 버섯을 보낸다.","공동 기억망 일부가 갈색으로 변하며 잘못된 기억을 반복한다.","수인 무리가 감염 포자를 막겠다며 외곽 정원을 태운다.","던전은 격리 절단, 치료 배양, 기억 백업 중 방식을 정한다.","고대 조상균이 개화해 생존자의 판단을 대신하려 한다.","균사정원은 동맹·종속·결별 뒤 새 기억망 경계를 정한다."},
            new[]{"첫포자 기억병","균맥 절단칼","새정원 배양편"},
            new[]{"말 없이 전달된 첫 동맹 기억을 담은 병.","병든 균사만 공명으로 구분해 자르는 칼.","조상균과 분리된 새 공동체의 첫 배양편."})
    };

    private static readonly RequestSpec[] Requests =
    {
        R("guest-request:coronation-feast","몰락 귀족의 대관 만찬","망명 귀족이 지지자 앞에서 호화 만찬을 열 객실과 음식을 요구한다.",GuestRequestKind.LuxuryMeal,"building:luxury-dining-room","food:luxury-feast",4,5),
        R("guest-request:allergen-banquet","금기 없는 화합식","서로 음식 금기가 다른 두 사절단이 같은 식탁을 요청한다.",GuestRequestKind.LuxuryMeal,"building:festival-common-hall","food:festival-sampler",8,4),
        R("guest-request:emergency-surgery","사절의 응급 수술","중상 사절이 무균 수술실과 고급 약품을 즉시 요구한다.",GuestRequestKind.Medical,"building:surgery-room","medicine:advanced",3,2),
        R("guest-request:plague-screening","대상단 검역","긴 대상행렬이 입장 전 전원 검사와 격리 공간을 요구한다.",GuestRequestKind.Medical,"building:isolation-ward","medicine:antiseptic",6,3),
        R("guest-request:precision-barter","정밀 부품 교역회","상인이 정밀 부품과 룬 도체의 현물 교환을 제안한다.",GuestRequestKind.Trade,"building:trade-counter","component:precision-parts",4,7),
        R("guest-request:winter-fuel-auction","겨울 연료 경매","세 세력이 숯과 석탄을 놓고 공개 경매를 열 장소를 요청한다.",GuestRequestKind.Trade,"building:faction-audience-hall","material:charcoal",12,5),
        R("guest-request:memorial-performance","전사자 추모 공연","용병단이 사망자 이름을 부르는 공연과 추모 공간을 요청한다.",GuestRequestKind.Spectacle,"workstation:v19:memorial-room","item:candle",10,5),
        R("guest-request:monster-circus","비전 생물 시연","학자 손님이 포획 생물의 안전한 공개 시연을 의뢰한다.",GuestRequestKind.Spectacle,"building:circus-arena","item:reinforced-restraint",4,6),
        R("guest-request:flood-refuge","범람 피난민","침수된 정착지 주민이 침대·물·보존식을 요청한다.",GuestRequestKind.Refuge,"building:guest-dormitory","food:preserved-ration",20,4),
        R("guest-request:persecuted-family","추방 가족의 은신","추격받는 혼혈 가족이 격리되지 않은 가족실을 요청한다.",GuestRequestKind.Refuge,"building:family-quarters","resource:clean-water",12,3),
        R("guest-request:sealed-archive","봉인 기록 판독","기록관이 안전한 연구실과 공학 도면을 대가로 봉인 문서를 맡긴다.",GuestRequestKind.Research,"building:prototype-laboratory","material:engineering-blueprint",2,8),
        R("guest-request:disease-sample","희귀 병원체 공동연구","외부 의사가 무균 시설과 항원 표본을 이용한 공동연구를 청한다.",GuestRequestKind.Research,"building:vaccine-laboratory","item:pathogen-sample",3,6),
        R("guest-request:militia-arms","민병대 긴급 무장","습격받는 정착지가 방패와 종이 탄약통을 긴급 요청한다.",GuestRequestKind.Armament,"building:armory","ammo:paper-cartridge",30,4),
        R("guest-request:bodyguard-kit","사절 호위 장비","위험 지역을 지날 사절단이 방폭 외투와 수리 키트를 요구한다.",GuestRequestKind.Armament,"building:smith-workshop","item:field-repair-kit",6,5)
    };

    [MenuItem("DungeonStory/V20/Build Faction and Service Content (100)")]
    public static void Build()
    {
        if (Factions.Length != 6 || Requests.Length != 14) throw new InvalidOperationException("V20 faction/service manifest header is invalid.");
        EnsureFolders();
        GameDomainContentCatalogSO domain = AssetDatabase.LoadAssetAtPath<GameDomainContentCatalogSO>(DomainCatalogPath) ?? throw new InvalidOperationException("Domain catalog missing.");
        ItemDefinitionCatalogSO items = AssetDatabase.LoadAssetAtPath<ItemDefinitionCatalogSO>(ItemCatalogPath) ?? throw new InvalidOperationException("Item catalog missing.");
        List<ScriptableObject> authored = new();
        List<ItemDefinitionSO> relics = new();
        foreach (FactionSpec faction in Factions)
        {
            List<FactionChapterDefinitionSO> chapters = Enumerable.Range(1,6).Select(number => CreateChapter(faction,number)).ToList();
            List<FactionContractDefinitionSO> contracts = Enumerable.Range(0,3).Select(index => CreateContract(faction,index)).ToList();
            List<ItemDefinitionSO> factionRelics = Enumerable.Range(0,3).Select(index => CreateRelic(faction,index)).ToList();
            authored.AddRange(chapters); authored.AddRange(contracts); authored.Add(CreateArc(faction,chapters,contracts,factionRelics)); relics.AddRange(factionRelics);
        }
        authored.AddRange(Requests.Select(CreateRequest));
        authored.AddRange(CreateIncidents());
        if (authored.Count != 82 || relics.Count != 18) throw new InvalidOperationException($"Expected 82 faction/service and 18 relic definitions; found {authored.Count}/{relics.Count}.");
        List<string> errors = authored.OfType<V20AuthoredContentSO>().SelectMany(x=>x.ValidateDefinition()).Concat(relics.SelectMany(x=>x.ValidateDefinition())).ToList();
        if(errors.Count>0) throw new InvalidOperationException(string.Join(" | ",errors));
        Type[] owned={typeof(FactionArcDefinitionSO),typeof(FactionChapterDefinitionSO),typeof(FactionContractDefinitionSO),typeof(GuestRequestDefinitionSO),typeof(ServiceIncidentDefinitionSO)};
        domain.SetDefinitions(domain.Definitions.Where(x=>x!=null&&!owned.Contains(x.GetType())).Concat(authored));
        HashSet<string> relicIds=relics.Select(x=>x.ItemId).ToHashSet(StringComparer.Ordinal);
        items.SetDefinitions(items.Definitions.Where(x=>x!=null&&!relicIds.Contains(x.ItemId)).Concat(relics));
        EditorUtility.SetDirty(domain);EditorUtility.SetDirty(items);AssetDatabase.SaveAssets();AssetDatabase.Refresh();
        Debug.Log("V20_FACTION_SERVICE_CONTENT=PASS; arcs=6; chapters=36; contracts=18; requests=14; incidents=8; relics=18");
    }

    private static FactionArcDefinitionSO CreateArc(FactionSpec spec,IReadOnlyList<FactionChapterDefinitionSO> chapters,IReadOnlyList<FactionContractDefinitionSO> contracts,IReadOnlyList<ItemDefinitionSO> relics)
    {
        FactionArcDefinitionSO value=Asset<FactionArcDefinitionSO>($"{Root}/Arcs/arc_{spec.Key}.asset");Meta(value,$"faction-arc:{spec.Key}",spec.Name,spec.Description);
        value.factionId=spec.Id;value.chapterIds=chapters.Select(x=>x.StableId).ToList();value.contractIds=contracts.Select(x=>x.StableId).ToList();value.relicItemIds=relics.Select(x=>x.ItemId).ToList();Dirty(value);return value;
    }

    private static FactionChapterDefinitionSO CreateChapter(FactionSpec spec,int number)
    {
        FactionChapterDefinitionSO value=Asset<FactionChapterDefinitionSO>($"{Root}/Chapters/chapter_{spec.Key}_{number}.asset");
        Meta(value,$"faction-chapter:{spec.Key}:{number}",spec.ChapterNames[number-1],spec.ChapterDescriptions[number-1]);value.factionId=spec.Id;value.chapterNumber=number;value.kind=(FactionChapterKind)(number-1);value.crossFactionId=number==3?spec.CrossFaction:string.Empty;
        value.triggerRequirements=new V20ContentRequirementSet{factions=new List<V20FactionRequirement>{new(){factionId=spec.Id,minimumRapport=-100,maximumGrievance=100}}};
        value.choices=new List<V20ChoiceDefinition>{Choice("support","요구를 받아들인다",V20ContentEffectKind.FactionRapport,spec.Id,8),Choice("bargain","대가와 책임을 협상한다",V20ContentEffectKind.FactionObligation,spec.Id,1),Choice("refuse","개입을 거부한다",V20ContentEffectKind.FactionGrievance,spec.Id,7)};
        Dirty(value);return value;
    }

    private static FactionContractDefinitionSO CreateContract(FactionSpec spec,int index)
    {
        string[] suffix={"supply","crisis","strategic"};string[] names={"정기 물자 계약","위기 대응 계약","장기 전략 계약"};string[] items={spec.SupplyItem,spec.CrisisItem,spec.StrategicItem};int[] amounts={12,6,4};
        FactionContractDefinitionSO value=Asset<FactionContractDefinitionSO>($"{Root}/Contracts/contract_{spec.Key}_{suffix[index]}.asset");
        Meta(value,$"faction-contract:{spec.Key}:{suffix[index]}",$"{spec.Name} {names[index]}",$"{spec.Name}이 실제 물자와 작업 결과를 요구하는 {names[index]}이다.");value.factionId=spec.Id;value.kind=(V20FactionContractKind)index;value.deadlineDays=index==0?20:index==1?7:45;
        value.completionRequirements=new V20ContentRequirementSet{items=new List<V20ItemAmountRequirement>{new(){itemDefinitionId=items[index],amount=amounts[index],consume=true}}};
        value.successEffects=new List<V20ContentEffect>{Effect(V20ContentEffectKind.FactionRapport,spec.Id,index==2?15:8),Effect(V20ContentEffectKind.FactionObligation,spec.Id,1)};
        value.failureEffects=new List<V20ContentEffect>{Effect(V20ContentEffectKind.FactionGrievance,spec.Id,index==1?12:7)};Dirty(value);return value;
    }

    private static ItemDefinitionSO CreateRelic(FactionSpec spec,int index)
    {
        string id=$"relic:faction:{spec.Key}:{index+1}";GenericItemDefinitionSO value=Asset<GenericItemDefinitionSO>($"{RelicRoot}/relic_{spec.Key}_{index+1}.asset");
        value.ConfigureCore(id,spec.RelicNames[index],spec.RelicDescriptions[index],StockCategory.General,0,.2f,1);value.RemoveFeature<ProductionItemFeature>();value.RemoveFeature<MarketItemFeature>();Dirty(value);return value;
    }

    private static GuestRequestDefinitionSO CreateRequest(RequestSpec spec)
    {
        GuestRequestDefinitionSO value=Asset<GuestRequestDefinitionSO>($"{ServiceRoot}/GuestRequests/{spec.Id.Replace(':','_')}.asset");Meta(value,spec.Id,spec.Name,spec.Description);value.kind=spec.Kind;value.deadlineDays=spec.Deadline;
        value.serviceRequirements=new V20ContentRequirementSet{items=new List<V20ItemAmountRequirement>{new(){itemDefinitionId=spec.Item,amount=spec.Amount,consume=true}},facilities=new List<V20FacilityRequirement>{new(){buildingDefinitionId=spec.Facility,minimumCount=1,mustBeOperational=true}}};
        value.successEffects=new List<V20ContentEffect>{Effect(V20ContentEffectKind.Money,"guest-service",spec.Amount*20),Effect(V20ContentEffectKind.FactionRapport,"requesting-faction",4)};value.failureEffects=new List<V20ContentEffect>{Effect(V20ContentEffectKind.FactionGrievance,"requesting-faction",5)};Dirty(value);return value;
    }

    private static IEnumerable<ServiceIncidentDefinitionSO> CreateIncidents()
    {
        yield return Incident(ServiceIncidentKind.Brawl,"난투","술자리의 모욕이 무장 난투로 번졌다.",("separate","경비가 분리한다",V20ContentEffectKind.Health,-3),("mediate","당사자를 중재한다",V20ContentEffectKind.Relationship,4),("arrest","주동자를 체포한다",V20ContentEffectKind.FactionGrievance,4));
        yield return Incident(ServiceIncidentKind.Theft,"절도","손님의 귀중품이 사라지고 직원 한 명이 의심받는다.",("search","공개 수색한다",V20ContentEffectKind.WorkDelayDays,1),("compensate","현물로 보상한다",V20ContentEffectKind.Money,-100),("investigate","기록과 동선을 조사한다",V20ContentEffectKind.WorldFlag,1));
        yield return Incident(ServiceIncidentKind.Contamination,"객실 오염","객실과 음식에서 같은 오염원이 발견됐다.",("quarantine","구역을 격리한다",V20ContentEffectKind.WorkDelayDays,2),("treat","노출자를 치료한다",V20ContentEffectKind.Health,5),("conceal","오염 사실을 은폐한다",V20ContentEffectKind.Threat,6));
        yield return Incident(ServiceIncidentKind.CulturalInsult,"문화적 모욕","사절이 다른 문화의 장례 관습을 공개적으로 조롱했다.",("apology","공개 사과를 요구한다",V20ContentEffectKind.Relationship,5),("ritual","화해 의식을 연다",V20ContentEffectKind.Mood,4),("expel","모욕한 사절을 추방한다",V20ContentEffectKind.FactionGrievance,8));
        yield return Incident(ServiceIncidentKind.ForbiddenMeal,"금기 음식 제공","조리표 오류로 손님에게 금기 음식이 제공됐다.",("replace","즉시 대체식을 제공한다",V20ContentEffectKind.Mood,2),("compensate","숙박비를 보상한다",V20ContentEffectKind.Money,-60),("deny","금기 표기가 없었다고 항변한다",V20ContentEffectKind.FactionGrievance,5));
        yield return Incident(ServiceIncidentKind.MedicalCollapse,"의료 응급상황","식사 중 손님이 급성 증상으로 쓰러졌다.",("emergency-care","응급 치료한다",V20ContentEffectKind.Health,8),("transfer","격리 병동으로 옮긴다",V20ContentEffectKind.WorkDelayDays,1),("refuse","외부 환자라 거부한다",V20ContentEffectKind.FactionGrievance,10));
        yield return Incident(ServiceIncidentKind.EnvoyConflict,"외교 사절 충돌","적대 세력 사절이 같은 객실 구역에서 마주쳤다.",("separate-envoys","동선을 분리한다",V20ContentEffectKind.WorkDelayDays,1),("joint-talk","공동 회담을 연다",V20ContentEffectKind.FactionRapport,5),("choose-side","한쪽을 퇴실시킨다",V20ContentEffectKind.FactionGrievance,8));
        yield return Incident(ServiceIncidentKind.Sabotage,"내부 파괴 공작","손님 구역에서 생산 시설로 이어진 고의 파손 흔적이 발견됐다.",("lockdown","시설을 봉쇄하고 수색한다",V20ContentEffectKind.WorkDelayDays,2),("bait","가짜 부품으로 공작원을 유인한다",V20ContentEffectKind.WorldFlag,1),("quiet-repair","조용히 수리하고 감시한다",V20ContentEffectKind.Threat,3));
    }

    private static ServiceIncidentDefinitionSO Incident(ServiceIncidentKind kind,string name,string description,params (string id,string title,V20ContentEffectKind kind,float amount)[] responses)
    {
        string id=$"service-incident:{kind.ToString().ToLowerInvariant()}";ServiceIncidentDefinitionSO value=Asset<ServiceIncidentDefinitionSO>($"{ServiceRoot}/Incidents/{id.Replace(':','_')}.asset");Meta(value,id,name,description);value.kind=kind;value.triggerRequirements=new V20ContentRequirementSet();value.responses=responses.Select(x=>Choice(x.id,x.title,x.kind,IsFactionEffect(x.kind)?"affected-faction":id,x.amount)).ToList();Dirty(value);return value;
    }

    private static V20ChoiceDefinition Choice(string id,string title,V20ContentEffectKind kind,string target,float amount)=>new(){choiceId=id,title=title,outcomeText=title,requirements=new V20ContentRequirementSet(),effects=new List<V20ContentEffect>{Effect(kind,target,amount)}};
    private static V20ContentEffect Effect(V20ContentEffectKind kind,string target,float amount)=>new(){kind=kind,targetId=target,amount=amount};
    private static bool IsFactionEffect(V20ContentEffectKind kind)=>kind is V20ContentEffectKind.FactionRapport or V20ContentEffectKind.FactionGrievance or V20ContentEffectKind.FactionObligation;
    private static void Meta(V20AuthoredContentSO value,string id,string name,string description)=>value.ConfigureMetadata(id,name,description,1,"V20 hand-authored faction/service manifest.");
    private static void Dirty(UnityEngine.Object value)=>EditorUtility.SetDirty(value);
    private static T Asset<T>(string path) where T:ScriptableObject{UnityEngine.Object existing=AssetDatabase.LoadMainAssetAtPath(path);if(existing!=null&&existing is not T)throw new InvalidOperationException($"Wrong asset type at '{path}'.");if(existing is T typed)return typed;T value=ScriptableObject.CreateInstance<T>();AssetDatabase.CreateAsset(value,path);return value;}
    private static void EnsureFolders(){Ensure("Assets/Resources/SO/V20","Factions");foreach(string folder in new[]{"Arcs","Chapters","Contracts"})Ensure(Root,folder);Ensure("Assets/Resources/SO/V20/Society","Services");Ensure(ServiceRoot,"GuestRequests");Ensure(ServiceRoot,"Incidents");Ensure("Assets/Resources/SO/V20","Items");Ensure("Assets/Resources/SO/V20/Items","FactionRelics");}
    private static void Ensure(string parent,string child){string path=$"{parent}/{child}";if(!AssetDatabase.IsValidFolder(path))AssetDatabase.CreateFolder(parent,child);}
    private static FactionSpec S(string key,string id,string name,string description,string cross,string supply,string crisis,string strategic,string[] chapterNames,string[] chapterDescriptions,string[] relicNames,string[] relicDescriptions)=>new(){Key=key,Id=id,Name=name,Description=description,CrossFaction=cross,SupplyItem=supply,CrisisItem=crisis,StrategicItem=strategic,ChapterNames=chapterNames,ChapterDescriptions=chapterDescriptions,RelicNames=relicNames,RelicDescriptions=relicDescriptions};
    private static RequestSpec R(string id,string name,string description,GuestRequestKind kind,string facility,string item,int amount,int deadline)=>new(){Id=id,Name=name,Description=description,Kind=kind,Facility=facility,Item=item,Amount=amount,Deadline=deadline};
}
#endif
