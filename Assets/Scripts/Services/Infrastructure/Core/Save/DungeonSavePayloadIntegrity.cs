using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

/// <summary>
/// Infrastructure-level cold-path integrity, not authentication or proof of
/// domain semantics. Hashes bind exact UTF-8 payloads and envelope metadata; no
/// gameplay IDs or payload-specific branches belong here.
/// </summary>
public static class DungeonSavePayloadIntegrity
{
    public const int CurrentVersion = 1;
    private static readonly Encoding StrictUtf8 = new UTF8Encoding(false, true);

    internal static string HashSection(DungeonSaveSectionEnvelope envelope)
    {
        if (envelope == null) throw new ArgumentNullException(nameof(envelope));
        return Hash(writer =>
        {
            writer.Write("dungeon-save-section@1");
            writer.Write(envelope.sectionId ?? string.Empty);
            writer.Write(envelope.sectionVersion);
            writer.Write((int)envelope.restorePhase);
            writer.Write(envelope.optional);
            // Distinguish null from empty even though JSON normally stores text.
            writer.Write(envelope.payloadJson != null);
            writer.Write(envelope.payloadJson ?? string.Empty);
        });
    }

    internal static string HashManifest(DungeonSaveManifestData manifest) => Hash(writer =>
    {
        writer.Write("dungeon-save-snapshot@1");
        writer.Write(manifest.compatibilityGeneration);
        writer.Write(manifest.payloadHashVersion);
        DungeonSaveManifestSectionData[] ordered = (manifest.sections
                ?? new List<DungeonSaveManifestSectionData>())
            .OrderBy(section => section?.sectionId, StringComparer.Ordinal).ToArray();
        writer.Write(ordered.Length);
        foreach (DungeonSaveManifestSectionData section in ordered)
        {
            if (section == null) throw new ArgumentException("Save manifest contains a null section.");
            writer.Write(section.sectionId ?? string.Empty);
            writer.Write(section.sectionVersion);
            writer.Write(section.optional);
            writer.Write(section.payloadSha256 ?? string.Empty);
        }
    });

    internal static bool TryValidate(DungeonSaveManifestData manifest,
        IReadOnlyList<DungeonSaveSectionEnvelope> envelopes, out string reason)
    {
        reason = string.Empty;
        IReadOnlyList<DungeonSaveManifestSectionData> sections = manifest.sections
            ?? (IReadOnlyList<DungeonSaveManifestSectionData>)Array.Empty<DungeonSaveManifestSectionData>();
        if (manifest.payloadHashVersion == 0)
        {
            if (!string.IsNullOrEmpty(manifest.snapshotSha256)
                || sections.Any(section => !string.IsNullOrEmpty(section.payloadSha256)))
            {
                reason = "Save snapshot has partial legacy integrity metadata.";
                return false;
            }
            // The loader must disclose this legacy path; never invent a digest.
            return true;
        }
        if (manifest.payloadHashVersion != CurrentVersion || !CanonicalHash(manifest.snapshotSha256))
        {
            reason = "Save snapshot payload hash version or root digest is invalid.";
            return false;
        }
        Dictionary<string, DungeonSaveSectionEnvelope> byId = (envelopes
            ?? Array.Empty<DungeonSaveSectionEnvelope>()).ToDictionary(
            envelope => envelope.sectionId.Trim(), StringComparer.Ordinal);
        try
        {
            foreach (DungeonSaveManifestSectionData section in sections)
            {
                if (!byId.TryGetValue(section.sectionId.Trim(), out var envelope)
                    || !CanonicalHash(section.payloadSha256)
                    || section.payloadSha256 != HashSection(envelope))
                {
                    reason = "Save snapshot payload digest mismatch: " + section.sectionId;
                    return false;
                }
            }
            if (manifest.snapshotSha256 != HashManifest(manifest))
            {
                reason = "Save snapshot manifest digest mismatch.";
                return false;
            }
        }
        catch (EncoderFallbackException)
        {
            reason = "Save snapshot contains invalid UTF-16 payload text.";
            return false;
        }
        return true;
    }

    private static bool CanonicalHash(string value) => value != null && value.Length == 64
        && value.All(character => character is >= '0' and <= '9' or >= 'a' and <= 'f');

    private static string Hash(Action<BinaryWriter> write)
    {
        using SHA256 sha = SHA256.Create();
        using CryptoStream stream = new(Stream.Null, sha, CryptoStreamMode.Write, leaveOpen: true);
        using BinaryWriter writer = new(stream, StrictUtf8, leaveOpen: true);
        write(writer);
        writer.Flush();
        stream.FlushFinalBlock();
        StringBuilder output = new(64);
        foreach (byte value in sha.Hash) output.Append(value.ToString("x2", CultureInfo.InvariantCulture));
        return output.ToString();
    }
}
