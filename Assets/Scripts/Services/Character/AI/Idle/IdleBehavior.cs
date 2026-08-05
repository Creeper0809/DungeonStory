using UnityEngine;

public interface IIdleBehavior
{
    string DisplayName { get; }
    bool UsesMovement { get; }
    bool CanRun(CharacterActor actor);
    bool TryRun(CharacterActor actor, float duration, out string failureReason);
}

public sealed class StaffWanderIdleBehavior : IIdleBehavior
{
    public string DisplayName => "주변 배회";
    public bool UsesMovement => true;

    public bool CanRun(CharacterActor actor)
    {
        return actor != null && actor.TryGetAbility(out AbilityMove _);
    }

    public bool TryRun(CharacterActor actor, float duration, out string failureReason)
    {
        failureReason = string.Empty;
        if (!CanRun(actor))
        {
            failureReason = "던전 배회 조건 불만족";
            return false;
        }

        AbilityMove move = actor.GetAbility<AbilityMove>();
        if (move != null && move.StartIdleWander(duration))
        {
            return true;
        }

        failureReason = "던전 배회 위치 없음";
        return false;
    }
}

public sealed class StaticWaitIdleBehavior : IIdleBehavior
{
    public string DisplayName => "제자리 대기";
    public bool UsesMovement => false;

    public bool CanRun(CharacterActor actor)
    {
        return actor != null && actor.TryGetAbility(out AbilityMove _);
    }

    public bool TryRun(CharacterActor actor, float duration, out string failureReason)
    {
        failureReason = string.Empty;
        if (!CanRun(actor))
        {
            failureReason = "이동 능력 없음";
            return false;
        }

        actor.GetAbility<AbilityMove>().StartWait(duration);
        return true;
    }
}

public sealed class QueueWaitIdleBehavior : IIdleBehavior
{
    public string DisplayName => "줄 서는 중";
    public bool UsesMovement => false;

    public bool CanRun(CharacterActor actor)
    {
        return actor != null && actor.TryGetAbility(out AbilityMove _);
    }

    public bool TryRun(CharacterActor actor, float duration, out string failureReason)
    {
        failureReason = string.Empty;
        if (!CanRun(actor))
        {
            failureReason = "줄 서기 조건 불만족";
            return false;
        }

        actor.GetAbility<AbilityMove>().StartWait(Mathf.Clamp(duration, 0.5f, 1.6f));
        return true;
    }
}

public sealed class InspectFacilityIdleBehavior : IIdleBehavior
{
    public string DisplayName => "주변 살피기";
    public bool UsesMovement => true;

    public bool CanRun(CharacterActor actor)
    {
        return actor != null && actor.TryGetAbility(out AbilityMove _);
    }

    public bool TryRun(CharacterActor actor, float duration, out string failureReason)
    {
        failureReason = string.Empty;
        if (!CanRun(actor))
        {
            failureReason = "살펴볼 여유 없음";
            return false;
        }

        AbilityMove move = actor.GetAbility<AbilityMove>();
        if (move != null && move.StartIdleWander(Mathf.Clamp(duration, 0.6f, 1.4f), 1, 3))
        {
            return true;
        }

        failureReason = "살펴볼 수 있는 주변 칸 없음";
        return false;
    }
}

public sealed class ShortChatIdleBehavior : IIdleBehavior
{
    public string DisplayName => "짧은 대화";
    public bool UsesMovement => false;

    public bool CanRun(CharacterActor actor)
    {
        CharacterAiWorldSignalSnapshot signals = actor?.WorldSignalQuery?.Capture(
                actor,
                CharacterAiBranch.Idle)
            ?? CharacterAiWorldSignalSnapshot.Neutral;
        return actor != null
            && signals.NearbyCharacters > 0
            && actor.TryGetAbility(out AbilityMove _);
    }

    public bool TryRun(CharacterActor actor, float duration, out string failureReason)
    {
        failureReason = string.Empty;
        if (!CanRun(actor))
        {
            failureReason = "대화할 상대 없음";
            return false;
        }

        actor.GetAbility<AbilityMove>().StartWait(Mathf.Clamp(duration, 0.7f, 1.8f));
        return true;
    }
}

public sealed class ShelterFromWeatherIdleBehavior : IIdleBehavior
{
    public string DisplayName => "날씨 피하기";
    public bool UsesMovement => false;

    public bool CanRun(CharacterActor actor)
    {
        CharacterAiWorldSignalSnapshot signals = actor?.WorldSignalQuery?.Capture(
                actor,
                CharacterAiBranch.Idle)
            ?? CharacterAiWorldSignalSnapshot.Neutral;
        return actor != null
            && signals.WeatherPressure >= CharacterAiNaturalnessSettingsResolver.Require(actor).ShelterWeatherThreshold
            && actor.TryGetAbility(out AbilityMove _);
    }

    public bool TryRun(CharacterActor actor, float duration, out string failureReason)
    {
        failureReason = string.Empty;
        if (!CanRun(actor))
        {
            failureReason = "피할 날씨 아님";
            return false;
        }

        actor.GetAbility<AbilityMove>().StartWait(Mathf.Clamp(duration, 0.8f, 2f));
        return true;
    }
}

public sealed class StepAsideIdleBehavior : IIdleBehavior
{
    public string DisplayName => "길 비켜주기";
    public bool UsesMovement => true;

    public bool CanRun(CharacterActor actor)
    {
        return actor != null && actor.TryGetAbility(out AbilityMove _);
    }

