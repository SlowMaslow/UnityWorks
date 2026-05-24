using UnityEngine;

/// <summary>
/// Воспроизводит фоновую музыку непрерывно через все переходы сцен.
/// DontDestroyOnLoad — создаётся один раз, живёт до закрытия игры.
/// Присутствует в обеих сценах, но только первый экземпляр выживает.
/// </summary>
public class MusicPlayer : MonoBehaviour
{
    public static MusicPlayer Instance { get; private set; }

    [SerializeField] private AudioClip music;
    [SerializeField][Range(0f, 1f)] private float volume = 0.45f;

    private AudioSource _source;

    private void Awake()
    {
        // Если уже есть — уничтожаем дубликат (музыка продолжает играть)
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        _source             = gameObject.AddComponent<AudioSource>();
        _source.clip        = music;
        _source.loop        = true;
        _source.volume      = volume;
        _source.playOnAwake = false;
        _source.Play();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }
}
