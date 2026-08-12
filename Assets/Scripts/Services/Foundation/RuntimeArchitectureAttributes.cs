using System;

/// <summary>
/// Marks a non-authoritative render cache that is never persisted and can be
/// reconstructed solely from immutable code/content after a domain reload.
/// </summary>
[AttributeUsage(AttributeTargets.Field, AllowMultiple = false, Inherited = false)]
public sealed class RuntimeRebuildableCacheAttribute : Attribute
{
}

/// <summary>
/// Marks private application-adapter state that only tracks a rebuildable
/// Unity/event projection. It must never contain gameplay or persisted state.
/// </summary>
[AttributeUsage(AttributeTargets.Field, AllowMultiple = false, Inherited = false)]
public sealed class ApplicationAdapterTransientStateAttribute : Attribute
{
}

/// <summary>
/// Marks a public gameplay command that is intentionally reachable from a live
/// player or AI entry surface. The evidence string names that surface and its
/// focused execution verifier so dead public APIs cannot masquerade as features.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class GameplayEntryPointAttribute : Attribute
{
    public GameplayEntryPointAttribute(string executionEvidence)
    {
        ExecutionEvidence = string.IsNullOrWhiteSpace(executionEvidence)
            ? throw new ArgumentException("Execution evidence is required.", nameof(executionEvidence))
            : executionEvidence.Trim();
    }

    public string ExecutionEvidence { get; }
}

/// <summary>
/// Marks a public mutation that exists only as an orchestration boundary inside
/// the runtime. It must not be called directly by UI or content code.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class GameplayInternalOnlyAttribute : Attribute
{
    public GameplayInternalOnlyAttribute(string reason, string allowedCallerScope)
    {
        Reason = Require(reason, nameof(reason));
        AllowedCallerScope = Require(allowedCallerScope, nameof(allowedCallerScope));
    }

    public string Reason { get; }
    public string AllowedCallerScope { get; }

    private static string Require(string value, string parameterName) =>
        string.IsNullOrWhiteSpace(value)
            ? throw new ArgumentException("A non-empty architecture reason is required.", parameterName)
            : value.Trim();
}

/// <summary>
/// Marks a compatibility mutation that may only be invoked by a named migration
/// path until its declared removal condition is met.
/// </summary>
[AttributeUsage(
    AttributeTargets.Method
    | AttributeTargets.Field
    | AttributeTargets.Property
    | AttributeTargets.Class,
    AllowMultiple = false,
    Inherited = false)]
public sealed class GameplayMigrationOnlyAttribute : Attribute
{
    public GameplayMigrationOnlyAttribute(string reason, string removalCondition)
    {
        Reason = Require(reason, nameof(reason));
        RemovalCondition = Require(removalCondition, nameof(removalCondition));
    }

    public string Reason { get; }
    public string RemovalCondition { get; }

    private static string Require(string value, string parameterName) =>
        string.IsNullOrWhiteSpace(value)
            ? throw new ArgumentException("A non-empty migration reason is required.", parameterName)
            : value.Trim();
}
