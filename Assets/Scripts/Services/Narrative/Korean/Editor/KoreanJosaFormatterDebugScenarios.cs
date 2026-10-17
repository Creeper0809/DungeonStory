using System;
using System.Text;
using DungeonStory.Narrative.Korean;

public static class KoreanJosaFormatterDebugScenarios
{
    public static string RunAll()
    {
        KoreanJosaFormatter formatter = new KoreanJosaFormatter();

        RequireText(formatter, "민수", Auto("name-1"), KoreanJosaKind.Topic, "민수는");
        RequireText(formatter, "민혁", Auto("name-2"), KoreanJosaKind.Topic, "민혁은");
        RequireText(formatter, "유리", Auto("name-3"), KoreanJosaKind.Subject, "유리가");
        RequireText(formatter, "강철", Auto("name-4"), KoreanJosaKind.Subject, "강철이");
        RequireText(formatter, "민수", Auto("name-5"), KoreanJosaKind.Object, "민수를");
        RequireText(formatter, "민혁", Auto("name-6"), KoreanJosaKind.Object, "민혁을");
        RequireText(formatter, "학교", Auto("name-7"), KoreanJosaKind.Direction, "학교로");
        RequireText(formatter, "성", Auto("name-8"), KoreanJosaKind.Direction, "성으로");
        RequireText(formatter, "마을", Auto("name-9"), KoreanJosaKind.Direction, "마을로");
        RequireText(formatter, "<b>민혁</b>", Auto("markup-1"), KoreanJosaKind.Subject, "<b>민혁</b>이");
        RequireText(
            formatter,
            "민수".Normalize(NormalizationForm.FormD),
            Auto("nfd-1"),
            KoreanJosaKind.Subject,
            "민수".Normalize(NormalizationForm.FormD) + "가");

        RequireText(formatter, "0", Number("0", "number-0"), KoreanJosaKind.Topic, "0은");
        RequireText(formatter, "1", Number("1", "number-1"), KoreanJosaKind.Topic, "1은");
        RequireText(formatter, "2", Number("2", "number-2"), KoreanJosaKind.Topic, "2는");
        RequireText(formatter, "3", Number("3", "number-3"), KoreanJosaKind.Topic, "3은");
        RequireText(formatter, "4", Number("4", "number-4"), KoreanJosaKind.Topic, "4는");
        RequireText(formatter, "5", Number("5", "number-5"), KoreanJosaKind.Topic, "5는");
        RequireText(formatter, "6", Number("6", "number-6"), KoreanJosaKind.Topic, "6은");
        RequireText(formatter, "7", Number("7", "number-7"), KoreanJosaKind.Topic, "7은");
        RequireText(formatter, "8", Number("8", "number-8"), KoreanJosaKind.Topic, "8은");
        RequireText(formatter, "9", Number("9", "number-9"), KoreanJosaKind.Topic, "9는");
        RequireText(formatter, "10", Number("10", "number-10"), KoreanJosaKind.Direction, "10으로");
        RequireText(formatter, "100", Number("100", "number-100"), KoreanJosaKind.Subject, "100이");
        RequireText(formatter, "1000", Number("1000", "number-1000"), KoreanJosaKind.Object, "1000을");
        RequireText(formatter, "10000", Number("10000", "number-10000"), KoreanJosaKind.Direction, "10000으로");

        RequireText(formatter, "L", Initialism("L", "initial-l"), KoreanJosaKind.Subject, "L이");
        RequireText(formatter, "M", Initialism("M", "initial-m"), KoreanJosaKind.Subject, "M이");
        RequireText(formatter, "N", Initialism("N", "initial-n"), KoreanJosaKind.Subject, "N이");
        RequireText(formatter, "R", Initialism("R", "initial-r"), KoreanJosaKind.Direction, "R로");
        RequireText(
            formatter,
            "“민혁”",
            KoreanPronunciationHint.SpokenHangul("민혁", "quoted-name"),
            KoreanJosaKind.Subject,
            "“민혁”이");

        KoreanJosaFormatResult unknown = Format(
            formatter,
            "Game",
            KoreanPronunciationHint.Unknown("unknown-1"),
            KoreanJosaKind.Subject,
            "same-display");
        Require(
            unknown.Status == KoreanJosaFormatStatus.UnknownPronunciation
            && unknown.RequiresNeutralFrame
            && unknown.Text == "Game"
            && unknown.SelectedParticle.Length == 0,
            "Unhinted Latin names must require a neutral frame.");

        KoreanJosaFormatResult spokenConsonant = Format(
            formatter,
            "Game",
            KoreanPronunciationHint.SpokenHangul("게임", "same-pronunciation"),
            KoreanJosaKind.Subject,
            "same-display");
        KoreanJosaFormatResult spokenVowel = Format(
            formatter,
            "Game",
            KoreanPronunciationHint.SpokenHangul("게이미", "same-pronunciation"),
            KoreanJosaKind.Subject,
            "same-display");
        Require(
            spokenConsonant.Text == "Game이" && spokenVowel.Text == "Game가",
            "Pronunciation value changes must not collide in the cache.");

        KoreanJosaFormatResult firstName = Format(
            formatter,
            "민수",
            Auto("same-pronunciation"),
            KoreanJosaKind.Subject,
            "same-display");
        KoreanJosaFormatResult secondName = Format(
            formatter,
            "민혁",
            Auto("same-pronunciation"),
            KoreanJosaKind.Subject,
            "same-display");
        Require(
            firstName.Text == "민수가" && secondName.Text == "민혁이",
            "Different display values with coincident revisions must not collide.");

        KoreanJosaCacheKey defaultKey = default;
        KoreanJosaResolutionCache defaultCache = new KoreanJosaResolutionCache();
        Require(!defaultKey.IsCacheable, "The default cache key must be non-cacheable.");
        Require(defaultKey.GetHashCode() == 0, "The default cache key hash must be stable.");
        Require(!defaultCache.TryGet(defaultKey, out _), "The default key must not resolve a cache entry.");

        KoreanJosaResolutionCache boundedCache = new KoreanJosaResolutionCache(2);
        KoreanJosaFormatter boundedFormatter = new KoreanJosaFormatter(boundedCache);
        RequireText(
            boundedFormatter,
            "민수",
            Auto("bounded-a"),
            KoreanJosaKind.Subject,
            "민수가");
        RequireText(
            boundedFormatter,
            "민혁",
            Auto("bounded-b"),
            KoreanJosaKind.Subject,
            "민혁이");
        RequireText(
            boundedFormatter,
            "유리",
            Auto("bounded-c"),
            KoreanJosaKind.Subject,
            "유리가");
        Require(
            boundedCache.Count == 2
            && boundedCache.Count <= boundedCache.Capacity,
            "The cache must deterministically evict its oldest entry at capacity.");
        RequireText(
            boundedFormatter,
            "민수",
            Auto("bounded-a"),
            KoreanJosaKind.Subject,
            "민수가");
        Require(
            boundedCache.Count == 2,
            "Recomputation after FIFO eviction must remain bounded and deterministic.");
        boundedCache.Clear();
        Require(boundedCache.Count == 0, "Clear must remove cached entries and FIFO state.");

        string malformed = new string(new[] { '\uD800' });
        KoreanJosaFormatResult malformedResult = Format(
            formatter,
            malformed,
            Auto("malformed-1"),
            KoreanJosaKind.Subject,
            "malformed-display");
        Require(
            malformedResult.Status == KoreanJosaFormatStatus.UnknownPronunciation
            && malformedResult.RequiresNeutralFrame,
            "Malformed UTF-16 must fail closed to a neutral frame.");

        return "PASS KoreanJosaFormatterDebugScenarios";
    }

