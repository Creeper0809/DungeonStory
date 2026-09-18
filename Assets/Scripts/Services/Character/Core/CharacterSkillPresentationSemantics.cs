using System;
using System.Linq;

/// <summary>
/// Public, nonnumeric meaning for an already-authorized skill capability and
/// targeting rule. This is presentation context only: formula values, costs,
/// and module selection remain owned by the formula/runtime path.
/// </summary>
public static class CharacterSkillPresentationSemantics
{
    public static string DescribeCapability(string capabilityId)
    {
        return capabilityId switch
        {
            "damage" => "기본 피해에 배율을 곱해 직접 피해를 준다.",
            "dot" => "대상에게 시간이 지나며 피해를 준다.",
            "vulnerability" => "대상이 받는 피해를 더 크게 만든다.",
            "delay" => "대상의 행동 순서 주도권에 페널티를 준다.",
            "debuff" => "대상의 전투 능력을 약화한다.",
            "multi_target" => "기본 효과가 닿는 대상 범위를 넓힌다.",
            "conditional_amplify" => "대상 체력이 절반 이하일 때 기본 피해를 추가한다.",
            "heal" => "대상의 체력을 회복한다.",
            "guard" => "대상이 받는 피해를 줄이는 보호 상태를 부여한다.",
            "buff" => "대상의 전투 능력을 강화한다.",
            "cleanse" => "전투 중에는 대상의 해로운 상태를 제거하고, 전투 밖에서는 부상으로 인한 기분 상태를 제거한다.",
            "protect" => "대상이 받는 피해를 줄이는 보호 상태를 부여한다.",
            "cooldown_adjust" => "대상의 기술 재사용 대기시간을 줄인다.",
            "reposition" => "대상을 전투 대열에서 뒤쪽으로 이동시킨다.",
            "work_speed" => "작업 시작·완료에 적용되는 작업 속도 보정을 준다.",
            "output" => "작업 완료 때 생산량을 늘린다.",
            "repair" => "작업 완료 때 수리 속도를 높인다.",
            "stock" => "작업 완료 때 재고 생산량을 늘린다.",
            "research" => "작업 완료로 집계된 시간에 비례해 연구 진척을 더한다.",
            "revenue" => "작업 완료 때 얻는 수익을 늘린다.",
            "cleaning" => "작업 완료 때 청소 속도를 높인다.",
            "needs" => "가장 부족한 생존 욕구를 회복한다.",
            "mood" => "기분에 긍정적인 상태를 부여한다.",
            "relationship" => "긍정적인 관계 감정 변화에 보정을 더한다.",
            _ => throw new InvalidOperationException(
                "CharacterSkill presentation has no public meaning for capability '"
                + (capabilityId ?? string.Empty) + "'.")
        };
    }

    /// <summary>
    /// Describes an authorized capability as consumed by this exact skill path.
    /// Some capability axes have a distinct, already-authored Defense-ultimate
    /// applicator and must not inherit their combat-path wording.
    /// </summary>
    public static string DescribeCapability(
        string capabilityId,
        CharacterSkillCandidateRule rule,
        CharacterSkillKind kind)
    {
        if (rule == null) throw new ArgumentNullException(nameof(rule));
        if (kind == CharacterSkillKind.Ultimate
            && rule.ultimateDomain == CharacterUltimateDomain.Defense)
        {
            return capabilityId switch
            {
                "dot" => "침입자에게 피해량과 기간을 반영해 합산한 즉시 피해를 준다.",
                "conditional_amplify" => "침입자에게 기본 피해 계수의 절반을 반영한 추가 즉시 피해를 준다.",
                _ => DescribeCapability(capabilityId)
            };
        }

        return DescribeCapability(capabilityId);
    }

    public static string DescribeCapability(
        NarrativeFormulaCapabilityDescriptor descriptor)
    {
        if (descriptor == null) throw new ArgumentNullException(nameof(descriptor));
        string meaning = DescribeCapability(descriptor.CapabilityId);
        string[] forbidden = (descriptor.ForbiddenSynergies ?? Array.Empty<string>())
            .OrderBy(value => value, StringComparer.Ordinal).ToArray();
        return forbidden.Length == 0
            ? meaning
            : meaning + " 함께 선택 금지 이로운 기능="
                + string.Join(",", forbidden) + ".";
    }

    public static string DescribeCapability(
        NarrativeFormulaCapabilityDescriptor descriptor,
        CharacterSkillCandidateRule rule,
        CharacterSkillKind kind)
    {
        if (descriptor == null) throw new ArgumentNullException(nameof(descriptor));
        string meaning = DescribeCapability(descriptor.CapabilityId, rule, kind);
        string[] forbidden = (descriptor.ForbiddenSynergies ?? Array.Empty<string>())
            .OrderBy(value => value, StringComparer.Ordinal).ToArray();
        return forbidden.Length == 0
            ? meaning
            : meaning + " 함께 선택 금지 이로운 기능="
                + string.Join(",", forbidden) + ".";
    }

