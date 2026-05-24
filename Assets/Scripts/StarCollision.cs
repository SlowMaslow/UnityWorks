using UnityEngine;

/// <summary>
/// Скрипт для коллекционируемых звёзд на уровне.
/// При касании любым коллайдером звезда считается собранной.
/// Назначается на дочерний объект внутри Stars/18337_Star_v1.
/// Убедитесь, что объект имеет Collider с Is Trigger = true.
/// </summary>
public class StarCollision : MonoBehaviour
{
    private bool isCollected;

    private void OnTriggerEnter(Collider other)
    {
        if (isCollected) return;
        isCollected = true;

        VFXManager.Instance?.PlayStarVFX(transform.position);
        LevelManager.Instance?.RegisterStar();
        gameObject.SetActive(false);
    }
}
