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
    ///
    /// 🐞 Здесь было `next &lt; totalLevels`, и из-за строгого неравенства ПОСЛЕДНИЙ уровень пака не
    /// открывался НИКОГДА: при 10 уровнях прохождение 9-го давало next = 10, а `10 &lt; 10` — ложь.
    /// В окне выбора он так и оставался «LOCKED». Незаметно это было потому, что кнопка «дальше»
    /// (<c>SceneController.LoadNextScene</c>) разблокировку не проверяет — сыграть последний уровень
    /// было можно, а карточка при этом оставалась серой. Индекс уровня 1-based, всего их totalLevels,
    /// значит допустимый максимум — РОВНО totalLevels.
    /// </summary>
    public static void UnlockNextLevel(int completedLevelIndex, int totalLevels)
    {
        int next = completedLevelIndex + 1;
        if (next <= totalLevels && next > LastLevel)
        {
            LastLevel = next;
        }
    }

    // ─── Звёзды/задачи по уровням (маска выполненных задач) ──────────────────
    // Храним БИТОВУЮ МАСКУ выполненных задач (бит = (int)LevelConfig.StarTaskType),
    // а не число звёзд. Звёзды = кол-во выполненных задач. Маска нужна, чтобы финалка
    // показывала РЕКОРД (не регрессировала) и для per-task галочек в карточке/сайдбаре.
    public static int GetLevelTaskMask(int levelIndex)
        => PlayerPrefs.GetInt($"TaskMask_{levelIndex}", 0);

    /// <summary>Число звёзд уровня = кол-во когда-либо выполненных задач.</summary>
    public static int GetLevelStars(int levelIndex)
        => CountBits(GetLevelTaskMask(levelIndex));

    /// <summary>
    /// Мержит (OR) маску выполненных в забеге задач в сохранённый рекорд.
    /// Возвращает СТАРУЮ маску (до мержа) — чтобы узнать, какие задачи выполнены ВПЕРВЫЕ.
    /// </summary>
    public static int MergeLevelTaskMask(int levelIndex, int runMask)
    {
        int old    = GetLevelTaskMask(levelIndex);
        int merged = old | runMask;
        if (merged != old)
        {
            PlayerPrefs.SetInt($"TaskMask_{levelIndex}", merged);
            PlayerPrefs.Save();
        }
        return old;
    }

    /// <summary>Кол-во установленных бит (популяция). Утилита для звёзд/масок.</summary>
    public static int CountBits(int v)
    {
        int c = 0;
        while (v != 0) { c += v & 1; v >>= 1; }
        return c;
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

    // ─── Артефакты по уровням (собираются в картинку-мир) ────────────────────
    public static int GetLevelArtifacts(int levelIndex)
        => PlayerPrefs.GetInt($"Artifacts_{levelIndex}", 0);

    /// <summary>Best-kept (как звёзды) — исключает фарм повторным прохождением уровня.</summary>
    public static void TrySetLevelArtifacts(int levelIndex, int count)
    {
        if (count > GetLevelArtifacts(levelIndex))
        {
            PlayerPrefs.SetInt($"Artifacts_{levelIndex}", Mathf.Max(0, count));
            PlayerPrefs.Save();
        }
    }

    /// <summary>Сумма собранных артефактов по диапазону уровней пака (прогресс картинки-мира).</summary>
    public static int GetPackArtifacts(int firstLevelIndex, int lastLevelIndex)
    {
        int sum = 0;
        for (int i = firstLevelIndex; i <= lastLevelIndex; i++)
            sum += GetLevelArtifacts(i);
        return sum;
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
