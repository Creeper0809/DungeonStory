using System;
using System.Globalization;

/// <summary>Identity at request creation, never rebound by transport retries or restore.</summary>
[Serializable]
public sealed class CharacterNarrativeRequestOwnerBinding
{
    public const int CurrentVersion = 1;
    public int version;
    public string persistentId = string.Empty;
    public long lifecycleRevision = -1;
    public int generationRevision = -1;

    public bool IsWellFormed => version == CurrentVersion
        && !string.IsNullOrWhiteSpace(persistentId)
        && string.Equals(persistentId, persistentId.Trim(), StringComparison.Ordinal)
        && lifecycleRevision >= 0 && generationRevision >= 0;

    public string IdentitySuffix => "g" + generationRevision.ToString(CultureInfo.InvariantCulture)
        + ":l" + lifecycleRevision.ToString(CultureInfo.InvariantCulture);

    public CharacterNarrativeRequestOwnerBinding Clone() =>
        (CharacterNarrativeRequestOwnerBinding)MemberwiseClone();

    public static CharacterNarrativeRequestOwnerBinding Capture(CharacterProgression progression)
    {
        CharacterActor actor = progression?.Actor;
        if (actor == null || actor.Identity == null || actor.Lifecycle == null
            || !actor.Identity.TypedPersistentId.IsValid || actor.IsDead
            || progression.GrowthState.skillGenerationRevision < 0)
            throw new InvalidOperationException("INCOMPLETE_OWNER_BINDING: a valid persistent identity, living owner and valid revisions are required.");
        return new CharacterNarrativeRequestOwnerBinding
        {
            version = CurrentVersion,
            persistentId = actor.Identity.PersistentId,
            lifecycleRevision = actor.Lifecycle.LifecycleRevision,
            generationRevision = progression.GrowthState.skillGenerationRevision
        };
    }

    public bool Matches(CharacterProgression progression, out string reason)
    {
        CharacterActor actor = progression?.Actor;
        bool matches = IsWellFormed && actor != null && !actor.IsDead
            && actor.Lifecycle != null && actor.Identity != null
            && string.Equals(persistentId, actor.Identity.PersistentId, StringComparison.Ordinal)
            && lifecycleRevision == actor.Lifecycle.LifecycleRevision
            && generationRevision == progression.GrowthState.skillGenerationRevision;
        reason = matches ? string.Empty : "STALE_OWNER: request creation identity or lifecycle/generation revision no longer matches; missing legacy bindings cannot be rebound.";
        return matches;
    }
}
