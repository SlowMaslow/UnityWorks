using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ⭐ ХОЛСТ ЛАБИРИНТА — то, во что модули-головоломки штампуют свои правки.
/// Оборачивает состояние генератора (сетку символов, разметку комнат, флаги декора) и даёт модулям
/// ровно те операции, что им нужны, вместо доступа ко всему подряд.
///
/// Массивы НЕ копируются: холст держит те же ссылки, что и генератор, поэтому правки видны обеим
/// сторонам. Это осознанно — переписывать 900 строк генератора ради модулей было бы неоправданным
/// риском, а так модули въезжают в него по одному.
///
/// Заведён 2026-09-02: генератор перестаёт быть черновиком и становится продуктовой фичей
/// (дейли-уровни), а значит механизмы надо не зашивать намертво, а собирать из кирпичей.
/// </summary>
public class MazeCanvas
{
    public readonly char[,] G;
    public readonly int Rows, Cols;
    public readonly int CW, CH;
    public readonly bool[,] Bump;      // декор, который можно снести
    public readonly bool[,] NoBump;    // сюда декор ставить нельзя (посадочные холды у люков)
    public readonly bool[,] NoFill;    // сюда нельзя досыпать камень (стволы шахт)
    public readonly System.Random Rng;

    /// <summary>
    /// ⭐ ЗАПАС К ДОТЯЖКЕ. Строим подъёмы в 3 ряда, хотя замеренный предел — тоже 3: генератор обязан
    /// строить С ЗАПАСОМ, а не впритык. Плейтест игрока (2026-07-18): подъём в РОВНО 4 ряда на
    /// висящую ступеньку физически не берётся. Отсюда же ограничение на высоту комнат H ≤ Climb+2.
    /// </summary>
    public const int Climb = 3;

    /// <summary>
    /// ⭐ БОКОВАЯ ДОТЯЖКА В КЛЕТКАХ (замер: вбок ≤6). Модулям она нужна не для украшения, а чтобы
    /// их преграда ВООБЩЕ БЫЛА преградой: разрыв уже дотяжки игрок просто перетягивает руками.
    /// </summary>
    public const int ReachSide = 6;

    private readonly int[] _colX, _rowY, _colW, _rowH;
    private readonly List<(int r, int c, char ch)> _journal = new List<(int, int, char)>();
    private bool _recording;
    private int _nextGroup;            // 0 → 'A'

    public MazeCanvas(char[,] g, int rows, int cols, int cw, int ch,
                      int[] colX, int[] rowY, int[] colW, int[] rowH,
                      bool[,] bump, bool[,] noBump, bool[,] noFill, System.Random rng)
    {
        G = g; Rows = rows; Cols = cols; CW = cw; CH = ch;
        _colX = colX; _rowY = rowY; _colW = colW; _rowH = rowH;
        Bump = bump; NoBump = noBump; NoFill = noFill; Rng = rng;
    }

    // ─── Сетка ────────────────────────────────────────────────────────────────
    public char At(int r, int c) => (r >= 0 && r < Rows && c >= 0 && c < Cols) ? G[r, c] : '#';

    public void Set(int r, int c, char ch)
    {
        if (r < 0 || r >= Rows || c < 0 || c >= Cols) return;
        if (_recording) _journal.Add((r, c, G[r, c]));
        G[r, c] = ch;
    }

    // ─── Комнаты ──────────────────────────────────────────────────────────────
    public int C0(int cx) => _colX[cx];                 // левый столбец интерьера
    public int R0(int cy) => _rowY[CH - 1 - cy];        // верхний ряд интерьера (cy=0 — низ)
    public int W(int cx) => _colW[cx];
    public int H(int cy) => _rowH[cy];
    public int FloorRow(int cy) => R0(cy) + H(cy);      // ряд-СТЕНА под комнатой = её пол
    public int AirRow(int cy) => R0(cy) + H(cy) - 1;    // нижний ряд ВОЗДУХА в комнате

