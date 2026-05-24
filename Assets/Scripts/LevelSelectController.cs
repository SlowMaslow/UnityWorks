using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Экран выбора уровней. Панель выезжает снизу (как WinScreen).
/// Карточки создаются динамически при открытии.
/// </summary>
public class LevelSelectController : MonoBehaviour
{
    [Header("Ссылки")]
    [SerializeField] private Font          gameFont;          // Bangers шрифт
    [SerializeField] private GameObject    levelSelectPanel; // полный overlay (включает/выключается)
    [SerializeField] private RectTransform panelRT;          // панель которая анимируется
    [SerializeField] private Transform     cardContainer;    // GridLayoutGroup

    // ─── Цвета карточек ──────────────────────────────────────────────────────
    private static readonly Color ColCardUnlocked = new Color(0.72f, 0.28f, 0.08f);
    private static readonly Color ColCardLocked   = new Color(0.48f, 0.48f, 0.52f);
    private static readonly Color ColStarFilled   = new Color(1.00f, 0.85f, 0.10f);
    private static readonly Color ColStarEmpty    = new Color(0.20f, 0.15f, 0.08f);
    private static readonly Color ColLockIcon     = new Color(0.85f, 0.85f, 0.90f);

    private const int FIRST_LEVEL = 1;

    private Vector2 _shownPos;
    private Vector2 _hiddenPos;

    // ─── Unity ───────────────────────────────────────────────────────────────
    private void Awake()
    {
        if (panelRT != null)
        {
            _shownPos  = panelRT.anchoredPosition;
            _hiddenPos = new Vector2(_shownPos.x, _shownPos.y - 900f);
            panelRT.anchoredPosition = _hiddenPos;
        }
        levelSelectPanel?.SetActive(false);
    }

    // ─── Public API ──────────────────────────────────────────────────────────
    public void Show()
    {
        levelSelectPanel?.SetActive(true);
        BuildCards();
        StopAllCoroutines();
        StartCoroutine(SlideIn());
    }

    public void Hide()
    {
        StopAllCoroutines();
        StartCoroutine(SlideOut());
    }

    // ─── Animation ───────────────────────────────────────────────────────────
    private IEnumerator SlideIn()
    {
        if (panelRT == null) yield break;
        panelRT.anchoredPosition = _hiddenPos;

        float duration = 0.5f;
        float elapsed  = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t  = 1f - Mathf.Pow(1f - Mathf.Clamp01(elapsed / duration), 3f); // ease-out cubic
            panelRT.anchoredPosition = Vector2.Lerp(_hiddenPos, _shownPos, t);
            yield return null;
        }
        panelRT.anchoredPosition = _shownPos;
    }

    private IEnumerator SlideOut()
    {
        if (panelRT == null) { levelSelectPanel?.SetActive(false); yield break; }

        float duration = 0.3f;
        float elapsed  = 0f;
        Vector2 from   = panelRT.anchoredPosition;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t  = Mathf.Clamp01(elapsed / duration);
            float et = t * t; // ease-in
            panelRT.anchoredPosition = Vector2.Lerp(from, _hiddenPos, et);
            yield return null;
        }
        panelRT.anchoredPosition = _hiddenPos;
        levelSelectPanel?.SetActive(false);
    }

    // ─── Cards ───────────────────────────────────────────────────────────────
    private void BuildCards()
    {
        foreach (Transform c in cardContainer) Destroy(c.gameObject);

        int total     = LevelLoader.TotalLevels;
        int lastLevel = SaveSystem.LastLevel;

        for (int i = FIRST_LEVEL; i <= total; i++)
            CreateCard(i, i <= lastLevel, SaveSystem.GetLevelStars(i));
    }

    private void CreateCard(int idx, bool unlocked, int stars)
    {
        var card = MakeGO($"Card_{idx}", cardContainer);
        var rt   = card.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(130, 140);

        var bg = card.AddComponent<Image>();
        bg.color = unlocked ? ColCardUnlocked : ColCardLocked;

        var btn = card.AddComponent<Button>();
        btn.targetGraphic = bg;
        btn.interactable  = unlocked;

        if (unlocked)
        {
            int capture = idx;
            btn.onClick.AddListener(() => LoadLevel(capture));
        }

        // Номер уровня
        Color numColor = unlocked ? Color.white : new Color(1f, 1f, 1f, 0.35f);
        MakeText(card.transform, "Num", idx.ToString(),
            new Vector2(0.5f, 0.63f), new Vector2(120, 64), 52, FontStyle.Bold, numColor);

        if (!unlocked)
            MakeText(card.transform, "LockLabel", "LOCKED",
                new Vector2(0.5f, 0.15f), new Vector2(120, 28), 13, FontStyle.Bold,
                new Color(1f, 1f, 1f, 0.45f));

        if (unlocked)
            MakeStarsRow(card.transform, stars);
    }

    private void MakeStarsRow(Transform parent, int filled)
    {
        var row   = MakeGO("Stars", parent);
        var rowRt = row.GetComponent<RectTransform>();
        rowRt.anchorMin        = new Vector2(0.5f, 0f);
        rowRt.anchorMax        = new Vector2(0.5f, 0f);
        rowRt.pivot            = new Vector2(0.5f, 0f);
        rowRt.anchoredPosition = new Vector2(0f, 10f);
        rowRt.sizeDelta        = new Vector2(110f, 26f);

        var hlg = row.AddComponent<HorizontalLayoutGroup>();
        hlg.childAlignment         = TextAnchor.MiddleCenter;
        hlg.spacing                = 4f;
        hlg.childForceExpandWidth  = true;
        hlg.childForceExpandHeight = true;

        // Звёзды — используем LegacyRuntime: Fredoka One не содержит символы ★/☆
        var starFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        for (int i = 0; i < 3; i++)
        {
            var sGO = MakeGO($"S{i}", row.transform);
            var t   = sGO.AddComponent<Text>();
            t.text      = "★";
            t.font      = starFont;
            t.fontSize  = 20;
            t.alignment = TextAnchor.MiddleCenter;
            t.color     = i < filled ? ColStarFilled : ColStarEmpty;
        }
    }

    private static void LoadLevel(int idx)
    {
        LevelLoader.PendingLevel = idx;
        if (ScreenFader.Instance != null)
            ScreenFader.Instance.FadeToScene(1);
        else
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(1);
        }
    }

    // ─── Helpers ─────────────────────────────────────────────────────────────
    private static GameObject MakeGO(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go;
    }

    private void MakeText(Transform parent, string name, string content,
        Vector2 anchor, Vector2 size, int fontSize, FontStyle style, Color color)
    {
        var go = MakeGO(name, parent);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin        = anchor;
        rt.anchorMax        = anchor;
        rt.pivot            = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta        = size;

        var txt       = go.AddComponent<Text>();
        txt.text      = content;
        txt.font      = gameFont != null ? gameFont : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        txt.fontSize  = fontSize;
        txt.fontStyle = style;
        txt.alignment = TextAnchor.MiddleCenter;
        txt.color     = color;
    }
}
