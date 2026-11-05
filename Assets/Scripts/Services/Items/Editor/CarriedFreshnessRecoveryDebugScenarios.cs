#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;

public static class CarriedFreshnessRecoveryDebugScenarios
{
    private const string ItemId = "food:qa:freshness-recovery";
    private const string UnknownComponentId = "item-state:qa-unknown";

    public static void VerifyForBatch()
    {
        VerifyExactComponentsClone();
        VerifyMonotoneFreshnessDecay();
        VerifyFreshnessIncreaseRejected();
        VerifyPreservationChangeRejected();
        VerifyUnrelatedComponentChangeRejected();
        VerifyEquipmentStateChangeRejected();
        VerifyMissingAndMalformedFreshnessRejected();
        VerifyUnknownComponentSurvives();
        VerifyCustodyLikeComponentStaysExact();
    }

    private static void VerifyExactComponentsClone()
    {
        List<ItemInstanceComponentSaveData> physical = new()
        {
            FoodFreshnessComponentCodec.Create(100d, preserved: false),
            CreateOpaque(UnknownComponentId, "stable")
        };
        List<ItemInstanceComponentSaveData> carried = Clone(physical);
        string physicalBefore = Fingerprint(physical);

        Require(ItemTransferService.TryResolveRecoveryComponents(
                physical,
                carried,
                ItemId,
                out List<ItemInstanceComponentSaveData> resolved),
            "Exact carried components were rejected.");
        Require(Fingerprint(resolved) == Fingerprint(carried),
            "Exact recovery did not preserve the carried component values.");
        Require(Fingerprint(physical) == physicalBefore,
            "Exact recovery mutated the physical component snapshot.");
        RequireDetached(resolved, physical, carried);
    }

    private static void VerifyMonotoneFreshnessDecay()
    {
        List<ItemInstanceComponentSaveData> physical = new()
        {
            FoodFreshnessComponentCodec.Create(100d, preserved: true)
        };
        ItemInstanceComponentSaveData physicalFreshness = physical[0];
        List<ItemInstanceComponentSaveData> carried = new()
        {
            FoodFreshnessComponentCodec.Create(75d, preserved: true)
        };
        string physicalBefore = Fingerprint(physical);

        Require(ItemTransferService.TryResolveRecoveryComponents(
                physical,
                carried,
                ItemId,
                out List<ItemInstanceComponentSaveData> resolved),
            "Monotone carried freshness decay was rejected.");
        Require(FoodFreshnessComponentCodec.TryRead(
                resolved,
                out double remaining,
                out bool preserved)
                && remaining == 75d
                && preserved,
            "Recovery did not select the carried freshness state.");
        Require(Fingerprint(physical) == physicalBefore
                && ReferenceEquals(physical[0], physicalFreshness),
            "Freshness reconciliation mutated the physical component snapshot.");
        RequireDetached(resolved, physical, carried);
    }

    private static void VerifyFreshnessIncreaseRejected()
    {
        RequireRejected(
            new[] { FoodFreshnessComponentCodec.Create(75d, preserved: false) },
            new[] { FoodFreshnessComponentCodec.Create(100d, preserved: false) },
            "A carried freshness increase was accepted.");
    }

    private static void VerifyPreservationChangeRejected()
    {
        RequireRejected(
            new[] { FoodFreshnessComponentCodec.Create(100d, preserved: false) },
            new[] { FoodFreshnessComponentCodec.Create(75d, preserved: true) },
            "A carried preservation flag change was accepted.");
    }

    private static void VerifyUnrelatedComponentChangeRejected()
    {
        RequireRejected(
            new[]
            {
                FoodFreshnessComponentCodec.Create(100d, preserved: false),
                CreateOpaque(UnknownComponentId, "original")
            },
            new[]
            {
                FoodFreshnessComponentCodec.Create(75d, preserved: false),
                CreateOpaque(UnknownComponentId, "changed")
            },
            "A non-freshness component change was accepted with valid decay.");
    }

    private static void VerifyEquipmentStateChangeRejected()
    {
        RequireRejected(
            new[]
            {
                FoodFreshnessComponentCodec.Create(100d, preserved: false),
                CreateOpaque(ItemInstanceComponentIds.Equipment, "equipped:main")
            },
            new[]
            {
                FoodFreshnessComponentCodec.Create(75d, preserved: false),
                CreateOpaque(ItemInstanceComponentIds.Equipment, "equipped:offhand")
            },
            "An equipment-like component change was accepted with valid decay.");
    }

    private static void VerifyMissingAndMalformedFreshnessRejected()
    {
        ItemInstanceComponentSaveData stable =
            CreateOpaque(UnknownComponentId, "stable");
        RequireRejected(
            new[]
            {
                FoodFreshnessComponentCodec.Create(100d, preserved: false),
                stable
            },
            new[] { stable.Clone() },
            "Missing carried freshness was accepted as decay.");

        ItemInstanceComponentSaveData malformed =
            FoodFreshnessComponentCodec.Create(75d, preserved: false);
        malformed.schemaVersion = FoodFreshnessComponentCodec.SchemaVersion + 1;
        RequireRejected(
            new[] { FoodFreshnessComponentCodec.Create(100d, preserved: false) },
            new[] { malformed },
            "Malformed carried freshness was accepted as decay.");
    }

