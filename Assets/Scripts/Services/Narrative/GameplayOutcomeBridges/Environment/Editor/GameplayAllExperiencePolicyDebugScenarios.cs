using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

/// <summary>
/// Pure coverage harness for the explicitly registered experience policy rules.
/// The environmental exposure observer has its own focused contract suite and is
/// deliberately excluded from the per-type rule cases here.
/// </summary>
public static class GameplayAllExperiencePolicyDebugScenarios
{
    private static readonly string EnvironmentalExposureType =
        EnvironmentOutcomeIds.EnvironmentalExposureThreshold.Value;

    public static string Run(IReadOnlyList<string> registeredTypes)
    {
        var verification = new Verification();
        string[] registered = NormalizeRegisteredTypes(registeredTypes, verification);
        string[] supported = GameplayExperienceObserverRegistration.Default.SupportedOutcomeTypes
            .OrderBy(value => value, StringComparer.Ordinal).ToArray();

        verification.Require(
            registered.SequenceEqual(supported, StringComparer.Ordinal),
            DescribeSetMismatch(registered, supported));
        verification.Require(
            supported.Contains(EnvironmentalExposureType, StringComparer.Ordinal),
            "The focused environmental exposure observer is absent from the default registry.");

        var cases = new List<GameplayExperiencePolicyCase>();
        AddCases(cases, GameplayWorldExperienceRuleCases.Create(), "world");
        AddCases(cases, GameplayLifeExperienceRuleCases.Create(), "life");
        AddCases(cases, GameplayMigratedExperienceRuleCases.Create(), "migrated");

        verification.Require(cases.Count > 0, "No all-type experience policy cases were supplied.");
        verification.Require(
            cases.All(value => value != null),
            "An all-type experience policy case is null.");
        verification.Require(
            cases.All(value => value.Fact != null),
            "An all-type experience policy case has no binding.");
        verification.Require(
            cases.All(value => !string.IsNullOrWhiteSpace(value.Name)),
            "An all-type experience policy case has no readable name.");
        verification.Require(
            cases.All(value => value.Fact.outcomeTypeId != EnvironmentalExposureType),
            "Environmental exposure cases belong in the focused observer contract suite.");

        string[] requiredTypes = supported
            .Where(value => value != EnvironmentalExposureType)
            .ToArray();
        string[] caseTypes = cases.Select(value => value.Fact.outcomeTypeId)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        verification.Require(
            caseTypes.All(value => requiredTypes.Contains(value, StringComparer.Ordinal)),
            "Cases contain types outside the default registry: " + string.Join(", ",
                caseTypes.Except(requiredTypes, StringComparer.Ordinal)));

        foreach (string type in requiredTypes)
        {
            verification.Require(
                cases.Any(value => value.Fact.outcomeTypeId == type
                    && value.Expected != GameplayExperienceCategory.Unassessed),
                "No successful assessed policy case covers registered type: " + type);
        }

        var results = cases
            .OrderBy(value => value.Fact.outcomeTypeId, StringComparer.Ordinal)
            .ThenBy(value => value.Name, StringComparer.Ordinal)
            .Select(value => VerifyCase(value, verification))
            .ToArray();

        var report = new StringBuilder(4096);
        report.AppendLine("Gameplay experience policy coverage");
        report.Append("registered types: ").Append(registered.Length).AppendLine();
        report.Append("covered rule types: ").Append(requiredTypes.Length).AppendLine();
        report.Append("excluded focused type: ").Append(EnvironmentalExposureType).AppendLine();
        var ruleDescriptions = GameplayActionLogCatalog.Definitions.Select(value => value.ExperienceObserver)
            .OfType<GameplayActionExperiencePolicy>().ToDictionary(value => value.OutcomeType, value => value.Description);
        foreach (IGrouping<string, CaseResult> group in results.GroupBy(
                     value => value.OutcomeType, StringComparer.Ordinal))
        {
            report.Append('[').Append(group.Key).AppendLine("]");
            report.Append("policy: ").AppendLine(ruleDescriptions[group.Key]);
            foreach (CaseResult result in group)
            {
                report.Append("- ").Append(OneLine(result.Name))
                    .Append(" | category=").Append(result.Category)
                    .Append(" | importance=")
                    .Append(result.Importance.ToString("0.00", CultureInfo.InvariantCulture))
                    .Append(" | reason=").Append(OneLine(result.Reason))
                    .Append(" | explanationKo=").Append(OneLine(result.ExplanationKo))
                    .AppendLine();
            }
        }
        report.Append("total cases: ").Append(results.Length).AppendLine();
        report.Append("assertions: ").Append(verification.Count).AppendLine();
        return report.ToString();
    }

