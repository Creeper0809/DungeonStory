#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;

/// <summary>
/// Source-derived inventory of physical-mass authoring sites. The registry is
/// intentionally built from lexical invocation/declaration evidence instead
/// of a file allowlist, so a newly introduced writer is either classified by
/// one of the supported provenance shapes or fails as unknown in the same run.
/// Comments and string payloads are tokenized and cannot create fake calls.
/// </summary>
public static class V27PhysicalMassWriterProvenanceRegistry
{
    public static V27PhysicalMassWriterProvenanceSnapshot Capture(
        string projectRoot,
        string excludedPath)
    {
        if (string.IsNullOrWhiteSpace(projectRoot))
            throw new ArgumentException("Project root is required.", nameof(projectRoot));
        RequireLexicalRegression();

        string sourceRoot = Path.Combine(projectRoot, "Assets", "Scripts");
        if (!Directory.Exists(sourceRoot))
            throw new InvalidOperationException("C# source root is missing: " + sourceRoot);

        List<V27PhysicalMassWriterProvenanceRow> rows = new();
        List<string> unknown = new();
        foreach (string absolutePath in Directory.GetFiles(
                     sourceRoot,
                     "*.cs",
                     SearchOption.AllDirectories)
                 .OrderBy(value => value, StringComparer.Ordinal))
        {
            string path = CanonicalPath(Path.GetRelativePath(projectRoot, absolutePath));
            if (string.Equals(path, excludedPath, StringComparison.Ordinal)
                || path.EndsWith(
                    "/V27PhysicalMassWriterProvenanceRegistry.cs",
                    StringComparison.Ordinal))
            {
                continue;
            }

            string source = File.ReadAllText(absolutePath);
            IReadOnlyList<Token> tokens = Tokenize(source);
            WriterEvidence evidence = CaptureEvidence(path, tokens);
            if (!evidence.IsWriter)
                continue;

            string role = Classify(path, tokens);
            if (role.Length == 0)
                unknown.Add(path + "|" + evidence.CanonicalShape);
            rows.Add(new V27PhysicalMassWriterProvenanceRow(
                path,
                role.Length == 0 ? "unknown" : role,
                evidence.CanonicalShape,
                evidence.WriteSiteCount,
                ComputeFileDigest(absolutePath)));
        }

        V27PhysicalMassWriterProvenanceRow[] ordered = rows
            .OrderBy(value => value.Path, StringComparer.Ordinal)
            .ToArray();
        string[] duplicatePaths = ordered
            .GroupBy(value => value.Path, StringComparer.Ordinal)
            .Where(group => group.Count() != 1)
            .Select(group => group.Key)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        return new V27PhysicalMassWriterProvenanceSnapshot(
            ordered,
            unknown.OrderBy(value => value, StringComparer.Ordinal).ToArray(),
            duplicatePaths);
    }

    private static WriterEvidence CaptureEvidence(
        string path,
        IReadOnlyList<Token> tokens)
    {
        int configureCore = CountInvocation(tokens, "ConfigureCore");
        int resourceConfigure = ContainsIdentifier(tokens, "ResourceItemDefinitionSO")
            ? CountMemberInvocation(tokens, "Configure")
            : 0;
        int serializedWeight = CountFindPropertyWrite(tokens, "weight")
            + CountFindPropertyWrite(tokens, "baseWeight")
            + CountFindPropertyWrite(tokens, "weightMultiplier")
            + CountFindPropertyWrite(tokens, "unitWeight");
        int definition = path.StartsWith(
                "Assets/Scripts/Models/Economy/Content/",
                StringComparison.Ordinal)
            && (ContainsIdentifier(tokens, "ScriptableObject")
                || ContainsIdentifier(tokens, "DataScriptableObject"))
            && ContainsAnyIdentifier(
                tokens,
                "unitWeight",
                "weight",
                "baseWeight",
                "weightMultiplier")
                ? 1
                : 0;
        int count = configureCore + resourceConfigure + serializedWeight + definition;
        List<string> shapes = new();
        if (definition != 0) shapes.Add("definition");
        if (configureCore != 0) shapes.Add("configure-core:" + configureCore);
        if (resourceConfigure != 0) shapes.Add("resource-configure:" + resourceConfigure);
        if (serializedWeight != 0) shapes.Add("serialized-property-write:" + serializedWeight);
        return new WriterEvidence(count, string.Join("|", shapes));
    }

    private static void RequireLexicalRegression()
    {
        const string falsePositiveSource =
            "// ConfigureCore(1); FindProperty(\"weight\").floatValue = 1;\n"
            + "/* ResourceItemDefinitionSO value; value.Configure(1); */\n"
            + "string report = \"ConfigureCore( FindProperty(\\\"weight\\\")\";";
        WriterEvidence falsePositive = CaptureEvidence(
            "Assets/Scripts/Services/Economy/Editor/CommentOnlyProbe.cs",
            Tokenize(falsePositiveSource));
        if (falsePositive.IsWriter)
        {
            throw new InvalidOperationException(
                "PHYSICAL_MASS_WRITER_LEXER_FALSE_POSITIVE.");
        }

        const string actualWriterSource =
            "void Apply(ItemDefinitionSO item) { item.ConfigureCore(1); }";
        WriterEvidence actual = CaptureEvidence(
            "Assets/Scripts/Services/Economy/Editor/ActualProbe.cs",
            Tokenize(actualWriterSource));
        if (!actual.IsWriter || actual.WriteSiteCount != 1)
        {
            throw new InvalidOperationException(
                "PHYSICAL_MASS_WRITER_LEXER_FALSE_NEGATIVE.");
        }
    }

