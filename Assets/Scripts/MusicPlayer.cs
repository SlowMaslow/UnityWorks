using UnityEngine;

/// <summary>
/// Воспроизводит фоновую музыку непрерывно через все переходы сцен.
/// DontDestroyOnLoad — создаётся один раз, живёт до закрытия игры.
/// Присутствует в обеих сценах, но только первый экземпляр выживает.
/// </summary>
[DefaultExecutionOrder(-100)] // Awake до любых других скриптов
[DisallowMultipleComponent]
public class MusicPlayer : MonoBehaviour
{
    public static MusicPlayer Instance { get; private set; }

    [SerializeField] private AudioClip music;
    [SerializeField][Range(0f, 1f)] private float volume = 0.45f;

    private AudioSource _source;

    private void Awake()
    {
        // Если уже есть живой Instance — мы дубликат, должны умереть НЕМЕДЛЕННО
        if (Instance != null && Instance != this)
        {
            // DestroyImmediate чтобы не было ни одного кадра с двумя источниками
            DestroyImmediate(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        // Переиспользуем AudioSource если уже есть (защита от двойного AddComponent
        // при повторном Awake после ошибки или редактора)
        _source = GetComponent<AudioSource>();
        if (_source == null) _source = gameObject.AddComponent<AudioSource>();

        // Чистим возможные лишние AudioSource на этом же GameObject
        var allSources = GetComponents<AudioSource>();
        if (allSources.Length > 1)
        {
            for (int i = 0; i < allSources.Length; i++)
                if (allSources[i] != _source)
                    Destroy(allSources[i]);
        }

        _source.clip        = music;
        _source.loop        = true;
        _source.volume      = volume;
        _source.playOnAwake = false;

        // Не рестартуем трек если уже играет (после domain reload / повторного Awake)
        if (!_source.isPlaying) _source.Play();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }
}
