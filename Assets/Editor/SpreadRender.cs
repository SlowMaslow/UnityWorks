using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ⭐ РЕНДЕР РАСКЛАДКИ РАСТАЛКИВАНИЕМ: комнаты вырезаются в сплошном камне, а связи между ними —
/// ТОННЕЛИ, прокладываемые поиском пути.
///
/// Чем отличается от рендера по <see cref="RoomLayout"/>: там связь — дыра в общей стене, её положение
/// известно заранее из раскладки. Здесь комнаты стоят порознь, и ход надо ещё ПРОЛОЖИТЬ. Поиск пути
/// заодно решает две задачи, которые в модели общей стены просто не возникали:
///   • ход не должен вспороть ТРЕТЬЮ комнату по дороге;
///   • два хода не должны слиться — иначе получается развилка, которой нет в плане головоломки,
///     и через неё обходится механизм (модель проходимости объявит ворота декоративными).
///
/// ⚠️ ХОД ПРОРЕЗАЕТСЯ ШТАМПОМ 2×2, А НЕ ПО ОДНОЙ КЛЕТКЕ. Коридор в одну клетку непроходим: игроку
/// нужен ряд воздуха над полом. Штамп 2×2 даёт горизонтальному ходу высоту 2 (пол под ним остаётся
/// камнем), а вертикальному — ширину 2, и в такой шахте есть за что цепляться.
/// </summary>
public static class SpreadRender
{
    /// <summary>Отступ камня по краям сетки.</summary>
    private const int Pad = 3;

    /// Диагностика: почему заход не сложился.
    public static int FailRoute, FailShaft, EdgesTried, EdgesFailed, FailFirstIdx, FailGap;
    public static void ResetStats()
    { FailRoute = FailShaft = EdgesTried = EdgesFailed = FailFirstIdx = FailGap = 0; }

    public class Carved
    {
        public char[,] g;
        public int rows, cols;
        public MazeCanvas.RoomRect[] rects;          // комнаты в координатах СЕТКИ (Y вниз)
        public List<List<Vector2Int>> corridors = new List<List<Vector2Int>>();
    }

