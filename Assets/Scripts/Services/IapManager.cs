using System;
using UnityEngine;

/// <summary>
/// Единая точка покупок. Живёт между сценами, создаётся автоматически до первой сцены.
/// В v1: продукт remove_ads — ставит флаг SaveSystem.AdsRemoved и глушит interstitial.
/// Сейчас активен StubIapService (без стор-биллинга) — заменяется на Unity IAP.
/// </summary>
public class IapManager : MonoBehaviour
{
    public static IapManager Instance { get; private set; }

    public const string PRODUCT_REMOVE_ADS = "remove_ads";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        if (Instance != null) return;
        var go = new GameObject("[IapManager]");
        Instance = go.AddComponent<IapManager>();
        DontDestroyOnLoad(go);
    }

    private IIapService _service;

    public bool IsAdsRemoved => SaveSystem.AdsRemoved;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        // TODO: при настройке биллинга заменить на new UnityIapService(...)
        _service = new StubIapService();
        _service.Initialize(() => Debug.Log("[IAP] ready"));
    }

    /// <summary>Покупка «убрать рекламу». onDone(true) при успехе (или если уже куплено).</summary>
    public void BuyRemoveAds(Action<bool> onDone)
    {
        if (SaveSystem.AdsRemoved) { onDone?.Invoke(true); return; }
        _service.Purchase(PRODUCT_REMOVE_ADS, success =>
        {
            if (success)
            {
                SaveSystem.AdsRemoved = true;
                AnalyticsManager.Instance?.Purchase(PRODUCT_REMOVE_ADS, "n/a");
            }
            onDone?.Invoke(success);
        });
    }

    /// <summary>Восстановление покупок (кнопка Restore). onDone(true) если remove-ads восстановлен.</summary>
    public void RestorePurchases(Action<bool> onDone)
    {
        _service.Restore(restored =>
        {
            if (restored) SaveSystem.AdsRemoved = true;
            onDone?.Invoke(restored);
        });
    }
}
