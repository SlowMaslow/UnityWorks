using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ⭐⭐ ПАТТЕРН = ФОРМА (картинка) + СОДЕРЖИМОЕ (типизированные объекты).
///
/// Почему именно так. Первую версию трафаретов я построил на ASCII — и немедленно получил все
/// болезни, из-за которых ASCII в сентябре и отменили в пользу <see cref="LevelSpec"/>: алфавит
/// кончается, инверсию пришлось тащить отдельной строкой, вложенность — отдельным словарём, а окно
/// платформы не выражается вовсе. На движущейся платформе (путь, скорость) и противнике (маршрут,
/// поведение) это упёрлось бы сразу: в клетку такое не кладётся никаким алфавитом. Поправлено
/// игроком: «мы уже ранее решили, что ASCII не подходит».
///
/// Поэтому разделено:
///   • ФОРМА — картинка из камня, воздуха и требований к месту. Она честно двумерна, и рисовать её
///     глазами удобно. Никаких букв групп в ней больше нет;
///   • СОДЕРЖИМОЕ — список объектов со ссылками на клетки. Новый вид содержимого добавляется ПОЛЕМ
///     в объекте, а не новым символом в алфавите.
///
/// АЛФАВИТ ФОРМЫ (и это весь алфавит):
///   <c>#</c> станет камнем   <c>.</c> станет воздухом   <c>?</c> не трогаем и ничего не требуем
///   <c>!</c> требуем камень, не трогаем
///   <c>+</c> УСТЬЕ ВХОДА   <c>&gt;</c> УСТЬЕ ВЫХОДА — обе метки требуют: хотя бы одна клетка
///   ЭТОГО ВИДА обязана быть воздухом УЖЕ. Так паттерн стыкуется с миром там, где ему надо.
///
/// ⚠️ УСТЬЕВ ДВА ВИДА, И ЭТО НЕ ИЗЛИШЕСТВО. Вход и выход у камеры на разных сторонах: провалился
/// сверху слева, вылез вбок. 🐞 Пока вид был один, трафарет рисовал свою рамку поверх стен комнаты
/// и ЗАМУРОВЫВАЛ дверной проём, который раскладка уже прорезала: замер — «не встал дверь» на семи
/// схемах из девяти, камера оставалась без выхода, уровень непроходим. Теперь сторона выхода не
/// назначается, а ВЫБИРАЕТСЯ: зеркало кладётся так, чтобы открытый бок камеры смотрел на проём.
///
/// ⚠️ УСТЬЯ — АЛЬТЕРНАТИВЫ, А НЕ СПИСОК ТРЕБОВАНИЙ, и это стоило половины построек камеры.
/// 🐞 Замер: из 156 попыток посадки 116 отбивались ровно на устье. Устье было ОДНО и с точностью до
/// клетки — «воздух вот здесь». А комната сверху встаёт со случайным сдвигом вбок (раскладке нужно
/// лишь перекрытие), свободы по столбцу у трафарета всего три клетки, и назначенный столбец просто
/// не попадал в её воздух. Требование было завышено: паттерну нужно ПРИМЫКАНИЕ К МИРУ, а не
/// примыкание в заранее выбранной точке.
/// </summary>
public class Pattern
{
    public string name = "паттерн";
    /// <summary>Форма. Строка 0 — ВЕРХ, как и в схеме уровня.</summary>
    public string[] shape;
    /// <summary>Содержимое: платформы, ключи и всё, что появится дальше.</summary>
    public List<PatternPiece> pieces = new List<PatternPiece>();