    /// <summary>Вырезать раскладку. null — если какой-то ход проложить не удалось.</summary>
    public static Carved Build(RoomSpread.Result lay, int roomCount, System.Random rng)
    {
        int cols = lay.Width + Pad * 2, rows = lay.Height + Pad * 2;
        var g = new char[rows, cols];
        for (int r = 0; r < rows; r++) for (int c = 0; c < cols; c++) g[r, c] = '#';

        // Мир (Y вверх, начало в 0) → сетка (Y вниз) со сдвигом на отступ.
        System.Func<int, int> toRow = y => rows - 1 - Pad - y;

        var owner = new int[rows, cols];
        for (int r = 0; r < rows; r++) for (int c = 0; c < cols; c++) owner[r, c] = -1;

        var rects = new MazeCanvas.RoomRect[roomCount];
        var byId = new Dictionary<int, RoomSpread.Box>();
        foreach (var rm in lay.rooms)
        {
            byId[rm.id] = rm;
            for (int y = rm.y0; y <= rm.Y1; y++)
            for (int x = rm.x0; x <= rm.X1; x++)
            { g[toRow(y), x + Pad] = '.'; owner[toRow(y), x + Pad] = rm.id; }
            rects[rm.id] = new MazeCanvas.RoomRect
            { col0 = rm.x0 + Pad, row0 = toRow(rm.Y1), w = rm.w, h = rm.h };
        }

        // ⭐ ПОРЯДОК ПРОКЛАДКИ РЕШАЕТ. Каждый прорезанный ход занимает камень и мешает следующим:
        // между комнатами всего RoomSpread.Gap клеток, и первый тоннель съедает две из них. Замер:
        // падало 5-13% РЁБЕР, а поскольку одного неудачного ребра хватает, чтобы уровень не сложился,
        // это давало 1 уровень из 4. Поэтому перебираем порядок — почти всегда находится рабочий.
        // ⚠️ ПОРЯДОК — ПО ДЕРЕВУ, дальше просто перетасовка. 🐞 Пробовал «длинные ходы первыми»,
        // рассудив, что длинному нужен свободный канал: замер сказал обратное — 16 комнат просели
        // с 6/6 до 2/6. Средний зазор упавшего ребра (9 против медианы 4) вводил в заблуждение: это
        // не «длинные ломаются», а «то, что осталось напоследок, ломается, и оно длиннее среднего».
        var order = new List<RoomSpread.Edge>(lay.edges);
        for (int attempt = 0; attempt < 10; attempt++)
        {
            if (attempt > 0)
                for (int i = order.Count - 1; i > 0; i--)
                { int j = rng.Next(i + 1); var tmp = order[i]; order[i] = order[j]; order[j] = tmp; }

            var gTry = (char[,])g.Clone();
            var corridor = new bool[rows, cols];
            var paths = new List<List<Vector2Int>>();
            bool ok = true;
            foreach (var e in order)
            {
                EdgesTried++;
                var path = Route(gTry, owner, corridor, rows, cols, e.a, e.b, rects);
                if (path == null)
                {
                    EdgesFailed++;
                    if (ok)
                    {
                        FailRoute++;
                        var pa = byId[e.a]; var pb = byId[e.b];
                        int gx = Mathf.Max(pa.x0 - pb.X1, pb.x0 - pa.X1) - 1;
                        int gy = Mathf.Max(pa.y0 - pb.Y1, pb.y0 - pa.Y1) - 1;
                        FailGap += Mathf.Max(0, gx) + Mathf.Max(0, gy);
                    }
                    ok = false; break;                          // ранний выход: неудачный поиск дорог
                }
                foreach (var p in path)
                for (int dr = 0; dr < 2; dr++)
                for (int dc = 0; dc < 2; dc++)
                {
                    int r = p.y + dr, c = p.x + dc;
                    if (owner[r, c] < 0) { gTry[r, c] = '.'; corridor[r, c] = true; }
                }
                paths.Add(path);
            }
            if (!ok) continue;

            // ⚠️ В ВЕРТИКАЛЬНОЙ ШАХТЕ НУЖНЫ УСТУПЫ. Подъём строим ≤ Climb рядов, а шахта бывает
            // длиннее: без уступов она непроходима, и уровень разваливается на два куска.
            for (int k = 0; k < paths.Count; k++) Staircase(gTry, rows, cols, rects, order[k], paths[k], rng);
            ClimbAids(gTry, owner, rows, cols, rects, rng);

            var res = new Carved { g = gTry, rows = rows, cols = cols, rects = rects };
            res.corridors.AddRange(paths);
            return res;
        }
        return null;
    }

