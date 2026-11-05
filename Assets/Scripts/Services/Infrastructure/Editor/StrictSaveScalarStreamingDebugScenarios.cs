#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

// Reference-equivalence fixture for the strict scalar streaming reader only.
public static class StrictSaveScalarStreamingDebugScenarios
{
    private sealed class ScalarCase
    {
        public readonly string Name;
        public readonly string Json;
        public readonly string PropertyName;

        public ScalarCase(string name, string json, string propertyName = "target")
        {
            Name = name;
            Json = json;
            PropertyName = propertyName;
        }
    }

    private sealed class Observation
    {
        public string Value;
        public Exception Failure;
    }

    public static void VerifyForBatch()
    {
        foreach (ScalarCase testCase in Cases()) VerifyCase(testCase);
        VerifyLargePayload();
    }

    private static IEnumerable<ScalarCase> Cases()
    {
        yield return new ScalarCase("basic string", "{\"target\":\"value\"}");
        yield return new ScalarCase("target first", "{\"target\":\"first\",\"middle\":1,\"last\":2}");
        yield return new ScalarCase("target middle", "{\"first\":1,\"target\":\"middle\",\"last\":2}");
        yield return new ScalarCase("target last", "{\"first\":1,\"middle\":2,\"target\":\"last\"}");
        yield return new ScalarCase("missing", "{\"other\":1}");
        yield return new ScalarCase("null", "{\"target\":null}");
        yield return new ScalarCase("boolean", "{\"target\":true}");
        yield return new ScalarCase("integer", "{\"target\":42}");
        yield return new ScalarCase("double", "{\"target\":1.25}");
        yield return new ScalarCase("big integer", "{\"target\":123456789012345678901234567890}");
        yield return new ScalarCase("date", "{\"target\":\"2026-10-29T12:34:56Z\"}");
        yield return new ScalarCase("empty string", "{\"target\":\"\"}");
        yield return new ScalarCase("escaped string",
            "{\"target\":\"line\\nquote\\\"slash\\\\unicode\\uD55C\"}");
        yield return new ScalarCase("object target", "{\"target\":{\"value\":1}}");
        yield return new ScalarCase("array target", "{\"target\":[1,2,3]}");
        yield return new ScalarCase("root null", "null");
        yield return new ScalarCase("root array", "[1,2,3]");
        yield return new ScalarCase("root string", "\"root\"");
        yield return new ScalarCase("empty document", string.Empty);
        yield return new ScalarCase("whitespace document", " \r\n\t ");
        yield return new ScalarCase("null json", null);
        yield return new ScalarCase("null property", "{\"target\":\"value\"}", null);
        yield return new ScalarCase("leading comment", "/* lead */{\"target\":\"value\"}");
        yield return new ScalarCase("inner comments", "{/* before */\"target\":/* value */\"inner\"}");
        yield return new ScalarCase("trailing comment", "{\"target\":\"tail\"}// trailing");
        yield return new ScalarCase("unquoted property", "{target:\"unquoted\"}");
        yield return new ScalarCase("single quoted string", "{'target':'single'}");
        yield return new ScalarCase("constructor", "{\"target\":new Date(0)}");
        yield return new ScalarCase("undefined", "{\"target\":undefined}");
        yield return new ScalarCase("NaN", "{\"target\":NaN}");
        yield return new ScalarCase("positive infinity", "{\"target\":Infinity}");
        yield return new ScalarCase("negative infinity", "{\"target\":-Infinity}");
        yield return new ScalarCase("same nested keys are legal",
            "{\"target\":\"ok\",\"nested\":[{\"same\":1},{\"same\":2}],\"other\":{\"same\":3}}");
        yield return new ScalarCase("duplicate root target", "{\"target\":\"first\",\"target\":\"second\"}");
        yield return new ScalarCase("duplicate deep nested",
            "{\"target\":\"ok\",\"nested\":{\"same\":1,\"same\":2}}");
        yield return new ScalarCase("escaped duplicate key",
            "{\"target\":\"first\",\"tar\\u0067et\":\"second\"}");
        yield return new ScalarCase("duplicate after target",
            "{\"target\":\"ok\",\"other\":1,\"other\":2}");
        yield return new ScalarCase("truncated object", "{\"target\":\"value\"");
        yield return new ScalarCase("truncated array", "{\"target\":[1,2");
        yield return new ScalarCase("truncated string", "{\"target\":\"value");
        yield return new ScalarCase("invalid escape", "{\"target\":\"\\q\"}");
        yield return new ScalarCase("trailing second root", "{\"target\":\"one\"} {\"other\":2}");
    }

