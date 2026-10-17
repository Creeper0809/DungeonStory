using System;
using System.Collections.Generic;
using System.Linq;

public sealed class AnimalHusbandryWorkFact
{
    private readonly AnimalHusbandryPendingOutcomeState snapshot;

    public AnimalHusbandryWorkFact(
        AnimalHusbandryPendingOutcomeState pending,
        bool proposal = false)
    {
        if (pending == null)
            throw new ArgumentNullException(nameof(pending));

        Validate(pending, proposal);
        Sequence = AnimalHusbandryDomainOperationIdentity.RequireSequence(
            pending.OperationId,
            pending.Kind,
            pending.AnimalId.Value);
        snapshot = pending.Clone();
        StripPublicationState(snapshot);
        IsProposal = proposal;
    }

    public bool IsProposal { get; }
    public long Sequence { get; }

    public AnimalHusbandryPendingOutcomeState CaptureDomainState() =>
        snapshot.Clone();

    private static void StripPublicationState(
        AnimalHusbandryPendingOutcomeState value)
    {
        value.PublicationKey = string.Empty;
        value.LastFailure = string.Empty;
        value.CompletionIdentityRequired = false;
        value.OutcomeCommitted = false;
        value.CompletionPublicationKey = string.Empty;
        value.CompletionOrigin = default;
        value.CompletionContributionWorkUnits = 0f;
        value.CompletionSkillModifierProvenance = string.Empty;
    }

    private static void Validate(
        AnimalHusbandryPendingOutcomeState value,
        bool proposal)
    {
        if (value.Kind is < AnimalHusbandryOutcomeOperationKind.Tame
                or > AnimalHusbandryOutcomeOperationKind.Slaughter
            || value.AbsoluteDay < 1
            || !value.PenId.IsValid
            || !value.AnimalId.IsValid
            || !value.SpeciesId.IsValid
            || !(new CharacterId(value.WorkerId)).IsValid
            || !IsCanonicalRequired(value.WorkerId)
            || !IsCanonicalRequired(value.WorkerDisplayName)
            || value.CarcassStackId == null
            || value.CompletedWorkMilliUnits <= 0
            || value.OutputQuantity < 0
            || value.OutputMassGrams < 0L)
        {
            throw new ArgumentException(
                "A canonical husbandry work identity and metrics are required.",
                nameof(value));
        }

        _ = AnimalHusbandryDomainOperationIdentity.RequireSequence(
            value.OperationId,
            value.Kind,
            value.AnimalId.Value);

        if (value.AnimalStatesBefore == null
            || value.AnimalStatesBefore.Count != 1
            || !MatchesIdentity(value.AnimalStatesBefore[0], value))
        {
            throw new ArgumentException(
                "Husbandry work requires exactly one matching before state.",
                nameof(value));
        }

        int expectedAfterCount = proposal
            || value.Kind == AnimalHusbandryOutcomeOperationKind.Slaughter
                ? 0
                : 1;
        if (value.AnimalStatesAfter == null
            || value.AnimalStatesAfter.Count != expectedAfterCount
            || expectedAfterCount == 1
                && !MatchesIdentity(value.AnimalStatesAfter[0], value))
        {
            throw new ArgumentException(
                "Husbandry work after-state shape does not match its kind.",
                nameof(value));
        }

        ValidateContributions(value);
        ValidateOutput(value, proposal);
    }

    private static bool MatchesIdentity(
        HusbandryAnimalState state,
        AnimalHusbandryPendingOutcomeState owner) =>
        state != null
        && state.AnimalId.Equals(owner.AnimalId)
        && state.PenId.Equals(owner.PenId)
        && state.SpeciesId.Equals(owner.SpeciesId);

