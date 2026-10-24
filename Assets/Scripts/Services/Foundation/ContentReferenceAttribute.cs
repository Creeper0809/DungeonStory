using System;
using UnityEngine;

// Authoring metadata only. Runtime and save data continue to store the stable ID.
public enum ContentReferenceKind
{
    Item, Weapon, Armor, Shield, Ammunition, Building, BuildingNumeric, Research, ResearchNumeric,
    Recipe, Species, AnatomyProfile, AnatomyNode, Proficiency, Culture, Practice,
    Ambition, LifeEvent, Faction, FactionChapter, FactionContract, EnemyAbility,
    EnemyArchetype, BattlefieldModifier, Disease, EffectTarget, WeightedTarget,
    Definition, SpeciesOrAnatomy, NarrativeTarget, ExpeditionChoice, ExpeditionSite, AnatomySpecies,
    AuthoredEffectText, InvasionPattern, EncounterTarget, Equipment,
    WorkerTrait, WorkType, GameplayValueTarget, SkillCapability,
    CampaignTarget, AcquiredTraitBinding, SkillModule, SkillVariant,
    EquipmentCapability, FacilityCapability
}

[AttributeUsage(AttributeTargets.Field)]
public sealed class ContentReferenceAttribute : PropertyAttribute
{
    public ContentReferenceKind Kind { get; }
    public int EmptyNumericId { get; }
    public string DefinitionTypeName { get; }
    public string IdMemberName { get; }
    // Only explicit, existing compatibility aliases or contextual keys belong here.
    // This is never a fallback for a missing definition.
    public string Aliases { get; set; }
    public bool IgnoreCase { get; set; }
    public ContentReferenceAttribute(ContentReferenceKind kind, int emptyNumericId = -1)
    {
        Kind = kind;
        EmptyNumericId = emptyNumericId;
    }

    // Type names keep authoring metadata from introducing dependencies between
    // otherwise independent domain assemblies. The catalog resolves and checks
    // both the concrete definition type and its identity member.
    public ContentReferenceAttribute(string definitionTypeName, string idMemberName, int emptyNumericId = -1)
    {
        Kind = ContentReferenceKind.Definition;
        DefinitionTypeName = definitionTypeName;
        IdMemberName = idMemberName;
        EmptyNumericId = emptyNumericId;
    }
}

// Published identities are compatibility contracts, not editable display names.
[AttributeUsage(AttributeTargets.Field)]
public sealed class ContentIdentityAttribute : PropertyAttribute { }
