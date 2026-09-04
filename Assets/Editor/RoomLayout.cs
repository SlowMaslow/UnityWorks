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

    /// <summary>Что комната требует от размера. Заполняется из ролей рецепта.</summary>
    public class RoomReq
    {
        public int minW = 3, minH = 3;
        public int maxW = 8, maxH = MaxRoomHeight;
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
    {
        for (int attempt = 0; attempt < tries; attempt++)
        {
            var res = TryBuild(n, parent, req, dir, rng);
            if (res != null) return res;
        }
        return null;
    }

    private static Result TryBuild(int n, int[] parent, RoomReq[] req, LinkDir[] dir, System.Random rng)
    {
        var boxes = new RoomBox[n];
        var res = new Result();

        // Порядок обхода: родитель всегда раньше ребёнка.
        var order = new List<int>();
        var kids = new List<int>[n];
        for (int i = 0; i < n; i++) kids[i] = new List<int>();
        int root = -1;
        for (int i = 0; i < n; i++) { if (parent[i] < 0) root = i; else kids[parent[i]].Add(i); }
        if (root < 0) return null;
        var stack = new Stack<int>(); stack.Push(root);
        while (stack.Count > 0) { int v = stack.Pop(); order.Add(v); foreach (int k in kids[v]) stack.Push(k); }

        System.Func<RoomReq, int[]> size = r => new[]
        {
            Mathf.Clamp(r.minW + rng.Next(3), r.minW, r.maxW),
            // ⚠️ Потолок берём из САМОГО требования: зал просит больше, обычная комната — как раньше.
            Mathf.Clamp(r.minH + rng.Next(r.maxH > MaxRoomHeight ? 6 : 2), r.minH,
                        Mathf.Min(r.maxH, MaxHallHeight))
        };

        var s0 = size(req[root]);
        boxes[root] = new RoomBox { id = root, x0 = 0, y0 = 0, w = s0[0], h = s0[1] };

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

        // ⭐ ОБЛАСТЬ РОСТА ВЕТКИ. 🐞 Пока комнаты клались просто жадно, укладка не сходилась в
        // половине случаев: ранняя ветка занимала место, нужное поздней, и отката не было.
        // Теперь каждая ветка получает полуплоскость за стеной родителя, и ВСЕ её потомки обязаны
        // остаться в ней — пересечением с областями предков. Ветки перестают лезть друг в друга.
        var region = new int[n, 4];                    // xmin, xmax, ymin, ymax
        const int Far = 100000;
        for (int i = 0; i < n; i++)
        { region[i, 0] = -Far; region[i, 1] = Far; region[i, 2] = -Far; region[i, 3] = Far; }

        foreach (int v in order)
        {
            foreach (int k in kids[v])
            {
                var pb = boxes[v];
                var sz = size(req[k]);
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

                RoomBox placed = null; RoomLink link = null;
                var placedDir = LinkDir.Any; bool placedRight = false, lastRight = false;
                foreach (var d in dirs)
                {
                    if (placed != null) break;
                    for (int shot = 0; shot < 12 && placed == null; shot++)
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
                            if (hi < lo) continue;
                            int x = lo + rng.Next(hi - lo + 1);
                            cand = new RoomBox { id = k, x0 = x, y0 = y, w = cw, h = ch };
                            int l2 = Mathf.Max(cand.x0, pb.x0), h2 = Mathf.Min(cand.X1, pb.X1);
                            if (h2 - l2 + 1 < MinOverlap) continue;
                            int wrow = up ? pb.Y1 + 1 : pb.y0 - 1;
                            lk = new RoomLink { a = up ? v : k, b = up ? k : v, vertical = true,
                                                from = l2, to = h2, wall = wrow, wallEnd = wrow };
                        }
                        // Комната обязана уместиться в область роста СВОЕЙ ветки.
                        if (cand.x0 < region[v, 0] || cand.X1 > region[v, 1] ||
                            cand.y0 < region[v, 2] || cand.Y1 > region[v, 3]) continue;
                        if (!free(cand, v)) continue;
                        placed = cand; link = lk; placedDir = d; placedRight = lastRight;
                    }
                }
                if (placed == null) return null;               // место не нашлось — вся раскладка заново
                boxes[k] = placed; res.links.Add(link);
                // ⚠️ СУЖАЕМ ОБЛАСТЬ ТОЛЬКО ТАМ, ГДЕ ЕСТЬ КОГО РАЗВОДИТЬ. 🐞 Сперва я урезал её у
                // каждого ребёнка — и змейка, шагнув вправо, уже никогда не могла вернуться влево:
                // основной путь упирался в собственную границу, укладка не сходилась в половине
                // случаев. У единственного ребёнка соседей нет, делить нечего, область наследуется
                // целиком. Столкновения всё равно проверяются глобально, так что это безопасно.
                for (int q = 0; q < 4; q++) region[k, q] = region[v, q];
                if (kids[v].Count > 1)
                {
                    if (placedDir == LinkDir.Up)        region[k, 2] = Mathf.Max(region[k, 2], pb.Y1 + 2);
                    else if (placedDir == LinkDir.Down) region[k, 3] = Mathf.Min(region[k, 3], pb.y0 - 2);
                    else if (placedRight)               region[k, 0] = Mathf.Max(region[k, 0], pb.X1 + 2);
                    else                                region[k, 1] = Mathf.Min(region[k, 1], pb.x0 - 2);
                }
            }
        }

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
