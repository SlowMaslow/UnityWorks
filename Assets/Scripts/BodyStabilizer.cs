using UnityEngine;

/// <summary>
/// Удерживает таз персонажа лицом к камере (по горизонтали), убирая рысканье.
/// Работает всегда — даёт телу "осанку", чтобы рагдолл не отворачивался и
/// не сваливался в неопрятные позы. Руки/ноги при этом свободно articulate.
/// </summary>
public class BodyStabilizer : MonoBehaviour
{
    [SerializeField] private Rigidbody hipsRb;

    [Header("Facing (рысканье к камере)")]
    [Tooltip("Куда тело должно смотреть по горизонтали. (0,0,-1) = к камере.")]
    [SerializeField] private Vector3 targetFacing = new Vector3(0f, 0f, -1f);
    [SerializeField] private float facingStrength = 25f;
    [SerializeField] private float facingDamping  = 4f;

    private void Reset()
    {
        // Авто-поиск Hips при добавлении компонента
        foreach (var rb in GetComponentsInChildren<Rigidbody>())
            if (rb.name == "mixamorig:Hips") { hipsRb = rb; break; }
    }

    private void FixedUpdate()
    {
        if (hipsRb == null) return;

        // Текущее направление "взгляда" таза, спроецированное на горизонталь
        Vector3 fwd = hipsRb.transform.forward;
        fwd.y = 0f;
        if (fwd.sqrMagnitude < 1e-4f) return;
        fwd.Normalize();

        Vector3 tgt = targetFacing;
        tgt.y = 0f;
        tgt.Normalize();

        // Ошибка по рысканью (вокруг мировой оси Y)
        float angleErr = Vector3.SignedAngle(fwd, tgt, Vector3.up); // градусы
        float torqueY  = angleErr * facingStrength * Mathf.Deg2Rad
                       - hipsRb.angularVelocity.y * facingDamping;

        hipsRb.AddTorque(0f, torqueY, 0f, ForceMode.Acceleration);
    }
}
