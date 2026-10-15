using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;

/// <summary>
/// Focused production-composition smoke for the action log module registry.
/// It proves that each declared module is the adapter/descriptor instance actually
/// resolved by the gameplay outcome registry, rather than checking parallel inventories.
/// </summary>
public static class GameplayActionLogModuleSmokeScenarios
{
    private const int CurrentActionModuleCount = 149;

    public static string Run(IGameplayOutcomeRegistry registry)
    {
        if (registry == null)
            throw new ArgumentNullException(nameof(registry));

        var verification = new Verification();
        verification.Require(
            registry is IGameplayActionLogRegistry,
            "The production outcome registry does not expose action log modules.");
        var actionRegistry = (IGameplayActionLogRegistry)registry;
        verification.Require(
            actionRegistry.ActionModules != null,
            "The production action module list is missing.");
        verification.Require(
            actionRegistry.ExperienceObservers != null,
            "The production experience observer registry is missing.");

        IGameplayActionLogModule[] modules = actionRegistry.ActionModules.ToArray();
        verification.Require(
            modules.Length == CurrentActionModuleCount,
            "Production action module count differs. Expected "
            + CurrentActionModuleCount + ", actual " + modules.Length + ".");
        verification.Require(
            modules.All(value => value != null),
            "The production action module list contains null.");

        string[] moduleTypes = modules
            .Select(value => value.OutcomeTypeId.Value)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        verification.Require(
            moduleTypes.All(GameplayOutcomeStableIdSyntax.IsValid),
            "An action module has an invalid outcome type.");
        verification.Require(
            moduleTypes.Distinct(StringComparer.Ordinal).Count() == modules.Length,
            "Production action modules contain duplicate outcome types.");

        string[] registeredTypes = TypeNames(registry.RegisteredOutcomeTypes);
        verification.Require(
            registeredTypes.Length == CurrentActionModuleCount,
            "Registered outcome type count differs. Expected "
            + CurrentActionModuleCount + ", actual " + registeredTypes.Length + ".");
        verification.Require(
            registeredTypes.SequenceEqual(moduleTypes, StringComparer.Ordinal),
            DescribeSetMismatch("module/registry", moduleTypes, registeredTypes));

        string[] observerTypes = actionRegistry.ExperienceObservers.SupportedOutcomeTypes
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        verification.Require(
            observerTypes.SequenceEqual(moduleTypes, StringComparer.Ordinal),
            DescribeSetMismatch("module/experience-observer", moduleTypes, observerTypes));

        foreach (IGameplayActionLogModule module in modules
                     .OrderBy(value => value.OutcomeTypeId.Value, StringComparer.Ordinal))
        {
            VerifyModule(registry, module, verification);
        }

        verification.Require(
            Rejects(() => GameplayOutcomeRegistry.FromModules(
                new[] { modules[0], modules[0] })),
            "FromModules accepted the same action module twice.");

        IGameplayActionLogModule first = modules[0];
        IGameplayActionLogModule different = modules.First(value =>
            value.OutcomeTypeId != first.OutcomeTypeId);
        verification.Require(
            Rejects(() => new GameplayActionLogModule(
                first.Adapter,
                first.Descriptor,
                different.ExperienceObserver)),
            "GameplayActionLogModule accepted an observer for a different outcome type.");

        GameplayOutcomeRegistry rebuilt = GameplayOutcomeRegistry.FromModules(modules);
        verification.Require(
            rebuilt is IGameplayActionLogRegistry,
            "The rebuilt outcome registry does not expose action log modules.");
        var rebuiltActions = (IGameplayActionLogRegistry)rebuilt;
        string[] rebuiltTypes = TypeNames(rebuilt.RegisteredOutcomeTypes);
        verification.Require(
            rebuiltTypes.SequenceEqual(moduleTypes, StringComparer.Ordinal),
            DescribeSetMismatch("module/rebuilt-registry", moduleTypes, rebuiltTypes));
        verification.Require(
            TypeNames(rebuiltActions.ActionModules.Select(value => value.OutcomeTypeId))
                .SequenceEqual(moduleTypes, StringComparer.Ordinal),
            "The rebuilt registry changed the action module type set.");
        verification.Require(
            rebuiltActions.ExperienceObservers.SupportedOutcomeTypes
                .OrderBy(value => value, StringComparer.Ordinal)
                .SequenceEqual(moduleTypes, StringComparer.Ordinal),
            "The rebuilt registry changed the experience observer type set.");

        var report = new StringBuilder(256);
        report.AppendLine("Gameplay action log module smoke");
        report.Append("modules: ").Append(modules.Length).AppendLine();
        report.Append("unique outcome types: ").Append(moduleTypes.Length).AppendLine();
        report.Append("registered receipt adapters: ")
            .Append(registry.RegisteredReceiptTypes.Count).AppendLine();
        report.Append("rebuilt outcome types: ").Append(rebuiltTypes.Length).AppendLine();
        report.Append("duplicate module rejected: yes").AppendLine();
        report.Append("mismatched observer rejected: yes").AppendLine();
        report.Append("assertions: ").Append(verification.Count).AppendLine();
        return report.ToString();
    }

