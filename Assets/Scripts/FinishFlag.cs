using UnityEngine;

/// <summary>
/// Финишный флаг (одиночный анимированный флаг Flag_finish вместо пары LeftFin/RightFin).
/// Касание ЛЮБЫМ пэдом → запускает отсчёт победы через существующий WinScript (на игроке):
/// поднимает WinValue до порога, WinScript ведёт 3-2-1 и зовёт LevelManager.CompleteLevel().
/// Уход пэда из зоны сбрасывает отсчёт (как у старых финиш-зон). Требует триггер-коллайдер.
/// </summary>
[RequireComponent(typeof(Collider))]
public class FinishFlag : MonoBehaviour
{
    private WinScript _win;
    private int       _padsInside;

    private void Start()
    {
        var col = GetComponent<Collider>();
        if (col != null) col.isTrigger = true;
        // WinScript живёт на игроке (один в GameScene) — находим при загрузке уровня, как WinCollider.
        _win = FindFirstObjectByType<WinScript>();
        if (_win == null) Debug.LogWarning("[FinishFlag] WinScript не найден в сцене!");
    }

    private static bool IsPad(Collider other)
    {
        if (other.CompareTag("LeftPad") || other.CompareTag("RightPad")) return true;
        int pads = LayerMask.NameToLayer("Pads");
        var rb = other.attachedRigidbody;
        return other.gameObject.layer == pads || (rb != null && rb.gameObject.layer == pads);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (_win == null || !IsPad(other)) return;
        _padsInside++;
        // Одного пэда на флаге достаточно: WinScript срабатывает при WinValue >= 2.
        _win.WinValue = _padsInside > 0 ? 2 : 0;
    }

    private void OnTriggerExit(Collider other)
    {
        if (_win == null || !IsPad(other)) return;
        _padsInside = Mathf.Max(0, _padsInside - 1);
        _win.WinValue = _padsInside > 0 ? 2 : 0;
    }
}
