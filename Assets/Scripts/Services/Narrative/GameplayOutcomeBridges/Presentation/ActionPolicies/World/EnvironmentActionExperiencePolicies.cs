using System;
using System.Linq;
using C = GameplayExperienceCategory;
using static WorldExperiencePolicyFacts;

public sealed class CharacterGolemUpkeepEpisodeExperiencePolicy : GameplayActionExperiencePolicy
{
    public CharacterGolemUpkeepEpisodeExperiencePolicy() : base("character.golem-upkeep-episode", "충전/마모는 일상, 실제 방전은 유의미. 별도 신체 피해 중복 추정 금지.") { }
    protected override GameplayExperienceJudgment Evaluate(GameplayExperienceFacts f)
    {
        if (!f.Character || !f.HasRole("actor")) return f.Technical();
        string kind = f.Fact("episode-kind");
        f.Fact("state-before"); f.Fact("state-after");
        return kind switch
        {
            "wear" => f.Action(C.Routine, "작업에 따른 골렘 마모. 직접 신체 손상은 별도 결과."),
            "recharge" => f.Action(C.Routine, "골렘 재충전 완료."),
            "discharge" => f.Action(C.Meaningful, "골렘 방전 에피소드. 사망이나 영구 고장은 별도 증거 필요."),
            _ => f.Judge(C.Unassessed, "unknown-golem-episode", "등록되지 않은 골렘 에피소드 종류: " + kind)
        };
    }
}

public sealed class EnvironmentControlChangedExperiencePolicy : GameplayActionExperiencePolicy
{
    public EnvironmentControlChangedExperiencePolicy() : base("environment.control-changed", "환경 제어 설정 변경. 체감 피해는 별도 노출 결과에서 판단.") { }
    protected override GameplayExperienceJudgment Evaluate(GameplayExperienceFacts f)
    => f.Action(C.Routine, "환경 제어 설정 변경. 체감 피해는 별도 노출 결과에서 판단.");
}

public sealed class EnvironmentDiseaseRouteExposureExperiencePolicy : GameplayActionExperiencePolicy
{
    public EnvironmentDiseaseRouteExposureExperiencePolicy() : base("environment.disease-route-exposure", "노출 시간만으로 발병·생명위기 추정 금지.") { }
    protected override GameplayExperienceJudgment Evaluate(GameplayExperienceFacts f)
    {
            Nonnegative(f.Number("exposure-hours", "hour", "disease"));
            Nonnegative(f.Number("environment-coefficient", "ratio", "disease"));
            return f.Action(C.Routine, "질병 경로에 노출됨. 발병이나 실제 신체 손상은 이 결과에 없음.");
        }
}

public sealed class EnvironmentHarpyGaleRelocationExperiencePolicy : GameplayActionExperiencePolicy
{
    public EnvironmentHarpyGaleRelocationExperiencePolicy() : base("environment.harpy-gale-relocation", "Failed도 실제 돌풍 이동이면 경미. 물건 이동을 소멸/부상으로 추정하지 않음.") { }
    protected override GameplayExperienceJudgment Evaluate(GameplayExperienceFacts f)
    {
            double quantity = Nonnegative(f.Number("quantity", "count", "item"));
            bool moved = f.Number("source-x", "cell", "item") != f.Number("destination-x", "cell", "item")
                | f.Number("source-y", "cell", "item") != f.Number("destination-y", "cell", "item");
            return quantity > 0 && moved ? f.Judge(C.Mild, "gale-physical-relocation", "돌풍으로 물건이 실제 이동. 부상·재산 소멸은 없음.") : f.Technical("no-gale-relocation");
        }
}

public sealed class EnvironmentProcessAccidentExperiencePolicy : GameplayActionExperiencePolicy
{
    public EnvironmentProcessAccidentExperiencePolicy() : base("environment.process-accident", "작업자 실제 피해만 경미; 전체 체력 맥락 없어 중상 추정 금지.") { }
    protected override GameplayExperienceJudgment Evaluate(GameplayExperienceFacts f)
    => f.Character && f.HasRole("actor") ? f.AppliedDamage(f.Number("damage", "point"), "작업 중 사고 피해.") : f.Technical("not-injured-worker");
}

public sealed class EnvironmentRoomConditionChangedExperiencePolicy : GameplayActionExperiencePolicy
{
    public EnvironmentRoomConditionChangedExperiencePolicy() : base("environment.room-condition-changed", "방 청결 수치 변화만 일상; 부상·감정 추정 금지.") { }
    protected override GameplayExperienceJudgment Evaluate(GameplayExperienceFacts f)
    {
            f.Number("previous-cleanliness", "point", "room"); f.Number("current-cleanliness", "point", "room");
            return f.Action(C.Routine, "방 청결 변화 관측. 개인의 실제 피해나 기분 변화는 별도 결과.");
        }
}

public sealed class EnvironmentRoomExperienceAppliedExperiencePolicy : GameplayActionExperiencePolicy
{
    public EnvironmentRoomExperienceAppliedExperiencePolicy() : base("environment.room-experience-applied", "당사자의 실제 합산 기분 변화: 0 일상, 절대값10 미만 경미, 이상 유의미.") { }
    protected override GameplayExperienceJudgment Evaluate(GameplayExperienceFacts f)
    => f.Character && f.HasRole("actor") ? Mood(f, f.Number("impression-mood", "point") + f.Number("cleanliness-mood", "point")) : f.Technical("room-context-not-personal");
}

public sealed class EnvironmentSpeciesIncidentTriggeredExperiencePolicy : GameplayActionExperiencePolicy
{
    public EnvironmentSpeciesIncidentTriggeredExperiencePolicy() : base("environment.species-incident-triggered", "종족 사건 발생만 경미. 이름/설명에서 장애·치명상 추정 금지.") { }
    protected override GameplayExperienceJudgment Evaluate(GameplayExperienceFacts f)
    {
            f.Fact("incident-id");
            return f.Character && f.HasRole("actor") ? f.Judge(C.Mild, "species-incident-observed", "종족 특성 사건 발생. 신체 피해의 크기는 별도 결과로 판단.") : f.Technical("species-definition-context");
        }
}

public sealed class CombatDamageResolvedExperiencePolicy : GameplayActionExperiencePolicy
{
    public CombatDamageResolvedExperiencePolicy() : base("combat.damage-resolved", "피해자는 실제 피해. 공격자는 타격 사실만. 피해율/영구상태 없는 절대 피해량으로 고득점 금지.") { }
    protected override GameplayExperienceJudgment Evaluate(GameplayExperienceFacts f)
    {
            double damage = Nonnegative(f.Number("health.damage", "health-point", "victim"));
            return f.HasRole("victim") ? f.AppliedDamage(damage, "전투 피해.")
                : f.HasRole("attacker") ? f.Judge(damage > 0 ? C.Mild : C.Routine, "attack-consequence", "타격 결과. 상대의 피해를 자신의 생명 위기로 평가하지 않음.") : f.Technical();
        }
}

