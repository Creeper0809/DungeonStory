using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.Scripting.APIUpdating;

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public enum TabId
{
    Construction = 0,
    Buildings = 1,
    Staff = 2,
    Shop = 3,
    Warehouse = 4,
    Operations = 5,
    Defense = 6,
    Expedition = 7,
    Research = 8,
    Codex = 9
}

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public enum UITabSurfaceKind
{
    Construction,
    Staff,
    Feature
}

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class UITabDefinition
{
    public UITabDefinition(
        TabId id,
        int order,
        string buttonLabel,
        string panelTitle,
        UITabSurfaceKind surfaceKind)
    {
        Id = id;
        Order = order;
        ButtonLabel = buttonLabel ?? throw new ArgumentNullException(nameof(buttonLabel));
        PanelTitle = panelTitle ?? throw new ArgumentNullException(nameof(panelTitle));
        SurfaceKind = surfaceKind;
    }

    public TabId Id { get; }
    public int Order { get; }
    public string ButtonLabel { get; }
    public string PanelTitle { get; }
    public UITabSurfaceKind SurfaceKind { get; }
    public bool IsGenerated => Id != TabId.Construction;
}

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public static class UITabCatalog
{
    private static readonly IReadOnlyList<UITabDefinition> Definitions = Array.AsReadOnly(
        new[]
        {
            new UITabDefinition(TabId.Construction, 0, "건축", "건축", UITabSurfaceKind.Construction),
            new UITabDefinition(TabId.Buildings, 1, "건물", "건물 관리", UITabSurfaceKind.Feature),
            new UITabDefinition(TabId.Staff, 2, "직원", "직원 관리", UITabSurfaceKind.Staff),
            new UITabDefinition(TabId.Shop, 3, "상점", "상점", UITabSurfaceKind.Feature),
            new UITabDefinition(TabId.Warehouse, 4, "창고", "창고", UITabSurfaceKind.Feature),
            new UITabDefinition(TabId.Operations, 5, "운영", "운영", UITabSurfaceKind.Feature),
            new UITabDefinition(TabId.Defense, 6, "방어", "침공/방어", UITabSurfaceKind.Feature),
            new UITabDefinition(TabId.Expedition, 7, "원정", "원정", UITabSurfaceKind.Feature),
            new UITabDefinition(TabId.Research, 8, "연구", "연구/제작", UITabSurfaceKind.Feature),
            new UITabDefinition(TabId.Codex, 9, "도감", "도감/기록", UITabSurfaceKind.Feature)
        });

    private static readonly IReadOnlyDictionary<TabId, UITabDefinition> ById =
        Definitions.ToDictionary(definition => definition.Id);

    public static IReadOnlyList<UITabDefinition> All => Definitions;

    public static bool TryGet(TabId id, out UITabDefinition definition)
    {
        return ById.TryGetValue(id, out definition);
    }

    public static UITabDefinition GetRequired(TabId id)
    {
        if (!TryGet(id, out UITabDefinition definition))
        {
            throw new ArgumentOutOfRangeException(nameof(id), id, "Unknown top-level tab id.");
        }

        return definition;
    }

    public static bool TryFromLegacyId(int value, out TabId id)
    {
        id = (TabId)value;
        return ById.ContainsKey(id);
    }
}

public interface IFeatureSurfaceTabPresenter
{
    TabId Id { get; }
    void Present(IFeatureSurfaceView view);
}

public interface IFeatureSurfaceView
{
    void AddSection(string title, string summary);
    void AddLabel(string text, float fontSize, float height);
    void AddDataCard(
        string actionName,
        string title,
        string detail,
        string buttonText,
        Action onClick,
        float height);
    void ShowFeedback(string message);
    void RequestRefresh();
}

public interface IFeatureSurfaceTabPresenterRegistry
{
    bool TryGet(TabId id, out IFeatureSurfaceTabPresenter presenter);
}

public sealed class FeatureSurfaceTabPresenterRegistry : IFeatureSurfaceTabPresenterRegistry
{
    private readonly IReadOnlyDictionary<TabId, IFeatureSurfaceTabPresenter> presenters;

    public FeatureSurfaceTabPresenterRegistry(IEnumerable<IFeatureSurfaceTabPresenter> presenters)
    {
        IFeatureSurfaceTabPresenter[] registered = presenters?.ToArray()
            ?? throw new ArgumentNullException(nameof(presenters));
        IGrouping<TabId, IFeatureSurfaceTabPresenter> duplicate = registered
            .GroupBy(presenter => presenter.Id)
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicate != null)
        {
            throw new InvalidOperationException(
                $"Multiple feature surface presenters are registered for {duplicate.Key}.");
        }

        this.presenters = registered.ToDictionary(presenter => presenter.Id);
        TabId[] missing = UITabCatalog.All
            .Where(definition => definition.SurfaceKind == UITabSurfaceKind.Feature)
            .Select(definition => definition.Id)
            .Where(id => !this.presenters.ContainsKey(id))
            .ToArray();
        if (missing.Length > 0)
        {
            throw new InvalidOperationException(
                $"Feature surface presenters are missing for: {string.Join(", ", missing)}.");
        }
    }

    public bool TryGet(TabId id, out IFeatureSurfaceTabPresenter presenter)
    {
        return presenters.TryGetValue(id, out presenter);
    }
}
