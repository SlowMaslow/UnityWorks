using System;
using UnityEngine;

namespace ClimbUp.Tutorial
{
    /// <summary>
    /// Композитное условие: ВСЕ (And) или ЛЮБОЕ (Or) из дочерних должны выполниться.
    /// Позволяет собирать сложные триггеры в Inspector без кода.
    /// </summary>
    [Serializable]
    public class CompoundTutorialCondition : TutorialCondition
    {
        public enum Mode { And, Or }

        public Mode mode = Mode.And;

        [SerializeReference, SubclassSelector] public TutorialCondition[] children = new TutorialCondition[0];

        public override void Enter()
        {
            base.Enter();
            if (children == null) return;
            foreach (var c in children)
            {
                if (c == null) continue;
                c.Reset();
                c.OnCompleted += OnChildCompleted;
                c.Enter();
            }
            // Возможно дети сразу выполнены — проверим
            CheckCompletion();
        }

        public override void Exit()
        {
            if (children != null)
            {
                foreach (var c in children)
                {
                    if (c == null) continue;
                    c.OnCompleted -= OnChildCompleted;
                    c.Exit();
                }
            }
            base.Exit();
        }

        public override void Tick(float dt)
        {
            if (children == null) return;
            foreach (var c in children)
                if (c != null && !c.IsCompleted)
                    c.Tick(dt);
        }

        private void OnChildCompleted() => CheckCompletion();

        private void CheckCompletion()
        {
            if (children == null || children.Length == 0) return;
            bool result = mode == Mode.And;
            foreach (var c in children)
            {
                if (c == null) continue;
                bool ok = c.IsCompleted;
                if (mode == Mode.And) result &= ok;
                else                  result |= ok;
            }
            if (result) Complete();
        }
    }
}
