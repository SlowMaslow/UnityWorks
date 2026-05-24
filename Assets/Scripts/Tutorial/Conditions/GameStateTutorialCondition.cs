using System;

namespace ClimbUp.Tutorial
{
    /// <summary>
    /// Завершается когда GameManager переходит в указанное состояние.
    /// </summary>
    [Serializable]
    public class GameStateTutorialCondition : TutorialCondition
    {
        public GameState targetState;

        public override void Enter()
        {
            base.Enter();
            GameManager.OnGameStateChanged += Handle;
        }

        public override void Exit()
        {
            GameManager.OnGameStateChanged -= Handle;
            base.Exit();
        }

        private void Handle(GameState s)
        {
            if (s == targetState) Complete();
        }
    }
}
