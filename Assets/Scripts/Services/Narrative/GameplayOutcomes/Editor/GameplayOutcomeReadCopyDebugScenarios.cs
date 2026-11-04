#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

// Controlled save DTO fixture. It does not publish gameplay facts or restore a world.
public static class GameplayOutcomeReadCopyDebugScenarios
{
    public static void VerifyForBatch()
    {
        var source = (GameplayOutcomeLedgerSaveData)Populate(typeof(GameplayOutcomeLedgerSaveData), "ledger", 0);
        var copy = (GameplayOutcomeLedgerSaveData)((IDungeonSaveReadSnapshot)source).CopyForSaveRead();
        AssertCopy(typeof(GameplayOutcomeLedgerSaveData), source, copy, "ledger", 0);

        var nullable = (GameplayOutcomeLedgerSaveData)Populate(typeof(GameplayOutcomeLedgerSaveData), "nullable", 0);
        nullable.outbox = null;
        nullable.exactOutcomes[0].metrics = null;
        var nullableCopy = (GameplayOutcomeLedgerSaveData)((IDungeonSaveReadSnapshot)nullable).CopyForSaveRead();
        AssertCopy(typeof(GameplayOutcomeLedgerSaveData), nullable, nullableCopy, "nullable", 0);
        Require(nullableCopy.outbox == null && nullableCopy.exactOutcomes[0].metrics == null
                && nullableCopy.exactOutcomes[2] == null,
            "Null lists or null list elements changed during copy.");

        string json = JsonUtility.ToJson(source);
        long parses = DungeonSaveJson.EditorSourceParseCount;
        long replays = DungeonSaveJson.EditorReplayCount;
        long copies = DungeonSaveJson.EditorSnapshotCopyCount;
        using (DungeonSaveJson.BeginReadScope())
        {
            GameplayOutcomeLedgerSaveData first = DungeonSaveJson.Deserialize<GameplayOutcomeLedgerSaveData>(json);
            Require(first.outbox.Count > 0 && first.outbox[0] != null
                    && first.outbox[0].metrics.Count > 0 && first.outbox[0].metrics[0] != null,
                "Serialized fixture lost the nested outcome and metric.");
            double originalMetric = first.outbox[0].metrics[0].value;
            string originalProducer = first.outbox[0].producerId;
            first.outbox[0].metrics[0].value = -50d;
            first.outbox[0].producerId = "mutated";
            GameplayOutcomeLedgerSaveData second = DungeonSaveJson.Deserialize<GameplayOutcomeLedgerSaveData>(json);
            Require(DungeonSaveJson.EditorSourceParseCount == parses + 1
                    && DungeonSaveJson.EditorReplayCount == replays + 1
                    && DungeonSaveJson.EditorSnapshotCopyCount == copies + 2,
                "Scoped ledger read did not create a private snapshot and one copy replay.");
            Require(!ReferenceEquals(first, second)
                    && !ReferenceEquals(first.outbox, second.outbox)
                    && !ReferenceEquals(first.outbox[0], second.outbox[0])
                    && !ReferenceEquals(first.outbox[0].metrics, second.outbox[0].metrics)
                    && !ReferenceEquals(first.outbox[0].metrics[0], second.outbox[0].metrics[0])
                    && second.outbox[0].producerId == originalProducer
                    && BitConverter.DoubleToInt64Bits(second.outbox[0].metrics[0].value)
                        == BitConverter.DoubleToInt64Bits(originalMetric),
                "Mutating the first read changed the later deep copy.");
        }

        const string exactJson = "{\"outbox\":[{\"metrics\":[{\"value\":0.10000000000000002}]}]}";
        using (DungeonSaveJson.BeginReadScope())
        {
            GameplayOutcomeLedgerSaveData first = DungeonSaveJson.Deserialize<GameplayOutcomeLedgerSaveData>(exactJson);
            GameplayOutcomeLedgerSaveData second = DungeonSaveJson.Deserialize<GameplayOutcomeLedgerSaveData>(exactJson);
            long expected = BitConverter.DoubleToInt64Bits(0.10000000000000002d);
            Require(BitConverter.DoubleToInt64Bits(first.outbox[0].metrics[0].value) == expected
                    && BitConverter.DoubleToInt64Bits(second.outbox[0].metrics[0].value) == expected,
                "Source parse or snapshot copy changed the exact double literal.");
        }
    }

    private static object Populate(Type type, string path, int depth)
    {
        if (depth > 24) throw new InvalidOperationException("Unexpected recursive save DTO: " + path);
        if (type == typeof(string)) return "qa:" + path;
        if (type == typeof(bool)) return true;
        if (type == typeof(int)) return 17;
        if (type == typeof(long)) return 41L;
        if (type == typeof(float)) return 1.25f;
        if (type == typeof(double)) return 0.10000000000000002d;
        if (type.IsEnum)
        {
            object[] options = Enum.GetValues(type).Cast<object>().ToArray();
            object nonzero = options.FirstOrDefault(value => Convert.ToInt64(value) != 0L);
            return nonzero ?? throw new InvalidOperationException("Enum has no nonzero fixture value: " + type.FullName);
        }
        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>))
        {
            IList list = (IList)Activator.CreateInstance(type);
            Type element = type.GetGenericArguments()[0];
            list.Add(Populate(element, path + "[0]", depth + 1));
            list.Add(Populate(element, path + "[1]", depth + 1));
            if (element.IsClass) list.Add(null);
            return list;
        }
        if (!type.IsClass || !type.IsSealed || !type.IsDefined(typeof(SerializableAttribute), false))
            throw new InvalidOperationException("Unknown save fixture field type: " + type.FullName + " at " + path);
        object instance = Activator.CreateInstance(type);
        foreach (FieldInfo field in type.GetFields(BindingFlags.Instance | BindingFlags.Public))
            field.SetValue(instance, Populate(field.FieldType, path + "." + field.Name, depth + 1));
        return instance;
    }

    private static void AssertCopy(Type type, object source, object copy, string path, int depth)
    {
        if (depth > 24) throw new InvalidOperationException("Unexpected recursive save DTO: " + path);
        if (source == null || copy == null)
        {
            Require(source == null && copy == null, "Null changed at " + path);
            return;
        }
        if (type == typeof(string) || type.IsPrimitive || type.IsEnum)
        {
            Require(source.Equals(copy), "Scalar changed at " + path);
            return;
        }
        Require(!ReferenceEquals(source, copy), "Reference was shared at " + path);
        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>))
        {
            IList left = (IList)source;
            IList right = (IList)copy;
            Require(left.Count == right.Count, "List length changed at " + path);
            Type element = type.GetGenericArguments()[0];
            for (int index = 0; index < left.Count; index++)
                AssertCopy(element, left[index], right[index], path + "[" + index + "]", depth + 1);
            return;
        }
        if (!type.IsClass || !type.IsSealed || !type.IsDefined(typeof(SerializableAttribute), false))
            throw new InvalidOperationException("Unknown save copy field type: " + type.FullName + " at " + path);
        foreach (FieldInfo field in type.GetFields(BindingFlags.Instance | BindingFlags.Public))
            AssertCopy(field.FieldType, field.GetValue(source), field.GetValue(copy), path + "." + field.Name, depth + 1);
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
#endif
