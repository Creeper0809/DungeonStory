using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using DungeonStory.Narrative.Korean;
using VContainer;

// The observer owns native ledger identity; gameplay keeps only the opaque proof.
public static class CharacterSkillActivationPersistenceContract
{
    public const string ProducerId = "character.skill-activation";
    public const string OperationPrefix = CharacterSkillActivationIdentity.OperationPrefix;
    public static string OperationId(string sourceCharacterId, long revision) =>
        CharacterSkillActivationIdentity.OperationId(sourceCharacterId, revision);
    public static string ResultKey(string sourceCharacterId, long revision) =>
        ProducerId + "|" + OperationId(sourceCharacterId, revision) + "|" + revision + "|0";
}

public static class CharacterSkillActivationOutcomeIds
{
    public const string ProducerId =
        CharacterSkillActivationPersistenceContract.ProducerId;

    public static readonly GameplayOutcomeTypeId Activated =
        new("character.skill-activated");
    public static readonly GameplayEntityKindId CharacterKind =
        new("character");
    public static readonly GameplayRoleId SourceRole =
        new("skill-source");
    public static readonly GameplayRoleId RecipientRole =
        new("skill-recipient");
    public static readonly GameplayRoleId SourceRecipientRole =
        new("skill-source-recipient");

    public static readonly GameplayMetricId OwnerRevisionBefore =
        new("skill-activation.owner-revision-before");
    public static readonly GameplayMetricId OwnerRevisionAfter =
        new("skill-activation.owner-revision-after");
    public static readonly GameplayMetricId UseCountBefore =
        new("skill-activation.use-count-before");
    public static readonly GameplayMetricId UseCountAfter =
        new("skill-activation.use-count-after");
    public static readonly GameplayMetricId ReadyHourBefore =
        new("skill-activation.ready-hour-before");
    public static readonly GameplayMetricId ReadyHourAfter =
        new("skill-activation.ready-hour-after");
    public static readonly GameplayMetricUnitId RevisionUnit =
        new("revision");
    public static readonly GameplayMetricUnitId CountUnit = new("count");
    public static readonly GameplayMetricUnitId HourUnit = new("hour");

    public static readonly GameplayOutcomeTagId CharacterTag =
        new("domain:character");
    public static readonly GameplayOutcomeTagId ActivationTag =
        new("skill-activation");

    public static readonly GameplayOutcomeFactId ActivationKind =
        new("skill.activation-kind");
    public static readonly GameplayOutcomeFactId SkillId =
        new("skill.id");
    public static readonly GameplayOutcomeFactId SkillDisplayName =
        new("skill.display-name");
    public static readonly GameplayOutcomeFactId Trigger =
        new("skill.trigger");
    public static readonly GameplayOutcomeFactId Target =
        new("skill.target");
    public static readonly GameplayOutcomeFactId TargetingMode =
        new("skill.targeting-mode");
    public static readonly GameplayOutcomeFactId EffectArea =
        new("skill.effect-area");
    public static readonly GameplayOutcomeFactId AreaSize =
        new("skill.area-size");
    public static readonly GameplayOutcomeFactId DurationHours =
        new("skill.duration-hours");
    public static readonly GameplayOutcomeFactId CooldownDays =
        new("skill.cooldown-days");
    public static readonly GameplayOutcomeFactId AbsoluteHour =
        new("skill.absolute-hour");
    public static readonly GameplayOutcomeFactId ExpiresAbsoluteHour =
        new("skill.expires-absolute-hour");
    public static readonly GameplayOutcomeFactId MechanicsSha256 =
        new("skill.mechanics-sha256");
    public static readonly GameplayOutcomeFactId RecipientCount =
        new("skill.recipient-count");
    public static readonly GameplayOutcomeFactId RecipientStateSha256 =
        new("skill.recipient-state-sha256");
    public static readonly GameplayOutcomeFactId ChildResultKeys =
        new("skill.child-result-keys");
    public static readonly GameplayOutcomeFactId RecipientStateChunkCount =
        new("skill.recipient-state-chunk-count");
    public static readonly GameplayOutcomeFactId TriggerEventId =
        new("skill.trigger-event-id");
    public static readonly GameplayOutcomeFactId TriggerEventKeySha256 =
        new("skill.trigger-event-key-sha256");
    public static readonly GameplayOutcomeFactId UltimateDomain =
        new("skill.ultimate-domain");
    public static readonly GameplayOutcomeFactId TriggerSerial =
        new("skill.trigger-serial");

    public static readonly GameplayOutcomeFactId[] OrderedBaseFacts =
    {
        ActivationKind,
        SkillId,
        SkillDisplayName,
        Trigger,
        Target,
        TargetingMode,
        EffectArea,
        AreaSize,
        DurationHours,
        CooldownDays,
        AbsoluteHour,
        ExpiresAbsoluteHour,
        MechanicsSha256,
        RecipientCount,
        RecipientStateSha256,
        ChildResultKeys,
        RecipientStateChunkCount,
        TriggerEventId,
        TriggerEventKeySha256,
        UltimateDomain,
        TriggerSerial
    };

    public const string MechanicsAnchor = "skill-mechanics-sha256";
    public const string RecipientStateAnchor =
        "skill-recipient-state-sha256";

