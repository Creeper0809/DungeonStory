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
        return Mathf.Max(0.1f, actor != null ? actor.GetMoveSpeed() : fallback);
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
