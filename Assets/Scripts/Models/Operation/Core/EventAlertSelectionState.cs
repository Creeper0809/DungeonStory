namespace DungeonStory.Operation
{
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
        if (!TryGetChoice(index, out EventAlertChoice choice))
        {
            return false;
        }

        choice.Callback?.Invoke();
        return true;
    }

    public bool TryGetChoice(int index, out EventAlertChoice choice)
    {
        choice = null;
        if (SelectedRecord == null
            || index < 0
            || index >= SelectedRecord.Choices.Count)
        {
            return false;
        }

        choice = SelectedRecord.Choices[index];
        return choice != null;
    }
}

}
