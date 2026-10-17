using System;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

internal static class GameplayOutcomeCanonicalHash
{
    public const int Sha256ByteCount = 32;

    public static string HashExact(in GameplayOutcomeReadView view)
    {
        StringBuilder builder = new StringBuilder(512);
        Append(builder, view.OutcomeId.RunId.Value);
        Append(builder, view.OutcomeId.Sequence);
        Append(builder, view.ResultKey.ProducerId);
        Append(builder, view.OperationId.Value);
        Append(builder, view.ResultKey.CommitRevision);
        Append(builder, view.ResultKey.LocalResultIndex);
        Append(builder, view.OwnerRevision);
        Append(builder, view.OutcomeTypeId.Value);
        Append(builder, view.AbsoluteDay);
        Append(builder, (int)view.Status);
        GameplayLocationReference location = view.Location;
        Append(builder, location.LocationId);
        Append(builder, location.RoomId);
        Append(builder, location.X);
        Append(builder, location.Y);
        GameplayOutcomeCausation causation = view.Causation;
        Append(builder, causation.ParentOutcomeId.RunId.Value);
        Append(builder, causation.ParentOutcomeId.Sequence);
        Append(builder, causation.RootOperationId.Value);
        Append(builder, causation.RelationId);
        Append(builder, view.ParticipantCount);
        for (int index = 0; index < view.ParticipantCount; index++)
        {
            GameplayOutcomeParticipant value = view.GetParticipant(index);
            Append(builder, value.EntityId.Kind.Value);
            Append(builder, value.EntityId.Value);
            Append(builder, value.RoleId.Value);
            Append(builder, (int)value.ParticipationKind);
            Append(builder, value.HasPerceptionEvidence ? 1 : 0);
            Append(builder, value.DisplayName.DisplayText);
            Append(builder, value.DisplayName.DisplaySnapshotRevision);
            Append(builder, value.DisplayName.Locale);
            Append(builder, (int)value.DisplayName.PronunciationHint.Mode);
            Append(builder, value.DisplayName.PronunciationHint.Value);
            Append(builder, (int)value.DisplayName.PronunciationHint.ExplicitFinalConsonant);
            Append(builder, value.DisplayName.PronunciationHint.Revision);
        }
        Append(builder, view.MetricCount);
        for (int index = 0; index < view.MetricCount; index++)
        {
            GameplayOutcomeMetric value = view.GetMetric(index);
            Append(builder, value.MetricId.Value);
            Append(builder, value.Value.ToString("R", CultureInfo.InvariantCulture));
            Append(builder, value.UnitId.Value);
            Append(builder, value.DefinitionOrInstanceId.Kind.Value);
            Append(builder, value.DefinitionOrInstanceId.Value);
        }
        Append(builder, view.TagCount);
        for (int index = 0; index < view.TagCount; index++)
            Append(builder, view.GetTag(index).Value);
        Append(builder, view.ProvenanceCount);
        for (int index = 0; index < view.ProvenanceCount; index++)
        {
            GameplayOutcomeProvenanceReference provenance = view.GetProvenance(index);
            Append(builder, provenance.KindId);
            Append(builder, provenance.Value);
        }
        Append(builder, view.FactCount);
        for (int index = 0; index < view.FactCount; index++)
        {
            GameplayOutcomeFact fact = view.GetFact(index);
            Append(builder, fact.FactId.Value);
            Append(builder, fact.Value);
        }
        Append(builder, view.InitialSubjectCount);
        for (int index = 0; index < view.InitialSubjectCount; index++)
        {
            GameplayOutcomeSubjectLink subject = view.GetInitialSubject(index);
            Append(builder, subject.SubjectId.Kind.Value);
            Append(builder, subject.SubjectId.Value);
            Append(builder, subject.Salience.ToString("R", CultureInfo.InvariantCulture));
            Append(builder, (int)subject.Tier);
            Append(builder, subject.IsPinned ? 1 : 0);
            Append(builder, subject.IsOptionalWitness ? 1 : 0);
            Append(builder, subject.AnchorRevision);
            Append(builder, subject.NextEvaluationDay);
        }
        Append(builder, view.InitialAnchorCount);
        for (int index = 0; index < view.InitialAnchorCount; index++)
        {
            Append(builder, view.GetInitialAnchorSubject(index).Kind.Value);
            Append(builder, view.GetInitialAnchorSubject(index).Value);
            Append(builder, view.GetInitialAnchor(index).AnchorTypeId);
            Append(builder, view.GetInitialAnchor(index).AnchorId);
        }
        return Sha256(builder.ToString());
    }

