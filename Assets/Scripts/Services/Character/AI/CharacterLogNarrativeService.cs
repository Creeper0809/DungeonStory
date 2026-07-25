using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;

public interface ICharacterLogNarrativeService
{
    bool TryBuildImmediateLine(CharacterLog characterLog, CharacterLogEntry entry, out string line);
    bool ShouldDeferDisplay(CharacterLogEntry entry);
    bool RequestNarrative(CharacterLog characterLog, CharacterLogEntry entry);
    bool TryApplyFallback(CharacterLog characterLog, CharacterLogEntry entry);
}

public sealed class CharacterLogNarrativeService : ICharacterLogNarrativeService
{
    public const int MaxLineCharacters = 60;
    private const int MaxPendingRequestsPerCharacter = 3;
    private const int MaxRecentLines = 3;
    private const int MaxCorrectionAttempts = 2;
    private static readonly HashSet<string> NarrativeStopWords = new HashSet<string>(
        StringComparer.OrdinalIgnoreCase)
    {
        "AI", "작업", "행동", "상태", "시작", "종료", "완료", "실패", "중단",
        "이용", "이동", "변경", "대기", "대응", "지정", "우선", "진행", "없음",
        "부족", "성공", "취소", "보류"
    };
    private static readonly string[] KoreanParticles =
    {
        "으로부터", "에게서", "에서는", "으로", "에서", "에게", "까지", "부터",
        "처럼", "보다", "의", "을", "를", "이", "가", "은", "는", "에", "와",
        "과", "로", "도", "만"
    };
    private static readonly HashSet<string> SafeNarrativeWords = new HashSet<string>(
        StringComparer.OrdinalIgnoreCase)
    {
        "그리고", "하지만", "다시", "바로", "곧바로", "차분히", "조용히", "천천히",
        "부지런히", "무사히", "마침내", "한동안", "잠시", "잠깐", "위해", "위해서", "따라",
        "함께", "스스로", "본격적으로", "차례로", "끝까지", "발걸음", "매듭"
    };
    private static readonly string[] SafeNarrativeVerbStems =
    {
        "시작", "나서", "나섰", "착수", "마치", "마무리", "끝내", "돌아오", "복귀",
        "정리", "이어", "향하", "들어가", "시도", "멈추", "포기", "못하", "실패",
        "부족", "모자", "찾", "도착", "완료", "향해", "향했", "옮기", "옮겼",
        "움직", "몰두",
        "매달", "힘쓰", "챙기", "살피", "준비", "끝맺", "매듭", "막히", "막혔",
        "막혀", "중단", "마쳤", "끝냈", "돌아왔", "들어갔", "멈췄", "챙겼",
        "살폈", "힘썼"
    };
    private static readonly string[] ForbiddenInventedConsequences =
    {
        "사망", "죽었", "죽어", "부상", "다쳤", "다치", "골드", "금화", "돈을",
        "획득", "얻었", "주웠", "잃었", "도난", "훔쳤", "파괴", "부쉈", "폭발",
        "화재", "고장", "레벨업", "승진", "해고", "처치", "살해", "실종", "체포"
    };
    private static readonly string[] SafeSituationWordStems =
    {
        "엉뚱", "방향", "길", "샜", "돌아", "되돌아", "슬쩍", "잘못", "틀", "당황",
        "기색", "의욕", "앞서", "허둥", "아무", "없", "태연", "척", "박자", "멈칫",
        "머쓱", "다시", "딴생각", "퍼뜩", "깨어", "뒤늦", "정신", "차리", "헤매",
        "이리저리", "갈팡질팡", "괜히"
    };
    private readonly ILocalLlmRuntimeProvider llmRuntimeProvider;
    private readonly Dictionary<int, int> pendingRequestsByLog = new Dictionary<int, int>();

    public CharacterLogNarrativeService(ILocalLlmRuntimeProvider llmRuntimeProvider)
    {
        this.llmRuntimeProvider = llmRuntimeProvider
            ?? throw new ArgumentNullException(nameof(llmRuntimeProvider));
    }

    public int RequestedCount { get; private set; }
    public int AppliedCount { get; private set; }
    public int ControlledFallbackCount { get; private set; }
    public int TemplateLineCount { get; private set; }
    public string LastPrompt { get; private set; } = string.Empty;
    public string LastResponse { get; private set; } = string.Empty;
    public string LastError { get; private set; } = string.Empty;

    public bool TryBuildImmediateLine(CharacterLog characterLog, CharacterLogEntry entry, out string line)
    {
        if (!Application.isPlaying)
        {
            line = string.Empty;
            return false;
        }

        if (!CharacterRecordTemplateBank.TryBuildLine(characterLog, entry, out line))
        {
            return false;
        }

        TemplateLineCount++;
        return true;
    }

    public bool ShouldDeferDisplay(CharacterLogEntry entry)
    {
        return Application.isPlaying && ShouldNarrate(entry);
    }

