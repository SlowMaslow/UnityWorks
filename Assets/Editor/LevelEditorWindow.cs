using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System.IO;

/// <summary>
/// Редактор уровней ClimbUp.
/// Открыть: Tools → Level Editor  (или Ctrl+Shift+E)
/// Полностью исключён из билдов — папка Assets/Editor.
/// </summary>
public class LevelEditorWindow : EditorWindow
{
    // ─── Инструменты ──────────────────────────────────────────────────────────
    private enum Tool
    {
        Select,
        Platform, PlatformWall,
        Coin, Star,
        FinLeft, FinRight,
        SpawnPoint,
        Tile
    }

    private static readonly string[] ToolLabels =
    {
        "🔲 Select",
        "⬛ Platform", "▎PlatformWall",
        "🪙 Coin", "⭐ Star",
        "🏁 FinLeft", "🏁 FinRight",
        "📍 SpawnPoint",
        "🧱 Tile"
    };

    private static readonly Color[] ToolColors =
    {
        Color.white,
        new Color(0.4f, 0.4f, 0.4f), new Color(0.35f, 0.35f, 0.45f),
        new Color(1f, 0.8f, 0.1f),   new Color(1f, 0.85f, 0.1f),
        new Color(0.2f, 0.8f, 0.3f), new Color(0.2f, 0.8f, 0.3f),
        new Color(1f, 0.4f, 0.4f),
        new Color(0.55f, 0.5f, 0.45f)
    };

    // ─── State ────────────────────────────────────────────────────────────────
    private Tool    _tool     = Tool.Select;
    private bool    _snapGrid  = true;
    private bool    _snapMove  = false;
    private bool    _showGrid  = true;  // отрисовка сетки в Scene View
    private float   _grid      = 0.25f;
    // Pivot: (0=left/bottom, 0.5=center, 1=right/top)
    // Center snap (0.5,0.5) = старое поведение без смещения
    private float   _pivotX   = 0f; // default: left
    private float   _pivotY   = 0f; // default: bottom → bottom-left corner
    private float   _platW    = 3f;
    private string  _levelName;
    private Vector2 _scroll;

    // Для загрузки существующих уровней
    private string[] _existingLevels = {};
    private int      _selectedLevel  = 0;
    private string   _loadedPrefabPath; // путь откуда загрузили (для сохранения)

    // ─── Scene ────────────────────────────────────────────────────────────────
    private GameObject _root;          // корень текущего уровня в сцене
    private Vector3    _previewPos;
    private bool       _hovering;

    // ─── Prefabs ──────────────────────────────────────────────────────────────
    private GameObject _pfPlatform, _pfWall, _pfCoin, _pfStar;
    private GameObject _pfBgWall, _pfFallCollider, _pfLeftFin, _pfRightFin;
    private GameObject _pfTile;

    // ─── Tiles ────────────────────────────────────────────────────────────────
    private Sprite[]   _tileSprites = {};
    private int        _selTile = 0;
    private Vector2    _tileScroll;
    private float _tileCell = 0.56f; // шаг сетки тайлов (редактируется в SETTINGS). < размера тайла (0.58) = лёгкое перекрытие, плотные швы
    private bool  _tileGridView = true; // показывать тайловую сетку (по галочке) в ЛЮБОМ инструменте

    // ─── Menu ─────────────────────────────────────────────────────────────────
    [MenuItem("Tools/Level Editor %#e")]
    public static void ShowWindow()
    {
        var w = GetWindow<LevelEditorWindow>("Level Editor");
        w.minSize = new Vector2(290, 520);
    }

    // ─── Unity Editor callbacks ───────────────────────────────────────────────
    private void OnEnable()
    {
        SceneView.duringSceneGui += OnSceneGUI;
        UnityEditor.SceneManagement.PrefabStage.prefabStageOpened += OnPrefabStageOpened;
        EditorSceneManager.sceneClosing += OnSceneClosing;
        LoadPrefabs();
        AutoLevelName();
        RefreshExistingLevels();
    }

    private void OnDisable()
    {
        SceneView.duringSceneGui -= OnSceneGUI;
        UnityEditor.SceneManagement.PrefabStage.prefabStageOpened -= OnPrefabStageOpened;
        EditorSceneManager.sceneClosing -= OnSceneClosing;
        _tool = Tool.Select;
    }

