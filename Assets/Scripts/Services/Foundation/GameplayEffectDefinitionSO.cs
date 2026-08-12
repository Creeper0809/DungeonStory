using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "DungeonStory/Effects/Gameplay Effect", order = 0)]
public sealed class GameplayEffectDefinitionSO : ScriptableObject
{
    [SerializeField] private int numericId;
    [SerializeField] private string effectId = string.Empty;
    [SerializeField] private string targetId = string.Empty;
    [SerializeField] private GameplayEffectOperation operation;
    [SerializeField] private GameplayEffectProjectionPhase projectionPhase;
    [SerializeField] private GameplayEffectSourceKind allowedSources =
        GameplayEffectSourceKind.All;
    [SerializeField] private GameplayEffectStackingPolicy stackingPolicy =
        GameplayEffectStackingPolicy.StackAll;
    [SerializeField] private float minimumResult = float.MinValue;
    [SerializeField] private float maximumResult = float.MaxValue;

    public string EffectId => effectId?.Trim() ?? string.Empty;
    public int NumericId => numericId;
    public string TargetId => targetId?.Trim() ?? string.Empty;
    public GameplayEffectOperation Operation => operation;
    public GameplayEffectProjectionPhase ProjectionPhase => projectionPhase;
    public GameplayEffectSourceKind AllowedSources => allowedSources;
    public GameplayEffectStackingPolicy StackingPolicy => stackingPolicy;
    public float MinimumResult => minimumResult;
    public float MaximumResult => maximumResult;

#if UNITY_EDITOR
    public void Configure(
        int numericId,
        string stableEffectId,
        string stableTargetId,
        GameplayEffectOperation authoredOperation,
        GameplayEffectProjectionPhase phase,
        GameplayEffectSourceKind sources,
        GameplayEffectStackingPolicy stacking,
        float minimum,
        float maximum)
    {
        this.numericId = numericId;
        effectId = stableEffectId?.Trim() ?? string.Empty;
        targetId = stableTargetId?.Trim() ?? string.Empty;
        operation = authoredOperation;
        projectionPhase = phase;
        allowedSources = sources;
        stackingPolicy = stacking;
        minimumResult = minimum;
        maximumResult = maximum;
    }
#endif

    public IReadOnlyList<string> ValidateDefinition()
    {
        List<string> errors = new();
        if (NumericId <= 0) errors.Add("Gameplay effect numeric id must be positive.");
        if (string.IsNullOrWhiteSpace(EffectId))
            errors.Add($"Gameplay effect {NumericId} requires a stable effect id.");
        if (string.IsNullOrWhiteSpace(TargetId))
            errors.Add($"Gameplay effect '{EffectId}' requires a target id.");
        if (AllowedSources == GameplayEffectSourceKind.None)
            errors.Add($"Gameplay effect '{EffectId}' requires an allowed source.");
        if (float.IsNaN(MinimumResult) || float.IsNaN(MaximumResult)
            || MinimumResult > MaximumResult)
            errors.Add($"Gameplay effect '{EffectId}' has invalid result bounds.");
        return errors;
    }
}
