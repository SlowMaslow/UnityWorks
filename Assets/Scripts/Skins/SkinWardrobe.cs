using System.Collections.Generic;
using UnityEngine;

/// <summary>Six permanent slots. Modules replace explicit regions on one canonical skeleton.</summary>
[DisallowMultipleComponent]
public sealed class SkinWardrobe : MonoBehaviour
{
    public const int TriangleBudget = 6000;
    public const int SlotCount = 6;
    [SerializeField] private SkinnedMeshRenderer body;
    [SerializeField, HideInInspector] private Mesh baseBodyMesh;
    [Tooltip("Load the player's four shop selections. Turn off only for authoring a fixed preview outfit.")]
    [SerializeField] private bool useSavedLoadout = true;
    [Header("Six independent slots (empty restores the base module)")]
    [SerializeField] private SkinAccessory headwear;
    [SerializeField] private SkinAccessory outerwear;
    [SerializeField] private SkinAccessory pants;
    [SerializeField] private SkinAccessory footwear;
    [SerializeField] private SkinAccessory head;
    [SerializeField] private SkinAccessory glasses;
    private readonly SkinnedMeshRenderer[] renderers = new SkinnedMeshRenderer[SlotCount];
    private readonly SkinAccessory[] items = new SkinAccessory[SlotCount];
    private readonly Dictionary<int, Mesh> coveredBodies = new Dictionary<int, Mesh>();
    private Mesh fullBodyMesh;
    public SkinnedMeshRenderer Body => body;
    public SkinAccessory GetEquipped(SkinAccessorySlot slot) => (int)slot >= 0 && (int)slot < SlotCount ? items[(int)slot] : null;

    private void Awake()
    {
        EnsureSlots();
        if (useSavedLoadout) ApplySavedLoadout();
        else
        {
            var initial = new[] { headwear, outerwear, pants, footwear, head, glasses };
            for (int i = 0; i < initial.Length; i++)
                if (initial[i] != null && (int)initial[i].slot == i) Equip(initial[i]);
        }
    }
    private void OnEnable()
    {
        SaveSystem.AccessoriesChanged += OnSelectionChanged;
        if (Application.isPlaying && useSavedLoadout) ApplySavedLoadout();
    }
    private void OnDisable() => SaveSystem.AccessoriesChanged -= OnSelectionChanged;
    private void OnSelectionChanged() { if (useSavedLoadout) ApplySavedLoadout(); }

