using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public interface IPlayerInputReader
{
    Vector3 MousePosition { get; }
    float ScreenWidth { get; }
    float ScreenHeight { get; }
    float ScrollDeltaY { get; }
    bool GetKey(KeyCode keyCode);
    bool GetKeyDown(KeyCode keyCode);
    bool GetMouseButton(int button);
    bool GetMouseButtonDown(int button);
}

public interface IWorldPointerRaycaster
{
    bool TryRaycast(Camera camera, LayerMask layerMask, out RaycastHit2D hit);
}

public interface IUiPointerBlocker
{
    bool IsPointerOverUi();
}

public sealed class UnityPlayerInputReader : IPlayerInputReader
{
    public Vector3 MousePosition
    {
        get
        {
            if (DungeonAutomationInputState.TryGetPointerPosition(out Vector3 automationPosition))
            {
                return automationPosition;
            }

            if (Mouse.current != null)
            {
                Vector2 position = Mouse.current.position.ReadValue();
                if (!float.IsNaN(position.x)
                    && !float.IsNaN(position.y)
                    && !float.IsInfinity(position.x)
                    && !float.IsInfinity(position.y))
                {
                    return new Vector3(position.x, position.y, Input.mousePosition.z);
                }
            }

            return Input.mousePosition;
        }
    }
    public float ScreenWidth => Screen.width;
    public float ScreenHeight => Screen.height;
    public float ScrollDeltaY
    {
        get
        {
            if (DungeonAutomationInputState.TryConsumeScrollDeltaY(out float automationScroll))
            {
                return automationScroll;
            }

            if (Mouse.current != null)
            {
                Vector2 scroll = Mouse.current.scroll.ReadValue();
                if (!float.IsNaN(scroll.y)
                    && !float.IsInfinity(scroll.y)
                    && !Mathf.Approximately(scroll.y, 0f))
                {
                    return scroll.y;
                }
            }

            try
            {
                return Input.mouseScrollDelta.y;
            }
            catch (InvalidOperationException)
            {
                return 0f;
            }
        }
    }

    public bool GetKey(KeyCode keyCode)
    {
        return DungeonAutomationInputState.GetKey(keyCode)
            || IsLegacyKeyPressed(keyCode)
            || IsInputSystemKeyPressed(keyCode);
    }

    public bool GetKeyDown(KeyCode keyCode)
    {
        return DungeonAutomationInputState.GetKeyDown(keyCode)
            || IsLegacyKeyPressedThisFrame(keyCode)
            || IsInputSystemKeyPressedThisFrame(keyCode);
    }

    public bool GetMouseButtonDown(int button)
    {
        if (DungeonAutomationInputState.GetMouseButtonDown(button))
        {
            return true;
        }

        if (Mouse.current != null)
        {
            return button switch
            {
                0 => Mouse.current.leftButton.wasPressedThisFrame,
                1 => Mouse.current.rightButton.wasPressedThisFrame,
                2 => Mouse.current.middleButton.wasPressedThisFrame,
                _ => Input.GetMouseButtonDown(button)
            };
        }

        return Input.GetMouseButtonDown(button);
    }

    public bool GetMouseButton(int button)
    {
        if (DungeonAutomationInputState.GetMouseButton(button))
        {
            return true;
        }

        if (Mouse.current != null)
        {
            return button switch
            {
                0 => Mouse.current.leftButton.isPressed,
                1 => Mouse.current.rightButton.isPressed,
                2 => Mouse.current.middleButton.isPressed,
                _ => Input.GetMouseButton(button)
            };
        }

        return Input.GetMouseButton(button);
    }

