using System;

namespace ClimbUp.Tutorial
{
    /// <summary>
    /// Никогда не выполняется автоматически — переход только через
    /// TutorialPlayer.AdvanceManually() или autoAdvanceDelay шага.
    /// </summary>
    [Serializable]
    public class ManualAdvanceCondition : TutorialCondition { }
}
