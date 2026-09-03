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
    }

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
    /// <summary>Сколько клеток общей стены нужно, чтобы прорезать проход (люк — 2 клетки минимум).</summary>
    private const int MinOverlap = 2;
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
            Mathf.Clamp(r.minH + rng.Next(2), r.minH, Mathf.Min(r.maxH, MaxRoomHeight))
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
                            // ⚠️ Полы выравниваем: иначе через проём нельзя просто перейти.
                            bool toRight = rng.Next(2) == 0;
                            lastRight = toRight;
                            int x = toRight ? pb.X1 + 2 : pb.x0 - 1 - cw;
                            cand = new RoomBox { id = k, x0 = x, y0 = pb.y0, w = cw, h = ch };
                            int lo = Mathf.Max(cand.y0, pb.y0), hi = Mathf.Min(cand.Y1, pb.Y1);
                            if (hi - lo + 1 < MinOverlap) continue;
                            lk = new RoomLink { a = v, b = k, vertical = false, from = lo, to = hi,
                                                wall = toRight ? pb.X1 + 1 : pb.x0 - 1 };
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
                            lk = new RoomLink { a = up ? v : k, b = up ? k : v, vertical = true,
                                                from = l2, to = h2, wall = up ? pb.Y1 + 1 : pb.y0 - 1 };
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
            if (l.vertical) { l.from += dx; l.to += dx; l.wall += dy; }
            else            { l.from += dy; l.to += dy; l.wall += dx; }
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
                if (a.y0 != b.y0) sb.Append($"связь {l.a}-{l.b}: полы не выровнены; ");
            }
        }
        return sb.ToString();
    }
}
