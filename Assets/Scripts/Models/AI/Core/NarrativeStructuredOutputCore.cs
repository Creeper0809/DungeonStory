using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

public enum NarrativeQualityVerdict
{
    StrongPass,
    SoftPass,
    HardReject
}

[Serializable]
public sealed class NarrativeGenerationTrace
{
    public string schemaId = string.Empty;
    public int schemaVersion;
    public string schemaHash = string.Empty;
    public string cultureStyleId = string.Empty;
    public string[] usedMotifIds = Array.Empty<string>();
    public string[] usedCharacterFactIds = Array.Empty<string>();
    public NarrativeQualityVerdict verdict;
    public int retryCount;
    public bool usedFallback;
}

public readonly struct NarrativeQualityResult
{
    public NarrativeQualityResult(
        NarrativeQualityVerdict verdict,
        string error,
        string[] motifIds,
        string[] characterFactIds)
    {
        Verdict = verdict;
        Error = error ?? string.Empty;
        MotifIds = motifIds ?? Array.Empty<string>();
        CharacterFactIds = characterFactIds ?? Array.Empty<string>();
    }

    public NarrativeQualityVerdict Verdict { get; }
    public string Error { get; }
    public string[] MotifIds { get; }
    public string[] CharacterFactIds { get; }
    public bool IsAccepted => Verdict != NarrativeQualityVerdict.HardReject;
}

public sealed class NarrativeContextEntry
{
    public NarrativeContextEntry(string stableId, string label, int priority)
    {
        StableId = (stableId ?? string.Empty).Trim().Replace("|", ":");
        Label = (label ?? string.Empty).Trim()
            .Replace('\r', ' ').Replace('\n', ' ').Replace('|', '/');
        Priority = priority;
    }

    public string StableId { get; }
    public string Label { get; }
    public int Priority { get; }
    public string Reference { get; internal set; } = string.Empty;
}

public sealed class NarrativeRequestContext
{
    public const int MaximumFacts = 24;
    public const int MaximumMotifs = 12;
    public const string BeginMarker = "[[V24-NARRATIVE-CONTEXT";
    public const string EndMarker = "[[/V24-NARRATIVE-CONTEXT]]";

    private readonly List<NarrativeContextEntry> facts = new();
    private readonly List<NarrativeContextEntry> motifs = new();
    private readonly IReadOnlyList<NarrativeContextEntry> factsView;
    private readonly IReadOnlyList<NarrativeContextEntry> motifsView;
    private bool frozen;

    public NarrativeRequestContext(
        string profileId,
        string cultureStyleId,
        bool requireCharacterFact,
        bool requireMotif)
    {
        ProfileId = (profileId ?? string.Empty).Trim();
        CultureStyleId = (cultureStyleId ?? string.Empty).Trim();
        RequireCharacterFact = requireCharacterFact;
        RequireMotif = requireMotif;
        factsView = facts.AsReadOnly();
        motifsView = motifs.AsReadOnly();
    }

    public string ProfileId { get; }
    public string CultureStyleId { get; }
    public bool RequireCharacterFact { get; }
    public bool RequireMotif { get; }
    public IReadOnlyList<NarrativeContextEntry> Facts => factsView;
    public IReadOnlyList<NarrativeContextEntry> Motifs => motifsView;
    public bool IsFrozen => frozen;

    public void AddFact(string stableId, string label, int priority = 0)
    {
        RequireMutable();
        AddUnique(facts, stableId, label, priority);
    }

    public void AddMotif(string stableId, string label, int priority = 0)
    {
        RequireMutable();
        AddUnique(motifs, stableId, label, priority);
    }

    public void Freeze()
    {
        if (frozen) return;
        AssignReferences(facts, "F", MaximumFacts);
        AssignReferences(motifs, "M", MaximumMotifs);
        frozen = true;
    }

    public string AppendToPrompt(string prompt)
    {
        if ((prompt ?? string.Empty).Contains(BeginMarker))
        {
            return prompt;
        }

        if (!frozen)
        {
            AssignReferences(facts, "F", MaximumFacts);
            AssignReferences(motifs, "M", MaximumMotifs);
        }
        StringBuilder builder = new StringBuilder((prompt?.Length ?? 0) + 2048);
        builder.AppendLine(prompt ?? string.Empty);
        builder.AppendLine();
        builder.Append(BeginMarker)
            .Append(" profile=").Append(ProfileId)
            .Append(" requireFact=").Append(RequireCharacterFact ? '1' : '0')
            .Append(" requireMotif=").Append(RequireMotif ? '1' : '0')
            .Append(" culture=").Append(CultureStyleId)
            .AppendLine("]]" );
        builder.AppendLine(
            "Use only the request-local Fxx and Mxx references below. " +
            "Return only references concretely reflected in the Korean prose. " +
            "Copy exact tokens such as F01 and M01 into usedCharacterFactIds and usedMotifIds. " +
            "Never replace those tokens with labels or internal ids. " +
            "Never invent a person, event, relationship, trait, or reference.");
        foreach (NarrativeContextEntry fact in facts)
        {
            builder.Append(fact.Reference).Append('|').Append(fact.StableId)
                .Append('|').AppendLine(fact.Label);
        }
        foreach (NarrativeContextEntry motif in motifs)
        {
            builder.Append(motif.Reference).Append('|').Append(motif.StableId)
                .Append('|').AppendLine(motif.Label);
        }
        builder.AppendLine(EndMarker);
        return builder.ToString();
    }

