using System;
using System.Collections.Generic;

public enum CharacterMedicalProcedureKind
{
    Stabilization = 1,
    Treatment = 2
}

[Serializable]
public sealed class CharacterMedicalPartFactData
{
    public float currentHealth;
    public float maxHealth;
    public float bleedingPerSecond;

    public CharacterMedicalPartFactData Clone() =>
        (CharacterMedicalPartFactData)MemberwiseClone();
}

[Serializable]
public sealed class CharacterMedicalAnatomyFactData
{
    public string nodeId = string.Empty;
    public float currentHealth;
    public float maxHealth;
    public float bleedingPerSecond;
    public bool missing;

    public CharacterMedicalAnatomyFactData Clone() =>
        (CharacterMedicalAnatomyFactData)MemberwiseClone();
}

[Serializable]
public sealed class CharacterMedicalVitalsFactData
{
    public float health;
    public float bloodLoss;
    public bool downed;
    public List<CharacterMedicalPartFactData> parts = new();
    public List<CharacterMedicalAnatomyFactData> anatomyNodes = new();

    public CharacterMedicalVitalsFactData Clone()
    {
        var clone = (CharacterMedicalVitalsFactData)MemberwiseClone();
        clone.parts = CloneParts(parts);
        clone.anatomyNodes = CloneAnatomyNodes(anatomyNodes);
        return clone;
    }

    private static List<CharacterMedicalPartFactData> CloneParts(
        List<CharacterMedicalPartFactData> source)
    {
        if (source == null) return null;
        var clone = new List<CharacterMedicalPartFactData>(source.Count);
        foreach (CharacterMedicalPartFactData value in source)
            clone.Add(value?.Clone());
        return clone;
    }

    private static List<CharacterMedicalAnatomyFactData> CloneAnatomyNodes(
        List<CharacterMedicalAnatomyFactData> source)
    {
        if (source == null) return null;
        var clone = new List<CharacterMedicalAnatomyFactData>(source.Count);
        foreach (CharacterMedicalAnatomyFactData value in source)
            clone.Add(value?.Clone());
        return clone;
    }
}

[Serializable]
public sealed class CharacterMedicalProcedureFactData
{
    public string orderId = string.Empty;
    public string patientId = string.Empty;
    public string patientName = string.Empty;
    public string clinicianId = string.Empty;
    public string clinicianName = string.Empty;
    public string facilityId = string.Empty;
    public string materialItemId = string.Empty;
    public int sequence;
    public int absoluteDay;
    public int materialQuantity;
    public CharacterMedicalProcedureKind procedure;
    public CharacterMedicalSupplyKind materialKind;
    public float completedWork;
    public float requiredWork;
    public CharacterMedicalVitalsFactData before = new();
    public CharacterMedicalVitalsFactData after = new();

    public CharacterMedicalProcedureFactData Clone()
    {
        var clone = (CharacterMedicalProcedureFactData)MemberwiseClone();
        clone.before = before?.Clone();
        clone.after = after?.Clone();
        return clone;
    }
}

[Serializable]
public sealed class CharacterMedicalPendingFactData
{
    public string publicationKey = string.Empty;
    public CharacterMedicalProcedureFactData fact = new();

    public CharacterMedicalPendingFactData Clone()
    {
        var clone = (CharacterMedicalPendingFactData)MemberwiseClone();
        clone.fact = fact?.Clone();
        return clone;
    }
}
