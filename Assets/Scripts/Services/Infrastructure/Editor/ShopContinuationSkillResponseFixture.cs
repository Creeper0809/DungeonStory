#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

/// <summary>
/// Deterministic editor-only preference fixture for the controlled shop
/// continuation witness. It is not a model or training result.
/// </summary>
public static class ShopContinuationSkillResponseFixture
{
    private const int DisplayNameMaximumLength = 14;
    private const int NarrativeFlavorMaximumLength = 48;

    public static string Build(LocalLlmCapturedRequest request)
    {
        if (request == null)
        {
            throw new ArgumentNullException(nameof(request));
        }
        return BuildProfilePrompt(request.ProfileId, request.Prompt);
    }

    private static string BuildProfilePrompt(string profileId, string prompt)
    {
        if (!string.Equals(
                profileId,
                LocalLlmRequestProfiles.CharacterSkillModuleSelection.Id,
                StringComparison.Ordinal))
        {
            return null;
        }

        string[] lines = SplitLines(prompt);
        string character = RequireSingleValue(lines, "character=");
        CandidatePacket[] candidates = TryParseBatch(lines, out CandidatePacket[] batch)
            ? batch
            : new[] { ParseCandidate(lines) };
        if (candidates.Length is < 1 or > 3)
        {
            throw new InvalidOperationException(
                "Module-selection fixture requires one to three candidates.");
        }

        HashSet<string> usedSelectionIds = new(StringComparer.Ordinal);
        HashSet<string> usedPositiveModules = new(StringComparer.Ordinal);
        JObject[] responseCandidates = candidates
            .Select(candidate => BuildCandidate(
                candidate,
                character,
                usedSelectionIds,
                usedPositiveModules))
            .ToArray();

        JObject root = responseCandidates.Length == 1 && !HasBatchMarkers(lines)
            ? responseCandidates[0]
            : new JObject { ["candidates"] = new JArray(responseCandidates) };
        return root.ToString(Formatting.None);
    }