    private static void VerifyCase(ScalarCase testCase)
    {
        Observation expected = ObserveReference(testCase.Json, testCase.PropertyName);

        long outsideBefore = DungeonSaveJson.EditorStrictScalarParseCount;
        Observation outside = ObserveActual(testCase.Json, testCase.PropertyName);
        RequireEquivalent(expected, outside, testCase.Name + " outside scope");
        Require(DungeonSaveJson.EditorStrictScalarParseCount == outsideBefore + 1,
            testCase.Name + " did not dispatch exactly one outside-scope parse.");

        using (DungeonSaveJson.BeginReadScope())
        {
            long scopedBefore = DungeonSaveJson.EditorStrictScalarParseCount;
            Observation first = ObserveActual(testCase.Json, testCase.PropertyName);
            Observation second = ObserveActual(testCase.Json, testCase.PropertyName);
            RequireEquivalent(expected, first, testCase.Name + " first scoped read");
            RequireEquivalent(expected, second, testCase.Name + " second scoped read");
            long expectedParses = expected.Failure == null ? 1L : 2L;
            Require(DungeonSaveJson.EditorStrictScalarParseCount == scopedBefore + expectedParses,
                testCase.Name + " changed successful memoization or memoized a failure.");
        }
    }

    private static void VerifyLargePayload()
    {
        VerifyLargeNormal(BuildLargePayload(true, false), "front", "large target first");
        VerifyLargeNormal(BuildLargePayload(false, false), "back", "large target last");
        VerifyCase(new ScalarCase("large duplicate at document end", BuildLargePayload(true, true)));
    }

    private static void VerifyLargeNormal(string json, string expectedValue, string label)
    {
        Observation expected = ObserveReference(json, "target");
        Require(expected.Failure == null && expected.Value == expectedValue,
            label + " reference fixture is invalid.");
        using (DungeonSaveJson.BeginReadScope())
        {
            long parses = DungeonSaveJson.EditorStrictScalarParseCount;
            long streaming = DungeonSaveJson.EditorStrictScalarStreamingParseCount;
            long dom = DungeonSaveJson.EditorStrictScalarDomParseCount;
            Observation first = ObserveActual(json, "target");
            Observation second = ObserveActual(json, "target");
            RequireEquivalent(expected, first, label + " first read");
            RequireEquivalent(expected, second, label + " replay");
            Require(DungeonSaveJson.EditorStrictScalarParseCount == parses + 1,
                label + " was not memoized after one parse.");
            Require(DungeonSaveJson.EditorStrictScalarStreamingParseCount == streaming + 1
                    && DungeonSaveJson.EditorStrictScalarDomParseCount == dom,
                label + " did not use exactly one streaming parse and zero DOM dispatches.");
        }
    }

    private static string BuildLargePayload(bool targetFirst, bool duplicateTargetAtEnd)
    {
        const string fields = "\"fields\":[0,1,2,3,\"same\",true,null,{\"nested\":\"same\"}]";
        var json = new StringBuilder(96000);
        json.Append('{');
        if (targetFirst) json.Append("\"target\":\"front\",");
        json.Append("\"siblings\":[");
        for (int index = 0; index < 400; index++)
        {
            if (index != 0) json.Append(',');
            json.Append("{\"id\":").Append(index).Append(',').Append(fields)
                .Append(",\"label\":\"sibling-").Append(index).Append("\"}");
        }
        json.Append(']');
        if (!targetFirst) json.Append(",\"target\":\"back\"");
        if (duplicateTargetAtEnd) json.Append(",\"target\":\"duplicate-at-end\"");
        return json.Append('}').ToString();
    }

    private static Observation ObserveReference(string json, string propertyName)
    {
        try
        {
            return new Observation
            {
                Value = (string)JObject.Parse(json, new JsonLoadSettings
                    { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error })[propertyName]
            };
        }
        catch (Exception error)
        {
            return new Observation { Failure = error };
        }
    }

    private static Observation ObserveActual(string json, string propertyName)
    {
        try
        {
            return new Observation { Value = DungeonSaveJson.ReadStrictStringProperty(json, propertyName) };
        }
        catch (Exception error)
        {
            return new Observation { Failure = error };
        }
    }

    private static void RequireEquivalent(Observation expected, Observation actual, string label)
    {
        Require((expected.Failure == null) == (actual.Failure == null),
            label + " changed success or failure.");
        if (expected.Failure == null)
        {
            Require(string.Equals(expected.Value, actual.Value, StringComparison.Ordinal),
                label + " changed the scalar value.");
            return;
        }

        Require(expected.Failure.GetType() == actual.Failure.GetType()
                && string.Equals(expected.Failure.Message, actual.Failure.Message, StringComparison.Ordinal),
            label + " changed the exception type or message.");
        if (expected.Failure is JsonReaderException expectedJson)
        {
            var actualJson = actual.Failure as JsonReaderException;
            Require(actualJson != null
                    && string.Equals(expectedJson.Path, actualJson.Path, StringComparison.Ordinal)
                    && expectedJson.LineNumber == actualJson.LineNumber
                    && expectedJson.LinePosition == actualJson.LinePosition,
                label + " changed the JSON reader path or line position.");
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
#endif
