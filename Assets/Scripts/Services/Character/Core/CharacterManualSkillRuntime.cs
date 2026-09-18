using System;
using System.Collections.Generic;
using System.Linq;

public sealed class CharacterManualSkillCommandState
{
    public string SkillId { get; internal set; } = string.Empty;
    public string DisplayName { get; internal set; } = string.Empty;
    public string MechanicalDescription { get; internal set; } = string.Empty;
    public CharacterSkillTarget Target { get; internal set; }
    public CharacterSkillTargetingMode TargetingMode { get; internal set; }
    public CharacterSkillEffectArea EffectArea { get; internal set; }
    public int AreaSize { get; internal set; }
    public int DurationHours { get; internal set; }
    public int CooldownDays { get; internal set; }
    public long RemainingCooldownHours { get; internal set; }
    public bool IsReady => RemainingCooldownHours <= 0;
}

public static class CharacterManualSkillRuntime
{
    public static IReadOnlyList<CharacterManualSkillCommandState> GetCommands(
        CharacterActor source,
        long absoluteHour)
    {
        CharacterProgression progression = source?.Progression;
        if (progression == null)
            return Array.Empty<CharacterManualSkillCommandState>();

        CharacterSkillUseLimitState limits = RequireLimits(progression);
        return progression.ActiveSkills
            .Where(IsManualWorkSkill)
            .OrderBy(value => value.id, StringComparer.Ordinal)
            .Select(skill =>
            {
                CharacterManualSkillCooldownState cooldown =
                    FindCooldown(limits, skill.id);
                return new CharacterManualSkillCommandState
                {
                    SkillId = skill.id,
                    DisplayName = skill.displayName,
                    MechanicalDescription = skill.mechanicalDescription,
                    Target = skill.target,
                    TargetingMode = skill.targetingMode,
                    EffectArea = skill.effectArea,
                    AreaSize = skill.areaSize,
                    DurationHours = skill.manualDurationHours,
                    CooldownDays = skill.manualCooldownDays,
                    RemainingCooldownHours = Math.Max(
                        0L,
                        (cooldown?.readyAbsoluteHour ?? 0L) - absoluteHour)
                };
            }).ToArray();
    }

    public static bool RequiresPlayerTarget(
        CharacterActor source,
        string skillId)
    {
        CharacterSkillInstance skill = FindSkill(source, skillId);
        return skill != null
            && skill.targetingMode == CharacterSkillTargetingMode.PlayerSelected;
    }

    public static bool TryActivate(
        CharacterActor source,
        string skillId,
        CharacterActor selectedCharacter,
        BuildableObject selectedFacility,
        ICharacterAiWorldRegistry world,
        IRoomLayoutCache rooms,
        long absoluteHour,
        out string message)
    {
        message = string.Empty;
        CharacterSkillInstance skill = FindSkill(source, skillId);
        if (!IsManualWorkSkill(skill))
        {
            message = "사용 가능한 작업 능력이 아닙니다.";
            return false;
        }
        try
        {
            RequireValidSkill(skill);
        }
        catch (Exception exception)
        {
            message = exception.Message;
            return false;
        }
        if (world == null || rooms == null)
        {
            message = "대상 탐색 시스템이 준비되지 않았습니다.";
            return false;
        }

        CharacterSkillUseLimitState sourceLimits = RequireLimits(source.Progression);
        CharacterManualSkillCooldownState existing =
            FindCooldown(sourceLimits, skill.id);
        long remaining = Math.Max(
            0L,
            (existing?.readyAbsoluteHour ?? 0L) - absoluteHour);
        if (remaining > 0)
        {
            message = $"{skill.displayName}: 재사용까지 {remaining}시간 남음";
            return false;
        }

        if (!TryResolveRecipients(
                source,
                skill,
                selectedCharacter,
                selectedFacility,
                world,
                rooms,
                existing?.useCount ?? 0,
                absoluteHour,
                out CharacterActor[] recipients,
                out message))
            return false;

        return CommitActivation(
            source,
            skill,
            recipients,
            sourceLimits,
            existing,
            absoluteHour,
            out message);
    }

