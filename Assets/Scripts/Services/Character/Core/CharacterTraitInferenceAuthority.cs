using System.Collections.Generic;

/// <summary>
/// The progression component remains the sole serialized state authority.
/// Inference uses current queries and explicit publication/rollback commands.
/// </summary>
internal interface ICharacterTraitInferenceAuthority
{
    string RequireCharacterId();
    string IdentityDisplayName { get; }
    string GrowthDisplayName { get; }
    CharacterNarrativeLedger NarrativeLedger { get; }
    IGameplayOutcomeEvidenceUseTransaction EvidenceUseTransaction { get; }
    CharacterAcquiredTraitAggregateState CaptureAcquiredTraitState();
    List<CharacterAcquiredTraitValidationIssue> ValidateAcquiredTraitCandidate(
        CharacterAcquiredTraitAggregateState current,
        CharacterAcquiredTraitAggregateState candidate,
        int expectedRevision,
        CharacterAcquiredTraitSettingsSO settings,
        IEnumerable<CharacterAcquiredTraitModuleSO> moduleDefinitions);
    void ReplaceAcquiredTraitState(CharacterAcquiredTraitAggregateState state);
    void PublishInferredTraitChanged();
}
