public interface IUiPopupService
{
    void CloseAll();
    void Open(UIPopUp popup);
    void ClosePeek(UIPopUp popup);
    void BlockTouch();
    void ReleaseTouch();
}
