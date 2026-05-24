using System;
using UnityEngine;

namespace ClimbUp.Tutorial
{
    [Serializable]
    public class StepHighlight
    {
        [Tooltip("ID цели (резолвится через TutorialTarget)")]
        public string targetId;
        [Tooltip("Цвет заливки и кольца")]
        public Color  color = Color.white;
    }

    /// <summary>
    /// Один шаг туториала. Полностью data-driven: текст, условие, подсветки —
    /// настраиваются в Inspector. Логика "когда → что" нигде не зашита.
    /// </summary>
    [Serializable]
    public class TutorialStep
    {
        [Tooltip("Логический id шага. Можно использовать для прыжков (jumpToId на флагах).")]
        public string id;

        [TextArea(2, 4)] public string hintText;
        [TextArea(1, 3)] public string subText;

        [Tooltip("Условие завершения шага. Если null — шаг бесконечный (или используется autoAdvanceDelay).")]
        [SerializeReference, SubclassSelector] public TutorialCondition condition = new ManualAdvanceCondition();

        [Tooltip("Если >0 — шаг автоматически завершится через указанное время unscaled-секунд (вне зависимости от condition).")]
        public float autoAdvanceDelay = 0f;

        [Tooltip("Подсветки, которые показываются пока активен этот шаг")]
        public StepHighlight[] highlights = new StepHighlight[0];

        [Tooltip("Действия (Actions): полиморфный список. Enter — при входе в шаг, Exit — при выходе. " +
                 "Пример: BlockInputAction блокирует игровой ввод и/или UI на время шага.")]
        [SerializeReference, SubclassSelector] public TutorialAction[] actions = new TutorialAction[0];

        [Tooltip("Если задано — при входе в шаг ставит PlayerPrefs.SetInt(flagName, 1)")]
        public string setFlagOnEnter;

        [Tooltip("Если задано — при выполнении condition ставит PlayerPrefs.SetInt(flagName, 1)")]
        public string setFlagOnComplete;

        [Tooltip("Если задано — шаг пропускается если PlayerPrefs.HasKey(flagName)")]
        public string skipIfFlagSet;

        [Tooltip("Если true — после выполнения condition туториал останавливается в текущей сессии " +
                 "(setFlagOnComplete выставится, но Advance не вызывается). Полезно когда дальше " +
                 "ожидается game-restart, и продолжение должно произойти в новой сессии через skipIfFlagSet.")]
        public bool stopAfterComplete;
    }
}
