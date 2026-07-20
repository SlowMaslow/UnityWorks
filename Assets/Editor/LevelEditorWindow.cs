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
    // ⚠️ [SerializeField] ОБЯЗАТЕЛЕН для настроек окна: обычные приватные поля EditorWindow Unity
    // сбрасывает к значениям по умолчанию при КАЖДОЙ перекомпиляции. Игрок выставлял 300 монет,
    // получал 30 и думал, что параметр не работает (2026-07-18).
    // Окно активности появляющихся платформ считается из длины пути (см. ApplyTriggerWindows).
    // Стартовая калибровка игрока: 2 клетки/сек + 2 сек запаса. Крутить здесь, код не трогать.
    [SerializeField] private float _platSpeed  = 2f;
    [SerializeField] private float _platBuffer = 2f;
    // Минимальная дистанция между флагами-чекпоинтами (в клетках). Правило «по чекпоинту у каждого
    // ключа» без неё лепило флаги в шаге друг от друга, когда ключи кучные (фидбэк с плейтеста).
    [SerializeField] private float _cpMinGap = 8f;
    // Сколько монет класть в уровень (примерно — генератор варьирует ±15% и ограничен местом).
    [SerializeField] private int _coinBudget = 30;

    // ─── Import from scheme (ASCII-карта уровня) ────────────────────────────────
    private bool    _showScheme;
    private string  _schemeText = "";
    private Vector2 _schemeScroll;
    // Радиус дотяжки холд→холд в КЛЕТКАХ (для эвристической проверки проходимости). Тюнить по плейтесту.
    // Дотяжка холд→холд ПО ОСЯМ (замер игрока, тайлы): climb вверх ≤4, вбок ≤4. НЕ радиус.
    private float   _reachUpCells   = 4f;
    private float   _reachSideCells = 4f;
    // Декор/вариация тайлов: off = максимально ровно (одна трава + один камень).
    private bool    _schemeDecorate;
    // Генератор лабиринта: размер сетки комнат + сид (0 = случайный).
    [SerializeField] private int _mazeW = 5, _mazeH = 5, _mazeSeed = 0;   // см. пояснение к [SerializeField] ниже

    // ─── Tiles ────────────────────────────────────────────────────────────────
    private Sprite[]   _tileSprites = {};
    private int        _selTile = 0;
    private Vector2    _tileScroll;
    private float _tileCell = 0.5f; // шаг сетки тайлов (редактируется в SETTINGS). Текущий тайлсет = шаг 0.5 (при 0.56 были щели)

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
        Tools.hidden = false; // вернуть стандартный гизмо сцене при закрытии окна
    }

    /// <summary>Показать нативный гизмо при возврате в Select (placement-тулы его прятали через Tools.hidden).
    /// Tools.current НЕ трогаем — перемещение объектов уровня идёт нашим handle (см. OnSceneGUI), чтобы
    /// не было двух активных инструментов/гизмо.</summary>
    private void EnsureSelectUsable() => Tools.hidden = false;

    /// <summary>Снап позиции при перемещении объекта уровня: тайлы — по _tileCell (центр), прочие — по
    /// _grid с учётом pivot. Z не трогаем.</summary>
    private Vector3 SnapMovePos(Transform t, Vector3 newPos)
    {
        bool isTile = t.parent != null &&
            (t.parent.name == "Tiles" || t.parent.GetComponent<DisappearingPlatform>() != null);
        if (isTile)
            return new Vector3(Mathf.Round(newPos.x / _tileCell) * _tileCell,
                               Mathf.Round(newPos.y / _tileCell) * _tileCell, t.position.z);
        var pivotOffset = new Vector3((0.5f - _pivotX) * t.localScale.x,
                                      (0.5f - _pivotY) * t.localScale.y, 0f);
        var pivotInWorld = newPos - pivotOffset;
        var snappedPivot = new Vector3(Mathf.Round(pivotInWorld.x / _grid) * _grid,
                                       Mathf.Round(pivotInWorld.y / _grid) * _grid, t.position.z);
        return snappedPivot + pivotOffset;
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
                if (_tool == Tool.Select) EnsureSelectUsable();
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
        // ── Перемещение объекта уровня СВОИМ handle (единый гизмо; нативный Move скрыт → нет дубля). ──
        // В Select для любого объекта уровня; снап по сетке — ТОЛЬКО при включённом "Snap on move".
        if (_tool == Tool.Select && _root != null && Selection.activeTransform != null
            && IsPartOfLevel(Selection.activeTransform.gameObject))
        {
            var t = Selection.activeTransform;
            Tools.hidden = true; // прячем нативный гизмо — двигаем своим (иначе два гизмо/два тула)
            EditorGUI.BeginChangeCheck();
            var newPos = Handles.PositionHandle(t.position, Quaternion.identity);
            if (EditorGUI.EndChangeCheck())
            {
                Vector3 target = _snapMove ? SnapMovePos(t, newPos)
                                           : new Vector3(newPos.x, newPos.y, t.position.z);
                Vector3 delta = target - t.position;
                var selT = Selection.transforms;
                Undo.RecordObjects(selT, "Move");
                foreach (var st in selT)
                    if (IsPartOfLevel(st.gameObject)) st.position += delta;
                EditorSceneManager.MarkSceneDirty(
                    UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            }
        }
        else if (_root != null)
        {
            // Инструменты РАЗМЕЩЕНИЯ прячут нативный гизмо (иначе Move/Rect перехватывает клик-постановку).
            // Гейт по _root: окно БЕЗ загруженного уровня (напр. фоновый/утёкший инстанс) НЕ трогает
            // глобальный Tools.hidden — иначе перебивает активное окно (был двойной гизмо).
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
            EnsureSelectUsable();
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
            fc.transform.localPosition = new Vector3(0f, -5f, 0f); // ниже, иначе тело на спавне задевает зону смерти
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

        _reachUpCells = EditorGUILayout.Slider(
            new GUIContent("Reach ↑ (тайлы)", "Макс. разрыв ВВЕРХ холд→холд для проверки проходимости (замер игрока ≈2.5)."),
            _reachUpCells, 1f, 5f);
        _reachSideCells = EditorGUILayout.Slider(
            new GUIContent("Reach ↔ (тайлы)", "Макс. разрыв ВБОК холд→холд (замер игрока ≈4)."),
            _reachSideCells, 1f, 8f);
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
        // Обратный конвертер: загруженный уровень → схема в поле (для изучения/правки существующих уровней).
        using (new EditorGUI.DisabledScope(_root == null))
            if (GUILayout.Button("⤴ Level → схема (в поле выше)", GUILayout.Height(22)))
                { _schemeText = ExportScheme(_root); Debug.Log("[LevelEditor] Уровень экспортирован в схему (поле Import)."); }

        // ── Генератор лабиринта ──────────────────────────────────────────────
        GUILayout.Space(4);
        GUILayout.Label("🌀 MAZE GENERATOR (ветвления + тупики)", EditorStyles.boldLabel);
        using (new GUILayout.HorizontalScope())
        {
            _mazeW    = EditorGUILayout.IntField(new GUIContent("Комнат ↔", "Ширина сетки комнат"), _mazeW);
            _mazeH    = EditorGUILayout.IntField(new GUIContent("Комнат ↕", "Высота сетки комнат"), _mazeH);
            _mazeSeed = EditorGUILayout.IntField(new GUIContent("Seed", "0 = случайный каждый раз"), _mazeSeed);
        }
        // По одному полю в строке: вчетвером в одной строке подписи сжимались до нечитаемого вида,
        // и легко было ввести число не в то поле.
        _coinBudget = EditorGUILayout.IntField(new GUIContent("Монет на уровень",
            "Сколько монет класть в уровень (±15%, ограничено свободным местом)"), _coinBudget);
        _cpMinGap   = EditorGUILayout.FloatField(new GUIContent("Флаги ≥ клеток",
            "Минимальная дистанция между чекпоинтами (и до спавна). До финиша — в 1.5 раза больше"), _cpMinGap);
        using (new GUILayout.HorizontalScope())
        {
            _platSpeed  = EditorGUILayout.FloatField(new GUIContent("Климб кл/с",
                "Скорость прохождения для расчёта окна активности появляющихся платформ"), _platSpeed);
            _platBuffer = EditorGUILayout.FloatField(new GUIContent("Запас, с",
                "Добавка к окну активности сверх расчётного времени пути"), _platBuffer);
        }
        var mprev = GUI.backgroundColor;
        GUI.backgroundColor = new Color(0.7f, 0.5f, 0.9f);
        if (GUILayout.Button("🌀 Generate maze → в поле", GUILayout.Height(24)))
            { _schemeText = GenerateMazeScheme(); Debug.Log("[LevelEditor] Лабиринт сгенерирован в поле Import — жми Import, чтобы построить."); }
        GUI.backgroundColor = mprev;
    }

    /// <summary>
    /// Генератор ЛАБИРИНТА: сетка комнат _mazeW×_mazeH, recursive-backtracker → остовное дерево (ветвления
    /// + тупики, единственный путь спавн→финиш). Рендер под механику: комната = 3-широкая платформа
    /// (шаг 4×4 клетки); гор.проход = соседи в дотяжке, гор.стена = тайл-колонна в зазоре; верт.проход =
    /// промежуточный холд на +2 ряда (лестница), верт.стена = разрыв 4 ряда (>2.5, блок). Финиш — самая
    /// дальняя комната (BFS), артефакты — в тупиках. Спавн по центру нижне-левой платформы.
    /// </summary>
    /// <summary>Максимальный подъём холд→холд, который СТРОИТ генератор, в клетках. Намеренно меньше
    /// залоченной дотяжки _reachUpCells=4: 4 — это предел замера, а в игре подъём на висящую ступеньку
    /// впритык не берётся (плейтест 2026-07-18). Держим ряд запаса.</summary>
    private const int MazeClimb = 3;

    private string GenerateMazeScheme()
    {
        int CW = Mathf.Clamp(_mazeW, 2, 12), CH = Mathf.Clamp(_mazeH, 2, 12);
        var rng = _mazeSeed == 0 ? new System.Random() : new System.Random(_mazeSeed);

        // ── ФОРМА ЛАБИРИНТА (фидбэк игрока: уровни не должны быть все квадратные). Часть клеток
        // сетки объявляется МЁРТВОЙ — комнат там нет, лишний камень обрезается, и силуэт получается
        // неправильным. Формы КОМБИНИРУЮТСЯ (раньше выбиралась одна и результат был предсказуем).
        // ⚠️ Клетку (0,0) беречь больше НЕ НАДО: стартовая комната выбирается случайно среди живых,
        // поэтому маска свободна резать что угодно — лишь бы осталась достаточно большая связная часть.
        var alive = new bool[CW, CH];
        for (int x = 0; x < CW; x++) for (int y = 0; y < CH; y++) alive[x, y] = true;
        if (CW >= 4 && CH >= 4)
        {
            if (rng.Next(100) < 70)                       // СКАЙЛАЙН ПО СТОЛБЦАМ: рваный верх (или низ)
            {
                bool fromTop = rng.Next(2) == 0;
                for (int x = 0; x < CW; x++)
                {
                    int keepRooms = Mathf.Max(1, CH - rng.Next(0, (CH * 2) / 3 + 1));
                    for (int y = 0; y < CH; y++)
                    {
                        bool inside = fromTop ? (y < keepRooms) : (y >= CH - keepRooms);
                        if (!inside) alive[x, y] = false;
                    }
                }
            }
            if (rng.Next(100) < 55)                       // СКАЙЛАЙН ПО СТРОКАМ: рваный левый/правый край
            {
                bool fromLeft = rng.Next(2) == 0;
                for (int y = 0; y < CH; y++)
                {
                    int keepRooms = Mathf.Max(1, CW - rng.Next(0, (CW * 2) / 3 + 1));
                    for (int x = 0; x < CW; x++)
                    {
                        bool inside = fromLeft ? (x < keepRooms) : (x >= CW - keepRooms);
                        if (!inside) alive[x, y] = false;
                    }
                }
            }
            if (rng.Next(100) < 50)                       // ВЫКУСЫ: рваные единичные клетки по краю
            {
                int notches = 2 + rng.Next(3 + (CW * CH) / 8);
                for (int i = 0; i < notches; i++)
                {
                    int x = rng.Next(CW), y = rng.Next(CH);
                    if (x > 0 && x < CW - 1 && y > 0 && y < CH - 1) continue;
                    alive[x, y] = false;
                }
            }
            if (CW >= 5 && CH >= 5 && rng.Next(100) < 45)  // ВНУТРЕННИЕ ПУСТОТЫ: дыры в середине массива
            {
                // Ни скайлайны, ни выкусы такого не дают — они грызут только края. Дыра внутри
                // превращается в «окно» со сквозным небом и обход вокруг него.
                int holes = 1 + rng.Next(2 + (CW * CH) / 20);
                for (int i = 0; i < holes; i++)
                {
                    int hx = 1 + rng.Next(CW - 2), hy = 1 + rng.Next(CH - 2);
                    int hw2 = 1 + rng.Next(2), hh2 = 1 + rng.Next(2);
                    for (int x = hx; x < Mathf.Min(CW - 1, hx + hw2); x++)
                    for (int y = hy; y < Mathf.Min(CH - 1, hy + hh2); y++)
                        alive[x, y] = false;
                }
            }
        }
        {   // Оставляем САМУЮ БОЛЬШУЮ связную часть (не обязательно ту, что в углу). Если и она мала —
            // откат к прямоугольнику: лучше скучная форма, чем уровень из трёх комнат.
            var seenCell = new bool[CW, CH];
            var best = new System.Collections.Generic.List<Vector2Int>();
            for (int x0 = 0; x0 < CW; x0++)
            for (int y0 = 0; y0 < CH; y0++)
            {
                if (seenCell[x0, y0] || !alive[x0, y0]) continue;
                var comp2 = new System.Collections.Generic.List<Vector2Int>();
                var q0 = new System.Collections.Generic.Queue<Vector2Int>();
                seenCell[x0, y0] = true; q0.Enqueue(new Vector2Int(x0, y0));
                while (q0.Count > 0)
                {
                    var cur = q0.Dequeue(); comp2.Add(cur);
                    foreach (var d in new[] { new Vector2Int(1,0), new Vector2Int(-1,0), new Vector2Int(0,1), new Vector2Int(0,-1) })
                    {
                        int nx = cur.x + d.x, ny = cur.y + d.y;
                        if (nx < 0 || nx >= CW || ny < 0 || ny >= CH || seenCell[nx, ny] || !alive[nx, ny]) continue;
                        seenCell[nx, ny] = true; q0.Enqueue(new Vector2Int(nx, ny));
                    }
                }
                if (comp2.Count > best.Count) best = comp2;
            }
            // Порог отката снижен до 30% (игрок: «чем более случайно, тем лучше») — узкие перешейки
            // и кишки допустимы, лишь бы лабиринт не выродился в горстку комнат.
            if (best.Count >= Mathf.Max(4, (CW * CH) * 3 / 10))
            {
                for (int x = 0; x < CW; x++) for (int y = 0; y < CH; y++) alive[x, y] = false;
                foreach (var p in best) alive[p.x, p.y] = true;
            }
            else for (int x = 0; x < CW; x++) for (int y = 0; y < CH; y++) alive[x, y] = true;
        }

        // Проходы: hPass[x,y] — между (x,y) и (x+1,y); vPass[x,y] — между (x,y) и (x,y+1).
        var hPass = new bool[CW, CH];
        var vPass = new bool[CW, CH];
        var vis   = new bool[CW, CH];
        // Мёртвые клетки помечаем «посещёнными» — обход их не тронет, проходов к ним не будет.
        for (int x = 0; x < CW; x++) for (int y = 0; y < CH; y++) if (!alive[x, y]) vis[x, y] = true;
        var stack = new System.Collections.Generic.Stack<Vector2Int>();
        // ⭐ СТАРТ НЕ ОБЯЗАН БЫТЬ В УГЛУ (фидбэк игрока): выбираем стартовую комнату среди ЖИВЫХ —
        // может оказаться сверху, снизу, в центре. Дальше всё считается от неё: обход лабиринта,
        // расстояния, выбор финиша (самая дальняя комната), маршрут, сторона ворот.
        var aliveList = new System.Collections.Generic.List<Vector2Int>();
        for (int x = 0; x < CW; x++) for (int y = 0; y < CH; y++) if (alive[x, y]) aliveList.Add(new Vector2Int(x, y));
        var startRoom = aliveList[rng.Next(aliveList.Count)];

        int lastDir = -1;
        vis[startRoom.x, startRoom.y] = true; stack.Push(startRoom);
        while (stack.Count > 0)
        {
            var cur = stack.Peek();
            int x = cur.x, y = cur.y;
            var nb = new System.Collections.Generic.List<int>(); // 0=R,1=L,2=U,3=D
            if (x + 1 < CW && !vis[x + 1, y]) nb.Add(0);
            if (x - 1 >= 0 && !vis[x - 1, y]) nb.Add(1);
            if (y + 1 < CH && !vis[x, y + 1]) nb.Add(2);
            if (y - 1 >= 0 && !vis[x, y - 1]) nb.Add(3);
            if (nb.Count == 0) { stack.Pop(); lastDir = -1; continue; }
            // Смещение в сторону ПРЯМЫХ коридоров: без него backtracker плодит частые повороты и
            // мелкие тупики-ниши, и уровень читается как «просто иди по маршруту» (фидбэк игрока).
            // Продолжая прежнее направление, получаем длинные ветки, в которые игрок реально уходит.
            int d = (lastDir >= 0 && nb.Contains(lastDir) && rng.Next(100) < 65)
                  ? lastDir : nb[rng.Next(nb.Count)];
            lastDir = d;
            int nx = x, ny = y;
            if (d == 0) { hPass[x, y] = true; nx = x + 1; }
            else if (d == 1) { hPass[x - 1, y] = true; nx = x - 1; }
            else if (d == 2) { vPass[x, y] = true; ny = y + 1; }
            else { vPass[x, y - 1] = true; ny = y - 1; }
            vis[nx, ny] = true; stack.Push(new Vector2Int(nx, ny));
        }

        // BFS от (0,0) по проходам: расстояния + степень (для тупиков) + самая дальняя комната (финиш).
        var dist = new int[CW, CH]; for (int x = 0; x < CW; x++) for (int y = 0; y < CH; y++) dist[x, y] = -1;
        var q = new System.Collections.Generic.Queue<Vector2Int>();
        dist[startRoom.x, startRoom.y] = 0; q.Enqueue(startRoom);
        Vector2Int far = startRoom;
        while (q.Count > 0)
        {
            var c = q.Dequeue(); int x = c.x, y = c.y;
            if (dist[x, y] > dist[far.x, far.y]) far = c;
            if (x + 1 < CW && hPass[x, y] && dist[x + 1, y] < 0) { dist[x + 1, y] = dist[x, y] + 1; q.Enqueue(new Vector2Int(x + 1, y)); }
            if (x - 1 >= 0 && hPass[x - 1, y] && dist[x - 1, y] < 0) { dist[x - 1, y] = dist[x, y] + 1; q.Enqueue(new Vector2Int(x - 1, y)); }
            if (y + 1 < CH && vPass[x, y] && dist[x, y + 1] < 0) { dist[x, y + 1] = dist[x, y] + 1; q.Enqueue(new Vector2Int(x, y + 1)); }
            if (y - 1 >= 0 && vPass[x, y - 1] && dist[x, y - 1] < 0) { dist[x, y - 1] = dist[x, y] + 1; q.Enqueue(new Vector2Int(x, y - 1)); }
        }

        // Тупики = комнаты со степенью 1 (кроме спавна и финиша) — туда артефакты.
        System.Func<int,int,int> degree = (x, y) =>
        {
            int deg = 0;
            if (x + 1 < CW && hPass[x, y]) deg++;
            if (x - 1 >= 0 && hPass[x - 1, y]) deg++;
            if (y + 1 < CH && vPass[x, y]) deg++;
            if (y - 1 >= 0 && vPass[x, y - 1]) deg++;
            return deg;
        };

        // Соседи комнаты по реальным проходам — база для веток и маршрута.
        System.Func<Vector2Int, System.Collections.Generic.List<Vector2Int>> nbrs = p =>
        {
            var l = new System.Collections.Generic.List<Vector2Int>();
            if (p.x + 1 < CW && hPass[p.x, p.y])     l.Add(new Vector2Int(p.x + 1, p.y));
            if (p.x - 1 >= 0 && hPass[p.x - 1, p.y]) l.Add(new Vector2Int(p.x - 1, p.y));
            if (p.y + 1 < CH && vPass[p.x, p.y])     l.Add(new Vector2Int(p.x, p.y + 1));
            if (p.y - 1 >= 0 && vPass[p.x, p.y - 1]) l.Add(new Vector2Int(p.x, p.y - 1));
            return l;
        };
        // Глубина тупиковой ветки: сколько комнат от тупика до ближайшей развилки. Чем глубже, тем
        // ценнее как «зона интереса» — игрок реально сворачивает и возвращается, а не заглядывает в нишу.
        System.Func<Vector2Int,int> branchDepth = start =>
        {
            var list = nbrs(start);
            if (list.Count != 1) return 0;
            Vector2Int prev = start, cur = list[0];
            int depth = 1;
            while (true)
            {
                var l = nbrs(cur);
                if (l.Count != 2 || depth > CW * CH) break;      // развилка или тупик — ветка кончилась
                var nx = l[0].Equals(prev) ? l[1] : l[0];
                prev = cur; cur = nx; depth++;
            }
            return depth;
        };

        // ── Рендер v2: «ВЫРЕЗАНО В СКАЛЕ» — сплошной массив, коридоры карвятся. Срезки и мусорные
        // карманы исключены ПО ПОСТРОЕНИЮ: всё, что не вырезано — камень; движение только по коридорам.
        // v3 ВАРИАТИВНОСТЬ (фидбэк: v2 повторял паттерны — все комнаты 4×4, все проёмы на всю высоту,
        // отчего ряд комнат сливался в одну пустую «простыню»). Теперь: размеры комнат неравномерны,
        // проёмы разной высоты, люки разной ширины, у пола рельеф.
        // ⚠️ ВЫСОТУ КОМНАТ ДИКТУЕТ КЛИМБ: подъёмы строим с запасом ≤MazeClimb (3), а не в залоченный
        // максимум ↑4 — плейтест показал, что 4 ряда до висящей ступеньки игрок не берёт. Ограничение
        // H ≤ MazeClimb+2 = 5 (см. выкладку у ступеньки). Ширина ничем не ограничена — вдоль пола
        // движение идёт клетка за клеткой.
        // Ширина комнат 3..8: по горизонтали дотяжка ничего не ограничивает (движение вдоль пола идёт
        // клетка за клеткой), поэтому разброс здесь можно давать шире, чем по высоте.
        var colW = new int[CW]; for (int x = 0; x < CW; x++) colW[x] = 3 + rng.Next(6);
        var rowH = new int[CH]; for (int y = 0; y < CH; y++)
            rowH[y] = Mathf.Min(MazeClimb + 2, 3 + rng.Next(3));                          // высота комнат 3..5
        var colX = new int[CW + 1]; colX[0] = 1;                     // префикс-суммы: левый столбец интерьера (+1 = стена)
        for (int x = 0; x < CW; x++) colX[x + 1] = colX[x] + colW[x] + 1;
        var rowY = new int[CH + 1]; rowY[0] = 1;                     // сверху вниз: первым идёт ряд комнат CH−1
        for (int y = 0; y < CH; y++) rowY[y + 1] = rowY[y] + rowH[CH - 1 - y] + 1;
        int rows = rowY[CH], cols = colX[CW];
        var g = new char[rows, cols];
        for (int r = 0; r < rows; r++) for (int c = 0; c < cols; c++) g[r, c] = '#';
        System.Action<int,int,char> set = (r, c, ch) => { if (r >= 0 && r < rows && c >= 0 && c < cols) g[r, c] = ch; };
        System.Func<int,int,char> at = (r, c) => (r >= 0 && r < rows && c >= 0 && c < cols) ? g[r, c] : '#';
        System.Func<int,int> C0 = cx => colX[cx];                    // левый столбец интерьера комнаты
        System.Func<int,int> R0 = cy => rowY[CH - 1 - cy];           // верхний ряд интерьера (cy=0 — низ)
        System.Func<int,int> W  = cx => colW[cx];
        System.Func<int,int> H  = cy => rowH[cy];
        // Колонка внутри комнаты по порядку «от центра к краям» (для размещений — центр всегда приятнее края).
        System.Func<int,int,int> offFromMid = (w, d) => { int mid = w / 2; return mid + (d % 2 == 0 ? d / 2 : -(d / 2 + 1)); };

        // Вырезанное пространство: нужно, чтобы отличать «воздух уровня» от пустоты СНАРУЖИ после
        // обрезки лишнего камня (иначе главной областью воздуха стало бы небо вокруг уровня).
        var carved = new bool[rows, cols];
        System.Action<int,int> carve = (r, c) =>
        { if (r >= 0 && r < rows && c >= 0 && c < cols) { g[r, c] = '.'; carved[r, c] = true; } };

        for (int cx = 0; cx < CW; cx++)                        // полости комнат (только живые клетки)
        for (int cy = 0; cy < CH; cy++)
        {
            if (!alive[cx, cy]) continue;
            for (int r = 0; r < H(cy); r++) for (int c = 0; c < W(cx); c++) carve(R0(cy) + r, C0(cx) + c);
        }

        for (int cx = 0; cx < CW - 1; cx++)                    // горизонтальные проёмы: чаще НИЗКИЕ (от пола),
        for (int cy = 0; cy < CH; cy++)                        // иногда во всю высоту = слитый зал
            if (hPass[cx, cy])
            {
                int hh = H(cy);
                // «Низкий» проём должен быть СТРОГО ниже комнаты (hh−1), иначе в низких комнатах (H=3)
                // min(hh, 2..3) сплошь давал полную высоту — ряд комнат сливался в пустую «простыню».
                int dh = rng.Next(100) < 25 ? hh : Mathf.Min(hh - 1, 2 + rng.Next(2));
                for (int r = 0; r < dh; r++) carve(R0(cy) + hh - 1 - r, C0(cx) + W(cx));
            }

        // Клетки, где НЕЛЬЗЯ ставить тумбу: она встанет на посадочный холд у люка и убьёт его
        // (холд = сплошная клетка с пустотой СВЕРХУ; тумба сверху превращает посадку в глухую стену).
        var noBump = new bool[rows, cols];
        // Тумбы — ЧИСТЫЙ декор, их можно убрать при расшивке диагональных зажимов. Ступеньки сюда НЕ
        // входят: они несут вертикальные проходы, и снос ступеньки превращает подъём в непроходимый
        // (обжёгся: снёс их расшивкой — 43 из 138 лабиринтов развалились при строгой ↑3).
        var bump = new bool[rows, cols];
        // Вертикальные проходы: запоминаем, чтобы позже часть из них превратить в ВОРОТА (ступенька из
        // появляющихся платформ). Ключи: комната снизу, ряд ступеньки, её колонки.
        var shafts = new System.Collections.Generic.List<(Vector2Int room, int stepRow, int col0, int width)>();
        // Клетки настоящих КОРИДОРОВ (дверные проёмы и вертикальные шахты): расшивке запрещено их
        // заглушать — иначе она замуровывает сам проход (обжёгся: 24 из 138 разваливались при ↑3).
        var noFill = new bool[rows, cols];
        // Создаст ли тайл в (r,c) диагональный зажим — проверяем все четыре квадрата 2×2 вокруг клетки.
        // ⚠️ Считаем только КАМЕНЬ — ровно как CheckDiagonalPinch. Раньше здесь был IsSolidCell, который
        // считает появляющиеся платформы сплошными: внутренняя проверка молчала, а валидатор потом ловил
        // зажим. Две трактовки одного правила = баг по построению (уже обжигались на базе клиренса флага).
        System.Func<int,int,bool> makesPinch = (r, c) =>
        {
            for (int rr = r - 1; rr <= r; rr++)
            for (int cc = c - 1; cc <= c; cc++)
            {
                bool tl = at(rr, cc) == '#',     tr = at(rr, cc + 1) == '#';
                bool bl = at(rr + 1, cc) == '#', br = at(rr + 1, cc + 1) == '#';
                if ((tl && br && !tr && !bl) || (tr && bl && !tl && !br)) return true;
            }
            return false;
        };
        for (int cx = 0; cx < CW - 1; cx++)                    // помечаем клетки дверных проёмов как коридор
        for (int cy = 0; cy < CH; cy++)
            if (hPass[cx, cy])
                for (int r = 0; r < H(cy); r++)
                {
                    int rr = R0(cy) + r, cc = C0(cx) + W(cx);
                    if (at(rr, cc) == '.') noFill[rr, cc] = true;
                }

        for (int cx = 0; cx < CW; cx++)                        // вертикальные шахты: люк в потолке + ступенька
        for (int cy = 0; cy < CH - 1; cy++)
            if (vPass[cx, cy])
            {
                int ceilRow = R0(cy) - 1;                      // ряд-стена между cy и cy+1
                int hw = Mathf.Min(W(cx) - 1, 2 + rng.Next(2));            // ширина люка 2-3 (край комнаты остаётся)
                // ⚠️ ПОСАДОЧНЫЙ ХОЛД: вылезая из люка, игрок цепляется за клетку потолка СБОКУ от люка.
                // Колонка-стена холдом не бывает (над ней тоже стена), поэтому люк вплотную к краю комнаты
                // оставляет игрока без зацепа с этой стороны. Держим люк внутри, если ширина позволяет.
                int sMax = W(cx) - hw;
                int s = (sMax >= 2) ? 1 + rng.Next(sMax - 1) : rng.Next(Mathf.Max(1, sMax + 1));
                // ⚠️ ЗАПАС К ДОТЯЖКЕ (плейтест игрока 2026-07-18): привязка ступеньки к потолку давала
                // подъём пол→ступенька = H−1, т.е. РОВНО 4 ряда при H=5 — впритык к залоченному максимуму,
                // и на висящую в воздухе ступеньку игрок физически не влезал. Теперь ступенька ставится так,
                // чтобы ОБА участка были ≤ MazeClimb рядов: пол→ступенька и ступенька→пол верхней комнаты.
                //   пол = R0+H, ступенька = R0+H−climb, верхний пол = R0−1
                //   пол→ступенька = climb;  ступенька→верхний пол = H−climb+1  ⇒ обе ≤3 при H ≤ climb+2.
                // ⚠️ ГРАНИЦЫ climb (обжёгся дважды — оба раза модель это пропускала):
                //   сверху `H−1` — иначе ступенька встаёт ВПЛОТНУЮ ПОД ЛЮК и затыкает собственную шахту
                //     (при H=3 и climb=3 так запечатывались целые этажи: 25 схем из 40 с глухими карманами);
                //   снизу `H−2` — иначе участок ступенька→пол верхней комнаты (= H−climb+1) вылезает за MazeClimb.
                int loClimb = Mathf.Max(2, H(cy) - 2);
                int hiClimb = Mathf.Min(MazeClimb, H(cy) - 1);
                int climb = hiClimb;
                if (hiClimb > loClimb && rng.Next(2) == 0) climb = loClimb;   // иногда положе — вариация
                int stepRow = R0(cy) + H(cy) - climb;
                for (int k = 0; k < hw; k++)
                {
                    carve(ceilRow, C0(cx) + s + k);                        // люк
                    set(stepRow, C0(cx) + s + k, '#');                     // ступенька под люком
                }
                for (int k = -1; k <= hw; k++)                             // защищаем посадочные холды по бокам люка
                {
                    int c = C0(cx) + s + k;
                    if (ceilRow - 1 >= 0 && c >= 0 && c < cols) noBump[ceilRow - 1, c] = true;
                }
                shafts.Add((new Vector2Int(cx, cy), stepRow, C0(cx) + s, hw));
                for (int k = 0; k < hw; k++)                               // ствол шахты (ступенька→люк) = коридор
                for (int rr = ceilRow; rr < stepRow; rr++)
                {
                    int c = C0(cx) + s + k;
                    if (rr >= 0 && rr < rows && c >= 0 && c < cols) noFill[rr, c] = true;
                }
            }

        // ── ДЕТАЛИ КОМНАТ: набор паттернов вместо одних тумб (фидбэк игрока: «можно придумать больше
        // паттернов, чтобы было разнообразнее»). Всё это ДЕКОР — помечается `bump`, чтобы расшивка
        // зажимов могла безболезненно снять любой тайл. Каждый тайл ставится и откатывается, если
        // рождает диагональный зажим или лезет в коридор.
        System.Func<int,int,bool> tryDetail = (r, c) =>
        {
            if (at(r, c) != '.' || noBump[r, c] || noFill[r, c]) return false;
            set(r, c, '#');
            if (makesPinch(r, c)) { set(r, c, '.'); return false; }
            bump[r, c] = true; return true;
        };
        for (int cx = 0; cx < CW; cx++)
        for (int cy = 0; cy < CH; cy++)
        {
            if (rng.Next(100) >= 55) continue;                 // деталь примерно в половине комнат
            int rFloor = R0(cy) + H(cy) - 1, w = W(cx), h = H(cy);
            switch (rng.Next(6))
            {
                case 4:                                        // ЗУБЦЫ: два коротких выступа с зазором
                {
                    if (w < 5) break;
                    int off = 1 + rng.Next(Mathf.Max(1, w - 4));
                    tryDetail(rFloor, C0(cx) + off);
                    tryDetail(rFloor, C0(cx) + Mathf.Min(w - 2, off + 2));
                    break;
                }
                case 5:                                        // СТАЛАКТИТ: тайл свисает с потолка
                {                                              // (не холд — сверху камень; чистый декор)
                    if (h < 4) break;
                    int sw = 1 + rng.Next(2), off = 1 + rng.Next(Mathf.Max(1, w - sw - 1));
                    for (int k = 0; k < sw; k++) tryDetail(R0(cy), C0(cx) + off + k);
                    break;
                }
                case 0:                                        // ТУМБА: 1-2 клетки на полу
                {
                    int bw = 1 + rng.Next(2), off = 1 + rng.Next(Mathf.Max(1, w - bw - 1));
                    for (int k = 0; k < bw; k++) tryDetail(rFloor, C0(cx) + off + k);
                    break;
                }
                case 1:                                        // ПИЛОН: столб от пола вверх (делит комнату)
                {
                    if (w < 4) break;                          // в узкой комнате столб = пробка
                    int off = 1 + rng.Next(w - 2), ph = 1 + rng.Next(Mathf.Min(2, h - 1));
                    for (int k = 0; k < ph; k++) tryDetail(rFloor - k, C0(cx) + off);
                    break;
                }
                case 2:                                        // БАЛКОН: полка у стены на высоте 2 (в дотяжке)
                {
                    if (h < 4) break;
                    bool left = rng.Next(2) == 0;
                    int lw = 2 + rng.Next(Mathf.Max(1, w - 3));
                    for (int k = 0; k < lw; k++)
                        tryDetail(rFloor - 2, C0(cx) + (left ? k : w - 1 - k));
                    break;
                }
                default:                                       // ЛЕСЕНКА: две ступени вразнобой
                {
                    if (w < 4 || h < 4) break;
                    int off = 1 + rng.Next(Mathf.Max(1, w - 3));
                    tryDetail(rFloor,     C0(cx) + off);
                    tryDetail(rFloor - 2, C0(cx) + Mathf.Min(w - 1, off + 2));
                    break;
                }
            }
        }

        // ── РАСШИВКА ДИАГОНАЛЬНЫХ ЗАЖИМОВ (фидбэк игрока 2026-07-18: «тайлы стоят вплотную по диагонали,
        // пэды туда просто не пролезут»). Два тайла, соприкасающихся УГЛАМИ, оставляют щель нулевой ширины —
        // физический пэд там не проходит, а модель проходимости считала место открытым: wallBetween смотрит
        // колонки МЕЖДУ холдами, а у диагонали их нет. Инвариант: ни в одном квадрате 2×2 не должно быть
        // «шахматки» (два тайла по одной диагонали, две пустоты по другой).
        // Сколько клеток воздуха связано с опорной точкой (низ стартовой комнаты), 4-связность.
        // Заделка щели ОБЯЗАНА сохранять связность: иначе она замуровывает комнаты (игрок поймал —
        // генерились глухие карманы с ключами, 25 схем из 40).
        // ⚠️ Опорную точку НЕ фиксируем: если бы она сама оказалась замурована, страховка ослепла бы
        // (внутри кармана любая заделка «сохраняет связность»). Меряем ГЛАВНУЮ область воздуха.
        System.Func<bool[,]> airMap = () =>
        {
            var seen = new bool[rows, cols];
            var main = new bool[rows, cols];
            int bestSize = 0;
            for (int r0 = 0; r0 < rows; r0++)
            for (int c0 = 0; c0 < cols; c0++)
            {
                if (seen[r0, c0] || IsSolidCell(at(r0, c0))) continue;
                var cells = new System.Collections.Generic.List<Vector2Int>();
                var fq = new System.Collections.Generic.Queue<Vector2Int>();
                seen[r0, c0] = true; fq.Enqueue(new Vector2Int(c0, r0));
                while (fq.Count > 0)
                {
                    var cur = fq.Dequeue(); cells.Add(cur);
                    for (int k = 0; k < 4; k++)
                    {
                        int nr = cur.y + (k == 0 ? -1 : k == 1 ? 1 : 0), nc = cur.x + (k == 2 ? -1 : k == 3 ? 1 : 0);
                        if (nr < 0 || nr >= rows || nc < 0 || nc >= cols || seen[nr, nc] || IsSolidCell(at(nr, nc))) continue;
                        seen[nr, nc] = true; fq.Enqueue(new Vector2Int(nc, nr));
                    }
                }
                // ⚠️ Берём самую большую область ИЗ ВЫРЕЗАННЫХ: после обрезки лишнего камня снаружи
                // появляется огромная пустота (небо), и по размеру победила бы она — спавн уехал бы
                // на крышу уровня.
                bool hasCarved = false;
                foreach (var p in cells) if (carved[p.y, p.x]) { hasCarved = true; break; }
                if (hasCarved && cells.Count > bestSize)
                {
                    bestSize = cells.Count;
                    main = new bool[rows, cols];
                    foreach (var p in cells) main[p.y, p.x] = true;
                }
            }
            return main;
        };
        System.Func<int> countAir = () =>
        {
            var m = airMap(); int cnt = 0;
            for (int r = 0; r < rows; r++) for (int c = 0; c < cols; c++) if (m[r, c]) cnt++;
            return cnt;
        };
        // Заглушить клетку, только если воздух остался связным (потеря ровно 1 клетки — самой заглушённой).
        System.Func<int,int,bool> tryFill = (r, c) =>
        {
            if (noFill[r, c]) return false;
            int before = countAir();
            set(r, c, '#');
            if (countAir() == before - 1) return true;
            set(r, c, '.'); return false;                      // откат: заделка отрезала бы кусок уровня
        };

        for (int iter = 0; iter < 16; iter++)   // с запасом: заделка угла иногда рождает новый угол
        {
            int fixes = 0;
            for (int r = 0; r + 1 < rows; r++)
            for (int c = 0; c + 1 < cols; c++)
            {
                bool tl = IsSolidCell(at(r, c)),     tr = IsSolidCell(at(r, c + 1));
                bool bl = IsSolidCell(at(r + 1, c)), br = IsSolidCell(at(r + 1, c + 1));
                if (!((tl && br && !tr && !bl) || (tr && bl && !tl && !br))) continue;
                int s1r, s1c, s2r, s2c, e1r, e1c, e2r, e2c;
                if (tl && br) { s1r=r; s1c=c;   s2r=r+1; s2c=c+1; e1r=r; e1c=c+1; e2r=r+1; e2c=c;   }
                else          { s1r=r; s1c=c+1; s2r=r+1; s2c=c;   e1r=r; e1c=c;   e2r=r+1; e2c=c+1; }
                // 1) Если один из тайлов — ТУМБА (чистый декор), убираем ЕЁ: маршрут сохраняется,
                //    стены остаются целыми (дырявить стены нельзя — это вернёт «срезки» из v1).
                if      (bump[s1r, s1c]) { set(s1r, s1c, '.'); bump[s1r, s1c] = false; }
                else if (bump[s2r, s2c]) { set(s2r, s2c, '.'); bump[s2r, s2c] = false; }
                else
                {
                    // 2) Иначе ДОСТРАИВАЕМ угол: заглушаем ту пустоту, что «тупиковее» (меньше открытых
                    //    соседей). Щель исчезает, полка становится Г-образной, ступенька цела.
                    //    ⚠️ Клетки коридоров (проёмы, стволы шахт) заглушать НЕЛЬЗЯ — замуруем проход.
                    System.Func<int,int,int> openNb = (rr, cc) =>
                    {
                        int k = 0;
                        if (!IsSolidCell(at(rr - 1, cc))) k++;
                        if (!IsSolidCell(at(rr + 1, cc))) k++;
                        if (!IsSolidCell(at(rr, cc - 1))) k++;
                        if (!IsSolidCell(at(rr, cc + 1))) k++;
                        return k;
                    };
                    //    Пробуем сначала более «тупиковую» пустоту; tryFill сам откатится, если заделка
                    //    отрежет часть уровня.
                    bool deadFirst = openNb(e1r, e1c) <= openNb(e2r, e2c);
                    int f1r = deadFirst ? e1r : e2r, f1c = deadFirst ? e1c : e2c;
                    int f2r = deadFirst ? e2r : e1r, f2c = deadFirst ? e2c : e1c;
                    // 3) Если ни одну пустоту заглушить нельзя (обе — проход), убираем ТАЙЛ: маленькая
                    //    ниша в стене лучше замурованной комнаты.
                    if (!tryFill(f1r, f1c) && !tryFill(f2r, f2c))
                    {
                        // Убираем тот тайл, снятие которого НЕ рождает новый зажим по соседству —
                        // иначе расшивка зацикливается, гоняя один и тот же угол между двумя клетками.
                        set(s1r, s1c, '.');
                        if (makesPinch(s1r, s1c)) { set(s1r, s1c, '#'); set(s2r, s2c, '.'); }
                    }
                }
                fixes++;
            }
            if (fixes == 0) break;
        }

        // ГЛАВНАЯ область воздуха — единственное место, куда можно что-либо ставить. Без этой привязки
        // спавн однажды сел в замурованную одноклеточную ячейку (проверка размещения смотрела только
        // «есть ли пол под клеткой»), а ключи оказывались в глухих карманах.
        var mainAir = airMap();

        // Размещение на полу комнаты: воздух-ряд над полом, колонка с ЦЕЛЫМ полом снизу (не над люком).
        // Игрока (@) и флаги ставим на ШИРОКУЮ полку — пол и под соседними клетками, куда лягут пэды
        // (иначе вторая рука висит над обрывом, см. фикс чекпоинта). Первый проход требует широкую полку,
        // второй — любую (чтобы спавн/финиш не пропали в тесной геометрии).
        System.Func<int,int,char,bool> placeOnFloor = (cy, cx2, chr) =>
        {
            int rAir = R0(cy) + H(cy) - 1, w = W(cx2);
            for (int pass = 0; pass < 2; pass++)
            for (int d = 0; d < w; d++)
            {
                int off = offFromMid(w, d);
                if (off < 0 || off >= w) continue;
                int c = C0(cx2) + off;
                if (at(rAir, c) != '.' || at(rAir + 1, c) != '#' || !mainAir[rAir, c]) continue;
                if (pass == 0)   // широкая полка: обе соседние клетки — годная опора под пэд
                {
                    if (at(rAir, c - 1) != '.' || at(rAir + 1, c - 1) != '#') continue;
                    if (at(rAir, c + 1) != '.' || at(rAir + 1, c + 1) != '#') continue;
                }
                set(rAir, c, chr); return true;
            }
            return false;
        };
        // Колонка с ШИРОКОЙ полкой (пол под c-1,c,c+1) в комнате — чтобы оба пэда легли на неё.
        System.Func<int,int,int> wideCol = (cy, cx2) =>
        {
            int rAir = R0(cy) + H(cy) - 1, w = W(cx2);
            for (int d = 0; d < w; d++)
            {
                int off = offFromMid(w, d);
                if (off < 0 || off >= w) continue;
                int c = C0(cx2) + off;
                if (at(rAir, c) != '.' || at(rAir + 1, c) != '#' || !mainAir[rAir, c]) continue;
                if (at(rAir, c - 1) != '.' || at(rAir + 1, c - 1) != '#') continue;
                if (at(rAir, c + 1) != '.' || at(rAir + 1, c + 1) != '#') continue;
                return c;
            }
            return -1;
        };
        // Спавн — на ШИРОКОЙ полке (иначе при появлении/респавне вторая рука висит над обрывом, фидбэк
        // игрока). Стартовая комната у формы может быть вся с люком в полу — тогда берём ближайшую по
        // структуре комнату с широкой полкой. В самом крайнем случае — узкая где угодно (схема без '@'
        // невалидна, а проверка её не ловила: считала достижимым всё подряд, поймано на seed 572).
        {
            var byNear = new System.Collections.Generic.List<Vector2Int>();
            for (int x = 0; x < CW; x++) for (int y = 0; y < CH; y++)
                if (dist[x, y] >= 0) byNear.Add(new Vector2Int(x, y));
            byNear.Sort((a, b) =>
            {
                int da = Mathf.Abs(a.x - startRoom.x) + Mathf.Abs(a.y - startRoom.y);
                int db = Mathf.Abs(b.x - startRoom.x) + Mathf.Abs(b.y - startRoom.y);
                return da.CompareTo(db);
            });
            int wc = wideCol(startRoom.y, startRoom.x); Vector2Int spRoom = startRoom;
            if (wc < 0) foreach (var rm in byNear) { wc = wideCol(rm.y, rm.x); if (wc >= 0) { spRoom = rm; break; } }
            if (wc >= 0) set(R0(spRoom.y) + H(spRoom.y) - 1, wc, '@');
            else { foreach (var rm in byNear) if (placeOnFloor(rm.y, rm.x, '@')) break; }
        }

        // ── Финиш: нужен свободный БОКС вокруг, а не только пустая колонка сверху (фидбэк игрока).
        // Гоча, которую это чинит: ступенька под люком садится на R0+1, т.е. ровно на 2 клетки НАД полом
        // комнаты — флаг вставал прямо под неё и баннер прятался. Скорим колонки: воздух над целым полом,
        // ≥flagClear пусто вверх, максимум пустых клеток по бокам на высоте флага (центр комнаты лучше края).
        int flagClear = Mathf.Max(2, Mathf.CeilToInt(FlagClearWorld / Mathf.Max(0.01f, _tileCell)));
        int flagRight = Mathf.Max(2, Mathf.CeilToInt(FlagRightWorld / Mathf.Max(0.01f, _tileCell)));
        System.Func<int,int,int> bestFlagCol = (cy, cx2) =>
        {
            int rAir = R0(cy) + H(cy) - 1, bestC = -1, bestScore = -1;
            for (int off = 0; off < W(cx2); off++)
            {
                int c = C0(cx2) + off;
                if (at(rAir, c) != '.' || at(rAir + 1, c) != '#' || !mainAir[rAir, c]) continue; // воздух над ЦЕЛЫМ полом
                // ⚠️ ПОЛКА ПОД ОБА ПЭДА (фидбэк игрока: при респавне вторая рука ушла «за лабиринт»).
                // Пэды стоят в клетках c±1 от корня (размах ~1 юнит при cell 0.5). Если под соседней
                // клеткой обрыв, пэд повисает над пропастью и цепляется за дальнюю полку. Требуем, чтобы
                // с ОБЕИХ сторон была годная опора: воздух для пэда + пол под ним (не у стены/края).
                if (at(rAir, c - 1) != '.' || at(rAir + 1, c - 1) != '#') continue;
                if (at(rAir, c + 1) != '.' || at(rAir + 1, c + 1) != '#') continue;
                // ⚠️ БАЗА ОТСЧЁТА — ряд ПОЛА (rAir+1), ровно как в CheckFlagClearance (тот находит базу
                // сканом вниз до первой сплошной). Раньше считал от ряда флага → сдвиг на клетку, и
                // генератор «одобрял» колонку, которую валидатор потом ругал. Считаем ту же зону, что и он.
                int baseRow = rAir + 1;
                int up = 0; while (up < flagClear && !IsSolidCell(at(baseRow - 1 - up, c))) up++;
                if (up < flagClear) continue;                                      // баннер спрячется — не годится
                // Баннер уходит ВПРАВО (пивот в основании слева): справа нужна свободная зона
                // flagRight × flagClear. Слева древко — требований нет.
                int blocked = 0;
                for (int k = 1; k <= flagClear; k++)
                for (int dx = 1; dx <= flagRight; dx++)
                    if (IsSolidCell(at(baseRow - k, c + dx))) blocked++;
                if (blocked > 0) continue;
                int score = 0;                                                     // при равенстве — где просторнее сверху
                for (int k = 1; k <= flagClear + 1; k++) if (!IsSolidCell(at(baseRow - k, c))) score++;
                if (score > bestScore) { bestScore = score; bestC = c; }
            }
            return bestC;
        };
        // Кандидаты — комнаты по убыванию удалённости от спавна: берём САМУЮ дальнюю, где флагу хватает места.
        var byDist = new System.Collections.Generic.List<Vector2Int>();
        for (int x = 0; x < CW; x++) for (int y = 0; y < CH; y++)
            if (dist[x, y] >= 0 && !(x == startRoom.x && y == startRoom.y)) byDist.Add(new Vector2Int(x, y));
        byDist.Sort((a, b) => dist[b.x, b.y].CompareTo(dist[a.x, a.y]));
        bool finPlaced = false;
        foreach (var room in byDist)
        {
            int c = bestFlagCol(room.y, room.x);
            if (c < 0) continue;
            set(R0(room.y) + H(room.y) - 1, c, '^'); far = room; finPlaced = true; break;
        }
        if (!finPlaced) placeOnFloor(far.y, far.x, '^');   // фолбэк: как раньше (клиренс = сама полость)

        // Артефакты — РОВНО 3: тупики в приоритете (зона интереса в конце тупикового пути), затем любые.
        // ── ЗОНЫ ИНТЕРЕСА ────────────────────────────────────────────────────────────────────────
        // Тупики по УБЫВАНИЮ глубины ветки: чем длиннее ветка, тем ценнее награда в её конце.
        var deadEnds = new System.Collections.Generic.List<Vector2Int>();
        for (int x = 0; x < CW; x++) for (int y = 0; y < CH; y++)
        {
            if (degree(x, y) != 1) continue;
            if ((x == startRoom.x && y == startRoom.y) || (x == far.x && y == far.y)) continue;
            deadEnds.Add(new Vector2Int(x, y));
        }
        deadEnds.Sort((a, b) => branchDepth(b).CompareTo(branchDepth(a)));

        // Маршрут спавн→финиш: чекпоинт и кнопку игрок должен встретить ПО ДОРОГЕ, а не в тупике.
        var parent = new System.Collections.Generic.Dictionary<Vector2Int, Vector2Int>();
        var seenR  = new bool[CW, CH];
        var bq     = new System.Collections.Generic.Queue<Vector2Int>();
        seenR[startRoom.x, startRoom.y] = true; bq.Enqueue(startRoom);
        while (bq.Count > 0)
        {
            var cur = bq.Dequeue();
            foreach (var nb2 in nbrs(cur))
                if (!seenR[nb2.x, nb2.y]) { seenR[nb2.x, nb2.y] = true; parent[nb2] = cur; bq.Enqueue(nb2); }
        }
        var path = new System.Collections.Generic.List<Vector2Int>();
        { var cur = far; path.Add(cur); while (parent.ContainsKey(cur)) { cur = parent[cur]; path.Add(cur); } path.Reverse(); }

        // Ставит символ на пол комнаты (первая подходящая колонка от центра). Общий помощник размещений.
        System.Func<Vector2Int,char,bool> putOnFloor = (room, chr) =>
        {
            int rAir = R0(room.y) + H(room.y) - 1, w = W(room.x);
            for (int d = 0; d < w; d++)
            {
                int off = offFromMid(w, d);
                if (off < 0 || off >= w) continue;
                int c = C0(room.x) + off;
                if (at(rAir, c) == '.' && at(rAir + 1, c) == '#' && mainAir[rAir, c]) { set(rAir, c, chr); return true; }
            }
            return false;
        };

        // Комнаты, уже занятые под роль: разные механики не должны лезть друг другу в клетки.
        var usedRooms = new System.Collections.Generic.HashSet<Vector2Int>();
        int placed = 0;

        // ── КЛЮЧИ: сперва глубокие тупики, потом любые комнаты.
        // ⚠️ ЗАЗОР ПО БОКАМ ОБЯЗАТЕЛЕН: замер дал ключ 0.80 × 0.37 юнита — при cell 0.5 он ШИРЕ своей
        // клетки и вылезает в соседние. Значит слева и справа должно быть ПУСТО, иначе спрайт
        // пересекается с тайлами, монетами и прочим (фидбэк игрока 2026-07-18).
        var keyRooms = new System.Collections.Generic.List<Vector2Int>();
        for (int pass = 0; pass < 2 && placed < 3; pass++)
        {
            var order = new System.Collections.Generic.List<Vector2Int>();
            if (pass == 0) order.AddRange(deadEnds);
            else for (int x = 0; x < CW; x++) for (int y = 0; y < CH; y++) order.Add(new Vector2Int(x, y));
            foreach (var room in order)
            {
                if (placed >= 3) break;
                int cx = room.x, cy = room.y;
                if (room.Equals(startRoom) || (cx == far.x && cy == far.y) || usedRooms.Contains(room)) continue;
                // Высота ключа: 1-2 ряда НАД полом, а не середина полости — в высокой комнате середина
                // уходит выше дотяжки ↑4 от пола, и валидатор ловил «артефакт недостижим».
                int r = Mathf.Max(R0(cy), R0(cy) + H(cy) - 2 - rng.Next(2)), w = W(cx);
                for (int d = 0; d < w; d++)
                {
                    int off = offFromMid(w, d);
                    if (off < 0 || off >= w) continue;
                    int c = C0(cx) + off;
                    if (at(r, c) != '.' || !mainAir[r, c]) continue;
                    if (at(r, c - 1) != '.' || at(r, c + 1) != '.') continue;   // пусто с ОБЕИХ сторон
                    set(r, c, '*'); placed++; usedRooms.Add(room); keyRooms.Add(room); break;
                }
            }
        }

        // ── ЧЕКПОИНТЫ: по одному в комнате КАЖДОГО ключа — прогресс фиксируется в ключевых точках,
        // а не где попало. Если флагу не хватает клиренса — ставим в соседнюю комнату той же ветки.
        // ⚠️ ПРОРЕЖИВАНИЕ (фидбэк с плейтеста): «по чекпоинту у каждого ключа» без учёта расстояния
        // лепило флаги в шаге друг от друга на кучных ключах, а чекпоинт у финиша вообще бессмыслен —
        // уровень там кончается. Рядом со спавном он тоже пустой: респавн и так там. Правило: ставим,
        // только если до ближайшего чекпоинта и до спавна ≥ _cpMinGap клеток, а до финиша — в полтора
        // раза больше. Количество подстраивается само: кучные ключи делят один флаг.
        Vector2Int spawnCell = new Vector2Int(-999, -999), finCell = new Vector2Int(-999, -999);
        for (int r = 0; r < rows; r++)
        for (int c = 0; c < cols; c++)
        {
            if (g[r, c] == '@') spawnCell = new Vector2Int(c, r);
            if (g[r, c] == '^') finCell   = new Vector2Int(c, r);
        }
        var cpCells = new System.Collections.Generic.List<Vector2Int>();
        float cpGap = Mathf.Max(2f, _cpMinGap), cpGapFinish = cpGap * 1.5f;
        System.Func<int,int,bool> cpTooClose = (r, c) =>
        {
            var p = new Vector2(c, r);
            if (Vector2.Distance(p, new Vector2(spawnCell.x, spawnCell.y)) < cpGap) return true;
            if (Vector2.Distance(p, new Vector2(finCell.x,   finCell.y))   < cpGapFinish) return true;
            foreach (var q in cpCells) if (Vector2.Distance(p, new Vector2(q.x, q.y)) < cpGap) return true;
            return false;
        };
        // Ключи по возрастанию удалённости от спавна: прогресс фиксируется по ходу движения, а при
        // конфликте выигрывает более ранний ключ.
        keyRooms.Sort((a, b) => dist[a.x, a.y].CompareTo(dist[b.x, b.y]));

        var cpRooms = new System.Collections.Generic.HashSet<Vector2Int>();
        foreach (var kr in keyRooms)
        {
            // Кандидаты — сама комната ключа, затем соседи, затем соседи соседей: клиренс флага
            // правосторонний и жёсткий, в тесной комнате места может не найтись.
            var cands = new System.Collections.Generic.List<Vector2Int> { kr };
            foreach (var n1 in nbrs(kr))
            {
                if (!cands.Contains(n1)) cands.Add(n1);
                foreach (var n2 in nbrs(n1)) if (!cands.Contains(n2)) cands.Add(n2);
            }
            foreach (var room in cands)
            {
                if (room.Equals(startRoom)) continue;                       // не в стартовой комнате
                if (cpRooms.Contains(room)) continue;                       // не два чекпоинта в одной комнате
                int c = bestFlagCol(room.y, room.x);
                if (c < 0) continue;
                int rr = R0(room.y) + H(room.y) - 1;
                if (cpTooClose(rr, c)) continue;                            // слишком близко к флагу/спавну/финишу
                set(rr, c, '=');
                cpRooms.Add(room);
                cpCells.Add(new Vector2Int(c, rr));
                break;
            }
        }

        // ⚠️ Комнаты ФИНИША и ЧЕКПОИНТОВ тоже занимаем: иначе ворота ставили свою платформу прямо над
        // флагом и тот терял правый/верхний клиренс (поймано свипом после добавления ворот).
        usedRooms.Add(far);
        foreach (var cr in cpRooms) usedRooms.Add(cr);

        // Страховка: если ни у одного ключа флаг не прошёл по дистанции (кучные ключи у спавна/финиша),
        // ставим один на середине маршрута — но по тому же правилу дистанции. На совсем коротком
        // уровне не пройдёт и он, и это нормально: там чекпоинт не нужен.
        if (cpCells.Count == 0 && path.Count >= 3)
            for (int tries = 0; tries < path.Count; tries++)
            {
                int idx = Mathf.Clamp(path.Count / 2 + (tries % 2 == 0 ? tries / 2 : -(tries / 2 + 1)), 1, path.Count - 1);
                var room = path[idx];
                if (room.Equals(startRoom)) continue;
                int c = bestFlagCol(room.y, room.x);
                if (c < 0) continue;
                int rr = R0(room.y) + H(room.y) - 1;
                if (cpTooClose(rr, c)) continue;
                set(rr, c, '='); cpCells.Add(new Vector2Int(c, rr));
                break;
            }

        // ── ВОРОТА ИЗ ПОЯВЛЯЮЩИХСЯ ПЛАТФОРМ ──────────────────────────────────────────────────────
        // Ступенька вертикального прохода строится из платформ группы. Без нажатой кнопки подъём с пола
        // до потолка = H+1 рядов; при H ≥ 4 это больше дотяжки ↑4, значит зона за проходом недостижима.
        // Обхода нет ПО ПОСТРОЕНИЮ: лабиринт — остовное дерево, другого пути в ту комнату не существует.
        // Кнопка ставится с ОБЕИХ сторон: снизу — открыть проход, внутри зоны — вернуться тем же путём.
        // ⛔ Намеренные провалы в полу как механику не используем (решение игрока).
        System.Func<Vector2Int,Vector2Int> findFloorSpot = room =>
        {
            int rAir = R0(room.y) + H(room.y) - 1, w = W(room.x);
            for (int d = 0; d < w; d++)
            {
                int off = offFromMid(w, d);
                if (off < 0 || off >= w) continue;
                int c = C0(room.x) + off;
                if (at(rAir, c) == '.' && at(rAir + 1, c) == '#' && mainAir[rAir, c]) return new Vector2Int(c, rAir);
            }
            return new Vector2Int(-1, -1);
        };
        // Комнаты по одну сторону от снятого ребра шахты (обход дерева без этого ребра).
        System.Func<Vector2Int,Vector2Int,System.Collections.Generic.HashSet<Vector2Int>> sideWithout =
            (a, b) =>
        {
            var seen2 = new System.Collections.Generic.HashSet<Vector2Int> { a };
            var q3 = new System.Collections.Generic.Queue<Vector2Int>(); q3.Enqueue(a);
            while (q3.Count > 0)
            {
                var cur = q3.Dequeue();
                foreach (var nb3 in nbrs(cur))
                {
                    if ((cur.Equals(a) && nb3.Equals(b)) || (cur.Equals(b) && nb3.Equals(a))) continue;
                    if (!seen2.Add(nb3)) continue;
                    q3.Enqueue(nb3);
                }
            }
            return seen2;
        };
        var gateGroups = new System.Collections.Generic.List<char>();
        int maxGates = Mathf.Clamp((CW * CH) / 8, 1, 3);
        // ⭐ ЦЕПОЧКА (выбор игрока): второй проход ставит платформы ВНУТРИ уже запертой зоны — тогда
        // к ключу ведёт последовательность «нажал A, поднялся, нажал B, поднялся ещё». Первый проход
        // сажает ворота где угодно, дальше приоритет у продолжений цепочки; если таких нет — обычные.
        var gatedZones = new System.Collections.Generic.List<System.Collections.Generic.HashSet<Vector2Int>>();
        for (int chainPass = 0; chainPass < 2 && gateGroups.Count < maxGates; chainPass++)
        foreach (var sh in shafts)
        {
            if (gateGroups.Count >= maxGates) break;
            var below = sh.room;
            if (chainPass == 0 && gatedZones.Count > 0)
            {
                bool beyond = false;
                foreach (var z in gatedZones) if (z.Contains(below)) { beyond = true; break; }
                if (!beyond) continue;                            // на первом проходе — только вглубь цепочки
            }
            var above = new Vector2Int(sh.room.x, sh.room.y + 1);
            if (H(below.y) < 4) continue;                       // при H=3 без ступеньки всё равно долезут
            if (usedRooms.Contains(below) || usedRooms.Contains(above)) continue;
            var lower = sideWithout(below, above);
            if (!lower.Contains(startRoom)) continue;              // старт обязан остаться СНИЗУ от ворот
            if (lower.Contains(above)) continue;                  // ребро не разрезало дерево
            bool worth = !lower.Contains(far);                    // за воротами финиш…
            foreach (var kr in keyRooms) if (!lower.Contains(kr)) worth = true;   // …или ключ
            if (!worth) continue;
            var spotBelow = findFloorSpot(below);
            var spotAbove = findFloorSpot(above);
            if (spotBelow.x < 0 || spotAbove.x < 0) continue;     // некуда поставить пару кнопок
            // ⚠️ Платформа НЕ должна лепиться вплотную к земле (фидбэк с плейтеста): при climb=2 ступенька
            // висела в одной клетке над полом — бессмысленно и некрасиво. Ставим её ровно в 3 ряда над
            // полом (2 пустых ряда под ней): подъём пол→платформа = 3 ≤ MazeClimb, платформа→верхний пол
            // = H−2 ≤ 3 при H ≤ 5. Если исходная ступенька была ниже — переносим.
            int floorRow = R0(below.y) + H(below.y);
            int stepRow  = floorRow - MazeClimb;
            if (stepRow <= R0(below.y)) continue;                 // упёрлась бы в потолок — не эта шахта
            // Под платформой нужны 2 пустых ряда. Мешать может ДЕКОР комнаты (тумба/пилон) — его сносим;
            // если под платформой настоящий камень, эту шахту пропускаем, а не лепим платформу к земле.
            bool groundBusy = false;
            var toClear = new System.Collections.Generic.List<Vector2Int>();
            for (int k = 0; k < sh.width && !groundBusy; k++)
            for (int dr = 1; dr <= 2; dr++)
            {
                int c = sh.col0 + k, r2 = stepRow + dr;
                if (at(r2, c) != '#') continue;
                if (bump[r2, c]) toClear.Add(new Vector2Int(c, r2));
                else { groundBusy = true; break; }
            }
            if (groundBusy) continue;
            foreach (var p in toClear)
            {
                set(p.y, p.x, '.');
                if (makesPinch(p.y, p.x)) { set(p.y, p.x, '#'); groundBusy = true; break; }
                bump[p.y, p.x] = false;
            }
            if (groundBusy) continue;                             // снос декора рождал зажим — не эта шахта
            char grp = (char)('A' + gateGroups.Count);
            for (int k = 0; k < sh.width; k++)
            {
                int c = sh.col0 + k;
                if (sh.stepRow != stepRow && at(sh.stepRow, c) == '#') set(sh.stepRow, c, '.');  // убрать старую
                if (at(stepRow, c) == '.' || at(stepRow, c) == '#') set(stepRow, c, char.ToLower(grp));
            }
            set(spotBelow.y, spotBelow.x, grp);                   // кнопка «открыть»
            set(spotAbove.y, spotAbove.x, grp);                   // кнопка «вернуться»
            gateGroups.Add(grp);
            usedRooms.Add(below); usedRooms.Add(above);
            // Зона за этими воротами — в неё будет целиться следующая группа, чтобы получилась цепочка.
            var beyondZone = new System.Collections.Generic.HashSet<Vector2Int>();
            for (int x = 0; x < CW; x++) for (int y = 0; y < CH; y++)
            {
                var p = new Vector2Int(x, y);
                if (alive[x, y] && !lower.Contains(p)) beyondZone.Add(p);
            }
            gatedZones.Add(beyondZone);
        }

        // ── ГОРИЗОНТАЛЬНЫЙ МОСТ (идея игрока): у комнаты убирается ПОЛ, а платформы группы кладутся
        // на его место. Нажал кнопку — пол появился, перебежал; не успел — провалился.
        // Цена ошибки двух видов, вперемежку (выбор игрока):
        //   • под комнатой есть этаж → падаешь в него, цел, возвращаешься в обход по лабиринту
        //     (это всегда возможно: лабиринт — связное дерево);
        //   • комната в нижнем ряду → прорезаем оболочку вниз, падение в ПРОПАСТЬ = смерть и респавн
        //     на чекпоинте. Возврат тут не нужен вовсе.
        // ⚠️ Раньше игрок отверг «намеренный провал» — но там провал был способом ЗАПЕРЕТЬ проход.
        // Здесь он цена ошибки на таймере, это другая роль (уточнено 2026-07-18).
        {
            // ⚠️ МОСТ ОБЯЗАН ИМЕТЬ СМЫСЛ (фидбэк игрока: «пропасть бессмысленна, триггер можно обойти»).
            // Для вертикальных подъёмов осмысленность проверяется разрезом дерева, а мост я ставил просто
            // по признаку «широкий пол» — и он оказывался в стороне от маршрута. Условие: комната должна
            // быть СКВОЗНЫМ ГОРИЗОНТАЛЬНЫМ КОРИДОРОМ (вход слева, выход справа, вертикальных проходов нет)
            // И лежать НА МАРШРУТЕ спавн→финиш. Тогда пересечь её обязательно, обойти мост нельзя.
            // Связаны ли две комнаты, если ИСКЛЮЧИТЬ третью (нужно, чтобы понять, по какую сторону моста
            // лежит комната под ним).
            System.Func<Vector2Int,Vector2Int,Vector2Int,bool> connectedWithout = (excl, from, to) =>
            {
                if (from.Equals(excl) || to.Equals(excl)) return false;
                var seen3 = new System.Collections.Generic.HashSet<Vector2Int> { from };
                var q5 = new System.Collections.Generic.Queue<Vector2Int>(); q5.Enqueue(from);
                while (q5.Count > 0)
                {
                    var cur = q5.Dequeue();
                    if (cur.Equals(to)) return true;
                    foreach (var nb4 in nbrs(cur)) { if (nb4.Equals(excl) || !seen3.Add(nb4)) continue; q5.Enqueue(nb4); }
                }
                return seen3.Contains(to);
            };
            for (int pi = 1; pi < path.Count - 1; pi++)
            {
                var room = path[pi];
                if (gateGroups.Count >= maxGates + 1) break;
                if (room.Equals(startRoom) || room.Equals(far) || usedRooms.Contains(room)) continue;
                if (!alive[room.x, room.y] || W(room.x) < 5) continue;        // нужен пролёт + опоры по краям
                bool hLeft  = room.x > 0 && hPass[room.x - 1, room.y];
                bool hRight = room.x + 1 < CW && hPass[room.x, room.y];
                bool vUp    = room.y + 1 < CH && vPass[room.x, room.y];
                bool vDown  = room.y > 0 && vPass[room.x, room.y - 1];
                if (!hLeft || !hRight || vUp || vDown) continue;              // не сквозной коридор — обойдут
                // ⭐ ПАДЕНИЕ = ОТКАТ, А НЕ СРЕЗКА (требование игрока). Убирая пол, я создаю дыру в комнату
                // снизу — связь, которой в дереве не было. Если та комната лежит на стороне ФИНИША,
                // падение уносит игрока ВПЕРЁД мимо моста, и механика превращается в короткий путь.
                // Требуем, чтобы комната под мостом была на стороне СПАВНА: тогда упавший возвращается
                // к уже пройденному и идёт к кнопке заново. Третий случай (комната не связана ни с той,
                // ни с другой стороной без этой комнаты) отвергаем — там можно застрять.
                if (room.y > 0 && alive[room.x, room.y - 1]
                    && !connectedWithout(room, path[pi - 1], new Vector2Int(room.x, room.y - 1))) continue;
                int floorRow = R0(room.y) + H(room.y), c0 = C0(room.x) + 1, c1 = C0(room.x) + W(room.x) - 2;
                bool ok = true;
                for (int c = c0; c <= c1 && ok; c++) if (at(floorRow, c) != '#') ok = false;
                if (!ok) continue;
                bool hasFloorBelow = room.y > 0 && alive[room.x, room.y - 1];
                char grp = (char)('A' + gateGroups.Count);
                // Журнал изменений: мост строится ПОСЛЕ расшивки зажимов, поэтому может их создать
                // (например убранный пол встречается со сталактитом комнаты снизу). Если так — полный откат.
                var undo = new System.Collections.Generic.List<(int r, int c, char ch)>();
                System.Action<int,int,char> put2 = (r, c, ch) =>
                { if (r >= 0 && r < rows && c >= 0 && c < cols) { undo.Add((r, c, g[r, c])); set(r, c, ch); } };

                // Декор, стоявший НА полу пролёта, иначе повиснет в воздухе над мостом (и даст зажим).
                for (int c = c0; c <= c1; c++)
                    if (bump[floorRow - 1, c]) { put2(floorRow - 1, c, '.'); bump[floorRow - 1, c] = false; }
                for (int c = c0; c <= c1; c++) put2(floorRow, c, char.ToLower(grp));
                if (!hasFloorBelow)                                           // ПРОПАСТЬ: режем оболочку вниз
                    for (int c = c0; c <= c1; c++)
                    for (int r = floorRow + 1; r < rows; r++)
                        if (at(r, c) == '#') put2(r, c, ' '); else break;
                // Кнопки по обоим концам моста — на уцелевших опорах.
                int rAirB = floorRow - 1, cRight = C0(room.x) + W(room.x) - 1;
                bool b1 = at(rAirB, C0(room.x)) == '.' && at(floorRow, C0(room.x)) == '#';
                bool b2 = at(rAirB, cRight) == '.' && at(floorRow, cRight) == '#';
                if (b1) put2(rAirB, C0(room.x), grp);
                if (b2) put2(rAirB, cRight, grp);

                bool pinched = false;
                for (int r = floorRow - 2; r <= floorRow + 2 && !pinched; r++)
                for (int c = c0 - 2; c <= c1 + 2 && !pinched; c++)
                    if (makesPinch(r, c)) pinched = true;

                if (!b1 || !b2 || pinched)                                    // мост не сложился — откат
                {
                    for (int i = undo.Count - 1; i >= 0; i--) set(undo[i].r, undo[i].c, undo[i].ch);
                    continue;
                }
                gateGroups.Add(grp); usedRooms.Add(room);
                break;
            }
        }

        // ── МОНЕТЫ: дорожка-приманка в тупиковые ветки (награда за исследование) + немного на маршруте.
        int coins = 0;
        int coinBudget = Mathf.Max(0, Mathf.RoundToInt(_coinBudget * (0.85f + (float)rng.NextDouble() * 0.3f)));
        System.Action<Vector2Int,int> dropCoins = (room, count) =>
        {
            if (!alive[room.x, room.y]) return;
            int rAir = R0(room.y) + H(room.y) - 1, w = W(room.x);
            for (int dy = 0; dy < 2 && count > 0; dy++)      // два ряда: над полом и на клетку выше
            for (int d = 0; d < w && count > 0; d++)
            {
                int off = offFromMid(w, d);
                if (off < 0 || off >= w) continue;
                int c = C0(room.x) + off, r = rAir - dy;
                if (at(r, c) != '.' || !mainAir[r, c]) continue;
                if (at(r, c - 1) == '*' || at(r, c + 1) == '*') continue;   // не занимать зазор ключа
                set(r, c, '$'); coins++; count--;
            }
        };
        // Приоритет: тупики (приманка за исследование) → маршрут → все остальные комнаты.
        foreach (var room in deadEnds) { if (coins >= coinBudget) break; dropCoins(room, 3 + rng.Next(3)); }
        for (int i = 1; i < path.Count - 1 && coins < coinBudget; i++) dropCoins(path[i], 2 + rng.Next(3));
        for (int x = 0; x < CW && coins < coinBudget; x++)
        for (int y = 0; y < CH && coins < coinBudget; y++)
            dropCoins(new Vector2Int(x, y), 2 + rng.Next(3));

        // ── ОБРЕЗКА ЛИШНЕГО КАМНЯ ────────────────────────────────────────────────────────────────
        // Оставляем оболочку RockShell клеток вокруг вырезанного пространства, остальное — пустота.
        // Без этого силуэт уровня ВСЕГДА прямоугольный (массив камня целиком), и форма лабиринта
        // не видна снаружи. Побочно: на неправильных формах резко меньше тайлов в сцене.
        // ⚠️ Толщина оболочки прямо определяет, ВИДНА ли форма снаружи. При 3 клетках пропущенная
        // комната (3-5 рядов) целиком попадала в запас и зарастала камнем — силуэт оставался
        // прямоугольным, сколько формы ни генерируй (фидбэк игрока). 2 клетки: масса ещё читается
        // как скала, но вырезы видно.
        const int RockShell = 2;
        {
            var dRock = new int[rows, cols];
            for (int r = 0; r < rows; r++) for (int c = 0; c < cols; c++) dRock[r, c] = int.MaxValue;
            var q4 = new System.Collections.Generic.Queue<Vector2Int>();
            for (int r = 0; r < rows; r++)
            for (int c = 0; c < cols; c++)
                if (g[r, c] != '#') { dRock[r, c] = 0; q4.Enqueue(new Vector2Int(c, r)); }
            while (q4.Count > 0)
            {
                var cur = q4.Dequeue();
                for (int k = 0; k < 4; k++)
                {
                    int nr = cur.y + (k == 0 ? -1 : k == 1 ? 1 : 0), nc = cur.x + (k == 2 ? -1 : k == 3 ? 1 : 0);
                    if (nr < 0 || nr >= rows || nc < 0 || nc >= cols || dRock[nr, nc] != int.MaxValue) continue;
                    dRock[nr, nc] = dRock[cur.y, cur.x] + 1; q4.Enqueue(new Vector2Int(nc, nr));
                }
            }
            // ⚠️ Срезаем только камень, связанный с ВНЕШНЕЙ границей. Иначе середина мёртвой клетки
            // сетки (блок 5-8 × 3-5) оказывалась дальше оболочки и выедалась изнутри — получалась
            // ЗАПЕЧАТАННАЯ камера, визуально неотличимая от комнаты, куда невозможно попасть. Игрок
            // справедливо назвал такой уровень непроходимым (2026-07-18).
            var outside = new bool[rows, cols];
            var qOut = new System.Collections.Generic.Queue<Vector2Int>();
            System.Action<int,int> seed = (r, c) =>
            {
                if (r < 0 || r >= rows || c < 0 || c >= cols || outside[r, c]) return;
                if (g[r, c] != '#' || dRock[r, c] <= RockShell) return;
                outside[r, c] = true; qOut.Enqueue(new Vector2Int(c, r));
            };
            for (int c = 0; c < cols; c++) { seed(0, c); seed(rows - 1, c); }
            for (int r = 0; r < rows; r++) { seed(r, 0); seed(r, cols - 1); }
            while (qOut.Count > 0)
            {
                var cur = qOut.Dequeue();
                for (int k = 0; k < 4; k++)
                    seed(cur.y + (k == 0 ? -1 : k == 1 ? 1 : 0), cur.x + (k == 2 ? -1 : k == 3 ? 1 : 0));
            }
            for (int r = 0; r < rows; r++)
            for (int c = 0; c < cols; c++)
                if (outside[r, c]) g[r, c] = ' ';

            // ⚠️ Обрезка идёт ПОСЛЕ расшивки зажимов и сама может оставить два тайла углом к углу
            // (поле расстояний даёт диагональные ступеньки). Возвращаем такие клетки обратно в камень —
            // проверка зажимов не разбирает, игровая это зона или внешняя оболочка.
            for (int iter = 0; iter < 8; iter++)
            {
                int restored = 0;
                for (int r = 0; r + 1 < rows; r++)
                for (int c = 0; c + 1 < cols; c++)
                {
                    bool tl = g[r, c] == '#',     tr = g[r, c + 1] == '#';
                    bool bl = g[r + 1, c] == '#', br = g[r + 1, c + 1] == '#';
                    if (!((tl && br && !tr && !bl) || (tr && bl && !tl && !br))) continue;
                    if (g[r, c + 1] == ' ')     { g[r, c + 1] = '#'; restored++; }
                    else if (g[r + 1, c] == ' ') { g[r + 1, c] = '#'; restored++; }
                    else if (g[r, c] == ' ')     { g[r, c] = '#'; restored++; }
                    else if (g[r + 1, c + 1] == ' ') { g[r + 1, c + 1] = '#'; restored++; }
                }
                if (restored == 0) break;
            }
        }

        var sb = new System.Text.StringBuilder();
        for (int r = 0; r < rows; r++) { for (int c = 0; c < cols; c++) sb.Append(g[r, c]); sb.Append('\n'); }
        return sb.ToString();
    }

    /// <summary>
    /// Обратный конвертер: уровень (root) → ASCII-схема. Определяет шаг сетки по тайлам, квантует позиции
    /// объектов в грид, эмитит символы (#/*/$/@/^/=/A-Z/a-z). Для изучения/правки существующих уровней.
    /// Финиш/чекпоинт помечаются в их позиции (не идеальный round-trip — флаги при реимпорте «падают»).
    /// </summary>
    private string ExportScheme(GameObject root)
    {
        if (root == null) return "";
        var tiles = new System.Collections.Generic.List<Transform>();
        var disTiles = new System.Collections.Generic.Dictionary<Transform, string>();
        var arts = new System.Collections.Generic.List<Transform>();
        var coins = new System.Collections.Generic.List<Transform>();
        var buttons = new System.Collections.Generic.Dictionary<Transform, string>();
        Transform spawn = null, finish = null, checkp = null;
        foreach (Transform grp in root.transform)
        {
            switch (grp.name)
            {
                case "Tiles":     foreach (Transform t in grp) tiles.Add(t); break;
                case "Artifacts": foreach (Transform t in grp) arts.Add(t);  break;
                case "Coins":     foreach (Transform t in grp) coins.Add(t); break;
                case "SpawnPoint": spawn = grp; break;
                case "Flag_finish": finish = grp; break;
                case "Disappearing":
                    foreach (Transform cont in grp)
                    { var dp = cont.GetComponent<DisappearingPlatform>(); string gid = dp != null ? dp.groupId : "A";
                      foreach (Transform t in cont) disTiles[t] = gid; }
                    break;
                case "Triggers": case "Button":
                    foreach (Transform t in grp) { var tt = t.GetComponentInChildren<TriggerTile>(true); buttons[t] = tt != null ? tt.groupId : "A"; }
                    break;
                case "Checkpoints": case "Flag":
                    if (grp.childCount > 0) checkp = grp.GetChild(0); break;
            }
        }
        if (finish == null) finish = root.transform.Find("Flag_finish");
        if (checkp == null) checkp = root.transform.Find("Flag_checkpoint");

        // Шаг сетки = мода дельт X соседних тайлов (иначе _tileCell).
        var xs = new System.Collections.Generic.List<float>(); foreach (var t in tiles) xs.Add(t.position.x); xs.Sort();
        var deltas = new System.Collections.Generic.Dictionary<float, int>();
        for (int i = 1; i < xs.Count; i++)
        { float d = Mathf.Round((xs[i] - xs[i - 1]) * 100f) / 100f; if (d > 0.05f) deltas[d] = deltas.TryGetValue(d, out int v) ? v + 1 : 1; }
        float cell = _tileCell; int best = 0; foreach (var kv in deltas) if (kv.Value > best) { best = kv.Value; cell = kv.Key; }
        if (cell < 0.1f) cell = _tileCell;

        var cells = new System.Collections.Generic.Dictionary<(int, int), char>();
        int minX = int.MaxValue, maxX = int.MinValue, minY = int.MaxValue, maxY = int.MinValue;
        System.Action<Transform, char> put = (t, ch) =>
        {
            if (t == null) return;
            int x = Mathf.RoundToInt(t.position.x / cell), y = Mathf.RoundToInt(t.position.y / cell);
            if (!cells.ContainsKey((x, y)) || ch != '#') cells[(x, y)] = ch; // не-тайл перекрывает тайл
            if (x < minX) minX = x; if (x > maxX) maxX = x; if (y < minY) minY = y; if (y > maxY) maxY = y;
        };
        foreach (var t in tiles) put(t, '#');
        foreach (var kv in disTiles) put(kv.Key, char.ToLower(kv.Value.Length > 0 ? kv.Value[0] : 'a'));
        foreach (var t in arts) put(t, '*');
        foreach (var t in coins) put(t, '$');
        foreach (var kv in buttons) put(kv.Key, char.ToUpper(kv.Value.Length > 0 ? kv.Value[0] : 'A'));
        put(spawn, '@'); put(finish, '^'); put(checkp, '=');

        if (minX > maxX) return "";
        var sb = new System.Text.StringBuilder();
        for (int y = maxY; y >= minY; y--)
        {
            for (int x = minX; x <= maxX; x++) sb.Append(cells.TryGetValue((x, y), out char c) ? c : '.');
            sb.Append('\n');
        }
        return sb.ToString();
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
                // Кнопка — на ПОВЕРХНОСТЬ полки под символом (как флаг).
                Vector3 bp = SurfaceBelow(grid, rows, r, c, cell, world, p);
                var go = PlaceFromPrefab(_pfButton, bp, GetGroup("Triggers"), "Trigger");
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

        // FallCollider под низ грида (ниже пола на запас — иначе тело на спавне сразу в зоне смерти)
        var fall = _root.transform.Find("FallCollider");
        if (fall != null) fall.position = new Vector3((grid[rows - 1].Length) * cell * 0.5f, -5f, 0f);

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
        CheckPlacementRules(grid, rows);
        CheckDiagonalPinch(grid, rows);
        CheckTriggerGroups(grid, rows);
        CheckTriggerReturn(grid, rows);
        ApplyTriggerWindows(grid, rows);
        Repaint();
    }

    /// <summary>
    /// Диагональный зажим: два тайла соприкасаются УГЛАМИ, между ними щель нулевой ширины. Тайлы и пэды
    /// физические — пэд туда не пролезает, место непроходимо (фидбэк игрока 2026-07-18: так генератор
    /// выдал непроходимый Level_04, 4 таких точки). Проверка проходимости это НЕ ловит: wallBetween
    /// смотрит колонки МЕЖДУ холдами, а у диагонали их нет.
    /// </summary>
    private void CheckDiagonalPinch(char[][] grid, int rows)
    {
        // Считаем только КАМЕНЬ: появляющиеся платформы (`a`-`z`) твёрдые лишь на несколько секунд,
        // зажим с их участием игрок просто пережидает.
        System.Func<char,bool> rock = ch => ch == '#';
        int pinch = 0; var where = new System.Text.StringBuilder();
        for (int r = 0; r + 1 < rows; r++)
        for (int c = 0; c + 1 < grid[r].Length; c++)
        {
            bool tl = rock(CellAt(grid, r, c)),     tr = rock(CellAt(grid, r, c + 1));
            bool bl = rock(CellAt(grid, r + 1, c)), br = rock(CellAt(grid, r + 1, c + 1));
            if (!((tl && br && !tr && !bl) || (tr && bl && !tl && !br))) continue;
            pinch++;
            if (pinch <= 8) where.Append($" (ряд {r}, колонка {c})");
        }
        if (pinch > 0)
            Debug.LogWarning($"[Зажим] Диагональных зажимов: {pinch}{where} — тайлы касаются углами, " +
                "пэд между ними не пролезет. Сдвинь тайл или убери один из пары (щель должна быть по прямой).");
    }

    /// <summary>
    /// Появляющиеся платформы должны ОТКРЫВАТЬ путь, а не перекрывать его. Для каждой группы (`a`-`z`)
    /// моделируем ВКЛЮЧЁННОЕ состояние (тайлы группы = камень) и проверяем, что от спавна по воздуху
    /// по-прежнему достижимы финиш, чекпоинт и артефакты. Ловит случай «нажал кнопку — замуровал проход»
    /// (фидбэк игрока 2026-07-18).
    /// </summary>
    private void CheckTriggerGroups(char[][] grid, int rows)
    {
        int maxCol = 0; for (int r = 0; r < rows; r++) if (grid[r].Length > maxCol) maxCol = grid[r].Length;
        var groups = new System.Collections.Generic.HashSet<char>();
        int spR = -1, spC = -1;
        for (int r = 0; r < rows; r++)
        for (int c = 0; c < grid[r].Length; c++)
        {
            char ch = grid[r][c];
            if (ch >= 'a' && ch <= 'z') groups.Add(ch);
            if (ch == '@') { spR = r; spC = c; }
        }
        if (groups.Count == 0 || spR < 0) return;

        foreach (char g in groups)
        {
            var seen = new bool[rows, maxCol];
            var q = new System.Collections.Generic.Queue<Vector2Int>();
            seen[spR, spC] = true; q.Enqueue(new Vector2Int(spC, spR));
            while (q.Count > 0)
            {
                var cur = q.Dequeue();
                for (int k = 0; k < 4; k++)
                {
                    int nr = cur.y + (k == 0 ? -1 : k == 1 ? 1 : 0), nc = cur.x + (k == 2 ? -1 : k == 3 ? 1 : 0);
                    if (nr < 0 || nr >= rows || nc < 0 || nc >= maxCol || seen[nr, nc]) continue;
                    char ch = CellAt(grid, nr, nc);
                    if (ch == '#' || ch == g) continue;                  // группа ВКЛЮЧЕНА = камень
                    seen[nr, nc] = true; q.Enqueue(new Vector2Int(nc, nr));
                }
            }
            var lost = new System.Text.StringBuilder(); int lostCount = 0;
            for (int r = 0; r < rows; r++)
            for (int c = 0; c < grid[r].Length; c++)
            {
                char ch = grid[r][c];
                if (ch != '^' && ch != '=' && ch != '*') continue;
                if (seen[r, c]) continue;
                lostCount++;
                if (lostCount <= 5) lost.Append($" '{ch}'(ряд {r}, кол {c})");
            }
            if (lostCount > 0)
                Debug.LogWarning($"[Триггер] Группа '{char.ToUpper(g)}': во ВКЛЮЧЁННОМ состоянии платформы " +
                    $"перекрывают путь — недостижимо{lost}. Кнопка должна ОТКРЫВАТЬ проход: сдвинь платформы " +
                    "с коридора или оставь обход.");
        }
    }

    /// <summary>
    /// Гарантия ВОЗВРАТА: если группа платформ запирает зону (с выключенной группой часть уровня
    /// недостижима), то ВНУТРИ этой зоны обязана быть кнопка той же группы — иначе игрок, взяв ключ,
    /// останется там навсегда (требование игрока 2026-07-18: «удостовериться, что обратно доберётся»).
    /// Зона считается по ВОЗДУХУ + климбу: моделируем выключенное состояние (платформы сквозные и НЕ
    /// холды) и смотрим, докуда игрок дотягивается от спавна.
    /// </summary>
    private void CheckTriggerReturn(char[][] grid, int rows)
    {
        int maxCol = 0; for (int r = 0; r < rows; r++) if (grid[r].Length > maxCol) maxCol = grid[r].Length;
        var groups = new System.Collections.Generic.HashSet<char>();
        int spR = -1, spC = -1;
        for (int r = 0; r < rows; r++)
        for (int c = 0; c < grid[r].Length; c++)
        {
            char ch = grid[r][c];
            if (ch >= 'a' && ch <= 'z') groups.Add(ch);
            if (ch == '@') { spR = r; spC = c; }
        }
        if (groups.Count == 0 || spR < 0) return;

        foreach (char g in groups)
        {
            // Холды при ВЫКЛЮЧЕННОЙ группе: камень + платформы ДРУГИХ групп (эта — сквозная).
            System.Func<int,int,bool> solidOff = (r, c) =>
            { char ch = CellAt(grid, r, c); return ch == '#' || (ch >= 'a' && ch <= 'z' && ch != g); };
            var reach = new bool[rows, maxCol];
            var q = new System.Collections.Generic.Queue<Vector2Int>();
            reach[spR, spC] = true; q.Enqueue(new Vector2Int(spC, spR));
            while (q.Count > 0)   // воздух (4 стороны) + подъём/спуск в пределах дотяжки по колонне
            {
                var cur = q.Dequeue();
                for (int k = 0; k < 4; k++)
                {
                    int nr = cur.y + (k == 0 ? -1 : k == 1 ? 1 : 0), nc = cur.x + (k == 2 ? -1 : k == 3 ? 1 : 0);
                    if (nr < 0 || nr >= rows || nc < 0 || nc >= maxCol || reach[nr, nc] || solidOff(nr, nc)) continue;
                    // Вверх — только если рядом есть за что держаться в пределах дотяжки.
                    if (k == 0)
                    {
                        bool hold = false;
                        for (int d = 1; d <= Mathf.CeilToInt(_reachUpCells) && !hold; d++)
                            if (solidOff(nr + d, nc - 1) || solidOff(nr + d, nc + 1) || solidOff(nr, nc - 1) || solidOff(nr, nc + 1))
                                hold = true;
                        if (!hold) continue;
                    }
                    reach[nr, nc] = true; q.Enqueue(new Vector2Int(nc, nr));
                }
            }
            // Что осталось за воротами и есть ли там кнопка возврата.
            bool gated = false, hasReturn = false;
            var lost = new System.Text.StringBuilder(); int lostCount = 0;
            char btn = char.ToUpper(g);
            for (int r = 0; r < rows; r++)
            for (int c = 0; c < grid[r].Length; c++)
            {
                char ch = grid[r][c];
                if (reach[r, c]) continue;
                if (ch == btn) { hasReturn = true; continue; }
                if (ch != '*' && ch != '^' && ch != '=') continue;
                gated = true; lostCount++;
                if (lostCount <= 4) lost.Append($" '{ch}'(ряд {r}, кол {c})");
            }
            if (gated && !hasReturn)
                Debug.LogWarning($"[Триггер] Группа '{btn}': за воротами есть{lost}, но кнопки '{btn}' " +
                    "ВНУТРИ зоны нет — игрок войдёт и не сможет вернуться тем же путём. Поставь дублирующую кнопку.");
        }
    }

    /// <summary>
    /// Окно активности группы = запас + длина пути / скорость климба. Импортёр раньше его вообще не
    /// задавал (оставался дефолт префаба), из-за чего короткая перебежка и длинный подъём получали
    /// одинаковые 5 секунд. Длина пути меряется BFS по ВОЗДУХУ от кнопки до самой дальней платформы
    /// группы (+2 клетки на подъём мимо неё). Нужно для будущей полностью процедурной генерации
    /// (live-ops бонусный уровень) — окно должно подбираться само.
    /// </summary>
    private void ApplyTriggerWindows(char[][] grid, int rows)
    {
        int maxCol = 0; for (int r = 0; r < rows; r++) if (grid[r].Length > maxCol) maxCol = grid[r].Length;
        var groups = new System.Collections.Generic.HashSet<char>();
        for (int r = 0; r < rows; r++)
        for (int c = 0; c < grid[r].Length; c++)
            if (grid[r][c] >= 'a' && grid[r][c] <= 'z') groups.Add(grid[r][c]);

        foreach (char g in groups)
        {
            char btn = char.ToUpper(g);
            var dist = new int[rows, maxCol];
            for (int r = 0; r < rows; r++) for (int c = 0; c < maxCol; c++) dist[r, c] = -1;
            var q = new System.Collections.Generic.Queue<Vector2Int>();
            for (int r = 0; r < rows; r++)
            for (int c = 0; c < grid[r].Length; c++)
                if (grid[r][c] == btn) { dist[r, c] = 0; q.Enqueue(new Vector2Int(c, r)); }
            if (q.Count == 0) continue;                       // группа без кнопки — окно не трогаем
            while (q.Count > 0)
            {
                var cur = q.Dequeue();
                for (int k = 0; k < 4; k++)
                {
                    int nr = cur.y + (k == 0 ? -1 : k == 1 ? 1 : 0), nc = cur.x + (k == 2 ? -1 : k == 3 ? 1 : 0);
                    if (nr < 0 || nr >= rows || nc < 0 || nc >= maxCol || dist[nr, nc] >= 0) continue;
                    if (CellAt(grid, nr, nc) == '#') continue;
                    dist[nr, nc] = dist[cur.y, cur.x] + 1; q.Enqueue(new Vector2Int(nc, nr));
                }
            }
            int far = 0;
            for (int r = 0; r < rows; r++)
            for (int c = 0; c < grid[r].Length; c++)
                if (grid[r][c] == g && dist[r, c] > far) far = dist[r, c];

            float window = Mathf.Clamp(_platBuffer + (far + 2) / Mathf.Max(0.1f, _platSpeed), 3f, 30f);
            string gid = btn.ToString();
            var parent = _root != null ? _root.transform.Find("Disappearing") : null;
            if (parent == null) continue;
            foreach (Transform ch in parent)
            {
                var dp = ch.GetComponent<DisappearingPlatform>();
                if (dp == null || dp.groupId != gid) continue;
                dp.activeWindow = window;
                dp.warningTime  = Mathf.Min(dp.warningTime, window * 0.5f);
                EditorUtility.SetDirty(dp);
                Debug.Log($"[Триггер] Группа '{gid}': путь {far} клеток → окно {window:F1} сек " +
                          $"(скорость {_platSpeed:F1} кл/с, запас {_platBuffer:F1} с).");
            }
        }
    }

    /// <summary>Проверки размещения: ключ (~2 тайла шириной) не в тесноте; спавн не у самого края и с полом под ним.</summary>
    private void CheckPlacementRules(char[][] grid, int rows)
    {
        for (int r = 0; r < rows; r++)
        for (int c = 0; c < grid[r].Length; c++)
        {
            char ch = grid[r][c];
            if (ch == '*')
            {
                // Замер: ключ 0.80 × 0.37 юнита — при cell 0.5 он ~1.6 клетки ШИРИНОЙ и вылезает в
                // соседние клетки по горизонтали. Поэтому слева и справа должно быть ПУСТО — не только
                // от тайлов, но и от монет/кнопок/других ключей, иначе спрайты пересекаются.
                char l = CellAt(grid, r, c - 1), rt = CellAt(grid, r, c + 1);
                System.Func<char,bool> busy = ch2 => ch2 != '.' && ch2 != ' ';
                if (busy(l) || busy(rt))
                    Debug.LogWarning($"[Артефакт] ряд {r}, колонка {c}: ключ шире своей клетки, а рядом " +
                        $"{(busy(l) ? $"слева '{l}'" : "")}{(busy(l) && busy(rt) ? " и " : "")}{(busy(rt) ? $"справа '{rt}'" : "")}" +
                        " — спрайты пересекутся. Нужна пустая клетка с обеих сторон.");
            }
            else if (ch == '@')
            {
                bool floorBelow = IsSolidCell(CellAt(grid, r + 1, c)) || IsSolidCell(CellAt(grid, r + 2, c));
                bool edge = c <= 0 || c >= grid[r].Length - 1;
                if (edge || !floorBelow)
                    Debug.LogWarning($"[Спавн] колонка {c}: не ставь у самого края и без пола под ним — нужен пол снизу и тайлы с обеих сторон.");
            }
        }
    }

    // Флаг (финиш/чекпоинт) высокий (~2 юнита) — над его полкой нужно свободное место, иначе верхушка
    // (баннер) прячется за тайлами сверху. Правило: над базой флага должно быть ~FlagClearWorld пусто.
    // Замер префабов (2026-07-18): пивот флага в ОСНОВАНИИ СЛЕВА (0.02, 0), спрайт 0.89 × 1.16 юнита.
    // Значит баннер уходит ВПРАВО (~2 клетки при cell 0.5) и ВВЕРХ (~3 клетки), а влево — ничего, там
    // древко. Поэтому клиренс правосторонний, а не симметричный, как было раньше.
    private const float FlagClearWorld = 1.5f;   // вверх
    private const float FlagRightWorld = 1.0f;   // вправо (баннер)
    private void CheckFlagClearance(char[][] grid, int rows, float cell)
    {
        int clear = Mathf.Max(2, Mathf.CeilToInt(FlagClearWorld / Mathf.Max(0.01f, cell)));
        int right = Mathf.Max(2, Mathf.CeilToInt(FlagRightWorld / Mathf.Max(0.01f, cell)));
        for (int r = 0; r < rows; r++)
        for (int c = 0; c < grid[r].Length; c++)
        {
            char ch = grid[r][c];
            if (ch != '^' && ch != '=') continue;
            int rr = r + 1;
            while (rr < rows && !IsSolidCell(CellAt(grid, rr, c))) rr++;      // база = первая сплошная ниже
            int baseRow = (rr < rows && IsSolidCell(CellAt(grid, rr, c))) ? rr : r;
            string who = ch == '^' ? "Финиш" : "Чекпоинт";
            for (int k = 1; k <= clear; k++)
                if (IsSolidCell(CellAt(grid, baseRow - k, c)))
                {
                    Debug.LogWarning($"[Flag] {who} (колонка {c}): над ним тайл " +
                        $"на {k}-й клетке — верхушка флага спрячется. Оставь ≥{clear} свободных клеток над полкой.");
                    break;
                }
            // Баннер уходит ВПРАВО: нужны свободные клетки справа на всей высоте флага.
            int blocked = 0;
            for (int k = 1; k <= clear; k++)
            for (int dx = 1; dx <= right; dx++)
                if (IsSolidCell(CellAt(grid, baseRow - k, c + dx))) blocked++;
            if (blocked > 0)
                Debug.LogWarning($"[Flag] {who} (колонка {c}): справа от него тайлы ({blocked} шт. в зоне " +
                    $"{right}×{clear}) — баннер перекрыт. Оставь ≥{right} свободных клеток СПРАВА на высоте флага.");
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
    /// Эвристическая проверка проходимости: BFS по грабельным холдам от спавна к финишу. Ребро между
    /// холдами — если укладываются в дотяжку ПО ОСЯМ: |Δвысота| ≤ _reachUpCells И |Δширина| ≤ _reachSideCells
    /// (замер игрока: вверх 2.5, вбок 4 тайла). НЕ гарантия (физика сложнее), но ловит явные разрывы.
    /// </summary>
    private void CheckSchemeReachability(char[][] grid)
    {
        if (grid.Length == 0) return;
        int rows = grid.Length;
        float cell = _tileCell;
        float maxUp   = _reachUpCells   * cell;   // мир: макс разрыв по Y (вверх И ВНИЗ — ход пэдом ограничен в обе стороны)
        float maxSide = _reachSideCells * cell;   // мир: макс разрыв по X
        // Дотяжка СИММЕТРИЧНА: |Δy|≤maxUp (НЕ «падение на любую глубину» — двигаемся пэд-за-пэдом), |Δx|≤maxSide.
        System.Func<Vector2,Vector2,bool> canReach = (from, to) =>
            Mathf.Abs(from.x - to.x) <= maxSide + 1e-4f && Mathf.Abs(from.y - to.y) <= maxUp + 1e-4f;

        int maxCol = 0; for (int r = 0; r < rows; r++) if (grid[r].Length > maxCol) maxCol = grid[r].Length;

        // Холд = верх грабельного тайла (над клеткой пусто). Храним мир + грид (для стен).
        var holds = new System.Collections.Generic.List<Vector2>();
        var hCol = new System.Collections.Generic.List<int>();
        var hRow = new System.Collections.Generic.List<int>();
        Vector2 spawn = new Vector2(float.NaN, float.NaN), finish = new Vector2(float.NaN, float.NaN);
        int spawnCol = -1, spawnRow = -1, finishCol = -1, finishRow = -1;
        var artPos = new System.Collections.Generic.List<Vector2>();
        var artCol = new System.Collections.Generic.List<int>();
        var artRow = new System.Collections.Generic.List<int>();
        var keyPos = new System.Collections.Generic.List<Vector2>();   // чекпоинт и кнопки-триггеры
        var keyCol = new System.Collections.Generic.List<int>();
        var keyRow = new System.Collections.Generic.List<int>();
        var keyChar = new System.Collections.Generic.List<char>();
        for (int r = 0; r < rows; r++)
        for (int c = 0; c < grid[r].Length; c++)
        {
            char ch = grid[r][c];
            Vector2 wp = new Vector2(c * cell, (rows - 1 - r) * cell);
            if (IsSolidCell(ch) && !IsSolidCell(CellAt(grid, r - 1, c)))
            { holds.Add(wp + Vector2.up * cell * 0.5f); hCol.Add(c); hRow.Add(r); }
            if (ch == '@') { spawn = wp; spawnCol = c; spawnRow = r; }
            if (ch == '^') { finish = wp; finishCol = c; finishRow = r; }
            if (ch == '*') { artPos.Add(wp); artCol.Add(c); artRow.Add(r); }
            // Чекпоинт и кнопки-триггеры тоже критичны: до кнопки без пути не нажать, а значит
            // появляющиеся платформы не включить и ключ за ними не взять.
            if (ch == '=' || (ch >= 'A' && ch <= 'Z'))
            { keyPos.Add(wp); keyCol.Add(c); keyRow.Add(r); keyChar.Add(ch); }
        }
        // ⚠️ Схема без спавна/финиша — это НЕ «всё достижимо», это невалидная схема. Раньше при
        // отсутствии '@' обход стартовал со ВСЕХ холдов сразу и проверка рапортовала «всё ок».
        if (spawnCol < 0)
            Debug.LogWarning("[Reach] ⚠ В схеме НЕТ спавна '@' — проверка достижимости бессмысленна, поставь спавн.");
        if (finishCol < 0)
            Debug.LogWarning("[Reach] ⚠ В схеме НЕТ финиша '^'.");
        if (holds.Count == 0) { Debug.LogWarning("[Reach] В схеме нет грабельных холдов."); return; }
        int n = holds.Count;

        // ── СВЯЗНОСТЬ ВОЗДУХА (4 направления) ────────────────────────────────────────────────────
        // Игрок перемещается ПО ВОЗДУХУ; сквозь пол/потолок хода нет, по диагонали пэд не пролезает.
        // ⚠️ БЕЗ ЭТОГО МОДЕЛЬ ПРОПУСКАЛА ЗАПЕЧАТАННЫЕ КОМНАТЫ (фидбэк игрока 2026-07-18): wallBetween
        // смотрит колонки МЕЖДУ холдами, а у холдов в одной колонке их нет — «подъём сквозь потолок»
        // считался возможным, и глухой карман с ключом выглядел достижимым.
        // ⚠️ Исчезающие тайлы (`a`-`z`) для ВОЗДУХА проходимы: по умолчанию DisappearingPlatform в
        // preview — прозрачная и СКВОЗНАЯ, твёрдой становится лишь на activeWindow сек после кнопки.
        // Холдом она при этом остаётся (её и цепляют, пока активна) — поэтому в дотяжке считается, а
        // воздух через неё течёт. Иначе валидатор счёл бы комнату за такой платформой запечатанной.
        System.Func<char,bool> blocksAir = ch => ch == '#';
        var comp = new int[rows, maxCol];
        for (int r = 0; r < rows; r++) for (int c = 0; c < maxCol; c++) comp[r, c] = blocksAir(CellAt(grid, r, c)) ? -1 : 0;
        int compCount = 0;
        for (int r = 0; r < rows; r++)
        for (int c = 0; c < maxCol; c++)
        {
            if (comp[r, c] != 0) continue;
            compCount++;
            var fq = new System.Collections.Generic.Queue<Vector2Int>();
            comp[r, c] = compCount; fq.Enqueue(new Vector2Int(c, r));
            while (fq.Count > 0)
            {
                var cur = fq.Dequeue();
                for (int k = 0; k < 4; k++)
                {
                    int nr = cur.y + (k == 0 ? -1 : k == 1 ? 1 : 0), nc = cur.x + (k == 2 ? -1 : k == 3 ? 1 : 0);
                    if (nr < 0 || nr >= rows || nc < 0 || nc >= maxCol || comp[nr, nc] != 0) continue;
                    comp[nr, nc] = compCount; fq.Enqueue(new Vector2Int(nc, nr));
                }
            }
        }
        // Воздух холда — клетка НАД ним (там висит игрок). Для спавна/финиша/артефакта — их собственная.
        System.Func<int,int,int> compAt = (r, c) =>
            (r < 0 || r >= rows || c < 0 || c >= maxCol) ? -1 : comp[r, c];
        var holdComp = new int[n];
        for (int i = 0; i < n; i++) holdComp[i] = compAt(hRow[i] - 1, hCol[i]);
        int spawnComp  = spawnCol  >= 0 ? compAt(spawnRow,  spawnCol)  : -1;
        int finishComp = finishCol >= 0 ? compAt(finishRow, finishCol) : -1;
        System.Func<int,int,bool> sameAir = (a, b) => a > 0 && b > 0 && a == b;

        // Стена блокирует прыжок (лабиринт!): в колонне СТРОГО между холдами есть тайл в КОРИДОРЕ пэда по
        // высоте [верхний холд..нижний холд]. Проём (нет тайла на этой высоте) = проход сквозь стену.
        System.Func<int,int,int,int,bool> wallBetween = (ca, ra, cb, rb) =>
        {
            int lo = Mathf.Min(ca, cb), hi = Mathf.Max(ca, cb);
            int top = Mathf.Min(ra, rb), bot = Mathf.Max(ra, rb);
            for (int c = lo + 1; c < hi; c++)
                for (int r = top; r <= bot; r++)
                    if (IsSolidCell(CellAt(grid, r, c))) return true;
            return false;
        };

        var visited = new bool[n];
        var queue = new System.Collections.Generic.Queue<int>();
        for (int i = 0; i < n; i++) // старт: из спавна дотягиваемся до холда, нет стены И общий воздух
            if (float.IsNaN(spawn.x) || (canReach(spawn, holds[i]) && !wallBetween(spawnCol, spawnRow, hCol[i], hRow[i])
                                         && sameAir(spawnComp, holdComp[i])))
            { if (!visited[i]) { visited[i] = true; queue.Enqueue(i); } }

        while (queue.Count > 0)
        {
            int i = queue.Dequeue();
            for (int j = 0; j < n; j++)
                if (!visited[j] && canReach(holds[i], holds[j]) && !wallBetween(hCol[i], hRow[i], hCol[j], hRow[j])
                    && sameAir(holdComp[i], holdComp[j]))
                { visited[j] = true; queue.Enqueue(j); }
        }

        int reachableCount = 0; foreach (var v in visited) if (v) reachableCount++;
        int isolated = n - reachableCount;

        bool finishOk = false;
        if (!float.IsNaN(finish.x))
            for (int i = 0; i < n; i++)
                if (visited[i] && canReach(holds[i], finish) && !wallBetween(hCol[i], hRow[i], finishCol, finishRow)
                    && sameAir(holdComp[i], finishComp))
                { finishOk = true; break; }

        // Артефакт (коллектибл в воздухе) достижим, если рядом ДОСТИЖИМЫЙ холд в дотяжке без стены.
        int artTotal = artPos.Count, artUnreach = 0;
        for (int a = 0; a < artTotal; a++)
        {
            bool ok = false;
            int aComp = compAt(artRow[a], artCol[a]);
            for (int i = 0; i < n && !ok; i++)
                if (visited[i] && canReach(holds[i], artPos[a]) && !wallBetween(hCol[i], hRow[i], artCol[a], artRow[a])
                    && sameAir(holdComp[i], aComp))
                    ok = true;
            if (!ok) artUnreach++;
        }

        // Чекпоинт и кнопки — по той же логике, что артефакты.
        int keyUnreach = 0;
        for (int k = 0; k < keyPos.Count; k++)
        {
            bool ok = false;
            int kComp = compAt(keyRow[k], keyCol[k]);
            for (int i = 0; i < n && !ok; i++)
                if (visited[i] && canReach(holds[i], keyPos[k]) && !wallBetween(hCol[i], hRow[i], keyCol[k], keyRow[k])
                    && sameAir(holdComp[i], kComp))
                    ok = true;
            if (!ok)
            {
                keyUnreach++;
                Debug.LogWarning($"[Reach] ⚠ {(keyChar[k] == '=' ? "Чекпоинт" : $"Кнопка '{keyChar[k]}'")} " +
                    $"(ряд {keyRow[k]}, колонка {keyCol[k]}) недостижим(а) от спавна." +
                    (keyChar[k] == '=' ? "" : " Без неё появляющиеся платформы не включить."));
            }
        }

        // КРИТЕРИЙ ТРЕВОГИ = игровое: финиш + все артефакты достижимы. Изолированные холды сами по себе
        // НЕ тревога (это часто внешний каркас/крыша лабиринта, куда и не надо лезть) — только инфо.
        bool badFinish = !float.IsNaN(finish.x) && !finishOk;
        bool critical  = badFinish || artUnreach > 0 || keyUnreach > 0 || spawnCol < 0 || finishCol < 0;
        string msg = $"[Reach] Холдов: {n}, достижимо: {reachableCount}, изолировано: {isolated} " +
                     $"(изолир. ≠ проблема, если это каркас). Финиш={(badFinish ? "НЕДОСТ." : "ok")}, " +
                     $"артефакты {artTotal - artUnreach}/{artTotal}. Reach ↑{_reachUpCells:F1} ↔{_reachSideCells:F1}.";
        if (critical) Debug.LogWarning(msg); else Debug.Log(msg);
        if (badFinish)
            Debug.LogWarning("[Reach] ⚠ ФИНИШ не достижим от спавна — разрыв больше дотяжки (↑" + _reachUpCells.ToString("F1") + "/↔" + _reachSideCells.ToString("F1") + "). Сдвинь/добавь холды.");
        if (artUnreach > 0)
            Debug.LogWarning($"[Reach] ⚠ {artUnreach} артефакт(ов) недостижимы от спавна — перенеси их на достижимый путь.");
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
