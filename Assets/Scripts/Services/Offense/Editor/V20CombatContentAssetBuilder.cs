#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class V20CombatContentAssetBuilder
{
    private const string CatalogPath="Assets/Resources/SO/Content/GameDomainContentCatalog.asset";
    private const string Root="Assets/Resources/SO/V20/Combat";
    private sealed class AbilitySpec{public string Id,Name,Description,Target;public int Cooldown,Duration;public EnemyAbilityEffectKind Kind;public float Magnitude;public OffenseBattleTargetRule TargetRule;}
    private sealed class EnemySpec{public string Id,Name,Description,Faction,Species,Formation,Weapon,Armor,Ammo;public EnemyCombatRole Role;public string[] Abilities,Counters,Rewards;public float Health,Attack,Toughness,Dexterity,Move;}
    private sealed class ModifierSpec{public string Id,Name,Description,Counter;public BattlefieldModifierKind Kind;public float Move,Accuracy,Damage;}
    private sealed class EncounterSpec{public int Index,RoundLimit;public string Name,ObjectiveTargetId,ModifierId;public bool Elite,Boss;public OffenseEncounterObjective Objective;public string[] EnemyIds,CounterTags,RewardItemIds;}

    private static readonly AbilitySpec[] Abilities=
    {
        A("enemy-ability:charge","돌진","거리를 좁히며 첫 타격의 충격을 높인다.",2,EnemyAbilityEffectKind.Damage,1.25f,"front",OffenseBattleTargetRule.Enemy),
        A("enemy-ability:shield-wall","방패벽","인접 전열을 보호하고 밀려남을 막는다.",3,EnemyAbilityEffectKind.Guard,1.35f,"ally-front",OffenseBattleTargetRule.Ally),
        A("enemy-ability:suppressive-volley","제압 사격","넓은 구역을 사격해 이동과 행동을 늦춘다.",3,EnemyAbilityEffectKind.Suppression,1f,"backline",OffenseBattleTargetRule.Enemy,2),
        A("enemy-ability:field-dressing","야전 처치","가장 크게 다친 아군의 출혈과 체력을 회복한다.",3,EnemyAbilityEffectKind.Heal,18f,"injured-ally",OffenseBattleTargetRule.Ally),
        A("enemy-ability:smoke-screen","연막 전개","시야를 차단해 원거리 명중을 낮추고 퇴로를 만든다.",4,EnemyAbilityEffectKind.Smoke,1f,"battlefield",OffenseBattleTargetRule.Self,2),
        A("enemy-ability:powder-shot","화약 일제","긴 준비 뒤 방어구를 뚫는 강한 사격을 가한다.",3,EnemyAbilityEffectKind.Damage,1.55f,"armored",OffenseBattleTargetRule.Enemy),
        A("enemy-ability:arcane-null","마법 차단","대상의 비전 강화 효과를 제거하고 취약하게 만든다.",4,EnemyAbilityEffectKind.Dispel,1f,"arcane",OffenseBattleTargetRule.Enemy),
        A("enemy-ability:summon-minion","지원체 소환","예비 병력이나 소형 구성체를 전장에 투입한다.",5,EnemyAbilityEffectKind.Summon,1f,"reserve",OffenseBattleTargetRule.Self),
        A("enemy-ability:retreat-cover","퇴각 엄호","부상한 아군의 퇴로를 지키며 추격자를 제압한다.",3,EnemyAbilityEffectKind.Guard,1.2f,"retreating-ally",OffenseBattleTargetRule.Ally),
        A("enemy-ability:hook-pull","갈고리 끌기","후열 대상을 전열로 끌어내 진형을 무너뜨린다.",3,EnemyAbilityEffectKind.Delay,2f,"backline",OffenseBattleTargetRule.Enemy),
        A("enemy-ability:armor-break","갑주 파쇄","단단한 목표의 방어를 약화시키는 강타를 가한다.",3,EnemyAbilityEffectKind.Vulnerability,.3f,"armored",OffenseBattleTargetRule.Enemy,2),
        A("enemy-ability:rune-ward","룬 보호막","아군에게 일시적인 마법·화약 피해 보호를 부여한다.",4,EnemyAbilityEffectKind.Guard,1.4f,"priority-ally",OffenseBattleTargetRule.Ally,2),
        A("enemy-ability:rally","전열 재정비","흩어진 아군을 보호 대상 주변으로 다시 모은다.",4,EnemyAbilityEffectKind.Guard,1.25f,"all-allies",OffenseBattleTargetRule.Ally,2),
        A("enemy-ability:poison-cloud","독성 구름","구역에 지속 독 피해를 남겨 엄폐지를 비우게 한다.",4,EnemyAbilityEffectKind.DamageOverTime,7f,"cluster",OffenseBattleTargetRule.Enemy,3),
        A("enemy-ability:frost-bind","서리 구속","대상의 이동과 다음 행동을 크게 늦춘다.",3,EnemyAbilityEffectKind.Delay,3f,"fast",OffenseBattleTargetRule.Enemy,2),
        A("enemy-ability:blood-drain","혈액 흡수","상처 입은 대상에게 피해를 주고 자신을 회복한다.",3,EnemyAbilityEffectKind.Damage,1.2f,"wounded",OffenseBattleTargetRule.Enemy),
        A("enemy-ability:core-repair","핵 긴급수리","구성체의 핵과 외장을 전투 중 수리한다.",4,EnemyAbilityEffectKind.Heal,24f,"construct",OffenseBattleTargetRule.Ally),
        A("enemy-ability:truth-seal","진실 봉인","강화 효과를 지우고 지속 피해 표식을 남긴다.",4,EnemyAbilityEffectKind.Dispel,1f,"highest-power",OffenseBattleTargetRule.Enemy,3)
    };

    private static readonly EnemySpec[] Enemies=
    {
        E("enemy:crown-levy","왕령 징집병","수적 압박으로 전열을 붙잡는 징집 보병.","human:crown","Human",EnemyCombatRole.Vanguard,"line","weapon:spear","armor:gambeson","",82,7,6,5,4,new[]{"enemy-ability:charge"},new[]{"counter:reach"}),
        E("enemy:crown-tower-guard","왕령 탑방패병","보호 대상 앞에서 방패벽을 유지한다.","human:crown","Human",EnemyCombatRole.Defender,"guard","weapon:mace","armor:brigandine","",112,7,11,4,3,new[]{"enemy-ability:shield-wall"},new[]{"counter:armor-break"}),
        E("enemy:crown-longbow","왕령 장궁병","가장 약한 후열을 골라 제압 사격한다.","human:crown","Human",EnemyCombatRole.Marksman,"ranged-line","weapon:longbow","armor:gambeson","ammo:arrow",72,10,4,9,4.5f,new[]{"enemy-ability:suppressive-volley"},new[]{"counter:smoke"}),
        E("enemy:crown-chaplain","왕령 군종사제","부상병을 치료하고 전열 붕괴를 늦춘다.","human:crown","Human",EnemyCombatRole.Support,"protected-center","weapon:spear","armor:mail-shirt","",86,6,7,7,4,new[]{"enemy-ability:field-dressing","enemy-ability:rally"},new[]{"counter:focus-support"}),
        E("enemy:crown-marshal","왕령 원수","보호와 재집결을 반복하는 지휘관.","human:crown","Human",EnemyCombatRole.Boss,"command-center","weapon:longsword","armor:breastplate","",150,12,12,9,4.5f,new[]{"enemy-ability:rally","enemy-ability:armor-break"},new[]{"counter:isolate-leader"}),
        E("enemy:legion-pikeman","도시연맹 장창병","접근하는 적을 긴 창으로 묶는다.","human:legion","Human",EnemyCombatRole.Vanguard,"pike-line","weapon:halberd","armor:brigandine","",92,9,7,6,4,new[]{"enemy-ability:hook-pull"},new[]{"counter:ranged"}),
        E("enemy:legion-pavise","도시연맹 파비스병","사격수를 가리는 이동 엄폐를 만든다.","human:legion","Human",EnemyCombatRole.Defender,"pavise-line","weapon:falchion","armor:scale-coat","",118,7,12,4,3,new[]{"enemy-ability:shield-wall","enemy-ability:retreat-cover"},new[]{"counter:flank"}),
        E("enemy:legion-arquebus","도시연맹 아쿼버스병","갑주 목표에게 화약 일제를 집중한다.","human:legion","Human",EnemyCombatRole.Marksman,"powder-line","weapon:arquebus","armor:gambeson","ammo:paper-cartridge",76,12,4,8,3.5f,new[]{"enemy-ability:powder-shot","enemy-ability:smoke-screen"},new[]{"counter:rush"}),
        E("enemy:legion-surgeon","도시연맹 외과의","엄폐 뒤에서 중상자를 우선 치료한다.","human:legion","Human",EnemyCombatRole.Support,"rear-clinic","weapon:dagger","armor:gambeson","",78,5,5,9,4,new[]{"enemy-ability:field-dressing","enemy-ability:smoke-screen"},new[]{"counter:interrupt-heal"}),
        E("enemy:legion-captain","도시연맹 백인대장","창벽과 화약병의 사격 주기를 조율한다.","human:legion","Human",EnemyCombatRole.Controller,"command-flank","weapon:halberd","armor:breastplate","",132,11,10,9,4.5f,new[]{"enemy-ability:rally","enemy-ability:suppressive-volley"},new[]{"counter:break-formation"}),
        E("enemy:inquisition-purifier","성화 심문 정화병","독과 비전 오염 구역을 불태우며 전진한다.","human:inquisition","Human",EnemyCombatRole.Vanguard,"purge-line","weapon:warhammer","armor:blast-coat","",98,10,8,6,4,new[]{"enemy-ability:armor-break"},new[]{"counter:kite"}),
        E("enemy:inquisition-ward-knight","성화 결계기사","마법 공격을 막는 룬 방패로 심문관을 보호한다.","human:inquisition","Human",EnemyCombatRole.Defender,"ward-circle","weapon:mace","armor:breastplate","",126,8,13,5,3.5f,new[]{"enemy-ability:rune-ward","enemy-ability:shield-wall"},new[]{"counter:physical-burst"}),
        E("enemy:inquisition-rune-sniper","성화 룬저격수","비전 사용자를 우선 표적으로 삼아 강화 효과를 지운다.","human:inquisition","Human",EnemyCombatRole.Marksman,"anti-mage-line","weapon:windlass-crossbow","armor:smoke-hood","ammo:bolt",80,11,5,10,4,new[]{"enemy-ability:arcane-null","enemy-ability:suppressive-volley"},new[]{"counter:decoy"}),
        E("enemy:inquisition-censor","성화 검열관","아군 결계를 유지하고 적의 능력을 봉쇄한다.","human:inquisition","Human",EnemyCombatRole.Controller,"ward-center","weapon:mana-lance","armor:rune-ward-mail","",92,8,8,8,4,new[]{"enemy-ability:arcane-null","enemy-ability:rune-ward"},new[]{"counter:dispel"}),
        E("enemy:inquisition-high-judge","성화 대심문관","전장의 최고 전력을 봉인한 뒤 정화병을 집중시킨다.","human:inquisition","Human",EnemyCombatRole.Boss,"judgement-center","weapon:rune-blade","armor:breastplate","",158,13,12,11,4.5f,new[]{"enemy-ability:truth-seal","enemy-ability:rally"},new[]{"counter:split-pressure"}),
        E("enemy:merchant-caravan-blade","금화동맹 대상검","화물을 버리지 않고 위협을 밀어낸다.","human:merchant","Human",EnemyCombatRole.Vanguard,"cargo-front","weapon:falchion","armor:leather","",86,8,6,7,4.5f,new[]{"enemy-ability:charge"},new[]{"counter:brace"}),
        E("enemy:merchant-hired-bulwark","금화동맹 고용방벽","계약 대상과 화물을 최우선으로 보호한다.","human:merchant","Human",EnemyCombatRole.Defender,"cargo-ring","weapon:mace","armor:scale-coat","",116,7,12,5,3.5f,new[]{"enemy-ability:shield-wall","enemy-ability:retreat-cover"},new[]{"counter:separate-cargo"}),
        E("enemy:merchant-crossbow","금화동맹 석궁수","안전한 사거리에서 확실한 표적만 쏜다.","human:merchant","Human",EnemyCombatRole.Marksman,"wagon-rear","weapon:crossbow","armor:leather","ammo:bolt",74,10,5,9,4,new[]{"enemy-ability:suppressive-volley"},new[]{"counter:cover"}),
        E("enemy:merchant-alchemist","금화동맹 연금술사","독성 구름과 연막으로 전리품 손상을 줄인다.","human:merchant","Human",EnemyCombatRole.Controller,"wagon-center","weapon:dagger","armor:smoke-hood","",82,7,6,9,4,new[]{"enemy-ability:poison-cloud","enemy-ability:smoke-screen"},new[]{"counter:air-filter"}),
        E("enemy:merchant-factor","금화동맹 전투지배인","불리하면 엄호 퇴각하고 유리하면 용병을 부른다.","human:merchant","Human",EnemyCombatRole.Support,"command-rear","weapon:matchlock-pistol","armor:brigandine","ammo:paper-cartridge",104,9,8,9,4,new[]{"enemy-ability:summon-minion","enemy-ability:retreat-cover"},new[]{"counter:deny-retreat"}),
        E("enemy:settler-trapper","자유개척 덫사냥꾼","빠른 목표를 구속하고 측면으로 이동한다.","human:settler","Human",EnemyCombatRole.Controller,"loose-skirmish","weapon:throwing-axe","armor:leather","",78,8,5,10,5,new[]{"enemy-ability:frost-bind"},new[]{"counter:slow-advance"}),
        E("enemy:settler-barricadier","자유개척 바리케이드병","임시 방벽 뒤에서 약한 동료를 지킨다.","human:settler","Human",EnemyCombatRole.Defender,"scattered-cover","weapon:falchion","armor:gambeson","",102,7,10,6,4,new[]{"enemy-ability:shield-wall"},new[]{"counter:destroy-cover"}),
        E("enemy:settler-hunter","자유개척 사냥꾼","부상한 목표를 추적해 전투 이탈을 막는다.","human:settler","Human",EnemyCombatRole.Marksman,"high-ground","weapon:composite-bow","armor:leather","ammo:arrow",76,10,5,11,5,new[]{"enemy-ability:suppressive-volley"},new[]{"counter:smoke"}),
        E("enemy:settler-herbalist","자유개척 약초사","치료와 독성 도포를 상황에 따라 바꾼다.","human:settler","Human",EnemyCombatRole.Support,"cover-rear","weapon:dagger","armor:leather","",80,6,5,10,4.5f,new[]{"enemy-ability:field-dressing","enemy-ability:poison-cloud"},new[]{"counter:focus-support"}),
        E("enemy:settler-ranger-chief","자유개척 순찰대장","분산 진형과 집중 사격을 교대로 명령한다.","human:settler","Human",EnemyCombatRole.Boss,"mobile-command","weapon:composite-bow","armor:brigandine","ammo:arrow",130,11,9,12,5,new[]{"enemy-ability:rally","enemy-ability:retreat-cover","enemy-ability:suppressive-volley"},new[]{"counter:pin-leader"}),
        E("enemy:rival-packbreaker","경쟁 던전 무리파쇄자","갈고리로 후열을 끌어내 수인 돌격대에 넘긴다.","rival:dungeon","Beastkin",EnemyCombatRole.Vanguard,"pack-wedge","weapon:halberd","armor:scale-coat","",124,12,9,10,5,new[]{"enemy-ability:hook-pull","enemy-ability:charge"},new[]{"counter:guard-backline"}),
        E("enemy:rival-pactbinder","경쟁 던전 계약결속사","소환체와 보호막으로 시간을 번다.","rival:dungeon","Demon",EnemyCombatRole.Controller,"pact-circle","weapon:mana-lance","armor:rune-ward-mail","",110,10,8,11,4.5f,new[]{"enemy-ability:summon-minion","enemy-ability:rune-ward"},new[]{"counter:dispel"}),
        E("enemy:rival-siege-frame","경쟁 던전 공성골렘","갑주를 파쇄하고 스스로 핵을 수리한다.","rival:dungeon","Golem",EnemyCombatRole.Defender,"siege-column","weapon:mana-lance","armor:blacksteel-carapace","",175,13,15,5,3,new[]{"enemy-ability:armor-break","enemy-ability:core-repair"},new[]{"counter:mana-disrupt"}),
        E("enemy:rival-stormcaller","경쟁 던전 폭풍부름꾼","높은 기동력으로 후열을 제압하고 연막을 흩뜨린다.","rival:dungeon","Harpy",EnemyCombatRole.Marksman,"aerial-skirmish","weapon:composite-bow","armor:leather","ammo:arrow",96,12,6,14,6,new[]{"enemy-ability:suppressive-volley","enemy-ability:frost-bind"},new[]{"counter:anti-air"}),
        E("enemy:rival-spore-shepherd","경쟁 던전 포자목자","독성 포자와 소형 균사체로 지역을 장악한다.","rival:dungeon","Myconid",EnemyCombatRole.Support,"spore-garden","weapon:spear","armor:rune-ward-mail","",118,9,10,9,3.5f,new[]{"enemy-ability:poison-cloud","enemy-ability:summon-minion"},new[]{"counter:fungicide"}),
        E("enemy:truth-sealkeeper","진실 봉인수호자","강화된 목표를 찾아 진실 봉인을 새긴다.","truth:guardian","Truth",EnemyCombatRole.Controller,"seal-triangle","weapon:rune-blade","armor:rune-ward-mail","",142,13,12,11,4.5f,new[]{"enemy-ability:truth-seal","enemy-ability:arcane-null"},new[]{"counter:mundane"}),
        E("enemy:truth-null-warden","진실 무효감시자","방벽과 주문을 지우며 봉인 장치를 지킨다.","truth:guardian","Construct",EnemyCombatRole.Defender,"seal-core","weapon:mana-lance","armor:blacksteel-carapace","",184,12,16,7,3.5f,new[]{"enemy-ability:arcane-null","enemy-ability:rune-ward","enemy-ability:core-repair"},new[]{"counter:powder"}),
        E("enemy:truth-archivist","진실 기록집행자","전투 기록에 따라 반복 전술을 봉쇄하는 보스.","truth:guardian","Truth",EnemyCombatRole.Boss,"archive-center","weapon:mana-lance","armor:powered-harness","",230,16,17,14,4,new[]{"enemy-ability:truth-seal","enemy-ability:summon-minion","enemy-ability:rally"},new[]{"counter:mixed-tactics"}),
        E("enemy:neutral-mercenary","중립 용병대장","계약 목표가 무너지면 엄호 퇴각한다.","neutral:mercenary","Human",EnemyCombatRole.Vanguard,"contract-line","weapon:greatsword","armor:brigandine","",108,11,8,9,4.5f,new[]{"enemy-ability:charge","enemy-ability:retreat-cover"},new[]{"counter:break-contract"}),
        E("enemy:neutral-clockwork","유랑 태엽구성체","가장 가까운 장치를 보호하고 손상 시 수리한다.","neutral:construct","Golem",EnemyCombatRole.Defender,"device-guard","weapon:mace","armor:breastplate","",148,10,14,6,3.5f,new[]{"enemy-ability:shield-wall","enemy-ability:core-repair"},new[]{"counter:precision"}),
        E("enemy:neutral-smoke-sapper","연막 공병","연막 속에서 시설 목표에 접근해 파괴한다.","neutral:specialist","Human",EnemyCombatRole.Controller,"sapper-wedge","weapon:matchlock-pistol","armor:blast-coat","ammo:paper-cartridge",94,10,7,11,4.5f,new[]{"enemy-ability:smoke-screen","enemy-ability:powder-shot"},new[]{"counter:detector"})
    };

    private static readonly ModifierSpec[] Modifiers=
    {
        M("battlefield:narrow-bridge","좁은 교량","전열 교대가 어렵고 밀집 공격의 가치가 커진다.",BattlefieldModifierKind.Terrain,.7f,1f,1.08f,"counter:reach"),
        M("battlefield:broken-pillars","무너진 기둥숲","엄폐는 많지만 직선 사격과 빠른 이동이 어렵다.",BattlefieldModifierKind.Terrain,.8f,.75f,1f,"counter:flank"),
        M("battlefield:elevated-gallery","고가 회랑","높은 사격 위치와 추락 위험이 동시에 생긴다.",BattlefieldModifierKind.Terrain,.9f,1.18f,1f,"counter:anti-air"),
        M("battlefield:flooded-floor","침수 바닥","중장비 이동이 느려지고 전기·서리 효과가 강화된다.",BattlefieldModifierKind.Terrain,.65f,1f,1.1f,"counter:insulated"),
        M("battlefield:civilian-corridor","피난 통로","민간인 보호 때문에 광역 공격을 자유롭게 쓰기 어렵다.",BattlefieldModifierKind.Objective,.9f,.9f,.85f,"counter:precision"),
        M("battlefield:unstable-device","불안정 장치","시간 내 장치를 파괴하지 못하면 양측 모두 피해를 입는다.",BattlefieldModifierKind.Objective,1f,1f,1.1f,"counter:sabotage"),
        M("battlefield:sealed-exit","봉인된 퇴로","탈출 장치를 조작하기 전에는 퇴각할 수 없다.",BattlefieldModifierKind.Objective,.85f,1f,1f,"counter:engineering"),
        M("battlefield:hostage-chain","인질 구속줄","지휘관 생포와 인질 보호가 같은 위치에서 충돌한다.",BattlefieldModifierKind.Objective,.9f,.9f,.9f,"counter:nonlethal"),
        M("battlefield:mana-storm","마나 폭풍","비전 피해가 강해지지만 능력 오작동 위험도 커진다.",BattlefieldModifierKind.Hazard,.9f,.85f,1.2f,"counter:mana-grounding"),
        M("battlefield:powder-smoke","화약 연무","사격 명중이 떨어지고 연기 보호구 없는 병력이 지친다.",BattlefieldModifierKind.Hazard,1f,.65f,1f,"counter:smoke-hood"),
        M("battlefield:spore-bloom","포자 개화","장기전일수록 포자 노출과 시야 방해가 누적된다.",BattlefieldModifierKind.Hazard,.85f,.8f,1f,"counter:fungicide"),
        M("battlefield:collapsing-ceiling","붕괴 천장","한 위치에 오래 머물수록 낙하 피해 위험이 커진다.",BattlefieldModifierKind.Hazard,1.2f,.9f,1.1f,"counter:mobile")
    };

    // Explicit authored compositions. Do not replace this with modulo-based enemy,
    // objective or battlefield rotation: every row is a separate gameplay contract.
    private static readonly EncounterSpec[] Encounters=
    {
        C(1,"왕령 농지 징발",OffenseEncounterObjective.DefeatAll,0,"battlefield:narrow-bridge",new[]{"enemy:crown-levy","enemy:crown-tower-guard"},new[]{"counter:reach","counter:break-formation"}),
        C(2,"왕령 호송대",OffenseEncounterObjective.SurviveRounds,8,"battlefield:broken-pillars",new[]{"enemy:crown-tower-guard","enemy:crown-longbow"},new[]{"counter:flank","counter:armor-break"}),
        C(3,"도시연맹 무기고",OffenseEncounterObjective.ProtectTarget,6,"battlefield:elevated-gallery",new[]{"enemy:legion-arquebus","enemy:legion-pavise","enemy:legion-surgeon"},new[]{"counter:anti-air","counter:smoke","counter:guard-backline"},"target:field-medic"),
        C(4,"성화 비전 봉쇄",OffenseEncounterObjective.SabotageTarget,7,"battlefield:flooded-floor",new[]{"enemy:inquisition-censor","enemy:inquisition-ward-knight","enemy:inquisition-purifier"},new[]{"counter:insulated","counter:mana-disrupt","counter:focus-support"},"target:war-device"),
        C(5,"경쟁 던전 척후",OffenseEncounterObjective.Escape,8,"battlefield:civilian-corridor",new[]{"enemy:rival-stormcaller","enemy:rival-packbreaker"},new[]{"counter:precision","counter:anti-air","counter:guard-backline"},elite:true),
        C(6,"진실 봉인실",OffenseEncounterObjective.CaptureLeader,6,"battlefield:unstable-device",new[]{"enemy:truth-sealkeeper","enemy:truth-null-warden"},new[]{"counter:sabotage","counter:mundane","counter:nonlethal"},"enemy:truth-sealkeeper",boss:true),
        C(7,"도시연맹 파비스 전열",OffenseEncounterObjective.DefeatAll,0,"battlefield:sealed-exit",new[]{"enemy:legion-pavise","enemy:legion-pikeman"},new[]{"counter:engineering","counter:flank"}),
        C(8,"도시연맹 화약 일제",OffenseEncounterObjective.SurviveRounds,8,"battlefield:hostage-chain",new[]{"enemy:legion-arquebus","enemy:legion-pavise"},new[]{"counter:nonlethal","counter:smoke-hood","counter:rush"}),
        C(9,"도시연맹 야전병원",OffenseEncounterObjective.ProtectTarget,7,"battlefield:mana-storm",new[]{"enemy:legion-surgeon","enemy:legion-pikeman","enemy:legion-arquebus"},new[]{"counter:mana-grounding","counter:interrupt-heal"},"target:field-medic"),
        C(10,"도시연맹 지휘소",OffenseEncounterObjective.SabotageTarget,7,"battlefield:powder-smoke",new[]{"enemy:legion-captain","enemy:legion-pavise"},new[]{"counter:smoke-hood","counter:isolate-leader","counter:sabotage"},"target:war-device",elite:true),
        C(11,"성화 심문 정화대",OffenseEncounterObjective.Escape,7,"battlefield:spore-bloom",new[]{"enemy:inquisition-purifier","enemy:inquisition-rune-sniper"},new[]{"counter:fungicide","counter:kite"}),
        C(12,"성화 결계기사 생포",OffenseEncounterObjective.CaptureLeader,7,"battlefield:collapsing-ceiling",new[]{"enemy:inquisition-ward-knight","enemy:inquisition-censor"},new[]{"counter:mobile","counter:physical-burst","counter:nonlethal"},"enemy:inquisition-ward-knight",boss:true),
        C(13,"성화 룬저격수 교량",OffenseEncounterObjective.DefeatAll,0,"battlefield:narrow-bridge",new[]{"enemy:inquisition-rune-sniper","enemy:inquisition-ward-knight"},new[]{"counter:reach","counter:mundane","counter:smoke"}),
        C(14,"성화 검열관 방어선",OffenseEncounterObjective.SurviveRounds,8,"battlefield:broken-pillars",new[]{"enemy:inquisition-censor","enemy:inquisition-purifier"},new[]{"counter:flank","counter:dispel"}),
        C(15,"성화 대심문관의 선고",OffenseEncounterObjective.ProtectTarget,8,"battlefield:elevated-gallery",new[]{"enemy:inquisition-high-judge","enemy:inquisition-rune-sniper","enemy:inquisition-ward-knight"},new[]{"counter:anti-air","counter:split-pressure"},"target:field-medic",elite:true),
        C(16,"금화동맹 대상 호위",OffenseEncounterObjective.SabotageTarget,7,"battlefield:flooded-floor",new[]{"enemy:merchant-caravan-blade","enemy:merchant-hired-bulwark"},new[]{"counter:insulated","counter:brace","counter:sabotage"},"target:war-device"),
        C(17,"금화동맹 화물 방벽",OffenseEncounterObjective.Escape,8,"battlefield:civilian-corridor",new[]{"enemy:merchant-hired-bulwark","enemy:merchant-crossbow"},new[]{"counter:precision","counter:separate-cargo"}),
        C(18,"금화동맹 석궁 지휘관",OffenseEncounterObjective.CaptureLeader,6,"battlefield:unstable-device",new[]{"enemy:merchant-crossbow","enemy:merchant-caravan-blade"},new[]{"counter:sabotage","counter:cover","counter:nonlethal"},"enemy:merchant-crossbow",boss:true),
        C(19,"금화동맹 연금 연무",OffenseEncounterObjective.DefeatAll,0,"battlefield:sealed-exit",new[]{"enemy:merchant-alchemist","enemy:merchant-hired-bulwark"},new[]{"counter:engineering","counter:air-filter","counter:rush"}),
        C(20,"금화동맹 전투지배인",OffenseEncounterObjective.SurviveRounds,8,"battlefield:hostage-chain",new[]{"enemy:merchant-factor","enemy:merchant-crossbow","enemy:merchant-caravan-blade"},new[]{"counter:nonlethal","counter:deny-retreat"},elite:true),
        C(21,"자유개척 덫사냥꾼",OffenseEncounterObjective.ProtectTarget,7,"battlefield:mana-storm",new[]{"enemy:settler-trapper","enemy:settler-hunter"},new[]{"counter:mana-grounding","counter:slow-advance"},"target:field-medic"),
        C(22,"자유개척 임시 방벽",OffenseEncounterObjective.SabotageTarget,7,"battlefield:powder-smoke",new[]{"enemy:settler-barricadier","enemy:settler-hunter"},new[]{"counter:smoke-hood","counter:destroy-cover","counter:sabotage"},"target:war-device"),
        C(23,"자유개척 추적 사냥",OffenseEncounterObjective.Escape,7,"battlefield:spore-bloom",new[]{"enemy:settler-hunter","enemy:settler-trapper"},new[]{"counter:fungicide","counter:smoke"}),
        C(24,"자유개척 약초사 생포",OffenseEncounterObjective.CaptureLeader,6,"battlefield:collapsing-ceiling",new[]{"enemy:settler-herbalist","enemy:settler-barricadier"},new[]{"counter:mobile","counter:focus-support","counter:nonlethal"},"enemy:settler-herbalist",boss:true),
        C(25,"자유개척 순찰대 결전",OffenseEncounterObjective.DefeatAll,0,"battlefield:narrow-bridge",new[]{"enemy:settler-ranger-chief","enemy:settler-hunter","enemy:settler-trapper"},new[]{"counter:reach","counter:pin-leader"},elite:true),
        C(26,"경쟁 던전 무리파쇄자",OffenseEncounterObjective.SurviveRounds,8,"battlefield:broken-pillars",new[]{"enemy:rival-packbreaker","enemy:rival-stormcaller"},new[]{"counter:flank","counter:guard-backline"}),
        C(27,"경쟁 던전 계약결속사",OffenseEncounterObjective.ProtectTarget,7,"battlefield:elevated-gallery",new[]{"enemy:rival-pactbinder","enemy:rival-packbreaker"},new[]{"counter:anti-air","counter:dispel"},"target:field-medic"),
        C(28,"경쟁 던전 공성골렘",OffenseEncounterObjective.SabotageTarget,8,"battlefield:flooded-floor",new[]{"enemy:rival-siege-frame","enemy:rival-pactbinder"},new[]{"counter:insulated","counter:mana-disrupt","counter:sabotage"},"target:war-device"),
        C(29,"경쟁 던전 폭풍부름꾼",OffenseEncounterObjective.Escape,7,"battlefield:civilian-corridor",new[]{"enemy:rival-stormcaller","enemy:rival-packbreaker"},new[]{"counter:precision","counter:anti-air"}),
        C(30,"경쟁 던전 포자목자",OffenseEncounterObjective.CaptureLeader,7,"battlefield:unstable-device",new[]{"enemy:rival-spore-shepherd","enemy:rival-pactbinder"},new[]{"counter:sabotage","counter:fungicide","counter:nonlethal"},"enemy:rival-spore-shepherd",boss:true),
        C(31,"진실 봉인수호자",OffenseEncounterObjective.DefeatAll,0,"battlefield:sealed-exit",new[]{"enemy:truth-sealkeeper","enemy:truth-null-warden"},new[]{"counter:engineering","counter:mundane"}),
        C(32,"진실 무효감시자",OffenseEncounterObjective.SurviveRounds,8,"battlefield:hostage-chain",new[]{"enemy:truth-null-warden","enemy:truth-sealkeeper"},new[]{"counter:nonlethal","counter:powder","counter:interrupt-heal"}),
        C(33,"진실 기록집행자",OffenseEncounterObjective.ProtectTarget,8,"battlefield:mana-storm",new[]{"enemy:truth-archivist","enemy:truth-sealkeeper","enemy:truth-null-warden"},new[]{"counter:mana-grounding","counter:mixed-tactics"},"target:field-medic",elite:true),
        C(34,"중립 용병 계약 붕괴",OffenseEncounterObjective.SabotageTarget,7,"battlefield:powder-smoke",new[]{"enemy:neutral-mercenary","enemy:merchant-crossbow"},new[]{"counter:smoke-hood","counter:break-contract","counter:sabotage"},"target:war-device"),
        C(35,"유랑 태엽구성체",OffenseEncounterObjective.Escape,8,"battlefield:spore-bloom",new[]{"enemy:neutral-clockwork","enemy:rival-spore-shepherd"},new[]{"counter:fungicide","counter:precision"},elite:true),
        C(36,"연막 공병 생포",OffenseEncounterObjective.CaptureLeader,6,"battlefield:collapsing-ceiling",new[]{"enemy:neutral-smoke-sapper","enemy:neutral-mercenary"},new[]{"counter:mobile","counter:detector","counter:nonlethal"},"enemy:neutral-smoke-sapper",boss:true)
    };

    [MenuItem("DungeonStory/V20/Build Combat Content (96)")]
    public static void Build()
    {
        if(Abilities.Length!=18||Enemies.Length!=36||Modifiers.Length!=12)throw new InvalidOperationException("V20 combat manifest count contract is broken.");
        Ensure("Assets/Resources/SO/V20","Combat");foreach(string folder in new[]{"Abilities","Enemies","Modifiers","Encounters"})Ensure(Root,folder);
        GameDomainContentCatalogSO catalog=AssetDatabase.LoadAssetAtPath<GameDomainContentCatalogSO>(CatalogPath)??throw new InvalidOperationException("Domain catalog missing.");
        if(Encounters.Length!=36||Encounters.Select(value=>value.Index).Distinct().Count()!=36)throw new InvalidOperationException("V20 authored encounter manifest must contain 36 unique entries.");
        if(Encounters.Select(value=>$"{value.Objective}|{value.ModifierId}|{string.Join(",",value.EnemyIds)}|{string.Join(",",value.CounterTags)}").Distinct(StringComparer.Ordinal).Count()!=36)throw new InvalidOperationException("Every V20 encounter requires a unique authored gameplay signature.");
        List<EnemyAbilityDefinitionSO> abilities=Abilities.Select(CreateAbility).ToList();List<EnemyArchetypeDefinitionSO> enemies=Enemies.Select(CreateEnemy).ToList();List<BattlefieldModifierDefinitionSO> modifiers=Modifiers.Select(CreateModifier).ToList();List<OffenseEncounterSO> encounters=Encounters.Select(spec=>CreateEncounter(spec,enemies,modifiers)).ToList();
        List<string> errors=abilities.SelectMany(x=>x.ValidateDefinition()).Concat(enemies.SelectMany(x=>x.ValidateDefinition())).Concat(modifiers.SelectMany(x=>x.ValidateDefinition())).Concat(encounters.SelectMany(x=>x.ValidateDefinition())).ToList();
        HashSet<string> abilityIds=abilities.Select(x=>x.stableId).ToHashSet(StringComparer.Ordinal);foreach(EnemyArchetypeDefinitionSO enemy in enemies)foreach(string id in enemy.abilityIds)if(!abilityIds.Contains(id))errors.Add($"{enemy.stableId} missing ability {id}");
        HashSet<string> enemyIds=enemies.Select(x=>x.stableId).ToHashSet(StringComparer.Ordinal);foreach(OffenseEncounterSO encounter in encounters)foreach(OffenseEnemyArchetypeEntry entry in encounter.enemies)if(!enemyIds.Contains(entry.enemyArchetypeId))errors.Add($"{encounter.encounterId} missing enemy {entry.enemyArchetypeId}");
        if(errors.Count>0)throw new InvalidOperationException(string.Join(" | ",errors));
        Type[] owned={typeof(EnemyAbilityDefinitionSO),typeof(EnemyArchetypeDefinitionSO),typeof(BattlefieldModifierDefinitionSO),typeof(OffenseEncounterSO)};
        catalog.SetDefinitions(catalog.Definitions.Where(x=>x!=null&&!owned.Contains(x.GetType())).Concat(abilities).Concat(enemies).Concat(modifiers).Concat(encounters));EditorUtility.SetDirty(catalog);AssetDatabase.SaveAssets();AssetDatabase.Refresh();
        Debug.Log("V20_COMBAT_CONTENT=PASS; abilities=18; enemies=36; encountersTotal=36; encountersNew=30; modifiers=12; netNew=96");
    }

    private static EnemyAbilityDefinitionSO CreateAbility(AbilitySpec s){EnemyAbilityDefinitionSO v=Asset<EnemyAbilityDefinitionSO>($"{Root}/Abilities/{s.Id.Replace(':','_')}.asset");v.stableId=s.Id;v.displayName=s.Name;v.description=s.Description;v.authoringRevision=1;v.sourceNote="V20 hand-authored enemy ability.";v.cooldownRounds=s.Cooldown;v.targetRule=s.TargetRule;v.effects=new List<EnemyAbilityEffectRecord>{new(){kind=s.Kind,magnitude=s.Magnitude,durationRounds=s.Duration,targetTag=s.Target}};Dirty(v);return v;}
    private static EnemyArchetypeDefinitionSO CreateEnemy(EnemySpec s){EnemyArchetypeDefinitionSO v=Asset<EnemyArchetypeDefinitionSO>($"{Root}/Enemies/{s.Id.Replace(':','_')}.asset");v.stableId=s.Id;v.displayName=s.Name;v.description=s.Description;v.authoringRevision=1;v.sourceNote="V20 hand-authored enemy archetype.";v.factionId=s.Faction;v.speciesTag=s.Species;v.role=s.Role;v.maxHealth=s.Health;v.attack=s.Attack;v.strength=s.Attack*.85f;v.toughness=s.Toughness;v.dexterity=s.Dexterity;v.moveSpeed=s.Move;v.equipment=new EnemyEquipmentLoadoutRecord{weaponDefinitionId=s.Weapon,armorDefinitionId=s.Armor,shieldDefinitionId=ShieldFor(s.Id),ammunitionItemId=s.Ammo};v.abilityIds=s.Abilities.ToList();v.counterTags=s.Counters.ToList();v.rewardItemIds=s.Rewards.ToList();v.tacticalProfile=Profile(s.Role,s.Formation);v.individualGeneration=new EnemyIndividualGenerationProfile{minimumGeneralTraits=2,maximumGeneralTraits=s.Role==EnemyCombatRole.Boss?4:3,minimumExpressedHeritableTraits=0,maximumExpressedHeritableTraits=2,maximumLatentHeritableTraits=1,aptitudeVariance=15,combatStatVariance=.12f,minimumLoyalty=s.Role==EnemyCombatRole.Boss?55f:20f,maximumLoyalty=s.Role==EnemyCombatRole.Boss?90f:75f,recruitable=s.Species!="Truth",militaryTrainingId="training:"+s.Id.Substring("enemy:".Length)};v.bossPhases=s.Role==EnemyCombatRole.Boss?new List<EnemyBossPhaseRecord>{new(){healthThreshold=.5f,abilityIds=s.Abilities.Take(2).ToList(),tacticalProfileOverrideTag="desperate"}}:new List<EnemyBossPhaseRecord>();Dirty(v);return v;}
    private static string ShieldFor(string enemyId) => enemyId switch
    {
        "enemy:crown-tower-guard" => "shield:tower",
        "enemy:legion-pavise" => "shield:pavise",
        "enemy:inquisition-ward-knight" => "shield:mana-buckler",
        "enemy:merchant-hired-bulwark" => "shield:tower",
        "enemy:settler-barricadier" => "shield:wood",
        "enemy:neutral-clockwork" => "shield:iron",
        _ => string.Empty
    };
    private static EnemyTacticalProfile Profile(EnemyCombatRole role,string formation)=>new(){attackWeight=role==EnemyCombatRole.Support?1:4,protectWeight=role is EnemyCombatRole.Defender or EnemyCombatRole.Support?5:1,abilityWeight=role is EnemyCombatRole.Controller or EnemyCombatRole.Support or EnemyCombatRole.Boss?6:3,retreatWeight=role==EnemyCombatRole.Boss?0:2,retreatHealthFraction=role==EnemyCombatRole.Defender?.08f:.2f,preferredTargetTags=new List<string>{role==EnemyCombatRole.Marksman?"backline":role==EnemyCombatRole.Controller?"fast":"nearest"},avoidedTargetTags=new List<string>{role==EnemyCombatRole.Marksman?"shielded":"none"},formationTag=formation};
    private static BattlefieldModifierDefinitionSO CreateModifier(ModifierSpec s){BattlefieldModifierDefinitionSO v=Asset<BattlefieldModifierDefinitionSO>($"{Root}/Modifiers/{s.Id.Replace(':','_')}.asset");v.stableId=s.Id;v.displayName=s.Name;v.description=s.Description;v.authoringRevision=1;v.sourceNote="V20 hand-authored battlefield modifier.";v.kind=s.Kind;v.movementMultiplier=s.Move;v.accuracyMultiplier=s.Accuracy;v.damageMultiplier=s.Damage;v.requiredCounterTag=s.Counter;Dirty(v);return v;}
    private static OffenseEncounterSO CreateEncounter(EncounterSpec spec,IReadOnlyList<EnemyArchetypeDefinitionSO> enemies,IReadOnlyList<BattlefieldModifierDefinitionSO> modifiers)
    {
        Dictionary<string,EnemyArchetypeDefinitionSO> enemyById=enemies.ToDictionary(value=>value.stableId,StringComparer.Ordinal);HashSet<string> modifierIds=modifiers.Select(value=>value.stableId).ToHashSet(StringComparer.Ordinal);
        if(!modifierIds.Contains(spec.ModifierId))throw new InvalidOperationException($"Encounter {spec.Index:00} references missing modifier {spec.ModifierId}.");
        foreach(string enemyId in spec.EnemyIds)if(!enemyById.ContainsKey(enemyId))throw new InvalidOperationException($"Encounter {spec.Index:00} references missing enemy {enemyId}.");
        string path=spec.Index<=6?$"Assets/Resources/SO/Offense/Encounters/encounter_{spec.Index:00}.asset":$"{Root}/Encounters/encounter_{spec.Index:00}.asset";OffenseEncounterSO v=Asset<OffenseEncounterSO>(path);v.id=spec.Index<=6?spec.Index:8000+spec.Index;v.encounterId=$"encounter:{spec.Index:00}";
        v.displayName=spec.Name;v.minimumSiteStrength=Mathf.Max(1,(spec.Index-1)/4+1);v.maximumSiteStrength=v.minimumSiteStrength+3;v.elite=spec.Elite;v.boss=spec.Boss;
        v.objective=spec.Objective;v.objectiveRoundLimit=spec.Objective==OffenseEncounterObjective.DefeatAll?0:Mathf.Max(1,spec.RoundLimit);v.objectiveTargetId=spec.ObjectiveTargetId??string.Empty;
        v.battlefieldModifierIds=new List<string>{spec.ModifierId};v.counterTags=spec.CounterTags.Distinct(StringComparer.Ordinal).ToList();v.rewardItemIds=spec.RewardItemIds.Distinct(StringComparer.Ordinal).ToList();v.enemies=spec.EnemyIds.Select((enemyId,index)=>new OffenseEnemyArchetypeEntry{enemyArchetypeId=enemyId,minimumCount=1,maximumCount=spec.Boss&&index==0?1:2}).ToList();Dirty(v);return v;
    }
    private static T Asset<T>(string path)where T:ScriptableObject{UnityEngine.Object x=AssetDatabase.LoadMainAssetAtPath(path);if(x!=null&&x is not T)throw new InvalidOperationException($"Wrong asset type at {path}");if(x is T t)return t;T v=ScriptableObject.CreateInstance<T>();AssetDatabase.CreateAsset(v,path);return v;}
    private static void Ensure(string parent,string child){string p=$"{parent}/{child}";if(!AssetDatabase.IsValidFolder(p))AssetDatabase.CreateFolder(parent,child);}private static void Dirty(UnityEngine.Object v)=>EditorUtility.SetDirty(v);
    private static AbilitySpec A(string id,string name,string description,int cooldown,EnemyAbilityEffectKind kind,float magnitude,string target,OffenseBattleTargetRule rule,int duration=0)=>new(){Id=id,Name=name,Description=description,Cooldown=cooldown,Kind=kind,Magnitude=magnitude,Target=target,TargetRule=rule,Duration=duration};
    private static EnemySpec E(string id,string name,string description,string faction,string species,EnemyCombatRole role,string formation,string weapon,string armor,string ammo,float health,float attack,float toughness,float dexterity,float move,string[] abilities,string[] counters)=>new(){Id=id,Name=name,Description=description,Faction=faction,Species=species,Role=role,Formation=formation,Weapon=weapon,Armor=armor,Ammo=ammo,Health=health,Attack=attack,Toughness=toughness,Dexterity=dexterity,Move=move,Abilities=abilities,Counters=counters,Rewards=new[]{OffenseLootItemIds.UnappraisedLoot}};
    private static ModifierSpec M(string id,string name,string description,BattlefieldModifierKind kind,float move,float accuracy,float damage,string counter)=>new(){Id=id,Name=name,Description=description,Kind=kind,Move=move,Accuracy=accuracy,Damage=damage,Counter=counter};
    private static EncounterSpec C(int index,string name,OffenseEncounterObjective objective,int roundLimit,string modifierId,string[] enemyIds,string[] counters,string objectiveTargetId="",bool elite=false,bool boss=false)=>new(){Index=index,Name=name,Objective=objective,RoundLimit=roundLimit,ObjectiveTargetId=objectiveTargetId,ModifierId=modifierId,EnemyIds=enemyIds,CounterTags=counters,RewardItemIds=new[]{OffenseLootItemIds.UnappraisedLoot},Elite=elite,Boss=boss};
}
#endif
