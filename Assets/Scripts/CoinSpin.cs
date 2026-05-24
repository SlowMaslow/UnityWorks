using UnityEngine;

/// <summary>
/// Простое вращение монетки вокруг оси Y.
/// Добавляется на корневой объект монетки (Coin).
/// </summary>
public class CoinSpin : MonoBehaviour
{
    [SerializeField] private float speed = 200f; // градусов в секунду

    private void Update()
    {
        transform.Rotate(speed * Time.deltaTime, 0f, 0f, Space.Self);
    }
}
