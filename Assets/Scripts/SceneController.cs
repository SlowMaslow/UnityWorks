using System;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Управляет переходами между сценами.
/// Все игровые уровни загружаются через единую GameScene (buildIndex 1).
/// LevelLoader.PendingLevel задаёт какой prefab инстанциировать.
/// </summary>
public class SceneController : MonoBehaviour
{
    private const int MENU_INDEX  = 0;
    private const int GAME_INDEX  = 1; // GameScene — единственная игровая сцена

    // ─── Текущий уровень ─────────────────────────────────────────────────────
    /// <summary>Индекс текущего уровня (1-based). В меню возвращает 0.</summary>
    public int GetCurrentScene()
        => SceneManager.GetActiveScene().buildIndex == MENU_INDEX
            ? 0
            : LevelLoader.PendingLevel;

    // ─── Навигация ───────────────────────────────────────────────────────────
    public void LoadMainMenu()
        => LoadScene(MENU_INDEX);

    /// <summary>Перезапустить текущий уровень. Перед перезапуском — interstitial (с учётом кэпа).</summary>
    public void ReloadCurrentScene()
    {
        int lvl = LevelLoader.PendingLevel;
        ShowAdThen(() => LoadGameScene(lvl));
    }

    /// <summary>Алиас для кнопок Inspector.</summary>
    public void LoadCurrentScene()
        => ReloadCurrentScene();

    /// <summary>Загрузить следующий уровень. Если уровней нет — вернуться к первому.</summary>
    public void LoadNextScene()
    {
        int next   = LevelLoader.PendingLevel + 1;
        int total  = LevelLoader.TotalLevels;
        int target = next <= total ? next : 1;
        ShowAdThen(() => LoadGameScene(target));
    }

    /// <summary>Показать interstitial (если положен по кэпу), затем выполнить переход.</summary>
    private void ShowAdThen(Action load)
    {
        if (AdsManager.Instance != null) AdsManager.Instance.NotifyLevelEnded(load);
        else load();
    }

    /// <summary>Загрузить последний открытый уровень (кнопка Play в меню).</summary>
    public void LoadLastLevel()
        => LoadGameScene(SaveSystem.LastLevel);

    // ─── Обратная совместимость с Inspector-кнопками ─────────────────────────
    public void SaveLastLevel()
    {
        int current = GetCurrentScene();
        if (current > SaveSystem.LastLevel) SaveSystem.LastLevel = current;
    }

    public void SaveLastLevelOnNext()
        => SaveSystem.UnlockNextLevel(GetCurrentScene(), LevelLoader.TotalLevels);

    // ─── Private ─────────────────────────────────────────────────────────────
    private void LoadGameScene(int levelIndex)
    {
        LevelLoader.PendingLevel = levelIndex;
        LoadScene(GAME_INDEX);
    }

    private static void LoadScene(int index)
    {
        if (ScreenFader.Instance != null)
            ScreenFader.Instance.FadeToScene(index);
        else
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(index);
        }
    }
}
