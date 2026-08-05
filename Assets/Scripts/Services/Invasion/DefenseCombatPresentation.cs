using System.Collections;
using DungeonStory.Foundation;
using TMPro;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class DefenseCombatPresentation : MonoBehaviour
{
    private CharacterActor actor;
    private TextMeshPro engagementText;
    private TextMeshPro damageText;
    private SpriteRenderer heldEquipment;
    private Coroutine attackRoutine;
    private Coroutine hitRoutine;
    private Coroutine damageRoutine;
    private Coroutine statusRoutine;
    private Coroutine reloadRoutine;
    private Vector3 visualRestPosition;
    private Quaternion visualRestRotation = Quaternion.identity;
    private Color visualRestColor = Color.white;
    private bool downed;
    private IGameClock gameClock;
    private ITmpKoreanFontService tmpKoreanFontService;
    private string currentStatus = string.Empty;
    private bool combatActive;

    public string CurrentStatus => currentStatus;
    public bool IsCombatActive => combatActive;

    public static DefenseCombatPresentation Ensure(CharacterActor owner)
    {
        if (owner == null)
        {
            return null;
        }

        if (!owner.TryGetComponent(out DefenseCombatPresentation presentation))
        {
            presentation = owner.gameObject.AddComponent<DefenseCombatPresentation>();
        }

        presentation.Bind(owner, owner.TmpKoreanFontService);
        return presentation;
    }

    private void Awake()
    {
        Bind(GetComponent<CharacterActor>());
    }

    private void OnDisable()
    {
        StopAllCoroutines();
        WorldCharacterNameplate.Ensure(actor)?.SetCombatActive(false);
        if (heldEquipment != null)
        {
            heldEquipment.gameObject.SetActive(false);
        }
        RestoreVisual();
    }

    public void Bind(CharacterActor owner)
    {
        Bind(owner, owner != null ? owner.TmpKoreanFontService : null);
    }

    public void Bind(CharacterActor owner, ITmpKoreanFontService fontService)
    {
        actor = owner;
        gameClock = owner != null ? owner.GameClock : null;
        tmpKoreanFontService = fontService;
        EnsureTexts();
        CacheVisualRestState();
    }

    public void SetEngaged(bool engaged)
    {
        SetStatus(engaged ? "교전 중" : string.Empty, engaged);
        if (!engaged)
        {
            RestoreVisual();
        }
    }

    public void SetStatus(string status, bool combatActive = false)
    {
        currentStatus = status?.Trim() ?? string.Empty;
        this.combatActive = combatActive;
        EnsureTexts();
        if (statusRoutine != null)
        {
            StopCoroutine(statusRoutine);
            statusRoutine = null;
        }

        if (engagementText != null)
        {
            engagementText.text = currentStatus;
            float roleOffset = string.Equals(engagementText.text, "대기", System.StringComparison.Ordinal)
                ? 0.22f
                : 0f;
            engagementText.transform.localPosition = new Vector3(
                0f,
                GetVisualHeight() + 0.12f + roleOffset,
                0f);
            engagementText.gameObject.SetActive(!string.IsNullOrWhiteSpace(engagementText.text));
        }

        WorldCharacterNameplate.Ensure(actor)?.SetCombatActive(combatActive);
    }

    public void ShowTemporaryStatus(string status, float seconds)
    {
        SetStatus(status, combatActive: false);
        if (isActiveAndEnabled && !string.IsNullOrWhiteSpace(status) && seconds > 0f)
        {
            statusRoutine = StartCoroutine(HideStatusAfter(seconds));
        }
    }

    public void PlayAttack(Vector3 targetWorldPosition, CombatWeaponSnapshot weapon = null)
    {
        if (!isActiveAndEnabled || actor == null)
        {
            return;
        }

        if (attackRoutine != null)
        {
            StopCoroutine(attackRoutine);
        }

        PrepareHeldEquipment(weapon, targetWorldPosition);
        attackRoutine = StartCoroutine(AttackMotion(targetWorldPosition, weapon));
    }

    public void PlayReload(CombatWeaponSnapshot weapon, float duration)
    {
        if (!isActiveAndEnabled || actor == null || weapon == null)
        {
            return;
        }

        if (reloadRoutine != null)
        {
            StopCoroutine(reloadRoutine);
        }

        PrepareHeldEquipment(weapon, actor.transform.position + Vector3.right);
        reloadRoutine = StartCoroutine(ReloadMotion(Mathf.Max(0.1f, duration)));
    }

    public void SetDowned(bool value)
    {
        downed = value;
        Transform visual = actor != null ? actor.VisualRoot : null;
        if (visual == null)
        {
            return;
        }

        if (value)
        {
            CacheVisualRestState();
            visual.localRotation = visualRestRotation * Quaternion.Euler(0f, 0f, -90f);
            visual.localPosition = visualRestPosition + new Vector3(0f, 0.08f, 0f);
            if (heldEquipment != null)
            {
                heldEquipment.gameObject.SetActive(false);
            }
        }
        else
        {
            visual.localRotation = visualRestRotation;
            visual.localPosition = visualRestPosition;
        }
    }

    public void PlayHit(
        float damage,
        CombatDamageType damageType,
        IWorldUiHierarchy worldUiHierarchy)
    {
        if (!isActiveAndEnabled || actor == null)
        {
            return;
        }

        WorldCharacterNameplate.Ensure(actor)?.RevealHealthForCombat(2.5f);
        if (hitRoutine != null)
        {
            StopCoroutine(hitRoutine);
        }

        if (damageRoutine != null)
        {
            StopCoroutine(damageRoutine);
        }

        hitRoutine = StartCoroutine(HitFlash());
        damageRoutine = StartCoroutine(ShowDamage(damage));
        CombatImpactPresentation.Play(
            actor.transform.position,
            damageType,
            gameClock,
            worldUiHierarchy);
    }

    private IEnumerator AttackMotion(Vector3 targetWorldPosition, CombatWeaponSnapshot weapon)
    {
        Transform visual = actor != null ? actor.VisualRoot : null;
        if (visual == null)
        {
            yield break;
        }

        CacheVisualRestState();
        float direction = Mathf.Sign(targetWorldPosition.x - actor.transform.position.x);
        bool ranged = weapon?.IsRanged == true;
        Vector3 lunge = visualRestPosition + new Vector3(
            direction * (ranged ? 0.06f : 0.13f),
            ranged ? 0.02f : 0f,
            0f);
        float elapsed = 0f;
        const float duration = 0.18f;
        while (elapsed < duration)
        {
            float phase = elapsed / duration;
            float amount = phase < 0.45f
                ? phase / 0.45f
                : 1f - ((phase - 0.45f) / 0.55f);
            visual.localPosition = Vector3.Lerp(visualRestPosition, lunge, Mathf.Clamp01(amount));
            elapsed += GameDeltaTime;
            yield return null;
        }

        visual.localPosition = visualRestPosition;
        if (heldEquipment != null)
        {
            heldEquipment.gameObject.SetActive(false);
        }
        attackRoutine = null;
    }

    private IEnumerator ReloadMotion(float duration)
    {
        if (heldEquipment == null)
        {
            yield break;
        }

        Vector3 rest = heldEquipment.transform.localPosition;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            float phase = Mathf.Sin((elapsed / duration) * Mathf.PI * 4f);
            heldEquipment.transform.localPosition = rest + Vector3.down * (0.04f + phase * 0.025f);
            elapsed += GameDeltaTime;
            yield return null;
        }

        heldEquipment.transform.localPosition = rest;
        heldEquipment.gameObject.SetActive(false);
        reloadRoutine = null;
    }

    private IEnumerator HitFlash()
    {
        SpriteRenderer renderer = actor != null ? actor.VisualRenderer : null;
        if (renderer == null)
        {
            yield break;
        }

        visualRestColor = renderer.color;
        renderer.color = new Color(1f, 0.28f, 0.24f, visualRestColor.a);
        float elapsed = 0f;
        const float duration = 0.16f;
        while (elapsed < duration)
        {
            elapsed += GameDeltaTime;
            yield return null;
        }

        if (renderer != null)
        {
            renderer.color = visualRestColor;
        }

        hitRoutine = null;
    }

    private IEnumerator ShowDamage(float damage)
    {
        EnsureTexts();
        if (damageText == null)
        {
            yield break;
        }

        damageText.text = $"-{Mathf.Max(1, Mathf.RoundToInt(damage))}";
        damageText.color = new Color(1f, 0.32f, 0.24f, 1f);
        damageText.gameObject.SetActive(true);
        Vector3 start = new Vector3(0f, GetVisualHeight() + 0.28f, 0f);
        Vector3 end = start + Vector3.up * 0.32f;
        float elapsed = 0f;
        const float duration = 0.55f;
        while (elapsed < duration)
        {
            float t = elapsed / duration;
            damageText.transform.localPosition = Vector3.Lerp(start, end, t);
            Color color = damageText.color;
            color.a = 1f - t;
            damageText.color = color;
            elapsed += GameDeltaTime;
            yield return null;
        }

        damageText.gameObject.SetActive(false);
        damageRoutine = null;
    }

    private IEnumerator HideStatusAfter(float seconds)
    {
        float elapsed = 0f;
        while (elapsed < seconds)
        {
            elapsed += GameDeltaTime;
            yield return null;
        }

        statusRoutine = null;
        SetStatus(string.Empty, combatActive: false);
    }

    private float GameDeltaTime => gameClock?.DeltaTime ?? actor?.GameClock?.DeltaTime ?? 0f;

    private void PrepareHeldEquipment(CombatWeaponSnapshot weapon, Vector3 targetWorldPosition)
    {
        EnsureHeldEquipment();
        IDungeonItemCatalogProvider itemCatalog =
            actor?.WorldItemStackRuntime?.CatalogProvider;
        string itemId = weapon != null
            ? PhysicalItemIds.ForEquipment(weapon.DefinitionId)
            : string.Empty;
        if (heldEquipment == null
            || weapon == null
            || string.IsNullOrWhiteSpace(weapon.DefinitionId)
            || itemCatalog == null
            || !itemCatalog.TryGetDefinition(itemId, out DungeonItemDefinition item)
            || item?.Sprite == null)
        {
            if (heldEquipment != null)
            {
                heldEquipment.gameObject.SetActive(false);
            }

            return;
        }

        float direction = Mathf.Sign(targetWorldPosition.x - actor.transform.position.x);
        if (Mathf.Approximately(direction, 0f))
        {
            direction = 1f;
        }

        heldEquipment.sprite = item.Sprite;
        heldEquipment.flipX = direction < 0f;
        heldEquipment.transform.localPosition = new Vector3(
            direction * 0.22f,
            Mathf.Max(0.28f, GetVisualHeight() * 0.45f),
            0f);
        heldEquipment.transform.localRotation = Quaternion.Euler(
            0f,
            0f,
            weapon.Kind == CombatEquipmentKind.MeleeWeapon ? direction * -18f : 0f);
        heldEquipment.gameObject.SetActive(true);
    }

    private void EnsureHeldEquipment()
    {
        if (heldEquipment != null || actor == null)
        {
            return;
        }

        Transform child = transform.Find("CombatHeldEquipment");
        if (child == null)
        {
            child = new GameObject("CombatHeldEquipment").transform;
            child.SetParent(transform, false);
        }

        heldEquipment = child.GetComponent<SpriteRenderer>();
        if (heldEquipment == null)
        {
            heldEquipment = child.gameObject.AddComponent<SpriteRenderer>();
        }

        SpriteRenderer actorRenderer = actor.VisualRenderer;
        heldEquipment.sortingLayerID = actorRenderer != null
            ? actorRenderer.sortingLayerID
            : SortingLayer.NameToID("DungeonObject");
        heldEquipment.sortingOrder = actorRenderer != null
            ? actorRenderer.sortingOrder + 1
            : 1;
        heldEquipment.gameObject.SetActive(false);
    }

    private void EnsureTexts()
    {
        if (actor == null)
        {
            return;
        }

        engagementText = EnsureText(
            "DefenseEngagementMarker",
            engagementText,
            string.Empty,
            new Color(1f, 0.42f, 0.2f, 1f),
            GetVisualHeight() + 0.12f,
            42);
        damageText = EnsureText(
            "DefenseDamageNumber",
            damageText,
            string.Empty,
            new Color(1f, 0.32f, 0.24f, 1f),
            GetVisualHeight() + 0.28f,
            44);
        if (string.IsNullOrWhiteSpace(engagementText.text))
        {
            engagementText.gameObject.SetActive(false);
        }
        if (string.IsNullOrWhiteSpace(damageText.text))
        {
            damageText.gameObject.SetActive(false);
        }
    }

    private TextMeshPro EnsureText(
        string objectName,
        TextMeshPro current,
        string value,
        Color color,
        float localY,
        int sortingOrder)
    {
        if (current != null)
        {
            return current;
        }

        Transform child = transform.Find(objectName);
        if (child == null)
        {
            child = new GameObject(objectName).transform;
            child.SetParent(transform, false);
        }

        TextMeshPro text = child.GetComponent<TextMeshPro>();
        if (text == null)
        {
            text = child.gameObject.AddComponent<TextMeshPro>();
        }

        text.text = value;
        text.alignment = TextAlignmentOptions.Center;
        text.fontSize = 2.8f;
        text.fontStyle = FontStyles.Bold;
        text.color = color;
        ApplyKoreanFont(text);
        if (text.fontSharedMaterial != null)
        {
            text.outlineColor = new Color(0.02f, 0.01f, 0.01f, 0.95f);
            text.outlineWidth = 0.2f;
        }
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.raycastTarget = false;
        text.transform.localPosition = new Vector3(0f, localY, 0f);
        MeshRenderer renderer = text.GetComponent<MeshRenderer>();
        if (renderer != null)
        {
            renderer.sortingLayerName = "UI";
            renderer.sortingOrder = sortingOrder;
        }

        return text;
    }

    private void ApplyKoreanFont(TextMeshPro text)
    {
        if (text == null || tmpKoreanFontService == null)
        {
            return;
        }

        tmpKoreanFontService.Apply(text);
    }

    private float GetVisualHeight()
    {
        return actor != null ? Mathf.Max(0.7f, actor.GetVisualTopLocalY()) : 1f;
    }

    private void CacheVisualRestState()
    {
        Transform visual = actor != null ? actor.VisualRoot : null;
        if (visual != null && attackRoutine == null && !downed)
        {
            visualRestPosition = visual.localPosition;
            visualRestRotation = visual.localRotation;
        }

        SpriteRenderer renderer = actor != null ? actor.VisualRenderer : null;
        if (renderer != null && hitRoutine == null)
        {
            visualRestColor = renderer.color;
        }
    }

    private void RestoreVisual()
    {
        if (actor == null)
        {
            return;
        }

        if (actor.VisualRoot != null)
        {
            actor.VisualRoot.localPosition = visualRestPosition;
            actor.VisualRoot.localRotation = downed
                ? visualRestRotation * Quaternion.Euler(0f, 0f, -90f)
                : visualRestRotation;
        }

        if (actor.VisualRenderer != null)
        {
            actor.VisualRenderer.color = visualRestColor;
        }
    }
}
