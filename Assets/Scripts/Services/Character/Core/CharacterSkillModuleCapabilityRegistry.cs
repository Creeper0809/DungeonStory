using System;
using System.Collections.Generic;

public enum CharacterSkillCapabilityDomain
{
    Combat,
    Management
}

public enum CharacterSkillCapabilityTargetPolicy
{
    EnemyOnly,
    FriendlyOnly,
    ActorEither,
    NonEnemy
}

public enum CharacterSkillManagementReachability
{
    None,
    WorkStartedOrCompleted,
    WorkCompleted,
    BroadEvent,
    RelationshipChanged
}

public sealed class CharacterSkillModuleCapabilityDescriptor
{
    private readonly HashSet<CharacterSkillTrigger> selfTriggerHazards;

    public CharacterSkillModuleCapabilityDescriptor(
        string capabilityId,
        CharacterSkillCapabilityDomain domain,
        CharacterSkillCapabilityTargetPolicy targetPolicy,
        CharacterSkillManagementReachability managementReachability =
            CharacterSkillManagementReachability.None,
        params CharacterSkillTrigger[] selfTriggerHazards)
    {
        CapabilityId = capabilityId ?? throw new ArgumentNullException(nameof(capabilityId));
        Domain = domain;
        TargetPolicy = targetPolicy;
        ManagementReachability = managementReachability;
        this.selfTriggerHazards = new HashSet<CharacterSkillTrigger>(
            selfTriggerHazards ?? Array.Empty<CharacterSkillTrigger>());
    }

    public string CapabilityId { get; }
    public CharacterSkillCapabilityDomain Domain { get; }
    public CharacterSkillCapabilityTargetPolicy TargetPolicy { get; }
    public CharacterSkillManagementReachability ManagementReachability { get; }

    public bool IsTargetCompatible(CharacterSkillTarget target)
    {
        bool enemy = target == CharacterSkillTarget.Enemy
            || target == CharacterSkillTarget.AllEnemies;
        bool friendly = target == CharacterSkillTarget.Self
            || target == CharacterSkillTarget.Ally
            || target == CharacterSkillTarget.AllAllies;
        return TargetPolicy switch
        {
            CharacterSkillCapabilityTargetPolicy.EnemyOnly => enemy,
            CharacterSkillCapabilityTargetPolicy.FriendlyOnly => friendly,
            CharacterSkillCapabilityTargetPolicy.ActorEither => enemy || friendly,
            CharacterSkillCapabilityTargetPolicy.NonEnemy => !enemy,
            _ => false
        };
    }

    public bool WouldSelfTrigger(CharacterSkillTrigger trigger) =>
        selfTriggerHazards.Contains(trigger);

    public bool IsManagementModuleReachable(
        CharacterSkillKind kind,
        CharacterSkillTrigger trigger)
    {
        if (Domain != CharacterSkillCapabilityDomain.Management)
        {
            return false;
        }
        if (kind == CharacterSkillKind.Ultimate)
        {
            return true;
        }
        if (kind == CharacterSkillKind.Active)
        {
            return trigger == CharacterSkillTrigger.ManualWork;
        }
        if (kind != CharacterSkillKind.Passive)
        {
            return false;
        }

        return ManagementReachability switch
        {
            CharacterSkillManagementReachability.WorkStartedOrCompleted =>
                trigger == CharacterSkillTrigger.WorkStarted
                || trigger == CharacterSkillTrigger.WorkCompleted,
            CharacterSkillManagementReachability.WorkCompleted =>
                trigger == CharacterSkillTrigger.WorkCompleted,
            CharacterSkillManagementReachability.BroadEvent =>
                trigger == CharacterSkillTrigger.WorkStarted
                || trigger == CharacterSkillTrigger.WorkCompleted
                || trigger == CharacterSkillTrigger.NeedChanged
                || trigger == CharacterSkillTrigger.MoodChanged
                || trigger == CharacterSkillTrigger.RelationshipChanged
                || trigger == CharacterSkillTrigger.OperatingDayStarted,
            CharacterSkillManagementReachability.RelationshipChanged =>
                trigger == CharacterSkillTrigger.RelationshipChanged,
            _ => false
        };
    }
}

/// <summary>
/// Runtime registry for reusable CharacterSkill mechanics. Authored module IDs
/// remain stable content identities; capability IDs select the C# behavior.
/// </summary>
public static class CharacterSkillModuleCapabilityRegistry
{
    private static readonly IReadOnlyDictionary<string, CharacterSkillModuleCapabilityDescriptor>
        Descriptors = BuildDescriptors();

    public static IEnumerable<string> CapabilityIds => Descriptors.Keys;