    public static GameplayOutcomeFactId RecipientStateChunk(int index) =>
        new("skill.recipient-state-chunk-"
            + index.ToString("D2", CultureInfo.InvariantCulture));
}

public readonly struct CharacterSkillActivationRecipientReceipt
{
    public CharacterSkillActivationRecipientReceipt(
        string characterId,
        string displayName,
        string beforeState,
        string afterState)
    {
        CharacterId = GameplayOutcomeStableIdSyntax.Require(
            characterId,
            nameof(characterId));
        DisplayName = EvolutionOutcomeNames.Snapshot(
            CharacterId,
            displayName);
        BeforeState = RequireFact(beforeState, nameof(beforeState));
        AfterState = RequireFact(afterState, nameof(afterState));
    }

    public string CharacterId { get; }
    public KoreanNameSnapshot DisplayName { get; }
    public string BeforeState { get; }
    public string AfterState { get; }

    private static string RequireFact(string value, string parameterName)
    {
        if (!GameplayOutcomeLedger.IsValidBoundedUtf16(
                value,
                GameplayOutcomeBufferLimits.MaximumFactValueUtf16Length,
                allowEmpty: false))
        {
            throw new ArgumentException(
                "A bounded recipient state is required.",
                parameterName);
        }
        return value;
    }
}

