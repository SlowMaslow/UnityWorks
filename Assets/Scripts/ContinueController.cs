using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

/// <summary>
/// Continue-флоу: на Fail показывает панель «Продолжить?». Кнопка → rewarded-реклама
/// (сейчас стаб AdsManager — имитирует показ и ВСЕГДА выдаёт награду, т.е. проверяемо в Unity без SDK)
/// → оживление игрока с сохранением времени/ключей. 1 раз за попытку; второй Fail → полный сброс.
/// Само-спавнится в GameScene (buildIndex 1). ⚠️ UI черновой (код), см. [[ui-tech-debt-prefabs]].
/// </summary>
public class ContinueController : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.buildIndex != 1) return;
        if (FindFirstObjectByType<ContinueController>() != null) return;
        Canvas target = null;
        foreach (var c in FindObjectsByType<Canvas>(FindObjectsSortMode.None))
        {
            var root = c.rootCanvas;
            if (root == null || root.name == "FadeCanvas") continue;
            if (target == null || root.sortingOrder < target.sortingOrder) target = root;
        }
        if (target == null) return;
        var go = new GameObject("ContinueController", typeof(RectTransform));
        go.transform.SetParent(target.transform, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        go.AddComponent<ContinueController>();
    }

    private static Font UIFont => Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

    private bool       _used;   // continue уже потрачен в этой попытке
    private GameObject _panel;

    private void OnEnable()  => GameManager.OnGameStateChanged += OnState;
    private void OnDisable() => GameManager.OnGameStateChanged -= OnState;

    // Пороги «есть прогресс» — окно continue не суём со старта в лоб (тюнить при желании).
    private const int   MinArtifacts = 1;
    private const int   MinCoins     = 3;
    private const float MinSeconds   = 10f;

    private void OnState(GameState s)
    {
        if (s != GameState.Fail) { HidePanel(); return; }
        // continue уже потрачен ИЛИ прогресса по уровню нет (только начал) → сразу сброс, без окна
        if (_used || !HasProgress()) { Reload(); return; }
        ShowPanel();
    }

    private bool HasProgress()
    {
        var lm = LevelManager.Instance;
        if (lm == null) return false;
        return lm.ArtifactsCollected >= MinArtifacts
            || lm.CoinsThisRun       >= MinCoins
            || lm.ElapsedTime        >= MinSeconds;
    }

    // ─── Панель ──────────────────────────────────────────────────────────────
    private void ShowPanel()
    {
        HidePanel();
        _panel = MakeGO("ContinuePanel", transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        _panel.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.65f); // затемнение (блокирует UI-клики)

        var card = MakeCentered("Card", _panel.transform, new Vector2(480, 320));
        card.AddComponent<Image>().color = new Color(0.14f, 0.10f, 0.06f, 0.98f);

        MakeLabel(card.transform, "ПРОДОЛЖИТЬ?", new Vector2(0.5f, 0.82f), new Vector2(440, 50), 34, FontStyle.Bold, Color.white);
        MakeLabel(card.transform, "Оживить с сохранением времени и ключей", new Vector2(0.5f, 0.60f), new Vector2(440, 40), 19, FontStyle.Normal, new Color(1, 1, 1, 0.8f));
        MakeButton(card.transform, "ПРОДОЛЖИТЬ (реклама)", new Vector2(0.5f, 0.33f), new Vector2(380, 66), new Color(0.30f, 0.66f, 0.20f), OnContinue);
        MakeButton(card.transform, "Сдаться", new Vector2(0.5f, 0.13f), new Vector2(240, 52), new Color(0.55f, 0.20f, 0.15f), Reload);
    }

    private void HidePanel() { if (_panel != null) { Destroy(_panel); _panel = null; } }

    // ─── Действия ────────────────────────────────────────────────────────────
    private void OnContinue()
    {
        HidePanel();
        var ads = AdsManager.Instance;
        if (ads != null) ads.ShowRewarded("continue", granted => { if (granted) DoRevive(); else Reload(); });
        else DoRevive(); // подстраховка (в Unity стаб всегда есть)
    }

    private void DoRevive()
    {
        _used = true;
        var playerGO = GameObject.FindWithTag("Player");
        var climb = playerGO != null ? playerGO.GetComponent<ClimbController>() : null;
        var fader = ScreenFader.Instance;
        Vector3 pos = LevelManager.Instance != null ? LevelManager.Instance.ReviveSpawnPosition
                    : (climb != null ? climb.BodyPosition : Vector3.zero);

        if (fader != null)
        {
            // Телепорт к чекпоинту ПОД затемнение — без некрасивого прыжка.
            fader.FadeThrough(
                atBlack: () =>
                {
                    GameManager.Instance?.SetState(GameState.Playing); // таймер снова идёт
                    if (climb != null) climb.Revive(pos);
                },
                onComplete: () => FindFirstObjectByType<TaskSidebarController>()?.RevealTasks());
        }
        else
        {
            GameManager.Instance?.SetState(GameState.Playing);
            if (climb != null) climb.Revive(pos);
            FindFirstObjectByType<TaskSidebarController>()?.RevealTasks();
        }
    }

    private void Reload()
    {
        HidePanel();
        FindFirstObjectByType<SceneController>()?.ReloadCurrentScene();
    }

    // ─── Helpers (черновой UI кодом) ─────────────────────────────────────────
    private GameObject MakeGO(string name, Transform parent, Vector2 aMin, Vector2 aMax, Vector2 oMin, Vector2 oMax)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = aMin; rt.anchorMax = aMax; rt.offsetMin = oMin; rt.offsetMax = oMax;
        return go;
    }

    private GameObject MakeCentered(string name, Transform parent, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero; rt.sizeDelta = size;
        return go;
    }

    private void MakeLabel(Transform parent, string text, Vector2 anchor, Vector2 size, int fs, FontStyle style, Color col)
    {
        var go = new GameObject("Label", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = anchor; rt.anchoredPosition = Vector2.zero; rt.sizeDelta = size;
        var t = go.AddComponent<Text>();
        t.text = text; t.font = UIFont; t.fontSize = fs; t.fontStyle = style;
        t.alignment = TextAnchor.MiddleCenter; t.color = col; t.raycastTarget = false;
    }

    private void MakeButton(Transform parent, string label, Vector2 anchor, Vector2 size, Color col, System.Action onClick)
    {
        var go = new GameObject("Btn", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = anchor; rt.anchoredPosition = Vector2.zero; rt.sizeDelta = size;
        var img = go.AddComponent<Image>(); img.color = col;
        var btn = go.AddComponent<Button>(); btn.targetGraphic = img;
        if (onClick != null) btn.onClick.AddListener(() => onClick());
        MakeLabel(go.transform, label, new Vector2(0.5f, 0.5f), size, 24, FontStyle.Bold, Color.white);
    }
}