    /// <summary>
    /// Pure parser/response checks using synthetic packets only; these are not
    /// evidence that a runtime request was dispatched or a model was invoked.
    /// </summary>
    public static bool RunChecks()
    {
        JObject batch = JObject.Parse(BuildProfilePrompt(
            LocalLlmRequestProfiles.CharacterSkillModuleSelection.Id,
            BatchPacket(
            "리아",
            ("selection:shop:first", "output", "fact:shop:first"),
            ("selection:shop:second", "work_speed", "fact:shop:second"))));
        JArray batchCandidates = (JArray)batch["candidates"];
        Require(batchCandidates?.Count == 2,
            "Batch fixture did not preserve the exact candidate count.");
        Require((string)batchCandidates[0]["selectionId"]
                == "selection:shop:first"
            && (string)batchCandidates[1]["selectionId"]
                == "selection:shop:second"
            && (string)batchCandidates[0]["positiveModuleIds"][0] == "output"
            && (string)batchCandidates[1]["positiveModuleIds"][0]
                == "work_speed",
            "Batch fixture did not preserve candidate order or legal positives.");

        CharacterSkillModuleSelectionBatchResponseDto visitorBatch =
            JsonConvert.DeserializeObject<CharacterSkillModuleSelectionBatchResponseDto>(
                BuildProfilePrompt(
                    LocalLlmRequestProfiles.CharacterSkillModuleSelection.Id,
                    BatchPacket(
                        "세나 1",
                        ("selection:visitor:first", "output", "fact:visitor:first"),
                        ("selection:visitor:second", "work_speed", "fact:visitor:second"),
                        ("selection:visitor:third", "stock", "fact:visitor:third"))));
        Require(visitorBatch?.candidates?.Count == 3
                && visitorBatch.Validate(out _, "세나 1")
                && visitorBatch.candidates.Select(candidate => candidate.displayName)
                    .Distinct(StringComparer.Ordinal).Count() == 3,
            "Visitor preparation fixture repeated a candidate name or violated the production DTO contract.");
        visitorBatch.candidates[1].displayName = visitorBatch.candidates[0].displayName;
        Require(!visitorBatch.Validate(out _, "세나 1"),
            "Production DTO accepted duplicate visitor preparation skill names.");

        JObject single = JObject.Parse(BuildProfilePrompt(
            LocalLlmRequestProfiles.CharacterSkillModuleSelection.Id,
            SinglePacket(
            "세나",
            "selection:shop:single",
            "stock",
            "fact:shop:single")));
        Require((string)single["selectionId"] == "selection:shop:single"
            && (string)single["positiveModuleIds"][0] == "stock"
            && (string)single["evidenceFactIds"][0] == "fact:shop:single"
            && (string)single["displayName"] == "세나 감각"
            && (string)single["narrativeFlavor"]
                == "세나의 반복된 경험에서 익힌 감각이다.",
            "Single fixture did not produce the canonical module-selection response.");

        RequireThrows(
            () => BuildProfilePrompt(
                LocalLlmRequestProfiles.CharacterSkillModuleSelection.Id,
                "selectionId=selection:bad\npositiveModules:\n- id=output; meaning=test\ncharacter=리아\n"),
            "Malformed module-selection packet was accepted.");
        RequireThrows(
            () => BuildProfilePrompt(
                LocalLlmRequestProfiles.CharacterSkillModuleSelection.Id,
                BatchPacket(
                    "리아",
                    ("selection:shop:duplicate-a", "output", "fact:shop:a"),
                    ("selection:shop:duplicate-b", "output", "fact:shop:b"))),
            "Batch fixture reused a positive module across candidates.");
        RequireThrows(
            () => BuildProfilePrompt(
                LocalLlmRequestProfiles.CharacterSkillModuleSelection.Id,
                SinglePacket(
                    "가나다라마바사아자차카타파하",
                    "selection:shop:too-long",
                    "output",
                    "fact:shop:length")),
            "Overlength narrative fixture output was accepted.");
        Require(BuildProfilePrompt(
                    LocalLlmRequestProfiles.Persona.Id,
                    SinglePacket(
                        "리아",
                        "selection:shop:ignored",
                        "output",
                        "fact:shop:ignored")) == null,
            "Unsupported profile did not return null.");
        RunNameNumberChecks();
        return true;
    }

    /// <summary>
    /// Verifies the DTO-only exception for a numeric authoritative character
    /// identity. The name is supplied by the immutable request context through
    /// the overload, never by a response-owned identity field.
    /// </summary>
    public static bool RunNameNumberChecks()
    {
        const string authoritativeName = "세나 1";
        const string displayName = "세나 1 감각";
        const string narrativeFlavor = "세나 1의 반복된 경험에서 익힌 감각이다.";

        CharacterSkillModuleSelectionResponseDto valid = NewSelection(
            displayName,
            narrativeFlavor);
        Require(valid.Validate(out _, authoritativeName),
            "The exact numeric authoritative name was not permitted in module prose.");
        Require(!valid.Validate(out _),
            "The strict overload trusted a numeric response name without an authority.");
        Require(!valid.Validate(out _, "레오 2"),
            "A different authoritative name permitted 세나 1 in module prose.");

        RequireModuleNumberRejected("세나 12 감각", narrativeFlavor,
            "A name with an extra trailing number was accepted.");
        RequireModuleNumberRejected("1 감각", "1의 반복된 경험에서 익힌 감각이다.",
            "A digits-only authoritative name was accepted.", authoritativeName: "1");
        RequireModuleNumberRejected(displayName, "세나 1의 회복 20을 말한다.",
            "A mechanical effect number was accepted.");
        RequireModuleNumberRejected(displayName, "세나 1의 회복 20%를 말한다.",
            "An ASCII percentage was accepted.");
        RequireModuleNumberRejected(displayName, "세나 1의 회복 20％를 말한다.",
            "A full-width percentage was accepted.");
        RequireModuleNumberRejected("세나 １ 감각", "세나 １의 반복된 경험에서 익힌 감각이다.",
            "A Unicode digit was accepted.");
        RequireModuleNumberRejected("1세나 1 감각", narrativeFlavor,
            "A number prefixed to the authoritative name was accepted.");

        CharacterSkillGenerationResponseDto presentation = new()
        {
            presentationId = "presentation:skill:" + new string('a', 64),
            displayName = displayName,
            narrativeFlavor = narrativeFlavor
        };
        Require(presentation.Validate(out _, authoritativeName),
            "A canonical presentation:skill ID rejected the authoritative numeric name.");
        presentation.narrativeFlavor = "세나 1의 회복 20을 말한다.";
        Require(!presentation.Validate(out _, authoritativeName),
            "A canonical presentation:skill ID permitted an effect number.");

        CharacterSkillModuleSelectionBatchResponseDto validBatch = new()
        {
            candidates = new List<CharacterSkillModuleSelectionResponseDto>
            {
                NewSelection(displayName, narrativeFlavor),
                NewSelection("세나 1 기척", "세나 1의 누적된 발걸음에서 익힌 기척이다.")
            }
        };
        Require(validBatch.Validate(out _, authoritativeName),
            "The batch overload rejected authoritative numeric-name prose.");
        validBatch.candidates[1].narrativeFlavor = "세나 1의 회복 20을 말한다.";
        Require(!validBatch.Validate(out _, authoritativeName),
            "The batch overload accepted a malicious second candidate.");
        return true;
    }

