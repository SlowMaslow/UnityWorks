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
    /// <summary>⭐ НОМЕР ГРУППЫ В КАЖДОЙ КЛЕТКЕ, −1 — ничья. Это и есть личность плитки; буква в
    /// <see cref="G"/> рядом — только картинка (см. NextGroup).</summary>
    public readonly int[,] Grp;
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

    /// <summary>
    /// ⭐ СИМВОЛ ШИПОВ В СЕТКЕ. Выбран из свободных: '#' камень, '.' воздух, '@' спавн, '^' финиш,
    /// '*' артефакт, '=' чекпоинт, '$' монета, буквы — группы. Восклицательный знак не занят ничем
    /// и читается как опасность.
    /// </summary>
    public const char Spike = '!';

    /// <summary>
    /// Глубина ямы под пролётом моста в рядах воздуха (дно с шипами идёт следующим рядом).
    /// Три ряда — чуть больше дотяжки вверх (Climb = 3): назад из ямы уже не выбраться, но на
    /// высоту уровня она почти не влияет. Раньше на её месте была дыра до самого дна сетки.
    /// </summary>
    public const int PitDepth = 3;


    /// <summary>Прямоугольник комнаты в СЕТКЕ СИМВОЛОВ: (col0, row0) — левый ВЕРХНИЙ угол интерьера.</summary>
    public struct RoomRect { public int col0, row0, w, h; }

    /// <summary>
    /// ⭐ КОМНАТЫ ХРАНЯТСЯ ПРЯМОУГОЛЬНИКАМИ ПО ID, а не как пересечение колонки и строки.
    ///
    /// Это тот самый переход, ради которого заводилась <see cref="RoomLayout"/>: на решётке размер
    /// комнаты был `colW[cx] × rowH[cy]`, то есть общий для всей колонки и всей строки, и комната не
    /// могла быть большой сама по себе. Модули при этом обращались к холсту по (cx,cy).
    ///
    /// ⚠️ ОБА ГЕНЕРАТОРА КОРМЯТ ОДИН И ТОТ ЖЕ API. Решётчатый просто выкладывает сюда прямоугольники,
    /// посчитанные из своей сетки (id = cy*CW + cx), свободный — из раскладки по дереву. Модулям всё
    /// равно, кто их вызвал, и переносить их дважды не пришлось.
    /// </summary>
    private readonly RoomRect[] _rooms;

    private readonly int[] _colX, _rowY, _colW, _rowH;
    private readonly List<(int r, int c, char ch, int grp)> _journal = new List<(int, int, char, int)>();
    private bool _recording;
    private int _nextGroup;            // 0 → 'A'

    public MazeCanvas(char[,] g, int rows, int cols, int cw, int ch,
                      int[] colX, int[] rowY, int[] colW, int[] rowH,
                      bool[,] bump, bool[,] noBump, bool[,] noFill, System.Random rng)
    {
        G = g; Rows = rows; Cols = cols; CW = cw; CH = ch;
        Grp = NewGrpPlane(rows, cols);
        _colX = colX; _rowY = rowY; _colW = colW; _rowH = rowH;
        Bump = bump; NoBump = noBump; NoFill = noFill; Rng = rng;
        // Решётка выкладывает свои комнаты прямоугольниками: id = cy*CW + cx.
        _rooms = new RoomRect[cw * ch];
        for (int cx = 0; cx < cw; cx++)
        for (int cy = 0; cy < ch; cy++)
            _rooms[cy * cw + cx] = new RoomRect
            { col0 = colX[cx], row0 = rowY[ch - 1 - cy], w = colW[cx], h = rowH[cy] };
    }

    /// <summary>Холст для генератора БЕЗ решётки: комнаты приходят готовыми прямоугольниками.</summary>
    public MazeCanvas(char[,] g, int rows, int cols, RoomRect[] rooms,
                      bool[,] bump, bool[,] noBump, bool[,] noFill, System.Random rng)
    {
        G = g; Rows = rows; Cols = cols; CW = 0; CH = 0;
        Grp = NewGrpPlane(rows, cols);
        _rooms = rooms;
        Bump = bump; NoBump = noBump; NoFill = noFill; Rng = rng;
    }

    private static int[,] NewGrpPlane(int rows, int cols)
    {
        var p = new int[rows, cols];
        for (int r = 0; r < rows; r++) for (int c = 0; c < cols; c++) p[r, c] = -1;
        return p;
    }

    /// <summary>Номер комнаты решётки — чтобы старый генератор мог назвать её так же, как модули.</summary>
    public int RoomId(int cx, int cy) => cy * CW + cx;

    // ─── Сетка ────────────────────────────────────────────────────────────────
    public char At(int r, int c) => (r >= 0 && r < Rows && c >= 0 && c < Cols) ? G[r, c] : '#';

    public void Set(int r, int c, char ch)
    {
        if (r < 0 || r >= Rows || c < 0 || c >= Cols) return;
        if (_recording) _journal.Add((r, c, G[r, c], Grp[r, c]));
        G[r, c] = ch; Grp[r, c] = -1;
    }

    /// <summary>
    /// ⭐ ЕДИНСТВЕННЫЙ СПОСОБ ПОЛОЖИТЬ ПЛИТКУ ГРУППЫ. Пишет номер в <see cref="Grp"/> и букву в
    /// сетку. 🐞 Раньше модули писали просто <c>Set(r, c, char.ToLower(grp))</c>, и личность группы
    /// существовала ТОЛЬКО в виде буквы — отсюда потолок в 26 и потерянные группы.
    /// </summary>
    public void SetGroup(int r, int c, int groupIx)
    {
        if (r < 0 || r >= Rows || c < 0 || c >= Cols) return;
        if (_recording) _journal.Add((r, c, G[r, c], Grp[r, c]));
        G[r, c] = TileChar(groupIx); Grp[r, c] = groupIx;
    }

    /// <summary>
    /// ⭐ ПОЛОЖИТЬ КНОПКУ группы. ⚠️ Номер в плоскость НЕ пишется, и это существенно: плоскость
    /// отвечает на вопрос «чья тут ПЛИТКА», а кнопка — не плитка. Запиши сюда номер — и BuildSpec
    /// соберёт клетку кнопки в список платформ группы.
    /// </summary>
    public void SetButton(int r, int c, int groupIx)
    {
        if (r < 0 || r >= Rows || c < 0 || c >= Cols) return;
        if (_recording) _journal.Add((r, c, G[r, c], Grp[r, c]));
        G[r, c] = ButtonChar(groupIx); Grp[r, c] = -1;
    }

    /// <summary>Номер группы в клетке; за краем и на «ничьей» клетке — −1.</summary>
    public int GrpAt(int r, int c) => (r >= 0 && r < Rows && c >= 0 && c < Cols) ? Grp[r, c] : -1;

    // ─── Комнаты ──────────────────────────────────────────────────────────────
    // ⚠️ ВСЕ ЭТИ МЕТОДЫ ТЕПЕРЬ ПРИНИМАЮТ id КОМНАТЫ, а не индекс колонки/строки. Раньше размер был
    // пересечением общей ширины колонки и общей высоты строки — из-за этого комната не могла быть
    // большой сама по себе, и «заказ геометрии» упирался в потолок трижды подряд.
    public int C0(int room) => _rooms[room].col0;       // левый столбец интерьера
    public int R0(int room) => _rooms[room].row0;       // верхний ряд интерьера
    public int W(int room)  => _rooms[room].w;
    public int H(int room)  => _rooms[room].h;
    public int FloorRow(int room) => R0(room) + H(room);   // ряд-СТЕНА под комнатой = её пол
    public int AirRow(int room)   => R0(room) + H(room) - 1; // нижний ряд ВОЗДУХА в комнате

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
        { var e = _journal[i]; G[e.r, e.c] = e.ch; Grp[e.r, e.c] = e.grp; }
        _recording = false; _journal.Clear();
    }

    /// <summary>
    /// ⭐⭐ ЛИЧНОСТЬ ГРУППЫ — ЭТО НОМЕР, А НЕ БУКВА В СЕТКЕ.
    ///
    /// 🐞 БЫЛО: <c>NextGroupId() => (char)('A' + _nextGroup++)</c>, плитки рисовались той же буквой
    /// в нижнем регистре, а собирались обратно сканом диапазона 'a'..'z'. Это молчаливый потолок в
    /// 26 групп, который нигде не объявлен: 27-я получала '[', у него НЕТ регистра (char.ToLower('[')
    /// это '['), плитка и кнопка становились одним символом, а скан такую клетку не видел вовсе.
    /// Группа уезжала в уровень с ПУСТЫМ списком плиток — кнопка есть, нажимать нечего.
    /// Замер при заказе 50 механизмов: мёртвых групп с ключом за 'Z' — 17, среди букв A-Z — НИ ОДНОЙ.
    ///
    /// ⛔ ПОПЫТКА ПОЧИНИТЬ АЛФАВИТОМ ОТВЕРГНУТА. Продолжить латиницу кириллицей технически можно
    /// (48 пар с настоящим регистром, char.ToLower работает), но кириллические а е о р с у х
    /// НЕОТЛИЧИМЫ на вид от латинских a e o p c y x. Схему читают и правят глазами — дамп из
    /// смеси омоглифов не правится руками вообще. Опасение игрока было верным.
    ///
    /// ⭐ РЕШЕНИЕ: разнести личность и картинку. Номер группы живёт в <see cref="Grp"/> — отдельной
    /// плоскости той же формы, что и сетка. Буква в сетке остаётся ЧИСТО ДЛЯ ГЛАЗ и спокойно
    /// повторяется по кругу (a-z), потому что по ней больше никто ничего не опознаёт. Потолок групп
    /// теперь один и объявленный: 63 бита маски в LevelModel.
    /// </summary>
    public int NextGroup() => _nextGroup++;

    /// <summary>Буква для ГЛАЗ: после 'z' начинается сначала. Опознавать по ней ничего нельзя.</summary>
    public static char TileChar(int groupIx) => (char)('a' + ((groupIx % 26) + 26) % 26);
    /// <summary>Та же буква в верхнем регистре — так кнопка выглядит в дампе схемы.</summary>
    public static char ButtonChar(int groupIx) => (char)('A' + ((groupIx % 26) + 26) % 26);

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
    public int FindFloorSpot(int room, int preferFromMid = 0)
    {
        int air = AirRow(room), floor = FloorRow(room), w = W(room), c0 = C0(room);
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
    public Vector2Int room;            // комната в координатах ДЕРЕВА (для отладки и логов)
    public Vector2Int roomAbove;       // для вертикальных связок (иначе (-1,-1))
    /// <summary>⭐ ГЕОМЕТРИЯ БЕРЁТСЯ ПО ЭТИМ id, а не по (cx,cy): комнаты теперь прямоугольники
    /// произвольного размера, и решётка — лишь один из способов их получить.</summary>
    public int roomId = -1, roomAboveId = -1;
    public bool onRoute;               // лежит ли на маршруте спавн→финиш

    /// <summary>
    /// ⭐⭐ НУЖНА ЛИ ВОЗВРАТНАЯ КНОПКА — та, что стоит ЗА механизмом и пускает игрока обратно.
    ///
    /// ⭐ ОСНОВНОЙ МАРШРУТ ОДНОСТОРОННИЙ ПО ЗАМЫСЛУ (решение игрока 2026-09-11): шаг назад по
    /// трассе не гарантируется, точка невозврата — это приём, а не поломка. Значит на маршруте
    /// возвратная кнопка не нужна вовсе, а это 145 механизмов из 185 по замеру.
    /// Гарантию проходимости несёт НЕ механизм, а ВЕТКА: она уходит с трассы, забирает ключ и
    /// возвращается на трассу дальше по ходу. Поэтому на ветке возврат пока оставляем — сегодня
    /// петлёй помечены 4 ветки из 31, остальные 27 ключей лежат в тупиках, и выйти оттуда можно
    /// только тем же механизмом.
    ///
    /// ⛔ ДВЕ ГИПОТЕЗЫ ПРОВЕРЕНЫ И ОПРОВЕРГНУТЫ (2026-09-11), не повторять:
    ///   • «одна кнопка у ВСЕХ механизмов» — чистых 0 уровней из 10, непроходимых 10. Сторона
    ///     кнопки при этом была верной (мёртвых групп ноль, цели достижимы порознь) — ломалось
    ///     ровно одно: не существует прохождения за один заход;
    ///   • «воротам вторая кнопка не нужна, спуститься можно всегда» — чистых 1 из 10,
    ///     безвыходных клеток 801 против 40-129. Спуск в шахту без вызванных платформ модель
    ///     проходом не считает.
    /// Оба раза причина одна: механизм сидит на ЕДИНСТВЕННОЙ связи между двумя частями дерева.
    /// Снимать возврат можно там, где обход гарантирован чем-то ещё, — на трассе это её
    /// односторонность, на ветке это будет замкнутая петля.
    /// </summary>
    public bool NeedsReturnButton => !onRoute;
    public bool hasFloorBelow;         // есть ли под комнатой этаж (падение не смертельно)
    public bool throughCorridor;       // сквозной горизонтальный коридор (вход слева, выход справа)

    // ── Шахта между room и roomAbove (для вертикальных ворот) ──
    public int shaftCol0 = -1, shaftWidth, shaftStepRow;
    // Куда встанут кнопки: (столбец, ряд). Считает ГЕНЕРАТОР — место под кнопку зависит от того,
    // относится ли клетка к основной области воздуха, а это его знание, не модуля.
    public Vector2Int buttonBelow = new Vector2Int(-1, -1), buttonAbove = new Vector2Int(-1, -1);
    /// <summary>Плоскость крепления каждой из двух кнопок. По умолчанию пол — как было всегда.</summary>
    public MountSide mountBelow = MountSide.Floor, mountAbove = MountSide.Floor;

    // ── Дверной проём между двумя комнатами по горизонтали (для двери-инверсии) ──
    public int doorCol = -1, doorRowTop, doorHeight;

    /// <summary>
    /// ⭐⭐ ГОРИЗОНТАЛЬНАЯ ДВЕРЬ: барьер лежит в ПОЛУ/ПОТОЛКЕ между комнатами друг над другом.
    /// <c>doorRow</c> — ряд-стена, <c>doorCol0..doorCol0+doorWidth-1</c> — прорезанный люк; −1 —
    /// дверь вертикальная (см. <see cref="doorCol"/>).
    ///
    /// 🐞 Дверь умела резаться ТОЛЬКО вертикальной щелью в боковой стене, и это молча ограничивало
    /// всё вокруг. Ближайшее следствие: ребро возврата петли искали лишь среди боковых соседей, а
    /// ветка возвращается к трассе как придётся. Замер: из 23 помеченных петель боковое соседство
    /// с маршрутом было у 9, только вертикальное — у 2.
    /// Игрок: «дверь должна резаться абсолютно в любой ориентации, хоть сверху, хоть снизу».
    /// </summary>
    public int doorRow = -1, doorCol0, doorWidth;

    // ── ЛЮК в полу верхней комнаты (для люка-провала): сам ряд и его колонки ──
    public int hatchRow = -1, hatchCol0, hatchWidth;

    /// <summary>
    /// ⭐ Нужна ПОЛКА ПОД ВЛОЖЕННУЮ КНОПКУ: на платформу этого механизма сядет кнопка следующего.
    /// 🐞 Без отдельной полки кнопка занимала клетку НАД ступенькой — то есть ровно то место, куда
    /// игрок ставит пэд, вставая на неё (поймано игроком на первом же импорте: «кнопка мешает
    /// поставить туда пэд»). Поэтому ступенька расширяется на одну колонку, кнопка садится на
    /// пристройку, а хваты остаются свободными.
    /// </summary>
    public bool wantButtonShelf;

    /// <summary>
    /// ⭐ РОЛЬ «ВЫХОД»: проход открывается ТОЛЬКО ИЗНУТРИ замкнутой области. Кнопка ставится с одной
    /// стороны — со стороны запертого, — и снаружи войти этим путём нельзя.
    ///
    /// ⚠️ Это свойство РОЛИ, а не элемента: та же дверь-инверсия в середине маршрута обязана иметь
    /// кнопки с обеих сторон (иначе игрок, перейдя, не вернётся). 🐞 Если выпустить выход с двумя
    /// кнопками, в камеру войдут сбоку мимо потолка, и потолок станет декоративным — модель это
    /// поймает, но уровень к тому времени уже собран впустую.
    /// </summary>
    public bool exitOnly;

    /// <summary>Какая из сторон прохода — ВНУТРИ запертой области (для <see cref="exitOnly"/>).</summary>
    public bool insideIsBelow;

    /// <summary>
    /// ⭐ С КАКОЙ СТОРОНЫ ИГРОК ПРИХОДИТ в сквозной коридор: 1 — слева, 0 — справа, −1 — неизвестно.
    /// Нужна мосту при одной кнопке: поставить её не с той стороны значит оставить кнопку ЗА
    /// пропастью, и мост станет непроходим вовсе. У ворот и двери этот вопрос решает buttonBelow
    /// (он считается по комнате родителя), а мост живёт внутри ОДНОЙ комнаты и сторон не знает.
    /// </summary>
    public int entryOnLeft = -1;
}

