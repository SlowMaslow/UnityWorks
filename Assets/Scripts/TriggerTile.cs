using UnityEngine;

/// <summary>
/// Триггер (кнопка): касание ПЭДОМ активирует группу исчезающих платформ с тем же groupId.
/// Пара по id-ключу (НЕ Unity-тег). Re-pressable. Требует триггер-коллайдер.
/// Анимация кнопки/Animator Controller — позже; сейчас только логика.
/// </summary>
[RequireComponent(typeof(Collider))]
public class TriggerTile : MonoBehaviour
{
    [Tooltip("Ключ пары. Совпадает с DisappearingPlatform.groupId.")]
    public string groupId = "A";

    [Tooltip("Мин. пауза между срабатываниями (сек), чтобы не спамить при контакте.")]
    public float cooldown = 0.3f;

    private float _lastFire = -999f;
    private Animator _anim;

    private void Awake() => _anim = GetComponentInParent<Animator>();

    private void OnEnable()  => DisappearingPlatform.GroupStateChanged += OnGroupState;
    private void OnDisable() => DisappearingPlatform.GroupStateChanged -= OnGroupState;

    /// <summary>Кнопка следует за состоянием своей группы: Active=true пока платформа живёт.</summary>
    private void OnGroupState(string id, bool active)
    {
        if (id == groupId && _anim != null) _anim.SetBool("Active", active);
    }

    private void Reset()
    {
        var c = GetComponent<Collider>();
        if (c != null) c.isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (Time.time - _lastFire < cooldown) return;

        var rb = other.attachedRigidbody;
        if (rb == null) return;

        int pads = LayerMask.NameToLayer("Pads");
        if (other.gameObject.layer != pads && rb.gameObject.layer != pads) return; // только пэды

        _lastFire = Time.time;
        DisappearingPlatform.ActivateGroup(groupId);
    }
}
