using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ⭐ РАСКЛАДКА КОМНАТ ПО ДЕРЕВУ — каждая комната получает СВОЙ прямоугольник нужного ей размера,
/// а не пересечение общей ширины колонки и общей высоты строки.
///
/// Зачем: на решётке размер комнаты — это `colW[x] × rowH[y]`, то есть ширина общая для ВСЕЙ колонки
/// и высота общая для ВСЕЙ строки. Комната не может быть большой сама по себе: если вылазке нужен зал
/// 6×5, шесть клеток получает вся колонка, а пять рядов — вся строка. Из-за этого «заказ геометрии»
/// упирался в потолок трижды подряд (мост, залы, вылазка), и я каждый раз лечил симптом.
///
/// ⚠️ ПОЧЕМУ НЕ ОБЫЧНЫЙ TREEMAP (аналогия игрока — тепловая карта рынка). Тепловая карта пакует
/// прямоугольники ПО ПЛОЩАДИ и про соседство ничего не обещает: две связанные в дереве комнаты у неё
/// запросто окажутся в разных углах, а нам между ними нужен проход. Здесь раскладка идёт ПО САМОМУ
/// ДЕРЕВУ: ребёнок ставится вплотную к стене родителя, и соседство верно по построению.
///
/// ⚠️ ПОЛЫ ГОРИЗОНТАЛЬНЫХ СОСЕДЕЙ ВЫРАВНИВАЮТСЯ (y0 совпадает). Иначе дверной проём соединял бы
/// комнаты на разной высоте и через него нельзя было бы просто перейти. Потолки при этом разные —
/// отсюда и берётся часть непохожести.
/// </summary>
public static class RoomLayout
{
    /// <summary>Прямоугольник комнаты в клетках: (x0,y0) — левый НИЖНИЙ угол интерьера.</summary>
    public class RoomBox
    {
        public int id;
        public int x0, y0, w, h;
        public int X1 => x0 + w - 1;
        public int Y1 => y0 + h - 1;
        public override string ToString() => $"#{id}({x0},{y0} {w}×{h})";
    }

    /// <summary>Связь двух комнат и отрезок ОБЩЕЙ СТЕНЫ, в котором можно прорезать проход.</summary>
    public class RoomLink
    {
        public int a, b;              // a — родитель, b — ребёнок
        public bool vertical;         // true: b НАД a (подъём); false: b сбоку
        public int from, to;          // общий отрезок: столбцы для вертикальной связи, ряды для боковой
        public int wall;              // ряд-стена между ними (верт.) либо столбец-стена (гор.)
        /// <summary>Связь СВЕРХ дерева: даёт петлю, механизма на ней нет.</summary>
        public bool extra;
        /// <summary>Для ТОННЕЛЯ: последний столбец прорезаемого хода (у обычной связи равен wall).
        /// 🐞 Пока лишними рёбрами могли стать только комнаты, стоящие ровно через одну стену,
        /// петель выходило 0.4 на уровень и процент ни на что не влиял — несвязанные комнаты почти
        /// всегда стоят дальше. Тоннель снимает это ограничение (и это ровно коридоры из статьи).</summary>
        public int wallEnd;
    }

    /// <summary>Насколько длинный ход можно прорезать сквозь камень ради петли.</summary>
    private const int MaxTunnel = 7;

    /// <summary>Счётчики поэтапной укладки — чтобы решать по замеру, а не по ощущению.</summary>
    public static int StatBuilds, StatRestarts, StatRelaxed, StatStageRetries;
    public static void ResetStats() { StatBuilds = StatRestarts = StatRelaxed = StatStageRetries = 0; }


