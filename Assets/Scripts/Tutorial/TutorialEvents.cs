using System;

namespace ClimbUp.Tutorial
{
    /// <summary>
    /// Глобальная шина событий туториала. Игровой код вызывает <see cref="Raise"/>,
    /// условия (Condition'ы) подписываются и реагируют. Туториальный движок
    /// сам по себе ничего о специфике игры не знает.
    /// </summary>
    public static class TutorialEvents
    {
        /// <summary>Сигнатура: (eventId, payload). Payload может быть null.</summary>
        public static event Action<string, object> OnEvent;

        public static void Raise(string id, object payload = null)
        {
            if (string.IsNullOrEmpty(id)) return;
            OnEvent?.Invoke(id, payload);
        }
    }

    /// <summary>
    /// Стандартные id событий. Не обязательно использовать константы — можно строки,
    /// но IDE-help и защита от опечаток приятнее.
    /// </summary>
    public static class TutorialEventIds
    {
        public const string PadDragStart     = "pad_drag_start";
        public const string PadDragEnd       = "pad_drag_end";
        public const string ZoneEntered      = "zone_entered";
        public const string ZoneExited       = "zone_exited";
        public const string ItemCollected    = "item_collected";
        public const string GameStateChanged = "game_state_changed";
    }
}