    // Уход в ПРЕФАБ или закрытие СЦЕНЫ с загруженным уровнем → предложить сохранить и ВЫГРУЗИТЬ
    // уровень (грид/инструменты в сцене отключаются, т.к. рисуются только при _root != null).
    private void OnPrefabStageOpened(UnityEditor.SceneManagement.PrefabStage stage)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || _root == null) return;
        PromptSaveLevel();
        DestroyImmediate(_root);
        _root = null;
        Repaint();
    }

    private void OnSceneClosing(UnityEngine.SceneManagement.Scene scene, bool removing)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || _root == null || _root.scene != scene) return;
        PromptSaveLevel();
        _root = null;   // сцена закрывается — объект уничтожится сам
        Repaint();
    }

    private void PromptSaveLevel()
    {
        if (_root == null) return;
        if (EditorUtility.DisplayDialog("Level Editor",
                $"Уровень '{_levelName}' будет выгружен.\nСохранить изменения в Level_XX.prefab?",
                "Сохранить", "Не сохранять"))
            SaveLevel();
    }

    private void OnGUI()
    {
        DrawHeader();
        GUILayout.Space(4);
        DrawToolPalette();
        GUILayout.Space(4);
        DrawSettings();
        GUILayout.Space(4);
        DrawLevelActions();
        GUILayout.Space(4);
        DrawObjectList();
    }

    // ─── GUI sections ─────────────────────────────────────────────────────────
    private void DrawHeader()
    {
        using (new GUILayout.HorizontalScope(EditorStyles.toolbar))
        {
            GUILayout.Label("🎮  LEVEL EDITOR", EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            if (_root != null)
                GUILayout.Label($"▶ {_root.name}", EditorStyles.miniLabel);
        }
    }

    private void DrawToolPalette()
    {
        GUILayout.Label("TOOLS", EditorStyles.boldLabel);

        using (new GUILayout.HorizontalScope())
        {
            DrawToolGroup(0, 1); // Select (0)
            DrawToolGroup(1, 2); // Platform, PlatformWall (1,2)
        }
        using (new GUILayout.HorizontalScope())
        {
            DrawToolGroup(3, 2); // Coin, Star (3,4)
            DrawToolGroup(5, 2); // FinLeft, FinRight (5,6)
        }
        using (new GUILayout.HorizontalScope())
        {
            DrawToolGroup(7, 1); // SpawnPoint (7)
            DrawToolGroup(8, 1); // Tile (8)
        }
    }

    private void DrawToolGroup(int startIdx, int count)
    {
        for (int i = startIdx; i < startIdx + count && i < ToolLabels.Length; i++)
        {
            Tool t    = (Tool)i;
            bool sel  = _tool == t;
            var  prev = GUI.backgroundColor;
            GUI.backgroundColor = sel ? new Color(0.5f, 0.8f, 1f) : ToolColors[i] * 0.9f;

            if (GUILayout.Button(ToolLabels[i], GUILayout.Height(28)))
            {
                _tool = sel ? Tool.Select : t;
                SceneView.RepaintAll();
            }
            GUI.backgroundColor = prev;
        }
    }

    private void DrawSettings()
    {
        GUILayout.Label("SETTINGS", EditorStyles.boldLabel);
        _snapGrid  = EditorGUILayout.Toggle("Snap on place", _snapGrid);
        _snapMove  = EditorGUILayout.Toggle("Snap on move",  _snapMove);
        _showGrid  = EditorGUILayout.Toggle("Show grid",     _showGrid);
        if (_showGrid)
            _tileGridView = EditorGUILayout.Toggle(
                new GUIContent("Tile grid", "Рисовать грид по тайловой клетке (по границам клеток) в ЛЮБОМ инструменте. Выкл — обычный Grid size."),
                _tileGridView);
        if (_showGrid && _tileGridView)
            _tileCell = EditorGUILayout.Slider(
                new GUIContent("Tile cell", "Шаг тайловой сетки (постановка + snap-move + грид)."),
                _tileCell, 0.3f, 2f);
        if (_snapGrid || _snapMove)
        {
            _grid = EditorGUILayout.Slider("Grid size", _grid, 0.25f, 2f);
            // Подсказка: толщина платформы/стены = 0.25 — используй кратные значения
            GUI.color = new Color(1f, 0.9f, 0.5f);
            GUILayout.Label("ℹ Platform/Wall thickness = 0.25\n  Рекомендуется: 0.25, 0.5, 1.0",
                EditorStyles.helpBox);
            GUI.color = Color.white;
        }

        GUILayout.Space(4);
        GUILayout.Label("Pivot (точка привязки):", EditorStyles.miniLabel);
        DrawPivotSelector();

        if (_tool == Tool.Platform || _tool == Tool.PlatformWall)
        {
            string label = _tool == Tool.Platform ? "Width" : "Height";
            // Размер кратен сетке: показываем в клетках, значение = клетки × grid
            int cells = Mathf.Max(1, Mathf.RoundToInt(_platW / _grid));
            cells  = EditorGUILayout.IntSlider(
                $"{label}  ({cells * _grid:F2} u)", cells, 1, 24);
            _platW = cells * _grid;
        }

        if (_tool == Tool.Tile)
            DrawTilePalette();
    }

    private void DrawTilePalette()
    {
        GUILayout.Space(4);
        GUILayout.Label("TILE PALETTE", EditorStyles.boldLabel);
        if (_tileSprites == null || _tileSprites.Length == 0)
        {
            EditorGUILayout.HelpBox("Нет спрайтов в Assets/Sprites/Tileset", MessageType.Info);
            if (GUILayout.Button("↻ Reload tiles")) LoadTiles();
            return;
        }

        const int   cols = 4;
        const float sz   = 58f;
        _tileScroll = GUILayout.BeginScrollView(_tileScroll, GUILayout.Height(190));
        for (int i = 0; i < _tileSprites.Length; i++)
        {
            if (i % cols == 0) GUILayout.BeginHorizontal();

            // НЕ используем AssetPreview.GetAssetPreview — он генерит превью АСИНХРОННО и заставляет
            // окно перерисовываться БЕСКОНЕЧНО, пока превью «грузятся»/инвалидируются (особенно после
            // входа/выхода из префаба = сброс кэша превью) — это и есть петля. Спрайт тайла = своя
            // текстура (512²), берём её напрямую: всегда готова, без асинхронной генерации.
            Texture tex = _tileSprites[i].texture;

            var prev = GUI.backgroundColor;
            if (i == _selTile) GUI.backgroundColor = new Color(0.4f, 0.8f, 1f);
            if (GUILayout.Button(new GUIContent(tex), GUILayout.Width(sz), GUILayout.Height(sz)))
            {
                _selTile = i;
                SceneView.RepaintAll();
            }
            GUI.backgroundColor = prev;

            if (i % cols == cols - 1 || i == _tileSprites.Length - 1) GUILayout.EndHorizontal();
        }
        GUILayout.EndScrollView();
        GUILayout.Label($"Selected: {_tileSprites[_selTile].name}", EditorStyles.centeredGreyMiniLabel);
        if (GUILayout.Button("↻ Reload tiles")) LoadTiles();
    }

    private void DrawLevelActions()
    {
        GUILayout.Label("LEVEL", EditorStyles.boldLabel);

        // ── Загрузка существующего уровня ────────────────────────────────────
        if (_existingLevels.Length > 0)
        {
            GUILayout.Label("Load existing:", EditorStyles.miniLabel);
            using (new GUILayout.HorizontalScope())
            {
                _selectedLevel = EditorGUILayout.Popup(_selectedLevel, _existingLevels);
                if (GUILayout.Button("📂 Load", GUILayout.Width(70), GUILayout.Height(22)))
                    LoadLevel(_existingLevels[_selectedLevel]);
            }
            GUILayout.Space(4);
        }

        // ── Создать новый уровень ─────────────────────────────────────────────
        _levelName = EditorGUILayout.TextField("New level name", _levelName);
        using (new GUILayout.HorizontalScope())
        {
            if (GUILayout.Button("➕ New Level", GUILayout.Height(28)))
                CreateNewLevel();

            GUI.enabled = _root != null;
            var prev = GUI.backgroundColor;
            GUI.backgroundColor = new Color(0.3f, 0.8f, 0.4f);
            if (GUILayout.Button("💾 Save", GUILayout.Height(28)))
                SaveLevel();
            GUI.backgroundColor = prev;
            GUI.enabled = true;
        }

        if (_root != null)
        {
            GUI.color = new Color(1f, 0.9f, 0.5f);
            string suffix = string.IsNullOrEmpty(_loadedPrefabPath) ? " (новый)" : " (загружен)";
            GUILayout.Label($"▶ {_root.name}{suffix}", EditorStyles.centeredGreyMiniLabel);
            GUI.color = Color.white;
        }
    }

    private void DrawObjectList()
    {
        if (_root == null) return;

        GUILayout.Label("OBJECTS", EditorStyles.boldLabel);
        _scroll = GUILayout.BeginScrollView(_scroll, GUILayout.ExpandHeight(true));

        DrawGroupFoldout("Platforms",  _root.transform.Find("Platforms"));
        DrawGroupFoldout("Walls",      _root.transform.Find("Walls"));
        DrawGroupFoldout("Coins",      _root.transform.Find("Coins"));
        DrawGroupFoldout("Stars",      _root.transform.Find("Stars"));
        DrawGroupFoldout("Tiles",      _root.transform.Find("Tiles"));

        // Одиночные объекты
        foreach (Transform t in _root.transform)
        {
            if (t.childCount == 0 || IsGroup(t.name)) continue;
            DrawObjectRow(t.gameObject);
        }

        GUILayout.EndScrollView();

        int total = _root.transform.childCount;
        GUILayout.Label($"Total root objects: {total}", EditorStyles.centeredGreyMiniLabel);
    }

    private void DrawGroupFoldout(string name, Transform group)
    {
        if (group == null || group.childCount == 0) return;
        GUILayout.Label($"  {name} ({group.childCount})", EditorStyles.miniBoldLabel);
        foreach (Transform child in group)
            DrawObjectRow(child.gameObject, "    ");
    }

    private void DrawObjectRow(GameObject go, string prefix = "  ")
    {
        using (new GUILayout.HorizontalScope())
        {
            bool sel = Selection.activeGameObject == go;
            if (GUILayout.Toggle(sel, $"{prefix}{go.name}", EditorStyles.miniButton,
                GUILayout.ExpandWidth(true)) && !sel)
            {
                Selection.activeGameObject = go;
                // Пингуем в Hierarchy и фокусируем Scene View
                EditorGUIUtility.PingObject(go);
                SceneView.lastActiveSceneView?.Frame(
                    new Bounds(go.transform.position, Vector3.one * 3f), false);
            }

            var prev = GUI.backgroundColor;
            GUI.backgroundColor = new Color(1f, 0.4f, 0.4f);
            if (GUILayout.Button("✕", GUILayout.Width(22)))
            {
                Undo.DestroyObjectImmediate(go);
                EditorSceneManager.MarkSceneDirty(
                    UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            }
            GUI.backgroundColor = prev;
        }
    }

    // ─── Scene GUI ────────────────────────────────────────────────────────────
    private void OnSceneGUI(SceneView sv)
    {
        DrawGrid(sv);
        // ── Snap on move: кастомный handle с реальным snap во время drag ──────
        if (_snapMove && _root != null && Selection.activeTransform != null
            && _tool == Tool.Select
            && IsPartOfLevel(Selection.activeTransform.gameObject))
        {
            var t = Selection.activeTransform;

            // Скрываем стандартный Unity handle, рисуем свой
            Tools.hidden = true;

            EditorGUI.BeginChangeCheck();
            var newPos = Handles.PositionHandle(t.position, Quaternion.identity);
            if (EditorGUI.EndChangeCheck())
            {
                // Тайлы снапятся по СВОЕЙ сетке _tileCell (по ЦЕНТРУ — как при размещении);
                // остальные объекты — по PIVOT (угол/ребро) к общей сетке _grid.
                bool isTile = t.parent != null && t.parent.name == "Tiles";
                Vector3 snapped;
                if (isTile)
                {
                    snapped = new Vector3(
                        Mathf.Round(newPos.x / _tileCell) * _tileCell,
                        Mathf.Round(newPos.y / _tileCell) * _tileCell,
                        t.position.z);
                }
                else
                {
                    var pivotOffset = new Vector3(
                        (0.5f - _pivotX) * t.localScale.x,
                        (0.5f - _pivotY) * t.localScale.y,
                        0f);
                    var pivotInWorld = newPos - pivotOffset;
                    var snappedPivot = new Vector3(
                        Mathf.Round(pivotInWorld.x / _grid) * _grid,
                        Mathf.Round(pivotInWorld.y / _grid) * _grid,
                        t.position.z);
                    snapped = snappedPivot + pivotOffset;
                }

                // Дельта снапнутого перемещения активного объекта — применяем ко ВСЕМ выделенным
                // объектам уровня (множественное перемещение по сетке, относит. позиции сохраняются).
                Vector3 delta = snapped - t.position;
                var selT = Selection.transforms;
                Undo.RecordObjects(selT, "Move (Snapped)");
                foreach (var st in selT)
                    if (IsPartOfLevel(st.gameObject))
                        st.position += delta;

                EditorSceneManager.MarkSceneDirty(
                    UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            }
        }
        else
        {
            // Для инструментов РАЗМЕЩЕНИЯ (Tile/Platform/…) прячем стандартный гизмо — иначе активный
            // Move/Rect-тул Unity перехватывает клик (рамка-выделение) и объект НЕ ставится.
            // Для Select (snap off) — стандартные handles видны.
            Tools.hidden = (_tool != Tool.Select);
        }

        if (_tool == Tool.Select || _root == null) return;

        var e = Event.current;
        _hovering = false;

        // Позиция курсора в мировых координатах (плоскость Z=0)
        var ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);
        if (ray.direction.z != 0f)
        {
            float t   = -ray.origin.z / ray.direction.z;
            var   pos = ray.origin + ray.direction * t;
            pos.z     = 0f;
            if (_tool == Tool.Tile)
            {
                pos.x = Mathf.Round(pos.x / _tileCell) * _tileCell;
                pos.y = Mathf.Round(pos.y / _tileCell) * _tileCell;
            }
            else if (_snapGrid || _snapMove) pos = Snap(pos);
            _previewPos = pos;
            _hovering   = true;
        }

        // Ghost preview
        if (_hovering)
        {
            DrawPreviewGhost(_previewPos);
            HandleUtility.AddDefaultControl(GUIUtility.GetControlID(FocusType.Passive));
        }

        // Клик — размещаем объект
        if (_hovering && e.type == EventType.MouseDown && e.button == 0 && !e.alt)
        {
            PlaceObject(_previewPos);
            e.Use();
        }

        // ПКМ — удалить объект под курсором (в 2D-режиме ПКМ свободна; быстрое удаление при постройке)
        if (_hovering && e.type == EventType.MouseDown && e.button == 1 && !e.alt)
        {
            Vector3 raw = _previewPos;
            var dray = HandleUtility.GUIPointToWorldRay(e.mousePosition);
            if (dray.direction.z != 0f) { float dt = -dray.origin.z / dray.direction.z; raw = dray.origin + dray.direction * dt; raw.z = 0f; }
            DeleteAtCursor(raw);
            e.Use();
        }

        // ESC — возврат в Select
        if (e.type == EventType.KeyDown && e.keyCode == KeyCode.Escape)
        {
            _tool = Tool.Select;
            Repaint();
            e.Use();
        }

        // Repaint только когда активен инструмент размещения (показываем ghost).
        if (_hovering)
            sv.Repaint();
    }

    private void DrawPreviewGhost(Vector3 pos)
    {
        Handles.color = new Color(0.5f, 1f, 0.5f, 0.6f);

        switch (_tool)
        {
            case Tool.Platform:
            {
                float h = _pfPlatform != null ? _pfPlatform.transform.localScale.y : 0.25f;
                Handles.DrawWireCube(pos + GetPivotOffset(_platW, h),
                    new Vector3(_platW, h, 0.3f));
                break;
            }
            case Tool.PlatformWall:
            {
                float w = _pfWall != null ? _pfWall.transform.localScale.y : 0.25f;
                Handles.DrawWireCube(pos + GetPivotOffset(w, _platW),
                    new Vector3(w, _platW, 0.3f));
                break;
            }
            case Tool.Coin:
            case Tool.Star:
                Handles.DrawWireDisc(pos, Vector3.forward, 0.35f);
                break;
            case Tool.FinLeft:
            case Tool.FinRight:
                Handles.color = new Color(0.2f, 1f, 0.4f, 0.7f);
                Handles.DrawWireDisc(pos, Vector3.forward, 0.5f);
                break;
            case Tool.SpawnPoint:
                Handles.color = new Color(1f, 0.4f, 0.4f, 0.8f);
                Handles.DrawSolidDisc(pos, Vector3.forward, 0.2f);
                Handles.DrawWireDisc(pos, Vector3.forward, 0.5f);
                break;
            case Tool.Tile:
                Handles.color = new Color(0.5f, 1f, 0.5f, 0.7f);
                Handles.DrawWireCube(pos, new Vector3(_tileCell, _tileCell, 0.1f));
                break;
        }

        Handles.color = Color.white;
        Handles.Label(pos + Vector3.up * 0.6f,
            $"  {ToolLabels[(int)_tool]}  ({pos.x:F1}, {pos.y:F1})",
            EditorStyles.whiteLabel);
    }

    // ─── Placement ────────────────────────────────────────────────────────────
    private void PlaceObject(Vector3 pos)
    {
        GameObject go = null;

        switch (_tool)
        {
            case Tool.Platform:
            {
                var s   = _pfPlatform != null ? _pfPlatform.transform.localScale : Vector3.one;
                float w = _platW, h = s.y;
                go = PlaceFromPrefab(_pfPlatform, pos + GetPivotOffset(w, h),
                    GetGroup("Platforms"), "Platform");
                if (go != null)
                    go.transform.localScale = new Vector3(w, h, s.z);
                break;
            }
            case Tool.PlatformWall:
            {
                var s   = _pfWall != null ? _pfWall.transform.localScale : Vector3.one;
                // PlatformWall.prefab имеет rotation (0,0,90), поэтому локальные X/Y
                // меняются местами в визуале. Сетим scale=(длина, толщина, z),
                // что после поворота даст визуально (толщина, длина) = вертикальная стена.
                float thickness = s.y;
                float length    = _platW;
                float w = thickness, h = length; // для preview / GetPivotOffset (визуальные размеры)
                go = PlaceFromPrefab(_pfWall, pos + GetPivotOffset(w, h),
                    GetGroup("Walls"), "PlatformWall");
                if (go != null)
                    go.transform.localScale = new Vector3(length, thickness, s.z);
                break;
            }

            case Tool.Coin:
                go = PlaceFromPrefab(_pfCoin, pos, GetGroup("Coins"), "Coin");
                break;

            case Tool.Star:
                go = PlaceFromPrefab(_pfStar, pos, GetGroup("Stars"), "Star");
                break;

            case Tool.FinLeft:
                go = PlaceFinZone(_pfLeftFin, pos, "LeftFin", "LeftPad");
                break;

            case Tool.FinRight:
                go = PlaceFinZone(_pfRightFin, pos, "RightFin", "RightPad");
                break;

            case Tool.SpawnPoint:
                go = PlaceSpawnPoint(pos);
                break;

            case Tool.Tile:
            {
                if (_pfTile == null || _tileSprites == null || _tileSprites.Length == 0)
                {
                    Debug.LogWarning("[LevelEditor] Tile prefab или спрайты не найдены (Assets/Sprites/Tileset)");
                    break;
                }
                var grp = GetGroup("Tiles");
                go = (GameObject)PrefabUtility.InstantiatePrefab(_pfTile, grp);
                go.transform.position = new Vector3(pos.x, pos.y, grp != null ? grp.position.z : 0f);
                var sr = go.GetComponent<SpriteRenderer>();
                if (sr != null) sr.sprite = _tileSprites[_selTile];
                go.name = UniqueChildName(grp, _tileSprites[_selTile].name);
                break;
            }
        }

        if (go != null)
        {
            Undo.RegisterCreatedObjectUndo(go, $"Place {_tool}");
            Selection.activeGameObject = go;
            EditorSceneManager.MarkSceneDirty(
                UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            // Принудительно обновляем Scene View — без этого объект может не появиться
            SceneView.RepaintAll();
            EditorApplication.QueuePlayerLoopUpdate();
        }

        Repaint();
    }

    // Удаляет объект уровня под курсором (ПКМ). Находит ближайший рендерер, чьи XY-границы
    // накрывают точку, поднимается до объекта-ребёнка группы и удаляет (с Undo). Игрока не трогает.
    private void DeleteAtCursor(Vector3 world)
    {
        if (_root == null) return;
        GameObject best = null; float bestDist = float.MaxValue;
        foreach (var r in _root.GetComponentsInChildren<Renderer>())
        {
            var b = r.bounds;
            if (world.x < b.min.x || world.x > b.max.x || world.y < b.min.y || world.y > b.max.y) continue;
            float d = ((Vector2)(b.center - world)).sqrMagnitude;
            if (d < bestDist) { bestDist = d; best = r.gameObject; }
        }
        if (best == null) return;
        // Поднимаемся до объекта, лежащего прямо в группе (а не его дочернего меша/спрайта).
        Transform t = best.transform;
        while (t.parent != null && t.parent != _root.transform && !IsGroup(t.parent.name))
            t = t.parent;
        var go = t.gameObject;
        if (go.name.Contains("Player")) return;   // игрока не удаляем
        Undo.DestroyObjectImmediate(go);
        EditorSceneManager.MarkSceneDirty(
            UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        SceneView.RepaintAll();
        Repaint();
    }

    private GameObject PlaceFromPrefab(GameObject prefab, Vector3 pos,
        Transform parent, string baseName)
    {
        if (prefab == null) { Debug.LogWarning($"Prefab for {_tool} not found!"); return null; }

        // PrefabUtility.InstantiatePrefab — сохраняет связь с source prefab.
        // Любые правки в source-префабе (Platform.prefab, Coin.prefab и т.д.)
        // автоматически подтянутся в сохранённые уровни.
        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
        // Z берём от родителя — чтобы localPosition.z = 0 внутри группы
        go.transform.position = new Vector3(pos.x, pos.y,
            parent != null ? parent.position.z : 0f);
        go.name = UniqueChildName(parent, baseName);
        return go;
    }

    private GameObject PlaceFinZone(GameObject prefab, Vector3 pos, string finName, string padTag)
    {
        if (prefab == null) { Debug.LogWarning($"{finName} prefab не найден!"); return null; }

        // Если уже есть — просто перемещаем
        var existing = _root.transform.Find(finName)?.gameObject;
        if (existing != null)
        {
            Undo.RecordObject(existing.transform, $"Move {finName}");
            existing.transform.position = pos;
            return existing;
        }

        // Инстанциируем как prefab instance (со связью к source)
        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, _root.transform);
        go.transform.position = pos;
        go.name = finName;

        // Убеждаемся что padTag выставлен корректно (override на инстансе)
        var wc = go.GetComponent<WinCollider>();
        if (wc != null)
        {
            var so = new SerializedObject(wc);
            so.FindProperty("padTag").stringValue = padTag;
            so.ApplyModifiedProperties();
        }

        return go;
    }

    private GameObject PlaceSpawnPoint(Vector3 pos)
    {
        GameObject sp;
        var existing = _root.transform.Find("SpawnPoint")?.gameObject;
        if (existing != null)
        {
            Undo.RecordObject(existing.transform, "Move SpawnPoint");
            existing.transform.position = pos;
            sp = existing;
        }
        else
        {
            sp = new GameObject("SpawnPoint");
            sp.transform.SetParent(_root.transform, false);
            sp.transform.position = pos;
        }

        // Player всегда следует за SpawnPoint (z = -0.4 — стандартная глубина)
        var player = _root.transform.Find("Player");
        if (player != null)
        {
            Undo.RecordObject(player, "Move Player with SpawnPoint");
            player.position = new Vector3(pos.x, pos.y, -0.4f);
        }

        return sp;
    }

    // ─── Level create / save ──────────────────────────────────────────────────
    private void CreateNewLevel()
    {
        if (_root != null &&
            !EditorUtility.DisplayDialog("New Level",
                "Текущий уровень будет закрыт. Продолжить?", "Да", "Отмена"))
            return;

        // Удаляем старый root из сцены (не из prefab)
        if (_root != null) DestroyImmediate(_root);

        _root = new GameObject(_levelName);

        // Создаём группы
        new GameObject("Platforms").transform.SetParent(_root.transform, false);
        new GameObject("Walls").transform.SetParent(_root.transform, false);
        new GameObject("Coins").transform.SetParent(_root.transform, false);
        new GameObject("Stars").transform.SetParent(_root.transform, false);

        // BackGroundWall
        if (_pfBgWall != null)
        {
            var bg = (GameObject)PrefabUtility.InstantiatePrefab(_pfBgWall, _root.transform);
            bg.name = "BackGroundWall";
        }

        // FallCollider
        if (_pfFallCollider != null)
        {
            var fc = (GameObject)PrefabUtility.InstantiatePrefab(_pfFallCollider, _root.transform);
            fc.name = "FallCollider";
            fc.transform.localPosition = new Vector3(0f, -3f, 0f);
        }

        // SpawnPoint по умолчанию
        var sp = new GameObject("SpawnPoint");
        sp.transform.SetParent(_root.transform, false);
        sp.transform.position = new Vector3(0f, 1f, 0f);

        // Player по умолчанию на SpawnPoint — как prefab instance со связью
        var playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player.prefab");
        if (playerPrefab != null)
        {
            var pl = (GameObject)PrefabUtility.InstantiatePrefab(playerPrefab, _root.transform);
            pl.transform.position = new Vector3(0f, 0.9f, -0.4f);
            pl.name = "Player";
        }

        Undo.RegisterCreatedObjectUndo(_root, "Create Level");
        EditorSceneManager.MarkSceneDirty(
            UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        Selection.activeGameObject = _root;
        SceneView.FrameLastActiveSceneView();

        Debug.Log($"[LevelEditor] Создан новый уровень: {_levelName}");
        Repaint();
    }

    private void LoadLevel(string levelName)
    {
        if (_root != null &&
            !EditorUtility.DisplayDialog("Загрузить уровень",
                "Текущий уровень будет закрыт. Продолжить?", "Да", "Отмена"))
            return;

        string path = $"Assets/Resources/Levels/{levelName}.prefab";
        var prefab  = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (prefab == null) { Debug.LogError($"Prefab не найден: {path}"); return; }

        // Удаляем старый root
        if (_root != null) DestroyImmediate(_root);

        // Инстанциируем prefab в сцену для редактирования.
        // НЕ распаковываем — оставляем вложенные prefab instances связанными
        // со своими source prefabs. Это критично: при правках в Player.prefab,
        // Platform.prefab и т.д. изменения подтянутся в этот уровень.
        _root = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
        _loadedPrefabPath = path;
        _levelName = levelName;

        Selection.activeGameObject = _root;
        SceneView.FrameLastActiveSceneView();
        EditorSceneManager.MarkSceneDirty(
            UnityEngine.SceneManagement.SceneManager.GetActiveScene());

        Debug.Log($"[LevelEditor] Загружен: {path}");
        Repaint();
    }

    private void SaveLevel()
    {
        if (_root == null) return;

        // Если загружали — сохраняем по тому же пути; иначе по _levelName
        string dir  = "Assets/Resources/Levels";
        string path = !string.IsNullOrEmpty(_loadedPrefabPath)
            ? _loadedPrefabPath
            : $"{dir}/{_levelName}.prefab";

        if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

        bool isNew = !File.Exists(path);
        if (!isNew && string.IsNullOrEmpty(_loadedPrefabPath) &&
            !EditorUtility.DisplayDialog("Перезаписать?",
                $"{path} уже существует. Перезаписать?", "Да", "Отмена"))
            return;

        PrefabUtility.SaveAsPrefabAsset(_root, path);
        AssetDatabase.Refresh();
        RefreshExistingLevels();

        // Выгружаем уровень из сцены — он сохранён в префаб, в сцене больше не нужен.
        Object.DestroyImmediate(_root);
        _root              = null;
        _loadedPrefabPath  = null;
        _tool              = Tool.Select;
        AutoLevelName();

        EditorSceneManager.MarkSceneDirty(
            UnityEngine.SceneManagement.SceneManager.GetActiveScene());

        EditorUtility.DisplayDialog("Сохранено!", $"Уровень сохранён:\n{path}", "OK");
        Debug.Log($"[LevelEditor] Сохранён и выгружен из сцены: {path}");
        Repaint();
    }

    private void RefreshExistingLevels()
    {
        string dir = "Assets/Resources/Levels";
        if (!Directory.Exists(dir)) { _existingLevels = new string[0]; return; }

        var files = Directory.GetFiles(dir, "Level_*.prefab");
        _existingLevels = new string[files.Length];
        for (int i = 0; i < files.Length; i++)
            _existingLevels[i] = Path.GetFileNameWithoutExtension(files[i]);
        System.Array.Sort(_existingLevels);
    }

    // ─── Helpers ──────────────────────────────────────────────────────────────
    private void LoadPrefabs()
    {
        _pfPlatform     = Load("Assets/Prefabs/Platform.prefab");
        _pfWall         = Load("Assets/Prefabs/PlatformWall.prefab");
        _pfCoin         = Load("Assets/Prefabs/Coin.prefab");
        _pfStar         = Load("Assets/Prefabs/Star.prefab");
        _pfBgWall       = Load("Assets/Prefabs/BackGroundWall.prefab");
        _pfFallCollider = Load("Assets/Prefabs/FallCollider.prefab");
        _pfLeftFin      = Load("Assets/Prefabs/LeftFin.prefab");
        _pfRightFin     = Load("Assets/Prefabs/RightFin.prefab");
        _pfTile         = Load("Assets/Prefabs/Tile.prefab");
        LoadTiles();
    }

    private void LoadTiles()
    {
        var guids = AssetDatabase.FindAssets("t:Sprite", new[] { "Assets/Sprites/Tileset" });
        var list  = new System.Collections.Generic.List<Sprite>();
        foreach (var g in guids)
        {
            var s = AssetDatabase.LoadAssetAtPath<Sprite>(AssetDatabase.GUIDToAssetPath(g));
            if (s != null) list.Add(s);
        }
        list.Sort((a, b) => string.Compare(a.name, b.name, System.StringComparison.Ordinal));
        _tileSprites = list.ToArray();
        if (_tileSprites.Length > 0) _selTile = Mathf.Clamp(_selTile, 0, _tileSprites.Length - 1);
    }

    private static GameObject Load(string path)
        => AssetDatabase.LoadAssetAtPath<GameObject>(path);

    private Transform GetGroup(string groupName)
    {
        var t = _root?.transform.Find(groupName);
        if (t == null && _root != null)
        {
            var go = new GameObject(groupName);
            go.transform.SetParent(_root.transform, false);
            t = go.transform;
        }
        return t;
    }

    private static string UniqueChildName(Transform parent, string baseName)
    {
        if (parent == null) return baseName;
        int n = 0;
        foreach (Transform c in parent)
            if (c.name.StartsWith(baseName)) n++;
        return n == 0 ? baseName : $"{baseName} ({n})";
    }

    private Vector3 Snap(Vector3 pos)
    {
        pos.x = Mathf.Round(pos.x / _grid) * _grid;
        pos.y = Mathf.Round(pos.y / _grid) * _grid;
        return pos;
    }

    // ─── Grid drawing ─────────────────────────────────────────────────────────
    private void DrawGrid(SceneView sv)
    {
        // Грид рисуем ТОЛЬКО когда есть активно загруженный уровень — иначе редактор пассивен в сцене.
        if (!_showGrid || _root == null) return;

        var cam = sv.camera;

        // Определяем видимую область в мировых координатах (плоскость Z=0)
        float dist   = Mathf.Abs(cam.transform.position.z);
        float halfH  = cam.orthographic
            ? cam.orthographicSize
            : dist * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
        float halfW  = halfH * cam.aspect;

        Vector3 center = cam.transform.position;
        center.z = 0f;

        // Шаг сетки: для Tile-тула — по тайловой клетке, со сдвигом на ПОЛКЛЕТКИ, чтобы линии шли
        // по ГРАНИЦАМ клеток (центр тайла = центр клетки → клик в клетку заполняет её). Для прочих — _grid.
        bool tileGrid = _tileGridView;
        float step = tileGrid ? _tileCell : _grid;
        float off  = tileGrid ? step * 0.5f : 0f;

        // Ограничиваем сетку видимой областью + 1 клетка запаса (линии на k*step + off)
        float x0 = Mathf.Floor((center.x - halfW - step - off) / step) * step + off;
        float x1 = Mathf.Ceil ((center.x + halfW + step - off) / step) * step + off;
        float y0 = Mathf.Floor((center.y - halfH - step - off) / step) * step + off;
        float y1 = Mathf.Ceil ((center.y + halfH + step - off) / step) * step + off;

        // Защита от слишком мелкой сетки (перфоманс)
        int maxLines = 300;
        if ((x1 - x0) / step > maxLines || (y1 - y0) / step > maxLines)
            return;

        // Крупные линии каждые 4 клетки
        float bigStep = step * 4f;

        for (float x = x0; x <= x1 + 0.001f; x += step)
        {
            float m = (x - off) % bigStep;
            bool big = Mathf.Abs(m) < 0.001f || Mathf.Abs(m - bigStep) < 0.001f;
            Handles.color = big
                ? new Color(1f, 1f, 1f, tileGrid ? 0.35f : 0.20f)
                : new Color(1f, 1f, 1f, tileGrid ? 0.15f : 0.07f);
            Handles.DrawLine(new Vector3(x, y0, 0f), new Vector3(x, y1, 0f));
        }

        for (float y = y0; y <= y1 + 0.001f; y += step)
        {
            float m = (y - off) % bigStep;
            bool big = Mathf.Abs(m) < 0.001f || Mathf.Abs(m - bigStep) < 0.001f;
            Handles.color = big
                ? new Color(1f, 1f, 1f, tileGrid ? 0.35f : 0.20f)
                : new Color(1f, 1f, 1f, tileGrid ? 0.15f : 0.07f);
            Handles.DrawLine(new Vector3(x0, y, 0f), new Vector3(x1, y, 0f));
        }

        // Оси X=0 и Y=0 — чуть ярче
        Handles.color = new Color(1f, 0.6f, 0.6f, 0.35f);
        Handles.DrawLine(new Vector3(0f, y0, 0f), new Vector3(0f, y1, 0f));
        Handles.color = new Color(0.6f, 1f, 0.6f, 0.35f);
        Handles.DrawLine(new Vector3(x0, 0f, 0f), new Vector3(x1, 0f, 0f));

        Handles.color = Color.white;
        // НЕ вызываем sv.Repaint() — это создаёт бесконечный цикл перерисовки!
    }

    /// <summary>3×3 сетка выбора точки привязки (как Unity sprite pivot)</summary>
    private void DrawPivotSelector()
    {
        // Метки: ↖↑↗ / ←·→ / ↙↓↘ — сверху вниз в UI = сверху вниз в мире
        string[][] labels =
        {
            new[]{"↖","↑","↗"},  // top row
            new[]{"←","·","→"},  // mid row
            new[]{"↙","↓","↘"},  // bottom row
        };
        float[] pxVals = { 0f, 0.5f, 1f };
        float[] pyVals = { 1f, 0.5f, 0f }; // UI top = world top

        for (int row = 0; row < 3; row++)
        {
            using (new GUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();
                for (int col = 0; col < 3; col++)
                {
                    float px = pxVals[col];
                    float py = pyVals[row];
                    bool sel = Mathf.Approximately(_pivotX, px) &&
                               Mathf.Approximately(_pivotY, py);
                    var prev = GUI.backgroundColor;
                    GUI.backgroundColor = sel ? new Color(0.4f, 0.8f, 1f) : Color.white * 0.8f;
                    if (GUILayout.Button(labels[row][col],
                        GUILayout.Width(28), GUILayout.Height(24)))
                    { _pivotX = px; _pivotY = py; }
                    GUI.backgroundColor = prev;
                }
                GUILayout.FlexibleSpace();
            }
        }

        // Подсказка текущего режима
        string hint = (_pivotX == 0.5f && _pivotY == 0.5f)
            ? "Center (стандартный)"
            : $"Привязка: {PivotName(_pivotX, _pivotY)}";
        GUILayout.Label(hint, EditorStyles.centeredGreyMiniLabel);
    }

    private static string PivotName(float px, float py)
    {
        string h = px < 0.3f ? "Left" : px > 0.7f ? "Right" : "Center";
        string v = py < 0.3f ? "Bottom" : py > 0.7f ? "Top" : "Center";
        return $"{v}-{h}";
    }

    /// <summary>Смещение от точки клика до центра объекта.</summary>
    private Vector3 GetPivotOffset(float w, float h)
    {
        if (Mathf.Approximately(_pivotX, 0.5f) && Mathf.Approximately(_pivotY, 0.5f))
            return Vector3.zero; // center — смещения нет
        return new Vector3((0.5f - _pivotX) * w, (0.5f - _pivotY) * h, 0f);
    }

    private bool IsPartOfLevel(GameObject go)
    {
        if (_root == null || go == null) return false;
        var t = go.transform;
        while (t != null)
        {
            if (t == _root.transform) return true;
            t = t.parent;
        }
        return false;
    }

    private static bool IsGroup(string name)
        => name == "Platforms" || name == "Walls" || name == "Coins" || name == "Stars" || name == "Tiles";

    private void AutoLevelName()
    {
        // Ищем следующий свободный номер
        int n = 1;
        while (File.Exists($"Assets/Resources/Levels/Level_{n:D2}.prefab")) n++;
        _levelName = $"Level_{n:D2}";
    }

    private static Material CreateColorMaterial(Color color)
    {
        var mat   = new Material(Shader.Find("Standard"));
        mat.color = color;
        return mat;
    }
}
