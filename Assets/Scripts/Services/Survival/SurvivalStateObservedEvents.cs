using System;
using System.Collections.Generic;
using System.Linq;

public readonly struct DeprivationBurdenSample
{
    public DeprivationBurdenSample(CharacterId characterId, float highestBurden)
    {
        if (!characterId.IsValid || !float.IsFinite(highestBurden) || highestBurden < 0f || highestBurden > 100f)
            throw new ArgumentException("A deprivation sample requires a character and finite burden in 0..100.");
        CharacterId = characterId;
        HighestBurden = highestBurden;
    }
    public CharacterId CharacterId { get; }
    public float HighestBurden { get; }
}

// A complete pass of current domain state, not a list of display alerts.
public readonly struct DeprivationBurdenSnapshotEvent
{
    public DeprivationBurdenSnapshotEvent(int restoreRevision, IEnumerable<DeprivationBurdenSample> samples)
    {
        if (restoreRevision < 0 || samples == null) throw new ArgumentException("Invalid deprivation snapshot.");
        var copy = samples.ToArray();
        var ids = new HashSet<CharacterId>();
        foreach (var sample in copy)
            if (!sample.CharacterId.IsValid || !ids.Add(sample.CharacterId)
                || !float.IsFinite(sample.HighestBurden) || sample.HighestBurden < 0f || sample.HighestBurden > 100f)
                throw new ArgumentException("Invalid or duplicate deprivation sample.");
        RestoreRevision = restoreRevision;
        Samples = Array.AsReadOnly(copy);
    }
    public int RestoreRevision { get; }
    public IReadOnlyList<DeprivationBurdenSample> Samples { get; }
}

public readonly struct DeprivationCharacterResetEvent
{
    public DeprivationCharacterResetEvent(CharacterId characterId)
    {
        if (!characterId.IsValid) throw new ArgumentException("A reset requires a character.");
        CharacterId = characterId;
    }
    public CharacterId CharacterId { get; }
}

public readonly struct SurvivalWeatherSampledEvent
{
    public SurvivalWeatherSampledEvent(string frontId, SurvivalWeatherType weather)
    {
        if (!Enum.IsDefined(typeof(SurvivalWeatherType), weather))
            throw new ArgumentOutOfRangeException(nameof(weather));
        FrontId = frontId ?? string.Empty;
        Weather = weather;
    }
    public string FrontId { get; }
    public SurvivalWeatherType Weather { get; }
}

public enum SurvivalFacilityWorkResultKind { WaterDrawing, Cooking, DetoxTreatment, Treatment, Refueling }

// Synchronous observation only. These references are not retained or serialized.
public readonly struct SurvivalFacilityWorkResultEvent
{
    public SurvivalFacilityWorkResultEvent(SurvivalFacilityWorkResultKind kind,
        IBuildingVisitorPort worker, BuildableObject facility, int quantity,
        bool preservedFood = false, CharacterActor patient = null, string patientId = "")
    {
        if (!Enum.IsDefined(typeof(SurvivalFacilityWorkResultKind), kind) || quantity < 0)
            throw new ArgumentException("Invalid survival work result.");
        Kind = kind; Worker = worker; Facility = facility; Quantity = quantity;
        PreservedFood = preservedFood; Patient = patient; PatientId = patientId ?? string.Empty;
    }
    public SurvivalFacilityWorkResultKind Kind { get; }
    public IBuildingVisitorPort Worker { get; }
    public BuildableObject Facility { get; }
    public int Quantity { get; }
    public bool PreservedFood { get; }
    public CharacterActor Patient { get; }
    public string PatientId { get; }
}

public readonly struct DeprivationBreakdownStartedEvent
{
    public DeprivationBreakdownStartedEvent(CharacterActor actor, CharacterBreakdownKind kind, DeprivationKind cause)
    {
        if (actor == null || !Enum.IsDefined(typeof(CharacterBreakdownKind), kind)
            || !Enum.IsDefined(typeof(DeprivationKind), cause))
            throw new ArgumentException("Invalid deprivation breakdown observation.");
        Actor = actor; Kind = kind; Cause = cause;
    }
    public CharacterActor Actor { get; }
    public CharacterBreakdownKind Kind { get; }
    public DeprivationKind Cause { get; }
}