    public bool TryRun(CharacterActor actor, float duration, out string failureReason)
    {
        failureReason = string.Empty;
        if (!CanRun(actor))
        {
            failureReason = "비켜설 수 없음";
            return false;
        }

        AbilityMove move = actor.GetAbility<AbilityMove>();
        if (move != null && move.StartIdleWander(Mathf.Clamp(duration, 0.5f, 1.2f)))
        {
            return true;
        }

        move?.StartWait(Mathf.Clamp(duration, 0.35f, 0.8f));
        return true;
    }
}

public sealed class MoodDrivenWanderIdleBehavior : IIdleBehavior
{
    public string DisplayName => "기분 내키는 대로 배회";
    public bool UsesMovement => true;

    public bool CanRun(CharacterActor actor)
    {
        return CharacterMoodImpulseUtility.ShouldPreferAutonomousIdle(actor, out _)
            && actor.TryGetAbility(out AbilityMove _);
    }

    public bool TryRun(CharacterActor actor, float duration, out string failureReason)
    {
        failureReason = string.Empty;
        if (!CanRun(actor))
        {
            failureReason = "자율 행동 조건 불만족";
            return false;
        }

        AbilityMove move = actor.GetAbility<AbilityMove>();
        if (move != null && move.StartIdleWander(Mathf.Clamp(duration, 0.5f, 1.2f), 2, 7))
        {
            return true;
        }

        failureReason = "자율 행동으로 이동할 칸 없음";
        return false;
    }
}

public static class IdleBehaviorRunner
{
    private static readonly IIdleBehavior StaffWander = new StaffWanderIdleBehavior();
    private static readonly IIdleBehavior StaticWait = new StaticWaitIdleBehavior();
    private static readonly IIdleBehavior QueueWait = new QueueWaitIdleBehavior();
    private static readonly IIdleBehavior InspectFacility = new InspectFacilityIdleBehavior();
    private static readonly IIdleBehavior ShortChat = new ShortChatIdleBehavior();
    private static readonly IIdleBehavior ShelterFromWeather = new ShelterFromWeatherIdleBehavior();
    private static readonly IIdleBehavior StepAside = new StepAsideIdleBehavior();
    private static readonly IIdleBehavior MoodDrivenWander = new MoodDrivenWanderIdleBehavior();

    public static bool TryRunDefault(
        CharacterActor actor,
        float duration,
        bool allowMovement,
        out string behaviorName,
        out string failureReason)
    {
        behaviorName = string.Empty;
        failureReason = string.Empty;

        IIdleBehavior behavior = SelectBehavior(actor, allowMovement);
        if (behavior == null)
        {
            failureReason = "No idle behavior configured for current state.";
            return false;
        }

        if (!behavior.CanRun(actor))
        {
            failureReason = $"{behavior.DisplayName} cannot run.";
            return false;
        }

        if (!behavior.TryRun(actor, duration, out failureReason))
        {
            string movementFailure = string.Empty;
            if (!allowMovement
                || ReferenceEquals(behavior, StaffWander)
                || !StaffWander.CanRun(actor)
                || !StaffWander.TryRun(actor, duration, out movementFailure))
            {
                if (!string.IsNullOrWhiteSpace(movementFailure))
                {
                    failureReason = string.IsNullOrWhiteSpace(failureReason)
                        ? movementFailure
                        : $"{failureReason}; {movementFailure}";
                }
                return false;
            }

            behavior = StaffWander;
        }

        behaviorName = behavior.DisplayName;
        return true;
    }

    public static bool TryRunStatic(
        CharacterActor actor,
        float duration,
        out string behaviorName,
        out string failureReason)
    {
        behaviorName = string.Empty;
        failureReason = string.Empty;
        if (!StaticWait.TryRun(actor, duration, out failureReason))
        {
            return false;
        }

        behaviorName = StaticWait.DisplayName;
        return true;
    }

    public static string GetSelectedBehaviorTypeNameForDebug(CharacterActor actor, bool allowMovement)
    {
        IIdleBehavior behavior = SelectBehavior(actor, allowMovement);
        return behavior != null ? behavior.GetType().Name : string.Empty;
    }

    public static bool IsSelectedBehaviorMovementBasedForDebug(CharacterActor actor, bool allowMovement)
    {
        IIdleBehavior behavior = SelectBehavior(actor, allowMovement);
        return behavior != null && behavior.UsesMovement;
    }

    private static IIdleBehavior SelectBehavior(CharacterActor actor, bool allowMovement)
    {
        if (!allowMovement)
        {
            return StaticWait;
        }

        CharacterAiWorldSignalSnapshot signals = actor?.WorldSignalQuery?.Capture(
                actor,
                CharacterAiBranch.Idle)
            ?? CharacterAiWorldSignalSnapshot.Neutral;
        CharacterAiNaturalnessSettingsSO settings =
            CharacterAiNaturalnessSettingsResolver.Require(actor);
        if (CharacterMoodImpulseUtility.ShouldPreferAutonomousIdle(actor, out _))
        {
            return MoodDrivenWander;
        }

        if (signals.RecentFailurePressure >= settings.StepAsideFailureThreshold)
        {
            return StepAside;
        }

        if (signals.QueuePressure >= settings.QueueWaitThreshold)
        {
            return QueueWait;
        }

        if (signals.WeatherPressure >= settings.ShelterWeatherThreshold)
        {
            return ShelterFromWeather;
        }

        if (signals.NearbyCharacters > 0 && signals.SocialOpportunity >= 0.25f)
        {
            return ShortChat;
        }

        if (CharacterWorkRoleUtility.TryGetWork(actor, out _))
        {
            return signals.RecentMovementPressure > 0.55f ? InspectFacility : StaffWander;
        }

        return signals.SocialOpportunity > 0.1f ? InspectFacility : StaffWander;
    }
}