    /// <summary>Паттерн запирает игрока: из его области нужен выход (см. SpaceNeed.needsExit).</summary>
    public bool needsExit;
    /// <summary>Можно ли отражать по горизонтали — бесплатная вариативность.</summary>
    public bool mirrorable = true;
    /// <summary>Какой ряд формы обязан лечь на ПОТОЛОК комнаты (стену над ней); −1 — не важно.</summary>
    public int ceilingRow = -1;
    /// <summary>
    /// ⭐ ФОРМА САДИТСЯ РОВНО ПО КОМНАТЕ: столбец 0 ложится на её ЛЕВУЮ СТЕНУ, последний — на правую,
    /// а всё между ними — её нутро. Тогда крайние столбцы можно оставить нетронутыми ('?'), и стены
    /// с уже прорезанными в них проёмами достаются паттерну готовыми, а не рисуются заново.
    /// ⚠️ Свободы по столбцу при этом не остаётся вовсе — её и не должно быть: комната резервируется
    /// РОВНО под трафарет (SpaceNeed.maxWidth), так что «подвинуть на клетку» значило бы съехать со
    /// своей же комнаты. Вариативность даёт зеркало, и теперь она осмысленная: зеркало выбирает,
    /// с какой стороны у камеры выход.
    /// </summary>
    public bool roomAligned;
    /// <summary>
    /// ⭐⭐ С ЭТОГО РЯДА ПАТТЕРН РОЕТ ВНИЗ, И РОЕТ ТОЛЬКО В СПЛОШНОМ КАМНЕ. −1 — не роет вовсе.
    ///
    /// Зачем. Комната резервируется под ВЕРХНЮЮ часть картинки — у камеры это сам коридор в три
    /// ряда. Всё, что ниже, лежит в толще между этажами, и раскладке про него знать не нужно.
    /// 🐞 Пока камера просила комнату во всю свою высоту (13×9), порт коридора оказывался у ПОТОЛКА
    /// комнаты, а горизонтальные проёмы режутся от ПОЛА — трасса входила не туда, где нарисован
    /// вход. Теперь порт сам собой попадает на нижний ряд комнаты, а глубина ничего не стоит.
    /// ⚠️ Требование камня строгое и на всю ширину: иначе паттерн вскроется в чужую комнату снизу.
    /// </summary>
    public int rockBelowRow = -1;

    public int Height => shape.Length;
    public int Width { get { int w = 0; foreach (var r in shape) w = Mathf.Max(w, r.Length); return w; } }

    public char ShapeAt(int r, int c, bool mirror)
    {
        if (r < 0 || r >= shape.Length) return '?';
        int cc = mirror ? Width - 1 - c : c;
        return cc >= 0 && cc < shape[r].Length ? shape[r][cc] : '?';
    }

    /// <summary>Столбец устья ВХОДА ('+') в форме; −1 — входа нет. Нужен, чтобы посадить форму
    /// входом к той комнате, ОТКУДА игрок приходит: у формы с односторонним прологом стороны не
    /// равноправны, и зеркало их меняет местами.</summary>
    public int EntryCol()
    {
        for (int r = 0; r < shape.Length; r++)
        for (int c = 0; c < shape[r].Length; c++)
            if (shape[r][c] == '+') return c;
        return -1;
    }
}

/// <summary>Что паттерн ставит. ⚠️ Новый вид (движущаяся платформа, противник) добавляется СЮДА
/// новым значением и своими полями — форма при этом не меняется вовсе.</summary>
public enum PieceKind { Platform, Key, Checkpoint }

/// <summary>Один объект паттерна: клетки заданы ОТНОСИТЕЛЬНО якоря трафарета.</summary>
public class PatternPiece
{
    /// <summary>Имя куска — чтобы приёмку можно было спросить, КАКАЯ из групп паттерна холостая.
    /// Без него все пять групп камеры зовутся одинаково, и разбирать поломку не по чему.</summary>
    public string name = "";
    public PieceKind kind = PieceKind.Platform;
    public List<Vector2Int> cells = new List<Vector2Int>();   // (столбец, ряд) от якоря
    /// <summary>Платформа ТВЁРДАЯ в покое, кнопка её убирает. false — наоборот: платформы нет,
    /// кнопка её создаёт. ⚠️ Смысл проверен по DisappearingPlatform, а не по догадке.</summary>
    public bool inverted;
    /// <summary>Окно в секундах. Пока модель считает бюджет ходов общим, но число уже здесь —
    /// чтобы не переделывать формат, когда бюджет станет считаться из окна каждой группы.</summary>
    public float window = 5f;
    /// <summary>
    /// ⭐ КЛАПАН ВОЗВРАТА: стена, которую игрок открывает ИЗНУТРИ, чтобы выйти. Держать цель — не её
    /// работа, и критерий «убери механизм — пропала ли цель» на ней врёт: убери её, и цель тем более
    /// достижима, просто наружу не выйти.
    ///
    /// 🐞 У камеры две такие стены (выход из шахты с ключом и ход в колодец возврата), и признак им
    /// не проставлялся — приёмка исправно звала их холостыми. Замер: сиды 4007 и 4009 браковались
    /// ровно по ним, при полном комплекте ключей и достижимом финише. То же исключение, что у
    /// двери-выхода из ветки (LevelGroup.returnValve), просто трафарету его забыли передать.
    /// </summary>
    public bool returnValve;
    public List<PatternButton> buttons = new List<PatternButton>();
}