public readonly struct CharacterSkillActivationOutcomeReceipt
{
    private const int ChunkLength = 3000;

    public CharacterSkillActivationOutcomeReceipt(
        string sourceCharacterId,
        string sourceDisplayName,
        long ownerRevision,
        CharacterSkillActivationKind activationKind,
        CharacterSkillInstance skill,
        int absoluteDay,
        long absoluteHour,
        long expiresAbsoluteHour,
        int useCountBefore,
        int useCountAfter,
        long readyAbsoluteHourBefore,
        long readyAbsoluteHourAfter,
        string mechanicsSha256,
        IReadOnlyList<CharacterSkillActivationRecipientReceipt> recipients,
        IReadOnlyList<GameplayResultKey> childResultKeys,
        string triggerEventId = "",
        string triggerEventKeySha256 = "",
        CharacterUltimateDomain ultimateDomain = CharacterUltimateDomain.None,
        int triggerSerial = -1)
        : this(
            sourceCharacterId,
            sourceDisplayName,
            ownerRevision,
            activationKind,
            skill != null,
            skill?.id,
            skill?.displayName,
            skill != null ? skill.trigger : default,
            skill != null ? skill.target : default,
            skill != null ? skill.targetingMode : default,
            skill != null ? skill.effectArea : default,
            skill?.areaSize ?? default,
            skill?.manualDurationHours ?? default,
            skill?.manualCooldownDays ?? default,
            absoluteDay,
            absoluteHour,
            expiresAbsoluteHour,
            useCountBefore,
            useCountAfter,
            readyAbsoluteHourBefore,
            readyAbsoluteHourAfter,
            mechanicsSha256,
            recipients,
            childResultKeys,
            triggerEventId,
            triggerEventKeySha256,
            ultimateDomain,
            triggerSerial)
    {
    }

    public CharacterSkillActivationOutcomeReceipt(
        CharacterSkillActivationFact fact)
        : this(
            RequireActivationFact(fact).SourceCharacterId,
            fact.SourceDisplayName,
            fact.OwnerRevision,
            fact.ActivationKind,
            true,
            fact.SkillId,
            fact.SkillDisplayName,
            fact.Trigger,
            fact.Target,
            fact.TargetingMode,
            fact.EffectArea,
            fact.AreaSize,
            fact.DurationHours,
            fact.CooldownDays,
            fact.AbsoluteDay,
            fact.AbsoluteHour,
            fact.ExpiresAbsoluteHour,
            fact.UseCountBefore,
            fact.UseCountAfter,
            fact.ReadyAbsoluteHourBefore,
            fact.ReadyAbsoluteHourAfter,
            fact.MechanicsSha256,
            CaptureRecipients(fact.Recipients),
            ParseChildResultKeys(fact.ChildPublicationKeys),
            fact.TriggerEventId,
            fact.TriggerEventKeySha256,
            fact.UltimateDomain,
            fact.TriggerSerial)
    {
    }

    private CharacterSkillActivationOutcomeReceipt(
        string sourceCharacterId,
        string sourceDisplayName,
        long ownerRevision,
        CharacterSkillActivationKind activationKind,
        bool skillPresent,
        string skillId,
        string skillDisplayName,
        CharacterSkillTrigger trigger,
        CharacterSkillTarget target,
        CharacterSkillTargetingMode targetingMode,
        CharacterSkillEffectArea effectArea,
        int areaSize,
        int durationHours,
        int cooldownDays,
        int absoluteDay,
        long absoluteHour,
        long expiresAbsoluteHour,
        int useCountBefore,
        int useCountAfter,
        long readyAbsoluteHourBefore,
        long readyAbsoluteHourAfter,
        string mechanicsSha256,
        IReadOnlyList<CharacterSkillActivationRecipientReceipt> recipients,
        IReadOnlyList<GameplayResultKey> childResultKeys,
        string triggerEventId,
        string triggerEventKeySha256,
        CharacterUltimateDomain ultimateDomain,
        int triggerSerial)
    {
        SourceCharacterId = GameplayOutcomeStableIdSyntax.Require(
            sourceCharacterId,
            nameof(sourceCharacterId));
        SourceDisplayName = EvolutionOutcomeNames.Snapshot(
            SourceCharacterId,
            sourceDisplayName);
        if (ownerRevision <= 0L)
            throw new ArgumentOutOfRangeException(nameof(ownerRevision));
        if (!Enum.IsDefined(typeof(CharacterSkillActivationKind), activationKind)
            || activationKind == CharacterSkillActivationKind.None)
        {
            throw new ArgumentOutOfRangeException(nameof(activationKind));
        }
        if (!skillPresent)
            throw new ArgumentNullException("skill");
        if (absoluteDay < 0 || absoluteHour < 0L)
            throw new ArgumentOutOfRangeException(nameof(absoluteDay));
        if (expiresAbsoluteHour < absoluteHour)
            throw new ArgumentOutOfRangeException(nameof(expiresAbsoluteHour));
        if (useCountBefore < 0 || useCountAfter != useCountBefore + 1)
            throw new ArgumentOutOfRangeException(nameof(useCountAfter));
        bool eventBound = activationKind is
            CharacterSkillActivationKind.AutomaticPassive
            or CharacterSkillActivationKind.AutomaticUltimate
            or CharacterSkillActivationKind.ManualCombatUltimate;
        if (!eventBound && (readyAbsoluteHourBefore < 0L
                || readyAbsoluteHourAfter < absoluteHour)
            || eventBound && (readyAbsoluteHourBefore != 0L
                || readyAbsoluteHourAfter != 0L))
        {
            throw new ArgumentOutOfRangeException(nameof(readyAbsoluteHourAfter));
        }
        if (!IsSha256(mechanicsSha256))
            throw new ArgumentException(
                "A canonical mechanics SHA-256 is required.",
                nameof(mechanicsSha256));

        if (!eventBound)
        {
            if (!string.IsNullOrEmpty(triggerEventId)
                || !string.IsNullOrEmpty(triggerEventKeySha256)
                || ultimateDomain != CharacterUltimateDomain.None
                || triggerSerial != -1)
            {
                throw new ArgumentException(
                    "Non-event activation cannot carry a trigger identity.",
                    nameof(triggerEventId));
            }
        }
        else
        {
            triggerEventId = RequireFact(
                triggerEventId,
                nameof(triggerEventId));
            if (!IsSha256(triggerEventKeySha256))
                throw new ArgumentException(
                    "A canonical trigger-event SHA-256 is required.",
                    nameof(triggerEventKeySha256));
            if (activationKind
                    == CharacterSkillActivationKind.AutomaticPassive)
            {
                if (ultimateDomain != CharacterUltimateDomain.None
                    || triggerSerial != -1)
                {
                    throw new ArgumentException(
                        "Passive activation cannot carry an ultimate limit.",
                        nameof(ultimateDomain));
                }
            }
            else if (ultimateDomain == CharacterUltimateDomain.None
                || !Enum.IsDefined(
                    typeof(CharacterUltimateDomain),
                    ultimateDomain)
                || triggerSerial < 0)
            {
                throw new ArgumentException(
                    "Event-bound ultimate requires a valid domain and serial.",
                    nameof(ultimateDomain));
            }
        }

        CharacterSkillActivationRecipientReceipt[] ordered =
            (recipients ?? Array.Empty<CharacterSkillActivationRecipientReceipt>())
            .OrderBy(value => value.CharacterId, StringComparer.Ordinal)
            .ToArray();
        if (ordered.Length == 0
            || ordered.Select(value => value.CharacterId)
                .Distinct(StringComparer.Ordinal).Count() != ordered.Length)
        {
            throw new ArgumentException(
                "At least one unique recipient is required.",
                nameof(recipients));
        }

        OwnerRevision = ownerRevision;
        OperationId = new GameplayOperationId(
            CharacterSkillActivationPersistenceContract.OperationId(
                SourceCharacterId,
                ownerRevision));
        ActivationKind = activationKind;
        SkillId = GameplayOutcomeStableIdSyntax.Require(
            skillId,
            "id");
        SkillDisplayName = RequireFact(
            skillDisplayName,
            "displayName");
        Trigger = trigger.ToString();
        Target = target.ToString();
        TargetingMode = targetingMode.ToString();
        EffectArea = effectArea.ToString();
        AreaSize = areaSize;
        DurationHours = durationHours;
        CooldownDays = cooldownDays;
        AbsoluteDay = absoluteDay;
        AbsoluteHourValue = absoluteHour;
        ExpiresAbsoluteHourValue = expiresAbsoluteHour;
        UseCountBeforeValue = useCountBefore;
        UseCountAfterValue = useCountAfter;
        ReadyAbsoluteHourBeforeValue = readyAbsoluteHourBefore;
        ReadyAbsoluteHourAfterValue = readyAbsoluteHourAfter;
        MechanicsSha256Value = mechanicsSha256.ToLowerInvariant();
        Recipients = ordered;
        RecipientStateCanonical = BuildRecipientStateCanonical(ordered);
        RecipientStateSha256Value = NarrativeInferenceHash.ComputeSha256Utf8(
                RecipientStateCanonical)
            .Substring("sha256:".Length);
        RecipientStateChunks = Split(RecipientStateCanonical);
        ChildResultKeysValue = CanonicalChildren(childResultKeys);
        TriggerEventIdValue = eventBound ? triggerEventId : "none";
        TriggerEventKeySha256Value = eventBound
            ? triggerEventKeySha256.ToLowerInvariant()
            : "none";
        UltimateDomainValue = eventBound
            ? ultimateDomain.ToString()
            : CharacterUltimateDomain.None.ToString();
        TriggerSerialValue = triggerSerial;
    }

    private static CharacterSkillActivationFact RequireActivationFact(
        CharacterSkillActivationFact fact) =>
        fact ?? throw new ArgumentNullException(nameof(fact));

    private static CharacterSkillActivationRecipientReceipt[] CaptureRecipients(
        IReadOnlyList<CharacterSkillActivationRecipientFact> recipients)
    {
        if (recipients == null)
            throw new ArgumentNullException(nameof(recipients));
        var captured = new CharacterSkillActivationRecipientReceipt[recipients.Count];
        for (int index = 0; index < recipients.Count; index++)
        {
            CharacterSkillActivationRecipientFact recipient = recipients[index];
            if (recipient == null)
                throw new ArgumentException(
                    "Skill activation recipients must be present.",
                    nameof(recipients));
            captured[index] = new CharacterSkillActivationRecipientReceipt(
                recipient.CharacterId,
                recipient.DisplayName,
                recipient.BeforeState,
                recipient.AfterState);
        }
        return captured;
    }

    private static GameplayResultKey[] ParseChildResultKeys(
        IReadOnlyList<string> publicationKeys)
    {
        if (publicationKeys == null)
            throw new ArgumentNullException(nameof(publicationKeys));
        var keys = new GameplayResultKey[publicationKeys.Count];
        for (int index = 0; index < publicationKeys.Count; index++)
        {
            if (!GameplayDomainFactDeliveryQuery.TryReadKey(
                    publicationKeys[index],
                    out keys[index]))
            {
                throw new ArgumentException(
                    "Child publication keys must be canonical gameplay result keys.",
                    nameof(publicationKeys));
            }
        }
        return keys;
    }

    public string SourceCharacterId { get; }
    public KoreanNameSnapshot SourceDisplayName { get; }
    public long OwnerRevision { get; }
    public GameplayOperationId OperationId { get; }
    public CharacterSkillActivationKind ActivationKind { get; }
    public string SkillId { get; }
    public string SkillDisplayName { get; }
    public string Trigger { get; }
    public string Target { get; }
    public string TargetingMode { get; }
    public string EffectArea { get; }
    public int AreaSize { get; }
    public int DurationHours { get; }
    public int CooldownDays { get; }
    public int AbsoluteDay { get; }
    public long AbsoluteHourValue { get; }
    public long ExpiresAbsoluteHourValue { get; }
    public int UseCountBeforeValue { get; }
    public int UseCountAfterValue { get; }
    public long ReadyAbsoluteHourBeforeValue { get; }
    public long ReadyAbsoluteHourAfterValue { get; }
    public string MechanicsSha256Value { get; }
    public IReadOnlyList<CharacterSkillActivationRecipientReceipt> Recipients { get; }
    public string RecipientStateCanonical { get; }
    public string RecipientStateSha256Value { get; }
    public IReadOnlyList<string> RecipientStateChunks { get; }
    public string ChildResultKeysValue { get; }
    public string TriggerEventIdValue { get; }
    public string TriggerEventKeySha256Value { get; }
    public string UltimateDomainValue { get; }
    public int TriggerSerialValue { get; }
    public GameplayResultKey ResultKey => new(
        CharacterSkillActivationOutcomeIds.ProducerId,
        OperationId,
        OwnerRevision,
        0);

    private static string BuildRecipientStateCanonical(
        IReadOnlyList<CharacterSkillActivationRecipientReceipt> recipients)
    {
        StringBuilder value = new("skill-recipient-states-v1;");
        value.Append(recipients.Count.ToString(CultureInfo.InvariantCulture))
            .Append(';');
        foreach (CharacterSkillActivationRecipientReceipt recipient in recipients)
        {
            AppendToken(value, recipient.CharacterId);
            AppendToken(value, recipient.BeforeState);
            AppendToken(value, recipient.AfterState);
        }
        return value.ToString();
    }

    private static string[] Split(string value)
    {
        int count = Math.Max(1, (value.Length + ChunkLength - 1) / ChunkLength);
        if (CharacterSkillActivationOutcomeIds.OrderedBaseFacts.Length + count
            > GameplayOutcomeBufferLimits.AbsoluteMaximumFactsPerOutcome)
        {
            throw new ArgumentException(
                "Recipient state exceeds the outcome fact capacity.",
                nameof(value));
        }
        var chunks = new string[count];
        for (int index = 0; index < count; index++)
        {
            int start = index * ChunkLength;
            chunks[index] = value.Substring(
                start,
                Math.Min(ChunkLength, value.Length - start));
        }
        return chunks;
    }

    private static string CanonicalChildren(
        IReadOnlyList<GameplayResultKey> childResultKeys)
    {
        string[] values = (childResultKeys ?? Array.Empty<GameplayResultKey>())
            .Select(value => value.IsValid
                ? value.ToString()
                : throw new ArgumentException(
                    "Child result keys must be valid.",
                    nameof(childResultKeys)))
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        if (values.Distinct(StringComparer.Ordinal).Count() != values.Length)
        {
            throw new ArgumentException(
                "Child result keys must be unique.",
                nameof(childResultKeys));
        }
        return values.Length == 0 ? "none" : string.Join(";", values);
    }

    private static string RequireFact(string value, string parameterName)
    {
        string normalized = value?.Trim() ?? string.Empty;
        if (!GameplayOutcomeLedger.IsValidBoundedUtf16(
                normalized,
                GameplayOutcomeBufferLimits.MaximumFactValueUtf16Length,
                allowEmpty: false))
        {
            throw new ArgumentException(
                "A bounded immutable fact is required.",
                parameterName);
        }
        return normalized;
    }

    private static bool IsSha256(string value) =>
        value != null
        && value.Length == 64
        && value.All(character =>
            character is >= '0' and <= '9'
                or >= 'a' and <= 'f'
                or >= 'A' and <= 'F');

    private static void AppendToken(StringBuilder target, string value)
    {
        target.Append(value.Length.ToString(CultureInfo.InvariantCulture))
            .Append(':').Append(value).Append(';');
    }
}

