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
    // [SerializeField] — иначе значение слетает при каждой перекомпиляции (см. гочу про поля EditorWindow).
    [SerializeField] private bool _showGroupColors = true;  // подсветка групп триггеров цветом в сцене
    [SerializeField] private float _groupWindow = 5f;       // окно активации для НОВЫХ групп (панель рядом с Group ID)
    [SerializeField] private bool  _groupInverted = false;  // режим для НОВЫХ групп: стартует твёрдой, кнопка убирает
    [SerializeField] private bool  _groupCrush    = true;   // вернувшийся камень убивает (стена); для пола выключить
    // ── Маршрут глазами МОДЕЛИ (для отладки самой модели, а не уровня) ──
    [SerializeField] private bool _showRoute = true;
    [SerializeField] private bool _routeStrict = true;   // учитывать перекрытия при вертикальном движении
    [SerializeField] private string _routeExclude = "";  // считать маршрут БЕЗ этой группы (пусто = все включены)
    private System.Collections.Generic.List<Vector3> _routePath;    // спавн → финиш по мнению модели
    private System.Collections.Generic.List<Vector3> _routeReach;   // все холды, что модель считает достижимыми
    private System.Collections.Generic.List<Vector3> _routeDead;    // холды, до которых модель НЕ дотягивается
    private System.Collections.Generic.List<Vector3> _routePress;   // где жмётся кнопка
    private System.Collections.Generic.List<System.Collections.Generic.List<Vector3>> _routeBranch; // ветки к ключам
    private System.Collections.Generic.List<Vector3> _routeLostKeys;  // ключи, до которых пути нет
    private System.Collections.Generic.List<Vector3> _routeBranchPress; // кнопки, нужные ТОЛЬКО ради ключа
    private string _routeInfo = "";
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
    // ⭐ ЗАМЕР НА КАЛИБРОВОЧНОМ УРОВНЕ (2026-08-18, Level_07, 15 станций):
    //   подъём 1,2,3 клетки — берётся ВСЕГДА, при смещении вбок от 0 до 6;
    //   подъём 4 клетки  — НЕ берётся НИ ПРИ КАКОМ смещении, включая нулевое.
    // Значит граница чисто вертикальная, диагонального штрафа НЕТ: шаг (4,3) длиной 5 клеток
    // проходит, а (2,4) длиной 4.5 — нет. Форма дотяжки = КОРОБКА (гипотеза про эллипс отвергнута
    // замером). Прежнее ↑4 было завышено и стояло во ВСЕХ проверках с июля.
    private float   _reachUpCells   = 3f;
    private float   _reachSideCells = 6f;   // 5 и 6 взялись с запасом; выше 6 не измеряли
    /// <summary>
    /// ⭐ БЮДЖЕТ ОКНА: сколько ПЕРЕХВАТОВ игрок успевает, пока платформа держится. 0 = время
    /// выключено (окно вечное, как было до 2026-09-04).
    ///
    /// ⭐ ЗАМЕРЕНО (телеметрия ClimbTelemetry, 5 попыток игрока, 2026-09-04):
    ///   • перехват стоит 1.30 с (53 перехвата за 69.1 с открытых окон), медиана интервала 1.40 с;
    ///   • темп 0.77 перехвата в секунду, разброс по окнам 0.60..1.00;
    ///   • окно 5 с → 3..5 перехватов, чаще 4; окно 7 с → 5..6.
    /// Отсюда правило: БЮДЖЕТ ≈ длина окна (с) × 0.77. Для пятисекундного окна это 4 — прикидка,
    /// с которой начинали, совпала с замером.
    ///
    /// ⚠️ ЧИСЛО ОДНО НА ВСЕ ГРУППЫ, А ОКНА У НИХ РАЗНЫЕ (в замере: 5.0 с у A, C, D, E и 7.0 с у B).
    /// Пока это загрубление в строгую сторону для длинных окон. Честно было бы брать бюджет из
    /// activeWindow каждой платформы — отдельная задача, для неё нужен ещё один боковой канал:
    /// ASCII-схема длительностей не выражает.
    /// </summary>
    private int     _moveBudget     = 4;
    // Декор/вариация тайлов: off = максимально ровно (одна трава + один камень).
    private bool    _schemeDecorate;
    // Генератор лабиринта: размер сетки комнат + сид (0 = случайный).
    [SerializeField] private int _mazeW = 5, _mazeH = 5, _mazeSeed = 0;   // см. пояснение к [SerializeField] ниже
    /// <summary>⭐ РУЧКА СЛОЖНОСТИ ПАКА: сколько механизмов игрок обязан открыть ПО ПОРЯДКУ.
    /// 1 — «нажал и прошёл», 3 — «открыл, поднялся, нашёл вторую кнопку, открыл третью». Это желаемая
    /// глубина: сколько реально получится, зависит от формы лабиринта (см. лог плана).</summary>
    [SerializeField] private int _mazeChain = 2;
    /// <summary>⭐ Сколько дверей-инверсий (самозакрывающихся стен) дозволено на уровень.
    /// ⚠️ Раньше здесь была жёсткая единица — осторожность дня, когда механика только появилась.
    /// Она устарела (крушение проверено в игре, в ручном Level_07 таких групп две) и резала
    /// разнообразие ВНУТРИ уровня, поэтому стала ручкой: дебют механики — 1, финал пака — 2-3.
    /// ⚠️ Монотонность «дверь на каждом уровне» лечится НЕ этим лимитом, а приоритетом ворот
    /// в планировщике (см. PuzzleComposer) — это две разные проблемы, их легко спутать.</summary>
    [SerializeField] private int _mazeDoors = 2;
    /// <summary>
    /// ⭐ Потолок по числу механизмов на уровень. ⚠️ ЭТО НЕ ДИЗАЙНЕРСКОЕ ОГРАНИЧЕНИЕ, А ЦЕНА ПРОВЕРКИ:
    /// поиск идёт по состояниям (позиция × 2^групп), а приёмка гоняет его ещё раз на КАЖДУЮ группу
    /// («механизм несущий?»). Стоимость ≈ (G+1)·2^G — каждый лишний механизм УДВАИВАЕТ проверку.
    /// Жёсткий предел модели — 12 групп (<see cref="LevelModel"/>), дальше поиск неподъёмен.
    /// Само по себе число механизмов игре ничем не мешает.
    /// </summary>
    [SerializeField] private int _mazeMechs = 4;
    /// <summary>
    /// ⭐ Доля вертикальных проходов, превращаемых в ДВУХЪЯРУСНЫЙ ЗАЛ (потолок между этажами вырезан
    /// почти целиком). Ручка облика уровня: 0 — сплошь приземистые комнаты, как было; выше — больше
    /// высоких пространств.
    /// ⚠️ Не бесплатно: зал съедает пол верхней комнаты, а на нём стоят кнопки, ключи и флаги. Чем
    /// больше залов, тем чаще генератор перебрасывает сид, потому что объект стало некуда поставить.
    /// </summary>
    [SerializeField] private int _mazeHalls = 35;
    /// <summary>Шанс поставить ВЫЛАЗКУ — целую петлю «провалился → взял ключ → вылез в другом месте»
    /// (см. ExcursionModule). Съедает сразу три группы, поэтому не на каждом уровне.</summary>
    [SerializeField] private int _mazeExcursion = 40;
    /// <summary>⭐ СЛОЖНОСТЬ УРОВНЯ В БАЛЛАХ. Состав рецепта подбирается под неё случайно, но точно:
    /// один и тот же слот пака каждый раз выглядит по-новому, оставаясь той же сложности.
    /// ⚠️ Сверху ограничена потолком групп: сложность может запросить больше, чем приёмка успевает
    /// проверить, и тогда честнее недобрать баллов (об этом будет предупреждение в логе).</summary>
    [SerializeField] private int _mazeDifficulty = 12;
    /// <summary>Замысел последней сгенерированной схемы человеческими словами — для лога.</summary>
    private string _mazePlanText = "";
    /// <summary>Рецепт последней схемы: из чего уровень задуман (см. <see cref="LevelRecipe"/>).</summary>
    private string _mazeRecipeText = "";

    // ⭐ СВОЙСТВА ГРУПП, КОТОРЫЕ ASCII-СХЕМА ВЫРАЗИТЬ НЕ МОЖЕТ (инверсия и т.д.) — едут отдельным
    // каналом от генератора к импортёру и к проверке. Это первый шаг к тому, чтобы генератор отдавал
    // СТРУКТУРУ вместо текста: геометрия пока в сетке символов, свойства уже нет.
    // ⚠️ Привязаны к КОНКРЕТНОЙ схеме: игрок может вставить в поле импорта свой текст, и применять к
    // нему свойства от прошлой генерации нельзя.
    private System.Collections.Generic.List<ModuleStamp> _mazeStamps = new System.Collections.Generic.List<ModuleStamp>();
    private string _mazeStampsFor = "";

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
        // ⚠️ Тумблеры, влияющие на ОТРИСОВКУ В СЦЕНЕ, оборачиваем в change-check: без явного
        // SceneView.RepaintAll() сцена не перерисовывается, и выключенный грид ПРОДОЛЖАЕТ висеть на
        // экране старой картинкой (игрок поймал 2026-08-18: «включаю/выключаю грид — не реагирует»).
        // Это НЕ та петля, из-за которой убрали sv.Repaint(): та была ВНУТРИ OnSceneGUI (перерисовка
        // вызывала перерисовку). Здесь — разовый репейнт из OnGUI окна и только по факту изменения.
        EditorGUI.BeginChangeCheck();
        _snapGrid  = EditorGUILayout.Toggle("Snap on place", _snapGrid);
        _snapMove  = EditorGUILayout.Toggle("Snap on move",  _snapMove);
        _showGrid  = EditorGUILayout.Toggle("Show grid",     _showGrid);
        _showGroupColors = EditorGUILayout.Toggle(
            new GUIContent("Цвет групп", "Подсветить исчезающие платформы и кнопки цветом их группы (a-z) прямо в сцене"),
            _showGroupColors);
        if (_showGrid && IsTileTool)
            _tileCell = EditorGUILayout.Slider(
                new GUIContent("Tile cell", "Шаг тайловой сетки (постановка + snap-move + грид). Грид рисуется автоматически в тайл-инструментах."),
                _tileCell, 0.3f, 2f);
        if (EditorGUI.EndChangeCheck()) SceneView.RepaintAll();

        // ── Маршрут глазами модели: отладка САМОЙ модели проходимости ──
        using (new GUILayout.HorizontalScope())
        {
            using (new EditorGUI.DisabledScope(_root == null))
                if (GUILayout.Button(new GUIContent("🧭 Маршрут модели",
                    "Показать в сцене путь спавн→финиш так, как его видит модель проходимости"), GUILayout.Height(22)))
                    ComputeRoute();
            bool prevShow = _showRoute;
            _showRoute = EditorGUILayout.ToggleLeft("показывать", _showRoute, GUILayout.Width(95));
            if (prevShow != _showRoute) SceneView.RepaintAll();
        }
        bool prevStrict = _routeStrict;
        _routeStrict = EditorGUILayout.ToggleLeft(
            new GUIContent("учитывать перекрытия (иначе модель лезет сквозь пол)"), _routeStrict);
        if (prevStrict != _routeStrict && _root != null) ComputeRoute();
        string prevExc = _routeExclude;
        _routeExclude = EditorGUILayout.TextField(
            new GUIContent("Без группы", "Ключ группы, которую считать ВЫКЛЮЧЕННОЙ — видно, что именно она открывает. Пусто = все включены"),
            _routeExclude);
        if (prevExc != _routeExclude && _root != null) ComputeRoute();
        if (!string.IsNullOrEmpty(_routeInfo))
            GUILayout.Label(_routeInfo + "   (синие точки — модель достаёт, серые — нет)", EditorStyles.miniLabel);

        // Ключ группы: связывает Trigger-кнопку с её группой исчезающих тайлов (одинаковый groupId).
        if (_tool == Tool.TriggerButton || _tool == Tool.DisappearTile)
        {
            GUI.color = new Color(0.85f, 0.9f, 1f);
            _groupId = EditorGUILayout.TextField(
                new GUIContent("Group ID", "Ключ пары триггер↔исчезающие тайлы. Кнопка и её тайлы должны иметь ОДИНАКОВЫЙ Group ID."),
                _groupId);
            if (string.IsNullOrWhiteSpace(_groupId)) _groupId = "A";

            // Окно активации ЭТОЙ группы — правим прямо здесь, не выискивая компонент в иерархии.
            // Если группа уже есть в уровне — показываем и пишем её реальное значение; если ещё нет,
            // значение запомнится и применится при создании группы (GetOrCreateDisappearGroup).
            var dpCur = FindDisappearGroup(_groupId);
            float shown = dpCur != null ? dpCur.activeWindow : _groupWindow;
            EditorGUI.BeginChangeCheck();
            float edited = EditorGUILayout.FloatField(
                new GUIContent("Окно, с", "Сколько секунд платформы группы твёрдые после нажатия кнопки"),
                shown);
            if (EditorGUI.EndChangeCheck())
            {
                edited = Mathf.Max(0.5f, edited);
                _groupWindow = edited;
                if (dpCur != null)
                {
                    Undo.RecordObject(dpCur, "Change active window");
                    dpCur.activeWindow = edited;
                    // warningTime не должен превышать окно — иначе вибрация начинается до активации.
                    dpCur.warningTime = Mathf.Min(dpCur.warningTime, edited * 0.5f);
                    EditorUtility.SetDirty(dpCur);
                    EditorSceneManager.MarkSceneDirty(
                        UnityEngine.SceneManagement.SceneManager.GetActiveScene());
                    SceneView.RepaintAll();   // подпись секунд под кнопкой обновится сразу
                }
            }
            // ⭐ ИНВЕРСИЯ: группа стартует твёрдой, а кнопка её УБИРАЕТ на окно. Правится так же,
            // как окно: у живой группы пишем сразу, для будущей — запоминаем до создания.
            bool invShown = dpCur != null ? dpCur.inverted : _groupInverted;
            EditorGUI.BeginChangeCheck();
            bool invEdited = EditorGUILayout.Toggle(
                new GUIContent("Инверсная", "Стартует ТВЁРДОЙ, кнопка убирает её на время (проход в стене / провал в полу)"),
                invShown);
            bool crushShown = dpCur != null ? dpCur.crushOnReturn : _groupCrush;
            bool crushEdited = crushShown;
            using (new EditorGUI.DisabledScope(!invEdited))
                crushEdited = EditorGUILayout.Toggle(
                    new GUIContent("Возврат убивает", "СТЕНА: вернувшийся камень убивает того, кто внутри. Для ПОЛА выключить"),
                    crushShown);
            if (EditorGUI.EndChangeCheck())
            {
                _groupInverted = invEdited; _groupCrush = crushEdited;
                if (dpCur != null)
                {
                    Undo.RecordObject(dpCur, "Change group mode");
                    dpCur.inverted = invEdited; dpCur.crushOnReturn = crushEdited;
                    EditorUtility.SetDirty(dpCur);
                    EditorSceneManager.MarkSceneDirty(
                        UnityEngine.SceneManagement.SceneManager.GetActiveScene());
                    SceneView.RepaintAll();
                }
            }
            if (dpCur == null)
                GUILayout.Label($"Группы '{_groupId}' в уровне ещё нет — настройки применятся при создании.",
                    EditorStyles.miniLabel);

            GUILayout.Label(_tool == Tool.TriggerButton
                    ? "🔘 Кнопка " + (invShown ? "УБИРАЕТ" : "активирует") + " тайлы с этим Group ID"
                    : invShown
                        ? "🧱 Тайлы группы твёрдые, кнопка убирает их на время"
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
            GUI.backgroundColor = new Color(0.85f, 0.6f, 0.35f);
            if (GUILayout.Button(new GUIContent("⏏ Unload",
                    "Убрать уровень со сцены. Спросит, сохранять ли изменения."),
                    GUILayout.Width(90), GUILayout.Height(28)))
                UnloadLevel();
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
        DrawGroupColors();
        DrawRoute();
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
        // ⚠️ Исчезающие тайлы вложены ГЛУБЖЕ обычных: _root/Disappearing/Disappear_A/Tile. Имени
        // «Disappear_A» в IsGroup нет, а «Disappearing» есть — поэтому подъём проскакивал сам тайл и
        // останавливался на КОНТЕЙНЕРЕ, и ПКМ сносил всю группу разом (баг, пойман игроком 2026-08-18).
        // Останавливаемся ещё и на контейнере группы: у него есть DisappearingPlatform.
        Transform t = best.transform;
        while (t.parent != null && t.parent != _root.transform
               && !IsGroup(t.parent.name)
               && t.parent.GetComponent<DisappearingPlatform>() == null)
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
    /// <summary>
    /// Считает маршрут спавн→финиш ТАК, КАК ЕГО ВИДИТ МОДЕЛЬ проходимости, и запоминает для отрисовки.
    /// Смысл не в проверке уровня, а в проверке САМОЙ МОДЕЛИ: где нарисованная линия пройдёт сквозь
    /// потолок или перепрыгнет невозможное — там модель и врёт. Числами это искали три захода и не нашли.
    /// ⚠️ Сетку строим НАПРЯМУЮ из объектов уровня, а не через ExportScheme: тот кладёт всё в одну карту,
    /// и монета затирает тайл (в Level_06 так терялось ~77 камней → в платформах появлялись дыры).
    /// </summary>
    /// <summary>
    /// Дотяжка — ЭЛЛИПС, а не коробка. Замеры «вверх 4» и «вбок 4» делались по отдельности, а в модель
    /// попали как «можно и то, и другое ОДНОВРЕМЕННО»: шаг (4,3) проходил проверку `|Δx|≤4 && |Δy|≤4`,
    /// хотя это дистанция 5 клеток — рука столько не тянет. Игрок поймал это на маршрутах A и D
    /// (2026-08-18): модель прыгала по длинным диагоналям там, где реально лезут короткими шагами.
    /// </summary>
    /// <summary>Насколько близко надо подойти, чтобы нажать кнопку (клеток). Кнопку давят пэдом
    /// вплотную — это НЕ та же дистанция, что дотяжка до холда.</summary>

    // ⛔ КОРИДОР (BodyCorridor + DistToSegment) УДАЛЁН 2026-08-31 — НЕ ВОЗВРАЩАТЬ.
    // Это было единственное подобранное на глаз число модели: «траектория не отклоняется от прямой
    // холд→цель дальше 2.0 клеток». Заменён БЮДЖЕТОМ ПУТИ в PathPossible (длина обхода ≤ ReachSumCells),
    // который делает ту же работу, но выводится из ЗАМЕРА, а не из подгонки.
    // Почему убрали: на Level_03 модель отказала в обычном ходе (2,2) — с полки (26.5,7.0) на (27.5,8.0).
    // Рука обязана обогнуть саму целевую полку справа: путь 6 шагов, отклонение 2.12 клетки при
    // разрешённых 2.0. Отказ с запасом в 0.12 клетки; игрок прислал скрин и подтвердил, что ход берётся.
    // Проверено после удаления: спорный нырок под стену на Level_06 (6 вбок + 2 на обход = 8 > 7)
    // ПО-ПРЕЖНЕМУ запрещён, то есть послаблением это не стало — просто ограничение стало честным.

    private const int ReachSumCells = 7;


    /// <summary>
    /// Уровень (иерархия GameObject) → <see cref="LevelSpec"/>. Всё, что модель знает об уровне,
    /// собирается ЗДЕСЬ и только здесь; сама модель со сценой не работает и потому одинаково служит
    /// и загруженному уровню, и тому, что породит генератор.
    /// Возвращает null, если нет спавна или финиша.
    /// </summary>
    private static LevelSpec BuildSpecFromLevel(GameObject root, float cell, string excludeGroup)
    {
        var spawn = root.transform.Find("SpawnPoint");
        var finish = root.transform.Find("Flag_finish");
        if (spawn == null || finish == null) return null;

        var spec = new LevelSpec { cell = cell };
        System.Func<Vector3, Vector2Int> K = p => new Vector2Int(
            Mathf.RoundToInt(p.x / cell), Mathf.RoundToInt(p.y / cell));

        // Габарит цели в клетках: касание — это перекрытие с КОЛЛАЙДЕРОМ, а не попадание в пивот
        // (у артефакта триггер-сфера в 2 клетки поперёк, у флага плоская коробка у основания).
        System.Action<Transform, LevelTarget> fill = (tr, t) =>
        {
            t.exists = true; t.world = tr.position; t.cell = K(tr.position);
            var cols = tr.GetComponentsInChildren<Collider>(true);
            if (cols.Length > 0)
            {
                var b = cols[0].bounds;
                for (int i = 1; i < cols.Length; i++) b.Encapsulate(cols[i].bounds);
                t.center = new Vector2(b.center.x / cell, b.center.y / cell);
                t.half   = new Vector2(b.extents.x / cell, b.extents.y / cell);
            }
            else { t.center = new Vector2(tr.position.x / cell, tr.position.y / cell); t.half = Vector2.zero; }
        };

        var tilesG = root.transform.Find("Tiles");
        if (tilesG != null) foreach (Transform t in tilesG) spec.rock.Add(K(t.position));

        var dis = root.transform.Find("Disappearing");
        if (dis != null) foreach (Transform c in dis)
        {
            var dp = c.GetComponent<DisappearingPlatform>(); if (dp == null) continue;
            if (!string.IsNullOrEmpty(excludeGroup)
                && dp.groupId.Equals(excludeGroup, System.StringComparison.OrdinalIgnoreCase)) continue;
            var g = spec.GetOrAddGroup(dp.groupId, dp.inverted);
            // ⚠️ Кнопку, лежащую ВНУТРИ группы, тайлом считать нельзя — она не поверхность.
            foreach (Transform tl in c)
                if (tl.GetComponentInChildren<TriggerTile>(true) == null) g.tiles.Add(K(tl.position));
        }

        // ⭐ Кнопки ищем ПО ВСЕМУ уровню, а не в контейнере "Triggers": игрок кладёт кнопку ВНУТРЬ
        // группы, чтобы она была недоступна, пока та не открыта (Level_07: кнопка C лежит в группе D).
        foreach (var tt in root.GetComponentsInChildren<TriggerTile>(true))
        {
            int gi = spec.IndexOfGroup(tt.groupId); if (gi < 0) continue;
            int host = -1;
            for (var p = tt.transform.parent; p != null; p = p.parent)
            {
                var owner = p.GetComponent<DisappearingPlatform>();
                if (owner == null) continue;
                host = spec.IndexOfGroup(owner.groupId); break;
            }
            var btn = new LevelButton { cell = K(tt.transform.position), host = host };
            var box = new LevelTarget(); fill(tt.transform, box);
            btn.center = box.center; btn.half = box.half;
            spec.groups[gi].buttons.Add(btn);
        }

        spec.spawn = K(spawn.position);
        fill(finish, spec.finish);
        var arts = root.transform.Find("Artifacts");
        if (arts != null) foreach (Transform a in arts)
        {
            // ⚠️ Ключ — это объект с компонентом Artifact, а не «всё, что лежит в контейнере»:
            // в Level_01 там же лежит TutorArtifact, вещь другой природы.
            if (a.GetComponentInChildren<Artifact>(true) == null) continue;
            var t = new LevelTarget(); fill(a, t); spec.artifacts.Add(t);
        }
        var cps = root.transform.Find("Checkpoints");
        if (cps != null) foreach (Transform c in cps)
        { var t = new LevelTarget(); fill(c, t); spec.checkpoints.Add(t); }
        return spec;
    }

    private void ComputeRoute()
    {
        _routePath = new System.Collections.Generic.List<Vector3>();
        _routeReach = new System.Collections.Generic.List<Vector3>();
        _routeDead = new System.Collections.Generic.List<Vector3>();
        _routePress = new System.Collections.Generic.List<Vector3>();
        _routeBranch = new System.Collections.Generic.List<System.Collections.Generic.List<Vector3>>();
        _routeLostKeys = new System.Collections.Generic.List<Vector3>();
        _routeBranchPress = new System.Collections.Generic.List<Vector3>();
        _routeInfo = "";
        if (_root == null) { _routeInfo = "уровень не загружен"; return; }

        float cell = _tileCell;
        System.Func<Vector3, Vector2Int> K = p => new Vector2Int(
            Mathf.RoundToInt(p.x / cell), Mathf.RoundToInt(p.y / cell));
        int RU = Mathf.RoundToInt(_reachUpCells), RS = Mathf.RoundToInt(_reachSideCells);

        // ⭐ Уровень приводится к LevelSpec, а весь поиск живёт в LevelModel — одна реализация правил
        // на маршрут по уровню и на проверку схем (раньше их было две, см. LevelModel).
        var spec = BuildSpecFromLevel(_root, cell, _routeExclude);
        if (spec == null) { _routeInfo = "нет спавна или финиша"; return; }
        var model = new LevelModel(spec, RS, RU) { MoveBudget = _moveBudget };
        model.Search();
        if (model.TooManyGroups)
        { _routeInfo = "слишком много групп (" + spec.groups.Count + ") для точного поиска"; return; }

        var allCells = model.Cells; int N = model.N, G = model.G, MASKS = model.MASKS, TOTAL = model.TOTAL;
        var seenState = model.Seen; var prevState = model.Prev;
        var prevKind = model.PrevKind; var depthState = model.Depth;
        var spK = model.SpawnCell;
        var fnK = spec.finish.cell;
        System.Func<int, System.Collections.Generic.HashSet<Vector2Int>> SolidFor = m => model.SolidFor(m);
        System.Func<int, Vector2Int, Vector2Int, bool> CanStep = (m, a, b) => model.CanStep(m, a, b);

        // Финиш: первое по числу ходов состояние, из которого до флага дотягиваются.
        int goal = -1, goalDepth = int.MaxValue;
        for (int st = 0; st < TOTAL; st++)
        {
            if (!seenState[st] || depthState[st] >= goalDepth) continue;
            if (!model.CanTouch(model.GroupMask(st), allCells[st % N], spec.finish)) continue;
            goal = st; goalDepth = depthState[st];
        }

        // Алиасы под старый код ниже: имена групп и их кнопки берём уже из структуры.
        var gids = new System.Collections.Generic.List<string>();
        var gButtons = new System.Collections.Generic.List<System.Collections.Generic.List<Vector2Int>>();
        foreach (var g in spec.groups)
        {
            gids.Add(g.id);
            var bl = new System.Collections.Generic.List<Vector2Int>();
            foreach (var b in g.buttons) bl.Add(b.cell);
            gButtons.Add(bl);
        }
        // Цепочка состояний от спавна до st (первым идёт стартовое состояние).
        System.Func<int, System.Collections.Generic.List<int>> ChainTo = st =>
        {
            var ch = new System.Collections.Generic.List<int>();
            for (int s = st; s >= 0; s = prevState[s]) { ch.Add(s); if (prevState[s] < 0) break; }
            ch.Reverse(); return ch;
        };

        // Цепочка состояний → путевые точки. Нажатие кнопки разворачивается в заход НА кнопку и
        // обратно: иначе нажатие происходит «на месте», линия проходит мимо, и не видно, что жали.
        // Общая на маршрут и на ветки — чтобы кнопки на побочных путях рисовались так же, как на
        // основном (ровно этого не хватало: ветка к ключу шла сквозь ещё не открытые платформы).
        System.Func<System.Collections.Generic.List<int>,
                    System.Collections.Generic.List<Vector2Int>,
                    System.Collections.Generic.List<string>,
                    System.Collections.Generic.List<Vector2Int>> Walk = (chain, outWp, outPressed) =>
        {
            var btnCells = new System.Collections.Generic.List<Vector2Int>();
            for (int i = 0; i < chain.Count; i++)
            {
                int m = chain[i] / N, ci = chain[i] % N;
                if (i > 0 && prevKind[chain[i]] == 1)
                {
                    int pm = chain[i - 1] / N, added = m & ~pm;
                    for (int g = 0; g < G; g++)
                        if ((added & (1 << g)) != 0)
                        {
                            if (outPressed != null) outPressed.Add(gids[g]);
                            Vector2Int bbest = gButtons[g][0]; int bd3 = int.MaxValue;
                            foreach (var btn in gButtons[g])
                            { int d3 = Mathf.Abs(btn.x - allCells[ci].x) + Mathf.Abs(btn.y - allCells[ci].y);
                              if (d3 < bd3) { bd3 = d3; bbest = btn; } }
                            btnCells.Add(bbest);
                            outWp.Add(bbest);
                            outWp.Add(allCells[ci]);
                        }
                    continue;
                }
                outWp.Add(allCells[ci]);
            }
            return btnCells;
        };

        var waypoints = new System.Collections.Generic.List<Vector2Int> { spK };
        var pressed = new System.Collections.Generic.List<string>();
        var goalChain = new System.Collections.Generic.List<int>();
        int finalMask = 0;
        if (goal >= 0)
        {
            goalChain = ChainTo(goal);
            finalMask = goal / N;
            foreach (var b in Walk(goalChain, waypoints, pressed))
                _routePress.Add(new Vector3(b.x * cell, b.y * cell + cell * 0.5f, 0f));
            waypoints.Add(fnK);
        }

        System.Func<Vector2Int, Vector3> W = k => new Vector3(k.x * cell, k.y * cell + cell * 0.5f, 0f);
        foreach (var k in waypoints) _routePath.Add(W(k));

        // Достижимое — по ВСЕМУ дереву состояний: холд считается взятым, если модель постояла на нём
        // хоть в каком-то состоянии (в т.ч. открыв группу необязательной кнопкой). Раньше смотрели
        // только маску обязательных кнопок, и всё за побочной кнопкой краснело как недостижимое.
        {
            var reachedCells = new System.Collections.Generic.HashSet<int>();
            for (int st = 0; st < TOTAL; st++) if (seenState[st]) reachedCells.Add(st % N);
            var allSolid = SolidFor(MASKS - 1);
            foreach (var k in allSolid)
            {
                if (allSolid.Contains(new Vector2Int(k.x, k.y + 1))) continue;   // не холд ни при какой маске
                int ci = model.CellIndex(k); if (ci < 0) continue;
                if (reachedCells.Contains(ci)) _routeReach.Add(W(k)); else _routeDead.Add(W(k));
            }
        }

        // ── ВЕТКИ К КЛЮЧАМ (зелёным): видно, что ключ достижим, каким путём к нему идти И КАКУЮ
        // КНОПКУ ради него надо нажать. Берутся из ТОГО ЖЕ дерева состояний, что и маршрут к финишу.
        int keysOk = 0, keysTotal = 0;
        var keyNeeds = new System.Collections.Generic.List<string>();
        {
            {
                int keyNo = 0;
                foreach (var art in spec.artifacts)
                {
                    keysTotal++; keyNo++;
                    // ⚠️ Берём состояние с БЛИЖАЙШИМ К КЛЮЧУ холдом (число ходов — только тай-брейк).
                    // Раньше сравнивали ТОЛЬКО дальность от маршрута, и ветка цеплялась за первый холд
                    // минимальной дальности: на Level_06 линия шла через пол-экрана на 6 клеток, хотя
                    // зацеп есть прямо под ключом (фидбэк игрока со скриншотом). Последний отрезок ветки
                    // игрок читает как «вот так дотянуться» — он обязан показывать самый близкий хват.
                    int best = model.FindTouchState(art);
                    if (best < 0) { _routeLostKeys.Add(new Vector3(art.world.x, art.world.y, 0f)); continue; }
                    keysOk++;

                    // Ветку рисуем от места, где она ОТДЕЛЯЕТСЯ от основного маршрута: обе цепочки
                    // растут из одного BFS-дерева, значит у них общий префикс — его и срезаем.
                    var chain = ChainTo(best);
                    int common = 0;
                    while (common < chain.Count && common < goalChain.Count && chain[common] == goalChain[common])
                        common++;
                    int from = Mathf.Max(0, common - 1);
                    var sub = chain.GetRange(from, chain.Count - from);

                    var wp = new System.Collections.Generic.List<Vector2Int>();
                    var pr = new System.Collections.Generic.List<string>();
                    foreach (var b in Walk(sub, wp, pr))
                        _routeBranchPress.Add(new Vector3(b.x * cell, b.y * cell + cell * 0.5f, 0f));

                    var line = new System.Collections.Generic.List<Vector3>();
                    foreach (var k in wp) line.Add(W(k));
                    line.Add(new Vector3(art.world.x, art.world.y, 0f));
                    if (line.Count > 1) _routeBranch.Add(line);

                    // Кнопки, которых нет на обязательном маршруте — то есть нажимаемые РАДИ КЛЮЧА.
                    var extra = new System.Collections.Generic.List<string>();
                    foreach (var g in pr) if (!pressed.Contains(g) && !extra.Contains(g)) extra.Add(g);
                    if (extra.Count > 0)
                        keyNeeds.Add("ключ " + keyNo + " ← " + string.Join("→", extra.ToArray()));
                }
            }
        }

        var optional = new System.Collections.Generic.List<string>();
        for (int g = 0; g < G; g++) if ((finalMask & (1 << g)) == 0) optional.Add(gids[g]);
        _routeInfo = "финиш " + (goal >= 0 ? "ok" : "НЕ достигнут")
            + ", ключей " + keysOk + "/" + keysTotal
            + ", шагов " + Mathf.Max(0, _routePath.Count - 1)
            + (pressed.Count > 0 ? "  |  ОБЯЗАТЕЛЬНЫЕ кнопки: " + string.Join("→", pressed.ToArray())
                                 : "  |  кнопки не нужны")
            + (optional.Count > 0 ? "  |  необязательные: " + string.Join(",", optional.ToArray()) : "")
            + (keyNeeds.Count > 0 ? "  |  РАДИ КЛЮЧЕЙ: " + string.Join("; ", keyNeeds.ToArray()) : "");
        SceneView.RepaintAll();
    }

    /// <summary>Рисует маршрут модели: красная линия спавн→финиш, точки — что модель считает достижимым.</summary>
    private void DrawRoute()
    {
        if (!_showRoute || _root == null || _routePath == null) return;
        if (Event.current.type != EventType.Repaint) return;

        if (_routeDead != null)
        {
            Handles.color = new Color(0.45f, 0.45f, 0.45f, 0.55f);   // модель сюда не дотянулась
            foreach (var p in _routeDead) Handles.DrawSolidDisc(p, Vector3.forward, _tileCell * 0.10f);
        }
        if (_routeReach != null)
        {
            Handles.color = new Color(0.3f, 0.8f, 1f, 0.55f);        // достижимо по мнению модели
            foreach (var p in _routeReach) Handles.DrawSolidDisc(p, Vector3.forward, _tileCell * 0.12f);
        }
        // Ветки к ключам — зелёным. Пунктирный вид не нужен: они и так тоньше основного маршрута.
        if (_routeBranch != null)
        {
            Handles.color = new Color(0.25f, 0.95f, 0.35f, 0.9f);
            foreach (var line in _routeBranch)
                for (int i = 1; i < line.Count; i++)
                {
                    Handles.DrawAAPolyLine(3.5f, line[i - 1], line[i]);
                    Handles.DrawSolidDisc(line[i], Vector3.forward, _tileCell * 0.10f);
                }
        }
        // Кнопка, которую жмут ТОЛЬКО ради ключа — зелёное кольцо (обязательные остаются жёлтыми).
        if (_routeBranchPress != null)
        {
            Handles.color = new Color(0.25f, 0.95f, 0.35f, 1f);
            foreach (var p in _routeBranchPress)
            {
                Handles.DrawWireDisc(p, Vector3.forward, _tileCell * 0.55f);
                Handles.DrawWireDisc(p, Vector3.forward, _tileCell * 0.75f);
            }
        }
        if (_routeLostKeys != null)
        {
            Handles.color = new Color(1f, 0.2f, 0.2f, 1f);   // ключ, до которого пути НЕТ
            foreach (var p in _routeLostKeys)
            {
                float r = _tileCell * 0.55f;
                Handles.DrawAAPolyLine(4f, p + new Vector3(-r,-r), p + new Vector3(r,r));
                Handles.DrawAAPolyLine(4f, p + new Vector3(-r,r),  p + new Vector3(r,-r));
            }
        }
        if (_routePress != null)
        {
            Handles.color = new Color(1f, 0.95f, 0.2f, 1f);   // жёлтое кольцо = здесь жмём кнопку
            foreach (var p in _routePress)
            {
                Handles.DrawWireDisc(p, Vector3.forward, _tileCell * 0.75f);
                Handles.DrawWireDisc(p, Vector3.forward, _tileCell * 0.95f);
            }
        }
        if (_routePath.Count > 1)
        {
            int RS = Mathf.RoundToInt(_reachSideCells), RU = Mathf.RoundToInt(_reachUpCells);
            for (int i = 1; i < _routePath.Count; i++)
            {
                int dx = Mathf.RoundToInt((_routePath[i].x - _routePath[i - 1].x) / _tileCell);
                int dy = Mathf.RoundToInt((_routePath[i].y - _routePath[i - 1].y) / _tileCell);
                // Диагонального штрафа НЕТ (замер на Level_07): граница чисто вертикальная, поэтому
                // подсветка «подозрительных диагоналей» больше не нужна — модель либо разрешает шаг,
                // либо нет, и оба случая теперь измерены.
                bool bad = Mathf.Abs(dy) > RU || Mathf.Abs(dx) > RS;
                Handles.color = bad ? new Color(1f, 0.55f, 0f, 1f) : new Color(1f, 0.15f, 0.15f, 0.95f);
                Handles.DrawAAPolyLine(bad ? 7f : 5f, _routePath[i - 1], _routePath[i]);
                Handles.DrawSolidDisc(_routePath[i], Vector3.forward, _tileCell * 0.16f);
            }
        }
        Handles.color = Color.white;
    }

    /// <summary>Контейнер группы по ключу, или null. Для показа/правки её окна в панели.</summary>
    private DisappearingPlatform FindDisappearGroup(string groupId)
    {
        if (_root == null) return null;
        var parent = _root.transform.Find("Disappearing");
        if (parent == null) return null;
        foreach (Transform c in parent)
        {
            var dp = c.GetComponent<DisappearingPlatform>();
            if (dp != null && dp.groupId == groupId) return dp;
        }
        return null;
    }

    /// <param name="usePanelMode">Брать ли режим (инверсия/раздавливание) из панели редактора.
    /// ⚠️ ИМПОРТУ — НЕЛЬЗЯ. Тумблер «Инверсная» запоминается между сессиями (`[SerializeField]`), и
    /// если игрок включил его когда-то руками, импорт схемы делал ИНВЕРСНЫМИ ВСЕ группы уровня.
    /// Ровно так и вышло с Level_08: генератор пометил дверью одну группу D, а в уровне их стало
    /// четыре. Импорт создаёт группы обычными, а инверсию ставит потом — по штампам генератора.</param>
    private GameObject GetOrCreateDisappearGroup(string groupId, bool usePanelMode = true)
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
        comp.activeWindow = Mathf.Max(0.5f, _groupWindow);           // окно, выставленное в панели
        comp.warningTime  = Mathf.Min(comp.warningTime, comp.activeWindow * 0.5f);
        comp.inverted      = usePanelMode && _groupInverted;         // режим из панели — только для ручной кладки
        comp.crushOnReturn = !usePanelMode || _groupCrush;
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
        _moveBudget = EditorGUILayout.IntSlider(
            new GUIContent("Окно, перехватов",
                "Сколько перехватов успеваешь, пока платформа держится. 0 = время выключено " +
                "(окно вечное). ЧИСЛО НЕ ЗАМЕРЕНО — поставь по игре, как ставил дотяжку."),
            _moveBudget, 0, 10);
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
            // ⭐ ГЛАВНАЯ РУЧКА: состав уровня подбирается под неё сам. Ворота стоят 2 балла, дверь 3,
            // мост 4, вложенная кнопка 5, вылазка 9, плюс +2 за каждый ярус цепочки сверх первого.
            _mazeDifficulty = EditorGUILayout.IntSlider(new GUIContent("Сложность, баллы",
                "Из чего состоит уровень, генератор подбирает САМ под эту сумму: ворота 2, дверь 3, "
                + "мост 4, вложенная кнопка 5, вылазка 9, плюс 2 за каждый ярус цепочки сверх первого. "
                + "Один и тот же балл каждый раз даёт разный состав. Сверху ограничено «Механизмов»: "
                + "сложность может запросить больше, чем приёмка успевает проверить — тогда будет "
                + "предупреждение о недоборе в логе."),
                _mazeDifficulty, 4, 30);
            _mazeChain = EditorGUILayout.IntSlider(new GUIContent("Цепочка",
                "Сколько механизмов игрок обязан открыть ПО ПОРЯДКУ: 1 — нажал и прошёл, 3 — открыл, "
                + "поднялся, нашёл вторую кнопку, открыл третью. Сколько выйдет на деле — в логе плана."),
                _mazeChain, 0, 4);
            _mazeDoors = EditorGUILayout.IntSlider(new GUIContent("Дверей-инверсий",
                "Потолок по самозакрывающимся стенам на уровень. 1 — дебют механики, 2-3 — финал пака. "
                + "На то, как ЧАСТО дверь вообще появляется, не влияет: там решает приоритет ворот."),
                _mazeDoors, 0, 3);
            _mazeMechs = EditorGUILayout.IntSlider(new GUIContent("Механизмов",
                "Потолок по числу механизмов на уровень. Ограничение здесь одно — ЦЕНА ПРОВЕРКИ: "
                + "каждый лишний механизм удваивает поиск по состояниям, а приёмка гоняет его ещё раз "
                + "на каждую группу. Предел модели — 12 групп. Игре само число механизмов не мешает."),
                _mazeMechs, 1, 10);
            _mazeHalls = EditorGUILayout.IntSlider(new GUIContent("Двухъярусных залов, %",
                "Как часто вертикальный проход становится ЗАЛОМ: потолок между этажами вырезан почти "
                + "целиком, два этажа читаются как одно высокое пространство. 0 — только приземистые "
                + "комнаты, как было. Дороже по перебросам: зал съедает пол верхней комнаты."),
                _mazeHalls, 0, 100);
            _mazeExcursion = EditorGUILayout.IntSlider(new GUIContent("Вылазок, %",
                "Шанс поставить ВЫЛАЗКУ: тупиковая комната с ключом, войти в которую можно только "
                + "провалившись сверху на вызванную заранее площадку, а выйти — через дверь, "
                + "открываемую изнутри. Съедает три группы сразу."),
                _mazeExcursion, 0, 100);
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
        GUI.backgroundColor = new Color(0.45f, 0.8f, 0.5f);
        if (GUILayout.Button("🌿 Generate free maze → в поле", GUILayout.Height(24)))
            { _schemeText = GenerateFreeScheme(); Debug.Log("[LevelEditor] Свободная раскладка в поле Import — жми Import."); }
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

    /// <summary>Счётчики отладки: сколько вертикальных проходов стали залом, а сколько нет.
    /// Нужны, чтобы не гадать по косвенным признакам, работает ли ручка (уже обжигался: измерял
    /// облик и делал выводы о частоте, хотя частота была почти нулевой).</summary>
    public static int HallsBuilt, HallsSkipped;



    /// <summary>
    /// ⭐ ГЕНЕРАТОР НЕ ОТДАЁТ БРАК: строит схему, САМ проверяет её моделью проходимости
    /// (<see cref="AnalyseScheme"/>) и, если что-то не так, перебрасывает со следующим сидом.
    /// Требование игрока (2026-08-31): «нужно в целом исключить такие моменты, чтобы генератор
    /// генерировал только правильные уровни» — точечно чинить конкретный сид бессмысленно.
    ///
    /// Почему перебросом, а не починкой на месте: брак редкий (замер — 4 схемы из 405, все одного
    /// вида: ступенька под люком встала во всю ширину комнаты и запечатала её пол вместе с мостом
    /// и кнопками), а починка «на месте» правит геометрию уже после всех проходов и легко родит
    /// новый зажим. Переброс детерминирован: сид n даёт n, n+1, n+2… — один и тот же результат
    /// при повторе. При _mazeSeed = 0 (случайный) каждая попытка просто случайная.
    /// </summary>
    private string GenerateMazeScheme()
    {
        const int MaxTries = 12;
        /// Сколько попыток тратим на ДОБОР ГЛУБИНЫ, когда чистая схема уже есть (см. ниже).
        const int ChainSearchTries = 5;
        string best = null; int bestScore = int.MinValue; int usedTry = 0;
        // Лучшая ЧИСТАЯ схема, которой не хватило только глубины: если запрошенное сцепление ни разу
        // не выйдет, отдадим самую глубокую из чистых, а не первую попавшуюся.
        string bestClean = null; int bestCleanRank = -1, bestCleanChain = -1, bestCleanTry = 0;
        // 🐞 ПЕРЕБРОС СЪЕДАЛ ВЫЛАЗКУ. Приёмка про неё не знала и спокойно меняла схему С вылазкой на
        // схему БЕЗ неё, если та лучше по глубине — ползунок «Вылазок, %» переставал что-либо значить
        // (поймано игроком: «7×6 сид 4 вообще не показал шаблона»). Теперь наличие вылазки входит в
        // ранг кандидата и весит больше глубины: это целая головоломка, а не лишний механизм.
        bool wantExcursion = _mazeExcursion > 0;
        // ⚠️ СВОЙСТВА ГРУПП ЕДУТ ВМЕСТЕ СО СХЕМОЙ. _mazeStamps всегда описывают ПОСЛЕДНЮЮ построенную
        // схему, а вернуть мы можем более раннюю. Без переноса InvertedGroupsFor вернул бы пустоту
        // (ключ _mazeStampsFor не совпал), и дверь-инверсия импортировалась бы обычной группой —
        // ровно тот класс бага, на котором уже обожглись с Level_08.
        System.Collections.Generic.List<ModuleStamp> bestCleanStamps = null, bestStamps = null;
        string bestCleanPlan = "", bestPlan = "";
        for (int tryNo = 0; tryNo < MaxTries; tryNo++)
        {
            // ⚠️ ШАГ ПЕРЕБОРА — БОЛЬШОЕ ПРОСТОЕ ЧИСЛО, А НЕ +1. При шаге в единицу соседние сиды
            // сходятся на одной схеме: сид 7 при браке пробует 8, и если чистой оказалась она, то
            // сиды 7, 8 и 9 дают ОДИН И ТОТ ЖЕ уровень (поймано на подборе кандидатов). Для дейли это
            // означало бы одинаковый уровень несколько дней подряд.
            int excBefore = ExcursionModule.StampedCount;
            string scheme = GenerateMazeSchemeOnce(_mazeSeed == 0 ? 0 : _mazeSeed + tryNo * 7919);
            bool hasExcursion = ExcursionModule.StampedCount > excBefore;
            var lines = scheme.Replace("\r", "").Split('\n');
            var grid = new char[lines.Length][];
            for (int i = 0; i < lines.Length; i++) grid[i] = lines[i].ToCharArray();
            var rep = AnalyseScheme(grid, InvertedGroupsFor(scheme), ButtonHostsFor(scheme));
            if (!rep.Bad)
            {
                // ⭐ РУЧКА «ЦЕПОЧКА» — ТРЕБОВАНИЕ, А НЕ ПОЖЕЛАНИЕ. Меряем сцепление по МОДЕЛИ (кто без
                // кого не нажимается), а не по замыслу компоновщика: замер 24 схем показал расхождения
                // в обе стороны. Не добрали глубину — перебрасываем сид, как при любом другом браке.
                if (rep.chainDepth >= _mazeChain && (hasExcursion || !wantExcursion))
                {
                    if (tryNo > 0)
                        Debug.Log($"[Maze] Схема принята с попытки {tryNo + 1}: предыдущие забракованы самопроверкой.");
                    Debug.Log($"[Maze] Рецепт: {_mazeRecipeText}");
                    Debug.Log($"[Maze] Замысел: {_mazePlanText} → по модели сцепление {rep.chainDepth}");
                    return scheme;
                }
                // Ранг: вылазка перевешивает глубину — целая головоломка ценнее лишнего яруса.
                int rank = (hasExcursion ? 1000 : 0) + rep.chainDepth;
                if (rank > bestCleanRank)
                {
                    bestCleanRank = rank; bestCleanChain = rep.chainDepth;
                    bestClean = scheme; bestCleanTry = tryNo;
                    bestCleanStamps = _mazeStamps; bestCleanPlan = _mazePlanText;
                }
                // ⚠️ ЗА ГЛУБИНОЙ ГОНИМСЯ НЕ ДО ПОСЛЕДНЕГО. Брак искать все 12 попыток надо — уровень с
                // недостижимой целью отдавать нельзя ни при каких условиях. А вот «цепочка вышла 2
                // вместо 3» — это НЕ брак, а недобор: схема играбельна. Без этого предела запрос
                // цепочки 3 на форме, которая её не даёт, сжигал все 12 попыток по 4 поиска каждая, и
                // редактор замирал почти на минуту на одну кнопку.
                if (tryNo + 1 >= ChainSearchTries) break;
                continue;
            }
            // Худшее — недостижимая цель, дальше мёртвые группы, дальше замурованный объём.
            int score = -1000 * ((rep.finishOk ? 0 : 1) + (rep.artTotal - rep.artOk) + (rep.cpTotal - rep.cpOk))
                        - 100 * rep.deadGroups.Count - rep.sealedPocket;
            if (score > bestScore)
            { bestScore = score; best = scheme; usedTry = tryNo; bestStamps = _mazeStamps; bestPlan = _mazePlanText; }
        }
        if (bestClean != null)
        {
            _mazeStamps = bestCleanStamps; _mazeStampsFor = bestClean; _mazePlanText = bestCleanPlan;
            Debug.Log($"[Maze] Замысел: {bestCleanPlan} → по модели сцепление {bestCleanChain} "
                + $"вместо запрошенных {_mazeChain}: за {MaxTries} попыток форма лабиринта глубже не дала "
                + $"(отдаю попытку {bestCleanTry + 1}, брака в ней нет).");
            return bestClean;
        }
        _mazeStamps = bestStamps; _mazeStampsFor = best; _mazePlanText = bestPlan;
        Debug.LogWarning($"[Maze] За {MaxTries} попыток чистая схема не вышла — отдаю лучшую из них "
            + $"(попытка {usedTry + 1}). Жми «Проверить схему», чтобы увидеть, что именно не так.");
        return best;
    }

    /// <summary>
    /// ⭐ ГЕНЕРАЦИЯ БЕЗ РЕШЁТКИ (<see cref="FreeMazeBuilder"/>): рецепт → дерево → роли → раскладка
    /// комнат своими размерами → уровень. Живёт РЯДОМ со старым путём, пока не заменит его целиком.
    ///
    /// Переброс сида здесь тот же, что и у решётчатого: генератор не отдаёт брак. Замер на 40 схемах
    /// БЕЗ перебросов дал 7% брака (у решётчатого 36-56%), так что перебросов почти не потребуется.
    /// </summary>
    private string GenerateFreeScheme()
    {
        // ⚠️ ПОПЫТОК МНОГО, ПОТОМУ ЧТО РАСКЛАДКА РЕДКАЯ, А НЕ ПОТОМУ ЧТО СХЕМЫ ПЛОХИЕ.
        // 🐞 Было восемь. На сорока комнатах раскладка сходится в 2 попытках из 8, сид фиксирован —
        // значит неудачный сид давал пустое поле при КАЖДОМ нажатии. Несошедшаяся раскладка стоит
        // ~9 мс, так что два десятка попыток дешевле одной проверки схемы моделью.
        const int MaxTries = 24;
        string best = null; int bestScore = int.MinValue;
        System.Collections.Generic.List<ModuleStamp> bestStamps = null;
        string bestRecipe = "", bestStory = "";
        // ⚠️ ПОТОЛОК РАЗМЕРА ЖИВЁТ В РАСКЛАДКЕ, А НЕ ЗДЕСЬ. На сорока комнатах RoomLayout сходится
        // лишь в 2 попытках из 8, а сид фиксирован — поэтому неудачный сид давал пустое поле при
        // КАЖДОМ нажатии (поймано игроком). Ужимать заказ в ответ нельзя: это подменяет то, что
        // попросили, и потолок остаётся на месте. Лечится только самой раскладкой.
        int wantRooms = Mathf.Clamp(_mazeW * _mazeH / 3, 6, 40);
        // Один и тот же сид нужно уметь отыграть ДВАЖДЫ (с отделкой и без), поэтому сид всегда явный.
        var seedRng = new System.Random();
        System.Func<int, bool, FreeMazeBuilder.Built> build = (seed, dressed) =>
        {
            FreeMazeBuilder.SkipDressing = !dressed;
            var r = new System.Random(seed);
            // ⚠️ Вылазку тут пока не заказываем: её камере нужны ДВЕ связи (боковой выход и провал
            // сверху), а раскладка строит дерево, где связь одна. Отдельная задача.
            var rec = LevelRecipe.RollForDifficulty(r, _mazeDifficulty, _mazeMechs, false);
            // Размер уровня — отдельная ручка: «Комнат ↔ × Комнат ↕» задают, сколько комнат строить.
            // ⚠️ ЗАЛЫ ПОКА ОТКЛЮЧЕНЫ (передаём 0 вместо _mazeHalls). Высокая комната даёт объём —
            // замер: пробегов выше шести клеток 5% → 15% при половине залов, — но ломает проходимость:
            // финиш достижим на 2 уровнях из 5 вместо 5, холдов 46% вместо 65%. Две причины уже нашёл
            // и починил (шаг лестницы вбок сверх дотяжки; лестница считалась от пола, и верхняя
            // площадка оказывалась в 4 рядах от потолка при подъёме 3), но что-то осталось.
            // Включать обратно — только после того, как зал будет разобран по клеткам моделью.
            var b = FreeMazeBuilder.Build(rec, r, wantRooms, 10, 0);
            FreeMazeBuilder.SkipDressing = false;
            if (b != null) b.recipeText = rec.Points + "б: " + rec.Describe();
            return b;
        };
        System.Func<FreeMazeBuilder.Built, SchemeReport> check = (b) =>
        {
            var inv = new System.Collections.Generic.HashSet<string>();
            var hosts = new System.Collections.Generic.Dictionary<Vector2Int, string>();
            foreach (var st in b.stamps)
            {
                if (st.inverted) inv.Add(st.groupId.ToString());
                for (int i = 0; i < st.buttons.Count && i < st.buttonHosts.Count; i++)
                    if (st.buttonHosts[i] != '\0') hosts[st.buttons[i]] = st.buttonHosts[i].ToString();
            }
            var lines = b.scheme.Replace("\r", "").Split('\n');
            var grid = new char[lines.Length][];
            for (int i = 0; i < lines.Length; i++) grid[i] = lines[i].ToCharArray();
            return AnalyseScheme(grid, inv, hosts);
        };

        for (int tryNo = 0; tryNo < MaxTries; tryNo++)
        {
            int seed = _mazeSeed == 0 ? seedRng.Next(int.MaxValue) : _mazeSeed + tryNo * 7919;
            var built = build(seed, true);
            if (built == null) continue;
            var rep = check(built);

            // 🐞 ОТДЕЛКА СЪЕДАЛА УРОВЕНЬ. Декор изредка запечатывает воздушный карман так, что ключ
            // перестаёт браться (замер: 2 схемы из 16, «замуровано» 0→8). Откат внутри Decorate ловит
            // не всё — там видна только топология воздуха, а ломается ДОТЯЖКА, а её знает только модель.
            // Поэтому сид, испорченный отделкой, переигрываем голым, а не выбрасываем.
            if (rep.Bad)
            {
                var plain = build(seed, false);
                if (plain != null)
                {
                    var prep = check(plain);
                    if (!prep.Bad)
                    {
                        Debug.Log("[Свободный] Отделка ломала схему — сид отыгран без декора и монет.");
                        built = plain; rep = prep;
                    }
                }
            }

            var recipe = built.recipeText;
            if (!rep.Bad)
            {
                _mazeStamps = built.stamps; _mazeStampsFor = built.scheme;
                _mazeRecipeText = recipe;
                _mazePlanText = built.story;
                Debug.Log($"[Свободный] Рецепт: {_mazeRecipeText}");
                Debug.Log($"[Свободный] {built.story} → сцепление {rep.chainDepth}"
                        + (tryNo > 0 ? $" (принято с попытки {tryNo + 1})" : ""));
                return built.scheme;
            }
            int score = -1000 * ((rep.finishOk ? 0 : 1) + (rep.artTotal - rep.artOk))
                        - 100 * rep.idleGroups.Count - rep.deadInternal;
            if (score > bestScore)
            { bestScore = score; best = built.scheme; bestStamps = built.stamps;
              bestRecipe = recipe; bestStory = built.story; }
        }
        if (best == null) { Debug.LogWarning("[Свободный] Раскладка не сошлась ни разу."); return ""; }
        _mazeStamps = bestStamps; _mazeStampsFor = best;
        _mazeRecipeText = bestRecipe; _mazePlanText = bestStory;
        Debug.LogWarning($"[Свободный] За {MaxTries} попыток чистой схемы не вышло — отдаю лучшую. "
                       + "Жми «Проверить схему», чтобы увидеть, что не так.");
        return best;
    }

    private string GenerateMazeSchemeOnce(int seedValue)
    {
        var rng = seedValue == 0 ? new System.Random() : new System.Random(seedValue);

        // ── РЕЦЕПТ — ПЕРВЫМ ДЕЛОМ, ДО ВСЯКОЙ ГЕОМЕТРИИ ───────────────────────────────────────────
        // ⭐ Предложение игрока: управлять СОСТАВОМ уровня, а не частотой выпадения паттернов.
        // Рецепт говорит, из чего уровень состоит («основной маршрут: ворота ×3, мост; ветка к ключу:
        // вылазка»), и уже под него подбирается всё остальное. Разнообразие — из разных рецептов, а не
        // из разных бросков одной геометрии.
        // ⭐ Сложность задаётся БАЛЛАМИ, а состав под них подбирается сам (предложение игрока):
        // «ворота ×3» и «одна вылазка» — разная задача, числом механизмов это не выразить.
        var recipe = LevelRecipe.RollForDifficulty(rng, _mazeDifficulty, _mazeMechs, _mazeExcursion > 0);
        _mazeRecipeText = recipe.Points + "б: " + recipe.Describe();
        if (recipe.Points < _mazeDifficulty - 2)
            _mazeRecipeText += $" ⚠ недобор до {_mazeDifficulty}б: потолок в {_mazeMechs} групп "
                             + "(поднимать нельзя без роста цены приёмки)";

        // ⭐ РАЗМЕР СЕТКИ — СЛЕДСТВИЕ РЕЦЕПТА, А НЕ НАСТРОЙКА (решение игрока: «если в маленькой сетке
        // не помещается, делать такую, в которой помещается»). Запас втрое: маска формы выкусывает
        // часть клеток, а остовное дерево виляет, поэтому «комнат ровно столько же» не хватает.
        int CW = Mathf.Clamp(_mazeW, 2, 12), CH = Mathf.Clamp(_mazeH, 2, 12);
        int roomsNeeded = recipe.RoomsNeeded * 3;
        while (CW * CH < roomsNeeded && (CW < 12 || CH < 12))
        {
            if (CW <= CH && CW < 12) CW++;
            else if (CH < 12) CH++;
            else CW++;
        }
        if (CW != Mathf.Clamp(_mazeW, 2, 12) || CH != Mathf.Clamp(_mazeH, 2, 12))
            _mazeRecipeText += $" (сетка расширена до {CW}×{CH}: рецепту нужно {recipe.RoomsNeeded} комнат)";

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
        // ⭐ КОЛОННЫ-ЗАЛЫ РЕШАЮТСЯ ДО ШИРИНЫ, А НЕ ПОСЛЕ. Сначала я выбирал зал уже по готовой комнате
        // («широкая? сделаю залом»), и при 35% строился ОДИН зал на уровень: комнаты шириной ≥7 сами
        // по себе редки. Это тот же промах, что был у механизмов до компоновщика — решать по факту
        // геометрии вместо того, чтобы геометрию под замысел и строить.
        // Теперь: сначала колонка объявляется зальной, и уже поэтому получает ширину 7-8.
        // ⭐ НАЗНАЧЕНИЕ РОЛЕЙ: рецепт из заказа становится нарядом. Делается ЗДЕСЬ — дерево уже есть,
        // размеры комнат ещё не выбраны, и потому их можно выдать под роли (см. ниже).
        var roleSlots = LevelRecipe.AssignRoles(recipe, CW, CH,
            (x, y) => x >= 0 && x < CW && y >= 0 && y < CH && alive[x, y],
            (x, y) => x >= 0 && x < CW && y >= 0 && y < CH && hPass[x, y],
            (x, y) => x >= 0 && x < CW && y >= 0 && y < CH && vPass[x, y],
            startRoom, far);
        if (roleSlots.Count < recipe.ElementCount)
            _mazeRecipeText += $" ⚠ размещено ролей {roleSlots.Count} из {recipe.ElementCount} — "
                             + "форма дерева вместила не всё";

        var hallCol = new bool[CW];
        var colW = new int[CW];
        for (int x = 0; x < CW; x++)
        {
            hallCol[x] = rng.Next(100) < _mazeHalls;
            // ⚠️ Обычные колонки остаются 3..8 как были — иначе при нулевой ручке из уровня пропали бы
            // комнаты шириной 8, а на них держится мост (ему нужен пролёт шире дотяжки).
            colW[x] = hallCol[x] ? 7 + rng.Next(2) : 3 + rng.Next(6);
        }
        var rowH = new int[CH]; for (int y = 0; y < CH; y++)
            rowH[y] = Mathf.Min(MazeClimb + 2, 3 + rng.Next(3));                          // высота комнат 3..5
        {
            // ⭐⭐ РАЗМЕРЫ ВЫДАЮТСЯ ПОД РОЛИ, А НЕ БРОСАЮТСЯ ВСЛЕПУЮ. Здесь рецепт и перестаёт быть
            // лотереей: каждый элемент уже привязан к конкретной комнате (см. AssignRoles выше), и
            // её колонка/строка получают ровно то, что элемент просил. Раньше размеры кидались до
            // всякого замысла, и мост (нужна ширина 8) или вылазка (глубина 5) ждали удачи —
            // замер давал одну вылазку на сорок схем.
            // ⚠️ Требования вида «не меньше», поэтому берётся МАКСИМУМ по строке и колонке —
            // упаковка не нужна, конфликтов не бывает.
            foreach (var slot in roleSlots)
            {
                var need = PuzzleVocabulary.Need(slot.element);
                foreach (var rm in new[] { slot.roomA, slot.roomB })
                {
                    if (rm.x < 0 || rm.x >= CW || rm.y < 0 || rm.y >= CH) continue;
                    colW[rm.x] = Mathf.Clamp(Mathf.Max(colW[rm.x], need.minWidth), 3, 8);
                    // ⚠️ Высота ограничена климбом: подъёмы строим ≤3 рядов, отсюда H ≤ Climb+2.
                    rowH[rm.y] = Mathf.Clamp(Mathf.Max(rowH[rm.y], need.minHeight), 3, MazeClimb + 2);
                }
            }
        }
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
        var shafts = new System.Collections.Generic.List<(Vector2Int room, int stepRow, int col0, int width,
                                                  int hatchRow, int hatchCol0, int hatchWidth)>();
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
                // ⭐ ДВУХЪЯРУСНЫЙ ЗАЛ. Замер подписи геометрии: 92% вертикальных пустот — 1-5 клеток,
                // весь уровень поле приземистых коробок, и оттого уровни неотличимы друг от друга.
                // Здесь потолок между этажами вырезается ПОЧТИ ЦЕЛИКОМ, и два этажа читаются как один
                // высокий зал — вертикальная пустота сразу вдвое выше всего, что генератор умел.
                // ⚠️ По краям потолок ОСТАЁТСЯ (2 клетки с каждой стороны): это пол верхней комнаты
                // у дверных проёмов. Без него игрок, шагнув в проём наверху, ступил бы в пустоту, а
                // пэд повис бы над пропастью (уже обжигались на этом у флага).
                // ⚠️ Нужна ширина ≥7: 2+2 клетки полок и ≥3 клетки проёма.
                // Второй раз не бросаем: колонка уже объявлена зальной и ради этого получила ширину.
                bool hall = hallCol[cx] && W(cx) >= 7;
                if (hall) HallsBuilt++; else HallsSkipped++;
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
                // Проём в потолке и СТУПЕНЬКА — теперь это две разные вещи. У обычной шахты они
                // совпадают (люк 2-3 клетки, под ним такая же ступенька). У зала проём во всю комнату,
                // а ступенька остаётся узкой и жмётся к ЛЕВОЙ полке: подъём с неё на полку — это те же
                // выстраданные H−climb+1 рядов, только вбок на клетку-другую, а не строго вверх.
                int hatchS = s, hatchW = hw, stepS = s, stepW = hw;
                if (hall)
                {
                    hatchS = 2; hatchW = W(cx) - 4;                        // 2 клетки полки слева и справа
                    stepS = 2; stepW = Mathf.Min(3, hatchW);
                }
                for (int k = 0; k < hatchW; k++) carve(ceilRow, C0(cx) + hatchS + k);
                for (int k = 0; k < stepW;  k++) set(stepRow, C0(cx) + stepS + k, '#');
                for (int k = -1; k <= hatchW; k++)                         // защищаем посадочные холды по краям проёма
                {
                    int c = C0(cx) + hatchS + k;
                    if (ceilRow - 1 >= 0 && c >= 0 && c < cols) noBump[ceilRow - 1, c] = true;
                }
                shafts.Add((new Vector2Int(cx, cy), stepRow, C0(cx) + stepS, stepW,
                            ceilRow, C0(cx) + hatchS, hatchW));
                for (int k = 0; k < hatchW; k++)                           // ствол (ступенька→проём) = коридор
                for (int rr = ceilRow; rr < stepRow; rr++)
                {
                    int c = C0(cx) + hatchS + k;
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
        // ⭐ КЛЮЧ ОБЯЗАН ЛЕЧЬ В КАМЕРУ ВЫЛАЗКИ. Иначе запирать нечего: вся конструкция строится ради
        // того, что за ней лежит. Роль назначена раньше (см. AssignRoles), здесь мы лишь двигаем её
        // комнату в начало очереди на ключи.
        foreach (var slot in roleSlots)
            if (slot.element == PuzzleElement.Excursion && deadEnds.Remove(slot.roomA))
                deadEnds.Insert(0, slot.roomA);

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

        // ── ЗАМЫСЕЛ ГОЛОВОЛОМКИ: СНАЧАЛА ПЛАН, ПОТОМ ГЕОМЕТРИЯ ───────────────────────────────────
        // ⭐ Раньше здесь стояли ТРИ независимых прохода, и каждый искал, куда бы воткнуть свой
        // механизм: «шахта режет дерево? ставлю ворота», «широкий пол? ставлю мост». Механизмы не
        // знали друг о друге, цепочка «нажал A → дотянулся до B» получалась случайно, и уровень
        // выходил не задуманным, а насыпанным (вердикт игрока: «уровни среднего качества»).
        //
        // Теперь порядок обратный. <see cref="PuzzleComposer"/> на ГОЛОМ ДЕРЕВЕ КОМНАТ решает, какие
        // рёбра заперты, чем и в каком порядке игрок обязан их открывать — не зная ни клетки. Дальше
        // генератор вписывает план в геометрию и ИМЕЕТ ПРАВО ОТКАЗАТЬ: не встало — ребро в запрет,
        // план строится заново без него. Проверка моделью после сборки остаётся страховкой, но
        // «каждый механизм несущий» и «цепочка настоящая» теперь верны ПО ПОСТРОЕНИЮ, а не по
        // отбраковке постфактум.
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
        // Связаны ли две комнаты, если ИСКЛЮЧИТЬ третью. Нужно мосту: понять, по какую сторону от
        // него лежит комната снизу (см. «падение = откат, а не срезка»).
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

        var gateGroups = new System.Collections.Generic.List<char>();
        // Что именно поставили модули: свойства групп (инверсия), которые сетка символов не выражает.
        var stamps = new System.Collections.Generic.List<ModuleStamp>();
        // Потолок по числу механизмов: ручка из панели, но не больше, чем позволяет размер лабиринта —
        // одна комната несёт один механизм, и в сетке 4×4 десяти просто некуда встать.
        int maxGates = Mathf.Clamp(Mathf.Min(_mazeMechs, Mathf.Max(1, (CW * CH) / 5)), 1, 10);

        // ── МИР ГОЛОВОЛОМКИ: дерево комнат и цели, без единой клетки ──
        var world = new PuzzleComposer.PuzzleWorld { start = startRoom, finish = far };
        for (int x = 0; x < CW; x++)
        for (int y = 0; y < CH; y++)
        {
            if (!alive[x, y]) continue;
            var rm = new Vector2Int(x, y);
            world.adj[rm] = nbrs(rm);
            // При H=3 ступенька ворот бессмысленна — до потолка долезут и без неё.
            if (H(y) < 4) world.noGate.Add(rm);
            bool hL = x > 0 && hPass[x - 1, y], hR = x + 1 < CW && hPass[x, y];
            bool vU = y + 1 < CH && vPass[x, y],  vD = y > 0 && vPass[x, y - 1];
            // Мост осмыслен только в СКВОЗНОМ горизонтальном коридоре (иначе обойдут — фидбэк игрока)
            // И только если пролёт ШИРЕ ДОТЯЖКИ: через разрыв ≤6 клеток игрок перетягивается руками,
            // и пол ему не нужен вовсе (см. TimedBridgeModule.Fits).
            if (hL && hR && !vU && !vD && W(x) >= MazeCanvas.ReachSide + 2) world.corridors.Add(rm);
        }
        string excursionText = "";
        // ── ВЫЛАЗКА: целая головоломка на тупиковой комнате с ключом ─────────────────────────────
        // ⭐ Ставится ДО компоновщика и целиком: это не «ещё один механизм», а готовая петля из трёх
        // групп (перенос ручного Level_07 — см. ExcursionModule). Компоновщик потом планирует вокруг:
        // её комнаты попадают в busy, и он туда не лезет.
        // Условия жёсткие, поэтому вылазка редка — и это правильно: она должна быть событием.
        // ⭐⭐ МЕСТО БЕРЁТСЯ ИЗ НАРЯДА, А НЕ ИЩЕТСЯ. Раньше этот проход сам сканировал комнаты в
        // надежде, что какая-то подойдёт — замер давал одну вылазку на сорок схем. Теперь комната
        // назначена рецептом ДО выбора размеров и уже получила нужные глубину и ширину.
        foreach (var slot in roleSlots)
        {
            if (slot.element != PuzzleElement.Excursion) continue;
            var kr = slot.roomA; var up = slot.roomB;
            // ⚠️ Причину отказа записываем в лог: заказанный рецептом элемент, который молча не
            // построился, — это ровно та слепота, из-за которой вылазка полгода была лотереей.
            if (gateGroups.Count + 3 > maxGates)
            { excursionText = "⚠ вылазка не построена: нет бюджета групп; "; break; }
            if (!keyRooms.Contains(kr))
            { excursionText = $"⚠ вылазка не построена: в камеру {kr} не лёг ключ; "; continue; }
            // Единственное ребро дерева обязано быть ГОРИЗОНТАЛЬНЫМ — оно станет дверью-выходом.
            int exitCx = -1;
            if (kr.x > 0 && hPass[kr.x - 1, kr.y]) exitCx = kr.x - 1;
            else if (kr.x + 1 < CW && hPass[kr.x, kr.y]) exitCx = kr.x;
            if (exitCx < 0)
            { excursionText = $"⚠ вылазка не построена: у камеры {kr} нет горизонтального выхода; "; continue; }
            int col = C0(exitCx) + W(exitCx), rBottom = R0(kr.y) + H(kr.y) - 1;
            int hDoor = 0;
            while (hDoor < H(kr.y) && at(rBottom - hDoor, col) == '.') hDoor++;
            if (hDoor < 2) continue;
            var spotIn = findFloorSpot(kr);
            var spotUp = findFloorSpot(up);
            if (spotIn.x < 0 || spotUp.x < 0) continue;
            var canvas = new MazeCanvas(g, rows, cols, CW, CH, colX, rowY, colW, rowH,
                                        bump, noBump, noFill, rng);
            canvas.SetNextGroupIndex(gateGroups.Count);
            excursionText = $"⚠ вылазка не построена: модуль отказал в камере {kr}; ";
            var trio = new ExcursionModule().StampAll(canvas, new MazeSite
            {
                roomId = kr.y * CW + kr.x, roomAboveId = up.y * CW + up.x, room = kr, roomAbove = up, onRoute = false,
                doorCol = col, doorRowTop = rBottom - hDoor + 1, doorHeight = hDoor,
                buttonBelow = spotIn, buttonAbove = spotUp
            });
            if (trio == null) continue;
            foreach (var st in trio) { gateGroups.Add(st.groupId); stamps.Add(st); }
            usedRooms.Add(kr); usedRooms.Add(up);
            // ⚠️ Вылазку компоновщик своей не считает (её ставят до него), поэтому в отчёт о замысле
            // она попадает отдельной строкой — иначе лог говорил бы «механизмов 3» при шести группах.
            excursionText = "ВЫЛАЗКА в комнате " + kr + " (пусковая " + trio[0].groupId
                          + ", ловчая " + trio[1].groupId + ", выход " + trio[2].groupId + "); ";
            break;                                                     // одна вылазка на уровень
        }

        foreach (var kr in keyRooms) world.keys.Add(kr);
        foreach (var ur in usedRooms) world.busy.Add(ur);

        var builtGates = new System.Collections.Generic.List<PuzzleComposer.PlannedGate>();
        {
            // Рёбра, на которых геометрия уже отказала: плану сюда больше не ходить.
            var banned = new System.Collections.Generic.HashSet<string>();
            // ⭐ КУДА МОЖНО ПОВЕСИТЬ ВЛОЖЕННУЮ КНОПКУ: клетка воздуха НАД ступенькой построенных ворот.
            // Пока ворота не нажаты, ступеньки нет — кнопка висит призраком и не нажимается; нажал
            // хозяина, встал на ступеньку — и кнопка под рукой. Именно «под рукой»: модель не знает
            // времени, поэтому бежать до вложенной кнопки нельзя ни одной клетки (см. PlannedGate.nestOn).
            var mountSpot = new System.Collections.Generic.Dictionary<PuzzleComposer.PlannedGate, Vector2Int>();
            int wantChain = Mathf.Clamp(_mazeChain, 0, maxGates);
            for (int attempt = 0; attempt < 4 && gateGroups.Count < maxGates; attempt++)
            {
                // ⚠️ Лимит на двери-инверсии считается по УЖЕ ПОСТРОЕННЫМ: попыток планирования
                // несколько, и каждый новый план про предыдущие двери не знает.
                int doorsBuilt = 0;
                foreach (var bg in builtGates) if (bg.role == PuzzleComposer.Role.Door) doorsBuilt++;
                var plan = PuzzleComposer.Plan(world, wantChain, maxGates - gateGroups.Count, rng,
                                               _mazeDoors - doorsBuilt, banned);
                if (plan.gates.Count == 0) break;
                bool failed = false;
                foreach (var pg in plan.gates)
                {
                    if (gateGroups.Count >= maxGates) break;
                    ModuleStamp st = null;
                    // Куда встанет кнопка «открыть». У вложенного механизма — на платформу хозяина;
                    // если хозяин ту платформу не дал, механизм НЕ ставим вовсе: молча уронить кнопку
                    // на пол значило бы потерять зависимость, а план продолжал бы обещать цепочку.
                    var nestAt = new Vector2Int(-1, -1);
                    if (pg.nestOn != null && !mountSpot.TryGetValue(pg.nestOn, out nestAt))
                        nestAt = new Vector2Int(-1, -1);
                    bool nestFailed = pg.nestOn != null && nestAt.x < 0;
                    // Куда встанет ступенька этих ворот — понадобится, чтобы предложить её под вложенную
                    // кнопку следующему механизму.
                    var myMount = new Vector2Int(-1, -1);

                    if (nestFailed) { }
                    else if (pg.role == PuzzleComposer.Role.Gate)
                    {
                        // Шахта между комнатами уже прорезана (ребро дерева = вертикальный проход),
                        // ключ в списке — НИЖНЯЯ комната, а она у подъёма всегда родитель.
                        int shIdx = -1;
                        for (int k = 0; k < shafts.Count; k++)
                            if (shafts[k].room.Equals(pg.edge.parent)) { shIdx = k; break; }
                        var spotBelow = nestAt.x >= 0 ? nestAt : findFloorSpot(pg.edge.parent);
                        var spotAbove = findFloorSpot(pg.edge.child);
                        if (shIdx >= 0 && spotBelow.x >= 0 && spotAbove.x >= 0)
                        {
                            var sh = shafts[shIdx];
                            var canvas = new MazeCanvas(g, rows, cols, CW, CH, colX, rowY, colW, rowH,
                                                        bump, noBump, noFill, rng);
                            canvas.SetNextGroupIndex(gateGroups.Count);
                            // Полку под вложенную кнопку просим ТОЛЬКО если план на эти ворота кого-то
                            // вешает: лишняя пристройка к ступеньке никому не нужна.
                            bool needsShelf = false;
                            foreach (var other in plan.gates) if (other.nestOn == pg) { needsShelf = true; break; }
                            st = new VerticalGateModule().Stamp(canvas, new MazeSite
                            {
                                roomId = pg.edge.parent.y * CW + pg.edge.parent.x, roomAboveId = pg.edge.child.y * CW + pg.edge.child.x, room = pg.edge.parent, roomAbove = pg.edge.child, onRoute = true,
                                shaftCol0 = sh.col0, shaftWidth = sh.width, shaftStepRow = sh.stepRow,
                                buttonBelow = spotBelow, buttonAbove = spotAbove,
                                wantButtonShelf = needsShelf
                            });
                            // 🐞 Раньше площадку считал сам генератор — «клетка над ступенькой», — и она
                            // совпадала с местом, куда игрок ставит пэд, вставая на ступеньку (поймано
                            // игроком на первом импорте). Теперь площадку выдаёт МОДУЛЬ: он пристраивает
                            // к ступеньке отдельную колонку, а хваты остаются свободными.
                            if (st != null) myMount = st.shelfCell;
                        }
                    }
                    else if (pg.role == PuzzleComposer.Role.Door)
                    {
                        int cx = Mathf.Min(pg.edge.parent.x, pg.edge.child.x), cy = pg.edge.parent.y;
                        int col = C0(cx) + W(cx), rBottom = R0(cy) + H(cy) - 1;
                        int hDoor = 0;
                        while (hDoor < H(cy) && at(rBottom - hDoor, col) == '.') hDoor++;
                        var spotNear = nestAt.x >= 0 ? nestAt : findFloorSpot(pg.edge.parent);
                        var spotFar  = findFloorSpot(pg.edge.child);
                        if (hDoor >= 2 && spotNear.x >= 0 && spotFar.x >= 0)
                        {
                            var canvas = new MazeCanvas(g, rows, cols, CW, CH, colX, rowY, colW, rowH,
                                                        bump, noBump, noFill, rng);
                            canvas.SetNextGroupIndex(gateGroups.Count);
                            st = new InvertedDoorModule().Stamp(canvas, new MazeSite
                            {
                                roomId = pg.edge.parent.y * CW + pg.edge.parent.x, roomAboveId = pg.edge.child.y * CW + pg.edge.child.x, room = pg.edge.parent, roomAbove = pg.edge.child, onRoute = true,
                                doorCol = col, doorRowTop = rBottom - hDoor + 1, doorHeight = hDoor,
                                buttonBelow = spotNear, buttonAbove = spotFar
                            });
                        }
                    }
                    else
                    {
                        // ⭐ ПАДЕНИЕ = ОТКАТ, А НЕ СРЕЗКА (требование игрока). Убирая пол, мы создаём
                        // связь в комнату снизу, которой в дереве не было. Если та комната на стороне
                        // ФИНИША, провал уносит игрока ВПЕРЁД мимо моста — механика превращается в
                        // короткий путь. Требуем сторону СПАВНА: упавший возвращается к пройденному.
                        var room = pg.edge.parent;
                        bool fallOk = true;
                        Vector2Int prevRoom;
                        if (room.y > 0 && alive[room.x, room.y - 1] && parent.TryGetValue(room, out prevRoom))
                            fallOk = connectedWithout(room, prevRoom, new Vector2Int(room.x, room.y - 1));
                        if (fallOk)
                        {
                            var canvas = new MazeCanvas(g, rows, cols, CW, CH, colX, rowY, colW, rowH,
                                                        bump, noBump, noFill, rng);
                            canvas.SetNextGroupIndex(gateGroups.Count);
                            st = new TimedBridgeModule().Stamp(canvas, new MazeSite
                            {
                                roomId = room.y * CW + room.x, room = room, roomAbove = new Vector2Int(-1, -1),
                                onRoute = true, throughCorridor = true,
                                hasFloorBelow = room.y > 0 && alive[room.x, room.y - 1]
                            });
                        }
                    }

                    if (st == null)
                    {
                        // Геометрия отказала — ребро в запрет, и план строится заново уже без него.
                        banned.Add(PuzzleComposer.EdgeKey(pg.edge));
                        failed = true; break;
                    }
                    pg.groupId = st.groupId;
                    // ⚠️ Хозяина кнопки записываем В ШТАМП: сетка символов вложенность выразить не может,
                    // и без этого канала импорт положит кнопку на общий контейнер, а приёмка сочтёт её
                    // вечно доступной — то есть примет уровень, который на деле не проходится.
                    if (pg.nestOn != null)
                        for (int bi = 0; bi < st.buttons.Count; bi++)
                            if (st.buttons[bi] == nestAt) st.buttonHosts[bi] = pg.nestOn.groupId;
                    if (myMount.x >= 0) mountSpot[pg] = myMount;
                    gateGroups.Add(st.groupId); stamps.Add(st); builtGates.Add(pg);
                    usedRooms.Add(pg.edge.parent);  usedRooms.Add(pg.edge.child);
                    world.busy.Add(pg.edge.parent); world.busy.Add(pg.edge.child);
                }
                if (!failed) break;
            }
            // ⚠️ Ярусы пересчитываем ПО ФАКТУ построенного: планов могло быть несколько, и у каждого
            // своя нумерация. Без пересчёта отчёт о сложности врал бы в меньшую сторону.
            PuzzleComposer.Retier(world, builtGates);
        }
        _mazePlanText = excursionText + new PuzzleComposer.PuzzlePlan { gates = builtGates }.Describe();
        if (excursionText.StartsWith("⚠")) _mazeRecipeText += " " + excursionText.Trim();

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
        var scheme = sb.ToString();
        // Свойства групп, которых нет в сетке символов, привязываем К ЭТОЙ схеме — чтобы они не
        // применились к чужому тексту, вставленному игроком в поле импорта вручную.
        _mazeStamps = stamps; _mazeStampsFor = scheme;
        return scheme;
    }

    /// <summary>
    /// ⭐ ХОЗЯЕВА КНОПОК для ДАННОЙ схемы: клетка сетки (столбец, ряд сверху) → ключ группы, ВНУТРИ
    /// которой лежит кнопка. Пусто, если схема не наша (игрок вставил свой текст в поле импорта).
    /// Второй канал после инверсии — по той же причине: ASCII выражает геометрию, но не иерархию.
    /// </summary>
    private System.Collections.Generic.Dictionary<Vector2Int, string> ButtonHostsFor(string scheme)
    {
        var map = new System.Collections.Generic.Dictionary<Vector2Int, string>();
        if (_mazeStampsFor != scheme) return map;
        foreach (var st in _mazeStamps)
        for (int i = 0; i < st.buttons.Count && i < st.buttonHosts.Count; i++)
            if (st.buttonHosts[i] != '\0') map[st.buttons[i]] = st.buttonHosts[i].ToString();
        return map;
    }

    /// <summary>Инверсные группы для ДАННОЙ схемы (пусто, если схема не наша).</summary>
    private System.Collections.Generic.HashSet<string> InvertedGroupsFor(string scheme)
    {
        var set = new System.Collections.Generic.HashSet<string>();
        if (_mazeStampsFor != scheme) return set;
        foreach (var st in _mazeStamps) if (st.inverted) set.Add(st.groupId.ToString());
        return set;
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
        // Кнопка → группа-ХОЗЯИН (платформа, к которой она припаркована). ⚠️ Схема этого выразить не
        // может, поэтому иерархия едет отдельным каналом — как и инверсия.
        var nestedHost = new System.Collections.Generic.Dictionary<Transform, string>();
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
                    {
                        var dp = cont.GetComponent<DisappearingPlatform>(); string gid = dp != null ? dp.groupId : "A";
                        foreach (Transform t in cont)
                        {
                            // ⭐⭐ ВЛОЖЕННАЯ КНОПКА — ЭТО КНОПКА, А НЕ ТАЙЛ ПЛАТФОРМЫ.
                            // 🐞 Раньше В ТАЙЛЫ ГРУППЫ уходили ВСЕ дети контейнера, включая триггер,
                            // припаркованный к платформе (так игрок собирает вложенность: Trigger —
                            // дочерний объект Disappear_B). Кнопка исчезала из схемы совсем: у группы
                            // оставались тайлы и НОЛЬ кнопок, стена не открывалась никогда, и модель
                            // честно объявляла ключ за ней недостижимым. Так «поломались» Level_07,
                            // 10 и 11 — то есть ровно те, где вложенные кнопки и есть.
                            var nested = t.GetComponent<TriggerTile>();
                            if (nested == null) nested = t.GetComponentInChildren<TriggerTile>(true);
                            if (nested != null)
                            {
                                buttons[t] = nested.groupId;
                                nestedHost[t] = gid;          // хозяин: платформа, на которой сидит
                                continue;
                            }
                            disTiles[t] = gid;
                        }
                    }
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
        // ── Боковой канал: инверсия групп и хозяева вложенных кнопок ──
        // ⚠️ Ни то, ни другое ASCII не выражает, а без них разбор уровня ВРЁТ: стена без кнопки
        // выглядит вечной, а обычная группа — инверсной. Складываем их рядом со схемой, как это
        // уже сделано для генератора (_mazeStampsFor).
        _exportInverted.Clear(); _exportHosts.Clear();
        foreach (var dpc in root.GetComponentsInChildren<DisappearingPlatform>(true))
            if (dpc.inverted && dpc.groupId.Length > 0)
                _exportInverted.Add(char.ToUpperInvariant(dpc.groupId[0]).ToString());
        foreach (var kv in nestedHost)
        {
            int x = Mathf.RoundToInt(kv.Key.position.x / cell), y = Mathf.RoundToInt(kv.Key.position.y / cell);
            _exportHosts[new Vector2Int(x - minX, maxY - y)] =   // в координаты СЕТКИ (ряд сверху)
                char.ToUpperInvariant(kv.Value.Length > 0 ? kv.Value[0] : 'A').ToString();
        }
        _exportFor = sb.ToString();
        return _exportFor;
    }

    /// <summary>Инверсия и хозяева кнопок последнего экспортированного уровня (см. ExportScheme).</summary>
    private readonly System.Collections.Generic.HashSet<string> _exportInverted =
        new System.Collections.Generic.HashSet<string>();
    private readonly System.Collections.Generic.Dictionary<Vector2Int, string> _exportHosts =
        new System.Collections.Generic.Dictionary<Vector2Int, string>();
    private string _exportFor;

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
        // Свойства, которых нет в сетке символов: какие группы инверсные и какие кнопки лежат ВНУТРИ
        // группы. Привязаны к ЭТОЙ схеме — если игрок вставил в поле свой текст, каналы пусты.
        var schemeHosts = ButtonHostsFor(text);
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
                // usePanelMode: false — импорт НЕ должен зависеть от тумблеров панели, см. метод.
                var container = GetOrCreateDisappearGroup(gid, false);
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
                // ⭐ ВЛОЖЕННАЯ КНОПКА кладётся ВНУТРЬ группы-хозяина, а не в общий контейнер: именно
                // родительство делает её полупрозрачной и ненажимаемой, пока хозяин в превью
                // (BuildSpecFromLevel определяет хозяина обходом родителей — так же, как у Level_07,
                // собранного руками). Хозяин приходит отдельным каналом: ASCII иерархию не выражает.
                string hostId;
                Transform btnParent = (schemeHosts != null && schemeHosts.TryGetValue(new Vector2Int(c, r), out hostId))
                    ? GetOrCreateDisappearGroup(hostId, false).transform
                    : GetGroup("Triggers");
                var go = PlaceFromPrefab(_pfButton, bp, btnParent, "Trigger");
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

        // ⭐ Свойства групп, которых нет в сетке символов: инверсия приезжает отдельным каналом от
        // генератора (см. _mazeStamps). Без этого дверь-инверсия импортировалась бы как обычная
        // группа — то есть проход был бы открыт по умолчанию, и головоломка исчезала.
        {
            var inv = InvertedGroupsFor(text);
            if (inv.Count > 0)
            {
                var disRoot = _root.transform.Find("Disappearing");
                if (disRoot != null) foreach (Transform c in disRoot)
                {
                    var dp = c.GetComponent<DisappearingPlatform>();
                    if (dp == null || !inv.Contains(dp.groupId.ToUpperInvariant())) continue;
                    dp.inverted = true;
                    EditorUtility.SetDirty(dp);
                    Debug.Log($"[LevelEditor] Группа '{dp.groupId}' помечена ИНВЕРСНОЙ (дверь).");
                }
            }
        }

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

        // ⛔ Правила перехода (InReach/PathPossible/StepPossible/TouchPossible) ПЕРЕЕХАЛИ
        // в LevelModel — там их единственный дом. Здесь их держать нельзя: копии правил
        // в этом проекте уже трижды расходились и давали баги.

    /// <summary>Итог разбора схемы. Одна структура на всех потребителей: и лог валидатора, и
    /// самопроверка генератора (см. <see cref="AnalyseScheme"/>).</summary>
    private class SchemeReport
    {
        public bool noSpawn, noFinish;
        public bool tooManyGroups;    // групп больше 12 — точный поиск по состояниям неподъёмен
        public bool finishOk;
        public int artOk, artTotal, cpOk, cpTotal;
        public int reachHolds, totalHolds;
        public int sealedPocket;      // самая большая ЗАМУРОВАННАЯ полость (связная область воздуха)
        /// <summary>Недостижимые холды ВНУТРИ массива (над ними потолок, а не небо). Отделяет
        /// «мертва внешняя крыша» — это нормально и неизбежно — от «замурованы целые комнаты».</summary>
        public int deadInternal;
        /// <summary>Механизмы, которые НИЧЕГО не держат: убери их совсем — все цели по-прежнему
        /// достижимы. Такую группу игрок обойдёт и головоломки не заметит.</summary>
        public System.Collections.Generic.List<string> idleGroups = new System.Collections.Generic.List<string>();
        public System.Collections.Generic.List<string> deadGroups = new System.Collections.Generic.List<string>();
        public System.Collections.Generic.List<string> pressOrder = new System.Collections.Generic.List<string>();
        /// <summary>
        /// ⭐ ИЗМЕРЕННОЕ СЦЕПЛЕНИЕ — сколько механизмов игрок обязан открыть ПО ПОРЯДКУ. Считается не
        /// по замыслу компоновщика, а по факту: B зависит от A, если без A кнопку B нажать нельзя
        /// НИКОГДА. Длина самой длинной цепочки в этом графе зависимостей.
        /// ⚠️ Заведено потому, что план и реальность расходились в ОБЕ стороны (замер 24 схем: 5
        /// расхождений). Замысел — это намерение, а игроку достаётся то, что померила модель.
        /// </summary>
        public int chainDepth;
        /// <summary>
        /// ⭐ Состояния, из которых финиш уже не достать — игрок заперся всерьёз. Появляется только с
        /// ОДНОСТОРОННИМИ механизмами (кнопка лишь с одной стороны): пока у всех кнопки с обеих сторон,
        /// проход всегда можно переоткрыть, и запереться нечем. Поэтому и считается только тогда —
        /// проверка стоит примерно как ещё один поиск.
        /// </summary>
        public int stuckStates;
        /// <summary>
        /// ⭐⭐ ГЛАВНЫЙ КРИТЕРИЙ: существует ОДНО прохождение, в котором собраны все артефакты и
        /// достигнут финиш, с учётом времени окон. Заменяет прежнюю связку «каждый ключ достижим
        /// по отдельности» + «финиш достижим»: на уровне с односторонним потоком та связка врала —
        /// ключ 1 берётся одним маршрутом, ключ 2 другим, а вместе никогда.
        /// </summary>
        public bool playable;

        /// <summary>Брак: то, из-за чего уровень нельзя отдавать игроку.</summary>
        ///
        /// ⚠️ ТУПИКИ БОЛЬШЕ НЕ БРАК (решение игрока 2026-09-04). Застревание — это не поломка, а
        /// место, куда просится выход: кнопка с дверью, мост назад к маршруту или другой паттерн,
        /// который умеет вернуть игрока. У игрока в ручных уровнях именно так и сделано (D и E в
        /// Level_11 — ровно эти выходы). Плюс у игры есть чекпоинты и продолжение, так что застрять
        /// безвозвратно нельзя — можно лишь потерять попытку. Число тупиков остаётся как ПОДСКАЗКА,
        /// куда генератору ставить выход, и как мера того, насколько уровень наказывает за ошибку.
        public bool Bad => noSpawn || tooManyGroups || !playable || cpOk < cpTotal
                        || deadGroups.Count > 0 || idleGroups.Count > 0
                        || deadInternal > DeadInternalLimit
                        || sealedPocket > SealedPocketLimit;
    }

    /// <summary>Замурованный карман крупнее этого — брак. Ноль требовать нельзя: мелкие карманы на
    /// 1-25 холдов есть у большинства лабиринтов (замер по 36 схемам), это складки рельефа. А вот
    /// запечатанная КОМНАТА даёт кратно больше — у пойманного случая (8×8 seed 6) их было 90.</summary>
    private const int SealedPocketLimit = 30;

    /// <summary>
    /// Сколько ВНУТРЕННИХ холдов (с потолком над ними) дозволено оставить недостижимыми.
    /// Порог по замеру 40 сырых схем: медиана 3, основная масса ≤10, дальше редкий хвост 12-20 и
    /// один выброс 48. Игрок прислал скрин с большим замурованным участком — там было 27.
    /// ⚠️ Мерить надо ИМЕННО внутренние: у любого уровня мертва внешняя крыша массива (там их бывает
    /// под полсотни), и общая доля мёртвых холдов ничего не отличает — у того же скрина она была
    /// ровно медианной, 31%.
    /// </summary>
    private const int DeadInternalLimit = 10;

    /// <summary>
    /// Разбор схемы по ИЗМЕРЕННОЙ модели (дотяжка-восьмиугольник + бюджет пути + порядок кнопок).
    /// До 2026-08-18 здесь была коробка ↑4 ↔4 без учёта кнопок: она пропускала подъёмы на 4
    /// клетки, которых в игре нет, и считала платформы групп вечно твёрдыми. Все прежние «0 проблемных»
    /// получены той моделью и доверия не заслуживают.
    /// ⚠️ НИЧЕГО НЕ ЛОГИРУЕТ: генератор гоняет её десятками за одну генерацию.
    /// </summary>
    /// <param name="invertedGroups">Ключи групп, которые ИНВЕРСНЫЕ. Сетка символов этого выразить не
    /// может, поэтому свойство приходит отдельным каналом — от генератора либо из поля импорта.</param>
    /// <param name="buttonHosts">Клетка сетки (столбец, ряд сверху) → группа, ВНУТРИ которой лежит
    /// кнопка. Тоже отдельный канал: вложенность это иерархия, сетка символов её не выражает.
    /// ⚠️ Пропустить его — значит счесть заведомо запертую кнопку вечно доступной и принять уровень,
    /// который на деле не проходится.</param>
    private SchemeReport AnalyseScheme(char[][] grid,
                                       System.Collections.Generic.HashSet<string> invertedGroups = null,
                                       System.Collections.Generic.Dictionary<Vector2Int, string> buttonHosts = null)
    {
        var rep = new SchemeReport();
        if (grid.Length == 0) { rep.noSpawn = true; return rep; }
        int rows = grid.Length, maxCol = 0;
        for (int r = 0; r < rows; r++) if (grid[r].Length > maxCol) maxCol = grid[r].Length;
        int RS = Mathf.RoundToInt(_reachSideCells), RU = Mathf.RoundToInt(_reachUpCells);

        // Схема: строка 0 — верх. Переводим в клетки, где Y растёт ВВЕРХ (как в мире).
        System.Func<int, int> toY = r => rows - 1 - r;
        var rock = new System.Collections.Generic.HashSet<Vector2Int>();
        var groupTiles = new System.Collections.Generic.Dictionary<char, System.Collections.Generic.List<Vector2Int>>();
        var groupButtons = new System.Collections.Generic.Dictionary<char, System.Collections.Generic.List<Vector2Int>>();
        var hostOfButton = new System.Collections.Generic.Dictionary<Vector2Int, string>();
        var arts = new System.Collections.Generic.List<Vector2Int>();
        var checkpoints = new System.Collections.Generic.List<Vector2Int>();
        Vector2Int spawn = new Vector2Int(-9999, -9999), finish = new Vector2Int(-9999, -9999);

        for (int r = 0; r < rows; r++)
        for (int c = 0; c < grid[r].Length; c++)
        {
            char ch = grid[r][c];
            var k = new Vector2Int(c, toY(r));
            if (ch == '#') rock.Add(k);
            else if (ch >= 'a' && ch <= 'z')
            {
                if (!groupTiles.TryGetValue(ch, out var l))
                { l = new System.Collections.Generic.List<Vector2Int>(); groupTiles[ch] = l; }
                l.Add(k);
            }
            else if (ch >= 'A' && ch <= 'Z')
            {
                char g = char.ToLower(ch);
                if (!groupButtons.TryGetValue(g, out var l))
                { l = new System.Collections.Generic.List<Vector2Int>(); groupButtons[g] = l; }
                l.Add(k);
                // Хозяин приходит в КООРДИНАТАХ СЕТКИ (столбец, ряд сверху), а состояния считаются в
                // мировых (Y вверх) — перекладываем сразу, чтобы дальше жила одна система координат.
                string hostId;
                if (buttonHosts != null && buttonHosts.TryGetValue(new Vector2Int(c, r), out hostId))
                    hostOfButton[k] = hostId;
            }
            else if (ch == '@') spawn = k;
            else if (ch == '^') finish = k;
            else if (ch == '*') arts.Add(k);
            else if (ch == '=') checkpoints.Add(k);
        }

        rep.artTotal = arts.Count; rep.cpTotal = checkpoints.Count;
        if (spawn.x < -9000) { rep.noSpawn = true; return rep; }
        if (finish.x < -9000) rep.noFinish = true;

        // ⭐ Схема приводится к тому же LevelSpec, что и настоящий уровень, и считается той же
        // LevelModel. Прежде здесь была ВТОРАЯ копия поиска — для черновиков это стоило времени, для
        // автономного генератора стоило бы непроходимого уровня у игрока на экране.
        // ⚠️ Инверсию и вложенность кнопок сама СХЕМА выразить не может: в ASCII только «a-z тайлы,
        // A-Z кнопки». Здесь все группы обычные и без хозяев — это ограничение ФОРМАТА, не модели.
        var spec = new LevelSpec { cell = _tileCell, spawn = spawn };
        foreach (var k in rock) spec.rock.Add(k);
        foreach (var kv in groupTiles)
        {
            string gid = char.ToUpperInvariant(kv.Key).ToString();
            bool inv = invertedGroups != null && invertedGroups.Contains(gid);
            spec.GetOrAddGroup(kv.Key.ToString(), inv).tiles.AddRange(kv.Value);
        }
        foreach (var kv in groupButtons)
        {
            int gi = spec.IndexOfGroup(kv.Key.ToString());
            if (gi < 0) continue;                       // кнопка без тайлов — такой группы нет
            foreach (var b in kv.Value)
            {
                string hostId;
                int host = LevelButton.NoHost;
                if (hostOfButton.TryGetValue(b, out hostId))
                {
                    host = spec.IndexOfGroup(hostId.ToLowerInvariant());
                    if (host < 0) host = LevelButton.NoHost;         // хозяина в схеме нет — кнопка своя
                }
                spec.groups[gi].buttons.Add(new LevelButton
                { cell = b, center = new Vector2(b.x, b.y), half = Vector2.zero, host = host });
            }
        }
        System.Func<Vector2Int, LevelTarget> mkT = k => new LevelTarget
        { exists = true, cell = k, center = new Vector2(k.x, k.y), half = Vector2.zero,
          world = new Vector3(k.x * spec.cell, k.y * spec.cell, 0f) };
        if (finish.x > -9000) spec.finish = mkT(finish);
        foreach (var a in arts) spec.artifacts.Add(mkT(a));
        foreach (var c in checkpoints) spec.checkpoints.Add(mkT(c));

        var model = new LevelModel(spec, RS, RU) { MoveBudget = _moveBudget };
        model.Search();
        if (model.TooManyGroups) { rep.tooManyGroups = true; return rep; }
        if (model.N == 0) return rep;

        // Цель достижима, если её достаёт ХОТЬ ОДНО посещённое состояние — в своей маске (например,
        // уже после того, как стена убрана).
        System.Func<LevelTarget, bool> canGet = tg =>
        {
            for (int st = 0; st < model.TOTAL; st++)
                if (model.Seen[st] && model.CanTouch(model.GroupMask(st), model.Cells[st % model.N], tg)) return true;
            return false;
        };
        rep.finishOk = !spec.finish.exists || canGet(spec.finish);

        // ⚠️ ЗАПИРАНИЕ ВОЗМОЖНО ТОЛЬКО ПРИ ОДНОСТОРОННИХ МЕХАНИЗМАХ. Если у каждой группы кнопки с
        // обеих сторон, любой проход переоткрывается — запереться нечем, и платить за проверку
        // (примерно ещё один поиск) незачем. Считаем ровно тогда, когда есть чем запереться.
        bool anyOneWay = false;
        foreach (var g0 in spec.groups) if (g0.buttons.Count < 2) { anyOneWay = true; break; }
        if (anyOneWay) rep.stuckStates = model.StuckStates();
        // ⭐ Проходимость целиком: всё собрано И финиш достигнут В ОДНОМ прохождении.
        rep.playable = model.AllKeysAndFinish();
        foreach (var a in spec.artifacts) if (canGet(a)) rep.artOk++;
        foreach (var c in spec.checkpoints) if (canGet(c)) rep.cpOk++;
        rep.deadGroups.AddRange(model.DeadGroups());

        // ⭐ КРИТЕРИЙ «КАЖДЫЙ МЕХАНИЗМ НЕСУЩИЙ»: убираем группу совсем и смотрим, пропала ли хоть одна
        // цель. Не пропала — механизм декоративный, игрок его обойдёт и головоломки не заметит.
        // Именно это отличает «уровень спроектировали» от «на уровень насыпали».
        // ⚠️ Считается ОТДЕЛЬНЫМ поиском на каждую группу — отсюда и цена проверки.
        {
            var targets = new System.Collections.Generic.List<LevelTarget>();
            if (spec.finish.exists) targets.Add(spec.finish);
            targets.AddRange(spec.artifacts);
            targets.AddRange(spec.checkpoints);
            var reachableNow = new bool[targets.Count];
            for (int i = 0; i < targets.Count; i++) reachableNow[i] = canGet(targets[i]);

            // Граф зависимостей: dep[b, a] = «без механизма a кнопку b не нажать НИКОГДА». Считается
            // из ТЕХ ЖЕ поисков, что и «несущий механизм», — лишней цены нет.
            int GN = spec.groups.Count;
            var dep = new bool[GN, GN];

            for (int gi = 0; gi < spec.groups.Count; gi++)
            {
                var variant = spec.WithoutGroup(gi);
                var m2 = new LevelModel(variant, RS, RU) { MoveBudget = _moveBudget };
                m2.Search();
                if (m2.TooManyGroups || m2.N == 0) continue;
                var deadWithout = m2.DeadGroups();
                for (int b = 0; b < GN; b++)
                    if (b != gi && deadWithout.Contains(spec.groups[b].id.ToUpperInvariant())) dep[b, gi] = true;
                bool somethingLost = false;
                for (int i = 0; i < targets.Count && !somethingLost; i++)
                {
                    if (!reachableNow[i]) continue;                 // и так было недостижимо — не в счёт
                    bool still = false;
                    for (int st = 0; st < m2.TOTAL && !still; st++)
                        if (m2.Seen[st] && m2.CanTouch(m2.GroupMask(st), m2.Cells[st % m2.N], targets[i])) still = true;
                    if (!still) somethingLost = true;
                }
                // ⭐⭐ ЗАПИРАНИЕ — ТОЖЕ ПОТЕРЯ. Дверь-выход, открываемая только изнутри, по критерию
                // «пропала ли цель» выглядит декоративной: убери её — ключ всё равно достижим
                // (в камеру-то провалился), просто наружу не выйти. Это и есть её работа, поэтому
                // «стало можно запереться» засчитываем как потерю. Разбор ручного Level_07.
                //
                // 🐞 Правило стояло за двумя лишними условиями и почти никогда не срабатывало:
                //   • `rep.stuckStates == 0` — то есть оно работало ТОЛЬКО на уровнях, где тупиков и
                //     так нет. У любого уровня с камерой их 8-80, и до правила не доходило вовсе;
                //   • `buttons.Count < 2` — «у двусторонних запереться нечем». Для СТЕНЫ это неверно:
                //     кнопки с обеих сторон не помогают, если ты не на той стороне.
                // Правильное сравнение — не «были ли тупики», а «стало ли их БОЛЬШЕ без этой группы».
                // Так критерий начинает видеть стены, ради которых всё и затевалось.
                if (!somethingLost && m2.StuckStates() > rep.stuckStates) somethingLost = true;
                if (!somethingLost) rep.idleGroups.Add(spec.groups[gi].id.ToUpperInvariant());
            }

            // Длиннейший путь в графе зависимостей = сцепление. Мемоизация со «страховочной» единицей
            // в ячейке до расчёта: цикла в зависимостях быть не должно, но зациклиться на нём нельзя.
            var memo = new int[GN];
            for (int i = 0; i < GN; i++) memo[i] = -1;
            System.Func<int, int> longestFrom = null;
            longestFrom = idx =>
            {
                if (memo[idx] >= 0) return memo[idx];
                memo[idx] = 1;
                int best = 1;
                for (int a = 0; a < GN; a++)
                    if (dep[idx, a]) { int v = 1 + longestFrom(a); if (v > best) best = v; }
                memo[idx] = best; return best;
            };
            for (int i = 0; i < GN; i++) { int v = longestFrom(i); if (v > rep.chainDepth) rep.chainDepth = v; }
        }

        // Порядок кнопок: по тому, на каком ходу группа впервые оказалась включена.
        {
            var firstDepth = new int[model.G];
            for (int g = 0; g < model.G; g++) firstDepth[g] = int.MaxValue;
            for (int st = 0; st < model.TOTAL; st++)
            {
                if (!model.Seen[st]) continue;
                int m = model.GroupMask(st);
                for (int g = 0; g < model.G; g++)
                    if ((m & (1 << g)) != 0 && model.Depth[st] < firstDepth[g]) firstDepth[g] = model.Depth[st];
            }
            var order = new System.Collections.Generic.List<int>();
            for (int g = 0; g < model.G; g++) if (firstDepth[g] != int.MaxValue) order.Add(g);
            order.Sort((x, y) => firstDepth[x].CompareTo(firstDepth[y]));
            foreach (int g in order) rep.pressOrder.Add(spec.groups[g].id.ToUpperInvariant());
        }

        // Холды и замурованные зоны — по ОБЪЕДИНЕНИЮ всех групп: клетка холд, если она твёрдая и над
        // ней пусто хоть при какой-то маске (так же, как оверлей достижимости в ComputeRoute).
        var union = new System.Collections.Generic.HashSet<Vector2Int>(rock);
        foreach (var g in spec.groups) foreach (var k in g.tiles) union.Add(k);
        var allHolds = new System.Collections.Generic.List<Vector2Int>();
        foreach (var k in union) if (!union.Contains(new Vector2Int(k.x, k.y + 1))) allHolds.Add(k);
        var reachedCells = new System.Collections.Generic.HashSet<Vector2Int>();
        foreach (int ci in model.ReachedCellIndices()) reachedCells.Add(model.Cells[ci]);
        int reachCount = 0;
        foreach (var k in allHolds) if (reachedCells.Contains(k)) reachCount++;
        rep.reachHolds = reachCount; rep.totalHolds = allHolds.Count;
        rep.sealedPocket = LargestSealedAirPocket(union, allHolds, reachedCells);
        {
            // ⭐ «ВНУТРИ МАССИВА» = НАД ХОЛДОМ ЗАМКНУТАЯ ПОЛОСТЬ, А НЕ ПРОСТО КАМЕНЬ ВЫШЕ ПО КОЛОНКЕ.
            // 🐞 Прежний критерий («есть камень где-то выше в этой колонке») врал на СКЛАДКАХ СИЛУЭТА:
            // холд на внешней поверхности скалы открыт небу сбоку, но над ним нависает выступ соседней
            // скалы — и он считался замурованным. Поймано при переносе обрезки оболочки на свободный
            // путь: достижимых холдов ровно столько же, а «замуровано» подскочило с 0 до 8-12 и дало
            // ложный брак на 7 схемах из 16.
            // Теперь холд внутренний, только если полость над ним НЕ ВЫХОДИТ наружу — то есть её
            // область воздуха не касается края сетки.
            int minX2 = int.MaxValue, maxX2 = int.MinValue, minY2 = int.MaxValue, maxY2 = int.MinValue;
            foreach (var k in union)
            {
                minX2 = Mathf.Min(minX2, k.x); maxX2 = Mathf.Max(maxX2, k.x);
                minY2 = Mathf.Min(minY2, k.y); maxY2 = Mathf.Max(maxY2, k.y);
            }
            var regionOf = new System.Collections.Generic.Dictionary<Vector2Int, int>();
            var openRegion = new System.Collections.Generic.List<bool>();
            for (int y = minY2; y <= maxY2; y++)
            for (int x = minX2; x <= maxX2; x++)
            {
                var start = new Vector2Int(x, y);
                if (union.Contains(start) || regionOf.ContainsKey(start)) continue;
                int id = openRegion.Count; bool touchesEdge = false;
                var q2 = new System.Collections.Generic.Queue<Vector2Int>();
                regionOf[start] = id; q2.Enqueue(start);
                while (q2.Count > 0)
                {
                    var c2 = q2.Dequeue();
                    if (c2.x <= minX2 || c2.x >= maxX2 || c2.y <= minY2 || c2.y >= maxY2) touchesEdge = true;
                    for (int d = 0; d < 4; d++)
                    {
                        var nb = new Vector2Int(c2.x + (d == 2 ? -1 : d == 3 ? 1 : 0),
                                                c2.y + (d == 0 ? -1 : d == 1 ? 1 : 0));
                        if (nb.x < minX2 || nb.x > maxX2 || nb.y < minY2 || nb.y > maxY2) { touchesEdge = true; continue; }
                        if (union.Contains(nb) || regionOf.ContainsKey(nb)) continue;
                        regionOf[nb] = id; q2.Enqueue(nb);
                    }
                }
                openRegion.Add(touchesEdge);
            }
            foreach (var k in allHolds)
            {
                if (reachedCells.Contains(k)) continue;
                int rid;
                if (regionOf.TryGetValue(new Vector2Int(k.x, k.y + 1), out rid) && !openRegion[rid])
                    rep.deadInternal++;
            }
        }
        return rep;
    }

    /// <summary>
    /// Самый большой ЗАМУРОВАННЫЙ карман: связная группа недостижимых холдов ВНУТРИ массива.
    /// «Внутри» = выше по колонке есть камень, то есть над холдом потолок, а не небо — иначе в счёт
    /// попадала бы внешняя крыша лабиринта, которая недостижима по построению и никому не мешает.
    /// Ловит случай, ради которого и заведена: ступенька под люком встала во всю ширину комнаты и
    /// запечатала её пол вместе с мостом и кнопками (8×8 seed 6, 12×3 seed 13).
    /// </summary>
    /// <summary>
    /// ⭐ ЗАМУРОВАННАЯ ЗОНА = СВЯЗНАЯ ОБЛАСТЬ ВОЗДУХА, в которую модель ни разу не ступила.
    /// Размер зоны — сколько в ней холдов (полок, на которых игрок мог бы стоять).
    ///
    /// 🐞 Прежняя версия считала иначе и ПРОПУСКАЛА большие мёртвые куски: она слипала недостижимые
    /// холды по соседству в ±2 клетки, и вертикальная шахта с полками через 3-4 ряда разваливалась на
    /// мелкие кусочки. Игрок прислал скрин 7×6 сид 10: недостижимых холдов 75 из 244, целая пятая
    /// часть уровня отрезана, — а «карман» показывал 12 при пороге 30.
    /// Область воздуха такие полки объединяет правильно: они смотрят в одну и ту же полость.
    ///
    /// Наружное небо в счёт не идёт: области, касающиеся края сетки, пропускаем.
    /// </summary>
    private static int LargestSealedAirPocket(System.Collections.Generic.HashSet<Vector2Int> solid,
                                              System.Collections.Generic.List<Vector2Int> holds,
                                              System.Collections.Generic.HashSet<Vector2Int> reach)
    {
        int minX = int.MaxValue, maxX = int.MinValue, minY = int.MaxValue, maxY = int.MinValue;
        foreach (var k in solid)
        {
            if (k.x < minX) minX = k.x; if (k.x > maxX) maxX = k.x;
            if (k.y < minY) minY = k.y; if (k.y > maxY) maxY = k.y;
        }
        if (minX > maxX) return 0;

        // Клетка стояния достижимого холда = холд + 1: по ним узнаём, была ли модель в этой полости.
        var visitedAir = new System.Collections.Generic.HashSet<Vector2Int>();
        foreach (var h in reach) visitedAir.Add(new Vector2Int(h.x, h.y + 1));
        // Холды по клетке стояния — чтобы мерить зону в полках, а не в пустых клетках.
        var holdByAir = new System.Collections.Generic.Dictionary<Vector2Int, int>();
        foreach (var h in holds)
        {
            var air = new Vector2Int(h.x, h.y + 1);
            holdByAir[air] = holdByAir.ContainsKey(air) ? holdByAir[air] + 1 : 1;
        }

        var seen = new System.Collections.Generic.HashSet<Vector2Int>();
        int worst = 0;
        for (int x = minX; x <= maxX; x++)
        for (int y = minY; y <= maxY + 1; y++)
        {
            var start = new Vector2Int(x, y);
            if (solid.Contains(start) || seen.Contains(start)) continue;
            var comp = new System.Collections.Generic.List<Vector2Int>();
            var q = new System.Collections.Generic.Queue<Vector2Int>();
            q.Enqueue(start); seen.Add(start);
            bool touchesOutside = false, touchedByModel = false;
            while (q.Count > 0)
            {
                var c = q.Dequeue(); comp.Add(c);
                if (c.x <= minX || c.x >= maxX || c.y <= minY || c.y >= maxY + 1) touchesOutside = true;
                if (visitedAir.Contains(c)) touchedByModel = true;
                for (int k = 0; k < 4; k++)
                {
                    var nb = new Vector2Int(c.x + (k == 2 ? -1 : k == 3 ? 1 : 0),
                                            c.y + (k == 0 ? -1 : k == 1 ? 1 : 0));
                    if (nb.x < minX || nb.x > maxX || nb.y < minY || nb.y > maxY + 1) { touchesOutside = true; continue; }
                    if (solid.Contains(nb) || !seen.Add(nb)) continue;
                    q.Enqueue(nb);
                }
            }
            if (touchesOutside || touchedByModel) continue;      // небо снаружи либо модель тут была
            int size = 0;
            foreach (var c in comp) { int n; if (holdByAir.TryGetValue(c, out n)) size += n; }
            if (size > worst) worst = size;
        }
        return worst;
    }

    private static int LargestSealedPocketOld(System.Collections.Generic.HashSet<Vector2Int> solid,
                                              System.Collections.Generic.List<Vector2Int> holds,
                                              System.Collections.Generic.HashSet<Vector2Int> reach)
    {
        int maxY = 0;
        foreach (var k in solid) if (k.y > maxY) maxY = k.y;
        var inner = new System.Collections.Generic.HashSet<Vector2Int>();
        foreach (var h in holds)
        {
            if (reach.Contains(h)) continue;
            for (int y = h.y + 2; y <= maxY; y++)
                if (solid.Contains(new Vector2Int(h.x, y))) { inner.Add(h); break; }
        }
        int best = 0;
        var seen = new System.Collections.Generic.HashSet<Vector2Int>();
        foreach (var start in inner)
        {
            if (!seen.Add(start)) continue;
            int size = 0;
            var q = new System.Collections.Generic.Queue<Vector2Int>();
            q.Enqueue(start);
            while (q.Count > 0)
            {
                var c = q.Dequeue(); size++;
                for (int dx = -2; dx <= 2; dx++)
                for (int dy = -2; dy <= 2; dy++)
                {
                    var nb = new Vector2Int(c.x + dx, c.y + dy);
                    if (inner.Contains(nb) && seen.Add(nb)) q.Enqueue(nb);
                }
            }
            if (size > best) best = size;
        }
        return best;
    }

    /// <summary>Лог-обёртка над <see cref="AnalyseScheme"/> — то, что видит игрок в консоли.</summary>
    private void CheckSchemeReachability(char[][] grid)
    {
        var rep = AnalyseScheme(grid, InvertedGroupsFor(_schemeText), ButtonHostsFor(_schemeText));
        if (rep.noSpawn) { Debug.LogWarning("[Reach] ⚠ В схеме НЕТ спавна '@' — проверять нечего."); return; }
        if (rep.tooManyGroups)
        { Debug.LogWarning("[Reach] ⚠ Слишком много групп для точного поиска по состояниям."); return; }
        if (rep.noFinish) Debug.LogWarning("[Reach] ⚠ В схеме НЕТ финиша '^'.");
        int RS = Mathf.RoundToInt(_reachSideCells), RU = Mathf.RoundToInt(_reachUpCells);
        string msg = $"[Reach] Холдов достижимо: {rep.reachHolds}/{rep.totalHolds}. "
            + $"Финиш={(rep.finishOk ? "ok" : "НЕДОСТ.")}, артефакты {rep.artOk}/{rep.artTotal}, "
            + $"чекпоинты {rep.cpOk}/{rep.cpTotal}. Дотяжка ↔{RS} ↑{RU} (сумма ≤{ReachSumCells})"
            + (rep.pressOrder.Count > 0 ? ", кнопки: " + string.Join("→", rep.pressOrder.ToArray()) : "")
            // Сцепление показываем и для РУЧНЫХ уровней: это единственная объективная мерка того,
            // головоломка перед нами или набор независимых кнопок.
            + $", сцепление {rep.chainDepth}";
        if (rep.Bad) Debug.LogWarning(msg); else Debug.Log(msg);
        if (!rep.finishOk) Debug.LogWarning("[Reach] ⚠ ФИНИШ недостижим от спавна.");
        if (rep.artOk < rep.artTotal) Debug.LogWarning($"[Reach] ⚠ {rep.artTotal - rep.artOk} артефакт(ов) недостижимы.");
        if (rep.cpOk < rep.cpTotal) Debug.LogWarning($"[Reach] ⚠ {rep.cpTotal - rep.cpOk} чекпоинт(ов) недостижимы.");
        if (rep.deadGroups.Count > 0)
            Debug.LogWarning("[Reach] ⚠ Группы, чьи кнопки недостижимы (платформы мертвы): "
                + string.Join(",", rep.deadGroups.ToArray()));
        if (rep.idleGroups.Count > 0)
            Debug.LogWarning("[Reach] ⚠ Механизмы НИЧЕГО не держат (их можно обойти): "
                + string.Join(",", rep.idleGroups.ToArray()));
        if (rep.deadInternal > DeadInternalLimit)
            Debug.LogWarning($"[Reach] ⚠ Замуровано {rep.deadInternal} холдов ВНУТРИ массива "
                + "(порог " + DeadInternalLimit + ") — большой кусок уровня отрезан.");
        if (rep.sealedPocket > SealedPocketLimit)
            Debug.LogWarning($"[Reach] ⚠ Замурованная зона: {rep.sealedPocket} холдов внутри массива, "
                + "куда не попасть (порог " + SealedPocketLimit + ").");
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
        ComputeRoute();   // маршрут модели сразу, без лишнего клика (считается за ~40 мс)
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

        ClearLoadedLevel();   // сохранён в префаб — в сцене больше не нужен

        EditorUtility.DisplayDialog("Сохранено!", $"Уровень сохранён:\n{path}", "OK");
        Debug.Log($"[LevelEditor] Сохранён и выгружен из сцены: {path}");
    }

    /// <summary>
    /// Убрать уровень со сцены и вернуть окно в исходное состояние. Общий хвост для «Save» и
    /// «Unload»: сохранение тоже выгружает уровень, и раньше эти шаги были только внутри SaveLevel.
    /// </summary>
    private void ClearLoadedLevel()
    {
        if (_root != null) Object.DestroyImmediate(_root);
        _root             = null;
        _loadedPrefabPath = null;
        _tool             = Tool.Select;
        AutoLevelName();
        // Маршрут модели считается для загруженного уровня — держать его после выгрузки незачем.
        _routePath = _routeReach = _routeDead = _routePress = _routeLostKeys = _routeBranchPress = null;
        _routeBranch = null;
        _routeInfo = "";
        EditorSceneManager.MarkSceneDirty(
            UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        SceneView.RepaintAll();
        Repaint();
    }

    /// <summary>
    /// Выгрузить уровень со сцены. До этого выгрузить его можно было ТОЛЬКО сохранением (запрос
    /// игрока 2026-09-01) — то есть чтобы просто убрать уровень с глаз, приходилось записывать
    /// префаб. Спрашиваем, сохранять ли: потерять правки молча тут слишком легко.
    /// </summary>
    private void UnloadLevel()
    {
        if (_root == null) return;
        int choice = EditorUtility.DisplayDialogComplex("Выгрузить уровень",
            $"Уровень '{_root.name}' будет убран со сцены.",
            "Сохранить и выгрузить", "Отмена", "Выгрузить без сохранения");
        if (choice == 1) return;                    // Отмена
        if (choice == 0) { SaveLevel(); return; }   // SaveLevel выгружает сам
        string name = _root.name;
        ClearLoadedLevel();
        Debug.Log($"[LevelEditor] Уровень '{name}' выгружен из сцены БЕЗ сохранения.");
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
    /// <summary>
    /// Цвет группы триггеров по её ключу. Оттенок разносим ЗОЛОТЫМ СЕЧЕНИЕМ (0.618) — при таком шаге
    /// соседние по алфавиту группы получают максимально далёкие цвета, и 26 групп a-z не сливаются
    /// (равномерный шаг hue += 1/26 дал бы почти одинаковые соседние оттенки).
    /// </summary>
    /// ⚠️ Сама формула переехала в рантайм (<see cref="GroupPalette"/>): той же краской красится
    /// КНОПКА в игре, а держать два экземпляра одного правила нельзя — разъедутся.
    private static Color GroupColor(string groupId) => GroupPalette.For(groupId);

    private static GUIStyle _groupLabelStyle;   // кэш: OnSceneGUI зовётся каждый repaint, не аллоцируем

    /// <summary>
    /// Подсветка групп исчезающих платформ прямо в сцене: каждая группа — свой цвет, тайлы залиты
    /// квадратом, кнопки обведены кругом, у всех подпись с ключом группы. Без этого при 5+ группах
    /// в редакторе не видно, где чьи тайлы (фидбэк игрока 2026-08-18) — обычные тайлы и триггерные
    /// выглядят одинаково. Рисуем ТОЛЬКО оверлеем (Handles), сами объекты НЕ трогаем: цвет спрайтов
    /// принадлежит рантайму (DisappearingPlatform красит их сам через previewAlpha).
    /// </summary>
    private void DrawGroupColors()
    {
        if (!_showGroupColors || _root == null) return;
        if (Event.current.type != EventType.Repaint) return;   // не тратим на layout/mouse-события

        float half = _tileCell * 0.5f;
        if (_groupLabelStyle == null) _groupLabelStyle = new GUIStyle(EditorStyles.boldLabel);
        var labelStyle = _groupLabelStyle;

        // Окно активации каждой группы — чтобы подписать его под кнопкой. Значение живёт в
        // DisappearingPlatform (импортёр считает его от длины пути, см. ApplyTriggerWindows),
        // а кнопка знает только groupId — связываем по ключу.
        var windows = new System.Collections.Generic.Dictionary<string, float>();

        // ── Тайлы групп: контейнеры Disappear_X под "Disappearing" ──
        var dis = _root.transform.Find("Disappearing");
        if (dis != null)
            foreach (Transform cont in dis)
            {
                var dp = cont.GetComponent<DisappearingPlatform>();
                if (dp == null) continue;
                windows[dp.groupId] = dp.activeWindow;
                Color col = GroupColor(dp.groupId);
                labelStyle.normal.textColor = col;

                foreach (Transform tile in cont)
                {
                    Vector3 p = tile.position; p.z = 0f;
                    var quad = new Vector3[]
                    {
                        p + new Vector3(-half, -half), p + new Vector3(-half, half),
                        p + new Vector3( half,  half), p + new Vector3( half, -half)
                    };
                    Handles.DrawSolidRectangleWithOutline(quad,
                        new Color(col.r, col.g, col.b, 0.30f), new Color(col.r, col.g, col.b, 0.95f));
                }
                // Подпись — один раз на группу, у первого тайла (иначе каша из букв на каждой клетке).
                if (cont.childCount > 0)
                {
                    Vector3 lp = cont.GetChild(0).position; lp.z = 0f;
                    Handles.Label(lp + Vector3.up * (half + 0.12f), dp.groupId, labelStyle);
                }
            }

        // ── Кнопки: под "Triggers", цвет по TriggerTile.groupId ──
        var trig = _root.transform.Find("Triggers");
        if (trig != null)
            foreach (Transform btn in trig)
            {
                var tt = btn.GetComponentInChildren<TriggerTile>(true);
                if (tt == null) continue;
                Color col = GroupColor(tt.groupId);
                labelStyle.normal.textColor = col;
                Vector3 p = btn.position; p.z = 0f;

                Handles.color = new Color(col.r, col.g, col.b, 0.95f);
                Handles.DrawWireDisc(p, Vector3.forward, half * 1.15f);
                Handles.DrawWireDisc(p, Vector3.forward, half * 0.75f);
                Handles.Label(p + Vector3.up * (half + 0.12f), tt.groupId, labelStyle);

                // Окно активации ПОД кнопкой. «—» = у группы нет тайлов (кнопка висит впустую,
                // сразу видно опечатку в groupId).
                string sec = windows.TryGetValue(tt.groupId, out float w)
                    ? w.ToString("0.#") + " с" : "—";
                Handles.Label(p + Vector3.down * (half + 0.34f) + Vector3.left * half * 0.6f, sec, labelStyle);
            }

        Handles.color = Color.white;
    }

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