    public bool RequestNarrative(CharacterLog characterLog, CharacterLogEntry entry)
    {
        if (!Application.isPlaying
            || characterLog == null
            || !ShouldNarrate(entry)
            || !llmRuntimeProvider.TryGetRuntime(out ILocalLlmRuntime runtime))
        {
            return false;
        }

        int logId = characterLog.GetInstanceID();
        int pendingCount = GetPendingCount(logId);
        if (pendingCount >= MaxPendingRequestsPerCharacter)
        {
            LastError = "Character record request skipped because this character already has three pending rewrites.";
            return false;
        }

        CharacterActor actor = characterLog.GetComponent<CharacterActor>();
        string requiredSubject = BuildRequiredSubject(ResolveDisplayName(actor, characterLog));
        string prompt = BuildPrompt(actor, characterLog, entry, requiredSubject);
        LastPrompt = prompt;
        LastResponse = string.Empty;
        LastError = string.Empty;
        pendingRequestsByLog[logId] = pendingCount + 1;

        bool accepted = runtime.GenerateCharacterRecordAsync(
            prompt,
            entry.OriginalMessage,
            result => OnNarrativeResult(characterLog, logId, entry, requiredSubject, result, 0));
        if (!accepted)
        {
            DecrementPending(logId);
            LastError = "Character record request was not accepted by LocalLlmRequestQueue.";
            return false;
        }

        RequestedCount++;
        return true;
    }

    public static bool ShouldNarrate(CharacterLogEntry entry)
    {
        if (entry.EntryId <= 0
            || entry.Count != 1
            || entry.Activity == null
            || !entry.Activity.NarrativeEligible
            || !entry.Activity.VisibleToPlayer
            || string.IsNullOrWhiteSpace(entry.OriginalMessage))
        {
            return false;
        }

        return true;
    }

    public static string BuildPrompt(
        CharacterActor actor,
        CharacterLog characterLog,
        CharacterLogEntry entry)
    {
        string requiredSubject = BuildRequiredSubject(ResolveDisplayName(actor, characterLog));
        return BuildPrompt(actor, characterLog, entry, requiredSubject);
    }

    private static string BuildPrompt(
        CharacterActor actor,
        CharacterLog characterLog,
        CharacterLogEntry entry,
        string requiredSubject)
    {
        CustomerPersonaData persona = actor != null && actor.PersonaRuntime != null
            ? actor.PersonaRuntime.Persona
            : null;
        string name = ResolveDisplayName(actor, characterLog);
        string species = actor != null && !string.IsNullOrWhiteSpace(actor.SpeciesTag)
            ? actor.SpeciesTag
            : "unknown";
        string role = actor != null ? actor.Role.ToString() : "unknown";
        string trait = !string.IsNullOrWhiteSpace(persona != null ? persona.traitName : null)
            ? persona.traitName
            : "unknown";
        string flavor = !string.IsNullOrWhiteSpace(persona != null ? persona.flavorText : null)
            ? persona.flavorText
            : "unknown";
        int styleSeed = Math.Abs(entry.EntryId % 4);
        string sentenceShape = GetPreferredSentenceShape(entry.OriginalMessage, styleSeed);
        string styleBrief = GetStyleBrief(styleSeed);

        StringBuilder builder = new StringBuilder(1200);
        builder.AppendLine("Rewrite one DungeonStory character activity record in natural Korean.");
        builder.AppendLine("Return exactly one compact JSON object: {\"line\":\"...\"}. No markdown and no extra keys.");
        builder.AppendLine($"Write one compact third-person micro-story between 32 and {MaxLineCharacters} Korean characters.");
        builder.AppendLine("Keep every concrete action, place, target, success, failure, and result from the source event.");
        builder.AppendLine("Add exactly one small, harmless situation around the event so it reads like an anecdote, not a task summary.");
        builder.AppendLine("You may invent only the character's fleeting behavior, minor near-misstep, quirky reaction, or light comic beat.");
        builder.AppendLine("Build a tiny event-and-reaction beat: something happens, the character reacts, then the source action continues.");
        builder.AppendLine("Use one short situation clause and one core event clause. Do not chain extra explanations.");
        builder.AppendLine("A lone mood adverb, a pleasant expression, or merely deciding/intending to act does not count as a situation.");
        builder.AppendLine("Do not invent any new prop, decoration, sound source, smell source, customer, or facility feature.");
        builder.AppendLine("Let the persona shape that moment, and respect the species anatomy; do not assume incompatible clothes or body parts.");
        builder.AppendLine("Do not invent gameplay consequences: rewards, losses, injuries, damage, combat, permanent changes, new numbers, or named people.");
        builder.AppendLine("Do not copy the source verbatim. Vary the sentence structure and verb while preserving its meaning.");
        builder.AppendLine("Make the line feel individually written, not filled from one repeated template.");
        builder.AppendLine("Avoid reusing the cadence of recent records, especially the default '~에서 ~을 시작했다' pattern.");
        builder.AppendLine("Use every requiredAnchorWord exactly somewhere in the Korean sentence. Do not replace it with a synonym.");
        builder.AppendLine("The persona may influence rhythm and word choice only; it must not add facts.");
        builder.AppendLine($"The sentence MUST begin exactly with this requiredSubject: {requiredSubject}");
        builder.AppendLine("Use requiredSubject once as the grammatical subject, including its supplied 이/가 particle.");
        builder.AppendLine("Do not omit or alter the character name. Do not mention species or role in the record sentence.");
        builder.AppendLine("Use a lively but restrained game-diary tone, not a system label and not first-person speech.");
        builder.AppendLine("Use plain in-world Korean without internet slang, memes, or abstract filler.");
        builder.AppendLine("Prefer natural Korean narrative endings such as -했다, -나섰다, -마무리했다, or -돌아왔다.");
        builder.AppendLine("Avoid formal report endings such as -했습니다 and -되었습니다.");
        builder.AppendLine("Avoid generic stiff filler such as 업무, 활동, 자신의, 수행, and 진행되었습니다.");
        builder.AppendLine($"styleSeed: {styleSeed}");
        builder.AppendLine($"styleBrief: {styleBrief}");
        builder.AppendLine($"situationBrief: {GetSituationBrief(styleSeed)}");
        builder.AppendLine($"sentenceShape: {sentenceShape}");
        builder.AppendLine("Character:");
        builder.AppendLine($"name: {name}");
        builder.AppendLine($"requiredSubject: {requiredSubject}");
        builder.AppendLine($"species: {species}");
        builder.AppendLine($"role: {role}");
        builder.AppendLine($"personaTrait: {trait}");
        builder.AppendLine($"personaFlavor: {flavor}");
        builder.AppendLine($"eventKind: {entry.Activity?.KindId}");
        builder.AppendLine($"actionId: {entry.Activity?.ActionId}");
        builder.AppendLine($"target: {entry.Activity?.TargetName}");
        builder.AppendLine($"place: {entry.Activity?.PlaceName}");
        builder.AppendLine($"outcome: {entry.Activity?.OutcomeId}");
        builder.AppendLine($"reasonCode: {entry.Activity?.ReasonCode}");
        builder.AppendLine($"value: {entry.Activity?.Value:0.###}");
        builder.AppendLine($"quantity: {entry.Activity?.Quantity}");
        builder.AppendLine("Source event:");
        builder.AppendLine($"tag: {entry.Tag}");
        builder.AppendLine($"message: {entry.OriginalMessage}");
        builder.AppendLine($"requiredAnchorWords: {string.Join(", ", ExtractRequiredAnchors(entry.OriginalMessage))}");
        builder.AppendLine("Recent visible records to avoid repeating their phrasing:");
        AppendRecentLines(builder, characterLog, entry.DisplayLine);
        return builder.ToString();
    }