    private static KoreanPronunciationHint Auto(string revision) =>
        KoreanPronunciationHint.AutoHangulDisplay(revision);

    private static KoreanPronunciationHint Number(string value, string revision) =>
        KoreanPronunciationHint.WholeNumber(value, revision);

    private static KoreanPronunciationHint Initialism(string value, string revision) =>
        KoreanPronunciationHint.LatinInitialism(value, revision);

    private static void RequireText(
        IKoreanJosaFormatter formatter,
        string display,
        KoreanPronunciationHint pronunciation,
        KoreanJosaKind particle,
        string expected)
    {
        KoreanJosaFormatResult result = Format(
            formatter,
            display,
            pronunciation,
            particle,
            "display:" + expected);
        Require(
            result.Status == KoreanJosaFormatStatus.Applied
            && !result.RequiresNeutralFrame
            && string.Equals(result.Text, expected, StringComparison.Ordinal),
            $"Expected '{expected}', got status={result.Status}, text='{result.Text}'.");
    }

    private static KoreanJosaFormatResult Format(
        IKoreanJosaFormatter formatter,
        string display,
        KoreanPronunciationHint pronunciation,
        KoreanJosaKind particle,
        string displayRevision)
    {
        return formatter.Format(new KoreanJosaRequest(
            new KoreanNameSnapshot(
                display,
                displayRevision,
                pronunciation,
                "ko-KR"),
            particle));
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
