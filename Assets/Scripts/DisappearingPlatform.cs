using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Группа исчезающих платформ (массив тайлов внутри одного GameObject).
/// Preview: все тайлы полупрозрачные, коллайдеры + PlatformCollisionLogic ВЫКЛ
///   (неграбельно, сквозно) + лёгкая пульсация как подсказка.
/// Active: непрозрачные, грип+коллизия ВКЛ (как обычный тайл) на activeWindow сек;
///   последние warningTime сек — вибрация-предупреждение; затем назад в Preview.
/// Активируется TriggerTile по совпадению groupId (re-pressable — рестартит окно).
/// Пара по id-ключу (НЕ Unity-тег).
/// </summary>
public class DisappearingPlatform : MonoBehaviour
{
    [Tooltip("Ключ пары триггер↔платформы (НЕ Unity-тег). Совпадает с TriggerTile.groupId.")]
    public string groupId = "A";

    [Tooltip("Сколько секунд платформа активна (грабельна) после нажатия триггера.")]
    public float activeWindow = 5f;

    [Tooltip("Последние N секунд активного окна — вибрация-предупреждение.")]
    public float warningTime = 3f;

    [Header("Preview (неактивно)")]
    [Range(0f, 1f)] public float previewAlpha = 0.35f;
    [Tooltip("Амплитуда пульсации прозрачности в preview как подсказка (0 = выкл).")]
    public float previewPulse = 0.12f;

    [Header("Вибрация (предупреждение)")]
    public float vibrateAmp  = 0.04f;
    public float vibrateFreq = 30f;

    // ─── Реестр по groupId (активация из TriggerTile) ────────────────────────
    private static readonly Dictionary<string, List<DisappearingPlatform>> _registry
        = new Dictionary<string, List<DisappearingPlatform>>();

    /// <summary>Смена состояния группы (groupId, active) — для синхронизации кнопки-триггера (Animator).</summary>
    public static event System.Action<string, bool> GroupStateChanged;

    public static void ActivateGroup(string id)
    {
        if (_registry.TryGetValue(id, out var list))
            foreach (var p in list) if (p != null) p.Activate();
    }

    private SpriteRenderer[]          _sprites;
    private Collider[]                _colliders;
    private PlatformCollisionLogic[]  _tops;
    private Color[]                   _baseColors;
    private Vector3                   _home;
    private bool                      _active;
    private Coroutine                 _run;

    private void Awake()
    {
        _sprites   = GetComponentsInChildren<SpriteRenderer>(true);
        _colliders = GetComponentsInChildren<Collider>(true);
        _tops      = GetComponentsInChildren<PlatformCollisionLogic>(true);
        _baseColors = new Color[_sprites.Length];
        for (int i = 0; i < _sprites.Length; i++) _baseColors[i] = _sprites[i].color;
        _home = transform.localPosition;
    }

    private void OnEnable()
    {
        if (!_registry.TryGetValue(groupId, out var list))
        { list = new List<DisappearingPlatform>(); _registry[groupId] = list; }
        if (!list.Contains(this)) list.Add(this);
        SetActiveState(false); // старт — preview
    }

    private void OnDisable()
    {
        if (_registry.TryGetValue(groupId, out var list)) list.Remove(this);
        if (_run != null) { StopCoroutine(_run); _run = null; }
        transform.localPosition = _home;
    }

    private void Update()
    {
        if (!_active && previewPulse > 0f)
            SetAlpha(Mathf.Clamp01(previewAlpha + Mathf.Sin(Time.time * 3f) * previewPulse * 0.5f));
    }

    // ─── Активация ───────────────────────────────────────────────────────────
    public void Activate()
    {
        if (_run != null) StopCoroutine(_run);   // re-pressable → рестарт окна
        transform.localPosition = _home;
        _run = StartCoroutine(ActiveRoutine());
    }

    private IEnumerator ActiveRoutine()
    {
        SetActiveState(true);

        float solid = Mathf.Max(0f, activeWindow - warningTime);
        yield return new WaitForSeconds(solid);

        // Фаза предупреждения — вибрация по X
        float t = 0f;
        while (t < warningTime)
        {
            t += Time.deltaTime;
            float dx = Mathf.Sin(t * vibrateFreq) * vibrateAmp;
            transform.localPosition = _home + new Vector3(dx, 0f, 0f);
            yield return null;
        }
        transform.localPosition = _home;

        SetActiveState(false);
        _run = null;
    }

    private void SetActiveState(bool active)
    {
        _active = active;
        foreach (var c in _colliders) if (c != null) c.enabled = active;
        foreach (var top in _tops)    if (top != null) top.enabled = active; // грип: вкл/выкл в PlatformCollisionLogic.All

        if (active)
        {
            // ВАЖНО: у только что ВКЛЮЧЁННЫХ коллайдеров bounds ещё устаревшие (шага физики не было),
            // поэтому PlatformCollisionLogic.OnEnable мог посчитать SurfaceY по мусорным bounds →
            // «грип за воздух». Форсируем свежие bounds и пересчитываем поверхность у всех тайлов.
            Physics.SyncTransforms();
            foreach (var top in _tops) if (top != null) top.RecomputeSurface();
            RestoreColors();
        }
        else SetAlpha(previewAlpha);

        GroupStateChanged?.Invoke(groupId, active); // кнопка-триггер синхронит свой Animator
    }

    private void SetAlpha(float a)
    {
        for (int i = 0; i < _sprites.Length; i++)
        {
            if (_sprites[i] == null) continue;
            var c = _baseColors[i]; c.a = a; _sprites[i].color = c;
        }
    }

    private void RestoreColors()
    {
        for (int i = 0; i < _sprites.Length; i++)
            if (_sprites[i] != null) _sprites[i].color = _baseColors[i];
    }
}
