using UnityEngine;

/// <summary>
/// Процедурный 2-костный IK для одной руки (плечо→локоть→кисть).
/// 2.5D: руки двигаются в плоскости XY, нормаль плоскости сгиба = мировой Z.
/// Используем Z как стабильный "up" в LookRotation → roll детерминирован,
/// кости НЕ выкручиваются в узел.
/// Выполняется в LateUpdate ПОСЛЕ позиционирования рига телом.
/// </summary>
public class TwoBoneArmIK : MonoBehaviour
{
    public Transform upper;   // плечо (mixamorig:LeftArm / RightArm)
    public Transform lower;   // локоть (ForeArm)
    public Transform hand;    // кисть (Hand)
    public Transform target;  // куда тянуть кисть (пэд)

    [Tooltip("Нормаль плоскости работы рук (2.5D: руки в XY → нормаль Z).")]
    public Vector3 planeNormal = Vector3.forward;

    [Tooltip("Знак сгиба локтя: +1 или -1. Стабильный для каждой руки (не пересчитывается).")]
    public float bendSign = 1f;

    [Header("Кисть")]
    [Tooltip("Ориентировать кисть: ладонь к стене, пальцы продолжают линию предплечья.")]
    public bool orientHand = true;
    [Tooltip("Локальная ось кисти = нормаль ладони. Skin01 = +Z.")]
    public Vector3 palmLocalAxis    = new Vector3(0, 0, 1);
    [Tooltip("Локальная ось кисти = направление пальцев. Skin01 = +Y.")]
    public Vector3 fingersLocalAxis = new Vector3(0, 1, 0);
    [Tooltip("Куда смотрит ладонь в мире (к стене = +Z).")]
    public Vector3 palmWorldFacing  = Vector3.forward;
    [Tooltip("[не используется — пальцы идут вдоль предплечья]")]
    public Vector3 fingersWorldDir  = Vector3.up;

    private float      _upperLen, _lowerLen;
    private Quaternion _upperOffset, _lowerOffset; // смещение rest-ориентации кости от LookRotation
    private bool       _init;

    public void Init()
    {
        if (upper == null || lower == null || hand == null) return;
        _upperLen = Vector3.Distance(upper.position, lower.position);
        _lowerLen = Vector3.Distance(lower.position, hand.position);

        Vector3 upAim = (lower.position - upper.position).normalized;
        Vector3 loAim = (hand.position  - lower.position).normalized;
        _upperOffset = Quaternion.Inverse(Quaternion.LookRotation(upAim, planeNormal)) * upper.rotation;
        _lowerOffset = Quaternion.Inverse(Quaternion.LookRotation(loAim, planeNormal)) * lower.rotation;

        _init = true;
    }

    public void Solve()
    {
        if (!_init || target == null) return;
        // IK тянет ЗАПЯСТЬЕ прямо к пэду. Просто и надёжно — естественные позы рук,
        // локти гнутся корректно. Пэд держится у кисти (не идеально в центре ладони,
        // но визуально приемлемо, без артефактов).
        SolveTo(target.position);
    }

    private void SolveTo(Vector3 goal)
    {
        Vector3 root = upper.position;

        float dist = Vector3.Distance(root, goal);
        float maxReach = _upperLen + _lowerLen;
        dist = Mathf.Clamp(dist, Mathf.Abs(_upperLen - _lowerLen) + 0.01f, maxReach - 0.01f);

        Vector3 dirToGoal = (goal - root).normalized;

        float cosUpper = (_upperLen*_upperLen + dist*dist - _lowerLen*_lowerLen) / (2f * _upperLen * dist);
        cosUpper = Mathf.Clamp(cosUpper, -1f, 1f);
        float angUpper = Mathf.Acos(cosUpper) * Mathf.Rad2Deg;

        Vector3 bendAxis = planeNormal.normalized;
        Vector3 elbowDir = Quaternion.AngleAxis(angUpper * bendSign, bendAxis) * dirToGoal;
        Vector3 elbowPos = root + elbowDir * _upperLen;

        Vector3 upAim = (elbowPos - root).normalized;
        upper.rotation = Quaternion.LookRotation(upAim, planeNormal) * _upperOffset;

        Vector3 loAim = (goal - elbowPos).normalized;
        lower.rotation = Quaternion.LookRotation(loAim, planeNormal) * _lowerOffset;

        // Кисть: ладонь к стене, ПАЛЬЦЫ продолжают линию предплечья (loAim = локоть→кисть).
        // Рука вниз → пальцы вниз, вверх → вверх, по диагонали → по диагонали. Естественно.
        if (orientHand)
        {
            // Направление пальцев = вдоль предплечья. Ортогонализуем относительно нормали стены,
            // чтобы базис был корректным (пальцы в плоскости, перпендикулярной взгляду ладони).
            Vector3 palmN  = palmWorldFacing.normalized;
            Vector3 fingers = Vector3.ProjectOnPlane(loAim, palmN).normalized;
            if (fingers.sqrMagnitude < 1e-4f) fingers = Vector3.up;

            Quaternion worldTarget = Quaternion.LookRotation(palmN, fingers);
            Quaternion localBasis  = Quaternion.LookRotation(palmLocalAxis.normalized, fingersLocalAxis.normalized);
            hand.rotation = worldTarget * Quaternion.Inverse(localBasis);
        }
    }
}
