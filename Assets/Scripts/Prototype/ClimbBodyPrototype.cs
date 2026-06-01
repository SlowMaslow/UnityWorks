using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ПРОТОТИП тела в стиле ADGAC + платформы. Открой сцену ClimbPrototype → Play.
///
/// Пэд имеет 3 состояния:
///   Dragging — тащим пальцем (кинематик, следует за курсором)
///   Gripped  — захвачен на платформе (кинематик, жёсткий якорь)
///   Dangling — отпущен в воздухе (динамик + гравитация, висит с лёгким тонусом)
///
/// Тело висит на захваченных пэдах (тросы + лёгкое напряжение).
/// Болтающийся пэд = свободная рука: висит ниже тела с тонусом (не тряпка).
/// Перетаскиваешь свободный пэд на платформу → захват. Перехватываешься вверх по платформам.
/// Разрыв — когда тело растянуто между двумя опорами на максимум.
///
/// Управление (тач/мышь): зажми пэд и тащи на платформу. Отпусти — захват/падение.
/// </summary>
public class ClimbBodyPrototype : MonoBehaviour
{
    private enum PadState { Dragging, Gripped, Dangling }

    [Header("Геометрия")]
    public float armLength   = 1.5f;
    public float padSpacing  = 1.0f;

    [Header("Трос (предел руки)")]
    public float tetherStrength = 320f;   // жёсткость удержания на пределе досягаемости
    public float tetherDamping  = 26f;

    [Header("Подтягивание (тащимая рука)")]
    [Tooltip("Сила с которой тащимая рука тянет тело вверх к захвату.")]
    public float pullStrength = 90f;
    public float pullDamping  = 12f;
    [Tooltip("Целевая длина тащимой руки (тело лезет к этой дистанции).")]
    public float pullRest     = 0.7f;

    [Header("Опорная рука")]
    [Tooltip("Мягкая сила опоры — держит тело, но даёт ехать вверх при подтягивании.")]
    public float holdTension    = 10f;
    [Range(0.5f, 1f)] public float holdRestFactor = 0.95f;

    [Header("Свободная рука (болтается)")]
    [Tooltip("Натяжение свободной руки — держит пэд у точки покоя против гравитации.")]
    public float danglingTension = 60f;
    [Tooltip("Где висит свободная рука относительно тела (доля armLength вниз).")]
    [Range(0.3f, 1f)] public float danglingRest = 0.75f;

    [Header("Масса и ощущение")]
    public float bodyMass        = 10f;
    public float bodyAngularDamp = 0.5f;
    public float padMass         = 0.15f;  // лёгкий — свободная рука не раскачивает тело
    public float padFollowSpeed  = 25f;

    [Header("Вертикаль")]
    public float uprightStrength = 12f;   // мягче — тело живее наклоняется (свинг)
    public float uprightDamping  = 3f;
    [Tooltip("Гашение раскачки тела в покое (когда не тащим). Больше = быстрее успокаивается.")]
    public float idleBodyDamping = 2.5f;
    [Tooltip("Сила свинга: насколько тело кренится в сторону опоры (0=строго вертикально, 1=вдоль руки).")]
    [Range(0f, 0.6f)] public float swingAmount = 0.25f;

    [Header("Разрыв")]
    public bool breakEnabled = true;    // выключить для отладки растяжения
    [Range(0.85f, 1.1f)] public float breakFactor = 1.0f;
    public float breakHoldTime = 0.12f;

    [Header("Плечи (раздельные точки крепления рук)")]
    [Tooltip("Смещение плеча от центра тела по X. Левая рука крепится к -X, правая к +X.")]
    public float shoulderOffsetX = 0.63f;
    [Tooltip("Высота плеча относительно центра тела (local Y).")]
    public float shoulderLocalY = 1f;

