using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Боевой контроллер карабканья (порт механики из ClimbBodyPrototype в реальную игру).
///
/// Отличие от прототипа: НЕ строит мир. Тело-капсула, два пэда и визуальный риг —
/// ссылки на объекты авторского Player.prefab. Платформы берутся из уровня через
/// PlatformCollisionLogic (роль PlatformTop). Физика/IK/ноги портированы ДОСЛОВНО —
/// game feel прототипа сохранён 1:1.
///
/// Пэд имеет 3 состояния:
///   Dragging — тащим пальцем (кинематик, следует за курсором)
///   Gripped  — захвачен на платформе (кинематик, жёсткий якорь)
///   Dangling — отпущен в воздухе (динамик + гравитация, висит с лёгким тонусом)
///
/// Разрыв (обе руки растянуты на максимум при активном тащении) → GameState.Fail.
/// </summary>
public class ClimbController : MonoBehaviour
{
    private enum PadState { Dragging, Gripped, Dangling }

    [Header("Ссылки (авторский префаб)")]
    [Tooltip("Капсула-тело: Rigidbody mass 10, без коллайдера, FreezePositionZ+RotXY.")]
    public Rigidbody bodyRb;
    [Tooltip("Два пэда. [0]=левый→левая рука, [1]=правый→правая рука. Kinematic Rigidbody + SphereCollider.")]
    public Rigidbody[] padRb = new Rigidbody[2];
    [Tooltip("Корень визуального рига (Skin01). Кости Mixamo. Следует за капсулой.")]
    public Transform visualRig;

    [Header("Геометрия")]
    public float armLength   = 1.24f;

    [Header("Трос (предел руки)")]
    public float tetherStrength = 320f;   // жёсткость удержания на пределе досягаемости
    public float tetherDamping  = 26f;

    [Header("Подтягивание (тащимая рука)")]
    [Tooltip("Сила с которой тащимая рука тянет тело вверх к захвату.")]
    public float pullStrength = 90f;
    public float pullDamping  = 12f;
    [Tooltip("Целевая длина тащимой руки (тело лезет к этой дистанции).")]
    public float pullRest     = 0.7f;

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
    public float uprightStrength = 20f;   // мягче — тело живее наклоняется (свинг)
    public float uprightDamping  = 3f;
    [Tooltip("Гашение раскачки тела в покое (когда не тащим). Больше = быстрее успокаивается.")]
    public float idleBodyDamping = 2.5f;
    [Tooltip("Сила свинга: насколько тело кренится в сторону опоры (0=строго вертикально, 1=вдоль руки).")]
    [Range(0f, 0.6f)] public float swingAmount = 0.25f;

    [Header("Разрыв")]
    public bool breakEnabled = true;    // выключить для отладки растяжения
    [Range(0.85f, 1.1f)] public float breakFactor = 0.95f;
    public float breakHoldTime = 0.5f;
    [Tooltip("Если за это время после разрыва Fail не наступил — форсируем GameState.Fail.")]
    public float forceFailDelay = 3f;
    [Tooltip("Высота капсулы-коллайдера тела (покрывает длину рига, чтобы не проваливаться в зону смерти).")]
    public float bodyColliderHeight = 3f;
    [Tooltip("Радиус капсулы-коллайдера тела (покрывает толщину рига).")]
    public float bodyColliderRadius = 0.7f;

    [Header("Плечи (раздельные точки крепления рук)")]
    [Tooltip("Смещение плеча от центра тела по X. Левая рука крепится к -X, правая к +X.")]
    public float shoulderOffsetX = 0.63f;
    [Tooltip("Высота плеча относительно центра тела (local Y).")]
    public float shoulderLocalY = 1f;

    [Header("Визуальный риг")]
    [Tooltip("Смещение рига относительно центра капсулы.")]
    public Vector3 rigOffset = new Vector3(0f, -2.04f, -0.82f);
    [Tooltip("Масштаб рига (применяется к visualRig в Awake).")]
    public float rigScale = 4.5f;
    [Tooltip("Скрыть меш капсулы (оставить только риг).")]
    public bool hideCapsule = true;

    [Header("Шар в руке (декор вместо визуала пэда)")]
    [Tooltip("Скрыть реальный пэд и показать декоративный шар в кисти.")]
    public bool hidePadVisual = true;
    [Tooltip("Позиция шара в ЛЕВОЙ кисти (для правой X зеркалится). Подбирай в Inspector.")]
    public Vector3 handBallLocalPos = new Vector3(0.02f, 0.05f, 0.04f);
    [Tooltip("Мировой диаметр шара в руке.")]
    public float handBallSize = 0.45f;

    [Header("Старт / захват")]
    [Tooltip("Максимальная дистанция вниз для поиска стартовой платформы под пэдом.")]
    public float startSnapRange = 0.6f;
    [Tooltip("Допуск по высоте для захвата пэда у поверхности платформы (геометрический, не зависит от матрицы слоёв).")]
    public float grabTolerance = 0.25f;