/// <summary>Что модуль пообещал компоновщику: какую группу он завёл и что она держит.</summary>
public class ModuleStamp
{
    public string moduleName;
    /// <summary>⭐ НОМЕР группы — её личность. Буква в сетке повторяется по кругу и ничего не
    /// опознаёт (см. MazeCanvas.NextGroup).</summary>
    public int groupIx = -1;
    /// <summary>Буква ДЛЯ ЛОГА И ГЛАЗ, та же, что легла в сетку. По ней ничего не ищут.</summary>
    public char groupId => MazeCanvas.TileChar(groupIx);
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
    public List<int> buttonHosts = new List<int>();
    /// <summary>Сторона крепления каждой кнопки (тот же индекс). Ось, независимая от вложенности:
    /// «на чём держится» и «внутри какой группы лежит» — разные вопросы.</summary>
    public List<MountSide> buttonMounts = new List<MountSide>();

    /// <summary>Окно активности группы в секундах — свойство группы, а не клетки.</summary>
    public float window = 5f;
    /// <summary>Это КЛАПАН ВОЗВРАТА — дверь из ветки на маршрут, кнопка только изнутри.</summary>
    public bool returnValve;

    /// <summary>Ребро дерева комнат, которое этот механизм запирает: (родитель, ребёнок). (-1,-1) —
    /// механизм не привязан к ребру (трафарет, клапан). Нужно для разбора холостых групп: без этой
    /// связи «какая группа на каком ребре» приходится каждый раз восстанавливать по порядку списка.</summary>
    public Vector2Int edgeRooms = new Vector2Int(-1, -1);

