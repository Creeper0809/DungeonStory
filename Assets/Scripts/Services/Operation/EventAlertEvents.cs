public struct EventAlertRequestedEvent
{
    public EventAlertRequest request;

    public EventAlertRequestedEvent(EventAlertRequest request)
    {
        this.request = request;
    }
}

public struct EventAlertLoggedEvent
{
    public EventAlertRecordSnapshot record;

    public EventAlertLoggedEvent(EventAlertRecord record)
    {
        this.record = record?.CreateSnapshot();
    }

    public EventAlertLoggedEvent(EventAlertRecordSnapshot record)
    {
        this.record = record;
    }
}