public sealed class CharacterSkillActivationOutcomeAdapter :
    GameplayOutcomeAdapter<CharacterSkillActivationOutcomeReceipt>
{
    public override GameplayOutcomeTypeId OutcomeTypeId =>
        CharacterSkillActivationOutcomeIds.Activated;

    public override OutcomePrepareResult TryGetRequirements(
        in CharacterSkillActivationOutcomeReceipt receipt,
        long currentWorldEpoch,
        out OutcomeWriteRequirements requirements)
    {
        string sourceCharacterId = receipt.SourceCharacterId;
        int participantCount = receipt.Recipients.Count
            + (receipt.Recipients.Any(value => string.Equals(
                    value.CharacterId,
                    sourceCharacterId,
                    StringComparison.Ordinal))
                ? 0
                : 1);
        requirements = new OutcomeWriteRequirements(
            receipt.ResultKey,
            OutcomeTypeId,
            receipt.AbsoluteDay,
            GameplayOutcomeStatus.Succeeded,
            currentWorldEpoch,
            receipt.OwnerRevision,
            participantCount,
            metricCount: 6,
            subjectCount: participantCount,
            tagCount: 2,
            anchorCount: 2,
            provenanceCount: 2,
            factCount: CharacterSkillActivationOutcomeIds.OrderedBaseFacts.Length
                + receipt.RecipientStateChunks.Count);
        return receipt.ResultKey.IsValid
            && receipt.OwnerRevision > 0L
            && receipt.Recipients.Count > 0
            && receipt.RecipientStateChunks.Count > 0
                ? OutcomePrepareResult.Prepared()
                : new OutcomePrepareResult(
                    OutcomePrepareCode.InvalidReceipt,
                    "character-skill-activation-receipt-invalid");
    }

    public override OutcomePrepareResult TryWrite(
        in CharacterSkillActivationOutcomeReceipt receipt,
        ref OutcomeWriteBuilder builder)
    {
        string sourceCharacterId = receipt.SourceCharacterId;
        GameplayEntityId source = new(
            CharacterSkillActivationOutcomeIds.CharacterKind,
            sourceCharacterId);
        CharacterSkillActivationRecipientReceipt? sourceRecipient =
            receipt.Recipients.Cast<CharacterSkillActivationRecipientReceipt?>()
                .FirstOrDefault(value => value.HasValue
                    && string.Equals(
                        value.Value.CharacterId,
                        sourceCharacterId,
                        StringComparison.Ordinal));
        bool written = builder.AddParticipant(new GameplayOutcomeParticipant(
                source,
                sourceRecipient.HasValue
                    ? CharacterSkillActivationOutcomeIds.SourceRecipientRole
                    : CharacterSkillActivationOutcomeIds.SourceRole,
                GameplayParticipationKind.Direct,
                true,
                receipt.SourceDisplayName))
            && builder.AddSubject(new GameplayOutcomeSubjectLink(
                source,
                0.64f,
                NarrativeMemoryTier.Recent,
                false,
                false,
                0));

        foreach (CharacterSkillActivationRecipientReceipt recipient in
                 receipt.Recipients.Where(value => !string.Equals(
                     value.CharacterId,
                     sourceCharacterId,
                     StringComparison.Ordinal)))
        {
            GameplayEntityId entity = new(
                CharacterSkillActivationOutcomeIds.CharacterKind,
                recipient.CharacterId);
            written = written
                && builder.AddParticipant(new GameplayOutcomeParticipant(
                    entity,
                    CharacterSkillActivationOutcomeIds.RecipientRole,
                    GameplayParticipationKind.Direct,
                    false,
                    recipient.DisplayName))
                && builder.AddSubject(new GameplayOutcomeSubjectLink(
                    entity,
                    0.56f,
                    NarrativeMemoryTier.Recent,
                    false,
                    false,
                    0));
        }

        GameplayOutcomeMetric[] metrics =
        {
            new(CharacterSkillActivationOutcomeIds.OwnerRevisionBefore,
                receipt.OwnerRevision - 1L,
                CharacterSkillActivationOutcomeIds.RevisionUnit,
                source),
            new(CharacterSkillActivationOutcomeIds.OwnerRevisionAfter,
                receipt.OwnerRevision,
                CharacterSkillActivationOutcomeIds.RevisionUnit,
                source),
            new(CharacterSkillActivationOutcomeIds.UseCountBefore,
                receipt.UseCountBeforeValue,
                CharacterSkillActivationOutcomeIds.CountUnit,
                source),
            new(CharacterSkillActivationOutcomeIds.UseCountAfter,
                receipt.UseCountAfterValue,
                CharacterSkillActivationOutcomeIds.CountUnit,
                source),
            new(CharacterSkillActivationOutcomeIds.ReadyHourBefore,
                receipt.ReadyAbsoluteHourBeforeValue,
                CharacterSkillActivationOutcomeIds.HourUnit,
                source),
            new(CharacterSkillActivationOutcomeIds.ReadyHourAfter,
                receipt.ReadyAbsoluteHourAfterValue,
                CharacterSkillActivationOutcomeIds.HourUnit,
                source)
        };
        foreach (GameplayOutcomeMetric metric in metrics)
            written = written && builder.AddMetric(metric);

        string[] baseValues =
        {
            receipt.ActivationKind.ToString(),
            receipt.SkillId,
            receipt.SkillDisplayName,
            receipt.Trigger,
            receipt.Target,
            receipt.TargetingMode,
            receipt.EffectArea,
            receipt.AreaSize.ToString(CultureInfo.InvariantCulture),
            receipt.DurationHours.ToString(CultureInfo.InvariantCulture),
            receipt.CooldownDays.ToString(CultureInfo.InvariantCulture),
            receipt.AbsoluteHourValue.ToString(CultureInfo.InvariantCulture),
            receipt.ExpiresAbsoluteHourValue.ToString(CultureInfo.InvariantCulture),
            receipt.MechanicsSha256Value,
            receipt.Recipients.Count.ToString(CultureInfo.InvariantCulture),
            receipt.RecipientStateSha256Value,
            receipt.ChildResultKeysValue,
            receipt.RecipientStateChunks.Count.ToString(CultureInfo.InvariantCulture),
            receipt.TriggerEventIdValue,
            receipt.TriggerEventKeySha256Value,
            receipt.UltimateDomainValue,
            receipt.TriggerSerialValue.ToString(CultureInfo.InvariantCulture)
        };
        written = written
            && builder.AddTag(CharacterSkillActivationOutcomeIds.CharacterTag)
            && builder.AddTag(CharacterSkillActivationOutcomeIds.ActivationTag)
            && builder.AddAnchor(source, new NarrativeEvidenceReference(
                CharacterSkillActivationOutcomeIds.MechanicsAnchor,
                receipt.MechanicsSha256Value))
            && builder.AddAnchor(source, new NarrativeEvidenceReference(
                CharacterSkillActivationOutcomeIds.RecipientStateAnchor,
                receipt.RecipientStateSha256Value))
            && builder.AddProvenance(new GameplayOutcomeProvenanceReference(
                "runtime-receipt",
                "character-skill-activation-v1"))
            && builder.AddProvenance(new GameplayOutcomeProvenanceReference(
                "domain-commit",
                receipt.OperationId.Value));
        for (int index = 0; written && index < baseValues.Length; index++)
        {
            written = builder.AddFact(new GameplayOutcomeFact(
                CharacterSkillActivationOutcomeIds.OrderedBaseFacts[index],
                baseValues[index]));
        }
        for (int index = 0;
             written && index < receipt.RecipientStateChunks.Count;
             index++)
        {
            written = builder.AddFact(new GameplayOutcomeFact(
                CharacterSkillActivationOutcomeIds.RecipientStateChunk(index),
                receipt.RecipientStateChunks[index]));
        }
        return written
            ? OutcomePrepareResult.Prepared()
            : new OutcomePrepareResult(
                OutcomePrepareCode.AdapterWriteFailed,
                "character-skill-activation-write-failed");
    }
}

