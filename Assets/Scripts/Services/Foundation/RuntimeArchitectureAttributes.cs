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