    private static void VerifyModule(
        IGameplayOutcomeRegistry registry,
        IGameplayActionLogModule module,
        Verification verification)
    {
        string label = module.OutcomeTypeId.Value;
        verification.Require(
            module.Adapter != null && module.Descriptor != null
            && module.ExperienceObserver != null,
            label + " has an incomplete action module.");
        verification.Require(
            module.OutcomeTypeId == module.Descriptor.OutcomeTypeId,
            label + " module and descriptor types differ.");
        verification.Require(
            module.ExperienceObserver.OutcomeTypes != null
            && module.ExperienceObserver.OutcomeTypes.Count == 1
            && module.ExperienceObserver.OutcomeTypes[0] == label,
            label + " observer must own exactly the module type.");
        verification.Require(
            Supports(module.Adapter, module.OutcomeTypeId),
            label + " adapter does not support the module type.");
        verification.Require(
            registry.TryGetDescriptor(
                module.OutcomeTypeId,
                out IGameplayOutcomeDescriptor registeredDescriptor)
            && ReferenceEquals(module.Descriptor, registeredDescriptor),
            label + " does not resolve to the module descriptor instance.");

        IGameplayOutcomeAdapterRegistration registeredAdapter = ResolveAdapter(
            registry,
            module.Adapter.ReceiptType);
        verification.Require(
            ReferenceEquals(module.Adapter, registeredAdapter),
            label + " does not resolve to the module adapter instance.");
    }

    private static bool Supports(
        IGameplayOutcomeAdapterRegistration adapter,
        GameplayOutcomeTypeId outcomeTypeId) => adapter != null
        && adapter.ReceiptType != null
        && (adapter is IGameplayOutcomeDynamicAdapterRegistration dynamicAdapter
            ? dynamicAdapter.SupportsOutcomeType(outcomeTypeId)
            : adapter.OutcomeTypeId == outcomeTypeId);

    private static IGameplayOutcomeAdapterRegistration ResolveAdapter(
        IGameplayOutcomeRegistry registry,
        Type receiptType)
    {
        if (receiptType == null)
            throw new InvalidOperationException("An action module adapter has no receipt type.");
        MethodInfo method = typeof(IGameplayOutcomeRegistry).GetMethods()
            .Single(value => value.Name == nameof(IGameplayOutcomeRegistry.TryGetAdapter)
                && value.IsGenericMethodDefinition);
        object[] arguments = { null };
        bool found = (bool)method.MakeGenericMethod(receiptType).Invoke(registry, arguments);
        if (!found || arguments[0] is not IGameplayOutcomeAdapterRegistration adapter)
        {
            throw new InvalidOperationException(
                "Registered receipt has no resolvable adapter: " + receiptType.FullName);
        }
        return adapter;
    }

    private static bool Rejects(Action action)
    {
        try
        {
            action();
            return false;
        }
        catch (ArgumentException)
        {
            return true;
        }
        catch (InvalidOperationException)
        {
            return true;
        }
    }

    private static string[] TypeNames(
        IEnumerable<GameplayOutcomeTypeId> outcomeTypes) => (outcomeTypes
            ?? throw new InvalidOperationException("Registered outcome types are missing."))
        .Select(value => value.Value)
        .OrderBy(value => value, StringComparer.Ordinal)
        .ToArray();

    private static string DescribeSetMismatch(
        string label,
        IReadOnlyList<string> expected,
        IReadOnlyList<string> actual) => label + " outcome type sets differ. missing=["
        + string.Join(", ", expected.Except(actual, StringComparer.Ordinal))
        + "] unexpected=["
        + string.Join(", ", actual.Except(expected, StringComparer.Ordinal)) + "]";

    private sealed class Verification
    {
        internal int Count { get; private set; }

        internal void Require(bool condition, string message)
        {
            if (!condition)
                throw new InvalidOperationException(message);
            Count++;
        }
    }
}