    private static string Classify(string path, IReadOnlyList<Token> tokens)
    {
        if (path.StartsWith(
                "Assets/Scripts/Models/Economy/Content/",
                StringComparison.Ordinal))
        {
            return ContainsIdentifier(tokens, "ResourceItemDefinitionSO")
                && ContainsIdentifier(tokens, "Configure")
                    ? "definition-forwarder"
                    : "definition-authority";
        }

        string fileName = Path.GetFileNameWithoutExtension(path);
        if (fileName.EndsWith("AssetBuilder", StringComparison.Ordinal))
            return "production-authoring-builder";
        if (fileName.EndsWith("Migration", StringComparison.Ordinal))
            return "explicit-migration-writer";

        bool editorOnly = path.Contains("/Editor/", StringComparison.Ordinal)
            || fileName.EndsWith("DebugScenarios", StringComparison.Ordinal)
            || fileName.EndsWith("Verifier", StringComparison.Ordinal)
            || fileName.EndsWith("Fixture", StringComparison.Ordinal)
            || fileName.EndsWith("Transaction", StringComparison.Ordinal);
        if (editorOnly)
            return "diagnostic-fixture-writer";
        return string.Empty;
    }

    private static int CountInvocation(IReadOnlyList<Token> tokens, string method)
    {
        int count = 0;
        for (int index = 0; index + 1 < tokens.Count; index++)
        {
            if (tokens[index].Kind == TokenKind.Identifier
                && string.Equals(tokens[index].Value, method, StringComparison.Ordinal)
                && tokens[index + 1].IsPunctuation("("))
            {
                count++;
            }
        }
        return count;
    }

    private static int CountMemberInvocation(
        IReadOnlyList<Token> tokens,
        string method)
    {
        int count = 0;
        for (int index = 1; index + 1 < tokens.Count; index++)
        {
            if (tokens[index - 1].IsPunctuation(".")
                && tokens[index].Kind == TokenKind.Identifier
                && string.Equals(tokens[index].Value, method, StringComparison.Ordinal)
                && tokens[index + 1].IsPunctuation("("))
            {
                count++;
            }
        }
        return count;
    }

    private static int CountFindPropertyWrite(
        IReadOnlyList<Token> tokens,
        string propertyName)
    {
        int count = 0;
        for (int index = 0; index + 3 < tokens.Count; index++)
        {
            if (tokens[index].Kind != TokenKind.Identifier
                || !string.Equals(tokens[index].Value, "FindProperty", StringComparison.Ordinal)
                || !tokens[index + 1].IsPunctuation("(")
                || tokens[index + 2].Kind != TokenKind.String
                || !string.Equals(
                    tokens[index + 2].Value,
                    propertyName,
                    StringComparison.Ordinal)
                || !tokens[index + 3].IsPunctuation(")"))
            {
                continue;
            }

            // A read-only lookup is not a writer. Require a floatValue assignment
            // in the same lexical statement/method body; this excludes reports
            // that merely mention the property name.
            int limit = Math.Min(tokens.Count, index + 96);
            bool assignment = false;
            for (int probe = index + 4; probe + 1 < limit; probe++)
            {
                if (tokens[probe].Kind == TokenKind.Identifier
                    && string.Equals(tokens[probe].Value, "floatValue", StringComparison.Ordinal)
                    && tokens[probe + 1].IsPunctuation("="))
                {
                    assignment = true;
                    break;
                }
            }
            if (assignment)
                count++;
        }
        return count;
    }

    private static bool ContainsIdentifier(
        IEnumerable<Token> tokens,
        string identifier) =>
        tokens.Any(value => value.Kind == TokenKind.Identifier
            && string.Equals(value.Value, identifier, StringComparison.Ordinal));

    private static bool ContainsAnyIdentifier(
        IEnumerable<Token> tokens,
        params string[] identifiers)
    {
        HashSet<string> expected = new(identifiers, StringComparer.Ordinal);
        return tokens.Any(value => value.Kind == TokenKind.Identifier
            && expected.Contains(value.Value));
    }

