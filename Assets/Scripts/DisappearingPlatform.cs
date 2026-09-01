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
///
/// ⭐ ИНВЕРСИЯ (<see cref="inverted"/>, 2026-09-01): тот же автомат наоборот — группа стартует
/// ТВЁРДОЙ, а кнопка УБИРАЕТ её на окно. Это другой глагол: не «построй мост и беги», а «открой
/// проход и успей пройти» (или «убери пол и провались»). Состояние во время окна = !inverted,
/// состояние покоя = inverted — поэтому обе механики живут в одной корутине.
/// </summary>
public class DisappearingPlatform : MonoBehaviour
{
    [Tooltip("Ключ пары триггер↔платформы (НЕ Unity-тег). Совпадает с TriggerTile.groupId.")]
    public string groupId = "A";

    [Header("Инверсия")]
    [Tooltip("Инверсная группа: стартует ТВЁРДОЙ, кнопка убирает её на activeWindow секунд.")]
    public bool inverted = false;

    [Tooltip("СТЕНА: вернувшийся камень убивает того, кто оказался внутри проёма. " +
             "Для ПОЛА выключить — там игрок просто проваливается вниз, давить некого.")]
    public bool crushOnReturn = true;

    [Tooltip("Сколько секунд платформа активна (грабельна) после нажатия триггера.")]
    public float activeWindow = 5f;

    [Tooltip("Последние N секунд активного окна — вибрация-предупреждение.")]
    public float warningTime = 3f;

    [Header("Preview (неактивно)")]
    [Range(0f, 1f)] public float previewAlpha = 0.35f;

    [Tooltip("Порядок сортировки для полупрозрачных МЕШЕЙ группы (кнопка внутри). Тайлы уровня — " +
             "спрайты с порядком 10, поэтому по умолчанию 12: иначе кнопка рисуется ПОД уровнем.")]
    public int previewSortingOrder = 12;
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
    // Не-спрайтовые рендереры (кнопка = два MeshRenderer на Standard). Их материалы НЕПРОЗРАЧНЫ,
    // и одной альфой их не притушить: у Standard прозрачность включается режимом, а не цветом.
    private Renderer[]                _meshes;
    private Material[][]              _meshBase;    // исходные материалы (общие ассеты)
    private Material[][]              _meshFade;    // их полупрозрачные копии, созданные в рантайме
    private int[]                     _meshOrder;   // исходный sortingOrder — вернуть при активации
    private MaterialPropertyBlock     _mpb;         // альфа мешей; RGB там же пишет TriggerTile
    private Vector3                   _home;
    private bool                      _active;
    private Coroutine                 _run;

    private void Awake() => BuildCaches();

    /// <summary>
    /// Сбор ссылок и подготовка прозрачных копий материалов.
    /// ⚠️ Вызывается не только из Awake, но и ЛЕНИВО из SetAlpha/RestoreColors: при перекомпиляции
    /// скриптов ПРЯМО В ПЛЕЙ-МОДЕ Unity перезагружает домен, объекты пересоздаются из сериализованных
    /// данных, и `Awake` для них ПОВТОРНО НЕ ВЫЗЫВАЕТСЯ — все несериализуемые поля обнуляются.
    /// Без ленивого восстановления это давало NullReferenceException из OnEnable и Update.
    /// </summary>
    private void BuildCaches()
    {
        _sprites   = GetComponentsInChildren<SpriteRenderer>(true);
        _colliders = GetComponentsInChildren<Collider>(true);
        _tops      = GetComponentsInChildren<PlatformCollisionLogic>(true);
        _baseColors = new Color[_sprites.Length];
        for (int i = 0; i < _sprites.Length; i++) _baseColors[i] = _sprites[i].color;

        // Меш-рендереры: заранее готовим прозрачные копии их материалов, чтобы в превью подменять.
        // Копия, а не отдельный ассет: ассеты пришлось бы держать в синхроне с оригиналами, а
        // менять альфу на общем ассете в рантайме — значит пачкать его прямо в проекте.
        var meshes = new List<Renderer>();
        foreach (var r in GetComponentsInChildren<Renderer>(true))
            if (!(r is SpriteRenderer)) meshes.Add(r);
        _meshes    = meshes.ToArray();
        _meshBase  = new Material[_meshes.Length][];
        _meshFade  = new Material[_meshes.Length][];
        _meshOrder = new int[_meshes.Length];
        for (int i = 0; i < _meshes.Length; i++)
        {
            _meshOrder[i] = _meshes[i].sortingOrder;
            _meshBase[i] = _meshes[i].sharedMaterials;
            _meshFade[i] = new Material[_meshBase[i].Length];
            for (int k = 0; k < _meshBase[i].Length; k++)
                _meshFade[i][k] = _meshBase[i][k] != null ? MakeFadeCopy(_meshBase[i][k]) : null;
        }
        _home = transform.localPosition;
    }

