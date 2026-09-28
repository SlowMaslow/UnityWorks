using UnityEngine;

// Keep the existing values/names: saved four-slot outfits migrate without losing purchases.
public enum SkinAccessorySlot { Headwear = 0, Outerwear = 1, Pants = 2, Footwear = 3, Head = 4, Glasses = 5 }

/// <summary>A garment mesh in the shared Skin01 bind pose. Never owns a skeleton.</summary>
[CreateAssetMenu(menuName = "ClimbUp/Skin Accessory")]
public sealed class SkinAccessory : ScriptableObject
{
    public string accessoryId;
    public string displayName;
    [Min(0)] public int price;
    public Sprite previewSprite;
    public SkinAccessorySlot slot;
    public Mesh mesh;
    public Material[] materials;
    [Tooltip("Replaced base region: -1 overlay, 0 head, 1 torso/arms, 2 legs, 3 feet. Must use the canonical interface loops.")]
    [Range(-1, 3)] public int bodyRegion = -1;
}
