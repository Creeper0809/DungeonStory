#if UNITY_EDITOR
using System;
using System.Collections.Generic;

internal static class NarrativeRequestContextTestFixture
{
    public static NarrativeRequestContextBuilder CreateEmpty()
    {
        EmptyNarrativeAuthority authority = new();
        return new NarrativeRequestContextBuilder(
            authority,
            authority,
            authority,
            authority,
            authority);
    }

    private sealed class EmptyNarrativeAuthority :
        ICharacterNarrativeQuery,
        ICharacterLifeQuery,
        ICharacterNarrativeCatalog,
        ICharacterWorldQuery,
        ICharacterLifetimeQuery
    {
        public int Version => 0;
        public int CharacterVersion => 0;
        public int LifetimeCharacterVersion => 0;

        public IReadOnlyCollection<CharacterNarrativeSnapshot> All =>
            Array.Empty<CharacterNarrativeSnapshot>();
        public IReadOnlyCollection<CharacterLifeRecord> Records =>
            Array.Empty<CharacterLifeRecord>();
        public IReadOnlyList<CharacterActor> Characters =>
            Array.Empty<CharacterActor>();
        public IReadOnlyList<CharacterActor> AllCharacters =>
            Array.Empty<CharacterActor>();

        public IReadOnlyList<CharacterBackgroundDefinitionSO> Backgrounds =>
            Array.Empty<CharacterBackgroundDefinitionSO>();
        public IReadOnlyList<CharacterAmbitionDefinitionSO> Ambitions =>
            Array.Empty<CharacterAmbitionDefinitionSO>();
        public IReadOnlyList<LifeEventDefinitionSO> LifeEvents =>
            Array.Empty<LifeEventDefinitionSO>();
        public IReadOnlyList<SpeciesCultureDefinitionSO> Cultures =>
            Array.Empty<SpeciesCultureDefinitionSO>();
        public IReadOnlyList<CulturalPracticeDefinitionSO> Practices =>
            Array.Empty<CulturalPracticeDefinitionSO>();
        public IReadOnlyList<HeritableTraitDefinitionSO> HeritableTraits =>
            Array.Empty<HeritableTraitDefinitionSO>();
        public IReadOnlyList<ProficiencyDefinitionSO> Proficiencies =>
            Array.Empty<ProficiencyDefinitionSO>();

        public bool TryGet(
            CharacterId characterId,
            out CharacterNarrativeSnapshot snapshot)
        {
            snapshot = default;
            return false;
        }

        bool ICharacterLifeQuery.TryGet(
            CharacterId characterId,
            out CharacterLifeRecord record)
        {
            record = default;
            return false;
        }

        public bool CanPerformPractice(
            CharacterId characterId,
            string practiceId,
            int absoluteDay,
            out int nextAllowedAbsoluteDay) =>
            throw new NotSupportedException(
                "The empty narrative context fixture cannot preview cultural practices.");

        public bool TryPreviewAmbitionProgress(
            CharacterId characterId,
            int amount,
            out AmbitionProgressPreview preview) =>
            throw new NotSupportedException(
                "The empty narrative context fixture cannot preview ambition progress.");

        public CharacterBackgroundDefinitionSO Require(
            CharacterBackgroundId id) =>
            Missing<CharacterBackgroundDefinitionSO>(id.ToString());

        public CharacterAmbitionDefinitionSO Require(
            CharacterAmbitionId id) =>
            Missing<CharacterAmbitionDefinitionSO>(id.ToString());

        public LifeEventDefinitionSO Require(NarrativeEventId id) =>
            Missing<LifeEventDefinitionSO>(id.ToString());

        public SpeciesCultureDefinitionSO Require(SpeciesCultureId id) =>
            Missing<SpeciesCultureDefinitionSO>(id.ToString());

        public SpeciesCultureDefinitionSO RequireDefaultCulture(
            string speciesId) =>
            Missing<SpeciesCultureDefinitionSO>(speciesId);

        public HeritableTraitDefinitionSO RequireHeritable(string traitId) =>
            Missing<HeritableTraitDefinitionSO>(traitId);

        public ProficiencyDefinitionSO Require(
            CharacterProficiencyId id) =>
            Missing<ProficiencyDefinitionSO>(id.ToString());

        private static T Missing<T>(string id) =>
            throw new KeyNotFoundException(
                $"The empty narrative context fixture has no catalog entry '{id}'.");
    }
}
#endif
