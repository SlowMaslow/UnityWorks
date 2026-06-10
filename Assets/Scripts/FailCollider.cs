using UnityEngine;

/// <summary>
/// Зона провала внизу уровня (FallCollider). Живёт в level prefab.
/// Срабатывает, когда в неё попадает что-либо принадлежащее игроку (пэд/тело) → GameState.Fail.
/// Игрок определяется по ClimbController в корне иерархии — без зависимости от старой
/// ragdoll-механики (DragObject/CollisionChecker удалены).
/// </summary>
public class FailCollider : MonoBehaviour
{
    private void OnCollisionEnter(Collision collision) => TryFail(collision.collider);
    private void OnTriggerEnter(Collider other)        => TryFail(other);

    private void TryFail(Collider other)
    {
        if (other == null) return;
        // Принадлежит игроку? (корень иерархии содержит ClimbController)
        if (other.transform.root.GetComponentInChildren<ClimbController>() == null) return;

        GameManager.Instance?.SetState(GameState.Fail);
    }
}
