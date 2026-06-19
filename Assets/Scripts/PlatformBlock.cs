using UnityEngine;

/// <summary>
/// Простой визуал платформы: ТОЛСТЫЙ блок с обычным материалом (текстура «трава сверху + земля»).
/// Никакого кастомного шейдера — обычный Standard-материал. Текстура тайлится по ШИРИНЕ платформы
/// (через MaterialPropertyBlock _MainTex_ST), по высоте — один проход (трава вверху, земля вниз).
/// Видимый блок толще тонкого коллайдера: верх совмещён с верхней гранью коллайдера (где цепляются
/// пэды), тело уходит вниз на visualHeight. Блок сдвинут НАЗАД по Z (frontZ) — передняя грань позади
/// рук/пэдов игрока, персонаж карабкается перед блоками. Коллизия пэдов в XY (Z игнор) → захват цел.
/// Дочерний меш — HideAndDontSave (пересоздаётся, не сохраняется). Работает на любой ширине.
/// </summary>
[ExecuteAlways]
public class PlatformBlock : MonoBehaviour
{
    [Tooltip("Материал блока (Standard с текстурой грунта).")]
    public Material blockMaterial;
    [Tooltip("Высота видимой плиты в мире (трава+земля вниз от верхней грани коллайдера). Чем больше — тем толще блок.")]
    public float visualHeight = 1.3f;
    [Tooltip("Глубина блока по Z (объём вглубь).")]
    public float depth = 0.6f;
    [Tooltip("Мировой Z передней грани блока. Должен быть ПОЗАДИ рук/пэдов игрока (те ~ -0.05).")]
    public float frontZ = 0.02f;
    [Tooltip("Ширина одного тайла текстуры в мире (для горизонтального повтора). ~visualHeight = квадратные тайлы.")]
    public float tileWidth = 1.3f;

    private Transform _visual;
    private MaterialPropertyBlock _mpb;

    private void OnEnable()  => Rebuild();
    private void OnDisable() => Cleanup();

    private void Cleanup()
    {
        if (_visual != null)
        {
            if (Application.isPlaying) Destroy(_visual.gameObject);
            else DestroyImmediate(_visual.gameObject);
            _visual = null;
        }
    }

    public void Rebuild()
    {
        // Прячем собственный тонкий куб-меш платформы — рисует наш толстый блок.
        var ownMr = GetComponent<MeshRenderer>();
        if (ownMr != null) ownMr.enabled = false;

        if (_visual == null)
        {
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name      = "BlockVisual";
            cube.hideFlags = HideFlags.HideAndDontSave;
            var col = cube.GetComponent<Collider>();
            if (col != null) { if (Application.isPlaying) Destroy(col); else DestroyImmediate(col); }
            _visual = cube.transform;
            _visual.SetParent(transform, true);
        }

        var mr = _visual.GetComponent<MeshRenderer>();
        if (blockMaterial != null) mr.sharedMaterial = blockMaterial;

        // Габариты платформы в мире (по коллайдеру) — верх и ширина.
        Bounds b   = GetWorldBounds();
        var    pls = transform.lossyScale;

        float centerX = b.center.x;
        float topY    = b.max.y;
        float centerY = topY - visualHeight * 0.5f;
        float centerZ = frontZ + depth * 0.5f;

        _visual.localScale = new Vector3(
            b.size.x     / Mathf.Max(1e-4f, Mathf.Abs(pls.x)),
            visualHeight / Mathf.Max(1e-4f, Mathf.Abs(pls.y)),
            depth        / Mathf.Max(1e-4f, Mathf.Abs(pls.z)));
        _visual.position = new Vector3(centerX, centerY, centerZ);
        _visual.rotation = Quaternion.identity;

        // Горизонтальный тайл текстуры по ширине (по вертикали — один проход: трава сверху).
        _mpb ??= new MaterialPropertyBlock();
        mr.GetPropertyBlock(_mpb);
        float tilesX = Mathf.Max(1f, Mathf.Round(b.size.x / Mathf.Max(0.01f, tileWidth)));
        _mpb.SetVector("_MainTex_ST", new Vector4(tilesX, 1f, 0f, 0f));
        mr.SetPropertyBlock(_mpb);
    }

    private Bounds GetWorldBounds()
    {
        var col = GetComponent<Collider>();
        if (col != null) return col.bounds;
        var pls = transform.lossyScale;
        return new Bounds(transform.position,
            new Vector3(Mathf.Abs(pls.x), Mathf.Abs(pls.y), Mathf.Abs(pls.z)));
    }
}
