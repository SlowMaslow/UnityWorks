using System;

namespace ClimbUp.Tutorial
{
    /// <summary>
    /// Завершается через <see cref="seconds"/> unscaled-секунд после Enter.
    /// Полезно для информационных шагов с авто-переходом.
    /// </summary>
    [Serializable]
    public class TimerTutorialCondition : TutorialCondition
    {
        public float seconds = 2f;

        private float _elapsed;

        public override void Enter()
        {
            base.Enter();
            _elapsed = 0f;
        }

        public override void Tick(float unscaledDeltaTime)
        {
            if (IsCompleted) return;
            _elapsed += unscaledDeltaTime;
            if (_elapsed >= seconds) Complete();
        }

        public override void Reset()
        {
            base.Reset();
            _elapsed = 0f;
        }
    }
}
