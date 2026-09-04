using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ⭐ РАСКЛАДКА РАСТАЛКИВАНИЕМ — комнаты не обязаны делить стену.
///
/// Зачем понадобилась вторая раскладка. В <see cref="RoomLayout"/> связь двух комнат — это ДЫРА В
/// ОБЩЕЙ СТЕНЕ, значит комнаты стоят вплотную, и ребёнок кладётся жадно к стене родителя внутри
/// выделенной ветке полуплоскости. Замер (2026-09-03): 16 комнат — 20/20, 24 — 20/20, 32 — 12/20,
/// 40 — 7/20, 56 — 0/20. Больше попыток не спасает: 2000 попыток на 40 комнатах дали 3/5 за 55 мс.
/// Это один и тот же корень у трёх жалоб игрока — «лабиринт маленький», «ветки ни о чём» и «петель
/// нет»: длинная ветка = больше комнат вплотную = чаще несход, а лишним ребром могли стать только
/// комнаты через одну стену (петель выходило 0.4 на уровень).
///
/// Здесь связь — ТОННЕЛЬ, прорезаемый сквозь камень (ровно коридоры из статьи про подземелья).
/// Комнаты свободно висят в пространстве, поэтому раскладка сводится к двум силам:
///   • РАСТАЛКИВАНИЕ — пересекающиеся комнаты разъезжаются по короткой оси;
///   • ПРУЖИНА ПО РЕБРУ ДЕРЕВА — связанные комнаты держатся на дистанции «бок к боку плюс зазор»
///     в том направлении, которое диктует роль (ворота требуют подъёма, мост — бокового хода).
/// Направление ребра выбирается ОДИН РАЗ на старте и дальше не меняется: иначе комната гуляет между
/// «сверху» и «сбоку», силы дёргают её туда-обратно и раскладка не сходится.
///
/// ⚠️ ЧТО ЭТА РАСКЛАДКА НЕ РЕШАЕТ. Она гарантирует только непересечение и близость связанных комнат.
/// Можно ли реально прорезать ход между двумя комнатами, не вспоров третью, решает уже прокладка
/// коридоров по сетке — здесь об этом судить нечем.
/// </summary>
public static class RoomSpread
{
    /// <summary>Камня между комнатами. Двух клеток мало: тоннель прорезается ВНУТРИ этого камня,
    /// и ему нужен свой ряд плюс стенки, иначе ход сливается со соседней комнатой.</summary>
    /// <remarks>
    /// Замер зазора (4 сида, прорезание тоннелей): 3 → 16 комнат 3/6 и 24 комнаты 0/6; 4 → 3/4 и 2/4;
    /// 5 → 3/4 и 4/4; 6 → раскладка уже начинает не сходиться. Зазор — это камень, СКВОЗЬ который
    /// прорезаются ходы: из трёх клеток первый же тоннель забирает две, и второму пройти негде.
    /// </remarks>
    public static int Gap = 5;

    /// <summary>
    /// Насколько длинным может выйти ход между связанными комнатами (сумма зазоров по осям — это и есть
    /// длина Г-образного тоннеля). Дальше ход читается не как проход между комнатами, а как кишка.
    ///
    /// Замер сходимости по порогу (12 сидов, 5 механизмов): порог 10 давал 0/12 уже на 32 комнатах,
    /// 14 — 3/12, 18 — 8/12, 24 — 8/12. При этом МЕДИАНА длины хода всего 3-5 клеток на любом размере:
    /// подавляющее большинство комнат стоят почти вплотную, а порог режет редкий хвост. Поэтому 20 —
    /// не «разрешаем длинные коридоры», а «не выбрасываем всю раскладку из-за одного длинного».
    /// </summary>
    public static int MaxReachGap = 20;

    /// Диагностика: по какой из проверок приёмки заход отвалился. Нужна, чтобы не гадать.
    public static int RejOverlap, RejTooNear, RejTooFar, RejNoOverlap;
    public static void ResetStats() { RejOverlap = RejTooNear = RejTooFar = RejNoOverlap = 0; }

    public class Box
    {
        public int id;
        public int x0, y0, w, h;
        public int X1 => x0 + w - 1;
        public int Y1 => y0 + h - 1;
        public float cx, cy;                 // центр в дробных координатах — им работает расталкивание
        public override string ToString() => $"#{id}({x0},{y0} {w}×{h})";
    }

    /// <summary>Ребро дерева после раскладки: кто с кем и по какой оси разошлись.</summary>
    public class Edge
    {
        public int a, b;                     // a — родитель, b — ребёнок
        public bool vertical;                // b выше/ниже a (подъём) либо сбоку
        public bool positive;                // vertical: b выше a. иначе: b правее a
        public bool extra;                   // ребро сверх дерева (петля), механизма на нём нет
    }

