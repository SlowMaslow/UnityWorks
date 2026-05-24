using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Миграция unpacked Level prefab'ов в prefab-instances.
/// Проходит по дочерним объектам каждого уровня, опознаёт source prefab
/// по имени и конвертирует unpacked GameObject обратно в prefab instance
/// со связью с исходным префабом. Все правки в source-префабах теперь будут
/// автоматически подхватываться уровнями.
/// </summary>
public static class LevelMigrator
{
    // ─── Карта "имя в иерархии → путь к source prefab" ───────────────────────
    private static readonly Dictionary<string, string> ExactName = new Dictionary<string, string>
    {
        { "Player",         "Assets/Prefabs/Player.prefab"         },
        { "BackGroundWall", "Assets/Prefabs/BackGroundWall.prefab" },
        { "FallCollider",   "Assets/Prefabs/FallCollider.prefab"   },
        { "LeftFin",        "Assets/Prefabs/LeftFin.prefab"        },
        { "RightFin",       "Assets/Prefabs/RightFin.prefab"       },
    };

    // Совпадение по началу имени (Platform_01, Platform (1), Wall_05, ...).
    // Префикс матчится только если после него следует разделитель ' ', '_', '(',
    // либо это всё имя. Это исключает совпадения типа "Platforms" (группа), "PlatformWall".
    // Порядок важен: более специфичные сначала.
    private static readonly (string prefix, string path)[] PrefixName =
    {
        ("PlatformWall", "Assets/Prefabs/PlatformWall.prefab"),
        ("Wall",         "Assets/Prefabs/PlatformWall.prefab"),
        ("Platform",     "Assets/Prefabs/Platform.prefab"),
        ("Coin",         "Assets/Prefabs/Coin.prefab"),
        ("Star",         "Assets/Prefabs/Star.prefab"),
    };

    private static bool MatchesPrefix(string name, string prefix)
    {
        if (name == null || prefix == null) return false;
        if (name.Length < prefix.Length || !name.StartsWith(prefix)) return false;
        if (name.Length == prefix.Length) return true;
        char next = name[prefix.Length];
        return next == ' ' || next == '_' || next == '(';
    }

    [MenuItem("Tools/ClimbUp/Re-link Level Prefabs to Sources")]
    public static void MigrateAllLevels()
    {
        var guids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Resources/Levels" });
        int total = 0, converted = 0;

        foreach (var guid in guids)
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            int n = MigrateLevel(path);
            if (n > 0) converted += n;
            total++;
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        EditorUtility.DisplayDialog("Done",
            $"Обработано уровней: {total}\nКонвертировано объектов: {converted}", "OK");
    }

    public static int MigrateLevel(string levelPath)
    {
        var root = PrefabUtility.LoadPrefabContents(levelPath);
        if (root == null) return 0;

        int converted = 0;
        try
        {
            // Идём по всем потомкам и пытаемся каждый конвертировать
            // (НО только если он сам не является уже prefab instance)
            var queue = new Queue<Transform>();
            queue.Enqueue(root.transform);

            var toConvert = new List<GameObject>();
            while (queue.Count > 0)
            {
                var t = queue.Dequeue();
                foreach (Transform c in t)
                {
                    var go = c.gameObject;
                    bool isAlreadyInstance =
                        PrefabUtility.IsPartOfPrefabInstance(go) ||
                        PrefabUtility.GetCorrespondingObjectFromSource(go) != null;
                    if (!isAlreadyInstance && DetectSource(go) != null)
                    {
                        toConvert.Add(go);
                        // Не идём внутрь — нашли узел для конвертации
                    }
                    else
                    {
                        // Идём глубже только если это не сам по себе instance
                        if (!isAlreadyInstance)
                            queue.Enqueue(c);
                    }
                }
            }

            foreach (var go in toConvert)
            {
                var source = DetectSource(go);
                if (source == null) continue;

                var settings = new ConvertToPrefabInstanceSettings
                {
                    objectMatchMode                    = ObjectMatchMode.ByHierarchy,
                    componentsNotMatchedBecomesOverride= true,
                    gameObjectsNotMatchedBecomesOverride= true,
                    recordPropertyOverridesOfMatches   = true,
                    changeRootNameToAssetName          = false,
                };
                PrefabUtility.ConvertToPrefabInstance(go, source, settings,
                    InteractionMode.AutomatedAction);
                converted++;
            }

            if (converted > 0)
                PrefabUtility.SaveAsPrefabAsset(root, levelPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }

        Debug.Log($"[LevelMigrator] {levelPath}: converted {converted} objects.");
        return converted;
    }

    private static GameObject DetectSource(GameObject plain)
    {
        if (plain == null) return null;
        string n = plain.name;

        if (ExactName.TryGetValue(n, out var path))
            return AssetDatabase.LoadAssetAtPath<GameObject>(path);

        foreach (var (prefix, p) in PrefixName)
            if (MatchesPrefix(n, prefix))
                return AssetDatabase.LoadAssetAtPath<GameObject>(p);

        return null;
    }
}