public readonly struct PreparedCharacterSkillActivationOutcome
{
    internal PreparedCharacterSkillActivationOutcome(
        PreparedOwnerOutcome prepared,
        GameplayResultKey resultKey,
        long ownerRevision,
        bool isReplay)
    {
        Prepared = prepared;
        ResultKey = resultKey;
        OwnerRevision = ownerRevision;
        IsReplay = isReplay;
    }

    internal PreparedOwnerOutcome Prepared { get; }
    public GameplayResultKey ResultKey { get; }
    public long OwnerRevision { get; }
    public bool IsReplay { get; }
}

public interface ICharacterSkillActivationOutcomeCommitter
{
    bool TryPrepare(
        in CharacterSkillActivationOutcomeReceipt receipt,
        out PreparedCharacterSkillActivationOutcome prepared,
        out string failureReason);
    OwnerOutcomeCommitResult Commit(
        in PreparedCharacterSkillActivationOutcome prepared);
    OwnerOutcomeCommitResult Reconcile(GameplayResultKey resultKey);
    void Cancel(in PreparedCharacterSkillActivationOutcome prepared);
}

public sealed class CharacterSkillActivationGameplayOutcomeBridge :
    ICharacterSkillActivationOutcomeCommitter
{
    private readonly PreparedOutcomeOwnerTransaction transactions;

    public CharacterSkillActivationGameplayOutcomeBridge(
        IGameplayOutcomeRecorder recorder,
        IGameplayOutcomeDiagnosticsQuery diagnostics)
    {
        transactions = new PreparedOutcomeOwnerTransaction(
            recorder ?? throw new ArgumentNullException(nameof(recorder)),
            diagnostics ?? throw new ArgumentNullException(nameof(diagnostics)));
    }

    public bool TryPrepare(
        in CharacterSkillActivationOutcomeReceipt receipt,
        out PreparedCharacterSkillActivationOutcome prepared,
        out string failureReason)
    {
        prepared = default;
        if (!transactions.TryPrepare(
                receipt,
                out PreparedOwnerOutcome token,
                out OwnerOutcomeCommitResult replay,
                out _,
                out failureReason))
        {
            return false;
        }
        prepared = new PreparedCharacterSkillActivationOutcome(
            token,
            receipt.ResultKey,
            receipt.OwnerRevision,
            replay.DurablyCommitted);
        return true;
    }

    public OwnerOutcomeCommitResult Commit(
        in PreparedCharacterSkillActivationOutcome prepared) =>
        prepared.IsReplay
            ? transactions.Reconcile(prepared.ResultKey)
            : transactions.Commit(prepared.Prepared, prepared.OwnerRevision);

    public OwnerOutcomeCommitResult Reconcile(GameplayResultKey resultKey) =>
        transactions.Reconcile(resultKey);

    public void Cancel(in PreparedCharacterSkillActivationOutcome prepared)
    {
        if (!prepared.IsReplay)
            transactions.Cancel(prepared.Prepared);
    }
}

