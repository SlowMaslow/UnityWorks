using UnityEngine;

/// <summary>
/// Собираемый артефакт на уровне (крюк-коллектибл). Концептуально заменяет старые
/// звёзды-пикапы (StarCollision): звезда «собрал все артефакты» = собрать все Artifact уровня.
/// При касании коллайдером — засчитывается в текущий забег (LevelManager.RegisterArtifact).
/// НЕ банкуется при провале: в SaveSystem (картинку-мир) попадает только при прохождении уровня.
/// Требует Collider с Is Trigger = true.
/// </summary>
public class Artifact : MonoBehaviour
{
    private bool _collected;

    /// <summary>Собран ли в текущем забеге.</summary>
    public bool IsCollected => _collected;

    private void OnTriggerEnter(Collider other)
    {
        if (_collected) return;
        _collected = true;

        VFXManager.Instance?.PlayStarVFX(transform.position); // TODO: отдельная VFX артефакта
        LevelManager.Instance?.RegisterArtifact();
        gameObject.SetActive(false);
    }
}
