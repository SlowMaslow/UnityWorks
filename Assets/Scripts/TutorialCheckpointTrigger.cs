using UnityEngine;

/// <summary>
/// Триггер чекпоинта туториала.
/// Детектирует попадание нужного пэда (по тегу ClimbCollider) в зону.
/// Аналог WinCollider — использует физический коллайдер вместо дистанции.
/// </summary>
[RequireComponent(typeof(SphereCollider))]
public class TutorialCheckpointTrigger : MonoBehaviour
{
    [Tooltip("Тег пэда который должен войти в зону: RightPad или LeftPad")]
    [SerializeField] public string padTag = "RightPad";

    [Tooltip("Логический id зоны — payload для TutorialEvents (zone_entered/zone_exited)")]
    [SerializeField] public string zoneId = "";

    /// <summary>true пока нужный пэд находится внутри зоны.</summary>
    public bool IsOccupied { get; private set; }

    private void Awake()
    {
        var col      = GetComponent<SphereCollider>();
        col.isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag(padTag) && IsAbove(other))
        {
            if (!IsOccupied)
            {
                IsOccupied = true;
                ClimbUp.Tutorial.TutorialEvents.Raise(
                    ClimbUp.Tutorial.TutorialEventIds.ZoneEntered, zoneId);
            }
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag(padTag))
        {
            if (IsOccupied)
            {
                IsOccupied = false;
                ClimbUp.Tutorial.TutorialEvents.Raise(
                    ClimbUp.Tutorial.TutorialEventIds.ZoneExited, zoneId);
            }
        }
    }

    private void OnTriggerStay(Collider other)
    {
        if (other.CompareTag(padTag) && IsAbove(other))
        {
            if (!IsOccupied)
            {
                IsOccupied = true;
                ClimbUp.Tutorial.TutorialEvents.Raise(
                    ClimbUp.Tutorial.TutorialEventIds.ZoneEntered, zoneId);
            }
        }
    }

    /// <summary>
    /// Пэд должен быть на уровне платформы или выше (не под ней).
    /// Допуск 0.4 единицы — меньше не считается "сверху".
    /// </summary>
    private bool IsAbove(Collider other)
        => other.transform.position.y >= transform.position.y - 0.4f;
}