/// <summary>Кнопка объекта. <see cref="hostPiece"/> — индекс куска, НА КОТОРОМ она стоит: пока того
/// нет, кнопку не нажать. Это и есть вложенность, и выражена она ССЫЛКОЙ, а не буквой в картинке.</summary>
public class PatternButton
{
    public Vector2Int cell;
    public int hostPiece = -1;
    /// <summary>К чему прижата: пол, потолок, стена. ⚠️ Ось, НЕЗАВИСИМАЯ от вложенности — кнопка
    /// может висеть под потолком и при этом принадлежать группе, и наоборот (поправлено игроком).</summary>
    public MountSide mount = MountSide.Floor;
}

/// <summary>
/// ⭐ БИБЛИОТЕКА ПАТТЕРНОВ. Новый паттерн добавляется СЮДА: форма плюс список объектов.
/// </summary>
public static class StencilLibrary
{
    /// <summary>
    /// ⭐⭐ КАМЕРА С КЛЮЧОМ — перенос ручного Level_11 игрока, клетка в клетку.
    ///
    /// ГЛАВНОЕ, ЧТО ПОКАЗАЛ РАЗБОР ОРИГИНАЛА: это не «комната с головоломкой», а ПРОЛЁТ ТРАССЫ.
    /// Весь паттерн вместе с коридором сверху и колодцем возврата — один прямоугольник 22×15, и
    /// дорога назад нарисована ВНУТРИ него. Поэтому камере не нужны ни внешний выход, ни лишние
    /// рёбра: она сама себе и вход, и возврат.
    ///
    /// Как читается маршрут (ряд 3 — сама трасса, слева направо):
    ///   • пол трассы в середине — платформа A, которой в покое НЕТ. Провал 7 клеток при боковой
    ///     дотяжке 6, то есть обойти нельзя: нажал — прошёл. Механизм ГЕЙТИТ маршрут, а не украшает;
    ///   • постоял на ней лишнее, окно истекло — провалился в камеру. Это ТОЧКА НЕВОЗВРАТА, и она
    ///     намеренная: наверх из камеры хода нет;
    ///   • кнопка платформы B висит под потолком над трассой — до неё дотягиваются, СТОЯ НА A,
    ///     то есть решение про лут принимается до падения;
    ///   • внизу: полка B, на ней кнопка стенки C (вложенность), за стенкой шахта с ключом;
    ///   • из шахты вниз, кнопка E — вернулся в камеру снизу; кнопка D — ушёл в правый колодец;
    ///   • колодец с уступами ведёт вверх, в ПОСТОЯННУЮ дыру трассы (3 клетки — перепрыгивается).
    ///     Так игрок всегда возвращается на основную трассу, ни разу ничего не отменяя.
    ///
    /// ⚠️ Ни у одной области нет тупика: у каждой минимум два выхода. Это и есть правило, которым
    /// игрок заменил «две кнопки на механизм»: возврат обеспечивает ТОПОЛОГИЯ, а не обратимость.
    /// </summary>
    public static Pattern Vault()
    {
        var p = new Pattern
        {
            name = "камера с ключом",
            //          0123456789012345678901
            // ⚠️ Комната под трафарет — только ВЕРХНИЕ ТРИ РЯДА (сам коридор). Всё, что ниже ряда 4,
            // паттерн РОЕТ В СПЛОШНОМ КАМНЕ (см. rockBelowRow). Так порт коридора попадает ровно
            // туда, где раскладка и режет горизонтальные проёмы — по нижнему ряду комнаты.
            shape = new[]
            {
                "??????????????????????",  // 0  потолок комнаты: под ним висит кнопка B
                "?....................?",  // 1
                "?....................?",  // 2
                "+....................>",  // 3  ТРАССА: устья по краям, слева кнопка A
                "?#####.......###...##?",  // 4  пол трассы: платформа A (6-12) и постоянная дыра (16-18)
                "?...#........#.......?",  // 5  шахта ключа слева (1-3), камера в середине, колодец справа
                "?...#........#..##...?",  // 6  уступ колодца
                "?##.#........#.......?",  // 7  уступ шахты
                "?...#........#.......?",  // 8  стенка шахты C (кол. 4)
                "?...#........#...##..?",  // 9  уступ колодца; кнопка C встанет на полку под собой
                "?..##........#.......?",  // 10 полка B (5-12)
                "?...#........#.......?",  // 11 стенка E (кол. 4) и стенка D (кол. 13)
                "?##.#........#..##...?",  // 12 уступ колодца
                "?...#........#.......?",  // 13 пол камеры
                "!!!!!!!!!!!!!!!!!!!!!!"   // 14 под всем этим обязан быть камень
            },
            needsExit = false,          // возврат нарисован внутри — внешний выход не нужен
            ceilingRow = 0,
            roomAligned = true
            // ⚠️ Требования «рой в сплошном камне» здесь НЕТ, и это осознанно: место под нижние ряды
            // резервирует ПЛАН (SpaceNeed.cellarHeight), комнатой-подвалом ровно под коридором.
            // 🐞 Пока картинка копала сама, замер дал 104 отказа из 110 посадок — сплошного массива
            // под коридором в живом уровне попросту не бывает.
        };

        // 0 — ПОЛ ТРАССЫ: обычная платформа, в покое её нет. Кнопка A стоит на камне слева от провала.
        var road = new PatternPiece { name = "пол трассы", inverted = false, window = 5f };
        for (int c = 6; c <= 12; c++) road.cells.Add(new Vector2Int(c, 4));
        road.buttons.Add(new PatternButton { cell = new Vector2Int(4, 3) });
        p.pieces.Add(road);

        // 1 — ПОЛКА в камере: тоже обычная, а кнопка B — под потолком НАД трассой. Дотянуться до неё
        // можно, только стоя на платформе A: у игрока это ряд 1, три ряда над трассой (подъём = 3).
        var shelf = new PatternPiece { name = "полка", inverted = false, window = 7f };
        for (int c = 5; c <= 12; c++) shelf.cells.Add(new Vector2Int(c, 10));
        // ⚠️ ВИСИТ ПОД ПОТОЛКОМ, а не стоит на полу — как группа B в оригинале. Это и есть причина,
        // по которой до неё дотягиваются только СТОЯ НА платформе трассы: с пола коридора три ряда
        // вверх не берутся. Поставь её на пол — и зависимость исчезнет, а головоломка вместе с ней.
        shelf.buttons.Add(new PatternButton { cell = new Vector2Int(10, 1), mount = MountSide.Ceiling });
        p.pieces.Add(shelf);

        // 2 — ВЕРХНЯЯ СТЕНКА ШАХТЫ: инверсная, кнопка стоит НА полке (индекс 1) — вложенность.
        var wallUp = new PatternPiece { name = "стенка шахты (верх)", inverted = true, window = 5f };
        wallUp.cells.Add(new Vector2Int(4, 8));
        wallUp.cells.Add(new Vector2Int(4, 9));
        wallUp.buttons.Add(new PatternButton { cell = new Vector2Int(8, 9), hostPiece = 1 });
        p.pieces.Add(wallUp);

        // 3 — НИЖНЯЯ СТЕНКА ШАХТЫ: кнопка ВНУТРИ шахты, на её полу. Ею игрок выходит из кармана
        // обратно в камеру — не отменяя ничего, а двигаясь дальше. Ровно группа E оригинала.
        var wallDown = new PatternPiece { name = "стенка шахты (низ)", inverted = true, window = 5f,
                                          returnValve = true };
        for (int r = 11; r <= 13; r++) wallDown.cells.Add(new Vector2Int(4, r));
        wallDown.buttons.Add(new PatternButton { cell = new Vector2Int(3, 13) });
        p.pieces.Add(wallDown);

        // 4 — СТЕНКА В КОЛОДЕЦ: кнопка на полу камеры. Ею открывается дорога к возврату (группа D).
        var wallOut = new PatternPiece { name = "стенка в колодец", inverted = true, window = 5f,
                                         returnValve = true };
        for (int r = 11; r <= 13; r++) wallOut.cells.Add(new Vector2Int(13, r));
        wallOut.buttons.Add(new PatternButton { cell = new Vector2Int(11, 13) });
        p.pieces.Add(wallOut);

        // 5 — КЛЮЧ в шахте, под каменной крышкой: сверху его не достать.
        var key = new PatternPiece { kind = PieceKind.Key };
        key.cells.Add(new Vector2Int(2, 5));
        p.pieces.Add(key);

        // 6 — ЧЕКПОИНТ В КОЛОДЦЕ ВОЗВРАТА, сразу за последней стенкой. Правило игрока: награда за
        // риск — ключ И чекпоинт. Здесь он попадает ровно туда, где риск уже позади: ключ в руках,
        // все три стенки пройдены, остаётся подъём назад на трассу. Сорвался на подъёме — вернёшься
        // сюда, а не к началу камеры.
        var cp = new PatternPiece { kind = PieceKind.Checkpoint };
        cp.cells.Add(new Vector2Int(16, 13));
        p.pieces.Add(cp);

        return p;
    }