    public static bool TryParseNarrativeLine(
        string response,
        string originalLine,
        out string line,
        out string error)
    {
        return TryParseNarrativeLine(response, originalLine, string.Empty, out line, out error);
    }

    public static bool TryParseNarrativeLine(
        string response,
        string originalLine,
        string requiredSubject,
        out string line,
        out string error)
    {
        line = string.Empty;
        if (!LlmJsonResponseParser.TryParse(
                response,
                out CharacterRecordJsonDto dto,
                out error))
        {
            return false;
        }

        string normalized = Regex.Replace(dto.line.Trim(), @"\s+", " ")
            .Trim('-', '*', '•', '"', '\'', ' ');
        if (normalized.Length == 0)
        {
            error = "Character record line is empty.";
            return false;
        }

        if (!ContainsHangul(normalized))
        {
            error = "Character record line must contain Korean text.";
            return false;
        }

        if (!string.IsNullOrWhiteSpace(requiredSubject))
        {
            string subjectPrefix = requiredSubject.Trim() + " ";
            if (!normalized.StartsWith(subjectPrefix, StringComparison.Ordinal))
            {
                error = $"Character record line must begin with the exact subject: {requiredSubject}.";
                return false;
            }

            if (normalized.IndexOf(requiredSubject, subjectPrefix.Length, StringComparison.Ordinal) >= 0)
            {
                error = $"Character record line repeated the required subject: {requiredSubject}.";
                return false;
            }

            string subjectName = requiredSubject.Trim();
            subjectName = subjectName.Length > 1
                ? subjectName.Substring(0, subjectName.Length - 1)
                : string.Empty;
            if (subjectName.Length >= 2
                && normalized.IndexOf(subjectName, subjectPrefix.Length, StringComparison.Ordinal) >= 0)
            {
                error = $"Character record line repeated the character name: {subjectName}.";
                return false;
            }
        }

        int internalSentenceEnd = normalized.IndexOfAny(new[] { '.', '!', '?' });
        if (internalSentenceEnd >= 0 && internalSentenceEnd != normalized.Length - 1)
        {
            error = "Character record line must be exactly one sentence.";
            return false;
        }

        if (normalized.Contains(" · ", StringComparison.Ordinal)
            || ContainsAny(
                normalized,
                "습니다",
                "합니다",
                "됩니다",
                "입니다",
                "드립니다",
                "뇌피셜",
                "흔쾌한 생각",
                "안되자마자",
                "그것을"))
        {
            error = "Character record line used a rigid system-label or formal report style.";
            return false;
        }

        if (string.Equals(normalized, originalLine?.Trim(), StringComparison.Ordinal))
        {
            error = "Character record line was not rewritten.";
            return false;
        }

        if (!PreservesSourceMeaning(normalized, originalLine, out error))
        {
            return false;
        }

        if (!ContainsNoInventedConsequences(normalized, originalLine, out error))
        {
            return false;
        }

        if (!ContainsCreativeDetail(normalized, originalLine, requiredSubject, out error))
        {
            return false;
        }

        char last = normalized[normalized.Length - 1];
        if (last != '.' && last != '!' && last != '?')
        {
            normalized += ".";
        }

        if (normalized.Length > MaxLineCharacters)
        {
            error = $"Character record line must be {MaxLineCharacters} characters or shorter.";
            return false;
        }

        line = normalized;
        error = string.Empty;
        return true;
    }

