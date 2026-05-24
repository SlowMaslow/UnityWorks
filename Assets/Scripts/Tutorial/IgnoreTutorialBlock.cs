using UnityEngine;

namespace ClimbUp.Tutorial
{
    /// <summary>
    /// Маркер: UI-элемент, помеченный этим компонентом, остаётся интерактивным
    /// даже когда TutorialInputGate.UIBlocked == true. Полезно для debug-кнопок.
    ///
    /// Работает за счёт собственного CanvasGroup на этом GameObject с
    /// blocksRaycasts=true, interactable=true. CanvasGroup нижнего уровня
    /// в иерархии переопределяет CanvasGroup родителя (Canvas).
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    public class IgnoreTutorialBlock : MonoBehaviour
    {
        private void Awake()
        {
            var cg = GetComponent<CanvasGroup>();
            cg.interactable   = true;
            cg.blocksRaycasts = true;
            cg.ignoreParentGroups = true;
        }
    }
}
