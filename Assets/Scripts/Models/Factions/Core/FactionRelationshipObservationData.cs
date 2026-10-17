using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace DungeonStory.Factions
{
    public enum FactionRelationshipObservationKind { Trust = 1, Betrayal = 2 }

    [Serializable]
    public sealed class FactionRelationshipTrustData
    {
        public string factionId = string.Empty;
        public string factionDisplayName = string.Empty;
        public int previousTrust;
        public int currentTrust;
        public FactionRelationshipTrustData Clone() => (FactionRelationshipTrustData)MemberwiseClone();
    }

    // Frozen owner data and an opaque delivery identity. No native record, score or display DTO.
    [Serializable]
    public sealed class FactionRelationshipObservationData
    {
        public FactionRelationshipObservationKind kind;
        public int sequence;
        public int absoluteDay;
        public string publicationKey = string.Empty;
        public string frozenFingerprint = string.Empty;
        public string ownerIdentity = string.Empty;
        public string factionId = string.Empty;
        public string factionDisplayName = string.Empty;
        public int previousTrust;
        public int currentTrust;
        public string reason = string.Empty;
        public int actualLootValue;
        public List<FactionRelationshipTrustData> transitions = new();

        public string ComputeFingerprint()
        {
            var text = new StringBuilder("faction-relationship-observation@1|");
            void Add(string value)
            {
                if (value == null) throw new InvalidOperationException("faction-relationship-frozen-field-null");
                text.Append(value.Length.ToString(System.Globalization.CultureInfo.InvariantCulture));
                text.Append(':'); text.Append(value);
            }
            string Number(int value) => value.ToString(System.Globalization.CultureInfo.InvariantCulture);
            Add(Number((int)kind)); Add(Number(sequence)); Add(Number(absoluteDay));
            Add(publicationKey); Add(ownerIdentity); Add(factionId); Add(factionDisplayName);
            Add(Number(previousTrust)); Add(Number(currentTrust)); Add(reason); Add(Number(actualLootValue));
            if (transitions == null) throw new InvalidOperationException("faction-relationship-frozen-transitions-null");
            Add(Number(transitions.Count));
            foreach (var item in transitions)
            {
                if (item == null) throw new InvalidOperationException("faction-relationship-frozen-transition-null");
                Add(item.factionId); Add(item.factionDisplayName); Add(Number(item.previousTrust)); Add(Number(item.currentTrust));
            }
            using var hash = SHA256.Create();
            return BitConverter.ToString(hash.ComputeHash(new UTF8Encoding(false, true).GetBytes(text.ToString())))
                .Replace("-", string.Empty).ToLowerInvariant();
        }

        public FactionRelationshipObservationData Clone()
        {
            var copy = (FactionRelationshipObservationData)MemberwiseClone();
            copy.transitions = transitions?.Select(value => value?.Clone()).ToList();
            return copy;
        }
    }

    public static class FactionRelationshipObservationValidation
    {
        public static void Validate(int sequence, IReadOnlyList<FactionRelationshipObservationData> pending,
            int currentDay, IReadOnlyCollection<string> factionIds)
        {
            if (sequence < 0 || pending == null || pending.Count > 1 || currentDay < 1 || factionIds == null)
                throw new InvalidOperationException("faction-relationship-observation-shape-invalid");
            foreach (var value in pending)
            {
                if (value == null || value.sequence <= 0 || value.sequence != sequence
                    || value.absoluteDay < 1 || value.absoluteDay > currentDay
                    || !Canonical(value.publicationKey, false) || !Canonical(value.ownerIdentity, false)
                    || !Canonical(value.factionId, false) || !factionIds.Contains(value.factionId)
                    || value.transitions == null || !Canonical(value.reason, true))
                    throw new InvalidOperationException("faction-relationship-observation-identity-invalid");
                switch (value.kind)
                {
                    case FactionRelationshipObservationKind.Trust:
                        if (value.ownerIdentity != value.factionId || !Canonical(value.factionDisplayName, false)
                            || !Trust(value.previousTrust) || !Trust(value.currentTrust)
                            || value.actualLootValue != 0 || value.transitions.Count != 0)
                            throw new InvalidOperationException("faction-relationship-trust-observation-invalid");
                        break;
                    case FactionRelationshipObservationKind.Betrayal:
                        if (value.ownerIdentity != $"faction-betrayal:{value.factionId}:{value.absoluteDay.ToString(System.Globalization.CultureInfo.InvariantCulture)}"
                            || value.factionDisplayName != string.Empty || value.previousTrust != 0 || value.currentTrust != 0
                            || value.reason != string.Empty || value.actualLootValue < 0 || value.transitions.Count == 0
                            || !value.transitions.Any(item => item?.factionId == value.factionId))
                            throw new InvalidOperationException("faction-relationship-betrayal-observation-invalid");
                        var seen = new HashSet<string>(StringComparer.Ordinal);
                        foreach (var item in value.transitions)
                            if (item == null || !Canonical(item.factionId, false) || !factionIds.Contains(item.factionId)
                                || !seen.Add(item.factionId) || !Canonical(item.factionDisplayName, false)
                                || !Trust(item.previousTrust) || !Trust(item.currentTrust))
                                throw new InvalidOperationException("faction-relationship-transition-invalid");
                        break;
                    default: throw new InvalidOperationException("faction-relationship-observation-kind-invalid");
                }
                if (value.frozenFingerprint?.Length != 64 || !value.frozenFingerprint.All(Uri.IsHexDigit)
                    || value.frozenFingerprint != value.ComputeFingerprint())
                    throw new InvalidOperationException("faction-relationship-frozen-fingerprint-mismatch");
            }
        }
        private static bool Trust(int value) => value is >= -100 and <= 100;
        private static bool Canonical(string value, bool emptyAllowed) => value != null
            && (emptyAllowed || value.Length > 0) && value == value.Trim();
    }
}