    public static string ToModelPrompt(string prompt)
    {
        bool hasPublicModelInput = NarrativePublicModelInput.ContainsMarker(prompt);
        NarrativePublicModelInput publicModelInput = null;
        if (hasPublicModelInput
            && !NarrativePublicModelInput.TryParsePrompt(
                prompt,
                out publicModelInput))
        {
            throw new InvalidOperationException(
                "Narrative prompt contains an invalid public model-input payload.");
        }
        if (!TryParse(prompt, out NarrativeRequestContext context))
        {
            return prompt ?? string.Empty;
        }
        if (publicModelInput != null && !publicModelInput.MatchesRequestContext(context))
        {
            throw new InvalidOperationException(
                "Narrative request references do not match the public model-input payload.");
        }

        int start = prompt.IndexOf(BeginMarker, StringComparison.Ordinal);
        int end = prompt.IndexOf(EndMarker, StringComparison.Ordinal);
        int suffixStart = end + EndMarker.Length;
        StringBuilder builder = new StringBuilder((prompt?.Length ?? 0) + 512);
        builder.Append(prompt, 0, start);
        builder.AppendLine("Available character facts (request-local references):");
        foreach (NarrativeContextEntry fact in context.Facts)
        {
            builder.Append(fact.Reference).Append(" = ").AppendLine(fact.Label);
        }
        builder.AppendLine("Available culture motifs (request-local references):");
        foreach (NarrativeContextEntry motif in context.Motifs)
        {
            builder.Append(motif.Reference).Append(" = ").AppendLine(motif.Label);
        }
        builder.AppendLine(StyleInstruction(context.ProfileId));
        if (NarrativeExactKeyContract.AllowsNarrativeReferenceKeys(context.ProfileId))
        {
            builder.Append(
                "Use only the exact Fxx/Mxx tokens listed above in usedCharacterFactIds and usedMotifIds. ");
            if (context.RequireCharacterFact)
            {
                builder.Append("At least one usedCharacterFactIds token is required. ");
            }
            if (context.RequireMotif)
            {
                builder.Append("At least one usedMotifIds token is required. ");
            }
            builder.AppendLine(
                "Do not output labels as reference values and do not invent people, events, relationships, traits, or facts.");
        }
        else
        {
            builder.AppendLine(
                "Use the supplied facts and motifs only to ground prose. " +
                "Do not emit reference-tracking keys or any key outside the response schema, " +
                "and do not invent people, events, relationships, traits, or facts.");
        }
        if (suffixStart < prompt.Length)
        {
            builder.Append(prompt, suffixStart, prompt.Length - suffixStart);
        }
        string modelPrompt = builder.ToString();
        if (hasPublicModelInput
            && !publicModelInput.IsPreservedInPrompt(modelPrompt))
        {
            throw new InvalidOperationException(
                "Narrative model prompt did not preserve its public model-input payload.");
        }
        return modelPrompt;
    }

    /// <summary>
    /// Builds the same validated request-local fact view as <see cref="ToModelPrompt"/>,
    /// then removes the redundant canonical public JSON from the transport prompt.
    /// The caller must retain the original prompt for audit and semantic identity.
    /// </summary>
    internal static string ToModelPromptWithoutCanonicalPublicPayload(string prompt)
    {
        string modelPrompt = ToModelPrompt(prompt);
        int begin = modelPrompt.IndexOf(
            NarrativePublicModelInput.BeginMarker,
            StringComparison.Ordinal);
        if (begin < 0)
        {
            return modelPrompt;
        }
        int end = modelPrompt.IndexOf(
            NarrativePublicModelInput.EndMarker,
            begin,
            StringComparison.Ordinal);
        if (end < begin)
        {
            throw new InvalidOperationException(
                "Validated narrative prompt lost its canonical public-input end marker.");
        }

        const string header = "Public narrative context (canonical public JSON):";
        int removeStart = modelPrompt.LastIndexOf(
            header,
            begin,
            StringComparison.Ordinal);
        if (removeStart < 0)
        {
            removeStart = begin;
        }
        else if (removeStart > 0 && modelPrompt[removeStart - 1] == '\n')
        {
            removeStart--;
        }
        int removeEnd = end + NarrativePublicModelInput.EndMarker.Length;
        while (removeEnd < modelPrompt.Length
               && (modelPrompt[removeEnd] == '\r' || modelPrompt[removeEnd] == '\n'))
        {
            removeEnd++;
        }
        return modelPrompt.Remove(removeStart, removeEnd - removeStart);
    }

    // Transport-only encoding: original prompt/canonical payload remain the audit
    // and semantic authority. No fact, participant, date, count, or ID is omitted.
    internal static string ToCompactPublicModelPrompt(string prompt)
        => BuildCompactPublicModelPrompt(prompt, compactRepeatedText: false);

    internal static string ToCompactEvolutionModelPrompt(string prompt)
        => BuildCompactPublicModelPrompt(prompt, compactRepeatedText: true);

    internal static string ToCompactTraitModelPrompt(string prompt)
    {
        string compact = CompactTraitDomainEvidenceGroups(ToCompactPublicModelPrompt(prompt));
        JObject references = TraitEvidenceReferences(prompt);
        if (references == null) return compact;
        if (compact.Contains(TraitEvidenceRefPrefix) || compact.Contains(TraitEvidenceRefBegin)
            || compact.Contains(TraitEvidenceRefEnd))
            throw new InvalidOperationException("Trait prompt contains reserved evidence-reference transport fields.");
        string projected = compact;
        foreach (JProperty reference in references.Properties())
            projected = projected.Replace((string)reference.Value, reference.Name);
        var table = new JObject
        {
            ["requestRef"] = CharacterSkillCompactModelPrompt.RequestRef(prompt),
            ["evidence"] = references
        };
        string encoded = projected + TraitEvidenceRefEnvelopePrefix
            + table.ToString(Formatting.None) + "\n" + TraitEvidenceRefEnd;
        if (!string.Equals(ExpandTraitEvidenceReferences(encoded), compact, StringComparison.Ordinal))
            throw new InvalidOperationException("Trait evidence references lost exact model input.");
        return encoded;
    }

    private const string TraitEvidenceRefPrefix = "[[trait-e:";
    private const string TraitEvidenceRefBegin = "[[trait-evidence-refs-v1]]";
    private const string TraitEvidenceRefEnd = "[[/trait-evidence-refs-v1]]";
    private const string TraitEvidenceRefEnvelopePrefix =
        "\nReturn requestRef exactly. For this wire, earlier full-ID wording means decoded IDs: evidenceFactIds must emit the offered [[trait-e:n]] references, not full IDs. C# alone resolves them against this immutable request; no module or prose rewriting.\n"
        + TraitEvidenceRefBegin + "\n";

