using System;

namespace ClimbUp.Tutorial
{
    /// <summary>
    /// Базовое условие туториала. Хранится по [SerializeReference] внутри TutorialStep —
    /// разные подклассы выбираются в Inspector через dropdown.
    /// Условие живёт ровно столько, сколько активен шаг (Enter → Tick* → Exit).
    /// </summary>
    [Serializable]
    public abstract class TutorialCondition
    {
        public event Action OnCompleted;
        public bool IsCompleted { get; private set; }

        /// <summary>Подписываемся на нужные события / стартуем таймер и т.д.</summary>
        public virtual void Enter() { }

        /// <summary>Отписываемся / чистим состояние. Вызывается даже если условие не выполнилось.</summary>
        public virtual void Exit() { }

        /// <summary>Опционально — для тиковых условий (таймеры, расстояния).</summary>
        public virtual void Tick(float unscaledDeltaTime) { }

        protected void Complete()
        {
            if (IsCompleted) return;
            IsCompleted = true;
            OnCompleted?.Invoke();
        }

        /// <summary>Сбрасывает состояние — туториал может проигрываться повторно.</summary>
        public virtual void Reset() => IsCompleted = false;
    }
}
