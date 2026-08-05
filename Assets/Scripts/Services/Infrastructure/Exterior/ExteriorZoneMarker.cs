using System;
using UnityEngine;

public sealed class ExteriorZoneMarker : Facility
{
    private const float CleanWorkThreshold = 75f;
    private const float ReadinessCompleteThreshold = 100f;

    private ExteriorZoneType zoneType;
    private string zoneId;
    private float cleanliness = 100f;
    private float damage;
    private float patrolReadiness = 45f;
    private float receptionReadiness = 55f;
    private int waitingVisitors;
    private float firstImpressionBonus;
    private int completedWorks;
    private ExteriorIncidentKind activeIncidentKind;
    private string activeIncidentId = string.Empty;
    private string activeIncidentText = string.Empty;
    private float incidentRemainingSeconds;

    public string ZoneId => zoneId;
    public ExteriorZoneType ZoneType => zoneType;
    public Vector2Int GridPosition => centerPos;
    public string DisplayName => BuildingData != null && !string.IsNullOrWhiteSpace(BuildingData.objectName)
        ? BuildingData.objectName
        : zoneType.ToString();
    public float Cleanliness => cleanliness;
    public float Damage => damage;
    public float PatrolReadiness => patrolReadiness;
    public float ReceptionReadiness => receptionReadiness;
    public int WaitingVisitorCount => waitingVisitors;
    public float FirstImpressionBonus => firstImpressionBonus;
    public bool HasActiveIncident => activeIncidentKind != ExteriorIncidentKind.None;
    public ExteriorIncidentKind ActiveIncidentKind => activeIncidentKind;
    public string ActiveIncidentId => activeIncidentId;
    public string ActiveIncidentText => activeIncidentText;
    public float IncidentRemainingSeconds => incidentRemainingSeconds;
    public bool IsOutdoorRestSpot => zoneType == ExteriorZoneType.OutdoorRestSpot;
    public bool CanRunReceptionWork =>
        (zoneType == ExteriorZoneType.ReceptionPoint || zoneType == ExteriorZoneType.IncidentPoint)
        && (receptionReadiness < ReadinessCompleteThreshold
            || waitingVisitors > 0
            || HasActiveIncident);
    public bool CanRunPatrolWork =>
        (zoneType == ExteriorZoneType.GuardPost
            || zoneType == ExteriorZoneType.PatrolPoint
            || zoneType == ExteriorZoneType.IncidentPoint)
        && (patrolReadiness < ReadinessCompleteThreshold
            || HasActiveIncident);
    public bool CanRunExteriorCleanWork => cleanliness < CleanWorkThreshold;
    public bool CanRunExteriorRepairWork => damage > 0.01f;

    public void InitializeRuntime(
        Grid grid,
        Vector2Int position,
        ExteriorZoneType type,
        BuildingSO definition,
        ExteriorZoneSaveData savedState = null)
    {
        zoneType = type;
        zoneId = savedState != null && !string.IsNullOrWhiteSpace(savedState.zoneId)
            ? savedState.zoneId
            : CreateZoneId(type, position);

        BuildingSO data = definition
            ?? throw new ArgumentNullException(nameof(definition));

        SetGrid(grid);
        Initialization(data, position);
        transform.position = grid != null
            ? grid.GetWorldPos(position)
            : new Vector3(position.x, position.y, 0f);
        ApplySaveData(savedState);

        bool registeredOnGrid = grid != null
            && grid.RegisterOccupant(this, data.layer, buildPoses, false);
        if (!registeredOnGrid)
        {
            Debug.LogWarning($"Exterior zone '{zoneId}' could not register at {position}.");
        }
    }

    internal override float GetLegacyWorkUrgency(FacilityWorkType workType)
    {
        return base.GetLegacyWorkUrgency(workType);
    }

    public void ApplyReceptionWork(float readinessGain, float impressionBonus)
    {
        receptionReadiness = Mathf.Clamp(receptionReadiness + Mathf.Max(0f, readinessGain), 0f, 100f);
        firstImpressionBonus = Mathf.Clamp(firstImpressionBonus + Mathf.Max(0f, impressionBonus), 0f, 25f);
        waitingVisitors = Mathf.Max(0, waitingVisitors - 1);
        completedWorks++;
    }

    public void ApplyPatrolWork(float readinessGain, float detectionBonus)
    {
        patrolReadiness = Mathf.Clamp(patrolReadiness + Mathf.Max(0f, readinessGain), 0f, 100f);
        completedWorks++;
    }

    public void RecordOutdoorRest()
    {
        completedWorks++;
    }

    public void ApplyExteriorCleanWork(float amount)
    {
        cleanliness = Mathf.Clamp(cleanliness + Mathf.Max(0f, amount), 0f, 100f);
        SetCleanliness(cleanliness);
        completedWorks++;
    }

