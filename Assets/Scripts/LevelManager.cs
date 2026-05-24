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

    // ─── Состояние уровня ────────────────────────────────────────────────────
    public float ElapsedTime    { get; private set; }
    public int   StarsCollected { get; private set; }
    public int   CoinsThisRun   { get; private set; }
    public bool  IsRunning      { get; private set; }

    private int _currentLevelIndex;

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
        LoadLevelPrefab(_currentLevelIndex);
        StartLevel();
    }

    private void Update()
    {
        if (!IsRunning) return;
        if (GameManager.Instance == null || GameManager.Instance.State != GameState.Playing) return;

        ElapsedTime += Time.deltaTime;
        OnTimerTick?.Invoke(ElapsedTime);
    }

    // ─── Prefab loading ──────────────────────────────────────────────────────
    private void LoadLevelPrefab(int index)
    {
        var prefabName = LevelLoader.PrefabName(index);
        var prefab     = Resources.Load<GameObject>($"{LevelLoader.ResourcesPath}/{prefabName}");

        if (prefab == null)
        {
            Debug.LogError($"[LevelManager] Prefab не найден: Resources/{LevelLoader.ResourcesPath}/{prefabName}");
            return;
        }

        var parent = levelContainer != null ? levelContainer : transform;
        Instantiate(prefab, parent.position, parent.rotation, parent);
        Debug.Log($"[LevelManager] Загружен уровень {index} ({prefabName})");
    }

    // ─── API ─────────────────────────────────────────────────────────────────
    public void StartLevel()
    {
        ElapsedTime    = 0f;
        StarsCollected = 0;
        CoinsThisRun   = 0;
        IsRunning      = true;
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
        OnLevelCompleted?.Invoke(result);
    }
}
