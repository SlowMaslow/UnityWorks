using UnityEngine;

/// <summary>
/// Процедурное покачивание ног висящего тела (без IK — опоры для ног нет).
/// Бёдра отклоняются от инерции (тело движется → ноги отстают) + лёгкое idle-колебание.
/// Создаётся прототипом, кости и тело прокидываются снаружи. Solve() в LateUpdate
/// ПОСЛЕ позиционирования рига (как и IK рук).
/// </summary>
public class LegSwing : MonoBehaviour
{
    public Transform leftUpLeg, rightUpLeg;   // бёдра
    public Transform leftLeg,   rightLeg;     // колени (для лёгкого вторичного сгиба)
    public Rigidbody bodyRb;                  // тело-капсула — источник скорости

    [Header("Инерция")]
    [Tooltip("Сила отклонения ног от горизонтальной скорости тела (градусы на м/с).")]
    public float inertiaAngle = 12f;
    [Tooltip("Максимальный угол отклонения бедра.")]
    public float maxSwing = 35f;
    [Tooltip("Сглаживание (больше = плавнее, медленнее реагирует).")]
    public float smooth = 8f;

    [Header("Idle-колебание (жизнь)")]
    [Tooltip("Амплитуда лёгкого покачивания в покое (градусы).")]
    public float idleAmplitude = 4f;
    [Tooltip("Скорость idle-колебания.")]
    public float idleSpeed = 1.5f;
    [Tooltip("Вторичный сгиб колена от свинга (доля).")]
    public float kneeFactor = 0.4f;

    private Quaternion _lUpRest, _rUpRest, _lKneeRest, _rKneeRest;
    private float _curSwing;     // текущий сглаженный угол свинга
    private bool  _init;

    public void Init()
    {
        if (leftUpLeg == null || rightUpLeg == null) return;
        _lUpRest = leftUpLeg.localRotation;
        _rUpRest = rightUpLeg.localRotation;
        if (leftLeg)  _lKneeRest = leftLeg.localRotation;
        if (rightLeg) _rKneeRest = rightLeg.localRotation;
        _init = true;
    }

    public void Solve()
    {
        if (!_init || bodyRb == null) return;

        // Горизонтальная скорость тела (X) → целевой угол отклонения
        float vx = bodyRb.linearVelocity.x;
        float targetSwing = Mathf.Clamp(-vx * inertiaAngle, -maxSwing, maxSwing);

        // idle-колебание поверх
        float idle = Mathf.Sin(Time.time * idleSpeed) * idleAmplitude;

        // сглаживаем
        _curSwing = Mathf.Lerp(_curSwing, targetSwing + idle, smooth * Time.deltaTime);

        // Ось качания = мировой Z (в плоскости экрана, бёдра качаются вперёд-назад вбок)
        // Применяем поворот вокруг локальной оси бедра, соответствующей мировому Z
        ApplySwing(leftUpLeg,  _lUpRest,  _curSwing);
        ApplySwing(rightUpLeg, _rUpRest,  _curSwing);

        // Лёгкий вторичный сгиб коленей (отстают сильнее)
        if (leftLeg)  ApplySwing(leftLeg,  _lKneeRest, _curSwing * kneeFactor);
        if (rightLeg) ApplySwing(rightLeg, _rKneeRest, _curSwing * kneeFactor);
    }

    private void ApplySwing(Transform bone, Quaternion rest, float angle)
    {
        // Качание вокруг мировой оси Z, переведённой в локальную систему родителя кости
        Vector3 worldAxis = Vector3.forward;
        Vector3 localAxis = bone.parent != null
            ? bone.parent.InverseTransformDirection(worldAxis)
            : worldAxis;
        bone.localRotation = rest * Quaternion.AngleAxis(angle, localAxis.normalized);
    }
}
