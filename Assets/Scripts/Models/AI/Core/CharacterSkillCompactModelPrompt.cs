using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

// Transport projection only. Original queue prompt, public hash, offers and
// response validators remain authoritative; table indices are never game IDs.
internal static class CharacterSkillCompactModelPrompt
{
    private const string ProseAuthorityInstruction =
        "각 후보에 서로 다른 기술명과 완결된 획득 서사를 창작하되 사실을 만들지는 않는다. " +
        "displayName은 첫 positiveModuleIds의 특징과 문화 모티프를 담은 서로 다른 한국어 기술명이다. " +
        "캐릭터 본명만 기술명으로 쓰지 않는다. " +
        "narrativeFlavor는 character의 정확한 본명을 문장의 주어로 한 번 넣고, 인용한 실제 경험을 문화 모티프로 비유하여 주효과를 익힌 이유를 쓴다. " +
        "본명을 확인하는 문구나 작성 지시를 서사로 쓰지 않는다. 후보별 경험과 주효과가 드러나는 서로 다른 한국어 한 문장을 마침표로 완결한다. " +
        "기술을 익히기 전의 실제 경험과 이제 얻는 기술을 구분한다. 인용한 근거에 없는 청소·방 이동·전투·부상·기술 사용을 과거 사건으로 만들지 않는다. " +
        "주변 환경 변화는 본인이 만든 결과라는 증거가 아니다. 주효과의 모듈 의미를 다른 기능이나 다른 지표의 변화로 바꾸지 않는다. " +
        "문화는 비유로만 쓰고, 주변 수치보다 완료한 행동·경험 근거를 우선한다. " +
        "근거 문장을 그대로 옮기거나 지표 변화·기계 효과를 설명하지 않는다. " +
        "숫자·퍼센트·지표명·ID는 서술에 복사하지 않는다. 숫자를 한글·한자 등으로 바꿔 쓰는 것도 금지다. " +
        "정확한 캐릭터 본명에 원래 포함된 숫자만 예외다. 이름은 14자 이하, 서사는 마침표를 포함해 48자 이하로 쓴다.";

