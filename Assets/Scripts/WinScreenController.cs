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

        StartCoroutine(SlideIn());
        // Запускаем конфетти после того как панель доедет (~0.95s)
        StartCoroutine(DelayedWinVFX());
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