    public static bool TryActivateForExpedition(
        CharacterActor source,
        string skillId,
        CharacterActor selectedCharacter,
        IReadOnlyList<CharacterActor> livingPartyMembers,
        Func<CharacterActor, int> formationRank,
        long absoluteHour,
        out string message)
    {
        message = string.Empty;
        CharacterSkillInstance skill = FindSkill(source, skillId);
        if (!IsManualWorkSkill(skill))
        {
            message = "사용 가능한 작업 능력이 아닙니다.";
            return false;
        }
        try
        {
            RequireValidSkill(skill);
        }
        catch (Exception exception)
        {
            message = exception.Message;
            return false;
        }
        if (livingPartyMembers == null || formationRank == null)
        {
            message = "원정대 대상 탐색 시스템이 준비되지 않았습니다.";
            return false;
        }

        CharacterActor[] party = livingPartyMembers
            .Where(value => value != null && !value.IsDead)
            .Distinct()
            .OrderBy(value => CharacterPersistentIdentity.Require(value).Value,
                StringComparer.Ordinal)
            .ToArray();
        if (!party.Contains(source))
        {
            message = "능력 사용자가 현재 원정대에 없습니다.";
            return false;
        }

        CharacterSkillUseLimitState sourceLimits = RequireLimits(source.Progression);
        CharacterManualSkillCooldownState existing =
            FindCooldown(sourceLimits, skill.id);
        long remaining = Math.Max(
            0L,
            (existing?.readyAbsoluteHour ?? 0L) - absoluteHour);
        if (remaining > 0)
        {
            message = $"{skill.displayName}: 재사용까지 {remaining}시간 남음";
            return false;
        }

        if (!TryResolveExpeditionRecipients(
                source,
                skill,
                selectedCharacter,
                party,
                formationRank,
                existing?.useCount ?? 0,
                out CharacterActor[] recipients,
                out message))
            return false;

        return CommitActivation(
            source,
            skill,
            recipients,
            sourceLimits,
            existing,
            absoluteHour,
            out message);
    }

    public static IReadOnlyList<CharacterSkillInstance> GetActiveBuffSkills(
        CharacterActor target,
        long absoluteHour)
    {
        CharacterSkillUseLimitState limits = target?.Progression?.GrowthState?.useLimits;
        if (limits == null)
            return Array.Empty<CharacterSkillInstance>();
        limits.manualSkillBuffs ??= new List<CharacterManualSkillBuffState>();
        limits.manualSkillBuffs.RemoveAll(value => value == null
            || value.frozenSkill == null
            || value.expiresAbsoluteHour <= absoluteHour);
        return limits.manualSkillBuffs
            .Select(value => value.frozenSkill)
            .Where(IsManualWorkSkill)
            .ToArray();
    }

    private static bool TryResolveRecipients(
        CharacterActor source,
        CharacterSkillInstance skill,
        CharacterActor selectedCharacter,
        BuildableObject selectedFacility,
        ICharacterAiWorldRegistry world,
        IRoomLayoutCache rooms,
        int useCount,
        long absoluteHour,
        out CharacterActor[] recipients,
        out string message)
    {
        recipients = Array.Empty<CharacterActor>();
        message = string.Empty;
        CharacterActor[] eligible = world.Characters
            .Where(value => value != null && !value.IsDead)
            .Where(value => skill.target == CharacterSkillTarget.Self
                ? ReferenceEquals(value, source)
                : skill.target == CharacterSkillTarget.Ally
                    && !ReferenceEquals(value, source)
                    && OwnerCommandSelectionState.IsCommandable(value))
            .OrderBy(value => CharacterPersistentIdentity.Require(value).Value,
                StringComparer.Ordinal)
            .ToArray();

        CharacterActor anchorActor = selectedCharacter;
        BuildableObject anchorFacility = selectedFacility;
        switch (skill.targetingMode)
        {
            case CharacterSkillTargetingMode.Self:
                anchorActor = source;
                anchorFacility = null;
                break;
            case CharacterSkillTargetingMode.PlayerSelected:
                if (anchorActor == null && anchorFacility == null)
                {
                    message = "대상 캐릭터나 시설을 우클릭하세요.";
                    return false;
                }
                break;
            case CharacterSkillTargetingMode.DeterministicRandom:
                if (eligible.Length == 0)
                {
                    message = "무작위로 고를 합법 대상이 없습니다.";
                    return false;
                }
                int seed = CharacterGrowthRules.StableHash(string.Join("|",
                    CharacterPersistentIdentity.Require(source).Value,
                    skill.id,
                    useCount,
                    skill.formulaVersion,
                    skill.formulaCatalogSha256 ?? string.Empty));
                anchorActor = eligible[(seed & int.MaxValue) % eligible.Length];
                anchorFacility = null;
                break;
            case CharacterSkillTargetingMode.AllEligible:
                recipients = eligible;
                return RequireNonEmpty(recipients, out message);
            default:
                message = "알 수 없는 대상 선택 방식입니다.";
                return false;
        }

        if (skill.effectArea == CharacterSkillEffectArea.Single)
        {
            if (anchorActor == null || !eligible.Contains(anchorActor))
            {
                message = "선택한 캐릭터는 이 능력의 합법 대상이 아닙니다.";
                return false;
            }
            recipients = new[] { anchorActor };
            return true;
        }

        if (!world.TryGetGrid(out Grid grid) || grid == null)
        {
            message = "대상 범위를 계산할 그리드가 없습니다.";
            return false;
        }
        UnityEngine.Vector2Int center = anchorActor != null
            ? anchorActor.GetNowXY()
            : anchorFacility.centerPos;
        if (skill.effectArea == CharacterSkillEffectArea.Room)
        {
            bool hasRoom = anchorFacility != null
                ? rooms.TryGetRoom(anchorFacility, out RoomInstance room)
                : rooms.TryGetRoom(grid, center, out room);
            if (!hasRoom || room == null || !room.IsUsable)
            {
                message = "선택한 대상이 사용 가능한 방 안에 있지 않습니다.";
                return false;
            }
            recipients = eligible.Where(value => room.ContainsCell(value.GetNowXY()))
                .ToArray();
            return RequireNonEmpty(recipients, out message);
        }
        if (skill.effectArea == CharacterSkillEffectArea.Square)
        {
            int half = skill.areaSize / 2;
            recipients = eligible.Where(value =>
            {
                UnityEngine.Vector2Int position = value.GetNowXY();
                return Math.Abs(position.x - center.x) <= half
                    && Math.Abs(position.y - center.y) <= half;
            }).ToArray();
            return RequireNonEmpty(recipients, out message);
        }
        message = "대상 범위와 선택 방식이 일치하지 않습니다.";
        return false;
    }

