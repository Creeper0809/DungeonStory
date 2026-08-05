using System;
using UnityEngine;
using VContainer;

public interface IResearchTreeWindowFactory
{
    ResearchTreeWindow Ensure(GameObject panelObject);
}

public sealed class ResearchTreeWindowFactory : IResearchTreeWindowFactory
{
    private readonly IObjectResolver objectResolver;

    public ResearchTreeWindowFactory(IObjectResolver objectResolver)
    {
        this.objectResolver = objectResolver
            ?? throw new ArgumentNullException(nameof(objectResolver));
    }

    public ResearchTreeWindow Ensure(GameObject panelObject)
    {
        if (panelObject == null)
        {
            throw new ArgumentNullException(nameof(panelObject));
        }

        ResearchTreeWindow window = panelObject.GetComponent<ResearchTreeWindow>();
        if (window == null)
        {
            window = panelObject.AddComponent<ResearchTreeWindow>();
        }

        objectResolver.Inject(window);
        window.ConfigureHost();
        return window;
    }
}