    [Header("Грип-IK (рука держит контроллер)")]
    [Tooltip("На сколько отвести ЗАПЯСТЬЕ назад от контроллера вдоль плечо→контроллер, чтобы ЛАДОНЬ села на контроллер (~длина кисти). Подбирай, чтобы ладонь точно держала шар. Без петли обратной связи → без дрожи.")]
    [Range(0f, 0.6f)] public float handReach = 0f;
    [Tooltip("Распределение снапа кисти (stretchy IK): доля идёт в ЛОКОТЬ (растягивает плечо-локоть), остальное — в запястье (предплечье). 0.5 = поровну, деформация размазана по руке → почти незаметна. 0 = всё в запястье.")]
    [Range(0f, 1f)] public float stretchToElbow = 0.5f;

    [Header("Отладка")]
    public bool showArmLines = false;   // отладочные верёвки плечо→пэд
    public bool debugLog = false;       // ключевые события (захват, разрыв)
    public bool debugVerbose = false;   // подробные логи тела/руки каждый кадр

    private readonly PadState[] state = { PadState.Gripped, PadState.Gripped };
    private readonly Color[] padColors = { new Color(0.2f,0.4f,0.9f), new Color(0.9f,0.2f,0.2f) };
    private readonly TwoBoneArmIK[] _armIK = new TwoBoneArmIK[2];
    private readonly Transform[] _handBalls = new Transform[2];
    // Отдельный визуальный IK-таргет: каждый кадр ставим его так, чтобы ЛАДОНЬ кисти села на контроллер.
    private readonly Transform[] _ikTarget = new Transform[2];
    private LegSwing _legSwing;
    private LineRenderer[] armLines = new LineRenderer[2];
    private Renderer[] padRend = new Renderer[2];

    private int     draggingPad = -1;
    private bool    broken;
    private float   breakTimer;
    private Camera  cam;
    private float   _logTimer;
    private readonly float[] gripSurfaceY = new float[2];
    private float _padRadius = 0.15f;   // вычисляется из коллайдера пэда в EnforcePadConfig (следует за масштабом)
    private int   _blockerMask;         // слои о которые пэд НЕ должен проходить (платформы + стены-коллайдеры)

    // ─── Unity ───────────────────────────────────────────────────────────────
    private void Start()
    {
        cam = Camera.main;
        // Платформы (вкл. боковые PlatformWall) + стены-коллайдеры. БЕЗ слоя Wall (там фон BackGroundWall).
        _blockerMask = LayerMask.GetMask("Platforms", "WallCollider");

        // Слой VisualPad делаем «детектором падения»: сталкивается ТОЛЬКО с FallArea.
        // На нём висит коллайдер тела → при разрыве тело приземляется на зону смерти,
        // НЕ цепляя платформы/монеты/стены во время карабканья.
        int deathLayer = LayerMask.NameToLayer("VisualPad");
        int fallLayer  = LayerMask.NameToLayer("FallArea");
        if (deathLayer >= 0)
            for (int i = 0; i < 32; i++)
                Physics.IgnoreLayerCollision(deathLayer, i, i != fallLayer);

        if (bodyRb == null || padRb == null || padRb.Length < 2 || padRb[0] == null || padRb[1] == null)
        {
            Debug.LogError("[ClimbController] Не назначены ссылки bodyRb/padRb — отключаюсь.");
            enabled = false;
            return;
        }

        EnforceBodyConfig();
        EnforcePadConfig();
        SetupArmIK();
        // Уровень инстанцируется LevelManager.Start() в том же кадре — порядок не гарантирован.
        // Ждём кадр, чтобы платформы (PlatformCollisionLogic.All) уже были в сцене.
        StartCoroutine(DeferredInitialGrip());
    }

    private IEnumerator DeferredInitialGrip()
    {
        yield return null;
        yield return new WaitForFixedUpdate();
        InitialGrip();
    }

    /// <summary>
    /// Телепортирует игрока в точку (корень). Сбрасывает скорость тела и заново цепляет пэды
    /// за платформу под спавном. Вызывается LevelManager после загрузки уровня (игрок один в сцене).
    /// </summary>
    public void MoveTo(Vector3 worldPos)
    {
        transform.position = worldPos;
        if (bodyRb != null)
        {
            bodyRb.position = worldPos;          // тело — дочерний в (0,0,0), мир = корень
            bodyRb.linearVelocity  = Vector3.zero;
            bodyRb.angularVelocity = Vector3.zero;
        }
        if (isActiveAndEnabled) StartCoroutine(DeferredInitialGrip());
    }