    private static bool TryResolveExpeditionRecipients(
        CharacterActor source,
        CharacterSkillInstance skill,
        CharacterActor selectedCharacter,
        IReadOnlyList<CharacterActor> party,
        Func<CharacterActor, int> formationRank,
        int useCount,
        out CharacterActor[] recipients,
        out string message)
    {
        recipients = Array.Empty<CharacterActor>();
        message = string.Empty;
        CharacterActor[] eligible = party
            .Where(value => skill.target == CharacterSkillTarget.Self
                ? ReferenceEquals(value, source)
                : skill.target == CharacterSkillTarget.Ally
                    && !ReferenceEquals(value, source))
            .OrderBy(value => CharacterPersistentIdentity.Require(value).Value,
                StringComparer.Ordinal)
            .ToArray();

        CharacterActor anchor = selectedCharacter;
        switch (skill.targetingMode)
        {
            case CharacterSkillTargetingMode.Self:
                anchor = source;
                break;
            case CharacterSkillTargetingMode.PlayerSelected:
                if (anchor == null)
                {
                    message = "대상 원정대원을 선택하세요.";
                    return false;
                }
                break;
            case CharacterSkillTargetingMode.DeterministicRandom:
                if (eligible.Length == 0)
                {
                    message = "무작위로 고를 합법 원정대원이 없습니다.";
                    return false;
                }
                int seed = CharacterGrowthRules.StableHash(string.Join("|",
                    CharacterPersistentIdentity.Require(source).Value,
                    skill.id,
                    useCount,
                    skill.formulaVersion,
                    skill.formulaCatalogSha256 ?? string.Empty));
                anchor = eligible[(seed & int.MaxValue) % eligible.Length];
                break;
            case CharacterSkillTargetingMode.AllEligible:
                recipients = eligible;
                return RequireNonEmpty(recipients, out message);
            default:
                message = "알 수 없는 대상 선택 방식입니다.";
                return false;
        }

        if (anchor == null || !eligible.Contains(anchor))
        {
            message = "선택한 원정대원은 이 능력의 합법 대상이 아닙니다.";
            return false;
        }
        if (skill.effectArea == CharacterSkillEffectArea.Single)
        {
            recipients = new[] { anchor };
            return true;
        }

        int anchorRank = formationRank(anchor);
        int maximumDistance = skill.effectArea switch
        {
            CharacterSkillEffectArea.Room => 0,
            CharacterSkillEffectArea.Square when skill.areaSize == 3 => 0,
            CharacterSkillEffectArea.Square when skill.areaSize == 5 => 1,
            CharacterSkillEffectArea.Square when skill.areaSize == 7 => int.MaxValue,
            CharacterSkillEffectArea.Dungeon => int.MaxValue,
            _ => -1
        };
        if (maximumDistance < 0)
        {
            message = "원정에서 지원하지 않는 대상 범위입니다.";
            return false;
        }

        recipients = eligible
            .Where(value => maximumDistance == int.MaxValue
                || Math.Abs(formationRank(value) - anchorRank) <= maximumDistance)
            .ToArray();
        return RequireNonEmpty(recipients, out message);
    }

