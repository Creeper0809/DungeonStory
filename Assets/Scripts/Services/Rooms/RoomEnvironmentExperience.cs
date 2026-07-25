using System;
using UnityEngine;

public enum RoomExperienceActivity
{
    FacilityUse,
    Shopping,
    Work
}

public readonly struct RoomEnvironmentExperienceEvent
{
    public RoomEnvironmentExperienceEvent(
        CharacterActor actor,
        BuildableObject facility,
        RoomExperienceActivity activity)
        : this(actor, facility, activity, default)
    {
    }

    public RoomEnvironmentExperienceEvent(
        CharacterActor actor,
        BuildableObject facility,
        RoomExperienceActivity activity,
        WorkTypeId workTypeId)
    {
        Actor = actor;
        Facility = facility;
        Activity = activity;
        WorkTypeId = workTypeId;
        WorkType = WorkTypeCatalog.TryGet(workTypeId, out WorkTypeDefinition definition)
            ? definition.Type
            : FacilityWorkType.None;
    }

    public CharacterActor Actor { get; }
    public BuildableObject Facility { get; }
    public RoomExperienceActivity Activity { get; }
    public WorkTypeId WorkTypeId { get; }
    internal FacilityWorkType WorkType { get; }
}

public interface IRoomEnvironmentExperienceService
{
    bool Apply(RoomEnvironmentExperienceEvent eventType);
}

public sealed class RoomEnvironmentExperienceService : IRoomEnvironmentExperienceService
{
    private readonly IRoomLayoutCache roomLayoutCache;
    private readonly IRoomEnvironmentEvaluator evaluator;
    private readonly IRoomEnvironmentSettingsProvider settingsProvider;

    public RoomEnvironmentExperienceService(
        IRoomLayoutCache roomLayoutCache,
        IRoomEnvironmentEvaluator evaluator,
        IRoomEnvironmentSettingsProvider settingsProvider)
    {
        this.roomLayoutCache = roomLayoutCache
            ?? throw new ArgumentNullException(nameof(roomLayoutCache));
        this.evaluator = evaluator
            ?? throw new ArgumentNullException(nameof(evaluator));
        this.settingsProvider = settingsProvider
            ?? throw new ArgumentNullException(nameof(settingsProvider));
    }

    public bool Apply(RoomEnvironmentExperienceEvent eventType)
    {
        CharacterActor actor = eventType.Actor;
        BuildableObject facility = eventType.Facility;
        if (actor == null
            || facility == null
            || facility.isDestroy
            || facility.Grid == null
            || !TryGetFacilityRoom(facility, out RoomInstance room)
            || room == null
            || room.IsSelfContained
            || !room.IsUsable)
        {
            return false;
        }

        RoomEnvironmentSnapshot snapshot = evaluator.Evaluate(facility.Grid, room);
        if (!snapshot.IsEnvironmentActive)
        {
            return false;
        }

        RoomEnvironmentSettingsSO settings = settingsProvider.Settings;
        float impressionMood = settings.GetImpressivenessMood(snapshot.Impressiveness);
        float cleanlinessMood = settings.GetCleanlinessMood(snapshot.Cleanliness);
        string roomKey = $"{facility.Grid.GetHashCode()}:{room.Bounds.xMin}:{room.Bounds.yMin}";
        string roomName = RoomEnvironmentPresentation.GetRoomName(snapshot.Roles);
        string action = GetActionLabel(eventType, facility);

        if (!Mathf.Approximately(impressionMood, 0f))
        {
            actor.ApplyMoodFactor(
                $"room:{roomKey}:impression",
                BuildImpressionLabel(snapshot.Impressiveness, roomName, action),
                impressionMood,
                settings.MoodDurationSeconds,
                1);
        }

        if (!Mathf.Approximately(cleanlinessMood, 0f))
        {
            actor.ApplyMoodFactor(
                $"room:{roomKey}:cleanliness",
                BuildCleanlinessLabel(snapshot.Cleanliness, roomName),
                cleanlinessMood,
                settings.MoodDurationSeconds,
                1);
        }

        return !Mathf.Approximately(impressionMood, 0f)
            || !Mathf.Approximately(cleanlinessMood, 0f);
    }

    private bool TryGetFacilityRoom(BuildableObject facility, out RoomInstance room)
    {
        room = null;
        if (facility == null || facility.Grid == null)
        {
            return false;
        }

        if (facility.buildPoses != null)
        {
            foreach (Vector2Int position in facility.buildPoses)
            {
                if (roomLayoutCache.TryGetRoom(facility.Grid, position, out room))
                {
                    return true;
                }
            }
        }

        return roomLayoutCache.TryGetRoom(facility.Grid, facility.centerPos, out room);
    }

    private static string GetActionLabel(
        RoomEnvironmentExperienceEvent eventType,
        BuildableObject facility)
    {
        if (eventType.Activity == RoomExperienceActivity.Shopping)
        {
            return "쇼핑함";
        }

        if (eventType.Activity == RoomExperienceActivity.Work)
        {
            return eventType.WorkType switch
            {
                FacilityWorkType.Research => "연구함",
                FacilityWorkType.Construct => "건설함",
                FacilityWorkType.Repair => "수리함",
                FacilityWorkType.Clean => "청소함",
                FacilityWorkType.Guard => "근무함",
                FacilityWorkType.Restock => "보급함",
                FacilityWorkType.Rescue => "구조 작업을 함",
                FacilityWorkType.Rest => "휴식함",
                _ => "일함"
            };
        }

        FacilityRole roles = facility.Facility?.roles ?? FacilityRole.None;
        if ((roles & FacilityRole.Meal) != 0) return "식사함";
        if ((roles & FacilityRole.Rest) != 0) return "휴식함";
        if ((roles & FacilityRole.Hygiene) != 0) return "씻음";
        if ((roles & FacilityRole.Toilet) != 0) return "용변을 봄";
        if ((roles & FacilityRole.Training) != 0) return "훈련함";
        if ((roles & FacilityRole.Research) != 0) return "연구함";
        if ((roles & FacilityRole.Mana) != 0) return "마력을 다룸";
        if ((roles & FacilityRole.Purchase) != 0) return "쇼핑함";
        return "시간을 보냄";
    }

    private static string BuildImpressionLabel(float value, string roomName, string action)
    {
        if (value < 20f) return $"끔찍한 {roomName}에서 {action}";
        if (value < 40f) return $"초라한 {roomName}에서 {action}";
        if (value < 80f) return $"쾌적한 {roomName}에서 {action}";
        return $"인상적인 {roomName}에서 {action}";
    }

    private static string BuildCleanlinessLabel(float value, string roomName)
    {
        if (value < 20f) return $"지저분한 {roomName}이 신경 쓰임";
        if (value < 40f) return $"어수선한 {roomName}이 거슬림";
        return "깔끔한 공간이 마음에 듦";
    }
}
