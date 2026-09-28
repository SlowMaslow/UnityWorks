using UnityEngine;
using UnityEngine.UI;
using System.Collections;

/// <summary>
/// Анимированный экран окончания уровня.
/// Панель выезжает снизу после завершения уровня.
/// Показывает звёзды, монеты за этот запуск и время.
/// </summary>
public class WinScreenController : MonoBehaviour
{
    [Header("Панель (анимируется)")]
    [SerializeField] private RectTransform panelRT;

    [Header("Звёзды")]
    [SerializeField] private Text[] starTexts; // 3 элемента

    [Header("Статистика")]
    [SerializeField] private Text coinsText;
    [SerializeField] private Text timeText;

    [Header("Кнопки")]
    [SerializeField] private Button nextLevelBtn;
    [SerializeField] private Button menuBtn;

    private static readonly Color StarFilled = new Color(1f, 0.82f, 0.05f);
    private static readonly Color StarEmpty  = new Color(0.7f, 0.7f, 0.7f, 0.35f);

    private Vector2 _shownPos;
    private Vector2 _hiddenPos;

    // ─── Unity ───────────────────────────────────────────────────────────────
    private void Awake()
    {
        LevelManager.OnLevelCompleted += Show;

        // Кнопки — находим SceneController через GetComponentInParent или в сцене
        var sc = FindFirstObjectByType<SceneController>();
        nextLevelBtn?.onClick.AddListener(() => sc?.LoadNextScene());
        menuBtn?.onClick.AddListener(() => sc?.LoadMainMenu());
    }

    private void Start()
    {
        if (panelRT == null) return;
        _shownPos  = panelRT.anchoredPosition;
        _hiddenPos = new Vector2(_shownPos.x, _shownPos.y - 900f);
        panelRT.anchoredPosition = _hiddenPos;
    }

    private void OnDestroy()
    {
        LevelManager.OnLevelCompleted -= Show;
    }

    // ─── Show ────────────────────────────────────────────────────────────────
    private void Show(LevelResult result)
    {
        // Звёзды: заполняем по РЕКОРДУ (result.stars), НЕ по текущему заходу → нет регресса
        int newCount = SaveSystem.CountBits(result.newTasksMask);
        int firstNew = result.stars - newCount; // [firstNew, result.stars) — впервые в этом заходе

        for (int i = 0; i < starTexts.Length; i++)
        {
            if (starTexts[i] == null) continue;
            starTexts[i].text  = "★";
            starTexts[i].color = i < result.stars ? StarFilled : StarEmpty;
            starTexts[i].transform.localScale = Vector3.one;
        }

        // Подсветка ВПЕРВЫЕ заработанных в этом заходе звёзд (pop-анимация)
        if (newCount > 0) StartCoroutine(PopNewStars(firstNew, result.stars));

        // Монеты
        if (coinsText != null)
            coinsText.text = result.coinsCollected > 0 ? $"+{result.coinsCollected}" : "0";

        // Время
        if (timeText != null)
        {
            int m = (int)(result.time / 60f);
            int s = (int)(result.time % 60f);
            timeText.text = $"{m:00}:{s:00}";
        }

        ShowArtifacts(result);

        StartCoroutine(SlideIn());
        // Запускаем конфетти после того как панель доедет (~0.95s)
        StartCoroutine(DelayedWinVFX());
    }

    // ─── Артефакты: строка «ключи X/N» и «+N в коллекцию» ────────────────────
    // ⚠️ СТРОКА СТРОИТСЯ КОДОМ, А НЕ ПОЛЕМ В ИНСПЕКТОРЕ. Панель финалки собрана в сцене, и новое
    // поле пришлось бы вешать руками в двух местах (сцена + префаб). Весь остальной новый UI проекта
    // (сайдбар, continue, карточка уровня) тоже рисуется кодом — см. память ui-tech-debt-prefabs,
    // там же план вынести всё это в префабы отдельной фазой. Держимся того же чернового курса.
    // Кириллица: Bangers её не содержит, поэтому LegacyRuntime.
    private static readonly Color ColKey     = new Color(0.25f, 0.90f, 0.85f);
    private static readonly Color ColKeyDim  = new Color(1f, 1f, 1f, 0.55f);
    private static readonly Color ColSecret  = new Color(1f, 0.85f, 0.25f);

