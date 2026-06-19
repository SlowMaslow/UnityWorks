using System;
using UnityEngine;

/// <summary>
/// Единая точка управления рекламой. Живёт между сценами (DontDestroyOnLoad),
/// создаётся автоматически до загрузки первой сцены — настройки в сцене не нужны.
/// Конкретная сеть (AppLovin MAX) спрятана за IAdsService. Сейчас активен StubAdsService
/// (работает без ключей/SDK). Уважает флаг SaveSystem.AdsRemoved (IAP «убрать рекламу»).
/// </summary>
public class AdsManager : MonoBehaviour
{
    public static AdsManager Instance { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        if (Instance != null) return;
        var go = new GameObject("[AdsManager]");
        Instance = go.AddComponent<AdsManager>();
        DontDestroyOnLoad(go);
    }

    // ─── Частотный кэп interstitial ──────────────────────────────────────────
    [Tooltip("Мин. секунд между показами interstitial.")]
    public float interstitialMinInterval = 45f;
    [Tooltip("Мин. число завершений уровня (win/fail) между показами interstitial.")]
    public int interstitialEveryNLevels = 2;

    private IAdsService _service;
    private float _lastInterstitialTime = -9999f;
    private int _levelEndsSinceInterstitial = 0;

    public bool AdsRemoved => SaveSystem.AdsRemoved;
    public bool IsRewardedReady => _service != null && _service.IsRewardedReady;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        // TODO: при наличии ad unit IDs заменить на new AppLovinMaxAdsService(...)
        _service = new StubAdsService(this);
        _service.Initialize(() =>
        {
            _service.LoadInterstitial();
            _service.LoadRewarded();
        });
    }

    /// <summary>
    /// Вызывается при завершении уровня (переход Next/Retry). Показывает interstitial
    /// с учётом частотного кэпа, затем продолжает переход. Если показ не положен — сразу onContinue.
    /// </summary>
    public void NotifyLevelEnded(Action onContinue)
    {
        _levelEndsSinceInterstitial++;
        if (CanShowInterstitial()) ShowInterstitial("level_end", onContinue);
        else onContinue?.Invoke();
    }

    private bool CanShowInterstitial()
    {
        if (AdsRemoved) return false;
        if (_service == null || !_service.IsInterstitialReady) return false;
        if (Time.realtimeSinceStartup - _lastInterstitialTime < interstitialMinInterval) return false;
        if (_levelEndsSinceInterstitial < interstitialEveryNLevels) return false;
        return true;
    }

    public void ShowInterstitial(string placement, Action onClosed)
    {
        if (!CanShowInterstitial()) { onClosed?.Invoke(); return; }
        _lastInterstitialTime = Time.realtimeSinceStartup;
        _levelEndsSinceInterstitial = 0;
        AnalyticsManager.Instance?.AdShown("interstitial", placement);
        _service.ShowInterstitial(placement, () =>
        {
            _service.LoadInterstitial(); // префетч следующего
            onClosed?.Invoke();
        });
    }

    /// <summary>Показ rewarded. onReward(true) если игрок досмотрел до награды.</summary>
    public void ShowRewarded(string placement, Action<bool> onReward)
    {
        if (_service == null || !_service.IsRewardedReady) { onReward?.Invoke(false); return; }
        AnalyticsManager.Instance?.AdShown("rewarded", placement);
        _service.ShowRewarded(placement, granted =>
        {
            _service.LoadRewarded(); // префетч следующего
            onReward?.Invoke(granted);
        });
    }
}
