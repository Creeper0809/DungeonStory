using UnityEngine;
using UnityEngine.Serialization;

public class UIGridTab : MonoBehaviour
{
    [FormerlySerializedAs("buildingCategory")]
    public int categoryId;

    private void Start()
    {
        CloseTab();
    }
    public bool ToggleTab(int categoryId)
    {
        if (this.categoryId != categoryId)
        {
            CloseTab();
            return false;
        }
        if (gameObject.activeSelf)
        {
            CloseTab();
            return false;
        }
        else
        {
            OpenTap();
            return true;
        }
    }
    public void OpenTap()
    {
        gameObject.SetActive(true);
    }
    public void CloseTab()
    {
        gameObject.SetActive(false);
    }
}
