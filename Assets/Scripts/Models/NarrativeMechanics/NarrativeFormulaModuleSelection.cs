using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

public enum NarrativeFormulaModulePolarity
{
    Positive = 0,
    Drawback = 1
}

/// <summary>
/// One authored module that C# has already proven individually reachable for a
/// specific pending operation. This is an immutable runtime projection; the
/// ScriptableObject definition remains the authoring authority.
/// </summary>
public sealed class NarrativeFormulaModuleOffer
{
    public NarrativeFormulaModuleOffer(
        string moduleId,
        NarrativeFormulaModulePolarity polarity,
        string semanticDescription,
        NarrativeFormulaCapabilityInput capability,
        int minimumFeasibilityCost,
        IEnumerable<string> requiredCompanionModuleIds = null,
        IEnumerable<string> requiredCompanionGroups = null)
    {
        ModuleId = RequireCanonical(moduleId, nameof(moduleId));
        if (!Enum.IsDefined(typeof(NarrativeFormulaModulePolarity), polarity))
            throw new ArgumentOutOfRangeException(nameof(polarity));
        Polarity = polarity;
        SemanticDescription = RequireCanonical(
            semanticDescription, nameof(semanticDescription));
        Capability = capability ?? throw new ArgumentNullException(nameof(capability));
        if (minimumFeasibilityCost < 0
            || minimumFeasibilityCost > NarrativeFormulaCore.MaximumFormulaBudget)
            throw new ArgumentOutOfRangeException(nameof(minimumFeasibilityCost));
        MinimumFeasibilityCost = minimumFeasibilityCost;
        RequiredCompanionModuleIds = CanonicalSet(requiredCompanionModuleIds);
        RequiredCompanionGroups = CanonicalSet(requiredCompanionGroups);
    }

    public string ModuleId { get; }
    public NarrativeFormulaModulePolarity Polarity { get; }
    public string SemanticDescription { get; }
    public NarrativeFormulaCapabilityInput Capability { get; }
    public int MinimumFeasibilityCost { get; }
    public IReadOnlyList<string> RequiredCompanionModuleIds { get; }
    public IReadOnlyList<string> RequiredCompanionGroups { get; }

    private static IReadOnlyList<string> CanonicalSet(IEnumerable<string> values)
    {
        string[] result = (values ?? Array.Empty<string>())
            .Select(value => RequireCanonical(value, nameof(values)))
            .OrderBy(value => value, StringComparer.Ordinal).ToArray();
        if (result.Distinct(StringComparer.Ordinal).Count() != result.Length)
            throw new ArgumentException("Required companion values must be distinct.", nameof(values));
        return Array.AsReadOnly(result);
    }

    private static string RequireCanonical(string value, string name)
    {
        string canonical = value?.Trim() ?? string.Empty;
        if (canonical.Length == 0
            || !string.Equals(canonical, value, StringComparison.Ordinal))
            throw new ArgumentException("A canonical non-empty value is required.", name);
        return canonical;
    }
}

public sealed class NarrativeFormulaModuleSelectionRequest
{
    public NarrativeFormulaModuleSelectionRequest(
        string selectionId,
        IEnumerable<NarrativeFormulaModuleOffer> offers,
        IEnumerable<string> evidenceFactIds,
        int maximumPositiveModules,
        int maximumDrawbackModules)
    {
        SelectionId = RequireCanonical(selectionId, nameof(selectionId));
        NarrativeFormulaModuleOffer[] offered = (offers
                ?? throw new ArgumentNullException(nameof(offers)))
            .OrderBy(value => value?.ModuleId, StringComparer.Ordinal).ToArray();
        if (offered.Length == 0 || offered.Any(value => value == null)
            || offered.Select(value => value.ModuleId)
                .Distinct(StringComparer.Ordinal).Count() != offered.Length)
            throw new ArgumentException(
                "A module-selection request requires distinct non-null offers.",
                nameof(offers));
        if (offered.All(value => value.Polarity != NarrativeFormulaModulePolarity.Positive))
            throw new ArgumentException(
                "A module-selection request requires at least one positive offer.",
                nameof(offers));
        if (maximumPositiveModules < 1 || maximumPositiveModules > 3)
            throw new ArgumentOutOfRangeException(nameof(maximumPositiveModules));
        if (maximumDrawbackModules < 0 || maximumDrawbackModules > 3)
            throw new ArgumentOutOfRangeException(nameof(maximumDrawbackModules));
        string[] evidence = (evidenceFactIds
                ?? throw new ArgumentNullException(nameof(evidenceFactIds)))
            .Select(value => RequireCanonical(value, nameof(evidenceFactIds)))
            .OrderBy(value => value, StringComparer.Ordinal).ToArray();
        if (evidence.Length == 0
            || evidence.Distinct(StringComparer.Ordinal).Count() != evidence.Length)
            throw new ArgumentException(
                "Offered evidence IDs must be non-empty and distinct.",
                nameof(evidenceFactIds));
        Offers = Array.AsReadOnly(offered);
        EvidenceFactIds = Array.AsReadOnly(evidence);
        MaximumPositiveModules = maximumPositiveModules;
        MaximumDrawbackModules = maximumDrawbackModules;
    }

