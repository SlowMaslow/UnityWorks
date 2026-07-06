using UnityEngine;

/// <summary>
/// Чекпоинт для оживления (continue). Ставится в уровне КАК SpawnPoint: корень маркера в точке,
/// куда телепортируется КОРЕНЬ игрока (правило спавна: (X, surfaceY - padLocalY, z), чтобы пэды
/// сели на платформу при MoveTo). LevelManager активирует чекпоинт, когда игрок поднялся до его
/// высоты; при оживлении телепорт к самому высокому пройденному чекпоинту (иначе — старт-спавн).
/// </summary>
public class Checkpoint : MonoBehaviour
{
    private void OnDrawGizmos()
    {
        Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.9f);
        Gizmos.DrawWireSphere(transform.position, 0.3f);
        Gizmos.DrawLine(transform.position, transform.position + Vector3.up * 1.2f);
    }
}