    private static CaseResult VerifyCase(
        GameplayExperiencePolicyCase policyCase,
        Verification verification)
    {
        GameplayOutcomeEvidenceBindingSnapshot fact = policyCase.Fact;
        string label = fact.outcomeTypeId + "/" + policyCase.Name;
        verification.Require(
            Enum.IsDefined(typeof(GameplayExperienceCategory), policyCase.Expected),
            label + " has an undefined expected category.");

        GameplayExperienceAssessmentSnapshot actual = ObserveUnchanged(fact, label, verification);
        verification.Require(
            actual.category == (int)policyCase.Expected,
            label + " category differs. Expected " + policyCase.Expected
            + ", actual " + (GameplayExperienceCategory)actual.category + ": " + actual.explanationKo);
        verification.Require(
            actual.importance.Equals(GameplayExperienceImportancePolicy.Importance(policyCase.Expected)),
            label + " importance does not match the common policy.");
        verification.Require(
            !string.IsNullOrWhiteSpace(actual.observerId)
            && !string.IsNullOrWhiteSpace(actual.reasonCode)
            && !string.IsNullOrWhiteSpace(actual.explanationKo),
            label + " lacks observer, reason or Korean explanation.");

        GameplayOutcomeEvidenceBindingSnapshot clone = fact.Clone();
        GameplayExperienceAssessmentSnapshot cloned = ObserveUnchanged(
            clone, label + " clone", verification);
        verification.Require(
            GameplayExperienceImportancePolicy.Equivalent(actual, cloned),
            label + " changed when evaluated through BindingSnapshot.Clone().");

        GameplayOutcomeEvidenceBindingSnapshot storageSignals = fact.Clone();
        storageSignals.subjectSalience = fact.subjectSalience.Equals(-999f) ? -998f : -999f;
        storageSignals.anchorRevision = Different(fact.anchorRevision, 700001);
        storageSignals.influenceUseCount = Different(fact.influenceUseCount, 700002);
        storageSignals.influenceRevision = Different(fact.influenceRevision, 700003);
        GameplayExperienceAssessmentSnapshot independent = ObserveUnchanged(
            storageSignals, label + " storage-independent", verification);
        verification.Require(
            GameplayExperienceImportancePolicy.Equivalent(actual, independent),
            label + " changed with raw salience, anchor or influence storage signals.");

        GameplayOutcomeEvidenceBindingSnapshot invalidStatus = fact.Clone();
        invalidStatus.status = 987;
        RequireUnassessed(
            ObserveUnchanged(invalidStatus, label + " invalid-status", verification),
            label + " accepted an undefined status.",
            verification);

        GameplayOutcomeEvidenceBindingSnapshot compacted = fact.Clone();
        compacted.sourceKind = "compacted";
        compacted.outcomeRunId = string.Empty;
        compacted.outcomeSequence = 0L;
        compacted.compactedMemoryId = "memory:all-experience-policy";
        RequireUnassessed(
            ObserveUnchanged(compacted, label + " compacted", verification),
            label + " assessed compacted evidence as an exact event.",
            verification);

        GameplayOutcomeEvidenceBindingSnapshot frozen = fact.Clone();
        frozen.experience = actual.Clone();
        string serialized = JsonConvert.SerializeObject(frozen, Formatting.None);
        GameplayOutcomeEvidenceBindingSnapshot restored =
            JsonConvert.DeserializeObject<GameplayOutcomeEvidenceBindingSnapshot>(serialized);
        verification.Require(restored != null, label + " JSON round trip returned null.");
        verification.Require(
            JToken.DeepEquals(JToken.Parse(serialized), JToken.Parse(
                JsonConvert.SerializeObject(restored, Formatting.None))),
            label + " binding changed across the Newtonsoft.Json round trip.");
        verification.Require(
            restored.experience != null
            && GameplayExperienceImportancePolicy.Equivalent(frozen.experience, restored.experience),
            label + " frozen experience changed across the JSON round trip.");
        verification.Require(
            !ReferenceEquals(frozen, restored)
            && !ReferenceEquals(frozen.experience, restored.experience),
            label + " JSON round trip retained a mutable alias.");

        return new CaseResult(
            fact.outcomeTypeId,
            policyCase.Name,
            policyCase.Expected,
            actual.importance,
            actual.reasonCode,
            actual.explanationKo);
    }

