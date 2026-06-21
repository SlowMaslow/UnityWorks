using UnityEngine;
using System;

/// <summary>
/// Управляет логикой уровня: загрузка prefab, таймер, звёзды, завершение.
/// Присутствует только в GameScene на объекте GameManager.
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
    public float ElapsedTime    { get; private set; }
    public int   StarsCollected { get; private set; }
    public int   CoinsThisRun   { get; private set; }
    public bool  IsRunning      { get; private set; }

    private int _currentLevelIndex;

    /// <summary>Индекс текущего загруженного уровня (для аналитики/рекламы).</summary>
    public int CurrentLevelIndex => _currentLevelIndex;

    // ─── События ─────────────────────────────────────────────────────────────
    public static event Action<float>       OnTimerTick;
    public static event Action<int>         OnStarCollected;
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
            Instance        = null;
            OnTimerTick     = null;
            OnStarCollected = null;
            OnLevelCompleted = null;
        }
    }

    private void Start()
    {
        _currentLevelIndex = LevelLoader.PendingLevel;
        var levelGO = LoadLevelPrefab(_currentLevelIndex);
        PositionPlayerAtSpawn(levelGO);
        StartLevel();
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
        var climb = playerGO.GetComponent<ClimbController>();
        if (climb != null) climb.MoveTo(pos);
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
        ElapsedTime    = 0f;
        StarsCollected = 0;
        CoinsThisRun   = 0;
        IsRunning      = true;

        AnalyticsManager.Instance?.LevelStart(_currentLevelIndex);
    }

    public void RegisterCoin()
    {
        CoinsThisRun++;
        OnCoinCollected?.Invoke(SaveSystem.Coins + CoinsThisRun);
    }

    public void RegisterStar()
    {
        StarsCollected = Mathf.Min(StarsCollected + 1, 3);
        OnStarCollected?.Invoke(StarsCollected);
    }

    public void CompleteLevel()
    {
        if (!IsRunning) return;
        IsRunning = false;

        int total = LevelLoader.TotalLevels;

        // Только здесь сохраняем монеты — исключает фарм без прохождения
        SaveSystem.Coins += CoinsThisRun;
        GameManager.Instance?.NotifyCoinsChanged();

        SaveSystem.TrySetLevelStars(_currentLevelIndex, StarsCollected);
        SaveSystem.TrySetLevelBestTime(_currentLevelIndex, ElapsedTime);
        SaveSystem.UnlockNextLevel(_currentLevelIndex, total);

        var result = new LevelResult
        {
            levelIndex     = _currentLevelIndex,
            stars          = StarsCollected,
            time           = ElapsedTime,
            coinsCollected = CoinsThisRun
        };

        GameManager.Instance?.SetState(GameState.Win);
        AnalyticsManager.Instance?.LevelComplete(result);
        OnLevelCompleted?.Invoke(result);
    }
}
