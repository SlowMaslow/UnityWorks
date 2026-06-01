using UnityEngine;

/// <summary>
/// Реестр всех скинов игры.
/// Положить в Assets/Resources/SkinDatabase.asset
/// Создать: Assets → Create → ClimbUp → Skin Database
/// </summary>
[CreateAssetMenu(menuName = "ClimbUp/Skin Database", fileName = "SkinDatabase")]
public class SkinDatabase : ScriptableObject
{
    public SkinDefinition[] skins;

    private static SkinDatabase _instance;

    public static SkinDatabase Instance
    {
        get
        {
            if (_instance == null)
                _instance = Resources.Load<SkinDatabase>("SkinDatabase");
            return _instance;
        }
    }

    /// <summary>Найти скин по skinId.</summary>
    public SkinDefinition Get(string skinId)
    {
        foreach (var s in skins)
            if (s != null && s.skinId == skinId) return s;
        return skins != null && skins.Length > 0 ? skins[0] : null;
    }

    /// <summary>Скин по индексу в массиве.</summary>
    public SkinDefinition GetAt(int index)
    {
        if (skins == null || index < 0 || index >= skins.Length) return null;
        return skins[index];
    }

    public int Count => skins?.Length ?? 0;
}