    /// <summary>
    /// ⭐ КАНДИДАТЫ В ЛИШНИЕ РЁБРА: пары комнат, которые СТОЯТ РЯДОМ (через одну стену и с достаточным
    /// перекрытием), но в дереве не связаны. Идея из разбора статьи про генерацию подземелий: там к
    /// остовному дереву добавляют 8-10% рёбер, и уровень перестаёт быть деревом — появляются петли.
    ///
    /// Нам это нужно не для красоты: ВЫЛАЗКА требует у камеры ДВУХ связей (вход провалом сверху и
    /// выход стеной вбок), а дерево даёт одну — потому она на свободном пути и не строилась.
    ///
    /// ⚠️ Здесь только ГЕОМЕТРИЯ. Решать, какие из этих рёбер безопасно открыть, — не дело раскладки:
    /// лишний проход может обойти механизм и сделать его декоративным.
    /// </summary>
    public static List<RoomLink> FindAdjacent(Result r)
    {
        var linked = new HashSet<long>();
        foreach (var l in r.links) linked.Add(Key(l.a, l.b));
        var found = new List<RoomLink>();
        for (int i = 0; i < r.rooms.Count; i++)
        for (int j = i + 1; j < r.rooms.Count; j++)
        {
            var a = r.rooms[i]; var b = r.rooms[j];
            if (linked.Contains(Key(a.id, b.id))) continue;
            // Соседи по горизонтали: между ними от одной стены до короткого ТОННЕЛЯ.
            var left = a.X1 < b.x0 ? a : b; var right = a.X1 < b.x0 ? b : a;
            int gapX = right.x0 - left.X1 - 1;
            if (gapX >= 1 && gapX <= MaxTunnel)
            {
                int lo = Mathf.Max(a.y0, b.y0), hi = Mathf.Min(a.Y1, b.Y1);
                if (hi - lo + 1 < MinOverlap || Mathf.Abs(a.y0 - b.y0) > MaxFloorStep) continue;
                // ⚠️ Ход не должен вспороть третью комнату по дороге.
                int tLo = Mathf.Min(a.y0, b.y0), tHi = Mathf.Max(a.y0, b.y0) + MinOverlap - 1;
                bool blocked = false;
                foreach (var o in r.rooms)
                {
                    if (o.id == a.id || o.id == b.id) continue;
                    if (o.x0 <= right.x0 - 1 && left.X1 + 1 <= o.X1 && o.y0 <= tHi + 1 && tLo - 1 <= o.Y1)
                    { blocked = true; break; }
                }
                if (blocked) continue;
                found.Add(new RoomLink { a = a.id, b = b.id, vertical = false, extra = true,
                    from = tLo, to = tHi, wall = left.X1 + 1, wallEnd = right.x0 - 1 });
                continue;
            }
            // Соседи по вертикали: одна стена, перекрытие по ширине.
            bool aUnder = b.y0 == a.Y1 + 2, bUnder = a.y0 == b.Y1 + 2;
            if (!aUnder && !bUnder) continue;
            int lo2 = Mathf.Max(a.x0, b.x0), hi2 = Mathf.Min(a.X1, b.X1);
            if (hi2 - lo2 + 1 < MinOverlap) continue;
            int vw = aUnder ? a.Y1 + 1 : b.Y1 + 1;
            found.Add(new RoomLink { a = aUnder ? a.id : b.id, b = aUnder ? b.id : a.id,
                vertical = true, extra = true, from = lo2, to = hi2, wall = vw, wallEnd = vw });
        }
        return found;
    }

    private static long Key(int a, int b) => (long)Mathf.Min(a, b) * 100000 + Mathf.Max(a, b);

    /// <summary>Мешает ли третья комната прорезать ход между этими двумя (для тоннеля вбок).</summary>
    private static bool Blocked(RoomBox a, RoomBox b, RoomBox[] boxes, int n, int skipA, int skipB)
    {
        var left = a.X1 < b.x0 ? a : b; var right = a.X1 < b.x0 ? b : a;
        if (right.x0 - left.X1 - 1 <= 1) return false;          // общая стена — резать нечего
        int tLo = Mathf.Min(a.y0, b.y0), tHi = Mathf.Max(a.y0, b.y0) + MinOverlap - 1;
        for (int i = 0; i < n; i++)
        {
            var o = boxes[i];
            if (o == null || o == a || o == b || o.id == skipA || o.id == skipB) continue;
            if (o.x0 <= right.x0 - 1 && left.X1 + 1 <= o.X1 && o.y0 <= tHi + 1 && tLo - 1 <= o.Y1) return true;
        }
        return false;
    }

    /// <summary>
    /// Можно ли прорезать проход между комнатами: либо общая стена, либо короткий ТОННЕЛЬ сквозь
    /// камень — ровно то же, что считает соседством <see cref="FindAdjacent"/>.
    /// ⚠️ Требовать именно общую стену оказалось слишком строго: петля замыкалась в 16% случаев.
    /// Тоннель рендер уже умеет (RoomLink.wallEnd), и для двери он ничем не хуже стены.
    /// </summary>
    private static bool Touches(RoomBox a, RoomBox b)
    {
        if (a == null || b == null) return false;
        var left = a.X1 < b.x0 ? a : b; var right = a.X1 < b.x0 ? b : a;
        int gapX = right.x0 - left.X1 - 1;
        if (gapX >= 1 && gapX <= MaxTunnel)
        {
            int lo = Mathf.Max(a.y0, b.y0), hi = Mathf.Min(a.Y1, b.Y1);
            if (hi - lo + 1 >= MinOverlap && Mathf.Abs(a.y0 - b.y0) <= MaxFloorStep) return true;
        }
        bool stacked = a.Y1 + 2 == b.y0 || b.Y1 + 2 == a.y0;
        if (!stacked) return false;
        int lo2 = Mathf.Max(a.x0, b.x0), hi2 = Mathf.Min(a.X1, b.X1);
        return hi2 - lo2 + 1 >= MinOverlap;
    }