    public static Pattern For(PuzzleElement e)
    {
        switch (e)
        {
            case PuzzleElement.Vault: return Vault();
        }
        return null;
    }
}

/// <summary>
/// Штамповщик: ищет место, где ФОРМА совместима с сеткой, вписывает её и расставляет ОБЪЕКТЫ.
/// Один на все паттерны — в этом весь смысл.
/// </summary>
public static class StencilStamper
{
    public static int StampedCount, RolledBackCount;
    /// <summary>Почему не встал — по причинам. Урок камеры: без нумерации отказов я трижды подряд
    /// ослаблял проверки наугад и каждый раз мимо.</summary>
    public static int FailNoSpot, FailPinch, FailButton;

    /// <summary>Разбор отказа ПОСАДКИ, до штампа. 🐞 Счётчика «места не нашлось» мало: он один на три
    /// разные болезни — комната не той высоты (кандидатов нет вовсе), устье замуровано, чужой механизм
    /// на клетке. Пока их не разделили, было видно только «3 постройки из 12 заказов».</summary>
    public static int NoCandidates, SpotsTried, BlockMouth, BlockExit, BlockRock, BlockMech;
    /// <summary>Сколько посадок отсеяно из-за того, что устье входа смотрело не к родителю.</summary>
    public static int SideSkipped;
    /// <summary>Габариты комнат, куда камеру звали: чтобы не гадать, попадает ли она по размеру.</summary>
    public static readonly List<string> Rooms = new List<string>();
    /// <summary>Где именно сработал зажим, в координатах формы: без этого его чинят наугад.</summary>
    public static readonly List<string> PinchAt = new List<string>();