    /// <summary>
    /// Ledger-owned single-writer used only while the ledger lock is held.
    /// Its encoder, UTF-8/numeric scratch, and SHA state are reused so the
    /// warm preparation path stores a fixed digest without per-result GC.
    /// HashExact remains the intentionally allocating cold reference codec
    /// used by strict restore/parity verification.
    /// </summary>
    internal sealed class ExactDigestWriter
    {
        private readonly IncrementalHash algorithm =
            IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        private readonly Encoder encoder = Encoding.UTF8.GetEncoder();
        private readonly byte[] utf8Scratch = new byte[512];
        private readonly char[] numericScratch = new char[64];

        public void Write(in GameplayOutcomeReadView view, byte[] destination)
        {
            if (destination == null || destination.Length != Sha256ByteCount)
                throw new ArgumentException("A 32-byte SHA-256 destination is required.", nameof(destination));
            try
            {
                Feed(view.OutcomeId.RunId.Value);
                Feed(view.OutcomeId.Sequence);
                Feed(view.ResultKey.ProducerId);
                Feed(view.OperationId.Value);
                Feed(view.ResultKey.CommitRevision);
                Feed(view.ResultKey.LocalResultIndex);
                Feed(view.OwnerRevision);
                Feed(view.OutcomeTypeId.Value);
                Feed(view.AbsoluteDay);
                Feed((int)view.Status);
                GameplayLocationReference location = view.Location;
                Feed(location.LocationId); Feed(location.RoomId); Feed(location.X); Feed(location.Y);
                GameplayOutcomeCausation causation = view.Causation;
                Feed(causation.ParentOutcomeId.RunId.Value);
                Feed(causation.ParentOutcomeId.Sequence);
                Feed(causation.RootOperationId.Value);
                Feed(causation.RelationId);
                Feed(view.ParticipantCount);
                for (int index = 0; index < view.ParticipantCount; index++)
                {
                    GameplayOutcomeParticipant value = view.GetParticipant(index);
                    Feed(value.EntityId.Kind.Value); Feed(value.EntityId.Value); Feed(value.RoleId.Value);
                    Feed((int)value.ParticipationKind); Feed(value.HasPerceptionEvidence ? 1 : 0);
                    Feed(value.DisplayName.DisplayText); Feed(value.DisplayName.DisplaySnapshotRevision);
                    Feed(value.DisplayName.Locale); Feed((int)value.DisplayName.PronunciationHint.Mode);
                    Feed(value.DisplayName.PronunciationHint.Value);
                    Feed((int)value.DisplayName.PronunciationHint.ExplicitFinalConsonant);
                    Feed(value.DisplayName.PronunciationHint.Revision);
                }
                Feed(view.MetricCount);
                for (int index = 0; index < view.MetricCount; index++)
                {
                    GameplayOutcomeMetric value = view.GetMetric(index);
                    Feed(value.MetricId.Value); Feed(value.Value); Feed(value.UnitId.Value);
                    Feed(value.DefinitionOrInstanceId.Kind.Value);
                    Feed(value.DefinitionOrInstanceId.Value);
                }
                Feed(view.TagCount);
                for (int index = 0; index < view.TagCount; index++) Feed(view.GetTag(index).Value);
                Feed(view.ProvenanceCount);
                for (int index = 0; index < view.ProvenanceCount; index++)
                {
                    GameplayOutcomeProvenanceReference value = view.GetProvenance(index);
                    Feed(value.KindId); Feed(value.Value);
                }
                Feed(view.FactCount);
                for (int index = 0; index < view.FactCount; index++)
                {
                    GameplayOutcomeFact value = view.GetFact(index);
                    Feed(value.FactId.Value); Feed(value.Value);
                }
                Feed(view.InitialSubjectCount);
                for (int index = 0; index < view.InitialSubjectCount; index++)
                {
                    GameplayOutcomeSubjectLink value = view.GetInitialSubject(index);
                    Feed(value.SubjectId.Kind.Value); Feed(value.SubjectId.Value); Feed(value.Salience);
                    Feed((int)value.Tier); Feed(value.IsPinned ? 1 : 0);
                    Feed(value.IsOptionalWitness ? 1 : 0); Feed(value.AnchorRevision);
                    Feed(value.NextEvaluationDay);
                }
                Feed(view.InitialAnchorCount);
                for (int index = 0; index < view.InitialAnchorCount; index++)
                {
                    Feed(view.GetInitialAnchorSubject(index).Kind.Value);
                    Feed(view.GetInitialAnchorSubject(index).Value);
                    Feed(view.GetInitialAnchor(index).AnchorTypeId);
                    Feed(view.GetInitialAnchor(index).AnchorId);
                }
                if (!algorithm.TryGetHashAndReset(destination, out int written)
                    || written != Sha256ByteCount)
                    throw new InvalidOperationException("SHA-256 writer returned an invalid digest length.");
            }
            catch
            {
                algorithm.TryGetHashAndReset(destination, out _);
                Array.Clear(destination, 0, destination.Length);
                throw;
            }
        }

