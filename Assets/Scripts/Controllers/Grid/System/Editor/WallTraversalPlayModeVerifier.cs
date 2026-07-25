using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class WallTraversalPlayModeVerifier
{
    public const string ReportPath = "Temp/wall-traversal-report.txt";
    public const string CameraCapturePath = "Temp/wall-traversal-blocked-camera.png";
    public const string ScreenCapturePath = "Temp/wall-traversal-blocked-screen.png";

    [MenuItem("DungeonStory/Debug/Grid Foundation/Run Wall Traversal PlayMode Verification")]
    public static void RunFromMenu()
    {
        if (!EditorApplication.isPlaying)
        {
            Debug.LogError("Wall traversal verification requires PlayMode.");
            return;
        }

        if (Object.FindFirstObjectByType<WallTraversalPlayModeVerificationRunner>() != null)
        {
            Debug.LogWarning("Wall traversal verification is already running.");
            return;
        }

        new GameObject("Wall Traversal PlayMode Verification Runner")
            .AddComponent<WallTraversalPlayModeVerificationRunner>();
    }
}

public sealed class WallTraversalPlayModeVerificationRunner : MonoBehaviour
{
    private readonly List<string> report = new List<string>();
    private readonly List<string> failures = new List<string>();
    private readonly List<string> capturedErrors = new List<string>();
    private readonly List<string> capturedWarnings = new List<string>();

    private IEnumerator Start()
    {
        Directory.CreateDirectory("Temp");
        Application.logMessageReceived += OnLogMessageReceived;
        yield return null;
        yield return null;

        GridSystemManager gridManager = Object.FindFirstObjectByType<GridSystemManager>();
        CharacterActor actor = Object.FindObjectsByType<CharacterActor>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None)
            .FirstOrDefault(candidate => candidate != null
                && candidate.CurrentLifecycleState == CharacterLifecycleState.Active
                && candidate.GetAbility<AbilityMove>() != null);
        BuildingSO wallData = AssetDatabase.LoadAssetAtPath<BuildingSO>(
            "Assets/Resources/SO/Building/Wall.asset");
        Camera mainCamera = Camera.main;

        Check(gridManager != null, "GRID_MANAGER", "runtime grid manager resolved");
        Check(actor != null, "ACTIVE_ACTOR", "active character with AbilityMove resolved");
        Check(wallData != null && wallData.IsStructuralWall, "WALL_DATA", "structural wall data resolved");
        Check(mainCamera != null, "MAIN_CAMERA", "main camera resolved");
        if (gridManager == null || actor == null || wallData == null || mainCamera == null)
        {
            Finish();
            yield break;
        }

        Grid grid = gridManager.grid;
        Check(TryFindTraversalPair(grid, out Vector2Int start, out Vector2Int target),
            "TRAVERSAL_PAIR",
            "adjacent walkable start and empty wall target resolved");
        if (failures.Count > 0)
        {
            Finish();
            yield break;
        }

        AbilityMove move = actor.GetAbility<AbilityMove>();
        AIBrain brain = actor.Brain;
        BehaviorDesigner.Runtime.BehaviorTree behaviorTree =
            actor.GetComponent<BehaviorDesigner.Runtime.BehaviorTree>();
        Vector3 originalPosition = actor.transform.position;
        CharacterLifecycleState originalLifecycle = actor.CurrentLifecycleState;
        bool brainWasEnabled = brain != null && brain.enabled;
        bool behaviorTreeWasEnabled = behaviorTree != null && behaviorTree.enabled;
        BuildableObject wall = null;

