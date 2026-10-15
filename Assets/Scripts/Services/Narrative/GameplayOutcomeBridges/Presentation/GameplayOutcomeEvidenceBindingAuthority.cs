using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

/// <summary>
/// Fail-closed validation for primitive persisted exact-evidence bindings.
/// Clone and JSON restore deliberately preserve raw values; only this authority
/// may admit them back into a ledger query or a mechanic transaction.
/// </summary>
public static class GameplayOutcomeEvidenceBindingAuthority
{
    public const int CurrentSchemaVersion = 4;

    public static bool TryValidate(
        GameplayOutcomeEvidenceBindingSnapshot binding,
        out string failureCode)
    {
        failureCode = string.Empty;
        if (binding == null)
            return Fail("binding-null", out failureCode);
        int version = binding.schemaVersion <= 1 ? 1 : binding.schemaVersion;
        if (version > CurrentSchemaVersion)
            return Fail("binding-schema-unsupported", out failureCode);
        string sourceKind = EffectiveSourceKind(binding);
        bool exact = string.Equals(sourceKind, "exact", StringComparison.Ordinal);
        bool compacted = string.Equals(sourceKind, "compacted", StringComparison.Ordinal);
        if (!exact && !compacted)
            return Fail("binding-source-kind-invalid", out failureCode);
        if (!GameplayOutcomeStableIdSyntax.IsValid(binding.publicFactId)
            || !GameplayOutcomeStableIdSyntax.IsValid(binding.outcomeTypeId)
            || !GameplayOutcomeStableIdSyntax.IsValid(binding.subjectKindId)
            || !GameplayOutcomeStableIdSyntax.IsValid(binding.subjectId))
            return Fail("binding-identity-invalid", out failureCode);
        if (exact && (!GameplayOutcomeStableIdSyntax.IsValid(binding.outcomeRunId)
                || binding.outcomeSequence <= 0L
                || !string.IsNullOrEmpty(binding.compactedMemoryId))
            || compacted && (!GameplayOutcomeStableIdSyntax.IsValid(
                    binding.compactedMemoryId)
                || !string.IsNullOrEmpty(binding.outcomeRunId)
                || binding.outcomeSequence != 0L))
            return Fail("binding-source-identity-invalid", out failureCode);
        if (binding.anchorRevision < 0
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
        if (version >= 2 && !TryValidateTyped(binding, out failureCode))
            return false;
        if (version >= 3 && !GameplayExperienceImportancePolicy.Equivalent(binding.experience,
                GameplayExperienceObserverRegistration.Default.Observe(binding)))
            return Fail("binding-experience-assessment-invalid", out failureCode);
        return true;
    }

    public static string EffectiveSourceKind(
        GameplayOutcomeEvidenceBindingSnapshot binding) =>
        string.IsNullOrWhiteSpace(binding?.sourceKind)
            ? "exact"
            : binding.sourceKind.Trim();

    public static GameplayOutcomeStatus RequireStatus(
        GameplayOutcomeEvidenceBindingSnapshot binding)
    {
        if (!TryValidate(binding, out string failureCode))
            throw new InvalidOperationException(
                "Exact gameplay-outcome evidence binding was rejected: "
                + failureCode);
        return (GameplayOutcomeStatus)binding.status;
    }

    public static string ComputeCanonicalHash(
        GameplayOutcomeEvidenceBindingSnapshot binding)
    {
        if (!TryValidate(binding, out string failureCode))
            throw new InvalidOperationException(
                "Cannot hash invalid gameplay-outcome evidence: " + failureCode);
        StringBuilder value = new(2048);
        Append(value, (binding.schemaVersion <= 1 ? 1 : binding.schemaVersion)
            .ToString(CultureInfo.InvariantCulture));
        Append(value, EffectiveSourceKind(binding));
        Append(value, binding.publicFactId);
        Append(value, binding.outcomeRunId);
        Append(value, binding.outcomeSequence.ToString(CultureInfo.InvariantCulture));
        Append(value, binding.compactedMemoryId);
        Append(value, binding.outcomeTypeId);
        Append(value, binding.subjectKindId);
        Append(value, binding.subjectId);
        Append(value, binding.perspectiveKind.ToString(CultureInfo.InvariantCulture));
        Append(value, binding.firstDay.ToString(CultureInfo.InvariantCulture));
        Append(value, binding.lastDay.ToString(CultureInfo.InvariantCulture));
        Append(value, binding.occurrenceCount.ToString(CultureInfo.InvariantCulture));
        Append(value, binding.anchorRevision.ToString(CultureInfo.InvariantCulture));
        Append(value, binding.status.ToString(CultureInfo.InvariantCulture));
        Append(value, BitConverter.SingleToInt32Bits(binding.subjectSalience)
            .ToString(CultureInfo.InvariantCulture));
        Append(value, binding.influenceUseCount.ToString(CultureInfo.InvariantCulture));
        Append(value, binding.influenceRevision.ToString(CultureInfo.InvariantCulture));
        Append(value, binding.canonicalFactText);
        AppendMany(value, binding.roleIds);
        AppendMany(value, binding.metricIds);
        AppendMany(value, binding.metricReferenceIds);
        AppendMany(value, binding.factIds);
        AppendMany(value, binding.semanticTags);
        AppendMany(value, binding.participants, item => item == null ? "<null>" :
            string.Join("\u001f", item.entityKindId, item.entityId, item.roleId,
                item.participationKind, item.hasPerceptionEvidence,
                item.displayText, item.displayRevision, item.locale,
                item.pronunciationMode, item.pronunciationValue,
                item.finalConsonant, item.pronunciationRevision));
        AppendMany(value, binding.metrics, item => item == null ? "<null>" :
            string.Join("\u001f", item.metricId, item.unitId,
                item.referenceKindId, item.referenceId,
                BitConverter.DoubleToInt64Bits(item.value),
                BitConverter.DoubleToInt64Bits(item.minimum),
                BitConverter.DoubleToInt64Bits(item.maximum), item.sampleCount));
        AppendMany(value, binding.facts, item => item == null ? "<null>" :
            item.factId + "\u001f" + item.value);
        AppendMany(value, binding.provenance, item => item == null ? "<null>" :
            item.kindId + "\u001f" + item.value);
        Append(value, binding.locationId);
        Append(value, binding.roomId);
        Append(value, binding.locationX.ToString(CultureInfo.InvariantCulture));
        Append(value, binding.locationY.ToString(CultureInfo.InvariantCulture));
        Append(value, binding.operationId);
        Append(value, binding.parentOutcomeId);
        Append(value, binding.rootOperationId);
        Append(value, binding.causationRelationId);
        if (binding.schemaVersion >= 3)
        {
            Append(value, binding.experience.policyVersion.ToString(CultureInfo.InvariantCulture));
            Append(value, binding.experience.observerId);
            Append(value, binding.experience.category.ToString(CultureInfo.InvariantCulture));
            Append(value, BitConverter.SingleToInt32Bits(binding.experience.importance)
                .ToString(CultureInfo.InvariantCulture));
            Append(value, binding.experience.reasonCode);
            AppendMany(value, binding.experience.evidenceMetricIds);
            Append(value, binding.experience.explanationKo);
            AppendMany(value, binding.experience.evidenceFactIds);
            AppendMany(value, binding.experience.evidenceRoleIds);
        }
        return NarrativeInferenceHash.ComputeSha256Utf8(value.ToString());
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

    private static bool TryValidateTyped(
        GameplayOutcomeEvidenceBindingSnapshot binding,
        out string failureCode)
    {
        if (!Enum.IsDefined(typeof(NarrativePerspectiveKind), binding.perspectiveKind)
            || binding.firstDay < 0
            || binding.lastDay < binding.firstDay
            || binding.occurrenceCount <= 0)
            return Fail("binding-temporal-perspective-invalid", out failureCode);
        if (binding.participants == null
            || binding.metrics == null
            || binding.facts == null
            || binding.provenance == null)
            return Fail("binding-typed-collections-null", out failureCode);

        HashSet<string> participantKeys = new(StringComparer.Ordinal);
        for (int index = 0; index < binding.participants.Count; index++)
        {
            GameplayOutcomeEvidenceParticipantBindingSnapshot value =
                binding.participants[index];
            if (value == null
                || !GameplayOutcomeStableIdSyntax.IsValid(value.entityKindId)
                || !GameplayOutcomeStableIdSyntax.IsValid(value.entityId)
                || !GameplayOutcomeStableIdSyntax.IsValid(value.roleId)
                || !Enum.IsDefined(
                    typeof(GameplayParticipationKind), value.participationKind)
                || !participantKeys.Add(
                    value.roleId + "\n" + value.entityKindId + "\n" + value.entityId))
                return Fail("binding-participant-invalid", out failureCode);
        }

        HashSet<string> metricKeys = new(StringComparer.Ordinal);
        for (int index = 0; index < binding.metrics.Count; index++)
        {
            GameplayOutcomeEvidenceMetricBindingSnapshot value = binding.metrics[index];
            bool hasReferenceKind = !string.IsNullOrEmpty(value?.referenceKindId);
            bool hasReferenceId = !string.IsNullOrEmpty(value?.referenceId);
            if (value == null
                || !GameplayOutcomeStableIdSyntax.IsValid(value.metricId)
                || !GameplayOutcomeStableIdSyntax.IsValid(value.unitId)
                || hasReferenceKind != hasReferenceId
                || hasReferenceKind
                    && (!GameplayOutcomeStableIdSyntax.IsValid(value.referenceKindId)
                        || !GameplayOutcomeStableIdSyntax.IsValid(value.referenceId))
                || !Finite(value.value)
                || !Finite(value.minimum)
                || !Finite(value.maximum)
                || value.minimum > value.maximum
                || value.sampleCount <= 0
                || !metricKeys.Add(value.metricId + "\n"
                    + value.referenceKindId + "\n" + value.referenceId))
                return Fail("binding-metric-invalid", out failureCode);
        }

        if (!SameIds(binding.roleIds, binding.participants, value => value.roleId)
            || !SameIds(binding.metricIds, binding.metrics, value => value.metricId)
            || !SameIds(binding.factIds, binding.facts, value => value.factId))
            return Fail("binding-flattened-typed-mismatch", out failureCode);

        HashSet<string> factKeys = new(StringComparer.Ordinal);
        for (int index = 0; index < binding.facts.Count; index++)
        {
            GameplayOutcomeEvidenceFactBindingSnapshot value = binding.facts[index];
            if (value == null
                || !GameplayOutcomeStableIdSyntax.IsValid(value.factId)
                || value.value == null
                || !factKeys.Add(value.factId))
                return Fail("binding-fact-invalid", out failureCode);
        }

        HashSet<string> provenanceKeys = new(StringComparer.Ordinal);
        for (int index = 0; index < binding.provenance.Count; index++)
        {
            GameplayOutcomeEvidenceProvenanceBindingSnapshot value =
                binding.provenance[index];
            if (value == null
                || !GameplayOutcomeStableIdSyntax.IsValid(value.kindId)
                || string.IsNullOrWhiteSpace(value.value)
                || !provenanceKeys.Add(value.kindId + "\n" + value.value))
                return Fail("binding-provenance-invalid", out failureCode);
        }

        if (!OptionalStableId(binding.locationId)
            || !OptionalStableId(binding.roomId)
            || !OptionalStableId(binding.operationId)
            || !OptionalStableId(binding.parentOutcomeId)
            || !OptionalStableId(binding.rootOperationId)
            || !OptionalStableId(binding.causationRelationId))
            return Fail("binding-context-invalid", out failureCode);

        failureCode = string.Empty;
        return true;
    }

    private static bool SameIds<T>(
        IReadOnlyList<string> flattened,
        IEnumerable<T> typed,
        Func<T, string> selector) =>
        (flattened ?? Array.Empty<string>())
            .OrderBy(value => value, StringComparer.Ordinal)
            .SequenceEqual(
                (typed ?? Array.Empty<T>())
                    .Where(value => value is not null)
                    .Select(selector)
                    .Distinct(StringComparer.Ordinal)
                    .OrderBy(value => value, StringComparer.Ordinal),
                StringComparer.Ordinal);

    private static bool Finite(double value) =>
        !double.IsNaN(value) && !double.IsInfinity(value);

    private static bool OptionalStableId(string value) =>
        string.IsNullOrEmpty(value) || GameplayOutcomeStableIdSyntax.IsValid(value);

    private static void Append(StringBuilder builder, object raw)
    {
        string value = raw?.ToString() ?? string.Empty;
        builder.Append(value.Length.ToString(CultureInfo.InvariantCulture))
            .Append(':').Append(value).Append('\n');
    }

    private static void AppendMany(
        StringBuilder builder,
        IEnumerable<string> values)
    {
        string[] canonical = (values ?? Array.Empty<string>())
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        Append(builder, canonical.Length);
        foreach (string value in canonical)
            Append(builder, value);
    }

    private static void AppendMany<T>(
        StringBuilder builder,
        IEnumerable<T> values,
        Func<T, string> projector)
    {
        string[] canonical = (values ?? Array.Empty<T>())
            .Select(projector)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        Append(builder, canonical.Length);
        foreach (string value in canonical)
            Append(builder, value);
    }

    private static bool Fail(string code, out string failureCode)
    {
        failureCode = code;
        return false;
    }
}