        private void Feed(string value)
        {
            int length = value?.Length ?? 0;
            if (!length.TryFormat(numericScratch, out int written, default, CultureInfo.InvariantCulture))
                throw new InvalidOperationException("String token length exceeded its bounded scratch.");
            FeedAscii(numericScratch, written);
            FeedAscii((byte)':');
            if (!string.IsNullOrEmpty(value)) FeedUtf8(value);
            FeedAscii((byte)'|');
        }

        private void Feed(int value)
        {
            if (!value.TryFormat(numericScratch, out int written, default, CultureInfo.InvariantCulture))
                throw new InvalidOperationException("Integer canonical formatting exceeded its bounded scratch.");
            FeedAscii(numericScratch, written); FeedAscii((byte)'|');
        }

        private void Feed(long value)
        {
            if (!value.TryFormat(numericScratch, out int written, default, CultureInfo.InvariantCulture))
                throw new InvalidOperationException("Long canonical formatting exceeded its bounded scratch.");
            FeedAscii(numericScratch, written); FeedAscii((byte)'|');
        }

        private void Feed(float value)
        {
            if (!value.TryFormat(numericScratch, out int written, "R", CultureInfo.InvariantCulture))
                throw new InvalidOperationException("Float canonical formatting exceeded its bounded scratch.");
            FeedFormattedString(written);
        }

        private void Feed(double value)
        {
            if (!value.TryFormat(numericScratch, out int written, "R", CultureInfo.InvariantCulture))
                throw new InvalidOperationException("Double canonical formatting exceeded its bounded scratch.");
            FeedFormattedString(written);
        }

        private void FeedFormattedString(int formattedLength)
        {
            // The cold reference codec feeds floating-point values through the
            // string overload, so their R-format text is length-prefixed.
            Span<char> lengthText = stackalloc char[16];
            if (!formattedLength.TryFormat(lengthText, out int lengthWritten, default, CultureInfo.InvariantCulture))
                throw new InvalidOperationException("Formatted numeric token length exceeded its bounded scratch.");
            FeedAscii(lengthText, lengthWritten);
            FeedAscii((byte)':');
            FeedAscii(numericScratch, formattedLength);
            FeedAscii((byte)'|');
        }

        private void FeedAscii(byte value)
        {
            utf8Scratch[0] = value;
            algorithm.AppendData(utf8Scratch, 0, 1);
        }

        private void FeedAscii(char[] source, int count)
        {
            for (int index = 0; index < count; index++)
                utf8Scratch[index] = checked((byte)source[index]);
            algorithm.AppendData(utf8Scratch, 0, count);
        }

        private void FeedAscii(ReadOnlySpan<char> source, int count)
        {
            for (int index = 0; index < count; index++)
                utf8Scratch[index] = checked((byte)source[index]);
            algorithm.AppendData(utf8Scratch, 0, count);
        }

