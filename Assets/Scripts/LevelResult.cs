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

    // ─── Артефакты: связь ЗАБЕГА с МЕТОЙ ─────────────────────────────────────
    // 🐞 Их тут не было, и финалка показывала только звёзды/время — игрок про ключи сказал
    // «собираются, но уходят в никуда». Забег обязан сам сообщать, что он дал коллекции.
    public int artifactsCollected; // взято в ЭТОМ заходе
    public int artifactsTotal;     // сколько их на уровне
    public int artifactsBanked;    // сколько ВПЕРВЫЕ легло в картинку-мир (рекорд рос на столько)
    public int packCollected;      // всего в коллекции ПОСЛЕ этого забега
    public int packTotal;          // сколько артефактов в паке всего
    public bool secretPackJustUnlocked; // картинка собралась ИМЕННО сейчас
}
