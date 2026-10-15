using System;
using System.Collections.Generic;
using System.Globalization;
using DungeonStory.Foundation.Facts;

public sealed class CharacterDeprivationExperienceFactBinding : IDomainFactObserverBinding
{
    public IReadOnlyCollection<Type> FactTypes { get; } =
        Array.AsReadOnly(new[] { typeof(CharacterDeprivationExperienceFact) });
    public IDisposable Connect(DomainFactDispatcher dispatcher) =>
        dispatcher.Subscribe("survival.personal-and-witness-experience", new GameplayPostCommitFactObserver<
            CharacterDeprivationExperienceFact>(Validate, Observe));

    private static string Validate(CharacterDeprivationExperienceFact fact)
    {
        if (fact.Actor == null || fact.Actor.Identity?.PersistentId != fact.CharacterId)
            throw new InvalidOperationException("deprivation-experience-recipient-changed");
        return "deprivation-experience:" + NarrativeInferenceHash.ComputeSha256Utf8(
            fact.Kind + "\n" + fact.CharacterId + "\n" + fact.SubjectId + "\n"
            + fact.Memory + "\n" + fact.Magnitude.ToString("R", CultureInfo.InvariantCulture)
            + "\n" + fact.AbsoluteDay.ToString(CultureInfo.InvariantCulture));
    }

    private static void Observe(CharacterDeprivationExperienceFact fact)
    {
        bool witness = fact.Kind == CharacterDeprivationExperienceKind.WitnessedTaboo;
        fact.Actor.Progression?.RecordNarrative(
            witness ? CharacterNarrativeDomain.Relationship : CharacterNarrativeDomain.Survival,
            witness ? "survival/taboo-witness" : "survival/taboo",
            fact.SubjectId, fact.Memory, fact.Magnitude, fact.AbsoluteDay);
    }
}