    [Header("Визуальный риг")]
    [Tooltip("FBX персонажа (Skin01). Следует за капсулой-телом.")]
    public GameObject visualRigPrefab;
    [Tooltip("Смещение рига относительно центра капсулы.")]
    public Vector3 rigOffset = new Vector3(0f, -1f, 0f);
    [Tooltip("Масштаб рига.")]
    public float rigScale = 1f;
    [Tooltip("Скрыть меш капсулы (оставить только риг).")]
    public bool hideCapsule = true;

    [Header("Шар в руке (декор вместо визуала пэда)")]
    [Tooltip("Скрыть реальный пэд и показать декоративный шар в кисти.")]
    public bool hidePadVisual = true;
    [Tooltip("Позиция шара в ЛЕВОЙ кисти (для правой X зеркалится). Подбирай в Inspector.")]
    public Vector3 handBallLocalPos = new Vector3(0.02f, 0.05f, 0.04f);
    [Tooltip("Мировой диаметр шара в руке.")]
    public float handBallSize = 0.45f;

    [Header("Отладка")]
    public bool showArmLines = false;   // отладочные верёвки плечо→пэд
    public bool debugLog = true;        // ключевые события (захват, разрыв)
    public bool debugVerbose = false;   // подробные логи тела/руки каждый кадр

    private Rigidbody[] padRb   = new Rigidbody[2];
    private Renderer[]  padRend = new Renderer[2];
    private PadState[]  state   = { PadState.Gripped, PadState.Gripped };
    private Rigidbody   bodyRb;
    private LineRenderer[] armLines = new LineRenderer[2];
    private readonly Vector3 shoulderLocal = new Vector3(0f, 1f, 0f);
    private readonly Color[] padColors = { new Color(0.9f,0.2f,0.2f), new Color(0.2f,0.4f,0.9f) };

    private List<PlatformTop> tops = new List<PlatformTop>();
    private List<Collider>    _platformColliders = new List<Collider>();
    private const float       PAD_COLLIDE_RADIUS = 0.15f;
    private int     draggingPad = -1;
    private bool    broken;
    private float   breakTimer;
    private Camera  cam;
    private float   _logTimer;
    private float[] gripSurfaceY = new float[2];   // поверхность на которой захвачен пэд
    private const float PAD_RADIUS = 0.15f;
    private Transform _visualRig;                  // инстанс визуального рига
    private TwoBoneArmIK[] _armIK = new TwoBoneArmIK[2]; // [0]=левая рука→Pad_0, [1]=правая→Pad_1
    private LegSwing       _legSwing;

