#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using Newtonsoft.Json.Linq;

// Controlled DTO fixture for the character save reader. No world restore is performed.
public static class SavePayloadReadMemoDebugScenarios
{
    [Serializable]
    public sealed class Child
    {
        public int value;
    }

    [Serializable]
    public sealed class Payload
    {
        public List<Child> children;
        public double number;
        public decimal amount;
        public byte[] bytes;
        public string note;
        public DateTime date;
        public int value;
    }

    [Serializable]
    public sealed class DecimalProjection
    {
        public decimal number;
    }

    public static void VerifyForBatch()
    {
        string json = "{\"children\":[{\"value\":7}],"
            + "\"number\":0.10000000000000002,"
            + "\"amount\":0.1234567890123456789012345678,"
            + "\"bytes\":\"AQID\",\"note\":null,"
            + "\"date\":\"2026-10-28T01:02:03Z\",\"value\":1,\"value\":2}";
        long parses = DungeonSaveJson.EditorSourceParseCount;
        long replays = DungeonSaveJson.EditorReplayCount;
        using (DungeonSaveJson.BeginReadScope())
        {
            Payload first = DungeonSaveJson.Deserialize<Payload>(json);
            Require(DungeonSaveJson.EditorSourceParseCount == parses + 1
                    && DungeonSaveJson.EditorReplayCount == replays,
                "The first scoped read did not parse the source exactly once.");
            CheckValues(first);
            first.children[0].value = 99;
            first.children.Add(new Child { value = 100 });
            first.bytes[0] = 99;
            first.note = "changed";
            Payload second = DungeonSaveJson.Deserialize<Payload>(json);
            Require(DungeonSaveJson.EditorSourceParseCount == parses + 1
                    && DungeonSaveJson.EditorReplayCount == replays + 1,
                "The second identical typed read did not replay the stored tokens.");
            Require(!ReferenceEquals(first, second)
                    && !ReferenceEquals(first.children, second.children)
                    && !ReferenceEquals(first.children[0], second.children[0])
                    && !ReferenceEquals(first.bytes, second.bytes),
                "Replay shared a mutable result object, list, child, or byte array.");
            CheckValues(second);

            DecimalProjection otherType = DungeonSaveJson.Deserialize<DecimalProjection>(json);
            Require(otherType.number == decimal.Parse("0.10000000000000002", CultureInfo.InvariantCulture)
                    && DungeonSaveJson.EditorSourceParseCount == parses + 2
                    && DungeonSaveJson.EditorReplayCount == replays + 1,
                "A different DTO type reused the original type's token stream.");
            string equalButDistinct = new string(json.ToCharArray());
            Require(!ReferenceEquals(json, equalButDistinct) && json == equalButDistinct,
                "Distinct source-string fixture did not preserve equal text.");
            DungeonSaveJson.Deserialize<Payload>(equalButDistinct);
            Require(DungeonSaveJson.EditorSourceParseCount == parses + 3
                    && DungeonSaveJson.EditorReplayCount == replays + 1,
                "A different string reference reused an existing source entry.");

            using (DungeonSaveJson.BeginReadScope())
            {
                CheckValues(DungeonSaveJson.Deserialize<Payload>(json));
                Require(DungeonSaveJson.EditorSourceParseCount == parses + 4
                        && DungeonSaveJson.EditorReplayCount == replays + 1,
                    "Nested scope reused its outer scope's token stream.");
                CheckValues(DungeonSaveJson.Deserialize<Payload>(json));
                Require(DungeonSaveJson.EditorSourceParseCount == parses + 4
                        && DungeonSaveJson.EditorReplayCount == replays + 2,
                    "Nested scope did not replay its own entry.");
            }
            CheckValues(DungeonSaveJson.Deserialize<Payload>(json));
            Require(DungeonSaveJson.EditorSourceParseCount == parses + 4
                    && DungeonSaveJson.EditorReplayCount == replays + 3,
                "Leaving a nested scope did not restore the outer memo.");
        }

        CheckValues(DungeonSaveJson.Deserialize<Payload>(json));
        Require(DungeonSaveJson.EditorSourceParseCount == parses + 5
                && DungeonSaveJson.EditorReplayCount == replays + 3,
            "An out-of-scope read reused a scoped token stream.");
        using (DungeonSaveJson.BeginReadScope())
        {
            CheckValues(DungeonSaveJson.Deserialize<Payload>(json));
            Require(DungeonSaveJson.EditorSourceParseCount == parses + 6
                    && DungeonSaveJson.EditorReplayCount == replays + 3,
                "A new scope reused an earlier scope's token stream.");
        }

        VerifyInvalid("{\"value\":\"not-an-integer\",\"value\":2}");
        VerifyInvalid("{} {}");
        VerifyInvalid("{\"value\":");
        VerifyStrictScalar();
    }

