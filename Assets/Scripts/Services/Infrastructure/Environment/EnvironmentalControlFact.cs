using System;

/// <summary>
/// Immutable environmental-control fact. The mutable save DTO is copied both
/// when the fact is captured and whenever an observer requests a snapshot.
/// </summary>
public sealed class EnvironmentalControlFact
{
    private readonly int schemaVersion;
    private readonly string operationId;
    private readonly string buildingInstanceId;
    private readonly string buildingDisplayName;
    private readonly string commandSource;
    private readonly int x;
    private readonly int y;
    private readonly float requestedTargetTemperatureC;
    private readonly float appliedTargetTemperatureC;
    private readonly float minimumTargetTemperatureC;
    private readonly float maximumTargetTemperatureC;
    private readonly float targetBeforeTemperatureC;
    private readonly float targetAfterTemperatureC;
    private readonly int absoluteDay;
    private readonly long ownerRevision;

    public EnvironmentalControlFact(EnvironmentalControlOutcomeSaveData source)
    {
        if (source == null) throw new ArgumentNullException(nameof(source));
        schemaVersion = source.schemaVersion;
        operationId = source.operationId;
        buildingInstanceId = source.buildingInstanceId;
        buildingDisplayName = source.buildingDisplayName;
        commandSource = source.commandSource;
        x = source.x;
        y = source.y;
        requestedTargetTemperatureC = source.requestedTargetTemperatureC;
        appliedTargetTemperatureC = source.appliedTargetTemperatureC;
        minimumTargetTemperatureC = source.minimumTargetTemperatureC;
        maximumTargetTemperatureC = source.maximumTargetTemperatureC;
        targetBeforeTemperatureC = source.targetBeforeTemperatureC;
        targetAfterTemperatureC = source.targetAfterTemperatureC;
        absoluteDay = source.absoluteDay;
        ownerRevision = source.ownerRevision;
    }

    public EnvironmentalControlOutcomeSaveData Snapshot() => new()
    {
        schemaVersion = schemaVersion,
        operationId = operationId,
        buildingInstanceId = buildingInstanceId,
        buildingDisplayName = buildingDisplayName,
        commandSource = commandSource,
        x = x,
        y = y,
        requestedTargetTemperatureC = requestedTargetTemperatureC,
        appliedTargetTemperatureC = appliedTargetTemperatureC,
        minimumTargetTemperatureC = minimumTargetTemperatureC,
        maximumTargetTemperatureC = maximumTargetTemperatureC,
        targetBeforeTemperatureC = targetBeforeTemperatureC,
        targetAfterTemperatureC = targetAfterTemperatureC,
        absoluteDay = absoluteDay,
        ownerRevision = ownerRevision
    };
}
