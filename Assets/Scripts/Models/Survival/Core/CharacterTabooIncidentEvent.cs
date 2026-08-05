using UnityEngine;

public readonly struct CharacterTabooIncidentEvent<TSource>
    where TSource : class
{
    public CharacterTabooIncidentEvent(
        TSource source,
        Vector2Int position,
        string memory,
        string witnessLabel,
        float witnessMood)
    {
        Source = source;
        Position = position;
        Memory = memory ?? string.Empty;
        WitnessLabel = witnessLabel ?? string.Empty;
        WitnessMood = witnessMood;
    }

    public TSource Source { get; }
    public Vector2Int Position { get; }
    public string Memory { get; }
    public string WitnessLabel { get; }
    public float WitnessMood { get; }
}
