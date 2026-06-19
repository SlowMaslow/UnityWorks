using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Заглушка рекламы для разработки без SDK/ключей. Симулирует загрузку и показ:
/// interstitial «показывается» с короткой реальной паузой, rewarded всегда выдаёт награду.
/// Реальная реализация (AppLovinMaxAdsService) встанет на её место за тем же IAdsService.
/// </summary>
public class StubAdsService : IAdsService
{
    private readonly MonoBehaviour _host; // для запуска корутин-имитаций

    public bool IsInterstitialReady { get; private set; }
    public bool IsRewardedReady { get; private set; }

    public StubAdsService(MonoBehaviour host) { _host = host; }

    public void Initialize(Action onInitialized)
    {
        Debug.Log("[Ads/Stub] Initialize");
        onInitialized?.Invoke();
    }

    public void LoadInterstitial()
    {
        IsInterstitialReady = true;
        Debug.Log("[Ads/Stub] interstitial loaded");
    }

    public void LoadRewarded()
    {
        IsRewardedReady = true;
        Debug.Log("[Ads/Stub] rewarded loaded");
    }

    public void ShowInterstitial(string placement, Action onClosed)
    {
        IsInterstitialReady = false;
        Debug.Log($"[Ads/Stub] SHOW interstitial @ {placement}");
        _host.StartCoroutine(FakeAd(0.6f, () => onClosed?.Invoke()));
    }

    public void ShowRewarded(string placement, Action<bool> onRewarded)
    {
        IsRewardedReady = false;
        Debug.Log($"[Ads/Stub] SHOW rewarded @ {placement}");
        _host.StartCoroutine(FakeAd(0.6f, () => onRewarded?.Invoke(true)));
    }

    private IEnumerator FakeAd(float seconds, Action done)
    {
        yield return new WaitForSecondsRealtime(seconds);
        done?.Invoke();
    }
}
