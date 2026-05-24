using UnityEngine;

/// <summary>
/// Хранит флаги контакта каждого пэда с платформой.
/// Singleton — к нему обращаются PlatformCollisionLogic без поиска по сцене.
/// </summary>
public class CollisionChecker : MonoBehaviour
{
    public static CollisionChecker Instance { get; private set; }

    // [0] = левый пэд, [1] = правый пэд
    [HideInInspector] public bool[] collideCheck = new bool[2];

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }
}
