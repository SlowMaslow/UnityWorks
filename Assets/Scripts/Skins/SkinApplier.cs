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

        // Перепривязка костей: ищем каждую кость скина в риге по имени. Если кость отсутствует
        // (у скина больше костей, чем у базового рига — напр. фаланги среднего пальца у Skin03,
        // которых нет у Skin01) — ВОССОЗДАЁМ её на риге с теми же локальными трансформами, что
        // в скине (bind-pose совпадает → палец отрисовывается корректно, просто без анимации).
        var srcBones = skinSmr.bones;
        var newBones = new Transform[srcBones.Length];
        for (int i = 0; i < srcBones.Length; i++)
        {
            if (srcBones[i] == null) continue;
            newBones[i] = GetOrCreateBone(srcBones[i], boneMap);
        }
        newSmr.bones = newBones;

        // rootBone — Hips
        if (boneMap.TryGetValue("mixamorig:Hips", out var hips))
            newSmr.rootBone = hips;

        Debug.Log($"[SkinApplier] Applied skin: {skin.displayName}");
    }

    /// <summary>
    /// Возвращает кость рига с именем как у src. Если её нет — рекурсивно создаёт цепочку
    /// недостающих костей (от ближайшего существующего предка) с локальными трансформами из
    /// скелета скина, чтобы bind-pose совпал и меш деформировался корректно.
    /// </summary>
    private Transform GetOrCreateBone(Transform src, Dictionary<string, Transform> boneMap)
    {
        if (src == null)
            return boneMap.TryGetValue("mixamorig:Hips", out var root) ? root : transform;

        if (boneMap.TryGetValue(src.name, out var existing) && existing != null)
            return existing;

        var parent = GetOrCreateBone(src.parent, boneMap);
        var bone = new GameObject(src.name).transform;
        bone.SetParent(parent, false);
        bone.localPosition = src.localPosition;
        bone.localRotation = src.localRotation;
        bone.localScale    = src.localScale;
        boneMap[src.name]  = bone;
        return bone;
    }

    private void SetDefaultRenderers(bool enabled)
    {
        if (defaultRenderers == null) return;
        foreach (var r in defaultRenderers)
            if (r != null) r.enabled = enabled;
    }
}
