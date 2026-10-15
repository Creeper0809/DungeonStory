using System;
using System.Collections.Generic;
using System.Linq;

// Domain completion remains observable even without any presentation observer.
public readonly struct V20ContentEffectsResolvedEvent
{
    private readonly V20ContentEffect[] effects;
    private readonly V20ResolvedEventResult? resolution;

    public V20ContentEffectsResolvedEvent(string definitionId, string resolutionId,
        IReadOnlyList<V20ContentEffect> effects, bool physicalEffectsApplied, string occurrenceInstanceId = "")
    {
        DefinitionId = definitionId ?? string.Empty;
        ResolutionId = resolutionId ?? string.Empty;
        this.effects = CopyEffects(effects);
        PhysicalEffectsApplied = physicalEffectsApplied;
        OccurrenceInstanceId = occurrenceInstanceId?.Trim() ?? string.Empty;
        resolution = null;
    }

    public V20ContentEffectsResolvedEvent(V20ResolvedEventResult result, bool physicalEffectsApplied)
        : this(result.DefinitionId, result.ResolutionId, result.Effects,
            physicalEffectsApplied, result.OccurrenceInstanceId)
    { resolution = CopyResult(result); }

    public string DefinitionId { get; }
    public string ResolutionId { get; }
    public IReadOnlyList<V20ContentEffect> Effects => Array.AsReadOnly(CopyEffects(effects));
    public bool PhysicalEffectsApplied { get; }
    public string OccurrenceInstanceId { get; }

    public bool TryGetResolution(out V20ResolvedEventResult result)
    {
        result = resolution.HasValue ? CopyResult(resolution.Value) : default;
        return resolution.HasValue;
    }

    private static V20ContentEffect[] CopyEffects(IReadOnlyList<V20ContentEffect> source) =>
        (source ?? Array.Empty<V20ContentEffect>()).Select(value => value == null ? null : new V20ContentEffect
        { kind = value.kind, targetId = value.targetId, amount = value.amount, durationDays = value.durationDays }).ToArray();

    private static V20ResolvedEventResult CopyResult(V20ResolvedEventResult value) => new(
        value.DefinitionId, value.ResolutionId, Array.AsReadOnly(CopyEffects(value.Effects)),
        Array.AsReadOnly((value.ParticipantCharacterIds ?? Array.Empty<string>()).ToArray()),
        value.ContextFactionId, value.InstanceId, value.DisplayName, value.RiskTier, value.RiskReason,
        value.TerminalActionLabel, value.TerminalOutcomeNarrative, value.TerminalEffectKinds,
        value.ObservedCause, value.OccurrenceInstanceId, value.Applications);
}
