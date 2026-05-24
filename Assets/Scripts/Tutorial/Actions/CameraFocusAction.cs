using System;
using UnityEngine;

namespace ClimbUp.Tutorial
{
    /// <summary>
    /// Переключает фокус камеры на объект с указанным TutorialTarget.id.
    /// Камера остаётся на цели и после завершения шага — чтобы вернуть её
    /// к игроку, добавь следующий CameraFocusAction с targetId="player".
    /// При полном завершении туториала камера автоматически возвращается к игроку.
    /// </summary>
    [Serializable]
    public class CameraFocusAction : TutorialAction
    {
        [Tooltip("TutorialTarget.id объекта, на который сфокусироваться (например 'tutor_star', 'player')")]
        public string targetId;

        [Tooltip("Скорость плавного приближения (0 = мгновенно, 4 = плавное)")]
        public float lerpSpeed = 4f;

        [Tooltip("Дистанция камеры до объекта по Z (0 = не трогать Z, оставить как есть). " +
                 "Чем меньше — тем ближе камера. Стандартный setup камеры обычно ≈ 5.")]
        public float distance = 0f;

        [Tooltip("Сдвиг по Y. 0 = объект ровно по центру экрана. " +
                 "Положительное — объект ниже центра, отрицательное — выше центра.")]
        public float yOffset = 0f;

        public override void Enter()
        {
            if (CameraController.Instance == null) return;
            var tt = TutorialTarget.FindComponent(targetId);
            if (tt == null)
            {
                Debug.LogWarning($"[CameraFocusAction] TutorialTarget '{targetId}' не найден");
                return;
            }
            CameraController.Instance.SetTemporaryTarget(
                tt.transform, lerpSpeed, distance, yOffset, tt.offset);
        }

        // Exit намеренно пустой — камера остаётся на цели до следующего CameraFocusAction.
    }
}
