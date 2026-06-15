using UnityEngine;

/// <summary>
/// Визуал платформы: вместо куба-меша рисует 9-slice спрайт (трава+земля) под ширину платформы.
/// Концы спрайта (бордеры) фиксированы, середина тянется — подходит для платформ любой ширины.
/// Трава совмещается с ВЕРХНЕЙ гранью коллайдера (где цепляются пэды), земля уходит вниз —
/// видимая плита толще тонкого коллайдера, но физика не меняется.
/// Дочерний визуал — HideAndDontSave (не сохраняется, пересоздаётся при загрузке) → нет дублей.
/// </summary>
[ExecuteAlways]
public class PlatformSkin : MonoBehaviour
{
    public Sprite sprite;
    [Tooltip("Высота видимой плиты в мире (трава+земля). Пропорции травы/земли сохраняются.")]
    public float visualHeight = 0.9f;
    [Tooltip("Смещение спрайта по Z от центра платформы. Чуть назад от пэдов, чтобы руки были спереди.")]
    public float zOffset = 0.05f;
    public int sortingOrder = 0;

    private SpriteRenderer _sr;

    private void OnEnable()  => Rebuild();
    private void OnDisable()
    {
        if (_sr != null)
        {
            if (Application.isPlaying) Destroy(_sr.gameObject);
            else DestroyImmediate(_sr.gameObject);
            _sr = null;
        }
    }

    private void Rebuild()
    {
        if (sprite == null) return;

        var mr = GetComponent<MeshRenderer>();
        if (mr != null) mr.enabled = false;

        if (_sr == null)
        {
            var go = new GameObject("PlatformVisual") { hideFlags = HideFlags.HideAndDontSave };
            go.transform.SetParent(transform, false);
            _sr = go.AddComponent<SpriteRenderer>();
        }

        _sr.sprite       = sprite;
        _sr.drawMode     = SpriteDrawMode.Sliced;
        _sr.sortingOrder = sortingOrder;

        var pls = transform.lossyScale;
        _sr.transform.localScale = new Vector3(1f / pls.x, 1f / pls.y, 1f / pls.z); // компенсируем масштаб куба
        float width = Mathf.Abs(pls.x);
        _sr.size = new Vector2(width, visualHeight);

        var col   = GetComponent<Collider>();
        float topY = col != null ? col.bounds.max.y : transform.position.y + pls.y * 0.5f;
        _sr.transform.position = new Vector3(transform.position.x, topY - visualHeight * 0.5f,
                                             transform.position.z + zOffset);
    }
}
