#if UNITY_EDITOR
using System;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

/// <summary>
/// Canonical JSON shared by source-v8 hashes and its Python verifier:
/// recursively sorted object keys, compact UTF-8, unescaped Unicode and finite
/// JSON numbers. Unlike the older controlled-input canonicalizer, this permits
/// typed evidence metrics represented as JSON floating-point values.
/// </summary>
internal static class GameplayModuleSelectionCanonicalJson
{
    private static readonly UTF8Encoding Utf8NoBom = new(false, true);

    internal static byte[] ToUtf8(JToken value)
    {
        if (value == null)
            throw new ArgumentNullException(nameof(value));
        JToken canonical = Canonicalize(value);
        string json = JsonConvert.SerializeObject(
            canonical,
            Formatting.None,
            new JsonSerializerSettings
            {
                Culture = CultureInfo.InvariantCulture,
                StringEscapeHandling = StringEscapeHandling.Default,
                FloatFormatHandling = FloatFormatHandling.DefaultValue
            });
        return Utf8NoBom.GetBytes(json);
    }

    internal static string Sha256Prefixed(byte[] bytes)
    {
        using SHA256 sha = SHA256.Create();
        return "sha256:" + string.Concat(
            sha.ComputeHash(bytes ?? throw new ArgumentNullException(nameof(bytes)))
                .Select(value => value.ToString("x2", CultureInfo.InvariantCulture)));
    }

    internal static string Hash(JToken value) => Sha256Prefixed(ToUtf8(value));

    private static JToken Canonicalize(JToken value) => value.Type switch
    {
        JTokenType.Object => new JObject(((JObject)value).Properties()
            .OrderBy(property => property.Name, StringComparer.Ordinal)
            .Select(property => new JProperty(
                property.Name,
                Canonicalize(property.Value)))),
        JTokenType.Array => new JArray(((JArray)value).Select(Canonicalize)),
        JTokenType.Integer => new JValue(value.Value<long>()),
        JTokenType.Float => Finite(value.Value<double>()),
        JTokenType.String => new JValue(value.Value<string>() ?? string.Empty),
        JTokenType.Boolean => new JValue(value.Value<bool>()),
        JTokenType.Null => JValue.CreateNull(),
        _ => throw new ArgumentException(
            "Unsupported source-v8 canonical JSON token: " + value.Type,
            nameof(value))
    };

    private static JValue Finite(double value)
    {
        if (double.IsNaN(value) || double.IsInfinity(value))
            throw new ArgumentException(
                "Source-v8 canonical JSON numbers must be finite.",
                nameof(value));
        return new JValue(value);
    }
}
#endif
