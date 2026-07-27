using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

[DisallowMultipleComponent]
public class CharacterAbilityCache : SerializedMonoBehaviour
{
    private CharacterAbility[] characterAbilities;
    private IReadOnlyList<CharacterAbility> characterAbilitiesView;
    private readonly Dictionary<Type, CharacterAbility> abilityByRequestedType =
        new Dictionary<Type, CharacterAbility>();
    private bool isAbilityCache;

    public IReadOnlyList<CharacterAbility> Abilities
    {
        get
        {
            CacheAbility();
            return characterAbilitiesView ??= ReadOnlyView.List(
                characterAbilities ?? Array.Empty<CharacterAbility>());
        }
    }

    private void Awake()
    {
        CacheAbility();
    }

    public void CacheAbility()
    {
        if (isAbilityCache) return;
        RefreshAbilityCache();
    }

    public void RefreshAbilityCache()
    {
        characterAbilities = GetComponents<CharacterAbility>();
        characterAbilitiesView = ReadOnlyView.List(characterAbilities);
        abilityByRequestedType.Clear();
        for (int i = 0; i < characterAbilities.Length; i++)
        {
            CharacterAbility ability = characterAbilities[i];
            if (ability != null)
            {
                abilityByRequestedType[ability.GetType()] = ability;
            }
        }

        isAbilityCache = true;
    }

    public T GetAbility<T>() where T : CharacterAbility
    {
        if (TryGetAbility(out T result))
        {
            return result;
        }

        Debug.Log($"{gameObject.name}: {typeof(T).Name} 능력이 없습니다");
        return null;
    }

    public bool TryGetAbility<T>(out T result) where T : CharacterAbility
    {
        CacheAbility();
        Type requestedType = typeof(T);
        if (abilityByRequestedType.TryGetValue(
                requestedType,
                out CharacterAbility cached))
        {
            result = cached as T;
            return result != null;
        }

        for (int i = 0; i < characterAbilities.Length; i++)
        {
            CharacterAbility ability = characterAbilities[i];
            if (ability is T characterAbility)
            {
                abilityByRequestedType[requestedType] = characterAbility;
                result = characterAbility;
                return true;
            }
        }

        result = null;
        return false;
    }
}
