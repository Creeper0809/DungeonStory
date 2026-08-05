using System;
using VContainer.Unity;

public sealed class SurvivalEnvironmentalFieldBridge : IStartable
{
    private readonly ISurvivalStorageEnvironmentSink sink;
    private readonly IEnvironmentalFieldQuery field;

    public SurvivalEnvironmentalFieldBridge(
        ISurvivalStorageEnvironmentSink sink,
        IEnvironmentalFieldQuery field)
    {
        this.sink = sink ?? throw new ArgumentNullException(nameof(sink));
        this.field = field ?? throw new ArgumentNullException(nameof(field));
    }

    public void Start()
    {
        sink.ConfigureStorageEnvironment(field);
    }
}