    // ─── Журнал: правка модуля либо ложится целиком, либо откатывается ────────
    /// <summary>
    /// ⚠️ Откат обязателен: штамп идёт ПОСЛЕ расшивки зажимов и сам может их создать (убранный пол
    /// встречается со сталактитом комнаты снизу). Частично применённый модуль хуже, чем никакого.
    /// </summary>
    public void Begin() { _journal.Clear(); _recording = true; }
    public void Commit() { _recording = false; _journal.Clear(); }
    public void Rollback()
    {
        for (int i = _journal.Count - 1; i >= 0; i--)
        { var e = _journal[i]; G[e.r, e.c] = e.ch; }
        _recording = false; _journal.Clear();
    }

    /// <summary>Следующий свободный ключ группы: A, B, C…</summary>
    public char NextGroupId() => (char)('A' + _nextGroup++);
    public int GroupsUsed => _nextGroup;
    /// <summary>Синхронизация с группами, заведёнными вне модулей (пока такие ещё есть).</summary>
    public void SetNextGroupIndex(int i) { _nextGroup = i; }

    /// <summary>
    /// Диагональный зажим: два камня углом к углу, между ними пэдам не пролезть.
    /// Проверяется вокруг только что изменённой клетки.
    /// </summary>
    public bool MakesPinch(int r, int c)
    {
        for (int dr = -1; dr <= 0; dr++)
        for (int dc = -1; dc <= 0; dc++)
        {
            int r0 = r + dr, c0 = c + dc;
            bool tl = At(r0, c0) == '#', tr = At(r0, c0 + 1) == '#';
            bool bl = At(r0 + 1, c0) == '#', br = At(r0 + 1, c0 + 1) == '#';
            if ((tl && br && !tr && !bl) || (tr && bl && !tl && !br)) return true;
        }
        return false;
    }

    /// <summary>Свободная клетка воздуха НАД полом комнаты — куда можно поставить кнопку/объект.</summary>
    public int FindFloorSpot(int cy, int cx, int preferFromMid = 0)
    {
        int air = AirRow(cy), floor = FloorRow(cy), w = W(cx), c0 = C0(cx);
        for (int d = 0; d < w; d++)
        {
            int mid = w / 2;
            int off = mid + (d % 2 == 0 ? d / 2 : -(d / 2 + 1));
            if (off < 0 || off >= w) continue;
            int c = c0 + off;
            if (At(air, c) == '.' && At(floor, c) == '#') return c;
        }
        return -1;
    }
}

/// <summary>Место-кандидат под модуль: комната (или пара комнат по вертикали) и её контекст.</summary>
public class MazeSite
{
    public Vector2Int room;            // комната, в которую штампуем
    public Vector2Int roomAbove;       // для вертикальных связок (иначе (-1,-1))
    public bool onRoute;               // лежит ли на маршруте спавн→финиш
    public bool hasFloorBelow;         // есть ли под комнатой этаж (падение не смертельно)
    public bool throughCorridor;       // сквозной горизонтальный коридор (вход слева, выход справа)

    // ── Шахта между room и roomAbove (для вертикальных ворот) ──
    public int shaftCol0 = -1, shaftWidth, shaftStepRow;
    // Куда встанут кнопки: (столбец, ряд). Считает ГЕНЕРАТОР — место под кнопку зависит от того,
    // относится ли клетка к основной области воздуха, а это его знание, не модуля.
    public Vector2Int buttonBelow = new Vector2Int(-1, -1), buttonAbove = new Vector2Int(-1, -1);

    // ── Дверной проём между двумя комнатами по горизонтали (для двери-инверсии) ──
    public int doorCol = -1, doorRowTop, doorHeight;

    /// <summary>
    /// ⭐ Нужна ПОЛКА ПОД ВЛОЖЕННУЮ КНОПКУ: на платформу этого механизма сядет кнопка следующего.
    /// 🐞 Без отдельной полки кнопка занимала клетку НАД ступенькой — то есть ровно то место, куда
    /// игрок ставит пэд, вставая на неё (поймано игроком на первом же импорте: «кнопка мешает
    /// поставить туда пэд»). Поэтому ступенька расширяется на одну колонку, кнопка садится на
    /// пристройку, а хваты остаются свободными.
    /// </summary>
    public bool wantButtonShelf;
}

