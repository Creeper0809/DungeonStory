using System;
using System.Collections;
using DungeonStory.Foundation;
using UnityEngine;

// Frame-time interpolation is separate from AbilityMove's action/lifecycle ownership.
// The owner supplies cancellation, admission and one position commit; this runner
// never changes an action, lifecycle, path or inventory on its own.
internal static class CharacterWorldPositionMovement
{
    internal static IEnumerator WaitForDelay(IGameClock clock, float duration, Func<bool> cancelled)
    {
        float timer = 0f;
        while (timer < duration)
        {
            if (cancelled()) yield break;
            timer += clock.DeltaTime;
            yield return null;
        }
    }

    internal static IEnumerator Execute(CharacterActor actor, IGameClock clock,
        ICharacterAiSchedulingService scheduler, Vector3 end, float speed,
        Func<bool> cancelled, Func<bool> blocked, Func<Vector3, bool> commit,
        Action<GridMoveFailureReason> fail)
    {
        if (cancelled()) { fail(GridMoveFailureReason.Cancelled); yield break; }
        if (speed <= 0f) { fail(GridMoveFailureReason.InvalidSpeed); yield break; }
        Vector3 start = actor.transform.position;
        CharacterMovementKinematics.UpdateFacing(actor, end.x - start.x);
        float duration = Vector3.Distance(start, end) / speed;
        float timer = 0f;
        while (timer < duration)
        {
            if (cancelled()) { fail(GridMoveFailureReason.Cancelled); yield break; }
            if (blocked() || !commit(Vector3.Lerp(start, end, timer / duration))) yield break;
            timer += clock.DeltaTime;
            int stride = scheduler.GetMovementFrameStride(actor);
            for (int i = 1; i < stride && timer < duration; i++)
            {
                yield return null;
                timer += clock.DeltaTime;
                if (cancelled()) { fail(GridMoveFailureReason.Cancelled); yield break; }
                if (blocked()) yield break;
            }
            yield return null;
        }
        if (cancelled()) { fail(GridMoveFailureReason.Cancelled); yield break; }
        if (!blocked()) commit(end);
    }

    internal static IEnumerator WaitForDoor(AbilityMove move, CharacterActor actor, IGameClock clock,
        Vector3 destination, float speed, Func<bool> cancelled)
    {
        do
        {
            if (cancelled()) { move.MarkGridMoveFailure(GridMoveFailureReason.Cancelled); yield break; }
            yield return move.Move2PosBySpeed(destination, speed);
            if (move.LastGridMoveFailureReason != GridMoveFailureReason.DoorDenied) yield break;
            // Explicit transit wait: retain Entering/Departing/Returning ownership.
            // A denied passage is observable and is never substituted with teleport.
            float retryAt = clock.Time + 1f;
            while (clock.Time < retryAt && !cancelled()) yield return null;
        } while (actor != null);
        move.MarkGridMoveFailure(GridMoveFailureReason.Cancelled);
    }
}