        private void FeedUtf8(string value)
        {
            encoder.Reset();
            ReadOnlySpan<char> remaining = value.AsSpan();
            bool completed;
            do
            {
                encoder.Convert(
                    remaining,
                    utf8Scratch.AsSpan(),
                    flush: true,
                    out int charsUsed,
                    out int bytesUsed,
                    out completed);
                if (bytesUsed > 0)
                    algorithm.AppendData(utf8Scratch, 0, bytesUsed);
                remaining = remaining.Slice(charsUsed);
            }
            while (!completed);
        }
    }

    public static string RollSourceHash(string previousHash, string exactHash) =>
        Sha256((previousHash ?? string.Empty) + "|" + (exactHash ?? string.Empty));

    public static string HashCompacted(CompactedNarrativeMemorySnapshot memory)
    {
        if (memory == null)
            return string.Empty;
        using CompactedHashAccumulator accumulator = new CompactedHashAccumulator(memory);
        while (!accumulator.Advance()) { }
        return accumulator.Result;
    }

    internal sealed class CompactedHashAccumulator : IDisposable
    {
        private readonly CompactedNarrativeMemorySnapshot memory;
        private readonly SHA256 algorithm = SHA256.Create();
        private int stage;
        private int index;
        private bool finalized;

        public CompactedHashAccumulator(CompactedNarrativeMemorySnapshot memory) =>
            this.memory = memory ?? throw new ArgumentNullException(nameof(memory));

        public string Result { get; private set; } = string.Empty;

        // One call consumes at most one variable-length collection row. This is
        // the hash work unit used by consolidation's Stopwatch-tick slicing.
        public bool Advance()
        {
            if (finalized) return true;
            if (stage == 0)
            {
                Feed(memory.memoryId); Feed(memory.sharedAggregateId); Feed(memory.signature);
                Feed(memory.outcomeTypeId); Feed((int)memory.status); Feed(memory.subjectKindId);
                Feed(memory.subjectId); Feed(memory.locationId); Feed(memory.roomId);
                Feed(memory.locationX); Feed(memory.locationY); Feed(memory.firstSequence);
                Feed(memory.lastSequence); Feed(memory.firstDay); Feed(memory.lastDay);
                Feed(memory.occurrenceCount); Feed(memory.salience.ToString("R", CultureInfo.InvariantCulture));
                Feed(memory.influenceUseCount); Feed(memory.influenceRevision);
                Feed(memory.sourceSegmentHash); Feed(memory.tags?.Count ?? 0);
                stage = 1; index = 0; return false;
            }
            if (stage == 1 && index < (memory.tags?.Count ?? 0))
            { Feed(memory.tags[index++]); return false; }
            if (stage == 1) { Feed(memory.metrics?.Count ?? 0); stage = 2; index = 0; return false; }
            if (stage == 2 && index < (memory.metrics?.Count ?? 0))
            {
                GameplayOutcomeMetricAggregateSnapshot metric = memory.metrics[index++];
                Feed(metric.metricId); Feed(metric.unitId); Feed(metric.referenceKindId); Feed(metric.referenceId);
                Feed(metric.sum.ToString("R", CultureInfo.InvariantCulture));
                Feed(metric.minimum.ToString("R", CultureInfo.InvariantCulture));
                Feed(metric.maximum.ToString("R", CultureInfo.InvariantCulture)); Feed(metric.sampleCount);
                return false;
            }
            if (stage == 2) { Feed(memory.participants?.Count ?? 0); stage = 3; index = 0; return false; }
            if (stage == 3 && index < (memory.participants?.Count ?? 0))
            {
                GameplayOutcomeParticipantSnapshot participant = memory.participants[index++];
                Feed(participant.entityKindId); Feed(participant.entityId); Feed(participant.roleId);
                Feed((int)participant.participationKind); Feed(participant.hasPerceptionEvidence ? 1 : 0);
                Feed(participant.displayText); Feed(participant.displayRevision); Feed(participant.locale);
                Feed((int)participant.pronunciationMode); Feed(participant.pronunciationValue);
                Feed((int)participant.finalConsonant); Feed(participant.pronunciationRevision);
                return false;
            }
            if (stage == 3) { Feed(memory.provenance?.Count ?? 0); stage = 4; index = 0; return false; }
            if (stage == 4 && index < (memory.provenance?.Count ?? 0))
            {
                GameplayOutcomeProvenanceReferenceSnapshot provenance = memory.provenance[index++];
                Feed(provenance.kindId); Feed(provenance.value); return false;
            }
            if (stage == 4) { Feed(memory.facts?.Count ?? 0); stage = 5; index = 0; return false; }
            if (stage == 5 && index < (memory.facts?.Count ?? 0))
            {
                GameplayOutcomeFactSnapshot fact = memory.facts[index++];
                Feed(fact.factId); Feed(fact.value); return false;
            }
            algorithm.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
            Result = ToHex(algorithm.Hash);
            finalized = true;
            return true;
        }