    private void OnNarrativeResult(
        CharacterLog characterLog,
        int logId,
        CharacterLogEntry entry,
        string requiredSubject,
        LocalLlmResult result,
        int correctionAttempt)
    {
        DecrementPending(logId);
        LastResponse = result.Content;
        if (result.IsCancelled)
        {
            LastError = string.Empty;
            return;
        }

        if (!result.IsSuccess)
        {
            LastError = $"{result.Status}: {result.Error}";
            TryApplyControlledFallback(characterLog, entry, requiredSubject);
            return;
        }

        if (!TryParseNarrativeLine(
                result.Content,
                entry.OriginalMessage,
                requiredSubject,
                out string line,
                out string error))
        {
            if (correctionAttempt < MaxCorrectionAttempts
                && TryRequestCorrection(
                    characterLog,
                    logId,
                    entry,
                    requiredSubject,
                    result.Content,
                    error,
                    correctionAttempt + 1))
            {
                return;
            }

            if (TryApplyControlledFallback(characterLog, entry, requiredSubject))
            {
                return;
            }

            LastError = error;
            return;
        }

        string fallbackLine = BuildControlledFallbackLine(entry, requiredSubject);
        if (characterLog == null
            || !characterLog.TryUpdateDisplayLine(
                entry.EntryId,
                entry.DisplayLine,
                fallbackLine,
                line))
        {
            LastError = "Character record response became stale before it could be applied.";
            return;
        }

        AppliedCount++;
        LastError = string.Empty;
    }

    private bool TryRequestCorrection(
        CharacterLog characterLog,
        int logId,
        CharacterLogEntry entry,
        string requiredSubject,
        string rejectedResponse,
        string rejectionReason,
        int correctionAttempt)
    {
        if (characterLog == null
            || !llmRuntimeProvider.TryGetRuntime(out ILocalLlmRuntime runtime))
        {
            return false;
        }

        CharacterActor actor = characterLog.GetComponent<CharacterActor>();
        string prompt = BuildCorrectionPrompt(
            actor,
            entry,
            requiredSubject,
            rejectedResponse,
            rejectionReason);
        LastPrompt = prompt;
        LastError = $"Retrying rejected character record: {rejectionReason}";
        pendingRequestsByLog[logId] = GetPendingCount(logId) + 1;
        bool accepted = runtime.GenerateCharacterRecordAsync(
            prompt,
            entry.OriginalMessage,
            result => OnNarrativeResult(
                characterLog,
                logId,
                entry,
                requiredSubject,
                result,
                correctionAttempt));
        if (!accepted)
        {
            DecrementPending(logId);
            return false;
        }

        RequestedCount++;
        return true;
    }

    public static string BuildCorrectionPrompt(
        CharacterLogEntry entry,
        string rejectedResponse,
        string rejectionReason)
    {
        return BuildCorrectionPrompt(entry, string.Empty, rejectedResponse, rejectionReason);
    }

    public static string BuildCorrectionPrompt(
        CharacterLogEntry entry,
        string requiredSubject,
        string rejectedResponse,
        string rejectionReason)
    {
        return BuildCorrectionPrompt(
            null,
            entry,
            requiredSubject,
            rejectedResponse,
            rejectionReason);
    }

    private static string BuildCorrectionPrompt(
        CharacterActor actor,
        CharacterLogEntry entry,
        string requiredSubject,
        string rejectedResponse,
        string rejectionReason)
    {
        CustomerPersonaData persona = actor != null && actor.PersonaRuntime != null
            ? actor.PersonaRuntime.Persona
            : null;
        IReadOnlyList<string> phrases = ExtractRequiredPhrases(entry.OriginalMessage);
        int styleSeed = Math.Abs(entry.EntryId % 4);
        string sentenceShape = GetPreferredSentenceShape(entry.OriginalMessage, styleSeed);
        string groundedExample = BuildGroundedSentenceExample(
            entry.OriginalMessage,
            phrases,
            requiredSubject,
            styleSeed);

        StringBuilder builder = new StringBuilder(800);
        builder.AppendLine("The previous Korean activity record was rejected.");
        builder.AppendLine("Return exactly one compact JSON object: {\"line\":\"...\"}. No markdown or extra keys.");
        builder.AppendLine("Write exactly one natural Korean game-diary sentence in plain -했다 style.");
        builder.AppendLine($"Rejection reason: {rejectionReason}");
        builder.AppendLine($"Source event: {entry.OriginalMessage}");
        builder.AppendLine($"Required exact phrases: {string.Join(" / ", phrases)}");
        builder.AppendLine("The sentence MUST contain every required exact phrase unchanged.");
        if (!string.IsNullOrWhiteSpace(requiredSubject))
        {
            builder.AppendLine($"The sentence MUST begin exactly with this subject: {requiredSubject}");
            builder.AppendLine("Keep that name and its 이/가 particle unchanged and use the subject once.");
        }
        builder.AppendLine("Do not use bullets, middle dots, labels, commas with a trailing place, or formal -습니다 endings.");
        builder.AppendLine("Do not use internet slang, memes, or vague filler such as 뇌피셜 or 흔쾌한 생각.");
        builder.AppendLine("Add exactly one harmless micro-situation from the character's own behavior: a near-misstep, quirky reaction, or comic recovery.");
        builder.AppendLine("It must include a small event and reaction before or during the source action; one emotion adjective is not enough.");
        builder.AppendLine($"Keep the complete line at or below {MaxLineCharacters} Korean characters with only one setup clause.");
        builder.AppendLine("Do not turn a source start into a plan, decision, intention, or future action.");
        builder.AppendLine("Do not invent a prop, decoration, music, smell source, customer, or new facility feature.");
        builder.AppendLine("Do not add rewards, losses, injuries, damage, combat, permanent changes, numbers, or named people.");
        builder.AppendLine($"Species: {(actor != null ? actor.SpeciesTag : "unknown")}");
        builder.AppendLine($"Persona trait: {(persona != null ? persona.traitName : "unknown")}");
        builder.AppendLine($"Persona flavor: {(persona != null ? persona.flavorText : "unknown")}");
        builder.AppendLine($"Keep this rhythm and verb family: {GetStyleBrief(styleSeed)}");
        builder.AppendLine($"Create this kind of harmless moment: {GetSituationBrief(styleSeed)}");
        builder.AppendLine($"Use this sentence shape with the source phrases: {sentenceShape}");
        if (!string.IsNullOrWhiteSpace(groundedExample))
        {
            builder.AppendLine($"Grounded base only; add the required harmless moment and do not return it unchanged: {groundedExample}");
        }
        builder.AppendLine($"Rejected response: {rejectedResponse}");
        return builder.ToString();
    }