    // Reversible wording compaction of existing instructions, not game rules.
    // Qualitative module meanings and every immutable packet value stay intact.
    private static readonly Dictionary<string, string> Instructions = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["당신은 던전 경영 RPG 기술에 어울리는 기능 모듈과 실제 근거를 고르고 이름과 서사를 작성한다."] = "Select skill modules and actual evidence; write a grounded name and story.",
        ["C#이 합법인 개별 모듈 전체를 제공한다. 모듈만 선택하며 수치와 비용은 응답 후 C#이 계산한다."] = "Select only C# offers. C# computes numbers and costs after the response.",
        ["형식: {\"candidates\":[{\"selectionId\":\"제공된 값 그대로\",\"positiveModuleIds\":[\"lowercase_snake_case 1~3개\"],\"drawbackModuleIds\":[\"character-skill:drawback:lowercase-kebab 0~1개\"],\"evidenceFactIds\":[\"제공된 근거 1~2개\"],\"displayName\":\"14자 이하 한국어 이름\",\"narrativeFlavor\":\"48자 이하 간결한 한국어 한 문장\"}]}"] = "Return {\"candidates\":[{\"selectionId\":\"exact offered ID\",\"positiveModuleIds\":[],\"drawbackModuleIds\":[],\"evidenceFactIds\":[],\"displayName\":\"Korean <=14 characters\",\"narrativeFlavor\":\"Korean one sentence <=48 characters\"}]}.",
        ["주효과 계약 v1: positiveModuleIds[0]이 주효과다. 나머지는 보조효과다. 이름과 설명은 첫 효과를 중심으로 쓰고 보조효과를 주효과처럼 이름 붙이지 않는다. 수치·비율은 C#만 배분한다."] = "Primary effect: positiveModuleIds[0]; others are secondary. Name/story must center on the first effect, not secondary ones. Only C# allocates numbers and ratios.",
        ["주효과 계약 v1: positiveModuleIds의 첫 항목은 근거 서사의 중심 효과이며 나머지는 보조다. C#은 추가 강화 예산을 주효과에 보조 전체 이상 배분한다. displayName은 첫 효과를 중심으로 짓고 보조 기능을 주된 능력처럼 이름 붙이지 않는다. 수치나 배분 비율은 작성하지 않는다."] = "The first positiveModuleIds entry is the story's primary effect. C# assigns its extra budget at least as much as all secondary effects combined. Name it for the first effect; emit no numbers or allocation ratios.",
        ["1. 최상위에는 candidates만 출력하고, 제공된 후보 수와 정확히 같은 수의 객체를 제공된 순서대로 출력한다. 각 객체에는 selectionId, positiveModuleIds, drawbackModuleIds, evidenceFactIds, displayName, narrativeFlavor만 출력한다."] = "Output only candidates; exactly the offered count, in offered order. Each object has only selectionId, positiveModuleIds, drawbackModuleIds, evidenceFactIds, displayName, narrativeFlavor.",
        ["2. selectionId는 제공된 값을 한 글자도 바꾸지 않는다. positiveModuleIds에는 positiveModules의 ID만, drawbackModuleIds에는 drawbackModules의 ID만, evidenceFactIds에는 evidenceFactIds의 ID만 1~2개 넣고 서로 섞거나 만들지 않는다."] = "Copy selectionId exactly. Use only the respective positive, drawback and evidence offers; cite 1..2 exact evidence IDs. Never mix or invent IDs.",
        ["3. 각 후보의 positiveModuleIds에는 어울리는 이로운 모듈을 최소 1개 선택하고, 후보끼리 positiveModuleIds 조합을 중복하지 않는다. drawbackModules가 제공되면 부정 사건이 실제 설명에 필요할 때만 최대 1개 선택하고 그 부정 근거 ID를 반드시 함께 인용한다. 단점 설명의 '함께 선택 금지 이로운 기능'과 겹치는 모듈은 고르지 않는다. narrativeFlavor는 수치나 기계 효과를 설명하지 않는 간결한 한국어 한 문장으로 쓴다. 수치, 비용, 조합 ID는 출력하지 않는다."] = "Choose 1..3 positive modules per candidate, with distinct combinations across candidates. Choose at most one drawback, only if actual negative evidence is needed; cite that evidence. Respect forbidden positive combinations. Write one concise Korean sentence, without numbers, costs, combination IDs or mechanical effects.",
        ["4. 공개 사실에 없는 사건이나 결과를 만들지 않는다."] = "Invent no events or outcomes absent from public facts.",
    };

    private static readonly string[] OutcomeSeparators =
    {
        "; sourceKind=", "; selectable=", "; outcomeType=", "; subject=",
        "; days=", "; roles=", "; metrics=", "; cause=", "; fact="
    };

    internal static string Build(string prompt)
    { return Build(prompt, out _); }

    internal static string Build(string prompt, out JObject table)
    {
        table = null;
        string validated = NarrativeRequestContext.ToModelPromptWithoutCanonicalPublicPayload(prompt);
        string normalized = validated.Replace("\r\n", "\n");
        int start = normalized.StartsWith("candidateCount=", StringComparison.Ordinal) ? 0
            : normalized.IndexOf("\ncandidateCount=", StringComparison.Ordinal);
        if (start >= 0 && normalized[start] == '\n') start++;
        // The existing single-candidate profile has no shared batch packet.
        // Its validated representation is intentionally unchanged.
        if (start < 0) return ProjectNarrativeEvidence(prompt, validated, null);
        int end = normalized.IndexOf("\ncharacter=", start, StringComparison.Ordinal);
        Require(end > start, "Skill batch packet has no owner boundary.");
        string originalPacket = normalized.Substring(start, end - start + 1);
        table = CapturePacket(originalPacket);
        Require(RestorePacket(table) == originalPacket, "Skill compact packet lost immutable input.");
        string requestRef = RequestRef(prompt);
        JObject modelTable = ReferencedTable(table, requestRef);
        string suffix = CompactInstructions(normalized.Substring(end + 1), useReferences: true);
        string compactSuffix = CompactLocalFactLines(suffix, out JArray localFacts);
        Require(RestoreLocalFactLines(compactSuffix, localFacts) == suffix,
            "Skill request-local facts changed during transport compaction.");
        // The readable F lines already contain every local value verbatim;
        // do not send the same fact catalog a second time.
        Require(JToken.DeepEquals(ExpandReferencedTable(modelTable, table, requestRef), table),
            "Skill model table lost immutable packet values.");
        suffix = ProjectNarrativeEvidence(prompt, suffix, (JArray)table["E"]);
        return CompactInstructions(normalized.Substring(0, start))
            + "Return requestRef exactly plus candidates. Use C selection references S0.. and E evidence references E0.. exactly; "
            + "C# alone resolves these against this immutable request. M contains full module IDs; copy them unchanged. "
            + "Zero-based tables: X uses primary P; C uses allowed list A. O=[E,H,G]; H=outcome metadata; "
            + "G metrics=[D,V], D=[metric ID,unit], V=exact numeric strings. F labels are prose facts, not selectable E references.\n"
            + modelTable.ToString(Formatting.None) + "\n" + ReadableOutcomeFacts(table) + SharedLocalFactLines(suffix)
            + "\n" + ProseAuthorityInstruction;
    }

    private static string ProjectNarrativeEvidence(string prompt, string text, JArray evidenceCatalog)
    {
        const string prefix = "narrativeEvidenceRefs=";
        string[] required = System.Text.RegularExpressions.Regex.Matches(
                prompt.Replace("\r\n", "\n"), "^evidenceFactIds=(.*)$",
                System.Text.RegularExpressions.RegexOptions.Multiline)
            .Cast<System.Text.RegularExpressions.Match>()
            .SelectMany(match => match.Groups[1].Value.Split(','))
            .Where(id => id.StartsWith("skill-evidence:", StringComparison.Ordinal))
            .Distinct(StringComparer.Ordinal).OrderBy(id => id, StringComparer.Ordinal).ToArray();
        string[] lines = text.Replace("\r\n", "\n").Split('\n');
        string[] mappingLines = lines.Where(line => line.StartsWith(prefix, StringComparison.Ordinal)).ToArray();
        if (mappingLines.Length == 0 && required.Length == 0) return text;
        Require(mappingLines.Length == 1, "Skill narrative evidence mapping is missing or duplicated.");
        JObject mapping = JObject.Parse(mappingLines[0].Substring(prefix.Length),
            new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
        Require(mapping.Properties().Select(value => value.Name).OrderBy(id => id, StringComparer.Ordinal)
            .SequenceEqual(required, StringComparer.Ordinal), "Skill narrative evidence mapping differs from offers.");
        if (required.Length == 0) return text.Replace(mappingLines[0], string.Empty);
        Require(NarrativePublicModelInput.TryParsePrompt(prompt, out NarrativePublicModelInput modelInput),
            "Skill narrative evidence requires frozen public input.");
        JObject publicInput = JObject.Parse(modelInput.CanonicalPayload);
        var projected = new JObject();
        foreach (string id in required)
        {
            Require(mapping[id].Type == JTokenType.String, "Skill narrative public identity is not a string.");
            string factId = (string)mapping[id];
            JObject[] facts = ((JArray)publicInput["publicFacts"]).OfType<JObject>()
                .Where(fact => (string)fact["factId"] == factId).ToArray();
            JObject[] events = ((JArray)publicInput["publicNarrativeContext"]["events"]).OfType<JObject>()
                .Where(fact => (string)fact["factId"] == factId).ToArray();
            Require(facts.Length == 1 && events.Length == 1,
                "Selected skill narrative evidence has no frozen public fact/event: " + id);
            string reference = id;
            if (evidenceCatalog != null)
            {
                int index = evidenceCatalog.Values<string>().ToList().IndexOf(id);
                Require(index >= 0, "Skill narrative evidence is outside the immutable catalog.");
                reference = "E" + index.ToString(System.Globalization.CultureInfo.InvariantCulture);
            }
            var participantIds = new HashSet<string>(new[] {
                (string)events[0]["actorId"], (string)events[0]["targetId"] }, StringComparer.Ordinal);
            projected.Add(reference, new JObject
            {
                ["sourceKind"] = "progression-aggregate",
                ["fact"] = facts[0].DeepClone(),
                ["event"] = events[0].DeepClone(),
                ["participants"] = new JArray(((JArray)publicInput["publicNarrativeContext"]["entities"])
                    .OfType<JObject>().Where(entity => participantIds.Contains((string)entity["entityId"]))
                    .Select(entity => entity.DeepClone()))
            });
        }
        return text.Replace(mappingLines[0],
            "Narrative evidence below is an accumulated progression fact, not an exact outcome or proof of a causal link.\n"
            + "narrativeEvidence=" + projected.ToString(Formatting.None));
    }

    private static string ReadableOutcomeFacts(JObject table)
    {
        // A read-only view of the same packet: no invented actor/action/cause,
        // no changed eligibility, and no repair of model-produced prose.
        var builder = new StringBuilder("읽기용 원문 공유 (E별 신원·수치는 O 표 그대로):\n");
        var groups = ((JArray)table["O"]).OfType<JArray>().GroupBy(outcome => new
        {
            SourceKind = (string)outcome[1],
            Selectable = (string)outcome[2],
            OutcomeType = (string)outcome[3],
            Subject = (int)outcome[4],
            Days = (string)outcome[5],
            Roles = (int)outcome[6],
            Cause = (string)outcome[8],
            Fact = (int)outcome[9],
            Experience = (string)outcome[10]
        });
        foreach (var group in groups)
        {
            // Share only identical wording/metadata. Every E identity and its
            // individual metrics remain in the original immutable table.
            JArray outcome = group.First();
            builder.Append(string.Join(",", group.Select(row => "E" + ((int)row[0]).ToString(
                    System.Globalization.CultureInfo.InvariantCulture))))
                .Append(" [").Append((string)outcome[3])
                .Append("; selectable=").Append((string)outcome[2]).Append("] ")
                .Append((string)table["T"][(int)outcome[9]]);
            if (outcome[10].Type != JTokenType.Null)
                builder.Append(" [경험 평가: ").Append((string)outcome[10]).Append(']');
            builder.Append('\n');
        }
        return builder.ToString();
    }

    private static string SharedLocalFactLines(string suffix)
    {
        // F aliases are prose context, not outcome identities. Only share
        // byte-equal text within one consecutive fact block; retain every alias
        // and every non-fact line. The original suffix remains authoritative.
        var output = new List<string>();
        string[] lines = suffix.Split('\n');
        int cursor = 0;
        while (cursor < lines.Length)
        {
            var facts = new List<System.Text.RegularExpressions.Match>();
            while (cursor < lines.Length)
            {
                var match = System.Text.RegularExpressions.Regex.Match(
                    lines[cursor], "^(F[0-9]+) = (.*)$");
                if (!match.Success) break;
                facts.Add(match);
                cursor++;
            }
            if (facts.Count == 0)
            {
                output.Add(lines[cursor++]);
                continue;
            }
            foreach (var group in facts.GroupBy(value => value.Groups[2].Value,
                StringComparer.Ordinal))
                output.Add(string.Join(",", group.Select(value => value.Groups[1].Value))
                    + " = " + group.Key);
        }
        return string.Join("\n", output);
    }

    internal static string RequestRef(string prompt)
    {
        using var sha = System.Security.Cryptography.SHA256.Create();
        return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(prompt)))
            .Replace("-", string.Empty).ToLowerInvariant();
    }

    internal static JObject ReferencedTable(JObject raw, string requestRef)
    {
        JObject model = CompactTable(raw);
        JArray candidates = (JArray)model["C"];
        for (int index = 0; index < candidates.Count; index++) candidates[index][0] = "S" + index;
        model["E"] = new JArray(Enumerable.Range(0, ((JArray)raw["E"]).Count).Select(index => "E" + index));
        model.Remove("E_prefix");
        model["transport"] = "skill-request-refs-v2";
        model["requestRef"] = requestRef;
        return model;
    }

    internal static JObject ExpandReferencedTable(JObject model, JObject source, string requestRef)
    {
        Require((string)model["transport"] == "skill-request-refs-v2"
            && (string)model["requestRef"] == requestRef, "Skill reference table request differs.");
        JObject raw = (JObject)model.DeepClone();
        JArray candidates = (JArray)raw["C"];
        Require(candidates.Count == ((JArray)source["C"]).Count, "Skill reference candidate count differs.");
        for (int index = 0; index < candidates.Count; index++)
        {
            Require((string)candidates[index][0] == "S" + index, "Skill reference candidate order differs.");
            candidates[index][0] = source["C"][index][0].DeepClone();
        }
        Require(((JArray)raw["E"]).Values<string>().SequenceEqual(
            Enumerable.Range(0, ((JArray)source["E"]).Count).Select(index => "E" + index)),
            "Skill reference evidence catalog differs.");
        raw["E"] = source["E"].DeepClone(); raw["E_prefix"] = string.Empty;
        raw.Remove("transport"); raw.Remove("requestRef");
        return ExpandTable(raw);
    }

    internal static string Resolve(string prompt, string content)
    {
        Build(prompt, out JObject table);
        if (table == null) return content;
        JObject response = JObject.Parse(content, new JsonLoadSettings
            { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
        Require(response.Properties().Select(property => property.Name).OrderBy(value => value)
            .SequenceEqual(new[] { "candidates", "requestRef" }), "Skill reference response keys differ.");
        Require(response["requestRef"]?.Type == JTokenType.String
            && (string)response["requestRef"] == RequestRef(prompt), "Skill reference response request differs.");
        Require(response["candidates"] is JArray, "Skill reference candidates are missing.");
        JArray candidates = (JArray)response["candidates"];
        Require(candidates.Count == ((JArray)table["C"]).Count, "Skill reference response count differs.");
        for (int index = 0; index < candidates.Count; index++)
        {
            Require(candidates[index] is JObject, "Skill reference candidate is not an object.");
            JObject candidate = (JObject)candidates[index];
            Require(candidate["selectionId"]?.Type == JTokenType.String
                && (string)candidate["selectionId"] == "S" + index, "Skill reference selection order differs.");
            candidate["selectionId"] = table["C"][index][0].DeepClone();
            Require(candidate["evidenceFactIds"] is JArray, "Skill reference evidence is missing.");
            JArray selected = (JArray)candidate["evidenceFactIds"];
            for (int fact = 0; fact < selected.Count; fact++)
            {
                string reference = selected[fact].Type == JTokenType.String ? (string)selected[fact] : string.Empty;
                int evidenceIndex = -1;
                Require(reference.StartsWith("E", StringComparison.Ordinal)
                    && int.TryParse(reference.Substring(1), System.Globalization.NumberStyles.None,
                        System.Globalization.CultureInfo.InvariantCulture, out evidenceIndex)
                    && evidenceIndex >= 0 && evidenceIndex < ((JArray)table["E"]).Count
                    && reference == "E" + evidenceIndex
                    && ((JArray)table["C"][index][5]).Values<int>().Contains(evidenceIndex),
                    "Skill reference evidence is unknown or outside this candidate's offers.");
                selected[fact] = table["E"][evidenceIndex].DeepClone();
            }
        }
        response.Remove("requestRef");
        string resolved = response.ToString(Formatting.None);
        Require(NarrativeExactKeyContract.TryValidateProfileResponse(
            LocalLlmRequestProfiles.CharacterSkillModuleSelection.Id, resolved,
            out _, out string error), "Resolved Skill exact-key reject: " + error);
        return resolved;
    }

    internal static JObject CompactTable(JObject raw)
    {
        foreach (string key in new[] { "E_prefix", "P", "A", "H_columns", "H", "G", "V", "L_values" })
            Require(raw[key] == null, "Skill raw table conflicts with model catalog: " + key);
        JObject model = (JObject)raw.DeepClone();
        var primary = new List<string>();
        var allowed = new JArray();
        var headers = new JArray();
        var numbers = new List<string>();
        var vectors = new JArray();
        foreach (JArray row in (JArray)model["X"])
            row[2] = Intern(primary, (string)row[2]);
        ((JArray)model["X_columns"])[2] = "primary_P";
        foreach (JArray row in (JArray)model["C"])
            row[5] = Intern(allowed, row[5].DeepClone());
        ((JArray)model["C_columns"])[5] = "allowed_A";
        var outcomes = new JArray();
        foreach (JArray row in (JArray)raw["O"])
        {
            var header = new JArray(row[1].DeepClone(), row[2].DeepClone(), row[3].DeepClone(),
                row[4].DeepClone(), row[5].DeepClone(), row[6].DeepClone(), row[8].DeepClone(), row[9].DeepClone(), row[10].DeepClone());
            var vector = new JArray(((JArray)row[7]).OfType<JArray>().Select(metric =>
                new JArray((int)metric[0], Intern(numbers, (string)metric[1]))));
            outcomes.Add(new JArray((int)row[0], Intern(headers, header), Intern(vectors, vector)));
        }
        const string evidencePrefix = "public-fact:sha256:";
        string prefix = ((JArray)raw["E"]).Values<string>().All(value =>
            value.StartsWith(evidencePrefix, StringComparison.Ordinal)) ? evidencePrefix : string.Empty;
        model["E"] = new JArray(((JArray)raw["E"]).Values<string>().Select(value => value.Substring(prefix.Length)));
        model["E_prefix"] = prefix;
        model["P"] = new JArray(primary); model["A"] = allowed;
        model["O"] = outcomes; model["O_columns"] = new JArray("E", "H", "G");
        model["H_columns"] = new JArray("sourceKind", "selectable", "outcomeType", "subject_S", "days", "roles_R", "cause", "fact_T", "experience");
        model["H"] = headers; model["G"] = vectors; model["V"] = new JArray(numbers);
        return model;
    }

    internal static JObject ExpandTable(JObject compact)
    {
        JObject raw = (JObject)compact.DeepClone();
        foreach (JArray row in (JArray)raw["X"])
            row[2] = compact["P"][(int)row[2]].DeepClone();
        ((JArray)raw["X_columns"])[2] = "primaryContract";
        foreach (JArray row in (JArray)raw["C"])
            row[5] = compact["A"][(int)row[5]].DeepClone();
        ((JArray)raw["C_columns"])[5] = "allowed_E";
        var outcomes = new JArray();
        foreach (JArray row in (JArray)compact["O"])
        {
            JArray header = (JArray)compact["H"][(int)row[1]];
            var metrics = new JArray(((JArray)compact["G"][(int)row[2]]).OfType<JArray>().Select(metric =>
                new JArray((int)metric[0], (string)compact["V"][(int)metric[1]])));
            outcomes.Add(new JArray(row[0].DeepClone(), header[0].DeepClone(), header[1].DeepClone(),
                header[2].DeepClone(), header[3].DeepClone(), header[4].DeepClone(), header[5].DeepClone(),
                metrics, header[6].DeepClone(), header[7].DeepClone(), header[8].DeepClone()));
        }
        raw["O"] = outcomes;
        raw["O_columns"] = new JArray("E", "sourceKind", "selectable", "outcomeType", "subject_S", "days", "roles_R", "metrics_D_value", "cause", "fact_T", "experience");
        raw["E"] = new JArray(((JArray)compact["E"]).Values<string>().Select(value => (string)compact["E_prefix"] + value));
        foreach (string key in new[] { "E_prefix", "P", "A", "H_columns", "H", "G", "V", "L_values" }) raw.Remove(key);
        return raw;
    }

    internal static string CompactLocalFactLines(string suffix, out JArray catalog)
    {
        var values = new List<string>();
        string[] lines = suffix.Split('\n');
        for (int index = 0; index < lines.Length; index++)
        {
            var match = System.Text.RegularExpressions.Regex.Match(lines[index], "^(F[0-9]+) = (.*)$");
            if (match.Success) lines[index] = match.Groups[1].Value + " = @" + Intern(values, match.Groups[2].Value);
        }
        catalog = new JArray(values);
        return string.Join("\n", lines);
    }

    internal static string RestoreLocalFactLines(string suffix, JArray catalog)
    {
        return string.Join("\n", suffix.Split('\n').Select(line =>
        {
            var match = System.Text.RegularExpressions.Regex.Match(line, "^(F[0-9]+) = @([0-9]+)$");
            if (!match.Success) return line;
            int index = int.Parse(match.Groups[2].Value, System.Globalization.CultureInfo.InvariantCulture);
            Require(index >= 0 && index < catalog.Count, "Skill local fact index is outside its catalog.");
            return match.Groups[1].Value + " = " + (string)catalog[index];
        }));
    }

    internal static JObject CapturePacket(string packet)
    {
        string[] lines = packet.Replace("\r\n", "\n").TrimEnd('\n').Split('\n');
        var evidence = new List<string>();
        var modules = new JArray();
        var contexts = new JArray();
        var candidates = new JArray();
        var outcomes = new JArray();
        var subjects = new List<string>();
        var roles = new List<string>();
        var facts = new List<string>();
        var metrics = new JArray();
        int cursor = 0;
        string Take() { Require(cursor < lines.Length, "Skill packet ended early."); return lines[cursor++]; }
        string Value(string prefix)
        {
            string line = Take();
            Require(line.StartsWith(prefix, StringComparison.Ordinal), "Skill packet expected " + prefix);
            return line.Substring(prefix.Length);
        }
        Require(int.TryParse(Value("candidateCount="), System.Globalization.NumberStyles.Integer,
            System.Globalization.CultureInfo.InvariantCulture, out int count) && count is >= 1 and <= 3,
            "Skill packet candidate count is invalid.");
        for (int index = 0; index < count; index++)
        {
            Require(Take() == "candidate[" + index + "]:", "Skill candidate order changed.");
            string selection = Value("selectionId=");
            string mechanics = Take();
            Require(mechanics.StartsWith("skillKind=", StringComparison.Ordinal), "Skill context is missing.");
            string context = Value("skillContext=");
            string limit = Value("positiveModuleLimit=");
            string primary = Take();
            Require(primary.StartsWith("주효과 계약 v1: ", StringComparison.Ordinal), "Skill primary contract is missing.");
            Require(Take() == "positiveModules:", "Skill positive offers are missing.");
            JArray ReadModules(string terminator)
            {
                var result = new JArray();
                while (cursor < lines.Length && lines[cursor].StartsWith("- id=", StringComparison.Ordinal))
                {
                    string line = Take().Substring(5);
                    int split = line.IndexOf("; meaning=", StringComparison.Ordinal);
                    Require(split > 0, "Skill module meaning is missing.");
                    result.Add(Intern(modules, new JArray(line.Substring(0, split), line.Substring(split + 10))));
                }
                Require(Take() == terminator, "Skill module boundary changed.");
                return result;
            }
            JArray positive = ReadModules("drawbackModules:");
            var drawback = new JArray();
            bool emptyMarker = cursor < lines.Length && lines[cursor] == "- none (return [])";
            if (emptyMarker) cursor++;
            else while (cursor < lines.Length && lines[cursor].StartsWith("- id=", StringComparison.Ordinal))
            {
                string line = Take().Substring(5);
                int split = line.IndexOf("; meaning=", StringComparison.Ordinal);
                Require(split > 0, "Skill drawback meaning is missing.");
                drawback.Add(Intern(modules, new JArray(line.Substring(0, split), line.Substring(split + 10))));
            }
            string[] allowed = Value("evidenceFactIds=").Split(',');
            Require(positive.Count > 0 && allowed.All(value => !string.IsNullOrWhiteSpace(value))
                && allowed.Distinct(StringComparer.Ordinal).Count() == allowed.Length,
                "Skill packet has empty or duplicate offers.");
            candidates.Add(new JArray(selection, Intern(contexts, new JArray(mechanics, context, CompactInstruction(primary))),
                limit, positive, drawback, new JArray(allowed.Select(value => Intern(evidence, value))), emptyMarker));
        }
        Require(Take() == "sharedEvidenceFacts:" && Take() == "authoritativeOutcomeEvidence:",
            "Skill shared evidence boundary changed.");
        bool noOutcomes = cursor < lines.Length && lines[cursor] == "- none";
        if (noOutcomes) cursor++;
        else while (cursor < lines.Length)
        {
            string line = Take();
            Require(line.StartsWith("- id=", StringComparison.Ordinal), "Unknown skill outcome line.");
            string[] values = new string[11];
            string remainder = line.Substring(5);
            for (int field = 0; field < 7; field++)
            {
                int split = remainder.IndexOf(OutcomeSeparators[field], StringComparison.Ordinal);
                Require(split >= 0, "Skill outcome field is missing: " + OutcomeSeparators[field]);
                values[field] = remainder.Substring(0, split);
                remainder = remainder.Substring(split + OutcomeSeparators[field].Length);
            }
            int factStart = remainder.IndexOf(OutcomeSeparators[8], StringComparison.Ordinal);
            Require(factStart >= 0, "Skill outcome field is missing: " + OutcomeSeparators[8]);
            string metricAndCause = remainder.Substring(0, factStart);
            const string experienceSeparator = "; experience=";
            int experienceStart = metricAndCause.IndexOf(experienceSeparator, StringComparison.Ordinal);
            if (experienceStart >= 0)
            {
                values[10] = metricAndCause.Substring(experienceStart + experienceSeparator.Length);
                Require(!string.IsNullOrWhiteSpace(values[10]), "Skill experience assessment is empty.");
                metricAndCause = metricAndCause.Substring(0, experienceStart);
            }
            int causeStart = metricAndCause.IndexOf(OutcomeSeparators[7], StringComparison.Ordinal);
            values[7] = causeStart < 0 ? metricAndCause : metricAndCause.Substring(0, causeStart);
            // An absent public parent is not an empty or hidden parent ID.
            // Preserve absence through both table projections and exact replay.
            values[8] = causeStart < 0 ? null
                : metricAndCause.Substring(causeStart + OutcomeSeparators[7].Length);
            values[9] = remainder.Substring(factStart + OutcomeSeparators[8].Length);
            var metricValues = new JArray();
            if (values[7].Length > 0) foreach (string metric in values[7].Split(','))
            {
                int equals = metric.LastIndexOf('=');
                int space = metric.LastIndexOf(' ');
                Require(equals > 0 && space > equals + 1, "Skill metric is malformed.");
                string number = metric.Substring(equals + 1, space - equals - 1);
                Require(double.TryParse(number, System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out double parsed) && !double.IsNaN(parsed)
                    && !double.IsInfinity(parsed), "Skill metric is nonfinite.");
                metricValues.Add(new JArray(Intern(metrics, new JArray(metric.Substring(0, equals), metric.Substring(space + 1))), number));
            }
            outcomes.Add(new JArray(Intern(evidence, values[0]), values[1], values[2], values[3],
                Intern(subjects, values[4]), values[5], Intern(roles, values[6]), metricValues,
                values[8], Intern(facts, values[9]), values[10]));
        }
        Require(cursor == lines.Length, "Skill packet has trailing content.");
        return new JObject
        {
            ["C_columns"] = new JArray("selectionId", "X", "positiveModuleLimit", "positive_M", "drawback_M", "allowed_E", "emptyDrawbackMarker"),
            ["C"] = candidates, ["X_columns"] = new JArray("mechanics", "context", "primaryContract"),
            ["X"] = contexts, ["M"] = modules, ["E"] = new JArray(evidence),
            ["O_columns"] = new JArray("E", "sourceKind", "selectable", "outcomeType", "subject_S", "days", "roles_R", "metrics_D_value", "cause", "fact_T", "experience"),
            ["O"] = outcomes, ["S"] = new JArray(subjects), ["R"] = new JArray(roles),
            ["D_metricId_unit"] = metrics, ["T"] = new JArray(facts), ["noOutcomes"] = noOutcomes
        };
    }

    internal static string RestorePacket(JObject table)
    {
        var builder = new StringBuilder();
        void Line(string value) { builder.Append(value).Append('\n'); }
        JArray candidates = (JArray)table["C"];
        Line("candidateCount=" + candidates.Count);
        for (int index = 0; index < candidates.Count; index++)
        {
            JArray row = (JArray)candidates[index];
            JArray context = (JArray)table["X"][(int)row[1]];
            Line("candidate[" + index + "]:"); Line("selectionId=" + (string)row[0]);
            Line((string)context[0]); Line("skillContext=" + (string)context[1]);
            Line("positiveModuleLimit=" + (string)row[2]);
            string primary = (string)context[2];
            Line(Instructions.FirstOrDefault(pair => pair.Value == primary).Key ?? primary);
            void Modules(JArray indices) { foreach (int module in indices.Values<int>())
                Line("- id=" + (string)table["M"][module][0] + "; meaning=" + (string)table["M"][module][1]); }
            Line("positiveModules:"); Modules((JArray)row[3]); Line("drawbackModules:"); Modules((JArray)row[4]);
            if ((bool)row[6]) Line("- none (return [])");
            Line("evidenceFactIds=" + string.Join(",", ((JArray)row[5]).Values<int>().Select(value => (string)table["E"][value])));
        }
        Line("sharedEvidenceFacts:"); Line("authoritativeOutcomeEvidence:");
        if ((bool)table["noOutcomes"]) Line("- none");
        foreach (JArray row in (JArray)table["O"])
        {
            string metricText = string.Join(",", ((JArray)row[7]).OfType<JArray>().Select(value =>
                (string)table["D_metricId_unit"][(int)value[0]][0] + "=" + (string)value[1] + " "
                + (string)table["D_metricId_unit"][(int)value[0]][1]));
            string[] values = { (string)table["E"][(int)row[0]], (string)row[1], (string)row[2], (string)row[3],
                (string)table["S"][(int)row[4]], (string)row[5], (string)table["R"][(int)row[6]], metricText,
                (string)row[8], (string)table["T"][(int)row[9]] };
            string line = "- id=" + values[0];
            for (int field = 0; field < OutcomeSeparators.Length; field++)
            {
                if (field == 7 && values[8] == null) continue;
                if (field == 8 && row[10].Type != JTokenType.Null)
                    line += "; experience=" + (string)row[10];
                line += OutcomeSeparators[field] + values[field + 1];
            }
            Line(line);
        }
        return builder.ToString();
    }

    private static int Intern(List<string> values, string value)
    { int index = values.IndexOf(value); if (index >= 0) return index; values.Add(value); return values.Count - 1; }
    private static int Intern(JArray values, JToken value)
    { for (int index = 0; index < values.Count; index++) if (JToken.DeepEquals(values[index], value)) return index;
        values.Add(value); return values.Count - 1; }
    private static void Require(bool condition, string message)
    { if (!condition) throw new InvalidOperationException(message); }

    private static string CompactInstruction(string line)
    { return Instructions.TryGetValue(line, out string compact) ? compact : line; }
    private static string CompactInstructions(string text, bool useReferences = false)
    {
        return string.Join("\n", text.Split('\n').Select(line =>
        {
            const string namePrefix = "5. displayName 또는 narrativeFlavor에 캐릭터 이름 ";
            const string nameSuffix = "을 정확히 넣는다.";
            if (line.StartsWith(namePrefix, StringComparison.Ordinal) && line.EndsWith(nameSuffix, StringComparison.Ordinal))
                return "Include the exact character name "
                    + line.Substring(namePrefix.Length, line.Length - namePrefix.Length - nameSuffix.Length)
                    + " in displayName or narrativeFlavor.";
            string compact = CompactInstruction(line);
            if (useReferences && Instructions.ContainsKey(line))
                compact = compact.Replace("Output only candidates;", "Output only requestRef and candidates;")
                    .Replace("Return {\"candidates\":", "Return {\"requestRef\":\"exact supplied requestRef\",\"candidates\":");
            return compact;
        }));
    }
}