    private void OnDestroy()
    {
        if (_meshFade == null) return;
        // ⚠️ DestroyImmediate вне плей-мода обязателен: уровень живёт в сцене и в РЕДАКТОРЕ, где
        // Level Editor сносит его при загрузке/выгрузке. Обычный Destroy там ругается в консоль
        // («Destroy may not be called from edit mode») на каждую выгрузку.
        foreach (var arr in _meshFade)                       // копии материалов наши — за собой убираем
            if (arr != null) foreach (var m in arr)
            {
                if (m == null) continue;
                if (Application.isPlaying) Destroy(m); else DestroyImmediate(m);
            }
    }

    /// <summary>
    /// Полупрозрачная копия материала. У Standard прозрачность НЕ включается альфой цвета: нужен
    /// режим Fade целиком — блендинг, выключенный ZWrite, ключевое слово и очередь рендера.
    /// Поэтому «просто выставить альфу» на материалах кнопки (они Opaque, _Mode=0) не работает.
    /// </summary>
    private static Material MakeFadeCopy(Material src)
    {
        var m = new Material(src);
        if (m.HasProperty("_Mode")) m.SetFloat("_Mode", 2f);            // Fade
        m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        m.DisableKeyword("_ALPHATEST_ON");
        m.EnableKeyword("_ALPHABLEND_ON");
        m.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        m.SetInt("_ZWrite", 0);
        m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        // ⚠️ Прозрачной очереди мало: в ней порядок решают sortingLayer/sortingOrder, а тайлы уровня —
        // СПРАЙТЫ с порядком 10, тогда как у мешей кнопки порядок 0. Поэтому кнопка рисовалась ПЕРЕД
        // тайлами и уходила под них («вообще невидима», баг игрока 2026-09-01). Порядок поднимает
        // SetMeshAlpha — см. previewSortingOrder. Промежуточная попытка «блендинг в непрозрачной
        // очереди с ZWrite=1» тоже плоха: даёт грязь и артефакты самоперекрытия.
        return m;
    }

    private void OnEnable()
    {
        if (!_registry.TryGetValue(groupId, out var list))
        { list = new List<DisappearingPlatform>(); _registry[groupId] = list; }
        if (!list.Contains(this)) list.Add(this);
        SetActiveState(inverted); // покой: обычная — preview, инверсная — твёрдая
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
        SetActiveState(!inverted);   // обычная — стала твёрдой; инверсная — открылась

        float solid = Mathf.Max(0f, activeWindow - warningTime);
        yield return new WaitForSeconds(solid);

        // Фаза предупреждения — вибрация по X. Для обычной = «сейчас исчезну»,
        // для инверсной = «сейчас вернусь», и это единственное предупреждение перед смертью.
        float t = 0f;
        while (t < warningTime)
        {
            t += Time.deltaTime;
            float dx = Mathf.Sin(t * vibrateFreq) * vibrateAmp;
            transform.localPosition = _home + new Vector3(dx, 0f, 0f);
            yield return null;
        }
        transform.localPosition = _home;

        SetActiveState(inverted);    // вернулись в покой
        if (inverted && crushOnReturn) CrushCheck();
        _run = null;
    }

    /// <summary>
    /// Камень вернулся — если внутри оказался игрок, это провал (решение игрока 2026-09-01:
    /// «для стен убивает, для пола проваливаемся»). Пол помечается crushOnReturn=false: оттуда
    /// игрок и так улетает вниз, давить некого.
    /// ⚠️ Проверяем ПОСЛЕ включения коллайдеров: SetActiveState(true) делает Physics.SyncTransforms,
    /// поэтому bounds свежие. Коробку сжимаем — пэд, ЛЕЖАЩИЙ на поверхности, внутрь не считается.
    /// </summary>
    private void CrushCheck()
    {
        // ⭐ Давит ТОЛЬКО пэд и ТОЛЬКО находясь ВНУТРИ камня (уточнение игрока 2026-09-01: раньше
        // смерть наступала, даже когда тело просто ВИСЕЛО на этих платформах). Отсюда два условия:
        //   1. слой «Pads» — тело, руки и шар в расчёт не идут;
        //   2. ЦЕНТР пэда внутри bounds тайла, а не просто касание. Схваченный пэд лежит НА
        //      поверхности, его центр выше верхней грани — и он законно не считается раздавленным.
        int padsLayer = LayerMask.NameToLayer("Pads");
        int mask = padsLayer >= 0 ? (1 << padsLayer) : ~0;
        foreach (var c in _colliders)
        {
            if (c == null || !c.enabled) continue;
            var b = c.bounds;
            var hits = Physics.OverlapBox(b.center, b.extents, c.transform.rotation, mask,
                                          QueryTriggerInteraction.Collide);
            foreach (var h in hits)
            {
                if (h == null || h.transform.root == transform.root) continue;   // сам уровень
                var climb = h.transform.root.GetComponentInChildren<ClimbController>();
                if (climb == null) continue;
                if (!b.Contains(h.transform.position)) continue;                 // касание сверху — не в счёт
                // Не Fail напрямую: экран поражения из ниоткуда смотрелся резко. Рвём связку так же,
                // как при перерастяжке — кувырок, VFX, — а Fail придёт следом сам.
                climb.ForceBreak();
                return;
            }
        }
    }