/// <summary>Что модуль пообещал компоновщику: какую группу он завёл и что она держит.</summary>
public class ModuleStamp
{
    public string moduleName;
    public char groupId;
    public bool inverted;
    public string gates;               // человекочитаемо: что именно закрыто этой группой
    public List<Vector2Int> buttons = new List<Vector2Int>();   // (столбец, ряд) в координатах сетки
    /// <summary>
    /// ⭐ ХОЗЯИН для каждой кнопки из <see cref="buttons"/> (тот же индекс): '\0' — кнопка сама по себе,
    /// иначе ключ группы, ВНУТРИ которой она лежит. Вложенная кнопка полупрозрачна и не нажимается,
    /// пока платформы хозяина не появились.
    /// ⚠️ Сетка символов этого выразить не может (в ASCII только «a-z тайлы, A-Z кнопки»), а вложенность
    /// — это ИЕРАРХИЯ. Поэтому едет отдельным каналом, как и инверсия. Тот, кто читает схему и забудет
    /// про этот канал, получит уровень, где заведомо запертая кнопка считается вечно доступной.
    /// </summary>
    public List<char> buttonHosts = new List<char>();

    /// <summary>Единственный способ добавить кнопку: два списка иначе разъезжаются.</summary>
    public void AddButton(Vector2Int cell, char host = '\0')
    { buttons.Add(cell); buttonHosts.Add(host); }

    /// <summary>Клетка ВОЗДУХА, куда можно посадить вложенную кнопку следующего механизма: над
    /// пристройкой к платформе. (-1,-1) — полки нет, вложить в этот механизм нельзя.</summary>
    public Vector2Int shelfCell = new Vector2Int(-1, -1);

    /// <summary>Хозяин кнопки по её клетке ('\0' — своя).</summary>
    public char HostOf(Vector2Int cell)
    {
        for (int i = 0; i < buttons.Count; i++)
            if (buttons[i] == cell) return i < buttonHosts.Count ? buttonHosts[i] : '\0';
        return '\0';
    }
}

/// <summary>
/// ⭐ КИРПИЧ ГОЛОВОЛОМКИ. Модуль не изобретает геометрию с нуля: он получает комнату известного
/// размера с известными стенами и вписывает в неё заранее продуманную правку, локальная проходимость
/// которой гарантирована ПО ПОСТРОЕНИЮ (все подъёмы ≤ MazeClimb).
///
/// Ответственность модуля кончается на его месте. За то, что цепочка обязательна и уровень решаем,
/// отвечают компоновщик и проверка моделью после сборки.
/// </summary>
public interface IMazeModule
{
    string Name { get; }
    /// <summary>Подходит ли место: быстрая проверка предусловий БЕЗ правок сетки.</summary>
    bool Fits(MazeCanvas canvas, MazeSite site);
    /// <summary>Штамп. Возвращает null, если по ходу дела место оказалось негодным (правки откатывает сам).</summary>
    ModuleStamp Stamp(MazeCanvas canvas, MazeSite site);
}

/// <summary>
/// МОСТ НА ТАЙМЕРЕ (идея игрока): у сквозного коридора убирается ПОЛ, а на его место кладутся
/// платформы группы. Нажал кнопку — пол появился, перебежал; не успел — провалился.
///
/// ⚠️ Мост обязан ИМЕТЬ СМЫСЛ: комната должна быть СКВОЗНЫМ горизонтальным коридором и лежать на
/// маршруте, иначе его просто обойдут (фидбэк игрока). Эти два факта знает генератор, он и приносит
/// их в <see cref="MazeSite"/>; модуль проверяет только геометрию своего места.
/// Цена ошибки двух видов: под комнатой есть этаж — падаешь в него и возвращаешься в обход;
/// комнаты снизу нет — прорезаем оболочку вниз, и падение становится смертельным.
/// </summary>
public class TimedBridgeModule : IMazeModule
{
    /// <summary>Сколько раз штамп лёг и сколько раз откатился — статистика для отладки генератора
    /// (и доказательство, что модуль вообще исполняется, а не молчит).</summary>
    public static int StampedCount, RolledBackCount;

    public string Name => "мост на таймере";