    public void ApplyExteriorRepairWork(float amount)
    {
        damage = Mathf.Clamp(damage - Mathf.Max(0f, amount), 0f, 100f);
        SetDamaged(damage > 0.01f);
        completedWorks++;
    }

    public void ApplyExteriorWear(float cleanlinessLoss, float damageGain)
    {
        cleanliness = Mathf.Clamp(cleanliness - Mathf.Max(0f, cleanlinessLoss), 0f, 100f);
        damage = Mathf.Clamp(damage + Mathf.Max(0f, damageGain), 0f, 100f);
        patrolReadiness = Mathf.Clamp(patrolReadiness - 0.4f, 0f, 100f);
        receptionReadiness = Mathf.Clamp(receptionReadiness - 0.25f, 0f, 100f);
        SetCleanliness(cleanliness);
        SetDamaged(damage > 0.01f);
    }

    public float GetReceptionUrgency()
    {
        return DungeonStory.Exterior.ExteriorActivityRules.GetReceptionUrgency(
            CreateDomainSnapshot(),
            HasActiveIncident);
    }

    public float GetPatrolUrgency()
    {
        return DungeonStory.Exterior.ExteriorActivityRules.GetPatrolUrgency(
            CreateDomainSnapshot(),
            HasActiveIncident);
    }

    public float GetCleanUrgency()
    {
        return Mathf.Clamp((CleanWorkThreshold - cleanliness) * 1.3f, 0f, 80f);
    }

    public float GetRepairUrgency()
    {
        return Mathf.Clamp(30f + damage * 0.75f, 0f, 95f);
    }

    public void SetWaitingVisitors(int count)
    {
        waitingVisitors = Mathf.Max(0, count);
    }

    internal void ProjectIncident(
        ExteriorIncidentKind kind,
        string incidentId,
        string text,
        float remainingSeconds)
    {
        activeIncidentKind = kind;
        activeIncidentId = incidentId ?? string.Empty;
        activeIncidentText = text ?? string.Empty;
        incidentRemainingSeconds = Mathf.Max(0f, remainingSeconds);
    }

    internal void ClearIncidentProjection()
    {
        activeIncidentKind = ExteriorIncidentKind.None;
        activeIncidentId = string.Empty;
        activeIncidentText = string.Empty;
        incidentRemainingSeconds = 0f;
    }

    public ExteriorZoneSaveData CreateSaveData()
    {
        return new ExteriorZoneSaveData
        {
            zoneId = zoneId,
            buildingInstanceId = RequirePersistentInstanceId().Value,
            zoneType = zoneType,
            gridX = centerPos.x,
            gridY = centerPos.y,
            cleanliness = cleanliness,
            damage = damage,
            patrolReadiness = patrolReadiness,
            receptionReadiness = receptionReadiness,
            waitingVisitors = waitingVisitors,
            firstImpressionBonus = firstImpressionBonus,
            completedWorks = completedWorks
        };
    }

    public void ApplySaveData(ExteriorZoneSaveData saveData)
    {
        if (saveData == null)
        {
            SetCleanliness(cleanliness);
            SetDamaged(damage > 0.01f);
            return;
        }

        cleanliness = Mathf.Clamp(saveData.cleanliness, 0f, 100f);
        damage = Mathf.Clamp(saveData.damage, 0f, 100f);
        patrolReadiness = Mathf.Clamp(saveData.patrolReadiness, 0f, 100f);
        receptionReadiness = Mathf.Clamp(saveData.receptionReadiness, 0f, 100f);
        waitingVisitors = Mathf.Max(0, saveData.waitingVisitors);
        firstImpressionBonus = Mathf.Clamp(saveData.firstImpressionBonus, 0f, 25f);
        completedWorks = Mathf.Max(0, saveData.completedWorks);
        SetCleanliness(cleanliness);
        SetDamaged(damage > 0.01f);
    }

    private static string CreateZoneId(ExteriorZoneType type, Vector2Int position)
    {
        return DungeonStory.Exterior.ExteriorZoneId.Create(
            (DungeonStory.Exterior.ExteriorZoneKind)type,
            position.x,
            position.y).Value;
    }

    private DungeonStory.Exterior.ExteriorZoneSnapshot CreateDomainSnapshot()
    {
        return new DungeonStory.Exterior.ExteriorZoneSnapshot(
            new DungeonStory.Exterior.ExteriorZoneId(zoneId),
            PersistentInstanceId,
            (DungeonStory.Exterior.ExteriorZoneKind)zoneType,
            new DungeonStory.Exterior.ExteriorZoneAddress(
                centerPos.x,
                centerPos.y),
            cleanliness,
            damage,
            patrolReadiness,
            receptionReadiness,
            waitingVisitors,
            firstImpressionBonus,
            completedWorks);
    }
}
