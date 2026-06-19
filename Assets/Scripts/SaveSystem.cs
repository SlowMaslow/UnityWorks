using UnityEngine;

/// <summary>
/// Единая точка доступа к сохранениям. Все скрипты работают с данными только через SaveSystem.
/// Хранит: монеты, прогресс уровней, звёзды, лучшее время, стоимость апгрейда.
/// </summary>
public static class SaveSystem
{
    // ─── Ключи ───────────────────────────────────────────────────────────────
    private const string KEY_COINS         = "Coins";
    private const string KEY_LAST_LEVEL    = "LastLevel";
    private const string KEY_UPGRADE_COST  = "UpgradeCost";
    private const string KEY_FIRST_PLAY    = "FirstPlay";

    // ─── Монеты ──────────────────────────────────────────────────────────────
    public static int Coins
    {
        get => PlayerPrefs.GetInt(KEY_COINS, 0);
        set { PlayerPrefs.SetInt(KEY_COINS, Mathf.Max(0, value)); PlayerPrefs.Save(); }
    }

    // ─── Прогресс уровней ────────────────────────────────────────────────────
    /// <summary>
    /// Последний открытый уровень (buildIndex). Минимум 1 (первый игровой уровень).
    /// </summary>
    public static int LastLevel
    {
        get => PlayerPrefs.GetInt(KEY_LAST_LEVEL, 1);
        set { PlayerPrefs.SetInt(KEY_LAST_LEVEL, Mathf.Max(1, value)); PlayerPrefs.Save(); }
    }

    /// <summary>
    /// Вызывается при победе: открывает следующий уровень, если он ещё не открыт.
    /// </summary>
    public static void UnlockNextLevel(int completedLevelIndex, int totalLevels)
    {
        int next = completedLevelIndex + 1;
        if (next < totalLevels && next > LastLevel)
        {
            LastLevel = next;
        }
    }

    // ─── Звёзды по уровням ───────────────────────────────────────────────────
    public static int GetLevelStars(int levelIndex)
        => PlayerPrefs.GetInt($"Stars_{levelIndex}", 0);

    /// <summary>Сохраняет результат только если он лучше предыдущего.</summary>
    public static void TrySetLevelStars(int levelIndex, int stars)
    {
        if (stars > GetLevelStars(levelIndex))
        {
            PlayerPrefs.SetInt($"Stars_{levelIndex}", Mathf.Clamp(stars, 0, 3));
            PlayerPrefs.Save();
        }
    }

    // ─── Лучшее время ────────────────────────────────────────────────────────
    public static float GetLevelBestTime(int levelIndex)
        => PlayerPrefs.GetFloat($"BestTime_{levelIndex}", float.MaxValue);

    /// <summary>Сохраняет время только если оно лучше предыдущего.</summary>
    public static void TrySetLevelBestTime(int levelIndex, float time)
    {
        if (time < GetLevelBestTime(levelIndex))
        {
            PlayerPrefs.SetFloat($"BestTime_{levelIndex}", time);
            PlayerPrefs.Save();
        }
    }

    // ─── Апгрейды ────────────────────────────────────────────────────────────
    public static int UpgradeCost
    {
        get => PlayerPrefs.GetInt(KEY_UPGRADE_COST, 1);
        set { PlayerPrefs.SetInt(KEY_UPGRADE_COST, Mathf.Max(1, value)); PlayerPrefs.Save(); }
    }

    // ─── Первый запуск ───────────────────────────────────────────────────────
    public static bool IsFirstPlay => !PlayerPrefs.HasKey(KEY_FIRST_PLAY);

    public static void SetFirstPlayDone()
    {
        PlayerPrefs.SetInt(KEY_FIRST_PLAY, 1);
        PlayerPrefs.Save();
    }

    // ─── Tutorial ────────────────────────────────────────────────────────────
    public static bool IsTutorialDone
    {
        get => PlayerPrefs.HasKey("TutorialDone");
        set { if (value) { PlayerPrefs.SetInt("TutorialDone", 1); PlayerPrefs.Save(); } }
    }

    // ─── Скины ───────────────────────────────────────────────────────────────
    private const string KEY_SELECTED_SKIN = "SelectedSkin";

    public static string SelectedSkinId
    {
        get => PlayerPrefs.GetString(KEY_SELECTED_SKIN, "");
        set { PlayerPrefs.SetString(KEY_SELECTED_SKIN, value); PlayerPrefs.Save(); }
    }

    public static bool IsSkinUnlocked(string skinId)
        => PlayerPrefs.GetInt($"SkinUnlocked_{skinId}", 0) == 1;

    public static void UnlockSkin(string skinId)
    {
        PlayerPrefs.SetInt($"SkinUnlocked_{skinId}", 1);
        PlayerPrefs.Save();
    }

    // ─── Реклама (IAP «убрать рекламу») ──────────────────────────────────────
    private const string KEY_ADS_REMOVED = "AdsRemoved";

    public static bool AdsRemoved
    {
        get => PlayerPrefs.GetInt(KEY_ADS_REMOVED, 0) == 1;
        set { PlayerPrefs.SetInt(KEY_ADS_REMOVED, value ? 1 : 0); PlayerPrefs.Save(); }
    }

    // ─── Dev ─────────────────────────────────────────────────────────────────
    /// <summary>Полный сброс прогресса (для тестирования).</summary>
    public static void ResetAll()
    {
        PlayerPrefs.DeleteAll(); // удаляет всё включая TutorialDone
        PlayerPrefs.Save();
    }
}