    public bool Fits(MazeCanvas c, MazeSite s)
    {
        if (!s.throughCorridor) return false;
        // ⚠️ ПРОЛЁТ ОБЯЗАН БЫТЬ ШИРЕ ДОТЯЖКИ, иначе моста нет вовсе. Опоры стоят по краям комнаты, то
        // есть разрыв между ними = W−1 клеток. При W 5..7 это 4..6 — ровно в пределах боковой дотяжки
        // (≤6), и игрок ПЕРЕТЯГИВАЕТСЯ через пропасть руками, пол ему не нужен.
        // 🐞 Так и было: приёмка браковала КАЖДУЮ схему с мостом (16 из 16 на замере 7×6) с вердиктом
        // «группа декоративная», а генератор молча перебрасывал сид, пока мост не исчезнет. Мосты
        // пропали из выдачи давно и незаметно — нашлось только когда компоновщик стал печатать замысел.
        if (c.W(s.room.x) < MazeCanvas.ReachSide + 2) return false;
        int floorRow = c.FloorRow(s.room.y);
        int c0 = c.C0(s.room.x) + 1, c1 = c.C0(s.room.x) + c.W(s.room.x) - 2;
        for (int x = c0; x <= c1; x++) if (c.At(floorRow, x) != '#') return false;
        return true;
    }

    public ModuleStamp Stamp(MazeCanvas c, MazeSite s)
    {
        if (!Fits(c, s)) return null;
        int floorRow = c.FloorRow(s.room.y);
        int cLeft = c.C0(s.room.x), cRight = c.C0(s.room.x) + c.W(s.room.x) - 1;
        int c0 = cLeft + 1, c1 = cRight - 1, rAir = floorRow - 1;
        char grp = c.NextGroupId();

        c.Begin();
        // Декор, стоявший НА полу пролёта: иначе повиснет в воздухе над мостом и даст зажим.
        for (int x = c0; x <= c1; x++)
            if (c.Bump[rAir, x]) { c.Set(rAir, x, '.'); c.Bump[rAir, x] = false; }
        for (int x = c0; x <= c1; x++) c.Set(floorRow, x, char.ToLower(grp));
        if (!s.hasFloorBelow)                                    // ПРОПАСТЬ: режем оболочку вниз
            for (int x = c0; x <= c1; x++)
            for (int r = floorRow + 1; r < c.Rows; r++)
                if (c.At(r, x) == '#') c.Set(r, x, ' '); else break;

        bool b1 = c.At(rAir, cLeft) == '.' && c.At(floorRow, cLeft) == '#';
        bool b2 = c.At(rAir, cRight) == '.' && c.At(floorRow, cRight) == '#';
        if (b1) c.Set(rAir, cLeft, grp);
        if (b2) c.Set(rAir, cRight, grp);

        bool pinched = false;
        for (int r = floorRow - 2; r <= floorRow + 2 && !pinched; r++)
        for (int x = c0 - 2; x <= c1 + 2 && !pinched; x++)
            if (c.MakesPinch(r, x)) pinched = true;

        // Кнопки нужны с ОБЕИХ сторон: иначе, перебежав, игрок не сможет вернуться тем же путём.
        if (!b1 || !b2 || pinched) { c.Rollback(); RolledBackCount++; return null; }
        c.Commit(); StampedCount++;
        var st = new ModuleStamp
        { moduleName = Name, groupId = grp, inverted = false, gates = "пол сквозного коридора" };
        st.AddButton(new Vector2Int(cLeft, rAir));
        st.AddButton(new Vector2Int(cRight, rAir));
        return st;
    }
}

/// <summary>
/// ВЕРТИКАЛЬНЫЕ ВОРОТА: ступенька, по которой лезут из нижней комнаты в верхнюю, делается платформой
/// группы. Кнопка внизу — «открыть», кнопка наверху — «вернуться» (без неё игрок, поднявшись, не
/// смог бы спуститься тем же путём).
///
/// ⚠️ ВЫСОТА СТУПЕНЬКИ ВЫСТРАДАНА ПЛЕЙТЕСТОМ. Платформа ставится ровно в <see cref="MazeCanvas.Climb"/>
/// рядов над полом (под ней 2 пустых ряда): подъём пол→платформа = 3, платформа→пол верхней комнаты
/// = H−2 ≤ 3 при H ≤ 5. Лепить её вплотную к земле нельзя — при climb=2 ступенька висела в одной
/// клетке над полом, это бессмысленно и некрасиво (фидбэк игрока).
///
/// Разрез дерева (что именно заперто, остаётся ли старт снизу, есть ли за воротами ключ или финиш) —
/// знание ГЕНЕРАТОРА: он проверяет это до вызова и приносит уже отобранное место.
/// </summary>
public class VerticalGateModule : IMazeModule
{
    public static int StampedCount, RolledBackCount;

