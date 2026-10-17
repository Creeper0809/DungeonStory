using System;
using System.Collections.Generic;
using System.Linq;

public sealed class GameplayOutcomeRegistry : IGameplayOutcomeRegistry, IGameplayActionLogRegistry
{
    public IReadOnlyList<IGameplayActionLogModule> ActionModules { get; } = Array.Empty<IGameplayActionLogModule>();
    public GameplayExperienceObserverRegistry ExperienceObservers { get; }

    public static GameplayOutcomeRegistry FromModules(IEnumerable<IGameplayActionLogModule> modules)
    {
        var frozen = (modules ?? throw new ArgumentNullException(nameof(modules))).ToArray();
        if (frozen.Length == 0 || frozen.Any(value => value == null))
            throw new ArgumentException("Action logging requires nonempty, nonnull modules.");
        // Revalidate implementations of the public extension interface, not just our concrete class.
        foreach (var module in frozen)
            _ = new GameplayActionLogModule(module.Adapter, module.Descriptor, module.ExperienceObserver);
        if (frozen.Any(value => value.OutcomeTypeId != value.Descriptor.OutcomeTypeId))
            throw new ArgumentException("Action module identity differs from its descriptor.");
        return new GameplayOutcomeRegistry(frozen);
    }

    private GameplayOutcomeRegistry(IGameplayActionLogModule[] modules)
        : this(modules.Select(value => value.Descriptor), DistinctModuleAdapters(modules))
    {
        ActionModules = Array.AsReadOnly(modules.OrderBy(value => value.OutcomeTypeId.Value,
            StringComparer.Ordinal).ToArray());
        ExperienceObservers = new GameplayExperienceObserverRegistry(
            ActionModules.Select(value => value.ExperienceObserver));
    }

    private static IEnumerable<IGameplayOutcomeAdapterRegistration> DistinctModuleAdapters(
        IEnumerable<IGameplayActionLogModule> modules)
    {
        var found = new Dictionary<Type, IGameplayOutcomeAdapterRegistration>();
        foreach (var module in modules)
        {
            var adapter = module.Adapter;
            if (found.TryGetValue(adapter.ReceiptType, out var existing))
            {
                if (!ReferenceEquals(adapter, existing))
                    throw new InvalidOperationException("Action modules cannot replace a shared receipt adapter.");
            }
            else found.Add(adapter.ReceiptType, adapter);
        }
        return found.Values;
    }

    private readonly Dictionary<GameplayOutcomeTypeId, IGameplayOutcomeDescriptor> descriptors;
    private readonly Dictionary<Type, IGameplayOutcomeAdapterRegistration> adapters;
    private readonly GameplayOutcomeTypeId[] registeredOutcomeTypes;
    private readonly Type[] registeredReceiptTypes;
    private readonly int consolidationPolicyVersion;

    public GameplayOutcomeRegistry(
        IEnumerable<IGameplayOutcomeDescriptor> descriptorSource,
        IEnumerable<IGameplayOutcomeAdapterRegistration> adapterSource,
        IEnumerable<IGameplayOutcomeDescriptorCatalog> descriptorCatalogs = null)
    {
        descriptors = new Dictionary<GameplayOutcomeTypeId, IGameplayOutcomeDescriptor>();
        adapters = new Dictionary<Type, IGameplayOutcomeAdapterRegistration>();

        int observedPolicyVersion = 0;
        if (descriptorSource != null)
        {
            foreach (IGameplayOutcomeDescriptor descriptor in descriptorSource)
                RegisterDescriptor(descriptor, ref observedPolicyVersion);
        }

        if (descriptorCatalogs != null)
        {
            foreach (IGameplayOutcomeDescriptorCatalog catalog in descriptorCatalogs)
            {
                IReadOnlyList<IGameplayOutcomeDescriptor> catalogDescriptors =
                    catalog?.Descriptors
                    ?? throw new InvalidOperationException(
                        "Every gameplay outcome descriptor catalog must provide a descriptor collection.");
                for (int index = 0; index < catalogDescriptors.Count; index++)
                    RegisterDescriptor(catalogDescriptors[index], ref observedPolicyVersion);
            }
        }

        if (adapterSource != null)
        {
            foreach (IGameplayOutcomeAdapterRegistration adapter in adapterSource)
            {
                if (adapter == null || adapter.ReceiptType == null || !adapter.OutcomeTypeId.IsValid)
                    throw new InvalidOperationException("Every gameplay outcome adapter registration must provide receipt and outcome types.");
                if (adapter is IGameplayOutcomeDynamicAdapterRegistration dynamicAdapter)
                {
                    IReadOnlyList<GameplayOutcomeTypeId> supported =
                        dynamicAdapter.SupportedOutcomeTypes;
                    if (supported == null || supported.Count == 0)
                    {
                        throw new InvalidOperationException(
                            $"Dynamic gameplay outcome adapter '{adapter.ReceiptType.FullName}' has no supported outcome types.");
                    }
                    var uniqueTypes = new HashSet<GameplayOutcomeTypeId>();
                    for (int index = 0; index < supported.Count; index++)
                    {
                        GameplayOutcomeTypeId outcomeTypeId = supported[index];
                        if (!outcomeTypeId.IsValid
                            || !uniqueTypes.Add(outcomeTypeId)
                            || !dynamicAdapter.SupportsOutcomeType(outcomeTypeId)
                            || !descriptors.ContainsKey(outcomeTypeId))
                        {
                            throw new InvalidOperationException(
                                $"Dynamic gameplay outcome adapter '{adapter.ReceiptType.FullName}' has an invalid, duplicate, unsupported, or descriptor-less outcome type '{outcomeTypeId}'.");
                        }
                    }
                    if (!dynamicAdapter.SupportsOutcomeType(adapter.OutcomeTypeId))
                    {
                        throw new InvalidOperationException(
                            $"Dynamic gameplay outcome adapter '{adapter.ReceiptType.FullName}' does not include its primary outcome type '{adapter.OutcomeTypeId}'.");
                    }
                }
                else if (!descriptors.ContainsKey(adapter.OutcomeTypeId))
                {
                    throw new InvalidOperationException(
                        $"Gameplay outcome adapter '{adapter.ReceiptType.FullName}' has no descriptor for '{adapter.OutcomeTypeId}'.");
                }
                if (!adapters.TryAdd(adapter.ReceiptType, adapter))
                    throw new InvalidOperationException($"Duplicate gameplay outcome adapter for receipt '{adapter.ReceiptType.FullName}'.");
            }
        }

        foreach (GameplayOutcomeTypeId descriptorId in descriptors.Keys)
        {
            bool hasAdapter = false;
            foreach (IGameplayOutcomeAdapterRegistration adapter in adapters.Values)
            {
                if (adapter.OutcomeTypeId == descriptorId
                    || adapter is IGameplayOutcomeDynamicAdapterRegistration dynamicAdapter
                    && dynamicAdapter.SupportsOutcomeType(descriptorId))
                {
                    hasAdapter = true;
                    break;
                }
            }
            if (!hasAdapter)
            {
                throw new InvalidOperationException(
                    $"Gameplay outcome descriptor '{descriptorId}' has no registered receipt adapter.");
            }
        }

        registeredOutcomeTypes = new GameplayOutcomeTypeId[descriptors.Count];
        descriptors.Keys.CopyTo(registeredOutcomeTypes, 0);
        Array.Sort(registeredOutcomeTypes, CompareOutcomeTypeIds);
        registeredReceiptTypes = new Type[adapters.Count];
        adapters.Keys.CopyTo(registeredReceiptTypes, 0);
        Array.Sort(registeredReceiptTypes, CompareTypes);
        consolidationPolicyVersion = observedPolicyVersion == 0 ? 1 : observedPolicyVersion;
    }