    private Text _artifactText;

    private void ShowArtifacts(LevelResult result)
    {
        if (panelRT == null) return;
        if (result.artifactsTotal <= 0 && result.packTotal <= 0) return;

        if (_artifactText == null)
        {
            var go = new GameObject("ArtifactLine", typeof(RectTransform));
            go.transform.SetParent(panelRT, false);
            var rt = go.GetComponent<RectTransform>();
            // Под статистикой, у нижнего края панели — выше кнопок не лезем.
            rt.anchorMin = new Vector2(0.5f, 0f);
            rt.anchorMax = new Vector2(0.5f, 0f);
            rt.pivot     = new Vector2(0.5f, 0f);
            rt.anchoredPosition = new Vector2(0f, 118f);
            rt.sizeDelta        = new Vector2(460f, 34f);

            _artifactText = go.AddComponent<Text>();
            _artifactText.font      = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _artifactText.fontSize  = 22;
            _artifactText.alignment = TextAnchor.MiddleCenter;
            _artifactText.raycastTarget = false;         // не перехватывать клики по кнопкам панели
            // 🐞 Строка ОБРЕЗАЛАСЬ: «КАРТИНКА СОБРАНА — СЕКРЕТНЫЙ ПАК|ОТКРЫТ» — у баннера текст длиннее
            // обычной строки, а ширина панели фиксированная. Подгонка кегля дешевле, чем подбирать
            // формулировки под ширину: длинные варианты просто ужимаются.
            _artifactText.resizeTextForBestFit = true;
            _artifactText.resizeTextMinSize    = 12;
            _artifactText.resizeTextMaxSize    = 22;
        }

        string line = $"Ключи {result.artifactsCollected}/{result.artifactsTotal}";
        if (result.artifactsBanked > 0)
        {
            // ⭐ Главное звено петли: забег видно В КОЛЛЕКЦИИ, а не только на своём уровне.
            line += $"   +{result.artifactsBanked} в коллекцию  ({result.packCollected}/{result.packTotal})";
            _artifactText.color = ColKey;
        }
        else
        {
            // Повтор уже собранного: честно говорим, что картинке это ничего не добавило.
            line += $"   в коллекции {result.packCollected}/{result.packTotal}";
            _artifactText.color = ColKeyDim;
        }
        _artifactText.text = line;

        if (result.secretPackJustUnlocked)
        {
            _artifactText.text  = "КАРТИНКА СОБРАНА — СЕКРЕТНЫЙ ПАК ОТКРЫТ";
            _artifactText.color = ColSecret;
        }
    }

    private IEnumerator DelayedWinVFX()
    {
        yield return new WaitForSecondsRealtime(0.5f);
        VFXManager.Instance?.PlayWinVFX();
    }

    private IEnumerator PopNewStars(int fromIndex, int toIndex)
    {
        yield return new WaitForSecondsRealtime(1.0f); // ждём, пока панель выедет
        for (int i = fromIndex; i < toIndex && i < starTexts.Length; i++)
        {
            if (i < 0 || starTexts[i] == null) continue;
            var   tr  = starTexts[i].transform;
            float dur = 0.35f, el = 0f;
            while (el < dur)
            {
                el += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(el / dur);
                tr.localScale = Vector3.one * (1f + 0.6f * Mathf.Sin(t * Mathf.PI));
                yield return null;
            }
            tr.localScale = Vector3.one;
        }
    }

    private IEnumerator SlideIn()
    {
        if (panelRT == null) yield break;

        // Небольшая задержка — ждём пока 3-2-1 анимация скроется
        yield return new WaitForSecondsRealtime(0.4f);

        panelRT.anchoredPosition = _hiddenPos;

        float duration = 0.55f;
        float elapsed  = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t  = elapsed / duration;
            // ease-out cubic
            float et = 1f - Mathf.Pow(1f - t, 3f);
            panelRT.anchoredPosition = Vector2.Lerp(_hiddenPos, _shownPos, et);
            yield return null;
        }

        panelRT.anchoredPosition = _shownPos;
    }
}