    /// <summary>Что комната требует от размера. Заполняется из ролей рецепта.</summary>
    public class RoomReq
    {
        public int minW = 3, minH = 3;
        public int maxW = 8, maxH = MaxRoomHeight;
        /// <summary>
        /// ⭐ КОМНАТА ОБЯЗАНА ВСТАТЬ РОВНО ПОД РОДИТЕЛЕМ, столбец в столбец. Нужна паттерну, который
        /// занимает не одну комнату, а НЕСКОЛЬКО ЭТАЖЕЙ: у камеры это коридор трассы и вырытый под
        /// ним подвал с ключом и колодцем возврата.
        /// 🐞 Без этого паттерн приходилось «рыть в сплошном камне», и замер показал, почему так
        /// нельзя: сплошного массива под коридором не бывает — из 110 посадок 104 отбивались именно
        /// на требовании камня. Место под паттерн надо РЕЗЕРВИРОВАТЬ, а не искать.
        /// </summary>
        public bool alignX;
        /// <summary>
        /// ⭐ ЖЕЛАЕМОЕ направление — в отличие от жёсткого <c>dir[]</c> это лишь порядок перебора:
        /// сперва пробуем его, не вышло — обычные варианты. Нужно ветке-петле, которой надо уйти
        /// вверх, пройти вбок и вернуться вниз к трассе.
        /// 🐞 Жёстким требованием то же самое не строится: раскладка не сошлась НИ РАЗУ из сорока —
        /// спускающийся конец ветки упирается в уже занятые комнаты маршрута, а запасного хода нет.
        /// </summary>
        public LinkDir prefer = LinkDir.Any;

        /// <summary>
        /// ⭐⭐ КОМНАТА ВЫХОДИТ ИЗ ОБЛАСТИ РОСТА СВОЕЙ ВЕТКИ. Нужна ветке-ПЕТЛЕ: она обязана вернуться
        /// туда, откуда ушла, а область роста именно это и запрещает — ветки разводятся по секторам,
        /// чтобы не наезжать друг на друга.
        /// 🐞 Замер, доказавший, что дело в области, а не в направлениях: форма «вверх → вбок → вниз»
        /// жёстко — раскладка не сошлась ни разу из 40; мягко — сходится, но концов веток рядом с
        /// трассой стало МЕНЬШЕ (1 из 120 против 6 без всякой формы).
        /// </summary>
        public bool freeRegion;

        /// <summary>
        /// ⭐⭐ КОМНАТА ОБЯЗАНА ВСТАТЬ ВПЛОТНУЮ К ЭТОЙ (id) — помимо связи с родителем. Так замыкается
        /// петля: последнее звено ветки касается комнаты маршрута, и между ними можно поставить дверь.
        /// −1 — требования нет.
        /// ⚠️ Требование МЯГКОЕ по последствиям: если места нет, звено ставится как обычно, а петля
        /// в этот раз не замкнётся. Жёсткое требование ронять раскладку целиком не имеет права.
        /// </summary>
        public int nextTo = -1;
        /// <summary>⭐ Годится соседство с ЛЮБОЙ из этих комнат. Петле неважно, в какую именно точку
        /// трассы выйти — важно выйти. Назначать одну конкретную оказалось слишком строго: она к
        /// моменту укладки бывает уже обстроена со всех сторон.</summary>
        public List<int> nextToAny;

        /// <summary>
        /// ⭐⭐ ТЯНУТЬ КОМНАТУ К ЭТОЙ (id): из всех годных мест выбирается БЛИЖАЙШЕЕ к ней, а не первое
        /// попавшееся. Этим ветка-петля и наводится на трассу — иначе она бредёт случайно, и её конец
        /// оказывается рядом с целью только по удаче.
        /// 🐞 Замер без притяжения: петля замыкалась в 8 случаях из 86 (9%). Само требование
        /// соседства при этом верное — не хватало именно наведения.
        /// </summary>
        public int pullTo = -1;
    }

    /// <summary>Куда обязан встать ребёнок относительно родителя (диктуется ролью на ребре).</summary>
    public enum LinkDir { Any, Up, Down, Horizontal }

    public class Result
    {
        public List<RoomBox> rooms = new List<RoomBox>();
        public List<RoomLink> links = new List<RoomLink>();
        public int Width, Height;     // габарит всей раскладки в клетках
    }

    /// <summary>⚠️ Высота комнаты ограничена КЛИМБОМ: подъёмы строим ≤3 рядов, отсюда H ≤ Climb+2.
    /// Выше — только двухъярусные залы, и там подъём делится уступами.</summary>
    public const int MaxRoomHeight = MazeCanvas.Climb + 2;

