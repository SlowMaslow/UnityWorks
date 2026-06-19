using UnityEngine;

/// <summary>
/// Слой параллакс-фона.
/// Без тайлинга: слой следует за камерой, pos = startPos + (cam - camStart) * parallaxFactor.
///   (0 = неподвижен в мире, 1 = приклеен к камере). Дальние слои → фактор ближе к 1.
/// С tileHorizontal: в рантайме создаёт 3 копии слоя и бесшовно прокручивает их по X
///   (камера по X залочена на родителя — тайлы всегда покрывают кадр; параллакс по X идёт через
///   смещение тайлов). Для НЕвидимых швов спрайт должен быть бесшовным по горизонтали.
/// Z не трогается (порядок отрисовки). Факторы крутятся в инспекторе/Play.
/// </summary>
[ExecuteAlways]
public class ParallaxLayer : MonoBehaviour
{
    [Tooltip("0 = неподвижен в мире, 1 = приклеен к камере. X и Y раздельно.")]
    public Vector2 parallaxFactor = new Vector2(0.7f, 0.9f);

    [Tooltip("Бесконечный горизонтальный тайлинг (3 копии). Спрайт должен быть бесшовным по X.")]
    public bool tileHorizontal = false;

    private Transform   _cam;
    private Vector3     _startPos;
    private Vector3     _camStart;
    private bool        _init;
    private Transform[] _tiles;
    private float       _tileWLocal;

    private void OnEnable() => Init();

    private void Init()
    {
        var c = Camera.main;
        if (c == null) { _init = false; return; }
        _cam      = c.transform;
        _startPos = transform.position;
        _camStart = _cam.position;

        if (tileHorizontal && Application.isPlaying && _tiles == null)
            SetupTiles();

        _init = true;
    }

    private void SetupTiles()
    {
        var sr = GetComponent<SpriteRenderer>();
        if (sr == null || sr.sprite == null) return;

        _tileWLocal = sr.sprite.rect.width / sr.sprite.pixelsPerUnit; // ширина тайла в ЛОКАЛЬНЫХ единицах
        _tiles = new Transform[3];
        for (int i = 0; i < 3; i++)
        {
            var go  = new GameObject("Tile" + i);
            var t   = go.transform;
            t.SetParent(transform, false);
            var tsr = go.AddComponent<SpriteRenderer>();
            tsr.sprite       = sr.sprite;
            tsr.sortingOrder = sr.sortingOrder;
            tsr.sortingLayerID = sr.sortingLayerID;
            tsr.color        = sr.color;
            _tiles[i] = t;
        }
        sr.enabled = false; // собственный рендер выключаем — рисуют тайлы
    }

    private void LateUpdate()
    {
        if (!_init || _cam == null) { Init(); if (!_init) return; }

        if (tileHorizontal && Application.isPlaying && _tiles != null && _tiles.Length >= 3
            && _tiles[0] != null && _tiles[1] != null && _tiles[2] != null)
        {
            // Родитель: по X — вслед за камерой (тайлы всегда покрывают кадр), по Y — параллакс.
            var p = transform.position;
            p.x = _startPos.x + (_cam.position.x - _camStart.x);
            p.y = _startPos.y + (_cam.position.y - _camStart.y) * parallaxFactor.y;
            transform.position = p;

            // Прокрутка тайлов внутри родителя (даёт параллакс по X), бесшовный wrap каждые _tileWLocal.
            float scrollLocal = (_cam.position.x - _camStart.x) * (1f - parallaxFactor.x)
                              / Mathf.Max(0.0001f, transform.lossyScale.x);
            float baseOff = -Mathf.Repeat(scrollLocal, _tileWLocal);
            for (int i = 0; i < 3; i++)
                _tiles[i].localPosition = new Vector3(baseOff + (i - 1) * _tileWLocal, 0f, 0f);
        }
        else
        {
            Vector3 d = _cam.position - _camStart;
            var p = transform.position;
            p.x = _startPos.x + d.x * parallaxFactor.x;
            p.y = _startPos.y + d.y * parallaxFactor.y;
            transform.position = p;   // z не меняем
        }
    }
}
