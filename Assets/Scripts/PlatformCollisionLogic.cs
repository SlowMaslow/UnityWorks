using UnityEngine;

/// <summary>
/// Фиксирует пэд на платформе.
/// Обнуляет velocity каждый Stay-кадр — joint-импульсы от тела не могут сдвинуть пэд.
/// Снимает фиксацию ТОЛЬКО когда пэд активно тащат мышью (IsDragging).
/// </summary>
public class PlatformCollisionLogic : MonoBehaviour
{
    private static readonly RigidbodyConstraints FrozenConstraints =
        RigidbodyConstraints.FreezePosition    |
        RigidbodyConstraints.FreezeRotationX   |
        RigidbodyConstraints.FreezeRotationY;

    private void OnCollisionEnter(Collision collision) => TryFreeze(collision);
    private void OnCollisionStay(Collision collision)  => TryFreeze(collision);

    private void TryFreeze(Collision collision)
    {
        var colRb = collision.rigidbody;
        if (colRb == null) return;

        var drag = collision.gameObject.GetComponent<DragObject>();
        var cc   = CollisionChecker.Instance;
        if (drag == null || cc == null) return;

        // После разрыва joint'ов — не трогаем пэды, они должны свободно упасть
        if (drag.AreJointsBroken) return;

        // Если пэд сейчас тащат — не мешаем
        if (drag.IsDragging) return;

        // Захват разрешён только если пэд находится в пределах платформы по X (с отступом от краёв).
        // Это исключает угловые захваты, когда пэд висит на ребре платформы.
        var platformCol = GetComponent<Collider>();
        if (platformCol != null)
        {
            var b = platformCol.bounds;
            const float edgeInset = 0.01f;  // минимальный отступ от края
            if (colRb.position.x < b.min.x + edgeInset ||
                colRb.position.x > b.max.x - edgeInset)
                return;
        }

        // Полная фиксация + обнуление velocity каждый кадр
        // Это гарантирует что никакой joint-импульс не сдвинет пэд
        colRb.constraints      = FrozenConstraints;
        colRb.linearVelocity   = Vector3.zero;
        colRb.angularVelocity  = Vector3.zero;

        // Визуальный пэд — kinematic + отвязка от Hand + снап к точке захвата
        drag.FreezeVisual(colRb.transform.position);

        cc.collideCheck[drag.IsFirstPad(colRb) ? 0 : 1] = true;
        drag.UpdatePadDamping();
    }

    private void OnCollisionExit(Collision collision)
    {
        var colRb = collision.rigidbody;
        if (colRb == null) return;

        var drag = collision.gameObject.GetComponent<DragObject>();
        var cc   = CollisionChecker.Instance;
        if (drag == null || cc == null) return;

        if (drag.AreJointsBroken) return;

        // Снимаем фиксацию ТОЛЬКО если пэд активно тащат
        // Иначе тело могло просто дёрнуть пэд — сохраняем FreezePosition
        if (!drag.IsDragging) return;

        colRb.constraints = RigidbodyConstraints.FreezeRotationX
                          | RigidbodyConstraints.FreezeRotationY;

        // Возвращаем визуальный пэд в физику, re-parent обратно под Hand
        drag.UnfreezeVisual();

        cc.collideCheck[drag.IsFirstPad(colRb) ? 0 : 1] = false;
    }
}
