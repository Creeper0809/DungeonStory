using System;
using System.Collections.Generic;
using System.Diagnostics;
using DungeonStory.Foundation;
using UnityEngine;
using VContainer.Unity;

public interface ICharacterPresentationScheduler
{
    int RegisteredCount { get; }
    int VisibleCount { get; }
    void Register(
        CharacterActor actor,
        WorldCharacterNameplate nameplate,
        CharacterFeedbackBubble feedbackBubble);
    void Unregister(CharacterActor actor);
    bool IsVisible(CharacterActor actor);
}

public sealed class CharacterPresentationScheduler :
    ICharacterPresentationScheduler,
    ILateTickable,
    IDisposable
{
    private const float ViewportMargin = 0.08f;
    private const float OffscreenProbeInterval = 0.12f;
    private const double MinimumSliceMilliseconds = 0.05;
    private const double MaximumSliceMilliseconds = 0.45;

    private readonly IMainCameraProvider cameraProvider;
    private readonly IDynamicFrameWorkBudget frameWorkBudget;
    private readonly IUiClock uiClock;
    private readonly List<Entry> entries = new List<Entry>();
    private readonly List<Entry> visibleEntries = new List<Entry>();
    private readonly Dictionary<CharacterActor, Entry> entriesByActor =
        new Dictionary<CharacterActor, Entry>();

    private Camera camera;
    private Vector3 lastCameraPosition;
    private float lastOrthographicSize = -1f;
    private float lastAspect = -1f;
    private int cameraVersion;
    private int probeCursor;

    public CharacterPresentationScheduler(
        IMainCameraProvider cameraProvider,
        IDynamicFrameWorkBudget frameWorkBudget,
        IUiClock uiClock)
    {
        this.cameraProvider = cameraProvider
            ?? throw new ArgumentNullException(nameof(cameraProvider));
        this.frameWorkBudget = frameWorkBudget
            ?? throw new ArgumentNullException(nameof(frameWorkBudget));
        this.uiClock = uiClock ?? throw new ArgumentNullException(nameof(uiClock));
    }

    public int RegisteredCount => entries.Count;
    public int VisibleCount => visibleEntries.Count;

    public void Register(
        CharacterActor actor,
        WorldCharacterNameplate nameplate,
        CharacterFeedbackBubble feedbackBubble)
    {
        if (actor == null)
        {
            return;
        }

        if (entriesByActor.TryGetValue(actor, out Entry existing))
        {
            existing.Nameplate = nameplate;
            existing.FeedbackBubble = feedbackBubble;
            existing.NextProbeTime = 0f;
            existing.CameraVersion = -1;
            return;
        }

        Entry entry = new Entry(
            actor,
            nameplate,
            feedbackBubble,
            entries.Count);
        entries.Add(entry);
        entriesByActor.Add(actor, entry);
        ProbeEntry(entry, force: true);
    }

    public void Unregister(CharacterActor actor)
    {
        if (actor == null
            || !entriesByActor.TryGetValue(actor, out Entry entry))
        {
            return;
        }

        entriesByActor.Remove(actor);
        SetVisible(entry, false);
        entry.Nameplate?.HideFromScheduler();
        entry.FeedbackBubble?.HideFromScheduler();

        int removeIndex = entry.EntryIndex;
        int lastIndex = entries.Count - 1;
        if (removeIndex != lastIndex)
        {
            Entry moved = entries[lastIndex];
            entries[removeIndex] = moved;
            moved.EntryIndex = removeIndex;
        }

        entries.RemoveAt(lastIndex);
        if (entries.Count == 0)
        {
            probeCursor = 0;
        }
        else
        {
            probeCursor %= entries.Count;
        }
    }

    public bool IsVisible(CharacterActor actor)
    {
        return actor != null
            && entriesByActor.TryGetValue(actor, out Entry entry)
            && entry.Visible;
    }

    public void LateTick()
    {
        RefreshCameraState();
        long started = Stopwatch.GetTimestamp();

        UpdateVisibleEntries();
        double sliceMilliseconds = frameWorkBudget.GetSliceMilliseconds(
            DynamicFrameWorkDomain.Presentation,
            MinimumSliceMilliseconds,
            MaximumSliceMilliseconds);
        ProbeOffscreenEntries(started, sliceMilliseconds);

        frameWorkBudget.SetBacklog(
            DynamicFrameWorkDomain.Presentation,
            Math.Max(0, entries.Count - visibleEntries.Count));
        frameWorkBudget.ReportConsumed(
            DynamicFrameWorkDomain.Presentation,
            GetElapsedMilliseconds(started));
    }

    public void Dispose()
    {
        for (int i = 0; i < entries.Count; i++)
        {
            entries[i].Nameplate?.HideFromScheduler();
            entries[i].FeedbackBubble?.HideFromScheduler();
        }

        entries.Clear();
        visibleEntries.Clear();
        entriesByActor.Clear();
        probeCursor = 0;
    }

    private void UpdateVisibleEntries()
    {
        for (int i = visibleEntries.Count - 1; i >= 0; i--)
        {
            Entry entry = visibleEntries[i];
            if (!CanPresent(entry.Actor) || !IsInsideViewport(entry.Actor))
            {
                SetVisible(entry, false);
                entry.Nameplate?.TickFromScheduler(false, force: false);
                entry.FeedbackBubble?.TickFromScheduler(false);
                continue;
            }

            entry.Actor.TickPresentationMaintenance();
            entry.Nameplate?.TickFromScheduler(true, force: false);
            entry.FeedbackBubble?.TickFromScheduler(true);
            entry.NextProbeTime = uiClock.Time + OffscreenProbeInterval;
            entry.CameraVersion = cameraVersion;
        }
    }

    private void ProbeOffscreenEntries(long started, double sliceMilliseconds)
    {
        if (entries.Count == 0)
        {
            return;
        }

        int inspected = 0;
        int maximumInspections = entries.Count;
        while (inspected < maximumInspections)
        {
            if (GetElapsedMilliseconds(started) >= sliceMilliseconds)
            {
                break;
            }

            if (probeCursor >= entries.Count)
            {
                probeCursor = 0;
            }

            Entry entry = entries[probeCursor++];
            inspected++;
            if (entry.Visible)
            {
                continue;
            }

            if (entry.CameraVersion == cameraVersion
                && uiClock.Time < entry.NextProbeTime)
            {
                continue;
            }

            ProbeEntry(entry, force: false);
        }
    }

    private void ProbeEntry(Entry entry, bool force)
    {
        if (entry == null)
        {
            return;
        }

        bool visible = CanPresent(entry.Actor) && IsInsideViewport(entry.Actor);
        SetVisible(entry, visible);
        entry.Actor?.TickPresentationMaintenance();
        entry.Nameplate?.TickFromScheduler(visible, force);
        entry.FeedbackBubble?.TickFromScheduler(visible);
        entry.NextProbeTime = uiClock.Time + OffscreenProbeInterval;
        entry.CameraVersion = cameraVersion;
    }

    private void SetVisible(Entry entry, bool visible)
    {
        if (entry.Visible == visible)
        {
            return;
        }

        entry.Visible = visible;
        if (visible)
        {
            entry.VisibleIndex = visibleEntries.Count;
            visibleEntries.Add(entry);
            return;
        }

        int removeIndex = entry.VisibleIndex;
        int lastIndex = visibleEntries.Count - 1;
        if (removeIndex >= 0 && removeIndex <= lastIndex)
        {
            if (removeIndex != lastIndex)
            {
                Entry moved = visibleEntries[lastIndex];
                visibleEntries[removeIndex] = moved;
                moved.VisibleIndex = removeIndex;
            }

            visibleEntries.RemoveAt(lastIndex);
        }

        entry.VisibleIndex = -1;
    }

    private void RefreshCameraState()
    {
        Camera resolved = null;
        try
        {
            resolved = cameraProvider.Camera;
        }
        catch (InvalidOperationException)
        {
            // Scene teardown can release the camera before entry points dispose.
        }

        bool changed = resolved != camera;
        camera = resolved;
        if (camera != null)
        {
            changed |= camera.transform.position != lastCameraPosition
                || !Mathf.Approximately(
                    camera.orthographicSize,
                    lastOrthographicSize)
                || !Mathf.Approximately(camera.aspect, lastAspect);
            lastCameraPosition = camera.transform.position;
            lastOrthographicSize = camera.orthographicSize;
            lastAspect = camera.aspect;
        }

        if (changed)
        {
            cameraVersion++;
        }
    }

    private bool IsInsideViewport(CharacterActor actor)
    {
        if (camera == null || actor == null)
        {
            return false;
        }

        Vector3 viewport = camera.WorldToViewportPoint(actor.transform.position);
        return viewport.z >= 0f
            && viewport.x >= -ViewportMargin
            && viewport.x <= 1f + ViewportMargin
            && viewport.y >= -ViewportMargin
            && viewport.y <= 1f + ViewportMargin;
    }

    private static bool CanPresent(CharacterActor actor)
    {
        return actor != null
            && actor.gameObject.activeInHierarchy
            && !actor.IsDead
            && actor.CurrentLifecycleState
                != CharacterLifecycleState.OnExpedition
            && actor.CurrentLifecycleState
                != CharacterLifecycleState.Despawned;
    }

    private static double GetElapsedMilliseconds(long started)
    {
        return (Stopwatch.GetTimestamp() - started)
            * 1000.0
            / Stopwatch.Frequency;
    }

    private sealed class Entry
    {
        public Entry(
            CharacterActor actor,
            WorldCharacterNameplate nameplate,
            CharacterFeedbackBubble feedbackBubble,
            int entryIndex)
        {
            Actor = actor;
            Nameplate = nameplate;
            FeedbackBubble = feedbackBubble;
            EntryIndex = entryIndex;
        }

        public CharacterActor Actor { get; }
        public WorldCharacterNameplate Nameplate { get; set; }
        public CharacterFeedbackBubble FeedbackBubble { get; set; }
        public int EntryIndex { get; set; }
        public int VisibleIndex { get; set; } = -1;
        public int CameraVersion { get; set; } = -1;
        public float NextProbeTime { get; set; }
        public bool Visible { get; set; }
    }
}
