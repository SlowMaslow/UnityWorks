/// <summary>
/// Результат прохождения уровня. Передаётся через LevelManager.OnLevelCompleted.
/// </summary>
public struct LevelResult
{
    public int   levelIndex;
    public int   stars;          // 1–3: количество собранных звёзд
    public float time;           // Время прохождения в секундах
    public int   coinsCollected; // Монеты, собранные за этот запуск
}
