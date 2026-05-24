using UnityEngine;

/// <summary>
/// Финишная зона (LeftFin / RightFin). Живёт в level prefab.
/// Использует тег пэда вместо serialized Collider-ссылки,
/// чтобы не зависеть от объектов сцены.
/// </summary>
public class WinCollider : MonoBehaviour
{
    /// <summary>
    /// Тег пэда который должен коснуться этой зоны.
    /// LeftFin  → "LeftPad"
    /// RightFin → "RightPad"
    /// </summary>
    [SerializeField] private string padTag = "LeftPad";

    private WinScript _winScript;

    private void Start()
    {
        // WinScript живёт на Player в сцене — находим при загрузке уровня
        _winScript = FindFirstObjectByType<WinScript>();

        if (_winScript == null)
            Debug.LogWarning($"[WinCollider] WinScript не найден в сцене!");
    }

    private void OnTriggerEnter(Collider other)
    {
        if (_winScript == null || !other.CompareTag(padTag)) return;
        _winScript.WinValue++;
    }

    private void OnTriggerExit(Collider other)
    {
        if (_winScript == null || !other.CompareTag(padTag)) return;
        _winScript.WinValue--;
    }
}
