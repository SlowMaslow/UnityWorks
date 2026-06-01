using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Находится на корневом объекте Player.
/// При старте читает выбранный скин из SaveSystem и подменяет визуальный меш,
/// оставляя физику (кости рагдолла) нетронутой.
/// </summary>
public class SkinApplier : MonoBehaviour
{
    [Header("Дефолтные рендереры (выключаются при применении скина)")]
    [SerializeField] private SkinnedMeshRenderer[] defaultRenderers;

    private void Start()
    {
        var db = SkinDatabase.Instance;
        if (db == null) { Debug.LogWarning("[SkinApplier] SkinDatabase not found in Resources"); return; }

        var selectedId = SaveSystem.SelectedSkinId;
        var skin = string.IsNullOrEmpty(selectedId) ? db.GetAt(0) : db.Get(selectedId);
        if (skin == null) return;

        ApplySkin(skin);
    }

    public void ApplySkin(SkinDefinition skin)
    {
        if (skin == null || skin.skinPrefab == null) return;

        // Скин с дефолтным мешем — просто включаем дефолтные рендереры
        if (skin.isDefault)
        {
            SetDefaultRenderers(true);
            // Удаляем ранее добавленный кастомный меш если есть
            var old = transform.Find("_SkinMesh");
            if (old != null) Destroy(old.gameObject);
            return;
        }

        // Выключаем дефолтный визуал
        SetDefaultRenderers(false);

        // Удаляем предыдущий скин-меш
        var prev = transform.Find("_SkinMesh");
        if (prev != null) Destroy(prev.gameObject);

        // Получаем SkinnedMeshRenderer из prefab скина
        var skinSmr = skin.skinPrefab.GetComponentInChildren<SkinnedMeshRenderer>();
        if (skinSmr == null) { Debug.LogWarning($"[SkinApplier] No SkinnedMeshRenderer in skin prefab: {skin.skinId}"); return; }

        // Строим карту костей текущего рагдолла (по имени)
        var boneMap = new Dictionary<string, Transform>();
        foreach (var t in transform.GetComponentsInChildren<Transform>(true))
        {
            if (!boneMap.ContainsKey(t.name))
                boneMap[t.name] = t;
        }

        // Создаём объект для нового меша
        var meshGO = new GameObject("_SkinMesh");
        meshGO.transform.SetParent(transform, false);

        // Копируем SkinnedMeshRenderer и перепривязываем кости к рагдоллу
        var newSmr       = meshGO.AddComponent<SkinnedMeshRenderer>();
        newSmr.sharedMesh = skinSmr.sharedMesh;
        newSmr.sharedMaterials = skinSmr.sharedMaterials;

        // Перепривязка костей: ищем каждую кость скина в иерархии рагдолла
        var srcBones = skinSmr.bones;
        var newBones = new Transform[srcBones.Length];
        for (int i = 0; i < srcBones.Length; i++)
        {
            if (srcBones[i] == null) continue;
            if (!boneMap.TryGetValue(srcBones[i].name, out newBones[i]))
                Debug.LogWarning($"[SkinApplier] Bone not found in ragdoll: {srcBones[i].name}");
        }
        newSmr.bones = newBones;

        // rootBone — Hips
        if (boneMap.TryGetValue("mixamorig:Hips", out var hips))
            newSmr.rootBone = hips;

        Debug.Log($"[SkinApplier] Applied skin: {skin.displayName}");
    }

    private void SetDefaultRenderers(bool enabled)
    {
        if (defaultRenderers == null) return;
        foreach (var r in defaultRenderers)
            if (r != null) r.enabled = enabled;
    }
}
