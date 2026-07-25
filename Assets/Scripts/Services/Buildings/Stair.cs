using System.Collections;
using UnityEngine;

public class Stair : BuildableObject, IInteractable, IGridMovementHandler
{
    private const float EnterSpeedMultiplier = 0.9f;
    private const float DefaultHiddenTravelDelay = 2f;
    private const float ReappearDelay = 0.06f;

    public override GridMoveType GridMoveType => GridMoveType.Stair;

    public IEnumerator Traverse(CharacterActor actor, GridMoveStep step)
    {
        if (actor == null || step == null) yield break;

        AbilityMove moveable = actor.GetAbility<AbilityMove>();
        if (moveable == null || grid == null) yield break;

        Vector3 fromAnchor = GetFloorCenterAnchor(step.From);
        Vector3 toAnchor = GetFloorCenterAnchor(step.To);
        fromAnchor.z = actor.transform.position.z;
        toAnchor.z = actor.transform.position.z;

        if ((actor.transform.position - fromAnchor).sqrMagnitude > 0.01f)
        {
            yield return moveable.Move2PosBySpeed(fromAnchor, EnterSpeedMultiplier);
        }

        float hiddenTravelDelay = GetHiddenTravelDelay();
        float failSafeDelay = hiddenTravelDelay + ReappearDelay + 0.5f;
        actor.HideForTraversal(failSafeDelay);

        try
        {
            yield return new WaitForSeconds(hiddenTravelDelay);
            actor.transform.position = toAnchor;
            yield return new WaitForSeconds(ReappearDelay);
        }
        finally
        {
            actor.RestoreTraversalVisibility();
        }
    }

    public IEnumerator Interact(CharacterActor actor)
    {
        if (actor == null || grid == null) yield break;

        Vector2Int from = grid.GetXY(actor.transform.position);
        Vector2Int to = from;
        GridCell cell = grid.GetGridCell(from);
        if (cell != null)
        {
            foreach (GridTraversalLink link in cell.TraversalLinks)
            {
                if (ReferenceEquals(link.Through, this))
                {
                    to = link.To;
                    break;
                }
            }
        }

        if (to == from)
        {
            to = new Vector2Int(from.x, Mathf.Clamp(from.y + 1, 0, grid.height - 1));
        }

        yield return Traverse(actor, new GridMoveStep(from, to, this, this, GridMoveType.Stair));
    }

    private float GetHiddenTravelDelay()
    {
        return BuildingData != null
            ? Mathf.Max(0f, BuildingData.movementTravelTime)
            : DefaultHiddenTravelDelay;
    }

    private Vector3 GetFloorCenterAnchor(Vector2Int fallbackPosition)
    {
        if (grid == null)
        {
            return transform.position;
        }

        if (buildPoses == null || buildPoses.Count == 0)
        {
            return GetMovementWorldPosition(fallbackPosition);
        }

        bool found = false;
        int minX = int.MaxValue;
        int maxX = int.MinValue;
        foreach (Vector2Int position in buildPoses)
        {
            if (position.y != fallbackPosition.y) continue;

            found = true;
            minX = Mathf.Min(minX, position.x);
            maxX = Mathf.Max(maxX, position.x);
        }

        if (!found)
        {
            return GetMovementWorldPosition(fallbackPosition);
        }

        Vector3 anchor = grid.GetWorldPos(new Vector2((minX + maxX) * 0.5f, fallbackPosition.y));
        if (BuildingData != null)
        {
            anchor += (Vector3)BuildingData.movementAnchorOffset;
        }

        return anchor;
    }

}
