using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ⭐ КАМЕРА — ручной паттерн игрока из Level_11, разобранный и обобщённый.
///
/// Что в оригинале (прочитано не по ASCII, а по самим объектам уровня):
///   • группа A — ПОТОЛОК камеры, 7 тайлов, обычная: нажал снаружи — потолок исчез, провалился внутрь;
///   • группа B — платформа внутри камеры, 8 тайлов, обычная;
///   • ⭐ кнопка группы C — ДОЧЕРНИЙ ОБЪЕКТ платформы B: она физически едет на исчезающей платформе;
///   • группа C — стенка кармана, в котором лежит ключ;
///   • группы D и E — инверсные боковые стены (появляются по нажатию и могут запереть).
///
/// Что здесь сделано ИНАЧЕ и почему. Платформа B у игрока обычная, то есть кнопка на ней доступна
/// сразу, а держится всё на ТАЙМЕРАХ (окна 3-5 секунд). Модель проходимости времени не знает, поэтому
/// такую связку она сочла бы тремя независимыми кнопками и не увидела бы головоломки. Здесь B сделана
/// ИНВЕРСНОЙ: платформы нет, пока не нажата её кнопка. Тогда цепочка A → B → C → ключ становится
/// зависимостью по построению, и приёмка её видит. Когда появится модель времени, B можно вернуть к
/// оригинальному поведению — паттерн от этого не изменится, изменится только способ проверки.
///
/// ⚠️ Модуль НЕ копирует уровень: ширина камеры, высота, сторона кармана, длина полки и положение
/// кнопок берутся из места и из рандома. Копия одного уровня — это не паттерн.
/// </summary>
public class VaultModule : IMazeModule
{
    public static int StampedCount, RolledBackCount;
    /// <summary>Счётчик отказов ПО ПРИЧИНАМ. 🐞 Без него я три раза подряд ослаблял проверки наугад,
    /// каждый раз промахиваясь: откатов было видно много, а какой именно срабатывает — нет.</summary>
    public static readonly int[] FailBy = new int[10];
    public static readonly string[] FailName =
    { "карман не в камне", "полка упирается в потолок", "негде кнопка потолка",
      "негде кнопка полки", "негде кнопка кармана", "нет бокового проёма",
      "проём занят чужим", "негде кнопка выхода", "зажим", "—" };
    public static void ResetStats()
    { StampedCount = RolledBackCount = 0; for (int i = 0; i < FailBy.Length; i++) FailBy[i] = 0; }

    private static List<ModuleStamp> Fail(MazeCanvas c, int why)
    { c.Rollback(); RolledBackCount++; FailBy[why]++; return null; }

    public string Name => "камера с ключом";

    /// <summary>Сколько рядов нужно камере: пол, полка на дотяжке над ним и ряд под кнопку на полке.
    /// При Climb=3 это ровно 5 — обычная комната, зал не требуется.</summary>
    public const int MinChamberHeight = MazeCanvas.Climb + 2;
    /// <summary>Ширина: карман + стенка + полка, и всё это не впритык.</summary>
    public const int MinChamberWidth = 7;

    public bool Fits(MazeCanvas c, MazeSite s)
    {
        int room = s.roomId;
        if (c.H(room) < MinChamberHeight || c.W(room) < MinChamberWidth) return false;
        if (s.buttonAbove.x < 0) return false;              // кнопке потолка негде встать снаружи
        // ⚠️ ПОТОЛОК МОЖЕТ БЫТЬ УЖЕ ПРОРЕЗАН — И ЭТО НОРМАЛЬНО. Связь между комнатами прорезает в нём
        // люк ещё в рендере. 🐞 Сперва я требовал сплошной камень и камера не строилась НИ РАЗУ
        // (заказана 8 раз, встала 0). Камера сама себе дверь: люк она закрывает своей группой, и
        // проход открывается только кнопкой. Нельзя лишь занимать чужие тайлы.
        int ceil = c.R0(room) - 1;
        for (int x = c.C0(room); x < c.C0(room) + c.W(room); x++)
        {
            char ch = c.At(ceil, x);
            if (ch != '#' && ch != '.') return false;      // там уже чей-то механизм
        }
        return true;
    }

