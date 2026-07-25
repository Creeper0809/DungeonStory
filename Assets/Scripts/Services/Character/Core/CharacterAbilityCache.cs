using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

[DisallowMultipleComponent]
public class CharacterAbilityCache : SerializedMonoBehaviour
{
    private CharacterAbility[] characterAbilities;
    private IReadOnlyList<CharacterAbility> characterAbilitiesView;
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
        isAbilityCache = true;
    }

    public T GetAbility<T>() where T : CharacterAbility
    {
        CacheAbility();
        foreach (CharacterAbility ability in characterAbilities)
        {
            if (ability is T characterAbility)
            {
                return characterAbility;
            }
        }

        Debug.Log($"{gameObject.name}: {typeof(T).Name} 능력이 없습니다");
        return null;
    }

    public bool TryGetAbility<T>(out T result) where T : CharacterAbility
    {
        CacheAbility();
        foreach (CharacterAbility ability in characterAbilities)
        {
            if (ability is T characterAbility)
            {
                result = characterAbility;
                return true;
            }
        }

        result = null;
        return false;
    }
}
