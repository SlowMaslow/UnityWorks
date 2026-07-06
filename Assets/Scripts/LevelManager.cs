using UnityEngine;
using System;
using System.Collections.Generic;

/// <summary>
/// Управляет логикой уровня: загрузка prefab, таймер, артефакты, звёзды-по-задачам, завершение.
/// Присутствует только в GameScene на объекте GameManager.
/// Звёзды больше НЕ пикапы: считаются на финале по data-driven задачам LevelConfig
/// (прошёл / собрал все артефакты / уложился во время). Артефакты — собираемые крюки на уровне.
/// </summary>
public class LevelManager : MonoBehaviour
{
    // ─── Singleton ───────────────────────────────────────────────────────────
    public static LevelManager Instance { get; private set; }

    // ─── Inspector ───────────────────────────────────────────────────────────
    [Header("Пустой GameObject куда инстанциируется уровень")]
    [SerializeField] private Transform levelContainer;

    [Header("Спавн игрока")]
    [Tooltip("Z-плоскость игрока при спавне (геймплейная плоскость). X/Y берутся из SpawnPoint уровня.")]
    [SerializeField] private float playerSpawnZ = -0.05f;

    // ─── Состояние уровня ────────────────────────────────────────────────────
    public float ElapsedTime        { get; private set; }
    public int   ArtifactsCollected { get; private set; }
    public int   ArtifactsTotal     { get; private set; }
    public int   CoinsThisRun       { get; private set; }
    public bool  IsRunning          { get; private set; }

    private int         _currentLevelIndex;
    private LevelConfig _config;

    // Чекпоинт для оживления (continue): позиция спавна. Дефолт = старт-спавн; флажки её переставляют.
    private Vector3        _checkpointPos;
    private bool           _checkpointSet;
    private ClimbController _playerClimb;

    /// <summary>Индекс текущего загруженного уровня (для аналитики/рекламы).</summary>
    public int CurrentLevelIndex => _currentLevelIndex;

    /// <summary>Конфиг текущего уровня (задачи под звёзды). Может быть null (тогда fallback).</summary>
    public LevelConfig Config => _config;

    /// <summary>Позиция оживления (continue): самый высокий ПРОЙДЕННЫЙ чекпоинт, иначе старт-спавн.</summary>
    public Vector3 ReviveSpawnPosition
    {
        get
        {
            if (_checkpointSet) return _checkpointPos;
            return _playerClimb != null ? _playerClimb.BodyPosition : Vector3.zero;
        }
    }

    /// <summary>Активировать чекпоинт (вызывает CheckpointFlag при касании пэдом). Z — геймплейный.</summary>
    public void SetCheckpoint(Vector3 worldPos)
    {
        _checkpointPos = new Vector3(worldPos.x, worldPos.y, playerSpawnZ);
        _checkpointSet = true;
    }

    // ─── События ─────────────────────────────────────────────────────────────
    public static event Action<float>       OnTimerTick;
    /// <summary>Артефакт собран в забеге. Передаёт (собрано, всего) — для HUD/сайдбара/звука.</summary>
    public static event Action<int, int>    OnArtifactCollected;
    public static event Action<LevelResult> OnLevelCompleted;
    /// <summary>Монета собрана — передаёт текущий баланс для отображения в UI (сохранённые + за этот запуск).</summary>
    public static event Action<int>         OnCoinCollected;