    private void Start()
    {
        cam = Camera.main;
        BuildPlatforms();

        // Пэды стартуют ровно на поверхности стартовой платформы (первая в layout: y=4.2, h=0.25)
        float startSurfaceY = 4.2f + 0.25f * 0.5f;   // = 4.325
        float startY = startSurfaceY + 0.15f;        // центр пэда на поверхности = 4.475

        // Тело
        var bodyGO = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        bodyGO.name = "Body";
        bodyGO.transform.position = new Vector3(0f, startY - armLength, 0f);
        Destroy(bodyGO.GetComponent<Collider>()); // тело без физ-коллайдера (как в игре)
        bodyRb = bodyGO.AddComponent<Rigidbody>();
        bodyRb.mass           = bodyMass;
        bodyRb.angularDamping = bodyAngularDamp;
        bodyRb.interpolation  = RigidbodyInterpolation.Interpolate;
        bodyRb.constraints    = RigidbodyConstraints.FreezePositionZ
                              | RigidbodyConstraints.FreezeRotationX
                              | RigidbodyConstraints.FreezeRotationY;

        // Скрыть меш капсулы (физика остаётся)
        if (hideCapsule)
        {
            var capRend = bodyGO.GetComponent<Renderer>();
            if (capRend != null) capRend.enabled = false;
        }

        // Визуальный риг — инстанс FBX, следует за капсулой в LateUpdate
        if (visualRigPrefab != null)
        {
            var rig = Instantiate(visualRigPrefab);
            rig.name = "VisualRig";
            rig.transform.localScale = Vector3.one * rigScale;
            // убираем физику с рига если есть (это чисто визуал)
            foreach (var rb in rig.GetComponentsInChildren<Rigidbody>(true)) Destroy(rb);
            foreach (var col in rig.GetComponentsInChildren<Collider>(true)) Destroy(col);
            _visualRig = rig.transform;
        }

        // Пэды — стартуют захваченными на стартовой платформе
        for (int i = 0; i < 2; i++)
        {
            float x = (i == 0 ? -1f : 1f) * padSpacing * 0.5f;
            var padGO = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            padGO.name = $"Pad_{i}";
            padGO.transform.localScale = Vector3.one * 0.3f;
            padGO.transform.position   = new Vector3(x, startY, 0f);
            Destroy(padGO.GetComponent<Collider>());
            padRend[i] = padGO.GetComponent<Renderer>();
            padRend[i].material.color = padColors[i];
            // Скрываем визуал реального пэда — он только физика/коллизия.
            // Декоративный шар в руке (создаётся в SetupArmIK) показывает где «пэд».
            if (hidePadVisual) padRend[i].enabled = false;

            // Коллайдер пэда (физический — не проходит сквозь платформы)
            var sc = padGO.AddComponent<SphereCollider>();
            sc.radius = 0.5f; // локальный → мировой 0.15 при scale 0.3

            var prb = padGO.AddComponent<Rigidbody>();
            prb.mass        = padMass;
            prb.isKinematic = true;        // захвачен = кинематик
            prb.interpolation = RigidbodyInterpolation.Interpolate;
            prb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            prb.constraints = RigidbodyConstraints.FreezePositionZ
                            | RigidbodyConstraints.FreezeRotationX
                            | RigidbodyConstraints.FreezeRotationY;
            padRb[i] = prb;

            if (showArmLines)
            {
                var line = padGO.AddComponent<LineRenderer>();
                line.material   = new Material(Shader.Find("Sprites/Default"));
                line.startColor = line.endColor = new Color(1f, 0.85f, 0.7f);
                line.startWidth = line.endWidth = 0.07f;
                line.positionCount = 2;
                armLines[i] = line;
            }
        }

        // Пэды не сталкиваются друг с другом
        Physics.IgnoreCollision(padRb[0].GetComponent<Collider>(), padRb[1].GetComponent<Collider>(), true);

        // Точная стартовая посадка на поверхность стартовой платформы (единая логика с TryGrip)
        float startSurf = 4.2f + 0.25f * 0.5f; // surfaceY стартовой платформы
        for (int i = 0; i < 2; i++)
        {
            var p = padRb[i].position;
            p.y = startSurf + PAD_RADIUS;
            padRb[i].position = p;
            gripSurfaceY[i] = startSurf + PAD_RADIUS;
        }

        // Тело стартует так, чтобы плечо→пэд было ЗАМЕТНО меньше armLength (иначе мгновенный
        // разрыв на старте). Плечо на +1 от тела (anchor.y=1). Целимся в ~0.8×armLength зазор.
        float startHang = armLength * 0.8f;
        bodyRb.position = new Vector3(0f, startY - startHang - 1f, 0f);

        SetupArmIK();
    }

