using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

/// <summary>
/// Editor audit evidence that a final performance result was consumed by a
/// domain boundary. Calls are removed from player builds by Conditional.
/// This is diagnostic evidence only and never participates in gameplay state.
/// </summary>
public static class CharacterPerformanceExecutionTrace
{
    private static readonly object Gate = new();
    private static readonly Dictionary<string, MutableEntry> Entries =
        new(StringComparer.Ordinal);

    [Conditional("UNITY_EDITOR")]
    public static void Record(
        string formulaId,
        string consumerId,
        float inputValue,
        float outputValue,
        string detail = "")
    {
        string formula = Require(formulaId, nameof(formulaId));
        string consumer = Require(consumerId, nameof(consumerId));
        string key = formula + "|" + consumer;
        lock (Gate)
        {
            if (!Entries.TryGetValue(key, out MutableEntry entry))
            {
                entry = new MutableEntry(formula, consumer);
                Entries.Add(key, entry);
            }
            entry.Count++;
            entry.InputValue = inputValue;
            entry.OutputValue = outputValue;
            entry.Detail = detail?.Trim() ?? string.Empty;
        }
    }

    public static void Clear()
    {
        lock (Gate)
            Entries.Clear();
    }

    public static IReadOnlyList<CharacterPerformanceExecutionTraceEntry>
        Snapshot()
    {
        lock (Gate)
        {
            return Entries.Values
                .OrderBy(value => value.FormulaId, StringComparer.Ordinal)
                .ThenBy(value => value.ConsumerId, StringComparer.Ordinal)
                .Select(value => new CharacterPerformanceExecutionTraceEntry(
                    value.FormulaId,
                    value.ConsumerId,
                    value.Count,
                    value.InputValue,
                    value.OutputValue,
                    value.Detail))
                .ToArray();
        }
    }

    private static string Require(string value, string parameterName) =>
        string.IsNullOrWhiteSpace(value)
            ? throw new ArgumentException(
                "Performance execution trace identifiers are required.",
                parameterName)
            : value.Trim();

    private sealed class MutableEntry
    {
        public MutableEntry(string formulaId, string consumerId)
        {
            FormulaId = formulaId;
            ConsumerId = consumerId;
        }

        public string FormulaId { get; }
        public string ConsumerId { get; }
        public int Count { get; set; }
        public float InputValue { get; set; }
        public float OutputValue { get; set; }
        public string Detail { get; set; } = string.Empty;
    }
}

public sealed class CharacterPerformanceExecutionTraceEntry
{
    public CharacterPerformanceExecutionTraceEntry(
        string formulaId,
        string consumerId,
        int count,
        float inputValue,
        float outputValue,
        string detail)
    {
        FormulaId = formulaId ?? string.Empty;
        ConsumerId = consumerId ?? string.Empty;
        Count = Math.Max(0, count);
        InputValue = inputValue;
        OutputValue = outputValue;
        Detail = detail ?? string.Empty;
    }

    public string FormulaId { get; }
    public string ConsumerId { get; }
    public int Count { get; }
    public float InputValue { get; }
    public float OutputValue { get; }
    public string Detail { get; }
}
