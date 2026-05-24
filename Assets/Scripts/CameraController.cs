using UnityEngine;

/// <summary>
/// Камера следует за Player.
/// Поддерживает временный override цели и дистанции (для туториала — CameraFocusAction).
/// </summary>
public class CameraController : MonoBehaviour
{
    [Tooltip("Оставить пустым — найдёт Player автоматически")]
    [SerializeField] private Transform target;

    [Tooltip("Скорость плавного следования. 0 — мгновенно.")]
    [SerializeField] private float defaultLerpSpeed = 0f;

    [Tooltip("Сдвиг по Y для стандартного следования за Player (1 = игрок ниже центра)")]
    [SerializeField] private float defaultYOffset = 1f;

    // ── Temporary override ───────────────────────────────────────────────────
    private Transform _overrideTarget;
    private float     _overrideLerpSpeed;
    private bool      _hasOverrideZ;
    private float     _overrideZ;
    private float     _overrideYOffset;
    private Vector3   _overrideLocalOffset;
    private float     _originalZ;

    public static CameraController Instance { get; private set; }

    private void Awake()
    {
        Instance   = this;
        _originalZ = transform.position.z;
    }
    private void OnDestroy() { if (Instance == this) Instance = null; }

    /// <summary>
    /// Временно фокусирует камеру на другом объекте.
    /// distance > 0 — установит camera.z = target.z - distance (приближение по оси Z).
    /// distance == 0 — Z не трогается (только X/Y следуют за целью).
    /// </summary>
    public void SetTemporaryTarget(Transform t, float lerpSpeed = 4f,
        float distance = 0f, float yOffset = 0f, Vector3 worldOffset = default)
    {
        _overrideTarget      = t;
        _overrideLerpSpeed   = lerpSpeed;
        _overrideYOffset     = yOffset;
        _overrideLocalOffset = worldOffset; // переменная названа исторически, теперь world
        _hasOverrideZ        = distance > 0f;
        if (_hasOverrideZ && t != null)
            _overrideZ = t.position.z + worldOffset.z - distance;
    }

    /// <summary>Возвращает камеру к стандартной цели (Player) и оригинальному Z.</summary>
    public void ClearTemporaryTarget(float lerpSpeed = 4f)
    {
        _overrideTarget    = null;
        _overrideLerpSpeed = lerpSpeed;
        _hasOverrideZ      = false;
    }

    private void LateUpdate()
    {
        if (target == null) target = FindPlayerTarget();

        Transform active = _overrideTarget != null ? _overrideTarget : target;
        if (active == null) return;

        float wantZ = _hasOverrideZ
            ? _overrideZ
            : (_overrideTarget != null ? transform.position.z : _originalZ);

        float yOff = _overrideTarget != null ? _overrideYOffset : defaultYOffset;
        Vector3 activePos = _overrideTarget != null
            ? active.position + _overrideLocalOffset
            : active.position;

        Vector3 desired = new Vector3(
            activePos.x,
            activePos.y + yOff,
            wantZ);

        float lerp = _overrideTarget != null
            ? _overrideLerpSpeed
            : defaultLerpSpeed;

        if (lerp <= 0f)
            transform.position = desired;
        else
            transform.position = Vector3.Lerp(transform.position, desired,
                1f - Mathf.Exp(-lerp * Time.unscaledDeltaTime));
    }

    private static Transform FindPlayerTarget()
    {
        var playerGO = GameObject.FindWithTag("Player");
        if (playerGO == null) playerGO = GameObject.Find("Player");
        if (playerGO == null) return null;

        foreach (var t in playerGO.GetComponentsInChildren<Transform>(true))
            if (t.name.EndsWith("Hips")) return t;
        foreach (var t in playerGO.GetComponentsInChildren<Transform>(true))
            if (t.name.EndsWith("Spine1")) return t;
        return playerGO.transform;
    }
}
