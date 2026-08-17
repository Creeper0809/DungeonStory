#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using DungeonStory.Balance;

[BalanceSerializationLayer]
public static class V27BalanceCsvSerializer
{
    public const string ArtifactPath = "Artifacts/QA/v27-balance-before-after.csv";

    private static readonly string[] Header =
    {
        "schemaVersion", "domain", "definitionKind", "stableId", "metric", "unit",
        "before", "after", "authoredRoundedValue", "percentDelta", "exactFormula",
        "beforeBom", "afterBom", "beforeDirectWU", "afterDirectWU", "beforeBomEwu",
        "afterBomEwu", "beforeLaborDensity", "afterLaborDensity", "upstreamOnlyAfter",
        "inheritedDelta", "rawLocalDelta", "roundingEnvelope", "downstreamConsumerCount",
        "dependencyIds", "rootCauseIds", "anomalyDisposition", "reasonCode", "reasonDetail",
        "sourceAuthority", "sourcePropertyPath", "executionRoute", "saveAuthority",
        "verificationEvidence", "reviewStatus", "approvalKey", "dependencyFingerprint",
        "localFingerprint", "sourceDigest", "semanticHash", "assetApplied",
        "balanceBaselineRecordId"
    };

    public static void Write(Stream stream, FrozenBalanceLedger ledger)
    {
        if (stream == null)
            throw new ArgumentNullException(nameof(stream));
        if (ledger == null)
            throw new ArgumentNullException(nameof(ledger));
        V27Utf8CsvWriter writer = new V27Utf8CsvWriter(stream, 16384);
        EmitHeader(writer);
        foreach (CanonicalBalanceMetricRecord record in ledger.Records)
            EmitRecord(writer, record);
        writer.Flush();
    }

    public static void WriteEscapedField(StreamWriter writer, ReadOnlySpan<char> text)
    {
        if (writer == null)
            throw new ArgumentNullException(nameof(writer));
        ValidateUtf16(text);
        bool needsQuotes = false;
        for (int index = 0; index < text.Length; index++)
        {
            char character = text[index];
            if (character == ',' || character == '"' || character == '\r' || character == '\n')
            {
                needsQuotes = true;
                break;
            }
        }
        if (!needsQuotes)
        {
            writer.Write(text);
            return;
        }

        writer.Write('"');
        int segmentStart = 0;
        for (int index = 0; index < text.Length; index++)
        {
            if (text[index] != '"')
                continue;
            writer.Write(text.Slice(segmentStart, index - segmentStart));
            writer.Write('"');
            writer.Write('"');
            segmentStart = index + 1;
        }
        writer.Write(text.Slice(segmentStart));
        writer.Write('"');
    }

    private static void EmitHeader(V27Utf8CsvWriter writer)
    {
        for (int index = 0; index < Header.Length; index++)
        {
            if (index != 0)
                writer.WriteAscii(',');
            writer.WriteUtf8(Header[index].AsSpan());
        }
        writer.WriteCrLf();
    }

