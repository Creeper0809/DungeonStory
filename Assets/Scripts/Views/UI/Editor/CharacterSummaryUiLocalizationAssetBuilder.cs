#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Localization;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.Localization.Tables;

public static class CharacterSummaryUiLocalizationAssetBuilder
{
    private const string Root = "Assets/Localization";
    private static readonly IReadOnlyDictionary<string, string> Korean =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["CharacterSummary.Deprivation.Hunger"] = "굶주림",
            ["CharacterSummary.Deprivation.Thirst"] = "탈수",
            ["CharacterSummary.Deprivation.Bladder"] = "방광 손상",
            ["CharacterSummary.Deprivation.Contamination"] = "오염",
            ["CharacterSummary.Deprivation.Exhaustion"] = "탈진",
            ["CharacterSummary.Deprivation.MentalInstability"] = "정신 불안",

            ["CharacterSummary.Burden.Critical"] = "붕괴 임박",
            ["CharacterSummary.Burden.Danger"] = "위험",
            ["CharacterSummary.Burden.Unhealthy"] = "건강 이상",
            ["CharacterSummary.Burden.Accumulating"] = "누적 중",
            ["CharacterSummary.Burden.Stable"] = "안정",

            ["CharacterSummary.Breakdown.DesperateRelief"] = "배변 통제 상실",
            ["CharacterSummary.Breakdown.DesperateDrink"] = "절박한 갈증",
            ["CharacterSummary.Breakdown.DesperateEat"] = "금기 포식",
            ["CharacterSummary.Breakdown.Collapse"] = "실신",
            ["CharacterSummary.Breakdown.ViolentImpulse"] = "폭력 충동",
            ["CharacterSummary.Common.None"] = "없음",

            ["CharacterSummary.Mood.NoFactors"] = "현재 기분을 바꾸는 요인이 없습니다.",
            ["CharacterSummary.Mood.NeedHeading"] = "욕구 영향",
            ["CharacterSummary.Mood.InteractionHeading"] = "최근 경험",
            ["CharacterSummary.Mood.FactorRow"] = "- {0}  {1}",
            ["CharacterSummary.Mood.InteractionRow"] = "- {0}  {1}  · {2}",
            ["CharacterSummary.Log.Empty"] = "아직 기록이 없습니다.",
            ["CharacterSummary.Log.Entry"] = "- {0}",

            ["CharacterSummary.Health.Thirsty"] = "갈증",
            ["CharacterSummary.Health.Hungry"] = "배고픔",
            ["CharacterSummary.Health.Exposed"] = "노출",
            ["CharacterSummary.Health.Sick"] = "질병",
            ["CharacterSummary.Health.Infected"] = "감염",
            ["CharacterSummary.Health.Recovering"] = "회복 중",
            ["CharacterSummary.Health.Healthy"] = "건강",

            ["CharacterSummary.Time.Seconds"] = "{0}초",
            ["CharacterSummary.Time.MinutesSeconds"] = "{0}분 {1}초",
            ["CharacterSummary.Time.Minutes"] = "{0}분",
            ["CharacterSummary.Role.Owner"] = "사장",
            ["CharacterSummary.Role.Regular"] = "일반",

            ["CharacterSummary.Lifecycle.SpawningOutside"] = "입장 준비",
            ["CharacterSummary.Lifecycle.EnteringDungeon"] = "입장 중",
            ["CharacterSummary.Lifecycle.Active"] = "활동 중",
            ["CharacterSummary.Lifecycle.ExitingDungeon"] = "퇴장 중",
            ["CharacterSummary.Lifecycle.OnExpedition"] = "원정 중",
            ["CharacterSummary.Lifecycle.PreparingExpedition"] = "출정 준비",
            ["CharacterSummary.Lifecycle.DepartingExpedition"] = "출정 중",
            ["CharacterSummary.Lifecycle.ReturningExpedition"] = "귀환 중",
            ["CharacterSummary.Lifecycle.Downed"] = "쓰러짐",
            ["CharacterSummary.Lifecycle.Despawned"] = "퇴장",
            ["CharacterSummary.Lifecycle.Waiting"] = "대기",

            ["CharacterSummary.Captivity.AwaitingCapture"] = "포획 대기",
            ["CharacterSummary.Captivity.Stabilizing"] = "현장 안정화",
            ["CharacterSummary.Captivity.AwaitingEscort"] = "호송 대기",
            ["CharacterSummary.Captivity.Escorting"] = "호송 중",
            ["CharacterSummary.Captivity.Confined"] = "수용 중",
            ["CharacterSummary.Captivity.Labor"] = "노역 중",
            ["CharacterSummary.Captivity.Interaction"] = "관리 작업 중",
            ["CharacterSummary.Captivity.Performer"] = "공연 참가",
            ["CharacterSummary.Captivity.EscapeAttempt"] = "탈출 시도",
            ["CharacterSummary.Captivity.Ransom"] = "몸값 협상",
            ["CharacterSummary.Weight.Kilograms"] = "{0:0.#}kg",

            ["CharacterSummary.Action.Close"] = "닫기",
            ["CharacterSummary.Tab.Status"] = "상태",
            ["CharacterSummary.Tab.Health"] = "건강",
            ["CharacterSummary.Tab.Combat"] = "전투",
            ["CharacterSummary.Tab.Growth"] = "성장",
            ["CharacterSummary.Tab.Mood"] = "기분",
            ["CharacterSummary.Tab.Records"] = "기록",
            ["CharacterSummary.Section.Status"] = "상태",
            ["CharacterSummary.Section.Needs"] = "욕구",
            ["CharacterSummary.Section.Level"] = "레벨",
            ["CharacterSummary.Section.SkillSlots"] = "기술 슬롯과 후보",
            ["CharacterSummary.Section.Mood"] = "기분",
            ["CharacterSummary.Section.MoodFactors"] = "기분 요인",
            ["CharacterSummary.Meter.Health"] = "체력",
            ["CharacterSummary.Meter.Satiety"] = "포만감",
            ["CharacterSummary.Meter.Thirst"] = "갈증",
            ["CharacterSummary.Meter.Fun"] = "재미",
            ["CharacterSummary.Meter.Rest"] = "휴식",
            ["CharacterSummary.Meter.Excretion"] = "배변",
            ["CharacterSummary.Meter.Hygiene"] = "위생",
            ["CharacterSummary.Meter.Experience"] = "경험치",
            ["CharacterSummary.Meter.CurrentMood"] = "현재 기분",
            ["CharacterSummary.Carry.Empty"] = "소지 아이템 없음",
            ["CharacterSummary.Health.Action.Captivity"] = "포획 명령",
            ["CharacterSummary.Health.Action.DietFree"] = "식단: 자유식",
            ["CharacterSummary.Health.Action.ScheduleSurgery"] = "수술 예약",
            ["CharacterSummary.Health.Action.AutomaticEmergencyOn"] = "응급 수술: 켬",
            ["CharacterSummary.Health.Action.SelectSubstance"] = "약물 선택",
            ["CharacterSummary.Health.Action.SubstanceProhibited"] = "금지",
            ["CharacterSummary.Health.Empty"] = "결핍 건강 정보가 없습니다.",
            ["CharacterSummary.Combat.Action.Loadout"] = "전투 장비",
            ["CharacterSummary.Combat.Action.SwitchWeapon"] = "무기 교체",
            ["CharacterSummary.Combat.Action.Reload"] = "재장전",
            ["CharacterSummary.Combat.Action.Aimed"] = "조준",
            ["CharacterSummary.Combat.Action.FireAllowed"] = "사격 허용",
            ["CharacterSummary.Combat.Action.Repair"] = "수리 요청",
            ["CharacterSummary.Combat.Empty"] = "전투 정보가 없습니다.",
            ["CharacterSummary.Mood.DefaultSummary"] = "평온함 · 기준 50 · 보정 +0",
            ["CharacterSummary.AI.Empty"] = "AI 판단 기록이 아직 없습니다.",
            ["CharacterSummary.Detailed.Title"] = "상세 능력치",
            ["CharacterSummary.Detailed.Empty"] = "표시할 정보가 없습니다.",

