using System;
using System.Collections.Generic;

/// <summary>
/// Fail-closed validation for primitive persisted exact-evidence bindings.
/// Clone and JSON restore deliberately preserve raw values; only this authority
/// may admit them back into a ledger query or a mechanic transaction.
/// </summary>
public static class GameplayOutcomeEvidenceBindingAuthority
{
    public static bool TryValidate(
        GameplayOutcomeEvidenceBindingSnapshot binding,
        out string failureCode)
    {
        failureCode = string.Empty;
        if (binding == null)
            return Fail("binding-null", out failureCode);
        if (!GameplayOutcomeStableIdSyntax.IsValid(binding.publicFactId)
            || !GameplayOutcomeStableIdSyntax.IsValid(binding.outcomeRunId)
            || !GameplayOutcomeStableIdSyntax.IsValid(binding.outcomeTypeId)
            || !GameplayOutcomeStableIdSyntax.IsValid(binding.subjectKindId)
            || !GameplayOutcomeStableIdSyntax.IsValid(binding.subjectId))
            return Fail("binding-identity-invalid", out failureCode);
        if (binding.outcomeSequence <= 0L
            || binding.anchorRevision < 0
            || binding.influenceUseCount < 0
            || binding.influenceRevision < 0)
            return Fail("binding-revision-invalid", out failureCode);
        if (float.IsNaN(binding.subjectSalience)
            || float.IsInfinity(binding.subjectSalience)
            || binding.subjectSalience < 0f)
            return Fail("binding-salience-invalid", out failureCode);
        if (!Enum.IsDefined(typeof(GameplayOutcomeStatus), binding.status))
            return Fail("binding-status-invalid", out failureCode);
        if (string.IsNullOrWhiteSpace(binding.canonicalFactText))
            return Fail("binding-fact-text-empty", out failureCode);
        if (!ValidStableSet(binding.roleIds, requireAny: true)
            || !ValidStableSet(binding.metricIds, requireAny: false)
            || !ValidReferenceSet(binding.metricReferenceIds)
            || !ValidStableSet(binding.factIds, requireAny: false)
            || !ValidStableSet(binding.semanticTags, requireAny: false))
            return Fail("binding-descriptor-shape-invalid", out failureCode);
        return true;
    }

    public static GameplayOutcomeStatus RequireStatus(
        GameplayOutcomeEvidenceBindingSnapshot binding)
    {
        if (!TryValidate(binding, out string failureCode))
            throw new InvalidOperationException(
                "Exact gameplay-outcome evidence binding was rejected: "
                + failureCode);
        return (GameplayOutcomeStatus)binding.status;
    }

    private static bool ValidStableSet(
        IReadOnlyList<string> values,
        bool requireAny)
    {
        if (values == null || requireAny && values.Count == 0)
            return false;
        HashSet<string> distinct = new(StringComparer.Ordinal);
        for (int index = 0; index < values.Count; index++)
        {
            string value = values[index];
            if (!GameplayOutcomeStableIdSyntax.IsValid(value)
                || !distinct.Add(value))
                return false;
        }
        return true;
    }

    private static bool ValidReferenceSet(IReadOnlyList<string> values)
    {
        if (values == null)
            return false;
        HashSet<string> distinct = new(StringComparer.Ordinal);
        for (int index = 0; index < values.Count; index++)
        {
            string value = values[index];
            int separator = value?.IndexOf(':') ?? -1;
            if (separator <= 0
                || separator >= value.Length - 1
                || !GameplayOutcomeStableIdSyntax.IsValid(
                    value.Substring(0, separator))
                || !GameplayOutcomeStableIdSyntax.IsValid(
                    value.Substring(separator + 1))
                || !distinct.Add(value))
                return false;
        }
        return true;
    }

    private static bool Fail(string code, out string failureCode)
    {
        failureCode = code;
        return false;
    }
}
