/// <summary>
/// Результат прохождения уровня. Передаётся через LevelManager.OnLevelCompleted.
/// </summary>
public struct LevelResult
{
    public int   levelIndex;
    public int   stars;          // РЕКОРД: кол-во когда-либо выполненных задач (финалка не регрессирует)
    public int   starsThisRun;   // звёзды, заработанные именно в этом заходе
    public int   taskMask;       // рекордная маска выполненных задач (бит = (int)LevelConfig.StarTaskType)
    public int   newTasksMask;   // задачи, ВПЕРВЫЕ выполненные в этом заходе (для подсветки)
    public float time;           // Время прохождения в секундах
    public int   coinsCollected; // Монеты, собранные за этот запуск
}
