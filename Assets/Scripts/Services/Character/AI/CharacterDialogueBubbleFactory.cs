using System;
using TMPro;
using UnityEngine;
using VContainer;

public interface ICharacterDialogueBubbleFactory
{
    TextMeshPro Create(Transform parent);
}

public sealed class CharacterDialogueBubbleFactory : ICharacterDialogueBubbleFactory
{
    private readonly ITmpKoreanFontService tmpKoreanFontService;

    [Inject]
    public CharacterDialogueBubbleFactory(ITmpKoreanFontService tmpKoreanFontService)
    {
        this.tmpKoreanFontService = tmpKoreanFontService
            ?? throw new ArgumentNullException(nameof(tmpKoreanFontService));
    }

    public TextMeshPro Create(Transform parent)
    {
        if (parent == null)
        {
            throw new ArgumentNullException(nameof(parent));
        }

        GameObject textObject = new GameObject("CharacterDialogueBubble", typeof(TextMeshPro));
        textObject.transform.SetParent(parent, false);

        TextMeshPro text = textObject.GetComponent<TextMeshPro>();
        text.alignment = TextAlignmentOptions.Center;
        text.fontSize = 2.2f;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        tmpKoreanFontService.Apply(text);

        MeshRenderer renderer = text.GetComponent<MeshRenderer>();
        if (renderer != null)
        {
            renderer.sortingOrder = 210;
        }

        return text;
    }
}