    /// <summary>
    /// Проложить ход из комнаты <paramref name="from"/> в комнату <paramref name="to"/>.
    /// Дейкстра по положению штампа 2×2, шаг стоит 1, поворот — ещё 2: прямые ходы читаются как
    /// коридор, а виляющие — как случайная кишка.
    /// </summary>
    private static List<Vector2Int> Route(char[,] g, int[,] owner, bool[,] corridor,
                                          int rows, int cols, int from, int to,
                                          MazeCanvas.RoomRect[] rects)
    {
        // Можно ли поставить штамп левым верхним углом в (r,c).
        System.Func<int, int, bool> fits = (r, c) =>
        {
            if (r < 0 || c < 0 || r + 1 >= rows || c + 1 >= cols) return false;
            // ⚠️ ХОД ДЕРЖИТСЯ ПОДАЛЬШЕ ОТ ЧУЖИХ КОМНАТ. Мало не вспороть третью комнату — нельзя идти
            // и ВПЛОТНУЮ к её стене: тогда стена между ходом и комнатой становится в одну клетку,
            // комната и коридор читаются как один аморфный массив, и уровень перестаёт выглядеть
            // уровнем (первый же прорезанный образец именно так и выглядел).
            for (int dr = -1; dr <= 2; dr++)
            for (int dc = -1; dc <= 2; dc++)
            {
                int r2 = r + dr, c2 = c + dc;
                if (r2 < 0 || c2 < 0 || r2 >= rows || c2 >= cols) continue;
                int o = owner[r2, c2];
                if (o < 0 || o == from || o == to) continue;
                // Своя пара комнат — можно вплотную и внутрь; чужая — держим клетку камня.
                return false;
            }
            // ⚠️ Ход не должен ПЕРЕСЕКАТЬСЯ с другим ходом: слияние двух коридоров — это развилка,
            // которой нет в плане, и через неё обходится механизм.
            // 🐞 Сперва я запрещал даже касание (кольцо в клетку вокруг штампа) — и ходы переставали
            // прокладываться вовсе: из одной комнаты выходит несколько связей, и второй ход утыкался
            // в кольцо первого прямо у выхода (замер: 24 комнаты — 0 из 6 прорезано).
            for (int dr = 0; dr < 2; dr++)
            for (int dc = 0; dc < 2; dc++)
                if (corridor[r + dr, c + dc]) return false;
            return true;
        };
        // ⭐⭐ ХОД ВХОДИТ И ВЫХОДИТ НА УРОВНЕ ПОЛА КОМНАТЫ.
        //
        // 🐞 Пока устье могло быть на любой высоте, уровень рвался ровно в одном месте, а дальше
        // отваливалось всё поддерево (замер по комнатам: сид 7 — достижимы 4 комнаты из 20, первая
        // недостижимая на глубине 3 основного пути). Причина: НА ХОЛД ВСТАЮТ СВЕРХУ. Два коридора,
        // прошедшие рядом, разделены сплошной плитой: снизу это потолок, сверху пол, и перелезть
        // между этажами негде, даже если по вертикали всего три ряда.
        // Выход на уровне пола убирает саму возможность такой плиты между комнатой и ходом.
        System.Func<int, int, bool> atFloorOf = (r, roomId) =>
        {
            if (roomId < 0 || roomId >= rects.Length || rects[roomId].w == 0) return false;
            var rc = rects[roomId];
            return r + 1 == rc.row0 + rc.h - 1;                    // низ штампа = нижний ряд воздуха
        };
        System.Func<int, int, bool> touches = (r, c) =>
        {
            for (int dr = 0; dr < 2; dr++)
            for (int dc = 0; dc < 2; dc++)
                if (owner[r + dr, c + dc] == to) return atFloorOf(r, to);
            return false;
        };

        const int Dirs = 4;
        int[] dr4 = { -1, 1, 0, 0 }, dc4 = { 0, 0, -1, 1 };
        var dist = new int[rows, cols, Dirs + 1];
        var prev = new Vector3Int[rows, cols, Dirs + 1];
        for (int r = 0; r < rows; r++)
        for (int c = 0; c < cols; c++)
        for (int d = 0; d <= Dirs; d++) dist[r, c, d] = int.MaxValue;

        // ⚠️ Стартуем изнутри комнаты: штамп целиком в её клетках. Так ход выходит из комнаты,
        // а не начинается в камне рядом с ней.
        // ⚠️ ОЧЕРЕДЬ — ДВОИЧНАЯ КУЧА. 🐞 Сперва тут был List с поиском минимума перебором: на сетке
        // 60×60 это O(n) на каждое извлечение, и прогон вешал редактор по таймауту.
        var heapKey = new List<int>(); var heapVal = new List<int>();
        System.Action<int, int> push = (key, val) =>
        {
            heapKey.Add(key); heapVal.Add(val);
            int i = heapKey.Count - 1;
            while (i > 0)
            {
                int p = (i - 1) / 2;
                if (heapKey[p] <= heapKey[i]) break;
                int tk = heapKey[p]; heapKey[p] = heapKey[i]; heapKey[i] = tk;
                int tv = heapVal[p]; heapVal[p] = heapVal[i]; heapVal[i] = tv;
                i = p;
            }
        };
        System.Func<int> pop = () =>
        {
            int top = heapVal[0];
            int last = heapKey.Count - 1;
            heapKey[0] = heapKey[last]; heapVal[0] = heapVal[last];
            heapKey.RemoveAt(last); heapVal.RemoveAt(last);
            int i = 0, n2 = heapKey.Count;
            while (true)
            {
                int l = i * 2 + 1, r2 = l + 1, m = i;
                if (l < n2 && heapKey[l] < heapKey[m]) m = l;
                if (r2 < n2 && heapKey[r2] < heapKey[m]) m = r2;
                if (m == i) break;
                int tk = heapKey[m]; heapKey[m] = heapKey[i]; heapKey[i] = tk;
                int tv = heapVal[m]; heapVal[m] = heapVal[i]; heapVal[i] = tv;
                i = m;
            }
            return top;
        };
        int stride = Dirs + 1;
        System.Func<int, int, int, int> pack = (r, c, d) => (r * cols + c) * stride + d;

        int seeded = 0;
        for (int r = 0; r + 1 < rows; r++)
        for (int c = 0; c + 1 < cols; c++)
        {
            bool allFrom = true;
            for (int dr = 0; dr < 2 && allFrom; dr++)
            for (int dc = 0; dc < 2 && allFrom; dc++)
                if (owner[r + dr, c + dc] != from) allFrom = false;
            if (!allFrom) continue;
            if (!atFloorOf(r, from)) continue;                     // выходим только с пола комнаты
            dist[r, c, Dirs] = 0;
            // ⚠️ У СТАРТОВЫХ КЛЕТОК ПРЕДОК ДОЛЖЕН БЫТЬ ЯВНО ПУСТЫМ. 🐞 Vector3Int по умолчанию (0,0,0),
            // а ноль — это валидное направление: восстановление пути уходило в бесконечный цикл и
            // съедало память (падало на OutOfMemory прямо в первом же прогоне).
            prev[r, c, Dirs] = new Vector3Int(-1, -1, -1);
            push(0, pack(r, c, Dirs)); seeded++;
        }
        if (seeded == 0) return null;

        var done = new bool[rows, cols, Dirs + 1];
        while (heapKey.Count > 0)
        {
            int id = pop();
            int cz = id % stride, cell = id / stride, cc = cell % cols, cr = cell / cols;
            if (done[cr, cc, cz]) continue;
            done[cr, cc, cz] = true;
            int cd = dist[cr, cc, cz];

            if (owner[cr, cc] != from && touches(cr, cc))
            {
                var path = new List<Vector2Int>();
                var walk = new Vector3Int(cc, cr, cz);
                while (true)
                {
                    path.Add(new Vector2Int(walk.x, walk.y));
                    var p = prev[walk.y, walk.x, walk.z];
                    if (p.x < 0) break;
                    walk = p;
                }
                path.Reverse();
                return path;
            }

            for (int d = 0; d < Dirs; d++)
            {
                int nr = cr + dr4[d], nc = cc + dc4[d];
                if (!fits(nr, nc)) continue;
                int nd = cd + 1 + (cz != Dirs && cz != d ? 2 : 0);
                if (nd >= dist[nr, nc, d]) continue;
                dist[nr, nc, d] = nd;
                prev[nr, nc, d] = new Vector3Int(cc, cr, cz);
                push(nd, pack(nr, nc, d));
            }
        }
        return null;
    }

