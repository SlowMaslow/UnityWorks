using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Детектор поверхности платформы для новой механики карабканья (роль PlatformTop из прототипа).
/// Висит на дочернем объекте "PadCollision" платформы (тонкий коллайдер у верхней грани).
///
/// Пэды кинематические → детект через ТРИГГЕР (OnTriggerEnter/Exit срабатывает для kinematic).
/// ClimbController опрашивает All / Contains() чтобы решить — захват или падение,
/// и берёт SurfaceY для приклеивания пэда к поверхности.
///
/// Сплошной коллайдер тела платформы (родитель) остаётся для SlideAroundPlatforms
/// (пэд не проходит сквозь платформу) и отдаётся через BodyCollider.
/// </summary>
[RequireComponent(typeof(Collider))]
public class PlatformCollisionLogic : MonoBehaviour
{
    /// <summary>Все активные детекторы платформ в сцене.</summary>
    public static readonly List<PlatformCollisionLogic> All = new List<PlatformCollisionLogic>();

    /// <summary>Мировая Y верхней поверхности платформы (для приклеивания пэда).</summary>
    public float SurfaceY { get; private set; }

    /// <summary>Сплошной коллайдер тела платформы (для SlideAroundPlatforms).</summary>
    public Collider BodyCollider { get; private set; }

    private Collider _triggerCol;
    private Bounds   _bodyBounds;
    private readonly HashSet<Rigidbody> _pads = new HashSet<Rigidbody>();

    private void Awake()
    {
        _triggerCol = GetComponent<Collider>();
        _triggerCol.isTrigger = true;   // детект-зона захвата (надёжно для kinematic пэдов)

        // Тело платформы = коллайдер родителя (сплошной блок). Запасной вариант — свой коллайдер.
        BodyCollider = transform.parent != null ? transform.parent.GetComponent<Collider>() : null;
        if (BodyCollider == null) BodyCollider = _triggerCol;
    }

    private void OnEnable()
    {
        if (!All.Contains(this)) All.Add(this);
        RecomputeSurface();
    }

    private void OnDisable() => All.Remove(this);

    /// <summary>Пересчитывает верхнюю поверхность и X-границы из тела платформы.</summary>
    public void RecomputeSurface()
    {
        if (BodyCollider == null) return;
        _bodyBounds = BodyCollider.bounds;
        SurfaceY    = _bodyBounds.max.y;
    }

    private void OnTriggerEnter(Collider other)
    {
        var rb = other.attachedRigidbody;
        if (rb != null) _pads.Add(rb);
    }

    private void OnTriggerExit(Collider other)
    {
        var rb = other.attachedRigidbody;
        if (rb != null) _pads.Remove(rb);
    }

    /// <summary>Пэд сейчас на поверхности этой платформы?</summary>
    public bool Contains(Rigidbody rb) => _pads.Contains(rb);

    /// <summary>X внутри границ платформы (с отступом от края, чтобы не цеплять угол)?</summary>
    public bool WithinXBounds(float x, float inset)
        => x >= _bodyBounds.min.x + inset && x <= _bodyBounds.max.x - inset;
}