    private static bool TryParseBatch(
        IReadOnlyList<string> lines,
        out CandidatePacket[] candidates)
    {
        candidates = null;
        if (!HasBatchMarkers(lines))
        {
            if (lines.Any(line => line.StartsWith(
                    "candidateCount=",
                    StringComparison.Ordinal)))
            {
                throw new InvalidOperationException(
                    "Module-selection packet declared a batch count without candidates.");
            }
            return false;
        }

        string countValue = RequireSingleValue(lines, "candidateCount=");
        if (!int.TryParse(countValue, out int declaredCount)
            || declaredCount is < 1 or > 3)
        {
            throw new InvalidOperationException(
                "Module-selection batch has an invalid candidate count.");
        }

        List<int> starts = new();
        for (int index = 0; index < lines.Count; index++)
        {
            if (!TryReadCandidateHeader(lines[index], out int candidateIndex))
            {
                continue;
            }
            if (candidateIndex != starts.Count)
            {
                throw new InvalidOperationException(
                    "Module-selection batch candidate indices are not sequential.");
            }
            starts.Add(index);
        }
        if (starts.Count != declaredCount)
        {
            throw new InvalidOperationException(
                "Module-selection batch candidate count does not match its packet.");
        }

        candidates = new CandidatePacket[starts.Count];
        for (int index = 0; index < starts.Count; index++)
        {
            int end = index + 1 < starts.Count ? starts[index + 1] : lines.Count;
            candidates[index] = ParseCandidate(lines.Skip(starts[index] + 1)
                .Take(end - starts[index] - 1)
                .ToArray());
        }
        return true;
    }

