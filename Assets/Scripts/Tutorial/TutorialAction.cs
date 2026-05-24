using System;

namespace ClimbUp.Tutorial
{
    /// <summary>
    /// Базовое действие туториала. Хранится по [SerializeReference] внутри TutorialStep.
    /// Actions запускаются ПОСЛЕДОВАТЕЛЬНО: следующий стартует только когда предыдущий
    /// сообщил IsDone=true. Большинство мгновенные (CameraFocus, BlockInput) — они
    /// просто настраивают side-effect и сразу готовы. DelayAction блокирует очередь
    /// на N секунд. Exit() для ВСЕХ запущенных вызывается при выходе из шага.
    /// </summary>
    [Serializable]
    public abstract class TutorialAction
    {
        /// <summary>Вызывается один раз когда action становится активным.</summary>
        public virtual void Enter() { }

        /// <summary>Тикает каждый кадр пока IsDone == false.</summary>
        public virtual void Tick(float unscaledDeltaTime) { }

        /// <summary>true — action завершён, очередь может перейти к следующему.</summary>
        public virtual bool IsDone => true;

        /// <summary>Вызывается при выходе из шага (если Enter был вызван).</summary>
        public virtual void Exit() { }
    }
}
