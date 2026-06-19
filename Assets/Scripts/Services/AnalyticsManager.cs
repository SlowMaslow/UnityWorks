using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Единая точка аналитики. Живёт между сценами, создаётся автоматически до первой сцены.
/// Игра шлёт события через AnalyticsManager.Instance, не зная о конкретном SDK.
/// Сейчас активен StubAnalyticsService (лог в консоль) — заменяется на GameAnalytics.
/// </summary>
public class AnalyticsManager : MonoBehaviour
{
    public static AnalyticsManager Instance { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        if (Instance != null) return;
        var go = new GameObject("[AnalyticsManager]");
        Instance = go.AddComponent<AnalyticsManager>();
        DontDestroyOnLoad(go);
    }

    private IAnalyticsService _service;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        // TODO: при наличии game key заменить на new GameAnalyticsService(...)
        _service = new StubAnalyticsService();
        _service.Initialize();
    }

    // ─── Удобный API для геймплея ────────────────────────────────────────────
    public void LevelStart(int level) => _service?.LevelStart(level);

    public void LevelComplete(LevelResult r)
        => _service?.LevelComplete(r.levelIndex, r.stars, r.time, r.coinsCollected);

    public void LevelFail(int level)
        => _service?.LevelFail(level, LevelManager.Instance != null ? LevelManager.Instance.ElapsedTime : 0f);

    public void AdShown(string adType, string placement) => _service?.AdShown(adType, placement);

    public void Purchase(string productId, string price) => _service?.Purchase(productId, price);

    public void Custom(string eventName, IDictionary<string, object> data = null)
        => _service?.Custom(eventName, data);
}
