using System.Collections;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class CombatPlayerCommandPlayModeVerifier
{
    private static string report = "not-started";

    public static string GetReport()
    {
        return report;
    }

    [MenuItem("DungeonStory/Debug/Combat/Verify Player Grid Move")]
    public static void StartVerification()
    {
        if (!Application.isPlaying)
        {
            report = "failed: enter PlayMode first";
            Debug.LogError(report);
            return;
        }

        GameObject host = new GameObject("Combat Player Command Verifier");
        host.hideFlags = HideFlags.HideAndDontSave;
        host.AddComponent<Runner>().Begin();
    }

    private sealed class Runner : MonoBehaviour
    {
        public void Begin()
        {
            StartCoroutine(Verify());
        }

        private IEnumerator Verify()
        {
            yield return null;
            OwnerCommandController controller =
                Object.FindFirstObjectByType<OwnerCommandController>();
            CharacterActor actor = Object.FindObjectsByType<CharacterActor>(
                    FindObjectsInactive.Exclude,
                    FindObjectsSortMode.None)
                .Select(CharacterActorCollection.GetCanonical)
                .FirstOrDefault(candidate =>
                    candidate != null
                    && !candidate.IsDead
                    && !candidate.IsOwner
                    && candidate.CanRunAi
                    && candidate.TryGetAbility(out AbilityMove _)
                    && candidate.TryGetAbility(out AbilityWork _));
            GridSystemManager gridManager = Object.FindFirstObjectByType<GridSystemManager>(
                FindObjectsInactive.Include);
            Grid grid = gridManager != null ? gridManager.grid : null;
            if (controller == null || actor == null
                || grid == null)
            {
                Finish("failed: controller, actor, or grid missing");
                yield break;
            }

            Vector2Int start = actor.GetNowXY();
            GridPathSearchResult search = grid.SearchPath(start);
            Vector2Int[] candidates = search.GetReachablePositions()
                .Where(position => position != start
                    && position.y == start.y
                    && grid.IsWalkable(position)
                    && Mathf.Abs(position.x - start.x) + Mathf.Abs(position.y - start.y) >= 2)
                .OrderBy(position =>
                    Mathf.Abs(position.x - start.x) + Mathf.Abs(position.y - start.y))
                .ThenBy(position => position.y)
                .ThenBy(position => position.x)
                .ToArray();
            if (candidates.Length == 0)
            {
                Finish($"failed: no reachable target from {start}");
                yield break;
            }
            Vector2Int target = candidates[0];

            bool selected = controller.TrySelectActor(actor, out string selectionMessage);
            string commandMessage = string.Empty;
            bool commanded = selected
                && controller.TryIssueMoveCommand(target, out commandMessage);
            if (!selected || !commanded)
            {
                Finish($"failed: select={selectionMessage}; command={commandMessage}");
                yield break;
            }

            bool manualLockObserved = actor.Brain != null
                && actor.Brain.IsManualCommandActive;
            float timeout = Time.unscaledTime + 10f;
            while (Time.unscaledTime < timeout
                && actor != null
                && actor.Brain != null
                && actor.Brain.IsManualCommandActive)
            {
                yield return null;
            }

            Vector2Int end = actor != null ? actor.GetNowXY() : new Vector2Int(-1, -1);
            bool arrived = actor != null && end == target;
            bool released = actor?.Brain != null && !actor.Brain.IsManualCommandActive;
            bool cancelCommanded = controller.TryIssueMoveCommand(start, out string cancelMessage);
            bool cancelLockObserved = cancelCommanded
                && actor.Brain != null
                && actor.Brain.IsManualCommandActive;
            actor.TryGetAbility(out AbilityMove move);
            move?.CancelActiveMovement();
            bool cancelReleased = actor.Brain != null && !actor.Brain.IsManualCommandActive;
            Finish(
                $"completed={arrived && released && manualLockObserved && cancelLockObserved && cancelReleased}; "
                + $"actor={actor?.name}; start={start}; target={target}; end={end}; "
                + $"manualLock={manualLockObserved}; released={released}; command={commandMessage}; "
                + $"cancelLock={cancelLockObserved}; cancelReleased={cancelReleased}; cancel={cancelMessage}");
        }

        private void Finish(string value)
        {
            report = value;
            Debug.Log($"[CombatPlayerCommandPlayModeVerifier] {value}");
            Destroy(gameObject);
        }
    }
}
