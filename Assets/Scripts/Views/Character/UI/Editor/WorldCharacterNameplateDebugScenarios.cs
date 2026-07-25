using System;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public static class WorldCharacterNameplateDebugScenarios
{
    [MenuItem("DungeonStory/Debug/Character/Run World Nameplate Scenarios")]
    public static void RunFromMenu()
    {
        bool success = RunAll(logSuccess: true);
        if (!success)
        {
            Debug.LogError("World character nameplate scenarios failed.");
        }
    }

    public static bool RunAll(bool logSuccess)
    {
        string report = RunHealthOverlayScenario();
        bool success = report.StartsWith("PASS", StringComparison.Ordinal);
        if (success)
        {
            if (logSuccess)
            {
                Debug.Log($"WORLD_NAMEPLATE_VALIDATION={report}");
            }

            return true;
        }

        Debug.LogError($"WORLD_NAMEPLATE_VALIDATION={report}");
        return false;
    }

    private static string RunHealthOverlayScenario()
    {
        GameObject actorObject = null;
        try
        {
            actorObject = CreateActorObject();
            CharacterActor actor = actorObject.GetComponent<CharacterActor>();
            actor.EnsureRuntimeState();
            actor.SetLifecycleState(CharacterLifecycleState.Active);
            actor.InitializeStats(resetCurrentHealth: true);

            WorldCharacterNameplate nameplate = WorldCharacterNameplate.Ensure(actor);
            nameplate.RefreshNowForDebug();

            Transform root = actorObject.transform.Find("WorldNameplate");
            TextMeshPro text = root != null ? root.GetComponentInChildren<TextMeshPro>(true) : null;
            LineRenderer[] lines = root != null
                ? root.GetComponentsInChildren<LineRenderer>(true)
                : Array.Empty<LineRenderer>();
            LineRenderer background = lines.FirstOrDefault(line => line.name == "HealthBackground");
            LineRenderer fill = lines.FirstOrDefault(line => line.name == "HealthFill");
            MeshRenderer textRenderer = text != null ? text.GetComponent<MeshRenderer>() : null;
            SpriteRenderer spriteRenderer = actor.VisualRenderer;
            bool fullHealthHidden = fill != null
                && background != null
                && !fill.gameObject.activeInHierarchy
                && !background.gameObject.activeInHierarchy;

            actor.ApplyDamage(actor.MaxHealth * 0.5f, "nameplate validation");
            nameplate.RefreshNowForDebug();
            float damagedRight = fill != null ? fill.GetPosition(1).x : float.NaN;
            float backgroundRight = background != null ? background.GetPosition(1).x : float.NaN;
            bool damagedHealthVisible = fill != null
                && background != null
                && fill.gameObject.activeInHierarchy
                && background.gameObject.activeInHierarchy
                && damagedRight < backgroundRight;

            bool valid = root != null
                && text != null
                && fill != null
                && lines.Length >= 2
                && !string.IsNullOrWhiteSpace(text.text)
                && text.fontSize >= 2f
                && fullHealthHidden
                && damagedHealthVisible
                && textRenderer != null
                && spriteRenderer != null
                && textRenderer.sortingLayerName == "UI"
                && SortingLayer.GetLayerValueFromID(textRenderer.sortingLayerID)
                    > SortingLayer.GetLayerValueFromID(spriteRenderer.sortingLayerID)
                && textRenderer.sortingOrder > spriteRenderer.sortingOrder;

            return valid
                ? $"PASS; text={text.text}; font={text.fontSize:0.###}; fullHidden={fullHealthHidden}; damagedRight={damagedRight:0.###}; backgroundRight={backgroundRight:0.###}; sorting={textRenderer.sortingLayerName}:{textRenderer.sortingOrder}"
                : $"FAIL; root={root != null}; text={text != null}; fill={fill != null}; lines={lines.Length}; font={(text != null ? text.fontSize : 0f):0.###}; fullHidden={fullHealthHidden}; damagedVisible={damagedHealthVisible}; damagedRight={damagedRight:0.###}; backgroundRight={backgroundRight:0.###}; sorting={textRenderer?.sortingLayerName}:{textRenderer?.sortingOrder}";
        }
        catch (Exception ex)
        {
            Debug.LogException(ex);
            return $"FAIL; exception={ex.GetType().Name}; message={ex.Message}";
        }
        finally
        {
            if (actorObject != null)
            {
                Object.DestroyImmediate(actorObject);
            }
        }
    }

    private static GameObject CreateActorObject()
    {
        GameObject actorObject = new GameObject("World Nameplate Scenario Actor");
        SpriteRenderer spriteRenderer = actorObject.AddComponent<SpriteRenderer>();
        spriteRenderer.sprite = CreateOnePixelSprite();
        spriteRenderer.sortingLayerName = "Default";
        spriteRenderer.sortingOrder = 120;
        actorObject.AddComponent<CharacterActor>();
        return actorObject;
    }

    private static Sprite CreateOnePixelSprite()
    {
        Texture2D texture = new Texture2D(1, 1, TextureFormat.RGBA32, mipChain: false)
        {
            hideFlags = HideFlags.HideAndDontSave
        };
        texture.SetPixel(0, 0, Color.white);
        texture.Apply(updateMipmaps: false, makeNoLongerReadable: false);
        Sprite sprite = Sprite.Create(texture, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0f), 1f);
        sprite.hideFlags = HideFlags.HideAndDontSave;
        return sprite;
    }
}