        try
        {
            if (brain != null) brain.enabled = false;
            if (behaviorTree != null) behaviorTree.enabled = false;
            actor.SetLifecycleState(CharacterLifecycleState.EnteringDungeon);
            foreach (MonoBehaviour behaviour in actor.GetComponents<MonoBehaviour>())
            {
                behaviour.StopAllCoroutines();
            }

            move.CancelActiveMovement();
            actor.transform.position = grid.GetWorldPos(start);
            yield return null;

            Queue<GridMoveStep> stalePath = grid.GetMovePath(start, pos => pos == target);
            Check(stalePath != null && stalePath.Count == 1,
                "STALE_PATH_READY",
                $"path count before wall={stalePath?.Count ?? -1}");

            StartCoroutine(move.MoveByPath(stalePath));
            bool movementStarted = false;
            for (int i = 0; i < 20; i++)
            {
                yield return null;
                float progress = Vector3.Distance(actor.transform.position, grid.GetWorldPos(start));
                if (progress > 0.01f)
                {
                    movementStarted = grid.GetXY(actor.transform.position) != target;
                    break;
                }
            }
            Check(movementStarted, "MOVEMENT_STARTED", $"position before wall={actor.transform.position:F3}");

            wall = PlaceWall(grid, wallData, target);
            Check(wall != null, "LIVE_WALL_PLACED", $"target={target}");
            gridManager.NotifyGridObjectChanged();

            bool everEnteredWallCell = grid.GetXY(actor.transform.position) == target;
            for (int i = 0; i < 30; i++)
            {
                yield return null;
                everEnteredWallCell |= grid.GetXY(actor.transform.position) == target;
            }

            Vector3 startWorld = grid.GetWorldPos(start);
            Check(grid.IsMovementBlockedByWall(target),
                "TARGET_BLOCKED",
                $"wall occupant={grid.GetGridCell(target)?.GetBuildingInlayer(GridLayer.Building)?.name}");
            Check(!everEnteredWallCell,
                "NO_WALL_CELL_ENTRY",
                $"actor grid={grid.GetXY(actor.transform.position)}; wall grid={target}");
            Check(Vector3.Distance(actor.transform.position, startWorld) <= 0.03f,
                "ROLLBACK_TO_VALID_CELL",
                $"actor={actor.transform.position:F3}; expected={startWorld:F3}");
            Check(move.LastGridMoveWasBlocked,
                "BLOCKED_STEP_REPORTED",
                $"blocked state={move.LastGridMoveWasBlocked}");

            GridMoveStep directStep = new GridMoveStep(
                start,
                target,
                null,
                null,
                GridMoveType.Walk);
            yield return move.MoveByStep(directStep);
            Check(move.LastGridMoveWasBlocked
                    && Vector3.Distance(actor.transform.position, startWorld) <= 0.03f,
                "DIRECT_STEP_REJECTS_WALL",
                $"blocked={move.LastGridMoveWasBlocked}; actor={actor.transform.position:F3}");

            Queue<GridMoveStep> freshPath = grid.GetMovePath(start, pos => pos == target);
            Check(freshPath.Count == 0,
                "FRESH_PATH_REJECTS_WALL_DESTINATION",
                $"path count after wall={freshPath.Count}");

            RemoveWall(gridManager, grid, wall);
            wall = null;
            yield return null;

            actor.SetLifecycleState(CharacterLifecycleState.Active);
            actor.transform.position = grid.GetWorldPos(target);
            wall = PlaceWall(grid, wallData, target);
            Check(wall != null, "OCCUPIED_CELL_WALL_PLACED", $"target={target}");
            gridManager.NotifyGridObjectChanged();
            yield return null;

            Vector2Int recoveredPosition = grid.GetXY(actor.transform.position);
            Check(recoveredPosition != target,
                "OCCUPIED_CELL_EJECTED",
                $"recovered={recoveredPosition}; blocked={target}");
            Check(grid.IsWalkable(recoveredPosition),
                "RECOVERY_CELL_WALKABLE",
                $"recovered={recoveredPosition}");

            yield return new WaitForEndOfFrame();
            int visiblePixels = CaptureCloseup(
                mainCamera,
                actor,
                target,
                grid,
                WallTraversalPlayModeVerifier.CameraCapturePath);
            Texture2D screenCapture = ScreenCapture.CaptureScreenshotAsTexture();
            File.WriteAllBytes(
                WallTraversalPlayModeVerifier.ScreenCapturePath,
                screenCapture.EncodeToPNG());
            Object.Destroy(screenCapture);
            Check(visiblePixels > 1000,
                "CAMERA_CAPTURE_NONBLANK",
                $"visible pixels={visiblePixels}");

            report.Add($"actor={actor.name}");
            report.Add($"start={start}; target={target}; recovered={recoveredPosition}");
            report.Add($"finalWorld={actor.transform.position:F3}; blocked={grid.IsMovementBlockedByWall(target)}");
            report.Add($"cameraCapture={WallTraversalPlayModeVerifier.CameraCapturePath}; visiblePixels={visiblePixels}");
            report.Add($"screenCapture={WallTraversalPlayModeVerifier.ScreenCapturePath}");
        }
        finally
        {
            if (wall != null)
            {
                RemoveWall(gridManager, grid, wall);
            }

            move.CancelActiveMovement();
            actor.transform.position = originalPosition;
            actor.SetLifecycleState(originalLifecycle);
            if (brain != null) brain.enabled = brainWasEnabled;
            if (behaviorTree != null) behaviorTree.enabled = behaviorTreeWasEnabled;
            Application.logMessageReceived -= OnLogMessageReceived;
        }