    public string Name => "вертикальные ворота";

    public bool Fits(MazeCanvas c, MazeSite s)
    {
        if (s.shaftWidth <= 0 || s.shaftCol0 < 0) return false;
        if (c.H(s.room.y) < 4) return false;                       // при H=3 долезут и без ступеньки
        if (s.buttonBelow.x < 0 || s.buttonAbove.x < 0) return false;   // некуда поставить пару кнопок
        int stepRow = c.FloorRow(s.room.y) - MazeCanvas.Climb;
        return stepRow > c.R0(s.room.y);                           // иначе упрётся в потолок
    }

    public ModuleStamp Stamp(MazeCanvas c, MazeSite s)
    {
        if (!Fits(c, s)) return null;
        int stepRow = c.FloorRow(s.room.y) - MazeCanvas.Climb;

        // Под платформой нужны 2 пустых ряда. Мешать может ДЕКОР комнаты (тумба/пилон) — его сносим;
        // если под платформой настоящий камень, эта шахта не годится, а не «прилепим платформу ниже».
        bool groundBusy = false;
        var toClear = new List<Vector2Int>();
        for (int k = 0; k < s.shaftWidth && !groundBusy; k++)
        for (int dr = 1; dr <= 2; dr++)
        {
            int col = s.shaftCol0 + k, r2 = stepRow + dr;
            if (c.At(r2, col) != '#') continue;
            if (c.Bump[r2, col]) toClear.Add(new Vector2Int(col, r2));
            else { groundBusy = true; break; }
        }
        if (groundBusy) { RolledBackCount++; return null; }

        // ⚠️ ЧАСТИЧНЫЙ ОТКАЗ ЗДЕСЬ УНАСЛЕДОВАН ОТ СТАРОГО КОДА: если снос декора рождает зажим, мы
        // возвращаем ТОЛЬКО эту клетку, а снесённое ранее так и остаётся снесённым. Оставлено ровно
        // как было, чтобы перенос в модуль можно было проверить побайтовым совпадением схем.
        // Это несогласованность (модуль обязан откатываться целиком) — чинить отдельной правкой,
        // со своей проверкой, а не заодно.
        foreach (var p in toClear)
        {
            c.Set(p.y, p.x, '.');
            if (c.MakesPinch(p.y, p.x)) { c.Set(p.y, p.x, '#'); groundBusy = true; break; }
            c.Bump[p.y, p.x] = false;
        }
        if (groundBusy) { RolledBackCount++; return null; }

        char grp = c.NextGroupId();
        for (int k = 0; k < s.shaftWidth; k++)
        {
            int col = s.shaftCol0 + k;
            if (s.shaftStepRow != stepRow && c.At(s.shaftStepRow, col) == '#')
                c.Set(s.shaftStepRow, col, '.');                  // убрать старую ступеньку
            if (c.At(stepRow, col) == '.' || c.At(stepRow, col) == '#')
                c.Set(stepRow, col, char.ToLower(grp));
        }
        c.Set(s.buttonBelow.y, s.buttonBelow.x, grp);             // «открыть»
        c.Set(s.buttonAbove.y, s.buttonAbove.x, grp);             // «вернуться»

        var st = new ModuleStamp
        { moduleName = Name, groupId = grp, inverted = false, gates = "подъём в верхнюю комнату" };

        // ── ПОЛКА ПОД ВЛОЖЕННУЮ КНОПКУ ────────────────────────────────────────────────────────
        // Пристраиваем к ступеньке ОДНУ колонку сбоку и сажаем кнопку над ней. Сама ступенька при этом
        // остаётся свободной под хваты — ради этого всё и делается.
        // ⚠️ Ступеньке нельзя расползаться на всю ширину комнаты: так уже запечатывались целые этажи.
        // Поэтому пристройка только внутрь интерьера и только если хотя бы одна колонка комнаты
        // останется незанятой.
        if (s.wantButtonShelf)
        {
            int c0 = c.C0(s.room.x), w = c.W(s.room.x);
            for (int side = 0; side < 2 && st.shelfCell.x < 0; side++)
            {
                int col = side == 0 ? s.shaftCol0 + s.shaftWidth : s.shaftCol0 - 1;
                if (col < c0 || col > c0 + w - 1) continue;             // не вылезаем из комнаты
                if (s.shaftWidth + 1 >= w) continue;                    // ступенька заняла бы всю ширину
                if (c.At(stepRow, col) != '.') continue;                // место занято камнем/декором
                if (c.At(stepRow - 1, col) != '.') continue;            // кнопке нужен воздух над полкой
                c.Set(stepRow, col, char.ToLower(grp));
                if (c.MakesPinch(stepRow, col)) { c.Set(stepRow, col, '.'); continue; }
                st.shelfCell = new Vector2Int(col, stepRow - 1);
            }
            // ⚠️ Полка не вышла — ворота всё равно годные, просто вложить в них нельзя. Отменять весь
            // штамп нельзя: этот модуль не ведёт журнал (см. пометку про унаследованный частичный
            // откат), и «отказ» после записи ступеньки оставил бы в схеме тайлы без группы. Пусть
            // вложенный механизм не найдёт площадку и уйдёт на перепланировку — этот путь уже есть.
        }

        StampedCount++;
        st.AddButton(s.buttonBelow);
        st.AddButton(s.buttonAbove);
        return st;
    }
}