    /// <summary>Настраивает 2-костный IK обеих рук рига, таргеты — пэды.</summary>
    private void SetupArmIK()
    {
        if (_visualRig == null) return;

        // Кости Mixamo: [0]=левая рука→Pad_0, [1]=правая рука→Pad_1
        var boneNames = new (string arm, string fore, string hand)[]
        {
            ("mixamorig:LeftArm",  "mixamorig:LeftForeArm",  "mixamorig:LeftHand"),
            ("mixamorig:RightArm", "mixamorig:RightForeArm", "mixamorig:RightHand"),
        };

        var map = new System.Collections.Generic.Dictionary<string, Transform>();
        foreach (var t in _visualRig.GetComponentsInChildren<Transform>(true))
            if (!map.ContainsKey(t.name)) map[t.name] = t;

        for (int i = 0; i < 2; i++)
        {
            var ik = _visualRig.gameObject.AddComponent<TwoBoneArmIK>();
            map.TryGetValue(boneNames[i].arm,  out ik.upper);
            map.TryGetValue(boneNames[i].fore, out ik.lower);
            map.TryGetValue(boneNames[i].hand, out ik.hand);
            ik.target  = padRb[i].transform;
            // Знак сгиба фиксирован: левая рука (i=0) гнётся в одну сторону, правая (i=1) в другую.
            // Локти "наружу-вниз", не перепрыгивают при проходе пэда под плечом.
            ik.bendSign = (i == 0) ? 1f : -1f;
            ik.Init();

            // Декоративный шар В РУКЕ — дочерний кости кисти, едет с ней автоматически,
            // всегда идеально в ладони. Заменяет визуал скрытого реального пэда.
            if (hidePadVisual)
            {
                var ball = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                ball.name = $"HandBall_{(i==0?"L":"R")}";
                Destroy(ball.GetComponent<Collider>());
                ball.transform.SetParent(ik.hand, false);
                // X зеркалим для левой/правой руки (подобрано вручную)
                var bp = handBallLocalPos;
                if (i == 1) bp.x = -bp.x;
                ball.transform.localPosition = bp;
                // компенсируем масштаб рига (×rigScale) чтобы шар был нужного размера
                float s = handBallSize / Mathf.Max(0.0001f, rigScale);
                ball.transform.localScale = Vector3.one * s;
                ball.GetComponent<Renderer>().material.color = padColors[i];
            }
            _armIK[i] = ik;
        }

        // Покачивание ног
        _legSwing = _visualRig.gameObject.AddComponent<LegSwing>();
        map.TryGetValue("mixamorig:LeftUpLeg",  out _legSwing.leftUpLeg);
        map.TryGetValue("mixamorig:RightUpLeg", out _legSwing.rightUpLeg);
        map.TryGetValue("mixamorig:LeftLeg",    out _legSwing.leftLeg);
        map.TryGetValue("mixamorig:RightLeg",   out _legSwing.rightLeg);
        _legSwing.bodyRb = bodyRb;
        _legSwing.Init();
    }

    private void BuildPlatforms()
    {
        // Стартовая широкая + лесенка вверх
        var layout = new (float x, float y, float w)[]
        {
            ( 0.0f, 4.2f, 3.0f),
            (-1.0f, 5.4f, 1.4f),
            ( 1.0f, 6.6f, 1.4f),
            (-0.6f, 7.8f, 1.4f),
            ( 0.8f, 9.0f, 1.4f),
        };
        const float h = 0.25f;   // толщина доски (визуал)
        foreach (var (x, y, w) in layout)
        {
            // Тело платформы — твёрдый коллайдер (пэды на него опираются)
            var p = GameObject.CreatePrimitive(PrimitiveType.Cube);
            p.name = "Platform";
            p.transform.position   = new Vector3(x, y, 0f);
            p.transform.localScale = new Vector3(w, h, 0.5f);
            p.GetComponent<Renderer>().material.color = new Color(0.15f, 0.15f, 0.15f);
            _platformColliders.Add(p.GetComponent<Collider>());

            // Триггер верхней грани — тонкая полоска, ПРИЛЕГАЕТ к поверхности доски.
            // Нижняя грань триггера = surfaceY, толщина = диаметр пэда (детектит касание).
            float surfaceY  = y + h * 0.5f;
            float padRadius = 0.15f;
            float triggerThickness = 0.01f; // очень тонкая полоска на поверхности
            var topGO = new GameObject("PlatformTop");
            // центр триггера на surfaceY + half-thickness → нижняя грань ровно на доске
            topGO.transform.position   = new Vector3(x, surfaceY + triggerThickness * 0.5f, 0f);
            topGO.transform.localScale = Vector3.one;
            var topCol = topGO.AddComponent<BoxCollider>();
            topCol.isTrigger = true;
            topCol.size = new Vector3(w, triggerThickness, 0.5f);

            var top = topGO.AddComponent<PlatformTop>();
            top.surfaceY = surfaceY;
            tops.Add(top);
        }
    }