    /// <summary>Принудительно выставляет физ-параметры тела (надёжно, независимо от авторинга префаба).</summary>
    private void EnforceBodyConfig()
    {
        bodyRb.mass           = bodyMass;
        bodyRb.angularDamping = bodyAngularDamp;
        bodyRb.useGravity     = true;
        bodyRb.isKinematic    = false;
        bodyRb.interpolation  = RigidbodyInterpolation.Interpolate;
        bodyRb.constraints    = RigidbodyConstraints.FreezePositionZ
                              | RigidbodyConstraints.FreezeRotationX
                              | RigidbodyConstraints.FreezeRotationY;

        // Коллайдер тела на слое VisualPad (сталкивается только с FallArea) — детектор падения.
        // Во время карабканья ни с чем не контактирует; при разрыве приземляется на зону смерти.
        // Капсула подогнана под габариты рига (он co-вращается с телом) — чтобы при кувырке
        // тело не проваливалось в зону: высота покрывает длину рига, радиус — толщину.
        var capsule = bodyRb.GetComponent<CapsuleCollider>();
        if (capsule == null) capsule = bodyRb.gameObject.AddComponent<CapsuleCollider>();
        capsule.enabled   = true;
        capsule.isTrigger = false;
        capsule.direction = 1;                       // вдоль локальной оси Y тела (= длинная ось рига)
        capsule.center    = Vector3.zero;
        capsule.height    = bodyColliderHeight;
        capsule.radius    = bodyColliderRadius;
        int deathLayer = LayerMask.NameToLayer("VisualPad");
        if (deathLayer >= 0) bodyRb.gameObject.layer = deathLayer;

        if (hideCapsule)
        {
            var capRend = bodyRb.GetComponent<Renderer>();
            if (capRend != null) capRend.enabled = false;
        }
    }

    private void EnforcePadConfig()
    {
        for (int i = 0; i < 2; i++)
        {
            var prb = padRb[i];
            prb.mass        = padMass;
            prb.isKinematic = true;        // стартуют захваченными = кинематик
            prb.useGravity  = false;
            // None (НЕ Interpolate!): пэд кинематический и позиционируется точно каждый кадр.
            // Интерполяция давала отставание визуала пэда от физики (~0.12) → меш «парил» над
            // платформой, и IK целился в отстающий transform. С None transform==rb.position.
            prb.interpolation = RigidbodyInterpolation.None;
            prb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            prb.constraints = RigidbodyConstraints.FreezePositionZ
                            | RigidbodyConstraints.FreezeRotationX
                            | RigidbodyConstraints.FreezeRotationY;

            padRend[i] = prb.GetComponent<Renderer>();
            if (padRend[i] != null)
            {
                padRend[i].material.color = padColors[i];
                if (hidePadVisual) padRend[i].enabled = false;
            }

            if (showArmLines)
            {
                var line = prb.gameObject.AddComponent<LineRenderer>();
                line.material   = new Material(Shader.Find("Sprites/Default"));
                line.startColor = line.endColor = new Color(1f, 0.85f, 0.7f);
                line.startWidth = line.endWidth = 0.07f;
                line.positionCount = 2;
                armLines[i] = line;
            }
        }

        // Пэды не сталкиваются друг с другом
        var c0 = padRb[0].GetComponent<Collider>();
        var c1 = padRb[1].GetComponent<Collider>();
        if (c0 != null && c1 != null) Physics.IgnoreCollision(c0, c1, true);

        // Радиус коллайдера пэда = радиус ВИЗУАЛЬНОГО шара (handBallSize — мировой ДИАМЕТР),
        // чтобы габарит коллизии совпадал с видимым контроллером.
        float ballWorldRadius = handBallSize * 0.5f;
        for (int i = 0; i < 2; i++)
        {
            var sci = padRb[i].GetComponent<SphereCollider>();
            if (sci == null) continue;
            var ls = padRb[i].transform.lossyScale;
            float scale = Mathf.Max(ls.x, Mathf.Max(ls.y, ls.z));
            sci.radius = ballWorldRadius / Mathf.Max(0.0001f, scale);
        }
        _padRadius = ballWorldRadius;
    }

