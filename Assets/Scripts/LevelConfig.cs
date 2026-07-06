using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Конфиг уровня: data-driven задачи под звёзды + пороги.
/// Висит на КОРНЕ префаба уровня. Читается LevelManager при загрузке.
/// Задачи не хардкодим — секретный пак сможет иметь свою 3-ю задачу (просто другой набор).
/// </summary>
public class LevelConfig : MonoBehaviour
{
    /// <summary>Типы задач под звёзды. Добавить новый тип = новый case в оценке (LevelManager).</summary>
    public enum StarTaskType
    {
        Complete,            // прошёл уровень (база — всегда выполнима)
        CollectAllArtifacts, // собрал все артефакты уровня
        BeatTime,            // уложился во время <= TimeThreshold
    }

    [System.Serializable]
    public struct StarTask
    {
        public StarTaskType type;

        [Tooltip("Порог времени (сек) для BeatTime. Игнорируется для остальных типов.")]
        public float timeThreshold;
    }

    [Header("Задачи под звёзды (data-driven). По умолчанию 3: прошёл / артефакты / время.")]
    [SerializeField]
    private StarTask[] tasks = new StarTask[]
    {
        new StarTask { type = StarTaskType.Complete },
        new StarTask { type = StarTaskType.CollectAllArtifacts },
        new StarTask { type = StarTaskType.BeatTime, timeThreshold = 60f },
    };

    /// <summary>Задачи под звёзды этого уровня (по порядку).</summary>
    public IReadOnlyList<StarTask> Tasks => tasks ?? System.Array.Empty<StarTask>();

    /// <summary>Сколько звёзд максимум на уровне (= число задач). Ожидается 3.</summary>
    public int TaskCount => tasks != null ? tasks.Length : 0;

    /// <summary>Короткая метка задачи для UI (карточка/сайдбар).</summary>
    public static string Describe(StarTask t)
    {
        switch (t.type)
        {
            case StarTaskType.Complete:            return "Пройти уровень";
            case StarTaskType.CollectAllArtifacts: return "Собрать все артефакты";
            case StarTaskType.BeatTime:            return $"Пройти за {Mathf.RoundToInt(t.timeThreshold)} сек";
            default:                               return t.type.ToString();
        }
    }
}