        public void Dispose() => algorithm.Dispose();

        private void Feed(string value)
        {
            string token = (value?.Length ?? 0).ToString(CultureInfo.InvariantCulture)
                + ":" + value + "|";
            FeedBytes(token);
        }
        private void Feed(long value) => FeedBytes(value.ToString(CultureInfo.InvariantCulture) + "|");
        private void Feed(int value) => FeedBytes(value.ToString(CultureInfo.InvariantCulture) + "|");
        private void FeedBytes(string token)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(token);
            algorithm.TransformBlock(bytes, 0, bytes.Length, bytes, 0);
        }
    }

    public static string CreateMemoryId(
        GameplayOutcomeRunId runId,
        GameplayEntityId subjectId,
        GameplayMemorySignature signature) =>
        "memory:" + Sha256(runId.Value + "|" + subjectId + "|" + signature.Value).Substring(0, 32);

    public static string CreateSharedAggregateId(
        GameplayOutcomeRunId runId,
        GameplayOutcomeTypeId outcomeTypeId,
        GameplayOutcomeStatus status,
        long firstSequence,
        long lastSequence,
        int occurrenceCount,
        string sourceSegmentHash) =>
        "aggregate:" + Sha256(
            runId.Value + "|" + outcomeTypeId.Value + "|"
            + ((int)status).ToString(CultureInfo.InvariantCulture) + "|"
            + firstSequence.ToString(CultureInfo.InvariantCulture) + "|"
            + lastSequence.ToString(CultureInfo.InvariantCulture) + "|"
            + occurrenceCount.ToString(CultureInfo.InvariantCulture) + "|" + sourceSegmentHash)
            .Substring(0, 32);

    private static string Sha256(string value)
    {
        using SHA256 algorithm = SHA256.Create();
        byte[] bytes = Encoding.UTF8.GetBytes(value ?? string.Empty);
        byte[] hash = algorithm.ComputeHash(bytes);
        return ToHex(hash);
    }

    internal static string ToHex(ReadOnlySpan<byte> hash)
    {
        StringBuilder result = new StringBuilder(hash.Length * 2);
        for (int index = 0; index < hash.Length; index++)
            result.Append(hash[index].ToString("x2", CultureInfo.InvariantCulture));
        return result.ToString();
    }

    internal static bool TryDecodeHex(string value, byte[] destination)
    {
        if (value == null || value.Length != Sha256ByteCount * 2
            || destination == null || destination.Length != Sha256ByteCount)
            return false;
        for (int index = 0; index < destination.Length; index++)
        {
            int high = HexNibble(value[index * 2]);
            int low = HexNibble(value[index * 2 + 1]);
            if (high < 0 || low < 0) return false;
            destination[index] = (byte)((high << 4) | low);
        }
        return true;
    }

    private static int HexNibble(char value)
    {
        if (value >= '0' && value <= '9') return value - '0';
        if (value >= 'a' && value <= 'f') return value - 'a' + 10;
        return -1;
    }

    private static void Append(StringBuilder builder, string value) =>
        builder.Append(value?.Length ?? 0).Append(':').Append(value).Append('|');
    private static void Append(StringBuilder builder, long value) =>
        builder.Append(value.ToString(CultureInfo.InvariantCulture)).Append('|');
    private static void Append(StringBuilder builder, int value) =>
        builder.Append(value.ToString(CultureInfo.InvariantCulture)).Append('|');
}
