public readonly struct RunResultReadyEvent
{
    public RunResultSnapshot result { get; }

    public RunResultReadyEvent(RunResultSnapshot result)
    {
        this.result = result;
    }
}