    public static string DescribeContext(
        CharacterSkillCandidateRule rule,
        CharacterSkillKind kind)
    {
        if (rule == null) throw new ArgumentNullException(nameof(rule));
        CharacterSkillAreaRules.RequireValid(
            rule.targetingMode, rule.effectArea, rule.areaSize);
        if (kind == CharacterSkillKind.Ultimate
            && rule.ultimateDomain == CharacterUltimateDomain.None)
        {
            throw new InvalidOperationException(
                "Ultimate CharacterSkill presentation requires an ultimate domain.");
        }
        if (kind != CharacterSkillKind.Ultimate
            && rule.ultimateDomain != CharacterUltimateDomain.None)
        {
            throw new InvalidOperationException(
                "Only ultimate CharacterSkills may expose an ultimate domain.");
        }

        bool defenseUltimate = kind == CharacterSkillKind.Ultimate
            && rule.ultimateDomain == CharacterUltimateDomain.Defense;
        return DescribeKind(kind) + " " + DescribeTrigger(rule.trigger) + " "
            + DescribeTarget(rule.target) + " "
            + DescribeTargeting(rule.targetingMode, defenseUltimate) + " "
            + DescribeArea(rule.effectArea) + " "
            + "대상 분류는 기준 대상군이고, 선택 방식과 효과 범위가 실제 적용 대상을 정한다. "
            + (kind == CharacterSkillKind.Ultimate
                ? DescribeUltimateDomain(rule.ultimateDomain)
                : "궁극기 전용 분류는 적용되지 않는다.");
    }

    private static string DescribeKind(CharacterSkillKind kind) => kind switch
    {
        CharacterSkillKind.SpeciesActive => "종족 고유 발동 기술이다.",
        CharacterSkillKind.OwnerFixed => "소유자에게 고정된 기술이다.",
        CharacterSkillKind.Active => "플레이어가 직접 쓰거나 지정된 상황에서 쓰는 기술이다.",
        CharacterSkillKind.Passive => "조건이 맞으면 자동으로 적용되는 지속 기술이다.",
        CharacterSkillKind.Ultimate => "강한 제한 아래 사용하는 궁극기다.",
        _ => throw new ArgumentOutOfRangeException(nameof(kind))
    };

    private static string DescribeTrigger(CharacterSkillTrigger trigger) => trigger switch
    {
        CharacterSkillTrigger.ManualCombat => "전투 중 직접 사용한다.",
        CharacterSkillTrigger.BattleStarted => "전투가 시작될 때 발동한다.",
        CharacterSkillTrigger.DamageTaken => "피해를 받았을 때 발동한다.",
        CharacterSkillTrigger.EnemyDefeated => "적을 쓰러뜨렸을 때 발동한다.",
        CharacterSkillTrigger.BattleCompleted => "전투가 끝났을 때 발동한다.",
        CharacterSkillTrigger.InvasionStarted => "침입이 시작될 때 발동한다.",
        CharacterSkillTrigger.WorkStarted => "작업을 시작할 때 발동한다.",
        CharacterSkillTrigger.WorkCompleted => "작업을 끝냈을 때 발동한다.",
        CharacterSkillTrigger.NeedChanged => "생존 욕구 상태가 바뀔 때 발동한다.",
        CharacterSkillTrigger.MoodChanged => "기분 상태가 바뀔 때 발동한다.",
        CharacterSkillTrigger.RelationshipChanged => "관계 감정이 바뀔 때 발동한다.",
        CharacterSkillTrigger.OperatingDayStarted => "운영일이 시작될 때 발동한다.",
        CharacterSkillTrigger.ManualWork => "작업 중 직접 사용한다.",
        _ => throw new ArgumentOutOfRangeException(nameof(trigger))
    };

    private static string DescribeTarget(CharacterSkillTarget target) => target switch
    {
        CharacterSkillTarget.Self => "사용자 자신에게 적용한다.",
        CharacterSkillTarget.Ally => "아군을 대상으로 한다.",
        CharacterSkillTarget.Enemy => "적 한 명을 대상으로 한다.",
        CharacterSkillTarget.AllAllies => "아군 전체를 대상으로 한다.",
        CharacterSkillTarget.AllEnemies => "적 전체를 대상으로 한다.",
        CharacterSkillTarget.Facility => "시설을 대상으로 한다.",
        CharacterSkillTarget.Dungeon => "던전 운영 범위를 대상으로 한다.",
        _ => throw new ArgumentOutOfRangeException(nameof(target))
    };

    private static string DescribeTargeting(
        CharacterSkillTargetingMode mode,
        bool defenseUltimate) => defenseUltimate
            ? "침입 시작 이벤트가 침입자를 자동으로 지정한다."
            : mode switch
    {
        CharacterSkillTargetingMode.Self => "대상 선택 없이 자신에게 적용한다.",
        CharacterSkillTargetingMode.PlayerSelected => "플레이어가 유효한 대상 하나를 고른다.",
        CharacterSkillTargetingMode.DeterministicRandom => "유효한 대상 가운데 결정론적으로 한 명을 고른다.",
        CharacterSkillTargetingMode.AllEligible => "유효한 모든 대상에게 적용한다.",
        _ => throw new ArgumentOutOfRangeException(nameof(mode))
    };

    private static string DescribeArea(CharacterSkillEffectArea area) => area switch
    {
        CharacterSkillEffectArea.Single => "단일 대상 범위로 적용한다.",
        CharacterSkillEffectArea.Room => "대상이 있는 방 범위로 적용한다.",
        CharacterSkillEffectArea.Square => "대상을 중심으로 한 정사각 범위로 적용한다.",
        CharacterSkillEffectArea.Dungeon => "던전 전체의 유효 대상 범위로 적용한다.",
        _ => throw new ArgumentOutOfRangeException(nameof(area))
    };

    private static string DescribeUltimateDomain(CharacterUltimateDomain domain) => domain switch
    {
        CharacterUltimateDomain.Offense => "공격 계열 궁극기다.",
        CharacterUltimateDomain.Defense => "방어 계열 궁극기다.",
        CharacterUltimateDomain.Management => "운영 계열 궁극기다.",
        _ => throw new ArgumentOutOfRangeException(nameof(domain))
    };
}