    /// <summary>Единственный способ добавить кнопку: списки иначе разъезжаются.</summary>
    public void AddButton(Vector2Int cell, int host = -1, MountSide mount = MountSide.Floor)
    { buttons.Add(cell); buttonHosts.Add(host); buttonMounts.Add(mount); }

    /// <summary>Сторона крепления кнопки по её клетке (по умолчанию пол).</summary>
    public MountSide MountOf(Vector2Int cell)
    {
        for (int i = 0; i < buttons.Count; i++)
            if (buttons[i] == cell) return i < buttonMounts.Count ? buttonMounts[i] : MountSide.Floor;
        return MountSide.Floor;
    }

    /// <summary>Клетка ВОЗДУХА, куда можно посадить вложенную кнопку следующего механизма: над
    /// пристройкой к платформе. (-1,-1) — полки нет, вложить в этот механизм нельзя.</summary>
    public Vector2Int shelfCell = new Vector2Int(-1, -1);

    /// <summary>Хозяин кнопки по её клетке (−1 — своя).</summary>
    public int HostOf(Vector2Int cell)
    {
        for (int i = 0; i < buttons.Count; i++)
            if (buttons[i] == cell) return i < buttonHosts.Count ? buttonHosts[i] : -1;
        return -1;
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
        if (c.W(s.roomId) < MazeCanvas.ReachSide + 2) return false;
        int floorRow = c.FloorRow(s.roomId);
        int c0 = c.C0(s.roomId) + 1, c1 = c.C0(s.roomId) + c.W(s.roomId) - 2;
        for (int x = c0; x <= c1; x++) if (c.At(floorRow, x) != '#') return false;
        return true;
    }