    public static void ResetStats()
    {
        StampedCount = RolledBackCount = FailNoSpot = FailPinch = FailButton = 0;
        NoCandidates = SpotsTried = BlockMouth = BlockExit = BlockRock = BlockMech = SideSkipped = 0;
        Rooms.Clear(); PinchAt.Clear();
    }

    /// <param name="parentSide">С какой стороны игрок ПРИХОДИТ: 0 — родительская комната слева,
    /// 1 — справа, −1 — неизвестно (тогда сторона не проверяется).
    ///
    /// 🐞🐞 ЗЕРКАЛО ВЫБИРАЛОСЬ ЖРЕБИЕМ, И ЭТО ЛОМАЛО КАМЕРУ ЦЕЛИКОМ. У её формы стороны не
    /// равноправны: слева пролог с кнопкой на твёрдом полу, за ним провал в семь клеток при боковой
    /// дотяжке шесть — обойти нельзя намеренно. Зеркало меняет вход и выход местами, и в половине
    /// случаев игрок приходил со стороны ВЫХОДА: провал перед ним, а кнопка провала — за провалом.
    /// Дальше по трассе не пройти, и всё, что за камерой, включая финиш, отрезано.
    /// Замер: из семи уровней с камерой браком были ШЕСТЬ (без камеры — 3 из 29).
    /// Поэтому сторона теперь не пожелание, а фильтр: посадка с устьем не к родителю не предлагается
    /// вовсе. Не нашлось подходящей — камеры не будет, и это честнее сломанного уровня.</param>
    public static List<ModuleStamp> Stamp(MazeCanvas c, int roomId, Pattern p,
                                          System.Random rng, out Vector2Int keyCell,
                                          int parentSide = -1)
    {
        keyCell = new Vector2Int(-1, -1);
        int w = p.Width, h = p.Height;
        int r0 = c.R0(roomId) - 3, c0 = c.C0(roomId) - 2;
        int r1 = c.FloorRow(roomId) + 1, c1 = c.C0(roomId) + c.W(roomId) + 1;

        // ⚠️ Привязанные координаты СЧИТАЮТСЯ, а не ищутся перебором: паттерн вправе уходить глубоко
        // под комнату (см. Pattern.rockBelowRow), и рамка «влезь в комнату по высоте» отсекала бы
        // такой паттерн подчистую — кандидатов выходило бы ноль.
        var rows = new List<int>();
        if (p.ceilingRow >= 0) rows.Add(c.R0(roomId) - 1 - p.ceilingRow);
        else for (int r = r0; r + h <= r1 + 1; r++) rows.Add(r);

        var cols = new List<int>();
        if (p.roomAligned) cols.Add(c.C0(roomId) - 1);
        else for (int cc = c0; cc + w <= c1 + 1; cc++) cols.Add(cc);

        int entryCol = p.EntryCol();
        var spots = new List<Vector3Int>();
        foreach (int r in rows)
        {
            if (r < 0 || r + h > c.Rows) continue;          // за сеткой Set молчит, а At врёт камнем
            foreach (int cc in cols)
            {
                if (cc < 0 || cc + w > c.Cols) continue;
                for (int mir = 0; mir <= 1; mir++)
                {
                    if (mir == 1 && !p.mirrorable) continue;
                    // Устье входа обязано смотреть туда, откуда игрок приходит (см. parentSide).
                    if (parentSide >= 0 && entryCol >= 0)
                    {
                        int ex = mir == 1 ? w - 1 - entryCol : entryCol;
                        bool entryLeft = ex * 2 < w;
                        if ((parentSide == 0) != entryLeft) { SideSkipped++; continue; }
                    }
                    spots.Add(new Vector3Int(r, cc, mir));
                }
            }
        }
        if (Rooms.Count < 200)
            Rooms.Add(c.W(roomId) + "x" + c.H(roomId) + " надо " + w + "x" + h + " мест " + spots.Count);
        for (int i = spots.Count - 1; i > 0; i--)
        { int j = rng.Next(i + 1); var t = spots[i]; spots[i] = spots[j]; spots[j] = t; }

        foreach (var sp in spots)
        {
            SpotsTried++;
            if (!ShapeFits(c, p, sp.x, sp.y, sp.z == 1)) continue;
            var res = Apply(c, p, sp.x, sp.y, sp.z == 1, out keyCell);
            if (res != null) { StampedCount++; return res; }
        }
        if (spots.Count == 0) NoCandidates++;
        FailNoSpot++;
        return null;
    }