    public string SelectionId { get; }
    public IReadOnlyList<NarrativeFormulaModuleOffer> Offers { get; }
    public IReadOnlyList<string> EvidenceFactIds { get; }
    public int MaximumPositiveModules { get; }
    public int MaximumDrawbackModules { get; }

    private static string RequireCanonical(string value, string name)
    {
        string canonical = value?.Trim() ?? string.Empty;
        if (canonical.Length == 0
            || !string.Equals(canonical, value, StringComparison.Ordinal))
            throw new ArgumentException("A canonical non-empty value is required.", name);
        return canonical;
    }
}

public sealed class NarrativeFormulaModuleSelectionChoice
{
    public NarrativeFormulaModuleSelectionChoice(
        string selectionId,
        IEnumerable<string> positiveModuleIds,
        IEnumerable<string> drawbackModuleIds,
        IEnumerable<string> evidenceFactIds)
    {
        SelectionId = selectionId ?? string.Empty;
        PositiveModuleIds = (positiveModuleIds ?? Array.Empty<string>()).ToArray();
        DrawbackModuleIds = (drawbackModuleIds ?? Array.Empty<string>()).ToArray();
        EvidenceFactIds = (evidenceFactIds ?? Array.Empty<string>()).ToArray();
    }

    public string SelectionId { get; }
    public IReadOnlyList<string> PositiveModuleIds { get; }
    public IReadOnlyList<string> DrawbackModuleIds { get; }
    public IReadOnlyList<string> EvidenceFactIds { get; }
}

public sealed class NarrativeFormulaValidatedModuleSelection
{
    internal NarrativeFormulaValidatedModuleSelection(
        IEnumerable<NarrativeFormulaModuleOffer> positive,
        IEnumerable<NarrativeFormulaModuleOffer> drawbacks,
        IEnumerable<string> evidenceFactIds)
    {
        PositiveModules = new ReadOnlyCollection<NarrativeFormulaModuleOffer>(
            positive.ToArray());
        DrawbackModules = new ReadOnlyCollection<NarrativeFormulaModuleOffer>(
            drawbacks.ToArray());
        EvidenceFactIds = new ReadOnlyCollection<string>(evidenceFactIds.ToArray());
    }

    public IReadOnlyList<NarrativeFormulaModuleOffer> PositiveModules { get; }
    public IReadOnlyList<NarrativeFormulaModuleOffer> DrawbackModules { get; }
    public IReadOnlyList<string> EvidenceFactIds { get; }
}

public sealed class NarrativeFormulaSelectedModuleResolution
{
    public NarrativeFormulaSelectedModuleResolution(
        NarrativeFormulaCandidate positive,
        NarrativeFormulaCandidate drawbackSeverity,
        IEnumerable<string> positiveModuleIds,
        IEnumerable<string> drawbackModuleIds)
    {
        Positive = positive ?? throw new ArgumentNullException(nameof(positive));
        DrawbackSeverity = drawbackSeverity;
        PositiveModuleIds = Array.AsReadOnly((positiveModuleIds
            ?? throw new ArgumentNullException(nameof(positiveModuleIds))).ToArray());
        DrawbackModuleIds = Array.AsReadOnly((drawbackModuleIds
            ?? throw new ArgumentNullException(nameof(drawbackModuleIds))).ToArray());
    }

    public NarrativeFormulaCandidate Positive { get; }
    public NarrativeFormulaCandidate DrawbackSeverity { get; }
    public IReadOnlyList<string> PositiveModuleIds { get; }
    public IReadOnlyList<string> DrawbackModuleIds { get; }
}

