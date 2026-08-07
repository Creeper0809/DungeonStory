#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class V20NarrativeContentAssetBuilder
{
    private const string CatalogPath = "Assets/Resources/SO/Content/GameDomainContentCatalog.asset";
    private const string Root = "Assets/Resources/SO/V20/Narrative";
    private const string RevisionNote = "V20 content-density expansion; hand-authored Korean source revision 1.";

    private sealed class BackgroundSpec
    {
        public string Id, Name, Description, Skill, Memory, Faction;
        public int Experience, Reaction;
    }

    private sealed class AmbitionSpec
    {
        public string Id, Name, Description, EventId, RewardTarget;
        public CharacterAmbitionCategory Category;
        public int Target;
        public V20ContentEffectKind RewardKind;
    }

    private sealed class EventSpec
    {
        public string Id, Name, Description, FirstChoice, SecondChoice;
        public LifeEventCategory Category;
        public bool Automatic;
        public V20ContentEffectKind FirstEffect, SecondEffect;
        public float FirstAmount, SecondAmount;
    }

    private sealed class CultureSpec
    {
        public string Id, Species, Name, Description, PreferredItem, ForbiddenItem, Environment, Etiquette;
    }

    private sealed class PracticeSpec
    {
        public string Id, CultureId, Name, Description, RequiredItem;
        public CulturalPracticeKind Kind;
        public V20ContentEffectKind Effect;
    }

    private static readonly BackgroundSpec[] Backgrounds =
    {
        B("background:tenant-farmer", "소작농의 자식", "척박한 밭에서 수확량보다 토양과 이웃을 먼저 살피는 법을 배웠다.", "farming", 80, "memory:first-harvest", "faction:free-settlers", 5),
        B("background:caravan-artisan", "대상단 수공업자", "이동 공방에서 자재 손실과 납기를 동시에 다루며 손기술을 익혔다.", "crafting", 80, "memory:caravan-workshop", "faction:merchant-league", 6),
        B("background:mine-bondworker", "광산 계약노동자", "무너지는 갱도와 부당한 할당량 속에서 광맥과 위험 징후를 읽었다.", "mining", 90, "memory:collapsed-shaft", "faction:iron-consortium", -5),
        B("background:inn-servant", "여관 심부름꾼", "취객과 사절 사이를 오가며 주문, 소문, 모욕을 놓치지 않았다.", "service", 75, "memory:night-shift", "faction:merchant-league", 4),
        B("background:siege-survivor", "공성전 생존자", "성벽이 무너진 날 살아남아 엄폐와 비상 보급을 습관처럼 확인한다.", "defense", 90, "memory:fallen-wall", "faction:human-crown", -8),
        B("background:camp-refugee", "피난 행렬 출신", "긴 피난길에서 식량을 나누고 뒤처진 사람을 챙기는 법을 배웠다.", "hauling", 70, "memory:last-ration", "faction:free-settlers", 8),
        B("background:deserter", "탈영병", "명령보다 살아남는 판단을 택했으며 군대의 습관과 약점을 함께 안다.", "combat", 85, "memory:desertion-night", "faction:human-legion", -12),
        B("background:exiled-heir", "추방된 후계자", "가문의 몰락 뒤에도 협상 언어와 권력의 체면을 잊지 않았다.", "diplomacy", 80, "memory:broken-seal", "faction:old-nobility", 6),
        B("background:monastery-novice", "수도원 수련자", "기록과 절제, 환자 곁을 지키는 반복 노동에 익숙하다.", "medicine", 75, "memory:monastery-vigil", "faction:truth-keepers", 5),
        B("background:guild-apprentice", "길드 도제", "규격과 검사표를 지키며 실패한 시제품의 원인을 기록했다.", "engineering", 90, "memory:first-masterwork", "faction:iron-consortium", 7),
        B("background:ritual-test-subject", "의식 실험 생존자", "비전 실험의 흔적을 지녔고 마나 이상에 예민하게 반응한다.", "arcana", 85, "memory:ritual-chamber", "faction:truth-keepers", -6),
        B("background:archive-caretaker", "봉인 기록고지기", "읽어서는 안 될 장부를 분류하며 지식의 가치와 위험을 배웠다.", "research", 90, "memory:sealed-ledger", "faction:archive-conclave", 8)
    };

    private static readonly AmbitionSpec[] Ambitions =
    {
        A("ambition:master-smith", "대장장이의 대가", "자신의 이름으로 남을 장비 계보를 완성하려 한다.", CharacterAmbitionCategory.Mastery, 300, "life-event:masterpiece-commission", V20ContentEffectKind.SkillExperience, "crafting"),
        A("ambition:master-healer", "죽음을 늦추는 손", "치료 불가능하다고 여긴 환자를 회복시키려 한다.", CharacterAmbitionCategory.Mastery, 260, "life-event:impossible-patient", V20ContentEffectKind.SkillExperience, "medicine"),
        A("ambition:field-scholar", "현장 학자의 증명", "원정에서 발견한 사실로 기존 학설을 뒤집으려 한다.", CharacterAmbitionCategory.Mastery, 280, "life-event:disputed-thesis", V20ContentEffectKind.SkillExperience, "research"),
        A("ambition:raise-family", "안전한 가족", "가족이 굶거나 피난하지 않아도 되는 집을 만들려 한다.", CharacterAmbitionCategory.Family, 220, "life-event:family-room", V20ContentEffectKind.Relationship, "household"),
        A("ambition:restore-lineage", "끊긴 계보 복원", "잃어버린 가문의 기록과 유산을 다음 세대에 잇고자 한다.", CharacterAmbitionCategory.Family, 260, "life-event:lineage-relic", V20ContentEffectKind.WorldFlag, "flag:lineage-restored"),
        A("ambition:worthy-guardian", "믿을 만한 보호자", "보호받지 못한 아이가 성년이 될 때까지 곁을 지키려 한다.", CharacterAmbitionCategory.Family, 240, "life-event:guardian-oath", V20ContentEffectKind.Relationship, "guardian"),
        A("ambition:become-steward", "관리인의 자리", "던전의 혼란을 정리하는 공식 관리인이 되려 한다.", CharacterAmbitionCategory.Status, 250, "life-event:position-rivalry", V20ContentEffectKind.WorldFlag, "flag:earned-stewardship"),
        A("ambition:guard-captain", "경비대의 신뢰", "승리가 아니라 모두를 귀환시키는 지휘관으로 인정받으려 한다.", CharacterAmbitionCategory.Status, 280, "life-event:captains-test", V20ContentEffectKind.Relationship, "guards"),
        A("ambition:cultural-voice", "문화의 대변자", "자신의 관습이 조롱받지 않고 공동체의 규칙에 반영되길 바란다.", CharacterAmbitionCategory.Status, 230, "life-event:cultural-petition", V20ContentEffectKind.Mood, "cultural-pride"),
        A("ambition:build-clinic", "모두를 위한 진료소", "신분과 종족에 관계없이 치료받는 시설을 세우려 한다.", CharacterAmbitionCategory.Community, 280, "life-event:clinic-shortage", V20ContentEffectKind.WorldFlag, "flag:public-clinic"),
        A("ambition:end-hunger", "빈 식탁 없는 겨울", "한겨울에도 모든 가구가 충분히 먹는 생산망을 만들려 한다.", CharacterAmbitionCategory.Community, 260, "life-event:winter-pantry", V20ContentEffectKind.WorldFlag, "flag:winter-fed"),
        A("ambition:mentor-generation", "다음 세대의 스승", "세 명의 도제를 숙련자로 성장시키고 자신의 실패까지 전하려 한다.", CharacterAmbitionCategory.Community, 300, "life-event:apprentice-mistake", V20ContentEffectKind.SkillExperience, "mentoring"),
        A("ambition:faction-peacemaker", "두 깃발의 중재자", "적대 세력 사이에 지속 가능한 계약을 성립시키려 한다.", CharacterAmbitionCategory.Faction, 320, "life-event:envoy-appeal", V20ContentEffectKind.FactionRapport, "faction:negotiated"),
        A("ambition:faction-champion", "동맹의 방패", "선택한 세력의 위기를 막아 신뢰받는 투사가 되려 한다.", CharacterAmbitionCategory.Faction, 300, "life-event:allied-distress", V20ContentEffectKind.FactionObligation, "faction:protected"),
        A("ambition:break-debt", "물려받은 빚 청산", "가구를 옭아맨 오래된 세력 의무를 끝내려 한다.", CharacterAmbitionCategory.Faction, 260, "life-event:inherited-debt", V20ContentEffectKind.FactionGrievance, "faction:creditor"),
        A("ambition:avenge-fallen", "쓰러진 이의 이름", "가까운 이를 죽인 지휘관을 찾아 생포하거나 쓰러뜨리려 한다.", CharacterAmbitionCategory.VengeanceOrDiscovery, 300, "life-event:killer-sighted", V20ContentEffectKind.Trauma, "vengeance"),
        A("ambition:find-origin", "기원의 흔적", "자신의 출생과 관련된 기록·실험·유적의 진실을 찾으려 한다.", CharacterAmbitionCategory.VengeanceOrDiscovery, 300, "life-event:origin-clue", V20ContentEffectKind.WorldFlag, "flag:origin-known"),
        A("ambition:map-depths", "심층의 끝", "아직 누구도 돌아오지 못한 심층 경로를 기록하려 한다.", CharacterAmbitionCategory.VengeanceOrDiscovery, 340, "life-event:uncharted-passage", V20ContentEffectKind.WorldFlag, "flag:depths-mapped")
    };

    private static readonly EventSpec[] Events =
    {
        E("life-event:first-forbidden-door", "잠긴 문 너머", "아이가 출입 금지 구역에서 들려오는 소리를 따라가려 한다.", LifeEventCategory.Childhood, "함께 안전하게 확인한다", "금지 이유를 설명하고 돌려보낸다", V20ContentEffectKind.Relationship, 4, V20ContentEffectKind.Mood, -2),
        E("life-event:foundling-question", "나는 어디서 왔어?", "입양된 아이가 자신의 친부모 기록을 보여 달라고 요청한다.", LifeEventCategory.Childhood, "기록을 함께 읽는다", "성년까지 봉인한다", V20ContentEffectKind.Relationship, 6, V20ContentEffectKind.Trauma, 3),
        E("life-event:dangerous-friendship", "위험 구역의 친구", "두 아이가 감독 없이 산업 구역을 지름길로 쓰다 적발됐다.", LifeEventCategory.Childhood, "안전 교육에 함께 참여시킨다", "통행을 엄격히 금지한다", V20ContentEffectKind.SkillExperience, 15, V20ContentEffectKind.Mood, -3),
        E("life-event:childhood-bully", "놀림의 대가", "문화가 다른 아이를 향한 반복적인 놀림이 다툼으로 번졌다.", LifeEventCategory.Childhood, "공개 화해 의식을 연다", "가해자를 별도 교육한다", V20ContentEffectKind.Relationship, 5, V20ContentEffectKind.WorkDelayDays, 1),
        E("life-event:apprentice-mistake", "도제의 큰 실수", "도제가 귀한 중간재 한 묶음을 망치고 보고를 망설인다.", LifeEventCategory.Apprenticeship, "손실을 감수하고 원인을 가르친다", "재료 회수 작업을 맡긴다", V20ContentEffectKind.SkillExperience, 25, V20ContentEffectKind.Mood, -4),
        E("life-event:stolen-design", "닮은 설계도", "경쟁 작업반이 도제의 시제품 설계를 자신의 성과로 제출했다.", LifeEventCategory.Apprenticeship, "공식 심사를 청구한다", "공동 설계로 타협한다", V20ContentEffectKind.WorldFlag, 1, V20ContentEffectKind.Relationship, 4),
        E("life-event:mentor-favor", "스승의 편애", "한 도제에게만 좋은 작업이 몰린다는 불만이 터졌다.", LifeEventCategory.Apprenticeship, "작업 배정을 공개한다", "스승의 재량을 지지한다", V20ContentEffectKind.Relationship, 5, V20ContentEffectKind.Mood, -3),
        E("life-event:masterpiece-commission", "첫 대작 의뢰", "대가 시험을 위한 장비 제작 의뢰가 들어왔다.", LifeEventCategory.Apprenticeship, "희귀 재료를 배정한다", "일반 재료로 실력을 증명한다", V20ContentEffectKind.AmbitionProgress, 50, V20ContentEffectKind.SkillExperience, 35),
        E("life-event:family-room", "가족이 함께 살 방", "늘어난 가족이 다른 가구와 뒤섞인 침실을 벗어나길 원한다.", LifeEventCategory.PartnershipFamily, "가족실을 우선 배정한다", "공동 보육실을 확장한다", V20ContentEffectKind.Relationship, 7, V20ContentEffectKind.Mood, 3),
        E("life-event:guardian-oath", "보호자의 맹세", "고아가 된 미성년자의 보호자 후보 둘이 서로 다른 미래를 약속한다.", LifeEventCategory.PartnershipFamily, "가까운 관계를 우선한다", "안정된 가구를 우선한다", V20ContentEffectKind.Relationship, 6, V20ContentEffectKind.Health, 4),
        E("life-event:inherited-debt", "가구에 남은 빚", "사망자의 세력 의무가 남은 가족에게 청구됐다.", LifeEventCategory.PartnershipFamily, "물자로 상환한다", "계약의 정당성을 다툰다", V20ContentEffectKind.FactionObligation, -1, V20ContentEffectKind.FactionGrievance, 8),
        E("life-event:cultural-petition", "식탁의 금기", "가구가 공동 식당에서 문화적 금기를 지켜 달라는 청원을 냈다.", LifeEventCategory.PartnershipFamily, "별도 조리대를 배정한다", "공통 대체식을 합의한다", V20ContentEffectKind.Mood, 5, V20ContentEffectKind.Relationship, 5),
        E("life-event:position-rivalry", "한 자리, 두 후보", "같은 직위를 원하는 두 숙련자가 공개 평가를 요구한다.", LifeEventCategory.Career, "실기 평가로 정한다", "공동 임무 성과로 정한다", V20ContentEffectKind.SkillExperience, 20, V20ContentEffectKind.Relationship, 4),
        E("life-event:captains-test", "대장의 시험", "퇴로가 불안한 방어전에서 경비대장 후보가 위험한 역습을 제안한다.", LifeEventCategory.Career, "제한된 역습을 허가한다", "민간인 철수를 우선한다", V20ContentEffectKind.AmbitionProgress, 45, V20ContentEffectKind.Relationship, 6),
        E("life-event:disputed-thesis", "논쟁적인 논문", "새 발견이 수석 연구원의 기존 이론을 정면으로 반박한다.", LifeEventCategory.Career, "증거를 공개 검증한다", "추가 실험까지 보류한다", V20ContentEffectKind.SkillExperience, 30, V20ContentEffectKind.WorkDelayDays, 2),
        E("life-event:clinic-shortage", "누구를 먼저 치료할 것인가", "의약품이 부족한 날 직원과 외부 손님이 동시에 중증으로 쓰러졌다.", LifeEventCategory.Career, "위급도 순으로 배분한다", "공동체 구성원을 우선한다", V20ContentEffectKind.Health, 8, V20ContentEffectKind.FactionRapport, -6),
        E("life-event:retirement-request", "도구를 내려놓는 날", "노년의 대가가 현장 은퇴와 후계자 지명을 요청한다.", LifeEventCategory.ElderRetirement, "은퇴와 멘토직을 보장한다", "한 계절 더 현장을 부탁한다", V20ContentEffectKind.Mood, 6, V20ContentEffectKind.WorkDelayDays, -2),
        E("life-event:last-lesson", "마지막 수업", "쇠약해진 스승이 위험을 감수하고 마지막 실습을 열려 한다.", LifeEventCategory.ElderRetirement, "안전한 시연으로 바꾼다", "원래 실습을 지원한다", V20ContentEffectKind.SkillExperience, 25, V20ContentEffectKind.Health, -5),
        E("life-event:lineage-relic", "유산의 행방", "가문의 유물을 계승할지 공동 랜드마크에 봉헌할지 결정해야 한다.", LifeEventCategory.DeathLegacy, "후계자에게 계승한다", "공동체에 봉헌한다", V20ContentEffectKind.Relationship, 7, V20ContentEffectKind.WorldFlag, 1),
        E("life-event:killer-sighted", "원수의 깃발", "원정 정찰대가 오래전 죽음의 책임자를 발견했다.", LifeEventCategory.DeathLegacy, "생포 작전을 준비한다", "복수를 접고 기록을 공개한다", V20ContentEffectKind.AmbitionProgress, 60, V20ContentEffectKind.Trauma, -8),
        Auto("life-event:first-lost-tooth", "첫 이갈이", "아이가 빠진 이를 문화 관습에 따라 간직한다.", LifeEventCategory.Childhood, V20ContentEffectKind.Mood, 2),
        Auto("life-event:shared-lullaby", "서로 다른 자장가", "보육실에서 두 문화의 노래가 하나의 곡으로 섞인다.", LifeEventCategory.Childhood, V20ContentEffectKind.Relationship, 2),
        Auto("life-event:first-safe-task", "첫 안전 작업", "청소년이 감독 아래 첫 작업을 사고 없이 마쳤다.", LifeEventCategory.Apprenticeship, V20ContentEffectKind.SkillExperience, 10),
        Auto("life-event:tool-inheritance", "물려받은 도구", "도제가 은퇴자의 낡은 도구와 사용 기록을 넘겨받았다.", LifeEventCategory.Apprenticeship, V20ContentEffectKind.AmbitionProgress, 10),
        Auto("life-event:household-meal", "늦은 가족 식사", "엇갈리던 가구 구성원들이 오랜만에 같은 식탁에 앉았다.", LifeEventCategory.PartnershipFamily, V20ContentEffectKind.Relationship, 2),
        Auto("life-event:newborn-welcome", "새 생명의 환영", "가구가 새 구성원을 공동체에 소개했다.", LifeEventCategory.PartnershipFamily, V20ContentEffectKind.Mood, 3),
        Auto("life-event:quiet-promotion", "조용한 승급", "꾸준히 일한 직원이 다음 숙련 등급에 도달했다.", LifeEventCategory.Career, V20ContentEffectKind.Mood, 2),
        Auto("life-event:shift-saved", "교대를 구한 손", "작업자가 동료 대신 위험한 교대의 마무리를 맡았다.", LifeEventCategory.Career, V20ContentEffectKind.Relationship, 2),
        Auto("life-event:retiree-story", "휴게실의 옛 이야기", "은퇴자가 실패담을 들려주어 젊은 직원이 같은 실수를 피했다.", LifeEventCategory.ElderRetirement, V20ContentEffectKind.SkillExperience, 8),
        Auto("life-event:elder-birthday", "노년의 생일상", "여러 세대가 한 자리에 모여 노년 구성원의 생일을 축하했다.", LifeEventCategory.ElderRetirement, V20ContentEffectKind.Mood, 3),
        Auto("life-event:grave-visit", "묘비 앞의 약속", "가까운 이가 묘비를 찾아 지난 선택을 되새겼다.", LifeEventCategory.DeathLegacy, V20ContentEffectKind.Trauma, -2),
        Auto("life-event:story-compressed", "이름으로 남은 생애", "오래된 개인 기록이 가계의 핵심 이야기로 정리됐다.", LifeEventCategory.DeathLegacy, V20ContentEffectKind.WorldFlag, 1)
    };

    private static readonly CultureSpec[] Cultures =
    {
        C("culture:adventurer-frontier", "Adventurer", "개척자 연맹 문화", "서로 다른 고향의 규칙을 원정대식 실용주의로 엮는다.", "food:preserved-ration", "food:raw-monster-meat", "lit communal rooms", "전리품은 귀환자 전원이 확인한 뒤 나눈다."),
        C("culture:beastkin-pack", "Beastkin", "무리의 화로 문화", "식사와 휴식을 무리 단위로 공유하고 홀로 남는 이를 먼저 챙긴다.", "resource:meat", "medicine:strong-perfume", "warm shared quarters", "상대의 냄새표식을 허락 없이 지우지 않는다."),
        C("culture:demon-contract", "Demon", "재의 계약 문화", "말보다 기록된 약속을 중시하고 결연과 장례에 계약 소각을 쓴다.", "food:spiced-stew", "item:unsealed-oath", "warm dry rooms", "세 번 확인한 약속은 공개적으로 번복하지 않는다."),
        C("culture:golem-core", "Golem", "핵 공명 문화", "침묵의 정비와 기억 기록을 휴식으로 여기며 핵의 이력을 존중한다.", "component:maintenance-parts", "resource:corrosive-slurry", "dry rune-powered alcoves", "다른 골렘의 핵각인을 허락 없이 읽지 않는다."),
        C("culture:harpy-aerie", "Harpy", "높은 둥지 문화", "높은 공간과 바람길을 선호하며 소식과 노래를 공동 자산으로 여긴다.", "food:dried-fruit", "food:heavy-oil-stew", "high airy rooms", "노래 중간에 말을 끊는 것은 공개적인 도전이다."),
        C("culture:kobold-toolclan", "Kobold", "도구씨족 문화", "잘 관리된 도구를 가계의 증거로 여기며 작은 성취도 씨족 앞에서 기록한다.", "food:mushroom-stew", "item:broken-tool", "compact warm workshops", "도구를 빌릴 때는 돌려줄 상태를 먼저 약속한다."),
        C("culture:myconid-grove", "Myconid", "포자정원 문화", "대화와 기억을 향과 포자 리듬으로 나누며 습한 공동 정원을 지킨다.", "food:cultured-mushroom", "item:fungicide", "humid dim gardens", "타인의 포자권에 불꽃을 들이지 않는다."),
        C("culture:orc-vigil", "Orc", "무기 철야 문화", "행동으로 신뢰를 증명하며 잔치와 철야에서 공동체의 빚을 기억한다.", "food:salted-meat-stew", "food:tiny-portion", "robust communal halls", "무기를 내려놓고 한 약속은 결투로 뒤집지 않는다."),
        C("culture:slime-confluence", "Slime", "합류수 문화", "몸의 일부와 기억을 나누는 것을 친밀함으로 여기되 핵의 경계는 엄격히 지킨다.", "resource:clean-water", "resource:salt", "clean humid pools", "상대 핵에 닿기 전 반드시 색 변화로 동의를 구한다."),
        C("culture:vampire-nightcourt", "Vampire", "밤궁정 문화", "절제된 환대와 혈액 제공의 동의를 체면보다 중요한 규칙으로 삼는다.", "food:blood-reserve", "food:garlic-tonic", "dark private chambers", "혈액과 개인 서사는 명시적 허락 없이 거래하지 않는다.")
    };

    private static readonly PracticeSpec[] Practices =
    {
        P("practice:adventurer-return-table", "culture:adventurer-frontier", "귀환자의 식탁", "원정 귀환자가 같은 배급식을 나누며 미귀환자를 보고한다.", CulturalPracticeKind.DailyRoutine, "food:preserved-ration", V20ContentEffectKind.Relationship),
        P("practice:adventurer-name-token", "culture:adventurer-frontier", "이름패 걸기", "성년자는 첫 독립 임무 뒤 공동 지도에 이름패를 건다.", CulturalPracticeKind.ComingOfAge, "material:paper", V20ContentEffectKind.Mood),
        P("practice:beastkin-pack-meal", "culture:beastkin-pack", "무리 한솥", "가구와 가까운 동료가 큰 고기솥을 함께 비운다.", CulturalPracticeKind.Food, "resource:meat", V20ContentEffectKind.Relationship),
        P("practice:beastkin-scent-vigil", "culture:beastkin-pack", "향취 철야", "사망자의 익숙한 향을 천에 남겨 무리가 차례로 작별한다.", CulturalPracticeKind.Funeral, "textile:cloth", V20ContentEffectKind.Trauma),
        P("practice:demon-oath-embers", "culture:demon-contract", "맹세의 잿불", "중요한 약속을 종이에 쓰고 한쪽 사본만 태운다.", CulturalPracticeKind.Partnership, "material:paper", V20ContentEffectKind.WorldFlag),
        P("practice:demon-third-bell", "culture:demon-contract", "세 번째 종", "교대 종료를 세 번 확인한 뒤에야 작업장을 떠난다.", CulturalPracticeKind.WorkRest, "item:bell", V20ContentEffectKind.Mood),
        P("practice:golem-core-polish", "culture:golem-core", "핵 광택일", "정기 정비 때 서로의 외장만 손질하고 핵은 본인이 점검한다.", CulturalPracticeKind.DailyRoutine, "component:maintenance-parts", V20ContentEffectKind.Health),
        P("practice:golem-memory-plaque", "culture:golem-core", "기억판 안치", "정지된 골렘의 핵 기록을 금속판에 요약해 보관한다.", CulturalPracticeKind.Funeral, "material:iron-ingot", V20ContentEffectKind.Trauma),
        P("practice:harpy-dawn-chorus", "culture:harpy-aerie", "새벽 합창", "밤 교대가 끝나는 시간에 높은 통로에서 하루의 소식을 노래한다.", CulturalPracticeKind.DailyRoutine, "food:dried-fruit", V20ContentEffectKind.Mood),
        P("practice:harpy-first-flight", "culture:harpy-aerie", "첫 비행의 깃", "성년식에서 안전줄을 매고 가장 높은 내부 비행로를 완주한다.", CulturalPracticeKind.ComingOfAge, "material:rope", V20ContentEffectKind.SkillExperience),
        P("practice:kobold-tool-naming", "culture:kobold-toolclan", "첫 도구 이름짓기", "도제는 직접 수리한 첫 도구에 이름과 날짜를 새긴다.", CulturalPracticeKind.ComingOfAge, "material:iron-ingot", V20ContentEffectKind.SkillExperience),
        P("practice:kobold-bench-feast", "culture:kobold-toolclan", "작업대 잔치", "큰 공사가 끝나면 작업대를 닦고 작은 버섯 요리를 나눈다.", CulturalPracticeKind.WorkRest, "food:mushroom-stew", V20ContentEffectKind.Mood),
        P("practice:myconid-shared-mist", "culture:myconid-grove", "공유 안개", "휴식 시간에 깨끗한 물을 분무해 포자 리듬을 맞춘다.", CulturalPracticeKind.DailyRoutine, "resource:clean-water", V20ContentEffectKind.Relationship),
        P("practice:myconid-spore-return", "culture:myconid-grove", "포자 귀환", "사망자의 배양 기록을 정원 토양에 되돌려 공동 기억으로 남긴다.", CulturalPracticeKind.Funeral, "resource:compost", V20ContentEffectKind.Trauma),
        P("practice:orc-weapon-vigil", "culture:orc-vigil", "무기 철야", "동료의 무기를 밤새 손질하며 그의 용기와 실수를 함께 말한다.", CulturalPracticeKind.Funeral, "material:charcoal", V20ContentEffectKind.Trauma),
        P("practice:orc-shared-cauldron", "culture:orc-vigil", "큰솥의 몫", "가장 약한 구성원이 먼저 고기 스튜를 받은 뒤 나머지가 먹는다.", CulturalPracticeKind.Food, "food:salted-meat-stew", V20ContentEffectKind.Relationship),
        P("practice:slime-clear-water", "culture:slime-confluence", "맑은물 합류", "서로 다른 가구의 슬라임이 깨끗한 수조 가장자리에서 기억을 나눈다.", CulturalPracticeKind.Social, "resource:clean-water", V20ContentEffectKind.Relationship),
        P("practice:slime-core-ring", "culture:slime-confluence", "핵고리 성년식", "성년이 된 슬라임의 핵 둘레에 손상 없는 보호 고리를 맞춘다.", CulturalPracticeKind.ComingOfAge, "component:mana-shield-plate", V20ContentEffectKind.Health),
        P("practice:vampire-consent-cup", "culture:vampire-nightcourt", "동의의 잔", "혈액을 나누기 전 제공자와 수령자가 양과 목적을 함께 선언한다.", CulturalPracticeKind.Food, "food:blood-reserve", V20ContentEffectKind.Relationship),
        P("practice:vampire-incense-memory", "culture:vampire-nightcourt", "혈향 촛불", "사망자의 기억을 상징하는 향초를 3일간 개인실 앞에 밝힌다.", CulturalPracticeKind.Funeral, "item:candle", V20ContentEffectKind.Trauma)
    };

    [MenuItem("DungeonStory/V20/Build Narrative Content (92)")]
    public static void Build()
    {
        RequireManifestCounts();
        EnsureFolders();
        GameDomainContentCatalogSO catalog = AssetDatabase.LoadAssetAtPath<GameDomainContentCatalogSO>(CatalogPath)
            ?? throw new InvalidOperationException($"Required domain catalog is missing at '{CatalogPath}'.");

        List<ScriptableObject> authored = new();
        authored.AddRange(Backgrounds.Select(CreateBackground));
        authored.AddRange(Ambitions.Select(CreateAmbition));
        authored.AddRange(Events.Select(CreateEvent));
        authored.AddRange(Cultures.Select(CreateCulture));
        authored.AddRange(Practices.Select(CreatePractice));

        List<string> errors = authored.OfType<V20AuthoredContentSO>()
            .SelectMany(definition => definition.ValidateDefinition())
            .ToList();
        V20AuthoredDefinitionValidation.RequireUniqueNonEmptyIds(
            authored.OfType<V20AuthoredContentSO>(), value => value.StableId,
            "V20 narrative", errors);
        foreach (CulturalPracticeDefinitionSO practice in authored.OfType<CulturalPracticeDefinitionSO>())
            if (!authored.OfType<SpeciesCultureDefinitionSO>().Any(culture => culture.StableId == practice.cultureId))
                errors.Add($"Practice '{practice.StableId}' references missing culture '{practice.cultureId}'.");
        if (errors.Count > 0) throw new InvalidOperationException(string.Join(" | ", errors));

        Type[] ownedTypes =
        {
            typeof(CharacterBackgroundDefinitionSO), typeof(CharacterAmbitionDefinitionSO),
            typeof(LifeEventDefinitionSO), typeof(SpeciesCultureDefinitionSO),
            typeof(CulturalPracticeDefinitionSO)
        };
        catalog.SetDefinitions(catalog.Definitions
            .Where(value => value != null && !ownedTypes.Contains(value.GetType()))
            .Concat(authored));
        EditorUtility.SetDirty(catalog);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("V20_NARRATIVE_CONTENT=PASS; backgrounds=12; ambitions=18; lifeEvents=32; cultures=10; practices=20; total=92");
    }

    private static CharacterBackgroundDefinitionSO CreateBackground(BackgroundSpec spec)
    {
        CharacterBackgroundDefinitionSO value = Asset<CharacterBackgroundDefinitionSO>("Backgrounds", spec.Id);
        Meta(value, spec.Id, spec.Name, spec.Description);
        value.startingSkills = new List<V20SkillBonus> { new() { skillId = spec.Skill, experience = spec.Experience } };
        value.startingEffects = new List<V20ContentEffect> { Effect(V20ContentEffectKind.Mood, "background-confidence", 2, 10) };
        value.factionReactions = new List<V20WeightedId> { new() { id = spec.Faction, weight = Mathf.Clamp(spec.Reaction / 5f + 1f, 0.1f, 10f) } };
        value.initialMemoryCode = spec.Memory;
        Dirty(value); return value;
    }

    private static CharacterAmbitionDefinitionSO CreateAmbition(AmbitionSpec spec)
    {
        CharacterAmbitionDefinitionSO value = Asset<CharacterAmbitionDefinitionSO>("Ambitions", spec.Id);
        Meta(value, spec.Id, spec.Name, spec.Description);
        value.category = spec.Category; value.targetProgress = spec.Target;
        value.activationRequirements = new V20ContentRequirementSet
        {
            characters = new List<V20CharacterRequirement> { new() { minimumLifeStage = CharacterLifeStage.Adult, maximumLifeStage = CharacterLifeStage.Elder } }
        };
        value.failureConditions = new V20ContentRequirementSet();
        value.completionRewards = new List<V20ContentEffect> { Effect(spec.RewardKind, spec.RewardTarget, spec.RewardKind == V20ContentEffectKind.SkillExperience ? 100 : 1) };
        value.relatedEventWeights = new List<V20WeightedId> { new() { id = spec.EventId, weight = 2f } };
        value.cooperationAmbitionIds = new List<string>(); value.conflictAmbitionIds = new List<string>();
        Dirty(value); return value;
    }

    private static LifeEventDefinitionSO CreateEvent(EventSpec spec)
    {
        LifeEventDefinitionSO value = Asset<LifeEventDefinitionSO>("LifeEvents", spec.Id);
        Meta(value, spec.Id, spec.Name, spec.Description);
        value.category = spec.Category; value.automatic = spec.Automatic; value.emergency = false;
        value.responseDeadlineDays = spec.Automatic ? 1 : 3; value.cooldownDays = spec.Automatic ? 45 : 90;
        value.frequencyRule = LifeEventFrequencyRule.OncePerCharacter;
        value.triggerRequirements = new V20ContentRequirementSet();
        value.choices = spec.Automatic ? new List<V20ChoiceDefinition>() : new List<V20ChoiceDefinition>
        {
            Choice("first", spec.FirstChoice, Effect(spec.FirstEffect, EffectTarget(spec.FirstEffect, spec.Id), spec.FirstAmount)),
            Choice("second", spec.SecondChoice, Effect(spec.SecondEffect, EffectTarget(spec.SecondEffect, spec.Id), spec.SecondAmount))
        };
        value.automaticEffects = spec.Automatic
            ? new List<V20ContentEffect> { Effect(spec.FirstEffect, EffectTarget(spec.FirstEffect, spec.Id), spec.FirstAmount) }
            : new List<V20ContentEffect>();
        Dirty(value); return value;
    }

    private static SpeciesCultureDefinitionSO CreateCulture(CultureSpec spec)
    {
        SpeciesCultureDefinitionSO value = Asset<SpeciesCultureDefinitionSO>("Cultures", spec.Id);
        Meta(value, spec.Id, spec.Name, spec.Description);
        value.defaultSpeciesId = spec.Species;
        value.preferredItemIds = new List<string> { spec.PreferredItem };
        value.forbiddenItemIds = new List<string> { spec.ForbiddenItem };
        value.preferredFacilityIds = new List<string>();
        value.environmentalPreferences = new List<string> { spec.Environment };
        value.etiquetteRules = new List<string> { spec.Etiquette };
        value.ceremonyIds = Practices.Where(item => item.CultureId == spec.Id).Select(item => item.Id).ToList();
        value.otherCultureAttitudes = Cultures.Where(item => item.Id != spec.Id)
            .Select(item => new V20WeightedId { id = item.Id, weight = 1f }).ToList();
        value.assimilationDays = 120;
        Dirty(value); return value;
    }

    private static CulturalPracticeDefinitionSO CreatePractice(PracticeSpec spec)
    {
        CulturalPracticeDefinitionSO value = Asset<CulturalPracticeDefinitionSO>("Practices", spec.Id);
        Meta(value, spec.Id, spec.Name, spec.Description);
        value.cultureId = spec.CultureId; value.kind = spec.Kind;
        value.requirements = new V20ContentRequirementSet
        {
            items = new List<V20ItemAmountRequirement> { new() { itemDefinitionId = spec.RequiredItem, amount = 1, consume = true } }
        };
        value.successEffects = new List<V20ContentEffect> { Effect(spec.Effect, spec.Id, spec.Effect == V20ContentEffectKind.Trauma ? -4 : 3, 5) };
        value.neglectedEffects = new List<V20ContentEffect> { Effect(V20ContentEffectKind.Mood, spec.Id, -2, 3) };
        Dirty(value); return value;
    }

    private static void RequireManifestCounts()
    {
        if (Backgrounds.Length != 12 || Ambitions.Length != 18 || Events.Length != 32
            || Events.Count(value => !value.Automatic) != 20 || Events.Count(value => value.Automatic) != 12
            || Cultures.Length != 10 || Practices.Length != 20)
            throw new InvalidOperationException("V20 narrative manifest count contract is broken.");
    }

    private static void EnsureFolders()
    {
        Ensure("Assets/Resources/SO", "V20"); Ensure("Assets/Resources/SO/V20", "Narrative");
        foreach (string child in new[] { "Backgrounds", "Ambitions", "LifeEvents", "Cultures", "Practices" }) Ensure(Root, child);
    }

    private static void Ensure(string parent, string child)
    {
        string path = $"{parent}/{child}";
        if (!AssetDatabase.IsValidFolder(path)) AssetDatabase.CreateFolder(parent, child);
    }

    private static T Asset<T>(string folder, string id) where T : ScriptableObject
    {
        string path = $"{Root}/{folder}/{Safe(id)}.asset";
        UnityEngine.Object existing = AssetDatabase.LoadMainAssetAtPath(path);
        if (existing != null && existing is not T)
            throw new InvalidOperationException($"'{path}' contains '{existing.GetType().Name}', expected '{typeof(T).Name}'.");
        if (existing is T typed) return typed;
        T created = ScriptableObject.CreateInstance<T>();
        AssetDatabase.CreateAsset(created, path);
        return created;
    }

    private static string Safe(string value) => string.Concat(value.Select(character => Path.GetInvalidFileNameChars().Contains(character) || character == ':' ? '_' : character));
    private static void Meta(V20AuthoredContentSO value, string id, string name, string description) => value.ConfigureMetadata(id, name, description, 1, RevisionNote);
    private static void Dirty(UnityEngine.Object value) => EditorUtility.SetDirty(value);
    private static V20ContentEffect Effect(V20ContentEffectKind kind, string target, float amount, int days = 0) => new() { kind = kind, targetId = target, amount = amount, durationDays = days };
    private static string EffectTarget(V20ContentEffectKind kind, string defaultTarget) =>
        kind is V20ContentEffectKind.FactionRapport
            or V20ContentEffectKind.FactionGrievance
            or V20ContentEffectKind.FactionObligation
            ? "affected-faction"
            : defaultTarget;
    private static V20ChoiceDefinition Choice(string id, string title, V20ContentEffect effect) => new() { choiceId = id, title = title, outcomeText = title, requirements = new V20ContentRequirementSet(), effects = new List<V20ContentEffect> { effect } };
    private static BackgroundSpec B(string id, string name, string description, string skill, int experience, string memory, string faction, int reaction) => new() { Id = id, Name = name, Description = description, Skill = skill, Experience = experience, Memory = memory, Faction = faction, Reaction = reaction };
    private static AmbitionSpec A(string id, string name, string description, CharacterAmbitionCategory category, int target, string eventId, V20ContentEffectKind reward, string rewardTarget) => new() { Id = id, Name = name, Description = description, Category = category, Target = target, EventId = eventId, RewardKind = reward, RewardTarget = rewardTarget };
    private static EventSpec E(string id, string name, string description, LifeEventCategory category, string firstChoice, string secondChoice, V20ContentEffectKind firstEffect, float firstAmount, V20ContentEffectKind secondEffect, float secondAmount) => new() { Id = id, Name = name, Description = description, Category = category, FirstChoice = firstChoice, SecondChoice = secondChoice, FirstEffect = firstEffect, FirstAmount = firstAmount, SecondEffect = secondEffect, SecondAmount = secondAmount };
    private static EventSpec Auto(string id, string name, string description, LifeEventCategory category, V20ContentEffectKind effect, float amount) => new() { Id = id, Name = name, Description = description, Category = category, Automatic = true, FirstEffect = effect, FirstAmount = amount };
    private static CultureSpec C(string id, string species, string name, string description, string preferred, string forbidden, string environment, string etiquette) => new() { Id = id, Species = species, Name = name, Description = description, PreferredItem = preferred, ForbiddenItem = forbidden, Environment = environment, Etiquette = etiquette };
    private static PracticeSpec P(string id, string culture, string name, string description, CulturalPracticeKind kind, string item, V20ContentEffectKind effect) => new() { Id = id, CultureId = culture, Name = name, Description = description, Kind = kind, RequiredItem = item, Effect = effect };
}
#endif
