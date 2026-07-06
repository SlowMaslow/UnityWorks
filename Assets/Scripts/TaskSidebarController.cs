using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Внутриигровой сайдбар прогресса задач (слева, сворачиваемый, анимированный).
/// На старте уровня выезжает, через 3 сек авто-сворачивается; таб « / » разворачивает/сворачивает.
/// Показывает живой прогресс: ключи X/N, время ≤ порога, дойти до финиша.
/// Само-спавнится в GameScene (buildIndex 1) через sceneLoaded — вручную настраивать сцену НЕ нужно.
/// ⚠️ UI построен КОДОМ (черновой) — визуал дизайним позже, см. память ui-tech-debt-prefabs.
/// </summary>
public class TaskSidebarController : MonoBehaviour
{
    // ─── Само-спавн в GameScene ──────────────────────────────────────────────
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.buildIndex != 1) return;                                   // только GameScene
        if (FindFirstObjectByType<TaskSidebarController>() != null) return;  // уже есть

        // ВАЖНО: берём ОСНОВНОЙ HUD-канвас, НЕ FadeCanvas (order=9999, без GraphicRaycaster).
        // Иначе сайдбар (а) рисуется поверх затемнения экрана и (б) таб не кликается.
        Canvas target = null;
        foreach (var c in FindObjectsByType<Canvas>(FindObjectsSortMode.None))
        {
            var root = c.rootCanvas;
            if (root == null || root.name == "FadeCanvas") continue;
            if (target == null || root.sortingOrder < target.sortingOrder) target = root;
        }
        if (target == null) return;

        var go = new GameObject("TaskSidebar", typeof(RectTransform));
        go.transform.SetParent(target.transform, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        go.AddComponent<TaskSidebarController>();
    }

    // ─── Стиль (черновой) ────────────────────────────────────────────────────
    private static Font UIFont => Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
    private static readonly Color ColPanel   = new Color(0.10f, 0.07f, 0.05f, 0.85f);
    private static readonly Color ColTab     = new Color(0.20f, 0.14f, 0.09f, 0.95f);
    private static readonly Color ColDone    = new Color(0.35f, 0.85f, 0.35f);
    private static readonly Color ColNeutral = new Color(1f, 1f, 1f, 0.85f);
    private static readonly Color ColFail    = new Color(0.95f, 0.35f, 0.30f);

    private const float PanelW            = 232f;
    private const float PanelH            = 190f;
    private const float PanelY            = 40f;
    private const float AnimDur           = 0.3f;
    private const float AutoCollapseDelay = 3f;
    private const float IntroDelay        = 0.2f; // короткая пауза перед выездом (после старта уровня)

    private RectTransform _panelRT;
    private Text          _tabLabel;
    private bool          _expanded     = false;
    private bool          _rowsBuilt    = false;
    private bool          _introStarted = false;
    private float         _collapsedX;
    private Coroutine     _anim;
    private Coroutine     _introCo;
    private Coroutine     _autoHide;
    private readonly List<System.Action> _updaters = new List<System.Action>();

    // ─── Unity ───────────────────────────────────────────────────────────────
    private void Start() => BuildChrome();

    private void Update()
    {
        if (!_rowsBuilt)
        {
            var lm = LevelManager.Instance;
            if (lm != null && lm.IsRunning) BuildRows(lm);
        }
        for (int i = 0; i < _updaters.Count; i++) _updaters[i]?.Invoke();
    }

    // ─── Chrome (панель + таб) ───────────────────────────────────────────────
    private void BuildChrome()
    {
        _collapsedX = -PanelW;

        var panelGO = new GameObject("Panel", typeof(RectTransform));
        panelGO.transform.SetParent(transform, false);
        _panelRT = panelGO.GetComponent<RectTransform>();
        _panelRT.anchorMin = _panelRT.anchorMax = new Vector2(0f, 0.5f);
        _panelRT.pivot     = new Vector2(0f, 0.5f);
        _panelRT.sizeDelta = new Vector2(PanelW, PanelH);
        _panelRT.anchoredPosition = new Vector2(_collapsedX, PanelY); // старт свёрнут (виден только таб)
        var bg = panelGO.AddComponent<Image>();
        bg.color = ColPanel;
        bg.raycastTarget = false; // не перехватывать драг пэдов

        MakeText(_panelRT, "Header", "ЗАДАЧИ", new Vector2(0.5f, 1f),
            new Vector2(PanelW - 16, 30), new Vector2(0f, -6f), 20, FontStyle.Bold, ColNeutral, TextAnchor.MiddleCenter);

        // Таб-кнопка сворачивания — на правом краю панели (единственный кликабельный элемент)
        var tabGO = new GameObject("Tab", typeof(RectTransform));
        tabGO.transform.SetParent(_panelRT, false);
        var tabRT = tabGO.GetComponent<RectTransform>();
        tabRT.anchorMin = tabRT.anchorMax = new Vector2(1f, 0.5f);
        tabRT.pivot     = new Vector2(0f, 0.5f);
        tabRT.sizeDelta = new Vector2(28f, 64f);
        tabRT.anchoredPosition = Vector2.zero;
        var tabImg = tabGO.AddComponent<Image>();
        tabImg.color = ColTab;
        var tabBtn = tabGO.AddComponent<Button>();
        tabBtn.targetGraphic = tabImg;
        tabBtn.onClick.AddListener(Toggle);
        _tabLabel = MakeText(tabRT, "TabLabel", "»", new Vector2(0.5f, 0.5f),
            new Vector2(28, 40), Vector2.zero, 22, FontStyle.Bold, ColNeutral, TextAnchor.MiddleCenter);
    }

    // ─── Rows (задачи из LevelConfig) ────────────────────────────────────────
    private void BuildRows(LevelManager lm)
    {
        _rowsBuilt = true;
        var cfg   = lm.Config;
        var tasks = new List<LevelConfig.StarTask>();
        if (cfg != null && cfg.TaskCount > 0) foreach (var t in cfg.Tasks) tasks.Add(t);
        else tasks.Add(new LevelConfig.StarTask { type = LevelConfig.StarTaskType.Complete });

        float y = -44f;
        foreach (var task in tasks)
        {
            var row = MakeText(_panelRT, "Row", "", new Vector2(0f, 1f),
                new Vector2(PanelW - 24f, 30f), new Vector2(14f, y), 20, FontStyle.Normal, ColNeutral, TextAnchor.MiddleLeft);
            var captured = task;
            _updaters.Add(() => UpdateRow(row, captured, lm));
            y -= 38f;
        }

        // Интро: выехать → подержать 3с → свернуться (один раз)
        if (!_introStarted)
        {
            _introStarted = true;
            _introCo = StartCoroutine(IntroRoutine());
        }
    }

    // ─── Выезд/сворачивание с авто-скрытием ──────────────────────────────────
    private IEnumerator IntroRoutine()
    {
        // Ждём, пока уйдёт затемнение экрана (FadeIn), чтобы игрок УВИДЕЛ выезд
        yield return new WaitForSecondsRealtime(IntroDelay);
        _introCo = null;
        Expand();
    }

    private void Toggle()
    {
        if (_introCo != null) { StopCoroutine(_introCo); _introCo = null; } // прервать интро
        if (_expanded) Collapse(); else Expand();
    }

    /// <summary>Показать цели (напр. после continue-рекламы): выезд + авто-скрытие 3с.</summary>
    public void RevealTasks()
    {
        if (!_rowsBuilt) return; // строки ещё не готовы — интро само покажет
        if (_introCo != null) { StopCoroutine(_introCo); _introCo = null; }
        Expand();
    }

    private void Expand()
    {
        _expanded = true;
        UpdateTab();
        StartAnim(0f);
        RestartAutoHide(); // всегда авто-скрытие через 3с, если игрок не свернёт сам
    }

    private void Collapse()
    {
        _expanded = false;
        UpdateTab();
        StartAnim(_collapsedX);
        CancelAutoHide();
    }

    private void RestartAutoHide()
    {
        CancelAutoHide();
        _autoHide = StartCoroutine(AutoHideRoutine());
    }

    private void CancelAutoHide()
    {
        if (_autoHide != null) { StopCoroutine(_autoHide); _autoHide = null; }
    }

    private IEnumerator AutoHideRoutine()
    {
        yield return new WaitForSecondsRealtime(AutoCollapseDelay);
        _autoHide = null;
        Collapse();
    }

    private void UpdateTab() { if (_tabLabel != null) _tabLabel.text = _expanded ? "«" : "»"; }

    private void StartAnim(float targetX)
    {
        if (_anim != null) StopCoroutine(_anim);
        _anim = StartCoroutine(AnimateTo(targetX));
    }

    private IEnumerator AnimateTo(float targetX)
    {
        float startX = _panelRT.anchoredPosition.x;
        float el = 0f;
        while (el < AnimDur)
        {
            el += Time.unscaledDeltaTime;
            float t  = Mathf.Clamp01(el / AnimDur);
            float et = 1f - Mathf.Pow(1f - t, 3f); // ease-out cubic
            SetX(Mathf.Lerp(startX, targetX, et));
            yield return null;
        }
        SetX(targetX);
        _anim = null;
    }

    private void SetX(float x) => _panelRT.anchoredPosition = new Vector2(x, _panelRT.anchoredPosition.y);

    // ─── Live-обновление строк ───────────────────────────────────────────────
    private void UpdateRow(Text row, LevelConfig.StarTask task, LevelManager lm)
    {
        if (row == null) return;
        switch (task.type)
        {
            case LevelConfig.StarTaskType.CollectAllArtifacts:
            {
                int c = lm.ArtifactsCollected, n = lm.ArtifactsTotal;
                row.text  = $"Ключи: {c}/{n}";
                row.color = (n > 0 && c >= n) ? ColDone : ColNeutral;
                break;
            }
            case LevelConfig.StarTaskType.BeatTime:
            {
                bool ok = lm.ElapsedTime <= task.timeThreshold;
                row.text  = $"Время: {Fmt(lm.ElapsedTime)} / {Fmt(task.timeThreshold)}";
                row.color = ok ? ColDone : ColFail;
                break;
            }
            default: // Complete
                row.text  = "Дойти до финиша";
                row.color = ColNeutral;
                break;
        }
    }

    private static string Fmt(float s)
    {
        int m = (int)(s / 60f), sec = (int)(s % 60f);
        return $"{m:00}:{sec:00}";
    }

    // ─── Helper ──────────────────────────────────────────────────────────────
    private Text MakeText(Transform parent, string name, string content, Vector2 anchor, Vector2 size,
        Vector2 offset, int fontSize, FontStyle style, Color color, TextAnchor align)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = anchor;
        rt.sizeDelta = size;
        rt.anchoredPosition = offset;
        var t = go.AddComponent<Text>();
        t.text          = content;
        t.font          = UIFont;
        t.fontSize      = fontSize;
        t.fontStyle     = style;
        t.alignment     = align;
        t.color         = color;
        t.raycastTarget = false;
        return t;
    }
}