public static class NarrativeFormulaModuleSelectionValidator
{
    public static bool TryValidate(
        NarrativeFormulaModuleSelectionRequest request,
        NarrativeFormulaModuleSelectionChoice choice,
        out NarrativeFormulaValidatedModuleSelection selection,
        out string error)
    {
        selection = null;
        error = string.Empty;
        if (request == null || choice == null)
            return Fail("Module-selection request and response are required.", out error);
        if (!string.Equals(request.SelectionId, choice.SelectionId,
                StringComparison.Ordinal))
            return Fail("selectionId is stale or does not match the pending offer.", out error);
        if (!TryCanonicalIds(choice.PositiveModuleIds, "positiveModuleIds",
                out string[] positiveIds, out error)
            || !TryCanonicalIds(choice.DrawbackModuleIds, "drawbackModuleIds",
                out string[] drawbackIds, out error)
            || !TryCanonicalIds(choice.EvidenceFactIds, "evidenceFactIds",
                out string[] evidenceIds, out error))
            return false;
        if (positiveIds.Length < 1
            || positiveIds.Length > request.MaximumPositiveModules)
            return Fail(
                "At least one positive module is required and the profile limit cannot be exceeded.",
                out error);
        if (drawbackIds.Length > request.MaximumDrawbackModules)
            return Fail("The drawback module count exceeds the profile limit.", out error);
        if (positiveIds.Intersect(drawbackIds, StringComparer.Ordinal).Any())
            return Fail("A module cannot be selected as both positive and drawback.", out error);
        if (evidenceIds.Length == 0
            || evidenceIds.Except(request.EvidenceFactIds, StringComparer.Ordinal).Any())
            return Fail("evidenceFactIds must cite only facts in the pending offer.", out error);

        Dictionary<string, NarrativeFormulaModuleOffer> offered = request.Offers
            .ToDictionary(value => value.ModuleId, StringComparer.Ordinal);
        if (!TryResolve(positiveIds, NarrativeFormulaModulePolarity.Positive,
                offered, out NarrativeFormulaModuleOffer[] positives, out error)
            || !TryResolve(drawbackIds, NarrativeFormulaModulePolarity.Drawback,
                offered, out NarrativeFormulaModuleOffer[] drawbacks, out error))
            return false;
        NarrativeFormulaModuleOffer[] all = positives.Concat(drawbacks).ToArray();
        HashSet<string> selectedIds = all.Select(value => value.ModuleId)
            .ToHashSet(StringComparer.Ordinal);
        HashSet<string> selectedGroups = all
            .SelectMany(value => value.Capability.Descriptor.ConflictGroups)
            .ToHashSet(StringComparer.Ordinal);
        foreach (NarrativeFormulaModuleOffer module in all)
        {
            if (module.RequiredCompanionModuleIds.Any(value => !selectedIds.Contains(value)))
                return Fail($"Selected module '{module.ModuleId}' is missing a required companion module.", out error);
            if (module.RequiredCompanionGroups.Any(value => !selectedGroups.Contains(value)))
                return Fail($"Selected module '{module.ModuleId}' is missing a required companion group.", out error);
        }
        for (int first = 0; first < all.Length; first++)
        for (int second = first + 1; second < all.Length; second++)
        {
            NarrativeFormulaCapabilityDescriptor left = all[first].Capability.Descriptor;
            NarrativeFormulaCapabilityDescriptor right = all[second].Capability.Descriptor;
            if (left.ConflictGroups.Intersect(right.ConflictGroups,
                    StringComparer.Ordinal).Any()
                || left.ForbiddenSynergies.Contains(right.CapabilityId,
                    StringComparer.Ordinal)
                || right.ForbiddenSynergies.Contains(left.CapabilityId,
                    StringComparer.Ordinal))
                return Fail(
                    $"Selected modules '{all[first].ModuleId}' and '{all[second].ModuleId}' conflict.",
                    out error);
        }

        selection = new NarrativeFormulaValidatedModuleSelection(
            positives, drawbacks, evidenceIds);
        return true;
    }

    private static bool TryResolve(
        IEnumerable<string> ids,
        NarrativeFormulaModulePolarity expected,
        IReadOnlyDictionary<string, NarrativeFormulaModuleOffer> offered,
        out NarrativeFormulaModuleOffer[] modules,
        out string error)
    {
        List<NarrativeFormulaModuleOffer> result = new();
        foreach (string id in ids)
        {
            if (!offered.TryGetValue(id, out NarrativeFormulaModuleOffer module))
            {
                modules = Array.Empty<NarrativeFormulaModuleOffer>();
                return Fail($"Selected module '{id}' was not offered.", out error);
            }
            if (module.Polarity != expected)
            {
                modules = Array.Empty<NarrativeFormulaModuleOffer>();
                return Fail($"Selected module '{id}' has the wrong polarity.", out error);
            }
            result.Add(module);
        }
        modules = result.ToArray();
        error = string.Empty;
        return true;
    }

    private static bool TryCanonicalIds(
        IEnumerable<string> source,
        string field,
        out string[] ids,
        out string error)
    {
        ids = (source ?? Array.Empty<string>()).ToArray();
        if (ids.Any(value => string.IsNullOrWhiteSpace(value)
                || !string.Equals(value, value.Trim(), StringComparison.Ordinal))
            || ids.Distinct(StringComparer.Ordinal).Count() != ids.Length)
            return Fail($"{field} must contain canonical distinct IDs.", out error);
        error = string.Empty;
        return true;
    }

    private static bool Fail(string message, out string error)
    {
        error = message;
        return false;
    }
}
