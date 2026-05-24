using System;
using UnityEngine;

namespace ClimbUp.Tutorial
{
    /// <summary>
    /// Завершается, когда на TutorialEvents шину прилетает событие <see cref="eventId"/>.
    /// Опциональный фильтр <see cref="payloadFilter"/>:
    ///   - пусто → срабатывает на любой payload
    ///   - строка → сравнивается с payload.ToString() / Component.name / GameObject.name
    /// </summary>
    [Serializable]
    public class EventTutorialCondition : TutorialCondition
    {
        [Tooltip("ID события (TutorialEventIds.* или своё)")]
        public string eventId;

        [Tooltip("Опциональный фильтр payload (по имени объекта или строковому id)")]
        public string payloadFilter;

        public override void Enter()
        {
            base.Enter();
            TutorialEvents.OnEvent += Handle;
        }

        public override void Exit()
        {
            TutorialEvents.OnEvent -= Handle;
            base.Exit();
        }

        private void Handle(string id, object payload)
        {
            if (id != eventId) return;
            if (!string.IsNullOrEmpty(payloadFilter) && !Matches(payload, payloadFilter)) return;
            Complete();
        }

        private static bool Matches(object payload, string filter)
        {
            if (payload == null) return false;
            if (payload is string s) return s == filter;
            if (payload is UnityEngine.Component c && c != null) return c.name == filter;
            if (payload is GameObject go && go != null) return go.name == filter;
            return payload.ToString() == filter;
        }
    }
}