    private static void EmitRecord(V27Utf8CsvWriter writer, CanonicalBalanceMetricRecord record)
    {
        bool first = true;
        WriteField(writer, record.SchemaVersion, ref first);
        WriteField(writer, record.Domain, ref first);
        WriteField(writer, record.DefinitionKind, ref first);
        WriteField(writer, record.StableId, ref first);
        WriteField(writer, record.Metric, ref first);
        WriteField(writer, record.Unit, ref first);
        WriteField(writer, record.Before, ref first);
        WriteField(writer, record.After, ref first);
        WriteField(writer, record.AuthoredRoundedValue, ref first);
        WriteField(writer, record.PercentDelta, ref first);
        WriteField(writer, record.ExactFormula, ref first);
        WriteField(writer, record.BeforeBom, ref first);
        WriteField(writer, record.AfterBom, ref first);
        WriteField(writer, record.BeforeDirectWu, ref first);
        WriteField(writer, record.AfterDirectWu, ref first);
        WriteField(writer, record.BeforeBomEwu, ref first);
        WriteField(writer, record.AfterBomEwu, ref first);
        WriteField(writer, record.BeforeLaborDensity, ref first);
        WriteField(writer, record.AfterLaborDensity, ref first);
        WriteField(writer, record.UpstreamOnlyAfter, ref first);
        WriteField(writer, record.InheritedDelta, ref first);
        WriteField(writer, record.RawLocalDelta, ref first);
        WriteField(writer, record.RoundingEnvelope, ref first);
        WriteField(writer, record.DownstreamConsumerCount, ref first);
        WriteListField(writer, record.DependencyIds, ref first);
        WriteListField(writer, record.RootCauseIds, ref first);
        WriteField(writer, record.AnomalyDisposition, ref first);
        WriteField(writer, record.ReasonCode, ref first);
        WriteField(writer, record.ReasonDetail, ref first);
        WriteField(writer, record.SourceAuthority, ref first);
        WriteField(writer, record.SourcePropertyPath, ref first);
        WriteField(writer, record.ExecutionRoute, ref first);
        WriteField(writer, record.SaveAuthority, ref first);
        WriteField(writer, record.VerificationEvidence, ref first);
        WriteField(writer, record.ReviewStatus, ref first);
        WriteField(writer, record.ApprovalKey, ref first);
        WriteField(writer, record.DependencyFingerprint, ref first);
        WriteField(writer, record.LocalFingerprint, ref first);
        WriteField(writer, record.SourceDigest, ref first);
        WriteField(writer, record.SemanticHash, ref first);
        WriteField(writer, record.AssetApplied, ref first);
        WriteField(writer, record.BalanceBaselineRecordId, ref first);
        writer.WriteCrLf();
    }

    private static void WriteField(V27Utf8CsvWriter writer, string value, ref bool first)
    {
        if (!first)
            writer.WriteAscii(',');
        first = false;
        WriteEscapedField(writer, (value ?? string.Empty).AsSpan());
    }

    private static void WriteListField(
        V27Utf8CsvWriter writer,
        IReadOnlyList<string> values,
        ref bool first)
    {
        if (!first)
            writer.WriteAscii(',');
        first = false;
        if (values == null || values.Count == 0)
            return;
        for (int index = 0; index < values.Count; index++)
        {
            if (index != 0)
                writer.WriteAscii('|');
            writer.WriteUtf8(values[index].AsSpan());
        }
    }

    internal static void WriteEscapedField(V27Utf8CsvWriter writer, ReadOnlySpan<char> text) =>
        writer.WriteEscapedField(text);

    private static void ValidateUtf16(ReadOnlySpan<char> text)
    {
        for (int index = 0; index < text.Length; index++)
        {
            char character = text[index];
            if (char.IsHighSurrogate(character))
            {
                if (index + 1 >= text.Length || !char.IsLowSurrogate(text[index + 1]))
                    throw new InvalidDataException("CSV field contains an unpaired high surrogate.");
                index++;
            }
            else if (char.IsLowSurrogate(character))
            {
                throw new InvalidDataException("CSV field contains an unpaired low surrogate.");
            }
        }
    }
}

[BalanceSerializationLayer]
internal sealed class V27Utf8CsvWriter
{
    private static readonly UTF8Encoding Utf8 = new UTF8Encoding(false, true);
    private readonly char[] buffer;
    private readonly byte[] encodedBuffer;
    private Stream stream;
    private int count;

    public V27Utf8CsvWriter(Stream stream, int bufferSize)
    {
        this.stream = stream ?? throw new ArgumentNullException(nameof(stream));
        if (bufferSize < 256)
            throw new ArgumentOutOfRangeException(nameof(bufferSize));
        buffer = new char[bufferSize];
        encodedBuffer = new byte[Utf8.GetMaxByteCount(bufferSize)];
    }

    public void Reset(Stream target)
    {
        Flush();
        stream = target ?? throw new ArgumentNullException(nameof(target));
    }

