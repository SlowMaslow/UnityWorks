using System.Collections.Generic;

/// <summary>
/// Абстракция аналитики. Реализация (GameAnalytics / стаб) прячется за интерфейсом.
/// Выбран GameAnalytics — кросс-платформа Android/iOS/WebGL.
/// </summary>
public interface IAnalyticsService
{
    void Initialize();

    void LevelStart(int level);
    void LevelComplete(int level, int stars, float time, int coins);
    void LevelFail(int level, float time);

    void AdShown(string adType, string placement);
    void Purchase(string productId, string price);

    void Custom(string eventName, IDictionary<string, object> data = null);
}