    /// <summary>
    /// ⭐ ПЛОЩАДКИ В КОМНАТАХ. Холд в этой игре — камень, НАД которым воздух: то, за что хватаются.
    /// Пустая комната даёт холды только по полу, а её стены и потолок — нет, потому что над ними
    /// камень. Значит комната выше дотяжки — это коробка, из которой не выбраться.
    ///
    /// 🐞 Первый сквозной прогон новой геометрии: из 200+ холдов достижимы 6 — ровно пол стартовой
    /// комнаты. Выход был через шахту в потолке, до неё 8 рядов при подъёме 3. Тот же баг уже был
    /// пойман на решётчатом пути («люк прорезан, а лезть к нему не по чему») — и при переносе
    /// потерялся, потому что там его чинила ступенька в связи, а связей в этом смысле больше нет.
    ///
    /// ⚠️ Площадка УЗКАЯ и прижата к стене: во всю ширину она запечатывает комнату (отдельно
    /// задокументированный баг решётчатого пути).
    /// </summary>
    private static void ClimbAids(char[,] g, int[,] owner, int rows, int cols,
                                  MazeCanvas.RoomRect[] rects, System.Random rng)
    {
        foreach (var rm in rects)
        {
            if (rm.w == 0) continue;
            int top = rm.row0, bottom = rm.row0 + rm.h;      // ряд КАМНЯ под комнатой — это опора
            // Со дна комнаты игрок достаёт на Climb рядов вверх. Всё, что выше, нужно подпереть.
            for (int r = bottom - MazeCanvas.Climb; r > top; r -= MazeCanvas.Climb)
            {
                bool left = rng.Next(2) == 0;
                int lw = Mathf.Min(2, rm.w - 2);
                if (lw < 1) break;
                for (int k = 0; k < lw; k++)
                {
                    int c = left ? rm.col0 + k : rm.col0 + rm.w - 1 - k;
                    if (r >= 0 && r < rows && c >= 0 && c < cols && g[r, c] == '.') g[r, c] = '#';
                }
            }
        }
    }

