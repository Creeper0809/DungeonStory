using System;
using UnityEngine;

public interface ICharacterMovementKinematicsActor
{
    float GetMoveSpeed();
    void Flip(CharacterFacing facing);
}

public static class CharacterMovementKinematics
{
    public static float GetMoveSpeed(
        ICharacterMovementKinematicsActor actor,
        float fallback)
    {
        float speed = actor != null ? actor.GetMoveSpeed() : fallback;
        if (float.IsNaN(speed) || float.IsInfinity(speed) || speed < 0f)
        {
            throw new ArgumentOutOfRangeException(
                nameof(speed),
                speed,
                "Character movement speed must be finite and non-negative.");
        }

        return speed;
    }

    public static void UpdateFacing(
        ICharacterMovementKinematicsActor actor,
        float deltaX)
    {
        if (actor == null || Mathf.Abs(deltaX) <= 0.001f)
        {
            return;
        }

        actor.Flip(deltaX > 0f ? CharacterFacing.RIGHT : CharacterFacing.LEFT);
    }
}
