using System;
using DungeonStory.Foundation;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public struct NoticeFeedEvent
{
    public string notice;
    public Grade grade;

    public enum Grade
    {
        NONE = 0,
        WARNING = 1,
        DANGER = 2
    }

    public NoticeFeedEvent(string notice, Grade grade)
    {
        this.notice = notice;
        this.grade = grade;
    }
}

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public static class GameEventBusNoticeFeedExtensions
{
    public static void ShowNotice(
        this IGameEventBus gameEventBus,
        string notice,
        NoticeFeedEvent.Grade grade)
    {
        if (gameEventBus == null)
        {
            throw new ArgumentNullException(nameof(gameEventBus));
        }

        gameEventBus.Publish(new NoticeFeedEvent(notice, grade));
    }
}

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public interface INoticeFeedPresenter
{
    void Present(GameObject prefab, Transform parent, NoticeFeedEvent notice);
}
