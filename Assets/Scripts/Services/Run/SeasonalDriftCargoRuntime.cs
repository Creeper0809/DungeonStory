using System;
using System.Collections.Generic;
using System.Linq;

[Serializable]
public sealed class SeasonalDriftCargoSaveData
{
    public bool configured;
    public string itemId = string.Empty;
    public int exactQuantity;
    public bool positionFrozen;
    public int positionX;
    public int positionY;
    public bool spawned;
    public List<string> sourceStackIds = new();
    public int securedQuantity;
    public bool expirationCompleted;
    public string dispositionOperationId = string.Empty;
    public string dispositionCommitId = string.Empty;
    public List<string> dispositionSourceStackIds = new();
    public int expiredQuantity;
    public long expiredMassGrams;
    public string lastFailureReason = string.Empty;

    public static SeasonalDriftCargoSaveData FromProfile(
        SeasonalDriftCargoProfile profile) => profile?.IsConfigured == true
        ? new SeasonalDriftCargoSaveData
        {
            configured = true,
            itemId = profile.itemId,
            exactQuantity = profile.exactQuantity
        }
        : new SeasonalDriftCargoSaveData();
}

public static class SeasonalDriftCargoRules
{
    public const string ComponentTypeId = "item-state:seasonal-drift-cargo";
    public const string ExpirationReason = "seasonal-drift-cargo-world-exit";
    private const string OccurrenceKey = "occurrence-id";
    private const string SecuredKey = "secured-after-pickup";

    public static ItemInstanceComponentSaveData CreateComponent(
        string occurrenceInstanceId,
        bool secured) => new()
    {
        componentTypeId = ComponentTypeId,
        schemaVersion = 1,
        affectsStacking = true,
        values = new List<ItemStateValueSaveData>
        {
            new()
            {
                key = OccurrenceKey,
                kind = ItemStateValueKind.String,
                stringValue = occurrenceInstanceId
            },
            new()
            {
                key = SecuredKey,
                kind = ItemStateValueKind.Boolean,
                booleanValue = secured
            }
        }
    };

    public static bool TryReadComponent(
        WorldItemStackSnapshot stack,
        out string occurrenceInstanceId,
        out bool secured)
    {
        occurrenceInstanceId = string.Empty;
        secured = false;
        ItemInstanceComponentSaveData[] matches = (stack?.Components
                ?? Array.Empty<ItemInstanceComponentSaveData>())
            .Where(value => value != null
                && string.Equals(
                    value.componentTypeId,
                    ComponentTypeId,
                    StringComparison.Ordinal))
            .ToArray();
        if (matches.Length != 1 || matches[0].schemaVersion != 1)
            return false;
        ItemStateValueSaveData occurrence = matches[0].values
            ?.SingleOrDefault(value => value != null
                && string.Equals(value.key, OccurrenceKey, StringComparison.Ordinal));
        ItemStateValueSaveData securedValue = matches[0].values
            ?.SingleOrDefault(value => value != null
                && string.Equals(value.key, SecuredKey, StringComparison.Ordinal));
        if (occurrence?.kind != ItemStateValueKind.String
            || securedValue?.kind != ItemStateValueKind.Boolean
            || !Canonical(occurrence.stringValue))
            return false;
        occurrenceInstanceId = occurrence.stringValue;
        secured = securedValue.booleanValue;
        return true;
    }

    public static void RequireValidFrozenState(
        SeasonalDriftCargoSaveData state,
        SeasonalDriftCargoProfile authored,
        string occurrenceInstanceId)
    {
        state ??= new SeasonalDriftCargoSaveData();
        bool authoredConfigured = authored?.IsConfigured == true;
        if (!authoredConfigured)
        {
            if (HasAnyState(state))
                throw new InvalidOperationException(
                    $"Seasonal event '{occurrenceInstanceId}' contains unauthored drift cargo.");
            return;
        }
        if (!state.configured
            || !string.Equals(state.itemId, authored.itemId, StringComparison.Ordinal)
            || state.exactQuantity != authored.exactQuantity
            || state.sourceStackIds == null
            || state.dispositionSourceStackIds == null
            || state.securedQuantity < 0
            || state.expiredQuantity < 0
            || state.securedQuantity + state.expiredQuantity > state.exactQuantity
            || state.expiredMassGrams < 0L
            || state.spawned != (state.sourceStackIds.Count > 0)
            || state.spawned && !state.positionFrozen
            || state.sourceStackIds.Any(value => !Canonical(value))
            || state.sourceStackIds.Distinct(StringComparer.Ordinal).Count()
                != state.sourceStackIds.Count
            || state.dispositionSourceStackIds.Any(value => !Canonical(value))
            || state.dispositionSourceStackIds.Distinct(StringComparer.Ordinal).Count()
                != state.dispositionSourceStackIds.Count
            || !CanonicalOptionalFailure(state.lastFailureReason))
        {
            throw new InvalidOperationException(
                $"Seasonal event '{occurrenceInstanceId}' drift-cargo state is invalid.");
        }
        bool hasDisposition = !string.IsNullOrEmpty(state.dispositionOperationId)
            || !string.IsNullOrEmpty(state.dispositionCommitId)
            || state.dispositionSourceStackIds.Count > 0
            || state.expiredQuantity > 0
            || state.expiredMassGrams > 0L;
        if (hasDisposition
            && (!string.Equals(
                    state.dispositionOperationId,
                    ExpirationOperationId(occurrenceInstanceId),
                    StringComparison.Ordinal)
                || !Canonical(state.dispositionCommitId)
                || state.dispositionSourceStackIds.Count == 0
                || state.expiredQuantity <= 0
                || state.expiredMassGrams <= 0L))
        {
            throw new InvalidOperationException(
                $"Seasonal event '{occurrenceInstanceId}' drift-cargo disposition receipt is invalid.");
        }
    }

    public static string ExpirationOperationId(string occurrenceInstanceId) =>
        $"seasonal-drift-cargo-expire:{occurrenceInstanceId}";

    private static bool HasAnyState(SeasonalDriftCargoSaveData state) =>
        state.configured
        || !string.IsNullOrEmpty(state.itemId)
        || state.exactQuantity != 0
        || state.positionFrozen
        || state.positionX != 0
        || state.positionY != 0
        || state.spawned
        || (state.sourceStackIds?.Count ?? 0) > 0
        || state.securedQuantity != 0
        || state.expirationCompleted
        || !string.IsNullOrEmpty(state.dispositionOperationId)
        || !string.IsNullOrEmpty(state.dispositionCommitId)
        || (state.dispositionSourceStackIds?.Count ?? 0) > 0
        || state.expiredQuantity != 0
        || state.expiredMassGrams != 0L
        || !string.IsNullOrEmpty(state.lastFailureReason);

    private static bool Canonical(string value) =>
        !string.IsNullOrWhiteSpace(value)
        && string.Equals(value, value.Trim(), StringComparison.Ordinal);

    private static bool CanonicalOptionalFailure(string value) =>
        value != null
        && value.Length <= 256
        && (value.Length == 0 || Canonical(value));
}
