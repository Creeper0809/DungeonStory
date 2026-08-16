public static class CharacterWorkRoleUtility
{
#if UNITY_EDITOR
    private static readonly System.Collections.Generic.HashSet<int>
        debugVisitorActorIds = new System.Collections.Generic.HashSet<int>();
#endif

    public static bool TryGetWork(CharacterActor actor, out AbilityWork work)
    {
        work = null;
        // AbilityWork is injected on the shared character prefab. A live
        // Customer therefore still has the component, but its authoritative
        // population role is visitor, not worker. Treating component presence
        // as staff ownership removes Shopping/LookAround/Exit from the real
        // visitor catalog and lets customers accumulate until deprivation
        // breakdown. Promoted population staff are projected to NPC by
        // CharacterPopulationService.ApplyStaffRuntimeState before this query.
        if (actor?.Identity?.CharacterType == CharacterType.Customer)
        {
            return false;
        }
#if UNITY_EDITOR
        if (actor != null && debugVisitorActorIds.Contains(actor.GetInstanceID()))
        {
            return false;
        }
#endif
        return actor != null
            && actor.TryGetAbility(out work)
            && work != null;
    }

#if UNITY_EDITOR
    /// <summary>
    /// Verifier-only role projection. This leaves the real AbilityWork component
    /// and its state untouched while production visitor policies and action
    /// definitions are exercised against an existing live Brain.
    /// </summary>
    public static System.IDisposable DebugProjectAsVisitor(CharacterActor actor)
    {
        if (actor == null)
            throw new System.ArgumentNullException(nameof(actor));
        int id = actor.GetInstanceID();
        debugVisitorActorIds.Add(id);
        return new DebugVisitorRoleLease(id);
    }

    private sealed class DebugVisitorRoleLease : System.IDisposable
    {
        private readonly int actorId;
        private bool disposed;

        public DebugVisitorRoleLease(int actorId)
        {
            this.actorId = actorId;
        }

        public void Dispose()
        {
            if (disposed)
                return;
            disposed = true;
            debugVisitorActorIds.Remove(actorId);
        }
    }
#endif

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
