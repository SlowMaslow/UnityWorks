using UnityEngine;

/// <summary>
/// Реестр коллекции: картинка секретного мира и плитка-замок, которой она закрыта.
/// Лежит в Assets/Resources/CollectionDatabase.asset (та же схема, что у SkinDatabase).
///
/// ⚠️ ПОЧЕМУ РЕЕСТР, А НЕ Resources.Load ПО ПУТИ. Арт игрока живёт в Assets/Sprites/Collectable,
/// и тащить его в Resources значило бы перекладывать чужие файлы. Реестр даёт экрану коллекции
/// ссылки, не трогая раскладку спрайтов, — и заодно ставит размер мозаики под правку без кода.
/// </summary>
[CreateAssetMenu(menuName = "ClimbUp/Collection Database", fileName = "CollectionDatabase")]
public class CollectionDatabase : ScriptableObject
{
    [Tooltip("Картинка секретного мира, которую вскрывают ключи.")]
    public Sprite packImage;

    [Tooltip("Плитка-замок, закрывающая один кусочек картинки.")]
    public Sprite lockTile;

    [Tooltip("Сетка мозаики. Клеток должно быть не меньше, чем артефактов в паке.")]
    public int columns = 6;
    public int rows    = 6;

    private static CollectionDatabase _instance;

    public static CollectionDatabase Instance
    {
        get
        {
            if (_instance == null) _instance = Resources.Load<CollectionDatabase>("CollectionDatabase");
            return _instance;
        }
    }

    /// <summary>
    /// Сколько кусочков в мозаике. ⚠️ Не меньше, чем артефактов в паке: иначе последние ключи
    /// вскрывать было бы нечего, и картинка «дособиралась» бы раньше, чем собраны ключи.
    /// На сегодня 12 уровней × 3 ключа = 36 = ровно 6×6.
    /// </summary>
    public int PieceCount => Mathf.Max(1, columns) * Mathf.Max(1, rows);
}