/// <summary>
/// ДВЕРЬ-ИНВЕРСИЯ — первый модуль на механике, которой генератор раньше не умел (2026-09-02).
/// Дверной проём между двумя комнатами ЗАКЛАДЫВАЕТСЯ платформами ИНВЕРСНОЙ группы: по умолчанию это
/// сплошная стена, нажатие кнопки убирает её на окно. Не «построй мост и беги», а «открой проход и
/// успей пройти».
///
/// ⚠️ Кнопки ставим с ОБЕИХ сторон. Инверсная дверь закрывается сама, и с одной кнопкой игрок,
/// перейдя на ту сторону, оказался бы заперт — генератор не имеет права такое выдавать.
/// ⚠️ Стоять в проёме, когда камень возвращается, смертельно (crushOnReturn) — но это честно:
/// перед возвратом группа предупреждает вибрацией.
/// </summary>
public class InvertedDoorModule : IMazeModule
{
    public static int StampedCount, RolledBackCount;

    public string Name => "дверь-инверсия";

    public bool Fits(MazeCanvas c, MazeSite s)
    {
        if (s.doorCol < 0 || s.doorHeight < 2) return false;      // проём в одну клетку — не дверь
        if (s.buttonBelow.x < 0 || s.buttonAbove.x < 0) return false;
        for (int r = s.doorRowTop; r < s.doorRowTop + s.doorHeight; r++)
            if (c.At(r, s.doorCol) != '.') return false;          // проём должен быть чистым воздухом
        return true;
    }

    public ModuleStamp Stamp(MazeCanvas c, MazeSite s)
    {
        if (!Fits(c, s)) { RolledBackCount++; return null; }
        char grp = c.NextGroupId();
        c.Begin();
        for (int r = s.doorRowTop; r < s.doorRowTop + s.doorHeight; r++)
            c.Set(r, s.doorCol, char.ToLower(grp));
        c.Set(s.buttonBelow.y, s.buttonBelow.x, grp);             // кнопка со стороны старта
        c.Set(s.buttonAbove.y, s.buttonAbove.x, grp);             // кнопка с той стороны — на обратный путь

        // Заложенный проём не должен породить диагональный зажим с соседним камнем.
        bool pinched = false;
        for (int r = s.doorRowTop - 1; r <= s.doorRowTop + s.doorHeight && !pinched; r++)
        for (int dc = -1; dc <= 1 && !pinched; dc++)
            if (c.MakesPinch(r, s.doorCol + dc)) pinched = true;
        if (pinched) { c.Rollback(); RolledBackCount++; return null; }

        c.Commit(); StampedCount++;
        var st = new ModuleStamp
        { moduleName = Name, groupId = grp, inverted = true, gates = "дверной проём между комнатами" };
        st.AddButton(s.buttonBelow);
        st.AddButton(s.buttonAbove);
        return st;
    }
}