    private void SetActiveState(bool active)
    {
        if (_colliders == null || _tops == null) BuildCaches();   // домен мог перезагрузиться
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

        // ⚠️ Кнопке важно НЕ «твёрдые ли тайлы», а «идёт ли окно после нажатия»: она показывает
        // собственное нажатие, а не состояние камня. У обычной группы это одно и то же, у инверсной —
        // ПРОТИВОПОЛОЖНОЕ (её покой = тайлы твёрдые). Пока рассылали `active`, инверсные кнопки
        // стояли вдавленными в покое и ОТЖИМАЛИСЬ при нажатии (баг, найден игроком 2026-09-01).
        GroupStateChanged?.Invoke(groupId, inverted ? !active : active);
    }

    private void SetAlpha(float a)
    {
        if (_sprites == null || _meshes == null) BuildCaches();   // домен мог перезагрузиться, см. BuildCaches
        for (int i = 0; i < _sprites.Length; i++)
        {
            if (_sprites[i] == null) continue;
            var c = _baseColors[i]; c.a = a; _sprites[i].color = c;
        }
        // Меши (кнопка внутри группы): подменяем материалы на прозрачные копии и гасим альфу.
        // Без этого вложенная кнопка оставалась бы НЕПРОЗРАЧНОЙ, хотя нажать её нельзя, — игрок
        // тыкался бы в неё и не понимал, почему не работает.
        SetMeshAlpha(a, true);
    }

    /// <summary>
    /// Альфа меш-рендереров через MaterialPropertyBlock, с сохранением RGB.
    /// ⚠️ Почему не цветом материала: шляпку кнопки красит TriggerTile в цвет группы, тоже блоком.
    /// Если писать сюда весь `_Color`, один затрёт другого — поэтому каждый правит ТОЛЬКО свой канал:
    /// TriggerTile — RGB, эта функция — альфу.
    /// </summary>
    private void SetMeshAlpha(float a, bool fadeMaterials)
    {
        if (_meshes == null) return;
        if (_mpb == null) _mpb = new MaterialPropertyBlock();
        for (int i = 0; i < _meshes.Length; i++)
        {
            if (_meshes[i] == null) continue;
            if (fadeMaterials) _meshes[i].sharedMaterials = _meshFade[i];
            // В прозрачной очереди порядок решает sortingOrder, а тайлы уровня — спрайты с порядком 10.
            // На время превью поднимаем кнопку выше них, при активации возвращаем как было.
            _meshes[i].sortingOrder = fadeMaterials ? previewSortingOrder : _meshOrder[i];
            _meshes[i].GetPropertyBlock(_mpb);
            Color rgb = _mpb.HasColor("_Color")
                ? _mpb.GetColor("_Color")
                : (_meshBase[i].Length > 0 && _meshBase[i][0] != null ? _meshBase[i][0].color : Color.white);
            rgb.a = a;
            _mpb.SetColor("_Color", rgb);
            _meshes[i].SetPropertyBlock(_mpb);
        }
    }

    private void RestoreColors()
    {
        if (_sprites == null || _meshes == null) BuildCaches();   // домен мог перезагрузиться, см. BuildCaches
        for (int i = 0; i < _sprites.Length; i++)
            if (_sprites[i] != null) _sprites[i].color = _baseColors[i];
        if (_meshes == null) return;
        for (int i = 0; i < _meshes.Length; i++)
            if (_meshes[i] != null) _meshes[i].sharedMaterials = _meshBase[i];
        SetMeshAlpha(1f, false);   // непрозрачно, но окраску шляпки от TriggerTile сохраняем
    }
}
