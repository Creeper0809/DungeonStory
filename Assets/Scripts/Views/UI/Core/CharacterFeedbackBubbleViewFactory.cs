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
    private readonly IWorldUiHierarchy worldUiHierarchy;

    [Inject]
    public CharacterFeedbackBubbleViewFactory(
        ITmpKoreanFontService tmpKoreanFontService,
        IWorldUiHierarchy worldUiHierarchy)
    {
        this.tmpKoreanFontService = tmpKoreanFontService
            ?? throw new ArgumentNullException(nameof(tmpKoreanFontService));
        this.worldUiHierarchy = worldUiHierarchy
            ?? throw new ArgumentNullException(nameof(worldUiHierarchy));
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

        Transform currentParent = text.transform.parent;
        bool parentIsDeactivating = currentParent != null
            && (!currentParent.gameObject.activeSelf
                || !currentParent.gameObject.activeInHierarchy);
        text.SetText(string.Empty);
        text.gameObject.SetActive(false);
        if (parentIsDeactivating)
        {
            UnityEngine.Object.Destroy(text.gameObject);
            return;
        }

        Transform poolParent = Application.isPlaying
            ? worldUiHierarchy.GetWorldUiRoot()
            : null;
        text.transform.SetParent(poolParent, false);
        textPool.Push(text);
    }

    private TextMeshPro CreateTextView()
    {
        GameObject bubbleObject = new GameObject("CharacterFeedbackBubble", typeof(TextMeshPro));
        worldUiHierarchy.ParentToWorldUi(bubbleObject);
        TextMeshPro view = bubbleObject.GetComponent<TextMeshPro>();
        view.alignment = TextAlignmentOptions.Center;
        view.fontSize = 3.2f;
        view.textWrappingMode = TextWrappingModes.NoWrap;
        tmpKoreanFontService.Apply(view);
        return view;
    }
}