internal sealed class CharacterSkillActivationMemoryPolicy :
    IOutcomeMemoryPolicy
{
    public int PolicyVersion => 1;

    public GameplayMemorySignature GetSignature(
        in GameplayOutcomeReadView outcome,
        GameplayEntityId subjectId) => new(
        outcome.OutcomeTypeId.Value + ":" + subjectId.Kind.Value + ":"
        + outcome.GetFact(1).Value);

    public OutcomeMemoryEvaluation Evaluate(
        in GameplayOutcomeReadView outcome,
        GameplayEntityId subjectId,
        int priorMatchingCount,
        int evaluationDay)
    {
        int age = Math.Max(0, evaluationDay - outcome.AbsoluteDay);
        float salience = Math.Clamp(
            0.62f - Math.Min(0.22f, age * 0.008f)
            - Math.Min(0.12f, priorMatchingCount * 0.01f),
            0f,
            1f);
        return new OutcomeMemoryEvaluation(
            salience,
            NarrativeMemoryTier.Recent,
            Math.Max(evaluationDay + 3, outcome.AbsoluteDay + 3));
    }
}

internal sealed class CharacterSkillActivationPerceptionPolicy :
    IOutcomePerceptionPolicy
{
    public int MaximumOptionalWitnessLinks => 0;
    public bool ShouldCreateOptionalWitnessLink(
        in GameplayOutcomeReadView outcome,
        in GameplayOutcomeSubjectLink candidate) => false;
}

