#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public static class SingleSpritePresentationPlayModeVerifier
{
    public const string ReportPath =
        "Artifacts/QA/SingleSpritePresentation/single-sprite-presentation.txt";
    public const string DesktopCapturePath =
        "Artifacts/QA/SingleSpritePresentation/presentation-1600x900.png";
    public const string PortraitCapturePath =
        "Artifacts/QA/SingleSpritePresentation/presentation-900x1600.png";

    [MenuItem(
        "DungeonStory/Debug/QA/Run Single Sprite Presentation PlayMode Verification")]
    public static void RunFromMenu()
    {
        if (!EditorApplication.isPlaying)
        {
            Debug.LogError(
                "Single-sprite presentation verification requires PlayMode.");
            return;
        }

        if (UnityEngine.Object.FindFirstObjectByType<
                SingleSpritePresentationPlayModeRunner>() != null)
        {
            return;
        }

        new GameObject("Single Sprite Presentation PlayMode Runner")
            .AddComponent<SingleSpritePresentationPlayModeRunner>();
    }
}

public sealed class SingleSpritePresentationPlayModeRunner : MonoBehaviour
{
    private readonly List<string> report = new List<string>();
    private readonly List<string> failures = new List<string>();
    private readonly List<string> consoleIssues = new List<string>();
    private float originalTimeScale;
    private int originalResolutionIndex = -1;
    private CharacterActor verifiedActor;
    private CharacterCarryInventory verifiedInventory;
    private CharacterCarryInventorySaveData originalCarryInventory;
    private Sprite originalCharacterSprite;