    /// <summary>
    /// ⭐ ПОТОЛОК ЗАЛА. Обычная комната ограничена дотяжкой (<see cref="MaxRoomHeight"/>): выше игроку
    /// не за что зацепиться. Зал снимает это ограничение тем, что подъём в нём разбит УСТУПАМИ —
    /// ровно так, как это сделано в ручных уровнях игрока.
    ///
    /// Зачем вообще: замер объёма (вертикальные пробеги открытого пространства) — в ручных уровнях
    /// 22-31% пробегов выше шести клеток и максимум 22-30, у генератора 5-7% и максимум 11-14. То
    /// есть у нас был ОДИН масштаб, 3×5 на весь уровень, и оттого «примитивный прямоугольник» вместо
    /// объёма. Разброс масштабов — то же, чем добиваются естественности в Cogmind (разбор статьи).
    /// </summary>
    public const int MaxHallHeight = MazeCanvas.Climb * 4;
    /// <summary>Сколько клеток общей стены нужно, чтобы прорезать проход (люк — 2 клетки минимум).</summary>
    private const int MinOverlap = 2;

    /// <summary>
    /// ⭐ Насколько пол соседней комнаты может отличаться по высоте. Дотяжкой НЕ ограничен: не хватает
    /// вылета — рендер достраивает ступени прямо в проёме (идея игрока 2026-09-03).
    /// ⚠️ Предел всё же есть: очень высокая лестница шириной в клетку читается как колодец, а не как
    /// проход между комнатами.
    /// </summary>
    public const int MaxFloorStep = MazeCanvas.Climb * 2;
    /// <summary>
    /// Камня между НЕ связанными комнатами. ⚠️ ОДНА КЛЕТКА — ровно как было на решётке
    /// (`colX[x+1] = colX[x] + colW[x] + 1`), и там это выглядело нормально все эти месяцы.
    /// 🐞 Сперва я поставил две «на всякий случай» — и укладка не сходилась в половине случаев:
    /// на змейке третья комната почти всегда нарушала зазор с первой. Перестраховка стоила половины
    /// раскладок, а гипотезы про регионы, которые я проверял до этого, были мимо.
    /// </summary>
    private const int Margin = 1;

    /// <summary>
    /// Разложить дерево. <paramref name="parent"/>[i] — родитель комнаты i (−1 у корня),
    /// <paramref name="dir"/>[i] — куда обязан встать ребёнок i относительно родителя.
    /// Возвращает null, если за <paramref name="tries"/> попыток уложить не удалось: недобор честнее
    /// молчаливой подмены.
    /// </summary>
    /// <remarks>
    /// Замер отдачи от числа попыток на реалистичных деревьях («гусеница» — основной путь плюс
    /// короткие ветки): 60 → 82% уложенных, 300 → 88%, 1500 → 90%. Дальше упирается в деревья,
    /// которые этой жадной укладкой не берутся в принципе (узел с тремя детьми, у каждого своё
    /// жёсткое направление). 300 стоит 0.4 мс — берём его, а оставшиеся проценты пусть решает
    /// переброс сида уровнем выше, он и так есть.
    /// </remarks>
    public static Result Build(int n, int[] parent, RoomReq[] req, LinkDir[] dir,
                               System.Random rng, int tries = 300)
        => BuildStaged(n, parent, req, dir, rng, null, tries);

