using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Current-version-only JSON section whose complete, detached restore candidate
/// is built during preflight/staging. Commit may only publish that already
/// validated candidate; it must not parse, normalize, filter, or resolve IDs.
/// </summary>
public abstract class DungeonStrictJsonSaveSection<TPayload, TRestoreCandidate> :
    IDungeonSaveSection,
    IDungeonSaveSectionPreflight,
    IDungeonStagedSaveSection
    where TPayload : class
    where TRestoreCandidate : class
{
    public abstract string SectionId { get; }
    public abstract int SectionVersion { get; }
    public abstract DungeonSaveRestorePhase RestorePhase { get; }
    public virtual IReadOnlyList<string> DependsOn => Array.Empty<string>();

    public string Capture()
    {
        TPayload payload = CapturePayload()
            ?? throw new InvalidOperationException(
                $"{SectionId} capture returned a null payload.");
        return JsonUtility.ToJson(payload);
    }

    public void ValidatePayload(
        string payloadJson,
        int sectionVersion,
        DungeonGameRestoreReport report)
    {
        RequireReport(report);
        RequireCurrentVersion(sectionVersion);
        ValidateParsedPayload(ParsePayload(payloadJson));
    }

    protected virtual void ValidateParsedPayload(TPayload payload)
    {
        TRestoreCandidate candidate = BuildRestoreCandidate(payload)
            ?? throw new InvalidOperationException(
                $"{SectionId} restore candidate builder returned null.");
        if (candidate is IDungeonDiscardableRestoreCandidate discardable)
        {
            discardable.Discard();
        }
    }

    public void Restore(
        string payloadJson,
        int sectionVersion,
        DungeonGameRestoreReport report)
    {
        IDungeonSaveRestoreStage stage = StageRestore(
            payloadJson,
            sectionVersion,
            report);
        if (report.Success)
        {
            stage.Commit(report);
        }
    }

    public IDungeonSaveRestoreStage StageRestore(
        string payloadJson,
        int sectionVersion,
        DungeonGameRestoreReport report)
    {
        RequireReport(report);
        RequireCurrentVersion(sectionVersion);
        TRestoreCandidate candidate = BuildRestoreCandidate(
                ParsePayload(payloadJson))
            ?? throw new InvalidOperationException(
                $"{SectionId} restore candidate builder returned null.");
        return new DungeonCandidateSaveRestoreStage<TRestoreCandidate>(
            SectionId,
            candidate,
            PublishRestoreCandidate);
    }

    protected abstract TPayload CapturePayload();
    protected abstract TRestoreCandidate BuildRestoreCandidate(TPayload payload);
    protected abstract void PublishRestoreCandidate(TRestoreCandidate candidate);

    /// <summary>
    /// Validates JSON shape that Unity's serializer cannot preserve. In
    /// particular, JsonUtility normalizes some explicit null collections to
    /// field-initialized empty lists, so required collection fields must be
    /// checked before deserialization.
    /// </summary>
    protected virtual void ValidateRawPayload(string payloadJson)
    {
    }

    protected void RequireTopLevelArrayFields(
        string payloadJson,
        params string[] fieldNames)
    {
        DungeonStrictJsonShape.RequireTopLevelArrays(
            SectionId,
            payloadJson,
            fieldNames);
    }

    private TPayload ParsePayload(string payloadJson)
    {
        if (string.IsNullOrWhiteSpace(payloadJson))
        {
            throw new InvalidOperationException(
                $"{SectionId} payload is empty.");
        }

        ValidateRawPayload(payloadJson);

        try
        {
            return JsonUtility.FromJson<TPayload>(payloadJson)
                ?? throw new InvalidOperationException(
                    $"{SectionId} payload deserialized to null.");
        }
        catch (Exception exception) when (exception is not InvalidOperationException)
        {
            throw new InvalidOperationException(
                $"{SectionId} payload JSON is invalid: {exception.Message}",
                exception);
        }
    }

    private void RequireCurrentVersion(int sectionVersion)
    {
        if (sectionVersion != SectionVersion)
        {
            throw new InvalidOperationException(
                $"Unsupported {SectionId} section version {sectionVersion}; "
                + $"expected current version {SectionVersion}. Start a new game for legacy saves.");
        }
    }

    private static void RequireReport(DungeonGameRestoreReport report)
    {
        if (report == null)
        {
            throw new ArgumentNullException(nameof(report));
        }
    }
}