internal sealed class CharacterSkillActivationConsolidator :
    IOutcomeMemoryConsolidator
{
    public bool CanCompact(
        in GameplayOutcomeReadView outcome,
        GameplayEntityId subjectId) => false;
    public bool IsAdditiveMetric(GameplayMetricId metricId) => false;
}

internal sealed class CharacterSkillActivationPerspectiveProjector :
    INarrativePerspectiveProjector
{
    public NarrativeView Project(
        in GameplayOutcomeReadView outcome,
        NarrativePerspectiveContext perspective)
    {
        GameplayOutcomeParticipant source = outcome.GetParticipant(0);
        string text = source.DisplayName.DisplayText + ": 기술 「"
            + outcome.GetFact(2).Value + "」을 대상 "
            + outcome.GetFact(13).Value + "명에게 발동했다.";
        return new NarrativeView(
            outcome.OutcomeId,
            perspective.Kind,
            text,
            "character-skill-activation-v1",
            true);
    }
}

public sealed class CharacterSkillActivationOutcomeDescriptor :
    IGameplayOutcomeDescriptor
{
    private static readonly INarrativePerspectiveProjector Projector =
        new CharacterSkillActivationPerspectiveProjector();
    private static readonly IOutcomeMemoryPolicy Memory =
        new CharacterSkillActivationMemoryPolicy();
    private static readonly IOutcomePerceptionPolicy Perception =
        new CharacterSkillActivationPerceptionPolicy();
    private static readonly IOutcomeMemoryConsolidator Consolidator =
        new CharacterSkillActivationConsolidator();

    public GameplayOutcomeTypeId OutcomeTypeId =>
        CharacterSkillActivationOutcomeIds.Activated;
    public INarrativePerspectiveProjector PerspectiveProjector => Projector;
    public IOutcomeMemoryPolicy MemoryPolicy => Memory;
    public IOutcomePerceptionPolicy PerceptionPolicy => Perception;
    public IOutcomeMemoryConsolidator MemoryConsolidator => Consolidator;

    public bool IsKnownRole(GameplayRoleId roleId) =>
        roleId.Equals(CharacterSkillActivationOutcomeIds.SourceRole)
        || roleId.Equals(CharacterSkillActivationOutcomeIds.RecipientRole)
        || roleId.Equals(CharacterSkillActivationOutcomeIds.SourceRecipientRole);

    public bool IsKnownMetric(
        GameplayMetricId metricId,
        GameplayMetricUnitId unitId) =>
        unitId.Equals(CharacterSkillActivationOutcomeIds.RevisionUnit)
            && (metricId.Equals(
                    CharacterSkillActivationOutcomeIds.OwnerRevisionBefore)
                || metricId.Equals(
                    CharacterSkillActivationOutcomeIds.OwnerRevisionAfter))
        || unitId.Equals(CharacterSkillActivationOutcomeIds.CountUnit)
            && (metricId.Equals(
                    CharacterSkillActivationOutcomeIds.UseCountBefore)
                || metricId.Equals(
                    CharacterSkillActivationOutcomeIds.UseCountAfter))
        || unitId.Equals(CharacterSkillActivationOutcomeIds.HourUnit)
            && (metricId.Equals(
                    CharacterSkillActivationOutcomeIds.ReadyHourBefore)
                || metricId.Equals(
                    CharacterSkillActivationOutcomeIds.ReadyHourAfter));

    public OutcomeValidationResult Validate(
        in GameplayOutcomeReadView outcome)
    {
        int baseCount = CharacterSkillActivationOutcomeIds
            .OrderedBaseFacts.Length;
        if (outcome.OutcomeTypeId != OutcomeTypeId
            || outcome.ParticipantCount < 1
            || outcome.SubjectCount != outcome.ParticipantCount
            || outcome.MetricCount != 6
            || outcome.TagCount != 2
            || outcome.AnchorCount != 2
            || outcome.ProvenanceCount != 2
            || outcome.FactCount <= baseCount)
        {
            return OutcomeValidationResult.Reject(
                "character-skill-activation-shape-invalid");
        }
        GameplayOutcomeParticipant source = outcome.GetParticipant(0);
        if (!source.EntityId.Kind.Equals(
                CharacterSkillActivationOutcomeIds.CharacterKind)
            || !(source.RoleId.Equals(
                    CharacterSkillActivationOutcomeIds.SourceRole)
                || source.RoleId.Equals(
                    CharacterSkillActivationOutcomeIds.SourceRecipientRole)))
        {
            return OutcomeValidationResult.Reject(
                "character-skill-activation-source-invalid");
        }
        for (int index = 0; index < outcome.ParticipantCount; index++)
        {
            GameplayOutcomeParticipant participant =
                outcome.GetParticipant(index);
            if (!participant.EntityId.Kind.Equals(
                    CharacterSkillActivationOutcomeIds.CharacterKind)
                || !HasSubject(outcome, participant.EntityId)
                || index > 0 && !participant.RoleId.Equals(
                    CharacterSkillActivationOutcomeIds.RecipientRole))
            {
                return OutcomeValidationResult.Reject(
                    "character-skill-activation-participant-invalid");
            }
        }
        for (int index = 0; index < baseCount; index++)
        {
            if (!outcome.GetFact(index).FactId.Equals(
                    CharacterSkillActivationOutcomeIds.OrderedBaseFacts[index]))
            {
                return OutcomeValidationResult.Reject(
                    "character-skill-activation-fact-order-invalid");
            }
        }
        if (!int.TryParse(
                outcome.GetFact(16).Value,
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out int chunks)
            || chunks <= 0
            || baseCount + chunks != outcome.FactCount)
        {
            return OutcomeValidationResult.Reject(
                "character-skill-activation-chunk-count-invalid");
        }
        StringBuilder canonical = new();
        for (int index = 0; index < chunks; index++)
        {
            GameplayOutcomeFact chunk = outcome.GetFact(baseCount + index);
            if (!chunk.FactId.Equals(
                    CharacterSkillActivationOutcomeIds
                        .RecipientStateChunk(index)))
            {
                return OutcomeValidationResult.Reject(
                    "character-skill-activation-chunk-order-invalid");
            }
            canonical.Append(chunk.Value);
        }
        if (NarrativeInferenceHash.ComputeSha256Utf8(canonical.ToString())
                .Substring("sha256:".Length)
            != outcome.GetFact(14).Value)
        {
            return OutcomeValidationResult.Reject(
                "character-skill-activation-state-hash-invalid");
        }
        return OutcomeValidationResult.Accepted;
    }

    private static bool HasSubject(
        in GameplayOutcomeReadView outcome,
        GameplayEntityId entityId)
    {
        for (int index = 0; index < outcome.SubjectCount; index++)
        {
            if (outcome.GetSubject(index).SubjectId == entityId)
                return true;
        }
        return false;
    }
}

public static class CharacterSkillActivationOutcomeRegistration
{
    public static void RegisterCharacterSkillActivationGameplayOutcomes(
        this IContainerBuilder builder)
    {
        builder.Register<CharacterSkillActivationGameplayOutcomeBridge>(
                Lifetime.Singleton)
            .As<ICharacterSkillActivationOutcomeCommitter>();
        builder.RegisterGameplayActionLogs(nameof(CharacterSkillActivationOutcomeAdapter));
    }
}
