using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Триггер на верхней грани платформы. Отслеживает какие пэды сейчас на поверхности.
/// Прототип спрашивает Contains() при отпускании пэда чтобы решить — захват или падение.
/// </summary>
public class PlatformTop : MonoBehaviour
{
    public float surfaceY;   // мировая Y верхней поверхности (для привязки пэда)
    private readonly HashSet<Rigidbody> _pads = new HashSet<Rigidbody>();

    private void OnTriggerEnter(Collider other)
    {
        var rb = other.attachedRigidbody;
        if (rb != null) _pads.Add(rb);
    }

    private void OnTriggerExit(Collider other)
    {
        var rb = other.attachedRigidbody;
        if (rb != null) _pads.Remove(rb);
    }

    public bool Contains(Rigidbody rb) => _pads.Contains(rb);
}
