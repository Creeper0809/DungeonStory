using System;
using DungeonStory.Foundation;
using UnityEngine;
using VContainer;

/// <summary>
/// Explicit, verifier-owned automation input capability. It follows the active gameplay
/// scope across scene changes and falls back to an isolated instance when no runtime
/// container exists.
/// </summary>
public sealed class DungeonAutomationInputTestCapability : IDisposable
{
    private readonly DungeonAutomationInputState fallback;
    private IDungeonAutomationInputControl active;
    private bool enabled;

    public DungeonAutomationInputTestCapability()
    {
        fallback = new DungeonAutomationInputState(
            new UnityGameClock(),
            new UnityUiClock());
    }

    public void Enable()
    {
        enabled = true;
        ResolveCurrent().Enable();
    }

    public void MovePointer(Vector2 position) =>
        ResolveEnabled().MovePointer(position);

    public int ClickPointer(int button) =>
        ResolveEnabled().ClickPointer(button);

    public void Scroll(float deltaY) =>
        ResolveEnabled().Scroll(deltaY);

    public bool HoldKey(KeyCode key, float durationSeconds) =>
        ResolveEnabled().HoldKey(key, durationSeconds);

    public void ReleaseKey(KeyCode key)
    {
        ResolveCurrent().ReleaseKey(key);
    }

    public void Dispose()
    {
        enabled = false;
        active?.Disable();
        if (!ReferenceEquals(active, fallback))
        {
            fallback.Disable();
        }

        active = null;
    }

    private IDungeonAutomationInputControl ResolveEnabled()
    {
        IDungeonAutomationInputControl current = ResolveCurrent();
        if (!enabled)
        {
            throw new InvalidOperationException(
                "The automation input test capability is not enabled.");
        }

        return current;
    }

    private IDungeonAutomationInputControl ResolveCurrent()
    {
        DungeonRuntimeLifetimeScope scope =
            UnityEngine.Object.FindAnyObjectByType<DungeonRuntimeLifetimeScope>();
        IDungeonAutomationInputControl resolved = scope?.Container != null
            ? scope.Container.Resolve<IDungeonAutomationInputControl>()
            : fallback;
        if (ReferenceEquals(active, resolved))
        {
            return active;
        }

        active?.Disable();
        active = resolved;
        if (enabled)
        {
            active.Enable();
        }

        return active;
    }
}
