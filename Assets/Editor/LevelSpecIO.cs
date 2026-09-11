using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ⭐⭐ ХРАНЕНИЕ ОПИСАНИЯ УРОВНЯ НА ДИСКЕ.
///
/// 🐞 Зачем понадобилось (поймано игроком: «ничего не изменилось, кнопка C не на потолке, D не в
/// группе C»). Описание жило в поле окна редактора, а окно — это ScriptableObject, и Unity
/// сериализует его при каждой перезагрузке домена: перекомпиляция скрипта, вход в Play, смена
/// раскладки окон. СТРОКИ такой круг переживают, а обычный C#-объект — нет, он становится null.
/// Замер после жалобы: `_mazeStampsFor` 3150 символов на месте, `_mazeStamps` 0, `_mazeSpec` null.
/// Import при этом молча уходил на разбор символов и строил уровень БЕЗ инверсии, вложенности и
/// крепления — то есть ровно то, от чего мы уходили.
///
/// ⚠️ И это была не разовая неудача: тем же способом терялись `_mazeStamps` всё время, что они
/// существуют. Достаточно было тронуть любой скрипт между «Сгенерировать» и «Import».
///
/// Лежит в Library — рядом с проектом, но вне версионирования: файл производный, его не хранят.
/// </summary>
public static class LevelSpecIO
{
    /// <summary>Куда кладём последнее сгенерированное описание.</summary>
    public static string DefaultPath
    {
        get
        {
            string dir = System.IO.Path.Combine(
                System.IO.Path.GetDirectoryName(Application.dataPath), "Library", "ClimbUp");
            System.IO.Directory.CreateDirectory(dir);
            return System.IO.Path.Combine(dir, "last_generated_spec.json");
        }
    }

    public static bool Save(LevelSpec spec, string path)
    {
        if (spec == null) return false;
        try { System.IO.File.WriteAllText(path, JsonUtility.ToJson(ToDto(spec))); return true; }
        catch (System.Exception e) { Debug.LogWarning("[LevelSpecIO] Не сохранилось: " + e.Message); return false; }
    }

    public static LevelSpec Load(string path)
    {
        try
        {
            if (!System.IO.File.Exists(path)) return null;
            var dto = JsonUtility.FromJson<SpecDto>(System.IO.File.ReadAllText(path));
            return dto == null ? null : FromDto(dto);
        }
        catch (System.Exception e) { Debug.LogWarning("[LevelSpecIO] Не прочиталось: " + e.Message); return null; }
    }

    // ── DTO: то, что умеет JsonUtility. HashSet и вложенные словари она не тянет, поэтому списки ──
    [System.Serializable] private class SpecDto
    {
        public float cell; public bool hasSpawn; public Vector2Int spawn;
        public List<Vector2Int> rock = new List<Vector2Int>();
        public List<Vector2Int> coins = new List<Vector2Int>();
        /// <summary>Шипы. ⚠️ Без них сохранённое описание теряет смертельные клетки, и уровень,
        /// собранный из файла, отличается от того, что проверила приёмка.</summary>
        public List<Vector2Int> spikes = new List<Vector2Int>();
        public List<GroupDto> groups = new List<GroupDto>();
        public TargetDto finish = new TargetDto();
        public List<TargetDto> artifacts = new List<TargetDto>();
        public List<TargetDto> checkpoints = new List<TargetDto>();
        /// <summary>Схема, к которой это описание относится. Ключ от подмены: если в поле редактора
        /// лежит уже другой текст, описание не про него.</summary>
        public string schemeKey = "";
    }

    [System.Serializable] private class GroupDto
    {
        public string id; public bool inverted; public float window;
        public List<Vector2Int> tiles = new List<Vector2Int>();
        public List<ButtonDto> buttons = new List<ButtonDto>();
    }

    [System.Serializable] private class ButtonDto
    {
        public Vector2Int cell; public Vector2 center, half; public int host; public int mount;
    }

    [System.Serializable] private class TargetDto
    {
        public bool exists; public Vector2Int cell; public Vector2 center, half; public Vector3 world;
    }

    private static SpecDto ToDto(LevelSpec s)
    {
        var d = new SpecDto { cell = s.cell, hasSpawn = s.hasSpawn, spawn = s.spawn };
        foreach (var k in s.rock) d.rock.Add(k);
        d.coins.AddRange(s.coins);
        d.spikes.AddRange(s.spikes);
        foreach (var g in s.groups)
        {
            var gd = new GroupDto { id = g.id, inverted = g.inverted, window = g.window };
            gd.tiles.AddRange(g.tiles);
            foreach (var b in g.buttons)
                gd.buttons.Add(new ButtonDto
                { cell = b.cell, center = b.center, half = b.half, host = b.host, mount = (int)b.mount });
            d.groups.Add(gd);
        }
        d.finish = T(s.finish);
        foreach (var a in s.artifacts) d.artifacts.Add(T(a));
        foreach (var c in s.checkpoints) d.checkpoints.Add(T(c));
        return d;
    }

    private static TargetDto T(LevelTarget t) => new TargetDto
    { exists = t.exists, cell = t.cell, center = t.center, half = t.half, world = t.world };

    private static LevelTarget T(TargetDto t) => new LevelTarget
    { exists = t.exists, cell = t.cell, center = t.center, half = t.half, world = t.world };

    private static LevelSpec FromDto(SpecDto d)
    {
        var s = new LevelSpec { cell = d.cell, hasSpawn = d.hasSpawn, spawn = d.spawn };
        foreach (var k in d.rock) s.rock.Add(k);
        s.coins.AddRange(d.coins);
        if (d.spikes != null) s.spikes.AddRange(d.spikes);
        foreach (var g in d.groups)
        {
            var gg = new LevelGroup { id = g.id, inverted = g.inverted, window = g.window };
            gg.tiles.AddRange(g.tiles);
            foreach (var b in g.buttons)
                gg.buttons.Add(new LevelButton
                { cell = b.cell, center = b.center, half = b.half, host = b.host, mount = (MountSide)b.mount });
            s.groups.Add(gg);
        }
        s.finish = T(d.finish);
        foreach (var a in d.artifacts) s.artifacts.Add(T(a));
        foreach (var c in d.checkpoints) s.checkpoints.Add(T(c));
        return s;
    }

    /// <summary>Сохранить описание вместе с ключом-схемой.</summary>
    public static bool SaveFor(LevelSpec spec, string schemeKey)
    {
        if (spec == null) return false;
        try
        {
            var dto = ToDto(spec); dto.schemeKey = schemeKey ?? "";
            System.IO.File.WriteAllText(DefaultPath, JsonUtility.ToJson(dto));
            return true;
        }
        catch (System.Exception e) { Debug.LogWarning("[LevelSpecIO] Не сохранилось: " + e.Message); return false; }
    }

    /// <summary>Прочитать описание, если оно относится ИМЕННО к этой схеме. Иначе null.</summary>
    public static LevelSpec LoadFor(string schemeKey)
    {
        try
        {
            if (!System.IO.File.Exists(DefaultPath)) return null;
            var dto = JsonUtility.FromJson<SpecDto>(System.IO.File.ReadAllText(DefaultPath));
            if (dto == null || dto.schemeKey != (schemeKey ?? "")) return null;
            return FromDto(dto);
        }
        catch (System.Exception e) { Debug.LogWarning("[LevelSpecIO] Не прочиталось: " + e.Message); return null; }
    }
}