    /// <summary>
    /// ⭐⭐ ПОЭТАПНАЯ УКЛАДКА (предложение игрока). Комнаты кладутся не одной попыткой на весь
    /// уровень, а группами: сперва хребет старт→финиш, потом каждая ветка отдельно, добор последним.
    /// Не встала группа — переигрывается ТОЛЬКО она, а всё уже поставленное остаётся.
    ///
    /// 🐞 Зачем. Прежняя укладка была «всё или ничего»: не встала одна комната — в мусор шла вся
    /// попытка, и снаружи это перезапускалось до 300 раз с нуля. Цена видна на замере петли: стык
    /// с трассой при жёстком требовании получался в 4 случаях из 4, а раскладка сходилась 4 попытки
    /// из 40 — губило не требование, а именно безоткатность.
    ///
    /// ⚠️ ОБЛАСТЕЙ РОСТА ЗДЕСЬ БОЛЬШЕ НЕТ. Они существовали ровно затем, чтобы ранняя ветка не
    /// занимала место поздней при отсутствии отката. С поэтапной укладкой откат есть, а области
    /// мешали: именно они разносили комнаты так, что конец ветки в 60 случаях из 86 не касался
    /// ничего, и петлю нельзя было замкнуть. Раскидистость уровня при этом не порок, а цель
    /// (решение игрока): уровень должен казаться насыщенным, а не линейным.
    /// </summary>
    /// <param name="stageOf">Номер этапа для каждого узла (null — всё одним этапом). Корень кладётся
    /// первым независимо от номера.</param>
    public static Result BuildStaged(int n, int[] parent, RoomReq[] req, LinkDir[] dir,
                                     System.Random rng, int[] stageOf, int triesPerStage = 60)
    {
        var kids = new List<int>[n];
        for (int i = 0; i < n; i++) kids[i] = new List<int>();
        int root = -1;
        for (int i = 0; i < n; i++) { if (parent[i] < 0) root = i; else kids[parent[i]].Add(i); }
        if (root < 0) return null;

        // Порядок обхода: родитель всегда раньше ребёнка.
        var order = new List<int>();
        var stack = new Stack<int>(); stack.Push(root);
        while (stack.Count > 0) { int v = stack.Pop(); order.Add(v); foreach (int k in kids[v]) stack.Push(k); }

        // Этапы в порядке возрастания номера; корень уже стоит, его пропускаем.
        var stages = new List<List<int>>();
        {
            var byStage = new Dictionary<int, List<int>>();
            foreach (int id in order)
            {
                if (id == root) continue;
                int st = stageOf != null ? stageOf[id] : 0;
                List<int> l;
                if (!byStage.TryGetValue(st, out l)) { l = new List<int>(); byStage[st] = l; }
                l.Add(id);
            }
            var nums = new List<int>(byStage.Keys); nums.Sort();
            foreach (int st in nums) stages.Add(byStage[st]);
        }

        // ⚠️ ВНЕШНИЙ ПЕРЕЗАПУСК ПОВЕРХ ЭТАПОВ. Этапы дают дешёвый откат, но не всесильны: если
        // ранние комнаты обстроили точку отрыва со всех сторон, ветке физически некуда встать, а
        // переигрывать ранний этап поздний уже не может. 🐞 Замер: одни этапы дали 29 уровней из 40.
        // Перезапуск возвращает утраченное, оставаясь во много раз дешевле прежних 300 попыток —
        // те начинали с нуля ВСЕГДА, а эти только когда действительно тупик.
        RoomBox[] boxes = null; Result res = null;
        bool allOk = false;
        StatBuilds++;
        for (int restart = 0; restart < 12 && !allOk; restart++)
        {
            if (restart > 0) StatRestarts++;
        boxes = new RoomBox[n];
        res = new Result();
        var s0 = Size(req[root], rng);
        boxes[root] = new RoomBox { id = root, x0 = 0, y0 = 0, w = s0[0], h = s0[1] };
        allOk = true;

        foreach (var stageNodes in stages)
        {
            bool ok = false;
            for (int attempt = 0; attempt < triesPerStage && !ok; attempt++)
            {
                int linksBefore = res.links.Count;
                ok = PlaceStage(n, parent, req, dir, rng, boxes, res, stageNodes);
                if (!ok)
                {
                    StatStageRetries++;
                    // Откат ТОЛЬКО этого этапа: всё, что стояло раньше, остаётся на месте.
                    foreach (int id in stageNodes) boxes[id] = null;
                    res.links.RemoveRange(linksBefore, res.links.Count - linksBefore);
                }
            }
            // ⚠️ ПОСЛЕДНЯЯ СТУПЕНЬ: уровень не должен умирать из-за УКРАШЕНИЯ. Замыкание петли и
            // притяжение к трассе — пожелания; если этап из-за них не встаёт, снимаем их и кладём
            // ветку как обычную. 🐞 Без этой ступени этапы дали скачок замыкания (9% → 38%), но
            // уронили 11 уровней из 40: не встал этап — не встал весь уровень.
            if (!ok)
            {
                StatRelaxed++;
                var savedNext = new List<int>[stageNodes.Count];
                var savedPull = new int[stageNodes.Count];
                for (int i = 0; i < stageNodes.Count; i++)
                {
                    savedNext[i] = req[stageNodes[i]].nextToAny; savedPull[i] = req[stageNodes[i]].pullTo;
                    req[stageNodes[i]].nextToAny = null; req[stageNodes[i]].pullTo = -1;
                }
                for (int attempt = 0; attempt < triesPerStage && !ok; attempt++)
                {
                    int linksBefore = res.links.Count;
                    ok = PlaceStage(n, parent, req, dir, rng, boxes, res, stageNodes);
                    if (!ok)
                    {
                        foreach (int id in stageNodes) boxes[id] = null;
                        res.links.RemoveRange(linksBefore, res.links.Count - linksBefore);
                    }
                }
                for (int i = 0; i < stageNodes.Count; i++)
                { req[stageNodes[i]].nextToAny = savedNext[i]; req[stageNodes[i]].pullTo = savedPull[i]; }
            }
            if (!ok) { allOk = false; break; }
        }
        }
        if (!allOk) return null;

        // Нормализация: сдвигаем всё так, чтобы осталось место под оболочку.
        int minX = int.MaxValue, minY = int.MaxValue, maxX = int.MinValue, maxY = int.MinValue;
        for (int i = 0; i < n; i++)
        {
            if (boxes[i] == null) return null;
            minX = Mathf.Min(minX, boxes[i].x0); minY = Mathf.Min(minY, boxes[i].y0);
            maxX = Mathf.Max(maxX, boxes[i].X1); maxY = Mathf.Max(maxY, boxes[i].Y1);
        }
        const int Shell = 3;
        int dx = Shell - minX, dy = Shell - minY;
        for (int i = 0; i < n; i++) { boxes[i].x0 += dx; boxes[i].y0 += dy; res.rooms.Add(boxes[i]); }
        foreach (var l in res.links)
        {
            if (l.vertical) { l.from += dx; l.to += dx; l.wall += dy; l.wallEnd += dy; }
            else            { l.from += dy; l.to += dy; l.wall += dx; l.wallEnd += dx; }
        }
        res.Width = maxX - minX + 1 + Shell * 2;
        res.Height = maxY - minY + 1 + Shell * 2;
        return res;
    }