    private static CandidatePacket ParseCandidate(IReadOnlyList<string> lines)
    {
        string selectionId = RequireSingleValue(lines, "selectionId=");
        if (selectionId.Length > 96)
        {
            throw new InvalidOperationException(
                "Module-selection fixture selectionId exceeds its schema limit.");
        }

        int positiveStart = RequireSingleLineIndex(lines, "positiveModules:");
        int drawbackStart = FindLineIndex(lines, "drawbackModules:", positiveStart + 1);
        if (drawbackStart < 0)
        {
            throw new InvalidOperationException(
                "Module-selection packet has no drawbackModules boundary.");
        }

        List<string> positives = new();
        for (int index = positiveStart + 1; index < drawbackStart; index++)
        {
            string line = lines[index].Trim();
            if (!line.StartsWith("- id=", StringComparison.Ordinal))
            {
                continue;
            }
            string value = line.Substring("- id=".Length);
            int separator = value.IndexOf(';');
            string moduleId = (separator >= 0 ? value.Substring(0, separator) : value)
                .Trim();
            if (moduleId.Length == 0 || !positives.Contains(moduleId))
            {
                if (moduleId.Length == 0)
                {
                    throw new InvalidOperationException(
                        "Module-selection packet contains an empty positive module ID.");
                }
                positives.Add(moduleId);
            }
        }
        if (positives.Count == 0)
        {
            throw new InvalidOperationException(
                "Module-selection packet has no positive module candidates.");
        }

        string evidenceLine = RequireSingleValue(lines, "evidenceFactIds=");
        string evidence = evidenceLine.Split(',')
            .Select(value => value.Trim())
            .FirstOrDefault(value => value.Length > 0);
        if (string.IsNullOrWhiteSpace(evidence))
        {
            throw new InvalidOperationException(
                "Module-selection packet has no usable evidence fact.");
        }
        return new CandidatePacket(selectionId, positives, evidence);
    }

    private static JObject BuildCandidate(
        CandidatePacket candidate,
        string character,
        ISet<string> usedSelectionIds,
        ISet<string> usedPositiveModules)
    {
        if (!usedSelectionIds.Add(candidate.SelectionId))
        {
            throw new InvalidOperationException(
                "Module-selection batch repeated a selection ID.");
        }
        string positive = candidate.PositiveModules.FirstOrDefault(
            moduleId => !usedPositiveModules.Contains(moduleId));
        if (string.IsNullOrWhiteSpace(positive))
        {
            throw new InvalidOperationException(
                "Module-selection batch has no unused positive module candidate.");
        }
        usedPositiveModules.Add(positive);

        // The production batch contract rejects duplicate names even when the
        // selected modules differ. These names are isolated preparation only.
        string sense = usedSelectionIds.Count switch
        {
            1 => "감각",
            2 => "직감",
            3 => "통찰",
            _ => throw new InvalidOperationException("Unsupported fixture candidate count.")
        };
        string displayName = character + " " + sense;
        string narrativeFlavor = character + "의 반복된 경험에서 익힌 " + sense + "이다.";
        if (displayName.Length > DisplayNameMaximumLength
            || narrativeFlavor.Length > NarrativeFlavorMaximumLength)
        {
            throw new InvalidOperationException(
                "Module-selection fixture output exceeds the response schema length limit.");
        }

        return new JObject
        {
            ["selectionId"] = candidate.SelectionId,
            ["positiveModuleIds"] = new JArray(positive),
            ["drawbackModuleIds"] = new JArray(),
            ["evidenceFactIds"] = new JArray(candidate.EvidenceFactId),
            ["displayName"] = displayName,
            ["narrativeFlavor"] = narrativeFlavor
        };
    }

    private static CharacterSkillModuleSelectionResponseDto NewSelection(
        string displayName,
        string narrativeFlavor) => new()
    {
        selectionId = "selection:shop:name-number",
        positiveModuleIds = new List<string> { "output" },
        drawbackModuleIds = new List<string>(),
        evidenceFactIds = new List<string> { "fact:shop:name-number" },
        displayName = displayName,
        narrativeFlavor = narrativeFlavor
    };

    private static void RequireModuleNumberRejected(
        string displayName,
        string narrativeFlavor,
        string message,
        string authoritativeName = "세나 1")
    {
        Require(!NewSelection(displayName, narrativeFlavor).Validate(
                out _, authoritativeName),
            message);
    }

    private static string[] SplitLines(string prompt)
    {
        if (string.IsNullOrWhiteSpace(prompt))
        {
            throw new InvalidOperationException(
                "Module-selection fixture requires a non-empty captured prompt.");
        }
        return prompt.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
    }