    public ModuleStamp Stamp(MazeCanvas c, MazeSite s)
    {
        if (!Fits(c, s)) return null;
        int floorRow = c.FloorRow(s.roomId);
        int cLeft = c.C0(s.roomId), cRight = c.C0(s.roomId) + c.W(s.roomId) - 1;
        int c0 = cLeft + 1, c1 = cRight - 1, rAir = floorRow - 1;
        int grp = c.NextGroup();

        c.Begin();
        // Декор, стоявший НА полу пролёта: иначе повиснет в воздухе над мостом и даст зажим.
        for (int x = c0; x <= c1; x++)
            if (c.Bump[rAir, x]) { c.Set(rAir, x, '.'); c.Bump[rAir, x] = false; }
        for (int x = c0; x <= c1; x++) c.SetGroup(floorRow, x, grp);
        // ⭐⭐ ЯМА С ШИПАМИ ВМЕСТО ПРОПАСТИ ДО ДНА.
        // 🐞 Здесь было: «if (!s.hasFloorBelow) режем оболочку вниз до c.Rows». Смерть в игре была
        // ровно одна — FallCollider внизу уровня, — поэтому смертельное падение приходилось делать
        // дырой НАСКВОЗЬ ВСЕГО УРОВНЯ. Замер на Level_12: под одним мостом шахта в 72 ряда при
        // высоте уровня 113, то есть две трети холста уходили в пустоту ради одной смерти. Игрок:
        // «падение очень далеко в пустоту, ради того чтобы игрок получил смерть».
        // Теперь под пролётом короткая яма, дно устлано шипами (см. LevelSpec.spikes). Наказание то
        // же — падение смертельно, — а стоит оно PitDepth рядов вместо всей высоты уровня.
        // ⚠️ Условие оставлено прежним: когда этаж снизу ЕСТЬ, копать нельзя — яма пробила бы потолок
        // комнаты под мостом. Там цена ошибки как была: провалился в комнату, вернулся в обход.
        // ⚠️⚠️ КОПАЕМ СТРОГО ПО КАМНЮ И ОСТАНАВЛИВАЕМСЯ НА ПЕРВОЙ ЧУЖОЙ КЛЕТКЕ — ровно то правило,
        // что было у старого кода («if (c.At(r, x) == '#') ... else break»). 🐞 Первая версия ямы
        // рыла вслепую на всю глубину и подкладывала снизу камень: замер на тех же сидах дал
        // чистых 5 из 10 вместо 8, замурованные карманы на 346, 58 и 41 клетку и недобор ключей.
        // Причина простая: подложенный камень запечатывал чужую полость, а слепой раскоп вскрывал
        // потолок тому, что лежало под мостом. Здесь мы ТОЛЬКО убираем камень и ничего не добавляем,
        // поэтому запечатать что-либо физически нечем.
        if (!s.hasFloorBelow)
            for (int x = c0; x <= c1; x++)
            {
                int dug = 0;
                for (int r = floorRow + 1; r <= floorRow + MazeCanvas.PitDepth && r < c.Rows; r++)
                {
                    if (c.At(r, x) != '#') break;      // упёрлись в чужую полость — дальше не наше
                    c.Set(r, x, '.'); dug++;
                }
                // Дно ямы — последний вынутый ряд. Так шип всегда лежит на том, что было камнем,
                // и под ним остаётся нетронутая порода: яма читается ямой, а не полоской в воздухе.
                if (dug > 0) c.Set(floorRow + dug, x, MazeCanvas.Spike);
            }

        bool b1 = c.At(rAir, cLeft) == '.' && c.At(floorRow, cLeft) == '#';
        bool b2 = c.At(rAir, cRight) == '.' && c.At(floorRow, cRight) == '#';
        // ⚠️ Обе клетки обязаны ГОДИТЬСЯ и при одной кнопке: ставим одну, но выбирать сторону можно
        // только когда годны обе. Иначе кнопка уедет за пропасть.
        bool keepLeft  = s.NeedsReturnButton || s.entryOnLeft != 0;
        bool keepRight = s.NeedsReturnButton || s.entryOnLeft == 0;
        if (b1 && keepLeft)  c.SetButton(rAir, cLeft, grp);
        if (b2 && keepRight) c.SetButton(rAir, cRight, grp);

        bool pinched = false;
        for (int r = floorRow - 2; r <= floorRow + 2 && !pinched; r++)
        for (int x = c0 - 2; x <= c1 + 2 && !pinched; x++)
            if (c.MakesPinch(r, x)) pinched = true;

        // Кнопки нужны с ОБЕИХ сторон: иначе, перебежав, игрок не сможет вернуться тем же путём.
        if (!b1 || !b2 || pinched) { c.Rollback(); RolledBackCount++; return null; }
        c.Commit(); StampedCount++;
        var st = new ModuleStamp
        { moduleName = Name, groupIx = grp, inverted = false, gates = "пол сквозного коридора" };
        if (keepLeft)  st.AddButton(new Vector2Int(cLeft, rAir));
        if (keepRight) st.AddButton(new Vector2Int(cRight, rAir));
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
    /// <summary>Почему ворота не встали: 0 нет шахты, 1 низкая комната, 2 некуда кнопки,
    /// 3 ступенька упрётся в потолок, 4 не нашлось рядов под уступы, 5 уступ вышел огрызком.
    /// Без разбивки «не встал ворота» — один счётчик на пять разных болезней.</summary>
    public static readonly int[] FailWhy = new int[6];

    public string Name => "вертикальные ворота";

    public bool Fits(MazeCanvas c, MazeSite s)
    {
        if (s.shaftWidth <= 0 || s.shaftCol0 < 0) { FailWhy[0]++; return false; }
        if (c.H(s.roomId) < 4) { FailWhy[1]++; return false; }      // при H=3 долезут и без ступеньки
        if (s.buttonBelow.x < 0 || s.buttonAbove.x < 0) { FailWhy[2]++; return false; }   // некуда кнопки
        int stepRow = c.FloorRow(s.roomId) - MazeCanvas.Climb;
        if (stepRow <= c.R0(s.roomId)) { FailWhy[3]++; return false; }   // иначе упрётся в потолок
        return true;
    }

    /// <summary>
    /// ⭐⭐ ЗИГЗАГ ВМЕСТО ОДНОЙ СТУПЕНЬКИ — форма считается ПОД МЕСТО, а не берётся из заготовки.
    ///
    /// Почему: разбор ручного Level_09 показал, что игрок одной кнопкой вызывает ЛЕСТНИЦУ ИЗ
    /// НЕСКОЛЬКИХ УСТУПОВ ВРАЗБЕЖКУ, и одна и та же механика выглядит по-разному просто потому, что
    /// разложена в разных по размеру комнатах (3×5, 5×5, 8×5 — одна стратегия, три облика).
    /// А мой модуль всегда клал ОДНУ ступеньку в 2-3 тайла — отсюда и «уровни на одно лицо»:
    /// композицию рецепт уже разнообразил, а каждый отдельный паттерн рендерился одинаково.
    ///
    /// Стратегия: уступы через <c>step</c> рядов (2 или 3), стороны чередуются, длина уступа
    /// подбирается так, чтобы соседние уступы были В ПРЕДЕЛАХ ДОТЯЖКИ. Заготовок нет — есть правило
    /// и место, поэтому вариантов столько, сколько бывает комнат.
    ///
    /// ⚠️ ГРАНИЦЫ НЕ ВЫДУМАНЫ, А ЗАМЕРЕНЫ: подъём ≤ Climb рядов, вбок ≤ ReachSide, сумма ≤ 7.
    /// Переход между уступами разных сторон = (W − 2·len + 1) вбок при <c>step</c> вверх, отсюда и
    /// нижняя граница длины уступа. Не помещается — откатываемся к одному уступу, как было.
    /// </summary>
    public ModuleStamp Stamp(MazeCanvas c, MazeSite s)
    {
        if (!Fits(c, s)) return null;
        int floorRow = c.FloorRow(s.roomId);
        int ceilRow  = c.R0(s.roomId) - 1;
        int w = c.W(s.roomId), col0 = c.C0(s.roomId);

        int step = 2 + c.Rng.Next(2);                              // 2 или 3 ряда между уступами
        // Соседние уступы на разных сторонах: вбок между ними W−2·len+1, вверх step.
        // Требуем dx ≤ ReachSide и dx+dy ≤ 7 — отсюда минимальная длина уступа.
        int minLen = Mathf.Max(2, Mathf.CeilToInt((w + step - (LevelModel.ReachSumCells - step)) / 2f));
        int maxLen = Mathf.Max(2, w - 2);                          // во всю ширину нельзя: запечатает комнату
        if (minLen > maxLen) minLen = maxLen;
        int len = minLen + c.Rng.Next(maxLen - minLen + 1);

        // ⚠️ НИЖНИЙ УСТУП НЕ ВПЛОТНУЮ К ПОЛУ. Иначе он ложится в самый нижний ряд воздуха и это уже не
        // уступ, а бугор на полу: лезть по нему некуда, а выглядит как мусор. Старый код требовал два
        // пустых ряда под платформой ровно поэтому (фидбэк игрока про «ступеньку в одной клетке над
        // землёй»), и при переходе на зигзаг это правило чуть не потерялось.
        var rowsOfLedges = new List<int>();
        for (int r = ceilRow + step; r <= floorRow - 2; r += step) rowsOfLedges.Add(r);
        if (rowsOfLedges.Count == 0) { RolledBackCount++; FailWhy[4]++; return null; }

        // Верхний уступ ставим у той стороны, к которой ближе люк, — с него игрок и вылезает наверх.
        bool topLeft = (s.shaftCol0 - col0) <= (col0 + w - 1 - (s.shaftCol0 + s.shaftWidth - 1));

        c.Begin();
        int grp = c.NextGroup();

        // Старую каменную ступеньку под люком убираем: её место занимает зигзаг.
        for (int k = 0; k < s.shaftWidth; k++)
        {
            int col = s.shaftCol0 + k;
            if (c.At(s.shaftStepRow, col) == '#') c.Set(s.shaftStepRow, col, '.');
        }

        int placedLedges = 0, topRow = rowsOfLedges[0], topStart = col0;
        for (int i = 0; i < rowsOfLedges.Count; i++)
        {
            bool left = (i % 2 == 0) == topLeft;
            int start = left ? col0 : col0 + w - len;
            int cellsHere = 0;
            for (int k = 0; k < len; k++)
            {
                int col = start + k, r = rowsOfLedges[i];
                char at = c.At(r, col);
                if (at == '.') { c.SetGroup(r, col, grp); cellsHere++; }
                else if (at == '#' && c.Bump[r, col])              // декор сносим, настоящий камень — нет
                { c.SetGroup(r, col, grp); c.Bump[r, col] = false; cellsHere++; }
            }
            if (cellsHere < 2) continue;                           // огрызок уступа никому не нужен
            placedLedges++;
            if (placedLedges == 1) { topRow = rowsOfLedges[i]; topStart = start; }
        }
        if (placedLedges == 0) { c.Rollback(); RolledBackCount++; FailWhy[5]++; return null; }

        c.SetButton(s.buttonBelow.y, s.buttonBelow.x, grp);             // «открыть» — сторона прихода
        if (s.NeedsReturnButton)
            c.SetButton(s.buttonAbove.y, s.buttonAbove.x, grp);         // «вернуться» — за замком

        var st = new ModuleStamp
        { moduleName = Name, groupIx = grp, inverted = false,
          gates = "подъём в верхнюю комнату (" + placedLedges + " уступ(ов) через " + step + ")" };

        // ── ПОЛКА ПОД ВЛОЖЕННУЮ КНОПКУ ────────────────────────────────────────────────────────
        // Пристраиваем к ступеньке ОДНУ колонку сбоку и сажаем кнопку над ней. Сама ступенька при этом
        // остаётся свободной под хваты — ради этого всё и делается.
        // ⚠️ Ступеньке нельзя расползаться на всю ширину комнаты: так уже запечатывались целые этажи.
        // Поэтому пристройка только внутрь интерьера и только если хотя бы одна колонка комнаты
        // останется незанятой.
        if (s.wantButtonShelf)
        {
            // Пристраиваем колонку к ВЕРХНЕМУ уступу — именно на нём игрок стоит перед выходом наверх.
            for (int side = 0; side < 2 && st.shelfCell.x < 0; side++)
            {
                int col = side == 0 ? topStart + len : topStart - 1;
                if (col < col0 || col > col0 + w - 1) continue;         // не вылезаем из комнаты
                if (len + 1 >= w) continue;                             // уступ занял бы всю ширину
                if (c.At(topRow, col) != '.') continue;                 // место занято камнем/декором
                if (c.At(topRow - 1, col) != '.') continue;             // кнопке нужен воздух над полкой
                c.SetGroup(topRow, col, grp);
                if (c.MakesPinch(topRow, col)) { c.Set(topRow, col, '.'); continue; }
                st.shelfCell = new Vector2Int(col, topRow - 1);
            }
            // ⚠️ Полка не вышла — ворота всё равно годные, просто вложить в них нельзя.
            // Весь штамп из-за этого не отменяем: зигзаг сам по себе рабочий.
        }

        c.Commit(); StampedCount++;
        st.AddButton(s.buttonBelow);
        if (s.NeedsReturnButton) st.AddButton(s.buttonAbove);
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
        if (s.buttonBelow.x < 0 || s.buttonAbove.x < 0) return false;
        if (s.doorRow >= 0)                                        // ГОРИЗОНТАЛЬНАЯ: барьер в полу
        {
            // ⚠️ ОБЫЧНОМУ проходу нужен люк в две клетки, КЛАПАНУ ВОЗВРАТА хватает одной: сквозь него
            // проваливаются ровно один раз, и это люк, а не коридор. 🐞 Замер: одиннадцать веток из
            // двенадцати не замыкались только потому, что промахивались мимо комнаты маршрута на
            // одну колонку, — порог был написан не про клапан.
            if (s.doorWidth < (s.exitOnly ? 1 : 2)) return false;
            for (int x = s.doorCol0; x < s.doorCol0 + s.doorWidth; x++)
                if (c.At(s.doorRow, x) != '.') return false;
            return true;
        }
        if (s.doorCol < 0 || s.doorHeight < 2) return false;       // проём в одну клетку — не дверь
        for (int r = s.doorRowTop; r < s.doorRowTop + s.doorHeight; r++)
            if (c.At(r, s.doorCol) != '.') return false;           // проём должен быть чистым воздухом
        return true;
    }

    public ModuleStamp Stamp(MazeCanvas c, MazeSite s)
    {
        if (!Fits(c, s)) { RolledBackCount++; return null; }
        int grp = c.NextGroup();
        bool horizontal = s.doorRow >= 0;
        c.Begin();
        if (horizontal)
            for (int x = s.doorCol0; x < s.doorCol0 + s.doorWidth; x++) c.SetGroup(s.doorRow, x, grp);
        else
            for (int r = s.doorRowTop; r < s.doorRowTop + s.doorHeight; r++) c.SetGroup(r, s.doorCol, grp);
        // ⭐ В РОЛИ ВЫХОДА кнопка ставится ТОЛЬКО ИЗНУТРИ: снаружи этим путём не войти, и вход в
        // область остаётся единственным — тем, ради которого она и запиралась.
        var inside = s.insideIsBelow ? s.buttonBelow : s.buttonAbove;
        var outside = s.insideIsBelow ? s.buttonAbove : s.buttonBelow;
        c.SetButton(inside.y, inside.x, grp);
        if (!s.exitOnly && s.NeedsReturnButton)
            c.SetButton(outside.y, outside.x, grp);                     // возвратная, за дверью

        // Заложенный проём не должен породить диагональный зажим с соседним камнем.
        bool pinched = false;
        if (horizontal)
        {
            for (int dr = -1; dr <= 1 && !pinched; dr++)
            for (int x = s.doorCol0 - 1; x <= s.doorCol0 + s.doorWidth && !pinched; x++)
                if (c.MakesPinch(s.doorRow + dr, x)) pinched = true;
        }
        else
        {
            for (int r = s.doorRowTop - 1; r <= s.doorRowTop + s.doorHeight && !pinched; r++)
            for (int dc = -1; dc <= 1 && !pinched; dc++)
                if (c.MakesPinch(r, s.doorCol + dc)) pinched = true;
        }
        if (pinched) { c.Rollback(); RolledBackCount++; return null; }

        c.Commit(); StampedCount++;
        var st = new ModuleStamp
        { moduleName = s.exitOnly ? Name + ": выход изнутри" : Name,
          groupIx = grp, inverted = true,
          gates = s.exitOnly
              ? (horizontal ? "выход из запертой области (люк)" : "выход из запертой области")
              : (horizontal ? "люк между комнатами" : "дверной проём между комнатами") };
        st.AddButton(inside, -1, s.insideIsBelow ? s.mountBelow : s.mountAbove);
        if (!s.exitOnly && s.NeedsReturnButton)
            st.AddButton(outside, -1, s.insideIsBelow ? s.mountAbove : s.mountBelow);
        return st;
    }
}

/// <summary>
/// ⭐ ВЫЛАЗКА — перенос ручного Level_07 в генератор. Первый СОСТАВНОЙ модуль: одна головоломка из
/// трёх групп, потому что меньшим числом она не собирается.
///
/// Форма: тупиковая комната с ключом, войти в которую можно ТОЛЬКО провалившись сверху, а выйти —
/// только через дверь, открываемую изнутри. Вход и выход разные, петля замкнута.
///   • ПУСКОВАЯ площадка в комнате сверху — стоишь на ней, окно истекает, и ты падаешь в люк;
///   • ЛОВЧАЯ площадка над пропастью в нижней комнате — не вызвал её заранее, падение смертельно;
///   • ВЫХОД — инверсная стена в единственном ребре дерева, кнопка только ИЗНУТРИ.
///
/// ⚠️ ПОЧЕМУ ПУСКОВАЯ ПЛОЩАДКА ВООБЩЕ НУЖНА. В этой игре нельзя спрыгнуть по своей воле: падение
/// бывает, только когда из-под ног исчезла платформа (правило игрока). Значит «намеренное падение»
/// приходится ОРГАНИЗОВАТЬ — ровно так и сделано в Level_07 (площадка B с окном 3 с над колодцем).
/// ⚠️ ПОЧЕМУ ПУСКОВАЯ И ЛОВЧАЯ — РАЗНЫЕ ГРУППЫ. Будь они одной, окно истекло бы у обеих сразу:
/// игрок падал бы вместе с исчезающей ловчей площадкой в пропасть. У ловчей окно должно быть
/// заметно длиннее (в Level_07: пусковая 3 с, ловчая 10 с).
///
/// Все три группы НЕСУЩИЕ по построению: без пусковой не упасть, без ловчей падение смертельно,
/// без выхода из камеры не выбраться (это ловит проверка запирания — см. LevelModel.StuckStates).
/// </summary>
public class ExcursionModule
{
    public static int StampedCount, RolledBackCount;

