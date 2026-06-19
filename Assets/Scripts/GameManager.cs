using UnityEngine;
using System;

/// <summary>
/// Центральный синглтон сцены. Управляет состоянием игры и глобальными событиями.
/// Присутствует в каждой сцене на объекте GameManager.
/// НЕ использует DontDestroyOnLoad — каждая сцена имеет свой экземпляр.
/// </summary>
public class GameManager : MonoBehaviour
{
    // ─── Singleton ───────────────────────────────────────────────────────────
    public static GameManager Instance { get; private set; }

    // ─── Состояние ───────────────────────────────────────────────────────────
    public GameState State { get; private set; } = GameState.Playing;
    public bool IsPaused => State == GameState.Paused;

    // ─── Глобальные события ──────────────────────────────────────────────────
    /// <summary>Вызывается при любой смене состояния игры.</summary>
    public static event Action<GameState> OnGameStateChanged;

    /// <summary>Вызывается при изменении количества монет. Передаёт новое значение.</summary>
    public static event Action<int> OnCoinsChanged;

    // ─── Unity ───────────────────────────────────────────────────────────────
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
            // Очищаем статические события при уничтожении,
            // чтобы не держать ссылки на объекты предыдущей сцены.
            OnGameStateChanged = null;
            OnCoinsChanged = null;
        }
    }

    // ─── Управление состоянием ───────────────────────────────────────────────
    public void SetState(GameState newState)
    {
        if (State == newState) return;
        State = newState;

        // Пауза останавливает Time.timeScale; Win/Fail — оставляем физику
        Time.timeScale = (newState == GameState.Paused) ? 0f : 1f;

        // Аналитика проигрыша (победа логируется в LevelManager.CompleteLevel с результатом)
        if (newState == GameState.Fail)
            AnalyticsManager.Instance?.LevelFail(LevelManager.Instance != null ? LevelManager.Instance.CurrentLevelIndex : -1);

        OnGameStateChanged?.Invoke(newState);
    }

    public void TogglePause()
    {
        if (State == GameState.Playing) SetState(GameState.Paused);
        else if (State == GameState.Paused) SetState(GameState.Playing);
    }

    // ─── Монеты ──────────────────────────────────────────────────────────────
    public void AddCoins(int amount)
    {
        if (amount <= 0) return;
        SaveSystem.Coins += amount;
        OnCoinsChanged?.Invoke(SaveSystem.Coins);
    }

    /// <summary>Оповещает UI об изменении монет без изменения значения (например после внешнего сохранения).</summary>
    public void NotifyCoinsChanged() => OnCoinsChanged?.Invoke(SaveSystem.Coins);

    /// <returns>true если монет хватило и они были списаны.</returns>
    public bool TrySpendCoins(int amount)
    {
        if (SaveSystem.Coins < amount) return false;
        SaveSystem.Coins -= amount;
        OnCoinsChanged?.Invoke(SaveSystem.Coins);
        return true;
    }
}