    public void WriteEscapedField(ReadOnlySpan<char> text)
    {
        WriteEscapedField(text, V27CsvFieldShape.Capture(text));
    }

    public void WriteEscapedField(ReadOnlySpan<char> text, V27CsvFieldShape shape)
    {
        if (!shape.NeedsQuotes)
        {
            WriteUtf8(text);
            return;
        }

        WriteAscii('"');
        int segmentStart = 0;
        int quoteIndex = shape.FirstQuoteIndex;
        if (quoteIndex < 0)
        {
            WriteUtf8(text);
            WriteAscii('"');
            return;
        }
        if (shape.QuoteCount == 1)
        {
            WriteUtf8(text.Slice(0, quoteIndex));
            WriteAscii('"');
            WriteAscii('"');
            WriteUtf8(text.Slice(quoteIndex + 1));
            WriteAscii('"');
            return;
        }
        while (segmentStart < text.Length)
        {
            WriteUtf8(text.Slice(segmentStart, quoteIndex - segmentStart));
            WriteAscii('"');
            WriteAscii('"');
            segmentStart = quoteIndex + 1;
            int relativeQuote = text.Slice(segmentStart).IndexOf('"');
            if (relativeQuote < 0)
                break;
            quoteIndex = segmentStart + relativeQuote;
        }
        WriteUtf8(text.Slice(segmentStart));
        WriteAscii('"');
    }

    public void WriteUtf8(ReadOnlySpan<char> text)
    {
        while (!text.IsEmpty)
        {
            if (count == buffer.Length)
                FlushBuffer();
            int characters = Math.Min(text.Length, buffer.Length - count);
            text.Slice(0, characters).CopyTo(buffer.AsSpan(count));
            count += characters;
            text = text.Slice(characters);
        }
    }

    public void WriteAscii(char character)
    {
        if (character > 0x7f)
            throw new ArgumentOutOfRangeException(nameof(character));
        if (count == buffer.Length)
            FlushBuffer();
        buffer[count++] = character;
    }

    public void WriteCrLf()
    {
        WriteAscii('\r');
        WriteAscii('\n');
    }

    public void Flush()
    {
        FlushBuffer();
        stream.Flush();
    }

    private void FlushBuffer()
    {
        if (count == 0)
            return;
        int byteCount = Utf8.GetBytes(
            buffer.AsSpan(0, count),
            encodedBuffer.AsSpan());
        stream.Write(encodedBuffer, 0, byteCount);
        count = 0;
    }

}

internal readonly struct V27CsvFieldShape
{
    private V27CsvFieldShape(bool needsQuotes, int firstQuoteIndex, int quoteCount)
    {
        NeedsQuotes = needsQuotes;
        FirstQuoteIndex = firstQuoteIndex;
        QuoteCount = quoteCount;
    }

    public bool NeedsQuotes { get; }
    public int FirstQuoteIndex { get; }
    public int QuoteCount { get; }

    public static V27CsvFieldShape Capture(ReadOnlySpan<char> text)
    {
        int firstSpecial = text.IndexOfAny(',', '"', '\n');
        if (firstSpecial < 0)
            firstSpecial = text.IndexOf('\r');
        if (firstSpecial < 0)
            return new V27CsvFieldShape(false, -1, 0);
        int firstQuote = -1;
        int quoteCount = 0;
        for (int index = firstSpecial; index < text.Length; index++)
        {
            if (text[index] != '"')
                continue;
            if (firstQuote < 0)
                firstQuote = index;
            quoteCount++;
        }
        return new V27CsvFieldShape(
            true,
            firstQuote,
            quoteCount);
    }
}

[BalanceSerializationLayer]
public static class V27BalanceJsonSerializer
{
    public const string AnomalyArtifactPath =
        "Artifacts/QA/v27-balance-anomaly-graph.json";

