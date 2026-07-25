internal sealed class ProbeExitDungeonActionSet : AIExitDungeon
{
    public float probeScore = 1f;
    public override bool RequiresDestination => false;

    public override bool CanStart(CharacterActor actor)
    {
        return actor != null;
    }

    public override float AdjustScore(CharacterActor actor, float baseScore)
    {
        return probeScore;
    }

    public override void Execute(CharacterActor actor)
    {
        actor?.AddLog("Probe survival action executed.");
        if (actor != null && actor.Brain != null)
        {
            actor.Brain.isBestActionEnd = false;
        }
    }
}

internal sealed class ProbeEatActionSet : AIEat
{
    public float probeScore = 1f;
    public override bool RequiresDestination => false;

    public override bool CanStart(CharacterActor actor)
    {
        return actor != null;
    }

    public override float AdjustScore(CharacterActor actor, float baseScore)
    {
        return probeScore;
    }

    public override bool TryResolveDestinationWithFailure(
        CharacterActor actor,
        GridPathSearchResult searchResult,
        out BuildableObject destination,
        out AIActionFailure failure)
    {
        destination = null;
        failure = AIActionFailure.None;
        return true;
    }

    public override void Execute(CharacterActor actor)
    {
        actor?.AddLog("Probe food action executed.");
        if (actor != null && actor.Brain != null)
        {
            actor.Brain.isBestActionEnd = false;
        }
    }
}

internal sealed class ProbeWorkActionSet : AIWork
{
    public float probeScore = 1f;
    public override bool RequiresDestination => false;

    public override bool CanStart(CharacterActor actor)
    {
        return actor != null;
    }

    public override float AdjustScore(CharacterActor actor, float baseScore)
    {
        return probeScore;
    }

    public override void Execute(CharacterActor actor)
    {
        actor?.AddLog("Probe work action executed.");
        if (actor != null && actor.Brain != null)
        {
            actor.Brain.isBestActionEnd = false;
        }
    }
}

internal sealed class ProbeShoppingActionSet : AIShopping
{
    public float probeScore = 1f;
    public override bool RequiresDestination => false;

    public override bool CanStart(CharacterActor actor)
    {
        return actor != null;
    }

    public override float AdjustScore(CharacterActor actor, float baseScore)
    {
        return probeScore;
    }

    public override bool TryResolveDestinationWithFailure(
        CharacterActor actor,
        GridPathSearchResult searchResult,
        out BuildableObject destination,
        out AIActionFailure failure)
    {
        destination = null;
        failure = AIActionFailure.None;
        return true;
    }

    public override void Execute(CharacterActor actor)
    {
        actor?.AddLog("Probe shopping action executed.");
        if (actor != null && actor.Brain != null)
        {
            actor.Brain.isBestActionEnd = false;
        }
    }
}

internal sealed class ProbeLookAroundActionSet : AILookAround
{
    public float probeScore = 1f;
    public override bool RequiresDestination => false;

    public override bool CanStart(CharacterActor actor)
    {
        return actor != null;
    }

    public override float AdjustScore(CharacterActor actor, float baseScore)
    {
        return probeScore;
    }

    public override void Execute(CharacterActor actor)
    {
        actor?.AddLog("Probe look-around action executed.");
        if (actor != null && actor.Brain != null)
        {
            actor.Brain.isBestActionEnd = false;
        }
    }
}

internal sealed class ProbeWaitActionSet : AIWait
{
    public float probeScore = 1f;
    public override bool RequiresDestination => false;

    public override bool CanStart(CharacterActor actor)
    {
        return actor != null;
    }

    public override float AdjustScore(CharacterActor actor, float baseScore)
    {
        return probeScore;
    }

    public override void Execute(CharacterActor actor)
    {
        actor?.AddLog("Probe wait action executed.");
        if (actor != null && actor.Brain != null)
        {
            actor.Brain.isBestActionEnd = false;
        }
    }
}

internal sealed class ProbeVolatileWorkActionSet : AIWork
{
    public float highScore = 1f;
    public float laterScore = 0.1f;
    public int highScoreEvaluationCount = 1;
    public int ScoreRequestCount { get; private set; }
    public override bool RequiresDestination => false;

    public override bool CanStart(CharacterActor actor)
    {
        return actor != null;
    }

    public override float AdjustScore(CharacterActor actor, float baseScore)
    {
        ScoreRequestCount++;
        return ScoreRequestCount <= highScoreEvaluationCount
            ? highScore
            : laterScore;
    }

    public override void Execute(CharacterActor actor)
    {
        actor?.AddLog("Probe volatile work action executed.");
        if (actor != null && actor.Brain != null)
        {
            actor.Brain.isBestActionEnd = false;
        }
    }
}

internal sealed class ProbeContinuableWorkActionSet : AIWork
{
    public float highScore = 1f;
    public float laterScore = 0.1f;
    public int highScoreEvaluationCount = 1;
    public bool canContinue = true;
    public int ScoreRequestCount { get; private set; }
    public bool StopCalled { get; private set; }
    public override bool RequiresDestination => false;
    public override float MinimumDuration => 0f;

    public override bool CanStart(CharacterActor actor)
    {
        return actor != null;
    }

    public override float AdjustScore(CharacterActor actor, float baseScore)
    {
        ScoreRequestCount++;
        return ScoreRequestCount <= highScoreEvaluationCount
            ? highScore
            : laterScore;
    }

    public override bool CanContinue(CharacterActor actor, AIAction runningAction, out string stopReason)
    {
        stopReason = canContinue ? string.Empty : "Probe requested stop.";
        return canContinue;
    }

    public override bool CanInterrupt(CharacterActor actor, AIAction runningAction, out string interruptReason)
    {
        interruptReason = string.Empty;
        return false;
    }

    public override void Execute(CharacterActor actor)
    {
        actor?.AddLog("Probe continuable work action executed.");
        if (actor != null && actor.TryGetAbility(out AbilityWork work))
        {
            work.isWorking = true;
        }

        if (actor != null && actor.Brain != null)
        {
            actor.Brain.isBestActionEnd = false;
        }
    }

    public override void OnStop(CharacterActor actor, AIAction runningAction, string reason)
    {
        StopCalled = true;
        base.OnStop(actor, runningAction, reason);
    }
}

internal sealed class ProbeOneShotWorkActionSet : AIWork
{
    public bool canStart = true;
    public override bool RequiresDestination => false;

    public override bool CanStart(CharacterActor actor)
    {
        return actor != null && canStart;
    }

    public override float AdjustScore(CharacterActor actor, float baseScore)
    {
        return 1f;
    }

    public override void Execute(CharacterActor actor)
    {
        actor?.AddLog("Probe one-shot work action executed.");
        if (actor != null && actor.Brain != null)
        {
            actor.Brain.isBestActionEnd = false;
        }
    }
}
