using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using VContainer;

public interface ICharacterFeedbackBubbleViewFactory
{
    TextMeshPro Acquire(Transform parent, Vector3 localPosition);
    void Release(TextMeshPro text);
}

public sealed class CharacterFeedbackBubbleViewFactory : ICharacterFeedbackBubbleViewFactory
{
    private readonly Stack<TextMeshPro> textPool = new Stack<TextMeshPro>();
    private readonly ITmpKoreanFontService tmpKoreanFontService;

    [Inject]
    public CharacterFeedbackBubbleViewFactory(ITmpKoreanFontService tmpKoreanFontService)
    {
        this.tmpKoreanFontService = tmpKoreanFontService
            ?? throw new ArgumentNullException(nameof(tmpKoreanFontService));
    }

    public TextMeshPro Acquire(Transform parent, Vector3 localPosition)
    {
        if (parent == null)
        {
            throw new ArgumentNullException(nameof(parent));
        }

        TextMeshPro text = textPool.Count > 0 ? textPool.Pop() : CreateTextView();
        tmpKoreanFontService.Apply(text);
        text.transform.SetParent(parent, false);
        text.transform.localPosition = localPosition;
        text.gameObject.SetActive(true);

        MeshRenderer renderer = text.GetComponent<MeshRenderer>();
        if (renderer != null)
        {
            renderer.sortingOrder = 200;
        }

        return text;
    }

    public void Release(TextMeshPro text)
    {
        if (text == null)
        {
            return;
        }

        text.text = string.Empty;
        text.gameObject.SetActive(false);
        Transform poolParent = Application.isPlaying
            ? DungeonRuntimeHierarchy.GetCategory(DungeonRuntimeHierarchy.WorldUi)
            : null;
        text.transform.SetParent(poolParent, false);
        textPool.Push(text);
    }

    private TextMeshPro CreateTextView()
    {
        GameObject bubbleObject = new GameObject("CharacterFeedbackBubble", typeof(TextMeshPro));
        DungeonRuntimeHierarchy.Parent(bubbleObject, DungeonRuntimeHierarchy.WorldUi);
        TextMeshPro view = bubbleObject.GetComponent<TextMeshPro>();
        view.alignment = TextAlignmentOptions.Center;
        view.fontSize = 3.2f;
        view.textWrappingMode = TextWrappingModes.NoWrap;
        tmpKoreanFontService.Apply(view);
        return view;
    }
}
