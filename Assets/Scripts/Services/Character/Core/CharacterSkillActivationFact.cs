using System;
using System.Collections.Generic;
using System.Linq;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using DungeonStory.Foundation.Facts;

/// <summary>
/// Frozen evidence of an applied skill, not a request to execute it. It carries
/// no actor, mutable definition, presentation policy, or storage implementation.
/// Related publication keys are opaque to the skill owner.
/// </summary>
public sealed class CharacterSkillActivationFact
{
    public const int MaximumStateCharacters = 4096;
    public const int MaximumRecipients = 2048;
    private readonly CharacterSkillActivationFactData data;

    public CharacterSkillActivationFact(CharacterSkillActivationFactData data)
    {
        this.data = (data ?? throw new ArgumentNullException(nameof(data))).Clone();
        Validate(this.data);
        Recipients = Array.AsReadOnly(this.data.recipients
            .Select(value => new CharacterSkillActivationRecipientFact(value))
            .ToArray());
        ChildPublicationKeys = Array.AsReadOnly(this.data.childPublicationKeys.ToArray());
        var canonical = new StringBuilder("skill-recipient-states-v1;");
        canonical.Append(Recipients.Count.ToString(CultureInfo.InvariantCulture)).Append(';');
        foreach (var recipient in Recipients.OrderBy(value => value.CharacterId, StringComparer.Ordinal))
            foreach (string token in new[] { recipient.CharacterId, recipient.BeforeState, recipient.AfterState })
                canonical.Append(token.Length.ToString(CultureInfo.InvariantCulture)).Append(':').Append(token).Append(';');
        RecipientStateSha256 = HashCanonical(canonical.ToString());
    }

    public string SourceCharacterId => data.sourceCharacterId;
    public string SourceDisplayName => data.sourceDisplayName;
    public long OwnerRevision => data.ownerRevision;
    public CharacterSkillActivationKind ActivationKind => data.activationKind;
    public string SkillId => data.skillId;
    public string SkillDisplayName => data.skillDisplayName;
    public CharacterSkillTrigger Trigger => data.trigger;
    public CharacterSkillTarget Target => data.target;
    public CharacterSkillTargetingMode TargetingMode => data.targetingMode;
    public CharacterSkillEffectArea EffectArea => data.effectArea;
    public int AreaSize => data.areaSize;
    public int DurationHours => data.durationHours;
    public int CooldownDays => data.cooldownDays;
    public int AbsoluteDay => data.absoluteDay;
    public long AbsoluteHour => data.absoluteHour;
    public long ExpiresAbsoluteHour => data.expiresAbsoluteHour;
    public int UseCountBefore => data.useCountBefore;
    public int UseCountAfter => data.useCountAfter;
    public long ReadyAbsoluteHourBefore => data.readyAbsoluteHourBefore;
    public long ReadyAbsoluteHourAfter => data.readyAbsoluteHourAfter;
    public string MechanicsSha256 => data.mechanicsSha256;
    public IReadOnlyList<CharacterSkillActivationRecipientFact> Recipients { get; }
    public IReadOnlyList<string> ChildPublicationKeys { get; }
    public string RecipientStateSha256 { get; }
    public string TriggerEventId => data.triggerEventId;
    public string TriggerEventKeySha256 => data.triggerEventKeySha256;
    public CharacterUltimateDomain UltimateDomain => data.ultimateDomain;
    public int TriggerSerial => data.triggerSerial;

    public CharacterSkillActivationFactData Snapshot() => data.Clone();

    public bool MatchesUseLimit(CharacterSkillActivationCommitState value, string publicationKey) =>
        value != null && value.ownerRevision == OwnerRevision && value.resultKey == publicationKey
        && value.activationKind == ActivationKind && value.skillId == SkillId && value.mechanicsSha256 == MechanicsSha256
        && value.absoluteHour == AbsoluteHour && value.expiresAbsoluteHour == ExpiresAbsoluteHour
        && value.useCountBefore == UseCountBefore && value.useCountAfter == UseCountAfter
        && value.readyAbsoluteHourBefore == ReadyAbsoluteHourBefore && value.readyAbsoluteHourAfter == ReadyAbsoluteHourAfter
        && value.recipientStateSha256 == RecipientStateSha256
        && value.recipientCharacterIds != null && value.recipientCharacterIds.SequenceEqual(
            Recipients.Select(recipient => recipient.CharacterId).OrderBy(id => id, StringComparer.Ordinal))
        && value.triggerEventId == TriggerEventId && value.triggerEventKeySha256 == TriggerEventKeySha256
        && value.ultimateDomain == UltimateDomain && value.triggerSerial == TriggerSerial;

