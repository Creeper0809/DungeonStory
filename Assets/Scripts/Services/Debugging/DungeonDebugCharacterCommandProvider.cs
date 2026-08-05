using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public sealed class DungeonDebugCharacterCommandProvider : IDungeonDebugCommandProvider
{
    private readonly ICharacterDeprivationCommand deprivationRuntime;
    private readonly ICharacterWorldQuery characterWorld;
    private readonly ICharacterNeedDefinitionCatalog needDefinitionCatalog;

    public DungeonDebugCharacterCommandProvider(
        ICharacterDeprivationCommand deprivationRuntime,
        ICharacterWorldQuery characterWorld,
        ICharacterNeedDefinitionCatalog needDefinitionCatalog)
    {
        this.deprivationRuntime = deprivationRuntime
            ?? throw new ArgumentNullException(nameof(deprivationRuntime));
        this.characterWorld = characterWorld
            ?? throw new ArgumentNullException(nameof(characterWorld));
        this.needDefinitionCatalog = needDefinitionCatalog
            ?? throw new ArgumentNullException(nameof(needDefinitionCatalog));
    }

    public IEnumerable<IDungeonDebugCommand> GetCommands()
    {
        yield return CharacterCommand("character:heal", "완전 회복", context =>
        {
            CharacterActor actor = context.Target.Character;
            actor.Heal(actor.MaxHealth);
            actor.SetInjurySeverity(0f);
            return $"{Name(actor)} 완전 회복";
        });
        yield return CharacterCommand("character:fill-needs", "욕구 전체 충족", context =>
        {
            CharacterActor actor = context.Target.Character;
            foreach (CharacterCondition condition in Enum.GetValues(typeof(CharacterCondition)))
            {
                if (condition != CharacterCondition.MOOD
                    && actor.stats.TryGetValue(condition, out float current))
                {
                    actor.ChangesStat(condition, 100f - current);
                }
            }

            return $"{Name(actor)} 욕구 충족";
        });
        foreach (CharacterCondition condition in Enum.GetValues(typeof(CharacterCondition)))
        {
            if (condition == CharacterCondition.MOOD) continue;
            CharacterCondition captured = condition;
            yield return CharacterCommand(
                $"character:set-need:{captured}",
                $"{NeedName(captured)} 설정",
                context =>
                {
                    CharacterActor actor = context.Target.Character;
                    float current = actor.stats.TryGetValue(captured, out float value) ? value : 0f;
                    float target = Mathf.Clamp(context.NumericValue, 0f, 100f);
                    actor.ChangesStat(captured, target - current);
                    return $"{Name(actor)} {NeedName(captured)} {target:0}";
                },
                defaultValue: 100f);
        }

        yield return CharacterCommand("character:mood", "기분 변경", context =>
        {
            CharacterActor actor = context.Target.Character;
            actor.ApplyMoodFactor(
                "debug:mood",
                "디버그 기분 변경",
                Mathf.Clamp(context.NumericValue, -100f, 100f),
                600f,
                1);
            return $"{Name(actor)} 기분 {context.NumericValue:+0;-0;0}";
        });
        yield return CharacterCommand("character:damage", "피해 적용", context =>
        {
            CharacterActor actor = context.Target.Character;
            float amount = Mathf.Max(0f, context.NumericValue);
            actor.ApplyDamage(amount, "디버그");
            return $"{Name(actor)} 피해 {amount:0.#}";
        }, dangerous: true, defaultValue: 10f);
        yield return CharacterCommand("character:kill", "살해", context =>
        {
            CharacterActor actor = context.Target.Character;
            actor.Die("디버그 살해");
            return $"{Name(actor)} 사망";
        }, dangerous: true);
        yield return CharacterCommand("character:xp", "경험치 지급", context =>
        {
            CharacterActor actor = context.Target.Character;
            int amount = Mathf.Max(1, Mathf.RoundToInt(context.NumericValue));
            int levels = actor.Progression != null ? actor.Progression.AddExperience(amount) : 0;
            return $"{Name(actor)} 경험치 +{amount} · 레벨 상승 {levels}";
        }, defaultValue: 100f);
        yield return CharacterCommand("character:level", "최소 레벨 설정", context =>
        {
            CharacterActor actor = context.Target.Character;
            int level = Mathf.Clamp(Mathf.RoundToInt(context.NumericValue), 1, CharacterProgression.MaxLevel);
            bool changed = actor.Progression != null
                && actor.Progression.EnsureMinimumLevel(level, "디버그 레벨 조정");
            return changed
                ? $"{Name(actor)} 레벨 {actor.Progression.Level}"
                : $"{Name(actor)}은 이미 레벨 {actor.Progression?.Level ?? 1}";
        }, defaultValue: 10f);
        foreach (CharacterBreakdownKind kind in Enum.GetValues(typeof(CharacterBreakdownKind)))
        {
            if (kind == CharacterBreakdownKind.None) continue;
            CharacterBreakdownKind captured = kind;
            yield return CharacterCommand(
                $"character:breakdown:{captured}",
                $"{BreakdownName(captured)} 발동",
                context => deprivationRuntime.DebugForceBreakdown(context.Target.Character, captured)
                    ? $"{Name(context.Target.Character)}에게 {BreakdownName(captured)} 발동"
                    : "붕괴를 발동하지 못함",
                dangerous: true);
        }

        yield return CharacterCommand("character:clear-breakdown", "붕괴 해제", context =>
            deprivationRuntime.DebugClearBreakdown(context.Target.Character)
                ? $"{Name(context.Target.Character)} 붕괴 해제"
                : "활성 붕괴가 없음");
        yield return CharacterCommand("character:injure", "부상 적용", context =>
        {
            CharacterActor actor = context.Target.Character;
            float severity = Mathf.Clamp(context.NumericValue, 0f, 100f);
            actor.SetInjurySeverity(severity);
            return $"{Name(actor)} 부상 {severity:0}";
        }, dangerous: true, defaultValue: 35f);
        yield return CharacterCommand("character:treat", "부상 치료", context =>
        {
            CharacterActor actor = context.Target.Character;
            actor.SetInjurySeverity(0f);
            actor.Heal(actor.MaxHealth);
            return $"{Name(actor)} 부상 치료 완료";
        });
        yield return GlobalStaffCommand("character:heal-all", "전체 직원 완전 회복", actor =>
        {
            actor.Heal(actor.MaxHealth);
            actor.SetInjurySeverity(0f);
        });
        yield return GlobalStaffCommand("character:fill-needs-all", "전체 직원 욕구 충족", actor =>
        {
            foreach (CharacterCondition condition in Enum.GetValues(typeof(CharacterCondition)))
            {
                if (condition != CharacterCondition.MOOD
                    && actor.stats.TryGetValue(condition, out float current))
                {
                    actor.ChangesStat(condition, 100f - current);
                }
            }
        });
    }

    private IDungeonDebugCommand GlobalStaffCommand(
        string id,
        string label,
        Action<CharacterActor> execute)
    {
        return new DelegateDungeonDebugCommand(
            id,
            label,
            "사장과 현재 직원 전체에 적용합니다.",
            DungeonDebugCategory.Character,
            DungeonDebugTargetKind.None,
            _ =>
            {
                int count = 0;
                foreach (CharacterActor actor in characterWorld.Characters
                             .Where(IsFriendlyStaff))
                {
                    execute(actor);
                    count++;
                }

                return count > 0
                    ? DungeonDebugCommandResult.Succeeded($"{count}명에게 적용했습니다.")
                    : DungeonDebugCommandResult.Failed("적용할 사장 또는 직원이 없습니다.");
            });
    }

    private static IDungeonDebugCommand CharacterCommand(
        string id,
        string label,
        Func<DungeonDebugExecutionContext, string> execute,
        bool dangerous = false,
        float defaultValue = 10f)
    {
        return new DelegateDungeonDebugCommand(
            id,
            label,
            "정확히 클릭한 캐릭터에 적용합니다.",
            DungeonDebugCategory.Character,
            DungeonDebugTargetKind.Character,
            context =>
            {
                string message = execute(context);
                return message.Contains("못함", StringComparison.Ordinal)
                       || message.Contains("없음", StringComparison.Ordinal)
                    ? DungeonDebugCommandResult.Failed(message)
                    : DungeonDebugCommandResult.Succeeded(message);
            },
            isDangerous: dangerous,
            defaultNumericValue: defaultValue);
    }

    private static string Name(CharacterActor actor)
    {
        return actor?.Identity?.DisplayName ?? "캐릭터";
    }

    private static bool IsFriendlyStaff(CharacterActor actor)
    {
        return actor != null
            && !actor.IsDead
            && actor.characterType == CharacterType.NPC;
    }

    private string NeedName(CharacterCondition condition)
    {
        return needDefinitionCatalog.TryGet(condition, out CharacterNeedDefinition need)
            ? need.DisplayName
            : condition.ToString();
    }

    private static string BreakdownName(CharacterBreakdownKind kind)
    {
        return kind switch
        {
            CharacterBreakdownKind.DesperateRelief => "배변 붕괴",
            CharacterBreakdownKind.DesperateDrink => "갈증 붕괴",
            CharacterBreakdownKind.DesperateEat => "굶주림 붕괴",
            CharacterBreakdownKind.Collapse => "탈진",
            CharacterBreakdownKind.ViolentImpulse => "폭력 충동",
            _ => kind.ToString()
        };
    }
}
