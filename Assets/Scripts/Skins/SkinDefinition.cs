using UnityEngine;

/// <summary>
/// Описание одного скина персонажа.
/// Создать: Assets → Create → ClimbUp → Skin Definition
/// </summary>
[CreateAssetMenu(menuName = "ClimbUp/Skin Definition", fileName = "SkinDefinition")]
public class SkinDefinition : ScriptableObject
{
    [Header("Info")]
    public string   skinId;        // уникальный ключ, напр. "skin_01"
    public string   displayName;   // отображаемое имя, напр. "Robot"
    public bool     isDefault;     // первый скин бесплатный и всегда открыт

    [Header("Shop")]
    public int      price;         // цена в монетах (0 = бесплатно)
    public Sprite   previewSprite; // картинка в карточке магазина

    [Header("Model")]
    public GameObject skinPrefab;  // prefab со SkinnedMeshRenderer (Skin01.prefab и т.д.)
}