    public IReadOnlyList<GameplayOutcomeTypeId> RegisteredOutcomeTypes => registeredOutcomeTypes;
    public IReadOnlyList<Type> RegisteredReceiptTypes => registeredReceiptTypes;
    public int ConsolidationPolicyVersion => consolidationPolicyVersion;

    public bool TryGetDescriptor(
        GameplayOutcomeTypeId outcomeTypeId,
        out IGameplayOutcomeDescriptor descriptor) =>
        descriptors.TryGetValue(outcomeTypeId, out descriptor);

    public bool TryGetAdapter<TReceipt>(out IGameplayOutcomeAdapter<TReceipt> adapter)
    {
        if (adapters.TryGetValue(typeof(TReceipt), out IGameplayOutcomeAdapterRegistration registration)
            && registration is IGameplayOutcomeAdapter<TReceipt> typed)
        {
            adapter = typed;
            return true;
        }
        adapter = null;
        return false;
    }

    private void RegisterDescriptor(
        IGameplayOutcomeDescriptor descriptor,
        ref int observedPolicyVersion)
    {
        if (descriptor == null
            || !descriptor.OutcomeTypeId.IsValid
            || descriptor.PerspectiveProjector == null
            || descriptor.MemoryPolicy == null
            || descriptor.PerceptionPolicy == null
            || descriptor.MemoryConsolidator == null)
        {
            throw new InvalidOperationException(
                "Every gameplay outcome descriptor must provide a valid ID, projector, memory policy, perception policy, and consolidator.");
        }
        if (descriptor.MemoryPolicy.PolicyVersion <= 0)
        {
            throw new InvalidOperationException(
                $"Outcome descriptor '{descriptor.OutcomeTypeId}' has an invalid policy version.");
        }
        if (observedPolicyVersion == 0)
            observedPolicyVersion = descriptor.MemoryPolicy.PolicyVersion;
        else if (observedPolicyVersion != descriptor.MemoryPolicy.PolicyVersion)
            throw new InvalidOperationException(
                "All gameplay outcome descriptors must share one consolidation policy version.");
        if (descriptor.PerceptionPolicy.MaximumOptionalWitnessLinks < 0)
        {
            throw new InvalidOperationException(
                $"Outcome descriptor '{descriptor.OutcomeTypeId}' has an invalid witness limit.");
        }
        if (descriptor.MemoryConsolidator is IOutcomeCompactionContract compaction
            && compaction.SupportsCompaction
            && descriptor.PerspectiveProjector is not ICompactedNarrativePerspectiveProjector)
        {
            throw new InvalidOperationException(
                $"Compactable outcome descriptor '{descriptor.OutcomeTypeId}' must provide a compacted narrative projector.");
        }
        if (!descriptors.TryAdd(descriptor.OutcomeTypeId, descriptor))
        {
            throw new InvalidOperationException(
                $"Duplicate gameplay outcome descriptor '{descriptor.OutcomeTypeId}'.");
        }
    }

    private static int CompareOutcomeTypeIds(GameplayOutcomeTypeId left, GameplayOutcomeTypeId right) =>
        string.CompareOrdinal(left.Value, right.Value);
    private static int CompareTypes(Type left, Type right) =>
        string.CompareOrdinal(left?.AssemblyQualifiedName, right?.AssemblyQualifiedName);
}
