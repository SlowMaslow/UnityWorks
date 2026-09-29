using UnityEngine;

/// <summary>
/// Процедурный 2-костный IK для одной руки (плечо→локоть→кисть).
/// При развороте тела локти следуют пространственному ориентиру bendReference.
/// Каждый сегмент отклоняется от исходной позы без добавочного осевого скручивания.
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

    [Tooltip("Если задан, локти следуют ориентации тела в 3D при развороте. Без ссылки используется прежняя плоскость XY.")]
    public Transform bendReference;
    [Min(1f)] public float elbowTurnSpeed = 360f;
    private Vector3 lastBend;
    private bool hasLastBend;

    [Header("Кисть")]
    [Tooltip("Совмещать ладонь с направлением захвата без накопления скручивания.")]
    public bool orientHand = true;
    private ForearmTwistRig twistRig;
    [Tooltip("Локальная ось кисти = нормаль ладони. Skin01 = +Z.")]
    public Vector3 palmLocalAxis    = new Vector3(0, 0, 1);
    [Tooltip("Локальная ось кисти = направление пальцев. Skin01 = +Y.")]
    public Vector3 fingersLocalAxis = new Vector3(0, 1, 0);
    [Tooltip("Куда смотрит ладонь в мире (к стене = +Z).")]
    public Vector3 palmWorldFacing  = Vector3.forward;
    [Tooltip("[не используется — пальцы идут вдоль предплечья]")]
    public Vector3 fingersWorldDir  = Vector3.up;

    private float      _upperLen, _lowerLen;
    private bool       _init;

    // Rest-поза костей (bind), захваченная в Init. Нужна для сброса при оживлении: доснап в
    // ClimbController.LateUpdate пишет в localPosition локтя/кисти, а Solve его НЕ сбрасывает —
    // при разрыве оффсет замирает и портит следующий Solve после Revive (искажает форму руки).
    private Vector3    _upperRestPos, _lowerRestPos, _handRestPos;
    private Quaternion _upperRestRot, _lowerRestRot, _handRestRot;

    public void Init()
    {
        if (upper == null || lower == null || hand == null) return;
        _upperLen = Vector3.Distance(upper.position, lower.position);
        _lowerLen = Vector3.Distance(lower.position, hand.position);

        _upperRestPos = upper.localPosition; _upperRestRot = upper.localRotation;
        _lowerRestPos = lower.localPosition; _lowerRestRot = lower.localRotation;
        _handRestPos  = hand.localPosition;  _handRestRot  = hand.localRotation;

        _init = true;
        twistRig = lower.GetComponent<ForearmTwistRig>();
        ResetSolveHistory();
    }

    /// <summary>
    /// Возвращает кости руки в захваченную в Init bind-позу (и rotation, И localPosition).
    /// ClimbController зовёт это ПЕРЕД каждым Solve: доснап кисти пишет в localPosition локтя/кисти,
    /// а Solve сам его не сбрасывает → без сброса оффсет НАКАПЛИВАЕТСЯ и садится в равновесие,
    /// зависящее от истории (после разрыва/оживления давало излом локтя). Сброс в bind делает позу
    /// детерминированной функцией геометрии → старт и оживление дают одинаковую руку.
    /// </summary>
    public void ResetPose()
    {
        if (!_init) return;
        upper.localPosition = _upperRestPos; upper.localRotation = _upperRestRot;
        lower.localPosition = _lowerRestPos; lower.localRotation = _lowerRestRot;
        hand.localPosition  = _handRestPos;  hand.localRotation  = _handRestRot;
    }

    public void ResetSolveHistory()
    {
        hasLastBend = false;
    }

    public void Solve() => Solve(Time.deltaTime);

    public void Solve(float deltaTime)
    {
        if (!_init || target == null) return;
        // IK тянет ЗАПЯСТЬЕ прямо к пэду. Просто и надёжно — естественные позы рук,
        // локти гнутся корректно. Пэд держится у кисти (не идеально в центре ладони,
        // но визуально приемлемо, без артефактов).
        SolveTo(target.position, deltaTime);
    }

    private void SolveTo(Vector3 goal, float deltaTime)
    {
        Vector3 root = upper.position;

        Vector3 toGoal = goal - root;
        float dist = toGoal.magnitude;
        float maxReach = _upperLen + _lowerLen;
        if (_upperLen < 1e-6f || _lowerLen < 1e-6f) return;
        float margin = Mathf.Min(.01f, Mathf.Min(_upperLen, _lowerLen) * .1f);
        dist = Mathf.Clamp(dist, Mathf.Abs(_upperLen - _lowerLen) + margin, maxReach - margin);

        Vector3 dirToGoal = toGoal.sqrMagnitude > 1e-10f ? toGoal.normalized : (hand.position - root).normalized;
        if (dirToGoal.sqrMagnitude < 1e-6f) dirToGoal = Vector3.down;
        // Both segments must solve toward the same reachable point, including clamped targets.
        Vector3 reachableGoal = root + dirToGoal * dist;

        float cosUpper = (_upperLen*_upperLen + dist*dist - _lowerLen*_lowerLen) / (2f * _upperLen * dist);
        cosUpper = Mathf.Clamp(cosUpper, -1f, 1f);
        float angUpper = Mathf.Acos(cosUpper) * Mathf.Rad2Deg;

        Vector3 bendAxis = planeNormal.normalized;
        Vector3 elbowPos;
        if (bendReference != null)
        {
            // A body-relative elbow hint gives the arm depth during the side-on part
            // of the turn. Projecting it keeps the solve plane valid for 3D targets.
            Vector3 hint = bendReference.TransformDirection(new Vector3(-bendSign * .6f, -1f, -.35f));
            Vector3 bend = Vector3.ProjectOnPlane(hint, dirToGoal);
            if (bend.sqrMagnitude < 1e-8f)
                bend = Vector3.ProjectOnPlane(bendReference.forward, dirToGoal);
            if (bend.sqrMagnitude < 1e-8f)
                bend = Vector3.ProjectOnPlane(bendReference.right, dirToGoal);
            bend.Normalize();
            // Near a pole/aim alignment the projected hint can reverse abruptly.
            // Transport the preceding elbow direction into the new solve plane
            // and turn toward the hint at a bounded angular speed.
            Vector3 previous = Vector3.ProjectOnPlane(lastBend, dirToGoal);
            if (hasLastBend && previous.sqrMagnitude > 1e-8f)
            {
                previous.Normalize();
                float angle = Vector3.SignedAngle(previous, bend, dirToGoal);
                float limit = elbowTurnSpeed * Mathf.Max(0f, deltaTime);
                bend = Quaternion.AngleAxis(Mathf.Clamp(angle, -limit, limit), dirToGoal) * previous;
            }
            lastBend = bend;
            hasLastBend = true;
            bendAxis = Vector3.Cross(dirToGoal, bend).normalized * bendSign;
            float along = _upperLen * cosUpper;
            float height = Mathf.Sqrt(Mathf.Max(0f, _upperLen * _upperLen - along * along));
            elbowPos = root + dirToGoal * along + bend * height;
        }
        else
        {
            // Project the old normal too: even in 2.5D, wrists can be offset in depth.
            bendAxis = Vector3.ProjectOnPlane(bendAxis, dirToGoal).normalized;
            if (bendAxis.sqrMagnitude < 1e-8f)
                bendAxis = Vector3.ProjectOnPlane(Vector3.up, dirToGoal).normalized;
            Vector3 elbowDir = Quaternion.AngleAxis(angUpper * bendSign, bendAxis) * dirToGoal;
            elbowPos = root + elbowDir * _upperLen;
        }

        // Swing each link from its current rest frame without introducing axial roll.
        // ResetPose has restored the bind rotations before this solve. This makes
        // the result independent of how many times the body has turned.
        Vector3 upAim = (elbowPos - root).normalized;
        Vector3 restUpperAim = lower.position - upper.position;
        upper.rotation = Quaternion.FromToRotation(restUpperAim, upAim) * upper.rotation;

        Vector3 loAim = (reachableGoal - elbowPos).normalized;
        Vector3 restLowerAim = hand.position - lower.position;
        lower.rotation = Quaternion.FromToRotation(restLowerAim, loAim) * lower.rotation;

        Quaternion neutralForearm = lower.rotation;
        float twist = 0f;
        if (orientHand)
        {
            Vector3 normal = palmWorldFacing.normalized;
            Vector3 fingers = Vector3.ProjectOnPlane(loAim, normal);
            if (fingers.sqrMagnitude < 1e-6f)
                fingers = Vector3.ProjectOnPlane(hand.TransformDirection(fingersLocalAxis), normal);
            if (fingers.sqrMagnitude < 1e-6f) fingers = Vector3.ProjectOnPlane(Vector3.up, normal);
            Quaternion targetRotation = Quaternion.LookRotation(normal, fingers.normalized) *
                Quaternion.Inverse(Quaternion.LookRotation(palmLocalAxis, fingersLocalAxis));
            Vector3 from = Vector3.ProjectOnPlane(neutralForearm * _handRestRot * palmLocalAxis, loAim);
            Vector3 to = Vector3.ProjectOnPlane(normal, loAim);
            if (from.sqrMagnitude > 1e-8f && to.sqrMagnitude > 1e-8f)
                twist = Vector3.SignedAngle(from, to, loAim);
            // Recalculate from the neutral frame; never unwrap/accumulate full turns.
            lower.rotation = Quaternion.AngleAxis(twist, loAim) * neutralForearm;
            hand.rotation = targetRotation;
        }
        if (twistRig != null) twistRig.Apply(neutralForearm, loAim, twist);
    }
}