    public void EnsureSlots()
    {
        if (body == null) body = GetComponentInChildren<SkinnedMeshRenderer>();
        if (body == null) return;
        if (fullBodyMesh == null) fullBodyMesh = baseBodyMesh != null ? baseBodyMesh : body.sharedMesh;
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] != null) continue;
            string slotName=((SkinAccessorySlot)i).ToString();
            var existing=body.transform.Find(slotName);
            var go=existing != null ? existing.gameObject : new GameObject(slotName) { layer = body.gameObject.layer };
            go.transform.SetParent(body.transform, false);
            var renderer=go.GetComponent<SkinnedMeshRenderer>();
            if(renderer==null) renderer=go.AddComponent<SkinnedMeshRenderer>();
            renderer.bones = body.bones;
            renderer.rootBone = body.rootBone;
            renderer.localBounds = body.localBounds;
            renderer.quality = body.quality;
            renderer.updateWhenOffscreen = body.updateWhenOffscreen;
            renderer.enabled = false;
            renderers[i] = renderer;
        }
    }

    public void ApplySavedLoadout()
    {
        EnsureSlots();
        var database = SkinAccessoryDatabase.Instance;
        for (int i = 0; i < items.Length; i++)
        {
            var slot = (SkinAccessorySlot)i;
            var item = database != null ? database.Get(SaveSystem.GetSelectedAccessory(slot)) : null;
            if (item != null && item.slot == slot && AccessoryShop.IsOwned(item))
            {
                if (items[i] != item && !Equip(item)) Unequip(slot);
            }
            else Unequip(slot);
        }
    }

    public bool Equip(SkinAccessory accessory)
    {
        EnsureSlots();
        if (accessory == null || accessory.mesh == null || body == null || fullBodyMesh == null) return false;
        int slot = (int)accessory.slot;
        if (slot < 0 || slot >= items.Length) return false;
        var binds = accessory.mesh.bindposes;
        var bodyBinds = fullBodyMesh.bindposes;
        if (binds.Length != body.bones.Length || binds.Length != bodyBinds.Length ||
            accessory.materials == null || accessory.materials.Length != accessory.mesh.subMeshCount) return false;
        for (int i = 0; i < binds.Length; i++) if (binds[i] != bodyBinds[i]) return false;
        if (accessory.bodyRegion != RegionForSlot(accessory.slot)) return false;
        int mask = accessory.bodyRegion >= 0 ? 1 << accessory.bodyRegion : 0;
        for (int i = 0; i < items.Length; i++)
            if (i != slot && items[i] != null && items[i].bodyRegion >= 0) mask |= 1 << items[i].bodyRegion;
        int count = Triangles(GetCoveredBody(mask)) + Triangles(accessory.mesh);
        for (int i = 0; i < items.Length; i++) if (i != slot && items[i] != null) count += Triangles(items[i].mesh);
        if (count > TriangleBudget)
        {
            Debug.LogWarning($"[SkinWardrobe] Outfit exceeds {TriangleBudget} triangles.", this);
            return false;
        }
        var renderer = renderers[slot];
        renderer.sharedMesh = accessory.mesh;
        renderer.sharedMaterials = accessory.materials;
        renderer.enabled = true;
        items[slot] = accessory;
        SetField(accessory.slot, accessory);
        UpdateBodyCoverage();
        return true;
    }

    public void Unequip(SkinAccessorySlot slot)
    {
        int index = (int)slot;
        if (index < 0 || index >= items.Length) return;
        if (renderers[index] != null)
        {
            renderers[index].enabled = false;
            renderers[index].sharedMesh = null;
            renderers[index].sharedMaterials = new Material[0];
        }
        items[index] = null;
        SetField(slot, null);
        UpdateBodyCoverage();
    }

    private void SetField(SkinAccessorySlot slot, SkinAccessory value)
    {
        switch (slot)
        {
            case SkinAccessorySlot.Headwear: headwear = value; break;
            case SkinAccessorySlot.Outerwear: outerwear = value; break;
            case SkinAccessorySlot.Pants: pants = value; break;
            case SkinAccessorySlot.Footwear: footwear = value; break;
            case SkinAccessorySlot.Head: head = value; break;
            case SkinAccessorySlot.Glasses: glasses = value; break;
        }
    }

    private void UpdateBodyCoverage()
    {
        if (body == null || fullBodyMesh == null) return;
        int mask = 0;
        foreach (var item in items) if (item != null && item.bodyRegion >= 0) mask |= 1 << item.bodyRegion;
        body.sharedMesh = GetCoveredBody(mask);
    }

    public static int RegionForSlot(SkinAccessorySlot slot)
    {
        switch (slot)
        {
            case SkinAccessorySlot.Head: return 0;
            case SkinAccessorySlot.Outerwear: return 1;
            case SkinAccessorySlot.Pants: return 2;
            case SkinAccessorySlot.Footwear: return 3;
            default: return -1;
        }
    }

    private Mesh GetCoveredBody(int mask)
    {
        if (mask == 0) return fullBodyMesh;
        if (!coveredBodies.TryGetValue(mask, out var covered))
        {
            covered = Instantiate(fullBodyMesh);
            covered.name = "Skin01_Body (module mask " + mask + ")";
            var regions = fullBodyMesh.uv2;
            for (int submesh = 0; submesh < fullBodyMesh.subMeshCount; submesh++)
            {
                var input = fullBodyMesh.GetTriangles(submesh);
                var visible = new List<int>(input.Length);
                for (int i = 0; i < input.Length; i += 3)
                {
                    int region = Mathf.RoundToInt(regions[input[i]].x);
                    if ((mask & (1 << region)) != 0) continue;
                    visible.Add(input[i]); visible.Add(input[i + 1]); visible.Add(input[i + 2]);
                }
                covered.SetTriangles(visible, submesh);
            }
            coveredBodies.Add(mask, covered);
        }
        return covered;
    }

    private void OnDestroy()
    {
        foreach (var mesh in coveredBodies.Values)
            if (Application.isPlaying) Destroy(mesh); else DestroyImmediate(mesh);
    }
    private static int Triangles(Mesh mesh)
    {
        int count = 0;
        for (int i = 0; i < mesh.subMeshCount; i++) count += (int)mesh.GetIndexCount(i) / 3;
        return count;
    }
}
