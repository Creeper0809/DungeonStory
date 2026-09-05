using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace DungeonStory.Balance
{
    public readonly struct ProductionOutputCostAllocationWeight
    {
        public ProductionOutputCostAllocationWeight(string outputLineId, long weight)
        {
            OutputLineId = outputLineId ?? throw new ArgumentNullException(nameof(outputLineId));
            Weight = weight;
        }

        public string OutputLineId { get; }
        public long Weight { get; }
    }

    public interface IProductionOutputCostAllocationCapability
    {
        string CapabilityId { get; }
        int ContractVersion { get; }

        IReadOnlyList<ProductionOutputCostAllocationWeight> ResolveWeights(
            ProductionOutputCostAllocationAuthoringSnapshot authoring,
            IReadOnlyList<ProductionOutputDefinition> outputs);
    }

    public sealed class ProductionOutputCostAllocationCapabilityRegistry
    {
        private readonly Dictionary<string, IProductionOutputCostAllocationCapability>
            capabilities;

        public ProductionOutputCostAllocationCapabilityRegistry(
            IEnumerable<IProductionOutputCostAllocationCapability> source)
        {
            capabilities = new Dictionary<string,
                IProductionOutputCostAllocationCapability>(StringComparer.Ordinal);
            foreach (IProductionOutputCostAllocationCapability capability in
                     (source ?? throw new ArgumentNullException(nameof(source)))
                     .OrderBy(value => value.CapabilityId, StringComparer.Ordinal))
            {
                string key = Key(capability.CapabilityId, capability.ContractVersion);
                if (!capabilities.TryAdd(key, capability))
                    throw new InvalidOperationException(
                        $"Duplicate output-cost allocation capability '{key}'.");
            }
        }

        public static ProductionOutputCostAllocationCapabilityRegistry CreateDefault() =>
            new(new IProductionOutputCostAllocationCapability[]
            {
                new WeightedOutputShareProductionOutputCostAllocationCapability()
            });

        public IReadOnlyList<ProductionOutputCostAllocationWeight> ResolveWeights(
            ProductionOutputCostAllocationAuthoringSnapshot authoring,
            IReadOnlyList<ProductionOutputDefinition> outputs)
        {
            if (authoring.IsEmpty)
                throw new InvalidOperationException(
                    "A multi-output recipe requires explicit output-cost allocation authoring.");
            string key = Key(authoring.CapabilityId, authoring.ContractVersion);
            if (!capabilities.TryGetValue(key, out IProductionOutputCostAllocationCapability capability))
                throw new InvalidOperationException(
                    $"Unknown output-cost allocation capability '{key}'.");
            return capability.ResolveWeights(authoring, outputs);
        }

        private static string Key(string id, int version) =>
            string.Concat(id ?? string.Empty, "@", version.ToString(CultureInfo.InvariantCulture));
    }

    /// <summary>
    /// Splits one batch debit by authored, market-independent integer weights.
    /// Returned packaging may have zero weight; every other physical output must
    /// have positive weight. Rounding remainder is assigned to the unique Main.
    /// </summary>
    public sealed class WeightedOutputShareProductionOutputCostAllocationCapability :
        IProductionOutputCostAllocationCapability
    {
        public const string Id = "weighted-output-share";
        public const int Version = 1;

        public string CapabilityId => Id;
        public int ContractVersion => Version;

        public IReadOnlyList<ProductionOutputCostAllocationWeight> ResolveWeights(
            ProductionOutputCostAllocationAuthoringSnapshot authoring,
            IReadOnlyList<ProductionOutputDefinition> outputs)
        {
            if (authoring.CapabilityId != Id || authoring.ContractVersion != Version)
                throw new InvalidOperationException("Weighted output-share contract mismatch.");
            ProductionOutputDefinition[] physical = CapturePhysicalOutputs(outputs);
            Dictionary<string, long> parsed = ParsePayload(authoring.CanonicalPayload);
            if (parsed.Count != physical.Length)
                throw new InvalidOperationException(
                    "Output-cost allocation must name every physical output exactly once.");

            int mainCount = physical.Count(value => value.Role == ProductionOutputRole.Main);
            if (mainCount != 1)
                throw new InvalidOperationException(
                    "Weighted output-share allocation requires exactly one Main output.");

            var resolved = new ProductionOutputCostAllocationWeight[physical.Length];
            long positiveWeight = 0L;
            for (int index = 0; index < physical.Length; index++)
            {
                ProductionOutputDefinition output = physical[index];
                if (!parsed.TryGetValue(output.OutputLineId, out long weight))
                    throw new InvalidOperationException(
                        $"Missing output-cost weight for '{output.OutputLineId}'.");
                bool returnedPackaging =
                    output.Role == ProductionOutputRole.ReturnedPackaging;
                if ((returnedPackaging && weight != 0L)
                    || (!returnedPackaging && weight <= 0L))
                {
                    throw new InvalidOperationException(
                        $"Invalid output-cost weight for '{output.OutputLineId}'.");
                }
                positiveWeight = checked(positiveWeight + weight);
                resolved[index] = new ProductionOutputCostAllocationWeight(
                    output.OutputLineId,
                    weight);
            }
            if (positiveWeight <= 0L)
                throw new InvalidOperationException("Output-cost weight total must be positive.");
            return Array.AsReadOnly(resolved);
        }

        public static string BuildPayload(
            IEnumerable<ProductionOutputDefinition> outputs)
        {
            ProductionOutputDefinition[] physical = CapturePhysicalOutputs(outputs);
            if (physical.Count(value => value.Role == ProductionOutputRole.Main) != 1)
                throw new InvalidOperationException(
                    "Weighted output-share authoring requires exactly one Main output.");
            return string.Join(";", physical.Select(output => string.Concat(
                output.OutputLineId,
                "=",
                (output.Role == ProductionOutputRole.ReturnedPackaging
                    ? 0L
                    : (long)output.Amount).ToString(CultureInfo.InvariantCulture))));
        }

        private static ProductionOutputDefinition[] CapturePhysicalOutputs(
            IEnumerable<ProductionOutputDefinition> outputs) =>
            (outputs ?? throw new ArgumentNullException(nameof(outputs)))
            .Where(value => value != null
                && ProductionOutputRoleRules.IsPhysical(value.Role)
                && value.Probability > 0f)
            .OrderBy(value => value.OutputLineId, StringComparer.Ordinal)
            .ToArray();

        private static Dictionary<string, long> ParsePayload(string payload)
        {
            if (string.IsNullOrWhiteSpace(payload)
                || !string.Equals(payload, payload.Trim(), StringComparison.Ordinal))
                throw new InvalidOperationException("Output-cost payload is noncanonical.");
            var result = new Dictionary<string, long>(StringComparer.Ordinal);
            string previous = null;
            foreach (string token in payload.Split(';'))
            {
                int separator = token.LastIndexOf('=');
                if (separator <= 0 || separator == token.Length - 1)
                    throw new InvalidOperationException("Output-cost payload token is malformed.");
                string lineId = token.Substring(0, separator);
                string weightToken = token.Substring(separator + 1);
                if (previous != null
                    && StringComparer.Ordinal.Compare(previous, lineId) >= 0)
                    throw new InvalidOperationException("Output-cost payload is not ordinal sorted.");
                if (!long.TryParse(weightToken, NumberStyles.None,
                        CultureInfo.InvariantCulture, out long weight)
                    || weight < 0L
                    || weight > int.MaxValue
                    || !result.TryAdd(lineId, weight))
                    throw new InvalidOperationException("Output-cost payload weight is invalid.");
                previous = lineId;
            }
            return result;
        }
    }
}
