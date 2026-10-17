using System;

/// <summary>Detached, single-use trait leg of the research command transaction.</summary>
public sealed class ForbiddenResearchLeapPreparation
{
    private readonly CharacterIdentityStateStore states;
    private readonly string traitId;
    private readonly CharacterIdentityRuleStateSaveData before;
    private readonly string after;
    private bool published;

    internal ForbiddenResearchLeapPreparation(
        CharacterIdentityStateStore states, string characterId, string traitId,
        string projectId, CharacterIdentityRuleStateSaveData before, string after,
        float aftermathUntilSeconds, ExtremeRiskResolution resolution)
    {
        this.states = states;
        CharacterId = characterId;
        this.traitId = traitId;
        ProjectId = projectId;
        this.before = before?.Clone();
        this.after = after;
        AftermathUntilSeconds = aftermathUntilSeconds;
        Resolution = resolution;
    }

    public string CharacterId { get; }
    public string ProjectId { get; }
    public float AftermathUntilSeconds { get; }
    public ExtremeRiskResolution Resolution { get; }

    public bool IsCurrent
    {
        get
        {
            states.TryGet(CharacterId, traitId, ExtremeTraitRuleIds.ForbiddenLeapRuleId, out var current);
            return !published && (before == null ? current == null : current != null
                && before.revision == current.revision
                && string.Equals(before.statePayload, current.statePayload, StringComparison.Ordinal));
        }
    }

    [GameplayInternalOnly("Publish only inside the research/outbox rollback boundary.",
        "BlueprintResearchOutcomeTransaction")]
    public bool TryPublish()
    {
        if (!IsCurrent) return false;
        states.Set(CharacterId, traitId, ExtremeTraitRuleIds.ForbiddenLeapRuleId, 1, after);
        published = true;
        return true;
    }

    [GameplayInternalOnly("Rollback only an uncommitted research command.",
        "BlueprintResearchOutcomeTransaction")]
    public void Rollback()
    {
        if (!published) return;
        states.TryGet(CharacterId, traitId, ExtremeTraitRuleIds.ForbiddenLeapRuleId, out var current);
        if (current == null || current.revision != 1
            || !string.Equals(current.statePayload, after, StringComparison.Ordinal))
            throw new InvalidOperationException("research-leap-rollback-conflict");
        if (before == null)
            states.RemoveRule(CharacterId, traitId, ExtremeTraitRuleIds.ForbiddenLeapRuleId);
        else
            states.Set(CharacterId, traitId, ExtremeTraitRuleIds.ForbiddenLeapRuleId,
                before.revision, before.statePayload);
        published = false;
    }
}