    private Vector3 Shoulder => bodyRb.transform.TransformPoint(shoulderLocal);

    /// <summary>Точка крепления конкретной руки: pad 0 (левый) → левое плечо (-X), pad 1 → правое (+X).</summary>
    private Vector3 ShoulderFor(int padIndex)
    {
        float x = (padIndex == 0) ? -shoulderOffsetX : shoulderOffsetX;
        return bodyRb.transform.TransformPoint(new Vector3(x, shoulderLocalY, 0f));
    }

    private void Update()
    {
        if (broken) return;

        if (Input.GetMouseButtonDown(0))
        {
            Vector3 mw = ScreenToWorld(Input.mousePosition);
            float best = 0.7f; int candidate = -1;
            for (int i = 0; i < 2; i++)
            {
                float d = Vector2.Distance(mw, padRb[i].position);
                if (d < best) { best = d; candidate = i; }
            }
            // Грабить можно ТОЛЬКО если второй пэд надёжно захвачен (есть опора)
            if (candidate >= 0 && state[1 - candidate] == PadState.Gripped)
            {
                draggingPad = candidate;
                SetState(draggingPad, PadState.Dragging);
            }
            else if (candidate >= 0 && debugLog)
            {
                Debug.Log($"[Climb] ГРАБ ЗАБЛОКИРОВАН Pad{candidate}: второй пэд не захвачен (state={state[1-candidate]})");
            }
        }
        if (Input.GetMouseButtonUp(0) && draggingPad >= 0)
        {
            ReleasePad(draggingPad);
            draggingPad = -1;
        }

        if (showArmLines)
            for (int i = 0; i < 2; i++)
            {
                if (armLines[i] == null) continue;
                armLines[i].SetPosition(0, ShoulderFor(i));
                armLines[i].SetPosition(1, padRb[i].position);
            }
    }

    private void SetState(int i, PadState s)
    {
        if (debugVerbose && state[i] != s)
            Debug.Log($"[Climb] Pad{i}: {state[i]} → {s}  padY={padRb[i].position.y:F2}  " +
                      $"BODY_Y={bodyRb.position.y:F2}  tilt={bodyRb.transform.eulerAngles.z:F0}°  t={Time.time:F1}");
        state[i] = s;
        var rb = padRb[i];
        switch (s)
        {
            case PadState.Dragging:
            case PadState.Gripped:
                rb.isKinematic = true;
                padRend[i].material.color = padColors[i];
                break;
            case PadState.Dangling:
                rb.isKinematic = false;
                rb.useGravity  = true;
                padRend[i].material.color = padColors[i] * 0.6f;
                break;
        }
    }

    private void ReleasePad(int i)
    {
        if (!TryGrip(i))
            SetState(i, PadState.Dangling); // не на платформе — падает (свободная рука)
    }

    /// <summary>Захватывает пэд если он у поверхности платформы. true если захвачен.</summary>
    private bool TryGrip(int i)
    {
        foreach (var top in tops)
        {
            if (!top.Contains(padRb[i])) continue;

            // Тонкий триггер уже гарантирует что пэд у поверхности — просто приклеиваем
            float restY = top.surfaceY + PAD_RADIUS;
            Vector3 p = padRb[i].position;
            p.y = restY;                  // приклеиваем к поверхности
            padRb[i].position = p;
            gripSurfaceY[i] = restY;
            SetState(i, PadState.Gripped);
            return true;
        }
        return false;
    }

