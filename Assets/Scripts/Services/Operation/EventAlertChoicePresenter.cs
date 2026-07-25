using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public sealed class EventAlertChoicePresenter
{
    private readonly List<Button> choiceButtons = new List<Button>();
    private readonly IEventAlertButtonFactory buttonFactory;

    public EventAlertChoicePresenter(IEventAlertButtonFactory buttonFactory)
    {
        this.buttonFactory = buttonFactory
            ?? throw new ArgumentNullException(nameof(buttonFactory));
    }

    public void Rebuild(Transform parent, EventAlertRecord record, Func<int, bool> executeChoice)
    {
        Clear();
        if (parent == null || record == null || executeChoice == null || record.Choices.Count == 0)
        {
            return;
        }

        for (int i = 0; i < record.Choices.Count; i++)
        {
            EventAlertChoice choice = record.Choices[i];
            int choiceIndex = i;
            Button button = buttonFactory.CreateChoiceButton(
                parent,
                choice,
                i,
                () => executeChoice(choiceIndex));
            if (button != null)
            {
                choiceButtons.Add(button);
            }
        }
    }

    public void Clear()
    {
        foreach (Button button in choiceButtons)
        {
            if (button != null)
            {
                buttonFactory.Release(button);
            }
        }

        choiceButtons.Clear();
    }
}
