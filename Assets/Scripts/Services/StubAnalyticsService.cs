using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Заглушка аналитики — логирует события в консоль. Позволяет проверить воронку
/// до подключения GameAnalytics SDK/ключа. Реальная реализация встанет за тем же интерфейсом.
/// </summary>
public class StubAnalyticsService : IAnalyticsService
{
    public void Initialize() => Debug.Log("[Analytics/Stub] Initialize");

    public void LevelStart(int level)
        => Debug.Log($"[Analytics/Stub] level_start lvl={level}");

    public void LevelComplete(int level, int stars, float time, int coins)
        => Debug.Log($"[Analytics/Stub] level_complete lvl={level} stars={stars} time={time:0.0} coins={coins}");

    public void LevelFail(int level, float time)
        => Debug.Log($"[Analytics/Stub] level_fail lvl={level} time={time:0.0}");

    public void AdShown(string adType, string placement)
        => Debug.Log($"[Analytics/Stub] ad_shown type={adType} placement={placement}");

    public void Purchase(string productId, string price)
        => Debug.Log($"[Analytics/Stub] purchase id={productId} price={price}");

    public void Custom(string eventName, IDictionary<string, object> data = null)
        => Debug.Log($"[Analytics/Stub] custom {eventName}");
}