    public static string BuildControlledFallbackLine(
        CharacterLogEntry entry,
        string requiredSubject)
    {
        IReadOnlyList<string> phrases = ExtractRequiredPhrases(entry.OriginalMessage);
        return BuildGroundedSentenceExample(
            entry.OriginalMessage,
            phrases,
            requiredSubject,
            entry.EntryId % 4);
    }

    public bool TryApplyFallback(CharacterLog characterLog, CharacterLogEntry entry)
    {
        CharacterActor actor = characterLog != null
            ? characterLog.GetComponent<CharacterActor>()
            : null;
        string requiredSubject = BuildRequiredSubject(ResolveDisplayName(actor, characterLog));
        return TryApplyControlledFallback(characterLog, entry, requiredSubject);
    }

    private bool TryApplyControlledFallback(
        CharacterLog characterLog,
        CharacterLogEntry entry,
        string requiredSubject)
    {
        string fallback = BuildControlledFallbackLine(entry, requiredSubject);
        if (string.IsNullOrWhiteSpace(fallback))
        {
            return false;
        }

        string json = JsonUtility.ToJson(new CharacterRecordJsonDto { line = fallback });
        if (!TryParseNarrativeLine(
                json,
                entry.OriginalMessage,
                requiredSubject,
                out string line,
                out _)
            || characterLog == null
            || !characterLog.TryUpdateDisplayLine(entry.EntryId, entry.DisplayLine, line))
        {
            return false;
        }

        AppliedCount++;
        ControlledFallbackCount++;
        LastError = string.Empty;
        return true;
    }

    private static void AppendRecentLines(
        StringBuilder builder,
        CharacterLog characterLog,
        string currentLine)
    {
        IReadOnlyList<string> entries = characterLog != null ? characterLog.Entries : null;
        int written = 0;
        if (entries != null)
        {
            for (int i = entries.Count - 1; i >= 0 && written < MaxRecentLines; i--)
            {
                string candidate = entries[i];
                if (string.IsNullOrWhiteSpace(candidate)
                    || string.Equals(candidate, currentLine, StringComparison.Ordinal))
                {
                    continue;
                }

                builder.AppendLine($"- {candidate}");
                written++;
            }
        }

        if (written == 0)
        {
            builder.AppendLine("- none");
        }
    }

    private static bool ContainsHangul(string value)
    {
        for (int i = 0; i < value.Length; i++)
        {
            if (value[i] >= '\uAC00' && value[i] <= '\uD7A3')
            {
                return true;
            }
        }

        return false;
    }

    public static IReadOnlyList<string> ExtractRequiredAnchors(string source)
    {
        List<string> anchors = new List<string>();
        if (string.IsNullOrWhiteSpace(source))
        {
            return anchors;
        }

        string[] tokens = Regex.Split(source, @"[\s·:;,/\\()\[\]{}<>→-]+");
        HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < tokens.Length; i++)
        {
            string token = Regex.Replace(tokens[i], @"[^\p{L}\p{N}_]", string.Empty);
            token = StripKoreanParticle(token);
            if (token.Length < 2 || NarrativeStopWords.Contains(token) || !seen.Add(token))
            {
                continue;
            }

            anchors.Add(token);
        }