    private static int[] Size(RoomReq r, System.Random rng) => new[]
    {
        Mathf.Clamp(r.minW + rng.Next(3), r.minW, r.maxW),
        // ⚠️ Потолок берём из САМОГО требования: зал просит больше, обычная комната — как раньше.
        Mathf.Clamp(r.minH + rng.Next(r.maxH > MaxRoomHeight ? 6 : 2), r.minH,
                    Mathf.Min(r.maxH, MaxHallHeight))
    };

    /// <summary>
    /// Разместить узлы одного ЭТАПА относительно уже стоящих комнат. false — этап не встал целиком;
    /// откат делает вызывающий, и откатывает он только этот этап.
    /// </summary>
    private static bool PlaceStage(int n, int[] parent, RoomReq[] req, LinkDir[] dir,
                                   System.Random rng, RoomBox[] boxes, Result res, List<int> stageNodes)
    {
        // Свободно ли место: со всеми уложенными, кроме родителя, держим зазор Margin.
        System.Func<RoomBox, int, bool> free = (cand, exceptId) =>
        {
            for (int i = 0; i < n; i++)
            {
                var p = boxes[i];
                if (p == null || p.id == exceptId) continue;
                if (cand.x0 - Margin <= p.X1 && p.x0 - Margin <= cand.X1 &&
                    cand.y0 - Margin <= p.Y1 && p.y0 - Margin <= cand.Y1) return false;
            }
            return true;
        };

        {
            foreach (int k in stageNodes)
            {
                int v = parent[k];
                if (v < 0 || boxes[v] == null) return false;
                var pb = boxes[v];
                var sz = Size(req[k], rng);
                int cw = sz[0], ch = sz[1];

                // Какие направления вообще дозволены ролью на этом ребре.
                var dirs = new List<LinkDir>();
                switch (dir[k])
                {
                    case LinkDir.Up:         dirs.Add(LinkDir.Up); break;
                    case LinkDir.Down:       dirs.Add(LinkDir.Down); break;
                    case LinkDir.Horizontal: dirs.Add(LinkDir.Horizontal); break;
                    // ⚠️ У СВОБОДНОГО РЕБРА ПЕРЕВЕС У БОКОВОГО. Механизмы почти все требуют подъёма
                    // (ворота), и если развязки между ними тоже лезут вверх, уровень вырождается в
                    // башню — первый же прогон дал 17 в ширину при 42 в высоту. Боковое ребро
                    // добавляем дважды, чтобы уровень разворачивался в стороны.
                    default:
                        dirs.Add(LinkDir.Horizontal); dirs.Add(LinkDir.Horizontal);
                        dirs.Add(LinkDir.Up); dirs.Add(LinkDir.Down); break;
                }
                for (int i = dirs.Count - 1; i > 0; i--)
                { int j = rng.Next(i + 1); var tmp = dirs[i]; dirs[i] = dirs[j]; dirs[j] = tmp; }
                // Желаемое направление уходит в начало очереди — но остальные остаются запасными.
                if (req[k].prefer != LinkDir.Any && dirs.Count > 1)
                { dirs.Remove(req[k].prefer); dirs.Insert(0, req[k].prefer); }

                RoomBox placed = null; RoomLink link = null;
                var placedDir = LinkDir.Any; bool placedRight = false, lastRight = false;
                // ⚠️ ЗАМЫКАНИЕ ПЕТЛИ — ЖЕЛАНИЕ, А НЕ УЛЬТИМАТУМ. Первый проход ищет место, где звено
                // касается комнаты маршрута; не нашлось — второй проход ставит его как обычно.
                // 🐞 Жёстким это требование роняло ВСЮ раскладку: сошлось 4 попытки из 40 (петля при
                // этом замыкалась в 4 из 4 — то есть механизм верный, губила именно безысходность).
                bool relaxNextTo = false;
                // Притяжение: перебираем ВСЕ варианты и берём ближайший к цели, а не первый годный.
                bool pulling = req[k].pullTo >= 0 && req[k].pullTo < n && boxes[req[k].pullTo] != null;
                long bestScore = long.MaxValue;
                for (int pass = 0; pass < 2 && placed == null; pass++)
                {
                relaxNextTo = pass == 1;
                foreach (var d in dirs)
                {
                    if (placed != null && !pulling) break;
                    for (int shot = 0; shot < 12 && (placed == null || pulling); shot++)
                    {
                        RoomBox cand; RoomLink lk;
                        if (d == LinkDir.Horizontal)
                        {
                            // ⭐ ПОЛЫ СОСЕДЕЙ НЕ ОБЯЗАНЫ СОВПАДАТЬ — достаточно, чтобы разница влезала
                            // в дотяжку: проём режется от НИЖНЕГО пола вверх, и получается ступенька.
                            // 🐞 Жёсткое выравнивание не давало горизонтальной связи НИ ОДНОЙ степени
                            // свободы: ребёнку фиксировались и X, и Y, оставалось ровно два положения
                            // (слева/справа), и укладка не сходилась на больших деревьях. Заодно это
                            // чинит вид: уровень перестаёт быть плоской лентой из комнат в один ряд.
                            // ⚠️ СТОРОНА — МОНЕТКОЙ, И ЭТО ПРОВЕРЕНО. Я пробовал разворачивать основной
                            // маршрут всегда в одну сторону, думая, что «гармошка» и сводит спавн с
                            // финишем. Замер сказал: без разворота спавн→финиш 53% габарита (худший
                            // случай 46%), с разворотом 54% (худший 38%) — то есть пользы ноль, а
                            // худший случай хуже. Сводил их не изгиб маршрута, а то, что финиш вообще
                            // ставился не в его конец (см. Node.isFinish в FreeMazeBuilder).
                            bool toRight = rng.Next(2) == 0;
                            lastRight = toRight;
                            int x = toRight ? pb.X1 + 2 : pb.x0 - 1 - cw;
                            // ⭐ ПЕРЕПАД ПОЛОВ НЕ ОГРАНИЧЕН ДОТЯЖКОЙ (идея игрока): не хватает вылета —
                            // в проёме достраиваются СТУПЕНИ. Это тот же зигзаг, только применённый к
                            // связи. Благодаря этому у горизонтального ребра появляется свобода по Y,
                            // которой раньше не было вовсе (фиксировались и X, и Y — ровно два
                            // положения на выбор, оттого укладка и не сходилась на больших деревьях).
                            // ⚠️ Разумный предел всё же нужен: слишком высокая лестница в одну клетку
                            // ширины читается как колодец, а не как проход.
                            int drop = rng.Next(-MaxFloorStep, MaxFloorStep + 1);
                            cand = new RoomBox { id = k, x0 = x, y0 = pb.y0 + drop, w = cw, h = ch };
                            int lo = Mathf.Max(cand.y0, pb.y0), hi = Mathf.Min(cand.Y1, pb.Y1);
                            if (hi - lo + 1 < MinOverlap) continue;
                            // Проём идёт от НИЖНЕГО пола до верхнего плюс запас — войти можно с любой
                            // стороны, а ступени в нём достроит рендер.
                            int lowFloor = Mathf.Min(cand.y0, pb.y0);
                            int topNeed = Mathf.Max(cand.y0, pb.y0) + MinOverlap - 1;
                            if (topNeed > hi) continue;
                            int wcol = toRight ? pb.X1 + 1 : pb.x0 - 1;
                            lk = new RoomLink { a = v, b = k, vertical = false,
                                                from = lowFloor, to = topNeed, wall = wcol, wallEnd = wcol };
                        }
                        else
                        {
                            bool up = d == LinkDir.Up;
                            int y = up ? pb.Y1 + 2 : pb.y0 - 1 - ch;
                            // Сдвиг вбок случайный, но общий отрезок не меньше MinOverlap.
                            int lo = pb.x0 - (cw - MinOverlap), hi = pb.X1 - (MinOverlap - 1);
                            if (req[k].alignX) { lo = pb.x0; hi = pb.x0; }   // этаж под этажом, столбец в столбец
                            if (hi < lo) continue;
                            int x = lo + rng.Next(hi - lo + 1);
                            cand = new RoomBox { id = k, x0 = x, y0 = y, w = cw, h = ch };
                            int l2 = Mathf.Max(cand.x0, pb.x0), h2 = Mathf.Min(cand.X1, pb.X1);
                            if (h2 - l2 + 1 < MinOverlap) continue;
                            int wrow = up ? pb.Y1 + 1 : pb.y0 - 1;
                            lk = new RoomLink { a = up ? v : k, b = up ? k : v, vertical = true,
                                                from = l2, to = h2, wall = wrow, wallEnd = wrow };
                        }
                        if (!free(cand, v)) continue;
                        // Замыкание петли: звено должно касаться заданной комнаты с достаточным
                        // перекрытием — иначе прохода между ними не прорезать.
                        if (!relaxNextTo && req[k].nextTo >= 0 && req[k].nextTo < n
                            && boxes[req[k].nextTo] != null
                            && !Touches(cand, boxes[req[k].nextTo])) continue;
                        if (!relaxNextTo && req[k].nextToAny != null && req[k].nextToAny.Count > 0)
                        {
                            bool anyOk = false;
                            foreach (int t2 in req[k].nextToAny)
                                if (t2 >= 0 && t2 < n && boxes[t2] != null && t2 != v
                                    && Touches(cand, boxes[t2]) && !Blocked(cand, boxes[t2], boxes, n, k, v))
                                { anyOk = true; break; }
                            if (!anyOk) continue;
                        }
                        if (pulling)
                        {
                            var tb = boxes[req[k].pullTo];
                            long ddx = (cand.x0 + cand.X1) / 2 - (tb.x0 + tb.X1) / 2;
                            long ddy = (cand.y0 + cand.Y1) / 2 - (tb.y0 + tb.Y1) / 2;
                            long score = ddx * ddx + ddy * ddy;
                            if (score >= bestScore) continue;
                            bestScore = score;
                        }
                        placed = cand; link = lk; placedDir = d; placedRight = lastRight;
                    }
                }
                }
                if (placed == null) return false;              // этот этап не встал — переиграем его
                boxes[k] = placed; res.links.Add(link);
            }
        }
        return true;
    }