    private static bool ShapeFits(MazeCanvas c, Pattern p, int row, int col, bool mirror)
    {
        int inN = 0, inOpen = 0, outN = 0, outOpen = 0;
        for (int r = 0; r < p.Height; r++)
        for (int k = 0; k < p.Width; k++)
        {
            char want = p.ShapeAt(r, k, mirror);
            char have = c.At(row + r, col + k);
            // ⚠️ Ниже своей черты паттерн роет в ТОЛЩЕ: там обязан быть камень, и не только под теми
            // клетками, которые он пишет, а под всей полосой — иначе он вскроется в чужую комнату.
            if (p.rockBelowRow >= 0 && r >= p.rockBelowRow && have != '#') { BlockRock++; return false; }
            if (want == '?') continue;
            if (want == '!') { if (have != '#') { BlockRock++; return false; } continue; }
            // Устье ВЫХОДА не переписываем вовсе: это стена комнаты с проёмом, она и так на месте.
            if (want == '>') { outN++; if (have == '.') outOpen++; continue; }
            // Камень и воздух переписываем, чужие механизмы — никогда.
            if (have != '#' && have != '.') { BlockMech++; return false; }
            if (want == '+') { inN++; if (have == '.') inOpen++; }
        }
        // По одному открытому устью каждого вида: иначе паттерн либо замурован, либо без выхода.
        if (inN > 0 && inOpen == 0) { BlockMouth++; return false; }
        if (outN > 0 && outOpen == 0) { BlockExit++; return false; }
        return true;
    }