    /// <summary>
    /// Возвращает ТРИ штампа: потолок, полка, стенка кармана. ⚠️ Список, а не один штамп: паттерн —
    /// это связка групп, и по одной их ставить нельзя, иначе половина ляжет, а половина откатится.
    /// </summary>
    public List<ModuleStamp> StampAll(MazeCanvas c, MazeSite s, out Vector2Int keyCell)
    {
        keyCell = new Vector2Int(-1, -1);
        if (!Fits(c, s)) return null;

        int room = s.roomId;
        int col0 = c.C0(room), w = c.W(room), row0 = c.R0(room);
        int floorRow = c.FloorRow(room), airRow = c.AirRow(room);
        int ceilRow = row0 - 1;

        char gCeil = c.NextGroupId(), gLedge = c.NextGroupId(), gPocket = c.NextGroupId();

        c.Begin();

        // ── ПОТОЛОК: исчезает по кнопке снаружи, игрок проваливается внутрь ──
        // Края потолка оставляем камнем: комната сверху должна на чём-то держаться.
        int ceilFrom = col0 + 1, ceilTo = col0 + w - 2;
        for (int x = ceilFrom; x <= ceilTo; x++) c.Set(ceilRow, x, char.ToLower(gCeil));

        // ── КАРМАН С КЛЮЧОМ: ниша в стене камеры, запертая стенкой группы ──
        bool pocketLeft = c.Rng.Next(2) == 0;
        int pocketCol = pocketLeft ? col0 : col0 + w - 1;    // столбец-стенка внутри камеры
        // ⚠️⚠️ КАРМАН ОБЯЗАН БЫТЬ ГЛУХИМ. 🐞 Я рыл его наружу, не проверяя, что снаружи камень, — и
        // ниша иногда вскрывалась в соседнее пространство. Ключ тогда брался в обход стенки, и ВСЯ
        // камера становилась декоративной («декоративные [B,C,D]» на всех трёх уровнях замера).
        // ⚠️ Но и требовать толстую оболочку нельзя: с карманом в две клетки и запасом в клетку
        // модуль откатывался на КАЖДОЙ постройке. Ниша в одну клетку, и вокруг неё камень.
        int pocketDepth = 1;
        int digDir = pocketLeft ? -1 : 1;                    // куда рыть карман сквозь стену
        for (int d = 1; d <= pocketDepth + 1; d++)
        for (int r = airRow - 2; r <= airRow + 1; r++)
            if (c.At(r, pocketCol + digDir * d) != '#') { return Fail(c, 0); }
        for (int d = 1; d <= pocketDepth; d++)
        for (int r = airRow - 1; r <= airRow; r++)
            c.Set(r, pocketCol + digDir * d, '.');
        for (int r = airRow - 1; r <= airRow; r++) c.Set(r, pocketCol, char.ToLower(gPocket));
        // Пол под карманом, иначе ключ висит над пустотой.
        for (int d = 0; d <= pocketDepth; d++) c.Set(airRow + 1, pocketCol + digDir * d, '#');
        keyCell = new Vector2Int(pocketCol + digDir * pocketDepth, airRow);

        // ── ПОЛКА: инверсная платформа в дотяжке над полом, на ней поедет кнопка кармана ──
        int ledgeRow = airRow - MazeCanvas.Climb;
        if (ledgeRow <= row0) { return Fail(c, 1); }
        int ledgeLen = Mathf.Clamp(2 + c.Rng.Next(2), 2, w - 3);
        int ledgeCol = pocketLeft ? col0 + w - 1 - ledgeLen : col0 + 1;
        for (int k = 0; k < ledgeLen; k++) c.Set(ledgeRow, ledgeCol + k, char.ToLower(gLedge));

        // ── КНОПКИ ──
        // Потолка — снаружи, в комнате сверху: только оттуда его и открывают.
        // 🐞 Кнопку мало добавить в штамп — её надо ПОЛОЖИТЬ В СЕТКУ. Забыл, и группа потолка вышла
        // без кнопки вовсе: модель честно объявила её мёртвой, а всю камеру — декоративной.
        var btnCeil = s.buttonAbove;
        if (c.At(btnCeil.y, btnCeil.x) != '.') { return Fail(c, 2); }
        c.Set(btnCeil.y, btnCeil.x, gCeil);
        // Полки — на полу камеры: игрок падает внутрь и первым делом видит её.
        int btnLedgeCol = pocketLeft ? col0 + w - 2 : col0 + 1;
        if (c.At(airRow, btnLedgeCol) != '.') { return Fail(c, 3); }
        c.Set(airRow, btnLedgeCol, gLedge);
        // ⭐ Кнопка кармана СТОИТ НА ПОЛКЕ — ровно как в оригинале, где триггер C припаркован к
        // платформе B. Пока полки нет, до кнопки не дотянуться: это и есть вложенность.
        int btnPocketCol = ledgeCol + ledgeLen / 2;
        if (c.At(ledgeRow - 1, btnPocketCol) != '.') { return Fail(c, 4); }
        c.Set(ledgeRow - 1, btnPocketCol, gPocket);

        // ⚠️ ВЫХОДА ЗДЕСЬ НЕТ, И ЭТО НАМЕРЕННО. Камера лишь ОБЪЯВЛЯЕТ, что запирает игрока
        // (SpaceNeed.needsExit), а выход ставится ОТДЕЛЬНЫМ элементом на ребре назад — тем, который
        // выберет словарь (PuzzleVocabulary.CanBeExit): сегодня дверь-инверсия или мост, завтра любой
        // новый паттерн, умеющий вернуть игрока.
        // 🐞 В первой версии выход был вшит сюда четвёртой группой — и тем закрывал дорогу всем
        // остальным способам, хотя мост назад к маршруту ничем не хуже стены. Поправлено игроком.

        // Зажимы: правка большая, проверяем всю камеру с запасом.
        for (int r = ceilRow - 1; r <= floorRow + 1; r++)
        for (int x = col0 - pocketDepth - 2; x <= col0 + w + pocketDepth + 1; x++)
            if (c.MakesPinch(r, x)) { return Fail(c, 8); }

        c.Commit(); StampedCount++;

        // ⚠️⚠️ СМЫСЛ ФЛАГА inverted (проверено по DisappearingPlatform, а не по догадке):
        //   inverted = false — покоя НЕТ платформы, кнопка делает её твёрдой (платформа ПОЯВЛЯЕТСЯ);
        //   inverted = true  — платформа твёрдая, кнопка УБИРАЕТ её на окно.
        // 🐞 Я прочитал флаг наоборот и выставил полке inverted = true: она стояла с самого начала,
        // кнопка кармана была доступна сразу, никакой вложенности не возникало — и модель справедливо
        // объявила ВСЮ камеру декоративной. Поправлено игроком, он автор оригинального паттерна.
        var stCeil = new ModuleStamp
        { moduleName = Name + ": потолок", groupId = gCeil, inverted = true,
          gates = "провал в камеру" };
        stCeil.AddButton(btnCeil);

        var stLedge = new ModuleStamp
        { moduleName = Name + ": полка", groupId = gLedge, inverted = false,
          gates = "подъём к кнопке кармана" };
        stLedge.AddButton(new Vector2Int(btnLedgeCol, airRow));

        var stPocket = new ModuleStamp
        { moduleName = Name + ": карман", groupId = gPocket, inverted = true,
          gates = "ниша с ключом" };
        // Хозяин кнопки — полка: без неё до кнопки не добраться.
        stPocket.AddButton(new Vector2Int(btnPocketCol, ledgeRow - 1), gLedge);

        return new List<ModuleStamp> { stCeil, stLedge, stPocket };
    }

    /// <summary>Одиночный штамп интерфейсу не годится — паттерн ставится только целиком.</summary>
    public ModuleStamp Stamp(MazeCanvas c, MazeSite s) => null;
}