    internal static string HashCanonical(string value)
    {
        using SHA256 sha = SHA256.Create();
        byte[] digest = sha.ComputeHash(Encoding.UTF8.GetBytes(value));
        return string.Concat(digest.Select(part => part.ToString("x2", CultureInfo.InvariantCulture)));
    }

    private static void Validate(CharacterSkillActivationFactData value)
    {
        RequireIdentity(value.sourceCharacterId, nameof(value.sourceCharacterId));
        RequireIdentity(value.skillId, nameof(value.skillId));
        RequireText(value.sourceDisplayName, nameof(value.sourceDisplayName), true);
        RequireText(value.skillDisplayName, nameof(value.skillDisplayName), false);
        if (value.ownerRevision <= 0L
            || !Enum.IsDefined(typeof(CharacterSkillActivationKind), value.activationKind)
            || value.activationKind == CharacterSkillActivationKind.None
            || !Enum.IsDefined(typeof(CharacterSkillTrigger), value.trigger)
            || !Enum.IsDefined(typeof(CharacterSkillTarget), value.target)
            || !Enum.IsDefined(typeof(CharacterSkillTargetingMode), value.targetingMode)
            || !Enum.IsDefined(typeof(CharacterSkillEffectArea), value.effectArea)
            || value.areaSize < 1 || value.durationHours < 0 || value.cooldownDays < 0)
            throw new ArgumentException("skill-activation-fact-definition-invalid");
        if (value.absoluteDay < 0 || value.absoluteHour < 0L
            || value.expiresAbsoluteHour < value.absoluteHour
            || value.useCountBefore < 0 || value.useCountBefore == int.MaxValue
            || value.useCountAfter != value.useCountBefore + 1)
            throw new ArgumentException("skill-activation-fact-transition-invalid");
        if (!IsSha256(value.mechanicsSha256))
            throw new ArgumentException("skill-activation-fact-mechanics-invalid");

        bool eventBound = value.activationKind is
            CharacterSkillActivationKind.AutomaticPassive
            or CharacterSkillActivationKind.AutomaticUltimate
            or CharacterSkillActivationKind.ManualCombatUltimate;
        if (!eventBound)
        {
            if (value.readyAbsoluteHourBefore < 0L
                || value.readyAbsoluteHourAfter < value.absoluteHour
                || value.triggerEventId != string.Empty
                || value.triggerEventKeySha256 != string.Empty
                || value.ultimateDomain != CharacterUltimateDomain.None
                || value.triggerSerial != -1)
                throw new ArgumentException("skill-activation-fact-manual-transition-invalid");
        }
        else
        {
            RequireText(value.triggerEventId, nameof(value.triggerEventId), false);
            if (value.readyAbsoluteHourBefore != 0L || value.readyAbsoluteHourAfter != 0L
                || !IsSha256(value.triggerEventKeySha256))
                throw new ArgumentException("skill-activation-fact-event-invalid");
            bool passive = value.activationKind == CharacterSkillActivationKind.AutomaticPassive;
            if (passive
                    ? value.ultimateDomain != CharacterUltimateDomain.None || value.triggerSerial != -1
                    : value.ultimateDomain == CharacterUltimateDomain.None
                        || !Enum.IsDefined(typeof(CharacterUltimateDomain), value.ultimateDomain)
                        || value.triggerSerial < 0)
                throw new ArgumentException("skill-activation-fact-ultimate-limit-invalid");
        }

        if (value.recipients == null || value.recipients.Count == 0
            || value.recipients.Count > MaximumRecipients
            || value.recipients.Any(recipient => recipient == null))
            throw new ArgumentException("skill-activation-fact-recipients-invalid");
        var identities = new HashSet<string>(StringComparer.Ordinal);
        foreach (CharacterSkillActivationRecipientFactData recipient in value.recipients)
        {
            RequireIdentity(recipient.characterId, nameof(recipient.characterId));
            RequireText(recipient.displayName, nameof(recipient.displayName), true);
            RequireText(recipient.beforeState, nameof(recipient.beforeState), false);
            RequireText(recipient.afterState, nameof(recipient.afterState), false);
            if (!identities.Add(recipient.characterId))
                throw new ArgumentException("skill-activation-fact-recipient-duplicate");
        }
        if (value.childPublicationKeys == null)
            throw new ArgumentException("skill-activation-fact-children-missing");
        identities.Clear();
        foreach (string key in value.childPublicationKeys)
        {
            // Only the observer interprets its own durable identity format.
            RequireText(key, nameof(value.childPublicationKeys), false);
            if (!identities.Add(key))
                throw new ArgumentException("skill-activation-fact-child-duplicate");
        }
    }

