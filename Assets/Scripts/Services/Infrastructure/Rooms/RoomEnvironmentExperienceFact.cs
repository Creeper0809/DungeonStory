/// <summary>
/// Immutable room-experience transition captured by its domain owner. Durable
/// observers decide how this fact is represented in the gameplay ledger.
/// </summary>
public sealed class RoomEnvironmentExperienceFact
{
    private readonly int locationX;
    private readonly int locationY;

    public RoomEnvironmentExperienceFact(
        long sequence,
        CharacterId actorId,
        string actorDisplayName,
        BuildingInstanceId facilityId,
        string facilityDisplayName,
        string roomId,
        string roomDisplayName,
        RoomExperienceActivity activity,
        WorkTypeId workTypeId,
        float impressionMood,
        float cleanlinessMood,
        float durationSeconds,
        CoreGridCell location,
        int absoluteDay)
    {
        Sequence = sequence;
        ActorId = actorId;
        ActorDisplayName = actorDisplayName ?? string.Empty;
        FacilityId = facilityId;
        FacilityDisplayName = facilityDisplayName ?? string.Empty;
        RoomId = roomId ?? string.Empty;
        RoomDisplayName = roomDisplayName ?? string.Empty;
        Activity = activity;
        WorkTypeId = workTypeId;
        ImpressionMood = impressionMood;
        CleanlinessMood = cleanlinessMood;
        DurationSeconds = durationSeconds;
        locationX = location.X;
        locationY = location.Y;
        AbsoluteDay = absoluteDay;
    }

    public long Sequence { get; }
    public CharacterId ActorId { get; }
    public string ActorDisplayName { get; }
    public BuildingInstanceId FacilityId { get; }
    public string FacilityDisplayName { get; }
    public string RoomId { get; }
    public string RoomDisplayName { get; }
    public RoomExperienceActivity Activity { get; }
    public WorkTypeId WorkTypeId { get; }
    public float ImpressionMood { get; }
    public float CleanlinessMood { get; }
    public float DurationSeconds { get; }
    public CoreGridCell Location => new(locationX, locationY);
    public int AbsoluteDay { get; }
}

/// <summary>
/// Immutable observation of a cleanliness transition for one character and
/// room. Cleanliness stays in the owner's raw 0-100 domain scale.
/// </summary>
public sealed class RoomEnvironmentConditionFact
{
    private readonly int locationX;
    private readonly int locationY;

    public RoomEnvironmentConditionFact(
        long sequence,
        CharacterId observerId,
        string observerDisplayName,
        BuildingInstanceId facilityId,
        string facilityDisplayName,
        string roomId,
        string roomDisplayName,
        float previousCleanliness,
        float currentCleanliness,
        CoreGridCell location,
        int absoluteDay)
    {
        Sequence = sequence;
        ObserverId = observerId;
        ObserverDisplayName = observerDisplayName ?? string.Empty;
        FacilityId = facilityId;
        FacilityDisplayName = facilityDisplayName ?? string.Empty;
        RoomId = roomId ?? string.Empty;
        RoomDisplayName = roomDisplayName ?? string.Empty;
        PreviousCleanliness = previousCleanliness;
        CurrentCleanliness = currentCleanliness;
        locationX = location.X;
        locationY = location.Y;
        AbsoluteDay = absoluteDay;
    }

    public long Sequence { get; }
    public CharacterId ObserverId { get; }
    public string ObserverDisplayName { get; }
    public BuildingInstanceId FacilityId { get; }
    public string FacilityDisplayName { get; }
    public string RoomId { get; }
    public string RoomDisplayName { get; }
    public float PreviousCleanliness { get; }
    public float CurrentCleanliness { get; }
    public CoreGridCell Location => new(locationX, locationY);
    public int AbsoluteDay { get; }
}
