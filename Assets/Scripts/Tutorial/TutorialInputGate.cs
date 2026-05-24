using System;
using UnityEngine;

namespace ClimbUp.Tutorial
{
    /// <summary>
    /// Глобальный гейт ввода — управляется через TutorialAction'ы.
    /// Игровой код (DragObject и т.д.) проверяет <see cref="GameInputBlocked"/>.
    /// UI блокируется через CanvasGroup на главном Canvas.
    /// </summary>
    public static class TutorialInputGate
    {
        public static bool GameInputBlocked { get; private set; }
        public static bool UIBlocked        { get; private set; }

        public static event Action OnStateChanged;

        public static string MainCanvasName = "Canvas";

        private static CanvasGroup _uiGroup;
        private static bool        _uiGroupAdded;

        public static void SetGameInput(bool blocked)
        {
            if (GameInputBlocked == blocked) return;
            GameInputBlocked = blocked;
            OnStateChanged?.Invoke();
        }

        public static void SetUI(bool blocked)
        {
            if (UIBlocked == blocked) return;
            UIBlocked = blocked;

            EnsureUIGroup();
            if (_uiGroup != null)
            {
                _uiGroup.interactable   = !blocked;
                _uiGroup.blocksRaycasts = !blocked;
            }
            OnStateChanged?.Invoke();
        }

        /// <summary>Сбросить всё (вызывается при OnDestroy у TutorialPlayer).</summary>
        public static void ResetAll()
        {
            if (GameInputBlocked) SetGameInput(false);
            if (UIBlocked)        SetUI(false);
            _uiGroup      = null;
            _uiGroupAdded = false;
        }

        private static void EnsureUIGroup()
        {
            if (_uiGroup != null) return;
            var canvasGO = GameObject.Find(MainCanvasName);
            if (canvasGO == null) return;
            _uiGroup = canvasGO.GetComponent<CanvasGroup>();
            if (_uiGroup == null)
            {
                _uiGroup      = canvasGO.AddComponent<CanvasGroup>();
                _uiGroupAdded = true;
            }
        }
    }
}