    private IEnumerator Start()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(
            SingleSpritePresentationPlayModeVerifier.ReportPath));
        Application.logMessageReceived += CaptureConsole;
        originalTimeScale = Time.timeScale;
        originalResolutionIndex = GameViewResolutionController.SelectedSizeIndex;
        Time.timeScale = 1f;

        yield return null;
        yield return null;
        Button ownerButton = Resources.FindObjectsOfTypeAll<Button>()
            .FirstOrDefault(button =>
                button != null
                && button.gameObject.scene.IsValid()
                && button.gameObject.activeInHierarchy
                && button.interactable
                && button.name.StartsWith(
                    "OwnerOption_",
                    StringComparison.Ordinal));
        if (ownerButton != null)
        {
            RectTransform rect = ownerButton.transform as RectTransform;
            Vector2 clickPosition = rect != null
                ? RectTransformUtility.WorldToScreenPoint(
                    null,
                    rect.TransformPoint(rect.rect.center))
                : Vector2.zero;
            PlayModeVerificationFrameWait.DispatchPointerClick(
                ownerButton.gameObject,
                clickPosition);
            yield return null;
        }

        yield return StartPartyPlayModeTestDriver.CompleteIfVisible();
        yield return new WaitForSecondsRealtime(0.35f);
        CharacterActor[] actors = FindObjectsByType<CharacterActor>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.InstanceID);
        Check(actors.Length > 0, "ACTORS", $"runtime actors={actors.Length}");
        if (actors.Length == 0)
        {
            Finish();
            yield break;
        }

        CharacterActor actor = actors[0];
        verifiedActor = actor;
        originalCharacterSprite = actor.VisualRenderer != null
            ? actor.VisualRenderer.sprite
            : null;
        if (actor.VisualRenderer != null && actor.VisualRenderer.sprite == null)
        {
            CharacterSO sample = Resources.LoadAll<CharacterSO>("SO/Character")
                .FirstOrDefault(candidate =>
                    candidate != null && candidate.characterSprite != null);
            actor.GetComponent<CharacterVisual>()?.SetCharacterSprite(
                sample != null ? sample.characterSprite : null);
        }

        CharacterProceduralMotionPresenter motion =
            actor.GetComponent<CharacterProceduralMotionPresenter>();
        CharacterCarryPresentation carry =
            actor.GetComponent<CharacterCarryPresentation>();
        CharacterWorldActionPresenter action =
            actor.GetComponent<CharacterWorldActionPresenter>();
        Check(
            motion != null && carry != null && action != null,
            "COMPOSITION",
            $"motion={motion != null}; carry={carry != null}; action={action != null}");
        if (motion == null || carry == null || action == null)
        {
            Finish();
            yield break;
        }

        CenterCamera(actor);
        yield return new WaitForSecondsRealtime(0.2f);
        VerifySingleSpriteAndReset(actor, motion);
        VerifyVisibilityBudget(motion, carry, action);
        yield return VerifyCarryFacingAndSorting(actor, carry);
        VerifySteadyStateAllocation(motion, carry, action);
        VerifyFacilityPresentation();

        yield return CaptureAtResolution(
            actor,
            motion,
            new Vector2Int(1600, 900),
            SingleSpritePresentationPlayModeVerifier.DesktopCapturePath);
        yield return CaptureAtResolution(
            actor,
            motion,
            new Vector2Int(900, 1600),
            SingleSpritePresentationPlayModeVerifier.PortraitCapturePath);
        Finish();
    }

    private void VerifySingleSpriteAndReset(
        CharacterActor actor,
        CharacterProceduralMotionPresenter motion)
    {
        SpriteRenderer renderer = actor.VisualRenderer;
        Sprite originalSprite = renderer != null ? renderer.sprite : null;
        motion.RecaptureBaselineAfterFootAlignment();
        Vector3 expectedPosition = motion.BaselineLocalPosition;
        Transform visualRoot = actor.VisualRoot;
        if (visualRoot != null)
        {
            visualRoot.localPosition += new Vector3(0.3125f, 0.1875f, 0f);
            visualRoot.localRotation = Quaternion.Euler(0f, 0f, 13f);
            visualRoot.localScale = new Vector3(1.22f, 0.73f, 1f);
        }

        motion.ResetVisualRootToDefault();
        Check(
            visualRoot != null
            && Approximately(visualRoot.localPosition, expectedPosition)
            && Approximately(visualRoot.localScale, Vector3.one)
            && Quaternion.Angle(visualRoot.localRotation, Quaternion.identity) < 0.001f,
            "RESET_BASELINE",
            visualRoot != null
                ? $"position={visualRoot.localPosition}; scale={visualRoot.localScale}; "
                    + $"rotation={visualRoot.localRotation.eulerAngles.z:0.###}"
                : "visual root missing");
        Check(
            renderer != null
            && originalSprite != null
            && renderer.sprite == originalSprite,
            "STATIC_SPRITE",
            $"sprite={renderer?.sprite?.name ?? "missing"}; unchanged="
                + $"{renderer != null && renderer.sprite == originalSprite}");
    }

    private void VerifyVisibilityBudget(
        CharacterProceduralMotionPresenter motion,
        CharacterCarryPresentation carry,
        CharacterWorldActionPresenter action)
    {
        motion.TickPresentation(false);
        carry.TickPresentation(false);
        action.TickPresentation(false);
        int transformCount = motion.AppliedTransformCount;
        int actionCount = action.VisibleTickCount;
        int carryCount = carry.RefreshCount;
        for (int i = 0; i < 128; i++)
        {
            action.TickPresentation(false);
            motion.TickPresentation(false);
            carry.TickPresentation(false);
        }

        Check(
            motion.AppliedTransformCount == transformCount
            && action.VisibleTickCount == actionCount
            && carry.RefreshCount == carryCount,
            "OFFSCREEN_ZERO_WORK",
            $"transform={transformCount}->{motion.AppliedTransformCount}; "
                + $"action={actionCount}->{action.VisibleTickCount}; "
                + $"carry={carryCount}->{carry.RefreshCount}");
        action.TickPresentation(true);
        motion.TickPresentation(true);
        carry.TickPresentation(true);
    }

    private IEnumerator VerifyCarryFacingAndSorting(
        CharacterActor actor,
        CharacterCarryPresentation carry)
    {
        CharacterCarryInventory inventory = CharacterCarryInventory.Ensure(actor);
        verifiedInventory = inventory;
        CharacterCarryInventorySaveData original = inventory.Capture();
        originalCarryInventory = original;
        ResourceDungeonItemCatalogProvider catalog =
            EditorItemCatalogFactory.Create();
        const string itemId = "material:lumber";
        bool added = inventory.TryAddPartialStack(
            "qa:single-sprite",
            itemId,
            1,
            catalog,
            null,
            out int accepted,
            out string failure);
        Check(
            added && accepted == 1,
            "CARRY_SETUP",
            $"added={added}; accepted={accepted}; failure={failure}");
        carry.TickPresentation(true);
        yield return null;

        SpriteRenderer characterRenderer = actor.VisualRenderer;
        SpriteRenderer propRenderer = carry.PropRenderer;
        bool originalFlip = characterRenderer.flipX;
        int originalOrder = characterRenderer.sortingOrder;

        characterRenderer.flipX = true;
        carry.TickPresentation(true);
        float rightX = propRenderer.transform.localPosition.x;
        int rightRelativeOrder =
            propRenderer.sortingOrder - characterRenderer.sortingOrder;

        characterRenderer.flipX = false;
        carry.TickPresentation(true);
        float leftX = propRenderer.transform.localPosition.x;
        int leftRelativeOrder =
            propRenderer.sortingOrder - characterRenderer.sortingOrder;

        characterRenderer.sortingOrder += 4;
        carry.TickPresentation(true);
        int changedRelativeOrder =
            propRenderer.sortingOrder - characterRenderer.sortingOrder;
        Check(
            propRenderer.enabled
            && rightX > 0f
            && leftX < 0f
            && rightRelativeOrder == 1
            && leftRelativeOrder == 1
            && changedRelativeOrder == 1
            && propRenderer.sortingLayerID == characterRenderer.sortingLayerID,
            "CARRY_FACING_SORTING",
            $"enabled={propRenderer.enabled}; x={rightX:0.###}/{leftX:0.###}; "
                + $"orders={rightRelativeOrder}/{leftRelativeOrder}/{changedRelativeOrder}; "
                + $"layer={propRenderer.sortingLayerID}/{characterRenderer.sortingLayerID}");

        characterRenderer.flipX = originalFlip;
        characterRenderer.sortingOrder = originalOrder;
        carry.TickPresentation(true);
    }

    private void VerifySteadyStateAllocation(
        CharacterProceduralMotionPresenter motion,
        CharacterCarryPresentation carry,
        CharacterWorldActionPresenter action)
    {
        action.TickPresentation(true);
        motion.TickPresentation(true);
        carry.TickPresentation(true);
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 1024; i++)
        {
            action.TickPresentation(true);
            motion.TickPresentation(true);
            carry.TickPresentation(true);
        }

        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Check(
            allocated == 0,
            "STEADY_STATE_GC",
            $"allocatedBytes={allocated}");
    }

    private void VerifyFacilityPresentation()
    {
        BuildableObject[] buildings = FindObjectsByType<BuildableObject>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.None);
        int eligibleBuildings = buildings.Count(building =>
            building != null && !building.isDestroy && building.BuildingData != null);
        FacilityOperationalPresentation[] presentations =
            FindObjectsByType<FacilityOperationalPresentation>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
        Check(
            presentations.Length == eligibleBuildings,
            "FACILITY_COMPOSITION",
            $"presentations={presentations.Length}; eligibleBuildings={eligibleBuildings}");
        FacilityOperationalPresentation presentation =
            presentations.FirstOrDefault();
        if (presentation == null)
        {
            return;
        }

        presentation.TickPresentation(
            FacilityOperationalVisualState.Operating,
            true,
            10f,
            false);
        bool operating = presentation.State
            == FacilityOperationalVisualState.Operating
            && presentation.StatusRenderer.enabled;
        presentation.TickPresentation(
            FacilityOperationalVisualState.Idle,
            true,
            10.1f,
            false);
        bool completed = presentation.State
            == FacilityOperationalVisualState.Completed
            && presentation.StatusRenderer.enabled;
        presentation.TickPresentation(
            FacilityOperationalVisualState.Idle,
            true,
            11f,
            false);
        bool returnedIdle = presentation.State
            == FacilityOperationalVisualState.Idle
            && !presentation.StatusRenderer.enabled;
        presentation.TickPresentation(
            FacilityOperationalVisualState.DrainBlocked,
            true,
            12f,
            false);
        bool blocked = presentation.State
            == FacilityOperationalVisualState.DrainBlocked
            && presentation.StatusRenderer.enabled;
        presentation.TickPresentation(
            FacilityOperationalVisualState.Idle,
            false,
            13f,
            false);
        Check(
            operating && completed && returnedIdle && blocked,
            "FACILITY_STATES",
            $"operating={operating}; completed={completed}; "
                + $"idle={returnedIdle}; drainBlocked={blocked}");
    }

    private IEnumerator CaptureAtResolution(
        CharacterActor actor,
        CharacterProceduralMotionPresenter motion,
        Vector2Int resolution,
        string path)
    {
        GameViewResolutionController.Select(resolution.x, resolution.y);
        float timeout = Time.realtimeSinceStartup + 3f;
        while ((Screen.width != resolution.x || Screen.height != resolution.y)
            && Time.realtimeSinceStartup < timeout)
        {
            yield return null;
        }

        CenterCamera(actor);
        yield return null;
        motion.TriggerImpact(actor.VisualRenderer != null
            && actor.VisualRenderer.flipX ? 1f : -1f);
        motion.TickPresentation(true);
        yield return new WaitForEndOfFrame();
        Texture2D capture = ScreenCapture.CaptureScreenshotAsTexture();
        File.WriteAllBytes(path, capture.EncodeToPNG());
        Check(
            capture.width == resolution.x && capture.height == resolution.y,
            $"CAPTURE_{resolution.x}x{resolution.y}",
            $"requested={resolution.x}x{resolution.y}; "
                + $"actual={capture.width}x{capture.height}; "
                + $"rotationCompatible={motion.ImpactRotationCompatible}");
        Destroy(capture);
        motion.ResetVisualRootToDefault();
    }

    private static void CenterCamera(CharacterActor actor)
    {
        Camera camera = Camera.main;
        if (camera == null || actor == null)
        {
            return;
        }

        Vector3 actorPosition = actor.transform.position;
        camera.transform.position = new Vector3(
            actorPosition.x,
            actorPosition.y + 0.5f,
            camera.transform.position.z);
    }

    private void Check(bool passed, string id, string details)
    {
        string line = $"{(passed ? "PASS" : "FAIL")} {id}: {details}";
        report.Add(line);
        if (!passed)
        {
            failures.Add(line);
        }
    }

    private void CaptureConsole(
        string condition,
        string stackTrace,
        LogType type)
    {
        if (type == LogType.Error
            || type == LogType.Exception
            || type == LogType.Assert
            || type == LogType.Warning)
        {
            consoleIssues.Add($"{type}: {condition}\n{stackTrace}");
        }
    }

    private void Finish()
    {
        Application.logMessageReceived -= CaptureConsole;
        Time.timeScale = originalTimeScale;
        if (verifiedInventory != null && originalCarryInventory != null)
        {
            verifiedInventory.Restore(originalCarryInventory);
        }

        if (verifiedActor != null && verifiedActor.VisualRenderer != null)
        {
            verifiedActor.GetComponent<CharacterVisual>()?.SetCharacterSprite(
                originalCharacterSprite);
        }

        if (originalResolutionIndex >= 0)
        {
            GameViewResolutionController.SelectedSizeIndex =
                originalResolutionIndex;
        }

        Check(
            consoleIssues.Count == 0,
            "CONSOLE",
            consoleIssues.Count == 0
                ? "Error 0 / Warning 0"
                : string.Join(" | ", consoleIssues));
        report.Add($"RESULT: {(failures.Count == 0 ? "PASS" : "FAIL")}");
        File.WriteAllLines(
            SingleSpritePresentationPlayModeVerifier.ReportPath,
            report);
        if (failures.Count == 0)
        {
            Debug.Log(
                "Single-sprite procedural presentation verification PASS. "
                + SingleSpritePresentationPlayModeVerifier.ReportPath);
        }
        else
        {
            Debug.LogError(
                "Single-sprite procedural presentation verification FAIL:\n"
                + string.Join("\n", failures));
        }

        Destroy(gameObject);
    }

    private static bool Approximately(Vector3 first, Vector3 second)
    {
        return (first - second).sqrMagnitude < 0.0000001f;
    }
}
#endif