    private static void RequireIdentity(string value, string name)
    {
        if (!DomainFactValueSyntax.IsIdentity(value))
            throw new ArgumentException("skill-activation-fact-identity-invalid", name);
    }

    private static void RequireText(string value, string name, bool allowEmpty)
    {
        if (!DomainFactValueSyntax.IsText(value, MaximumStateCharacters, allowEmpty))
            throw new ArgumentException("skill-activation-fact-text-invalid", name);
    }

    private static bool IsSha256(string value) => value != null && value.Length == 64
        && value.All(character => character is >= '0' and <= '9'
            or >= 'a' and <= 'f' or >= 'A' and <= 'F');
}

public sealed class CharacterSkillActivationRecipientFact
{
    internal CharacterSkillActivationRecipientFact(CharacterSkillActivationRecipientFactData data)
    {
        CharacterId = data.characterId;
        DisplayName = data.displayName;
        BeforeState = data.beforeState;
        AfterState = data.afterState;
    }

    public string CharacterId { get; }
    public string DisplayName { get; }
    public string BeforeState { get; }
    public string AfterState { get; }
}

/// <summary>Value-only save/transport representation; construction freezes a private copy.</summary>
[Serializable]
public sealed class CharacterSkillActivationFactData
{
    public string sourceCharacterId = string.Empty;
    public string sourceDisplayName = string.Empty;
    public long ownerRevision;
    public CharacterSkillActivationKind activationKind;
    public string skillId = string.Empty;
    public string skillDisplayName = string.Empty;
    public CharacterSkillTrigger trigger;
    public CharacterSkillTarget target;
    public CharacterSkillTargetingMode targetingMode;
    public CharacterSkillEffectArea effectArea;
    public int areaSize = 1;
    public int durationHours;
    public int cooldownDays;
    public int absoluteDay;
    public long absoluteHour;
    public long expiresAbsoluteHour;
    public int useCountBefore;
    public int useCountAfter;
    public long readyAbsoluteHourBefore;
    public long readyAbsoluteHourAfter;
    public string mechanicsSha256 = string.Empty;
    public List<CharacterSkillActivationRecipientFactData> recipients = new();
    public List<string> childPublicationKeys = new();
    public string triggerEventId = string.Empty;
    public string triggerEventKeySha256 = string.Empty;
    public CharacterUltimateDomain ultimateDomain;
    public int triggerSerial = -1;

    public CharacterSkillActivationFactData Clone()
    {
        var copy = (CharacterSkillActivationFactData)MemberwiseClone();
        copy.recipients = recipients?.Select(value => value?.Clone()).ToList();
        copy.childPublicationKeys = childPublicationKeys?.ToList();
        return copy;
    }
}

[Serializable]
public sealed class CharacterSkillActivationRecipientFactData
{
    public string characterId = string.Empty;
    public string displayName = string.Empty;
    public string beforeState = string.Empty;
    public string afterState = string.Empty;
    public CharacterSkillActivationRecipientFactData Clone() =>
        (CharacterSkillActivationRecipientFactData)MemberwiseClone();
}
