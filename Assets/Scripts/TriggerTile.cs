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

    [Tooltip("Какой рендерер красить в цвет группы (красная шляпка кнопки). " +
             "Пусто — найдём сам по материалу с 'red' в имени.")]
    public Renderer colorTarget;

    /// <summary>Телеметрия: кнопку нажали (группа, точка). Только сообщает, ни на что не влияет.</summary>
    public static event System.Action<string, Vector3> Pressed;

    private float _lastFire = -999f;
    private Animator _anim;

    private void Awake()
    {
        _anim = GetComponentInParent<Animator>();
        TintByGroup();
    }

    /// <summary>
    /// Красим шляпку кнопки в цвет её группы (запрос игрока 2026-09-01): при нескольких группах на
    /// уровне игрок должен видеть, какая кнопка что открывает. Цвет берём из <see cref="GroupPalette"/> —
    /// ровно тот же, которым Level Editor подсвечивает группы в сцене.
    ///
    /// ⚠️ Через MaterialPropertyBlock, а НЕ подменой материала: материал у кнопки общий ассет (покрасив
    /// его, покрасили бы все кнопки разом), а кроме того DisappearingPlatform подменяет материалы на
    /// полупрозрачные, когда кнопка лежит ВНУТРИ группы. Блок живёт на рендерере и переживает подмену.
    /// Альфу здесь не трогаем — её пишет DisappearingPlatform, и каждый правит только свой канал.
    /// </summary>
    private void TintByGroup()
    {
        var target = colorTarget;
        if (target == null)
            foreach (var r in GetComponentsInChildren<Renderer>(true))
            {
                var m = r.sharedMaterial;
                if (m != null && m.name.ToLowerInvariant().Contains("red")) { target = r; break; }
            }
        if (target == null) return;

        var mpb = new MaterialPropertyBlock();
        target.GetPropertyBlock(mpb);
        var col = GroupPalette.For(groupId);
        float alpha = mpb.HasColor("_Color") ? mpb.GetColor("_Color").a
                    : (target.sharedMaterial != null ? target.sharedMaterial.color.a : 1f);
        col.a = alpha;
        mpb.SetColor("_Color", col);
        target.SetPropertyBlock(mpb);
    }

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
        if (Pressed != null) Pressed(groupId, transform.position);
        DisappearingPlatform.ActivateGroup(groupId);
    }
}
