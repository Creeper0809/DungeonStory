public static class CharacterWorkRoleUtility
{
    public static bool TryGetWork(CharacterActor actor, out AbilityWork work)
    {
        work = null;
        return actor != null
            && actor.TryGetAbility(out work)
            && work != null;
    }

    public static bool IsWorker(CharacterActor actor)
    {
        return TryGetWork(actor, out _);
    }

    public static bool IsOnDutyWorker(CharacterActor actor)
    {
        return TryGetWork(actor, out AbilityWork work)
            && !work.IsOffDuty;
    }

}