            ["CharacterSummary.Detailed.Selection.None"] = "선택 없음",
            ["CharacterSummary.Detailed.Common.Unknown"] = "미상",
            ["CharacterSummary.Detailed.Summary.Health"] = "체력",
            ["CharacterSummary.Detailed.Summary.Dead"] = "사망",
            ["CharacterSummary.Detailed.Summary.CombatVitality"] = "현재 전투 생존력",
            ["CharacterSummary.Detailed.Summary.Level"] = "레벨",
            ["CharacterSummary.Detailed.Summary.LevelDetail"] = "성장과 파츠 효과는 아래 탭에서 분리 표시",
            ["CharacterSummary.Detailed.Summary.ActualMovement"] = "실제 이동",
            ["CharacterSummary.Detailed.Summary.MovementDetail"] = "기본 능력·건강·파츠·환경을 반영",
            ["CharacterSummary.Detailed.Summary.Species"] = "종족",
            ["CharacterSummary.Detailed.Summary.DungeonOwner"] = "던전 주인",
            ["CharacterSummary.Detailed.BaseStats.Breakdown"] = "기본 {0:+#;-#;0} · 종족/특성 {1:+#;-#;0} · 성장 {2:+#;-#;0} · 조건 {3:+#;-#;0}",
            ["CharacterSummary.Detailed.Work.CurrentMultiplier"] = "능력·건강·파츠·기분·환경을 반영한 현재 작업 배율",

            ["CharacterSummary.Detailed.Combat.PrimaryWeapon"] = "주 무기",
            ["CharacterSummary.Detailed.Combat.WeaponDetail"] = "피해 {0:0.#} × 재질 {1:0.##} · 관통 {2:0.#} × {3:0.##}{4}",
            ["CharacterSummary.Detailed.Combat.AmmoSuffix"] = " · 탄약 {0}/{1}",
            ["CharacterSummary.Detailed.Combat.Armor"] = "방어구",
            ["CharacterSummary.Detailed.Combat.Durability"] = "내구 {0:0}%",
            ["CharacterSummary.Detailed.Combat.ArmorDetail"] = "보호 부위 {0} · 베기 {1:0.#} · 관통 {2:0.#} · 충격 {3:0.#}",
            ["CharacterSummary.Detailed.Combat.Shield"] = "방패",
            ["CharacterSummary.Detailed.Combat.ShieldDetail"] = "정면 방어 {0:0.#}%",
            ["CharacterSummary.Detailed.Combat.DefensiveEquipment"] = "방어 장비",
            ["CharacterSummary.Detailed.Combat.EmptySlots"] = "장비 슬롯이 비어 있습니다.",

            ["CharacterSummary.Detailed.Anatomy.Redacted"] = "내부 파츠와 효율은 확인되지 않았습니다.",
            ["CharacterSummary.Detailed.Anatomy.Value"] = "{0} {1:0}% · 자연 기준 {2:0}%",
            ["CharacterSummary.Detailed.Anatomy.Detail"] = "파츠 효율 {0:0.##} × 가동률 {1:0.##} + 모듈 {2:+0.##;-0.##;0} · {3}",
            ["CharacterSummary.Detailed.Anatomy.Status"] = "신체 상태",
            ["CharacterSummary.Detailed.Anatomy.Normal"] = "정상",
            ["CharacterSummary.Detailed.Anatomy.NoAbnormalParts"] = "비정상 부위가 없습니다.",
            ["CharacterSummary.Detailed.Modifier.Capped"] = "원본 ×{0:0.##} · 상한 ×{1:0.##} 적용",
            ["CharacterSummary.Detailed.Modifier.Uncapped"] = "원본 ×{0:0.##} · 상한 ×{1:0.##}",
            ["CharacterSummary.Detailed.Axis.Label"] = "계산 축 · {0}",
            ["CharacterSummary.Detailed.Axis.Detail"] = "신체 부위 기여도를 합산한 내부 계산값",
            ["CharacterSummary.Detailed.Condition.Missing"] = "표현 누락({0})",

            ["CharacterSummary.Detailed.Recovery.Natural"] = "자연 회복",
            ["CharacterSummary.Detailed.Recovery.AssistedRegeneration"] = "보조 재생",
            ["CharacterSummary.Detailed.Recovery.MaintenanceOnly"] = "약식 정비 필요",
            ["CharacterSummary.Detailed.Recovery.ReplaceOnFailure"] = "파손 시 교체 필요",
            ["CharacterSummary.Detailed.Tab.Summary"] = "종합",
            ["CharacterSummary.Detailed.Tab.BaseStats"] = "기본 능력",
            ["CharacterSummary.Detailed.Tab.Work"] = "작업",
            ["CharacterSummary.Detailed.Tab.CombatEquipment"] = "전투·장비",
            ["CharacterSummary.Detailed.Tab.HealthAnatomy"] = "건강·신체",
            ["CharacterSummary.Detailed.Tab.Modifiers"] = "상태·보정",
            ["CharacterSummary.Detailed.Activity.Movement"] = "이동",
            ["CharacterSummary.Detailed.Activity.Accuracy"] = "명중",
            ["CharacterSummary.Detailed.Activity.Evasion"] = "회피",
            ["CharacterSummary.Detailed.Activity.Work"] = "작업 속도",
            ["CharacterSummary.Detailed.Activity.Carry"] = "운반",
            ["CharacterSummary.Detailed.Activity.MeleePower"] = "근접 위력",
            ["CharacterSummary.Detailed.Activity.Treatment"] = "치료",
            ["CharacterSummary.Detailed.Activity.Recovery"] = "회복",
            ["CharacterSummary.Detailed.Activity.Overclock"] = "오버클럭",
            ["CharacterSummary.Detailed.Axis.Awareness"] = "인지",
            ["CharacterSummary.Detailed.Axis.Handling"] = "조작",
            ["CharacterSummary.Detailed.Axis.Locomotion"] = "기동",
            ["CharacterSummary.Detailed.Axis.Sustain"] = "지속",
            ["CharacterSummary.Detailed.Axis.Recovery"] = "회복",

            ["CharacterSummary.Combat.Notice.LoadoutCombat"] = "{0}: 전투 로드아웃으로 전환",
            ["CharacterSummary.Combat.Notice.LoadoutPeace"] = "{0}: 평시 로드아웃으로 전환",
            ["CharacterSummary.Combat.Notice.LoadoutUnavailable"] = "현재 장비 조합으로는 해당 로드아웃을 사용할 수 없습니다.",
            ["CharacterSummary.Combat.Notice.NoCarriedWeapon"] = "교체할 소지 무기가 없습니다.",
            ["CharacterSummary.Combat.Notice.WeaponSwitched"] = "{0}: 무기 교체",
            ["CharacterSummary.Combat.Notice.NoActiveWeaponToReload"] = "재장전할 활성 무기가 없습니다.",
            ["CharacterSummary.Combat.Notice.Reloaded"] = "{0}: 탄약 {1}발 재장전",
            ["CharacterSummary.Combat.Notice.ReloadUnavailable"] = "맞는 탄약이 없거나 이미 장전되어 있습니다.",
            ["CharacterSummary.Combat.Notice.NoAvailableFireMode"] = "사용 가능한 사격 모드가 없습니다.",
            ["CharacterSummary.Combat.Notice.FireModeSelected"] = "{0}: {1} 사격",
            ["CharacterSummary.Combat.Notice.HoldFire"] = "{0}: 사격 중지",
            ["CharacterSummary.Combat.Notice.AllowFire"] = "{0}: 사격 허용",
            ["CharacterSummary.Combat.Notice.RepairInfoUnavailable"] = "장비 수리 정보를 불러올 수 없습니다.",
            ["CharacterSummary.Combat.Notice.NoRepairCandidate"] = "수리가 필요한 방어구나 방패가 없습니다.",
            ["CharacterSummary.Combat.Notice.CombatInfoUnavailable"] = "전투 장비 정보를 불러올 수 없습니다.",

