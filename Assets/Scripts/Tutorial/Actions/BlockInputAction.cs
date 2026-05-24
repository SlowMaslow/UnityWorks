using System;
using UnityEngine;

namespace ClimbUp.Tutorial
{
    /// <summary>
    /// Блокирует/разблокирует игровой ввод и/или UI на время активного шага.
    /// При Enter блокирует, при Exit разблокирует.
    /// </summary>
    [Serializable]
    public class BlockInputAction : TutorialAction
    {
        [Tooltip("Блокировать игровой ввод (драг пэдов и т.п.)")]
        public bool blockGameInput = true;

        [Tooltip("Блокировать UI (кнопки, тычки по экрану через UI)")]
        public bool blockUI = true;

        public override void Enter()
        {
            if (blockGameInput) TutorialInputGate.SetGameInput(true);
            if (blockUI)        TutorialInputGate.SetUI(true);
        }

        public override void Exit()
        {
            if (blockGameInput) TutorialInputGate.SetGameInput(false);
            if (blockUI)        TutorialInputGate.SetUI(false);
        }
    }
}