    public static CharacterSkillModuleCapabilityDescriptor Require(
        CharacterSkillModuleRule module)
    {
        if (module == null) throw new ArgumentNullException(nameof(module));
        string capabilityId = module.capabilityId?.Trim() ?? string.Empty;
        if (capabilityId.Length == 0
            || !string.Equals(capabilityId, module.capabilityId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"CharacterSkill module '{module.id ?? string.Empty}' requires an explicit canonical capabilityId.");
        }
        if (!Descriptors.TryGetValue(capabilityId, out CharacterSkillModuleCapabilityDescriptor descriptor))
        {
            throw new InvalidOperationException(
                $"CharacterSkill module '{module.id ?? string.Empty}' references unknown capabilityId '{capabilityId}'.");
        }

        bool combatModule = module is CharacterCombatSkillModuleRule;
        bool managementModule = module is CharacterManagementSkillModuleRule;
        bool expectedCombat = descriptor.Domain == CharacterSkillCapabilityDomain.Combat;
        if (combatModule == managementModule || combatModule != expectedCombat)
        {
            throw new InvalidOperationException(
                $"CharacterSkill module '{module.id ?? string.Empty}' class does not match capabilityId '{capabilityId}' domain '{descriptor.Domain}'.");
        }
        return descriptor;
    }

    public static CharacterSkillModuleCapabilityDescriptor Require(string capabilityId)
    {
        string canonical = capabilityId?.Trim() ?? string.Empty;
        if (canonical.Length == 0
            || !string.Equals(canonical, capabilityId, StringComparison.Ordinal)
            || !Descriptors.TryGetValue(canonical, out CharacterSkillModuleCapabilityDescriptor descriptor))
        {
            throw new InvalidOperationException(
                $"Unknown or noncanonical CharacterSkill capabilityId '{capabilityId ?? string.Empty}'.");
        }
        return descriptor;
    }

    private static IReadOnlyDictionary<string, CharacterSkillModuleCapabilityDescriptor>
        BuildDescriptors()
    {
        Dictionary<string, CharacterSkillModuleCapabilityDescriptor> values =
            new Dictionary<string, CharacterSkillModuleCapabilityDescriptor>(StringComparer.Ordinal);

        AddCombat(values, CharacterSkillCapabilityTargetPolicy.EnemyOnly,
            "damage", "dot", "vulnerability", "delay", "debuff",
            "multi_target", "conditional_amplify");
        AddCombat(values, CharacterSkillCapabilityTargetPolicy.FriendlyOnly,
            "heal", "guard", "buff", "cleanse", "protect", "cooldown_adjust");
        AddCombat(values, CharacterSkillCapabilityTargetPolicy.ActorEither,
            "reposition");

        AddManagement(values, "work_speed",
            CharacterSkillManagementReachability.WorkStartedOrCompleted);
        AddManagement(values, "output", CharacterSkillManagementReachability.WorkCompleted);
        AddManagement(values, "repair", CharacterSkillManagementReachability.WorkCompleted);
        AddManagement(values, "stock", CharacterSkillManagementReachability.WorkCompleted);
        AddManagement(values, "research", CharacterSkillManagementReachability.WorkCompleted);
        AddManagement(values, "revenue", CharacterSkillManagementReachability.WorkCompleted);
        AddManagement(values, "cleaning", CharacterSkillManagementReachability.BroadEvent);
        AddManagement(values, "needs", CharacterSkillManagementReachability.BroadEvent,
            CharacterSkillTrigger.NeedChanged);
        AddManagement(values, "mood", CharacterSkillManagementReachability.BroadEvent,
            CharacterSkillTrigger.MoodChanged);
        AddManagement(values, "relationship",
            CharacterSkillManagementReachability.RelationshipChanged,
            CharacterSkillTrigger.RelationshipChanged);
        return values;
    }

    private static void AddCombat(
        IDictionary<string, CharacterSkillModuleCapabilityDescriptor> values,
        CharacterSkillCapabilityTargetPolicy targetPolicy,
        params string[] capabilityIds)
    {
        foreach (string capabilityId in capabilityIds)
        {
            Add(values, new CharacterSkillModuleCapabilityDescriptor(
                capabilityId,
                CharacterSkillCapabilityDomain.Combat,
                targetPolicy));
        }
    }

    private static void AddManagement(
        IDictionary<string, CharacterSkillModuleCapabilityDescriptor> values,
        string capabilityId,
        CharacterSkillManagementReachability reachability,
        params CharacterSkillTrigger[] selfTriggerHazards)
    {
        Add(values, new CharacterSkillModuleCapabilityDescriptor(
            capabilityId,
            CharacterSkillCapabilityDomain.Management,
            CharacterSkillCapabilityTargetPolicy.NonEnemy,
            reachability,
            selfTriggerHazards));
    }

    private static void Add(
        IDictionary<string, CharacterSkillModuleCapabilityDescriptor> values,
        CharacterSkillModuleCapabilityDescriptor descriptor)
    {
        if (!values.TryAdd(descriptor.CapabilityId, descriptor))
        {
            throw new InvalidOperationException(
                $"Duplicate CharacterSkill capabilityId '{descriptor.CapabilityId}'.");
        }
    }
}