            ["CharacterSummary.Combat.Summary.Empty"] = "전투 정보가 없습니다.",
            ["CharacterSummary.Combat.Summary.Ability.Title"] = "전투 능력",
            ["CharacterSummary.Combat.Summary.Ability.Stats"] = "근접 {0} · 사격 {1} · 회피 {2} · 민첩 {3} · 힘 {4}",
            ["CharacterSummary.Combat.Summary.Ability.RangedHit"] = "기본 사격 명중 {0:0.#}%  (45 + 사격×2.5 + 민첩×1)",
            ["CharacterSummary.Combat.Summary.Ability.MeleeHit"] = "기본 근접 명중 {0:0.#}%  (대상 회피 전)",
            ["CharacterSummary.Combat.Summary.Ability.Evasion"] = "기본 회피 {0:0.#}%  (2 + 회피×1 + 이동×0.3)",
            ["CharacterSummary.Combat.Summary.Body.Title"] = "신체 상태",
            ["CharacterSummary.Combat.Summary.Body.Functions"] = "의식 {0:0}% · 조작 {1:0}% · 이동 {2:0}%",
            ["CharacterSummary.Combat.Summary.Body.Damage"] = "혈액 손실 {0:0.#}% · 제압 {1:0.#}%{2}",
            ["CharacterSummary.Combat.Summary.Body.DownedSuffix"] = " · 쓰러짐",
            ["CharacterSummary.Combat.Summary.Body.BleedingSuffix"] = " · 출혈 {0:0.##}/s",
            ["CharacterSummary.Combat.Summary.Equipment.Title"] = "장비와 탄약",
            ["CharacterSummary.Combat.Summary.Equipment.Loadout"] = "로드아웃  {0}",
            ["CharacterSummary.Combat.Common.Peace"] = "평시",
            ["CharacterSummary.Combat.Summary.Equipment.LoadedSuffix"] = " · 장전 {0}/{1}",
            ["CharacterSummary.Combat.Summary.Equipment.ActiveWeapon"] = "활성 무기  {0} [{1}] · 최대 {2}칸{3}",
            ["CharacterSummary.Combat.Summary.Equipment.MaterialStats"] = "재질 성능  피해 ×{0:0.##} · 관통 ×{1:0.##} · {2:0.##}kg",
            ["CharacterSummary.Combat.Summary.Equipment.FireMode"] = "사격 모드  {0}{1}",
            ["CharacterSummary.Combat.Summary.Equipment.HoldFireSuffix"] = " · 사격 중지",
            ["CharacterSummary.Combat.Summary.Equipment.Unarmed"] = "활성 무기  맨손",
            ["CharacterSummary.Combat.Summary.Equipment.Armor"] = "방어구",
            ["CharacterSummary.Combat.Summary.Equipment.Shield"] = "방패",
            ["CharacterSummary.Combat.Summary.Equipment.ListNone"] = "{0}  없음",
            ["CharacterSummary.Combat.Summary.Equipment.ListDerived"] = "{0} [{1}] · 내구 {2:0}% · 방어 ×{3:0.##} · {4:0.##}kg",
            ["CharacterSummary.Combat.Summary.Equipment.ListBasic"] = "{0} [{1}] {2:0}%",
            ["CharacterSummary.Combat.Summary.Maintenance.Title"] = "장비 정비",
            ["CharacterSummary.Combat.Summary.Maintenance.Policy"] = "정책  {0}{1}",
            ["CharacterSummary.Combat.Common.Standard"] = "표준",
            ["CharacterSummary.Combat.Summary.Maintenance.Automatic"] = " · {0:P0}에 보내고 {1:P0}에 복귀",
            ["CharacterSummary.Combat.Summary.Maintenance.AutomaticOff"] = " · 자동 수리 꺼짐",
            ["CharacterSummary.Combat.Summary.Maintenance.RepairActive"] = "수리 상태  {0} · {1:P0} · {2} ×{3} · 작업량 {4:0.#}/{5:0.#}",
            ["CharacterSummary.Combat.Summary.Maintenance.RepairNone"] = "수리 상태  대기 없음",
            ["CharacterSummary.Combat.Summary.Ammunition"] = "탄약  화살 {0} · 볼트 {1}",
            ["CharacterSummary.Combat.Summary.Weight"] = "무게  {0:0.#}kg / 허용 {1:0.#}kg",

            ["CharacterSummary.Combat.Button.CombatLoadout"] = "전투 장비",
            ["CharacterSummary.Combat.Button.PeaceLoadout"] = "평시 장비",
            ["CharacterSummary.Combat.Button.SwitchWeapon"] = "무기 교체",
            ["CharacterSummary.Combat.Button.ReloadCount"] = "재장전 {0}/{1}",
            ["CharacterSummary.Combat.Button.Reload"] = "재장전",
            ["CharacterSummary.Combat.Button.HoldFire"] = "사격 중지",
            ["CharacterSummary.Combat.Button.AllowFire"] = "사격 허용",
            ["CharacterSummary.Combat.Button.RequestRepair"] = "수리 요청",

            ["CharacterSummary.Combat.FireMode.Aimed"] = "조준",
            ["CharacterSummary.Combat.FireMode.Rapid"] = "속사",
            ["CharacterSummary.Combat.FireMode.Suppressive"] = "제압",
            ["CharacterSummary.Combat.RepairState.PendingCombatEnd"] = "교전 종료 대기",
            ["CharacterSummary.Combat.RepairState.WaitingForDelivery"] = "운반 대기",
            ["CharacterSummary.Combat.RepairState.Ready"] = "수리 준비",
            ["CharacterSummary.Combat.RepairState.InProgress"] = "수리 중",
            ["CharacterSummary.Combat.RepairState.Completed"] = "완료",
            ["CharacterSummary.Combat.RepairState.Cancelled"] = "취소",
            ["CharacterSummary.Combat.Quality.Awful"] = "조악",
            ["CharacterSummary.Combat.Quality.Poor"] = "형편없음",
            ["CharacterSummary.Combat.Quality.Normal"] = "보통",
            ["CharacterSummary.Combat.Quality.Good"] = "좋음",
            ["CharacterSummary.Combat.Quality.Excellent"] = "훌륭",
            ["CharacterSummary.Combat.Quality.Masterwork"] = "걸작",
            ["CharacterSummary.Combat.Quality.Legendary"] = "전설",
            ["CharacterSummary.Combat.BodyPart.Head"] = "머리",
            ["CharacterSummary.Combat.BodyPart.Torso"] = "몸통",
            ["CharacterSummary.Combat.BodyPart.LeftArm"] = "왼팔",
            ["CharacterSummary.Combat.BodyPart.RightArm"] = "오른팔",
            ["CharacterSummary.Combat.BodyPart.LeftLeg"] = "왼다리",
            ["CharacterSummary.Combat.BodyPart.RightLeg"] = "오른다리",
            ["CharacterSummary.Combat.Failure.CharacterRequired"] = "전투 장비를 변경할 캐릭터가 필요합니다.",
            ["CharacterSummary.Combat.Failure.WeaponNotAssigned"] = "해당 무기는 현재 로드아웃에 배정되지 않았습니다.",
            ["CharacterSummary.Combat.Failure.InsufficientHands"] = "이 장비 조합을 사용하기에는 손이 부족합니다.",
            ["CharacterSummary.Combat.Failure.NoActiveRangedWeapon"] = "활성 원거리 무기가 없습니다.",
            ["CharacterSummary.Combat.Failure.UnsupportedFireMode"] = "활성 무기가 해당 사격 모드를 지원하지 않습니다.",
            ["CharacterSummary.Combat.Failure.Unknown"] = "전투 장비 명령 실패({0})",