    // ─── Unity ───────────────────────────────────────────────────────────────
    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance            = null;
            OnTimerTick         = null;
            OnArtifactCollected = null;
            OnLevelCompleted    = null;
            OnCoinCollected     = null;
        }
    }

    private void Start()
    {
        _currentLevelIndex = LevelLoader.PendingLevel;
        var levelGO = LoadLevelPrefab(_currentLevelIndex);
        ReadLevelConfig(levelGO);
        PositionPlayerAtSpawn(levelGO);
        StartLevel();
    }

    /// <summary>Читает LevelConfig и считает артефакты в загруженном уровне.</summary>
    private void ReadLevelConfig(GameObject levelGO)
    {
        _config        = levelGO != null ? levelGO.GetComponentInChildren<LevelConfig>(true) : null;
        ArtifactsTotal = levelGO != null ? levelGO.GetComponentsInChildren<Artifact>(true).Length : 0;
    }

    /// <summary>Ставит единственного игрока сцены на SpawnPoint загруженного уровня.</summary>
    private void PositionPlayerAtSpawn(GameObject levelGO)
    {
        if (levelGO == null) return;

        Transform spawn = null;
        foreach (var t in levelGO.GetComponentsInChildren<Transform>(true))
            if (t.name == "SpawnPoint") { spawn = t; break; }
        if (spawn == null)
        {
            Debug.LogWarning("[LevelManager] SpawnPoint не найден в уровне — игрок не перемещён.");
            return;
        }

        var playerGO = GameObject.FindWithTag("Player");
        if (playerGO == null)
        {
            Debug.LogWarning("[LevelManager] Объект с тегом Player не найден в сцене.");
            return;
        }

        Vector3 pos = new Vector3(spawn.position.x, spawn.position.y, playerSpawnZ);
        _playerClimb = playerGO.GetComponent<ClimbController>();
        _checkpointPos = pos; _checkpointSet = true; // дефолтный чекпоинт = старт-спавн
        if (_playerClimb != null) _playerClimb.MoveTo(pos);
        else playerGO.transform.position = pos;
    }

    private void Update()
    {
        if (!IsRunning) return;
        if (GameManager.Instance == null || GameManager.Instance.State != GameState.Playing) return;

        ElapsedTime += Time.deltaTime;
        OnTimerTick?.Invoke(ElapsedTime);
    }

    // ─── Prefab loading ──────────────────────────────────────────────────────
    private GameObject LoadLevelPrefab(int index)
    {
        var prefabName = LevelLoader.PrefabName(index);
        var prefab     = Resources.Load<GameObject>($"{LevelLoader.ResourcesPath}/{prefabName}");

        if (prefab == null)
        {
            Debug.LogError($"[LevelManager] Prefab не найден: Resources/{LevelLoader.ResourcesPath}/{prefabName}");
            return null;
        }

        var parent   = levelContainer != null ? levelContainer : transform;
        var instance = Instantiate(prefab, parent.position, parent.rotation, parent);
        Debug.Log($"[LevelManager] Загружен уровень {index} ({prefabName})");
        return instance;
    }

    // ─── API ─────────────────────────────────────────────────────────────────
    public void StartLevel()
    {
        ElapsedTime        = 0f;
        ArtifactsCollected = 0;
        CoinsThisRun       = 0;
        IsRunning          = true;

        AnalyticsManager.Instance?.LevelStart(_currentLevelIndex);
    }

    public void RegisterCoin()
    {
        CoinsThisRun++;
        OnCoinCollected?.Invoke(SaveSystem.Coins + CoinsThisRun);
    }

    /// <summary>Вызывается Artifact при касании. Не банкуется здесь — банк только при прохождении.</summary>
    public void RegisterArtifact()
    {
        ArtifactsCollected++;
        OnArtifactCollected?.Invoke(ArtifactsCollected, ArtifactsTotal);
    }

    public void CompleteLevel()
    {
        if (!IsRunning) return;
        IsRunning = false;

        int total   = LevelLoader.TotalLevels;
        int runMask = EvaluateTaskMask();

        // Только здесь сохраняем монеты — исключает фарм без прохождения
        SaveSystem.Coins += CoinsThisRun;
        GameManager.Instance?.NotifyCoinsChanged();

        // Мержим маску задач в рекорд; oldMask нужен для подсветки ВПЕРВЫЕ выполненных задач
        int oldMask  = SaveSystem.MergeLevelTaskMask(_currentLevelIndex, runMask);
        int bestMask = oldMask | runMask;

        SaveSystem.TrySetLevelBestTime(_currentLevelIndex, ElapsedTime);
        // Артефакты банкуются в картинку-мир ТОЛЬКО при прохождении (best-kept)
        SaveSystem.TrySetLevelArtifacts(_currentLevelIndex, ArtifactsCollected);
        SaveSystem.UnlockNextLevel(_currentLevelIndex, total);

        var result = new LevelResult
        {
            levelIndex     = _currentLevelIndex,
            stars          = SaveSystem.CountBits(bestMask),   // РЕКОРД (финалка не регрессирует)
            starsThisRun   = SaveSystem.CountBits(runMask),
            taskMask       = bestMask,
            newTasksMask   = runMask & ~oldMask,               // впервые выполнено в этом заходе
            time           = ElapsedTime,
            coinsCollected = CoinsThisRun
        };

        GameManager.Instance?.SetState(GameState.Win);
        AnalyticsManager.Instance?.LevelComplete(result);
        OnLevelCompleted?.Invoke(result);
    }

    /// <summary>
    /// Битовая маска выполненных в ЭТОМ забеге задач (бит = 1 &lt;&lt; (int)StarTaskType).
    /// Нет конфига → только бит Complete (fallback, чтобы старые уровни не давали 0).
    /// </summary>
    private int EvaluateTaskMask()
    {
        if (_config == null || _config.TaskCount == 0)
            return 1 << (int)LevelConfig.StarTaskType.Complete;

        int mask = 0;
        foreach (var task in _config.Tasks)
        {
            bool done = false;
            switch (task.type)
            {
                case LevelConfig.StarTaskType.Complete:
                    done = true; // дошли до финала
                    break;
                case LevelConfig.StarTaskType.CollectAllArtifacts:
                    done = ArtifactsTotal > 0 && ArtifactsCollected >= ArtifactsTotal;
                    break;
                case LevelConfig.StarTaskType.BeatTime:
                    done = ElapsedTime <= task.timeThreshold;
                    break;
            }
            if (done) mask |= 1 << (int)task.type;
        }
        return mask;
    }
}
