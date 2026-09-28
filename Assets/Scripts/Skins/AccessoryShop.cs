/// <summary>Purchase once, then select independently in one of four wardrobe slots.</summary>
public static class AccessoryShop
{
    public static bool IsOwned(SkinAccessory item) => item != null &&
        (item.price <= 0 || SaveSystem.IsAccessoryUnlocked(item.accessoryId));

    public static bool TrySelect(SkinAccessory item, out string error)
    {
        error = null;
        if (item == null || item.mesh == null || string.IsNullOrEmpty(item.accessoryId) ||
            (int)item.slot < 0 || (int)item.slot >= SkinWardrobe.SlotCount)
        {
            error = "This item is unavailable.";
            return false;
        }
        if (!IsOwned(item))
        {
            int price = UnityEngine.Mathf.Max(0, item.price);
            if (SaveSystem.Coins < price) { error = "Not enough coins."; return false; }
            SaveSystem.Coins -= price;
            SaveSystem.UnlockAccessory(item.accessoryId);
            GameManager.Instance?.NotifyCoinsChanged();
        }
        SaveSystem.SetSelectedAccessory(item.slot, item.accessoryId);
        return true;
    }

    public static void Clear(SkinAccessorySlot slot) => SaveSystem.SetSelectedAccessory(slot, "");
}