    /// <summary>
    /// ⭐ ЛЕСТНИЦА ОТ ПОЛА НИЖНЕЙ КОМНАТЫ ДО ВЕРХА ХОДА — ОДНОЙ НЕПРЕРЫВНОЙ ЦЕПОЧКОЙ.
    ///
    /// 🐞 Сперва уступы в шахте и площадки в комнате строились ДВУМЯ независимыми проходами, и цепочка
    /// не смыкалась: площадка комнаты оказывалась на 5 рядов ниже первого уступа шахты при подъёме 3.
    /// Замер был беспощаден — достижимо 2% холдов, финиш 0 из 3. Опора нужна СКВОЗНАЯ: игрок лезет от
    /// пола комнаты вверх, и каждый следующий уступ обязан лежать в пределах дотяжки от предыдущего,
    /// без разницы, комната это ещё или уже шахта.
    /// </summary>
    private static void Staircase(char[,] g, int rows, int cols, MazeCanvas.RoomRect[] rects,
                                  RoomSpread.Edge e, List<Vector2Int> path, System.Random rng)
    {
        int topRow = int.MaxValue;
        foreach (var p in path) topRow = Mathf.Min(topRow, p.y);

        // Нижняя из двух комнат — та, чей пол ниже (больше номер ряда).
        var ra = rects[e.a]; var rb = rects[e.b];
        var low = (ra.row0 + ra.h) >= (rb.row0 + rb.h) ? ra : rb;

        // ⚠️ ЛЕСТНИЦУ СТРОИМ У УСТЬЯ ХОДА, а не по его габариту. 🐞 Сперва я брал крайние столбцы
        // всего пути: у вертикальной шахты это одно и то же, а у горизонтального коридора устье
        // оказывалось на другом конце уровня, и лестница вела в никуда.
        int lowCx = low.col0 + low.w / 2, lowCy = low.row0 + low.h / 2;
        int cMin = path[0].x, best = int.MaxValue;
        foreach (var p in path)
        {
            int d = Mathf.Abs(p.x - lowCx) + Mathf.Abs(p.y - lowCy);
            if (d < best) { best = d; cMin = p.x; }
        }
        int cMax = cMin + 1;
        // ⚠️⚠️ ОТСЧЁТ ИДЁТ ОТ РЯДА КАМНЯ, А НЕ ВОЗДУХА. Холд — это КАМЕНЬ, над которым воздух; пол
        // комнаты как опора лежит на ряд НИЖЕ её нижнего ряда воздуха.
        // 🐞 Ошибка ровно на единицу давала 4 ряда между опорами при подъёме 3 — и не дотягивалось
        // ВЕЗДЕ: замер показывал 2% достижимых холдов, и я дважды искал причину не там.
        int floorRow = low.row0 + low.h;                     // ряд КАМНЯ под нижней комнатой
        if (floorRow <= topRow) return;                      // ход не ведёт вверх — лестница не нужна

        bool left = rng.Next(2) == 0;
        for (int r = floorRow - MazeCanvas.Climb; r > topRow; r -= MazeCanvas.Climb)
        {
            // Ставим уступ в том столбце хода, что свободен; в комнате столбцы хода могут выходить
            // за её стены — тогда прижимаемся к ближайшей стенке комнаты.
            int c = left ? cMin : cMax;
            if (r >= low.row0 && r <= low.row0 + low.h - 1)
                c = Mathf.Clamp(c, low.col0, low.col0 + low.w - 1);
            if (r < 0 || r >= rows || c < 0 || c >= cols) { left = !left; continue; }
            if (g[r, c] == '.') g[r, c] = '#';
            left = !left;
        }
    }
}
