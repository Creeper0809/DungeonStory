using System;
using VContainer.Unity;

public interface ISurvivalStorageEnvironmentSink
{
    void ConfigureStorageEnvironment(
        IEnvironmentalFieldRuntime fieldRuntime);
}

public sealed class SurvivalEnvironmentalFieldBridge : IStartable
{
    private readonly ISurvivalStorageEnvironmentSink sink;
    private readonly IEnvironmentalFieldRuntime field;

    public SurvivalEnvironmentalFieldBridge(
        ISurvivalStorageEnvironmentSink sink,
        IEnvironmentalFieldRuntime field)
    {
        this.sink = sink ?? throw new ArgumentNullException(nameof(sink));
        this.field = field ?? throw new ArgumentNullException(nameof(field));
    }

    public void Start()
    {
        sink.ConfigureStorageEnvironment(field);
    }
}