    public string Name => "вылазка";

    /// <summary>Ширина люка (он же пролёт ловчей площадки). Две клетки: приземляться надо на что-то
    /// шире одной, но и пол камеры нельзя съедать целиком.</summary>
    private const int HatchWidth = 2;

    public bool Fits(MazeCanvas c, MazeSite s)
    {
        if (s.roomAboveId < 0) return false;
        if (c.W(s.roomId) < HatchWidth + 4) return false;         // люк + по 2 клетки пола по краям
        if (c.H(s.roomAboveId) < 4) return false;                 // в верхней комнате нужна высота под пусковую
        // ⚠️⚠️ КАМЕРА ОБЯЗАНА БЫТЬ ГЛУБОКОЙ. 🐞 Иначе ключ достают СВЕРХУ ЧЕРЕЗ ЛЮК, не спускаясь:
        // ключ лежит в 1-2 рядах над полом камеры, то есть в H−1 рядах под полом верхней комнаты,
        // а дотяжка вниз — 3 ряда. При H ≤ 4 игрок просто протягивает руку в люк, и вся вылазка
        // превращается в украшение (поймано приёмкой: пусковая и ловчая помечены декоративными,
        // сид 16). Нужен H = Climb+2 = 5: тогда ключ уходит на 4 ряда и за дотяжку не влезает.
        if (c.H(s.roomId) < MazeCanvas.Climb + 2) return false;
        if (s.doorCol < 0 || s.doorHeight < 2) return false;      // ребро наружу — горизонтальный проём
        if (s.buttonBelow.x < 0 || s.buttonAbove.x < 0) return false;
        return true;
    }

