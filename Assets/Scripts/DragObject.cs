using UnityEngine;

/// <summary>
/// Управление пэдом мышью (desktop) / первым касанием (mobile/WebGL).
/// Unity автоматически эмулирует OnMouseDown/Drag/Up через первый тач.
/// Концепция игры: один пэд фиксируется на платформе, другой тащишь — multi-touch не нужен.
/// </summary>
public class DragObject : MonoBehaviour
{
    [SerializeField] private Transform[] padsPositions;
    [SerializeField] private Transform   head;

    [Header("PD Controller")]
    [SerializeField] private float kP = 150f;
    [SerializeField] private float kD = 18f;

    [Header("Joint Break")]
    [SerializeField] private float breakMargin    = 0.35f;
    [SerializeField] private float minPadsDistance = 0.3f;  // пэды не могут налезть друг на друга

    /// <summary>Визуальный пэд этого контрол-пэда. Фризится/размораживается синхронно.</summary>
    [HideInInspector] public Rigidbody visualPadRb;

    // Оригинальный родитель визуального пэда (Hand-кость) — нужен для re-parenting
    private Transform _visualOriginalParent;
    private bool      _visualFrozen;

    private Rigidbody        rb;
    private float            mouseZCoord;
    private float            fixedZPosition;
    private float            breakDistance;
    private CharacterJoint   headJoint;
    private CharacterJoint[] padsJoints     = new CharacterJoint[2];
    private SoftJointLimit   minSwing2Limit;
    private SoftJointLimit   maxSwing2Limit;
    private Quaternion       naturalRotation;
    private bool             isDragging;
    private Vector3          targetWorldPos;
    private bool             _jointsBroken;

    [HideInInspector] public Rigidbody[] padsRB = new Rigidbody[2];

    public bool IsDragging          => isDragging;
    public bool AreJointsBroken     => _jointsBroken;
    public bool IsFirstPad(Rigidbody r) => r == padsRB[0];

    // ─── Unity ───────────────────────────────────────────────────────────────
    private void Start()
    {
        rb              = GetComponent<Rigidbody>();
        fixedZPosition  = transform.position.z;
        naturalRotation = rb.rotation;
        headJoint       = head.GetComponent<CharacterJoint>();

        for (int i = 0; i < 2; i++)
        {
            padsRB[i]     = padsPositions[i].GetComponent<Rigidbody>();
            padsJoints[i] = padsPositions[i].GetComponent<CharacterJoint>();
        }

        // Запоминаем родителя визуального пэда (Hand-кость) — понадобится при re-parenting
        if (visualPadRb != null)
            _visualOriginalParent = visualPadRb.transform.parent;

        minSwing2Limit.limit = 0f;
        maxSwing2Limit.limit = 90f;
        breakDistance        = PadsDistance() + breakMargin;
    }

    private void Update()
    {
        if (padsJoints[0] == null)
        {
            if (!_jointsBroken) { _jointsBroken = true; ReleaseAllConstraints(); }
            return;
        }

        transform.position = new Vector3(transform.position.x, transform.position.y, fixedZPosition);
        CheckJointBreak();
    }

    private void FixedUpdate()
    {
        if (_jointsBroken) return;
        if (!isDragging) return;
        if (GameManager.Instance == null || GameManager.Instance.State != GameState.Playing) return;

        rb.constraints = RigidbodyConstraints.FreezePositionZ
                       | RigidbodyConstraints.FreezeRotationX
                       | RigidbodyConstraints.FreezeRotationY;

        Vector3 posError = targetWorldPos - transform.position;
        float   stretch  = PadsDistance();
        float   factor   = stretch > 1.3f ? Mathf.Max(0.3f, 1f - (stretch - 1.3f)) : 1f;

        var  cc          = CollisionChecker.Instance;
        bool anyGrounded = cc != null && (cc.collideCheck[0] || cc.collideCheck[1]);
        Vector3 force    = posError * (kP * factor) - rb.linearVelocity * kD;
        if (!anyGrounded)
        {
            force.y = Mathf.Min(force.y, 0f);
            force.x *= 0.4f;
        }

        // Минимальная дистанция: убираем компонент силы направленный К другому пэду
        if (stretch < minPadsDistance)
        {
            var   otherRb = rb == padsRB[0] ? padsRB[1] : padsRB[0];
            var   away    = (transform.position - otherRb.position).normalized;
            float dot     = Vector3.Dot(force, away);
            if (dot < 0f) force -= dot * away;  // только если тянем навстречу
        }

        rb.AddForce(force, ForceMode.Force);
    }

    // ─── Input (мышь на desktop, первый тач на mobile — Unity эмулирует автоматически) ──
    private void OnMouseDown()
    {
        if (_jointsBroken) return;
        // Туториал может блокировать ввод
        if (ClimbUp.Tutorial.TutorialInputGate.GameInputBlocked) return;

        // Нельзя оторвать зафиксированный пэд пока другой в воздухе
        var cc = CollisionChecker.Instance;
        if (cc != null)
        {
            int myIdx    = (rb == padsRB[0]) ? 0 : 1;
            int otherIdx = 1 - myIdx;
            if (cc.collideCheck[myIdx] && !cc.collideCheck[otherIdx]) return;
        }

        mouseZCoord    = Camera.main.WorldToScreenPoint(transform.position).z;
        isDragging     = true;
        targetWorldPos = MouseToWorldPoint();
        rb.rotation    = naturalRotation;
        rb.constraints = RigidbodyConstraints.FreezePositionZ
                       | RigidbodyConstraints.FreezeRotationX
                       | RigidbodyConstraints.FreezeRotationY;

        // Сразу размораживаем visual — даже если пэд скользит вдоль платформы (CollisionExit не сработает)
        UnfreezeVisual();

        ClimbUp.Tutorial.TutorialEvents.Raise(
            ClimbUp.Tutorial.TutorialEventIds.PadDragStart, this);
    }

