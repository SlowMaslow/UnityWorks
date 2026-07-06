using UnityEngine;

/// <summary>
/// Флажок-чекпоинт: при касании ПЭДОМ активирует чекпоинт (LevelManager.SetCheckpoint) — игрок будет
/// оживать здесь при continue. Требует триггер-коллайдер. Точка спавна = spawnPoint (если задан),
/// иначе позиция флажка + spawnOffset (корень игрока: поверхность − локальный Y пэдов ≈ −1.2).
/// Арт флажка + анимация подъёма — позже.
/// </summary>
[RequireComponent(typeof(Collider))]
public class CheckpointFlag : MonoBehaviour
{
    [Tooltip("Точка спавна игрока при оживлении (корень). Если пусто — позиция флажка + spawnOffset.")]
    [SerializeField] private Transform spawnPoint;

    [Tooltip("Оффсет от флажка до точки спавна, если spawnPoint не задан. Y ≈ −1.2 (локальный Y пэдов).")]
    [SerializeField] private Vector3 spawnOffset = new Vector3(0f, -1.2f, 0f);

    private bool _activated;

    private void Reset()
    {
        var c = GetComponent<Collider>();
        if (c != null) c.isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        var rb = other.attachedRigidbody;
        if (rb == null) return;
        int pads = LayerMask.NameToLayer("Pads");
        if (other.gameObject.layer != pads && rb.gameObject.layer != pads) return; // только пэды

        Vector3 spawn = spawnPoint != null ? spawnPoint.position : transform.position + spawnOffset;
        LevelManager.Instance?.SetCheckpoint(spawn);

        if (!_activated) { _activated = true; /* TODO: анимация подъёма флажка (арт позже) */ }
    }
}
