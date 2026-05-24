using UnityEngine;
using System;

/// <summary>
/// Отслеживает касание обоих WinCollider.
/// Запускает обратный отсчёт 3-2-1 пока оба пэда в зонах.
/// При выходе пэда из зоны — сбрасывает отсчёт.
/// </summary>
public class WinScript : MonoBehaviour
{
    [HideInInspector] public int WinValue;

    [SerializeField] private float countdownDuration = 3f;

    // ─── События ─────────────────────────────────────────────────────────────
    /// <summary>Текущая цифра отсчёта (3, 2, 1). Вызывается при смене цифры.</summary>
    public static event Action<int> OnCountdownTick;

    /// <summary>Отсчёт сброшен — пэд покинул зону.</summary>
    public static event Action OnCountdownCancel;

    // ─── Runtime ─────────────────────────────────────────────────────────────
    private float _timer;
    private int   _lastTick = -1;
    private bool  _wasActive;

    private void OnDestroy()
    {
        OnCountdownTick   = null;
        OnCountdownCancel = null;
    }

    private void Update()
    {
        if (WinValue >= 2)
        {
            _wasActive = true;
            _timer += Time.deltaTime;

            // Текущая цифра: 3 → 2 → 1
            int tick = Mathf.Max(1, Mathf.CeilToInt(countdownDuration - _timer));
            if (tick != _lastTick)
            {
                _lastTick = tick;
                OnCountdownTick?.Invoke(tick);
            }

            if (_timer >= countdownDuration)
            {
                LevelManager.Instance?.CompleteLevel();
                enabled = false;
            }
        }
        else
        {
            if (_wasActive)
            {
                // Пэд покинул зону — сбрасываем
                _timer     = 0f;
                _lastTick  = -1;
                _wasActive = false;
                OnCountdownCancel?.Invoke();
            }
        }
    }
}