    /// <summary>Ставит все три группы разом. Возвращает null, если место не подошло (правки откатывает).</summary>
    public List<ModuleStamp> StampAll(MazeCanvas c, MazeSite s)
    {
        if (!Fits(c, s)) { RolledBackCount++; return null; }

        int ceilRow  = c.R0(s.roomId) - 1;                        // ряд-стена: пол верхней комнаты
        int floorRow = c.FloorRow(s.roomId);                      // пол камеры
        int hatch0   = c.C0(s.roomId) + (c.W(s.roomId) - HatchWidth) / 2;
        int launchRow = ceilRow - MazeCanvas.Climb;               // пусковая: на 3 ряда над полом верхней

        // Предусловия по клеткам — ДО единой правки, чтобы не пришлось откатывать половину.
        if (launchRow <= c.R0(s.roomAboveId)) return null;         // упрётся в потолок верхней комнаты
        // ⚠️ ДЕКОР СНОСИМ, КАМЕНЬ — НЕТ. Тумбы и пилоны ставятся раньше и легко попадают в клетки,
        // которые вылазке нужны пустыми: путь от люка вверх, место над ловчей, площадка пусковой.
        // 🐞 Пока модуль на них просто отказывался, он не строился НИ РАЗУ, хотя место было годным —
        // и молча, отчего это долго выглядело как «редкость шаблона». Ворота такой декор давно сносят.
        var toClear = new List<Vector2Int>();
        for (int k = 0; k < HatchWidth; k++)
        {
            int col = hatch0 + k;
            if (c.At(ceilRow,   col) != '#') return null;          // потолок камеры должен быть целым
            if (c.At(floorRow,  col) != '#') return null;          // пол камеры тоже
            foreach (int r in new[] { launchRow, ceilRow - 1, floorRow - 1 })
            {
                if (c.At(r, col) == '.') continue;
                if (c.At(r, col) == '#' && c.Bump[r, col]) { toClear.Add(new Vector2Int(col, r)); continue; }
                return null;                                       // настоящий камень — место не годится
            }
        }

        c.Begin();
        foreach (var p in toClear) { c.Set(p.y, p.x, '.'); c.Bump[p.y, p.x] = false; }
        int launch = c.NextGroup(), catcher = c.NextGroup(), exit = c.NextGroup();

        for (int k = 0; k < HatchWidth; k++)
        {
            int col = hatch0 + k;
            c.Set(ceilRow,   col, '.');                            // ЛЮК в полу верхней комнаты
            c.SetGroup(launchRow, col, launch);           // ПУСКОВАЯ площадка над люком
            c.SetGroup(floorRow,  col, catcher);          // ЛОВЧАЯ площадка на месте пола
            for (int r = floorRow + 1; r < c.Rows; r++)            // ПРОПАСТЬ под ней: режем оболочку вниз
            { if (c.At(r, col) == '#') c.Set(r, col, ' '); else break; }
        }
        for (int r = s.doorRowTop; r < s.doorRowTop + s.doorHeight; r++)
            c.SetGroup(r, s.doorCol, exit);               // ВЫХОД: инверсная стена в проёме

        c.SetButton(s.buttonAbove.y, s.buttonAbove.x, launch);           // кнопки пусковой и ловчей — НАВЕРХУ,
        var catchBtn = new Vector2Int(s.buttonAbove.x, s.buttonAbove.y);
        // ⚠️ Кнопке ловчей нужно СВОЁ место: нажать обе надо ДО прыжка, а две кнопки в одной клетке
        // не поставить. Ищем соседнюю свободную клетку на том же полу.
        bool placed = false;
        for (int d = 1; d <= 6 && !placed; d++)
        for (int sign = -1; sign <= 1 && !placed; sign += 2)
        {
            int col = s.buttonAbove.x + sign * d;
            if (c.At(s.buttonAbove.y, col) != '.') continue;
            if (c.At(s.buttonAbove.y + 1, col) != '#') continue;   // должна стоять на полу
            catchBtn = new Vector2Int(col, s.buttonAbove.y);
            c.SetButton(catchBtn.y, catchBtn.x, catcher);
            placed = true;
        }
        if (!placed) { c.Rollback(); RolledBackCount++; return null; }
        c.SetButton(s.buttonBelow.y, s.buttonBelow.x, exit);             // кнопка выхода — ТОЛЬКО внутри камеры

        bool pinched = false;
        for (int r = ceilRow - 1; r <= floorRow + 1 && !pinched; r++)
        for (int k = -1; k <= HatchWidth && !pinched; k++)
            if (c.MakesPinch(r, hatch0 + k)) pinched = true;
        if (pinched) { c.Rollback(); RolledBackCount++; return null; }

        c.Commit(); StampedCount++;
        var stLaunch = new ModuleStamp
        { moduleName = Name + ": пусковая", groupIx = launch, inverted = false, gates = "прыжок в люк" };
        stLaunch.AddButton(s.buttonAbove);
        var stCatch = new ModuleStamp
        { moduleName = Name + ": ловчая", groupIx = catcher, inverted = false, gates = "приземление над пропастью" };
        stCatch.AddButton(catchBtn);
        var stExit = new ModuleStamp
        { moduleName = Name + ": выход", groupIx = exit, inverted = true, gates = "выход из камеры (кнопка изнутри)" };
        stExit.AddButton(s.buttonBelow);
        return new List<ModuleStamp> { stLaunch, stCatch, stExit };
    }
}