    public static void WriteAnomalyGraph(
        Stream stream,
        IReadOnlyList<BalanceAnomalyNode> nodes)
    {
        if (stream == null)
            throw new ArgumentNullException(nameof(stream));
        using StreamWriter writer = new StreamWriter(
            stream,
            new UTF8Encoding(false, true),
            8192,
            leaveOpen: true);
        writer.Write('{');
        writer.Write('\n');
        writer.Write("  \"schemaVersion\": \"v27.1\",");
        writer.Write('\n');
        writer.Write("  \"nodes\": [");
        writer.Write('\n');
        int count = nodes?.Count ?? 0;
        for (int index = 0; index < count; index++)
        {
            BalanceAnomalyNode node = nodes[index];
            writer.Write("    {\"stableId\":");
            WriteJsonString(writer, node.StableId);
            writer.Write(",\"metric\":");
            WriteJsonString(writer, node.Metric);
            writer.Write(",\"severity\":");
            WriteJsonString(writer, SeverityToken(node.Severity));
            writer.Write(",\"disposition\":");
            WriteJsonString(writer, DispositionToken(node.Disposition));
            writer.Write(",\"reasonCode\":");
            WriteJsonString(writer, node.ReasonCode);
            writer.Write(",\"emitsCiAnnotation\":");
            writer.Write(node.EmitsCiAnnotation ? "true" : "false");
            writer.Write(",\"rootCauseIds\":[");
            for (int rootIndex = 0; rootIndex < node.RootCauseIds.Count; rootIndex++)
            {
                if (rootIndex != 0)
                    writer.Write(',');
                WriteJsonString(writer, node.RootCauseIds[rootIndex]);
            }
            writer.Write("]}");
            if (index + 1 < count)
                writer.Write(',');
            writer.Write('\n');
        }
        writer.Write("  ]");
        writer.Write('\n');
        writer.Write('}');
        writer.Write('\n');
        writer.Flush();
    }

    internal static void WriteJsonString(StreamWriter writer, string value)
    {
        ReadOnlySpan<char> text = (value ?? string.Empty).AsSpan();
        writer.Write('"');
        int segmentStart = 0;
        for (int index = 0; index < text.Length; index++)
        {
            char character = text[index];
            string escape = character switch
            {
                '"' => "\\\"",
                '\\' => "\\\\",
                '\b' => "\\b",
                '\f' => "\\f",
                '\n' => "\\n",
                '\r' => "\\r",
                '\t' => "\\t",
                _ => null
            };
            if (escape == null && character >= 0x20)
                continue;
            writer.Write(text.Slice(segmentStart, index - segmentStart));
            if (escape != null)
            {
                writer.Write(escape);
            }
            else
            {
                writer.Write("\\u");
                WriteHex4(writer, character);
            }
            segmentStart = index + 1;
        }
        writer.Write(text.Slice(segmentStart));
        writer.Write('"');
    }

    private static string SeverityToken(BalanceAnomalySeverity severity) => severity switch
    {
        BalanceAnomalySeverity.None => "None",
        BalanceAnomalySeverity.Warning => "Warning",
        BalanceAnomalySeverity.Critical => "Critical",
        _ => throw new ArgumentOutOfRangeException(nameof(severity), severity, null)
    };

    internal static string DispositionToken(BalanceAnomalyDisposition disposition) => disposition switch
    {
        BalanceAnomalyDisposition.None => "None",
        BalanceAnomalyDisposition.RootCritical => "RootCritical",
        BalanceAnomalyDisposition.LocalCritical => "LocalCritical",
        BalanceAnomalyDisposition.CollapsedInheritedOnly => "CollapsedInheritedOnly",
        BalanceAnomalyDisposition.CollapsedRoundingOnly => "CollapsedRoundingOnly",
        BalanceAnomalyDisposition.CollapsedMultiRoot => "CollapsedMultiRoot",
        BalanceAnomalyDisposition.Approved => "Approved",
        _ => throw new ArgumentOutOfRangeException(nameof(disposition), disposition, null)
    };

