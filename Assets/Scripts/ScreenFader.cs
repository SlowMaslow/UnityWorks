using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using System.Collections;

/// <summary>
/// Управляет затемнением/осветлением экрана при переходах между сценами.
/// Присутствует в каждой сцене. При старте — плавно осветляет.
/// При загрузке — плавно затемняет, затем грузит сцену.
/// </summary>
public class ScreenFader : MonoBehaviour
{
    public static ScreenFader Instance { get; private set; }

    [SerializeField] private Image fadeImage;
    [SerializeField] private float fadeDuration = 0.35f;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        // Создаём свежий FadeCanvas + Image — без зависимости от сцены
        {
            var fadeCanvasGO        = new GameObject("FadeCanvas");
            var fadeCanvas          = fadeCanvasGO.AddComponent<Canvas>();
            fadeCanvas.renderMode   = RenderMode.ScreenSpaceOverlay;
            fadeCanvas.sortingOrder = 9999;

            var imgGO  = new GameObject("Fade", typeof(RectTransform));
            imgGO.transform.SetParent(fadeCanvasGO.transform, false);
            var rt     = imgGO.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.sizeDelta = Vector2.zero;
            rt.anchoredPosition = Vector2.zero;

            fadeImage = imgGO.AddComponent<Image>();
            fadeImage.color = Color.black;
            fadeImage.raycastTarget = false;

            // Сразу чёрный до первого рендера
            fadeImage.gameObject.SetActive(true);
            SetAlpha(1f);
        }
    }

    private void Start()
    {
        StartCoroutine(FadeIn());
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // ─── Public API ──────────────────────────────────────────────────────────
    /// <summary>Затемнить и загрузить сцену.</summary>
    public void FadeToScene(int sceneIndex)
    {
        StartCoroutine(FadeOutAndLoad(sceneIndex));
    }

    // ─── Coroutines ──────────────────────────────────────────────────────────
    private IEnumerator FadeIn()
    {
        if (fadeImage == null) yield break;
        fadeImage.gameObject.SetActive(true);
        SetAlpha(1f);

        // Пропускаем несколько кадров: первый кадр после загрузки сцены
        // имеет огромный deltaTime (= время загрузки), что ломает анимацию
        yield return null;
        yield return null;

        float elapsed = 0f;
        while (elapsed < fadeDuration)
        {
            // Ограничиваем deltaTime чтобы не было прыжков
            elapsed += Mathf.Min(Time.unscaledDeltaTime, 0.05f);
            SetAlpha(Mathf.Lerp(1f, 0f, Mathf.Clamp01(elapsed / fadeDuration)));
            yield return null;
        }

        SetAlpha(0f);
        fadeImage.gameObject.SetActive(false);
    }

    private IEnumerator FadeOutAndLoad(int sceneIndex)
    {
        if (fadeImage != null)
        {
            fadeImage.gameObject.SetActive(true);
            SetAlpha(0f);

            float elapsed = 0f;
            while (elapsed < fadeDuration)
            {
                elapsed += Mathf.Min(Time.unscaledDeltaTime, 0.05f);
                SetAlpha(Mathf.Lerp(0f, 1f, Mathf.Clamp01(elapsed / fadeDuration)));
                yield return null;
            }
            SetAlpha(1f);
        }

        Time.timeScale = 1f;
        SceneManager.LoadScene(sceneIndex);
    }

    private void SetAlpha(float a)
    {
        if (fadeImage != null)
            fadeImage.color = new Color(0f, 0f, 0f, a);
    }
}