    private static IReadOnlyList<Token> Tokenize(string source)
    {
        List<Token> result = new();
        int index = 0;
        while (index < source.Length)
        {
            char current = source[index];
            if (char.IsWhiteSpace(current))
            {
                index++;
                continue;
            }
            if (current == '/' && index + 1 < source.Length)
            {
                if (source[index + 1] == '/')
                {
                    index += 2;
                    while (index < source.Length && source[index] != '\n') index++;
                    continue;
                }
                if (source[index + 1] == '*')
                {
                    index += 2;
                    while (index + 1 < source.Length
                           && (source[index] != '*' || source[index + 1] != '/'))
                    {
                        index++;
                    }
                    index = Math.Min(source.Length, index + 2);
                    continue;
                }
            }
            if (current == '@' && index + 1 < source.Length && source[index + 1] == '"')
            {
                index += 2;
                System.Text.StringBuilder value = new();
                while (index < source.Length)
                {
                    if (source[index] == '"')
                    {
                        if (index + 1 < source.Length && source[index + 1] == '"')
                        {
                            value.Append('"');
                            index += 2;
                            continue;
                        }
                        index++;
                        break;
                    }
                    value.Append(source[index++]);
                }
                result.Add(new Token(TokenKind.String, value.ToString()));
                continue;
            }
            if (current == '"')
            {
                index++;
                System.Text.StringBuilder value = new();
                while (index < source.Length)
                {
                    char character = source[index++];
                    if (character == '"')
                        break;
                    if (character == '\\' && index < source.Length)
                    {
                        char escaped = source[index++];
                        value.Append(escaped switch
                        {
                            'n' => '\n',
                            'r' => '\r',
                            't' => '\t',
                            _ => escaped
                        });
                        continue;
                    }
                    value.Append(character);
                }
                result.Add(new Token(TokenKind.String, value.ToString()));
                continue;
            }
            if (current == '\'')
            {
                index++;
                while (index < source.Length)
                {
                    char character = source[index++];
                    if (character == '\\' && index < source.Length)
                    {
                        index++;
                        continue;
                    }
                    if (character == '\'')
                        break;
                }
                continue;
            }
            if (current == '_' || char.IsLetter(current))
            {
                int start = index++;
                while (index < source.Length
                       && (source[index] == '_' || char.IsLetterOrDigit(source[index])))
                {
                    index++;
                }
                result.Add(new Token(
                    TokenKind.Identifier,
                    source.Substring(start, index - start)));
                continue;
            }

            result.Add(new Token(TokenKind.Punctuation, current.ToString()));
            index++;
        }
        return result;
    }

    private static string ComputeFileDigest(string path)
    {
        using SHA256 sha = SHA256.Create();
        using FileStream stream = File.OpenRead(path);
        return Hex(sha.ComputeHash(stream));
    }

    private static string Hex(byte[] bytes)
    {
        const string digits = "0123456789abcdef";
        char[] result = new char[bytes.Length * 2];
        for (int index = 0; index < bytes.Length; index++)
        {
            result[index * 2] = digits[bytes[index] >> 4];
            result[index * 2 + 1] = digits[bytes[index] & 0x0f];
        }
        return new string(result);
    }

    private static string CanonicalPath(string value) =>
        (value ?? string.Empty).Replace('\\', '/');

    private readonly struct WriterEvidence
    {
        public WriterEvidence(int writeSiteCount, string canonicalShape)
        {
            WriteSiteCount = writeSiteCount;
            CanonicalShape = canonicalShape ?? string.Empty;
        }

        public bool IsWriter => WriteSiteCount > 0;
        public int WriteSiteCount { get; }
        public string CanonicalShape { get; }
    }

    private enum TokenKind
    {
        Identifier,
        String,
        Punctuation
    }

    private readonly struct Token
    {
        public Token(TokenKind kind, string value)
        {
            Kind = kind;
            Value = value ?? string.Empty;
        }

        public TokenKind Kind { get; }
        public string Value { get; }

        public bool IsPunctuation(string value) =>
            Kind == TokenKind.Punctuation
            && string.Equals(Value, value, StringComparison.Ordinal);
    }
}

public sealed class V27PhysicalMassWriterProvenanceSnapshot
{
    public V27PhysicalMassWriterProvenanceSnapshot(
        IReadOnlyList<V27PhysicalMassWriterProvenanceRow> rows,
        IReadOnlyList<string> unknown,
        IReadOnlyList<string> duplicatePaths)
    {
        Rows = rows ?? Array.Empty<V27PhysicalMassWriterProvenanceRow>();
        Unknown = unknown ?? Array.Empty<string>();
        DuplicatePaths = duplicatePaths ?? Array.Empty<string>();
    }

    public IReadOnlyList<V27PhysicalMassWriterProvenanceRow> Rows { get; }
    public IReadOnlyList<string> Unknown { get; }
    public IReadOnlyList<string> DuplicatePaths { get; }
    public int DeclaredCount => Rows.Count(value => value.Role != "unknown");
    public int DiscoveredCount => Rows.Count;
    public int DeclaredNotDiscoveredCount => 0;
}

public readonly struct V27PhysicalMassWriterProvenanceRow
{
    public V27PhysicalMassWriterProvenanceRow(
        string path,
        string role,
        string evidenceShape,
        int writeSiteCount,
        string digest)
    {
        Path = path ?? string.Empty;
        Role = role ?? string.Empty;
        EvidenceShape = evidenceShape ?? string.Empty;
        WriteSiteCount = writeSiteCount;
        Digest = digest ?? string.Empty;
    }

    public string Path { get; }
    public string Role { get; }
    public string EvidenceShape { get; }
    public int WriteSiteCount { get; }
    public string Digest { get; }
}
#endif
