using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using System.Collections;

/// <summary>
/// ⭐⭐ ЭКРАН КОЛЛЕКЦИИ — ПЕЙОФФ АРТЕФАКТОВ. Здесь замыкается петля, про которую игрок сказал:
/// «ключи собираются, но уходят в никуда».
///
/// Картинка секретного мира закрыта мозаикой плиток-замков; каждый занесённый в коллекцию ключ
/// снимает одну плитку. Прогресс ВИДЕН: не число в статистике, а кусок мира, которого раньше не было.
/// Собрал всю картинку — открылся секретный пак (<see cref="SaveSystem.SecretPackUnlocked"/>).
///
/// Само-спавнится в Menu (buildIndex 0) — тем же приёмом, что <see cref="TaskSidebarController"/> и
/// ContinueController в GameScene. Сцену руками настраивать не нужно.
/// ⚠️ UI построен КОДОМ (черновой) — визуал дизайним позже, см. память ui-tech-debt-prefabs.
/// </summary>
public class CollectionController : MonoBehaviour
{
    // ─── Само-спавн в меню ───────────────────────────────────────────────────
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.buildIndex != 0) return;                                // только Menu
        if (FindFirstObjectByType<CollectionController>() != null) return;

        // Тот же выбор канваса, что у сайдбара: НЕ оверлей затемнения, иначе кнопка не кликается.
        Canvas target = null;
        foreach (var c in FindObjectsByType<Canvas>(FindObjectsSortMode.None))
        {
            var root = c.rootCanvas;
            if (root == null || root.name.Contains("Fade")) continue;
            if (target == null || root.sortingOrder < target.sortingOrder) target = root;
        }
        if (target == null) return;

        var go = new GameObject("Collection", typeof(RectTransform));
        go.transform.SetParent(target.transform, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        go.AddComponent<CollectionController>();
    }

    // ─── Стиль (черновой) ────────────────────────────────────────────────────
    private static Font UIFont => Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
    private static readonly Color ColBtn      = new Color(0.16f, 0.45f, 0.50f);
    private static readonly Color ColDimmer   = new Color(0f, 0f, 0f, 0.72f);
    private static readonly Color ColCard     = new Color(0.10f, 0.13f, 0.14f, 0.99f);
    private static readonly Color ColKey      = new Color(0.35f, 0.92f, 0.88f);
    private static readonly Color ColSecret   = new Color(1f, 0.85f, 0.25f);
    private static readonly Color ColClose    = new Color(0.62f, 0.20f, 0.15f);

    /// <summary>
    /// ⚠️ РАЗМЕР МОЗАИКИ СЧИТАЕТСЯ ОТ ЭКРАНА, А НЕ ЗАДАН ЧИСЛОМ. 🐞 Стояли жёсткие 420, и на
    /// портретном канвасе карточка (420 + 190 на заголовок и подпись) вылезала за верх экрана —
    /// заголовок «СЕКРЕТНЫЙ МИР» и крестик срезало. Берём меньшее из ширины и высоты за вычетом
    /// полей под заголовок/подпись, с потолком, чтобы на планшете не раздувалась.
    /// </summary>
    private const float MosaicMax   = 560f;
    private const float CardPadX    = 60f;    // поля карточки по бокам от мозаики
    private const float CardPadY    = 190f;   // заголовок сверху + строка прогресса снизу

    private GameObject _overlay;
    private Text       _btnProgress;
    private float      _mosaicSize = MosaicMax;

    // ─── Кнопка в меню ───────────────────────────────────────────────────────
    private void Start()
    {
        BuildButton();
    }

    private void OnEnable()
    {
        // Вернулись из уровня — прогресс мог вырасти.
        ArtifactCatalog.Invalidate();
        RefreshButton();
    }

    private void BuildButton()
    {
        var go = new GameObject("CollectionButton", typeof(RectTransform));
        go.transform.SetParent(transform, false);
        var rt = go.GetComponent<RectTransform>();

        // ⚠️ МЕСТО БЕРЁМ У СУЩЕСТВУЮЩЕЙ КНОПКИ, А НЕ ЗАДАЁМ ЧИСЛАМИ. Меню собрано в сцене, его
        // раскладку я не контролирую: жёсткие координаты рано или поздно лягут поверх чужой кнопки.
        // Находим «Skins», копируем её геометрию и встаём РОВНО НА СТРОКУ НИЖЕ с тем же шагом.
        var sibling = FindRect(transform.parent, "Skins");
        if (sibling != null)
        {
            rt.anchorMin = sibling.anchorMin;
            rt.anchorMax = sibling.anchorMax;
            rt.pivot     = sibling.pivot;
            rt.sizeDelta = sibling.sizeDelta;
            rt.anchoredPosition = sibling.anchoredPosition
                                + new Vector2(0f, -(sibling.sizeDelta.y + 28f));
        }
        else
        {
            // Запасной угол: верх справа (слева в меню живут монеты).
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(1f, 1f);
            rt.anchoredPosition = new Vector2(-24f, -24f);
            rt.sizeDelta        = new Vector2(240f, 70f);
        }

        var img = go.AddComponent<Image>();
        img.color = ColBtn;
        var btn = go.AddComponent<Button>();
        btn.targetGraphic = img;
        btn.onClick.AddListener(Open);

        MakeText(go.transform, "Label", "КОЛЛЕКЦИЯ", new Vector2(0.5f, 0.66f),
                 new Vector2(rt.sizeDelta.x - 20f, 40f), 26, FontStyle.Bold, Color.white);

        // ⭐ Тизер прямо на кнопке: сколько ключей осталось до секрета. Дешевле отдельного экрана
        // и попадается на глаза каждый раз, когда игрок возвращается в меню.
        _btnProgress = MakeText(go.transform, "Progress", "", new Vector2(0.5f, 0.26f),
                                new Vector2(rt.sizeDelta.x - 20f, 28f), 18, FontStyle.Normal, ColKey);
        RefreshButton();
    }

    private void RefreshButton()
    {
        if (_btnProgress == null) return;
        int got = ArtifactCatalog.PackCollected, all = ArtifactCatalog.PackTotal;
        _btnProgress.text = SaveSystem.SecretPackUnlocked
            ? "картинка собрана"
            : $"ключи {got}/{all}";
        _btnProgress.color = SaveSystem.SecretPackUnlocked ? ColSecret : ColKey;
    }

    // ─── Экран ───────────────────────────────────────────────────────────────
    private void Open()
    {
        Close();
        ArtifactCatalog.Invalidate();

        var canvas = GetComponentInParent<Canvas>();
        if (canvas == null) return;

        _overlay = new GameObject("CollectionOverlay", typeof(RectTransform));
        _overlay.transform.SetParent(canvas.transform, false);
        var ovRt = _overlay.GetComponent<RectTransform>();
        ovRt.anchorMin = Vector2.zero; ovRt.anchorMax = Vector2.one;
        ovRt.offsetMin = Vector2.zero; ovRt.offsetMax = Vector2.zero;
        var dim = _overlay.AddComponent<Image>();
        dim.color = ColDimmer;
        var dimBtn = _overlay.AddComponent<Button>();
        dimBtn.targetGraphic = dim;
        dimBtn.onClick.AddListener(Close);

        // Сколько места реально есть — в единицах канваса, а не в пикселях экрана.
        var canvasRt = canvas.GetComponent<RectTransform>();
        float availW = canvasRt != null ? canvasRt.rect.width  : MosaicMax + CardPadX;
        float availH = canvasRt != null ? canvasRt.rect.height : MosaicMax + CardPadY;
        _mosaicSize = Mathf.Max(160f, Mathf.Min(MosaicMax, availW - CardPadX - 40f, availH - CardPadY - 40f));

        var card = new GameObject("Card", typeof(RectTransform));
        card.transform.SetParent(_overlay.transform, false);
        var cardRt = card.GetComponent<RectTransform>();
        cardRt.anchorMin = cardRt.anchorMax = cardRt.pivot = new Vector2(0.5f, 0.5f);
        cardRt.anchoredPosition = Vector2.zero;
        cardRt.sizeDelta = new Vector2(_mosaicSize + CardPadX, _mosaicSize + CardPadY);
        card.AddComponent<Image>().color = ColCard;
        card.AddComponent<Button>();                      // глушит клик, чтобы карточка не закрывалась

        // 🐞 Заголовок брал ширину во всю мозаику и залезал под крестик — «СЕКРЕТНЫЙ МИ|X».
        // Оставляем место под кнопку закрытия с обеих сторон и разрешаем шрифту ужиматься:
        // ширина карточки считается от экрана, и одним кеглем все размеры не покрыть.
        MakeText(card.transform, "Title", "СЕКРЕТНЫЙ МИР", new Vector2(0.5f, 0.955f),
                 new Vector2(_mosaicSize - 100f, 44f), 30, FontStyle.Bold, Color.white, true);

        BuildMosaic(card.transform);

        int got = ArtifactCatalog.PackCollected, all = ArtifactCatalog.PackTotal;
        string line = SaveSystem.SecretPackUnlocked
            ? "Картинка собрана — секретный пак открыт"
            : $"Ключей {got} из {all}   ·   осталось {Mathf.Max(0, all - got)}";
        MakeText(card.transform, "Progress", line, new Vector2(0.5f, 0.075f),
                 new Vector2(_mosaicSize + 20f, 34f), 20, FontStyle.Normal,
                 SaveSystem.SecretPackUnlocked ? ColSecret : ColKey);

        MakeButton(card.transform, "Close", "X", new Vector2(0.93f, 0.955f),
                   new Vector2(44f, 44f), ColClose, Close);
    }

    private void Close()
    {
        if (_overlay != null) { Destroy(_overlay); _overlay = null; }
        RefreshButton();
    }

    // ─── Мозаика ─────────────────────────────────────────────────────────────
    /// <summary>
    /// Картинка мира, поверх неё сетка плиток-замков. Снято плиток = ключей в коллекции.
    ///
    /// ⚠️⚠️ ПОРЯДОК ВСКРЫТИЯ ДЕТЕРМИНИРОВАН И РАЗБРОСАН. Оба свойства обязательны:
    /// • детерминирован — иначе при каждом открытии экрана вскрытые куски прыгали бы по картинке,
    ///   и «прогресс» читался бы как мерцание. Берём перестановку от ФИКСИРОВАННОГО сида;
    /// • разбросан — при вскрытии слева направо картинка открывалась бы полосой, и до последнего
    ///   ключа не было бы понятно, что там нарисовано. Разброс показывает мир целиком и сразу.
    /// </summary>
    private void BuildMosaic(Transform parent)
    {
        var db = CollectionDatabase.Instance;

        var holder = new GameObject("Mosaic", typeof(RectTransform));
        holder.transform.SetParent(parent, false);
        var hRt = holder.GetComponent<RectTransform>();
        hRt.anchorMin = hRt.anchorMax = hRt.pivot = new Vector2(0.5f, 0.5f);
        hRt.anchoredPosition = new Vector2(0f, 24f);
        hRt.sizeDelta = new Vector2(_mosaicSize, _mosaicSize);

        var pic = holder.AddComponent<Image>();
        if (db != null && db.packImage != null) pic.sprite = db.packImage;
        else pic.color = new Color(0.2f, 0.25f, 0.28f);   // реестра нет — хотя бы не пустая дыра

        int cols = db != null ? Mathf.Max(1, db.columns) : 6;
        int rows = db != null ? Mathf.Max(1, db.rows)    : 6;
        int pieces = cols * rows;

        // Сколько плиток снято. ⚠️ Ключей может быть больше клеток (пак вырос) — тогда снимаем всё.
        int total = Mathf.Max(1, ArtifactCatalog.PackTotal);
        int open  = SaveSystem.SecretPackUnlocked
            ? pieces
            : Mathf.Clamp(Mathf.RoundToInt(ArtifactCatalog.PackCollected * pieces / (float)total), 0, pieces);

        var order = RevealOrder(pieces);
        float w = _mosaicSize / cols, h = _mosaicSize / rows;

        for (int i = 0; i < pieces; i++)
        {
            if (order[i] < open) continue;                // этот кусок уже вскрыт — плитку не кладём

            int cx = i % cols, cy = i / cols;
            var tile = new GameObject($"Lock_{i}", typeof(RectTransform));
            tile.transform.SetParent(holder.transform, false);
            var tRt = tile.GetComponent<RectTransform>();
            tRt.anchorMin = tRt.anchorMax = new Vector2(0f, 1f);
            tRt.pivot     = new Vector2(0f, 1f);
            tRt.sizeDelta = new Vector2(w, h);
            tRt.anchoredPosition = new Vector2(cx * w, -cy * h);

            // ⚠️⚠️ ПОД ЗАМКОМ — ГЛУХАЯ ПОДЛОЖКА, И ЭТО НЕ УКРАШЕНИЕ.
            // 🐞 Сперва плитка была одним Image со спрайтом замка, и у того по краям прозрачные
            // поля (это скруглённая пластина, а не полный квадрат). Между замками зияли щели, сквозь
            // которые картинка читалась ЦЕЛИКОМ ещё до сбора — вскрывать было уже нечего, и весь
            // смысл «видимого прогресса» пропадал. Подложка закрывает клетку полностью, замок лежит
            // на ней значком.
            var back = tile.AddComponent<Image>();
            back.color = new Color(0.07f, 0.10f, 0.12f, 1f);

            var icon = new GameObject("Icon", typeof(RectTransform));
            icon.transform.SetParent(tile.transform, false);
            var iRt = icon.GetComponent<RectTransform>();
            iRt.anchorMin = Vector2.zero; iRt.anchorMax = Vector2.one;
            // Небольшой отступ, чтобы замки не сливались в сплошное полотно и читались поштучно.
            iRt.offsetMin = new Vector2(w * 0.06f, h * 0.06f);
            iRt.offsetMax = new Vector2(-w * 0.06f, -h * 0.06f);
            var ic = icon.AddComponent<Image>();
            ic.raycastTarget = false;
            if (db != null && db.lockTile != null) ic.sprite = db.lockTile;
            else ic.color = new Color(0.20f, 0.26f, 0.30f);
        }
    }

    /// <summary>
    /// Перестановка 0..n−1 от фиксированного сида: какой по счёту ключ вскрывает какую клетку.
    /// Сид константный — порядок обязан быть один и тот же во всех запусках игры.
    /// </summary>
    private static int[] RevealOrder(int n)
    {
        var a = new int[n];
        for (int i = 0; i < n; i++) a[i] = i;
        var rng = new System.Random(20260913);
        for (int i = n - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            int t = a[i]; a[i] = a[j]; a[j] = t;
        }
        return a;
    }

    // ─── Мелкие построители ──────────────────────────────────────────────────
    private static RectTransform FindRect(Transform root, string name)
    {
        if (root == null) return null;
        foreach (var rt in root.GetComponentsInChildren<RectTransform>(true))
            if (rt.name == name) return rt;
        return null;
    }

    private static Text MakeText(Transform parent, string name, string text, Vector2 anchor,
                                 Vector2 size, int fontSize, FontStyle style, Color color,
                                 bool bestFit = false)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = anchor;
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = size;

        var t = go.AddComponent<Text>();
        t.text      = text;
        t.font      = UIFont;
        t.fontSize  = fontSize;
        t.fontStyle = style;
        t.color     = color;
        t.alignment = TextAnchor.MiddleCenter;
        t.raycastTarget = false;                          // текст не должен перехватывать клик кнопки
        if (bestFit)
        {
            t.resizeTextForBestFit = true;
            t.resizeTextMinSize    = 10;
            t.resizeTextMaxSize    = fontSize;
        }
        return t;
    }

    private static void MakeButton(Transform parent, string name, string label, Vector2 anchor,
                                   Vector2 size, Color color, UnityEngine.Events.UnityAction onClick)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = anchor;
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = size;

        var img = go.AddComponent<Image>();
        img.color = color;
        var btn = go.AddComponent<Button>();
        btn.targetGraphic = img;
        btn.onClick.AddListener(onClick);

        MakeText(go.transform, "Label", label, new Vector2(0.5f, 0.5f), size, 22, FontStyle.Bold, Color.white);
    }
}
