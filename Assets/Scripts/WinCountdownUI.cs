using UnityEngine;
using UnityEngine.UI;
using System.Collections;

/// <summary>
/// Отображает обратный отсчёт 3-2-1 в центре экрана.
/// Объект ВСЕГДА активен — скрываем только дочерние элементы.
/// Подписка в Awake/OnDestroy, не в OnEnable/OnDisable.
/// </summary>
public class WinCountdownUI : MonoBehaviour
{
    [SerializeField] private Text numberText;
    [SerializeField] private Text labelText;

    private static readonly Color ColorThree = new Color(1f, 0.85f, 0.1f);
    private static readonly Color ColorTwo   = new Color(1f, 0.55f, 0.1f);
    private static readonly Color ColorOne   = new Color(1f, 0.25f, 0.25f);

    // ─── Unity ───────────────────────────────────────────────────────────────
    private void Awake()
    {
        WinScript.OnCountdownTick     += ShowNumber;
        WinScript.OnCountdownCancel   += HideContent;
        LevelManager.OnLevelCompleted += OnLevelCompleted;
        SetVisible(false);
    }

    private void OnDestroy()
    {
        WinScript.OnCountdownTick     -= ShowNumber;
        WinScript.OnCountdownCancel   -= HideContent;
        LevelManager.OnLevelCompleted -= OnLevelCompleted;
    }

    private void OnLevelCompleted(LevelResult _)
    {
        StopAllCoroutines();
        StartCoroutine(FadeOut());
    }

    private IEnumerator FadeOut()
    {
        float duration  = 0.25f;
        float elapsed   = 0f;
        var   numCol    = numberText != null ? numberText.color : Color.white;
        var   labelCol  = labelText  != null ? labelText.color  : Color.white;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float a  = Mathf.Lerp(1f, 0f, elapsed / duration);
            float s  = Mathf.Lerp(1f, 0.4f, elapsed / duration);
            if (numberText != null) numberText.color = new Color(numCol.r,   numCol.g,   numCol.b,   a);
            if (labelText  != null) labelText.color  = new Color(labelCol.r, labelCol.g, labelCol.b, a);
            transform.localScale = Vector3.one * s;
            yield return null;
        }

        SetVisible(false);
        transform.localScale = Vector3.one;
    }

    // ─── Events ──────────────────────────────────────────────────────────────
    private void ShowNumber(int num)
    {
        SetVisible(true);

        if (numberText != null)
        {
            numberText.text  = num.ToString();
            numberText.color = num == 3 ? ColorThree : num == 2 ? ColorTwo : ColorOne;
        }

        StopAllCoroutines();
        StartCoroutine(PopAnimation());
    }

    private void HideContent()
    {
        StopAllCoroutines();
        SetVisible(false);
    }

    // ─── Helpers ─────────────────────────────────────────────────────────────
    private void SetVisible(bool visible)
    {
        if (numberText != null) numberText.enabled = visible;
        if (labelText  != null) labelText.enabled  = visible;
    }

    private IEnumerator PopAnimation()
    {
        float duration = 0.25f;
        float elapsed  = 0f;
        transform.localScale = Vector3.one * 1.6f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = elapsed / duration;
            float s = Mathf.Lerp(1.6f, 1f, t * t);
            transform.localScale = Vector3.one * s;
            yield return null;
        }

        transform.localScale = Vector3.one;
    }
}
