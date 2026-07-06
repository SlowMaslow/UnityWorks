using UnityEngine;

/// <summary>
/// Центральный менеджер звука. Присутствует в каждой сцене на GameManager.
/// Реагирует на игровые события и предоставляет API для воспроизведения звуков.
/// </summary>
public class SoundManager : MonoBehaviour
{
    public static SoundManager Instance { get; private set; }

    [Header("Звуки")]
    [SerializeField] private AudioClip coinSound;
    [SerializeField] private AudioClip starSound;
    [SerializeField] private AudioClip victorySound;
    [SerializeField] private AudioClip buttonSound;
    [SerializeField][Range(0f, 1f)] private float sfxVolume = 1.0f;

    private AudioSource _sfx;

    // ─── Unity ───────────────────────────────────────────────────────────────
    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;

        _sfx             = gameObject.AddComponent<AudioSource>();
        _sfx.loop        = false;
        _sfx.playOnAwake = false;
    }

    private void OnEnable()
    {
        LevelManager.OnCoinCollected     += OnCoin;
        LevelManager.OnArtifactCollected += OnArtifact;
        LevelManager.OnLevelCompleted    += OnVictory;
    }

    private void OnDisable()
    {
        LevelManager.OnCoinCollected     -= OnCoin;
        LevelManager.OnArtifactCollected -= OnArtifact;
        LevelManager.OnLevelCompleted    -= OnVictory;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // ─── Обработчики событий ─────────────────────────────────────────────────
    private void OnCoin(int _)                  => PlaySFX(coinSound);
    private void OnArtifact(int c, int t)       => PlaySFX(starSound);
    private void OnVictory(LevelResult _)       => PlaySFX(victorySound);

    // ─── Public API ──────────────────────────────────────────────────────────
    public void PlayButtonSound() => PlaySFX(buttonSound);

    // ─── Private ─────────────────────────────────────────────────────────────
    private void PlaySFX(AudioClip clip)
    {
        if (clip == null || _sfx == null) return;
        _sfx.PlayOneShot(clip, sfxVolume);
    }
}