    internal static JObject TraitEvidenceReferences(string prompt)
    {
        string original = prompt ?? string.Empty;
        int context = original.IndexOf(BeginMarker, StringComparison.Ordinal);
        string[] lines = (context < 0 ? original : original.Substring(0, context))
            .Split('\n').Select(value => value.Trim()).ToArray();
        const string gameplayKey = "gameplayEvidenceFactIds=";
        if (!lines.Any(value => value.StartsWith(gameplayKey, StringComparison.Ordinal))) return null;
        string Read(string key)
        {
            string[] matches = lines.Where(value => value.StartsWith(key, StringComparison.Ordinal)).ToArray();
            if (matches.Length != 1 || matches[0].Length == key.Length)
                throw new InvalidOperationException("Trait references require one nonempty " + key + " header.");
            return matches[0].Substring(key.Length);
        }
        string offered = Read("evidenceFactIds=");
        string gameplay = Read(gameplayKey);
        ValidateTraitDomainFactList(offered);
        ValidateTraitDomainFactList(gameplay);
        string[] ids = offered.Split(',');
        if (gameplay.Split(',').Any(value => !ids.Contains(value, StringComparer.Ordinal)))
            throw new InvalidOperationException("Trait gameplay references are outside the offered evidence.");
        var references = new JObject();
        for (int index = 0; index < ids.Length; index++)
            references.Add(TraitEvidenceRefPrefix + index.ToString(CultureInfo.InvariantCulture) + "]]", ids[index]);
        return references;
    }

