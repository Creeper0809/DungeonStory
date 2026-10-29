using TMPro;
using UnityEngine;

public interface IStaffManagementSurfaceQuery
{
    RectTransform ContentRoot { get; }
    Transform TableRoot { get; }
    TMP_Text TitleText { get; }
    IStaffWorkPriorityPanelUiFactory UiFactory { get; }
    bool IsManagementMode { get; }
}

public interface IStaffManagementSurfaceCommand
{
    void SetVisibleCounts(int workerCount, int cellCount);
    void RequestRefresh();
    void SetManagementMode(bool managementMode);
}
