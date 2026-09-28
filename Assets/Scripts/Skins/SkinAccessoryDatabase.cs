using System.Linq;
using UnityEngine;

[CreateAssetMenu(menuName = "ClimbUp/Accessory Database")]
public sealed class SkinAccessoryDatabase : ScriptableObject
{
    public SkinAccessory[] accessories = new SkinAccessory[0];
    public Sprite emptySlotPreview;
    public Sprite[] baseSlotPreviews = new Sprite[SkinWardrobe.SlotCount];
    public Sprite GetBasePreview(SkinAccessorySlot slot) => baseSlotPreviews != null &&
        (int)slot >= 0 && (int)slot < baseSlotPreviews.Length ? baseSlotPreviews[(int)slot] : null;
    private static SkinAccessoryDatabase instance;
    public static SkinAccessoryDatabase Instance => instance != null ? instance :
        (instance = Resources.Load<SkinAccessoryDatabase>("SkinAccessoryDatabase"));
    public SkinAccessory Get(string id) => string.IsNullOrEmpty(id) ? null :
        accessories?.FirstOrDefault(a => a != null && a.accessoryId == id);
    public SkinAccessory[] GetSlot(SkinAccessorySlot slot) => accessories == null ? new SkinAccessory[0] :
        accessories.Where(a => a != null && a.slot == slot).ToArray();
}