    private void FixedUpdate()
    {
        if (broken) return;

        // Тащим выбранный пэд за пальцем — но кисть НЕ уходит дальше armLength от плеча.
        // Палец ведёшь куда угодно, рука остаётся жёсткой (визуально не «резинит»).
        // Чтобы дотянуться дальше — тело должно подъехать (его подтянет опорная рука).
        if (draggingPad >= 0)
        {
            Vector3 sh     = ShoulderFor(draggingPad);  // плечо ИМЕННО этой руки
            Vector3 target = ScreenToWorld(Input.mousePosition);
            target.z = 0f;

            Vector3 fromShoulder = target - sh;
            if (fromShoulder.magnitude > armLength)
                target = sh + fromShoulder.normalized * armLength;

            var p = padRb[draggingPad];
            Vector3 desired = Vector3.Lerp(p.position, target, padFollowSpeed * Time.fixedDeltaTime);

            // Пэд не проходит сквозь платформы — скользит вдоль поверхности.
            desired = SlideAroundPlatforms(p.position, desired);

            p.MovePosition(desired);
        }

        // Болтающийся пэд, коснувшись поверхности платформы, захватывается автоматически
        for (int i = 0; i < 2; i++)
            if (i != draggingPad && state[i] == PadState.Dangling)
                TryGrip(i);

        // Периодический лог поведения тела во время перетаскивания
        if (debugVerbose && draggingPad >= 0)
        {
            _logTimer += Time.fixedDeltaTime;
            if (_logTimer >= 0.3f)
            {
                _logTimer = 0f;
                int anchor = 1 - draggingPad;
                float anchorDist = Vector3.Distance(ShoulderFor(anchor), padRb[anchor].position);
                float dragDist   = Vector3.Distance(ShoulderFor(draggingPad), padRb[draggingPad].position);
                Debug.Log($"[Body] Y={bodyRb.position.y:F2} tilt={bodyRb.transform.eulerAngles.z:F0}° " +
                          $"vel={bodyRb.linearVelocity.magnitude:F1}  опораРука={anchorDist:F2} тащимРука={dragDist:F2}");
            }
        }

        // Лог свободно висящей руки (Dangling) — когда НЕ тащим
        if (debugVerbose && draggingPad < 0)
        {
            for (int i = 0; i < 2; i++)
            {
                if (state[i] != PadState.Dangling) continue;
                _logTimer += Time.fixedDeltaTime;
                if (_logTimer >= 0.3f)
                {
                    _logTimer = 0f;
                    var rb = padRb[i];
                    float d = Vector3.Distance(ShoulderFor(i), rb.position);
                    Debug.Log($"[Dangle] Pad{i} pos=({rb.position.x:F2},{rb.position.y:F2}) " +
                              $"vel={rb.linearVelocity.magnitude:F2} dist={d:F2}/{armLength} " +
                              $"| Body Y={bodyRb.position.y:F2} tilt={bodyRb.transform.eulerAngles.z:F0}° " +
                              $"bodyVel={bodyRb.linearVelocity.magnitude:F2}");
                }
            }
        }

        // Свинг: тело наклоняется ВДОЛЬ направления "от опоры к телу" — как висящий человек
        // кренится в сторону движения. Цель наклона управляема → нет хаотичного заваливания.
        if (uprightStrength > 0f)
        {
            // Опорная точка = средняя позиция захваченных/тащимых пэдов
            Vector3 anchorPos = Vector3.zero; int cnt = 0;
            for (int i = 0; i < 2; i++)
                if (state[i] != PadState.Dangling) { anchorPos += padRb[i].position; cnt++; }

            Vector3 targetUp = Vector3.up;
            if (cnt > 0)
            {
                anchorPos /= cnt;
                // направление от тела к опоре — тело "висит" вдоль него
                Vector3 toAnchor = (anchorPos - bodyRb.worldCenterOfMass).normalized;
                // смешиваем чистую вертикаль с направлением на опору (swingAmount — сила наклона)
                targetUp = Vector3.Slerp(Vector3.up, toAnchor, swingAmount).normalized;
            }

            Vector3 up   = bodyRb.transform.up;
            Vector3 axis = Vector3.Cross(up, targetUp);
            bodyRb.AddTorque(axis * uprightStrength - bodyRb.angularVelocity * uprightDamping,
                             ForceMode.Acceleration);
        }

        // Когда не тащим — гасим раскачку тела (вис стабилизируется, без вечного маятника)
        if (draggingPad < 0)
            bodyRb.AddForce(-bodyRb.linearVelocity * idleBodyDamping, ForceMode.Acceleration);

        for (int i = 0; i < 2; i++)
        {
            Vector3 shoulder = ShoulderFor(i);  // каждая рука от СВОЕГО плеча
            Vector3 toPad = padRb[i].position - shoulder;
            float   dist  = toPad.magnitude;
            Vector3 dir   = toPad / Mathf.Max(dist, 1e-4f);

            if (state[i] == PadState.Dangling)
            {
                // Свободная рука — МАЯТНИК: гравитация свободно тянет пэд вниз (реальный вес),
                // а длина руки удерживает на дистанции от плеча → пэд качается как груз на нити.
                // Масса пэда мала (0.15) → не раскачивает тело.
                Vector3 vel = padRb[i].linearVelocity;
                float   armHang = armLength * danglingRest; // длина «верёвки» руки

                if (dist > armHang)
                {
                    // Жёсткое ограничение длины руки (как нить маятника) — удерживает,
                    // но гравитация даёт качаться. Гасим только радиальную скорость (не тангенциальную).
                    float   radialV = Vector3.Dot(vel, dir);
                    Vector3 pull    = -dir * ((dist - armHang) * danglingTension + radialV * 4f);
                    padRb[i].AddForce(pull, ForceMode.Acceleration);
                }
                // лёгкое общее демпфирование чтобы маятник со временем затухал
                padRb[i].AddForce(-vel * 0.8f, ForceMode.Acceleration);
            }
            else
            {
                float vAlong = Vector3.Dot(bodyRb.linearVelocity, dir);

                // Силы рук — в ЦЕНТР МАСС (AddForce), без рычага → не закручивают тело.
                // Наклон/свинг контролируется отдельно через uprightStrength (стабильно).
                if (i == draggingPad)
                {
                    // Тащимая рука: СИЛЬНО подтягивает тело к захвату (мышечная тяга вверх).
                    if (dist > pullRest)
                        bodyRb.AddForce(dir * ((dist - pullRest) * pullStrength - vAlong * pullDamping),
                                        ForceMode.Acceleration);
                }
                else
                {
                    // Опорная рука = трос: ТОЛЬКО удерживает у предела длины (тянет к пэду),
                    // внутри радиуса рука свободно сгибается — НЕ отталкивает тело.
                    if (dist > armLength)
                    {
                        float excess = dist - armLength;
                        bodyRb.AddForce(dir * (excess * tetherStrength - vAlong * tetherDamping),
                                        ForceMode.Acceleration);
                    }
                }
            }
        }

        // Мягкий кламп: если плечо ушло за длину опорной руки — корректируем позицию ЧАСТИЧНО
        // (доля 0.5, не телепорт) и прикладываем в ЦЕНТР МАСС (без рычага → не закручивает тело).
        for (int i = 0; i < 2; i++)
        {
            if (state[i] == PadState.Dangling) continue;
            Vector3 sh2   = ShoulderFor(i);   // от СВОЕГО плеча
            Vector3 spToPad = padRb[i].position - sh2;
            float   d     = spToPad.magnitude;
            if (d > armLength)
            {
                Vector3 correction = spToPad.normalized * (d - armLength) * 0.5f; // мягко, без рывка
                bodyRb.position += correction;
                float vAlong2 = Vector3.Dot(bodyRb.linearVelocity, spToPad.normalized);
                if (vAlong2 < 0f)
                    bodyRb.linearVelocity -= spToPad.normalized * vAlong2 * 0.5f;
            }
        }

        // Разрыв — когда ОБЕ руки растянуты на максимум И игрок АКТИВНО тащит пэд.
        // Без проверки draggingPad тело провисает под весом на старте и рвётся ложно.
        // Разрыв должен быть следствием действия игрока (растащил пэды), не пассивного виса.
        bool bothSupport = state[0] != PadState.Dangling && state[1] != PadState.Dangling;
        bool bothMaxed = bothSupport && draggingPad >= 0
            && Vector3.Distance(ShoulderFor(0), padRb[0].position) > armLength * breakFactor
            && Vector3.Distance(ShoulderFor(1), padRb[1].position) > armLength * breakFactor;
        breakTimer = bothMaxed ? breakTimer + Time.fixedDeltaTime : 0f;
        if (breakEnabled && breakTimer >= breakHoldTime) TriggerBreak();
    }

