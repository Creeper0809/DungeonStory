using System;

public sealed class AnimalHusbandryCompletionFact
{
    public AnimalHusbandryCompletionFact(
        AnimalHusbandryPendingOutcomeState pending)
    {
        if (pending == null)
            throw new ArgumentNullException(nameof(pending));
        if (!pending.CompletionIdentityRequired)
        {
            throw new ArgumentException(
                "A retained husbandry completion is required.",
                nameof(pending));
        }
        if (pending.Kind is < AnimalHusbandryOutcomeOperationKind.Tame
                or > AnimalHusbandryOutcomeOperationKind.Slaughter
            || !pending.AnimalId.IsValid
            || !pending.PenId.IsValid
            || !pending.SpeciesId.IsValid
            || pending.AbsoluteDay < 1)
        {
            throw new ArgumentException(
                "A canonical husbandry completion identity and day are required.",
                nameof(pending));
        }

        var workerId = new CharacterId(pending.WorkerId);
        if (!workerId.IsValid
            || !string.Equals(
                pending.WorkerId,
                pending.WorkerId?.Trim(),
                StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "A canonical husbandry completion worker is required.",
                nameof(pending));
        }

        _ = AnimalHusbandryDomainOperationIdentity.RequireSequence(
            pending.OperationId,
            pending.Kind,
            pending.AnimalId.Value);

        if (!IsOpaquePublicationKey(pending.PublicationKey))
        {
            throw new ArgumentException(
                "A bounded opaque parent publication key is required.",
                nameof(pending));
        }
        if (pending.CompletedWorkMilliUnits <= 0
            || !float.IsFinite(pending.CompletionContributionWorkUnits)
            || pending.CompletionContributionWorkUnits <= 0f
            || pending.CompletionContributionWorkUnits
                > pending.CompletedWorkMilliUnits / 1000f + 0.001f)
        {
            throw new ArgumentException(
                "A positive completion contribution within completed work is required.",
                nameof(pending));
        }
        if (!Enum.IsDefined(
                typeof(CharacterCommandOrigin),
                pending.CompletionOrigin)
            || string.IsNullOrWhiteSpace(
                pending.CompletionSkillModifierProvenance))
        {
            throw new ArgumentException(
                "Completion origin and skill-modifier provenance are required.",
                nameof(pending));
        }

        WorkOperationId = pending.OperationId;
        Kind = pending.Kind;
        AnimalId = pending.AnimalId;
        PenId = pending.PenId;
        SpeciesId = pending.SpeciesId;
        WorkerId = workerId;
        AbsoluteDay = pending.AbsoluteDay;
        ParentPublicationKey = pending.PublicationKey;
        Origin = pending.CompletionOrigin;
        ContributionWorkUnits = pending.CompletionContributionWorkUnits;
        SkillModifierProvenance = pending.CompletionSkillModifierProvenance;
    }

    public string WorkOperationId { get; }
    public AnimalHusbandryOutcomeOperationKind Kind { get; }
    public WildlifeInstanceId AnimalId { get; }
    public BuildingInstanceId PenId { get; }
    public WildlifeSpeciesId SpeciesId { get; }
    public CharacterId WorkerId { get; }
    public int AbsoluteDay { get; }
    public string ParentPublicationKey { get; }
    public CharacterCommandOrigin Origin { get; }
    public float ContributionWorkUnits { get; }
    public string SkillModifierProvenance { get; }

    private static bool IsOpaquePublicationKey(string value)
    {
        if (string.IsNullOrWhiteSpace(value)
            || value.Length > 512
            || !string.Equals(value, value.Trim(), StringComparison.Ordinal))
        {
            return false;
        }

        foreach (char character in value)
        {
            if (char.IsControl(character)) return false;
        }
        return true;
    }
}
