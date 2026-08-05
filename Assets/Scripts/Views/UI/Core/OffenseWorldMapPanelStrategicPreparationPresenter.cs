using System;
using TMPro;
using UnityEngine;

public sealed class OffenseStrategicPreparationPresenter
{
    private readonly OffenseWorldMapStrategicViewFactory viewFactory;
    private readonly TMP_Text detailText;

    public OffenseStrategicPreparationPresenter(
        OffenseWorldMapStrategicViewFactory viewFactory,
        TMP_Text detailText)
    {
        this.viewFactory = viewFactory
            ?? throw new ArgumentNullException(nameof(viewFactory));
        this.detailText = detailText
            ?? throw new ArgumentNullException(nameof(detailText));
    }

    public void AddAction(
        string label,
        Action callback,
        Color? color = null)
    {
        viewFactory.AddRightButton(label, callback, color);
    }

    public void SetDetail(string text)
    {
        detailText.text = text ?? string.Empty;
    }
}
