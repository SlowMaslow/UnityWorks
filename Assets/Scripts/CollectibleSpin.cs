using UnityEngine;

/// <summary>
/// Постоянное вращение коллектибла (ключ-артефакт) для привлекательности.
/// Вешается на корень префаба. Ось/скорость настраиваются в инспекторе.
/// Вращение вокруг симметричной триггер-сферы безопасно (на сбор не влияет).
/// </summary>
public class CollectibleSpin : MonoBehaviour
{
    [Tooltip("Ось вращения в локальных координатах. Y (вверх) = ключ крутится как на кольце.")]
    [SerializeField] private Vector3 axis = Vector3.up;

    [Tooltip("Скорость вращения, градусов в секунду.")]
    [SerializeField] private float speed = 90f;

    private void Update()
    {
        transform.Rotate(axis.normalized * (speed * Time.deltaTime), Space.Self);
    }
}
