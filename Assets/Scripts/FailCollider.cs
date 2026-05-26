using UnityEngine;

/// <summary>
/// Зона провала внизу уровня. Живёт в level prefab.
/// Находит ragdoll rigidbodies по DragObject в сцене —
/// не нужны serialized ссылки на объекты из другой иерархии.
/// </summary>
public class FailCollider : MonoBehaviour
{
    private Rigidbody[] _ragdollBodies;

    private void Start()
    {
        // DragObject есть на каждом пэде (LeftHockeyPad, RightHockeyPad)
        var drags = FindObjectsByType<DragObject>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);

        _ragdollBodies = new Rigidbody[drags.Length];
        for (int i = 0; i < drags.Length; i++)
            _ragdollBodies[i] = drags[i].GetComponent<Rigidbody>();
    }

    private void OnCollisionEnter(Collision collision)
    {
        // Игнорируем отлетевшие visual пэды — у них root без CollisionChecker в иерархии.
        // Реагируем только на части рагдолла (их root = Player, CollisionChecker есть в children).
        if (collision.rigidbody != null &&
            collision.rigidbody.transform.root.GetComponentInChildren<CollisionChecker>() == null)
            return;

        // Освобождаем рагдолл
        foreach (var rb in _ragdollBodies)
            if (rb != null) rb.constraints = RigidbodyConstraints.None;

        GameManager.Instance?.SetState(GameState.Fail);
    }
}