            ["CharacterSummary.Health.Deprivation.Title"] = "결핍 부담",
            ["CharacterSummary.Health.Deprivation.InfectionBurden"] = "감염 부담  {0:0.#} / 100",
            ["CharacterSummary.Health.Deprivation.BreakdownChance"] = "붕괴 확률  {0:0.#}% / 5초",
            ["CharacterSummary.Health.Deprivation.CurrentBreakdown"] = "현재 붕괴  {0}",
            ["CharacterSummary.Health.Deprivation.Cause"] = "원인  {0}",
            ["CharacterSummary.Health.Deprivation.Target"] = "목표  {0}",
            ["CharacterSummary.Health.Deprivation.SuppressionResistance"] = "제압 저항  {0:0.#}",
            ["CharacterSummary.Health.Taboo.Title"] = "최근 금기 행동",
            ["CharacterSummary.Health.Taboo.NoRecords"] = "기록 없음",
            ["CharacterSummary.Health.Surgery.AutomaticOn"] = "응급 수술: 켬",
            ["CharacterSummary.Health.Surgery.AutomaticOff"] = "응급 수술: 끔",
            ["CharacterSummary.Health.Consumables.Title"] = "식단·약물",
            ["CharacterSummary.Health.Consumables.DietPolicy"] = "식단 정책  {0}",
            ["CharacterSummary.Health.Consumables.SubstanceRow"] = "{0}  {1} · 내성 {2:0.#} · 중독 {3:0.#} · 금단 {4:0.#}{5}",
            ["CharacterSummary.Health.Consumables.ActiveEffectSuffix"] = " · 효과 {0:0}s",
            ["CharacterSummary.Health.Button.DietCurrent"] = "식단: {0}",
            ["CharacterSummary.Health.Button.DietPolicy"] = "식단 정책",
            ["CharacterSummary.Health.Button.NoSubstance"] = "약물 없음",
            ["CharacterSummary.Health.Button.NoPolicy"] = "정책 없음",
            ["CharacterSummary.Health.DietPolicy.Vegan"] = "비건",
            ["CharacterSummary.Health.DietPolicy.Vegetarian"] = "채식",
            ["CharacterSummary.Health.DietPolicy.CarnivorePreferred"] = "육식 선호",
            ["CharacterSummary.Health.DietPolicy.StrictTaboo"] = "금기 엄수",
            ["CharacterSummary.Health.DietPolicy.Free"] = "자유식",
            ["CharacterSummary.Health.SubstancePolicy.MedicalOnly"] = "의료 전용",
            ["CharacterSummary.Health.SubstancePolicy.CombatOnly"] = "전투 시",
            ["CharacterSummary.Health.SubstancePolicy.MoodThreshold"] = "기분 임계",
            ["CharacterSummary.Health.SubstancePolicy.Scheduled"] = "예약 복용",
            ["CharacterSummary.Health.SubstancePolicy.Forbidden"] = "금지",

            ["CharacterSummary.Health.Anatomy.Title"] = "신체·장기 · {0}",
            ["CharacterSummary.Health.Anatomy.PrimaryFunctions"] = "의식 {0} · 시야 {1} · 호흡 {2}",
            ["CharacterSummary.Health.Anatomy.SecondaryFunctions"] = "소화 {0} · 여과 {1} · 조작 {2} · 이동 {3}",
            ["CharacterSummary.Health.Anatomy.Missing"] = "결손",
            ["CharacterSummary.Health.Anatomy.BleedingSuffix"] = " · 출혈 {0:0.##}/초",
            ["CharacterSummary.Health.Anatomy.InfectionSuffix"] = " · 감염 {0:0.#}",
            ["CharacterSummary.Health.Anatomy.RejectionSuffix"] = " · 거부 {0:0.#}",
            ["CharacterSummary.Health.Anatomy.MutationSuffix"] = " · 변이 {0:0.#}",
            ["CharacterSummary.Health.Anatomy.PartKind.NaturalOrgan"] = "자연 장기",
            ["CharacterSummary.Health.Anatomy.PartKind.Prosthetic"] = "의수·의족",
            ["CharacterSummary.Health.Anatomy.PartKind.Implant"] = "임플란트",
            ["CharacterSummary.Health.Anatomy.PartKind.ArcaneGraft"] = "비전 이식체",
            ["CharacterSummary.Health.Treatment.NoQueue"] = "수술 대기열 없음",
            ["CharacterSummary.Health.Treatment.Queue"] = "수술 대기 · {0} · {1} · {2}",
            ["CharacterSummary.Health.Treatment.Doctor"] = "집도의 {0}",

