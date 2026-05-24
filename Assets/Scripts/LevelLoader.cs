using UnityEngine;

/// <summary>
/// Статический контейнер для передачи индекса уровня между сценами.
/// НЕ сохраняется в PlayerPrefs — живёт только на время сессии.
/// Использование: установить PendingLevel, затем загрузить GameScene.
/// </summary>
public static class LevelLoader
{
    /// <summary>Индекс уровня (1-based) для загрузки при старте GameScene.</summary>
    public static int PendingLevel = 1;

    /// <summary>Путь внутри Resources/ где лежат prefab-уровни.</summary>
    public const string ResourcesPath = "Levels";

    /// <summary>
    /// Имя prefab по индексу уровня. Level 1 → "Level_01", Level 12 → "Level_12".
    /// </summary>
    public static string PrefabName(int index) => $"Level_{index:D2}";

    /// <summary>Количество доступных уровней (по числу prefab в Resources/Levels).</summary>
    public static int TotalLevels
        => Resources.LoadAll<GameObject>(ResourcesPath).Length;
}
