using System;
using UnityEngine;
using VContainer;

public class UITab : UIPopUp
{
    public int id;
    private IUiPopupService popupService;

    [Inject]
    public void Construct(IUiPopupService popupService)
    {
        this.popupService = popupService
            ?? throw new ArgumentNullException(nameof(popupService));
    }

    public bool ToggleTab(int id)
    {
        if (this.id != id)
        {
            CloseTab();
            return false;
        }
        else
        {
            return Toggle();
        }
    }
    public bool Toggle()
    {
        if(gameObject.activeSelf)
        {
            CloseTab();
            return false;
        }
        else
        {
            ResolvePopupService().Open(this);
            gameObject.SetActive(true);
            return true;
        }
    }
    public override void OnClose()
    {
        gameObject.SetActive(false);
    }
    public void CloseTab()
    {
        if (!gameObject.activeSelf) return;
        ResolvePopupService().ClosePeek(this);
    }

    private IUiPopupService ResolvePopupService()
    {
        if (popupService == null)
        {
            throw new InvalidOperationException(
                $"{nameof(UITab)} requires VContainer injection of {nameof(IUiPopupService)}.");
        }

        return popupService;
    }
}
