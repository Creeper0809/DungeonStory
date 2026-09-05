using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Immutable, exact six-participant registry. Journal persistence binds to
/// its fingerprint, while runtime execution uses the dependency-derived order.
/// Persisted participant rows remain ID-ordinal and never use execution order.
/// </summary>
public sealed class ProductionFacilityDestructiveDrainParticipantRegistry :
    IProductionFacilityDestructiveDrainParticipantRegistry
{
    public const string Schema =
        "production-facility-destructive-drain-participant-registry@3";
    public const string ExpectedRegistryFingerprint =
        "2a12b255807935361d7326d21e5d533f8802c6825332343aa03471a8033aff58";

    private static readonly IReadOnlyDictionary<string, ParticipantSpec>
        Required = new Dictionary<string, ParticipantSpec>(
            StringComparer.Ordinal)
        {
            [ProductionFacilityDestructiveDrainParticipantIds
                .ApparelWorkOrders] = new(
                    1,
                    Array.Empty<string>()),
            [ProductionFacilityDestructiveDrainParticipantIds
                .CapacityRoutingOutbox] = new(
                    1,
                    new[]
                    {
                        ProductionFacilityDestructiveDrainParticipantIds
                            .ApparelWorkOrders,
                        ProductionFacilityDestructiveDrainParticipantIds
                            .CombatEquipmentCrafting,
                        ProductionFacilityDestructiveDrainParticipantIds
                            .GenericProductionBills
                    }),
            [ProductionFacilityDestructiveDrainParticipantIds
                .CombatEquipmentCrafting] = new(
                    1,
                    Array.Empty<string>()),
            [ProductionFacilityDestructiveDrainParticipantIds
                .GenericProductionBills] = new(
                    1,
                    Array.Empty<string>()),
            [ProductionFacilityDestructiveDrainParticipantIds
                .PhysicalCustodyCarryRecovery] = new(
                    1,
                    new[]
                    {
                        ProductionFacilityDestructiveDrainParticipantIds
                            .CapacityRoutingOutbox
                    }),
            [ProductionFacilityDestructiveDrainParticipantIds
                .StockSensorEmbeddedSalvage] = new(
                    2,
                    new[]
                    {
                        ProductionFacilityDestructiveDrainParticipantIds
                            .PhysicalCustodyCarryRecovery
                    })
        };

    private readonly IReadOnlyDictionary<string,
        IProductionFacilityDestructiveDrainParticipant> byId;

    public ProductionFacilityDestructiveDrainParticipantRegistry(
        IEnumerable<IProductionFacilityDestructiveDrainParticipant>
            participants)
    {
        IProductionFacilityDestructiveDrainParticipant[] source =
            (participants
                ?? throw new ArgumentNullException(nameof(participants)))
            .ToArray();
        Dictionary<string, IProductionFacilityDestructiveDrainParticipant>
            collected = new(StringComparer.Ordinal);
        Dictionary<string, string[]> dependencies =
            new(StringComparer.Ordinal);

        foreach (IProductionFacilityDestructiveDrainParticipant participant in
                 source)
        {
            if (participant == null
                || !ProductionFacilityDestructiveDrainCanonical
                    .IsCanonicalToken(participant.ParticipantId)
                || participant.ContractVersion <= 0)
            {
                throw new InvalidOperationException(
                    "Destructive-drain participant registry contains an invalid participant.");
            }
            if (!collected.TryAdd(participant.ParticipantId, participant))
            {
                throw new InvalidOperationException(
                    "Duplicate destructive-drain participant: "
                    + participant.ParticipantId);
            }

            string[] declared = (participant.DependsOnParticipantIds
                    ?? throw new InvalidOperationException(
                        "Destructive-drain participant dependency list is null: "
                        + participant.ParticipantId))
                .ToArray();
            if (declared.Any(value =>
                    !ProductionFacilityDestructiveDrainCanonical
                        .IsCanonicalToken(value))
                || declared.Distinct(StringComparer.Ordinal).Count()
                    != declared.Length
                || declared.Contains(
                    participant.ParticipantId,
                    StringComparer.Ordinal))
            {
                throw new InvalidOperationException(
                    "Destructive-drain participant dependencies are invalid: "
                    + participant.ParticipantId);
            }
            dependencies.Add(
                participant.ParticipantId,
                declared.OrderBy(value => value, StringComparer.Ordinal)
                    .ToArray());
        }

        RequireExactParticipantSet(collected);
        foreach (KeyValuePair<string, string[]> pair in dependencies)
        {
            foreach (string dependency in pair.Value)
            {
                if (!collected.ContainsKey(dependency))
                {
                    throw new InvalidOperationException(
                        "Unknown destructive-drain participant dependency: "
                        + pair.Key + " -> " + dependency);
                }
            }
        }

        IProductionFacilityDestructiveDrainParticipant[] execution =
            BuildExecutionOrder(collected, dependencies);
        RequireExactContracts(collected, dependencies);
        byId = collected;
        ExecutionOrder = Array.AsReadOnly(execution);
        RegistryFingerprint = ComputeFingerprint(collected, dependencies);
        if (!string.Equals(
                RegistryFingerprint,
                ExpectedRegistryFingerprint,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Destructive-drain participant registry fingerprint drifted from its current-format contract.");
        }
    }

    public string RegistryFingerprint { get; }

    public IReadOnlyList<IProductionFacilityDestructiveDrainParticipant>
        ExecutionOrder { get; }

    public bool TryGet(
        string participantId,
        out IProductionFacilityDestructiveDrainParticipant participant) =>
        byId.TryGetValue(participantId ?? string.Empty, out participant);

    internal static bool TryGetRequiredContractVersion(
        string participantId,
        out int contractVersion)
    {
        if (Required.TryGetValue(
                participantId ?? string.Empty,
                out ParticipantSpec spec))
        {
            contractVersion = spec.ContractVersion;
            return true;
        }

        contractVersion = 0;
        return false;
    }

    private static void RequireExactParticipantSet(
        IReadOnlyDictionary<string,
            IProductionFacilityDestructiveDrainParticipant> collected)
    {
        string[] actual = collected.Keys
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        string[] expected = Required.Keys
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        if (!actual.SequenceEqual(expected, StringComparer.Ordinal))
        {
            throw new InvalidOperationException(
                "Destructive-drain participant registry does not contain the exact required six IDs.");
        }
    }

    private static void RequireExactContracts(
        IReadOnlyDictionary<string,
            IProductionFacilityDestructiveDrainParticipant> collected,
        IReadOnlyDictionary<string, string[]> dependencies)
    {
        foreach (KeyValuePair<string, ParticipantSpec> pair in Required)
        {
            IProductionFacilityDestructiveDrainParticipant participant =
                collected[pair.Key];
            if (participant.ContractVersion != pair.Value.ContractVersion
                || !dependencies[pair.Key].SequenceEqual(
                    pair.Value.Dependencies,
                    StringComparer.Ordinal))
            {
                throw new InvalidOperationException(
                    "Destructive-drain participant contract or dependency DAG drifted: "
                    + pair.Key);
            }
        }
    }

    private static IProductionFacilityDestructiveDrainParticipant[]
        BuildExecutionOrder(
            IReadOnlyDictionary<string,
                IProductionFacilityDestructiveDrainParticipant> collected,
            IReadOnlyDictionary<string, string[]> dependencies)
    {
        Dictionary<string, int> remaining = dependencies.ToDictionary(
            pair => pair.Key,
            pair => pair.Value.Length,
            StringComparer.Ordinal);
        Dictionary<string, List<string>> consumers = collected.Keys
            .ToDictionary(
                value => value,
                _ => new List<string>(),
                StringComparer.Ordinal);
        foreach (KeyValuePair<string, string[]> pair in dependencies)
        {
            foreach (string dependency in pair.Value)
                consumers[dependency].Add(pair.Key);
        }

        SortedSet<string> ready = new(
            remaining.Where(pair => pair.Value == 0)
                .Select(pair => pair.Key),
            StringComparer.Ordinal);
        List<IProductionFacilityDestructiveDrainParticipant> ordered = new();
        while (ready.Count > 0)
        {
            string next = ready.Min;
            ready.Remove(next);
            ordered.Add(collected[next]);
            foreach (string consumer in consumers[next]
                         .OrderBy(value => value, StringComparer.Ordinal))
            {
                int count = --remaining[consumer];
                if (count == 0)
                    ready.Add(consumer);
            }
        }

        if (ordered.Count != collected.Count)
        {
            throw new InvalidOperationException(
                "Destructive-drain participant dependency graph contains a cycle.");
        }
        return ordered.ToArray();
    }

    private static string ComputeFingerprint(
        IReadOnlyDictionary<string,
            IProductionFacilityDestructiveDrainParticipant> collected,
        IReadOnlyDictionary<string, string[]> dependencies)
    {
        CanonicalSemanticDigestBuilder digest = new();
        digest.Append(Schema);
        digest.Append(collected.Count);
        foreach (string id in collected.Keys
                     .OrderBy(value => value, StringComparer.Ordinal))
        {
            digest.Append(id);
            digest.Append(collected[id].ContractVersion);
            digest.Append(dependencies[id].Length);
            foreach (string dependency in dependencies[id])
                digest.Append(dependency);
        }
        return digest.ComputeSha256();
    }

    private readonly struct ParticipantSpec
    {
        internal ParticipantSpec(
            int contractVersion,
            IReadOnlyList<string> dependencies)
        {
            ContractVersion = contractVersion;
            Dependencies = (dependencies ?? Array.Empty<string>())
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();
        }

        internal int ContractVersion { get; }
        internal string[] Dependencies { get; }
    }
}
