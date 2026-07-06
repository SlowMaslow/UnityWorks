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
    private GameObject _detailOverlay;

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
            btn.onClick.AddListener(() => ShowDetail(capture));
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

    // ─── Detail card (задачи уровня) ─────────────────────────────────────────
    private static readonly Color ColTaskDone    = new Color(0.32f, 0.82f, 0.32f);
    private static readonly Color ColTaskPending  = new Color(1f, 1f, 1f, 0.30f);
    private static readonly Color ColDetailBg     = new Color(0.16f, 0.10f, 0.06f, 0.98f);
    private static readonly Color ColDimmer       = new Color(0f, 0f, 0f, 0.60f);

    // Кириллица: gameFont (Bangers) её не содержит → детальную карточку рисуем Arial.
    private static Font UIFont => Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

    private void ShowDetail(int idx)
    {
        CloseDetail();

        var canvas = cardContainer != null
            ? cardContainer.GetComponentInParent<Canvas>()
            : FindFirstObjectByType<Canvas>();
        if (canvas == null) { LoadLevel(idx); return; }

        // Затемняющий фон (клик по нему = закрыть)
        _detailOverlay = MakeGO($"DetailOverlay_{idx}", canvas.transform);
        var ovRt = _detailOverlay.GetComponent<RectTransform>();
        ovRt.anchorMin = Vector2.zero; ovRt.anchorMax = Vector2.one;
        ovRt.offsetMin = Vector2.zero; ovRt.offsetMax = Vector2.zero;
        var dim = _detailOverlay.AddComponent<Image>();
        dim.color = ColDimmer;
        var dimBtn = _detailOverlay.AddComponent<Button>();
        dimBtn.targetGraphic = dim;
        dimBtn.onClick.AddListener(CloseDetail);

        // Карточка
        var cardGO = MakeGO("DetailCard", _detailOverlay.transform);
        var cardRt = cardGO.GetComponent<RectTransform>();
        cardRt.anchorMin = cardRt.anchorMax = cardRt.pivot = new Vector2(0.5f, 0.5f);
        cardRt.anchoredPosition = Vector2.zero;
        cardRt.sizeDelta = new Vector2(460, 400);
        var cardBg = cardGO.AddComponent<Image>();
        cardBg.color = ColDetailBg;
        cardGO.AddComponent<Button>(); // no-op: ловит клик, чтобы карточка не закрывалась

        MakeLabel(cardGO.transform, "Title", $"УРОВЕНЬ {idx}",
            new Vector2(0.5f, 0.90f), new Vector2(420, 50), 34, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter);

        MakeStarsRowAt(cardGO.transform, SaveSystem.GetLevelStars(idx), new Vector2(0.5f, 0.77f), 30);

        // Задачи (data-driven из LevelConfig)
        var tasks = LoadLevelTasks(idx);
        int mask  = SaveSystem.GetLevelTaskMask(idx);
        float y   = 0.58f;
        foreach (var task in tasks)
        {
            bool done = (mask & (1 << (int)task.type)) != 0;
            MakeTaskRow(cardGO.transform, task, done, y);
            y -= 0.12f;
        }

        MakeButton(cardGO.transform, "PlayBtn", "ИГРАТЬ", new Vector2(0.5f, 0.11f),
            new Vector2(240, 58), new Color(0.30f, 0.66f, 0.20f), () => { CloseDetail(); LoadLevel(idx); });

        MakeButton(cardGO.transform, "CloseBtn", "X", new Vector2(0.90f, 0.90f),
            new Vector2(46, 46), new Color(0.62f, 0.20f, 0.15f), CloseDetail);
    }

    private void CloseDetail()
    {
        if (_detailOverlay != null) { Destroy(_detailOverlay); _detailOverlay = null; }
    }

    /// <summary>Читает задачи уровня из LevelConfig на префабе (без инстанса).</summary>
    private List<LevelConfig.StarTask> LoadLevelTasks(int idx)
    {
        var list   = new List<LevelConfig.StarTask>();
        var prefab = Resources.Load<GameObject>($"{LevelLoader.ResourcesPath}/{LevelLoader.PrefabName(idx)}");
        var cfg    = prefab != null ? prefab.GetComponentInChildren<LevelConfig>(true) : null;
        if (cfg != null && cfg.TaskCount > 0)
            foreach (var t in cfg.Tasks) list.Add(t);
        else
            list.Add(new LevelConfig.StarTask { type = LevelConfig.StarTaskType.Complete });
        return list;
    }

    private void MakeTaskRow(Transform parent, LevelConfig.StarTask task, bool done, float anchorY)
    {
        // Галочка (Arial поддерживает ✓ и •)
        MakeLabel(parent, "Check", done ? "✓" : "•",
            new Vector2(0.13f, anchorY), new Vector2(40, 36), 28, FontStyle.Bold,
            done ? ColTaskDone : ColTaskPending, TextAnchor.MiddleCenter);

        // Текст задачи (слева)
        var go = MakeGO("Task", parent);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.21f, anchorY);
        rt.pivot = new Vector2(0f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = new Vector2(330, 36);
        var txt = go.AddComponent<Text>();
        txt.text      = LevelConfig.Describe(task);
        txt.font      = UIFont;
        txt.fontSize  = 22;
        txt.alignment = TextAnchor.MiddleLeft;
        txt.color     = done ? Color.white : new Color(1f, 1f, 1f, 0.6f);
    }

    private void MakeStarsRowAt(Transform parent, int filled, Vector2 anchor, int starSize)
    {
        var row   = MakeGO("StarsRow", parent);
        var rowRt = row.GetComponent<RectTransform>();
        rowRt.anchorMin = rowRt.anchorMax = anchor;
        rowRt.pivot = new Vector2(0.5f, 0.5f);
        rowRt.anchoredPosition = Vector2.zero;
        rowRt.sizeDelta = new Vector2(starSize * 3.6f, starSize + 6);

        var hlg = row.AddComponent<HorizontalLayoutGroup>();
        hlg.childAlignment         = TextAnchor.MiddleCenter;
        hlg.spacing                = 6f;
        hlg.childForceExpandWidth   = false;
        hlg.childForceExpandHeight  = false;

        for (int i = 0; i < 3; i++)
        {
            var sGO = MakeGO($"S{i}", row.transform);
            var le  = sGO.AddComponent<LayoutElement>();
            le.preferredWidth  = starSize + 2;
            le.preferredHeight = starSize + 4;
            var t = sGO.AddComponent<Text>();
            t.text      = "★";
            t.font      = UIFont;
            t.fontSize  = starSize;
            t.alignment = TextAnchor.MiddleCenter;
            t.color     = i < filled ? ColStarFilled : ColStarEmpty;
        }
    }

    private void MakeButton(Transform parent, string name, string label, Vector2 anchor, Vector2 size, Color color, System.Action onClick)
    {
        var go = MakeGO(name, parent);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = anchor;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = size;
        var img = go.AddComponent<Image>();
        img.color = color;
        var btn = go.AddComponent<Button>();
        btn.targetGraphic = img;
        if (onClick != null) btn.onClick.AddListener(() => onClick());
        MakeLabel(go.transform, "Label", label, new Vector2(0.5f, 0.5f), size, 26, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter);
    }

    private void MakeLabel(Transform parent, string name, string content, Vector2 anchor, Vector2 size,
        int fontSize, FontStyle style, Color color, TextAnchor align)
    {
        var go = MakeGO(name, parent);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = anchor;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = size;
        var t = go.AddComponent<Text>();
        t.text      = content;
        t.font      = UIFont;
        t.fontSize  = fontSize;
        t.fontStyle = style;
        t.alignment = align;
        t.color     = color;
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