    /// <summary>Клетка объекта в координатах сетки, с учётом зеркала.</summary>
    private static Vector2Int Place(Pattern p, Vector2Int cell, int row, int col, bool mirror)
        => new Vector2Int(col + (mirror ? p.Width - 1 - cell.x : cell.x), row + cell.y);

    private static List<ModuleStamp> Apply(MazeCanvas c, Pattern p, int row, int col,
                                           bool mirror, out Vector2Int keyCell)
    {
        keyCell = new Vector2Int(-1, -1);

        // ⚠️⚠️ ЗАЖИМЫ СЧИТАЕМ ТОЛЬКО СВОИ. 🐞 Проверка шла по кольцу на клетку шире паттерна и ловила
        // диагональные стыки, которые там БЫЛИ И ДО НЕГО: стена комнаты с прорезанным проёмом даёт
        // такой стык сама по себе. Замер: 80 откатов из 100 посадок, и ни один не был виной картинки.
        // Поэтому запоминаем, что было зажато ДО правки, и спрашиваем только про новое.
        var wasPinched = new HashSet<long>();
        for (int r = -1; r <= p.Height; r++)
        for (int k = -1; k <= p.Width; k++)
            if (c.MakesPinch(row + r, col + k)) wasPinched.Add((long)(row + r) * 100000 + (col + k));

        c.Begin();

        for (int r = 0; r < p.Height; r++)
        for (int k = 0; k < p.Width; k++)
        {
            char want = p.ShapeAt(r, k, mirror);
            if (want == '+') want = '.';                   // устье — тот же воздух, только со связью
            if (want == '#' || want == '.') c.Set(row + r, col + k, want);
        }

        // Сперва раздаём id всем платформам: кнопка может ссылаться на кусок, до которого обход
        // ещё не дошёл, и без предварительной раздачи ссылка осталась бы пустой.
        var idOf = new int[p.pieces.Count];
        for (int i = 0; i < p.pieces.Count; i++)
            idOf[i] = p.pieces[i].kind == PieceKind.Platform ? c.NextGroup() : -1;

        var stamps = new List<ModuleStamp>();
        for (int i = 0; i < p.pieces.Count; i++)
        {
            var piece = p.pieces[i];
            if (piece.kind == PieceKind.Key)
            {
                foreach (var cell in piece.cells)
                { var g = Place(p, cell, row, col, mirror); keyCell = g; c.Set(g.y, g.x, '*'); }
                continue;
            }
            if (piece.kind == PieceKind.Checkpoint)
            {
                foreach (var cell in piece.cells)
                { var g = Place(p, cell, row, col, mirror); c.Set(g.y, g.x, '='); }
                continue;
            }

            int id = idOf[i];
            foreach (var cell in piece.cells)
            { var g = Place(p, cell, row, col, mirror); c.SetGroup(g.y, g.x, id); }

            var st = new ModuleStamp
            { moduleName = p.name + (piece.name.Length > 0 ? ": " + piece.name : ""),
              groupIx = id, inverted = piece.inverted, window = piece.window,
              returnValve = piece.returnValve,
              gates = "паттерн " + p.name };
            foreach (var b in piece.buttons)
            {
                var g = Place(p, b.cell, row, col, mirror);
                c.SetButton(g.y, g.x, id);
                int host = b.hostPiece >= 0 && b.hostPiece < idOf.Length ? idOf[b.hostPiece] : -1;
                // ⚠️ Зеркало меняет и сторону крепления: правая стена становится левой.
                var mount = b.mount;
                if (mirror && mount == MountSide.WallLeft) mount = MountSide.WallRight;
                else if (mirror && mount == MountSide.WallRight) mount = MountSide.WallLeft;
                st.AddButton(g, host, mount);
            }
            if (st.buttons.Count == 0)
            { c.Rollback(); RolledBackCount++; FailButton++; return null; }
            stamps.Add(st);
        }

        for (int r = -1; r <= p.Height; r++)
        for (int k = -1; k <= p.Width; k++)
        {
            if (!c.MakesPinch(row + r, col + k)) continue;
            if (wasPinched.Contains((long)(row + r) * 100000 + (col + k))) continue;  // не мы сделали
            if (PinchAt.Count < 400) PinchAt.Add(k + ":" + r);
            c.Rollback(); RolledBackCount++; FailPinch++; return null;
        }

        c.Commit();
        return stamps;
    }
}