internal static class DungeonStrictJsonShape
{
    public static void RequireTopLevelArrays(
        string sectionId,
        string payloadJson,
        IReadOnlyCollection<string> requiredFieldNames)
    {
        HashSet<string> required = new HashSet<string>(
            requiredFieldNames ?? Array.Empty<string>(),
            StringComparer.Ordinal);
        if (required.Count == 0)
        {
            return;
        }

        JsonShapeReader reader = new JsonShapeReader(
            sectionId,
            payloadJson);
        HashSet<string> found = reader.ReadTopLevelArrayFields(required);
        string[] missing = required
            .Where(fieldName => !found.Contains(fieldName))
            .OrderBy(fieldName => fieldName, StringComparer.Ordinal)
            .ToArray();
        if (missing.Length > 0)
        {
            throw new InvalidOperationException(
                $"{sectionId} payload is missing required array field(s): "
                + string.Join(", ", missing) + ".");
        }
    }

    private sealed class JsonShapeReader
    {
        private readonly string sectionId;
        private readonly string source;
        private int index;

        public JsonShapeReader(string sectionId, string source)
        {
            this.sectionId = string.IsNullOrWhiteSpace(sectionId)
                ? "Save section"
                : sectionId;
            this.source = source ?? string.Empty;
        }

        public HashSet<string> ReadTopLevelArrayFields(
            ISet<string> required)
        {
            HashSet<string> found = new HashSet<string>(StringComparer.Ordinal);
            SkipWhitespace();
            Require('{');
            SkipWhitespace();
            if (TryConsume('}'))
            {
                RequireEnd();
                return found;
            }

            while (true)
            {
                string fieldName = ReadString();
                SkipWhitespace();
                Require(':');
                SkipWhitespace();
                if (required.Contains(fieldName))
                {
                    if (!found.Add(fieldName))
                    {
                        Fail($"contains duplicate required field '{fieldName}'");
                    }
                    if (Peek() != '[')
                    {
                        Fail($"field '{fieldName}' must be a JSON array and cannot be null");
                    }
                }

                SkipValue();
                SkipWhitespace();
                if (TryConsume('}'))
                {
                    RequireEnd();
                    return found;
                }

                Require(',');
                SkipWhitespace();
            }
        }

        private void SkipValue()
        {
            SkipWhitespace();
            switch (Peek())
            {
                case '"':
                    ReadString();
                    return;
                case '{':
                    SkipObject();
                    return;
                case '[':
                    SkipArray();
                    return;
                default:
                    SkipPrimitive();
                    return;
            }
        }

        private void SkipObject()
        {
            Require('{');
            SkipWhitespace();
            if (TryConsume('}'))
            {
                return;
            }

            while (true)
            {
                ReadString();
                SkipWhitespace();
                Require(':');
                SkipValue();
                SkipWhitespace();
                if (TryConsume('}'))
                {
                    return;
                }

                Require(',');
                SkipWhitespace();
            }
        }

        private void SkipArray()
        {
            Require('[');
            SkipWhitespace();
            if (TryConsume(']'))
            {
                return;
            }

            while (true)
            {
                SkipValue();
                SkipWhitespace();
                if (TryConsume(']'))
                {
                    return;
                }

                Require(',');
                SkipWhitespace();
            }
        }

        private void SkipPrimitive()
        {
            switch (Peek())
            {
                case 't':
                    RequireLiteral("true");
                    return;
                case 'f':
                    RequireLiteral("false");
                    return;
                case 'n':
                    RequireLiteral("null");
                    return;
                default:
                    SkipNumber();
                    return;
            }
        }

        private void RequireLiteral(string expected)
        {
            for (int offset = 0; offset < expected.Length; offset++)
            {
                if (index >= source.Length
                    || source[index++] != expected[offset])
                {
                    Fail($"contains an invalid JSON literal; expected '{expected}'");
                }
            }
        }

