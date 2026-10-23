using System;
using UnityEngine;

// Authoring metadata only. Runtime and save data continue to store the stable ID.
public enum ContentReferenceKind
{
    Item, Weapon, Armor, Shield, Ammunition, Building, BuildingNumeric, Research, ResearchNumeric,
    Recipe, Species, AnatomyProfile, AnatomyNode, Proficiency, Culture, Practice,
    Ambition, LifeEvent, Faction, FactionChapter, FactionContract, EnemyAbility,
    EnemyArchetype, BattlefieldModifier, Disease, EffectTarget, WeightedTarget
}

[AttributeUsage(AttributeTargets.Field)]
public sealed class ContentReferenceAttribute : PropertyAttribute
{
    public ContentReferenceKind Kind { get; }
    public int EmptyNumericId { get; }
    public ContentReferenceAttribute(ContentReferenceKind kind, int emptyNumericId = -1)
    {
        Kind = kind;
        EmptyNumericId = emptyNumericId;
    }
}
