using System;

namespace ClimbUp.Tutorial
{
    /// <summary>
    /// Задержка перед запуском следующего action в той же цепочке actions[] шага.
    /// На время задержки не блокирует ничего другого — игра/туториал продолжают идти.
    /// Использует unscaled-время, поэтому работает и в паузе.
    /// </summary>
    [Serializable]
    public class DelayAction : TutorialAction
    {
        public float seconds = 1f;

        private float _elapsed;
        private bool  _done;

        public override void Enter()
        {
            _elapsed = 0f;
            _done    = false;
        }

        public override void Tick(float unscaledDeltaTime)
        {
            if (_done) return;
            _elapsed += unscaledDeltaTime;
            if (_elapsed >= seconds) _done = true;
        }

        public override bool IsDone => _done;
    }
}