    private static void VerifyUnknownComponentSurvives()
    {
        ItemInstanceComponentSaveData physicalUnknown =
            CreateOpaque(UnknownComponentId, "future-data");
        ItemInstanceComponentSaveData carriedUnknown = physicalUnknown.Clone();
        List<ItemInstanceComponentSaveData> physical = new()
        {
            FoodFreshnessComponentCodec.Create(100d, preserved: false),
            physicalUnknown
        };
        List<ItemInstanceComponentSaveData> carried = new()
        {
            FoodFreshnessComponentCodec.Create(75d, preserved: false),
            carriedUnknown
        };

        Require(ItemTransferService.TryResolveRecoveryComponents(
                physical,
                carried,
                ItemId,
                out List<ItemInstanceComponentSaveData> resolved),
            "An unchanged unknown component blocked valid decay.");
        ItemInstanceComponentSaveData resolvedUnknown = resolved.Single(value =>
            value.componentTypeId == UnknownComponentId);
        Require(resolvedUnknown.ToCanonicalString()
                == carriedUnknown.ToCanonicalString(),
            "Recovery did not preserve an unchanged unknown component.");
        RequireDetached(resolved, physical, carried);
    }

    private static void VerifyCustodyLikeComponentStaysExact()
    {
        ItemInstanceComponentSaveData physicalCustody = CreateOpaque(
            FacilityOutputExactRouteCustodyCodec.ComponentTypeId,
            "route-a",
            FacilityOutputExactRouteCustodyCodec.SchemaVersion);
        ItemInstanceComponentSaveData carriedCustody = physicalCustody.Clone();
        List<ItemInstanceComponentSaveData> physical = new()
        {
            FoodFreshnessComponentCodec.Create(100d, preserved: true),
            physicalCustody
        };
        List<ItemInstanceComponentSaveData> carried = new()
        {
            FoodFreshnessComponentCodec.Create(75d, preserved: true),
            carriedCustody
        };

        Require(ItemTransferService.TryResolveRecoveryComponents(
                physical,
                carried,
                ItemId,
                out List<ItemInstanceComponentSaveData> resolved),
            "Unchanged custody-like data blocked valid decay.");
        Require(resolved.Single(value => value.componentTypeId
                    == FacilityOutputExactRouteCustodyCodec.ComponentTypeId)
                .ToCanonicalString() == carriedCustody.ToCanonicalString(),
            "Recovery did not preserve unchanged custody-like data.");
        RequireDetached(resolved, physical, carried);

        List<ItemInstanceComponentSaveData> changedCustody = Clone(carried);
        changedCustody.Single(value => value.componentTypeId
                == FacilityOutputExactRouteCustodyCodec.ComponentTypeId)
            .values[0].stringValue = "route-b";
        RequireRejected(
            physical,
            changedCustody,
            "Changed custody-like data was accepted with valid decay.");
    }

    private static ItemInstanceComponentSaveData CreateOpaque(
        string componentTypeId,
        string payload,
        int schemaVersion = 1)
    {
        return new ItemInstanceComponentSaveData
        {
            componentTypeId = componentTypeId,
            schemaVersion = schemaVersion,
            affectsStacking = true,
            values = new List<ItemStateValueSaveData>
            {
                new()
                {
                    key = "payload",
                    kind = ItemStateValueKind.String,
                    stringValue = payload
                }
            }
        };
    }

    private static List<ItemInstanceComponentSaveData> Clone(
        IEnumerable<ItemInstanceComponentSaveData> source)
    {
        return source.Select(value => value.Clone()).ToList();
    }

    private static string Fingerprint(
        IEnumerable<ItemInstanceComponentSaveData> source)
    {
        return string.Join("|", source.Select(value =>
            $"{value.affectsStacking}:{value.ToCanonicalString()}"));
    }

    private static void RequireRejected(
        IReadOnlyList<ItemInstanceComponentSaveData> physical,
        IReadOnlyList<ItemInstanceComponentSaveData> carried,
        string message)
    {
        string physicalBefore = Fingerprint(physical);
        Require(!ItemTransferService.TryResolveRecoveryComponents(
                    physical,
                    carried,
                    ItemId,
                    out List<ItemInstanceComponentSaveData> resolved)
                && resolved == null,
            message);
        Require(Fingerprint(physical) == physicalBefore,
            "Rejected reconciliation mutated the physical component snapshot.");
    }

    private static void RequireDetached(
        IReadOnlyList<ItemInstanceComponentSaveData> resolved,
        IReadOnlyList<ItemInstanceComponentSaveData> physical,
        IReadOnlyList<ItemInstanceComponentSaveData> carried)
    {
        Require(!ReferenceEquals(resolved, physical)
                && !ReferenceEquals(resolved, carried),
            "Recovery returned a source component list.");
        ItemInstanceComponentSaveData[] sourceComponents = physical
            .Concat(carried)
            .Where(value => value != null)
            .ToArray();
        ItemStateValueSaveData[] sourceValues = sourceComponents
            .SelectMany(value => value.values
                ?? new List<ItemStateValueSaveData>())
            .Where(value => value != null)
            .ToArray();
        foreach (ItemInstanceComponentSaveData component in resolved)
        {
            Require(sourceComponents.All(value =>
                    !ReferenceEquals(value, component)),
                "Recovery reused a source component object.");
            Require(sourceComponents.All(value =>
                    !ReferenceEquals(value.values, component.values)),
                "Recovery reused a source component value list.");
            foreach (ItemStateValueSaveData value in component.values)
            {
                Require(sourceValues.All(source =>
                        !ReferenceEquals(source, value)),
                    "Recovery reused a source component value object.");
            }
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
#endif