    /// <summary>
    /// Самопроверка раскладки: комнаты не пересекаются, связанные соприкасаются через ОДНУ стену,
    /// общий отрезок достаточен для прохода. Возвращает пустую строку, если всё в порядке.
    /// ⚠️ Заведена не для красоты: раскладка — фундамент, и её ошибка проявится далеко от места,
    /// где сделана (кривой уровень вместо кривого прямоугольника).
    /// </summary>
    public static string Validate(Result r)
    {
        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < r.rooms.Count; i++)
        for (int j = i + 1; j < r.rooms.Count; j++)
        {
            var a = r.rooms[i]; var b = r.rooms[j];
            if (a.x0 <= b.X1 && b.x0 <= a.X1 && a.y0 <= b.Y1 && b.y0 <= a.Y1)
                sb.Append($"комнаты {a} и {b} ПЕРЕСЕКАЮТСЯ; ");
        }
        var byId = new Dictionary<int, RoomBox>();
        foreach (var rm in r.rooms) byId[rm.id] = rm;
        foreach (var l in r.links)
        {
            var a = byId[l.a]; var b = byId[l.b];
            if (l.to - l.from + 1 < MinOverlap) sb.Append($"связь {l.a}-{l.b}: общий отрезок мал; ");
            if (l.vertical)
            {
                if (b.y0 != a.Y1 + 2) sb.Append($"связь {l.a}-{l.b}: между ними не одна стена; ");
                if (l.from < Mathf.Max(a.x0, b.x0) || l.to > Mathf.Min(a.X1, b.X1))
                    sb.Append($"связь {l.a}-{l.b}: отрезок вне общей части; ");
            }
            else
            {
                bool ok = b.x0 == a.X1 + 2 || a.x0 == b.X1 + 2;
                if (!ok) sb.Append($"связь {l.a}-{l.b}: между ними не одна стена; ");
                // Полы могут различаться: перепад в проёме разбивается ступенями (см. MaxFloorStep).
                if (Mathf.Abs(a.y0 - b.y0) > MaxFloorStep)
                    sb.Append($"связь {l.a}-{l.b}: перепад полов сверх допустимого; ");
                if (Mathf.Min(a.Y1, b.Y1) - Mathf.Max(a.y0, b.y0) + 1 < MinOverlap)
                    sb.Append($"связь {l.a}-{l.b}: комнаты почти не перекрываются по высоте; ");
            }
        }
        return sb.ToString();
    }
}