    /// <summary>Настраивает 2-костный IK обеих рук рига, таргеты — пэды.</summary>
    private void SetupArmIK()
    {
        if (visualRig == null) return;
        visualRig.localScale = Vector3.one * rigScale;

        // Кости Mixamo: [0]=левая рука→Pad_0, [1]=правая рука→Pad_1
        var boneNames = new (string arm, string fore, string hand)[]
        {
            ("mixamorig:LeftArm",  "mixamorig:LeftForeArm",  "mixamorig:LeftHand"),
            ("mixamorig:RightArm", "mixamorig:RightForeArm", "mixamorig:RightHand"),
        };

        var map = new Dictionary<string, Transform>();
        foreach (var t in visualRig.GetComponentsInChildren<Transform>(true))
            if (!map.ContainsKey(t.name)) map[t.name] = t;

        for (int i = 0; i < 2; i++)
        {
            var ik = visualRig.gameObject.AddComponent<TwoBoneArmIK>();
            map.TryGetValue(boneNames[i].arm,  out ik.upper);
            map.TryGetValue(boneNames[i].fore, out ik.lower);
            map.TryGetValue(boneNames[i].hand, out ik.hand);
            // IK тянет запястье к ОТДЕЛЬНОМУ таргету (не прямо к пэду): в LateUpdate ставим его так,
            // чтобы ЛАДОНЬ (точка handBallLocalPos на кисти) села на контроллер → шар оказывается «в руке».
            if (_ikTarget[i] == null)
                _ikTarget[i] = new GameObject($"IKTarget_{(i==0?"L":"R")}").transform;
            _ikTarget[i].position = padRb[i].position;
            ik.target  = _ikTarget[i];
            // Знак сгиба фиксирован: левая рука (i=0) в одну сторону, правая (i=1) в другую.
            ik.bendSign = (i == 0) ? 1f : -1f;
            ik.Init();

            // Шар = КОНТРОЛЛЕР: самостоятельный объект ровно в позиции пэда (жёстко, без люфта).
            if (hidePadVisual)
            {
                var ball = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                ball.name = $"HandBall_{(i==0?"L":"R")}";
                Destroy(ball.GetComponent<Collider>());
                ball.transform.SetParent(null);
                ball.transform.position   = padRb[i].position;
                ball.transform.localScale = Vector3.one * handBallSize;
                ball.GetComponent<Renderer>().material.color = padColors[i];
                _handBalls[i] = ball.transform;
            }
            _armIK[i] = ik;
        }

        // Покачивание ног
        _legSwing = visualRig.gameObject.AddComponent<LegSwing>();
        map.TryGetValue("mixamorig:LeftUpLeg",  out _legSwing.leftUpLeg);
        map.TryGetValue("mixamorig:RightUpLeg", out _legSwing.rightUpLeg);
        map.TryGetValue("mixamorig:LeftLeg",    out _legSwing.leftLeg);
        map.TryGetValue("mixamorig:RightLeg",   out _legSwing.rightLeg);
        _legSwing.bodyRb = bodyRb;
        _legSwing.Init();
    }

    /// <summary>
    /// Стартовый захват: для каждого пэда ищем платформу под ним (в пределах startSnapRange),
    /// приклеиваем к поверхности и фиксируем как Gripped. Если платформы нет — пэд просто
    /// остаётся в авторской позиции захваченным (тело подтянется).
    /// </summary>
    private void InitialGrip()
    {
        for (int i = 0; i < 2; i++)
        {
            float? surf = FindStartSurface(padRb[i].position);
            if (surf.HasValue)
            {
                float restY = surf.Value + _padRadius;
                Vector3 p = padRb[i].position; p.y = restY;   // дно сферы на поверхности, без проникновения
                padRb[i].position = p;
                padRb[i].transform.position = p;   // синхронизируем visual transform с физикой
                gripSurfaceY[i] = p.y;
            }
            else
            {
                gripSurfaceY[i] = padRb[i].position.y;
            }
            SetState(i, PadState.Gripped);
        }
    }

    /// <summary>Ищет верхнюю поверхность платформы рядом с пэдом по X в пределах startSnapRange по Y.</summary>
    private float? FindStartSurface(Vector3 padPos)
    {
        float best = float.NegativeInfinity;
        bool found = false;
        foreach (var top in PlatformCollisionLogic.All)
        {
            if (top == null || !top.WithinXBounds(padPos.x, 0.01f)) continue;
            float surf = top.SurfaceY;
            // поверхность должна быть около пэда (чуть ниже / на уровне)
            if (surf <= padPos.y + 0.2f && surf >= padPos.y - startSnapRange && surf > best)
            {
                best = surf; found = true;
            }
        }
        return found ? best : (float?)null;
    }

    private Vector3 ShoulderFor(int padIndex)
    {
        float x = (padIndex == 0) ? -shoulderOffsetX : shoulderOffsetX;
        return bodyRb.transform.TransformPoint(new Vector3(x, shoulderLocalY, 0f));
    }

