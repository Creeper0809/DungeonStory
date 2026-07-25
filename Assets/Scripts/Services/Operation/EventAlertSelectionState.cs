public sealed class EventAlertSelectionState
{
    public EventAlertRecord SelectedRecord { get; private set; }

    public void Select(EventAlertRecord record)
    {
        if (record != null)
        {
            SelectedRecord = record;
        }
    }

    public void Clear()
    {
        SelectedRecord = null;
    }

    public bool ExecuteChoice(int index)
    {
        if (SelectedRecord == null || index < 0 || index >= SelectedRecord.Choices.Count)
        {
            return false;
        }

        SelectedRecord.Choices[index].Callback?.Invoke();
        return true;
    }
}
