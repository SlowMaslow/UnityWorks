using UnityEngine;

/// <summary>
/// Монета собирается в LevelManager.CoinsThisRun.
/// В SaveSystem монеты попадают ТОЛЬКО при завершении уровня (LevelManager.CompleteLevel).
/// Это исключает фарм монет без прохождения уровня.
/// </summary>
public class CoinCollision : MonoBehaviour
{
    private bool isCollected;

    private void OnTriggerEnter(Collider other)
    {
        if (isCollected) return;
        isCollected = true;

        VFXManager.Instance?.PlayCoinVFX(transform.position);
        LevelManager.Instance?.RegisterCoin();
        Destroy(transform.parent.gameObject);
    }
}
