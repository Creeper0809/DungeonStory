using UnityEngine;

internal static class GridMoveBlockedResponder
{
    public static void Respond(
        CharacterActor actor,
        Grid grid,
        Vector3 worldPosition)
    {
        actor?.AiMemory?.RecordMovement(
            grid != null ? grid.GetXY(worldPosition) : Vector2Int.zero,
            0f,
            false,
            "길 막힘");
        if (actor?.Brain == null)
        {
            return;
        }

        actor.Brain.ClearPathSearchCache();
        if (actor.Brain.bestAction != null)
        {
            actor.Brain.SetActionPhase("이동 막힘", actor.Brain.bestAction.destination);
        }
    }
}
