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
    // Порядок = актуальный флоу: тайлы → мета (артефакты/чекпоинт) → триггер-механика →
    // монеты/финиш/спавн → легаси (Platform/Wall, тайлсет их заменил).
    private enum Tool
    {
        Select,
        Tile, DisappearTile,
        Artifact, Flag, TriggerButton,
        Coin, Finish,
        SpawnPoint,
        Platform, PlatformWall
    }

    private static readonly string[] ToolLabels =
    {
        "🔲 Select",
        "🧱 Tile", "👻 Disappear",
        "🔑 Artifact", "🚩 Checkpoint", "🔘 Trigger",
        "🪙 Coin", "🏁 Finish",
        "📍 SpawnPoint",
        "⬛ Platform", "▎Wall"
    };

    private static readonly Color[] ToolColors =
    {
        Color.white,
        new Color(0.55f, 0.5f, 0.45f), new Color(0.5f, 0.7f, 1f),
        new Color(1f, 0.75f, 0.1f),    new Color(0.3f, 0.8f, 0.4f), new Color(0.9f, 0.55f, 0.2f),
        new Color(1f, 0.8f, 0.1f),     new Color(0.25f, 0.85f, 0.35f),
        new Color(1f, 0.4f, 0.4f),
        new Color(0.4f, 0.4f, 0.4f),   new Color(0.35f, 0.35f, 0.45f)
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
    private GameObject _pfPlatform, _pfWall, _pfCoin;
    private GameObject _pfFallCollider, _pfFlagFinish;
    private GameObject _pfTile;
    private GameObject _pfArtifact, _pfFlag, _pfButton;

    // Ключ пары триггер↔исчезающие тайлы (общий для Trigger + Disappear тулов). НЕ Unity-тег.
    private string _groupId = "A";

    // ─── Import from scheme (ASCII-карта уровня) ────────────────────────────────
    private bool    _showScheme;
    private string  _schemeText = "";
    private Vector2 _schemeScroll;
    // Радиус дотяжки холд→холд в КЛЕТКАХ (для эвристической проверки проходимости). Тюнить по плейтесту.
    private float   _schemeReachCells = 2.5f;
    // Декор/вариация тайлов: off = максимально ровно (одна трава + один камень).
    private bool    _schemeDecorate;

    // ─── Tiles ────────────────────────────────────────────────────────────────
    private Sprite[]   _tileSprites = {};
    private int        _selTile = 0;
    private Vector2    _tileScroll;
    private float _tileCell = 0.56f; // шаг сетки тайлов (редактируется в SETTINGS). < размера тайла (0.58) = лёгкое перекрытие, плотные швы

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
        DrawSchemeImport();
        GUILayout.Space(4);
        DrawLevelConfig();
        GUILayout.Space(4);
        DrawObjectList();
    }

    // ─── LevelConfig (задачи под звёзды, data-driven) ──────────────────────────
    private void DrawLevelConfig()
    {
        if (_root == null) return;
        GUILayout.Label("STAR TASKS (LevelConfig)", EditorStyles.boldLabel);

        var cfg = _root.GetComponent<LevelConfig>();
        if (cfg == null)
        {
            GUI.color = new Color(1f, 0.7f, 0.5f);
            GUILayout.Label("⚠ Нет LevelConfig — звёзды/задачи не работают.", EditorStyles.helpBox);
            GUI.color = Color.white;
            if (GUILayout.Button("➕ Add LevelConfig (3 задачи по умолчанию)"))
            {
                Undo.AddComponent<LevelConfig>(_root);
                EditorSceneManager.MarkSceneDirty(_root.scene);
            }
            return;
        }

        // Инлайн-редактор массива задач через SerializedObject (правится прямо здесь).
        var so = new SerializedObject(cfg);
        so.Update();
        var tasks = so.FindProperty("tasks");
        EditorGUILayout.PropertyField(tasks, new GUIContent("Tasks (звёзды)"), true);
        if (so.ApplyModifiedProperties())
            EditorSceneManager.MarkSceneDirty(_root.scene);

        if (cfg.TaskCount != 3)
        {
            GUI.color = new Color(1f, 0.8f, 0.4f);
            GUILayout.Label($"ℹ Задач: {cfg.TaskCount} (дизайн ждёт РОВНО 3 звезды на уровне).",
                EditorStyles.helpBox);
            GUI.color = Color.white;
        }
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

        DrawToolGroup(0, 1);                        // Select (0)
        using (new GUILayout.HorizontalScope())
            { DrawToolGroup(1, 1); DrawToolGroup(2, 1); }   // Tile, Disappear (1,2)
        using (new GUILayout.HorizontalScope())
            { DrawToolGroup(3, 1); DrawToolGroup(4, 1); DrawToolGroup(5, 1); } // Artifact, Flag, Trigger (3,4,5)
        using (new GUILayout.HorizontalScope())
            { DrawToolGroup(6, 1); DrawToolGroup(7, 1); }   // Coin, Finish (6,7)
        using (new GUILayout.HorizontalScope())
            { DrawToolGroup(8, 1); DrawToolGroup(9, 1); DrawToolGroup(10, 1); } // Spawn, Platform, Wall (8,9,10)
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
        if (_showGrid && IsTileTool)
            _tileCell = EditorGUILayout.Slider(
                new GUIContent("Tile cell", "Шаг тайловой сетки (постановка + snap-move + грид). Грид рисуется автоматически в тайл-инструментах."),
                _tileCell, 0.3f, 2f);

        // Ключ группы: связывает Trigger-кнопку с её группой исчезающих тайлов (одинаковый groupId).
        if (_tool == Tool.TriggerButton || _tool == Tool.DisappearTile)
        {
            GUI.color = new Color(0.85f, 0.9f, 1f);
            _groupId = EditorGUILayout.TextField(
                new GUIContent("Group ID", "Ключ пары триггер↔исчезающие тайлы. Кнопка и её тайлы должны иметь ОДИНАКОВЫЙ Group ID."),
                _groupId);
            if (string.IsNullOrWhiteSpace(_groupId)) _groupId = "A";
            GUILayout.Label(_tool == Tool.TriggerButton
                    ? "🔘 Кнопка активирует тайлы с этим Group ID"
                    : "👻 Тайлы уйдут в группу с этим Group ID (полупрозрачные до нажатия)",
                EditorStyles.helpBox);
            GUI.color = Color.white;
        }
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

        if (IsTileTool)
            DrawTilePalette();
    }

    /// <summary>Инструменты, работающие по тайловой сетке и использующие палитру тайлов.</summary>
    private bool IsTileTool => _tool == Tool.Tile || _tool == Tool.DisappearTile;

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

        DrawGroupFoldout("Tiles",       _root.transform.Find("Tiles"));
        DrawGroupFoldout("Disappearing",_root.transform.Find("Disappearing"));
        DrawGroupFoldout("Triggers",    _root.transform.Find("Triggers"));
        DrawGroupFoldout("Artifacts",   _root.transform.Find("Artifacts"));
        DrawGroupFoldout("Checkpoints", _root.transform.Find("Checkpoints"));
        DrawGroupFoldout("Coins",       _root.transform.Find("Coins"));
        DrawGroupFoldout("Platforms",   _root.transform.Find("Platforms"));
        DrawGroupFoldout("Walls",       _root.transform.Find("Walls"));

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
                // Тайл = ребёнок группы "Tiles" ИЛИ контейнера DisappearingPlatform (исчезающие тайлы).
                bool isTile = t.parent != null &&
                    (t.parent.name == "Tiles" || t.parent.GetComponent<DisappearingPlatform>() != null);
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
            if (IsTileTool)
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
                Handles.DrawWireDisc(pos, Vector3.forward, 0.35f);
                break;
            case Tool.Artifact:
                Handles.color = new Color(1f, 0.8f, 0.15f, 0.85f);
                Handles.DrawWireDisc(pos, Vector3.forward, 0.35f);
                Handles.DrawWireDisc(pos, Vector3.forward, 0.18f);
                break;
            case Tool.Flag:
                Handles.color = new Color(0.3f, 0.9f, 0.4f, 0.85f);
                Handles.DrawLine(pos, pos + Vector3.up * 0.9f);
                Handles.DrawSolidDisc(pos, Vector3.forward, 0.08f);
                break;
            case Tool.TriggerButton:
                Handles.color = new Color(0.95f, 0.55f, 0.2f, 0.85f);
                Handles.DrawWireDisc(pos, Vector3.forward, 0.4f);
                Handles.Label(pos + Vector3.up * 0.35f, $"[{_groupId}]", EditorStyles.whiteBoldLabel);
                break;
            case Tool.Finish:
                Handles.color = new Color(0.25f, 1f, 0.4f, 0.8f);
                Handles.DrawLine(pos, pos + Vector3.up * 1.2f);
                Handles.DrawWireCube(pos + Vector3.up * 1.0f + Vector3.right * 0.25f, new Vector3(0.5f, 0.35f, 0.1f));
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
            case Tool.DisappearTile:
                Handles.color = new Color(0.5f, 0.7f, 1f, 0.7f);
                Handles.DrawWireCube(pos, new Vector3(_tileCell, _tileCell, 0.1f));
                Handles.Label(pos + Vector3.up * (_tileCell * 0.5f + 0.15f), $"👻[{_groupId}]", EditorStyles.whiteBoldLabel);
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

            case Tool.Finish:
                go = PlaceFinish(pos);
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
                go = PlaceTile(grp, pos);
                break;
            }

            case Tool.DisappearTile:
            {
                if (_pfTile == null || _tileSprites == null || _tileSprites.Length == 0)
                {
                    Debug.LogWarning("[LevelEditor] Tile prefab или спрайты не найдены (Assets/Sprites/Tileset)");
                    break;
                }
                // Тайл кладётся в контейнер DisappearingPlatform с текущим groupId (создаём при отсутствии).
                var container = GetOrCreateDisappearGroup(_groupId);
                go = PlaceTile(container.transform, pos);
                break;
            }

            case Tool.Artifact:
                go = PlaceFromPrefab(_pfArtifact, pos, GetGroup("Artifacts"), "Artifact");
                break;

            case Tool.Flag:
                go = PlaceFromPrefab(_pfFlag, pos, GetGroup("Checkpoints"), "Flag");
                break;

            case Tool.TriggerButton:
            {
                go = PlaceFromPrefab(_pfButton, pos, GetGroup("Triggers"), "Trigger");
                if (go != null)
                {
                    var tt = go.GetComponentInChildren<TriggerTile>(true);
                    if (tt != null) { tt.groupId = _groupId; EditorUtility.SetDirty(tt); }
                }
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

    /// <summary>Ставит выбранный тайл (спрайт) как инстанс Tile.prefab в заданного родителя.</summary>
    private GameObject PlaceTile(Transform parent, Vector3 pos)
    {
        var go = (GameObject)PrefabUtility.InstantiatePrefab(_pfTile, parent);
        go.transform.position = new Vector3(pos.x, pos.y, parent != null ? parent.position.z : 0f);
        var sr = go.GetComponent<SpriteRenderer>();
        if (sr != null) sr.sprite = _tileSprites[_selTile];
        go.name = UniqueChildName(parent, _tileSprites[_selTile].name);
        return go;
    }

    /// <summary>
    /// Находит/создаёт контейнер DisappearingPlatform с данным groupId (под группой "Disappearing").
    /// Все исчезающие тайлы одного groupId = дети одного контейнера (он же вибрирует/скрывает их вместе).
    /// </summary>
    private GameObject GetOrCreateDisappearGroup(string groupId)
    {
        var parent = GetGroup("Disappearing");
        foreach (Transform c in parent)
        {
            var dp = c.GetComponent<DisappearingPlatform>();
            if (dp != null && dp.groupId == groupId) return c.gameObject;
        }
        var go = new GameObject($"Disappear_{groupId}");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = Vector3.zero; // контейнер в origin группы → _home корректен для вибрации
        var comp = go.AddComponent<DisappearingPlatform>();
        comp.groupId = groupId;
        Undo.RegisterCreatedObjectUndo(go, "Create Disappear group");
        return go;
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

    /// <summary>Ставит/перемещает единственный финиш (Flag_finish) в корне уровня. Уникален на уровень.</summary>
    private GameObject PlaceFinish(Vector3 pos)
    {
        if (_pfFlagFinish == null) { Debug.LogWarning("Flag_finish.prefab не найден!"); return null; }

        var existing = _root.transform.Find("Flag_finish")?.gameObject;
        if (existing != null)
        {
            Undo.RecordObject(existing.transform, "Move Flag_finish");
            existing.transform.position = pos;
            return existing;
        }

        var go = (GameObject)PrefabUtility.InstantiatePrefab(_pfFlagFinish, _root.transform);
        go.transform.position = pos;
        go.name = "Flag_finish";
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
        // Игрок в уровень НЕ кладётся (один в GameScene, LevelManager ставит его на SpawnPoint).
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

        _root = BuildLevelScaffold(_levelName);
        Undo.RegisterCreatedObjectUndo(_root, "Create Level");
        EditorSceneManager.MarkSceneDirty(
            UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        Selection.activeGameObject = _root;
        SceneView.FrameLastActiveSceneView();

        Debug.Log($"[LevelEditor] Создан новый уровень: {_levelName}");
        Repaint();
    }

    /// <summary>Каркас уровня: корень + LevelConfig + группы Tiles/Artifacts + FallCollider + SpawnPoint.
    /// Игрока и BackGroundWall НЕ добавляем (игрок один в GameScene, фон = параллакс).</summary>
    private GameObject BuildLevelScaffold(string levelName)
    {
        var root = new GameObject(levelName);
        root.AddComponent<LevelConfig>(); // 3 задачи-звезды по умолчанию — БЕЗ него звёзды не работают
        new GameObject("Tiles").transform.SetParent(root.transform, false);
        new GameObject("Artifacts").transform.SetParent(root.transform, false);

        if (_pfFallCollider != null)
        {
            var fc = (GameObject)PrefabUtility.InstantiatePrefab(_pfFallCollider, root.transform);
            fc.name = "FallCollider";
            fc.transform.localPosition = new Vector3(0f, -3f, 0f);
        }

        var sp = new GameObject("SpawnPoint");
        sp.transform.SetParent(root.transform, false);
        sp.transform.position = new Vector3(0f, 1f, 0f);
        return root;
    }

    // ─── Import from scheme (ASCII-карта → уровень) ─────────────────────────────
    private void DrawSchemeImport()
    {
        _showScheme = EditorGUILayout.Foldout(_showScheme, "🗺  IMPORT FROM SCHEME", true);
        if (!_showScheme) return;

        EditorGUILayout.HelpBox(
            "1 символ = 1 тайл-клетка.  Низ текста = низ уровня.\n" +
            "#=тайл  *=артефакт  $=монета  @=спавн  ^=финиш  ==чекпоинт\n" +
            "A–Z=кнопка группы X   a–z=исчезающий тайл группы X (пара по букве)\n" +
            ". или пробел = пусто.  Тайлы: авто (трава сверху / камень внутри).",
            MessageType.None);

        _schemeReachCells = EditorGUILayout.Slider(
            new GUIContent("Reach (клетки)", "Радиус дотяжки холд→холд для проверки проходимости. Эвристика — тюнить по плейтесту."),
            _schemeReachCells, 1f, 5f);
        _schemeDecorate = EditorGUILayout.Toggle(
            new GUIContent("Decorate/vary", "off = максимально ровно (одна трава + один камень). on = изредка цветы/кусты + чередование камня."),
            _schemeDecorate);

        _schemeScroll = GUILayout.BeginScrollView(_schemeScroll, GUILayout.Height(160));
        _schemeText = EditorGUILayout.TextArea(_schemeText, GUILayout.ExpandHeight(true));
        GUILayout.EndScrollView();

        using (new GUILayout.HorizontalScope())
        {
            var prev = GUI.backgroundColor;
            GUI.backgroundColor = new Color(0.4f, 0.7f, 1f);
            if (GUILayout.Button("🗺 Import → New Level", GUILayout.Height(26)))
                ImportScheme(_schemeText);
            GUI.backgroundColor = prev;
            if (GUILayout.Button("Check reachability", GUILayout.Height(26), GUILayout.Width(150)))
                CheckSchemeReachability(ParseScheme(_schemeText));
        }
    }

    // Парсит текст в матрицу символов [row][col]; row 0 = ВЕРХ (первая строка текста).
    private char[][] ParseScheme(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return new char[0][];
        var lines = text.Replace("\r", "").Split('\n');
        // Обрезаем пустые строки по краям
        int first = 0, last = lines.Length - 1;
        while (first <= last && lines[first].Trim().Length == 0) first++;
        while (last >= first && lines[last].Trim().Length == 0) last--;
        int rows = last - first + 1;
        if (rows <= 0) return new char[0][];
        var grid = new char[rows][];
        for (int r = 0; r < rows; r++) grid[r] = lines[first + r].ToCharArray();
        return grid;
    }

    private char CellAt(char[][] grid, int r, int c)
        => (r < 0 || r >= grid.Length || c < 0 || c >= grid[r].Length) ? '.' : grid[r][c];

    private static bool IsSolidCell(char ch) // тайл, дающий поверхность (обычный или исчезающий)
        => ch == '#' || (ch >= 'a' && ch <= 'z');

    private void ImportScheme(string text)
    {
        var grid = ParseScheme(text);
        if (grid.Length == 0) { Debug.LogWarning("[LevelEditor] Схема пуста."); return; }
        if (_pfTile == null || _tileSprites == null || _tileSprites.Length == 0)
        { Debug.LogWarning("[LevelEditor] Нет Tile.prefab/спрайтов тайлсета."); return; }

        if (_root != null &&
            !EditorUtility.DisplayDialog("Import scheme",
                "Текущий уровень будет закрыт и построен из схемы. Продолжить?", "Да", "Отмена"))
            return;
        if (_root != null) DestroyImmediate(_root);

        _root = BuildLevelScaffold(_levelName);
        _loadedPrefabPath = null;

        int rows = grid.Length;
        float cell = _tileCell;
        // Мир: bottom-left клетки = (0,0). row 0 = верх → y = (rows-1-row)*cell.
        System.Func<int,int,Vector3> world = (r, c) =>
            new Vector3(c * cell, (rows - 1 - r) * cell, 0f);

        var tilesGrp = GetGroup("Tiles");
        var artGrp   = GetGroup("Artifacts");
        int placed = 0, artifacts = 0, buttons = 0, disTiles = 0, checkpoints = 0, coins = 0;
        bool spawnSet = false, finishSet = false;

        for (int r = 0; r < rows; r++)
        for (int c = 0; c < grid[r].Length; c++)
        {
            char ch = grid[r][c];
            Vector3 p = world(r, c);

            if (ch == '#')
            {
                bool topExposed    = !IsSolidCell(CellAt(grid, r - 1, c)); // над пусто → грабельная верхушка
                bool bottomExposed = !IsSolidCell(CellAt(grid, r + 1, c)); // под пусто → сталактитовый низ
                bool leftEdge      = !IsSolidCell(CellAt(grid, r, c - 1)); // нет левого соседа → лево-кап
                bool rightEdge     = !IsSolidCell(CellAt(grid, r, c + 1)); // нет правого соседа → право-кап
                SpawnTileSprite(tilesGrp, p, PickAutoTile(topExposed, bottomExposed, leftEdge, rightEdge, r, c));
                placed++;
            }
            else if (ch >= 'a' && ch <= 'z')
            {
                string gid = char.ToUpper(ch).ToString();
                var container = GetOrCreateDisappearGroup(gid);
                bool topExposed    = !IsSolidCell(CellAt(grid, r - 1, c));
                bool bottomExposed = !IsSolidCell(CellAt(grid, r + 1, c));
                bool leftEdge      = !IsSolidCell(CellAt(grid, r, c - 1));
                bool rightEdge     = !IsSolidCell(CellAt(grid, r, c + 1));
                SpawnTileSprite(container.transform, p, PickAutoTile(topExposed, bottomExposed, leftEdge, rightEdge, r, c));
                disTiles++;
            }
            else if (ch >= 'A' && ch <= 'Z')
            {
                var go = PlaceFromPrefab(_pfButton, p, GetGroup("Triggers"), "Trigger");
                var tt = go != null ? go.GetComponentInChildren<TriggerTile>(true) : null;
                if (tt != null) { tt.groupId = ch.ToString(); EditorUtility.SetDirty(tt); }
                buttons++;
            }
            else if (ch == '*') // артефакт-ключ — коллектибл, парит где нарисован
            {
                PlaceFromPrefab(_pfArtifact, p, artGrp, "Artifact");
                artifacts++;
            }
            else if (ch == '$') // монета — коллектибл, парит где нарисована
            {
                PlaceFromPrefab(_pfCoin, p, GetGroup("Coins"), "Coin");
                coins++;
            }
            else if (ch == '=') // чекпоинт-флаг: на поверхность полки (spawnOffset −1.2 = корень игрока там)
            {
                PlaceFromPrefab(_pfFlag, SurfaceBelow(grid, rows, r, c, cell, world, p),
                    GetGroup("Checkpoints"), "Flag");
                checkpoints++;
            }
            else if (ch == '@')
            {
                var sp = _root.transform.Find("SpawnPoint");
                if (sp != null) sp.position = p;
                spawnSet = true;
            }
            else if (ch == '^') // финиш: тоже на поверхность полки под '^' (его рисуют в воздухе над ней)
            {
                PlaceFinish(SurfaceBelow(grid, rows, r, c, cell, world, p));
                finishSet = true;
            }
        }

        // FallCollider под низ грида
        var fall = _root.transform.Find("FallCollider");
        if (fall != null) fall.position = new Vector3((grid[rows - 1].Length) * cell * 0.5f, -2.5f, 0f);

        Undo.RegisterCreatedObjectUndo(_root, "Import scheme");
        EditorSceneManager.MarkSceneDirty(_root.scene);
        Selection.activeGameObject = _root;
        SceneView.FrameLastActiveSceneView();

        Debug.Log($"[LevelEditor] Импортирован уровень из схемы: тайлов={placed}, исчезающих={disTiles}, " +
                  $"кнопок={buttons}, артефактов={artifacts}, монет={coins}, чекпоинтов={checkpoints}, " +
                  $"спавн={(spawnSet ? "ok" : "НЕТ")}, финиш={(finishSet ? "ok" : "НЕТ")}");
        if (!spawnSet)  Debug.LogWarning("[LevelEditor] В схеме нет '@' (спавн) — SpawnPoint остался в (0,1).");
        if (!finishSet) Debug.LogWarning("[LevelEditor] В схеме нет '^' (финиш) — уровень непроходим до победы.");

        CheckSchemeReachability(grid);
        CheckFlagClearance(grid, rows, cell);
        Repaint();
    }

    // Флаг (финиш/чекпоинт) высокий (~2 юнита) — над его полкой нужно свободное место, иначе верхушка
    // (баннер) прячется за тайлами сверху. Правило: над базой флага должно быть ~FlagClearWorld пусто.
    private const float FlagClearWorld = 1.5f;
    private void CheckFlagClearance(char[][] grid, int rows, float cell)
    {
        int clear = Mathf.Max(2, Mathf.CeilToInt(FlagClearWorld / Mathf.Max(0.01f, cell)));
        for (int r = 0; r < rows; r++)
        for (int c = 0; c < grid[r].Length; c++)
        {
            char ch = grid[r][c];
            if (ch != '^' && ch != '=') continue;
            int rr = r + 1;
            while (rr < rows && !IsSolidCell(CellAt(grid, rr, c))) rr++;      // база = первая сплошная ниже
            int baseRow = (rr < rows && IsSolidCell(CellAt(grid, rr, c))) ? rr : r;
            for (int k = 1; k <= clear; k++)
                if (IsSolidCell(CellAt(grid, baseRow - k, c)))
                {
                    Debug.LogWarning($"[Flag] {(ch == '^' ? "Финиш" : "Чекпоинт")} (колонка {c}): над ним тайл " +
                        $"на {k}-й клетке — верхушка флага спрячется. Оставь ≥{clear} свободных клеток над полкой.");
                    break;
                }
        }
    }

    /// <summary>Позиция на ПОВЕРХНОСТИ первой сплошной клетки ниже (r,c): центр клетки + полклетки вверх.
    /// Для флагов (финиш/чекпоинт), которые рисуют в воздухе над полкой. Если ниже пусто — fallback.</summary>
    private Vector3 SurfaceBelow(char[][] grid, int rows, int r, int c, float cell,
                                 System.Func<int,int,Vector3> world, Vector3 fallback)
    {
        int rr = r + 1;
        while (rr < rows && !IsSolidCell(CellAt(grid, rr, c))) rr++;
        return (rr < rows && IsSolidCell(CellAt(grid, rr, c)))
            ? world(rr, c) + Vector3.up * (cell * 0.5f) : fallback;
    }

    /// <summary>Инстанс Tile.prefab с заданным спрайтом (для автотайлинга при импорте).</summary>
    private GameObject SpawnTileSprite(Transform parent, Vector3 pos, Sprite sprite)
    {
        var go = (GameObject)PrefabUtility.InstantiatePrefab(_pfTile, parent);
        go.transform.position = new Vector3(pos.x, pos.y, parent != null ? parent.position.z : 0f);
        var sr = go.GetComponent<SpriteRenderer>();
        if (sr != null && sprite != null) sr.sprite = sprite;
        go.name = UniqueChildName(parent, sprite != null ? sprite.name : "Tile");
        return go;
    }

    // Роли тайлов (12-набор, см. tileset-level-building). ТРИ оси у травы: низ (ровный 01-06 / сталактит
    // 07-12) × гориз.позиция (лево-кап / середина / право-кап). Камень (06/12) = заливка без капов.
    //   Ровный набор:  лево-кап=1, право-кап=5, середина plain=3 decor={2,4}, камень=6
    //   Сталакт.набор: лево-кап=7, право-кап=11, середина plain=9 decor={8,10}, камень=12
    private struct GrassSet { public int leftCap, rightCap, midPlain; public int[] midDecor; }
    private static readonly GrassSet Flat = new GrassSet { leftCap = 1, rightCap = 5, midPlain = 3, midDecor = new[] { 2, 4 } };
    private static readonly GrassSet Stal = new GrassSet { leftCap = 7, rightCap = 11, midPlain = 9, midDecor = new[] { 8, 10 } };
    private const int FlatStone = 6;   // камень, ровный низ
    private const int StalStone = 12;  // камень, сталактит-низ

    /// <summary>
    /// Автотайл по роли: верх открыт → трава (иначе камень); низ открыт → сталактит-набор (07-12), иначе
    /// ровный (01-06); у травы гориз.позиция: нет левого соседа → лево-кап, нет правого → право-кап, иначе
    /// середина. По умолчанию простая середина (макс. ровно); _schemeDecorate — изредка декор. Детерминир. по клетке.
    /// </summary>
    private const float DecorChance = 0.25f; // доля середин-травы, получающих декор (при _schemeDecorate)

    private Sprite PickAutoTile(bool topExposed, bool bottomExposed, bool leftEdge, bool rightEdge, int r, int c)
    {
        int num;
        if (topExposed) // трава-шапка
        {
            var set = bottomExposed ? Stal : Flat;
            if      (leftEdge)  num = set.leftCap;   // нет левого соседа → закрываем левый край
            else if (rightEdge) num = set.rightCap;  // нет правого соседа → закрываем правый край
            // Декор — СЛУЧАЙНО при каждом импорте (структура детерминирована, а декор игрок хотел разным).
            else if (_schemeDecorate && UnityEngine.Random.value < DecorChance)
                num = set.midDecor[UnityEngine.Random.Range(0, set.midDecor.Length)];
            else num = set.midPlain;  // середина полки
        }
        else            // камень (закрытый верх)
        {
            num = bottomExposed ? StalStone : FlatStone;
        }
        var s = FindTile(num);
        return s != null ? s : _tileSprites[_selTile]; // фолбэк — выбранный вручную
    }

    private Sprite FindTile(int number)
    {
        foreach (var s in _tileSprites) if (TileNumber(s.name) == number) return s;
        return null;
    }

    private static int TileNumber(string name)
    {
        int u = name.LastIndexOf('_');
        if (u < 0 || u == name.Length - 1) return -1;
        return int.TryParse(name.Substring(u + 1), out int n) ? n : -1;
    }

    /// <summary>
    /// Эвристическая проверка проходимости: BFS по грабельным холдам от спавна к финишу,
    /// рёбра между холдами в пределах _schemeReachCells. Пишет предупреждения в консоль (НЕ гарантия —
    /// физика климба сложнее; калибровать Reach по плейтесту).
    /// </summary>
    private void CheckSchemeReachability(char[][] grid)
    {
        if (grid.Length == 0) return;
        int rows = grid.Length;
        float cell = _tileCell;
        float reach = _schemeReachCells * cell;

        // Холд = верх грабельного тайла (над клеткой пусто). Позиция = центр-верх клетки.
        var holds = new System.Collections.Generic.List<Vector2>();
        Vector2 spawn = new Vector2(float.NaN, float.NaN), finish = new Vector2(float.NaN, float.NaN);
        for (int r = 0; r < rows; r++)
        for (int c = 0; c < grid[r].Length; c++)
        {
            char ch = grid[r][c];
            Vector2 wp = new Vector2(c * cell, (rows - 1 - r) * cell);
            if (IsSolidCell(ch) && !IsSolidCell(CellAt(grid, r - 1, c)))
                holds.Add(wp + Vector2.up * cell * 0.5f);
            if (ch == '@') spawn  = wp;
            if (ch == '^') finish = wp;
        }
        if (holds.Count == 0) { Debug.LogWarning("[Reach] В схеме нет грабельных холдов."); return; }

        // Стартовые холды = в пределах reach от спавна (или все нижние, если спавна нет).
        int n = holds.Count;
        var visited = new bool[n];
        var queue = new System.Collections.Generic.Queue<int>();
        for (int i = 0; i < n; i++)
            if (float.IsNaN(spawn.x) || Vector2.Distance(holds[i], spawn) <= reach * 1.5f)
            { if (!visited[i]) { visited[i] = true; queue.Enqueue(i); } }

        while (queue.Count > 0)
        {
            int i = queue.Dequeue();
            for (int j = 0; j < n; j++)
                if (!visited[j] && Vector2.Distance(holds[i], holds[j]) <= reach)
                { visited[j] = true; queue.Enqueue(j); }
        }

        int reachable = 0; foreach (var v in visited) if (v) reachable++;
        int isolated = n - reachable;

        bool finishOk = false;
        if (!float.IsNaN(finish.x))
            for (int i = 0; i < n; i++)
                if (visited[i] && Vector2.Distance(holds[i], finish) <= reach) { finishOk = true; break; }

        string msg = $"[Reach] Холдов: {n}, достижимо от спавна: {reachable}, изолировано: {isolated}. " +
                     $"Reach={_schemeReachCells:F1} клетки ({reach:F2}u).";
        if (isolated > 0 || (!float.IsNaN(finish.x) && !finishOk)) Debug.LogWarning(msg);
        else Debug.Log(msg);
        if (!float.IsNaN(finish.x) && !finishOk)
            Debug.LogWarning("[Reach] ⚠ ФИНИШ не достижим от спавна по холдам — есть непроходимый разрыв (увеличь Reach или добавь холды).");
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
        _pfFallCollider = Load("Assets/Prefabs/FallCollider.prefab");
        _pfFlagFinish   = Load("Assets/Prefabs/Flag_finish.prefab");
        _pfTile         = Load("Assets/Prefabs/Tile.prefab");
        _pfArtifact     = Load("Assets/Prefabs/Artifact.prefab");
        _pfFlag         = Load("Assets/Prefabs/Flag_checkpoint.prefab"); // переименован из Flag.prefab
        _pfButton       = Load("Assets/Prefabs/Button.prefab");
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
        // Грид автоматически по активному инструменту: Tile → тайловая клетка (_tileCell),
        // Platform/PlatformWall и пр. → общая сетка (_grid). Видимый грид всегда = шагу снапа инструмента.
        bool tileGrid = IsTileTool;
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
        => name == "Tiles" || name == "Artifacts" || name == "Checkpoints" || name == "Triggers"
        || name == "Disappearing" || name == "Coins"
        || name == "Platforms" || name == "Walls" || name == "Stars"; // Platforms/Walls/Stars — легаси

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
