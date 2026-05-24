using System.Collections.Generic;
using UnityEngine;

namespace ClimbUp.Tutorial
{
    /// <summary>
    /// Маркер объекта-цели туториала. Вешается на любой GameObject в сцене/префабе.
    /// Туториал ссылается на цели по логическому <see cref="id"/> (например "red_pad"),
    /// не зная имени GameObject в иерархии.
    /// </summary>
    public class TutorialTarget : MonoBehaviour
    {
        [Tooltip("Логический id (например: red_pad, blue_pad, zone_1, tutor_star)")]
        public string id;

        [Tooltip("Сдвиг от transform.position в МИРОВЫХ единицах. " +
                 "Используется когда pivot объекта не совпадает с визуальным центром " +
                 "(подсветка/фокус будут привязываться к position + offset).")]
        public Vector3 offset;

        /// <summary>Мировая позиция с учётом offset — используется для подсветки и камеры.</summary>
        public Vector3 WorldPosition => transform.position + offset;

        private static readonly Dictionary<string, TutorialTarget> _registry
            = new Dictionary<string, TutorialTarget>();

        private void OnEnable()
        {
            if (string.IsNullOrEmpty(id)) return;
            _registry[id] = this;
        }

        private void OnDisable()
        {
            if (string.IsNullOrEmpty(id)) return;
            if (_registry.TryGetValue(id, out var t) && t == this)
                _registry.Remove(id);
        }

        /// <summary>Backward-compat: возвращает Transform без offset.</summary>
        public static Transform Find(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            return _registry.TryGetValue(id, out var t) && t != null ? t.transform : null;
        }

        /// <summary>Возвращает компонент целиком (для доступа к offset / WorldPosition).</summary>
        public static TutorialTarget FindComponent(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            return _registry.TryGetValue(id, out var t) && t != null ? t : null;
        }

        /// <summary>Мировая позиция цели с учётом offset (или Vector3.zero если не найдено).</summary>
        public static Vector3 FindWorldPosition(string id)
        {
            var tt = FindComponent(id);
            return tt != null ? tt.WorldPosition : Vector3.zero;
        }
    }
}