    private static bool IsLegacyKeyPressed(KeyCode keyCode)
    {
        try
        {
            return Input.GetKey(keyCode);
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    private static bool IsLegacyKeyPressedThisFrame(KeyCode keyCode)
    {
        try
        {
            return Input.GetKeyDown(keyCode);
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    private static bool IsInputSystemKeyPressed(KeyCode keyCode)
    {
        return TryGetInputSystemKey(keyCode, out Key key)
            && Keyboard.current != null
            && Keyboard.current[key].isPressed;
    }

    private static bool IsInputSystemKeyPressedThisFrame(KeyCode keyCode)
    {
        return TryGetInputSystemKey(keyCode, out Key key)
            && Keyboard.current != null
            && Keyboard.current[key].wasPressedThisFrame;
    }

    private static bool TryGetInputSystemKey(KeyCode keyCode, out Key key)
    {
        key = keyCode switch
        {
            KeyCode.W => Key.W,
            KeyCode.A => Key.A,
            KeyCode.S => Key.S,
            KeyCode.D => Key.D,
            KeyCode.UpArrow => Key.UpArrow,
            KeyCode.DownArrow => Key.DownArrow,
            KeyCode.LeftArrow => Key.LeftArrow,
            KeyCode.RightArrow => Key.RightArrow,
            KeyCode.Equals => Key.Equals,
            KeyCode.Minus => Key.Minus,
            KeyCode.KeypadPlus => Key.NumpadPlus,
            KeyCode.KeypadMinus => Key.NumpadMinus,
            KeyCode.PageUp => Key.PageUp,
            KeyCode.PageDown => Key.PageDown,
            KeyCode.LeftShift => Key.LeftShift,
            KeyCode.RightShift => Key.RightShift,
            KeyCode.Escape => Key.Escape,
            _ => Key.None
        };
        return key != Key.None;
    }

}

public sealed class EventSystemUiPointerBlocker : IUiPointerBlocker
{
    private readonly IPlayerInputReader inputReader;
    private readonly List<RaycastResult> raycastResults = new List<RaycastResult>();

    public EventSystemUiPointerBlocker(IPlayerInputReader inputReader)
    {
        this.inputReader = inputReader ?? throw new ArgumentNullException(nameof(inputReader));
    }

    public bool IsPointerOverUi()
    {
        if (!TryRaycastUi())
        {
            return false;
        }

        foreach (RaycastResult raycastResult in raycastResults)
        {
            if (raycastResult.module is GraphicRaycaster)
            {
                return true;
            }
        }

        return false;
    }

    public bool IsPointerOverScrollableUi()
    {
        if (!TryRaycastUi())
        {
            return false;
        }

        foreach (RaycastResult raycastResult in raycastResults)
        {
            if (raycastResult.module is not GraphicRaycaster || raycastResult.gameObject == null)
            {
                continue;
            }

            ScrollRect scrollRect = raycastResult.gameObject.GetComponentInParent<ScrollRect>();
            if (scrollRect != null
                && scrollRect.isActiveAndEnabled
                && (scrollRect.horizontal || scrollRect.vertical))
            {
                return true;
            }
        }

        return false;
    }

    private bool TryRaycastUi()
    {
        EventSystem eventSystem = EventSystem.current;
        if (eventSystem == null || !eventSystem.isActiveAndEnabled)
        {
            raycastResults.Clear();
            return false;
        }

        PointerEventData pointerEventData = new PointerEventData(eventSystem)
        {
            position = inputReader.MousePosition
        };
        raycastResults.Clear();
        eventSystem.RaycastAll(pointerEventData, raycastResults);
        return raycastResults.Count > 0;
    }
}

public sealed class PhysicsWorldPointerRaycaster : IWorldPointerRaycaster
{
    private readonly IPlayerInputReader inputReader;

    public PhysicsWorldPointerRaycaster(IPlayerInputReader inputReader)
    {
        this.inputReader = inputReader ?? throw new ArgumentNullException(nameof(inputReader));
    }

    public bool TryRaycast(Camera camera, LayerMask layerMask, out RaycastHit2D hit)
    {
        hit = default;
        if (camera == null)
        {
            return false;
        }

        Vector3 screenPosition = inputReader.MousePosition;
        screenPosition.z = -camera.transform.position.z;
        Vector3 worldPosition = camera.ScreenToWorldPoint(screenPosition);
        hit = Physics2D.Raycast(worldPosition, Vector2.zero, 0f, layerMask);
        return hit.collider != null;
    }
}
