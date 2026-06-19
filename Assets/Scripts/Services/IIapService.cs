using System;

/// <summary>
/// Абстракция покупок. Реализация (Unity IAP / стаб) прячется за интерфейсом.
/// В v1 единственный продукт — remove_ads (non-consumable).
/// </summary>
public interface IIapService
{
    void Initialize(Action onReady);

    /// <summary>Купить продукт. onComplete(true) при успехе.</summary>
    void Purchase(string productId, Action<bool> onComplete);

    /// <summary>Восстановить покупки (iOS требует обязательно; на Android — no-op/auto).</summary>
    void Restore(Action<bool> onComplete);
}