        Finish();
        Destroy(gameObject);
    }

    private static bool TryFindTraversalPair(Grid grid, out Vector2Int start, out Vector2Int target)
    {
        for (int y = 0; y < grid.height; y++)
        {
            for (int x = 1; x < grid.width - 1; x++)
            {
                Vector2Int candidateTarget = new Vector2Int(x, y);
                GridCell targetCell = grid.GetGridCell(candidateTarget);
                if (targetCell == null
                    || !targetCell.HasOccupantInLayer(GridLayer.Hallway)
                    || targetCell.GetBuildingInlayer(GridLayer.Building) != null)
                {
                    continue;
                }

                foreach (int direction in new[] { -1, 1 })
                {
                    Vector2Int candidateStart = candidateTarget + new Vector2Int(direction, 0);
                    if (grid.IsValidGridPos(candidateStart)
                        && grid.IsWalkable(candidateStart)
                        && !grid.IsMovementBlockedByWall(candidateStart))
                    {
                        start = candidateStart;
                        target = candidateTarget;
                        return true;
                    }
                }
            }
        }

        start = default;
        target = default;
        return false;
    }

    private static BuildableObject PlaceWall(Grid grid, BuildingSO wallData, Vector2Int target)
    {
        BuildableObject wall = new GridBuildingFactory().Create(grid, wallData, target);
        if (wall == null)
        {
            return null;
        }

        wall.SetGrid(grid);
        wall.Initialization(wallData, target);
        bool registered = grid.RegisterOccupant(
            wall,
            GridLayer.Building,
            wallData.GetGridPosList(target),
            false);
        if (registered)
        {
            return wall;
        }

        Object.Destroy(wall.gameObject);
        return null;
    }

    private static void RemoveWall(GridSystemManager manager, Grid grid, BuildableObject wall)
    {
        if (wall == null)
        {
            return;
        }

        grid.RemoveOccupant(GridLayer.Building, wall.buildPoses, false);
        Object.Destroy(wall.gameObject);
        manager.NotifyGridObjectChanged();
    }

    private static int CaptureCloseup(
        Camera sourceCamera,
        CharacterActor actor,
        Vector2Int wallPosition,
        Grid grid,
        string path)
    {
        const int width = 768;
        const int height = 512;
        GameObject cameraObject = new GameObject("Wall Traversal Closeup Camera");
        Camera closeupCamera = cameraObject.AddComponent<Camera>();
        closeupCamera.CopyFrom(sourceCamera);
        closeupCamera.enabled = false;
        closeupCamera.orthographic = true;
        closeupCamera.orthographicSize = 2.4f;
        closeupCamera.aspect = (float)width / height;
        Vector3 wallWorld = grid.GetWorldPos(wallPosition);
        Vector3 center = Vector3.Lerp(actor.transform.position, wallWorld, 0.5f);
        closeupCamera.transform.SetPositionAndRotation(
            new Vector3(center.x, center.y + 1.5f, sourceCamera.transform.position.z),
            sourceCamera.transform.rotation);

        RenderTexture renderTexture = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
        Texture2D capture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        RenderTexture previousTarget = closeupCamera.targetTexture;
        RenderTexture previousActive = RenderTexture.active;
        try
        {
            closeupCamera.targetTexture = renderTexture;
            closeupCamera.Render();
            RenderTexture.active = renderTexture;
            capture.ReadPixels(new Rect(0f, 0f, width, height), 0, 0);
            capture.Apply();
            File.WriteAllBytes(path, capture.EncodeToPNG());
            return capture.GetPixels32().Count(pixel => pixel.a > 0 && (pixel.r > 4 || pixel.g > 4 || pixel.b > 4));
        }
        finally
        {
            closeupCamera.targetTexture = previousTarget;
            RenderTexture.active = previousActive;
            renderTexture.Release();
            Object.Destroy(renderTexture);
            Object.Destroy(capture);
            Object.Destroy(cameraObject);
        }
    }

    private void Check(bool condition, string key, string detail)
    {
        string line = $"{key}: {(condition ? "PASS" : "FAIL")} - {detail}";
        report.Add(line);
        if (!condition)
        {
            failures.Add(line);
        }
    }

    private void OnLogMessageReceived(string condition, string stackTrace, LogType type)
    {
        if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
        {
            capturedErrors.Add(condition);
        }
        else if (type == LogType.Warning)
        {
            capturedWarnings.Add(condition);
        }
    }

    private void Finish()
    {
        report.Add($"capturedErrors={capturedErrors.Count}; capturedWarnings={capturedWarnings.Count}");
        if (capturedErrors.Count > 0)
        {
            failures.AddRange(capturedErrors.Select(error => "UNITY_ERROR: " + error));
        }
        if (capturedWarnings.Count > 0)
        {
            failures.AddRange(capturedWarnings.Select(warning => "UNITY_WARNING: " + warning));
        }

        File.WriteAllLines(WallTraversalPlayModeVerifier.ReportPath, report.Concat(failures));
        if (failures.Count == 0)
        {
            Debug.Log("Wall traversal PlayMode verification passed. " + string.Join(" | ", report));
        }
        else
        {
            Debug.LogError("Wall traversal PlayMode verification failed. " + string.Join(" | ", failures));
        }
    }
}
