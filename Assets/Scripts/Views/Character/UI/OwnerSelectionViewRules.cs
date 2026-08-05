using System;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

internal static class OwnerSelectionViewRules
{
    public static string MakeButtonLabel(
        CharacterSO candidate,
        IOwnerDoctrineDefinitionCatalog doctrines)
    {
        if (candidate == null)
        {
            return "없음";
        }

        OwnerDoctrineDefinition doctrine = (doctrines
                ?? throw new ArgumentNullException(nameof(doctrines)))
            .ResolveFor(candidate);
        string summary = string.IsNullOrWhiteSpace(candidate.ownerSummary)
            ? "균형 잡힌 운영을 지향하는 사장"
            : candidate.ownerSummary.Trim();
        return doctrine == null
            ? $"{candidate.characterName}\n{candidate.SpeciesTag}\n\n{summary}"
            : $"{candidate.characterName}\n{candidate.SpeciesTag} · {doctrine.title}\n\n{summary}\n\n이점  {doctrine.benefit}\n대가  {doctrine.tradeoff}";
    }

    public static Image CreatePanel(string name, Transform parent, Color color)
    {
        GameObject panel = new GameObject(
            name,
            typeof(RectTransform),
            typeof(Image));
        panel.transform.SetParent(parent, false);
        Image image = panel.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = true;
        return image;
    }

    public static GameObject CreateHorizontalRow(
        Transform parent,
        string name,
        float height,
        float spacing)
    {
        GameObject row = new GameObject(
            name,
            typeof(RectTransform),
            typeof(LayoutElement),
            typeof(HorizontalLayoutGroup));
        row.transform.SetParent(parent, false);
        row.GetComponent<LayoutElement>().preferredHeight = height;
        HorizontalLayoutGroup layout = row.GetComponent<HorizontalLayoutGroup>();
        layout.spacing = spacing;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = true;
        return row;
    }

    public static void SetLayout(
        GameObject target,
        float preferredHeight,
        float flexibleHeight = 0f)
    {
        LayoutElement layout = target.GetComponent<LayoutElement>();
        if (layout == null)
        {
            layout = target.AddComponent<LayoutElement>();
        }

        layout.preferredHeight = preferredHeight;
        layout.flexibleHeight = flexibleHeight;
    }

    public static string PotentialLabel(CharacterPotentialGrade grade)
    {
        return grade switch
        {
            CharacterPotentialGrade.Promising => "유망",
            CharacterPotentialGrade.Excellent => "우수",
            CharacterPotentialGrade.Exceptional => "탁월",
            CharacterPotentialGrade.Genius => "천재",
            _ => "평범"
        };
    }

    public static string RarityLabel(CharacterSkillRarity rarity)
    {
        return rarity switch
        {
            CharacterSkillRarity.Advanced => "고급",
            CharacterSkillRarity.Rare => "희귀",
            CharacterSkillRarity.Heroic => "영웅",
            CharacterSkillRarity.Legendary => "전설",
            _ => "일반"
        };
    }

    public static bool IsSaveModalOpen()
    {
        for (int sceneIndex = 0; sceneIndex < SceneManager.sceneCount; sceneIndex++)
        {
            Scene scene = SceneManager.GetSceneAt(sceneIndex);
            if (!scene.IsValid() || !scene.isLoaded)
            {
                continue;
            }

            foreach (GameObject rootObject in scene.GetRootGameObjects())
            {
                Transform modal = FindDescendantByName(
                    rootObject != null ? rootObject.transform : null,
                    "SaveModal");
                if (modal != null && modal.gameObject.activeInHierarchy)
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static Transform FindDescendantByName(
        Transform root,
        string targetName)
    {
        if (root == null)
        {
            return null;
        }

        if (root.name == targetName)
        {
            return root;
        }

        for (int index = 0; index < root.childCount; index++)
        {
            Transform found = FindDescendantByName(
                root.GetChild(index),
                targetName);
            if (found != null)
            {
                return found;
            }
        }

        return null;
    }
}
