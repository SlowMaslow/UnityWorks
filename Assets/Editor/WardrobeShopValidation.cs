using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public static class WardrobeShopValidation
{
    [MenuItem("ClimbUp/Skins/Validate Wardrobe Purchases")]
    public static void ValidatePurchases()
    {
        var slots=Enum.GetValues(typeof(SkinAccessorySlot)).Cast<SkinAccessorySlot>().ToArray();
        var oldSlots=slots.Select(s=>(slot:s,has:PlayerPrefs.HasKey("AccessorySlot_"+s),value:SaveSystem.GetSelectedAccessory(s))).ToArray();
        bool hadCoins=PlayerPrefs.HasKey("Coins");int oldCoins=SaveSystem.Coins;
        var item=UnityEngine.Object.Instantiate(SkinAccessoryDatabase.Instance.GetSlot(SkinAccessorySlot.Headwear)[0]);
        item.accessoryId="__wardrobe_validation_paid";item.price=100;
        string unlockKey="AccessoryUnlocked_"+item.accessoryId;
        bool hadUnlock=PlayerPrefs.HasKey(unlockKey);int oldUnlock=PlayerPrefs.GetInt(unlockKey);
        GameObject instance=null;
        try
        {
            PlayerPrefs.DeleteKey(unlockKey);
            SaveSystem.Coins=150;
            Require(AccessoryShop.TrySelect(item,out _),"Initial purchase failed");
            Require(SaveSystem.Coins==50 && SaveSystem.IsAccessoryUnlocked(item.accessoryId),"Purchase charge/ownership failed");
            Require(AccessoryShop.TrySelect(item,out _) && SaveSystem.Coins==50,"Owned item charged twice");
            PlayerPrefs.DeleteKey(unlockKey);
            string selection=SaveSystem.GetSelectedAccessory(SkinAccessorySlot.Headwear);
            Require(!AccessoryShop.TrySelect(item,out _) && SaveSystem.Coins==50 &&
                SaveSystem.GetSelectedAccessory(SkinAccessorySlot.Headwear)==selection,"Insufficient-funds purchase changed state");
            var database=SkinAccessoryDatabase.Instance;
            foreach(var slot in slots)
            {
                var free=database.GetSlot(slot).FirstOrDefault(a=>a.price==0);
                if(free==null) AccessoryShop.Clear(slot);
                else Require(AccessoryShop.TrySelect(free,out _),"Free selection failed");
            }
            var saved=slots.Select(SaveSystem.GetSelectedAccessory).ToArray();
            AccessoryShop.Clear(SkinAccessorySlot.Headwear);
            Require(SaveSystem.GetSelectedAccessory(SkinAccessorySlot.Headwear)=="","Empty slot was not saved");
            for(int i=1;i<slots.Length;i++) Require(SaveSystem.GetSelectedAccessory(slots[i])==saved[i],"Clearing one slot affected another");
            instance=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Models/Skins/Skin01/Skin01.prefab"));
            var wardrobe=instance.GetComponent<SkinWardrobe>();wardrobe.ApplySavedLoadout();
            Require(wardrobe.GetEquipped(SkinAccessorySlot.Headwear)==null,"New character ignored empty slot");
            for(int i=1;i<slots.Length;i++) Require((wardrobe.GetEquipped(slots[i])?.accessoryId ?? "")==saved[i],"New character did not restore outfit");
            int transforms=instance.GetComponentsInChildren<Transform>().Length;
            for(int i=0;i<10;i++)
            {
                var hats=database.GetSlot(SkinAccessorySlot.Headwear);
                wardrobe.Equip(hats[i%hats.Length]);
                wardrobe.Unequip(SkinAccessorySlot.Headwear);
            }
            Require(instance.GetComponentsInChildren<Transform>().Length==transforms,"Switching items duplicated slots/bones");
            Directory.CreateDirectory("Temp/SkinWork/Unity");
            File.WriteAllText("Temp/SkinWork/Unity/shop-validation.json","{\"purchaseOnce\":true,\"insufficientFunds\":true,\"independentSlots\":true,\"emptySlotSaved\":true,\"newCharacterRestoresOutfit\":true,\"stableSlotObjects\":true}");
            Debug.Log("[Wardrobe] Purchase, save and independent-slot checks passed.");
        }
        finally
        {
            if(instance!=null) UnityEngine.Object.DestroyImmediate(instance);
            UnityEngine.Object.DestroyImmediate(item);
            if(hadCoins) PlayerPrefs.SetInt("Coins",oldCoins); else PlayerPrefs.DeleteKey("Coins");
            if(hadUnlock) PlayerPrefs.SetInt(unlockKey,oldUnlock); else PlayerPrefs.DeleteKey(unlockKey);
            foreach(var old in oldSlots)
            {
                SaveSystem.SetSelectedAccessory(old.slot,old.value);
                if(!old.has) PlayerPrefs.DeleteKey("AccessorySlot_"+old.slot);
            }
            PlayerPrefs.Save();GameManager.Instance?.NotifyCoinsChanged();
        }
    }

    [MenuItem("ClimbUp/Skins/Render Wardrobe Shop")]
    public static void RenderShop()
    {
        var original=Resources.FindObjectsOfTypeAll<SkinShopController>().First(c=>c.gameObject.scene.IsValid());
        var source=new SerializedObject(original);
        var panelSource=(GameObject)source.FindProperty("panelRoot").objectReferenceValue;
        var preview=new PreviewRenderUtility();
        var canvasGO=new GameObject("WardrobePreview",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));
        try
        {
            preview.AddSingleGO(canvasGO);
            var canvas=canvasGO.GetComponent<Canvas>();canvas.renderMode=RenderMode.WorldSpace;
            canvas.worldCamera=preview.camera;canvas.planeDistance=1;
            ((RectTransform)canvasGO.transform).sizeDelta=new Vector2(924,519);
            preview.camera.orthographic=true;preview.camera.orthographicSize=259.5f;
            preview.camera.transform.position=new Vector3(0,0,-10);preview.camera.transform.rotation=Quaternion.identity;
            preview.camera.nearClipPlane=.01f;preview.camera.farClipPlane=100;
            var scaler=canvasGO.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution=new Vector2(924,519);scaler.matchWidthOrHeight=.5f;
            var panel=UnityEngine.Object.Instantiate(panelSource,canvasGO.transform);
            var controller=canvasGO.AddComponent<SkinShopController>();
            var so=new SerializedObject(controller);
            so.FindProperty("panelRoot").objectReferenceValue=panel;
            so.FindProperty("cardContainer").objectReferenceValue=panel.transform.Find("CardContainer");
            so.FindProperty("cardTemplate").objectReferenceValue=panel.GetComponentInChildren<SkinCardView>(true);
            so.FindProperty("coinsLabel").objectReferenceValue=panel.transform.Find("Coins").GetComponent<Text>();
            so.ApplyModifiedPropertiesWithoutUndo();controller.Open();
            foreach(SkinAccessorySlot slot in Enum.GetValues(typeof(SkinAccessorySlot)))
            {
                controller.SelectSlot(slot);
                preview.BeginStaticPreview(new Rect(0,0,1280,720));
                Canvas.ForceUpdateCanvases();
                LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)panel.transform);
                preview.Render();
                var image=preview.EndStaticPreview();
                File.WriteAllBytes("Temp/SkinWork/Unity/shop-"+slot+".png",image.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(image);
            }
        }
        finally { preview.Cleanup(); }
    }
    private static void Require(bool condition,string message) { if(!condition) throw new Exception(message); }
}
