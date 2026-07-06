using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using System.Collections;

/// <summary>
/// Отвечает ТОЛЬКО за отображение UI. Не содержит игровой логики.
/// Реагирует на события GameManager и LevelManager через подписки.
/// </summary>
public class UIController : MonoBehaviour
{
    // ─── Inspector ───────────────────────────────────────────────────────────
    [Header("Auto-restart on fail")]
    [SerializeField] private float failRestartDelay = 1.5f; // задержка перед авторестартом

    [Header("HUD")]
    [SerializeField] private Text       CoinsUI;
    [SerializeField] private Text       LevelUI;
    [SerializeField] private Text       TimerUI;     // Опционально: отображение таймера

    [Header("Menu only")]
    [SerializeField] private Text UpgradeUI;

    // ─── Unity ───────────────────────────────────────────────────────────────
    private void OnEnable()
    {
        GameManager.OnGameStateChanged += HandleStateChanged;
        GameManager.OnCoinsChanged     += UpdateCoinsUI;
        LevelManager.OnLevelCompleted  += HandleLevelCompleted;
        LevelManager.OnTimerTick       += UpdateTimerUI;
        LevelManager.OnCoinCollected   += UpdateCoinsUI;
    }

    private void OnDisable()
    {
        GameManager.OnGameStateChanged -= HandleStateChanged;
        GameManager.OnCoinsChanged     -= UpdateCoinsUI;
        LevelManager.OnLevelCompleted  -= HandleLevelCompleted;
        LevelManager.OnTimerTick       -= UpdateTimerUI;
        LevelManager.OnCoinCollected   -= UpdateCoinsUI;
    }

    private void Start()
    {
        // Инициализируем HUD начальными данными
        UpdateCoinsUI(SaveSystem.Coins);

        bool isMenuScene = SceneManager.GetActiveScene().buildIndex == 0;
        if (!isMenuScene)
            SetText(LevelUI,   $"LEVEL: {LevelLoader.PendingLevel}");
        else
            SetText(UpgradeUI, $"COST: {SaveSystem.UpgradeCost}");
    }

    // ─── Обработчики событий ─────────────────────────────────────────────────
    private void HandleStateChanged(GameState state)
    {
        // Fail теперь обрабатывает ContinueController (панель «Продолжить?» → оживление / сброс).
        // Авто-рестарт убран, чтобы не перезагружать сцену пока висит предложение continue.
    }

    private IEnumerator AutoRestart()
    {
        // Даём рагдоллу упасть — пауза для "драматического эффекта"
        yield return new WaitForSecondsRealtime(failRestartDelay);

        var sc = FindFirstObjectByType<SceneController>();
        sc?.ReloadCurrentScene();
    }

    private void HandleLevelCompleted(LevelResult result)
    {
        // Win-экран теперь обрабатывается WinScreenController (с анимацией)
    }

    // ─── Обновление элементов ────────────────────────────────────────────────
    private void UpdateCoinsUI(int coins)
        => SetText(CoinsUI, coins.ToString());

    private void UpdateTimerUI(float seconds)
    {
        int m = (int)(seconds / 60f);
        int s = (int)(seconds % 60f);
        SetText(TimerUI, $"{m:00}:{s:00}");
    }

    public void UpdateUpgradeCostUI()
        => SetText(UpgradeUI, $"COST: {SaveSystem.UpgradeCost}");

    // ─── Вспомогательное ─────────────────────────────────────────────────────
    private static void SetText(Text label, string value)
    {
        if (label != null) label.text = value;
    }
}