    private void Update()
    {
        if (broken) return;
        if (GameManager.Instance != null && GameManager.Instance.State != GameState.Playing) return;
        if (ClimbUp.Tutorial.TutorialInputGate.GameInputBlocked) return;

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
                ClimbUp.Tutorial.TutorialEvents.Raise(
                    ClimbUp.Tutorial.TutorialEventIds.PadDragStart, padRb[draggingPad].gameObject);
            }
            else if (candidate >= 0 && debugLog)
            {
                Debug.Log($"[Climb] ГРАБ ЗАБЛОКИРОВАН Pad{candidate}: второй пэд не захвачен (state={state[1-candidate]})");
            }
        }
        if (Input.GetMouseButtonUp(0) && draggingPad >= 0)
        {
            ClimbUp.Tutorial.TutorialEvents.Raise(
                ClimbUp.Tutorial.TutorialEventIds.PadDragEnd, padRb[draggingPad].gameObject);
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
                rb.useGravity  = false;
                if (padRend[i] != null) padRend[i].material.color = padColors[i];
                break;
            case PadState.Dangling:
                rb.isKinematic = false;
                rb.useGravity  = true;
                if (padRend[i] != null) padRend[i].material.color = padColors[i] * 0.6f;
                break;
        }
    }

    private void ReleasePad(int i)
    {
        if (!TryGrip(i))
        {
            if (debugLog) Debug.Log($"[Grip] Pad{i} отпущен в воздухе ({padRb[i].position.x:F2},{padRb[i].position.y:F2}) -> Dangling");
            SetState(i, PadState.Dangling); // не на платформе — падает (свободная рука)
        }
    }

    /// <summary>
    /// Захватывает пэд если он у поверхности платформы. true если захвачен.
    /// Геометрический критерий (X в границах платформы + пэд у поверхности) — не зависит
    /// от матрицы коллизий слоёв; OR с триггерным Contains() как быстрый путь.
    /// </summary>
    private bool TryGrip(int i)
    {
        Vector3 pad = padRb[i].position;
        foreach (var top in PlatformCollisionLogic.All)
        {
            if (top == null) continue;
            float restY = top.SurfaceY + _padRadius;
            bool nearSurface = top.WithinXBounds(pad.x, 0f)
                            && pad.y >= top.SurfaceY - 0.05f
                            && pad.y <= restY + grabTolerance;
            bool viaTrigger = top.Contains(padRb[i]);
            if (!nearSurface && !viaTrigger) continue;

            if (debugLog)
            {
                var bodyCol = top.BodyCollider;
                float colTop  = bodyCol != null ? bodyCol.bounds.max.y : -999f;
                Debug.Log($"[Grip] Pad{i} -> '{top.transform.parent?.name}' | SurfaceY={top.SurfaceY:F3} colTopLive={colTop:F3} padR={_padRadius:F3} " +
                          $"=> restY={restY:F3} | padWas=({pad.x:F2},{pad.y:F3}) resultBottom={(restY - _padRadius):F3} gapToSurface={(restY - _padRadius - top.SurfaceY):F3} | match={(nearSurface ? "geom" : "trigger")}");
            }

            Vector3 p = padRb[i].position;
            p.y = restY;   // дно сферы-контроллера на поверхности, без проникновения
            padRb[i].position = p;
            padRb[i].transform.position = p;   // синхронизируем visual transform с физикой
            gripSurfaceY[i] = p.y;
            SetState(i, PadState.Gripped);
            return true;
        }
        return false;
    }

    private void FixedUpdate()
    {
        if (broken) return;

        // Тащим выбранный пэд за пальцем — но кисть НЕ уходит дальше armLength от плеча.
        if (draggingPad >= 0)
        {
            Vector3 sh     = ShoulderFor(draggingPad);
            Vector3 target = ScreenToWorld(Input.mousePosition);
            target.z = 0f;

            Vector3 fromShoulder = target - sh;
            if (fromShoulder.magnitude > armLength)
                target = sh + fromShoulder.normalized * armLength;

            var p = padRb[draggingPad];
            Vector3 desired = Vector3.Lerp(p.position, target, padFollowSpeed * Time.fixedDeltaTime);

            // Пэд не проходит сквозь платформы — скользит вдоль поверхности.
            desired = SlideAroundPlatforms(draggingPad, p.position, desired);

            // Кинематический пэд: задаём И физику, И transform напрямую (НЕ MovePosition —
            // он оставляет visual transform рассинхронным с rb.position → меш «парит»).
            p.position = desired;
            p.transform.position = desired;
        }

        // Болтающийся пэд, коснувшись поверхности платформы, захватывается автоматически
        for (int i = 0; i < 2; i++)
            if (i != draggingPad && state[i] == PadState.Dangling)
                TryGrip(i);

        // Свинг: тело наклоняется ВДОЛЬ направления "от опоры к телу".
        if (uprightStrength > 0f)
        {
            Vector3 anchorPos = Vector3.zero; int cnt = 0;
            for (int i = 0; i < 2; i++)
                if (state[i] != PadState.Dangling) { anchorPos += padRb[i].position; cnt++; }

            Vector3 targetUp = Vector3.up;
            if (cnt > 0)
            {
                anchorPos /= cnt;
                Vector3 toAnchor = (anchorPos - bodyRb.worldCenterOfMass).normalized;
                targetUp = Vector3.Slerp(Vector3.up, toAnchor, swingAmount).normalized;
            }

            Vector3 up   = bodyRb.transform.up;
            Vector3 axis = Vector3.Cross(up, targetUp);
            bodyRb.AddTorque(axis * uprightStrength - bodyRb.angularVelocity * uprightDamping,
                             ForceMode.Acceleration);
        }

        // Когда не тащим — гасим раскачку тела
        if (draggingPad < 0)
            bodyRb.AddForce(-bodyRb.linearVelocity * idleBodyDamping, ForceMode.Acceleration);

        for (int i = 0; i < 2; i++)
        {
            Vector3 shoulder = ShoulderFor(i);
            Vector3 toPad = padRb[i].position - shoulder;
            float   dist  = toPad.magnitude;
            Vector3 dir   = toPad / Mathf.Max(dist, 1e-4f);

            if (state[i] == PadState.Dangling)
            {
                // Свободная рука — МАЯТНИК: гравитация тянет пэд вниз, длина руки удерживает.
                Vector3 vel = padRb[i].linearVelocity;
                float   armHang = armLength * danglingRest;

                if (dist > armHang)
                {
                    float   radialV = Vector3.Dot(vel, dir);
                    Vector3 pull    = -dir * ((dist - armHang) * danglingTension + radialV * 4f);
                    padRb[i].AddForce(pull, ForceMode.Acceleration);
                }
                padRb[i].AddForce(-vel * 0.8f, ForceMode.Acceleration);
            }
            else
            {
                float vAlong = Vector3.Dot(bodyRb.linearVelocity, dir);

                if (i == draggingPad)
                {
                    // Тащимая рука: СИЛЬНО подтягивает тело к захвату.
                    if (dist > pullRest)
                        bodyRb.AddForce(dir * ((dist - pullRest) * pullStrength - vAlong * pullDamping),
                                        ForceMode.Acceleration);
                }
                else
                {
                    // Опорная рука = трос: только удерживает у предела длины.
                    if (dist > armLength)
                    {
                        float excess = dist - armLength;
                        bodyRb.AddForce(dir * (excess * tetherStrength - vAlong * tetherDamping),
                                        ForceMode.Acceleration);
                    }
                }
            }
        }

        // Удержание на пределе: опорная рука держит тело ТРОС-СИЛОЙ (выше). Здесь только мягко гасим
        // скорость ВЫХОДА за предел — стабильно. Прямой позиционный кламп (MovePosition / position +=)
        // УБРАН: он драйвил вибрацию тела на пределе растяжки (замер: с ним bodyVel≈0.69, без него 0.00 —
        // трос-сила сама держит тело у предела). НЕ возвращать позиционный кламп.
        for (int i = 0; i < 2; i++)
        {
            if (state[i] == PadState.Dangling) continue;
            Vector3 sh2     = ShoulderFor(i);
            Vector3 spToPad = padRb[i].position - sh2;
            float   d       = spToPad.magnitude;
            if (d > armLength)
            {
                float vAlong2 = Vector3.Dot(bodyRb.linearVelocity, spToPad.normalized);
                if (vAlong2 < 0f)
                    bodyRb.linearVelocity -= spToPad.normalized * vAlong2 * 0.5f;
            }
        }

        // Разрыв — обе руки растянуты на максимум И игрок АКТИВНО тащит пэд.
        bool bothSupport = state[0] != PadState.Dangling && state[1] != PadState.Dangling;
        bool bothMaxed = bothSupport && draggingPad >= 0
            && Vector3.Distance(ShoulderFor(0), padRb[0].position) > armLength * breakFactor
            && Vector3.Distance(ShoulderFor(1), padRb[1].position) > armLength * breakFactor;
        breakTimer = bothMaxed ? breakTimer + Time.fixedDeltaTime : 0f;
        if (breakEnabled && breakTimer >= breakHoldTime) TriggerBreak();
    }

    private void LateUpdate()
    {
        if (visualRig == null) return;

        // Перелинковка ссылок (после пересоздания рига / domain-reload): шар — самостоятельный объект,
        // IK-компонент и таргет восстанавливаем по той же причине (иначе Solve молча не вызывается).
        for (int i = 0; i < 2; i++)
        {
            if (_handBalls[i] == null)
            { var go = GameObject.Find(i == 0 ? "HandBall_L" : "HandBall_R"); if (go != null) _handBalls[i] = go.transform; }
            if (_armIK[i] == null)
                foreach (var ik in visualRig.GetComponentsInChildren<TwoBoneArmIK>(true))
                    if (ik.hand != null && ik.hand.name.Contains(i == 0 ? "Left" : "Right")) { _armIK[i] = ik; break; }
            if (_ikTarget[i] == null)
            { var go = GameObject.Find($"IKTarget_{(i==0?"L":"R")}"); _ikTarget[i] = go != null ? go.transform : new GameObject($"IKTarget_{(i==0?"L":"R")}").transform; }
            if (_armIK[i] != null && _ikTarget[i] != null && _armIK[i].target != _ikTarget[i]) _armIK[i].target = _ikTarget[i];
        }

        // Визуальный риг следует за капсулой-телом (позиция + наклон/свинг)
        visualRig.position = bodyRb.transform.TransformPoint(rigOffset);
        visualRig.rotation = bodyRb.transform.rotation;

        // Palm-IK: каждый кадр ставим IK-таргет так, чтобы ЛАДОНЬ (точка handBallLocalPos на кисти)
        // села на КОНТРОЛЛЕР (пэд). Feed-forward 3 итерации = полная сходимость В КАДРЕ (плавно,
        // не лагает как серво: при гладком движении тела поза тоже гладкая). После разрыва не солвим.
        if (!broken)
            for (int i = 0; i < 2; i++)
            {
                var ik = _armIK[i];
                if (ik == null || ik.hand == null || _ikTarget[i] == null) continue;
                Vector3 G = padRb[i].position;
                // СТАБИЛЬНОЕ размещение запястья БЕЗ петли обратной связи (она и давала дрожь у предела
                // руки): направление руки берём из ГЕОМЕТРИИ — плечо→контроллер — и отводим запястье на
                // длину кисти назад вдоль него. Не зависит от ориентации из солва → «охотиться» нечему.
                Vector3 sh = ShoulderFor(i);
                Vector3 armDir = G - sh; armDir.z = 0f;
                armDir = armDir.sqrMagnitude > 1e-6f ? armDir.normalized : Vector3.down;
                _ikTarget[i].position = new Vector3(G.x - armDir.x * handReach, G.y - armDir.y * handReach, G.z);
                ik.Solve();
                // ДОСНАП КИСТИ: после IK двигаем руку так, чтобы её грип-точка (handBallLocalPos)
                // легла ТОЧНО на контроллер. Прямой снап (не петля) → шар и в ладони, и на поверхности.
                // Растяжение РАЗМАЗЫВАЕМ по руке (stretchy IK): часть в локоть, часть в запястье →
                // деформация на сустав вдвое меньше, почти незаметна.
                Vector3 grip = handBallLocalPos; if (i == 1) grip.x = -grip.x;
                Vector3 snap = G - ik.hand.TransformPoint(grip);
                if (ik.lower != null) ik.lower.position += snap * stretchToElbow; // локоть+кисть → тянет плечо-локоть
                ik.hand.position += snap * (1f - stretchToElbow);                 // остаток → предплечье
            }

        // Шар = контроллер: жёстко в позиции пэда (без люфта), любой кадр.
        for (int i = 0; i < 2; i++)
        {
            if (_handBalls[i] == null) continue;
            // Шар = контроллер (на поверхности). Кисть доснаплена так, что её ладонь = контроллер → шар в ладони.
            _handBalls[i].position   = padRb[i].position;
            _handBalls[i].localScale = Vector3.one * handBallSize;
        }

        _legSwing?.Solve();
    }

    private void TriggerBreak()
    {
        if (debugLog) Debug.Log($"[BREAK] t={Time.time:F2}");

        // Направление разрыва: куда игрок тянул (от тела к тащимому пэду) — туда и кувырок.
        float dir = Random.value < 0.5f ? -1f : 1f;
        if (draggingPad >= 0)
        {
            float dx = padRb[draggingPad].position.x - bodyRb.position.x;
            if (Mathf.Abs(dx) > 0.01f) dir = Mathf.Sign(dx);
        }

        broken = true;
        draggingPad = -1;
        // 2.5D-кувырок: вращение только вокруг Z (в плоскости экрана), позиция Z заморожена.
        bodyRb.constraints = RigidbodyConstraints.FreezePositionZ
                           | RigidbodyConstraints.FreezeRotationX
                           | RigidbodyConstraints.FreezeRotationY;
        // подброс + толчок В СТОРОНУ РАЗРЫВА + закрутка туда же → кувырок в направлении натяжения
        bodyRb.linearVelocity  += new Vector3(dir * Random.Range(0.8f, 2f), 1.5f, 0f);
        bodyRb.angularVelocity  = new Vector3(0f, 0f, -dir * Random.Range(4f, 7f));
        for (int i = 0; i < 2; i++)
        {
            padRb[i].isKinematic = false;
            padRb[i].useGravity  = true;
            if (padRend[i] != null) padRend[i].material.color = Color.gray;
        }

        if (VFXManager.Instance != null)
        {
            var mid = (padRb[0].position + padRb[1].position) * 0.5f;
            VFXManager.Instance.PlayJointBreakVFX(mid);
        }

        StartCoroutine(FailWhenFallen());
    }

    /// <summary>
    /// После разрыва ждёт, пока тело не достигнет зоны смерти (FailCollider) — тогда Fail.
    /// Тело без коллайдера (чтобы не цеплять платформы/монеты), поэтому контакт ловим
    /// геометрически по bounds зоны. forceFailDelay — запасной предел, если зоны нет/не достигли.
    /// </summary>
    private IEnumerator FailWhenFallen()
    {
        var fail = UnityEngine.Object.FindFirstObjectByType<FailCollider>();
        Collider zone = fail != null ? fail.GetComponent<Collider>() : null;
        float deadline = Time.time + forceFailDelay;

        while (Time.time < deadline)
        {
            if (zone != null)
            {
                var b = zone.bounds;
                Vector3 bp = bodyRb.position;
                if (bp.y <= b.max.y && bp.x >= b.min.x && bp.x <= b.max.x)
                    break;   // тело вошло в зону смерти
            }
            yield return new WaitForFixedUpdate();
        }

        if (GameManager.Instance != null && GameManager.Instance.State == GameState.Playing)
            GameManager.Instance.SetState(GameState.Fail);
    }

    private Vector3 ScreenToWorld(Vector3 screen)
    {
        if (cam == null) cam = Camera.main;
        float planeZ = bodyRb != null ? bodyRb.position.z : 0f;
        var ray = cam.ScreenPointToRay(screen);
        float t = (planeZ - ray.origin.z) / ray.direction.z;
        return ray.origin + ray.direction * t;
    }

    /// <summary>
    /// Не даёт пэду проникнуть в платформы — скользит вдоль поверхности.
    ///  1. Sweep (Raycast) от текущей позиции к цели — ловит быстрый проход насквозь.
    ///  2. ComputePenetration — выталкивает если пэд уже пересекает платформу.
    /// </summary>
    private Vector3 SlideAroundPlatforms(int padIndex, Vector3 from, Vector3 to)
    {
        // Слайд считаем по позиции ВИЗУАЛЬНОГО шара (шар = пэд + сдвиг кисти): из блоков должен
        // выталкиваться видимый контроллер, а не геометрический центр пэда. Коррекцию вернём как пэд.
        Vector3 ballOff = Vector3.zero;
        if (_handBalls != null && _handBalls[padIndex] != null)
        {
            ballOff = _handBalls[padIndex].position - padRb[padIndex].position;
            ballOff.z = 0f;
        }
        from += ballOff; to += ballOff;
        from.z = to.z = bodyRb.position.z;
        float r = _padRadius;
        float moveDist = (to - from).magnitude;

        Vector3 mid = (from + to) * 0.5f;
        var cols = Physics.OverlapSphere(mid, moveDist * 0.5f + r + 0.5f,
                                         _blockerMask, QueryTriggerInteraction.Ignore);
        if (cols.Length == 0) return to - ballOff;

        // СУБСТЕПЫ: дробим движение на шаги <= r/2, чтобы пэд НЕ перепрыгнул тонкую платформу
        // за один кадр (главная причина туннелирования). На каждом субшаге выталкиваем по
        // МИНИМАЛЬНОЙ оси в плоскости XY (как в прототипе — кладёт на ближайшую грань). Z не трогаем.
        int sub = Mathf.Clamp(Mathf.CeilToInt(moveDist / (r * 0.5f)), 1, 64);
        Vector3 step = (to - from) / sub;
        Vector3 cur = from;
        for (int s = 1; s <= sub; s++)
        {
            Vector3 prev = cur;                          // предыдущая РАЗРЕШЁННАЯ позиция (carry-forward)
            cur += step;                                 // двигаем от разрешённой позиции (НЕ ресэмпл линии!)
            cur.z = from.z;
            for (int iter = 0; iter < 4; iter++)
            {
                bool changed = false;
                foreach (var col in cols)
                {
                    if (col == null) continue;
                    var b = col.bounds;
                    float minX = b.min.x - r, maxX = b.max.x + r;
                    float minY = b.min.y - r, maxY = b.max.y + r;
                    if (cur.x <= minX || cur.x >= maxX || cur.y <= minY || cur.y >= maxY) continue;

                    // Выталкиваем к грани, С КОТОРОЙ пэд пришёл (по prev) — нет туннелирования
                    // даже когда точка прошла за центр бокса.
                    if      (prev.y >= maxY) cur.y = maxY;
                    else if (prev.y <= minY) cur.y = minY;
                    else if (prev.x <= minX) cur.x = minX;
                    else if (prev.x >= maxX) cur.x = maxX;
                    else
                    {
                        // prev тоже внутри (редкий случай) → ближайшая грань
                        float dL = cur.x - minX, dR = maxX - cur.x, dB = cur.y - minY, dT = maxY - cur.y;
                        float m = Mathf.Min(Mathf.Min(dL, dR), Mathf.Min(dB, dT));
                        if      (m == dT) cur.y = maxY;
                        else if (m == dB) cur.y = minY;
                        else if (m == dL) cur.x = minX;
                        else              cur.x = maxX;
                    }
                    changed = true;
                }
                if (!changed) break;
            }
        }
        return cur - ballOff;
    }
}
