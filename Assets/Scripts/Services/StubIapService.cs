using System;
using UnityEngine;

/// <summary>
/// Заглушка покупок для разработки без стор-биллинга. Имитирует успешную покупку/восстановление.
/// Реальная реализация (Unity IAP) встанет за тем же IIapService.
/// </summary>
public class StubIapService : IIapService
{
    public void Initialize(Action onReady)
    {
        Debug.Log("[IAP/Stub] Initialize");
        onReady?.Invoke();
    }

    public void Purchase(string productId, Action<bool> onComplete)
    {
        Debug.Log($"[IAP/Stub] purchase {productId} -> success");
        onComplete?.Invoke(true);
    }

    public void Restore(Action<bool> onComplete)
    {
        Debug.Log("[IAP/Stub] restore -> success");
        onComplete?.Invoke(true);
    }
}