    private static string RequireSingleValue(
        IEnumerable<string> lines,
        string prefix)
    {
        string[] values = lines
            .Where(line => line.StartsWith(prefix, StringComparison.Ordinal))
            .Select(line => line.Substring(prefix.Length).Trim())
            .ToArray();
        if (values.Length != 1 || string.IsNullOrWhiteSpace(values[0]))
        {
            throw new InvalidOperationException(
                "Module-selection packet is missing one canonical '" + prefix + "' line.");
        }
        return values[0];
    }

    private static int RequireSingleLineIndex(
        IReadOnlyList<string> lines,
        string expected)
    {
        int[] matches = lines
            .Select((line, index) => new { line, index })
            .Where(value => string.Equals(
                value.line,
                expected,
                StringComparison.Ordinal))
            .Select(value => value.index)
            .ToArray();
        if (matches.Length != 1)
        {
            throw new InvalidOperationException(
                "Module-selection packet is missing one '" + expected + "' section.");
        }
        return matches[0];
    }

    private static int FindLineIndex(
        IReadOnlyList<string> lines,
        string expected,
        int start)
    {
        for (int index = start; index < lines.Count; index++)
        {
            if (string.Equals(lines[index], expected, StringComparison.Ordinal))
            {
                return index;
            }
        }
        return -1;
    }

    private static bool HasBatchMarkers(IReadOnlyList<string> lines) =>
        lines.Any(line => TryReadCandidateHeader(line, out _));

    private static bool TryReadCandidateHeader(string line, out int index)
    {
        index = -1;
        if (string.IsNullOrEmpty(line)
            || !line.StartsWith("candidate[", StringComparison.Ordinal)
            || !line.EndsWith("]:", StringComparison.Ordinal))
        {
            return false;
        }
        string value = line.Substring(
            "candidate[".Length,
            line.Length - "candidate[".Length - 2);
        return int.TryParse(value, out index) && index >= 0;
    }

    private static string SinglePacket(
        string character,
        string selectionId,
        string positiveModule,
        string evidence) =>
        "selectionId=" + selectionId + "\n"
        + "positiveModules:\n"
        + "- id=" + positiveModule + "; meaning=fixture\n"
        + "drawbackModules:\n"
        + "- none (return [])\n"
        + "evidenceFactIds=" + evidence + "\n"
        + "character=" + character + "\n";

    private static string BatchPacket(
        string character,
        params (string selectionId, string positiveModule, string evidence)[] candidates)
    {
        StringBuilder builder = new();
        builder.Append("candidateCount=").Append(candidates.Length).Append('\n');
        for (int index = 0; index < candidates.Length; index++)
        {
            (string selectionId, string positiveModule, string evidence) candidate =
                candidates[index];
            builder.Append("candidate[").Append(index).Append("]:\n")
                .Append("selectionId=").Append(candidate.selectionId).Append('\n')
                .Append("positiveModules:\n")
                .Append("- id=").Append(candidate.positiveModule)
                .Append("; meaning=fixture\n")
                .Append("drawbackModules:\n")
                .Append("- none (return [])\n")
                .Append("evidenceFactIds=").Append(candidate.evidence).Append('\n');
        }
        return builder.Append("sharedEvidenceFacts:\ncharacter=")
            .Append(character)
            .Append('\n')
            .ToString();
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    private static void RequireThrows(Action action, string message)
    {
        try
        {
            action();
        }
        catch (InvalidOperationException)
        {
            return;
        }
        throw new InvalidOperationException(message);
    }

    private sealed class CandidatePacket
    {
        public CandidatePacket(
            string selectionId,
            IReadOnlyList<string> positiveModules,
            string evidenceFactId)
        {
            SelectionId = selectionId;
            PositiveModules = positiveModules;
            EvidenceFactId = evidenceFactId;
        }

        public string SelectionId { get; }
        public IReadOnlyList<string> PositiveModules { get; }
        public string EvidenceFactId { get; }
    }
}
#endif