    private static void WriteHex4(StreamWriter writer, int value)
    {
        const string Hex = "0123456789abcdef";
        writer.Write(Hex[(value >> 12) & 0xf]);
        writer.Write(Hex[(value >> 8) & 0xf]);
        writer.Write(Hex[(value >> 4) & 0xf]);
        writer.Write(Hex[value & 0xf]);
    }
}

public static class V27BalanceArtifactWriter
{
    private static readonly UTF8Encoding Utf8NoBom = new UTF8Encoding(false, true);

    public static bool WriteCsvIfDifferent(string projectRelativePath, FrozenBalanceLedger ledger)
    {
        return WriteIfDifferent(projectRelativePath, stream =>
            V27BalanceCsvSerializer.Write(stream, ledger));
    }

    public static bool WriteIfDifferent(string projectRelativePath, Action<Stream> write)
    {
        if (write == null)
            throw new ArgumentNullException(nameof(write));
        string canonical = BalanceCanonicalText.ProjectRelativePath(projectRelativePath);
        string root = Directory.GetParent(UnityEngine.Application.dataPath)?.FullName
            ?? throw new InvalidOperationException("Project root is unavailable.");
        string target = Path.Combine(root, canonical.Replace('/', Path.DirectorySeparatorChar));
        string directory = Path.GetDirectoryName(target)
            ?? throw new InvalidOperationException("Artifact directory is unavailable.");
        Directory.CreateDirectory(directory);
        string temporary = target + ".v27.tmp";
        try
        {
            using (FileStream stream = new FileStream(
                       temporary,
                       FileMode.Create,
                       FileAccess.Write,
                       FileShare.None,
                       16384,
                       FileOptions.SequentialScan))
            {
                write(stream);
                stream.Flush(flushToDisk: true);
            }
            if (File.Exists(target) && FilesEqual(target, temporary))
            {
                File.Delete(temporary);
                return false;
            }
            if (File.Exists(target))
                File.Replace(temporary, target, null);
            else
                File.Move(temporary, target);
            return true;
        }
        catch
        {
            if (File.Exists(temporary))
                File.Delete(temporary);
            throw;
        }
    }

    public static string ComputeSha256(string projectRelativePath)
    {
        string canonical = BalanceCanonicalText.ProjectRelativePath(projectRelativePath);
        string root = Directory.GetParent(UnityEngine.Application.dataPath)?.FullName
            ?? throw new InvalidOperationException("Project root is unavailable.");
        string path = Path.Combine(root, canonical.Replace('/', Path.DirectorySeparatorChar));
        using FileStream stream = File.OpenRead(path);
        using SHA256 sha = SHA256.Create();
        byte[] digest = sha.ComputeHash(stream);
        char[] characters = new char[digest.Length * 2];
        const string Hex = "0123456789abcdef";
        for (int index = 0; index < digest.Length; index++)
        {
            characters[index * 2] = Hex[digest[index] >> 4];
            characters[index * 2 + 1] = Hex[digest[index] & 0x0f];
        }
        return new string(characters);
    }

    private static bool FilesEqual(string leftPath, string rightPath)
    {
        FileInfo left = new FileInfo(leftPath);
        FileInfo right = new FileInfo(rightPath);
        if (left.Length != right.Length)
            return false;
        const int BufferSize = 32768;
        byte[] leftBuffer = new byte[BufferSize];
        byte[] rightBuffer = new byte[BufferSize];
        using FileStream leftStream = File.OpenRead(leftPath);
        using FileStream rightStream = File.OpenRead(rightPath);
        while (true)
        {
            int leftRead = leftStream.Read(leftBuffer, 0, leftBuffer.Length);
            int rightRead = rightStream.Read(rightBuffer, 0, rightBuffer.Length);
            if (leftRead != rightRead)
                return false;
            if (leftRead == 0)
                return true;
            for (int index = 0; index < leftRead; index++)
                if (leftBuffer[index] != rightBuffer[index])
                    return false;
        }
    }
}
#endif
