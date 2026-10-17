using System;
using System.Linq;

public enum ResearchWorkCause { Work, ForbiddenLeap, ImmediateCompletion, KnowledgeProgress }

/// <summary>Frozen domain facts awaiting observation, never a log receipt or a replayed command.</summary>
[Serializable]
public sealed class ResearchWorkPublicationData
{
    public long revision;
    public int absoluteDay;
    public bool isProject;
    public int blueprintId = -1;
    public string subjectId = string.Empty;
    public string subjectName = string.Empty;
    public string researcherId = string.Empty;
    public string researcherName = string.Empty;
    public string facilityId = string.Empty;
    public string facilityName = string.Empty;
    public float before;
    public float after;
    public float required;
    public float approvedWork;
    public bool completed;
    public ResearchWorkCause cause;
    public int leapOutcome;
    public float leapPrimary;
    public float leapSecondary;
    public float leapDelta;
    public ulong leapHash;
    public float aftermathUntil;
    public ResearchUnlockFactData[] unlocks = Array.Empty<ResearchUnlockFactData>();
    public bool hasEquipment;
    public ResearchEquipmentUseData equipment;

    public ResearchWorkPublicationData Clone()
    {
        var copy = (ResearchWorkPublicationData)MemberwiseClone();
        copy.unlocks = unlocks?.Select(value => value?.Clone()).ToArray();
        copy.equipment = equipment?.Clone();
        return copy;
    }
}

[Serializable]
public sealed class ResearchUnlockFactData
{
    public string typeId = string.Empty;
    public string categoryLabel = string.Empty;
    public string valueId = string.Empty;
    public string displayName = string.Empty;
    public string codexDetail = string.Empty;
    public ResearchUnlockFactData Clone() => (ResearchUnlockFactData)MemberwiseClone();
}

[Serializable]
public sealed class ResearchWorkPendingPublication
{
    public bool active;
    public string publicationKey = string.Empty;
    public ResearchWorkPublicationData fact;
    public ResearchWorkPendingPublication Clone() => new()
    { active = active, publicationKey = publicationKey, fact = fact?.Clone() };
}

[Serializable]
public sealed class ResearchEquipmentUseData
{
    public string stackId = string.Empty;
    public string itemId = string.Empty;
    public string facilityId = string.Empty;
    public long assignmentSequence;
    public long revisionBefore;
    public long revisionAfter;
    public double maximum;
    public double before;
    public double after;
    public double requestedWear;
    public ResearchEquipmentUseData Clone() => (ResearchEquipmentUseData)MemberwiseClone();
}