    private void OnMouseUp()
    {
        isDragging            = false;
        if (headJoint != null)
            headJoint.swing2Limit = minSwing2Limit;

        ClimbUp.Tutorial.TutorialEvents.Raise(
            ClimbUp.Tutorial.TutorialEventIds.PadDragEnd, this);
    }

    private void OnMouseDrag()
    {
        targetWorldPos = MouseToWorldPoint();
        LookAtDragPoint();
    }

    // ─── Public API ──────────────────────────────────────────────────────────

    /// <summary>
    /// Замораживает позицию визуального пэда (отвязывает от Hand, снапает к ControlPad).
    /// FreezePosition + FreezeRotationXY — позиция жёсткая, но Z-вращение свободно:
    /// пэд может естественно покрутиться под воздействием CharJoint.
    /// </summary>
    public void FreezeVisual(Vector3 snapPosition)
    {
        if (visualPadRb == null || _visualFrozen) return;
        _visualFrozen = true;
        visualPadRb.transform.SetParent(null, worldPositionStays: true); // отвязать от Hand
        visualPadRb.transform.position = snapPosition;
        visualPadRb.constraints        = RigidbodyConstraints.FreezePosition
                                       | RigidbodyConstraints.FreezeRotationX
                                       | RigidbodyConstraints.FreezeRotationY;
        visualPadRb.linearVelocity     = Vector3.zero;
        visualPadRb.angularVelocity    = Vector3.zero;
    }

    /// <summary>
    /// Возвращает визуальный пэд в нормальное состояние: снимает заморозку, re-parent к Hand.
    /// </summary>
    public void UnfreezeVisual()
    {
        if (visualPadRb == null || !_visualFrozen) return;
        _visualFrozen = false;
        visualPadRb.constraints = RigidbodyConstraints.FreezePositionZ
                                 | RigidbodyConstraints.FreezeRotationX
                                 | RigidbodyConstraints.FreezeRotationY;
        visualPadRb.transform.SetParent(_visualOriginalParent, worldPositionStays: true); // вернуть под Hand
    }

    public void UpdatePadDamping()
    {
        var cc = CollisionChecker.Instance;
        if (cc == null) return;
        padsRB[1].linearDamping = (!cc.collideCheck[0] && cc.collideCheck[1]) ? 10000f : 5f;
        padsRB[0].linearDamping = (!cc.collideCheck[1] && cc.collideCheck[0]) ? 10000f : 5f;
    }

    // ─── Private ─────────────────────────────────────────────────────────────
    private Vector3 MouseToWorldPoint()
    {
        Vector3 mp = Input.mousePosition;
        mp.z = mouseZCoord;
        var pos = Camera.main.ScreenToWorldPoint(mp);
        pos.z = fixedZPosition;
        return pos;
    }

    private void LookAtDragPoint()
    {
        if (headJoint == null) return;
        headJoint.swing2Limit = maxSwing2Limit;
        head.LookAt(targetWorldPos);
    }

    private void ReleaseAllConstraints()
    {
        if (VFXManager.Instance != null && padsPositions != null && padsPositions.Length >= 2
            && padsPositions[0] != null && padsPositions[1] != null)
        {
            var mid = (padsPositions[0].position + padsPositions[1].position) * 0.5f;
            VFXManager.Instance.PlayJointBreakVFX(mid);
        }

        // Visual пэд — уничтожаем CharacterJoint, отцепляем, снимаем все constraints.
        // Наследуем скорость руки чтобы пэд не стартовал из покоя пока рагдолл уже летит.
        if (visualPadRb != null)
        {
            _visualFrozen = false;

            var visualJoint     = visualPadRb.GetComponent<CharacterJoint>();
            var handVelocity    = Vector3.zero;
            var handAngVelocity = Vector3.zero;
            if (visualJoint != null)
            {
                var handRb = visualJoint.connectedBody;
                if (handRb != null)
                {
                    handVelocity    = handRb.linearVelocity;
                    handAngVelocity = handRb.angularVelocity;
                }
                // DestroyImmediate — уничтожаем joint мгновенно (не в конце кадра),
                // иначе следующий physics step ещё тянет пэд к руке
                Object.DestroyImmediate(visualJoint);
            }

            visualPadRb.transform.SetParent(null, worldPositionStays: true);
            visualPadRb.constraints    = RigidbodyConstraints.None;
            visualPadRb.linearDamping  = 0f;
            visualPadRb.angularDamping = 0.05f;
            visualPadRb.linearVelocity  = handVelocity;
            visualPadRb.angularVelocity = handAngVelocity + Random.insideUnitSphere * 4f;

            // Переводим visual пэд на слой VisualPad — взаимодействует ТОЛЬКО с FallArea.
            // Платформы, тело персонажа, всё остальное — игнорируется.
            visualPadRb.gameObject.layer = LayerMask.NameToLayer("VisualPad");
            var visualCol = visualPadRb.GetComponent<Collider>();
            if (visualCol != null) visualCol.isTrigger = false;

        }

        var allRbs = transform.root.GetComponentsInChildren<Rigidbody>(true);
        foreach (var r in allRbs)
            r.constraints = RigidbodyConstraints.None;
    }

    private void CheckJointBreak()
    {
        if (PadsDistance() > breakDistance)
            for (int i = 0; i < 2; i++)
                padsJoints[i].breakForce = 0f;
    }

    private float PadsDistance()
        => Vector3.Distance(padsPositions[0].position, padsPositions[1].position);
}