    private static void ValidateContributions(
        AnimalHusbandryPendingOutcomeState value)
    {
        if (value.Contributions == null || value.Contributions.Count == 0)
        {
            throw new ArgumentException(
                "Husbandry work requires typed contributions.",
                nameof(value));
        }

        string previous = null;
        bool containsWorker = false;
        foreach (CraftContributionSaveData contribution in value.Contributions)
        {
            string characterId = contribution?.characterId ?? string.Empty;
            if (contribution == null
                || !(new CharacterId(characterId)).IsValid
                || !IsCanonicalRequired(characterId)
                || !float.IsFinite(contribution.contributedWork)
                || contribution.contributedWork <= 0f
                || !float.IsFinite(contribution.relevantSkill)
                || contribution.relevantSkill < 0f
                || !TryToMilliWork(contribution.contributedWork, out _)
                || previous != null
                    && string.CompareOrdinal(previous, characterId) >= 0)
            {
                throw new ArgumentException(
                    "Husbandry work contributions must be canonical, positive, unique and sorted.",
                    nameof(value));
            }
            containsWorker |= string.Equals(
                characterId,
                value.WorkerId,
                StringComparison.Ordinal);
            previous = characterId;
        }
        if (!containsWorker)
        {
            throw new ArgumentException(
                "The completing husbandry worker is absent from the contribution set.",
                nameof(value));
        }
    }

    private static void ValidateOutput(
        AnimalHusbandryPendingOutcomeState value,
        bool proposal)
    {
        if (value.OutputStacks == null)
        {
            throw new ArgumentException(
                "Husbandry output stacks must not be null.",
                nameof(value));
        }

        bool producesOutput = value.Kind is
            AnimalHusbandryOutcomeOperationKind.CollectProduct
            or AnimalHusbandryOutcomeOperationKind.CollectManure
            or AnimalHusbandryOutcomeOperationKind.Slaughter;
        if (producesOutput != value.OutputItemId.IsValid
            || producesOutput != (value.OutputQuantity > 0)
            || !producesOutput && value.OutputMassGrams != 0L)
        {
            throw new ArgumentException(
                "Husbandry output item and metrics do not match the work kind.",
                nameof(value));
        }

        if (proposal)
        {
            if (value.OutputStacks.Count != 0
                || !string.IsNullOrEmpty(value.CarcassStackId))
            {
                throw new ArgumentException(
                    "A husbandry proposal cannot contain committed physical output.",
                    nameof(value));
            }
            return;
        }

        if (!producesOutput)
        {
            if (value.OutputStacks.Count != 0
                || !string.IsNullOrEmpty(value.CarcassStackId))
            {
                throw new ArgumentException(
                    "Non-output husbandry work contains physical output evidence.",
                    nameof(value));
            }
            return;
        }

        string previous = null;
        int total = 0;
        foreach (AnimalHusbandryOutputStack output in value.OutputStacks)
        {
            if (output == null
                || !(new ItemStackId(output.StackId)).IsValid
                || !IsCanonicalRequired(output.StackId)
                || output.Quantity <= 0
                || previous != null
                    && string.CompareOrdinal(previous, output.StackId) >= 0)
            {
                throw new ArgumentException(
                    "Husbandry output stacks must be canonical, positive, unique and sorted.",
                    nameof(value));
            }
            total = checked(total + output.Quantity);
            previous = output.StackId;
        }
        if (value.OutputStacks.Count == 0 || total != value.OutputQuantity)
        {
            throw new ArgumentException(
                "Husbandry output stacks do not equal the committed quantity.",
                nameof(value));
        }

        if (value.Kind == AnimalHusbandryOutcomeOperationKind.Slaughter)
        {
            if (!(new ItemStackId(value.CarcassStackId)).IsValid
                || !value.OutputStacks.Any(output => string.Equals(
                    output.StackId,
                    value.CarcassStackId,
                    StringComparison.Ordinal)))
            {
                throw new ArgumentException(
                    "Slaughter work requires its exact committed carcass stack.",
                    nameof(value));
            }
        }
        else if (!string.IsNullOrEmpty(value.CarcassStackId))
        {
            throw new ArgumentException(
                "Non-slaughter work cannot contain a carcass stack.",
                nameof(value));
        }
    }

    private static bool TryToMilliWork(float work, out int milliWork)
    {
        double value = Math.Round(Math.Max(0f, work) * 1000d);
        if (value <= 0d || value > int.MaxValue)
        {
            milliWork = 0;
            return false;
        }
        milliWork = (int)value;
        return true;
    }

    private static bool IsCanonicalRequired(string value) =>
        !string.IsNullOrWhiteSpace(value)
        && string.Equals(value, value.Trim(), StringComparison.Ordinal);
}
