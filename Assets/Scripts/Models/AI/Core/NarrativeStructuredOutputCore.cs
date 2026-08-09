using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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
    }

    public string ProfileId { get; }
    public string CultureStyleId { get; }
    public bool RequireCharacterFact { get; }
    public bool RequireMotif { get; }
    public IReadOnlyList<NarrativeContextEntry> Facts => facts;
    public IReadOnlyList<NarrativeContextEntry> Motifs => motifs;

    public void AddFact(string stableId, string label, int priority = 0) =>
        AddUnique(facts, stableId, label, priority);

    public void AddMotif(string stableId, string label, int priority = 0) =>
        AddUnique(motifs, stableId, label, priority);

    public string AppendToPrompt(string prompt)
    {
        if ((prompt ?? string.Empty).Contains(BeginMarker))
        {
            return prompt;
        }

        AssignReferences(facts, "F", MaximumFacts);
        AssignReferences(motifs, "M", MaximumMotifs);
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
        if (!TryParse(prompt, out NarrativeRequestContext context))
        {
            return prompt ?? string.Empty;
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
        if (suffixStart < prompt.Length)
        {
            builder.Append(prompt, suffixStart, prompt.Length - suffixStart);
        }
        return builder.ToString();
    }

    private static string StyleInstruction(string profileId)
    {
        if (string.Equals(profileId, "CharacterSkill", StringComparison.Ordinal)
            || string.Equals(profileId, "EvolutionHistory", StringComparison.Ordinal))
        {
            return "Style: use a strong fantasy or wuxia name grounded in one character fact and one culture motif. "
                + "Avoid generic element-plus-weapon names and explain the personal history behind the name.";
        }
        if (string.Equals(profileId, "FacilityEvolution", StringComparison.Ordinal))
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
            if ((prefix != 'F' && prefix != 'M') || !char.IsDigit(json[index + 1]) || !char.IsDigit(json[index + 2])) continue;
            string reference = string.Concat(prefix, json[index + 1], json[index + 2]);
            bool valid = prefix == 'F'
                ? context.TryResolveFact(reference, out _)
                : context.TryResolveMotif(reference, out _);
            if (!valid) return true;
        }
        return false;
    }

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