    private static GameplayExperienceAssessmentSnapshot ObserveUnchanged(
        GameplayOutcomeEvidenceBindingSnapshot fact,
        string label,
        Verification verification)
    {
        string before = JsonConvert.SerializeObject(fact, Formatting.None);
        GameplayExperienceAssessmentSnapshot result =
            GameplayExperienceObserverRegistration.Default.Observe(fact);
        string after = JsonConvert.SerializeObject(fact, Formatting.None);
        verification.Require(before == after, label + " observation mutated the source binding.");
        verification.Require(result != null, label + " returned no explicit assessment.");
        return result;
    }

    private static void RequireUnassessed(
        GameplayExperienceAssessmentSnapshot value,
        string message,
        Verification verification) => verification.Require(
        value.category == (int)GameplayExperienceCategory.Unassessed
        && value.importance.Equals(GameplayExperienceImportancePolicy.Importance(
            GameplayExperienceCategory.Unassessed)),
        message);

    private static string[] NormalizeRegisteredTypes(
        IReadOnlyList<string> registeredTypes,
        Verification verification)
    {
        verification.Require(registeredTypes != null, "Actual registry types are missing.");
        verification.Require(
            registeredTypes.All(value => !string.IsNullOrWhiteSpace(value)),
            "Actual registry contains an invalid outcome type.");
        verification.Require(
            registeredTypes.Distinct(StringComparer.Ordinal).Count() == registeredTypes.Count,
            "Actual registry contains duplicate outcome types.");
        return registeredTypes.OrderBy(value => value, StringComparer.Ordinal).ToArray();
    }

    private static void AddCases(
        ICollection<GameplayExperiencePolicyCase> target,
        IEnumerable<GameplayExperiencePolicyCase> source,
        string family)
    {
        if (source == null)
            throw new InvalidOperationException("The " + family + " experience case family is missing.");
        foreach (GameplayExperiencePolicyCase value in source)
            target.Add(value);
    }

    private static string DescribeSetMismatch(
        IReadOnlyList<string> registered,
        IReadOnlyList<string> supported)
    {
        string missing = string.Join(", ", registered.Except(supported, StringComparer.Ordinal));
        string unexpected = string.Join(", ", supported.Except(registered, StringComparer.Ordinal));
        return "Default experience observer coverage differs from the actual registry. missing=["
            + missing + "] unexpected=[" + unexpected + "]";
    }

    private static int Different(int original, int candidate) =>
        original == candidate ? candidate + 1 : candidate;

    private static string OneLine(string value) => (value ?? string.Empty)
        .Replace('\r', ' ').Replace('\n', ' ').Trim();

    private sealed class Verification
    {
        internal int Count { get; private set; }

        internal void Require(bool condition, string message)
        {
            if (!condition)
                throw new InvalidOperationException(message);
            Count++;
        }
    }

    private sealed class CaseResult
    {
        internal CaseResult(
            string outcomeType,
            string name,
            GameplayExperienceCategory category,
            float importance,
            string reason,
            string explanationKo)
        {
            OutcomeType = outcomeType;
            Name = name;
            Category = category;
            Importance = importance;
            Reason = reason;
            ExplanationKo = explanationKo;
        }

        internal string OutcomeType { get; }
        internal string Name { get; }
        internal GameplayExperienceCategory Category { get; }
        internal float Importance { get; }
        internal string Reason { get; }
        internal string ExplanationKo { get; }
    }
}