            ["CharacterSummary.Status.SpeciesUnknown"] = "종족 미상",
            ["CharacterSummary.Status.Profile"] = "Lv.{0} · {1} · {2} · {3}",
            ["CharacterSummary.Status.Health.Injury"] = "{0}/{1} · 부상 {2}%",
            ["CharacterSummary.Status.Health.Normal"] = "{0}/{1}",
            ["CharacterSummary.Status.Carry.Unavailable"] = "소지품 없음\n운반 한도 정보 없음",
            ["CharacterSummary.Status.Carry.Weight"] = "소지 무게 {0} / 기본 {1} / 최대 {2}",
            ["CharacterSummary.Status.Carry.Overloaded"] = "과적 중 · 이동 속도 {0}%",
            ["CharacterSummary.Status.Carry.Normal"] = "과적 없음 · 이동 속도 100%",
            ["CharacterSummary.Status.Carry.NoItems"] = "소지 아이템 없음",
            ["CharacterSummary.Status.Temperature.Stable"] = "체온 안정",
            ["CharacterSummary.Status.Temperature.Caution"] = "체온 주의",
            ["CharacterSummary.Status.Temperature.Danger"] = "체온 위험",
            ["CharacterSummary.Status.Survival.IssueSuffix"] = " 외 {0}건",
            ["CharacterSummary.Status.Survival.Row"] = "생존 상태 {0}{1} · {2} · {3} · {4}"
        };

    private static readonly IReadOnlyDictionary<string, string> English =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["CharacterSummary.Deprivation.Hunger"] = "Starvation",
            ["CharacterSummary.Deprivation.Thirst"] = "Dehydration",
            ["CharacterSummary.Deprivation.Bladder"] = "Bladder damage",
            ["CharacterSummary.Deprivation.Contamination"] = "Contamination",
            ["CharacterSummary.Deprivation.Exhaustion"] = "Exhaustion",
            ["CharacterSummary.Deprivation.MentalInstability"] = "Mental instability",

            ["CharacterSummary.Burden.Critical"] = "Breakdown imminent",
            ["CharacterSummary.Burden.Danger"] = "Danger",
            ["CharacterSummary.Burden.Unhealthy"] = "Health impaired",
            ["CharacterSummary.Burden.Accumulating"] = "Accumulating",
            ["CharacterSummary.Burden.Stable"] = "Stable",

            ["CharacterSummary.Breakdown.DesperateRelief"] = "Loss of bowel control",
            ["CharacterSummary.Breakdown.DesperateDrink"] = "Desperate thirst",
            ["CharacterSummary.Breakdown.DesperateEat"] = "Taboo consumption",
            ["CharacterSummary.Breakdown.Collapse"] = "Collapse",
            ["CharacterSummary.Breakdown.ViolentImpulse"] = "Violent impulse",
            ["CharacterSummary.Common.None"] = "None",

            ["CharacterSummary.Mood.NoFactors"] = "No factors are currently changing this character's mood.",
            ["CharacterSummary.Mood.NeedHeading"] = "Need effects",
            ["CharacterSummary.Mood.InteractionHeading"] = "Recent experiences",
            ["CharacterSummary.Mood.FactorRow"] = "- {0}  {1}",
            ["CharacterSummary.Mood.InteractionRow"] = "- {0}  {1}  · {2}",
            ["CharacterSummary.Log.Empty"] = "No records yet.",
            ["CharacterSummary.Log.Entry"] = "- {0}",

            ["CharacterSummary.Health.Thirsty"] = "Thirsty",
            ["CharacterSummary.Health.Hungry"] = "Hungry",
            ["CharacterSummary.Health.Exposed"] = "Exposed",
            ["CharacterSummary.Health.Sick"] = "Sick",
            ["CharacterSummary.Health.Infected"] = "Infected",
            ["CharacterSummary.Health.Recovering"] = "Recovering",
            ["CharacterSummary.Health.Healthy"] = "Healthy",

            ["CharacterSummary.Time.Seconds"] = "{0}s",
            ["CharacterSummary.Time.MinutesSeconds"] = "{0}m {1}s",
            ["CharacterSummary.Time.Minutes"] = "{0}m",
            ["CharacterSummary.Role.Owner"] = "Owner",
            ["CharacterSummary.Role.Regular"] = "Regular",

            ["CharacterSummary.Lifecycle.SpawningOutside"] = "Preparing to enter",
            ["CharacterSummary.Lifecycle.EnteringDungeon"] = "Entering",
            ["CharacterSummary.Lifecycle.Active"] = "Active",
            ["CharacterSummary.Lifecycle.ExitingDungeon"] = "Exiting",
            ["CharacterSummary.Lifecycle.OnExpedition"] = "On expedition",
            ["CharacterSummary.Lifecycle.PreparingExpedition"] = "Preparing expedition",
            ["CharacterSummary.Lifecycle.DepartingExpedition"] = "Departing",
            ["CharacterSummary.Lifecycle.ReturningExpedition"] = "Returning",
            ["CharacterSummary.Lifecycle.Downed"] = "Downed",
            ["CharacterSummary.Lifecycle.Despawned"] = "Departed",
            ["CharacterSummary.Lifecycle.Waiting"] = "Waiting",

            ["CharacterSummary.Captivity.AwaitingCapture"] = "Awaiting capture",
            ["CharacterSummary.Captivity.Stabilizing"] = "Stabilizing on site",
            ["CharacterSummary.Captivity.AwaitingEscort"] = "Awaiting escort",
            ["CharacterSummary.Captivity.Escorting"] = "Escorting",
            ["CharacterSummary.Captivity.Confined"] = "Confined",
            ["CharacterSummary.Captivity.Labor"] = "Assigned to labor",
            ["CharacterSummary.Captivity.Interaction"] = "Management interaction",
            ["CharacterSummary.Captivity.Performer"] = "Performing",
            ["CharacterSummary.Captivity.EscapeAttempt"] = "Escape attempt",
            ["CharacterSummary.Captivity.Ransom"] = "Ransom negotiation",
            ["CharacterSummary.Weight.Kilograms"] = "{0:0.#} kg",

            ["CharacterSummary.Action.Close"] = "Close",
            ["CharacterSummary.Tab.Status"] = "Status",
            ["CharacterSummary.Tab.Health"] = "Health",
            ["CharacterSummary.Tab.Combat"] = "Combat",
            ["CharacterSummary.Tab.Growth"] = "Growth",
            ["CharacterSummary.Tab.Mood"] = "Mood",
            ["CharacterSummary.Tab.Records"] = "Records",
            ["CharacterSummary.Section.Status"] = "Status",
            ["CharacterSummary.Section.Needs"] = "Needs",
            ["CharacterSummary.Section.Level"] = "Level",
            ["CharacterSummary.Section.SkillSlots"] = "Skill slots and candidates",
            ["CharacterSummary.Section.Mood"] = "Mood",
            ["CharacterSummary.Section.MoodFactors"] = "Mood factors",
            ["CharacterSummary.Meter.Health"] = "Health",
            ["CharacterSummary.Meter.Satiety"] = "Satiety",
            ["CharacterSummary.Meter.Thirst"] = "Thirst",
            ["CharacterSummary.Meter.Fun"] = "Fun",
            ["CharacterSummary.Meter.Rest"] = "Rest",
            ["CharacterSummary.Meter.Excretion"] = "Excretion",
            ["CharacterSummary.Meter.Hygiene"] = "Hygiene",
            ["CharacterSummary.Meter.Experience"] = "Experience",
            ["CharacterSummary.Meter.CurrentMood"] = "Current mood",
            ["CharacterSummary.Carry.Empty"] = "No carried items",
            ["CharacterSummary.Health.Action.Captivity"] = "Capture command",
            ["CharacterSummary.Health.Action.DietFree"] = "Diet: unrestricted",
            ["CharacterSummary.Health.Action.ScheduleSurgery"] = "Schedule surgery",
            ["CharacterSummary.Health.Action.AutomaticEmergencyOn"] = "Emergency surgery: on",
            ["CharacterSummary.Health.Action.SelectSubstance"] = "Select substance",
            ["CharacterSummary.Health.Action.SubstanceProhibited"] = "Prohibited",
            ["CharacterSummary.Health.Empty"] = "No deprivation-health information.",
            ["CharacterSummary.Combat.Action.Loadout"] = "Combat equipment",
            ["CharacterSummary.Combat.Action.SwitchWeapon"] = "Switch weapon",
            ["CharacterSummary.Combat.Action.Reload"] = "Reload",
            ["CharacterSummary.Combat.Action.Aimed"] = "Aimed",
            ["CharacterSummary.Combat.Action.FireAllowed"] = "Fire allowed",
            ["CharacterSummary.Combat.Action.Repair"] = "Request repair",
            ["CharacterSummary.Combat.Empty"] = "No combat information.",
            ["CharacterSummary.Mood.DefaultSummary"] = "Calm · base 50 · adjustment +0",
            ["CharacterSummary.AI.Empty"] = "No AI decision records yet.",
            ["CharacterSummary.Detailed.Title"] = "Detailed stats",
            ["CharacterSummary.Detailed.Empty"] = "No information to display.",

            ["CharacterSummary.Detailed.Selection.None"] = "No selection",
            ["CharacterSummary.Detailed.Common.Unknown"] = "Unknown",
            ["CharacterSummary.Detailed.Summary.Health"] = "Health",
            ["CharacterSummary.Detailed.Summary.Dead"] = "Dead",
            ["CharacterSummary.Detailed.Summary.CombatVitality"] = "Current combat vitality",
            ["CharacterSummary.Detailed.Summary.Level"] = "Level",
            ["CharacterSummary.Detailed.Summary.LevelDetail"] = "Growth and part effects are separated in the tabs below",
            ["CharacterSummary.Detailed.Summary.ActualMovement"] = "Actual movement",
            ["CharacterSummary.Detailed.Summary.MovementDetail"] = "Includes base stats, health, parts, and environment",
            ["CharacterSummary.Detailed.Summary.Species"] = "Species",
            ["CharacterSummary.Detailed.Summary.DungeonOwner"] = "Dungeon owner",
            ["CharacterSummary.Detailed.BaseStats.Breakdown"] = "Base {0:+#;-#;0} · species/traits {1:+#;-#;0} · growth {2:+#;-#;0} · conditional {3:+#;-#;0}",
            ["CharacterSummary.Detailed.Work.CurrentMultiplier"] = "Current work multiplier including stats, health, parts, mood, and environment",

            ["CharacterSummary.Detailed.Combat.PrimaryWeapon"] = "Primary weapon",
            ["CharacterSummary.Detailed.Combat.WeaponDetail"] = "Damage {0:0.#} × material {1:0.##} · penetration {2:0.#} × {3:0.##}{4}",
            ["CharacterSummary.Detailed.Combat.AmmoSuffix"] = " · ammo {0}/{1}",
            ["CharacterSummary.Detailed.Combat.Armor"] = "Armor",
            ["CharacterSummary.Detailed.Combat.Durability"] = "Durability {0:0}%",
            ["CharacterSummary.Detailed.Combat.ArmorDetail"] = "Protected parts {0} · slash {1:0.#} · pierce {2:0.#} · blunt {3:0.#}",
            ["CharacterSummary.Detailed.Combat.Shield"] = "Shield",
            ["CharacterSummary.Detailed.Combat.ShieldDetail"] = "Frontal block {0:0.#}%",
            ["CharacterSummary.Detailed.Combat.DefensiveEquipment"] = "Defensive equipment",
            ["CharacterSummary.Detailed.Combat.EmptySlots"] = "Equipment slots are empty.",

            ["CharacterSummary.Detailed.Anatomy.Redacted"] = "Internal parts and efficiency are unavailable.",
            ["CharacterSummary.Detailed.Anatomy.Value"] = "{0} {1:0}% · natural baseline {2:0}%",
            ["CharacterSummary.Detailed.Anatomy.Detail"] = "Part efficiency {0:0.##} × operation {1:0.##} + module {2:+0.##;-0.##;0} · {3}",
            ["CharacterSummary.Detailed.Anatomy.Status"] = "Body status",
            ["CharacterSummary.Detailed.Anatomy.Normal"] = "Normal",
            ["CharacterSummary.Detailed.Anatomy.NoAbnormalParts"] = "No abnormal parts.",
            ["CharacterSummary.Detailed.Modifier.Capped"] = "Raw ×{0:0.##} · cap ×{1:0.##} applied",
            ["CharacterSummary.Detailed.Modifier.Uncapped"] = "Raw ×{0:0.##} · cap ×{1:0.##}",
            ["CharacterSummary.Detailed.Axis.Label"] = "Calculation axis · {0}",
            ["CharacterSummary.Detailed.Axis.Detail"] = "Internal value summed from body-part contributions",
            ["CharacterSummary.Detailed.Condition.Missing"] = "Missing presentation ({0})",

            ["CharacterSummary.Detailed.Recovery.Natural"] = "Natural recovery",
            ["CharacterSummary.Detailed.Recovery.AssistedRegeneration"] = "Assisted regeneration",
            ["CharacterSummary.Detailed.Recovery.MaintenanceOnly"] = "Maintenance required",
            ["CharacterSummary.Detailed.Recovery.ReplaceOnFailure"] = "Replace on failure",
            ["CharacterSummary.Detailed.Tab.Summary"] = "Summary",
            ["CharacterSummary.Detailed.Tab.BaseStats"] = "Base stats",
            ["CharacterSummary.Detailed.Tab.Work"] = "Work",
            ["CharacterSummary.Detailed.Tab.CombatEquipment"] = "Combat & equipment",
            ["CharacterSummary.Detailed.Tab.HealthAnatomy"] = "Health & anatomy",
            ["CharacterSummary.Detailed.Tab.Modifiers"] = "Status & modifiers",
            ["CharacterSummary.Detailed.Activity.Movement"] = "Movement",
            ["CharacterSummary.Detailed.Activity.Accuracy"] = "Accuracy",
            ["CharacterSummary.Detailed.Activity.Evasion"] = "Evasion",
            ["CharacterSummary.Detailed.Activity.Work"] = "Work speed",
            ["CharacterSummary.Detailed.Activity.Carry"] = "Carry",
            ["CharacterSummary.Detailed.Activity.MeleePower"] = "Melee power",
            ["CharacterSummary.Detailed.Activity.Treatment"] = "Treatment",
            ["CharacterSummary.Detailed.Activity.Recovery"] = "Recovery",
            ["CharacterSummary.Detailed.Activity.Overclock"] = "Overclock",
            ["CharacterSummary.Detailed.Axis.Awareness"] = "Awareness",
            ["CharacterSummary.Detailed.Axis.Handling"] = "Handling",
            ["CharacterSummary.Detailed.Axis.Locomotion"] = "Locomotion",
            ["CharacterSummary.Detailed.Axis.Sustain"] = "Sustain",
            ["CharacterSummary.Detailed.Axis.Recovery"] = "Recovery",

            ["CharacterSummary.Combat.Notice.LoadoutCombat"] = "{0}: switched to combat loadout",
            ["CharacterSummary.Combat.Notice.LoadoutPeace"] = "{0}: switched to peacetime loadout",
            ["CharacterSummary.Combat.Notice.LoadoutUnavailable"] = "The current equipment combination cannot use that loadout.",
            ["CharacterSummary.Combat.Notice.NoCarriedWeapon"] = "No carried weapon is available to switch to.",
            ["CharacterSummary.Combat.Notice.WeaponSwitched"] = "{0}: weapon switched",
            ["CharacterSummary.Combat.Notice.NoActiveWeaponToReload"] = "There is no active weapon to reload.",
            ["CharacterSummary.Combat.Notice.Reloaded"] = "{0}: reloaded {1} rounds",
            ["CharacterSummary.Combat.Notice.ReloadUnavailable"] = "No compatible ammunition is available, or the weapon is already loaded.",
            ["CharacterSummary.Combat.Notice.NoAvailableFireMode"] = "No fire mode is available.",
            ["CharacterSummary.Combat.Notice.FireModeSelected"] = "{0}: {1} fire selected",
            ["CharacterSummary.Combat.Notice.HoldFire"] = "{0}: holding fire",
            ["CharacterSummary.Combat.Notice.AllowFire"] = "{0}: fire allowed",
            ["CharacterSummary.Combat.Notice.RepairInfoUnavailable"] = "Equipment repair information is unavailable.",
            ["CharacterSummary.Combat.Notice.NoRepairCandidate"] = "No armor or shield requires repair.",
            ["CharacterSummary.Combat.Notice.CombatInfoUnavailable"] = "Combat equipment information is unavailable.",

            ["CharacterSummary.Combat.Summary.Empty"] = "No combat information is available.",
            ["CharacterSummary.Combat.Summary.Ability.Title"] = "Combat ability",
            ["CharacterSummary.Combat.Summary.Ability.Stats"] = "Melee {0} · shooting {1} · evasion {2} · dexterity {3} · strength {4}",
            ["CharacterSummary.Combat.Summary.Ability.RangedHit"] = "Base ranged hit {0:0.#}%  (45 + shooting×2.5 + dexterity×1)",
            ["CharacterSummary.Combat.Summary.Ability.MeleeHit"] = "Base melee hit {0:0.#}%  (before target evasion)",
            ["CharacterSummary.Combat.Summary.Ability.Evasion"] = "Base evasion {0:0.#}%  (2 + evasion×1 + movement×0.3)",
            ["CharacterSummary.Combat.Summary.Body.Title"] = "Body status",
            ["CharacterSummary.Combat.Summary.Body.Functions"] = "Consciousness {0:0}% · manipulation {1:0}% · mobility {2:0}%",
            ["CharacterSummary.Combat.Summary.Body.Damage"] = "Blood loss {0:0.#}% · suppression {1:0.#}%{2}",
            ["CharacterSummary.Combat.Summary.Body.DownedSuffix"] = " · downed",
            ["CharacterSummary.Combat.Summary.Body.BleedingSuffix"] = " · bleeding {0:0.##}/s",
            ["CharacterSummary.Combat.Summary.Equipment.Title"] = "Equipment and ammunition",
            ["CharacterSummary.Combat.Summary.Equipment.Loadout"] = "Loadout  {0}",
            ["CharacterSummary.Combat.Common.Peace"] = "Peacetime",
            ["CharacterSummary.Combat.Summary.Equipment.LoadedSuffix"] = " · loaded {0}/{1}",
            ["CharacterSummary.Combat.Summary.Equipment.ActiveWeapon"] = "Active weapon  {0} [{1}] · max range {2} tiles{3}",
            ["CharacterSummary.Combat.Summary.Equipment.MaterialStats"] = "Material performance  damage ×{0:0.##} · penetration ×{1:0.##} · {2:0.##}kg",
            ["CharacterSummary.Combat.Summary.Equipment.FireMode"] = "Fire mode  {0}{1}",
            ["CharacterSummary.Combat.Summary.Equipment.HoldFireSuffix"] = " · holding fire",
            ["CharacterSummary.Combat.Summary.Equipment.Unarmed"] = "Active weapon  unarmed",
            ["CharacterSummary.Combat.Summary.Equipment.Armor"] = "Armor",
            ["CharacterSummary.Combat.Summary.Equipment.Shield"] = "Shield",
            ["CharacterSummary.Combat.Summary.Equipment.ListNone"] = "{0}  none",
            ["CharacterSummary.Combat.Summary.Equipment.ListDerived"] = "{0} [{1}] · durability {2:0}% · defense ×{3:0.##} · {4:0.##}kg",
            ["CharacterSummary.Combat.Summary.Equipment.ListBasic"] = "{0} [{1}] {2:0}%",
            ["CharacterSummary.Combat.Summary.Maintenance.Title"] = "Equipment maintenance",
            ["CharacterSummary.Combat.Summary.Maintenance.Policy"] = "Policy  {0}{1}",
            ["CharacterSummary.Combat.Common.Standard"] = "Standard",
            ["CharacterSummary.Combat.Summary.Maintenance.Automatic"] = " · send at {0:P0} and return at {1:P0}",
            ["CharacterSummary.Combat.Summary.Maintenance.AutomaticOff"] = " · automatic repair off",
            ["CharacterSummary.Combat.Summary.Maintenance.RepairActive"] = "Repair status  {0} · {1:P0} · {2} ×{3} · work {4:0.#}/{5:0.#}",
            ["CharacterSummary.Combat.Summary.Maintenance.RepairNone"] = "Repair status  no pending order",
            ["CharacterSummary.Combat.Summary.Ammunition"] = "Ammunition  arrows {0} · bolts {1}",
            ["CharacterSummary.Combat.Summary.Weight"] = "Weight  {0:0.#}kg / allowed {1:0.#}kg",

            ["CharacterSummary.Combat.Button.CombatLoadout"] = "Combat equipment",
            ["CharacterSummary.Combat.Button.PeaceLoadout"] = "Peacetime equipment",
            ["CharacterSummary.Combat.Button.SwitchWeapon"] = "Switch weapon",
            ["CharacterSummary.Combat.Button.ReloadCount"] = "Reload {0}/{1}",
            ["CharacterSummary.Combat.Button.Reload"] = "Reload",
            ["CharacterSummary.Combat.Button.HoldFire"] = "Hold fire",
            ["CharacterSummary.Combat.Button.AllowFire"] = "Allow fire",
            ["CharacterSummary.Combat.Button.RequestRepair"] = "Request repair",

            ["CharacterSummary.Combat.FireMode.Aimed"] = "Aimed",
            ["CharacterSummary.Combat.FireMode.Rapid"] = "Rapid",
            ["CharacterSummary.Combat.FireMode.Suppressive"] = "Suppressive",
            ["CharacterSummary.Combat.RepairState.PendingCombatEnd"] = "Waiting for combat to end",
            ["CharacterSummary.Combat.RepairState.WaitingForDelivery"] = "Waiting for delivery",
            ["CharacterSummary.Combat.RepairState.Ready"] = "Ready for repair",
            ["CharacterSummary.Combat.RepairState.InProgress"] = "Repairing",
            ["CharacterSummary.Combat.RepairState.Completed"] = "Completed",
            ["CharacterSummary.Combat.RepairState.Cancelled"] = "Cancelled",
            ["CharacterSummary.Combat.Quality.Awful"] = "Awful",
            ["CharacterSummary.Combat.Quality.Poor"] = "Poor",
            ["CharacterSummary.Combat.Quality.Normal"] = "Normal",
            ["CharacterSummary.Combat.Quality.Good"] = "Good",
            ["CharacterSummary.Combat.Quality.Excellent"] = "Excellent",
            ["CharacterSummary.Combat.Quality.Masterwork"] = "Masterwork",
            ["CharacterSummary.Combat.Quality.Legendary"] = "Legendary",
            ["CharacterSummary.Combat.BodyPart.Head"] = "Head",
            ["CharacterSummary.Combat.BodyPart.Torso"] = "Torso",
            ["CharacterSummary.Combat.BodyPart.LeftArm"] = "Left arm",
            ["CharacterSummary.Combat.BodyPart.RightArm"] = "Right arm",
            ["CharacterSummary.Combat.BodyPart.LeftLeg"] = "Left leg",
            ["CharacterSummary.Combat.BodyPart.RightLeg"] = "Right leg",
            ["CharacterSummary.Combat.Failure.CharacterRequired"] = "A character is required to change combat equipment.",
            ["CharacterSummary.Combat.Failure.WeaponNotAssigned"] = "That weapon is not assigned to the current loadout.",
            ["CharacterSummary.Combat.Failure.InsufficientHands"] = "There are not enough hands for this equipment combination.",
            ["CharacterSummary.Combat.Failure.NoActiveRangedWeapon"] = "There is no active ranged weapon.",
            ["CharacterSummary.Combat.Failure.UnsupportedFireMode"] = "The active weapon does not support that fire mode.",
            ["CharacterSummary.Combat.Failure.Unknown"] = "Combat equipment command failed ({0})",

            ["CharacterSummary.Health.Deprivation.Title"] = "Deprivation burden",
            ["CharacterSummary.Health.Deprivation.InfectionBurden"] = "Infection burden  {0:0.#} / 100",
            ["CharacterSummary.Health.Deprivation.BreakdownChance"] = "Breakdown chance  {0:0.#}% / 5 seconds",
            ["CharacterSummary.Health.Deprivation.CurrentBreakdown"] = "Current breakdown  {0}",
            ["CharacterSummary.Health.Deprivation.Cause"] = "Cause  {0}",
            ["CharacterSummary.Health.Deprivation.Target"] = "Target  {0}",
            ["CharacterSummary.Health.Deprivation.SuppressionResistance"] = "Suppression resistance  {0:0.#}",
            ["CharacterSummary.Health.Taboo.Title"] = "Recent taboo actions",
            ["CharacterSummary.Health.Taboo.NoRecords"] = "No records",
            ["CharacterSummary.Health.Surgery.AutomaticOn"] = "Emergency surgery: on",
            ["CharacterSummary.Health.Surgery.AutomaticOff"] = "Emergency surgery: off",
            ["CharacterSummary.Health.Consumables.Title"] = "Diet and substances",
            ["CharacterSummary.Health.Consumables.DietPolicy"] = "Diet policy  {0}",
            ["CharacterSummary.Health.Consumables.SubstanceRow"] = "{0}  {1} · tolerance {2:0.#} · addiction {3:0.#} · withdrawal {4:0.#}{5}",
            ["CharacterSummary.Health.Consumables.ActiveEffectSuffix"] = " · effect {0:0}s",
            ["CharacterSummary.Health.Button.DietCurrent"] = "Diet: {0}",
            ["CharacterSummary.Health.Button.DietPolicy"] = "Diet policy",
            ["CharacterSummary.Health.Button.NoSubstance"] = "No substance",
            ["CharacterSummary.Health.Button.NoPolicy"] = "No policy",
            ["CharacterSummary.Health.DietPolicy.Vegan"] = "Vegan",
            ["CharacterSummary.Health.DietPolicy.Vegetarian"] = "Vegetarian",
            ["CharacterSummary.Health.DietPolicy.CarnivorePreferred"] = "Carnivore preferred",
            ["CharacterSummary.Health.DietPolicy.StrictTaboo"] = "Strict taboo",
            ["CharacterSummary.Health.DietPolicy.Free"] = "Unrestricted",
            ["CharacterSummary.Health.SubstancePolicy.MedicalOnly"] = "Medical only",
            ["CharacterSummary.Health.SubstancePolicy.CombatOnly"] = "In combat",
            ["CharacterSummary.Health.SubstancePolicy.MoodThreshold"] = "Mood threshold",
            ["CharacterSummary.Health.SubstancePolicy.Scheduled"] = "Scheduled",
            ["CharacterSummary.Health.SubstancePolicy.Forbidden"] = "Forbidden",

            ["CharacterSummary.Health.Anatomy.Title"] = "Body and organs · {0}",
            ["CharacterSummary.Health.Anatomy.PrimaryFunctions"] = "Consciousness {0} · sight {1} · breathing {2}",
            ["CharacterSummary.Health.Anatomy.SecondaryFunctions"] = "Digestion {0} · filtration {1} · manipulation {2} · mobility {3}",
            ["CharacterSummary.Health.Anatomy.Missing"] = "Missing",
            ["CharacterSummary.Health.Anatomy.BleedingSuffix"] = " · bleeding {0:0.##}/sec",
            ["CharacterSummary.Health.Anatomy.InfectionSuffix"] = " · infection {0:0.#}",
            ["CharacterSummary.Health.Anatomy.RejectionSuffix"] = " · rejection {0:0.#}",
            ["CharacterSummary.Health.Anatomy.MutationSuffix"] = " · mutation {0:0.#}",
            ["CharacterSummary.Health.Anatomy.PartKind.NaturalOrgan"] = "Natural organ",
            ["CharacterSummary.Health.Anatomy.PartKind.Prosthetic"] = "Prosthetic",
            ["CharacterSummary.Health.Anatomy.PartKind.Implant"] = "Implant",
            ["CharacterSummary.Health.Anatomy.PartKind.ArcaneGraft"] = "Arcane graft",
            ["CharacterSummary.Health.Treatment.NoQueue"] = "No surgery queued",
            ["CharacterSummary.Health.Treatment.Queue"] = "Surgery queued · {0} · {1} · {2}",
            ["CharacterSummary.Health.Treatment.Doctor"] = "Surgeon {0}",

            ["CharacterSummary.Status.SpeciesUnknown"] = "Unknown species",
            ["CharacterSummary.Status.Profile"] = "Lv.{0} · {1} · {2} · {3}",
            ["CharacterSummary.Status.Health.Injury"] = "{0}/{1} · injury {2}%",
            ["CharacterSummary.Status.Health.Normal"] = "{0}/{1}",
            ["CharacterSummary.Status.Carry.Unavailable"] = "No carried items\nCarry-limit information unavailable",
            ["CharacterSummary.Status.Carry.Weight"] = "Carried weight {0} / base {1} / maximum {2}",
            ["CharacterSummary.Status.Carry.Overloaded"] = "Overloaded · movement speed {0}%",
            ["CharacterSummary.Status.Carry.Normal"] = "Not overloaded · movement speed 100%",
            ["CharacterSummary.Status.Carry.NoItems"] = "No carried items",
            ["CharacterSummary.Status.Temperature.Stable"] = "Temperature stable",
            ["CharacterSummary.Status.Temperature.Caution"] = "Temperature caution",
            ["CharacterSummary.Status.Temperature.Danger"] = "Temperature danger",
            ["CharacterSummary.Status.Survival.IssueSuffix"] = " and {0} more",
            ["CharacterSummary.Status.Survival.Row"] = "Survival status {0}{1} · {2} · {3} · {4}"
        };

    [MenuItem("Tools/DungeonStory/Content/Update Character Summary UI Localization")]
    public static void Synchronize()
    {
        Locale koreanLocale = RequireLocale("ko", "Korean");
        Locale englishLocale = RequireLocale("en", "English");
        StringTableCollection collection =
            LocalizationEditorSettings.GetStringTableCollection(
                CharacterSummaryUiTextQuery.TableName)
            ?? LocalizationEditorSettings.CreateStringTableCollection(
                CharacterSummaryUiTextQuery.TableName,
                Root,
                new List<Locale> { koreanLocale, englishLocale });
        if (collection == null)
        {
            throw new InvalidOperationException(
                "Could not create CharacterSummaryUI String Table collection.");
        }

        StringTable korean = RequireTable(collection, koreanLocale, "ko");
        StringTable english = RequireTable(collection, englishLocale, "en");
        RemoveObsoleteEntries(collection);
        foreach (string key in Korean.Keys.OrderBy(
                     value => value,
                     StringComparer.Ordinal))
        {
            Set(korean, key, Korean[key]);
            Set(english, key, English[key]);
        }

        Validate(collection, korean, english);
        EditorUtility.SetDirty(collection.SharedData);
        EditorUtility.SetDirty(korean);
        EditorUtility.SetDirty(english);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        if (LocalizationSettings.HasSettings)
        {
            LocalizationSettings.StringDatabase.ReleaseTable(
                CharacterSummaryUiTextQuery.TableName);
        }
        Debug.Log(
            "CharacterSummaryUI localization synchronized: "
            + $"{Korean.Count} keys, ko/en placeholder parity complete.");
    }

    public static void Validate()
    {
        StringTableCollection collection =
            LocalizationEditorSettings.GetStringTableCollection(
                CharacterSummaryUiTextQuery.TableName)
            ?? throw new InvalidOperationException(
                "CharacterSummaryUI String Table collection is missing.");
        Validate(
            collection,
            collection.GetTable("ko") as StringTable
                ?? throw new InvalidOperationException(
                    "CharacterSummaryUI Korean String Table is missing."),
            collection.GetTable("en") as StringTable
                ?? throw new InvalidOperationException(
                    "CharacterSummaryUI English String Table is missing."));
    }

    private static Locale RequireLocale(string code, string name) =>
        LocalizationEditorSettings.GetLocale(code)
        ?? throw new InvalidOperationException(
            $"{name} locale '{code}' is missing.");

    private static StringTable RequireTable(
        StringTableCollection collection,
        Locale locale,
        string suffix) =>
        collection.GetTable(locale.Identifier) as StringTable
        ?? collection.AddNewTable(
            locale.Identifier,
            $"{Root}/CharacterSummaryUI_{suffix}.asset") as StringTable
        ?? throw new InvalidOperationException(
            $"Could not create CharacterSummaryUI '{suffix}' String Table.");

    private static void RemoveObsoleteEntries(StringTableCollection collection)
    {
        HashSet<string> required = new HashSet<string>(
            Korean.Keys,
            StringComparer.Ordinal);
        foreach (string key in collection.SharedData.Entries
                     .Select(entry => entry.Key)
                     .Where(key => !required.Contains(key))
                     .ToArray())
        {
            collection.RemoveEntry(key);
        }
    }

    private static void Set(StringTable table, string key, string value)
    {
        StringTableEntry entry = table.GetEntry(key);
        if (entry == null)
        {
            table.AddEntry(key, value);
        }
        else
        {
            entry.Value = value;
        }
    }

    private static void Validate(
        StringTableCollection collection,
        StringTable korean,
        StringTable english)
    {
        string[] expected = Korean.Keys
            .OrderBy(key => key, StringComparer.Ordinal)
            .ToArray();
        string[] englishKeys = English.Keys
            .OrderBy(key => key, StringComparer.Ordinal)
            .ToArray();
        string[] actual = collection.SharedData.Entries
            .Select(entry => entry.Key)
            .OrderBy(key => key, StringComparer.Ordinal)
            .ToArray();
        if (!expected.SequenceEqual(englishKeys, StringComparer.Ordinal))
        {
            throw new InvalidOperationException(
                "CharacterSummaryUI authored ko/en keys do not match.");
        }
        if (!expected.SequenceEqual(actual, StringComparer.Ordinal))
        {
            throw new InvalidOperationException(
                $"CharacterSummaryUI must contain exactly {Korean.Count} authored keys.");
        }

        foreach (string key in expected)
        {
            string koreanValue = Require(korean, key);
            string englishValue = Require(english, key);
            int[] koreanPlaceholders = GetPlaceholderIndexes(koreanValue);
            int[] englishPlaceholders = GetPlaceholderIndexes(englishValue);
            if (!koreanPlaceholders.SequenceEqual(englishPlaceholders))
            {
                throw new InvalidOperationException(
                    $"CharacterSummaryUI placeholder mismatch for '{key}'.");
            }
            ValidateCompositeFormat(key, "ko", koreanValue, koreanPlaceholders);
            ValidateCompositeFormat(key, "en", englishValue, englishPlaceholders);
        }
    }

    private static string Require(StringTable table, string key)
    {
        StringTableEntry entry = table.GetEntry(key);
        if (entry == null || string.IsNullOrWhiteSpace(entry.Value))
        {
            throw new InvalidOperationException(
                $"String Table '{table.LocaleIdentifier}' is missing '{key}'.");
        }
        return entry.Value;
    }

    private static int[] GetPlaceholderIndexes(string value) =>
        Regex.Matches(value, @"\{(?<index>\d+)(?:[^{}]*)\}")
            .Cast<Match>()
            .Select(match => int.Parse(
                match.Groups["index"].Value,
                CultureInfo.InvariantCulture))
            .Distinct()
            .OrderBy(index => index)
            .ToArray();

    private static void ValidateCompositeFormat(
        string key,
        string locale,
        string template,
        IReadOnlyList<int> placeholders)
    {
        int argumentCount = placeholders.Count == 0
            ? 0
            : placeholders[placeholders.Count - 1] + 1;
        object[] arguments = Enumerable.Repeat<object>(0, argumentCount).ToArray();
        try
        {
            string.Format(CultureInfo.InvariantCulture, template, arguments);
        }
        catch (FormatException exception)
        {
            throw new InvalidOperationException(
                $"CharacterSummaryUI '{key}' has an invalid {locale} format.",
                exception);
        }
    }
}
#endif