    private static bool CommitActivation(
        CharacterActor source,
        CharacterSkillInstance skill,
        CharacterActor[] recipients,
        CharacterSkillUseLimitState sourceLimits,
        CharacterManualSkillCooldownState existing,
        long absoluteHour,
        out string message)
    {
        string sourceId = CharacterPersistentIdentity.Require(source).Value;
        long expires = checked(absoluteHour + skill.manualDurationHours);
        foreach (CharacterActor recipient in recipients)
        {
            CharacterSkillUseLimitState targetLimits =
                RequireLimits(recipient.Progression);
            targetLimits.manualSkillBuffs ??=
                new List<CharacterManualSkillBuffState>();
            targetLimits.manualSkillBuffs.RemoveAll(value => value == null
                || value.expiresAbsoluteHour <= absoluteHour
                || (string.Equals(value.sourceCharacterId, sourceId,
                        StringComparison.Ordinal)
                    && string.Equals(value.skillId, skill.id,
                        StringComparison.Ordinal)));
            targetLimits.manualSkillBuffs.Add(new CharacterManualSkillBuffState
            {
                sourceCharacterId = sourceId,
                skillId = skill.id,
                expiresAbsoluteHour = expires,
                frozenSkill = skill.Clone()
            });
            CharacterSkillRuntimeEffects.ExecuteSkill(
                new CharacterSkillExecutionContext(
                    source,
                    CharacterSkillTrigger.ManualWork,
                    eventId: $"manual-work:{sourceId}:{skill.id}:{absoluteHour}",
                    targetActor: recipient),
                skill);
        }

        sourceLimits.manualSkillCooldowns ??=
            new List<CharacterManualSkillCooldownState>();
        CharacterManualSkillCooldownState cooldown = existing
            ?? new CharacterManualSkillCooldownState { skillId = skill.id };
        if (existing == null)
            sourceLimits.manualSkillCooldowns.Add(cooldown);
        cooldown.useCount = checked(cooldown.useCount + 1);
        cooldown.readyAbsoluteHour = checked(
            absoluteHour
            + (long)skill.manualCooldownDays * GameCalendarRules.HoursPerDay);
        message = recipients.Length == 1
            ? $"{skill.displayName}: {recipients[0].name}에게 발동"
            : $"{skill.displayName}: 대상 {recipients.Length}명에게 발동";
        return true;
    }

    private static bool RequireNonEmpty(
        CharacterActor[] recipients,
        out string message)
    {
        if (recipients != null && recipients.Length > 0)
        {
            message = string.Empty;
            return true;
        }
        message = "범위 안에 합법 대상이 없습니다.";
        return false;
    }

    private static CharacterSkillInstance FindSkill(
        CharacterActor source,
        string skillId) => source?.Progression?.ActiveSkills
        .FirstOrDefault(value => value != null
            && string.Equals(value.id, skillId, StringComparison.Ordinal));

    private static bool IsManualWorkSkill(CharacterSkillInstance skill) =>
        skill != null
        && skill.IsReady
        && skill.kind == CharacterSkillKind.Active
        && skill.trigger == CharacterSkillTrigger.ManualWork;

    private static void RequireValidSkill(CharacterSkillInstance skill)
    {
        CharacterSkillAreaRules.RequireValid(
            skill.targetingMode,
            skill.effectArea,
            skill.areaSize);
        if (skill.manualDurationHours < 1 || skill.manualCooldownDays < 1)
            throw new InvalidOperationException(
                "수동 작업 능력에는 지속시간과 일 단위 재사용 대기가 필요합니다.");
        bool self = skill.targetingMode == CharacterSkillTargetingMode.Self;
        if ((self && (skill.target != CharacterSkillTarget.Self
                || skill.effectArea != CharacterSkillEffectArea.Single))
            || (!self && skill.target != CharacterSkillTarget.Ally))
            throw new InvalidOperationException(
                "수동 작업 능력의 대상 종류와 획득 방식이 일치하지 않습니다.");
    }

    private static CharacterSkillUseLimitState RequireLimits(
        CharacterProgression progression)
    {
        if (progression?.GrowthState == null)
            throw new InvalidOperationException("캐릭터 성장 상태가 없습니다.");
        progression.GrowthState.useLimits ??= new CharacterSkillUseLimitState();
        return progression.GrowthState.useLimits;
    }

    private static CharacterManualSkillCooldownState FindCooldown(
        CharacterSkillUseLimitState limits,
        string skillId) => (limits.manualSkillCooldowns
            ??= new List<CharacterManualSkillCooldownState>())
            .SingleOrDefault(value => value != null
                && string.Equals(value.skillId, skillId, StringComparison.Ordinal));
}
