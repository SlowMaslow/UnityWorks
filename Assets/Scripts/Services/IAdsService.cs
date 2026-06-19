using System;

/// <summary>
/// Абстракция рекламной сети. Конкретная реализация (AppLovin MAX / стаб) прячется за этим
/// интерфейсом — игра вызывает только AdsManager, сеть заменяется без правок геймплея.
/// </summary>
public interface IAdsService
{
    void Initialize(Action onInitialized);

    bool IsInterstitialReady { get; }
    bool IsRewardedReady { get; }

    void LoadInterstitial();
    void LoadRewarded();

    /// <summary>Показ полноэкранной рекламы. onClosed вызывается после закрытия.</summary>
    void ShowInterstitial(string placement, Action onClosed);

    /// <summary>Показ rewarded. onRewarded(true) — досмотрел до конца и заслужил награду.</summary>
    void ShowRewarded(string placement, Action<bool> onRewarded);
}
