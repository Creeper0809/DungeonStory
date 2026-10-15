using System;
using DungeonStory.Foundation.Facts;

public enum CharacterDeprivationExperienceKind { PersonalTaboo, WitnessedTaboo }

// A transient notification of an already-applied gameplay experience.
// It neither owns the character state nor requests a new physical effect.
public readonly struct CharacterDeprivationExperienceFact
{
    public CharacterDeprivationExperienceFact(CharacterDeprivationExperienceKind kind, CharacterActor actor,
        string subjectId, string memory, float magnitude, int absoluteDay)
    {
        if (actor == null || !Enum.IsDefined(typeof(CharacterDeprivationExperienceKind), kind)
            || !DomainFactValueSyntax.IsText(memory, 4096, false) || absoluteDay < 0
            || float.IsNaN(magnitude) || float.IsInfinity(magnitude)
            || subjectId == null || subjectId.Length > 0 && !DomainFactValueSyntax.IsIdentity(subjectId))
            throw new ArgumentException("deprivation-experience-fact-invalid");
        Actor = actor; CharacterId = CharacterPersistentIdentity.Require(actor).Value;
        Kind = kind; SubjectId = subjectId; Memory = memory; Magnitude = magnitude; AbsoluteDay = absoluteDay;
    }
    public CharacterDeprivationExperienceKind Kind { get; }
    public CharacterActor Actor { get; }
    public string CharacterId { get; }
    public string SubjectId { get; }
    public string Memory { get; }
    public float Magnitude { get; }
    public int AbsoluteDay { get; }
}
