using System;

/// <summary>Only immutable domain values; no scene reference or observer lease is saved.</summary>
[Serializable]
public sealed class RoomPendingFactSaveData
{
    public string kind;
    public long sequence;
    public string characterId;
    public string characterName;
    public string facilityId;
    public string facilityName;
    public string roomId;
    public string roomName;
    public int x;
    public int y;
    public int day;
    public int activity;
    public string workTypeId;
    public float impressionMood;
    public float cleanlinessMood;
    public float durationSeconds;
    public float previousCleanliness;
    public float currentCleanliness;

    public static RoomPendingFactSaveData Capture(RoomEnvironmentExperienceFact fact) => fact == null ? null : new()
    {
        kind = "experience", sequence = fact.Sequence,
        characterId = fact.ActorId.Value, characterName = fact.ActorDisplayName,
        facilityId = fact.FacilityId.Value, facilityName = fact.FacilityDisplayName,
        roomId = fact.RoomId, roomName = fact.RoomDisplayName,
        x = fact.Location.X, y = fact.Location.Y, day = fact.AbsoluteDay,
        activity = (int)fact.Activity, workTypeId = fact.WorkTypeId.Value,
        impressionMood = fact.ImpressionMood, cleanlinessMood = fact.CleanlinessMood,
        durationSeconds = fact.DurationSeconds
    };

    public static RoomPendingFactSaveData Capture(RoomEnvironmentConditionFact fact) => fact == null ? null : new()
    {
        kind = "condition", sequence = fact.Sequence,
        characterId = fact.ObserverId.Value, characterName = fact.ObserverDisplayName,
        facilityId = fact.FacilityId.Value, facilityName = fact.FacilityDisplayName,
        roomId = fact.RoomId, roomName = fact.RoomDisplayName,
        x = fact.Location.X, y = fact.Location.Y, day = fact.AbsoluteDay,
        previousCleanliness = fact.PreviousCleanliness, currentCleanliness = fact.CurrentCleanliness
    };

    public void Validate(long expectedSequence)
    {
        if (sequence != expectedSequence || sequence <= 0 || sequence == long.MaxValue || day < 1
            || !Canonical(characterId) || !Canonical(characterName) || !Canonical(facilityId)
            || !Canonical(facilityName) || !Canonical(roomId) || !Canonical(roomName)
            || kind is not ("experience" or "condition"))
            throw new InvalidOperationException("Room pending fact identity is invalid.");
        if (kind == "experience")
        {
            if (!Enum.IsDefined(typeof(RoomExperienceActivity), activity)
                || !float.IsFinite(impressionMood) || !float.IsFinite(cleanlinessMood)
                || !float.IsFinite(durationSeconds) || durationSeconds <= 0f)
                throw new InvalidOperationException("Room pending experience values are invalid.");
        }
        else if (!float.IsFinite(previousCleanliness) || !float.IsFinite(currentCleanliness)
            || previousCleanliness < 0f || previousCleanliness > 100f
            || currentCleanliness < 0f || currentCleanliness > 100f)
            throw new InvalidOperationException("Room pending cleanliness values are invalid.");
    }

    public RoomEnvironmentExperienceFact Experience() => kind != "experience" ? null : new(
        sequence, new CharacterId(characterId), characterName, new BuildingInstanceId(facilityId),
        facilityName, roomId, roomName, (RoomExperienceActivity)activity,
        string.IsNullOrEmpty(workTypeId) ? default : new WorkTypeId(workTypeId),
        impressionMood, cleanlinessMood, durationSeconds, new CoreGridCell(x, y), day);
    public RoomEnvironmentConditionFact Condition() => kind != "condition" ? null : new(
        sequence, new CharacterId(characterId), characterName, new BuildingInstanceId(facilityId),
        facilityName, roomId, roomName, previousCleanliness, currentCleanliness, new CoreGridCell(x, y), day);
    private static bool Canonical(string value) => !string.IsNullOrWhiteSpace(value) && value == value.Trim();
}