        return anchors;
    }

    public static IReadOnlyList<string> ExtractRequiredPhrases(string source)
    {
        List<string> phrases = new List<string>();
        if (string.IsNullOrWhiteSpace(source))
        {
            return phrases;
        }

        string[] segments = Regex.Split(source, @"[·:;→]+");
        for (int i = 0; i < segments.Length; i++)
        {
            string phrase = Regex.Replace(segments[i].Trim(), @"\s+", " ");
            phrase = Regex.Replace(
                phrase,
                @"^(작업|행동)\s+(시작|종료|완료|실패)$",
                string.Empty);
            phrase = Regex.Replace(
                phrase,
                @"\s+(시작|종료|완료|실패|중단|취소)$",
                string.Empty);
            if (phrase.Length < 2 || NarrativeStopWords.Contains(phrase) || phrases.Contains(phrase))
            {
                continue;
            }

            phrases.Add(phrase);
        }

        if (phrases.Count == 0)
        {
            phrases.AddRange(ExtractRequiredAnchors(source));
        }

        return phrases;
    }

    private static bool PreservesSourceMeaning(string line, string source, out string error)
    {
        IReadOnlyList<string> phrases = ExtractRequiredPhrases(source);
        for (int i = 0; i < phrases.Count; i++)
        {
            if (line.IndexOf(phrases[i], StringComparison.OrdinalIgnoreCase) >= 0)
            {
                continue;
            }

            error = $"Character record line dropped required source phrase: {phrases[i]}.";
            return false;
        }

        IReadOnlyList<string> anchors = ExtractRequiredAnchors(source);
        for (int i = 0; i < anchors.Count; i++)
        {
            if (line.IndexOf(anchors[i], StringComparison.OrdinalIgnoreCase) >= 0)
            {
                continue;
            }

            error = $"Character record line dropped required source anchor: {anchors[i]}.";
            return false;
        }

        if (ContainsAny(source, "시작", "출발", "착수")
            && !ContainsAny(line, "시작", "출발", "착수", "나섰", "들어갔"))
        {
            error = "Character record line dropped the source start transition.";
            return false;
        }

        if (ContainsAny(source, "시작", "출발", "착수")
            && ContainsAny(
                line,
                "하기로",
                "하려고",
                "할 예정",
                "마음 먹",
                "마음먹",
                "결심했",
                "생각했"))
        {
            error = "Character record line weakened an actual start into a plan or intention.";
            return false;
        }

        if (ContainsAny(source, "종료", "완료", "복귀", "마침")
            && !ContainsAny(line, "종료", "완료", "복귀", "마쳤", "마무리", "끝냈", "끝내", "돌아왔"))
        {
            error = "Character record line dropped the source completion transition.";
            return false;
        }

        if (ContainsAny(source, "실패", "없음", "부족", "중단", "취소")
            && !ContainsAny(line, "실패", "없", "부족", "모자", "못", "막혀", "중단", "취소", "포기"))
        {
            error = "Character record line dropped the source failure transition.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    public static bool HasCreativeDetail(
        string line,
        string source,
        string requiredSubject)
    {
        return ContainsCreativeDetail(line, source, requiredSubject, out _);
    }

    private static bool ContainsCreativeDetail(
        string line,
        string source,
        string requiredSubject,
        out string error)
    {
        int creativeTokenCount = 0;
        string[] tokens = Regex.Split(line, @"[\s·:;,/\\()\[\]{}<>→-]+");
        for (int i = 0; i < tokens.Length; i++)
        {
            string token = Regex.Replace(tokens[i], @"[^\p{L}\p{N}_]", string.Empty);
            token = StripKoreanParticle(token);
            if (token.Length < 2
                || source.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0
                || (!string.IsNullOrWhiteSpace(requiredSubject)
                    && requiredSubject.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0)
                || NarrativeStopWords.Contains(token)
                || SafeNarrativeWords.Contains(token)
                || StartsWithAny(token, SafeNarrativeVerbStems)
                || (source.Contains("새", StringComparison.Ordinal) && token.StartsWith("새", StringComparison.Ordinal)))
            {
                continue;
            }

            if (!StartsWithAny(token, SafeSituationWordStems))
            {
                error = $"Character record line invented an unrelated situation detail: {token}.";
                return false;
            }

            creativeTokenCount++;
        }

        bool hasEventReactionBeat = ContainsAny(
            line,
            "다가",
            "더니",
            "지만",
            "는데",
            "순간",
            "바람에",
            "탓에",
            " 뒤",
            " 후",
            " 때",
            "듯",
            " 채",
            "하며",
            "며 ",
            "면서",
            "자마자",
            "깨어나");
        bool hasHarmlessSituationAnchor = ContainsAny(
            line,
            "엉뚱",
            "되돌아",
            "허둥",
            "멈칫",
            "아무 일 없",
            "딴생각",
            "정신을 차",
            "착각",
            "우왕좌왕",
            "머쓱",
            "태연",
            "한 박자",
            "뒤늦게",
            "슬쩍",
            "괜히",
            "헤매",
            "이리저리",
            "갈팡질팡");
        if (creativeTokenCount >= 2 && hasEventReactionBeat && hasHarmlessSituationAnchor)
        {
            error = string.Empty;
            return true;
        }

        error = "Character record line needs a concrete harmless event-and-reaction beat beyond the source facts.";
        return false;
    }

    private static bool ContainsNoInventedConsequences(string line, string source, out string error)
    {
        for (int i = 0; i < ForbiddenInventedConsequences.Length; i++)
        {
            string consequence = ForbiddenInventedConsequences[i];
            if (line.IndexOf(consequence, StringComparison.OrdinalIgnoreCase) < 0
                || source.IndexOf(consequence, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                continue;
            }

            error = $"Character record line invented a gameplay consequence: {consequence}.";
            return false;
        }

        MatchCollection numbers = Regex.Matches(line, @"\d+(?:[.,]\d+)?");
        for (int i = 0; i < numbers.Count; i++)
        {
            if (source.IndexOf(numbers[i].Value, StringComparison.Ordinal) >= 0)
            {
                continue;
            }

            error = $"Character record line invented a numeric fact: {numbers[i].Value}.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    private static string StripKoreanParticle(string token)
    {
        for (int i = 0; i < KoreanParticles.Length; i++)
        {
            string particle = KoreanParticles[i];
            if (token.EndsWith(particle, StringComparison.Ordinal)
                && token.Length - particle.Length >= 2)
            {
                return token.Substring(0, token.Length - particle.Length);
            }
        }

        return token;
    }

    private static bool ContainsAny(string value, params string[] patterns)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        for (int i = 0; i < patterns.Length; i++)
        {
            if (value.IndexOf(patterns[i], StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }
        }

        return false;
    }

    private static bool StartsWithAny(string value, params string[] prefixes)
    {
        for (int i = 0; i < prefixes.Length; i++)
        {
            if (value.StartsWith(prefixes[i], StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static string GetPreferredSentenceShape(string source, int styleSeed)
    {
        int variant = Math.Abs(styleSeed % 4);
        if (!string.IsNullOrWhiteSpace(source)
            && source.StartsWith("비번 시작", StringComparison.Ordinal))
        {
            return "<subject> <reason>으로 비번을 시작했다.";
        }

        if (ContainsAny(source, "종료", "완료", "복귀", "마침"))
        {
            return variant switch
            {
                0 => "<subject> <place>에서 <work>를 마치고 <result>도 끝냈다.",
                1 => "<subject> <place>에서 <work>와 <result>를 마쳤다.",
                2 => "<subject> <place>에서 <result>를 끝내고 <work>도 마쳤다.",
                _ => "<subject> <place>에서 <work>와 <result>를 마쳤다."
            };
        }

        if (ContainsAny(source, "실패", "없음", "부족", "중단", "취소"))
        {
            return variant switch
            {
                0 => "<subject> <place>에서 <action>을 시도했지만 <reason>으로 멈췄다.",
                1 => "<subject> <action>에 나섰지만 <place>에서 <reason>에 막혔다.",
                2 => "<subject> <place>에서 <reason>에 막혀 <action>을 멈췄다.",
                _ => "<subject> <action>을 이어가려 했지만 <place>에서 <reason>으로 중단했다."
            };
        }

        if (ContainsAny(source, "시작", "출발", "착수"))
        {
            return variant switch
            {
                0 => "<subject> <place>에서 <action>을 시작했다.",
                1 => "<subject> <place>에서 <action>에 착수했다.",
                2 => "<subject> <place>로 향해 <action>에 착수했다.",
                _ => "<subject> <place>에서 <action>을 시작했다."
            };
        }

        return Math.Abs(styleSeed % 4) switch
        {
            0 => "Begin with <subject>, then state the concrete action and place.",
            1 => "Begin with <subject>, then move from the place into the concrete action.",
            2 => "Begin with <subject>, then state the concrete result and target.",
            _ => "Begin with <subject>, then use a compact transition centered on what changed."
        };
    }

    private static string BuildGroundedSentenceExample(
        string source,
        IReadOnlyList<string> phrases,
        string requiredSubject,
        int styleSeed)
    {
        if (phrases == null || phrases.Count == 0)
        {
            return string.Empty;
        }

        string storySubject = PrefixSubject(requiredSubject, GetGroundedSituationLead(styleSeed));

        if (!string.IsNullOrWhiteSpace(source)
            && source.StartsWith("비번 시작", StringComparison.Ordinal)
            && phrases.Count >= 2)
        {
            return PrefixSubject(
                storySubject,
                $"{WithDirectionalParticle(phrases[1])} {WithObjectParticle(phrases[0])} 시작했다.");
        }

        if (ContainsAny(source, "종료", "완료", "복귀", "마침") && phrases.Count >= 2)
        {
            if (phrases.Count >= 3)
            {
                string work = WithObjectParticle(phrases[0]);
                string result = WithObjectParticle(phrases[2]);
                return Math.Abs(styleSeed % 4) switch
                {
                    0 => PrefixSubject(
                        storySubject,
                        $"{phrases[1]}에서 {work} 마치고 {phrases[2]}도 끝냈다."),
                    1 => PrefixSubject(
                        storySubject,
                        $"{phrases[1]}에서 {WithConjunctionParticle(phrases[0])} {result} 마쳤다."),
                    2 => PrefixSubject(
                        storySubject,
                        $"{phrases[1]}에서 {result} 끝내고 {phrases[0]}도 마쳤다."),
                    _ => PrefixSubject(
                        storySubject,
                        $"{phrases[1]}에서 {WithConjunctionParticle(phrases[0])} {result} 마쳤다.")
                };
            }

            return PrefixSubject(
                storySubject,
                $"{phrases[1]}에서 {WithObjectParticle(phrases[0])} 마무리했다.");
        }

        if (ContainsAny(source, "시작", "출발", "착수") && phrases.Count >= 2)
        {
            string action = WithObjectParticle(phrases[0]);
            return Math.Abs(styleSeed % 4) switch
            {
                0 => PrefixSubject(storySubject, $"{phrases[1]}에서 {action} 시작했다."),
                1 => PrefixSubject(storySubject, $"{phrases[1]}에서 {phrases[0]}에 착수했다."),
                2 => PrefixSubject(storySubject, $"{WithDirectionalParticle(phrases[1])} 향해 {phrases[0]}에 착수했다."),
                _ => PrefixSubject(storySubject, $"{phrases[1]}에서 {action} 시작했다.")
            };
        }

        return string.Empty;
    }

    private static string GetStyleBrief(int styleSeed)
    {
        return Math.Abs(styleSeed % 4) switch
        {
            0 => "담백한 현장 기록: 장소에서 행동으로 이어지는 짧고 또렷한 문장.",
            1 => "행동 중심 기록: 행동을 먼저 꺼내고 착수·끝냄을 경쾌하게 표현.",
            2 => "이동감 있는 기록: 향하다·들어가다·마무리하다를 자연스럽게 연결.",
            _ => "결과 중심 기록: 결과나 변화를 먼저 세우고 마지막에 장소와 행동을 매듭."
        };
    }

    private static string GetSituationBrief(int styleSeed)
    {
        return Math.Abs(styleSeed % 4) switch
        {
            0 => "엉뚱한 길로 샜다가 금세 돌아오는 실수.",
            1 => "잠깐 허둥댄 뒤 태연한 척하는 버릇.",
            2 => "한 박자 멈칫했다가 머쓱하게 다시 나서는 반응.",
            _ => "딴생각에서 퍼뜩 깨어나 원래 행동을 잇는 반전."
        };
    }

    private static string GetGroundedSituationLead(int styleSeed)
    {
        return Math.Abs(styleSeed % 4) switch
        {
            0 => "엉뚱한 길로 샜다가 돌아와",
            1 => "잠깐 허둥댄 뒤 태연한 척하고",
            2 => "한 박자 멈칫했다가 머쓱하게",
            _ => "딴생각에서 퍼뜩 깨어나"
        };
    }

    public static string BuildRequiredSubject(string displayName)
    {
        string normalizedName = Regex.Replace(displayName?.Trim() ?? string.Empty, @"\s+", " ");
        if (normalizedName.Length == 0)
        {
            return string.Empty;
        }

        char last = normalizedName[normalizedName.Length - 1];
        return normalizedName + (HasFinalConsonant(last) ? "이" : "가");
    }

    private static string ResolveDisplayName(CharacterActor actor, CharacterLog characterLog)
    {
        CharacterIdentity identity = actor != null ? actor.Identity : null;
        if (!string.IsNullOrWhiteSpace(identity != null ? identity.DisplayName : null))
        {
            return identity.DisplayName;
        }

        return actor != null
            ? actor.name
            : characterLog != null ? characterLog.name : "unknown";
    }

    private static string PrefixSubject(string requiredSubject, string sentence)
    {
        return string.IsNullOrWhiteSpace(requiredSubject)
            ? sentence
            : $"{requiredSubject} {sentence}";
    }

    private static string WithObjectParticle(string phrase)
    {
        return AppendParticle(phrase, "을", "를");
    }

    private static string WithConjunctionParticle(string phrase)
    {
        return AppendParticle(phrase, "과", "와");
    }

    private static string WithDirectionalParticle(string phrase)
    {
        if (string.IsNullOrWhiteSpace(phrase))
        {
            return phrase ?? string.Empty;
        }

        string trimmed = phrase.Trim();
        char last = trimmed[trimmed.Length - 1];
        bool rieulFinal = last >= '\uAC00'
            && last <= '\uD7A3'
            && (last - '\uAC00') % 28 == 8;
        return trimmed + (HasFinalConsonant(last) && !rieulFinal ? "으로" : "로");
    }

    private static string AppendParticle(string phrase, string consonantParticle, string vowelParticle)
    {
        if (string.IsNullOrWhiteSpace(phrase))
        {
            return phrase ?? string.Empty;
        }

        string trimmed = phrase.Trim();
        return trimmed + (HasFinalConsonant(trimmed[trimmed.Length - 1])
            ? consonantParticle
            : vowelParticle);
    }

    private static bool HasFinalConsonant(char value)
    {
        return value >= '\uAC00'
            && value <= '\uD7A3'
            && (value - '\uAC00') % 28 != 0;
    }

    private int GetPendingCount(int logId)
    {
        return pendingRequestsByLog.TryGetValue(logId, out int count) ? count : 0;
    }

    private void DecrementPending(int logId)
    {
        int remaining = GetPendingCount(logId) - 1;
        if (remaining > 0)
        {
            pendingRequestsByLog[logId] = remaining;
        }
        else
        {
            pendingRequestsByLog.Remove(logId);
        }
    }
}

[Serializable]
public sealed class CharacterRecordJsonDto : ILlmJsonPayload
{
    public string line;

    public bool Validate(out string error)
    {
        error = string.Empty;
        if (string.IsNullOrWhiteSpace(line))
        {
            error = "line is required.";
            return false;
        }

        if (line.Length > CharacterLogNarrativeService.MaxLineCharacters)
        {
            error = $"line must be {CharacterLogNarrativeService.MaxLineCharacters} characters or shorter.";
            return false;
        }

        return true;
    }
}
