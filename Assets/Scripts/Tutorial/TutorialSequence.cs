using UnityEngine;

namespace ClimbUp.Tutorial
{
    /// <summary>
    /// Последовательность шагов туториала. Один asset = один туториал.
    /// Можно создавать сколько угодно — на разных уровнях, для разных механик.
    /// </summary>
    [CreateAssetMenu(fileName = "TutorialSequence", menuName = "ClimbUp/Tutorial Sequence", order = 0)]
    public class TutorialSequence : ScriptableObject
    {
        [Tooltip("PlayerPrefs-ключ, который ставится в 1 при успешном завершении. " +
                 "Если ключ уже стоит — туториал не запускается. Оставь пустым для всегда-запуска (полезно при отладке).")]
        public string completeFlag = "TutorialDone";

        [Tooltip("Дополнительные флаги — если ЛЮБОЙ из них уже стоит, туториал считается пройденным и не запускается. " +
                 "Полезно для веток (например 'троллинг уже видел')")]
        public string[] additionalCompleteFlags = new string[0];

        [Tooltip("Шаги по порядку")]
        public TutorialStep[] steps = new TutorialStep[0];

        public int FindIndexById(string id)
        {
            if (string.IsNullOrEmpty(id) || steps == null) return -1;
            for (int i = 0; i < steps.Length; i++)
                if (steps[i] != null && steps[i].id == id) return i;
            return -1;
        }
    }
}
