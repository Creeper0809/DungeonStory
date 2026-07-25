public static class WorkDebugLog
{
    public static void LogStarted(CharacterActor actor)
    {
        if (TryGetWorkContext(actor, out AbilityWork work, out BuildableObject target, out string context))
        {
            actor.AddActivity(CharacterActivityEvent.Work(
                work.AssignedWorkType,
                CharacterActivityOutcomes.Started,
                $"작업 시작 · {context}",
                target));
        }
    }

    public static void LogEnd(CharacterActor actor, string reason = null)
    {
        if (!TryGetWorkContext(actor, out AbilityWork work, out BuildableObject target, out string context))
        {
            return;
        }

        string message = $"작업 종료 · {context}";
        if (!string.IsNullOrWhiteSpace(reason))
        {
            message += $" · {reason}";
        }

        actor?.AddActivity(CharacterActivityEvent.Work(
            work.AssignedWorkType,
            CharacterActivityOutcomes.Completed,
            message,
            target,
            reasonCode: reason));
    }

    private static bool TryGetWorkContext(
        CharacterActor actor,
        out AbilityWork work,
        out BuildableObject target,
        out string context)
    {
        work = null;
        if (actor == null || !actor.TryGetAbility(out work))
        {
            target = null;
            context = string.Empty;
            return false;
        }

        if (work.AssignedWorkType == FacilityWorkType.None || work.assignedShop == null)
        {
            target = null;
            context = string.Empty;
            return false;
        }

        string workName = work.AssignedWorkDisplayName;
        target = work.assignedShop;
        string targetName = target != null && target.BuildingData != null
            && !string.IsNullOrWhiteSpace(target.BuildingData.objectName)
                ? target.BuildingData.objectName
                : target.name;

        context = $"{workName} · {targetName}";
        return true;
    }
}