    internal static string ExpandTraitEvidenceReferences(string encoded)
    {
        string value = encoded ?? string.Empty;
        int start = value.IndexOf(TraitEvidenceRefEnvelopePrefix, StringComparison.Ordinal);
        if (start < 0)
        {
            if (value.Contains(TraitEvidenceRefPrefix) || value.Contains(TraitEvidenceRefBegin)
                || value.Contains(TraitEvidenceRefEnd))
                throw new InvalidOperationException("Trait evidence references have no valid envelope.");
            return value;
        }
        string suffix = "\n" + TraitEvidenceRefEnd;
        if (!value.EndsWith(suffix, StringComparison.Ordinal)
            || value.IndexOf(TraitEvidenceRefEnvelopePrefix, start + 1, StringComparison.Ordinal) >= 0)
            throw new InvalidOperationException("Trait evidence reference envelope is malformed.");
        JObject table = JObject.Parse(value.Substring(start + TraitEvidenceRefEnvelopePrefix.Length,
            value.Length - start - TraitEvidenceRefEnvelopePrefix.Length - suffix.Length),
            new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
        if (table.Properties().Count() != 2 || table["requestRef"]?.Type != JTokenType.String
            || ((string)table["requestRef"]).Length != 64
            || ((string)table["requestRef"]).Any(character =>
                !(character >= '0' && character <= '9' || character >= 'a' && character <= 'f'))
            || table["evidence"] is not JObject references
            || references.Count == 0)
            throw new InvalidOperationException("Trait evidence reference table is invalid.");
        string restored = value.Substring(0, start);
        int index = 0;
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (JProperty reference in references.Properties())
        {
            string expected = TraitEvidenceRefPrefix + (index++).ToString(CultureInfo.InvariantCulture) + "]]";
            if (reference.Name != expected || reference.Value.Type != JTokenType.String
                || string.IsNullOrWhiteSpace((string)reference.Value)
                || !ids.Add((string)reference.Value) || !restored.Contains(reference.Name))
                throw new InvalidOperationException("Trait evidence reference mapping is noncanonical or unused.");
            restored = restored.Replace(reference.Name, (string)reference.Value);
        }
        if (restored.Contains(TraitEvidenceRefPrefix))
            throw new InvalidOperationException("Trait model input has an unknown evidence reference.");
        return restored;
    }

    internal static string ResolveTraitEvidenceReferences(string prompt, string content)
    {
        JObject references = TraitEvidenceReferences(prompt);
        if (references == null) return content;
        JObject response = JObject.Parse(content, new JsonLoadSettings
            { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
        string[] keys = { "displayName", "drawbackModuleIds", "evidenceFactIds", "narrativeFlavor",
            "positiveModuleIds", "requestRef", "selectionId" };
        if (!response.Properties().Select(value => value.Name).OrderBy(value => value, StringComparer.Ordinal)
                .SequenceEqual(keys, StringComparer.Ordinal)
            || response["requestRef"]?.Type != JTokenType.String
            || (string)response["requestRef"] != CharacterSkillCompactModelPrompt.RequestRef(prompt)
            || response["evidenceFactIds"] is not JArray evidence || evidence.Count == 0)
            throw new InvalidOperationException("Trait reference response keys, request or evidence differ.");
        var selected = new HashSet<string>(StringComparer.Ordinal);
        foreach (JToken value in evidence)
            if (value.Type != JTokenType.String || references[(string)value]?.Type != JTokenType.String
                || !selected.Add((string)value))
                throw new InvalidOperationException("Trait response evidence reference is unknown, noncanonical or duplicated.");
        for (int index = 0; index < evidence.Count; index++)
            evidence[index] = references[(string)evidence[index]].DeepClone();
        response.Remove("requestRef");
        string resolved = response.ToString(Formatting.None);
        if (!NarrativeExactKeyContract.TryValidateProfileResponse(
                LocalLlmRequestProfiles.AcquiredTraitModuleSelection.Id, resolved, out _, out string error))
            throw new InvalidOperationException("Resolved Trait exact-key reject: " + error);
        return resolved;
    }

    private const string TraitDomainGroupBegin = "[[trait-domain-evidence-groups-v1]]";
    private const string TraitDomainGroupEnd = "[[/trait-domain-evidence-groups-v1]]";
    private const string TraitDomainGroupField = "; domainEvidenceGroup=";
    private const string TraitDomainFactField = "; domainEvidenceFactIds=";
    private const string TraitDomainGroupInstruction =
        "domainEvidenceGroup refers to the full fact ID list below. Cite full fact IDs, never DG keys, in evidenceFactIds; also cite the required exact gameplay evidence.";

    internal static string CompactTraitDomainEvidenceGroups(string modelPrompt)
    {
        string original = modelPrompt ?? string.Empty;
        if (original.Contains(TraitDomainGroupBegin) || original.Contains(TraitDomainGroupEnd)
            || original.Contains(TraitDomainGroupField))
            throw new InvalidOperationException("Trait prompt already contains reserved domain-group transport fields.");
        var keysByList = new Dictionary<string, string>(StringComparer.Ordinal);
        JObject groups = new();
        string[] lines = original.Split('\n');
        for (int index = 0; index < lines.Length; index++)
        {
            string line = lines[index];
            if (!line.StartsWith("- moduleId=", StringComparison.Ordinal)) continue;
            int marker = line.LastIndexOf(TraitDomainFactField, StringComparison.Ordinal);
            if (marker < 0) continue;
            bool cr = line.EndsWith("\r", StringComparison.Ordinal);
            string list = line.Substring(marker + TraitDomainFactField.Length,
                line.Length - marker - TraitDomainFactField.Length - (cr ? 1 : 0));
            if (list.Length == 0) continue; // No requirement is represented by an empty original list.
            ValidateTraitDomainFactList(list);
            if (!keysByList.TryGetValue(list, out string key))
            {
                key = "DG" + (keysByList.Count + 1).ToString("D2", CultureInfo.InvariantCulture);
                keysByList.Add(list, key);
                groups.Add(key, list);
            }
            lines[index] = line.Substring(0, marker) + TraitDomainGroupField + key + (cr ? "\r" : string.Empty);
        }
        if (groups.Count == 0) return original;
        string encoded = string.Join("\n", lines) + "\n" + TraitDomainGroupInstruction
            + "\n" + TraitDomainGroupBegin + "\n" + groups.ToString(Formatting.None)
            + "\n" + TraitDomainGroupEnd;
        if (!string.Equals(original, ExpandTraitDomainEvidenceGroups(encoded), StringComparison.Ordinal))
            throw new InvalidOperationException("Trait domain groups did not reconstruct the exact model prompt.");
        return encoded;
    }

    internal static string ExpandTraitDomainEvidenceGroups(string encoded)
    {
        string value = encoded ?? string.Empty;
        if (!value.Contains(TraitDomainGroupBegin))
        {
            if (value.Contains(TraitDomainGroupEnd) || value.Contains(TraitDomainGroupField))
                throw new InvalidOperationException("Trait domain groups have no declaration.");
            return value;
        }
        string prefix = "\n" + TraitDomainGroupInstruction + "\n" + TraitDomainGroupBegin + "\n";
        string suffix = "\n" + TraitDomainGroupEnd;
        int start = value.IndexOf(prefix, StringComparison.Ordinal);
        if (start < 0 || !value.EndsWith(suffix, StringComparison.Ordinal)
            || value.IndexOf(prefix, start + prefix.Length, StringComparison.Ordinal) >= 0)
            throw new InvalidOperationException("Trait domain group envelope is malformed.");
        string json = value.Substring(start + prefix.Length,
            value.Length - suffix.Length - start - prefix.Length);
        JObject groups = JObject.Parse(json, new JsonLoadSettings
            { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
        if (groups.Count == 0 || groups.Properties().Any(property =>
                property.Name.Length < 4 || !property.Name.StartsWith("DG", StringComparison.Ordinal)
                || property.Name.Substring(2).Any(character => character < '0' || character > '9')
                || property.Value.Type != JTokenType.String))
            throw new InvalidOperationException("Trait domain group definitions are invalid.");
        foreach (JProperty property in groups.Properties()) ValidateTraitDomainFactList((string)property.Value);
        var used = new HashSet<string>(StringComparer.Ordinal);
        string[] lines = value.Substring(0, start).Split('\n');
        for (int index = 0; index < lines.Length; index++)
        {
            string line = lines[index];
            if (!line.StartsWith("- moduleId=", StringComparison.Ordinal)) continue;
            int marker = line.LastIndexOf(TraitDomainGroupField, StringComparison.Ordinal);
            if (marker < 0) continue;
            bool cr = line.EndsWith("\r", StringComparison.Ordinal);
            string key = line.Substring(marker + TraitDomainGroupField.Length,
                line.Length - marker - TraitDomainGroupField.Length - (cr ? 1 : 0));
            if (groups[key]?.Type != JTokenType.String)
                throw new InvalidOperationException("Trait module refers to an unknown domain group.");
            used.Add(key);
            lines[index] = line.Substring(0, marker) + TraitDomainFactField + (string)groups[key]
                + (cr ? "\r" : string.Empty);
        }
        if (used.Count != groups.Count)
            throw new InvalidOperationException("Trait domain group definitions are not all used.");
        return string.Join("\n", lines);
    }

    private static void ValidateTraitDomainFactList(string list)
    {
        string[] ids = (list ?? string.Empty).Split(',');
        if (ids.Any(id => string.IsNullOrWhiteSpace(id) || id != id.Trim())
            || ids.Distinct(StringComparer.Ordinal).Count() != ids.Length)
            throw new InvalidOperationException("Trait domain group fact IDs are empty, noncanonical or duplicated.");
    }

    private static string BuildCompactPublicModelPrompt(string prompt, bool compactRepeatedText)
    {
        string validated = ToModelPrompt(prompt);
        if (!NarrativePublicModelInput.ContainsMarker(prompt)) return validated;
        if (!TryParse(prompt, out NarrativeRequestContext context)
            || !NarrativePublicModelInput.TryParsePrompt(prompt, out NarrativePublicModelInput input))
            throw new InvalidOperationException("Compact transport requires validated public context.");

        JObject payload = JObject.Parse(input.CanonicalPayload);
        JArray publicFacts = (JArray)payload["publicFacts"];
        for (int index = 0; index < publicFacts.Count; index++)
        {
            string factId = (string)publicFacts[index]["factId"];
            // Full output IDs stay in each fact row. Only context links use
            // one-based row indices; no Fxx shorthand competes with output IDs.
            foreach (JValue value in PublicFactReferences(payload)
                         .Where(value => value.Type == JTokenType.String && (string)value == factId))
                value.Value = index + 1;
        }
        payload["publicFacts"] = CompactRows((JArray)payload["publicFacts"],
            "count", "domain", "factId", "lastDay", "milestoneCount",
            "outcome", "subjectId", "text", "totalValueDecimal");
        JObject publicContext = (JObject)payload["publicNarrativeContext"];
        publicContext["events"] = CompactRows((JArray)publicContext["events"],
            "actorId", "count", "day", "eventType", "evidenceId", "factId",
            "generation", "outcome", "targetId");
        if (compactRepeatedText)
            payload = CompactRepeatedPublicText(payload);

        int contextStart = prompt.IndexOf(BeginMarker, StringComparison.Ordinal);
        int contextEnd = prompt.IndexOf(EndMarker, StringComparison.Ordinal) + EndMarker.Length;
        int publicStart = prompt.IndexOf(NarrativePublicModelInput.BeginMarker, StringComparison.Ordinal);
        int publicEnd = prompt.IndexOf(NarrativePublicModelInput.EndMarker, StringComparison.Ordinal)
            + NarrativePublicModelInput.EndMarker.Length;
        if (contextStart < 0 || publicStart < contextEnd)
            throw new InvalidOperationException("Compact transport context ordering is invalid.");
        const string header = "Public narrative context (canonical public JSON):";
        int headerStart = prompt.LastIndexOf(header, publicStart, StringComparison.Ordinal);
        if (headerStart >= contextEnd) publicStart = headerStart;

        var builder = new StringBuilder(prompt.Length);
        builder.Append(prompt, 0, contextStart);
        builder.AppendLine("Public facts and events below use column/row tables. Null fact cells mean absent optional fields; event nulls are preserved.");
        builder.AppendLine(compactRepeatedText
            ? "Context fact links are one-based publicFacts row indices. Output evidenceFactIds must copy the exact IDs from the allowed evidenceFactIds list above, not row indices or other background facts."
            : "Context fact links are one-based publicFacts row indices. Output evidenceFactIds must copy exact public-fact:sha256: IDs from the allowed evidenceFactIds list above, not row indices or other background facts.");
        if (compactRepeatedText)
            builder.AppendLine("encoding=repeated-public-text-v1: data is the public context. Each {$text:[...]} concatenates literal strings and {$s:n} references to strings[n], in order. Tables have rowCount rows: columnValues aligns with columns; arrays contain one value per row, {$repeat:v} means v in every row. Exact original data, not extra evidence; output only response-schema keys.");
        builder.AppendLine(payload.ToString(Formatting.None));
        builder.AppendLine("Available culture motifs:");
        foreach (NarrativeContextEntry motif in context.Motifs)
            builder.Append(motif.Reference).Append(" = ").AppendLine(motif.Label);
        builder.AppendLine(StyleInstruction(context.ProfileId));
        builder.AppendLine("Ground prose only in supplied facts and motifs; emit only response-schema keys.");
        builder.Append(prompt, contextEnd, publicStart - contextEnd);
        builder.Append(prompt, publicEnd, prompt.Length - publicEnd);
        return builder.ToString();
    }

    // Share repeated literal prose, never summarize or remove an event. The
    // inverse is checked before sending, so identity/date/count remain exact.
    internal static JObject CompactRepeatedPublicText(JObject payload)
    {
        if (payload == null) throw new ArgumentNullException(nameof(payload));
        JObject data = (JObject)payload.DeepClone();
        // Share constant table columns without discarding any row or field.
        foreach (JObject table in data.Descendants().OfType<JObject>()
                     .Where(value => value["columns"] is JArray && value["rows"] is JArray).ToArray())
        {
            JArray columns = (JArray)table["columns"], rows = (JArray)table["rows"];
            if (rows.Count == 0) continue;
            if (rows.Any(row => row is not JArray cells || cells.Count != columns.Count))
                throw new InvalidOperationException("Invalid public table row width.");
            var columnValues = new JArray();
            for (int column = 0; column < columns.Count; column++)
            {
                JToken first = rows[0][column];
                columnValues.Add(rows.All(row => JToken.DeepEquals(row[column], first))
                    ? new JObject { ["$repeat"] = first.DeepClone() }
                    : new JArray(rows.Select(row => row[column].DeepClone())));
            }
            table.Property("rows").Remove();
            table["rowCount"] = rows.Count;
            table["columnValues"] = columnValues;
        }
        JValue[] values = data.Descendants().OfType<JValue>()
            .Where(value => value.Type == JTokenType.String
                && ((string)value).Length >= 48 && ((string)value).Any(char.IsWhiteSpace)).ToArray();
        string[] texts = values.Select(value => (string)value).Distinct(StringComparer.Ordinal).ToArray();
        var segments = new HashSet<string>(StringComparer.Ordinal);
        for (int first = 0; first < texts.Length; first++)
            for (int second = first + 1; second < texts.Length; second++)
            {
                string a = texts[first], b = texts[second];
                int prefix = 0, suffix = 0, limit = Math.Min(a.Length, b.Length);
                while (prefix < limit && a[prefix] == b[prefix]) prefix++;
                while (suffix < limit - prefix && a[a.Length - suffix - 1] == b[b.Length - suffix - 1]) suffix++;
                // Shared text parts must also be valid UTF-16 on their own.
                if (prefix > 0 && prefix < a.Length && char.IsHighSurrogate(a[prefix - 1])
                    && char.IsLowSurrogate(a[prefix])) prefix--;
                if (suffix > 0 && suffix < a.Length && char.IsLowSurrogate(a[a.Length - suffix])
                    && char.IsHighSurrogate(a[a.Length - suffix - 1])) suffix--;
                if (prefix >= 24) segments.Add(a.Substring(0, prefix));
                if (suffix >= 24) segments.Add(a.Substring(a.Length - suffix));
            }
        foreach (var group in values.GroupBy(value => (string)value, StringComparer.Ordinal).Where(group => group.Count() > 1))
            segments.Add(group.Key);
        var prefixes = segments.OrderByDescending(segment =>
                (values.Count(value => ((string)value).StartsWith(segment, StringComparison.Ordinal)) - 1) * segment.Length)
            .ThenByDescending(segment => segment.Length).ThenBy(segment => segment, StringComparer.Ordinal).ToArray();
        var suffixes = segments.OrderByDescending(segment =>
                (values.Count(value => ((string)value).EndsWith(segment, StringComparison.Ordinal)) - 1) * segment.Length)
            .ThenByDescending(segment => segment.Length).ThenBy(segment => segment, StringComparer.Ordinal).ToArray();
        var strings = new List<string>();
        JObject Reference(string text)
        {
            int index = strings.IndexOf(text);
            if (index < 0) { index = strings.Count; strings.Add(text); }
            return new JObject { ["$s"] = index };
        }
        foreach (JValue value in values)
        {
            string text = (string)value;
            string prefix = prefixes.FirstOrDefault(segment => text.StartsWith(segment, StringComparison.Ordinal)) ?? string.Empty;
            string suffix = suffixes.FirstOrDefault(segment => segment.Length <= text.Length - prefix.Length
                && text.EndsWith(segment, StringComparison.Ordinal)) ?? string.Empty;
            if (prefix.Length + suffix.Length < 64) continue;
            var parts = new JArray();
            if (prefix.Length > 0) parts.Add(Reference(prefix));
            string middle = text.Substring(prefix.Length, text.Length - prefix.Length - suffix.Length);
            if (middle.Length > 0) parts.Add(middle);
            if (suffix.Length > 0) parts.Add(Reference(suffix));
            value.Replace(new JObject { ["$text"] = parts });
        }
        var encoded = new JObject
        {
            ["encoding"] = "repeated-public-text-v1", ["strings"] = new JArray(strings), ["data"] = data
        };
        if (!JToken.DeepEquals(payload, ExpandCompactPublicText(encoded)))
            throw new InvalidOperationException("Repeated public text encoding changed its immutable input.");
        return encoded;
    }

    internal static JObject ExpandCompactPublicText(JObject encoded)
    {
        if (encoded == null || encoded.Properties().Count() != 3
            || (string)encoded["encoding"] != "repeated-public-text-v1"
            || encoded["strings"] is not JArray strings
            || strings.Any(value => value.Type != JTokenType.String)
            || encoded["data"] is not JObject source)
            throw new InvalidOperationException("Invalid repeated public text encoding.");
        JObject data = (JObject)source.DeepClone();
        foreach (JObject text in data.Descendants().OfType<JObject>()
                     .Where(value => value.Property("$text") != null).ToArray())
        {
            if (text.Properties().Count() != 1 || text["$text"] is not JArray parts || parts.Count == 0)
                throw new InvalidOperationException("Invalid repeated public text parts.");
            var restored = new StringBuilder();
            foreach (JToken part in parts)
            {
                if (part.Type == JTokenType.String) restored.Append((string)part);
                else if (part is JObject reference && reference.Properties().Count() == 1
                    && reference["$s"]?.Type == JTokenType.Integer
                    && (long)reference["$s"] >= 0 && (long)reference["$s"] < strings.Count)
                    restored.Append((string)strings[(int)reference["$s"]]);
                else throw new InvalidOperationException("Invalid repeated public text reference.");
            }
            text.Replace(new JValue(restored.ToString()));
        }
        foreach (JObject table in data.Descendants().OfType<JObject>()
                     .Where(value => value.Property("columnValues") != null).ToArray())
        {
            if (table.Properties().Count() != 3 || table["columns"] is not JArray columns
                || table["rowCount"]?.Type != JTokenType.Integer
                || (long)table["rowCount"] < 0 || (long)table["rowCount"] > int.MaxValue
                || table["columnValues"] is not JArray columnValues
                || columns.Count != columnValues.Count)
                throw new InvalidOperationException("Invalid repeated public table columns.");
            int rowCount = (int)table["rowCount"];
            foreach (JToken column in columnValues)
                if (!(column is JArray series && series.Count == rowCount)
                    && !(column is JObject constant && constant.Properties().Count() == 1
                        && constant.Property("$repeat") != null))
                    throw new InvalidOperationException("Invalid repeated public table values.");
            var rows = new JArray();
            for (int row = 0; row < rowCount; row++)
                rows.Add(new JArray(columnValues.Select(column =>
                    (column is JArray series ? series[row] : column["$repeat"]).DeepClone())));
            table.Property("columnValues").Remove();
            table.Property("rowCount").Remove();
            table["rows"] = rows;
        }
        return data;
    }

    private static IEnumerable<JValue> PublicFactReferences(JObject payload)
    {
        foreach (JProperty property in ((JObject)payload["publicNarrativeContext"]).Descendants().OfType<JProperty>()
                     .Where(property => property.Name == "factId" || property.Name == "evidenceId"))
            if (property.Value is JValue value && value.Type == JTokenType.String) yield return value;
        foreach (string key in new[] { "backgroundFactIds", "relationshipFactIds", "memoryFactIds", "priorHistoryFactIds" })
            foreach (JValue value in ((JArray)payload["publicNarrativeContext"][key]).OfType<JValue>())
                yield return value;
    }

    private static JObject CompactRows(JArray rows, params string[] columns)
    {
        if (rows.Any(row => row is not JObject
            || ((JObject)row).Properties().Any(property => !columns.Contains(property.Name))))
            throw new InvalidOperationException("Compact public table contains an unsupported field.");
        return new JObject
        {
            ["columns"] = new JArray(columns),
            ["rows"] = new JArray(rows.OfType<JObject>().Select(row =>
                new JArray(columns.Select(column => row[column]?.DeepClone() ?? JValue.CreateNull()))))
        };
    }

    private static string StyleInstruction(string profileId)
    {
        if (string.Equals(profileId, "CharacterSkill", StringComparison.Ordinal)
            || string.Equals(profileId, "EvolutionHistory", StringComparison.Ordinal))
        {
            return "Style: use a strong fantasy or wuxia name grounded in one character fact and one culture motif. "
                + "Avoid generic element-plus-weapon names and explain the personal history behind the name.";
        }
        if (string.Equals(profileId, "FacilityEvolution", StringComparison.Ordinal)
            || string.Equals(profileId, "FacilityEvolutionModuleSelection", StringComparison.Ordinal))
        {
            return "Style: use a distinctive workshop legend or dungeon chronicle voice, grounded in actual use and crisis history.";
        }
        if (string.Equals(profileId, "Persona", StringComparison.Ordinal)
            || string.Equals(profileId, "CharacterRecord", StringComparison.Ordinal))
        {
            return "Style: use medium-strength fantasy prose; let age, origin, ambition, and career shape the voice without listing them mechanically.";
        }
        if (string.Equals(profileId, "BubbleLine", StringComparison.Ordinal))
        {
            return "Style: use natural short spoken Korean. Do not sound like a title, narrator, system message, or ceremonial inscription.";
        }
        return "Style: use concise natural Korean with light culture flavor; clarity is more important than ornate wording.";
    }

    public static bool TryParse(string prompt, out NarrativeRequestContext context)
    {
        context = null;
        if (string.IsNullOrWhiteSpace(prompt)) return false;
        int start = prompt.IndexOf(BeginMarker, StringComparison.Ordinal);
        int end = prompt.IndexOf(EndMarker, StringComparison.Ordinal);
        if (start < 0 || end <= start) return false;

        string[] lines = prompt.Substring(start, end - start).Split(
            new[] { '\r', '\n' },
            StringSplitOptions.RemoveEmptyEntries);
        if (lines.Length == 0) return false;
        string header = lines[0];
        context = new NarrativeRequestContext(
            Header(header, "profile"),
            Header(header, "culture"),
            Header(header, "requireFact") == "1",
            Header(header, "requireMotif") == "1");
        for (int index = 1; index < lines.Length; index++)
        {
            string[] parts = lines[index].Split(new[] { '|' }, 3);
            if (parts.Length != 3) continue;
            string reference = parts[0].Trim();
            if (!IsReference(reference, 'F') && !IsReference(reference, 'M')) continue;
            NarrativeContextEntry entry = new NarrativeContextEntry(parts[1], parts[2], 0)
            {
                Reference = reference
            };
            (reference[0] == 'F' ? context.facts : context.motifs).Add(entry);
        }
        return true;
    }

    public bool TryResolveFact(string reference, out NarrativeContextEntry entry) =>
        Resolve(facts, reference, out entry);

    public bool TryResolveMotif(string reference, out NarrativeContextEntry entry) =>
        Resolve(motifs, reference, out entry);

    private static void AddUnique(
        List<NarrativeContextEntry> destination,
        string stableId,
        string label,
        int priority)
    {
        NarrativeContextEntry entry = new NarrativeContextEntry(stableId, label, priority);
        if (string.IsNullOrWhiteSpace(entry.StableId)
            || string.IsNullOrWhiteSpace(entry.Label)
            || destination.Any(value => string.Equals(
                value.StableId, entry.StableId, StringComparison.Ordinal))) return;
        destination.Add(entry);
    }

    private void RequireMutable()
    {
        if (frozen)
        {
            throw new InvalidOperationException(
                "A frozen narrative request context cannot be changed.");
        }
    }

    private static void AssignReferences(
        List<NarrativeContextEntry> entries,
        string prefix,
        int maximum)
    {
        NarrativeContextEntry[] selected = entries
            .OrderByDescending(value => value.Priority)
            .ThenBy(value => value.StableId, StringComparer.Ordinal)
            .Take(maximum).ToArray();
        entries.Clear();
        entries.AddRange(selected);
        for (int index = 0; index < entries.Count; index++)
            entries[index].Reference = prefix + (index + 1).ToString("00");
    }

    private static bool Resolve(
        IEnumerable<NarrativeContextEntry> entries,
        string reference,
        out NarrativeContextEntry entry)
    {
        string normalized = (reference ?? string.Empty).Trim().ToUpperInvariant();
        entry = entries.FirstOrDefault(value => string.Equals(
            value.Reference, normalized, StringComparison.Ordinal));
        return entry != null;
    }

    private static bool IsReference(string value, char prefix) =>
        value.Length == 3 && value[0] == prefix
        && char.IsDigit(value[1]) && char.IsDigit(value[2]);

    private static string Header(string header, string key)
    {
        string token = key + "=";
        int start = header.IndexOf(token, StringComparison.Ordinal);
        if (start < 0) return string.Empty;
        start += token.Length;
        int end = header.IndexOfAny(new[] { ' ', ']' }, start);
        return (end < 0 ? header.Substring(start) : header.Substring(start, end - start)).Trim();
    }
}

public static class NarrativeCultureStyleCatalog
{
    private sealed class Style { public string Id; public string[] Motifs; }

    public static NarrativeRequestContext Create(
        string profileId,
        string cultureOrSpecies,
        bool requireCharacterFact,
        bool requireMotif)
    {
        Style style = Resolve(cultureOrSpecies);
        NarrativeRequestContext context = new NarrativeRequestContext(
            profileId, style.Id, requireCharacterFact, requireMotif);
        for (int index = 0; index < style.Motifs.Length; index++)
            context.AddMotif($"motif:{style.Id}:{index + 1}", style.Motifs[index], style.Motifs.Length - index);
        return context;
    }

    private static Style Resolve(string value)
    {
        string key = (value ?? string.Empty).Trim().ToLowerInvariant();
        if (Has(key, "orc")) return New("orc-vigil", "scar", "iron spirit", "formation", "weapon vigil", "inherited weapon");
        if (Has(key, "vamp")) return New("vampire-nightcourt", "lunar eclipse", "blood incense", "candle", "court oath", "centuries of memory");
        if (Has(key, "demon")) return New("demon-contract", "ash", "seal", "debt", "contract", "ritual flame");
        if (Has(key, "slime")) return New("slime-confluence", "ripple", "confluence", "clear water", "core rhythm", "fluid memory");
        if (Has(key, "kobold")) return New("kobold-toolclan", "gear", "wedge", "blueprint", "named tool", "workshop lineage");
        if (Has(key, "mycon", "fung")) return New("myconid-grove", "spore", "mycelium", "mist", "garden", "return to colony");
        if (Has(key, "harpy")) return New("harpy-aerie", "wind", "dawn", "altitude", "wing", "chorus");
        if (Has(key, "beast")) return New("beastkin-pack", "footprint", "fang", "hunt", "campfire", "pack oath");
        if (Has(key, "golem", "construct")) return New("golem-core", "core", "resonance", "engraving", "steel", "memory plate");
        return New("adventurer-frontier", "road", "return", "fortress", "map", "frontier oath");
    }

    private static bool Has(string source, params string[] candidates) =>
        candidates.Any(source.Contains);
    private static Style New(string id, params string[] motifs) =>
        new Style { Id = id, Motifs = motifs };
}

public interface INarrativeTextQualityGate
{
    NarrativeQualityResult Evaluate(
        LocalLlmRequestProfile profile,
        string prompt,
        string response);
}

public sealed class NarrativeTextQualityGate : INarrativeTextQualityGate
{
    [Serializable]
    private sealed class Envelope
    {
        public string[] usedMotifIds = Array.Empty<string>();
        public string[] usedCharacterFactIds = Array.Empty<string>();
    }

    public NarrativeQualityResult Evaluate(
        LocalLlmRequestProfile profile,
        string prompt,
        string response)
    {
        if (profile == null) return Reject("Narrative profile is missing.");
        if (!NarrativeRequestContext.TryParse(prompt, out NarrativeRequestContext context))
            return Reject("Narrative request context is missing.");
        if (NarrativeExactKeyContract.IsRegisteredProfile(profile.Id))
        {
            if (!NarrativeExactKeyContract.TryValidateProfileResponse(
                    profile.Id,
                    response,
                    out string exactJson,
                    out string exactError))
            {
                return Reject(exactError);
            }
            if (!NarrativeExactKeyContract.TryCollectPlayerFacingText(exactJson, out List<string> prose, out string proseError))
                return Reject(proseError);
            if (prose.Any(value => ContainsUnknownReference(value, context)))
                return Reject("Unknown inline reference.");
            if (prose.Any(value => ContainsStableId(value, context)))
                return Reject("Internal stable id leak.");
            return new NarrativeQualityResult(
                NarrativeQualityVerdict.SoftPass,
                string.Empty,
                Array.Empty<string>(),
                Array.Empty<string>());
        }
        if (!TryExtractObject(response, out string json, out string error)) return Reject(error);

        Envelope envelope;
        try { envelope = JsonUtility.FromJson<Envelope>(json) ?? new Envelope(); }
        catch (Exception exception) { return Reject("Reference parse failed: " + exception.Message); }

        string[] motifs = Normalize(envelope.usedMotifIds, NarrativeRequestContext.MaximumMotifs);
        string[] facts = Normalize(envelope.usedCharacterFactIds, NarrativeRequestContext.MaximumFacts);
        if (motifs.Any(value => !context.TryResolveMotif(value, out _))) return Reject("Unknown motif reference.");
        if (facts.Any(value => !context.TryResolveFact(value, out _))) return Reject("Unknown character fact reference.");
        if (ContainsUnknownReference(json, context)) return Reject("Unknown inline reference.");
        if (ContainsStableId(json, context)) return Reject("Internal stable id leak.");
        if (context.RequireMotif && motifs.Length == 0) return Reject("A motif reference is required.");
        if (context.RequireCharacterFact && facts.Length == 0) return Reject("A character fact reference is required.");

        string[] motifIds = Resolve(motifs, context.TryResolveMotif);
        string[] factIds = Resolve(facts, context.TryResolveFact);
        return new NarrativeQualityResult(
            motifIds.Length >= 2 && factIds.Length >= 1
                ? NarrativeQualityVerdict.StrongPass
                : NarrativeQualityVerdict.SoftPass,
            string.Empty, motifIds, factIds);
    }

    private delegate bool Resolver(string value, out NarrativeContextEntry entry);

    private static string[] Resolve(IEnumerable<string> values, Resolver resolver)
    {
        List<string> result = new List<string>();
        foreach (string value in values)
            if (resolver(value, out NarrativeContextEntry entry)) result.Add(entry.StableId);
        return result.ToArray();
    }

    private static bool TryExtractObject(string response, out string json, out string error)
    {
        json = string.Empty;
        error = string.Empty;
        if (string.IsNullOrWhiteSpace(response)) { error = "LLM response is empty."; return false; }
        string trimmed = response.Trim();
        int first = trimmed.IndexOf('{');
        int last = trimmed.LastIndexOf('}');
        if (first < 0 || last < first) { error = "LLM response has no JSON object."; return false; }
        json = trimmed.Substring(first, last - first + 1);
        return true;
    }

    private static bool ContainsUnknownReference(string json, NarrativeRequestContext context)
    {
        for (int index = 0; index + 2 < json.Length; index++)
        {
            char prefix = char.ToUpperInvariant(json[index]);
            if ((prefix != 'F' && prefix != 'M')
                || !char.IsDigit(json[index + 1])
                || !char.IsDigit(json[index + 2])
                || (index > 0 && IsAsciiIdentifierCharacter(json[index - 1]))
                || (index + 3 < json.Length
                    && IsAsciiIdentifierCharacter(json[index + 3])))
            {
                continue;
            }
            string reference = string.Concat(prefix, json[index + 1], json[index + 2]);
            bool valid = prefix == 'F'
                ? context.TryResolveFact(reference, out _)
                : context.TryResolveMotif(reference, out _);
            if (!valid) return true;
        }
        return false;
    }

    private static bool IsAsciiIdentifierCharacter(char value) =>
        value is >= 'a' and <= 'z'
            or >= 'A' and <= 'Z'
            or >= '0' and <= '9'
            or '_';

    private static bool ContainsStableId(string json, NarrativeRequestContext context)
    {
        foreach (NarrativeContextEntry entry in context.Facts.Concat(context.Motifs))
            if (entry.StableId.Length >= 4 && json.IndexOf(entry.StableId, StringComparison.Ordinal) >= 0) return true;
        return false;
    }

    private static string[] Normalize(IEnumerable<string> values, int maximum) =>
        (values ?? Array.Empty<string>()).Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim().ToUpperInvariant()).Distinct(StringComparer.Ordinal)
            .Take(maximum).ToArray();

    private static NarrativeQualityResult Reject(string error) =>
        new NarrativeQualityResult(NarrativeQualityVerdict.HardReject, error,
            Array.Empty<string>(), Array.Empty<string>());
}