    private static void VerifyStrictScalar()
    {
        string source = "{\"runId\":\"run:one\",\"other\":\"second\",\"nested\":{\"n\":1}}";
        long parses = DungeonSaveJson.EditorStrictScalarParseCount;
        long hits = DungeonSaveJson.EditorStrictScalarReplayCount;
        using (DungeonSaveJson.BeginReadScope())
        {
            Require(DungeonSaveJson.ReadStrictStringProperty(source, "runId") == "run:one"
                && DungeonSaveJson.ReadStrictStringProperty(source, "runId") == "run:one"
                && DungeonSaveJson.EditorStrictScalarParseCount == parses + 1
                && DungeonSaveJson.EditorStrictScalarReplayCount == hits + 1,
                "Strict scalar was not parsed once and replayed exactly.");
            Require(DungeonSaveJson.ReadStrictStringProperty(source, "other") == "second"
                && DungeonSaveJson.EditorStrictScalarParseCount == parses + 2,
                "Strict scalar reused a different property.");
            DungeonSaveJson.ReadStrictStringProperty(new string(source.ToCharArray()), "runId");
            Require(DungeonSaveJson.EditorStrictScalarParseCount == parses + 3,
                "Strict scalar reused equal text from a different source reference.");
            using (DungeonSaveJson.BeginReadScope())
            {
                DungeonSaveJson.ReadStrictStringProperty(source, "runId");
                Require(DungeonSaveJson.EditorStrictScalarParseCount == parses + 4,
                    "Nested scope reused an outer strict scalar.");
            }
            DungeonSaveJson.ReadStrictStringProperty(source, "runId");
            Require(DungeonSaveJson.EditorStrictScalarReplayCount == hits + 2,
                "Outer strict scalar was lost after leaving a nested scope.");
        }
        DungeonSaveJson.ReadStrictStringProperty(source, "runId");
        using (DungeonSaveJson.BeginReadScope()) DungeonSaveJson.ReadStrictStringProperty(source, "runId");
        Require(DungeonSaveJson.EditorStrictScalarParseCount == parses + 6,
            "Strict scalar escaped its operation scope.");
        foreach (string json in new[] { "{}", "{\"runId\":null}", "{\"runId\":123}",
                     "{\"runId\":\"first\",\"runId\":\"second\"}",
                     "{\"runId\":\"x\",\"nested\":{\"same\":1,\"same\":2}}",
                     "{\"runId\":{}}", "{} {}", "{\"runId\":" })
        {
            string expected = null; Type expectedFailure = null;
            try { expected = (string)JObject.Parse(json, new JsonLoadSettings {
                DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error })["runId"]; }
            catch (Exception error) { expectedFailure = error.GetType(); }
            using (DungeonSaveJson.BeginReadScope())
            {
                long before = DungeonSaveJson.EditorStrictScalarParseCount;
                for (int repeat = 0; repeat < 2; repeat++)
                {
                    string actual = null; Type failure = null;
                    try { actual = DungeonSaveJson.ReadStrictStringProperty(json, "runId"); }
                    catch (Exception error) { failure = error.GetType(); }
                    Require(expected == actual && expectedFailure == failure,
                        "Strict scalar changed original missing/null/type/duplicate-key/error semantics.");
                }
                Require(DungeonSaveJson.EditorStrictScalarParseCount == before + (expectedFailure == null ? 1 : 2),
                    "A failed strict parse was memoized or a successful null scalar was not replayed.");
            }
        }
    }

    private static void CheckValues(Payload value)
    {
        Require(value != null && value.children != null && value.children.Count == 1
                && value.children[0] != null && value.children[0].value == 7
                && BitConverter.DoubleToInt64Bits(value.number)
                    == BitConverter.DoubleToInt64Bits(0.10000000000000002d)
                && value.amount == decimal.Parse("0.1234567890123456789012345678", CultureInfo.InvariantCulture)
                && value.bytes != null && value.bytes.Length == 3
                && value.bytes[0] == 1 && value.bytes[1] == 2 && value.bytes[2] == 3
                && value.note == null
                && value.date == new DateTime(2026, 10, 28, 1, 2, 3, DateTimeKind.Utc)
                && value.date.Kind == DateTimeKind.Utc
                && value.value == 2,
            "Save reader changed typed numbers, nested content, bytes, null, date, or duplicate-key order.");
    }

    private static void VerifyInvalid(string json)
    {
        Type expected = FailureType(json);
        Require(expected != null, "Invalid source unexpectedly succeeded outside a scope.");
        using (DungeonSaveJson.BeginReadScope())
        {
            long parses = DungeonSaveJson.EditorSourceParseCount;
            long replays = DungeonSaveJson.EditorReplayCount;
            Require(FailureType(json) == expected && FailureType(json) == expected,
                "Scoped invalid input changed the original parser failure type.");
            Require(DungeonSaveJson.EditorSourceParseCount == parses + 2
                    && DungeonSaveJson.EditorReplayCount == replays,
                "A failed source was memoized instead of parsed again.");
        }
    }

    private static Type FailureType(string json)
    {
        try { DungeonSaveJson.Deserialize<Payload>(json); }
        catch (Exception error) { return error.GetType(); }
        return null;
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
#endif