        private void SkipNumber()
        {
            TryConsume('-');
            if (TryConsume('0'))
            {
                if (index < source.Length && IsDigit(source[index]))
                {
                    Fail("contains a JSON number with a leading zero");
                }
            }
            else
            {
                RequireDigit(oneToNine: true);
                while (index < source.Length && IsDigit(source[index]))
                {
                    index++;
                }
            }

            if (TryConsume('.'))
            {
                RequireDigit(oneToNine: false);
                while (index < source.Length && IsDigit(source[index]))
                {
                    index++;
                }
            }

            if (index < source.Length
                && (source[index] == 'e' || source[index] == 'E'))
            {
                index++;
                if (index < source.Length
                    && (source[index] == '+' || source[index] == '-'))
                {
                    index++;
                }

                RequireDigit(oneToNine: false);
                while (index < source.Length && IsDigit(source[index]))
                {
                    index++;
                }
            }
        }

        private void RequireDigit(bool oneToNine)
        {
            if (index >= source.Length
                || (oneToNine
                    ? source[index] < '1' || source[index] > '9'
                    : !IsDigit(source[index])))
            {
                Fail("contains an invalid JSON number");
            }

            index++;
        }

        private static bool IsDigit(char value) =>
            value >= '0' && value <= '9';

        private string ReadString()
        {
            Require('"');
            System.Text.StringBuilder value = new System.Text.StringBuilder();
            while (index < source.Length)
            {
                char current = source[index++];
                if (current == '"')
                {
                    return value.ToString();
                }

                if (current != '\\')
                {
                    if (current < 0x20)
                    {
                        Fail("contains a control character in a JSON string");
                    }
                    value.Append(current);
                    continue;
                }

                if (index >= source.Length)
                {
                    Fail("contains an incomplete JSON escape");
                }

                char escaped = source[index++];
                switch (escaped)
                {
                    case '"': value.Append('"'); break;
                    case '\\': value.Append('\\'); break;
                    case '/': value.Append('/'); break;
                    case 'b': value.Append('\b'); break;
                    case 'f': value.Append('\f'); break;
                    case 'n': value.Append('\n'); break;
                    case 'r': value.Append('\r'); break;
                    case 't': value.Append('\t'); break;
                    case 'u': value.Append(ReadUnicodeEscape()); break;
                    default:
                        Fail($"contains unsupported JSON escape '\\{escaped}'");
                        break;
                }
            }

            Fail("contains an unterminated JSON string");
            return string.Empty;
        }

        private char ReadUnicodeEscape()
        {
            if (index + 4 > source.Length)
            {
                Fail("contains an incomplete Unicode escape");
            }

            int value = 0;
            for (int offset = 0; offset < 4; offset++)
            {
                char digit = source[index++];
                value <<= 4;
                if (digit >= '0' && digit <= '9')
                {
                    value += digit - '0';
                }
                else if (digit >= 'a' && digit <= 'f')
                {
                    value += digit - 'a' + 10;
                }
                else if (digit >= 'A' && digit <= 'F')
                {
                    value += digit - 'A' + 10;
                }
                else
                {
                    Fail("contains an invalid Unicode escape");
                }
            }

            return (char)value;
        }

        private void SkipWhitespace()
        {
            while (index < source.Length
                && IsJsonWhitespace(source[index]))
            {
                index++;
            }
        }

        private static bool IsJsonWhitespace(char value) =>
            value == ' '
            || value == '\t'
            || value == '\n'
            || value == '\r';

        private char Peek()
        {
            if (index >= source.Length)
            {
                Fail("ended unexpectedly");
            }

            return source[index];
        }

        private bool TryConsume(char expected)
        {
            if (index >= source.Length || source[index] != expected)
            {
                return false;
            }

            index++;
            return true;
        }

        private void Require(char expected)
        {
            if (!TryConsume(expected))
            {
                Fail($"expected '{expected}'");
            }
        }

        private void RequireEnd()
        {
            SkipWhitespace();
            if (index != source.Length)
            {
                Fail("contains trailing data after the root object");
            }
        }

        private void Fail(string detail)
        {
            throw new InvalidOperationException(
                $"{sectionId} payload {detail} at JSON offset {index}.");
        }
    }
}