    private void LateUpdate()
    {
        // Визуальный риг следует за капсулой-телом (позиция + наклон/свинг)
        if (_visualRig == null) return;
        _visualRig.position = bodyRb.transform.TransformPoint(rigOffset);
        _visualRig.rotation = bodyRb.transform.rotation;

        // IK рук — ПОСЛЕ позиционирования рига: кисти тянутся к пэдам
        for (int i = 0; i < 2; i++)
            _armIK[i]?.Solve();

        // Покачивание ног
        _legSwing?.Solve();
    }

    private void TriggerBreak()
    {
        if (debugLog) Debug.Log($"[BREAK] t={Time.time:F2}");

        broken = true;
        draggingPad = -1;
        bodyRb.constraints = RigidbodyConstraints.FreezePositionZ;
        for (int i = 0; i < 2; i++)
        {
            padRb[i].isKinematic = false;
            padRb[i].useGravity  = true;
            padRend[i].material.color = Color.gray;
        }
    }

    private Vector3 ScreenToWorld(Vector3 screen)
    {
        var ray = cam.ScreenPointToRay(screen);
        float t = (0f - ray.origin.z) / ray.direction.z;
        return ray.origin + ray.direction * t;
    }

    /// <summary>
    /// Не даёт пэду проникнуть в платформы — скользит вдоль поверхности.
    /// Двойная защита:
    ///  1. Sweep (SphereCast) от текущей позиции к цели — ловит быстрый проход насквозь
    ///     тонких платформ, обрезает движение до точки касания.
    ///  2. ComputePenetration — выталкивает если пэд уже пересекает платформу
    ///     (работает даже когда центр пэда ВНУТРИ коллайдера, в отличие от ClosestPoint).
    /// </summary>
    private Vector3 SlideAroundPlatforms(Vector3 from, Vector3 to)
    {
        from.z = 0f; to.z = 0f;
        Vector3 move = to - from;
        float moveDist = move.magnitude;

        // ── 1. Sweep: ловим быстрый проход сквозь тонкую платформу ──
        if (moveDist > 1e-4f)
        {
            foreach (var col in _platformColliders)
            {
                if (col == null) continue;
                // расстояние от точки старта до платформы вдоль направления движения
                if (col.Raycast(new Ray(from, move / moveDist), out var hit, moveDist + PAD_COLLIDE_RADIUS))
                {
                    // упёрлись — обрезаем до точки касания (с зазором на радиус) и
                    // оставляем только тангенциальную составляющую (скольжение)
                    float allowed = Mathf.Max(0f, hit.distance - PAD_COLLIDE_RADIUS);
                    Vector3 stop = from + (move / moveDist) * allowed;
                    Vector3 tangent = Vector3.ProjectOnPlane(to - stop, hit.normal);
                    to = stop + tangent;
                    to.z = 0f;
                }
            }
        }

        // ── 2. ComputePenetration: выталкиваем если пэд внутри/пересекает платформу ──
        var padCol = padRb[draggingPad].GetComponent<Collider>();
        for (int iter = 0; iter < 4; iter++)
        {
            bool pushed = false;
            foreach (var col in _platformColliders)
            {
                if (col == null) continue;
                if (Physics.ComputePenetration(
                        padCol, to, Quaternion.identity,
                        col, col.transform.position, col.transform.rotation,
                        out Vector3 dir, out float depth))
                {
                    to += dir * depth;
                    to.z = 0f;
                    pushed = true;
                }
            }
            if (!pushed) break;
        }
        return to;
    }
}