    public class Result
    {
        public List<Box> rooms = new List<Box>();
        public List<Edge> edges = new List<Edge>();
        public int Width, Height;
    }

    /// <summary>
    /// Разложить дерево расталкиванием. Возвращает null, если за <paramref name="tries"/> заходов
    /// раскладка не сошлась — недобор честнее молчаливой подмены.
    /// </summary>
    public static Result Build(int n, int[] parent, RoomLayout.RoomReq[] req, RoomLayout.LinkDir[] dir,
                               System.Random rng, int tries = 6)
    {
        for (int t = 0; t < tries; t++)
        {
            var res = TryBuild(n, parent, req, dir, rng);
            if (res != null) return res;
        }
        return null;
    }

    private static Result TryBuild(int n, int[] parent, RoomLayout.RoomReq[] req,
                                   RoomLayout.LinkDir[] dir, System.Random rng)
    {
        var box = new Box[n];
        var kids = new List<int>[n];
        for (int i = 0; i < n; i++) kids[i] = new List<int>();
        int root = -1;
        for (int i = 0; i < n; i++) { if (parent[i] < 0) root = i; else kids[parent[i]].Add(i); }
        if (root < 0) return null;

        for (int i = 0; i < n; i++)
        {
            var r = req[i] ?? new RoomLayout.RoomReq();
            box[i] = new Box
            {
                id = i,
                w = Mathf.Clamp(r.minW + rng.Next(4), r.minW, r.maxW),
                h = Mathf.Clamp(r.minH + rng.Next(2), r.minH, Mathf.Min(r.maxH, RoomLayout.MaxRoomHeight))
            };
        }

        // ── Направление каждого ребра выбираем ОДИН РАЗ и больше не меняем ──
        // ⚠️ У свободного ребра перевес у бокового: механизмы почти все требуют подъёма, и если
        // развязки между ними тоже лезут вверх, уровень вырождается в башню (обжигался на решётке).
        var vert = new bool[n];
        var pos = new bool[n];
        for (int i = 0; i < n; i++)
        {
            switch (dir[i])
            {
                case RoomLayout.LinkDir.Up:         vert[i] = true;  pos[i] = true;  break;
                case RoomLayout.LinkDir.Down:       vert[i] = true;  pos[i] = false; break;
                case RoomLayout.LinkDir.Horizontal: vert[i] = false; pos[i] = rng.Next(2) == 0; break;
                default:
                    vert[i] = rng.Next(3) == 0;                     // вверх реже, чем вбок
                    pos[i] = vert[i] ? rng.Next(4) > 0 : rng.Next(2) == 0;
                    break;
            }
        }

        // ── Начальная расстановка: обход дерева, ребёнок отходит от родителя в свою сторону ──
        box[root].cx = 0f; box[root].cy = 0f;
        var order = new List<int>();
        var stack = new Stack<int>(); stack.Push(root);
        while (stack.Count > 0)
        { int v = stack.Pop(); order.Add(v); foreach (int k in kids[v]) stack.Push(k); }
        foreach (int v in order)
        foreach (int k in kids[v])
        {
            float sep = Sep(box[v], box[k], vert[k]);
            float jitter = (float)(rng.NextDouble() * 6.0 - 3.0);
            if (vert[k])
            { box[k].cx = box[v].cx + jitter; box[k].cy = box[v].cy + (pos[k] ? sep : -sep); }
            else
            { box[k].cx = box[v].cx + (pos[k] ? sep : -sep); box[k].cy = box[v].cy + jitter; }
        }

        // ── Релаксация: пружины по рёбрам + расталкивание пересечений ──
        // ⚠️ ОТЖИГ ОБЯЗАТЕЛЕН. 🐞 При постоянной силе пружины она и расталкивание тянут друг против
        // друга и равновесия нет вовсе: замер дал 12/20 на шестнадцати комнатах и 0/20 начиная с
        // двадцати четырёх, причём ВСЕ отказы — «пересечение». Гасим пружину к концу, чтобы последнее
        // слово осталось за расталкиванием, а потом добиваем чистым расталкиванием без пружин.
        const int Iters = 220;
        for (int it = 0; it < Iters; it++)
        {
            float k = 0.5f * (1f - it / (float)Iters);
            // Пружины: связанные комнаты держим на дистанции «бок к боку плюс зазор» по своей оси,
            // а поперёк оси стягиваем к нулю, чтобы ход между ними был коротким и прямым.
            foreach (int v in order)
            foreach (int c in kids[v])
            {
                var p = box[v]; var q = box[c];
                float sep = Sep(p, q, vert[c]);
                float tx, ty;
                if (vert[c]) { tx = 0f; ty = pos[c] ? sep : -sep; }
                else { tx = pos[c] ? sep : -sep; ty = 0f; }
                float ex = (q.cx - p.cx) - tx, ey = (q.cy - p.cy) - ty;
                q.cx -= ex * k * 0.5f; q.cy -= ey * k * 0.5f;
                p.cx += ex * k * 0.5f; p.cy += ey * k * 0.5f;
            }
            // Расталкивание: пересечение разводим по КОРОТКОЙ оси — так комнаты не улетают далеко.
            bool any = false;
            for (int i = 0; i < n; i++)
            for (int j = i + 1; j < n; j++)
            {
                var a = box[i]; var b = box[j];
                float needX = (a.w + b.w) * 0.5f + Gap, needY = (a.h + b.h) * 0.5f + Gap;
                float dx = b.cx - a.cx, dy = b.cy - a.cy;
                float ox = needX - Mathf.Abs(dx), oy = needY - Mathf.Abs(dy);
                if (ox <= 0f || oy <= 0f) continue;
                any = true;
                if (ox < oy)
                {
                    float push = ox * 0.5f * (dx >= 0f ? 1f : -1f);
                    b.cx += push; a.cx -= push;
                }
                else
                {
                    float push = oy * 0.5f * (dy >= 0f ? 1f : -1f);
                    b.cy += push; a.cy -= push;
                }
            }
            if (!any && it > 40) break;
        }

        // ── Чистое расталкивание: пружин больше нет, значит равновесие достижимо ──
        // Слэк +1 клетка — запас на округление к целым: две комнаты, стоящие в дробных координатах
        // ровно на пределе, после округления снова пересекутся.
        for (int it = 0; it < 150; it++)
        {
            bool any = false;
            for (int i = 0; i < n; i++)
            for (int j = i + 1; j < n; j++)
            {
                var a = box[i]; var b = box[j];
                float needX = (a.w + b.w) * 0.5f + Gap + 1f, needY = (a.h + b.h) * 0.5f + Gap + 1f;
                float dx = b.cx - a.cx, dy = b.cy - a.cy;
                float ox = needX - Mathf.Abs(dx), oy = needY - Mathf.Abs(dy);
                if (ox <= 0f || oy <= 0f) continue;
                any = true;
                if (ox < oy)
                { float push = ox * 0.55f * (dx >= 0f ? 1f : -1f); b.cx += push; a.cx -= push; }
                else
                { float push = oy * 0.55f * (dy >= 0f ? 1f : -1f); b.cy += push; a.cy -= push; }
            }
            if (!any) break;
        }

        // ── Целые координаты и жёсткая доводка: после округления пересечения могут вернуться ──
        for (int i = 0; i < n; i++)
        {
            box[i].x0 = Mathf.RoundToInt(box[i].cx - box[i].w * 0.5f);
            box[i].y0 = Mathf.RoundToInt(box[i].cy - box[i].h * 0.5f);
        }
        for (int pass = 0; pass < 150; pass++)
        {
            bool any = false;
            for (int i = 0; i < n; i++)
            for (int j = i + 1; j < n; j++)
            {
                var a = box[i]; var b = box[j];
                if (a.x0 - Gap > b.X1 || b.x0 - Gap > a.X1) continue;
                if (a.y0 - Gap > b.Y1 || b.y0 - Gap > a.Y1) continue;
                any = true;
                // ⚠️ Сдвиг считаем ПО КАЖДОМУ направлению отдельно и толкаем именно в то, которое
                // выбрали. 🐞 Сперва я брал минимум по двум направлениям, а сторону выбирал по центрам —
                // и комнаты разъезжались на меньшую величину, чем нужно: доводка не сходилась никогда
                // (замер: 227 отказов «пересечение» из 240 заходов, то есть 0/20).
                int bRight = a.X1 + Gap + 1 - b.x0;      // b вправо / a влево
                int bLeft  = b.X1 + Gap + 1 - a.x0;      // b влево / a вправо
                int bUp    = a.Y1 + Gap + 1 - b.y0;      // b вверх / a вниз
                int bDown  = b.Y1 + Gap + 1 - a.y0;      // b вниз / a вверх
                int cx = Mathf.Min(bRight, bLeft), cy = Mathf.Min(bUp, bDown);
                if (cx <= cy)
                {
                    int s = bRight <= bLeft ? 1 : -1, d = Mathf.Min(bRight, bLeft);
                    b.x0 += s * ((d + 1) / 2); a.x0 -= s * (d / 2);
                }
                else
                {
                    int s = bUp <= bDown ? 1 : -1, d = Mathf.Min(bUp, bDown);
                    b.y0 += s * ((d + 1) / 2); a.y0 -= s * (d / 2);
                }
            }
            if (!any) break;
        }

        // ── СЖАТИЕ: подтягиваем поддеревья к родителю, пока не упрутся ──
        // 🐞 После отжига пружины уже не держат, и связанные комнаты расходятся: замер дал 39 отказов
        // «далеко» из 48 заходов при нуле пересечений. Двигаем не одну комнату, а ВСЁ ПОДДЕРЕВО —
        // сдвиг одной комнаты немедленно растянул бы её собственные рёбра, и сжатие ходило бы по кругу.
        var sub = new List<int>[n];
        for (int i = 0; i < n; i++) sub[i] = new List<int>();
        for (int oi = order.Count - 1; oi >= 0; oi--)
        {
            int v = order[oi];
            sub[v].Add(v);
            foreach (int c in kids[v]) sub[v].AddRange(sub[c]);
        }
        System.Func<int, int, int, bool> shift = (rootId, dx, dy) =>
        {
            var moved = sub[rootId];
            foreach (int id in moved) { box[id].x0 += dx; box[id].y0 += dy; }
            var inSub = new bool[n];
            foreach (int id in moved) inSub[id] = true;
            bool bad = false;
            foreach (int id in moved)
            {
                var a = box[id];
                for (int j = 0; j < n && !bad; j++)
                {
                    if (inSub[j]) continue;
                    var b = box[j];
                    if (a.x0 - Gap <= b.X1 && b.x0 - Gap <= a.X1 &&
                        a.y0 - Gap <= b.Y1 && b.y0 - Gap <= a.Y1) bad = true;
                }
                if (bad) break;
            }
            if (bad) { foreach (int id in moved) { box[id].x0 -= dx; box[id].y0 -= dy; } return false; }
            return true;
        };
        for (int round = 0; round < 24; round++)
        {
            bool moved = false;
            for (int i = 0; i < n; i++)
            {
                if (parent[i] < 0) continue;
                var p = box[parent[i]]; var q = box[i];
                int gx = Mathf.Max(p.x0 - q.X1, q.x0 - p.X1) - 1;
                int gy = Mathf.Max(p.y0 - q.Y1, q.y0 - p.Y1) - 1;
                if (gx > 0 && shift(i, q.x0 > p.x0 ? -1 : 1, 0)) { moved = true; continue; }
                if (gy > 0 && shift(i, 0, q.y0 > p.y0 ? -1 : 1)) moved = true;
            }
            if (!moved) break;
        }

        // ── Приёмка раскладки ──
        for (int i = 0; i < n; i++)
        for (int j = i + 1; j < n; j++)
        {
            var a = box[i]; var b = box[j];
            if (a.x0 - Gap <= b.X1 && b.x0 - Gap <= a.X1 &&
                a.y0 - Gap <= b.Y1 && b.y0 - Gap <= a.Y1)
            { RejOverlap++; return null; }                                // доводка не справилась
        }
        var res2 = new Result();
        for (int i = 0; i < n; i++)
        {
            if (parent[i] < 0) continue;
            var p = box[parent[i]]; var q = box[i];
            // ⚠️ ТОННЕЛЮ НЕ НУЖНО ПЕРЕКРЫТИЕ ПОПЕРЁК ОСИ. Требование «общая стена ≥2 клеток» было у
            // прежней раскладки, потому что проход там — дыра в общей стене. Ход гнётся, поэтому
            // единственное, что важно, — чтобы он был КОРОТКИМ: длинный читается не как проход между
            // комнатами, а как отдельная кишка. Мерим зазор прямоугольников по обеим осям и складываем:
            // это и есть длина Г-образного хода.
            int gx = Mathf.Max(p.x0 - q.X1, q.x0 - p.X1) - 1;
            int gy = Mathf.Max(p.y0 - q.Y1, q.y0 - p.Y1) - 1;
            int len = Mathf.Max(0, gx) + Mathf.Max(0, gy);
            if (len > MaxReachGap) { RejTooFar++; return null; }
            res2.edges.Add(new Edge { a = p.id, b = q.id, vertical = vert[i], positive = pos[i] });
        }

        // Сдвиг в положительные координаты и габарит.
        int minX = int.MaxValue, minY = int.MaxValue, maxX = int.MinValue, maxY = int.MinValue;
        for (int i = 0; i < n; i++)
        {
            minX = Mathf.Min(minX, box[i].x0); minY = Mathf.Min(minY, box[i].y0);
            maxX = Mathf.Max(maxX, box[i].X1); maxY = Mathf.Max(maxY, box[i].Y1);
        }
        for (int i = 0; i < n; i++) { box[i].x0 -= minX; box[i].y0 -= minY; res2.rooms.Add(box[i]); }
        res2.Width = maxX - minX + 1; res2.Height = maxY - minY + 1;
        return res2;
    }

    /// <summary>Дистанция между центрами, при которой комнаты стоят бок о бок с зазором Gap.</summary>
    private static float Sep(Box a, Box b, bool vertical)
        => vertical ? (a.h + b.h) * 0.5f + Gap + 1f : (a.w + b.w) * 0.5f + Gap + 1f;
}
