using System;

public interface IUiPopupService
{
    void CloseAll();
    void Open(UIPopUp popup);
    void ClosePeek(UIPopUp popup);
    void BlockTouch();
    void ReleaseTouch();
}

public sealed class UiPopupService : IUiPopupService
{
    private readonly DungeonSceneRuntimeReferences sceneReferences;

    public UiPopupService(DungeonSceneRuntimeReferences sceneReferences)
    {
        this.sceneReferences = sceneReferences ?? throw new ArgumentNullException(nameof(sceneReferences));
    }

    public void CloseAll()
    {
        ResolveManager().CloseAllPopup();
    }

    public void Open(UIPopUp popup)
    {
        ResolveManager().OpenPopup(popup);
    }

    public void ClosePeek(UIPopUp popup)
    {
        ResolveManager().ClosePopupPeek(popup);
    }

    public void BlockTouch()
    {
        ResolveManager().MakeTouchFalse();
    }

    public void ReleaseTouch()
    {
        ResolveManager().MakeTouchTrue();
    }

    private UIManager ResolveManager()
    {
        return sceneReferences.UIManager
            ?? throw new InvalidOperationException($"{nameof(IUiPopupService)} requires a loaded {nameof(UIManager)}.");
    }
}
