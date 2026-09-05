#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Newtonsoft.Json;
using UnityEngine;

/// <summary>
/// Shared fail-loud loader for V27 review authorities and receipts. Unity and
/// the portable verifier must reject duplicate object keys and non-canonical
/// UTF-8 before JsonUtility projects the validated bytes into DTOs.
/// </summary>
internal static class V27StrictJsonGuard
{
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);

    public static string ReadProjectRelative(string projectRelativePath)
    {
        if (string.IsNullOrWhiteSpace(projectRelativePath)
            || projectRelativePath.Contains("\\", StringComparison.Ordinal)
            || Path.IsPathRooted(projectRelativePath))
        {
            throw new InvalidOperationException(
                "V27_STRICT_JSON_PATH_INVALID: " + projectRelativePath);
        }
        string root = Directory.GetParent(Application.dataPath)?.FullName
            ?? throw new InvalidOperationException("Project root is unavailable.");
        string absolute = Path.GetFullPath(Path.Combine(
            root,
            projectRelativePath.Replace('/', Path.DirectorySeparatorChar)));
        string canonicalRoot = Path.GetFullPath(root)
            .TrimEnd(Path.DirectorySeparatorChar)
            + Path.DirectorySeparatorChar;
        if (!absolute.StartsWith(canonicalRoot, StringComparison.OrdinalIgnoreCase)
            || !File.Exists(absolute))
        {
            throw new InvalidOperationException(
                "V27_STRICT_JSON_FILE_MISSING_OR_ESCAPED: "
                + projectRelativePath);
        }
        byte[] bytes = File.ReadAllBytes(absolute);
        if (bytes.Length >= 3
            && bytes[0] == 0xef
            && bytes[1] == 0xbb
            && bytes[2] == 0xbf)
        {
            throw new InvalidOperationException(
                "V27_STRICT_JSON_UTF8_BOM_FORBIDDEN: " + projectRelativePath);
        }
        string json;
        try
        {
            json = StrictUtf8.GetString(bytes);
        }
        catch (DecoderFallbackException exception)
        {
            throw new InvalidOperationException(
                "V27_STRICT_JSON_UTF8_INVALID: " + projectRelativePath,
                exception);
        }
        ValidateSingleObject(json, projectRelativePath);
        return json;
    }

    private static void ValidateSingleObject(string json, string label)
    {
        var scopes = new Stack<HashSet<string>>();
        bool rootStarted = false;
        bool rootComplete = false;
        try
        {
            using StringReader input = new(json);
            using JsonTextReader reader = new(input)
            {
                DateParseHandling = DateParseHandling.None,
                FloatParseHandling = FloatParseHandling.Decimal,
                SupportMultipleContent = false
            };
            while (reader.Read())
            {
                if (reader.TokenType == JsonToken.Comment
                    || reader.TokenType == JsonToken.Undefined)
                {
                    throw new InvalidOperationException(
                        "V27_STRICT_JSON_NON_DATA_TOKEN: " + label);
                }
                if (rootComplete)
                {
                    throw new InvalidOperationException(
                        "V27_STRICT_JSON_TRAILING_CONTENT: " + label);
                }
                switch (reader.TokenType)
                {
                    case JsonToken.StartObject:
                        if (!rootStarted)
                            rootStarted = true;
                        scopes.Push(new HashSet<string>(StringComparer.Ordinal));
                        break;
                    case JsonToken.StartArray:
                        if (!rootStarted)
                        {
                            throw new InvalidOperationException(
                                "V27_STRICT_JSON_ROOT_NOT_OBJECT: " + label);
                        }
                        scopes.Push(null);
                        break;
                    case JsonToken.PropertyName:
                        if (scopes.Count == 0 || scopes.Peek() == null)
                        {
                            throw new InvalidOperationException(
                                "V27_STRICT_JSON_PROPERTY_OUTSIDE_OBJECT: " + label);
                        }
                        string propertyName = reader.Value as string
                            ?? throw new InvalidOperationException(
                                "V27_STRICT_JSON_PROPERTY_NAME_INVALID: " + label);
                        if (!scopes.Peek().Add(propertyName))
                        {
                            throw new InvalidOperationException(
                                "V27_STRICT_JSON_DUPLICATE_KEY: " + label
                                + ":" + propertyName);
                        }
                        break;
                    case JsonToken.EndObject:
                    case JsonToken.EndArray:
                        if (scopes.Count == 0)
                        {
                            throw new InvalidOperationException(
                                "V27_STRICT_JSON_SCOPE_UNDERFLOW: " + label);
                        }
                        scopes.Pop();
                        if (scopes.Count == 0)
                            rootComplete = true;
                        break;
                }
            }
        }
        catch (JsonReaderException exception)
        {
            throw new InvalidOperationException(
                "V27_STRICT_JSON_SYNTAX_INVALID: " + label,
                exception);
        }
        if (!rootStarted || !rootComplete || scopes.Count != 0)
        {
            throw new InvalidOperationException(
                "V27_STRICT_JSON_ROOT_INCOMPLETE: " + label);
        }
    }
}
#endif
